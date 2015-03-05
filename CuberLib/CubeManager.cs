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

		public void GenerateTiles(string outputPath)
		{
			CubeMetadata metadata = new CubeMetadata(size) { Extents = ObjInstance.Size };

			// Generate some tiles
			for (int x = 0; x < size.X; x++)
			{
				for (int y = 0; y < size.Y; y++)
				{
					for (int z = 0; z < size.Z; z++)
					{
						string fileOutPath = Path.Combine(outputPath, string.Format("{0}_{1}_{2}.obj", x, y, z));
						int vertexCount = ObjInstance.WriteObjGridTile(fileOutPath, size.X, size.Y, size.Z, x, y, z);
						metadata.CubeExists[x, y, z] = vertexCount > 0;
                    }
				}
			}

			// Write out some json metadata
			string metadataPath = Path.Combine(outputPath, "metadata.json");
			if (File.Exists(metadataPath)) File.Delete(metadataPath);

			string metadataString = JsonConvert.SerializeObject(metadata);
			File.WriteAllText(metadataPath, metadataString);
        }

		// Action to show incremental file loading status
		public static void ShowLinesLoaded(int lines)
		{
			Console.SetCursorPosition(0, 10);
			Console.Write("Loaded {0} lines             ", lines);
			Console.SetCursorPosition(0, 0);
		}
	}
}
