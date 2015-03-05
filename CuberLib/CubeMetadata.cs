using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CuberLib
{
	public class CubeMetadata
	{
		public bool[,,] CubeExists { get; set; }
		public Extent Extents { get; set; }

		public XyzPoint GridSize { get; set; }

		public CubeMetadata(XyzPoint size)
		{
			CubeExists = new bool[size.X, size.Y, size.Z];
		}
	}
}
