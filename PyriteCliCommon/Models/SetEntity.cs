using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace PyriteCliCommon.Models
{
    public class SetEntity : TableEntity
    {
        public static readonly string DefaultPartitionKey = "PROCESSINGSET";

        public SetEntity(string setName, DateTime queueTime)
        {
            this.PartitionKey = DefaultPartitionKey;
            this.RowKey = string.Format("{0}_{1}", queueTime.ToUniversalTime().Ticks, setName);
            this.CreatedOn = queueTime;
        }

        public SetEntity() { }

        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public int TextureTilesX { get; set; }
        public int TextureTilesY { get; set; }
        public string ResultPath { get; set; }
        public DateTime? StartedOn { get; set; }
        public bool Completed { get; set; }
        public DateTime? CompletedOn { get; set; }
    }
}
