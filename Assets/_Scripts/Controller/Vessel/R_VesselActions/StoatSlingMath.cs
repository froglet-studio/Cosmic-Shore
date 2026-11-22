using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pure arithmetic of the Stoat's slingshot, the SWING (<c>R_VesselActions/STOAT.md</c> §2, <c>STOAT_DIPOLE.md</c> §1b):
    /// what a squeeze is worth, where the hole goes, how far and how fast it turns you, and when the autopilot presses and lets
    /// go. No scene, no time, no input device — every method is a function of its arguments, so <c>StoatSlingTests</c> hold it
    /// directly and the executor only applies it. The catch orbit's, the aimed exit's, the trampoline's and the crystal
    /// style's arithmetic was deleted with them (2026-11-21).
    /// </summary>
    public static class StoatSlingMath
    {
        /// <summary>
        /// The squeeze, 0–1. A device with real analog triggers reports its travel; one without
        /// (keyboard, mouse, touch all write a binary 0/1 into the same field) ramps with hold time
        /// over <paramref name="rampSeconds"/>, so "hold longer = bigger" is the same verb
        /// everywhere; the autopilot writes no trigger and takes a fixed squeeze.
        /// </summary>
        public static float Hold01(float rawAnalog, float heldSeconds, bool hasAnalogTriggers, float rampSeconds,
            bool autopilot, float autopilotHold01)
        {
            if (autopilot) return Mathf.Clamp01(autopilotHold01);
            if (hasAnalogTriggers) return Mathf.Clamp01(rawAnalog);
            return rampSeconds > 0f ? Mathf.Clamp01(heldSeconds / rampSeconds) : 1f;
        }

        /// <summary>
        /// The squeeze a hold has reached: the DEEPEST sample so far, never the latest. A real trigger
        /// is let go over several frames — its travel sweeps back down through the samples before
        /// it crosses the deadzone and the release edge fires — so the latest sample at release is
        /// ~the deadzone. The keyboard ramp and the autopilot's fixed squeeze never fall, so for them peak == latest.
        /// </summary>
        public static float Peak(float peakSoFar, float sample) => Mathf.Max(Mathf.Clamp01(peakSoFar), Mathf.Clamp01(sample));

        // ------------------------------------------------------------------ the autopilot's press

        /// <summary>
        /// The autopilot's turn: given its target in the HULL's local frame, press when the target
        /// is at least <paramref name="minTurnDegrees"/> off the nose on the hull's horizontal and
        /// <paramref name="minDistance"/> away, on the side it wants to turn (target to the left,
        /// x &lt; 0, → the left trigger). A target behind the hull counts as a full turn toward its side.
        /// </summary>
        public static bool TryAutopilotSide(Vector3 targetLocal, float minTurnDegrees, float minDistance, out bool turnLeft)
        {
            turnLeft = targetLocal.x < 0f;
            var flat = new Vector2(targetLocal.x, targetLocal.z);
            if (flat.sqrMagnitude < 1e-6f || targetLocal.magnitude < minDistance) return false;
            float offNose = Mathf.Abs(Mathf.Atan2(targetLocal.x, targetLocal.z)) * Mathf.Rad2Deg;
            return offNose >= minTurnDegrees;
        }

        /// <summary>
        /// Whether an autopilot presses for the warp alone: its target at least <paramref name="warpDistance"/> away, at any
        /// bearing (0 = never). <see cref="TryAutopilotSide"/> never fires with the target dead ahead, so without this a
        /// lined-up AI never earns the swing's warp down a straight.
        /// </summary>
        public static bool WantsWarp(Vector3 targetLocal, float warpDistance) =>
            warpDistance > 0f && targetLocal.magnitude >= warpDistance;

        /// <summary>
        /// Whether the autopilot presses, and on which side the hole goes: for a turn it wants (<see cref="TryAutopilotSide"/>),
        /// or for the warp alone on a long leg (<see cref="WantsWarp"/>; 0 = never).
        /// </summary>
        public static bool AutopilotPress(Vector3 targetLocal, float minTurnDegrees, float minDistance, float warpDistance,
            out bool turnLeft) =>
            TryAutopilotSide(targetLocal, minTurnDegrees, minDistance, out turnLeft) | WantsWarp(targetLocal, warpDistance);

        // ------------------------------------------------------------------ the fling's (optional) push and its momentum

        /// <summary>The push at a fling: <paramref name="speed"/> × lerp(min, max, the deepest squeeze), u/s.</summary>
        public static float SlingBoost(float speed, float peak01, float minFraction, float maxFraction) =>
            Mathf.Max(0f, speed) * Mathf.Lerp(Mathf.Max(0f, minFraction), Mathf.Max(0f, maxFraction), Mathf.Clamp01(peak01));

        /// <summary>The push a fling may add: <paramref name="kick"/>, but never past <paramref name="capShare"/> × the speed at
        /// the fling.</summary>
        public static float CappedKick(float kick, float releaseSpeed, float capShare) =>
            Mathf.Clamp(kick, 0f, Mathf.Max(0f, (capShare - 1f) * releaseSpeed));

        /// <summary>
        /// The push, capped so <paramref name="flightClock"/> × the push (the speed the world sees it add) is never past
        /// <paramref name="capCruise"/> × <paramref name="cruiseSpeed"/>. A clock below 1 does not raise the cap.
        /// </summary>
        public static float WarpCappedKick(float kick, float flightClock, float cruiseSpeed, float capCruise) =>
            Mathf.Clamp(kick, 0f, Mathf.Max(0f, capCruise) * Mathf.Max(0f, cruiseSpeed) / Mathf.Max(1f, flightClock));

        /// <summary>
        /// A fling's momentum as a flight-clock warp: the swing's <paramref name="releaseClock"/> × the
        /// <paramref name="timing01"/>, fading linearly to 1 over <paramref name="seconds"/>.
        /// </summary>
        public static float MomentumWarp(float releaseClock, float timing01, float age, float seconds)
        {
            if (seconds <= 0f || age >= seconds || age < 0f) return 1f;
            float top = 1f + (Mathf.Max(1f, releaseClock) - 1f) * Mathf.Clamp01(timing01);
            return 1f + (top - 1f) * (1f - age / seconds);
        }

        /// <summary>Space-5 Accretion: <paramref name="captured"/> × <paramref name="perPrism"/>, clamped to 0..<paramref name="max"/>.</summary>
        public static float AccretionBonus(int captured, float perPrism, float max) =>
            Mathf.Clamp(Mathf.Max(0, captured) * Mathf.Max(0f, perPrism), 0f, Mathf.Max(0f, max));

        // ------------------------------------------------------------------ the swing (STOAT_DIPOLE.md §1b)

        /// <summary>Where the swing's black hole is laid: <paramref name="ahead"/> along the nose and <paramref name="side"/> to
        /// the pressed side (RT right, LT left), so flying on you pass beside it on that side.</summary>
        public static Vector3 SwingHole(Vector3 position, Vector3 forward, Vector3 right, bool pressedRight, float ahead, float side)
        {
            var f = forward.sqrMagnitude > 1e-8f ? forward.normalized : Vector3.forward;
            var r = right.sqrMagnitude > 1e-8f ? right.normalized : Vector3.right;
            return position + f * Mathf.Max(0f, ahead) + r * ((pressedRight ? 1f : -1f) * Mathf.Max(0f, side));
        }

        /// <summary>The swing starts: within <paramref name="reach"/> of the hole, with the hole at least
        /// <paramref name="catchDegrees"/> off the nose (passing beside it, not flying into it).</summary>
        public static bool SwingStarts(Vector3 hull, Vector3 forward, Vector3 hole, float reach, float catchDegrees)
        {
            var toHole = hole - hull;
            return toHole.sqrMagnitude <= reach * reach && Vector3.Angle(forward, toHole) >= catchDegrees;
        }

        /// <summary>The swing's turn axis, fixed at its start: turning the nose about it by a positive angle turns it toward the
        /// hole (the plane of the nose and the hole). Dead ahead or behind, the hull's up.</summary>
        public static Vector3 SwingAxis(Vector3 forward, Vector3 toHole, Vector3 fallbackUp)
        {
            var axis = Vector3.Cross(forward, toHole);
            if (axis.sqrMagnitude < 1e-8f) axis = fallbackUp;
            return axis.sqrMagnitude > 1e-8f ? axis.normalized : Vector3.up;
        }

        /// <summary>
        /// The angle a swing turns the hull, read from the stick: the stick's share toward the hole (the stick
        /// <paramref name="stickX"/> right / <paramref name="stickY"/> up against the hole's direction in the hull's frame,
        /// <paramref name="holeX"/> / <paramref name="holeY"/>) maps <paramref name="minDegrees"/> (away) .. the middle (at rest)
        /// .. <paramref name="maxDegrees"/> (toward). Read every frame, so moving the stick moves the exit.
        /// </summary>
        public static float SwingTurnDegrees(float stickX, float stickY, float holeX, float holeY, float minDegrees, float maxDegrees)
        {
            var hole = new Vector2(holeX, holeY);
            float toward = hole.sqrMagnitude > 1e-8f ? Vector2.Dot(new Vector2(stickX, stickY), hole.normalized) : 0f;
            return Mathf.Lerp(minDegrees, maxDegrees, 0.5f + 0.5f * Mathf.Clamp(toward, -1f, 1f));
        }

        /// <summary>How fast a swing turns the hull, degrees a second: its speed over its distance from the hole (ω = v / d, as a
        /// circle round the hole), never faster than <paramref name="maxDegreesPerSecond"/>.</summary>
        public static float SwingRate(float speed, float distance, float maxDegreesPerSecond) =>
            Mathf.Min(Mathf.Max(0f, speed) / Mathf.Max(1f, distance) * Mathf.Rad2Deg, Mathf.Max(0f, maxDegreesPerSecond));

        /// <summary>The autopilot lets the swing throw it once it has turned at least <paramref name="minDegrees"/> and either faces
        /// its target (within <paramref name="faceDegrees"/>) or has its target no longer on the swing's side
        /// (<paramref name="targetOnTurnSide"/> false: turning on would turn it away - a press for the warp alone), or once it has
        /// turned <paramref name="maxDegrees"/>.</summary>
        public static bool SwingAiLetsGo(float turnedDegrees, float offTargetDegrees, float minDegrees, float maxDegrees, float faceDegrees,
            bool targetOnTurnSide) =>
            turnedDegrees >= maxDegrees || (turnedDegrees >= minDegrees && (offTargetDegrees <= faceDegrees || !targetOnTurnSide));

        /// <summary>The target is on the swing's side of the nose: turning on about <paramref name="axis"/> brings the nose toward
        /// it.</summary>
        public static bool OnSwingSide(Vector3 forward, Vector3 toTarget, Vector3 axis) =>
            Vector3.Dot(Vector3.Cross(forward, toTarget), axis) > 0f;
    }
}
