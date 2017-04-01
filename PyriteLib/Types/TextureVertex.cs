using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PyriteLib.Types
{
    public class TextureVertex : IType
    {
        public const int MinimumDataLength = 3;
        public const string Prefix = "vt";

        public double X { get; set; }

        public double Y { get; set; }

		public double OriginalX { get; set; }

		public double OriginalY { get; set; }

		public int Index { get; set; }

		public bool Transformed { get; set; }

		public void LoadFromStringArray(string[] data)
        {
            if (data.Length < MinimumDataLength)
                throw new ArgumentException("Input array must be of minimum length " + MinimumDataLength, "data");

            if (!data[0].ToLower().Equals(Prefix))
                throw new ArgumentException("Data prefix must be '" + Prefix + "'", "data");

            bool success;

            double x, y;

            success = double.TryParse(data[1], NumberStyles.Any, CultureInfo.InvariantCulture, out x);
            if (!success) throw new ArgumentException("Could not parse X parameter as double");

            success = double.TryParse(data[2], NumberStyles.Any, CultureInfo.InvariantCulture, out y);
            if (!success) throw new ArgumentException("Could not parse Y parameter as double");

            X = OriginalX = x;
            Y = OriginalY = y;
        }

        public bool InRectangleTransform(RectangleTransform transform)
        {
			return transform.ContainsPoint(OriginalX, OriginalY);
        }

		public void Transform(RectangleTransform transform)
		{
			if (Transformed) return;

			X += ((X - transform.Left) * transform.ScaleX) - (X - transform.Left);
			Y += ((Y - transform.Top) * transform.ScaleY) - (Y - transform.Top);

			X -= transform.OffsetX;
			Y += transform.OffsetY;

			Transformed = true;
		}

        public override string ToString()
        {
            return string.Format("vt {0} {1}", X, Y);
        }

		public int CloneOriginal(List<TextureVertex> textureVertexList)
        {
		    lock (textureVertexList)
		    {
		        int newIndex = textureVertexList.Count + 1;
		        textureVertexList.Add(new TextureVertex
		        {
		            Index = newIndex,
		            X = this.OriginalX,
		            Y = this.OriginalY,
		            OriginalX = this.OriginalX,
		            OriginalY = this.OriginalY,
		            Transformed = false
		        });

		        return newIndex;
		    }
        }

        public bool Near(double x, double y)
        {
            if ((Math.Abs(X - x) < 0.00001) && (Math.Abs(Y - y) < 0.00001)) return true;
            if ((Math.Abs(OriginalX - x) < 0.00001) && (Math.Abs(OriginalY - y) < 0.00001)) return true;
            return false;
        }
    }
}
