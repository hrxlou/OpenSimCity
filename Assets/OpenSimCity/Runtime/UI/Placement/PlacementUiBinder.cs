using System.Collections.Generic;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.Overlays;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.UI.Foundation;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Placement
{
    /// <summary>
    /// Binds placement and road-upgrade state to the runtime UI shells. UI renders
    /// Application previews and read models only; it does not inspect or mutate
    /// Simulation collections.
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
        private readonly Dictionary<StableId, VisualElement> _upgradeTierButtons =
            new Dictionary<StableId, VisualElement>();
        private readonly Dictionary<RoadDirectionMode, VisualElement> _upgradeDirectionButtons =
            new Dictionary<RoadDirectionMode, VisualElement>();

        private Label _paletteTool;
        private Label _paletteCost;
        private Label _paletteShortcut;
        private Label _inspectorOperation;
        private Label _inspectorCost;
        private Label _inspectorBalance;
        private Label _inspectorStatusMarker;
        private Label _inspectorStatus;
        private VisualElement _roadUpgradePanel;
        private Label _upgradeSelection;
        private Label _upgradeEconomy;
        private Label _upgradePerformance;
        private Label _upgradeTraffic;
        private Label _upgradeReachability;
        private Label _upgradeRisk;
        private VisualElement _legendList;
        private Label _legendTitle;
        private VisualElement _overlayTray;
        private VisualElement _overlayToggleButton;

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
            _upgradeTierButtons.Clear();
            _upgradeDirectionButtons.Clear();
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
            content.style.flexDirection = FlexDirection.Row;
            content.style.alignItems = Align.Center;

            var toolGrid = new VisualElement { name = "osc-tool-grid" };
            toolGrid.AddToClassList("osc-tool-grid");
            content.Add(toolGrid);
            AddToolButton(toolGrid, ConstructionToolMode.Road, "road", "도로", "1");
            AddToolButton(toolGrid, ConstructionToolMode.RoadUpgrade, "road-upgrade", "도로 업그레이드", "9");
            AddToolButton(toolGrid, ConstructionToolMode.ZoneResidential, "residential", "주거 구역", "2");
            AddToolButton(toolGrid, ConstructionToolMode.ZoneCommercial, "commercial", "상업 구역", "3");
            AddToolButton(toolGrid, ConstructionToolMode.ZoneIndustrial, "industrial", "공업 구역", "4");
            AddToolButton(toolGrid, ConstructionToolMode.ZoneClear, "zone-clear", "구역 해제", "5");
            AddToolButton(toolGrid, ConstructionToolMode.Demolish, "demolish", "철거", "6");
            AddToolButton(toolGrid, ConstructionToolMode.PowerPlant, "power", "발전소", "7");
            AddToolButton(toolGrid, ConstructionToolMode.RemovePowerPlant, "power-remove", "발전소 철거", "8");

            _overlayTray = new VisualElement { name = "osc-data-overlay-tray" };
            _overlayTray.AddToClassList("osc-data-overlay-tray");
            _overlayTray.style.display = DisplayStyle.None;
            var overlayHeader = new Label("도시 데이터");
            overlayHeader.AddToClassList(UiFoundationNames.PanelCaptionClass);
            _overlayTray.Add(overlayHeader);

            var overlayGrid = new VisualElement { name = "osc-placement-overlay-grid" };
            overlayGrid.AddToClassList("osc-overlay-grid");
            _overlayTray.Add(overlayGrid);
            AddOverlayButton(overlayGrid, PlacementOverlayKind.RoadConnectivity, "연결");
            AddOverlayButton(overlayGrid, PlacementOverlayKind.RoadHierarchy, "도로 등급");
            AddOverlayButton(overlayGrid, PlacementOverlayKind.Accessibility, "접근성");
            AddOverlayButton(overlayGrid, PlacementOverlayKind.Zone, "구역");
            AddOverlayButton(overlayGrid, PlacementOverlayKind.Parcel, "필지");
            AddOverlayButton(overlayGrid, PlacementOverlayKind.Power, "전력");
            content.Add(_overlayTray);

            _overlayToggleButton = MakeItemButton("데이터", ToggleOverlayTray);
            _overlayToggleButton.name = "osc-data-overlay-toggle";
            _overlayToggleButton.tooltip = "연결·도로 등급·접근성·구역·필지·전력 데이터";
            _overlayToggleButton.AddToClassList("osc-data-overlay-toggle");
            content.Add(_overlayToggleButton);

            var manageButton = MakeItemButton(
                string.Empty,
                () => UiSystemPanelDock.Toggle(bootstrap.SystemPanelDock));
            manageButton.name = "osc-city-management-toggle";
            manageButton.tooltip = "대중교통·공공서비스·도시 데이터 관리";
            manageButton.AddToClassList("osc-city-management-button");
            var manageIcon = new VisualElement { name = "osc-city-management-icon" };
            manageIcon.AddToClassList("osc-tool-button__icon");
            manageIcon.AddToClassList("osc-icon--management");
            var manageLabel = new Label("도시 관리");
            manageLabel.AddToClassList("osc-tool-button__label");
            manageButton.Add(manageIcon);
            manageButton.Add(manageLabel);
            content.Add(manageButton);
        }

        private void ToggleOverlayTray()
        {
            if (_overlayTray == null)
                return;

            var open = _overlayTray.resolvedStyle.display == DisplayStyle.None;
            _overlayTray.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _overlayToggleButton?.EnableInClassList(UiFoundationNames.ItemSelectedClass, open);
        }

        private void AddToolButton(
            VisualElement parent,
            ConstructionToolMode mode,
            string iconName,
            string label,
            string shortcut)
        {
            var button = MakeItemButton(string.Empty, () => CityRuntimeContext.Current?.SelectTool(mode));
            button.tooltip = $"{label} ({shortcut})";
            button.AddToClassList("osc-tool-button");

            var iconElement = new VisualElement { name = $"osc-tool-{iconName}-icon" };
            iconElement.AddToClassList("osc-tool-button__icon");
            iconElement.AddToClassList($"osc-icon--{iconName}");
            var nameLabel = new Label(label.Replace(" 구역", string.Empty)
                .Replace("도로 업그레이드", "도로 확장").Replace("발전소 철거", "전력 철거"));
            nameLabel.AddToClassList("osc-tool-button__label");
            var shortcutLabel = new Label(shortcut);
            shortcutLabel.AddToClassList("osc-tool-button__shortcut");
            button.Add(iconElement);
            button.Add(nameLabel);
            button.Add(shortcutLabel);
            parent.Add(button);
            _toolButtons[mode] = button;
        }

        private void AddOverlayButton(VisualElement parent, PlacementOverlayKind kind, string label)
        {
            var button = MakeItemButton(label, () => CityRuntimeContext.Current?.ToggleOverlay(kind));
            button.AddToClassList("osc-overlay-button");
            parent.Add(button);
            _overlayButtons[kind] = button;
        }

        private void BuildPalette()
        {
            var palette = bootstrap.BuildPalette;
            if (palette == null)
                return;

            var content = ReplaceContent(palette, "osc-placement-palette");
            palette.AddToClassList("osc-placement-palette--compact");
            content.AddToClassList("osc-placement-palette__content");
            content.style.flexDirection = FlexDirection.Row;
            content.style.alignItems = Align.Center;
            var title = palette.Q<Label>(className: UiFoundationNames.PanelTitleClass);
            title?.AddToClassList("osc-placement-palette__heading");
            _paletteTool = AddValueLabel(content, UiFoundationNames.PanelTitleClass);
            _paletteTool.AddToClassList("osc-placement-palette__tool");
            _paletteShortcut = AddValueLabel(content, UiFoundationNames.PanelCaptionClass);
            _paletteShortcut.AddToClassList("osc-placement-palette__shortcut");
            _paletteCost = AddValueLabel(content, UiFoundationNames.PanelCaptionClass);
            _paletteCost.AddToClassList("osc-placement-palette__cost");
        }

        private void BuildInspector()
        {
            var inspector = bootstrap.ContextInspector;
            if (inspector == null)
                return;

            var content = ReplaceContent(inspector, "osc-placement-inspector");
            _inspectorOperation = AddValueLabel(content, UiFoundationNames.PanelCaptionClass);
            _inspectorOperation.AddToClassList("osc-inspector-primary");
            _inspectorCost = AddValueLabel(content, UiFoundationNames.PanelCaptionClass);
            _inspectorCost.AddToClassList("osc-inspector-secondary");
            _inspectorBalance = AddValueLabel(content, UiFoundationNames.PanelCaptionClass);
            _inspectorBalance.AddToClassList("osc-inspector-secondary");

            var statusRow = new VisualElement { name = "osc-placement-status" };
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.AddToClassList("osc-inspector-status");
            _inspectorStatusMarker = new Label(string.Empty);
            _inspectorStatusMarker.AddToClassList(UiFoundationNames.ItemMarkerClass);
            _inspectorStatusMarker.style.display = DisplayStyle.Flex;
            _inspectorStatus = new Label(string.Empty);
            statusRow.Add(_inspectorStatusMarker);
            statusRow.Add(_inspectorStatus);
            content.Add(statusRow);

            _roadUpgradePanel = new VisualElement { name = "osc-road-upgrade-panel" };
            _roadUpgradePanel.style.flexDirection = FlexDirection.Column;
            _roadUpgradePanel.style.marginTop = 8f;
            _roadUpgradePanel.style.display = DisplayStyle.None;
            var heading = new Label("도로 업그레이드 비교");
            heading.AddToClassList(UiFoundationNames.PanelCaptionClass);
            _roadUpgradePanel.Add(heading);

            var tierRow = new VisualElement { name = "osc-road-upgrade-tier-row" };
            tierRow.style.flexDirection = FlexDirection.Row;
            AddUpgradeTierButton(tierRow, RoadUpgradeDefinitionIds.Small, "소형");
            AddUpgradeTierButton(tierRow, RoadUpgradeDefinitionIds.Street, "일반");
            AddUpgradeTierButton(tierRow, RoadUpgradeDefinitionIds.Avenue, "4차선");
            AddUpgradeTierButton(tierRow, RoadUpgradeDefinitionIds.Boulevard, "6차선");
            _roadUpgradePanel.Add(tierRow);

            var directionRow = new VisualElement { name = "osc-road-upgrade-direction-row" };
            directionRow.style.flexDirection = FlexDirection.Row;
            AddUpgradeDirectionButton(directionRow, RoadDirectionMode.Bidirectional, "↔ 양방향");
            AddUpgradeDirectionButton(directionRow, RoadDirectionMode.Forward, "→ 정방향");
            AddUpgradeDirectionButton(directionRow, RoadDirectionMode.Reverse, "← 역방향");
            _roadUpgradePanel.Add(directionRow);

            _upgradeSelection = AddValueLabel(_roadUpgradePanel, UiFoundationNames.PanelCaptionClass);
            _upgradeEconomy = AddValueLabel(_roadUpgradePanel, UiFoundationNames.PanelCaptionClass);
            _upgradePerformance = AddValueLabel(_roadUpgradePanel, UiFoundationNames.PanelCaptionClass);
            _upgradeTraffic = AddValueLabel(_roadUpgradePanel, UiFoundationNames.PanelCaptionClass);
            _upgradeReachability = AddValueLabel(_roadUpgradePanel, UiFoundationNames.PanelCaptionClass);
            _upgradeRisk = AddValueLabel(_roadUpgradePanel, UiFoundationNames.PanelCaptionClass);
            content.Add(_roadUpgradePanel);
        }

        private void AddUpgradeTierButton(VisualElement parent, StableId definitionId, string label)
        {
            var button = MakeItemButton(label, () => ConfigureRoadUpgrade(definitionId, null));
            parent.Add(button);
            _upgradeTierButtons[definitionId] = button;
        }

        private void AddUpgradeDirectionButton(
            VisualElement parent,
            RoadDirectionMode directionMode,
            string label)
        {
            var button = MakeItemButton(label, () => ConfigureRoadUpgrade(null, directionMode));
            parent.Add(button);
            _upgradeDirectionButtons[directionMode] = button;
        }

        private void ConfigureRoadUpgrade(StableId? targetDefinitionId, RoadDirectionMode? directionMode)
        {
            var tool = _boundContext?.Tool;
            if (tool == null)
                return;

            tool.ConfigureRoadUpgrade(
                targetDefinitionId ?? tool.RoadUpgradeTargetDefinitionId,
                directionMode ?? tool.RoadUpgradeDirectionMode);
            RefreshPalette();
            RefreshRoadUpgradePanel();
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
            RefreshRoadUpgradePanel();
            if (mode == ConstructionToolMode.None)
                RefreshInspector(preview: null, committed: null);
        }

        private void HandlePreviewChanged(ConstructionPreview preview)
        {
            RefreshPalette();
            RefreshInspector(preview, committed: null);
            RefreshRoadUpgradePanel();
        }

        private void HandleCommitted(ConstructionCommitResult result)
        {
            RefreshInspector(_boundContext?.Tool.Preview, result);
            RefreshRoadUpgradePanel();
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
            RefreshRoadUpgradePanel();
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
            if (bootstrap?.BuildPalette != null)
                bootstrap.BuildPalette.style.display = mode == ConstructionToolMode.None
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            _paletteTool.text = ToolDisplayName(mode);
            _paletteShortcut.text = mode == ConstructionToolMode.None ? string.Empty : $"단축키 {ToolShortcut(mode)}";

            var preview = _boundContext?.Tool.Preview;
            if (preview != null && preview.HasPreview)
                _paletteCost.text = preview.Cost > 0 ? $"비용 {preview.Cost:N0}" : "무료";
            else if (mode == ConstructionToolMode.Road)
                _paletteCost.text = "도로: 셀당 비용";
            else if (mode == ConstructionToolMode.RoadUpgrade)
                _paletteCost.text = "차액·혼잡 미리보기";
            else if (mode.IsPaintZoneTool() || mode == ConstructionToolMode.ZoneClear)
                _paletteCost.text = "무료";
            else
                _paletteCost.text = string.Empty;
        }

        private void RefreshInspector(ConstructionPreview preview, ConstructionCommitResult committed)
        {
            if (_inspectorOperation == null)
                return;

            var visible = committed != null && committed.HadPreview || preview != null && preview.HasPreview;
            if (bootstrap?.ContextInspector != null)
                bootstrap.ContextInspector.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (committed != null && committed.HadPreview)
            {
                _inspectorOperation.text = $"작업: {OperationName(committed.Operation)}";
                _inspectorCost.text = committed.Committed ? $"비용 {committed.Cost:N0}" : "적용 안 됨";
                _inspectorBalance.text = $"잔액 {committed.FundsAfter:N0}";
                var done = committed.Operation == ConstructionOperation.UpgradeRoad
                    ? "업그레이드 완료"
                    : "설치 완료";
                SetStatus(committed.Committed, committed.Committed ? done : committed.Message);
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
            var validMessage = preview.Operation == ConstructionOperation.UpgradeRoad
                ? "업그레이드 가능"
                : "설치 가능";
            SetStatus(preview.IsValid, preview.IsValid ? validMessage : preview.FailureReason);
        }

        private void RefreshRoadUpgradePanel()
        {
            if (_roadUpgradePanel == null)
                return;

            var tool = _boundContext?.Tool;
            var active = tool != null && tool.Mode == ConstructionToolMode.RoadUpgrade;
            _roadUpgradePanel.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            if (!active)
                return;

            foreach (var pair in _upgradeTierButtons)
            {
                pair.Value.EnableInClassList(
                    UiFoundationNames.ItemSelectedClass,
                    pair.Key == tool.RoadUpgradeTargetDefinitionId);
            }
            foreach (var pair in _upgradeDirectionButtons)
            {
                pair.Value.EnableInClassList(
                    UiFoundationNames.ItemSelectedClass,
                    pair.Key == tool.RoadUpgradeDirectionMode);
            }

            var comparison = tool.RoadUpgradePreview ?? tool.LastCommittedRoadUpgradePreview;
            _upgradeSelection.text = $"대상 {TierName(tool.RoadUpgradeTargetDefinitionId)} · " +
                                     RoadUpgradePreviewFormatting.DirectionModeLabel(tool.RoadUpgradeDirectionMode);
            if (comparison == null)
            {
                _upgradeEconomy.text = "기존 도로를 드래그하면 전후 수치를 비교합니다.";
                _upgradePerformance.text = string.Empty;
                _upgradeTraffic.text = string.Empty;
                _upgradeReachability.text = string.Empty;
                _upgradeRisk.text = string.Empty;
                return;
            }

            _upgradeSelection.text = $"대상 {TierName(comparison.TargetDefinitionId)} · " +
                                     RoadUpgradePreviewFormatting.DirectionLabel(comparison);
            _upgradeEconomy.text =
                $"건설값 {comparison.CurrentBuildCost:N0} → {comparison.TargetBuildCost:N0} · " +
                $"차액 {comparison.UpgradeCost:N0} · 유지비 " +
                $"{comparison.CurrentMonthlyMaintenance:N0} → {comparison.TargetMonthlyMaintenance:N0}";
            _upgradePerformance.text =
                $"용량 {comparison.CurrentCapacity:N0} → {comparison.TargetCapacity:N0} · " +
                $"속도 {comparison.CurrentMinimumSpeed:N0} → {comparison.TargetMinimumSpeed:N0} · " +
                $"소음 {comparison.CurrentNoise:N0} → {comparison.TargetNoise:N0}";
            _upgradeTraffic.text =
                $"최대 V/C {FormatPermille(comparison.CurrentMaximumVolumeCapacityPermille)} → " +
                $"{FormatPermille(comparison.PredictedMaximumVolumeCapacityPermille)} · 혼잡 edge " +
                $"{comparison.CurrentCongestedEdgeCount:N0} → {comparison.PredictedCongestedEdgeCount:N0}";
            _upgradeReachability.text =
                $"도달 trip {comparison.ReachableTripAmountBefore:N0} → {comparison.ReachableTripAmountAfter:N0} · " +
                $"미배정 {comparison.UnroutedTripAmountBefore:N0} → {comparison.UnroutedTripAmountAfter:N0} · " +
                $"평균 자유통행 {comparison.AverageFreeFlowSecondsBefore:N0}s → " +
                $"{comparison.AverageFreeFlowSecondsAfter:N0}s";

            var failure = RoadUpgradePreviewFormatting.PrimaryFailureLabel(comparison);
            if (comparison.UnroutedTripAmountDelta > 0)
            {
                _upgradeRisk.text = $"경고: +{comparison.UnroutedTripAmountDelta:N0} trip · {failure}";
                _upgradeRisk.EnableInClassList(UiFoundationNames.ItemErrorClass, true);
            }
            else if (comparison.HasPredictedBottleneck)
            {
                _upgradeRisk.text =
                    $"예상 병목 {comparison.PredictedBottleneckA} ↔ {comparison.PredictedBottleneckB}";
                _upgradeRisk.EnableInClassList(UiFoundationNames.ItemErrorClass, false);
            }
            else
            {
                _upgradeRisk.text = "새 도달 불가 없음";
                _upgradeRisk.EnableInClassList(UiFoundationNames.ItemErrorClass, false);
            }
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

        private static string TierName(StableId definitionId)
        {
            if (definitionId == RoadUpgradeDefinitionIds.Small)
                return "소형";
            if (definitionId == RoadUpgradeDefinitionIds.Avenue)
                return "왕복 4차선";
            if (definitionId == RoadUpgradeDefinitionIds.Boulevard)
                return "왕복 6차선";
            return "일반";
        }

        private static string FormatPermille(int permille)
        {
            return $"{permille / 1000}.{(permille % 1000) / 100}x";
        }

        private static string ToolDisplayName(ConstructionToolMode mode)
        {
            switch (mode)
            {
                case ConstructionToolMode.Road:
                    return "도로";
                case ConstructionToolMode.RoadUpgrade:
                    return "도로 업그레이드";
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
                case ConstructionToolMode.PowerPlant:
                    return "발전소";
                case ConstructionToolMode.RemovePowerPlant:
                    return "발전소 철거";
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
                case ConstructionToolMode.RoadUpgrade:
                    return "9";
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
                case ConstructionToolMode.PowerPlant:
                    return "7";
                case ConstructionToolMode.RemovePowerPlant:
                    return "8";
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
                case ConstructionOperation.UpgradeRoad:
                    return "도로 업그레이드";
                case ConstructionOperation.DemolishRoad:
                    return "도로 철거";
                case ConstructionOperation.PaintZone:
                    return "구역 지정";
                case ConstructionOperation.ClearZone:
                    return "구역 해제";
                case ConstructionOperation.PlacePowerProducer:
                    return "발전소 설치";
                case ConstructionOperation.RemovePowerProducer:
                    return "발전소 철거";
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
