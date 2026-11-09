using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// A place's close-up for the Voyage window's cards (Rev 74 C3 "Place", picked by Shombith on 2026-11-08,
    /// Docs/MENU_REDESIGN_B.md sections 149-150): the HyperSea's water around a location, painted from the same density
    /// field and colour key as the window's map (<see cref="HyperSea.Density"/>, <see cref="VoyageMapGraphic.Sea"/>),
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

        /// <summary>The C3 grade: how much colour is kept, and how bright what is left is.</summary>
        public const float Saturation = 0.25f, Brightness = 0.55f;

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
            var tex = Paint(place.GalaxyX, place.GalaxyY);
            tex.name = "VoyagePlace_" + place.Id;
            s_Cache[key] = tex;
            return tex;
        }

        static Texture2D Paint(double cx, double cy)
        {
            var px = new Color32[Width * Height];
            double spanY = SpanLy * Height / (double)Width;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                // Texel rows run up the screen, and the board's y runs the other way (VoyageMapGraphic.Galaxy).
                double gx = cx + SpanLy * ((x + 0.5) / Width - 0.5);
                double gy = cy - spanY * ((y + 0.5) / Height - 0.5);
                px[y * Width + x] = Mute(VoyageMapGraphic.Sea(HyperSea.Density(gx, gy), 255), Saturation, Brightness);
            }
            Mark(px);
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
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
        static void Mark(Color32[] px)
        {
            var light = new Color32(230, 233, 255, 255);
            int cx = Width / 2, cy = Height / 2;
            const int r = 3;
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
                if (Mathf.Abs(dx) + Mathf.Abs(dy) <= r)
                    px[(cy + dy) * Width + cx + dx] = light;
        }

        /// <summary>
        /// <paramref name="c"/> with <paramref name="saturation"/> of its colour kept (0 grey, 1 as it was) and then scaled
        /// by <paramref name="brightness"/>; alpha unchanged. The CSS saturate() and brightness() the picked board drew
        /// with, in that order.
        /// </summary>
        public static Color32 Mute(Color32 c, float saturation, float brightness)
        {
            // Rec. 709 luma, as CSS saturate() weighs the channels.
            float l = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            byte Ch(byte v) => (byte)Mathf.Clamp(Mathf.RoundToInt((l + (v - l) * saturation) * brightness), 0, 255);
            return new Color32(Ch(c.r), Ch(c.g), Ch(c.b), c.a);
        }
    }
}
