using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Jewel Wasp (vessel class 31; design <c>R_VesselActions/JEWEL_WASP.md</c>; Garrett, 2026-11-14: "its signature
    /// ability should be to possess fauna and be able to fly around and consume prisms or attack vessels as that fauna").
    /// A small wasp that stings a creature, nests beside its heart crystal and flies it.
    ///
    /// <list type="bullet">
    /// <item><b>Free</b> (or riding a plant): hold RT and the creature nearest the aim lights up; let go and the wasp flies to
    /// it and stings it. LT does the same for plants. The range is the cell's diameter: any host, always. A worm segment
    /// takes the colony's head, a swarm member the whole murmuration (<see cref="Fauna.PossessionHost"/>).</item>
    /// <item><b>In a creature</b>: the sticks fly it (<see cref="IFaunaLeash.TrySteer"/>, every frame). RT is its attack at a
    /// rival vessel ahead (a scored hit, PvP is petals only) or its own suction at the prisms ahead (Space reaches further).
    /// LT is a burst of speed (Time). A (on screen on a phone) leaves; at Charge 5 leaving bursts the host.</item>
    /// <item><b>In a plant</b>: the plant grows faster with Mass while the camera orbits it (Garrett, 2026-11-15): the thumbs
    /// together turn it round the plant about the camera's own axes (no lock, no flip), Y-diff rolls it about its view axis,
    /// X-diff zooms. Either trigger chooses the next host from the view's middle and shoots the wasp out into it.</item>
    /// </list>
    ///
    /// <para><b>One machine decides</b>: the server in a networked match, because it simulates the creatures
    /// (<see cref="JewelWaspNetworkRelay"/>); every other peer mirrors it. Inside a host the wasp still shows: it glows in
    /// its pilot's colours and its tail streaks thin behind the host, for an observer who looks.</para>
    /// </summary>
    [DefaultExecutionOrder(-50)]   // the hull is placed on its host in LateUpdate before the chase camera reads it
    [DisallowMultipleComponent]
    public sealed class JewelWaspExecutor : ShipActionExecutorBase, IFaunaPossession
    {
        [Header("Wiring (serialized on the prefab so a missing wire shows in the inspector)")]
        [Tooltip("Every number (Assets/_SO_Assets/VesselActions/JewelWasp/JewelWaspConfig.asset).")]
        [SerializeField] JewelWaspConfigSO config;
        [Tooltip("The hull's model: scaled small, and smaller still nested beside a host's heart.")]
        [SerializeField] Transform hullModel;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // A person's rides on a creature, for the fauna to learn from (Garrett, 2026-11-14; PossessionRecorder).
        readonly PossessionRecorder _record = new();
#endif

        /// <summary>What the wasp is doing.</summary>
        public enum WaspMode { Free = 0, Stinging = 1, InFauna = 2, InFlora = 3 }

        IVesselStatus _status;
        WaspMode _mode;
        Fauna _host;
        Flora _plant;
        Component _stingTarget;   // the Fauna (its PossessionHost) or the Flora being flown to
        float _stingStartedAt, _stingArrivedAt;
        Component _aim;           // what the held trigger has lit
        bool _aimFauna;           // RT's aim (fauna) or LT's (flora)
        bool _rightHeld, _leftHeld;
        Quaternion _frame = Quaternion.identity;   // where the host is being flown (or the camera's orbit on a plant)
        float _throttle01 = 1f;

        float _stingReadyAt, _burstUntil, _burstReadyAt, _burstTotal, _attackReadyAt, _attackTotal, _suctionAt;
        float _strikeFrom = -1f, _strikeUntil = -1f;
        float _graceUntil;
        float _hostSince, _aiLookAt;
        float _hasteAt;

        // the hull's own state, taken over while it rides and given back after
        bool _holding, _heldWasStationary, _penned, _incorporeal;
        readonly List<Collider> _disabledColliders = new();
        readonly List<Collider> _colliderScratch = new();
        Vector3 _modelRestScale = Vector3.one;
        Quaternion _modelRestRotation = Quaternion.identity;
        bool _modelCaptured;
        float _modelScale = 1f;
        Vector3 _flightPosition;             // the sting's flight to the heart (StepSting)
        Quaternion _flightRotation = Quaternion.identity;
        JewelWaspNetworkRelay _relay;        // on the root, beside the NetworkObject (JewelWaspNetworkRelay)
        bool _relayLooked;
        Transform _abdomen;                  // the model's gaster bone (JewelWasp.fbx), curled by the sting
        Quaternion _abdomenRest = Quaternion.identity;
        float _curl;
        ButterflyAnimation _wings;           // the four wings, folded over the back while nested

        // the chase camera's stand-back while riding, and its orbit round a plant (a Spectate vantage)
        CustomCameraController _cam;
        bool _camEngaged, _spectating;
        float _camWritten;
        Vector3 _plantCentre;                // the plant's middle, eased as it grows
        float _plantRadius = -1f;            // its reach from that middle (u), eased; < 0 until first measured
        float _plantZoom = 1f;               // X-diff's zoom, a multiple of the resting stand-back
        float _plantDistance;                // the stand-back decided this frame (published as the camera's view.w)
        float _plantMeasureAt;
        Flora _viewPlant;                    // the plant the view is measuring (a mirror finds its own)
        static readonly List<Renderer> s_plantRenderers = new();

        // the held trigger's choice: a flash when it changes
        float _selectFlashFrom = -10f;
        bool _selectFlashing;

        // what others see of the wasp inside a host: its tail thinned, its hull lit in its pilot's colours
        readonly List<TrailRenderer> _tails = new();
        readonly List<float> _tailRestWidths = new();
        bool _tailsFound;
        float _tailScale = 1f;
        float _flashAt = -10f, _lastAttack01;
        bool _lastBursting, _glowing;
        WaspMode _lastDressedMode;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // a client's own ride, recorded on its own machine (the server records only its own pilot's)
        Fauna _mirrorHost;
        bool _mirrorRide;   // a ride began this possession (it is not begun again when its creature dies first)
        float _recAttack01;
        bool _recBursting;
#endif

        readonly List<VesselStatus> _vessels = new();
        float _vesselScanAt;
        static readonly List<Fauna> s_candidates = new();
        uint _hatchSerial;
        bool _warnedRemote;

        // ------------------------------------------------------------------ what the HUD and the recorder read

        public WaspMode Mode => _mode;   // on a mirror (a networked peer that is not the server) the server's, every frame
        /// <summary>The creature the wasp rides, or null.</summary>
        public Fauna Host => _mode == WaspMode.InFauna ? _host : null;
        /// <summary>The plant the wasp rides, or null.</summary>
        public Flora Plant => _mode == WaspMode.InFlora ? _plant : null;
        /// <summary>What the held trigger has lit (a Fauna or a Flora), or null.</summary>
        public Component Aim => _aim;
        public bool Bursting => Mirrors ? (Relay.State & JewelWaspNetworkRelay.BurstingBit) != 0
                                        : _mode == WaspMode.InFauna && Time.time < _burstUntil;
        public float AttackCooldown01 => Mirrors ? Relay.View.x : Remaining01(_attackReadyAt, _attackTotal);
        public float BurstCooldown01 => Mirrors ? Relay.View.y : Remaining01(_burstReadyAt, config ? config.BurstCooldown : 0f);
        public float StingCooldown01 => Mirrors ? Relay.View.z : Remaining01(_stingReadyAt, config ? config.StingCooldown : 0f);
        public JewelWaspConfigSO Config => config;

        static float Remaining01(float readyAt, float total)
        {
            if (total <= 0f) return 0f;
            float left = readyAt - Time.time;
            return left <= 0f ? 0f : Mathf.Clamp01(left / total);
        }

        // ------------------------------------------------------------------ lifecycle

        public override void Initialize(IVesselStatus shipStatus)
        {
            LetGo(launch: false);
            _status = shipStatus;
            _relay = null;
            _relayLooked = false;
            _mode = WaspMode.Free;
            _rightHeld = _leftHeld = false;
            _stingReadyAt = _burstUntil = _burstReadyAt = _attackReadyAt = _suctionAt = 0f;
            _strikeFrom = _strikeUntil = -1f;
            _graceUntil = 0f;
            _aiLookAt = Time.time + (config ? config.AILookSeconds : 1.5f);
            RestoreDress();
            _tailsFound = false;   // a re-init may bring a new tail: found again, its own width the rest
            _lastDressedMode = WaspMode.Free;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _mirrorHost = null;
            _mirrorRide = false;
#endif
            CaptureModel();
            if (!config)
                CSDebug.LogError($"[JewelWasp] {name}: no JewelWaspConfig - the wasp cannot sting. Wire " +
                                 "Assets/_SO_Assets/VesselActions/JewelWasp/JewelWaspConfig.asset.");
        }

        void OnDisable()
        {
            LetGo(launch: false);
            ReleaseCamera();
            RestoreDress();
        }

        // ------------------------------------------------------------------ the gates

        /// <summary>The machine that decides the possession: the SERVER in a networked match, because it simulates the
        /// creatures, whoever owns the hull (JEWEL_WASP.md §6.1); anywhere off the network. A hull with no relay keeps
        /// the owner rule it had (it cannot possess what another machine simulates; Sting refuses that once, loudly).</summary>
        bool DecidesHere => _status != null && _status.Transform &&
                            (Relay ? Relay.IsDecider
                                   : ElementalTransfer.IsDecidedHere(_status) && !(_status.Player is { IsNetworkOwner: false }));

        /// <summary>A networked peer that is not the server: it shows the server's possession (the model, the nest, the
        /// HUD) and, on the hull's owner, holds the hull where the server nests it.</summary>
        bool Mirrors => Relay && Relay.IsNetworked && !Relay.IsDecider;

        JewelWaspNetworkRelay Relay
        {
            get
            {
                if (!_relayLooked && _status != null && _status.Transform)
                {
                    _relayLooked = true;
                    _status.Transform.TryGetComponent(out _relay);
                }
                return _relay;
            }
        }

        bool IsLocalHuman => _status != null && _status.Player != null && !_status.IsInitializedAsAI && _status.IsLocalUser;
        bool IsAI => _status != null && (_status.IsInitializedAsAI || _status.AutoPilotEnabled ||
                                         (Relay && Relay.IsNetworked && !Relay.IsOwner && Relay.OwnerAutoPilot));

        // The four level-5 upgrades, each its own gate with its element written out (/element-ability-table reads them).
        R_VesselElementalAbilityHandler Abilities => _status?.ElementalAbilityHandler;
        /// <summary>CHARGE 5, Burst: leaving bursts the host, a blast-class hit on every rival vessel round it.</summary>
        bool ChargeUpgraded { get { var a = Abilities; return a && a.IsUpgradeActive(Element.Charge); } }
        /// <summary>MASS 5, Overgrowth: every plant near the one possessed grows as fast.</summary>
        bool MassUpgraded { get { var a = Abilities; return a && a.IsUpgradeActive(Element.Mass); } }
        /// <summary>SPACE 5, Maw: a host's suction pulls from every side, not only ahead.</summary>
        bool SpaceUpgraded { get { var a = Abilities; return a && a.IsUpgradeActive(Element.Space); } }
        /// <summary>TIME 5, Restless: no wait between stings, and none between bursts.</summary>
        bool TimeUpgraded { get { var a = Abilities; return a && a.IsUpgradeActive(Element.Time); } }

        // ------------------------------------------------------------------ the buttons (JewelWaspActionSO)

        public void Press(JewelWaspControl control, bool down)
        {
            if (!config) return;
            if (!DecidesHere)
            {
                // the server decides; the owner only keeps which trigger is held, to light what its aim would sting
                if (Mirrors && Relay.IsOwner) HoldTriggerShown(control, down);
                return;
            }
            switch (control)
            {
                case JewelWaspControl.Right:
                    _rightHeld = down;
                    if (_mode == WaspMode.InFauna) { if (down) HostRight(); }
                    else if (down) BeginAim(fauna: true);
                    else ReleaseAim(fauna: true);
                    break;
                case JewelWaspControl.Left:
                    _leftHeld = down;
                    if (_mode == WaspMode.InFauna) { if (down) Burst(); }
                    else if (down) BeginAim(fauna: false);
                    else ReleaseAim(fauna: false);
                    break;
                case JewelWaspControl.Leave:
                    if (down && (_mode == WaspMode.InFauna || _mode == WaspMode.InFlora || _mode == WaspMode.Stinging))
                        Leave(burst: _mode == WaspMode.InFauna && ChargeUpgraded);
                    break;
            }
        }

        // ------------------------------------------------------------------ the frame

        void Update()
        {
            if (!config || _status == null || !_status.Transform) return;
            float dt = Time.deltaTime;
            if (Relay && Relay.IsOwner) Relay.PublishOwnerAutoPilot(_status.AutoPilotEnabled);   // the server runs its AI
            if (!DecidesHere)
            {
                if (Mirrors) StepMirror(dt);
                else SetAim(null, true);
                return;
            }
            if (_incorporeal && _mode == WaspMode.Free && Time.time >= _graceUntil) SetIncorporeal(false);

            switch (_mode)
            {
                case WaspMode.Free:
                    if (IsAI) StepAIFree();
                    break;
                case WaspMode.Stinging:
                    StepSting(dt);
                    break;
                case WaspMode.InFauna:
                    StepHost(dt);
                    break;
                case WaspMode.InFlora:
                    StepPlant(dt);
                    break;
            }
            if (_mode != WaspMode.InFauna && (_rightHeld || _leftHeld)) UpdateAim(_aimFauna);
            else if (_mode == WaspMode.InFauna) SetAim(null, true);
            TickChoice();
            EaseModel(dt);
        }

        void LateUpdate()
        {
            if (!config || _status == null || !_status.Transform) return;
            if (DecidesHere)
            {
                PlaceHull();
                PublishState();
            }
            else if (Mirrors && Relay.IsOwner && _mode != WaspMode.Free)
                _status.Transform.SetPositionAndRotation(Relay.NestPosition, Relay.NestRotation);   // the server's nest
            if (IsLocalHuman && !_status.AutoPilotEnabled) UpdateCamera(Time.deltaTime);
            else ReleaseCamera();
        }

        // ------------------------------------------------------------------ aiming and stinging

        void BeginAim(bool fauna)
        {
            _aimFauna = fauna;
            UpdateAim(fauna);
        }

        void ReleaseAim(bool fauna)
        {
            if (_aimFauna != fauna) return;   // the other trigger's aim stands
            var target = _aim;
            SetAim(null, fauna);
            if (_rightHeld || _leftHeld) _aimFauna = _rightHeld;   // the other trigger is still held: its aim takes over
            if (target) Sting(target);
        }

        void UpdateAim(bool fauna)
        {
            AimRay(out var origin, out var forward);
            Component best = fauna ? (Component)FindFaunaAhead(origin, forward, config.AimConeDegrees, out _)
                                   : FindFloraAhead(origin, forward, config.AimConeDegrees);
            SetAim(best, fauna);
        }

        /// <summary>The aim: the hull's nose flying free; on a plant, the camera's view from its orbit, so what is chosen is
        /// what sits in the middle of the screen.</summary>
        void AimRay(out Vector3 origin, out Vector3 forward)
        {
            if (_mode == WaspMode.InFlora && PlantVantage(out origin, out _, out var frame))
            {
                forward = frame * Vector3.forward;
                return;
            }
            origin = _status.Transform.position;
            forward = _status.Transform.forward;
        }

        const float HeldGlow = 1.6f, HeldLean = 0.85f;

        void SetAim(Component target, bool fauna)
        {
            if (target == _aim) return;
            if (_aim) LifeformHighlight.Clear(_aim);
            _aim = target;
            _selectFlashing = false;
            if (!_aim || !IsLocalHuman) return;
            // a new choice flashes bright and settles (TickChoice), with its cue: the moment of choosing is felt
            _selectFlashFrom = Time.time;
            _selectFlashing = config.SelectPulseSeconds > 0f;
            LifeformHighlight.Light(_aim, _status.Domain, HeldLean, _selectFlashing ? config.SelectPulseGlow : HeldGlow);
            PlayCue(config.SelectEvent, _aim.transform.position);
        }

        /// <summary>The chosen creature or plant's flash, easing from the pulse to the held glow (this machine only).</summary>
        void TickChoice()
        {
            if (!_selectFlashing || !_aim) return;
            float k = (Time.time - _selectFlashFrom) / config.SelectPulseSeconds;
            if (k >= 1f) _selectFlashing = false;
            float glow = Mathf.Lerp(config.SelectPulseGlow, HeldGlow, Mathf.Clamp01(k) * (2f - Mathf.Clamp01(k)));
            LifeformHighlight.Light(_aim, _status.Domain, HeldLean, glow);
        }

        float StingRange(Vector3 at)
        {
            var cell = Cell.FindNearestActiveCell(at);
            return cell && cell.MembraneRadius > 1f ? cell.MembraneRadius * 2f : config.StingRangeOutsideCell;
        }

        /// <summary>The creature nearest the aim, within the cone and the sting range: any diet, its possession host
        /// possessable here, not the one the wasp rides.</summary>
        Fauna FindFaunaAhead(Vector3 from, Vector3 forward, float coneDeg, out float distance)
        {
            distance = 0f;
            var cell = Cell.FindNearestActiveCell(from);
            if (!cell) return null;
            float range = StingRange(from), rangeSqr = range * range;
            float cosCone = Mathf.Cos(coneDeg * Mathf.Deg2Rad);
            Fauna best = null;
            float bestScore = float.PositiveInfinity;
            var live = cell.LiveFauna;
            for (int i = 0; i < live.Count; i++)
            {
                var f = live[i];
                if (!f) continue;
                var host = f.PossessionHost;
                if (!host || !host.Possessable || host == _host) continue;
                Vector3 to = f.PossessionNestPoint - from;
                float sqr = to.sqrMagnitude;
                if (sqr > rangeSqr || sqr < 1e-4f) continue;
                float d = Mathf.Sqrt(sqr);
                float cos = Vector3.Dot(to / d, forward);
                if (cos < cosCone) continue;
                // nearest to the aim line first, then nearest
                float score = (1f - cos) * 4000f + d;
                if (score < bestScore) { bestScore = score; best = f; distance = d; }
            }
            return best;
        }

        Flora FindFloraAhead(Vector3 from, Vector3 forward, float coneDeg)
        {
            float range = StingRange(from), rangeSqr = range * range;
            float cosCone = Mathf.Cos(coneDeg * Mathf.Deg2Rad);
            Flora best = null;
            float bestScore = float.PositiveInfinity;
            var live = FloraHeartRegistry.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var p = live[i];
                if (!p || p.IsDying || !p.HeartTransform || p == _plant || p == _viewPlant) continue;
                Vector3 to = p.HeartTransform.position - from;
                float sqr = to.sqrMagnitude;
                if (sqr > rangeSqr || sqr < 1e-4f) continue;
                float d = Mathf.Sqrt(sqr);
                float cos = Vector3.Dot(to / d, forward);
                if (cos < cosCone) continue;
                float score = (1f - cos) * 4000f + d;
                if (score < bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        void Sting(Component target)
        {
            if (Time.time < _stingReadyAt && !TimeUpgraded) return;   // Restless (Time 5): no wait
            if (target is Fauna f)
            {
                var host = f.PossessionHost;
                if (!host || !host.Possessable)
                {
                    if (host && !host.IsSimAuthority && !_warnedRemote)
                    {
                        _warnedRemote = true;
                        CSDebug.LogWarning("[JewelWasp] this machine does not simulate that creature, so it cannot be " +
                                           "possessed from here: the hull has no JewelWaspNetworkRelay to hand the sting to " +
                                           "the server (JEWEL_WASP.md section 6.1).");
                    }
                    return;
                }
                target = host;
            }
            else if (target is not Flora) return;

            LeaveCurrent(launch: false);
            _stingTarget = target;
            _stingStartedAt = Time.time;
            _flightPosition = _status.Transform.position;
            _flightRotation = _status.Transform.rotation;
            _stingArrivedAt = -1f;
            _mode = WaspMode.Stinging;
            HoldHull(true);
            SetPen(true);
            SetIncorporeal(true);
            PlayCue(config.StingEvent, _status.Transform.position);
        }

        void StepSting(float dt)
        {
            var target = _stingTarget;
            bool gone = !target || (target is Fauna f && !f.Possessable) || (target is Flora p && p.IsDying);
            if (gone || Time.time - _stingStartedAt > config.StingMaxSeconds)
            {
                AbortSting();
                return;
            }
            var hull = _status.Transform;
            NestPose(target, out var nest, out var nestRot);
            // the flight is kept here, not read back off the transform: on the server a client's hull is placed by its
            // owner from what this publishes, so its transform lags the flight by a round trip
            Vector3 to = nest - _flightPosition;
            float arrive = HeartRadius(target) * 2f + 1f;
            if (_stingArrivedAt < 0f)
            {
                float step = config.StingSpeed * dt;
                if (to.sqrMagnitude <= Mathf.Max(arrive * arrive, step * step))
                {
                    _stingArrivedAt = Time.time;
                    _flightPosition = nest;
                }
                else
                {
                    _flightPosition += to.normalized * step;
                    if (SafeLookRotation.TryGet(to, out var look, this)) _flightRotation = look;
                }
                hull.SetPositionAndRotation(_flightPosition, _flightRotation);
                return;
            }
            // tucked in beside the heart: the sting (the model's nose dips into the crystal)
            if (Time.time - _stingArrivedAt < config.StingSeconds) return;
            if (target is Fauna host) Possess(host);
            else if (target is Flora plant) Root(plant);
        }

        void AbortSting()
        {
            _stingTarget = null;
            _mode = WaspMode.Free;
            LetGo(launch: true);
        }

        void Possess(Fauna host)
        {
            _stingTarget = null;
            if (!host.TryPossess(this))
            {
                AbortSting();
                return;
            }
            _host = host;
            _mode = WaspMode.InFauna;
            _hostSince = Time.time;
            // the creature is the wasp's from now on (Garrett, 2026-11-19: "steal the prisms of any controlled fauna and
            // convert it to its domain, even after leaving"): its body prisms, its diet's side and its offspring take the
            // wasp's domain, and nothing gives them back when the wasp leaves. Fauna.StealInto, replicated by FaunaNetworkSync.
            host.StealInto(_status.Domain);
            _burstUntil = 0f;
            _strikeFrom = _strikeUntil = -1f;
            Vector3 fwd = host.PossessionForward;
            _frame = SafeLookRotation.TryGet(fwd.sqrMagnitude > 1e-6f ? fwd : _status.Transform.forward, out var r, this)
                ? r : _status.Transform.rotation;
            _throttle01 = 1f;
            Flash();
            PlayCue(config.NestEvent, host.PossessionNestPoint);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsLocalHuman && !_status.AutoPilotEnabled) _record.Begin(host, _status);
#endif
        }

        void Root(Flora plant)
        {
            _stingTarget = null;
            _plant = plant;
            _mode = WaspMode.InFlora;
            _hasteAt = 0f;
            _frame = _status.Transform.rotation;   // the orbit starts looking the way the wasp flew in
            _plantZoom = 1f;
            BeginPlantView(plant);
            Flash();
            PlayCue(config.NestEvent, plant.HeartTransform ? plant.HeartTransform.position : plant.transform.position);
        }

        // ------------------------------------------------------------------ riding a creature

        void StepHost(float dt)
        {
            var host = _host;
            if (!host || !ReferenceEquals(host.Possessor, this))
            {
                // it died, was destroyed, or another holder took it: the wasp is thrown free
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _record.End(host ? "taken" : "host died");
#endif
                _host = null;
                _mode = WaspMode.Free;
                LetGo(launch: true);
                return;
            }

            if (IsAI) StepAIHost(dt);
            else SteerFromSticks(dt);

            if (_rightHeld && Time.time >= _suctionAt) HostRight();
            if (_strikeFrom >= 0f && Time.time >= _strikeFrom)
            {
                bool hit = TryStrikeAround(host, config.AttackReach, config.AttackHitClass, config.AttackCooldown.EvaluateLive(_status));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (hit) _record.Hit();
#endif
                if (hit || Time.time >= _strikeUntil)
                    _strikeFrom = _strikeUntil = -1f;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_record.Recording)
                _record.Tick(_status, _frame, _throttle01, _rightHeld, _leftHeld, Bursting,
                             NearestRival(host.PossessionNestPoint, _frame * Vector3.forward, 2000f, 180f));
#endif
        }

        /// <summary>The fleet's two-thumb flight on the host's heading: both sticks together pitch and yaw, one up and the
        /// other down rolls, apart is faster (<see cref="VesselTransformer"/>'s Pitch, Yaw, Roll and throttle axes).</summary>
        void SteerFromSticks(float dt)
        {
            var input = _status.InputStatus;
            if (input == null) return;
            float turn = config.HostTurnDegrees * dt;
            Vector3 right = _frame * Vector3.right, up = _frame * Vector3.up, fwd = _frame * Vector3.forward;
            _frame = Quaternion.AngleAxis(input.YSum * turn, right)
                     * Quaternion.AngleAxis(input.XSum * turn, up)
                     * Quaternion.AngleAxis(input.YDiff * config.HostRollDegrees * dt, fwd)
                     * _frame;
            _frame = Normalised(_frame);
            _throttle01 = Mathf.Clamp01(input.XDiff);
        }

        static Quaternion Normalised(Quaternion q)
        {
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return m > 1e-6f ? new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m) : Quaternion.identity;
        }

        /// <summary>The share of the host's possessed speed the throttle gives (sticks together slow, apart full).</summary>
        float ThrottleShare => Mathf.Lerp(config.HostSlowShare, 1f, _throttle01);

        float SpeedScale => config.HostSpeedScale.EvaluateLive(_status) * ThrottleShare * (Bursting ? config.BurstMultiplier : 1f);

        /// <summary>RT in a creature: the attack at a rival vessel ahead, else the suction at the prisms ahead.</summary>
        void HostRight()
        {
            var host = _host;
            if (!host) return;
            float reach = config.AttackReach + host.PossessionBodyRadius;
            var victim = NearestRival(host.PossessionNestPoint, _frame * Vector3.forward, reach * config.AttackAimReaches, 50f);
            if (victim != null)
            {
                if (Time.time < _attackReadyAt) return;
                _attackTotal = Mathf.Max(0.05f, config.AttackCooldown.EvaluateLive(_status));
                _attackReadyAt = Time.time + _attackTotal;
                host.PlayPossessionAttack(victim.Transform.position, config.AttackSeconds);
                Flash();
                _strikeFrom = Time.time + config.AttackSeconds * FaunaAttackDriver.StrikeAt;
                _strikeUntil = Time.time + config.AttackSeconds;
                PlayCue(config.AttackEvent, host.PossessionNestPoint);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _record.Attack();
#endif
                return;
            }
            _suctionAt = Time.time + config.SuctionInterval;
            int bites = host.PossessedSuction(config.SuctionReachScale.EvaluateLive(_status), config.SuctionBites, SpaceUpgraded);
            if (bites > 0)
            {
                PlayCue(config.SuctionEvent, host.PossessionNestPoint);
                Flash();
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _record.Suction(bites);
#endif
        }

        /// <summary>LT in a creature: THE TIME ABILITY, a burst of speed.</summary>
        void Burst()
        {
            if (Time.time < _burstReadyAt && !TimeUpgraded) return;
            _burstUntil = Time.time + config.BurstSeconds;
            _burstReadyAt = Time.time + config.BurstSeconds + config.BurstCooldown;
            if (_host) PlayCue(config.BurstEvent, _host.PossessionNestPoint);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _record.Burst();
#endif
        }

        // ------------------------------------------------------------------ riding a plant

        void StepPlant(float dt)
        {
            var plant = _plant;
            if (!plant || plant.IsDying)
            {
                _plant = null;
                _mode = WaspMode.Free;
                LetGo(launch: true);
                return;
            }
            // the camera orbits the plant (Garrett, 2026-11-15): the thumbs together turn it about its OWN up and right
            // (post-multiplied, so the turn never locks or flips over the top the way a world-up yaw does), Y-diff rolls it
            // about its view axis, X-diff zooms (thumbs apart in, together out)
            var input = _status.InputStatus;
            if (input != null && !IsAI)
            {
                float turn = config.FloraOrbitDegrees * dt;
                _frame = Normalised(_frame
                                    * Quaternion.AngleAxis(input.XSum * turn, Vector3.up)
                                    * Quaternion.AngleAxis(input.YSum * turn, Vector3.right)
                                    * Quaternion.AngleAxis(input.YDiff * config.FloraRollDegrees * dt, Vector3.forward));
                var range = config.FloraZoomRange;
                _plantZoom = Mathf.Clamp(_plantZoom * Mathf.Pow(2f, -input.XDiff * config.FloraZoomRate * dt),
                                         Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
                // a held trigger's choice settles toward the middle of the view
                if ((_rightHeld || _leftHeld) && _aim && config.FloraAimAssistDegrees > 0f &&
                    PlantVantage(out var eye, out _, out _) &&
                    SafeLookRotation.TryGet(_aim.transform.position - eye, _frame * Vector3.up, out var toward, this, logError: false))
                    _frame = Quaternion.RotateTowards(_frame, toward, config.FloraAimAssistDegrees * dt);
            }
            else if (IsAI && Time.time >= _aiLookAt) StepAIFree();
            MeasurePlant(plant, dt);
            // the plant grows with Mass (Flora.Hasten: it only shortens the wait between its own grow ticks)
            if (Time.time >= _hasteAt)
            {
                _hasteAt = Time.time + 0.5f;
                float factor = config.FloraGrowth.EvaluateLive(_status);
                plant.Hasten(factor, 1f);
                if (MassUpgraded && config.OvergrowthRadius > 0f && plant.HeartTransform)
                {
                    Vector3 at = plant.HeartTransform.position;
                    float r2 = config.OvergrowthRadius * config.OvergrowthRadius;
                    var live = FloraHeartRegistry.Live;
                    for (int i = 0; i < live.Count; i++)
                    {
                        var other = live[i];
                        if (!other || other == plant || other.IsDying || !other.HeartTransform) continue;
                        if ((other.HeartTransform.position - at).sqrMagnitude <= r2) other.Hasten(factor, 1f);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ the view of a plant

        void BeginPlantView(Flora plant)
        {
            _viewPlant = plant;
            _plantRadius = -1f;
            _plantMeasureAt = 0f;
            MeasurePlant(plant, 0f);
        }

        /// <summary>The plant's middle and reach, read from what it has grown (its renderers' bounds) four times a second
        /// and eased, so the view draws back smoothly as the plant grows. Its heart when nothing is drawn yet.</summary>
        void MeasurePlant(Flora plant, float dt)
        {
            if (!plant) return;
            var heart = plant.HeartTransform ? plant.HeartTransform : plant.transform;
            if (_plantRadius < 0f || Time.time >= _plantMeasureAt)
            {
                _plantMeasureAt = Time.time + 0.25f;
                Vector3 centre = heart.position;
                float radius = HeartRadius(plant) * 4f;
                plant.GetComponentsInChildren(false, s_plantRenderers);
                bool any = false;
                Bounds b = default;
                for (int i = 0; i < s_plantRenderers.Count; i++)
                {
                    var r = s_plantRenderers[i];
                    if (!r || !r.enabled) continue;
                    if (!any) { b = r.bounds; any = true; }
                    else b.Encapsulate(r.bounds);
                }
                s_plantRenderers.Clear();
                if (any)
                {
                    b.Encapsulate(heart.position);
                    centre = b.center;
                    radius = Mathf.Max(radius, b.extents.magnitude);
                }
                if (_plantRadius < 0f)
                {
                    _plantCentre = centre;
                    _plantRadius = radius;
                }
                _measuredCentre = centre;
                _measuredRadius = radius;
            }
            float k = 1f - Mathf.Exp(-config.FloraFollowEase * dt);
            _plantCentre = Vector3.Lerp(_plantCentre, _measuredCentre, k);
            _plantRadius = Mathf.Lerp(_plantRadius, _measuredRadius, k);
            _plantDistance = Mathf.Clamp(_plantRadius * config.FloraFitRadii * _plantZoom,
                                         config.FloraMinDistance, config.FloraCameraDistance);
        }

        Vector3 _measuredCentre;
        float _measuredRadius;
        Quaternion _mirrorFrame = Quaternion.identity;   // a mirror's orbit, eased onto the server's (it arrives at tick rate)
        float _mirrorDistance;

        /// <summary>Where the camera sits on a plant: the plant's middle, backed off along the orbit's forward by the
        /// stand-back. The decider's own orbit and zoom; a mirror's from the server's (the hull's rotation and view.w).</summary>
        bool PlantVantage(out Vector3 eye, out Vector3 centre, out Quaternion frame)
        {
            eye = centre = default;
            frame = Mirrors ? _mirrorFrame : _frame;
            if (_mode != WaspMode.InFlora || _plantRadius < 0f) return false;
            centre = _plantCentre;
            float distance = Mirrors ? _mirrorDistance : _plantDistance;
            eye = centre - frame * Vector3.forward * Mathf.Max(1f, distance);
            return true;
        }

        /// <summary>A mirror's plant: the live one whose heart is nearest the nested hull.</summary>
        Flora NearestPlant(Vector3 at)
        {
            Flora best = null;
            float bestSqr = float.PositiveInfinity;
            var live = FloraHeartRegistry.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var p = live[i];
                if (!p || p.IsDying || !p.HeartTransform) continue;
                float sqr = (p.HeartTransform.position - at).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = p; }
            }
            return best;
        }

        // ------------------------------------------------------------------ leaving

        /// <summary>A: leave the host. At Charge 5 (Burst) leaving a creature bursts it: a blast-class scored hit on every
        /// rival vessel round it, and the creature dies the way a jousted one does.</summary>
        void Leave(bool burst)
        {
            if (_mode == WaspMode.Stinging)
            {
                AbortSting();
                return;
            }
            var host = _host;
            if (burst && host)
            {
                TryStrikeAround(host, config.BurstRadius, config.BurstHitClass, 0.5f, allInRange: true);
                PlayCue(config.BurstEvent, host.PossessionNestPoint);
            }
            LeaveCurrent(launch: true);
            if (burst && host && host.IsSimAuthority) host.Jousted(_status.PlayerName);
            PlayCue(config.LeaveEvent, _status.Transform.position);
        }

        /// <summary>Off whatever the wasp rides (nothing is told twice), back to flying free.</summary>
        void LeaveCurrent(bool launch)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _record.End(launch ? "left" : "re-stung");
#endif
            if (_host)
            {
                var host = _host;
                _host = null;
                host.Unpossess(this);
            }
            _plant = null;
            _viewPlant = null;
            if (_mode == WaspMode.InFauna || _mode == WaspMode.InFlora)
            {
                _mode = WaspMode.Free;
                _stingReadyAt = Time.time + config.StingCooldown;
                LetGo(launch);
            }
        }

        /// <summary>Give the hull back its flight: it moves again, lays again, and is solid again after the grace.</summary>
        void LetGo(bool launch)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _record.End(launch ? "thrown free" : "stopped");
#endif
            if (_host)
            {
                var host = _host;
                _host = null;
                host.Unpossess(this);
            }
            _plant = null;
            _viewPlant = null;
            _stingTarget = null;
            if (_aim) SetAim(null, _aimFauna);
            HoldHull(false);
            SetPen(false);
            if (!config || !launch || config.LeaveGraceSeconds <= 0f) SetIncorporeal(false);
            else _graceUntil = Time.time + config.LeaveGraceSeconds;
            if (_mode != WaspMode.Free) _mode = WaspMode.Free;
        }

        // ------------------------------------------------------------------ IFaunaPossession

        bool IFaunaLeash.TrySteer(Fauna creature, out Vector3 goal, out float speed, out float turnRate)
        {
            goal = default;
            speed = 0f;
            turnRate = 0f;
            if (!config || creature != _host || _mode != WaspMode.InFauna) return false;
            float ahead = Mathf.Max(40f, creature.PossessionBodyRadius * 8f);
            goal = creature.PossessionNestPoint + _frame * Vector3.forward * ahead;
            speed = creature.PossessionTopSpeed * SpeedScale;
            turnRate = 10f;
            return true;
        }

        Prism IFaunaLeash.OrderedMeal(Fauna creature) => null;   // the rider's RT decides every mouthful

        void IFaunaLeash.Released(Fauna creature)
        {
            if (creature != _host) return;
            // the creature died, or another holder took it: StepHost throws the wasp free on its next frame
            _host = null;
        }

        bool IFaunaLeash.Sustains(Fauna creature) => false;   // the host keeps its starvation clock

        bool IFaunaLeash.TryGetLeashMark(out Domains domain, out float lean, out float glow)
        {
            // faint: an onlooker sees a creature; only an observant one notices the tint and the wasp at its heart
            domain = _status != null ? _status.Domain : default;
            lean = 0.12f;
            glow = 0f;
            return _status != null;
        }

        bool IFaunaHerd.EatsShields(Fauna creature) => true;
        float IFaunaHerd.FeedsPerOffspringScale(Fauna creature) => 1f;
        void IFaunaHerd.Born(Fauna parent, Fauna child) { }   // offspring are born wild

        public Domains PossessorDomain => _status != null ? _status.Domain : default;
        public string PossessorName => _status != null ? _status.PlayerName : name;
        public float CruiseScale(Fauna host) => config ? SpeedScale : 1f;

        // ------------------------------------------------------------------ hits (PvP is petals only)

        bool TryStrikeAround(Fauna host, float reach, CombatHitClass hitClass, float cooldown, bool allInRange = false)
        {
            if (!host || _status == null) return false;
            ScanVessels();
            Vector3 origin = host.PossessionAttackOrigin;
            float r = reach + host.PossessionBodyRadius;
            float r2 = r * r;
            bool hit = false;
            var own = _status.Domain;
            for (int i = 0; i < _vessels.Count; i++)
            {
                var v = _vessels[i];
                if (!v || v.Player == null) continue;
                IVesselStatus victim = v;
                if (victim.Domain == own || victim.Transform == _status.Transform) continue;
                Vector3 to = v.transform.position - origin;
                if (to.sqrMagnitude > r2) continue;
                if (Strike(victim, to, hitClass, cooldown)) hit = true;
                if (hit && !allInRange) return true;
            }
            return hit;
        }

        bool Strike(IVesselStatus victim, Vector3 approach, CombatHitClass hitClass, float cooldown)
        {
            if (!CombatHitDrain.TryAdmit(victim, _status, hitClass, cooldown, ElementalDebuffSources.VesselContact,
                                         out int supersededRank))
                return false;
            CombatHitDrain.ApplyPriced(victim, _status, hitClass, supersededRank,
                                       approach.normalized * 10f, ElementalDebuffSources.VesselContact);
            var channel = config.OnCombatHitLanded;
            if (channel)
                channel.Raise(new CombatHitStats
                {
                    ShooterName = _status.PlayerName,
                    VictimName = victim.PlayerName,
                    HitClass = hitClass,
                    SupersededRank = supersededRank,
                });
            return true;
        }

        /// <summary>The rival vessel nearest <paramref name="from"/> within <paramref name="range"/> and
        /// <paramref name="coneDeg"/> of <paramref name="forward"/>, or null.</summary>
        IVesselStatus NearestRival(Vector3 from, Vector3 forward, float range, float coneDeg)
        {
            ScanVessels();
            float cosCone = Mathf.Cos(coneDeg * Mathf.Deg2Rad), best = range * range;
            IVesselStatus found = null;
            var own = _status.Domain;
            for (int i = 0; i < _vessels.Count; i++)
            {
                var v = _vessels[i];
                if (!v || v.Player == null) continue;
                IVesselStatus s = v;
                if (s.Domain == own || s.Transform == _status.Transform) continue;
                Vector3 to = v.transform.position - from;
                float sqr = to.sqrMagnitude;
                if (sqr > best || sqr < 1e-4f) continue;
                if (Vector3.Dot(to / Mathf.Sqrt(sqr), forward) < cosCone) continue;
                best = sqr;
                found = s;
            }
            return found;
        }

        void ScanVessels()
        {
            if (Time.time < _vesselScanAt) return;
            _vesselScanAt = Time.time + 1f;   // once a second, not per frame (the Jacks' and the Angler's scan)
            _vessels.Clear();
            _vessels.AddRange(Object.FindObjectsByType<VesselStatus>(FindObjectsSortMode.None));
        }

        // ------------------------------------------------------------------ the omni crystal

        /// <summary>
        /// An omni crystal hatches a random PREDATOR where it was collected and the wasp possesses it at once (Garrett,
        /// 2026-11-14: "for the omni make a random predator"). Born through the ecology's one spawn seam, in the cell's
        /// colour, as any creature is; only where the ecology is simulated. False when none could be born here.
        /// </summary>
        public bool HatchPredator(Vector3 at)
        {
            if (!config || _status == null || !DecidesHere) return false;
            var cell = Cell.FindNearestActiveCell(at);
            if (!cell) return false;
            var pool = config.OmniPredators;
            int live = 0;
            for (int i = 0; i < pool.Count; i++) if (pool[i] && pool[i].FaunaPrefab) live++;
            if (live == 0) return false;
            int pick = (int)(Hash01(++_hatchSerial) * live) % live;   // vessel rule 18: a hash of a serial, not Random
            FaunaConfigurationSO cfg = null;
            for (int i = 0; i < pool.Count && !cfg; i++)
                if (pool[i] && pool[i].FaunaPrefab && pick-- == 0) cfg = pool[i];
            if (!cfg || (cfg.NetworkSynced && !FaunaNetworkSync.IsSimAuthority)) return false;
            if (cell.HasFaunaPen) at = cell.ClampToFaunaContainment(at, at);
            var creature = CellLifeSpawnerBase.SpawnFaunaWithDomain(cell, cfg.FaunaPrefab, at, cell.ControllingDomain, at, cfg);
            if (!creature) return false;
            CrystalFaunaBirth.Announce(_status, creature);   // the crystal folds into the newborn's heart
            var host = creature.PossessionHost;
            if (!host || !host.Possessable) return true;
            LeaveCurrent(launch: false);
            HoldHull(true);
            SetPen(true);
            SetIncorporeal(true);
            Possess(host);
            return true;
        }

        static float Hash01(uint n)
        {
            n ^= n >> 16; n *= 0x7feb352dU; n ^= n >> 15; n *= 0x846ca68bU; n ^= n >> 16;
            return (n & 0xFFFFFF) / 16777216f;
        }

        // ------------------------------------------------------------------ the AI

        void StepAIFree()
        {
            if (Time.time < _aiLookAt) return;
            _aiLookAt = Time.time + config.AILookSeconds;
            var hull = _status.Transform;
            // a creature ahead first; any creature in the cell if none is ahead
            var f = FindFaunaAhead(hull.position, hull.forward, 35f, out _) ?? FindFaunaAhead(hull.position, hull.forward, 180f, out _);
            if (f) Sting(f);
        }

        void StepAIHost(float dt)
        {
            var host = _host;
            var cell = Cell.FindNearestActiveCell(host.PossessionNestPoint);
            Vector3 target = cell ? cell.GetExplosionTarget(_status.Domain) : host.PossessionNestPoint + _frame * Vector3.forward * 100f;
            var rival = NearestRival(host.PossessionNestPoint, _frame * Vector3.forward, 400f, 180f);
            if (rival != null) target = rival.Transform.position;
            Vector3 to = target - host.PossessionNestPoint;
            if (to.sqrMagnitude > 1f && SafeLookRotation.TryGet(to, out var want, this))
                _frame = Quaternion.RotateTowards(_frame, want, config.HostTurnDegrees * dt);
            _throttle01 = 1f;
            if (Time.time >= _suctionAt) HostRight();
            if (Time.time - _hostSince > config.AIHostSeconds) Leave(burst: ChargeUpgraded);
        }

        // ------------------------------------------------------------------ the hull

        /// <summary>Where the wasp nests on <paramref name="target"/>: beside a creature's heart, or a plant's.</summary>
        void NestPose(Component target, out Vector3 position, out Quaternion rotation)
        {
            rotation = _frame;
            if (target is Fauna f)
            {
                var nest = f.PossessionNest;
                Vector3 side = nest ? nest.right : f.transform.right;
                float r = HeartRadius(f);
                position = f.PossessionNestPoint + side * (r * config.NestOffset);
                return;
            }
            if (target is Flora p)
            {
                var heart = p.HeartTransform ? p.HeartTransform : p.transform;
                position = heart.position + heart.right * (HeartRadius(p) * config.NestOffset);
                return;
            }
            position = _status.Transform.position;
        }

        float HeartRadius(Component target)
        {
            Transform heart = target is Fauna f ? f.PossessionNest : target is Flora p ? p.HeartTransform : null;
            if (!heart || heart == target.transform) return config.FallbackHeartRadius;
            var s = heart.lossyScale;
            return Mathf.Max(0.25f, 0.5f * Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z))));
        }

        /// <summary>The hull rides its host: placed beside the heart every frame, facing where the host is flown (on a
        /// plant: the camera's orbit). The chase camera follows the hull, so it follows the host.</summary>
        void PlaceHull()
        {
            Component target = _mode switch
            {
                WaspMode.InFauna => _host,
                WaspMode.InFlora => _plant,
                WaspMode.Stinging when _stingArrivedAt >= 0f => _stingTarget,
                _ => null,
            };
            if (!target) return;
            NestPose(target, out var position, out _);
            var hull = _status.Transform;
            if (_mode == WaspMode.Stinging && _stingTarget is Fauna)
            {
                // the sting: facing the heart, the nose dips in
                Vector3 toHeart = ((Fauna)_stingTarget).PossessionNestPoint - position;
                if (SafeLookRotation.TryGet(toHeart, out var face, this)) hull.SetPositionAndRotation(position, face);
                else hull.position = position;
                return;
            }
            hull.SetPositionAndRotation(position, _frame);
        }

        void CaptureModel()
        {
            if (!hullModel || _modelCaptured) return;
            _modelRestScale = hullModel.localScale;
            _modelRestRotation = hullModel.localRotation;
            _modelCaptured = true;
            _modelScale = config ? config.HullScale : 1f;
            hullModel.localScale = _modelRestScale * _modelScale;
            _abdomen = FindBone(hullModel, "abdomen");
            if (_abdomen) _abdomenRest = _abdomen.localRotation;
            if (_status != null && _status.Transform) _status.Transform.TryGetComponent(out _wings);
        }

        static Transform FindBone(Transform root, string bone)
        {
            if (root.name == bone) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindBone(root.GetChild(i), bone);
                if (found) return found;
            }
            return null;
        }

        void EaseModel(float dt)
        {
            if (!hullModel || !_modelCaptured) return;
            bool nested = _mode == WaspMode.InFauna || _mode == WaspMode.InFlora ||
                          (_mode == WaspMode.Stinging && _stingArrivedAt >= 0f);
            float want = nested ? config.NestedScale : config.HullScale;
            _modelScale = Mathf.MoveTowards(_modelScale, want, dt * 2f);
            hullModel.localScale = _modelRestScale * _modelScale;
            float dip = 0f, curl = nested ? config.NestedCurlDegrees : 0f;
            if (_mode == WaspMode.Stinging && _stingArrivedAt >= 0f)
            {
                float beat = Mathf.Sin(Mathf.Clamp01((Time.time - _stingArrivedAt) / config.StingSeconds) * Mathf.PI);
                dip = beat * 40f;
                curl = Mathf.Max(curl, beat * config.StingCurlDegrees);
            }
            hullModel.localRotation = _modelRestRotation * Quaternion.Euler(dip, 0f, 0f);
            // the sting: the gaster curls under the body about the waist (the model's `abdomen` bone), about the hull's
            // right axis, so the needle swings down and forward whatever the bone's own import frame
            _curl = Mathf.MoveTowards(_curl, curl, dt * 360f);
            if (_abdomen && _abdomen.parent)
            {
                var parent = _abdomen.parent.rotation;
                _abdomen.localRotation = Quaternion.Inverse(parent) * Quaternion.AngleAxis(-_curl, hullModel.right) * parent *
                                         _abdomenRest;
            }
            if (_wings) _wings.SetFolded(nested);
            DressInHost(dt, nested);
        }

        /// <summary>Mark an ability's use: the wasp flashes in its pilot's colours (see <see cref="DressInHost"/>).</summary>
        void Flash() => _flashAt = Time.time;

        /// <summary>
        /// What every machine shows of a wasp inside a host (Garrett, 2026-11-15). Its TAIL (<see cref="VesselTail"/>, the
        /// streak other players find a ship by) thins to <see cref="JewelWaspConfigSO.NestedTailWidth"/> of its own width,
        /// so a possessed creature trails a thread "to an astute observer". Its hull GLOWS in its own domain's colours
        /// through the hull's flare (<see cref="VesselAnimation.FlareBody(float)"/>, the brightness lever the Dolphin's
        /// Charge 5 lights pilots with): a slow breath while nested, a flash on every ability (an attack, a bite of
        /// suction, a burst, taking a host). The flashes a mirror cannot be told of are read from what it is told: the
        /// attack's recharge jumping full, the burst bit, a new host.
        /// </summary>
        void DressInHost(float dt, bool nested)
        {
            float attack01 = AttackCooldown01;
            bool bursting = Bursting;
            if (attack01 > _lastAttack01 + 0.5f) Flash();
            if (bursting && !_lastBursting) Flash();
            bool riding = _mode == WaspMode.InFauna || _mode == WaspMode.InFlora;
            bool wasRiding = _lastDressedMode == WaspMode.InFauna || _lastDressedMode == WaspMode.InFlora;
            if (riding && !wasRiding && Mirrors) Flash();
            _lastAttack01 = attack01;
            _lastBursting = bursting;
            _lastDressedMode = _mode;

            // the tail
            float tailWant = nested ? config.NestedTailWidth : 1f;
            if (!Mathf.Approximately(_tailScale, tailWant) || !_tailsFound)
            {
                _tailScale = Mathf.MoveTowards(_tailScale, tailWant, dt * 1.5f);
                ApplyTails(_tailScale);
            }

            // the glow
            float since = Time.time - _flashAt;
            float glow = nested ? config.NestedGlow * (0.8f + 0.2f * Mathf.Sin(Time.time * 2.4f)) : 0f;
            if (since < config.AbilityFlashSeconds)
                glow = Mathf.Max(glow, config.AbilityFlashGlow * (1f - since / config.AbilityFlashSeconds));
            if (!_wings) return;
            if (glow > 0.01f)
            {
                _wings.FlareBody(glow);
                _glowing = true;
            }
            else if (_glowing)
            {
                _wings.StopFlareBody();
                _glowing = false;
            }
        }

        void ApplyTails(float scale)
        {
            if (!_tailsFound && _status != null && _status.Transform)
            {
                _tailsFound = true;
                _tails.Clear();
                _tailRestWidths.Clear();
                var markers = _status.Transform.GetComponentsInChildren<VesselTail>(true);
                for (int m = 0; m < markers.Length; m++)
                {
                    foreach (var trail in markers[m].GetComponentsInChildren<TrailRenderer>(true))
                    {
                        _tails.Add(trail);
                        _tailRestWidths.Add(trail.widthMultiplier);
                    }
                }
            }
            for (int i = 0; i < _tails.Count; i++)
                if (_tails[i]) _tails[i].widthMultiplier = _tailRestWidths[i] * scale;
        }

        /// <summary>The tail at its own width and the hull unlit: a disable or a re-init leaves nothing borrowed behind.</summary>
        void RestoreDress()
        {
            if (_tailsFound) ApplyTails(1f);
            _tailScale = 1f;
            if (_glowing && _wings) _wings.StopFlareBody();
            _glowing = false;
            _flashAt = -10f;
        }

        /// <summary>While the wasp rides, the hull's own flight stops (the fleet's stop, <see cref="IVesselStatus.IsStationary"/>,
        /// asserted every frame, then put back as it was: the Jacks' hold).</summary>
        void HoldHull(bool hold)
        {
            if (_status == null) return;
            if (hold == _holding)
            {
                if (hold && !_status.IsStationary) { _heldWasStationary = false; _status.IsStationary = true; }
                return;
            }
            _holding = hold;
            if (hold)
            {
                _heldWasStationary = _status.IsStationary;
                _status.IsStationary = true;
            }
            else _status.IsStationary = _heldWasStationary;
        }

        void SetPen(bool penned)
        {
            if (penned == _penned) return;
            _penned = penned;
            _status?.VesselPrismController?.SetSpawnerPaused(penned);   // a nested wasp lays no trail
        }

        /// <summary>Every enabled collider on the hull off while it rides (it is inside its host), and only those back on
        /// (the Tiger Moth drop's rule).</summary>
        void SetIncorporeal(bool incorporeal)
        {
            if (incorporeal == _incorporeal) return;
            _incorporeal = incorporeal;
            var root = _status?.Transform;
            if (root) VesselIncorporeal.Set(root, incorporeal);
            if (incorporeal)
            {
                _disabledColliders.Clear();
                if (!root) return;
                _colliderScratch.Clear();
                root.GetComponentsInChildren(false, _colliderScratch);
                foreach (var c in _colliderScratch)
                {
                    if (!c || !c.enabled) continue;
                    c.enabled = false;
                    _disabledColliders.Add(c);
                }
                _colliderScratch.Clear();
            }
            else
            {
                foreach (var c in _disabledColliders)
                    if (c) c.enabled = true;
                _disabledColliders.Clear();
            }
        }

        // ------------------------------------------------------------------ the camera

        void UpdateCamera(float dt)
        {
            var manager = CameraManager.Instance;
            var cam = manager ? manager.GetActiveController() as CustomCameraController : null;
            var hull = _status.Transform;
            if (!cam || !cam.FollowTarget || !(cam.FollowTarget == hull || cam.FollowTarget.IsChildOf(hull)))
            {
                ReleaseCamera();
                return;
            }
            _cam = cam;
            UpdatePlantOrbit(cam);
            float neutral = cam.NeutralOffsetZ;
            if (neutral >= 0f) return;   // not a chase camera
            float back = _mode == WaspMode.InFlora ? 0f : CameraBack();   // on a plant the orbit frames it instead
            float want = back > 0f ? -Mathf.Max(-neutral, back) : neutral;
            if (!_camEngaged && Mathf.Approximately(want, neutral)) return;
            if (_camEngaged && !Mathf.Approximately(cam.GetCameraDistance(), _camWritten))
            {
                _camEngaged = false;   // somebody else moved it: theirs now
                return;
            }
            float now = _camEngaged ? _camWritten : cam.GetCameraDistance();
            float next = Mathf.Lerp(now, want, 1f - Mathf.Exp(-config.CameraEase * dt));
            if (Mathf.Approximately(want, neutral) && Mathf.Abs(next - neutral) < 0.05f) next = neutral;
            cam.SetCameraDistance(next);
            _camWritten = next;
            _camEngaged = !Mathf.Approximately(next, neutral);
        }

        /// <summary>On a plant the camera leaves the chase and ORBITS: a still vantage (<see cref="CustomCameraController.Spectate"/>,
        /// eased in and out by the camera) set every frame from the orbit, looking at the plant's middle with the orbit's up,
        /// so the roll turns the whole view. Cleared the frame the wasp leaves.</summary>
        void UpdatePlantOrbit(CustomCameraController cam)
        {
            if (Mirrors && _mode == WaspMode.InFlora)
            {
                // a mirror measures its own copy of the plant, the one its nested hull sits in
                bool fresh = !_viewPlant || _viewPlant.IsDying;
                if (fresh) BeginPlantView(NearestPlant(_status.Transform.position));
                else MeasurePlant(_viewPlant, Time.deltaTime);
                Quaternion served = _status.Transform.rotation;
                float k = fresh ? 1f : 1f - Mathf.Exp(-15f * Time.deltaTime);
                _mirrorFrame = fresh ? served : Quaternion.Slerp(_mirrorFrame, served, k);
                _mirrorDistance = fresh ? Relay.View.w : Mathf.Lerp(_mirrorDistance, Relay.View.w, k);
            }
            else if (Mirrors && _viewPlant)
            {
                _viewPlant = null;
                _plantRadius = -1f;
            }
            if (PlantVantage(out var eye, out var centre, out var frame))
            {
                cam.Spectate = new CustomCameraController.SpectateView
                {
                    Position = eye, LookAt = centre, Up = frame * Vector3.up,
                };
                _spectating = true;
            }
            else if (_spectating)
            {
                cam.Spectate = null;
                _spectating = false;
            }
        }

        /// <summary>How far the camera stands back while the wasp rides (0: the hull's own distance; on a plant the orbit's).</summary>
        float CameraBack()
        {
            if (Mirrors) return Relay.View.w;
            if (_mode == WaspMode.InFauna && _host)
                return Mathf.Max(config.HostCameraFloor, config.HostCameraBase + _host.PossessionBodyRadius * config.HostCameraBodies);
            return _mode == WaspMode.InFlora ? _plantDistance : 0f;
        }

        // ------------------------------------------------------------------ the network (JewelWaspNetworkRelay)

        /// <summary>The server's decision, published every frame it changes (nothing off the network).</summary>
        void PublishState()
        {
            var relay = Relay;
            if (!relay || !relay.IsNetworked) return;
            byte state = (byte)((byte)_mode & JewelWaspNetworkRelay.ModeMask);
            if (_mode == WaspMode.Stinging && _stingArrivedAt >= 0f) state |= JewelWaspNetworkRelay.ArrivedBit;
            if (_incorporeal) state |= JewelWaspNetworkRelay.IncorporealBit;
            if (Bursting) state |= JewelWaspNetworkRelay.BurstingBit;
            var hull = _status.Transform;
            bool flying = _mode == WaspMode.Stinging && _stingArrivedAt < 0f;
            relay.Publish(state, flying ? _flightPosition : hull.position, flying ? _flightRotation : hull.rotation,
                          new Vector4(AttackCooldown01, BurstCooldown01, StingCooldown01, CameraBack()));
        }

        /// <summary>A peer that is not the server follows its decision: the model dressed for the mode, the nested hull
        /// held still, laying nothing and solid to nothing, and on the owner the aim its held trigger would sting.</summary>
        void StepMirror(float dt)
        {
            var relay = Relay;
            byte state = relay.State;
            var mode = (WaspMode)(state & JewelWaspNetworkRelay.ModeMask);
            bool arrived = mode == WaspMode.Stinging && (state & JewelWaspNetworkRelay.ArrivedBit) != 0;
            if (mode != WaspMode.Stinging) _stingArrivedAt = -1f;
            else if (arrived && _stingArrivedAt < 0f) _stingArrivedAt = Time.time;
            else if (!arrived) _stingArrivedAt = -1f;
            _mode = mode;
            bool riding = mode != WaspMode.Free;
            if (relay.IsOwner) HoldHull(riding);
            SetPen(riding);
            SetIncorporeal((state & JewelWaspNetworkRelay.IncorporealBit) != 0);
            if (relay.IsOwner && mode != WaspMode.InFauna && mode != WaspMode.Stinging && (_rightHeld || _leftHeld))
                UpdateAim(_aimFauna);
            else SetAim(null, true);
            TickChoice();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RecordMirrorRide();
#endif
            EaseModel(dt);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>A client's own ride, recorded on its own machine from what the server sends (the server's recorder only
        /// hears its own pilot). The host is the creature whose nest the hull sits at. Suction bites and landed hits are
        /// decided on the server and not sent, so a client's file counts the attacks and bursts only.</summary>
        void RecordMirrorRide()
        {
            bool mine = Relay.IsOwner && IsLocalHuman && !_status.AutoPilotEnabled;
            if (!mine || _mode != WaspMode.InFauna)
            {
                if (_record.Recording) _record.End(_mode == WaspMode.Free ? "left" : "re-stung");
                _mirrorHost = null;
                _mirrorRide = false;
                return;
            }
            if (_mirrorRide && !_mirrorHost)
            {
                if (_record.Recording) _record.End("host died");   // the server throws the wasp free next
                return;
            }
            if (!_mirrorRide)
            {
                _mirrorRide = true;
                _mirrorHost = NearestHost(_status.Transform.position);
                if (!_mirrorHost) return;
                _record.Begin(_mirrorHost, _status);
                _recAttack01 = AttackCooldown01;
                _recBursting = Bursting;
            }
            float attack01 = AttackCooldown01;
            bool bursting = Bursting;
            if (attack01 > _recAttack01 + 0.5f) _record.Attack();
            if (bursting && !_recBursting) _record.Burst();
            _recAttack01 = attack01;
            _recBursting = bursting;
            var frame = _status.Transform.rotation;
            var input = _status.InputStatus;
            _record.Tick(_status, frame, input != null ? Mathf.Clamp01(input.XDiff) : 1f, _rightHeld, _leftHeld, bursting,
                         NearestRival(_mirrorHost.PossessionNestPoint, frame * Vector3.forward, 2000f, 180f));
        }

        /// <summary>The possession host whose nest is nearest <paramref name="at"/> (a mirror's own copy of the creature).</summary>
        static Fauna NearestHost(Vector3 at)
        {
            var cell = Cell.FindNearestActiveCell(at);
            if (!cell) return null;
            Fauna best = null;
            float bestSqr = float.PositiveInfinity;
            var live = cell.LiveFauna;
            for (int i = 0; i < live.Count; i++)
            {
                var f = live[i] ? live[i].PossessionHost : null;
                if (!f) continue;
                float sqr = (f.PossessionNestPoint - at).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = f; }
            }
            return best;
        }
#endif

        void HoldTriggerShown(JewelWaspControl control, bool down)
        {
            if (control == JewelWaspControl.Right)
            {
                _rightHeld = down;
                if (down) _aimFauna = true;
                else if (_leftHeld) _aimFauna = false;
            }
            else if (control == JewelWaspControl.Left)
            {
                _leftHeld = down;
                if (down) _aimFauna = false;
                else if (_rightHeld) _aimFauna = true;
            }
        }

        void ReleaseCamera()
        {
            if (_spectating && _cam) _cam.Spectate = null;
            _spectating = false;
            if (_camEngaged && _cam && Mathf.Approximately(_cam.GetCameraDistance(), _camWritten))
                _cam.SetCameraDistance(_cam.NeutralOffsetZ);
            _camEngaged = false;
            _cam = null;
        }

        static void PlayCue(FMODUnity.EventReference reference, Vector3 position)
        {
            if (reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, position);
        }
    }
}
