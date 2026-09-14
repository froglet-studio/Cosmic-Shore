using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Hair (cap, crest feathers, whiskers when pale): a base colour with strand streaks running
    /// along v (the growth direction of every hair-slot part) and a slight sheen band.
    /// </summary>
    public static class HairTextureLayer
    {
        public static Color HairColor(CharacterGenerationConfigSO cfg, CharacterGenome g)
        {
            Color hair = Color.Lerp(cfg.HairDark, cfg.HairPale, GeometryKit.SafePow(Mathf.Clamp01(g.HairShade), 1.3f));
            Color red = Color.Lerp(Color.white, cfg.HairRedTint, Mathf.Clamp01(g.HairWarmth) * (0.35f + 0.65f * g.HairShade));
            return new Color(hair.r * red.r, hair.g * red.g, hair.b * red.b, 1f);
        }

        public static void Paint(TextureCanvas c, Color hair, Color sheen, int seed)
        {
            for (int y = 0; y < c.Height; y++)
            {
                float v = y / (float)(c.Height - 1);
                for (int x = 0; x < c.Width; x++)
                {
                    float u = x / (float)c.Width;
                    float strands = Noise.Periodic(u * 64f, v * 9f, 64, seed + 61);
                    float clumps = Noise.Periodic(u * 12f, v * 12f, 12, seed + 63);
                    float fine = Noise.Periodic(u * 220f, v * 30f, 220, seed + 62);
                    Color col = hair * (0.62f + 0.35f * strands + 0.25f * clumps + 0.12f * fine);
                    float band = GeometryKit.Bell((v - 0.55f) / 0.3f);
                    col = Color.Lerp(col, sheen, band * 0.08f);
                    col *= 0.75f + 0.25f * GeometryKit.Smooth(v / 0.35f);   // roots darker near the crown
                    col.a = 1f;
                    c.Set(x, y, col);
                }
            }
        }
    }
}
