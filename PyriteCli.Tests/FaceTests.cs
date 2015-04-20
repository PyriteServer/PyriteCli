using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PyriteLib;
using PyriteLib.Types;
using System.Linq;
using System.Drawing;
using System.Diagnostics;

namespace PyriteCli.Tests
{
	[TestClass]
	[DeploymentItem(@"SampleData\model.obj")]
	[DeploymentItem(@"SampleData\texture.jpg")]
    public class FaceTests
	{

		[TestMethod]
		public void FindFacesInExtentPerf()
		{
			CubeManager manager = GetLoadedManager();



			Stopwatch watch = Stopwatch.StartNew();
			for (int i = 0; i < 100; i++)
			{
				List<Face> chunkFaceList;
				chunkFaceList = manager.ObjInstance.FaceList.AsParallel().Where(
					v => v.InExtent(manager.ObjInstance.Size, manager.ObjInstance.VertexList)).ToList();
			}
			Trace.WriteLine(watch.ElapsedMilliseconds, "Faces In Extent Time (MS): ");			
		}

		private CubeManager GetLoadedManager()
		{
			var options = new SlicingOptions
			{				
				GenerateObj = true,
				Texture = "texture.jpg",
				TextureScale = 1,
				TextureSliceX = 2,
				TextureSliceY = 2,
				ForceCubicalCubes = false
			};

			CubeManager manager = new CubeManager("model.obj", 2, 2, 2);

			return manager;
		}
	}
}
