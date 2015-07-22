using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace PyriteLib.Tests
{
    [TestClass()]
    public class SerializationUtilitiesTests
    {
        [TestMethod()]
        public void EncodeDecodeTest()
        {
            var data = GenerateTestMetadata(61);
            string encoded = SerializationUtilities.EncodeMetadataToBase64(data);

            Assert.IsFalse(string.IsNullOrEmpty(encoded));

            var decoded = SerializationUtilities.DecodeMetadataFromBase64(data.Keys, encoded);

            Assert.AreEqual(decoded.Count, data.Count);

            foreach(var key in data.Keys)
            {
                Assert.AreEqual(data[key] > 0, decoded[key]);
            }
        }

        private Dictionary<Vector3, int> GenerateTestMetadata(int length)
        {
            var result = new Dictionary<Vector3, int>();           

            for (int i = 0; i < length; i++)
            {
                result.Add(new Vector3(i, i, i), i % 2);
            }

            return result;
        }
    }
}