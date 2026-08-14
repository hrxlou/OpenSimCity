using OpenSimCity.Application.Queries;
using OpenSimCity.Application.Settings;
using OpenSimCity.Application.World;
using UnityEngine;

namespace OpenSimCity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class DayNightPresentationController : MonoBehaviour
    {
        private CityRuntimeContext _context;
        private DayNightPresentationReadModel _model;
        private Light _sun;
        private Quaternion _originalRotation;
        private float _originalIntensity;
        private Color _originalColor;
        private Color _originalAmbient;
        private bool _captured;
        private int _observedVersion = -1;
        private bool _lastEnabled = true;

        public DayNightPresentationSnapshot Snapshot => _model?.Snapshot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Object.FindAnyObjectByType<DayNightPresentationController>() != null) return;
            var root = new GameObject("OpenSimCity DayNight Presentation");
            root.AddComponent<DayNightPresentationController>();
        }

        private void Update()
        {
            var context = CityRuntimeContext.Current;
            if (!ReferenceEquals(context, _context))
            {
                _context = context;
                _model = context == null ? null : new DayNightPresentationReadModel(context.Runtime);
                _observedVersion = -1;
            }
            if (_model == null) return;
            _model.RefreshIfNeeded();
            var enabled = PresentationSettingsContext.Current?.DayNightEnabled ?? true;
            if (_observedVersion == _model.Version && _lastEnabled == enabled) return;
            _lastEnabled = enabled;
            _observedVersion = _model.Version;
            EnsureSun();
            if (!_captured) CaptureOriginal();
            if (!enabled) ApplyDayDefaults();
            else ApplyPhase(_model.Snapshot.DayPhasePermille);
        }

        private void OnDisable()
        {
            if (_captured) RestoreOriginal();
        }

        private void EnsureSun()
        {
            if (_sun != null) return;
            _sun = RenderSettings.sun;
            if (_sun == null)
            {
                var candidate = Object.FindAnyObjectByType<Light>();
                if (candidate != null && candidate.type == LightType.Directional) _sun = candidate;
            }
        }

        private void CaptureOriginal()
        {
            _originalAmbient = RenderSettings.ambientLight;
            if (_sun != null)
            {
                _originalRotation = _sun.transform.rotation;
                _originalIntensity = _sun.intensity;
                _originalColor = _sun.color;
            }
            _captured = true;
        }

        private void ApplyPhase(int phasePermille)
        {
            var t = Mathf.Clamp01(phasePermille / 1000f);
            var daylight = Mathf.Clamp01(Mathf.Sin((t - 0.25f) * Mathf.PI * 2f) * 0.72f + 0.45f);
            if (_sun != null)
            {
                _sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(-12f, 190f, t), -35f, 0f);
                _sun.intensity = Mathf.Lerp(0.08f, Mathf.Max(0.8f, _originalIntensity), daylight);
                _sun.color = Color.Lerp(new Color(0.58f, 0.66f, 0.86f), new Color(1f, 0.94f, 0.82f), daylight);
            }
            RenderSettings.ambientLight = Color.Lerp(new Color(0.08f, 0.11f, 0.2f),
                _originalAmbient.maxColorComponent > 0f ? _originalAmbient : new Color(0.55f, 0.58f, 0.62f), daylight);
        }

        private void ApplyDayDefaults()
        {
            if (_sun != null)
            {
                _sun.transform.rotation = _originalRotation;
                _sun.intensity = _originalIntensity;
                _sun.color = _originalColor;
            }
            RenderSettings.ambientLight = _originalAmbient;
        }

        private void RestoreOriginal() => ApplyDayDefaults();
    }
}
