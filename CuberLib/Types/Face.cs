using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CuberLib.Types
{
    public class Face : IType
    {
        public const int MinimumDataLength = 4;
        public const string Prefix = "f";

        public int[] VertexIndexList { get; set; }
        public int[] TextureVertexIndexList { get; set; }     
        
        private int[] originalVertexIndexList;
        private int[] originalTextureVertexIndexList;

        public void LoadFromStringArray(string[] data)
        {
            if (data.Length < MinimumDataLength)
                throw new ArgumentException("Input array must be of minimum length " + MinimumDataLength, "data");

            if (!data[0].ToLower().Equals(Prefix))
                throw new ArgumentException("Data prefix must be '" + Prefix + "'", "data");            

            int vcount = data.Count() - 1;
            VertexIndexList = new int[vcount];
            TextureVertexIndexList = new int[vcount];
			originalVertexIndexList = new int[vcount];
			originalTextureVertexIndexList = new int[vcount];

			bool success;

            for (int i = 0; i < vcount; i++)
            {
                string[] parts = data[i + 1].Split('/');

                int vindex;
                success = int.TryParse(parts[0], out vindex);
                if (!success) throw new ArgumentException("Could not parse parameter as int");
                VertexIndexList[i] = vindex;

                if (parts.Count() > 1)
                {
                    success = int.TryParse(parts[1], out vindex);
                    if (!success) throw new ArgumentException("Could not parse parameter as int");
                    TextureVertexIndexList[i] = vindex;
                }
            }

			VertexIndexList.CopyTo(originalVertexIndexList,0);
			TextureVertexIndexList.CopyTo(originalTextureVertexIndexList, 0);

        }

        public bool InExtent(Extent extent, List<Vertex> vertexList)
        {
            foreach(int index in VertexIndexList)
            {
                Vertex v = vertexList[index-1];
                if (v.InExtent(extent)) return true;
            }

            return false;
        }

        public void UpdateVertexIndex(int oldIndex, int newIndex)
        {           
            for(int index = 0; index < VertexIndexList.Count(); index++)
            {
                if (originalVertexIndexList[index] == oldIndex)
                {
                    VertexIndexList[index] = newIndex;
                    return;
                }
            }
        }

        public void UpdateTextureVertexIndex(int oldIndex, int newIndex)
        {
            for (int index = 0; index < TextureVertexIndexList.Count(); index++)
            {
                if (originalTextureVertexIndexList[index] == oldIndex)
                {
                    TextureVertexIndexList[index] = newIndex;
                    return;
                }
            }
        }

		public void RevertVertices()
		{
			originalVertexIndexList.CopyTo(VertexIndexList, 0);
			originalTextureVertexIndexList.CopyTo(TextureVertexIndexList, 0);
		}

        // HACKHACK this will write invalid files if there are no texture vertices in
        // the faces, since we don't read that in properly yet
        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.Append("f");

            for(int i = 0; i < VertexIndexList.Count(); i++)
            {
                b.AppendFormat(" {0}/{1}", VertexIndexList[i], TextureVertexIndexList[i]);                
            }

            return b.ToString();
        }

        public string ToString(List<TextureVertex> textureList, List<Vertex> vertexList)
        {
            StringBuilder b = new StringBuilder();
            b.Append("f");

            for (int i = 0; i < VertexIndexList.Count(); i++)
            {
                b.AppendFormat(" {0}/{1}", vertexList[VertexIndexList[i]], textureList[TextureVertexIndexList[i]]);
            }

            return b.ToString();
        }
    }
}
