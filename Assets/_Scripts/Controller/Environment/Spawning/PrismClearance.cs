using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// NO PRISM MAY INTERPENETRATE ANOTHER (<c>Docs/ECOSYSTEM.md §49.10</c>), held for every
    /// generated lay list rather than fitted one species at a time.
    ///
    /// <para>The rule used to be held only by the generators that had been fitted under it (the
    /// Garland cell, the Borromean and Schwarz P flora, the quasicrystal struts, the shields). The
    /// Spawnable shapes - the cell environments, the menu's freestyle worlds and the arcade arenas -
    /// had never been measured, and <c>Tools/Build/audit_prism_clipping.py</c> found 39 of the 54
    /// shipped prefabs clipping: trails whose pitch is shorter than their prisms, structures that
    /// cross with no clearance, joints where several trails share one point, and Spherene laying
    /// most of its prisms twice. A guarantee one generator can break is not one, so this is the
    /// one place it is made, after a generator has emitted and before anything is laid.</para>
    ///
    /// <para><b>The pass YIELDS, in lay order</b> (the Garland's shore, §48.10, made universal).
    /// Each prism is tested against the prisms already accepted with the 15-axis separating-axis
    /// test. A prism that clips is SHRUNK about its own centre to the largest size that clears -
    /// along its long axis first, which is what a trail whose pitch is short needs, then across
    /// its footprint, then uniformly, keeping whichever leaves the most volume. A prism no shrink
    /// can clear (a duplicate, a joint whose centres coincide) is CLEARED: its scale becomes zero
    /// and <see cref="PrismTrailBuilder"/> lays nothing there. Cleared lays keep their slot, so a
    /// generator that indexes its lay list by range (Skein's rails, Regatta's lanes, the
    /// Switchyard and Breakwater segments) is untouched.</para>
    ///
    /// <para><b>A gameplay prism never yields to a decorative one</b>: with a priority list,
    /// every prism of a higher priority is placed before any lower one, so a Danger or Shielded
    /// prism keeps its size and a Plain prism beside it gives way.</para>
    ///
    /// <para><b>The scale window.</b> A laid prism's scale is clamped per axis into its prefab's
    /// <c>PrismScaleAnimator</c> window, so the pass resolves the size that will be ON SCREEN
    /// (an authored 0.2 plate on a 0.5 floor is a 0.5 plate) and never shrinks an axis below the
    /// floor - a shrink the clamp would undo is not a fix.</para>
    ///
    /// <para><b>Determinism.</b> Clients build environments locally with no seed sync, so this is
    /// a pure function of the lay list: no randomness, a fixed visiting order, and a fixed number
    /// of bisection steps.</para>
    /// </summary>
    public static class PrismClearance
    {
        /// <summary>A resolved prism clears every accepted neighbour by this fraction of the thinner
        /// prism's thinnest half-extent, so float noise between platforms cannot re-open a pair.</summary>
        public const float ClearanceFraction = 0.01f;

        /// <summary>A shrink that keeps less than this fraction of the prism's volume clears it
        /// instead: a sliver at a joint reads as a fault, not as a prism.</summary>
        public const float MinKeptVolume = 0.1f;

        const int BisectionSteps = 12;

        /// <summary>What a pass did, for the audit and for a verbose log line.</summary>
        public readonly struct Result
        {
            public readonly int Tested, Shrunk, Cleared;
            public Result(int tested, int shrunk, int cleared) { Tested = tested; Shrunk = shrunk; Cleared = cleared; }
        }

        /// <summary>True for a lay the pass cleared (scale zero): it is not laid.</summary>
        public static bool IsCleared(in SpawnPoint p) => p.Scale.x == 0f && p.Scale.y == 0f && p.Scale.z == 0f;

        struct Box
        {
            public Vector3 C, X, Y, Z;
            public Vector3 H; // half extents along X, Y, Z
            public float Radius => H.magnitude;
            public float MinHalf => Mathf.Min(H.x, Mathf.Min(H.y, H.z));
        }

        /// <summary>
        /// Resolve <paramref name="points"/> in place. <paramref name="minScale"/> /
        /// <paramref name="maxScale"/> are the laying prefab's per-axis scale window (null = the
        /// lay admits its authored scale). <paramref name="priority"/>, when given, is one value
        /// per point; higher values are placed first (ties in lay order).
        /// </summary>
        public static Result Resolve(SpawnPoint[] points, Vector3? minScale = null, Vector3? maxScale = null,
            int[] priority = null)
        {
            int n = points?.Length ?? 0;
            if (n < 2) return new Result(n, 0, 0);

            var boxes = new Box[n];
            for (int i = 0; i < n; i++)
            {
                var p = points[i];
                if (IsCleared(p)) continue; // a default Box is the cleared box: zero extents
                var s = Abs(p.Scale);
                if (minScale is { } lo && maxScale is { } hi) s = Clamp(s, lo, hi);
                else if (minScale is { } lo2) s = Vector3.Max(s, lo2);
                boxes[i] = new Box
                {
                    C = p.Position,
                    X = p.Rotation * Vector3.right,
                    Y = p.Rotation * Vector3.up,
                    Z = p.Rotation * Vector3.forward,
                    H = 0.5f * s,
                };
            }

            // Candidate pairs, found ONCE at the starting size: a shrink only ever makes a box
            // smaller, so a pair that cannot touch now never will.
            var neighbours = CandidatePairs(boxes);

            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            if (priority != null && priority.Length == n)
                Array.Sort(order, (a, b) => priority[a] != priority[b] ? priority[b].CompareTo(priority[a]) : a.CompareTo(b));

            var placed = new bool[n];
            var floor = minScale is { } f ? 0.5f * f : Vector3.zero; // half-extent floor
            var authored = new Vector3[n];
            for (int i = 0; i < n; i++) authored[i] = boxes[i].H;
            FitChains(boxes, priority, floor);
            int shrunk = 0, cleared = 0;
            var conflicts = new List<int>(16);

            foreach (int i in order)
            {
                if (IsEmpty(boxes[i])) continue; // the generator already cleared it
                conflicts.Clear();
                var list = neighbours[i];
                if (list != null)
                    foreach (int j in list)
                        if (placed[j] && !Clears(boxes[i], boxes[j])) conflicts.Add(j);

                if (conflicts.Count > 0)
                {
                    if (!TryShrink(ref boxes[i], boxes, conflicts, floor))
                    {
                        boxes[i].H = Vector3.zero;
                        points[i] = new SpawnPoint(points[i].Position, points[i].Rotation, Vector3.zero);
                        cleared++;
                        continue;
                    }
                }
                placed[i] = true;
                if (boxes[i].H != authored[i]) shrunk++;
                var sign = points[i].Scale;
                var h = 2f * boxes[i].H;
                points[i] = new SpawnPoint(points[i].Position, points[i].Rotation,
                    new Vector3(Mathf.Sign(sign.x) * h.x, Mathf.Sign(sign.y) * h.y, Mathf.Sign(sign.z) * h.z));
            }
            return new Result(n, shrunk, cleared);
        }

        /// <summary>
        /// A RUN of consecutive lays where each clips the next is one trail whose pitch is shorter
        /// than its prisms, and it is trimmed by ONE factor along every member's long axis - the
        /// largest that clears every consecutive pair. Left to the yield pass a rail alternates:
        /// the second prism shrinks clear of the first, which frees the third to stay whole, and
        /// the rail reads as long-short-long stripes. A run of mixed priorities is left to the
        /// yield pass (a gameplay prism does not trim for a decorative neighbour), as is a run no
        /// trim above the scale floor can clear (a duplicate, centres that coincide).
        /// </summary>
        static void FitChains(Box[] boxes, int[] priority, Vector3 floor)
        {
            int n = boxes.Length;
            int i = 0;
            while (i < n - 1)
            {
                if (IsEmpty(boxes[i]) || !ChainLink(boxes, priority, i))
                {
                    i++;
                    continue;
                }
                int end = i + 1;
                while (end < n - 1 && ChainLink(boxes, priority, end)) end++;
                TrimRun(boxes, i, end, floor);
                i = end;
            }
        }

        static bool ChainLink(Box[] boxes, int[] priority, int i) =>
            !IsEmpty(boxes[i + 1]) &&
            (priority == null || priority.Length != boxes.Length || priority[i] == priority[i + 1]) &&
            !Clears(boxes[i], boxes[i + 1]);

        static void TrimRun(Box[] boxes, int start, int end, Vector3 floor)
        {
            int count = end - start + 1;
            var full = new Vector3[count];
            var axis = new int[count];
            for (int k = 0; k < count; k++)
            {
                full[k] = boxes[start + k].H;
                var h = full[k];
                axis[k] = h.x >= h.y && h.x >= h.z ? 0 : h.y >= h.z ? 1 : 2;
            }

            void Apply(float t)
            {
                for (int k = 0; k < count; k++)
                {
                    var h = full[k];
                    int a = axis[k];
                    h[a] = Mathf.Lerp(floor[a], full[k][a], t);
                    boxes[start + k].H = h;
                }
            }

            bool RunClears()
            {
                for (int k = start; k < end; k++)
                    if (!Clears(boxes[k], boxes[k + 1])) return false;
                return true;
            }

            Apply(0f);
            if (!RunClears())
            {
                Apply(1f); // no trim clears it: the yield pass decides, prism by prism
                return;
            }
            float lo = 0f, hi = 1f;
            for (int step = 0; step < BisectionSteps; step++)
            {
                float mid = 0.5f * (lo + hi);
                Apply(mid);
                if (RunClears()) lo = mid; else hi = mid;
            }
            Apply(lo);
        }

        /// <summary>The largest box about the same centre that clears every conflict, over four
        /// shrink shapes (long axis, footprint, thin axis, uniform). False when none keeps
        /// <see cref="MinKeptVolume"/> of the prism above the scale floor.</summary>
        static bool TryShrink(ref Box b, Box[] boxes, List<int> conflicts, Vector3 floor)
        {
            var h = b.H;
            int longest = h.x >= h.y && h.x >= h.z ? 0 : h.y >= h.z ? 1 : 2;
            int thinnest = h.x <= h.y && h.x <= h.z ? 0 : h.y <= h.z ? 1 : 2;
            float fullVolume = h.x * h.y * h.z;
            float bestVolume = -1f;
            Vector3 best = h;
            for (int m = 0; m < 4; m++)
            {
                var mask = m switch
                {
                    0 => Axis(longest),
                    1 => Vector3.one - Axis(thinnest),
                    2 => Axis(thinnest),
                    _ => Vector3.one,
                };
                // t = 1 keeps the box, t = 0 takes every masked axis to the floor.
                Vector3 At(float t) => new Vector3(
                    mask.x > 0f ? Mathf.Lerp(floor.x, h.x, t) : h.x,
                    mask.y > 0f ? Mathf.Lerp(floor.y, h.y, t) : h.y,
                    mask.z > 0f ? Mathf.Lerp(floor.z, h.z, t) : h.z);

                var trial = b;
                trial.H = At(0f);
                if (!ClearsAll(trial, boxes, conflicts)) continue; // even the floor clips
                float lo = 0f, hi = 1f;
                for (int k = 0; k < BisectionSteps; k++)
                {
                    float mid = 0.5f * (lo + hi);
                    trial.H = At(mid);
                    if (ClearsAll(trial, boxes, conflicts)) lo = mid; else hi = mid;
                }
                var size = At(lo);
                float v = size.x * size.y * size.z;
                if (v > bestVolume) { bestVolume = v; best = size; }
            }
            if (bestVolume < MinKeptVolume * fullVolume || bestVolume <= 0f) return false;
            b.H = best;
            return true;
        }

        static bool ClearsAll(in Box a, Box[] boxes, List<int> others)
        {
            foreach (int j in others)
                if (!Clears(a, boxes[j])) return false;
            return true;
        }

        static bool Clears(in Box a, in Box b)
        {
            if (IsEmpty(b)) return true;
            float gap = ClearanceFraction * Mathf.Min(a.MinHalf, b.MinHalf);
            return Separation(a, b) >= gap;
        }

        /// <summary>Signed separation of two oriented boxes over the 15 separating axes: positive
        /// clears by that much, negative is the depth they interpenetrate.</summary>
        static float Separation(in Box a, in Box b)
        {
            var d = b.C - a.C;
            float best = float.NegativeInfinity;
            best = Mathf.Max(best, Axis(a, b, d, a.X)); best = Mathf.Max(best, Axis(a, b, d, a.Y));
            best = Mathf.Max(best, Axis(a, b, d, a.Z)); best = Mathf.Max(best, Axis(a, b, d, b.X));
            best = Mathf.Max(best, Axis(a, b, d, b.Y)); best = Mathf.Max(best, Axis(a, b, d, b.Z));
            if (best >= 0f) return best;
            var ax = a.X; var ay = a.Y; var az = a.Z;
            var bx = b.X; var by = b.Y; var bz = b.Z;
            best = Mathf.Max(best, Axis(a, b, d, Vector3.Cross(ax, bx))); best = Mathf.Max(best, Axis(a, b, d, Vector3.Cross(ax, by)));
            best = Mathf.Max(best, Axis(a, b, d, Vector3.Cross(ax, bz))); best = Mathf.Max(best, Axis(a, b, d, Vector3.Cross(ay, bx)));
            best = Mathf.Max(best, Axis(a, b, d, Vector3.Cross(ay, by))); best = Mathf.Max(best, Axis(a, b, d, Vector3.Cross(ay, bz)));
            best = Mathf.Max(best, Axis(a, b, d, Vector3.Cross(az, bx))); best = Mathf.Max(best, Axis(a, b, d, Vector3.Cross(az, by)));
            best = Mathf.Max(best, Axis(a, b, d, Vector3.Cross(az, bz)));
            return best;
        }

        static float Axis(in Box a, in Box b, Vector3 d, Vector3 l)
        {
            float len = l.magnitude;
            if (len < 1e-6f) return float.NegativeInfinity; // parallel edges: the cross is void
            return (Mathf.Abs(Vector3.Dot(d, l)) - Project(a, l) - Project(b, l)) / len;
        }

        static float Project(in Box o, Vector3 l) =>
            Mathf.Abs(Vector3.Dot(o.X, l)) * o.H.x + Mathf.Abs(Vector3.Dot(o.Y, l)) * o.H.y + Mathf.Abs(Vector3.Dot(o.Z, l)) * o.H.z;

        /// <summary>
        /// Every pair whose world AABBs overlap, by sweep-and-prune along the axis the centres
        /// spread widest on. Each prism's list holds only its overlapping partners. A sweep rather
        /// than a hash grid because the shipped sizes span three orders of magnitude (0.5 to 132),
        /// and a grid sized for either end is quadratic at the other.
        /// </summary>
        static List<int>[] CandidatePairs(Box[] boxes)
        {
            int n = boxes.Length;
            var mins = new Vector3[n];
            var maxs = new Vector3[n];
            Vector3 lo = Vector3.positiveInfinity, hi = Vector3.negativeInfinity;
            for (int i = 0; i < n; i++)
            {
                var b = boxes[i];
                var e = new Vector3(
                    Mathf.Abs(b.X.x) * b.H.x + Mathf.Abs(b.Y.x) * b.H.y + Mathf.Abs(b.Z.x) * b.H.z,
                    Mathf.Abs(b.X.y) * b.H.x + Mathf.Abs(b.Y.y) * b.H.y + Mathf.Abs(b.Z.y) * b.H.z,
                    Mathf.Abs(b.X.z) * b.H.x + Mathf.Abs(b.Y.z) * b.H.y + Mathf.Abs(b.Z.z) * b.H.z);
                mins[i] = b.C - e;
                maxs[i] = b.C + e;
                lo = Vector3.Min(lo, b.C);
                hi = Vector3.Max(hi, b.C);
            }
            var spread = hi - lo;
            int ax = spread.x >= spread.y && spread.x >= spread.z ? 0 : spread.y >= spread.z ? 1 : 2;

            var sorted = new int[n];
            var keys = new float[n];
            for (int i = 0; i < n; i++) { sorted[i] = i; keys[i] = mins[i][ax]; }
            Array.Sort(keys, sorted);

            var lists = new List<int>[n];
            for (int s = 0; s < n; s++)
            {
                int i = sorted[s];
                if (IsEmpty(boxes[i])) continue;
                float end = maxs[i][ax];
                for (int t = s + 1; t < n; t++)
                {
                    int j = sorted[t];
                    if (mins[j][ax] > end) break;
                    if (IsEmpty(boxes[j])) continue;
                    if (mins[i].x > maxs[j].x || mins[j].x > maxs[i].x ||
                        mins[i].y > maxs[j].y || mins[j].y > maxs[i].y ||
                        mins[i].z > maxs[j].z || mins[j].z > maxs[i].z) continue;
                    (lists[i] ??= new List<int>(4)).Add(j);
                    (lists[j] ??= new List<int>(4)).Add(i);
                }
            }
            return lists;
        }

        // Exact, not Unity's approximate Vector3 ==: a prism a hundred-thousandth wide is a prism.
        static bool IsEmpty(in Box b) => b.H.x == 0f && b.H.y == 0f && b.H.z == 0f;
        static Vector3 Axis(int a) => a == 0 ? Vector3.right : a == 1 ? Vector3.up : Vector3.forward;
        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        static Vector3 Clamp(Vector3 v, Vector3 lo, Vector3 hi) =>
            new Vector3(Mathf.Clamp(v.x, lo.x, hi.x), Mathf.Clamp(v.y, lo.y, hi.y), Mathf.Clamp(v.z, lo.z, hi.z));
    }
}
