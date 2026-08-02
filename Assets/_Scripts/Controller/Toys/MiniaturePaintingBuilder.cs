using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Builds a MINIATURE of a painting - the painting's own strokes, normalized to a small radius -
    /// so a Connect-the-Dots station IS a tiny spinning preview of the masterpiece it offers, not an
    /// anonymous glowing ball.
    ///
    /// <b>Signature strokes, not the whole canvas.</b> A masterpiece is 20-60 strokes; drawn all at
    /// once at thumbnail size they cross-hatch into a fuzzy ball that reads the same for every
    /// painting - so the miniature keeps only a handful of SIGNATURE strokes (the longest, one per
    /// domain first so the painting's colour identity survives) and draws them thick enough to read
    /// as an icon. That is what lets a station identify itself without its text label, which is
    /// where the whole toybox is heading. Each stroke keeps enough points to hold its curvature -
    /// the shape of the signature is the whole point of showing it.
    /// </summary>
    public static class MiniaturePaintingBuilder
    {
        /// <summary>How many signature strokes an icon gets. Small on purpose - see the class doc.</summary>
        const int MaxStrokes = 5;
        const int MaxPointsPerStroke = 24;

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

            var order = SelectSignatureStrokes(strokes);

            // Frame the SIGNATURE, not the whole canvas: fitting the subset to the full painting's
            // bounds would leave a handful of strokes rattling around in a corner of the icon.
            if (!TryGetBounds(strokes, order, out Bounds b)) return false;
            float scale = radius * 2f / Mathf.Max(b.size.x, Mathf.Max(b.size.y, Mathf.Max(b.size.z, 1e-3f)));
            Vector3 center = b.center;

            var mini = new GameObject("Miniature");
            mini.transform.SetParent(parent, false);

            foreach (int idx in order)
            {
                var stroke = strokes[idx];
                var pts = Decimate(stroke.points, MaxPointsPerStroke);
                // Thick enough to read as an icon rather than a scratch: a handful of bold strokes
                // beats two dozen hairlines at thumbnail size.
                var lr = ToyFactory.CreateLine($"Mini_{idx}", mini.transform, radius * 0.075f, false);
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

        /// <summary>
        /// The strokes that identify this painting: the longest stroke of EACH domain first (so a
        /// two- or three-colour composition still reads as itself), then the longest remaining
        /// strokes until the budget is spent. Returned in author order, so the icon draws
        /// bottom-up like the real canvas. Lengths are measured once up front - a comparator that
        /// re-walks the polylines would pay O(n log n) full-stroke walks on the spawn frame.
        /// </summary>
        static List<int> SelectSignatureStrokes(IReadOnlyList<PaintingStroke> strokes)
        {
            var lengths = new float[strokes.Count];
            var byLength = new List<int>(strokes.Count);
            for (int i = 0; i < strokes.Count; i++)
            {
                lengths[i] = PaintingPresetLibrary.StrokeLength(strokes[i]);
                byLength.Add(i);
            }
            byLength.Sort((x, y) => lengths[y].CompareTo(lengths[x]));

            var chosen = new List<int>(MaxStrokes);
            var domainsSeen = new HashSet<Domains>();
            foreach (int i in byLength)
            {
                if (chosen.Count >= MaxStrokes) break;
                if (domainsSeen.Add(strokes[i].domain)) chosen.Add(i);
            }
            foreach (int i in byLength)
            {
                if (chosen.Count >= MaxStrokes) break;
                if (!chosen.Contains(i)) chosen.Add(i);
            }

            chosen.Sort();
            return chosen;
        }

        /// <summary>Bounds of just the chosen strokes' points. False when they carry no points.</summary>
        static bool TryGetBounds(IReadOnlyList<PaintingStroke> strokes, List<int> chosen, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (int idx in chosen)
            {
                var pts = strokes[idx]?.points;
                if (pts == null) continue;
                foreach (var p in pts)
                {
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return any && bounds.size.magnitude > 1e-3f;
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
