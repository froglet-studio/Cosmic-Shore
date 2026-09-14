using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Vibrissae: a fan of thin tapered strands from each cheek, pointing outward and forward.
    /// Keratin slot (pale when no beak/mandible/antenna claims the keratin colour). Embedded.
    /// </summary>
    public static class WhiskerFeature
    {
        const int Sections = 6, Segs = 5;

        public static void Generate(FeatureContext ctx)
        {
            var p = ctx.Params.Whisker;
            var part = ctx.Begin("Whiskers", CharacterMaterialSlot.Keratin);
            int n = Mathf.Max(1, p.PerSide);
            float fan = p.FanDeg * Mathf.Deg2Rad;
            var rng = ctx.Rng;
            for (int i = 0; i < n; i++)
            {
                float f = n == 1 ? 0.5f : i / (float)(n - 1);
                float pitch = (f - 0.5f) * fan;
                float jitter = rng.Range(-0.08f, 0.08f);
                // Outward (+x is the outer side of a cheek site), forward (+z), fanned in pitch.
                Vector3 dir = new Vector3(0.55f + jitter, Mathf.Sin(pitch) - 0.05f, Mathf.Cos(pitch)).normalized;
                Vector3 root = new Vector3(0f, (f - 0.5f) * 0.05f, -0.006f);
                var rings = new List<IReadOnlyList<Vector3>>();
                var uvs = new List<IReadOnlyList<Vector2>>();
                for (int s = 0; s < Sections; s++)
                {
                    float t = s / (float)(Sections - 1);
                    Vector3 c = root + dir * (p.Length * t) + new Vector3(0f, -0.25f * p.Length * t * t, 0f);
                    Vector3 tangent = (dir + new Vector3(0f, -0.5f * t, 0f)).normalized;
                    GeometryKit.Frame(tangent, Vector3.up, out var e1, out var e2);
                    float radius = p.Radius * (1f - 0.9f * t);
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
                GeometryKit.Loft(part, rings, uvs, capTip: false, Vector3.zero, Vector2.zero);
            }
            ctx.Rng = rng;
            GeometryKit.RecalculateNormals(part);
        }
    }
}
