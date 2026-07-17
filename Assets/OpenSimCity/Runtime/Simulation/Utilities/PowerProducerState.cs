using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.Utilities
{
    [Serializable]
    public sealed class PowerProducerState
    {
        public PowerProducerState(StableId id, StableId definitionId, CellCoord anchor, int currentProduction)
        {
            if (!id.IsValid)
                throw new ArgumentException("A producer requires a valid stable ID.", nameof(id));
            if (!definitionId.IsValid)
                throw new ArgumentException("A producer requires a valid definition ID.", nameof(definitionId));
            if (currentProduction < 0)
                throw new ArgumentOutOfRangeException(nameof(currentProduction));
            Id = id;
            DefinitionId = definitionId;
            Anchor = anchor;
            CurrentProduction = currentProduction;
        }

        public StableId Id { get; }
        public StableId DefinitionId { get; }
        public CellCoord Anchor { get; }
        public int CurrentProduction { get; }

        public PowerProducerStateDto ToDto() => new PowerProducerStateDto
        { Id = Id.Value, DefinitionId = DefinitionId.Value, AnchorX = Anchor.X, AnchorY = Anchor.Y, CurrentProduction = CurrentProduction };

        public static PowerProducerState FromDto(PowerProducerStateDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            return new PowerProducerState(new StableId(dto.Id), new StableId(dto.DefinitionId),
                new CellCoord(dto.AnchorX, dto.AnchorY), dto.CurrentProduction);
        }
    }

    [Serializable]
    public sealed class PowerProducerCollection
    {
        private readonly Dictionary<StableId, PowerProducerState> _byId = new Dictionary<StableId, PowerProducerState>();
        private readonly Dictionary<CellCoord, PowerProducerState> _byAnchor = new Dictionary<CellCoord, PowerProducerState>();
        private readonly List<PowerProducerState> _ordered = new List<PowerProducerState>();
        private readonly IReadOnlyList<PowerProducerState> _readOnly;

        public PowerProducerCollection() { _readOnly = _ordered.AsReadOnly(); }
        public int Count => _ordered.Count;
        public int Version { get; private set; }
        public IReadOnlyList<PowerProducerState> Producers => _readOnly;

        public void Add(PowerProducerState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (_byId.ContainsKey(state.Id)) throw new ArgumentException($"Producer {state.Id} already exists.", nameof(state));
            if (_byAnchor.ContainsKey(state.Anchor)) throw new ArgumentException($"A producer already occupies {state.Anchor}.", nameof(state));
            _byId.Add(state.Id, state);
            _byAnchor.Add(state.Anchor, state);
            var index = _ordered.BinarySearch(state, ProducerComparer.Instance);
            _ordered.Insert(index < 0 ? ~index : index, state);
            Version = checked(Version + 1);
        }

        public bool TryGet(StableId id, out PowerProducerState state) => _byId.TryGetValue(id, out state);
        public bool TryGetAt(CellCoord anchor, out PowerProducerState state) => _byAnchor.TryGetValue(anchor, out state);
        public bool Remove(StableId id)
        {
            if (!_byId.TryGetValue(id, out var state)) return false;
            _byId.Remove(id); _byAnchor.Remove(state.Anchor); _ordered.Remove(state);
            Version = checked(Version + 1); return true;
        }

        public PowerProducerCollectionDto ToDto()
        {
            var states = new PowerProducerStateDto[_ordered.Count];
            for (var i = 0; i < states.Length; i++) states[i] = _ordered[i].ToDto();
            return new PowerProducerCollectionDto { Producers = states };
        }

        public static PowerProducerCollection FromDto(PowerProducerCollectionDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            var collection = new PowerProducerCollection();
            foreach (var state in dto.Producers ?? Array.Empty<PowerProducerStateDto>())
                collection.Add(PowerProducerState.FromDto(state));
            return collection;
        }

        private sealed class ProducerComparer : IComparer<PowerProducerState>
        {
            public static readonly ProducerComparer Instance = new ProducerComparer();
            public int Compare(PowerProducerState left, PowerProducerState right) => left.Id.CompareTo(right.Id);
        }
    }

    [Serializable]
    public sealed class PowerProducerStateDto
    {
        public ulong Id;
        public ulong DefinitionId;
        public int AnchorX;
        public int AnchorY;
        public int CurrentProduction;
    }

    [Serializable]
    public sealed class PowerProducerCollectionDto
    {
        public PowerProducerStateDto[] Producers = Array.Empty<PowerProducerStateDto>();
    }
}
