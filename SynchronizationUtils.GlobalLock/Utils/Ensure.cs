using System;
using System.Runtime.CompilerServices;

namespace SynchronizationUtils.GlobalLock.Utils
{
    /// <summary>
    /// Contains validation assertions and helper functions to ensure data integrity.
    /// </summary>
    internal static class Ensure
    {
        /// <summary>
        /// Checks if the argument is not null.
        /// </summary>
        /// <typeparam name="T">The type of the argument to check.</typeparam>
        /// <param name="value">The value of the argument to check.</param>
        /// <param name="name">The name of the argument.</param>
        /// <param name="message">The custom error message to use.</param>
        /// <returns>The provided value if it is valid and throws otherwise.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T IsNotNull<T>(T value, string name = null, string message = null)
        {
            return value ?? throw new ArgumentNullException(name, message ?? "Argument cannot be null");
        }

        /// <summary>
        /// Checks if the given string is not null or whitespace.
        /// </summary>
        /// <param name="value">The value of the argument to check.</param>
        /// <param name="name">The name of the argument.</param>
        /// <param name="message">The custom error message to use.</param>
        /// <returns>The provided value if it is valid and throws otherwise.</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string IsNotNullOrWhiteSpace(string value, string name = null, string message = null)
        {
            IsNotNull(value, name);

            return !string.IsNullOrWhiteSpace(value) ? value
                : throw new ArgumentException(message ?? "Argument cannot be an empty string or whitespace", name);
        }

        /// <summary>
        /// Checks if the given value is greater than the comparand.
        /// </summary>
        /// <param name="value">The value of the argument to check.</param>
        /// <param name="comparand">The value to compare with.</param>
        /// <param name="name">The name of the argument.</param>
        /// <param name="message">The custom error message to use.</param>
        /// <returns>The provided value if it is valid and throws otherwise.</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T IsGreaterThan<T>(T value, T comparand, string name = null, string message = null) where T : IComparable
        {
            IsNotNull(value, name);
            IsNotNull(comparand, nameof(comparand));

            return value.CompareTo(comparand) > 0 ? value
                : throw new ArgumentOutOfRangeException(name, message ?? $"Argument must be greater than {comparand}");
        }
    }
}
