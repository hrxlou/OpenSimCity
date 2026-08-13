using System;
using System.Text;
using OpenSimCity.Application.Conditions;
using OpenSimCity.Application.Input;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Conditions
{
    /// <summary>
    /// Selected-building condition inspector and datamap control. Every displayed
    /// factor, period and action reason comes from Application read models.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingConditionUiBinder : MonoBehaviour
    {
        [SerializeField] private UIDocument document;

        private CityRuntimeContext _context;
        private BuildingConditionRuntimeModule _module;
        private VisualElement _panel;
        private Label _summary;
        private Label _selectionTitle;
        private Label _details;
        private Label _factors;
        private Label _action;
        private Button _redevelop;
        private Button _datamap;
        private bool _pointerCaptured;
        private bool _keyboardCaptured;
        private int _observedReadVersion = -1;
        private int _observedSelectionVersion = -1;
        private int _observedMapVersion = -1;

        public VisualElement Panel => _panel;
        public BuildingConditionRuntimeModule Module => _module;

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
            _module.RefreshReadModels();
            var readVersion = _module.ReadModel.Snapshot.Version;
            var selectionVersion = BuildingConditionSelection.Current.Version;
            var mapVersion = BuildingConditionDatamapSelection.Current.Version;
            if (_observedReadVersion == readVersion &&
                _observedSelectionVersion == selectionVersion &&
                _observedMapVersion == mapVersion)
            {
                return;
            }
            _observedReadVersion = readVersion;
            _observedSelectionVersion = selectionVersion;
            _observedMapVersion = mapVersion;
            UpdateText();
        }

        private void BindRuntimeIfNeeded()
        {
            var current = CityRuntimeContext.Current;
            if (ReferenceEquals(current, _context))
                return;
            _context = current;
            _module = current == null
                ? null
                : BuildingConditionRuntimeRegistry.GetOrCreate(current.Runtime);
            _observedReadVersion = -1;
            _observedSelectionVersion = -1;
            _observedMapVersion = -1;
            UpdateText();
        }

        private void RebuildPanel()
        {
            if (!isActiveAndEnabled || document == null || document.rootVisualElement == null)
                return;
            _panel?.RemoveFromHierarchy();
            _panel = new VisualElement { name = "osc-building-condition-panel" };
            _panel.style.position = Position.Absolute;
            _panel.style.right = 12;
            _panel.style.bottom = 12;
            _panel.style.width = 330;
            _panel.style.paddingLeft = 10;
            _panel.style.paddingRight = 10;
            _panel.style.paddingTop = 8;
            _panel.style.paddingBottom = 8;
            _panel.style.backgroundColor = new Color(0.055f, 0.075f, 0.10f, 0.94f);
            _panel.style.borderTopLeftRadius = 6;
            _panel.style.borderTopRightRadius = 6;
            _panel.style.borderBottomLeftRadius = 6;
            _panel.style.borderBottomRightRadius = 6;

            var heading = new Label("건물 상태") { name = "osc-condition-heading" };
            heading.style.fontSize = 15;
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            _panel.Add(heading);

            _summary = CreateLabel("osc-condition-summary");
            _panel.Add(_summary);
            _datamap = new Button(ToggleDatamap) { name = "osc-condition-datamap" };
            _panel.Add(_datamap);

            _selectionTitle = CreateLabel("osc-condition-selection-title");
            _selectionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _panel.Add(_selectionTitle);
            _details = CreateLabel("osc-condition-details");
            _panel.Add(_details);
            _factors = CreateLabel("osc-condition-factors");
            _panel.Add(_factors);
            _action = CreateLabel("osc-condition-action");
            _panel.Add(_action);
            _redevelop = new Button(RequestRedevelopment)
            {
                name = "osc-condition-redevelop",
                text = "재개발 요청"
            };
            _panel.Add(_redevelop);

            _panel.RegisterCallback<PointerEnterEvent>(_ => CapturePointer());
            _panel.RegisterCallback<PointerLeaveEvent>(_ => ReleasePointer());
            _panel.RegisterCallback<FocusInEvent>(_ => CaptureKeyboard());
            _panel.RegisterCallback<FocusOutEvent>(_ => ReleaseKeyboard());
            document.rootVisualElement.Add(_panel);
            UpdateText();
        }

        private static Label CreateLabel(string name)
        {
            var label = new Label { name = name };
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 4;
            return label;
        }

        private void ToggleDatamap()
        {
            BuildingConditionDatamapSelection.Current.Toggle();
            UpdateText();
        }

        private void RequestRedevelopment()
        {
            if (_module == null || !BuildingConditionSelection.Current.HasSelection)
                return;
            var result = _module.Execute(new RequestBuildingRedevelopmentCommand(
                BuildingConditionSelection.Current.BuildingId));
            _action.text = result.IsApplied
                ? "재개발이 시작되었습니다. 월간 진행도를 확인하세요."
                : result.Message;
            UpdateText();
        }

        private void UpdateText()
        {
            if (_summary == null || _selectionTitle == null || _details == null ||
                _factors == null || _action == null || _redevelop == null || _datamap == null)
            {
                return;
            }
            if (_module == null)
            {
                _summary.text = "도시 runtime을 기다리는 중";
                ClearSelectionText();
                return;
            }

            var snapshot = _module.ReadModel.Snapshot;
            _summary.text =
                $"정상 {snapshot.HealthyCount} · 압박 {snapshot.StressedCount} · " +
                $"쇠퇴 {snapshot.DecliningCount} · 폐건물 {snapshot.AbandonedCount} · " +
                $"재개발 {snapshot.RedevelopingCount}\n" +
                (snapshot.HasProcessedMonth
                    ? $"적용 기간 {snapshot.ProcessedYear}년 {snapshot.ProcessedMonth}월"
                    : "아직 처리된 월 없음");
            _datamap.text = BuildingConditionDatamapSelection.Current.IsVisible
                ? "상태 데이터맵 끄기"
                : "상태 데이터맵 켜기";

            if (!BuildingConditionSelection.Current.HasSelection ||
                !_module.ReadModel.TryGet(
                    BuildingConditionSelection.Current.BuildingId,
                    out var row))
            {
                ClearSelectionText();
                return;
            }

            _selectionTitle.text = $"선택 건물 {row.BuildingId.Value} · {StageText(row.Stage)}";
            var capacity = row.EffectiveHousingCapacity > 0
                ? $"주거 {row.Occupancy}/{row.EffectiveHousingCapacity}"
                : $"고용 {row.FilledJobs}/{row.EffectiveJobCapacity}";
            _details.text =
                $"상태 점수 {row.AdverseScore} · {capacity}\n" +
                $"악화 {row.AdverseMonths}개월 · 회복 {row.RecoveryMonths}개월" +
                (row.Stage == BuildingConditionStageView.Redeveloping
                    ? $" · 남은 재개발 {row.RedevelopmentMonthsRemaining}개월"
                    : string.Empty);

            var factorText = new StringBuilder();
            factorText.Append("원인");
            if (row.Factors.Count == 0)
            {
                factorText.Append(": 없음");
            }
            else
            {
                for (var index = 0; index < row.Factors.Count; index++)
                {
                    factorText.Append(index == 0 ? ": " : "\n");
                    factorText.Append(index + 1);
                    factorText.Append(". ");
                    factorText.Append(ReasonText(row.Factors[index].Reason));
                    factorText.Append(" +");
                    factorText.Append(row.Factors[index].Contribution);
                }
            }
            _factors.text = factorText.ToString();
            _action.text = ActionText(row);
            _redevelop.SetEnabled(row.IsRedevelopmentEligible);
        }

        private void ClearSelectionText()
        {
            _selectionTitle.text = "월드에서 건물을 선택하세요.";
            _details.text = string.Empty;
            _factors.text = string.Empty;
            _action.text = string.Empty;
            _redevelop.SetEnabled(false);
        }

        private static string ActionText(BuildingConditionBuildingReadModel row)
        {
            if (row.Stage == BuildingConditionStageView.Redeveloping)
                return "도로·전력·환경 조건을 유지하면 월간 진행이 계속됩니다.";
            if (row.IsRedevelopmentEligible)
                return "조건이 회복되었습니다. 재개발을 요청할 수 있습니다.";
            if (row.Stage == BuildingConditionStageView.Abandoned)
                return BlockText(row.RedevelopmentBlockReason);
            switch (row.TopReason)
            {
                case BuildingConditionReasonView.NoRoadAccess:
                    return "도로와 외부 관문 연결을 복구하세요.";
                case BuildingConditionReasonView.NoPower:
                    return "발전 용량과 도로 전력 연결을 복구하세요.";
                case BuildingConditionReasonView.Pollution:
                    return "오염 source를 철거하거나 주거·산업 간격을 늘리세요.";
                case BuildingConditionReasonView.Crime:
                    return "범죄 pressure source를 완화하세요.";
                case BuildingConditionReasonView.Noise:
                    return "교통 소음 source 또는 도로 배치를 완화하세요.";
                case BuildingConditionReasonView.LowLandValue:
                    return "공원·환경 개선으로 토지가치를 회복하세요.";
                case BuildingConditionReasonView.LowOccupancy:
                    return "수요·일자리·접근성 조건을 회복하세요.";
                default:
                    return "현재 즉시 필요한 조치 없음";
            }
        }

        private static string BlockText(RedevelopmentBlockReasonView reason)
        {
            switch (reason)
            {
                case RedevelopmentBlockReasonView.NoRoadAccess:
                    return "재개발 차단: 도로·외부 연결 없음";
                case RedevelopmentBlockReasonView.NoPower:
                    return "재개발 차단: 전력 공급 없음";
                case RedevelopmentBlockReasonView.EnvironmentNotRecovered:
                    return "재개발 차단: 환경 조건 미회복";
                case RedevelopmentBlockReasonView.RecoveryDelay:
                    return "재개발 차단: 회복 조건 유지 기간 부족";
                default:
                    return "재개발 조건을 확인하세요.";
            }
        }

        private static string StageText(BuildingConditionStageView stage)
        {
            switch (stage)
            {
                case BuildingConditionStageView.Healthy: return "정상";
                case BuildingConditionStageView.Stressed: return "압박";
                case BuildingConditionStageView.Declining: return "쇠퇴";
                case BuildingConditionStageView.Abandoned: return "폐건물";
                default: return "재개발 중";
            }
        }

        private static string ReasonText(BuildingConditionReasonView reason)
        {
            switch (reason)
            {
                case BuildingConditionReasonView.NoRoadAccess: return "도로 접근 없음";
                case BuildingConditionReasonView.NoPower: return "전력 없음";
                case BuildingConditionReasonView.Pollution: return "오염";
                case BuildingConditionReasonView.Crime: return "범죄";
                case BuildingConditionReasonView.Noise: return "소음";
                case BuildingConditionReasonView.LowLandValue: return "낮은 토지가치";
                case BuildingConditionReasonView.LowOccupancy: return "낮은 점유율";
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

        private void ReleaseInputCaptures()
        {
            ReleasePointer();
            ReleaseKeyboard();
        }
    }
}
