using System;

namespace OpenSimCity.Core
{
    [Serializable]
    public readonly struct StableId : IEquatable<StableId>, IComparable<StableId>
    {
        public static readonly StableId Invalid = default;

        public StableId(ulong value)
        {
            if (value == 0)
                throw new ArgumentOutOfRangeException(nameof(value), "A stable ID must be non-zero.");

            Value = value;
        }

        public ulong Value { get; }

        public bool IsValid => Value != 0;

        public int CompareTo(StableId other) => Value.CompareTo(other.Value);

        public bool Equals(StableId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is StableId other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (int)(Value ^ (Value >> 32));
            }
        }

        public override string ToString() => IsValid ? Value.ToString() : "Invalid";

        public static bool operator ==(StableId left, StableId right) => left.Equals(right);

        public static bool operator !=(StableId left, StableId right) => !left.Equals(right);

        public static bool operator <(StableId left, StableId right) => left.CompareTo(right) < 0;

        public static bool operator >(StableId left, StableId right) => left.CompareTo(right) > 0;

        public static bool operator <=(StableId left, StableId right) => left.CompareTo(right) <= 0;

        public static bool operator >=(StableId left, StableId right) => left.CompareTo(right) >= 0;
    }
}
