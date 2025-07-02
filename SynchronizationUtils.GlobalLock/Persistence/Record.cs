using Azure.Data.Tables;
using SynchronizationUtils.GlobalLock.Utils;
using System;
using System.Runtime.Serialization;

namespace SynchronizationUtils.GlobalLock.Persistence
{
    /// <summary>
    /// Represents an entry in the synchronous operations history log.
    /// </summary>
    internal class Record : ITableEntity
    {
        /// <summary>
        /// Gets or sets the partition key of the table entity.
        /// </summary>
        public string PartitionKey { get; set; }

        /// <summary>
        /// Gets or sets the row key of the table entity.
        /// </summary>
        public string RowKey { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the table entity.
        /// </summary>
        public DateTimeOffset? Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the ETag of the table entity.
        /// </summary>
        public Azure.ETag ETag { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Record"/> class.
        /// </summary>
        public Record() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Record"/> class.
        /// </summary>
        /// <param name="id">The record ID.</param>
        public Record(RecordId id)
        {
            PartitionKey = Ensure.IsNotNullOrWhiteSpace(id?.PartitionKey);
            RowKey = Ensure.IsNotNullOrWhiteSpace(id?.RowKey);
        }

        /// <summary>
        /// Gets the record ID.
        /// Note: This property is not stored in the table as it's derived from PartitionKey and RowKey.
        /// </summary>
        [IgnoreDataMember]
        public RecordId Id => new(RowKey, PartitionKey);

        /// <summary>
        /// Gets or sets the resource name.
        /// </summary>
        public string Resource { get; set; }

        /// <summary>
        /// Gets or sets the scope of the synchronous operation.
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// Gets or sets the time the lease has been acquired at.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the expiration date of the lease.
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the time the lease has been released at.
        /// </summary>
        public DateTime CompletedAt { get; set; }
    }
}
