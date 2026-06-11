using System;
using System.Globalization;

namespace CosmicShore.Engine
{
    /// <summary>Color ↔ HTML hex string conversions matching the ported code's expected API.</summary>
    public static class ColorUtility
    {
        static int ToByte(float channel) => (int)Mathf.Clamp(Mathf.Round(channel * 255f), 0f, 255f);

        public static string ToHtmlStringRGB(Color color)
            => $"{ToByte(color.r):X2}{ToByte(color.g):X2}{ToByte(color.b):X2}";

        public static string ToHtmlStringRGBA(Color color)
            => $"{ToByte(color.r):X2}{ToByte(color.g):X2}{ToByte(color.b):X2}{ToByte(color.a):X2}";

        /// <summary>Parses "#RGB", "#RRGGBB", "#RRGGBBAA" (leading '#' optional).</summary>
        public static bool TryParseHtmlString(string htmlString, out Color color)
        {
            color = Color.black;
            if (string.IsNullOrEmpty(htmlString)) return false;

            string hex = htmlString[0] == '#' ? htmlString.Substring(1) : htmlString;

            static bool TryByte(string s, out float channel)
            {
                bool ok = byte.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b);
                channel = b / 255f;
                return ok;
            }

            switch (hex.Length)
            {
                case 3:
                {
                    if (TryByte(new string(hex[0], 2), out float r) &&
                        TryByte(new string(hex[1], 2), out float g) &&
                        TryByte(new string(hex[2], 2), out float b))
                    {
                        color = new Color(r, g, b, 1f);
                        return true;
                    }
                    return false;
                }
                case 6:
                {
                    if (TryByte(hex.Substring(0, 2), out float r) &&
                        TryByte(hex.Substring(2, 2), out float g) &&
                        TryByte(hex.Substring(4, 2), out float b))
                    {
                        color = new Color(r, g, b, 1f);
                        return true;
                    }
                    return false;
                }
                case 8:
                {
                    if (TryByte(hex.Substring(0, 2), out float r) &&
                        TryByte(hex.Substring(2, 2), out float g) &&
                        TryByte(hex.Substring(4, 2), out float b) &&
                        TryByte(hex.Substring(6, 2), out float a))
                    {
                        color = new Color(r, g, b, a);
                        return true;
                    }
                    return false;
                }
                default:
                    return false;
            }
        }
    }
}
