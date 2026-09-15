using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The human skin: tone × warmth, with the subsurface REGIONS a face has — warmer cheeks,
    /// nose and ears, cooler and slightly darker sockets, a lighter forehead — plus low-frequency
    /// mottling. Flat skin is what reads as plastic; the regions are what read as blood.
    /// This is the file for "the skin looks plastic" (colour) — pores are in SurfaceDetailLayer.
    /// </summary>
    public static class SkinBaseLayer
    {
        public static Color HumanBase(CharacterGenerationConfigSO c, CharacterGenome g)
        {
            Color tone = Color.Lerp(c.SkinPale, c.SkinDeep, GeometryKit.SafePow(Mathf.Clamp01(g.SkinTone), 1.1f));
            Color tint = Color.Lerp(c.SkinCoolTint, c.SkinWarmTint, Mathf.Clamp01(g.SkinWarmth));
            return new Color(tone.r * tint.r, tone.g * tint.g, tone.b * tint.b, 1f);
        }

        public static Color Evaluate(PaintContext ctx, in PaintContext.Pixel px)
        {
            Color skin = ctx.HumanSkin;
            var lm = ctx.Landmarks;
            float blush = 0f, shadow = 0f;
            if (lm.TryGet("Cheek.L", out var cl)) blush += GeometryKit.Bell(PaintContext.LandmarkDistance(cl, px.U, px.V, 3.2f, 2.8f) );
            if (lm.TryGet("Cheek.R", out var cr)) blush += GeometryKit.Bell(PaintContext.LandmarkDistance(cr, px.U, px.V, 3.2f, 2.8f));
            if (lm.TryGet("NoseTip", out var nt)) blush += 0.8f * GeometryKit.Bell(PaintContext.LandmarkDistance(nt, px.U, px.V, 2.2f, 2.0f));
            if (lm.TryGet("EarSide.L", out var el)) blush += 0.6f * GeometryKit.Bell(PaintContext.LandmarkDistance(el, px.U, px.V, 1.4f, 1.4f));
            if (lm.TryGet("EarSide.R", out var er)) blush += 0.6f * GeometryKit.Bell(PaintContext.LandmarkDistance(er, px.U, px.V, 1.4f, 1.4f));
            if (lm.TryGet("Eye.L", out var eyeL)) shadow += GeometryKit.Bell(PaintContext.LandmarkDistance(eyeL, px.U, px.V, 2.1f, 2.4f));
            if (lm.TryGet("Eye.R", out var eyeR)) shadow += GeometryKit.Bell(PaintContext.LandmarkDistance(eyeR, px.U, px.V, 2.1f, 2.4f));
            if (lm.TryGet("Mouth", out var mouth)) shadow += 0.5f * GeometryKit.Bell(PaintContext.LandmarkDistance(mouth, px.U, px.V, 2.4f, 2.0f));

            blush = Mathf.Clamp01(blush) * 0.09f;
            shadow = Mathf.Clamp01(shadow) * 0.07f;
            // Blush pushes red up, green/blue down; the socket shadow darkens and cools.
            skin.r += blush * 0.9f; skin.g -= blush * 0.35f; skin.b -= blush * 0.25f;
            skin.r -= shadow * 0.9f; skin.g -= shadow * 1.1f; skin.b -= shadow * 0.7f;

            // Forehead is a shade lighter and less saturated; the chin/jaw a touch darker.
            float fore = GeometryKit.Bell((px.ThetaDeg - 62f) / 22f) * 0.05f;
            float jaw = GeometryKit.Bell((px.ThetaDeg - 125f) / 20f) * 0.04f;
            skin.r += fore - jaw; skin.g += fore - jaw; skin.b += fore - jaw * 0.6f;

            // Mottling: low-frequency blotches of warmth, more with age; a finer capillary flush.
            float age = ctx.Blueprint.Genome.Age;
            float mottle = (Noise.Fbm(px.Phi * 4f + 30f, px.ThetaDeg * 0.08f, 3, 2f, 0.55f, ctx.Seed) - 0.5f) * (0.07f + 0.07f * age);
            skin.r += mottle * 1.2f; skin.g += mottle * 0.6f; skin.b += mottle * 0.4f;
            float flush = (Noise.Fbm(px.Phi * 11f + 3f, px.ThetaDeg * 0.22f, 2, 2f, 0.5f, ctx.Seed + 5) - 0.5f) * 0.05f;
            skin.r += flush; skin.g -= flush * 0.4f; skin.b -= flush * 0.5f;
            // Beard shadow on the jaw, chin and upper lip: a per-individual strength rolled off the seed.
            float beard = ctx.BeardShadow;
            if (beard > 0.01f)
            {
                float jawZone = GeometryKit.Smooth((px.ThetaDeg - 112f) / 12f) * (1f - GeometryKit.Smooth((px.ThetaDeg - 150f) / 10f));
                float lipZone = GeometryKit.Bell((px.ThetaDeg - 118f) / 5f) * GeometryKit.Bell(px.Phi / 0.35f);
                float stubble = 0.5f + 0.5f * Noise.Value(px.Phi * 260f, px.ThetaDeg * 1.6f, ctx.Seed + 9);
                float m = Mathf.Max(jawZone * (1f - GeometryKit.Smooth((Mathf.Abs(px.Phi) - 1.5f) / 0.5f)), lipZone) * beard * (0.6f + 0.4f * stubble);
                skin.r -= m * 0.12f; skin.g -= m * 0.13f; skin.b -= m * 0.10f;
            }
            return skin;
        }
    }
}
