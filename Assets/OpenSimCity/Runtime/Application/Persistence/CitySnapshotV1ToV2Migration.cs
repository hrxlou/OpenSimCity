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

        public int SourceVersion => 1;
        public int TargetVersion => 2;

        public SnapshotMigrationStepResult Migrate(byte[] sourcePayload)
        {
            if (sourcePayload == null || sourcePayload.Length == 0)
                return SnapshotMigrationStepResult.Failure();

            SnapshotPayloadDecodeResult decodeResult;
            try
            {
                decodeResult = _codec.Decode((byte[])sourcePayload.Clone());
            }
            catch (Exception)
            {
                return SnapshotMigrationStepResult.Failure();
            }

            if (decodeResult == null ||
                !decodeResult.IsSuccess ||
                decodeResult.Snapshot?.Header == null)
            {
                return SnapshotMigrationStepResult.Failure();
            }

            var snapshot = decodeResult.Snapshot;
            if (!string.Equals(snapshot.Header.Magic, CitySnapshotFormat.Magic, StringComparison.Ordinal) ||
                snapshot.Header.FormatVersion != SourceVersion)
            {
                return SnapshotMigrationStepResult.Failure();
            }

            var roads = snapshot.Roads?.Cells ?? Array.Empty<RoadCellSnapshotDto>();
            for (var index = 0; index < roads.Length; index++)
            {
                if (roads[index] == null)
                    return SnapshotMigrationStepResult.Failure();

                roads[index].TravelDirection = (int)RoadTravelDirection.Bidirectional;
            }

            snapshot.Header.FormatVersion = TargetVersion;

            SnapshotPayloadEncodeResult encodeResult;
            try
            {
                encodeResult = _codec.Encode(snapshot);
            }
            catch (Exception)
            {
                return SnapshotMigrationStepResult.Failure();
            }

            if (encodeResult == null || !encodeResult.IsSuccess || encodeResult.Payload == null)
                return SnapshotMigrationStepResult.Failure();

            return SnapshotMigrationStepResult.Success(encodeResult.Payload);
        }
    }
}
