using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace SynchronizationUtils.GlobalLock.Tests.Persistence
{
    internal class InMemoryBlob : BlobLeaseClient
    {
        public Action OnReleaseCallback { get; set; }

        public override Task<Response<ReleasedObjectInfo>> ReleaseAsync(RequestConditions conditions = null, CancellationToken cancellationToken = default)
        {
            OnReleaseCallback?.Invoke();
            return Task.FromResult(Response.FromValue(new ReleasedObjectInfo(new ETag(), DateTimeOffset.Now), new MockResponse(200, "OK")));
        }
    }
}
