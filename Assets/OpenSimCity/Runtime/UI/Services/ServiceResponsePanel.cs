using OpenSimCity.Application.Services;
using OpenSimCity.Gameplay.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Services
{
    [DisallowMultipleComponent]
    public sealed class ServiceResponsePanel : MonoBehaviour
    {
        private PublicServiceUiBinder _binder;
        private PublicServiceRuntimeHost _host;
        private ServiceResponseProjection _projection;
        private VisualElement _section;
        private Label _status;
        private int _version = -1;
        private int _settingsVersion = -1;

        private void Update()
        {
            if (_binder == null) _binder = GetComponent<PublicServiceUiBinder>();
            if (_host == null) _host = Object.FindFirstObjectByType<PublicServiceRuntimeHost>();
            var panel = _binder?.Panel;
            if (panel == null || _host?.Module == null) return;
            if (_section == null || _section.parent != panel) Build(panel);
            if (_projection == null) _projection = new ServiceResponseProjection(_host.Module);
            _projection.RefreshIfNeeded();
            var settings = ServiceResponseSettings.Current;
            if (_version == _projection.Version && _settingsVersion == settings.Version) return;
            _version = _projection.Version;
            _settingsVersion = settings.Version;
            var routeCount = 0;
            for (var index = 0; index < _projection.Events.Count; index++)
                if (_projection.Events[index].HasRoute) routeCount++;
            _status.text = $"미서비스 사건 {_projection.Events.Count} · 출동 경로 {routeCount} · 표시 상한 {settings.MaximumSamples}";
        }

        private void Build(VisualElement panel)
        {
            _section?.RemoveFromHierarchy();
            _section = new VisualElement { name = "osc-service-response-section" };
            _section.style.marginTop = 8;
            _status = new Label { name = "osc-service-response-status" };
            _section.Add(_status);
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.Add(new Button(() => ServiceResponseSettings.Current.SetMaximumSamples(0)) { text = "차량 표시 0" });
            row.Add(new Button(() => ServiceResponseSettings.Current.SetMaximumSamples(ServiceResponseProjection.DefaultMaximumSamples)) { text = "차량 기본" });
            _section.Add(row);
            panel.Add(_section);
            _version = -1;
        }
    }
}
