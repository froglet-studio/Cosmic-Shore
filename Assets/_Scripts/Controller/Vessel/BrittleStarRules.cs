using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Brittle Star's rules as PURE code (design: <c>R_VesselActions/BRITTLE_STAR.md</c> §3): the support ring's
    /// stock, the guns' heat and the aim math, with no scene and no Unity object, so <c>BrittleStarRulesTests</c> run them as-is and
    /// every peer that feeds them the same replicated events reaches the same state.
    /// </summary>
    public static class BrittleStarRules
    {
        /// <summary>The mode selected: one at a time, or Cruise (none). Values are serialized nowhere but kept static.</summary>
        public enum Mode { Cruise = 0, Speed = 1, Creation = 2, Destruction = 3, Charge = 4 }

        /// <summary>
        /// The support ring and its stock of SPARE rings (Garrett, 2026-10-16: "only let them shoot the ring if they have a
        /// spare one that automatically regrows. they can store two extra rings, and they get 1 ring per crystal"). It
        /// replaces the class document's one stored charge, Overdrive and the ringless state:
        /// <list type="bullet">
        /// <item>The hull always carries a ring; throwing it needs a SPARE, and the mounted ring then regrows on its own
        /// over the rebuild time.</item>
        /// <item>A crystal adds one spare, up to <c>maxSpares</c>; at a full stock it adds nothing.</item>
        /// <item>The modes need the mounted ring: none during the brief regrowth.</item>
        /// </list>
        /// Time is passed in, never read, so a test can step it.
        /// </summary>
        public sealed class RingState
        {
            public bool Mounted { get; private set; } = true;
            /// <summary>Seconds left on a regrowth in progress; 0 when none is.</summary>
            public float RebuildRemaining { get; private set; }
            /// <summary>Spare rings in stock (each one throw).</summary>
            public int Spares { get; private set; }

            public bool Rebuilding => RebuildRemaining > 0f;
            /// <summary>The modes need a ring: none while it is regrowing.</summary>
            public bool ModesAvailable => Mounted;
            /// <summary>Can the ring be thrown now? A mounted ring and a spare to regrow it from.</summary>
            public bool CanLaunch => Mounted && Spares > 0;
            /// <summary>0 just thrown, rising to 1 as the next one grows, 1 while mounted (what the animation draws).</summary>
            public float Presence01(float rebuildSeconds) =>
                Mounted ? 1f : Rebuilding && rebuildSeconds > 0f ? 1f - RebuildRemaining / rebuildSeconds : 0f;

            public void Reset()
            {
                Mounted = true;
                RebuildRemaining = 0f;
                Spares = 0;
            }

            /// <summary>Throw the ring. False (nothing happens) without a mounted ring and a spare; otherwise a spare is
            /// spent and the next ring regrows over <paramref name="rebuildSeconds"/>.</summary>
            public bool TryLaunch(float rebuildSeconds)
            {
                if (!CanLaunch) return false;
                Spares--;
                Mounted = false;
                RebuildRemaining = Mathf.Max(1e-4f, rebuildSeconds);
                return true;
            }

            /// <summary>A crystal: one more spare ring, up to <paramref name="maxSpares"/>. False at a full stock.</summary>
            public bool CollectCrystal(int maxSpares)
            {
                if (Spares >= maxSpares) return false;
                Spares++;
                return true;
            }

            /// <summary>Advance the clock. Returns true on the frame a regrowth completes.</summary>
            public bool Tick(float dt)
            {
                if (RebuildRemaining <= 0f) return false;
                RebuildRemaining -= dt;
                if (RebuildRemaining > 0f) return false;
                RebuildRemaining = 0f;
                Mounted = true;
                return true;
            }
        }

        /// <summary>
        /// The guns' HEAT (Garrett, 2026-10-16: "shooting should over heat… the amount of heat produced is proportional to
        /// firing rate (trigger pull) and number of guns firing… using charge lasers cools the guns"). Every round fired adds
        /// <c>heatPerRound</c>, so heat rises with rounds per second × guns; it bleeds off at a cooling rate the caller
        /// passes in (faster with Charge Mode's cords out). At 1 the guns OVERHEAT and cannot fire until heat falls to
        /// <c>recoverBelow</c>. Pure: no clock, no randomness.
        /// </summary>
        public sealed class HeatState
        {
            public float Heat01 { get; private set; }
            public bool Overheated { get; private set; }

            public void Reset()
            {
                Heat01 = 0f;
                Overheated = false;
            }

            /// <summary>Rounds fired: heat rises. Returns true on the round that overheats the guns.</summary>
            public bool AddRounds(int rounds, float heatPerRound)
            {
                if (rounds <= 0) return false;
                Heat01 = Mathf.Min(1f, Heat01 + rounds * Mathf.Max(0f, heatPerRound));
                if (Overheated || Heat01 < 1f) return false;
                Overheated = true;
                return true;
            }

            /// <summary>Cooling over <paramref name="dt"/>. Returns true on the frame the guns recover.</summary>
            public bool Cool(float coolPerSecond, float recoverBelow, float dt)
            {
                Heat01 = Mathf.Max(0f, Heat01 - Mathf.Max(0f, coolPerSecond) * Mathf.Max(0f, dt));
                if (!Overheated || Heat01 > recoverBelow) return false;
                Overheated = false;
                return true;
            }
        }

        /// <summary>Is <paramref name="mode"/>'s effect on? The selected mode while the ring is mounted.</summary>
        public static bool IsOn(Mode mode, Mode selected, RingState ring) =>
            mode != Mode.Cruise && ring != null && ring.ModesAvailable && selected == mode;

        /// <summary>How many guns fire in a mode: Space (Destruction) all five, Charge none (the trigger works the
        /// cords), every other mode the one turret the ring has turned to the aim.</summary>
        public static int GunsFor(Mode on) => on == Mode.Destruction ? BrittleStarHullForm.Points : on == Mode.Charge ? 0 : 1;

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

        /// <summary>
        /// One of Destruction Mode's five ARMS, aimed by how far the stick is pushed "in and out" (Garrett, 2026-10-14):
        /// with the stick IN (centred, <paramref name="splay01"/> 0) every arm angles inward to meet the nose axis at
        /// <paramref name="focusDistance"/> ahead — the whole vessel a crosshair — and pushed OUT to the rim
        /// (<paramref name="splay01"/> 1) each splays <paramref name="maxSplayDegrees"/> outward from the nose. Between,
        /// the angle moves linearly. <paramref name="radialOut"/> is the unit direction from the nose axis out to the arm's
        /// muzzle (perpendicular to <paramref name="forward"/>), <paramref name="radialDistance"/> how far out it sits.
        /// </summary>
        public static Vector3 ArmDirection(Vector3 forward, Vector3 radialOut, float radialDistance, float splay01,
                                           float focusDistance, float maxSplayDegrees)
        {
            float inward = Mathf.Atan2(Mathf.Max(0f, radialDistance), Mathf.Max(1e-3f, focusDistance)) * Mathf.Rad2Deg;
            float degrees = Mathf.Lerp(-inward, maxSplayDegrees, Mathf.Clamp01(splay01));
            float a = degrees * Mathf.Deg2Rad;
            return (forward * Mathf.Cos(a) + radialOut * Mathf.Sin(a)).normalized;
        }

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
        /// The hull's speed multiplier from the modes' eased blends: Speed Mode's ×N and Creation Mode's slow-down
        /// (the slowest of the modes), multiplied so a hand-over between the two eases through both. Destruction and
        /// Charge leave the speed alone.
        /// </summary>
        public static float ModeSpeed(float speed01, float creation01, float speedMultiplier, float creationMultiplier) =>
            Mathf.Lerp(1f, speedMultiplier, speed01) * Mathf.Lerp(1f, creationMultiplier, creation01);

        /// <summary>
        /// Does the ring explosion's disk — centred at the origin of <paramref name="offset"/>, lying across
        /// <paramref name="normal"/> (unit), ±<paramref name="halfThickness"/> thick and <paramref name="radius"/>
        /// wide — reach a body at <paramref name="offset"/> whose own half-extent along the normal is
        /// <paramref name="extentAlongNormal"/> and across it <paramref name="extentAcross"/>?
        /// </summary>
        public static bool DiskReaches(Vector3 offset, Vector3 normal, float halfThickness, float radius,
                                       float extentAlongNormal, float extentAcross)
        {
            float along = Vector3.Dot(offset, normal);
            if (Mathf.Abs(along) > halfThickness + extentAlongNormal) return false;
            float reach = radius + extentAcross;
            return (offset - normal * along).sqrMagnitude <= reach * reach;
        }
    }
}
