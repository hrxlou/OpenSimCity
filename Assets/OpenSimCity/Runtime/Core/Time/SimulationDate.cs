using System;

namespace OpenSimCity.Core.Time
{
    [Serializable]
    public readonly struct SimulationDate : IEquatable<SimulationDate>, IComparable<SimulationDate>
    {
        public SimulationDate(int year, int month)
        {
            if (year < 1)
                throw new ArgumentOutOfRangeException(nameof(year));
            if (month < 1 || month > 12)
                throw new ArgumentOutOfRangeException(nameof(month));

            Year = year;
            Month = month;
        }

        public int Year { get; }

        public int Month { get; }

        public SimulationDate AddMonths(int months)
        {
            var absoluteMonth = checked(((long)Year - 1L) * 12L + Month - 1L + months);
            if (absoluteMonth < 0 || absoluteMonth > (long)int.MaxValue * 12L - 1L)
                throw new ArgumentOutOfRangeException(nameof(months));

            return new SimulationDate((int)(absoluteMonth / 12L) + 1, (int)(absoluteMonth % 12L) + 1);
        }

        public int CompareTo(SimulationDate other)
        {
            var yearComparison = Year.CompareTo(other.Year);
            return yearComparison != 0 ? yearComparison : Month.CompareTo(other.Month);
        }

        public bool Equals(SimulationDate other) => Year == other.Year && Month == other.Month;

        public override bool Equals(object obj) => obj is SimulationDate other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Year * 397) ^ Month;
            }
        }

        public override string ToString() => $"{Year:D4}-{Month:D2}";

        public static bool operator ==(SimulationDate left, SimulationDate right) => left.Equals(right);

        public static bool operator !=(SimulationDate left, SimulationDate right) => !left.Equals(right);
    }
}
