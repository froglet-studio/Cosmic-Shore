using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One way the Pistol can set off: from perch <see cref="Perch"/>, riding <see cref="Dir"/> (+1 toward the
    /// newer perches, -1 toward the older ones).</summary>
    public readonly struct PathChoice
    {
        public readonly int Perch, Dir;
        public PathChoice(int perch, int dir) { Perch = perch; Dir = dir >= 0 ? 1 : -1; }
        /// <summary>The island it rides: segment k joins perch k and perch k + 1.</summary>
        public int Segment => Dir > 0 ? Perch : Perch - 1;
        public bool Same(PathChoice o) => o.Perch == Perch && o.Dir == Dir;
    }

    /// <summary>
    /// The Pistol's choice at a perch (Garrett, 2026-10-25: "when the shrimp gets to a new perch it should be able to pick
    /// another perch to teleport to. teleporting to different perches does not grow them. only traveling the distance using
    /// the dive should grow the den. the pistol should be able to travel either direction from a den as well. the view that
    /// allows the pistol to find a den and direction of interest should show which paths have collected the most opposing
    /// prisms for the vortex ring to destroy."). Pure, so the tests read it: the options along the perch chain, the cursor
    /// that walks them, the AI's pick, and which prisms count toward a path's score.
    /// </summary>
    public static class PistolPaths
    {
        /// <summary>
        /// Every (perch, direction) the Pistol can set off on, in the order a cursor walks the chain: perch 0 ahead, perch 1
        /// back, perch 1 ahead, ... perch n back, then the head perch ahead, which is PENDING (no island yet: it waits for
        /// the Goby's next dart, <see cref="Ready"/> false) and is listed whenever there is a head to wait at.
        /// </summary>
        public static void Options(int perches, int segments, List<PathChoice> into)
        {
            into.Clear();
            if (perches <= 0) return;
            int segs = Mathf.Min(segments, perches - 1);
            for (int k = 0; k < perches; k++)
            {
                if (k > 0 && k - 1 < segs) into.Add(new PathChoice(k, -1));
                if (k < segs) into.Add(new PathChoice(k, 1));
            }
            var head = new PathChoice(perches - 1, 1);
            if (head.Segment >= segs) into.Add(head);
        }

        /// <summary>True when the choice has an island to ride (the head's "ahead" waits for one).</summary>
        public static bool Ready(PathChoice c, int segments) => c.Segment >= 0 && c.Segment < segments;

        /// <summary>Where <paramref name="c"/> sits in <paramref name="options"/>; -1 when it is not there.</summary>
        public static int IndexOf(List<PathChoice> options, PathChoice c)
        {
            for (int i = 0; i < options.Count; i++) if (options[i].Same(c)) return i;
            return -1;
        }

        /// <summary>The choice <paramref name="delta"/> steps along the chain from <paramref name="current"/>, clamped to its
        /// ends; the nearest option on its perch when <paramref name="current"/> is gone.</summary>
        public static PathChoice Step(List<PathChoice> options, PathChoice current, int delta)
        {
            if (options.Count == 0) return current;
            int i = IndexOf(options, current);
            if (i < 0) i = Nearest(options, current.Perch);
            return options[Mathf.Clamp(i + delta, 0, options.Count - 1)];
        }

        /// <summary>The option on perch <paramref name="perch"/> (or the nearest perch that has one).</summary>
        public static int Nearest(List<PathChoice> options, int perch)
        {
            int best = 0, bestD = int.MaxValue;
            for (int i = 0; i < options.Count; i++)
            {
                int d = Mathf.Abs(options[i].Perch - perch);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>
        /// The ready option whose island holds the most opposing prisms (<paramref name="scores"/>, one per segment); on a
        /// tie the one setting off nearest <paramref name="fromPerch"/>. -1 when every island is empty, so the caller keeps
        /// its own way.
        /// </summary>
        public static int Best(List<PathChoice> options, IReadOnlyList<int> scores, int segments, int fromPerch)
        {
            int best = -1, bestScore = 0, bestD = int.MaxValue;
            for (int i = 0; i < options.Count; i++)
            {
                var c = options[i];
                if (!Ready(c, segments) || c.Segment >= scores.Count) continue;
                int s = scores[c.Segment], d = Mathf.Abs(c.Perch - fromPerch);
                if (s > bestScore || (s == bestScore && s > 0 && d < bestD)) { best = i; bestScore = s; bestD = d; }
            }
            return best;
        }

        /// <summary>
        /// Whether a prism at frame coordinates <paramref name="fr"/> (x along the island, y up, z out of its plane) lies
        /// where the vortex ring can reach it: over the island (0..<paramref name="endX"/>), within the undertow's
        /// <paramref name="reach"/> of the plane (it is pulled in), and within <paramref name="band"/> of the ground.
        /// </summary>
        public static bool InBand(Vector3 fr, float endX, float ground, float reach, float band)
            => fr.x >= 0f && fr.x <= endX && Mathf.Abs(fr.z) <= reach && Mathf.Abs(fr.y - ground) <= band;
    }
}
