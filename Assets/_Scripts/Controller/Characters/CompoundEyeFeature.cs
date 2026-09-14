using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An arthropod compound eye: a large dome proud of the socket, no lids, no pupil. The facet
    /// field is TEXTURE (Eye slot, painted as a hex lattice by <see cref="IrisTextureLayer"/>)
    /// on a smooth-shaded cap — the dome must read as one glossy organ with structure, never
    /// as low-poly geometry. Embedded; site-local head units.
    /// </summary>
    public static class CompoundEyeFeature
    {
        const int Rows = 16, Segs = 32;

        public static void Generate(FeatureContext ctx)
        {
            var p = ctx.Params.Eye;
            float orbit = GeometryKit.Dial(ctx.Shape.Clamped(HeadAxis.OrbitalSize), 0.85f, 1.25f);
            float R = p.DomeRadius * orbit;
            float proud = Mathf.Clamp01(p.DomeProud);
            Vector3 centre = new Vector3(0f, 0f, -R * (1f - proud));

            var dome = ctx.Begin("Dome", CharacterMaterialSlot.Eye);
            // A spherical cap a little past the hemisphere so its rim tucks under the skin.
            float betaMax = Mathf.Acos(Mathf.Clamp(-(1f - proud) - 0.12f, -1f, 1f));
            int first = dome.Verts.Count;
            for (int r = 0; r <= Rows; r++)
            {
                float beta = betaMax * r / Rows;
                float sb = Mathf.Sin(beta), cb = Mathf.Cos(beta);
                for (int s = 0; s <= Segs; s++)
                {
                    float a = GeometryKit.Tau * s / Segs;
                    Vector3 d = new Vector3(Mathf.Cos(a) * sb, Mathf.Sin(a) * sb, cb);
                    // UVs: polar from the dome's front pole, so the facet lattice is centred on it.
                    dome.AddVertex(centre + d * R, new Vector2(s / (float)Segs, 1f - r / (float)Rows));
                }
            }
            int cols = Segs + 1;
            for (int r = 0; r < Rows; r++)
                for (int s = 0; s < Segs; s++)
                {
                    int a = first + r * cols + s, b = a + 1, c = a + cols + 1, d = a + cols;
                    dome.AddQuad(a, d, c, b);
                }
            GeometryKit.RecalculateNormals(dome);
        }
    }
}
