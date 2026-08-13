using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using OpenSimCity.Application.Verification;
using UnityEditor;
using UnityEngine;

namespace OpenSimCity.Editor
{
    public static class P9ProductionArtProvenanceValidator
    {
        public static IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();
            if (!P9ThirdPartyProvenance.ValidateStaticManifest()) issues.Add("Typed provenance manifest is invalid.");
            RequireFile(P9ThirdPartyProvenance.AcquisitionManifestPath, issues);
            RequireFile(P9ThirdPartyProvenance.NormalizationReportPath, issues);
            RequireFile(P9ThirdPartyProvenance.ValidationReportPath, issues);
            var pack = P9ThirdPartyProvenance.Pack();
            RequireHash(pack.LicenseRepositoryPath, pack.LicenseSha256, issues);
            var assets = P9ThirdPartyProvenance.Assets();
            for (var index = 0; index < assets.Count; index++)
            {
                var row = assets[index];
                RequireHash(row.SourcePath, row.SourceSha256, issues);
                RequireHash(row.NormalizedPath, row.NormalizedSha256, issues);
                var wrapperPath = P9ThirdPartyProvenance.WrapperRoot + "/" + row.WrapperKey + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(wrapperPath) == null) issues.Add("Missing wrapper: " + wrapperPath);
            }
            return issues.AsReadOnly();
        }

        private static void RequireFile(string path, ICollection<string> issues)
        {
            if (!File.Exists(path)) issues.Add("Required file is missing: " + path);
        }

        private static void RequireHash(string path, string expected, ICollection<string> issues)
        {
            if (!File.Exists(path))
            {
                issues.Add("Required payload is missing or LFS is not hydrated: " + path);
                return;
            }
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            if (!string.Equals(actual, expected, StringComparison.Ordinal)) issues.Add("SHA-256 mismatch: " + path);
        }
    }
}
