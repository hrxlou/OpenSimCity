using System;
using OpenSimCity.Application.World;
using OpenSimCity.Simulation.Economy;

namespace OpenSimCity.Application.Bootstrap
{
    /// <summary>
    /// Finite, deterministic RCI pressure applied only when the Application creates
    /// a brand-new city. The values become ordinary <see cref="RciDemandState"/>
    /// immediately and therefore decay through the existing monthly integer
    /// smoothing; they are neither a permanent demand floor nor wall-clock based.
    /// </summary>
    [Serializable]
    public sealed class NewCityGrowthBootstrapParameters
    {
        public const int DefaultResidentialDemand = 64;
        public const int DefaultCommercialDemand = 64;
        public const int DefaultIndustrialDemand = 64;

        public NewCityGrowthBootstrapParameters(
            int residentialDemand = DefaultResidentialDemand,
            int commercialDemand = DefaultCommercialDemand,
            int industrialDemand = DefaultIndustrialDemand)
        {
            if (residentialDemand < 0)
                throw new ArgumentOutOfRangeException(nameof(residentialDemand));
            if (commercialDemand < 0)
                throw new ArgumentOutOfRangeException(nameof(commercialDemand));
            if (industrialDemand < 0)
                throw new ArgumentOutOfRangeException(nameof(industrialDemand));

            ResidentialDemand = residentialDemand;
            CommercialDemand = commercialDemand;
            IndustrialDemand = industrialDemand;
        }

        public int ResidentialDemand { get; }
        public int CommercialDemand { get; }
        public int IndustrialDemand { get; }

        public static NewCityGrowthBootstrapParameters Default =>
            new NewCityGrowthBootstrapParameters();

        public static NewCityGrowthBootstrapParameters Disabled =>
            new NewCityGrowthBootstrapParameters(0, 0, 0);
    }

    /// <summary>
    /// Explicit new-city creation seam. Snapshot restore intentionally constructs a
    /// bare <see cref="CityRuntime"/> and restores the saved RCI state, so this
    /// bootstrap cannot be applied a second time during load.
    /// </summary>
    public static class NewCityRuntimeFactory
    {
        public const string BootstrapFactorName = "NewCityBootstrap";

        public static CityRuntime Create(
            CityRuntimeOptions options = null,
            NewCityGrowthBootstrapParameters bootstrap = null)
        {
            var runtime = new CityRuntime(options ?? CityRuntimeOptions.Default);
            ApplyBootstrap(
                runtime.UrbanEconomySystem,
                bootstrap ?? NewCityGrowthBootstrapParameters.Default);
            runtime.RefreshReadModels();
            return runtime;
        }

        private static void ApplyBootstrap(
            UrbanEconomySystem economy,
            NewCityGrowthBootstrapParameters parameters)
        {
            if (economy == null)
                throw new ArgumentNullException(nameof(economy));
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            if (economy.HasProcessedMonth)
                throw new InvalidOperationException("New-city bootstrap requires an unprocessed economy.");
            if (HasExistingDemandState(economy.Rci))
                throw new InvalidOperationException("New-city bootstrap cannot replace existing RCI state.");

            ValidateWithinClamp(parameters.ResidentialDemand, economy.Parameters, nameof(parameters.ResidentialDemand));
            ValidateWithinClamp(parameters.CommercialDemand, economy.Parameters, nameof(parameters.CommercialDemand));
            ValidateWithinClamp(parameters.IndustrialDemand, economy.Parameters, nameof(parameters.IndustrialDemand));

            economy.Rci.RestoreFromDto(new RciDemandStateDto
            {
                Residential = CreateSector(EconomicSector.Residential, parameters.ResidentialDemand),
                Commercial = CreateSector(EconomicSector.Commercial, parameters.CommercialDemand),
                Industrial = CreateSector(EconomicSector.Industrial, parameters.IndustrialDemand)
            });
        }

        private static bool HasExistingDemandState(RciDemandState rci)
        {
            return HasExistingState(rci.Residential) ||
                   HasExistingState(rci.Commercial) ||
                   HasExistingState(rci.Industrial);
        }

        private static bool HasExistingState(RciSectorDemand sector)
        {
            return sector.RawPressure != 0 ||
                   sector.ClampedPressure != 0 ||
                   sector.SmoothedDemand != 0 ||
                   sector.Factors.Count != 0;
        }

        private static void ValidateWithinClamp(
            int demand,
            UrbanEconomyParameters economy,
            string name)
        {
            if (demand > economy.RciClampMagnitude)
            {
                throw new ArgumentOutOfRangeException(
                    name,
                    demand,
                    "New-city demand must fit inside the configured RCI clamp.");
            }
        }

        private static RciSectorDemandDto CreateSector(EconomicSector sector, int demand)
        {
            var factors = demand == 0
                ? Array.Empty<RciDemandFactorDto>()
                : new[]
                {
                    new RciDemandFactorDto
                    {
                        Name = BootstrapFactorName,
                        Contribution = demand
                    }
                };

            return new RciSectorDemandDto
            {
                Sector = (int)sector,
                RawPressure = demand,
                ClampedPressure = demand,
                SmoothedDemand = demand,
                Factors = factors
            };
        }
    }
}
