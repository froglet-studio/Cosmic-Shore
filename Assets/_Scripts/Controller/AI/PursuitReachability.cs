using System.Collections.Generic;
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
    ///
    /// <para><b>A pursuer does not need to reach a POINT — it needs to pass within the objective's
    /// capture radius</b>, and leaving that out is a real defect rather than a refinement. With
    /// <c>c = 0</c> the test asks whether the vessel can fly onto an infinitely small target, which
    /// at 20 units of range is false for any bearing error over 7°, so an AI on final approach
    /// peels away from a crystal it was about to collect. The generalisation is exact and changes
    /// one term: the objective is truly unreachable only when its whole capture sphere sits inside
    /// the turning circle, <c>|d − C| &lt; R − c</c>, which expands to
    /// <code>
    ///   |d|² + 2Rc − c² &lt; 2R·|d⊥|
    /// </code>
    /// and reduces to the line above at <c>c = 0</c>. The guaranteed separation moves with it, to
    /// <c>2R − c</c> — the same quadratic's other root is <c>|d| ≤ c</c>, i.e. "already inside the
    /// capture sphere", so the do-not-peel-away-on-final-approach case is not a special case at
    /// all: it is the second half of the solution. At <c>c ≥ R</c> nothing is ever unreachable,
    /// which is correct — a slow vessel with a generous capture radius can always clip its
    /// objective.</para>
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
        /// Separation beyond which a target is reachable from ANY heading — <c>2R − c</c>, the
        /// turning circle's diameter less the objective's capture radius. See the class summary:
        /// the reachability test is <c>|d| &lt; 2R·sin θ</c> (capture-adjusted) and <c>sin θ</c>
        /// cannot exceed 1.
        /// </summary>
        public static float GuaranteedReachableSeparation(float minTurnRadius, float captureRadius = 0f) =>
            float.IsInfinity(minTurnRadius)
                ? float.PositiveInfinity
                : Mathf.Max(0f, 2f * minTurnRadius - Mathf.Max(0f, captureRadius));

        /// <summary>
        /// True when <paramref name="toTarget"/> lies inside the turning circle on its own side —
        /// i.e. when no amount of turning can bring this vessel onto it, and pure pursuit will
        /// orbit instead.
        ///
        /// <paramref name="heading"/> is the direction of TRAVEL, not the nose: on a drifting
        /// vessel the two differ and it is the velocity the turn radius applies to.
        /// It need not be normalized.
        ///
        /// <paramref name="captureRadius"/> is how close the vessel has to PASS to count as having
        /// arrived — a crystal's collect radius, a ball's contact radius. It defaults to 0, which
        /// asks the stricter question "can it fly onto the exact point"; that is almost never what
        /// a pursuer actually needs, and using it made AI peel away from crystals on final approach.
        ///
        /// A zero-length heading or target is reported reachable — there is no orbit to break out
        /// of, and inventing one would send a stationary or coincident vessel on an escape run.
        /// </summary>
        public static bool IsInsideTurningCircle(Vector3 toTarget, Vector3 heading, float minTurnRadius,
                                                 float captureRadius = 0f)
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

            float capture = Mathf.Max(0f, captureRadius);
            float lateral = Vector3.Cross(toTarget, forward).magnitude;   // = |d| sin θ = |d⊥|

            // |d - C| < R - c, expanded, with the R² cancelled. At c = 0 this is |d|² < 2R|d⊥|.
            return distanceSqr + 2f * minTurnRadius * capture - capture * capture
                   < 2f * minTurnRadius * lateral;
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

    /// <summary>
    /// Which objective a pursuing AI should fly at. Pure and list-based so the SHIPPED selection is
    /// the tested one — the alternative, a helper the pilot uses and a reference the tests use, is
    /// two implementations that agree until they do not.
    ///
    /// <para>It exists because the original one-line selection carried a defect that is invisible
    /// on inspection and unmistakable in play: it compared a squared distance against
    /// <c>MinDistance * MinDistance</c> while <c>MinDistance</c> already held a squared distance,
    /// making the threshold <c>d⁴</c>. Every candidate after the first passed, so the pilot took
    /// the LAST eligible item rather than the nearest — and since the item list re-orders as
    /// crystals are collected and respawned, and every respawn re-runs the selection, its objective
    /// jumped to an arbitrary crystal mid-approach. On screen that is an AI swerving away from a
    /// crystal it was about to collect.</para>
    /// </summary>
    public static class AIObjectiveScoring
    {
        /// <summary>
        /// How good an objective is, lower being better.
        ///
        /// Plain range by default. With <paramref name="preferApproachRun"/> it is instead the
        /// distance from the RUN-UP the pilot wants, so it picks something it can make a proper run
        /// at rather than whatever is under its nose — which matters for a vessel that has to line
        /// something up on the way in. Skipping a near objective costs nothing, because collecting
        /// is physical: the pilot still picks up whatever it flies through.
        /// </summary>
        public static float Score(float distance, bool preferApproachRun, float desiredRun) =>
            preferApproachRun ? Mathf.Abs(distance - desiredRun) : distance;

        /// <summary>
        /// Index of the objective to fly at, or -1 when there are none.
        ///
        /// <paramref name="heldIndex"/> is the objective already committed to (-1 if none or if it
        /// is gone). A committed approach is only abandoned for a candidate scoring at or below
        /// <paramref name="switchImprovement"/> of it — otherwise any crystal event anywhere in the
        /// cell can re-point a pilot that was a second from arriving, which is the same swerve the
        /// class summary describes arriving by a different route.
        /// </summary>
        public static int Select(IReadOnlyList<float> distances, int heldIndex,
                                 bool preferApproachRun, float desiredRun, float switchImprovement)
        {
            if (distances == null || distances.Count == 0) return -1;

            int best = -1;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < distances.Count; i++)
            {
                float score = Score(distances[i], preferApproachRun, desiredRun);
                if (score >= bestScore) continue;
                bestScore = score;
                best = i;
            }

            if (heldIndex < 0 || heldIndex >= distances.Count || heldIndex == best) return best;

            float heldScore = Score(distances[heldIndex], preferApproachRun, desiredRun);
            return bestScore > heldScore * switchImprovement ? heldIndex : best;
        }
    }
}
