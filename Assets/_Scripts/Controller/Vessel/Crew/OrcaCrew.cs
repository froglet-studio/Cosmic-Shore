using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;
using V3 = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Orca's crew: the 4-seat vessel (Crew of Four Lab round 4; <c>Docs/Studios/CREW_OF_FOUR_LAB.md</c>, design
    /// <c>ORCA_CREW.md</c> beside this file). Four seats on one hull, always filled: a person takes one and an AI flies
    /// every other (Garrett, 2026-10-20: "this vessel always has 4 players (AI if not people)").
    /// <list type="bullet">
    /// <item><b>Helm</b> (seat 0): flies the hull with two thumbs (the hull's own <c>VesselTransformer</c>), boosts on the
    /// right trigger while a tank lasts, and raises a <b>shield</b> on the left trigger: danger prisms on a shell round
    /// the hull with the forward cone open, riding with it for a few seconds.</item>
    /// <item><b>Gunners</b> (seats 1-3): first-person turrets on outriggers, gyro-stabilized (round 4: the aim lives in
    /// the world, the view's horizon is held and eased back to the hull's up). Release launches a missile from the
    /// hull's shared magazine and hands the gunner its controls; it bursts on contact, at its 5 s fuse or on a second
    /// Release (<c>AOEExplosion</c>), and collects any crystal it flies through for the hull. Omni crystals refill the
    /// magazine (<see cref="OrcaOmniCrystalEffectSO"/>).</item>
    /// </list>
    /// The rules are <see cref="OrcaCrewCore"/>'s, held to the lab page by <c>Tools/Build/orca_crew_lab/parity.cjs</c>;
    /// this is the glue to the scene.
    ///
    /// <para><b>Seats.</b> Keys 1-4 pick a seat; the D-pad (and the arena's <see cref="PilotSwapGesture"/>) steps through
    /// them; a two-finger tap steps on a touch screen. While the person holds a gunner seat the hull's own <c>AIPilot</c>
    /// flies it and the person's flight input is paused, exactly as the Wrasse's navigator seat does.</para>
    ///
    /// <para><b>Where it runs.</b> On the local pilot's own hull while a person flies it, and on an AI hull on the
    /// authority (every seat AI). Missiles, the shield's poses and the AI gunners are that machine's; networking the
    /// crew is a follow-up (ORCA_CREW.md), as it is for the Wrasse.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OrcaCrew : MonoBehaviour
    {
        public const int Helm = 0, Seats = 4;

        [Tooltip("Every number of the crew; each field names the Crew of Four Lab key it came from.")]
        [SerializeField] OrcaCrewConfigSO config;

        /// <summary>Every crew live this frame, so a helm can see rival missiles closing and missiles can meet.</summary>
        static readonly List<OrcaCrew> Live = new();

        enum Mode { Idle = 0, LocalHuman = 1, AICrew = 2 }
        enum TargetKind { None = 0, Hull = 1, Prism = 2, Crystal = 3, Omni = 4 }
        struct Target { public TargetKind Kind; public Vector3 Position, Velocity; public Transform Follow; }

        OrcaCrewConfigSO _cfg;
        IVessel _vessel;
        IVesselStatus _status;
        IPlayer _player;
        VesselImpactor _impactor;
        SkimmerImpactor _skimmer;
        Mode _mode;
        OrcaCrewSettings _s;
        OrcaCrewSkill _skill;
        System.Random _rng;
        OrcaCrewView _view;

        int _humanSeat = Helm;
        bool _weTookHull, _boosting, _shieldTriggerHeld;
        float _swapReadyAt, _tank, _shieldEndsAt, _shieldReadyAt, _twoFingerAt = -1f;
        CursorLockMode _cursorBefore;
        bool _cursorLocked;

        readonly OrcaTurret[] _turrets = new OrcaTurret[Seats];
        readonly OrcaMissile[] _missiles = new OrcaMissile[Seats];
        readonly Target[] _targets = new Target[Seats];
        readonly float[] _retargetAt = new float[Seats];
        OrcaFrame _pod;
        OrcaMagazine _mag;

        readonly List<Prism> _shield = new();
        readonly List<float> _shieldBorn = new();
        readonly List<Vector3> _shieldLocal = new();
        readonly List<V3> _dirs = new();
        readonly List<Prism> _query = new();
        readonly Dictionary<int, float> _crystalTakenAt = new();

        // touch: a short tap releases, a drag aims (or steers a missile from where it started)
        int _touchId = int.MinValue;
        float _touchStart;
        Vector2 _touchFrom;
        bool _touchMoved;

        // ------------------------------------------------------------------ read by the HUD and the view

        public OrcaCrewConfigSO Config => _cfg ? _cfg : config;
        /// <summary>True while the local person rides this hull (and the HUD should read it).</summary>
        public bool HumanAboard => _mode == Mode.LocalHuman;
        public int HumanSeat => _humanSeat;
        public int Missiles => _mag?.Count ?? 0;
        public float BoostTank01 => _cfg && _cfg.BoostTank > 0f ? Mathf.Clamp01(_tank / _cfg.BoostTank) : 0f;
        public float ShieldCooldown01 => _cfg && _cfg.ShieldCooldown > 0f ? Mathf.Clamp01((_shieldReadyAt - Time.time) / _cfg.ShieldCooldown) : 0f;
        public bool ShieldUp => _shield.Count > 0;
        /// <summary>When a missile last burst / the shield was last raised (Time.time), for the HUD's flashes.</summary>
        public float LastBurstAt { get; private set; } = -99f;
        public float ShieldRaisedAt { get; private set; } = -99f;

        internal IVesselStatus Status => _status;
        internal Transform Hull => _vessel?.Transform;
        internal OrcaFrame Pod => _pod;
        internal OrcaTurret TurretOf(int seat) => _turrets[seat];
        internal OrcaMissile MissileOf(int seat) => _missiles[seat];

        /// <summary>The hull collected an Omni crystal (its own contact, or a missile's): the magazine refills.</summary>
        public void OnOmniCrystal()
        {
            if (_mag == null || !_cfg) return;
            if (_mag.AddOmni(_cfg.OmniMissiles) > 0) Play(_cfg.OmniRefillSound, transform.position);
        }

        // ------------------------------------------------------------------ lifecycle

        Mode WantedMode(out IVessel vessel)
        {
            vessel = null;
            if (!config || !TryGetComponent(out vessel) || vessel.VesselStatus == null) return Mode.Idle;
            var status = vessel.VesselStatus;
            if (VesselVisionShading.LocalVessel == transform && (_weTookHull || !status.AutoPilotEnabled)) return Mode.LocalHuman;
            if (status.Player != null && status.IsInitializedAsAI && IsAuthority()) return Mode.AICrew;
            return Mode.Idle;
        }

        static bool IsAuthority()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            return !nm || !nm.IsListening || nm.IsServer;
        }

        void Bind(IVessel vessel, Mode mode)
        {
            _cfg = config;
            _vessel = vessel;
            _status = vessel.VesselStatus;
            _player = _status.Player;
            _mode = mode;
            _s = _cfg.ToSettings();
            _skill = OrcaCrewSkill.For(_cfg.AiSkill);
            _rng = new System.Random(System.Environment.TickCount ^ GetInstanceID());
            _impactor = vessel.Transform.GetComponentInChildren<VesselImpactor>();
            _skimmer = SkimmerOf(_status.NearFieldSkimmer) ?? SkimmerOf(_status.FarFieldSkimmer);
            var hull = HullFrame();
            _pod = hull;
            for (int s = 1; s < Seats; s++)
            {
                _turrets[s] ??= new OrcaTurret();
                _turrets[s].Reset(hull, hull.F);
                _missiles[s] = null;
                _targets[s] = default;
                _retargetAt[s] = 0f;
            }
            _mag = new OrcaMagazine(_cfg.StartMissiles, _cfg.MaxMissiles);
            _tank = _cfg.BoostTank;
            _humanSeat = Helm;
            _weTookHull = _boosting = _shieldTriggerHeld = false;
            _shieldEndsAt = _shieldReadyAt = 0f;
            _crystalTakenAt.Clear();
            if (!Live.Contains(this)) Live.Add(this);
            _view = new OrcaCrewView(this);
        }

        void Unbind()
        {
            if (_vessel == null) return;
            if (_humanSeat != Helm) SetHumanSeat(Helm);
            StopBoost();
            RetireShield();
            for (int s = 1; s < Seats; s++) _missiles[s] = null;
            _view?.Dispose();
            _view = null;
            Live.Remove(this);
            _vessel = null; _status = null; _player = null; _impactor = null; _skimmer = null;
            _mode = Mode.Idle;
        }

        void OnDisable() => Unbind();

        void OnDestroy() => Unbind();

        static SkimmerImpactor SkimmerOf(Skimmer skimmer) =>
            skimmer && skimmer.TryGetComponent(out SkimmerImpactor impactor) ? impactor : null;

        void Update()
        {
            var mode = WantedMode(out var vessel);
            if (mode != _mode || (_vessel != null && !UnityLiveness.Alive(_vessel.Transform))) Unbind();
            if (mode == Mode.Idle) return;
            if (_vessel == null) Bind(vessel, mode);
            float dt = Time.deltaTime;

            if (_mode == Mode.LocalHuman) PollSeat();
            if (dt <= 0f) return;
            _s = _cfg.ToSettings();

            StepHelm(dt);
            _pod.Follow(HullFrame(), dt, _s.MountSmooth);
            for (int s = 1; s < Seats; s++) StepSeat(s, dt);
            for (int s = 1; s < Seats; s++) if (_missiles[s] != null) StepMissile(s, dt);
        }

        // after the hull has moved: the shield rides with it, and the view follows and draws
        void LateUpdate()
        {
            if (_vessel == null || !UnityLiveness.Alive(_vessel.Transform)) return;
            PoseShield();
            _view?.Frame(_mode == Mode.LocalHuman ? _humanSeat : Helm);
            _view?.Draw();
        }

        // ------------------------------------------------------------------ seats

        void PollSeat()
        {
            int pick = -1;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) pick = 0;
                else if (kb.digit2Key.wasPressedThisFrame) pick = 1;
                else if (kb.digit3Key.wasPressedThisFrame) pick = 2;
                else if (kb.digit4Key.wasPressedThisFrame) pick = 3;
            }
            if (pick < 0)
            {
                // keys 1 / 2 are also the arena gesture's; a digit already picked a seat above
                int dir = PilotSwapGesture.DirectionThisFrame();
                if (dir == 0 && TwoFingerTapped()) dir = 1;
                if (dir != 0) pick = ((_humanSeat + dir) % Seats + Seats) % Seats;
            }
            if (pick < 0 || pick == _humanSeat || Time.unscaledTime < _swapReadyAt) return;
            _swapReadyAt = Time.unscaledTime + PilotSwap.CooldownSeconds;
            SetHumanSeat(pick);
        }

        // Two fingers down and both up again quickly: the touch screen's "next seat".
        bool TwoFingerTapped()
        {
            var ts = Touchscreen.current;
            if (ts == null) return false;
            if (ts.touches.Count > 1 && ts.touches[1].press.wasPressedThisFrame) _twoFingerAt = Time.unscaledTime;
            if (_twoFingerAt < 0f) return false;
            for (int i = 0; i < ts.touches.Count; i++) if (ts.touches[i].press.isPressed) return false;
            bool quick = Time.unscaledTime - _twoFingerAt < 0.35f;
            _twoFingerAt = -1f;
            return quick;
        }

        void SetHumanSeat(int seat)
        {
            int was = _humanSeat;
            _humanSeat = seat;
            if (was == Helm && seat != Helm)
            {
                SetFlightInputPaused(true);
                StopBoost();
                _weTookHull = !_status.AutoPilotEnabled;
                if (_weTookHull) _vessel.ToggleAIPilot(true);
                LockCursor(true);
            }
            else if (seat == Helm && was != Helm)
            {
                if (_weTookHull && UnityLiveness.Alive(_vessel.Transform)) _vessel.ToggleAIPilot(false);
                _weTookHull = false;
                SetFlightInputPaused(false);
                LockCursor(false);
            }
            if (seat != Helm) _turrets[seat].React = 0f;
            _touchId = int.MinValue;
        }

        void SetFlightInputPaused(bool paused)
        {
            if (_player == null) return;
            if (UnityLiveness.Alive(_player.InputController)) _player.InputController.SetPause(paused);
            else if (_player.InputStatus != null) _player.InputStatus.Paused = paused;
        }

        // Mouse look in a turret; a touch screen has no cursor to lock.
        void LockCursor(bool locked)
        {
            if (locked && !_cursorLocked && Mouse.current != null && Touchscreen.current == null)
            {
                _cursorBefore = Cursor.lockState;
                Cursor.lockState = CursorLockMode.Locked;
                _cursorLocked = true;
            }
            else if (!locked && _cursorLocked)
            {
                Cursor.lockState = _cursorBefore;
                _cursorLocked = false;
            }
        }

        bool HumanAt(int seat) => _mode == Mode.LocalHuman && _humanSeat == seat;

        int HumanGunnersAboard() => _mode == Mode.LocalHuman && _humanSeat != Helm ? 1 : 0;

        // ------------------------------------------------------------------ helm: boost and shield

        void StepHelm(float dt)
        {
            bool wantBoost = false, shieldEdge = false;
            if (HumanAt(Helm))
            {
                var input = _player?.InputStatus;
                if (input != null)
                {
                    wantBoost = input.RightTriggerAnalog > 0.5f;
                    bool held = input.LeftTriggerAnalog > 0.5f;
                    shieldEdge = held && !_shieldTriggerHeld;
                    _shieldTriggerHeld = held;
                }
            }
            else shieldEdge = HelmAIWantsShield();

            if (wantBoost && _tank > 0f)
            {
                _tank = Mathf.Max(0f, _tank - dt);
                _boosting = true;
                _status.IsBoosting = true;   // what a boost executor writes; the hull's own boostMultiplier applies
                if (_tank <= 0f) StopBoost();
            }
            else
            {
                StopBoost();
                _tank = Mathf.Min(_cfg.BoostTank, _tank + _cfg.BoostRefillAt(_status) * dt);
            }

            if (_shield.Count > 0 && Time.time >= _shieldEndsAt) RetireShield();
            if (shieldEdge && Time.time >= _shieldReadyAt) RaiseShield();
        }

        void StopBoost()
        {
            if (!_boosting) return;
            _boosting = false;
            if (UnityLiveness.Alive(_status)) _status.IsBoosting = false;
        }

        // The lab's helmAI shield: raise it when a rival missile is closing inside the range this skill notices.
        bool HelmAIWantsShield()
        {
            if (Time.time < _shieldReadyAt) return false;
            var hullPos = N(transform.position);
            foreach (var other in Live)
            {
                if (other == this || other._status == null || other._status.Domain == _status.Domain) continue;
                for (int s = 1; s < Seats; s++)
                {
                    var m = other._missiles[s];
                    if (m != null && OrcaCrewCore.IsIncoming(hullPos, m.Position, m.Frame.F, _skill.ShieldLate)
                        && _rng.NextDouble() > _skill.ShieldLate * 0.2f)
                        return true;
                }
            }
            return false;
        }

        void RaiseShield()
        {
            RetireShield();
            var channel = _status.VesselPrismController ? _status.VesselPrismController.PrismSpawnChannel : null;
            if (!channel)
            {
                CSDebug.LogWarning("[OrcaCrew] The hull has no prism spawn channel; the shield cannot be laid.");
                return;
            }
            OrcaCrewCore.ShieldDirections(_cfg.ShieldBlocks, _cfg.ShieldOpenDeg, _dirs);
            string owner = $"{_status.PlayerName}::OrcaShield::";
            var hull = transform;
            for (int k = 0; k < _dirs.Count; k++)
            {
                float radius = Mathf.Lerp(_cfg.ShieldMinRadius, _cfg.ShieldMaxRadius, (float)_rng.NextDouble());
                var local = U(_dirs[k]) * radius;
                var world = hull.TransformDirection(local);
                var prism = BoostRingBuilder.LayOne(channel, hull.position + world, Quaternion.LookRotation(world, hull.up),
                    _cfg.ShieldBlockScale, PrismKind.Danger, _status.Domain, _status.PlayerName, owner + k);
                if (!prism) continue;
                _shield.Add(prism);
                _shieldBorn.Add(prism.prismProperties.TimeCreated);
                _shieldLocal.Add(local);
            }
            _shieldEndsAt = Time.time + _cfg.ShieldSecondsAt(_status);
            _shieldReadyAt = Time.time + _cfg.ShieldCooldown;
            ShieldRaisedAt = Time.time;
            Play(_cfg.ShieldRaiseSound, hull.position);
        }

        // A shield prism is still ours only while it is alive, active, unrecycled and named for this shield (the
        // pooled-prism identity test: ShrikeExecutor.PrismAlive, SquirrelTubeActionExecutor.IsStillOurTubePrism).
        bool StillOurs(int i)
        {
            var p = _shield[i];
            return p && !p.destroyed && p.gameObject.activeInHierarchy && p.prismProperties != null
                   && Mathf.Approximately(p.prismProperties.TimeCreated, _shieldBorn[i])
                   && p.ownerID != null && p.ownerID.Contains("::OrcaShield::");
        }

        // The shield rides with the hull (lab shieldAttached): the mover contract of vessel skill 4.x1.
        void PoseShield()
        {
            if (_shield.Count == 0) return;
            var hull = transform;
            for (int i = 0; i < _shield.Count; i++)
            {
                if (!StillOurs(i)) continue;
                var world = hull.TransformDirection(_shieldLocal[i]);
                var p = _shield[i];
                p.transform.SetPositionAndRotation(hull.position + world, Quaternion.LookRotation(world, hull.up));
                p.NotifyPositionChanged();
            }
        }

        void RetireShield()
        {
            for (int i = 0; i < _shield.Count; i++)
            {
                if (!StillOurs(i)) continue;
                PrismKinds.Clear(_shield[i]);
                _shield[i].ReturnToPool();
            }
            _shield.Clear(); _shieldBorn.Clear(); _shieldLocal.Clear();
        }

        // ------------------------------------------------------------------ gunners

        void StepSeat(int s, float dt)
        {
            var t = _turrets[s];
            bool human = HumanAt(s);
            var m = _missiles[s];
            if (m != null)
            {
                if (human)
                {
                    var stick = ReadMissileStick(out bool release);
                    m.StickYaw = stick.x; m.StickPitch = stick.y; m.StickRoll = 0f;
                    if (release && m.CanBurstByHand) Burst(s);
                }
                else MissileAI(s, m);
                return;
            }

            bool fire;
            if (human)
            {
                var aim = ReadTurretAim(dt, out fire);
                t.TurnBy(aim.x, aim.y);
            }
            else fire = TurretAI(s, t, dt);
            t.HoldHorizon(N(transform.up), dt, _s.HorizonSmooth);

            int reserve = human ? 0 : _s.AiReserve * HumanGunnersAboard();
            if (fire)
            {
                t.Release(human, dt);
                if (human && _mag.Count <= 0) Play(_cfg.EmptyMagazineSound, transform.position);
            }
            if (t.Step(dt, _mag.CanTake(reserve))) Launch(s);
        }

        void Launch(int s)
        {
            var t = _turrets[s];
            var mount = OrcaCrewCore.MountPosition(N(transform.position), _pod, s, _s.HullRadius);
            var m = new OrcaMissile();
            m.Launch(mount, t.Aim, _pod.U, N(transform.right));
            _mag.Take();
            _missiles[s] = m;
            t.React = 0f;
            _retargetAt[s] = 0f;
            Play(_cfg.MissileLaunchSound, U(mount));
        }

        // the element channels, read at use time (vessel rule 2): Charge scales the missile, Space the burst
        float MissileSpeed => _s.MissileSpeed * _cfg.MissileScaleAt(_status);
        float MissileTurn => _s.MissileTurnDeg * _cfg.MissileScaleAt(_status);
        float BlastRadius => _cfg.BlastRadiusAt(_status);

        void StepMissile(int s, float dt)
        {
            var m = _missiles[s];
            var prev = U(m.Step(dt, MissileSpeed, MissileTurn));
            var pos = U(m.Position);
            CollectCrystals(pos);
            if (Contact(prev, pos) || m.FuseOut(_s.MissileLife)) Burst(s);
        }

        // what a missile's path touched this step: a rival hull, a rival missile, or a hostile prism
        bool Contact(Vector3 prev, Vector3 pos)
        {
            var own = _status.Domain;
            var players = _cfg.GameData ? _cfg.GameData.Players : null;
            if (players != null)
            {
                float r = _s.HullRadius + 2f;
                foreach (var player in players)
                {
                    if (player == null || player == _player) continue;
                    var vessel = player.Vessel;
                    if (vessel?.VesselStatus == null || !UnityLiveness.Alive(vessel.Transform) || vessel.VesselStatus.Domain == own) continue;
                    if (SegmentPointSqr(prev, pos, vessel.Transform.position) < r * r) return true;
                }
            }
            foreach (var other in Live)
            {
                if (other == this || other._status == null || other._status.Domain == own) continue;
                for (int s = 1; s < Seats; s++)
                {
                    var om = other._missiles[s];
                    if (om != null && (U(om.Position) - pos).sqrMagnitude < 36f) { other.Burst(s); return true; }
                }
            }
            var index = PrismSpatialIndex.Instance;
            if (index && index.IsAvailable)
            {
                _query.Clear();
                index.QuerySegment(prev, pos, _cfg.MissileContactRadius, _query);
                foreach (var p in _query)
                    if (p && !p.destroyed && p.Domain != own) return true;
            }
            return false;
        }

        // A flown missile collects crystals for the hull through the same paths a hull contact runs: an elemental crystal
        // through the hull's skimmer (VesselWitherLifeformByCrystalEffectSO.TakeHeart), an omni crystal through the
        // sweep entry a race ring uses (CrystalRaceRing.Thread), so its credit, explosion and respawn are the game's.
        void CollectCrystals(Vector3 pos)
        {
            float r2 = _cfg.MissileCollectRadius * _cfg.MissileCollectRadius;
            var crystals = Crystal.Active;
            for (int i = crystals.Count - 1; i >= 0; i--)
            {
                var c = crystals[i];
                if (!c || c.IsEmbedded || c.IsExploding || (c.transform.position - pos).sqrMagnitude > r2) continue;
                if (_crystalTakenAt.TryGetValue(c.GetInstanceID(), out float at) && Time.time - at < 0.5f) continue;
                _crystalTakenAt[c.GetInstanceID()] = Time.time;
                if (c.crystalProperties.Element == Element.Omni)
                {
                    if (!_impactor || !c.TryGetComponent(out OmniCrystalImpactor omni)) continue;
                    _impactor.AcceptImpacteeFromSweep(omni);
                    if (c.CrystalManager) c.CrystalManager.ReportRingThreaded(c.Id, c.transform.position, _impactor);
                }
                else if (c.crystalProperties.IsElemental && _skimmer)
                {
                    var elemental = c.GetComponentInChildren<ElementalCrystalImpactor>(true);
                    if (elemental) elemental.CollectBy(_skimmer);
                }
            }
        }

        void Burst(int s)
        {
            var m = _missiles[s];
            if (m == null) return;
            _missiles[s] = null;
            _turrets[s].Cooldown = _s.LaunchCooldown;
            var at = U(m.Position);
            var explosions = _cfg.Explosions;
            if (explosions is { Length: > 0 })
            {
                var init = new AOEExplosion.InitializeStruct
                {
                    OwnDomain = _status.Domain,
                    Vessel = _vessel,
                    AnnonymousExplosion = false,
                    OverrideMaterial = _status.AOEExplosionMaterial,
                    MaxScale = 2f * BlastRadius,
                    SpawnPosition = at,
                    SpawnRotation = Quaternion.LookRotation(U(m.Frame.F), U(m.Frame.U)),
                    DurationOverride = _cfg.BlastSeconds,
                    AffectSelfOverride = false,
                };
                ExplosionHelper.CreateExplosion(explosions, init, _impactor ? _impactor.DIContainer : null);
            }
            LastBurstAt = Time.time;
            Play(_cfg.MissileBurstSound, at);
        }

        // ------------------------------------------------------------------ AI gunners (the lab's gunnerAI, inputs only)

        bool TurretAI(int s, OrcaTurret t, float dt)
        {
            var mount = OrcaCrewCore.MountPosition(N(transform.position), _pod, s, _s.HullRadius);
            var tg = TargetFor(s, U(mount));
            if (tg.Kind == TargetKind.None) { t.React = 0f; return false; }
            if (_rng.NextDouble() < 1.5 * dt)   // the lab re-rolls its aim error 5% of its 30 Hz steps
                t.Noise = RandDir() * (Gauss() * _skill.AimNoiseDeg * OrcaCrewCore.Deg2Rad);
            var aim = OrcaCrewCore.LeadAim(mount, N(transform.position), N(tg.Position), N(tg.Velocity), _s.MissileSpeed, _skill.Lead, t.Noise);
            t.React += dt;
            bool aligned = V3.Dot(aim, t.Aim) > Mathf.Cos(15f * Mathf.Deg2Rad);
            t.SlewToward(aim, dt, _s.TurretTurnDeg);
            return aligned && t.React > _skill.ReactSeconds;
        }

        void MissileAI(int s, OrcaMissile m)
        {
            var tg = TargetFor(s, U(m.Position));
            if (tg.Kind == TargetKind.None) { m.StickYaw = m.StickPitch = m.StickRoll = 0f; return; }
            var pos = U(m.Position);
            float d = Vector3.Distance(pos, tg.Position), lt = d / Mathf.Max(1f, MissileSpeed) * _skill.Lead;
            var aim = (tg.Position + tg.Velocity * lt - pos).normalized;
            var (yaw, pitch, roll) = OrcaCrewCore.SteerToward(m.Frame, N(aim), Gauss() * _skill.SteerNoise);
            m.StickYaw = yaw; m.StickPitch = pitch; m.StickRoll = roll;
            bool burst = (tg.Kind == TargetKind.Hull && d < BlastRadius * _skill.Detonate)
                         || (tg.Kind == TargetKind.Prism && d < _cfg.BlastRadiusAtRest * 0.4f);
            if (burst && m.CanBurstByHand) Burst(s);
        }

        // The lab's bestTarget: an omni crystal when the magazine runs low, else the nearest rival hull or hostile prism,
        // else any crystal. Re-picked every aiRetargetSeconds.
        Target TargetFor(int s, Vector3 from)
        {
            if (Time.time < _retargetAt[s] && _targets[s].Kind != TargetKind.None) return Refresh(_targets[s]);
            _retargetAt[s] = Time.time + _cfg.AiRetargetSeconds;
            float reach = _s.MissileSpeed * _s.MissileLife * 0.8f, best = float.MaxValue;
            var pick = default(Target);
            void Offer(TargetKind kind, Vector3 p, Vector3 v, float cost, Transform follow = null) { if (cost < best) { best = cost; pick = new Target { Kind = kind, Position = p, Velocity = v, Follow = follow }; } }

            var crystals = Crystal.Active;
            if (_mag.Count < 4)
                for (int i = 0; i < crystals.Count; i++)
                {
                    var c = crystals[i];
                    if (!c || c.IsEmbedded || c.crystalProperties.Element != Element.Omni) continue;
                    float d = Vector3.Distance(from, c.transform.position);
                    if (d < reach) Offer(TargetKind.Omni, c.transform.position, Vector3.zero, d * (_mag.Count <= 1 ? 0.3f : 0.7f));
                }
            var players = _cfg.GameData ? _cfg.GameData.Players : null;
            if (players != null)
                foreach (var player in players)
                {
                    var vessel = player?.Vessel;
                    var st = vessel?.VesselStatus;
                    if (player == _player || st == null || !UnityLiveness.Alive(vessel.Transform) || st.Domain == _status.Domain) continue;
                    float d = Vector3.Distance(from, vessel.Transform.position);
                    if (d < reach) Offer(TargetKind.Hull, vessel.Transform.position, vessel.Transform.forward * st.Speed, d, vessel.Transform);
                }
            var index = PrismSpatialIndex.Instance;
            if (index && index.IsAvailable)
            {
                _query.Clear();
                index.QuerySphere(from, Mathf.Min(reach, 250f), _query);
                foreach (var p in _query)
                {
                    if (!p || p.destroyed || p.Domain == _status.Domain || p.prismProperties is { IsSuperShielded: true }) continue;
                    Offer(TargetKind.Prism, p.transform.position, Vector3.zero, Vector3.Distance(from, p.transform.position) * 1.1f);
                }
            }
            if (pick.Kind == TargetKind.None)
                for (int i = 0; i < crystals.Count; i++)
                {
                    var c = crystals[i];
                    if (!c || c.IsEmbedded) continue;
                    float d = Vector3.Distance(from, c.transform.position);
                    if (d < reach) Offer(TargetKind.Crystal, c.transform.position, Vector3.zero, 1e9f + d);
                }
            _targets[s] = pick;
            return pick;
        }

        // a held hull target follows its hull between re-picks; a crystal or prism stays where it was picked
        static Target Refresh(Target t)
        {
            if (t.Kind == TargetKind.Hull && UnityLiveness.Alive(t.Follow)) t.Position = t.Follow.position;
            return t;
        }

        V3 RandDir()
        {
            float z = (float)_rng.NextDouble() * 2f - 1f, a = (float)_rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt(1f - z * z);
            return new V3(r * Mathf.Cos(a), z, r * Mathf.Sin(a));
        }

        float Gauss()
        {
            double u = 0.0;
            while (u <= 0.0) u = _rng.NextDouble();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u)) * System.Math.Cos(2.0 * System.Math.PI * _rng.NextDouble()));
        }

        // ------------------------------------------------------------------ a person's input in a gunner seat

        // The turret's turn this frame (az right, el up, radians) and whether Release was pressed: mouse look, a pad's
        // sticks or W A S D at the turret rate, a touch drag; Space, the left mouse button, A or the right trigger, a tap.
        Vector2 ReadTurretAim(float dt, out bool release)
        {
            var aim = Vector2.zero;
            release = false;
            float rate = _s.TurretTurnDeg * Mathf.Deg2Rad * dt;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                aim += mouse.delta.ReadValue() * _cfg.MouseAimRadiansPerPixel;
                release |= mouse.leftButton.wasPressedThisFrame;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                aim += (pad.leftStick.ReadValue() + pad.rightStick.ReadValue()) * rate;
                release |= pad.buttonSouth.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame;
            }
            var kb = Keyboard.current;
            if (kb != null)
            {
                aim += KeyAxes(kb) * rate;
                release |= kb.spaceKey.wasPressedThisFrame;
            }
            aim += ReadTouch(out bool tap, out _) * 0.006f;
            release |= tap;
            return aim;
        }

        // A flown missile's stick (yaw right, pitch up, each -1..1) and whether Release was pressed again.
        Vector2 ReadMissileStick(out bool release)
        {
            var stick = Vector2.zero;
            release = false;
            var pad = Gamepad.current;
            if (pad != null)
            {
                stick = Bigger(stick, pad.leftStick.ReadValue());
                stick = Bigger(stick, pad.rightStick.ReadValue());
                release |= pad.buttonSouth.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame;
            }
            var kb = Keyboard.current;
            if (kb != null)
            {
                stick = Bigger(stick, KeyAxes(kb));
                release |= kb.spaceKey.wasPressedThisFrame;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                stick = Bigger(stick, Vector2.ClampMagnitude(mouse.delta.ReadValue() * 0.05f, 1f));
                release |= mouse.leftButton.wasPressedThisFrame;
            }
            ReadTouch(out bool tap, out var drag);
            stick = Bigger(stick, Vector2.ClampMagnitude(drag / Mathf.Max(1f, Screen.height * 0.08f), 1f));
            release |= tap;
            return Vector2.ClampMagnitude(stick, 1f);
        }

        static Vector2 Bigger(Vector2 a, Vector2 b) => b.sqrMagnitude > a.sqrMagnitude ? b : a;

        static Vector2 KeyAxes(Keyboard kb) => new(
            (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f),
            (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f));

        // The primary touch: its movement this frame (px), whether it was a short tap that just ended, and its offset from
        // where it started (the missile's stick).
        Vector2 ReadTouch(out bool tap, out Vector2 drag)
        {
            tap = false;
            drag = Vector2.zero;
            var ts = Touchscreen.current;
            if (ts == null) return Vector2.zero;
            var touch = ts.primaryTouch;
            if (ts.touches.Count > 1 && ts.touches[1].press.isPressed) { _touchId = int.MinValue; return Vector2.zero; }
            if (touch.press.isPressed)
            {
                int id = touch.touchId.ReadValue();
                var p = touch.position.ReadValue();
                if (id != _touchId) { _touchId = id; _touchStart = Time.unscaledTime; _touchFrom = p; _touchMoved = false; return Vector2.zero; }
                drag = p - _touchFrom;
                if (drag.sqrMagnitude > 20f * 20f) _touchMoved = true;
                return touch.delta.ReadValue();
            }
            if (_touchId != int.MinValue)
            {
                tap = !_touchMoved && Time.unscaledTime - _touchStart < 0.25f;
                _touchId = int.MinValue;
            }
            return Vector2.zero;
        }

        // ------------------------------------------------------------------ helpers

        OrcaFrame HullFrame() => new(N(transform.forward), N(transform.up));

        internal static V3 N(Vector3 v) => new(v.x, v.y, v.z);
        internal static Vector3 U(V3 v) => new(v.X, v.Y, v.Z);

        static float SegmentPointSqr(Vector3 a, Vector3 b, Vector3 p)
        {
            var ab = b - a;
            float l2 = ab.sqrMagnitude, t = l2 > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2) : 0f;
            return (a + ab * t - p).sqrMagnitude;
        }

        static void Play(EventReference evt, Vector3 at)
        {
            if (evt.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(evt, at);
        }
    }
}
