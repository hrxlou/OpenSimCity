using System.Collections.Generic;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.Overlays;
using OpenSimCity.Application.World;
using OpenSimCity.UI.Foundation;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Placement
{
    /// <summary>
    /// Binds the P2 placement state to the four shells: the Tool Rail selects the
    /// road/zone/demolish tool and toggles overlays, the Build Palette shows the
    /// selected tool's subtype, cost and shortcut, the Context Inspector shows the
    /// live preview and its failure reason, and the Data Overlay Legend names the
    /// active overlay's categories with a non-color pattern channel.
    /// <para>
    /// It reads the ambient <see cref="CityRuntimeContext"/> and only shows values
    /// for systems implemented in P2 (Docs/DESIGN.md 3.1).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlacementUiBinder : MonoBehaviour
    {
        [SerializeField] private UiFoundationBootstrap bootstrap;

        private VisualElement _boundRoot;
        private CityRuntimeContext _boundContext;

        private readonly Dictionary<ConstructionToolMode, VisualElement> _toolButtons =
            new Dictionary<ConstructionToolMode, VisualElement>();
        private readonly Dictionary<PlacementOverlayKind, VisualElement> _overlayButtons =
            new Dictionary<PlacementOverlayKind, VisualElement>();

        private Label _paletteTool;
        private Label _paletteCost;
        private Label _paletteShortcut;
        private Label _inspectorOperation;
        private Label _inspectorCost;
        private Label _inspectorBalance;
        private Label _inspectorStatusMarker;
        private Label _inspectorStatus;
        private VisualElement _legendList;
        private Label _legendTitle;

        public bool IsBound => _boundContext != null && _boundRoot != null;

        public void ConfigureReferences(UiFoundationBootstrap uiBootstrap)
        {
            bootstrap = uiBootstrap;
        }

        private void OnDisable()
        {
            UnbindContext();
            _boundRoot = null;
        }

        private void Update()
        {
            if (bootstrap == null)
                return;

            // The bootstrap rebuilds its tree on enable, so re-inject content when
            // the root instance changes.
            if (!ReferenceEquals(bootstrap.Root, _boundRoot))
            {
                _boundRoot = bootstrap.Root;
                if (_boundRoot != null)
                    BuildContent();
            }

            var context = CityRuntimeContext.Current;
            if (!ReferenceEquals(context, _boundContext))
                BindContext(context);
        }

        private void BuildContent()
        {
            _toolButtons.Clear();
            _overlayButtons.Clear();
            BuildToolRail();
            BuildPalette();
            BuildInspector();
            BuildLegend();
            RefreshAll();
        }

        private void BuildToolRail()
        {
            var rail = bootstrap.ToolRail;
            if (rail == null)
                return;

            var content = ReplaceContent(rail, "osc-placement-tools");

            AddToolButton(content, ConstructionToolMode.Road, "도로", "1");
            AddToolButton(content, ConstructionToolMode.ZoneResidential, "주거 R", "2");
            AddToolButton(content, ConstructionToolMode.ZoneCommercial, "상업 C", "3");
            AddToolButton(content, ConstructionToolMode.ZoneIndustrial, "공업 I", "4");
            AddToolButton(content, ConstructionToolMode.ZoneClear, "구역 해제", "5");
            AddToolButton(content, ConstructionToolMode.Demolish, "철거", "6");

            var overlayHeader = new Label("오버레이");
            overlayHeader.AddToClassList(UiFoundationNames.PanelCaptionClass);
            content.Add(overlayHeader);

            AddOverlayButton(content, PlacementOverlayKind.RoadConnectivity, "연결");
            AddOverlayButton(content, PlacementOverlayKind.Accessibility, "접근성");
            AddOverlayButton(content, PlacementOverlayKind.Zone, "구역맵");
            AddOverlayButton(content, PlacementOverlayKind.Parcel, "필지");
        }

        private void AddToolButton(VisualElement parent, ConstructionToolMode mode, string label, string shortcut)
        {
            var button = MakeItemButton($"{label}  ·  {shortcut}", () => CityRuntimeContext.Current?.SelectTool(mode));
            parent.Add(button);
            _toolButtons[mode] = button;
        }

        private void AddOverlayButton(VisualElement parent, PlacementOverlayKind kind, string label)
        {
            var button = MakeItemButton(label, () => CityRuntimeContext.Current?.ToggleOverlay(kind));
            parent.Add(button);
            _overlayButtons[kind] = button;
        }

        private void BuildPalette()
        {
            var palette = bootstrap.BuildPalette;
            if (palette == null)
                return;

            var content = ReplaceContent(palette, "osc-placement-palette");
            _paletteTool = AddValueLabel(content, UiFoundationNames.PanelTitleClass);
            _paletteShortcut = AddValueLabel(content, UiFoundationNames.PanelCaptionClass);
            _paletteCost = AddValueLabel(content, UiFoundationNames.PanelCaptionClass);
        }

        private void BuildInspector()
        {
            var inspector = bootstrap.ContextInspector;
            if (inspector == null)
                return;

            var content = ReplaceContent(inspector, "osc-placement-inspector");
            _inspectorOperation = AddValueLabel(content, UiFoundationNames.PanelCaptionClass);
            _inspectorCost = AddValueLabel(content, UiFoundationNames.PanelCaptionClass);
            _inspectorBalance = AddValueLabel(content, UiFoundationNames.PanelCaptionClass);

            var statusRow = new VisualElement { name = "osc-placement-status" };
            statusRow.style.flexDirection = FlexDirection.Row;
            _inspectorStatusMarker = new Label(string.Empty);
            _inspectorStatusMarker.AddToClassList(UiFoundationNames.ItemMarkerClass);
            _inspectorStatusMarker.style.display = DisplayStyle.Flex;
            _inspectorStatus = new Label(string.Empty);
            statusRow.Add(_inspectorStatusMarker);
            statusRow.Add(_inspectorStatus);
            content.Add(statusRow);
        }

        private void BuildLegend()
        {
            var legend = bootstrap.DataOverlayLegend;
            if (legend == null)
                return;

            var content = ReplaceContent(legend, "osc-placement-legend");
            _legendTitle = AddValueLabel(content, UiFoundationNames.PanelCaptionClass);
            _legendList = new VisualElement { name = "osc-placement-legend-list" };
            content.Add(_legendList);
        }

        private void BindContext(CityRuntimeContext context)
        {
            UnbindContext();
            _boundContext = context;
            if (_boundContext == null)
            {
                RefreshAll();
                return;
            }

            _boundContext.ToolSelected += HandleToolSelected;
            _boundContext.PreviewChanged += HandlePreviewChanged;
            _boundContext.Committed += HandleCommitted;
            _boundContext.OverlayChanged += HandleOverlayChanged;
            RefreshAll();
        }

        private void UnbindContext()
        {
            if (_boundContext == null)
                return;

            _boundContext.ToolSelected -= HandleToolSelected;
            _boundContext.PreviewChanged -= HandlePreviewChanged;
            _boundContext.Committed -= HandleCommitted;
            _boundContext.OverlayChanged -= HandleOverlayChanged;
            _boundContext = null;
        }

        private void HandleToolSelected(ConstructionToolMode mode)
        {
            RefreshToolSelection();
            RefreshPalette();
        }

        private void HandlePreviewChanged(ConstructionPreview preview)
        {
            RefreshPalette();
            RefreshInspector(preview, committed: null);
        }

        private void HandleCommitted(ConstructionCommitResult result)
        {
            RefreshInspector(_boundContext?.Tool.Preview, result);
            RefreshOverlaySelection();
        }

        private void HandleOverlayChanged(PlacementOverlaySnapshot snapshot)
        {
            RefreshLegend(snapshot);
            RefreshOverlaySelection();
        }

        private void RefreshAll()
        {
            RefreshToolSelection();
            RefreshOverlaySelection();
            RefreshPalette();
            RefreshInspector(_boundContext?.Tool.Preview, committed: null);
            RefreshLegend(_boundContext?.ActiveOverlaySnapshot);
        }

        private void RefreshToolSelection()
        {
            var mode = _boundContext?.Tool.Mode ?? ConstructionToolMode.None;
            foreach (var pair in _toolButtons)
                pair.Value.EnableInClassList(UiFoundationNames.ItemSelectedClass, pair.Key == mode);
        }

        private void RefreshOverlaySelection()
        {
            var hasOverlay = _boundContext != null && _boundContext.HasActiveOverlay;
            var active = _boundContext?.ActiveOverlay ?? PlacementOverlayKind.RoadConnectivity;
            foreach (var pair in _overlayButtons)
            {
                pair.Value.EnableInClassList(
                    UiFoundationNames.ItemSelectedClass, hasOverlay && pair.Key == active);
            }
        }

        private void RefreshPalette()
        {
            if (_paletteTool == null)
                return;

            var mode = _boundContext?.Tool.Mode ?? ConstructionToolMode.None;
            _paletteTool.text = ToolDisplayName(mode);
            _paletteShortcut.text = mode == ConstructionToolMode.None ? string.Empty : $"단축키 {ToolShortcut(mode)}";

            var preview = _boundContext?.Tool.Preview;
            if (preview != null && preview.HasPreview)
                _paletteCost.text = preview.Cost > 0 ? $"비용 {preview.Cost:N0}" : "무료";
            else if (mode == ConstructionToolMode.Road)
                _paletteCost.text = "도로: 셀당 비용";
            else if (mode.IsPaintZoneTool() || mode == ConstructionToolMode.ZoneClear)
                _paletteCost.text = "무료";
            else
                _paletteCost.text = string.Empty;
        }

        private void RefreshInspector(ConstructionPreview preview, ConstructionCommitResult committed)
        {
            if (_inspectorOperation == null)
                return;

            if (committed != null && committed.HadPreview)
            {
                _inspectorOperation.text = $"작업: {OperationName(committed.Operation)}";
                _inspectorCost.text = committed.Committed ? $"비용 {committed.Cost:N0}" : "적용 안 됨";
                _inspectorBalance.text = $"잔액 {committed.FundsAfter:N0}";
                SetStatus(committed.Committed, committed.Committed ? "설치 완료" : committed.Message);
                return;
            }

            if (preview == null || !preview.HasPreview)
            {
                _inspectorOperation.text = "대상을 선택해 미리보기를 확인하세요.";
                _inspectorCost.text = string.Empty;
                _inspectorBalance.text = _boundContext != null ? $"잔액 {_boundContext.Funds:N0}" : string.Empty;
                SetStatus(true, string.Empty);
                return;
            }

            _inspectorOperation.text = $"작업: {OperationName(preview.Operation)} · {preview.ValidCells.Count}셀";
            _inspectorCost.text = preview.Cost > 0 ? $"비용 {preview.Cost:N0}" : "무료";
            _inspectorBalance.text = $"잔액 {preview.FundsAfter:N0}";
            SetStatus(preview.IsValid, preview.IsValid ? "설치 가능" : preview.FailureReason);
        }

        private void SetStatus(bool ok, string message)
        {
            _inspectorStatus.text = message ?? string.Empty;
            _inspectorStatusMarker.text = ok ? string.Empty : "X";
            _inspectorStatus.EnableInClassList(UiFoundationNames.ItemErrorClass, !ok);
            _inspectorStatusMarker.EnableInClassList(UiFoundationNames.ItemErrorClass, !ok);
        }

        private void RefreshLegend(PlacementOverlaySnapshot snapshot)
        {
            var legend = bootstrap.DataOverlayLegend;
            if (legend == null || _legendList == null)
                return;

            if (snapshot == null)
            {
                legend.style.display = DisplayStyle.None;
                _legendList.Clear();
                return;
            }

            legend.style.display = DisplayStyle.Flex;
            _legendTitle.text = snapshot.Title;
            _legendList.Clear();
            foreach (var entry in snapshot.Legend)
                _legendList.Add(MakeLegendRow(entry));
        }

        private VisualElement MakeLegendRow(PlacementOverlayLegendEntry entry)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2f;

            var swatch = new VisualElement();
            swatch.style.width = 12f;
            swatch.style.height = 12f;
            swatch.style.marginRight = 6f;
            swatch.style.backgroundColor = LegendColors.Resolve(entry.ColorRole);
            row.Add(swatch);

            var pattern = new Label($"[{PatternName(entry.Pattern)}]");
            pattern.AddToClassList(UiFoundationNames.PanelCaptionClass);
            pattern.style.marginRight = 6f;
            row.Add(pattern);

            var text = new Label(string.IsNullOrEmpty(entry.Label)
                ? entry.Description
                : $"{entry.Label} · {entry.Description}");
            row.Add(text);
            return row;
        }

        private static VisualElement ReplaceContent(VisualElement shell, string name)
        {
            // Hide the P2-00 placeholder note and the seven debug state fixtures so
            // the real P2 content is not overlapped by scaffolding. The fixture
            // elements stay in the tree (display:none), so the P2-00 shell smoke
            // that looks them up by state still finds all seven.
            var note = shell.Q<Label>(className: UiFoundationNames.EmptyNoteClass);
            if (note != null)
                note.style.display = DisplayStyle.None;

            var fixtures = shell.Q<VisualElement>(className: UiFoundationNames.FixturesClass);
            if (fixtures != null)
                fixtures.style.display = DisplayStyle.None;

            var existing = shell.Q<VisualElement>(name);
            if (existing != null)
                existing.RemoveFromHierarchy();

            var content = new VisualElement { name = name };
            content.style.flexDirection = FlexDirection.Column;
            shell.Insert(1, content);
            return content;
        }

        private static VisualElement MakeItemButton(string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList(UiFoundationNames.ItemClass);
            return button;
        }

        private static Label AddValueLabel(VisualElement parent, string styleClass)
        {
            var label = new Label(string.Empty);
            if (!string.IsNullOrEmpty(styleClass))
                label.AddToClassList(styleClass);
            parent.Add(label);
            return label;
        }

        private static string ToolDisplayName(ConstructionToolMode mode)
        {
            switch (mode)
            {
                case ConstructionToolMode.Road:
                    return "도로";
                case ConstructionToolMode.ZoneResidential:
                    return "주거 구역 (R)";
                case ConstructionToolMode.ZoneCommercial:
                    return "상업 구역 (C)";
                case ConstructionToolMode.ZoneIndustrial:
                    return "공업 구역 (I)";
                case ConstructionToolMode.ZoneClear:
                    return "구역 해제";
                case ConstructionToolMode.Demolish:
                    return "도로 철거";
                default:
                    return "도구를 선택하세요";
            }
        }

        private static string ToolShortcut(ConstructionToolMode mode)
        {
            switch (mode)
            {
                case ConstructionToolMode.Road:
                    return "1";
                case ConstructionToolMode.ZoneResidential:
                    return "2";
                case ConstructionToolMode.ZoneCommercial:
                    return "3";
                case ConstructionToolMode.ZoneIndustrial:
                    return "4";
                case ConstructionToolMode.ZoneClear:
                    return "5";
                case ConstructionToolMode.Demolish:
                    return "6";
                default:
                    return string.Empty;
            }
        }

        private static string OperationName(ConstructionOperation operation)
        {
            switch (operation)
            {
                case ConstructionOperation.PlaceRoad:
                    return "도로 설치";
                case ConstructionOperation.DemolishRoad:
                    return "도로 철거";
                case ConstructionOperation.PaintZone:
                    return "구역 지정";
                case ConstructionOperation.ClearZone:
                    return "구역 해제";
                default:
                    return "없음";
            }
        }

        private static string PatternName(OverlayPattern pattern)
        {
            switch (pattern)
            {
                case OverlayPattern.Hatch:
                    return "빗금";
                case OverlayPattern.CrossHatch:
                    return "교차빗금";
                case OverlayPattern.Dot:
                    return "점";
                case OverlayPattern.Ring:
                    return "고리";
                case OverlayPattern.Outline:
                    return "윤곽";
                default:
                    return "채움";
            }
        }
    }
}
