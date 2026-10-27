using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pure arithmetic of the Stoat's GATE MODE (<c>R_VesselActions/STOAT_DIPOLE.md</c> §1f), B's third press: a WARP
    /// GATE. A trigger press opens a black hole <see cref="Settings.Ahead"/> ahead of the nose and its white hole further
    /// along the same line; the deeper squeeze slides the white hole out (<see cref="Length"/>). Flying into the black hole
    /// carries the hull out of the white hole (the platform's <c>BlackHoleVesselPull.TryCarryThrough</c>), and the jump
    /// earns a warp of the flight clock — all of it for a long gate entered straight down its line (<see cref="Quality"/>).
    /// Held, a gate lasts <see cref="Settings.HoldMaxSeconds"/>; let go, it falls together in
    /// <see cref="Settings.CollapseSeconds"/> (<see cref="CollapseSpeed"/>). Within <see cref="Settings.FunnelRadius"/> the
    /// black hole draws its owner onto the line into its mouth (<see cref="FunnelStep"/>). The Vessel Studio's
    /// <c>gateLen</c> / <c>gateQuality</c> / <c>stepGateBoost</c> / the gate branch of <c>aiInput</c>
    /// (<c>Docs/Studios/StoatFlightStudio.html</c>), line for line. No scene, no time, no input device.
    /// </summary>
    public static class StoatGateMath
    {
        /// <summary>The gate's numbers (<see cref="ScriptableObjects.StoatModeActionSO"/>'s gate rows, the studio's gt* rows).</summary>
        public struct Settings
        {
            public float Ahead;            // u, the black hole ahead of the nose at the press
            public float LengthMin;        // u, black hole to white hole at the lightest squeeze
            public float LengthMax;        // u, fully squeezed
            public float Mouth;            // u, the black hole's horizon: the mouth flown into
            public float HoldMaxSeconds;   // held, it lasts at most this
            public float CollapseSeconds;  // let go, it falls together in this
            public float AimConeDegrees;   // full warp within this of the gate's line, none at it
            public float WarpShare;        // the top warp, a share of the loop boost
            public float BoostSeconds;     // the warp after a jump holds this long
            public float FunnelRadius;     // u: within this the black hole draws its owner onto its mouth
            public float FunnelRate;       // /s
        }

        /// <summary>The gate's length for a squeeze, through the Stoat's squeeze curve (holdExponent).</summary>
        public static float Length(float squeeze01, in Settings s, float holdExponent) =>
            s.LengthMin + (Mathf.Max(s.LengthMin, s.LengthMax) - s.LengthMin) *
            Mathf.Pow(Mathf.Clamp01(squeeze01), Mathf.Max(0.01f, holdExponent));

        /// <summary>The squeeze that gives <paramref name="length"/> (the inverse of <see cref="Length"/>).</summary>
        public static float SqueezeFor(float length, in Settings s, float holdExponent)
        {
            float t = Mathf.Clamp01((length - s.LengthMin) / Mathf.Max(1f, s.LengthMax - s.LengthMin));
            return Mathf.Pow(t, 1f / Mathf.Max(0.01f, holdExponent));
        }

        /// <summary>
        /// How good a jump is, 0..1: the heading at the entry against the gate's line (all of it along the line, none at
        /// <see cref="Settings.AimConeDegrees"/>), times its length (0.4 for the shortest gate, 1 for the longest).
        /// </summary>
        public static float Quality(Vector3 entryForward, Vector3 sink, Vector3 source, in Settings s)
        {
            var axis = source - sink;
            float len = axis.magnitude;
            if (len < 1e-3f || entryForward.sqrMagnitude < 1e-8f) return 0f;
            float angle = Vector3.Angle(entryForward, axis);
            float aim = Mathf.Clamp01(1f - angle / Mathf.Max(1f, s.AimConeDegrees));
            float len01 = Mathf.Clamp01((len - s.LengthMin) / Mathf.Max(1f, s.LengthMax - s.LengthMin));
            return aim * (0.4f + 0.6f * len01);
        }

        /// <summary>The closing speed that brings a let-go gate together in <paramref name="seconds"/>.</summary>
        public static float CollapseSpeed(float halfGap, float seconds) => Mathf.Max(1f, halfGap / Mathf.Max(0.05f, seconds));

        /// <summary>The warp after a perfect jump: <paramref name="share"/> of the loop boost (Time's rate, ≥ 1).</summary>
        public static float WarpTop(float boost, float share) => 1f + (Mathf.Max(1f, boost) - 1f) * Mathf.Max(0f, share);

        /// <summary>The warp a jump of <paramref name="quality"/> earns.</summary>
        public static float WarpTarget(float quality, float top) => 1f + (Mathf.Max(1f, top) - 1f) * Mathf.Clamp01(quality);

        /// <summary>The flight clock eased toward <paramref name="target"/>: up at <paramref name="riseRate"/> /s, down over
        /// <paramref name="fadeSeconds"/>.</summary>
        public static float StepWarp(float current, float target, float riseRate, float fadeSeconds, float dt)
        {
            current = Mathf.Max(1f, current);
            target = Mathf.Max(1f, target);
            if (target > current) return current + (target - current) * (1f - Mathf.Exp(-Mathf.Max(0.1f, riseRate) * dt));
            return target + (current - target) * Mathf.Exp(-dt / Mathf.Max(0.01f, fadeSeconds));
        }

        /// <summary>
        /// The black hole's pull on its owner this frame: within <see cref="Settings.FunnelRadius"/>, still ahead, the hull's
        /// sideways miss of the mouth closes at <see cref="Settings.FunnelRate"/> /s. The displacement to apply (zero outside).
        /// </summary>
        public static Vector3 FunnelStep(Vector3 hull, Vector3 forward, Vector3 sink, in Settings s, float dt)
        {
            var to = sink - hull;
            float along = Vector3.Dot(to, forward);
            if (along <= 0f || to.sqrMagnitude >= s.FunnelRadius * s.FunnelRadius) return Vector3.zero;
            return (to - forward * along) * (1f - Mathf.Exp(-Mathf.Max(0f, s.FunnelRate) * dt));
        }

        /// <summary>The gate an autopilot opens for a target <paramref name="distance"/> away: ending short of it.</summary>
        public static float AutopilotLength(float distance, in Settings s) =>
            Mathf.Clamp(distance - s.Ahead - 80f, s.LengthMin, Mathf.Max(s.LengthMin, s.LengthMax));
    }
}
