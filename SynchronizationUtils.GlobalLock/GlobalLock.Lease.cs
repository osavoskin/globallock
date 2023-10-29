using SynchronizationUtils.GlobalLock.Persistence;
using SynchronizationUtils.GlobalLock.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static SynchronizationUtils.GlobalLock.Utils.StringUtils;
using static System.Threading.CancellationTokenSource;

namespace SynchronizationUtils.GlobalLock
{
    internal partial class GlobalLock
    {
        /// <inheritdoc cref="ILease"/>
        private class Lease : ILease
        {
            /// <summary>
            /// Gets the instance of the global lock
            /// the lease has been created from.
            /// </summary>
            public GlobalLock GlobalLock { get; }

            /// <inheritdoc/>
            public string LeaseId => RecordId;

            /// <summary>
            /// Gets the resource name.
            /// </summary>
            public string Resource { get; }

            /// <summary>
            /// Gets the scope of the lease.
            /// </summary>
            public string Scope { get; }

            /// <summary>
            /// Gets the TTL of the lease.
            /// </summary>
            public TimeSpan Lifespan { get; }

            /// <summary>
            /// Gets the expiration time of the lease.
            /// </summary>
            public DateTime? ExpiresAt { get; private set; }

            /// <summary>
            /// Gets the log record ID.
            /// </summary>
            public RecordId RecordId { get; private set; }

            /// <summary>
            /// Gets the unique ID of the resource (including the scope).
            /// </summary>
            public string ResourceUID => GetResourceUID(Resource, Scope);

            /// <inheritdoc/>
            public bool IsAcquired => RecordId is not null
                && ExpiresAt.HasValue
                && ExpiresAt > DateTime.UtcNow;

            /// <summary>
            /// Intializes a new instance of the <see cref="Lease"/> class.
            /// </summary>
            /// <param name="globalLock">The global lock service instance.</param>
            /// <param name="resource">The resource to lock on.</param>
            /// <param name="scope">The scope of the lease.</param>
            /// <param name="expiration">The expected TTL of the lease.</param>
            public Lease(GlobalLock globalLock, string resource, string scope, TimeSpan expiration)
            {
                GlobalLock = Ensure.IsNotNull(globalLock, nameof(globalLock));
                Resource = Ensure.IsNotNullOrWhiteSpace(resource, nameof(resource));
                Scope = Ensure.IsNotNullOrWhiteSpace(scope, nameof(scope));
                Lifespan = Ensure.IsGreaterThan(expiration, TimeSpan.Zero, nameof(expiration));
            }

            /// <inheritdoc/>
            public ValueTask DisposeAsync() => new(Release());

            /// <inheritdoc/>
            public Task Release(CancellationToken token = default)
            {
                return IsAcquired ? GlobalLock.Release(LeaseId, token) : Task.CompletedTask;
            }

            /// <inheritdoc/>
            public Task Wait(CancellationToken token = default)
            {
                token.ThrowIfCancellationRequested();
                GlobalLock.serviceBoundToken.Token.ThrowIfCancellationRequested();

                if (IsAcquired)
                    return Task.CompletedTask;

                var tokenSource = token != default
                    ? CreateLinkedTokenSource(GlobalLock.serviceBoundToken.Token, token)
                    : GlobalLock.serviceBoundToken;

                lock (GlobalLock.requests)
                    return CreateRequest(callback, tokenSource.Token);

                void callback(Task task)
                {
                    if (token != default) tokenSource.Dispose();
                    if (task.Exception is not null)
                        throw task.Exception.GetBaseException();
                }
            }

            /// <summary>
            /// Marks the lease instance as acquired.
            /// </summary>
            /// <param name="recordId">The log record ID of the operation.</param>
            /// <param name="expiresAt">The expiration time of the lease.</param>
            public void SetAcquired(RecordId recordId, DateTime? expiresAt)
            {
                RecordId = recordId;
                ExpiresAt = expiresAt;
            }

            /// <summary>
            /// Creates a pending request for a lease.
            /// </summary>
            /// <param name="callback">
            /// The callback to execute when the task is completed.
            /// </param>
            /// <param name="token">A cancellation token.</param>
            private Task CreateRequest(Action<Task> callback, CancellationToken token)
            {
                var tcs = new TaskCompletionSource<byte>();
                token.Register(() => tcs.TrySetException(new OperationCanceledException()));

                GlobalLock.requests.TryGetValue(ResourceUID, out var queue);
                queue ??= new Queue<LeaseRequest>();

                queue.Enqueue(new LeaseRequest(this, tcs, token));
                GlobalLock.requests[ResourceUID] = queue;

                return tcs.Task.ContinueWith(callback, CancellationToken.None);
            }
        }
    }
}
