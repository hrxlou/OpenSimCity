using System;

namespace OpenSimCity.Application.Conditions
{
    /// <summary>
    /// Transient visibility state for the building-condition datamap. It is neither
    /// persisted nor used by the condition simulation.
    /// </summary>
    public sealed class BuildingConditionDatamapSelection
    {
        private static readonly BuildingConditionDatamapSelection Instance =
            new BuildingConditionDatamapSelection();

        public static BuildingConditionDatamapSelection Current => Instance;
        public bool IsVisible { get; private set; }
        public int Version { get; private set; }
        public event Action Changed;

        public void SetVisible(bool value)
        {
            if (IsVisible == value)
                return;
            IsVisible = value;
            Version = checked(Version + 1);
            Changed?.Invoke();
        }

        public void Toggle() => SetVisible(!IsVisible);
    }
}
