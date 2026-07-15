using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Builds a MINIATURE of a painting - the painting's own strokes, normalized to a small radius -
    /// so a Connect-the-Dots station IS a tiny spinning preview of the masterpiece it offers, not an
    /// anonymous glowing ball.
    ///
    /// Budgeted for sixteen stations on screen: the longest ~24 strokes, each decimated to ≤14
    /// points, drawn as thin domain-tinted LineRenderers under one child that idles in a slow spin.
    /// </summary>
    public static class MiniaturePaintingBuilder
    {
        const int MaxStrokes = 24;
        const int MaxPointsPerStroke = 14;

        /// <summary>
        /// Add a miniature of <paramref name="painting"/> under <paramref name="parent"/>, fitted to
        /// <paramref name="radius"/>. Returns false (no children added) when the painting has no
        /// drawable strokes - callers keep their sphere fallback.
        /// </summary>
        public static bool TryBuild(Transform parent, PaintingDefinitionSO painting, float radius,
            ToyContext context)
        {
            if (!painting) return false;
            painting.EnsureStrokes();
            var strokes = painting.Strokes;
            if (strokes == null || strokes.Count == 0) return false;

            Bounds b = painting.LocalBounds;
            if (b.size.magnitude < 1e-3f) return false;
            float scale = radius * 2f / Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            Vector3 center = b.center;

            // The longest strokes carry the silhouette - take those, keep author order otherwise.
            // Lengths are computed once up front: a comparator that re-walks the polylines pays
            // O(n log n) full-stroke walks on the toybox-spawn frame.
            var lengths = new float[strokes.Count];
            for (int i = 0; i < strokes.Count; i++)
                lengths[i] = PaintingPresetLibrary.StrokeLength(strokes[i]);
            var order = new List<int>();
            for (int i = 0; i < strokes.Count; i++) order.Add(i);
            order.Sort((x, y) => lengths[y].CompareTo(lengths[x]));
            if (order.Count > MaxStrokes) order.RemoveRange(MaxStrokes, order.Count - MaxStrokes);
            order.Sort(); // back to author order so the mini reads bottom-up like the real thing

            var mini = new GameObject("Miniature");
            mini.transform.SetParent(parent, false);

            foreach (int idx in order)
            {
                var stroke = strokes[idx];
                var pts = Decimate(stroke.points, MaxPointsPerStroke);
                var lr = ToyFactory.CreateLine($"Mini_{idx}", mini.transform, radius * 0.045f, false);
                Color c = ToyFactory.DomainAccentColor(context, stroke.domain);
                c.a = 0.95f;
                lr.startColor = lr.endColor = c;
                lr.positionCount = pts.Count;
                for (int p = 0; p < pts.Count; p++)
                    lr.SetPosition(p, (pts[p] - center) * scale);
            }

            // Slow turntable about the monument's vertical - a tiny spinning masterpiece.
            mini.AddComponent<ToyIdleSpin>().Configure(Vector3.up, 18f);
            return true;
        }

        static List<Vector3> Decimate(List<Vector3> pts, int cap)
        {
            if (pts.Count <= cap) return pts;
            var outPts = new List<Vector3>(cap);
            for (int i = 0; i < cap; i++)
                outPts.Add(pts[Mathf.RoundToInt(i * (pts.Count - 1) / (float)(cap - 1))]);
            return outPts;
        }
    }
}
