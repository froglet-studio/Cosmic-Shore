using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Brittle Star Time's RACE LINE, read off a lap of rings (2026-11-16): which rings Speed Mode holds through, where to
    /// let go of it before the ones it cannot, and the line an AI flies. Pure (no UnityEngine.Object, no clock, no
    /// randomness), computed from the broadcast rings alone, so every peer lays the same posts, and the AI's coach, the
    /// heat, the tests and the offline harness read one rule (BRITTLESTARTIME.md section 0).
    ///
    /// <list type="bullet">
    /// <item><b>Flat out</b> - a ring whose corner Speed Mode's circle holds (the corner's flyable circle, the inscribed
    /// circle of its two legs widened by the mouth, <see cref="FlyableRadius"/>): hold Speed Mode through it. Most rings
    /// of every rung; every ring of I1, I2 and the I4 coil.</item>
    /// <item><b>Let-go</b> - a ring whose corner is tighter than the Speed the hull carries (the I3 hairpin apexes, the
    /// I4 return): let go <see cref="LetGoDistance"/> before it, the coast from full Speed down to the corner's own
    /// speed. The course stands a let-go gate there, two columns of posts either side of the line
    /// (<see cref="LetGoPosts"/>). Offline, reading them saves 4.4% at I3 and 8.6% at I4 against the best line flown
    /// without them.</item>
    /// <item><b>The line</b> (<see cref="SteerPoint"/>) - down a ring's facing when the ring faces the leg into it, at its
    /// centre when it faces across that leg (a hairpin's apex). The AI's coach flies it. It is NOT drawn: rails marking it
    /// were measured and cut (the ring already shows its facing; they saved -2.3 / -4.3 / -0.7% at I1 / I2 / I4).</item>
    /// </list>
    /// </summary>
    public static class BrittleStarTimeLine
    {
        /// <summary>The line comes down a ring's facing when its axis is within this many degrees of the leg into it.</summary>
        public const float FacingMaxDegrees = 60f;

        /// <summary>How far back along the facing the line starts: this share of the leg into the ring...</summary>
        public const float LaneLegShare = 0.4f;

        /// <summary>...and never further than this.</summary>
        public const float LaneMaxLength = 300f;

        /// <summary>A ring wants a let-go when its corner speed is under this share of full Speed.</summary>
        public const float LetGoBelowShare = 0.9f;

        /// <summary>Extra let-go, u, for the time the mode takes to blend out (modeBlendSeconds 0.35 at Speed).</summary>
        public const float LetGoBlendMargin = 40f;

        /// <summary>The let-go gate's columns stand this far out from the mouth's rim, so they are never in the line.</summary>
        public const float ColumnOutset = 12f;

        /// <summary>Posts in each of the let-go gate's two columns.</summary>
        public const int PostsPerColumn = 3;

        /// <summary>The gap between a column's posts, u.</summary>
        public const float ColumnPitch = 11f;

        /// <summary>A let-go post's size, u.</summary>
        public const float PostSize = 7f;

        /// <summary>The angle between the leg into ring <paramref name="k"/> and the leg out of it, degrees.</summary>
        public static float TurnDegrees(IReadOnlyList<RaceGate> lap, int k)
        {
            int n = lap.Count;
            Vector3 a = lap[k].Position - lap[(k - 1 + n) % n].Position, b = lap[(k + 1) % n].Position - lap[k].Position;
            return RaceCourseGeometry.Angle(a, b);
        }

        /// <summary>The angle between ring <paramref name="k"/>'s axis and the leg into it, folded to 0..90 (a ring
        /// counts in either direction).</summary>
        public static float FacingDegrees(IReadOnlyList<RaceGate> lap, int k)
        {
            int n = lap.Count;
            Vector3 leg = lap[k].Position - lap[(k - 1 + n) % n].Position;
            float a = RaceCourseGeometry.Angle(leg, lap[k].Axis);
            return a > 90f ? 180f - a : a;
        }

        /// <summary>Does the line come down ring <paramref name="k"/>'s facing (it faces the leg into it)?</summary>
        public static bool FacesLeg(IReadOnlyList<RaceGate> lap, int k) => FacingDegrees(lap, k) <= FacingMaxDegrees;

        /// <summary>The direction the course flows through ring <paramref name="k"/>: its axis, turned to agree with the
        /// leg into it.</summary>
        public static Vector3 Flow(IReadOnlyList<RaceGate> lap, int k)
        {
            int n = lap.Count;
            Vector3 leg = lap[k].Position - lap[(k - 1 + n) % n].Position;
            return Vector3.Dot(leg, lap[k].Axis) >= 0f ? lap[k].Axis : -lap[k].Axis;
        }

        /// <summary>How far back along ring <paramref name="k"/>'s facing the line starts, u.</summary>
        public static float LaneLength(IReadOnlyList<RaceGate> lap, int k)
        {
            int n = lap.Count;
            float leg = (lap[k].Position - lap[(k - 1 + n) % n].Position).magnitude;
            return Mathf.Min(LaneMaxLength, LaneLegShare * leg);
        }

        /// <summary>
        /// The widest circle a pilot can fly through ring <paramref name="k"/>'s corner: the inscribed circle of the
        /// two legs (<see cref="RaceCourseGeometry.CornerRadius"/>) widened by the mouth, since a ring can be crossed
        /// anywhere inside it.
        /// </summary>
        public static float FlyableRadius(IReadOnlyList<RaceGate> lap, int k)
        {
            int n = lap.Count;
            float r = RaceCourseGeometry.CornerRadius(lap[(k - 1 + n) % n].Position, lap[k].Position, lap[(k + 1) % n].Position);
            return float.IsPositiveInfinity(r) ? r : r + lap[k].Radius;
        }

        /// <summary>The speed ring <paramref name="k"/>'s corner holds (its flyable circle), capped at
        /// <paramref name="topSpeed"/>.</summary>
        public static float CornerSpeed(IReadOnlyList<RaceGate> lap, int k, float topSpeed)
        {
            float r = FlyableRadius(lap, k);
            if (float.IsPositiveInfinity(r) || r * Mathf.Deg2Rad * BrittleStarTimeCourse.RotationThrottleScaler >= 1f)
                return topSpeed;
            return Mathf.Min(topSpeed, BrittleStarTimeCourse.SpeedForRadius(r));
        }

        /// <summary>
        /// How far before ring <paramref name="k"/> to let go of Speed Mode from <paramref name="topSpeed"/>: the coast
        /// down to its corner speed plus the mode's blend-out, or 0 for a ring Speed Mode holds (a flat-out ring).
        /// </summary>
        public static float LetGoDistance(IReadOnlyList<RaceGate> lap, int k, float topSpeed) =>
            LetGoFrom(topSpeed, CornerSpeed(lap, k, topSpeed));

        /// <summary>The let-go for a corner flown at <paramref name="cornerSpeed"/> from <paramref name="topSpeed"/>: the
        /// coast plus the blend-out, or 0 when the corner is within <see cref="LetGoBelowShare"/> of full Speed.</summary>
        public static float LetGoFrom(float topSpeed, float cornerSpeed) =>
            cornerSpeed >= LetGoBelowShare * topSpeed
                ? 0f
                : BrittleStarTimeCourse.CoastDistance(topSpeed, cornerSpeed) + LetGoBlendMargin;

        /// <summary>The way into ring <paramref name="k"/> that its let-go gate stands across: down its facing when it
        /// faces the leg into it, else along that leg.</summary>
        public static Vector3 Approach(IReadOnlyList<RaceGate> lap, int k)
        {
            int n = lap.Count;
            if (FacesLeg(lap, k)) return Flow(lap, k);
            Vector3 legIn = lap[k].Position - lap[(k - 1 + n) % n].Position;
            return RaceCourseGeometry.SafeNormalize(legIn, lap[k].Axis);
        }

        /// <summary>Where ring <paramref name="k"/>'s let-go gate stands (on the way in, <paramref name="letGo"/> before
        /// the ring, never past 90% of the leg in).</summary>
        public static Vector3 LetGoPoint(IReadOnlyList<RaceGate> lap, int k, float letGo)
        {
            int n = lap.Count;
            float leg = (lap[k].Position - lap[(k - 1 + n) % n].Position).magnitude;
            return lap[k].Position - Approach(lap, k) * Mathf.Min(letGo, 0.9f * leg);
        }

        /// <summary>A post of a let-go gate: where it stands, which way it faces (the way in), which ring it serves.</summary>
        public readonly struct Post
        {
            public readonly Vector3 Position;
            public readonly Vector3 Forward;
            public readonly Vector3 Up;
            public readonly int Ring;

            public Post(Vector3 position, Vector3 forward, Vector3 up, int ring)
            {
                Position = position;
                Forward = forward;
                Up = up;
                Ring = ring;
            }
        }

        /// <summary>
        /// Ring <paramref name="k"/>'s let-go gate, <paramref name="letGo"/> before it: two columns of
        /// <see cref="PostsPerColumn"/> posts, one either side of the line, <see cref="ColumnOutset"/> past the mouth's rim
        /// (a pair of columns, not a hoop, so it never reads as a ring to thread). Nothing for a flat-out ring.
        /// </summary>
        public static void LetGoPosts(IReadOnlyList<RaceGate> lap, int k, float letGo, List<Post> into)
        {
            if (letGo <= 0f) return;
            var g = lap[k];
            Vector3 along = Approach(lap, k);
            Vector3 centre = LetGoPoint(lap, k, letGo);
            // The columns stand across the line in the plane of the turn (the leg in and the leg out), so a pilot sees
            // one either side as they come in and turn.
            int n = lap.Count;
            Vector3 legOut = lap[(k + 1) % n].Position - g.Position;
            Vector3 up = RaceCourseGeometry.SafeNormalize(Vector3.Cross(along, legOut), RaceCourseGeometry.Perpendicular(along));
            Vector3 side = Vector3.Cross(up, along).normalized;
            float off = g.Radius + ColumnOutset;
            for (int c = -1; c <= 1; c += 2)
                for (int i = 0; i < PostsPerColumn; i++)
                {
                    float h = (i - 0.5f * (PostsPerColumn - 1)) * ColumnPitch;
                    into.Add(new Post(centre + side * (c * off) + up * h, along, up, k));
                }
        }

        /// <summary>Every let-go gate of the lap, let-go measured from <paramref name="topSpeed"/>.</summary>
        public static List<Post> Marks(IReadOnlyList<RaceGate> lap, float topSpeed)
        {
            var posts = new List<Post>(4 * 2 * PostsPerColumn);
            for (int k = 0; k < lap.Count; k++) LetGoPosts(lap, k, LetGoDistance(lap, k, topSpeed), posts);
            return posts;
        }

        /// <summary>
        /// Where a pilot on the line steers for ring <paramref name="k"/> from <paramref name="pos"/>: down its facing (a
        /// point back along the flow, half the range out, no further than <see cref="LaneLength"/>) and through the mouth
        /// once close; a ring that faces across the leg into it is flown at its centre. Past the ring (a missed mouth) it
        /// comes back through.
        /// </summary>
        public static Vector3 SteerPoint(IReadOnlyList<RaceGate> lap, int k, Vector3 pos)
        {
            var g = lap[k];
            float d = (g.Position - pos).magnitude;
            Vector3 flow = Flow(lap, k);
            if (Vector3.Dot(pos - g.Position, flow) > 0f) flow = -flow;
            if (d < 1.2f * g.Radius) return g.Position + flow * 200f;
            if (!FacesLeg(lap, k)) return g.Position;
            return g.Position - flow * Mathf.Min(0.5f * d, LaneLength(lap, k));
        }
    }
}
