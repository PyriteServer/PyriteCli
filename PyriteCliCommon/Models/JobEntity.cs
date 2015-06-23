using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace PyriteCliCommon.Models
{
    public class JobEntity : TableEntity
    {
        public JobEntity(string MeshName, string jobID)
        : base(MeshName, jobID) { }

        public JobEntity() { }

        public string ProductName { get; set; }
        public string Description { get; set; }
    }
}
