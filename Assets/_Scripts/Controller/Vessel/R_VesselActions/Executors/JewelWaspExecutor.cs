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
    /// <item><b>In a plant</b>: the plant grows faster with Mass, the camera stands far back and the sticks turn it, and
    /// either trigger shoots the wasp out into the next host.</item>
    /// </list>
    ///
    /// <para><b>One machine decides.</b> The presses round-trip to every peer; only the machine that owns the hull acts on
    /// them, and possession needs that machine to simulate the creature too (the leash's rule): solo and a host's own hull
    /// today. Every other peer sees the hull where its owner put it.</para>
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

        // the chase camera's stand-back while riding
        CustomCameraController _cam;
        bool _camEngaged;
        float _camWritten;

        readonly List<VesselStatus> _vessels = new();
        float _vesselScanAt;
        static readonly List<Fauna> s_candidates = new();
        uint _hatchSerial;
        bool _warnedRemote;

        // ------------------------------------------------------------------ what the HUD and the recorder read

        public WaspMode Mode => _mode;
        /// <summary>The creature the wasp rides, or null.</summary>
        public Fauna Host => _mode == WaspMode.InFauna ? _host : null;
        /// <summary>The plant the wasp rides, or null.</summary>
        public Flora Plant => _mode == WaspMode.InFlora ? _plant : null;
        /// <summary>What the held trigger has lit (a Fauna or a Flora), or null.</summary>
        public Component Aim => _aim;
        public bool Bursting => _mode == WaspMode.InFauna && Time.time < _burstUntil;
        public float AttackCooldown01 => Remaining01(_attackReadyAt, _attackTotal);
        public float BurstCooldown01 => Remaining01(_burstReadyAt, config ? config.BurstCooldown : 0f);
        public float StingCooldown01 => Remaining01(_stingReadyAt, config ? config.StingCooldown : 0f);
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
            _mode = WaspMode.Free;
            _rightHeld = _leftHeld = false;
            _stingReadyAt = _burstUntil = _burstReadyAt = _attackReadyAt = _suctionAt = 0f;
            _strikeFrom = _strikeUntil = -1f;
            _graceUntil = 0f;
            _aiLookAt = Time.time + (config ? config.AILookSeconds : 1.5f);
            CaptureModel();
            if (!config)
                CSDebug.LogError($"[JewelWasp] {name}: no JewelWaspConfig - the wasp cannot sting. Wire " +
                                 "Assets/_SO_Assets/VesselActions/JewelWasp/JewelWaspConfig.asset.");
        }

        void OnDisable()
        {
            LetGo(launch: false);
            ReleaseCamera();
        }

        // ------------------------------------------------------------------ the gates

        /// <summary>The machine that owns the hull acts on its presses (every other peer sees where it puts it).</summary>
        bool DecidesHere => _status != null && _status.Transform && ElementalTransfer.IsDecidedHere(_status) &&
                            !(_status.Player is { IsNetworkOwner: false });

        bool IsLocalHuman => _status != null && _status.Player != null && !_status.IsInitializedAsAI && _status.IsLocalUser;
        bool IsAI => _status != null && (_status.IsInitializedAsAI || _status.AutoPilotEnabled);

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
            if (!config || !DecidesHere) return;
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
            if (!DecidesHere)
            {
                SetAim(null, true);
                return;
            }
            float dt = Time.deltaTime;
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
            EaseModel(dt);
        }

        void LateUpdate()
        {
            if (!config || _status == null || !_status.Transform) return;
            if (DecidesHere) PlaceHull();
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
            var origin = _status.Transform;
            Component best = fauna ? (Component)FindFaunaAhead(origin.position, AimForward, config.AimConeDegrees, out _)
                                   : FindFloraAhead(origin.position, AimForward, config.AimConeDegrees);
            SetAim(best, fauna);
        }

        /// <summary>The aim: the hull's nose flying free, the camera's on a plant (it is the hull's frame there too).</summary>
        Vector3 AimForward => _status.Transform.forward;

        void SetAim(Component target, bool fauna)
        {
            if (target == _aim) return;
            if (_aim) LifeformHighlight.Clear(_aim);
            _aim = target;
            if (_aim && IsLocalHuman) LifeformHighlight.Light(_aim, _status.Domain, 0.85f, 1.6f);
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
                if (!p || p.IsDying || !p.HeartTransform || p == _plant) continue;
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
                                           "possessed from here (a client hull in a networked match: JEWEL_WASP.md, follow-ups).");
                    }
                    return;
                }
                target = host;
            }
            else if (target is not Flora) return;

            LeaveCurrent(launch: false);
            _stingTarget = target;
            _stingStartedAt = Time.time;
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
            Vector3 to = nest - hull.position;
            float arrive = HeartRadius(target) * 2f + 1f;
            if (_stingArrivedAt < 0f)
            {
                float step = config.StingSpeed * dt;
                if (to.sqrMagnitude <= Mathf.Max(arrive * arrive, step * step))
                {
                    _stingArrivedAt = Time.time;
                    hull.position = nest;
                }
                else
                {
                    hull.position += to.normalized * step;
                    if (SafeLookRotation.TryGet(to, out var look, this)) hull.rotation = look;
                }
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
            _burstUntil = 0f;
            _strikeFrom = _strikeUntil = -1f;
            Vector3 fwd = host.PossessionForward;
            _frame = SafeLookRotation.TryGet(fwd.sqrMagnitude > 1e-6f ? fwd : _status.Transform.forward, out var r, this)
                ? r : _status.Transform.rotation;
            _throttle01 = 1f;
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
            _frame = _status.Transform.rotation;
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
            if (bites > 0) PlayCue(config.SuctionEvent, host.PossessionNestPoint);
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
            // the sticks turn the camera round the plant (yaw and pitch)
            var input = _status.InputStatus;
            if (input != null && !IsAI)
            {
                float turn = config.FloraOrbitDegrees * dt;
                _frame = Normalised(Quaternion.AngleAxis(input.XSum * turn, Vector3.up)
                                    * Quaternion.AngleAxis(input.YSum * turn, _frame * Vector3.right) * _frame);
            }
            else if (IsAI && Time.time >= _aiLookAt) StepAIFree();
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
        }

        void EaseModel(float dt)
        {
            if (!hullModel || !_modelCaptured) return;
            bool nested = _mode == WaspMode.InFauna || _mode == WaspMode.InFlora ||
                          (_mode == WaspMode.Stinging && _stingArrivedAt >= 0f);
            float want = nested ? config.NestedScale : config.HullScale;
            _modelScale = Mathf.MoveTowards(_modelScale, want, dt * 2f);
            hullModel.localScale = _modelRestScale * _modelScale;
            float dip = 0f;
            if (_mode == WaspMode.Stinging && _stingArrivedAt >= 0f)
                dip = Mathf.Sin(Mathf.Clamp01((Time.time - _stingArrivedAt) / config.StingSeconds) * Mathf.PI) * 40f;
            hullModel.localRotation = _modelRestRotation * Quaternion.Euler(dip, 0f, 0f);
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
            float neutral = cam.NeutralOffsetZ;
            if (neutral >= 0f) return;   // not a chase camera
            float want = neutral;
            if (_mode == WaspMode.InFauna && _host)
                want = -Mathf.Max(-neutral, config.HostCameraBase + _host.PossessionBodyRadius * config.HostCameraBodies);
            else if (_mode == WaspMode.InFlora)
                want = -Mathf.Max(-neutral, config.FloraCameraDistance);
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

        void ReleaseCamera()
        {
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
