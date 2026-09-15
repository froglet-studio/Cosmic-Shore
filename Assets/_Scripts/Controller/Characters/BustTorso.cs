using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The bust below the head: shoulders and upper chest in a flight jacket, with a stand-up
    /// collar around the neck — the framing every shipped avatar illustration shares. Built in
    /// head space under the head's neck; Gear slot (the jacket region of the gear texture, the
    /// collar band carrying the domain accent). Scales with NeckThickness so a bull neck gets
    /// bull shoulders. This is the file for "the shoulders / the jacket are wrong".
    /// </summary>
    public static class BustTorso
    {
        const int Rings = 14, Segs = 48;

        public static MeshPart Generate(IHeadSurface surface, CharacterBlueprint bp)
        {
            var part = new MeshPart("Torso", CharacterMaterialSlot.Gear) { IsHeadSpace = true };
            float neck = GeometryKit.Dial(bp.Shape.Clamped(HeadAxis.NeckThickness), 0.85f, 1.3f);
            float flesh = Mathf.Lerp(0.9f, 1.12f, Mathf.Clamp01(bp.Genome.Fleshiness));
            float yTop = -0.70f, yBottom = -1.60f;

            // Body: superellipse rings from the collar line down to the frame's bottom.
            var pts = new Vector3[Rings + 1][];
            var uvs = new Vector2[Rings + 1][];
            for (int r = 0; r <= Rings; r++)
            {
                float t = r / (float)Rings;
                float y = Mathf.Lerp(yTop, yBottom, t);
                // Width: neck-tight at the collar, out to the shoulders by ~35% down, then straight.
                float shoulder = GeometryKit.Smooth((t - 0.02f) / 0.22f);
                float rx = Mathf.Lerp(0.33f * neck, 0.82f * flesh, shoulder) + 0.02f * GeometryKit.Bell((t - 0.45f) / 0.3f);
                float rz = Mathf.Lerp(0.30f * neck, 0.36f * flesh, shoulder);
                // Boxier toward the shoulders.
                float exponent = Mathf.Lerp(2.0f, 3.2f, shoulder);
                pts[r] = new Vector3[Segs];
                uvs[r] = new Vector2[Segs];
                for (int k = 0; k < Segs; k++)
                {
                    float a = GeometryKit.Tau * k / Segs;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    float x = Mathf.Sign(sa) * GeometryKit.SafePow(Mathf.Abs(sa), 2f / exponent) * rx;
                    float z = Mathf.Sign(ca) * GeometryKit.SafePow(Mathf.Abs(ca), 2f / exponent) * rz;
                    // Shoulders slope down toward the sides; chest is a shade forward.
                    float slope = -0.22f * GeometryKit.SafePow(Mathf.Abs(sa), 1.5f) * shoulder;
                    pts[r][k] = new Vector3(x, y + slope, z - 0.04f + 0.03f * Mathf.Max(0f, ca));
                    uvs[r][k] = new Vector2(0.02f + 0.46f * (k / (float)Segs), 0.05f + 0.9f * (1f - t));
                }
            }
            GeometryKit.Loft(part, pts, uvs, true, new Vector3(0f, yBottom - 0.02f, -0.04f), new Vector2(0.25f, 0.02f));

            // Collar: a stand-up band around the neck, open at the front, wearing the accent band (v ≥ 0.92).
            float cTop = -0.56f, cBottom = -0.76f;
            int f0 = part.Verts.Count;
            const int CSegs = 36;
            for (int k = 0; k <= CSegs; k++)
            {
                float a = Mathf.Lerp(0.35f, GeometryKit.Tau - 0.35f, k / (float)CSegs);   // open at the front (a = 0 is +z)
                float sa = Mathf.Sin(a), ca = Mathf.Cos(a);
                float rx = 0.26f * neck, rz = 0.235f * neck;
                float flare = 1f + 0.35f * GeometryKit.Bell((a - Mathf.PI) / 1.6f);        // stands out more at the back
                Vector3 inner = new Vector3(rx * sa, 0f, rz * ca - 0.05f);
                Vector3 outer = inner * (1f + 0.16f * flare);
                float uu = k / (float)CSegs;
                part.AddVertex(inner + Vector3.up * cBottom, new Vector2(0.02f + 0.46f * uu, 0.93f));
                part.AddVertex(outer + Vector3.up * cTop, new Vector2(0.02f + 0.46f * uu, 0.99f));
                part.AddVertex(outer * 1.05f + Vector3.up * (cTop - 0.02f), new Vector2(0.02f + 0.46f * uu, 0.99f));
                part.AddVertex(outer * 1.05f + Vector3.up * (cBottom - 0.02f), new Vector2(0.02f + 0.46f * uu, 0.93f));
            }
            for (int k = 0; k < CSegs; k++)
            {
                int a = f0 + k * 4;
                part.AddQuad(a + 0, a + 1, a + 5, a + 4);     // inner face
                part.AddQuad(a + 1, a + 2, a + 6, a + 5);     // top edge
                part.AddQuad(a + 2, a + 3, a + 7, a + 6);     // outer face
                part.AddQuad(a + 3, a + 0, a + 4, a + 7);     // bottom edge
            }
            GeometryKit.RecalculateNormals(part);
            return part;
        }
    }
}
