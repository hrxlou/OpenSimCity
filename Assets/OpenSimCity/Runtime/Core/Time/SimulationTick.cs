using System;

namespace OpenSimCity.Core.Time
{
    [Serializable]
    public readonly struct SimulationTick : IEquatable<SimulationTick>, IComparable<SimulationTick>
    {
        public static readonly SimulationTick Zero = new SimulationTick(0);

        public SimulationTick(long value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "A simulation tick cannot be negative.");

            Value = value;
        }

        public long Value { get; }

        public SimulationTick Next() => new SimulationTick(checked(Value + 1));

        public int CompareTo(SimulationTick other) => Value.CompareTo(other.Value);

        public bool Equals(SimulationTick other) => Value == other.Value;

        public override bool Equals(object obj) => obj is SimulationTick other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();

        public static bool operator ==(SimulationTick left, SimulationTick right) => left.Equals(right);

        public static bool operator !=(SimulationTick left, SimulationTick right) => !left.Equals(right);
    }
}
