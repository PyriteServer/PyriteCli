using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using DotImaging;
using DotImaging.Primitives2D;
using PyriteLib.Types;

namespace PyriteLib
{
    public class Texture
	{
        public Obj TargetObj { get; set; }

        private Bgr<byte>[,] source;
        private object sourceLock = new object();
        private bool disposed = false;

        public Texture(Obj obj)
		{
            TargetObj = obj;
		}

        public Texture(Obj obj, string texturePath)
        {
            Trace.TraceInformation("Loading image: " + texturePath);

            TargetObj = obj;
            var imageBytes = File.ReadAllBytes(texturePath);
            source = imageBytes.DecodeAsColorImage();
        }

        // Generates a copy of the provided texture and
        // draws the outline of all UVW's on the image
    /*    public void MarkupTextureFaces(string texturePath)
		{
			string outputPath = texturePath + "_debug.jpg";

			var triangles = GetUVTriangles(TargetObj.FaceList);

			using (Image output = Image.FromFile(texturePath))
			{
				using (Graphics g = Graphics.FromImage(output))
				{
					for (int i = 0; i < triangles.Count; i++)
					{
						var triangle = triangles[i];
						var poly = new PointF[] {
							new PointF((float)(triangle.Item1.X * output.Width), (float)((1-triangle.Item1.Y) * output.Height)),
							new PointF((float)(triangle.Item2.X * output.Width), (float)((1-triangle.Item2.Y) * output.Height)),
							new PointF((float)(triangle.Item3.X * output.Width), (float)((1-triangle.Item3.Y) * output.Height))
							};
						g.DrawPolygon(Pens.Red, poly); 
					}
				}

				// Write to disk
				if (File.Exists(outputPath)) File.Delete(outputPath);
				if (!Directory.Exists(Path.GetDirectoryName(outputPath))) Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
				output.Save(outputPath, ImageFormat.Jpeg);
			}
		}           
        public void MarkupTextureTransforms(string texturePath, RectangleTransform[] transforms, TextureVertex[] uvs, Vector2 tile)
		{
            using (Image output = Image.FromFile(texturePath))
            {
                using (Graphics g = Graphics.FromImage(output))
                {
                    g.DrawRectangles(new Pen(Color.Red, 10), transforms.Select(t => t.ToRectangle(output.Size)).ToArray());
                }

                // Write to disk
                WriteDebugImage(output, texturePath, string.Format("transforms_{0}_{1}", tile.X, tile.Y));
            }       
		}      */


		// The z axis is collapsed for the purpose of texture slicing.
		// Texture tiles correlate to a column of mesh data which is unbounded in the Z axis.
		public RectangleTransform[] GenerateTextureTile(string outputPath, Vector2 tile, SlicingOptions options, CancellationToken cancellationToken)
		{                        
			List<Face> chunkFaceList = GetFaceListFromTextureTile(options.TextureSliceY, options.TextureSliceX, tile.X, tile.Y, TargetObj).ToList();
            
			if (!chunkFaceList.Any())
			{
				Trace.TraceInformation("No faces found in tile {0}.  No texture generated.", tile);
				return new RectangleTransform[0];
			}

            Size originalSize;

            // Create a clone of the source to use independent of other threads
                originalSize = source.Size();
                Size newSize = new Size();

                Trace.TraceInformation("Generating sparse texture for tile {0}", tile);

                // Identify blob rectangles
                var groupedFaces = FindConnectedFaces(chunkFaceList, cancellationToken);
                var uvRects = FindUVRectangles(groupedFaces);
                Rectangle[] sourceRects = TransformUVRectToBitmapRect(uvRects, originalSize, 3);


                // Bin pack rects, growing to a maximum 16384.
                // Estimate ideal bin size
                var totalArea = sourceRects.Sum(r => r.Height * r.Width);
                var startingSize = NextPowerOfTwo((int)Math.Sqrt(totalArea));
                Rectangle[] destinationRects = PackTextures(sourceRects, startingSize, startingSize/2, 16384, cancellationToken);

                // Identify the cropped size of our new texture			
                newSize.Width = destinationRects.Max<Rectangle, int>(r => r.X + r.Width);
                newSize.Height = destinationRects.Max<Rectangle, int>(r => r.Y + r.Height);

                // Round new texture size up to nearest power of 2
                newSize.Width = NextPowerOfTwo(newSize.Width);
                newSize.Height = NextPowerOfTwo(newSize.Height);

                // Build the new bin packed and cropped texture
                WriteNewTexture(outputPath, options.TextureScale, newSize, source, sourceRects, destinationRects, cancellationToken);

                // Write an MTL if appropriate
                if (options.WriteMtl)
                {
                    WriteNewMtl(outputPath, Path.ChangeExtension(outputPath, "mtl"));
                }

                // Generate the UV transform array
                return GenerateUVTransforms(originalSize, newSize, sourceRects, destinationRects);           
		}

        private void WriteNewMtl(string texturePath, string outputPath)
        {
            // Ensure folder exists, file doesn't
            if (File.Exists(outputPath)) File.Delete(outputPath);
            if (!Directory.Exists(Path.GetDirectoryName(outputPath))) Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            // Write the MTL
            StringBuilder mtl = new StringBuilder();
            mtl.AppendLine("# Generated by Pyrite CLI");
            mtl.AppendLine("newmtl material_0");
            mtl.AppendLine("Ka 0.200000 0.200000 0.200000");
            mtl.AppendLine("Kd 0.000000 0.000000 0.000000");
            mtl.AppendLine("Ks 1.000000 1.000000 1.000000");
            mtl.AppendLine("Tr 0.000000");
            mtl.AppendLine("illum 2");
            mtl.AppendLine("Ns 0.000000");
            mtl.Append("map_Kd texture\\");
            mtl.Append(Path.GetFileName(texturePath));

            // Write to disk
            File.WriteAllText(outputPath, mtl.ToString());
        }

        private static void WriteNewTexture(string outputPath, float scale, Size newSize, Bgr<byte>[,] source, Rectangle[] sourceRects, Rectangle[] destinationRects, CancellationToken cancellationToken)
		{
            Bgr<byte>[,] packed = new Bgr<byte>[newSize.Width, newSize.Height];
            int sourceWidth = source.GetLength(0);
            int sourceHeight = source.GetLength(1);

			for (int i = 0; i < sourceRects.Length; i++)
			{
                cancellationToken.ThrowIfCancellationRequested();

                Point destinationOffset = destinationRects[i].Location;
                Rectangle sourceArea = sourceRects[i];

                for (int x = 0; x < sourceArea.Width; x++)
                {
                    for (int y = 0; y < sourceArea.Height; y++)
                    {                                          
                        if ((sourceArea.Y + y) >= 0 && (sourceArea.X + x) >= 0 && (sourceArea.X + x) < sourceWidth && (sourceArea.Y + y) < sourceHeight)
                        {
                            packed[destinationOffset.X + x, destinationOffset.Y + y] = source[sourceArea.X + x, sourceArea.Y + y];
                        }
                    }
                }
            }	

			// Write to disk
			if (File.Exists(outputPath)) File.Delete(outputPath);
			if (!Directory.Exists(Path.GetDirectoryName(outputPath))) Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

			if (scale != 1)
			{
                
                throw new NotImplementedException("Scaling not yet implemented in this branch.");
			}          
			else
			{
                packed.Save(outputPath);				
			}
			
		}

		private static RectangleTransform[] GenerateUVTransforms(Size originalSize, Size newSize, Rectangle[] sourceRects, Rectangle[] destinationRects)
		{
			var outputTransforms = new RectangleTransform[sourceRects.Length];
			for (int i = 0; i < sourceRects.Length; i++)
			{
				Rectangle s = sourceRects[i];
				Rectangle d = destinationRects[i];

				var transform = new RectangleTransform
				{
					// figure out total image size and convert rects to percentages
					Top = 1 - (s.Top / (double)originalSize.Height),
					Bottom = 1 - (s.Bottom / (double)originalSize.Height),
					Left = s.Left / (double)originalSize.Width,
					Right = s.Right / (double)originalSize.Width,
					OffsetX = (s.Left / (double)originalSize.Width) - (d.Left / (double)newSize.Width),
					OffsetY = ((s.Top / (double)originalSize.Height) - (d.Top / (double)newSize.Height)),
					ScaleX = (double)originalSize.Width / (double)newSize.Width,
					ScaleY = (double)originalSize.Height / (double)newSize.Height
				};

				outputTransforms[i] = transform;
			}

			return outputTransforms;
		}

		private static Rectangle[] TransformUVRectToBitmapRect(RectangleF[] uvRects, Size textureSize, int pixelBuffer)
		{
			Rectangle[] rects = new Rectangle[uvRects.Length];

			for (int i = 0; i < uvRects.Length; i++)
			{
				var r = uvRects[i];
				rects[i] = new Rectangle(
					(int)(r.X * textureSize.Width) - pixelBuffer, 
					(int)((1 - r.Bottom) * textureSize.Height) - pixelBuffer, 
					(int)(r.Width * textureSize.Width) + (pixelBuffer * 2), 
					(int)(r.Height * textureSize.Height) + (pixelBuffer * 2));
			}

			return rects;
		}

		private RectangleF[] FindUVRectangles(List<List<Face>> groupedFaces)
		{
			int groupCount = groupedFaces.Count();
			
			RectangleF[] rects = new RectangleF[groupCount];

			for (int i = 0; i < groupCount; i++)
			{
				var triangles = GetUVTriangles(groupedFaces[i]);
				var uvs = triangles.SelectMany(t => new List<TextureVertex> { t.Item1, t.Item2, t.Item3 }).Distinct();
				float minX = (float)uvs.Min(uv => uv.X);
				float maxX = (float)uvs.Max(uv => uv.X);
				float minY = (float)uvs.Min(uv => uv.Y);
				float maxY = (float)uvs.Max(uv => uv.Y);

				rects[i] = new RectangleF(minX, minY, maxX - minX, maxY - minY);
			}

			// Remove rectangles fully contained by others
			List<RectangleF> cleanRects = new List<RectangleF>();
			for (int i = 0; i < rects.Length; i++)
			{
				bool containedElsewhere = false;
				for (int j = 0; j < rects.Length; j++)
				{
					if (i != j)
					{
						if (rects[j].Contains(rects[i]))
						{
							containedElsewhere = true;
							break;
						}
					}
				}
				if (!containedElsewhere) cleanRects.Add(rects[i]);
			}

			if (cleanRects.Count() < rects.Length)
			{
				Trace.TraceInformation("Removed {0} obscured rectangles", rects.Length - cleanRects.Count());
			}

			return cleanRects.ToArray();

			//return rects;
        }

		private static List<List<Face>> FindConnectedFaces(List<Face> faces, CancellationToken cancellationToken)
		{
			var remainingFaces = new List<Face>(faces);

			var groupedFaces = new List<List<Face>>();

			while (remainingFaces.Any())
			{				
				var newGroup = new List<Face> { remainingFaces[0] };
				groupedFaces.Add(newGroup);

				// Pull the first face off the list and work on it				
				List<Face> matches = new List<Face> { remainingFaces[0] };

				remainingFaces.RemoveAt(0);

				// Intersect and move to group until no more intersections are found.
				do
				{
					matches = remainingFaces.AsParallel().Where(f => FacesIntersect(f, matches)).WithCancellation(cancellationToken).ToList();

					newGroup.AddRange(matches);	
					foreach (var f in matches)
					{
						remainingFaces.Remove(f);
					}
				}
				while (matches.Any());				
			}

			return groupedFaces;
		}


		private static bool FacesIntersect(Face f, List<Face> matches)
		{
			foreach (var m in matches)
			{
                foreach (int vt in m.TextureVertexIndexList)
                {
                    if (f.TextureVertexIndexHash.Contains(vt))
                    {
                        return true;
                    }
                }
            }

			return false;
		}

		private List<Tuple<TextureVertex, TextureVertex, TextureVertex>> GetUVTriangles(List<Face> chunkFaceList)
		{
			return chunkFaceList.Select(f => new Tuple<TextureVertex, TextureVertex, TextureVertex>(
                            TargetObj.TextureList[f.TextureVertexIndexList[0] - 1],
                            TargetObj.TextureList[f.TextureVertexIndexList[1] - 1],
                            TargetObj.TextureList[f.TextureVertexIndexList[2] - 1])).ToList();
		}

		public static IEnumerable<Face> GetFaceListFromTextureTile(int gridHeight, int gridWidth, int tileX, int tileY, Obj obj)
		{
            return GetCubeListFromTextureTile(gridHeight, gridWidth, tileX, tileY, obj).AsParallel().SelectMany(v => obj.FaceMatrix[v.X, v.Y, v.Z]);
        }

        public static IEnumerable<Vector3> GetCubeListFromTextureTile(int gridHeight, int gridWidth, int tileX, int tileY, Obj obj)
        {
            int xRatio = obj.FaceMatrix.GetLength(0) / gridWidth;
            int yRatio = obj.FaceMatrix.GetLength(1) / gridHeight;

            int maxZ = obj.FaceMatrix.GetLength(2);
            return from x in Enumerable.Range(tileX * xRatio, xRatio)
                   from y in Enumerable.Range(tileY * yRatio, yRatio)
                   from z in Enumerable.Range(0, maxZ)
                   select new Vector3(x, y, z);
        }

        public static Vector2 GetTextureCoordFromCube(int gridHeight, int gridWidth, int cubeX, int cubeY, Obj obj)
        {
            int xRatio = obj.FaceMatrix.GetLength(0) / gridWidth;
            int yRatio = obj.FaceMatrix.GetLength(1) / gridHeight;

            return new Vector2((int)Math.Floor(cubeX / (double)xRatio), (int)Math.Floor(cubeY / (double)yRatio));
        }

        private Rectangle[] PackTextures(Rectangle[] source, int width, int height, int maxSize, CancellationToken cancellationToken)
		{
            Trace.TraceInformation("Bin packing {0} rectangles", source.Length);
            Stopwatch stopwatch = Stopwatch.StartNew();

            if (width > maxSize || height > maxSize) return null;			

			MaxRectanglesBinPack bp = new MaxRectanglesBinPack(width, height, false);
			Rectangle[] rects = new Rectangle[source.Length];

			for (int i = 0; i < source.Length; i++)
			{   
				Rectangle rect = bp.Insert(source[i].Width, source[i].Height, MaxRectanglesBinPack.FreeRectangleChoiceHeuristic.RectangleBestAreaFit, cancellationToken);
				if (rect.Width == 0 || rect.Height == 0)
				{
					return PackTextures(source, width * (width <= height ? 2 : 1), height * (height < width ? 2 : 1), maxSize, cancellationToken);
				}
				rects[i] = rect;
			}

            Trace.TraceInformation("Bin packing time for {0} by {1} texture: " + stopwatch.Elapsed.ToString(), width, height);

            return rects;
		}

        /*
        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }     

        private string WriteDebugImage(Image source, string outputPath, string prefix = "error")
        {
            string directory = Path.GetDirectoryName(outputPath);
            string filename = string.Format(prefix + "-{0:yyyy-MM-dd_hh-mm-ss-tt}.jpeg", DateTime.Now);
            string newPath = Path.Combine(directory, filename);

            if (File.Exists(newPath)) File.Delete(newPath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            source.Save(newPath, ImageFormat.Jpeg);

            return filename;         
        }       */

		public static int NextPowerOfTwo(int x)
		{
			x--;
			x |= (x >> 1);
			x |= (x >> 2);
			x |= (x >> 4);
			x |= (x >> 8);
			x |= (x >> 16);
			return (x + 1);
		}
	}
}
