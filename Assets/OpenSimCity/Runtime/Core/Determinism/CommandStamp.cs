using System;
using OpenSimCity.Core.Time;

namespace OpenSimCity.Core.Determinism
{
    [Serializable]
    public readonly struct CommandStamp : IEquatable<CommandStamp>, IComparable<CommandStamp>
    {
        public CommandStamp(SimulationTick tick, CommandSequence sequence)
        {
            Tick = tick;
            Sequence = sequence;
        }

        public SimulationTick Tick { get; }

        public CommandSequence Sequence { get; }

        public int CompareTo(CommandStamp other)
        {
            var tickComparison = Tick.CompareTo(other.Tick);
            return tickComparison != 0 ? tickComparison : Sequence.CompareTo(other.Sequence);
        }

        public bool Equals(CommandStamp other) => Tick.Equals(other.Tick) && Sequence.Equals(other.Sequence);

        public override bool Equals(object obj) => obj is CommandStamp other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Tick.GetHashCode() * 397) ^ Sequence.GetHashCode();
            }
        }

        public override string ToString() => $"{Tick}:{Sequence}";

        public static bool operator ==(CommandStamp left, CommandStamp right) => left.Equals(right);

        public static bool operator !=(CommandStamp left, CommandStamp right) => !left.Equals(right);

        public static bool operator <(CommandStamp left, CommandStamp right) => left.CompareTo(right) < 0;

        public static bool operator >(CommandStamp left, CommandStamp right) => left.CompareTo(right) > 0;

        public static bool operator <=(CommandStamp left, CommandStamp right) => left.CompareTo(right) <= 0;

        public static bool operator >=(CommandStamp left, CommandStamp right) => left.CompareTo(right) >= 0;
    }
}
