using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.Buildings
{
    /// <summary>
    /// Owns building runtime state and exposes it in stable building-ID order.
    /// Later growth systems can create/remove entries through this boundary
    /// without exposing a mutable backing collection to views.
    /// </summary>
    [Serializable]
    public sealed class BuildingStateCollection
    {
        private readonly Dictionary<StableId, BuildingState> _byId =
            new Dictionary<StableId, BuildingState>();
        private readonly List<BuildingState> _ordered = new List<BuildingState>();
        private readonly IReadOnlyList<BuildingState> _readOnlyOrdered;

        public BuildingStateCollection()
        {
            _readOnlyOrdered = _ordered.AsReadOnly();
        }

        public int Count => _ordered.Count;

        /// <summary>Increments once for each successful add or remove.</summary>
        public int Version { get; private set; }

        public IReadOnlyList<BuildingState> Buildings => _readOnlyOrdered;

        public BuildingState Create(
            StableId id,
            StableId definitionId,
            StableId parcelId,
            CellCoord anchor,
            int occupancy = 0,
            int filledJobs = 0)
        {
            var state = new BuildingState(id, definitionId, parcelId, anchor, occupancy, filledJobs);
            Add(state);
            return state;
        }

        public void Add(BuildingState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (_byId.ContainsKey(state.Id))
                throw new ArgumentException($"Building {state.Id} is already registered.", nameof(state));

            _byId.Add(state.Id, state);
            var insertionIndex = _ordered.BinarySearch(state, BuildingIdComparer.Instance);
            if (insertionIndex < 0)
                insertionIndex = ~insertionIndex;
            _ordered.Insert(insertionIndex, state);
            Version = checked(Version + 1);
        }

        public bool TryGet(StableId id, out BuildingState state) => _byId.TryGetValue(id, out state);

        public BuildingState Get(StableId id)
        {
            if (!TryGet(id, out var state))
                throw new KeyNotFoundException($"Building {id} is not registered.");
            return state;
        }

        public bool Remove(StableId id)
        {
            if (!_byId.TryGetValue(id, out var state))
                return false;

            _byId.Remove(id);
            _ordered.Remove(state);
            Version = checked(Version + 1);
            return true;
        }

        public void Replace(BuildingState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (!_byId.TryGetValue(state.Id, out var existing))
                throw new KeyNotFoundException($"Building {state.Id} is not registered.");

            _byId[state.Id] = state;
            var index = _ordered.IndexOf(existing);
            _ordered[index] = state;
            Version = checked(Version + 1);
        }

        public BuildingStateCollectionDto ToDto()
        {
            var buildings = new BuildingStateDto[_ordered.Count];
            for (var index = 0; index < _ordered.Count; index++)
                buildings[index] = _ordered[index].ToDto();
            return new BuildingStateCollectionDto { Buildings = buildings };
        }

        public static BuildingStateCollection FromDto(BuildingStateCollectionDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var collection = new BuildingStateCollection();
            var buildings = dto.Buildings ?? Array.Empty<BuildingStateDto>();
            for (var index = 0; index < buildings.Length; index++)
                collection.Add(BuildingState.FromDto(buildings[index]));
            return collection;
        }

        private sealed class BuildingIdComparer : IComparer<BuildingState>
        {
            public static readonly BuildingIdComparer Instance = new BuildingIdComparer();

            public int Compare(BuildingState left, BuildingState right) => left.Id.CompareTo(right.Id);
        }
    }
}
