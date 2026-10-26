using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An Omega crew: three vessels of the shipped fleet that fly apart as their MEGA forms and gather into ONE OMEGA
    /// form with three seats (Yash, 2026-10-26: "each vessel will have their own megaforms and then finally they will have
    /// their omegaforms. They can combine"). Prototyped in the Vessel Studio's Omega Crews lab
    /// (<c>Docs/Studios/VesselStudio/omega.html</c>; design <c>Docs/Studios/CONCEPT_FLEET.md</c> §10), cloned here by
    /// /artifact-to-unity: the rules are <see cref="OmegaMath"/>'s, held to the page by <c>OmegaMathTests</c>; this is the
    /// glue to the scene. Port notes and what is not on the page yet: <c>OMEGA_CREW.md</c> beside this file.
    ///
    /// <list type="bullet">
    /// <item><b>Apart (mega).</b> The HULL is the member the person is seated in (its page cruise, turn and roll on
    /// <see cref="OmegaVesselTransformer"/>); the other two members are bodies this component flies beside it (an
    /// echelon, or to the hull when LT calls the gather) and <see cref="OmegaCrewView"/> draws. RT is the hull member's
    /// move: a boost burst (Stoat, Butterfly, Squirrel, Manta), a smash of the mass it reaches (Thresher, Rhino, Scarab),
    /// the Sparrow's guns, the Grizzly's charged cannon. The swap gesture (<see cref="PilotSwapGesture"/>: D-pad left /
    /// right, the arrow keys, a two-finger swipe) moves the person to another member: the hull takes that member's pose
    /// and flight, and the member left behind takes the hull's.</item>
    /// <item><b>Gather.</b> LT held with the meter at <c>combineAt</c> and every member within <c>gatherRange</c>: the two
    /// others dock onto their slots round the hull over <c>combineSeconds</c> (the page's kinematic dock), then the crew
    /// is the OMEGA.</item>
    /// <item><b>Combined (omega).</b> One body, the hull, on the crew's omega flight, ploughing the mass it touches. Three
    /// seats: <b>Helm</b> (0) flies, LT held <c>splitSeconds</c> splits; <b>Arms</b> (1) aims the crew weapon with the
    /// right stick, mouse or W A S D and fires it on RT (the Warden's Ripple whip, the Bulwark's Siege cannon, the
    /// Tempest's Pulsefire battery); <b>Core</b> (2) holds the shield on LT and fires the finisher on RT when the meter
    /// can pay (a cone ahead of annihilation, then the split). While the person is in Arms or Core the hull's own AIPilot
    /// flies it and the person's flight input is paused, exactly as the Orca's gunner seats do; an AI fills every seat
    /// the person does not hold. The meter drains combined, regenerates apart, and crystals fill it
    /// (<see cref="OmegaCrystalEffectSO"/>); empty, the crew splits.</item>
    /// </list>
    /// <para><b>Where it runs.</b> On the local pilot's own hull while a person flies it; an AI hull (the authority's or a
    /// remote copy) flies as its first member and never combines (OMEGA_CREW.md §6). One person per crew: the seats take
    /// no riders from other machines yet (<see cref="HostsRiders"/>).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OmegaCrew : MonoBehaviour, ICrewTriggers, IHullParkAware, ICrewStations
    {
        public const int Seats = OmegaMath.MemberCount;
        const int Helm = OmegaMath.Helm, Arms = OmegaMath.Arms, Core = OmegaMath.Core;

        [Tooltip("Every number of the crew; each lab field carries the Omega Crews lab's label.")]
        [SerializeField] OmegaCrewConfigSO config;
        [Tooltip("This hull's flight, whose cruise and turn rates the crew switches per form.")]
        [SerializeField] OmegaVesselTransformer flight;

        enum Mode { Idle = 0, LocalHuman = 1, AICrew = 2 }

        /// <summary>A member's body: the hull's mirror for the member the hull is, a simulated flyer for the others.</summary>
        public struct Body
        {
            public Vector3 Pos, Course;
            public Quaternion Rot;
            public float Speed, Boost, BoostT, Cool, Charge, GunT, ActiveT;
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
        OmegaMath.Form _form;
        int _hullMember, _humanSeat;
        float _meter, _gather, _split, _finisherT = -99f, _weaponCool, _weaponCharge, _gunT, _combinedT;
        bool _shield, _weTookHull, _rtHeld, _ltHeld, _weaponRtWas;
        float _aimYaw, _aimPitch;
        Vector3 _aimDir;
        CursorLockMode _cursorBefore;
        bool _cursorLocked;

        readonly List<Prism> _query = new();
        readonly List<Shot> _shots = new();
        Vector3 _finisherPos;
        Quaternion _finisherRot;
        float _finisherReach, _finisherFx;

        // ------------------------------------------------------------------ read by the HUD and the view

        public OmegaCrewConfigSO Config => _cfg ? _cfg : config;
        public OmegaMath.CrewDef Def => _cfg ? _def : (config ? config.Def : OmegaMath.Crews[0]);
        /// <summary>True while the local person flies or crews this hull (and the HUD should read it).</summary>
        public bool HumanAboard => _mode == Mode.LocalHuman;
        public OmegaMath.Form Form => _form;
        /// <summary>Apart: the member the hull is. Combined: the seat the person holds (0 Helm, 1 Arms, 2 Core).</summary>
        public int HumanSeat => _humanSeat;
        /// <summary>The member whose body the hull is apart (and whose slot it docks on).</summary>
        public int HullMember => _hullMember;
        public float Meter01 => _meter;
        public bool ShieldUp => _form == OmegaMath.Form.Omega && _shield;
        public bool FinisherReady => _form == OmegaMath.Form.Omega && OmegaMath.CanFinish(_meter, _p.FinisherAt);
        public float Finisher01 => _p.FinisherAt > 0f ? Mathf.Clamp01(_meter / _p.FinisherAt) : 0f;
        public Vector3 AimDirection => _aimDir;
        public float LastFireAt { get; private set; } = -99f;
        public float LastCombineAt { get; private set; } = -99f;
        public float LastSplitAt { get; private set; } = -99f;
        public float GatherK => _form == OmegaMath.Form.Gather ? Mathf.Clamp01(_gather / Mathf.Max(0.1f, _p.CombineSeconds)) : 0f;
        public IReadOnlyList<Shot> Shots => _shots;
        public bool FinisherFx(out Vector3 pos, out Quaternion rot, out float reach, out float t)
        {
            pos = _finisherPos; rot = _finisherRot; reach = _finisherReach; t = _finisherFx;
            return _finisherFx > 0f;
        }

        public Body BodyAt(int member) => _bodies[Mathf.Clamp(member, 0, Seats - 1)];

        /// <summary>The Charge card: a cannon's charge while held, else how far the weapon's cooldown has run.</summary>
        public float Weapon01
        {
            get
            {
                if (_form == OmegaMath.Form.Omega)
                {
                    if (_def.Weapon == OmegaMath.WeaponKind.Cannon) return _weaponCharge;
                    return _def.WeaponCool > 0f ? 1f - Mathf.Clamp01(_weaponCool / _def.WeaponCool) : 1f;
                }
                var m = OmegaMath.Members[_def.Members[_hullMember]];
                ref var b = ref _bodies[_hullMember];
                if (m.Kind == OmegaMath.MoveKind.Cannon) return b.Charge;
                return m.Cool > 0f ? 1f - Mathf.Clamp01(b.Cool / m.Cool) : 1f;
            }
        }

        /// <summary>The Space card: apart, how close the farthest member is to gather range (1 = all in range); the dock
        /// while gathering; full combined.</summary>
        public float Gather01
        {
            get
            {
                if (_form == OmegaMath.Form.Omega) return 1f;
                if (_form == OmegaMath.Form.Gather) return Mathf.Lerp(0.5f, 1f, GatherK);
                return Mathf.Clamp01(1f - FarthestMember() / (_p.GatherRange * 2f)) * 0.5f + (AllInGatherRange ? 0.5f : 0f);
            }
        }

        public bool AllInGatherRange => FarthestMember() < _p.GatherRange;

        public bool CanCombine => _form == OmegaMath.Form.Mega && OmegaMath.CanCombine(_meter, _p.CombineAt) && AllInGatherRange;

        public string SeatName(int seat) => _form == OmegaMath.Form.Omega
            ? OmegaMath.SeatNames[Mathf.Clamp(seat, 0, Seats - 1)]
            : OmegaMath.Members[_def.Members[Mathf.Clamp(seat, 0, Seats - 1)]].Name;

        internal IVesselStatus Status => _status;
        internal Transform Hull => _vessel?.Transform;

        /// <summary>The hull collected a crystal (<see cref="OmegaCrystalEffectSO"/>): the meter fills, half as much combined.</summary>
        public void OnCrystal()
        {
            if (!_cfg || _vessel == null) return;
            _meter = OmegaMath.OnCrystal(_meter, _p.MeterPerCrystal, _form == OmegaMath.Form.Omega, _p.CombinedCrystal);
        }

        // ------------------------------------------------------------------ lifecycle (the Orca's)

        Mode WantedMode(out IVessel vessel)
        {
            vessel = null;
            if (!config || !TryGetComponent(out vessel) || vessel.VesselStatus == null) return Mode.Idle;
            var status = vessel.VesselStatus;
            if (CrewStations.IsParkedHere(vessel)) return Mode.AICrew;
            if (VesselVisionShading.LocalVessel == transform && (_weTookHull || !status.AutoPilotEnabled)) return Mode.LocalHuman;
            if (status.Player != null && status.IsInitializedAsAI) return Mode.AICrew;
            return Mode.Idle;
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
            _form = OmegaMath.Form.Mega;
            _hullMember = 0;
            _humanSeat = 0;
            _meter = _cfg.StartMeter;
            _gather = _split = _combinedT = 0f;
            _weaponCool = _weaponCharge = _gunT = 0f;
            _finisherT = -99f;
            _shield = _weTookHull = _rtHeld = _ltHeld = _weaponRtWas = false;
            _aimYaw = _aimPitch = 0f;
            _shots.Clear();
            _finisherFx = 0f;
            for (int i = 0; i < Seats; i++)
            {
                _bodies[i] = default;
                _bodies[i].Rot = transform.rotation;
                _bodies[i].Course = transform.forward;
                _bodies[i].Boost = 1f;
                _bodies[i].Pos = transform.position + transform.rotation * OmegaMath.FormationOffset(i, _hullMember, _cfg.FormationSpread);
            }
            MirrorHull();
            if (flight) flight.SetForm(OmegaMath.MemberFlight(OmegaMath.Members[_def.Members[_hullMember]]));
            if (mode == Mode.LocalHuman) _view = new OmegaCrewView(this);
        }

        void Unbind()
        {
            if (_vessel == null) return;
            if (_form == OmegaMath.Form.Omega && _humanSeat != Helm) SetHumanSeat(Helm);
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

            if (_mode == Mode.AICrew) return;   // an AI hull flies as its first member; the crew's forms are the person's

            switch (_form)
            {
                case OmegaMath.Form.Mega: StepMega(dt); break;
                case OmegaMath.Form.Gather: StepGather(dt); break;
                case OmegaMath.Form.Omega: StepOmega(dt); break;
            }
            StepFx(dt);
        }

        void LateUpdate()
        {
            if (_vessel == null || !UnityLiveness.Alive(_vessel.Transform)) return;
            if (_mode == Mode.LocalHuman) MirrorHull();
            _view?.Draw();
        }

        // ------------------------------------------------------------------ seats

        /// <inheritdoc cref="ICrewStations.SeatCount"/>: three members apart, Helm / Arms / Core combined.
        public int SeatCount => Seats;

        /// <inheritdoc cref="ICrewStations.LocalSeat"/>
        public int LocalSeat => _mode == Mode.LocalHuman ? _humanSeat : -1;

        /// <inheritdoc cref="ICrewStations.HostsRiders"/>: not yet (OMEGA_CREW.md §6).
        public bool HostsRiders => false;

        /// <inheritdoc cref="ICrewStations.TakeSeat"/>
        public void TakeSeat(int seat)
        {
            if (_mode != Mode.LocalHuman || seat < 0 || seat >= Seats || seat == _humanSeat) return;
            if (_form == OmegaMath.Form.Omega) SetHumanSeat(seat);
            else if (_form == OmegaMath.Form.Mega) SwapHullTo(seat);
            // gathering: the dock is on the hull; a swap waits for the omega
        }

        /// <inheritdoc cref="IHullParkAware.ReturnHumanToHull"/>
        public void ReturnHumanToHull()
        {
            if (_form != OmegaMath.Form.Omega || _humanSeat == Helm) return;
            _weTookHull = false;   // the autopilot is the parker's now: SetHumanSeat(Helm) must not stop it
            SetHumanSeat(Helm);
        }

        /// <summary>The hull's trigger, pressed or released (<see cref="CrewTriggerActionSO"/>): what it does depends on
        /// the form and the seat (the page's LT / RT table).</summary>
        public void SetTrigger(int trigger, bool down)
        {
            if (trigger == 0) _rtHeld = down;
            else _ltHeld = down;
        }

        // combined: the person moves between Helm, Arms and Core; leaving Helm hands the hull to its AI
        void SetHumanSeat(int seat)
        {
            int was = _humanSeat;
            _humanSeat = seat;
            // a release made while the AI flies the hull never arrives (R_VesselActionHandler ignores input under autopilot)
            _rtHeld = _ltHeld = false;
            if (was == Helm && seat != Helm)
            {
                _weTookHull = CrewSeat.HandHullToAI(_vessel, _player);
                LockCursor(true);
            }
            else if (seat == Helm && was != Helm)
            {
                CrewSeat.GiveHullBack(_vessel, _player, _weTookHull);
                _weTookHull = false;
                LockCursor(false);
            }
            _weaponRtWas = false;
        }

        // apart: the hull becomes the chosen member, and the member left behind takes the hull's pose
        void SwapHullTo(int member)
        {
            if (member == _hullMember || !flight) return;
            MirrorHull();
            var mine = _bodies[_hullMember];
            var theirs = _bodies[member];
            _bodies[_hullMember].Pos = mine.Pos; _bodies[_hullMember].Rot = mine.Rot; _bodies[_hullMember].Course = mine.Course; _bodies[_hullMember].Speed = mine.Speed;
            flight.Teleport(theirs.Pos, theirs.Rot, theirs.Course, theirs.Speed);
            _hullMember = member;
            _humanSeat = member;
            _rtHeld = _ltHeld = false;
            flight.SetForm(OmegaMath.MemberFlight(OmegaMath.Members[_def.Members[member]]));
            ApplyHullBoost();
        }

        // Mouse look in the Arms seat; a touch screen has no cursor to lock.
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

        // ------------------------------------------------------------------ apart: the mega forms

        void MirrorHull()
        {
            if (_vessel == null) return;
            ref var b = ref _bodies[_hullMember];
            b.Pos = transform.position;
            b.Rot = transform.rotation;
            b.Course = transform.forward;
            b.Speed = flight ? flight.Speed : b.Speed;
        }

        float FarthestMember()
        {
            float far = 0f;
            var hull = transform.position;
            for (int i = 0; i < Seats; i++) if (i != _hullMember) far = Mathf.Max(far, Vector3.Distance(_bodies[i].Pos, hull));
            return far;
        }

        void StepMega(float dt)
        {
            MirrorHull();
            // the hull member's RT move
            StepMove(ref _bodies[_hullMember], OmegaMath.Members[_def.Members[_hullMember]], _rtHeld, dt);
            ApplyHullBoost();
            // the other members: fly beside the hull, or to it when LT calls the gather
            for (int i = 0; i < Seats; i++) if (i != _hullMember) StepAIMember(i, dt);
            _meter = OmegaMath.Regen(_meter, _p.Regen, dt);
            if (_ltHeld && CanCombine) BeginGather();
        }

        void StepAIMember(int i, float dt)
        {
            ref var b = ref _bodies[i];
            var m = OmegaMath.Members[_def.Members[i]];
            var f = OmegaMath.MemberFlight(m);
            if (b.BoostT > 0f) b.BoostT -= dt; else if (b.Boost > 1f) b.Boost = Mathf.Max(1f, b.Boost - 0.5f * dt);
            Vector3 target = _ltHeld
                ? transform.position - transform.forward * 12f + transform.rotation * OmegaMath.SlotOffset(SlotIndex(i))
                : transform.position + transform.rotation * OmegaMath.FormationOffset(i, _hullMember, _cfg.FormationSpread);
            // aim a little ahead of the slot so the member flies the formation rather than circling the point
            var ahead = target + transform.forward * Mathf.Max(20f, b.Speed * 0.6f);
            float gap = Vector3.Distance(b.Pos, target);
            var aim = gap > 8f ? target + (ahead - target) * Mathf.Clamp01(8f / gap) : ahead;
            b.Rot = OmegaMath.SteerToward(b.Rot, b.Pos, aim, transform.up, f, dt);
            float hullSpeed = flight ? flight.Speed : f.Cruise;
            float behind = Vector3.Dot(target - b.Pos, transform.forward);
            float cruise = behind > 15f ? Mathf.Max(hullSpeed, f.Cruise) * _cfg.MemberCatchUp : behind < -15f ? hullSpeed * 0.7f : Mathf.Max(hullSpeed, 0.25f * f.Cruise);
            OmegaMath.Fly(ref b.Pos, ref b.Course, ref b.Speed, b.Rot * Vector3.forward, cruise * b.Boost, dt);
        }

        // the dock slot a member takes round the hull: the hull is slot 0; the others slots 1 and 2 in member order
        int SlotIndex(int member)
        {
            if (member == _hullMember) return 0;
            int k = 1;
            for (int i = 0; i < Seats; i++)
            {
                if (i == _hullMember) continue;
                if (i == member) return k;
                k++;
            }
            return 2;
        }

        void ApplyHullBoost()
        {
            if (!flight) return;
            ref var b = ref _bodies[_hullMember];
            flight.SetExtraBoost(b.BoostT > 0f ? b.Boost : 1f);
        }

        // the page's megaMove, folded to what a hull does here (OmegaMath.MoveKind)
        void StepMove(ref Body b, in OmegaMath.Member m, bool rt, float dt)
        {
            b.Cool = Mathf.Max(0f, b.Cool - dt);
            bool press = rt && !b.RtWas;
            bool release = !rt && b.RtWas;
            b.RtWas = rt;
            b.Active = false;
            if (b.BoostT > 0f) b.BoostT -= dt;
            var fwd = transform.forward;
            var pos = transform.position;
            switch (m.Kind)
            {
                case OmegaMath.MoveKind.Boost:
                    if (press && b.Cool <= 0f)
                    {
                        b.Boost = m.Boost > 1f ? m.Boost : _cfg.FoldBoost;
                        b.BoostT = m.Time > 0f ? m.Time : 1f;
                        b.Cool = m.Cool;
                        Play(_cfg.MoveSound, pos);
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
                            Play(_cfg.MoveSound, pos);
                        }
                        if (b.ActiveT > 0f)
                        {
                            b.ActiveT -= dt; b.Active = true;
                            SmashSphere(pos + fwd * (m.Reach * 0.6f), m.Reach, fwd);
                        }
                    }
                    else if (rt)   // the Thresher's ball: held, smashing what it reaches, a cooldown per smash
                    {
                        b.Active = true;
                        if (b.Cool <= 0f)
                        {
                            var at = pos + fwd * (m.Reach * 0.8f) + transform.right * (Mathf.Sin(Time.time * 7f) * m.Reach * 0.5f);
                            if (SmashSphere(at, m.Reach * 0.6f, fwd) > 0) { b.Cool = m.Cool; _shots.Add(new Shot { A = pos, B = at, T = 0.15f, Life = 0.15f, Wide = true }); }
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
                            var hit = NearestInLine(pos, fwd, m.Range, 6f, 4f);
                            if (hit) { _shots.Add(new Shot { A = pos, B = hit.transform.position, T = 0.12f, Life = 0.12f }); DamagePrism(hit, fwd); LastFireAt = Time.time; }
                        }
                    }
                    else b.GunT = 0f;
                    break;
                case OmegaMath.MoveKind.Cannon:
                    if (rt) { b.Charge = Mathf.Min(1f, b.Charge + dt / m.Charge); b.Active = true; }
                    else if (release && b.Charge > 0.1f)
                    {
                        float blast = m.Blast * (0.5f + b.Charge);
                        var at = pos + fwd * _cfg.CannonRange;
                        SmashSphere(at, blast, fwd);
                        Explode(_cfg.CannonExplosions, at, transform.rotation, blast * 2f);
                        _shots.Add(new Shot { A = pos, B = at, T = 0.2f, Life = 0.2f });
                        b.Charge = 0f;
                        LastFireAt = Time.time;
                        Play(_cfg.WeaponSound, pos);
                    }
                    else b.Charge = 0f;
                    break;
            }
        }

        // ------------------------------------------------------------------ the gather and the omega

        void BeginGather()
        {
            _form = OmegaMath.Form.Gather;
            _gather = 0f;
            for (int i = 0; i < Seats; i++) { _bodies[i].Active = false; _bodies[i].ActiveT = 0f; _bodies[i].BoostT = 0f; _bodies[i].Boost = 1f; }
            ApplyHullBoost();
        }

        void StepGather(float dt)
        {
            MirrorHull();
            _gather += dt;
            float k = GatherK, e = OmegaMath.DockEase(k);
            float step = OmegaMath.DockStep(e, dt);
            float turn = Mathf.Min(1f, dt * 4f);
            for (int i = 0; i < Seats; i++)
            {
                if (i == _hullMember) continue;
                ref var b = ref _bodies[i];
                var slot = OmegaMath.SlotWorld(transform.position, transform.rotation, SlotIndex(i));
                b.Pos = Vector3.Lerp(b.Pos, slot, step);
                b.Rot = Quaternion.Slerp(b.Rot, transform.rotation, turn);
                b.Course = transform.forward;
                b.Speed = flight ? flight.Speed : b.Speed;
            }
            _meter = OmegaMath.Regen(_meter, _p.Regen, dt);
            if (k >= 1f) CompleteCombine();
        }

        void CompleteCombine()
        {
            _form = OmegaMath.Form.Omega;
            _humanSeat = Helm;
            _split = _combinedT = 0f;
            _weaponCool = _weaponCharge = _gunT = 0f;
            _aimYaw = _aimPitch = 0f;
            _shield = false;
            _rtHeld = _ltHeld = false;   // the LT that called the gather is spent
            LastCombineAt = Time.time;
            if (flight) flight.SetForm(OmegaMath.OmegaFlight(_def));
            Play(_cfg.CombineSound, transform.position);
        }

        void Split(string why)
        {
            if (_form != OmegaMath.Form.Omega) return;
            if (_humanSeat != Helm) SetHumanSeat(Helm);
            var pos = transform.position;
            var rot = transform.rotation;
            float speed = (flight ? flight.Speed : 0f) * OmegaMath.SplitSpeedShare;
            for (int i = 0; i < Seats; i++)
            {
                ref var b = ref _bodies[i];
                b.Pos = pos + rot * OmegaMath.SplitOffset(i);
                b.Rot = rot;
                b.Course = rot * Vector3.forward;
                b.Speed = speed;
                b.Boost = 1f; b.BoostT = 0f; b.Cool = 0f; b.Charge = 0f; b.Active = false; b.ActiveT = 0f;
            }
            if (flight)
            {
                flight.Teleport(_bodies[_hullMember].Pos, rot, _bodies[_hullMember].Course, speed);
                flight.SetForm(OmegaMath.MemberFlight(OmegaMath.Members[_def.Members[_hullMember]]));
                flight.SetExtraBoost(1f);
            }
            _form = OmegaMath.Form.Mega;
            _humanSeat = _hullMember;
            _shield = false;
            _rtHeld = _ltHeld = false;
            LastSplitAt = Time.time;
            Play(_cfg.SplitSound, pos);
            CSDebug.LogVerbose(CSLogChannel.VesselTelemetry, $"[OmegaCrew] {_def.Name}: split ({why})");
        }

        void StepOmega(float dt)
        {
            MirrorHull();
            _weaponCool = Mathf.Max(0f, _weaponCool - dt);
            bool humanHelm = _humanSeat == Helm, humanArms = _humanSeat == Arms, humanCore = _humanSeat == Core;

            // Helm: LT held splits on purpose (a person's); the AI helm (the hull's AIPilot) never splits by choice here
            bool helmLt = humanHelm && _ltHeld;
            if (helmLt) { _split += dt; if (_split >= _p.SplitSeconds) { Split("Helm"); return; } }
            else _split = 0f;

            // Arms: aim and fire
            Vector2 stick;
            bool armsRt;
            if (humanArms) stick = ReadAim(dt, out armsRt);
            else stick = AIArms(out armsRt);
            OmegaMath.AimStep(ref _aimYaw, ref _aimPitch, stick, dt);
            _aimDir = OmegaMath.AimDirection(transform.rotation, _aimYaw, _aimPitch);
            StepWeapon(armsRt, dt);
            if (_form != OmegaMath.Form.Omega) return;

            // Core: the shield and the finisher
            bool coreLt, coreRt;
            if (humanCore) { coreLt = _ltHeld; coreRt = _rtHeld; }
            else AICore(out coreLt, out coreRt);
            _shield = coreLt;
            if (coreRt && OmegaMath.CanFinish(_meter, _p.FinisherAt) && Time.time - _finisherT > 1f) { FireFinisher(); return; }

            if (_p.OmegaContactPlough || _shield) Plough();
            _meter = OmegaMath.Drain(_meter, _p.Drain, _shield, _p.ShieldDrain, dt);
            _combinedT += dt;
            if (_meter <= 0f) Split("meter empty");
        }

        // the page's omegaWeapon: the Arms seat's turret fires the crew weapon on RT
        void StepWeapon(bool rt, float dt)
        {
            bool release = !rt && _weaponRtWas;
            _weaponRtWas = rt;
            var pos = transform.position;
            switch (_def.Weapon)
            {
                case OmegaMath.WeaponKind.Whip:
                    if (rt && _weaponCool <= 0f)
                    {
                        var at = pos + _aimDir * (_def.WeaponReach * 0.8f);
                        SmashSphere(at, _def.WeaponReach * 0.55f, _aimDir);
                        _weaponCool = _def.WeaponCool;
                        _shots.Add(new Shot { A = pos, B = at, T = 0.15f, Life = 0.15f, Wide = true });
                        LastFireAt = Time.time;
                        Play(_cfg.WeaponSound, pos);
                    }
                    break;
                case OmegaMath.WeaponKind.Cannon:
                    if (rt) _weaponCharge = Mathf.Min(1f, _weaponCharge + dt / _def.WeaponCharge);
                    else if (release && _weaponCharge > 0.1f)
                    {
                        float blast = _def.WeaponBlast * (0.5f + _weaponCharge);
                        var at = pos + _aimDir * _cfg.CannonRange;
                        SmashSphere(at, blast, _aimDir);
                        Explode(_cfg.CannonExplosions, at, Quaternion.LookRotation(_aimDir, transform.up), blast * 2f);
                        _shots.Add(new Shot { A = pos + _aimDir * 14f, B = at, T = 0.2f, Life = 0.2f });
                        _weaponCharge = 0f;
                        LastFireAt = Time.time;
                        Play(_cfg.WeaponSound, pos);
                    }
                    else _weaponCharge = 0f;
                    break;
                case OmegaMath.WeaponKind.Guns:
                    if (rt)
                    {
                        _gunT -= dt;
                        if (_gunT <= 0f)
                        {
                            _gunT = _def.WeaponRate;
                            var hit = NearestInLine(pos, _aimDir, _def.WeaponRange, 7f, 6f);
                            if (hit) { _shots.Add(new Shot { A = pos, B = hit.transform.position, T = 0.08f, Life = 0.08f }); DamagePrism(hit, _aimDir); LastFireAt = Time.time; }
                        }
                    }
                    else _gunT = 0f;
                    break;
            }
        }

        // the page's omegaCore finisher: everything in the cone ahead, then the split
        void FireFinisher()
        {
            _finisherT = Time.time;
            float reach = _p.FinisherCone;
            var pos = transform.position;
            var rot = transform.rotation;
            var fwd = transform.forward;
            var inv = Quaternion.Inverse(rot);
            var index = PrismSpatialIndex.EnsureInstance();
            int n = 0;
            if (index != null && index.IsAvailable)
            {
                index.QueryCone(pos, fwd, reach, OmegaMath.FinisherQueryHalfAngle(reach), OmegaMath.FinisherBaseRadius, _query);
                for (int i = 0; i < _query.Count; i++)
                {
                    var p = _query[i];
                    if (!Hostile(p)) continue;
                    if (!OmegaMath.InFinisherCone(inv * (p.transform.position - pos), reach)) continue;
                    DamagePrism(p, fwd);
                    n++;
                }
            }
            Explode(_cfg.FinisherExplosions, pos + fwd * (reach * 0.5f), rot, reach * 0.9f, reach);
            _finisherPos = pos; _finisherRot = rot; _finisherReach = reach; _finisherFx = 0.6f;
            _meter = OmegaMath.Finisher(_meter, _p.FinisherCost);
            Play(_cfg.FinisherSound, pos);
            CSDebug.LogVerbose(CSLogChannel.VesselTelemetry, $"[OmegaCrew] {_def.Name}: {_def.Finisher}! {n} prisms");
            Split("finisher");
        }

        // the omega bulldozes the mass it touches (the page's omegaContactPlough; the shield does the same while held)
        void Plough()
        {
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return;
            index.QuerySphere(transform.position, _cfg.PloughRadius + 4f, _query);
            for (int i = 0; i < _query.Count; i++)
            {
                var p = _query[i];
                if (!Hostile(p)) continue;
                DamagePrism(p, (p.transform.position - transform.position).normalized);
            }
        }

        // ------------------------------------------------------------------ the AI seats (input only: sticks and triggers)

        // Arms: the turret turns onto the nearest mass ahead and fires when aligned (the cannon holds until charged)
        Vector2 AIArms(out bool rt)
        {
            rt = false;
            var pos = transform.position;
            var target = NearestInLine(pos, transform.forward, _cfg.AiAimRange, 40f, 6f);
            if (!target) return Vector2.zero;
            var dir = (target.transform.position - pos).normalized;
            var fwd = transform.forward; var right = transform.right; var up = transform.up;
            float wantYaw = Mathf.Atan2(Vector3.Dot(dir, right), Mathf.Max(0.05f, Vector3.Dot(dir, fwd)));
            float wantPitch = Mathf.Asin(Mathf.Clamp(Vector3.Dot(dir, up), -1f, 1f));
            var stick = new Vector2(Mathf.Clamp((wantYaw - _aimYaw) * 3f, -1f, 1f), Mathf.Clamp((wantPitch - _aimPitch) * 3f, -1f, 1f));
            float err = Mathf.Abs(wantYaw - _aimYaw) + Mathf.Abs(wantPitch - _aimPitch);
            rt = _def.Weapon == OmegaMath.WeaponKind.Cannon ? _weaponCharge < 0.97f || (err > 0.12f && _weaponCharge < 1f) : err < 0.25f;
            return stick;
        }

        // Core: the shield when mass is about to hit, the finisher when the meter is there and the cone is full of mass
        void AICore(out bool lt, out bool rt)
        {
            lt = rt = false;
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return;
            var pos = transform.position; var fwd = transform.forward;
            index.QueryCone(pos, fwd, 45f, 25f, 6f, _query);
            lt = CountHostile() >= _cfg.AiShieldMass && _meter > 0.3f;
            if (!OmegaMath.CanFinish(_meter, _p.FinisherAt)) return;
            float reach = _p.FinisherCone;
            index.QueryCone(pos, fwd, reach, OmegaMath.FinisherQueryHalfAngle(reach), OmegaMath.FinisherBaseRadius, _query);
            rt = CountHostile() >= _cfg.AiFinisherMass;
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

        void Explode(AOEExplosion[] prefabs, Vector3 at, Quaternion rotation, float maxScale, float height = 0f)
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
                HeightOverride = height,
                AffectSelfOverride = false,
            };
            ExplosionHelper.CreateExplosion(prefabs, init, _impactor ? _impactor.DIContainer : null);
        }

        // ------------------------------------------------------------------ input (the Arms seat) and effects

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
            if (_finisherFx > 0f) _finisherFx -= dt;
        }

        static void Play(EventReference evt, Vector3 at)
        {
            if (evt.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(evt, at);
        }
    }
}
