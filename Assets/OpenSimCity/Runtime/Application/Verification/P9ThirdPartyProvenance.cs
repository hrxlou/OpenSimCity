using System;
using System.Collections.Generic;

namespace OpenSimCity.Application.Verification
{
    [Serializable]
    public sealed class P9ThirdPartyPackRecord
    {
        public string AssetPack = string.Empty;
        public string Publisher = string.Empty;
        public string SourceUrl = string.Empty;
        public string DownloadPageUrl = string.Empty;
        public string AcquiredUtc = string.Empty;
        public string ArchiveSha256 = string.Empty;
        public string LicenseSpdx = string.Empty;
        public string LicenseRepositoryPath = string.Empty;
        public string LicenseSha256 = string.Empty;
        public string UnitContract = string.Empty;
        public string AxisContract = string.Empty;
        public string PivotContract = string.Empty;
        public string TransformationContract = string.Empty;
    }

    [Serializable]
    public sealed class P9ThirdPartyAssetRecord
    {
        public string AssetId = string.Empty;
        public string WrapperKey = string.Empty;
        public string SourcePath = string.Empty;
        public string SourceSha256 = string.Empty;
        public string NormalizedPath = string.Empty;
        public string NormalizedSha256 = string.Empty;
    }

    public static class P9ThirdPartyProvenance
    {
        public const string VendorRoot = "Assets/ThirdParty/Quaternius/DowntownCityMegaKit/Standard";
        public const string ProvenanceRoot = VendorRoot + "/Provenance";
        public const string AcquisitionManifestPath = ProvenanceRoot + "/acquisition-manifest.json";
        public const string NormalizationReportPath = ProvenanceRoot + "/normalization-report.json";
        public const string ValidationReportPath = ProvenanceRoot + "/validation-report.json";
        public const string WrapperRoot = "Assets/OpenSimCity/Content/Prefabs/RepresentativeArt";

        public static P9ThirdPartyPackRecord Pack() => new P9ThirdPartyPackRecord
        {
            AssetPack = "Downtown City MegaKit",
            Publisher = "Quaternius",
            SourceUrl = "https://quaternius.com/packs/downtowncitymegakit.html",
            DownloadPageUrl = "https://quaternius.itch.io/downtown-city-megakit",
            AcquiredUtc = "2026-07-23T09:10:53Z",
            ArchiveSha256 = "5b1a945576d54cdbb4ccc9c3d52711e6d530da74c74c586407ecc28b165335da",
            LicenseSpdx = "CC0-1.0",
            LicenseRepositoryPath = ProvenanceRoot + "/LICENSE_CC0_1.0.txt",
            LicenseSha256 = "afac5819301671301c41e90402d26e4ade5e459ad58fdf77f142147410862f1f",
            UnitContract = "1 Blender unit = 1 meter = 1 Unity unit",
            AxisContract = "Blender Z-up exported as Unity Y-up (-Z forward, Y up)",
            PivotContract = "bottom-center",
            TransformationContract = "axis/scale/pivot normalization plus basename texture relink"
        };

        public static IReadOnlyList<P9ThirdPartyAssetRecord> Assets() => Array.AsReadOnly(new[]
        {
            Asset("osc.benchmark.megakit.building.small", "OSC_REP_BLD_SMALL", "Building_Small_1.fbx", "b480fb44f735e44bfc2988743a87a176c4819f880cf557885f916066f0fbfed7", "OSC_REP_BLD_SMALL.fbx", "96a945606aad56399bbef3a78fc2ac77190cb80d52a2694b4006055f5c01d5b2"),
            Asset("osc.benchmark.megakit.building.medium", "OSC_REP_BLD_MEDIUM", "Building_Medium_2_001.fbx", "d8b63f79dc675f4cf5bcccc83278e25b6155f800fb123ae1187d79b38d468c22", "OSC_REP_BLD_MEDIUM.fbx", "7614c70a9eedcb7e8d9042bbaae5f95fdf25759114db42d6058bd76d9176287b"),
            Asset("osc.benchmark.megakit.building.large", "OSC_REP_BLD_LARGE", "Building_Large_2.fbx", "a03432dc29d99ea378b6111f233b34d447041340dcfe5478a954e5a60b4d2a7c", "OSC_REP_BLD_LARGE.fbx", "078695cc0cafefb3f305dcdb7e31430461c569d480365b4577cab5761f15ae2a"),
            Asset("osc.benchmark.megakit.road.street-2lane", "OSC_REP_ROAD_2LANE", "Street_2Lane.fbx", "c2161af30c65a3001c309497fd0e1ccb83bcdf6f8e2695a771126ab639c92796", "OSC_REP_ROAD_2LANE.fbx", "f6bc52a21527baf20805de743bc4a70972c81350015625b75b98fb59c0339ccc"),
            Asset("osc.benchmark.megakit.facade.brick-plain", "OSC_REP_FACADE_BRICK", "Brick_Plain_3.fbx", "0080309883ded3d6a180a6418b1cdf9743d06c320a4df6e5137a0836c88eb79c", "OSC_REP_FACADE_BRICK.fbx", "d072020d3e03612f3fe6b72f19d89d267d6f22a15a0f8382216eebd4e7b8d5c7"),
            Asset("osc.benchmark.megakit.prop.ac-unit", "OSC_REP_PROP_AC_UNIT", "Prop_ACUnit.fbx", "149251ed04869c6835c44e4db66d63da7e73afa8a375b1be9ea5f83ff74e6b6f", "OSC_REP_PROP_AC_UNIT.fbx", "ac643d356236e34d6897750fd125f0de7b0bd8cf4416e89f051154cce99785ee")
        });

        public static bool ValidateStaticManifest()
        {
            var pack = Pack();
            if (!IsSha256(pack.ArchiveSha256) || !IsSha256(pack.LicenseSha256) || pack.LicenseSpdx != "CC0-1.0") return false;
            var assetIds = new HashSet<string>(StringComparer.Ordinal);
            var wrappers = new HashSet<string>(StringComparer.Ordinal);
            var assets = Assets();
            for (var index = 0; index < assets.Count; index++)
            {
                var row = assets[index];
                if (string.IsNullOrWhiteSpace(row.AssetId) || string.IsNullOrWhiteSpace(row.WrapperKey) ||
                    string.IsNullOrWhiteSpace(row.SourcePath) || string.IsNullOrWhiteSpace(row.NormalizedPath) ||
                    !IsSha256(row.SourceSha256) || !IsSha256(row.NormalizedSha256) ||
                    !assetIds.Add(row.AssetId) || !wrappers.Add(row.WrapperKey)) return false;
            }
            var catalog = P9ProductionCatalog.Create();
            for (var index = 0; index < catalog.Count; index++)
                if (!wrappers.Contains(catalog[index].WrapperCandidateKey)) return false;
            return true;
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            for (var index = 0; index < value.Length; index++)
            {
                var c = value[index];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            }
            return true;
        }

        private static P9ThirdPartyAssetRecord Asset(string assetId, string wrapper, string sourceFile, string sourceHash, string normalizedFile, string normalizedHash) =>
            new P9ThirdPartyAssetRecord
            {
                AssetId = assetId,
                WrapperKey = wrapper,
                SourcePath = VendorRoot + "/Source/FBX/" + sourceFile,
                SourceSha256 = sourceHash,
                NormalizedPath = VendorRoot + "/Normalized/FBX/" + normalizedFile,
                NormalizedSha256 = normalizedHash
            };
    }
}
