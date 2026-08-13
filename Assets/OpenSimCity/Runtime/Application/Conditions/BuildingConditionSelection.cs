using System;
using OpenSimCity.Core;

namespace OpenSimCity.Application.Conditions
{
    /// <summary>Transient selected-building state shared by Gameplay and UI.</summary>
    public sealed class BuildingConditionSelection
    {
        private static readonly BuildingConditionSelection Instance = new BuildingConditionSelection();
        public static BuildingConditionSelection Current => Instance;

        public StableId BuildingId { get; private set; }
        public bool HasSelection => BuildingId.IsValid;
        public int Version { get; private set; }
        public event Action Changed;

        public void Select(StableId buildingId)
        {
            if (!buildingId.IsValid)
            {
                Clear();
                return;
            }
            if (BuildingId == buildingId)
                return;
            BuildingId = buildingId;
            Version = checked(Version + 1);
            Changed?.Invoke();
        }

        public void Clear()
        {
            if (!BuildingId.IsValid)
                return;
            BuildingId = StableId.Invalid;
            Version = checked(Version + 1);
            Changed?.Invoke();
        }
    }
}
