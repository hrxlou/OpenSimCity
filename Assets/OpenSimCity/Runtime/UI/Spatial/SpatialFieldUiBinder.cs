using System;
using OpenSimCity.Application.Input;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.Spatial;
using OpenSimCity.Application.World;
using OpenSimCity.UI.Foundation;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Spatial
{
    /// <summary>Player-facing selector and textual legend for P6 spatial fields.</summary>
    [DisallowMultipleComponent]
    public sealed class SpatialFieldUiBinder : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        private CityRuntimeContext _context;
        private SpatialFieldRuntimeModule _module;
        private VisualElement _panel;
        private Label _title;
        private Label _summary;
        private Label _legend;
        private bool _pointerCaptured;
        private bool _keyboardCaptured;
        private int _observedReadVersion = -1;
        private int _observedSelectionVersion = -1;

        public VisualElement Panel => _panel;

        public void Configure(UIDocument target)
        {
            document = target;
            RebuildPanel();
        }

        private void OnEnable()
        {
            if (document == null)
                document = GetComponent<UIDocument>();
            RebuildPanel();
        }

        private void OnDisable()
        {
            ReleaseInputCaptures();
            _panel?.RemoveFromHierarchy();
            _panel = null;
        }

        private void Update()
        {
            BindRuntimeIfNeeded();
            if (_module == null)
                return;
            _module.RefreshIfNeeded();
            var selection = SpatialFieldOverlaySelection.Current;
            var readVersion = _module.ReadModel.Snapshot.Version;
            if (_observedReadVersion == readVersion &&
                _observedSelectionVersion == selection.Version)
            {
                return;
            }
            _observedReadVersion = readVersion;
            _observedSelectionVersion = selection.Version;
            UpdateText();
        }

        private void BindRuntimeIfNeeded()
        {
            var current = CityRuntimeContext.Current;
            if (ReferenceEquals(current, _context))
                return;
            _context = current;
            _module = current == null ? null : SpatialFieldRuntimeRegistry.GetOrCreate(current.Runtime);
            _observedReadVersion = -1;
            _observedSelectionVersion = -1;
            UpdateText();
        }

        private void RebuildPanel()
        {
            if (!isActiveAndEnabled || document == null || document.rootVisualElement == null)
                return;
            _panel?.RemoveFromHierarchy();
            _panel = new VisualElement { name = "osc-spatial-fields-panel" };
            _panel.style.paddingLeft = 10;
            _panel.style.paddingRight = 10;
            _panel.style.paddingTop = 8;
            _panel.style.paddingBottom = 8;
            _panel.style.backgroundColor = new Color(0.055f, 0.075f, 0.10f, 0.94f);
            _panel.style.borderTopLeftRadius = 6;
            _panel.style.borderTopRightRadius = 6;
            _panel.style.borderBottomLeftRadius = 6;
            _panel.style.borderBottomRightRadius = 6;

            _title = new Label("공간 데이터맵") { name = "osc-spatial-title" };
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _title.style.fontSize = 15;
            _panel.Add(_title);

            var rowA = Row();
            rowA.Add(Button("토지가치", () => Show(SpatialFieldViewKind.LandValue)));
            rowA.Add(Button("오염", () => Show(SpatialFieldViewKind.Pollution)));
            rowA.Add(Button("소음", () => Show(SpatialFieldViewKind.Noise)));
            _panel.Add(rowA);
            var rowB = Row();
            rowB.Add(Button("범죄", () => Show(SpatialFieldViewKind.Crime)));
            rowB.Add(Button("공원 효과", () => Show(SpatialFieldViewKind.ParkAmenity)));
            rowB.Add(Button("끄기", () => SpatialFieldOverlaySelection.Current.Hide()));
            _panel.Add(rowB);

            _summary = new Label { name = "osc-spatial-summary" };
            _summary.style.whiteSpace = WhiteSpace.Normal;
            _panel.Add(_summary);
            _legend = new Label { name = "osc-spatial-legend" };
            _legend.style.whiteSpace = WhiteSpace.Normal;
            _panel.Add(_legend);

            _panel.RegisterCallback<PointerEnterEvent>(_ => CapturePointer());
            _panel.RegisterCallback<PointerLeaveEvent>(_ => ReleasePointer());
            _panel.RegisterCallback<FocusInEvent>(_ => CaptureKeyboard());
            _panel.RegisterCallback<FocusOutEvent>(_ => ReleaseKeyboard());
            UiSystemPanelDock.Dock(document.rootVisualElement, _panel);
            UpdateText();
        }

        private void Show(SpatialFieldViewKind kind)
        {
            SpatialFieldOverlaySelection.Current.Show(kind);
            UpdateText();
        }

        private void UpdateText()
        {
            if (_summary == null || _legend == null || _title == null)
                return;
            if (_module == null)
            {
                _summary.text = "도시 runtime을 기다리는 중";
                _legend.text = string.Empty;
                return;
            }

            var selection = SpatialFieldOverlaySelection.Current;
            var read = _module.ReadModel.Snapshot;
            _summary.text =
                $"source {read.Sources.Count} · 최근 dirty 셀 {read.LastRefreshCellCount} · " +
                $"청크 {read.LastRefreshChunkCount}\nchecksum 0x{read.Checksum:X16}";
            if (!selection.IsVisible)
            {
                _title.text = "공간 데이터맵 · 꺼짐";
                _legend.text = "색상과 함께 점·실선·원형·사선·교차선 band를 사용합니다.";
                return;
            }

            var overlay = _module.Overlay.Build(selection.Kind);
            _title.text = $"공간 데이터맵 · {overlay.Title}";
            var direction = overlay.HigherIsAdverse
                ? "높을수록 불리함"
                : "높을수록 유리함/효과 큼";
            var text = direction;
            for (var index = 0; index < overlay.Legend.Count; index++)
            {
                var entry = overlay.Legend[index];
                text += $"\n{entry.Minimum}–{entry.Maximum}: {entry.Label}";
            }
            _legend.text = text;
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            return row;
        }

        private static Button Button(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.marginRight = 4;
            button.style.marginBottom = 4;
            return button;
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

        private void CaptureKeyboard()
        {
            if (_keyboardCaptured) return;
            UiInputBoundary.Current.CaptureKeyboardFocus();
            _keyboardCaptured = true;
        }

        private void ReleaseKeyboard()
        {
            if (!_keyboardCaptured) return;
            UiInputBoundary.Current.ReleaseKeyboardFocus();
            _keyboardCaptured = false;
        }

        private void ReleaseInputCaptures()
        {
            ReleasePointer();
            ReleaseKeyboard();
        }
    }
}
