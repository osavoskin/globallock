using SynchronizationUtils.GlobalLock.Configuration;
using SynchronizationUtils.GlobalLock.Utils;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SynchronizationUtils.GlobalLock.Persistence
{
    /// <inheritdoc cref="IRepository"/>
    internal class Repository : IRepository
    {
        private readonly DateTime dateTimeMin;
        private readonly IStorageClient storageClient;
        private readonly GlobalLockConfiguration configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="Repository"/> class.
        /// </summary>
        /// <param name="storageClient">The Azure storage client.</param>
        /// <param name="configuration">The global lock configuration.</param>
        public Repository(IStorageClient storageClient, IOptions<GlobalLockConfiguration> configuration)
        {
            this.storageClient = Ensure.IsNotNull(storageClient, nameof(storageClient));
            this.configuration = Ensure.IsNotNull(configuration?.Value, nameof(configuration));
            dateTimeMin = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        /// <inheritdoc/>
        public async Task<bool> IsResourceAvailable(string resource, string scope, CancellationToken token)
        {
            Ensure.IsNotNullOrWhiteSpace(resource, nameof(resource));
            Ensure.IsNotNullOrWhiteSpace(scope, nameof(scope));
            token.ThrowIfCancellationRequested();

            var partitionKey = new RecordId(scope).PartitionKey;
            var predicate = TableQuery.GenerateFilterCondition(nameof(Record.Resource),
                QueryComparisons.Equal,
                resource);

            try
            {
                var table = await GetOrCreateTable(token);
                var record = await GetOngoingOperationRecord(table, predicate, partitionKey, token);
                return record is null;
            }
            catch (StorageException e) when (e.InnerException is TaskCanceledException)
            {
                throw new OperationCanceledException(null, e, token);
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ProlongSynchronousOperation(RecordId recordId, TimeSpan period, CancellationToken token)
        {
            Ensure.IsNotNull(recordId, nameof(recordId));
            Ensure.IsGreaterThan(period, TimeSpan.Zero, nameof(period));
            token.ThrowIfCancellationRequested();

            var rowKey = recordId.RowKey;
            var partitionKey = recordId.PartitionKey;
            var predicate = TableQuery.GenerateFilterCondition(nameof(Record.RowKey),
                QueryComparisons.Equal,
                rowKey);

            try
            {
                var table = await GetOrCreateTable(token);
                var record = await GetOngoingOperationRecord(table, predicate, partitionKey, token);
                if (record is null) return false;

                var props = new Dictionary<string, EntityProperty>
                {
                    [nameof(Record.ExpiresAt)] = new EntityProperty(record.ExpiresAt + period)
                };

                var entity = new DynamicTableEntity(partitionKey, rowKey, record.ETag, props);
                var updateOp = TableOperation.Merge(entity);

                var response = await table.ExecuteAsync(updateOp, null, null, token);
                return response.HttpStatusCode == 204;
            }
            catch (StorageException e) when (e.InnerException is TaskCanceledException)
            {
                throw new OperationCanceledException(null, e, token);
            }
            catch (StorageException e) when (e.RequestInformation.HttpStatusCode == 412)
            {
                return await ProlongSynchronousOperation(recordId, period, token);
            }
        }

        /// <inheritdoc/>
        public async Task<Record> BeginSynchronousOperation(
            string resource,
            string scope,
            TimeSpan expiration,
            CancellationToken token)
        {
            Ensure.IsNotNullOrWhiteSpace(resource, nameof(resource));
            Ensure.IsNotNullOrWhiteSpace(scope, nameof(scope));
            Ensure.IsGreaterThan(expiration, TimeSpan.Zero, nameof(expiration));
            token.ThrowIfCancellationRequested();

            await using var nativeLease = await storageClient.TryAcquireBlobLease(
                StringUtils.GetResourceUID(resource, scope),
                token);

            using var leaseExpiredTokenSource = new CancellationTokenSource();
            nativeLease.Expired += leaseExpiredTokenSource.Cancel;

            using var localTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                leaseExpiredTokenSource.Token,
                token);

            try
            {
                var shouldContinue = !localTokenSource.Token.IsCancellationRequested
                    && nativeLease.IsAcquired
                    && await IsResourceAvailable(resource, scope, localTokenSource.Token);

                if (!shouldContinue)
                    return null;

                var entity = new Record(new RecordId(scope))
                {
                    Scope = scope,
                    Resource = resource,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.Add(expiration),
                    CompletedAt = dateTimeMin
                };

                var insertOp = TableOperation.Insert(entity);
                var table = await GetOrCreateTable(localTokenSource.Token);
                await table.ExecuteAsync(insertOp, null, null, localTokenSource.Token);

                return entity;
            }
            catch (StorageException e) when (e.InnerException is TaskCanceledException)
            {
                throw new OperationCanceledException(null, e, localTokenSource.Token);
            }
            finally
            {
                await nativeLease.Release(token);
            }
        }

        /// <inheritdoc/>
        public async Task EndSynchronousOperation(RecordId recordId, CancellationToken token)
        {
            Ensure.IsNotNull(recordId, nameof(recordId));
            token.ThrowIfCancellationRequested();

            var rowKey = recordId.RowKey;
            var partitionKey = recordId.PartitionKey;
            var predicate = TableQuery.GenerateFilterCondition(nameof(Record.RowKey),
                QueryComparisons.Equal,
                rowKey);

            try
            {
                var table = await GetOrCreateTable(token);
                var record = await GetOngoingOperationRecord(table, predicate, partitionKey, token);
                if (record is null) return;

                var props = new Dictionary<string, EntityProperty>
                {
                    [nameof(Record.CompletedAt)] = new EntityProperty(DateTime.UtcNow)
                };

                var entity = new DynamicTableEntity(partitionKey, rowKey, record.ETag, props);
                await table.ExecuteAsync(TableOperation.Merge(entity), null, null, token);
            }
            catch (StorageException e) when (e.InnerException is TaskCanceledException)
            {
                throw new OperationCanceledException(null, e, token);
            }
            catch (StorageException e) when (e.RequestInformation.HttpStatusCode == 412)
            {
                await EndSynchronousOperation(recordId, token);
            }
        }

        /// <summary>
        /// Gets an ongoing operation record using a predicate and a partition key.
        /// </summary>
        /// <param name="table">The storage table to query.</param>
        /// <param name="predicate">The predicate to identify the row.</param>
        /// <param name="partitionKey">The record partition key.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>An ongoing operation record or null.</returns>
        private async Task<Record> GetOngoingOperationRecord(
            CloudTable table,
            string predicate,
            string partitionKey,
            CancellationToken token)
        {
            Ensure.IsNotNull(table, nameof(table));
            Ensure.IsNotNullOrWhiteSpace(predicate, nameof(predicate));
            Ensure.IsNotNullOrWhiteSpace(partitionKey, nameof(partitionKey));

            var partitionKeyEquals = TableQuery.GenerateFilterCondition(
                nameof(Record.PartitionKey),
                QueryComparisons.Equal,
                partitionKey);

            var recordExists = TableQuery.CombineFilters(
                predicate,
                TableOperators.And,
                partitionKeyEquals);

            var notCompleted = TableQuery.GenerateFilterConditionForDate(
                nameof(Record.CompletedAt),
                QueryComparisons.Equal,
                dateTimeMin);

            var notExpired = TableQuery.GenerateFilterConditionForDate(
                nameof(Record.ExpiresAt),
                QueryComparisons.GreaterThan,
                DateTime.UtcNow);

            var operationInProgress = TableQuery.CombineFilters(
                notCompleted,
                TableOperators.And,
                notExpired);

            var filter = TableQuery.CombineFilters(
                recordExists,
                TableOperators.And,
                operationInProgress);

            var query = new TableQuery<Record>()
                .Where(filter)
                .Take(2);

            var response = await table.ExecuteQuerySegmentedAsync(
                query, null, null, null, token
            );

            return response.Results.SingleOrDefault();
        }

        /// <summary>
        /// Gets the table containing the synchronous operations log.
        /// </summary>
        /// <param name="token">A cancellation token.</param>
        /// <returns>An instance of the <see cref="CloudTable"/> class.</returns>
        private async Task<CloudTable> GetOrCreateTable(CancellationToken token)
        {
            var table = await storageClient.GetTableReference(configuration.TableName);
            await table.CreateIfNotExistsAsync(null, null, token);
            return table;
        }
    }
}
