using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PyriteLib
{
	public class CubeManager
	{
        private readonly string TextureSubDirectory = "texture";

		public Obj ObjInstance { get; set; }
		private Vector3 size;

		public CubeManager(SlicingOptions options)
		{
            size = options.CubeGrid;
			
			// Parse and load the object
			Trace.TraceInformation("Loading {0}", options.Obj);
			ObjInstance = new Obj();
			ObjInstance.LoadObj(options.Obj, ShowLinesLoaded, size, options);

			// Write out a bit of info about the object
			Trace.TraceInformation("Loaded {0} vertices and {1} faces", ObjInstance.VertexList.Count(), ObjInstance.FaceList.Count());
			Trace.TraceInformation("Size: X {0} Y {1} Z {2}", ObjInstance.Size.XSize, ObjInstance.Size.YSize, ObjInstance.Size.ZSize);
			Trace.TraceInformation("Memory Used: " + GC.GetTotalMemory(true) / 1024 / 1024 + "mb");
		}

		public void GenerateCubes(string outputPath, SlicingOptions options)
		{
			CubeMetadata metadata = new CubeMetadata(size) {
				WorldBounds = ObjInstance.Size,
				VirtualWorldBounds = options.ForceCubicalCubes ? ObjInstance.CubicalSize : ObjInstance.Size,
				VertexCount = ObjInstance.VertexList.Count };

			// If appropriate, generate textures and save transforms first
			if (!string.IsNullOrEmpty(options.Texture) && (options.TextureSliceX + options.TextureSliceY) > 2)
			{
				options.UVTransforms = GenerateTextures(Path.Combine(outputPath, TextureSubDirectory), options);
                metadata.TextureSetSize = new Vector2(options.TextureSliceX, options.TextureSliceY);
			}
            else
            {
                metadata.TextureSetSize = new Vector2(1, 1);
            }

			// Generate some tiles			
			SpatialUtilities.EnumerateSpace(size, (x, y, z) =>
			{
				Trace.TraceInformation("Processing cube [{0}, {1}, {2}]", x, y, z);
				string fileOutPath = Path.Combine(outputPath, string.Format("{0}_{1}_{2}", x, y, z));
				int vertexCount = ObjInstance.WriteSpecificCube(fileOutPath, x, y, z, options);
				metadata.CubeExists[x, y, z] = vertexCount > 0;
			});			

			// Write out some json metadata
			string metadataPath = Path.Combine(outputPath, "metadata.json");
			if (File.Exists(metadataPath)) File.Delete(metadataPath);

			string metadataString = JsonConvert.SerializeObject(metadata);
			File.WriteAllText(metadataPath, metadataString);
        }

		public Dictionary<Vector2, RectangleTransform[]> GenerateTextures(string outputPath, SlicingOptions options)
		{
			if (string.IsNullOrEmpty(options.Texture)) throw new ArgumentNullException("Texture file not specified.");

			Trace.TraceInformation("Generating textures.");

			Dictionary<Vector2, RectangleTransform[]> transforms = new Dictionary<Vector2, RectangleTransform[]>();

			// Create texture
			Texture t = new Texture(this.ObjInstance, options.Texture);

			// Hold a ref to the texture instance for debugging
			options.TextureInstance = t;

			SpatialUtilities.EnumerateSpaceParallel(options.TextureSliceX, options.TextureSliceY, (x, y) =>
			{	
				string fileOutPath = Path.Combine(outputPath, string.Format("{0}_{1}.jpg", x, y));

				var transform = t.GenerateTextureTile(fileOutPath, x, y, options);
				transforms.Add(new Vector2(x, y), transform);
                ObjInstance.TransformUVsForTextureTile(options, new Vector2(x, y), transform);
            });

			return transforms;
		}

		// Action to show incremental file loading status
		public static void ShowLinesLoaded(int lines)
		{			
			//Console.SetCursorPosition(0, Console.CursorTop);
			//Console.Write("Loaded {0} lines             ", lines);			
		}
	}
}
