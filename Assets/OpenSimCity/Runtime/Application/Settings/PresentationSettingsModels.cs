using System;

namespace OpenSimCity.Application.Settings
{
    public enum UiScalePercent : ushort { Percent100 = 100, Percent125 = 125, Percent150 = 150 }
    public enum AccessibilityVisualMode : byte { Standard, ColorIndependent, HighContrast }

    public enum PresentationInputAction : byte
    {
        MoveUp, MoveDown, MoveLeft, MoveRight, RotateLeft, RotateRight, Cancel,
        ToolRoad, ToolResidential, ToolCommercial, ToolIndustrial, ToolZoneClear,
        ToolDemolish, ToolPowerPlant, ToolRemovePowerPlant, ToolRoadUpgrade
    }

    [Serializable]
    public sealed class PresentationBindingOverrideDto
    {
        public int Action;
        public string ControlPath = string.Empty;
    }

    [Serializable]
    public sealed class PresentationSettingsDto
    {
        public const int CurrentSchemaVersion = 1;
        public int SchemaVersion = CurrentSchemaVersion;
        public int UiScalePercent = 100;
        public int VisualMode = (int)AccessibilityVisualMode.Standard;
        public bool StrongFocusVisible = true;
        public PresentationBindingOverrideDto[] BindingOverrides = Array.Empty<PresentationBindingOverrideDto>();
    }

    public interface IPresentationSettingsStore
    {
        PresentationSettingsDto Load();
        void Save(PresentationSettingsDto settings);
    }
}
