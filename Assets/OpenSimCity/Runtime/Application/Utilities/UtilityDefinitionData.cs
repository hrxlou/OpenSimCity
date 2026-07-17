using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Simulation.Utilities;

namespace OpenSimCity.Application.Utilities
{
    [Serializable]
    public sealed class UtilityDefinitionData
    {
        public UtilityDefinitionData(StableId id, int powerCapacity, string presentationKey)
        {
            if (!id.IsValid) throw new ArgumentException("A utility definition requires a valid stable ID.", nameof(id));
            if (powerCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(powerCapacity));
            if (string.IsNullOrWhiteSpace(presentationKey)) throw new ArgumentException("A presentation key is required.", nameof(presentationKey));
            Id = id; PowerCapacity = powerCapacity; PresentationKey = presentationKey;
        }
        public StableId Id { get; }
        public int PowerCapacity { get; }
        public string PresentationKey { get; }
    }

    public sealed class UtilityDefinitionCatalog : IUtilityPowerProfileResolver
    {
        private readonly Dictionary<StableId, UtilityDefinitionData> _byId = new Dictionary<StableId, UtilityDefinitionData>();
        private readonly List<UtilityDefinitionData> _ordered = new List<UtilityDefinitionData>();
        private readonly IReadOnlyList<UtilityDefinitionData> _readOnly;
        public UtilityDefinitionCatalog(IEnumerable<UtilityDefinitionData> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            foreach (var definition in definitions)
            {
                if (definition == null) throw new ArgumentException("A utility definition cannot be null.", nameof(definitions));
                if (_byId.ContainsKey(definition.Id)) throw new ArgumentException($"Utility definition {definition.Id} is duplicated.", nameof(definitions));
                _byId.Add(definition.Id, definition); _ordered.Add(definition);
            }
            _ordered.Sort((left, right) => left.Id.CompareTo(right.Id)); _readOnly = _ordered.AsReadOnly();
        }
        public IReadOnlyList<UtilityDefinitionData> Definitions => _readOnly;
        public bool TryGet(StableId id, out UtilityDefinitionData definition) => _byId.TryGetValue(id, out definition);
        public UtilityDefinitionData Get(StableId id)
        {
            if (!TryGet(id, out var definition)) throw new KeyNotFoundException($"Utility definition {id} is not registered.");
            return definition;
        }
        public bool TryGetPowerProfile(StableId definitionId, out UtilityPowerProfile profile)
        {
            if (TryGet(definitionId, out var definition))
            { profile = new UtilityPowerProfile(definition.Id, definition.PowerCapacity); return true; }
            profile = default; return false;
        }
    }
}
