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

            blush = Mathf.Clamp01(blush) * 0.06f;
            shadow = Mathf.Clamp01(shadow) * 0.11f;
            // Blush pushes red up, green/blue down; the socket shadow darkens and cools.
            skin.r += blush * 0.9f; skin.g -= blush * 0.35f; skin.b -= blush * 0.25f;
            skin.r -= shadow * 0.9f; skin.g -= shadow * 1.1f; skin.b -= shadow * 0.7f;

            // Forehead is a shade lighter and less saturated; the chin/jaw a touch darker.
            float fore = GeometryKit.Bell((px.ThetaDeg - 62f) / 22f) * 0.05f;
            float jaw = GeometryKit.Bell((px.ThetaDeg - 125f) / 20f) * 0.04f;
            skin.r += fore - jaw; skin.g += fore - jaw; skin.b += fore - jaw * 0.6f;

            // Mottling: low-frequency blotches of warmth, more with age.
            float mottle = (Noise.Fbm(px.Phi * 4f + 30f, px.ThetaDeg * 0.08f, 3, 2f, 0.55f, ctx.Seed) - 0.5f) * (0.05f + 0.06f * ctx.Blueprint.Genome.Age);
            skin.r += mottle * 1.2f; skin.g += mottle * 0.7f; skin.b += mottle * 0.5f;
            return skin;
        }
    }
}
