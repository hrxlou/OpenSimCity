using System;

namespace OpenSimCity.Core.Determinism
{
    [Serializable]
    public struct DeterministicRandom
    {
        private const ulong DefaultNonZeroState = 0x9E3779B97F4A7C15UL;
        private const ulong StreamSeedSalt = 0xD1B54A32D192ED03UL;

        private ulong _state;

        public DeterministicRandom(ulong seed) : this(SeedToState(seed), true)
        {
        }

        public DeterministicRandom(ulong seed, string subsystemKey)
            : this(StreamSeedToState(seed, subsystemKey), true)
        {
        }

        private DeterministicRandom(ulong state, bool restoreState)
        {
            _state = state;
        }

        public ulong State => _state;

        public static DeterministicRandom CreateStream(ulong seed, string subsystemKey)
        {
            return new DeterministicRandom(seed, subsystemKey);
        }

        public static DeterministicRandom ForSubsystem(ulong seed, string subsystemKey)
        {
            return CreateStream(seed, subsystemKey);
        }

        public static DeterministicRandom Restore(ulong state)
        {
            if (state == 0)
                throw new ArgumentOutOfRangeException(nameof(state), "A saved RNG state must be non-zero.");

            return new DeterministicRandom(state, true);
        }

        public ulong NextUInt64()
        {
            if (_state == 0)
                throw new InvalidOperationException("Initialize the deterministic RNG with a seed before use.");

            var value = _state;
            value ^= value >> 12;
            value ^= value << 25;
            value ^= value >> 27;
            _state = value;
            return value * 2685821657736338717UL;
        }

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));

            var bound = (ulong)maxExclusive;
            var threshold = unchecked(0UL - bound) % bound;
            ulong value;
            do
            {
                value = NextUInt64();
            } while (value < threshold);

            return (int)(value % bound);
        }

        private static ulong Mix(ulong value)
        {
            value += DefaultNonZeroState;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }

        private static ulong SeedToState(ulong seed)
        {
            return EnsureNonZero(Mix(seed));
        }

        private static ulong StreamSeedToState(ulong seed, string subsystemKey)
        {
            if (subsystemKey == null)
                throw new ArgumentNullException(nameof(subsystemKey));
            if (subsystemKey.Length == 0)
                throw new ArgumentException("A subsystem key cannot be empty.", nameof(subsystemKey));

            var keyHash = DeterministicHash.HashString(subsystemKey);
            return EnsureNonZero(Mix(seed ^ keyHash ^ StreamSeedSalt));
        }

        private static ulong EnsureNonZero(ulong state)
        {
            return state == 0 ? DefaultNonZeroState : state;
        }
    }
}
