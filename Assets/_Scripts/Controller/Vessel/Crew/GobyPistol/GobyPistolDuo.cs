using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Goby &amp; Pistol crew vessel (VesselClassType.Goby). Garrett, 2026-10-22: "it should be a goby with its own kit
    /// that represents a goby and synergizes with the pistol and its kit" - the watchman goby and the near-blind pistol
    /// shrimp that share a burrow. The design: <c>.claude/skills/duo-vessel/references/designs/goby-and-pistol.md</c> §11.
    /// <list type="bullet">
    /// <item><b>The Goby</b> never cruises. It perches, aims, and DARTS like the Shrike slings itself: hold Dart to charge
    /// the distance, let go, and it flies there in <c>dartSeconds</c>, stopping on the first block on its line (Time 5: it
    /// passes through prisms). A dart into open space makes its own perch. Each dart lays the Pistol's ground. It LOCKS
    /// rival vessels (then fauna) in its nose's cone.</item>
    /// <item><b>The Pistol</b> rides the ground from perch to perch on a FLAT plane through the dart, downhill whichever
    /// way it goes (<see cref="PistolPlane"/>), Tiny Wings style. At each perch it rests and builds a DEN of prisms and
    /// shielded prisms (Mass 5: danger and super-shielded too), as many as its ENERGY and Mass make; it may ride back to an
    /// old den to add to it. Energy comes from the blocks of its ground it flies over instead of riding, the nth block of a
    /// flight more than the last, and each block flown over grows across the trail, is shielded once full, or is restored
    /// if it was destroyed (Garrett, 2026-10-29). Mass beside its plane is pulled into it, and crystals too, which it
    /// collects (Space; Space 5: shielded too); the path map weighs each island's two sides (one ridden ahead, the other
    /// back) by the unshielded opposing mass on it. Its Snap fires a vortex ring; with the Goby locked on a rival that is on the Pistol's screen,
    /// the ring homes on it and takes petals (Charge). Time is its jump's cooldown. Where a block of its ground was
    /// destroyed the ground is a hole it must clear (<see cref="PistolGround"/>); one that falls in restores the gap's blocks
    /// it got past, loses its energy, and comes out of a vortex-ring portal on the standing ground beyond the gap
    /// (2026-10-29), so a gap shrinks with every pass.</item>
    /// </list>
    ///
    /// <para>One human flies one half and the seat's AI the other; the swap is the arena's gesture (D-pad left / right,
    /// the left / right arrow keys, <see cref="PilotSwapGesture"/>) or a double tap on the small view. Each seat has its own controls: the
    /// Goby's Dart and Lock are the hull's own inputs (<see cref="CrewButtonActionSO"/>: Button3 / Button4, a pad's RT /
    /// LT), and on a phone each seat shows its own buttons (<see cref="GobyPistolButtons"/>).</para>
    ///
    /// <para>It lives on <c>Goby.prefab</c> and wakes only on the LOCAL pilot's hull while a human flies it (CrewSeat):
    /// the Pistol, the dens, the pull and the rings are simulated on that one machine until the crew is networked.</para>
    /// </summary>
    public sealed class GobyPistolDuo : MonoBehaviour, ICrewSeat, ICrewButtons, IHullParkAware, ICrewStations, ICrewRelay
    {
        enum Half { Goby = 0, Pistol = 1 }

        [Tooltip("The crew's numbers. Empty: the config's defaults.")]
        [SerializeField] GobyPistolConfigSO config;

        [Tooltip("The AI's level on whichever half the human is not flying.")]
        [SerializeField] AIDifficulty aiLevel = AIDifficulty.Hard;

        [Tooltip("Draw the Pistol's view (full screen while you fly the Pistol, the small square while you fly the Goby).")]
        [SerializeField] bool showSideView = true;

        sealed class Den { public Vector3 Pos, Up; public readonly List<Prism> Prisms = new(); public int Placed; }
        sealed class RingRun { public VortexRing Ring; public readonly List<Prism> Prisms = new(); public IVesselStatus Target; }

        IVessel _goby; IVesselStatus _status; Transform _gt; IPlayer _seatPlayer;
        Half _human = Half.Goby;
        float _t, _swapReadyAt;
        bool _pinned, _penHeld;
        Vector3 _home;
        readonly List<Den> _perches = new();                 // perch k: where the Goby sat, and the den the Pistol builds there
        readonly List<PlaneSegment> _segments = new();
        readonly List<PistolPlane.HillTrack> _tracks = new();   // each island's ground, built from its perches' seed (the same on every machine)
        // which of each island's ground blocks still stand (a destroyed one is a hole the Pistol must clear), and the blocks
        // themselves, where this machine laid them or found them (a rider's machine finds them as the Pistol nears)
        readonly List<PistolGround> _grounds = new();
        readonly List<Prism[]> _groundBlocks = new();
        float _bindAt;
        readonly Trail _trail = new();

        // the Goby: its dart and its lock
        bool _dartHeld, _lockHeld, _prevLockHeld, _charging, _darting;
        float _chargeT, _dartReadyAt, _dartDist, _dartWent, _dartSpeed;
        Vector3 _dartDir, _dartUp;
        bool _dartPhase;
        Transform _lockTarget; IVesselStatus _lockVessel; bool _lockIsVessel;

        // the Goby's own AI (while the human flies the Pistol): it aims the hull and darts by the same calls the buttons make
        Vector3 _aiDir; float _aiDecideAt, _aiIslandGoal; bool _aiHasDir;
        // its route: the island it means to lay next, aimed where the most opposing prisms are (PistolPaths.BestHeading)
        readonly List<Vector3> _routePts = new();
        uint _misjudgeSeed;
        Vector3 _aiRouteEnd; bool _aiHasRoute; int _aiRouteCount;
        const int RoutePointsMax = 2048;   // the opposing prisms a route is weighed against (sampled evenly beyond that)
        // perched: pinned on the end of an island, aiming, until the perch time is up (it cruises between perches)
        bool _perched; float _perchT;
        PistolAI.Mulberry32 _aiRng;

        // the Pistol
        PistolBodyMath.State _p; PistolBodyMath.Settings _body; VortexRing.Settings _vs;
        PistolPlane.Frame _f; PistolAI _ai;
        int _seg = -1, _dir = 1, _at, _travelDir = 1;
        bool _resting = true; float _restT, _waitT; int _restQueue, _restSpawned;
        // the Pistol's ENERGY (Garrett, 2026-10-29): every block it flies over instead of riding gives some, the nth of one
        // flight more (big jumps pay); a den is built from it, and a fall through a hole loses it all
        float _energy; int _flightBlocks;
        readonly List<int> _skipped = new();
        readonly Dictionary<Crystal, float> _crystalTakenAt = new();
        bool _touchGhost, _touchSnap, _backPressed;
        float _dive;
        // a fall into a gap in the trail (Garrett, 2026-10-29): two vortex-ring portals, where it fell in and where it comes
        // out beyond the gap, for fallPopSeconds; the Pistol spins out of the second
        float _popT = -1f; Vector3 _popOutAt, _popInAt;
        VortexRing _popOut, _popIn;
        // the juice of a rewarded block (Garrett, 2026-10-29: "there should be some good juice associated with growing,
        // restoring, and shielding prisms"): lit faceted shards and a flash shrinking onto the block, for the Pistol's camera
        enum Juice { Grow = 0, Shield = 1, Restore = 2 }
        struct Burst { public Vector3 At, R, Up, M, Scale; public Quaternion Rot; public float Born; public Juice Kind; public uint Seed; }
        readonly List<Burst> _bursts = new();
        readonly List<Matrix4x4>[] _juicePieces = { new(), new(), new() };
        Material[] _juiceMats;
        readonly float[] _juiceSoundAt = new float[3];
        uint _burstSeed;
        Domains _juiceDomain;
        const int MaxBursts = 128;
        // the blocks flown over this flight, rewarded when it lands (Garrett, 2026-10-29: "lets get the juice on landing
        // instead of as its happening"): released as a ripple running back from where it came down, juiceRippleSpeed u/s
        struct Reward { public int Island, Slot; public float S, Due; }
        readonly List<Reward> _flightRewards = new(), _dueRewards = new();
        // the choice at a perch (PistolPaths, Garrett 2026-10-25): every island's opposing prisms, kept one island at a time;
        // the map the person picks a den and a direction on; the cursor and how the dive confirms it
        readonly List<PathChoice> _options = new();
        readonly List<float> _pathScore = new();
        readonly HashSet<Prism> _scoreSeen = new();
        int _scoreCursor; float _pathScoreMax, _scoreAt;
        bool _choosing, _diveWasDown; PathChoice _choice; float _chooseT; Vector2 _stickWas;
        MapPointer _mapPointer;
        float _mapRadius = 1f, _mapYaw, _mapPitch;   // the person's spin of the map, kept from perch to perch
        Mesh _cube; Material[] _heatMats; Material _pickMat;
        readonly List<Matrix4x4>[] _heatPieces = { new(), new(), new(), new(), new() };
        readonly List<Matrix4x4> _pickPieces = new();
        bool _mouseDown, _aiming; Vector2 _mouseFrom, _pointerAt; Vector2? _mouseAim, _padAim; int _fingerId = -1;
        float _scanAt;
        readonly List<RingRun> _rings = new();
        readonly HashSet<Prism> _held = new();
        readonly List<Prism> _q = new(), _q2 = new();

        // the views
        readonly CrewPictureInPicture _pip = new();
        readonly CrewSmallViewDoubleTap _pipTap = new();
        readonly GobyPistolButtons _buttons = new();
        CrewViewRender _render;
        GameObject _view, _bodyGo; Camera _cam; bool _camLive; Quaternion _camRot; Vector3 _camPos, _camWindow; float _camWidth;
        Mesh _torus; Material _lockMat, _bodyMat;
        // the Pistol's model (Pistol.fbx): its scale, which way it faces along the plane, the claw it snaps and the tail it
        // curls on a dive, each turned about the model's own right axis from its rest pose
        bool _bodyIsModel; float _bodyScale = 1f, _facing = 1f, _snapAt = -10f, _tailCurl; Material _bodyDomain;
        Transform _claw, _tail; Quaternion _clawRest, _tailRest;

        GobyPistolConfigSO Config => config;

        // _rider: the person on this machine flies the Pistol of a Goby another machine flies (the Pistol, its dens, undertow
        // and rings run here, on the perches and the lock the hull's machine sends); _remotePistol: on the hull's machine, a
        // person on another machine flies the Pistol, so the Pistol does not run here
        bool _rider, _remotePistol;
        // _aiCrew: no person aboard (a hull an AI flies, or the person's own hull parked while they ride another): the
        // seat's own Goby AI and Pistol AI fly both halves, and the platform autopilot, which would fly the hull like its
        // donor Squirrel, stands down for them (_stoppedAutopilot: this crew stopped it, and starts it again on unbind)
        bool _aiCrew, _stoppedAutopilot;
        float _stateSendAt;
        int _sentPerches = -1;
        ulong _sentLock;
        readonly List<int> _stateScratch = new();

        /// <summary>True while the person on this machine flies this hull's Pistol from another machine than the hull's.</summary>
        public bool RidesHere => _rider;

        /// <summary>Add the crew to the vessel you are flying (it then stands in for the Goby). False when no local
        /// vessel is flying. A hull that already carries it (the Goby) is left as it is.</summary>
        public static bool AttachToLocalVessel(GobyPistolConfigSO cfg)
        {
            var vt = VesselVisionShading.LocalVessel;
            if (!vt || !vt.TryGetComponent(out IVessel _)) return false;
            if (vt.TryGetComponent(out GobyPistolDuo _)) return true;
            var duo = vt.gameObject.AddComponent<GobyPistolDuo>();
            if (cfg) duo.config = cfg;
            return true;
        }

        /// <summary>The seat's subvessel on the card ("Goby and Pistol").</summary>
        public string SeatName => "Pistol";
        /// <summary>True while the local human flies the Pistol and the seat's AI flies the Goby.</summary>
        public bool HumanInSeat => _human == Half.Pistol;

        // ---- readouts for the HUD and the tests
        public int PerchCount => _perches.Count;
        public int DenPrisms { get { int n = 0; foreach (var d in _perches) n += d.Placed; return n; } }
        public bool PistolResting => _resting;
        /// <summary>True for the moment the Pistol sinks into a gap in its trail, before it is set down beyond it.</summary>
        public bool Falling => _p != null && _p.falling;
        /// <summary>True while it comes out of the portal beyond a gap it fell into.</summary>
        bool Popping => _popT >= 0f && _popT < Config.FallPopSeconds;
        public bool HasLock => _lockTarget;
        public bool DartReady => !_darting && _t >= _dartReadyAt;
        public bool HeadingBack => _travelDir < 0;
        /// <summary>True while the Pistol, at a perch, waits for its person to pick a den and a direction (the path map).</summary>
        public bool Choosing => _choosing;
        /// <summary>The opposing prisms the vortex ring could reach along island <paramref name="segment"/>, last counted.</summary>
        public float PathScore(int segment) => PathScore(segment, 1) + PathScore(segment, -1);
        /// <summary>The opposing mass a vortex ring could destroy on one HALF of island <paramref name="segment"/> (the side
        /// the Pistol plays riding it <paramref name="dir"/>, +1 ahead, -1 back; <see cref="PistolPaths.Half(int, int)"/>):
        /// the summed volume of the unshielded prisms of other domains there, last counted.</summary>
        public float PathScore(int segment, int dir)
        {
            int h = PistolPaths.Half(segment, dir);
            return segment >= 0 && h < _pathScore.Count ? _pathScore[h] : 0f;
        }
        /// <summary>The Pistol's energy: what the blocks it flew over gave it, spent on its next den.</summary>
        public float Energy => _energy;
        /// <summary>The Pistol's energy as a share of the most it holds (its Mass card's gauge).</summary>
        public float Energy01 => Config ? Mathf.Clamp01(_energy / Mathf.Max(1f, Config.EnergyMax)) : 0f;
        // what each seat's four cards read (GobyPistolHUDController): 1 the instant a move is spent, 0 when it is ready
        public bool LockHeld => _lockHeld && _human == Half.Goby;
        public float DartCooldown01 => _darting || _t >= _dartReadyAt ? 0f : Mathf.Clamp01((_dartReadyAt - _t) / Mathf.Max(0.01f, Config.DartCooldown));
        public float SnapCooldown01 => _p == null || _t >= _p.vortexAt ? 0f : Mathf.Clamp01((_p.vortexAt - _t) / Mathf.Max(0.01f, _body.pVortexCd));
        public float GhostCooldown01 => _p == null || _t >= _p.ghostAt ? 0f : Mathf.Clamp01((_p.ghostAt - _t) / Mathf.Max(0.01f, _body.pGhostCd));
        public bool SnapHeld => _p != null && _p.prevVortex;
        public bool Ghosting => _p != null && _t < _p.ghostUntil;
        public bool Diving => _dive > 0.5f;
        /// <summary>The Pistol's speed as a share of its top speed (pMaxSpeed), 0..1.</summary>
        public float Speed01 => _p == null ? 0f : Mathf.Clamp01((_p.grounded ? Mathf.Abs(_p.v) : Mathf.Sqrt(_p.vx * _p.vx + _p.vy * _p.vy)) / Mathf.Max(1f, _body.pMaxSpeed));
        /// <summary>How far the undertow reaches now, 0 at Space's rest and 1 at its full level.</summary>
        public float PullReach01
        {
            get
            {
                if (!Config || _status == null) return 0f;
                var reach = Config.PullReach;
                float lo = reach.EvaluateAtNormalizedLevel(0f), hi = reach.EvaluateAtNormalizedLevel(1f);
                return hi - lo <= 1e-3f ? 0f : Mathf.Clamp01((reach.EvaluateReplicated(_status) - lo) / (hi - lo));
            }
        }

        // the local pilot's own hull, flown by a human: the crew seat rule every crew vessel shares; or the Pistol seat of a
        // Goby another machine flies, which the person here rides (CrewStations)
        bool ShouldRun(out IVessel vessel, out bool rider, out bool aiCrew)
        {
            aiCrew = false;
            rider = CrewStations.LocalRiderSeat(this) == 1;
            if (rider) return TryGetComponent(out vessel);
            if (!TryGetComponent(out vessel)) return false;
            if (IsAICrewHere(vessel)) { aiCrew = true; return true; }
            return CrewSeat.ShouldWake(this, false, out vessel);
        }

        // An AI crew runs where the hull is simulated: the server for an AI's hull, the person's own machine for their own
        // hull parked while they ride another (CrewStations) - every seat without a person in it is the AI's.
        bool IsAICrewHere(IVessel vessel)
        {
            if (CrewStations.IsParkedHere(vessel)) return true;
            var st = vessel.VesselStatus;
            return st?.Player != null && st.IsInitializedAsAI && st.Player.IsNetworkOwner && (st.AutoPilotEnabled || _stoppedAutopilot);
        }

        // ================================ binding ================================

        void Bind(IVessel vessel)
        {
            _goby = vessel; _status = vessel.VesselStatus; _gt = transform;
            if (!config) config = ScriptableObject.CreateInstance<GobyPistolConfigSO>();
            _body = Config.Body; _vs = Config.Vortex;
            _t = 0f; _swapReadyAt = 0f; _human = Half.Goby;
            _perches.Clear(); _segments.Clear(); _tracks.Clear(); _grounds.Clear(); _groundBlocks.Clear(); _rings.Clear(); _held.Clear();
            StopPops(); _bindAt = 0f; _flightRewards.Clear(); _dueRewards.Clear();
            _pathScore.Clear(); _scoreCursor = 0; _pathScoreMax = 0f; _choosing = false; _mapYaw = 0f; _mapPitch = Config.MapStartTilt; _mapPointer = default;
            _energy = 0f; _flightBlocks = 0; _crystalTakenAt.Clear();
            _darting = _charging = false; _dartReadyAt = 0f; ClearLock();
            _aiRng = new PistolAI.Mulberry32(20261022u); _ai = new PistolAI(31337u + 17u);
            _home = _gt.position; _aiHasDir = false; _aiIslandGoal = 0f; _perched = false; _perchT = 0f;
            _remotePistol = false; _sentPerches = -1; _sentLock = 0; _stateSendAt = 0f;
            if (_rider)
            {
                // a rider's Pistol: the hull is the other machine's (no pin, no pen, no pad of its own); the Goby perches
                // where it is until the hull's machine sends its perches
                _human = Half.Pistol;
                AddPerch(_gt.position, _gt.up, ownPad: false);
            }
            else
            {
                HoldPen(true);
                // the Goby starts perched where it is, on a pad of its own: the first island begins here
                AddPerch(_gt.position, _gt.up, ownPad: true);
                _perched = true; Pin(true);
            }
            _p = new PistolBodyMath.State { launch = Mathf.RoundToInt(_body.pLaunchCharges) };
            _f = WaitingFrame();
            PistolBodyMath.PlaceAtStart(_p, _f, _body.pMinSpeed);
            _seg = -1; _at = 0; _travelDir = 1; _resting = true; _restT = 0f; _restQueue = _restSpawned = 0; _waitT = 0f;
            if (!_aiCrew) BuildView();   // an AI crew draws nothing: no person here watches it
        }

        void Unbind()
        {
            if (_goby == null) return;
            if (_human == Half.Pistol && !_rider) SetHuman(Half.Goby);
            _human = Half.Goby;
            // the platform autopilot this AI crew stood down flies the hull again, while it is still an AI's (a person
            // who has just taken the hull flies it themselves)
            if (_stoppedAutopilot && UnityLiveness.Alive(_goby.Transform) && IsAICrewHere(_goby) && !_goby.VesselStatus.AutoPilotEnabled)
                _goby.ToggleAIPilot(true);
            _stoppedAutopilot = false;
            _buttons.Dispose();
            ReleaseAllRings();
            StopPops();
            Pin(false); HoldPen(false);
            _render?.Dispose(); _render = null; _pip.Dispose();
            TwoDCameraSky.Unregister(_cam);
            if (_view) Destroy(_view);
            if (_bodyGo) Destroy(_bodyGo);
            foreach (var o in new Object[] { _torus, _lockMat, _bodyMat, _cube, _pickMat }) if (o) Destroy(o);
            if (_heatMats != null) foreach (var m in _heatMats) if (m) Destroy(m);
            if (_juiceMats != null) foreach (var m in _juiceMats) if (m) Destroy(m);
            _juiceMats = null; _bursts.Clear(); _flightRewards.Clear(); _dueRewards.Clear();
            _heatMats = null; _choosing = false; _pathScore.Clear();
            _claw = _tail = null; _bodyIsModel = false; _bodyDomain = null;
            _perches.Clear(); _segments.Clear(); _tracks.Clear(); _grounds.Clear(); _groundBlocks.Clear(); ClearLock();
            _goby = null; _status = null; _gt = null; _cam = null; _view = null; _bodyGo = null; _camLive = false;
        }

        void OnDisable() => Unbind();

        void OnDestroy() => Unbind();

        /// <summary>Translation pinned (the fleet's restricted stance) while the Goby is perched or darting: perched it only
        /// aims, darting it is moved along the dart; between perches it cruises on its own flight.</summary>
        void Pin(bool on)
        {
            if (_pinned == on || _status == null) return;
            _pinned = on;
            if (_status.Vessel is VesselController controller) controller.SetTranslationRestricted(on);
            else _status.IsTranslationRestricted = on;
        }

        /// <summary>The fleet spawner's pen is up: the Goby's only trail is the ground its darts lay.</summary>
        void HoldPen(bool on)
        {
            if (_penHeld == on || _status == null) return;
            var prisms = _status.VesselPrismController;
            if (!prisms) return;
            prisms.SetSpawnerPaused(on);
            _penHeld = on;
        }

        bool IsAuthority()
        {
            var player = _status?.Player;
            if (player != null && player.IsNetworkOwner) return true;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            return nm == null || !nm.IsListening;
        }

        // ================================ the step ================================

        void Update()
        {
            if (!ShouldRun(out var vessel, out bool rider, out bool aiCrew)) { Unbind(); return; }
            if (_goby != null && (rider != _rider || aiCrew != _aiCrew)) Unbind();   // the seat changed hands: seat it afresh
            if (_goby == null) { _rider = rider; _aiCrew = aiCrew; Bind(vessel); }
            // the platform autopilot would fly the pinned Goby like its donor; the AI crew's own Goby AI flies it instead
            if (_aiCrew && _status.AutoPilotEnabled) { _goby.ToggleAIPilot(false); _stoppedAutopilot = true; }
            float dt = Time.deltaTime; if (dt <= 0f) return;
            _t += dt;
            _body.pLaunchCd = Mathf.Max(0.1f, Config.JumpCooldown.EvaluateReplicated(_status));

            if (_rider)
            {
                // the Pistol alone: the Goby is the hull machine's, its perches and lock arrive by OnCrewState
                PaintButtons();
                StepPistol(dt);
                Undertow(dt);
                BodyHits();
                StepRings(dt);
                return;
            }

            // a person on another machine flies the Pistol: the Goby's human keeps the Goby, and the Pistol runs there
            bool remote = CrewStations.HeldByRemoteHuman(this, 1);
            if (remote != _remotePistol) { _remotePistol = remote; _sentPerches = -1; ReleaseAllRings(); }   // this machine's rings let go: the Pistol is theirs now
            if (remote && _human == Half.Pistol) SetHuman(Half.Goby);

            if (!_aiCrew)
            {
                PollSwap();
                PaintButtons();
            }

            if (_human == Half.Goby && !_aiCrew) HumanGoby(dt); else GobyAI(dt);
            StepDart(dt);
            StepPerch(dt);
            HoldLock();

            if (_remotePistol) { SendPerches(); return; }
            StepPistol(dt);
            Undertow(dt);
            BodyHits();
            StepRings(dt);
        }

        // the level-5 upgrades, each read where it acts (named literally so the element ability table finds every gate)
        R_VesselElementalAbilityHandler Abilities => _status?.ElementalAbilityHandler;

        // ================================ the Goby: dart and perch ================================

        /// <summary>The Goby's own buttons from the hull's bindings (<see cref="CrewButtonActionSO"/>): 0 dart (hold to
        /// charge, let go to dart), 1 lock. The autopilot gate drops them while the seat's AI flies, so they come only from
        /// the person flying the Goby.</summary>
        public void SetButton(int button, bool down)
        {
            if (_goby == null || _human != Half.Goby) return;
            if (button == 0) _dartHeld = down;
            else if (button == 1) _lockHeld = down;
        }

        void HumanGoby(float dt)
        {
            if (_dartHeld && !_charging && DartReady) { _charging = true; _chargeT = 0f; }
            if (_charging)
            {
                _chargeT += dt;
                if (!_dartHeld) { _charging = false; FireDart(Mathf.Lerp(Config.DartMinRange, Config.DartRange, Mathf.Clamp01(_chargeT / Config.DartChargeSeconds))); }
            }
            if (_lockHeld && !_prevLockHeld) TryLock();
            _prevLockHeld = _lockHeld;
        }

        /// <summary>The charge the dart button holds, 0..1 (for the HUD).</summary>
        public float DartCharge01 => _charging ? Mathf.Clamp01(_chargeT / Config.DartChargeSeconds) : 0f;

        /// <summary>Dart along the nose, <paramref name="dist"/> u at most. It stops on the first block on its line (not the
        /// pair's own); with the Time upgrade it passes through prisms and stops at the full distance, on a block there if
        /// there is one. A dart that reaches nothing makes its own perch.</summary>
        public void FireDart(float dist)
        {
            if (_goby == null || !DartReady || dist < 20f) return;
            Vector3 o = _gt.position, f = _gt.forward;
            _dartPhase = (Abilities && Abilities.IsUpgradeActive(Element.Time));
            float go = dist;
            if (!_dartPhase && FirstOnLine(o, f, dist, out float along)) go = Mathf.Max(0f, along);
            if (go < 6f) return;
            _darting = true; _dartDir = f; _dartUp = _gt.up; _dartDist = go; _dartWent = 0f;
            _dartSpeed = go / Mathf.Max(0.05f, Config.DartSeconds);
            _perched = false; Pin(true);
        }

        /// <summary>The first block (not the pair's own) the hull's padded line meets within <paramref name="len"/>.</summary>
        bool FirstOnLine(Vector3 o, Vector3 f, float len, out float along)
        {
            along = float.MaxValue;
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return false;
            index.QuerySegment(o, o + f * len, Config.HullRadius + Config.QueryReach, _q);
            for (int i = 0; i < _q.Count; i++)
            {
                var prism = _q[i];
                if (!Perchable(prism)) continue;
                if (RayHits(prism, o, f, Config.HullRadius, len, out float t) && t < along) along = t;
            }
            _q.Clear();
            return along < float.MaxValue;
        }

        bool Perchable(Prism prism) => prism && !prism.destroyed && prism.prismProperties != null && prism.ownerID != _status.PlayerName;

        void StepDart(float dt)
        {
            if (!_darting) return;
            float move = Mathf.Min(_dartSpeed * dt, _dartDist - _dartWent);
            // without the Time upgrade, anything that moves into the line stops the dart there (the Shrike docks on the
            // first block it touches)
            if (!_dartPhase && move > 0f && FirstOnLine(_gt.position, _dartDir, move, out float hit)) { move = Mathf.Max(0f, hit); _dartDist = _dartWent + move; }
            if (IsAuthority()) _status.VesselTransformer.TranslateShip(_dartDir * move);
            _dartWent += move;
            if (_dartWent < _dartDist - 0.01f) return;
            _darting = false;
            _dartReadyAt = _t + Config.DartCooldown;
            Vector3 at = _gt.position;
            var prev = _perches[_perches.Count - 1];
            // a dash, not a perch: too close to the last perch for an island, or the Pistol already has its islands ahead
            if (!GobyIslands.Reaches(prev.Pos, at, Config.IslandMinLength) || IslandsAhead >= Config.IslandQueue) { Pin(false); return; }
            // a perch: the end of an island (a long gap is laid as several, each a perch of its own), pinned for the perch time
            bool onBlock = NearBlock(at, Config.HullRadius + 4f);
            int pieces = GobyIslands.Pieces(prev.Pos, at, Config.IslandMaxLength);
            Vector3 from = prev.Pos, fromUp = prev.Up;
            for (int i = 1; i <= pieces; i++)
            {
                float k = i / (float)pieces;
                Vector3 pos = Vector3.Lerp(prev.Pos, at, k), up = Vector3.Slerp(prev.Up, _dartUp, k);
                AddPerch(pos, up, ownPad: i < pieces || !onBlock);
                var seg = new PlaneSegment(from, pos, (fromUp + up).normalized);
                _segments.Add(seg);
                _tracks.Add(TrackFor(seg, _segments.Count - 1));
                AddGround(seg);
                LayGround(seg, _tracks[_tracks.Count - 1], _segments.Count - 1);
                from = pos; fromUp = up;
            }
            _perched = true; _perchT = 0f; _aiHasDir = false;
        }

        /// <summary>Islands laid ahead of the one the Pistol is on (none counted while it flies on another machine).</summary>
        int IslandsAhead => _remotePistol ? 0 : GobyIslands.Ahead(_perches.Count, _resting, _at, _seg, _dir);

        /// <summary>Perched, the Goby aims for <c>perchSeconds</c> (longer while a person charges a dart from it), then lifts
        /// off and cruises on its own flight.</summary>
        void StepPerch(float dt)
        {
            if (_perched)
            {
                _perchT += dt;
                bool charging = _human == Half.Goby && !_aiCrew && _charging;
                if (_perchT >= Config.PerchSeconds && !charging) _perched = false;
            }
            Pin(_perched || _darting);
        }

        bool NearBlock(Vector3 at, float r)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return false;
            index.QuerySphere(at, r + Config.QueryReach, _q2);
            bool near = false;
            for (int i = 0; i < _q2.Count && !near; i++)
                if (Perchable(_q2[i]) && BoxDistance(_q2[i], at) <= r) near = true;
            _q2.Clear();
            return near;
        }

        void AddPerch(Vector3 at, Vector3 up, bool ownPad)
        {
            var den = new Den { Pos = at, Up = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up };
            _perches.Add(den);
            if (!ownPad) return;
            // its own perch in open space: a small ring of blocks under the Goby
            var channel = _status?.VesselPrismController ? _status.VesselPrismController.PrismSpawnChannel : null;
            if (!channel) return;
            int n = Mathf.Max(1, Config.OwnPerchBlocks);
            Vector3 side = Vector3.Cross(den.Up, Mathf.Abs(Vector3.Dot(den.Up, Vector3.forward)) < 0.9f ? Vector3.forward : Vector3.right).normalized;
            float r = Config.PerchBlockSize * 1.6f;
            Vector3 under = at - den.Up * (Config.HullRadius + Config.PerchBlockSize * 0.6f);
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(360f * i / n, den.Up) * side;
                if (!SafeLookRotation.TryGet(dir, den.Up, out var rot, this, logError: false)) continue;
                BoostRingBuilder.LayOne(channel, under + dir * r, rot, Vector3.one * Config.PerchBlockSize, PrismKind.Plain,
                    _status.Domain, _status.PlayerName, _status.PlayerName);
            }
        }

        /// <summary>An island's ground from its perches (<see cref="PistolPlane.IslandSeed"/>): the rider's machine builds the same.</summary>
        PistolPlane.HillTrack TrackFor(in PlaneSegment seg, int island) => PistolPlane.HillTrack.Build(seg.L, PistolPlane.IslandSeed(seg.A, seg.B), Config.HillSpec, island);

        /// <summary>An island's ground blocks, all standing (<see cref="PistolGround"/>): one slot per <c>trailBlockGap</c>.</summary>
        void AddGround(in PlaneSegment seg)
        {
            var ground = new PistolGround(seg.L, Mathf.Max(2f, Config.TrailBlockGap));
            _grounds.Add(ground);
            _groundBlocks.Add(new Prism[ground.Count]);
        }

        PistolGround GroundOf(int island) => island >= 0 && island < _grounds.Count ? _grounds[island] : null;

        /// <summary>The island's ground: a trail block every <c>trailBlockGap</c> along the line, on the hills the Pistol rides.
        /// Each block is kept in its slot of the island's <see cref="PistolGround"/>: when it is destroyed the ground there is
        /// a hole.</summary>
        void LayGround(in PlaneSegment seg, PistolPlane.HillTrack track, int island)
        {
            if (seg.L < 1f) return;
            float gap = Mathf.Max(2f, Config.TrailBlockGap);
            var ground = GroundOf(island); var blocks = island >= 0 && island < _groundBlocks.Count ? _groundBlocks[island] : null;
            for (float s = gap * 0.5f; s < seg.L; s += gap)
            {
                var prism = LayBlock(seg, track, s);
                if (prism && ground != null && blocks != null) blocks[ground.SlotAt(s)] = prism;
            }
        }

        /// <summary>One ground block at <paramref name="s"/> along the island's line, on its hills, the pair's own.</summary>
        Prism LayBlock(in PlaneSegment seg, PistolPlane.HillTrack track, float s)
        {
            var prisms = _status.VesselPrismController;
            var channel = prisms ? prisms.PrismSpawnChannel : null;
            if (!channel) return null;
            Vector3 scale = Config.TrailBlockScale;
            Vector3 pos = seg.Ground(s, track);
            Vector3 along = seg.S + seg.N * track.Slope(s);
            if (!SafeLookRotation.TryGet(along, seg.N, out var rot, this, logError: false)) return null;
            var ret = channel.RaiseEvent(new PrismEventData
            {
                ownDomain = _status.Domain, Rotation = rot, SpawnPosition = pos, Scale = scale, PrismType = prisms.SpawnPrismType,
            });
            if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out Prism prism)) return null;
            prism.TargetScale = scale;
            prism.ownerID = _status.PlayerName;
            prism.ChangeTeam(_status.Domain);
            _trail.Add(prism);
            prism.prismProperties.Index = (ushort)Mathf.Min(_trail.TrailList.Count - 1, ushort.MaxValue);
            prism.Initialize(_status.PlayerName);
            prism.AdmitTargetScale(scale);
            prism.TargetScale = scale;
            prism.AssignTrail(_trail);   // AFTER Initialize: pool reuse clears trail membership
            return prism;
        }

        /// <summary>
        /// Which blocks of the island the Pistol rides still stand (Garrett, 2026-10-28: "when the goby's trail is destroyed
        /// the shrimp should have to clear the destroyed section. if they fall they should not hit phantom prisms"). A block is
        /// ground while it stands where the Goby laid it (a rival's steal leaves it standing, so it stays ground); a destroyed
        /// (or carried off) block is a hole, and a restored one ground again. A block this machine never laid or found is
        /// ground: on a rider's machine the blocks are found as the Pistol nears them.
        /// </summary>
        void RefreshGround()
        {
            if (_seg < 0 || _seg >= _grounds.Count || _seg >= _groundBlocks.Count) return;
            var ground = _grounds[_seg]; var blocks = _groundBlocks[_seg]; var seg = _segments[_seg]; var track = _tracks[_seg];
            bool bind = _rider && _t >= _bindAt;
            if (bind) _bindAt = _t + 0.25f;
            float near = PistolPlane.LineS(_f, _p.x / Mathf.Max(1e-4f, _f.Cos)), tol = 0.5f * ground.Gap;
            for (int i = 0; i < blocks.Length; i++)
            {
                float s = ground.BlockS(i);
                if (s >= seg.L) break;
                Vector3 at = seg.Ground(s, track);
                if (ReferenceEquals(blocks[i], null))
                {
                    if (!bind || Mathf.Abs(s - near) > GroundBindWindow) continue;
                    blocks[i] = FindGroundBlock(at, tol);
                    if (ReferenceEquals(blocks[i], null)) continue;
                }
                var prism = blocks[i];
                bool stands = prism && !prism.destroyed && (prism.transform.position - at).sqrMagnitude <= tol * tol;
                ground.SetGone(i, !stands);
            }
        }

        // how far along its island ahead of and behind the Pistol a rider's machine looks for the blocks it was not sent, u
        const float GroundBindWindow = 90f;

        /// <summary>The pair's own block laid at <paramref name="at"/> (a rider's machine was not sent the hull machine's).</summary>
        Prism FindGroundBlock(Vector3 at, float r)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return null;
            index.QuerySphere(at, r, _q2);
            Prism best = null; float bestD = r * r;
            foreach (var prism in _q2)
            {
                if (!prism || prism.destroyed || prism.ownerID != _status.PlayerName) continue;
                float d = (prism.transform.position - at).sqrMagnitude;
                if (d <= bestD) { bestD = d; best = prism; }
            }
            _q2.Clear();
            return best;
        }

        /// <summary>
        /// The blocks the Pistol flew over between frame-x <paramref name="fromX"/> and <paramref name="toX"/> instead of
        /// riding them (Garrett, 2026-10-29: "every time prisms are not ridden because the pistol is off trail then grow those
        /// prisms. if they are max size ... then shield them. if they are destroyed, restore them. this reward the player for
        /// making big jumps. the more prisms are skipped the more energy the pistol should collect"): each gives energy, the
        /// nth of the flight more than the last (<see cref="GobyDen.EnergyFor"/>) at once, and is rewarded
        /// (<see cref="RewardBlock"/>) when the flight comes down (<see cref="ReleaseFlightRewards"/>).
        /// </summary>
        void SkipOver(float fromX, float toX)
        {
            var ground = GroundOf(_seg);
            if (ground == null) return;
            _skipped.Clear();
            ground.BlocksBetween(PistolPlane.LineAt(_f, fromX), PistolPlane.LineAt(_f, toX), _skipped);
            foreach (int slot in _skipped)
            {
                _energy = Mathf.Min(Config.EnergyMax, _energy + GobyDen.EnergyFor(_flightBlocks++, Config.EnergyPerSkippedBlock, Config.EnergyFlightBonus));
                _flightRewards.Add(new Reward { Island = _seg, Slot = slot, S = ground.BlockS(slot) });
            }
            _skipped.Clear();
        }

        /// <summary>
        /// The flight's rewards fall due as it comes down at <paramref name="atS"/> along the island's line (a landing, a fall
        /// into a gap, a perch): a ripple running back from there toward the takeoff at <c>juiceRippleSpeed</c>, so the jump's
        /// blocks grow, shield and come back one after another with their juice (Garrett, 2026-10-29: "lets get the juice on
        /// landing instead of as its happening").
        /// </summary>
        void ReleaseFlightRewards(float atS)
        {
            float speed = Mathf.Max(1f, Config.JuiceRippleSpeed);
            foreach (var r in _flightRewards)
            {
                var due = r;
                due.Due = _t + Mathf.Abs(r.S - atS) / speed;
                _dueRewards.Add(due);
            }
            _flightRewards.Clear();
        }

        void ApplyDueRewards()
        {
            for (int i = 0; i < _dueRewards.Count; i++)
            {
                if (_dueRewards[i].Due > _t) continue;
                RewardBlock(_dueRewards[i].Island, _dueRewards[i].Slot);
                _dueRewards.RemoveAt(i--);
            }
        }

        /// <summary>A block the Pistol flew over: a destroyed one is restored (its hole is ground again); a standing one grows
        /// OUT OF THE PLANE only, across the trail (its local x, <c>skipGrowStep</c> a skip up to <c>skipGrowMax</c>); one
        /// already that wide is shielded. A block carried off its spot is not the island's any more and is left alone.</summary>
        void RewardBlock(int island, int slot)
        {
            if (island < 0 || island >= _groundBlocks.Count || slot < 0 || slot >= _groundBlocks[island].Length) return;
            var prism = _groundBlocks[island][slot];
            if (!prism) { RestoreBlock(island, slot); return; }   // its object is gone: a new block in its slot
            var ground = _grounds[island];
            Vector3 at = _segments[island].Ground(ground.BlockS(slot), _tracks[island]);
            float tol = 0.5f * ground.Gap;
            if (prism.destroyed || (prism.transform.position - at).sqrMagnitude > tol * tol) { RestoreBlock(island, slot); return; }
            var target = prism.TargetScale;
            float max = Mathf.Max(Config.TrailBlockScale.x, Config.SkipGrowMax);
            if (target.x < max - 1e-3f)
            {
                target.x = Mathf.Min(max, target.x + Config.SkipGrowStep);
                prism.AdmitTargetScale(target);
                prism.TargetScale = target;
                AddJuice(prism, Juice.Grow, target);
                return;
            }
            if (prism.prismProperties.IsShielded) return;
            prism.ActivateShield();
            AddJuice(prism, Juice.Shield, target);
        }

        /// <summary>
        /// A rewarded block's juice: a burst for the Pistol's camera (<see cref="DrawJuice"/>) and its sound, the kind's own
        /// FMOD field (empty: silent), at most one of a kind per <c>juiceSoundSpacing</c> (a long jump rewards a block every
        /// few frames). Grown and shielded blocks throw their shards out; a restored one draws them in. Nothing for an AI
        /// crew: no person here watches it.
        /// </summary>
        void AddJuice(Prism prism, Juice kind, Vector3 scale)
        {
            if (_aiCrew || !prism) return;
            var tr = prism.transform;
            if (_bursts.Count >= MaxBursts) _bursts.RemoveAt(0);
            _bursts.Add(new Burst
            {
                At = tr.position, Rot = tr.rotation, Scale = scale, R = _f.R, Up = _f.Up, M = _f.M, Born = _t, Kind = kind,
                Seed = unchecked(++_burstSeed * 2654435761u),
            });
            int k = (int)kind;
            var sound = kind switch { Juice.Grow => Config.GrowEvent, Juice.Shield => Config.ShieldEvent, _ => Config.RestoreEvent };
            if (_t < _juiceSoundAt[k]) return;
            _juiceSoundAt[k] = _t + Config.JuiceSoundSpacing;
            PlaySound(sound, tr.position);
        }

        static void PlaySound(FMODUnity.EventReference sound, Vector3 at)
        {
            if (sound.IsNull) return;
            var audio = CosmicShore.Core.AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(sound, at);
        }

        /// <summary>
        /// Brings island <paramref name="island"/>'s block <paramref name="slot"/> back when it is gone: <c>Restore</c> where
        /// the destroyed block still lies where the Goby laid it; a NEW block laid in its slot where it cannot be (Garrett,
        /// 2026-10-29: "I did not see any restores": a prism destroyed by a devastating hit - collisions of moved prisms,
        /// the Rhino, the Thresher, the Shrike's lance, a black hole - refuses <c>Restore</c> for good, so the gap stayed).
        /// False when the block stands, or this machine has no block for that slot (a rider's machine that never found it).
        /// </summary>
        bool RestoreBlock(int island, int slot)
        {
            if (island < 0 || island >= _groundBlocks.Count || slot < 0 || slot >= _groundBlocks[island].Length) return false;
            var blocks = _groundBlocks[island];
            var prism = blocks[slot];
            if (ReferenceEquals(prism, null)) return false;
            var ground = _grounds[island];
            float s = ground.BlockS(slot);
            Vector3 at = _segments[island].Ground(s, _tracks[island]);
            float tol = 0.5f * ground.Gap;
            bool inPlace = prism && (prism.transform.position - at).sqrMagnitude <= tol * tol;
            if (inPlace && !prism.destroyed) return false;   // it stands
            if (inPlace) prism.Restore();
            if (!inPlace || prism.destroyed)
            {
                if (_rider) return false;   // the hull's machine owns its trail: a rider never lays the pair's blocks
                var fresh = LayBlock(_segments[island], _tracks[island], s);
                if (!fresh) return false;
                blocks[slot] = prism = fresh;
            }
            ground.SetGone(slot, false);
            AddJuice(prism, Juice.Restore, prism.TargetScale);
            return true;
        }

        // ================================ the Goby: lock ================================

        /// <summary>Lock the best target in the nose's cone: a rival vessel first, then fauna (the Shrike's order), the one
        /// nearest the aim within its class. Nothing in the cone clears the lock.</summary>
        void TryLock()
        {
            Vector3 pos = _gt.position, f = _gt.forward;
            float cone = Config.LockCone * Mathf.Deg2Rad, best = cone;
            Transform target = null; IVesselStatus vessel = null;
            var players = Config.GameData ? Config.GameData.Players : null;
            if (players != null)
                foreach (var player in players)
                {
                    var v = EnemyStatus(player);
                    if (v == null) continue;
                    float a = Angle(pos, f, v.Transform.position, Config.LockRange);
                    if (a < best) { best = a; target = v.Transform; vessel = v; }
                }
            if (!target)
            {
                var cells = Cell.ActiveCellsSnapshot;
                for (int c = 0; c < cells.Count; c++)
                {
                    var cell = cells[c];
                    if (!cell) continue;
                    var fauna = cell.LiveFauna;
                    for (int i = fauna.Count - 1; i >= 0; i--)
                    {
                        var creature = fauna[i];
                        if (!creature || creature.IsDying || !creature.isActiveAndEnabled) continue;
                        float a = Angle(pos, f, creature.transform.position, Config.LockRange);
                        if (a < best) { best = a; target = creature.transform; }
                    }
                }
            }
            _lockTarget = target; _lockVessel = vessel; _lockIsVessel = vessel != null;
        }

        void HoldLock()
        {
            if (!_lockTarget) { ClearLock(); return; }
            if (_lockIsVessel && (_lockVessel == null || !UnityLiveness.Alive(_lockVessel.Transform))) { ClearLock(); return; }
            if ((_lockTarget.position - _gt.position).sqrMagnitude > Config.LockRange * Config.LockKeepScale * Config.LockRange * Config.LockKeepScale) ClearLock();
        }

        void ClearLock() { _lockTarget = null; _lockVessel = null; _lockIsVessel = false; }

        IVesselStatus EnemyStatus(IPlayer player)
        {
            var victim = player?.Vessel?.VesselStatus;
            if (victim == null || ReferenceEquals(victim, _status)) return null;
            if (victim.Domain == _status.Domain || !UnityLiveness.Alive(victim.Transform)) return null;
            return victim;
        }

        static float Angle(Vector3 pos, Vector3 fwd, Vector3 at, float range)
        {
            Vector3 d = at - pos; float l = d.magnitude;
            if (l > range || l < 1f) return 9f;
            return Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, fwd) / l, -1f, 1f));
        }

        // ================================ the Goby's AI (while the human flies the Pistol) ================================

        /// <summary>The seat's Goby (Garrett, 2026-10-23: "the goby is barely cruising around at all"): it CRUISES at
        /// <c>aiCruiseSpeed</c>, steering toward a locked rival or wandering within reach of home, and darts once its island
        /// is as long as it planned (a length between <c>islandMinLength</c> and <c>islandMaxLength</c>), so the dart's end
        /// is the next perch. While the Pistol already has <c>islandQueue</c> islands ahead it lays none and stays near the
        /// last perch. Perched, it aims at where it will go next. It acts through the calls the Goby's buttons make, and
        /// moves the hull itself (the pilot's input is paused while it flies).</summary>
        void GobyAI(float dt)
        {
            if (_darting) return;
            var lvl = Difficulty();
            if (!_lockTarget || !_lockIsVessel) AiLockNearestRival();
            if (_aiIslandGoal <= 0f)
            {
                // a new island: how long, and how the easier AIs will misread the field this time
                _aiIslandGoal = Mathf.Lerp(Config.IslandMinLength, Config.IslandMaxLength, _aiRng.Next());
                _misjudgeSeed = (uint)(_aiRng.Next() * uint.MaxValue);
                _aiHasDir = false;
            }
            if (!_aiHasDir || _t >= _aiDecideAt)
            {
                AiPlanRoute(lvl);
                _aiDir = AiCruiseDirection(lvl); _aiHasDir = true;
                _aiDecideAt = _t + Config.AiRethinkSeconds * (0.7f + 0.6f * _aiRng.Next()) + lvl.Late;
            }
            Quaternion.FromToRotation(_gt.forward, _aiDir).ToAngleAxis(out float deg, out var axis);
            if (deg > 0.05f && axis.sqrMagnitude > 1e-8f && IsAuthority())
                _status.VesselTransformer.ApplyRotation(Mathf.Min(deg, Config.AiTurnRate * dt), axis);
            if (_perched) return;   // aims from the perch; it lifts off when the perch time is up (StepPerch)
            if (IsAuthority()) _status.VesselTransformer.TranslateShip(_gt.forward * (Config.AiCruiseSpeed * dt));
            float from = Vector3.Distance(_gt.position, _perches[_perches.Count - 1].Pos);
            if (DartReady && deg < 25f && IslandsAhead < Config.IslandQueue && from + 0.5f * Config.AiDartLength >= _aiIslandGoal)
            {
                FireDart(Config.AiDartLength);
                if (_darting) _aiIslandGoal = 0f;
            }
        }

        void AiLockNearestRival()
        {
            var players = Config.GameData ? Config.GameData.Players : null;
            if (players == null) return;
            float best = Config.LockRange; IVesselStatus pick = null;
            foreach (var player in players)
            {
                var v = EnemyStatus(player);
                if (v == null) continue;
                float d = Vector3.Distance(v.Transform.position, _gt.position);
                if (d < best) { best = d; pick = v; }
            }
            if (pick != null) { _lockTarget = pick.Transform; _lockVessel = pick; _lockIsVessel = true; }
        }

        /// <summary>
        /// The island the Goby AI means to lay next (Garrett, 2026-10-25: "the goby AI was doing a bad job of taking paths
        /// that would give the shrimp plenty to destroy"): from the last perch, the heading whose island would pass the most
        /// opposing prisms within the Pistol's reach (the undertow's, or <c>pathScoreBand</c>), its end inside the home
        /// radius. The easier AIs misread the counts (misjudge, rolled per island: Easy up to ±50 %, Medium ±22 %). No route
        /// when nothing opposing is in reach: it falls back to a rival, home, or a wander.
        /// </summary>
        void AiPlanRoute(PistolAI.Difficulty lvl)
        {
            _aiHasRoute = false; _aiRouteCount = 0;
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable || _perches.Count == 0) return;
            Vector3 last = _perches[_perches.Count - 1].Pos;
            float goal = _aiIslandGoal > 0f ? _aiIslandGoal : Config.IslandMaxLength;
            float radius = Mathf.Max(Config.PullReach.EvaluateReplicated(_status), Config.PathScoreBand);
            index.QuerySphere(last, goal + radius, _q);
            _routePts.Clear();
            int stride = Mathf.Max(1, Mathf.CeilToInt(_q.Count / (float)RoutePointsMax));
            for (int i = 0; i < _q.Count; i += stride) if (Destroyable(_q[i])) _routePts.Add(_q[i].transform.position);
            _q.Clear();
            bool found = PistolPaths.BestHeading(last, goal, radius, _routePts, _home, Config.AiHomeRadius, _gt.forward,
                lvl.Mis * 10f, _misjudgeSeed, out var heading, out _aiRouteCount);
            _routePts.Clear();
            if (!found) return;
            _aiRouteEnd = last + heading * goal;
            _aiHasRoute = true;
        }

        /// <summary>Where the cruising Goby AI heads next: back toward the last perch while the Pistol's islands are full (it
        /// circles there instead of laying more); along its route, where the island will pass the most opposing prisms;
        /// else at a locked rival in reach; home when it strays; else a wander off the nose. Easy and Medium misjudge it by a
        /// few degrees.</summary>
        Vector3 AiCruiseDirection(PistolAI.Difficulty lvl)
        {
            Vector3 pos = _gt.position, last = _perches[_perches.Count - 1].Pos, dir;
            if (IslandsAhead >= Config.IslandQueue && (last - pos).magnitude > 0.5f * Config.IslandMinLength) dir = last - pos;
            else if (_aiHasRoute && (_aiRouteEnd - pos).sqrMagnitude > 1f) dir = _aiRouteEnd - pos;
            else if (_lockTarget && (_lockTarget.position - pos).sqrMagnitude < Config.LockRange * Config.LockRange) dir = _lockTarget.position - pos;
            else if ((pos - _home).magnitude > 0.7f * Config.AiHomeRadius) dir = _home - pos;
            else dir = Quaternion.AngleAxis((_aiRng.Next() - 0.5f) * 120f, _gt.up) * Quaternion.AngleAxis((_aiRng.Next() - 0.5f) * 50f, _gt.right) * _gt.forward;
            if (_aiRng.Next() < lvl.Mis * 4f) dir = Quaternion.AngleAxis((_aiRng.Next() - 0.5f) * 20f, _gt.up) * dir;
            return dir.sqrMagnitude > 1e-6f ? dir.normalized : _gt.forward;
        }

        PistolAI.Difficulty Difficulty() => aiLevel switch
        {
            AIDifficulty.Easy => PistolAI.Difficulty.Easy,
            AIDifficulty.Medium => PistolAI.Difficulty.Medium,
            _ => PistolAI.Difficulty.Hard,
        };

        // ================================ the Pistol: perch to perch ================================

        /// <summary>The frame the Pistol waits in before the first dart: straight ahead of the Goby's nose.</summary>
        PistolPlane.Frame WaitingFrame()
        {
            Vector3 a = _perches[_perches.Count - 1].Pos;
            return PistolPlane.For(new PlaneSegment(a, a + _gt.forward * 100f, _gt.up), 1, Config.SlopeDegrees, null);
        }

        void StepPistol(float dt)
        {
            ScorePaths();
            if (_popOut != null) { _popT += dt; if (_popT >= Config.FallPopSeconds) StopPops(); }
            ApplyDueRewards();
            if (_resting) { Rest(dt); return; }
            RefreshGround();
            var inp = _human == Half.Pistol ? HumanPistol() : AIPistol();
            _dive = inp.Dive;
            var ev = PistolBodyMath.Step(_p, _f, _t, dt, inp, _body);
            if (ev.Flew && !_p.falling) SkipOver(ev.FlewFrom, ev.FlewTo);
            if (ev.Landed || ev.FellIn) ReleaseFlightRewards(PistolPlane.LineAt(_f, _p.x));
            if (_p.grounded) _flightBlocks = 0;
            if (ev.FireSnap) { FireRing(ev.SnapAim); _snapAt = _t; }
            if (ev.FellIn) FallThrough();
            if (!_p.falling && _p.x >= _f.EndX - Config.ArriveDistance) Arrive(_dir > 0 ? _seg + 1 : _seg);
        }

        /// <summary>
        /// The Pistol sank into a gap in its trail (Garrett, 2026-10-29: "there is an unfun state where the best paths are no
        /// longer crossable. so lets have falling still restore the path up to what wasn't touched if the trail was still
        /// there, but once crossing where the trail should be they teleport to the next section of undestroyed trail, leaving
        /// the rest of the gap and loosing all energy. this way they can keep going and they restore some trail to try again on
        /// the next pass. this also still rewards them for how far they make it before failing without taking the thrill out
        /// of succeeding."): the gap's blocks it got past are restored (<see cref="PistolGround.AfterGap"/>), its energy is
        /// gone, and it comes out of a vortex-ring portal on the next standing ground beyond the gap, riding on at the speed it
        /// had; the rest of the gap stays for the next pass. A gap that runs to the perch sets it at the perch, where it
        /// arrives with no energy, so the den grows nothing.
        /// </summary>
        void FallThrough()
        {
            var ground = GroundOf(_seg);
            float s = PistolPlane.LineAt(_f, _p.x), land = s;
            _skipped.Clear();
            if (ground != null) land = ground.AfterGap(s, _dir, _skipped);
            foreach (int slot in _skipped) RestoreBlock(_seg, slot);
            _skipped.Clear();
            _energy = 0f; _flightBlocks = 0;
            float u = Mathf.Clamp(_dir > 0 ? land : _f.L - land, 0f, _f.L);
            float speed = Mathf.Max(_body.pMinSpeed, Mathf.Abs(_p.vx));
            if (_body.pMaxSpeed > 0f) speed = Mathf.Min(speed, _body.pMaxSpeed);
            _popOutAt = _p.wpos;
            PistolBodyMath.PlaceOnGround(_p, _f, PistolPlane.GroundPoint(_f, u).x, speed);
            _popInAt = _p.wpos;
            StopPops();
            _popOut = new VortexRing(_popOutAt, _f.M, Config.FallPopSeconds);
            _popIn = new VortexRing(_popInAt, _f.M, Config.FallPopSeconds);
            _popT = 0f;
            if (!_aiCrew) PlaySound(Config.FallThroughEvent, _popInAt);
        }

        void StopPops()
        {
            if (_popOut != null) VortexRingLens.Remove(_popOut);
            if (_popIn != null) VortexRingLens.Remove(_popIn);
            _popOut = _popIn = null; _popT = -1f;
        }

        /// <summary>The Pistol reaches perch <paramref name="perch"/>: it rests there and builds its den, as many prisms as
        /// its energy and the pair's Mass make (GobyDen), and spends that energy; what the den could not take is kept.</summary>
        void Arrive(int perch)
        {
            _resting = true; _at = Mathf.Clamp(perch, 0, _perches.Count - 1); _restT = 0f; _waitT = 0f; _restSpawned = 0;
            var den = _perches[_at];
            if (_flightRewards.Count > 0) ReleaseFlightRewards(PistolPlane.LineAt(_f, _p.x));   // came down on the perch
            float mass = Config.DenMassScale.EvaluateReplicated(_status);
            _restQueue = GobyDen.PrismsFor(_energy, Config.EnergyPerDenPrism, mass, den.Placed, Config.DenMax);
            _energy = Mathf.Max(0f, _energy - GobyDen.EnergySpent(_restQueue, Config.EnergyPerDenPrism, mass));
            _p.x = _f.EndX; _p.y = PistolPlane.Ground(_f, _p.x); _p.wpos = den.Pos; _p.grounded = true;
        }

        void Rest(float dt)
        {
            _restT += dt;
            var den = _perches[_at];
            _p.wpos = den.Pos;
            // the den grows over the rest, one prism at a time
            int due = Config.RestSeconds > 0f ? Mathf.CeilToInt(_restQueue * Mathf.Clamp01(_restT / Config.RestSeconds)) : _restQueue;
            while (_restSpawned < due) { LayDenPrism(den); _restSpawned++; }
            if (_restT < Config.RestSeconds) return;

            // the Pistol's choice at a perch (Garrett, 2026-10-25): any den, either way. A person picks on the path map; the AI
            // takes the island holding the most opposing prisms for its ring, and otherwise its old way (on toward the Goby,
            // back toward the older dens after waiting at the head)
            PistolPaths.Options(_perches.Count, _segments.Count, _options);
            if (_human == Half.Pistol) { Choose(dt); return; }
            int best = PistolPaths.Best(_options, _pathScore, _segments.Count, _at);
            if (best >= 0) { SetOff(_options[best]); return; }
            if (_at == _perches.Count - 1 && _at > 0) { _waitT += dt; if (_waitT > Config.AiBackAfter) { _travelDir = -1; _waitT = 0f; } }
            else if (_travelDir < 0 && _restT > Config.RestSeconds) _travelDir = 1;   // the AI turns for the head at the den it went back to

            if (_travelDir < 0 && _at == 0) _travelDir = 1;
            if (_travelDir > 0 && _at < _segments.Count) Ride(_at, 1);
            else if (_travelDir < 0 && _at > 0) Ride(_at - 1, -1);
            // else: at the head perch, waiting for the Goby's next dart
        }

        void Ride(int seg, int dir)
        {
            _seg = seg; _dir = dir; _resting = false;
            _f = PistolPlane.For(_segments[seg], dir, Config.SlopeDegrees, _tracks[seg], GroundOf(seg));
            // from a standstill: the Pistol rested at the perch, so it sets off at the ride's least speed and gathers it on
            // the way down, never carrying its arrival speed into the first hump and flying off the perch (Garrett,
            // 2026-10-22: "it should not launch from the perch each time")
            PistolBodyMath.PlaceAtStart(_p, _f, _body.pMinSpeed);
            RefreshGround();
        }

        /// <summary>Sets off on <paramref name="c"/>: from another perch it TELEPORTS there first, which grows no den (Garrett,
        /// 2026-10-25); its energy goes with it, for the den it next rides to.</summary>
        void SetOff(PathChoice c)
        {
            if (!PistolPaths.Ready(c, _segments.Count)) return;
            if (c.Perch != _at)
            {
                _at = Mathf.Clamp(c.Perch, 0, _perches.Count - 1);
                _p.wpos = _perches[_at].Pos;
                _camLive = false;   // a cut, not a pan, to the den it jumped to
            }
            _choosing = false;
            _travelDir = c.Dir;
            _waitT = 0f;
            Ride(c.Segment, c.Dir);
        }

        /// <summary>
        /// The person's pick on the path map, controls in the order the 2D cameras serve them (Garrett, 2026-10-25: "the
        /// control priority for 2d characters is mobile, gamepad, mouse/keyboard ... the player needs to be able to spin
        /// the map"):
        /// <list type="bullet">
        /// <item><b>Touch</b> (the mouse is the same pointer): drag spins the map; a tap on a path picks it, starting from
        /// the den at the end nearer the tap; a tap on the picked path rides it; the phone's Back button turns it round.
        /// A finger still down from the ride, held still for <c>chooseHoldSeconds</c>, rides on.</item>
        /// <item><b>Gamepad</b>: the right stick spins the map; the left stick moves the pick to the next path that way on
        /// screen; A (or the left trigger) rides it, a fresh press or held through the perch; Y turns it round.</item>
        /// <item><b>Keyboard</b>: Space (or S, down) rides it the same way; A / D move the pick; F turns it round; Q / E spin
        /// the map.</item>
        /// </list>
        /// </summary>
        void Choose(float dt)
        {
            bool began = false, held = false; Vector2 at = _pointerAt;
            if (_cam) ReadPointer(out at, out began, out held);
            var kb = Keyboard.current; var pad = Gamepad.current;
            if (!_choosing)
            {
                _choosing = true; _chooseT = 0f; _stickWas = Vector2.zero;
                _choice = new PathChoice(_at, _travelDir);
                _diveWasDown = DiveButtonHeld(kb, pad);
                // a finger or the mouse still down from the ride: held still it rides on, moved it spins the map
                _mapPointer.Open(held, at);
                _scoreCursor = 0;
            }
            _chooseT += dt;
            if (_options.Count == 0) return;
            if (PistolPaths.IndexOf(_options, _choice) < 0) _choice = _options[PistolPaths.Nearest(_options, _choice.Perch)];
            bool go = false;

            // touch first (and the mouse, the same pointer): drag spins, a tap picks, a tap on the pick rides
            _mapPointer.Step(began, held, at, dt, TapSlop, TapMaxSeconds, out var drag, out bool tap, out var tapAt, out bool carriedStill);
            if (drag != Vector2.zero) SpinMap(drag * (180f / Mathf.Max(1f, Screen.height)) * Config.MapSpinDragScale);
            if (carriedStill && _chooseT >= Config.ChooseHoldSeconds) go = true;
            if (tap && PickNear(tapAt, out var picked))
            {
                if (picked.Same(_choice)) go = true;
                else _choice = picked;
            }

            // then the gamepad: right stick spins, left stick moves the pick, A rides, Y turns round
            if (pad != null)
            {
                Vector2 spin = pad.rightStick.ReadValue();
                if (spin.sqrMagnitude > 0.04f) SpinMap(spin * (Config.MapSpinRate * dt));
                Vector2 move = pad.leftStick.ReadValue();
                if (move.magnitude > 0.6f && _stickWas.magnitude <= 0.6f) _choice = StepToward(move);
                _stickWas = move;
                if (pad.buttonNorth.wasPressedThisFrame) _backPressed = true;
            }

            // then the keyboard: A / D move the pick, F (or B) turns round, Q / E spin, Space rides
            if (kb != null)
            {
                if (kb.aKey.wasPressedThisFrame) _choice = StepToward(Vector2.left);
                if (kb.dKey.wasPressedThisFrame) _choice = StepToward(Vector2.right);
                if (kb.fKey.wasPressedThisFrame || kb.bKey.wasPressedThisFrame) _backPressed = true;
                float yaw = (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);
                if (yaw != 0f) SpinMap(new Vector2(yaw * Config.MapSpinRate * dt, 0f));
            }

            if (_backPressed)   // the phone's Back button, Y or F: the same den, the other way
            {
                _backPressed = false;
                var turned = new PathChoice(_choice.Perch, -_choice.Dir);
                if (PistolPaths.IndexOf(_options, turned) >= 0) _choice = turned;
            }

            bool dive = DiveButtonHeld(kb, pad);
            if (dive && !_diveWasDown) go = true;
            if (dive && _chooseT >= Config.ChooseHoldSeconds) go = true;   // held through the perch: rides on
            _diveWasDown = dive;
            if (!go || !PistolPaths.Ready(_choice, _segments.Count)) return;
            SetOff(_choice);
            _mouseDown = held && !_mapPointer.Moved; _aiming = false; _mouseFrom = at;   // a finger still down keeps diving
            _mapPointer = default;
        }

        // a press that moves less than this is a tap (scaled for the screen's density), and a tap is quick
        float TapSlop => 12f * Mathf.Max(1f, Screen.dpi / 160f);
        const float TapMaxSeconds = 0.35f;

        static bool DiveButtonHeld(Keyboard kb, Gamepad pad)
            => (pad != null && (pad.buttonSouth.isPressed || pad.leftTrigger.ReadValue() > 0.5f))
               || (kb != null && (kb.spaceKey.isPressed || kb.sKey.isPressed || kb.downArrowKey.isPressed));

        /// <summary>Spins the map: x turns it round its up, y tips it (degrees), the tip held short of looking straight down
        /// or up.</summary>
        void SpinMap(Vector2 degrees)
        {
            _mapYaw = Mathf.Repeat(_mapYaw + degrees.x + 180f, 360f) - 180f;
            _mapPitch = Mathf.Clamp(_mapPitch - degrees.y, -80f, 80f);
        }

        /// <summary>The path whose start lies next on screen in <paramref name="dir"/> (screen space, y up): the nearest
        /// within 60 degrees of it.</summary>
        PathChoice StepToward(Vector2 dir)
        {
            if (!_cam) return PistolPaths.Step(_options, _choice, dir.x >= 0f ? 1 : -1);
            Vector2 from = _cam.WorldToScreenPoint(OptionAnchor(_choice)), d = dir.normalized;
            var best = _choice; float bestCost = float.MaxValue;
            foreach (var c in _options)
            {
                if (c.Same(_choice)) continue;
                var sp = _cam.WorldToScreenPoint(OptionAnchor(c));
                if (sp.z <= 0f) continue;
                Vector2 v = (Vector2)sp - from;
                float len = v.magnitude;
                if (len < 1f) continue;
                float cos = Vector2.Dot(v / len, d);
                if (cos < 0.5f) continue;
                float cost = len * (1f + 2f * (1f - cos));
                if (cost < bestCost) { bestCost = cost; best = c; }
            }
            return best;
        }

        /// <summary>The path a tap at <paramref name="screen"/> lands on, within <c>mapPickPixels</c>: on a HALF PLANE's wing,
        /// that half (the way it is ridden, from the den it sets off from); on the trail itself, from the den at the end nearer
        /// the tap; the head's "ahead" by its pointer.</summary>
        bool PickNear(Vector2 screen, out PathChoice picked)
        {
            picked = _choice;
            if (!_cam) return false;
            float reach = Config.MapPickPixels * Mathf.Max(1f, Screen.dpi / 160f), bestD = reach * reach;
            bool any = false;
            for (int k = 0; k < _segments.Count; k++)
                for (int side = -1; side <= 1; side++)
                {
                    float d = ScreenDistance2(screen, k, side, out float along);
                    if (d >= bestD) continue;
                    bestD = d; any = true;
                    if (side == 0) picked = along <= 0.5f ? new PathChoice(k, 1) : new PathChoice(k + 1, -1);
                    else picked = side > 0 ? new PathChoice(k, 1) : new PathChoice(k + 1, -1);
                }
            foreach (var c in _options)
            {
                if (PistolPaths.Ready(c, _segments.Count)) continue;
                var sp = _cam.WorldToScreenPoint(OptionAnchor(c));
                float d = ((Vector2)sp - screen).sqrMagnitude;
                if (sp.z > 0f && d < bestD) { bestD = d; picked = c; any = true; }
            }
            return any;
        }

        /// <summary>Squared screen distance from <paramref name="screen"/> to island <paramref name="k"/>'s trail
        /// (<paramref name="side"/> 0) or to the middle of its wing on the half ridden <paramref name="side"/> (+1 ahead, -1
        /// back); <paramref name="along"/> 0..1 from A.</summary>
        float ScreenDistance2(Vector2 screen, int k, int side, out float along)
        {
            along = 0f;
            const int n = 12;
            float best = float.MaxValue;
            Vector3 a = _cam.WorldToScreenPoint(MapLinePoint(k, side, 0f));
            for (int i = 1; i <= n; i++)
            {
                Vector3 b = _cam.WorldToScreenPoint(MapLinePoint(k, side, (float)i / n));
                if (a.z > 0f && b.z > 0f)
                {
                    Vector2 ab = (Vector2)(b - a);
                    float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector2.Dot(screen - (Vector2)a, ab) / ab.sqrMagnitude) : 0f;
                    float d = (screen - ((Vector2)a + ab * t)).sqrMagnitude;
                    if (d < best) { best = d; along = (i - 1 + t) / n; }
                }
                a = b;
            }
            return best;
        }

        /// <summary>A point <paramref name="u"/> (0..1 from A) along island <paramref name="k"/>'s trail (<paramref name="side"/>
        /// 0), or along the middle of the wing of its half ridden <paramref name="side"/>.</summary>
        Vector3 MapLinePoint(int k, int side, float u)
        {
            var seg = _segments[k];
            Vector3 p = seg.Ground(seg.L * Mathf.Clamp01(u), _tracks[k]);
            if (side == 0) return p;
            return p + HalfSide(k, side) * (MapSpine + 0.5f * WingReach(HalfHeat(k, side)));
        }

        /// <summary>Which way out of island <paramref name="k"/>'s plane its half ridden <paramref name="dir"/> lies: the side
        /// that ride's frame calls +z (<see cref="PistolPaths.Half(int, int)"/>).</summary>
        Vector3 HalfSide(int k, int dir)
        {
            var f = PistolPlane.For(_segments[k], 1, Config.SlopeDegrees, null);
            return dir > 0 ? f.M : -f.M;
        }

        /// <summary>A half plane's share of the most destroyable opposing mass any half holds, 0..1.</summary>
        float HalfHeat(int k, int dir) => _pathScoreMax > 0f ? Mathf.Clamp01(PathScore(k, dir) / _pathScoreMax) : 0f;

        /// <summary>How far a half's wing reaches out of the trail on the map, from a sliver (empty) to <c>mapWingReach</c>.</summary>
        float WingReach(float heat) => _mapRadius * Config.MapWingReach * Mathf.Lerp(0.2f, 1f, heat);

        /// <summary>The trail's own thickness on the map.</summary>
        float MapSpine => _mapRadius * Config.MapPathThickness * Config.MapPathThinShare;

        /// <summary>Where an option is picked on the map: a little way along its island from its den (the head's "ahead",
        /// which waits for the next island, a little way along the Goby's nose).</summary>
        Vector3 OptionAnchor(PathChoice c)
        {
            if (!PistolPaths.Ready(c, _segments.Count))
                return _perches[Mathf.Clamp(c.Perch, 0, _perches.Count - 1)].Pos + (_gt ? _gt.forward : Vector3.forward) * _mapRadius * 0.12f;
            return MapLinePoint(c.Segment, c.Dir, c.Dir > 0 ? 0.18f : 0.82f);
        }

        /// <summary>One island's two scores at a time, one per HALF PLANE (Garrett, 2026-10-29: the side the Pistol plays
        /// riding it ahead, and the other side riding it back, <see cref="PistolPaths.Half(int, int)"/>): "how much unshielded
        /// mass of opposing domains is available to be destroyed by vortex rings" on that side, the summed VOLUME of the
        /// prisms <see cref="Destroyable"/> counts, within the undertow's reach of its plane and <c>pathScoreBand</c> of its
        /// ground (mass in the plane counts on both). Only the score is by half: the other side is harder to reach, not out of
        /// reach, and the rings and the undertow act on both. Every frame while the map is open, every
        /// <c>pathScoreInterval</c> otherwise.</summary>
        void ScorePaths()
        {
            int halves = _segments.Count * 2;
            while (_pathScore.Count < halves) _pathScore.Add(0f);
            if (_pathScore.Count > halves) _pathScore.RemoveRange(halves, _pathScore.Count - halves);
            if (_segments.Count == 0) { _pathScoreMax = 0f; return; }
            if (!_choosing && _t < _scoreAt) return;
            _scoreAt = _t + Config.PathScoreInterval;
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;
            int k = _scoreCursor % _segments.Count;
            _scoreCursor = (k + 1) % _segments.Count;
            var seg = _segments[k];
            var f = PistolPlane.For(seg, 1, Config.SlopeDegrees, _tracks[k]);
            float reach = Mathf.Max(1f, Config.PullReach.EvaluateReplicated(_status)), band = Config.PathScoreBand;
            float step = Mathf.Max(reach, band) * 1.5f, r = Mathf.Sqrt(reach * reach + band * band + step * step * 0.25f);
            float ahead = 0f, back = 0f;
            _scoreSeen.Clear();
            for (float s = 0f; s <= seg.L + step * 0.5f; s += step)
            {
                index.QuerySphere(seg.Ground(Mathf.Min(s, seg.L), _tracks[k]), r, _q2);
                foreach (var prism in _q2)
                {
                    if (!_scoreSeen.Add(prism) || !Destroyable(prism)) continue;
                    Vector3 fr = f.ToFrame(prism.transform.position);
                    float ground = PistolPlane.Ground(f, Mathf.Clamp(fr.x, 0f, f.EndX)), mass = Mathf.Max(1e-3f, prism.Volume);
                    // the back frame's +z is this frame's -z: the other side
                    if (PistolPaths.InHalf(fr, f.EndX, ground, reach, band)) ahead += mass;
                    if (PistolPaths.InHalf(new Vector3(fr.x, fr.y, -fr.z), f.EndX, ground, reach, band)) back += mass;
                }
                _q2.Clear();
            }
            _scoreSeen.Clear();
            _pathScore[PistolPaths.Half(k, 1)] = ahead;
            _pathScore[PistolPaths.Half(k, -1)] = back;
            _pathScoreMax = 0f;
            foreach (float n in _pathScore) _pathScoreMax = Mathf.Max(_pathScoreMax, n);
        }

        /// <summary>A prism a vortex ring can DESTROY that is not the pair's (Garrett, 2026-10-29: "unshielded mass of opposing
        /// domains"): another domain, breakable, not shielded or super-shielded (a ring's collapse only knocks a shield off),
        /// not still growing. What the path map weighs and the Goby AI routes through; the rings themselves take whatever
        /// they catch (StepRings).</summary>
        bool Destroyable(Prism prism) => Breakable(prism) && !prism.IsGrowing && !prism.prismProperties.IsShielded
                                         && !prism.prismProperties.IsSuperShielded && prism.Domain != _status.Domain;

        void LayDenPrism(Den den)
        {
            if (den.Placed >= Config.DenMax) return;
            var channel = _status?.VesselPrismController ? _status.VesselPrismController.PrismSpawnChannel : null;
            if (!channel) return;
            float spacing = GobyDen.Spacing(Config.DenBlockSize, Config.DenClearance);
            Vector3 local = GobyDen.Local(den.Placed, Config.DenFirstRadius, spacing);
            Quaternion basis = Quaternion.FromToRotation(Vector3.up, den.Up);
            Vector3 outward = basis * local;
            var kind = GobyDen.KindFor(den.Placed, (Abilities && Abilities.IsUpgradeActive(Element.Mass)), Config.DenShieldedShare, Config.DenDangerShare, Config.DenSuperShare);
            den.Placed++;
            if (!SafeLookRotation.TryGet(outward, den.Up, out var rot, this, logError: false)) rot = basis;
            var prism = BoostRingBuilder.LayOne(channel, den.Pos + outward, rot, Vector3.one * Config.DenBlockSize, kind,
                _status.Domain, _status.PlayerName, _status.PlayerName);
            if (prism) den.Prisms.Add(prism);
        }

        PistolBodyMath.Input AIPistol()
        {
            if (_t >= _scanAt) { _scanAt = _t + 0.1f; ScanForAI(); }
            return _ai.Think(_p, _f, _t, Difficulty(), Config.AI, LockedInView());
        }

        void ScanForAI()
        {
            _ai.Vis.Clear();
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;
            index.QuerySphere(_p.wpos, Mathf.Max(Config.AI.pLaunchRange, 200f) + 60f, _q);   // the Snap's reach ahead (PistolAI)
            for (int i = 0; i < _q.Count; i++)
            {
                var prism = _q[i];
                if (!Breakable(prism)) continue;
                Vector3 fr = _f.ToFrame(prism.transform.position);
                if (Mathf.Abs(fr.z) > Config.BodyHalfDepth * 2f) continue;
                _ai.Vis.Add(new PistolAI.Seen { X = fr.x, Y = fr.y, D = fr.z });
            }
            _q.Clear();
        }

        /// <summary>The Goby's locked RIVAL VESSEL is on the Pistol's screen: a ring fired now homes on it.</summary>
        public bool LockedInView()
        {
            if (!_lockIsVessel || !_lockTarget) return false;
            if (_cam && _cam.enabled)
            {
                Vector3 v = _cam.WorldToViewportPoint(_lockTarget.position);
                return v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
            }
            Vector3 fr = _f.ToFrame(_lockTarget.position);
            return Mathf.Abs(fr.x - _p.x) <= Config.ViewWidth * 0.5f && Mathf.Abs(fr.y - _p.y) <= Config.ViewWidth * 0.35f;
        }

        // ---- the Pistol's buttons on a phone (GobyPistolButtons): 0 ghost (held), 1 snap, 2 back. Pressed on the seat
        // directly: the seat's AI flies the hull meanwhile, and an InputStatus press would be dropped by its gate.
        void SetPistolButton(int button, bool down)
        {
            if (_goby == null || _human != Half.Pistol) return;
            if (button == 0) _touchGhost = down;
            else if (button == 1) _touchSnap = down;
            else if (button == 2 && down) _backPressed = true;
        }

        // On the fleet's four face buttons, the same ones the Goby's seat and every hull use, so each card's chip names its
        // control from the one glyph set (Space / A = Button1, T / B = Button2, Q / X = Button3, F / Y = Button4):
        // hold Space / A to dive (let go on an upslope and the hill throws it, Tiny Wings); T / B ghost; Q / X Snap; F / Y to
        // head back at a perch. The keys this seat used before the cards named them still work (G ghost, E Snap, the B key
        // and LB back). On a phone a finger on the Pistol's view dives. Only with aimedJump on: drag (or RT + right stick,
        // or J) aims a jump and letting go flings it (Garrett, 2026-10-22: "launching intentionally was wierd")
        PistolBodyMath.Input HumanPistol()
        {
            var inp = new PistolBodyMath.Input();
            var kb = Keyboard.current; var pad = Gamepad.current;
            float dive = 0f;
            if (_cam && ReadPointer(out var mp, out bool began, out bool held))
            {
                if (began) { _mouseDown = true; _aiming = false; _mouseFrom = mp; }
                if (_mouseDown)
                {
                    if (Config.AimedJump && (mp - _mouseFrom).magnitude > 12f) _aiming = true;
                    if (_aiming && PlanePoint(mp, out var w)) _mouseAim = new Vector2(w.x - _p.x, w.y - _p.y);
                    if (!_aiming) dive = 1f;
                    if (!held)
                    {
                        if (_aiming && _mouseAim.HasValue) { inp.HasLaunch = true; inp.Launch = _mouseAim.Value; }
                        _mouseDown = _aiming = false; _mouseAim = null;
                    }
                }
            }
            if (pad != null)
            {
                if (Config.AimedJump && pad.rightTrigger.ReadValue() > 0.5f) _padAim = pad.rightStick.ReadValue() * _body.pLaunchRange;
                else if (_padAim.HasValue) { if (_padAim.Value.magnitude > 10f) { inp.HasLaunch = true; inp.Launch = _padAim.Value; } _padAim = null; }
                if (pad.buttonSouth.isPressed) dive = 1f;
                dive = Mathf.Max(dive, pad.leftTrigger.ReadValue());
                inp.Ghost |= pad.buttonEast.isPressed;
                inp.Snap |= pad.buttonWest.isPressed;
                if (pad.buttonNorth.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame) _backPressed = true;
            }
            if (kb != null)
            {
                if (kb.spaceKey.isPressed || kb.sKey.isPressed || kb.downArrowKey.isPressed) dive = 1f;
                if (Config.AimedJump && kb.jKey.wasPressedThisFrame && !inp.HasLaunch) { inp.HasLaunch = true; inp.Launch = new Vector2(0.7f * _body.pLaunchRange, 0.45f * _body.pLaunchRange); }
                inp.Ghost |= kb.tKey.isPressed || kb.gKey.isPressed;
                inp.Snap |= kb.qKey.isPressed || kb.eKey.isPressed;
                if (kb.fKey.wasPressedThisFrame || kb.bKey.wasPressedThisFrame) _backPressed = true;
            }
            inp.Ghost |= _touchGhost;
            inp.Snap |= _touchSnap;
            inp.Dive = dive;
            if (inp.Snap) inp.SnapAim = _mouseAim ?? _padAim ?? new Vector2(1f, 0f);
            return inp;
        }

        // The Pistol's pointer: the mouse, or the ONE finger that started on the Pistol's view (by touch id, so a second
        // finger on the Ghost button never steals the drag). A press on the small view (the touch swap's) or on a seat
        // button (FlightTouchClaims) is never the Pistol's.
        bool ReadPointer(out Vector2 at, out bool began, out bool held)
        {
            at = _pointerAt; began = held = false;
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touches = touchscreen.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var t = touches[i];
                    int id = t.touchId.ReadValue();
                    if (_fingerId >= 0 ? id != _fingerId : !t.press.wasPressedThisFrame) continue;
                    var p = t.position.ReadValue();
                    if (_fingerId < 0)
                    {
                        if (!OnPistolView(p)) continue;
                        _fingerId = id; began = true;
                    }
                    at = _pointerAt = p;
                    held = t.press.isPressed;
                    if (!held) _fingerId = -1;
                    return true;
                }
                if (_fingerId >= 0) { _fingerId = -1; return true; }   // the finger is gone: a release where it last was
            }
            var mouse = Mouse.current;
            if (mouse == null) return false;
            at = _pointerAt = mouse.position.ReadValue();
            held = mouse.leftButton.isPressed;
            began = mouse.leftButton.wasPressedThisFrame && OnPistolView(at);
            return true;
        }

        bool OnPistolView(Vector2 screen) =>
            _cam.pixelRect.Contains(screen) && !_pip.SmallViewContains(_cam, HumanInSeat, screen) && !FlightTouchClaims.Claims(screen);

        /// <summary>A screen point on the Pistol's plane, in its frame (x, y).</summary>
        bool PlanePoint(Vector2 screen, out Vector2 frame)
        {
            frame = default;
            var ray = _cam.ScreenPointToRay(screen);
            float den = Vector3.Dot(ray.direction, _f.M);
            if (Mathf.Abs(den) < 1e-5f) return false;
            float s = Vector3.Dot(_f.O - ray.origin, _f.M) / den;
            if (s <= 0f) return false;
            Vector3 fr = _f.ToFrame(ray.origin + ray.direction * s);
            frame = new Vector2(fr.x, fr.y);
            return true;
        }

        // ================================ the undertow, the body, the rings ================================

        /// <summary>Space: mass either side of the Pistol's plane is pulled into it - never danger, super-shielded, a
        /// lifeform's body or the pair's own prisms; shielded ones only with the Space upgrade (Garrett, 2026-10-29: "the
        /// vortex rings are free to affect whatever they will": the path map's score counts one side, nothing here does).
        /// Crystals are drawn in too (<see cref="PullCrystals"/>).</summary>
        void Undertow(float dt)
        {
            if (Falling) return;   // nothing is pulled into a plane the Pistol has fallen out of
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;
            float reach = Mathf.Max(0f, Config.PullReach.EvaluateReplicated(_status)), window = Config.PullWindow;
            if (reach <= 0.5f) return;
            bool shielded = (Abilities && Abilities.IsUpgradeActive(Element.Space));
            index.QuerySphere(_p.wpos, Mathf.Sqrt(window * window + reach * reach), _q);
            int moved = 0;
            for (int i = 0; i < _q.Count && moved < Config.PullMaxPerFrame; i++)
            {
                var prism = _q[i];
                if (!Pullable(prism, shielded)) continue;
                var tr = prism.transform;
                Vector3 fr = _f.ToFrame(tr.position);
                if (Mathf.Abs(fr.x - _p.x) > window || Mathf.Abs(fr.z) > reach || Mathf.Abs(fr.z) < 0.5f) continue;
                float step = Mathf.Min(Mathf.Abs(fr.z), Config.PullSpeed * dt);
                tr.position -= _f.M * (Mathf.Sign(fr.z) * step);
                prism.NotifyPositionChanged();
                moved++;
            }
            _q.Clear();
            if (!_rider) PullCrystals(dt, reach, window);
        }

        /// <summary>
        /// Crystals beside the Pistol's plane drift into it, either side, within the undertow's reach and window (Garrett,
        /// 2026-10-29: "crystals are also attracted to the plane so pistol can collect them"), and one the Pistol's body
        /// touches is the pair's: an elemental crystal through the hull's skimmer, the omni crystal through the hull where it
        /// is decided (the Jacks' net's collect, <c>JacksNet.CollectByJacks</c>). The hull's machine only: a rider's machine
        /// would take a crystal its hull's machine never saw taken.
        /// </summary>
        void PullCrystals(float dt, float reach, float window)
        {
            var active = Crystal.Active;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (i >= active.Count) continue;
                var c = active[i];
                if (!c || !c.isActiveAndEnabled || c.IsExploding || c.IsEmbedded || !c.CanBeCollected(_status.Domain)) continue;
                var tr = c.transform;
                Vector3 fr = _f.ToFrame(tr.position);
                if (Mathf.Abs(fr.x - _p.x) > window || Mathf.Abs(fr.z) > reach) continue;
                if (Mathf.Abs(fr.z) > 0.25f) tr.position -= _f.M * (Mathf.Sign(fr.z) * Mathf.Min(Mathf.Abs(fr.z), Config.CrystalPullSpeed * dt));
                var size = tr.lossyScale;
                float touch = Config.BodyR + Config.CrystalTouchPad + 0.5f * Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                float dx = fr.x - _p.x, dy = fr.y - _p.y;
                if (Mathf.Abs(fr.z) <= touch && dx * dx + dy * dy <= touch * touch) CollectCrystal(c);
            }
        }

        void CollectCrystal(Crystal c)
        {
            if (_crystalTakenAt.TryGetValue(c, out float at) && _t - at < 0.5f) return;
            var impactor = c.GetComponentInChildren<CrystalImpactor>(true);
            if (impactor is ElementalCrystalImpactor elemental && !elemental.HasBeenCollected)
            {
                var skimmerOwner = _status.NearFieldSkimmer;
                var skimmer = skimmerOwner ? skimmerOwner.GetComponentInChildren<SkimmerImpactor>(true) : null;
                if (!skimmer) return;
                _crystalTakenAt[c] = _t;
                elemental.CollectBy(skimmer);
            }
            else if (impactor is OmniCrystalImpactor omni && ElementalTransfer.IsDecidedHere(_status) && c.CrystalManager)
            {
                var hull = _gt ? _gt.GetComponentInChildren<VesselImpactor>(true) : null;
                if (!hull) return;
                _crystalTakenAt[c] = _t;
                hull.AcceptImpacteeFromSweep(omni);
                c.CrystalManager.ReportRingThreaded(c.Id, c.transform.position, hull);
            }
        }

        bool Pullable(Prism prism, bool shieldedToo)
        {
            // a creature's or a plant's own body (a HealthPrism) is the ecosystem's to move, never the undertow's
            if (!Breakable(prism) || prism is HealthPrism || _held.Contains(prism) || prism.IsGrowing) return false;
            var props = prism.prismProperties;
            if (props.IsDangerous || props.IsSuperShielded) return false;
            return shieldedToo || !props.IsShielded;
        }

        /// <summary>A prism the Pistol may break: alive, not the pair's own (its ground, its dens, its perches).</summary>
        bool Breakable(Prism prism) => prism && !prism.destroyed && prism.prismProperties != null && prism.ownerID != _status.PlayerName;

        void BodyHits()
        {
            // sinking into a gap it touches nothing: no phantom hits
            if (_resting || _t < _p.ghostUntil || Falling) return;
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;
            index.QuerySphere(_p.wpos, Config.BodyR + Config.BodyHalfDepth + 6f, _q);
            Vector3 vel = PistolVelocity();
            for (int i = 0; i < _q.Count; i++)
            {
                var prism = _q[i];
                if (!Breakable(prism) || prism.prismProperties.IsSuperShielded) continue;
                if (!PistolBodyMath.Touches(_p, _f.ToFrame(prism.transform.position), Config.BodyHalfDepth, Config.BodyR)) continue;
                if (prism.DeflectsSilenced(_status.PlayerName, vel.magnitude)) continue;
                prism.Damage(vel, _status.Domain, _status.PlayerName);
                if (_p.grounded) _p.v *= 0.97f; else _p.vx *= 0.97f;
            }
            _q.Clear();
        }

        Vector3 PistolVelocity() => _p.grounded ? _f.R * _p.v : _f.R * _p.vx + _f.Up * _p.vy;

        void FireRing(Vector2 aim)
        {
            Vector3 n = aim.sqrMagnitude > 1f ? (_f.R * aim.x + _f.Up * aim.y).normalized : _f.R;
            var run = new RingRun { Ring = new VortexRing(_p.wpos, n, _vs.pVortexLife) };
            if (LockedInView()) run.Target = _lockVessel;   // the Goby's lock, on the Pistol's screen: the ring homes on it
            _rings.Add(run);
        }

        void StepRings(float dt)
        {
            var index = PrismSpatialIndex.Instance;
            var players = Config.GameData ? Config.GameData.Players : null;
            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                var run = _rings[i]; var ring = run.Ring;
                if (run.Target != null && UnityLiveness.Alive(run.Target.Transform)) ring.Steer(run.Target.Transform.position, Config.HomingTurnRate * Mathf.Deg2Rad * dt);
                bool alive = ring.Advance(dt, _vs);
                if (index && index.IsAvailable && !ring.Full(_vs))
                {
                    index.QuerySphere(ring.C, _vs.pVortexR + _vs.pVortexCapture, _q);
                    foreach (var prism in _q)
                    {
                        if (ring.Full(_vs)) break;
                        if (!Breakable(prism) || _held.Contains(prism) || prism.IsGrowing || prism.prismProperties.IsSuperShielded) continue;
                        if (ring.TryCapture(run.Prisms.Count, prism.transform.position, _vs)) { run.Prisms.Add(prism); _held.Add(prism); }
                    }
                    _q.Clear();
                }
                for (int j = 0; j < ring.Caps.Count; j++)
                {
                    var prism = run.Prisms[ring.Caps[j].Id]; if (!prism || prism.destroyed) continue;
                    var tr = prism.transform;
                    tr.position = ring.Flow(j, dt, _vs);
                    tr.Rotate(ring.N, 4f * dt * Mathf.Rad2Deg, Space.World);
                    prism.NotifyPositionChanged();
                }
                // a rival hull the ring passes through is a scored hit, and the ring collapses on it
                if (players != null)
                    foreach (var player in players)
                    {
                        var victim = EnemyStatus(player);
                        if (victim == null) continue;
                        float r = _vs.pVortexR + Config.RingHitPad;
                        if ((victim.Transform.position - ring.C).sqrMagnitude > r * r) continue;
                        ScoreRingHit(victim, ring.N * _vs.pVortexSpeed);
                        alive = false;
                        break;
                    }
                if (alive) continue;
                foreach (var prism in run.Prisms)
                {
                    _held.Remove(prism);
                    if (_vs.pVortexCollapse >= 0.5f && prism && !prism.destroyed && !prism.DeflectsSilenced(_status.PlayerName, 40f))
                        prism.Damage((prism.transform.position - ring.C).normalized * 40f, _status.Domain, _status.PlayerName);
                }
                VortexRingLens.Remove(ring);
                _rings.RemoveAt(i);
            }
        }

        /// <summary>A ring on a rival hull under the LOCKED PvP rule (petals only): admitted through the one gate every
        /// scored hit passes, it takes <c>ringPetals</c> (Charge) from each of the victim's elements, and raises the score.</summary>
        void ScoreRingHit(IVesselStatus victim, Vector3 velocity)
        {
            if (!ElementalTransfer.IsDecidedHere(_status)) return;
            var hitClass = Config.RingHitClass;
            if (!CombatHitDrain.TryAdmit(victim, _status, hitClass, Config.HitCooldown, ElementalDebuffSources.Other, out int superseded)) return;
            float petals = Mathf.Max(0f, Config.RingPetals.EvaluateReplicated(_status));
            ElementalTransfer.ApplyAllAuthoritative(ElementalTransferForm.Eject, victim, _status, petals * CombatHitDrain.NormalizedPerLevel,
                velocity, ElementalDebuffSources.Other);
            if (Config.OnCombatHitLanded)
                Config.OnCombatHitLanded.Raise(new CombatHitStats
                {
                    ShooterName = _status.PlayerName, VictimName = victim.PlayerName, HitClass = hitClass, SupersededRank = superseded,
                });
        }

        void ReleaseAllRings()
        {
            foreach (var run in _rings) { VortexRingLens.Remove(run.Ring); foreach (var prism in run.Prisms) _held.Remove(prism); }
            _rings.Clear(); _held.Clear();
        }

        // ================================ who flies what (the arena's swap) ================================

        void PollSwap()
        {
            // on a phone a double tap on the small view (every crew vessel's touch swap); the swap gesture (D-pad, the
            // left / right arrow keys, a two-finger swipe) is the person's, and CrewStations moves them here through TakeSeat
            bool tapped = _pipTap.Poll(showSideView ? _pip.SmallView(_cam, HumanInSeat) : null);
            if (!tapped || Time.unscaledTime < _swapReadyAt) return;
            TakeSeat(_human == Half.Goby ? 1 : 0);
        }

        /// <inheritdoc cref="ICrewStations.SeatCount"/>
        public int SeatCount => 2;

        /// <inheritdoc cref="ICrewStations.LocalSeat"/>
        public int LocalSeat => _goby != null && !_rider && !_aiCrew ? (_human == Half.Pistol ? 1 : 0) : -1;

        /// <inheritdoc cref="ICrewStations.HostsRiders"/>: the Pistol seat. It runs on the rider's machine, on the perches
        /// and the lock the hull's machine sends (<see cref="ICrewRelay"/>).
        public bool HostsRiders => true;

        /// <inheritdoc cref="ICrewStations.TakeSeat"/>
        public void TakeSeat(int seat)
        {
            var half = seat == 1 ? Half.Pistol : Half.Goby;
            if (_goby == null || _rider || _aiCrew || half == _human) return;
            if (half == Half.Pistol && CrewStations.HeldByRemoteHuman(this, 1)) return;   // a person on another machine flies it
            _swapReadyAt = Time.unscaledTime + PilotSwap.CooldownSeconds;
            SetHuman(half);
        }

        // ================================ the Pistol on another machine (ICrewRelay) ================================

        static int FloatBits(float v) => System.BitConverter.SingleToInt32Bits(v);
        static float BitsFloat(int v) => System.BitConverter.Int32BitsToSingle(v);

        // the hull's machine: every perch the Goby has made (where it sat, which way was up) and the rival it holds locked,
        // whenever either changes and once a second - the Pistol's ground and its rings' target on the rider's machine
        void SendPerches()
        {
            ulong lockId = _lockIsVessel && _lockVessel?.Vessel is { } v ? v.VesselNetId : 0;
            if (_perches.Count == _sentPerches && lockId == _sentLock && Time.time < _stateSendAt) return;
            _sentPerches = _perches.Count;
            _sentLock = lockId;
            _stateSendAt = Time.time + 1f;
            _stateScratch.Clear();
            _stateScratch.Add(_perches.Count);
            _stateScratch.Add((int)(lockId & 0xffffffffUL));
            _stateScratch.Add((int)(lockId >> 32));
            foreach (var den in _perches)
            {
                _stateScratch.Add(FloatBits(den.Pos.x)); _stateScratch.Add(FloatBits(den.Pos.y)); _stateScratch.Add(FloatBits(den.Pos.z));
                _stateScratch.Add(FloatBits(den.Up.x)); _stateScratch.Add(FloatBits(den.Up.y)); _stateScratch.Add(FloatBits(den.Up.z));
            }
            CrewRelay.SendState(this, _stateScratch.ToArray());
        }

        /// <inheritdoc cref="ICrewRelay.OnRiderInput"/>: the Pistol sends nothing the hull needs (its dens, undertow and
        /// rings act on this machine's prisms where it flies).
        public void OnRiderInput(int seat, int[] data) { }

        /// <inheritdoc cref="ICrewRelay.OnCrewState"/>: the Goby's perches and lock, from the hull's machine. Perches the
        /// rider already has keep their dens; new ones, and the darts between them, are added.
        public void OnCrewState(int[] data)
        {
            if (!_rider || _goby == null || data == null || data.Length < 3) return;
            int count = data[0];
            if (count < 1 || data.Length < 3 + count * 6) return;
            for (int k = 0; k < count; k++)
            {
                int i = 3 + k * 6;
                var pos = new Vector3(BitsFloat(data[i]), BitsFloat(data[i + 1]), BitsFloat(data[i + 2]));
                var up = new Vector3(BitsFloat(data[i + 3]), BitsFloat(data[i + 4]), BitsFloat(data[i + 5]));
                if (k < _perches.Count) { _perches[k].Pos = pos; _perches[k].Up = up; }
                else _perches.Add(new Den { Pos = pos, Up = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up });
            }
            if (_perches.Count > count) _perches.RemoveRange(count, _perches.Count - count);
            // the darts between them; the one the Pistol rides keeps its frame
            if (_segments.Count > count - 1)
            {
                int keep = count - 1, drop = _segments.Count - keep;
                _segments.RemoveRange(keep, drop); _tracks.RemoveRange(keep, drop); _grounds.RemoveRange(keep, drop); _groundBlocks.RemoveRange(keep, drop);
            }
            for (int k = _segments.Count; k < count - 1; k++)
            {
                _segments.Add(new PlaneSegment(_perches[k].Pos, _perches[k + 1].Pos, (_perches[k].Up + _perches[k + 1].Up).normalized));
                _tracks.Add(TrackFor(_segments[k], k));
                AddGround(_segments[k]);
            }
            if (_at >= _perches.Count) _at = _perches.Count - 1;
            if (_seg >= _segments.Count) { _seg = -1; _resting = true; }

            ulong lockId = (uint)data[1] | ((ulong)(uint)data[2] << 32);
            if (lockId != 0 && CrewStations.TryFindHull(lockId, out var target))
            {
                _lockTarget = target.Transform; _lockVessel = target.VesselStatus; _lockIsVessel = true;
            }
            else ClearLock();
        }

        /// <inheritdoc cref="IHullParkAware.ReturnHumanToHull"/>
        public void ReturnHumanToHull()
        {
            if (_human != Half.Pistol) return;
            SetHuman(Half.Goby);
        }

        void SetHuman(Half half)
        {
            // to the Pistol: the player's flight input paused, the seat's own Goby AI aiming and darting the hull; back: the
            // input given back. The player is captured on the way out: it outlives the hull (a swap re-homes it).
            if (half == Half.Pistol && _human != Half.Pistol)
            {
                _seatPlayer = _status?.Player;
                CrewSeat.SetFlightInputPaused(_seatPlayer, true);
            }
            else if (half == Half.Goby && _human == Half.Pistol)
            {
                CrewSeat.SetFlightInputPaused(_seatPlayer, false);
                _seatPlayer = null;
            }
            _human = half;
            // a press carried across the swap is not a press
            _ai?.ReleaseAll(); if (_p != null) { _p.prevGhost = _p.prevVortex = false; }
            _mouseDown = _aiming = false; _mouseAim = _padAim = null; _fingerId = -1;
            _dartHeld = _lockHeld = _prevLockHeld = _charging = false;
            _touchGhost = _touchSnap = _backPressed = false;
            _aiHasDir = false;
        }

        // each seat's buttons on a phone: built once the hull's HUD row is, painted for the seat the person is in
        void PaintButtons()
        {
            if (!_buttons.Built)
            {
                var input = _status?.Player?.InputStatus;
                if (!_buttons.TryBuild(_gt, Config, input, SetPistolButton)) return;
            }
            _buttons.Paint(HumanInSeat, DartReady, HasLock, _t >= _p.ghostAt, _t >= _p.vortexAt, HeadingBack);
        }

        // ================================ the views ================================

        void BuildView()
        {
            _view = new GameObject("PistolView");   // at the scene root: the hull's scale and pose are not the view's
            _cam = _view.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.Skybox; _cam.orthographic = false; _cam.nearClipPlane = 0.5f; _cam.farClipPlane = 6000f;
            _torus = BuildRingMesh(VortexRing.Tube, 24, 6);
            // the lock is a lit, faceted volume (nothing in the world looks flat); the vortex ring is no mesh at all but a
            // toroidal warp of what lies behind it (VortexRingLens, Garrett 2026-10-24: "loose the flat effect")
            _lockMat = Lit(new Color32(255, 92, 108, 255), new Color32(80, 16, 22, 255), 0.5f);
            BuildBody();
            _render = new CrewViewRender(_cam, RenderLook);
            TwoDCameraSky.Register(_cam);   // a 2D camera: no sky cell net glued to the side view (TwoDCameraSky)
            _camLive = false;
        }

        void LateUpdate()
        {
            if (_goby == null) return;
            PoseBody();
            if (!_cam) return;
            if (!showSideView) { _cam.enabled = false; _pip.Restore(); Draw3D(); return; }
            _pip.Layout(_cam, HumanInSeat, Config.PipSize, Config.ViewMargin);
            if (_choosing && _human == Half.Pistol) { MapView(); Draw3D(); return; }
            _cam.farClipPlane = 6000f;
            // the side camera, Tiny Wings' (Garrett, 2026-10-22): square to the plane and looking along +M from its back (so
            // the frame's x runs left to right), BESIDE the Pistol with an off-axis lens that looks ahead. It frames the
            // ground near the bottom with the Pistol a third of the way in, and zooms out as the Pistol speeds up and flies
            // high so the ground below never leaves the screen (PistolPlane.TinyWingsView); the camera pulls back with the
            // zoom, so the lens never changes. Zooming out is quick and back in slow, so a landing never snaps the view.
            float aspect = Mathf.Max(0.2f, _cam.pixelHeight > 0 ? (float)_cam.pixelWidth / _cam.pixelHeight : 1f);
            float speed = _p.grounded ? _p.v : Mathf.Sqrt(_p.vx * _p.vx + _p.vy * _p.vy);
            Vector2 centre = PistolPlane.TinyWingsView(_f, _p.x, _p.y, speed, Config.ViewWidth, Config.ViewMaxWidth, Config.ViewSpeedWiden,
                Config.ViewPistolAt, Config.ViewGroundedAt, Config.ViewHeadroom, aspect, out float wantW);
            _camWidth = !_camLive ? wantW : Mathf.Lerp(_camWidth, wantW, Mathf.Clamp01(Time.deltaTime * (wantW > _camWidth ? 3f : 0.8f)));
            float W = _camWidth, H = W / aspect, D = Mathf.Max(20f, Config.ViewDistance) * W / Mathf.Max(1f, Config.ViewWidth);
            Vector3 wantPos = _f.ToWorld(_p.x, centre.y) - _f.M * D, wantWindow = _f.ToWorld(centre.x, centre.y);
            var wantRot = Quaternion.LookRotation(_f.M, _f.Up);
            float k = Mathf.Clamp01(Time.deltaTime * 4f), kp = Mathf.Clamp01(Time.deltaTime * 6f);
            if (Popping) kp = Mathf.Clamp01(Time.deltaTime * 12f);   // out of the portal beyond the gap: the view catches up quickly
            _camRot = _camLive ? Quaternion.Slerp(_camRot, wantRot, k) : wantRot;
            _camPos = _camLive ? Vector3.Lerp(_camPos, wantPos, kp) : wantPos;
            _camWindow = _camLive ? Vector3.Lerp(_camWindow, wantWindow, kp) : wantWindow;
            _camLive = true;
            Vector3 w = Quaternion.Inverse(_camRot) * (_camWindow - _camPos);
            float near = _cam.nearClipPlane;
            PistolPlane.OffAxis(new Vector2(w.x, w.y), Mathf.Max(5f, w.z), W, H, near, out float fl, out float fr, out float fb, out float ft);
            _cam.projectionMatrix = Matrix4x4.Frustum(fl, fr, fb, ft, near, _cam.farClipPlane);
            _cam.transform.SetPositionAndRotation(_camPos, _camRot);
            Draw3D();
        }

        /// <summary>The Pistol's body: its model (Pistol.fbx) at its length, wearing the pair's domain on slot 0; a lit capsule
        /// stands in when the config names no model.</summary>
        void BuildBody()
        {
            var model = Config.PistolModel;
            _bodyIsModel = model;
            if (model)
            {
                _bodyGo = Instantiate(model);
                _bodyGo.name = "Pistol";
                foreach (var c in _bodyGo.GetComponentsInChildren<Collider>(true)) Destroy(c);
                foreach (var a in _bodyGo.GetComponentsInChildren<Animator>(true)) a.enabled = false;   // posed here, by bone
                float length = 0f;
                foreach (var r in _bodyGo.GetComponentsInChildren<Renderer>(true))
                {
                    // the mesh's own bounds: its node is identity in the model, nose on +Z (specs/pistol.py)
                    if (r is SkinnedMeshRenderer s && s.sharedMesh) { length = Mathf.Max(length, s.sharedMesh.bounds.size.z); s.updateWhenOffscreen = true; }
                    else if (r.TryGetComponent(out MeshFilter mf) && mf.sharedMesh) length = Mathf.Max(length, mf.sharedMesh.bounds.size.z);
                }
                _bodyDomain = null;
                PaintBodyDomain();
                _bodyScale = length > 1e-3f ? Config.PistolModelLength / length : 1f;
                _claw = FindBone(_bodyGo.transform, "claw");
                _tail = FindBone(_bodyGo.transform, "tail.1");
                if (_claw) _clawRest = _claw.localRotation;
                if (_tail) _tailRest = _tail.localRotation;
                return;
            }
            _bodyMat = Lit(new Color32(255, 138, 61, 255), new Color32(90, 34, 5, 255), 0.45f);
            _bodyGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _bodyGo.name = "Pistol";
            if (_bodyGo.TryGetComponent(out Collider col)) Destroy(col);
            if (_bodyGo.TryGetComponent(out MeshRenderer mr)) mr.sharedMaterial = _bodyMat;
        }

        /// <summary>The pair's domain on the model's slot 0 (the fleet's Domain slot), repainted when the hull's changes.
        /// Shared materials only: the domain material is a shared per-domain asset.</summary>
        void PaintBodyDomain()
        {
            var domain = _status?.ShipMaterial;
            if (!_bodyGo || !domain || domain == _bodyDomain) return;
            _bodyDomain = domain;
            foreach (var r in _bodyGo.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                if (mats.Length > 0) { mats[0] = domain; r.sharedMaterials = mats; }
            }
        }

        static Transform FindBone(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++) { var hit = FindBone(root.GetChild(i), name); if (hit) return hit; }
            return null;
        }

        /// <summary>Where the ride put the Pistol, nose along its travel on the plane (tipped with its flight or the slope
        /// under it), smaller while ghosting; the hammer claw shuts on a Snap and re-cocks, and the tail curls under on a
        /// dive.</summary>
        void PoseBody()
        {
            if (!_bodyGo) return;
            float ghost = _t < _p.ghostUntil ? 0.6f : 1f;
            // out of the portal beyond a gap it spins end over end, slowing to upright
            bool tumbling = Popping;
            float spin = tumbling ? Config.FallSpinDegrees * (1f - Mathf.SmoothStep(0f, 1f, _popT / Config.FallPopSeconds)) : 0f;
            if (!_bodyIsModel)
            {
                float r = Config.BodyR;   // the capsule: its long axis on the frame's x
                var capsule = Quaternion.LookRotation(_f.M, _f.R);
                if (tumbling) capsule = Quaternion.AngleAxis(spin, _f.M) * capsule;
                _bodyGo.transform.SetPositionAndRotation(_p.wpos, capsule);
                _bodyGo.transform.localScale = new Vector3(r * 2f, r * 1.6f, r * 2f) * ghost;
                return;
            }
            PaintBodyDomain();
            Vector2 d;
            if (_p.grounded) { PistolPlane.Ground(_f, _p.x, out float m); d = new Vector2(1f, m) * Mathf.Sign(_p.v == 0f ? _facing : _p.v); }
            else d = new Vector2(_p.vx, _p.vy);
            if (Mathf.Abs(d.x) > 0.05f) _facing = Mathf.Sign(d.x);
            if (d.sqrMagnitude < 1e-4f) d = new Vector2(_facing, 0f);
            Vector3 fwd = (_f.R * d.x + _f.Up * d.y).normalized;
            var root = _bodyGo.transform;
            var pose = Quaternion.LookRotation(fwd, _f.Up);
            if (tumbling) pose = Quaternion.AngleAxis(spin, _f.M) * pose;
            root.SetPositionAndRotation(_p.wpos, pose);
            root.localScale = Vector3.one * (_bodyScale * ghost);

            // the claw: shut on the Snap, held, then re-cocked (its hinge turns about the model's right axis)
            if (_claw)
            {
                float since = _t - _snapAt;
                float shut = since < 0f ? 0f : since <= Config.SnapClawHold ? 1f : 1f - Mathf.Clamp01((since - Config.SnapClawHold) / Config.SnapClawRecock);
                TurnAboutModelRight(root, _claw, _clawRest, shut * Config.SnapClawShut);
            }
            if (_tail)
            {
                _tailCurl = Mathf.MoveTowards(_tailCurl, Diving ? 1f : 0f, Time.deltaTime * Config.DiveTailRate);
                TurnAboutModelRight(root, _tail, _tailRest, -_tailCurl * Config.DiveTailCurl);   // minus: the tail's tip goes under
            }
        }

        /// <summary>Sets <paramref name="bone"/> to its rest pose turned <paramref name="degrees"/> about the model's own right
        /// axis (positive: the tip down and forward, the claw's shutting sense).</summary>
        static void TurnAboutModelRight(Transform root, Transform bone, Quaternion rest, float degrees)
        {
            var parent = bone.parent;
            var restWorld = (parent ? parent.rotation : Quaternion.identity) * rest;
            var turn = root.rotation * Quaternion.AngleAxis(degrees, Vector3.right) * Quaternion.Inverse(root.rotation);
            bone.rotation = turn * restWorld;
        }

        /// <summary>The path map: the camera pulls back to frame every perch, looking along the islands' planes (their
        /// hills side-on, the side view's language), with an ordinary lens.</summary>
        void MapView()
        {
            Vector3 lo = _perches[0].Pos, hi = lo, n = Vector3.zero, up = Vector3.zero;
            foreach (var d in _perches) { lo = Vector3.Min(lo, d.Pos); hi = Vector3.Max(hi, d.Pos); }
            for (int k = 0; k < _segments.Count; k++)
            {
                var f = PistolPlane.For(_segments[k], 1, Config.SlopeDegrees, null);
                n += Vector3.Dot(f.M, _f.M) >= 0f ? f.M : -f.M;
                up += f.Up;
            }
            if (n.sqrMagnitude < 1e-6f) n = _f.M;
            n.Normalize();
            up = Vector3.ProjectOnPlane(up.sqrMagnitude > 1e-6f ? up : _f.Up, n);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.up, n);
            Vector3 centre = (lo + hi) * 0.5f;
            float radius = 1f;
            foreach (var d in _perches) radius = Mathf.Max(radius, (d.Pos - centre).magnitude);
            radius += Config.PathScoreBand;
            _mapRadius = radius;
            float fov = Config.MapFov, dist = radius / Mathf.Sin(0.5f * fov * Mathf.Deg2Rad) * Config.MapMargin;
            // the person's spin: round the map's up, then tipped about the view's right
            up.Normalize();
            Vector3 right = Vector3.Cross(up, n).normalized;
            var spin = Quaternion.AngleAxis(_mapYaw, up) * Quaternion.AngleAxis(_mapPitch, right);
            Vector3 look = spin * n, lookUp = spin * up;
            var wantRot = Quaternion.LookRotation(look, lookUp);
            Vector3 wantPos = centre - look * dist;
            // the pull back eases in; once the map is open it follows the spin closely
            float k1 = Mathf.Clamp01(Time.deltaTime * (_chooseT < 0.6f ? 4f : 16f));
            _camRot = _camLive ? Quaternion.Slerp(_camRot, wantRot, k1) : wantRot;
            _camPos = _camLive ? Vector3.Lerp(_camPos, wantPos, k1) : wantPos;
            _camLive = true;
            _cam.farClipPlane = Mathf.Max(6000f, dist + radius * 2f);
            _cam.ResetProjectionMatrix();
            _cam.fieldOfView = fov;
            _cam.transform.SetPositionAndRotation(_camPos, _camRot);
            DrawMap();
        }

        /// <summary>
        /// The paths as facts in the world, for the Pistol's camera only, by HALF PLANE (Garrett, 2026-10-29: "the pistol
        /// should see the plane it should show one side of the trail when traveling one way and pass on the other side when
        /// traveling the other direction. the two different half planes available should signal their mass separately in a
        /// more 3d effect that obeys the no flat hypersea rule"): each island's trail is a thin lit spine, and each of its two
        /// halves a terraced WING of lit, faceted blocks standing out of the trail on its own side, wider and in more tiers
        /// (one to <see cref="WingTiers"/>) and from the neutral blue toward the call-to-action lime as that side holds more
        /// opposing prisms. The picked half is white, with a pointer at its den along the way it rides.
        /// </summary>
        void DrawMap()
        {
            EnsureMapAssets();
            foreach (var list in _heatPieces) list.Clear();
            _pickPieces.Clear();
            float spine = MapSpine;
            bool pickReady = PistolPaths.Ready(_choice, _segments.Count);
            for (int k = 0; k < _segments.Count; k++)
            {
                AddPath(k, spine, _heatPieces[0], spine);
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    float heat = HalfHeat(k, dir);
                    int level = Mathf.Clamp(Mathf.RoundToInt(heat * (_heatPieces.Length - 1)), 0, _heatPieces.Length - 1);
                    bool picked = pickReady && PistolPaths.Half(_choice) == PistolPaths.Half(k, dir);
                    AddWing(k, dir, heat, picked ? _pickPieces : _heatPieces[level]);
                }
            }
            // the pointer: at the picked den, along the way it rides (the head's "ahead" waits for the Goby's next island)
            float thick = _mapRadius * Config.MapPathThickness;
            Vector3 den = _perches[Mathf.Clamp(_choice.Perch, 0, _perches.Count - 1)].Pos, tip = OptionAnchor(_choice);
            Vector3 dir2 = tip - den;
            if (dir2.sqrMagnitude > 1e-4f)
            {
                float len = Mathf.Min(dir2.magnitude, _mapRadius * 0.1f), head = thick * 4f;
                var rot = Quaternion.LookRotation(dir2.normalized, _cam ? -_cam.transform.forward : Vector3.up);
                _pickPieces.Add(Matrix4x4.TRS(den + dir2.normalized * (len - head * 0.5f), rot, new Vector3(head, head * 0.4f, head)));
                _pickPieces.Add(Matrix4x4.TRS(den, Quaternion.identity, Vector3.one * thick * 3f));   // the den it sets off from
            }
            var rp = new RenderParams(_heatMats[0]) { camera = _cam, shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
            for (int i = 0; i < _heatPieces.Length; i++)
            {
                rp.material = _heatMats[i];
                DrawPieces(rp, _heatPieces[i]);
            }
            rp.material = _pickMat;
            DrawPieces(rp, _pickPieces);
        }

        // the most blocks one instanced draw takes
        const int InstanceBatch = 1023;

        void DrawPieces(RenderParams rp, List<Matrix4x4> pieces)
        {
            for (int start = 0; start < pieces.Count; start += InstanceBatch)
                Graphics.RenderMeshInstanced(rp, _cube, 0, pieces, Mathf.Min(InstanceBatch, pieces.Count - start), start);
        }

        /// <summary>The most tiers a half's wing steps up in (its fullest).</summary>
        const int WingTiers = 4;

        /// <summary>One half's wing along island <paramref name="k"/>: per piece of the island, <c>1 + heat (tiers - 1)</c>
        /// stacked slabs out of the trail on the half's side, each tier narrower and higher than the one below (a terrace
        /// that rises toward the trail), so a heavier half is a wider, taller volume.</summary>
        void AddWing(int k, int dir, float heat, List<Matrix4x4> into)
        {
            var seg = _segments[k]; var track = _tracks[k];
            Vector3 side = HalfSide(k, dir);
            float reach = WingReach(heat), spine = MapSpine, tierH = Mathf.Max(spine, reach * 0.22f);
            int tiers = 1 + Mathf.RoundToInt(heat * (WingTiers - 1));
            int pieces = Mathf.Clamp(Mathf.CeilToInt(seg.L / 80f), 4, 16);
            Vector3 a = seg.Ground(0f, track);
            for (int i = 1; i <= pieces; i++)
            {
                Vector3 b = seg.Ground(seg.L * i / pieces, track), d = b - a;
                if (d.sqrMagnitude > 1e-4f)
                {
                    var rot = Quaternion.LookRotation(d, side);   // local y out of the plane, z along the island
                    for (int t = 0; t < tiers; t++)
                    {
                        float w = reach * (1f - t / (float)(tiers + 1));
                        Vector3 centre = (a + b) * 0.5f + side * (spine + 0.5f * w) + seg.N * (tierH * (t + 0.5f));
                        into.Add(Matrix4x4.TRS(centre, rot, new Vector3(tierH, w, d.magnitude * 0.92f)));
                    }
                }
                a = b;
            }
        }

        /// <summary>Island <paramref name="k"/>'s trail on the map: a thin lit bar along its ground.</summary>
        void AddPath(int k, float w, List<Matrix4x4> into, float drawW)
        {
            var seg = _segments[k]; var track = _tracks[k];
            int pieces = Mathf.Clamp(Mathf.CeilToInt(seg.L / 40f), 4, 32);
            Vector3 lift = seg.N * drawW;   // just above the trail, so the ground's blocks never hide it
            Vector3 a = seg.Ground(0f, track) + lift;
            for (int i = 1; i <= pieces; i++)
            {
                Vector3 b = seg.Ground(seg.L * i / pieces, track) + lift;
                Vector3 d = b - a;
                if (d.sqrMagnitude > 1e-4f)
                    into.Add(Matrix4x4.TRS((a + b) * 0.5f, Quaternion.LookRotation(d, seg.N), new Vector3(drawW, drawW, d.magnitude + drawW * 0.5f)));
                a = b;
            }
        }

        void EnsureMapAssets()
        {
            if (_heatMats != null) return;
            if (!_cube) _cube = CubeMesh();
            _heatMats = new Material[_heatPieces.Length];
            for (int i = 0; i < _heatMats.Length; i++)
            {
                var c = Color.Lerp(Config.MapPathColdColor, Config.MapPathHotColor, i / (float)(_heatMats.Length - 1));
                _heatMats[i] = Lit(c, c * 0.45f, 0.55f);
            }
            var pick = Config.MapPickColor;
            _pickMat = Lit(pick, pick * 0.6f, 0.6f);
        }

        /// <summary>A unit cube with its own normals per face: a faceted, lit bar piece.</summary>
        static Mesh CubeMesh()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh = Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            Destroy(go);
            return mesh;
        }

        /// <summary>
        /// The bursts of rewarded blocks, for the Pistol's camera: each block flashes (a lit shell
        /// <c>juicePulseScale</c> its size shrinking onto it over the first 40 %) and throws <c>juiceShards</c> lit, faceted
        /// shards in the Pistol's plane, spinning and shrinking as they fly <c>juiceReach</c> out; a restored block's shards
        /// fly IN and land on it. Grown and restored in the pair's domain colour, lit; shielded in the call-to-action lime
        /// (Garrett, 2026-10-29: "cyan = jade which is a domain color. use Lime for shield. a lit domain color for grow and
        /// restore"); emissive so they bloom. The flight's blocks are rewarded when it lands, as a ripple back over the jump.
        /// </summary>
        void DrawJuice()
        {
            if (_bursts.Count == 0 || !_cam) return;
            EnsureJuiceAssets();
            foreach (var list in _juicePieces) list.Clear();
            float life = Mathf.Max(0.05f, Config.JuiceSeconds), reach = Config.JuiceReach, size = Config.JuiceShardSize;
            int shards = Config.JuiceShards;
            for (int i = _bursts.Count - 1; i >= 0; i--)
            {
                var b = _bursts[i];
                float age = _t - b.Born;
                if (age >= life || age < 0f) { _bursts.RemoveAt(i); continue; }
                float k = age / life, outE = 1f - (1f - k) * (1f - k);
                var into = _juicePieces[(int)b.Kind];
                if (k < 0.4f) into.Add(Matrix4x4.TRS(b.At, b.Rot, b.Scale * Mathf.Lerp(Config.JuicePulseScale, 1.02f, k / 0.4f)));
                bool inward = b.Kind == Juice.Restore;
                for (int j = 0; j < shards; j++)
                {
                    // spread over the upper half of the plane (the side the Pistol sees above its ground), a little jitter
                    float a = Mathf.PI * ((j + 0.5f) / shards) + (Hash01(b.Seed, j) - 0.5f) * 0.6f;
                    float far = reach * (0.6f + 0.4f * Hash01(b.Seed, j + 31));
                    Vector3 dir = b.R * Mathf.Cos(a) + b.Up * Mathf.Sin(a);
                    float d = inward ? far * (1f - outE) : far * outE;
                    float sz = size * (inward ? Mathf.Lerp(0.5f, 1f, outE) * (k < 0.85f ? 1f : (1f - k) / 0.15f) : 1f - k);
                    if (sz <= 1e-3f) continue;
                    var rot = Quaternion.AngleAxis((inward ? -1f : 1f) * 720f * age + 57f * j, b.M) * Quaternion.LookRotation(dir, b.M);
                    into.Add(Matrix4x4.TRS(b.At + dir * d, rot, Vector3.one * sz));
                }
            }
            var rp = new RenderParams(_juiceMats[0]) { camera = _cam, shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
            for (int i = 0; i < _juicePieces.Length; i++)
            {
                rp.material = _juiceMats[i];
                DrawPieces(rp, _juicePieces[i]);
            }
        }

        void EnsureJuiceAssets()
        {
            if (!_cube) _cube = CubeMesh();
            if (_juiceMats == null)
            {
                _juiceMats = new Material[3];
                for (int i = 0; i < 3; i++) _juiceMats[i] = Lit(Color.white, Color.white, 0.7f);
                PaintJuice(_juiceMats[(int)Juice.Shield], Config.JuiceShieldColor);
                _juiceDomain = (Domains)(-1);
            }
            // grown and restored mass is the PAIR's: lit in its domain's colour (the palette's one accessor for a light that
            // wears its owner's domain), repainted when the pair changes domain; shielding is the call-to-action lime
            var domain = _status != null ? _status.Domain : Domains.Blue;
            if (domain == _juiceDomain) return;
            _juiceDomain = domain;
            var tint = PrismLit.DomainTint(domain);
            PaintJuice(_juiceMats[(int)Juice.Grow], tint);
            PaintJuice(_juiceMats[(int)Juice.Restore], tint);
        }

        static void PaintJuice(Material m, Color c)
        {
            m.color = c;
            if (m.HasProperty(s_baseColor)) m.SetColor(s_baseColor, c);
            if (m.HasProperty(s_emission)) m.SetColor(s_emission, c * 1.6f);
        }

        static float Hash01(uint seed, int i)
        {
            uint h = unchecked(seed ^ ((uint)i * 0x9E3779B1u));
            h ^= h >> 15; h = unchecked(h * 0x2C1B3C6Du); h ^= h >> 12; h = unchecked(h * 0x297A2D39u); h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }

        void Draw3D()
        {
            DrawJuice();
            // each vortex ring bends the light behind it round its core: a subtle toroidal lens, the black holes' kin
            foreach (var run in _rings)
                VortexRingLens.Set(run.Ring, run.Ring.C, run.Ring.N, _vs.pVortexR, _vs.pVortexR * VortexRing.Tube,
                    VortexRingLens.LifeFade(run.Ring.Age, run.Ring.Life), VortexRingLens.DefaultTint, _vs.pVortexSpin);
            // the portals of a fall through a gap: where it went in and where it came out, facing the side view
            if (_popOut != null)
            {
                float life = Config.FallPopSeconds, r = Config.FallRingRadius, fade = VortexRingLens.LifeFade(_popT, life - _popT, 0.08f, 0.3f);
                VortexRingLens.Set(_popOut, _popOutAt, _f.M, r, r * VortexRing.Tube * 2f, fade, VortexRingLens.DefaultTint, _vs.pVortexSpin);
                VortexRingLens.Set(_popIn, _popInAt, _f.M, r, r * VortexRing.Tube * 2f, fade, VortexRingLens.DefaultTint, -_vs.pVortexSpin);
            }
            // the lock is a fact in the world, so it is drawn there: a lit ring round the locked target, facing the Goby
            if (_lockTarget)
            {
                Vector3 to = _gt.position - _lockTarget.position;
                if (to.sqrMagnitude > 1e-4f) Draw(_torus, Matrix4x4.TRS(_lockTarget.position, Quaternion.LookRotation(to.normalized), Vector3.one * 9f), _lockMat);
            }
        }

        static void Draw(Mesh mesh, Matrix4x4 m, Material material)
        {
            if (!mesh || !material) return;
            var rp = new RenderParams(material) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
            Graphics.RenderMesh(rp, mesh, 0, m);
        }

        // ---- how the Pistol's camera sees the game's prisms, for its render only (CrewViewRender, the crew vessels' one
        // look): their depth look measured from the Pistol's plane, and the occlusion corridor aimed from this lens at the
        // Pistol, so mass in front of it dithers instead of hiding it
        CrewViewRender.Look RenderLook()
        {
            var look = new CrewViewRender.Look { PlanePoint = _p.wpos, PlaneNormal = _f.M, FullSpreadAt = Config.DepthFullSpreadAt };
            var corridor = PrismOcclusionCorridor.Config;
            if (_gt && corridor && corridor.Enabled)
            {
                float r = Mathf.Clamp(Config.BodyR, corridor.MinVesselRadius, corridor.MaxVesselRadius);
                look.Hull = _gt;
                look.CorridorTarget = _p.wpos;
                look.OuterRadius = r * corridor.OuterRadiusScale;
                look.InnerRadius = r * corridor.InnerRadiusScale;
                look.CoreAlpha = corridor.CoreAlpha;
                look.NearRadius = corridor.NearRadius(r);
            }
            return look;
        }

        /// <summary>A lit material for a world effect (nothing in the world looks flat, LOCKED 2026-10-18).</summary>
        static Material Lit(Color32 color, Color32 emission, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (!shader) shader = Shader.Find("Standard");
            var m = new Material(shader) { color = color, enableInstancing = true };
            if (m.HasProperty(s_baseColor)) m.SetColor(s_baseColor, color);
            if (m.HasProperty(s_smoothness)) m.SetFloat(s_smoothness, smoothness);
            if (m.HasProperty(s_metallic)) m.SetFloat(s_metallic, 0.1f);
            if (m.HasProperty(s_emission)) { m.EnableKeyword("_EMISSION"); m.SetColor(s_emission, emission); }
            return m;
        }

        static readonly int s_baseColor = Shader.PropertyToID("_BaseColor"), s_smoothness = Shader.PropertyToID("_Smoothness"),
                            s_metallic = Shader.PropertyToID("_Metallic"), s_emission = Shader.PropertyToID("_EmissionColor");

        static Mesh BuildRingMesh(float tube, int seg, int side)
        {
            var v = new Vector3[VortexRing.FacetVertexCount(seg, side)]; var n = new Vector3[v.Length]; var tri = new int[v.Length];
            VortexRing.Facets(tube, seg, side, v, n);
            for (int k = 0; k < tri.Length; k++) tri[k] = k;
            var m = new Mesh { name = "VortexRing", vertices = v, normals = n, triangles = tri };
            m.RecalculateBounds();
            return m;
        }

        // ================================ geometry (ShrikeRules on the prism's box) ================================

        static Vector3 HalfExtents(Prism prism) => prism.transform.lossyScale * 0.5f;

        static float BoxDistance(Prism prism, Vector3 world)
        {
            var t = prism.transform;
            return ShrikeRules.BoxDistance(Quaternion.Inverse(t.rotation) * (world - t.position), HalfExtents(prism));
        }

        static bool RayHits(Prism prism, Vector3 origin, Vector3 dir, float pad, float maxT, out float entry)
        {
            var t = prism.transform;
            var inv = Quaternion.Inverse(t.rotation);
            return ShrikeRules.TryRayBox(inv * (origin - t.position), inv * dir, HalfExtents(prism), pad, maxT, out entry);
        }

        void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_goby == null) return;
            var box = new Rect(10f, 10f, 520f, 96f);
            GUI.Box(box, string.Empty);
            GUILayout.BeginArea(new Rect(box.x + 8f, box.y + 6f, box.width - 16f, box.height - 12f));
            GUILayout.Label($"GOBY & PISTOL   you fly: {(_human == Half.Goby ? "GOBY" : "PISTOL")}   AI: {aiLevel}   swap: D-pad left/right, 1/2 or double tap the small view");
            GUILayout.Label($"Goby    route {(_aiHasRoute ? _aiRouteCount + " opposing" : "none")}   perches {_perches.Count}   dart {(DartReady ? "ready" : "...")}{(_charging ? $" charging {DartCharge01:0.00}" : "")}   lock {(_lockTarget ? (_lockIsVessel ? "vessel" : "fauna") : "none")}");
            GUILayout.Label($"Pistol  {(_resting ? $"resting at perch {_at}" : $"riding {_seg} {(_dir > 0 ? "on" : "back")}")}   jump {_p.launch}   den prisms {DenPrisms}   heading {(_travelDir > 0 ? "on" : "back")}   rings {_rings.Count}");
            GUILayout.Label(_human == Half.Pistol
                ? (_choosing
                    ? "Pistol at a perch: the map - drag / right stick / Q E spins; tap a path / left stick / A D picks; tap it again / A / Space rides; Back / Y / F turns round"
                    : "Pistol: hold Space / mouse / finger / A to dive; T / B ghost; Q / X Snap; F / Y back; with aimed jumps on, drag (or RT + right stick, or J) and let go")
                : "Goby: aim the hull; hold Q / X / RT to charge a dart, let go to dart; F / Y / LT lock");
            GUILayout.EndArea();
#endif
        }
    }
}
