using System;

namespace OpenSimCity.Core.Determinism
{
    [Serializable]
    public readonly struct CommandSequence : IEquatable<CommandSequence>, IComparable<CommandSequence>
    {
        public static readonly CommandSequence Zero = new CommandSequence(0);
        public static readonly CommandSequence MaxValue = new CommandSequence(ulong.MaxValue);

        public CommandSequence(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public bool CanAdvance => Value != ulong.MaxValue;

        public CommandSequence Next()
        {
            if (!CanAdvance)
                throw new OverflowException("Command sequence cannot advance past UInt64.MaxValue.");

            return new CommandSequence(Value + 1);
        }

        public int CompareTo(CommandSequence other) => Value.CompareTo(other.Value);

        public bool Equals(CommandSequence other) => Value == other.Value;

        public override bool Equals(object obj) => obj is CommandSequence other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (int)(Value ^ (Value >> 32));
            }
        }

        public override string ToString() => Value.ToString();

        public static bool operator ==(CommandSequence left, CommandSequence right) => left.Equals(right);

        public static bool operator !=(CommandSequence left, CommandSequence right) => !left.Equals(right);

        public static bool operator <(CommandSequence left, CommandSequence right) => left.CompareTo(right) < 0;

        public static bool operator >(CommandSequence left, CommandSequence right) => left.CompareTo(right) > 0;

        public static bool operator <=(CommandSequence left, CommandSequence right) => left.CompareTo(right) <= 0;

        public static bool operator >=(CommandSequence left, CommandSequence right) => left.CompareTo(right) >= 0;
    }
}
