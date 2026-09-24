using System;
using System.Collections.Generic;
using OpenSimCity.Application.Settings;
using OpenSimCity.UI.Foundation;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Release
{
    [DisallowMultipleComponent]
    public sealed class AccessibilitySettingsBinder : MonoBehaviour
    {
        private UiFoundationBootstrap _bootstrap;
        private PresentationSettingsController _controller;
        private VisualElement _settingsPanel;
        private VisualElement _section;
        private DropdownField _scale;
        private DropdownField _mode;
        private Toggle _focus;
        private DropdownField _action;
        private DropdownField _key;
        private Label _feedback;
        private StyleSheet _style;
        private PanelSettings _panelSettings;
        private float _originalPanelScale;
        private readonly List<PresentationInputAction> _actions = new List<PresentationInputAction>();
        private readonly Dictionary<string, string> _paths = new Dictionary<string, string>(StringComparer.Ordinal);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<UiFoundationBootstrap>();
            if (bootstrap != null && bootstrap.GetComponent<AccessibilitySettingsBinder>() == null)
                bootstrap.gameObject.AddComponent<AccessibilitySettingsBinder>();
        }

        private void OnEnable() => _bootstrap = GetComponent<UiFoundationBootstrap>();

        private void OnDisable()
        {
            if (_panelSettings != null) _panelSettings.scale = _originalPanelScale;
            _panelSettings = null;
            BindController(null);
            _section?.RemoveFromHierarchy();
        }

        private void Update()
        {
            if (_bootstrap == null) _bootstrap = GetComponent<UiFoundationBootstrap>();
            if (_bootstrap?.Root == null) return;
            if (!ReferenceEquals(_controller, PresentationSettingsContext.Current))
                BindController(PresentationSettingsContext.Current);
            var panel = _bootstrap.Root.Q<VisualElement>("osc-release-settings-section");
            if (!ReferenceEquals(panel, _settingsPanel))
            {
                _section?.RemoveFromHierarchy();
                _settingsPanel = panel;
                if (panel != null) BuildSection();
            }
            ApplyPresentation();
        }

        private void BindController(PresentationSettingsController controller)
        {
            if (_controller != null) _controller.Changed -= HandleChanged;
            _controller = controller;
            if (_controller != null) _controller.Changed += HandleChanged;
            RefreshControls();
        }

        private void HandleChanged(PresentationSettingsDto _) => RefreshControls();

        private void BuildSection()
        {
            _section = new VisualElement { name = "osc-accessibility-settings" };
            var title = new Label("접근성 · 입력");
            title.AddToClassList(UiFoundationNames.PanelTitleClass);
            _section.Add(title);

            _scale = new DropdownField("UI 크기", new List<string> { "100%", "125%", "150%" }, 0);
            _scale.RegisterValueChangedCallback(evt => _controller?.SetUiScale(
                evt.newValue == "150%" ? UiScalePercent.Percent150 :
                evt.newValue == "125%" ? UiScalePercent.Percent125 : UiScalePercent.Percent100));
            _section.Add(_scale);

            _mode = new DropdownField("상태 표시", new List<string> { "표준", "색상+형태", "고대비" }, 0);
            _mode.RegisterValueChangedCallback(evt => _controller?.SetVisualMode(
                evt.newValue == "고대비" ? AccessibilityVisualMode.HighContrast :
                evt.newValue == "색상+형태" ? AccessibilityVisualMode.ColorIndependent : AccessibilityVisualMode.Standard));
            _section.Add(_mode);

            _focus = new Toggle("키보드 포커스 테두리 강화");
            _focus.RegisterValueChangedCallback(evt => _controller?.SetStrongFocusVisible(evt.newValue));
            _section.Add(_focus);

            var mouse = new Label("마우스: 좌클릭 선택/건설 · 중클릭 이동 · 휠 확대/축소");
            mouse.AddToClassList(UiFoundationNames.PanelCaptionClass);
            _section.Add(mouse);

            _actions.Clear();
            var actionLabels = new List<string>();
            var actions = PresentationInputBindingDefaults.RemappableActions;
            for (var index = 0; index < actions.Count; index++) { _actions.Add(actions[index]); actionLabels.Add(actions[index].ToString()); }
            _action = new DropdownField("키보드 동작", actionLabels, 0);
            _action.RegisterValueChangedCallback(_ => SyncKey());
            _section.Add(_action);

            _paths.Clear();
            var keyLabels = new List<string>();
            var allowed = PresentationInputBindingDefaults.AllowedKeyboardPaths;
            for (var index = 0; index < allowed.Count; index++)
            {
                var label = KeyLabel(allowed[index]);
                if (_paths.ContainsKey(label)) continue;
                _paths.Add(label, allowed[index]);
                keyLabels.Add(label);
            }
            _key = new DropdownField("키", keyLabels, 0);
            _section.Add(_key);

            var apply = new Button(ApplyRemap) { text = "키 변경 적용" };
            apply.AddToClassList(UiFoundationNames.ItemClass);
            _section.Add(apply);
            var reset = new Button(() => _controller?.ResetToDefaults()) { text = "접근성/입력 기본값 복원" };
            reset.AddToClassList(UiFoundationNames.ItemClass);
            _section.Add(reset);
            _feedback = new Label();
            _feedback.AddToClassList(UiFoundationNames.PanelCaptionClass);
            _section.Add(_feedback);
            panelAdd();
            RefreshControls();
        }

        private void panelAdd() => _settingsPanel.Add(_section);

        private void ApplyRemap()
        {
            if (_controller == null || _action == null || _key == null || _action.index < 0 || _action.index >= _actions.Count) return;
            if (!_paths.TryGetValue(_key.value, out var path)) return;
            var action = _actions[_action.index];
            _feedback.text = _controller.TryRemap(action, path, out var error)
                ? "✓ " + action + " → " + _key.value
                : "! " + error;
        }

        private void RefreshControls()
        {
            if (_controller == null || _section == null) return;
            _scale.SetValueWithoutNotify(_controller.UiScale == UiScalePercent.Percent150 ? "150%" : _controller.UiScale == UiScalePercent.Percent125 ? "125%" : "100%");
            _mode.SetValueWithoutNotify(_controller.VisualMode == AccessibilityVisualMode.HighContrast ? "고대비" : _controller.VisualMode == AccessibilityVisualMode.ColorIndependent ? "색상+형태" : "표준");
            _focus.SetValueWithoutNotify(_controller.StrongFocusVisible);
            SyncKey();
            ApplyPresentation();
        }

        private void SyncKey()
        {
            if (_controller == null || _key == null || _action == null || _action.index < 0 || _action.index >= _actions.Count) return;
            _key.SetValueWithoutNotify(KeyLabel(_controller.GetControlPath(_actions[_action.index])));
        }

        private void ApplyPresentation()
        {
            if (_bootstrap?.Root == null || _controller == null) return;
            if (_style == null) _style = Resources.Load<StyleSheet>("UI/Accessibility");
            if (_style != null && !_bootstrap.Root.styleSheets.Contains(_style)) _bootstrap.Root.styleSheets.Add(_style);
            var scale = (float)(int)_controller.UiScale / 100f;
            if (_panelSettings == null)
            {
                _panelSettings = _bootstrap.GetComponent<UIDocument>().panelSettings;
                _originalPanelScale = _panelSettings.scale;
            }
            // Panel scaling recomputes usable layout bounds; a root transform
            // only magnifies already-laid-out content beyond the viewport.
            _panelSettings.scale = _originalPanelScale * scale;
            _bootstrap.Root.EnableInClassList("osc-accessibility--color-independent", _controller.VisualMode != AccessibilityVisualMode.Standard);
            _bootstrap.Root.EnableInClassList("osc-accessibility--high-contrast", _controller.VisualMode == AccessibilityVisualMode.HighContrast);
            _bootstrap.Root.EnableInClassList("osc-accessibility--focus-strong", _controller.StrongFocusVisible);
        }

        private static string KeyLabel(string path)
        {
            const string prefix = "<Keyboard>/";
            return path != null && path.StartsWith(prefix, StringComparison.Ordinal) ? path.Substring(prefix.Length) : path ?? string.Empty;
        }
    }
}
