using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using PyriteCliCommon;
using PyriteCliCommon.Models;
using PyriteLib;

namespace PyriteCloudRole
{
    public class Scanner
    {
        public CloudQueue WorkQueue { get; set; }
        public CloudBlobClient BlobClient { get; set; }
        public static CloudTableClient TableClient { get; set; }

        private string outputPath, inputPath;

        public Scanner()
        {
      
            // Get storage account
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                CloudConfigurationManager.GetSetting("StorageConnectionString"));

            // Create the clients
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            BlobClient = storageAccount.CreateCloudBlobClient();
            TableClient = storageAccount.CreateCloudTableClient();

            // Retrieve a reference to a queue
            WorkQueue = queueClient.GetQueueReference(
                CloudConfigurationManager.GetSetting("Queue"));

            // Create the queue if it doesn't already exist
            WorkQueue.CreateIfNotExists();
        }

        public async Task DoWorkAsync(CancellationToken cancellationToken)
        {
            CloudQueueMessage retrievedMessage;
            DateTime startTime;
            try
            {
                // Get the next message
                retrievedMessage = await WorkQueue.GetMessageAsync(TimeSpan.FromHours(5), null, null, cancellationToken).ConfigureAwait(false);

                if (retrievedMessage == null) return;

                startTime = DateTime.UtcNow;
            }
            catch
            {
                return;
            }

            var messageContents = retrievedMessage.AsString;
            SlicingOptions slicingOptions = null;
            try
            {
                // Make some fresh directories
                outputPath = Path.Combine(RoleEnvironment.GetLocalResource("output").RootPath, Guid.NewGuid().ToString());
                inputPath = Path.Combine(RoleEnvironment.GetLocalResource("input").RootPath, Guid.NewGuid().ToString());

                Directory.CreateDirectory(outputPath);
                Directory.CreateDirectory(inputPath);

                // Get the message
                slicingOptions = JsonConvert.DeserializeObject<SlicingOptions>(messageContents);

                slicingOptions.Obj = Path.Combine(inputPath, slicingOptions.Obj);
                slicingOptions.Texture = Path.Combine(inputPath, slicingOptions.Texture);

                // ** Prep
                Trace.TraceInformation("Syncing data");
                await VerifySourceDataAsync(slicingOptions, cancellationToken);

                // ** Run
                Trace.TraceInformation("Starting Processing");
                CubeManager manager = new CubeManager(slicingOptions);
                
                if (!string.IsNullOrEmpty(slicingOptions.Texture))
                {
                    slicingOptions.TextureInstance = new Texture(manager.ObjInstance, slicingOptions.Texture);
                }

                var vertexCounts = await manager.GenerateCubesForTextureTileAsync(outputPath, slicingOptions.TextureTile, slicingOptions, cancellationToken).ConfigureAwait(false);

                StorageUtilities.InsertWorkCompleteMetadata(TableClient,
                    new WorkEntity(slicingOptions.CloudResultPath, slicingOptions.CloudResultContainer, slicingOptions.TextureTile.X, slicingOptions.TextureTile.Y, DateTime.UtcNow)
                    {
                        MetadataBase64 = SerializationUtilities.EncodeMetadataToBase64(vertexCounts),
                        StartTime = startTime
                    });

                // ** Check if set is complete
                CheckForComplete(slicingOptions, manager);

                // ** Cleanup
                slicingOptions.TextureInstance?.Dispose();
                Trace.TraceInformation("Writing Results");
                UploadResultData(slicingOptions);

                WorkQueue.DeleteMessage(retrievedMessage);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());

                // Release texure if we have one
                slicingOptions?.TextureInstance?.Dispose();

                // Either delete this message or make it visible again for retry
                if (retrievedMessage.DequeueCount > 3)
                {
                    WorkQueue.DeleteMessage(retrievedMessage);
                }
                else
                {
                    WorkQueue.UpdateMessage(retrievedMessage, TimeSpan.FromSeconds(10), MessageUpdateFields.Visibility);
                }
            }
            finally
            {
                if(Directory.Exists(outputPath))
                {
                    Directory.Delete(outputPath, true);
                }
                if(Directory.Exists(inputPath))
                {
                    Directory.Delete(inputPath, true);
                }
            }
        }

        private void CheckForComplete(SlicingOptions options, CubeManager manager)
        {
            int expectedResults = options.TextureSliceX * options.TextureSliceY;
            
            if (StorageUtilities.GetWorkCompletedCount(TableClient, options.CloudResultPath, options.CloudResultContainer) != expectedResults)
            {
                return;
            }

            var workResults = StorageUtilities.GetWorkCompletedMetadata(TableClient, options.CloudResultPath, options.CloudResultContainer);

            // Write metadata

            CubeMetadata metadata = new CubeMetadata(options.CubeGrid)
            {
                WorldBounds = manager.ObjInstance.Size,
                VirtualWorldBounds = options.ForceCubicalCubes ? manager.ObjInstance.CubicalSize : manager.ObjInstance.Size,
                VertexCount = manager.ObjInstance.VertexList.Count
            };

            // Configure texture slicing metadata
            if (!string.IsNullOrEmpty(options.Texture) && (options.TextureSliceX + options.TextureSliceY) > 2)
            {
                metadata.TextureSetSize = new Vector2(options.TextureSliceX, options.TextureSliceY);
            }
            else
            {
                metadata.TextureSetSize = new Vector2(1, 1);
            }

            var resultsList = workResults.Select(w => 
            SerializationUtilities.DecodeMetadataFromBase64(
                Texture.GetCubeListFromTextureTile(options.TextureSliceY, options.TextureSliceX, w.TextureTileX, w.TextureTileY, manager.ObjInstance), 
                w.MetadataBase64));            

            foreach (var result in resultsList)
            {
                foreach (var cube in result.Keys)
                {
                    metadata.CubeExists[cube.X, cube.Y, cube.Z] = result[cube];
                }
            }
        

            // Write out some json metadata
            string metadataPath = Path.Combine(outputPath, "metadata.json");
            if (File.Exists(metadataPath)) File.Delete(metadataPath);

            string metadataString = JsonConvert.SerializeObject(metadata);
            File.WriteAllText(metadataPath, metadataString);

            StorageUtilities.UpdateSetCompleted(TableClient, options.SetKey);

        }

        private void UploadResultData(SlicingOptions slicingOptions)
        {
            var files = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                StorageUtilities.UploadBlob(
                    BlobClient,
                    file, 
                    Path.Combine(slicingOptions.CloudResultPath, file.Replace(outputPath, string.Empty).TrimStart(new char[] { '\\', '/' })).Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), 
                    slicingOptions.CloudResultContainer);

                File.Delete(file);
            }

            try
            {
                File.Delete(slicingOptions.Obj);
                File.Delete(slicingOptions.Texture);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failure cleaning up source data. " + ex.ToString());
            }
        }

        private async Task VerifySourceDataAsync(SlicingOptions slicingOptions, CancellationToken cancellationToken)
        {
            if (!File.Exists(slicingOptions.Obj))
            {
                await StorageUtilities.DownloadBlobAsync(BlobClient, slicingOptions.Obj, slicingOptions.CloudObjPath, cancellationToken).ConfigureAwait(false);
            }

            if (!File.Exists(slicingOptions.Texture))
            {
                await StorageUtilities.DownloadBlobAsync(BlobClient, slicingOptions.Texture, slicingOptions.CloudTexturePath, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
