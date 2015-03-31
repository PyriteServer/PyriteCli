﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using af = AForge.Imaging;
using CuberLib.Types;

namespace CuberLib
{
	public class Texture
	{
		private Obj obj;

		public Texture(Obj obj)
		{
			this.obj = obj;
		}

		public void MarkupTextureFaces(string texturePath, string outputPath)
		{
			var triangles = GetUVTriangles(obj.FaceList);

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

		// The z axis is collapsed for the purpose of texture slicing.
		// Texture tiles correlate to a column of mesh data which is unbounded in the Z axis.
		public RectangleTransform[] GenerateTextureTile(string texturePath, string outputPath, int gridHeight, int gridWidth, int tileX, int tileY)
		{
			List<Face> chunkFaceList = GetFaceList(gridHeight, gridWidth, tileX, tileY);

			// get list of UV triangles
			var triangles = GetUVTriangles(chunkFaceList);

			if (!triangles.Any())
			{
				return new RectangleTransform[0];
			}

			Size originalSize;
			Size newSize = new Size();

			// Load original texture and initiate new texture
			using (Bitmap output = GenerateSparseTexture(texturePath, triangles))
			{
				// Identify blob rectangles
				Rectangle[] sourceRects = FindBlobRectangles(output);

				// Bin pack rects, starting with 1024x1024 and growing to a maximum 8192.
				Rectangle[] destinationRects = PackTextures(sourceRects, 1024, 1024, 8192);

				// Identify the cropped size of our new texture
				originalSize = output.Size;
				newSize.Width = destinationRects.Max<Rectangle, int>(r => r.X + r.Width);
				newSize.Height = destinationRects.Max<Rectangle, int>(r => r.Y + r.Height);

				// Build the new bin packed and cropped texture
				using (Bitmap packed = new Bitmap(newSize.Width, newSize.Height))
				{
					using (Graphics packedGraphics = Graphics.FromImage(packed))
					{
						for (int i = 0; i < sourceRects.Length; i++)
						{
							packedGraphics.DrawImage(output, destinationRects[i], sourceRects[i], GraphicsUnit.Pixel);
						}
					}

					// Write to disk
					if (File.Exists(outputPath)) File.Delete(outputPath);
					if (!Directory.Exists(Path.GetDirectoryName(outputPath))) Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
					packed.Save(outputPath, ImageFormat.Jpeg);
				}

				// Generate the UV transform array
				var outputTransforms = new RectangleTransform[sourceRects.Length];
				for (int i = 0; i < sourceRects.Length; i++)
				{
					var transform = new RectangleTransform
					{
						// figure out total image size and convert rects to percentages
						Top = sourceRects[i].Top / (double)originalSize.Height,
						Bottom = sourceRects[i].Bottom / (double)originalSize.Height,
						Left = sourceRects[i].Left / (double)originalSize.Width,
						Right = sourceRects[i].Right / (double)originalSize.Width,
						OffsetX = (sourceRects[i].Left / (double)originalSize.Width) - (destinationRects[i].Left / (double)newSize.Width),
						OffsetY = (sourceRects[i].Top / (double)originalSize.Height) - (destinationRects[i].Top / (double)newSize.Height)
					};

					outputTransforms[i] = transform;
				}

				return outputTransforms;
			}
		}

		private static Rectangle[] FindBlobRectangles(Bitmap output)
		{
			af.BlobCounter bc = new af.BlobCounter();
			bc.ProcessImage(output);
			Rectangle[] sourceRects = bc.GetObjectsRectangles();
			return sourceRects;
		}

		private Bitmap GenerateSparseTexture(string texturePath, List<Tuple<TextureVertex, TextureVertex, TextureVertex>> triangles)
		{
			Image original = Image.FromFile(texturePath);
			Bitmap output = new Bitmap(original.Width, original.Height);
			using (Graphics destGraphics = Graphics.FromImage(output))
			{

				// write into same location in new bitmap
				for (int i = 0; i < triangles.Count; i++)
				{
					var triangle = triangles[i];
					var poly = new PointF[] {
					new PointF((float)(triangle.Item1.X * original.Width), (float)((1-triangle.Item1.Y) * original.Height)),
					new PointF((float)(triangle.Item2.X * original.Width), (float)((1-triangle.Item2.Y) * original.Height)),
					new PointF((float)(triangle.Item3.X * original.Width), (float)((1-triangle.Item3.Y) * original.Height)),
					new PointF((float)(triangle.Item1.X * original.Width), (float)((1-triangle.Item1.Y) * original.Height))
					};

					CopyPolygon(original, destGraphics, poly, poly);

					if (i % 10000 == 0)
					{
						Console.WriteLine("{0} of {1}", i, triangles.Count);
					}
				}

				destGraphics.ResetClip();
			}

			original.Dispose();
			return output;
		}

		private List<Tuple<TextureVertex, TextureVertex, TextureVertex>> GetUVTriangles(List<Face> chunkFaceList)
		{
			return chunkFaceList.Select(f => new Tuple<TextureVertex, TextureVertex, TextureVertex>(
							obj.TextureList[f.TextureVertexIndexList[0] - 1],
							obj.TextureList[f.TextureVertexIndexList[1] - 1],
							obj.TextureList[f.TextureVertexIndexList[2] - 1])).ToList();
		}

		private List<Face> GetFaceList(int gridHeight, int gridWidth, int tileX, int tileY)
		{
			double tileHeight = obj.Size.YSize / gridHeight;
			double tileWidth = obj.Size.XSize / gridWidth;

			double yOffset = tileHeight * tileY;
			double xOffset = tileWidth * tileX;

			Extent newSize = new Extent
			{
				XMin = obj.Size.XMin + xOffset,
				YMin = obj.Size.YMin + yOffset,
				ZMin = obj.Size.ZMin,
				XMax = obj.Size.XMin + xOffset + tileWidth,
				YMax = obj.Size.YMin + yOffset + tileHeight,
				ZMax = obj.Size.ZMax
			};

			List<Face> chunkFaceList;
			chunkFaceList = obj.FaceList.AsParallel().Where(v => v.InExtent(newSize, obj.VertexList)).ToList();
			return chunkFaceList;
		}

		private Rectangle[] PackTextures(Rectangle[] source, int width, int height, int maxSize)
		{
			if (width > maxSize || height > maxSize) return null;			

			MaxRectanglesBinPack bp = new MaxRectanglesBinPack(width, height, false);
			Rectangle[] rects = new Rectangle[source.Length];

			for (int i = 0; i < source.Length; i++)
			{
				Rectangle rect = bp.Insert(source[i].Width, source[i].Height, MaxRectanglesBinPack.FreeRectangleChoiceHeuristic.RectangleBestAreaFit);
				if (rect.Width == 0 || rect.Height == 0)
				{
					return PackTextures(source, width * (width <= height ? 2 : 1), height * (height < width ? 2 : 1), maxSize);
				}
				rects[i] = rect;
			}

			return rects;
		}


		private void CopyPolygon(Image source, Graphics dest, PointF[] sourcePoly, PointF[] destPoly)
		{
			using (GraphicsPath gpdest = new GraphicsPath())
			using (GraphicsPath gpdestWide = new GraphicsPath())
			{
				gpdest.AddPolygon(destPoly);
				gpdestWide.AddPolygon(destPoly);
				gpdestWide.Widen(Pens.Black);

				//Draw on the Bitmap
				dest.SetClip(gpdest);
				dest.SetClip(gpdestWide, CombineMode.Union);
				dest.DrawImage(source, 0, 0);
			}
		}
	}
}
