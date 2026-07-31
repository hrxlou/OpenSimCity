using System;
using System.IO;
using OpenSimCity.Application.Persistence;
using OpenSimCity.Gameplay.Construction;
using UnityEngine;

namespace OpenSimCity.Platform.Persistence
{
    /// <summary>
    /// Unity-side composition root for P4 persistence. It supplies the platform
    /// save directory and JSON codec, processes queued application commands, and
    /// blocks world input while a save/load operation owns the clock.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class CityPersistenceHost : MonoBehaviour
    {
        public const string SaveDirectoryName = "Saves";

        [SerializeField] private CityRuntimeHost runtimeHost;
        [SerializeField] private ConstructionInputDriver inputDriver;

        private CityPersistenceController _controller;

        public CityPersistenceController Controller => _controller;
        public string StorageRootDirectory { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetContext()
        {
            CityPersistenceContext.Reset();
        }

        public void ConfigureReferences(
            CityRuntimeHost cityRuntimeHost,
            ConstructionInputDriver constructionInputDriver)
        {
            runtimeHost = cityRuntimeHost;
            inputDriver = constructionInputDriver;
        }

        private void Start()
        {
            Initialize();
        }

        private void OnDisable()
        {
            ShutdownController();
        }

        private void Update()
        {
            if (_controller == null && runtimeHost != null && runtimeHost.Runtime != null)
                Initialize();
            _controller?.ProcessPendingOperation();
        }

        public void Initialize()
        {
            Initialize(null);
        }

        /// <summary>
        /// Test seam for PlayMode isolation. Product composition always calls the
        /// parameterless overload and therefore uses persistentDataPath/Saves.
        /// </summary>
        public void ReinitializeForTests(string storageRootDirectory)
        {
            if (string.IsNullOrWhiteSpace(storageRootDirectory))
                throw new ArgumentException("A test storage root is required.", nameof(storageRootDirectory));
            ShutdownController();
            Initialize(storageRootDirectory);
        }

        private void Initialize(string storageRootDirectory)
        {
            if (_controller != null)
                return;
            if (runtimeHost == null || runtimeHost.Runtime == null)
                throw new InvalidOperationException("CityPersistenceHost requires an active CityRuntimeHost.");

            StorageRootDirectory = string.IsNullOrWhiteSpace(storageRootDirectory)
                ? Path.Combine(UnityEngine.Application.persistentDataPath, SaveDirectoryName)
                : Path.GetFullPath(storageRootDirectory);
            var storage = new AtomicFileCitySaveStorage(StorageRootDirectory);
            var codec = new UnityJsonCitySnapshotPayloadCodec();
            var service = new CityPersistenceService(
                storage,
                codec,
                new ICitySnapshotMigrationStep[]
                {
                    new CitySnapshotV1ToV2Migration(codec)
                });
            _controller = new CityPersistenceController(
                runtimeHost.Runtime,
                service,
                runtimeHost.ReplaceRuntime,
                () => DateTime.UtcNow.Ticks,
                string.IsNullOrWhiteSpace(UnityEngine.Application.version)
                    ? CitySnapshotFormat.DevelopmentGameVersion
                    : UnityEngine.Application.version);
            _controller.ViewModelChanged += HandleViewModelChanged;
            CityPersistenceContext.SetCurrent(_controller);
            HandleViewModelChanged(_controller.ViewModel);
        }

        private void ShutdownController()
        {
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
        }
    }
}
