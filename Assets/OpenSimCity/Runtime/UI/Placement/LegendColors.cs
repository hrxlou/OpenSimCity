using OpenSimCity.Application.Overlays;
using UnityEngine;

namespace OpenSimCity.UI.Placement
{
    /// <summary>
    /// Legend swatch colors for the overlay roles. It mirrors the world-overlay
    /// palette in the Presentation renderer so the legend and the map agree; the
    /// values are the overlay tokens from Docs/DESIGN.md 3.2, kept out of the
    /// Application layer.
    /// </summary>
    internal static class LegendColors
    {
        public static Color Resolve(OverlayColorRole role)
        {
            switch (role)
            {
                case OverlayColorRole.Info:
                    return Hex(0x66, 0xB3, 0xFF);
                case OverlayColorRole.Success:
                    return Hex(0x83, 0xCD, 0x52);
                case OverlayColorRole.Warning:
                    return Hex(0xF2, 0xC1, 0x4E);
                case OverlayColorRole.Danger:
                    return Hex(0xF0, 0x71, 0x71);
                case OverlayColorRole.Accent:
                    return Hex(0x31, 0xC7, 0xD5);
                case OverlayColorRole.Special:
                    return Hex(0xB0, 0x95, 0xF5);
                case OverlayColorRole.Residential:
                    return Hex(0x6F, 0xBF, 0x73);
                case OverlayColorRole.Commercial:
                    return Hex(0x5B, 0x8D, 0xEF);
                case OverlayColorRole.Industrial:
                    return Hex(0xE0, 0xA4, 0x58);
                default:
                    return Hex(0xAA, 0xB8, 0xC2);
            }
        }

        private static Color Hex(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}
