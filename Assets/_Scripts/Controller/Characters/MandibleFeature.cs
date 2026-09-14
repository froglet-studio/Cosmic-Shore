using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Paired arthropod mandibles: two curved, tapering pincers rooted either side of the mouth
    /// and curling inward toward each other. Embedded at the Mouth site; head units.
    /// </summary>
    public static class MandibleFeature
    {
        const int Sections = 12, Segs = 10;

        public static void Generate(FeatureContext ctx)
        {
            var p = ctx.Params.Mandible;
            var part = ctx.Begin("Mandibles", CharacterMaterialSlot.Keratin);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 root = new Vector3(side * p.Spread * 0.5f, -0.01f, -0.012f);
                Vector3 ctrl = root + new Vector3(side * p.Length * 0.12f, -0.04f, p.Length * 0.8f);
                Vector3 end = new Vector3(side * p.Spread * 0.05f, -0.06f, p.Length * (0.95f - 0.25f * p.Curl));
                var rings = new List<IReadOnlyList<Vector3>>();
                var uvs = new List<IReadOnlyList<Vector2>>();
                for (int i = 0; i < Sections; i++)
                {
                    float t = i / (float)Sections;
                    Vector3 c = Bezier(root, ctrl, end, t);
                    Vector3 tangent = (Bezier(root, ctrl, end, t + 0.01f) - c).normalized;
                    GeometryKit.Frame(tangent, Vector3.up, out var e1, out var e2);
                    float radius = p.RootRadius * GeometryKit.SafePow(1f - t, 0.8f);
                    var ring = new Vector3[Segs];
                    var uv = new Vector2[Segs];
                    for (int k = 0; k < Segs; k++)
                    {
                        float a = GeometryKit.Tau * k / Segs;
                        ring[k] = c + (e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a) * 0.75f) * radius;
                        uv[k] = new Vector2(k / (float)Segs, t);
                    }
                    rings.Add(ring); uvs.Add(uv);
                }
                GeometryKit.Loft(part, rings, uvs, capTip: true, end, new Vector2(0.5f, 1f));
            }
            GeometryKit.RecalculateNormals(part);
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return a * (u * u) + b * (2f * u * t) + c * (t * t);
        }
    }
}
