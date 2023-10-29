namespace SynchronizationUtils.GlobalLock.Configuration
{
    /// <summary>
    /// The global lock service configuration.
    /// </summary>
    public class GlobalLockConfiguration
    {
        /// <summary>
        /// Gets or sets the storage table name.
        /// </summary>
        public string TableName { get; set; } = "locks";

        /// <summary>
        /// Gets or sets the storage container name.
        /// </summary>
        public string ContainerName { get; set; } = "locks";

        /// <summary>
        /// Gets or sets the storage connection string.
        /// </summary>
        public string StorageConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the lease default TTL in seconds.
        /// </summary>
        public double LeaseDefaultExpirationSeconds { get; set; } = 86400;

        /// <summary>
        /// Gets or sets the pending requests acquirement interval in seconds.
        /// </summary>
        public double LeaseAcquirementIntervalSeconds { get; set; } = 5;
    }
}
