using System;
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
			for (int x = 0; x < X; x++)
			{
				for (int y = 0; y < Y; y++)
				{
					for (int z = 0; z < Z; z++)
					{
						Action(x, y, z);
					}
				}
			}
		}

		public static void EnumerateSpace(XyzPoint size, Action<int, int, int> Action)
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
			for (int x = 0; x < X; x++)
			{
				for (int y = 0; y < Y; y++)
				{
					Action(x, y);
				}
			}
		}

        public static void EnumerateSpaceParallel(int X, int Y, Action<int, int> Action)
        {
            Parallel.For(0, X, (x) =>
            {
                Parallel.For(0, Y, (y) =>
                {
                    Action(x, y);
                });
            });
        }


    }
}
