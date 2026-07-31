using System;
using System.Collections.Generic;
using OpenSimCity.Application.World;

namespace OpenSimCity.Application.Persistence
{
    /// <summary>
    /// Application orchestration for save/load. It owns validation and error
    /// mapping while storage paths, file APIs, and JSON implementation remain
    /// behind ports.
    /// </summary>
    public sealed class CityPersistenceService
    {
        private readonly ICitySaveStorage _storage;
        private readonly ICitySnapshotPayloadCodec _codec;
        private readonly CitySnapshotMigrationDispatcher _migrationDispatcher;

        public CityPersistenceService(
            ICitySaveStorage storage,
            ICitySnapshotPayloadCodec codec,
            IEnumerable<ICitySnapshotMigrationStep> migrations = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _migrationDispatcher = new CitySnapshotMigrationDispatcher(
                codec,
                CitySnapshotFormat.CurrentVersion,
                migrations ?? CreateDefaultMigrations(codec));
        }

        public CitySaveResult Save(
            string saveKey,
            CityRuntime runtime,
            string gameVersion = CitySnapshotFormat.DevelopmentGameVersion,
            long createdUtcTicks = 0)
        {
            if (runtime == null)
                return new CitySaveResult(CityPersistenceError.InvalidSnapshot, false);

            CitySnapshotDto snapshot;
            try
            {
                snapshot = CitySnapshotSerializer.Capture(runtime, gameVersion, createdUtcTicks);
            }
            catch (Exception)
            {
                return new CitySaveResult(CityPersistenceError.InvalidSnapshot, false);
            }

            SnapshotPayloadEncodeResult encoding;
            try
            {
                encoding = _codec.Encode(snapshot);
            }
            catch (Exception)
            {
                return new CitySaveResult(CityPersistenceError.EncodingFailed, false);
            }

            if (encoding == null || !encoding.IsSuccess || encoding.Payload == null)
                return new CitySaveResult(CityPersistenceError.EncodingFailed, false);

            // A codec must be able to read its own output before the storage port is
            // allowed to replace a known-good save.
            var preflight = _migrationDispatcher.Dispatch(encoding.Payload);
            if (!preflight.IsSuccess ||
                preflight.SourceVersion != CitySnapshotFormat.CurrentVersion)
            {
                return new CitySaveResult(CityPersistenceError.EncodingFailed, false);
            }

            SnapshotPayloadDecodeResult decoded;
            try
            {
                decoded = _codec.Decode(preflight.Payload);
            }
            catch (Exception)
            {
                return new CitySaveResult(CityPersistenceError.EncodingFailed, false);
            }

            if (decoded == null || !decoded.IsSuccess || decoded.Snapshot == null)
                return new CitySaveResult(CityPersistenceError.EncodingFailed, false);

            CitySaveStorageWriteResult write;
            try
            {
                write = _storage.WriteAtomically(saveKey, encoding.Payload);
            }
            catch (Exception)
            {
                return new CitySaveResult(CityPersistenceError.Unknown, false);
            }

            if (write == null)
                return new CitySaveResult(CityPersistenceError.Unknown, false);
            return new CitySaveResult(Map(write.Error), write.IsSuccess && write.HasBackup);
        }

        public CityLoadResult Load(
            string saveKey,
            CityRuntimeOptions definitionSource = null)
        {
            CitySaveStorageReadResult read;
            try
            {
                read = _storage.Read(saveKey);
            }
            catch (Exception)
            {
                return Failure(CityPersistenceError.Unknown);
            }

            if (read == null)
                return Failure(CityPersistenceError.Unknown);
            if (!read.IsSuccess)
                return Failure(Map(read.Error));

            var migration = _migrationDispatcher.Dispatch(read.Payload);
            if (!migration.IsSuccess)
                return Failure(Map(migration.Error), migration.SourceVersion);

            SnapshotPayloadDecodeResult decoding;
            try
            {
                decoding = _codec.Decode(migration.Payload);
            }
            catch (Exception)
            {
                return Failure(CityPersistenceError.CorruptData, migration.SourceVersion);
            }

            if (decoding == null || !decoding.IsSuccess || decoding.Snapshot == null)
                return Failure(CityPersistenceError.CorruptData, migration.SourceVersion);

            try
            {
                var runtime = CitySnapshotSerializer.Restore(decoding.Snapshot, definitionSource);
                return new CityLoadResult(
                    CityPersistenceError.None,
                    runtime,
                    migration.WasMigrated,
                    migration.SourceVersion);
            }
            catch (CitySnapshotValidationException)
            {
                return Failure(CityPersistenceError.InvalidSnapshot, migration.SourceVersion);
            }
            catch (Exception)
            {
                return Failure(CityPersistenceError.InvalidSnapshot, migration.SourceVersion);
            }
        }

        public CitySaveMetadataResult ReadMetadata(string saveKey)
        {
            CitySaveStorageReadResult read;
            try
            {
                read = _storage.Read(saveKey);
            }
            catch (Exception)
            {
                return MetadataFailure(CityPersistenceError.Unknown);
            }

            if (read == null)
                return MetadataFailure(CityPersistenceError.Unknown);
            if (!read.IsSuccess)
                return MetadataFailure(Map(read.Error));

            var migration = _migrationDispatcher.Dispatch(read.Payload);
            if (!migration.IsSuccess)
                return MetadataFailure(Map(migration.Error), migration.SourceVersion);

            SnapshotPayloadDecodeResult decoding;
            try
            {
                decoding = _codec.Decode(migration.Payload);
            }
            catch (Exception)
            {
                return MetadataFailure(CityPersistenceError.CorruptData, migration.SourceVersion);
            }

            var snapshot = decoding?.Snapshot;
            if (decoding == null ||
                !decoding.IsSuccess ||
                snapshot?.Header == null ||
                snapshot.Timeline == null)
            {
                return MetadataFailure(CityPersistenceError.CorruptData, migration.SourceVersion);
            }

            return new CitySaveMetadataResult(
                CityPersistenceError.None,
                snapshot.Header.FormatVersion,
                snapshot.Header.GameVersion,
                snapshot.Header.CreatedUtcTicks,
                snapshot.Timeline.CurrentTick,
                snapshot.Timeline.NextMonthYear,
                snapshot.Timeline.NextMonth,
                read.Payload.Length);
        }

        private static IEnumerable<ICitySnapshotMigrationStep> CreateDefaultMigrations(
            ICitySnapshotPayloadCodec codec)
        {
            return new ICitySnapshotMigrationStep[]
            {
                new CitySnapshotV1ToV2Migration(codec)
            };
        }

        private static CityLoadResult Failure(
            CityPersistenceError error,
            int sourceVersion = 0)
        {
            return new CityLoadResult(error, null, false, sourceVersion);
        }

        private static CitySaveMetadataResult MetadataFailure(
            CityPersistenceError error,
            int formatVersion = 0)
        {
            return new CitySaveMetadataResult(
                error,
                formatVersion,
                string.Empty,
                0,
                0,
                0,
                0,
                0);
        }

        private static CityPersistenceError Map(CitySaveStorageError error)
        {
            switch (error)
            {
                case CitySaveStorageError.None:
                    return CityPersistenceError.None;
                case CitySaveStorageError.InvalidSaveKey:
                    return CityPersistenceError.InvalidSaveKey;
                case CitySaveStorageError.NotFound:
                    return CityPersistenceError.NotFound;
                case CitySaveStorageError.AccessDenied:
                    return CityPersistenceError.AccessDenied;
                case CitySaveStorageError.Locked:
                    return CityPersistenceError.Locked;
                case CitySaveStorageError.WriteFailed:
                    return CityPersistenceError.WriteFailed;
                case CitySaveStorageError.FlushFailed:
                    return CityPersistenceError.FlushFailed;
                case CitySaveStorageError.ReplaceFailed:
                    return CityPersistenceError.ReplaceFailed;
                case CitySaveStorageError.CleanupFailed:
                    return CityPersistenceError.CleanupFailed;
                default:
                    return CityPersistenceError.Unknown;
            }
        }

        private static CityPersistenceError Map(SnapshotMigrationError error)
        {
            switch (error)
            {
                case SnapshotMigrationError.CorruptData:
                    return CityPersistenceError.CorruptData;
                case SnapshotMigrationError.UnsupportedVersion:
                    return CityPersistenceError.UnsupportedVersion;
                case SnapshotMigrationError.MigrationFailed:
                    return CityPersistenceError.MigrationFailed;
                default:
                    return CityPersistenceError.Unknown;
            }
        }
    }
}
