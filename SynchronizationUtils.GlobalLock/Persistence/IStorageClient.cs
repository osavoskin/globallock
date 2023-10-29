using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
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
        /// <returns>A reference to a blob container.</returns>
        Task<CloudBlobContainer> GetContainerReference(string container);

        /// <summary>
        /// Gets a reference to a storage table.
        /// </summary>
        /// <param name="table">A table name.</param>
        /// <returns>A reference to a storage table.</returns>
        Task<CloudTable> GetTableReference(string table);

        /// <summary>
        /// Tries to acquire a cloud native lock associated with the given resource UID.
        /// </summary>
        /// <param name="resourceUID">The resource UID to lock on.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A native lock instance for the given resource UID.</returns>
        Task<BlobLease> TryAcquireBlobLease(string resourceUID, CancellationToken token);
    }
}
