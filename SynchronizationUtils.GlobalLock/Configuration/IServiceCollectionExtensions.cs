using SynchronizationUtils.GlobalLock.Persistence;
using SynchronizationUtils.GlobalLock.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace SynchronizationUtils.GlobalLock.Configuration
{
    /// <summary>
    /// The IoC container registration extensions.
    /// </summary>
    public static class IServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the <see cref="IGlobalLock"/> service with the DI container.
        /// </summary>
        /// <param name="services">The services collection.</param>
        /// <param name="connectionString">The storage connection string.</param>
        /// <returns>The original <paramref name="services"/> object.</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static IServiceCollection AddGlobalLock(
            this IServiceCollection services,
            string connectionString)
        {
            Ensure.IsNotNull(services, nameof(services));
            Ensure.IsNotNullOrWhiteSpace(connectionString, nameof(connectionString));

            return services.AddGlobalLock(new GlobalLockConfiguration
            {
                StorageConnectionString = connectionString
            });
        }

        /// <summary>
        /// Registers the <see cref="IGlobalLock"/> service with the DI container.
        /// </summary>
        /// <param name="services">The services collection.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="sectionName">The section that contains the global lock settings.</param>
        /// <returns>The original <paramref name="services"/> object.</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static IServiceCollection AddGlobalLock(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = "GlobalLock")
        {
            Ensure.IsNotNull(services, nameof(services));
            Ensure.IsNotNull(configuration, nameof(configuration));
            Ensure.IsNotNullOrWhiteSpace(sectionName, nameof(sectionName));

            var section = configuration.GetSection(sectionName);
            var lockConfiguration = section.Get<GlobalLockConfiguration>();

            return services.AddGlobalLock(lockConfiguration);
        }

        /// <summary>
        /// Registers the <see cref="IGlobalLock"/> service with the DI container.
        /// </summary>
        /// <param name="services">The services collection.</param>
        /// <param name="configuration">The global lock configuration.</param>
        /// <returns>The original <paramref name="services"/> object.</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static IServiceCollection AddGlobalLock(
            this IServiceCollection services,
            GlobalLockConfiguration configuration)
        {
            Ensure.IsNotNull(services, nameof(services));
            Ensure.IsNotNull(configuration, nameof(configuration));

            services.AddSingleton<IGlobalLock, GlobalLock>();
            services.AddSingleton<IRepository, Repository>();
            services.AddSingleton<IStorageClient, StorageClient>();

            services.Configure<GlobalLockConfiguration>(o =>
            {
                o.TableName = Ensure.IsNotNullOrWhiteSpace(
                    configuration.TableName,
                    nameof(configuration.TableName));

                o.ContainerName = Ensure.IsNotNullOrWhiteSpace(
                    configuration.ContainerName,
                    nameof(configuration.ContainerName));

                o.StorageConnectionString = Ensure.IsNotNullOrWhiteSpace(
                    configuration.StorageConnectionString,
                    nameof(configuration.StorageConnectionString));

                o.LeaseDefaultExpirationSeconds = Ensure.IsGreaterThan(
                    configuration.LeaseDefaultExpirationSeconds, 0,
                    nameof(configuration.LeaseDefaultExpirationSeconds));

                o.LeaseAcquirementIntervalSeconds = Ensure.IsGreaterThan(
                    configuration.LeaseAcquirementIntervalSeconds, 0,
                    nameof(configuration.LeaseAcquirementIntervalSeconds));
            });

            return services;
        }
    }
}
