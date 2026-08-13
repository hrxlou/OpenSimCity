using System;
using System.Collections.Generic;
using OpenSimCity.Application.Services;

namespace OpenSimCity.Application.Verification
{
    public enum P9ProductionArtRole : byte
    {
        ResidentialLow,
        CommercialLow,
        IndustrialLow,
        PublicService
    }

    [Flags]
    public enum P9ProductionArtVisualMarker : byte
    {
        None = 0,
        KoreanSignage = 1 << 0,
        RooftopEquipment = 1 << 1,
        GroundFloorFrontage = 1 << 2,
        StreetEdgeDetail = 1 << 3
    }

    public enum P9ArtApprovalStatus : byte
    {
        Unverified,
        Approved,
        Rejected
    }

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
        Passed,
        MissingRole,
        DuplicatePresentationKey,
        InvalidStableMapping,
        InvalidFootprint,
        PlaceholderFallbackAllowed,
        MissingKoreanizationRequirement,
        MissingVisualEvidence,
        VisualApprovalRejected
    }

    public sealed class P9ProductionArtGateResult
    {
        public P9ProductionArtGateResult(bool passed, P9ProductionArtGateReason reason, string message)
        {
            Passed = passed;
            Reason = reason;
            Message = message ?? string.Empty;
        }

        public bool Passed { get; }
        public P9ProductionArtGateReason Reason { get; }
        public string Message { get; }
    }

    /// <summary>
    /// P9-01A vertical-slice contract. P3-08 representative wrappers are reused as
    /// candidate source geometry only. Their presence does not imply visual approval.
    /// The Korean-city markers below are requirements to verify in Unity, not claims
    /// that the current prefabs already satisfy them.
    /// </summary>
    public static class P9ProductionArtVerticalSlice
    {
        private static readonly P9ProductionArtRole[] RequiredRoles =
        {
            P9ProductionArtRole.ResidentialLow,
            P9ProductionArtRole.CommercialLow,
            P9ProductionArtRole.IndustrialLow,
            P9ProductionArtRole.PublicService
        };

        public static IReadOnlyList<P9ProductionArtBindingSpec> Create()
        {
            return Array.AsReadOnly(new[]
            {
                Binding(
                    P9ProductionArtRole.ResidentialLow,
                    "building.residential.low.a",
                    "OSC_REP_BLD_SMALL",
                    "osc.benchmark.megakit.building.small",
                    "DowntownCityMegaKit/Standard/Building_Small_1.fbx",
                    2, 2,
                    P9ProductionArtVisualMarker.KoreanSignage |
                    P9ProductionArtVisualMarker.RooftopEquipment |
                    P9ProductionArtVisualMarker.GroundFloorFrontage),
                Binding(
                    P9ProductionArtRole.CommercialLow,
                    "building.commercial.low.a",
                    "OSC_REP_BLD_MEDIUM",
                    "osc.benchmark.megakit.building.medium",
                    "DowntownCityMegaKit/Standard/Building_Medium_2_001.fbx",
                    2, 2,
                    P9ProductionArtVisualMarker.KoreanSignage |
                    P9ProductionArtVisualMarker.RooftopEquipment |
                    P9ProductionArtVisualMarker.GroundFloorFrontage |
                    P9ProductionArtVisualMarker.StreetEdgeDetail),
                Binding(
                    P9ProductionArtRole.IndustrialLow,
                    "building.industrial.low.a",
                    "OSC_REP_BLD_LARGE",
                    "osc.benchmark.megakit.building.large",
                    "DowntownCityMegaKit/Standard/Building_Large_1.fbx",
                    2, 2,
                    P9ProductionArtVisualMarker.KoreanSignage |
                    P9ProductionArtVisualMarker.RooftopEquipment |
                    P9ProductionArtVisualMarker.StreetEdgeDetail),
                Binding(
                    P9ProductionArtRole.PublicService,
                    PublicServicePresentationKeys.Police,
                    "OSC_REP_BLD_MEDIUM",
                    "osc.benchmark.megakit.building.medium",
                    "DowntownCityMegaKit/Standard/Building_Medium_2_001.fbx",
                    1, 1,
                    P9ProductionArtVisualMarker.KoreanSignage |
                    P9ProductionArtVisualMarker.RooftopEquipment |
                    P9ProductionArtVisualMarker.GroundFloorFrontage)
            });
        }

        public static P9ProductionArtGateResult ValidateStaticContract(
            IReadOnlyList<P9ProductionArtBindingSpec> bindings)
        {
            if (bindings == null)
                throw new ArgumentNullException(nameof(bindings));

            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            var roles = new HashSet<P9ProductionArtRole>();
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                if (binding == null ||
                    string.IsNullOrWhiteSpace(binding.PresentationKey) ||
                    string.IsNullOrWhiteSpace(binding.WrapperPrefabKey) ||
                    string.IsNullOrWhiteSpace(binding.SourceAssetId) ||
                    string.IsNullOrWhiteSpace(binding.SourceProvenanceKey))
                    return Fail(P9ProductionArtGateReason.InvalidStableMapping,
                        "Every vertical-slice entry needs explicit stable presentation, wrapper, asset and provenance keys.");
                if (!seenKeys.Add(binding.PresentationKey))
                    return Fail(P9ProductionArtGateReason.DuplicatePresentationKey,
                        $"Duplicate production-art presentation key: {binding.PresentationKey}.");
                if (binding.FootprintWidth <= 0 || binding.FootprintHeight <= 0)
                    return Fail(P9ProductionArtGateReason.InvalidFootprint,
                        $"Invalid footprint for {binding.PresentationKey}.");
                if (!binding.PlaceholderFallbackForbidden)
                    return Fail(P9ProductionArtGateReason.PlaceholderFallbackAllowed,
                        $"Covered key {binding.PresentationKey} must not silently fall back to placeholder art.");
                var koreanization = binding.RequiredVisualMarkers &
                    (P9ProductionArtVisualMarker.KoreanSignage |
                     P9ProductionArtVisualMarker.RooftopEquipment |
                     P9ProductionArtVisualMarker.GroundFloorFrontage |
                     P9ProductionArtVisualMarker.StreetEdgeDetail);
                if (koreanization == P9ProductionArtVisualMarker.None)
                    return Fail(P9ProductionArtGateReason.MissingKoreanizationRequirement,
                        $"{binding.PresentationKey} has no explicit Korean-city visual requirement.");
                roles.Add(binding.Role);
            }

            for (var index = 0; index < RequiredRoles.Length; index++)
                if (!roles.Contains(RequiredRoles[index]))
                    return Fail(P9ProductionArtGateReason.MissingRole,
                        $"The P9-01A vertical slice is missing {RequiredRoles[index]}.");

            return new P9ProductionArtGateResult(true, P9ProductionArtGateReason.Passed,
                "The static P9-01A production-art mapping contract is complete. Visual approval remains a separate gate.");
        }

        public static P9ProductionArtGateResult EvaluateVisualApproval(
            IReadOnlyList<P9ProductionArtBindingSpec> bindings,
            IReadOnlyList<P9ArtVisualApprovalEvidence> evidence)
        {
            var staticResult = ValidateStaticContract(bindings);
            if (!staticResult.Passed)
                return staticResult;
            if (evidence == null)
                throw new ArgumentNullException(nameof(evidence));

            for (var bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
            {
                var binding = bindings[bindingIndex];
                P9ArtVisualApprovalEvidence match = null;
                for (var evidenceIndex = 0; evidenceIndex < evidence.Count; evidenceIndex++)
                {
                    var candidate = evidence[evidenceIndex];
                    if (candidate != null &&
                        string.Equals(candidate.PresentationKey, binding.PresentationKey, StringComparison.Ordinal))
                    {
                        match = candidate;
                        break;
                    }
                }

                if (match == null || match.Status == P9ArtApprovalStatus.Unverified ||
                    string.IsNullOrWhiteSpace(match.Platform) ||
                    string.IsNullOrWhiteSpace(match.UnityVersion) ||
                    string.IsNullOrWhiteSpace(match.CaptureId) ||
                    string.IsNullOrWhiteSpace(match.Reviewer))
                    return Fail(P9ProductionArtGateReason.MissingVisualEvidence,
                        $"Visual/design approval is unverified for {binding.PresentationKey}.");
                if (match.Status == P9ArtApprovalStatus.Rejected)
                    return Fail(P9ProductionArtGateReason.VisualApprovalRejected,
                        $"Visual/design approval rejected {binding.PresentationKey}.");
            }

            return new P9ProductionArtGateResult(true, P9ProductionArtGateReason.Passed,
                "Every P9-01A vertical-slice entry has explicit visual/design approval evidence.");
        }

        private static P9ProductionArtBindingSpec Binding(
            P9ProductionArtRole role,
            string presentationKey,
            string wrapperPrefabKey,
            string sourceAssetId,
            string sourceProvenanceKey,
            int width,
            int height,
            P9ProductionArtVisualMarker requiredVisualMarkers)
        {
            return new P9ProductionArtBindingSpec
            {
                Role = role,
                PresentationKey = presentationKey,
                WrapperPrefabKey = wrapperPrefabKey,
                SourceAssetId = sourceAssetId,
                SourceProvenanceKey = sourceProvenanceKey,
                FootprintWidth = width,
                FootprintHeight = height,
                RequiredVisualMarkers = requiredVisualMarkers,
                PlaceholderFallbackForbidden = true
            };
        }

        private static P9ProductionArtGateResult Fail(P9ProductionArtGateReason reason, string message) =>
            new P9ProductionArtGateResult(false, reason, message);
    }
}
