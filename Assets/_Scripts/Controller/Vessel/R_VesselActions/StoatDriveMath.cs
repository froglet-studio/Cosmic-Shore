using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pure arithmetic of the Stoat's DRIVE MODE (<c>R_VesselActions/STOAT_DIPOLE.md</c> §1f), B's third press: a
    /// WARP PAIR that rides with the hull. A phantom black hole sits ahead of the nose and its white hole behind, mirrored
    /// through the hull, and the triggers shape it as they shape Mass mode's dipole:
    /// <list type="bullet">
    /// <item>their SUM is the pair's LENGTH, and a longer pair warps the flight clock more (the long legs);</item>
    /// <item>their DIFFERENCE slides the black hole to the deeper trigger's side (the white hole to the other): the pair
    /// TILTS, and the tilt turns the hull toward the black hole.</item>
    /// </list>
    /// So the pair's shape is the dial between agility and precision: a short pair pulled sideways carves, a long straight
    /// pair travels, a light squeeze with a small difference trims. The Vessel Studio's <c>driveShape</c> /
    /// <c>stepDriveBoost</c> / the drive branch of <c>aiInput</c> (<c>Docs/Studios/StoatFlightStudio.html</c>), line for
    /// line. No scene, no time, no input device: <c>StoatSlingTests</c> hold it directly.
    /// </summary>
    public static class StoatDriveMath
    {
        /// <summary>The pair's geometry and what it does, from <see cref="StoatModeActionSO"/>'s drive rows.</summary>
        public struct Settings
        {
            public float LengthMin;      // u ahead (and behind) with the triggers barely squeezed
            public float LengthMax;      // u with both triggers full
            public float LateralMax;     // u the black hole slides sideways at a full difference
            public float TurnGain;       // deg/s of turn per degree of tilt
            public float TurnMax;        // deg/s, the fastest the pair turns the hull
            public float WarpExponent;   // the warp's curve over the stretch
        }

        /// <summary>The autopilot's squeeze rule (the studio's <c>drAi*</c> rows).</summary>
        public struct AutopilotSettings
        {
            public float Near;           // u: no stretch this close to the target
            public float Far;            // u: full stretch from this far
            public float ConeDegrees;    // no stretch with the target this far off the nose
            public float LeanDegrees;    // a full difference at this much yaw still to make
        }

        /// <summary>One frame's pair.</summary>
        public struct Shape
        {
            public float Span;           // 0..1, the triggers' mean: the stretch
            public float Lean;           // -1..1, right minus left: + = the black hole to the right
            public float HalfLength;     // u, the black hole ahead (and the white hole behind)
            public float Lateral;        // u, the black hole's sideways offset (+ = right)
            public float TiltDegrees;    // the pair's axis off the nose, signed like Lean
            public float TurnRate;       // deg/s of FLIGHT time, + = right (yaw about the hull's up)
            public float Warp01;         // 0..1, the share of the top warp the stretch asks for
        }

        public static Shape Evaluate(float left01, float right01, in Settings s)
        {
            float l = Mathf.Clamp01(left01), r = Mathf.Clamp01(right01);
            var shape = new Shape
            {
                Span = Mathf.Clamp01((l + r) * 0.5f),
                Lean = Mathf.Clamp(r - l, -1f, 1f),
            };
            shape.HalfLength = s.LengthMin + (Mathf.Max(s.LengthMin, s.LengthMax) - s.LengthMin) * shape.Span;
            shape.Lateral = shape.Lean * Mathf.Max(0f, s.LateralMax);
            shape.TiltDegrees = Mathf.Atan2(shape.Lateral, Mathf.Max(1f, shape.HalfLength)) * Mathf.Rad2Deg;
            float cap = Mathf.Max(0f, s.TurnMax);
            shape.TurnRate = Mathf.Clamp(s.TurnGain * shape.TiltDegrees, -cap, cap);
            shape.Warp01 = Mathf.Pow(shape.Span, Mathf.Max(0.05f, s.WarpExponent));
            return shape;
        }

        /// <summary>The warp at full stretch: <paramref name="share"/> of the loop boost (Time's rate, ≥ 1).</summary>
        public static float WarpTop(float boost, float share) => 1f + (Mathf.Max(1f, boost) - 1f) * Mathf.Max(0f, share);

        /// <summary>The warp this pair asks for.</summary>
        public static float WarpTarget(in Shape shape, float top) => 1f + (Mathf.Max(1f, top) - 1f) * Mathf.Clamp01(shape.Warp01);

        /// <summary>The flight clock eased toward <paramref name="target"/>: up at <paramref name="riseRate"/> /s, down over
        /// <paramref name="fadeSeconds"/> (the field's StepBoost, toward a target that moves with the stretch).</summary>
        public static float StepWarp(float current, float target, float riseRate, float fadeSeconds, float dt)
        {
            current = Mathf.Max(1f, current);
            target = Mathf.Max(1f, target);
            if (target > current) return current + (target - current) * (1f - Mathf.Exp(-Mathf.Max(0.1f, riseRate) * dt));
            return target + (current - target) * Mathf.Exp(-dt / Mathf.Max(0.01f, fadeSeconds));
        }

        /// <summary>Where the pair rides: the black hole ahead and to the lean's side, the white hole mirrored through the hull.</summary>
        public static void Poles(Vector3 hull, Vector3 forward, Vector3 right, in Shape shape, out Vector3 sink, out Vector3 source)
        {
            var offset = forward * shape.HalfLength + right * shape.Lateral;
            sink = hull + offset;
            source = hull - offset;
        }

        /// <summary>
        /// The squeezes an autopilot holds (it writes no trigger): stretch with the distance still to go, none while the
        /// target is <see cref="AutopilotSettings.ConeDegrees"/> off the nose (turn first, then travel), and the
        /// difference toward the target's side, full at <see cref="AutopilotSettings.LeanDegrees"/> of yaw.
        /// </summary>
        public static void AutopilotSqueeze(float distance, float turnDegrees, float yawDegrees, in AutopilotSettings a,
            out float left01, out float right01)
        {
            float reach = Mathf.Clamp01((distance - a.Near) / Mathf.Max(1f, a.Far - a.Near));
            float span = reach * Mathf.Clamp01(1f - turnDegrees / Mathf.Max(1f, a.ConeDegrees));
            float lean = Mathf.Clamp(yawDegrees / Mathf.Max(1f, a.LeanDegrees), -1f, 1f);
            left01 = Mathf.Clamp01(span - lean * 0.5f);
            right01 = Mathf.Clamp01(span + lean * 0.5f);
        }
    }
}
