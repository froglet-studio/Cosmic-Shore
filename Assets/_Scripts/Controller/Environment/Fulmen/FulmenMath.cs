using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// FULMEN's arithmetic (the Charge fundamental, Yash 2026-10-29; Time has the Soliton): a seeded, deterministic 3D
    /// branching thunderbolt grown from a centre out to a range sphere. Pure: no MonoBehaviour, no scene, no allocation.
    /// The caller owns the <see cref="Segment"/> buffer, so the same buffer is reused bolt after bolt.
    ///
    /// <para><b>Shape.</b> <see cref="Settings.Roots"/> streams leave the centre in seeded directions. Each segment is
    /// followed by a continuation (its stream walks on, jittered, leaning outward) until the stream reaches the range sphere,
    /// where its last segment is clipped onto the sphere. A segment may also fork a new stream one depth deeper. Caps are
    /// hard: <see cref="Settings.MaxSegments"/> (also bounded by the buffer), <see cref="Settings.MaxBranches"/> streams
    /// (roots included) and <see cref="Settings.MaxDepth"/>.</para>
    ///
    /// <para><b>Order.</b> Segments are written breadth-first, so a parent's index is always below its child's and a
    /// child starts exactly where its parent ends. <see cref="Segment.StartDistance"/> / <see cref="Segment.EndDistance"/>
    /// are path length from the centre: a bolt grows by revealing segments as a front distance passes them.</para>
    ///
    /// <para><b>Determinism.</b> Same seed, settings and origin give the same bolt (xorshift, no UnityEngine.Random). Floats,
    /// so peers on different CPUs may differ in the last bits; publish the seed and let each peer grow the look, but keep
    /// damage authoritative.</para>
    /// </summary>
    public static class FulmenMath
    {
        /// <summary>One straight piece of a bolt.</summary>
        public struct Segment
        {
            public Vector3 Start, End;
            /// <summary>Path length from the bolt's centre to <see cref="Start"/> / <see cref="End"/>.</summary>
            public float StartDistance, EndDistance;
            /// <summary>0 for a root stream, +1 per fork.</summary>
            public int Depth;
            /// <summary>Index of the segment this one starts from; -1 for a root's first segment.</summary>
            public int Parent;
            /// <summary>Which stream (0..branches-1) this segment belongs to.</summary>
            public int Branch;
        }

        /// <summary>A bolt's shape and caps.</summary>
        public struct Settings
        {
            /// <summary>Radius of the range sphere round the origin; no point of the bolt lies outside it.</summary>
            public float Range;
            /// <summary>Streams leaving the centre.</summary>
            public int Roots;
            /// <summary>Hard cap on segments (the buffer's length caps it too).</summary>
            public int MaxSegments;
            /// <summary>Hard cap on streams, roots included.</summary>
            public int MaxBranches;
            /// <summary>Deepest fork allowed (0: roots only).</summary>
            public int MaxDepth;
            /// <summary>Segment length as a fraction of <see cref="Range"/>, min and max.</summary>
            public float SegmentMin, SegmentMax;
            /// <summary>How hard each segment's heading wanders (0 straight, 1 wild).</summary>
            public float Jitter;
            /// <summary>Pull of each heading toward straight out from the origin.</summary>
            public float Outward;
            /// <summary>Chance (0..1) a segment forks a new stream.</summary>
            public float ForkChance;
            /// <summary>Each fork's segments are this much shorter (per depth), so children stay finer.</summary>
            public float ForkShrink;

            public static Settings Default(float range) => new()
            {
                Range = range, Roots = 5, MaxSegments = 160, MaxBranches = 24, MaxDepth = 2,
                SegmentMin = 0.06f, SegmentMax = 0.14f, Jitter = 0.55f, Outward = 0.35f, ForkChance = 0.18f,
                ForkShrink = 0.7f,
            };
        }

        /// <summary>A tiny seeded xorshift32 (deterministic, allocation-free).</summary>
        public struct Rng
        {
            uint _s;
            public Rng(int seed) { _s = (uint)seed * 2654435761u ^ 0x9E3779B9u; if (_s == 0) _s = 1; }
            public uint Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return _s; }
            /// <summary>[0, 1).</summary>
            public float Value() => (Next() >> 8) * (1f / 16777216f);
            public float Range(float a, float b) => a + (b - a) * Value();
            public Vector3 OnUnitSphere()
            {
                float z = Range(-1f, 1f), phi = Range(0f, Mathf.PI * 2f), r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                return new Vector3(r * Mathf.Cos(phi), r * Mathf.Sin(phi), z);
            }
        }

        const float MinPiece = 1e-3f;

        /// <summary>
        /// Grows one bolt from <paramref name="origin"/> into <paramref name="buffer"/> and returns how many segments it
        /// wrote (never more than <c>min(MaxSegments, buffer.Length)</c>). Allocates nothing.
        /// </summary>
        public static int Generate(in Settings s, int seed, Vector3 origin, Segment[] buffer)
        {
            if (buffer == null || s.Range <= 0f) return 0;
            int cap = Mathf.Min(s.MaxSegments, buffer.Length);
            int maxBranches = Mathf.Max(0, s.MaxBranches);
            if (cap <= 0 || maxBranches == 0) return 0;

            var rng = new Rng(seed);
            int count = 0, branches = 0;
            float rangeSq = s.Range * s.Range;

            int roots = Mathf.Min(Mathf.Max(0, s.Roots), maxBranches);
            for (int r = 0; r < roots && count < cap; r++)
                if (TryPlace(in s, ref rng, origin, rangeSq, origin, rng.OnUnitSphere(), 0f, 0, -1, branches, buffer, ref count))
                    branches++;

            // breadth-first: each written segment may continue its stream and may fork one
            for (int i = 0; i < count && count < cap; i++)
            {
                var seg = buffer[i];
                var to = seg.End - origin;
                if (to.sqrMagnitude >= rangeSq * 0.9999f) continue;   // reached the sphere: the stream ends

                var dir = (seg.End - seg.Start).normalized;
                TryPlace(in s, ref rng, origin, rangeSq, seg.End, Wander(in s, ref rng, dir, to), seg.EndDistance,
                    seg.Depth, i, seg.Branch, buffer, ref count);

                if (count < cap && branches < maxBranches && seg.Depth < s.MaxDepth && rng.Value() < s.ForkChance)
                {
                    var forkDir = (dir + rng.OnUnitSphere() * 1.2f).normalized;
                    if (TryPlace(in s, ref rng, origin, rangeSq, seg.End, forkDir, seg.EndDistance, seg.Depth + 1, i,
                            branches, buffer, ref count))
                        branches++;
                }
            }
            return count;
        }

        static Vector3 Wander(in Settings s, ref Rng rng, Vector3 dir, Vector3 fromOrigin)
        {
            var outward = fromOrigin.sqrMagnitude > 1e-8f ? fromOrigin.normalized : dir;
            var d = dir + rng.OnUnitSphere() * s.Jitter + outward * s.Outward;
            return d.sqrMagnitude > 1e-8f ? d.normalized : outward;
        }

        /// <summary>Writes one segment from <paramref name="start"/> heading <paramref name="dir"/>, clipped to the sphere.</summary>
        static bool TryPlace(in Settings s, ref Rng rng, Vector3 origin, float rangeSq, Vector3 start, Vector3 dir,
            float startDistance, int depth, int parent, int branch, Segment[] buffer, ref int count)
        {
            float shrink = Mathf.Pow(Mathf.Clamp(s.ForkShrink, 0.05f, 1f), depth);
            float len = rng.Range(s.SegmentMin, s.SegmentMax) * s.Range * shrink;
            if (len < MinPiece) return false;

            // clip to the range sphere: the t >= 0 with |start - origin + dir t| = Range (start is inside)
            var p = start - origin;
            float b = Vector3.Dot(p, dir), c = p.sqrMagnitude - rangeSq;
            float disc = b * b - c;
            float tExit = disc > 0f ? -b + Mathf.Sqrt(disc) : 0f;
            if (tExit < len) len = tExit;
            if (len < MinPiece) return false;

            buffer[count++] = new Segment
            {
                Start = start, End = start + dir * len, StartDistance = startDistance, EndDistance = startDistance + len,
                Depth = depth, Parent = parent, Branch = branch,
            };
            return true;
        }
    }
}
