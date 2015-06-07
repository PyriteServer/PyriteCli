using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using clipr;
using clipr.Core;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using PyriteLib;
using System.Configuration;
using Newtonsoft.Json;
using PyriteCloudCLI.Properties;

namespace PyriteCli
{
    class Program
    {
        public static CloudQueue WorkQueue { get; set; }
        public static CloudBlobClient BlobClient { get; set; }

        static void Main(string[] args)
        {
            // Get storage account
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Settings.Default.StorageConnectionString);

            // Create the clients
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            BlobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve a reference to a queue
            WorkQueue = queueClient.GetQueueReference(Settings.Default.Queue);

            // Create the queue if it doesn't already exist
            WorkQueue.CreateIfNotExists();

            Options opt;

            try
            {
                opt = CliParser.Parse<Options>(args);

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
                    Debug = opt.Debug,
                    GenerateObj = true,
                    Texture = Path.GetFileName(opt.Texture),
                    Obj = Path.GetFileName(opt.Input.First()),
                    TextureScale = opt.ScaleTexture,
                    TextureSliceX = opt.TextureXSize,
                    TextureSliceY = opt.TextureYSize,
                    ForceCubicalCubes = opt.ForceCubical,
                    CubeGrid = new Vector3 { X = opt.XSize, Y = opt.YSize, Z = opt.ZSize }
                };

                
                string objPath = UploadBlob(opt.Input.First(), Guid.NewGuid().ToString(), "processingdata");
                string texPath = UploadBlob(opt.Texture, Guid.NewGuid().ToString(), "processingdata");

                options.CloudObjPath = objPath;
                options.CloudTexturePath = texPath;
                options.CloudResultContainer = "nashvillenew";
                options.CloudResultPath = opt.OutputPath;

                string message = JsonConvert.SerializeObject(options);
                WorkQueue.AddMessage(new CloudQueueMessage(message));

            }
            catch (ParserExit)
            {
                return;
            }
            catch (ParseException)
            {
                Console.WriteLine("usage: Cuber --help");
            }
        }

        private static string UploadBlob(string localPath, string remotePath, string containerName)
        {
            var container = BlobClient.GetContainerReference(containerName);
            var blob = container.GetBlockBlobReference(remotePath);
            blob.UploadFromFile(localPath, FileMode.Open);
            return (blob.Uri.ToString());
        }

    }

    [ApplicationInfo(Description = "Cuber Options")]
    public class Options
    {
        [NamedArgument('x', "xsize", Action = ParseAction.Store,
            Description = "The number of times to subdivide in the X dimension.  Default 10.")]
        public int XSize { get; set; }

        [NamedArgument('y', "ysize", Action = ParseAction.Store,
            Description = "The number of times to subdivide in the Y dimension.  Default 10.")]
        public int YSize { get; set; }

        [NamedArgument('z', "zsize", Action = ParseAction.Store,
            Description = "The number of times to subdivide in the Z dimension.  Default 10.")]
        public int ZSize { get; set; }

        [NamedArgument('u', "texturex", Action = ParseAction.Store,
            Description = "The number of times to subdivide texture in the X dimension. Default 4.")]
        public int TextureXSize { get; set; }

        [NamedArgument('v', "texturey", Action = ParseAction.Store,
            Description = "The number of times to subdivide texture in the Y dimension. Default 4.")]
        public int TextureYSize { get; set; }

        [NamedArgument('s', "scaletexture", Action = ParseAction.Store,
            Description = "A number between 0 and 1 telling Cuber how to resize/scale the texture when using -t.  Default 1.")]
        public float ScaleTexture { get; set; }

        [NamedArgument('m', "mtl", Action = ParseAction.Store,
            Description = "Override the MTL field in output obj files. e.g. -z model.mtl")]
        public string MtlOverride { get; set; }

        [NamedArgument('t', "texture", Action = ParseAction.Store,
            Description = "Include a texture to partition during cube slicing. Will rewrite UV's in output files. Requires -tx -ty parameters.")]
        public string Texture { get; set; }

        [NamedArgument('e', "ebo", Action = ParseAction.StoreTrue,
            Description = "Generate EBO files designed for use with CubeServer in addition to OBJ files")]
        public bool Ebo { get; set; }

        [NamedArgument('a', "markupUV", Action = ParseAction.StoreTrue,
            Description = "Draws UVW's on a texture")]
        public bool MarkupUV { get; set; }

        [NamedArgument('c', "forcecubical", Action = ParseAction.StoreTrue,
            Description = "X Y Z grid dimensions will be equal, and world space will be grown to fill a containing cube.")]
        public bool ForceCubical { get; set; }

        [NamedArgument('d', "debug", Action = ParseAction.StoreTrue,
            Description = "Generate various additional debug data during error states")]
        public bool Debug { get; set; }

        [PositionalArgument(0, MetaVar = "OUT",
            Description = "Output folder")]
        public string OutputPath { get; set; }

        [PositionalArgument(1, MetaVar = "IN",
            NumArgs = 1,
            Constraint = NumArgsConstraint.AtLeast,
            Description = "A list of .obj files to process")]
        public List<string> Input { get; set; }

        public Options()
        {
            XSize = 2;
            YSize = 2;
            ZSize = 2;
            ScaleTexture = 1;
            TextureXSize = 1;
            TextureYSize = 1;
            ForceCubical = true;
            Debug = false;
        }
    }

}
