using System;
using System.Threading;
using System.Threading.Tasks;

namespace SynchronizationUtils.GlobalLock.Persistence
{
    /// <summary>
    /// Contains methods to access and manage the persistent store of <see cref="GlobalLock"/>.
    /// </summary>
    internal interface IRepository
    {
        /// <summary>
        /// Checks if an exclusive lock on the resource can be acquired.
        /// </summary>
        /// <param name="resource">The resource to lock on.</param>
        /// <param name="scope">The scope.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>True if the resource is free, False otherwise.</returns>
        Task<bool> IsResourceAvailable(
            string resource,
            string scope,
            CancellationToken token);

        /// <summary>
        /// Tries to extend the exclusive lock on a resource.
        /// </summary>
        /// <param name="recordId">The record ID of the lock to extend.</param>
        /// <param name="period">The time period to extend the lock by.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>True on success and False otherwise.</returns>
        Task<bool> ProlongSynchronousOperation(
            RecordId recordId,
            TimeSpan period,
            CancellationToken token);

        /// <summary>
        /// Creates a record in the journal making the resource unavailable to others.
        /// </summary>
        /// <param name="resource">The resource name.</param>
        /// <param name="scope">The scope.</param>
        /// <param name="expiration">The TTL of the lease.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>
        /// A newly created record if an exclusive lock on the resource
        /// has been successfully acquired and null otherwise.
        /// </returns>
        Task<Record> BeginSynchronousOperation(
            string resource,
            string scope,
            TimeSpan expiration,
            CancellationToken token);

        /// <summary>
        /// Releases the resource associated with the given record ID.
        /// </summary>
        /// <param name="recordId">The record ID to be marked as completed.</param>
        /// <param name="token">A cancellation token.</param>
        Task EndSynchronousOperation(
            RecordId recordId,
            CancellationToken token);
    }
}
