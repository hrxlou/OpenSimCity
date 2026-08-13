using System;

namespace OpenSimCity.Application.Verification
{
    public enum P9LodDecision : byte { PendingMeasuredNeed, NoLodApproved, LodRequired }

    [Serializable]
    public sealed class P9ArtAssetBudget
    {
        public string WrapperKey = string.Empty;
        public string SharedMaterialPolicyKey = string.Empty;
        public int MaximumRendererCount;
        public int MaximumMaterialSlots;
        public int MaximumTrianglesLod0;
        public P9LodDecision LodDecision;
        public bool HasMeasuredTextureMemoryBudget;
        public long MaximumTextureMemoryBytes;
    }
}
