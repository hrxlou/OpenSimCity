using System;
using System.Collections.Generic;

namespace OpenSimCity.Application.Settings
{
    public static class PresentationInputBindingDefaults
    {
        private static readonly PresentationInputAction[] Actions =
            (PresentationInputAction[])Enum.GetValues(typeof(PresentationInputAction));
        private static readonly string[] AllowedPaths = CreateAllowedPaths();

        public static IReadOnlyList<PresentationInputAction> RemappableActions => Array.AsReadOnly(Actions);
        public static IReadOnlyList<string> AllowedKeyboardPaths => Array.AsReadOnly(AllowedPaths);

        public static string GetDefaultPath(PresentationInputAction action)
        {
            switch (action)
            {
                case PresentationInputAction.MoveUp: return "<Keyboard>/w";
                case PresentationInputAction.MoveDown: return "<Keyboard>/s";
                case PresentationInputAction.MoveLeft: return "<Keyboard>/a";
                case PresentationInputAction.MoveRight: return "<Keyboard>/d";
                case PresentationInputAction.RotateLeft: return "<Keyboard>/q";
                case PresentationInputAction.RotateRight: return "<Keyboard>/e";
                case PresentationInputAction.Cancel: return "<Keyboard>/escape";
                case PresentationInputAction.ToolRoad: return "<Keyboard>/digit1";
                case PresentationInputAction.ToolResidential: return "<Keyboard>/digit2";
                case PresentationInputAction.ToolCommercial: return "<Keyboard>/digit3";
                case PresentationInputAction.ToolIndustrial: return "<Keyboard>/digit4";
                case PresentationInputAction.ToolZoneClear: return "<Keyboard>/digit5";
                case PresentationInputAction.ToolDemolish: return "<Keyboard>/digit6";
                case PresentationInputAction.ToolPowerPlant: return "<Keyboard>/digit7";
                case PresentationInputAction.ToolRemovePowerPlant: return "<Keyboard>/digit8";
                case PresentationInputAction.ToolRoadUpgrade: return "<Keyboard>/digit9";
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        public static bool IsAllowedKeyboardPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            for (var index = 0; index < AllowedPaths.Length; index++)
                if (string.Equals(AllowedPaths[index], path, StringComparison.Ordinal)) return true;
            return false;
        }

        private static string[] CreateAllowedPaths()
        {
            var paths = new List<string>();
            for (var c = 'a'; c <= 'z'; c++) paths.Add("<Keyboard>/" + c);
            for (var digit = 0; digit <= 9; digit++) paths.Add("<Keyboard>/digit" + digit);
            for (var f = 7; f <= 12; f++) paths.Add("<Keyboard>/f" + f);
            paths.Add("<Keyboard>/escape");
            paths.Add("<Keyboard>/tab");
            paths.Add("<Keyboard>/space");
            paths.Add("<Keyboard>/enter");
            paths.Add("<Keyboard>/leftArrow");
            paths.Add("<Keyboard>/rightArrow");
            paths.Add("<Keyboard>/upArrow");
            paths.Add("<Keyboard>/downArrow");
            paths.Add("<Keyboard>/home");
            paths.Add("<Keyboard>/end");
            paths.Add("<Keyboard>/pageUp");
            paths.Add("<Keyboard>/pageDown");
            return paths.ToArray();
        }
    }
}
