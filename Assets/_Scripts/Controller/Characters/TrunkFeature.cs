using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A proboscidean trunk: a tapered tube from the nose tip that curls down and back toward
    /// the chest, with wrinkle bands carried by its ring UVs. Skin slot, UVs projected onto the
    /// head so it wears the covering (hide) the clade painted. Embedded. Head units.
    /// Local: +z outward from the site, +y up, +x toward the character's left.
    /// This is the file for "the trunk is wrong".
    /// </summary>
    public static class TrunkFeature
    {
        const int Segs = 20;

        public static void Generate(FeatureContext ctx)
        {
            var p = ctx.Params.Trunk;
            int rings = Mathf.Clamp(p.Rings, 6, 48);
            var part = ctx.Begin("Trunk", CharacterMaterialSlot.Skin, projectUvs: true);
            var ringPts = new Vector3[rings + 1][];
            var ringUv = new Vector2[rings + 1][];
            // Centre line: starts a little inside the face, leaves along +z, then curls down.
            Vector3 pos = new Vector3(0f, 0f, -0.03f);
            Vector3 dir = Vector3.forward;
            float ds = p.Length / rings;
            for (int r = 0; r <= rings; r++)
            {
                float s = r / (float)rings;
                float radius = Mathf.Lerp(p.RootRadius, p.TipRadius, GeometryKit.SafePow(s, 0.85f));
                // Wrinkle bands: the radius breathes with the ring index.
                radius *= 1f + 0.05f * Mathf.Sin(r * 2.4f);
                GeometryKit.Frame(dir, Vector3.up, out var right, out var up);
                ringPts[r] = new Vector3[Segs];
                ringUv[r] = new Vector2[Segs];
                for (int k = 0; k < Segs; k++)
                {
                    float a = GeometryKit.Tau * k / Segs;
                    ringPts[r][k] = pos + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius;
                    ringUv[r][k] = new Vector2(k / (float)Segs, s);
                }
                // Curl: bend the direction downward (and back toward −z) as it goes.
                float bend = p.Droop * (0.6f + 1.5f * s) * ds / p.Length * 1.7f;
                dir = GeometryKit.Rotate(dir, Vector3.right, bend).normalized;
                pos += dir * ds;
            }
            Vector3 tip = pos + dir * (p.TipRadius * 0.6f);
            GeometryKit.Loft(part, ringPts, ringUv, true, tip, new Vector2(0.5f, 1f));
            GeometryKit.RecalculateNormals(part);
        }
    }
}
