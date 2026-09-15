using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A covering's base colour and its MARKINGS — tabby stripes, countershading, scute plates,
    /// iridescent sheen, ridging — and the covering's own COVERAGE mask (where on the head it
    /// reaches at this weight). The domain accent tints the markings per
    /// <see cref="CharacterPaletteBinding"/>. This is the file for "the markings are wrong" and
    /// for "the feathers stop in the wrong place".
    /// </summary>
    public static class MarkingsLayer
    {
        /// <summary>0..1 how much of this covering shows at the pixel.</summary>
        public static float Coverage(CoveringLayer layer, in PaintContext.Pixel px, int seed)
        {
            if (layer.IsHuman) return 1f;
            var r = layer.Recipe;
            float edge = r.AnchorThetaDeg + layer.ReachDeg
                         + 18f * (Noise.Fbm(px.Phi * 2.5f + 5f, px.ThetaDeg * 0.04f, 3, 2f, 0.5f, seed + 21) - 0.5f)
                         + 6f * (Noise.Value(px.Phi * 14f, px.ThetaDeg * 0.3f, seed + 22) - 0.5f);
            // Coverings hug the face's centre line less than its sides: the reach shrinks toward φ = 0.
            float faceBias = 12f * GeometryKit.Bell(px.Phi / 0.9f);
            float t = (px.ThetaDeg - (edge - faceBias)) / 10f;
            return 1f - GeometryKit.Smooth(t + 0.5f);
        }

        public static Color Base(CoveringRecipe r, float key, in PaintContext.Pixel px, in CharacterPaletteBinding.DomainAccent accent, int seed)
        {
            Color c = Color.Lerp(r.BaseA, r.BaseB, Mathf.Clamp01(key));
            Color marking = Color.Lerp(r.MarkingColor, accent.Accent * 0.6f + r.MarkingColor * 0.4f, accent.MarkingTint);
            float s = r.MarkingStrength;
            switch (r.Marking)
            {
                case MarkingKind.Stripes:
                {
                    float wobble = 2.4f * (Noise.Fbm(px.Phi * 1.6f, px.ThetaDeg * 0.05f, 2, 2f, 0.5f, seed + 31) - 0.5f);
                    float band = Mathf.Sin(px.ThetaDeg * 0.0349f * r.MarkingScale + px.Phi * 1.1f + wobble);
                    float m = GeometryKit.SmoothStep(0.45f, 0.85f, band) * s;
                    return Color.Lerp(c, marking, m);
                }
                case MarkingKind.Countershade:
                {
                    float m = GeometryKit.SmoothStep(95f, 150f, px.ThetaDeg) * s;
                    return Color.Lerp(c, marking, m);
                }
                case MarkingKind.Scutes:
                {
                    Noise.Worley(px.Phi * 24f * 0.55f, px.ThetaDeg * 0.42f * 0.55f, seed + 6, out float id);
                    return Color.Lerp(c, Color.Lerp(marking, c, id), s * 0.7f);
                }
                case MarkingKind.Sheen:
                {
                    float m = (0.5f + 0.5f * Mathf.Sin(px.ThetaDeg * 0.07f + px.Phi * 0.8f + 1.3f)) * s;
                    float glint = GeometryKit.SmoothStep(0.6f, 0.9f, Noise.Fbm(px.Phi * 5f, px.ThetaDeg * 0.12f, 2, 2f, 0.5f, seed + 33));
                    return Color.Lerp(c, marking, m * 0.22f + glint * 0.25f * s);
                }
                case MarkingKind.Ridges:
                {
                    float ridge = 0.5f + 0.5f * Mathf.Sin(px.ThetaDeg * 0.3f * r.MarkingScale);
                    return Color.Lerp(c, marking, ridge * s * 0.5f);
                }
                case MarkingKind.Blaze:
                {
                    // Nose stripe: a band along φ = 0 between the brow and the mouth; cheek
                    // flanks either side of it in BaseB. Read straight off the head angles, so it
                    // lands wherever the muzzle is.
                    float stripe = GeometryKit.Bell(px.Phi / 0.13f) * GeometryKit.SmoothStep(84f, 92f, px.ThetaDeg) * (1f - GeometryKit.SmoothStep(118f, 126f, px.ThetaDeg));
                    float flank = GeometryKit.Bell((Mathf.Abs(px.Phi) - 0.42f) / 0.28f) * GeometryKit.SmoothStep(88f, 98f, px.ThetaDeg) * (1f - GeometryKit.SmoothStep(122f, 132f, px.ThetaDeg));
                    Color flankCol = r.BaseB;
                    Color o = Color.Lerp(c, flankCol, flank * s);
                    return Color.Lerp(o, marking, stripe * s);
                }
                default:
                    return c;
            }
        }
    }
}
