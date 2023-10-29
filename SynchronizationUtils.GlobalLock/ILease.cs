using System;
using System.Threading;
using System.Threading.Tasks;

namespace SynchronizationUtils.GlobalLock
{
    /// <summary>
    /// Represents the state of a lease.
    /// </summary>
    public interface ILease : IAsyncDisposable
    {
        /// <summary>
        /// Gets the lease ID.
        /// </summary>
        string LeaseId { get; }

        /// <summary>
        /// Gets a value indicating whether the lease has been acquired.
        /// </summary>
        bool IsAcquired { get; }

        /// <summary>
        /// Pauses the thread until the lease is acquired.
        /// </summary>
        /// <param name="token">A cancellation token.</param>
        /// <exception cref="OperationCanceledException"></exception>
        Task Wait(CancellationToken token = default);

        /// <summary>
        /// Explicitly releases the lease.
        /// </summary>
        /// <param name="token">A cancellation token.</param>
        /// <exception cref="OperationCanceledException"></exception>
        Task Release(CancellationToken token = default);
    }
}
