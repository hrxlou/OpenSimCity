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
        public RoadDefinition(StableId id, int buildCost)
            : this(id, buildCost, string.Empty)
        {
        }

        public RoadDefinition(StableId id, int buildCost, string presentationKey)
        {
            if (!id.IsValid)
                throw new ArgumentException("A road definition must have a valid stable ID.", nameof(id));
            if (buildCost < 0)
                throw new ArgumentOutOfRangeException(nameof(buildCost), "Road build cost cannot be negative.");

            Id = id;
            BuildCost = buildCost;
            PresentationKey = presentationKey ?? string.Empty;
        }

        public RoadDefinition(ulong id, int buildCost, string presentationKey = null)
            : this(new StableId(id), buildCost, presentationKey)
        {
        }

        public StableId Id { get; }

        public StableId DefinitionId => Id;

        public int BuildCost { get; }

        public int ConstructionCost => BuildCost;

        public string PresentationKey { get; }

        public bool Equals(RoadDefinition other)
        {
            return other != null && Id == other.Id && BuildCost == other.BuildCost &&
                   string.Equals(PresentationKey, other.PresentationKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as RoadDefinition);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Id.GetHashCode() * 397) ^ BuildCost) * 397 ^
                       StringComparer.Ordinal.GetHashCode(PresentationKey);
            }
        }

        public override string ToString() => $"{Id} [buildCost={BuildCost}, key={PresentationKey}]";
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
