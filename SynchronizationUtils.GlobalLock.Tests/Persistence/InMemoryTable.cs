using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;

namespace SynchronizationUtils.GlobalLock.Tests.Persistence
{
    internal partial class InMemoryTable : TableClient
    {
        public Func<Record, CancellationToken, Response> CommandFunc { get; set; }

        public Func<IEnumerable<Record>> QueryFunc { get; set; }

        public override AsyncPageable<T> QueryAsync<T>(string filter = null, int? maxPerPage = null, IEnumerable<string> select = null, CancellationToken cancellationToken = default)
        {
            return AsyncPageable<T>.FromPages([Page<T>.FromValues([.. QueryFunc().Cast<T>()], null, null)]);
        }

        public override Task<Response> AddEntityAsync<T>(T entity, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CommandFunc(entity as Record, cancellationToken));
        }

        public override Task<Response> UpdateEntityAsync<T>(T entity, ETag ifMatch, TableUpdateMode mode = TableUpdateMode.Merge, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CommandFunc(entity as Record, cancellationToken));
        }

        public override Task<Response<TableItem>> CreateIfNotExistsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Response.FromValue(new TableItem(string.Empty), new MockResponse(201, "Created")));
        }
    }
}
