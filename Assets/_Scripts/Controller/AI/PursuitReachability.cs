using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Why a pursuing AI ends up in a stable orbit around the thing it is chasing, and how to get
    /// it out. Pure math, no Unity state — <see cref="AIPilot"/> owns the state machine.
    ///
    /// <para><b>The problem is geometry, not tuning.</b> A vessel flying at speed <c>v</c> with a
    /// maximum turn rate <c>ω</c> cannot turn tighter than a circle of radius <c>R = v / ω</c>.
    /// Pure pursuit — "steer toward the target, hardest you can" — is therefore unable to reach any
    /// target lying INSIDE one of the two circles of radius <c>R</c> tangent to its own velocity:
    /// every frame it turns as hard as it can, every frame the target stays inside, and the result
    /// is a stable orbit that no amount of extra aggressiveness can break. Turning HARDER is
    /// precisely the wrong response, which is why the failure survives tuning.</para>
    ///
    /// <para>This is the classic <b>Dubins vehicle</b> reachability condition (Dubins, 1957): for a
    /// vehicle with a bounded turn radius the shortest path to a point inside its own turning
    /// circle is not a turn at all — it has to leave first. Pilots call the remedy <i>extend and
    /// re-attack</i>: fly out, come around, and come back in on an arc you can actually fly. Both
    /// halves of that fall out of the algebra below.</para>
    ///
    /// <para><b>The test reduces to one line.</b> With <c>d</c> the vector to the target, <c>f</c>
    /// the unit velocity direction, and <c>d⊥</c> the component of <c>d</c> across <c>f</c>, the
    /// turning circle on the target's side is centred at <c>C = R·(d⊥ / |d⊥|)</c> and the target is
    /// inside it when <c>|d − C| &lt; R</c>. Expanding and cancelling the <c>R²</c>:
    /// <code>
    ///   |d|² − 2R·|d⊥| + R² &lt; R²   ⟺   |d|² &lt; 2R·|d⊥|   ⟺   |d| &lt; 2R·sin θ
    /// </code>
    /// — no square roots beyond the two lengths, no trig, and it is exact rather than a heuristic.
    /// </para>
    ///
    /// <para><b>And the exit condition falls out of the same line.</b> <c>sin θ ≤ 1</c>, so a target
    /// further than <c>2R</c> away is reachable from ANY heading. "Get 2R of separation" is
    /// therefore a guarantee rather than a guess, and it is exactly the "fly away, turn around, fly
    /// back" the manoeuvre is named for: flying out grows <c>|d|</c> past <c>2R</c>, at which point
    /// ordinary pursuit can turn in.</para>
    /// </summary>
    public static class PursuitReachability
    {
        /// <summary>
        /// The tightest circle this vessel can fly: <c>R = v / ω</c>, with ω in radians.
        /// Returns <see cref="float.PositiveInfinity"/> for a vessel that cannot turn at all
        /// (which correctly reads as "nothing is reachable by turning") and 0 for one that is not
        /// moving (everything is reachable — it can spin in place).
        /// </summary>
        public static float MinTurnRadius(float speed, float turnRateDegreesPerSecond)
        {
            if (speed <= 0f) return 0f;
            if (turnRateDegreesPerSecond <= 0f) return float.PositiveInfinity;
            return speed / (turnRateDegreesPerSecond * Mathf.Deg2Rad);
        }

        /// <summary>
        /// Separation beyond which a target is reachable from ANY heading — <c>2R</c>, the diameter
        /// of the turning circle. See the class summary: the reachability test is
        /// <c>|d| &lt; 2R·sin θ</c> and <c>sin θ</c> cannot exceed 1.
        /// </summary>
        public static float GuaranteedReachableSeparation(float minTurnRadius) =>
            float.IsInfinity(minTurnRadius) ? float.PositiveInfinity : 2f * minTurnRadius;

        /// <summary>
        /// True when <paramref name="toTarget"/> lies inside the turning circle on its own side —
        /// i.e. when no amount of turning can bring this vessel onto it, and pure pursuit will
        /// orbit instead.
        ///
        /// <paramref name="heading"/> is the direction of TRAVEL, not the nose: on a drifting
        /// vessel the two differ and it is the velocity the turn radius applies to.
        /// It need not be normalized.
        ///
        /// A zero-length heading or target is reported reachable — there is no orbit to break out
        /// of, and inventing one would send a stationary or coincident vessel on an escape run.
        /// </summary>
        public static bool IsInsideTurningCircle(Vector3 toTarget, Vector3 heading, float minTurnRadius)
        {
            if (minTurnRadius <= 0f) return false;

            float distanceSqr = toTarget.sqrMagnitude;
            if (distanceSqr <= Mathf.Epsilon) return false;

            Vector3 forward = heading.normalized;
            if (forward.sqrMagnitude <= Mathf.Epsilon) return false;

            // An unturnable vessel can reach nothing off its nose, and the algebra below would
            // otherwise multiply infinity by a lateral offset of zero.
            if (float.IsInfinity(minTurnRadius))
                return Vector3.Cross(toTarget, forward).sqrMagnitude > Mathf.Epsilon;

            float lateral = Vector3.Cross(toTarget, forward).magnitude;   // = |d| sin θ = |d⊥|
            return distanceSqr < 2f * minTurnRadius * lateral;
        }

        /// <summary>
        /// Where to fly while extending. The cheapest escape is the one that costs the least
        /// TURNING — a vessel already committed to a hard turn gains separation fastest by rolling
        /// out and flying the tangent, not by hauling around 180° to point away first (which keeps
        /// it near the target for the whole reversal). So the escape heading is the current
        /// heading, biased away from the target by <paramref name="awayBias"/>.
        ///
        /// <paramref name="awayBias"/> 0 flies dead ahead; 1 splits the difference between ahead
        /// and directly away. Values above ~1 start reintroducing the hard turn the manoeuvre
        /// exists to avoid.
        ///
        /// Falls back to the heading when the two cancel (the vessel is pointed exactly at the
        /// target and the bias is 1), which is the degenerate case where flying straight through
        /// IS the escape.
        /// </summary>
        public static Vector3 EscapeDirection(Vector3 toTarget, Vector3 heading, float awayBias)
        {
            Vector3 forward = heading.normalized;
            if (forward.sqrMagnitude <= Mathf.Epsilon) return Vector3.forward;

            Vector3 away = -toTarget.normalized;
            if (away.sqrMagnitude <= Mathf.Epsilon) return forward;

            Vector3 blended = forward + away * Mathf.Max(0f, awayBias);
            return blended.sqrMagnitude > 1e-6f ? blended.normalized : forward;
        }
    }

    /// <summary>
    /// The empirical backstop to <see cref="PursuitReachability"/>'s geometric test: notices that a
    /// pursuer has circled its target without getting closer, whatever the cause.
    ///
    /// <para>The turning-circle test catches the orbit that BOUNDED TURN RATE causes, which is the
    /// common one and the only one that can be predicted before it happens. It cannot catch an
    /// orbit produced by anything else — a target that keeps moving, a throttle that keeps the
    /// vessel wide, an avoidance impulse fighting the pursuit — and those look identical from the
    /// outside. So this measures the SYMPTOM instead: angle swept around the target, with no
    /// progress made.</para>
    ///
    /// <para>Swept angle is accumulated UNSIGNED. A signed sweep needs a reference axis and in 3D
    /// there is no natural one; the "no progress" gate is what separates a genuine spiralling
    /// approach (which sweeps angle AND closes) from an orbit (which only sweeps).</para>
    /// </summary>
    public struct OrbitDetector
    {
        Vector3 _lastBearing;
        bool _hasBearing;
        float _sweptDegrees;
        float _bestDistance;

        /// <summary>Angle swept around the target since the last real progress, in degrees.</summary>
        public float SweptDegrees => _sweptDegrees;

        /// <summary>Forget everything. Call when the objective changes or the pursuit restarts.</summary>
        public void Reset()
        {
            _hasBearing = false;
            _sweptDegrees = 0f;
            _bestDistance = float.PositiveInfinity;
        }

        /// <summary>
        /// Feed one frame. Returns true once the pursuer has swept
        /// <paramref name="orbitSweepDegrees"/> around the target without ever closing to
        /// <paramref name="progressFraction"/> of its best distance.
        ///
        /// <paramref name="targetJumpFraction"/> guards the case that would otherwise produce a
        /// false positive out of nowhere: the objective being REPLACED (a crystal collected, a new
        /// one selected) teleports the bearing and the distance, and the accumulated sweep from the
        /// old target means nothing about the new one.
        /// </summary>
        public bool Tick(Vector3 toTarget, float orbitSweepDegrees, float progressFraction,
                         float targetJumpFraction)
        {
            float distance = toTarget.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                Reset();
                return false;
            }

            Vector3 bearing = toTarget / distance;

            if (!_hasBearing)
            {
                _hasBearing = true;
                _lastBearing = bearing;
                _bestDistance = distance;
                _sweptDegrees = 0f;
                return false;
            }

            // A discontinuity in range is a different target, not a manoeuvre.
            if (distance > _bestDistance * targetJumpFraction)
            {
                _lastBearing = bearing;
                _bestDistance = distance;
                _sweptDegrees = 0f;
                return false;
            }

            _sweptDegrees += Vector3.Angle(_lastBearing, bearing);
            _lastBearing = bearing;

            // Real closing resets the case for the defence. The comparison is against the range at
            // the START of this accumulation window, NOT the previous frame's and NOT a running
            // minimum: a running minimum ratchets down with every frame of a steady approach, so
            // the current distance is always within a hair of it and this test can never fire —
            // which silently turns the whole detector into something that only recognises an orbit
            // at EXACTLY constant range. (It shipped that way for about ten minutes and a closing
            // spiral was reported as an orbit; the window reference is what makes the gate mean
            // "have we actually got meaningfully closer since we started worrying".)
            if (distance < _bestDistance * progressFraction)
            {
                _bestDistance = distance;
                _sweptDegrees = 0f;
                return false;
            }

            return _sweptDegrees >= orbitSweepDegrees;
        }
    }
}
