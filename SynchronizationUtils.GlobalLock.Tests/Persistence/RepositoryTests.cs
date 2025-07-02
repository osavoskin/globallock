using Azure;
using Microsoft.Extensions.Options;
using SynchronizationUtils.GlobalLock.Utils;

namespace SynchronizationUtils.GlobalLock.Tests.Persistence
{
    [TestClass]
    public class RepositoryTests
    {
        private const string Resource = "Tenant ID #1";
        private const string Scope = "E2E Migration";

        private readonly InMemoryBlob blob = new();
        private readonly InMemoryTable table = new();
        private readonly Mock<IStorageClient> storageClient = new();
        private readonly GlobalLockConfiguration configuration = new();
        private readonly Repository repository;

        public RepositoryTests()
        {
            repository = new Repository(storageClient.Object, Options.Create(configuration));
        }

        [TestInitialize]
        public void Initialize()
        {
            storageClient
                .Setup(o => o.GetTableClient(configuration.TableName))
                .ReturnsAsync(table);

            // the resource is available by default
            table.QueryFunc = () => [];

            // success, echo back what has been received
            table.CommandFunc = (record, token) => MockResponse.NoContent;
        }

        [TestMethod]
        public async Task VerifyRecordCreatedWhenLeaseCanBeAcquired()
        {
            // Arrange
            var resourceUID = StringUtils.GetResourceUID(Resource, Scope);
            using var tokenSource = new CancellationTokenSource();

            var leaseReleased = false;
            blob.OnReleaseCallback = () => leaseReleased = true;

            storageClient
                .Setup(o => o.TryAcquireBlobLease(resourceUID, tokenSource.Token))
                .ReturnsAsync(new BlobLease(blob, 30, "leaseid"))
                .Verifiable();

            // Act
            var record = await repository.BeginSynchronousOperation(
                Resource,
                Scope,
                TimeSpan.FromMinutes(1),
                tokenSource.Token);

            // Assert
            Assert.IsNotNull(record);
            Assert.IsTrue(leaseReleased);
            Mock.Verify(storageClient);
        }

        [TestMethod]
        public async Task VerifyRecordNotCreatedWhenLeaseBeingHoldByAnotherClient()
        {
            // Arrange
            var resourceUID = StringUtils.GetResourceUID(Resource, Scope);
            using var tokenSource = new CancellationTokenSource();

            storageClient
                .Setup(o => o.TryAcquireBlobLease(resourceUID, tokenSource.Token))
                .ReturnsAsync(new BlobLease(blob, 30, null))
                .Verifiable();

            // Act
            var record = await repository.BeginSynchronousOperation(
                Resource,
                Scope,
                TimeSpan.FromMinutes(1),
                tokenSource.Token);

            // Assert
            Assert.IsNull(record);
            Mock.Verify(storageClient);
        }

        [TestMethod]
        public async Task VerifyRecordNotCreatedWhenCancellationRequested()
        {
            // Arrange
            using var tokenSource = new CancellationTokenSource();
            tokenSource.Cancel();

            storageClient
                .Setup(o => o.TryAcquireBlobLease(It.IsAny<string>(), tokenSource.Token))
                .ReturnsAsync(new BlobLease(blob, 30, null));

            // Act
            var task = repository.BeginSynchronousOperation(
                Resource,
                Scope,
                TimeSpan.FromMinutes(1),
                tokenSource.Token);

            // Assert
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => task);

            storageClient.Verify(o =>
                o.TryAcquireBlobLease(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);

            storageClient.Verify(o =>
                o.GetTableClient(It.IsAny<string>()),
                Times.Never);
        }

        [TestMethod]
        public async Task VerifyNewRecordNotCreatedWhenActiveRecordAlreadyExists()
        {
            // Arrange
            var resourceUID = StringUtils.GetResourceUID(Resource, Scope);
            using var tokenSource = new CancellationTokenSource();

            storageClient
                .Setup(o => o.TryAcquireBlobLease(resourceUID, tokenSource.Token))
                .ReturnsAsync(new BlobLease(blob, 30, "leaseid"))
                .Verifiable();

            var leaseReleased = false;
            blob.OnReleaseCallback = () => leaseReleased = true;
            table.QueryFunc = () => [new Record()];

            // Act
            var record = await repository.BeginSynchronousOperation(
                Resource,
                Scope,
                TimeSpan.FromMinutes(1),
                tokenSource.Token);

            // Assert
            Assert.IsNull(record);
            Assert.IsTrue(leaseReleased);
            Mock.Verify(storageClient);
        }

        [TestMethod]
        public async Task VerifyRecordCompletedWhenActiveExists()
        {
            // Arrange
            var recordId = new RecordId(Scope);
            var recordUpdated = false;

            table.QueryFunc = () =>
            [
                new Record(recordId) { ETag = new Azure.ETag(Guid.NewGuid().ToString()) }
            ];

            table.CommandFunc = (operation, _) =>
            {
                recordUpdated =
                    operation.RowKey == recordId.RowKey &&
                    operation.PartitionKey == recordId.PartitionKey;

                return null;
            };

            // Act
            await repository.EndSynchronousOperation(recordId, default);

            // Assert
            Assert.IsTrue(recordUpdated);
        }

        [TestMethod]
        public async Task VerifyRecordNotBeingCompletedMultipleTimes()
        {
            // Arrange
            var recordId = new RecordId(Scope);
            var requestIsSent = false;

            table.CommandFunc = (record, _) =>
            {
                requestIsSent = true;
                return MockResponse.NoContent;
            };

            // Act
            await repository.EndSynchronousOperation(recordId, default);

            // Assert
            Assert.IsFalse(requestIsSent);
        }

        [TestMethod]
        public async Task VerifyEndOperationEtagMismatchBeingRetried()
        {
            // Arrange
            var recordId = new RecordId(Scope);
            var recordUpdated = false;

            table.QueryFunc = () => [new Record(recordId)];
            table.CommandFunc = (operation, _) =>
            {
                table.CommandFunc = (nextOp, _) =>
                {
                    recordUpdated =
                        nextOp.RowKey == recordId.RowKey &&
                        nextOp.PartitionKey == recordId.PartitionKey;

                    return MockResponse.NoContent;
                };

                var errorResponse = MockResponse.PreconditionFailed;
                throw new RequestFailedException(errorResponse);
            };

            // Act
            await repository.EndSynchronousOperation(recordId, default);

            // Assert
            Assert.IsTrue(recordUpdated);
        }

        [TestMethod]
        public async Task VerifyLeaseExtendedWhenActiveRecordExists()
        {
            // Arrange
            var recordId = new RecordId(Scope);
            var leaseExtended = false;

            var extendBy = TimeSpan.FromMinutes(1);
            var originalExpiration = DateTime.UtcNow;
            var expectedExpiration = originalExpiration + extendBy;

            var record = new Record(recordId)
            {
                ExpiresAt = originalExpiration
            };

            table.QueryFunc = () => [record];
            table.CommandFunc = (operation, _) =>
            {
                leaseExtended =
                    operation.RowKey == recordId.RowKey &&
                    operation.PartitionKey == recordId.PartitionKey;

                record.ExpiresAt = operation.ExpiresAt;
                return MockResponse.NoContent;
            };

            // Act
            var success = await repository.ProlongSynchronousOperation(
                recordId, extendBy, default
            );

            // Assert
            Assert.IsTrue(success);
            Assert.IsTrue(leaseExtended);
            Assert.AreEqual(expectedExpiration, record.ExpiresAt);
        }

        [TestMethod]
        public async Task VerifyLeaseExtentionEtagMismatchBeingRetried()
        {
            // Arrange
            var recordId = new RecordId(Scope);
            var leaseExtended = false;

            var extendBy = TimeSpan.FromMinutes(1);
            var originalExpiration = DateTime.UtcNow;
            var expectedExpiration = originalExpiration + extendBy;

            var record = new Record(recordId)
            {
                ExpiresAt = originalExpiration
            };

            table.QueryFunc = () => [new Record(recordId)
            {
                ExpiresAt = originalExpiration
            }];

            table.CommandFunc = (operation, _) =>
            {
                table.CommandFunc = (nextOp, _) =>
                {
                    leaseExtended =
                        nextOp.RowKey == recordId.RowKey &&
                        nextOp.PartitionKey == recordId.PartitionKey;

                    record.ExpiresAt = nextOp.ExpiresAt;
                    return MockResponse.NoContent;
                };

                var errorResponse = MockResponse.PreconditionFailed;
                throw new RequestFailedException(errorResponse);
            };

            // Act
            var success = await repository.ProlongSynchronousOperation(
                recordId, extendBy, default
            );

            // Assert
            Assert.IsTrue(success);
            Assert.IsTrue(leaseExtended);
            Assert.AreEqual(expectedExpiration, record.ExpiresAt);
        }

        [TestMethod]
        public async Task VerifyCannotExtendLeaseWhenActiveRecordNotFound()
        {
            // Arrange
            var recordId = new RecordId(Scope);
            var requestIsSent = false;

            table.CommandFunc = (operation, _) =>
            {
                requestIsSent = true;
                return MockResponse.NoContent;
            };

            // Act
            var success = await repository.ProlongSynchronousOperation(
                recordId,
                TimeSpan.FromMinutes(1),
                default);

            // Assert
            Assert.IsFalse(success);
            Assert.IsFalse(requestIsSent);
        }

        [TestMethod]
        public async Task VerifyResourceIsAvailableWhenActiveRecordDoesNotExist()
        {
            // Arrange
            table.QueryFunc = () => [];

            // Act
            var available = await repository.IsResourceAvailable(
                Resource, Scope, default
            );

            // Assert
            Assert.IsTrue(available);
        }

        [TestMethod]
        public async Task VerifyResourceIsUnavailableWhenActiveRecordExists()
        {
            // Arrange
            table.QueryFunc = () => [new Record()];

            // Act
            var available = await repository.IsResourceAvailable(
                Resource, Scope, default
            );

            // Assert
            Assert.IsFalse(available);
        }

        [TestMethod]
        public async Task VerifyRecordNotCreatedWhenNativeLeaseIsLost()
        {
            // Arrange
            storageClient
                .Setup(o => o.TryAcquireBlobLease(It.IsAny<string>(), default))
                .ReturnsAsync(new BlobLease(blob, 3, "leaseid"))
                .Verifiable();

            table.CommandFunc = (operation, token) =>
            {
                Thread.Sleep(TimeSpan.FromSeconds(5));
                token.ThrowIfCancellationRequested();
                return MockResponse.NoContent;
            };

            // Act
            var task = repository.BeginSynchronousOperation(
                Resource,
                Scope,
                TimeSpan.FromMinutes(1),
                default);

            // Assert
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => task);
            Mock.Verify(storageClient);
        }
    }
}
