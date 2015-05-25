using PyriteLib.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PyriteLib
{
	public class Vector3D
	{
		public double X { get; set; }
		public double Y { get; set; }
		public double Z { get; set; }

        public Vector3D(double X, double Y, double Z)
        {
            this.X = X;
            this.Y = Y;
            this.Z = Z;
        }

        public Vector3D(Vertex vertex)
        {
            this.X = vertex.X;
            this.Y = vertex.Y;
            this.Z = vertex.Z;
        }

        public static Vector3D operator -(Vector3D v1, Vector3D v2)
        {
            return new Vector3D(v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z);
        }

        public static Vector3D operator +(Vector3D v1, Vector3D v2)
        {
            return new Vector3D(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
        }

        public static Vector3D operator *(Vector3D v1, Vector3D v2)
        {
            return new Vector3D(v1.X * v2.X, v1.Y * v2.Y, v1.Z * v2.Z);
        }

        public static Vector3D operator /(Vector3D v1, Vector3D v2)
        {
            return new Vector3D(v1.X / v2.X, v1.Y / v2.Y, v1.Z / v2.Z);
        }

        public static Vector3D operator *(Vector3D v, Double d)
        {
            return new Vector3D(v.X * d, v.Y * d, v.Z * d);
        }

        public static Vector3D operator /(Vector3D v, Double d)
        {
            return new Vector3D(v.X / d, v.Y / d, v.Z / d);
        }
    }
}
