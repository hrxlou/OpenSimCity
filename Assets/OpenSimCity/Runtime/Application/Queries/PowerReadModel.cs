using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Utilities;

namespace OpenSimCity.Application.Queries
{
    /// <summary>Versioned immutable projection consumed by overlays and later growth eligibility.</summary>
    public sealed class PowerReadModel
    {
        private readonly PowerNetwork _network;
        private IReadOnlyList<BuildingPowerResult> _buildings = Array.AsReadOnly(Array.Empty<BuildingPowerResult>());
        private IReadOnlyList<PowerProducerResult> _producers = Array.AsReadOnly(Array.Empty<PowerProducerResult>());
        private IReadOnlyList<PowerProducerViewData> _producerViews = Array.AsReadOnly(Array.Empty<PowerProducerViewData>());
        private int _sourceVersion = -1;
        public PowerReadModel(PowerNetwork network) { _network = network ?? throw new ArgumentNullException(nameof(network)); RefreshIfNeeded(); }
        public int Version { get { RefreshIfNeeded(); return _sourceVersion; } }
        public IReadOnlyList<BuildingPowerResult> Buildings { get { RefreshIfNeeded(); return _buildings; } }
        public IReadOnlyList<PowerProducerResult> Producers { get { RefreshIfNeeded(); return _producers; } }
        public IReadOnlyList<PowerProducerViewData> ProducerViews { get { RefreshIfNeeded(); return _producerViews; } }
        public bool RefreshIfNeeded()
        {
            _network.RefreshIfNeeded();
            if (_sourceVersion == _network.NetworkVersion) return false;
            var buildings = new BuildingPowerResult[_network.Buildings.Count];
            for (var i = 0; i < buildings.Length; i++) buildings[i] = _network.Buildings[i];
            var producers = new PowerProducerResult[_network.Producers.Count];
            var producerViews = new PowerProducerViewData[producers.Length];
            for (var i = 0; i < producers.Length; i++)
            {
                producers[i] = _network.Producers[i];
                producerViews[i] = new PowerProducerViewData(producers[i].ProducerId, producers[i].Cell,
                    producers[i].IsConnected, producers[i].Production);
            }
            _buildings = Array.AsReadOnly(buildings); _producers = Array.AsReadOnly(producers);
            _producerViews = Array.AsReadOnly(producerViews);
            _sourceVersion = _network.NetworkVersion; return true;
        }
        public bool TryGetBuilding(StableId id, out BuildingPowerResult result) => _network.TryGetBuilding(id, out result);
    }


    public readonly struct PowerProducerViewData
    {
        public PowerProducerViewData(StableId id, CellCoord cell, bool isConnected, int production)
        { Id = id; Cell = cell; IsConnected = isConnected; Production = production; }
        public StableId Id { get; }
        public CellCoord Cell { get; }
        public bool IsConnected { get; }
        public int Production { get; }
    }
}
