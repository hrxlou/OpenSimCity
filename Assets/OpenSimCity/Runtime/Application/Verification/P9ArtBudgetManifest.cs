using System;
using System.Collections.Generic;

namespace OpenSimCity.Application.Verification
{
    public static class P9ArtBudgetManifest
    {
        public static IReadOnlyList<P9ArtAssetBudget> Create() => Array.AsReadOnly(new[]
        {
            Budget("OSC_REP_BLD_SMALL", "megakit.building.shared", 1, 13, 18152),
            Budget("OSC_REP_BLD_MEDIUM", "megakit.building.shared", 1, 12, 25612),
            Budget("OSC_REP_BLD_LARGE", "megakit.building.shared", 1, 12, 44786),
            Budget("OSC_REP_ROAD_2LANE", "megakit.road.shared", 1, 3, 220),
            Budget("OSC_REP_FACADE_BRICK", "megakit.facade.shared", 1, 2, 6),
            Budget("OSC_REP_PROP_AC_UNIT", "megakit.prop.shared", 1, 1, 136)
        });

        public static bool CoversProductionCatalog(IReadOnlyList<P9ProductionCatalogEntry> catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var budgets = Create();
            for (var index = 0; index < budgets.Count; index++) keys.Add(budgets[index].WrapperKey);
            for (var index = 0; index < catalog.Count; index++)
                if (!keys.Contains(catalog[index].WrapperCandidateKey)) return false;
            return true;
        }

        private static P9ArtAssetBudget Budget(string wrapper, string materialPolicy, int renderers, int slots, int triangles) =>
            new P9ArtAssetBudget
            {
                WrapperKey = wrapper,
                SharedMaterialPolicyKey = materialPolicy,
                MaximumRendererCount = renderers,
                MaximumMaterialSlots = slots,
                MaximumTrianglesLod0 = triangles,
                LodDecision = P9LodDecision.PendingMeasuredNeed,
                HasMeasuredTextureMemoryBudget = false,
                MaximumTextureMemoryBytes = 0
            };
    }
}
