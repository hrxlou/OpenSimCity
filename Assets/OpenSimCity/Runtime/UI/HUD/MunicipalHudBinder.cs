using System;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.Overlays;
using OpenSimCity.Application.Persistence;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.UI.Foundation;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.HUD
{
    /// <summary>
    /// Displays the P3 municipal and growth ViewModels in the shared P2 UI
    /// foundation. UI actions submit application commands or application controls;
    /// this component never mutates city simulation state or query DTOs directly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MunicipalHudBinder : MonoBehaviour
    {
        private const int TaxPermilleMaximum = 1000;

        private enum TaxSector
        {
            Residential,
            Commercial,
            Industrial
        }

        [SerializeField] private UiFoundationBootstrap bootstrap;

        private VisualElement _boundRoot;
        private CityRuntimeContext _boundContext;
        private Label _date;
        private Label _period;
        private Label _balance;
        private Label _income;
        private Label _expense;
        private Label _population;
        private Label _rci;
        private Label _development;
        private Label _reasons;
        private Label _taxRates;
        private Label _feedback;
        private Button _pause;
        private Button _oneX;
        private Button _twoX;
        private Button _fourX;
        private string _lastRefreshKey;

        public bool IsBound => _boundContext != null && _boundRoot != null;

        public void ConfigureReferences(UiFoundationBootstrap uiBootstrap)
        {
            bootstrap = uiBootstrap;
        }

        private void OnDisable()
        {
            UnbindContext();
            _boundRoot = null;
            _lastRefreshKey = null;
        }

        private void Update()
        {
            if (bootstrap == null)
                return;

            if (!ReferenceEquals(bootstrap.Root, _boundRoot))
            {
                _boundRoot = bootstrap.Root;
                if (_boundRoot != null)
                    BuildContent();
            }

            var context = CityRuntimeContext.Current;
            if (!ReferenceEquals(context, _boundContext))
                BindContext(context);

            RefreshIfChanged();
        }

        private void BuildContent()
        {
            BuildGlobalHud();
            BuildSimulationControls();
            _lastRefreshKey = null;
        }

        private void BuildGlobalHud()
        {
            var hud = bootstrap.GlobalHud;
            if (hud == null)
                return;

            hud.Clear();

            // Keep all P3 readouts in a single status row. On the 1280px minimum
            // desktop target the left economy group and the right development /
            // tax group fit together, so a separate diagnostics row would only
            // consume World View space without improving scanability.
            var summary = new VisualElement { name = "osc-hud-summary" };
            summary.AddToClassList("osc-hud-summary-row");
            var summaryLeft = CreateSection("osc-hud-summary-left", "osc-hud-left");
            var summaryRight = CreateSection("osc-hud-summary-right", "osc-hud-right");
            _date = AddMetric(summaryLeft, "날짜", "osc-hud-date");
            _balance = AddMetric(summaryLeft, "잔액", "osc-hud-balance");
            _income = AddMetric(summaryLeft, "수입", "osc-hud-income");
            _expense = AddMetric(summaryLeft, "지출", "osc-hud-expense");
            _population = AddMetric(summaryLeft, "인구", "osc-hud-population");
            _rci = AddMetric(summaryLeft, "RCI", "osc-hud-rci");
            _development = AddMetric(summaryRight, "개발", "osc-hud-development-status");
            _period = AddMetric(summaryRight, "적용", "osc-hud-period");
            _reasons = AddMetric(summaryRight, "원인", "osc-hud-development-reasons");
            _taxRates = AddMetric(summaryRight, "세율", "osc-hud-tax-rates");

            var taxActions = new VisualElement { name = "osc-hud-tax-actions" };
            taxActions.AddToClassList("osc-hud-tax-actions");
            AddTaxButton(taxActions, "osc-tax-residential-increase", "주거+1", TaxSector.Residential);
            AddTaxButton(taxActions, "osc-tax-commercial-increase", "상업+1", TaxSector.Commercial);
            AddTaxButton(taxActions, "osc-tax-industrial-increase", "공업+1", TaxSector.Industrial);
            var parcelDetails = new Button(() => CityRuntimeContext.Current?.SetOverlay(PlacementOverlayKind.Parcel))
            {
                name = "osc-hud-parcel-overlay",
                text = "원인 보기",
                tooltip = "필지/원인 보기"
            };
            parcelDetails.AddToClassList(UiFoundationNames.ItemClass);
            parcelDetails.AddToClassList("osc-hud-action");
            taxActions.Add(parcelDetails);
            summaryRight.Add(taxActions);

            _feedback = new Label { name = "osc-hud-command-feedback" };
            _feedback.AddToClassList("osc-hud-feedback");
            summaryRight.Add(_feedback);
            summary.Add(summaryLeft);
            summary.Add(summaryRight);
            hud.Add(summary);
        }

        private void BuildSimulationControls()
        {
            var controls = bootstrap.SimulationControls;
            if (controls == null)
                return;

            controls.Clear();
            _pause = AddSpeedButton(controls, "osc-simulation-pause", "일시정지", SimulationSpeed.Paused);
            _oneX = AddSpeedButton(controls, "osc-simulation-1x", "1x", SimulationSpeed.One);
            _twoX = AddSpeedButton(controls, "osc-simulation-2x", "2x", SimulationSpeed.Two);
            _fourX = AddSpeedButton(controls, "osc-simulation-4x", "4x", SimulationSpeed.Four);
        }

        private void BindContext(CityRuntimeContext context)
        {
            UnbindContext();
            _boundContext = context;
            if (_boundContext != null)
                _boundContext.Committed += HandleConstructionCommitted;
            _lastRefreshKey = null;
        }

        private void UnbindContext()
        {
            if (_boundContext != null)
                _boundContext.Committed -= HandleConstructionCommitted;
            _boundContext = null;
        }

        private void HandleConstructionCommitted(ConstructionCommitResult result)
        {
            if (_feedback == null || !result.HadPreview)
                return;

            _feedback.text = result.Committed
                ? $"✓ {result.Message} · 비용 {result.Cost:N0}"
                : $"! {result.Message}";
            _feedback.EnableInClassList(UiFoundationNames.ItemErrorClass, !result.Committed);
            _lastRefreshKey = null;
        }

        private void RefreshIfChanged()
        {
            if (_date == null)
                return;

            if (_boundContext == null)
            {
                SetUnavailable();
                return;
            }

            var runtime = _boundContext.Runtime;
            var key = string.Concat(
                runtime.HudModel.Version, ":",
                (int)runtime.Clock.Speed);
            if (string.Equals(key, _lastRefreshKey, StringComparison.Ordinal))
                return;

            _lastRefreshKey = key;
            var hud = runtime.HudModel.Snapshot;
            _date.text = FormatDate(hud.CurrentYear, hud.CurrentMonth, hud.CurrentDay);
            _period.text = hud.HasSettledLedger ? FormatMonth(hud.LedgerYear, hud.LedgerMonth) : "월간 정산 전";
            _balance.text = $"{hud.Balance:N0}";
            _income.text = $"{hud.MonthlyIncome:N0}";
            _expense.text = $"{hud.MonthlyExpense:N0}";
            _taxRates.text = TaxText(
                hud.ActiveResidentialTaxPermille,
                hud.ActiveCommercialTaxPermille,
                hud.ActiveIndustrialTaxPermille,
                hud.HasPendingTaxRateChange,
                hud.PendingEffectiveYear,
                hud.PendingEffectiveMonth);
            _population.text = $"{hud.Population:N0}";
            _rci.text = $"R {hud.ResidentialRci:+#;-#;0} / C {hud.CommercialRci:+#;-#;0} / I {hud.IndustrialRci:+#;-#;0}";

            if (hud.HasDevelopmentStatus)
                SetDevelopmentStatus(hud);
            else
                SetDevelopmentUnavailable();

            SetSpeedSelection(runtime.Clock.Speed);
        }

        private void SetUnavailable()
        {
            _date.text = string.Empty;
            _period.text = string.Empty;
            _balance.text = string.Empty;
            _income.text = string.Empty;
            _expense.text = string.Empty;
            _population.text = string.Empty;
            _rci.text = string.Empty;
            _development.text = "도시를 불러오는 중";
            _reasons.text = string.Empty;
            _taxRates.text = string.Empty;
            SetSpeedSelection(SimulationSpeed.Paused);
        }

        private void SetDevelopmentUnavailable()
        {
            _development.text = "다음 월 정산 후 개발 상태 표시";
            _development.RemoveFromClassList("osc-hud-status");
            _development.RemoveFromClassList("osc-hud-status--warning");
            _reasons.text = string.Empty;
        }

        private void SetDevelopmentStatus(MunicipalHudSnapshotDto snapshot)
        {
            switch (snapshot.DevelopmentStatus)
            {
                case DevelopmentAvailabilityStatus.DemandAndFeasible:
                    _development.text = $"수요 있음 / 개발 가능 {snapshot.TotalFeasibleParcelCount:N0} 필지";
                    _development.AddToClassList("osc-hud-status");
                    _development.RemoveFromClassList("osc-hud-status--warning");
                    break;
                case DevelopmentAvailabilityStatus.DemandButNoFeasibleParcels:
                    _development.text = "수요 있음 / 개발 가능 필지 없음";
                    _development.RemoveFromClassList("osc-hud-status");
                    _development.AddToClassList("osc-hud-status--warning");
                    break;
                default:
                    _development.text = "수요 없음";
                    _development.RemoveFromClassList("osc-hud-status");
                    _development.RemoveFromClassList("osc-hud-status--warning");
                    break;
            }

            _reasons.text = ReasonText(snapshot);
            _reasons.EnableInClassList("osc-hud-reason", snapshot.TopBlockReasons.Length > 0);
        }

        private void AddTaxButton(VisualElement parent, string name, string text, TaxSector sector)
        {
            var button = new Button(() => RequestTaxIncrease(sector))
            {
                name = name,
                text = text,
                tooltip = $"{text.Replace("+", " 세율 +")}%"
            };
            button.AddToClassList(UiFoundationNames.ItemClass);
            button.AddToClassList("osc-hud-action");
            parent.Add(button);
        }

        /// <summary>Shared UI action seam used by the tax buttons and PlayMode wiring smoke.</summary>
        private void RequestTaxIncrease(TaxSector sector)
        {
            if (CityPersistenceContext.Current?.ViewModel.IsBusy == true)
                return;

            var context = CityRuntimeContext.Current;
            if (context == null)
                return;

            var hud = context.Runtime.HudModel.Snapshot;
            var residential = hud.HasPendingTaxRateChange
                ? hud.PendingResidentialTaxPermille
                : hud.ActiveResidentialTaxPermille;
            var commercial = hud.HasPendingTaxRateChange
                ? hud.PendingCommercialTaxPermille
                : hud.ActiveCommercialTaxPermille;
            var industrial = hud.HasPendingTaxRateChange
                ? hud.PendingIndustrialTaxPermille
                : hud.ActiveIndustrialTaxPermille;

            switch (sector)
            {
                case TaxSector.Residential:
                    residential = Math.Min(TaxPermilleMaximum, residential + 10);
                    break;
                case TaxSector.Commercial:
                    commercial = Math.Min(TaxPermilleMaximum, commercial + 10);
                    break;
                case TaxSector.Industrial:
                    industrial = Math.Min(TaxPermilleMaximum, industrial + 10);
                    break;
            }

            var execution = context.Runtime.Execute(
                new SetMunicipalTaxRatesCommand(residential, commercial, industrial));
            _feedback.text = execution.IsApplied
                ? "✓ 세율 변경이 다음 월에 적용됩니다."
                : execution.Message;
            _feedback.EnableInClassList(UiFoundationNames.ItemErrorClass, !execution.IsApplied);
            _lastRefreshKey = null;
        }

        private Button AddSpeedButton(VisualElement parent, string name, string text, SimulationSpeed speed)
        {
            var button = new Button(() => RequestSimulationSpeed(speed))
            {
                name = name,
                text = text
            };
            button.AddToClassList(UiFoundationNames.ItemClass);
            parent.Add(button);
            return button;
        }

        public void RequestResidentialTaxIncrease()
        {
            RequestTaxIncrease(TaxSector.Residential);
        }

        public void RequestSimulationSpeed(SimulationSpeed speed)
        {
            if (CityPersistenceContext.Current?.ViewModel.IsBusy == true)
                return;

            CityRuntimeContext.Current?.Runtime.SetSimulationSpeed(speed);
            _lastRefreshKey = null;
        }

        private void SetSpeedSelection(SimulationSpeed speed)
        {
            SetSelected(_pause, speed == SimulationSpeed.Paused);
            SetSelected(_oneX, speed == SimulationSpeed.One);
            SetSelected(_twoX, speed == SimulationSpeed.Two);
            SetSelected(_fourX, speed == SimulationSpeed.Four);
        }

        private static void SetSelected(VisualElement element, bool selected)
        {
            element?.EnableInClassList(UiFoundationNames.ItemSelectedClass, selected);
        }

        private static Label AddMetric(VisualElement parent, string title, string valueName)
        {
            var group = new VisualElement();
            group.AddToClassList("osc-hud-metric");
            var caption = new Label(title);
            caption.AddToClassList(UiFoundationNames.PanelCaptionClass);
            group.Add(caption);
            var value = new Label { name = valueName };
            value.AddToClassList("osc-hud-value");
            group.Add(value);
            parent.Add(group);
            return value;
        }

        private static VisualElement CreateSection(string name, string className)
        {
            var section = new VisualElement { name = name };
            section.AddToClassList(className);
            return section;
        }

        private static string TaxText(
            int residentialPermille,
            int commercialPermille,
            int industrialPermille,
            bool hasPendingChange,
            int pendingYear,
            int pendingMonth)
        {
            var active = $"R {residentialPermille / 10}% · C {commercialPermille / 10}% · I {industrialPermille / 10}%";
            return hasPendingChange
                ? $"{active} → {pendingYear}-{pendingMonth:D2} 적용"
                : active;
        }

        private static string ReasonText(MunicipalHudSnapshotDto snapshot)
        {
            if (snapshot.TopBlockReasons == null || snapshot.TopBlockReasons.Length == 0)
                return $"접근성 {snapshot.AverageAccessibilityFactor:N0} · 외부 도로 {(snapshot.HasReachableExternalRoad ? "연결" : "없음")} · 전력 {(snapshot.HasConnectedPowerProducer ? "연결" : "없음")}";

            var result = string.Empty;
            for (var index = 0; index < snapshot.TopBlockReasons.Length; index++)
            {
                var reason = snapshot.TopBlockReasons[index];
                if (index > 0)
                    result += " / ";
                result += $"{ReasonName(reason.Code)} {reason.ParcelCount:N0}";
            }

            return result;
        }

        private static string ReasonName(string reasonCode)
        {
            switch (reasonCode)
            {
                case "RoadIneligible":
                    return "외부 도로 미연결";
                case "NoPowerCapacity":
                    return "전력 부족";
                case "NoMatchingDefinition":
                    return "개발 정의 없음";
                default:
                    return reasonCode;
            }
        }

        private static string FormatMonth(int year, int month) => $"{year}년 {month}월";

        private static string FormatDate(int year, int month, int day) =>
            $"{year}년 {month}월 {day}일";
    }
}
