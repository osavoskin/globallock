using System.Security.Cryptography;
using System.Text;

namespace SynchronizationUtils.GlobalLock.Utils
{
    /// <summary>
    /// Utility class to facilitate work with strings.
    /// </summary>
    internal static class StringUtils
    {
        /// <summary>
        /// Gets an MD5 hash of the provided string.
        /// </summary>
        /// <param name="input">The string to take a hash of.</param>
        /// <returns>An MD5 hash of the given string.</returns>
        public static string GetHash(this string input)
        {
            Ensure.IsNotNull(input, nameof(input));

            using var md5 = MD5.Create();
            var inputBytes = Encoding.ASCII.GetBytes(input);
            var hashBytes = md5.ComputeHash(inputBytes);

            var builder = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
                builder.Append(hashBytes[i].ToString("X2"));

            return builder.ToString().ToLower();
        }

        /// <summary>
        /// Gets the resource UID.
        /// </summary>
        /// <param name="resource">A resource name.</param>
        /// <param name="scope">A scope.</param>
        /// <returns>
        /// Basically a hash of the resource & scope strings.
        /// </returns>
        public static string GetResourceUID(string resource, string scope)
        {
            Ensure.IsNotNullOrWhiteSpace(resource, nameof(resource));
            Ensure.IsNotNullOrWhiteSpace(scope, nameof(scope));
            return (resource + scope).GetHash();
        }
    }
}
