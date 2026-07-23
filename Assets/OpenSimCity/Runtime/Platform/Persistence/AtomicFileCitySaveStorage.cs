using System;
using System.IO;
using OpenSimCity.Application.Persistence;

namespace OpenSimCity.Platform.Persistence
{
    public enum AtomicFileSaveStage
    {
        BeforeWrite = 0,
        BeforeFlush = 1,
        BeforeReplace = 2
    }

    /// <summary>Test seam for deterministic interruption and failure coverage.</summary>
    public interface IAtomicFileSaveFailureInjector
    {
        void ThrowIfRequested(AtomicFileSaveStage stage);
    }

    /// <summary>
    /// File adapter that writes and flushes a sibling temporary file before an
    /// atomic move/replace. Successful replacement keeps the previous save as a
    /// backup; every handled failure attempts to remove the temporary file and
    /// leaves the destination intact.
    /// </summary>
    public sealed class AtomicFileCitySaveStorage : ICitySaveStorage
    {
        public const string SaveFileExtension = ".osc-save";
        public const string TemporaryFileSuffix = ".tmp";
        public const string BackupFileSuffix = ".bak";

        private readonly string _rootDirectory;
        private readonly IAtomicFileSaveFailureInjector _failureInjector;

        public AtomicFileCitySaveStorage(
            string rootDirectory,
            IAtomicFileSaveFailureInjector failureInjector = null)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("A storage root is required.", nameof(rootDirectory));

            _rootDirectory = Path.GetFullPath(rootDirectory);
            _failureInjector = failureInjector;
        }

        public CitySaveStorageReadResult Read(string saveKey)
        {
            if (!TryGetPaths(saveKey, out var paths))
                return CitySaveStorageReadResult.Failure(CitySaveStorageError.InvalidSaveKey);
            if (!File.Exists(paths.Destination))
                return CitySaveStorageReadResult.Failure(CitySaveStorageError.NotFound);

            try
            {
                using (var stream = new FileStream(
                           paths.Destination,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.Read))
                {
                    if (stream.Length > int.MaxValue)
                        return CitySaveStorageReadResult.Failure(CitySaveStorageError.Unknown);

                    var payload = new byte[(int)stream.Length];
                    var offset = 0;
                    while (offset < payload.Length)
                    {
                        var read = stream.Read(payload, offset, payload.Length - offset);
                        if (read == 0)
                            return CitySaveStorageReadResult.Failure(CitySaveStorageError.Unknown);
                        offset += read;
                    }

                    return CitySaveStorageReadResult.Success(payload);
                }
            }
            catch (UnauthorizedAccessException)
            {
                return CitySaveStorageReadResult.Failure(CitySaveStorageError.AccessDenied);
            }
            catch (IOException exception)
            {
                return CitySaveStorageReadResult.Failure(
                    IsSharingViolation(exception)
                        ? CitySaveStorageError.Locked
                        : CitySaveStorageError.Unknown);
            }
            catch (Exception)
            {
                return CitySaveStorageReadResult.Failure(CitySaveStorageError.Unknown);
            }
        }

        public CitySaveStorageWriteResult WriteAtomically(string saveKey, byte[] payload)
        {
            if (!TryGetPaths(saveKey, out var paths))
                return CitySaveStorageWriteResult.Failure(CitySaveStorageError.InvalidSaveKey);
            if (payload == null)
                return CitySaveStorageWriteResult.Failure(CitySaveStorageError.WriteFailed);

            try
            {
                Directory.CreateDirectory(_rootDirectory);
                DeleteIfPresent(paths.Temporary);
            }
            catch (UnauthorizedAccessException)
            {
                return CitySaveStorageWriteResult.Failure(CitySaveStorageError.AccessDenied);
            }
            catch (Exception)
            {
                return CitySaveStorageWriteResult.Failure(CitySaveStorageError.CleanupFailed);
            }

            var activeStage = AtomicFileSaveStage.BeforeWrite;
            try
            {
                _failureInjector?.ThrowIfRequested(activeStage);
                using (var stream = new FileStream(
                           paths.Temporary,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           4096,
                           FileOptions.WriteThrough))
                {
                    stream.Write(payload, 0, payload.Length);
                    activeStage = AtomicFileSaveStage.BeforeFlush;
                    _failureInjector?.ThrowIfRequested(activeStage);
                    stream.Flush(flushToDisk: true);
                }
            }
            catch (UnauthorizedAccessException)
            {
                return FailureAfterCleanup(paths.Temporary, CitySaveStorageError.AccessDenied);
            }
            catch (Exception)
            {
                return FailureAfterCleanup(
                    paths.Temporary,
                    activeStage == AtomicFileSaveStage.BeforeFlush
                        ? CitySaveStorageError.FlushFailed
                        : CitySaveStorageError.WriteFailed);
            }

            try
            {
                activeStage = AtomicFileSaveStage.BeforeReplace;
                _failureInjector?.ThrowIfRequested(activeStage);

                if (File.Exists(paths.Destination))
                {
                    DeleteIfPresent(paths.Backup);
                    File.Replace(
                        paths.Temporary,
                        paths.Destination,
                        paths.Backup,
                        ignoreMetadataErrors: true);
                    return CitySaveStorageWriteResult.Success(hasBackup: true);
                }

                File.Move(paths.Temporary, paths.Destination);
                return CitySaveStorageWriteResult.Success(hasBackup: false);
            }
            catch (UnauthorizedAccessException)
            {
                TryRestoreBackup(paths);
                return FailureAfterCleanup(paths.Temporary, CitySaveStorageError.AccessDenied);
            }
            catch (IOException exception)
            {
                TryRestoreBackup(paths);
                return FailureAfterCleanup(
                    paths.Temporary,
                    IsSharingViolation(exception)
                        ? CitySaveStorageError.Locked
                        : CitySaveStorageError.ReplaceFailed);
            }
            catch (Exception)
            {
                TryRestoreBackup(paths);
                return FailureAfterCleanup(paths.Temporary, CitySaveStorageError.ReplaceFailed);
            }
        }

        private CitySaveStorageWriteResult FailureAfterCleanup(
            string temporaryPath,
            CitySaveStorageError originalError)
        {
            try
            {
                DeleteIfPresent(temporaryPath);
                return CitySaveStorageWriteResult.Failure(originalError);
            }
            catch (Exception)
            {
                return CitySaveStorageWriteResult.Failure(CitySaveStorageError.CleanupFailed);
            }
        }

        private static void TryRestoreBackup(SavePaths paths)
        {
            if (File.Exists(paths.Destination) || !File.Exists(paths.Backup))
                return;

            try
            {
                File.Move(paths.Backup, paths.Destination);
            }
            catch (Exception)
            {
                // The original operation's safe error result is preserved. A
                // remaining backup is intentionally available for recovery.
            }
        }

        private bool TryGetPaths(string saveKey, out SavePaths paths)
        {
            paths = default;
            if (!IsValidSaveKey(saveKey))
                return false;

            var destination = Path.Combine(_rootDirectory, saveKey + SaveFileExtension);
            paths = new SavePaths(
                destination,
                destination + TemporaryFileSuffix,
                destination + BackupFileSuffix);
            return true;
        }

        private static bool IsValidSaveKey(string saveKey)
        {
            if (string.IsNullOrEmpty(saveKey) || saveKey.Length > 64)
                return false;

            for (var index = 0; index < saveKey.Length; index++)
            {
                var character = saveKey[index];
                var isAsciiLetter =
                    character >= 'A' && character <= 'Z' ||
                    character >= 'a' && character <= 'z';
                var isDigit = character >= '0' && character <= '9';
                if (!isAsciiLetter &&
                    !isDigit &&
                    character != '-' &&
                    character != '_' &&
                    character != '.')
                {
                    return false;
                }
            }

            return saveKey != "." &&
                   saveKey != ".." &&
                   !saveKey.StartsWith(".", StringComparison.Ordinal);
        }

        private static void DeleteIfPresent(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private static bool IsSharingViolation(IOException exception)
        {
            var code = exception.HResult & 0xFFFF;
            return code == 32 || code == 33;
        }

        private readonly struct SavePaths
        {
            public SavePaths(string destination, string temporary, string backup)
            {
                Destination = destination;
                Temporary = temporary;
                Backup = backup;
            }

            public string Destination { get; }
            public string Temporary { get; }
            public string Backup { get; }
        }
    }
}
