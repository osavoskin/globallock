using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using SynchronizationUtils.GlobalLock.Configuration;
using SynchronizationUtils.GlobalLock.Utils;
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
            var predicate = $"{nameof(Record.Resource)} eq '{resource}'";

            try
            {
                var table = await GetOrCreateTable(token);
                var record = await GetOngoingOperationRecord(table, predicate, partitionKey, token);
                return record is null;
            }
            catch (RequestFailedException e) when (e.InnerException is TaskCanceledException)
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
            var predicate = $"{nameof(Record.RowKey)} eq '{rowKey}'";

            try
            {
                var table = await GetOrCreateTable(token);
                var record = await GetOngoingOperationRecord(table, predicate, partitionKey, token);
                if (record is null) return false;

                record.ExpiresAt += period;
                var response = await table.UpdateEntityAsync(record, record.ETag, TableUpdateMode.Merge, token);
                return response.Status == 204;
            }
            catch (RequestFailedException e) when (e.InnerException is TaskCanceledException)
            {
                throw new OperationCanceledException(null, e, token);
            }
            catch (RequestFailedException e) when (e.Status == 412)
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

                var table = await GetOrCreateTable(localTokenSource.Token);
                await table.AddEntityAsync(entity, localTokenSource.Token);
                return entity;
            }
            catch (RequestFailedException e) when (e.InnerException is TaskCanceledException)
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
            var predicate = $"{nameof(Record.RowKey)} eq '{rowKey}'";

            try
            {
                var table = await GetOrCreateTable(token);
                var record = await GetOngoingOperationRecord(table, predicate, partitionKey, token);
                if (record is null) return;

                record.CompletedAt = DateTime.UtcNow;
                await table.UpdateEntityAsync(record, record.ETag, TableUpdateMode.Merge, token);
            }
            catch (RequestFailedException e) when (e.InnerException is TaskCanceledException)
            {
                throw new OperationCanceledException(null, e, token);
            }
            catch (RequestFailedException e) when (e.Status == 412)
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
            TableClient table,
            string predicate,
            string partitionKey,
            CancellationToken token)
        {
            Ensure.IsNotNull(table, nameof(table));
            Ensure.IsNotNullOrWhiteSpace(predicate, nameof(predicate));
            Ensure.IsNotNullOrWhiteSpace(partitionKey, nameof(partitionKey));

            var partitionKeyEquals = $"{nameof(Record.PartitionKey)} eq '{partitionKey}'";
            var recordExists = $"({predicate}) and ({partitionKeyEquals})";
            var notCompleted = $"{nameof(Record.CompletedAt)} eq datetime'{dateTimeMin:yyyy-MM-ddTHH:mm:ss.fffZ}'";
            var notExpired = $"{nameof(Record.ExpiresAt)} gt datetime'{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}'";
            var operationInProgress = $"({notCompleted}) and ({notExpired})";
            var filter = $"({recordExists}) and ({operationInProgress})";

            var results = new List<Record>();
            var records = table.QueryAsync<Record>(filter, maxPerPage: 2, cancellationToken: token);

            await foreach (var record in records)
            {
                results.Add(record);
                if (results.Count > 1) break;
            }

            return results.SingleOrDefault();
        }

        /// <summary>
        /// Gets the table containing the synchronous operations log.
        /// </summary>
        /// <param name="token">A cancellation token.</param>
        /// <returns>An instance of the <see cref="TableClient"/> class.</returns>
        private async Task<TableClient> GetOrCreateTable(CancellationToken token)
        {
            var table = await storageClient.GetTableClient(configuration.TableName);
            await table.CreateIfNotExistsAsync(token);
            return table;
        }
    }
}
