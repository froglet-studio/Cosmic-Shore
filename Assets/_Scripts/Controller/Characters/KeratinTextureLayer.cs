using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Keratin (beak, mandibles, antennae, fangs, whiskers): a root→tip gradient with a grain, and
    /// for a beak the gape line down each side. UVs: u around the section, v along the length.
    /// </summary>
    public static class KeratinTextureLayer
    {
        public static void Paint(TextureCanvas c, Color a, Color b, bool beak, float gapeHeight, int seed)
        {
            for (int y = 0; y < c.Height; y++)
            {
                float v = y / (float)(c.Height - 1);
                for (int x = 0; x < c.Width; x++)
                {
                    float u = x / (float)c.Width;
                    float grain = Noise.Fbm(u * 6f, v * 40f, 3, 2f, 0.5f, seed + 51);
                    Color col = Color.Lerp(a, b, GeometryKit.SafePow(v, 1.3f));
                    col *= 0.85f + 0.3f * grain;
                    if (beak)
                    {
                        // Gape: the sides at u = 0 and u = 0.5 (the section's ±x), from the root to near the tip.
                        float side = Mathf.Min(Mathf.Abs(PaintContext.WrapU(u - 0f)), Mathf.Abs(PaintContext.WrapU(u - 0.5f)));
                        float gape = GeometryKit.Bell(side / 0.02f) * (1f - GeometryKit.Smooth((v - 0.82f) / 0.12f)) * GeometryKit.Smooth(v / 0.08f);
                        col = Color.Lerp(col, col * 0.35f, gape);
                        // The culmen catches light: a slightly paler ridge along the top (u = 0.25).
                        float ridge = GeometryKit.Bell(Mathf.Abs(PaintContext.WrapU(u - 0.25f)) / 0.06f);
                        col = Color.Lerp(col, col * 1.25f + new Color(0.03f, 0.03f, 0.03f), ridge * 0.5f);
                    }
                    col.a = 1f;
                    c.Set(x, y, col);
                }
            }
        }
    }
}
