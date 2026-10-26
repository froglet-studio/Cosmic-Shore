using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An Omega crew in the game: three vessels of the shipped fleet flying as ONE MARCH FORMATION, each a role (Yash,
    /// 2026-10-26: "an aligned flock of three ... the relative distance between each vessel will be the same ... the
    /// manoeuvre will be done by the Stoat, the shooting and everything by the right vessel, and the left one will
    /// protect ... defender, midfielder and attacker"). Designed in the Vessel Studio's Omega Crews lab
    /// (<c>Docs/Studios/VesselStudio/omega.html</c>; the lab also has the mega / omega COMBINE, which is not in the game
    /// yet) and recorded in <c>Docs/Studios/CONCEPT_FLEET.md</c> §10; port notes <c>OMEGA_CREW.md</c> beside this file.
    /// The crews and their roles are <see cref="OmegaMath.Crews"/>; the arithmetic <see cref="OmegaMath"/>.
    ///
    /// <list type="bullet">
    /// <item><b>The formation.</b> The MOBILITY vessel (member 0) leads and does the manoeuvring: the hull's
    /// <see cref="OmegaVesselTransformer"/> always flies with ITS cruise and turn rates. The ATTACKER (member 1) rides to
    /// its right and behind, the DEFENDER (member 2) to its left; both are bodies this component places rigidly in the
    /// leader's frame and <see cref="OmegaCrewView"/> draws. Nothing ever drifts apart.</item>
    /// <item><b>Seats.</b> The person sits in one member; the HULL (the Unity vessel, with its camera) is that member, so
    /// the swap gesture (<see cref="PilotSwapGesture"/>: D-pad left / right, the arrow keys, a two-finger swipe) moves the
    /// hull to the chosen member's place in the formation and the camera goes with it; the member it left becomes a
    /// body. In the mobility seat the person steers with the sticks and RT is that vessel's move. In the attack seat
    /// the hull's own AIPilot flies the formation (the person's flight input is paused, the Orca's gunner rule), the
    /// right stick / mouse / W A S D aim the attacker's turret and RT fires its weapon. In the defence seat LT holds the
    /// shield (every hostile prism within <c>shieldRadius</c> of the formation dies; the meter drains) and RT is the
    /// ward burst (a one-shot sweep round the formation; costs meter). An AI fills every seat the person does not hold,
    /// input only: the attacker AI fires at mass ahead of its own place, the defender AI shields when mass is about to
    /// hit.</item>
    /// <item><b>The meter.</b> Regenerates always, crystals fill it (<see cref="OmegaCrystalEffectSO"/>), the shield and
    /// the ward spend it.</item>
    /// </list>
    /// <para><b>Where it runs.</b> On the local pilot's own hull while a person flies it, and on an AI hull on the
    /// authority (every seat AI: the hull's AIPilot leads, the attacker and defender bodies act). One person per crew:
    /// no rider seats from other machines yet (<see cref="HostsRiders"/>; multiplayer swapping is later, OMEGA_CREW.md §6).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OmegaCrew : MonoBehaviour, ICrewTriggers, IHullParkAware, ICrewStations
    {
        public const int Seats = OmegaMath.MemberCount;
        public const int Mobility = (int)OmegaMath.Role.Mobility, Attack = (int)OmegaMath.Role.Attack, Defence = (int)OmegaMath.Role.Defence;

        [Tooltip("Every number of the crew; each lab field carries the Omega Crews lab's label.")]
        [SerializeField] OmegaCrewConfigSO config;
        [Tooltip("This hull's flight: always the mobility vessel's cruise and turn rates.")]
        [SerializeField] OmegaVesselTransformer flight;

        enum Mode { Idle = 0, LocalHuman = 1, AICrew = 2 }

        /// <summary>A member's place and its move state. The hull's member mirrors the hull; the others ride the formation.</summary>
        public struct Body
        {
            public Vector3 Pos;
            public Quaternion Rot;
            public float Boost, BoostT, Cool, Charge, GunT, ActiveT;
            public bool Active, RtWas;
        }

        public struct Shot
        {
            public Vector3 A, B;
            public float T, Life;
            public bool Wide;
        }

        OmegaCrewConfigSO _cfg;
        OmegaMath.CrewDef _def;
        OmegaMath.Params _p;
        IVessel _vessel;
        IVesselStatus _status;
        IPlayer _player;
        VesselImpactor _impactor;
        Mode _mode;
        OmegaCrewView _view;

        readonly Body[] _bodies = new Body[Seats];
        int _hullMember;
        float _meter, _wardReadyAt, _wardFx;
        bool _shield, _weTookHull, _rtHeld, _ltHeld;
        float _aimYaw, _aimPitch;
        Vector3 _aimDir;
        CursorLockMode _cursorBefore;
        bool _cursorLocked;

        readonly List<Prism> _query = new();
        readonly List<Shot> _shots = new();

        // ------------------------------------------------------------------ read by the HUD and the view

        public OmegaCrewConfigSO Config => _cfg ? _cfg : config;
        public OmegaMath.CrewDef Def => _cfg ? _def : (config ? config.Def : OmegaMath.Crews[0]);
        /// <summary>True while the local person sits in this crew (and the HUD should read it).</summary>
        public bool HumanAboard => _mode == Mode.LocalHuman;
        /// <summary>The member the person sits in (0 mobility, 1 attack, 2 defence); the hull IS that member.</summary>
        public int HumanSeat => _hullMember;
        /// <summary>The member whose place the hull holds in the formation.</summary>
        public int HullMember => _hullMember;
        public float Meter01 => _meter;
        public bool ShieldUp => _shield;
        public Vector3 AimDirection => _aimDir;
        public float LastFireAt { get; private set; } = -99f;
        public float LastWardAt { get; private set; } = -99f;
        public float WardCooldown01 => _cfg && _cfg.WardCooldown > 0f ? Mathf.Clamp01((_wardReadyAt - Time.time) / _cfg.WardCooldown) : 0f;
        public bool WardFx(out float t) { t = _wardFx; return _wardFx > 0f; }
        public IReadOnlyList<Shot> Shots => _shots;
        public Body BodyAt(int member) => _bodies[Mathf.Clamp(member, 0, Seats - 1)];
        /// <summary>The formation's centre (the leader's place, wherever the hull is).</summary>
        public Vector3 FormationCentre => _bodies[Mobility].Pos;
        public float ShieldRadius => _cfg ? _cfg.ShieldRadius : 40f;

        /// <summary>The Charge card: the attacker's weapon (a cannon's charge while held, else how far its cooldown has run).</summary>
        public float Weapon01
        {
            get
            {
                var m = OmegaMath.Members[_def.Members[Attack]];
                ref var b = ref _bodies[Attack];
                if (m.Kind == OmegaMath.MoveKind.Cannon) return b.Charge;
                return m.Cool > 0f ? 1f - Mathf.Clamp01(b.Cool / m.Cool) : 1f;
            }
        }

        public string SeatName(int seat) => OmegaMath.Members[_def.Members[Mathf.Clamp(seat, 0, Seats - 1)]].Name;
        public string RoleName(int seat) => OmegaMath.RoleNames[Mathf.Clamp(seat, 0, Seats - 1)];

        internal IVesselStatus Status => _status;
        internal Transform Hull => _vessel?.Transform;

        /// <summary>The hull collected a crystal (<see cref="OmegaCrystalEffectSO"/>): the meter fills.</summary>
        public void OnCrystal()
        {
            if (!_cfg || _vessel == null) return;
            _meter = OmegaMath.OnCrystal(_meter, _p.MeterPerCrystal, false, _p.CombinedCrystal);
        }

        // ------------------------------------------------------------------ lifecycle (the Orca's)

        Mode WantedMode(out IVessel vessel)
        {
            vessel = null;
            if (!config || !TryGetComponent(out vessel) || vessel.VesselStatus == null) return Mode.Idle;
            var status = vessel.VesselStatus;
            if (CrewStations.IsParkedHere(vessel)) return Mode.AICrew;
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
            _def = _cfg.Def;
            _p = _cfg.Params;
            _vessel = vessel;
            _status = vessel.VesselStatus;
            _player = _status.Player;
            _mode = mode;
            _impactor = vessel.Transform.GetComponentInChildren<VesselImpactor>();
            if (!flight) TryGetComponent(out flight);
            _hullMember = Mobility;
            _meter = _cfg.StartMeter;
            _wardReadyAt = 0f; _wardFx = 0f;
            _shield = _weTookHull = _rtHeld = _ltHeld = false;
            _aimYaw = _aimPitch = 0f;
            _shots.Clear();
            for (int i = 0; i < Seats; i++) { _bodies[i] = default; _bodies[i].Boost = 1f; _bodies[i].Rot = transform.rotation; }
            PlaceFormation();
            if (flight) flight.SetForm(OmegaMath.MemberFlight(OmegaMath.Members[_def.Members[Mobility]]));
            _view = new OmegaCrewView(this);
        }

        void Unbind()
        {
            if (_vessel == null) return;
            if (_mode == Mode.LocalHuman && _hullMember != Mobility) HandOver(Mobility);
            if (flight)
            {
                flight.SetExtraBoost(1f);
                flight.ClearForm();
            }
            LockCursor(false);
            _view?.Dispose();
            _view = null;
            _vessel = null; _status = null; _player = null; _impactor = null;
            _mode = Mode.Idle;
        }

        void OnDisable() => Unbind();

        void OnDestroy() => Unbind();

        void Update()
        {
            var mode = WantedMode(out var vessel);
            if (mode != _mode || (_vessel != null && !UnityLiveness.Alive(_vessel.Transform))) Unbind();
            if (mode == Mode.Idle) return;
            if (_vessel == null) Bind(vessel, mode);
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _p = _cfg.Params;
            PlaceFormation();
            bool human = _mode == Mode.LocalHuman;

            // the mobility seat: its move on RT when the person is there (the AI leader flies the hull and never boosts)
            if (human && _hullMember == Mobility) StepMove(ref _bodies[Mobility], OmegaMath.Members[_def.Members[Mobility]], _rtHeld, _bodies[Mobility].Pos, transform.forward, dt);
            ApplyHullBoost();

            // the attack seat
            bool attackRt;
            Vector2 stick;
            if (human && _hullMember == Attack) stick = ReadAim(dt, out attackRt);
            else stick = AIAttacker(out attackRt);
            OmegaMath.AimStep(ref _aimYaw, ref _aimPitch, stick, dt);
            _aimDir = OmegaMath.AimDirection(_bodies[Attack].Rot, _aimYaw, _aimPitch);
            StepMove(ref _bodies[Attack], OmegaMath.Members[_def.Members[Attack]], attackRt, _bodies[Attack].Pos, _aimDir, dt);

            // the defence seat
            bool shield, ward;
            if (human && _hullMember == Defence) { shield = _ltHeld; ward = _rtHeld; }
            else AIDefender(out shield, out ward);
            _shield = shield && _meter > 0f;
            if (_shield) { ShieldSweep(); _meter = OmegaMath.Drain(_meter, 0f, true, _p.ShieldDrain, dt); }
            if (ward && Time.time >= _wardReadyAt && _meter >= _cfg.WardCost) WardBurst();

            _meter = OmegaMath.Regen(_meter, _p.Regen, dt);
            StepFx(dt);
        }

        void LateUpdate()
        {
            if (_vessel == null || !UnityLiveness.Alive(_vessel.Transform)) return;
            PlaceFormation();
            _view?.Draw();
        }

        // the hull's member mirrors the hull; the other two ride the formation in the hull's frame (rigid)
        void PlaceFormation()
        {
            if (_vessel == null || !_cfg) return;
            var pos = transform.position; var rot = transform.rotation;
            for (int i = 0; i < Seats; i++)
            {
                _bodies[i].Pos = pos + rot * OmegaMath.FormationOffsetFrom(_hullMember, i, _cfg.FormationSpread, _cfg.FormationBack);
                _bodies[i].Rot = rot;
            }
        }

        // ------------------------------------------------------------------ seats

        /// <inheritdoc cref="ICrewStations.SeatCount"/>: the mobility vessel, the attacker, the defender.
        public int SeatCount => Seats;

        /// <inheritdoc cref="ICrewStations.LocalSeat"/>
        public int LocalSeat => _mode == Mode.LocalHuman ? _hullMember : -1;

        /// <inheritdoc cref="ICrewStations.HostsRiders"/>: not yet (multiplayer swapping is later; OMEGA_CREW.md §6).
        public bool HostsRiders => false;

        /// <inheritdoc cref="ICrewStations.TakeSeat"/>: the hull moves to that member's place; the camera follows it.
        public void TakeSeat(int seat)
        {
            if (_mode != Mode.LocalHuman || seat < 0 || seat >= Seats || seat == _hullMember) return;
            HandOver(seat);
        }

        /// <inheritdoc cref="IHullParkAware.ReturnHumanToHull"/>
        public void ReturnHumanToHull()
        {
            if (_hullMember == Mobility) return;
            _weTookHull = false;   // the autopilot is the parker's now: the hand-back must not stop it
            HandOver(Mobility);
        }

        /// <summary>The hull's trigger, pressed or released (<see cref="CrewTriggerActionSO"/>): what it does depends on
        /// the seat (mobility: RT the move; attack: RT fire; defence: LT the shield, RT the ward).</summary>
        public void SetTrigger(int trigger, bool down)
        {
            if (trigger == 0) _rtHeld = down;
            else _ltHeld = down;
        }

        // the person moves to another member: the hull takes that member's place in the formation (pose and speed are
        // the formation's), the member it leaves takes the hull's; leaving the mobility seat hands the hull to its AI
        void HandOver(int seat)
        {
            int was = _hullMember;
            if (flight)
            {
                var to = _bodies[seat];
                flight.Teleport(to.Pos, to.Rot, to.Rot * Vector3.forward, flight.Speed);
            }
            _hullMember = seat;
            PlaceFormation();
            // a release made while the AI flies the hull never arrives (R_VesselActionHandler ignores input under autopilot)
            _rtHeld = _ltHeld = false;
            if (was == Mobility && seat != Mobility)
            {
                _weTookHull = CrewSeat.HandHullToAI(_vessel, _player);
                LockCursor(true);
            }
            else if (seat == Mobility && was != Mobility)
            {
                CrewSeat.GiveHullBack(_vessel, _player, _weTookHull);
                _weTookHull = false;
                LockCursor(false);
            }
        }

        // Mouse look in the attack seat; a touch screen has no cursor to lock.
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

        // ------------------------------------------------------------------ the members' moves (the page's megaMove, folded)

        void ApplyHullBoost()
        {
            if (!flight) return;
            ref var b = ref _bodies[Mobility];
            flight.SetExtraBoost(b.BoostT > 0f ? b.Boost : 1f);
        }

        /// <summary>A member's RT move from <paramref name="origin"/> along <paramref name="aim"/> (OmegaMath.MoveKind):
        /// a boost burst (the formation's: the mobility vessel's cruise), a smash of the mass it reaches, guns, a cannon.</summary>
        void StepMove(ref Body b, in OmegaMath.Member m, bool rt, Vector3 origin, Vector3 aim, float dt)
        {
            b.Cool = Mathf.Max(0f, b.Cool - dt);
            bool press = rt && !b.RtWas;
            bool release = !rt && b.RtWas;
            b.RtWas = rt;
            b.Active = false;
            if (b.BoostT > 0f) b.BoostT -= dt;
            switch (m.Kind)
            {
                case OmegaMath.MoveKind.Boost:
                    if (press && b.Cool <= 0f)
                    {
                        b.Boost = m.Boost > 1f ? m.Boost : _cfg.FoldBoost;
                        b.BoostT = m.Time > 0f ? m.Time : 1f;
                        b.Cool = m.Cool;
                        Play(_cfg.MoveSound, origin);
                    }
                    b.Active = b.BoostT > 0f;
                    break;
                case OmegaMath.MoveKind.Smash:
                    if (m.Time > 0f)   // the Rhino's plough, the Scarab's dash: a press, then a timed sweep
                    {
                        if (press && b.Cool <= 0f)
                        {
                            b.ActiveT = m.Time; b.Cool = m.Cool;
                            if (m.Boost > 1f) { b.Boost = m.Boost; b.BoostT = m.Time; }
                            if (m.Speed > 0f && flight) { b.Boost = Mathf.Max(1f, m.Speed / Mathf.Max(1f, flight.Form.Cruise)); b.BoostT = m.Time; }
                            Play(_cfg.MoveSound, origin);
                        }
                        if (b.ActiveT > 0f)
                        {
                            b.ActiveT -= dt; b.Active = true;
                            SmashSphere(origin + aim * (m.Reach * 0.6f), m.Reach, aim);
                        }
                    }
                    else if (rt)   // the Thresher's ball: held, smashing what it reaches, a cooldown per smash
                    {
                        b.Active = true;
                        if (b.Cool <= 0f)
                        {
                            var right = Vector3.Cross(Vector3.up, aim).normalized;
                            var at = origin + aim * (m.Reach * 0.8f) + right * (Mathf.Sin(Time.time * 7f) * m.Reach * 0.5f);
                            if (SmashSphere(at, m.Reach * 0.6f, aim) > 0)
                            {
                                b.Cool = m.Cool;
                                _shots.Add(new Shot { A = origin, B = at, T = 0.15f, Life = 0.15f, Wide = true });
                                LastFireAt = Time.time;
                            }
                        }
                    }
                    break;
                case OmegaMath.MoveKind.Guns:
                    if (rt)
                    {
                        b.Active = true;
                        b.GunT -= dt;
                        if (b.GunT <= 0f)
                        {
                            b.GunT = m.Rate;
                            var hit = NearestInLine(origin, aim, m.Range, 6f, 4f);
                            if (hit) { _shots.Add(new Shot { A = origin, B = hit.transform.position, T = 0.12f, Life = 0.12f }); DamagePrism(hit, aim); LastFireAt = Time.time; }
                        }
                    }
                    else b.GunT = 0f;
                    break;
                case OmegaMath.MoveKind.Cannon:
                    if (rt) { b.Charge = Mathf.Min(1f, b.Charge + dt / m.Charge); b.Active = true; }
                    else if (release && b.Charge > 0.1f)
                    {
                        float blast = m.Blast * (0.5f + b.Charge);
                        var at = origin + aim * _cfg.CannonRange;
                        SmashSphere(at, blast, aim);
                        Explode(_cfg.CannonExplosions, at, Quaternion.LookRotation(aim, Vector3.up), blast * 2f);
                        _shots.Add(new Shot { A = origin, B = at, T = 0.2f, Life = 0.2f });
                        b.Charge = 0f;
                        LastFireAt = Time.time;
                        Play(_cfg.WeaponSound, origin);
                    }
                    else b.Charge = 0f;
                    break;
            }
        }

        // ------------------------------------------------------------------ the defence seat

        // the shield: every hostile prism within shieldRadius of the formation's centre dies while it is held
        void ShieldSweep()
        {
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return;
            var centre = FormationCentre;
            index.QuerySphere(centre, _cfg.ShieldRadius, _query);
            for (int i = 0; i < _query.Count; i++)
            {
                var p = _query[i];
                if (!Hostile(p)) continue;
                DamagePrism(p, (p.transform.position - centre).normalized);
            }
        }

        // the ward burst: one sweep half again as wide as the shield, a cooldown, a cost
        void WardBurst()
        {
            var centre = FormationCentre;
            float r = _cfg.ShieldRadius * 1.5f;
            SmashSphere(centre, r, transform.forward);
            Explode(_cfg.FinisherExplosions, centre, transform.rotation, r);
            _wardReadyAt = Time.time + _cfg.WardCooldown;
            _wardFx = 0.4f;
            _meter = Mathf.Max(0f, _meter - _cfg.WardCost);
            LastWardAt = Time.time;
            Play(_cfg.FinisherSound, centre);
        }

        // ------------------------------------------------------------------ the AI seats (input only: sticks and triggers)

        // the attacker: its turret turns onto the nearest mass ahead of ITS place and fires when aligned (a cannon holds until charged)
        Vector2 AIAttacker(out bool rt)
        {
            rt = false;
            var b = _bodies[Attack];
            var fwd = b.Rot * Vector3.forward;
            var target = NearestInLine(b.Pos, fwd, _cfg.AiAimRange, 40f, 6f);
            if (!target) return Vector2.zero;
            var dir = (target.transform.position - b.Pos).normalized;
            var right = b.Rot * Vector3.right; var up = b.Rot * Vector3.up;
            float wantYaw = Mathf.Atan2(Vector3.Dot(dir, right), Mathf.Max(0.05f, Vector3.Dot(dir, fwd)));
            float wantPitch = Mathf.Asin(Mathf.Clamp(Vector3.Dot(dir, up), -1f, 1f));
            var stick = new Vector2(Mathf.Clamp((wantYaw - _aimYaw) * 3f, -1f, 1f), Mathf.Clamp((wantPitch - _aimPitch) * 3f, -1f, 1f));
            float err = Mathf.Abs(wantYaw - _aimYaw) + Mathf.Abs(wantPitch - _aimPitch);
            var m = OmegaMath.Members[_def.Members[Attack]];
            rt = m.Kind == OmegaMath.MoveKind.Cannon ? _bodies[Attack].Charge < 0.97f || (err > 0.12f && _bodies[Attack].Charge < 1f) : err < 0.25f;
            return stick;
        }

        // the defender: the shield when mass is about to hit the formation, the ward when it is crowded
        void AIDefender(out bool shield, out bool ward)
        {
            shield = ward = false;
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return;
            index.QuerySphere(FormationCentre + transform.forward * (_cfg.ShieldRadius * 0.5f), _cfg.ShieldRadius + 20f, _query);
            int n = CountHostile();
            shield = n >= _cfg.AiShieldMass && _meter > 0.3f;
            ward = n >= _cfg.AiFinisherMass;
        }

        int CountHostile()
        {
            int n = 0;
            for (int i = 0; i < _query.Count; i++) if (Hostile(_query[i])) n++;
            return n;
        }

        // ------------------------------------------------------------------ prisms

        bool Hostile(Prism p) => p && !p.destroyed && _status != null && p.Domain != _status.Domain;

        int SmashSphere(Vector3 at, float radius, Vector3 direction)
        {
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return 0;
            index.QuerySphere(at, radius, _query);
            int n = 0;
            for (int i = 0; i < _query.Count; i++)
            {
                var p = _query[i];
                if (!Hostile(p)) continue;
                DamagePrism(p, direction);
                n++;
            }
            return n;
        }

        // the nearest hostile prism within `perp` of the line from `from` along `dir`, at least `minAlong` out
        Prism NearestInLine(Vector3 from, Vector3 dir, float range, float perp, float minAlong)
        {
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return null;
            float halfAngle = Mathf.Max(3f, Mathf.Atan2(perp, Mathf.Max(1f, range * 0.25f)) * Mathf.Rad2Deg);
            index.QueryCone(from, dir, range, halfAngle, perp, _query);
            Prism best = null;
            float bestAlong = range;
            for (int i = 0; i < _query.Count; i++)
            {
                var p = _query[i];
                if (!Hostile(p)) continue;
                var d = p.transform.position - from;
                float along = Vector3.Dot(d, dir);
                if (along < minAlong || along > bestAlong) continue;
                if (Mathf.Sqrt(Mathf.Max(0f, d.sqrMagnitude - along * along)) > perp) continue;
                bestAlong = along;
                best = p;
            }
            return best;
        }

        void DamagePrism(Prism prism, Vector3 direction)
        {
            Vector3 impact = direction * _cfg.DebrisSpeed;
            if (prism.prismProperties is { IsSuperShielded: true }) prism.DeactivateShields(impact, _cfg.DebrisSpeedLimit);
            prism.Damage(impact, _status.Domain, _status.PlayerName, devastate: true, debrisSpeedLimit: _cfg.DebrisSpeedLimit);
        }

        void Explode(AOEExplosion[] prefabs, Vector3 at, Quaternion rotation, float maxScale)
        {
            if (prefabs == null || prefabs.Length == 0 || _status == null) return;
            var init = new AOEExplosion.InitializeStruct
            {
                OwnDomain = _status.Domain,
                Vessel = _status.Vessel,
                AnnonymousExplosion = false,
                OverrideMaterial = _status.AOEExplosionMaterial,
                MaxScale = maxScale,
                SpawnPosition = at,
                SpawnRotation = rotation,
                AffectSelfOverride = false,
            };
            ExplosionHelper.CreateExplosion(prefabs, init, _impactor ? _impactor.DIContainer : null);
        }

        // ------------------------------------------------------------------ input (the attack seat) and effects

        // Aim with mouse look, the pad's sticks, or W A S D (the arrows are the seat cycle); RT fires: the pad's right
        // trigger or X, the left mouse button, Space, or the hull's RT trigger action (a phone's thumb lift).
        Vector2 ReadAim(float dt, out bool rt)
        {
            var aim = Vector2.zero;
            rt = _rtHeld;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                aim += mouse.delta.ReadValue() * (_cfg.MouseAimRadiansPerPixel / Mathf.Max(1e-4f, OmegaMath.AimRateRadiansPerSecond * dt));
                rt |= mouse.leftButton.isPressed;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                aim += pad.leftStick.ReadValue() + pad.rightStick.ReadValue();
                rt |= pad.rightTrigger.ReadValue() > 0.3f || pad.buttonWest.isPressed;
            }
            var kb = Keyboard.current;
            if (kb != null)
            {
                aim += new Vector2((kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f), (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));
                rt |= kb.spaceKey.isPressed;
            }
            return Vector2.ClampMagnitude(aim, 3f);
        }

        void StepFx(float dt)
        {
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                var s = _shots[i];
                s.T -= dt;
                if (s.T <= 0f) _shots.RemoveAt(i); else _shots[i] = s;
            }
            if (_wardFx > 0f) _wardFx -= dt;
        }

        static void Play(EventReference evt, Vector3 at)
        {
            if (evt.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(evt, at);
        }
    }
}
