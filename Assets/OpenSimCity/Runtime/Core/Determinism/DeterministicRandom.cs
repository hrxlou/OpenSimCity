using System;

namespace OpenSimCity.Core.Determinism
{
    [Serializable]
    public struct DeterministicRandom
    {
        private const ulong DefaultNonZeroState = 0x9E3779B97F4A7C15UL;

        private ulong _state;

        public DeterministicRandom(ulong seed)
        {
            _state = Mix(seed);
            if (_state == 0)
                _state = DefaultNonZeroState;
        }

        private DeterministicRandom(ulong state, bool restoreState)
        {
            _state = state;
        }

        public ulong State => _state;

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
    }
}
