using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pure arithmetic of the Stoat's slingshot (<c>R_VesselActions/STOAT.md</c> §2): what a
    /// squeeze is worth and where the pair goes. No scene, no time, no input device — every method
    /// is a function of its arguments, so <c>StoatSlingTests</c> hold it directly and the executor
    /// only applies it.
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
        /// ~the deadzone, and slinging on it would throw every gamepad pair at minimum size. The
        /// keyboard ramp and the autopilot's fixed squeeze never fall, so for them peak == latest.
        /// </summary>
        public static float Peak(float peakSoFar, float sample) => Mathf.Max(Mathf.Clamp01(peakSoFar), Mathf.Clamp01(sample));

        /// <summary>
        /// The attractor's strength for a squeeze: <paramref name="minStrength"/> at a touch,
        /// <paramref name="maxStrength"/> fully buried, along <c>hold^exponent</c> so a light press
        /// stays a nudge.
        /// </summary>
        public static float Strength(float hold01, float minStrength, float maxStrength, float exponent)
        {
            float t = Mathf.Pow(Mathf.Clamp01(hold01), Mathf.Max(0.01f, exponent));
            return Mathf.Lerp(minStrength, Mathf.Max(minStrength, maxStrength), t);
        }

        /// <summary>
        /// The pair's axis, which runs black → white (<c>BlackHolePairMath.Positions</c> puts the
        /// attractor at −axis). The attractor lands on the PRESSED side: the left trigger wants
        /// it on the left, so the axis points right; the right trigger mirrors it.
        /// </summary>
        public static Vector3 PairAxis(bool blackOnLeft, Vector3 hullRight) => blackOnLeft ? hullRight : -hullRight;

        /// <summary>
        /// The autopilot's choice: given its target in the HULL's local frame, sling when the target
        /// is at least <paramref name="minTurnDegrees"/> off the nose on the hull's horizontal and
        /// <paramref name="minDistance"/> away, with the ATTRACTOR on the side it wants to turn
        /// (target to the left, x &lt; 0, → black on the left). A target behind the hull counts as
        /// a full turn toward its side.
        /// </summary>
        public static bool TryAutopilotSide(Vector3 targetLocal, float minTurnDegrees, float minDistance, out bool blackOnLeft)
        {
            blackOnLeft = targetLocal.x < 0f;
            var flat = new Vector2(targetLocal.x, targetLocal.z);
            if (flat.sqrMagnitude < 1e-6f || targetLocal.magnitude < minDistance) return false;
            float offNose = Mathf.Abs(Mathf.Atan2(targetLocal.x, targetLocal.z)) * Mathf.Rad2Deg;
            return offNose >= minTurnDegrees;
        }

        // ------------------------------------------------------------------ the orbit (drift pair)

        /// <summary>
        /// The orbit radius a squeeze asks for: <paramref name="wide"/> at a touch, <paramref name="tight"/>
        /// fully buried, along <c>hold^exponent</c> — squeeze harder, turn tighter.
        /// </summary>
        public static float OrbitRadius(float hold01, float wide, float tight, float exponent)
        {
            float t = Mathf.Pow(Mathf.Clamp01(hold01), Mathf.Max(0.01f, exponent));
            float w = Mathf.Max(1f, wide);
            return Mathf.Lerp(w, Mathf.Clamp(tight, 1f, w), t);
        }

        /// <summary>
        /// The horizon of the hole an orbit of <paramref name="radius"/> circles, <paramref name="orbitHorizons"/>
        /// horizon radii out. Floored at 3: inside 3 r_s (the Paczyński–Wiita ISCO) no circular orbit is
        /// stable, so a hole is never laid that close.
        /// </summary>
        public static float OrbitHorizon(float radius, float orbitHorizons) => Mathf.Max(0f, radius) / Mathf.Max(3f, orbitHorizons);

        /// <summary>
        /// The gravitational parameter that makes <paramref name="radius"/> a CIRCULAR orbit at
        /// <paramref name="speed"/> under the Paczyński–Wiita law the holes pull with:
        /// <c>v² / r = GM / (r − r_s)²</c>. The hole's strength is chosen, not authored — the right mass
        /// for the turn the squeeze asked for at the speed the pilot is flying.
        /// </summary>
        public static float CircularOrbitGM(float speed, float radius, float horizon)
        {
            float r = Mathf.Max(radius, 1e-3f);
            float gap = Mathf.Max(r - Mathf.Max(0f, horizon), 1e-3f);
            return speed * speed * gap * gap / r;
        }

        /// <summary>
        /// The next heading on the orbit: <paramref name="forward"/> with its radial part removed (the
        /// tangent), turned toward the centre (along <paramref name="toCentre"/>) by
        /// <paramref name="angleRadians"/>. A nose pointed straight at or away from the centre has no
        /// tangent and is returned as is.
        /// </summary>
        public static Vector3 OrbitHeading(Vector3 forward, Vector3 toCentre, float angleRadians)
        {
            if (toCentre.sqrMagnitude < 1e-8f) return forward;
            var u = toCentre.normalized;
            var t = forward - Vector3.Dot(forward, u) * u;
            if (t.sqrMagnitude < 1e-8f) return forward;
            t.Normalize();
            return Quaternion.AngleAxis(angleRadians * Mathf.Rad2Deg, Vector3.Cross(t, u)) * t;
        }

        /// <summary>
        /// How far to move the hull toward the centre this step to settle on <paramref name="targetRadius"/>
        /// at <paramref name="rate"/> per second (negative = outward).
        /// </summary>
        public static float RadialCorrection(float distance, float targetRadius, float rate, float dt) =>
            (distance - targetRadius) * (1f - Mathf.Exp(-Mathf.Max(0f, rate) * Mathf.Max(0f, dt)));

        /// <summary>The slingshot on release: <paramref name="speed"/> × lerp(min, max, the deepest squeeze), u/s.</summary>
        public static float SlingBoost(float speed, float peak01, float minFraction, float maxFraction) =>
            Mathf.Max(0f, speed) * Mathf.Lerp(Mathf.Max(0f, minFraction), Mathf.Max(0f, maxFraction), Mathf.Clamp01(peak01));

        /// <summary>Seconds an autopilot holds its squeeze to turn <paramref name="turnRadians"/> on an orbit of
        /// <paramref name="radius"/> at <paramref name="speed"/>: the arc over the speed.</summary>
        public static float AutopilotHoldSeconds(float turnRadians, float speed, float radius) =>
            Mathf.Abs(turnRadians) * Mathf.Max(1f, radius) / Mathf.Max(1f, speed);

        /// <summary>The pair's midpoint: <paramref name="aheadHorizons"/> horizon radii ahead of the hull.</summary>
        public static Vector3 Midpoint(Vector3 hullPosition, Vector3 hullForward, float horizonRadius, float aheadHorizons)
            => hullPosition + hullForward * (Mathf.Max(0f, horizonRadius) * Mathf.Max(0f, aheadHorizons));
    }
}
