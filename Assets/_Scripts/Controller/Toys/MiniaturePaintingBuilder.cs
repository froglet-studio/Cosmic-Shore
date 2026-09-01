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
    /// <b>Fidelity scales with the icon.</b> A masterpiece is 20-60 strokes. On a big gallery
    /// station there is room for nearly all of them and the painting should read as ITSELF; on a
    /// 7-unit emblem satellite the same strokes cross-hatch into a fuzzy ball that looks identical
    /// for every painting. So the stroke budget is derived from the radius the icon is being fitted
    /// to - a station gets near-full fidelity, an emblem satellite gets a signature - and the line
    /// width comes down as the budget goes up so a dense icon doesn't blob.
    ///
    /// <b>Selection is by COVERAGE, not by length.</b> Longest-first clustered: on a radially
    /// symmetric painting (the Rose's four petal whorls, the Lotus, the Peacock) it picked several
    /// strokes off one side and the icon showed half a flower. Under-budget icons now pick strokes
    /// that are spread across the painting's own bounds - farthest-point dispersion, seeded with
    /// the longest stroke of each domain so the colour identity survives - so a partial sample
    /// still reads as the whole shape.
    /// </summary>
    public static class MiniaturePaintingBuilder
    {
        // Strokes per world unit of icon radius, and the clamps. At the gallery station radius
        // (44) this is 48 - a Rose (~62 strokes) shows all four whorls; at an emblem satellite
        // (7.5) it is 8, a legible signature.
        const float StrokesPerRadius = 1.1f;
        const int MinStrokes = 5;
        const int MaxStrokes = 64;

        // Points per stroke follow the same logic: enough to hold a curve at icon size.
        const float PointsPerRadius = 0.55f;
        const int MinPointsPerStroke = 8;
        const int MaxPointsPerStroke = 26;

        // Thick lines read at signature density; at full density they would merge into a blob.
        const float SparseLineWidth = 0.075f;
        const float DenseLineWidth = 0.042f;
        const int DenseStrokeCount = 16;

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

            int strokeBudget = Mathf.Clamp(Mathf.RoundToInt(radius * StrokesPerRadius), MinStrokes, MaxStrokes);
            int pointBudget = Mathf.Clamp(Mathf.RoundToInt(radius * PointsPerRadius),
                MinPointsPerStroke, MaxPointsPerStroke);
            var order = SelectSignatureStrokes(strokes, strokeBudget);

            // Frame what is actually drawn: an under-budget sample fitted to the FULL painting's
            // bounds would rattle around in a corner of the icon.
            if (!TryGetBounds(strokes, order, out Bounds b)) return false;
            float scale = radius * 2f / Mathf.Max(b.size.x, Mathf.Max(b.size.y, Mathf.Max(b.size.z, 1e-3f)));
            Vector3 center = b.center;

            var mini = new GameObject("Miniature");
            mini.transform.SetParent(parent, false);

            foreach (int idx in order)
            {
                var stroke = strokes[idx];
                var pts = Decimate(stroke.points, pointBudget);
                // Bold at signature density (a few strokes must not read as scratches), thinner as
                // the icon fills in (a dense icon of fat lines is a blob).
                float width = radius * (order.Count >= DenseStrokeCount ? DenseLineWidth : SparseLineWidth);
                var lr = ToyFactory.CreateLine($"Mini_{idx}", mini.transform, width, false);
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
        /// The strokes that identify this painting, within <paramref name="budget"/>.
        ///
        /// Everything fits → take everything (a big station shows the real painting; there is no
        /// reason to editorialise). Otherwise: seed with the longest stroke of EACH domain (colour
        /// identity), then grow the set by <b>farthest-point dispersion</b> - repeatedly take the
        /// stroke whose centroid is furthest from everything already chosen, nudged by length so
        /// that among equally-distant candidates the more substantial one wins.
        ///
        /// Dispersion is the whole point. Longest-first is a trap on any radially symmetric
        /// painting: the petals of one whorl are all the same length, so it took several from one
        /// side and the icon showed half a flower. Returned in author order so the icon draws
        /// bottom-up like the real canvas.
        /// </summary>
        static List<int> SelectSignatureStrokes(IReadOnlyList<PaintingStroke> strokes, int budget)
        {
            int count = strokes.Count;
            var chosen = new List<int>(Mathf.Min(budget, count));

            if (count <= budget)
            {
                for (int i = 0; i < count; i++) chosen.Add(i);
                return chosen;
            }

            // Measured once up front - a comparator that re-walks the polylines would pay O(n log n)
            // full-stroke walks.
            var lengths = new float[count];
            var centroids = new Vector3[count];
            float longest = 0f;
            for (int i = 0; i < count; i++)
            {
                lengths[i] = PaintingPresetLibrary.StrokeLength(strokes[i]);
                centroids[i] = Centroid(strokes[i]);
                if (lengths[i] > longest) longest = lengths[i];
            }
            if (longest <= 0f) longest = 1f;

            // Seed: the longest stroke of each domain present.
            var taken = new bool[count];
            var domainsSeen = new HashSet<Domains>();
            var byLength = new List<int>(count);
            for (int i = 0; i < count; i++) byLength.Add(i);
            byLength.Sort((x, y) => lengths[y].CompareTo(lengths[x]));
            foreach (int i in byLength)
            {
                if (chosen.Count >= budget) break;
                if (!domainsSeen.Add(strokes[i].domain)) continue;
                chosen.Add(i);
                taken[i] = true;
            }

            // Grow by dispersion. O(budget x count) on <=64 x ~260 - trivial, and it runs on a
            // streamed frame, never on the toybox-spawn frame.
            var minDistance = new float[count];
            for (int i = 0; i < count; i++) minDistance[i] = float.MaxValue;
            foreach (int c in chosen) UpdateDistances(centroids, minDistance, centroids[c]);

            while (chosen.Count < budget)
            {
                int best = -1;
                float bestScore = -1f;
                for (int i = 0; i < count; i++)
                {
                    if (taken[i]) continue;
                    // Distance dominates; length breaks ties between equally-isolated candidates.
                    float score = minDistance[i] * (0.5f + 0.5f * lengths[i] / longest);
                    if (score <= bestScore) continue;
                    bestScore = score;
                    best = i;
                }
                if (best < 0) break;

                chosen.Add(best);
                taken[best] = true;
                UpdateDistances(centroids, minDistance, centroids[best]);
            }

            chosen.Sort();
            return chosen;
        }

        static void UpdateDistances(Vector3[] centroids, float[] minDistance, Vector3 added)
        {
            for (int i = 0; i < centroids.Length; i++)
            {
                float d = (centroids[i] - added).sqrMagnitude;
                if (d < minDistance[i]) minDistance[i] = d;
            }
        }

        static Vector3 Centroid(PaintingStroke stroke)
        {
            var pts = stroke?.points;
            if (pts == null || pts.Count == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            foreach (var p in pts) sum += p;
            return sum / pts.Count;
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
