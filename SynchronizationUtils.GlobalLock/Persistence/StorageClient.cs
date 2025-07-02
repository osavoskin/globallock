using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Options;
using SynchronizationUtils.GlobalLock.Configuration;
using SynchronizationUtils.GlobalLock.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SynchronizationUtils.GlobalLock.Persistence
{
    /// <inheritdoc cref="IStorageClient"/>
    internal class StorageClient : IStorageClient
    {
        private readonly double leaseSeconds = 30;
        private readonly GlobalLockConfiguration configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageClient"/> class.
        /// </summary>
        /// <param name="configuration">The global lock configuration.</param>
        public StorageClient(IOptions<GlobalLockConfiguration> configuration)
        {
            this.configuration = Ensure.IsNotNull(configuration?.Value, nameof(configuration));
        }

        /// <inheritdoc/>
        public Task<BlobContainerClient> GetContainerClient(string container)
        {
            Ensure.IsNotNullOrWhiteSpace(container, nameof(container));
            var blobServiceClient = new BlobServiceClient(configuration.StorageConnectionString);
            return Task.FromResult(blobServiceClient.GetBlobContainerClient(container));
        }

        /// <inheritdoc/>
        public Task<TableClient> GetTableClient(string table)
        {
            Ensure.IsNotNullOrWhiteSpace(table, nameof(table));
            var tableServiceClient = new TableServiceClient(configuration.StorageConnectionString);
            return Task.FromResult(tableServiceClient.GetTableClient(table));
        }

        /// <inheritdoc/>
        public async Task<BlobLease> TryAcquireBlobLease(string resourceUID, CancellationToken token)
        {
            Ensure.IsNotNullOrWhiteSpace(resourceUID, nameof(resourceUID));
            token.ThrowIfCancellationRequested();

            try
            {
                var blobClient = await GetBlobReference(resourceUID, token);
                var leaseClient = blobClient.GetBlobLeaseClient();
                return new BlobLease(leaseClient, leaseSeconds - 1, await TryAcquireBlobLease(leaseClient, token));
            }
            catch (RequestFailedException e) when (e.InnerException is TaskCanceledException)
            {
                throw new OperationCanceledException(null, e, token);
            }
        }

        /// <summary>
        /// Gets or creates a lock file (blob) for the given resource to lock on.
        /// </summary>
        /// <param name="resourceUID">The resource UID to create a lock file for.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A blob client.</returns>
        private async Task<BlobClient> GetBlobReference(string resourceUID, CancellationToken token)
        {
            var container = await GetContainerClient(configuration.ContainerName);
            await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: token);

            var blob = container.GetBlobClient(resourceUID);
            var exists = await blob.ExistsAsync(token);

            if (!exists.Value)
                await blob.UploadAsync(new BinaryData(string.Empty), false, token);

            return blob;
        }

        /// <summary>
        /// Tries to acquire an exclusive lock on the given blob associated with some resource UID.
        /// </summary>
        /// <param name="leaseClient">The blob lease client we are trying to get exclusive access to.</param>
        /// <param name="token">The cancellation token to be used.</param>
        /// <returns>A newly acquired blob lease ID, otherwise - an empty string.</returns>
        private async Task<string> TryAcquireBlobLease(BlobLeaseClient leaseClient, CancellationToken token)
        {
            try
            {
                var duration = TimeSpan.FromSeconds(leaseSeconds);
                var response = await leaseClient.AcquireAsync(duration, cancellationToken: token);
                return response.Value.LeaseId;
            }
            catch (RequestFailedException e) when (e.ErrorCode == BlobErrorCode.LeaseAlreadyPresent)
            {
                return string.Empty;
            }
        }
    }
}
