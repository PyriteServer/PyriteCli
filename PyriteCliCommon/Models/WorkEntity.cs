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
        public WorkEntity(string ResultPath, int tileX, int tileY, DateTime completedTime)
        {
            this.PartitionKey = EncodeResultPath(ResultPath);
            this.RowKey = string.Format("{0}_{1}", tileX, tileY);
            this.CompletedTime = completedTime;
            this.TextureTileX = tileX;
            this.TextureTileY = tileY;
        }

        public WorkEntity() { }

        public DateTime CompletedTime { get; set; }
        public int TextureTileX { get; set; }
        public int TextureTileY { get; set; }
        public string MetadataBase64 { get; set; }         
        
        public static string EncodeResultPath(string path)
        {
            return path.Replace('/', '_').Replace('\\', '_');
        }
    }
}
