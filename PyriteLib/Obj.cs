using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PyriteLib.Types;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

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
                processLine(line, options);

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

            // Crop all the cubes
            SpatialUtilities.EnumerateSpace(xLength, yLength, zLength,
                (x, y, z) => CropCube(matrix[x,y,z], x, y, z, matrix, size));
            
        }


        /// <summary>
        /// Updates all UV coordinates (Texture Vertex) based on
        /// the rectangular transforms which may be included in
        /// slicing options if texture partitioning has occurred.
        /// </summary>
        public void TransformUVs(SlicingOptions options)
        {
            Trace.TraceInformation("Transforming {0} UV points across {1} extents", TextureList.Count, options.UVTransforms.Keys.Count);
            foreach (var uvTransform in options.UVTransforms)
            {
                TransformUVsForTextureTile(options ,uvTransform.Key, uvTransform.Value, new CancellationToken());
            }
        }

        public void TransformUVsForTextureTile(SlicingOptions options, Vector2 textureTile, RectangleTransform[] uvTransforms, CancellationToken cancellationToken) 
        {
            Trace.TraceInformation("Transforming UV points for texture tile {0},{1}", textureTile.X, textureTile.Y);

            int newUVCount = 0, failedUVCount = 0, transformUVCount = 0;

            cancellationToken.ThrowIfCancellationRequested();
            var faces = Texture.GetFaceListFromTextureTile(
                options.TextureSliceY, 
                options.TextureSliceX,
                textureTile.X,
                textureTile.Y, 
                this);
            
            var uvIndices = faces.AsParallel().SelectMany(f => f.TextureVertexIndexList).WithCancellation(cancellationToken).Distinct();
            var uvs = uvIndices.Select(i => TextureList[i - 1]).ToList();

            Trace.TraceInformation("Selected UVs");

            foreach (var uv in uvs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var transforms = uvTransforms.Where(t => t.ContainsPoint(uv.OriginalX, uv.OriginalY));

                if (transforms.Any())
                {
                    RectangleTransform transform = transforms.First();
                    lock (uv)
                    {
                        if (uv.Transformed)
                        {
                            // This was already transformed in another extent, so we'll have to copy it
                            int newIndex = uv.CloneOriginal(TextureList);
                            TextureList[newIndex - 1].Transform(transform);

                            // Update all faces using the old UV in this extent
                            faces.AsParallel().Where(f => f.TextureVertexIndexList.Contains(uv.Index)).WithCancellation(cancellationToken).ForAll(face => face.UpdateTextureVertexIndex(uv.Index, newIndex, false));

                            newUVCount++;
                        }
                        else
                        {
                            uv.Transform(transform);     
                            transformUVCount++;

                        }
                    }
                }
                else
                {
                    failedUVCount++;
                }
            }
            
            Trace.TraceInformation("UV Transform results ({3},{4}): {0} success, {1} new, {2} failed.", transformUVCount, newUVCount, failedUVCount, textureTile.X, textureTile.Y);

            // Write out a marked up image file showing where lost UV's occured
            if (options.Debug)
            {
                var notTransformedUVs = uvs.Where(u => !u.Transformed).ToArray();
                var relevantTransforms = uvTransforms;
                    //options.TextureInstance.MarkupTextureTransforms(options.Texture, relevantTransforms, notTransformedUVs, textureTile);
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
        [MethodImpl(MethodImplOptions.Synchronized)]
        public int WriteSpecificCube(string path, Vector3 cube, SlicingOptions options)
        {
            string objPath = path + ".obj";
            string eboPath = path + ".ebo";
            string openCtmPath = path + ".ctm";

            // Delete files or handle resume if the required ones already exist
            CleanOldFiles(options, objPath, eboPath, openCtmPath);


            // Revert all vertices in case we previously changed their indexes
            if (_verticesRequireReset)
            {
                FaceList.AsParallel().ForAll(f => f.RevertVertices());
                _verticesRequireReset = false;
            }

            // Get all faces in this cube
            List<Face> chunkFaceList;
            chunkFaceList = FaceMatrix[cube.X, cube.Y, cube.Z];

            if (!chunkFaceList.Any())
                return 0;            

            Trace.TraceInformation("{0} faces", chunkFaceList.Count);

            if (options.GenerateEbo)
            {
                WriteEboFormattedFile(eboPath, options.OverrideMtl, chunkFaceList);
            }

            var tile = Texture.GetTextureCoordFromCube(options.TextureSliceY, options.TextureSliceX, cube.X, cube.Y, this);

            if (options.GenerateObj)
            {
                string comment = string.Format("Texture Tile {0},{1}", tile.X, tile.Y);
                WriteObjFormattedFile(objPath, options.OverrideMtl, chunkFaceList, tile, options, comment);
                chunkFaceList.AsParallel().ForAll(f => f.RevertVertices());
            }

            if (options.GenerateOpenCtm)
            {
                WriteOpenCtmFormattedFile(openCtmPath, chunkFaceList, tile);
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

            // New UV coordinate using parameterized equation of the 2d line
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

        private static void CleanOldFiles(SlicingOptions options, string objPath, string eboPath, string ctmPath)
        {
            if (!Directory.Exists(Path.GetDirectoryName(objPath))) { Directory.CreateDirectory(Path.GetDirectoryName(objPath)); }

            if (options.GenerateObj)
            {
                File.Delete(objPath);
            }

            if (options.GenerateEbo)
            {
                File.Delete(eboPath);
                File.Delete(GetEbo2Path(eboPath));
            }

            if (options.GenerateOpenCtm)
            {
                File.Delete(ctmPath);
            }
        }

        private void WriteObjFormattedFile(string path, string mtlOverride, List<Face> chunkFaceList, Vector2 tile, SlicingOptions options, string comment = "")
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
                writer.WriteLine("# Generated by PyriteCli");
                if (!string.IsNullOrEmpty(comment))
                {
                    writer.WriteLine("# " + comment);
                }

                if (!string.IsNullOrEmpty(mtlOverride))
                {
                    writer.WriteLine("mtllib " + mtlOverride);
                }
                else if (!string.IsNullOrEmpty(_mtl) && !options.RequiresTextureProcessing())
                {
                    writer.WriteLine("mtllib " + _mtl);
                }
                else if (options.RequiresTextureProcessing() && options.WriteMtl)
                {
                    writer.WriteLine("mtllib texture\\{0}_{1}.mtl", tile.X, tile.Y);
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
            // Write out ebo body into memory stream first
            // Header will be written after once certain counts are known
            using (var outStream = new MemoryStream())
            using (var writer = new BinaryWriter(outStream))
            {
                uint oldVoldUVCount = 0, oldVnewUVCount = 0, newVandUVCount = 0;

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
                            // Have we seen this texture vertex before?
                            var faceWithMatchingVertexAndUV = preexisting.FirstOrDefault(f =>
                            {
                                for (int ti = 0; ti < 3; ti++)
                                {
                                    if (f.VertexIndexList[ti] == desiredVertexIndex && f.TextureVertexIndexList[ti] == desiredTextureIndex)
                                    {
                                        return true;
                                    }
                                }
                                return false;
                            });

                            if (faceWithMatchingVertexAndUV != null)
                            {
                                // The total number of vertices prior to matching face
                                int index = (chunkFaceList.IndexOf(faceWithMatchingVertexAndUV)) * 3;
                                int indexInFace = Enumerable.Range(0, 3).First((innerIndex) =>
                                     chunkFaceList[index/3].VertexIndexList[innerIndex] == desiredVertexIndex &&
                                     chunkFaceList[index/3].TextureVertexIndexList[innerIndex] == desiredTextureIndex
                                );
                                // Now add the delta to index into this triangle correctly
                                index += indexInFace;

                                // write the back reference instead of the vertex
                                writer.Write((byte)0);
                                writer.Write((UInt32)index);

                                oldVoldUVCount++;

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

                                writer.Write((float)TextureList[desiredTextureIndex - 1].X);
                                writer.Write((float)TextureList[desiredTextureIndex - 1].Y);

                                oldVnewUVCount++;

                            }
                        }
                        else
                        {
                            writer.Write((byte)255);
                            writer.Write((float)VertexList[desiredVertexIndex - 1].X);
                            writer.Write((float)VertexList[desiredVertexIndex - 1].Y);
                            writer.Write((float)VertexList[desiredVertexIndex - 1].Z);

                            writer.Write((float)TextureList[desiredTextureIndex - 1].X);
                            writer.Write((float)TextureList[desiredTextureIndex - 1].Y);

                            newVandUVCount++;
                        }
                    }
                }

                writer.Write((byte)128);

                using (var eboFileStream = File.OpenWrite(path))
                using (var eboWriter = new BinaryWriter(eboFileStream))
                using (var ebo2FileStream = File.OpenWrite(GetEbo2Path(path)))
                using (var ebo2Writer = new BinaryWriter(ebo2FileStream))
                {
                    //
                    // Write the ebo header
                    //

                    // Write the number of faces (triangles) 
                    eboWriter.Write((ushort) chunkFaceList.Count);
                    ebo2Writer.Write((ushort)chunkFaceList.Count);

                    // Write the number of unique v,uv pairs
                    ebo2Writer.Write(newVandUVCount + oldVnewUVCount);

                    // Write out the rest of the data
                    outStream.WriteTo(eboFileStream);
                    outStream.WriteTo(ebo2FileStream);
                }
            }
        }

        /// <summary>
        /// Writes out the OpenCTM format for the chunk
        /// 
        /// According to http://openctm.sourceforge.net/media/FormatSpecification.pdf
        /// </summary>
        /// <param name="path">path of file to create</param>
        /// <param name="chunkFaceList">list of faces associated with this chunk</param>
        /// <param name="tile">texture tile information (used to associated with specific texture file)</param>
        private void WriteOpenCtmFormattedFile(string path, List<Face> chunkFaceList, Vector2 tile)
        {
            // Build a list of vertices indexes needed for these faces
            List<Tuple<int,int>> uniqueVertexUVPairs = null;

            var tv = Task.Run(() => { uniqueVertexUVPairs = chunkFaceList.AsParallel().SelectMany(f => f.VertexIndexList.Zip(f.TextureVertexIndexList, (v,uv) => new Tuple<int,int>(v, uv))).Distinct().ToList(); });

            Task.WaitAll(new Task[] { tv });		

            
            using (var outStream = File.OpenWrite(path))
            using (var writer = new BinaryWriter(outStream))
            {
                // Header
                writer.Write(Encoding.UTF8.GetBytes("OCTM"));
                writer.Write(5); // Version
                writer.Write(0x00574152); // Compression (RAW)
                writer.Write(uniqueVertexUVPairs.Count);  // Vertex count
                writer.Write(chunkFaceList.Count); // Triangle count
                writer.Write(1); // UV count
                writer.Write(0); // attribute map count
                writer.Write(0); // flags
                WriteOpenCTMString(writer, "Created by PyriteLib"); // comment

                //Body

                Dictionary<Tuple<int,int>,int> seenVertexUVPairs = new Dictionary<Tuple<int,int>, int>();
                List<int> vertices = new List<int>(uniqueVertexUVPairs.Count);
                List<int> uvs = new List<int>(uniqueVertexUVPairs.Count);
                int nextIndex = 0;
                // Indices
                writer.Write(0x58444e49); // "INDX"                                                                                                                                                           
                Parallel.ForEach(chunkFaceList, f =>
                {
                    lock (writer)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            int vertexIndex = f.VertexIndexList[i];
                            int uvIndex = f.TextureVertexIndexList[i];
                            Tuple<int, int> key = new Tuple<int, int>(vertexIndex, uvIndex);
                            if (!seenVertexUVPairs.ContainsKey(key))
                            {
                                // This is a new vertex uv pair
                                seenVertexUVPairs[key] = nextIndex++;
                                vertices.Add(vertexIndex);
                                uvs.Add(uvIndex);
                            }

                            writer.Write(seenVertexUVPairs[key]);
                        }
                    }
                });

                //Vertices
                writer.Write(0x54524556);
                vertices.ForEach(vertexIndex =>
                {
                    Vertex vertex = VertexList[vertexIndex - 1];
                    writer.Write((float) vertex.X);
                    writer.Write((float) vertex.Y);
                    writer.Write((float) vertex.Z);
                });

                //Normals
                // Not supported -- Skipped

                // UV Maps
                writer.Write(0x43584554);
                WriteOpenCTMString(writer, "Diffuse color");
                WriteOpenCTMString(writer, string.Format("{0}_{1}.jpg", tile.X, tile.Y));
                uvs.ForEach(uvIndex =>
                {
                    TextureVertex vertex = TextureList[uvIndex - 1];
                    writer.Write((float)vertex.X);
                    writer.Write((float)vertex.Y);
                });
            }
        }

        private static void WriteOpenCTMString(BinaryWriter writer, string stringToWrite)
        {
            writer.Write(stringToWrite.Length);
            writer.Write(Encoding.UTF8.GetBytes(stringToWrite));
        }

        private static string GetEbo2Path(string eboPath)
        {
            return eboPath + "2";
        }

        /// <summary>
        /// Helper to make determining the index of the written vertex
        /// and the stream output thread safe.  
        /// We block on writing the line, and incrementing the index.
        /// Has no real performance impact as most of the time is spent traversing arrays.
        /// </summary>
        object vertexWriteLock = new object();
        private int WriteVertexWithNewIndex<T>(T item, ref int index, StreamWriter writer)
        {
            lock (vertexWriteLock)
            {
                writer.WriteLine(item);
                index++;
            }
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
        private void processLine(string line, SlicingOptions options)
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

                        if (options.InvertYZ)
                        {
                            double temp = v.Y;
                            v.Y = v.Z;
                            v.Z = -temp;  
                        }

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
