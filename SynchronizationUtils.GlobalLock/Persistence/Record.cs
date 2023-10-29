using SynchronizationUtils.GlobalLock.Utils;
using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace SynchronizationUtils.GlobalLock.Persistence
{
    /// <summary>
    /// Represents an entry in the synchronous operations history log.
    /// </summary>
    internal class Record : TableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Record"/> class.
        /// </summary>
        public Record() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Record"/> class.
        /// </summary>
        /// <param name="id">The record ID.</param>
        public Record(RecordId id) : base(
            Ensure.IsNotNullOrWhiteSpace(id?.PartitionKey),
            Ensure.IsNotNullOrWhiteSpace(id?.RowKey))
        { }

        /// <summary>
        /// Gets the record ID.
        /// </summary>
        [IgnoreProperty]
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
