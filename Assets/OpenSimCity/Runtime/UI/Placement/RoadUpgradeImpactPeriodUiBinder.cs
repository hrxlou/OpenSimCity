using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.UI.Foundation;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Placement
{
    /// <summary>
    /// Appends the public upgrade impact period and route-failure reason to the
    /// road-upgrade inspector. It is installed in code so no scene YAML or view
    /// object becomes authoritative state.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(210)]
    public sealed class RoadUpgradeImpactPeriodUiBinder : MonoBehaviour
    {
        private const string LabelName = "osc-road-upgrade-period";

        private UiFoundationBootstrap _bootstrap;
        private CityRuntime _observedRuntime;
        private RoadUpgradeImpactPublicationReadModel _publication;
        private Label _label;
        private VisualElement _observedPanel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var bootstrap = FindFirstObjectByType<UiFoundationBootstrap>();
            if (bootstrap == null || bootstrap.GetComponent<RoadUpgradeImpactPeriodUiBinder>() != null)
                return;

            bootstrap.gameObject.AddComponent<RoadUpgradeImpactPeriodUiBinder>();
        }

        private void Awake()
        {
            _bootstrap = GetComponent<UiFoundationBootstrap>();
        }

        private void Update()
        {
            if (_bootstrap == null)
                _bootstrap = FindFirstObjectByType<UiFoundationBootstrap>();
            if (_bootstrap?.Root == null)
                return;

            var panel = _bootstrap.Root.Q<VisualElement>("osc-road-upgrade-panel");
            if (!ReferenceEquals(panel, _observedPanel))
            {
                _observedPanel = panel;
                _label = panel?.Q<Label>(LabelName);
                if (panel != null && _label == null)
                {
                    _label = new Label(string.Empty) { name = LabelName };
                    _label.AddToClassList(UiFoundationNames.PanelCaptionClass);
                    panel.Add(_label);
                }
            }

            if (_label == null)
                return;

            var context = CityRuntimeContext.Current;
            var impact = context?.Tool.RoadUpgradePreview ??
                         context?.Tool.LastCommittedRoadUpgradePreview;
            if (context == null || impact == null)
            {
                _label.text = string.Empty;
                _label.style.display = DisplayStyle.None;
                return;
            }

            if (!ReferenceEquals(_observedRuntime, context.Runtime))
            {
                _observedRuntime = context.Runtime;
                _publication = new RoadUpgradeImpactPublicationReadModel(context.Runtime);
            }

            var publication = _publication.Publish(impact);
            var reason = string.IsNullOrEmpty(publication.NewReachabilityFailureReason)
                ? "새 도달 불가 없음"
                : publication.NewReachabilityFailureReason;
            _label.text = $"적용 월 {publication.AppliesToPeriodLabel} · {reason}";
            _label.style.display = DisplayStyle.Flex;
        }
    }
}
