using System;

namespace OpenSimCity.Core.Determinism
{
    /// <summary>
    /// Hash functions whose inputs and byte order are explicitly defined for
    /// use in deterministic simulation paths.
    /// </summary>
    public static class DeterministicHash
    {
        private const ulong FnvOffsetBasis64 = 14695981039346656037UL;
        private const ulong FnvPrime64 = 1099511628211UL;

        /// <summary>
        /// Hashes the string's UTF-16 code units in ordinal order.
        /// String.GetHashCode is intentionally not used because its result is
        /// not a persistence contract across runtimes or processes.
        /// </summary>
        public static ulong HashString(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var hash = FnvOffsetBasis64;
            for (var index = 0; index < value.Length; index++)
            {
                hash ^= value[index];
                hash *= FnvPrime64;
            }

            return hash;
        }
    }
}
