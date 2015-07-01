using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using PyriteCliCommon.Models;

namespace PyriteCliCommon
{
    public static class StorageUtilities
    {
        public static string UploadBlob(CloudBlobClient client, string localPath, string remotePath, string containerName)
        {
            var container = client.GetContainerReference(containerName);
            var blob = container.GetBlockBlobReference(remotePath);
            blob.UploadFromFile(localPath, FileMode.Open);
            return (blob.Uri.ToString());
        }

        public static void DownloadBlob(CloudBlobClient client, string localPath, string remotePath)
        {
            var blob = client.GetBlobReferenceFromServer(new Uri(remotePath));
            blob.DownloadToFile(localPath, FileMode.CreateNew);
        }

        public static void InsertSetMetadata(CloudTableClient client, SetEntity set)
        {
            // Create the CloudTable object that represents the "people" table.
            CloudTable table = client.GetTableReference("sets");

            // Create the TableOperation that inserts the customer entity.
            TableOperation insertOperation = TableOperation.Insert(set);

            // Execute the insert operation.
            table.Execute(insertOperation);
        }

        public static void InsertWorkCompleteMetadata(CloudTableClient client, WorkEntity work)
        {
            // Create the CloudTable object that represents the "people" table.
            CloudTable table = client.GetTableReference("work");

            // Create the TableOperation that inserts the customer entity.
            TableOperation insertOperation = TableOperation.Insert(work);

            // Execute the insert operation.
            table.Execute(insertOperation);
        }
    }
}
