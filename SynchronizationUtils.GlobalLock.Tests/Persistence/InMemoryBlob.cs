using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace SynchronizationUtils.GlobalLock.Tests.Persistence
{
    internal class InMemoryBlob : CloudBlockBlob
    {
        public Action OnReleaseCallback { get; set; }

        public InMemoryBlob() : base(new Uri("https://account.blob.core.windows.net/locks")) { }

        public override Task ReleaseLeaseAsync(AccessCondition accessCondition, BlobRequestOptions options, OperationContext operationContext, CancellationToken cancellationToken)
        {
            OnReleaseCallback?.Invoke();
            return Task.CompletedTask;
        }
    }
}
