using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PyriteLib
{
    public class Extent
    {
        public double XMax { get; set; }
        public double XMin { get; set; }
        public double YMax { get; set; }
        public double YMin { get; set; }
        public double ZMax { get; set; }
        public double ZMin { get; set; }

        public double XSize { get { return XMax - XMin; }}
        public double YSize { get { return YMax - YMin; } }
        public double ZSize { get { return ZMax - ZMin; } }

        public Vector3D MaxCorner { get { return new Vector3D(XMax, YMax, ZMax); } }
        public Vector3D MinCorner { get { return new Vector3D(XMin, YMin, ZMin); } }

        public LineSegment[] Edges
        {
            get
            {
                var c1 = new Vector3D(XMax, YMax, ZMax);
                var c2 = new Vector3D(XMax, YMax, ZMin);
                var c3 = new Vector3D(XMax, YMin, ZMax);
                var c4 = new Vector3D(XMin, YMax, ZMax);
                var c5 = new Vector3D(XMax, YMin, ZMin);
                var c6 = new Vector3D(XMin, YMin, ZMax);
                var c7 = new Vector3D(XMin, YMax, ZMin);
                var c8 = new Vector3D(XMin, YMin, ZMin);

                return new LineSegment[]
                {
                    new LineSegment { Start = c1, End = c2 },
                    new LineSegment { Start = c1, End = c3 },
                    new LineSegment { Start = c1, End = c4 },

                    new LineSegment { Start = c6, End = c3 },
                    new LineSegment { Start = c6, End = c4 },
                    new LineSegment { Start = c6, End = c8 },

                    new LineSegment { Start = c5, End = c3 },
                    new LineSegment { Start = c5, End = c2 },
                    new LineSegment { Start = c5, End = c8 },

                    new LineSegment { Start = c7, End = c4 },
                    new LineSegment { Start = c7, End = c2 },
                    new LineSegment { Start = c7, End = c8 }

                };
            }
        }

    }
}
