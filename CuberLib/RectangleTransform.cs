using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CuberLib
{
	// Represents a transform that applies to data within a specific rectangle
	// Does not currently support rotation.
	// Everything scaled to 0-1.
	public class RectangleTransform
	{
		public double Left { get; set; }
		public double Right { get; set; }
		public double Top { get; set; }
		public double Bottom { get; set; }
		public double OffsetX { get; set; }
		public double OffsetY { get; set; }

		public bool ContainsPoint(double x, double y)
		{
			return (x >= Left && x <= Right && y >= Bottom && y <= Top);
		}
	}
}
