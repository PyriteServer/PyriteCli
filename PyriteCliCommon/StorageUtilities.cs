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
using PyriteCliCommon.Contracts;

namespace PyriteCliCommon
{
    public static class StorageUtilities
    {
        private const string SetsTableName = "sets";
        private const string WorkTableName = "work";

        public static string UploadBlob(CloudBlobClient client, string localPath, string remotePath, string containerName)
        {
            var container = client.GetContainerReference(containerName);
            container.CreateIfNotExists(BlobContainerPublicAccessType.Off);
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

        public static void UpdateSetFailed(CloudTableClient client, string setRowKey, string failureMessage = null)
        {
            var setPartiionKey = SetEntity.DefaultPartitionKey;
            CloudTable table = client.GetTableReference(SetsTableName);

            var entry = new DynamicTableEntity(setPartiionKey, setRowKey, "*", new Dictionary<string, EntityProperty>());
            entry.Properties["Failed"] = new EntityProperty(true);
            if(!string.IsNullOrEmpty(failureMessage))
            {
                entry.Properties["FailureMessage"] = new EntityProperty(failureMessage);
            }
            var mergeOperation = TableOperation.Merge(entry);

            table.Execute(mergeOperation);
        }

        public static IEnumerable<SetInfo> GetRecentSets(CloudTableClient client, int count)
        {
            CloudTable setTable = client.GetTableReference(SetsTableName);
            TableQuery<SetEntity> setQuery = new TableQuery<SetEntity>().Where(
                TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, SetEntity.DefaultPartitionKey)
                ).Take(count);

            IEnumerable<SetEntity> sets = setTable.ExecuteQuery(setQuery);

            return sets.Select(set => CreateSetInfoFromEntity(client, set));
        }

        private static SetInfo CreateSetInfoFromEntity(CloudTableClient client, SetEntity set)
        {
            SetInfo setInfo = new SetInfo();

            setInfo.CompletedAt = set.CompletedOn;
            setInfo.CompletedWorkItems = new List<WorkItem>();
            setInfo.Container = set.ResultContainer;
            setInfo.ExpectedWorkItems = set.TextureTilesX * set.TextureTilesY;
            setInfo.Id = set.RowKey;
            setInfo.InProgressWorkItems = new List<WorkItem>();
            setInfo.Path = set.ResultPath;
            setInfo.QueuedAt = set.CreatedOn;
            setInfo.Status = "NotStarted";



            TableQuery<WorkEntity> workQuery = new TableQuery<WorkEntity>().Where(
                TableQuery.GenerateFilterCondition(
                    "PartitionKey",
                    QueryComparisons.Equal,
                    WorkEntity.EncodeResultPath(set.ResultPath, set.ResultContainer))
                );

            IEnumerable<WorkEntity> workEntities = client.GetTableReference(WorkTableName).ExecuteQuery(workQuery);

            foreach (var workEntity in workEntities)
            {
                WorkItem workItem = new WorkItem();
                workItem.X = workEntity.TextureTileX;
                workItem.Y = workEntity.TextureTileY;
                workItem.StartedAt = workEntity.StartTime;
                workItem.CompletedAt = workEntity.CompletedTime;

                if(workItem.CompletedAt.HasValue)
                {
                    setInfo.CompletedWorkItems.Add(workItem);
                } else
                {
                    setInfo.Status = "InProgress";
                    setInfo.InProgressWorkItems.Add(workItem);
                }
            }

            if (set.Completed)
            {
                setInfo.Status = "Completed";
            } else if(set.Failed)
            {
                setInfo.Status = "Failed";
            }

            return setInfo;
        }
    }
}
