using UnityEngine;

namespace Tapzy.UI
{
    /// <summary>
    /// The single visual style every screen and every mini-game shares: "Bold Outline".
    /// Saturated flat colours, thick black outlines, hard shadows, chunky display lettering.
    /// </summary>
    public static class Theme
    {
        /// <summary>Hub background: bright blue.</summary>
        public static readonly Color Bg = Hex("1F5BFF");
        /// <summary>Background behind gameplay: deep navy, so every gameplay colour stands out.</summary>
        public static readonly Color GameBg = Hex("0B1030");
        public static readonly Color Panel = Hex("16214F");
        public static readonly Color PanelLight = Hex("2A3A7A");
        /// <summary>Main call-to-action colour (yellow).</summary>
        public static readonly Color Primary = Hex("FFD400");
        public static readonly Color Coin = Hex("FFD400");
        public static readonly Color Good = Hex("7CFF4D");
        public static readonly Color Bad = Hex("FF3D7F");
        public static readonly Color Text = Color.white;
        public static readonly Color TextDim = Hex("C9D6FF");
        public static readonly Color Dark = Hex("0B1030");
        public static readonly Color Outline = Color.black;

        /// <summary>Gameplay colours: pink, blue, green, yellow, purple, orange.</summary>
        public static readonly Color[] Palette =
        {
            Hex("FF3D7F"), Hex("3DE0FF"), Hex("7CFF4D"), Hex("FFD400"), Hex("B07CFF"), Hex("FF9A1F")
        };

        public static readonly string[] PaletteNames = { "PINK", "BLUE", "GREEN", "YELLOW", "PURPLE", "ORANGE" };

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;
        }

        public static Color Alpha(this Color c, float a)
        {
            c.a = a;
            return c;
        }

        public static Color Shade(this Color c, float mul)
        {
            return new Color(c.r * mul, c.g * mul, c.b * mul, c.a);
        }

        public static float Luminance(this Color c) => c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
    }
}
