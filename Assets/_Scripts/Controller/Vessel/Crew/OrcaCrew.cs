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
    /// The Orca's crew: the 4-seat vessel (Crew of Four Lab round 6; <c>Docs/Studios/CREW_OF_FOUR_LAB.md</c>, design
    /// <c>ORCA_CREW.md</c> beside this file). Four seats on one hull, always filled: a person takes one and an AI flies
    /// every other (Garrett, 2026-10-20: "this vessel always has 4 players (AI if not people)").
    /// <list type="bullet">
    /// <item><b>Helm</b> (seat 0): flies the hull with two thumbs (the hull's own <c>VesselTransformer</c>), boosts on the
    /// right trigger while a tank lasts, and raises a <b>shield</b> on the left trigger: prisms on a shell round the hull
    /// with the forward cone open, riding with it as SUPER-SHIELDED prisms for a few seconds, then each turning into a
    /// danger prism where it is and staying (round 5, Garrett 2026-10-21).</item>
    /// <item><b>Gunners</b> (seats 1-3): first-person turrets on outriggers, gyro-stabilized (round 4: the aim lives in
    /// the world, the view's horizon is held and eased back to the hull's up). Each seat has a <b>gun</b> and its own
    /// <b>missiles</b> (round 5). Each seat is an element (round 6): the helm Time, the sniper Space, the fast gun Mass,
    /// the bomb Charge, and the person's seat lights that ability icon. Fire shoots the seat's gun: the sniper is the
    /// Serpent's sniper shot, always scoped (every hostile prism in a thin cone dies, super-shielded ones included); the
    /// fast gun is the Brittle Star's rounds (overheats), and some of them leave a prism where they reach the pen's paper,
    /// laid as clean strokes (<see cref="OrcaPen"/>); the bomb is the Grizzly's without its self-launch, with no cooldown
    /// but one at a time: a press throws it, the next bursts it. Missile launches one of the seat's missiles and hands the gunner its
    /// controls; it bursts on contact, at its 5 s fuse or on a second press (<c>AOEExplosion</c>), and collects any
    /// crystal it flies through for the hull. An omni crystal gives every seat a missile
    /// (<see cref="OrcaOmniCrystalEffectSO"/>).</item>
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
        [Tooltip("The Mass seat's fast gun: a Gun with its own pool of the Sparrow's rounds (OrcaFastGun on the prefab). The rounds' own impact container damages prisms and scores bullet hits.")]
        [SerializeField] Gun fastGun;
        [Tooltip("The Charge seat's bomb gun: a Gun with its own pool of the Grizzly's bombs (OrcaBombGun on the prefab).")]
        [SerializeField] Gun bombGun;

        public const int SniperSeat = 1, FastGunSeat = 2, BombSeat = 3;

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
        readonly OrcaMagazine[] _mags = new OrcaMagazine[Seats];
        readonly float[] _gunCd = new float[Seats];
        readonly OrcaFastGun _fast = new();
        Projectile _bomb;
        float _bombAge, _bombLastD = -1f;
        Vector3 _bombPrev;
        bool _bombStruck;
        readonly List<Transform> _vesselScratch = new();
        readonly List<int> _virtualIds = new();
        readonly List<Prism> _dropped = new();
        readonly List<float> _droppedBorn = new();
        readonly List<float> _droppedUntil = new();
        float _droppedCheckAt;
        readonly float[] _burstAt = { -99f, -99f, -99f, -99f };

        // the fast gun's pen (round 6): rounds that will reach the paper, the stroke, and the prisms it has laid
        struct PenShot { public Vector3 At, Aim, Across, Up; public float Due; }
        readonly List<PenShot> _penShots = new();
        readonly OrcaPen _pen = new();
        readonly List<OrcaPenMark> _penMarks = new();
        readonly List<Prism> _penned = new();
        readonly List<float> _pennedBorn = new();
        float _penReadyAt;
        int _penLaid, _penStrokeSeen = -1;
        Trail _penTrail;

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
        float _lastTapAt = -9f;
        Vector2 _lastTapAt2;

        // ------------------------------------------------------------------ read by the HUD and the view

        public OrcaCrewConfigSO Config => _cfg ? _cfg : config;
        /// <summary>True while the local person rides this hull (and the HUD should read it).</summary>
        public bool HumanAboard => _mode == Mode.LocalHuman;
        public int HumanSeat => _humanSeat;
        /// <summary>The missiles the HUD shows: the person's own seat's, or all three seats' at the helm.</summary>
        public int Missiles => _humanSeat > Helm ? MissilesAt(_humanSeat) : MissilesAt(1) + MissilesAt(2) + MissilesAt(3);
        public int MissilesAt(int seat) => seat > Helm && seat < Seats ? _mags[seat]?.Count ?? 0 : 0;
        public float BoostTank01 => _cfg && _cfg.BoostTank > 0f ? Mathf.Clamp01(_tank / _cfg.BoostTank) : 0f;
        public float ShieldCooldown01 => _cfg && _cfg.ShieldCooldown > 0f ? Mathf.Clamp01((_shieldReadyAt - Time.time) / _cfg.ShieldCooldown) : 0f;
        public bool ShieldUp => _shield.Count > 0;

        /// <summary>A seat's gun, named and with its state, from the config's authored words (the HUD's {gun}).</summary>
        public string GunLine(int seat)
        {
            var cfg = Config;
            if (!cfg || !cfg.GunsOn || seat <= Helm || seat >= Seats || string.IsNullOrEmpty(cfg.GunLineTemplate)) return string.Empty;
            string state = seat == FastGunSeat ? (_fast.Hot ? cfg.GunHotText : cfg.GunReadyText)
                : seat == BombSeat ? (_bomb ? cfg.BombOutText : cfg.GunReadyText)
                : _gunCd[seat] > 0f ? cfg.GunCooldownTemplate.Replace("{seconds}", Mathf.CeilToInt(_gunCd[seat]).ToString())
                : cfg.GunReadyText;
            return cfg.GunLineTemplate.Replace("{name}", cfg.GunName(seat)).Replace("{state}", state);
        }
        /// <summary>A number that changes exactly when <see cref="GunLine"/>'s text would, so the HUD rebuilds the line only
        /// then (no per-frame strings): -1 overheated, -2 a bomb out, else the whole seconds left to cool (0 = ready).</summary>
        public int GunStateKey(int seat) => seat == FastGunSeat ? (_fast.Hot ? -1 : 0)
            : seat == BombSeat && _bomb ? -2
            : seat > Helm && seat < Seats && _gunCd[seat] > 0f ? Mathf.CeilToInt(_gunCd[seat]) : 0;
        /// <summary>The fast gun's heat (0-1) and whether it is locked out, for the HUD.</summary>
        public float FastGunHeat01 => _fast.Heat;
        /// <summary>When a missile last burst / the shield was last raised (Time.time), for the HUD's flashes.</summary>
        public float LastBurstAt { get; private set; } = -99f;
        /// <summary>When a seat's missile (or the bomb seat's bomb) last burst (Time.time): its own icon flashes.</summary>
        public float BurstAt(int seat) => seat >= 0 && seat < Seats ? _burstAt[seat] : -99f;
        /// <summary>The element a seat owns (round 6): its ability icon.</summary>
        public Element SeatElement(int seat) => Config ? Config.SeatElement(seat) : Element.Time;
        /// <summary>A seat's recharge on its icon's veil (1 just spent, 0 ready): the helm's boost tank, the sniper's
        /// cooldown, the fast gun's heat (full while locked out); the bomb has none.</summary>
        public float SeatVeil01(int seat) => seat switch
        {
            Helm => 1f - BoostTank01,
            SniperSeat => _cfg && _cfg.SniperCooldown > 0f ? Mathf.Clamp01(_gunCd[SniperSeat] / _cfg.SniperCooldown) : 0f,
            FastGunSeat => _fast.Hot ? 1f : _fast.Heat,
            _ => 0f,
        };
        /// <summary>A bomb is in the air (the bomb seat's next press bursts it).</summary>
        public bool BombOut => _bomb;
        /// <summary>The fast gun's next round is a pen round.</summary>
        public bool PenReady => _cfg && _cfg.FastGunPenRate > 0f && !_fast.Hot && Time.time >= _penReadyAt;
        public float ShieldRaisedAt { get; private set; } = -99f;

        internal IVesselStatus Status => _status;
        internal Transform Hull => _vessel?.Transform;
        internal OrcaFrame Pod => _pod;
        internal OrcaTurret TurretOf(int seat) => _turrets[seat];
        internal OrcaMissile MissileOf(int seat) => _missiles[seat];
        internal Transform BombTransform => _bomb ? _bomb.transform : null;
        internal float BombBlastRadius => _cfg && _status != null ? _cfg.BombBlastRadiusAt(_status) : 0f;
        internal float SniperCooldownLeft => _gunCd[SniperSeat];

        /// <summary>The hull collected an Omni crystal (its own contact, or a missile's): every gunner seat gets a missile.</summary>
        public void OnOmniCrystal()
        {
            if (!_cfg) return;
            int added = 0;
            for (int s = 1; s < Seats; s++) if (_mags[s] != null) added += _mags[s].AddOmni(_cfg.OmniMissiles);
            if (added > 0) Play(_cfg.OmniRefillSound, transform.position);
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
            for (int s = 1; s < Seats; s++) { _mags[s] = new OrcaMagazine(_cfg.StartMissiles, _cfg.MaxMissiles); _gunCd[s] = 0f; }
            _fast.Reset();
            _bomb = null; _bombStruck = false;
            _penShots.Clear(); _pen.Reset(); _penned.Clear(); _pennedBorn.Clear(); _penReadyAt = 0f; _penStrokeSeen = -1; _penTrail = null;
            for (int s = 0; s < Seats; s++) _burstAt[s] = -99f;
            if (fastGun) fastGun.Initialize(_status);
            if (bombGun) bombGun.Initialize(_status);
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
            RetireDropped();
            RetireBomb();
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
            StepBomb(dt);
            StepPen();
            if (_dropped.Count > 0 && Time.time >= _droppedCheckAt) { _droppedCheckAt = Time.time + 0.5f; ExpireDropped(); }
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
                // the lab's other shield keys: E, and the pad's B
                shieldEdge |= (Keyboard.current?.eKey.wasPressedThisFrame ?? false) || (Gamepad.current?.buttonEast.wasPressedThisFrame ?? false);
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

            if (_shield.Count > 0 && Time.time >= _shieldEndsAt) EndCarry();
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
            EndCarry();
            var channel = _status.VesselPrismController ? _status.VesselPrismController.PrismSpawnChannel : null;
            if (!channel)
            {
                CSDebug.LogWarning("[OrcaCrew] The hull has no prism spawn channel; the shield cannot be laid.");
                return;
            }
            OrcaCrewCore.ShieldDirections(_cfg.ShieldBlocks, _cfg.ShieldOpenDeg, _dirs);
            string owner = _status.PlayerName + ShieldTag;
            var hull = transform;
            for (int k = 0; k < _dirs.Count; k++)
            {
                float radius = Mathf.Lerp(_cfg.ShieldMinRadius, _cfg.ShieldMaxRadius, (float)_rng.NextDouble());
                var local = U(_dirs[k]) * radius;
                var world = hull.TransformDirection(local);
                var prism = BoostRingBuilder.LayOne(channel, hull.position + world, Quaternion.LookRotation(world, hull.up),
                    _cfg.ShieldBlockScale, _cfg.ShieldSuper ? PrismKind.SuperShielded : PrismKind.Danger, _status.Domain, _status.PlayerName, owner + k);
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
        bool StillOurs(int i) => StillOurs(_shield[i], _shieldBorn[i]);

        const string ShieldTag = "::OrcaShield::", PenTag = "::OrcaPen::";

        static bool StillOurs(Prism p, float born, string tag = ShieldTag) =>
            p && !p.destroyed && p.gameObject.activeInHierarchy && p.prismProperties != null
            && Mathf.Approximately(p.prismProperties.TimeCreated, born)
            && p.ownerID != null && p.ownerID.Contains(tag);

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

        // The carry ends (round 5): each prism turns into a danger prism where it stands and stays for shieldLinger
        // (PrismKinds.Retheme, the reversible re-theme), or, with shieldSuper off, round 4's shell simply goes.
        void EndCarry()
        {
            if (_shield.Count == 0) return;
            if (!_cfg.ShieldSuper) { RetireShield(); return; }
            float until = _cfg.ShieldLinger > 0f ? Time.time + _cfg.ShieldLinger : float.PositiveInfinity;
            for (int i = 0; i < _shield.Count; i++)
            {
                if (!StillOurs(i)) continue;
                PrismKinds.Retheme(_shield[i], PrismKind.Danger);
                _dropped.Add(_shield[i]); _droppedBorn.Add(_shieldBorn[i]); _droppedUntil.Add(until);
            }
            _shield.Clear(); _shieldBorn.Clear(); _shieldLocal.Clear();
        }

        void ExpireDropped()
        {
            for (int i = _dropped.Count - 1; i >= 0; i--)
            {
                bool ours = StillOurs(_dropped[i], _droppedBorn[i]);
                if (ours && Time.time < _droppedUntil[i]) continue;
                if (ours) { PrismKinds.Clear(_dropped[i]); _dropped[i].ReturnToPool(); }
                _dropped.RemoveAt(i); _droppedBorn.RemoveAt(i); _droppedUntil.RemoveAt(i);
            }
        }

        void RetireDropped()
        {
            for (int i = 0; i < _dropped.Count; i++)
                if (StillOurs(_dropped[i], _droppedBorn[i])) { PrismKinds.Clear(_dropped[i]); _dropped[i].ReturnToPool(); }
            _dropped.Clear(); _droppedBorn.Clear(); _droppedUntil.Clear();
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

            bool fire, gunHold, gunEdge;
            if (human)
            {
                var aim = ReadTurretAim(dt, out fire, out gunHold, out gunEdge);
                t.TurnBy(aim.x, aim.y);
            }
            else { fire = TurretAI(s, t, dt, out gunHold); gunEdge = false; }
            t.HoldHorizon(N(transform.up), dt, _s.HorizonSmooth);
            if (_cfg.GunsOn) StepGun(s, dt, gunHold, gunEdge, human);

            if (fire)
            {
                t.Release(human, dt);
                if (human && _mags[s].Count <= 0) Play(_cfg.EmptyMagazineSound, transform.position);
            }
            if (t.Step(dt, _mags[s].CanTake)) Launch(s);
        }

        void Launch(int s)
        {
            var t = _turrets[s];
            var mount = OrcaCrewCore.MountPosition(N(transform.position), _pod, s, _s.HullRadius);
            var m = new OrcaMissile();
            m.Launch(mount, t.Aim, _pod.U, N(transform.right));
            _mags[s].Take();
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
            LastBurstAt = _burstAt[s] = Time.time;
            Play(_cfg.MissileBurstSound, at);
        }

        // ------------------------------------------------------------------ the guns (round 5: one per seat, each different)

        bool GunReady(int s) => s == FastGunSeat ? !_fast.Hot : s == BombSeat ? !_bomb : _gunCd[s] <= 0f;   // round 6: the bomb has no cooldown, one in the air

        float GunRange(int s) => s == SniperSeat ? Mathf.Min(_cfg.SniperRange, 600f)
            : s == FastGunSeat ? _cfg.FastGunRoundSpeed * 2f * _cfg.FastGunRoundSeconds / Mathf.PI * 0.8f
            : _cfg.BombSpeed * _cfg.BombFuse * 0.6f;

        /// <summary>The lab's <c>stepGun</c>: the sniper fires when ready; the fast gun fires while held until it
        /// overheats; the bomb has no cooldown but one at a time: a press throws it, the next press bursts it (an AI bursts
        /// its own, <see cref="AIBurstsBomb"/>).</summary>
        void StepGun(int s, float dt, bool hold, bool edge, bool human)
        {
            if (_gunCd[s] > 0f) _gunCd[s] -= dt;
            Vector3 mount = U(OrcaCrewCore.MountPosition(N(transform.position), _pod, s, _s.HullRadius));
            Vector3 muzzleTip = default;
            bool fromBarrel = _view != null && _view.Rig.TryMuzzle(s, out muzzleTip);   // the model's barrel tip, when it draws one
            Vector3 aim = U(_turrets[s].Aim);
            switch (s)
            {
                case SniperSeat:
                    if ((hold || edge) && _gunCd[s] <= 0f) { Snipe(fromBarrel ? muzzleTip : mount, aim); _gunCd[s] = _cfg.SniperCooldown; }
                    break;
                case FastGunSeat:
                    int n = _fast.Step(hold, dt, _cfg.FastGunRate, _cfg.FastGunHeatSeconds, _cfg.FastGunCoolSeconds);
                    for (int k = 0; k < n && k < 4; k++) FireRound(fromBarrel ? muzzleTip - aim * 3f : mount, aim);   // a hitch never discharges as a burst (vessel rule 17)
                    break;
                case BombSeat:
                    if (_bomb) { if (human ? edge : AIBurstsBomb()) DetonateBomb(); }
                    else if (human ? edge : hold) ThrowBomb(fromBarrel ? muzzleTip - aim * 4f : mount, aim);
                    break;
            }
        }

        Vector3 HullVelocity => _status.Course * _status.Speed;

        // The sniper: the Serpent's sniper shot (SniperShotActionExecutor.ResolveShot) fired from the turret along its aim.
        // Every hostile prism in the cone dies (pierce unlimited), super-shielded ones included (the sanctioned order:
        // DeactivateShields, then a devastating Damage); every rival hull in the cone is stripped, a scored hit.
        void Snipe(Vector3 origin, Vector3 direction)
        {
            float range = _cfg.SniperRange, cone = _cfg.SniperConeDeg, minR = _cfg.SniperMinPathRadius;
            Vector3 stop = origin + direction * range;
            bool hit = false;
            var index = PrismSpatialIndex.EnsureInstance();
            if (index != null && index.IsAvailable)
            {
                _query.Clear();
                index.QueryCone(origin, direction, range, cone, minR, _query);
                float far = -1f;
                for (int i = 0; i < _query.Count; i++)
                {
                    var p = _query[i];
                    if (!p || p.destroyed || p.Domain == _status.Domain) continue;
                    float along = Vector3.Dot(p.transform.position - origin, direction);
                    if (along > far) { far = along; stop = p.transform.position; }
                    DestroyPrism(p, direction);
                    hit = true;
                }
                if (index.VirtualCount > 0)   // swarm members that are only data (Docs/SWARM_FAUNA.md §19), as the Serpent does
                {
                    index.QueryConeVirtualIds(origin, direction, range, cone, minR, _virtualIds);
                    for (int i = 0; i < _virtualIds.Count; i++)
                    {
                        if (!index.TryGetVirtualEntry(_virtualIds[i], out _, out var domain, out _) || domain == _status.Domain) continue;
                        var p = index.ResolvePrism(_virtualIds[i], materialise: true);
                        if (!p || p.destroyed || p.Domain == _status.Domain) continue;
                        DestroyPrism(p, direction);
                        hit = true;
                    }
                }
            }
            StripVessels(origin, direction, range);
            var colorSet = _cfg.GameData && _cfg.GameData.ThemeManagerData ? _cfg.GameData.ThemeManagerData.ColorSet : null;
            var colour = colorSet != null ? colorSet.GetDomainSignalColor(_status.Domain) : Color.white;
            float distance = Vector3.Distance(origin, stop);
            float endRadius = Mathf.Max(minR, distance * Mathf.Tan(cone * Mathf.Deg2Rad));
            SniperBeam.Fire(origin, stop, _cfg.SniperBeamStartWidth, endRadius * 2f, colour, _cfg.SniperBeamSeconds,
                _cfg.SniperFlareRadius, _cfg.SniperFlareSeconds, hit);
            Play(_cfg.SniperShotSound, origin);
        }

        void DestroyPrism(Prism prism, Vector3 direction)
        {
            Vector3 impact = direction * _cfg.SniperDebrisSpeed;
            if (prism.prismProperties is { IsSuperShielded: true }) prism.DeactivateShields(impact, _cfg.SniperDebrisSpeedLimit);
            prism.Damage(impact, _status.Domain, _status.PlayerName, devastate: true, debrisSpeedLimit: _cfg.SniperDebrisSpeedLimit);
        }

        // The Serpent's StripVessels: every opposing pilot in the cone loses sniperStripPerElement of each element, ejected
        // as crystals, a scored hit - decided on this crew's machine, settled by the victim's owner.
        void StripVessels(Vector3 origin, Vector3 direction, float reach)
        {
            if (_cfg.SniperStripPerElement <= 0f) return;
            float tanHalf = Mathf.Tan(Mathf.Deg2Rad * Mathf.Clamp(_cfg.SniperConeDeg, 0f, 89f));
            bool decidedHere = ElementalTransfer.IsDecidedHere(_status);
            VesselVisionShading.CollectStampedVessels(_vesselScratch);
            for (int i = 0; i < _vesselScratch.Count; i++)
            {
                var candidate = _vesselScratch[i];
                if (candidate == null) continue;
                var component = candidate.GetComponentInChildren<VesselStatus>();
                if (component == null) continue;
                IVesselStatus victim = component;
                if (ReferenceEquals(victim, _status) || victim.Domain == _status.Domain) continue;
                if (!PrismSpatialIndex.ConeContains(component.transform.position, origin, direction, reach, tanHalf, _cfg.SniperMinPathRadius)) continue;
                if (!decidedHere || !CombatHitDrain.TryAdmit(victim, _status, CombatHitClass.Debuff, _cfg.SniperSameVictimCooldown,
                        ElementalDebuffSources.Other, out int supersededRank))
                    continue;
                ElementalTransfer.ApplyAllAuthoritative(ElementalTransferForm.Eject, victim, _status, _cfg.SniperStripPerElement,
                    direction * _cfg.SniperEjectSpeed, ElementalDebuffSources.Other);
                if (_cfg.OnCombatHitLanded)
                    _cfg.OnCombatHitLanded.Raise(new CombatHitStats
                    {
                        ShooterName = _status.PlayerName,
                        VictimName = victim.PlayerName,
                        HitClass = CombatHitClass.Debuff,
                        SupersededRank = supersededRank,
                    });
            }
            _vesselScratch.Clear();
        }

        // The fast gun: the Brittle Star's round (BrittleStarExecutor.FireAlong) from the turret along its aim. The round's
        // own impact container damages prisms and scores bullet hits on rival hulls; our own domain's prisms (the trail,
        // the shield) are spared.
        void FireRound(Vector3 mount, Vector3 aim)
        {
            if (!fastGun) return;
            var muzzle = fastGun.transform;
            muzzle.SetPositionAndRotation(mount + aim * 3f, Quaternion.LookRotation(aim, transform.up));
            var velocity = HullVelocity;
            fastGun.FireGun(muzzle, _cfg.FastGunRoundSpeed, velocity, _cfg.FastGunRoundScale, true, _cfg.FastGunRoundSeconds, 0f,
                FiringPatterns.Default, 0, detachAfterSpawn: true, stopOnFirstPrismImpact: true, spareOwnDomain: true,
                aimDirection: aim, flightGrowthFactor: _cfg.FastGunRoundGrowth);
            QueuePen(muzzle.position, aim, aim * _cfg.FastGunRoundSpeed + velocity);
        }

        // The pen (round 6): at most fastGunPenRate rounds a second also leave a prism where they reach the paper. Where and
        // when is fixed at the press (the round's own cosine-eased path, OrcaPen.Landing), so the stroke is the gunner's
        // sweep whatever the round meets - and it needs nothing from the pooled round, which may be reissued before then.
        void QueuePen(Vector3 from, Vector3 aim, Vector3 velocity)
        {
            if (_cfg.FastGunPenRate <= 0f || Time.time < _penReadyAt || velocity.sqrMagnitude < 1e-4f) return;
            _penReadyAt = Time.time + 1f / _cfg.FastGunPenRate;
            var (distance, seconds) = OrcaPen.Landing(velocity.magnitude, _cfg.FastGunRoundSeconds, _cfg.FastGunPenReach);
            var up = U(_turrets[FastGunSeat].Up);
            var right = Vector3.Cross(up, aim);
            _penShots.Add(new PenShot { At = from + velocity.normalized * distance, Aim = aim, Across = (right + up).normalized, Up = up, Due = Time.time + seconds });
        }

        // Lay the strokes the pen rounds that reached their paper this frame make (the lab's stepPens), and the dot a pause
        // leaves. Ordinary prisms of our domain, one Trail per stroke; the oldest goes once the hull keeps fastGunPenCap.
        void StepPen()
        {
            int due = 0;
            for (int i = 0; i < _penShots.Count; i++) if (_penShots[i].Due <= Time.time) due++;
            if (due > 0)
            {
                _penShots.Sort((a, b) => a.Due.CompareTo(b.Due));   // a round that inherits more speed reaches its paper sooner
                for (int i = 0; i < due; i++)
                {
                    var shot = _penShots[i];
                    _pen.Lay(N(shot.At), N(shot.Aim), N(shot.Across), Time.time, _cfg.FastGunPenSpacing, _cfg.FastGunPenGap, _cfg.FastGunPenFill, _penMarks);
                    for (int k = 0; k < _penMarks.Count; k++) PenPrism(_penMarks[k], shot.Up);
                }
                _penShots.RemoveRange(0, due);
            }
            if (_pen.Flush(Time.time, _cfg.FastGunPenGap, out var dot)) PenPrism(dot, transform.up);
        }

        void PenPrism(OrcaPenMark mark, Vector3 up)
        {
            var channel = _status.VesselPrismController ? _status.VesselPrismController.PrismSpawnChannel : null;
            if (!channel) return;
            if (_pen.Strokes != _penStrokeSeen) { _penStrokeSeen = _pen.Strokes; _penTrail = new Trail(); }
            var along = U(mark.Along);
            var rotation = Vector3.Cross(along, up).sqrMagnitude > 1e-6f ? Quaternion.LookRotation(along, up) : Quaternion.LookRotation(along);
            var prism = BoostRingBuilder.LayOne(channel, U(mark.Position), rotation, _cfg.FastGunPenScale, PrismKind.Plain, _status.Domain,
                _status.PlayerName, _status.PlayerName + PenTag + _penLaid++, _penTrail);
            if (!prism) return;
            _penned.Add(prism);
            _pennedBorn.Add(prism.prismProperties.TimeCreated);
            while (_penned.Count > _cfg.FastGunPenCap)
            {
                if (StillOurs(_penned[0], _pennedBorn[0], PenTag)) _penned[0].ReturnToPool();
                _penned.RemoveAt(0); _pennedBorn.RemoveAt(0);
            }
        }

        // The bomb: the Grizzly's trigger bomb, cruising (Projectile.Cruises) from the turret along its aim. It bursts on a
        // rival hull or a hostile prism, on a person's second press, or at its fuse - and never knocks our own hull about:
        // the Grizzly's self-launch is GrizzlyTriggerBombExecutor.LaunchSelf, which is never called, and the blast spares
        // its owner (AffectSelfOverride off).
        void ThrowBomb(Vector3 mount, Vector3 aim)
        {
            if (!bombGun) return;
            var muzzle = bombGun.transform;
            muzzle.SetPositionAndRotation(mount + aim * 4f, Quaternion.LookRotation(aim, transform.up));
            bombGun.FireGun(muzzle, _cfg.BombSpeed, HullVelocity, _cfg.BombScale, true, 1f, 0.5f, FiringPatterns.Default, 0,
                detachAfterSpawn: true, stopOnFirstPrismImpact: false, spareOwnDomain: true, aimDirection: aim);
            var shot = bombGun.LastProjectile;
            if (!shot) return;
            shot.Cruises = true;   // after FireGun: the loop's first step runs inside the fire call (vessel rule 34)
            if (shot.TryGetComponent(out GrizzlyBombVisual visual)) visual.Arm(_cfg.BombColor, _status.Domain);
            shot.VesselStruck += OnBombStruck;
            shot.FlightEnded += OnBombFlightEnded;
            _bomb = shot;
            _bombAge = 0f;
            _bombLastD = -1f;
            _bombPrev = shot.transform.position;
            _bombStruck = false;
            Play(_cfg.BombThrowSound, mount);
        }

        // A hull the bomb swept into: a rival's freezes it there to burst next step (never from inside the sweep).
        void OnBombStruck(Projectile shot, VesselImpactor hit)
        {
            if (shot != _bomb || hit == null) return;
            var victim = hit.Vessel?.VesselStatus;
            if (victim == null || ReferenceEquals(victim, _status) || victim.Domain == _status.Domain) return;
            shot.Freeze();
            _bombStruck = true;
        }

        // The pool took the bomb back (a turn end, a teardown): no blast.
        void OnBombFlightEnded(Projectile shot, bool _)
        {
            if (shot != _bomb) return;
            Unhook(shot);
            _bomb = null;
        }

        void StepBomb(float dt)
        {
            if (!_bomb) return;
            if (!_bomb.gameObject.activeInHierarchy) { Unhook(_bomb); _bomb = null; return; }
            _bombAge += dt;
            var pos = _bomb.transform.position;
            if (_bombStruck || _bombAge >= _cfg.BombFuse || Contact(_bombPrev, pos)) { DetonateBomb(); return; }
            _bombPrev = pos;
        }

        // The bomb seat's AI bursts its bomb (the lab's aiBurst): a rival hull inside the blast, the bomb at its target, or
        // past it - a bomb nobody bursts would hold the seat's only throw.
        bool AIBurstsBomb()
        {
            var at = _bomb.transform.position;
            float radius = _cfg.BombBlastRadiusAt(_status);
            var players = _cfg.GameData ? _cfg.GameData.Players : null;
            if (players != null)
                foreach (var player in players)
                {
                    var vessel = player?.Vessel;
                    var st = vessel?.VesselStatus;
                    if (player == _player || st == null || !UnityLiveness.Alive(vessel.Transform) || st.Domain == _status.Domain) continue;
                    if (Vector3.Distance(vessel.Transform.position, at) < radius * _skill.Detonate + _s.HullRadius) return true;
                }
            var tg = _targets[BombSeat];
            if (tg.Kind == TargetKind.None) return _bombAge > 2f;
            float d = Vector3.Distance(at, Refresh(tg).Position);
            bool passed = _bombLastD >= 0f && d > _bombLastD + 0.01f;
            _bombLastD = d;
            return d < radius * 0.5f || passed || _bombAge > 4f;
        }

        void DetonateBomb()
        {
            var shot = _bomb;
            if (!shot) return;
            _bomb = null;
            Unhook(shot);
            Vector3 at = shot.transform.position;
            Quaternion rot = shot.transform.rotation;
            shot.ReturnToFactory();
            var explosions = _cfg.BombExplosions;
            if (explosions is { Length: > 0 })
            {
                var init = new AOEExplosion.InitializeStruct
                {
                    OwnDomain = _status.Domain,
                    Vessel = _vessel,
                    AnnonymousExplosion = false,
                    OverrideMaterial = _status.AOEExplosionMaterial,
                    MaxScale = 2f * _cfg.BombBlastRadiusAt(_status),
                    SpawnPosition = at,
                    SpawnRotation = rot,
                    AffectSelfOverride = false,
                };
                ExplosionHelper.CreateExplosion(explosions, init, _impactor ? _impactor.DIContainer : null);
            }
            LastBurstAt = _burstAt[BombSeat] = Time.time;
            Play(_cfg.BombBurstSound, at);
        }

        void RetireBomb()
        {
            var shot = _bomb;
            _bomb = null;
            if (!shot) return;
            Unhook(shot);
            if (shot.gameObject.activeInHierarchy) shot.ReturnToFactory();
        }

        void Unhook(Projectile shot)
        {
            if (!shot) return;
            shot.VesselStruck -= OnBombStruck;
            shot.FlightEnded -= OnBombFlightEnded;
        }

        // ------------------------------------------------------------------ AI gunners (the lab's gunnerAI, inputs only)

        // Round 5: the gun when the target is in its range and it is ready, else a missile (always for a crystal, which only a
        // missile collects). Returns whether to launch a missile; gunHold whether to fire the gun.
        bool TurretAI(int s, OrcaTurret t, float dt, out bool gunHold)
        {
            gunHold = false;
            var mount = OrcaCrewCore.MountPosition(N(transform.position), _pod, s, _s.HullRadius);
            float reach = _s.MissileSpeed * _s.MissileLife * 0.8f, gr = _cfg.GunsOn ? GunRange(s) : 0f;
            var tg = TargetFor(s, U(mount), Mathf.Max(reach, gr), _mags[s].Count);
            if (tg.Kind == TargetKind.None) { t.React = 0f; return false; }
            if (_rng.NextDouble() < 1.5 * dt)   // the lab re-rolls its aim error 5% of its 30 Hz steps; a sniper steadies its aim
                t.Noise = RandDir() * (Gauss() * _skill.AimNoiseDeg * OrcaCrewCore.Deg2Rad * (s == SniperSeat ? 0.4f : 1f));
            float dm = Vector3.Distance(U(mount), tg.Position);
            bool crystal = tg.Kind == TargetKind.Omni || tg.Kind == TargetKind.Crystal;
            bool useGun = _cfg.GunsOn && !crystal && dm < gr && GunReady(s);
            float speed = useGun ? (s == FastGunSeat ? _cfg.FastGunRoundSpeed : s == BombSeat ? _cfg.BombSpeed : 1e9f) : _s.MissileSpeed;
            var aim = OrcaCrewCore.LeadAim(mount, N(transform.position), N(tg.Position), N(tg.Velocity), speed, _skill.Lead, t.Noise);
            t.React += dt;
            float tol = useGun && s == SniperSeat ? 4f : useGun && s == FastGunSeat ? 8f : 15f;
            bool ok = V3.Dot(aim, t.Aim) > Mathf.Cos(tol * Mathf.Deg2Rad) && t.React > _skill.ReactSeconds;
            t.SlewToward(aim, dt, _s.TurretTurnDeg);
            gunHold = useGun && ok;
            return !useGun && ok && _mags[s].Count > 0 && dm < reach && (crystal || !_cfg.GunsOn || dm >= gr * 0.6f || !GunReady(s));
        }

        void MissileAI(int s, OrcaMissile m)
        {
            var tg = TargetFor(s, U(m.Position), _s.MissileSpeed * _s.MissileLife * 0.8f, _mags[s].Count + 1);
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

        // The lab's bestTarget: an omni crystal when the seat's magazine runs low, else the nearest rival hull or hostile
        // prism, else any crystal. Re-picked every aiRetargetSeconds.
        Target TargetFor(int s, Vector3 from, float reach, int mag)
        {
            if (Time.time < _retargetAt[s] && _targets[s].Kind != TargetKind.None) return Refresh(_targets[s]);
            _retargetAt[s] = Time.time + _cfg.AiRetargetSeconds;
            float best = float.MaxValue;
            var pick = default(Target);
            void Offer(TargetKind kind, Vector3 p, Vector3 v, float cost, Transform follow = null) { if (cost < best) { best = cost; pick = new Target { Kind = kind, Position = p, Velocity = v, Follow = follow }; } }

            var crystals = Crystal.Active;
            if (mag > 0 && mag < 2)
                for (int i = 0; i < crystals.Count; i++)
                {
                    var c = crystals[i];
                    if (!c || c.IsEmbedded || c.crystalProperties.Element != Element.Omni) continue;
                    float d = Vector3.Distance(from, c.transform.position);
                    if (d < reach) Offer(TargetKind.Omni, c.transform.position, Vector3.zero, d * 0.5f);
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
                index.QuerySphere(from, Mathf.Min(reach, 250f), _query);   // the sniper also reaches past this; prisms beyond come in its cone
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

        // The turret's turn this frame (az right, el up, radians), and the two buttons: Fire (the seat's gun: the left mouse
        // button, Space, the right trigger; on a touch screen a finger down) and Missile (the right mouse button, F, A; on
        // a touch screen a double tap). Aim with mouse look, a pad's sticks or W A S D at the turret rate, a touch drag.
        Vector2 ReadTurretAim(float dt, out bool missile, out bool gunHold, out bool gunEdge)
        {
            var aim = Vector2.zero;
            missile = gunHold = gunEdge = false;
            float rate = _s.TurretTurnDeg * Mathf.Deg2Rad * dt;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                aim += mouse.delta.ReadValue() * _cfg.MouseAimRadiansPerPixel;
                gunHold |= mouse.leftButton.isPressed;
                gunEdge |= mouse.leftButton.wasPressedThisFrame;
                missile |= mouse.rightButton.wasPressedThisFrame;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                aim += (pad.leftStick.ReadValue() + pad.rightStick.ReadValue()) * rate;
                gunHold |= pad.rightTrigger.ReadValue() > 0.3f;
                gunEdge |= pad.rightTrigger.wasPressedThisFrame;
                missile |= pad.buttonSouth.wasPressedThisFrame;
            }
            var kb = Keyboard.current;
            if (kb != null)
            {
                aim += KeyAxes(kb) * rate;
                gunHold |= kb.spaceKey.isPressed;
                gunEdge |= kb.spaceKey.wasPressedThisFrame;
                missile |= kb.fKey.wasPressedThisFrame;
            }
            aim += ReadTouch(out _, out _, out bool began, out bool held, out bool doubleTap) * 0.006f;
            gunHold |= held;
            gunEdge |= began && !doubleTap;
            missile |= doubleTap;
            return aim;
        }

        // A flown missile's stick (yaw right, pitch up, each -1..1) and whether Missile was pressed again (on a touch
        // screen, a tap).
        Vector2 ReadMissileStick(out bool burst)
        {
            var stick = Vector2.zero;
            burst = false;
            var pad = Gamepad.current;
            if (pad != null)
            {
                stick = Bigger(stick, pad.leftStick.ReadValue());
                stick = Bigger(stick, pad.rightStick.ReadValue());
                burst |= pad.buttonSouth.wasPressedThisFrame;
            }
            var kb = Keyboard.current;
            if (kb != null)
            {
                stick = Bigger(stick, KeyAxes(kb));
                burst |= kb.fKey.wasPressedThisFrame;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                stick = Bigger(stick, Vector2.ClampMagnitude(mouse.delta.ReadValue() * 0.05f, 1f));
                burst |= mouse.rightButton.wasPressedThisFrame;
            }
            ReadTouch(out bool tap, out var drag, out _, out _, out _);
            stick = Bigger(stick, Vector2.ClampMagnitude(drag / Mathf.Max(1f, Screen.height * 0.08f), 1f));
            burst |= tap;
            return Vector2.ClampMagnitude(stick, 1f);
        }

        static Vector2 Bigger(Vector2 a, Vector2 b) => b.sqrMagnitude > a.sqrMagnitude ? b : a;

        static Vector2 KeyAxes(Keyboard kb) => new(
            (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f),
            (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f));

        // The primary touch: its movement this frame (px); whether a short tap just ended; its offset from where it started
        // (the missile's stick); whether it began this frame, is down, or began as the second of a double tap.
        Vector2 ReadTouch(out bool tap, out Vector2 drag, out bool began, out bool held, out bool doubleTap)
        {
            tap = began = held = doubleTap = false;
            drag = Vector2.zero;
            var ts = Touchscreen.current;
            if (ts == null) return Vector2.zero;
            var touch = ts.primaryTouch;
            if (ts.touches.Count > 1 && ts.touches[1].press.isPressed) { _touchId = int.MinValue; return Vector2.zero; }
            if (touch.press.isPressed)
            {
                held = true;
                int id = touch.touchId.ReadValue();
                var p = touch.position.ReadValue();
                if (id != _touchId)
                {
                    _touchId = id; _touchStart = Time.unscaledTime; _touchFrom = p; _touchMoved = false;
                    began = true;
                    doubleTap = Time.unscaledTime - _lastTapAt < 0.35f && (p - _lastTapAt2).sqrMagnitude < 60f * 60f;
                    if (doubleTap) _lastTapAt = -9f;
                    return Vector2.zero;
                }
                drag = p - _touchFrom;
                if (drag.sqrMagnitude > 20f * 20f) _touchMoved = true;
                return touch.delta.ReadValue();
            }
            if (_touchId != int.MinValue)
            {
                tap = !_touchMoved && Time.unscaledTime - _touchStart < 0.25f;
                if (tap) { _lastTapAt = Time.unscaledTime; _lastTapAt2 = _touchFrom; }
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
