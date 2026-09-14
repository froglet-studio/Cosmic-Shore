using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A canine tooth at each mouth corner, pointing down and slightly out. Keratin (pale).
    /// Embedded at the bilateral MouthCorner site; head units.
    /// </summary>
    public static class FangFeature
    {
        const int Sections = 5, Segs = 8;

        public static void Generate(FeatureContext ctx)
        {
            var p = ctx.Params.Fang;
            var part = ctx.Begin("Fang", CharacterMaterialSlot.Keratin);
            Vector3 root = new Vector3(0f, 0.012f, -0.004f);
            Vector3 dir = new Vector3(0.08f, -1f, 0.25f).normalized;
            var rings = new List<IReadOnlyList<Vector3>>();
            var uvs = new List<IReadOnlyList<Vector2>>();
            for (int s = 0; s < Sections; s++)
            {
                float t = s / (float)Sections;
                Vector3 c = root + dir * (p.Length * t);
                GeometryKit.Frame(dir, Vector3.forward, out var e1, out var e2);
                float radius = p.Radius * (1f - t * 0.85f);
                var ring = new Vector3[Segs];
                var uv = new Vector2[Segs];
                for (int k = 0; k < Segs; k++)
                {
                    float a = GeometryKit.Tau * k / Segs;
                    ring[k] = c + (e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a)) * radius;
                    uv[k] = new Vector2(k / (float)Segs, t);
                }
                rings.Add(ring); uvs.Add(uv);
            }
            GeometryKit.Loft(part, rings, uvs, capTip: true, root + dir * p.Length, new Vector2(0.5f, 1f));
            GeometryKit.RecalculateNormals(part);
        }
    }
}
