using System;
using System.Text;
using OpenSimCity.Application.Districts;
using OpenSimCity.Application.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Districts
{
    [DisallowMultipleComponent]
    public sealed class DistrictPolicyUiBinder : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        private CityRuntimeContext _context;
        private DistrictPolicyRuntimeModule _module;
        private DistrictPolicyReadModel _readModel;
        private VisualElement _panel;
        private Label _summary;
        private Label _policies;
        private Label _advice;
        private int _version = -1;
        private int _selectionVersion = -1;

        public void Configure(UIDocument target) { document = target; BuildPanel(); }
        private void OnEnable() { if (document == null) document = GetComponent<UIDocument>(); BuildPanel(); }
        private void OnDisable() { _panel?.RemoveFromHierarchy(); _panel = null; _context = null; _module = null; _readModel = null; }
        private void Update()
        {
            BindIfNeeded();
            if (_readModel == null) return;
            _readModel.RefreshIfNeeded();
            var selection = DistrictSelection.Current;
            if (_version == _readModel.Snapshot.Version && _selectionVersion == selection.Version) return;
            _version = _readModel.Snapshot.Version; _selectionVersion = selection.Version; RefreshText();
        }

        private void BindIfNeeded()
        {
            var current = CityRuntimeContext.Current;
            if (ReferenceEquals(current, _context)) return;
            _context = current;
            _module = current == null ? null : DistrictPolicyRuntimeRegistry.GetOrCreate(current.Runtime);
            _readModel = _module == null ? null : new DistrictPolicyReadModel(_module);
            _version = -1;
        }

        private void BuildPanel()
        {
            if (!isActiveAndEnabled || document == null || document.rootVisualElement == null) return;
            _panel?.RemoveFromHierarchy();
            _panel = new VisualElement { name = "osc-district-policy-panel" };
            _panel.style.position = Position.Absolute; _panel.style.left = 334; _panel.style.top = 340; _panel.style.width = 360;
            _panel.style.paddingLeft = 10; _panel.style.paddingRight = 10; _panel.style.paddingTop = 8; _panel.style.paddingBottom = 8;
            _panel.style.backgroundColor = new Color(0.06f, 0.07f, 0.08f, 0.94f);
            var title = new Label("지구 정책") { name = "osc-district-policy-title" }; title.style.unityFontStyleAndWeight = FontStyle.Bold; _panel.Add(title);
            _summary = AddLabel("osc-district-policy-summary");
            _policies = AddLabel("osc-district-policy-rows");
            AddButton("화물차 제한 전환", ToggleHeavyVehicleRestriction);
            AddButton("대중교통 보조금 전환", ToggleTransitSubsidy);
            _advice = AddLabel("osc-district-policy-advice");
            document.rootVisualElement.Add(_panel);
        }

        private Label AddLabel(string name) { var label = new Label { name = name }; label.style.whiteSpace = WhiteSpace.Normal; _panel.Add(label); return label; }
        private void AddButton(string text, Action action) { _panel.Add(new Button(action) { text = text }); }

        private void ToggleHeavyVehicleRestriction()
        {
            Toggle(module => module.ToggleHeavyVehicleRestriction(DistrictSelection.Current.ActiveDistrictId));
        }

        private void ToggleTransitSubsidy()
        {
            Toggle(module => module.ToggleTransitSubsidy(DistrictSelection.Current.ActiveDistrictId));
        }

        private void Toggle(Func<DistrictPolicyRuntimeModule, OpenSimCity.Application.Commands.CommandExecutionResult> action)
        {
            var id = DistrictSelection.Current.ActiveDistrictId;
            if (_module == null || !id.IsValid) { _advice.text = "먼저 지구를 선택하세요."; return; }
            var result = action(_module);
            _advice.text = result.IsApplied ? "다음 월부터 정책 변경이 예약됐습니다." : result.Message;
            _readModel.RefreshIfNeeded();
        }

        private void RefreshText()
        {
            var id = DistrictSelection.Current.ActiveDistrictId;
            DistrictStatsViewData active = null;
            for (var index = 0; index < _readModel.Snapshot.Districts.Count; index++)
                if (_readModel.Snapshot.Districts[index].DistrictId == id) { active = _readModel.Snapshot.Districts[index]; break; }
            if (active == null)
            {
                _summary.text = "지구를 선택하면 정책 비용과 지난 기간 결과를 비교할 수 있습니다.";
                _policies.text = string.Empty;
                return;
            }

            var period = _readModel.Snapshot.HasProcessedMonth ? $"{_readModel.Snapshot.Year}-{_readModel.Snapshot.Month:00}" : "미정산";
            _summary.text = $"{active.Name} · 지난 기간 {period}\n인구 {active.Population} · 일자리 {active.Jobs} · 통과 통행 {active.RoutedTrips} · 소음 {active.TrafficNoisePermille}/1000\n버스 {active.BusTrips} · 자동차 {active.PrivateVehicleTrips} · 서비스 {active.ServiceCoveragePermille}/1000\n오염 {active.Pollution} · 지난 정책 지출 {active.PolicyCost}";
            var builder = new StringBuilder();
            for (var index = 0; index < active.Policies.Count; index++)
            {
                var row = active.Policies[index];
                var isHeavyVehicleRestriction = index == 0;
                var name = isHeavyVehicleRestriction ? "화물차 제한" : "대중교통 보조금";
                var input = isHeavyVehicleRestriction ? $"화물 route preference 1000→{row.InputPermille}" : $"bus generalized cost 1000→{row.InputPermille}";
                var operatingReason = !row.Enabled
                    ? "꺼짐"
                    : row.Effective
                        ? "효과 적용"
                        : row.LastProjectedCost > 0 && row.LastPaidCost == 0
                            ? "예산 부족"
                            : "효과 미적용";
                builder.Append(name).Append(" · ").Append(operatingReason)
                    .Append(" · 월 비용 ").Append(row.MonthlyCost).Append(" · ").Append(input)
                    .Append("\n지난 비용 ").Append(row.LastPaidCost).Append('/').Append(row.LastProjectedCost);
                if (row.HasPending) builder.Append(" · 다음 적용 ").Append(row.PendingYear).Append('-').Append(row.PendingMonth.ToString("00")).Append(row.PendingEnabled ? " ON" : " OFF");
                if (index + 1 < active.Policies.Count) builder.Append('\n');
            }
            _policies.text = builder.ToString();
            if (string.IsNullOrEmpty(_advice.text)) _advice.text = "정책은 다음 월 경계에서 적용되며, 비용을 낸 기간에만 교통 입력을 변경합니다.";
        }
    }
}
