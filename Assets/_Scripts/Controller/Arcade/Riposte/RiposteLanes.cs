using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// How Riposte's AI reads the Lists (RIPOSTE.md sections 3 and 4): pure functions over the arena's lanes and
    /// rails, held by <c>RiposteLanesTests</c>. A <b>lane</b> is a straight, stick-free line from the centre
    /// clearing's edge (<see cref="Line.A"/>) to its refuel clearing (<see cref="Line.B"/>); a <b>rail</b> is the
    /// line of pickets beside one. The pilot cuts a rail end to end for fuel, runs out to a lane's mouth, and winds
    /// up down the lane before it turns onto its rival.
    /// </summary>
    public static class RiposteLanes
    {
        /// <summary>A straight line of the arena, in world space. A is the end nearer the centre.</summary>
        public readonly struct Line
        {
            public readonly Vector3 A, B;
            public Line(Vector3 a, Vector3 b) { A = a; B = b; }
        }

        /// <summary>The line nearest <paramref name="self"/> that is not in <paramref name="skip"/>, or -1.</summary>
        public static int Nearest(Vector3 self, IReadOnlyList<Line> lines, ICollection<int> skip = null)
        {
            int best = -1;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < lines.Count; i++)
            {
                if (skip != null && skip.Contains(i)) continue;
                float sqr = RiposteYard.PointSegmentSqr(self, lines[i].A, lines[i].B);
                if (sqr < bestSqr) { bestSqr = sqr; best = i; }
            }
            return best;
        }

        /// <summary>Where a run along <paramref name="line"/> starts: the end nearer the pilot, so the whole line is
        /// flown in one straight pass.</summary>
        public static Vector3 Entry(Vector3 self, Line line) =>
            (line.A - self).sqrMagnitude <= (line.B - self).sqrMagnitude ? line.A : line.B;

        /// <summary>Where that run ends: the other end.</summary>
        public static Vector3 Exit(Vector3 self, Line line) =>
            (line.A - self).sqrMagnitude <= (line.B - self).sqrMagnitude ? line.B : line.A;

        /// <summary>True once the pilot is on the line: within <paramref name="lineUpRadius"/> of it.</summary>
        public static bool LinedUp(Vector3 self, Line line, float lineUpRadius) =>
            RiposteYard.PointSegmentSqr(self, line.A, line.B) <= lineUpRadius * lineUpRadius;

        /// <summary>
        /// The lane whose mouth (its outer end) lies most nearly ahead of the nose, when one lies within
        /// <paramref name="coneDegrees"/> of it and at least <paramref name="minDistance"/> away; else -1 (run
        /// straight out as before). A run-out is a straight leg: it never turns hard to find a lane.
        /// </summary>
        public static int MouthAhead(Vector3 self, Vector3 forward, IReadOnlyList<Line> lanes, float coneDegrees,
                                     float minDistance)
        {
            int best = -1;
            float bestAngle = coneDegrees;
            for (int i = 0; i < lanes.Count; i++)
            {
                Vector3 to = lanes[i].B - self;
                if (to.sqrMagnitude < minDistance * minDistance) continue;
                float angle = Vector3.Angle(forward, to);
                if (angle <= bestAngle) { bestAngle = angle; best = i; }
            }
            return best;
        }

        /// <summary>
        /// True while a pilot at a lane's mouth should keep flying DOWN the lane (winding the ramp up on the straight)
        /// instead of turning onto its rival: it is still outside the centre clearing and the rival is further than
        /// <paramref name="breakDistance"/>.
        /// </summary>
        public static bool RunIn(float distanceFromCentre, float centreClearingRadius, float rivalDistance,
                                 float breakDistance) =>
            distanceFromCentre > centreClearingRadius && rivalDistance > breakDistance;
    }
}
