using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Fine structure per covering — pores on skin, strands in fur, barbs in feathers, plate seams
    /// in scales, the polish of chitin, the wet mottle of hide — as a COLOUR modulation plus a
    /// HEIGHT contribution (the skin normal map is derived from height). This is the file for
    /// "the skin texture is plastic" when the complaint is about pores rather than colour.
    /// </summary>
    public static class SurfaceDetailLayer
    {
        /// <summary>Returns a luminance multiplier around 1 and writes a height in [0,1].</summary>
        public static float Evaluate(CoveringKind kind, float detailStrength, float age, in PaintContext.Pixel px, int seed, out float height)
        {
            float x = px.Phi * 24f, y = px.ThetaDeg * 0.42f;   // ~ isotropic-ish around the head
            switch (kind)
            {
                case CoveringKind.Skin:
                {
                    float pores = Noise.Fbm(x * 14f, y * 14f, 3, 2.2f, 0.5f, seed + 1);
                    float fine = Noise.Value(x * 40f, y * 40f, seed + 2);
                    float amount = detailStrength * (0.45f + 0.9f * age);
                    height = 0.5f + (pores - 0.5f) * 0.22f * amount + (fine - 0.5f) * 0.12f * amount;
                    return 1f + (pores - 0.5f) * 0.06f * amount + (fine - 0.5f) * 0.03f * amount;
                }
                case CoveringKind.Fur:
                {
                    float strands = Noise.Fbm(x * 3f, y * 40f, 3, 2f, 0.5f, seed + 3);   // long along φ
                    height = 0.5f + (strands - 0.5f) * 0.8f * detailStrength;
                    return 1f + (strands - 0.5f) * 0.32f * detailStrength;
                }
                case CoveringKind.Feather:
                {
                    float barbs = Noise.Fbm(x * 2f, y * 55f, 2, 2f, 0.5f, seed + 4);
                    float vanes = Noise.Value(x * 6f, y * 4f, seed + 5);
                    height = 0.5f + (barbs - 0.5f) * 0.7f * detailStrength;
                    return 1f + (barbs - 0.5f) * 0.26f * detailStrength + (vanes - 0.5f) * 0.12f;
                }
                case CoveringKind.Scale:
                {
                    float d = Noise.Worley(x * 0.55f, y * 0.55f, seed + 6, out _);
                    float seam = GeometryKit.SmoothStep(0.40f, 0.50f, d) ;  // near the cell boundary → seam
                    height = 0.62f - seam * 0.6f * detailStrength;
                    return 1f - seam * 0.35f * detailStrength;
                }
                case CoveringKind.Chitin:
                {
                    float polish = Noise.Fbm(x * 1.5f, y * 1.5f, 2, 2f, 0.5f, seed + 7);
                    float pits = Noise.Value(x * 14f, y * 14f, seed + 8);
                    height = 0.5f + (polish - 0.5f) * 0.15f - GeometryKit.SmoothStep(0.86f, 0.95f, pits) * 0.3f * detailStrength;
                    return 1f + (polish - 0.5f) * 0.08f;
                }
                case CoveringKind.Hide:
                default:
                {
                    float mottle = Noise.Fbm(x * 2.5f, y * 2.5f, 3, 2f, 0.5f, seed + 9);
                    height = 0.5f + (mottle - 0.5f) * 0.12f * detailStrength;
                    return 1f + (mottle - 0.5f) * 0.09f * detailStrength;
                }
            }
        }
    }
}
