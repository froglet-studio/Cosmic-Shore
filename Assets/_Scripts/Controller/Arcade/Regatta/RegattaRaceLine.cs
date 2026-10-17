using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A closed racing line along the Regatta braid, OUTSIDE its core: the line that runs between two of the three rails
    /// at a chosen distance from the spine. Pure (positions in, positions out), so the tests hold it without a scene.
    ///
    /// <para><b>Why a hull flies here and not through the ring's centre.</b> The spine runs through every ring's centre
    /// and the three super-shielded rails are braided 22 u round it, so the centre is the middle of the braid, 22 u from
    /// all three rails at once. A big hull flying there scrapes rails, and a ram slows most hulls (Yash, 2026-10-17:
    /// "it's always better to play from the sides"). The line here sits on the bisector of the GAP between two rails: at
    /// radius r its nearest rail is sqrt(r² + 22² - 22 r) away, so it never crosses a rail. It follows the braid's twist,
    /// so it threads every ring inside the mouth. On the recorded rails a gap line at r = 60 (I1) stays 51 u from every
    /// rail, crosses each ring at 55% of the mouth and is 3% longer than the spine; the centre line is 22 u from the
    /// rails (2026-10-17, <c>REGATTA.md</c> §6).</para>
    ///
    /// <para>Built ONCE from the live rails: for each prism of rail <c>a</c>, the matching points of rails <c>b</c> and
    /// <c>c</c> (the lanes are laid in gate order from the same spine sample, so the match is a local search round the
    /// proportional index). Their centroid is the spine; the line point is the spine plus the a-b bisector times r.</para>
    /// </summary>
    public sealed class RegattaRaceLine
    {
        const int MatchWindow = 40;
        const int NearestWindow = 60;

        readonly Vector3[] _points;

        RegattaRaceLine(Vector3[] points) => _points = points;

        public int Count => _points.Length;
        public Vector3 this[int i] => _points[Wrap(i)];

        /// <summary>
        /// The line in the gap between rails <paramref name="a"/> and <paramref name="b"/> (the third rail is
        /// <paramref name="c"/>), <paramref name="radius"/> from the spine. Null when a rail has under three points.
        /// </summary>
        public static RegattaRaceLine BuildGapLine(IReadOnlyList<Vector3> a, IReadOnlyList<Vector3> b,
                                                   IReadOnlyList<Vector3> c, float radius)
        {
            if (a == null || b == null || c == null || a.Count < 3 || b.Count < 3 || c.Count < 3) return null;
            var points = new Vector3[a.Count];
            for (int i = 0; i < a.Count; i++)
            {
                Vector3 pa = a[i];
                Vector3 pb = b[Match(b, pa, i * b.Count / a.Count)];
                Vector3 pc = c[Match(c, pa, i * c.Count / a.Count)];
                Vector3 spine = (pa + pb + pc) / 3f;
                Vector3 gap = 0.5f * (pa + pb) - spine;
                float m = gap.magnitude;
                points[i] = m > 1e-4f ? spine + gap * (radius / m) : spine;
            }
            return new RegattaRaceLine(points);
        }

        /// <summary>The index of the line point nearest <paramref name="p"/>: a local search round
        /// <paramref name="hint"/> (a pilot moves a few points a frame), or the whole line when the hint is negative.</summary>
        public int Nearest(Vector3 p, int hint)
        {
            int n = _points.Length;
            int from = 0, to = n;
            if (hint >= 0) { from = hint - NearestWindow; to = hint + NearestWindow + 1; }
            int best = Wrap(hint < 0 ? 0 : hint);
            float bestD = float.MaxValue;
            for (int k = from; k < to; k++)
            {
                int i = Wrap(k);
                float d = (_points[i] - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>The point <paramref name="distance"/> along the line ahead of point <paramref name="from"/>
        /// (the line is closed, so ahead wraps).</summary>
        public Vector3 PointAhead(int from, float distance)
        {
            int n = _points.Length;
            int i = Wrap(from);
            float left = Mathf.Max(0f, distance);
            for (int step = 0; step < n; step++)
            {
                int j = Wrap(i + 1);
                float seg = (_points[j] - _points[i]).magnitude;
                if (seg >= left) return seg > 1e-5f ? Vector3.Lerp(_points[i], _points[j], left / seg) : _points[j];
                left -= seg;
                i = j;
            }
            return _points[i];
        }

        /// <summary>Distance from <paramref name="p"/> to the nearest of <paramref name="rail"/>'s points: what a
        /// test or a reader measures the line's clearance with.</summary>
        public static float DistanceTo(IReadOnlyList<Vector3> rail, Vector3 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < rail.Count; i++)
            {
                float d = (rail[i] - p).sqrMagnitude;
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }

        int Wrap(int i)
        {
            int n = _points.Length;
            return ((i % n) + n) % n;
        }

        static int Match(IReadOnlyList<Vector3> rail, Vector3 p, int guess)
        {
            int n = rail.Count;
            int best = 0;
            float bestD = float.MaxValue;
            for (int k = guess - MatchWindow; k <= guess + MatchWindow; k++)
            {
                int i = ((k % n) + n) % n;
                float d = (rail[i] - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }
    }
}
