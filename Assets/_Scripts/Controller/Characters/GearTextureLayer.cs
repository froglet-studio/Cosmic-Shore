using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>The gear palette and its regions. Floats and colours only; retune freely.</summary>
    [Serializable]
    public struct GearStyle
    {
        public Color Jacket, JacketShade, Stitch, Leather, LeatherShade, Metal, Glass, GlassEdge;
        [Range(0f, 1f)] public float JacketSmoothness, LeatherSmoothness, GlassSmoothness;
        [Tooltip("How much of the collar band is the domain accent (the rest is jacket).")] [Range(0f, 1f)] public float CollarAccent;

        public static GearStyle Default => new GearStyle
        {
            Jacket = new Color(0.36f, 0.30f, 0.22f),
            JacketShade = new Color(0.22f, 0.17f, 0.12f),
            Stitch = new Color(0.62f, 0.52f, 0.36f),
            Leather = new Color(0.30f, 0.20f, 0.13f),
            LeatherShade = new Color(0.16f, 0.10f, 0.06f),
            Metal = new Color(0.70f, 0.66f, 0.58f),
            Glass = new Color(0.45f, 0.90f, 0.95f),
            GlassEdge = new Color(0.08f, 0.42f, 0.52f),
            JacketSmoothness = 0.30f,
            LeatherSmoothness = 0.55f,
            GlassSmoothness = 0.95f,
            CollarAccent = 0.85f,
        };
    }

    /// <summary>
    /// The gear atlas: u ∈ [0, 0.5) jacket fabric (v ≥ 0.92 the collar band, in the domain
    /// accent), u ∈ [0.5, 0.75) leather strap / goggle rim with metal rivets, u ∈ [0.75, 1]
    /// goggle glass. Alpha carries smoothness. This is the file for "the jacket / the goggle
    /// lens looks wrong".
    /// </summary>
    public static class GearTextureLayer
    {
        public static void Paint(TextureCanvas c, in GearStyle g, in CharacterPaletteBinding.DomainAccent accent, int seed)
        {
                        float ay = 0.2126f * accent.Accent.r + 0.7152f * accent.Accent.g + 0.0722f * accent.Accent.b;
            Color soft = Color.Lerp(new Color(ay, ay, ay), accent.Accent, 0.6f) * 0.7f;
            Color collar = Color.Lerp(g.Jacket, soft, g.CollarAccent);
            for (int y = 0; y < c.Height; y++)
            {
                float v = y / (float)(c.Height - 1);
                for (int x = 0; x < c.Width; x++)
                {
                    float u = x / (float)c.Width;
                    Color col; float smooth;
                    if (u < 0.5f)
                    {
                        // Fabric: a woven weave with a slow tone gradient, seams at a few v lines.
                        float weave = Noise.Periodic(u * 2f * 180f, v * 180f, 180, seed + 71);
                        float grain = Noise.Fbm(u * 24f, v * 12f, 2, 2f, 0.5f, seed + 72);
                        col = Color.Lerp(g.JacketShade, g.Jacket, 0.55f + 0.45f * grain) * (0.9f + 0.2f * weave);
                        float seam = GeometryKit.Bell((v - 0.62f) / 0.012f) + GeometryKit.Bell((v - 0.30f) / 0.012f);
                        col = Color.Lerp(col, g.Stitch, Mathf.Clamp01(seam) * 0.6f);
                        float band = GeometryKit.SmoothStep(0.915f, 0.93f, v);
                        col = Color.Lerp(col, collar * (0.85f + 0.3f * grain), band);
                        smooth = g.JacketSmoothness + 0.1f * band;
                    }
                    else if (u < 0.75f)
                    {
                        float pores = Noise.Fbm(u * 60f, v * 60f, 3, 2.1f, 0.5f, seed + 73);
                        col = Color.Lerp(g.LeatherShade, g.Leather, 0.4f + 0.6f * pores);
                        // Rivets along the strap's centre.
                        float rivet = GeometryKit.Bell(((v * 12f) % 1f - 0.5f) / 0.16f) * GeometryKit.Bell((u - 0.625f) / 0.03f);
                        col = Color.Lerp(col, g.Metal, GeometryKit.SmoothStep(0.5f, 0.8f, rivet));
                        smooth = g.LeatherSmoothness + 0.35f * GeometryKit.SmoothStep(0.5f, 0.8f, rivet);
                    }
                    else
                    {
                        // Glass: the lens UVs are a disc centred at (0.88, 0.5); darker at the edge,
                        // a diagonal reflection streak across the middle.
                        float dx = (u - 0.88f) / 0.11f, dy = (v - 0.5f) / 0.45f;
                        float rr = Mathf.Sqrt(dx * dx + dy * dy);
                        float edge = GeometryKit.SmoothStep(0.55f, 1f, rr);
                        float streak = GeometryKit.Bell((dx - dy * 0.6f + 0.25f) / 0.28f);
                        col = Color.Lerp(g.Glass, g.GlassEdge, edge);
                        col = Color.Lerp(col, Color.white, streak * 0.6f * (1f - edge));
                        smooth = g.GlassSmoothness;
                    }
                    col.a = Mathf.Clamp01(smooth);
                    c.Set(x, y, col);
                }
            }
        }
    }
}
