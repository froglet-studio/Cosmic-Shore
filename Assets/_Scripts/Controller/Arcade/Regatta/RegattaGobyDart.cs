using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Goby's dart for an AI seat in Regatta, after Perch Run's coach (<c>PerchRunController</c>, PERCH_RUN.md 5).
    /// Without a coach the seat's Goby AI lays islands for the Pistol and never races a ring (2026-11-17: 0 gates in
    /// Regatta). With this one it races on darts, through the calls the person's buttons make (<see cref="IGobyRaceCoach"/>):
    /// <list type="number">
    /// <item>A DOUBLE first: a line that threads this ring and the next within one dart, charged
    /// <see cref="Overshoot"/> past the second.</item>
    /// <item>Else this ring: its centre if the dart's own block test (<see cref="GobyPistolDuo.RaceDartStops"/>, the
    /// rails and every other prism) is clear, else the nearest clear point inside the mouth; charged
    /// <see cref="Overshoot"/> past the ring, or full when the ring is further than one dart.</item>
    /// <item>A ring inside the turning circle is only ever orbited: fly straight on until it is outside, then turn.</item>
    /// </list>
    /// Perch Run plans against its coral list; Regatta has no list, so the plan asks the Goby where its dart would stop.
    /// The two numbers it shares with Perch Run (<see cref="Overshoot"/>, <see cref="TurnMargin"/>) are the ones the
    /// Time-game AI search learns there (time_ai_search.py --hull Goby). Nothing on the vessel changes.
    /// </summary>
    [Serializable]
    public sealed class RegattaGobyDart
    {
        [Tooltip("How far past the last ring it threads the AI charges its dart (u). Perch Run's aiOvershoot.")]
        [Range(0f, 300f)] public float Overshoot = PerchRunCourse.Overshoot;

        [Tooltip("The turning circle is widened by this much before a ring counts as inside it. Perch Run's aiTurnMargin.")]
        [Range(1f, 3f)] public float TurnMargin = 1.25f;

        [Tooltip("How far inside the mouth's edge (beyond the hull's radius) a line must pass to count as threading it (u).")]
        [Min(0f)] public float RimMargin = PerchRunCourse.RimMargin;

        [Tooltip("Seconds a plan is kept before it is made again (the rim search asks the prism index up to 145 lines).")]
        [Min(0f)] public float ReplanSeconds = 0.1f;

        const int Directions = 24, Radii = 6;

        struct Plan
        {
            public int Ring;
            public float At;
            public Vector3 Aim;
            public float Length;
        }

        readonly Dictionary<GobyPistolDuo, Plan> _plans = new();

        public void Forget(GobyPistolDuo goby) => _plans.Remove(goby);

        public void Clear() => _plans.Clear();

        /// <summary>
        /// The dart from the Goby's hull at ring <paramref name="next"/> (and <paramref name="after"/>, the ring after it in
        /// race order, or null on the last). Length 0: cruise this frame (no clear line, or turning room first).
        /// </summary>
        public void Aim(GobyPistolDuo goby, int nextIndex, in RaceGate next, RaceGate? after, out Vector3 aim, out float length)
        {
            var hull = goby.RaceHull;
            float now = Time.time;
            if (_plans.TryGetValue(goby, out var plan) && plan.Ring == nextIndex && now - plan.At < ReplanSeconds)
            {
                aim = plan.Aim;
                length = plan.Length;
            }
            else
            {
                aim = PlanDart(goby, hull.position, next, after, out length);
                _plans[goby] = new Plan { Ring = nextIndex, At = now, Aim = aim, Length = length };
            }
            float radius = Mathf.Max(goby.RaceCruiseSpeed, 1f) / (Mathf.Max(goby.RaceTurnRate, 1f) * Mathf.Deg2Rad);
            if (PerchRunCourse.InsideTurn(hull.position, hull.forward, aim, radius, TurnMargin))
            {
                aim = hull.position + hull.forward * goby.RaceDartRange;
                length = 0f;
            }
        }

        Vector3 PlanDart(GobyPistolDuo goby, Vector3 from, in RaceGate next, RaceGate? after, out float length)
        {
            float min = goby.RaceDartMinRange, range = goby.RaceDartRange;
            if (after.HasValue && TryClearShot(goby, from, after.Value, out Vector3 far))
            {
                Vector3 d2 = far - from;
                float l2 = d2.magnitude;
                if (l2 + Overshoot <= range && PerchRunCourse.Threads(from, far, next, PerchRunCourse.HullRadius + RimMargin))
                {
                    length = Mathf.Clamp(l2 + Overshoot, min, range);
                    return far + d2 / Mathf.Max(1e-3f, l2) * Overshoot;
                }
            }
            if (!TryClearShot(goby, from, next, out Vector3 through))
            {
                // Nothing clears all the way: dart as far as the centre line goes (it stops on the block), never 0 - the
                // Goby's cruise flies through prisms, but at a sixth of a dart's pace.
                through = next.Position;
            }
            Vector3 to = through - from;
            float dist = to.magnitude;
            Vector3 dir = to / Mathf.Max(1e-3f, dist);
            length = Mathf.Clamp(dist + Overshoot, min, range);
            return through + dir * Overshoot;
        }

        /// <summary>A point inside <paramref name="ring"/>'s mouth that a dart from <paramref name="from"/> reaches
        /// without stopping on a block: the centre if clear, else the nearest clear point (24 directions x 6 radii).
        /// A ring further than one dart is tested for the dart's own length only (the next plan sees the rest).</summary>
        bool TryClearShot(GobyPistolDuo goby, Vector3 from, in RaceGate ring, out Vector3 through)
        {
            Vector3 e1 = RaceCourseGeometry.SafeNormalize(Vector3.Cross(ring.Axis, Vector3.up), Vector3.forward);
            Vector3 e2 = Vector3.Cross(ring.Axis, e1);
            float reach = Mathf.Max(0f, ring.Radius - PerchRunCourse.HullRadius - RimMargin);
            float range = goby.RaceDartRange;
            for (int ri = 0; ri <= Radii; ri++)
            {
                float o = reach * ri / Radii;
                for (int di = 0; di < (ri == 0 ? 1 : Directions); di++)
                {
                    float ang = di * (Mathf.PI * 2f / Directions);
                    through = ring.Position + (e1 * Mathf.Cos(ang) + e2 * Mathf.Sin(ang)) * o;
                    Vector3 to = through - from;
                    float len = to.magnitude;
                    if (len < 1f) continue;
                    Vector3 dir = to / len;
                    if (ri > 0 && Mathf.Abs(Vector3.Dot(dir, ring.Axis)) < 0.2f) continue;   // edge-on to the mouth: no shot
                    float test = Mathf.Min(len + PerchRunCourse.HullRadius + 2f, range);
                    if (goby.RaceDartStops(from, dir, test, out _)) continue;
                    return true;
                }
            }
            through = ring.Position;
            return false;
        }
    }
}
