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
        /// <summary>Max rate (rad/s) at which full stick tilts a planted orbit's plane.</summary>
        public float PivotSteerRate;
        /// <summary>Hook-on: the fastest the ship's heading turns onto the orbit (rad/s; 0 = snap),
        /// and how fast (1/s) the chain reels back the stretch that swing-out took.</summary>
        public float HookTurnRate, HookReturnRate;
        // Windmill (an omni crystal): the ball circles the SHIP at this speed for a while
        public float WindmillSpeed;
        /// <summary>ANALOG TRIGGERS (Thresher Plant Lab round 2, <c>Docs/Studios/THRESHER_PLANT_LAB.md</c>).
        /// Off: the shipped digital controls (LT plants, RT held lets chain out). On: LT's depth is a BRAKE
        /// that stops the ball only at a full pull; RT's depth is the chain's RIGIDITY (pulled = rigid,
        /// released = a slack bungee); there is no winch.</summary>
        public bool Analog;
        /// <summary>Analog: ball brake just short of a full pull (1/s), and the curve on the pull's depth.</summary>
        public float BrakeMax, BrakeCurve;
        /// <summary>Analog: the ship's share of the chain's pull = shipped share + the rest x depth^this.</summary>
        public float AnchorCurve;
        /// <summary>Analog: peak deceleration of the full-pull stop (u/s^2, game units).</summary>
        public float StopDecel;
        /// <summary>Analog: the chain's natural frequency released / fully pulled (rad/s), its damping
        /// ratio, the curve on RT's depth, and the most its rigidity may change per second (0 = at once).</summary>
        public float ChainSlackRate, ChainRigidRate, ChainDamping, RigidityCurve, RigiditySlew;
        /// <summary>Analog: the hook's duration (an S-curve that carries the ship's incoming turn).</summary>
        public float AnalogHookSeconds;
        /// <summary>Seconds over which the planted spin-up fades in (0 = full rate from the first frame).</summary>
        public float SpinEase;
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
        [Tooltip("While planted, full stick tilts the orbit's plane at this rate (degrees/s) - under half " +
                 "the hull's free-flight 120 deg/s, so the spin stays the ship's and the stick steers it. " +
                 "0 = sticks ignored while planted. A rate, so not scaled.")]
        [SerializeField] float pivotSteer = 45f;
        [Tooltip("Hook-on: when the plant catches, the ship's heading turns onto the orbit at most this fast " +
                 "(degrees/s) instead of snapping (it used to turn up to 91 deg in ONE frame). The chain stretches " +
                 "to let the ship swing wide while it turns. 0 = the old snap. A rate, so not scaled.")]
        [SerializeField] float hookTurnRate = 300f;
        [Tooltip("How fast the chain reels back the stretch the hook-on swing took (1/s, exponential).")]
        [SerializeField] float hookReturnRate = 5f;

        [Header("Analog triggers (Thresher Plant Lab round 2)")]
        [Tooltip("Off: the digital controls (LT plants; RT held lets chain out, released reels in). On: LT's DEPTH is a " +
                 "brake that stops the ball only at a FULL pull; RT's DEPTH is the chain's rigidity (pulled = rigid, " +
                 "released = a slack bungee) and there is no winch. Docs/Studios/THRESHER_PLANT_LAB.md round 2.")]
        [SerializeField] bool analogTriggers = false;
        [Tooltip("Analog: how hard the ball brakes just short of a full pull (1/s). A rate, so not scaled.")]
        [SerializeField] float analogBrake = 4f;
        [Tooltip("Analog: brake = depth^this x analogBrake. Above 1 the first part of the pull is gentle and the last part bites.")]
        [SerializeField] float analogBrakeCurve = 1.5f;
        [Tooltip("Analog: as LT deepens the braking ball becomes an anchor: the ship's share of the chain's pull = the " +
                 "shipped tug share + the rest x depth^this, so a half pull already swings the ship round a sliding ball.")]
        [SerializeField] float analogAnchorCurve = 2f;
        [Tooltip("Analog: peak deceleration of the stop at a full pull (sandbox units/s^2; 2443 = 450 u/s^2 in game). " +
                 "The stop takes 1.5 x the ball's speed / this, along an S-curve, so a slow squeeze stops gently.")]
        [SerializeField] float analogStopDecel = 2442.857f;
        [Tooltip("Analog: the chain's natural frequency with RT released (rad/s): a soft bungee. A rate, so not scaled.")]
        [SerializeField] float chainSlackRate = 1.2f;
        [Tooltip("Analog: the chain's natural frequency at a full RT pull (rad/s): ~1 u of stretch at full spin.")]
        [SerializeField] float chainRigidRate = 30f;
        [Tooltip("Analog: the chain's damping ratio (1 = critically damped).")]
        [SerializeField] float chainDamping = 0.8f;
        [Tooltip("Analog: rigidity = slack x (rigid/slack)^(depth^this), a log scale between the two.")]
        [SerializeField] float rigidityCurve = 1f;
        [Tooltip("Analog: the most the chain's rigidity may change per second, as a fraction of RT's travel - a SLAMMED " +
                 "RT then pulls the ball in over 1/this s instead of in one frame. 0 = it follows the trigger at once.")]
        [SerializeField] float rigiditySlew = 0f;
        [Tooltip("Analog: the hook onto the orbit at a full pull, an S-curve that starts at the ship's own turn rate (s).")]
        [SerializeField] float analogHookSeconds = 0.5f;
        [Tooltip("Seconds over which the planted spin-up fades in (0 = full lockSpin from the first frame).")]
        [SerializeField] float spinEase = 0f;

        [Header("Windmill (omni crystal)")]
        [Tooltip("Collecting an OMNI crystal throws the ball into a windmill: it circles the ship at this speed " +
                 "(sandbox units/s; 1000 sits between smash 700 and white-hot 1300, so the windmill is red-hot " +
                 "all the way round) in the plane across the ship's flight, at the chain's current length.")]
        [SerializeField] float windmillSpeed = 1000f;

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
                PivotSteerRate = Mathf.Max(0f, pivotSteer) * Mathf.Deg2Rad,
                HookTurnRate = Mathf.Max(0f, hookTurnRate) * Mathf.Deg2Rad,
                HookReturnRate = Mathf.Max(0f, hookReturnRate),
                WindmillSpeed = Mathf.Max(0f, windmillSpeed) * k,
                CruiseSpeed = gameCruise,
                Analog = analogTriggers,
                BrakeMax = Mathf.Max(0f, analogBrake),
                BrakeCurve = Mathf.Max(0.1f, analogBrakeCurve),
                AnchorCurve = Mathf.Max(0.1f, analogAnchorCurve),
                StopDecel = Mathf.Max(1f, analogStopDecel * k),
                ChainSlackRate = Mathf.Max(0.05f, chainSlackRate),
                ChainRigidRate = Mathf.Max(0.1f, chainRigidRate),
                ChainDamping = Mathf.Max(0f, chainDamping),
                RigidityCurve = Mathf.Max(0.05f, rigidityCurve),
                RigiditySlew = Mathf.Max(0f, rigiditySlew),
                AnalogHookSeconds = Mathf.Max(0.02f, analogHookSeconds),
                SpinEase = Mathf.Max(0f, spinEase),
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
        /// <summary>An omni crystal: the ball circles the SHIP like a windmill's sails, across its
        /// line of flight, for a while (<see cref="ThresherChainSolver.StartWindmill"/>).</summary>
        Windmill = 3,
    }

    /// <summary>What <see cref="ThresherChainSolver.AnalogTriggers"/> decided this frame (analog triggers only).</summary>
    public enum ThresherTriggerEdge
    {
        None = 0,
        /// <summary>LT reached a full pull: the ball has started its stop and the ship its hook.</summary>
        Stop = 1,
        /// <summary>LT fell below the let-go depth while stopped or planted: the caller lets go (or arms the
        /// aimed launch), exactly as the digital LT-up edge does.</summary>
        LetGo = 2,
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
        /// <summary>True on the frame an ARMED launch (<see cref="ThresherChainSolver.ArmLaunch"/>)
        /// reached its launch point and the ship left the orbit along <see cref="LaunchDirection"/>.</summary>
        public bool Launched;
        public Vector3 LaunchDirection;
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
        float _windmillRadius;
        Vector3 _planeNormal;      // steered orbit plane (zero = leave the plane alone)
        Vector3 _hookHeading;      // the ship's heading while it turns onto the orbit (zero = on it)
        float _hookSlack;          // how far past the chain's length the hook-on swing is allowed
        float _hookSlackSpeed;     // d(slack)/dt - the reel-back is a spring, so radial speed ramps
        Vector3 _launchAim;        // armed launch direction (zero = none armed)
        float _launchBoost;        // the armed launch leaves this many times the orbit's speed (1 = none)
        float _lineAge = -1f;      // seconds an aimed launch has flown its line (negative = not on one)
        bool _lineHauling;         // on the line, the ball has not fallen in behind the ship yet
        bool _ltLetGo;             // an aimed launch left and LT has not come back to rest: its depth anchors nothing

        // Analog triggers (Settings.Analog). The lab's names are kept in comments so the port reads beside
        // Docs/Studios/ThresherPlantLab.html, which Tools/Build/thresher_plant_lab/parity.cjs holds it to.
        bool _braking;             // the pivot is the ball still stopping (lab: braking)
        float _brakeT, _stopT;     // time into the full-pull stop, and its length
        Vector3 _brakeV0;          // the ball's velocity when the stop began
        Vector3 _hookStart;        // the heading the hook starts from
        float _hookT;              // time into the analog hook
        float _pivotAge;           // seconds since the pivot began (the spin-up fade-in)
        float _turnIn;             // the ship's turn rate coming in, about the orbit normal (rad/s)
        Vector3 _prevVel, _curVel; bool _hasVel; float _velDt;   // the last two towing velocities (the turn rate)
        bool _wasHooking;          // the hook was running last frame (its end hands over to the elastic state)
        float _sinceHook;          // seconds since the hook handed over
        float _e0;                 // the stretch the hook left the ship at

        /// <summary>Analog: LT's depth this frame (0..1).</summary>
        public float LeftTriggerDepth { get; private set; }
        /// <summary>Analog: the chain's rigidity this frame (0 = slack .. 1 = rigid), RT's depth after
        /// <see cref="ThresherChainSettings.RigiditySlew"/>.</summary>
        public float Rigidity { get; private set; }
        /// <summary>Analog: the ball is still stopping (the pivot moves with it).</summary>
        public bool IsBraking => _braking;
        /// <summary>How far an ARMED launch leaves BEFORE its point (radians), so a spin wound down over the
        /// next frames turns the ship onto the aim instead of past it. Set each frame by the transformer;
        /// 0 = leave at the point along the aim (the shipped launch).</summary>
        public float LaunchLeadRadians { get; set; }
        /// <summary>How far out the chain reaches: the winch length, or (analog, planted) rest length plus
        /// the elastic stretch.</summary>
        public float Extent => Settings.Analog && Mode == ThresherMode.Pivot ? Length + _hookSlack : Length;

        /// <summary>Seconds of windmill left; 0 outside <see cref="ThresherMode.Windmill"/>.</summary>
        public float WindmillTimeLeft { get; private set; }

        /// <summary>The ship is still swinging onto the orbit after the plant caught (turning onto
        /// the tangent, or reeling back the stretch that swing took).</summary>
        public bool IsHookingOn => Mode == ThresherMode.Pivot && (_hookHeading.sqrMagnitude > 0.5f || _hookSlack > 0f);

        /// <summary>A launch is armed: the ship keeps orbiting until it reaches the launch point.</summary>
        public bool LaunchArmed => _launchAim.sqrMagnitude > 0.5f;

        /// <summary>
        /// An ARMED launch has left and is flying its LINE: the chain hauls the ball after the ship and does
        /// not pull the ship, so the ship flies where the crosshair pointed whatever the triggers do. Without
        /// it the ball, still sitting at the old pivot, swung the ship back round it: with RT held rigid and
        /// LT just past the let-go point (its depth still anchors the ball) the heading went 177 degrees off
        /// the aim within 1.5 s (Garrett, 2026-10-21: "if I release the ball and keep the tension it doesn't
        /// always shoot me at the cross hair"). Two phases: until the ball trails within
        /// <see cref="LaunchLineTrailDegrees"/> of straight behind, the chain does not pull the ship at all;
        /// after that it pulls as usual but only ALONG the ship's line of flight - it can slow the ship, never
        /// turn or reverse it - until the ball has settled into the tow: trailing, the ship no faster than the ball
        /// can follow (<see cref="ThresherChainSettings.MaxBallSpeed"/>), and the ball not closing on the ship.
        /// The boosted launch leaves at up to ~1 300 u/s off a Time-raised spin against the ball's 479 u/s cap: a
        /// rigid chain then brakes the ship below the ball's own speed and the ball overtakes it, which pulled
        /// the ship 30-55 degrees off the aim once the line let go early. The line also ends after
        /// <see cref="LaunchLineMaxSeconds"/>, or on a new plant or windmill.
        /// </summary>
        public bool OnLaunchLine => _lineAge >= 0f;
        /// <summary>The ball counts as trailing (the launch line ends) within this angle of straight behind the ship.</summary>
        public const float LaunchLineTrailDegrees = 10f;
        /// <summary>The longest a launch line holds, however the ball is lying.</summary>
        public const float LaunchLineMaxSeconds = 3f;
        /// <summary>
        /// After an aimed launch, LT's depth brakes and anchors nothing until the trigger eases back under this
        /// (or is pulled full again, which plants). The launch fires as LT passes the let-go point, so the
        /// trigger is still most of the way down: read as a partial pull, that depth made the ball a sliding
        /// anchor that swung the ship round it, off the line it was aimed along.
        /// </summary>
        public const float LaunchReleaseRestDepth = 0.2f;
        /// <summary>LT was let go for an aimed launch and has not come back to rest yet (<see cref="LaunchReleaseRestDepth"/>).</summary>
        public bool LeftTriggerLetGo => _ltLetGo;

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
            WindmillTimeLeft = 0f;
            _planeNormal = Vector3.zero;
            _launchAim = Vector3.zero;
            _lineAge = -1f;
            _ltLetGo = false;
            _hookHeading = Vector3.zero;
            _hookSlack = 0f;
            _hookSlackSpeed = 0f;
            IsTaut = true;
            _braking = false; _hasVel = false; _wasHooking = false;
            LeftTriggerDepth = 0f; Rigidity = 0f; LaunchLeadRadians = 0f;
        }

        // ------------------------------------------------------------------ lock edges

        /// <summary>Left trigger down: start the skid. The ball keeps sliding (and smashing) and
        /// becomes the pivot once the skid is spent.</summary>
        public void Plant()
        {
            if (Mode != ThresherMode.Free) return;
            Mode = ThresherMode.Skidding;
            _skidTime = 0f;
            _lineAge = -1f;
        }

        /// <summary>Left trigger up: the ship flies off on its current velocity and, if the ball
        /// was the pivot, the ball is yanked after it at <see cref="ThresherChainSettings.Yank"/> of
        /// the ship's speed.</summary>
        public void Release(Vector3 shipVelocity)
        {
            // The windmill owns the ball until it runs out; a left-trigger release mid-windmill
            // (the trigger was held when the crystal was collected) must not cut it short.
            if (Mode == ThresherMode.Windmill) return;
            _launchAim = Vector3.zero;
            _planeNormal = Vector3.zero;
            _hookHeading = Vector3.zero;
            _hookSlack = 0f;
            _hookSlackSpeed = 0f;
            // (a ball let go while it is still stopping keeps its own speed; only a pinned ball is yanked)
            if (Mode == ThresherMode.Pivot && !_braking)
            {
                BallVelocity = Vector3.ClampMagnitude(shipVelocity * Settings.Yank, Settings.MaxBallSpeed);
            }
            Mode = ThresherMode.Free;
            LockSpeed = 0f;
            _wasSlack = false;
            _braking = false;
        }

        /// <summary>
        /// An omni crystal: for <paramref name="seconds"/> the ball circles the SHIP at
        /// <see cref="ThresherChainSettings.WindmillSpeed"/>, in the plane across the ship's line of
        /// flight (a windmill's sails facing the wind), at the chain's length — the winch still
        /// works, so the pilot sizes the windmill. A planted pivot is abandoned (the ship flies on at
        /// its orbit velocity). Collecting another crystal mid-windmill tops the clock up, never
        /// stacks it. When the clock runs out the ball is let go Free at the speed it has, and the
        /// ordinary rope takes over.
        /// </summary>
        public void StartWindmill(Vector3 shipPosition, float seconds)
        {
            if (seconds <= 0f) return;
            if (Mode != ThresherMode.Windmill)
                _windmillRadius = Mathf.Max(1e-3f, (BallPosition - shipPosition).magnitude);
            Mode = ThresherMode.Windmill;
            LockSpeed = 0f;
            _braking = false;
            _launchAim = Vector3.zero;
            _lineAge = -1f;
            _planeNormal = Vector3.zero;
            WindmillTimeLeft = Mathf.Max(WindmillTimeLeft, seconds);
        }

        /// <summary>
        /// Steer the planted orbit's PLANE: from the next step the ship's circle is laid into the
        /// plane with this normal (either sign — the spin keeps its own sense), so turning the aim
        /// camera turns the whole spin with it. Zero hands the plane back (only
        /// <see cref="SteerPivot"/> leans it then). Each frame's change is small, so re-laying the
        /// ship onto the new plane is a small move, never a jump.
        /// </summary>
        public void SteerOrbitPlane(Vector3 normal)
            => _planeNormal = normal.sqrMagnitude > 1e-8f ? normal.normalized : Vector3.zero;

        /// <summary>
        /// ARM a launch along <paramref name="aim"/>: rather than leaving the orbit wherever the ship
        /// is when the trigger comes up, it keeps circling until it reaches the one point of the
        /// circle whose tangent IS the aim (<see cref="LaunchRadial"/>), and leaves from there —
        /// so the pilot picks a point, and the timing is the solver's job. A re-arm replaces the aim;
        /// <see cref="Release"/> (or a windmill) disarms. Ignored unless planted.
        /// The launch leaves <paramref name="boost"/> times the orbit's speed however long it waited
        /// for its point: the aimed launch is rewarded for being AIMED, never for its timing
        /// (2026-10-13; pass 1 for none).
        /// </summary>
        public void ArmLaunch(Vector3 aim, float boost = 1f)
        {
            if (Mode != ThresherMode.Pivot || aim.sqrMagnitude < 1e-8f) return;
            _launchAim = aim.normalized;
            _launchBoost = Mathf.Max(1f, boost);
        }

        /// <summary>
        /// The aimed launch's EXIT BUFF (Garrett, 2026-10-30: "come out of a dual trigger swing with a speed buff that
        /// makes the exit speed 3x what it would be with the current physics, this can decay over 3 seconds ... to
        /// compensate for the time lost staying still"): the multiplier on the hull's travel <paramref name="t"/>
        /// seconds after the launch, <paramref name="multiplier"/> at the launch easing linearly to 1 at
        /// <paramref name="seconds"/> (the fleet's own <c>ModifyThrottle</c> shape). 1 outside it.
        /// </summary>
        public static float LaunchExitMultiplier(float t, float multiplier, float seconds)
            => seconds > 0f && t >= 0f && t < seconds ? Mathf.Lerp(Mathf.Max(1f, multiplier), 1f, t / seconds) : 1f;

        /// <summary>
        /// Carry the ball (and a pivot) by <paramref name="delta"/> with the hull: the share of the hull's travel the
        /// physics did not make (<see cref="LaunchExitMultiplier"/>). The rig moves as one body, so the chain sees the
        /// ship exactly where its own physics put it relative to the ball and nothing brakes the extra; velocities are
        /// untouched.
        /// </summary>
        public void Carry(Vector3 delta)
        {
            BallPosition += delta;
            if (Mode == ThresherMode.Pivot) Pivot += delta;
        }

        /// <summary>
        /// Seconds until the ship, circling at its current speed, reaches the launch point for
        /// <paramref name="aim"/> — the wait an LT release NOW would cost. Infinity when there is no
        /// launch point (not planted, or the aim is along the orbit's axis).
        /// </summary>
        public float SecondsToLaunchPoint(Vector3 shipPosition, Vector3 aim)
        {
            if (!LaunchRadial(shipPosition, aim, out Vector3 target)) return float.PositiveInfinity;
            Vector3 r = shipPosition - Pivot;
            float dist = r.magnitude;
            if (dist < 1e-5f || LockSpeed <= 1e-4f) return float.PositiveInfinity;
            Vector3 rh = r / dist;
            Vector3 t = Vector3.Cross(OrbitNormal(shipPosition), rh);
            float ahead = Mathf.Atan2(Vector3.Dot(target, t), Vector3.Dot(target, rh));
            if (ahead < 0f) ahead += 2f * Mathf.PI;
            return ahead * dist / LockSpeed;
        }

        /// <summary>
        /// Pull a FREE ball toward <paramref name="target"/> at <paramref name="acceleration"/>
        /// (u/s²) for one frame — the Charge level-5 attraction to enemy vessels. A planted ball is a
        /// pivot and a windmill's ball is driven round its circle, so neither is pulled; the rope
        /// still has the last word on where a free ball can go.
        /// </summary>
        public void AttractBall(Vector3 target, float acceleration, float dt)
        {
            if (Mode != ThresherMode.Free && Mode != ThresherMode.Skidding) return;
            Vector3 to = target - BallPosition;
            if (to.sqrMagnitude < 1e-6f || acceleration <= 0f || dt <= 0f) return;
            BallVelocity = Vector3.ClampMagnitude(BallVelocity + to.normalized * (acceleration * dt), Settings.MaxBallSpeed);
        }

        /// <summary>The planted orbit's normal in its TRAVEL sense (velocity = normal × radial), or
        /// zero when not planted / degenerate.</summary>
        public Vector3 OrbitNormal(Vector3 shipPosition)
        {
            if (Mode != ThresherMode.Pivot) return Vector3.zero;
            Vector3 r = shipPosition - Pivot;
            Vector3 n = Vector3.Cross(r, _lockDirection);
            return n.sqrMagnitude > 1e-10f ? n.normalized : Vector3.zero;
        }

        /// <summary>
        /// The unit radial (from the pivot) of the point on the planted circle where the ship's
        /// velocity points along <paramref name="aim"/>: with travel normal N, velocity at radial r is
        /// N × r, so that point is r = aim × N (aim laid into the orbit plane first). False when not
        /// planted, or the aim is along the orbit's axis (it has no such point).
        /// </summary>
        public bool LaunchRadial(Vector3 shipPosition, Vector3 aim, out Vector3 radial)
        {
            radial = Vector3.zero;
            Vector3 n = OrbitNormal(shipPosition);
            if (n.sqrMagnitude < 0.5f) return false;
            Vector3 a = Vector3.ProjectOnPlane(aim, n);
            if (a.sqrMagnitude < 1e-6f) return false;
            radial = Vector3.Cross(a.normalized, n).normalized;
            return true;
        }

        /// <summary>The launch point for <paramref name="aim"/> (see <see cref="LaunchRadial"/>) on
        /// the circle the ship is ACTUALLY flying — its own distance from the pivot, never the
        /// chain's length. While the winch pays out the chain is longer than the ship's orbit until
        /// the ship swings out to it, and a point drawn on the chain's circle sat outside the ship's
        /// path: the aim led to a circumference the ship was not on. The pivot when there is none.</summary>
        public Vector3 LaunchPoint(Vector3 shipPosition, Vector3 aim)
            => LaunchRadial(shipPosition, aim, out var r) ? Pivot + r * (shipPosition - Pivot).magnitude : Pivot;

        // ------------------------------------------------------------------ the step

        /// <summary>
        /// Advance one frame. <paramref name="shipVelocity"/> is what the flight model wants the
        /// ship to do this frame; the return value is what it actually does once the chain has had
        /// its say. <paramref name="payOut"/> is the right trigger: held lets chain out, released
        /// reels it in. <paramref name="shipCruise"/> is the ship's current throttle target — the
        /// tug's floor is a fraction of it, so the floor follows the pilot's own throttle.
        /// <paramref name="pivotSteer"/> is the stick as a world direction (magnitude 0..1, the way
        /// the nose would move); while planted it tilts the orbit (see <see cref="SteerPivot"/>), and
        /// it is ignored while towing, where the sticks fly the hull directly.
        /// </summary>
        public ThresherStepResult Step(Vector3 shipPosition, Vector3 shipVelocity, bool payOut,
                                    float shipCruise, float dt, Vector3 pivotSteer = default)
        {
            var result = new ThresherStepResult { ShipVelocity = shipVelocity, BallFrom = BallPosition };
            if (dt <= 0f) return result;

            if (Mode == ThresherMode.Windmill)
            {
                StepWindmill(shipPosition, shipVelocity, payOut, dt);
                return result;   // the ship flies on untouched: the windmill pulls on nothing
            }

            if (Mode == ThresherMode.Pivot)
            {
                if (TryLaunch(shipPosition, dt, ref result)) return result;
                SteerPivot(shipPosition, pivotSteer, dt);
                result.ShipVelocity = StepPivot(shipPosition, shipVelocity, payOut, dt, out bool stopped);
                result.BallFrom = BallPosition;
                result.Planted = stopped;
                return result;
            }

            if (Settings.Analog) return StepAnalog(shipPosition, shipVelocity, shipCruise, dt, result);

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
                float shipShare = OnLaunchLine && _lineHauling ? 0f : ShipShare(Settings);

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

                result.ShipVelocity = KeepOnLine(shipVelocity, ApplyShipFloor(shipVelocity, newShipVelocity, shipCruise), shipCruise);
            }

            BallVelocity = Vector3.ClampMagnitude(BallVelocity, Settings.MaxBallSpeed);
            BallPosition = ballNext;
            IsTaut = taut;
            _wasSlack = dist < Length * (1f - SlackTolerance);
            AdvanceLaunchLine(shipPosition + result.ShipVelocity * dt, result.ShipVelocity, dt);

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

        // ------------------------------------------------------------------ analog triggers

        /// <summary>
        /// ANALOG TRIGGERS, once per frame before the move (Settings.Analog only). Takes the triggers'
        /// depths (0..1): RT becomes the chain's <see cref="Rigidity"/> (at most
        /// <see cref="ThresherChainSettings.RigiditySlew"/> per second), and LT's depth brakes the towed
        /// ball. LT reaching <paramref name="fullPull"/> starts the stop (and the hook);
        /// falling below <paramref name="letGoBelow"/> while stopped or planted asks the caller to let go.
        /// <paramref name="shipUp"/> is the hull's up: an ambiguous plant orbits in the hull's turning plane.
        /// </summary>
        public ThresherTriggerEdge AnalogTriggers(float leftDepth, float rightDepth, float fullPull, float letGoBelow,
                                                  Vector3 shipPosition, Vector3 shipVelocity, Vector3 shipUp, float dt)
        {
            if (!Settings.Analog) return ThresherTriggerEdge.None;
            LeftTriggerDepth = Mathf.Clamp01(leftDepth);
            AnalogFullPull = Mathf.Clamp(fullPull, 0.05f, 1f);
            float rt = Mathf.Clamp01(rightDepth);
            Rigidity = Settings.RigiditySlew > 0f ? Mathf.MoveTowards(Rigidity, rt, Settings.RigiditySlew * Mathf.Max(0f, dt)) : rt;
            if (_ltLetGo && LeftTriggerDepth < LaunchReleaseRestDepth) _ltLetGo = false;
            if (Mode == ThresherMode.Free && LeftTriggerDepth >= fullPull)
            {
                _ltLetGo = false;
                AnalogBeginStop(shipPosition, shipVelocity, shipUp);
                return ThresherTriggerEdge.Stop;
            }
            if ((Mode == ThresherMode.Skidding || Mode == ThresherMode.Pivot) && LeftTriggerDepth < letGoBelow && !LaunchArmed)
                return ThresherTriggerEdge.LetGo;
            return ThresherTriggerEdge.None;
        }

        /// <summary>The chain's natural frequency (rad/s) at rigidity <paramref name="rigidity"/> (lab: ropeW).</summary>
        public float ChainRate(float rigidity)
            => Settings.ChainSlackRate * Mathf.Pow(Settings.ChainRigidRate / Settings.ChainSlackRate,
                                                   Mathf.Pow(Mathf.Clamp01(rigidity), Settings.RigidityCurve));

        /// <summary>The chain's pull per unit mass along it (>= 0: a chain cannot push): a damped spring past
        /// rest length at the current rigidity, plus a stiff limit past the chain's full length (lab: tension).</summary>
        float Tension(float dist, float radialSpeed)
        {
            float w = ChainRate(Rigidity), a = 0f;
            if (dist > Settings.RestLength) a += w * w * (dist - Settings.RestLength) + 2f * Settings.ChainDamping * w * radialSpeed;
            if (dist > Settings.MaxLength)
            {
                float wr = 2f * Settings.ChainRigidRate;
                a += wr * wr * (dist - Settings.MaxLength) + 1.4f * wr * Mathf.Max(0f, radialSpeed);
            }
            return Mathf.Max(0f, a);
        }

        /// <summary>
        /// A full pull: the ball stops along an S-curve, and the ship starts its hook round the STOPPING
        /// ball at once. Without that the stiff chain yanks a ship flying straight away from the ball back
        /// through zero speed and whips the hull round at ~2 000 deg/s (lab round 2, Found 1).
        /// </summary>
        void AnalogBeginStop(Vector3 shipPosition, Vector3 shipVelocity, Vector3 shipUp)
        {
            if (Mode != ThresherMode.Free) return;
            Mode = ThresherMode.Skidding;
            _lineAge = -1f;
            _brakeT = 0f;
            _brakeV0 = BallVelocity;
            _stopT = Mathf.Max(0.08f, 1.5f * BallVelocity.magnitude / Settings.StopDecel);
            _braking = true;
            BeginPivot(shipPosition, shipVelocity);
            Length = Settings.RestLength;          // the chain's stretch is the hook's slack from here on

            Vector3 up = shipUp.sqrMagnitude > 1e-6f ? shipUp.normalized : Vector3.up;
            Vector3 rh = (shipPosition - Pivot).normalized, v = shipVelocity.normalized;
            float turnUp = _hasVel && _velDt > 0f ? SignedAngle(_prevVel, _curVel, up) / _velDt : 0f;
            // Which way round: planted nearly straight away from the ball (within 30 degrees) the sideways
            // velocity that picks the orbit's direction is noise (and BeginPivot's AnyPerpendicular fallback
            // can tip the orbit out of the flight plane), so orbit in the hull's own turning plane, the way
            // the ship is ALREADY turning (lab round 2, Found 5).
            if (Mathf.Abs(Vector3.Dot(v, rh)) > Mathf.Cos(30f * Mathf.Deg2Rad))
            {
                Vector3 flat = Vector3.Cross(up, rh).normalized;
                if (flat.sqrMagnitude > 0.5f) _lockDirection = Vector3.Dot(flat, _lockDirection) >= 0f ? flat : -flat;
                if (Mathf.Abs(turnUp) > 5f * Mathf.Deg2Rad && Mathf.Sign(SignedAngle(v, _lockDirection, up)) != Mathf.Sign(turnUp))
                    _lockDirection = -_lockDirection;
            }
            // The ship's turn rate coming in, about the orbit normal: the hook starts from it.
            Vector3 n = Vector3.Cross(rh, _lockDirection).normalized;
            _turnIn = _hasVel && _velDt > 0f ? SignedAngle(_prevVel, _curVel, n) / _velDt : 0f;
        }

        /// <summary>Towing (and the first frame of a stop) with analog triggers: the chain is a spring,
        /// sub-stepped so the stiffest spring in play stays inside semi-implicit Euler's stable range
        /// (w x h &lt;= 0.15) at ANY frame rate - a rigid 30 rad/s chain at 30 fps is otherwise at the edge.</summary>
        ThresherStepResult StepAnalog(Vector3 shipPosition, Vector3 shipVelocity, float shipCruise, float dt, ThresherStepResult result)
        {
            var s = Settings;
            bool stopping = Mode == ThresherMode.Skidding;
            float baseShare = ShipShare(s);
            float depth = stopping ? 1f : _ltLetGo ? 0f : Mathf.Clamp01(LeftTriggerDepth / Mathf.Max(1e-3f, AnalogFullPull));
            float share = stopping ? 1f : baseShare + (1f - baseShare) * Mathf.Pow(depth, s.AnchorCurve);
            if (OnLaunchLine && _lineHauling) share = 0f;   // the ship flies its aimed line; the chain only hauls the ball
            result.BallFrom = BallPosition;

            float wMax = Mathf.Max(ChainRate(Rigidity), (BallPosition - shipPosition).magnitude > s.MaxLength * 0.9f ? 2f * s.ChainRigidRate : 0f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(wMax * dt / 0.15f));
            float h = dt / steps;
            Vector3 shipP = shipPosition, ship = shipVelocity;
            float dist = (BallPosition - shipPosition).magnitude;
            for (int i = 0; i < steps; i++)
            {
                Vector3 d = BallPosition - shipP;
                dist = d.magnitude;
                Vector3 n = dist > 1e-5f ? d / dist : Vector3.zero;
                float a = dist > 1e-5f ? Tension(dist, Vector3.Dot(BallVelocity - ship, n)) : 0f;
                ship += n * (a * share * h);
                if (stopping)
                {
                    _brakeT += h;
                    BallVelocity = _brakeV0 * (1f - Smooth(_brakeT / _stopT));
                }
                else
                {
                    BallVelocity -= n * (a * (1f - share) * h);
                    BallVelocity *= Mathf.Exp(-(s.BallDrag + Mathf.Pow(depth, s.BrakeCurve) * s.BrakeMax) * h);
                    BallVelocity = Vector3.ClampMagnitude(BallVelocity, s.MaxBallSpeed);
                }
                BallPosition += BallVelocity * h;
                shipP += ship * h;
            }
            // The transformer moves the hull by velocity x dt once, so hand it the sub-steps' mean velocity.
            result.ShipVelocity = KeepOnLine(shipVelocity, ApplyShipFloor(shipVelocity, (shipP - shipPosition) / dt, shipCruise), shipCruise);
            _prevVel = _hasVel ? _curVel : shipVelocity;
            _curVel = result.ShipVelocity;
            _hasVel = true;
            _velDt = dt;
            IsTaut = dist > s.RestLength;
            _wasSlack = false;
            Length = Mathf.Min(s.MaxLength, Mathf.Max(s.RestLength, dist));
            AdvanceLaunchLine(shipP, result.ShipVelocity, dt);
            return result;
        }

        /// <summary>Age the launch line by one Free frame: the haul ends once the ball trails the ship, and the line
        /// once the ball has settled into the tow - trailing, the ship no faster than the ball can follow, the ball not
        /// closing on it (or it has held <see cref="LaunchLineMaxSeconds"/>). <paramref name="shipPosition"/> is where
        /// the ship is after this frame's move.</summary>
        void AdvanceLaunchLine(Vector3 shipPosition, Vector3 shipVelocity, float dt)
        {
            if (_lineAge < 0f) return;
            _lineAge += dt;
            Vector3 back = BallPosition - shipPosition;
            bool trailing = shipVelocity.sqrMagnitude > 1e-6f && back.sqrMagnitude > 1e-6f &&
                            Vector3.Dot(back.normalized, -shipVelocity.normalized) >= Mathf.Cos(LaunchLineTrailDegrees * Mathf.Deg2Rad);
            if (_lineHauling && trailing) _lineHauling = false;
            bool closing = back.sqrMagnitude > 1e-6f && Vector3.Dot(BallVelocity - shipVelocity, back) < 0f;
            bool settled = !_lineHauling && trailing && !closing && shipVelocity.magnitude <= Settings.MaxBallSpeed;
            if (settled || _lineAge >= LaunchLineMaxSeconds) _lineAge = -1f;
        }

        /// <summary>On the launch line the chain changes the ship's speed along the line it is flying
        /// (<paramref name="before"/>, the flight model's own direction this frame, so the pilot's stick and the
        /// release wind-down still turn it) and nothing else: no sideways pull, and never below the tug's floor.</summary>
        Vector3 KeepOnLine(Vector3 before, Vector3 after, float shipCruise)
        {
            if (!OnLaunchLine || before.sqrMagnitude < 1e-8f) return after;
            Vector3 line = before.normalized;
            float floor = Mathf.Min(before.magnitude, Settings.MinShipFraction * Mathf.Max(0f, shipCruise));
            return line * Mathf.Max(floor, Vector3.Dot(after, line));
        }

        /// <summary>The full-pull depth a partial pull's brake and anchor curves are measured against (the
        /// <c>fullPull</c> last passed to <see cref="AnalogTriggers"/>).</summary>
        float AnalogFullPull = 1f;

        /// <summary>Advance the full-pull stop one frame; true when the ball has come to rest (lab: advanceBrake).</summary>
        bool AdvanceBrake(float dt)
        {
            _brakeT += dt;
            float u = _brakeT / _stopT;
            BallVelocity = _brakeV0 * (1f - Smooth(u));
            BallPosition += BallVelocity * dt;
            return u >= EndOfCurve;
        }

        /// <summary>A clock that adds dt N times lands a hair short of N x dt in float32 (30 x 1/60 =
        /// 0.49999997), so "u &gt;= 1" ends a curve a frame late on one machine and on time on another; the
        /// lab (float64) is held to this solver frame by frame, so both end a curve at this.</summary>
        const float EndOfCurve = 1f - 1e-4f;

        static float Smooth(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }

        /// <summary>Signed angle (radians) from <paramref name="a"/> to <paramref name="b"/> about <paramref name="axis"/>.</summary>
        static float SignedAngle(Vector3 a, Vector3 b, Vector3 axis)
        {
            Vector3 an = a.normalized, bn = b.normalized;
            return Mathf.Atan2(Vector3.Dot(Vector3.Cross(an, bn), axis.normalized), Vector3.Dot(an, bn));
        }

        /// <summary>A fraction <paramref name="w"/> of the way from unit <paramref name="a"/> to unit
        /// <paramref name="b"/> along the great circle (lab: slerpUnit).</summary>
        static Vector3 SlerpUnit(Vector3 a, Vector3 b, float w)
        {
            float c = Mathf.Clamp(Vector3.Dot(a, b), -1f, 1f), ang = Mathf.Acos(c);
            if (ang < 1e-5f) return b;
            Vector3 perp = b - a * c;
            if (perp.sqrMagnitude < 1e-10f) perp = AnyPerpendicular(a, Vector3.up);
            perp.Normalize();
            return (a * Mathf.Cos(ang * w) + perp * Mathf.Sin(ang * w)).normalized;
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

        // ------------------------------------------------------------------ the windmill

        /// <summary>
        /// One windmill frame. The ball's offset from the ship is laid into the plane across the
        /// ship's velocity (parallel transport: the sails keep their phase as the ship turns), turned
        /// about that velocity by <c>WindmillSpeed / radius × dt</c>, and put back at the radius,
        /// which eases onto the chain's length at the reel rate so the ball is pulled onto the circle
        /// rather than teleported there. The ball's velocity is what that move implies, so the sweep
        /// and the heat colour see the real speed (ship velocity + the sails' tangential speed).
        /// </summary>
        void StepWindmill(Vector3 shipPosition, Vector3 shipVelocity, bool payOut, float dt)
        {
            Length = payOut
                ? Mathf.Min(Settings.MaxLength, Length + Settings.PayOutRate * dt)
                : Mathf.Max(Settings.RestLength, Length - Settings.ReelInRate * dt);
            _windmillRadius = Mathf.MoveTowards(_windmillRadius, Length,
                Mathf.Max(Settings.PayOutRate, Settings.ReelInRate) * dt);

            Vector3 axis = shipVelocity.sqrMagnitude > 1e-6f ? shipVelocity.normalized
                : (_lockDirection.sqrMagnitude > 1e-6f ? _lockDirection : Vector3.forward);
            _lockDirection = axis;
            Vector3 radial = Vector3.ProjectOnPlane(BallPosition - shipPosition, axis);
            radial = radial.sqrMagnitude > 1e-8f ? radial.normalized : AnyPerpendicular(axis, Vector3.up);

            float radius = Mathf.Max(1e-3f, _windmillRadius);
            float angle = Settings.WindmillSpeed / radius * dt;
            Vector3 sail = Vector3.Cross(axis, radial);   // the sails' direction of travel
            Vector3 turned = (radial * Mathf.Cos(angle) + sail * Mathf.Sin(angle)).normalized;

            Vector3 ballNext = shipPosition + shipVelocity * dt + turned * radius;
            BallVelocity = Vector3.ClampMagnitude((ballNext - BallPosition) / dt, Settings.MaxBallSpeed);
            BallPosition = ballNext;
            IsTaut = true;

            WindmillTimeLeft -= dt;
            if (WindmillTimeLeft > 0f) return;
            WindmillTimeLeft = 0f;
            Mode = ThresherMode.Free;
            _wasSlack = false;
        }

        // ------------------------------------------------------------------ the pivot

        void BeginPivot(Vector3 shipPosition, Vector3 shipVelocity)
        {
            Mode = ThresherMode.Pivot;
            Pivot = BallPosition;
            if (!_braking) BallVelocity = Vector3.zero;   // (analog: a stopping ball keeps moving)

            Vector3 r = shipPosition - Pivot;
            Vector3 n = r.sqrMagnitude > 1e-8f ? r.normalized : Vector3.forward;
            Vector3 tangent = shipVelocity - n * Vector3.Dot(shipVelocity, n);
            _lockDirection = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : AnyPerpendicular(n, shipVelocity);
            // HOOK-ON: keep flying the way the ship was going and turn onto the orbit at a capped
            // rate (StepPivot), rather than replacing the heading outright in one frame.
            _hookHeading = Settings.HookTurnRate > 0f && shipVelocity.sqrMagnitude > 1e-6f
                ? shipVelocity.normalized : Vector3.zero;
            _hookSlack = 0f;
            _hookSlackSpeed = 0f;

            // Keep the speed you hooked on with — but never hook on at a crawl: a pilot who plants
            // while flying straight AT the ball has no tangential speed at all, and a zero orbit
            // is the stall this mechanic must never produce.
            LockSpeed = Mathf.Max(shipVelocity.magnitude * Settings.LockKeep,
                                  Settings.MinShipFraction * Settings.CruiseSpeed);
            _hookStart = _hookHeading;
            _hookT = 0f;
            _pivotAge = 0f;
            _wasHooking = true;
        }

        /// <summary>
        /// An armed launch leaves on the frame the ship would otherwise carry PAST its launch point:
        /// the angle still to go round (in the travel sense) is within this frame's angular step.
        /// It leaves along the aim at the orbit's speed — the tangent there, by construction — and
        /// the ball is yanked after it exactly as <see cref="Release"/> yanks it.
        /// </summary>
        bool TryLaunch(Vector3 shipPosition, float dt, ref ThresherStepResult result)
        {
            if (!LaunchArmed) return false;
            if (!LaunchRadial(shipPosition, _launchAim, out Vector3 target)) return false;
            Vector3 r = shipPosition - Pivot;
            float dist = r.magnitude;
            if (dist < 1e-5f) return false;
            Vector3 n = OrbitNormal(shipPosition);
            Vector3 rh = r / dist;
            Vector3 t = Vector3.Cross(n, rh);
            float ahead = Mathf.Atan2(Vector3.Dot(target, t), Vector3.Dot(target, rh));
            if (ahead < 0f) ahead += 2f * Mathf.PI;
            float step = LockSpeed / Mathf.Max(1e-3f, dist) * dt;
            float lead = Mathf.Max(0f, LaunchLeadRadians);
            if (ahead > step + lead) return false;

            Vector3 aim = _launchAim;
            // Shipped: leave along the aim. With a LEAD (the transformer winds the spin down after the
            // launch): leave along the chord this frame's orbit step would fly, and let the wind-down turn the
            // heading onto the aim, so the velocity never steps.
            Vector3 dir = lead > 0f
                ? Vector3.Cross(n, rh * Mathf.Cos(step * 0.5f) + t.normalized * Mathf.Sin(step * 0.5f)).normalized
                : aim;
            Vector3 velocity = dir * (LockSpeed * Mathf.Max(1f, _launchBoost));
            Release(velocity);   // Pivot -> Free, the yank, disarms
            _lineAge = 0f;       // ...and flies its line (OnLaunchLine)
            _lineHauling = true;
            _ltLetGo = Settings.Analog;   // ...and LT anchors nothing until it is back at rest
            result.ShipVelocity = velocity;
            result.BallFrom = BallPosition;
            result.Launched = true;
            result.LaunchDirection = aim;
            return true;
        }

        Vector3 StepPivot(Vector3 shipPosition, Vector3 shipVelocity, bool payOut, float dt, out bool stopped)
        {
            stopped = false;
            if (_braking)
            {
                // Analog: the pivot is the ball still stopping.
                if (AdvanceBrake(dt)) { _braking = false; BallVelocity = Vector3.zero; stopped = true; }
                Pivot = BallPosition;
            }
            else
            {
                BallPosition = Pivot;
                BallVelocity = Vector3.zero;
            }
            _pivotAge += dt;

            Vector3 r = shipPosition - Pivot;
            float dist = r.magnitude;
            Vector3 n = dist > 1e-5f ? r / dist : Vector3.forward;

            if (!Settings.Analog)   // (analog: no winch; the rigidity sets the radius below)
            {
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
            }

            // Spin-up: faster every lap, up to the cap. Conservation may carry you past the cap
            // (that IS the wind-in payoff); the spin-up simply stops adding once you are there.
            // SpinEase fades the spin-up in from the pivot's first frame (0 = full rate at once).
            float spinRate = Settings.LockSpinRate * (Settings.SpinEase > 0f ? Smooth(_pivotAge / Settings.SpinEase) : 1f);
            if (LockSpeed < Settings.LockMaxSpeed)
                LockSpeed = Mathf.Min(Settings.LockMaxSpeed, LockSpeed + spinRate * dt);
            LockSpeed = Mathf.Min(LockSpeed, Settings.MaxBallSpeed);

            Vector3 tangent = _lockDirection - n * Vector3.Dot(_lockDirection, n);
            tangent = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : AnyPerpendicular(n, shipVelocity);

            // HOOK-ON: the heading chases the tangent at HookTurnRate; while it does, the chain
            // stretches to let the ship swing wide (no clamp), and afterwards it reels the stretch
            // back at HookReturnRate. The ship never stalls and never turns faster than the cap.
            Vector3 heading = tangent;
            bool hooking = _hookHeading.sqrMagnitude > 0.5f;
            if (hooking && Settings.Analog)
            {
                // Analog: an S-curve hook (zero turn rate at its start) PLUS a carried turn that starts at the
                // ship's own incoming turn rate and fades out by the hook's end, so a ship already curving
                // round a braking ball keeps curving instead of stopping its turn (lab round 2, Found 2).
                _hookT += dt;
                float u = Mathf.Clamp01(_hookT / Settings.AnalogHookSeconds);
                Vector3 axis = OrbitNormal(shipPosition);
                heading = SlerpUnit(_hookStart, tangent, Smooth(u));
                float carry = _turnIn * Settings.AnalogHookSeconds * (u - (u * u * u - u * u * u * u / 2f)) * (1f - Smooth(u));
                if (axis.sqrMagnitude > 0.5f && Mathf.Abs(carry) > 1e-6f)
                    heading = (heading * Mathf.Cos(carry) + Vector3.Cross(axis, heading) * Mathf.Sin(carry)).normalized;
                _hookHeading = heading;
                if (u >= EndOfCurve) _hookHeading = Vector3.zero;
            }
            else if (hooking)
            {
                _hookHeading = TurnTowards(_hookHeading, tangent, Settings.HookTurnRate * dt);
                heading = _hookHeading;
                if (Vector3.Dot(_hookHeading, tangent) > 0.99995f) _hookHeading = Vector3.zero;   // on the orbit
            }

            Vector3 next = shipPosition + heading * (LockSpeed * dt);
            Vector3 fromPivot = next - Pivot;

            // A steered plane (the aim camera): lay the step onto it, so the whole circle turns
            // with the aim. The spin's sense is kept — only the plane moves.
            if (_planeNormal.sqrMagnitude > 0.5f)
            {
                Vector3 laid = Vector3.ProjectOnPlane(fromPivot, _planeNormal);
                if (laid.sqrMagnitude > 1e-8f) fromPivot = laid.normalized * fromPivot.magnitude;
            }

            float nextDist = fromPivot.magnitude;
            if (hooking)
            {
                // The swing-out stretches it; remember how fast it is still swinging out, so the
                // reel-back starts from that speed rather than from a standstill.
                float stretch = Mathf.Max(0f, nextDist - Length);
                _hookSlackSpeed = (stretch - _hookSlack) / dt;
                _hookSlack = Mathf.Max(_hookSlack, stretch);
            }
            else if (Settings.Analog)
            {
                StepElasticRadius(shipVelocity, n, dist, dt);
            }
            else if (_hookSlack > 0f)
            {
                // ...and it reels back as a CRITICALLY DAMPED spring: the radial speed ramps from
                // the swing-out's own speed, never steps (an exponential reel-back stepped it, a
                // one-frame 47 deg turn), and the stretch never overshoots below the chain's length.
                float k = Mathf.Max(0.1f, Settings.HookReturnRate);
                _hookSlackSpeed += (-k * k * _hookSlack - 2f * k * _hookSlackSpeed) * dt;
                _hookSlack += _hookSlackSpeed * dt;
                if (_hookSlack < 0.01f && Mathf.Abs(_hookSlackSpeed) < 0.5f) { _hookSlack = 0f; _hookSlackSpeed = 0f; }
                _hookSlack = Mathf.Max(0f, _hookSlack);
            }
            float reach = Length + _hookSlack;
            IsTaut = nextDist >= reach;
            // (analog, once on the orbit: the ship rides the elastic radius, in or out)
            if ((IsTaut || (Settings.Analog && !hooking)) && nextDist > 1e-5f)
                fromPivot *= reach / nextDist;
            next = Pivot + fromPivot;

            // The hull travels the CHORD so it lands exactly on the circle; the orbital speed is
            // kept in LockSpeed so the chord's tiny shortfall never accumulates into a stall.
            Vector3 velocity = (next - shipPosition) / dt;
            Vector3 n2 = (next - Pivot).sqrMagnitude > 1e-8f ? (next - Pivot).normalized : n;
            Vector3 t2 = velocity - n2 * Vector3.Dot(velocity, n2);
            if (_planeNormal.sqrMagnitude > 0.5f) t2 = Vector3.ProjectOnPlane(t2, _planeNormal);
            // (analog: the orbit's direction, once chosen, never flips - a heading swinging across the radial
            // line mid-hook would otherwise reverse the tangent and the whole swing; lab round 2, Found 5)
            if (t2.sqrMagnitude > 1e-6f && (!Settings.Analog || Vector3.Dot(t2, tangent) > 0f)) _lockDirection = t2.normalized;
            return velocity;
        }

        /// <summary>
        /// Analog, once on the orbit: the stretch past rest length is the chain's ELASTIC state. It springs,
        /// at the rigidity RT sets, toward the stretch where the orbit's outward pull balances the chain, and
        /// spin is conserved as the radius changes: pull RT and the ship is reeled in and spins up; ease off
        /// and it swings out and slows. Three hand-over rules keep it free of one-frame steps (lab round 2,
        /// Found 3-4): it starts at the ship's REAL stretch and radial speed; its rate ramps from the shipped
        /// hookReturnRate to the rigidity's own over 0.5 s; and its target eases in from where the hook left
        /// the ship. Sub-stepped like <see cref="StepAnalog"/>.
        /// </summary>
        void StepElasticRadius(Vector3 shipVelocity, Vector3 n, float dist, float dt)
        {
            var s = Settings;
            if (_wasHooking)
            {
                _hookSlack = Mathf.Max(0f, dist - Length);
                _hookSlackSpeed = Vector3.Dot(shipVelocity, n);
                _wasHooking = false;
                _sinceHook = 0f;
                _e0 = _hookSlack;
            }
            _sinceHook += dt;
            float wRope = ChainRate(Rigidity), wBack = Mathf.Min(wRope, s.HookReturnRate);
            float w = wBack + (wRope - wBack) * Smooth(_sinceHook / 0.5f);
            float room = s.MaxLength - s.RestLength, r0 = Mathf.Max(1f, dist);
            float eqFull = Mathf.Min(room, LockSpeed * LockSpeed / (r0 * w * w));
            float eq = _e0 + (eqFull - _e0) * Smooth(_sinceHook / 0.5f);
            float before = _hookSlack;
            int steps = Mathf.Max(1, Mathf.CeilToInt(w * dt / 0.15f));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                _hookSlackSpeed += (w * w * (eq - _hookSlack) - 2f * s.ChainDamping * w * _hookSlackSpeed) * h;
                _hookSlack += _hookSlackSpeed * h;
                if (_hookSlack < 0f) { _hookSlack = 0f; _hookSlackSpeed = Mathf.Max(0f, _hookSlackSpeed); }
                if (_hookSlack > room) { _hookSlack = room; _hookSlackSpeed = Mathf.Min(0f, _hookSlackSpeed); }
            }
            float rOld = s.RestLength + before, rNew = s.RestLength + _hookSlack;
            LockSpeed = Mathf.Min(s.MaxBallSpeed, LockSpeed * Mathf.Min(s.ReelSpinCap, Mathf.Max(1f / s.ReelSpinCap, rOld / rNew)));
        }

        /// <summary>
        /// Low-sensitivity control while planted: lean the orbit. The ship's heading can only turn
        /// about the chain (anything else would lengthen or shorten a taut chain), so the stick's
        /// component across the orbit plane — along <c>n × t</c>, the plane's normal at the ship —
        /// rotates the heading about the chain by at most <see cref="ThresherChainSettings.PivotSteerRate"/>
        /// × dt. Speed, radius and spin are untouched: it tilts the plane the ship spins in and
        /// nothing else. The in-plane part of the stick (speed up / slow down) is ignored.
        /// </summary>
        public void SteerPivot(Vector3 shipPosition, Vector3 steer, float dt)
        {
            if (Mode != ThresherMode.Pivot || Settings.PivotSteerRate <= 0f || dt <= 0f) return;
            if (steer.sqrMagnitude < 1e-6f) return;
            Vector3 r = shipPosition - Pivot;
            if (r.sqrMagnitude < 1e-8f) return;
            Vector3 n = r.normalized;
            Vector3 t = _lockDirection - n * Vector3.Dot(_lockDirection, n);
            if (t.sqrMagnitude < 1e-8f) return;
            t.Normalize();
            Vector3 b = Vector3.Cross(n, t);
            float across = Mathf.Clamp(Vector3.Dot(Vector3.ClampMagnitude(steer, 1f), b), -1f, 1f);
            float angle = across * Settings.PivotSteerRate * dt;
            _lockDirection = t * Mathf.Cos(angle) + b * Mathf.Sin(angle);
        }

        /// <summary>Turn unit vector <paramref name="from"/> toward unit <paramref name="to"/> by at
        /// most <paramref name="maxRadians"/> (in the plane they span).</summary>
        static Vector3 TurnTowards(Vector3 from, Vector3 to, float maxRadians)
        {
            float cos = Mathf.Clamp(Vector3.Dot(from, to), -1f, 1f);
            float angle = Mathf.Acos(cos);
            if (angle <= maxRadians || angle < 1e-5f) return to;
            Vector3 perp = to - from * cos;
            if (perp.sqrMagnitude < 1e-10f) perp = AnyPerpendicular(from, Vector3.up);
            perp.Normalize();
            return (from * Mathf.Cos(maxRadians) + perp * Mathf.Sin(maxRadians)).normalized;
        }

        static Vector3 AnyPerpendicular(Vector3 n, Vector3 hint)
        {
            Vector3 p = Vector3.Cross(n, hint.sqrMagnitude > 1e-6f ? hint : Vector3.up);
            if (p.sqrMagnitude < 1e-6f) p = Vector3.Cross(n, Vector3.right);
            if (p.sqrMagnitude < 1e-6f) p = Vector3.Cross(n, Vector3.forward);
            return p.normalized;
        }

        // ------------------------------------------------------------------ smash rules

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
        /// ploughs further), <c>CrushKeep</c> below smash speed.
        /// </summary>
        public static float KeepAfterHit(float speed, in ThresherChainSettings s)
            => IsSmash(speed, s) ? Mathf.Lerp(s.PloughKeep, s.PloughKeepHot, Heat01(speed, s)) : s.CrushKeep;

        /// <summary>
        /// The ball bounces off a prism it must not destroy (its own domain's). It is put back at
        /// the <paramref name="contact"/> point and the part of its velocity driving INTO the
        /// prism is reflected with <see cref="ThresherChainSettings.BounceRestitution"/>; the
        /// part sliding along the surface is kept. A ball already moving away is left alone, so a
        /// contact that lasts several frames bounces once. Returns true if it bounced.
        /// </summary>
        public bool Bounce(Vector3 contact, Vector3 outwardNormal)
        {
            if (Mode == ThresherMode.Pivot || Mode == ThresherMode.Windmill || outwardNormal.sqrMagnitude < 1e-8f) return false;
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
            // A planted ball has no speed to lose, and a windmill's ball is driven round its circle.
            if (Mode == ThresherMode.Pivot || Mode == ThresherMode.Windmill) return;
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

        /// <summary>Translate the whole chain with the hull (<see cref="ThresherChainSolver.Carry"/>): every link and its
        /// previous position move together, so no link gains a velocity from it.</summary>
        public void Carry(Vector3 delta)
        {
            for (int i = 0; i < Points.Length; i++) { Points[i] += delta; _previous[i] += delta; }
        }

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

        /// <summary>Verlet relaxation passes for a chain of <paramref name="links"/>: a max-distance
        /// rope converges one link per pass, so a longer chain needs more of them or it reads as
        /// stretched (and its middle sags) while the ends are already taut.</summary>
        public static int IterationsFor(int links) => Mathf.Max(6, (links + 1) / 2);

        /// <summary>Points a smoothed copy of <paramref name="points"/> needs at
        /// <paramref name="subdivisions"/> drawn segments per link.</summary>
        public static int SmoothedCount(int points, int subdivisions)
            => points < 2 ? points : (points - 1) * Mathf.Max(1, subdivisions) + 1;

        /// <summary>
        /// The DRAWN chain: a uniform Catmull-Rom curve through the gameplay links, with
        /// <paramref name="subdivisions"/> segments per link, written into <paramref name="output"/>
        /// (length <see cref="SmoothedCount"/>). It passes through every link exactly, so the
        /// drawn chain never strays from the one that cuts by more than the curve's bow between two
        /// links; the end tangents mirror their neighbour so the pins are not kinked either.
        /// Returns the number of points written.
        /// </summary>
        public static int Smooth(Vector3[] points, int subdivisions, Vector3[] output)
        {
            int n = points.Length;
            int sub = Mathf.Max(1, subdivisions);
            int count = SmoothedCount(n, sub);
            if (output == null || output.Length < count) return 0;
            if (n < 2)
            {
                for (int i = 0; i < n; i++) output[i] = points[i];
                return n;
            }

            int w = 0;
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p1 = points[i], p2 = points[i + 1];
                Vector3 p0 = i > 0 ? points[i - 1] : 2f * p1 - p2;
                Vector3 p3 = i + 2 < n ? points[i + 2] : 2f * p2 - p1;
                for (int k = 0; k < sub; k++)
                {
                    float t = k / (float)sub, t2 = t * t, t3 = t2 * t;
                    output[w++] = 0.5f * (2f * p1 + (p2 - p0) * t
                                          + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                                          + (3f * p1 - p0 - 3f * p2 + p3) * t3);
                }
            }
            output[w++] = points[n - 1];
            return w;
        }
    }

    /// <summary>
    /// The camera arithmetic the Thresher needs, pure so it is tested rather than eyeballed: how
    /// far back a chase camera must sit to keep the ball in frame, the asymmetric ease toward that
    /// distance (out fast, back in slowly), and the launch camera and sight of a spectated planted
    /// orbit (its pose, the aim's stick response, the zoom).
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

        /// <summary>
        /// The REACH envelope: the distance that keeps a ball anywhere on the chain's horizontal
        /// circle of radius <paramref name="length"/> in frame — sampled every 15° of yaw (the
        /// ring is left/right symmetric, so 0..180° covers it). The zoom tracks this, a function of
        /// the chain's length alone, so it follows the winch instead of pumping in and out with
        /// every swing; the ball's own position (<see cref="RequiredDistance"/>) only ever adds.
        /// </summary>
        public static float ReachDistance(float length, float radius, float height,
                                          float halfFovVerticalRad, float halfFovHorizontalRad,
                                          float margin, float minAhead)
        {
            float worst = 0f;
            for (int deg = 0; deg <= 180; deg += 15)
            {
                float a = deg * Mathf.Deg2Rad;
                var ball = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * length;
                worst = Mathf.Max(worst, RequiredDistance(ball, radius, height,
                    halfFovVerticalRad, halfFovHorizontalRad, margin, minAhead));
            }
            return worst;
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
        /// The launch view's GRIP (<c>ThresherExecutor.UpdateLaunchView</c>): with the digital triggers a held
        /// plant is enough; with analog triggers only the ball STOPPED and both triggers at a full pull - LT's
        /// depth and the chain's rigidity (RT after its slew) at <paramref name="fullPull"/>. On a phone both
        /// thumbs off a planted spin reads as exactly that (<see cref="ThresherTouchLaunch"/>).
        /// </summary>
        public static bool LaunchViewGrip(bool analog, bool braking, float leftDepth, float rigidity, float fullPull)
            => !analog || (!braking && leftDepth >= fullPull && rigidity >= fullPull);

        /// <summary>
        /// The LAUNCH camera of a spectated orbit: it follows behind the launch point ON the launch
        /// trajectory — <paramref name="distance"/> back along −<paramref name="aim"/>, raised
        /// <paramref name="elevationDegrees"/> toward <paramref name="aimUp"/> (0 ships: no height
        /// offset). At 0 the trajectory projects to the exact screen centre, which is what lets the
        /// sight be a crosshair. Look along the aim with aimUp as up.
        /// </summary>
        public static Vector3 LaunchCameraPosition(Vector3 launchPoint, Vector3 aim, Vector3 aimUp,
                                                   float distance, float elevationDegrees)
        {
            float a = elevationDegrees * Mathf.Deg2Rad;
            Vector3 dir = -aim.normalized * Mathf.Cos(a) + aimUp.normalized * Mathf.Sin(a);
            return launchPoint + dir.normalized * Mathf.Max(0f, distance);
        }

        /// <summary>
        /// The launch sight's zoom: trigger depth past <paramref name="deadzone"/>, remapped to 0..1
        /// (the Serpent scope's rule, <c>SniperScopeActionExecutor.ResolveZoomTarget</c>). The
        /// Plant engages at 5% depth, so a deadzone above that is a light hold that plants without
        /// zooming and a deeper pull that zooms.
        /// </summary>
        public static float ZoomTarget(float triggerDepth, float deadzone)
        {
            float depth = Mathf.Clamp01(triggerDepth);
            float dead = Mathf.Clamp(deadzone, 0f, 0.95f);
            return depth <= dead ? 0f : Mathf.InverseLerp(dead, 1f, depth);
        }

        /// <summary>
        /// Turn the aim with the stick, in SCREEN terms for the launch camera (looking along the aim,
        /// aimUp up, screen-right = aimUp × aim; the crosshair at screen centre is the aim): stick X swings the aim right/left about
        /// <paramref name="aimUp"/> at <paramref name="yawRate"/> (rad/s), stick Y pitches it at
        /// <paramref name="pitchRate"/> (rad/s) — positive Y pitches it DOWN, the hull's own
        /// nose-down sign, so the pilot's pitch inversion carries over. Pitch turns aim and aimUp
        /// together (the orbit plane tilts with it), so the aim can be swung anywhere on the sphere.
        /// Both stay unit and perpendicular.
        /// </summary>
        public static void TurnAim(ref Vector3 aim, ref Vector3 aimUp, float stickX, float stickY,
                                   float yawRate, float pitchRate, float dt)
        {
            float yaw = Mathf.Clamp(stickX, -1f, 1f) * yawRate * dt;
            float pitch = Mathf.Clamp(stickY, -1f, 1f) * pitchRate * dt;

            if (Mathf.Abs(yaw) > 0f)
            {
                Vector3 right = Vector3.Cross(aimUp, aim);
                aim = aim * Mathf.Cos(yaw) + right * Mathf.Sin(yaw);
            }
            PitchAim(ref aim, ref aimUp, pitch);
        }

        /// <summary>
        /// Pitch aim and aimUp together by <paramref name="radians"/> (positive DOWN, <see cref="TurnAim"/>'s sign),
        /// so the orbit plane tilts with the aim; both come back unit and perpendicular. The snap aim eases the plane
        /// onto the pilot's flight line with it (<see cref="ThresherSnapAim"/>).
        /// </summary>
        public static void PitchAim(ref Vector3 aim, ref Vector3 aimUp, float radians)
        {
            if (Mathf.Abs(radians) > 0f)
            {
                Vector3 a = aim * Mathf.Cos(radians) - aimUp * Mathf.Sin(radians);
                aimUp = aimUp * Mathf.Cos(radians) + aim * Mathf.Sin(radians);
                aim = a;
            }

            // Re-orthonormalise: the aim is the authority, aimUp is laid perpendicular to it.
            aim = aim.sqrMagnitude > 1e-8f ? aim.normalized : Vector3.forward;
            aimUp = Vector3.ProjectOnPlane(aimUp, aim);
            if (aimUp.sqrMagnitude < 1e-8f) aimUp = Vector3.Cross(aim, Vector3.right);
            if (aimUp.sqrMagnitude < 1e-8f) aimUp = Vector3.Cross(aim, Vector3.forward);
            aimUp.Normalize();
        }
    }
}
