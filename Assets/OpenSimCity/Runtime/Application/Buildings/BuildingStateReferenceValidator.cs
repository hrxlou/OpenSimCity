using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Simulation.Buildings;

namespace OpenSimCity.Application.Buildings
{
    public enum BuildingStateReferenceIssueKind
    {
        UnknownDefinition = 1,
        UnknownParcel = 2,
        OccupancyExceedsCapacity = 3,
        FilledJobsExceedCapacity = 4
    }

    public readonly struct BuildingStateReferenceIssue : IEquatable<BuildingStateReferenceIssue>
    {
        public BuildingStateReferenceIssue(
            BuildingStateReferenceIssueKind kind,
            StableId buildingId,
            StableId referencedId)
        {
            Kind = kind;
            BuildingId = buildingId;
            ReferencedId = referencedId;
        }

        public BuildingStateReferenceIssueKind Kind { get; }

        public StableId BuildingId { get; }

        public StableId ReferencedId { get; }

        public bool Equals(BuildingStateReferenceIssue other)
        {
            return Kind == other.Kind && BuildingId == other.BuildingId && ReferencedId == other.ReferencedId;
        }

        public override bool Equals(object obj) => obj is BuildingStateReferenceIssue other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397 ^ BuildingId.GetHashCode()) * 397 ^ ReferencedId.GetHashCode();
            }
        }
    }

    public static class BuildingStateReferenceValidator
    {
        public static IReadOnlyList<BuildingStateReferenceIssue> Validate(
            IEnumerable<BuildingState> states,
            BuildingDefinitionCatalog definitions,
            IEnumerable<StableId> parcelIds)
        {
            if (states == null)
                throw new ArgumentNullException(nameof(states));
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));
            if (parcelIds == null)
                throw new ArgumentNullException(nameof(parcelIds));

            var knownParcels = new HashSet<StableId>(parcelIds);
            var orderedStates = new List<BuildingState>();
            foreach (var state in states)
            {
                if (state == null)
                    throw new ArgumentException("A building state cannot be null.", nameof(states));
                orderedStates.Add(state);
            }

            orderedStates.Sort((left, right) => left.Id.CompareTo(right.Id));
            var issues = new List<BuildingStateReferenceIssue>();
            for (var index = 0; index < orderedStates.Count; index++)
            {
                var state = orderedStates[index];
                if (!definitions.TryResolve(state, out var definition))
                {
                    issues.Add(new BuildingStateReferenceIssue(
                        BuildingStateReferenceIssueKind.UnknownDefinition,
                        state.Id,
                        state.DefinitionId));
                }
                else
                {
                    if (state.Occupancy > definition.OccupancyCapacity)
                    {
                        issues.Add(new BuildingStateReferenceIssue(
                            BuildingStateReferenceIssueKind.OccupancyExceedsCapacity,
                            state.Id,
                            state.DefinitionId));
                    }

                    if (state.FilledJobs > definition.JobCapacity)
                    {
                        issues.Add(new BuildingStateReferenceIssue(
                            BuildingStateReferenceIssueKind.FilledJobsExceedCapacity,
                            state.Id,
                            state.DefinitionId));
                    }
                }

                if (!knownParcels.Contains(state.ParcelId))
                {
                    issues.Add(new BuildingStateReferenceIssue(
                        BuildingStateReferenceIssueKind.UnknownParcel,
                        state.Id,
                        state.ParcelId));
                }
            }

            return issues.AsReadOnly();
        }
    }
}
