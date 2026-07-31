using System;
using OpenSimCity.Simulation.Roads;

namespace OpenSimCity.Application.Persistence
{
    /// <summary>
    /// Version one stored only road definition IDs. Its exact historical meaning is
    /// therefore bidirectional travel on every occupied road cell.
    /// </summary>
    public sealed class CitySnapshotV1ToV2Migration : ICitySnapshotMigrationStep
    {
        private readonly ICitySnapshotPayloadCodec _codec;

        public CitySnapshotV1ToV2Migration(ICitySnapshotPayloadCodec codec)
        {
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        }

        public int FromVersion => 1;
        public int ToVersion => 2;

        public byte[] Migrate(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                throw new ArgumentException("A version-one snapshot payload is required.", nameof(payload));

            var dto = _codec.Decode(payload);
            if (dto?.Header == null)
                throw new CitySnapshotMigrationException(1, 2, "Snapshot header is missing.");
            if (!string.Equals(dto.Header.Magic, CitySnapshotFormat.Magic, StringComparison.Ordinal))
                throw new CitySnapshotMigrationException(1, 2, "Snapshot magic is invalid.");
            if (dto.Header.FormatVersion != FromVersion)
            {
                throw new CitySnapshotMigrationException(
                    FromVersion,
                    ToVersion,
                    $"Migration received version {dto.Header.FormatVersion}.");
            }

            var roads = dto.Roads?.Cells ?? Array.Empty<RoadCellSnapshotDto>();
            for (var index = 0; index < roads.Length; index++)
            {
                if (roads[index] == null)
                {
                    throw new CitySnapshotMigrationException(
                        FromVersion,
                        ToVersion,
                        $"Roads.Cells[{index}] is missing.");
                }
                roads[index].TravelDirection = (int)RoadTravelDirection.Bidirectional;
            }

            dto.Header.FormatVersion = ToVersion;
            return _codec.Encode(dto);
        }
    }
}
