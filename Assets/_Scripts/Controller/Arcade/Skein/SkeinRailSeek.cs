using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Skein's free-flight RAIL SEEK: where a FREE AI Urchin flies to catch a rail that threads its next ring, when
    /// that beats flying straight at the ring. The <c>TryOwnLaneAhead</c> pattern of <c>RegattaController</c>, for a
    /// cable instead of closed lanes.
    ///
    /// <para>Without it nothing steers a free Urchin toward a rail: the gate aim flies at the ring, and the Urchin only
    /// rides what it happens to touch on the way (14% of the time at I1, 2% at I3-I4 in the model), so it misses the
    /// pinned rings above I1 and never finishes I3/I4. With it, and Regatta's commit-from-approach aim, every modelled
    /// race finishes (<c>Docs/Reports/SKEIN_URCHIN_AI.md</c>, "the aim what-if"). This is the model's seek line for line
    /// (<c>Tools/Build/skein_urchin_sim.py</c>: <c>seek_candidates</c> and <c>race(seek=True)</c>): pure, so the
    /// edit-mode tests pin it.</para>
    /// </summary>
    public static class SkeinRailSeek
    {
        /// <summary>A place on a rail from which riding FORWARD threads the ring: the rail, the prism, and the arc of
        /// rail ridden from that prism to the ring.</summary>
        public readonly struct Candidate
        {
            public readonly int Rail;
            public readonly int Prism;
            public readonly float ArcToRing;

            public Candidate(int rail, int prism, float arcToRing)
            {
                Rail = rail; Prism = prism; ArcToRing = arcToRing;
            }
        }

        /// <summary>Cumulative arc length along <paramref name="points"/>, from 0 at the first prism.</summary>
        public static float[] ArcLengths(Vector3[] points)
        {
            var arc = new float[points.Length];
            for (int k = 1; k < points.Length; k++)
                arc[k] = arc[k - 1] + Vector3.Distance(points[k - 1], points[k]);
            return arc;
        }

        /// <summary>
        /// Every prism (every <paramref name="stride"/>th) from which riding forward threads the ring at
        /// <paramref name="ring"/> within <paramref name="maxArc"/> of rail: a rail segment whose nearest point to the
        /// ring lies inside <paramref name="captureRadius"/> threads it. One entry per (rail, prism), with the shortest
        /// arc. The model's <c>seek_candidates</c>.
        /// </summary>
        public static List<Candidate> Candidates(IReadOnlyList<Vector3[]> rails, IReadOnlyList<float[]> arcs,
                                                 Vector3 ring, float captureRadius, float maxArc, int stride,
                                                 List<Candidate> into = null)
        {
            var output = into ?? new List<Candidate>();
            output.Clear();
            stride = Mathf.Max(1, stride);
            var best = new Dictionary<int, float>();
            for (int r = 0; r < rails.Count; r++)
            {
                var pts = rails[r];
                var arc = arcs[r];
                if (pts == null || pts.Length < 2) continue;
                best.Clear();
                for (int j = 0; j < pts.Length - 1; j++)
                {
                    Vector3 a = pts[j], b = pts[j + 1];
                    // A cheap reject on one axis before the segment test, as the model does.
                    if (Mathf.Abs(a.x - ring.x) > captureRadius + 20f && Mathf.Abs(b.x - ring.x) > captureRadius + 20f)
                        continue;
                    Vector3 q = a + (b - a) * SegmentParam(a, b, ring);
                    if ((q - ring).magnitude > captureRadius) continue;
                    for (int k = j; k >= 0 && arc[j] - arc[k] <= maxArc; k--)
                    {
                        if (k % stride != 0) continue;
                        float d = arc[j] - arc[k];
                        if (!best.TryGetValue(k, out float had) || d < had) best[k] = d;
                    }
                }
                foreach (var kv in best) output.Add(new Candidate(r, kv.Key, kv.Value));
            }
            return output;
        }

        /// <summary>The parameter (0-1) of the point of segment a-b nearest p.</summary>
        public static float SegmentParam(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float l2 = Vector3.Dot(ab, ab);
            return l2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2) : 0f;
        }

        /// <summary>A rail's heading at prism <paramref name="k"/>: the central difference in index order (the race
        /// direction), as <c>Trail.HeadingAt</c>.</summary>
        public static Vector3 Heading(Vector3[] pts, int k)
        {
            Vector3 a = pts[Mathf.Max(0, k - 1)];
            Vector3 b = pts[Mathf.Min(pts.Length - 1, k + 1)];
            return (b - a).normalized;
        }

        /// <summary>
        /// The seek point, or false to fly at the ring. A candidate costs the flight to it at
        /// <paramref name="flySpeed"/> plus the ride from it at <paramref name="rideSpeed"/>; it must beat
        /// <paramref name="gain"/> times the straight flight to the ring, and the pilot must come at it no more than
        /// acos(<paramref name="approachCos"/>) off the rail's own heading (it rides the way it arrives). The point
        /// flown to is <paramref name="aheadPrisms"/> past the candidate, so the pilot joins the rail going its way.
        /// <paramref name="excludeRail"/> (or -1) is the rail it just left, which the game will not re-latch yet.
        /// </summary>
        public static bool TryPick(IReadOnlyList<Candidate> candidates, IReadOnlyList<Vector3[]> rails, Vector3 self,
                                   Vector3 ring, float flySpeed, float rideSpeed, float gain, float approachCos,
                                   int aheadPrisms, int excludeRail, out Vector3 aim)
        {
            aim = default;
            float fly = Mathf.Max(1e-3f, flySpeed);
            float best = (ring - self).magnitude / fly * gain;
            bool found = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (c.Rail == excludeRail) continue;
                var pts = rails[c.Rail];
                Vector3 toP = pts[c.Prism] - self;
                float dp = toP.magnitude;
                if (dp < 1e-6f) continue;
                if (Vector3.Dot(Heading(pts, c.Prism), toP) < approachCos * dp) continue;
                float cost = dp / fly + c.ArcToRing / Mathf.Max(1e-3f, rideSpeed);
                if (cost >= best) continue;
                best = cost;
                aim = pts[Mathf.Min(pts.Length - 1, c.Prism + aheadPrisms)];
                found = true;
            }
            return found;
        }

        /// <summary>The rail with a prism within <paramref name="tolerance"/> of <paramref name="point"/>, or -1.</summary>
        public static int RailAt(IReadOnlyList<Vector3[]> rails, Vector3 point, float tolerance)
        {
            float bestSq = tolerance * tolerance;
            int found = -1;
            for (int r = 0; r < rails.Count; r++)
            {
                var pts = rails[r];
                if (pts == null) continue;
                for (int k = 0; k < pts.Length; k++)
                {
                    float d = (pts[k] - point).sqrMagnitude;
                    if (d <= bestSq) { bestSq = d; found = r; }
                }
            }
            return found;
        }
    }
}
