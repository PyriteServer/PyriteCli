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
        public List<TextureVertex> TextureList;

        public Extent Size { get; set; }

        private string mtl;

		/// <summary>
		/// Parse and load an OBJ file into memory.  Will consume memory
		/// at aproximately 120% the size of the file.
		/// </summary>
		/// <param name="path">path to obj file on disk</param>
		/// <param name="linesProcessedCallback">callback for status updates</param>
        public void LoadObj(string path, Action<int> linesProcessedCallback)
        {
            VertexList = new List<Vertex>();
            FaceList = new List<Face>();
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
        }

		public void TransformUVs(SlicingOptions options)
		{
			Trace.TraceInformation("Transforming {0} UV points across {1} extents", TextureList.Count, options.UVTransforms.Keys.Count);

			foreach (var extent in options.UVTransforms.Keys)
			{
				var uvIndices = FaceList.AsParallel().Where(v => v.InExtent(extent, VertexList)).SelectMany(f => f.TextureVertexIndexList).Distinct();
				foreach (var uv in uvIndices.Select(i => TextureList[i-1]).Where(t => !t.Transformed))
				{
					var transforms = options.UVTransforms[extent].Where(t => t.ContainsPoint(uv.X, uv.Y));

					if (transforms.Any())
					{
						RectangleTransform transform = transforms.First();
						uv.Transform(transform);
					}
					else
					{                                                    
						Trace.TraceWarning("No transform found for UV ({0}, {1}) across {2} transforms", uv.X, uv.Y, options.UVTransforms[extent].Count());
					}
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
        public int WriteSpecificCube(string path, int gridHeight, int gridWidth, int gridDepth, int cubeX, int cubeY, int cubeZ, SlicingOptions options)
        {
            double cubeHeight = Size.YSize / gridHeight;
            double cubeWidth = Size.XSize / gridWidth;
			double cubeDepth = Size.ZSize / gridDepth;

			double yOffset = cubeHeight * cubeY;
            double xOffset = cubeWidth * cubeX;
			double zOffset = cubeDepth * cubeZ;

			Extent newSize = new Extent
            {
                XMin = Size.XMin + xOffset,
                YMin = Size.YMin + yOffset,
				ZMin = Size.ZMin + zOffset,				
                XMax = Size.XMin + xOffset + cubeWidth,
                YMax = Size.YMin + yOffset + cubeHeight,
				ZMax = Size.ZMin + zOffset + cubeDepth
			};

			return WriteObj(path, newSize, options);
        }

		/// <summary>
		/// Writes an OBJ uses vertices and faces contained within the provided boundries.
		/// Typically used by WriteObjGridTile(...)
		/// Returns number of vertices written, or 0 if nothing was written.
		/// </summary>
		public int WriteObj(string path, Extent boundries, SlicingOptions options)
        {
			string objPath = path + ".obj";
			string eboPath = path + ".ebo";
			bool objRequiresDelete = false, eboRequiresDelete = false;

			// Delete files or handle resume if the required ones already exist
			if (!Directory.Exists(Path.GetDirectoryName(objPath))) { Directory.CreateDirectory(Path.GetDirectoryName(objPath)); }						

			if (options.GenerateObj)
			{
				objRequiresDelete = File.Exists(objPath);
			}

			if (options.GenerateEbo)
			{
				eboRequiresDelete = File.Exists(eboPath);
			}

			if (options.AttemptResume && objRequiresDelete == options.GenerateObj && eboRequiresDelete == options.GenerateEbo)
			{
				return 1;
			}
			else
			{
				if (objRequiresDelete) File.Delete(objPath);
				if (eboRequiresDelete) File.Delete(eboPath);
			}

            // Revert all vertices in case we previously changed their indexes
            FaceList.AsParallel().ForAll(f => f.RevertVertices());

			// Get all faces in this cube
			List<Face> chunkFaceList;
			chunkFaceList = FaceList.AsParallel().Where(v => v.InExtent(boundries, VertexList)).ToList();

			if (!chunkFaceList.Any())
				return 0;

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
                else if (!string.IsNullOrEmpty(mtl))
                {
                    writer.WriteLine("mtllib " + mtl);
                }

                // Write each vertex and update faces				
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
                        mtl = parts[1];
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
