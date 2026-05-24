using System;
using UnityEngine;
using CosmicShore.Splats;

namespace CosmicShore.Tools.SplatImport
{
    // Tiny synthetic generator that exercises decode + decimate without a real .ply.
    // Used by the menu item "Tools/Splats/Run Synthetic Pipeline Test" and by edit-mode tests.
    public static class SplatSyntheticTest
    {
        public struct GroundTruth
        {
            public Vector3 expectedScale;
            public Color expectedColor;
            public float expectedOpacity;
            public Quaternion expectedRotation;
        }

        public static (SplatPlyImporter.RawSplat[] raw, GroundTruth[] truth) MakeSyntheticSplats(int count)
        {
            var rng = new System.Random(31337);
            var raw = new SplatPlyImporter.RawSplat[count];
            var truth = new GroundTruth[count];

            for (int i = 0; i < count; i++)
            {
                // Random position in [-1, 1]^3.
                Vector3 pos = new Vector3(
                    (float)rng.NextDouble() * 2f - 1f,
                    (float)rng.NextDouble() * 2f - 1f,
                    (float)rng.NextDouble() * 2f - 1f);

                // Anisotropic scale: long X, medium Y, thin Z.
                Vector3 trueScale = new Vector3(0.4f + 0.2f * (float)rng.NextDouble(), 0.1f, 0.02f);
                Vector3 scaleRaw = new Vector3(Mathf.Log(trueScale.x), Mathf.Log(trueScale.y), Mathf.Log(trueScale.z));

                // Opacity ~0.8 from logit.
                float trueOpacity = 0.8f;
                float opacityRaw = Mathf.Log(trueOpacity / (1f - trueOpacity)); // ~1.386

                // Magenta-ish color (R=0.9, G=0.2, B=0.7).
                Color trueColor = new Color(0.9f, 0.2f, 0.7f, 1f);
                Vector3 fDc = new Vector3(
                    (trueColor.r - 0.5f) / SplatDecimator.SH_C0,
                    (trueColor.g - 0.5f) / SplatDecimator.SH_C0,
                    (trueColor.b - 0.5f) / SplatDecimator.SH_C0);

                // Rotation: 30deg around Y.
                Quaternion trueRot = Quaternion.AngleAxis(30f, Vector3.up);
                // Encode as (w, x, y, z) - 3DGS convention.
                Vector4 rotRaw = new Vector4(trueRot.w, trueRot.x, trueRot.y, trueRot.z);

                raw[i] = new SplatPlyImporter.RawSplat
                {
                    position = pos,
                    scaleRaw = scaleRaw,
                    rotRaw = rotRaw,
                    fDc = fDc,
                    opacityRaw = opacityRaw,
                    hasRotation = true,
                    hasColor = true,
                    hasOpacity = true,
                    hasScale = true,
                };
                truth[i] = new GroundTruth
                {
                    expectedScale = trueScale,
                    expectedColor = trueColor,
                    expectedOpacity = trueOpacity,
                    expectedRotation = trueRot,
                };
            }
            return (raw, truth);
        }

        public static void AssertDecodedReasonable(SplatPoint[] points)
        {
            if (points == null || points.Length == 0)
            {
                Debug.LogWarning("[SplatSyntheticTest] No points to validate.");
                return;
            }
            int badColor = 0, badOpacity = 0, badScale = 0;
            float expectedR = 0.9f, expectedG = 0.2f, expectedB = 0.7f;
            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i];
                if (Mathf.Abs(p.color.r - expectedR) > 0.02f || Mathf.Abs(p.color.g - expectedG) > 0.02f || Mathf.Abs(p.color.b - expectedB) > 0.02f)
                    badColor++;
                if (Mathf.Abs(p.opacity - 0.8f) > 0.02f)
                    badOpacity++;
                if (p.scale.x < p.scale.y || p.scale.y < p.scale.z) // expected anisotropy: x > y > z
                    badScale++;
            }
            if (badColor == 0 && badOpacity == 0 && badScale == 0)
                Debug.Log($"[SplatSyntheticTest] Decode + decimate round-trip looks correct on {points.Length} points.");
            else
                Debug.LogWarning($"[SplatSyntheticTest] Issues found — badColor={badColor}, badOpacity={badOpacity}, badScale={badScale} (of {points.Length}).");
        }
    }
}
