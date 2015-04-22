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
		private XyzPoint size;

		public CubeManager(string inputFile, int xSize, int ySize, int zSize, SlicingOptions options)
		{
			size = new XyzPoint
			{
				X = xSize,
				Y = ySize,
				Z = zSize
			};
			
			// Parse and load the object
			Trace.TraceInformation("Loading {0}", inputFile);
			ObjInstance = new Obj();
			ObjInstance.LoadObj(inputFile, ShowLinesLoaded, new XyzPoint { X = xSize, Y = ySize, Z = zSize }, options);

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
			if (!string.IsNullOrEmpty(options.Texture))
			{
				options.UVTransforms = GenerateTextures(Path.Combine(outputPath, TextureSubDirectory), options);
				ObjInstance.TransformUVs(options);
                metadata.TextureSetSize = new XyPoint { X = options.TextureSliceX, Y = options.TextureSliceY };
			}
            else
            {
                metadata.TextureSetSize = new XyPoint { X = 1, Y = 1 };
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

		public Dictionary<Extent, RectangleTransform[]> GenerateTextures(string outputPath, SlicingOptions options)
		{
			if (string.IsNullOrEmpty(options.Texture)) throw new ArgumentNullException("Texture file not specified.");

			Trace.TraceInformation("Generating textures.");

			Dictionary<Extent, RectangleTransform[]> transforms = new Dictionary<Extent, RectangleTransform[]>();

			// Create texture
			Texture t = new Texture(this.ObjInstance);

			// Hold a ref to the texture instance for debugging
			options.TextureInstance = t;

			SpatialUtilities.EnumerateSpaceParallel(options.TextureSliceX, options.TextureSliceY, (x, y) =>
			{
				// Get extent
				double tileHeight = (options.ForceCubicalCubes ? this.ObjInstance.CubicalSize.YSize : this.ObjInstance.Size.YSize) / options.TextureSliceY;
				double tileWidth = (options.ForceCubicalCubes ? this.ObjInstance.CubicalSize.XSize : this.ObjInstance.Size.XSize) / options.TextureSliceX;

				double yOffset = tileHeight * y;
				double xOffset = tileWidth * x;

				Extent extent = new Extent
				{
					XMin = this.ObjInstance.Size.XMin + xOffset,
					YMin = this.ObjInstance.Size.YMin + yOffset,
					ZMin = this.ObjInstance.Size.ZMin,
					XMax = this.ObjInstance.Size.XMin + xOffset + tileWidth,
					YMax = this.ObjInstance.Size.YMin + yOffset + tileHeight,
					ZMax = this.ObjInstance.Size.ZMax
				};

				
				string fileOutPath = Path.Combine(outputPath, string.Format("{0}_{1}.jpg", x, y));

				var transform = t.GenerateTextureTile(options.Texture, fileOutPath, options.TextureSliceY, options.TextureSliceX, x, y, options.TextureScale, options.ForceCubicalCubes);
				transforms.Add(extent, transform);
            });

			return transforms;
		}

		// Action to show incremental file loading status
		public static void ShowLinesLoaded(int lines)
		{			
			Console.SetCursorPosition(0, Console.CursorTop);
			Console.Write("Loaded {0} lines             ", lines);			
		}
	}
}
