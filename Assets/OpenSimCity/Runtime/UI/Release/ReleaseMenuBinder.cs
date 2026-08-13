using System;
using OpenSimCity.Application.Input;
using OpenSimCity.Application.Onboarding;
using OpenSimCity.Application.Overlays;
using OpenSimCity.Application.Persistence;
using OpenSimCity.Application.World;
using OpenSimCity.UI.Foundation;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Release
{
    [DisallowMultipleComponent]
    public sealed class ReleaseMenuBinder : MonoBehaviour
    {
        private UiFoundationBootstrap _bootstrap;
        private VisualElement _boundRoot;
        private CityRuntimeContext _boundContext;
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
        private bool _menuOpen = true;
        private bool _pointerCaptured;
        private string _lastRefreshKey;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<UiFoundationBootstrap>();
            if (bootstrap != null && bootstrap.GetComponent<ReleaseMenuBinder>() == null)
                bootstrap.gameObject.AddComponent<ReleaseMenuBinder>();
        }

        private void OnEnable()
        {
            _bootstrap = GetComponent<UiFoundationBootstrap>();
        }

        private void OnDisable()
        {
            BindContext(null);
            ReleasePointer();
            _menu?.RemoveFromHierarchy();
            _tutorial?.RemoveFromHierarchy();
            _menu = null;
            _tutorial = null;
            _boundRoot = null;
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
            var context = CityRuntimeContext.Current;
            if (!ReferenceEquals(context, _boundContext)) BindContext(context);
            var onboarding = OnboardingContext.Current;
            if (onboarding != null && context != null)
            {
                onboarding.BindRuntime(context.Runtime);
                onboarding.Refresh();
            }
            Refresh();
        }

        private void BuildUi()
        {
            _menu?.RemoveFromHierarchy();
            _tutorial?.RemoveFromHierarchy();

            _menu = new VisualElement { name = "osc-release-menu" };
            _menu.AddToClassList(UiFoundationNames.PanelClass);
            _menu.style.position = Position.Absolute;
            _menu.style.left = 24;
            _menu.style.top = 72;
            _menu.style.width = 360;
            _menu.style.paddingLeft = 16;
            _menu.style.paddingRight = 16;
            _menu.style.paddingTop = 16;
            _menu.style.paddingBottom = 16;
            HookPointerBoundary(_menu);

            var title = new Label("OpenSimCity") { name = "osc-release-title" };
            title.AddToClassList(UiFoundationNames.PanelTitleClass);
            _menu.Add(title);
            var subtitle = new Label("도시를 계속하거나 새 도시를 시작하세요.");
            subtitle.AddToClassList(UiFoundationNames.PanelCaptionClass);
            _menu.Add(subtitle);

            _continue = AddButton(_menu, "osc-release-continue", "계속하기", ContinueCity);
            AddButton(_menu, "osc-release-new", "새 도시", StartNewCity);
            AddButton(_menu, "osc-release-load", "불러오기", ToggleLoad);
            AddButton(_menu, "osc-release-settings", "설정", ToggleSettings);
            AddButton(_menu, "osc-release-close", "도시로 돌아가기", ToggleMenu);

            _loadSection = new VisualElement { name = "osc-release-load-section" };
            _manualLoad = AddButton(_loadSection, "osc-release-load-manual", "수동 저장 불러오기", () => Load(CityPersistenceController.ManualSaveKey));
            _autosaveLoad = AddButton(_loadSection, "osc-release-load-autosave", "자동 저장 불러오기", () => Load(CityPersistenceController.AutosaveKey));
            _loadSection.style.display = DisplayStyle.None;
            _menu.Add(_loadSection);

            _settingsSection = new VisualElement { name = "osc-release-settings-section" };
            _hints = new Toggle("튜토리얼 힌트 표시") { name = "osc-release-setting-hints" };
            _hints.RegisterValueChangedCallback(evt => OnboardingContext.Current?.SetHintsEnabled(evt.newValue));
            _settingsSection.Add(_hints);
            _settingsSection.style.display = DisplayStyle.None;
            _menu.Add(_settingsSection);
            _boundRoot.Add(_menu);

            _tutorial = new VisualElement { name = "osc-onboarding-card" };
            _tutorial.AddToClassList(UiFoundationNames.PanelClass);
            _tutorial.style.position = Position.Absolute;
            _tutorial.style.left = 24;
            _tutorial.style.top = 72;
            _tutorial.style.width = 360;
            _tutorial.style.paddingLeft = 12;
            _tutorial.style.paddingRight = 12;
            _tutorial.style.paddingTop = 10;
            _tutorial.style.paddingBottom = 10;
            HookPointerBoundary(_tutorial);
            _tutorialTitle = new Label { name = "osc-onboarding-title" };
            _tutorialTitle.AddToClassList(UiFoundationNames.PanelTitleClass);
            _tutorialHint = new Label { name = "osc-onboarding-hint" };
            _problemIndicator = new Label { name = "osc-problem-indicator" };
            _problemDetails = AddButton(_tutorial, "osc-onboarding-problem-details", "! 원인 보기", OpenProblemDetails);
            _tutorial.Add(_tutorialTitle);
            _tutorial.Add(_tutorialHint);
            _tutorial.Add(_problemIndicator);
            _problemDetails.BringToFront();
            _boundRoot.Add(_tutorial);

            ApplyVisibility();
            _lastRefreshKey = null;
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
                OnboardingContext.Current?.BindRuntime(_boundContext.Runtime);
            }
            _lastRefreshKey = null;
        }

        private void HandleCommitted(Application.Construction.ConstructionCommitResult result)
        {
            OnboardingContext.Current?.ObserveConstruction(result);
            _lastRefreshKey = null;
        }

        private void Refresh()
        {
            if (_menu == null || _tutorial == null) return;
            var session = ReleaseSessionContext.Current;
            var persistence = session?.Persistence;
            var onboarding = OnboardingContext.Current?.Snapshot;
            var key = string.Concat(
                persistence?.State.ToString() ?? "none", ":",
                persistence?.Slots?.Count ?? 0, ":",
                onboarding?.Task.ToString() ?? "none", ":",
                onboarding?.HasProblem == true ? onboarding.ProblemCode : "none", ":",
                onboarding?.HintsEnabled == true ? "1" : "0", ":",
                _menuOpen ? "open" : "closed");
            if (string.Equals(key, _lastRefreshKey, StringComparison.Ordinal)) return;
            _lastRefreshKey = key;

            var manual = FindSlot(persistence, CityPersistenceController.ManualSaveKey);
            var autosave = FindSlot(persistence, CityPersistenceController.AutosaveKey);
            var canContinue = (manual?.HasSave == true || autosave?.HasSave == true) && persistence?.IsBusy != true;
            _continue?.SetEnabled(canContinue);
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

        private void ContinueCity()
        {
            if (ReleaseSessionContext.Current?.ContinueCity() == true) { _menuOpen = false; ApplyVisibility(); }
        }

        private void StartNewCity()
        {
            if (ReleaseSessionContext.Current?.StartNewCity() == true) { _menuOpen = false; ApplyVisibility(); }
        }

        private void Load(string saveKey)
        {
            if (ReleaseSessionContext.Current?.LoadCity(saveKey) == true) { _menuOpen = false; ApplyVisibility(); }
        }

        private void OpenProblemDetails()
        {
            CityRuntimeContext.Current?.SetOverlay(PlacementOverlayKind.Parcel);
            OnboardingContext.Current?.MarkProblemDetailsViewed();
            _lastRefreshKey = null;
        }

        private void ToggleLoad()
        {
            _loadSection.style.display = _loadSection.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            _settingsSection.style.display = DisplayStyle.None;
        }

        private void ToggleSettings()
        {
            _settingsSection.style.display = _settingsSection.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            _loadSection.style.display = DisplayStyle.None;
        }

        private void ToggleMenu()
        {
            _menuOpen = !_menuOpen;
            ApplyVisibility();
            _lastRefreshKey = null;
        }

        private void ApplyVisibility()
        {
            if (_menu != null) _menu.style.display = _menuOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (_tutorial != null)
            {
                var showTutorial = !_menuOpen && OnboardingContext.Current?.Snapshot != null;
                _tutorial.style.display = showTutorial ? DisplayStyle.Flex : DisplayStyle.None;
            }
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
