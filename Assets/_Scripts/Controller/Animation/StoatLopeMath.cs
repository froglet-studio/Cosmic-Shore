using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Stoat's BOUNDING LOPE as a pure function (<c>R_VesselActions/STOAT.md</c> §2.2): the
    /// pose of the body at a phase. No scene, no time step — <see cref="StoatAnimation"/> advances
    /// the phase and applies the pose, and <c>StoatLopeTests</c> hold the numbers.
    ///
    /// <para><b>Body-only, by the user's choice</b> (round 2, Option 2). The bound lifts and
    /// pitches the DRAWN hull; the vessel's own transform, its flight path and its prism trail
    /// stay straight, so aim and the wake are steady while the stoat still lopes. The viewer's
    /// "hull path" share is therefore fixed at 0 and has no field here.</para>
    ///
    /// <para>The shape, in one bound: the phase advances at <c>π · rate</c> per second, so the
    /// height <c>amplitude · sin²(phase)</c> repeats every <c>1/rate</c> seconds. The body rises
    /// nose-up, ARCHES at the top (spine &gt; 0) where it is longest, then lands nose-down,
    /// BUNCHED (spine &lt; 0, shortest) with the legs reaching — a weasel's bound.</para>
    /// </summary>
    public static class StoatLopeMath
    {
        public struct Settings
        {
            /// <summary>Peak lift of the body, world units.</summary>
            public float Amplitude;
            /// <summary>Bounds per second at a standstill.</summary>
            public float Rate;
            /// <summary>0..1: how much the spine arches and bunches, and the nose pitch.</summary>
            public float Arch;
            /// <summary>0..1: extra stretch at the top of the bound and squash at the landing.</summary>
            public float Squash;
            /// <summary>0..1: how much the rate rises with speed (0 = the same lope at every speed).</summary>
            public float SpeedLink;
        }

        /// <summary>The user's tuning from the Stoat viewer (amplitude 1.00, rate 0.66, arch 0.45,
        /// stretch 0.20, speed link 0), amplitude scaled ×1.6 from viewer to world units like the body.</summary>
        public static Settings Defaults => new() { Amplitude = 1.6f, Rate = 0.66f, Arch = 0.45f, Squash = 0.2f, SpeedLink = 0f };

        public struct Pose
        {
            /// <summary>Lift of the drawn hull above the vessel's path, world units.</summary>
            public float BodyLift;
            /// <summary>Nose pitch, degrees; NEGATIVE is nose-up (Unity's X rotation).</summary>
            public float PitchDegrees;
            /// <summary>−1..+1-ish: + arched at the top of the bound, − bunched at the landing.</summary>
            public float Spine;
            /// <summary>Length scale of the body (1 = rest); the girth scales by 1/√stretch, volume kept.</summary>
            public float Stretch;
            /// <summary>0..1: how far the legs reach out (1 at the landing, 0 at the top).</summary>
            public float Legs;
        }

        /// <summary>Phase advance per second at <paramref name="speed01"/> (0 = still, 1 = cruise).</summary>
        public static float PhaseRate(in Settings s, float speed01) =>
            Mathf.PI * Mathf.Max(0f, s.Rate) * (1f + Mathf.Max(0f, s.SpeedLink) * Mathf.Clamp01(speed01) * 2.2f);

        public static Pose Evaluate(in Settings s, float phase, float speed01)
        {
            float sn = Mathf.Sin(phase);
            float s2 = sn * sn;
            float arch = Mathf.Clamp01(s.Arch);
            return new Pose
            {
                BodyLift = Mathf.Max(0f, s.Amplitude) * s2,
                // Rising (phase in 0..π/2, sin 2φ > 0) is nose-UP, which is a negative X rotation.
                PitchDegrees = -arch * 0.45f * Mathf.Sin(2f * phase) * Mathf.Rad2Deg,
                Spine = arch * (s2 - 0.5f) * 2f,
                Stretch = 1f + (0.18f + 0.35f * Mathf.Clamp01(s.Squash)) * (s2 - 0.5f) * arch * (1f + Mathf.Clamp01(speed01)),
                Legs = 1f - s2,
            };
        }
    }
}
