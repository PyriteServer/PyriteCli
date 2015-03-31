using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CuberLib
{
	public class CubeManager
	{
		public Obj ObjInstance { get; set; }
		private XyzPoint size;

		public CubeManager(string inputFile, int xSize, int ySize, int zSize)
		{
			size = new XyzPoint
			{
				X = xSize,
				Y = ySize,
				Z = zSize
			};
			
			// Parse and load the object
			Console.WriteLine("Loading {0}", inputFile);
			ObjInstance = new Obj();
			ObjInstance.LoadObj(inputFile, ShowLinesLoaded);

			// Write out a bit of info about the object
			Console.WriteLine("Loaded {0} vertices and {1} faces", ObjInstance.VertexList.Count(), ObjInstance.FaceList.Count());
			Console.WriteLine("Size: X {0} Y {1} Z {2}", ObjInstance.Size.XSize, ObjInstance.Size.YSize, ObjInstance.Size.ZSize);
			Console.WriteLine("Memory Used: " + GC.GetTotalMemory(true) / 1024 / 1024 + "mb");
		}

		public void GenerateCubes(string outputPath, SlicingOptions options)
		{
			CubeMetadata metadata = new CubeMetadata(size) { Extents = ObjInstance.Size };

			// If appropriate, generate textures and save transforms first
			if (!string.IsNullOrEmpty(options.Texture))
			{
				options.UVTransforms = GenerateTextures(outputPath, options);
				ObjInstance.TransformUVs(options);
			}

			// Generate some tiles			
			SpatialUtilities.EnumerateSpace(size, (x, y, z) =>
			{
				Console.WriteLine("Processing cube [{0}, {1}, {2}]", x, y, z);
				string fileOutPath = Path.Combine(outputPath, string.Format("{0}_{1}_{2}", x, y, z));
				int vertexCount = ObjInstance.WriteSpecificCube(fileOutPath, size.X, size.Y, size.Z, x, y, z, options);
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

			Console.WriteLine("Generating textures.");

			Dictionary<Extent, RectangleTransform[]> transforms = new Dictionary<Extent, RectangleTransform[]>();

			SpatialUtilities.EnumerateSpace(options.TextureSliceX, options.TextureSliceY, (x, y) =>
			{
				// Get extent
				double tileHeight = this.ObjInstance.Size.YSize / options.TextureSliceY;
				double tileWidth = this.ObjInstance.Size.XSize / options.TextureSliceX;

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

				// Create texture
				Texture t = new Texture(this.ObjInstance);
				string fileOutPath = Path.Combine(outputPath, string.Format("{0}_{1}.jpg", x, y));
				transforms.Add(extent, t.GenerateTextureTile(options.Texture, fileOutPath, options.TextureSliceY, options.TextureSliceX, x, y));
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
