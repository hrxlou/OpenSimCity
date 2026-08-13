using System;
using System.Text;
using OpenSimCity.Application.Input;
using OpenSimCity.Application.WaterDensity;
using OpenSimCity.Gameplay.WaterDensity;
using OpenSimCity.Simulation.Development;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.WaterDensity
{
    [DisallowMultipleComponent]
    public sealed class WaterDensityUiBinder : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        private WaterDensityRuntimeHost _host;
        private VisualElement _panel;
        private Label _summary;
        private Label _selection;
        private Label _details;
        private Label _factors;
        private Label _feedback;
        private Button _waterOverlay;
        private Button _densityOverlay;
        private bool _pointerCaptured;
        private bool _keyboardCaptured;
        private int _readVersion = -1;
        private int _selectionVersion = -1;

        public VisualElement Panel => _panel;

        public void Configure(UIDocument target)
        {
            document = target;
            Rebuild();
        }

        private void OnEnable()
        {
            if (document == null) document = GetComponent<UIDocument>();
            Rebuild();
        }

        private void OnDisable()
        {
            ReleasePointer();
            ReleaseKeyboard();
            _panel?.RemoveFromHierarchy();
            _panel = null;
        }

        private void Update()
        {
            if (_host == null) _host = UnityEngine.Object.FindFirstObjectByType<WaterDensityRuntimeHost>();
            var module = _host?.Module;
            if (module == null) return;
            module.ReadModel.RefreshIfNeeded();
            var readVersion = module.ReadModel.Snapshot.Version;
            var selectionVersion = WaterDensitySelection.Current.Version;
            if (_readVersion == readVersion && _selectionVersion == selectionVersion) return;
            _readVersion = readVersion;
            _selectionVersion = selectionVersion;
            RefreshText();
        }

        private void Rebuild()
        {
            if (!isActiveAndEnabled || document == null || document.rootVisualElement == null) return;
            _panel?.RemoveFromHierarchy();
            _panel = new VisualElement { name = "osc-water-density-panel" };
            _panel.style.position = Position.Absolute;
            _panel.style.left = 384;
            _panel.style.bottom = 12;
            _panel.style.width = 370;
            _panel.style.paddingLeft = 10;
            _panel.style.paddingRight = 10;
            _panel.style.paddingTop = 8;
            _panel.style.paddingBottom = 8;
            _panel.style.backgroundColor = new Color(0.055f, 0.075f, 0.10f, 0.94f);
            _panel.style.borderTopLeftRadius = 6;
            _panel.style.borderTopRightRadius = 6;
            _panel.style.borderBottomLeftRadius = 6;
            _panel.style.borderBottomRightRadius = 6;

            var heading = new Label("수도·밀도") { name = "osc-water-density-heading" };
            heading.style.fontSize = 15;
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            _panel.Add(heading);
            _summary = AddLabel("osc-water-density-summary");
            _selection = AddLabel("osc-water-density-selection");

            var actionRow = new VisualElement { name = "osc-water-density-actions" };
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.Add(new Button(PlacePump) { name = "osc-place-water-pump", text = "선택 셀에 펌프" });
            _waterOverlay = new Button(() => ToggleOverlay(WaterDensityOverlayKind.Water))
            {
                name = "osc-water-overlay"
            };
            _densityOverlay = new Button(() => ToggleOverlay(WaterDensityOverlayKind.Density))
            {
                name = "osc-density-overlay"
            };
            actionRow.Add(_waterOverlay);
            actionRow.Add(_densityOverlay);
            _panel.Add(actionRow);

            _details = AddLabel("osc-water-density-details");
            _factors = AddLabel("osc-density-factors");
            _feedback = AddLabel("osc-water-density-feedback");
            _panel.RegisterCallback<PointerEnterEvent>(_ => CapturePointer());
            _panel.RegisterCallback<PointerLeaveEvent>(_ => ReleasePointer());
            _panel.RegisterCallback<FocusInEvent>(_ => CaptureKeyboard());
            _panel.RegisterCallback<FocusOutEvent>(_ => ReleaseKeyboard());
            document.rootVisualElement.Add(_panel);
            RefreshText();
        }

        private Label AddLabel(string name)
        {
            var label = new Label { name = name };
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 4;
            _panel.Add(label);
            return label;
        }

        private void PlacePump()
        {
            if (_host == null || !WaterDensitySelection.Current.SelectedCell.HasValue)
            {
                _feedback.text = "빈 도로 인접 셀을 먼저 선택하세요.";
                return;
            }
            var result = _host.PlacePump(WaterDensitySelection.Current.SelectedCell.Value);
            _feedback.text = result.IsApplied ? "수도 펌프를 배치했습니다." : result.Message;
            RefreshText();
        }

        private void ToggleOverlay(WaterDensityOverlayKind kind)
        {
            var selection = WaterDensitySelection.Current;
            var visible = !(selection.IsOverlayVisible && selection.OverlayKind == kind);
            selection.SetOverlay(kind, visible);
            RefreshText();
        }

        private void RefreshText()
        {
            if (_summary == null || _selection == null || _details == null ||
                _factors == null || _feedback == null || _waterOverlay == null ||
                _densityOverlay == null) return;
            var module = _host?.Module;
            if (module == null)
            {
                _summary.text = "수도·밀도 runtime을 기다리는 중";
                return;
            }
            module.ReadModel.RefreshIfNeeded();
            var snapshot = module.ReadModel.Snapshot;
            _summary.text = snapshot.HasProcessedMonth
                ? $"적용 기간 {snapshot.Year}년 {snapshot.Month}월 · 수도 {snapshot.WaterSupplied}/{snapshot.WaterRequested} · 펌프 {snapshot.Producers.Count}"
                : $"아직 처리된 월 없음 · 펌프 {snapshot.Producers.Count}";
            var selectedCell = WaterDensitySelection.Current.SelectedCell;
            _selection.text = selectedCell.HasValue
                ? $"선택 셀 {selectedCell.Value}"
                : "월드에서 건물 또는 빈 도로 인접 셀을 선택하세요.";
            _waterOverlay.text = WaterDensitySelection.Current.IsOverlayVisible &&
                                 WaterDensitySelection.Current.OverlayKind == WaterDensityOverlayKind.Water
                ? "수도맵 끄기"
                : "수도맵";
            _densityOverlay.text = WaterDensitySelection.Current.IsOverlayVisible &&
                                   WaterDensitySelection.Current.OverlayKind == WaterDensityOverlayKind.Density
                ? "밀도맵 끄기"
                : "밀도맵";

            DensityBuildingReadModel selectedBuilding = null;
            if (selectedCell.HasValue)
            {
                for (var index = 0; index < snapshot.Buildings.Count; index++)
                {
                    if (snapshot.Buildings[index].Cell == selectedCell.Value)
                    {
                        selectedBuilding = snapshot.Buildings[index];
                        break;
                    }
                }
            }
            if (selectedBuilding == null)
            {
                _details.text = "선택된 density building 없음";
                _factors.text = string.Empty;
                return;
            }

            _details.text =
                $"단계 {StageText(selectedBuilding.Stage)}" +
                (selectedBuilding.UpgradeMonthsRemaining > 0
                    ? $" → {StageText(selectedBuilding.TargetStage)} · 남은 {selectedBuilding.UpgradeMonthsRemaining}개월"
                    : string.Empty) +
                $"\n수도 {selectedBuilding.WaterSupplyPermille / 10}% · 주거 capacity {selectedBuilding.EffectiveHousingCapacity} · 일자리 capacity {selectedBuilding.EffectiveJobCapacity}" +
                $"\n예상 추가 세수 {selectedBuilding.ProjectedSupplementalTax} · 상위 차단 {ReasonText(selectedBuilding.TopReason)}";
            var factors = new StringBuilder();
            factors.Append("다음 단계 조건");
            if (selectedBuilding.Factors.Count == 0)
            {
                factors.Append(": 없음");
            }
            else
            {
                for (var index = 0; index < selectedBuilding.Factors.Count; index++)
                {
                    var factor = selectedBuilding.Factors[index];
                    factors.Append('\n');
                    factors.Append(factor.IsSatisfied ? "✓ " : "- ");
                    factors.Append(FactorText(factor.Kind));
                    factors.Append(' ');
                    factors.Append(factor.CurrentValue);
                    factors.Append('/');
                    factors.Append(factor.RequiredValue);
                }
            }
            _factors.text = factors.ToString();
        }

        private static string StageText(DensityStageView stage)
        {
            switch (stage)
            {
                case DensityStageView.Low: return "저밀도";
                case DensityStageView.Medium: return "중밀도";
                default: return "고밀도";
            }
        }

        private static string ReasonText(DensityBlockReasonView reason)
        {
            switch (reason)
            {
                case DensityBlockReasonView.CityPopulationTooLow: return "도시 인구 부족";
                case DensityBlockReasonView.NoWater: return "수도 부족";
                case DensityBlockReasonView.LandValueTooLow: return "토지가치 부족";
                case DensityBlockReasonView.RoadTierTooLow: return "도로 등급 부족";
                case DensityBlockReasonView.ServicesTooLow: return "서비스 coverage 부족";
                case DensityBlockReasonView.AlreadyMaximum: return "최고 단계";
                case DensityBlockReasonView.UpgradeInProgress: return "업그레이드 진행 중";
                default: return "충족";
            }
        }

        private static string FactorText(DensityUpgradeFactorKind kind)
        {
            switch (kind)
            {
                case DensityUpgradeFactorKind.CityPopulation: return "도시 인구";
                case DensityUpgradeFactorKind.WaterSupply: return "수도 공급";
                case DensityUpgradeFactorKind.LandValue: return "토지가치";
                case DensityUpgradeFactorKind.RoadTier: return "도로 등급";
                default: return "서비스";
            }
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
    }
}
