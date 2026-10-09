using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every number the Thresher's chain physics reads, in GAME units (already scaled from the 2D
    /// sandbox by <c>ThresherConfigSO.ToSettings</c>). A plain struct so the solver and its tests need
    /// no ScriptableObject and no scene.
    /// </summary>
    public struct ThresherChainSettings
    {
        // Chain
        public float RestLength, MaxLength, PayOutRate, ReelInRate, ReelSpinCap;
        // Ball
        public float BallDrag, BallMass, BallRadius, MaxBallSpeed;
        // Rope coupling
        public float Tug, MinShipFraction, CrackShare;
        // Smashing
        public float SmashSpeed, WhiteHotSpeed, PloughKeep, PloughKeepHot, CrushKeep, BounceRestitution;
        // Lock
        public float SkidRate, SkidSeconds, LockKeep, LockSpinRate, LockMaxSpeed, Yank;
        // Reference
        public float CruiseSpeed;
    }

    /// <summary>
    /// The Thresher's dials, authored in the units of the 2D browser sandbox the mechanic was tuned
    /// in, so every number here can be read side by side with that sandbox. <see cref="ToSettings"/>
    /// scales every speed and length by <c>gameCruise / sandboxCruise</c>. Rates (1/s) and
    /// dimensionless shares are unscaled — scaling lengths and speeds by the same factor leaves
    /// every TIME in the mechanic (how long a reel takes, how fast the ball cools) unchanged, which
    /// is what carries the sandbox's feel across.
    ///
    /// The class's field initializers are the ONE source of the defaults: the config asset embeds
    /// an instance and the edit-mode tests construct a fresh one.
    /// </summary>
    [System.Serializable]
    public sealed class ThresherDials
    {
        [Header("Scale")]
        [Tooltip("Cruise speed of the 2D sandbox the dials were tuned in (u/s). Every speed and length below is in SANDBOX units.")]
        [SerializeField] float sandboxCruise = 380f;
        [Tooltip("This game's cruise for the Thresher (u/s): its throttle target at neutral sticks " +
                 "(DefaultMinimumSpeed + 0.5 x DefaultThrottleScaler on the prefab). The scale factor is gameCruise / sandboxCruise.")]
        [SerializeField] float gameCruise = 70f;

        [Header("Chain")]
        [Tooltip("Chain length reeled fully in (sandbox units).")]
        [SerializeField] float restLen = 120f;
        [Tooltip("Chain length fully let out (sandbox units).")]
        [SerializeField] float maxLen = 760f;
        [Tooltip("Let-out rate while the right trigger is held (sandbox units/s).")]
        [SerializeField] float payOut = 1000f;
        [Tooltip("Reel-in rate when the right trigger is up (sandbox units/s). Fast, because the reel IS the crack.")]
        [SerializeField] float reelIn = 1800f;
        [Tooltip("Cap on the spin multiplier per length change while reeling in (dimensionless).")]
        [SerializeField] float reelSpinCap = 1.6f;

        [Header("Ball")]
        [Tooltip("Ball drag (1/s).")]
        [SerializeField] float ballDrag = 0.35f;
        [Tooltip("Ball mass relative to the ship (ship = 1).")]
        [SerializeField] float ballMass = 0.4f;
        [Tooltip("Ball radius (sandbox units) - also the swept-sphere radius for smashing.")]
        [SerializeField] float ballR = 17f;
        [Tooltip("Hard cap on ball speed (sandbox units/s).")]
        [SerializeField] float maxBall = 2600f;

        [Header("Rope")]
        [Tooltip("Fraction of the rope's mass-weighted yank the ship actually feels.")]
        [SerializeField] float tug = 0.2f;
        [Tooltip("The rope can never drag the ship below this fraction of its cruise.")]
        [SerializeField] float minShip = 0.6f;
        [Tooltip("Share of the lost radial speed thrown sideways into the ball when a slack chain snaps taut.")]
        [SerializeField] float crack = 0.6f;

        [Header("Smashing")]
        [Tooltip("At or above this ball speed a hit SMASHES the prism and the ball ploughs on (sandbox units/s).")]
        [SerializeField] float smashSpeed = 700f;
        [Tooltip("Ball speed at which it reads white-hot: full glow, longest hit-stop, best plough (sandbox units/s).")]
        [SerializeField] float whiteHotSpeed = 1300f;
        [Tooltip("Fraction of its speed the ball keeps per smash at smash speed.")]
        [SerializeField] float plough = 0.9f;
        [Tooltip("Fraction of its speed the ball keeps per smash when white-hot.")]
        [SerializeField] float ploughHot = 0.97f;
        [Tooltip("Fraction of its speed a ball BELOW smash speed keeps when it breaks a rival prism. " +
                 "Prisms are binary: a slow ball still destroys rival mass, it just pays more speed for it " +
                 "and makes no explosion.")]
        [SerializeField] float crushKeep = 0.85f;
        [Tooltip("Restitution when the ball bounces off a prism of its own domain (0 = dead stop along " +
                 "the contact normal, 1 = perfectly elastic).")]
        [SerializeField] float bounce = 0.6f;

        [Header("Lock (left trigger)")]
        [Tooltip("Skid brake on the ball while planting (1/s).")]
        [SerializeField] float skid = 7f;
        [Tooltip("Seconds the skid lasts before the ball becomes the pivot (still smashing while it slides).")]
        [SerializeField] float skidSeconds = 0.3f;
        [Tooltip("Fraction of the ship's speed kept on hooking onto the pivot.")]
        [SerializeField] float lockKeep = 1f;
        [Tooltip("Orbital spin-up while planted (sandbox units/s per second).")]
        [SerializeField] float lockSpin = 220f;
        [Tooltip("Spin-up stops at this multiple of cruise.")]
        [SerializeField] float lockMax = 2.2f;
        [Tooltip("On unlock the ball gets this fraction of the ship's velocity.")]
        [SerializeField] float yank = 0.5f;

        /// <summary>Sandbox → game length/speed factor.</summary>
        public float Scale => sandboxCruise > 0f ? gameCruise / sandboxCruise : 1f;
        public float GameCruise => gameCruise;

        public ThresherChainSettings ToSettings()
        {
            float k = Scale;
            return new ThresherChainSettings
            {
                RestLength = restLen * k,
                MaxLength = Mathf.Max(restLen, maxLen) * k,
                PayOutRate = payOut * k,
                ReelInRate = reelIn * k,
                ReelSpinCap = Mathf.Max(1f, reelSpinCap),
                BallDrag = Mathf.Max(0f, ballDrag),
                BallMass = Mathf.Max(0f, ballMass),
                BallRadius = ballR * k,
                MaxBallSpeed = maxBall * k,
                Tug = Mathf.Clamp01(tug),
                MinShipFraction = Mathf.Clamp01(minShip),
                CrackShare = Mathf.Max(0f, crack),
                SmashSpeed = smashSpeed * k,
                WhiteHotSpeed = Mathf.Max(smashSpeed, whiteHotSpeed) * k,
                PloughKeep = Mathf.Clamp01(plough),
                PloughKeepHot = Mathf.Clamp01(ploughHot),
                CrushKeep = Mathf.Clamp01(crushKeep),
                BounceRestitution = Mathf.Clamp01(bounce),
                SkidRate = Mathf.Max(0f, skid),
                SkidSeconds = Mathf.Max(0f, skidSeconds),
                LockKeep = Mathf.Max(0f, lockKeep),
                LockSpinRate = lockSpin * k,
                LockMaxSpeed = lockMax * gameCruise,
                Yank = Mathf.Max(0f, yank),
                CruiseSpeed = gameCruise,
            };
        }
    }

    /// <summary>What the ball is doing. Values are explicit per the enum-serialization rule.</summary>
    public enum ThresherMode
    {
        /// <summary>Towed: the ball swings on the chain behind a free-flying ship.</summary>
        Free = 0,
        /// <summary>Left trigger just went down: the ball is braking to a stop (still smashing).</summary>
        Skidding = 1,
        /// <summary>The ball is planted in space and the SHIP swings round it.</summary>
        Pivot = 2,
    }

    /// <summary>One frame's answer from <see cref="ThresherChainSolver.Step"/>.</summary>
    public struct ThresherStepResult
    {
        /// <summary>The ship's world velocity for this frame, after the rope's tug (Free/Skidding)
        /// or the orbit (Pivot). The transformer integrates the hull along exactly this.</summary>
        public Vector3 ShipVelocity;
        /// <summary>Where the ball was at the top of the frame — the start of its swept path.</summary>
        public Vector3 BallFrom;
        /// <summary>True on the frame a slack chain snapped taut and threw speed sideways.</summary>
        public bool Cracked;
        /// <summary>True on the frame the skid finished and the ball became the pivot.</summary>
        public bool Planted;
    }

    /// <summary>
    /// The Thresher's chain: a heavy point-mass ball on a max-distance rope behind a nimble ship.
    /// PURE C# — no MonoBehaviour, no scene, no physics engine — so every rule below is pinned by
    /// edit-mode tests (<c>ThresherChainSolverTests</c>) rather than by feel alone.
    ///
    /// <b>The rope</b> is a position-based max-distance constraint, mass-weighted: when the ball
    /// is past the chain's length the overshoot is split between the two bodies, the ship taking
    /// <c>Tug × m/(1+m)</c> of it (ship mass 1) and the ball the rest, and each body's velocity is
    /// then what its corrected position implies. That one rule is the whole swing: a taut chain
    /// cancels the ball's outward speed and keeps its tangential speed, so turning the ship whips
    /// the ball round. The ship can never be dragged below <c>MinShipFraction × cruise</c>.
    ///
    /// <b>Length changes conserve spin.</b> Reeling in keeps <c>v_tangential × r</c> constant
    /// (the ice skater pulling her arms in), capped at <see cref="ThresherChainSettings.ReelSpinCap"/>
    /// per change so a long frame cannot fling the ball; paying out SPENDS spin by
    /// <c>sqrt(oldL/newL)</c>. Wind out, whip, release the trigger: the reel-in is the crack.
    ///
    /// <b>The snap.</b> On the frame a slack chain goes taut, <see cref="ThresherChainSettings.CrackShare"/>
    /// of the radial speed the rope just cancelled is thrown sideways into the ball — the whip
    /// crack you get from turning one way and snapping back.
    ///
    /// <b>The lock.</b> <see cref="Plant"/> skids the ball to a stop over
    /// <see cref="ThresherChainSettings.SkidSeconds"/>, then it is a fixed pivot and the ship orbits
    /// it on the chain at the speed it hooked on with, spinning up toward
    /// <see cref="ThresherChainSettings.LockMaxSpeed"/>. <see cref="Release"/> flings the ship off on
    /// its tangent and yanks the ball after it.
    /// </summary>
    public sealed class ThresherChainSolver
    {
        public ThresherChainSettings Settings;

        public Vector3 BallPosition;
        public Vector3 BallVelocity;
        public float Length;
        public ThresherMode Mode { get; private set; }
        public Vector3 Pivot { get; private set; }
        /// <summary>The ship's orbital speed while <see cref="ThresherMode.Pivot"/>.</summary>
        public float LockSpeed { get; private set; }
        /// <summary>True while the rope is holding the ball (or the ship, in a pivot) at full length.</summary>
        public bool IsTaut { get; private set; }

        Vector3 _lockDirection;
        float _skidTime;
        bool _wasSlack;

        /// <summary>Fraction below full length at which the chain counts as SLACK for the crack.
        /// A chain riding taut frame after frame must not crack every frame; it has to go slack
        /// first and then snap.</summary>
        public const float SlackTolerance = 0.02f;

        public ThresherChainSolver(ThresherChainSettings settings) => Settings = settings;

        public float BallSpeed => BallVelocity.magnitude;

        /// <summary>Place the ball trailing the ship at rest length, moving with it.</summary>
        public void Reset(Vector3 shipPosition, Vector3 shipForward, Vector3 shipVelocity)
        {
            Vector3 back = shipForward.sqrMagnitude > 1e-6f ? -shipForward.normalized : Vector3.back;
            Length = Settings.RestLength;
            BallPosition = shipPosition + back * Length;
            BallVelocity = shipVelocity;
            Mode = ThresherMode.Free;
            LockSpeed = 0f;
            _skidTime = 0f;
            _wasSlack = false;
            IsTaut = true;
        }

        // ------------------------------------------------------------------ lock edges

        /// <summary>Left trigger down: start the skid. The ball keeps sliding (and smashing) and
        /// becomes the pivot once the skid is spent.</summary>
        public void Plant()
        {
            if (Mode != ThresherMode.Free) return;
            Mode = ThresherMode.Skidding;
            _skidTime = 0f;
        }

        /// <summary>Left trigger up: the ship flies off on its current velocity and, if the ball
        /// was the pivot, the ball is yanked after it at <see cref="ThresherChainSettings.Yank"/> of
        /// the ship's speed. <paramref name="slingshot"/> is the Time level-5 upgrade: the yank is never weaker than
        /// smash speed, so the ball leaves the pivot hot.</summary>
        public void Release(Vector3 shipVelocity, bool slingshot = false)
        {
            if (Mode == ThresherMode.Pivot)
            {
                Vector3 yank = shipVelocity * Settings.Yank;
                if (slingshot && shipVelocity.sqrMagnitude > 1e-6f && yank.magnitude < Settings.SmashSpeed)
                    yank = shipVelocity.normalized * Settings.SmashSpeed;
                BallVelocity = Vector3.ClampMagnitude(yank, Settings.MaxBallSpeed);
            }
            Mode = ThresherMode.Free;
            LockSpeed = 0f;
            _wasSlack = false;
        }

        // ------------------------------------------------------------------ the step

        /// <summary>
        /// Advance one frame. <paramref name="shipVelocity"/> is what the flight model wants the
        /// ship to do this frame; the return value is what it actually does once the chain has had
        /// its say. <paramref name="payOut"/> is the right trigger: held lets chain out, released
        /// reels it in. <paramref name="shipCruise"/> is the ship's current throttle target — the
        /// tug's floor is a fraction of it, so the floor follows the pilot's own throttle.
        /// </summary>
        public ThresherStepResult Step(Vector3 shipPosition, Vector3 shipVelocity, bool payOut,
                                    float shipCruise, float dt)
        {
            var result = new ThresherStepResult { ShipVelocity = shipVelocity, BallFrom = BallPosition };
            if (dt <= 0f) return result;

            if (Mode == ThresherMode.Pivot)
            {
                result.ShipVelocity = StepPivot(shipPosition, shipVelocity, payOut, dt);
                result.BallFrom = BallPosition;
                return result;
            }

            // 1) LENGTH — the winch, with spin conservation on the change.
            float oldLength = Length;
            float newLength = payOut
                ? Mathf.Min(Settings.MaxLength, oldLength + Settings.PayOutRate * dt)
                : Mathf.Max(Settings.RestLength, oldLength - Settings.ReelInRate * dt);
            if (!Mathf.Approximately(newLength, oldLength))
                ApplyLengthChange(shipPosition, shipVelocity, oldLength, newLength);
            Length = newLength;
            result.BallFrom = BallPosition;

            // 2) BALL — drag (plus the skid brake while planting), then a free step.
            float damping = Settings.BallDrag + (Mode == ThresherMode.Skidding ? Settings.SkidRate : 0f);
            BallVelocity *= Mathf.Exp(-damping * dt);
            Vector3 ballNext = BallPosition + BallVelocity * dt;
            Vector3 shipNext = shipPosition + shipVelocity * dt;

            // 3) ROPE — max-distance, mass-weighted, position-based.
            Vector3 d = ballNext - shipNext;
            float dist = d.magnitude;
            bool taut = dist > Length && dist > 1e-5f;
            if (taut)
            {
                Vector3 n = d / dist;
                float excess = dist - Length;
                float shipShare = ShipShare(Settings);

                // The radial speed this frame's projection is about to cancel — measured BEFORE
                // the projection, because that is the speed the crack redistributes.
                Vector3 relative = BallVelocity - shipVelocity;
                float radial = Vector3.Dot(relative, n);
                Vector3 tangential = relative - n * radial;

                shipNext += n * (excess * shipShare);
                ballNext -= n * (excess * (1f - shipShare));

                Vector3 newShipVelocity = (shipNext - shipPosition) / dt;
                BallVelocity = (ballNext - BallPosition) / dt;

                // The crack: a slack chain snapping taut throws a share of the cancelled radial
                // speed SIDEWAYS, along the ball's existing swing. A chain that was already taut
                // does not crack — it just rides.
                if (_wasSlack && radial > 0f && tangential.sqrMagnitude > 1e-6f)
                {
                    BallVelocity += tangential.normalized * (Settings.CrackShare * radial);
                    result.Cracked = true;
                }

                result.ShipVelocity = ApplyShipFloor(shipVelocity, newShipVelocity, shipCruise);
            }

            BallVelocity = Vector3.ClampMagnitude(BallVelocity, Settings.MaxBallSpeed);
            BallPosition = ballNext;
            IsTaut = taut;
            _wasSlack = dist < Length * (1f - SlackTolerance);

            // 4) SKID — a planted ball becomes the pivot once the skid is spent.
            if (Mode == ThresherMode.Skidding)
            {
                _skidTime += dt;
                if (_skidTime >= Settings.SkidSeconds)
                {
                    BeginPivot(shipPosition + result.ShipVelocity * dt, result.ShipVelocity);
                    result.Planted = true;
                }
            }

            return result;
        }

        /// <summary>The ship's share of the rope's overshoot: inverse-mass weighting with the
        /// ship's mass taken as 1, scaled by <see cref="ThresherChainSettings.Tug"/> — the fraction of
        /// the yank the ship's engine fails to absorb.</summary>
        public static float ShipShare(in ThresherChainSettings s)
            => Mathf.Clamp01(s.Tug * s.BallMass / (1f + s.BallMass));

        /// <summary>
        /// The tug's floor. The rope may slow the ship, but never below
        /// <c>MinShipFraction × cruise</c> — and never speeds up a pilot who was ALREADY slower
        /// than that by choice. Direction is the rope's; only the magnitude is floored.
        /// </summary>
        public Vector3 ApplyShipFloor(Vector3 before, Vector3 after, float shipCruise)
        {
            float floor = Mathf.Min(before.magnitude, Settings.MinShipFraction * Mathf.Max(0f, shipCruise));
            float speed = after.magnitude;
            if (speed >= floor) return after;
            Vector3 dir = speed > 1e-5f ? after / speed : (before.sqrMagnitude > 1e-8f ? before.normalized : Vector3.zero);
            return dir * floor;
        }

        /// <summary>
        /// Spin bookkeeping for a change of chain length, relative to the ship.
        /// Reeling IN a taut chain keeps <c>v_t × r</c> constant (multiplier capped at
        /// <see cref="ThresherChainSettings.ReelSpinCap"/> per change) and pulls the ball onto the new
        /// radius; paying OUT a taut chain spends spin by <c>sqrt(oldL/newL)</c>. A slack chain is
        /// not touching the ball, so neither does anything to it.
        /// </summary>
        public void ApplyLengthChange(Vector3 shipPosition, Vector3 shipVelocity, float oldLength, float newLength)
        {
            Vector3 r = BallPosition - shipPosition;
            float dist = r.magnitude;
            if (dist < 1e-5f || newLength <= 1e-5f) return;
            Vector3 n = r / dist;
            Vector3 relative = BallVelocity - shipVelocity;
            float radial = Vector3.Dot(relative, n);
            Vector3 tangential = relative - n * radial;

            if (newLength < oldLength)
            {
                if (dist <= newLength) return;   // slack: the winch is not pulling on the ball yet
                float mult = Mathf.Min(dist / newLength, Settings.ReelSpinCap);
                tangential *= mult;
                radial = Mathf.Min(radial, 0f);  // a reeling chain cannot let the ball run outward
                BallPosition = shipPosition + n * newLength;
            }
            else
            {
                if (dist < oldLength * (1f - SlackTolerance)) return;
                tangential *= Mathf.Sqrt(oldLength / newLength);
            }

            BallVelocity = shipVelocity + n * radial + tangential;
        }

        // ------------------------------------------------------------------ the pivot

        void BeginPivot(Vector3 shipPosition, Vector3 shipVelocity)
        {
            Mode = ThresherMode.Pivot;
            Pivot = BallPosition;
            BallVelocity = Vector3.zero;

            Vector3 r = shipPosition - Pivot;
            Vector3 n = r.sqrMagnitude > 1e-8f ? r.normalized : Vector3.forward;
            Vector3 tangent = shipVelocity - n * Vector3.Dot(shipVelocity, n);
            _lockDirection = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : AnyPerpendicular(n, shipVelocity);

            // Keep the speed you hooked on with — but never hook on at a crawl: a pilot who plants
            // while flying straight AT the ball has no tangential speed at all, and a zero orbit
            // is the stall this mechanic must never produce.
            LockSpeed = Mathf.Max(shipVelocity.magnitude * Settings.LockKeep,
                                  Settings.MinShipFraction * Settings.CruiseSpeed);
        }

        Vector3 StepPivot(Vector3 shipPosition, Vector3 shipVelocity, bool payOut, float dt)
        {
            BallPosition = Pivot;
            BallVelocity = Vector3.zero;

            Vector3 r = shipPosition - Pivot;
            float dist = r.magnitude;
            Vector3 n = dist > 1e-5f ? r / dist : Vector3.forward;

            // Winch: right trigger UP winds you in (and spins you up, by conservation); held
            // lets you out (and spends spin). Only a TAUT chain trades spin for length.
            float oldLength = Length;
            float newLength = payOut
                ? Mathf.Min(Settings.MaxLength, oldLength + Settings.PayOutRate * dt)
                : Mathf.Max(Settings.RestLength, oldLength - Settings.ReelInRate * dt);
            bool taut = dist >= oldLength * (1f - SlackTolerance);
            if (taut && newLength < oldLength && dist > newLength)
                LockSpeed *= Mathf.Min(dist / newLength, Settings.ReelSpinCap);
            else if (taut && newLength > oldLength)
                LockSpeed *= Mathf.Sqrt(oldLength / newLength);
            Length = newLength;

            // Spin-up: faster every lap, up to the cap. Conservation may carry you past the cap
            // (that IS the wind-in payoff); the spin-up simply stops adding once you are there.
            if (LockSpeed < Settings.LockMaxSpeed)
                LockSpeed = Mathf.Min(Settings.LockMaxSpeed, LockSpeed + Settings.LockSpinRate * dt);
            LockSpeed = Mathf.Min(LockSpeed, Settings.MaxBallSpeed);

            Vector3 tangent = _lockDirection - n * Vector3.Dot(_lockDirection, n);
            tangent = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : AnyPerpendicular(n, shipVelocity);

            Vector3 next = shipPosition + tangent * (LockSpeed * dt);
            Vector3 fromPivot = next - Pivot;
            float nextDist = fromPivot.magnitude;
            IsTaut = nextDist >= Length;
            if (IsTaut && nextDist > 1e-5f)
                next = Pivot + fromPivot * (Length / nextDist);

            // The hull travels the CHORD so it lands exactly on the circle; the orbital speed is
            // kept in LockSpeed so the chord's tiny shortfall never accumulates into a stall.
            Vector3 velocity = (next - shipPosition) / dt;
            Vector3 n2 = (next - Pivot).sqrMagnitude > 1e-8f ? (next - Pivot).normalized : n;
            Vector3 t2 = velocity - n2 * Vector3.Dot(velocity, n2);
            if (t2.sqrMagnitude > 1e-6f) _lockDirection = t2.normalized;
            return velocity;
        }

        static Vector3 AnyPerpendicular(Vector3 n, Vector3 hint)
        {
            Vector3 p = Vector3.Cross(n, hint.sqrMagnitude > 1e-6f ? hint : Vector3.up);
            if (p.sqrMagnitude < 1e-6f) p = Vector3.Cross(n, Vector3.right);
            if (p.sqrMagnitude < 1e-6f) p = Vector3.Cross(n, Vector3.forward);
            return p.normalized;
        }

        // ------------------------------------------------------------------ prediction + smash rules

        /// <summary>
        /// The READY cue: the ball's speed if the right trigger were released NOW and the chain
        /// reeled all the way in, by spin conservation from the current length. Deliberately
        /// ignores the snap crack and the ship's own turning — so it UNDERESTIMATES during a fast
        /// snap (a known weakness, recorded in THRESHER.md).
        /// </summary>
        public float PredictReelCrackSpeed(Vector3 shipPosition, Vector3 shipVelocity)
        {
            if (Mode != ThresherMode.Free) return BallSpeed;
            Vector3 r = BallPosition - shipPosition;
            float dist = r.magnitude;
            if (dist < 1e-5f) return BallSpeed;
            Vector3 n = r / dist;
            Vector3 relative = BallVelocity - shipVelocity;
            Vector3 tangential = relative - n * Vector3.Dot(relative, n);
            float mult = dist > Settings.RestLength ? dist / Settings.RestLength : 1f;
            return Mathf.Min(Settings.MaxBallSpeed, (shipVelocity + tangential * mult).magnitude);
        }

        /// <summary>How white-hot a ball at <paramref name="speed"/> is: 0 at smash speed, 1 at
        /// <see cref="ThresherChainSettings.WhiteHotSpeed"/>.</summary>
        public static float Heat01(float speed, in ThresherChainSettings s)
            => Mathf.Clamp01(Mathf.InverseLerp(s.SmashSpeed, Mathf.Max(s.SmashSpeed + 1e-3f, s.WhiteHotSpeed), speed));

        /// <summary>True when a hit at <paramref name="speed"/> SMASHES (destroys anything, explodes)
        /// rather than CRUSHES (destroys rival mass only, no explosion).</summary>
        public static bool IsSmash(float speed, in ThresherChainSettings s) => speed >= s.SmashSpeed;

        /// <summary>
        /// The fraction of its speed the ball keeps after destroying one prism: <c>PloughKeep</c>
        /// for a smash at smash speed rising to <c>PloughKeepHot</c> when white-hot (a hotter ball
        /// ploughs further), <c>CrushKeep</c> below smash speed. <paramref name="wrecker"/> is the
        /// Mass level-5 upgrade: a smash costs the ball nothing.
        /// </summary>
        public static float KeepAfterHit(float speed, in ThresherChainSettings s, bool wrecker = false)
            => IsSmash(speed, s)
                ? (wrecker ? 1f : Mathf.Lerp(s.PloughKeep, s.PloughKeepHot, Heat01(speed, s)))
                : s.CrushKeep;

        /// <summary>
        /// The ball bounces off a prism it must not destroy (its own domain's). It is put back at
        /// the <paramref name="contact"/> point and the part of its velocity driving INTO the
        /// prism is reflected with <see cref="ThresherChainSettings.BounceRestitution"/>; the
        /// part sliding along the surface is kept. A ball already moving away is left alone, so a
        /// contact that lasts several frames bounces once. Returns true if it bounced.
        /// </summary>
        public bool Bounce(Vector3 contact, Vector3 outwardNormal)
        {
            if (Mode == ThresherMode.Pivot || outwardNormal.sqrMagnitude < 1e-8f) return false;
            Vector3 n = outwardNormal.normalized;
            float into = Vector3.Dot(BallVelocity, n);
            if (into >= 0f) return false;
            BallVelocity -= n * ((1f + Settings.BounceRestitution) * into);
            BallPosition = contact;
            _wasSlack = false;
            return true;
        }

        /// <summary>Apply one hit's speed loss to the ball. Called by the executor per prism, in
        /// path order, so a long row costs the ball speed one prism at a time.</summary>
        public void RegisterHit(float keep)
        {
            if (Mode == ThresherMode.Pivot) return;
            BallVelocity *= Mathf.Clamp01(keep);
        }
    }

    /// <summary>
    /// The chain itself: a verlet rope of <c>N</c> links pinned to the hull and the ball. It is
    /// GAMEPLAY, not decoration — the chain slices prisms, so it is stepped inside the chain step
    /// on every peer (no rendering involved) and the renderer only draws what it holds. Pure C#.
    ///
    /// Links are max-distance only, so a slack chain really sags and a taut one is a straight
    /// line; space has no gravity, so a slack chain drifts with light damping.
    /// </summary>
    public sealed class ThresherChainLinks
    {
        public readonly Vector3[] Points;
        readonly Vector3[] _previous;
        bool _seeded;

        public ThresherChainLinks(int links)
        {
            int n = Mathf.Max(2, links) + 1;
            Points = new Vector3[n];
            _previous = new Vector3[n];
        }

        public int Count => Points.Length;

        /// <summary>Velocity of node <paramref name="i"/> over the last step.</summary>
        public Vector3 NodeVelocity(int i, float dt)
            => dt > 0f ? (Points[i] - _previous[i]) / dt : Vector3.zero;

        public void Reset(Vector3 hull, Vector3 ball)
        {
            int n = Points.Length;
            for (int i = 0; i < n; i++)
                Points[i] = _previous[i] = Vector3.Lerp(hull, ball, i / (float)(n - 1));
            _seeded = true;
        }

        /// <param name="length">The chain's current length (the rope's max distance).</param>
        public void Step(Vector3 hull, Vector3 ball, float length, float damping = 0.92f, int iterations = 6)
        {
            if (!_seeded) { Reset(hull, ball); return; }
            int n = Points.Length;

            // The two pins move with what they are pinned to; their "previous" is where they were.
            _previous[0] = Points[0];
            _previous[n - 1] = Points[n - 1];
            for (int i = 1; i < n - 1; i++)
            {
                Vector3 p = Points[i];
                Points[i] += (p - _previous[i]) * damping;
                _previous[i] = p;
            }

            float seg = length / (n - 1);
            for (int iter = 0; iter < iterations; iter++)
            {
                Points[0] = hull;
                Points[n - 1] = ball;
                for (int i = 0; i < n - 1; i++)
                {
                    Vector3 d = Points[i + 1] - Points[i];
                    float len = d.magnitude;
                    if (len <= seg || len < 1e-5f) continue;
                    Vector3 corr = d * ((len - seg) / len * 0.5f);
                    if (i != 0) Points[i] += corr;
                    if (i + 1 != n - 1) Points[i + 1] -= corr;
                }
            }
            Points[0] = hull;
            Points[n - 1] = ball;
        }
    }

    /// <summary>
    /// The camera arithmetic the Thresher needs, pure so it is tested rather than eyeballed: how
    /// far back a chase camera must sit to keep the ball in frame, the asymmetric ease toward that
    /// distance (out fast, back in slowly), and where to put a still camera that WATCHES a fast
    /// planted orbit instead of riding it.
    /// </summary>
    public static class ThresherCameraFraming
    {
        /// <summary>
        /// The smallest follow distance behind the hull that keeps a ball of radius
        /// <paramref name="radius"/> at <paramref name="ballLocal"/> (in the HULL's frame: x right,
        /// y up, z forward) inside the view. The camera is posed as <c>CustomCameraController</c>
        /// poses it: at (0, <paramref name="height"/>, -d), LOOKING AT the hull (so it pitches down
        /// by atan(height / d)), up = the hull's up. The ball must be at least
        /// <paramref name="minAhead"/> in front of it and inside both half-angles shrunk by
        /// <paramref name="margin"/>. Solved by bisection on <see cref="Frames"/> — the pitch makes
        /// it non-linear in d, and a level-camera closed form zoomed out for a reeled-in ball
        /// hanging low behind the hull, i.e. on every turn.
        /// </summary>
        public static float RequiredDistance(Vector3 ballLocal, float radius, float height,
                                             float halfFovVerticalRad, float halfFovHorizontalRad,
                                             float margin, float minAhead)
        {
            float tanV = Mathf.Max(0.05f, Mathf.Tan(halfFovVerticalRad));
            float tanH = Mathf.Max(0.05f, Mathf.Tan(halfFovHorizontalRad));
            float m = Mathf.Max(1f, margin);

            float hi = 1f;
            for (int i = 0; i < 24 && !Frames(ballLocal, radius, height, hi, tanV, tanH, m, minAhead); i++)
                hi *= 2f;
            float lo = 0f;
            for (int i = 0; i < 24; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (Frames(ballLocal, radius, height, mid, tanV, tanH, m, minAhead)) hi = mid;
                else lo = mid;
            }
            return hi;
        }

        /// <summary>Is the ball inside the margin-shrunk view of a chase camera
        /// <paramref name="distance"/> behind the hull (see <see cref="RequiredDistance"/>)?</summary>
        public static bool Frames(Vector3 ballLocal, float radius, float height, float distance,
                                  float tanV, float tanH, float margin, float minAhead)
        {
            float len = Mathf.Sqrt(height * height + distance * distance);
            if (len < 1e-4f) return false;
            // Camera basis in the hull's frame: forward = (0, -h, d) / len, up = (0, d, h) / len.
            float ry = ballLocal.y - height, rz = ballLocal.z + distance;
            float depth = (-ry * height + rz * distance) / len;
            float vertical = (ry * distance + rz * height) / len;
            if (depth < minAhead + radius) return false;
            return (Mathf.Abs(ballLocal.x) + radius) * margin <= tanH * depth &&
                   (Mathf.Abs(vertical) + radius) * margin <= tanV * depth;
        }

        /// <summary>Exponential ease toward <paramref name="target"/> at <paramref name="outRate"/>
        /// (1/s) when it is FARTHER than <paramref name="current"/> and <paramref name="inRate"/>
        /// when it is nearer: zoom out as fast as the ability needs, come back gently.</summary>
        public static float Ease(float current, float target, float outRate, float inRate, float dt)
        {
            float rate = target > current ? outRate : inRate;
            return current + (target - current) * (1f - Mathf.Exp(-Mathf.Max(0f, rate) * Mathf.Max(0f, dt)));
        }

        /// <summary>Angular speed (rad/s) of a planted orbit.</summary>
        public static float SpinRate(float orbitSpeed, float radius)
            => radius > 1e-3f ? orbitSpeed / radius : 0f;

        /// <summary>
        /// A still vantage that frames a whole orbit of radius <paramref name="radius"/> round
        /// <paramref name="pivot"/>: on the orbit plane's <paramref name="normal"/>, tilted by
        /// <paramref name="tiltDegrees"/> toward <paramref name="outward"/> (a unit vector in the
        /// plane), far enough that the circle fits the narrower half-angle with
        /// <paramref name="margin"/> to spare.
        /// </summary>
        public static Vector3 SpectatePosition(Vector3 pivot, Vector3 normal, Vector3 outward, float radius,
                                               float halfFovRad, float tiltDegrees, float margin)
        {
            float tan = Mathf.Max(0.05f, Mathf.Tan(halfFovRad));
            float d = radius * Mathf.Max(1f, margin) / tan;
            float a = tiltDegrees * Mathf.Deg2Rad;
            Vector3 dir = normal.normalized * Mathf.Cos(a) + outward.normalized * Mathf.Sin(a);
            return pivot + dir.normalized * d;
        }
    }
}
