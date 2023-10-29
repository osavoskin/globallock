using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System.Reflection;

namespace SynchronizationUtils.GlobalLock.Tests.Persistence
{
    internal class InMemoryTable : CloudTable
    {
        public Func<IEnumerable<Record>> QueryFunc { get; set; }

        public Func<TableOperation, CancellationToken, TableResult> CommandFunc { get; set; }

        public InMemoryTable() : base(new Uri("https://account.table.core.windows.net/locks")) { }

        public override Task<TableResult> ExecuteAsync(TableOperation operation, TableRequestOptions requestOptions, OperationContext operationContext, CancellationToken cancellationToken)
        {
            return Task.FromResult(CommandFunc?.Invoke(operation, cancellationToken));
        }

        public override Task<TableQuerySegment<T>> ExecuteQuerySegmentedAsync<T>(TableQuery<T> query, TableContinuationToken token, TableRequestOptions requestOptions, OperationContext operationContext, CancellationToken cancellationToken)
        {
            return Task.FromResult(GetQueryResult(QueryFunc?.Invoke())) as Task<TableQuerySegment<T>>;
        }

        public override Task<bool> CreateIfNotExistsAsync(TableRequestOptions requestOptions, OperationContext operationContext, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        private static TableQuerySegment<Record> GetQueryResult(IEnumerable<Record> records)
        {
            var constructor = GetConstructor(typeof(TableQuerySegment<Record>));

            var tableQuerySegment = constructor.Invoke(new object[]
            {
                records?.ToList() ?? new List<Record>()
            });

            return tableQuerySegment as TableQuerySegment<Record>;
        }

        private static ConstructorInfo GetConstructor(Type type) => type
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(o => o.GetParameters().Length == 1);
    }
}
