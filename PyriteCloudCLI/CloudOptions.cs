using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using clipr;
using PyriteCliCommon;

namespace PyriteCloudCLI
{
    class CloudOptions : Options
    {
        [NamedArgument("container", Action = ParseAction.Store,
            Description = "The name of the Azure Blob Storage container to write output files")]
        public string OutputContainer { get; set; }
    }
}
