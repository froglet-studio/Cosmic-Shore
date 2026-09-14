using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A beak: ONE loft from the muzzle site's seam ring to a tip, whose cross-section morphs
    /// from the unit circle (the contract) into a keeled, gape-lined beak profile over the first
    /// third of its length. Corvidae and Testudines share this generator and differ in DATA —
    /// long/straight/heavy against short/hooked/sharp — which is the "are the signatures thick
    /// enough to tell two beaks apart" question the spike exists to ask.
    /// SEAM feature: emitted in RING units (site radius = 1), +z outward along the site normal.
    /// This is the file for "the beak reads wrong".
    /// </summary>
    public static class BeakFeature
    {
        const int Sections = 14;

        public static void Generate(FeatureContext ctx)
        {
            var p = ctx.Params.Beak;
            float unit = Mathf.Max(1e-3f, ctx.Site.Radius);
            float L = p.Length / unit;                       // length in ring units
            var part = ctx.Begin("Beak", CharacterMaterialSlot.Keratin);
            part.SeamRingCount = AttachmentContract.RingCount;

            var rings = new List<IReadOnlyList<Vector3>>();
            var uvs = new List<IReadOnlyList<Vector2>>();
            int N = AttachmentContract.RingCount;
            for (int i = 0; i < Sections; i++)
            {
                float s = i / (float)Sections;                  // 0 at the seam … <1 before the tip
                float morph = GeometryKit.Smooth(s / 0.35f);    // circle → beak profile
                float taper = GeometryKit.SafePow(1f - s, 0.75f + 0.5f * p.TipSharpness);
                float droop = -p.Hook * L * s * s * 0.55f;      // hook: the spine bends down
                Vector3 c = new Vector3(0f, droop, L * s);
                var ring = new Vector3[N];
                var uv = new Vector2[N];
                for (int k = 0; k < N; k++)
                {
                    float a = GeometryKit.Tau * k / N;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    Vector3 circle = new Vector3(ca, sa, 0f);
                    // Beak profile: wider than tall, an upper culmen ridge, a flatter underside.
                    float hx = p.BaseWidth;
                    float hy = p.BaseHeight;
                    float yy = sa >= 0f ? sa * (1f + p.Culmen * GeometryKit.SafePow(Mathf.Max(0f, sa), 2f)) : sa * 0.8f;
                    // Sharpen toward the gape: the side of a beak is a ridge, not a cylinder.
                    float side = GeometryKit.SafePow(Mathf.Abs(ca), 0.85f) * Mathf.Sign(ca);
                    Vector3 profile = new Vector3(hx * side, hy * yy, 0f);
                    Vector3 sec = Vector3.Lerp(circle, profile, morph) * taper;
                    ring[k] = c + sec;
                    uv[k] = new Vector2(k / (float)N, s);
                }
                rings.Add(ring);
                uvs.Add(uv);
            }
            Vector3 tip = new Vector3(0f, -p.Hook * L * 0.55f - p.Hook * 0.12f * L, L);
            GeometryKit.Loft(part, rings, uvs, capTip: true, tip, new Vector2(0.5f, 1f));
            GeometryKit.RecalculateNormals(part);
            // The ring itself must sit exactly on the contract: assert the generator kept it.
            AttachmentContract.AssertSeamFeature(part, ctx.Site);
        }
    }
}
