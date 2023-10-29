using SynchronizationUtils.GlobalLock.Configuration;
using SynchronizationUtils.GlobalLock.Persistence;
using SynchronizationUtils.GlobalLock.Utils;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SynchronizationUtils.GlobalLock
{
    /// <inheritdoc cref="IGlobalLock"/>
    internal partial class GlobalLock : IGlobalLock
    {
        private readonly IRepository repository;
        private readonly GlobalLockConfiguration configuration;
        private readonly Timer timer;

        private readonly Utils.Semaphore semaphore = new();
        private readonly CancellationTokenSource serviceBoundToken = new();
        private readonly Dictionary<string, Queue<LeaseRequest>> requests = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalLock"/> class.
        /// </summary>
        /// <param name="repository">The synchronous operations repository.</param>
        /// <param name="configuration">The global lock configuration.</param>
        public GlobalLock(
            IRepository repository,
            IOptions<GlobalLockConfiguration> configuration)
        {
            this.repository = Ensure.IsNotNull(repository, nameof(repository));
            this.configuration = Ensure.IsNotNull(configuration?.Value, nameof(configuration));

            timer = new Timer(OnTimerTick, null,
                TimeSpan.FromSeconds(this.configuration.LeaseAcquirementIntervalSeconds),
                TimeSpan.FromSeconds(this.configuration.LeaseAcquirementIntervalSeconds));
        }

        /// <inheritdoc/>
        public async Task<ILease> TryAcquire(
            string resource,
            string scope = "default",
            TimeSpan? expiration = null,
            CancellationToken token = default)
        {
            Ensure.IsNotNullOrWhiteSpace(resource, nameof(resource));
            Ensure.IsNotNullOrWhiteSpace(scope, nameof(scope));

            token.ThrowIfCancellationRequested();
            serviceBoundToken.Token.ThrowIfCancellationRequested();

            var linkedTokenSource = token != default
                ? CancellationTokenSource.CreateLinkedTokenSource(serviceBoundToken.Token, token)
                : serviceBoundToken;

            try
            {
                var lease = new Lease(this,
                    resource.Trim().ToLower(), scope.Trim().ToLower(),
                    expiration ?? TimeSpan.FromSeconds(configuration.LeaseDefaultExpirationSeconds));

                return await semaphore.Run(lease.ResourceUID,
                    token => InternalAcquire(lease, token),
                    linkedTokenSource.Token);
            }
            finally
            {
                if (token != default) linkedTokenSource.Dispose();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> TryExtend(string leaseId, TimeSpan? period = null, CancellationToken token = default)
        {
            Ensure.IsNotNull(leaseId, nameof(leaseId));
            leaseId = ((string)(RecordId)leaseId) ?? string.Empty;
            Ensure.IsNotNullOrWhiteSpace(leaseId, nameof(leaseId), "Invalid lease ID");

            token.ThrowIfCancellationRequested();
            serviceBoundToken.Token.ThrowIfCancellationRequested();

            var linkedTokenSource = token != default
                ? CancellationTokenSource.CreateLinkedTokenSource(serviceBoundToken.Token, token)
                : serviceBoundToken;

            try
            {
                return await repository.ProlongSynchronousOperation(leaseId,
                    period ?? TimeSpan.FromSeconds(configuration.LeaseDefaultExpirationSeconds),
                    linkedTokenSource.Token);
            }
            finally
            {
                if (token != default) linkedTokenSource.Dispose();
            }
        }

        /// <inheritdoc/>
        public async Task Release(string leaseId, CancellationToken token = default)
        {
            Ensure.IsNotNull(leaseId, nameof(leaseId));
            leaseId = ((string)(RecordId)leaseId) ?? string.Empty;
            Ensure.IsNotNullOrWhiteSpace(leaseId, nameof(leaseId), "Invalid lease ID");

            token.ThrowIfCancellationRequested();
            serviceBoundToken.Token.ThrowIfCancellationRequested();

            var linkedTokenSource = token != default
                ? CancellationTokenSource.CreateLinkedTokenSource(serviceBoundToken.Token, token)
                : serviceBoundToken;

            try
            {
                await repository.EndSynchronousOperation(leaseId, linkedTokenSource.Token);
            }
            finally
            {
                if (token != default) linkedTokenSource.Dispose();
            }

            OnTimerTick(null);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            timer.Dispose();
            serviceBoundToken.Cancel();
            serviceBoundToken.Dispose();
        }

        /// <summary>
        /// Tries to acquire a lease on the resource it is associated with.
        /// </summary>
        /// <param name="lease">The lease instance to acquire.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>The lease instance.</returns>
        private async Task<Lease> InternalAcquire(Lease lease, CancellationToken token)
        {
            if (!await repository.IsResourceAvailable(lease.Resource, lease.Scope, token))
                return lease;

            var record = await repository.BeginSynchronousOperation(
                lease.Resource,
                lease.Scope,
                lease.Lifespan,
                token);

            lease.SetAcquired(record?.Id, record?.ExpiresAt);
            return lease;
        }

        /// <summary>
        /// Tries to acquire the next lease in queue for the given resource UID.
        /// </summary>
        /// <param name="resourceUID">The resource UID to acquire a lease for.</param>
        private async Task TryAcquirePending(string resourceUID)
        {
            if (!requests.TryGetValue(resourceUID, out var queue))
                return;

            if (queue.Count == 0)
            {
                requests.Remove(resourceUID);
                return;
            }

            var request = queue.Peek();

            if (!request.IsPending)
            {
                queue.Dequeue();
                return;
            }

            var lease = await InternalAcquire(
                request.Lease,
                request.Token);

            if (lease.IsAcquired)
            {
                request.Lease.SetAcquired(
                    lease.RecordId,
                    lease.ExpiresAt.Value);

                request.Task.TrySetResult(0);
                queue.Dequeue();
            }
        }

        /// <summary>
        /// The timer callback. Tries to process pending requests if any.
        /// </summary>
        private void OnTimerTick(object _)
        {
            foreach (var resourceUID in requests.Keys.ToList())
            {
                semaphore.Run(resourceUID,
                    token => TryAcquirePending(resourceUID),
                    serviceBoundToken.Token);
            }
        }
    }
}
