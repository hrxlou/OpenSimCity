using System;
using System.Collections.Generic;

namespace OpenSimCity.Application.Persistence
{
    public enum SnapshotMigrationError
    {
        None = 0,
        CorruptData = 1,
        UnsupportedVersion = 2,
        MigrationFailed = 3
    }

    public sealed class SnapshotMigrationStepResult
    {
        private SnapshotMigrationStepResult(bool isSuccess, byte[] payload)
        {
            IsSuccess = isSuccess;
            Payload = payload == null ? null : (byte[])payload.Clone();
        }

        public bool IsSuccess { get; }
        public byte[] Payload { get; }

        public static SnapshotMigrationStepResult Success(byte[] payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            return new SnapshotMigrationStepResult(true, payload);
        }

        public static SnapshotMigrationStepResult Failure()
        {
            return new SnapshotMigrationStepResult(false, null);
        }
    }

    /// <summary>
    /// One explicit, immutable source-version to target-version transformation.
    /// Implementations receive a private copy so even a faulty migration cannot
    /// modify the bytes supplied by the caller.
    /// </summary>
    public interface ICitySnapshotMigrationStep
    {
        int SourceVersion { get; }
        int TargetVersion { get; }
        SnapshotMigrationStepResult Migrate(byte[] sourcePayload);
    }

    public sealed class SnapshotMigrationResult
    {
        private SnapshotMigrationResult(
            SnapshotMigrationError error,
            byte[] payload,
            int sourceVersion,
            int targetVersion)
        {
            Error = error;
            Payload = payload == null ? null : (byte[])payload.Clone();
            SourceVersion = sourceVersion;
            TargetVersion = targetVersion;
        }

        public bool IsSuccess => Error == SnapshotMigrationError.None;
        public SnapshotMigrationError Error { get; }
        public byte[] Payload { get; }
        public int SourceVersion { get; }
        public int TargetVersion { get; }
        public bool WasMigrated => IsSuccess && SourceVersion != TargetVersion;

        public static SnapshotMigrationResult Success(
            byte[] payload,
            int sourceVersion,
            int targetVersion)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            return new SnapshotMigrationResult(
                SnapshotMigrationError.None,
                payload,
                sourceVersion,
                targetVersion);
        }

        public static SnapshotMigrationResult Failure(
            SnapshotMigrationError error,
            int sourceVersion,
            int targetVersion)
        {
            if (error == SnapshotMigrationError.None)
                throw new ArgumentOutOfRangeException(nameof(error));
            return new SnapshotMigrationResult(error, null, sourceVersion, targetVersion);
        }
    }

    /// <summary>
    /// Dispatches only registered forward migrations. It never guesses how to
    /// interpret unknown versions and never mutates source bytes.
    /// </summary>
    public sealed class CitySnapshotMigrationDispatcher
    {
        private readonly ICitySnapshotPayloadCodec _codec;
        private readonly int _targetVersion;
        private readonly IReadOnlyDictionary<int, ICitySnapshotMigrationStep> _steps;

        public CitySnapshotMigrationDispatcher(
            ICitySnapshotPayloadCodec codec,
            int targetVersion,
            IEnumerable<ICitySnapshotMigrationStep> steps = null)
        {
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            if (targetVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetVersion));

            _targetVersion = targetVersion;
            var bySourceVersion = new Dictionary<int, ICitySnapshotMigrationStep>();
            if (steps != null)
            {
                foreach (var step in steps)
                {
                    if (step == null)
                        throw new ArgumentException("Migration steps cannot contain null.", nameof(steps));
                    if (step.SourceVersion <= 0 ||
                        step.TargetVersion <= step.SourceVersion ||
                        step.TargetVersion > targetVersion)
                    {
                        throw new ArgumentException(
                            "Migration steps must move forward without passing the target version.",
                            nameof(steps));
                    }
                    if (bySourceVersion.ContainsKey(step.SourceVersion))
                    {
                        throw new ArgumentException(
                            $"More than one migration starts at version {step.SourceVersion}.",
                            nameof(steps));
                    }

                    bySourceVersion.Add(step.SourceVersion, step);
                }
            }

            _steps = bySourceVersion;
        }

        public SnapshotMigrationResult Dispatch(byte[] sourcePayload)
        {
            if (sourcePayload == null || sourcePayload.Length == 0)
            {
                return SnapshotMigrationResult.Failure(
                    SnapshotMigrationError.CorruptData,
                    0,
                    _targetVersion);
            }

            var currentPayload = (byte[])sourcePayload.Clone();
            var versionResult = SafeReadVersion(currentPayload);
            if (!versionResult.IsSuccess)
            {
                return SnapshotMigrationResult.Failure(
                    SnapshotMigrationError.CorruptData,
                    0,
                    _targetVersion);
            }

            var sourceVersion = versionResult.FormatVersion;
            var currentVersion = sourceVersion;
            while (currentVersion != _targetVersion)
            {
                if (currentVersion > _targetVersion ||
                    !_steps.TryGetValue(currentVersion, out var step))
                {
                    return SnapshotMigrationResult.Failure(
                        SnapshotMigrationError.UnsupportedVersion,
                        sourceVersion,
                        _targetVersion);
                }

                SnapshotMigrationStepResult stepResult;
                try
                {
                    stepResult = step.Migrate((byte[])currentPayload.Clone());
                }
                catch (Exception)
                {
                    return SnapshotMigrationResult.Failure(
                        SnapshotMigrationError.MigrationFailed,
                        sourceVersion,
                        _targetVersion);
                }

                if (stepResult == null || !stepResult.IsSuccess || stepResult.Payload == null)
                {
                    return SnapshotMigrationResult.Failure(
                        SnapshotMigrationError.MigrationFailed,
                        sourceVersion,
                        _targetVersion);
                }

                currentPayload = (byte[])stepResult.Payload.Clone();
                versionResult = SafeReadVersion(currentPayload);
                if (!versionResult.IsSuccess || versionResult.FormatVersion != step.TargetVersion)
                {
                    return SnapshotMigrationResult.Failure(
                        SnapshotMigrationError.MigrationFailed,
                        sourceVersion,
                        _targetVersion);
                }

                currentVersion = versionResult.FormatVersion;
            }

            return SnapshotMigrationResult.Success(
                currentPayload,
                sourceVersion,
                _targetVersion);
        }

        private SnapshotPayloadVersionResult SafeReadVersion(byte[] payload)
        {
            try
            {
                return _codec.ReadFormatVersion(payload) ??
                       SnapshotPayloadVersionResult.Failure(SnapshotPayloadError.CorruptData);
            }
            catch (Exception)
            {
                return SnapshotPayloadVersionResult.Failure(SnapshotPayloadError.CorruptData);
            }
        }
    }
}
