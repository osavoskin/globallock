using System;
using System.Threading;
using System.Threading.Tasks;

namespace SynchronizationUtils.GlobalLock
{
    /// <summary>
    /// Global lock client to synchronously manage distributed operations.
    /// </summary>
    public interface IGlobalLock : IDisposable
    {
        /// <summary>
        /// Tries to acquire a lease on the given resource.
        /// </summary>
        /// <param name="resource">A resource name.</param>
        /// <param name="scope">A scope.</param>
        /// <param name="expiration">A TTL of the lease.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A newly created lease instance.</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OperationCanceledException"></exception>
        Task<ILease> TryAcquire(
            string resource,
            string scope = "default",
            TimeSpan? expiration = null,
            CancellationToken token = default);

        /// <summary>
        /// Tries to extend the lease by the given time period.
        /// </summary>
        /// <param name="leaseId">An active lease ID.</param>
        /// <param name="period">The time to extend the lease by.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>True on success and False otherwise.</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OperationCanceledException"></exception>
        Task<bool> TryExtend(
            string leaseId,
            TimeSpan? period = null,
            CancellationToken token = default);

        /// <summary>
        /// Releases the lease associated with the given ID.
        /// </summary>
        /// <param name="leaseId">An active lease ID.</param>
        /// <param name="token">A cancellation token.</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="OperationCanceledException"></exception>
        Task Release(string leaseId, CancellationToken token = default);
    }
}
