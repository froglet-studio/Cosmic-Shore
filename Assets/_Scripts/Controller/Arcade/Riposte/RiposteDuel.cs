using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Riposte AI's plan, as pure functions (RIPOSTE.md section 3; held by <c>RiposteDuelTests</c>). A Swordfish's
    /// sword lands only while it is the faster hull, its speed is a ramp that winds up on a straight line, and the ramp
    /// runs on what the bill slices. So a duelist flies three legs, each a long straight line:
    /// <list type="bullet">
    /// <item><b>Reap</b>: across the heap, cutting sticks for fuel.</item>
    /// <item><b>Charge</b>: through a rival and on past them.</item>
    /// <item><b>Extend</b>: straight out after the pass, to make room for the next wind-up.</item>
    /// </list>
    /// </summary>
    public static class RiposteDuel
    {
        public enum Phase
        {
            Charge = 0,
            Extend = 1,
            Reap = 2,
        }

        /// <summary>
        /// The leg after this one. <paramref name="passed"/>: the charge went by its rival. <paramref name="arrived"/>:
        /// the run-out or cutting line reached its end. <paramref name="timedOut"/>: the leg ran its time.
        /// <paramref name="reapBelow"/> 0 (or less) never sends the pilot to cut on purpose.
        /// </summary>
        public static Phase Next(Phase phase, float fuel01, float reapBelow, float reapUntil, bool hasRival,
                                 bool passed, bool arrived, bool timedOut)
        {
            bool wantsFuel = reapBelow > 0f && fuel01 < reapBelow;
            switch (phase)
            {
                case Phase.Charge:
                    if (!hasRival) return reapBelow > 0f && fuel01 < reapUntil ? Phase.Reap : Phase.Charge;
                    if (passed || timedOut) return Phase.Extend;
                    return Phase.Charge;

                case Phase.Extend:
                    if (!arrived && !timedOut) return Phase.Extend;
                    return wantsFuel ? Phase.Reap : Phase.Charge;

                default:
                    if (fuel01 >= reapUntil) return Phase.Charge;
                    // A cutting line that ended short of the tank it wanted is followed by another one.
                    return Phase.Reap;
            }
        }

        /// <summary>
        /// True when a charge is spent: the rival is behind the nose and inside <paramref name="passRadius"/>, or the
        /// hulls are inside a third of it and this one is not the faster (a sword lands only on a slower victim, so
        /// staying is a slow knot).
        /// </summary>
        public static bool Passed(Vector3 self, Vector3 forward, float speed, Vector3 rival, float rivalSpeed,
                                  float passRadius)
        {
            Vector3 to = rival - self;
            float sqr = to.sqrMagnitude;
            if (sqr > passRadius * passRadius) return false;
            if (Vector3.Dot(forward, to) < 0f) return true;
            float knot = passRadius / 3f;
            return sqr <= knot * knot && speed <= rivalSpeed;
        }

        /// <summary>Where a charge steers: through <paramref name="aim"/> (the led rival, off by the plan's miss) and
        /// <paramref name="overshoot"/> on past it.</summary>
        public static Vector3 ChargePoint(Vector3 self, Vector3 aim, float overshoot)
        {
            Vector3 run = aim - self;
            return run.sqrMagnitude > 1e-4f ? aim + run.normalized * overshoot : aim;
        }

        /// <summary>The end of a run-out: <paramref name="distance"/> straight ahead, kept inside the arena ball.</summary>
        public static Vector3 ExtendPoint(Vector3 self, Vector3 forward, float distance, Vector3 centre, float arenaRadius)
        {
            Vector3 dir = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            return ClampToBall(self + dir * distance, centre, arenaRadius);
        }

        /// <summary>
        /// The end of a cutting line: the far side of the heap from <paramref name="self"/>, turned off the line through
        /// the centre by up to <paramref name="spreadDegrees"/> (<paramref name="u"/>, <paramref name="v"/> in 0..1
        /// pick the turn and its direction), on the heap's surface. A pilot at the centre gets a point straight ahead
        /// of nothing in particular: +z, turned the same way.
        /// </summary>
        public static Vector3 ReapPoint(Vector3 self, Vector3 centre, float heapRadius, float u, float v,
                                        float spreadDegrees)
        {
            Vector3 across = centre - self;
            Vector3 dir = across.sqrMagnitude > 1f ? across.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 tilt = Quaternion.AngleAxis(360f * Mathf.Clamp01(v), dir) * side;
            Vector3 turned = Quaternion.AngleAxis(spreadDegrees * Mathf.Clamp01(u), tilt) * dir;
            return centre + turned * heapRadius;
        }

        /// <summary><paramref name="point"/>, pulled onto the ball round <paramref name="centre"/> when outside it.</summary>
        public static Vector3 ClampToBall(Vector3 point, Vector3 centre, float radius)
        {
            Vector3 off = point - centre;
            return off.sqrMagnitude <= radius * radius ? point : centre + off.normalized * radius;
        }
    }
}
