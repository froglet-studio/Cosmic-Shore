using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Stoat's SLINGSHOT is the SWING (Yash, 2026-11-11, <c>R_VesselActions/STOAT_DIPOLE.md</c> §1b "The swing"): a lone,
    /// small BLACK HOLE, no white hole, no orbit. PRESS lays it <see cref="StoatSlingConfigSO.SwingAhead"/> ahead and
    /// <see cref="StoatSlingConfigSO.SwingSide"/> to the pressed side. Pass beside it and it SWINGS you toward itself
    /// (ω = v / d, capped) until you have turned the angle the stick asks (<see cref="StoatSlingMath.SwingTurnDegrees"/>, read
    /// every frame: toward the hole the most, away the least; the dotted exit line shows it), then lets you go on that heading.
    /// ONE slingshot (2026-11-19): the swing's own speed-up is kept as momentum, with no second push at the exit
    /// (<see cref="StoatSlingConfigSO.SwingKickShare"/> ships 0). Letting go of the trigger mid-swing lets you go at once; before
    /// it, it does nothing. Passed or out of time, the hole collapses and throws the Charge ripple
    /// (<see cref="StoatRippleExecutor"/>).
    ///
    /// <para><b>Retired 2026-11-21</b> (Yash: "make sure all AI uses our latest slingshot mechanics, and delete all other old
    /// pathfinding and other mechanics"): the catch orbit and its aimed exit, the skim sling, the trampoline and the crystal
    /// style. Every pilot, human or AI, in every mode, flies the swing; there is no other path.</para>
    ///
    /// <para><b>Where it runs.</b> The press and release replicate, so every peer lays its own copy of the hole from the
    /// replicated press. The swing itself is flown on the simulating machine only (an owned hole moves no vessel,
    /// <see cref="BlackHoleVesselPull"/>); the squeeze and the stick are not replicated.</para>
    ///
    /// <para><b>One hole per Stoat.</b> A new press collapses this hull's last hole first; other hulls' holes are left alone.</para>
    /// </summary>
    public sealed class StoatSlingExecutor : ShipActionExecutorBase
    {
        public enum Side { Left = 0, Right = 1 }

        [Header("Config")]
        [Tooltip("Every number of the swing: the hole, the turn, the warp, the autopilot, the audio slots.")]
        [SerializeField] StoatSlingConfigSO config;

        // The autopilot's numbers: the mode's own copy when it has one (AIModeTuning, e.g. StoatSlingConfig_Slingshot trained
        // in the real game), resolved once the pilot's mode is known; this config otherwise. Only the AI's reads go through
        // it - the mechanic itself, and a person flying this hull, read `config`.
        StoatSlingConfigSO _aiConfig;
        StoatSlingConfigSO AIConfig
        {
            get
            {
                if (!_aiConfig) _aiConfig = AIModeTuning.ForPilot(config, _status);
                return _aiConfig ? _aiConfig : config;
            }
        }

        struct Hold
        {
            public bool Holding;
            public float HeldFor;   // seconds, for devices with no analog trigger
            public float Peak;      // the deepest squeeze of the hold - the (optional) fling kick
        }

        readonly Hold[] _holds = new Hold[2];
        IVesselStatus _status;
        ToggleTranslationModeActionExecutor _stance;
        readonly InputEvents?[] _boundInput = new InputEvents?[2];
        readonly List<ShipActionSO> _bindScratch = new();
        float _aiLastSlingTime = float.NegativeInfinity;

        StoatModeExecutor _mode;
        StoatRippleExecutor _ripple;
        bool _resolvedSiblings;
        int _capturedAtLay;     // the hole's CapturedCount when it was laid (Space-5 Accretion counts from it)
        float _accretion;       // Space-5 Accretion: the next ripple's reach bonus, 0..accretionMax
        float _lastTiming = -1f;
        float _lastLaunchAt = float.NegativeInfinity;
        float _momentumClock = 1f;       // the flight clock at the last fling
        float _momentumAt = float.NegativeInfinity;
        float _launchShieldUntil = float.NegativeInfinity;   // Mass 5: the fling's trail is shielded until

        // The swing: a lone black hole that swings the hull round it.
        BlackHole _swingHole;
        bool _swingUp;              // a swing hole is up (every peer)
        bool _swinging;             // ...and it is swinging the hull (the simulating machine)
        bool _swingWasNear;         // the hull has been within its reach (every peer: the Mass-5 shield on the collapse)
        Side _swingSide;
        Vector3 _swingHolePos;
        float _swingReach;
        float _swingAt;
        Vector3 _swingEntry;        // the heading at the swing's start; the exit is it turned
        Vector3 _swingAxis;         // the turn axis, fixed at the start (toward the hole)
        float _swingTurned;         // degrees turned so far
        float _swingWant;           // degrees the stick asks for now
        Vector3 _swingExit = Vector3.forward;   // the heading the swing would throw the hull on now (the dotted line)
        float _swingFxAt = float.NegativeInfinity;
        Vector3 _swingFxCentre;
        Vector3 _swingFxNormal = Vector3.up;

        public bool IsHolding(Side side) => _holds[(int)side].Holding;
        /// <summary>The deepest squeeze of the current (or last) hold, 0..1.</summary>
        public float Hold01(Side side) => _holds[(int)side].Peak;
        /// <summary>True while either trigger is held.</summary>
        public bool IsHoldingAny => _holds[0].Holding || _holds[1].Holding;
        /// <summary>True while the swing turns the hull (the simulating machine). The warp and the HUD's Time card read it.</summary>
        public bool IsOrbiting => _swinging;
        /// <summary>How well a release RIGHT NOW would be timed, 0..1: the swing has no window, so 1 while it swings you.</summary>
        public float Timing01 => _swinging ? 1f : 0f;
        /// <summary>How far through the turn the stick asks for the swing is, 0..1 (the HUD's Time card).</summary>
        public float SweetProgress01 => _swinging ? Mathf.Clamp01(_swingTurned / Mathf.Max(1f, _swingWant)) : 0f;
        /// <summary>Seconds since the last fling (+infinity before the first). The HUD's Mass and Time cards.</summary>
        public float SinceLaunch => Time.time - _lastLaunchAt;
        /// <summary>Space-5 Accretion on the swing: how much of the ripple-reach bonus the hole has earned, 0..1.</summary>
        public float Accretion01 => config && config.AccretionMax > 0f ? Mathf.Clamp01(_accretion / config.AccretionMax) : 0f;
        /// <summary>The Charge ripple the open hole would throw now, as a share of the largest (Accretion full), 0..1; empty
        /// with no hole. HUD only.</summary>
        public float RippleCharge01 => _swingUp && config ? (1f + _accretion) / (1f + config.AccretionMax) : 0f;
        /// <summary>The last fling's timing, 0..1, or −1 before the first.</summary>
        public float LastTiming => _lastTiming;
        public StoatSlingConfigSO Config => config;
        /// <summary>The last fling's momentum as a flight-clock warp, fading to 1 (the pathfinder reads it).</summary>
        public float MomentumWarp => config ? StoatSlingMath.MomentumWarp(_momentumClock, 1f, Time.time - _momentumAt, config.MomentumSeconds) : 1f;

        /// <summary>True while a swing's black hole is up (every peer), and while it swings the hull (the simulating machine).</summary>
        public bool IsSwingUp => _swingUp;
        public bool IsSwinging => _swinging;
        /// <summary>The swing's black hole and the reach it starts a swing within, world.</summary>
        public Vector3 SwingHolePosition => _swingHolePos;
        public float SwingReachRadius => _swingUp ? _swingReach : 0f;
        /// <summary>The heading a swing would throw the hull on right now (the stick's angle about the hole): the dotted line.</summary>
        public Vector3 SwingExitHeading => _swingExit;
        /// <summary>Seconds since the last fling (+∞ before the first), and where: the fling ripple.</summary>
        public float SwingFxAge => Time.time - _swingFxAt;
        public Vector3 SwingFxCentre => _swingFxCentre;
        public Vector3 SwingFxNormal => _swingFxNormal;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _aiConfig = null;
            _holds[0] = default;
            _holds[1] = default;
            _lastLaunchAt = float.NegativeInfinity;
            _swingUp = _swinging = false;
            _swingFxAt = float.NegativeInfinity;
            _resolvedSiblings = false;
            _lastTiming = -1f;
            _stance = TryGetComponent<ActionExecutorRegistry>(out var registry) ? registry.Get<ToggleTranslationModeActionExecutor>() : null;
            if (config == null)
                CSDebug.LogError($"[Stoat] {name}: StoatSlingExecutor has no StoatSlingConfig — the triggers will sling nothing. " +
                                 "Wire Assets/_SO_Assets/VesselActions/Stoat/StoatSlingConfig.asset on the prefab.");
        }

        /// <summary>Trigger PRESS: the swing's black hole is laid ahead on the pressed side.</summary>
        public void BeginHold(Side side)
        {
            ResolveSiblings();
            if (_mode && !_mode.UsesOrbit) return;
            int i = (int)side;
            if (_holds[i].Holding) return;
            _holds[i].Holding = true;
            _holds[i].HeldFor = 0f;
            // An autopilot writes no trigger, so its fixed squeeze is the hold from the start.
            _holds[i].Peak = config != null && _status != null && _status.AutoPilotEnabled ? AIConfig.AutopilotHold01 : 0f;
            if (config == null) return;
            PlayOneShot(config.HoldStartEvent);
            LaySwing(side);
        }

        /// <summary>Trigger RELEASE: a release while the swing turns you lets you go at once; before it, the hole stays up.</summary>
        public void Release(Side side)
        {
            int i = (int)side;
            if (!_holds[i].Holding) return;
            _holds[i].Holding = false;
            if (config == null || _status == null) return;
            if (_swingUp && _swinging && side == _swingSide && MantaStingActionExecutor.IsSimAuthority(_status) &&
                _status.VesselTransformer && _status.Transform)
                Fling(_status.VesselTransformer, _status.Transform);
        }

        /// <summary>A mode switch or a reset: both triggers let go and the hole collapses without a fling. Every peer runs it.</summary>
        public void ReleaseAll()
        {
            if (_swingUp) EndSwing(flung: false);
            _holds[0].Holding = false;
            _holds[1].Holding = false;
        }

        void ResolveSiblings()
        {
            if (_resolvedSiblings) return;
            _resolvedSiblings = true;
            if (!TryGetComponent<ActionExecutorRegistry>(out var registry)) return;
            _mode = registry.Get<StoatModeExecutor>();
            _ripple = registry.Get<StoatRippleExecutor>();
        }

        /// <summary>The speed the hull is actually flying: the swing's turn ω = v / d comes from it.</summary>
        float FlightSpeed() => Mathf.Max(_status.Speed, 1f);

        /// <summary>The engine's own speed: the published speed with the flight-clock warp taken out, so the warp paces the
        /// flight without compounding into the (optional) fling kick.</summary>
        float EngineSpeed()
        {
            var transformer = _status.VesselTransformer;
            float clock = transformer ? Mathf.Max(1f, transformer.FlightTimeScale) : 1f;
            return Mathf.Max(_status.Speed / clock, 1f);
        }

        /// <summary>
        /// True while the swing's hole is up and warps the hull's flight clock: from the press
        /// (<see cref="StoatSlingConfigSO.CatchWarpFromPress"/>) or once it swings you. The pathfinder, the one writer of the
        /// clock, reads it.
        /// </summary>
        public bool WarpsFlight => config && config.CatchWarpShare > 0f && _swingUp && (_swinging || config.CatchWarpFromPress);

        void Detonate(Vector3 at, float separation)
        {
            ResolveSiblings();
            if (_ripple) _ripple.Detonate(at, separation, _accretion);
            _accretion = 0f;
        }

        /// <summary>
        /// Space 5 — Accretion on the swing: while the hole is up it STEALS what falls in (in your domain), and every capture
        /// since it was laid adds accretionPerPrism to its ripple's reach, to accretionMax. Every peer runs this on its own
        /// copy — the upgrade bit, the name and the domain are replicated, and the gravity field counts captures on every peer.
        /// </summary>
        void StepAccretion()
        {
            var black = _swingUp ? _swingHole : null;
            if (!black) return;
            var abilities = _status.ElementalAbilityHandler;
            bool active = _status.Player != null && abilities && abilities.IsUpgradeActive(Element.Space);
            if (!active)
            {
                if (black.PrismCapture == BlackHole.PrismCaptureRule.Steal) black.PrismCapture = BlackHole.PrismCaptureRule.Default;
                return;
            }
            black.PrismCapture = BlackHole.PrismCaptureRule.Steal;
            black.CaptureDomain = _status.Domain;
            black.CaptureOwnerName = _status.PlayerName;
            float before = _accretion;
            _accretion = StoatSlingMath.AccretionBonus(black.CapturedCount - _capturedAtLay, config.AccretionPerPrism, config.AccretionMax);
            if (_accretion > before)
                CSDebug.LogVerbose(CSLogChannel.BlackHole, $"[Stoat] Accretion: {black.CapturedCount - _capturedAtLay} stolen, ripple +{_accretion:P0}");
        }

        // ------------------------------------------------------------------ the swing (STOAT_DIPOLE.md §1b)

        /// <summary>
        /// The press: a lone, small BLACK HOLE laid <see cref="StoatSlingConfigSO.SwingAhead"/> ahead and
        /// <see cref="StoatSlingConfigSO.SwingSide"/> to the pressed side, held still. Owned by this hull, so it pulls no vessel.
        /// Every peer lays its own copy from the replicated press.
        /// </summary>
        void LaySwing(Side side)
        {
            var hull = _status.Transform;
            if (hull == null) return;
            // One per Stoat: a press over a live hole collapses it first.
            if (_swingUp) EndSwing(flung: false);
            var bh = BlackHoleRegistry.Config;
            if (!BlackHoleRegistry.CanSpawn(1))
            {
                CSDebug.LogVerbose(CSLogChannel.BlackHole,
                    $"[Stoat] swing refused — needs one free of {bh.MaxBlackHoles} wormholes ({BlackHoleRegistry.Count} live).");
                return;
            }

            var at = StoatSlingMath.SwingHole(hull.position, hull.forward, hull.right, side == Side.Right, config.SwingAhead, config.SwingSide);
            float strength = config.PoleGM / Mathf.Max(1e-3f, bh.GmPerStrength);
            _swingHole = BlackHoleRegistry.Spawn(at, strength, Vector3.zero, hull.up, config.SwingHorizon, HolePolarity.Sink);
            if (_swingHole == null) return;
            _swingHole.OwnerVessel = OwnerTransform();

            _swingUp = true;
            _swinging = false;
            _swingWasNear = false;
            _swingSide = side;
            _swingHolePos = at;
            // Space is reach: the replicated tier, so every peer starts and ends the swing at the same distance.
            _swingReach = config.SwingReach * Mathf.Max(1f, config.OrbitReach.EvaluateReplicated(_status));
            _swingAt = Time.time;
            _swingTurned = 0f;
            _accretion = 0f;
            _capturedAtLay = _swingHole.CapturedCount;
            // A swing is a launch: leave the hold-still stance, which would otherwise pin the hull.
            if (_status.IsTranslationRestricted && _stance) _stance.EndStance();
            CSDebug.LogVerbose(CSLogChannel.BlackHole,
                $"[Stoat] {side} swing hole laid {config.SwingAhead:F0} u ahead, {config.SwingSide:F0} u aside, reach {_swingReach:F0} u");
        }

        /// <summary>
        /// A frame with a swing hole up. The simulating machine: passing beside it starts the swing; the heading turns toward
        /// the hole at ω = v / d (capped) about the axis fixed at the start, until it has turned the angle the stick asks now
        /// (the autopilot: until it faces its target), then the fling. Every peer: passed untouched (moving away beyond its
        /// reach) or out of time, it collapses.
        /// </summary>
        void StepSwing(float dt)
        {
            var hull = _status.Transform;
            if (!hull) return;
            if (!_swingHole || _swingHole.IsDespawning) { EndSwing(flung: false); return; }
            var toHole = _swingHolePos - hull.position;
            float d = toHole.magnitude;
            if (d <= _swingReach) _swingWasNear = true;

            // The angle the stick asks for now, about the hole's side of the nose: what the dotted line shows.
            var axis = _swinging ? _swingAxis : StoatSlingMath.SwingAxis(hull.forward, toHole, hull.up);
            var entry = _swinging ? _swingEntry : hull.forward;
            Stick(out float x, out float y);
            _swingWant = StoatSlingMath.SwingTurnDegrees(x, y, Vector3.Dot(toHole, hull.right), Vector3.Dot(toHole, hull.up),
                config.SwingMinDegrees, config.SwingMaxDegrees);
            _swingExit = StoatSlingMath.SwingExit(entry, axis, _swingWant);

            var transformer = _status.VesselTransformer;
            if (MantaStingActionExecutor.IsSimAuthority(_status) && transformer && !_status.IsTranslationRestricted)
            {
                if (!_swinging && StoatSlingMath.SwingStarts(hull.position, hull.forward, _swingHolePos, _swingReach, config.SwingCatchDegrees))
                {
                    _swinging = true;
                    _swingEntry = hull.forward;
                    _swingAxis = StoatSlingMath.SwingAxis(hull.forward, toHole, hull.up);
                    _swingTurned = 0f;
                    PlayOneShot(config.SwingStartEvent);
                    if (IsLocalHumanPilot) HapticController.PlaySkim(1f);
                    CSDebug.LogVerbose(CSLogChannel.BlackHole, $"[Stoat] swing started {d:F0} u from the hole");
                }
                if (_swinging)
                {
                    bool done;
                    float want = _swingWant;
                    if (_status.AutoPilotEnabled)
                    {
                        // The AI's stick picks the angle toward its target: it is let go once it faces it.
                        var ai = _status.AIPilot;
                        var toTarget = ai ? ai.TargetPosition - hull.position : hull.forward;
                        float off = Vector3.Angle(hull.forward, toTarget);
                        want = config.SwingMaxDegrees;
                        done = StoatSlingMath.SwingAiLetsGo(_swingTurned, off, config.SwingMinDegrees, want, AIConfig.AiSwingFaceDegrees,
                            StoatSlingMath.OnSwingSide(hull.forward, toTarget, _swingAxis));
                    }
                    else done = _swingTurned >= want;
                    // Swung far out past the hole's reach (a slow turn on a wide pass): let go where it points.
                    if (done || d > 2f * _swingReach) { Fling(transformer, hull); return; }
                    float step = Mathf.Min(StoatSlingMath.SwingRate(FlightSpeed(), d, config.SwingDegreesPerSecond) * dt, want - _swingTurned);
                    var heading = Quaternion.AngleAxis(step, _swingAxis) * hull.forward;
                    Quaternion.FromToRotation(hull.forward, heading).ToAngleAxis(out float degrees, out var turnAxis);
                    if (degrees > 1e-4f && turnAxis.sqrMagnitude > 1e-8f) transformer.ApplyRotation(degrees, turnAxis);
                    transformer.SetCourseVelocity(heading);
                    _swingTurned += step;
                    return;
                }
            }
            if (!_swinging && ((Vector3.Dot(hull.forward, toHole) < 0f && d > _swingReach) || Time.time - _swingAt >= config.SwingSeconds))
                EndSwing(flung: false);
        }

        /// <summary>
        /// Let go by the swing (the simulating machine), on the heading it turned you onto. ONE slingshot (2026-11-19): the
        /// swing's own speed-up (the warp from its start) is kept as momentum; a second push - the hold's squeeze × Mass's power
        /// - is paid only as <see cref="StoatSlingConfigSO.SwingKickShare"/> of it (0 ships).
        /// </summary>
        void Fling(VesselTransformer transformer, Transform hull)
        {
            _swinging = false;
            float kick = 0f;
            if (config.SwingKickShare > 0f)
            {
                float power = config.LaunchPower.EvaluateLive(_status);
                float squeeze = StoatSlingMath.SlingBoost(EngineSpeed(), _holds[(int)_swingSide].Peak, config.SlingBoostMin, config.SlingBoostMax);
                kick = config.SwingKickShare * StoatSlingMath.CappedKick(squeeze * power, _status.Speed, config.SpeedCapShare);
                kick = StoatSlingMath.WarpCappedKick(kick, transformer.FlightTimeScale, EngineSpeed(), config.WarpKickCapCruise);
            }
            _momentumClock = Mathf.Max(1f, transformer.FlightTimeScale);
            _momentumAt = Time.time;
            _lastLaunchAt = Time.time;
            _lastTiming = 1f;
            // The modifier starts at 1.5× its amount and fades over the duration (VesselTransformer); the kick raises the
            // displacement cap for its own life only (vessel §4.ab2).
            if (kick > 0f) transformer.ModifyVelocity(hull.forward * (kick / 1.5f), config.SlingBoostSeconds, false, kick * 1.05f);
            _swingFxAt = Time.time;
            _swingFxCentre = _swingHolePos;
            _swingFxNormal = _swingAxis;
            PlayOneShot(config.SwingFlingEvent);
            CSDebug.LogVerbose(CSLogChannel.BlackHole, $"[Stoat] flung after {_swingTurned:F0}° of swing: kick {kick:F0}");
            EndSwing(flung: true);
        }

        /// <summary>The swing hole collapses (every peer, its own copy): the black hole goes and the Charge ripple is thrown from
        /// it. A hull that came within its reach lays its trail shielded for the kick's length (Mass 5).</summary>
        void EndSwing(bool flung)
        {
            if (!_swingUp) return;
            _swingUp = false;
            _swinging = false;
            if (_swingHole && !_swingHole.IsDespawning) _swingHole.BeginDespawn(0.25f);
            if (_swingHole && _swingHole.PrismCapture == BlackHole.PrismCaptureRule.Steal) _swingHole.PrismCapture = BlackHole.PrismCaptureRule.Default;
            _swingHole = null;
            if (flung || _swingWasNear) _launchShieldUntil = Time.time + config.SlingBoostSeconds;
            Detonate(_swingHolePos, config.RippleSeparation);
            CSDebug.LogVerbose(CSLogChannel.BlackHole, $"[Stoat] swing hole collapsed ({(flung ? "flung" : _swingWasNear ? "passed close" : "untouched")})");
        }

        // ------------------------------------------------------------------ frame

        /// <summary>The person flying this hull on this machine: no AI seat, no autopilot, the local owner. Only they get the
        /// haptics (and, in <see cref="StoatPathfinderExecutor"/>, the drawn reach ring and exit line).</summary>
        bool IsLocalHumanPilot => _status != null && _status.Player != null && _status.IsLocalUser &&
                                  !_status.IsInitializedAsAI && !_status.AutoPilotEnabled;

        /// <summary>The stick this frame, each axis clamped to a full deflection.</summary>
        void Stick(out float x, out float y)
        {
            var input = _status.InputStatus;
            x = input != null ? Mathf.Clamp(input.XSum, -1f, 1f) : 0f;
            y = input != null ? Mathf.Clamp(input.YSum, -1f, 1f) : 0f;
        }

        void Update()
        {
            if (config == null || _status == null) return;
            var input = _status.InputStatus;
            bool analog = input != null && input.ActiveInputDevice == InputDeviceType.Gamepad;
            bool autopilot = _status.AutoPilotEnabled;
            Track(Side.Left, input != null ? input.LeftTriggerAnalog : 0f, analog, autopilot);
            Track(Side.Right, input != null ? input.RightTriggerAnalog : 0f, analog, autopilot);
            var prisms = _status.VesselPrismController;
            if (prisms) prisms.ForceShielded = Time.time < _launchShieldUntil && config.ShieldsLaunchTrail(_status);

            StepAccretion();
            if (_swingUp) StepSwing(Time.deltaTime);

            ResolveSiblings();
            if (autopilot && MantaStingActionExecutor.IsSimAuthority(_status) && (!_mode || _mode.UsesOrbit)) AutopilotSwing();
        }

        /// <summary>
        /// The swing an autopilot cannot press (the arcade rule: never assume an AI can use a human's input). It presses toward
        /// a turn it wants (target at least <c>aiSlingMinTurnDegrees</c> off the nose, far enough away, every
        /// <c>aiSlingIntervalSeconds</c>; or for the warp on a long leg, <c>aiWarpMinDistance</c>) and lets go at once - the
        /// hole stays up. The hole sits on the turn's side, so steering toward its target it passes beside it and is swung; it
        /// is let go once it faces its target (<see cref="StepSwing"/>). Both edges through the REPLICATED path, so every peer
        /// runs the same swing a human's would. Simulating machine only.
        /// </summary>
        void AutopilotSwing()
        {
            var handler = _status.ActionHandler;
            var ai = _status.AIPilot;
            var hull = _status.Transform;
            if (!handler || !ai || !hull) return;
            for (int i = 0; i < 2; i++)
                if (_holds[i].Holding && !_swinging && ResolveBoundInput((Side)i, handler, out var held))
                    handler.StopShipControllerActionsReplicated(held);
            if (_swingUp || AIConfig.AiSlingMinTurnDegrees <= 0f || Time.time - _aiLastSlingTime < AIConfig.AiSlingIntervalSeconds) return;
            // Bearing from the steering point, range from the objective: on a race line the steering point sits one look-ahead
            // away, inside AiSlingMinDistance at cruise (StoatDipoleMath.AutopilotGauge).
            var local = StoatDipoleMath.AutopilotGauge(hull.InverseTransformPoint(ai.TargetPosition),
                                                       (ai.ObjectivePosition - hull.position).magnitude);
            if (!StoatSlingMath.AutopilotPress(local, AIConfig.AiSlingMinTurnDegrees, AIConfig.AiSlingMinDistance,
                                               AIConfig.AiWarpMinDistance, out bool turnLeft)) return;
            var side = turnLeft ? Side.Left : Side.Right;
            if (!ResolveBoundInput(side, handler, out var ie)) return;
            _aiLastSlingTime = Time.time;
            handler.PerformShipControllerActionsReplicated(ie);   // the black hole is laid ahead on the turn's side
            handler.StopShipControllerActionsReplicated(ie);      // and stays up
        }

        /// <summary>Which input event this side's sling is bound to on THIS vessel — read off the
        /// binding maps and retried until it succeeds (they fill after executors initialize,
        /// vessel contract rule 6).</summary>
        bool ResolveBoundInput(Side side, R_VesselActionHandler handler, out InputEvents ie)
        {
            int i = (int)side;
            if (_boundInput[i].HasValue) { ie = _boundInput[i].Value; return true; }
            foreach (InputEvents candidate in System.Enum.GetValues(typeof(InputEvents)))
            {
                _bindScratch.Clear();
                handler.CollectBoundActions(candidate, _bindScratch);
                foreach (var action in _bindScratch)
                    if (action is StoatSlingActionSO sling && sling.Side == side)
                    {
                        _boundInput[i] = candidate;
                        _bindScratch.Clear();
                        ie = candidate;
                        return true;
                    }
            }
            _bindScratch.Clear();
            ie = default;
            return false;
        }

        void Track(Side side, float rawAnalog, bool analog, bool autopilot)
        {
            int i = (int)side;
            if (!_holds[i].Holding) return;
            _holds[i].HeldFor += Time.deltaTime;
            float sample = StoatSlingMath.Hold01(rawAnalog, _holds[i].HeldFor, analog, config.HoldRampSeconds, autopilot, AIConfig.AutopilotHold01);
            _holds[i].Peak = StoatSlingMath.Peak(_holds[i].Peak, sample);
        }

        /// <summary>The transform <see cref="BlackHoleVesselPull"/> knows this vessel by (its
        /// <c>VesselStatus</c>'s).</summary>
        Transform OwnerTransform() => _status is Component c ? c.transform : _status.Transform;

        void PlayOneShot(EventReference reference)
        {
            if (reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, transform.position);
        }

        // A vessel swap or despawn drops the squeeze and collapses the hole.
        void OnDisable()
        {
            if (_swingUp)
            {
                _swingUp = _swinging = false;
                if (_swingHole && !_swingHole.IsDespawning) _swingHole.BeginDespawn(0.25f);
                _swingHole = null;
            }
            _holds[0] = default;
            _holds[1] = default;
            _boundInput[0] = null;
            _boundInput[1] = null;
            _launchShieldUntil = float.NegativeInfinity;
            _momentumAt = float.NegativeInfinity;
            var prisms = _status?.VesselPrismController;
            if (prisms) prisms.ForceShielded = false;
        }
    }
}
