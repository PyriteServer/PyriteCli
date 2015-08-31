using System;
using System.IO;
using System.Linq;
using System.Threading;
using clipr;
using clipr.Core;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using PyriteCliCommon;
using PyriteCliCommon.Models;
using PyriteCloudCLI;
using PyriteCloudCLI.Properties;
using PyriteLib;

namespace PyriteCli
{
    class Program
    {
        public static CloudQueue WorkQueue { get; set; }
        public static CloudBlobClient BlobClient { get; set; }
        public static CloudTableClient TableClient { get; set; }

        static void Main(string[] args)
        {
            // Get storage account
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Settings.Default.StorageConnectionString);

            // Create the clients
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            BlobClient = storageAccount.CreateCloudBlobClient();
            TableClient = storageAccount.CreateCloudTableClient();

            // Retrieve a reference to a queue
            WorkQueue = queueClient.GetQueueReference(Settings.Default.Queue);

            // Create the queue if it doesn't already exist
            WorkQueue.CreateIfNotExists();

            CloudOptions opt;

            try
            {
                opt = CliParser.Parse<CloudOptions>(args);

                if (opt.ForceCubical)
                {
                    int longestGridSide = Math.Max(Math.Max(opt.XSize, opt.YSize), opt.ZSize);
                    opt.XSize = opt.YSize = opt.ZSize = longestGridSide;

                    Console.WriteLine("Due to -ForceCubical grid size is now {0},{0},{0}", longestGridSide);
                }

                var options = new SlicingOptions
                {
                    OverrideMtl = opt.MtlOverride,
                    GenerateEbo = opt.Ebo,
                    GenerateOpenCtm = opt.OpenCtm,
                    Debug = opt.Debug,
                    GenerateObj = opt.Obj,
                    Texture = Path.GetFileName(opt.Texture),
                    Obj = Path.GetFileName(opt.Input.First()),
                    WriteMtl = opt.WriteMtl,
                    TextureScale = opt.ScaleTexture,
                    TextureSliceX = opt.TextureXSize,
                    TextureSliceY = opt.TextureYSize,
                    ForceCubicalCubes = opt.ForceCubical,
                    CubeGrid = new Vector3 { X = opt.XSize, Y = opt.YSize, Z = opt.ZSize }
                };

                string objPath;
                if (opt.Input.First().StartsWith("http"))
                {
                    objPath = opt.Input.First();
                }
                else
                {
                    objPath = StorageUtilities.UploadBlob(BlobClient, opt.Input.First(), Guid.NewGuid().ToString(), "processingdata");
                }

                
                string texPath ;
                if (opt.Texture.StartsWith("http"))
                {
                    texPath = opt.Texture;
                }
                else
                {
                    texPath = StorageUtilities.UploadBlob(BlobClient, opt.Texture, Guid.NewGuid().ToString(), "processingdata");
                }

                options.CloudObjPath = objPath;
                options.CloudTexturePath = texPath;
                options.CloudResultContainer = opt.OutputContainer;
                options.CloudResultPath = opt.OutputPath;

                // Get texture set size
                Vector2 setSize;
                if (!string.IsNullOrEmpty(options.Texture) && (options.TextureSliceX + options.TextureSliceY) > 2)
                {
                    setSize = new Vector2(options.TextureSliceX, options.TextureSliceY);
                }
                else
                {
                    setSize = new Vector2(1, 1);
                }

                // Queue work
                var setEntity = new SetEntity("Set", DateTime.UtcNow)
                {
                    ResultPath = options.CloudResultPath,
                    ResultContainer = options.CloudResultContainer,
                    TextureTilesX = setSize.X,
                    TextureTilesY = setSize.Y
                };

                options.SetKey = setEntity.RowKey;

                StorageUtilities.InsertSetMetadata(TableClient, setEntity);

                SpatialUtilities.EnumerateSpace(setSize, (x, y) =>
                {
                    options.TextureTile = new Vector2(x, y);
                    string message = JsonConvert.SerializeObject(options);
                    WorkQueue.AddMessage(new CloudQueueMessage(message));
                });
            }
            catch (ParserExit)
            {
                return;
            }
            catch (ParseException)
            {
                Console.WriteLine("usage: PyriteCli --help");
            }
        }

    }
   
}
