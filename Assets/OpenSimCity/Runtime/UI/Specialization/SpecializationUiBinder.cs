using System;
using System.Text;
using OpenSimCity.Application.Specialization;
using OpenSimCity.Application.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Specialization
{
    /// <summary>Player-facing projection of the P7-04 specialization state.</summary>
    [DisallowMultipleComponent]
    public sealed class SpecializationUiBinder : MonoBehaviour
    {
        [SerializeField] private UIDocument document;

        private CityRuntimeContext _context;
        private SpecializationRuntimeModule _module;
        private SpecializationReadModel _readModel;
        private VisualElement _panel;
        private Label _summary;
        private Label _stages;
        private Label _diagnostics;
        private Label _advice;
        private int _version = -1;

        public VisualElement Panel => _panel;
        public SpecializationReadModel ReadModel => _readModel;

        public void Configure(UIDocument target)
        {
            document = target;
            BuildPanel();
        }

        private void OnEnable()
        {
            if (document == null) document = GetComponent<UIDocument>();
            BuildPanel();
        }

        private void OnDisable()
        {
            _panel?.RemoveFromHierarchy();
            _panel = null;
            _context = null;
            _module = null;
            _readModel = null;
        }

        private void Update()
        {
            BindIfNeeded();
            if (_readModel == null) return;
            _readModel.RefreshIfNeeded();
            if (_version == _readModel.Snapshot.Version) return;
            _version = _readModel.Snapshot.Version;
            RefreshText();
        }

        private void BindIfNeeded()
        {
            var current = CityRuntimeContext.Current;
            if (ReferenceEquals(current, _context)) return;
            _context = current;
            _module = current == null
                ? null
                : OpenSimCity.Application.UiRuntimeFacades.GetSpecialization(current.Runtime);
            _readModel = _module == null ? null : new SpecializationReadModel(_module);
            _version = -1;
        }

        private void BuildPanel()
        {
            if (!isActiveAndEnabled || document == null || document.rootVisualElement == null) return;
            _panel?.RemoveFromHierarchy();
            _panel = new VisualElement { name = "osc-specialization-panel" };
            _panel.style.position = Position.Absolute;
            _panel.style.right = 12;
            _panel.style.top = 340;
            _panel.style.width = 330;
            _panel.style.paddingLeft = 10;
            _panel.style.paddingRight = 10;
            _panel.style.paddingTop = 8;
            _panel.style.paddingBottom = 8;
            _panel.style.backgroundColor = new Color(0.06f, 0.07f, 0.08f, 0.94f);
            var title = new Label("경공업 전문화") { name = "osc-specialization-title" };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _panel.Add(title);
            _summary = AddLabel("osc-specialization-summary");
            _stages = AddLabel("osc-specialization-stages");
            _diagnostics = AddLabel("osc-specialization-diagnostics");
            _advice = AddLabel("osc-specialization-advice");
            document.rootVisualElement.Add(_panel);
        }

        private Label AddLabel(string name)
        {
            var label = new Label { name = name };
            label.style.whiteSpace = WhiteSpace.Normal;
            _panel.Add(label);
            return label;
        }

        private void RefreshText()
        {
            var snapshot = _readModel.Snapshot;
            if (!snapshot.HasProcessedMonth)
            {
                _summary.text = "생산 기간을 기다리는 중";
                _stages.text = string.Empty;
                _diagnostics.text = string.Empty;
                _advice.text = "산업 일자리와 외부 도로 연결이 전문화의 시작 조건입니다.";
                return;
            }

            _summary.text =
                $"기간 {snapshot.Year}-{snapshot.Month:00} · 진행 {snapshot.ProgressPermille}/1000\n" +
                $"재고 원료 {snapshot.RawInventory} · 부품 {snapshot.IntermediateInventory} · 완제품 {snapshot.FinishedInventory}\n" +
                $"민간 수익/비용 {snapshot.LastRevenue}/{snapshot.LastCost} · 순익 {snapshot.LastNetProfit} · 누적 수출 {snapshot.CumulativeExports}";

            var stageText = new StringBuilder();
            SpecializationBottleneckView top = SpecializationBottleneckView.None;
            for (var index = 0; index < snapshot.Stages.Count; index++)
            {
                var row = snapshot.Stages[index];
                if (index > 0) stageText.Append('\n');
                stageText.Append(StageText(row.Kind)).Append(" · ")
                    .Append(row.IsUnlocked ? "해금" : "잠김")
                    .Append(" · 가동 ").Append(row.UtilizationPermille).Append("/1000")
                    .Append(" · ").Append(BottleneckText(row.Bottleneck));
                if (top == SpecializationBottleneckView.None && row.Bottleneck != SpecializationBottleneckView.None && row.Bottleneck != SpecializationBottleneckView.Locked)
                    top = row.Bottleneck;
            }
            _stages.text = stageText.ToString();

            var diagnosticText = new StringBuilder("압력/조건: ");
            for (var index = 0; index < snapshot.Diagnostics.Count; index++)
            {
                if (index > 0) diagnosticText.Append(" · ");
                diagnosticText.Append(DiagnosticText(snapshot.Diagnostics[index].Kind))
                    .Append(' ').Append(snapshot.Diagnostics[index].ValuePermille);
            }
            _diagnostics.text = diagnosticText.ToString();
            _advice.text = top == SpecializationBottleneckView.None
                ? "현재 생산 체인의 대표 중단 원인 없음"
                : "대표 병목: " + BottleneckText(top) + " · " + Advice(top);
        }

        private static string StageText(SpecializationStageView value)
        {
            switch (value)
            {
                case SpecializationStageView.MaterialPreparation: return "1. 원료 준비";
                case SpecializationStageView.Assembly: return "2. 조립";
                default: return "3. 수출";
            }
        }

        private static string DiagnosticText(SpecializationDiagnosticView value)
        {
            switch (value)
            {
                case SpecializationDiagnosticView.Labor: return "노동";
                case SpecializationDiagnosticView.Power: return "전력";
                case SpecializationDiagnosticView.Traffic: return "교통";
                case SpecializationDiagnosticView.Pollution: return "오염";
                case SpecializationDiagnosticView.Services: return "서비스";
                default: return "순익";
            }
        }

        private static string BottleneckText(SpecializationBottleneckView value)
        {
            switch (value)
            {
                case SpecializationBottleneckView.None: return "정상";
                case SpecializationBottleneckView.Locked: return "해금 조건 미달";
                case SpecializationBottleneckView.NoExternalMarket: return "외부 시장 연결 없음";
                case SpecializationBottleneckView.LaborShortage: return "산업 노동 부족";
                case SpecializationBottleneckView.PowerShortage: return "전력 공급 부족";
                case SpecializationBottleneckView.RawMaterialShortage: return "원료 부족";
                case SpecializationBottleneckView.IntermediateShortage: return "부품 부족";
                case SpecializationBottleneckView.FinishedGoodsShortage: return "완제품 부족";
                case SpecializationBottleneckView.TransportBottleneck: return "goods 통행 병목";
                case SpecializationBottleneckView.InventoryFull: return "재고 공간 포화";
                default: return value.ToString();
            }
        }

        private static string Advice(SpecializationBottleneckView value)
        {
            switch (value)
            {
                case SpecializationBottleneckView.NoExternalMarket: return "외부 도로 연결을 복구하세요.";
                case SpecializationBottleneckView.LaborShortage: return "산업 고용과 주거 노동 공급을 늘리세요.";
                case SpecializationBottleneckView.PowerShortage: return "전력 생산·연결 capacity를 확인하세요.";
                case SpecializationBottleneckView.TransportBottleneck: return "goods 경로의 도로 연결·용량을 개선하세요.";
                case SpecializationBottleneckView.InventoryFull: return "후속 생산/수출 병목을 먼저 해소하세요.";
                default: return "다음 기간의 입력·재고 흐름을 확인하세요.";
            }
        }
    }
}
