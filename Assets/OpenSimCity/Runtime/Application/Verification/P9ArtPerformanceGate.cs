using System;

namespace OpenSimCity.Application.Verification
{
    [Serializable]
    public sealed class P9ArtPerformanceEvidence
    {
        public string Platform = string.Empty;
        public string HardwareId = string.Empty;
        public string UnityVersion = string.Empty;
        public bool HasDrawCallMeasurement;
        public bool HasGpuFrameMeasurement;
        public bool HasTextureMemoryMeasurement;
        public bool PassedApprovedP8Budget;
    }

    public static class P9ArtPerformanceGate
    {
        public static bool IsApproved(P9ArtPerformanceEvidence evidence)
        {
            return evidence != null &&
                   !string.IsNullOrWhiteSpace(evidence.Platform) &&
                   !string.IsNullOrWhiteSpace(evidence.HardwareId) &&
                   !string.IsNullOrWhiteSpace(evidence.UnityVersion) &&
                   evidence.HasDrawCallMeasurement &&
                   evidence.HasGpuFrameMeasurement &&
                   evidence.HasTextureMemoryMeasurement &&
                   evidence.PassedApprovedP8Budget;
        }
    }
}
