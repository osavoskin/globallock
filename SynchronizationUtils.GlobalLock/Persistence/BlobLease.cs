using SynchronizationUtils.GlobalLock.Utils;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SynchronizationUtils.GlobalLock.Persistence
{
    /// <summary>
    /// Represents a native lock on a resource UID.
    /// </summary>
    internal class BlobLease : IAsyncDisposable
    {
        private readonly CloudBlockBlob blob;
        private readonly string leaseId;
        private bool isReleased;

        /// <summary>
        /// The event being raised on lease expiration.
        /// </summary>
        public event Action Expired;

        /// <summary>
        /// Gets a value indicating whether the lease has been acquired.
        /// </summary>
        public bool IsAcquired => !string.IsNullOrWhiteSpace(leaseId) && !isReleased;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobLease"/> class.
        /// </summary>
        /// <param name="blob">A blob reference.</param>
        /// <param name="timeout">The lease expiration timeout in seconds.</param>
        /// <param name="leaseId">The lease ID.</param>
        public BlobLease(CloudBlockBlob blob, double timeout, string leaseId)
        {
            this.blob = Ensure.IsNotNull(blob, nameof(blob));
            this.leaseId = leaseId;
            RunTimer(Ensure.IsGreaterThan(timeout, 0, nameof(timeout)));
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => Release(default);

        /// <summary>
        /// Releases the native lock if it's been previously acquired.
        /// </summary>
        public async ValueTask Release(CancellationToken token)
        {
            if (!IsAcquired) return;

            try
            {
                var condition = new AccessCondition { LeaseId = leaseId };
                await blob.ReleaseLeaseAsync(condition, null, null, token);
            }
            catch (StorageException e) when (e.InnerException is TaskCanceledException)
            {
                throw new OperationCanceledException(null, e, token);
            }

            isReleased = true;
        }

        /// <summary>
        /// Raises the <see cref="Expired"/> event on lease expiration.
        /// </summary>
        /// <param name="timeout">The lease expiration timeout in seconds.</param>
        private void RunTimer(double timeout)
        {
            if (IsAcquired) Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(timeout));

                if (IsAcquired)
                {
                    isReleased = true;
                    Expired?.Invoke();
                }
            });
        }
    }
}
