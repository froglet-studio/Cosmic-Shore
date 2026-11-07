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
        /// passes in (faster with Charge Mode's cords out). At full the guns OVERHEAT and cannot fire until they cool.
        ///
        /// <para><b>Capacity</b> (Garrett, 2026-10-16: "have the maximum heat scale with space"; 2026-10-17: a full
        /// burst "from 6 to 20 seconds"): how much heat the guns hold, in Space 0 capacities (1 = the base). Heat and cooling are in those same
        /// units, so a capacity of 3 takes exactly three times as long to fill at any firing rate, and
        /// <see cref="Heat01"/> stays the share of the CURRENT capacity (what the HUD draws). Recovery comes after shedding
        /// the same heat at every capacity, <c>1 − recoverBelow</c>, so the lockout is as long at Space 10 as at Space 0;
        /// only the run to overheat grows. Pure: no clock, no randomness.</para>
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

            /// <summary>Rounds fired: heat rises by <paramref name="heatPerRound"/> each, out of
            /// <paramref name="capacity"/>. Returns true on the round that overheats the guns.</summary>
            public bool AddRounds(int rounds, float heatPerRound, float capacity = 1f)
            {
                if (rounds <= 0) return false;
                Heat01 = Mathf.Min(1f, Heat01 + rounds * Mathf.Max(0f, heatPerRound) / Capacity(capacity));
                if (Overheated || Heat01 < 1f) return false;
                Overheated = true;
                return true;
            }

            /// <summary>Cooling over <paramref name="dt"/>, out of <paramref name="capacity"/>. Overheated guns recover
            /// once they have shed <c>1 − recoverBelow</c> of heat (the Space 0 mark at capacity 1). Returns true on the
            /// frame the guns recover.</summary>
            public bool Cool(float coolPerSecond, float recoverBelow, float dt, float capacity = 1f)
            {
                float c = Capacity(capacity);
                Heat01 = Mathf.Max(0f, Heat01 - Mathf.Max(0f, coolPerSecond) * Mathf.Max(0f, dt) / c);
                if (!Overheated || Heat01 > RecoverMark(recoverBelow, c)) return false;
                Overheated = false;
                return true;
            }

            /// <summary>The <see cref="Heat01"/> overheated guns recover at: the same heat shed at every capacity.</summary>
            public static float RecoverMark(float recoverBelow, float capacity) =>
                1f - (1f - Mathf.Clamp01(recoverBelow)) / Capacity(capacity);

            static float Capacity(float capacity) => Mathf.Max(1e-3f, capacity);
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

        // ------------------------------------------------------------------ the ring follows a spinning stick

        /// <summary>Degrees between two of the ring's points (five-fold).</summary>
        public const float PointSpacingDegrees = 360f / BrittleStarHullForm.Points;

        /// <summary>One frame of the stick's sweep (<see cref="AimSweep.Step"/>).</summary>
        public readonly struct AimSweepStep
        {
            /// <summary>The stick came out of the deadzone this frame (no sweep yet).</summary>
            public readonly bool Entered;
            /// <summary>The stick jumped across (more than the flick angle in one frame): a new aim, not a sweep.</summary>
            public readonly bool Flick;
            /// <summary>The smoothed signed sweep, degrees per second (positive = the phase's positive direction).</summary>
            public readonly float RateDegrees;

            public AimSweepStep(bool entered, bool flick, float rateDegrees)
            {
                Entered = entered;
                Flick = flick;
                RateDegrees = rateDegrees;
            }
        }

        /// <summary>
        /// The right stick's SWEEP: how fast, and which way, its angle is turning (Garrett, 2026-11-03, the ring spin
        /// lab <c>Docs/Studios/BrittleStarRingLab.html</c>). The angle's frame-to-frame change, eased over
        /// <c>smoothingSeconds</c>. Released, it forgets; a jump wider than <c>flickDegrees</c> is a flick, not a
        /// sweep. Pure: time is passed in.
        /// </summary>
        public sealed class AimSweep
        {
            float? _previous;
            public float RateDegrees { get; private set; }

            public void Reset()
            {
                _previous = null;
                RateDegrees = 0f;
            }

            public AimSweepStep Step(float? aimRadians, float dt, float smoothingSeconds, float flickDegrees)
            {
                if (aimRadians == null)
                {
                    Reset();
                    return new AimSweepStep(false, false, 0f);
                }
                bool entered = _previous == null, flick = false;
                if (!entered)
                {
                    float d = Mathf.DeltaAngle(_previous.Value * Mathf.Rad2Deg, aimRadians.Value * Mathf.Rad2Deg);
                    if (Mathf.Abs(d) > flickDegrees)
                    {
                        flick = true;
                        RateDegrees = 0f;
                    }
                    else if (dt > 0f)
                    {
                        float k = smoothingSeconds > 0f ? 1f - Mathf.Exp(-dt / smoothingSeconds) : 1f;
                        RateDegrees += (d / dt - RateDegrees) * k;
                    }
                }
                _previous = aimRadians;
                return new AimSweepStep(entered, flick, RateDegrees);
            }
        }

        /// <summary>
        /// THE RING FOLLOWS A SPINNING STICK FORWARD (Garrett, 2026-11-03; lab <c>Docs/Studios/BrittleStarRingLab.html</c>,
        /// "Keep turning forward"). The old rule re-picked the turret NEAREST the stick every frame and turned toward
        /// it at <paramref name="turnRateDegrees"/>: a stick spun faster than that left the ring 36° behind, the next
        /// turret became the nearest, it sat BEHIND the aim, and the ring turned backward to it. That read as a
        /// wagon wheel and, past ~4 rev/s at 60 fps, as a static jitter (measured: 0.00 rev/s net, 20 backward
        /// frames a second).
        /// <para>Now the ring COMMITS to a turret (<paramref name="point"/>). While the stick sweeps faster than
        /// <paramref name="sweepThresholdDegrees"/>, it hands over only to the turret AHEAD in the sweep direction, it
        /// never turns against the sweep (ahead of the aim, it holds and the stick comes round), and while the stick
        /// outruns it, it turns flat out. A slower stick, a still one, a fresh aim or a flick take the
        /// nearest turret, exactly as before, so below the cap the ring moves frame for frame as it did.</para>
        /// Returns the new phase (radians); <paramref name="point"/> is the committed (aimed) turret.
        /// </summary>
        public static float ForwardTurn(float aimRadians, float ringPhase, ref int point, in AimSweepStep sweep,
                                        float sweepThresholdDegrees, float turnRateDegrees, float dt)
        {
            bool sweeping = Mathf.Abs(sweep.RateDegrees) > sweepThresholdDegrees;
            float lag = CommittedLag(aimRadians, ringPhase, ref point, sweep.Entered || sweep.Flick,
                                     sweeping ? Mathf.Sign(sweep.RateDegrees) : 0f);
            float step = Mathf.Max(0f, turnRateDegrees) * Mathf.Deg2Rad * dt;
            if (sweeping)
            {
                float s = Mathf.Sign(sweep.RateDegrees);
                if (s * lag <= 0f) return ringPhase;                                  // ahead of the aim: hold
                if (Mathf.Abs(sweep.RateDegrees) >= turnRateDegrees) return ringPhase + s * step;   // outrun: flat out
            }
            return Mathf.MoveTowards(ringPhase, ringPhase + lag * Mathf.Deg2Rad, step);
        }

        /// <summary>The degrees the ring must still turn to put its committed turret <paramref name="point"/> on the
        /// aim. <paramref name="sweepSign"/> 0 = not sweeping (a lag past one point spacing re-takes the nearest);
        /// ±1 = sweeping that way: the commitment moves to the next turret AHEAD once the lag passes a full spacing,
        /// and a lag more than half a spacing against the sweep moves it the other way (the stick turned back).</summary>
        public static float CommittedLag(float aimRadians, float ringPhase, ref int point, bool reacquire, float sweepSign)
        {
            const int n = BrittleStarHullForm.Points;
            if (reacquire || point < 0 || point >= n) point = NearestPoint(aimRadians, ringPhase);
            float lag = LagTo(aimRadians, ringPhase, point);
            if (sweepSign != 0f)
            {
                float s = Mathf.Sign(sweepSign);
                int guard = 0;
                while (s * lag >= PointSpacingDegrees && guard++ < 16)
                {
                    lag -= s * PointSpacingDegrees;
                    point = s > 0f ? (point + 1) % n : (point + n - 1) % n;
                }
                while (s * lag < -PointSpacingDegrees * 0.5f && guard++ < 16)
                {
                    lag += s * PointSpacingDegrees;
                    point = s > 0f ? (point + n - 1) % n : (point + 1) % n;
                }
            }
            else if (Mathf.Abs(lag) > PointSpacingDegrees)
            {
                point = NearestPoint(aimRadians, ringPhase);
                lag = LagTo(aimRadians, ringPhase, point);
            }
            return lag;
        }

        static float LagTo(float aimRadians, float ringPhase, int point) =>
            Mathf.DeltaAngle(ringPhase * Mathf.Rad2Deg, (aimRadians - BrittleStarHullForm.PointAngle(point)) * Mathf.Rad2Deg);

        // ------------------------------------------------------------------ the ring's motion smear

        /// <summary>
        /// How many copies the ring's motion smear averages for a frame that turned it <paramref name="sweptDegrees"/>
        /// (Garrett, 2026-11-03: the lab's "Forward + motion smear", the copies normalized so the smear adds no
        /// brightness). One copy per <paramref name="stepDegrees"/> of the smeared arc, at least 2, at most
        /// <paramref name="maxCopies"/>; 0 = no smear (the ring turned less than <paramref name="minDegrees"/>, or
        /// the smear is off).
        /// </summary>
        public static int SmearCopies(float sweptDegrees, float amount, float minDegrees, float stepDegrees, int maxCopies)
        {
            float arc = Mathf.Abs(sweptDegrees) * Mathf.Max(0f, amount);
            if (arc <= 0f || Mathf.Abs(sweptDegrees) <= minDegrees || maxCopies < 2) return 0;
            return Mathf.Clamp(Mathf.CeilToInt(arc / Mathf.Max(0.1f, stepDegrees)), 2, maxCopies);
        }

        /// <summary>Copy <paramref name="k"/> of <paramref name="copies"/>: how far BACK from the drawn pose it sits,
        /// degrees (copy 0 is the ring as it stands, the last is a whole smeared arc back). Every copy weighs
        /// 1 / copies, so the weights sum to one.</summary>
        public static float SmearCopyBack(int k, int copies, float sweptDegrees, float amount) =>
            copies < 2 ? 0f : sweptDegrees * Mathf.Max(0f, amount) * k / (copies - 1);

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

        // ------------------------------------------------------------------ level-5 upgrades (Shombith, 2026-11-03)

        /// <summary>
        /// CHARGE L5, Thrown Sabers: can LT cut the five cords off and throw them? Only with the upgrade, in Charge Mode,
        /// with the cords held RIGID - RT held (the replicated press, so every peer agrees; the analog depth is the
        /// owner's alone) and the cords fully grown out - and not while new ones are still waiting to regrow.
        /// </summary>
        public static bool CanThrowSabers(bool upgrade, bool chargeOn, bool triggerHeld, float cordsOut01,
                                          float regrowRemaining) =>
            upgrade && chargeOn && triggerHeld && cordsOut01 >= 0.99f && regrowRemaining <= 0f;

        /// <summary>
        /// TIME L5, Speed Roll: the roll (x) and pull (y) rates, degrees per second, the right stick asks for. Nothing
        /// inside the deadzone; outside it each axis is proportional to its own deflection.
        /// </summary>
        public static Vector2 SpeedRollRates(Vector2 stick, float deadzone, float rollDegreesPerSecond,
                                             float pullDegreesPerSecond)
        {
            if (stick.sqrMagnitude <= deadzone * deadzone) return Vector2.zero;
            stick = Vector2.ClampMagnitude(stick, 1f);
            return new Vector2(stick.x * Mathf.Max(0f, rollDegreesPerSecond), stick.y * Mathf.Max(0f, pullDegreesPerSecond));
        }

        /// <summary>Does a burst of <paramref name="radius"/> reach a body at <paramref name="offset"/> from its centre
        /// whose own half-extent is <paramref name="extent"/>? (The ring explosion's core and Space L5's explosive
        /// rounds.)</summary>
        public static bool SphereReaches(Vector3 offset, float radius, float extent)
        {
            if (radius <= 0f) return false;
            float reach = radius + Mathf.Max(0f, extent);
            return offset.sqrMagnitude <= reach * reach;
        }
    }
}
