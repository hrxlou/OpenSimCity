using System;
using System.Collections.Generic;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.World;

namespace OpenSimCity.Application.Persistence
{
    public enum CitySaveSlotKind
    {
        Manual = 0,
        Autosave = 1
    }

    public enum CityPersistenceOperationState
    {
        Idle = 0,
        Saving = 1,
        Loading = 2,
        Saved = 3,
        Loaded = 4,
        Error = 5
    }

    public sealed class CitySaveSlotViewModel
    {
        internal CitySaveSlotViewModel(
            string saveKey,
            string displayName,
            CitySaveSlotKind kind,
            CitySaveMetadataResult metadata)
        {
            SaveKey = saveKey;
            DisplayName = displayName;
            Kind = kind;
            MetadataError = metadata?.Error ?? CityPersistenceError.Unknown;
            HasSave = MetadataError != CityPersistenceError.NotFound;
            IsMetadataAvailable = metadata != null && metadata.IsSuccess;
            FormatVersion = metadata?.FormatVersion ?? 0;
            GameVersion = metadata?.GameVersion ?? string.Empty;
            CreatedUtcTicks = metadata?.CreatedUtcTicks ?? 0;
            CurrentTick = metadata?.CurrentTick ?? 0;
            NextMonthYear = metadata?.NextMonthYear ?? 0;
            NextMonth = metadata?.NextMonth ?? 0;
            PayloadByteCount = metadata?.PayloadByteCount ?? 0;
        }

        public string SaveKey { get; }
        public string DisplayName { get; }
        public CitySaveSlotKind Kind { get; }
        public bool HasSave { get; }
        public bool IsMetadataAvailable { get; }
        public CityPersistenceError MetadataError { get; }
        public int FormatVersion { get; }
        public string GameVersion { get; }
        public long CreatedUtcTicks { get; }
        public long CurrentTick { get; }
        public int NextMonthYear { get; }
        public int NextMonth { get; }
        public int PayloadByteCount { get; }
    }

    public sealed class CityPersistenceViewModel
    {
        internal CityPersistenceViewModel(
            CityPersistenceOperationState state,
            string message,
            string activeSaveKey,
            bool canRetry,
            IReadOnlyList<CitySaveSlotViewModel> slots)
        {
            State = state;
            Message = message ?? string.Empty;
            ActiveSaveKey = activeSaveKey ?? string.Empty;
            CanRetry = canRetry;
            Slots = slots ?? Array.Empty<CitySaveSlotViewModel>();
        }

        public CityPersistenceOperationState State { get; }
        public bool IsBusy =>
            State == CityPersistenceOperationState.Saving ||
            State == CityPersistenceOperationState.Loading;
        public string Message { get; }
        public string ActiveSaveKey { get; }
        public bool CanRetry { get; }
        public IReadOnlyList<CitySaveSlotViewModel> Slots { get; }
    }

    /// <summary>
    /// Application command boundary for manual save/load and tick-boundary
    /// autosave. Requests pause the clock immediately, are processed by the
    /// platform host on its next update, and replace the active runtime only after
    /// a complete load has validated successfully.
    /// </summary>
    public sealed class CityPersistenceController : IDisposable
    {
        public const string ManualSaveKey = "manual-1";
        public const string AutosaveKey = "autosave";
        public const int DefaultAutosaveCadenceMonths = 1;

        private readonly CityPersistenceService _service;
        private readonly Action<CityRuntime> _replaceRuntime;
        private readonly Func<long> _utcNowTicks;
        private readonly string _gameVersion;
        private readonly int _autosaveCadenceMonths;
        private CityRuntime _runtime;
        private PendingOperation _pending;
        private PendingOperation _lastOperation;
        private SimulationSpeed _speedBeforeOperation;
        private int _nextAutosaveCompletedMonth;
        private IReadOnlyList<CitySaveSlotViewModel> _slots =
            Array.Empty<CitySaveSlotViewModel>();

        public CityPersistenceController(
            CityRuntime runtime,
            CityPersistenceService service,
            Action<CityRuntime> replaceRuntime,
            Func<long> utcNowTicks = null,
            string gameVersion = CitySnapshotFormat.DevelopmentGameVersion,
            int autosaveCadenceMonths = DefaultAutosaveCadenceMonths)
        {
            if (autosaveCadenceMonths <= 0)
                throw new ArgumentOutOfRangeException(nameof(autosaveCadenceMonths));

            _service = service ?? throw new ArgumentNullException(nameof(service));
            _replaceRuntime = replaceRuntime ?? throw new ArgumentNullException(nameof(replaceRuntime));
            _utcNowTicks = utcNowTicks ?? (() => DateTime.UtcNow.Ticks);
            _gameVersion = string.IsNullOrWhiteSpace(gameVersion)
                ? CitySnapshotFormat.DevelopmentGameVersion
                : gameVersion;
            _autosaveCadenceMonths = autosaveCadenceMonths;

            BindRuntime(runtime ?? throw new ArgumentNullException(nameof(runtime)));
            RefreshSlots();
            Publish(CityPersistenceOperationState.Idle, string.Empty, string.Empty, false);
        }

        public CityRuntime Runtime => _runtime;
        public bool HasPendingOperation => _pending != null;
        public CityPersistenceViewModel ViewModel { get; private set; }

        public event Action<CityPersistenceViewModel> ViewModelChanged;

        public bool RequestManualSave()
        {
            return Queue(PendingOperation.Save(ManualSaveKey, isAutosave: false));
        }

        public bool RequestLoad(string saveKey)
        {
            if (!IsKnownSaveKey(saveKey))
                return false;
            return Queue(PendingOperation.Load(saveKey));
        }

        public bool RetryLastOperation()
        {
            return ViewModel != null &&
                   ViewModel.State == CityPersistenceOperationState.Error &&
                   _lastOperation != null &&
                   Queue(_lastOperation.Clone());
        }

        public void DismissFeedback()
        {
            if (ViewModel == null || ViewModel.IsBusy)
                return;
            Publish(CityPersistenceOperationState.Idle, string.Empty, string.Empty, false);
        }

        public void ProcessPendingOperation()
        {
            if (_pending == null)
                return;

            var operation = _pending;
            _pending = null;
            _lastOperation = operation.Clone();

            if (operation.IsLoad)
                ProcessLoad(operation);
            else
                ProcessSave(operation);
        }

        public void Dispose()
        {
            if (_runtime != null && _pending != null)
                _runtime.SetSimulationSpeed(_speedBeforeOperation);
            if (_runtime != null)
                _runtime.SimulationAdvanceCompleted -= HandleSimulationAdvanceCompleted;
            _runtime = null;
            _pending = null;
        }

        private bool Queue(PendingOperation operation)
        {
            if (_runtime == null ||
                operation == null ||
                _pending != null ||
                ViewModel?.IsBusy == true)
                return false;

            _pending = operation;
            _speedBeforeOperation = _runtime.Clock.Speed;
            _runtime.SetSimulationSpeed(SimulationSpeed.Paused);
            Publish(
                operation.IsLoad
                    ? CityPersistenceOperationState.Loading
                    : CityPersistenceOperationState.Saving,
                operation.IsLoad
                    ? "도시를 불러오는 중입니다…"
                    : operation.IsAutosave
                        ? "자동 저장 중입니다…"
                        : "도시를 저장하는 중입니다…",
                operation.SaveKey,
                false);
            return true;
        }

        private void ProcessSave(PendingOperation operation)
        {
            CitySaveResult result = null;
            try
            {
                // The controller paused the runtime while the request waited for
                // the platform host. Restore the player-selected speed only for
                // the synchronous capture so the saved clock resumes as expected.
                _runtime.SetSimulationSpeed(_speedBeforeOperation);
                result = _service.Save(
                    operation.SaveKey,
                    _runtime,
                    _gameVersion,
                    _utcNowTicks());
            }
            catch (Exception)
            {
                result = null;
            }
            finally
            {
                _runtime.SetSimulationSpeed(_speedBeforeOperation);
            }

            RefreshSlots();
            if (result != null && result.IsSuccess)
            {
                Publish(
                    CityPersistenceOperationState.Saved,
                    operation.IsAutosave
                        ? "자동 저장이 완료되었습니다."
                        : result.HasBackup
                            ? "기존 저장을 안전하게 덮어썼습니다."
                            : "도시를 저장했습니다.",
                    operation.SaveKey,
                    false);
                return;
            }

            var error = result?.Error ?? CityPersistenceError.Unknown;
            PublishError(error, operation.SaveKey);
        }

        private void ProcessLoad(PendingOperation operation)
        {
            CityLoadResult result;
            try
            {
                result = _service.Load(operation.SaveKey);
            }
            catch (Exception)
            {
                result = null;
            }

            if (result == null || !result.IsSuccess || result.Runtime == null)
            {
                _runtime.SetSimulationSpeed(_speedBeforeOperation);
                PublishError(result?.Error ?? CityPersistenceError.Unknown, operation.SaveKey);
                return;
            }

            var previousRuntime = _runtime;
            try
            {
                result.Runtime.RefreshReadModels();
                _replaceRuntime(result.Runtime);
            }
            catch (Exception)
            {
                previousRuntime.SetSimulationSpeed(_speedBeforeOperation);
                PublishError(CityPersistenceError.InvalidSnapshot, operation.SaveKey);
                return;
            }

            BindRuntime(result.Runtime);
            RefreshSlots();
            Publish(
                CityPersistenceOperationState.Loaded,
                result.WasMigrated
                    ? "저장 파일을 변환해 도시를 복원했습니다."
                    : "도시를 안전하게 복원했습니다.",
                operation.SaveKey,
                false);
        }

        private void BindRuntime(CityRuntime runtime)
        {
            if (_runtime != null)
                _runtime.SimulationAdvanceCompleted -= HandleSimulationAdvanceCompleted;

            _runtime = runtime;
            _runtime.SimulationAdvanceCompleted += HandleSimulationAdvanceCompleted;
            _nextAutosaveCompletedMonth = checked(
                _runtime.MonthlyScheduler.CompletedMonths + _autosaveCadenceMonths);
        }

        private void HandleSimulationAdvanceCompleted(int processedTicks)
        {
            if (processedTicks <= 0 ||
                _runtime.MonthlyScheduler.CompletedMonths < _nextAutosaveCompletedMonth)
            {
                return;
            }

            if (Queue(PendingOperation.Save(AutosaveKey, isAutosave: true)))
            {
                _nextAutosaveCompletedMonth = checked(
                    _runtime.MonthlyScheduler.CompletedMonths + _autosaveCadenceMonths);
            }
        }

        private void RefreshSlots()
        {
            _slots = new[]
            {
                new CitySaveSlotViewModel(
                    ManualSaveKey,
                    "수동 저장",
                    CitySaveSlotKind.Manual,
                    _service.ReadMetadata(ManualSaveKey)),
                new CitySaveSlotViewModel(
                    AutosaveKey,
                    "자동 저장",
                    CitySaveSlotKind.Autosave,
                    _service.ReadMetadata(AutosaveKey))
            };
        }

        private void PublishError(CityPersistenceError error, string saveKey)
        {
            Publish(
                CityPersistenceOperationState.Error,
                ErrorMessage(error),
                saveKey,
                true);
        }

        private void Publish(
            CityPersistenceOperationState state,
            string message,
            string activeSaveKey,
            bool canRetry)
        {
            ViewModel = new CityPersistenceViewModel(
                state,
                message,
                activeSaveKey,
                canRetry,
                _slots);
            ViewModelChanged?.Invoke(ViewModel);
        }

        private static bool IsKnownSaveKey(string saveKey)
        {
            return string.Equals(saveKey, ManualSaveKey, StringComparison.Ordinal) ||
                   string.Equals(saveKey, AutosaveKey, StringComparison.Ordinal);
        }

        private static string ErrorMessage(CityPersistenceError error)
        {
            switch (error)
            {
                case CityPersistenceError.NotFound:
                    return "저장 파일을 찾을 수 없습니다. 현재 도시는 그대로 유지됩니다.";
                case CityPersistenceError.CorruptData:
                case CityPersistenceError.InvalidSnapshot:
                    return "저장 파일이 손상되어 열 수 없습니다. 현재 도시는 그대로 유지됩니다.";
                case CityPersistenceError.UnsupportedVersion:
                    return "지원하지 않는 저장 버전입니다. 현재 도시는 그대로 유지됩니다. 게임을 업데이트한 뒤 다시 시도하세요.";
                case CityPersistenceError.AccessDenied:
                    return "저장 위치에 접근할 수 없습니다. 폴더 권한을 확인해 주세요.";
                case CityPersistenceError.Locked:
                    return "다른 프로그램이 저장 파일을 사용 중입니다. 닫은 뒤 다시 시도하세요.";
                case CityPersistenceError.WriteFailed:
                case CityPersistenceError.FlushFailed:
                case CityPersistenceError.ReplaceFailed:
                case CityPersistenceError.CleanupFailed:
                    return "저장을 완료하지 못했습니다. 기존 저장은 보존되었습니다.";
                case CityPersistenceError.EncodingFailed:
                case CityPersistenceError.MigrationFailed:
                    return "저장 데이터를 준비하지 못했습니다. 현재 도시는 그대로 유지됩니다.";
                default:
                    return "저장 작업을 완료하지 못했습니다. 현재 도시는 그대로 유지됩니다.";
            }
        }

        private sealed class PendingOperation
        {
            private PendingOperation(string saveKey, bool isLoad, bool isAutosave)
            {
                SaveKey = saveKey;
                IsLoad = isLoad;
                IsAutosave = isAutosave;
            }

            public string SaveKey { get; }
            public bool IsLoad { get; }
            public bool IsAutosave { get; }

            public static PendingOperation Save(string saveKey, bool isAutosave)
            {
                return new PendingOperation(saveKey, isLoad: false, isAutosave: isAutosave);
            }

            public static PendingOperation Load(string saveKey)
            {
                return new PendingOperation(saveKey, isLoad: true, isAutosave: false);
            }

            public PendingOperation Clone()
            {
                return new PendingOperation(SaveKey, IsLoad, IsAutosave);
            }
        }
    }

    /// <summary>
    /// Ambient application command/query seam. The Platform host owns its
    /// lifecycle; UI only consumes the controller and never reads files or JSON.
    /// </summary>
    public static class CityPersistenceContext
    {
        public static CityPersistenceController Current { get; private set; }

        public static void SetCurrent(CityPersistenceController controller)
        {
            Current = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        public static void ClearCurrent(CityPersistenceController controller)
        {
            if (ReferenceEquals(Current, controller))
                Current = null;
        }

        public static void Reset()
        {
            Current = null;
        }
    }
}
