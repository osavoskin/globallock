using Azure.Data.Tables;
using Azure.Storage.Blobs;
using System.Threading;
using System.Threading.Tasks;

namespace SynchronizationUtils.GlobalLock.Persistence
{
    /// <summary>
    /// The Azure Storage Client wrapper.
    /// </summary>
    internal interface IStorageClient
    {
        /// <summary>
        /// Gets a reference to a blob container.
        /// </summary>
        /// <param name="container">A container name.</param>
        /// <returns>A reference to a blob container client.</returns>
        Task<BlobContainerClient> GetContainerClient(string container);

        /// <summary>
        /// Gets a reference to a table client.
        /// </summary>
        /// <param name="table">A table name.</param>
        /// <returns>A reference to a table client.</returns>
        Task<TableClient> GetTableClient(string table);

        /// <summary>
        /// Attempts to acquire a lease on a blob identified by the resource UID.
        /// </summary>
        /// <param name="resourceUID">A unique identifier for the resource to be leased.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="BlobLease"/> object representing the requested lease.</returns>
        Task<BlobLease> TryAcquireBlobLease(string resourceUID, CancellationToken token);
    }
}
