using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Brittle Star Time's garden ruins at I4 (BRITTLESTARTIME.md section 0a; Yash, 2026-11-24: the environment update
    /// Spearline got). The ruins themselves are <see cref="SpearlineRuins"/>' - this file only hands that generator the
    /// lap to lay them round, and keeps them off the let-go gates. Nothing here is a second generator.
    ///
    /// <para><b>The lap it is given is the FLOWN line, not the raw rings</b> (<see cref="RuinLap"/>). Spearline's
    /// generator keeps a clear lane round a curve that leaves and arrives along each ring's axis. That is how a ring
    /// that faces its leg is flown, but a ring that faces ACROSS the leg into it (the corkscrew's two return rings) is
    /// flown at its centre (<see cref="BrittleStarTimeLine.SteerPoint"/>): the pilot leaves the ring before on its
    /// facing, turns on Speed Mode's circle, and runs straight in. So such a leg is given as the turn
    /// (<see cref="TurnExit"/>) and then the straight, and the lane lies where the hull flies.</para>
    ///
    /// <para><b>What that leaves at I4.</b> The generator lays only on a leg of 380 u or more, 120 u clear of every
    /// ring. The corkscrew's coil rings stand ~170 u apart, so the coil carries none; the return straight (~670 u) is
    /// the one leg long enough, and it carries one ruin a lap. The kind is drawn from the lap's own rings, so it differs
    /// from race to race. If the corkscrew does not fit the shell and I4 flies the Gearbox, its long legs carry more.</para>
    ///
    /// <para>Pure: a function of the broadcast lap, so every peer lays the same ruins, and the edit-mode tests hold it
    /// (<c>BrittleStarTimeRuinsTests</c>). Decoration only - nothing here reads or writes a vessel.</para>
    /// </summary>
    public static class BrittleStarTimeRuins
    {
        /// <summary>No standing ruin prism comes nearer than this (u) to a let-go post, and no wheel sweeps nearer.</summary>
        public const float PostClearance = 30f;

        /// <summary>
        /// Where a pilot who leaves <paramref name="from"/> heading <paramref name="flow"/> and turns on a circle of
        /// <paramref name="turnRadius"/> comes out pointing straight at <paramref name="to"/>. False when
        /// <paramref name="to"/> is dead ahead or inside the turning circle (there is no straight to speak of).
        /// </summary>
        public static bool TurnExit(Vector3 from, Vector3 flow, Vector3 to, float turnRadius, out Vector3 exit)
        {
            exit = from;
            Vector3 d = to - from;
            float along = Vector3.Dot(d, flow);
            Vector3 across = d - flow * along;
            float side = across.magnitude;
            if (side < 1e-3f || turnRadius <= 0f) return false;
            Vector3 n = across / side;
            // In the plane of the turn: x along the flow, y towards the target; the circle's centre is (0, R).
            float vx = along, vy = side - turnRadius;
            float dist = Mathf.Sqrt(vx * vx + vy * vy);
            if (dist <= turnRadius) return false;
            float angle = Mathf.Atan2(vy, vx) - Mathf.Acos(turnRadius / dist);
            exit = from + flow * (turnRadius * Mathf.Cos(angle)) + n * (turnRadius + turnRadius * Mathf.Sin(angle));
            return true;
        }

        /// <summary>
        /// The lap the ruins are laid round: the course's rings from ring 1 on and round to ring 0 (so the kinds are
        /// drawn from a ring the seed moves, and the leg off the start line is the closing one that carries none), each
        /// with the direction it is flown IN as its axis. A ring that faces across a leg long enough to carry a ruin is
        /// preceded by the point the turn towards it ends (a ring of no radius), and takes the straight's direction.
        /// </summary>
        public static List<RaceGate> RuinLap(IReadOnlyList<RaceGate> course, float turnRadius)
        {
            int n = course.Count;
            var lap = new List<RaceGate>(n + 2);
            for (int step = 0; step < n; step++)
            {
                int k = (step + 1) % n;
                var g = course[k];
                bool faces = BrittleStarTimeLine.FacesLeg(course, k);
                if (!faces && lap.Count > 0)
                {
                    var before = lap[lap.Count - 1];
                    if (TurnExit(before.Position, before.Axis, g.Position, turnRadius, out var exit) &&
                        (g.Position - exit).magnitude >= SpearlineRuins.MinLegLength)
                    {
                        Vector3 straight = (g.Position - exit).normalized;
                        lap.Add(new RaceGate(exit, straight, 0f));
                        lap.Add(new RaceGate(g.Position, straight, g.Radius));
                        continue;
                    }
                }
                Vector3 axis = faces ? BrittleStarTimeLine.Flow(course, k) : BrittleStarTimeLine.Approach(course, k);
                lap.Add(new RaceGate(g.Position, axis, g.Radius));
            }
            return lap;
        }

        /// <summary>
        /// Every ruin prism of <paramref name="course"/> on this rung, appended to <paramref name="into"/> (the turning
        /// ones named in <paramref name="wheels"/>): <see cref="SpearlineRuins.Lay"/> round <see cref="RuinLap"/>, less
        /// whatever would stand in a let-go gate (<paramref name="posts"/>). Returns the lap the ruins were laid round.
        /// Both lists must come in empty.
        /// </summary>
        public static List<RaceGate> Lay(IReadOnlyList<RaceGate> course, int intensity, float turnRadius,
                                         IReadOnlyList<BrittleStarTimeLine.Post> posts,
                                         List<SpearlineRuinPart> into, List<SpearlineRuinWheel> wheels)
        {
            var settings = SpearlineRuins.ForIntensity(intensity);
            if (settings.PerLap <= 0 || course.Count < 3) return null;
            var lap = RuinLap(course, turnRadius);
            SpearlineRuins.Lay(lap, settings, into, wheels);
            if (posts != null && posts.Count > 0) KeepOffPosts(posts, into, wheels);
            return lap;
        }

        /// <summary>Drop each standing prism that reaches a let-go post, and each wheel whose sweep does (whole).</summary>
        static void KeepOffPosts(IReadOnlyList<BrittleStarTimeLine.Post> posts, List<SpearlineRuinPart> parts,
                                 List<SpearlineRuinWheel> wheels)
        {
            var kept = new List<SpearlineRuinPart>(parts.Count);
            var keptWheels = new List<SpearlineRuinWheel>(wheels.Count);
            int i = 0;
            while (i < parts.Count)
            {
                int w = WheelAt(wheels, i);
                if (w < 0)
                {
                    if (PostDistance(posts, parts[i]) >= PostClearance) kept.Add(parts[i]);
                    i++;
                    continue;
                }
                var wheel = wheels[w];
                if (NearestPost(posts, wheel.Centre) >= SpearlineRuins.WheelRadius + PostClearance)
                {
                    keptWheels.Add(new SpearlineRuinWheel(wheel.Centre, wheel.Axis, wheel.DegreesPerSecond, kept.Count, wheel.Count));
                    for (int j = wheel.First; j < wheel.First + wheel.Count; j++) kept.Add(parts[j]);
                }
                i = wheel.First + wheel.Count;
            }
            parts.Clear();
            parts.AddRange(kept);
            wheels.Clear();
            wheels.AddRange(keptWheels);
        }

        static int WheelAt(List<SpearlineRuinWheel> wheels, int part)
        {
            for (int w = 0; w < wheels.Count; w++)
                if (part >= wheels[w].First && part < wheels[w].First + wheels[w].Count) return w;
            return -1;
        }

        static float NearestPost(IReadOnlyList<BrittleStarTimeLine.Post> posts, Vector3 p)
        {
            float best = float.MaxValue;
            for (int k = 0; k < posts.Count; k++) best = Mathf.Min(best, (posts[k].Position - p).magnitude);
            return best;
        }

        /// <summary>How near <paramref name="part"/> comes to any let-go post, along its whole length.</summary>
        public static float PostDistance(IReadOnlyList<BrittleStarTimeLine.Post> posts, in SpearlineRuinPart part)
        {
            Vector3 half = part.Forward * (0.5f * part.Scale.z);
            Vector3 a = part.Position - half, ab = half * 2f;
            float len = ab.sqrMagnitude, best = float.MaxValue;
            for (int k = 0; k < posts.Count; k++)
            {
                Vector3 p = posts[k].Position;
                float t = len > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len) : 0f;
                best = Mathf.Min(best, (p - (a + ab * t)).magnitude);
            }
            return best;
        }
    }
}
