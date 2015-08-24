using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using PyriteCliCommon;
using PyriteCliCommon.Contracts;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace PyriteCloudAdmin.Controllers
{
    public class SetsController : ApiController
    {
        CloudTableClient TableClient { get; }
        public object CloudStorageClient { get; private set; }

        public SetsController()
        {
            CloudStorageAccount storageClient = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);

            TableClient = storageClient.CreateCloudTableClient();
        }

        public IEnumerable<SetInfo> Get()
        {
            return StorageUtilities.GetRecentSets(TableClient, 10);
        }
    }
}
