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
        public PresentationSettingsDto Snapshot => Clone(_settings);
        public event Action<PresentationSettingsDto> Changed;

        public string GetControlPath(PresentationInputAction action)
        {
            var rows = _settings.BindingOverrides ?? Array.Empty<PresentationBindingOverrideDto>();
            for (var index = 0; index < rows.Length; index++)
                if (rows[index] != null && rows[index].Action == (int)action)
                    return rows[index].ControlPath;
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
            if (string.Equals(controlPath, PresentationInputBindingDefaults.GetDefaultPath(action), StringComparison.Ordinal))
                overrides.Remove(action);
            else
                overrides[action] = controlPath;
            _settings.BindingOverrides = ToSortedArray(overrides);
            Persist();
            return true;
        }

        public void ResetToDefaults()
        {
            _settings = new PresentationSettingsDto();
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
            {
                var row = rows[index];
                if (row != null && Enum.IsDefined(typeof(PresentationInputAction), row.Action))
                    map[(PresentationInputAction)row.Action] = row.ControlPath;
            }
            return map;
        }

        private static PresentationSettingsDto Normalize(PresentationSettingsDto source)
        {
            if (source == null || source.SchemaVersion != PresentationSettingsDto.CurrentSchemaVersion)
                return new PresentationSettingsDto();

            var result = new PresentationSettingsDto
            {
                UiScalePercent = source.UiScalePercent == 125 || source.UiScalePercent == 150 ? source.UiScalePercent : 100,
                VisualMode = Enum.IsDefined(typeof(AccessibilityVisualMode), source.VisualMode)
                    ? source.VisualMode
                    : (int)AccessibilityVisualMode.Standard,
                StrongFocusVisible = source.StrongFocusVisible
            };

            var map = new Dictionary<PresentationInputAction, string>();
            var used = new HashSet<string>(StringComparer.Ordinal);
            var rows = source.BindingOverrides ?? Array.Empty<PresentationBindingOverrideDto>();
            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                if (row == null || !Enum.IsDefined(typeof(PresentationInputAction), row.Action) ||
                    !PresentationInputBindingDefaults.IsAllowedKeyboardPath(row.ControlPath) || !used.Add(row.ControlPath))
                    continue;
                var action = (PresentationInputAction)row.Action;
                if (!string.Equals(row.ControlPath, PresentationInputBindingDefaults.GetDefaultPath(action), StringComparison.Ordinal))
                    map[action] = row.ControlPath;
            }
            result.BindingOverrides = ToSortedArray(map);
            return result;
        }

        private static PresentationBindingOverrideDto[] ToSortedArray(Dictionary<PresentationInputAction, string> map)
        {
            var actions = new List<PresentationInputAction>(map.Keys);
            actions.Sort();
            var rows = new PresentationBindingOverrideDto[actions.Count];
            for (var index = 0; index < actions.Count; index++)
                rows[index] = new PresentationBindingOverrideDto
                {
                    Action = (int)actions[index],
                    ControlPath = map[actions[index]]
                };
            return rows;
        }

        private static PresentationSettingsDto Clone(PresentationSettingsDto source)
        {
            var rows = source.BindingOverrides ?? Array.Empty<PresentationBindingOverrideDto>();
            var copy = new PresentationBindingOverrideDto[rows.Length];
            for (var index = 0; index < rows.Length; index++)
                copy[index] = rows[index] == null ? null : new PresentationBindingOverrideDto
                {
                    Action = rows[index].Action,
                    ControlPath = rows[index].ControlPath ?? string.Empty
                };
            return new PresentationSettingsDto
            {
                SchemaVersion = source.SchemaVersion,
                UiScalePercent = source.UiScalePercent,
                VisualMode = source.VisualMode,
                StrongFocusVisible = source.StrongFocusVisible,
                BindingOverrides = copy
            };
        }
    }

    public static class PresentationSettingsContext
    {
        public static PresentationSettingsController Current { get; private set; }
        public static void SetCurrent(PresentationSettingsController controller) => Current = controller;
        public static void ClearCurrent(PresentationSettingsController controller)
        {
            if (ReferenceEquals(Current, controller)) Current = null;
        }
        public static void Reset() => Current = null;
    }
}
