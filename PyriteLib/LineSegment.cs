using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PyriteLib
{
    public class LineSegment
    {
        public Vector3D Start { get; set; }
        public Vector3D End { get; set; }

        public double Length()
        {
            return Math.Sqrt(Math.Pow(End.Z - Start.Z, 2) + Math.Pow(End.X - Start.X, 2) + Math.Pow(End.Y - Start.Y, 2));
        }

        public static double dot(Vector3D u, Vector3D v)
        {
            return (u.X * v.X + u.Y * v.Y + u.Z * v.Z);
        }

        public Vector3D IntersectTriangle(Vector3D t0, Vector3D t1, Vector3D t2)
        {
            Vector3D u, v, n;              // triangle vectors
            Vector3D dir, w0, w;           // ray vectors
            double r, a, b;              // params to calc ray-plane intersect

            // get triangle edge vectors and plane normal
            u = t1 - t0;
            v = t2 - t0;
            n = u * v;              // cross product
            if (n.IsZero())             // triangle is degenerate
                return null;                  // do not deal with this case

            dir = End - Start;              // ray direction vector
            w0 = Start - t0;
            a = -dot(n, w0);
            b = dot(n, dir);
            if (Math.Abs(b) < 0.00000001)
            {     // ray is  parallel to triangle plane
                return null;
            }

            // get intersect point of ray with triangle plane
            r = a / b;
            if (r < 0.0)
            {
                // ray goes away from triangle
                return null;                   // => no intersect
                                            // for a segment, also test if (r > 1.0) => no intersect
            }

            Vector3D intersect = Start + dir * r;            // intersect point of ray and plane

            // is I inside T?
            double uu, uv, vv, wu, wv, D;
            uu = dot(u, u);
            uv = dot(u, v);
            vv = dot(v, v);
            w = intersect - t0;
            wu = dot(w, u);
            wv = dot(w, v);
            D = uv * uv - uu * vv;

            // get and test parametric coords
            double s, t;
            s = (uv * wv - vv * wu) / D;
            if (s < 0.0 || s > 1.0)         // I is outside T
                return null;
            t = (uv * wu - uu * wv) / D;
            if (t < 0.0 || (s + t) > 1.0)  // I is outside T
                return null;

            return intersect;                     // I is in T
        }
    }
}
