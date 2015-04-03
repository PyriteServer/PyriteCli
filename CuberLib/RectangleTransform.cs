using System;
using System.Collections.Generic;
using System.Drawing;
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
		private const int PRECISION = 6;

		public double Left { get; set; }
		public double Right { get; set; }
		public double Top { get; set; }
		public double Bottom { get; set; }
		public double OffsetX { get; set; }
		public double OffsetY { get; set; }
		public double ScaleX { get; set; }
		public double ScaleY { get; set; }

		public bool ContainsPoint(double x, double y)
		{
			return (
				Math.Round(x, PRECISION) >= Math.Round(Left, PRECISION) && 
				Math.Round(x, PRECISION) <= Math.Round(Right, PRECISION) && 
				Math.Round(y, PRECISION) >= Math.Round(Bottom, PRECISION) && 
				Math.Round(y, PRECISION) <= Math.Round(Top, PRECISION)
				);
		}

		public Rectangle ToRectangle(Size textureSize)
		{
			return new Rectangle((int)(Left * textureSize.Width), (int)(Top * textureSize.Height), (int)((Right - Left) * textureSize.Width), (int)((Bottom - Top) * textureSize.Height));
		}
	}
}
