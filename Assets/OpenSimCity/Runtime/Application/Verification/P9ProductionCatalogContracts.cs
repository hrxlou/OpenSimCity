using System;
using System.Collections.Generic;
using OpenSimCity.Application.Services;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Simulation.Roads;

namespace OpenSimCity.Application.Verification
{
    [Serializable]
    public sealed class P9ProductionCatalogEntry
    {
        public string PresentationKey = string.Empty;
        public string WrapperCandidateKey = string.Empty;
        public string VariantFamilyKey = string.Empty;
        public string[] VariantKeys = Array.Empty<string>();
        public bool PlaceholderFallbackForbidden;
    }

    public enum P9ProductionCatalogGateReason : byte
    {
        Passed,
        MissingPresentationKey,
        UnexpectedPresentationKey,
        DuplicatePresentationKey,
        InvalidWrapperMapping,
        MissingVariant,
        DuplicateVariant,
        PlaceholderFallbackAllowed
    }

    public sealed class P9ProductionCatalogGateResult
    {
        public P9ProductionCatalogGateResult(bool passed, P9ProductionCatalogGateReason reason, string message)
        { Passed = passed; Reason = reason; Message = message ?? string.Empty; }
        public bool Passed { get; }
        public P9ProductionCatalogGateReason Reason { get; }
        public string Message { get; }
    }

    /// <summary>
    /// Derives the production-art coverage boundary from implemented gameplay
    /// definitions rather than maintaining a second hand-written gameplay list.
    /// </summary>
    public static class P9GameplayPresentationInventory
    {
        public static IReadOnlyList<string> Create()
        {
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            var options = CityRuntimeOptions.Default;
            var buildings = options.CreateBuildingDefinitions();
            for (var index = 0; index < buildings.Count; index++) keys.Add(buildings[index].PresentationKey);
            var roads = RoadHierarchyDefaults.CreateDefinitions();
            for (var index = 0; index < roads.Count; index++) keys.Add(roads[index].PresentationKey);
            var utilities = options.CreateUtilityDefinitions();
            for (var index = 0; index < utilities.Count; index++) keys.Add(utilities[index].PresentationKey);
            keys.Add(PublicServicePresentationKeys.Police);
            keys.Add(PublicServicePresentationKeys.Fire);
            keys.Add(PublicServicePresentationKeys.School);
            keys.Add(PublicServicePresentationKeys.Healthcare);
            keys.Add(PublicServicePresentationKeys.Waste);
            var result = new string[keys.Count];
            keys.CopyTo(result);
            return Array.AsReadOnly(result);
        }
    }

    /// <summary>
    /// Static P9 catalog mapping. Wrapper keys name OpenSimCity-owned wrapper families;
    /// variant keys are presentation-only palette/module choices. Actual Unity prefab,
    /// material and visual approval remain separate asset/evidence gates.
    /// </summary>
    public static class P9ProductionCatalog
    {
        public static IReadOnlyList<P9ProductionCatalogEntry> Create()
        {
            var entries = new List<P9ProductionCatalogEntry>();
            AddBuildingFamily(entries, "building.residential.low.", "OSC_REP_BLD_SMALL", "residential.low");
            AddBuildingFamily(entries, "building.commercial.low.", "OSC_REP_BLD_MEDIUM", "commercial.low");
            AddBuildingFamily(entries, "building.industrial.low.", "OSC_REP_BLD_MEDIUM", "industrial.low");
            Add(entries, "road.small", "OSC_REP_ROAD_2LANE", "road.small", "narrow", "narrow-marked");
            Add(entries, "road.street", "OSC_REP_ROAD_2LANE", "road.street", "standard", "standard-marked");
            Add(entries, "road.avenue", "OSC_REP_ROAD_2LANE", "road.avenue", "avenue", "avenue-transit-ready");
            Add(entries, "utility.power.plant.placeholder", "OSC_REP_PROP_AC_UNIT", "utility.power", "utility-default");
            AddService(entries, PublicServicePresentationKeys.Police, "police");
            AddService(entries, PublicServicePresentationKeys.Fire, "fire");
            AddService(entries, PublicServicePresentationKeys.School, "school");
            AddService(entries, PublicServicePresentationKeys.Healthcare, "healthcare");
            AddService(entries, PublicServicePresentationKeys.Waste, "waste");
            entries.Sort((left, right) => string.CompareOrdinal(left.PresentationKey, right.PresentationKey));
            return entries.AsReadOnly();
        }

        public static P9ProductionCatalogGateResult Validate(IReadOnlyList<P9ProductionCatalogEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            var expected = P9GameplayPresentationInventory.Create();
            var expectedSet = new HashSet<string>(expected, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < entries.Count; index++)
            {
                var row = entries[index];
                if (row == null || string.IsNullOrWhiteSpace(row.PresentationKey))
                    return Fail(P9ProductionCatalogGateReason.MissingPresentationKey, "Catalog entry has no presentation key.");
                if (!expectedSet.Contains(row.PresentationKey))
                    return Fail(P9ProductionCatalogGateReason.UnexpectedPresentationKey, $"Catalog contains non-gameplay key {row.PresentationKey}.");
                if (!seen.Add(row.PresentationKey))
                    return Fail(P9ProductionCatalogGateReason.DuplicatePresentationKey, $"Duplicate key {row.PresentationKey}.");
                if (string.IsNullOrWhiteSpace(row.WrapperCandidateKey) || string.IsNullOrWhiteSpace(row.VariantFamilyKey))
                    return Fail(P9ProductionCatalogGateReason.InvalidWrapperMapping, $"Invalid wrapper mapping for {row.PresentationKey}.");
                if (!row.PlaceholderFallbackForbidden)
                    return Fail(P9ProductionCatalogGateReason.PlaceholderFallbackAllowed, $"Covered key {row.PresentationKey} allows placeholder fallback.");
                var variants = row.VariantKeys ?? Array.Empty<string>();
                if (variants.Length == 0)
                    return Fail(P9ProductionCatalogGateReason.MissingVariant, $"No variants declared for {row.PresentationKey}.");
                var variantSet = new HashSet<string>(StringComparer.Ordinal);
                for (var variantIndex = 0; variantIndex < variants.Length; variantIndex++)
                    if (string.IsNullOrWhiteSpace(variants[variantIndex]) || !variantSet.Add(variants[variantIndex]))
                        return Fail(P9ProductionCatalogGateReason.DuplicateVariant, $"Invalid/duplicate variant for {row.PresentationKey}.");
            }
            for (var index = 0; index < expected.Count; index++)
                if (!seen.Contains(expected[index]))
                    return Fail(P9ProductionCatalogGateReason.MissingPresentationKey, $"Missing gameplay presentation key {expected[index]}.");
            return new P9ProductionCatalogGateResult(true, P9ProductionCatalogGateReason.Passed,
                "Every implemented gameplay presentation key has a stable P9 wrapper/variant mapping.");
        }

        public static P9ProductionCatalogEntry Get(string presentationKey)
        {
            var entries = Create();
            for (var index = 0; index < entries.Count; index++)
                if (string.Equals(entries[index].PresentationKey, presentationKey, StringComparison.Ordinal))
                    return entries[index];
            throw new KeyNotFoundException($"P9 production catalog has no entry for {presentationKey}.");
        }

        private static void AddBuildingFamily(List<P9ProductionCatalogEntry> entries, string prefix, string wrapper, string family)
        {
            Add(entries, prefix + "a", wrapper, family, family + ".warm", family + ".neutral", family + ".cool");
            Add(entries, prefix + "b", wrapper, family, family + ".warm", family + ".neutral", family + ".cool");
            Add(entries, prefix + "c", wrapper, family, family + ".warm", family + ".neutral", family + ".cool");
        }

        private static void AddService(List<P9ProductionCatalogEntry> entries, string key, string suffix) =>
            Add(entries, key, "OSC_REP_BLD_MEDIUM", "service." + suffix,
                "service." + suffix + ".default", "service." + suffix + ".compact");

        private static void Add(List<P9ProductionCatalogEntry> entries, string key, string wrapper, string family, params string[] variants)
        {
            entries.Add(new P9ProductionCatalogEntry
            {
                PresentationKey = key,
                WrapperCandidateKey = wrapper,
                VariantFamilyKey = family,
                VariantKeys = variants ?? Array.Empty<string>(),
                PlaceholderFallbackForbidden = true
            });
        }

        private static P9ProductionCatalogGateResult Fail(P9ProductionCatalogGateReason reason, string message) =>
            new P9ProductionCatalogGateResult(false, reason, message);
    }

    /// <summary>
    /// File/path-independent deterministic selector. It reads no RNG stream and has
    /// no mutable state, so presentation diversity cannot perturb Simulation.
    /// </summary>
    public static class P9StablePresentationVariantSelector
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static int SelectIndex(string presentationKey, StableId instanceId, int variantCount)
        {
            if (string.IsNullOrWhiteSpace(presentationKey)) throw new ArgumentException("Presentation key is required.", nameof(presentationKey));
            if (!instanceId.IsValid) throw new ArgumentException("A valid stable instance ID is required.", nameof(instanceId));
            if (variantCount <= 0) throw new ArgumentOutOfRangeException(nameof(variantCount));
            var hash = Offset;
            for (var index = 0; index < presentationKey.Length; index++)
            {
                hash ^= presentationKey[index];
                hash *= Prime;
            }
            var value = instanceId.Value;
            for (var offset = 0; offset < 8; offset++)
            {
                hash ^= (byte)(value >> (offset * 8));
                hash *= Prime;
            }
            return (int)(hash % (ulong)variantCount);
        }

        public static string SelectVariant(P9ProductionCatalogEntry entry, StableId instanceId)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            var variants = entry.VariantKeys ?? Array.Empty<string>();
            return variants[SelectIndex(entry.PresentationKey, instanceId, variants.Length)];
        }
    }
}
