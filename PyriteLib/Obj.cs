using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PyriteLib.Types;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PyriteLib
{
    public class Obj
    {
		const int NUMCORES = 7;

        public List<Vertex> VertexList;
        public List<Face> FaceList;
		public List<Face>[,,] FaceMatrix;
        public List<TextureVertex> TextureList;

        public Extent Size { get; set; }
		public Extent CubicalSize { get; set; }

		private string _mtl;
		private bool _verticesRequireReset;

		/// <summary>
		/// Parse and load an OBJ file into memory.  Will consume memory
		/// at aproximately 120% the size of the file.
		/// </summary>
		/// <param name="path">path to obj file on disk</param>
		/// <param name="linesProcessedCallback">callback for status updates</param>
        public void LoadObj(string path, Action<int> linesProcessedCallback, Vector3 gridSize, SlicingOptions options)
        {
            VertexList = new List<Vertex>();
            FaceList = new List<Face>();
			FaceMatrix = new List<Face>[gridSize.X, gridSize.Y, gridSize.Z];
            TextureList = new List<TextureVertex>();

            var input = File.ReadLines(path);

            int linesProcessed = 0;
                                
            foreach (string line in input)
            {
                processLine(line);

                // Handle a callback for a status update
                linesProcessed++;
                if (linesProcessedCallback != null && linesProcessed % 1000 == 0)
                    linesProcessedCallback(linesProcessed);
            }

            if (linesProcessedCallback != null)
                linesProcessedCallback(linesProcessed);

            updateSize();

			populateMatrix(FaceList, FaceMatrix, options.ForceCubicalCubes ? CubicalSize : Size);

			_verticesRequireReset = false;                    
        }

		private void populateMatrix(List<Face> faces, List<Face>[,,] matrix, Extent size)
		{
			Trace.TraceInformation("Partitioning Faces.");

			int xLength = matrix.GetLength(0);
			int yLength = matrix.GetLength(1);
			int zLength = matrix.GetLength(2);

			// World to cube ratios
			double xOffset = 0 - size.XMin;
			double xRatio = xLength / (size.XSize);

			double yOffset = 0 - size.YMin;
			double yRatio = yLength / (size.YSize);

			double zOffset = 0 - size.ZMin;
			double zRatio = zLength / (size.ZSize);

			// Initialize matrix
			SpatialUtilities.EnumerateSpace(xLength, yLength, zLength, 
				(x, y, z) => matrix[x, y, z] = new List<Face>());

            foreach (var face in faces)
            {
                Vertex vertex = VertexList[face.VertexIndexList[0] - 1];

                int x = (int)Math.Floor((vertex.X + xOffset) * xRatio);
                int y = (int)Math.Floor((vertex.Y + yOffset) * yRatio);
                int z = (int)Math.Floor((vertex.Z + zOffset) * zRatio);

                if (x == xLength) x--;
                if (y == yLength) y--;
                if (z == zLength) z--;

                matrix[x, y, z].Add(face);               
            }
		}


		/// <summary>
		/// Updates all UV coordinates (Texture Vertex) based on
		/// the rectangular transforms which may be included in
		/// slicing options if texture partitioning has occurred.
		/// </summary>
		public void TransformUVs(SlicingOptions options)
		{
			Trace.TraceInformation("Transforming {0} UV points across {1} extents", TextureList.Count, options.UVTransforms.Keys.Count);

			foreach (var extent in options.UVTransforms.Keys)
			{
				var faces = FaceList.AsParallel().Where(v => v.InExtent(extent, VertexList)).ToList();
                var uvIndices = faces.SelectMany(f => f.TextureVertexIndexList).Distinct();
				var uvs = uvIndices.Select(i => TextureList[i - 1]).ToList();
                foreach (var uv in uvs)
				{
					var transforms = options.UVTransforms[extent].Where(t => t.ContainsPoint(uv.OriginalX, uv.OriginalY));

					if (transforms.Any())
					{
						RectangleTransform transform = transforms.First();

						if (uv.Transformed)
						{
							// This was already transformed in another extent, so we'll have to copy it
							int newIndex = uv.CloneOriginal(TextureList);
							TextureList[newIndex - 1].Transform(transform);

							// Update all faces using the old UV in this extent
							var FacesToUpdate = faces.Where(f => f.TextureVertexIndexList.Contains(uv.Index));
							foreach (var face in FacesToUpdate)
							{
								face.UpdateTextureVertexIndex(uv.Index, newIndex, false);
							}

							Trace.TraceInformation("Added new VT: " + newIndex);
						}
						else
						{
							uv.Transform(transform);
						}
					}
					else
					{
						Trace.TraceWarning("No transform found for UV ({0}, {1}) across {2} transforms", uv.X, uv.Y, options.UVTransforms[extent].Count());
					}
				}

				// Write out a marked up image file showing where lost UV's occured
				if (options.Debug)
				{
					var notTransformedUVs = uvs.Where(u => !u.Transformed).ToArray();
					var relevantTransforms = options.UVTransforms[extent];
					if (relevantTransforms.Any() && notTransformedUVs.Any())
						options.TextureInstance.MarkupTextureTransforms(options.Texture, relevantTransforms, notTransformedUVs);
				}
            }
		}

		/// <summary>
		/// Write a single "cube".
		/// Pending addition of Z-axis so that they are actually cubes.
		/// </summary>
		/// <param name="path">Output path</param>
		/// <param name="gridHeight">Y size of grid</param>
		/// <param name="gridWidth">X size of grid</param>
		/// <param name="cubeX">Zero based X index of cube</param>
		/// <param name="cubeY">Zero based Y index of cube</param>
        public int WriteSpecificCube(string path, int cubeX, int cubeY, int cubeZ, SlicingOptions options)
        {
			string objPath = path + ".obj";
			string eboPath = path + ".ebo";

			// Delete files or handle resume if the required ones already exist
			CleanOldFiles(options, objPath, eboPath);


			// Revert all vertices in case we previously changed their indexes
			if (_verticesRequireReset)
			{
				FaceList.AsParallel().ForAll(f => f.RevertVertices());
				_verticesRequireReset = false;
			}

			// Get all faces in this cube
			List<Face> chunkFaceList;
			chunkFaceList = FaceMatrix[cubeX, cubeY, cubeZ];

			if (!chunkFaceList.Any())
				return 0;

            CropCube(chunkFaceList, cubeX, cubeY, cubeZ, FaceMatrix, options.ForceCubicalCubes ? CubicalSize : Size); ;

			Trace.TraceInformation("{0} faces", chunkFaceList.Count);

			if (options.GenerateEbo)
			{
				WriteEboFormattedFile(eboPath, options.OverrideMtl, chunkFaceList);
			}

			if (options.GenerateObj)
			{
				WriteObjFormattedFile(objPath, options.OverrideMtl, chunkFaceList);
			}

			return chunkFaceList.Count;
		}

        private void CropCube(List<Face> chunkFaceList, int cubeX, int cubeY, int cubeZ, List<Face>[,,] matrix, Extent size)
        {
            double cubeHeight;
            double cubeWidth;
            double cubeDepth;

            cubeHeight = size.YSize / matrix.GetLength(1);
            cubeWidth = size.XSize / matrix.GetLength(0);
            cubeDepth = size.ZSize / matrix.GetLength(2);       

            double yOffset = cubeHeight * cubeY;
            double xOffset = cubeWidth * cubeX;
            double zOffset = cubeDepth * cubeZ;

            Extent cubeExtent = new Extent
            {
                XMin = size.XMin + xOffset,
                YMin = size.YMin + yOffset,
                ZMin = size.ZMin + zOffset,
                XMax = size.XMin + xOffset + cubeWidth,
                YMax = size.YMin + yOffset + cubeHeight,
                ZMax = size.ZMin + zOffset + cubeDepth
            };

            Dictionary<Face, List<Vertex>> facesToRepair = new Dictionary<Face, List<Vertex>>();

            // Enumerate vertices for ones crossing bounds
            foreach(var face in chunkFaceList)
            {
                var vertices = FindOutOfBoundVertices(face, cubeExtent);

                if (vertices.Any())
                {
                    facesToRepair.Add(face, vertices);
                }            
            }

            foreach(var face in facesToRepair.Keys)
            {
                // Type 1 - yields two triangles
                if (facesToRepair[face].Count == 1)
                {

                }
                // Type 2 - yields single triangle
                else
                {
                    Vertex[] croppedVertices = facesToRepair[face].ToArray();
                    Vertex[] newVertices = new Vertex[2];
                                        
                    // Find the vertex we are keeping
                    var allVerts = face.VertexIndexList.Select(i => VertexList[i - 1]);
                    Vertex homeVertex = allVerts.Except(croppedVertices).First();

                    for (int i = 0; i < 2; i++)
                    {
                        var intersection = SpatialUtilities.CheckLineBox(
                                                cubeExtent.MinCorner,
                                                cubeExtent.MaxCorner,                                                
                                                new Vector3D(croppedVertices[i]),
                                                new Vector3D(homeVertex));

                        int length = VertexList.Count();
                        VertexList.Add(new Vertex { Index = length + 1, X = intersection.X, Y = intersection.Y, Z = intersection.Z });
                        face.UpdateVertexIndex(croppedVertices[i].Index, length + 1, false);
                    }
                }

            }
        }

        private List<Vertex> FindOutOfBoundVertices(Face face, Extent extent)
        {
            List<Vertex> result = new List<Vertex>();

            for (int i = 0; i < 3; i++)
            {
                Vertex vertex = VertexList[face.VertexIndexList[i] - 1];

                if (!vertex.InExtent(extent))
                {
                    result.Add(vertex);
                }
            }

            return result;
        }

        private static void CleanOldFiles(SlicingOptions options, string objPath, string eboPath)
		{
			if (!Directory.Exists(Path.GetDirectoryName(objPath))) { Directory.CreateDirectory(Path.GetDirectoryName(objPath)); }

			if (options.GenerateObj)
			{
				File.Delete(objPath);
			}

			if (options.GenerateEbo)
			{
				File.Delete(eboPath);
			}
		}

		private void WriteObjFormattedFile(string path, string mtlOverride, List<Face> chunkFaceList)
        {
			// Build a list of vertices indexes needed for these faces
			List<int> requiredVertices = null;
			List<int> requiredTextureVertices = null;

			var tv = Task.Run(() => { requiredVertices = chunkFaceList.AsParallel().SelectMany(f => f.VertexIndexList).Distinct().ToList(); });
			var ttv = Task.Run(() => { requiredTextureVertices = chunkFaceList.AsParallel().SelectMany(f => f.TextureVertexIndexList).Distinct().ToList(); });

			Task.WaitAll(new Task[] { tv, ttv });			

			using (var outStream = File.OpenWrite(path))
            using (var writer = new StreamWriter(outStream))
            {

                // Write some header data
                writer.WriteLine("# Generated by Cuber");

                if (!string.IsNullOrEmpty(mtlOverride))
                {
                    writer.WriteLine("mtllib " + mtlOverride);
                }
                else if (!string.IsNullOrEmpty(_mtl))
                {
                    writer.WriteLine("mtllib " + _mtl);
                }

				// Write each vertex and update faces		
				_verticesRequireReset = true;		
                int newVertexIndex = 0;

                Parallel.ForEach(requiredVertices, new ParallelOptions { MaxDegreeOfParallelism = NUMCORES }, i =>
                {
                    Vertex moving = VertexList[i - 1];
                    int newIndex = WriteVertexWithNewIndex(moving, ref newVertexIndex, writer);

                    var facesRequiringUpdate = chunkFaceList.Where(f => f.VertexIndexList.Contains(i));
                    foreach (var face in facesRequiringUpdate) face.UpdateVertexIndex(moving.Index, newIndex);
                });


                // Write each texture vertex and update faces
                int newTextureVertexIndex = 0;

                Parallel.ForEach(requiredTextureVertices, new ParallelOptions { MaxDegreeOfParallelism = NUMCORES }, i =>
                {
                    TextureVertex moving = TextureList[i - 1];
                    int newIndex = WriteVertexWithNewIndex(moving, ref newTextureVertexIndex, writer);

                    var facesRequiringUpdate = chunkFaceList.Where(f => f.TextureVertexIndexList.Contains(i));
                    foreach (var face in facesRequiringUpdate) face.UpdateTextureVertexIndex(moving.Index, newIndex);
                });

                // Write the faces
                chunkFaceList.ForEach(f => writer.WriteLine(f));
            }
        }

        private void WriteEboFormattedFile(string path, string mtlOverride, List<Face> chunkFaceList)
        {
            using (var outStream = File.OpenWrite(path))
            using (var writer = new BinaryWriter(outStream))
            {
				writer.Write((ushort)chunkFaceList.Count); 

				for (int fi = 0; fi < chunkFaceList.Count; fi++)
				{
					// Hardcode for triangles in this format, since that is what the client supports
					for (int i = 0; i < 3; i++)
					{
						// Have we written this vertex before? If so write a pointer to its index
						int desiredVertexIndex = chunkFaceList[fi].VertexIndexList[i];
						int desiredTextureIndex = chunkFaceList[fi].TextureVertexIndexList[i];
						var preexisting = chunkFaceList.Take(fi).AsParallel().Where(f => f.VertexIndexList.Contains(desiredVertexIndex));

						if (preexisting.Any())
						{
							var doubleMatch = preexisting.Where(f => f.TextureVertexIndexList.Contains(desiredTextureIndex));

							if (doubleMatch.Any())
							{
								var face = doubleMatch.First();

								// The total number of vertices prior to this face
								int index = (chunkFaceList.IndexOf(face)) * 3;

								// Now add the delta to index into this triangle correctly
								int indexInFace = face.VertexIndexList.ToList().IndexOf(desiredVertexIndex);
								index += indexInFace;

								// write the back reference instead of the vertex
								writer.Write((byte)0);
								writer.Write((UInt32)index);
								
							}
							else
							{
								var face = preexisting.First();

								// The total number of vertices prior to this face
								int index = (chunkFaceList.IndexOf(face)) * 3;

								// Now add the delta to index into this triangle correctly
								int indexInFace = face.VertexIndexList.ToList().IndexOf(desiredVertexIndex);
								index += indexInFace;

								// write the back reference instead of the vertex
								writer.Write((byte)64);
								writer.Write((UInt32)index);

								writer.Write((float)TextureList[chunkFaceList[fi].TextureVertexIndexList[i] - 1].X);
								writer.Write((float)TextureList[chunkFaceList[fi].TextureVertexIndexList[i] - 1].Y);							

							}
						}
						else
						{
							writer.Write((byte)255);
							writer.Write((float)VertexList[desiredVertexIndex - 1].X);
							writer.Write((float)VertexList[desiredVertexIndex - 1].Y);
							writer.Write((float)VertexList[desiredVertexIndex - 1].Z);

							writer.Write((float)TextureList[chunkFaceList[fi].TextureVertexIndexList[i] - 1].X);
							writer.Write((float)TextureList[chunkFaceList[fi].TextureVertexIndexList[i] - 1].Y);
						}
					}
				}
				writer.Write((byte)128);
			}
        }

		/// <summary>
		/// Helper to make determining the index of the written vertex
		/// and the stream output thread safe.  
		/// We block on writing the line, and incrementing the index.
		/// Has no real performance impact as most of the time is spent traversing arrays.
		/// </summary>
		[MethodImpl(MethodImplOptions.Synchronized)]
		private int WriteVertexWithNewIndex<T>(T item, ref int index, StreamWriter writer)
        {
			writer.WriteLine(item);
			index++;
			return index;
        }

		/// <summary>
		/// Sets our global object size with an extent object
		/// </summary>
        private void updateSize()
        {
            Size = new Extent
            {
                XMax = VertexList.Max(v => v.X),
                XMin = VertexList.Min(v => v.X),
                YMax = VertexList.Max(v => v.Y),
                YMin = VertexList.Min(v => v.Y),
                ZMax = VertexList.Max(v => v.Z),
                ZMin = VertexList.Min(v => v.Z)
            };

			double sideLength = Math.Max(Math.Max(Size.XSize, Size.YSize), Size.ZSize);

			CubicalSize = new Extent
			{
				XMin = Size.XMin,
				YMin = Size.YMin,
				ZMin = Size.ZMin,
				XMax = Size.XMin + sideLength,
				YMax = Size.YMin + sideLength,
				ZMax = Size.ZMin + sideLength
			};
        }

		/// <summary>
		/// Parses and loads a line from an OBJ file.
		/// Currently only supports V, VT, F and MTLLIB prefixes
		/// </summary>		
        private void processLine(string line)
        {
            string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length > 0)
            {
                switch (parts[0])
                {
                    case "mtllib":
                        _mtl = parts[1];
                        break;
                    case "v":
                        Vertex v = new Vertex();
                        v.LoadFromStringArray(parts);
                        VertexList.Add(v);
                        v.Index = VertexList.Count();
                        break;
                    case "f":
                        Face f = new Face();
                        f.LoadFromStringArray(parts);
                        FaceList.Add(f);
                        break;
                    case "vt":
                        TextureVertex vt = new TextureVertex();
                        vt.LoadFromStringArray(parts);
                        TextureList.Add(vt);
                        vt.Index = TextureList.Count();
                        break;

                }
            }
        }

    }
}
