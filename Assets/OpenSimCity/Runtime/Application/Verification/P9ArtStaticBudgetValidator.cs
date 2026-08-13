using System;
using System.Collections.Generic;

namespace OpenSimCity.Application.Verification
{
    public static class P9ArtStaticBudgetValidator
    {
        public static bool IsWithinBudget(string wrapperKey, int renderers, int materialSlots, int triangles, bool hasLodGroup)
        {
            if (string.IsNullOrWhiteSpace(wrapperKey) || renderers < 0 || materialSlots < 0 || triangles < 0)
                return false;
            var budgets = P9ArtBudgetManifest.Create();
            P9ArtAssetBudget budget = null;
            for (var index = 0; index < budgets.Count; index++)
                if (string.Equals(budgets[index].WrapperKey, wrapperKey, StringComparison.Ordinal))
                {
                    budget = budgets[index];
                    break;
                }
            if (budget == null || renderers > budget.MaximumRendererCount ||
                materialSlots > budget.MaximumMaterialSlots || triangles > budget.MaximumTrianglesLod0)
                return false;
            if (budget.LodDecision == P9LodDecision.NoLodApproved && hasLodGroup)
                return false;
            if (budget.LodDecision == P9LodDecision.LodRequired && !hasLodGroup)
                return false;
            return true;
        }
    }
}
