using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// A place's close-up for the Voyage window's cards (Rev 74 C3 "Place", picked by Shombith on 2026-11-08,
    /// Docs/MENU_REDESIGN_B.md sections 149-150): the HyperSea's water around a location, painted from the same density
    /// field and colour key as the window's map (<see cref="HyperSea.Density"/>, <see cref="HyperSea.SeaColor"/>),
    /// then muted (<see cref="Mute"/>) so the card reads as a quiet picture of the place, not a second, bluer map. The
    /// place itself is a small light mark at the centre. Painted once per location and kept; never a second live
    /// <see cref="VoyageMapGraphic"/> (the map's atlas is shared between its instances).
    /// </summary>
    public static class VoyagePlaceArt
    {
        /// <summary>The close-up's size, texels. Small on purpose: the muted sea is soft and the card scales it up
        /// bilinearly, and each texel is one <see cref="HyperSea.Density"/> sample (measured 2026-11-09: about 80 ms a place
        /// at 256 x 160 on a desktop CPU under .NET, so a window of five voyages stalled for a second on first open; 128 x
        /// 80 is a quarter of that).</summary>
        public const int Width = 128, Height = 80;

        /// <summary>The water a close-up shows, ly across.</summary>
        public const float SpanLy = 260f;

        /// <summary>The whole galaxy's picture (the Episodes tab): ly across, its size in texels (5:3, the board's frame)
        /// and its darker grey range, so the light words over it read. Measured offline 2026-11-09: 200 x 120 took about
        /// 200 ms (the core is the costly part of the density field), so it is 160 x 96, once a session.</summary>
        public const float GalaxySpanLy = 110000f;
        public const int GalaxyWidth = 160, GalaxyHeight = 96;
        public const byte GalaxyFloor = 8, GalaxyCeiling = 96;

        /// <summary>The C3 grade: how much colour is kept, and how bright what is left is.</summary>
        public const float Saturation = 0.25f, Brightness = 0.55f;

        /// <summary>The grey range the muted water is spread across (0-255): the open sea around most places is so thin
        /// that the grade alone left the card all but black, with only the place's mark showing (seen 2026-11-09).</summary>
        public const byte Floor = 34, Ceiling = 150;

        static readonly Dictionary<string, Texture2D> s_Cache = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // The textures are HideAndDontSave: with domain reload off they would outlive the cache that held them.
            foreach (var t in s_Cache.Values)
                if (t) Object.DestroyImmediate(t);
            s_Cache.Clear();
        }

        /// <summary>The close-up of <paramref name="place"/>, painted on first ask; null for no place.</summary>
        public static Texture2D For(VoyageLocation place)
        {
            if (place == null || string.IsNullOrEmpty(place.Id)) return null;
            string key = $"{place.Id}:{place.GalaxyX:F0},{place.GalaxyY:F0}";
            if (s_Cache.TryGetValue(key, out var cached) && cached) return cached;
            var tex = Paint(place.GalaxyX, place.GalaxyY, SpanLy, Width, Height, Saturation, Brightness, Floor, Ceiling, mark: true);
            tex.name = "VoyagePlace_" + place.Id;
            s_Cache[key] = tex;
            return tex;
        }

        /// <summary>The whole galaxy, wide (<see cref="GalaxySpanLy"/> across), graded as the Episodes board drew it
        /// (saturate .2, brightness .5) and spread like a place's close-up; no mark. Painted once and kept.</summary>
        public static Texture2D Galaxy()
        {
            const string key = "galaxy";
            if (s_Cache.TryGetValue(key, out var cached) && cached) return cached;
            var tex = Paint(0, 0, GalaxySpanLy, GalaxyWidth, GalaxyHeight, 0.2f, 0.5f, GalaxyFloor, GalaxyCeiling, mark: false);
            tex.name = "VoyageGalaxy";
            s_Cache[key] = tex;
            return tex;
        }

        static Texture2D Paint(double cx, double cy, float spanLy, int width, int height, float saturation, float brightness,
            byte floor, byte ceiling, bool mark)
        {
            // Graded and stretched in float, rounded once at the end: the open sea's colours sit within a few levels of
            // each other, and stretching them after rounding drew hard bands (seen in a render of Tollmoor, 2026-11-09).
            var rgb = new float[width * height * 3];
            double spanY = spanLy * height / (double)width;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                // Texel rows run up the screen, and the board's y runs the other way (VoyageMapGraphic.Galaxy).
                double gx = cx + spanLy * ((x + 0.5) / width - 0.5);
                double gy = cy - spanY * ((y + 0.5) / height - 0.5);
                var (r, g, b) = HyperSea.SeaColor(HyperSea.Density(gx, gy));
                int i = (y * width + x) * 3;
                MuteInto(rgb, i, (float)r, (float)g, (float)b, saturation, brightness);
            }
            Spread(rgb, floor, ceiling);
            var px = new Color32[width * height];
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color32(Byte(rgb[i * 3]), Byte(rgb[i * 3 + 1]), Byte(rgb[i * 3 + 2]), 255);
            if (mark) Mark(px, width, height);
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>The place: a small diamond in the style guide's light text colour, at the centre.</summary>
        static void Mark(Color32[] px, int width, int height)
        {
            var light = new Color32(230, 233, 255, 255);
            int cx = width / 2, cy = height / 2;
            const int r = 3;
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
                if (Mathf.Abs(dx) + Mathf.Abs(dy) <= r)
                    px[(cy + dy) * width + cx + dx] = light;
        }

        /// <summary>
        /// Stretch <paramref name="px"/>'s luma so its darkest texel sits at <paramref name="floor"/> and its brightest at
        /// <paramref name="ceiling"/>, keeping each texel's hue; a flat picture (no range to stretch) is lifted to the middle
        /// of the range instead. Alpha unchanged.
        /// </summary>
        public static void Spread(Color32[] px, byte floor, byte ceiling)
        {
            if (px == null || px.Length == 0) return;
            var rgb = new float[px.Length * 3];
            for (int i = 0; i < px.Length; i++) { rgb[i * 3] = px[i].r; rgb[i * 3 + 1] = px[i].g; rgb[i * 3 + 2] = px[i].b; }
            Spread(rgb, floor, ceiling);
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color32(Byte(rgb[i * 3]), Byte(rgb[i * 3 + 1]), Byte(rgb[i * 3 + 2]), px[i].a);
        }

        /// <summary><see cref="Spread(Color32[], byte, byte)"/> on interleaved float rgb (0-255), the painter's own form.</summary>
        static void Spread(float[] rgb, float floor, float ceiling)
        {
            int n = rgb.Length / 3;
            if (n == 0 || ceiling <= floor) return;
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                float l = Luma(rgb[i * 3], rgb[i * 3 + 1], rgb[i * 3 + 2]);
                if (l < lo) lo = l;
                if (l > hi) hi = l;
            }
            float span = hi - lo;
            for (int i = 0; i < n; i++)
            {
                float l = Luma(rgb[i * 3], rgb[i * 3 + 1], rgb[i * 3 + 2]);
                float target = span < 1f ? (floor + ceiling) * 0.5f : floor + (l - lo) / span * (ceiling - floor);
                float add = target - l;
                rgb[i * 3] += add;
                rgb[i * 3 + 1] += add;
                rgb[i * 3 + 2] += add;
            }
        }

        static byte Byte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v), 0, 255);

        static float Luma(float r, float g, float b) => 0.2126f * r + 0.7152f * g + 0.0722f * b;

        /// <summary>
        /// <paramref name="c"/> with <paramref name="saturation"/> of its colour kept (0 grey, 1 as it was) and then scaled
        /// by <paramref name="brightness"/>; alpha unchanged. The CSS saturate() and brightness() the picked board drew
        /// with, in that order.
        /// </summary>
        public static Color32 Mute(Color32 c, float saturation, float brightness)
        {
            var o = new float[3];
            MuteInto(o, 0, c.r, c.g, c.b, saturation, brightness);
            return new Color32(Byte(o[0]), Byte(o[1]), Byte(o[2]), c.a);
        }

        static void MuteInto(float[] o, int i, float r, float g, float b, float saturation, float brightness)
        {
            // Rec. 709 luma, as CSS saturate() weighs the channels.
            float l = Luma(r, g, b);
            o[i] = (l + (r - l) * saturation) * brightness;
            o[i + 1] = (l + (g - l) * saturation) * brightness;
            o[i + 2] = (l + (b - l) * saturation) * brightness;
        }
    }
}
