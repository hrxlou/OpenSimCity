using System;
using System.Collections.Generic;
using OpenSimCity.Application.Services;

namespace OpenSimCity.Application.Verification
{
    public enum P9ProductionArtRole : byte { ResidentialLow, CommercialLow, IndustrialLow, PublicService }

    [Flags]
    public enum P9ProductionArtVisualMarker : byte
    {
        None = 0,
        KoreanSignage = 1 << 0,
        RooftopEquipment = 1 << 1,
        GroundFloorFrontage = 1 << 2,
        StreetEdgeDetail = 1 << 3
    }

    public enum P9ArtApprovalStatus : byte { Unverified, Approved, Rejected }

    [Serializable]
    public sealed class P9ProductionArtBindingSpec
    {
        public P9ProductionArtRole Role;
        public string PresentationKey = string.Empty;
        public string WrapperPrefabKey = string.Empty;
        public string SourceAssetId = string.Empty;
        public string SourceProvenanceKey = string.Empty;
        public int FootprintWidth;
        public int FootprintHeight;
        public int SourceFootprintWidth;
        public int SourceFootprintHeight;
        public bool RequiresWrapperScaleNormalization;
        public P9ProductionArtVisualMarker RequiredVisualMarkers;
        public bool PlaceholderFallbackForbidden;
    }

    [Serializable]
    public sealed class P9ArtVisualApprovalEvidence
    {
        public string PresentationKey = string.Empty;
        public string Platform = string.Empty;
        public string UnityVersion = string.Empty;
        public string CaptureId = string.Empty;
        public string Reviewer = string.Empty;
        public P9ArtApprovalStatus Status;
    }

    public enum P9ProductionArtGateReason : byte
    {
        Passed, MissingRole, DuplicatePresentationKey, InvalidStableMapping, InvalidFootprint,
        UnnormalizedFootprint, PlaceholderFallbackAllowed, MissingKoreanizationRequirement,
        MissingVisualEvidence, VisualApprovalRejected
    }

    public sealed class P9ProductionArtGateResult
    {
        public P9ProductionArtGateResult(bool passed, P9ProductionArtGateReason reason, string message)
        { Passed = passed; Reason = reason; Message = message ?? string.Empty; }
        public bool Passed { get; }
        public P9ProductionArtGateReason Reason { get; }
        public string Message { get; }
    }

    /// <summary>
    /// Static P9-01A contract. P3-08 wrappers remain candidate source geometry only;
    /// visual marker flags are requirements to verify in Unity, not approval claims.
    /// </summary>
    public static class P9ProductionArtVerticalSlice
    {
        private static readonly P9ProductionArtRole[] RequiredRoles =
        {
            P9ProductionArtRole.ResidentialLow, P9ProductionArtRole.CommercialLow,
            P9ProductionArtRole.IndustrialLow, P9ProductionArtRole.PublicService
        };

        public static IReadOnlyList<P9ProductionArtBindingSpec> Create() => Array.AsReadOnly(new[]
        {
            Binding(P9ProductionArtRole.ResidentialLow, "building.residential.low.a",
                "OSC_REP_BLD_SMALL", "osc.benchmark.megakit.building.small",
                "DowntownCityMegaKit/Standard/Building_Small_1.fbx", 2, 2, 2, 2, false,
                P9ProductionArtVisualMarker.KoreanSignage | P9ProductionArtVisualMarker.RooftopEquipment |
                P9ProductionArtVisualMarker.GroundFloorFrontage),
            Binding(P9ProductionArtRole.CommercialLow, "building.commercial.low.a",
                "OSC_REP_BLD_MEDIUM", "osc.benchmark.megakit.building.medium",
                "DowntownCityMegaKit/Standard/Building_Medium_2_001.fbx", 2, 2, 2, 2, false,
                P9ProductionArtVisualMarker.KoreanSignage | P9ProductionArtVisualMarker.RooftopEquipment |
                P9ProductionArtVisualMarker.GroundFloorFrontage | P9ProductionArtVisualMarker.StreetEdgeDetail),
            Binding(P9ProductionArtRole.IndustrialLow, "building.industrial.low.a",
                "OSC_REP_BLD_MEDIUM", "osc.benchmark.megakit.building.medium",
                "DowntownCityMegaKit/Standard/Building_Medium_2_001.fbx", 2, 2, 2, 2, false,
                P9ProductionArtVisualMarker.KoreanSignage | P9ProductionArtVisualMarker.RooftopEquipment |
                P9ProductionArtVisualMarker.StreetEdgeDetail),
            Binding(P9ProductionArtRole.PublicService, PublicServicePresentationKeys.Police,
                "OSC_REP_BLD_MEDIUM", "osc.benchmark.megakit.building.medium",
                "DowntownCityMegaKit/Standard/Building_Medium_2_001.fbx", 1, 1, 2, 2, true,
                P9ProductionArtVisualMarker.KoreanSignage | P9ProductionArtVisualMarker.RooftopEquipment |
                P9ProductionArtVisualMarker.GroundFloorFrontage)
        });

        public static P9ProductionArtGateResult ValidateStaticContract(IReadOnlyList<P9ProductionArtBindingSpec> bindings)
        {
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var roles = new HashSet<P9ProductionArtRole>();
            for (var index = 0; index < bindings.Count; index++)
            {
                var row = bindings[index];
                if (row == null || string.IsNullOrWhiteSpace(row.PresentationKey) ||
                    string.IsNullOrWhiteSpace(row.WrapperPrefabKey) || string.IsNullOrWhiteSpace(row.SourceAssetId) ||
                    string.IsNullOrWhiteSpace(row.SourceProvenanceKey))
                    return Fail(P9ProductionArtGateReason.InvalidStableMapping, "Every entry needs stable presentation, wrapper, asset and provenance keys.");
                if (!keys.Add(row.PresentationKey))
                    return Fail(P9ProductionArtGateReason.DuplicatePresentationKey, $"Duplicate key: {row.PresentationKey}.");
                if (row.FootprintWidth <= 0 || row.FootprintHeight <= 0 || row.SourceFootprintWidth <= 0 || row.SourceFootprintHeight <= 0)
                    return Fail(P9ProductionArtGateReason.InvalidFootprint, $"Invalid footprint for {row.PresentationKey}.");
                if ((row.FootprintWidth != row.SourceFootprintWidth || row.FootprintHeight != row.SourceFootprintHeight) &&
                    !row.RequiresWrapperScaleNormalization)
                    return Fail(P9ProductionArtGateReason.UnnormalizedFootprint, $"{row.PresentationKey} needs explicit wrapper footprint normalization.");
                if (!row.PlaceholderFallbackForbidden)
                    return Fail(P9ProductionArtGateReason.PlaceholderFallbackAllowed, $"{row.PresentationKey} allows placeholder fallback.");
                if (row.RequiredVisualMarkers == P9ProductionArtVisualMarker.None)
                    return Fail(P9ProductionArtGateReason.MissingKoreanizationRequirement, $"{row.PresentationKey} has no Korean-city visual requirement.");
                roles.Add(row.Role);
            }
            for (var index = 0; index < RequiredRoles.Length; index++)
                if (!roles.Contains(RequiredRoles[index]))
                    return Fail(P9ProductionArtGateReason.MissingRole, $"Missing role: {RequiredRoles[index]}.");
            return new P9ProductionArtGateResult(true, P9ProductionArtGateReason.Passed,
                "Static P9-01A mapping is complete; visual approval remains separate.");
        }

        public static P9ProductionArtGateResult EvaluateVisualApproval(
            IReadOnlyList<P9ProductionArtBindingSpec> bindings, IReadOnlyList<P9ArtVisualApprovalEvidence> evidence)
        {
            var staticResult = ValidateStaticContract(bindings);
            if (!staticResult.Passed) return staticResult;
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));
            for (var bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
            {
                var binding = bindings[bindingIndex];
                P9ArtVisualApprovalEvidence match = null;
                for (var evidenceIndex = 0; evidenceIndex < evidence.Count; evidenceIndex++)
                {
                    var candidate = evidence[evidenceIndex];
                    if (candidate != null && string.Equals(candidate.PresentationKey, binding.PresentationKey, StringComparison.Ordinal))
                    { match = candidate; break; }
                }
                if (match == null || match.Status == P9ArtApprovalStatus.Unverified ||
                    string.IsNullOrWhiteSpace(match.Platform) || string.IsNullOrWhiteSpace(match.UnityVersion) ||
                    string.IsNullOrWhiteSpace(match.CaptureId) || string.IsNullOrWhiteSpace(match.Reviewer))
                    return Fail(P9ProductionArtGateReason.MissingVisualEvidence, $"Visual approval is unverified for {binding.PresentationKey}.");
                if (match.Status == P9ArtApprovalStatus.Rejected)
                    return Fail(P9ProductionArtGateReason.VisualApprovalRejected, $"Visual approval rejected {binding.PresentationKey}.");
            }
            return new P9ProductionArtGateResult(true, P9ProductionArtGateReason.Passed, "All vertical-slice visual approvals are recorded.");
        }

        private static P9ProductionArtBindingSpec Binding(
            P9ProductionArtRole role, string presentationKey, string wrapperPrefabKey, string sourceAssetId,
            string provenance, int width, int height, int sourceWidth, int sourceHeight, bool normalize,
            P9ProductionArtVisualMarker markers) => new P9ProductionArtBindingSpec
        {
            Role = role, PresentationKey = presentationKey, WrapperPrefabKey = wrapperPrefabKey,
            SourceAssetId = sourceAssetId, SourceProvenanceKey = provenance,
            FootprintWidth = width, FootprintHeight = height,
            SourceFootprintWidth = sourceWidth, SourceFootprintHeight = sourceHeight,
            RequiresWrapperScaleNormalization = normalize, RequiredVisualMarkers = markers,
            PlaceholderFallbackForbidden = true
        };

        private static P9ProductionArtGateResult Fail(P9ProductionArtGateReason reason, string message) =>
            new P9ProductionArtGateResult(false, reason, message);
    }
}
