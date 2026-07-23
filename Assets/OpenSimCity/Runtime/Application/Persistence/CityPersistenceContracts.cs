using System;
using OpenSimCity.Application.World;

namespace OpenSimCity.Application.Persistence
{
    public enum CitySaveStorageError
    {
        None = 0,
        InvalidSaveKey = 1,
        NotFound = 2,
        AccessDenied = 3,
        Locked = 4,
        WriteFailed = 5,
        FlushFailed = 6,
        ReplaceFailed = 7,
        CleanupFailed = 8,
        Unknown = 9
    }

    public sealed class CitySaveStorageReadResult
    {
        private CitySaveStorageReadResult(CitySaveStorageError error, byte[] payload)
        {
            Error = error;
            Payload = payload == null ? null : (byte[])payload.Clone();
        }

        public bool IsSuccess => Error == CitySaveStorageError.None;
        public CitySaveStorageError Error { get; }
        public byte[] Payload { get; }

        public static CitySaveStorageReadResult Success(byte[] payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            return new CitySaveStorageReadResult(CitySaveStorageError.None, payload);
        }

        public static CitySaveStorageReadResult Failure(CitySaveStorageError error)
        {
            if (error == CitySaveStorageError.None)
                throw new ArgumentOutOfRangeException(nameof(error));
            return new CitySaveStorageReadResult(error, null);
        }
    }

    public sealed class CitySaveStorageWriteResult
    {
        private CitySaveStorageWriteResult(CitySaveStorageError error, bool hasBackup)
        {
            Error = error;
            HasBackup = hasBackup;
        }

        public bool IsSuccess => Error == CitySaveStorageError.None;
        public CitySaveStorageError Error { get; }
        public bool HasBackup { get; }

        public static CitySaveStorageWriteResult Success(bool hasBackup)
        {
            return new CitySaveStorageWriteResult(CitySaveStorageError.None, hasBackup);
        }

        public static CitySaveStorageWriteResult Failure(CitySaveStorageError error)
        {
            if (error == CitySaveStorageError.None)
                throw new ArgumentOutOfRangeException(nameof(error));
            return new CitySaveStorageWriteResult(error, false);
        }
    }

    /// <summary>
    /// Application persistence port. A save key is an opaque stable identifier;
    /// paths, extensions, locking APIs, and replacement mechanics belong to the
    /// platform adapter.
    /// </summary>
    public interface ICitySaveStorage
    {
        CitySaveStorageReadResult Read(string saveKey);
        CitySaveStorageWriteResult WriteAtomically(string saveKey, byte[] payload);
    }

    public enum SnapshotPayloadError
    {
        None = 0,
        CorruptData = 1,
        EncodingFailed = 2
    }

    public sealed class SnapshotPayloadVersionResult
    {
        private SnapshotPayloadVersionResult(SnapshotPayloadError error, int formatVersion)
        {
            Error = error;
            FormatVersion = formatVersion;
        }

        public bool IsSuccess => Error == SnapshotPayloadError.None;
        public SnapshotPayloadError Error { get; }
        public int FormatVersion { get; }

        public static SnapshotPayloadVersionResult Success(int formatVersion)
        {
            if (formatVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(formatVersion));
            return new SnapshotPayloadVersionResult(SnapshotPayloadError.None, formatVersion);
        }

        public static SnapshotPayloadVersionResult Failure(SnapshotPayloadError error)
        {
            if (error == SnapshotPayloadError.None)
                throw new ArgumentOutOfRangeException(nameof(error));
            return new SnapshotPayloadVersionResult(error, 0);
        }
    }

    public sealed class SnapshotPayloadEncodeResult
    {
        private SnapshotPayloadEncodeResult(SnapshotPayloadError error, byte[] payload)
        {
            Error = error;
            Payload = payload == null ? null : (byte[])payload.Clone();
        }

        public bool IsSuccess => Error == SnapshotPayloadError.None;
        public SnapshotPayloadError Error { get; }
        public byte[] Payload { get; }

        public static SnapshotPayloadEncodeResult Success(byte[] payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            return new SnapshotPayloadEncodeResult(SnapshotPayloadError.None, payload);
        }

        public static SnapshotPayloadEncodeResult Failure(SnapshotPayloadError error)
        {
            if (error == SnapshotPayloadError.None)
                throw new ArgumentOutOfRangeException(nameof(error));
            return new SnapshotPayloadEncodeResult(error, null);
        }
    }

    public sealed class SnapshotPayloadDecodeResult
    {
        private SnapshotPayloadDecodeResult(SnapshotPayloadError error, CitySnapshotDto snapshot)
        {
            Error = error;
            Snapshot = snapshot;
        }

        public bool IsSuccess => Error == SnapshotPayloadError.None;
        public SnapshotPayloadError Error { get; }
        public CitySnapshotDto Snapshot { get; }

        public static SnapshotPayloadDecodeResult Success(CitySnapshotDto snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            return new SnapshotPayloadDecodeResult(SnapshotPayloadError.None, snapshot);
        }

        public static SnapshotPayloadDecodeResult Failure(SnapshotPayloadError error)
        {
            if (error == SnapshotPayloadError.None)
                throw new ArgumentOutOfRangeException(nameof(error));
            return new SnapshotPayloadDecodeResult(error, null);
        }
    }

    /// <summary>
    /// Encoding adapter seam. Unity JSON or a future binary codec remains outside
    /// the Unity-independent Application assembly.
    /// </summary>
    public interface ICitySnapshotPayloadCodec
    {
        SnapshotPayloadVersionResult ReadFormatVersion(byte[] payload);
        SnapshotPayloadEncodeResult Encode(CitySnapshotDto snapshot);
        SnapshotPayloadDecodeResult Decode(byte[] payload);
    }

    public enum CityPersistenceError
    {
        None = 0,
        InvalidSaveKey = 1,
        NotFound = 2,
        AccessDenied = 3,
        Locked = 4,
        WriteFailed = 5,
        FlushFailed = 6,
        ReplaceFailed = 7,
        CleanupFailed = 8,
        CorruptData = 9,
        UnsupportedVersion = 10,
        MigrationFailed = 11,
        InvalidSnapshot = 12,
        EncodingFailed = 13,
        Unknown = 14
    }

    public sealed class CitySaveResult
    {
        internal CitySaveResult(CityPersistenceError error, bool hasBackup)
        {
            Error = error;
            HasBackup = hasBackup;
        }

        public bool IsSuccess => Error == CityPersistenceError.None;
        public CityPersistenceError Error { get; }
        public bool HasBackup { get; }
    }

    public sealed class CityLoadResult
    {
        internal CityLoadResult(
            CityPersistenceError error,
            CityRuntime runtime,
            bool wasMigrated,
            int sourceFormatVersion)
        {
            Error = error;
            Runtime = runtime;
            WasMigrated = wasMigrated;
            SourceFormatVersion = sourceFormatVersion;
        }

        public bool IsSuccess => Error == CityPersistenceError.None;
        public CityPersistenceError Error { get; }
        public CityRuntime Runtime { get; }
        public bool WasMigrated { get; }
        public int SourceFormatVersion { get; }
    }

    public sealed class CitySaveMetadataResult
    {
        internal CitySaveMetadataResult(
            CityPersistenceError error,
            int formatVersion,
            string gameVersion,
            long createdUtcTicks,
            long currentTick,
            int nextMonthYear,
            int nextMonth,
            int payloadByteCount)
        {
            Error = error;
            FormatVersion = formatVersion;
            GameVersion = gameVersion ?? string.Empty;
            CreatedUtcTicks = createdUtcTicks;
            CurrentTick = currentTick;
            NextMonthYear = nextMonthYear;
            NextMonth = nextMonth;
            PayloadByteCount = payloadByteCount;
        }

        public bool IsSuccess => Error == CityPersistenceError.None;
        public CityPersistenceError Error { get; }
        public int FormatVersion { get; }
        public string GameVersion { get; }
        public long CreatedUtcTicks { get; }
        public long CurrentTick { get; }
        public int NextMonthYear { get; }
        public int NextMonth { get; }
        public int PayloadByteCount { get; }
    }
}
