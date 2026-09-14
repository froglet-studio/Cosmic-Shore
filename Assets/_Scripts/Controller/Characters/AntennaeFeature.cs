using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A lamellate (scarab-type) antenna: a curved stalk from the brow ending in a fan of flat
    /// club plates. Bilateral (the trait's Brow site is bilateral). Keratin. Embedded; head units.
    /// </summary>
    public static class AntennaeFeature
    {
        const int Sections = 10, Segs = 8;

        public static void Generate(FeatureContext ctx)
        {
            var p = ctx.Params.Antenna;
            var part = ctx.Begin("Antenna", CharacterMaterialSlot.Keratin);
            float el = p.ElevationDeg * Mathf.Deg2Rad, sp = p.SplayDeg * Mathf.Deg2Rad;
            // Stalk direction in site space: mostly outward (+z), lifted (+y) and splayed (+x = outward canthus side).
            Vector3 dir = new Vector3(Mathf.Sin(sp), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(sp)).normalized;
            Vector3 root = new Vector3(0f, 0f, -0.01f);
            Vector3 bend = new Vector3(0f, 0.35f, 0f);
            var rings = new List<IReadOnlyList<Vector3>>();
            var uvs = new List<IReadOnlyList<Vector2>>();
            Vector3 c = root;
            for (int i = 0; i < Sections; i++)
            {
                float t = i / (float)(Sections - 1);
                c = root + dir * (p.Length * t) + bend * (p.Length * t * t);
                Vector3 tangent = (dir + bend * (2f * t)).normalized;
                GeometryKit.Frame(tangent, Vector3.up, out var e1, out var e2);
                float radius = p.Radius * (1f - 0.35f * t);
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
            Vector3 endTangent = (dir + bend * 2f).normalized;
            GeometryKit.Loft(part, rings, uvs, capTip: true, c + endTangent * 0.01f, new Vector2(0.5f, 1f));

            // Club plates: flat ovals fanned about the stalk end.
            GeometryKit.Frame(endTangent, Vector3.up, out var pr, out var pu);
            int plates = Mathf.Max(1, p.Plates);
            for (int i = 0; i < plates; i++)
            {
                float fan = (i - (plates - 1) * 0.5f) * 0.42f;
                Vector3 axis = GeometryKit.Rotate(endTangent, pu, fan);
                var pts = new Vector3[2, 7];
                var uv2 = new Vector2[2, 7];
                for (int s = 0; s < 7; s++)
                {
                    float t = s / 6f;
                    float halfW = p.PlateLength * 0.28f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(0.1f + 0.9f * t));
                    Vector3 mid = c + axis * (p.PlateLength * t);
                    pts[0, s] = mid + pu * halfW; pts[1, s] = mid - pu * halfW;
                    uv2[0, s] = new Vector2(t, 1f); uv2[1, s] = new Vector2(t, 0f);
                }
                GeometryKit.Grid(part, pts, uv2);
                // Double-sided: duplicate with flipped winding so a thin plate never culls away.
                int firstV = part.Verts.Count - 14;
                for (int s = 0; s < 6; s++)
                {
                    int a = firstV + s, b = a + 1, cc = a + 7 + 1, d = a + 7;
                    part.AddTri(a, cc, b); part.AddTri(a, d, cc);
                }
            }
            GeometryKit.RecalculateNormals(part);
        }
    }
}
