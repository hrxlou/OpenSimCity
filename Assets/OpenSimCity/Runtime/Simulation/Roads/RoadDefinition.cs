using System;
using System.Collections.Generic;
using OpenSimCity.Core;

namespace OpenSimCity.Simulation.Roads
{
    /// <summary>
    /// Static, Unity-independent data describing one road type.
    /// Runtime occupancy stores only the stable definition ID.
    /// </summary>
    [Serializable]
    public sealed class RoadDefinition : IEquatable<RoadDefinition>
    {
        public const int DefaultCapacityPerMonth = 1000;
        public const int DefaultFreeFlowSpeedCellsPerHour = 30;

        public RoadDefinition(StableId id, int buildCost)
            : this(id, buildCost, string.Empty)
        {
        }

        public RoadDefinition(
            StableId id,
            int buildCost,
            string presentationKey,
            int monthlyMaintenanceCost = 0,
            int capacityPerMonth = DefaultCapacityPerMonth,
            int freeFlowSpeedCellsPerHour = DefaultFreeFlowSpeedCellsPerHour)
        {
            if (!id.IsValid)
                throw new ArgumentException("A road definition must have a valid stable ID.", nameof(id));
            if (buildCost < 0)
                throw new ArgumentOutOfRangeException(nameof(buildCost), "Road build cost cannot be negative.");
            if (monthlyMaintenanceCost < 0)
                throw new ArgumentOutOfRangeException(nameof(monthlyMaintenanceCost), "Road maintenance cannot be negative.");
            if (capacityPerMonth <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacityPerMonth), "Road capacity must be positive.");
            if (freeFlowSpeedCellsPerHour <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(freeFlowSpeedCellsPerHour),
                    "Road free-flow speed must be positive.");
            }

            Id = id;
            BuildCost = buildCost;
            MonthlyMaintenanceCost = monthlyMaintenanceCost;
            CapacityPerMonth = capacityPerMonth;
            FreeFlowSpeedCellsPerHour = freeFlowSpeedCellsPerHour;
            PresentationKey = presentationKey ?? string.Empty;
        }

        public RoadDefinition(
            ulong id,
            int buildCost,
            string presentationKey = null,
            int monthlyMaintenanceCost = 0,
            int capacityPerMonth = DefaultCapacityPerMonth,
            int freeFlowSpeedCellsPerHour = DefaultFreeFlowSpeedCellsPerHour)
            : this(
                new StableId(id),
                buildCost,
                presentationKey,
                monthlyMaintenanceCost,
                capacityPerMonth,
                freeFlowSpeedCellsPerHour)
        {
        }

        public StableId Id { get; }

        public StableId DefinitionId => Id;

        public int BuildCost { get; }

        public int ConstructionCost => BuildCost;

        public int MonthlyMaintenanceCost { get; }

        /// <summary>Aggregate trip units the road can carry in one simulation month.</summary>
        public int CapacityPerMonth { get; }

        /// <summary>Deterministic free-flow speed measured in road cells per hour.</summary>
        public int FreeFlowSpeedCellsPerHour { get; }

        public string PresentationKey { get; }

        public bool Equals(RoadDefinition other)
        {
            return other != null &&
                   Id == other.Id &&
                   BuildCost == other.BuildCost &&
                   MonthlyMaintenanceCost == other.MonthlyMaintenanceCost &&
                   CapacityPerMonth == other.CapacityPerMonth &&
                   FreeFlowSpeedCellsPerHour == other.FreeFlowSpeedCellsPerHour &&
                   string.Equals(PresentationKey, other.PresentationKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as RoadDefinition);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Id.GetHashCode();
                hashCode = (hashCode * 397) ^ BuildCost;
                hashCode = (hashCode * 397) ^ MonthlyMaintenanceCost;
                hashCode = (hashCode * 397) ^ CapacityPerMonth;
                hashCode = (hashCode * 397) ^ FreeFlowSpeedCellsPerHour;
                return (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(PresentationKey);
            }
        }

        public override string ToString() =>
            $"{Id} [buildCost={BuildCost}, maintenance={MonthlyMaintenanceCost}, capacity={CapacityPerMonth}, " +
            $"speed={FreeFlowSpeedCellsPerHour}, key={PresentationKey}]";
    }

    /// <summary>
    /// Stable lookup for static road definitions. Enumeration is exposed in ID
    /// order so diagnostic output never depends on dictionary iteration order.
    /// </summary>
    public sealed class RoadDefinitionCatalog
    {
        private readonly Dictionary<StableId, RoadDefinition> _byId;
        private readonly IReadOnlyList<RoadDefinition> _orderedDefinitions;

        public RoadDefinitionCatalog(IEnumerable<RoadDefinition> definitions)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));

            _byId = new Dictionary<StableId, RoadDefinition>();
            var ordered = new List<RoadDefinition>();
            foreach (var definition in definitions)
            {
                if (definition == null)
                    throw new ArgumentException("A road definition cannot be null.", nameof(definitions));
                if (_byId.ContainsKey(definition.Id))
                {
                    throw new ArgumentException(
                        $"Road definition {definition.Id} is registered more than once.",
                        nameof(definitions));
                }

                _byId.Add(definition.Id, definition);
                ordered.Add(definition);
            }

            ordered.Sort((left, right) => left.Id.CompareTo(right.Id));
            _orderedDefinitions = ordered.AsReadOnly();
        }

        public int Count => _byId.Count;

        public IReadOnlyList<RoadDefinition> Definitions => _orderedDefinitions;

        public bool TryGet(StableId id, out RoadDefinition definition)
        {
            return _byId.TryGetValue(id, out definition);
        }

        public RoadDefinition Get(StableId id)
        {
            if (!TryGet(id, out var definition))
            {
                throw new KeyNotFoundException($"Road definition {id} is not registered.");
            }

            return definition;
        }
    }
}
