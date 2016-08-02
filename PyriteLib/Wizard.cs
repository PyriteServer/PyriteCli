using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PyriteLib
{
    public class Wizard
    {
        public Wizard(string[] objs, SlicingOptions options)
        {
            SortedList<int, Obj> objects = new SortedList<int, Obj>();

            foreach(string item in objs)
            {
                Trace.TraceInformation("Loading {0}", item);

                var ObjInstance = new Obj();
                ObjInstance.LoadObj(item, null, new Vector3(128, 128, 128), options);
                ObjInstance.Title = Path.GetFileNameWithoutExtension(item);             
                objects.Add(ObjInstance.VertexList.Count(), ObjInstance);
                
            }

            for(int key=0;key < objects.Count(); key++)
            {
                var obj = objects[objects.Keys[key]];
                var result = MaxVertices(obj, 60000);
                Trace.TraceInformation("{0} [{4}] {1} vertices.  Recommended {2}x{2}x{2}, {3} max vert", key, objects.Keys[key], result.Item1, result.Item2, obj.Title);
            }

        }

        private Tuple<int, int> MaxVertices(Obj obj, int maxVertices)
        {
            var matrix = obj.FaceMatrix;

            SortedList<int, int> faces = new SortedList<int, int>();

            faces.Add(4, GetMax(4, obj));
            faces.Add(8, GetMax(8, obj));
            faces.Add(16, GetMax(16, obj));
            faces.Add(32, GetMax(32, obj));
            faces.Add(64, GetMax(64, obj));
            faces.Add(128, GetMax(128, obj));

            var best = faces.Where(f => f.Value < maxVertices).OrderBy(f => f.Key).First();
            
            return new Tuple<int,int>(best.Key, best.Value);
        }

        private static int GetMax(int gridSize, Obj obj)
        {
            float ratio = ((float)obj.FaceList.Count / (float)obj.VertexList.Count);
            int max = 0;
            SpatialUtilities.EnumerateSpace(new Vector3(gridSize, gridSize, gridSize), (x, y, z) =>
            {
                var faceCount = GetCubeList(gridSize, x, y, z, obj).Sum();
                var vertexCount = (int)(faceCount * ratio);
                if (vertexCount > max)
                {
                    max = vertexCount;
                }
            });

            return max;
        }

        private static IEnumerable<int> GetCubeList(int gridSize, int tileX, int tileY, int tileZ, Obj obj)
        {
            int ratio = 128 / gridSize;

            return from x in Enumerable.Range(tileX * ratio, ratio)
                   from y in Enumerable.Range(tileY * ratio, ratio)
                   from z in Enumerable.Range(tileZ * ratio, ratio)
                   select obj.FaceMatrix[x, y, z].Count();
        }
    }
}
