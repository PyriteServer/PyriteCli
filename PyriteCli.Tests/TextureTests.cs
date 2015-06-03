using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PyriteLib;
using PyriteLib.Types;
using System.Linq;
using System.Drawing;
using System.Diagnostics;

namespace PyriteLib.Tests
{
    [TestClass()]
    public class TextureTests
    {
        [TestMethod()]
        public void NextPowerOfTwoTest()
        {
            Assert.AreEqual(1024, Texture.NextPowerOfTwo(1023));
            Assert.AreEqual(1024, Texture.NextPowerOfTwo(1020));
            Assert.AreEqual(2048, Texture.NextPowerOfTwo(2048));
            Assert.AreEqual(2048, Texture.NextPowerOfTwo(1025));
        }
    }
}

namespace PyriteCli.Tests
{
	[TestClass]
	[DeploymentItem(@"SampleData\model.obj")]
	[DeploymentItem(@"SampleData\texture.jpg")]
    public class TextureTests
	{
		[TestMethod]
		public void FindConnectedFacesTest()
		{
			List<Face> faces = new List<Face>();
			faces.Add(new Face() { TextureVertexIndexList = new int[] { 1, 2, 3 } });
			faces.Add(new Face() { TextureVertexIndexList = new int[] { 1, 4, 5 } });
			faces.Add(new Face() { TextureVertexIndexList = new int[] { 5, 6, 4 } });

			faces.Add(new Face() { TextureVertexIndexList = new int[] { 7, 8, 9 } });
			faces.Add(new Face() { TextureVertexIndexList = new int[] { 10, 11, 8 } });
			faces.Add(new Face() { TextureVertexIndexList = new int[] { 11, 12, 13 } });

			faces.Add(new Face() { TextureVertexIndexList = new int[] { 14, 15, 16 } });

			foreach (var f in faces)
			{
				f.TextureVertexIndexHash = new HashSet<int>(f.TextureVertexIndexList);
			}

			// private static IEnumerable<IEnumerable<Face>> FindConnectedFaces(List<Face> faces)
			PrivateType texture = new PrivateType(typeof(Texture));
			var result = (IEnumerable<IEnumerable<Face>>)texture.InvokeStatic("FindConnectedFaces", new Object[] { faces });

			Assert.AreEqual(3, result.Count());
			Assert.AreEqual(7, result.Sum(g => g.Count()));
		}

		[TestMethod]
		public void FindConnectedFacesPerf()
		{
			// private static IEnumerable<IEnumerable<Face>> FindConnectedFaces(List<Face> faces)
			PrivateType textureType = new PrivateType(typeof(Texture));

			// private RectangleF[] FindUVRectangles(List<List<Face>> groupedFaces)
			PrivateObject textureObject = new PrivateObject(GetTestTexture());

			// private List<Face> GetFaceList(int gridHeight, int gridWidth, int tileX, int tileY, bool cubical)
			List<Face> faces = (List<Face>)textureObject.Invoke("GetFaceList", new Object[] { 2, 2, 0, 1, false });

			Stopwatch watch = Stopwatch.StartNew();
			for (int i = 0; i < 10; i++)
			{
				var result = (IEnumerable<IEnumerable<Face>>)textureType.InvokeStatic("FindConnectedFaces", new Object[] { faces });
			}
			Console.WriteLine("Connected Faces Milliseconds: " + watch.ElapsedMilliseconds);			
		}

		[TestMethod]
		public void FindUVRectanglesTest()
		{
			// private static IEnumerable<IEnumerable<Face>> FindConnectedFaces(List<Face> faces)
			PrivateType textureType = new PrivateType(typeof(Texture));

			// private RectangleF[] FindUVRectangles(List<List<Face>> groupedFaces)
			PrivateObject textureObject = new PrivateObject(GetTestTexture());

			// private List<Face> GetFaceList(int gridHeight, int gridWidth, int tileX, int tileY, bool cubical)
			List<Face> faces = (List<Face>)textureObject.Invoke("GetFaceList", new Object[] { 2, 2, 0, 1, false });

			var result = (IEnumerable<IEnumerable<Face>>)textureType.InvokeStatic("FindConnectedFaces", new Object[] { faces });

			RectangleF[] rectangles = (RectangleF[])textureObject.Invoke("FindUVRectangles", new Object[] { result });

			// We got our rects
			Assert.AreEqual(113, rectangles.Count());

			// None of them are contained inside other ones
			for (int i = 0; i < rectangles.Length; i++)
				for (int j = 0; j < rectangles.Length; j++)
					if (i != j)
						if (rectangles[j].Contains(rectangles[i]))
							Assert.Fail("Rectangle {0} contains {1}", rectangles[j].ToString(), rectangles[i].ToString());

		}

		private Texture GetTestTexture()
		{
			var options = new SlicingOptions
			{				
				GenerateObj = true,
				Texture = "texture.jpg",
				TextureScale = 1,
				TextureSliceX = 2,
				TextureSliceY = 2,
                ForceCubicalCubes = false,
                Obj = "model.obj",
                CubeGrid = new Vector3(2, 2, 2)
            };

            CubeManager manager = new CubeManager(options);

            return new Texture(manager.ObjInstance);
		}
	}
}
