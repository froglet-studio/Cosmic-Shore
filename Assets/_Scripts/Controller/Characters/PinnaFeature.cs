using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An external ear: a cupped, rimmed plate with thickness. One generator serves the human
    /// ear (rounded, cupped, at EarSide), the felid ear (tall, pointed, at EarTop, standing along
    /// the head normal by the trait's site tilt) and the chiropteran ear (very tall, ribbed by
    /// texture, with a tragus) — the DIFFERENCE is data on <see cref="PinnaParams"/>.
    /// Local: the plate lies in the xy plane facing +z; +y is the ear's tall axis; the root
    /// (attachment) edge is at x ≈ 0 and the plate extends toward +x. Embedded. Head units.
    /// </summary>
    public static class PinnaFeature
    {
        const int Rings = 7, Spokes = 22;

        public static void Generate(FeatureContext ctx)
        {
            var p = ctx.Params.Pinna;
            float hw = p.Width * 0.5f, hh = p.Height * 0.5f;
            float pitch = p.ForwardTiltDeg * Mathf.Deg2Rad;
            float roll = p.OutwardRollDeg * Mathf.Deg2Rad;

            var ear = ctx.Begin("Pinna", CharacterMaterialSlot.Skin, projectUvs: true);
            var front = new Vector3[Rings + 1, Spokes + 1];
            var back = new Vector3[Rings + 1, Spokes + 1];
            var uvF = new Vector2[Rings + 1, Spokes + 1];

            for (int r = 0; r <= Rings; r++)
            {
                float rho = r / (float)Rings;
                for (int s = 0; s <= Spokes; s++)
                {
                    float a = GeometryKit.Tau * s / Spokes;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    // Pointed: stretch the upper half toward a tip.
                    float stretch = 1f + p.Pointed * 0.75f * GeometryKit.SafePow(Mathf.Max(0f, sa), 3f);
                    float x = hw * 0.9f + rho * hw * ca;          // plate centred at x = 0.9 hw so the root sits at the head
                    float y = rho * hh * sa * stretch;
                    // Concha bowl (dips toward the head) and helix rim (rolls outward).
                    float bowl = -p.CupDepth * p.Width * 0.55f * GeometryKit.Bell(rho / 0.62f);
                    float rim = p.RimThickness * p.Width * 1.6f * GeometryKit.Bell((rho - 0.86f) / 0.16f);
                    float z = bowl + rim + p.Width * 0.30f + x * 0.15f;   // stands proud; the back edge more so
                    Vector3 v = new Vector3(x, y, z);
                    // Pitch about y (face forward), roll about x (top outward).
                    v = GeometryKit.Rotate(v, Vector3.up, -pitch);
                    v = GeometryKit.Rotate(v, Vector3.right, -roll);
                    front[r, s] = v;
                    back[r, s] = v + new Vector3(0f, 0f, -p.Thickness);
                    uvF[r, s] = new Vector2(0.5f + 0.5f * rho * ca, 0.5f + 0.5f * rho * sa);
                }
            }
            // Root inset: push the whole ear a little into the head so no gap shows at the seam.
            for (int r = 0; r <= Rings; r++)
                for (int s = 0; s <= Spokes; s++)
                {
                    front[r, s].z -= 0.012f;
                    back[r, s].z -= 0.012f;
                }

            int f0 = GeometryKit.Grid(ear, front, uvF);
            // Grid winding is (spoke × ring) = −z here; the FRONT sheet must face +z, the back −z.
            for (int i = 0; i < ear.Tris.Count; i += 3)
            {
                int t = ear.Tris[i + 1]; ear.Tris[i + 1] = ear.Tris[i + 2]; ear.Tris[i + 2] = t;
            }
            int b0 = GeometryKit.Grid(ear, back, uvF);
            // Rim strip between the outer rings.
            int outerF = f0 + Rings * (Spokes + 1), outerB = b0 + Rings * (Spokes + 1);
            for (int s = 0; s < Spokes; s++)
                ear.AddQuad(outerF + s, outerB + s, outerB + s + 1, outerF + s + 1);

            if (p.Tragus > 0.01f)
            {
                // Tragus: a small forward-facing leaf at the front of the root.
                var tr = ctx.Begin("Tragus", CharacterMaterialSlot.Skin, projectUvs: true);
                float th = p.Height * 0.28f * p.Tragus, tw = p.Width * 0.22f;
                var pts = new Vector3[5, 3];
                var uvs = new Vector2[5, 3];
                for (int r = 0; r < 5; r++)
                {
                    float t = r / 4f;
                    float halfW = tw * Mathf.Sin(Mathf.PI * Mathf.Clamp01(0.15f + 0.85f * t)) * 0.5f;
                    for (int c = 0; c < 3; c++)
                    {
                        float xx = -0.02f + (c - 1) * halfW;
                        Vector3 v = new Vector3(xx, -th * 0.35f + t * th, 0.012f + t * 0.02f);
                        v = GeometryKit.Rotate(v, Vector3.up, -pitch * 0.5f);
                        pts[r, c] = v; uvs[r, c] = new Vector2(c * 0.5f, t);
                    }
                }
                GeometryKit.Grid(tr, pts, uvs);
                GeometryKit.RecalculateNormals(tr);
            }

            GeometryKit.RecalculateNormals(ear);
        }
    }
}
