using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using clipr;

namespace PyriteCliCommon
{
    [ApplicationInfo(Description = "PyriteCli Options")]
    public class Options
    {
        [NamedArgument('x', "xsize", Action = ParseAction.Store,
            Description = "The number of times to subdivide in the X dimension.  Default 10.")]
        public int XSize { get; set; }

        [NamedArgument('y', "ysize", Action = ParseAction.Store,
            Description = "The number of times to subdivide in the Y dimension.  Default 10.")]
        public int YSize { get; set; }

        [NamedArgument('z', "zsize", Action = ParseAction.Store,
            Description = "The number of times to subdivide in the Z dimension.  Default 10.")]
        public int ZSize { get; set; }

        [NamedArgument('u', "texturex", Action = ParseAction.Store,
            Description = "The number of times to subdivide texture in the X dimension. Default 4.")]
        public int TextureXSize { get; set; }

        [NamedArgument('v', "texturey", Action = ParseAction.Store,
            Description = "The number of times to subdivide texture in the Y dimension. Default 4.")]
        public int TextureYSize { get; set; }

        [NamedArgument('s', "scaletexture", Action = ParseAction.Store,
            Description = "A number between 0 and 1 telling PyriteCli how to resize/scale the texture when using -t.  Default 1.")]
        public float ScaleTexture { get; set; }

        [NamedArgument('m', "mtl", Action = ParseAction.Store,
            Description = "Override the MTL field in output obj files. e.g. -z model.mtl")]
        public string MtlOverride { get; set; }

        [NamedArgument('t', "texture", Action = ParseAction.Store,
            Description = "Include a texture to partition during cube slicing. Will rewrite UV's in output files. Requires -tx -ty parameters.")]
        public string Texture { get; set; }

        [NamedArgument('o', "obj", Action = ParseAction.StoreTrue,
            Description = "Generate OBJ files designed for use with CubeServer")]
        public bool Obj { get; set; }

        [NamedArgument("writemtl", Action = ParseAction.StoreTrue,
            Description = "Writes one MTL file per texture and updates OBJ reference")]
        public bool WriteMtl { get; set; }

        [NamedArgument('e', "ebo", Action = ParseAction.StoreTrue,
            Description = "Generate EBO files designed for use with CubeServer")]
        public bool Ebo { get; set; }

        [NamedArgument('p', "openctm", Action = ParseAction.StoreTrue,
            Description = "Generate OpenCtm files designed for use with CubeServer")]
        public bool OpenCtm { get; set; }

        [NamedArgument('a', "markupUV", Action = ParseAction.StoreTrue,
            Description = "Draws UVW's on a texture")]
        public bool MarkupUV { get; set; }

        [NamedArgument('c', "forcecubical", Action = ParseAction.StoreTrue,
            Description = "X Y Z grid dimensions will be equal, and world space will be grown to fill a containing cube.")]
        public bool ForceCubical { get; set; }

        [NamedArgument('d', "debug", Action = ParseAction.StoreTrue,
            Description = "Generate various additional debug data during error states")]
        public bool Debug { get; set; }

        [PositionalArgument(0, MetaVar = "OUT",
            Description = "Output folder")]
        public string OutputPath { get; set; }

        [PositionalArgument(1, MetaVar = "IN",
            NumArgs = 1,
            Constraint = NumArgsConstraint.AtLeast,
            Description = "A list of .obj files to process")]
        public List<string> Input { get; set; }

        public Options()
        {
            XSize = 2;
            YSize = 2;
            ZSize = 2;
            ScaleTexture = 1;
            TextureXSize = 4;
            TextureYSize = 4;
            ForceCubical = false;
            Debug = false;
        }
    }

}
