﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PyriteLib
{
    public static class SpatialUtilities
    {
        /// <summary>
        /// Enumerates all points in the 3d Space
        /// </summary>
        /// <param name="X">Exclusive X max</param>
        /// <param name="Y">Exclusive Y max</param>
        /// <param name="Z">Exclusive Z max</param>
        /// <param name="Action">Action to preform</param>
        public static void EnumerateSpace(int X, int Y, int Z, Action<int, int, int> Action)
        {
            EnumerateSpace(new Vector3(X, Y, Z), Action);
        }

        public static void EnumerateSpace(Vector3 size, Action<int, int, int> Action)
        {
            for (int x = 0; x < size.X; x++)
            {
                for (int y = 0; y < size.Y; y++)
                {
                    for (int z = 0; z < size.Z; z++)
                    {
                        Action(x, y, z);
                    }
                }
            }
        }

        /// <summary>
        /// Enumerates all points in the 2d Space
        /// </summary>
        /// <param name="X">Exclusive X max</param>
        /// <param name="Y">Exclusive Y max</param>
        /// <param name="Action">Action to preform</param>
        public static void EnumerateSpace(int X, int Y, Action<int, int> Action)
        {
            EnumerateSpace(new Vector2(X, Y), Action);
        }

        public static void EnumerateSpace(Vector2 size, Action<int, int> Action)
        {
            for (int x = 0; x < size.X; x++)
            {
                for (int y = 0; y < size.Y; y++)
                {
                    Action(x, y);
                }
            }
        }

        public static void EnumerateSpaceParallel(int X, int Y, Action<int, int> Action)
        {
            EnumerateSpaceParallel(new Vector2(X, Y), Action);
        }

        public static void EnumerateSpaceParallel(Vector2 size, Action<int, int> Action)
        {
            var space = from x in Enumerable.Range(0, size.X)
                        from y in Enumerable.Range(0, size.Y)
                        select new Tuple<int, int>(x, y);

            Parallel.ForEach(space, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (spacePartition) =>
            {
                Action(spacePartition.Item1, spacePartition.Item2);
            });
        }

        public static void EnumerateSpaceParallel(int X, int Y, int Z, Action<int, int, int> Action)
        {
            EnumerateSpaceParallel(new Vector3(X, Y, Z), Action);
        }

        public static void EnumerateSpaceParallel(Vector3 size, Action<int, int, int> Action)
        {
            var space = from x in Enumerable.Range(0, size.X)
                        from y in Enumerable.Range(0, size.Y)
                        from z in Enumerable.Range(0, size.Z)
                        select new Tuple<int, int, int>(x, y, z);

            Parallel.ForEach(space, (spacePartition) =>
            {
                Action(spacePartition.Item1, spacePartition.Item2, spacePartition.Item3);
            });
        }

        public static bool GetIntersection(double fDst1, double fDst2, Vector3D P1, Vector3D P2, out Vector3D hit)
        {
            hit = null;

            if ((fDst1 * fDst2) >= 0.0f) return false;
            if (fDst1 == fDst2) return false;
            hit = P1 + (P2 - P1) * (-fDst1 / (fDst2 - fDst1));
            return true;
        }

        public static bool InBox(Vector3D Hit, Vector3D B1, Vector3D B2, int Axis) 
        {
            if ( Axis==1 && Hit.Z > B1.Z && Hit.Z<B2.Z && Hit.Y> B1.Y && Hit.Y<B2.Y) return true;
            if ( Axis==2 && Hit.Z > B1.Z && Hit.Z<B2.Z && Hit.X> B1.X && Hit.X<B2.X) return true;
            if ( Axis==3 && Hit.X > B1.X && Hit.X<B2.X && Hit.Y> B1.Y && Hit.Y<B2.Y) return true;
            return false;
        }

        // returns intersection point if line (L1, L2) intersects with the box (B1, B2)        
        public static Vector3D CheckLineBox(Vector3D B1, Vector3D B2, Vector3D L1, Vector3D L2)
        {
            if (L2.X < B1.X && L1.X < B1.X) return null;
            if (L2.X > B2.X && L1.X > B2.X) return null;
            if (L2.Y < B1.Y && L1.Y < B1.Y) return null;
            if (L2.Y > B2.Y && L1.Y > B2.Y) return null;
            if (L2.Z < B1.Z && L1.Z < B1.Z) return null;
            if (L2.Z > B2.Z && L1.Z > B2.Z) return null;
            if (L1.X > B1.X && L1.X < B2.X &&
                L1.Y > B1.Y && L1.Y < B2.Y &&
                L1.Z > B1.Z && L1.Z < B2.Z)
            {
                return L1;
            }

            Vector3D hit;

            if ((GetIntersection(L1.X - B1.X, L2.X - B1.X, L1, L2, out hit) && InBox(hit, B1, B2, 1))
              || (GetIntersection(L1.Y - B1.Y, L2.Y - B1.Y, L1, L2, out hit) && InBox(hit, B1, B2, 2))
              || (GetIntersection(L1.Z - B1.Z, L2.Z - B1.Z, L1, L2, out hit) && InBox(hit, B1, B2, 3))
              || (GetIntersection(L1.X - B2.X, L2.X - B2.X, L1, L2, out hit) && InBox(hit, B1, B2, 1))
              || (GetIntersection(L1.Y - B2.Y, L2.Y - B2.Y, L1, L2, out hit) && InBox(hit, B1, B2, 2))
              || (GetIntersection(L1.Z - B2.Z, L2.Z - B2.Z, L1, L2, out hit) && InBox(hit, B1, B2, 3)))
                return hit;

            return null;
        }

        public static Vector3D CheckFaceEdges(Extent extent, Vector3D a, Vector3D b, Vector3D c)
        {
            foreach (var line in extent.Edges)
            {
                var result = line.IntersectTriangle(a, b, c);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }


}
}
