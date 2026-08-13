using System;
using System.IO;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Onboarding;
using OpenSimCity.Application.Persistence;
using OpenSimCity.Application.World;
using OpenSimCity.Gameplay.Construction;
using UnityEngine;

namespace OpenSimCity.Platform.Persistence
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class CityPersistenceHost : MonoBehaviour
    {
        public const string SaveDirectoryName = "Saves";
        public const string ProfileDirectoryName = "Profile";
        public const string OnboardingProfileFileName = "onboarding.json";

        [SerializeField] private CityRuntimeHost runtimeHost;
        [SerializeField] private ConstructionInputDriver inputDriver;

        private CityPersistenceController _controller;
        private OnboardingProgressController _onboarding;
        private ReleaseSessionActions _releaseSession;

        public CityPersistenceController Controller => _controller;
        public string StorageRootDirectory { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetContext()
        {
            CityPersistenceContext.Reset();
            OnboardingContext.Reset();
            ReleaseSessionContext.Reset();
        }

        public void ConfigureReferences(CityRuntimeHost cityRuntimeHost, ConstructionInputDriver constructionInputDriver)
        {
            runtimeHost = cityRuntimeHost;
            inputDriver = constructionInputDriver;
        }

        private void Start() => Initialize();
        private void OnDisable() => ShutdownController();

        private void Update()
        {
            if (_controller == null && runtimeHost != null && runtimeHost.Runtime != null)
                Initialize();
            _controller?.ProcessPendingOperation();
        }

        public void Initialize() => Initialize(null);

        public void ReinitializeForTests(string storageRootDirectory)
        {
            if (string.IsNullOrWhiteSpace(storageRootDirectory))
                throw new ArgumentException("A test storage root is required.", nameof(storageRootDirectory));
            ShutdownController();
            Initialize(storageRootDirectory);
        }

        private void Initialize(string storageRootDirectory)
        {
            if (_controller != null) return;
            if (runtimeHost == null || runtimeHost.Runtime == null)
                throw new InvalidOperationException("CityPersistenceHost requires an active CityRuntimeHost.");

            StorageRootDirectory = string.IsNullOrWhiteSpace(storageRootDirectory)
                ? Path.Combine(UnityEngine.Application.persistentDataPath, SaveDirectoryName)
                : Path.GetFullPath(storageRootDirectory);
            var storage = new AtomicFileCitySaveStorage(StorageRootDirectory);
            var codec = new UnityJsonCitySnapshotPayloadCodec();
            var service = new CityPersistenceService(storage, codec,
                new ICitySnapshotMigrationStep[] { new CitySnapshotV1ToV2Migration(codec) });
            _controller = new CityPersistenceController(
                runtimeHost.Runtime, service, runtimeHost.ReplaceRuntime, () => DateTime.UtcNow.Ticks,
                string.IsNullOrWhiteSpace(UnityEngine.Application.version)
                    ? CitySnapshotFormat.DevelopmentGameVersion
                    : UnityEngine.Application.version);
            _controller.ViewModelChanged += HandleViewModelChanged;
            CityPersistenceContext.SetCurrent(_controller);

            var profilePath = ResolveOnboardingProfilePath(StorageRootDirectory);
            _onboarding = new OnboardingProgressController(
                runtimeHost.Runtime, new JsonFileOnboardingProfileStore(profilePath));
            OnboardingContext.SetCurrent(_onboarding);

            _releaseSession = new ReleaseSessionActions(
                StartNewCity,
                ContinueCity,
                key => _controller != null && _controller.RequestLoad(key),
                () => _controller?.ViewModel);
            ReleaseSessionContext.SetCurrent(_releaseSession);
            HandleViewModelChanged(_controller.ViewModel);
        }

        private bool StartNewCity()
        {
            if (_controller == null || _controller.ViewModel?.IsBusy == true || runtimeHost == null)
                return false;

            var storageRoot = StorageRootDirectory;
            ShutdownController();
            runtimeHost.ReplaceRuntime(NewCityRuntimeFactory.Create(CityRuntimeOptions.Default));
            Initialize(storageRoot);
            _onboarding?.ResetForNewProfile();
            return true;
        }

        private bool ContinueCity()
        {
            if (_controller == null || _controller.ViewModel?.IsBusy == true) return false;
            var slots = _controller.ViewModel?.Slots;
            if (slots == null) return false;
            CitySaveSlotViewModel best = null;
            for (var index = 0; index < slots.Count; index++)
            {
                var candidate = slots[index];
                if (candidate == null || !candidate.HasSave) continue;
                if (best == null || candidate.CreatedUtcTicks > best.CreatedUtcTicks ||
                    (candidate.CreatedUtcTicks == best.CreatedUtcTicks && candidate.Kind == CitySaveSlotKind.Manual))
                    best = candidate;
            }
            return best != null && _controller.RequestLoad(best.SaveKey);
        }

        private void ShutdownController()
        {
            if (_releaseSession != null)
            {
                ReleaseSessionContext.ClearCurrent(_releaseSession);
                _releaseSession = null;
            }
            if (_onboarding != null)
            {
                OnboardingContext.ClearCurrent(_onboarding);
                _onboarding = null;
            }
            if (_controller != null)
            {
                _controller.ViewModelChanged -= HandleViewModelChanged;
                CityPersistenceContext.ClearCurrent(_controller);
                _controller.Dispose();
                _controller = null;
            }
            inputDriver?.SetInteractionEnabled(true);
        }

        private void HandleViewModelChanged(CityPersistenceViewModel viewModel)
        {
            inputDriver?.SetInteractionEnabled(viewModel == null || !viewModel.IsBusy);
            if (viewModel?.State == CityPersistenceOperationState.Loaded && _controller?.Runtime != null)
                _onboarding?.BindRuntime(_controller.Runtime);
        }

        private static string ResolveOnboardingProfilePath(string saveRoot)
        {
            var saveDirectory = Path.GetFullPath(saveRoot);
            var parent = Directory.GetParent(saveDirectory)?.FullName ?? saveDirectory;
            return Path.Combine(parent, ProfileDirectoryName, OnboardingProfileFileName);
        }
    }
}
