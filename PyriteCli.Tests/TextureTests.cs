using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PyriteLib;
using PyriteLib.Types;
using System.Linq;

namespace PyriteCli.Tests
{
	[TestClass]
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

			// private static IEnumerable<IEnumerable<Face>> FindConnectedFaces(List<Face> faces)
			PrivateType texture = new PrivateType(typeof(Texture));
			var result = (IEnumerable<IEnumerable<Face>>)texture.InvokeStatic("FindConnectedFaces", new Object[] { faces });

			Assert.AreEqual(3, result.Count());
			Assert.AreEqual(7, result.Sum(g => g.Count()));
		}
	}
}
