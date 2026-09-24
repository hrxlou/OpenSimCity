using OpenSimCity.Application.Queries;
using OpenSimCity.Application.Settings;
using OpenSimCity.Application.World;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        private Color _originalSky;
        private Color _originalEquator;
        private Color _originalGround;
        private float _originalReflectionIntensity;
        private bool _captured;
        private int _observedVersion = -1;
        private bool _lastEnabled = true;

        public DayNightPresentationSnapshot Snapshot => _model?.Snapshot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Install();

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
            _originalSky = RenderSettings.ambientSkyColor;
            _originalEquator = RenderSettings.ambientEquatorColor;
            _originalGround = RenderSettings.ambientGroundColor;
            _originalReflectionIntensity = RenderSettings.reflectionIntensity;
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
                // Keep a readable moon key at night and a grazing, dimensional sun by day.
                var elevation = Mathf.Max(18f, Mathf.Sin((t - 0.25f) * Mathf.PI * 2f) * 42f);
                _sun.transform.rotation = Quaternion.Euler(elevation, Mathf.Lerp(-125f, 55f, t), 0f);
                _sun.intensity = Mathf.Lerp(0.18f, Mathf.Max(0.8f, _originalIntensity), daylight);
                _sun.color = Color.Lerp(new Color(0.58f, 0.66f, 0.86f), new Color(1f, 0.94f, 0.82f), daylight);
            }
            // City scenes use Trilight; ambientLight alone does not change that sky fill.
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.10f, 0.15f, 0.25f), _originalSky, daylight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.055f, 0.075f, 0.12f), _originalEquator, daylight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.025f, 0.035f, 0.055f), _originalGround, daylight);
            RenderSettings.reflectionIntensity = _originalReflectionIntensity * Mathf.Lerp(0.12f, 1f, daylight);
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
            RenderSettings.ambientSkyColor = _originalSky;
            RenderSettings.ambientEquatorColor = _originalEquator;
            RenderSettings.ambientGroundColor = _originalGround;
            RenderSettings.reflectionIntensity = _originalReflectionIntensity;
        }

        private void RestoreOriginal() => ApplyDayDefaults();
    }
}
