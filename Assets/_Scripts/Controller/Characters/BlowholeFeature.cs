using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A cetacean blowhole: a raised crescent rim on the crown-back site; the dark aperture is
    /// painted by the texture at the same landmark. Skin slot, projected UVs. Embedded.
    /// </summary>
    public static class BlowholeFeature
    {
        const int ArcSteps = 14, TubeSegs = 8;

        public static void Generate(FeatureContext ctx)
        {
            var p = ctx.Params.Blowhole;
            var part = ctx.Begin("BlowholeRim", CharacterMaterialSlot.Skin, projectUvs: true);
            var rings = new List<IReadOnlyList<Vector3>>();
            var uvs = new List<IReadOnlyList<Vector2>>();
            for (int i = 0; i <= ArcSteps; i++)
            {
                float t = i / (float)ArcSteps;
                float a = Mathf.Lerp(-0.15f * Mathf.PI, 1.15f * Mathf.PI, t);   // a crescent open toward −y (the face)
                Vector3 c = new Vector3(Mathf.Cos(a) * p.Radius, Mathf.Sin(a) * p.Radius, -0.004f);
                Vector3 tangent = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f);
                GeometryKit.Frame(tangent, Vector3.forward, out var e1, out var e2);
                float tube = p.RimHeight * Mathf.Sin(Mathf.PI * Mathf.Clamp01(0.06f + 0.94f * t));
                var ring = new Vector3[TubeSegs];
                var uv = new Vector2[TubeSegs];
                for (int k = 0; k < TubeSegs; k++)
                {
                    float b = GeometryKit.Tau * k / TubeSegs;
                    ring[k] = c + (e1 * Mathf.Cos(b) * 1.6f + e2 * Mathf.Sin(b)) * tube;
                    uv[k] = new Vector2(k / (float)TubeSegs, t);
                }
                rings.Add(ring); uvs.Add(uv);
            }
            GeometryKit.Loft(part, rings, uvs, capTip: false, Vector3.zero, Vector2.zero);
            GeometryKit.RecalculateNormals(part);
        }
    }
}
