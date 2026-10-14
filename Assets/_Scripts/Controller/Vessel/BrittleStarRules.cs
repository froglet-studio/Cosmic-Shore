using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Brittle Star's rules as PURE code (design: <c>R_VesselActions/BRITTLE_STAR.md</c> §3): the support ring's
    /// life cycle and the aim math, with no scene and no Unity object, so <c>BrittleStarRulesTests</c> run them as-is and
    /// every peer that feeds them the same replicated events reaches the same state.
    /// </summary>
    public static class BrittleStarRules
    {
        /// <summary>Which modes are on. Overdrive turns all three on at once.</summary>
        public enum Mode { Cruise = 0, Speed = 1, Creation = 2, Destruction = 3 }

        /// <summary>
        /// The support ring, its one stored charge and Overdrive — the class document's Ring Detonation section,
        /// clause by clause:
        /// <list type="bullet">
        /// <item>"Collecting a crystal adds one charge… Only one charge can be held at a time."</item>
        /// <item>"Collecting a crystal while you already have a charge will activate all 3 of the Brittle Stars modes
        /// simultaneously for a short time instead of adding a second charge."</item>
        /// <item>"After launching the outer ring, if the cockpit has a charge… it will immediately rebuild its support ring.
        /// If it does not… [it] is unable to rebuild… until a crystal is collected and the vessel loses access to its 3
        /// modes entirely until a support ring is constructed."</item>
        /// <item>"The support ring detonation has no cooldown, it only requires the existence of a support ring."</item>
        /// </list>
        /// Time is passed in, never read, so a test can step it.
        /// </summary>
        public sealed class RingState
        {
            public bool Mounted { get; private set; } = true;
            /// <summary>Seconds left on a rebuild in progress; 0 when none is.</summary>
            public float RebuildRemaining { get; private set; }
            public int Charges { get; private set; }
            public float OverdriveRemaining { get; private set; }

            public bool Rebuilding => RebuildRemaining > 0f;
            public bool Overdrive => OverdriveRemaining > 0f;
            /// <summary>The modes need a ring: none while it is gone or still being built.</summary>
            public bool ModesAvailable => Mounted;
            /// <summary>0 with no ring, rising to 1 as one is built, 1 while mounted (what the animation draws).</summary>
            public float Presence01(float rebuildSeconds) =>
                Mounted ? 1f : Rebuilding && rebuildSeconds > 0f ? 1f - RebuildRemaining / rebuildSeconds : 0f;

            public void Reset()
            {
                Mounted = true;
                RebuildRemaining = 0f;
                Charges = 0;
                OverdriveRemaining = 0f;
            }

            /// <summary>Throw the ring. False (nothing happens) without a mounted ring.</summary>
            public bool TryLaunch(float rebuildSeconds)
            {
                if (!Mounted) return false;
                Mounted = false;
                if (Charges > 0)
                {
                    Charges--;
                    RebuildRemaining = Mathf.Max(1e-4f, rebuildSeconds);
                }
                return true;
            }

            public enum CrystalOutcome { Rebuilt = 0, Stored = 1, Overdrive = 2 }

            /// <summary>A crystal: rebuild a missing ring, else store the charge, else Overdrive.</summary>
            public CrystalOutcome CollectCrystal(float rebuildSeconds, float overdriveSeconds)
            {
                if (!Mounted && !Rebuilding)
                {
                    RebuildRemaining = Mathf.Max(1e-4f, rebuildSeconds);
                    return CrystalOutcome.Rebuilt;
                }
                if (Charges == 0)
                {
                    Charges = 1;
                    return CrystalOutcome.Stored;
                }
                OverdriveRemaining = Mathf.Max(OverdriveRemaining, 0f) + overdriveSeconds;
                return CrystalOutcome.Overdrive;
            }

            /// <summary>Advance the clocks. Returns true on the frame a rebuild completes.</summary>
            public bool Tick(float dt)
            {
                if (OverdriveRemaining > 0f) OverdriveRemaining = Mathf.Max(0f, OverdriveRemaining - dt);
                if (RebuildRemaining <= 0f) return false;
                RebuildRemaining -= dt;
                if (RebuildRemaining > 0f) return false;
                RebuildRemaining = 0f;
                Mounted = true;
                return true;
            }
        }

        /// <summary>Is <paramref name="mode"/>'s effect on? The selected mode while the ring is mounted, or every mode
        /// during Overdrive.</summary>
        public static bool IsOn(Mode mode, Mode selected, RingState ring) =>
            mode != Mode.Cruise && ring != null && ring.ModesAvailable && (ring.Overdrive || selected == mode);

        /// <summary>Pressing a mode's control: select it, or return to Cruise if it was already selected.</summary>
        public static Mode Press(Mode selected, Mode pressed) => selected == pressed ? Mode.Cruise : pressed;

        /// <summary>A projectile's range at this flight time: speed × 2T/π, because a round decelerates to rest over
        /// its flight (<c>Projectile.MoveProjectileAsync</c>'s cosine ease, vessel rule 34).</summary>
        public static float RangeFor(float speed, float time) => speed * 2f * time / Mathf.PI;

        // ------------------------------------------------------------------ aim

        /// <summary>The aim's angle round the nose axis (radians, 0 = right, π/2 = up) from a stick, or null inside
        /// the deadzone.</summary>
        public static float? AimAngle(Vector2 stick, float deadzone)
        {
            if (stick.sqrMagnitude <= deadzone * deadzone) return null;
            return Mathf.Atan2(stick.y, stick.x);
        }

        /// <summary>How far the aim opens the spread, 0 at the deadzone's edge to 1 at full deflection.</summary>
        public static float Spread01(Vector2 stick, float deadzone) =>
            Mathf.Clamp01((stick.magnitude - deadzone) / Mathf.Max(1e-4f, 1f - deadzone));

        /// <summary>The point whose CURRENT angle (its rest angle plus the ring's phase) is closest to
        /// <paramref name="aimAngle"/>.</summary>
        public static int NearestPoint(float aimAngle, float ringPhase)
        {
            int best = 0;
            float bestDelta = float.MaxValue;
            for (int i = 0; i < BrittleStarHullForm.Points; i++)
            {
                float d = Mathf.Abs(Mathf.DeltaAngle(Mathf.Rad2Deg * (BrittleStarHullForm.PointAngle(i) + ringPhase),
                                                     Mathf.Rad2Deg * aimAngle));
                if (d < bestDelta) { bestDelta = d; best = i; }
            }
            return best;
        }

        /// <summary>The ring phase (radians) that puts point <paramref name="point"/> exactly at the aim angle, chosen
        /// as the nearest such phase to <paramref name="current"/> so the ring never spins the long way round.</summary>
        public static float PhaseToAim(float aimAngle, int point, float current)
        {
            float target = aimAngle - BrittleStarHullForm.PointAngle(point);
            return current + Mathf.DeltaAngle(current * Mathf.Rad2Deg, target * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        }

        /// <summary>
        /// A deterministic deflection inside a cone of <paramref name="halfAngleDegrees"/> about
        /// <paramref name="forward"/>, from an integer hash of the shot's serial — never <c>UnityEngine.Random</c>
        /// (vessel rule 18), so peers that agree on the shot count agree on every round.
        /// </summary>
        public static Vector3 Deflect(Vector3 forward, Vector3 up, float halfAngleDegrees, uint serial)
        {
            if (halfAngleDegrees <= 0f) return forward;
            float u = Hash01(serial * 2u + 1u);
            float v = Hash01(serial * 2u + 2u);
            // Uniform over the cone's disc: sqrt for area, a full turn for the direction.
            float off = halfAngleDegrees * Mathf.Sqrt(u);
            float roll = v * 360f;
            Vector3 side = Vector3.Cross(forward, up);
            if (side.sqrMagnitude < 1e-8f) side = Vector3.Cross(forward, Vector3.right);
            if (side.sqrMagnitude < 1e-8f) side = Vector3.Cross(forward, Vector3.up);
            var tilt = Quaternion.AngleAxis(roll, forward) * Quaternion.AngleAxis(off, side.normalized);
            return tilt * forward;
        }

        /// <summary>A pure integer hash to [0, 1).</summary>
        public static float Hash01(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352dU;
            x ^= x >> 15;
            x *= 0x846ca68bU;
            x ^= x >> 16;
            return (x & 0xFFFFFF) / 16777216f;
        }
    }
}
