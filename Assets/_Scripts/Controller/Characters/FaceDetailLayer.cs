using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The features that are PAINTED rather than modelled, each keyed on a landmark so it lands
    /// on whatever shape the head took: eyebrows, the lash line and socket shading, the mouth
    /// crease and lip colour, nostrils, the blowhole aperture, the domain adornment.
    /// This is the file for "the eyes look dead" (when the cause is the lash line / socket),
    /// "the mouth corners are wrong", "the eyebrows are wrong".
    /// </summary>
    public static class FaceDetailLayer
    {
        public static void Apply(PaintContext ctx, in PaintContext.Pixel px, float humanMask, bool browsAllowed, ref Color color, ref float height)
        {
            var bp = ctx.Blueprint;
            var lm = ctx.Landmarks;

            // ---- eyes -----------------------------------------------------------------
            if (!bp.HasCompoundEyes)
            {
                EyeRegion(ctx, px, "Eye.L", ref color, ref height);
                EyeRegion(ctx, px, "Eye.R", ref color, ref height);
            }
            else
            {
                DarkRing(lm, px, "Eye.L", 1.35f, 1.75f, ref color);
                DarkRing(lm, px, "Eye.R", 1.35f, 1.75f, ref color);
            }

            // ---- eyebrows ---------------------------------------------------------------
            if (browsAllowed && !bp.HasCompoundEyes)
            {
                Brow(ctx, px, "Brow.L", humanMask, ref color, ref height);
                Brow(ctx, px, "Brow.R", humanMask, ref color, ref height);
            }

            // ---- mouth ------------------------------------------------------------------
            if (bp.HasHumanMouth && lm.TryGet("Mouth", out var mouth))
            {
                float mouthW = GeometryKit.Dial(bp.Shape.Clamped(HeadAxis.MouthWidth), 0.75f, 1.5f);
                float du = PaintContext.WrapU(px.U - mouth.Uv.x) / Mathf.Max(1e-4f, mouth.UvRadius.x * mouthW);
                float dv = (px.V - mouth.Uv.y) / Mathf.Max(1e-4f, mouth.UvRadius.y);
                float lips = GeometryKit.Dial(bp.Shape.Clamped(HeadAxis.LipFullness), 0.3f, 1.4f);
                if (Mathf.Abs(du) < 1.25f)
                {
                    // The crease: a shallow curve whose corners lift a touch — a resting face that
                    // is not sad. Corner darkening ends the line rather than letting it fade.
                    float line = 0.04f + 0.09f * du * du - 0.03f * GeometryKit.SafePow(Mathf.Abs(du), 4f);
                    float thickness = 0.055f * (1f - 0.55f * Mathf.Abs(du));
                    float d = Mathf.Abs(dv - line) / thickness;
                    float crease = GeometryKit.Bell(d) * (1f - GeometryKit.Smooth((Mathf.Abs(du) - 1.0f) / 0.25f));
                    Color creaseColor = new Color(0.25f, 0.09f, 0.08f);
                    color = Color.Lerp(color, creaseColor, crease * 0.88f);
                    height -= crease * 0.35f;
                    // Lips: warmth above and below the crease, stronger toward the centre.
                    float upper = GeometryKit.Bell((dv - line - 0.20f * lips) / (0.24f * lips));
                    float lower = GeometryKit.Bell((dv - line + 0.24f * lips) / (0.28f * lips));
                    float lipMask = Mathf.Max(upper, lower) * (1f - GeometryKit.Smooth((Mathf.Abs(du) - 0.85f) / 0.3f));
                    Color lipTint = Color.Lerp(color, ctx.Config.LipTint, 0.55f);
                    color = Color.Lerp(color, lipTint, lipMask * 0.75f * humanMask);
                    // Philtrum shadow.
                    float philtrum = GeometryKit.Bell(du / 0.16f) * GeometryKit.Bell((dv - line - 0.55f) / 0.35f);
                    color *= 1f - philtrum * 0.06f;
                }
            }

            // ---- nostrils -------------------------------------------------------------------
            if (bp.HasHumanNose && lm.TryGet("NoseTip", out var nose))
            {
                float noseW = GeometryKit.Dial(bp.Shape.Clamped(HeadAxis.NoseWidth), 0.7f, 1.6f);
                for (int side = -1; side <= 1; side += 2)
                {
                    float du = (PaintContext.WrapU(px.U - nose.Uv.x) - side * nose.UvRadius.x * 0.95f * noseW) / (nose.UvRadius.x * 0.42f);
                    float dv = (px.V - (nose.Uv.y - nose.UvRadius.y * 0.55f)) / (nose.UvRadius.y * 0.34f);
                    float m = GeometryKit.Bell(GeometryKit.SafeSqrt(du * du + dv * dv));
                    m = GeometryKit.SmoothStep(0.15f, 0.7f, m);
                    color = Color.Lerp(color, new Color(0.12f, 0.06f, 0.05f), m * 0.85f);
                    height -= m * 0.5f;
                }
            }

            // ---- blowhole aperture ------------------------------------------------------
            if (bp.HasBlowhole && lm.TryGet("CrownBack", out var bh))
            {
                float d = PaintContext.LandmarkDistance(bh, px.U, px.V, 0.55f, 0.35f);
                float m = 1f - GeometryKit.Smooth((d - 0.8f) / 0.35f);
                color = Color.Lerp(color, new Color(0.05f, 0.04f, 0.05f), m);
                height -= m * 0.6f;
            }

            // ---- domain adornment: three slanted strokes on each temple -------------------
            if (ctx.Accent.Adornment)
            {
                Adornment(ctx, px, "Temple.L", ref color);
                Adornment(ctx, px, "Temple.R", ref color);
            }
        }

        static void EyeRegion(PaintContext ctx, in PaintContext.Pixel px, string name, ref Color color, ref float height)
        {
            if (!ctx.Landmarks.TryGet(name, out var eye)) return;
            float du = PaintContext.WrapU(px.U - eye.Uv.x) / Mathf.Max(1e-4f, eye.UvRadius.x);
            float dv = (px.V - eye.Uv.y) / Mathf.Max(1e-4f, eye.UvRadius.y);
            float r = GeometryKit.SafeSqrt(du * du + dv * dv);
            if (r > 3.2f) return;
            float open = Mathf.Clamp(ctx.Blueprint.Eye != null ? ctx.Blueprint.Eye.LidOpen : 0.62f, 0.15f, 1f);
            float angle = Mathf.Atan2(dv, du);
            // The visible eye opening in texture space ≈ 0.9 ring radii wide, open×0.75 tall.
            float openR = 0.9f * (Mathf.Abs(Mathf.Cos(angle)) + (dv >= 0f ? 0.85f * open : 0.5f * open) * Mathf.Abs(Mathf.Sin(angle)));
            // Lash line: a dark band just outside the upper edge of the opening.
            float upper = dv >= 0f ? 1f : 0.25f;
            float band = GeometryKit.Bell((r - openR - 0.04f) / 0.075f) * upper;
            band *= 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(angle * 0.5f + 0.4f));
            color = Color.Lerp(color, new Color(0.12f, 0.07f, 0.06f), band * 0.6f);
            // Socket shading: gentle, cool, wider than the opening.
            float socket = GeometryKit.Bell(r / 2.8f) * 0.055f;
            color.r -= socket * 0.9f; color.g -= socket; color.b -= socket * 0.6f;
            // Lid crease above the eye.
            float crease = GeometryKit.Bell((r - openR - 0.42f) / 0.12f) * Mathf.Clamp01(dv) * 0.35f;
            color *= 1f - crease * 0.5f;
            height -= crease * 0.3f;
        }

        static void DarkRing(FaceLandmarks lm, in PaintContext.Pixel px, string name, float inner, float outer, ref Color color)
        {
            if (!lm.TryGet(name, out var l)) return;
            float d = PaintContext.LandmarkDistance(l, px.U, px.V);
            float m = GeometryKit.Bell((d - (inner + outer) * 0.5f) / ((outer - inner) * 0.5f));
            color = Color.Lerp(color, color * 0.55f, m);
        }

        static void Brow(PaintContext ctx, in PaintContext.Pixel px, string name, float humanMask, ref Color color, ref float height)
        {
            if (!ctx.Landmarks.TryGet(name, out var b)) return;
            float outward = b.Uv.x >= 0.5f ? 1f : -1f;
            float ru = Mathf.Max(1e-4f, b.UvRadius.x), rv = Mathf.Max(1e-4f, b.UvRadius.y);
            float s = (PaintContext.WrapU(px.U - b.Uv.x) * outward) / (2.3f * ru) + 0.38f;   // 0 inner … 1 outer
            if (s < -0.05f || s > 1.05f) return;
            float sc = Mathf.Clamp01(s);
            float browRidge = ctx.Blueprint.Shape.Clamped(HeadAxis.BrowRidge);
            float arch = rv * (0.55f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(sc * 0.9f + 0.05f)) - 0.15f * sc);
            float lineV = b.Uv.y + arch + rv * 0.15f * browRidge;
            float thick = rv * (0.55f - 0.34f * sc) * (0.75f + 0.5f * ctx.Blueprint.Genome.HairVolume);
            float edge = 1f + 0.35f * (Noise.Value(px.U * 900f, px.V * 900f, ctx.Seed + 41) - 0.5f);
            float d = Mathf.Abs(px.V - lineV) / Mathf.Max(1e-4f, thick * edge);
            float ends = (1f - GeometryKit.Smooth((-s) / 0.06f)) * (1f - GeometryKit.Smooth((s - 1f) / 0.06f));
            float m = GeometryKit.Bell(d) * ends;
            float strands = 0.6f + 0.4f * Noise.Value(px.U * 1400f, px.V * 300f, ctx.Seed + 42);
            m = GeometryKit.SmoothStep(0.12f, 0.7f, m * strands) * humanMask;
            color = Color.Lerp(color, ctx.BrowColor, m * 0.92f);
            height += m * 0.25f;
        }

        static void Adornment(PaintContext ctx, in PaintContext.Pixel px, string name, ref Color color)
        {
            if (!ctx.Landmarks.TryGet(name, out var t)) return;
            float ru = Mathf.Max(1e-4f, t.UvRadius.x), rv = Mathf.Max(1e-4f, t.UvRadius.y);
            float du = PaintContext.WrapU(px.U - t.Uv.x) / ru, dv = (px.V - t.Uv.y) / rv;
            if (Mathf.Abs(du) > 1.6f || Mathf.Abs(dv) > 1.2f) return;
            for (int k = -1; k <= 1; k++)
            {
                float x0 = k * 0.55f + dv * 0.35f;     // slanted
                float d = Mathf.Abs(du - x0) / 0.11f;
                float m = GeometryKit.Bell(d) * (1f - GeometryKit.Smooth((Mathf.Abs(dv) - 0.75f) / 0.2f));
                m = GeometryKit.SmoothStep(0.2f, 0.7f, m);
                color = Color.Lerp(color, ctx.Accent.Accent, m * 0.9f);
            }
        }
    }
}
