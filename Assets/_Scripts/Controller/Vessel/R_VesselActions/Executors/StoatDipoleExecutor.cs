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
    /// The Stoat's FIELD DIPOLE — its Space ability (<c>R_VesselActions/STOAT_DIPOLE.md</c>), the round-15
    /// field trajectory of the Stoat Flight Studio. One sink–source pair, laid ahead of the hull and held
    /// open by the triggers: their DIFFERENCE pulls the poles apart sideways (the sink on the deeper
    /// trigger's side), their SUM lengthways (the sink always the nearer, so a pilot shoots into the sink
    /// and out of the source). The poles' strength and size never follow the triggers — only their
    /// separation does — and Space sizes them (<see cref="StoatDipoleConfigSO.PoleSize"/>). Ease off and
    /// they come back together. LET GO of a trigger and the pair is gone at once — annihilated where it
    /// stands, never recoverable — and every PRESS lays a fresh pair (both triggers pressed on one frame lay
    /// one). So there is only ever one pair, and only while it is held.
    ///
    /// <para><b>What the pair is.</b> Two of the black-hole system's own holes (<see cref="BlackHoleRegistry.Spawn"/>,
    /// a Sink and a Source joined as each other's <see cref="BlackHole.Throat"/>), placed by this executor
    /// every frame — not a registry <c>Pair</c>, whose closing drift is not this ability's. So prisms fall
    /// through the sink by the field's own job, the lens draws both, and a vessel crossing the sink's
    /// horizon is carried out of the source by <see cref="BlackHoleVesselPull"/>. On top of the platform's
    /// rules the sink carries its owner's:</para>
    /// <list type="bullet">
    /// <item><b>Prisms</b> it captures VANISH (destroyed at the horizon, no debris) — or, with the Space
    /// upgrade (level 5 "Accretion", replicated <c>IsUpgradeActive</c>), are STOLEN: carried out of the source in the
    /// owner's domain (<see cref="BlackHole.PrismCapture"/>), and every one swallowed GROWS both poles until the pair
    /// annihilates (<see cref="StoatDipoleConfigSO.AccretionPerPrism"/>).</item>
    /// <item><b>Fauna</b> that come within <see cref="StoatDipoleConfigSO.FaunaSwallowHorizons"/> of it are
    /// killed, their bodies suctioned into it (<see cref="Fauna.Predated(string, Transform)"/>, on the fauna's
    /// simulating machine).</item>
    /// <item><b>Vessels</b> carried through it (a rival's) lose their crystals on the sink's side
    /// (<see cref="BlackHole.CrystalStripShare"/>, <see cref="BlackHoleCrystalStrip"/>).</item>
    /// <item><b>Colour</b>: the sink is drawn toward the owner domain's DARK colour and the source toward
    /// its LIGHT one (<see cref="BlackHole.DomainTint"/>).</item>
    /// </list>
    ///
    /// <para><b>The owner's flight in the field.</b> <see cref="BlackHoleVesselPull"/> skips an owned
    /// horizon pair for every vessel (a vessel may not move an opposing one, Docs/ELEMENTAL_ECONOMY.md §9),
    /// so the owner's flight is flown here: <see cref="StoatDipoleMath.Step"/> — the studio's
    /// <c>fieldSubstep</c>, the same law the pathfinder predicts with — turns the hull (and its command,
    /// so the nose's follow is undisturbed) and carries the field's gravity speed along the nose, on the
    /// hull's own flight clock (<see cref="VesselTransformer.FlightTimeScale"/>, the pathfinder's boost).</para>
    ///
    /// <para><b>Modes.</b> The triggers lay the dipole in MASS mode only (<see cref="StoatModeExecutor"/>); in Time
    /// mode they fly the orbit slingshot. Every annihilation — a release, a fresh press over a live pair — throws the
    /// Charge ripple from the meeting point (<see cref="StoatRippleExecutor"/>), on every peer from its own copy.</para>
    ///
    /// <para><b>Network.</b> The simulating machine (<see cref="MantaStingActionExecutor.IsSimAuthority"/>)
    /// owns the pair: it reads the triggers, places the poles and publishes them
    /// (<see cref="R_VesselActionHandler.NetStoatDipoleSink"/> / <see cref="R_VesselActionHandler.NetStoatDipoleSource"/>).
    /// Every other peer lays its own copy from those and nothing else.</para>
    /// </summary>
    public sealed class StoatDipoleExecutor : ShipActionExecutorBase
    {
        public enum Side { Left = 0, Right = 1 }

        [Header("Config")]
        [Tooltip("Every number of the dipole and the pathfinder (shared with StoatPathfinderExecutor).")]
        [SerializeField] StoatDipoleConfigSO config;

        // The autopilot's numbers: the mode's own copy when it has one (AIModeTuning, e.g. StoatDipoleConfig_Regatta learned
        // from people's editor flights), resolved once the pilot's mode is known; this config otherwise. Only the AI's
        // reads go through it - a person flying this hull reads `config`.
        StoatDipoleConfigSO _aiConfig;
        StoatDipoleConfigSO AIConfig
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
            public float Live;      // this frame's squeeze
        }

        /// <summary>The fastest a peer's copy of a pole closes on its replicated place, per second.</summary>
        const float PeerFollowRate = 20f;
        /// <summary>How often the swallow test runs, seconds (creatures move slowly next to a frame).</summary>
        const float SwallowInterval = 0.1f;
        /// <summary>The autopilot pulls both triggers when its target is within this of the nose.</summary>
        const float AutopilotStraightDegrees = 12f;
        /// <summary>...and lays nothing for a target further off the nose than this.</summary>
        const float AutopilotMaxDegrees = 75f;

        /// <summary>
        /// A released pair on its way to nothing: the poles SLAM together from where they were let go, and their
        /// fields neutralize as they close (<see cref="BlackHole.Amplitude"/> — gravity, tides and the lens fall
        /// together at full size), annihilating where they meet. Owned by no trigger: it cannot be caught again.
        /// </summary>
        struct Collapse
        {
            public BlackHole Sink, Source;
            public Vector3 Middle, SinkFrom, SourceFrom;
            public float Age;
            public float Separation;   // the poles' distance when let go — the ripple's reach (Charge)
            public float Strength;     // the pair's strength when let go — the annihilation wave's (S = |GM| / StoatPoleGMPerStrength)
        }

        readonly List<Collapse> _collapses = new();
        readonly Hold[] _holds = new Hold[2];
        IVesselStatus _status;
        BlackHole _sink, _source;
        Vector3 _middle, _right, _forward;
        StoatDipoleMath.Separation _separation;
        float _age;
        float _gravitySpeed, _gravitySpeedFar;
        float _winding;      // degrees the heading has turned toward the sink inside its fly-through zone
        float _zoneScale;    // the poles' live size (Space × the sink's grow-in), which sizes the zone with them
        Vector3 _lastForward; // the hull's heading at the end of the last frame's field flight (the winding's "before")
        int _lastTeleportCount;
        float _nextSwallow;
        bool _publishedOpen;
        readonly InputEvents?[] _boundInput = new InputEvents?[2];
        readonly List<ShipActionSO> _bindScratch = new();
        int _openedFrame = -1;
        int _serial;         // the authority's pair count, published so peers re-lay on every fresh pair
        float _peerSerial;   // the serial of the pair a peer's copy shows
        int _capturedAtOpen;  // the sink's capture count when this pair opened (Accretion counts from it)
        float _accretion;     // Space 5: the share the poles have grown by from what the sink swallowed
        StoatModeExecutor _mode;
        StoatRippleExecutor _ripple;
        bool _resolvedSiblings;
        float _aiLastPairTime = float.NegativeInfinity;
        float _aiReleaseAt = float.PositiveInfinity;
        bool _aiHoldingLeft, _aiHoldingRight;
        float _aiHoldStart, _aiDry;          // watching the path: when this pair was laid; how long its path has been unwarped
        float _aiOrbitSwept;                 // degrees the hull has circled this pair's sink (the orbit cap)
        float _aiOrbitRate;                  // ...and how fast, degrees/s (smoothed): the sweep still to come while the poles close
        Vector3 _aiLastHullPos;
        int _aiLastTeleport;                 // a pass through the wormhole is a jump, not a sweep
        bool _aiOrbitCapped;                 // the last let-go was the orbit cap: wait the full interval, not the relay
        StoatPathfinderExecutor _pathfinder; // resolved lazily (rule 6): the warp verdict the watching autopilot reads

        public StoatDipoleConfigSO Config => config;
        /// <summary>The studio's ftStrength → the game's poleGM (studio_to_unity/stoat.json: scale 20 000).</summary>
        const float StoatPoleGMPerStrength = 20000f;

        /// <summary>True while this hull's pair is open on this machine.</summary>
        public bool IsOpen => _sink && _source && !_sink.IsDespawning && !_source.IsDespawning;
        public bool IsHolding(Side side) => _holds[(int)side].Holding;
        /// <summary>True while either trigger holds this executor's squeeze.</summary>
        public bool IsHoldingAny => _holds[0].Holding || _holds[1].Holding;
        /// <summary>Space 5's growth as a share of its ceiling, 0..1 (the HUD's Space card; 0 without the upgrade).</summary>
        public float Accretion01 => config && config.AccretionMax > 0f ? Mathf.Clamp01(_accretion / config.AccretionMax) : 0f;
        /// <summary>The ripple a let-go would throw right now, as a share of its largest (the HUD's Charge card).</summary>
        public float RippleCharge01 => IsOpen && _ripple && _ripple.Config
            ? Mathf.Clamp01(_ripple.Config.BaseRadius(_separation.Magnitude) / Mathf.Max(1f, _ripple.Config.BaseRadius(float.MaxValue)))
            : 0f;
        /// <summary>The live separation in the pair's own frame (the authority's; zero on a peer).</summary>
        public StoatDipoleMath.Separation Separation => _separation;
        /// <summary>The sink (black hole) of the open pair, or null.</summary>
        public BlackHole Sink => IsOpen ? _sink : null;
        /// <summary>The source (white hole) of the open pair, or null.</summary>
        public BlackHole Source => IsOpen ? _source : null;
        /// <summary>The field's gravity speed along the nose, u/s (the owner's flight).</summary>
        public float GravitySpeed => _gravitySpeed;
        /// <summary>Degrees the heading has turned toward the sink inside its fly-through zone (the owner's flight).</summary>
        public float Winding => _winding;

        /// <summary>The open pair as a field (what the pathfinder predicts through); false when closed.</summary>
        public bool TryGetField(out StoatDipoleMath.Field field)
        {
            field = default;
            if (!IsOpen || config == null) return false;
            float rs = Mathf.Max(0.05f, _sink.HorizonRadius), rsR = Mathf.Max(0.05f, _source.HorizonRadius);
            float gm = Mathf.Abs(_sink.GM), gmR = Mathf.Abs(_source.GM);
            field = new StoatDipoleMath.Field
            {
                Sink = _sink.transform.position, Source = _source.transform.position,
                SinkGM = gm, SinkHorizon = rs, SourceGM = gmR, SourceHorizon = rsR,
                SourceSoftening = config.SourceSofteningHorizons * rsR, AccelerationCap = config.AccelerationCap,
                SinkSoftening = config.SinkSofteningHorizons * rs,
                ThroughRadius = config.ThroughZoneRadius * _zoneScale, ThroughDegrees = config.ThroughDegrees,
            };
            return true;
        }

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _aiConfig = null;
            _resolvedSiblings = false;
            _holds[0] = default;
            _holds[1] = default;
            Close(annihilate: false);
            _gravitySpeed = 0f;
            _boundInput[0] = null;
            _boundInput[1] = null;
            _lastTeleportCount = _status?.VesselTransformer ? _status.VesselTransformer.TeleportCount : 0;
            if (config == null)
                CSDebug.LogError($"[Stoat] {name}: StoatDipoleExecutor has no StoatDipoleConfig — the triggers will lay nothing. " +
                                 "Wire Assets/_SO_Assets/VesselActions/Stoat/StoatDipoleConfig.asset on the prefab.");
        }

        /// <summary>
        /// Trigger PRESS: lay a FRESH pair — always, replacing any pair still open (the other trigger's). Both
        /// triggers pressed on the same frame lay one pair between them. The simulating machine only.
        /// </summary>
        public void BeginHold(Side side)
        {
            if (config == null || _status == null || !MantaStingActionExecutor.IsSimAuthority(_status)) return;
            // The triggers lay the dipole in MASS mode only; in Time mode they fly the orbit (StoatSlingExecutor).
            ResolveSiblings();
            if (_mode && !_mode.IsMassMode) return;
            int i = (int)side;
            if (_holds[i].Holding) return;
            _holds[i].Holding = true;
            _holds[i].HeldFor = 0f;
            _holds[i].Live = _status.AutoPilotEnabled ? AIConfig.AutopilotHold01 : 0f;
            if (!IsOpen || _openedFrame != Time.frameCount) Open();
        }

        /// <summary>
        /// Trigger RELEASE: the pair is gone at once, annihilated where it stands — there is no closing glide to
        /// catch it in, so a pair can never be recovered. A trigger still held keeps its squeeze but lays
        /// nothing until it is pressed again.
        /// </summary>
        public void Release(Side side)
        {
            int i = (int)side;
            bool wasHolding = _holds[i].Holding;
            _holds[i].Holding = false;
            _holds[i].Live = 0f;
            if (wasHolding && _status != null && MantaStingActionExecutor.IsSimAuthority(_status) && (_sink || _source))
                Close(annihilate: true);
        }

        /// <summary>A mode switch: both triggers let go here (the pair annihilates where it stands, the authority's).</summary>
        public void ReleaseAll()
        {
            Release(Side.Left);
            Release(Side.Right);
        }

        /// <summary>The siblings this executor hands to — the mode switch and the ripple — resolved once per init.</summary>
        void ResolveSiblings()
        {
            if (_resolvedSiblings) return;
            _resolvedSiblings = true;
            if (!TryGetComponent<ActionExecutorRegistry>(out var registry)) return;
            _mode = registry.Get<StoatModeExecutor>();
            _ripple = registry.Get<StoatRippleExecutor>();
        }

        // ------------------------------------------------------------------ the pair

        void Open()
        {
            var hull = _status.Transform;
            if (!hull) return;
            Close(annihilate: IsOpen);
            _middle = hull.position + hull.forward * config.AheadDistance;
            _right = hull.right;
            _forward = hull.forward;
            _separation = default;
            _accretion = 0f;
            if (!Spawn(_middle, _middle)) return;
            _capturedAtOpen = _sink.CapturedCount;
            _openedFrame = Time.frameCount;
            _serial++;
            PlayOneShot(config.OpenEvent);
            // The pair is a launch: leave the hold-still stance, which would otherwise pin the hull.
            if (_status.IsTranslationRestricted && TryGetComponent<ActionExecutorRegistry>(out var registry))
            {
                var stance = registry.Get<ToggleTranslationModeActionExecutor>();
                if (stance) stance.EndStance();
            }
        }

        bool Spawn(Vector3 sinkAt, Vector3 sourceAt)
        {
            // A collapsing pair holds two of the holes; a fresh press outranks it, oldest first.
            while (!BlackHoleRegistry.CanSpawn(2) && _collapses.Count > 0) FinishCollapse(0, detonate: true);
            if (!BlackHoleRegistry.CanSpawn(2))
            {
                CSDebug.LogVerbose(CSLogChannel.BlackHole,
                    $"[Stoat] dipole refused — needs two free of {BlackHoleRegistry.Config.MaxBlackHoles} holes ({BlackHoleRegistry.Count} live).");
                return false;
            }
            var up = _status.Transform ? _status.Transform.up : Vector3.up;
            _sink = BlackHoleRegistry.Spawn(sinkAt, 0f, Vector3.zero, up, 0.05f, HolePolarity.Sink);
            _source = _sink ? BlackHoleRegistry.Spawn(sourceAt, 0f, Vector3.zero, up, 0.05f, HolePolarity.Source) : null;
            if (!_sink || !_source)
            {
                if (_sink) _sink.BeginDespawn();
                _sink = null;
                _source = null;
                return false;
            }
            // A dipole's sink is a throat: what it captures comes out of the source (Docs/BLACK_HOLE.md §11).
            _sink.Throat = _source;
            _source.Throat = _sink;
            _sink.OwnerVessel = OwnerTransform();
            _source.OwnerVessel = OwnerTransform();
            _age = 0f;
            // The white hole's birth: its light converging from far away onto the pair (the studio's spawnWave('in', mid)).
            // Off by default (StoatDipoleConfigSO.AnnihilationWaves): in play it read as the annihilation flash at CREATION.
            if (config.AnnihilationWaves)
                BlackHoleWaves.Spawn(BlackHoleWaves.Kind.In, 0.5f * (sinkAt + sourceAt), inSeconds: config.SourceGrowSeconds);
            ApplyOwnerRules();
            return true;
        }

        /// <summary>
        /// The pair leaves this hull's hands. <paramref name="annihilate"/>: the poles slam together and neutralize
        /// (<see cref="StepCollapses"/>) — a release, a fresh press over it, the owner's close seen on a peer. Else
        /// they are removed at once (a teardown, a budget loss).
        /// </summary>
        void Close(bool annihilate)
        {
            bool live = _sink && !_sink.IsDespawning && _source && !_source.IsDespawning;
            if (annihilate && live && config != null)
            {
                var sinkAt = _sink.transform.position;
                var sourceAt = _source.transform.position;
                _collapses.Add(new Collapse
                {
                    Sink = _sink, Source = _source, Middle = 0.5f * (sinkAt + sourceAt), SinkFrom = sinkAt, SourceFrom = sourceAt,
                    Separation = Vector3.Distance(sinkAt, sourceAt),
                    // The pair's strength as it was let go (the slam drives Amplitude, and with it GM, to nothing).
                    Strength = Mathf.Abs(_sink.GM) / StoatPoleGMPerStrength,
                });
            }
            else
            {
                if (_sink && !_sink.IsDespawning) _sink.BeginDespawn();
                if (_source && !_source.IsDespawning) _source.BeginDespawn();
            }
            _sink = null;
            _source = null;
            _separation = default;
            _winding = 0f;
            _zoneScale = 0f;
            _accretion = 0f;
            PublishClosed();
        }

        /// <summary>
        /// Every released pair, one frame on: the poles accelerate into each other
        /// (<see cref="StoatDipoleMath.SlamRemaining"/>) and both holes' amplitude falls with the separation — a
        /// dipole's field IS its separation, so as the poles close the two fields cancel into nothing at the
        /// holes' full size, rather than each shrinking away. Where they meet: the annihilation.
        /// </summary>
        void StepCollapses(float dt)
        {
            for (int i = _collapses.Count - 1; i >= 0; i--)
            {
                var c = _collapses[i];
                if (!c.Sink || !c.Source || c.Sink.IsDespawning || c.Source.IsDespawning)
                {
                    FinishCollapse(i, detonate: false);
                    continue;
                }
                c.Age += dt;
                float remaining = StoatDipoleMath.SlamRemaining(c.Age, config.AnnihilateSeconds);
                c.Sink.transform.position = c.Middle + (c.SinkFrom - c.Middle) * remaining;
                c.Source.transform.position = c.Middle + (c.SourceFrom - c.Middle) * remaining;
                c.Sink.Amplitude = remaining;
                c.Source.Amplitude = remaining;
                _collapses[i] = c;
                if (remaining <= 0f)
                {
                    PlayOneShot(config.AnnihilateEvent, c.Middle);
                    FinishCollapse(i, detonate: true);
                }
            }
        }

        /// <summary>
        /// A released pair is gone. <paramref name="detonate"/>: it ANNIHILATED (met, or was cut short by a fresh press
        /// needing its holes) — the meeting point throws the Charge ripple (<see cref="StoatRippleExecutor"/>), on every
        /// peer from its own copy. A teardown or a pole lost under us throws nothing.
        /// </summary>
        void FinishCollapse(int index, bool detonate)
        {
            var c = _collapses[index];
            _collapses.RemoveAt(index);
            ResolveSiblings();
            if (detonate && _ripple) _ripple.Detonate(c.Middle, c.Separation);
            if (detonate && config != null && config.AnnihilationWaves)
            {
                // The studio's startDying: the pair's mass leaves as a gravitational wave from where the poles met, along
                // the line they met on, its strength the pair's (mass = S/6 in 0.3..2); the white hole's light leaves as an
                // outgoing shell.
                var axis = c.SourceFrom - c.SinkFrom;
                if (axis.sqrMagnitude < 1e-6f) axis = _right;
                BlackHoleWaves.Spawn(BlackHoleWaves.Kind.Gravitational, c.Middle, axis, Mathf.Min(2f, Mathf.Max(0.3f, c.Strength / 6f)));
                BlackHoleWaves.Spawn(BlackHoleWaves.Kind.Out, c.Middle);
            }
            // Amplitude 0 before the despawn: the despawn's own ease (a shrinking horizon) is then invisible.
            if (c.Sink) { c.Sink.Amplitude = 0f; if (!c.Sink.IsDespawning) c.Sink.BeginDespawn(); }
            if (c.Source) { c.Source.Amplitude = 0f; if (!c.Source.IsDespawning) c.Source.BeginDespawn(); }
        }

        /// <summary>
        /// The owner's rules on its own sink, refreshed every frame (an upgrade can arrive or lapse mid-hold):
        /// what happens to a captured prism, what a rival vessel passing through loses, the domain colours.
        /// Every peer runs this on its copy; the upgrade bit and the player's name and domain are replicated.
        /// </summary>
        void ApplyOwnerRules()
        {
            if (!_sink || !_source || _status?.Player == null) return;
            var domain = _status.Domain;
            string owner = _status.PlayerName;
            var abilities = _status.ElementalAbilityHandler;
            bool steal = abilities && abilities.IsUpgradeActive(Element.Space);
            _sink.PrismCapture = steal ? BlackHole.PrismCaptureRule.Steal : BlackHole.PrismCaptureRule.Vanish;
            _sink.CaptureDomain = domain;
            _sink.CaptureOwnerName = owner;
            _sink.CrystalStripShare = config.CrystalStripShare;
            _source.CaptureDomain = domain;
            _source.CaptureOwnerName = owner;

            var colors = PrismLit.ColorSet;
            if (colors && colors.TryGetPrismKindColors(domain, PrismKind.Plain, out var light, out var dark))
            {
                _sink.DomainTint = dark;
                _source.DomainTint = light;
                _sink.DomainTintAmount = config.DomainTintAmount;
                _source.DomainTintAmount = config.DomainTintAmount;
            }
        }

        /// <summary>
        /// Space 5 — Accretion: what the sink has swallowed since this pair opened grows both poles, up to the config's
        /// ceiling, for as long as the pair lives. Gated on the replicated unlock bit; the authority's size reaches every
        /// peer as the published horizon.
        /// </summary>
        float StepAccretion()
        {
            var abilities = _status.ElementalAbilityHandler;
            if (!_sink || !abilities || !abilities.IsUpgradeActive(Element.Space)) return 1f;
            _accretion = Mathf.Min(config.AccretionMax, (_sink.CapturedCount - _capturedAtOpen) * config.AccretionPerPrism);
            return 1f + _accretion;
        }

        /// <summary>The poles' horizon and GM this frame: fixed, Space-sized, grown in after a fresh squeeze.</summary>
        void SizePoles(float size)
        {
            float bornSink = Mathf.SmoothStep(0f, 1f, _age / Mathf.Max(0.01f, config.SinkGrowSeconds));
            float bornSource = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, _age / Mathf.Max(0.01f, config.SourceGrowSeconds)));
            _zoneScale = size * bornSink;
            SetPole(_sink, config.PoleHorizon * size * bornSink, config.PoleGM * size * bornSink);
            // the studio's rsR = ftHorizon × whSizeX × born; its push SR = ftStrength × ftWhite × born
            SetPole(_source, config.PoleHorizon * config.SourceSizeX * size * bornSource, config.PoleGM * config.SourcePush * size * bornSource);
        }

        static void SetPole(BlackHole hole, float horizon, float gm)
        {
            if (!hole || hole.IsDespawning) return;
            float perStrength = BlackHoleRegistry.Config.GmPerStrength;
            hole.SetStrength(perStrength > 0f ? gm / perStrength : 0f);
            hole.SetSize(Mathf.Max(0.05f, horizon));
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (config == null || _status == null) return;
            ResolveSiblings();
            float dt = Time.deltaTime;
            if (_collapses.Count > 0) StepCollapses(dt);
            if (MantaStingActionExecutor.IsSimAuthority(_status)) StepAuthority(dt);
            else StepPeer(dt);

            if (IsOpen)
            {
                ApplyOwnerRules();
                if (FaunaNetworkSync.IsSimAuthority && Time.time >= _nextSwallow)
                {
                    _nextSwallow = Time.time + SwallowInterval;
                    SwallowFauna();
                }
            }
        }

        void StepAuthority(float dt)
        {
            var input = _status.InputStatus;
            bool analog = input != null && input.ActiveInputDevice == InputDeviceType.Gamepad;
            bool autopilot = _status.AutoPilotEnabled;
            Track(Side.Left, input != null ? input.LeftTriggerAnalog : 0f, analog, autopilot, dt);
            Track(Side.Right, input != null ? input.RightTriggerAnalog : 0f, analog, autopilot, dt);

            // A squeeze the hole budget refused (every pilot's pairs, the fading ones too, share it) keeps asking
            // while the trigger is held, so the pair opens the moment there is room instead of only on a re-press.
            if (!IsOpen && (_holds[0].Holding || _holds[1].Holding) && BlackHoleRegistry.CanSpawn(2)) Open();

            if (IsOpen)
            {
                _age += dt;
                bool held = _holds[0].Holding || _holds[1].Holding;
                var target = StoatDipoleMath.TargetSeparation(Live(Side.Left), Live(Side.Right), config.HoldExponent,
                    config.SidewaysMax, config.LengthwaysMax);
                _separation = StoatDipoleMath.Follow(_separation, target, config.FollowRate, dt);
                StoatDipoleMath.PolePositions(_middle, _right, _forward, _separation, out var sinkAt, out var sourceAt);
                _sink.transform.position = sinkAt;
                _source.transform.position = sourceAt;
                SizePoles(config.PoleSize.EvaluateLive(_status) * StepAccretion());
                if (StoatDipoleMath.ShouldAnnihilate(held, _separation.Magnitude, _sink.HorizonRadius, _source.HorizonRadius))
                    Close(annihilate: true);
                else
                    Publish();
            }
            else if (_sink || _source)
            {
                // A pole despawned under us (the hole budget, a teardown): the pair is gone.
                Close(annihilate: false);
            }

            FlyField(dt);
            if (autopilot && (!_mode || _mode.IsMassMode)) Autopilot();
            else if (_aiHoldingLeft || _aiHoldingRight) HandBackAiHold();
        }

        /// <summary>
        /// The pilot took the hull back from the autopilot (the lava lamp's AI hands over when you click to
        /// fly) while the AI was holding a pair. <see cref="Autopilot"/> only runs while the AI flies, so
        /// without this its triggers stayed pressed forever: the pair it laid in the menu stayed open and its
        /// field flung the pilot's hull from the first frame of freestyle. Let go of what the AI pressed,
        /// through the same replicated path it pressed with; the pair then closes as any released pair does.
        /// </summary>
        void HandBackAiHold()
        {
            var handler = _status.ActionHandler;
            if (_aiHoldingLeft)
            {
                if (handler && ResolveBoundInput(Side.Left, handler, out var l)) handler.StopShipControllerActionsReplicated(l);
                else Release(Side.Left);
            }
            if (_aiHoldingRight)
            {
                if (handler && ResolveBoundInput(Side.Right, handler, out var r)) handler.StopShipControllerActionsReplicated(r);
                else Release(Side.Right);
            }
            _aiHoldingLeft = _aiHoldingRight = false;
            _aiReleaseAt = float.PositiveInfinity;
            _aiLastPairTime = Time.time;
        }

        float Live(Side side) => _holds[(int)side].Holding ? _holds[(int)side].Live : 0f;

        void Track(Side side, float rawAnalog, bool analog, bool autopilot, float dt)
        {
            int i = (int)side;
            if (!_holds[i].Holding) return;
            _holds[i].HeldFor += dt;
            // A key squeezes to the studio's fixed depth at once (keySqueeze); a trigger is its own analog depth.
            _holds[i].Live = !analog && !autopilot && config.KeySqueeze > 0f
                ? config.KeySqueeze
                : StoatSlingMath.Hold01(rawAnalog, _holds[i].HeldFor, analog, config.HoldRampSeconds, autopilot,
                    AIConfig.AutopilotHold01);
        }

        /// <summary>A peer's copy: lay, follow and close the pair exactly as the owner publishes it.</summary>
        void StepPeer(float dt)
        {
            var handler = _status.ActionHandler;
            if (!handler || !handler.IsSpawned) return;
            Vector4 sink = handler.NetStoatDipoleSink.Value;
            Vector4 published = handler.NetStoatDipoleSource.Value;
            var source = new Vector3(published.x, published.y, published.z);
            float horizon = sink.w;
            if (horizon <= 0f)
            {
                if (_sink || _source) Close(annihilate: true);
                return;
            }
            var sinkAt = new Vector3(sink.x, sink.y, sink.z);
            // A fresh pair (a new serial) is laid fresh, where it is — never the old copy slid across.
            if (IsOpen && !Mathf.Approximately(published.w, _peerSerial)) Close(annihilate: true);
            if (!IsOpen)
            {
                if (!Spawn(sinkAt, source)) return;
                _peerSerial = published.w;
            }
            float k = 1f - Mathf.Exp(-PeerFollowRate * dt);
            _sink.transform.position = Vector3.Lerp(_sink.transform.position, sinkAt, k);
            _source.transform.position = Vector3.Lerp(_source.transform.position, source, k);
            // The size travels as the horizon; the GM is the same multiple of the authored one.
            float size = horizon / Mathf.Max(1e-3f, config.PoleHorizon);
            SetPole(_sink, horizon, config.PoleGM * size);
            SetPole(_source, horizon * config.SourceSizeX, config.PoleGM * config.SourcePush * size);
        }

        void Publish()
        {
            var handler = _status.ActionHandler;
            if (!handler || !handler.IsSpawned || !handler.IsOwner || !IsOpen) return;
            var s = _sink.transform.position;
            handler.NetStoatDipoleSink.Value = new Vector4(s.x, s.y, s.z, Mathf.Max(0.05f, _sink.HorizonRadius));
            var src = _source.transform.position;
            handler.NetStoatDipoleSource.Value = new Vector4(src.x, src.y, src.z, _serial);
            _publishedOpen = true;
        }

        void PublishClosed()
        {
            if (!_publishedOpen) return;
            _publishedOpen = false;
            var handler = _status?.ActionHandler;
            if (!handler || !handler.IsSpawned || !handler.IsOwner) return;
            handler.NetStoatDipoleSink.Value = Vector4.zero;
        }

        // ------------------------------------------------------------------ the owner's flight

        /// <summary>
        /// The owner's flight in its own field, on the hull's flight clock: <see cref="StoatDipoleMath.Step"/>
        /// at ≤4 ms substeps turns the hull and its command together and carries the gravity speed along the
        /// nose; inside the sink's horizon it is carried out of the source (the platform's exit rule). With
        /// no pair, the gravity speed fades.
        /// </summary>
        void FlyField(float dt)
        {
            var hull = _status.Transform;
            var transformer = _status.VesselTransformer;
            if (!hull || !transformer || !transformer.IsActive) return;
            float clock = Mathf.Max(0f, transformer.FlightTimeScale);
            float kdt = dt * clock;

            // Through the wormhole by the platform's own carry (BlackHoleVesselPull): leave with the speed
            // the field had given before the dive, not the dive's.
            if (transformer.TeleportCount != _lastTeleportCount)
            {
                _lastTeleportCount = transformer.TeleportCount;
                _gravitySpeed = _gravitySpeedFar;
            }

            if (!TryGetField(out var field) || _status.IsTranslationRestricted || _status.IsStationary)
            {
                _winding = 0f;
                _lastForward = hull.forward;
                _gravitySpeed *= Mathf.Exp(-kdt / Mathf.Max(0.05f, config.GravityFadeSeconds));
                if (_status.IsTranslationRestricted || _status.IsStationary) _gravitySpeed = 0f;
                if (Mathf.Abs(_gravitySpeed) > 1e-3f) transformer.TranslateShip(hull.forward * (_gravitySpeed * kdt));
                return;
            }

            float engine = EngineSpeed();
            var flight = config.Flight(transformer.CruiseSpeed);
            var body = new StoatDipoleMath.Body
            {
                Position = hull.position, Rotation = hull.rotation, GravitySpeed = _gravitySpeed, Winding = _winding,
            };
            int n = Mathf.Clamp(Mathf.CeilToInt(kdt / 0.004f), 1, 32);
            float h = kdt / n;
            bool through = false;
            for (int i = 0; i < n; i++)
            {
                var before = body.Rotation * Vector3.forward;
                StoatDipoleMath.Step(ref body, field, engine, h, flight);
                float r = Vector3.Distance(body.Position, field.Sink);
                if (r > 4f * field.SinkHorizon) _gravitySpeedFar = body.GravitySpeed;
                if (r <= field.SinkHorizon) { through = true; break; }
                // The steering's turn this frame counts toward the loop too: fold it into the first substep's "before".
                if (i == 0) before = _lastForward.sqrMagnitude > 0.5f ? _lastForward : before;
                if (StoatDipoleMath.WindThrough(ref body, before, field)) { through = true; break; }
            }
            _gravitySpeed = body.GravitySpeed;
            _winding = body.Winding;

            // The field's turn, on the hull AND its command: the nose's follow toward the command keeps its gap.
            var turn = body.Rotation * Quaternion.Inverse(hull.rotation);
            turn.ToAngleAxis(out float degrees, out var axis);
            if (degrees > 180f) degrees -= 360f;
            if (Mathf.Abs(degrees) > 1e-4f && axis.sqrMagnitude > 1e-8f)
            {
                hull.rotation = turn * hull.rotation;
                transformer.ApplyRotation(degrees, axis);
            }
            if (Mathf.Abs(_gravitySpeed) > 1e-3f) transformer.TranslateShip(hull.forward * (_gravitySpeed * kdt));

            if (through)
            {
                var exit = StoatDipoleMath.ExitPoint(body.Position, field, BlackHolePairMath.EmitRadiusFraction);
                transformer.SetPose(new Pose(exit, hull.rotation));
                _lastTeleportCount = transformer.TeleportCount;
                _gravitySpeed = _gravitySpeedFar;
                _winding = 0f;
            }
            _lastForward = hull.forward;
        }

        /// <summary>The engine's own speed in the hull's flight-clock units (the published speed carries the warp).</summary>
        float EngineSpeed()
        {
            var transformer = _status.VesselTransformer;
            float clock = transformer ? Mathf.Max(1e-3f, transformer.FlightTimeScale) : 1f;
            return Mathf.Max(0f, _status.Speed) / clock;
        }

        // ------------------------------------------------------------------ what the sink takes

        /// <summary>Creatures that come into the sink are swallowed — killed, their bodies suctioned into it.</summary>
        void SwallowFauna()
        {
            if (_status.Player == null) return;
            var at = _sink.transform.position;
            float reach = _sink.HorizonRadius * config.FaunaSwallowHorizons;
            float reach2 = reach * reach;
            string killer = _status.PlayerName;
            var cells = Cell.ActiveCellsSnapshot;
            for (int c = 0; c < cells.Count; c++)
            {
                var cell = cells[c];
                if (!cell) continue;
                var fauna = cell.LiveFauna;
                for (int i = fauna.Count - 1; i >= 0; i--)
                {
                    var f = fauna[i];
                    if (!f || f.IsDying || !f.isActiveAndEnabled) continue;
                    if ((f.transform.position - at).sqrMagnitude > reach2) continue;
                    f.Predated(killer, _sink.transform);
                }
            }
        }

        // ------------------------------------------------------------------ the autopilot

        /// <summary>
        /// The pair an autopilot cannot squeeze (the arcade rule: never assume an AI can use a human's input).
        /// For a target far enough ahead it pulls both triggers (sink dead ahead: through the wormhole, warped,
        /// boosted) or the trigger on the target's side (the field turns it in), holds for
        /// <see cref="StoatDipoleConfigSO.AutopilotHoldSeconds"/> and lets go — both edges through the
        /// REPLICATED path, like a human's press. Simulating machine only.
        /// </summary>
        void Autopilot()
        {
            var handler = _status.ActionHandler;
            if (!handler) return;
            bool watch = AIConfig.AutopilotWatchPath && Pathfinder();
            if (_aiHoldingLeft || _aiHoldingRight)
            {
                // At most one lap round the sink, in either hold. A hull circling its own sink keeps the
                // predicted path warped every frame, so the watching hold below never dried out and ran to
                // Max Hold Seconds - four to six laps round the hole before the AI got out.
                var hullPos = _status.Transform.position;
                int teleports = _status.VesselTransformer ? _status.VesselTransformer.TeleportCount : 0;
                float dt = Time.deltaTime;
                if (IsOpen && teleports == _aiLastTeleport && dt > 0f)
                {
                    float swept = StoatDipoleMath.SweptAround(_sink.transform.position, _aiLastHullPos, hullPos);
                    _aiOrbitSwept += swept;
                    _aiOrbitRate += (swept / dt - _aiOrbitRate) * Mathf.Min(1f, dt * 8f);
                }
                _aiLastHullPos = hullPos;
                _aiLastTeleport = teleports;
                // Let go so the lap ENDS at the cap: the poles keep pulling until they close.
                float closing = IsOpen
                    ? StoatDipoleMath.ClosingSeconds(_separation.Magnitude, _sink.HorizonRadius, _source.HorizonRadius, config.FollowRate)
                    : 0f;
                bool capped = StoatDipoleMath.OrbitCapReached(_aiOrbitSwept, AIConfig.AutopilotMaxOrbitDegrees, _aiOrbitRate, closing);
                if (capped) _aiReleaseAt = 0f;
                else if (watch)
                {
                    // The studio's path-watching hold: keep the poles open while they warp the path and the target
                    // is still ahead; let go as it comes close, or once the warp has been off for a while.
                    float held = Time.time - _aiHoldStart;
                    _aiDry = _pathfinder.IsWarped ? 0f : _aiDry + Time.deltaTime;
                    var aiNow = _status.AIPilot;
                    bool near = aiNow && (aiNow.TargetPosition - _status.Transform.position).sqrMagnitude
                                         < AIConfig.AutopilotLetGoNear * AIConfig.AutopilotLetGoNear;
                    bool letGo = near || held > AIConfig.AutopilotMaxHoldSeconds ||
                                 (held > AIConfig.AutopilotMinHoldSeconds && _aiDry > AIConfig.AutopilotDrySeconds);
                    if (!letGo) return;
                    _aiReleaseAt = 0f;
                }
                if (Time.time < _aiReleaseAt) return;
                _aiReleaseAt = float.PositiveInfinity;
                if (_aiHoldingLeft && ResolveBoundInput(Side.Left, handler, out var l)) handler.StopShipControllerActionsReplicated(l);
                if (_aiHoldingRight && ResolveBoundInput(Side.Right, handler, out var r)) handler.StopShipControllerActionsReplicated(r);
                _aiHoldingLeft = _aiHoldingRight = false;
                _aiLastPairTime = Time.time;
                _aiOrbitCapped = capped;
                return;
            }
            // After an orbit-capped let-go, the full interval: the quick relay would lay the next pair while the
            // hull is still turning out of the last one, and catch it again.
            float wait = watch && !_aiOrbitCapped ? AIConfig.AutopilotRelaySeconds : AIConfig.AutopilotIntervalSeconds;
            if (Time.time - _aiLastPairTime < wait) return;
            var ai = _status.AIPilot;
            var hull = _status.Transform;
            if (!ai || !hull) return;
            var local = hull.InverseTransformPoint(ai.TargetPosition);
            // Watching the path lays a pair whenever the target is not right on top of it, at any bearing: the
            // warp is the speed, so the pair is open as much of the race as it can be.
            if (!StoatDipoleMath.AutopilotTriggers(local,
                    watch ? AIConfig.AutopilotLetGoNear * 1.5f : AIConfig.AutopilotMinDistance, AutopilotStraightDegrees,
                    watch ? 180f : AutopilotMaxDegrees, out bool left, out bool right))
                return;
            _aiReleaseAt = watch ? float.PositiveInfinity : Time.time + AIConfig.AutopilotHoldSeconds;
            _aiHoldStart = Time.time;
            _aiDry = 0f;
            _aiOrbitSwept = 0f;
            _aiOrbitRate = 0f;
            _aiOrbitCapped = false;
            _aiLastHullPos = hull.position;
            _aiLastTeleport = _status.VesselTransformer ? _status.VesselTransformer.TeleportCount : 0;
            if (left && ResolveBoundInput(Side.Left, handler, out var li)) { handler.PerformShipControllerActionsReplicated(li); _aiHoldingLeft = true; }
            if (right && ResolveBoundInput(Side.Right, handler, out var ri)) { handler.PerformShipControllerActionsReplicated(ri); _aiHoldingRight = true; }
        }

        /// <summary>The Stoat's pathfinder (its warp verdict), resolved until found (vessel contract rule 6).</summary>
        bool Pathfinder()
        {
            if (_pathfinder) return true;
            _pathfinder = TryGetComponent<ActionExecutorRegistry>(out var registry) ? registry.Get<StoatPathfinderExecutor>() : null;
            if (!_pathfinder) _pathfinder = GetComponent<StoatPathfinderExecutor>();
            return _pathfinder;
        }

        /// <summary>Which input event this side's trigger is bound to on THIS vessel — read off the binding maps
        /// and retried until it succeeds (they fill after executors initialize, vessel contract rule 6).</summary>
        bool ResolveBoundInput(Side side, R_VesselActionHandler handler, out InputEvents ie)
        {
            int i = (int)side;
            if (_boundInput[i].HasValue) { ie = _boundInput[i].Value; return true; }
            foreach (InputEvents candidate in System.Enum.GetValues(typeof(InputEvents)))
            {
                _bindScratch.Clear();
                handler.CollectBoundActions(candidate, _bindScratch);
                foreach (var action in _bindScratch)
                    if (action is StoatDipoleActionSO dipole && dipole.Side == side)
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

        /// <summary>The transform <see cref="BlackHoleVesselPull"/> knows this vessel by (its <c>VesselStatus</c>'s).</summary>
        Transform OwnerTransform() => _status is Component c ? c.transform : _status.Transform;

        void PlayOneShot(EventReference reference) => PlayOneShot(reference, transform.position);

        void PlayOneShot(EventReference reference, Vector3 at)
        {
            if (reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, at);
        }

        // A vessel swap or despawn closes the pair at once and drops the squeeze.
        void OnDisable()
        {
            Close(annihilate: false);
            while (_collapses.Count > 0) FinishCollapse(0, detonate: false);
            _holds[0] = default;
            _holds[1] = default;
            _boundInput[0] = null;
            _boundInput[1] = null;
            _aiReleaseAt = float.PositiveInfinity;
            _aiHoldingLeft = _aiHoldingRight = false;
            _gravitySpeed = 0f;
        }
    }
}
