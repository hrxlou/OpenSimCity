using System;
using OpenSimCity.Application.Input;
using OpenSimCity.Application.Transit;
using OpenSimCity.Application.World;
using OpenSimCity.Simulation.Transit;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Transit
{
    /// <summary>
    /// Programmatic player panel for transit construction, budget, line diagnostics
    /// and current-period results. All values come from Application read models.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TransitUiBinder : MonoBehaviour
    {
        [SerializeField] private UIDocument document;

        private CityRuntimeContext _context;
        private TransitRuntimeModule _module;
        private TransitConstructionTool _tool;
        private VisualElement _panel;
        private Label _summary;
        private Label _reason;
        private Label _preview;
        private SliderInt _budget;
        private Button _commitLine;
        private bool _pointerCaptured;
        private bool _keyboardCaptured;
        private int _observedReadVersion = -1;

        public VisualElement Panel => _panel;
        public TransitRuntimeModule Module => _module;
        public TransitConstructionTool Tool => _tool;

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
            UnbindTool();
            ReleaseInputCaptures();
            _panel?.RemoveFromHierarchy();
            _panel = null;
        }

        private void Update()
        {
            BindRuntimeIfNeeded();
            if (_module == null)
                return;
            _module.RefreshDerived();
            _module.ReadModel.RefreshIfNeeded();
            var snapshot = _module.ReadModel.Snapshot;
            if (_observedReadVersion == snapshot.Version)
                return;
            _observedReadVersion = snapshot.Version;
            UpdateLabels();
        }

        private void BindRuntimeIfNeeded()
        {
            var current = CityRuntimeContext.Current;
            if (ReferenceEquals(current, _context))
                return;
            UnbindTool();
            _context = current;
            _module = current == null ? null : TransitRuntimeRegistry.GetOrCreate(current.Runtime);
            _tool = _module == null ? null : TransitConstructionToolRegistry.GetOrCreate(_module);
            if (_tool != null)
                _tool.PreviewChanged += HandlePreviewChanged;
            _observedReadVersion = -1;
            UpdateLabels();
        }

        private void UnbindTool()
        {
            if (_tool != null)
                _tool.PreviewChanged -= HandlePreviewChanged;
            _tool = null;
            _module = null;
        }

        private void RebuildPanel()
        {
            if (!isActiveAndEnabled || document == null || document.rootVisualElement == null)
                return;
            _panel?.RemoveFromHierarchy();
            _panel = new VisualElement { name = "osc-transit-panel" };
            _panel.style.position = Position.Absolute;
            _panel.style.left = 12;
            _panel.style.top = 190;
            _panel.style.width = 310;
            _panel.style.paddingLeft = 10;
            _panel.style.paddingRight = 10;
            _panel.style.paddingTop = 8;
            _panel.style.paddingBottom = 8;
            _panel.style.backgroundColor = new Color(0.055f, 0.075f, 0.10f, 0.94f);
            _panel.style.borderTopLeftRadius = 6;
            _panel.style.borderTopRightRadius = 6;
            _panel.style.borderBottomLeftRadius = 6;
            _panel.style.borderBottomRightRadius = 6;

            var title = new Label("버스 대중교통") { name = "osc-transit-title" };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 15;
            _panel.Add(title);

            var toolRow = Row();
            toolRow.Add(Button("정류장 (B)", () => _tool?.SelectStopPlacement()));
            toolRow.Add(Button("노선 (N)", () => _tool?.SelectLineEditor()));
            toolRow.Add(Button("취소", () => _tool?.Cancel()));
            _panel.Add(toolRow);

            var lineRow = Row();
            _commitLine = Button("노선 확정 (Enter)", CommitLine);
            lineRow.Add(_commitLine);
            lineRow.Add(Button("마지막 정류장 제거", () => _tool?.RemoveLastLineStop()));
            _panel.Add(lineRow);

            _budget = new SliderInt("운행 예산", 0, 1000) { name = "osc-transit-budget" };
            _budget.showInputField = true;
            _panel.Add(_budget);
            _panel.Add(Button("다음 월 예산 적용", ApplyBudget));

            _summary = new Label { name = "osc-transit-summary" };
            _summary.style.whiteSpace = WhiteSpace.Normal;
            _panel.Add(_summary);
            _reason = new Label { name = "osc-transit-reason" };
            _reason.style.whiteSpace = WhiteSpace.Normal;
            _panel.Add(_reason);
            _preview = new Label { name = "osc-transit-preview" };
            _preview.style.whiteSpace = WhiteSpace.Normal;
            _panel.Add(_preview);

            _panel.RegisterCallback<PointerEnterEvent>(_ => CapturePointer());
            _panel.RegisterCallback<PointerLeaveEvent>(_ => ReleasePointer());
            _panel.RegisterCallback<FocusInEvent>(_ => CaptureKeyboard());
            _panel.RegisterCallback<FocusOutEvent>(_ => ReleaseKeyboard());
            document.rootVisualElement.Add(_panel);
            UpdateLabels();
        }

        private void CommitLine()
        {
            if (_tool == null)
                return;
            var result = _tool.Commit();
            _preview.text = result.IsApplied ? "노선 명령이 적용되었습니다." : result.Message;
        }

        private void ApplyBudget()
        {
            if (_module == null)
                return;
            var result = _module.Execute(new SetTransitBudgetCommand(_budget.value));
            _preview.text = result.IsApplied
                ? $"운행 예산 {_budget.value}/1000이 다음 월에 적용됩니다."
                : result.Message;
        }

        private void HandlePreviewChanged(TransitConstructionPreview preview)
        {
            if (_preview == null)
                return;
            if (preview == null || preview.Mode == TransitConstructionToolMode.None)
            {
                _preview.text = string.Empty;
                return;
            }
            var state = preview.IsValid ? "배치 가능" : "배치 불가";
            var detail = string.IsNullOrWhiteSpace(preview.Message) ? string.Empty : $" · {preview.Message}";
            _preview.text = $"{state} · 비용 {preview.Cost}{detail}";
            if (_commitLine != null)
                _commitLine.SetEnabled(preview.Mode == TransitConstructionToolMode.LineEditor && preview.IsValid);
        }

        private void UpdateLabels()
        {
            if (_summary == null || _reason == null || _budget == null)
                return;
            if (_module == null)
            {
                _summary.text = "도시 runtime을 기다리는 중";
                _reason.text = string.Empty;
                return;
            }

            var snapshot = _module.ReadModel.Snapshot;
            _budget.SetValueWithoutNotify(
                snapshot.HasPendingBudget ? snapshot.PendingBudgetPermille : snapshot.ActiveBudgetPermille);
            _summary.text =
                $"정류장 {snapshot.Stops.Count} · 노선 {snapshot.Lines.Count}\n" +
                $"통근 {snapshot.RequestedCommuteTrips} · 버스 {snapshot.BusTrips} · 자동차 {snapshot.PrivateVehicleTrips}\n" +
                $"운영비 {snapshot.LastPaidOperatingExpense}/{snapshot.LastProjectedOperatingExpense} · " +
                $"자동차 경로량 {snapshot.TransitAwareRoutedTrips}/{snapshot.BaselineRoutedTrips}";

            TransitModeChoiceReason topReason = TransitModeChoiceReason.None;
            var topAmount = 0;
            for (var index = 0; index < snapshot.OdRows.Count; index++)
            {
                var row = snapshot.OdRows[index];
                if (row.Reason == TransitModeChoiceReason.None || row.RequestedTrips <= topAmount)
                    continue;
                topReason = row.Reason;
                topAmount = row.RequestedTrips;
            }
            _reason.text = topReason == TransitModeChoiceReason.None
                ? "현재 대표 차단 원인 없음"
                : $"대표 차단 원인: {ReasonText(topReason)} ({topAmount} trips)";
        }

        private static string ReasonText(TransitModeChoiceReason reason)
        {
            switch (reason)
            {
                case TransitModeChoiceReason.NoEnabledLine: return "운행 중인 노선 없음";
                case TransitModeChoiceReason.BudgetDisabled: return "운행 예산 0";
                case TransitModeChoiceReason.BudgetUnaffordable: return "운영비 부족";
                case TransitModeChoiceReason.OriginOutsideWalkingRange: return "출발지 정류장 접근 불가";
                case TransitModeChoiceReason.DestinationOutsideWalkingRange: return "도착지 정류장 접근 불가";
                case TransitModeChoiceReason.DirectionMismatch: return "노선 방향 불일치";
                case TransitModeChoiceReason.BrokenLine: return "노선 도로 단절";
                case TransitModeChoiceReason.TooIndirect: return "과도한 우회·대기 비용";
                case TransitModeChoiceReason.CapacityExhausted: return "노선 용량 부족";
                default: return reason.ToString();
            }
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
