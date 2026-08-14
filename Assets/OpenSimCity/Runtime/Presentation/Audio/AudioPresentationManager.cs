using OpenSimCity.Application.Settings;
using UnityEngine;

namespace OpenSimCity.Presentation.Audio
{
    public enum AudioPresentationBus : byte { Music, Sfx, Ambient }

    [DisallowMultipleComponent]
    public sealed class AudioPresentationManager : MonoBehaviour
    {
        private AudioSource _music;
        private AudioSource _sfx;
        private AudioSource _ambient;
        private PresentationSettingsController _settings;
        private int _lastSettingsHash = int.MinValue;

        public AudioSource MusicSource => _music;
        public AudioSource SfxSource => _sfx;
        public AudioSource AmbientSource => _ambient;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Object.FindAnyObjectByType<AudioPresentationManager>() != null) return;
            var root = new GameObject("OpenSimCity Audio Presentation");
            root.AddComponent<AudioPresentationManager>();
        }

        private void Awake()
        {
            _music = CreateSource("Music", loop: true);
            _sfx = CreateSource("SFX", loop: false);
            _ambient = CreateSource("Ambient", loop: true);
        }

        private void Update()
        {
            var current = PresentationSettingsContext.Current;
            if (!ReferenceEquals(current, _settings))
            {
                _settings = current;
                _lastSettingsHash = int.MinValue;
            }
            ApplyVolumesIfNeeded();
        }

        public void SetLoopClip(AudioPresentationBus bus, AudioClip clip, bool play)
        {
            var source = Source(bus);
            source.clip = clip;
            if (!play || clip == null)
            {
                source.Stop();
                return;
            }
            if (!source.isPlaying) source.Play();
        }

        public void PlaySfx(AudioClip clip)
        {
            if (clip != null) _sfx.PlayOneShot(clip);
        }

        private AudioSource CreateSource(string objectName, bool loop)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

        private AudioSource Source(AudioPresentationBus bus)
        {
            switch (bus)
            {
                case AudioPresentationBus.Music: return _music;
                case AudioPresentationBus.Sfx: return _sfx;
                default: return _ambient;
            }
        }

        private void ApplyVolumesIfNeeded()
        {
            var settings = _settings;
            var master = settings?.MasterVolumePermille ?? 1000;
            var music = settings?.MusicVolumePermille ?? 700;
            var sfx = settings?.SfxVolumePermille ?? 1000;
            var ambient = settings?.AmbientVolumePermille ?? 800;
            var muted = settings?.AudioMuted ?? false;
            var hash = (((master * 31 + music) * 31 + sfx) * 31 + ambient) * 31 + (muted ? 1 : 0);
            if (hash == _lastSettingsHash) return;
            _lastSettingsHash = hash;
            var masterGain = muted ? 0f : master / 1000f;
            _music.volume = masterGain * music / 1000f;
            _sfx.volume = masterGain * sfx / 1000f;
            _ambient.volume = masterGain * ambient / 1000f;
        }
    }
}
