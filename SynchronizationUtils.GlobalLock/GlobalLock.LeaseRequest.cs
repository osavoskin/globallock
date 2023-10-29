using SynchronizationUtils.GlobalLock.Utils;
using System.Threading;
using System.Threading.Tasks;

namespace SynchronizationUtils.GlobalLock
{
    internal partial class GlobalLock
    {
        /// <summary>
        /// Represents a pending request for a lease.
        /// </summary>
        private readonly struct LeaseRequest
        {
            /// <summary>
            /// Gets the lease waiting for activation.
            /// </summary>
            public Lease Lease { get; }

            /// <summary>
            /// Gets the task to be completed on lease activation.
            /// </summary>
            public TaskCompletionSource<byte> Task { get; }

            /// <summary>
            /// Gets the cancellation token used for this request.
            /// </summary>
            public CancellationToken Token { get; }

            /// <summary>
            /// Gets a value indicating whether the lease has been already acquired or still waiting.
            /// </summary>
            public bool IsPending => !Token.IsCancellationRequested && !Lease.IsAcquired;

            /// <summary>
            /// Initializes a new instance of the <see cref="LeaseRequest"/> struct.
            /// </summary>
            /// <param name="lease">The lease waiting for activation.</param>
            /// <param name="tcs">The task to be completed on lease activation.</param>
            /// <param name="token">A cancellation token.</param>
            public LeaseRequest(Lease lease, TaskCompletionSource<byte> tcs, CancellationToken token)
            {
                Lease = Ensure.IsNotNull(lease, nameof(lease));
                Task = Ensure.IsNotNull(tcs, nameof(tcs));
                Token = token;
            }
        }
    }
}
