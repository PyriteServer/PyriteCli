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
                List<int> used = new List<int>();

                for (int i = 0; i < 3; i++)
                {                   
                    Vertex vertex = VertexList[face.VertexIndexList[i] - 1];

                    int x = (int)Math.Floor((vertex.X + xOffset) * xRatio);
                    int y = (int)Math.Floor((vertex.Y + yOffset) * yRatio);
                    int z = (int)Math.Floor((vertex.Z + zOffset) * zRatio);

                    if (x == xLength) x--;
                    if (y == yLength) y--;
                    if (z == zLength) z--;

                    int hash = x * 10000 + y * 100 + z;

                    if (!used.Contains(hash))
                    {
                        matrix[x, y, z].Add(face);
                        used.Add(hash);
                    }
                }
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
                // Type 1 - yields two triangles - 2 of the 3 vertices are in-bounds.
                if (facesToRepair[face].Count == 1)
                {
                    Vertex croppedVertex = facesToRepair[face].First();
                    Vertex[] newVertices = new Vertex[2];

                    // Find the vertices we are keeping
                    var allVerts = face.VertexIndexList.Select(i => VertexList[i - 1]);                    
                    Vertex[] homeVertices = allVerts.Except(new List<Vertex> { croppedVertex }).ToArray();

                    // First triangle, use existing face
                    var intersectionA = SpatialUtilities.CheckLineBox(
                                            cubeExtent.MinCorner,
                                            cubeExtent.MaxCorner,
                                            new Vector3D(croppedVertex),
                                            new Vector3D(homeVertices[0]));

                    var intersectionB = SpatialUtilities.CheckLineBox(
                                            cubeExtent.MinCorner,
                                            cubeExtent.MaxCorner,
                                            new Vector3D(croppedVertex),
                                            new Vector3D(homeVertices[1]));

                    if (intersectionA != null && intersectionB != null)
                    {
                        // Clone the face before we edit it, to use for the new face
                        var newFaceA = face.Clone();
                        var newFaceB = face.Clone();

                        // Update the UVs before the vertices so we can key off the original vertices
                        // New UV for NewVertexA / IntersectionA, which is a new point between homeVertices[0] and croppedVertex
                        var resultA = CalculateNewUV(face, croppedVertex, homeVertices[0], intersectionA);
                        newFaceA.UpdateTextureVertexIndex(resultA.OldIndex, resultA.NewIndex, false);

                        // New UV for NewVertexB / IntersectionB, which is a new point between homeVertices[1] and croppedVertex
                        var resultB = CalculateNewUV(newFaceB, croppedVertex, homeVertices[1], intersectionB);
                        newFaceB.UpdateTextureVertexIndex(resultB.OldIndex, resultB.NewIndex, false);

                        // Now update the vertices
                        // Add a new vertex and update the existing face
                        int length = VertexList.Count();
                        var NewVertexA = new Vertex { Index = length + 1, X = intersectionA.X, Y = intersectionA.Y, Z = intersectionA.Z };
                        VertexList.Add(NewVertexA);
                        newFaceA.UpdateVertexIndex(croppedVertex.Index, length + 1, false);

                        // Replace original face with newFaceA
                        chunkFaceList.Add(newFaceA);
                        chunkFaceList.Remove(face);

                        // Add another new vertex for the net-new face
                        length++;
                        var NewVertexB = new Vertex { Index = length + 1, X = intersectionB.X, Y = intersectionB.Y, Z = intersectionB.Z };
                        VertexList.Add(NewVertexB);

                        // Add the net-new face
                        // TODO: Almost certainly leaving the face and vertex list incorrect for future cubes
                        // Won't really know until I do a run and see what is broken...
                        chunkFaceList.Add(newFaceB);
                        newFaceB.UpdateVertexIndex(homeVertices[0].Index, length, false);
                        newFaceB.UpdateVertexIndex(croppedVertex.Index, length + 1, false);
                        
                    }

                }
                // Type 2 - yields single triangle - 1 of the 3 vertices are in-bounds.
                else if (facesToRepair[face].Count == 2)
                {
                    Vertex[] croppedVertices = facesToRepair[face].ToArray();
                    Vertex[] newVertices = new Vertex[2];
                                        
                    // Find the vertex we are keeping
                    var allVerts = face.VertexIndexList.Select(i => VertexList[i - 1]);
                    Vertex homeVertex = allVerts.Except(croppedVertices).First();

                    // Create new face
                    bool doReplacement = false;
                    var newFace = face.Clone();

                    for (int i = 0; i < 2; i++)
                    {
                        var croppedVertex = new Vector3D(croppedVertices[i]);

                        // Figure out where this line intersects the cube
                        var intersection = SpatialUtilities.CheckLineBox(
                                                cubeExtent.MinCorner,
                                                cubeExtent.MaxCorner,                                                
                                                new Vector3D(croppedVertices[i]),
                                                new Vector3D(homeVertex));

                        if (intersection != null)
                        {
                            doReplacement = true;

                            var result = CalculateNewUV(face, croppedVertices[i], homeVertex, intersection);

                            newFace.UpdateTextureVertexIndex(result.OldIndex, result.NewIndex, false);

                            // Add the new vertex
                            int length = VertexList.Count();
                            VertexList.Add(new Vertex { Index = length + 1, X = intersection.X, Y = intersection.Y, Z = intersection.Z });

                            // Update the new face vertex
                            newFace.UpdateVertexIndex(croppedVertices[i].Index, length + 1, false);
                        }
                    }

                    if (doReplacement)
                    {
                        chunkFaceList.Add(newFace);
                        chunkFaceList.Remove(face);
                    }
                }

            }
        }

        class CalculateNewUVResult
        {
            public int OldIndex { get; set; }
            public int NewIndex { get; set; }
        }

        /// <summary>
        /// Calculates and inserts a new UV between two existing ones
        /// </summary>
        /// <returns>The index of the new UV</returns>
        private CalculateNewUVResult CalculateNewUV(Face face, Vertex croppedVertex, Vertex homeVertex, Vector3D intersection)
        {
            // Figure out the UV transform
            // First, figure out the distance of the old and new line segments in 3d space
            double originalDistance = Math.Sqrt(Math.Pow(croppedVertex.X - homeVertex.X, 2) + Math.Pow(croppedVertex.Y - homeVertex.Y, 2) + Math.Pow(croppedVertex.Z - homeVertex.Z, 2));
            double newDistance = Math.Sqrt(Math.Pow(intersection.X - homeVertex.X, 2) + Math.Pow(intersection.Y - homeVertex.Y, 2) + Math.Pow(intersection.Z - homeVertex.Z, 2));
            double multiplier = newDistance / originalDistance;

            // And the distances in 2d (UV) space
            var croppedUV = TextureList[face.TextureVertexIndexList[Array.IndexOf(face.VertexIndexList, croppedVertex.Index)] - 1];
            var homeUV = TextureList[face.TextureVertexIndexList[Array.IndexOf(face.VertexIndexList, homeVertex.Index)] - 1];
            var originalUVDistance = Math.Sqrt(Math.Pow(croppedUV.X - homeUV.X, 2) + Math.Pow(croppedUV.Y - homeUV.Y, 2));
            var newUVDistance = originalUVDistance * multiplier;

            // New UV coordinate using parameterized equation of the 3d line
            double u = homeUV.X + (croppedUV.X - homeUV.X) * multiplier;
            double v = homeUV.Y + (croppedUV.Y - homeUV.Y) * multiplier;

            // Add the new UV
            int length = TextureList.Count();
            TextureList.Add(new TextureVertex { Index = length + 1, X = u, Y = v, OriginalX = u, OriginalY = v, Transformed = true });

            return new CalculateNewUVResult { OldIndex = croppedUV.Index, NewIndex = length + 1 };
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

                Parallel.ForEach(requiredVertices, i =>
                {
                    Vertex moving = VertexList[i - 1];
                    int newIndex = WriteVertexWithNewIndex(moving, ref newVertexIndex, writer);

                    var facesRequiringUpdate = chunkFaceList.Where(f => f.VertexIndexList.Contains(i));
                    foreach (var face in facesRequiringUpdate) face.UpdateVertexIndex(moving.Index, newIndex);
                });


                // Write each texture vertex and update faces
                int newTextureVertexIndex = 0;

                Parallel.ForEach(requiredTextureVertices, i =>
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

								// The total number of vertices prior to matching face
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

            double sideLength = Math.Ceiling(Math.Max(Math.Max(Size.XSize, Size.YSize), Size.ZSize));

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
