using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace PyriteCliCommon.Models
{
    public class WorkEntity : TableEntity
    {
        public WorkEntity(string ResultPath, string container, int tileX, int tileY)
        {
            this.PartitionKey = EncodeResultPath(ResultPath, container);
            this.RowKey = string.Format("{0}_{1}", tileX, tileY);
            this.TextureTileX = tileX;
            this.TextureTileY = tileY;
        }

        public WorkEntity() { }
        public DateTime StartTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public int TextureTileX { get; set; }
        public int TextureTileY { get; set; }
        public string MetadataBase64 { get; set; }         
        
        public static string EncodeResultPath(string path, string container)
        {
            return container + "_" + path.Replace('/', '_').Replace('\\', '_');
        }
    }
}
