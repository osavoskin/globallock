using SynchronizationUtils.GlobalLock.Configuration;
using SynchronizationUtils.GlobalLock.Utils;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Text;
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
        public Task<CloudBlobContainer> GetContainerReference(string container)
        {
            Ensure.IsNotNullOrWhiteSpace(container, nameof(container));
            var account = CloudStorageAccount.Parse(configuration.StorageConnectionString);
            return Task.FromResult(account.CreateCloudBlobClient().GetContainerReference(container));
        }

        /// <inheritdoc/>
        public Task<CloudTable> GetTableReference(string table)
        {
            Ensure.IsNotNullOrWhiteSpace(table, nameof(table));
            var account = CloudStorageAccount.Parse(configuration.StorageConnectionString);
            return Task.FromResult(account.CreateCloudTableClient().GetTableReference(table));
        }

        /// <inheritdoc/>
        public async Task<BlobLease> TryAcquireBlobLease(string resourceUID, CancellationToken token)
        {
            Ensure.IsNotNullOrWhiteSpace(resourceUID, nameof(resourceUID));
            token.ThrowIfCancellationRequested();

            try
            {
                var blob = await GetBlobReference(resourceUID, token);
                return new BlobLease(blob, leaseSeconds - 1, await TryAcquireBlobLease(blob, token));
            }
            catch (StorageException e) when (e.InnerException is TaskCanceledException)
            {
                throw new OperationCanceledException(null, e, token);
            }
        }

        /// <summary>
        /// Gets or creates a lock file (blob) for the given resource to lock on.
        /// </summary>
        /// <param name="resourceUID">The resource UID to create a lock file for.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A blob reference.</returns>
        private async Task<CloudBlockBlob> GetBlobReference(string resourceUID, CancellationToken token)
        {
            var container = await GetContainerReference(configuration.ContainerName);
            await container.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Off,
                null, null, token);

            var blob = container.GetBlockBlobReference(resourceUID);
            var exists = await blob.ExistsAsync(null, null, token);

            if (!exists) await blob.UploadTextAsync(string.Empty,
                Encoding.UTF8,
                AccessCondition.GenerateEmptyCondition(),
                null, null, token);

            return blob;
        }

        /// <summary>
        /// Tries to acquire an excusive lock on the given blob associated with some resource UID.
        /// </summary>
        /// <param name="blob">The blob we are trying to get exclusive access to.</param>
        /// <param name="token">The cancellation token to be used.</param>
        /// <returns>A newly acquired blob lease ID, otherwise - an empty string.</returns>
        private async Task<string> TryAcquireBlobLease(CloudBlockBlob blob, CancellationToken token)
        {
            try
            {
                return await blob.AcquireLeaseAsync(TimeSpan.FromSeconds(leaseSeconds), null, null, null, null, token);
            }
            catch (StorageException e) when (e.RequestInformation.ErrorCode == BlobErrorCodeStrings.LeaseAlreadyPresent)
            {
                return string.Empty;
            }
        }
    }
}
