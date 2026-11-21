using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The SHAPE of the silence vignette (Docs/ElementalAbilitySystem/SILENCE.md §5): how opaque the
    /// danger-coloured edge is at each point of the screen, before its colour and strength are
    /// applied. Pure and allocation-free, so <c>SilenceScreenFeedback</c> bakes it into a texture
    /// and the Silence Vignette Lab (<c>Docs/Studios/SilenceVignetteLab.html</c>) runs a line-for-line
    /// JavaScript twin of it. <c>Tools/Build/silence_vignette_parity/run.sh</c> holds the two
    /// together.
    ///
    /// <para><b>Why an overlay and not URP's Vignette.</b> URP's Vignette MULTIPLIES the frame by its
    /// colour toward the edges, so a red vignette can only darken. The arena is near-black space
    /// (the palette's sky is (0, 0, 0.1)) and red times near-black is near-black: the first cut,
    /// a URP Vignette at 0.26, was not visible at all in Garrett's Dog Fight run (2026-10-21). This
    /// mask is drawn as an alpha-blended overlay, so it ADDS the danger colour over whatever is
    /// there.</para>
    ///
    /// <para><b>Coordinates.</b> <c>u, v</c> run 0..1 across the screen. The centre is 0; the
    /// screen's top and bottom edge midpoints are distance 1. <see cref="Shape.Roundness"/> 0 makes
    /// the left and right edges 1 too (an ellipse the shape of the screen), 1 makes the shape a
    /// circle in pixels (the sides then sit at the aspect ratio, so the edge reaches the top and
    /// bottom first).</para>
    /// </summary>
    public static class SilenceVignetteMask
    {
        public const int StyleSoft = 0;
        public const int StyleFaceted = 1;

        /// <summary>Everything that decides the mask's shape. Copied from
        /// <c>VesselSilenceConfigSO</c>; the lab's keys are these fields' names.</summary>
        public struct Shape
        {
            public int Style;          // StyleSoft or StyleFaceted
            public int Facets;         // sides of the faceted window (Faceted only)
            public float Inner;        // distance where the edge starts (0 = centre, 1 = top edge)
            public float Feather;      // distance over which the fill rises to full
            public float Roundness;    // 0 = the screen's own ellipse, 1 = a circle in pixels
            public float Fill;         // opacity of the outer fill, 0..1
            public float RimStrength;  // opacity of the bright line at the inner edge, 0..1
            public float RimWidth;     // half-width of that line, in the same distance units
        }

        /// <summary>Distance of screen point (u, v) from the centre, in the mask's units (see the
        /// type doc), for a screen of width / height <paramref name="aspect"/>.</summary>
        public static float Distance(float u, float v, float aspect, in Shape s)
        {
            float x = (u - 0.5f) * 2f * Mathf.Lerp(1f, Mathf.Max(0.01f, aspect), Mathf.Clamp01(s.Roundness));
            float y = (v - 0.5f) * 2f;
            float r = Mathf.Sqrt(x * x + y * y);
            if (s.Style != StyleFaceted || s.Facets < 3) return r;

            // A regular polygon with a flat top: the distance is the point's projection on the
            // normal of the edge it faces, so every iso-line is the same polygon, scaled.
            float seg = 2f * Mathf.PI / s.Facets;
            float start = Mathf.PI * 0.5f - seg * 0.5f;
            float a = Repeat(Mathf.Atan2(y, x) - start, seg) - seg * 0.5f;
            return r * Mathf.Cos(a);
        }

        /// <summary>The mask's opacity at screen point (u, v): the outer fill plus the rim, 0..1.</summary>
        public static float Alpha(float u, float v, float aspect, in Shape s)
        {
            float d = Distance(u, v, aspect, s);
            float fill = SmoothStep(s.Inner, s.Inner + Mathf.Max(0f, s.Feather), d) * Mathf.Clamp01(s.Fill);
            float rim = 0f;
            if (s.RimStrength > 0f && s.RimWidth > 0f)
                rim = Mathf.Clamp01(1f - Mathf.Abs(d - s.Inner) / s.RimWidth) * Mathf.Clamp01(s.RimStrength);
            return Mathf.Clamp01(fill + rim);
        }

        /// <summary>The slow "breathing" of the edge while silenced: 1 at its peak, down to
        /// 1 - <paramref name="depth"/> at its trough, <paramref name="hz"/> breaths a second.</summary>
        public static float Breathe(float time, float depth, float hz)
        {
            if (depth <= 0f || hz <= 0f) return 1f;
            float wave = 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * hz * time);
            return 1f - Mathf.Clamp01(depth) * (1f - wave);
        }

        /// <summary>The overlay's opacity at a point: the mask times (the silence's eased weight x its
        /// strength x its breathing, plus the decaying hit pulse, clamped to 0..1). The game passes
        /// mask 1 and lets the baked texture multiply the mask back in, which is the same product.</summary>
        public static float Opacity(float mask, float weight, float intensity, float breathe, float pulse) =>
            Mathf.Clamp01(mask) *
            Mathf.Clamp01(Mathf.Clamp01(weight) * Mathf.Clamp01(intensity) * breathe + Mathf.Max(0f, pulse));

        static float SmoothStep(float a, float b, float x)
        {
            if (b <= a) return x >= a ? 1f : 0f;
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static float Repeat(float t, float length) => t - Mathf.Floor(t / length) * length;
    }
}
