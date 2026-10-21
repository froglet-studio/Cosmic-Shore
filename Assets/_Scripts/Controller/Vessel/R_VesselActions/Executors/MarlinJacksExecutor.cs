using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using FMODUnity;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Marlin and Jacks (vessel class 22; design <c>R_VesselActions/MARLIN_JACKS.md</c>, lab
    /// <c>Docs/Studios/SWARM_VESSEL_LAB.md</c>): a marlin (the Rhino's hull, its speed and its straight-line zoom) and two
    /// jacks that HERD real fauna. Every herder - the marlin, both jacks and the one decoy - repels the herd inside a ring
    /// at least a big thumb wide; the herd is drawn to the marlin's x-z plane, where the jacks run.
    ///
    /// <list type="bullet">
    /// <item><b>The jacks</b> are thumbs: each touch takes the nearest jack and it runs to the point under the thumb; lifting
    /// the thumb leaves the ONE decoy where that jack stands (a new decoy replaces the old). On a gamepad the left stick
    /// runs the left jack and the right stick the right one, and the bumper or trigger on that side is the lift.</item>
    /// <item><b>The marlin</b> flies the Rhino's two sticks and ramp. It can CALL the herd to itself (a long cooldown) and
    /// TELEPORT to the decoy (<see cref="MarlinJacksActionSO"/>).</item>
    /// <item><b>Seats:</b> the swap gesture (D-pad left / right, keys 1 / 2, or a double tap on the small view) moves the
    /// local human between the marlin and the jacks. The empty seat is an AI: the hull's own <c>AIPilot</c>, or the jacks'
    /// drive-and-collect AI (<see cref="MarlinJacksAI"/>), which plays through aim points only, as thumbs do.</item>
    /// </list>
    ///
    /// <para><b>The herd is a special swarm</b> (<see cref="IFaunaHerd"/>; the designer, 2026-10-21): the wild herbivores a
    /// herder's ring touches join it, its offspring join it, it starves only down to <see cref="MarlinJacksConfigSO.MinHerd"/>
    /// (and an empty herd has that many born to it), and its holder's level-5 upgrades break rules: Space 5 eats shielded
    /// prisms, Charge 5 attacks rivals, Mass 5 lays our mass. Otherwise every creature keeps its own diet, mouthful and
    /// stomach.</para>
    ///
    /// <para><b>Elements</b> (Garrett, 2026-10-20): Mass the herd's birth rate, Time its speed, Space every herder's ring,
    /// Charge the share of DANGER creatures (0% at -5, 100% at 15), whose touch on a rival hull is a scored Strike that
    /// spends the creature (PvP is petals only).</para>
    ///
    /// <para><b>Local only, as the Wrasse's seat is.</b> The herd is steered wherever the creatures are simulated (only that
    /// peer can leash them), and the jacks, the decoy and the views are this machine's. Networking the jacks is a
    /// follow-up (MARLIN_JACKS.md).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MarlinJacksExecutor : ShipActionExecutorBase, IFaunaHerd
    {
        [Header("Wiring (serialized on the prefab so a missing wire shows in the inspector)")]
        [Tooltip("Every number (MarlinJacksConfig.asset, the Swarm Vessel Lab's shipped values).")]
        [SerializeField] MarlinJacksConfigSO config;
        [Tooltip("The fleet's shared turn-end channel: the herd is let go.")]
        [SerializeField] ScriptableEventNoParam OnMiniGameTurnEnd;
        [Tooltip("Drag Event_CombatHitStats.asset - the channel StatsManager listens on. A danger creature's sting and a Charge " +
                 "level-5 bite are scored Strikes (PvP is petals only).")]
        [SerializeField] ScriptableEventCombatHitStats onCombatHitLanded;

        [Header("Presentation (game only; the lab's colours)")]
        [Tooltip("The jacks (the lab's #f2c84b).")]
        [SerializeField] Color jackColor = new(0.949f, 0.784f, 0.294f, 1f);
        [Tooltip("The decoy (the lab's #f0a043).")]
        [SerializeField] Color decoyColor = new(0.941f, 0.627f, 0.263f, 1f);
        [Tooltip("The marlin's mark on the jacks' view (the lab's #1fb8b0).")]
        [SerializeField] Color marlinColor = new(0.122f, 0.722f, 0.69f, 1f);
        [Tooltip("A danger creature's mark (the lab's #ff5a3c).")]
        [SerializeField] Color dangerColor = new(1f, 0.353f, 0.235f, 1f);
        [Tooltip("The rings' dots.")]
        [SerializeField] Color ringColor = new(0.949f, 0.784f, 0.294f, 0.55f);

        sealed class Member
        {
            public Fauna Creature;
            public float Roll;        // 0..1 for life: whether it is danger at the current Charge, and its wander
            public float Phase;       // the wander's phase
            public Prism Meal;        // the prism it is ordered to graze (null while it panics)
            public float MealAt;      // when the meal is next re-picked
            public Vector3 LastPos;
            public Vector3 Velocity;
            public Vector3 Desired;   // the velocity it heads for this frame
        }

        IVesselStatus _status;
        IVessel _vessel;
        readonly List<Member> _herd = new();               // join order: the first MinHerd are the floor (Sustains)
        readonly Dictionary<Fauna, Member> _byCreature = new();
        readonly List<Member> _scratch = new();
        readonly List<Prism> _query = new();
        readonly List<VesselStatus> _vessels = new();
        float _vesselScanAt, _recruitAt, _floorBirthAt, _pooAt;
        bool _seeding;
        uint _pooSerial;

        // the board: the marlin's x-z plane, eased toward the hull's (the Wrasse's Eased frame, KiteBoardFrame.Eased) - fast
        // while the human flies the marlin, slow while they run the jacks so the marlin's roll and pitch barely move their view
        Vector3 _r = Vector3.right, _f = Vector3.forward, _u = Vector3.up;
        bool _snapped;

        // the jacks and the decoy
        readonly Vector3[] _jack = new Vector3[2];
        readonly Vector3[] _jackTarget = new Vector3[2];
        readonly bool[] _jackDown = new bool[2];
        readonly bool[] _jackWasDown = new bool[2];
        bool _hasDecoy;
        Vector3 _decoy;

        // the marlin
        float _callUntil, _callReadyAt, _decoyReadyAt, _teleReadyAt, _aiDecoyAt;

        // seats
        bool _humanRunsJacks, _weTookHull;
        float _swapReadyAt;
        MarlinJacksAI _ai;
        MarlinJacksView _view;
        AnglerWorldMarks _marks;

        // the human's thumbs, mouse and sticks on the jacks
        readonly Dictionary<int, int> _pointerJack = new();   // touch id (-1 = the mouse) -> jack
        readonly int[] _padJack = { 0, 1 };                    // stick -> jack
        readonly bool[] _padLiftWas = new bool[2];
        const int MouseId = -1;

        // ------------------------------------------------------------------ read by the HUD and the action SOs

        public int HerdCount => _herd.Count;
        public int MinHerd => config ? config.MinHerd : 0;
        public bool Calling => Time.time < _callUntil;
        /// <summary>0 when the call is ready, 1 just after it was used.</summary>
        public float CallCooldown01 => config && config.CallCooldown > 0f ? Mathf.Clamp01((_callReadyAt - Time.time) / config.CallCooldown) : 0f;
        public bool HasDecoy => _hasDecoy;
        public bool HumanRunsJacks => _humanRunsJacks;
        /// <summary>The share of the herd that is danger at the current Charge.</summary>
        public float DangerShare => MarlinJacksMath.DangerShare(Level(Element.Charge));
        /// <summary>Every herder's ring now (u).</summary>
        public float Ring => config ? MarlinJacksMath.Ring(config.RingFloor, Level(Element.Space)) : 0f;
        internal MarlinJacksConfigSO Config => config;
        internal Vector3 BoardUp => _u;
        internal Vector3 BoardForward => _f;
        internal Vector3 BoardRight => _r;
        internal Vector3 MarlinPosition => _status != null && _status.Transform ? _status.Transform.position : transform.position;
        internal Vector3 HerdCentre { get; private set; }

        // ------------------------------------------------------------------ lifecycle

        public override void Initialize(IVesselStatus shipStatus)
        {
            ReleaseAll();
            LeaveJackSeat();
            _status = shipStatus;
            _vessel = _status != null && _status.Transform ? _status.Transform.GetComponent<IVessel>() : null;
            _snapped = false;
            _hasDecoy = false;
            _callUntil = _callReadyAt = _decoyReadyAt = _teleReadyAt = _aiDecoyAt = 0f;
            _recruitAt = _floorBirthAt = 0f;
            _pooAt = Time.time + (config ? config.PooEvery : 6f);
            _seeding = false;
            _pointerJack.Clear();
            _ai = new MarlinJacksAI(System.Environment.TickCount);
            for (int k = 0; k < 2; k++) _jackDown[k] = _jackWasDown[k] = false;
            PlaceJacks();
            if (!config)
                CSDebug.LogError($"[MarlinJacks] {name}: no MarlinJacksConfig - the jacks cannot run and the herd cannot be " +
                                 "steered. Wire Assets/_SO_Assets/VesselActions/MarlinJacks/MarlinJacksConfig.asset.");
        }

        void OnEnable()
        {
            if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised += OnTurnEnd;
        }

        void OnDisable()
        {
            if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised -= OnTurnEnd;
            ReleaseAll();
            LeaveJackSeat();
            _view?.Dispose();
            _view = null;
        }

        void OnDestroy()
        {
            _marks?.Dispose();
            _marks = null;
            _view?.Dispose();
            _view = null;
        }

        void OnTurnEnd()
        {
            ReleaseAll();
            _hasDecoy = false;
        }

        /// <summary>The local human owns this hull (not an AI, not a remote copy).</summary>
        bool IsLocalHuman => _status != null && _status.Player != null && !_status.IsInitializedAsAI && _status.IsLocalUser;

        float Level(Element e)
        {
            var resources = _status?.ResourceSystem;
            return resources ? resources.GetNormalizedLevel(e) * 10f : 0f;
        }

        bool Upgraded(Element e)
        {
            var abilities = _status?.ElementalAbilityHandler;
            return abilities && abilities.IsUpgradeActive(e);
        }

        // ------------------------------------------------------------------ the marlin's controls (MarlinJacksActionSO)

        /// <summary>The CALL: for <c>callTime</c> the herd is pulled to the marlin, which stops repelling; then a long cooldown.</summary>
        public void Call()
        {
            if (!config || Time.time < _callReadyAt) return;
            _callUntil = Time.time + config.CallTime;
            _callReadyAt = Time.time + config.CallCooldown;
            Play(config.CallSfx, MarlinPosition);
        }

        /// <summary>The marlin TELEPORTS to the decoy, keeping its heading; the decoy is spent. On the machine that owns the
        /// hull (its transform replicates).</summary>
        public void Teleport()
        {
            if (!config || !_hasDecoy || Time.time < _teleReadyAt || _vessel == null || _status == null) return;
            if (!ElementalTransfer.IsDecidedHere(_status)) return;
            var t = _status.Transform;
            if (!t) return;
            Play(config.TeleportSfx, t.position);
            _vessel.SetPose(new Pose(_decoy, t.rotation));
            _teleReadyAt = Time.time + config.TeleportGuard;
            _hasDecoy = false;
        }

        /// <summary>TIME level 5: the marlin drops THE decoy where it is (the jacks' one decoy; a new one replaces the old).</summary>
        public void DropMarlinDecoy()
        {
            if (!config || !Upgraded(Element.Time) || Time.time < _decoyReadyAt) return;
            DropDecoy(OnPlane(MarlinPosition));
            _decoyReadyAt = Time.time + config.DecoyCooldown;
        }

        void DropDecoy(Vector3 at)
        {
            _decoy = at;
            _hasDecoy = true;
            if (config) Play(config.DecoySfx, at);
        }

        // ------------------------------------------------------------------ the step

        void Update()
        {
            if (_status == null || !config || !_status.Transform) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            PruneDead();
            UpdateBoard(dt);
            if (IsLocalHuman) PollSwap();
            else if (_humanRunsJacks) LeaveJackSeat();

            float ring = Ring;
            if (_humanRunsJacks) ReadHumanJacks(dt, ring);
            else StepAIJacks(dt, ring);
            StepJacks(dt);
            if (_hasDecoy) _decoy = OnPlane(_decoy);
            if (_status.AutoPilotEnabled) StepAIMarlin();

            EnsureFloor();
            Recruit(ring);
            StepHerd(dt, ring);
            UpdateContacts();
            LayMass();
        }

        void LateUpdate()
        {
            if (_status == null || !config || !_status.Transform) return;
            if (IsLocalHuman)
            {
                _view ??= new MarlinJacksView(this);
                _view.Frame(_humanRunsJacks, Time.deltaTime);
            }
            DrawMarks();
        }

        // ---- the board ----

        void UpdateBoard(float dt)
        {
            var t = _status.Transform;
            if (!_snapped)
            {
                _f = t.forward; _u = t.up; _r = t.right;
                _snapped = true;
                return;
            }
            float a = MarlinJacksMath.Ease(_humanRunsJacks ? config.JackSeatPlaneEase : config.PilotSeatPlaneEase, dt);
            if (a <= 0f) return;
            _f += (t.forward - _f) * a;
            _u += (t.up - _u) * a;
            _f.Normalize();
            _r = Vector3.Cross(_u, _f).normalized;
            _u = Vector3.Cross(_f, _r);
        }

        /// <summary>A point laid onto the marlin's plane (the lab's <c>onPlane</c>).</summary>
        internal Vector3 OnPlane(Vector3 p) => p - _u * Vector3.Dot(p - MarlinPosition, _u);

        internal Vector2 ToBoard(Vector3 p)
        {
            var d = p - MarlinPosition;
            return new Vector2(Vector3.Dot(d, _r), Vector3.Dot(d, _f));
        }

        internal Vector3 FromBoard(Vector2 b) => MarlinPosition + _r * b.x + _f * b.y;

        // ---- the jacks ----

        void PlaceJacks()
        {
            if (_status == null || !_status.Transform || !config) return;
            var t = _status.Transform;
            _r = t.right; _f = t.forward; _u = t.up;
            float ring = Ring;
            for (int k = 0; k < 2; k++)
            {
                _jack[k] = t.position + _r * (k == 0 ? -ring : ring) - _f * ring;
                _jackTarget[k] = _jack[k];
            }
        }

        void StepJacks(float dt)
        {
            for (int k = 0; k < 2; k++)
            {
                _jack[k] = OnPlane(_jack[k]);
                if (_jackDown[k]) _jack[k] = MarlinJacksMath.StepToward(_jack[k], OnPlane(_jackTarget[k]), config.DogSpeed, dt);
                else if (_jackWasDown[k]) DropDecoy(_jack[k]);   // the lift: the decoy is left where the jack stands
                _jackWasDown[k] = _jackDown[k];
            }
        }

        void StepAIJacks(float dt, float ring)
        {
            var target = HerdTarget();
            var plan = _ai.Plan(this, dt, ring, target);
            _jackTarget[0] = FromBoard(plan.A);
            _jackTarget[1] = FromBoard(plan.B);
            _jackDown[0] = true;
            _jackDown[1] = !plan.LiftB;
        }

        /// <summary>Where the AI takes the herd: the densest rival mass in the marlin's cell (the cell's own targeting grid).</summary>
        Vector3 HerdTarget()
        {
            var cell = NearestCell(MarlinPosition);
            return cell ? cell.GetExplosionTarget(_status.Domain) : HerdCentre;
        }

        /// <summary>
        /// The AI marlin (the hull's own AIPilot flies it): it CALLS when the herd has scattered and it is near, and at Time 5
        /// it leaves a decoy on station every few seconds (the lab's <c>shAI</c> pilot rules; the flight is the AIPilot's).
        /// </summary>
        void StepAIMarlin()
        {
            float scale = config.LabScale;
            if (_herd.Count > 0 && HerdSpread > 200f * scale && Time.time >= _callReadyAt
                && (MarlinPosition - HerdCentre).sqrMagnitude < Sq(900f * scale))
                Call();
            if (Upgraded(Element.Time) && !_hasDecoy && Time.time >= _aiDecoyAt)
            {
                DropMarlinDecoy();
                _aiDecoyAt = Time.time + 6f;
            }
        }

        // ---- the human on the jacks: thumbs, the mouse, the sticks ----

        void ReadHumanJacks(float dt, float ring)
        {
            if (_view == null) return;
            bool[] held = { false, false };

            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touches = touchscreen.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var t = touches[i];
                    int id = t.touchId.ReadValue();
                    var at = t.position.ReadValue();
                    if (t.press.wasPressedThisFrame && !_view.PipContains(at, true)) Claim(id, at);
                    if (t.press.isPressed && _pointerJack.TryGetValue(id, out int k)) { Aim(k, at); held[k] = true; }
                    if (t.press.wasReleasedThisFrame) _pointerJack.Remove(id);
                }
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                var at = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame && !_view.PipContains(at, true)) Claim(MouseId, at);
                if (mouse.leftButton.isPressed && _pointerJack.TryGetValue(MouseId, out int k)) { Aim(k, at); held[k] = true; }
                if (mouse.leftButton.wasReleasedThisFrame) _pointerJack.Remove(MouseId);
            }

            var pad = Gamepad.current;
            if (pad != null) ReadPad(pad, dt, held);

            for (int k = 0; k < 2; k++)
                if (!held[k] && !_padHolds[k]) _jackDown[k] = false;
        }

        readonly bool[] _padHolds = new bool[2];

        void Claim(int pointer, Vector2 at)
        {
            int best = -1;
            float bd = float.PositiveInfinity;
            for (int k = 0; k < 2; k++)
            {
                if (_pointerJack.ContainsValue(k)) continue;
                float d = (_view.WorldToScreen(_jack[k]) - at).sqrMagnitude;
                if (d < bd) { bd = d; best = k; }
            }
            if (best >= 0) _pointerJack[pointer] = best;
        }

        void Aim(int k, Vector2 screen)
        {
            _jackTarget[k] = _view.ScreenToPlane(screen);
            _jackDown[k] = true;
        }

        /// <summary>
        /// The sticks (the lab's <c>gatherPadDogs</c>): the left stick runs the left jack and the right stick the right one,
        /// screen-relative (stick up = up the screen), at <c>padDogSpeed</c> x <c>dogSpeed</c>; a jack holds still when its
        /// stick rests, so resting drops nothing. The bumper or trigger on that side is the thumb's lift: it leaves the decoy
        /// where that jack stands. When both sticks rest, the left stick takes whichever jack is further left on screen. A
        /// stick never drives its jack off the screen.
        /// </summary>
        void ReadPad(Gamepad pad, float dt, bool[] heldByPointer)
        {
            Vector2 l = pad.leftStick.ReadValue(), r = pad.rightStick.ReadValue();
            bool restL = MarlinJacksMath.DeadZone(l.x) == 0f && MarlinJacksMath.DeadZone(l.y) == 0f;
            bool restR = MarlinJacksMath.DeadZone(r.x) == 0f && MarlinJacksMath.DeadZone(r.y) == 0f;
            if (restL && restR)
            {
                bool leftFirst = _view.WorldToScreen(_jack[0]).x <= _view.WorldToScreen(_jack[1]).x;
                _padJack[0] = leftFirst ? 0 : 1;
                _padJack[1] = leftFirst ? 1 : 0;
            }
            for (int s = 0; s < 2; s++)
            {
                int k = _padJack[s];
                _padHolds[k] = false;
                if (heldByPointer[k]) continue;
                bool lift = s == 0
                    ? pad.leftShoulder.isPressed || pad.leftTrigger.ReadValue() > 0.5f
                    : pad.rightShoulder.isPressed || pad.rightTrigger.ReadValue() > 0.5f;
                bool edge = lift && !_padLiftWas[s];
                _padLiftWas[s] = lift;
                if (edge) { _jackDown[k] = false; continue; }   // one step up: StepJacks drops the decoy where the jack stands
                var run = MarlinJacksMath.StickRun(s == 0 ? l : r, config.DogSpeed * config.PadDogSpeed, dt);
                _jackTarget[k] = _view.RunOnScreen(_jack[k], run);
                _jackDown[k] = true;
                _padHolds[k] = true;
            }
        }

        // ---- seats ----

        void PollSwap()
        {
            int dir = PilotSwapGesture.DirectionThisFrame();
            if (dir == 0 && _view != null && _view.PipDoubleTapped(_humanRunsJacks)) dir = 1;
            if (dir == 0 || Time.unscaledTime < _swapReadyAt) return;
            _swapReadyAt = Time.unscaledTime + PilotSwap.CooldownSeconds;
            if (_humanRunsJacks) LeaveJackSeat();
            else TakeJackSeat();
        }

        // The hull's own AI flies while the human runs the jacks, with its flight input paused so the sticks belong to the
        // jacks - the Wrasse's swap (KiteNavigator.SetHumanNavigates). Only an autopilot this component started is stopped.
        void TakeJackSeat()
        {
            if (_humanRunsJacks || _vessel == null || _status == null) return;
            _humanRunsJacks = true;
            _pointerJack.Clear();
            SetFlightInputPaused(true);
            _weTookHull = !_status.AutoPilotEnabled;
            if (_weTookHull) _vessel.ToggleAIPilot(true);
        }

        void LeaveJackSeat()
        {
            if (!_humanRunsJacks) return;
            _humanRunsJacks = false;
            _pointerJack.Clear();
            if (_weTookHull && _vessel != null && UnityLiveness.Alive(_vessel.Transform)) _vessel.ToggleAIPilot(false);
            _weTookHull = false;
            SetFlightInputPaused(false);
        }

        void SetFlightInputPaused(bool paused)
        {
            var player = _status?.Player;
            if (player == null) return;
            if (UnityLiveness.Alive(player.InputController)) player.InputController.SetPause(paused);
            else if (player.InputStatus != null) player.InputStatus.Paused = paused;
        }

        // ------------------------------------------------------------------ the herd

        /// <summary>The herd's spread on the board (the lab's flock radius, RMS).</summary>
        internal float HerdSpread { get; private set; }

        /// <summary>The herd's furthest creature on the board, from its centre (the lab's straggler, <c>far</c>).</summary>
        internal Vector2 HerdFar { get; private set; }

        internal Vector2 JackOnBoard(int k) => ToBoard(_jack[k]);

        /// <summary>
        /// The herd's steer, every member every frame (the lab's <c>stepSheep</c>, ported term for term in
        /// <see cref="MarlinJacksMath"/>): the flock's own separation, alignment and cohesion; each herder's ring; the call;
        /// the Charge-5 attack; grazing while calm; the wander; and the plane's spring. The creature then swims it by its own
        /// steering (<see cref="TrySteer"/>) and eats by its own mouthful (<see cref="OrderedMeal"/>).
        /// </summary>
        void StepHerd(float dt, float ring)
        {
            var herders = Herders(ring);
            Vector3 marlin = MarlinPosition, centre = Vector3.zero;
            bool calling = Calling;
            float pace = MarlinJacksMath.SpeedMultiplier(Level(Element.Time));
            float sep = config.Separation, nr = config.NeighbourRadius, nr2 = nr * nr;
            float callDead = 30f * config.LabScale;
            bool attacks = Upgraded(Element.Charge);
            if (attacks) ScanVessels();

            for (int i = 0; i < _herd.Count; i++)
            {
                var m = _herd[i];
                Vector3 pos = m.Creature.transform.position;
                m.Velocity = (pos - m.LastPos) / dt;
                m.LastPos = pos;
                centre += pos;
            }
            if (_herd.Count > 0) centre /= _herd.Count;
            HerdCentre = _herd.Count > 0 ? centre : marlin;
            MeasureSpread();

            for (int i = 0; i < _herd.Count; i++)
            {
                var m = _herd[i];
                var c = m.Creature;
                Vector3 pos = m.LastPos;
                float cruise = c.LeashTopSpeed * pace;

                Vector3 sepSum = Vector3.zero, velSum = Vector3.zero, cenSum = Vector3.zero;
                int n = 0;
                for (int j = 0; j < _herd.Count; j++)
                {
                    if (j == i) continue;
                    var o = _herd[j];
                    Vector3 d = pos - o.LastPos;
                    if (d.sqrMagnitude > nr2) continue;
                    sepSum += MarlinJacksMath.SeparationTerm(d, sep);
                    velSum += o.Velocity;
                    cenSum += o.LastPos;
                    n++;
                }
                Vector3 g = MarlinJacksMath.Flock(sepSum, velSum, cenSum, n, pos, cruise, nr);

                float panic = 0f;
                for (int h = 0; h < herders.Count; h++)
                {
                    g += MarlinJacksMath.Repel(pos, herders[h], ring, _u, out float k);
                    panic = Mathf.Max(panic, k);
                }
                if (calling)
                {
                    g += MarlinJacksMath.CallPull(pos, marlin, _u, callDead);
                    panic = Mathf.Max(panic, 0.5f);
                }
                if (attacks && TryNearestRival(pos, config.AttackRange, out var rival, out float rd))
                {
                    g += (rival - pos) * (2.2f / Mathf.Max(1e-3f, rd));
                    panic = Mathf.Max(panic, 0.6f);
                }

                float gz = 0f;
                if (panic < 0.5f)
                {
                    if (!MealStillGood(m) && Time.time >= m.MealAt) PickMeal(m, pos);
                    if (m.Meal)
                    {
                        Vector3 dv = m.Meal.transform.position - pos;
                        float d = dv.magnitude;
                        if (d > 1e-4f)
                        {
                            g += dv * (0.9f / d);
                            gz = Vector3.Dot(dv, _u) / d;
                        }
                    }
                }
                else m.Meal = null;   // a herder scared it off its meal

                m.Phase += dt * MarlinJacksMath.WanderRate(m.Roll);
                g += MarlinJacksMath.Wander(m.Phase, m.Roll, _r, _f);

                Vector3 want = MarlinJacksMath.Want(g, _u, cruise, panic);
                float height = Vector3.Dot(pos - marlin, _u);
                float gn = Vector3.Dot(g, _u) + 0.6f * gz;   // the lab's (gn - 0.9 gz) x 40 + 60 gz, over 40
                float vn = MarlinJacksMath.PlaneSpeed(height, gn, config.PlanePull, Mathf.Max(cruise, 1f) * 1.8f);
                m.Desired = want + _u * vn;
            }
        }

        readonly List<Vector3> _herders = new(4);

        /// <summary>Every herder this frame (the lab's <c>repellers</c>): the marlin (not while it calls), both jacks, the decoy.</summary>
        List<Vector3> Herders(float ring)
        {
            _herders.Clear();
            if (!Calling) _herders.Add(MarlinPosition);
            _herders.Add(_jack[0]);
            _herders.Add(_jack[1]);
            if (_hasDecoy) _herders.Add(_decoy);
            return _herders;
        }

        void MeasureSpread()
        {
            if (_herd.Count == 0) { HerdSpread = 0f; HerdFar = Vector2.zero; return; }
            var c = ToBoard(HerdCentre);
            float r2 = 0f, far = -1f;
            Vector2 farPt = c;
            for (int i = 0; i < _herd.Count; i++)
            {
                var q = ToBoard(_herd[i].LastPos);
                float d = (q - c).sqrMagnitude;
                r2 += d;
                if (d > far) { far = d; farPt = q; }
            }
            HerdSpread = Mathf.Sqrt(r2 / _herd.Count);
            HerdFar = farPt;
        }

        bool MealStillGood(Member m) =>
            m.Meal && !m.Meal.destroyed && m.Meal.Domain != _status.Domain && m.Creature.LeashCanEat(m.Meal);

        /// <summary>A calm creature grazes the nearest prism it can eat within <c>grazeSense</c> - never our own domain's mass
        /// (the lab's pasture rule) - re-picked twice a second, so a full herd costs a few sphere queries a frame.</summary>
        void PickMeal(Member m, Vector3 pos)
        {
            m.MealAt = Time.time + 0.5f + m.Roll * 0.1f;
            m.Meal = null;
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return;
            int found = index.QuerySphere(pos, config.GrazeSense, _query);
            float best = float.PositiveInfinity;
            var own = _status.Domain;
            for (int i = 0; i < found; i++)
            {
                var p = _query[i];
                if (!p || p.destroyed || p.Domain == own) continue;
                if (p is HealthPrism hp && hp.ResolveOwnerFauna()) continue;   // a creature's body is no meal
                if (!m.Creature.LeashCanEat(p)) continue;                     // its own diet: domain, nucleus, pens, shields
                float d = (p.transform.position - pos).sqrMagnitude;
                if (d < best) { best = d; m.Meal = p; }
            }
        }

        // ---- who joins: the wild herbivores a herder's ring touches ----

        void Recruit(float ring)
        {
            if (Time.time < _recruitAt || _herd.Count >= config.HerdCap) return;
            _recruitAt = Time.time + 0.25f;
            var herders = Herders(ring);
            float r2 = ring * ring;
            var cells = Cell.ActiveCellsSnapshot;
            for (int ci = 0; ci < cells.Count && _herd.Count < config.HerdCap; ci++)
            {
                var cell = cells[ci];
                if (!cell) continue;
                var live = cell.LiveFauna;
                for (int i = 0; i < live.Count && _herd.Count < config.HerdCap; i++)
                {
                    var f = live[i];
                    if (!f || f.IsDying || f.IsLeashed || _byCreature.ContainsKey(f) || !f.Leashable) continue;
                    var p = f.transform.position;
                    bool touched = false;
                    for (int h = 0; h < herders.Count && !touched; h++)
                        touched = MarlinJacksMath.InPlane(p - herders[h], _u).sqrMagnitude <= r2
                                  && Mathf.Abs(Vector3.Dot(p - herders[h], _u)) <= ring;
                    if (touched) Join(f);
                }
            }
        }

        bool Join(Fauna f)
        {
            if (!f || _byCreature.ContainsKey(f) || !f.TryLeash(this)) return false;
            var m = new Member
            {
                Creature = f,
                Roll = MarlinJacksMath.Roll01(f.GetInstanceID()),
                LastPos = f.transform.position,
            };
            m.Phase = m.Roll * 10f;
            _herd.Add(m);
            _byCreature[f] = m;
            return true;
        }

        /// <summary>
        /// An EMPTY herd has its floor born to it (the Angler keeper's rule grown to the herd; the designer, 2026-10-21: the
        /// herd "should only starve down to a minimum size and break other rules if need be"): one creature every
        /// <c>floorBirthSeconds</c> until the herd holds <c>minHerd</c>, through the platform's one fauna spawn seam in the
        /// cell's controlling colour, blooming in. A replicated species is born only where the ecology is simulated.
        /// </summary>
        void EnsureFloor()
        {
            if (_herd.Count == 0 && config.MinHerd > 0) _seeding = true;
            if (!_seeding) return;
            if (_herd.Count >= config.MinHerd) { _seeding = false; return; }
            if (Time.time < _floorBirthAt) return;
            _floorBirthAt = Time.time + config.FloorBirthSeconds;
            var cell = NearestCell(MarlinPosition);
            if (!cell) return;
            var cfg = FloorSpecies(cell);
            if (!cfg || (cfg.NetworkSynced && !FaunaNetworkSync.IsSimAuthority)) return;
            int i = _herd.Count;
            float th = i * 2.39996323f, rad = Ring * 0.6f * Mathf.Sqrt((i + 1f) / Mathf.Max(1f, config.MinHerd));
            Vector3 at = OnPlane(MarlinPosition - _f * Ring + (_r * Mathf.Cos(th) + _f * Mathf.Sin(th)) * rad);
            if (cell.FaunaExclusionRadius > 0f || cell.FaunaContainmentRadius > 0f) at = cell.ClampToFaunaContainment(at, at);
            var f = CellLifeSpawnerBase.SpawnFaunaWithDomain(cell, cfg.FaunaPrefab, at, cell.ControllingDomain, at, cfg);
            if (f) Join(f);   // born wild if it cannot be leashed: an ordinary creature of the cell from here on
        }

        FaunaConfigurationSO FloorSpecies(Cell cell)
        {
            var profile = cell.Config ? cell.Config.SpawnProfile : null;
            if (profile)
                for (int i = 0; i < profile.SupportedFaunas.Count; i++)
                {
                    var c = profile.SupportedFaunas[i];
                    if (c && c.FaunaPrefab && c.FaunaPrefab.Leashable) return c;
                }
            var companion = config.CompanionFauna;
            return companion && companion.FaunaPrefab && companion.FaunaPrefab.Leashable ? companion : null;
        }

        static Cell NearestCell(Vector3 at)
        {
            Cell best = null;
            float bd = float.PositiveInfinity;
            var cells = Cell.ActiveCellsSnapshot;
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                if (!c) continue;
                float d = (c.transform.position - at).sqrMagnitude;
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        // ---- contacts with rival hulls: danger stings, and Charge 5 bites (PvP is petals only) ----

        void ScanVessels()
        {
            if (Time.time < _vesselScanAt) return;
            _vessels.Clear();
            _vessels.AddRange(Object.FindObjectsByType<VesselStatus>(FindObjectsSortMode.None));   // once a second, not per frame
            _vesselScanAt = Time.time + 1f;
        }

        bool TryNearestRival(Vector3 from, float range, out Vector3 at, out float distance)
        {
            at = default;
            distance = range;
            bool found = false;
            var own = _status.Domain;
            for (int i = 0; i < _vessels.Count; i++)
            {
                var v = _vessels[i];
                if (!v || v.Player == null) continue;
                IVesselStatus vs = v;
                if (vs.Domain == own || vs.Transform == _status.Transform) continue;
                float d = Vector3.Distance(v.transform.position, from);
                if (d < distance) { distance = d; at = v.transform.position; found = true; }
            }
            return found;
        }

        /// <summary>
        /// A DANGER creature that touches a rival hull stings it - a scored Strike that takes petals - and is spent (it dies
        /// the ordinary way, a joust). At Charge 5 every creature that touches one bites it (a Strike, not spent). One gate,
        /// the same as every contact weapon: decided on the machine that owns the marlin, admitted by
        /// <see cref="CombatHitDrain.TryAdmit"/> (its per-pair latch keeps it to one Strike per <c>strikeCooldown</c> per hull).
        /// Never on our own team, and never a stun.
        /// </summary>
        void UpdateContacts()
        {
            if (_herd.Count == 0 || !ElementalTransfer.IsDecidedHere(_status)) return;
            if (_status.Player is { IsNetworkOwner: false }) return;
            ScanVessels();
            float charge = Level(Element.Charge);
            bool bites = Upgraded(Element.Charge);
            var own = _status.Domain;
            for (int v = 0; v < _vessels.Count; v++)
            {
                var hull = _vessels[v];
                if (!hull || hull.Player == null) continue;
                IVesselStatus victim = hull;
                if (victim.Domain == own || victim.Transform == _status.Transform) continue;
                Vector3 hp = hull.transform.position;
                for (int i = _herd.Count - 1; i >= 0; i--)
                {
                    var m = _herd[i];
                    if (!m.Creature) continue;
                    float reach = config.ContactReach + m.Creature.LeashMouthReach * 0.25f;
                    Vector3 to = hp - m.LastPos;
                    if (to.sqrMagnitude > reach * reach) continue;
                    bool danger = MarlinJacksMath.IsDanger(m.Roll, charge);
                    if (!danger && !bites) continue;
                    if (!Strike(victim, to)) break;   // latched: this hull was just scored on
                    if (danger && m.Creature.IsSimAuthority) m.Creature.Jousted(_status.PlayerName);   // the sting spends it
                    break;
                }
            }
        }

        bool Strike(IVesselStatus victim, Vector3 approach)
        {
            if (!CombatHitDrain.TryAdmit(victim, _status, CombatHitClass.Strike, config.StrikeCooldown,
                                         ElementalDebuffSources.VesselContact, out int supersededRank))
                return false;
            CombatHitDrain.ApplyPriced(victim, _status, CombatHitClass.Strike, supersededRank,
                                       approach.normalized * 10f, ElementalDebuffSources.VesselContact);
            if (onCombatHitLanded)
                onCombatHitLanded.Raise(new CombatHitStats
                {
                    ShooterName = _status.PlayerName,
                    VictimName = victim.PlayerName,
                    HitClass = CombatHitClass.Strike,
                    SupersededRank = supersededRank,
                });
            return true;
        }

        // ---- Mass 5: the herd lays a cluster of our mass ----

        /// <summary>
        /// MASS level 5 (Garrett, 2026-10-20: "the swarm periodically poops a cluster of mass"): every <c>pooEvery</c> one
        /// creature of the herd (a hash of a serial picks it, vessel rule 18) leaves <c>pooCount</c> prisms of our domain
        /// round itself, through the hull's own prism spawn channel (the Wrasse's Mass cluster).
        /// </summary>
        void LayMass()
        {
            if (Time.time < _pooAt) return;
            _pooAt = Time.time + config.PooEvery;
            if (!Upgraded(Element.Mass) || _herd.Count == 0) return;
            var spawner = _status.VesselPrismController;
            var channel = spawner ? spawner.PrismSpawnChannel : null;
            if (!channel) return;
            var m = _herd[(int)(MarlinJacksMath.Roll01((int)++_pooSerial) * _herd.Count) % _herd.Count];
            Vector3 at = m.LastPos;
            var scale = config.PooPrismScale;
            for (int i = 0; i < config.PooCount; i++)
            {
                float a = MarlinJacksMath.Roll01((int)(_pooSerial * 31u + (uint)i)) * Mathf.PI * 2f;
                float r = MarlinJacksMath.Roll01((int)(_pooSerial * 131u + (uint)i * 7u)) * config.PooSpread;
                float y = (MarlinJacksMath.Roll01((int)(_pooSerial * 17u + (uint)i * 3u)) - 0.5f) * config.PooSpread * 0.7f;
                Vector3 pos = at + (_r * Mathf.Cos(a) + _f * Mathf.Sin(a)) * r + _u * y;
                var ret = channel.RaiseEvent(new PrismEventData
                {
                    ownDomain = _status.Domain,
                    Rotation = Quaternion.LookRotation(_f, _u),
                    SpawnPosition = pos,
                    Scale = scale,
                    PrismType = spawner.SpawnPrismType,
                });
                if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out Prism prism)) continue;
                prism.TargetScale = scale;
                prism.ownerID = _status.PlayerName;
                prism.ChangeTeam(_status.Domain);
                prism.Initialize(_status.PlayerName);
                prism.AdmitTargetScale(scale);
                prism.TargetScale = scale;
            }
        }

        // ------------------------------------------------------------------ IFaunaHerd

        public bool TrySteer(Fauna creature, out Vector3 goal, out float speed, out float turnRate)
        {
            goal = default;
            speed = 0f;
            turnRate = 0f;
            if (!creature || !_byCreature.TryGetValue(creature, out var m)) return false;
            float s = m.Desired.magnitude;
            if (s < 1e-3f) return false;
            goal = m.LastPos + m.Desired / s * Mathf.Max(10f, s);   // a second ahead on its wanted heading
            speed = s;
            turnRate = 4f;
            return true;
        }

        public Prism OrderedMeal(Fauna creature) =>
            creature && _byCreature.TryGetValue(creature, out var m) && m.Meal && !m.Meal.destroyed ? m.Meal : null;

        public void Released(Fauna creature)
        {
            if (!creature || !_byCreature.TryGetValue(creature, out var m)) return;
            _byCreature.Remove(creature);
            _herd.Remove(m);
        }

        /// <summary>The herd's FLOOR: its first <c>minHerd</c> creatures (in the order they joined) do not starve.</summary>
        public bool Sustains(Fauna creature)
        {
            if (!config || !creature || !_byCreature.TryGetValue(creature, out var m)) return false;
            int floor = Mathf.Min(config.MinHerd, _herd.Count);
            for (int i = 0; i < floor; i++)
                if (_herd[i] == m) return true;
            return false;
        }

        /// <summary>SPACE level 5 (Garrett, 2026-10-20: "the swarm can eat shielded prisms"): a rule the herd breaks.</summary>
        public bool EatsShields(Fauna creature) => Upgraded(Element.Space);

        /// <summary>MASS: the herd's birth rate (<see cref="MarlinJacksMath.FeedsPerOffspringScale"/>).</summary>
        public float FeedsPerOffspringScale(Fauna creature) => MarlinJacksMath.FeedsPerOffspringScale(Level(Element.Mass));

        public void Born(Fauna parent, Fauna child)
        {
            if (config && _herd.Count < config.HerdCap) Join(child);
        }

        // ------------------------------------------------------------------ bookkeeping

        void PruneDead()
        {
            _scratch.Clear();
            for (int i = 0; i < _herd.Count; i++)
                if (!_herd[i].Creature || _herd[i].Creature.IsDying) _scratch.Add(_herd[i]);
            for (int i = 0; i < _scratch.Count; i++)
            {
                var m = _scratch[i];
                _herd.Remove(m);
                if (m.Creature) { m.Creature.Unleash(this); _byCreature.Remove(m.Creature); }
            }
            if (_scratch.Count > 0)
            {
                // a destroyed creature is a fake-null key: rebuild rather than chase it
                _byCreature.Clear();
                for (int i = 0; i < _herd.Count; i++) _byCreature[_herd[i].Creature] = _herd[i];
            }
        }

        void ReleaseAll()
        {
            for (int i = 0; i < _herd.Count; i++)
                if (_herd[i].Creature) _herd[i].Creature.Unleash(this);
            _herd.Clear();
            _byCreature.Clear();
        }

        static float Sq(float x) => x * x;

        static void Play(EventReference evt, Vector3 at)
        {
            if (evt.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(evt, at);
        }

        // ------------------------------------------------------------------ presentation

        /// <summary>Only the local human sees the jacks' rings and marks (they are the crew's instruments).</summary>
        void DrawMarks()
        {
            if (!IsLocalHuman) return;
            var cam = _humanRunsJacks && _view != null ? _view.Camera : ResolveCamera();
            if (!cam) return;
            _marks ??= new AnglerWorldMarks();
            if (!_marks.Begin(cam)) return;
            float ring = Ring, dot = Mathf.Max(1.5f, ring * 0.02f);
            for (int k = 0; k < 2; k++)
            {
                _marks.Dot(_jack[k], jackColor, dot * 3f, 8f);
                _marks.Ring(_jack[k], _r, _f, ring, 48, ringColor, dot, 3f);
            }
            if (_hasDecoy)
            {
                _marks.Dot(_decoy, decoyColor, dot * 3f, 8f);
                _marks.Ring(_decoy, _r, _f, ring, 48, decoyColor * new Color(1f, 1f, 1f, 0.6f), dot, 3f);
            }
            if (!Calling) _marks.Ring(MarlinPosition, _r, _f, ring, 48, marlinColor * new Color(1f, 1f, 1f, 0.6f), dot, 3f);
            if (_humanRunsJacks) _marks.Dot(MarlinPosition, marlinColor, dot * 3f, 8f);
            float charge = Level(Element.Charge);
            for (int i = 0; i < _herd.Count; i++)
                if (MarlinJacksMath.IsDanger(_herd[i].Roll, charge))
                    _marks.Dot(_herd[i].LastPos + _u * 4f, dangerColor, dot * 1.5f, 5f);
            _marks.End();
        }

        static Camera ResolveCamera()
        {
            var manager = CameraManager.Instance;
            if (manager && manager.GetActiveController() is CustomCameraController controller && controller.Camera)
                return controller.Camera;
            return Camera.main;
        }

        internal Vector3 JackWorld(int k) => _jack[k];
        internal bool TryGetDecoy(out Vector3 at) { at = _decoy; return _hasDecoy; }
    }
}
