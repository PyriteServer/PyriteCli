using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PyriteLib;

namespace PyriteLib
{
    public static class SerializationUtilities
    {
        public static string EncodeMetadataToBase64(IDictionary<Vector3, int> cubeExists)
        {
            var keys = cubeExists.Keys.OrderBy(k => k.X).ThenBy(k => k.Y).ThenBy(k => k.Z).ToList();

            BitArray bits = new BitArray(keys.Count);

            for (int i = 0; i < keys.Count; i++)
            {
                bits[i] = cubeExists[keys[i]] > 0;
            }

            var bytes = new byte[(bits.Length + 7) / 8];
            bits.CopyTo(bytes, 0);

            return Convert.ToBase64String(bytes);
        }

        public static IDictionary<Vector3, bool> DecodeMetadataFromBase64(IEnumerable<Vector3> cubes, string base64)
        {
            var result = new Dictionary<Vector3, bool>();

            var bytes = Convert.FromBase64String(base64);
            var bits = new BitArray(bytes);

            var keys = cubes.OrderBy(k => k.X).ThenBy(k => k.Y).ThenBy(k => k.Z).ToList();

            for (int i = 0; i < keys.Count; i++)
            {
                result.Add(keys[i], bits[i]);
            }

            return result;
        }
    }
}
