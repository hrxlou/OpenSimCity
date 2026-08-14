using System;
using System.Text;
using OpenSimCity.Application.Input;
using OpenSimCity.Application.Services;
using OpenSimCity.Application.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Services
{
    [DisallowMultipleComponent]
    public sealed class PublicServiceUiBinder : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        private CityRuntimeContext _context;
        private PublicServiceRuntimeModule _module;
        private VisualElement _panel;
        private Label _summary;
        private Label _selection;
        private Label _facilities;
        private Label _feedback;
        private Button _coverage;
        private bool _pointerCaptured;
        private bool _keyboardCaptured;
        private int _readVersion = -1;
        private int _selectionVersion = -1;

        public VisualElement Panel => _panel;
        internal PublicServiceRuntimeModule Module => _module;

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
            _context = null;
            _module = null;
        }

        private void Update()
        {
            BindIfNeeded();
            if (_module == null) return;
            _module.ReadModel.RefreshIfNeeded();
            var readVersion = _module.ReadModel.Snapshot.Version;
            var selectionVersion = PublicServiceSelection.Current.Version;
            if (_readVersion == readVersion && _selectionVersion == selectionVersion) return;
            _readVersion = readVersion;
            _selectionVersion = selectionVersion;
            RefreshText();
        }

        private void BindIfNeeded()
        {
            var current = CityRuntimeContext.Current;
            if (ReferenceEquals(current, _context)) return;
            _context = current;
            _module = current == null ? null : PublicServiceRuntimeRegistry.GetOrCreate(current.Runtime);
            _readVersion = -1;
        }

        private void Rebuild()
        {
            if (!isActiveAndEnabled || document == null || document.rootVisualElement == null) return;
            _panel?.RemoveFromHierarchy();
            _panel = new VisualElement { name = "osc-public-service-panel" };
            _panel.style.position = Position.Absolute;
            _panel.style.left = 12;
            _panel.style.bottom = 12;
            _panel.style.width = 360;
            _panel.style.paddingLeft = 10;
            _panel.style.paddingRight = 10;
            _panel.style.paddingTop = 8;
            _panel.style.paddingBottom = 8;
            _panel.style.backgroundColor = new Color(0.055f, 0.075f, 0.10f, 0.94f);
            _panel.style.borderTopLeftRadius = 6;
            _panel.style.borderTopRightRadius = 6;
            _panel.style.borderBottomLeftRadius = 6;
            _panel.style.borderBottomRightRadius = 6;

            var heading = new Label("공공서비스") { name = "osc-service-heading" };
            heading.style.fontSize = 15;
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            _panel.Add(heading);

            _summary = AddLabel("osc-service-summary");
            _selection = AddLabel("osc-service-selection");

            var kindRow = new VisualElement { name = "osc-service-kind-row" };
            kindRow.style.flexDirection = FlexDirection.Row;
            AddKindButton(kindRow, ServiceKindView.Police, "경찰");
            AddKindButton(kindRow, ServiceKindView.Fire, "소방");
            AddKindButton(kindRow, ServiceKindView.School, "학교");
            AddKindButton(kindRow, ServiceKindView.Healthcare, "병원");
            AddKindButton(kindRow, ServiceKindView.Waste, "폐기물");
            _panel.Add(kindRow);

            var actionRow = new VisualElement { name = "osc-service-action-row" };
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.Add(new Button(PlaceSelected) { name = "osc-service-place", text = "선택 셀에 배치" });
            _coverage = new Button(ToggleCoverage) { name = "osc-service-coverage" };
            actionRow.Add(_coverage);
            _panel.Add(actionRow);

            var budgetRow = new VisualElement { name = "osc-service-budget-row" };
            budgetRow.style.flexDirection = FlexDirection.Row;
            budgetRow.Add(new Button(() => SetBudget(500)) { text = "예산 50%" });
            budgetRow.Add(new Button(() => SetBudget(1000)) { text = "예산 100%" });
            budgetRow.Add(new Button(() => SetBudget(1500)) { text = "예산 150%" });
            _panel.Add(budgetRow);

            _facilities = AddLabel("osc-service-facilities");
            _feedback = AddLabel("osc-service-feedback");
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

        private static void AddKindButton(VisualElement row, ServiceKindView kind, string text)
        {
            row.Add(new Button(() => PublicServiceSelection.Current.SetKind(kind)) { text = text });
        }

        private void PlaceSelected()
        {
            if (_module == null || !PublicServiceSelection.Current.SelectedCell.HasValue)
            {
                _feedback.text = "월드에서 빈 도로 인접 셀을 먼저 선택하세요.";
                return;
            }
            var kind = PublicServiceSelection.Current.ActiveKind;
            var cell = PublicServiceSelection.Current.SelectedCell.Value;
            var definitionId = PublicServiceDefinitionIds.For(kind);
            var facilityId = _module.FacilityIdFor(definitionId, cell);
            var result = _module.Execute(new PlaceServiceFacilityCommand(facilityId, definitionId, cell));
            if (result.IsApplied) PublicServiceSelection.Current.SelectFacility(facilityId);
            _feedback.text = result.IsApplied ? "시설을 배치했습니다." : result.Message;
            RefreshText();
        }

        private void ToggleCoverage()
        {
            var selection = PublicServiceSelection.Current;
            selection.SetCoverageVisible(!selection.IsCoverageVisible);
            RefreshText();
        }

        private void SetBudget(int budgetPermille)
        {
            var selectedId = PublicServiceSelection.Current.SelectedFacilityId;
            if (!selectedId.IsValid || _module == null)
            {
                _feedback.text = "예산을 변경할 시설 셀을 선택하세요.";
                return;
            }
            var result = _module.Execute(new SetServiceBudgetCommand(selectedId, budgetPermille));
            _feedback.text = result.IsApplied
                ? $"{budgetPermille / 10}% 예산을 다음 월에 적용합니다."
                : result.Message;
            RefreshText();
        }

        private void RefreshText()
        {
            if (_summary == null || _selection == null || _facilities == null ||
                _feedback == null || _coverage == null) return;
            if (_module == null)
            {
                _summary.text = "서비스 runtime을 기다리는 중";
                return;
            }
            _module.ReadModel.RefreshIfNeeded();
            var snapshot = _module.ReadModel.Snapshot;
            _summary.text = snapshot.HasProcessedMonth
                ? $"적용 기간 {snapshot.Year}년 {snapshot.Month}월 · 제공 {snapshot.ServedTotal}/{snapshot.RequestedTotal} · 운영비 {snapshot.MonthlyCost}"
                : "아직 처리된 서비스 월 없음";
            var selectedCell = PublicServiceSelection.Current.SelectedCell;
            _selection.text = selectedCell.HasValue
                ? $"선택 셀 {selectedCell.Value} · 도구 {KindText(PublicServiceSelection.Current.ActiveKind)}"
                : "월드에서 셀을 선택하세요.";
            _coverage.text = PublicServiceSelection.Current.IsCoverageVisible
                ? "coverage 끄기"
                : "coverage 켜기";

            var builder = new StringBuilder();
            var facilities = snapshot.Facilities;
            if (facilities.Count == 0)
            {
                builder.Append("배치된 시설 없음");
            }
            else
            {
                for (var index = 0; index < facilities.Count; index++)
                {
                    var facility = facilities[index];
                    if (index > 0) builder.Append('\n');
                    builder.Append(KindText(facility.Kind));
                    builder.Append(" · 사용 ");
                    builder.Append(facility.Usage);
                    builder.Append('/');
                    builder.Append(facility.Capacity);
                    builder.Append(" · 예산 ");
                    builder.Append(facility.ActiveBudgetPermille / 10);
                    builder.Append('%');
                    if (facility.HasPendingBudget)
                    {
                        builder.Append(" → ");
                        builder.Append(facility.PendingBudgetPermille / 10);
                        builder.Append('%');
                    }
                }
            }
            var topFailure = TopFailure(snapshot, PublicServiceSelection.Current.ActiveKind);
            if (topFailure != ServiceFailureReasonView.None)
            {
                builder.Append("\n상위 미서비스 원인: ");
                builder.Append(FailureText(topFailure));
            }
            _facilities.text = builder.ToString();
        }

        private static ServiceFailureReasonView TopFailure(
            PublicServiceReadModelSnapshot snapshot,
            ServiceKindView kind)
        {
            var counts = new int[Enum.GetValues(typeof(ServiceFailureReasonView)).Length];
            for (var index = 0; index < snapshot.Coverage.Count; index++)
            {
                var row = snapshot.Coverage[index];
                if (row.Kind == kind && row.FailureReason != ServiceFailureReasonView.None)
                    counts[(int)row.FailureReason]++;
            }
            var best = ServiceFailureReasonView.None;
            for (var index = 1; index < counts.Length; index++)
            {
                if (counts[index] > counts[(int)best]) best = (ServiceFailureReasonView)index;
            }
            return best;
        }

        private static string KindText(ServiceKindView kind)
        {
            switch (kind)
            {
                case ServiceKindView.Police: return "경찰";
                case ServiceKindView.Fire: return "소방";
                case ServiceKindView.School: return "학교";
                case ServiceKindView.Healthcare: return "병원";
                default: return "폐기물";
            }
        }

        private static string FailureText(ServiceFailureReasonView reason)
        {
            switch (reason)
            {
                case ServiceFailureReasonView.NoFacility: return "시설 없음";
                case ServiceFailureReasonView.NoRoadAccess: return "도로 접근 없음";
                case ServiceFailureReasonView.DifferentRoadComponent: return "고립 도로망";
                case ServiceFailureReasonView.OutOfRange: return "범위 밖";
                case ServiceFailureReasonView.CapacityExceeded: return "용량 초과";
                case ServiceFailureReasonView.BudgetTooLow: return "예산 부족";
                default: return "없음";
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
