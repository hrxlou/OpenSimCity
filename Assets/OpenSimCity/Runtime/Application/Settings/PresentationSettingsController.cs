using System;
using System.Collections.Generic;

namespace OpenSimCity.Application.Settings
{
    public sealed class PresentationSettingsController
    {
        private readonly IPresentationSettingsStore _store;
        private PresentationSettingsDto _settings;

        public PresentationSettingsController(IPresentationSettingsStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _settings = Normalize(_store.Load());
        }

        public UiScalePercent UiScale => (UiScalePercent)_settings.UiScalePercent;
        public AccessibilityVisualMode VisualMode => (AccessibilityVisualMode)_settings.VisualMode;
        public bool StrongFocusVisible => _settings.StrongFocusVisible;
        public int MasterVolumePermille => _settings.MasterVolumePermille;
        public int MusicVolumePermille => _settings.MusicVolumePermille;
        public int SfxVolumePermille => _settings.SfxVolumePermille;
        public int AmbientVolumePermille => _settings.AmbientVolumePermille;
        public bool AudioMuted => _settings.AudioMuted;
        public bool DayNightEnabled => _settings.DayNightEnabled;
        public PresentationSettingsDto Snapshot => Clone(_settings);
        public event Action<PresentationSettingsDto> Changed;

        public string GetControlPath(PresentationInputAction action)
        {
            var rows = _settings.BindingOverrides ?? Array.Empty<PresentationBindingOverrideDto>();
            for (var index = 0; index < rows.Length; index++)
                if (rows[index] != null && rows[index].Action == (int)action) return rows[index].ControlPath;
            return PresentationInputBindingDefaults.GetDefaultPath(action);
        }

        public void SetUiScale(UiScalePercent value)
        {
            var raw = (int)value;
            if (raw != 100 && raw != 125 && raw != 150) throw new ArgumentOutOfRangeException(nameof(value));
            if (_settings.UiScalePercent == raw) return;
            _settings.UiScalePercent = raw;
            Persist();
        }

        public void SetVisualMode(AccessibilityVisualMode value)
        {
            if (!Enum.IsDefined(typeof(AccessibilityVisualMode), value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (_settings.VisualMode == (int)value) return;
            _settings.VisualMode = (int)value;
            Persist();
        }

        public void SetStrongFocusVisible(bool value)
        {
            if (_settings.StrongFocusVisible == value) return;
            _settings.StrongFocusVisible = value;
            Persist();
        }

        public void SetMasterVolumePermille(int value) => SetVolume(ref _settings.MasterVolumePermille, value);
        public void SetMusicVolumePermille(int value) => SetVolume(ref _settings.MusicVolumePermille, value);
        public void SetSfxVolumePermille(int value) => SetVolume(ref _settings.SfxVolumePermille, value);
        public void SetAmbientVolumePermille(int value) => SetVolume(ref _settings.AmbientVolumePermille, value);

        public void SetAudioMuted(bool value)
        {
            if (_settings.AudioMuted == value) return;
            _settings.AudioMuted = value;
            Persist();
        }

        public void SetDayNightEnabled(bool value)
        {
            if (_settings.DayNightEnabled == value) return;
            _settings.DayNightEnabled = value;
            Persist();
        }

        public bool TryRemap(PresentationInputAction action, string controlPath, out string error)
        {
            error = string.Empty;
            if (!Enum.IsDefined(typeof(PresentationInputAction), action) ||
                !PresentationInputBindingDefaults.IsAllowedKeyboardPath(controlPath))
            {
                error = "Unsupported action or keyboard path.";
                return false;
            }
            var actions = PresentationInputBindingDefaults.RemappableActions;
            for (var index = 0; index < actions.Count; index++)
            {
                var other = actions[index];
                if (other != action && string.Equals(GetControlPath(other), controlPath, StringComparison.Ordinal))
                {
                    error = controlPath + " is already assigned to " + other + ".";
                    return false;
                }
            }
            var overrides = BuildOverrideMap();
            if (string.Equals(controlPath, PresentationInputBindingDefaults.GetDefaultPath(action), StringComparison.Ordinal)) overrides.Remove(action);
            else overrides[action] = controlPath;
            _settings.BindingOverrides = ToSortedArray(overrides);
            Persist();
            return true;
        }

        public void ResetToDefaults()
        {
            _settings = new PresentationSettingsDto();
            Persist();
        }

        private void SetVolume(ref int field, int value)
        {
            if (value < 0 || value > 1000) throw new ArgumentOutOfRangeException(nameof(value));
            if (field == value) return;
            field = value;
            Persist();
        }

        private void Persist()
        {
            var snapshot = Clone(_settings);
            _store.Save(snapshot);
            Changed?.Invoke(Clone(snapshot));
        }

        private Dictionary<PresentationInputAction, string> BuildOverrideMap()
        {
            var map = new Dictionary<PresentationInputAction, string>();
            var rows = _settings.BindingOverrides ?? Array.Empty<PresentationBindingOverrideDto>();
            for (var index = 0; index < rows.Length; index++)
                if (rows[index] != null && IsDefinedInputAction(rows[index].Action))
                    map[(PresentationInputAction)rows[index].Action] = rows[index].ControlPath;
            return map;
        }

        private static PresentationSettingsDto Normalize(PresentationSettingsDto source)
        {
            if (source == null || (source.SchemaVersion != 1 && source.SchemaVersion != PresentationSettingsDto.CurrentSchemaVersion))
                return new PresentationSettingsDto();

            var result = new PresentationSettingsDto
            {
                SchemaVersion = PresentationSettingsDto.CurrentSchemaVersion,
                UiScalePercent = source.UiScalePercent == 125 || source.UiScalePercent == 150 ? source.UiScalePercent : 100,
                VisualMode = IsDefinedVisualMode(source.VisualMode) ? source.VisualMode : (int)AccessibilityVisualMode.Standard,
                StrongFocusVisible = source.StrongFocusVisible,
                MasterVolumePermille = source.SchemaVersion == 1 ? 1000 : ClampVolume(source.MasterVolumePermille, 1000),
                MusicVolumePermille = source.SchemaVersion == 1 ? 700 : ClampVolume(source.MusicVolumePermille, 700),
                SfxVolumePermille = source.SchemaVersion == 1 ? 1000 : ClampVolume(source.SfxVolumePermille, 1000),
                AmbientVolumePermille = source.SchemaVersion == 1 ? 800 : ClampVolume(source.AmbientVolumePermille, 800),
                AudioMuted = source.SchemaVersion == 1 ? false : source.AudioMuted,
                DayNightEnabled = source.SchemaVersion == 1 || source.DayNightEnabled
            };

            var actions = PresentationInputBindingDefaults.RemappableActions;
            var resolved = new Dictionary<PresentationInputAction, string>();
            for (var index = 0; index < actions.Count; index++) resolved[actions[index]] = PresentationInputBindingDefaults.GetDefaultPath(actions[index]);
            var map = new Dictionary<PresentationInputAction, string>();
            var rows = source.BindingOverrides ?? Array.Empty<PresentationBindingOverrideDto>();
            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                if (row == null || !IsDefinedInputAction(row.Action) ||
                    !PresentationInputBindingDefaults.IsAllowedKeyboardPath(row.ControlPath)) continue;
                var action = (PresentationInputAction)row.Action;
                var conflict = false;
                for (var otherIndex = 0; otherIndex < actions.Count; otherIndex++)
                {
                    var other = actions[otherIndex];
                    if (other != action && string.Equals(resolved[other], row.ControlPath, StringComparison.Ordinal)) { conflict = true; break; }
                }
                if (conflict) continue;
                resolved[action] = row.ControlPath;
                if (!string.Equals(row.ControlPath, PresentationInputBindingDefaults.GetDefaultPath(action), StringComparison.Ordinal)) map[action] = row.ControlPath;
            }
            result.BindingOverrides = ToSortedArray(map);
            return result;
        }

        private static bool IsDefinedVisualMode(int value)
        {
            return value >= byte.MinValue && value <= byte.MaxValue &&
                   Enum.IsDefined(typeof(AccessibilityVisualMode), (AccessibilityVisualMode)(byte)value);
        }

        private static bool IsDefinedInputAction(int value)
        {
            return value >= byte.MinValue && value <= byte.MaxValue &&
                   Enum.IsDefined(typeof(PresentationInputAction), (PresentationInputAction)(byte)value);
        }

        private static int ClampVolume(int value, int fallback) => value < 0 || value > 1000 ? fallback : value;

        private static PresentationBindingOverrideDto[] ToSortedArray(Dictionary<PresentationInputAction, string> map)
        {
            var actions = new List<PresentationInputAction>(map.Keys);
            actions.Sort();
            var rows = new PresentationBindingOverrideDto[actions.Count];
            for (var index = 0; index < actions.Count; index++)
                rows[index] = new PresentationBindingOverrideDto { Action = (int)actions[index], ControlPath = map[actions[index]] };
            return rows;
        }

        private static PresentationSettingsDto Clone(PresentationSettingsDto source)
        {
            var rows = source.BindingOverrides ?? Array.Empty<PresentationBindingOverrideDto>();
            var copy = new PresentationBindingOverrideDto[rows.Length];
            for (var index = 0; index < rows.Length; index++) copy[index] = rows[index] == null ? null : new PresentationBindingOverrideDto { Action = rows[index].Action, ControlPath = rows[index].ControlPath ?? string.Empty };
            return new PresentationSettingsDto
            {
                SchemaVersion = source.SchemaVersion,
                UiScalePercent = source.UiScalePercent,
                VisualMode = source.VisualMode,
                StrongFocusVisible = source.StrongFocusVisible,
                BindingOverrides = copy,
                MasterVolumePermille = source.MasterVolumePermille,
                MusicVolumePermille = source.MusicVolumePermille,
                SfxVolumePermille = source.SfxVolumePermille,
                AmbientVolumePermille = source.AmbientVolumePermille,
                AudioMuted = source.AudioMuted,
                DayNightEnabled = source.DayNightEnabled
            };
        }
    }

    public static class PresentationSettingsContext
    {
        public static PresentationSettingsController Current { get; private set; }
        public static void SetCurrent(PresentationSettingsController controller) => Current = controller;
        public static void ClearCurrent(PresentationSettingsController controller) { if (ReferenceEquals(Current, controller)) Current = null; }
        public static void Reset() => Current = null;
    }
}
