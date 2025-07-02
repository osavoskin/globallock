using Microsoft.Extensions.Options;
using SynchronizationUtils.GlobalLock.Tests.Persistence;

namespace SynchronizationUtils.GlobalLock.Tests
{
    [TestClass]
    public class GlobalLockTests
    {
        private const string Resource = "Tenant ID #1";
        private const string Scope = "E2E Migration";

        private readonly Random random = new();
        private readonly int timeoutSeconds = 15;
        private static int threadCount = 0;

        [TestMethod]
        public async Task VerifyThereIsOnlyOneThreadInCriticalSection()
        {
            // Arrange
            var repository = new InMemoryRepository();
            var config = Options.Create(new GlobalLockConfiguration());
            using var globalLock = new GlobalLock(repository, config);

            using var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            // Act
            var tasks = Enumerable.Range(0, 50).Select(o => Task.Run(() =>
                DoInParallel(globalLock, repository, tokenSource.Token))
            );

            await Task.WhenAll(tasks);

            // Assert
            Assert.AreEqual(500, repository.TotalCount);
            Assert.AreEqual(500, repository.CompletedCount);
        }

        [TestMethod]
        public async Task VerifyLeaseCanBeExtendedWhenBeingHeld()
        {
            // Arrange
            var config = Options.Create(new GlobalLockConfiguration());
            using var globalLock = new GlobalLock(new InMemoryRepository(), config);

            using var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromSeconds(5));

            // Act
            var lease = await globalLock.TryAcquire(
                Resource,
                token: tokenSource.Token);

            var extended = lease.IsAcquired && await globalLock.TryExtend(
                lease.LeaseId,
                token: tokenSource.Token);

            await globalLock.Release(lease.LeaseId, tokenSource.Token);

            // Assert
            Assert.IsTrue(extended);
        }

        [TestMethod]
        public async Task VerifyReleasedLeaseCannotBeExtended()
        {
            // Arrange
            var config = Options.Create(new GlobalLockConfiguration());
            using var globalLock = new GlobalLock(new InMemoryRepository(), config);

            using var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromSeconds(5));

            // Act
            var lease = await globalLock.TryAcquire(
                Resource,
                token: tokenSource.Token);

            Assert.IsTrue(lease.IsAcquired);
            await lease.Release(tokenSource.Token);

            var extended = await globalLock.TryExtend(
                lease.LeaseId,
                token: tokenSource.Token);

            // Assert
            Assert.IsFalse(extended);
        }

        private async Task DoInParallel(GlobalLock globalLock, InMemoryRepository repository, CancellationToken token)
        {
            for (int i = 0; i < 10; i++)
            {
                if (token.IsCancellationRequested)
                    break;

                var lease = await GetLease(globalLock, token);
                Assert.IsTrue(repository.IsActive(lease.LeaseId));

                try
                {
                    await DoSynchronously(token);
                }
                finally
                {
                    await globalLock.Release(lease.LeaseId, token);
                    Assert.IsFalse(repository.IsActive(lease.LeaseId));
                }
            }
        }

        private async Task DoSynchronously(CancellationToken token)
        {
            Interlocked.Increment(ref threadCount);
            try
            {
                if (Interlocked.CompareExchange(ref threadCount, 0, 0) > 1)
                    throw new InvalidOperationException("The lock did not work");

                await Task.Delay(random.Next(10), token);
            }
            finally
            {
                Interlocked.Decrement(ref threadCount);
            }
        }

        private async Task<ILease> GetLease(GlobalLock globalLock, CancellationToken token)
        {
            var lease = await globalLock.TryAcquire(Resource, Scope, null, token);
            if (lease.IsAcquired) return lease;

            using var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                tokenSource.Token,
                token);

            await lease.Wait(linkedTokenSource.Token);
            if (lease.IsAcquired) return lease;

            throw new InvalidOperationException("Unable to acquire the lease");
        }
    }
}
