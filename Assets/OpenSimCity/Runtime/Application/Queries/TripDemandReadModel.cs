using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Traffic;

namespace OpenSimCity.Application.Queries
{
    /// <summary>
    /// Immutable application-facing OD row. Chunk coordinates are derived from the
    /// endpoint road cells and remain read-only diagnostics; no route or vehicle is
    /// materialized by this model.
    /// </summary>
    [Serializable]
    public readonly struct TripDemandRowView
    {
        public TripDemandRowView(TripDemandRow source, int chunkSize)
        {
            Kind = source.Kind;
            OriginKind = source.Origin.Kind;
            OriginId = source.Origin.Id;
            OriginRoadCell = source.Origin.RoadCell;
            OriginChunk = source.Origin.Kind == TripEndpointKind.None
                ? ChunkCoord.Zero
                : source.Origin.RoadCell.ToChunkCoord(chunkSize);
            OriginComponentId = source.Origin.ComponentId;
            DestinationKind = source.Destination.Kind;
            DestinationId = source.Destination.Id;
            DestinationRoadCell = source.Destination.RoadCell;
            DestinationChunk = source.Destination.Kind == TripEndpointKind.None
                ? ChunkCoord.Zero
                : source.Destination.RoadCell.ToChunkCoord(chunkSize);
            DestinationComponentId = source.Destination.ComponentId;
            Amount = source.Amount;
            Status = source.Status;
            UnmetReason = source.UnmetReason;
        }

        public TripDemandKind Kind { get; }
        public TripEndpointKind OriginKind { get; }
        public StableId OriginId { get; }
        public CellCoord OriginRoadCell { get; }
        public ChunkCoord OriginChunk { get; }
        public int OriginComponentId { get; }
        public TripEndpointKind DestinationKind { get; }
        public StableId DestinationId { get; }
        public CellCoord DestinationRoadCell { get; }
        public ChunkCoord DestinationChunk { get; }
        public int DestinationComponentId { get; }
        public int Amount { get; }
        public TripDemandStatus Status { get; }
        public TripDemandUnmetReason UnmetReason { get; }
    }

    /// <summary>
    /// Versioned, allocation-free-on-read projection for debug tables, overlays and
    /// later traffic presentation. It copies Simulation rows only when the monthly
    /// state version changes.
    /// </summary>
    public sealed class TripDemandReadModel
    {
        public const int DefaultChunkSize = 16;

        private readonly TripDemandSystem _system;
        private readonly int _chunkSize;
        private TripDemandRowView[] _rows = Array.Empty<TripDemandRowView>();
        private IReadOnlyList<TripDemandRowView> _readOnlyRows =
            Array.AsReadOnly(Array.Empty<TripDemandRowView>());
        private int _sourceVersion = -1;

        public TripDemandReadModel(TripDemandSystem system, int chunkSize = DefaultChunkSize)
        {
            _system = system ?? throw new ArgumentNullException(nameof(system));
            if (chunkSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(chunkSize));
            _chunkSize = chunkSize;
            RefreshIfNeeded();
        }

        public int Version { get; private set; }
        public int ChunkSize => _chunkSize;
        public IReadOnlyList<TripDemandRowView> Rows
        {
            get
            {
                RefreshIfNeeded();
                return _readOnlyRows;
            }
        }

        public bool HasProcessedMonth
        {
            get
            {
                RefreshIfNeeded();
                return _system.State.HasProcessedMonth;
            }
        }

        public SimulationDate LastProcessedMonth
        {
            get
            {
                RefreshIfNeeded();
                return _system.State.LastProcessedMonth;
            }
        }

        public int CommuteRequested { get { RefreshIfNeeded(); return _system.State.CommuteRequested; } }
        public int CommuteMatched { get { RefreshIfNeeded(); return _system.State.CommuteMatched; } }
        public int CommuteUnmet { get { RefreshIfNeeded(); return _system.State.CommuteUnmet; } }
        public int GoodsRequested { get { RefreshIfNeeded(); return _system.State.GoodsRequested; } }
        public int GoodsMatched { get { RefreshIfNeeded(); return _system.State.GoodsMatched; } }
        public int GoodsUnmet { get { RefreshIfNeeded(); return _system.State.GoodsUnmet; } }
        public ulong Checksum { get { RefreshIfNeeded(); return _system.State.ComputeChecksum(); } }

        public bool RefreshIfNeeded()
        {
            var sourceVersion = _system.State.Version;
            if (_sourceVersion == sourceVersion)
                return false;

            var source = _system.State.Rows;
            _rows = new TripDemandRowView[source.Count];
            for (var index = 0; index < source.Count; index++)
                _rows[index] = new TripDemandRowView(source[index], _chunkSize);
            _readOnlyRows = Array.AsReadOnly(_rows);
            _sourceVersion = sourceVersion;
            Version = checked(Version + 1);
            return true;
        }
    }
}
