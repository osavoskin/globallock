namespace SynchronizationUtils.GlobalLock.Tests.Persistence
{
    internal class InMemoryRepository : IRepository
    {
        private readonly List<Record> records = [];
        private readonly DateTime dateTimeMin = new(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public int TotalCount => records.Count;
        public int CompletedCount => records.Count(o => o.CompletedAt > dateTimeMin);

        public Task<Record> BeginSynchronousOperation(string resource, string scope, TimeSpan expiration, CancellationToken token)
        {
            lock (records)
            {
                if (!IsResourceAvailable(resource, scope, token).Result)
                    return null;

                var record = new Record(new RecordId(scope))
                {
                    Scope = scope,
                    Resource = resource,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.Add(expiration),
                    CompletedAt = dateTimeMin
                };

                records.Add(record);
                return Task.FromResult(record);
            }
        }

        public Task EndSynchronousOperation(RecordId recordId, CancellationToken token)
        {
            lock (records)
            {
                var record = GetOngoingById(recordId);

                if (record is not null)
                    record.CompletedAt = DateTime.UtcNow;

                return Task.CompletedTask;
            }
        }

        public Task<bool> IsResourceAvailable(string resource, string scope, CancellationToken token)
        {
            lock (records)
            {
                return Task.FromResult(!records.Any(o => o.Resource == resource
                    && o.Scope == scope
                    && o.CompletedAt == dateTimeMin));
            }
        }

        public Task<bool> ProlongSynchronousOperation(RecordId recordId, TimeSpan period, CancellationToken token)
        {
            lock (records)
            {
                var record = GetOngoingById(recordId);

                if (record is not null)
                {
                    record.ExpiresAt += period;
                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
        }

        public bool IsActive(string leaseId)
        {
            lock (records) return GetOngoingById(leaseId) is not null;
        }

        private Record GetOngoingById(RecordId id)
        {
            return records.SingleOrDefault(o => o.Id == id && o.CompletedAt == dateTimeMin);
        }
    }
}
