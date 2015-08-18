using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using PyriteCliCommon.Models;

namespace PyriteCliCommon
{
    public static class StorageUtilities
    {
        private const string SetsTableName = "sets";
        private const string WorkTableName = "work";

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

        public static async Task DownloadBlobAsync(CloudBlobClient client, string localPath, string remotePath, CancellationToken cancellationToken)
        {
            var blob = await client.GetBlobReferenceFromServerAsync(new Uri(remotePath), cancellationToken);
            await blob.DownloadToFileAsync(localPath, FileMode.CreateNew, cancellationToken);
        }

        public static void InsertSetMetadata(CloudTableClient client, SetEntity set)
        {
            // Create the CloudTable object that represents the "sets" table.
            CloudTable table = client.GetTableReference(SetsTableName);
            table.CreateIfNotExists();

            // Create the TableOperation that inserts the set entity.
            TableOperation insertOperation = TableOperation.Insert(set);

            // Execute the insert operation.
            table.Execute(insertOperation);
        }

        public static void InsertWorkStartedMetadata(CloudTableClient client, WorkEntity work)
        {
            // Create the CloudTable object that represents the "work" table.
            CloudTable table = client.GetTableReference(WorkTableName);
            table.CreateIfNotExists();

            // Create the TableOperation that inserts the work entity.
            TableOperation insertOperation = TableOperation.Insert(work);

            // Execute the insert operation.
            TableResult result = table.Execute(insertOperation);
        }

        public static void UpdateWorkCompletedMetadata(CloudTableClient client, WorkEntity work)
        {
            // Create the CloudTable object that represents the "work" table.
            CloudTable table = client.GetTableReference(WorkTableName);
            table.CreateIfNotExists();

            // Create the TableOperation that inserts the work entity.
            TableOperation insertOperation = TableOperation.Merge(work);

            // Execute the insert operation.
            table.Execute(insertOperation);
        }

        public static int GetWorkCompletedCount(CloudTableClient client, string resultPath, string container)
        {
            var workTable = client.GetTableReference(WorkTableName);
            TableQuery<WorkEntity> workItemQuery = new TableQuery<WorkEntity>().Where(
                TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, WorkEntity.EncodeResultPath(resultPath, container)),
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition("CompletedTime", QueryComparisons.NotEqual, ""))).Select(new[] { "RowKey" });

            var completedItems = workTable.ExecuteQuery(workItemQuery);
            return completedItems.ToList().Count;
        }

        public static IEnumerable<WorkEntity> GetWorkCompletedMetadata(CloudTableClient client, string resultPath, string container)
        {
            var workTable = client.GetTableReference(WorkTableName);
            TableQuery<WorkEntity> workItemQuery = new TableQuery<WorkEntity>().Where(
                TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, WorkEntity.EncodeResultPath(resultPath, container)),
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition("CompletedTime", QueryComparisons.NotEqual, "")));

            return workTable.ExecuteQuery(workItemQuery);
        }

        public static void UpdateSetCompleted(CloudTableClient client, string setRowKey)
        {
            var setPartiionKey = SetEntity.DefaultPartitionKey;
            CloudTable table = client.GetTableReference(SetsTableName);

            var entry = new DynamicTableEntity(setPartiionKey, setRowKey, "*", new Dictionary<string, EntityProperty>());
            entry.Properties["CompletedOn"] = new EntityProperty(DateTime.UtcNow);
            entry.Properties["Completed"] = new EntityProperty(true);
            var mergeOperation = TableOperation.Merge(entry);

            table.Execute(mergeOperation);
        }
    }
}
