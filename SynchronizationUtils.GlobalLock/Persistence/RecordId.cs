using SynchronizationUtils.GlobalLock.Utils;
using System;
using System.Text;

namespace SynchronizationUtils.GlobalLock.Persistence
{
    /// <summary>
    /// Represents an operation log record ID.
    /// </summary>
    internal class RecordId
    {
        /// <summary>
        /// Gets the table row key.
        /// </summary>
        public string RowKey { get; }

        /// <summary>
        /// Gets the partition key.
        /// </summary>
        public string PartitionKey { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RecordId"/> class.
        /// </summary>
        /// <param name="scope">The synchronous operation scope.</param>
        public RecordId(string scope) : this(
            Guid.NewGuid().ToString("N").ToLower(),
            scope?.GetHash())
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="RecordId"/> class.
        /// </summary>
        /// <param name="rowKey">The table row key.</param>
        /// <param name="partitionKey">The partion key.</param>
        public RecordId(string rowKey, string partitionKey)
        {
            RowKey = Ensure.IsNotNullOrWhiteSpace(rowKey);
            PartitionKey = Ensure.IsNotNullOrWhiteSpace(partitionKey);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is RecordId id && id.GetHashCode() == GetHashCode();
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return RowKey.GetHashCode() ^ PartitionKey.GetHashCode();
        }

        /// <summary>
        /// The '==' operator override.
        /// </summary>
        /// <param name="left">The left operand to compare.</param>
        /// <param name="right">The right operand to compare.</param>
        /// <returns>True if the operands are equal and False otherwise.</returns>
        public static bool operator ==(RecordId left, RecordId right)
        {
            if (left is null ^ right is null)
                return false;

            return left is null || left.Equals(right);
        }

        /// <summary>
        /// The '!=' operator override.
        /// </summary>
        /// <param name="left">The left operand to compare.</param>
        /// <param name="right">The right operand to compare.</param>
        /// <returns>True if the operands are not equal and False otherwise.</returns>
        public static bool operator !=(RecordId left, RecordId right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Converts a lease ID into a record ID.
        /// </summary>
        /// <param name="leaseId">A lease ID.</param>
        public static implicit operator RecordId(string leaseId)
        {
            if (leaseId is not null)
            {
                byte[] bytes;

                try
                {
                    bytes = Convert.FromBase64String(leaseId);
                }
                catch (FormatException)
                {
                    return null;
                }

                var data = Encoding.UTF8.GetString(bytes).Split('|');

                return data.Length == 2
                    ? new RecordId(data[0], data[1])
                    : null;
            }

            return null;
        }

        /// <summary>
        /// Converts a record ID into a lease ID.
        /// </summary>
        /// <param name="recordId">A record ID.</param>
        public static implicit operator string(RecordId recordId)
        {
            if (recordId is not null)
            {
                var id = $"{recordId.RowKey}|{recordId.PartitionKey}";
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(id));
            }

            return null;
        }
    }
}
