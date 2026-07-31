using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OpenSimCity.Application.Persistence;

namespace OpenSimCity.Tests.EditMode
{
    /// <summary>
    /// Locks the exact field declaration order used by JsonUtility and the binary
    /// envelope payload. Adding, removing, reordering, or changing a field requires
    /// an explicit format-version decision and migration before this checksum moves.
    /// </summary>
    public sealed class CitySnapshotSchemaContractTests
    {
        private const int ExpectedContractRowCount = 207;
        private const ulong ExpectedSchemaChecksum = 0x6F6AA666B4FBC6B3UL;

        [Test]
        public void SnapshotSchemaDeclarationOrderMatchesVersionTwoContract()
        {
            var actual = BuildContract(typeof(CitySnapshotDto));
            var checksum = ComputeChecksum(actual);

            TestContext.Out.WriteLine(
                $"P5-04A snapshot v2 schema: rows={actual.Count}, checksum={checksum:X16}");

            Assert.That(actual.Count, Is.EqualTo(ExpectedContractRowCount));
            Assert.That(actual, Does.Contain(
                "  TravelDirection:System.Int32"));
            Assert.That(actual, Does.Not.Contain(
                "  RouteCache:OpenSimCity.Simulation.Traffic.DeterministicRouteSet"));
            Assert.That(
                checksum,
                Is.EqualTo(ExpectedSchemaChecksum),
                $"Canonical snapshot v2 schema checksum is 0x{checksum:X16}UL.");
        }

        private static IReadOnlyList<string> BuildContract(Type root)
        {
            var rows = new List<string>();
            Walk(root, new HashSet<Type>(), rows);
            return rows;
        }

        private static void Walk(Type type, ISet<Type> visited, ICollection<string> rows)
        {
            if (IsScalar(type))
                return;
            if (type.IsArray)
            {
                Walk(type.GetElementType(), visited, rows);
                return;
            }
            if (!visited.Add(type))
                return;

            rows.Add(type.FullName);
            var fields = type.GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);
            for (var index = 0; index < fields.Length; index++)
            {
                if (fields[index].IsStatic)
                    continue;
                rows.Add($"  {fields[index].Name}:{TypeName(fields[index].FieldType)}");
            }
            for (var index = 0; index < fields.Length; index++)
            {
                if (fields[index].IsStatic)
                    continue;
                Walk(fields[index].FieldType, visited, rows);
            }
        }

        private static string TypeName(Type type)
        {
            if (type.IsArray)
                return TypeName(type.GetElementType()) + "[]";
            return type.FullName;
        }

        private static bool IsScalar(Type type)
        {
            return type == typeof(bool) ||
                   type == typeof(byte) ||
                   type == typeof(sbyte) ||
                   type == typeof(short) ||
                   type == typeof(ushort) ||
                   type == typeof(int) ||
                   type == typeof(uint) ||
                   type == typeof(long) ||
                   type == typeof(ulong) ||
                   type == typeof(float) ||
                   type == typeof(double) ||
                   type == typeof(string);
        }

        private static ulong ComputeChecksum(IEnumerable<string> rows)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            foreach (var row in rows)
            {
                for (var index = 0; index < row.Length; index++)
                {
                    var value = row[index];
                    hash ^= (byte)value;
                    hash *= prime;
                    hash ^= (byte)(value >> 8);
                    hash *= prime;
                }
                hash ^= (byte)'\n';
                hash *= prime;
            }
            return hash;
        }
    }
}
