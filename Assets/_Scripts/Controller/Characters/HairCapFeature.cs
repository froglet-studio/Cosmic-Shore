using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Short cropped hair as a shell over the scalp: the head surface inside the hairline,
    /// offset outward by a clumped thickness that thins to nothing at the hairline. A bald
    /// procedural head reads as a mannequin; even a cap of hair makes it a person. Hair slot.
    /// Samples the head SURFACE directly (not a site), so it follows any head shape.
    /// This is the file for "the hairline is wrong".
    /// </summary>
    public static class HairCapFeature
    {
        const int Rows = 22, Cols = 48;

        public static void Generate(FeatureContext ctx)
        {
            var p = ctx.Params.Hair;
            float volume = Mathf.Clamp01(ctx.HairVolume);
            float thickness = p.Thickness * (0.3f + 0.7f * volume);
            var part = ctx.Begin("HairCap", CharacterMaterialSlot.Hair);
            part.IsHeadSpace = true;   // sampled straight off the surface, not through the site
            var pts = new Vector3[Rows + 1, Cols + 1];
            var uvs = new Vector2[Rows + 1, Cols + 1];
            float front = p.FrontHairlineDeg * Mathf.Deg2Rad;
            float side = p.SideHairlineDeg * Mathf.Deg2Rad;
            float back = p.BackHairlineDeg * Mathf.Deg2Rad;
            for (int c = 0; c <= Cols; c++)
            {
                float phi = (c / (float)Cols - 0.5f) * GeometryKit.Tau;
                float cp = Mathf.Cos(phi);
                float wf = Mathf.Max(0f, cp); wf *= wf;
                float wb = Mathf.Max(0f, -cp); wb *= wb;
                float ws = Mathf.Max(0f, 1f - wf - wb);
                // Widow's peak / temple recession as gentle noise on the hairline.
                float hairline = (front * wf + side * ws + back * wb) * (1f + 0.05f * (Noise.Value(c * 0.35f, 0f, 3) - 0.5f));
                for (int r = 0; r <= Rows; r++)
                {
                    float t = r / (float)Rows;
                    float theta = hairline * t;
                    Vector3 dir = GeometryKit.Dir(theta, phi);
                    Vector3 surf = ctx.Surface.Sample(dir);
                    float taper = 1f - GeometryKit.Smooth((t - 0.72f) / 0.28f);
                    float clump = 1f + p.Clumping * 0.6f * (Noise.Fbm(phi * 2f + 10f, theta * 4f, 3, 2f, 0.5f, 11) - 0.5f);
                    pts[r, c] = surf + dir * (thickness * taper * clump);
                    // Head-space projection is the UV: hair is painted in its own texture.
                    uvs[r, c] = new Vector2(c / (float)Cols, 1f - t);
                }
            }
            GeometryKit.Grid(part, pts, uvs);
            // Grid winding faces −z of the parameter plane; the sweep here runs crown → hairline with
            // φ increasing, which needs the flip to face outward.
            part.FlipWinding();
            GeometryKit.RecalculateNormals(part);
        }
    }
}
