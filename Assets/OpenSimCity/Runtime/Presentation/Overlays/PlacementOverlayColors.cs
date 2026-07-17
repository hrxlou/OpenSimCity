using OpenSimCity.Application.Overlays;
using UnityEngine;

namespace OpenSimCity.Presentation.Overlays
{
    /// <summary>
    /// Resolves an Application <see cref="OverlayColorRole"/> to a world-overlay
    /// color. Overlay colors are a presentation setting kept separate from the UI
    /// status tokens and the world material palette (Docs/DESIGN.md 3.2), so the
    /// Application layer never names a hex value.
    /// </summary>
    public static class PlacementOverlayColors
    {
        public static Color Resolve(OverlayColorRole role, float alpha = 0.55f)
        {
            var color = BaseColor(role);
            color.a = alpha;
            return color;
        }

        private static Color BaseColor(OverlayColorRole role)
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
