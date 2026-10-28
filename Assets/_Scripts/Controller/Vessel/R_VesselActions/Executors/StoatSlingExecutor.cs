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
    /// The Stoat's SLINGSHOT (<c>R_VesselActions/STOAT.md</c>): an attractor–repulsor WORMHOLE pair
    /// laid beside the hull — the attractor on the trigger's side, the repulsor mirrored on the other.
    ///
    /// <para><b>TIME MODE IS THE SKIM SLING (2026-10-29, <c>STOAT_DIPOLE.md</c> §1q)</b> whenever
    /// <see cref="StoatSlingConfigSO.Skim"/> is set (it ships set). PRESS lays a pair <see cref="StoatSkimConfigSO.LayAhead"/>
    /// ahead and <see cref="StoatSkimConfigSO.LaySide"/> to the pressed side, its axis the hull's up. Fly past and the
    /// TIDEPOOL captures you onto the SKIMSHELL (the sphere of radius R round the midpoint): the hull is pinned to its
    /// surface and rides it, a steep entry PLUNKING (keeping less speed, <see cref="StoatSkimMath.EntryCarry"/>). The squeeze
    /// SPINS the shell (the trigger's side sets the way): riding with the spin speeds you up and draws your track onto the
    /// SKIMLINE. RELEASE launches you along the surface at min(cap, v_ride × (1 + G × timing)) and the spin HOOKS the launch
    /// for a moment. A capture soon after a launch is the next SKIP of a run (each pays a little less). The catch below is
    /// the fallback with no skim config. All of it is flown on the simulating machine for THIS hull only: an owned pair
    /// moves no other vessel (rival riding is opt-in and not wired yet, ELEMENTAL_ECONOMY §9b).</para>
    ///
    /// <para><b>TIME MODE — THE CATCH (2026-10-14, <c>STOAT_DIPOLE.md</c> §1b).</b> The triggers fly this only while
    /// the hull is in Time mode (<see cref="StoatModeExecutor"/>; Mass mode lays the field dipole). It is the studio's
    /// dipole sling (<c>StoatFlightStudio.html</c>, the <c>dp*</c> rows): PRESS lays a LIGHT attractor
    /// <see cref="StoatSlingConfigSO.CatchAhead"/> ahead and <see cref="StoatSlingConfigSO.CatchSide"/> to the pressed side,
    /// its repulsor mirrored — out in front, where the camera sees it — and the pathfinder draws two rings round it in
    /// place of the path: the ORBIT radius (the squeeze sets it live, harder = tighter) and the DETECTION radius
    /// (<see cref="StoatSlingConfigSO.CatchReach"/> × the orbit). Fly past it and it CATCHES you
    /// (<see cref="StoatSlingMath.Catches"/>): your nose eases onto the orbit's tangent and you go round for as long as you
    /// hold, up to <see cref="StoatSlingConfigSO.MaxOrbitDegrees"/>. RELEASE and you slingshot out along the tangent, the
    /// kick TIMED by the angle swept since the catch — all of it a quarter-turn round
    /// (<see cref="StoatSlingConfigSO.SweetSpotDegrees"/> ± <see cref="StoatSlingConfigSO.SweetSpotHalfWidth"/>),
    /// <see cref="StoatSlingConfigSO.MissKickShare"/> of it outside, none before a catch — × Time's rate
    /// (<see cref="StoatDipoleConfigSO.BoostRatio"/>). It can never suck you in: it catches you at the orbit radius, many
    /// horizons out, and an owned pair pulls no vessel (<see cref="BlackHoleVesselPull"/>). Released, the pair falls
    /// together and annihilates, and the meeting throws the Charge ripple (<see cref="StoatRippleExecutor"/>).</para>
    ///
    /// <para><b>Why it changed.</b> The 2026-10-13 cut laid the attractor square beside the hull at the orbit radius and
    /// started the orbit at once: the pole sat ~80° off the chase camera's axis, so in play nothing was seen and the hull
    /// was "just pulled right or left" — and its strength was the circular-orbit GM, several times Mass mode's poles.</para>
    ///
    /// <para><b>Where it runs.</b> The press and release replicate, so every peer lays its own copy of the pair from the
    /// replicated press and lets it go on the release. The catch and the orbit are flown on the simulating machine only;
    /// the squeeze is not replicated.</para>
    ///
    /// <para><b>The crystal style</b> (<c>BlackHoleConfig.crystalPairs</c>, §13) is charming-cerf's
    /// wormhole and keeps its first design: laid on RELEASE, sized by the deepest squeeze
    /// (<see cref="StoatSlingMath.Peak"/> — a let-go trigger sweeps back down before the release edge).</para>
    ///
    /// <para><b>Naming.</b> Player-facing these are WORMHOLES (attractor / repulsor). In code they are
    /// the black-hole system's types (<c>BlackHole</c> with <c>HolePolarity.Sink</c> / <c>Source</c>,
    /// the names <c>cece/charming-cerf-alf1j1</c> chose).</para>
    ///
    /// <para><b>One pair per Stoat.</b> A new press ends this hull's last pair (and slings out of an orbit still
    /// running, so chaining triggers chains turns); other hulls' and the tool's pairs are left alone.</para>
    /// </summary>
    public sealed class StoatSlingExecutor : ShipActionExecutorBase
    {
        public enum Side { Left = 0, Right = 1 }

        [Header("Config")]
        [Tooltip("Every number of the sling: squeeze → orbit, slingshot, life, the crystal style, audio slots.")]
        [SerializeField] StoatSlingConfigSO config;

        // The autopilot's numbers: the mode's own copy when it has one (AIModeTuning, e.g. StoatSlingConfig_Regatta learned
        // from people's editor flights), resolved once the pilot's mode is known; this config otherwise. Only the AI's
        // reads go through it - a person flying this hull reads `config`.
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
            public float Live;      // this frame's squeeze — the orbit radius follows it
            public float Peak;      // the deepest squeeze of the hold — the boost and the crystal size
        }

        readonly Hold[] _holds = new Hold[2];
        IVesselStatus _status;
        BlackHoleRegistry.Pair _pair;
        CrystalWormhole _crystal;
        ToggleTranslationModeActionExecutor _stance;
        readonly InputEvents?[] _boundInput = new InputEvents?[2];
        readonly List<ShipActionSO> _bindScratch = new();
        float _aiLastSlingTime = float.NegativeInfinity;

        StoatModeExecutor _mode;
        StoatRippleExecutor _ripple;
        StoatDipoleExecutor _dipole;
        bool _resolvedSiblings;
        float _lastTiming = -1f;

        // Gate mode: the gate (a held pair stretched along the nose), whether it has been flown through, the jump's warp.
        BlackHoleRegistry.Pair _gate;
        bool _gateUsed;
        Vector3 _gateSink;
        Vector3 _gateAxis;
        float _gateQuality;
        float _gateBoostUntil = float.NegativeInfinity;
        int _transitsSeen;
        float _aiGateSqueeze;

        // The Time-mode pair under the trigger, and the orbit once it has caught the hull.
        bool _laid;          // a pair is laid and held by a trigger (every peer)
        bool _caught;        // ...and has caught the hull (the simulating machine)
        Side _orbitSide;
        float _orbitRadius;
        float _orbitAngle;   // radians swept round the attractor since the catch
        Vector3 _lastRadial;
        float _laidAt;

        // The Skim Sling (Time mode with config.Skim set): the shell, the ride, the hook, the skip run.
        Vector3 _skimCentre;
        Vector3 _skimAxis;          // the spin axis: hull up for the right trigger, down for the left
        float _carry = 1f;          // the entry's kept share (the plunk)
        float _spinRide;            // the spin's speed along the ride, eased (u/s, negative against the spin)
        float _spinSpeed;           // the shell's surface speed this frame (u/s)
        float _rideSpeed;           // the ride's speed this frame (u/s)
        float _spinPhase;           // degrees the Skimline has turned (the dots scroll with it)
        float _hookStart = float.NegativeInfinity;
        float _hookRate;            // rad/s at launch
        Vector3 _hookAxis;
        float _lastLaunchAt = float.NegativeInfinity;
        int _skipIndex;

        /// <summary>The Time-mode pair this hull laid last, alive or not; null before the first one.</summary>
        public BlackHoleRegistry.Pair LastPair => _pair;
        /// <summary>The crystal pair this hull slung last (the crystal style, Docs/BLACK_HOLE.md §13); null before the first.</summary>
        public CrystalWormhole LastCrystal => _crystal;
        public bool IsHolding(Side side) => _holds[(int)side].Holding;
        /// <summary>The deepest squeeze of the current (or last) hold, 0..1.</summary>
        public float Hold01(Side side) => _holds[(int)side].Peak;
        /// <summary>True while a Time-mode pair is laid and held (caught or not yet).</summary>
        public bool HasPair => _laid && _pair != null && _pair.Black != null;
        /// <summary>True while the attractor has caught the hull and it is circling (the simulating machine).</summary>
        public bool IsOrbiting => _laid && _caught;
        /// <summary>The orbit radius the squeeze asks for right now, world units (0 with no pair) — the solid ring.</summary>
        public float OrbitRadius => _laid ? _orbitRadius : 0f;
        /// <summary>How far out the attractor can catch the hull, world units (0 with no pair) — the faint ring.</summary>
        public float DetectionRadius => !_laid || !config ? 0f
            : UsesSkim ? StoatSkimMath.TidepoolRadius(_orbitRadius, config.Skim.TidepoolScale) : _orbitRadius * config.CatchReach;
        /// <summary>Radians swept round the attractor since the catch.</summary>
        public float OrbitAngle => _orbitAngle;
        /// <summary>True while either trigger holds this executor's squeeze.</summary>
        public bool IsHoldingAny => _holds[0].Holding || _holds[1].Holding;
        /// <summary>The laid pair's attractor (caught or not), or null.</summary>
        public BlackHole Attractor => HasPair ? _pair.Black : null;
        /// <summary>How well a release RIGHT NOW would be timed, 0..1 (0 before a catch) — the rings and the HUD.</summary>
        public float Timing01 => !IsOrbiting || !config ? 0f
            : UsesSkim ? config.Skim.SweetSpot(_orbitAngle * Mathf.Rad2Deg) : config.SweetSpot(_orbitAngle * Mathf.Rad2Deg);
        /// <summary>How far round the current orbit is toward the sweet spot, 0..1 (the HUD's Time card).</summary>
        public float SweetProgress01 => !IsOrbiting || !config ? 0f
            : Mathf.Clamp01(_orbitAngle * Mathf.Rad2Deg / (UsesSkim ? config.Skim.SweetSpotDegrees : config.SweetSpotDegrees));

        /// <summary>Time mode flies the Skim Sling: a skim config is set and the crystal style is off.</summary>
        public bool UsesSkim => config && config.Skim && !(BlackHoleRegistry.Config && BlackHoleRegistry.Config.CrystalPairs);
        /// <summary>The laid Skimshell's centre (the pair's midpoint, held still).</summary>
        public Vector3 SkimCentre => _skimCentre;
        /// <summary>The laid Skimshell's spin axis (the Skimline's normal; the dots scroll round it).</summary>
        public Vector3 SkimAxis => _skimAxis;
        /// <summary>Degrees the Skimline has turned with the spin (for the scrolling dots).</summary>
        public float SpinPhaseDegrees => _spinPhase;
        /// <summary>The shell's surface speed now as a share of its maximum (0 = still).</summary>
        public float Spin01 => UsesSkim && config.Skim.SpinSpeedMax > 0f ? Mathf.Clamp01(_spinSpeed / config.Skim.SpinSpeedMax) : 0f;
        /// <summary>The share of speed the last capture kept (1 = flat, lower = a plunk).</summary>
        public float LastCarry => _carry;
        /// <summary>This ride's place in a skip run: 0 = the first, 1 = the first skip, ...</summary>
        public int SkipIndex => _skipIndex;
        /// <summary>The ride's speed (u/s) while skimming.</summary>
        public float RideSpeed => IsOrbiting ? _rideSpeed : 0f;
        /// <summary>The last slingshot's timing, 0..1, or −1 before the first.</summary>
        public float LastTiming => _lastTiming;
        /// <summary>The squeeze on <paramref name="side"/> right now, 0..1 (0 when that trigger is not held).</summary>
        public float Squeeze01(Side side) => _holds[(int)side].Holding ? _holds[(int)side].Live : 0f;
        /// <summary>Gate mode: the gate this hull opened last, alive or not; null before the first.</summary>
        public BlackHoleRegistry.Pair Gate => _gate;
        /// <summary>Gate mode: true while a gate is open and not yet flown through.</summary>
        public bool GateOpen => _gate != null && _gate.IsAlive && !_gateUsed;
        /// <summary>Gate mode: how good the last jump was, 0..1 (<see cref="StoatGateMath.Quality"/>).</summary>
        public float GateQuality => _gateQuality;
        /// <summary>Gate mode: true while the last jump's warp holds (the pathfinder, the one writer of the clock, reads it).</summary>
        public bool GateWarps => Time.time < _gateBoostUntil;
        public StoatSlingConfigSO Config => config;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _aiConfig = null;
            _holds[0] = default;
            _holds[1] = default;
            _laid = false;
            _caught = false;
            _hookRate = 0f;
            _skipIndex = 0;
            _lastLaunchAt = float.NegativeInfinity;
            _resolvedSiblings = false;
            _lastTiming = -1f;
            _gate = null;
            _gateUsed = false;
            _gateBoostUntil = float.NegativeInfinity;
            _stance = TryGetComponent<ActionExecutorRegistry>(out var registry) ? registry.Get<ToggleTranslationModeActionExecutor>() : null;
            if (config == null)
                CSDebug.LogError($"[Stoat] {name}: StoatSlingExecutor has no StoatSlingConfig — the triggers will sling nothing. " +
                                 "Wire Assets/_SO_Assets/VesselActions/Stoat/StoatSlingConfig.asset on the prefab.");
        }

        /// <summary>Trigger PRESS: the Time-mode pair is laid ahead now (it catches the hull as it passes); a crystal pair
        /// waits for the release.</summary>
        public void BeginHold(Side side)
        {
            // The triggers fly the orbit in TIME mode; in Mass mode they lay the field dipole (StoatDipoleExecutor); in GATE
            // mode a press opens the warp gate (one at a time) and the squeeze stretches it.
            ResolveSiblings();
            bool gate = _mode && _mode.IsGateMode;
            if (_mode && !_mode.UsesOrbit && !gate) return;
            int i = (int)side;
            if (_holds[i].Holding) return;
            _holds[i].Holding = true;
            _holds[i].HeldFor = 0f;
            // An autopilot writes no trigger, so its fixed squeeze is the hold from the start.
            float start = config != null && _status != null && _status.AutoPilotEnabled ? (gate ? _aiGateSqueeze : AIConfig.AutopilotHold01) : 0f;
            _holds[i].Live = start;
            _holds[i].Peak = start;
            if (config == null) return;
            if (gate) { LayGate(side); return; }
            PlayOneShot(config.HoldStartEvent);
            if (!BlackHoleRegistry.Config.CrystalPairs) LayPair(side);
        }

        /// <summary>Trigger RELEASE: let the pair go — slingshot out of the orbit if it caught you — or lay the crystal pair.</summary>
        public void Release(Side side)
        {
            int i = (int)side;
            if (!_holds[i].Holding) return;
            _holds[i].Holding = false;
            if (config == null || _status == null) return;
            if (_mode && _mode.IsGateMode) { if (!IsHoldingAny) LetGoGate(); return; }
            if (_laid)
            {
                if (_orbitSide == side) Slingshot(boost: true);
                return;
            }
            if (BlackHoleRegistry.Config.CrystalPairs) SlingCrystal(side, _holds[i].Peak);
        }

        /// <summary>
        /// A mode switch: both triggers let go here. A pair is let go WITHOUT its kick (the switch is not a release) and
        /// closes and annihilates as any let-go pair does. Every peer runs it (the switch is replicated).
        /// </summary>
        public void ReleaseAll()
        {
            if (_laid) Slingshot(boost: false);
            LetGoGate();
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
            _dipole = registry.Get<StoatDipoleExecutor>();
        }

        // ------------------------------------------------------------------ Time mode: the catch and the orbit

        /// <summary>The speed the hull is actually flying: the orbit's turn ω = v / r comes from it, so the circle drawn is
        /// the circle flown (a floored speed would turn a slow hull on a smaller circle than the radius and leave the
        /// radial correction fighting it).</summary>
        float OrbitSpeed() => Mathf.Max(_status.Speed, 1f);

        /// <summary>The engine's own speed: the published speed with the flight-clock warp taken out. The kick is a share of
        /// it, as in the studio (<c>speed + flow</c>), so the warp paces the flight without compounding into the kick.</summary>
        float EngineSpeed()
        {
            var transformer = _status.VesselTransformer;
            float clock = transformer ? Mathf.Max(1f, transformer.FlightTimeScale) : 1f;
            return Mathf.Max(_status.Speed / clock, 1f);
        }

        /// <summary>
        /// True while the Time-mode pair is held and warps the hull's flight clock (the studio's catch warp, dpWarp): from
        /// the press (<see cref="StoatSlingConfigSO.CatchWarpFromPress"/>) or once caught. The pathfinder, the one writer of
        /// the clock, reads it.
        /// </summary>
        public bool WarpsFlight => !UsesSkim && _laid && _pair != null && _pair.IsAlive && _pair.Held && config &&
                                   config.CatchWarpShare > 0f && (_caught || config.CatchWarpFromPress);

        float RadiusFor(float hold01)
        {
            // Space is reach: read live, never cached (vessel contract rule 2).
            float reach = config.OrbitReach.EvaluateLive(_status);
            float radius = StoatSlingMath.OrbitRadius(hold01, config.OrbitRadiusWide * reach, config.OrbitRadiusTight * reach, config.HoldExponent);
            return StoatSlingMath.ClearOfRepulsor(radius, 2f * config.CatchSide, config.WhiteClear);
        }

        /// <summary>
        /// The press: a LIGHT pair laid AHEAD — the attractor to the pressed side, the repulsor mirrored — held, not
        /// moving. Every peer lays its own copy from the replicated press.
        /// </summary>
        void LayPair(Side side)
        {
            if (UsesSkim) { LaySkim(side); return; }
            var hull = _status.Transform;
            if (hull == null) return;
            // One pair per Stoat: a press while circling slings out of the old orbit first.
            if (_laid) Slingshot(boost: true);
            EndLastPair();
            var bh = BlackHoleRegistry.Config;
            if (!BlackHoleRegistry.CanSpawn(2))
            {
                CSDebug.LogVerbose(CSLogChannel.BlackHole,
                    $"[Stoat] Time-mode pair refused — needs two free of {bh.MaxBlackHoles} wormholes ({BlackHoleRegistry.Count} live).");
                return;
            }

            bool left = side == Side.Left;
            StoatSlingMath.CatchPair(hull.position, hull.forward, hull.right, left, config.CatchAhead, config.CatchSide,
                out var attractor, out var repulsor);
            // The pair's axis runs attractor → repulsor (BlackHolePairMath puts the attractor at −axis).
            float strength = config.PoleGM / Mathf.Max(1e-3f, bh.GmPerStrength);
            _pair = BlackHoleRegistry.SpawnPair(0.5f * (attractor + repulsor), StoatSlingMath.PairAxis(left, hull.right), strength,
                config.PoleHorizon, config.CatchSide, config.DriftSpeed, config.Lifetime, hull.up, OwnerTransform(), held: true);
            if (_pair == null) return;

            _laid = true;
            _caught = false;
            _orbitSide = side;
            _orbitRadius = RadiusFor(_holds[(int)side].Live);
            _orbitAngle = 0f;
            _laidAt = Time.time;
            // A sling is a launch: leave the hold-still stance, which would otherwise pin the hull.
            if (_status.IsTranslationRestricted && _stance) _stance.EndStance();
            CSDebug.LogVerbose(CSLogChannel.BlackHole,
                $"[Stoat] {side} Time-mode pair laid {config.CatchAhead:F0} u ahead, orbit {_orbitRadius:F0} u, " +
                $"detection {_orbitRadius * config.CatchReach:F0} u");
        }

        /// <summary>
        /// The simulating machine's frame with a pair laid: the orbit radius follows the squeeze; until the attractor
        /// catches the hull nothing else happens (an owned pair pulls no vessel); once caught the nose eases onto the
        /// orbit's tangent and the hull is held on the radius, and the angle swept round the attractor is counted — what
        /// the release's timing reads.
        /// </summary>
        void StepOrbit(float dt)
        {
            if (UsesSkim) { StepSkim(dt); return; }
            var hull = _status.Transform;
            var transformer = _status.VesselTransformer;
            if (!hull || !transformer || _pair.Black == null) return;

            // Caught and held where it was caught (holdAtCatch), the circle eases to the squeeze's slowly; otherwise it follows.
            float follow = _caught && config.HoldAtCatch ? config.OrbitEaseRate : config.RadiusFollowRate;
            _orbitRadius = Mathf.Lerp(_orbitRadius, RadiusFor(_holds[(int)_orbitSide].Live), 1f - Mathf.Exp(-follow * dt));
            var centre = _pair.Black.transform.position;

            if (!_caught)
            {
                if (_status.IsTranslationRestricted) return;
                if (!StoatSlingMath.Catches(hull.position, hull.forward, centre, _orbitRadius, _orbitRadius * config.CatchReach,
                        config.CatchDegrees))
                    return;
                _caught = true;
                _orbitAngle = 0f;
                _lastRadial = hull.position - centre;
                // Held where it was caught: no yank onto the squeeze's circle (the studio's dpHoldAtCatch).
                if (config.HoldAtCatch) _orbitRadius = _lastRadial.magnitude;
                CSDebug.LogVerbose(CSLogChannel.BlackHole,
                    $"[Stoat] caught at {_lastRadial.magnitude:F0} u (orbit {_orbitRadius:F0} u)");
            }

            // Onto the radius...
            var toCentre = centre - hull.position;
            float distance = toCentre.magnitude;
            if (distance > 1e-3f)
                transformer.TranslateShip(toCentre / distance *
                                          StoatSlingMath.RadialCorrection(distance, _orbitRadius, config.RadialCorrectionRate, dt));

            // ...and round it: the nose eases onto the tangent and turns at ω = v / r, the momentum with it.
            float step = OrbitSpeed() / Mathf.Max(1f, _orbitRadius) * dt;
            var tangent = StoatSlingMath.OrbitHeading(hull.forward, toCentre, step);
            var desired = StoatSlingMath.SettleHeading(hull.forward, tangent, step * Mathf.Rad2Deg,
                1f - Mathf.Exp(-config.SettleRate * dt));
            Quaternion.FromToRotation(hull.forward, desired).ToAngleAxis(out float degrees, out var axis);
            if (degrees > 1e-4f && axis.sqrMagnitude > 1e-8f) transformer.ApplyRotation(degrees, axis);
            transformer.SetCourseVelocity(desired);

            // The angle really swept round the attractor (not ω·t: a catch off the radius sweeps slower at first).
            var radial = hull.position - centre;
            _orbitAngle += Vector3.Angle(_lastRadial, radial) * Mathf.Deg2Rad;
            _lastRadial = radial;
            if (_orbitAngle * Mathf.Rad2Deg >= config.MaxOrbitDegrees) LetGoReplicated();
        }

        /// <summary>The longest orbit is flown: let go through the replicated path, so every peer's copy is let go too.</summary>
        void LetGoReplicated()
        {
            var handler = _status.ActionHandler;
            if (handler && ResolveBoundInput(_orbitSide, handler, out var ie)) handler.StopShipControllerActionsReplicated(ie);
            else Release(_orbitSide);
        }

        /// <summary>
        /// Out of the pair: it is let go (it closes and annihilates) and, on a real release of a pair that CAUGHT the
        /// hull, the hull is thrown along its tangent — the kick timed by the angle swept since the catch.
        /// </summary>
        void Slingshot(bool boost)
        {
            if (!_laid) return;
            bool caught = _caught;
            _laid = false;
            _caught = false;
            BlackHoleRegistry.LetGo(_pair);
            var transformer = _status.VesselTransformer;
            var hull = _status.Transform;
            if (UsesSkim)
            {
                if (boost && caught && transformer && hull && MantaStingActionExecutor.IsSimAuthority(_status)) LaunchSkim(transformer, hull);
                CSDebug.LogVerbose(CSLogChannel.BlackHole,
                    $"[Stoat] {_orbitSide} Skimshell let go ({(caught ? $"after {_orbitAngle * Mathf.Rad2Deg:F0}° ridden" : "never captured")})");
                return;
            }
            if (boost && caught && transformer && hull && MantaStingActionExecutor.IsSimAuthority(_status))
            {
                // TIMED: the whole kick for a release a quarter-turn round (the sweet spot), the miss share outside it;
                // × Time's rate, the Stoat's one Time number (StoatDipoleConfigSO.BoostRatio).
                ResolveSiblings();
                float timing = config.SweetSpot(_orbitAngle * Mathf.Rad2Deg);
                float rate = _dipole && _dipole.Config ? _dipole.Config.BoostRatio(_status) : 1f;
                float squeeze = StoatSlingMath.SlingBoost(EngineSpeed(), _holds[(int)_orbitSide].Peak, config.SlingBoostMin, config.SlingBoostMax);
                float kick = StoatSlingMath.TimedKick(squeeze, timing, config.MissKickShare, rate);
                _lastTiming = timing;
                // The modifier starts at 1.5× its amount and fades over the duration (VesselTransformer). A timed kick can
                // pass the shared 100 u/s displacement ceiling, so it raises the cap for its own life (vessel §4.ab2) —
                // never the vessel's ceiling, which every knock-back shares.
                if (kick > 0f) transformer.ModifyVelocity(hull.forward * (kick / 1.5f), config.SlingBoostSeconds, false, kick * 1.05f);
                if (timing >= 0.999f) PlayOneShot(config.SweetSpotEvent);
                PlayOneShot(config.SlingEvent);
            }
            CSDebug.LogVerbose(CSLogChannel.BlackHole,
                $"[Stoat] {_orbitSide} Time-mode pair let go ({(caught ? $"after {_orbitAngle * Mathf.Rad2Deg:F0}°" : "never caught")})");
        }

        void EndLastPair()
        {
            if (_pair != null && _pair.IsAlive)
            {
                // A fresh press over a live pair annihilates it where it stands: that is an annihilation too, and throws
                // the Charge ripple from its middle (StoatRippleExecutor).
                var middle = 0.5f * (_pair.Black.transform.position + _pair.White.transform.position);
                float separation = Vector3.Distance(_pair.Black.transform.position, _pair.White.transform.position);
                BlackHoleRegistry.Annihilate(_pair);
                Detonate(middle, separation);
            }
            if (_crystal && !_crystal.IsGone && !_crystal.IsAnnihilating) _crystal.Annihilate(BlackHoleRegistry.Config.CrystalReplaceSeconds);
        }

        /// <summary>This hull's let-go pair met and annihilated (every peer, its own copy): the Charge ripple, its reach
        /// the separation the pair was laid at.</summary>
        void OnPairMet(BlackHoleRegistry.Pair pair, Vector3 at)
        {
            if (pair == null || (pair != _pair && pair != _gate)) return;
            Detonate(at, 2f * pair.HalfGap0);
        }

        void Detonate(Vector3 at, float separation)
        {
            ResolveSiblings();
            if (_ripple) _ripple.Detonate(at, separation);
        }

        void OnEnable() => BlackHoleRegistry.PairMet += OnPairMet;

        // ------------------------------------------------------------------ Time mode: the Skim Sling (STOAT_DIPOLE.md §1q)

        float SkimRadiusFor(StoatSkimConfigSO skim) => skim.SkimRadius * skim.SkimReach.EvaluateLive(_status);   // Space is reach, read live

        /// <summary>
        /// The press: the Skimshell's pair laid AHEAD and to the pressed side, its axis the hull's up (the poles above and
        /// below the Skimline), spun the trigger's way. Held, still. Every peer lays its own copy from the replicated press.
        /// </summary>
        void LaySkim(Side side)
        {
            var skim = config.Skim;
            var hull = _status.Transform;
            if (hull == null) return;
            if (_laid) Slingshot(boost: true);
            EndLastPair();
            var bh = BlackHoleRegistry.Config;
            if (!BlackHoleRegistry.CanSpawn(2))
            {
                CSDebug.LogVerbose(CSLogChannel.BlackHole,
                    $"[Stoat] Skimshell refused — needs two free of {bh.MaxBlackHoles} wormholes ({BlackHoleRegistry.Count} live).");
                return;
            }
            bool left = side == Side.Left;
            float radius = SkimRadiusFor(skim);
            var centre = StoatSkimMath.Midpoint(hull.position, hull.forward, hull.right, left, skim.LayAhead, skim.LaySide);
            var spin = StoatSkimMath.SpinAxis(left, hull.up);
            float strength = skim.PoleGM / Mathf.Max(1e-3f, bh.GmPerStrength);
            _pair = BlackHoleRegistry.SpawnPair(centre, hull.up, strength, skim.PoleHorizon,
                StoatSkimMath.PoleHalfGap(radius, skim.PoleHalfGap), skim.DriftSpeed, skim.HoldMax, spin, OwnerTransform(), held: true);
            if (_pair == null) return;

            _laid = true;
            _caught = false;
            _orbitSide = side;
            _orbitRadius = radius;
            _orbitAngle = 0f;
            _laidAt = Time.time;
            _skimCentre = centre;
            _skimAxis = spin;
            _spinRide = 0f;
            _carry = 1f;
            if (_status.IsTranslationRestricted && _stance) _stance.EndStance();
            CSDebug.LogVerbose(CSLogChannel.BlackHole,
                $"[Stoat] {side} Skimshell laid {skim.LayAhead:F0} u ahead, R {radius:F0} u, Tidepool {radius * skim.TidepoolScale:F0} u");
        }

        /// <summary>
        /// The simulating machine's frame with a Skimshell laid: the squeeze spins it; the Tidepool captures the hull AS it
        /// passes (the entry's angle sets the plunk); captured, the hull is pinned to R, rides the surface along a great
        /// circle drawn onto the Skimline, its speed eased toward the spin's, and the angle ridden is counted.
        /// </summary>
        void StepSkim(float dt)
        {
            var skim = config.Skim;
            var hull = _status.Transform;
            var transformer = _status.VesselTransformer;
            if (!hull || !transformer) return;
            float radius = Mathf.Max(1f, _orbitRadius);
            var centre = _skimCentre;
            _spinSpeed = StoatSkimMath.SpinSpeed(_holds[(int)_orbitSide].Live, skim.SpinSpeedMax, skim.SpinExponent);
            _spinPhase = Mathf.Repeat(_spinPhase + _spinSpeed / radius * Mathf.Rad2Deg * dt, 360f);

            if (!_caught)
            {
                if (_status.IsTranslationRestricted) return;
                if (!StoatSkimMath.Captures(hull.position, hull.forward, centre, radius,
                        StoatSkimMath.TidepoolRadius(radius, skim.TidepoolScale), skim.CatchDegrees))
                    return;
                _caught = true;
                _orbitAngle = 0f;
                _lastRadial = hull.position - centre;
                float incidence = StoatSkimMath.IncidenceDegrees(hull.forward, _lastRadial);
                _carry = StoatSkimMath.EntryCarry(incidence, skim.PlunkCarry);
                _skipIndex = StoatSkimMath.SkipIndex(_skipIndex, Time.time - _lastLaunchAt, skim.SkipWindow, skim.MaxSkips);
                _spinRide = 0f;
                PlayOneShot(_carry < skim.PlunkCallout ? skim.PlunkEvent : skim.CaptureEvent);
                CSDebug.LogVerbose(CSLogChannel.BlackHole,
                    $"[Stoat] Tidepool capture at {_lastRadial.magnitude:F0} u, incidence {incidence:F0}°, carry {_carry:F2}, skip {_skipIndex}");
            }

            // Pinned to the Skimshell...
            var toCentre = centre - hull.position;
            float distance = toCentre.magnitude;
            if (distance > 1e-3f)
                transformer.TranslateShip(toCentre / distance * StoatSlingMath.RadialCorrection(distance, radius, skim.CaptureRate, dt));

            // ...the spin feeds the ride (with it gains, against it bleeds), the plunk keeps its share...
            var radial = hull.position - centre;
            var flow = StoatSkimMath.SpinFlow(_skimAxis, radial);
            _spinRide = StoatSkimMath.EaseSpin(_spinRide, StoatSkimMath.SpinTarget(_spinSpeed, hull.forward, flow), skim.SpinGrip, dt);
            float engine = EngineSpeed();
            float extra = Mathf.Max(-0.5f * engine, engine * (_carry - 1f) + _spinRide);
            _rideSpeed = engine + extra;

            // ...and round it on a great circle (ω = v / R), the track drawn onto the Skimline.
            float step = Mathf.Max(_rideSpeed, 1f) / radius * dt;
            var tangent = StoatSlingMath.OrbitHeading(hull.forward, toCentre, step);
            tangent = StoatSkimMath.Precess(tangent, flow, skim.Precess, dt);
            var desired = StoatSlingMath.SettleHeading(hull.forward, tangent, step * Mathf.Rad2Deg, 1f - Mathf.Exp(-skim.SettleRate * dt));
            Quaternion.FromToRotation(hull.forward, desired).ToAngleAxis(out float degrees, out var axis);
            if (degrees > 1e-4f && axis.sqrMagnitude > 1e-8f) transformer.ApplyRotation(degrees, axis);
            transformer.SetCourseVelocity(desired);
            if (Mathf.Abs(extra) > 1e-4f) transformer.TranslateShip(desired * (extra * dt));

            _orbitAngle += Vector3.Angle(_lastRadial, radial) * Mathf.Deg2Rad;
            _lastRadial = radial;
            if (_orbitAngle * Mathf.Rad2Deg >= skim.MaxRideDegrees) LetGoReplicated();
        }

        /// <summary>
        /// The release of a captured ride: out along the surface at min(cap, v_ride × (1 + G × timing × rate × decay^skip)) -
        /// the kick is what that is over the engine - and the spin's HOOK starts.
        /// </summary>
        void LaunchSkim(VesselTransformer transformer, Transform hull)
        {
            var skim = config.Skim;
            ResolveSiblings();
            float timing = skim.SweetSpot(_orbitAngle * Mathf.Rad2Deg);
            float rate = _dipole && _dipole.Config ? _dipole.Config.BoostRatio(_status) : 1f;
            float gain = StoatSkimMath.LaunchGain(_holds[(int)_orbitSide].Peak, skim.LaunchGainMin, skim.LaunchGainMax);
            float engine = EngineSpeed();
            float ride = _rideSpeed > 0f ? _rideSpeed : engine;
            float cap = skim.CapCruises * Mathf.Max(engine, transformer.CruiseSpeed);
            float launch = StoatSkimMath.LaunchSpeed(ride, gain, timing, skim.MissShare, rate, cap, skim.SkipDecay, _skipIndex);
            float kick = StoatSkimMath.Kick(launch, engine);
            _lastTiming = timing;
            _lastLaunchAt = Time.time;
            if (kick > 0f) transformer.ModifyVelocity(hull.forward * (kick / 1.5f), skim.KickSeconds, false, kick * 1.05f);
            // The hook: the launch keeps turning the way the ride turned, at the spin's rate × curve, fading out.
            _hookRate = StoatSkimMath.HookRate(skim.Curve, _spinSpeed, _orbitRadius);
            _hookAxis = _skimAxis;
            _hookStart = Time.time;
            if (timing >= 0.999f) PlayOneShot(skim.SweetSpotEvent);
            PlayOneShot(skim.LaunchEvent);
            CSDebug.LogVerbose(CSLogChannel.BlackHole,
                $"[Stoat] Skip launch: ride {ride:F0} → {launch:F0} u/s (kick {kick:F0}, timing {timing:F2}, gain {gain:F2}, " +
                $"skip {_skipIndex}, hook {StoatSkimMath.HookTotalDegrees(_hookRate, skim.CurveSeconds):F0}°)");
        }

        /// <summary>The launch's hook, frame by frame: the nose and course turned about the spin axis, fading to nothing.</summary>
        void StepHook(float dt)
        {
            var skim = config.Skim;
            var hull = _status.Transform;
            var transformer = _status.VesselTransformer;
            float share = skim ? StoatSkimMath.HookDecay(Time.time - _hookStart, skim.CurveSeconds) : 0f;
            if (share <= 0f || !hull || !transformer || _laid) { _hookRate = 0f; return; }
            float degrees = _hookRate * share * dt * Mathf.Rad2Deg;
            if (degrees <= 1e-5f) return;
            transformer.ApplyRotation(degrees, _hookAxis);
            transformer.SetCourseVelocity(Quaternion.AngleAxis(degrees, _hookAxis) * hull.forward);
        }

        // ------------------------------------------------------------------ the crystal style

        void SlingCrystal(Side side, float hold01)
        {
            var hull = _status.Transform;
            if (hull == null) return;
            var bh = BlackHoleRegistry.Config;
            float strength = StoatSlingMath.Strength(hold01, config.MinStrength, config.MaxStrength, config.HoldExponent);
            float rs = bh.HorizonRadius(strength);
            float halfGap = rs * config.HalfGapHorizons.EvaluateLive(_status);
            var midpoint = StoatSlingMath.Midpoint(hull.position, hull.forward, rs, config.AheadHorizons);
            var axis = StoatSlingMath.PairAxis(side == Side.Left, hull.right);

            EndLastPair();
            if (!BlackHoleRegistry.CanSpawn(2))
            {
                CSDebug.LogVerbose(CSLogChannel.BlackHole,
                    $"[Stoat] sling refused — needs two free of {bh.MaxBlackHoles} wormholes ({BlackHoleRegistry.Count} live).");
                return;
            }
            // charming-cerf's wormhole: its mouths carry this pilot alone, its wells pull this hull alone.
            var riders = new List<IPlayer>(1);
            if (_status.Player != null) riders.Add(_status.Player);
            _crystal = BlackHoleRegistry.SpawnCrystalPair(midpoint, axis, halfGap,
                BlackHoleRegistry.CrystalSettings(strength, 0f, hull.up, riders), OwnerTransform());
            if (_crystal == null) return;
            if (_status.IsTranslationRestricted && _stance) _stance.EndStance();
            PlayOneShot(config.SlingEvent);
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (config == null || _status == null) return;
            var input = _status.InputStatus;
            bool analog = input != null && input.ActiveInputDevice == InputDeviceType.Gamepad;
            bool autopilot = _status.AutoPilotEnabled;
            Track(Side.Left, input != null ? input.LeftTriggerAnalog : 0f, analog, autopilot);
            Track(Side.Right, input != null ? input.RightTriggerAnalog : 0f, analog, autopilot);

            if (_laid)
            {
                // The pair ended under us (annihilated, or held past its lifetime and let go on its own).
                if (_pair == null || !_pair.IsAlive) _laid = _caught = false;
                else if (!_pair.Held) Slingshot(boost: true);
                else if (MantaStingActionExecutor.IsSimAuthority(_status)) StepOrbit(Time.deltaTime);
            }
            if (_hookRate > 0f && MantaStingActionExecutor.IsSimAuthority(_status)) StepHook(Time.deltaTime);

            ResolveSiblings();
            if (_gate != null) StepGate(Time.deltaTime);
            if (autopilot && MantaStingActionExecutor.IsSimAuthority(_status) && (!_mode || _mode.UsesOrbit)) AutopilotSling();
            if (autopilot && MantaStingActionExecutor.IsSimAuthority(_status) && _mode && _mode.IsGateMode) AutopilotGate();
        }

        // ------------------------------------------------------------------ Gate mode: the warp gate (STOAT_DIPOLE.md §1f)

        StoatGateMath.Settings GateSettings() => _mode && _mode.Config ? _mode.Config.Gate() : default;

        /// <summary>
        /// The press: a black hole opened <see cref="StoatGateMath.Settings.Ahead"/> ahead of the nose and its white hole along
        /// the same line, as long as the squeeze asks (<see cref="StoatGateMath.Length"/>) - held, no pull on its owner (an
        /// owned drift pair pulls no vessel), the Time-mode poles' light strength. One gate at a time: none until the last has
        /// gone. Every peer lays its own copy from the replicated press (the squeeze is not replicated: a peer's copy keeps
        /// the length it was opened at).
        /// </summary>
        void LayGate(Side side)
        {
            var hull = _status.Transform;
            if (!hull || (_gate != null && _gate.IsAlive)) return;
            if (!BlackHoleRegistry.CanSpawn(2)) return;
            var gs = GateSettings();
            var bh = BlackHoleRegistry.Config;
            _gateAxis = hull.forward;
            _gateSink = hull.position + _gateAxis * gs.Ahead;
            float length = StoatGateMath.Length(_holds[(int)side].Live, gs, config.HoldExponent);
            float strength = config.PoleGM / Mathf.Max(1e-3f, bh.GmPerStrength);
            _gate = BlackHoleRegistry.SpawnPair(_gateSink + _gateAxis * (0.5f * length), _gateAxis, strength, gs.Mouth,
                0.5f * length, StoatGateMath.CollapseSpeed(0.5f * length, gs.CollapseSeconds), gs.HoldMaxSeconds, hull.up,
                OwnerTransform(), held: true);
            if (_gate == null) return;
            _gateUsed = false;
            _transitsSeen = BlackHoleVesselPull.VesselTransitsTotal;
            if (_status.IsTranslationRestricted && _stance) _stance.EndStance();
            PlayOneShot(config.HoldStartEvent);
            CSDebug.LogVerbose(CSLogChannel.BlackHole, $"[Stoat] gate opened {gs.Ahead:F0} u ahead, {length:F0} u long");
        }

        /// <summary>Let go (or flown through): the gate falls together in <see cref="StoatGateMath.Settings.CollapseSeconds"/>.</summary>
        void LetGoGate()
        {
            if (_gate == null || !_gate.IsAlive || !_gate.Held) return;
            BlackHoleRegistry.LetGo(_gate, StoatGateMath.CollapseSpeed(_gate.HalfGap0, GateSettings().CollapseSeconds));
        }

        /// <summary>
        /// The gate's frame: held, it stretches with the deeper squeeze (the black hole stays where it opened); let go, the
        /// registry closes it. On the simulating machine the black hole draws its owner onto its mouth
        /// (<see cref="StoatGateMath.FunnelStep"/>), and a carry through it (<see cref="BlackHoleVesselPull"/> counts every
        /// transit) scores the jump: its quality, the warp's time, and the gate let go behind the hull.
        /// </summary>
        void StepGate(float dt)
        {
            if (!_gate.IsAlive) { _gate = null; return; }
            var gs = GateSettings();
            if (_gate.Held)
            {
                if (!_gateUsed && IsHoldingAny)
                {
                    float squeeze = Mathf.Max(Squeeze01(Side.Left), Squeeze01(Side.Right));
                    float length = StoatGateMath.Length(squeeze, gs, config.HoldExponent);
                    BlackHoleRegistry.Reshape(_gate, _gateSink + _gateAxis * (0.5f * length), 0.5f * length);
                }
                else LetGoGate();
            }
            if (_gateUsed || !MantaStingActionExecutor.IsSimAuthority(_status)) return;
            var hull = _status.Transform;
            var transformer = _status.VesselTransformer;
            if (!hull || !transformer) return;
            int transits = BlackHoleVesselPull.VesselTransitsTotal;
            if (transits != _transitsSeen)
            {
                _transitsSeen = transits;
                var white = _gate.White;
                float near = white ? white.HorizonRadius * 4f + 10f : 0f;
                if (white && (hull.position - white.transform.position).sqrMagnitude <= near * near)
                {
                    // Through: the heading is the one it went in with (SetPose keeps the rotation).
                    _gateUsed = true;
                    _gateQuality = StoatGateMath.Quality(hull.forward, _gateSink, white.transform.position, gs);
                    _gateBoostUntil = Time.time + gs.BoostSeconds;
                    LetGoGate();
                    if (_gateQuality >= 0.999f) PlayOneShot(config.SweetSpotEvent);
                    PlayOneShot(config.SlingEvent);
                    CSDebug.LogVerbose(CSLogChannel.BlackHole, $"[Stoat] through the gate, quality {_gateQuality:F2}");
                    return;
                }
            }
            if (_gate.Black && !_status.IsTranslationRestricted)
            {
                var step = StoatGateMath.FunnelStep(hull.position, hull.forward, _gate.Black.transform.position, gs, dt);
                if (step.sqrMagnitude > 1e-8f) transformer.TranslateShip(step);
            }
        }

        /// <summary>
        /// The gate an autopilot cannot open by itself (it writes no trigger): on a long leg with its target near the nose
        /// (<see cref="StoatModeActionSO.AutopilotGateMinDistance"/>, <see cref="StoatModeActionSO.AutopilotGateConeDegrees"/>)
        /// it presses RT with the squeeze that ends the gate short of its target (<see cref="StoatGateMath.AutopilotLength"/>),
        /// holds it while the gate is open and lets go once through or once it closes. The gate lies along its nose and the
        /// funnel takes it in. Through the REPLICATED press, like a human's.
        /// </summary>
        void AutopilotGate()
        {
            var handler = _status.ActionHandler;
            var ai = _status.AIPilot;
            var hull = _status.Transform;
            var mc = _mode ? _mode.Config : null;
            if (!handler || !ai || !hull || !mc) return;
            if (IsHoldingAny)
            {
                if ((_gateUsed || _gate == null || !_gate.IsAlive || !_gate.Held) && ResolveBoundInput(Side.Right, handler, out var held))
                    handler.StopShipControllerActionsReplicated(held);
                return;
            }
            if (_gate != null && _gate.IsAlive) return;
            var to = ai.TargetPosition - hull.position;
            float distance = to.magnitude;
            if (distance < mc.AutopilotGateMinDistance || Vector3.Angle(hull.forward, to) > mc.AutopilotGateConeDegrees) return;
            var gs = mc.Gate();
            _aiGateSqueeze = StoatGateMath.SqueezeFor(StoatGateMath.AutopilotLength(distance, gs), gs, config.HoldExponent);
            if (ResolveBoundInput(Side.Right, handler, out var ie)) handler.PerformShipControllerActionsReplicated(ie);
        }

        /// <summary>
        /// The sling an autopilot cannot press (the arcade rule: never assume an AI can use a human's input). When the
        /// AI's target sits at least <c>aiSlingMinTurnDegrees</c> off the nose and far enough away, it presses the trigger
        /// on the side it wants to turn — the pair is laid ahead on that side, and the AI steering toward its target flies
        /// past it and is caught. Once caught it lets go when the target is within <c>aiReleaseConeDegrees</c> of the nose
        /// (or, inside the timing window, within three times that, for the full kick); a pair that has not caught it after
        /// <c>aiCatchGiveUpSeconds</c> is let go. Both edges through the REPLICATED path, so every peer runs the same sling
        /// a human's would. Simulating machine only.
        /// </summary>
        void AutopilotSling()
        {
            var handler = _status.ActionHandler;
            var ai = _status.AIPilot;
            var hull = _status.Transform;
            if (!handler || !ai || !hull) return;
            if (_laid)
            {
                bool letGo;
                if (!_caught) letGo = Time.time - _laidAt >= AIConfig.AiCatchGiveUpSeconds;
                else
                {
                    float off = Vector3.Angle(hull.forward, ai.TargetPosition - hull.position);
                    float cone = AIConfig.AiReleaseConeDegrees;
                    letGo = off <= cone || (Timing01 >= 0.999f && off <= 3f * cone);
                }
                if (letGo && ResolveBoundInput(_orbitSide, handler, out var held)) handler.StopShipControllerActionsReplicated(held);
                return;
            }
            if (AIConfig.AiSlingMinTurnDegrees <= 0f) return;
            if (Time.time - _aiLastSlingTime < AIConfig.AiSlingIntervalSeconds) return;
            // Bearing from the steering point, range from the objective: on a race line the steering point sits one
            // look-ahead away, inside AiSlingMinDistance at cruise (StoatDipoleMath.AutopilotGauge).
            var local = StoatDipoleMath.AutopilotGauge(hull.InverseTransformPoint(ai.TargetPosition),
                                                       (ai.ObjectivePosition - hull.position).magnitude);
            if (!StoatSlingMath.TryAutopilotSide(local, AIConfig.AiSlingMinTurnDegrees, AIConfig.AiSlingMinDistance, out bool attractorLeft))
                return;
            var side = attractorLeft ? Side.Left : Side.Right;
            if (!ResolveBoundInput(side, handler, out var ie)) return;
            _aiLastSlingTime = Time.time;
            handler.PerformShipControllerActionsReplicated(ie);                                         // the squeeze: the pair is laid ahead
            if (BlackHoleRegistry.Config.CrystalPairs) handler.StopShipControllerActionsReplicated(ie);   // the crystal sling
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
            // Gate mode ramps a key or a touch button to a full squeeze over its own, shorter time (gateKeyRampSeconds), and an
            // autopilot holds the squeeze its gate needs.
            bool gate = _mode && _mode.IsGateMode && _mode.Config;
            float ramp = gate ? _mode.Config.GateKeyRampSeconds : config.HoldRampSeconds;
            float sample = StoatSlingMath.Hold01(rawAnalog, _holds[i].HeldFor, analog, ramp, autopilot, gate ? _aiGateSqueeze : AIConfig.AutopilotHold01);
            _holds[i].Live = sample;
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

        // A vessel swap or despawn drops the squeeze and lets an orbiting pair go (it closes and annihilates).
        void OnDisable()
        {
            BlackHoleRegistry.PairMet -= OnPairMet;
            if (_laid)
            {
                _laid = _caught = false;
                BlackHoleRegistry.LetGo(_pair);
            }
            _hookRate = 0f;
            LetGoGate();
            _holds[0] = default;
            _holds[1] = default;
            _boundInput[0] = null;
            _boundInput[1] = null;
        }
    }
}
