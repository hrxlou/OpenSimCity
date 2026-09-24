using System;
using System.Collections;
using System.IO;
using OpenSimCity.Application.Input;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Onboarding;
using OpenSimCity.Application.Overlays;
using OpenSimCity.Application.Persistence;
using OpenSimCity.Application.World;
using OpenSimCity.UI.Foundation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Release
{
    [DisallowMultipleComponent]
    public sealed class ReleaseMenuBinder : MonoBehaviour
    {
        private UiFoundationBootstrap _bootstrap;
        private VisualElement _boundRoot;
        private CityRuntimeContext _boundContext;
        private OnboardingProgressController _boundOnboarding;
        private VisualElement _menuBackdrop;
        private VisualElement _menu;
        private VisualElement _loadSection;
        private VisualElement _settingsSection;
        private VisualElement _tutorial;
        private Label _tutorialTitle;
        private Label _tutorialHint;
        private Label _problemIndicator;
        private Button _problemDetails;
        private Button _continue;
        private Button _manualLoad;
        private Button _autosaveLoad;
        private Toggle _hints;
        private Button _openButton;
        private Button _quitButton;
        private Label _menuTitle;
        private bool _hasEnteredCity;
        private SimulationSpeed _resumeSpeed = SimulationSpeed.One;
        private bool _menuOpen = true;
        private bool _pointerCaptured;
        private bool _hasRefreshKey;
        private bool _lastHasPersistence;
        private CityPersistenceOperationState _lastPersistenceState;
        private int _lastPersistenceSlotCount;
        private bool _lastHasOnboarding;
        private OnboardingTaskKind _lastOnboardingTask;
        private bool _lastHasProblem;
        private string _lastProblemCode = string.Empty;
        private bool _lastHintsEnabled;
        private bool _lastMenuOpen;
        public bool IsTitleScreen => !_hasEnteredCity && _menuOpen;
        public bool IsMenuOpen => _menuOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterSceneInstaller()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<UiFoundationBootstrap>();
            if (bootstrap != null && bootstrap.GetComponent<ReleaseMenuBinder>() == null)
                bootstrap.gameObject.AddComponent<ReleaseMenuBinder>();
            if (bootstrap != null && bootstrap.GetComponent<AccessibilitySettingsBinder>() == null)
                bootstrap.gameObject.AddComponent<AccessibilitySettingsBinder>();
            if (bootstrap != null && bootstrap.GetComponent<AudioDayNightSettingsBinder>() == null)
                bootstrap.gameObject.AddComponent<AudioDayNightSettingsBinder>();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Install();
        }

        private void OnEnable()
        {
            _bootstrap = GetComponent<UiFoundationBootstrap>();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-osc-title-capture") >= 0)
                UnityEngine.Application.runInBackground = true;
            if (StandaloneSceneFlow.IsShowcaseRequested(Environment.GetCommandLineArgs()))
            {
                _menuOpen = false;
                _hasEnteredCity = true;
            }
        }

        private IEnumerator Start()
        {
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, "-osc-title-capture");
            if (index < 0 || index + 1 >= arguments.Length) yield break;
            for (var frame = 0; frame < 240 && _menuBackdrop == null; frame++) yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
            var path = Path.GetFullPath(arguments[index + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1f);
            UnityEngine.Application.Quit();
        }

        private void OnDisable()
        {
            BindContext(null);
            _boundOnboarding = null;
            ReleasePointer();
            _menuBackdrop?.RemoveFromHierarchy();
            _tutorial?.RemoveFromHierarchy();
            _menuBackdrop = null;
            _menu = null;
            _tutorial = null;
            _boundRoot = null;
            InvalidateRefreshKey();
        }

        private void Update()
        {
            if (_bootstrap == null) _bootstrap = GetComponent<UiFoundationBootstrap>();
            if (_bootstrap == null || _bootstrap.Root == null) return;
            if (!ReferenceEquals(_boundRoot, _bootstrap.Root))
            {
                _boundRoot = _bootstrap.Root;
                BuildUi();
            }

            EnsureOpenButton();
            _menuBackdrop?.EnableInClassList("osc-title-compact", _boundRoot.contentRect.height < 560f || _boundRoot.contentRect.width < 1000f);
            UiSystemPanelDock.Sync(_boundRoot.parent, _bootstrap.SystemPanelDock, _menuOpen);

            var context = CityRuntimeContext.Current;
            if (!ReferenceEquals(context, _boundContext)) BindContext(context);

            var onboarding = OnboardingContext.Current;
            if (!ReferenceEquals(onboarding, _boundOnboarding))
            {
                _boundOnboarding = onboarding;
                if (_boundOnboarding != null && _boundContext != null)
                    _boundOnboarding.BindRuntime(_boundContext.Runtime);
                InvalidateRefreshKey();
            }

            Refresh();
        }

        private void BuildUi()
        {
            _menuBackdrop?.RemoveFromHierarchy();
            _tutorial?.RemoveFromHierarchy();

            _menuBackdrop = new VisualElement { name = "osc-release-menu-backdrop" };
            _menuBackdrop.style.position = Position.Absolute;
            _menuBackdrop.style.left = 0f;
            _menuBackdrop.style.right = 0f;
            _menuBackdrop.style.top = 0f;
            _menuBackdrop.style.bottom = 0f;
            _menuBackdrop.AddToClassList("osc-release-backdrop");
            _menuBackdrop.pickingMode = PickingMode.Position;
            HookPointerBoundary(_menuBackdrop);

            var image = new VisualElement { name = "osc-title-city-image", pickingMode = PickingMode.Ignore };
            image.AddToClassList("osc-title-city-image");
            _menuBackdrop.Add(image);
            var shade = new TitleScreenShade { name = "osc-title-shade", pickingMode = PickingMode.Ignore };
            shade.AddToClassList("osc-title-shade");
            _menuBackdrop.Add(shade);

            _menu = new VisualElement { name = "osc-release-menu" };
            _menu.AddToClassList("osc-release-card");

            var eyebrow = new Label("길을 잇고, 일상을 채우다.");
            eyebrow.AddToClassList("osc-release-eyebrow");
            _menu.Add(eyebrow);
            _menuTitle = new Label("OpenSimCity") { name = "osc-release-title" };
            _menuTitle.AddToClassList(UiFoundationNames.PanelTitleClass);
            _menuTitle.AddToClassList("osc-release-title");
            _menu.Add(_menuTitle);
            var subtitle = new Label("당신의 선택으로 자라나는 도시.");
            subtitle.AddToClassList(UiFoundationNames.PanelCaptionClass);
            subtitle.AddToClassList("osc-release-subtitle");
            _menu.Add(subtitle);

            var primaryActions = new VisualElement();
            primaryActions.AddToClassList("osc-release-primary-actions");
            var newCity = AddButton(primaryActions, "osc-release-new", "새 도시 시작   →", RequestNewCity);
            newCity.AddToClassList("osc-release-primary");
            _continue = AddButton(primaryActions, "osc-release-continue", "계속하기", ContinueCity);
            _menu.Add(primaryActions);

            var secondaryActions = new VisualElement();
            secondaryActions.AddToClassList("osc-release-secondary-actions");
            AddButton(secondaryActions, "osc-release-load", "불러오기", ToggleLoad);
            AddButton(secondaryActions, "osc-release-settings", "환경 설정", ToggleSettings);
            _quitButton = AddButton(secondaryActions, "osc-release-quit", "게임 종료", () => UnityEngine.Application.Quit());
            _menu.Add(secondaryActions);

            _loadSection = new ScrollView { name = "osc-release-load-section" };
            _loadSection.AddToClassList(UiFoundationNames.PanelClass);
            _loadSection.AddToClassList("osc-release-subsection");
            AddButton(_loadSection, "osc-release-load-back", "← 돌아가기", CloseDetails);
            _loadSection.Add(new Label("저장된 도시") { name = "osc-release-load-title" });
            _manualLoad = AddButton(_loadSection, "osc-release-load-manual", "수동 저장 불러오기", () => Load(CityPersistenceController.ManualSaveKey));
            _autosaveLoad = AddButton(_loadSection, "osc-release-load-autosave", "자동 저장 불러오기", () => Load(CityPersistenceController.AutosaveKey));
            _loadSection.style.display = DisplayStyle.None;

            _settingsSection = new ScrollView { name = "osc-release-settings-section" };
            _settingsSection.AddToClassList(UiFoundationNames.PanelClass);
            _settingsSection.AddToClassList("osc-release-subsection");
            AddButton(_settingsSection, "osc-release-settings-back", "← 돌아가기", CloseDetails);
            var settingsHeader = new Label("환경 설정");
            settingsHeader.AddToClassList(UiFoundationNames.PanelTitleClass);
            _settingsSection.Add(settingsHeader);
            _hints = new Toggle("튜토리얼 힌트 표시") { name = "osc-release-setting-hints" };
            _hints.RegisterValueChangedCallback(evt => _boundOnboarding?.SetHintsEnabled(evt.newValue));
            _settingsSection.Add(_hints);
            _settingsSection.style.display = DisplayStyle.None;

            _menuBackdrop.Add(_menu);
            _menuBackdrop.Add(_loadSection);
            _menuBackdrop.Add(_settingsSection);
            var footer = new Label($"도시 건설 시뮬레이션  /  v{UnityEngine.Application.version}");
            footer.AddToClassList("osc-title-footer");
            _menuBackdrop.Add(footer);
            _boundRoot.Add(_menuBackdrop);
            _menuBackdrop.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape) return;
                if (_settingsSection.style.display == DisplayStyle.Flex || _loadSection.style.display == DisplayStyle.Flex) CloseDetails();
                else if (_hasEnteredCity) ToggleMenu();
                evt.StopPropagation();
            });
            _menuBackdrop.schedule.Execute(() => { if (_menuOpen) newCity.Focus(); });

            _tutorial = new VisualElement { name = "osc-onboarding-card" };
            _tutorial.AddToClassList(UiFoundationNames.PanelClass);
            _tutorial.style.position = Position.Absolute;
            _tutorial.style.left = 12;
            _tutorial.style.top = 88;
            _tutorial.style.width = 320;
            _tutorial.style.paddingLeft = 12;
            _tutorial.style.paddingRight = 12;
            _tutorial.style.paddingTop = 10;
            _tutorial.style.paddingBottom = 10;
            _tutorial.AddToClassList("osc-onboarding-card");
            HookPointerBoundary(_tutorial);
            _tutorialTitle = new Label { name = "osc-onboarding-title" };
            _tutorialTitle.AddToClassList(UiFoundationNames.PanelTitleClass);
            _tutorialHint = new Label { name = "osc-onboarding-hint" };
            _tutorialHint.style.whiteSpace = WhiteSpace.Normal;
            _problemIndicator = new Label { name = "osc-problem-indicator" };
            _problemIndicator.style.whiteSpace = WhiteSpace.Normal;
            _problemDetails = AddButton(_tutorial, "osc-onboarding-problem-details", "! 원인 보기", OpenProblemDetails);
            _tutorial.Add(_tutorialTitle);
            _tutorial.Add(_tutorialHint);
            _tutorial.Add(_problemIndicator);
            _problemDetails.BringToFront();
            _boundRoot.Add(_tutorial);

            ApplyVisibility();
            InvalidateRefreshKey();
        }

        private void EnsureOpenButton()
        {
            var controls = _bootstrap.SimulationControls;
            if (controls == null || _openButton?.parent == controls) return;
            _openButton = controls.Q<Button>("osc-release-menu-open");
            if (_openButton == null)
            {
                _openButton = new Button(ToggleMenu) { name = "osc-release-menu-open", text = "메뉴", tooltip = "계속하기·새 도시·불러오기·설정" };
                _openButton.AddToClassList(UiFoundationNames.ItemClass);
                controls.Add(_openButton);
            }
        }

        private void BindContext(CityRuntimeContext context)
        {
            if (_boundContext != null) _boundContext.Committed -= HandleCommitted;
            _boundContext = context;
            if (_boundContext != null)
            {
                _boundContext.Committed += HandleCommitted;
                _boundOnboarding?.BindRuntime(_boundContext.Runtime);
            }
            InvalidateRefreshKey();
        }

        private void HandleCommitted(Application.Construction.ConstructionCommitResult result)
        {
            _boundOnboarding?.ObserveConstruction(result);
            InvalidateRefreshKey();
        }

        private void Refresh()
        {
            if (_menu == null || _tutorial == null) return;
            var session = ReleaseSessionContext.Current;
            var persistence = session?.Persistence;
            var onboarding = _boundOnboarding?.Snapshot;

            var hasPersistence = persistence != null;
            var persistenceState = hasPersistence
                ? persistence.State
                : default(CityPersistenceOperationState);
            var persistenceSlotCount = persistence?.Slots?.Count ?? 0;
            var hasOnboarding = onboarding != null;
            var onboardingTask = hasOnboarding
                ? onboarding.Task
                : default(OnboardingTaskKind);
            var hasProblem = onboarding?.HasProblem == true;
            var problemCode = hasProblem ? onboarding.ProblemCode ?? string.Empty : string.Empty;
            var hintsEnabled = onboarding?.HintsEnabled == true;

            if (_hasRefreshKey &&
                _lastHasPersistence == hasPersistence &&
                _lastPersistenceState == persistenceState &&
                _lastPersistenceSlotCount == persistenceSlotCount &&
                _lastHasOnboarding == hasOnboarding &&
                _lastOnboardingTask == onboardingTask &&
                _lastHasProblem == hasProblem &&
                string.Equals(_lastProblemCode, problemCode, StringComparison.Ordinal) &&
                _lastHintsEnabled == hintsEnabled &&
                _lastMenuOpen == _menuOpen)
            {
                return;
            }

            _lastHasPersistence = hasPersistence;
            _lastPersistenceState = persistenceState;
            _lastPersistenceSlotCount = persistenceSlotCount;
            _lastHasOnboarding = hasOnboarding;
            _lastOnboardingTask = onboardingTask;
            _lastHasProblem = hasProblem;
            _lastProblemCode = problemCode;
            _lastHintsEnabled = hintsEnabled;
            _lastMenuOpen = _menuOpen;
            _hasRefreshKey = true;

            var manual = FindSlot(persistence, CityPersistenceController.ManualSaveKey);
            var autosave = FindSlot(persistence, CityPersistenceController.AutosaveKey);
            var canContinue = (manual?.HasSave == true || autosave?.HasSave == true) && persistence?.IsBusy != true;
            _continue?.SetEnabled(_hasEnteredCity || canContinue);
            _manualLoad?.SetEnabled(manual?.HasSave == true && persistence?.IsBusy != true);
            _autosaveLoad?.SetEnabled(autosave?.HasSave == true && persistence?.IsBusy != true);

            if (onboarding != null)
            {
                _hints?.SetValueWithoutNotify(onboarding.HintsEnabled);
                _tutorialTitle.text = onboarding.Title;
                _tutorialHint.text = onboarding.HintsEnabled ? onboarding.Hint : string.Empty;
                _problemIndicator.text = onboarding.HasProblem ? "! 현재 개발 문제: " + ProblemName(onboarding.ProblemCode) : "✓ 현재 대표 개발 문제 없음";
                _problemDetails.style.display = onboarding.HasProblem ? DisplayStyle.Flex : DisplayStyle.None;
            }
            ApplyVisibility();
        }

        private void InvalidateRefreshKey()
        {
            _hasRefreshKey = false;
        }

        private void ContinueCity()
        {
            if (_hasEnteredCity) { ToggleMenu(); return; }
            if (ReleaseSessionContext.Current?.ContinueCity() == true) EnterCity();
        }

        public void RequestNewCity()
        {
            if (ReleaseSessionContext.Current?.StartNewCity() == true) EnterCity();
        }

        private void Load(string saveKey)
        {
            if (ReleaseSessionContext.Current?.LoadCity(saveKey) == true) EnterCity();
        }

        private void EnterCity()
        {
            _hasEnteredCity = true;
            _menuOpen = false;
            _loadSection.style.display = DisplayStyle.None;
            _settingsSection.style.display = DisplayStyle.None;
            ApplyVisibility();
            InvalidateRefreshKey();
        }

        private void OpenProblemDetails()
        {
            CityRuntimeContext.Current?.SetOverlay(PlacementOverlayKind.Parcel);
            _boundOnboarding?.MarkProblemDetailsViewed();
            InvalidateRefreshKey();
        }

        private void ToggleLoad()
        {
            _loadSection.style.display = _loadSection.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            _settingsSection.style.display = DisplayStyle.None;
            SyncDetailVisibility();
        }

        public void ToggleSettings()
        {
            _settingsSection.style.display = _settingsSection.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            _loadSection.style.display = DisplayStyle.None;
            SyncDetailVisibility();
        }

        private void CloseDetails()
        {
            _settingsSection.style.display = DisplayStyle.None;
            _loadSection.style.display = DisplayStyle.None;
            SyncDetailVisibility();
            _menu.Q<Button>("osc-release-new")?.Focus();
        }

        private void SyncDetailVisibility()
        {
            _menuBackdrop.EnableInClassList("osc-release-detail-open",
                _settingsSection.style.display == DisplayStyle.Flex || _loadSection.style.display == DisplayStyle.Flex);
        }

        public void ToggleMenu()
        {
            if (!_hasEnteredCity) return;
            _menuOpen = !_menuOpen;
            CloseDetails();
            var runtime = CityRuntimeContext.Current?.Runtime;
            if (runtime != null)
            {
                if (_menuOpen)
                {
                    _resumeSpeed = runtime.Clock.Speed;
                    runtime.SetSimulationSpeed(SimulationSpeed.Paused);
                }
                else runtime.SetSimulationSpeed(_resumeSpeed);
            }
            ApplyVisibility();
            InvalidateRefreshKey();
        }

        private void ApplyVisibility()
        {
            if (_menuBackdrop != null)
            {
                _menuBackdrop.EnableInClassList("osc-title-screen", !_hasEnteredCity);
                _menuBackdrop.EnableInClassList("osc-pause-screen", _hasEnteredCity);
                _menu.EnableInClassList(UiFoundationNames.PanelClass, _hasEnteredCity);
                _menuTitle.text = _hasEnteredCity ? "도시 메뉴" : "OpenSimCity";
                _quitButton.style.display = !_hasEnteredCity ? DisplayStyle.Flex : DisplayStyle.None;
                _menuBackdrop.style.display = _menuOpen ? DisplayStyle.Flex : DisplayStyle.None;
                if (_menuOpen) _menuBackdrop.BringToFront();
            }

            if (_boundRoot != null)
            foreach (var child in _boundRoot.Children())
            {
                if (child == _menuBackdrop || child == _tutorial) continue;
                child.style.visibility = IsTitleScreen ? Visibility.Hidden : Visibility.Visible;
            }

            if (_tutorial != null)
            {
                var showTutorial = !_menuOpen && _boundOnboarding?.Snapshot != null;
                _tutorial.style.display = showTutorial ? DisplayStyle.Flex : DisplayStyle.None;
                if (showTutorial) _tutorial.BringToFront();
            }

            UiSystemPanelDock.Sync(_boundRoot?.parent, _bootstrap?.SystemPanelDock, _menuOpen);
            if (_menuOpen) CapturePointer();
            else ReleasePointer();
            _openButton?.EnableInClassList(UiFoundationNames.ItemSelectedClass, _menuOpen);
        }

        private void HookPointerBoundary(VisualElement element)
        {
            element.RegisterCallback<PointerEnterEvent>(_ => CapturePointer());
            element.RegisterCallback<PointerLeaveEvent>(_ => ReleasePointer());
        }

        private void CapturePointer()
        {
            if (_pointerCaptured) return;
            UiInputBoundary.Current.CapturePointer();
            _pointerCaptured = true;
        }

        private void ReleasePointer()
        {
            if (!_pointerCaptured) return;
            UiInputBoundary.Current.ReleasePointer();
            _pointerCaptured = false;
        }

        private static Button AddButton(VisualElement parent, string name, string text, Action action)
        {
            var button = new Button(action) { name = name, text = text };
            button.AddToClassList(UiFoundationNames.ItemClass);
            button.AddToClassList("osc-release-button");
            parent.Add(button);
            return button;
        }

        private static CitySaveSlotViewModel FindSlot(CityPersistenceViewModel model, string key)
        {
            if (model?.Slots == null) return null;
            for (var index = 0; index < model.Slots.Count; index++)
                if (string.Equals(model.Slots[index].SaveKey, key, StringComparison.Ordinal)) return model.Slots[index];
            return null;
        }

        private static string ProblemName(string code) => code == "RoadIneligible" ? "외부 도로 미연결"
            : code == "NoPowerCapacity" ? "연결된 전력 용량 부족" : code;
    }
}
