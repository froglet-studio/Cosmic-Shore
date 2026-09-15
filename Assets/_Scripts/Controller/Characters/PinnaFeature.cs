using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An external ear: an ear-shaped plate with a SCULPTED profile — helix rim, scapha groove,
    /// antihelix ridge, concha bowl, tragus and antitragus, a fleshy lobe — with thickness. One
    /// generator serves the human ear (rounded, cupped, at EarSide), the felid ear (tall,
    /// pointed, at EarTop, standing along the head normal by the trait's site tilt) and the
    /// chiropteran ear (very tall, with a big tragus) — the DIFFERENCE is data on
    /// <see cref="PinnaParams"/>. This is the file for "the ears are discs".
    /// Local: the plate lies in the xy plane facing +z (away from the head); +y is the ear's tall
    /// axis; the root (attachment) edge is at x ≈ 0 and the plate extends toward +x (the back
    /// of the head). Embedded. Head units.
    /// </summary>
    public static class PinnaFeature
    {
        const int Rings = 10, Spokes = 36;

        public static void Generate(FeatureContext ctx)
        {
            var p = ctx.Params.Pinna;
            float hw = p.Width * 0.5f, hh = p.Height * 0.5f;
            float pitch = p.ForwardTiltDeg * Mathf.Deg2Rad;
            float roll = p.OutwardRollDeg * Mathf.Deg2Rad;
            float rimH = p.RimThickness * p.Width * 1.1f;
            float bowl = p.CupDepth * p.Width * 0.45f;

            var ear = ctx.Begin("Pinna", CharacterMaterialSlot.Skin, projectUvs: true);
            var front = new Vector3[Rings + 1, Spokes + 1];
            var back = new Vector3[Rings + 1, Spokes + 1];
            var uvF = new Vector2[Rings + 1, Spokes + 1];

            for (int rr = 0; rr <= Rings; rr++)
            {
                float rho = rr / (float)Rings;
                for (int s = 0; s <= Spokes; s++)
                {
                    float a = GeometryKit.Tau * s / Spokes;        // 0 = back (+x), π/2 = top, π = front (root), 3π/2 = lobe
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    // Outline: wider across the top, narrowing into a rounded lobe; pointed
                    // stretches the upper half toward a tip.
                    float taper = 1f + 0.10f * sa - 0.16f * Mathf.Max(0f, -sa);
                    float stretch = 1f + p.Pointed * 0.85f * GeometryKit.SafePow(Mathf.Max(0f, sa), 3f);
                    float x = hw * 0.85f + rho * hw * ca * taper;   // plate centred at x = 0.85 hw so the root sits at the head
                    float y = rho * hh * sa * stretch;
                    // Profile. Weights: back half (the helix side) vs the front (root/tragus) half.
                    float backW = 0.5f + 0.5f * ca;
                    float topW = Mathf.Max(0f, sa);
                    float lobeW = Mathf.Max(0f, -sa);
                    float rim = rimH * GeometryKit.Bell((rho - 0.88f) / 0.15f) * (0.35f + 0.65f * backW) * (1f - 0.5f * lobeW);
                    float scapha = -0.45f * rimH * GeometryKit.Bell((rho - 0.70f) / 0.13f) * backW * (1f - lobeW);
                    float antihelix = 0.8f * rimH * GeometryKit.Bell((rho - 0.52f) / 0.17f) * backW * (0.4f + 0.6f * topW) * (1f - lobeW);
                    float concha = -bowl * GeometryKit.Bell(rho / 0.42f);
                    float tragus = p.Width * 0.22f * (0.3f + p.Tragus) * GeometryKit.Bell((rho - 0.82f) / 0.2f) * GeometryKit.Bell((a - Mathf.PI) / 0.55f);
                    float antitragus = p.Width * 0.12f * GeometryKit.Bell((rho - 0.78f) / 0.2f) * GeometryKit.Bell((a - 4.2f) / 0.45f);
                    float lobe = p.Width * 0.10f * GeometryKit.Bell((rho - 0.6f) / 0.5f) * lobeW * lobeW;
                    float stand = p.Width * 0.28f + x * 0.22f;       // the whole ear stands proud, the back edge more so
                    float z = stand + rim + scapha + antihelix + concha + tragus + antitragus + lobe;
                    Vector3 v = new Vector3(x, y, z);
                    // Pitch about y (face forward), roll about x (top outward).
                    v = GeometryKit.Rotate(v, Vector3.up, -pitch);
                    v = GeometryKit.Rotate(v, Vector3.right, -roll);
                    front[rr, s] = v;
                    // Back sheet: thicker at the rim, fleshier at the lobe, thin in the concha.
                    float thick = p.Thickness * (0.6f + 0.6f * GeometryKit.Smooth(rho / 0.7f) + 0.8f * lobeW * rho);
                    back[rr, s] = v + new Vector3(0f, 0f, -thick - Mathf.Max(0f, rim + antihelix));
                    uvF[rr, s] = new Vector2(0.5f + 0.5f * rho * ca, 0.5f + 0.5f * rho * sa);
                }
            }
            // Root inset: push the whole ear a little into the head so no gap shows at the seam.
            for (int rr = 0; rr <= Rings; rr++)
                for (int s = 0; s <= Spokes; s++)
                {
                    front[rr, s].z -= 0.012f;
                    back[rr, s].z -= 0.012f;
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
            GeometryKit.RecalculateNormals(ear);
        }
    }
}
