using System;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Splats;

namespace CosmicShore.Tools.SplatImport
{
    public enum SplatCropMode
    {
        None = 0,
        CenteredFraction = 1,
        ExplicitAABB = 2,
    }

    [Serializable]
    public struct SplatDecimateSettings
    {
        public SplatCropMode cropMode;
        [Range(0.01f, 1f)] public float cropFraction;
        public Vector3 cropMin;
        public Vector3 cropMax;
        [Min(1)] public int maxPrisms;
        [Min(0.0001f)] public float voxelSize;
        [Range(0f, 1f)] public float minOpacity;
        public bool flipHandedness; // negate Z on positions/quaternions to convert RH (3DGS) to Unity LH Y-up

        public static SplatDecimateSettings Default => new SplatDecimateSettings
        {
            cropMode = SplatCropMode.None,
            cropFraction = 1f,
            cropMin = new Vector3(-5f, -5f, -5f),
            cropMax = new Vector3( 5f,  5f,  5f),
            maxPrisms = SplatPrismSet.MaxPrisms,
            voxelSize = 0.05f,
            minOpacity = 0f,
            flipHandedness = false,
        };
    }

    public class DecimateReport
    {
        public int parsed;
        public int afterOpacityFilter;
        public int afterCrop;
        public int afterVoxel;
        public int finalCount;
        public Vector3 boundsMin;
        public Vector3 boundsMax;
        public Vector3 cropMin;
        public Vector3 cropMax;
        public string warnings;

        public string ToLogString()
        {
            return $"Parsed: {parsed}  ->  OpacityFilter: {afterOpacityFilter}  ->  Crop: {afterCrop}  ->  Voxel: {afterVoxel}  ->  Final: {finalCount}\n" +
                   $"  source bounds: {boundsMin} .. {boundsMax}\n" +
                   $"  crop bounds:   {cropMin} .. {cropMax}";
        }
    }

    public static class SplatDecimator
    {
        // 0th-order SH band constant. C0 = 1 / (2 * sqrt(pi))
        public const float SH_C0 = 0.2820947917738781f;

        public static SplatPoint Decode(in SplatPlyImporter.RawSplat raw, bool flipHandedness)
        {
            var pos = raw.position;
            Vector3 scl;
            Quaternion rot;
            Color col;
            float opacity;

            if (raw.hasScale)
            {
                // Stored log-encoded — undo with exp.
                scl = new Vector3(Mathf.Exp(raw.scaleRaw.x), Mathf.Exp(raw.scaleRaw.y), Mathf.Exp(raw.scaleRaw.z));
            }
            else
            {
                scl = Vector3.one;
            }

            if (raw.hasRotation)
            {
                // 3DGS convention: (w, x, y, z), w-first.
                var q = raw.rotRaw;
                float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
                if (mag > 1e-8f)
                {
                    float inv = 1f / mag;
                    rot = new Quaternion(q.y * inv, q.z * inv, q.w * inv, q.x * inv);
                }
                else
                {
                    rot = Quaternion.identity;
                }
            }
            else
            {
                rot = Quaternion.identity;
            }

            if (raw.hasColor)
            {
                float r = 0.5f + SH_C0 * raw.fDc.x;
                float g = 0.5f + SH_C0 * raw.fDc.y;
                float b = 0.5f + SH_C0 * raw.fDc.z;
                col = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
            }
            else
            {
                col = Color.white;
            }

            if (raw.hasOpacity)
                opacity = 1f / (1f + Mathf.Exp(-raw.opacityRaw));
            else
                opacity = 1f;

            if (flipHandedness)
            {
                pos.z = -pos.z;
                // Mirror quaternion across the XY-plane (Z-flip): (x, y, z, w) -> (-x, -y, z, w).
                rot = new Quaternion(-rot.x, -rot.y, rot.z, rot.w);
            }

            return new SplatPoint
            {
                position = pos,
                scale = scl,
                rotation = rot,
                color = col,
                opacity = opacity,
            };
        }

        public static SplatPoint[] DecimateToPoints(SplatPlyImporter.RawSplat[] raw, SplatDecimateSettings settings, out DecimateReport report)
        {
            report = new DecimateReport { parsed = raw?.Length ?? 0 };
            if (raw == null || raw.Length == 0)
            {
                report.finalCount = 0;
                return Array.Empty<SplatPoint>();
            }

            // 1) Decode + opacity filter, pass through min/max bounds.
            var decoded = new List<SplatPoint>(raw.Length);
            Vector3 bMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 bMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < raw.Length; i++)
            {
                var p = Decode(raw[i], settings.flipHandedness);
                if (p.opacity < settings.minOpacity) continue;
                decoded.Add(p);

                if (p.position.x < bMin.x) bMin.x = p.position.x;
                if (p.position.y < bMin.y) bMin.y = p.position.y;
                if (p.position.z < bMin.z) bMin.z = p.position.z;
                if (p.position.x > bMax.x) bMax.x = p.position.x;
                if (p.position.y > bMax.y) bMax.y = p.position.y;
                if (p.position.z > bMax.z) bMax.z = p.position.z;
            }
            report.afterOpacityFilter = decoded.Count;
            report.boundsMin = bMin;
            report.boundsMax = bMax;

            // 2) Crop.
            Vector3 cMin, cMax;
            if (settings.cropMode == SplatCropMode.None)
            {
                cMin = bMin;
                cMax = bMax;
            }
            else if (settings.cropMode == SplatCropMode.CenteredFraction)
            {
                Vector3 center = (bMin + bMax) * 0.5f;
                Vector3 half = (bMax - bMin) * 0.5f * Mathf.Clamp01(settings.cropFraction);
                cMin = center - half;
                cMax = center + half;
            }
            else
            {
                cMin = Vector3.Min(settings.cropMin, settings.cropMax);
                cMax = Vector3.Max(settings.cropMin, settings.cropMax);
            }
            report.cropMin = cMin;
            report.cropMax = cMax;

            var cropped = new List<SplatPoint>(decoded.Count);
            for (int i = 0; i < decoded.Count; i++)
            {
                var pos = decoded[i].position;
                if (pos.x < cMin.x || pos.x > cMax.x) continue;
                if (pos.y < cMin.y || pos.y > cMax.y) continue;
                if (pos.z < cMin.z || pos.z > cMax.z) continue;
                cropped.Add(decoded[i]);
            }
            report.afterCrop = cropped.Count;

            // 3) Voxel downsample. Pick the highest-importance splat per voxel.
            float voxel = Mathf.Max(0.0001f, settings.voxelSize);
            float invVoxel = 1f / voxel;
            var cells = new Dictionary<long, int>(); // cellKey -> index into cropped
            var cellImportance = new Dictionary<long, float>();
            for (int i = 0; i < cropped.Count; i++)
            {
                var p = cropped[i];
                long key = VoxelKey(p.position, invVoxel);
                float imp = Importance(p);
                if (!cellImportance.TryGetValue(key, out float existing) || imp > existing)
                {
                    cellImportance[key] = imp;
                    cells[key] = i;
                }
            }

            var survivors = new List<SplatPoint>(cells.Count);
            foreach (var kv in cells) survivors.Add(cropped[kv.Value]);
            report.afterVoxel = survivors.Count;

            // 4) Importance cap.
            int cap = Mathf.Min(settings.maxPrisms, SplatPrismSet.MaxPrisms);
            SplatPoint[] final;
            if (survivors.Count <= cap)
            {
                final = survivors.ToArray();
            }
            else
            {
                survivors.Sort((a, b) => Importance(b).CompareTo(Importance(a)));
                final = new SplatPoint[cap];
                for (int i = 0; i < cap; i++) final[i] = survivors[i];
            }
            report.finalCount = final.Length;

            if (final.Length > SplatPrismSet.MaxPrisms)
                throw new InvalidOperationException($"Decimator produced {final.Length} points, exceeding hard cap {SplatPrismSet.MaxPrisms}.");

            return final;
        }

        private static long VoxelKey(Vector3 pos, float invVoxel)
        {
            // Bucket into a 21-bit signed integer per axis (range ~ +/-1M cells, ample for any realistic cloud at sane voxel sizes).
            long x = (long)Mathf.Floor(pos.x * invVoxel) + (1L << 20);
            long y = (long)Mathf.Floor(pos.y * invVoxel) + (1L << 20);
            long z = (long)Mathf.Floor(pos.z * invVoxel) + (1L << 20);
            const long mask = (1L << 21) - 1;
            x &= mask; y &= mask; z &= mask;
            return (x << 42) | (y << 21) | z;
        }

        public static float Importance(in SplatPoint p)
        {
            // Geometric mean of decoded scale, weighted by opacity, with a mild saturation kicker.
            float sx = Mathf.Max(p.scale.x, 1e-6f);
            float sy = Mathf.Max(p.scale.y, 1e-6f);
            float sz = Mathf.Max(p.scale.z, 1e-6f);
            float sizeTerm = Mathf.Pow(sx * sy * sz, 1f / 3f);
            float maxC = Mathf.Max(p.color.r, Mathf.Max(p.color.g, p.color.b));
            float minC = Mathf.Min(p.color.r, Mathf.Min(p.color.g, p.color.b));
            float sat = maxC > 1e-6f ? (maxC - minC) / maxC : 0f;
            float satBoost = 1f + 0.25f * sat;
            return p.opacity * sizeTerm * satBoost;
        }
    }
}
