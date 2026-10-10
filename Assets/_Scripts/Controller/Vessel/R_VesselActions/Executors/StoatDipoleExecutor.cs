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
    /// they come back together; let go of both and they meet and annihilate. So there is only ever one
    /// pair, and only while it is held.
    ///
    /// <para><b>What the pair is.</b> Two of the black-hole system's own holes (<see cref="BlackHoleRegistry.Spawn"/>,
    /// a Sink and a Source joined as each other's <see cref="BlackHole.Throat"/>), placed by this executor
    /// every frame — not a registry <c>Pair</c>, whose closing drift is not this ability's. So prisms fall
    /// through the sink by the field's own job, the lens draws both, and a vessel crossing the sink's
    /// horizon is carried out of the source by <see cref="BlackHoleVesselPull"/>. On top of the platform's
    /// rules the sink carries its owner's:</para>
    /// <list type="bullet">
    /// <item><b>Prisms</b> it captures VANISH (destroyed at the horizon, no debris) — or, with the Space
    /// upgrade (level 5, replicated <c>IsUpgradeActive</c>), are STOLEN: carried out of the source in the
    /// owner's domain (<see cref="BlackHole.PrismCapture"/>).</item>
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

        readonly Hold[] _holds = new Hold[2];
        IVesselStatus _status;
        BlackHole _sink, _source;
        Vector3 _middle, _right, _forward;
        StoatDipoleMath.Separation _separation;
        float _age;
        float _gravitySpeed, _gravitySpeedFar;
        int _lastTeleportCount;
        float _nextSwallow;
        bool _publishedOpen;
        readonly InputEvents?[] _boundInput = new InputEvents?[2];
        readonly List<ShipActionSO> _bindScratch = new();
        float _aiLastPairTime = float.NegativeInfinity;
        float _aiReleaseAt = float.PositiveInfinity;
        bool _aiHoldingLeft, _aiHoldingRight;
        float _aiHoldStart, _aiDry;          // watching the path: when this pair was laid; how long its path has been unwarped
        StoatPathfinderExecutor _pathfinder; // resolved lazily (rule 6): the warp verdict the watching autopilot reads

        public StoatDipoleConfigSO Config => config;
        /// <summary>True while this hull's pair is open on this machine.</summary>
        public bool IsOpen => _sink && _source && !_sink.IsDespawning && !_source.IsDespawning;
        public bool IsHolding(Side side) => _holds[(int)side].Holding;
        /// <summary>The live separation in the pair's own frame (the authority's; zero on a peer).</summary>
        public StoatDipoleMath.Separation Separation => _separation;
        /// <summary>The sink (black hole) of the open pair, or null.</summary>
        public BlackHole Sink => IsOpen ? _sink : null;
        /// <summary>The source (white hole) of the open pair, or null.</summary>
        public BlackHole Source => IsOpen ? _source : null;
        /// <summary>The field's gravity speed along the nose, u/s (the owner's flight).</summary>
        public float GravitySpeed => _gravitySpeed;

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
            };
            return true;
        }

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
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

        /// <summary>Trigger PRESS: open the pair (or join the one the other trigger opened). The simulating machine only.</summary>
        public void BeginHold(Side side)
        {
            if (config == null || _status == null || !MantaStingActionExecutor.IsSimAuthority(_status)) return;
            int i = (int)side;
            if (_holds[i].Holding) return;
            _holds[i].Holding = true;
            _holds[i].HeldFor = 0f;
            _holds[i].Live = _status.AutoPilotEnabled ? config.AutopilotHold01 : 0f;
            if (!IsOpen) Open();
        }

        /// <summary>Trigger RELEASE: this trigger's share of the separation goes; with both let go the poles close and annihilate.</summary>
        public void Release(Side side)
        {
            int i = (int)side;
            _holds[i].Holding = false;
            _holds[i].Live = 0f;
        }

        // ------------------------------------------------------------------ the pair

        void Open()
        {
            var hull = _status.Transform;
            if (!hull) return;
            Close(annihilate: false);
            _middle = hull.position + hull.forward * config.AheadDistance;
            _right = hull.right;
            _forward = hull.forward;
            _separation = default;
            if (!Spawn(_middle, _middle)) return;
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
            ApplyOwnerRules();
            return true;
        }

        void Close(bool annihilate)
        {
            if (_sink && !_sink.IsDespawning) _sink.BeginDespawn();
            if (_source && !_source.IsDespawning) _source.BeginDespawn();
            bool wasOpen = _sink || _source;
            _sink = null;
            _source = null;
            _separation = default;
            PublishClosed();
            if (annihilate && wasOpen && config != null) PlayOneShot(config.AnnihilateEvent);
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

        /// <summary>The poles' horizon and GM this frame: fixed, Space-sized, grown in after a fresh squeeze.</summary>
        void SizePoles(float size)
        {
            float bornSink = Mathf.SmoothStep(0f, 1f, _age / Mathf.Max(0.01f, config.SinkGrowSeconds));
            float bornSource = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, _age / Mathf.Max(0.01f, config.SourceGrowSeconds)));
            SetPole(_sink, config.PoleHorizon * size * bornSink, config.PoleGM * size * bornSink);
            SetPole(_source, config.PoleHorizon * size * bornSource, config.PoleGM * config.SourcePush * size * bornSource);
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
            float dt = Time.deltaTime;
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
                SizePoles(config.PoleSize.EvaluateLive(_status));
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
            if (autopilot) Autopilot();
        }

        float Live(Side side) => _holds[(int)side].Holding ? _holds[(int)side].Live : 0f;

        void Track(Side side, float rawAnalog, bool analog, bool autopilot, float dt)
        {
            int i = (int)side;
            if (!_holds[i].Holding) return;
            _holds[i].HeldFor += dt;
            _holds[i].Live = StoatSlingMath.Hold01(rawAnalog, _holds[i].HeldFor, analog, config.HoldRampSeconds, autopilot,
                config.AutopilotHold01);
        }

        /// <summary>A peer's copy: lay, follow and close the pair exactly as the owner publishes it.</summary>
        void StepPeer(float dt)
        {
            var handler = _status.ActionHandler;
            if (!handler || !handler.IsSpawned) return;
            Vector4 sink = handler.NetStoatDipoleSink.Value;
            Vector3 source = handler.NetStoatDipoleSource.Value;
            float horizon = sink.w;
            if (horizon <= 0f)
            {
                if (_sink || _source) Close(annihilate: true);
                return;
            }
            var sinkAt = new Vector3(sink.x, sink.y, sink.z);
            if (!IsOpen && !Spawn(sinkAt, source)) return;
            float k = 1f - Mathf.Exp(-PeerFollowRate * dt);
            _sink.transform.position = Vector3.Lerp(_sink.transform.position, sinkAt, k);
            _source.transform.position = Vector3.Lerp(_source.transform.position, source, k);
            // The size travels as the horizon; the GM is the same multiple of the authored one.
            float size = horizon / Mathf.Max(1e-3f, config.PoleHorizon);
            SetPole(_sink, horizon, config.PoleGM * size);
            SetPole(_source, horizon, config.PoleGM * config.SourcePush * size);
        }

        void Publish()
        {
            var handler = _status.ActionHandler;
            if (!handler || !handler.IsSpawned || !handler.IsOwner || !IsOpen) return;
            var s = _sink.transform.position;
            handler.NetStoatDipoleSink.Value = new Vector4(s.x, s.y, s.z, Mathf.Max(0.05f, _sink.HorizonRadius));
            handler.NetStoatDipoleSource.Value = _source.transform.position;
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
                _gravitySpeed *= Mathf.Exp(-kdt / Mathf.Max(0.05f, config.GravityFadeSeconds));
                if (_status.IsTranslationRestricted || _status.IsStationary) _gravitySpeed = 0f;
                if (Mathf.Abs(_gravitySpeed) > 1e-3f) transformer.TranslateShip(hull.forward * (_gravitySpeed * kdt));
                return;
            }

            float engine = EngineSpeed();
            var flight = config.Flight(transformer.CruiseSpeed);
            var body = new StoatDipoleMath.Body { Position = hull.position, Rotation = hull.rotation, GravitySpeed = _gravitySpeed };
            int n = Mathf.Clamp(Mathf.CeilToInt(kdt / 0.004f), 1, 32);
            float h = kdt / n;
            bool through = false;
            for (int i = 0; i < n; i++)
            {
                StoatDipoleMath.Step(ref body, field, engine, h, flight);
                float r = Vector3.Distance(body.Position, field.Sink);
                if (r > 4f * field.SinkHorizon) _gravitySpeedFar = body.GravitySpeed;
                if (r <= field.SinkHorizon) { through = true; break; }
            }
            _gravitySpeed = body.GravitySpeed;

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
                var exit = BlackHolePairMath.ExitPosition(body.Position, field.Sink, field.Source, field.SourceHorizon,
                    field.Source - field.Sink);
                transformer.SetPose(new Pose(exit, hull.rotation));
                _lastTeleportCount = transformer.TeleportCount;
                _gravitySpeed = _gravitySpeedFar;
            }
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
            bool watch = config.AutopilotWatchPath && Pathfinder();
            if (_aiHoldingLeft || _aiHoldingRight)
            {
                if (watch)
                {
                    // The studio's path-watching hold: keep the poles open while they warp the path and the target
                    // is still ahead; let go as it comes close, or once the warp has been off for a while.
                    float held = Time.time - _aiHoldStart;
                    _aiDry = _pathfinder.IsWarped ? 0f : _aiDry + Time.deltaTime;
                    var aiNow = _status.AIPilot;
                    bool near = aiNow && (aiNow.TargetPosition - _status.Transform.position).sqrMagnitude
                                         < config.AutopilotLetGoNear * config.AutopilotLetGoNear;
                    bool letGo = near || held > config.AutopilotMaxHoldSeconds ||
                                 (held > config.AutopilotMinHoldSeconds && _aiDry > config.AutopilotDrySeconds);
                    if (!letGo) return;
                    _aiReleaseAt = 0f;
                }
                if (Time.time < _aiReleaseAt) return;
                _aiReleaseAt = float.PositiveInfinity;
                if (_aiHoldingLeft && ResolveBoundInput(Side.Left, handler, out var l)) handler.StopShipControllerActionsReplicated(l);
                if (_aiHoldingRight && ResolveBoundInput(Side.Right, handler, out var r)) handler.StopShipControllerActionsReplicated(r);
                _aiHoldingLeft = _aiHoldingRight = false;
                _aiLastPairTime = Time.time;
                return;
            }
            if (Time.time - _aiLastPairTime < (watch ? config.AutopilotRelaySeconds : config.AutopilotIntervalSeconds)) return;
            var ai = _status.AIPilot;
            var hull = _status.Transform;
            if (!ai || !hull) return;
            var local = hull.InverseTransformPoint(ai.TargetPosition);
            // Watching the path lays a pair whenever the target is not right on top of it, at any bearing: the
            // warp is the speed, so the pair is open as much of the race as it can be.
            if (!StoatDipoleMath.AutopilotTriggers(local,
                    watch ? config.AutopilotLetGoNear * 1.5f : config.AutopilotMinDistance, AutopilotStraightDegrees,
                    watch ? 180f : AutopilotMaxDegrees, out bool left, out bool right))
                return;
            _aiReleaseAt = watch ? float.PositiveInfinity : Time.time + config.AutopilotHoldSeconds;
            _aiHoldStart = Time.time;
            _aiDry = 0f;
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

        void PlayOneShot(EventReference reference)
        {
            if (reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, transform.position);
        }

        // A vessel swap or despawn closes the pair at once and drops the squeeze.
        void OnDisable()
        {
            Close(annihilate: false);
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
