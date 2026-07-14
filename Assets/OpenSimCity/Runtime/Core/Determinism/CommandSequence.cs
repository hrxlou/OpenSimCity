using System;

namespace OpenSimCity.Core.Determinism
{
    [Serializable]
    public readonly struct CommandSequence : IEquatable<CommandSequence>, IComparable<CommandSequence>
    {
        public static readonly CommandSequence Zero = new CommandSequence(0);

        public CommandSequence(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public CommandSequence Next() => new CommandSequence(checked(Value + 1));

        public int CompareTo(CommandSequence other) => Value.CompareTo(other.Value);

        public bool Equals(CommandSequence other) => Value == other.Value;

        public override bool Equals(object obj) => obj is CommandSequence other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();

        public static bool operator ==(CommandSequence left, CommandSequence right) => left.Equals(right);

        public static bool operator !=(CommandSequence left, CommandSequence right) => !left.Equals(right);
    }
}
