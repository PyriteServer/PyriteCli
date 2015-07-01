using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using PyriteCliCommon;
using PyriteLib;

namespace PyriteCloudRole
{
    public class Scanner
    {
        public CloudQueue WorkQueue { get; set; }
        public CloudBlobClient BlobClient { get; set; }

        private string outputPath, inputPath;

        public Scanner()
        {
            outputPath = Path.Combine(RoleEnvironment.GetLocalResource("output").RootPath, Guid.NewGuid().ToString());
            inputPath = Path.Combine(RoleEnvironment.GetLocalResource("input").RootPath, Guid.NewGuid().ToString());

            Directory.CreateDirectory(outputPath);
            Directory.CreateDirectory(inputPath);

            // Get storage account
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                CloudConfigurationManager.GetSetting("StorageConnectionString"));

            // Create the clients
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            BlobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve a reference to a queue
            WorkQueue = queueClient.GetQueueReference(
                CloudConfigurationManager.GetSetting("Queue"));

            // Create the queue if it doesn't already exist
            WorkQueue.CreateIfNotExists();
        }

        public void DoWork()
        {
            CloudQueueMessage retrievedMessage;

            try
            {
                // Get the next message
                retrievedMessage = WorkQueue.GetMessage();

                if (retrievedMessage == null) return;
            }
            catch
            {
                return;
            }

            // Just blindly delete the message for now, we have no logic to handle failures anyway
            WorkQueue.DeleteMessage(retrievedMessage);

            var messageContents = retrievedMessage.AsString;

            try
            {
                SlicingOptions slicingOptions = JsonConvert.DeserializeObject<SlicingOptions>(messageContents);

                slicingOptions.Obj = Path.Combine(inputPath, slicingOptions.Obj);
                slicingOptions.Texture = Path.Combine(inputPath, slicingOptions.Texture);

                // ** Prep
                Trace.TraceInformation("Syncing data");
                VerifySourceData(slicingOptions);

                // ** Run
                Trace.TraceInformation("Starting Processing");
                CubeManager manager = new CubeManager(slicingOptions);
                
                if (!string.IsNullOrEmpty(slicingOptions.Texture))
                {
                    slicingOptions.TextureInstance = new Texture(manager.ObjInstance, slicingOptions.Texture);
                }	
                	
                var vertexCounts = manager.GenerateCubesForTextureTile(outputPath, slicingOptions.TextureTile, slicingOptions);

                foreach (var cube in vertexCounts.Keys)
                {
                    // write to table!
                    //metadata.CubeExists[cube.X, cube.Y, cube.Z] = vertexCounts[cube] > 0;
                }           

                // ** Cleanup
                Trace.TraceInformation("Writing Results");
                UploadResultData(slicingOptions);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
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
        }

        private void VerifySourceData(SlicingOptions slicingOptions)
        {
            if (!File.Exists(slicingOptions.Obj))
            {
                StorageUtilities.DownloadBlob(BlobClient, slicingOptions.Obj, slicingOptions.CloudObjPath);
            }

            if (!File.Exists(slicingOptions.Texture))
            {
                StorageUtilities.DownloadBlob(BlobClient, slicingOptions.Texture, slicingOptions.CloudTexturePath);
            }
        }


    }
}
