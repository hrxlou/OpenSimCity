using System;
using System.Collections.Generic;

namespace OpenSimCity.Simulation.Economy
{
    /// <summary>
    /// Read-only access to the smoothed demand the growth system consumes. Keeping this
    /// a narrow port lets growth depend on the demand value without coupling to the whole
    /// economy pipeline, and keeps the RNG stream isolated from economy internals.
    /// </summary>
    public interface IRciDemandProvider
    {
        int SmoothedDemand(EconomicSector sector);
    }

    /// <summary>
    /// One signed diagnostic contribution to a sector's raw RCI pressure. The name
    /// is a stable diagnostic key (never a presentation string) so a HUD or
    /// inspector can attribute a demand change to the factor that caused it.
    /// </summary>
    [Serializable]
    public sealed class RciDemandFactor
    {
        public RciDemandFactor(string name, int contribution)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("A demand factor requires a name.", nameof(name));
            Name = name;
            Contribution = contribution;
        }

        public string Name { get; }
        public int Contribution { get; }

        public RciDemandFactorDto ToDto() => new RciDemandFactorDto { Name = Name, Contribution = Contribution };
    }

    /// <summary>
    /// Demand for a single sector this month. The pipeline is: sum the signed
    /// factor contributions into <see cref="RawPressure"/>, clamp it symmetrically
    /// into <see cref="ClampedPressure"/>, then move <see cref="SmoothedDemand"/>
    /// one integer smoothing step toward the clamped value. Smoothing is the demand
    /// value P3-05 growth consumes; raw pressure never develops directly.
    /// </summary>
    [Serializable]
    public sealed class RciSectorDemand
    {
        private RciDemandFactor[] _factors = Array.Empty<RciDemandFactor>();
        private IReadOnlyList<RciDemandFactor> _readOnlyFactors =
            Array.AsReadOnly(Array.Empty<RciDemandFactor>());

        public RciSectorDemand(EconomicSector sector)
        {
            Sector = sector;
        }

        public EconomicSector Sector { get; }
        public int RawPressure { get; private set; }
        public int ClampedPressure { get; private set; }
        public int SmoothedDemand { get; private set; }
        public IReadOnlyList<RciDemandFactor> Factors => _readOnlyFactors;

        internal void Set(
            RciDemandFactor[] factors,
            int rawPressure,
            int clampedPressure,
            int smoothedDemand)
        {
            _factors = factors ?? throw new ArgumentNullException(nameof(factors));
            _readOnlyFactors = Array.AsReadOnly(_factors);
            RawPressure = rawPressure;
            ClampedPressure = clampedPressure;
            SmoothedDemand = smoothedDemand;
        }

        public RciSectorDemandDto ToDto()
        {
            var rows = new RciDemandFactorDto[_factors.Length];
            for (var index = 0; index < _factors.Length; index++)
                rows[index] = _factors[index].ToDto();

            return new RciSectorDemandDto
            {
                Sector = (int)Sector,
                RawPressure = RawPressure,
                ClampedPressure = ClampedPressure,
                SmoothedDemand = SmoothedDemand,
                Factors = rows
            };
        }
    }

    /// <summary>
    /// Residential, commercial and industrial demand as pure simulation state,
    /// separate from any presentation colour or graph. The smoothed values persist
    /// across months so demand carries inertia and does not spike instantly.
    /// </summary>
    [Serializable]
    public sealed class RciDemandState : IRciDemandProvider
    {
        public RciDemandState()
        {
            Residential = new RciSectorDemand(EconomicSector.Residential);
            Commercial = new RciSectorDemand(EconomicSector.Commercial);
            Industrial = new RciSectorDemand(EconomicSector.Industrial);
        }

        public RciSectorDemand Residential { get; }
        public RciSectorDemand Commercial { get; }
        public RciSectorDemand Industrial { get; }

        public int SmoothedDemand(EconomicSector sector) => ForSector(sector).SmoothedDemand;

        public RciSectorDemand ForSector(EconomicSector sector)
        {
            switch (sector)
            {
                case EconomicSector.Residential:
                    return Residential;
                case EconomicSector.Commercial:
                    return Commercial;
                case EconomicSector.Industrial:
                    return Industrial;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sector));
            }
        }

        public RciDemandStateDto ToDto()
        {
            return new RciDemandStateDto
            {
                Residential = Residential.ToDto(),
                Commercial = Commercial.ToDto(),
                Industrial = Industrial.ToDto()
            };
        }
    }

    [Serializable]
    public sealed class RciDemandFactorDto
    {
        public string Name;
        public int Contribution;
    }

    [Serializable]
    public sealed class RciSectorDemandDto
    {
        public int Sector;
        public int RawPressure;
        public int ClampedPressure;
        public int SmoothedDemand;
        public RciDemandFactorDto[] Factors = Array.Empty<RciDemandFactorDto>();
    }

    [Serializable]
    public sealed class RciDemandStateDto
    {
        public RciSectorDemandDto Residential;
        public RciSectorDemandDto Commercial;
        public RciSectorDemandDto Industrial;
    }
}
