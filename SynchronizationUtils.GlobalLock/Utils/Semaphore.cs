using Polly;
using Polly.Bulkhead;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SynchronizationUtils.GlobalLock.Utils
{
    /// <summary>
    /// A simple utilitily to synchronize operations related to the same resource.
    /// </summary>
    internal class Semaphore
    {
        private readonly ConcurrentDictionary<string, WeakReference<AsyncBulkheadPolicy>> policies = new();

        /// <summary>
        /// Locks on a resource UID and executes a function in a thread safe manner.
        /// </summary>
        /// <param name="resourceUID">The resource UID to lock on.</param>
        /// <param name="func">The function to execute.</param>
        /// <param name="token">A cancellation token.</param>
        public Task Run(string resourceUID, Func<CancellationToken, Task> func, CancellationToken token)
        {
            Ensure.IsNotNullOrWhiteSpace(resourceUID, nameof(resourceUID));
            Ensure.IsNotNull(func, nameof(func));
            return GetPolicy(resourceUID).ExecuteAsync(func, token);
        }

        /// <summary>
        /// Locks on a resource UID and executes a function in a thread safe manner.
        /// </summary>
        /// <typeparam name="T">The type of the function result.</typeparam>
        /// <param name="resourceUID">The resource UID to lock on.</param>
        /// <param name="func">The function to execute.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>The function result.</returns>
        public Task<T> Run<T>(string resourceUID, Func<CancellationToken, Task<T>> func, CancellationToken token)
        {
            Ensure.IsNotNullOrWhiteSpace(resourceUID, nameof(resourceUID));
            Ensure.IsNotNull(func, nameof(func));
            return GetPolicy(resourceUID).ExecuteAsync(func, token);
        }

        /// <summary>
        /// Gets cached or creates a new bulkhead policy to handle the resource specific tasks.
        /// </summary>
        /// <param name="resourceUID">The resource UID to identify the policy.</param>
        /// <returns>The bulkhead policy associated with the given resource UID.</returns>
        private AsyncBulkheadPolicy GetPolicy(string resourceUID)
        {
            var weakRef = policies.GetOrAdd(resourceUID, Factory);
            if (weakRef.TryGetTarget(out var policy)) return policy;

            policies.TryUpdate(resourceUID, Factory(null), weakRef);
            return GetPolicy(resourceUID);
        }

        /// <summary>
        /// The bulkhead policy factory.
        /// </summary>
        /// <returns>A newly created policy as a weak reference.</returns>
        static WeakReference<AsyncBulkheadPolicy> Factory(string _)
        {
            var policy = Policy.BulkheadAsync(1, int.MaxValue);
            return new WeakReference<AsyncBulkheadPolicy>(policy);
        }
    }
}
