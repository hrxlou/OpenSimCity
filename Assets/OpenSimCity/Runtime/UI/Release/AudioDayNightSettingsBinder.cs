using OpenSimCity.Application.Queries;
using OpenSimCity.Application.Settings;
using OpenSimCity.Application.World;
using OpenSimCity.UI.Foundation;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Release
{
    [DisallowMultipleComponent]
    public sealed class AudioDayNightSettingsBinder : MonoBehaviour
    {
        private UiFoundationBootstrap _bootstrap;
        private PresentationSettingsController _settings;
        private VisualElement _settingsPanel;
        private VisualElement _section;
        private SliderInt _master;
        private SliderInt _music;
        private SliderInt _sfx;
        private SliderInt _ambient;
        private Toggle _mute;
        private Toggle _dayNight;
        private Label _cycleStatus;
        private CityRuntimeContext _context;
        private DayNightPresentationReadModel _cycle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var bootstrap = Object.FindAnyObjectByType<UiFoundationBootstrap>();
            if (bootstrap != null && bootstrap.GetComponent<AudioDayNightSettingsBinder>() == null)
                bootstrap.gameObject.AddComponent<AudioDayNightSettingsBinder>();
        }

        private void OnEnable() => _bootstrap = GetComponent<UiFoundationBootstrap>();

        private void OnDisable()
        {
            BindSettings(null);
            _section?.RemoveFromHierarchy();
            if (_bootstrap?.Root != null) _bootstrap.Root.EnableInClassList("osc-day-night--night", false);
        }

        private void Update()
        {
            if (_bootstrap == null) _bootstrap = GetComponent<UiFoundationBootstrap>();
            if (_bootstrap?.Root == null) return;
            if (!ReferenceEquals(_settings, PresentationSettingsContext.Current))
                BindSettings(PresentationSettingsContext.Current);
            var panel = _bootstrap.Root.Q<VisualElement>("osc-release-settings-section");
            if (!ReferenceEquals(panel, _settingsPanel))
            {
                _section?.RemoveFromHierarchy();
                _settingsPanel = panel;
                if (panel != null) BuildSection();
            }
            var context = CityRuntimeContext.Current;
            if (!ReferenceEquals(context, _context))
            {
                _context = context;
                _cycle = context == null ? null : new DayNightPresentationReadModel(context.Runtime);
            }
            if (_cycle != null)
            {
                _cycle.RefreshIfNeeded();
                var snapshot = _cycle.Snapshot;
                var enabled = _settings?.DayNightEnabled ?? true;
                _bootstrap.Root.EnableInClassList("osc-day-night--night", enabled && snapshot.IsNight);
                if (_cycleStatus != null)
                    _cycleStatus.text = enabled
                        ? $"표현 시간: {snapshot.Month.Year}.{snapshot.Month.Month:00} / {snapshot.Day}일 · {(snapshot.IsNight ? "밤" : "낮")}"
                        : "낮/밤 표현: 고정 낮";
            }
        }

        private void BindSettings(PresentationSettingsController settings)
        {
            if (_settings != null) _settings.Changed -= HandleChanged;
            _settings = settings;
            if (_settings != null) _settings.Changed += HandleChanged;
            RefreshControls();
        }

        private void HandleChanged(PresentationSettingsDto _) => RefreshControls();

        private void BuildSection()
        {
            _section = new VisualElement { name = "osc-audio-day-night-settings" };
            var title = new Label("오디오 · 낮/밤");
            title.AddToClassList(UiFoundationNames.PanelTitleClass);
            _section.Add(title);
            _master = Slider("마스터", value => _settings?.SetMasterVolumePermille(value * 10));
            _music = Slider("음악", value => _settings?.SetMusicVolumePermille(value * 10));
            _sfx = Slider("효과음", value => _settings?.SetSfxVolumePermille(value * 10));
            _ambient = Slider("환경음", value => _settings?.SetAmbientVolumePermille(value * 10));
            _section.Add(_master);
            _section.Add(_music);
            _section.Add(_sfx);
            _section.Add(_ambient);
            _mute = new Toggle("전체 음소거");
            _mute.RegisterValueChangedCallback(evt => _settings?.SetAudioMuted(evt.newValue));
            _section.Add(_mute);
            _dayNight = new Toggle("시뮬레이션 timeline 낮/밤 표현");
            _dayNight.RegisterValueChangedCallback(evt => _settings?.SetDayNightEnabled(evt.newValue));
            _section.Add(_dayNight);
            _cycleStatus = new Label();
            _cycleStatus.AddToClassList(UiFoundationNames.PanelCaptionClass);
            _section.Add(_cycleStatus);
            var note = new Label("낮/밤과 오디오는 표현 설정이며 도시 규칙을 변경하지 않습니다.");
            note.AddToClassList(UiFoundationNames.PanelCaptionClass);
            _section.Add(note);
            _settingsPanel.Add(_section);
            RefreshControls();
        }

        private SliderInt Slider(string label, System.Action<int> changed)
        {
            var slider = new SliderInt(label, 0, 100) { showInputField = true };
            slider.RegisterValueChangedCallback(evt => changed(evt.newValue));
            return slider;
        }

        private void RefreshControls()
        {
            if (_settings == null || _section == null) return;
            _master.SetValueWithoutNotify(_settings.MasterVolumePermille / 10);
            _music.SetValueWithoutNotify(_settings.MusicVolumePermille / 10);
            _sfx.SetValueWithoutNotify(_settings.SfxVolumePermille / 10);
            _ambient.SetValueWithoutNotify(_settings.AmbientVolumePermille / 10);
            _mute.SetValueWithoutNotify(_settings.AudioMuted);
            _dayNight.SetValueWithoutNotify(_settings.DayNightEnabled);
        }
    }
}
