using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PyriteLib
{
	public class SlicingOptions
	{
		public string OverrideMtl { get; set; }
		public bool GenerateEbo { get; set; }
		public bool GenerateObj { get; set; }
        public bool GenerateOpenCtm { get; set; }
		public bool AttemptResume { get; set; }
		public bool ForceCubicalCubes { get; set; }
		public bool Debug { get; set; }
		public string Texture { get; set; }
        public string Obj { get; set; }
        public int TextureSliceX { get; set; }
		public int TextureSliceY { get; set; }
		public float TextureScale { get; set; }
		public Texture TextureInstance { get; set; }
		public Dictionary<Vector2, RectangleTransform[]> UVTransforms { get; set; }
        public Vector3 CubeGrid { get; set; }

        // Support for cloud processing
        public string CloudObjPath { get; set; }
        public string CloudTexturePath { get; set; }
        public string CloudResultPath { get; set; }
        public string CloudResultContainer { get; set; }

    }
}
