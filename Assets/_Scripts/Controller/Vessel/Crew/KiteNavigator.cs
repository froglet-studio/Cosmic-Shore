using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The navigator's seat on a hull: the Kite &amp; Tail crew role (Crew Vessel Lab round 3, signed off 2026-10-17;
    /// <c>Docs/Studios/CREW_VESSEL_LAB.md</c>, <c>KITE_NAVIGATOR.md</c> beside this file). One player flies the hull; the
    /// navigator flies a kite on a 2D board that is the hull's x-z plane, with the world projected onto it. The kite's head
    /// pops hostile prisms and rams rival hulls; its tail is a lasso, and a loop round motes of ONE element fires that
    /// element's effect, <c>base × growth^(n − 1)</c> for n motes: Space blasts (AOEExplosion), Mass lays a cluster of our
    /// prisms, Time hands the pilot boost, Charge powers the kite so its next ram steals that many petals (a scored
    /// Strike, <c>CombatHitDrain.TryAdmit</c>). Motes are the navigator's own and are drawn only on its board.
    ///
    /// <para><b>Seats.</b> The swap is the arena's gesture (<see cref="PilotSwapGesture"/>: D-pad left / right, keys 1 / 2).
    /// It moves the local human between the hull and the kite; whichever seat is empty is flown by an AI squad mate - the
    /// hull's own <c>AIPilot</c>, or <see cref="KiteNavigatorAI"/>, which drives the kite only through an aim point, as a
    /// thumb does.</para>
    ///
    /// <para><b>Local only.</b> Everything here runs on the machine of the hull's local human: the board, the motes and
    /// the AI are per-viewer, and the effects are this machine's. Networking the seat is a follow-up (KITE_NAVIGATOR.md).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KiteNavigator : MonoBehaviour, IRingThreadProbe
    {
        internal struct Mote
        {
            public Vector3 World;
            public float X, Z, Y, VX, VZ;
            public bool Has;
            public int Element;   // 0 Charge, 1 Mass, 2 Space, 3 Time (Element - 1)
            public float Spin;    // the petal's turn on the board, degrees
        }

        internal struct PrismShadow
        {
            public Prism Prism;
            public float X, Z, Y;
            public Domains Domain;
            public bool Hostile;
        }

        internal struct RivalShadow
        {
            public IVesselStatus Status;
            public int Id;
            public float X, Z, Y, VX, VZ;
            public bool Has, Seen;
            public Domains Domain;
        }

        internal struct FaunaShadow
        {
            public Fauna Fauna;
            public float X, Z, Y;
        }

        internal enum FxKind { Loop = 0, Fizzle = 1, Pop = 2, Space = 3, Mass = 4, Time = 5, Charge = 6, Steal = 7 }

        internal struct Fx
        {
            public FxKind Kind;
            public float X, Z, T0, MaxScale;
            public KiteVec[] Loop;
        }

        // AOEExplosion.prefab's authored timing, used only to draw the blast's ring on the board
        internal const float BlastDelay = 0.2f, BlastDuration = 3f;
        const float FxLife = 1.4f;
        const float PrismHalfWidth = 1f;          // the Sparrow's 2 u trail prism
        const float RamReach = 4f;                // a hull's shadow is this much wider than a point
        const int MaxPrismShadows = 900;

        [Header("Config")]
        [Tooltip("Every number of the kite (KiteNavigatorConfig.asset, the lab's signed-off candidate). The Wrasse hull's " +
                 "prefab wires it; the play-mode menu hands it to a hull it attaches to.")]
        [SerializeField] KiteNavigatorConfigSO config;

        static KiteNavigator _active;

        /// <summary>The navigator riding the local hull, or null.</summary>
        public static KiteNavigator Active => _active;

        KiteNavigatorConfigSO _cfg;
        IPlayer _player;
        IVessel _vessel;
        IVesselStatus _status;
        KiteSettings _s;
        readonly KiteNavigatorCore _kite = new();
        KiteNavigatorAI _ai;
        System.Random _rng;

        Vector3 _r = Vector3.right, _f = Vector3.forward, _u = Vector3.up;
        bool _snapped;

        internal readonly List<Mote> Motes = new();
        internal readonly List<PrismShadow> Prisms = new();
        internal readonly List<RivalShadow> Rivals = new();
        internal readonly List<FaunaShadow> Creatures = new();
        internal readonly List<Fx> Effects = new();
        readonly List<Prism> _query = new();
        readonly List<KiteMote> _aiMotes = new();
        readonly List<KiteTarget> _aiPrisms = new();
        readonly List<KiteTarget> _aiRivals = new();
        readonly List<KiteVec> _loop = new();
        readonly Dictionary<IVesselStatus, float> _lastRam = new();
        readonly Queue<(Vector3 pos, Quaternion rot)> _massQueue = new();

        float _charge, _boostEndsAt, _massFlashAt = -99f;
        int _savedBoosts, _lastBlastDestroyed;
        // the last Space blast's hostile prisms, watched until the blast has run its course, to count what it destroyed
        readonly List<Prism> _blastWatch = new();
        readonly HashSet<Prism> _blastCounted = new();
        float _blastWatchUntil;
        bool _humanNavigates, _weTookHull, _down, _boosting, _clickOnPip;
        // this frame's aim is a thumb or a mouse holding the kite's rope (KiteControl.Sling), not a point it flies to
        bool _sling;
        // the kite as a SWITCH THREADER (IRingThreadProbe): bumped whenever the kite is re-placed rather than flown, so a
        // jump threads nothing
        int _probeEpoch;
        // the race ring lit for this pilot, when it lies on the board within reach (Garrett, 2026-10-20: the Wrasse collects
        // the rings) - the AI squad mate flies the kite through it before anything else
        bool _ringOnBoard;
        KiteVec _ringAt, _ringVel, _ringCentre;
        float _swapReadyAt, _boostReadyAt;
        int _omniPending;
        KiteVec _aim;
        enum Pointer { Mouse = 0, Touch = 1, Stick = 2 }
        Pointer _pointer;
        Vector2 _lastMouse;
        KiteNavigatorView _view;

        internal KiteNavigatorConfigSO Config => _cfg;
        internal KiteSettings Settings => _s;
        internal KiteNavigatorCore Kite => _kite;
        internal Domains OwnDomain => _status != null ? _status.Domain : Domains.Jade;
        internal float Charge => _charge;
        /// <summary>Boosts the navigator's Time loops have saved for the pilot.</summary>
        public int SavedBoosts => _savedBoosts;
        /// <summary>Rams the kite holds power for (whole petals of Charge).</summary>
        public int Charges => Mathf.FloorToInt(_charge);
        /// <summary>Prisms the last Space blast destroyed (counting up while it runs).</summary>
        public int LastBlastDestroyed => _lastBlastDestroyed;
        /// <summary>Time.time of the last Mass cluster laid (the HUD flashes the Mass icon).</summary>
        public float MassFlashAt => _massFlashAt;
        /// <summary>The boost's recharge veil, 1 while a boost runs and falling to 0 when the next can fire.</summary>
        public float BoostCooldown01 => _boosting ? 1f
            : config && config.BoostCooldownSeconds > 0f ? Mathf.Clamp01((_boostReadyAt - Time.time) / config.BoostCooldownSeconds) : 0f;
        /// <summary>The most boosts the pilot can hold: timeBoostCap seconds of 1.5 s boosts.</summary>
        int MaxSavedBoosts => Mathf.Max(1, Mathf.FloorToInt(_cfg.TimeBoostCap / Mathf.Max(0.05f, _cfg.TimeBoostSeconds)));
        internal bool Down => _down;
        internal KiteVec Aim => _aim;
        /// <summary>The kite hangs on the rope this frame (Sling, held): the view draws the cord from <see cref="Aim"/>,
        /// the hand, to the kite (Garrett, 2026-10-20: "make the wrasse cord visible to the wrasse player").</summary>
        internal bool CordHeld => _sling && _down;
        internal Vector3 BoardRight => _r;
        internal Vector3 BoardForward => _f;
        internal Vector3 BoardUp => _u;

        /// <summary>True while the local human flies the kite and the hull's AI flies the hull.</summary>
        public bool HumanNavigates => _humanNavigates;

        /// <summary>The hull collected an Omni crystal: a cluster of each element drops on the board for the kite to loop
        /// (KiteOmniCrystalEffectSO). Laid on the next step, where the board is current.</summary>
        public void OnOmniCrystal() => _omniPending++;

        /// <summary>
        /// Seat a navigator on the hull the local player flies (the FrogletTools play-mode menu calls this; the Wrasse hull
        /// carries one on its prefab). The human keeps the hull; the AI takes the kite until the swap gesture moves them.
        /// Returns null, with a warning, when no local hull is flying.
        /// </summary>
        public static KiteNavigator AttachToLocalVessel(KiteNavigatorConfigSO cfg)
        {
            var vt = VesselVisionShading.LocalVessel;
            if (!vt || !vt.TryGetComponent(out IVessel _))
            {
                CSDebug.LogWarning("[KiteNavigator] No local hull is flying yet; start a match or the Toybox first.");
                return null;
            }

            if (!vt.TryGetComponent(out KiteNavigator kite)) kite = vt.gameObject.AddComponent<KiteNavigator>();
            if (cfg) kite.config = cfg;
            return kite;
        }

        /// <summary>Take the navigator off whichever hull it rides, handing the hull back to its human.</summary>
        public static void DetachActive()
        {
            if (_active) Destroy(_active);
        }

        // The local pilot's own hull, flown by a human - or handed to the AI by this component's own swap. Anything else
        // (an AI hull, a remote copy, a hull the Vessel Changer has just spawned on autopilot, a menu that parked the
        // hull on its AI) unbinds, exactly as GobyPistolDuo does on the duo-vessel branch.
        //
        // An AI CREW is the other case that runs (Garrett, 2026-10-20: "AI vessels should be controlled by two AI, one for the
        // wrasse, and one for the grouper"): a hull that is not the local pilot's, flown by its AIPilot on THIS machine (the
        // host owns every AI). Its kite is flown by the navigator AI and has no view, swap or human input.
        bool ShouldRun(out IVessel vessel, out bool aiCrew)
        {
            vessel = null;
            aiCrew = false;
            if (!config || !TryGetComponent(out vessel)) return false;
            var status = vessel.VesselStatus;
            if (VesselVisionShading.LocalVessel == transform)
                return _weTookHull || status == null || !status.AutoPilotEnabled;
            aiCrew = true;
            return config.AICrewNavigates && status != null && status.AutoPilotEnabled
                   && status.Player != null && status.Player.IsNetworkOwner && !status.Player.IsLocalUser;
        }

        /// <summary>
        /// The kite's pose in the world this frame - where the Wrasse's body is drawn (WrasseCrewBodies places the model there
        /// and replicates it, so others see the Wrasse). Its heading follows the kite's flight, eased so a near-standstill does
        /// not spin it. False while no navigator is seated on this machine.
        /// </summary>
        public bool TryGetKitePose(out Vector3 position, out Quaternion rotation)
        {
            if (_vessel == null || !UnityLiveness.Alive(_vessel.Transform)) { position = default; rotation = Quaternion.identity; return false; }
            position = BoardToWorld(_kite.Position.X, _kite.Position.Z);
            var dir = _r * _kiteHeading.X + _f * _kiteHeading.Z;
            rotation = Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir : _f, _u);
            return true;
        }

        /// <summary>True while a crew body draws the kite as a model (the view then draws no flat kite of its own).</summary>
        internal bool BodyDrawsKite { get; set; }

        // the kite's heading on the board, eased toward its velocity (a unit vector; forward until it first moves)
        KiteVec _kiteHeading = new(0f, 1f);

        void EaseKiteHeading(float dt)
        {
            var v = _kite.Velocity;
            float sp = Mathf.Sqrt(v.X * v.X + v.Z * v.Z);
            if (sp < 2f) return;
            float a = 1f - Mathf.Exp(-12f * dt);
            float hx = _kiteHeading.X + (v.X / sp - _kiteHeading.X) * a, hz = _kiteHeading.Z + (v.Z / sp - _kiteHeading.Z) * a;
            float n = Mathf.Sqrt(hx * hx + hz * hz);
            if (n > 1e-4f) _kiteHeading = new KiteVec(hx / n, hz / n);
        }

        /// <summary>True when this seat's kite is flown by an AI crew (both seats AI; no human on this hull).</summary>
        public bool IsAICrew => _aiCrew;
        bool _aiCrew;

        void Bind(IVessel vessel)
        {
            _cfg = config;
            _vessel = vessel;
            _status = vessel.VesselStatus;
            _player = _status?.Player;
            _s = ScreenSettings();
            int seed = System.Environment.TickCount;
            _ai = new KiteNavigatorAI(seed) { Level = _cfg.AISkill };
            _rng = new System.Random(seed ^ 0x5f3759df);
            _kite.Reset(new KiteVec(0f, _s.Anchor * _s.BoardRadius * 0.5f));
            _probeEpoch++;
            _ai.Reset(_kite.Position);
            _aim = _kite.Position;
            _snapped = false;
            _humanNavigates = _weTookHull = _down = _boosting = false;
            Motes.Clear(); Effects.Clear(); _massQueue.Clear(); _lastRam.Clear(); Prisms.Clear(); Rivals.Clear();
            _charge = 0f; _savedBoosts = 0; _lastBlastDestroyed = 0; _blastWatch.Clear(); _blastCounted.Clear(); _massFlashAt = -99f; _boostReadyAt = 0f; _omniPending = 0;
            // the views, the swap and the static handle belong to the local pilot's seat; an AI crew draws nothing
            if (_aiCrew) return;
            if (_active && _active != this) Destroy(_active);
            _active = this;
            _view = new KiteNavigatorView(this);
        }

        // The settings, with the kite's bounds set to the kite view's screen when wholeScreen is on (Garrett, 2026-10-20: "Let
        // the wrasse travel the whole screen space"): a rectangle the screen's shape, viewScale board radii high, centred on
        // the hull as the view is ("center the grouper in the wrasses camera"). An AI crew, which has no view, takes this
        // machine's screen shape too, so its kite ranges as a human's would.
        KiteSettings ScreenSettings()
        {
            var s = _cfg.ToSettings();
            if (!_cfg.WholeScreen) return s;
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            float h = s.BoardRadius * _cfg.ViewScale;
            s.HalfDepth = Mathf.Max(1f, h - _cfg.KiteRadius);
            s.HalfWidth = Mathf.Max(1f, h * aspect - _cfg.KiteRadius);
            s.BoundsCentreZ = 0f;
            return s;
        }

        // Where the kite itself may fly: the screen's rectangle plus kiteOffscreenReach on every side. The screen's own
        // rectangle (_s) stays the board's CONTENT - motes, prisms, rivals, the AI's plans - but it is not a wall for the
        // kite (Garrett, 2026-10-21: "the wrasse should only be limited by the tether"); the cord holds it, and only a long
        // fling meets this far wall.
        KiteSettings FlightSettings()
        {
            var s = _s;
            if (!s.Rectangular) return s;
            s.HalfWidth += _cfg.KiteOffscreenReach;
            s.HalfDepth += _cfg.KiteOffscreenReach;
            return s;
        }

        // Where the hand may be while the human navigates (u): handEdgeMargin inside the screen's sides and top, and the
        // hand line at the bottom (Garrett, 2026-10-20: "pick a nice horizontal line just above the ability totems and don't
        // let the cursor travel below that line"; 2026-10-21: "the mouse should be limited by a small margin significantly
        // less than the cord length"). The kite view is centred on the hull, viewScale board radii high, so a fraction f of
        // the screen's height is ViewCentreZ + (2f - 1) x that.
        bool TryHandRect(out float xMax, out float zLo, out float zHi)
        {
            xMax = zLo = zHi = 0f;
            if (_view == null || !_humanNavigates) return false;
            float h = _s.BoardRadius * _cfg.ViewScale, m = _cfg.HandEdgeMargin;
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            xMax = Mathf.Max(0f, h * aspect - m);
            zHi = ViewCentreZ + h - m;
            zLo = Mathf.Min(zHi, ViewCentreZ + (2f * _view.HandLine01 - 1f) * h);
            return true;
        }

        /// <summary>Where the kite view is centred on the board (u along the board's forward): the hull.</summary>
        internal const float ViewCentreZ = 0f;

        void Unbind()
        {
            if (_vessel == null) return;
            if (_humanNavigates) SetHumanNavigates(false);
            EndBoost();
            _massQueue.Clear();
            _view?.Dispose();
            _view = null;
            _vessel = null; _status = null; _player = null;
            if (_active == this) _active = null;
        }

        void OnDisable() => Unbind();

        void OnDestroy() => Unbind();

        void Update()
        {
            if (!ShouldRun(out var vessel, out bool aiCrew)) { Unbind(); return; }
            if (_vessel != null && aiCrew != _aiCrew) Unbind();   // the hull changed hands: seat it afresh
            if (_vessel == null) { _aiCrew = aiCrew; Bind(vessel); }
            float dt = Time.deltaTime;

            if (!_aiCrew) PollSwap();
            if (dt <= 0f) return;

            _s = ScreenSettings();
            UpdateBoard(dt);
            _view?.Frame(_humanNavigates);
            ShadowRaceRing(dt);
            StepMotes(dt);
            ShadowPrisms();
            ShadowRivals(dt);
            ShadowCreatures();

            _sling = false;
            if (_humanNavigates) ReadKiteInput();
            else StepAI(dt);
            if (TryHandRect(out float hx, out float hzLo, out float hzHi))
                _aim = new KiteVec(Mathf.Clamp(_aim.X, -hx, hx), Mathf.Clamp(_aim.Z, hzLo, hzHi));

            var fly = FlightSettings();
            var prev = _sling ? _kite.Sling(fly, _aim.X, _aim.Z, _down, dt) : _kite.Move(fly, _aim.X, _aim.Z, _down, dt);
            HeadContacts(prev);
            if (_kite.AdvanceTail(_s, _loop)) CloseLoop(_loop);
            EaseKiteHeading(dt);

            StepBoost(dt);
            CountBlast();
            LayMassQueue();
            PruneEffects();
        }

        // after the hull has moved this frame: the views follow it, then draw
        void LateUpdate()
        {
            if (_view == null || _vessel == null || !UnityLiveness.Alive(_vessel.Transform)) return;
            UpdateBoard(0f);
            _view.Frame(_humanNavigates);
            _view.Draw();
        }

        // ------------------------------------------------------------------ seats

        void PollSwap()
        {
            int dir = PilotSwapGesture.DirectionThisFrame();
            if (dir == 0 && PipDoubleTapped()) dir = 1;
            if (dir == 0 || Time.unscaledTime < _swapReadyAt) return;
            _swapReadyAt = Time.unscaledTime + PilotSwap.CooldownSeconds;
            SetHumanNavigates(!_humanNavigates);
        }

        // A double tap on the small view swaps seats - the touch swap (Garrett, 2026-10-19: "when I double tap the pip
        // window to the other vessel it should switch"); 1 / 2 and the D-pad stay the pad and keyboard swap. Two SHORT
        // taps, the second starting soon after the first and near it, both inside the small view. Short is what keeps a
        // flying thumb out: the left thumb lands in that corner, and re-planting it quickly is a drift, not a swap - but
        // a planted thumb stays down, so it never counts as a tap.
        const float PipTapMaxSeconds = 0.3f, PipDoubleTapWindowSeconds = 0.45f, PipDoubleTapMaxPixels = 120f;
        int _pipPressId = int.MinValue;
        float _pipPressAt, _pipLastTapAt = -99f;
        Vector2 _pipPressPos, _pipLastTapPos;

        bool PipDoubleTapped()
        {
            if (_view == null) return false;
            bool doubled = false;

            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touches = touchscreen.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var t = touches[i];
                    if (t.press.wasPressedThisFrame) PipPress(t.touchId.ReadValue(), t.position.ReadValue());
                    if (t.press.wasReleasedThisFrame) doubled |= PipRelease(t.touchId.ReadValue());
                }
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) PipPress(-1, mouse.position.ReadValue());
                if (mouse.leftButton.wasReleasedThisFrame) doubled |= PipRelease(-1);
            }
            return doubled;
        }

        void PipPress(int id, Vector2 at)
        {
            if (!_view.PipContains(at, _humanNavigates)) return;
            _pipPressId = id;
            _pipPressAt = Time.unscaledTime;
            _pipPressPos = at;
        }

        bool PipRelease(int id)
        {
            if (id != _pipPressId) return false;
            _pipPressId = int.MinValue;
            float now = Time.unscaledTime;
            if (now - _pipPressAt > PipTapMaxSeconds) return false;

            bool doubled = _pipPressAt - _pipLastTapAt <= PipDoubleTapWindowSeconds
                           && (_pipPressPos - _pipLastTapPos).sqrMagnitude <= PipDoubleTapMaxPixels * PipDoubleTapMaxPixels;
            _pipLastTapAt = doubled ? -99f : now;
            _pipLastTapPos = _pipPressPos;
            return doubled;
        }

        void SetHumanNavigates(bool navigate)
        {
            if (_humanNavigates == navigate) return;
            _humanNavigates = navigate;
            _down = false;

            // The hull's own AI flies while the human navigates; its flight input is paused so the pointer and the
            // sticks belong to the board - the order the lava lamp and the mode preview hand a hull to its AI in
            // (MenuCrystalClickHandler, ModePreviewSession). Coming back restores both, in the opposite order. Only an
            // autopilot this component started is stopped again: one something else started is not ours to end.
            if (navigate)
            {
                SetFlightInputPaused(true);
                _weTookHull = !_status.AutoPilotEnabled;
                if (_weTookHull) _vessel.ToggleAIPilot(true);
            }
            else
            {
                if (_weTookHull && UnityLiveness.Alive(_vessel.Transform)) _vessel.ToggleAIPilot(false);
                _weTookHull = false;
                SetFlightInputPaused(false);
                _ai.Reset(_kite.Position);
            }
        }

        // the input is the PLAYER's, which outlives this hull (a swap re-homes it), so a destroyed one is skipped
        void SetFlightInputPaused(bool paused)
        {
            if (_player == null) return;
            if (UnityLiveness.Alive(_player.InputController)) _player.InputController.SetPause(paused);
            else if (_player.InputStatus != null) _player.InputStatus.Paused = paused;
        }

        // ------------------------------------------------------------------ the board

        void UpdateBoard(float dt)
        {
            var t = _vessel.Transform;
            Vector3 hf = t.forward, hu = t.up;
            if (!_snapped || _cfg.Frame == KiteBoardFrame.Locked)
            {
                _f = hf; _u = hu; _r = t.right;
                _snapped = true;
                return;
            }

            if (_cfg.Frame == KiteBoardFrame.Heading)
            {
                // tilt with the hull as Eased does...
                float a = 1f - Mathf.Exp(-_cfg.EaseRate * dt);
                _u += (hu - _u) * a;
                _u.Normalize();
                // ...but turn to its HEADING only by the distance flown: a yaw in place turns the Grouper on the screen and
                // leaves the board (and every mote on it) where it is; a medium run brings the view round behind it
                var fp = Vector3.ProjectOnPlane(_f, _u);
                if (fp.sqrMagnitude < 1e-6f) fp = Vector3.ProjectOnPlane(hf, _u);
                fp.Normalize();
                var tp = Vector3.ProjectOnPlane(hf, _u);
                if (tp.sqrMagnitude > 1e-6f && _status != null)
                {
                    float flown = Mathf.Abs(_status.Speed) * dt;
                    fp = Vector3.Slerp(fp, tp.normalized, 1f - Mathf.Exp(-flown / _cfg.HeadingAlignDistance)).normalized;
                }
                _f = fp;
                _r = Vector3.Cross(_u, _f).normalized;
                _f = Vector3.Cross(_r, _u);
                return;
            }

            if (_cfg.Frame == KiteBoardFrame.Eased)
            {
                float a = 1f - Mathf.Exp(-_cfg.EaseRate * dt);
                _f += (hf - _f) * a;
                _u += (hu - _u) * a;
                _f.Normalize();
                _r = Vector3.Cross(_u, _f).normalized;
                _u = Vector3.Cross(_f, _r);
                return;
            }

            var fh = new Vector3(hf.x, 0f, hf.z);
            if (fh.magnitude > 0.15f) _f = fh.normalized;
            _u = Vector3.up;
            _r = Vector3.Cross(_u, _f).normalized;
            _f = Vector3.Cross(_r, _u);
        }

        Vector3 HullPosition => _vessel.Transform.position;

        /// <summary>A world point on the board plane.</summary>
        internal Vector3 BoardToWorld(float x, float z) => HullPosition + _r * x + _f * z;

        /// <summary>A world point's shadow on the board (u).</summary>
        internal KiteVec WorldToBoard(Vector3 world)
        {
            var d = world - HullPosition;
            return new KiteVec(Vector3.Dot(d, _r), Vector3.Dot(d, _f));
        }

        void Shadow(Vector3 world, out float x, out float z, out float y)
        {
            var d = world - HullPosition;
            x = Vector3.Dot(d, _r); z = Vector3.Dot(d, _f); y = Vector3.Dot(d, _u);
        }

        bool Touchable(float y) => Mathf.Abs(y) <= _cfg.ReachY;

        // ------------------------------------------------------------------ motes (navigator only)

        void StepMotes(float dt)
        {
            float R = _s.BoardRadius, A = _s.Anchor * R;
            bool board = _cfg.MoteHome == KiteMoteHome.Board;
            for (int i = Motes.Count - 1; i >= 0; i--)
            {
                var m = Motes[i];
                Shadow(m.World, out float x, out float z, out float y);
                if (m.Has) { m.VX += ((x - m.X) / dt - m.VX) * 0.5f; m.VZ += ((z - m.Z) / dt - m.VZ) * 0.5f; }
                m.X = x; m.Z = z; m.Y = y; m.Has = true;
                if (board) { m.World = BoardToWorld(x, z); m.Y = 0f; }   // flow with the world, stay in the plane
                float dz = m.Z - A;
                if (Mathf.Sqrt(m.X * m.X + dz * dz) >= Mathf.Max(R * 1.9f, KiteNavigatorCore.BoundsReach(_s) * 1.15f + A)
                    || Mathf.Abs(m.Y) >= Mathf.Max(_cfg.ReachY * 5f, 160f))
                    Motes.RemoveAt(i);
                else Motes[i] = m;
            }

            // an Omni crystal: one cluster of each element, evenly round the board's centre, over the usual mote count
            for (; _omniPending > 0; _omniPending--)
            {
                float turn = (float)_rng.NextDouble() * Mathf.PI * 2f, rr = R * _cfg.OmniClusterRadius;
                for (int e = 0; e < 4; e++)
                {
                    float a = turn + e * Mathf.PI * 0.5f, cx = Mathf.Cos(a) * rr, cz = A + Mathf.Sin(a) * rr;
                    for (int i = 0; i < _cfg.OmniMotesPerElement; i++)
                    {
                        float x = cx + Gauss() * _cfg.MoteSpread * 0.6f, z = cz + Gauss() * _cfg.MoteSpread * 0.6f;
                        Motes.Add(new Mote { World = BoardToWorld(x, z), X = x, Z = z, Y = 0f, Has = true, Element = e, Spin = (float)_rng.NextDouble() * 360f });
                    }
                    AddFx(FxKind.Pop, cx, cz);
                }
            }

            // a same-element cluster ahead of the board, so it flows in from the top
            for (int guard = 0; Motes.Count < _cfg.MoteCount && guard < 6; guard++)
            {
                int e = PickElement(), n = Mathf.Max(1, Mathf.RoundToInt(_cfg.MoteCluster * (0.5f + (float)_rng.NextDouble())));
                float cx = ((float)_rng.NextDouble() * 2f - 1f) * R * 0.8f, cz = A + R * (0.75f + (float)_rng.NextDouble() * 0.5f);
                float cy = board ? 0f : Gauss() * _cfg.ReachY * 0.9f;
                for (int i = 0; i < n && Motes.Count < _cfg.MoteCount + 6; i++)
                {
                    float x = cx + Gauss() * _cfg.MoteSpread, z = cz + Gauss() * _cfg.MoteSpread, y = board ? 0f : cy + Gauss() * 6f;
                    var world = BoardToWorld(x, z) + _u * y;
                    Motes.Add(new Mote { World = world, X = x, Z = z, Y = y, Has = true, Element = e, Spin = (float)_rng.NextDouble() * 360f });
                }
            }
        }

        // The hull's own petals set how often each element comes to the kite: weight = base + perLevel x the hull's level
        // in it, so a pilot who has gathered Space sees more Space to loop. In a race (a ring is lit for this pilot) Time
        // comes more often still, among the others: the pilot's boosts are what reach the next ring.
        int PickElement()
        {
            var rs = _status.ResourceSystem;
            float total = 0f;
            for (int e = 0; e < 4; e++) total += _weights[e] = ElementWeight(rs, e);
            float pick = (float)_rng.NextDouble() * total;
            for (int e = 0; e < 4; e++)
            {
                pick -= _weights[e];
                if (pick <= 0f) return e;
            }
            return 3;
        }

        readonly float[] _weights = new float[4];

        float ElementWeight(ResourceSystem rs, int e) =>
            Mathf.Max(0.01f, _cfg.MoteBaseWeight + _cfg.MoteWeightPerLevel * (rs ? rs.GetLevel((Element)(e + 1)) : 0)
                             + (e == 3 && Racing ? _cfg.RaceTimeMoteBonus : 0f));

        /// <summary>A gate race is running and this hull has a next ring.</summary>
        bool Racing => _raceRing;

        // this hull's next race ring this frame (GateRaceController.TryGetNextRing), or null
        RaceGateRing _raceRing;

        // ------------------------------------------------------------------ race rings: the kite threads them

        /// <summary>
        /// The kite is a second body this hull threads race rings with (<see cref="IRingThreadProbe"/>): every gate race's
        /// rings, the Skim Race's and a mode preview's (Garrett, 2026-10-20: "make it so the wrasse can hit switches"). It does
        /// NOT trip the Toybox's toys: it is not an <see cref="ISwitchThreadProbe"/> ("lets flip things so the wrasse does not
        /// activate toys, but the thresher flail does").
        /// False while no navigator is seated (a remote hull, or an AI hull with aiCrewNavigates off), so those hulls thread
        /// with the hull alone.
        /// </summary>
        public bool TryGetRingProbe(out Vector3 position, out int epoch)
        {
            epoch = _probeEpoch;
            if (_vessel == null || !UnityLiveness.Alive(_vessel.Transform)) { position = default; return false; }
            position = BoardToWorld(_kite.Position.X, _kite.Position.Z);
            return true;
        }

        // The ring a gate race has lit for this pilot (this hull's next ring, whoever flies it), as the board sees it. The human navigator sees it in the kite view
        // (it is a world object the kite camera draws); the AI squad mate is handed a point just THROUGH its mouth as its
        // first goal. The kite can thread it only when the board plane cuts its mouth: the ring's centre within its radius
        // of the plane, and its axis not along the board's up (a ring lying flat in the plane has no mouth to cross).
        void ShadowRaceRing(float dt)
        {
            _raceRing = GateRaceController.TryGetNextRing(_vessel, out var next) ? next : null;
            var ring = _raceRing;
            if (!ring) { _ringOnBoard = false; return; }
            Shadow(ring.transform.position, out float x, out float z, out float y);
            Vector3 axis = ring.Axis;
            float ax = Vector3.Dot(axis, _r), az = Vector3.Dot(axis, _f), inPlane = Mathf.Sqrt(ax * ax + az * az);
            bool reachable = Mathf.Abs(y) < ring.Radius * 0.85f && inPlane > 0.35f && KiteNavigatorCore.InBounds(_s, x, z);
            if (!reachable) { _ringOnBoard = false; return; }
            ax /= inPlane; az /= inPlane;
            // go through from the kite's side: the overshoot points away from the kite
            if (ax * (x - _kite.Position.X) + az * (z - _kite.Position.Z) < 0f) { ax = -ax; az = -az; }
            float over = ring.Radius + 20f;
            var at = new KiteVec(x + ax * over, z + az * over);
            _ringVel = _ringOnBoard && dt > 0f ? new KiteVec((x - _ringCentre.X) / dt, (z - _ringCentre.Z) / dt) : default;
            _ringCentre = new KiteVec(x, z);
            _ringAt = at;
            _ringOnBoard = true;
        }

        float Gauss()
        {
            double u = System.Math.Max(1e-9, _rng.NextDouble()), v = _rng.NextDouble();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u)) * System.Math.Cos(2.0 * System.Math.PI * v));
        }

        // ------------------------------------------------------------------ prisms and rivals

        void ShadowPrisms()
        {
            Prisms.Clear();
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;

            // everywhere the kite can go: the screen's rectangle about the hull, or the round board about its centre
            float reach = KiteNavigatorCore.BoundsReach(_s);
            var centre = BoardToWorld(0f, _s.Rectangular ? _s.BoundsCentreZ : _s.Anchor * _s.BoardRadius);
            _query.Clear();
            index.QuerySphere(centre, Mathf.Sqrt(reach * reach + _cfg.ReachY * _cfg.ReachY * 4f), _query);

            var own = OwnDomain;
            foreach (var p in _query)
            {
                if (!p || p.destroyed) continue;
                Shadow(p.transform.position, out float x, out float z, out float y);
                if (!KiteNavigatorCore.InBounds(_s, x, z)) continue;
                var domain = p.Domain;
                Prisms.Add(new PrismShadow { Prism = p, X = x, Z = z, Y = y, Domain = domain, Hostile = domain != own });
                if (Prisms.Count >= MaxPrismShadows) break;
            }
        }

        void ShadowRivals(float dt)
        {
            for (int i = 0; i < Rivals.Count; i++) { var r = Rivals[i]; r.Seen = false; Rivals[i] = r; }

            var players = _cfg.GameData.Players;
            var own = OwnDomain;
            for (int p = 0; p < players.Count; p++)
            {
                var player = players[p];
                if (player == null || player == _player) continue;
                var vessel = player.Vessel;
                var status = vessel?.VesselStatus;
                if (status == null || !UnityLiveness.Alive(vessel.Transform) || status.Domain == own) continue;

                Shadow(vessel.Transform.position, out float x, out float z, out float y);
                int i = IndexOfRival(status);
                var r = i >= 0 ? Rivals[i] : new RivalShadow { Status = status, Id = vessel.Transform.GetInstanceID() };
                if (r.Has) { r.VX += ((x - r.X) / dt - r.VX) * 0.5f; r.VZ += ((z - r.Z) / dt - r.VZ) * 0.5f; }
                r.X = x; r.Z = z; r.Y = y; r.Has = true; r.Seen = true; r.Domain = status.Domain;
                if (i >= 0) Rivals[i] = r; else Rivals.Add(r);
            }

            for (int i = Rivals.Count - 1; i >= 0; i--) if (!Rivals[i].Seen) Rivals.RemoveAt(i);
        }

        int IndexOfRival(IVesselStatus status)
        {
            for (int i = 0; i < Rivals.Count; i++) if (Rivals[i].Status == status) return i;
            return -1;
        }

        // The charged kite rams creatures too (Garrett, 2026-10-18). There is no fauna spatial index: the live fauna of the
        // active cells are walked, as the Stoat's ripple does, and only while the kite holds a charge to spend.
        void ShadowCreatures()
        {
            Creatures.Clear();
            if (_charge < 1f || !FaunaNetworkSync.IsSimAuthority) return;
            var cells = Cell.ActiveCellsSnapshot;
            for (int c = 0; c < cells.Count; c++)
            {
                var cell = cells[c];
                if (!cell) continue;
                var fauna = cell.LiveFauna;
                for (int i = 0; i < fauna.Count; i++)
                {
                    var f = fauna[i];
                    if (!f || f.IsDying || !f.isActiveAndEnabled) continue;
                    Shadow(f.transform.position, out float x, out float z, out float y);
                    if (!KiteNavigatorCore.InBounds(_s, x, z) || !Touchable(y)) continue;
                    Creatures.Add(new FaunaShadow { Fauna = f, X = x, Z = z, Y = y });
                }
            }
        }

        bool OffRamCooldown(IVesselStatus victim) =>
            !_lastRam.TryGetValue(victim, out float t) || Time.time - t >= _cfg.StealCooldown;

        // ------------------------------------------------------------------ who moves the aim

        // The kite follows; nothing is clicked (Garrett, 2026-10-18). The mouse: the kite flies to the cursor. A thumbstick:
        // the kite flies the way the stick leans, as fast as it leans (circle the stick, the kite circles). A touch: the kite
        // flies to a point thumbOffset above the thumb, and coasts when the thumb lifts. Whichever was used last leads.
        // With control Sling (Garrett, 2026-10-20) every input holds the kite on a rope instead: the aim is the hand itself,
        // with no offset (the rope keeps the kite clear of the thumb); lifting the thumb lets go. The mouse and the stick
        // never let go: each is a hand that is always holding, as the no-click rule has it. The AI holds it the same way.
        void ReadKiteInput()
        {
            var pad = Gamepad.current;
            var stick = pad != null ? pad.leftStick.ReadValue() : Vector2.zero;
            var touchscreen = Touchscreen.current;
            bool touching = touchscreen != null && touchscreen.primaryTouch.press.isPressed;
            var mouse = Mouse.current;

            if (touching) _pointer = Pointer.Touch;
            else if (stick.magnitude > _cfg.StickDeadzone) _pointer = Pointer.Stick;
            else if (mouse != null)
            {
                var mp = mouse.position.ReadValue();
                if ((mp - _lastMouse).sqrMagnitude > 4f) _pointer = Pointer.Mouse;
                _lastMouse = mp;
            }

            switch (_pointer)
            {
                case Pointer.Touch:
                {
                    // a lifted thumb lets go of the rope: the kite flies free (Sling), or coasts (Follow)
                    _sling = _cfg.Control == KiteControl.Sling;
                    if (!touching) { _down = false; return; }
                    // a tap on the small view is the seat swap, not an aim
                    if (_view.PipContains(touchscreen.primaryTouch.startPosition.ReadValue(), true)) { _down = false; return; }
                    var at = touchscreen.primaryTouch.position.ReadValue();
                    if (!_sling) at.y += ThumbOffsetPixels();
                    _aim = _view.ScreenToBoard(at);
                    _down = true;
                    return;
                }
                case Pointer.Stick:
                {
                    // Sling: the stick is the hand's place on the board, as the mouse is - centred is the board's centre,
                    // a full lean slingStickReach board radii out - and, like the mouse, it never lets go
                    if (_cfg.Control == KiteControl.Sling)
                    {
                        _sling = true;
                        if (stick.magnitude <= _cfg.StickDeadzone) stick = Vector2.zero;
                        float cz = _s.Rectangular ? _s.BoundsCentreZ : KiteNavigatorCore.Centre(_s).Z;
                        float rx = _cfg.SlingStickReach * (_s.Rectangular ? _s.HalfWidth : _s.BoardRadius);
                        float rz = _cfg.SlingStickReach * (_s.Rectangular ? _s.HalfDepth : _s.BoardRadius);
                        // while a human holds it, the stick spans the hand's rectangle (inside the screen's edges, above the
                        // hand line) the way the mouse does: centred there, a full lean to its edges
                        if (_s.Rectangular && TryHandRect(out float hx, out float lo, out float hi))
                        {
                            cz = 0.5f * (lo + hi);
                            rx = hx;
                            rz = 0.5f * (hi - lo);
                        }
                        _aim = new KiteVec(stick.x * rx, cz + stick.y * rz);
                        // a trigger held lets go - the cord drops and the kite flies free; released, the rope takes it again
                        _down = !(pad != null && (pad.leftTrigger.ReadValue() > 0.5f || pad.rightTrigger.ReadValue() > 0.5f));
                        return;
                    }
                    // the kite's full speed is reached this far from its aim (KiteNavigatorCore.Move), so a full lean is
                    // full speed and the centre is a hover
                    float reach = _s.KiteSpeed / KiteNavigatorCore.ApproachGain;
                    if (stick.magnitude <= _cfg.StickDeadzone) stick = Vector2.zero;
                    _aim = new KiteVec(_kite.Position.X + stick.x * reach, _kite.Position.Z + stick.y * reach);
                    _down = true;
                    return;
                }
                default:
                {
                    if (mouse == null) { _down = false; return; }
                    var at = mouse.position.ReadValue();
                    _sling = _cfg.Control == KiteControl.Sling;
                    if (_cfg.OffsetMouse && !_sling) at.y += ThumbOffsetPixels();
                    _aim = _view.ScreenToBoard(at);
                    // Sling: a click held lets go - the cord drops and the kite flies free; released, the rope takes it again
                    // (a press that starts on the small view is the swap's, not a let-go)
                    if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
                        _clickOnPip = _view.PipContains(at, true);
                    bool held = (mouse.leftButton.isPressed || mouse.rightButton.isPressed) && !_clickOnPip;
                    _down = !(_sling && held);
                    return;
                }
            }
        }

        float ThumbOffsetPixels()
        {
            float dpi = Screen.dpi > 0f ? Screen.dpi : 160f;
            return _cfg.ThumbOffsetMillimetres / 25.4f * dpi;
        }

        void StepAI(float dt)
        {
            _aiMotes.Clear();
            foreach (var m in Motes)
                _aiMotes.Add(new KiteMote { X = m.X, Z = m.Z, VX = m.VX, VZ = m.VZ, Element = m.Element, Touchable = Touchable(m.Y) });

            // prisms are fixed in the world, so on the board they drift against the hull's own velocity
            var hullVelocity = _status.Course * _status.Speed;
            float pvx = -Vector3.Dot(hullVelocity, _r), pvz = -Vector3.Dot(hullVelocity, _f);
            _aiPrisms.Clear();
            foreach (var p in Prisms)
            {
                if (!p.Hostile || !Touchable(p.Y) || p.Prism.prismProperties is { IsSuperShielded: true }) continue;
                _aiPrisms.Add(new KiteTarget { Id = p.Prism.GetInstanceID(), X = p.X, Z = p.Z, VX = pvx, VZ = pvz });
            }

            _aiRivals.Clear();
            bool anyRival = false;
            foreach (var r in Rivals)
            {
                if (!KiteNavigatorCore.InBounds(_s, r.X, r.Z)) continue;
                anyRival = true;
                if (!Touchable(r.Y) || !OffRamCooldown(r.Status)) continue;
                _aiRivals.Add(new KiteTarget { Id = r.Id, X = r.X, Z = r.Z, VX = r.VX, VZ = r.VZ });
            }

            foreach (var c in Creatures)
            {
                anyRival = true;
                _aiRivals.Add(new KiteTarget { Id = c.Fauna.GetInstanceID(), X = c.X, Z = c.Z });
            }

            _ai.HasGoal = _ringOnBoard;
            _ai.Goal = _ringAt;
            _ai.GoalVelocity = _ringVel;
            _ai.ElementWeight[3] = Racing ? _cfg.RaceTimeLoopPreference : 1f;
            _ai.Control = _cfg.Control;
            _ai.SlingRope = _s.SlingRope;
            _ai.SlingLead = _cfg.AISlingLead;
            _ai.Step(_s, _cfg.Growth, _kite, _charge, _aiMotes, _aiPrisms, _aiRivals, anyRival, dt);
            _aim = _ai.Aim;
            _down = _ai.Down;
            _sling = _cfg.Control == KiteControl.Sling;   // the AI's aim is the hand on the rope, as a thumb's is
        }

        // ------------------------------------------------------------------ the head: pop and ram

        void HeadContacts(KiteVec prev)
        {
            var at = _kite.Position;
            float hr = _cfg.KiteRadius + PrismHalfWidth, h2 = hr * hr;
            var impact = (_r * _kite.Velocity.X + _f * _kite.Velocity.Z);
            var own = OwnDomain;
            string name = _status.PlayerName;

            foreach (var p in Prisms)
            {
                if (!p.Hostile || !Touchable(p.Y) || !p.Prism || p.Prism.destroyed) continue;
                if (KiteNavigatorCore.DistanceToSegmentSq(p.X, p.Z, prev, at) > h2) continue;
                p.Prism.Damage(impact, own, name);
                AddFx(FxKind.Pop, p.X, p.Z);
                Play(_cfg.PrismPoppedSfx, p.Prism.transform.position);
            }

            float rr = (_cfg.KiteRadius + RamReach) * (_cfg.KiteRadius + RamReach);
            for (int i = 0; i < Rivals.Count; i++)
            {
                var r = Rivals[i];
                if (!Touchable(r.Y)) continue;
                if (KiteNavigatorCore.DistanceToSegmentSq(r.X, r.Z, prev, at) <= rr) Ram(r, impact);
            }

            // a charged head kills a creature it passes through: the joust's death (heart freed at the strike), a charge each
            foreach (var c in Creatures)
            {
                if (_charge < _cfg.FaunaChargeCost || !c.Fauna || c.Fauna.IsDying) continue;
                if (KiteNavigatorCore.DistanceToSegmentSq(c.X, c.Z, prev, at) > rr) continue;
                if (!c.Fauna.Jousted(name)) continue;
                _charge = Mathf.Max(0f, _charge - _cfg.FaunaChargeCost);
                AddFx(FxKind.Steal, c.X, c.Z);
                Play(_cfg.FaunaStruckSfx, c.Fauna.transform.position);
            }
        }

        /// <summary>
        /// A charged ram is a scored Strike on the rival (PvP is petals only): one gate for the score and the petals, then
        /// whole petals moved one at a time from the rival's fullest element, as many as the kite holds power for. They are
        /// KNOCKED OUT as crystals anyone can take (Eject); at Charge L5 they are STOLEN for the pilot (Garrett, 2026-10-18).
        /// </summary>
        void Ram(RivalShadow rival, Vector3 impact)
        {
            var victim = rival.Status;
            if (_charge < 1f || !OffRamCooldown(victim)) return;
            if (!ElementalTransfer.IsDecidedHere(_status)) return;
            if (!CombatHitDrain.TryAdmit(victim, _status, CombatHitClass.Strike, _cfg.StealCooldown,
                                         ElementalDebuffSources.VesselContact, out int supersededRank))
                return;
            _lastRam[victim] = Time.time;

            int want = Mathf.FloorToInt(_charge), got = 0;
            var rs = victim.ResourceSystem;
            var form = Upgraded(Element.Charge) ? ElementalTransferForm.Steal : ElementalTransferForm.Eject;
            while (want > 0 && rs != null)
            {
                var element = Element.None; int most = 0;
                for (var e = Element.Charge; e <= Element.Time; e++)
                {
                    int level = rs.GetLevel(e);
                    if (level >= 1 && level > most) { most = level; element = e; }
                }
                if (element == Element.None) break;
                int moved = ElementalTransfer.ApplyAuthoritative(form, victim, _status,
                                                                 ElementalTransfer.MaskOf(element), CombatHitDrain.NormalizedPerLevel,
                                                                 impact, ElementalDebuffSources.VesselContact);
                if (moved <= 0) break;
                got++; want--;
            }
            _charge = Mathf.Max(0f, _charge - got);

            _cfg.OnCombatHitLanded.Raise(new CombatHitStats
            {
                ShooterName = _status.PlayerName,
                VictimName = victim.PlayerName,
                HitClass = CombatHitClass.Strike,
                SupersededRank = supersededRank,
            });
            AddFx(FxKind.Steal, rival.X, rival.Z);
            Play(_cfg.StealSfx, BoardToWorld(rival.X, rival.Z));
        }

        // ------------------------------------------------------------------ the tail: loops and their effects

        void CloseLoop(List<KiteVec> poly)
        {
            _ai.OnLoopClosed();
            var centre = KiteNavigatorCore.Centroid(poly);
            Effects.Add(new Fx { Kind = FxKind.Loop, X = centre.X, Z = centre.Z, T0 = Time.time, Loop = poly.ToArray() });

            int charge = 0, mass = 0, space = 0, time = 0, inside = 0;
            for (int i = Motes.Count - 1; i >= 0; i--)
            {
                var m = Motes[i];
                if (!Touchable(m.Y) || !KiteNavigatorCore.PointInPolygon(m.X, m.Z, poly)) continue;
                switch (m.Element) { case 0: charge++; break; case 1: mass++; break; case 2: space++; break; default: time++; break; }
                Motes.RemoveAt(i);
                inside++;
            }
            if (inside == 0) return;

            var at = BoardToWorld(centre.X, centre.Z);
            if (!KiteNavigatorCore.ResolveLoop(charge, mass, space, time, _cfg.MixedLoops, out int element, out int n))
            {
                AddFx(FxKind.Fizzle, centre.X, centre.Z);
                Play(_cfg.LoopFizzledSfx, at);
                return;
            }

            Play(_cfg.LoopClosedSfx, at);
            switch (element)
            {
                case 2: FireSpace(n, centre, at); break;
                case 1: FireMass(n, centre, at); break;
                case 3: FireTime(n, centre, at); break;
                default: FireCharge(n, centre, at); break;
            }
        }

        // SPACE: an AOEExplosion at the loop's centre, on the plane, sparing our own domain
        void FireSpace(int n, KiteVec c, Vector3 at)
        {
            float maxScale = KiteNavigatorCore.Grow(_cfg.SpaceMaxScale, n, _cfg.Growth, _cfg.SpaceMaxScaleCap);
            Effects.Add(new Fx { Kind = FxKind.Space, X = c.X, Z = c.Z, T0 = Time.time, MaxScale = maxScale });
            WatchBlast(at, 0.5f * maxScale);

            var prefabs = _cfg.SpaceExplosions;
            if (prefabs is not { Length: > 0 }) return;

            // Space L5: the blast destroys shielded and super-shielded prisms (Garrett, 2026-10-18). No blast breaks a super
            // shield on its own (ExplosionImpactor and the Burst path both absorb the hit), so the super shields inside the
            // blast's reach are dropped here first, by the sanctioned sequence (DeactivateShields; the Rhino sword, the
            // Thresher, the Shrike), and the blast itself goes out devastating, which takes plain shields outright.
            bool breaker = Upgraded(Element.Space);
            if (breaker) DropSuperShields(at, 0.5f * maxScale);
            var impactor = _vessel.Transform.GetComponentInChildren<VesselImpactor>();
            var init = new AOEExplosion.InitializeStruct
            {
                OwnDomain = _status.Domain,
                Vessel = _vessel,
                AnnonymousExplosion = false,
                OverrideMaterial = _status.AOEExplosionMaterial,
                MaxScale = maxScale,
                SpawnPosition = at,
                SpawnRotation = Quaternion.LookRotation(_f, _u),
                AffectSelfOverride = false,
                DevastatingOverride = breaker ? true : null,
            };
            ExplosionHelper.CreateExplosion(prefabs, init, impactor ? impactor.DIContainer : null);
        }

        // The Space icon shows what the last blast destroyed: its hostile prisms are watched while it runs, and each one that
        // goes is counted once (a pooled prism reused later cannot be counted twice, or uncounted).
        void WatchBlast(Vector3 centre, float radius)
        {
            _blastWatch.Clear();
            _blastCounted.Clear();
            _lastBlastDestroyed = 0;
            _blastWatchUntil = Time.time + BlastDelay + BlastDuration + 0.3f;
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;
            _query.Clear();
            index.QuerySphere(centre, radius, _query);
            var own = OwnDomain;
            foreach (var p in _query)
                if (p && !p.destroyed && p.Domain != own) _blastWatch.Add(p);
        }

        void CountBlast()
        {
            if (_blastWatch.Count == 0) return;
            for (int i = _blastWatch.Count - 1; i >= 0; i--)
            {
                var p = _blastWatch[i];
                if (p && !p.destroyed) continue;
                if (_blastCounted.Add(p)) _lastBlastDestroyed++;
                _blastWatch.RemoveAt(i);
            }
            if (Time.time > _blastWatchUntil) _blastWatch.Clear();
        }

        void DropSuperShields(Vector3 centre, float radius)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;
            _query.Clear();
            index.QuerySphere(centre, radius, _query);
            var own = OwnDomain;
            foreach (var p in _query)
            {
                if (!p || p.destroyed || p.Domain == own || p.prismProperties is not { IsSuperShielded: true }) continue;
                p.DeactivateShields((p.transform.position - centre).normalized * 20f);
            }
        }

        // MASS: a cluster of our domain's prisms at the loop's centre, laid over a few frames
        void FireMass(int n, KiteVec c, Vector3 at)
        {
            int count = Mathf.RoundToInt(KiteNavigatorCore.Grow(_cfg.MassPrisms, n, _cfg.Growth, _cfg.MassPrismCap));
            float rad = Mathf.Max(4f, 2.6f * Mathf.Pow(count, 1f / 3f) * (_cfg.MassPrismScale.z / 5f));
            for (int i = 0; i < count; i++)
            {
                var dir = RandomUnit();
                float d = rad * Mathf.Pow((float)_rng.NextDouble(), 1f / 3f);
                _massQueue.Enqueue((at + dir * d, Quaternion.LookRotation(RandomUnit(), RandomUnit())));
            }
            AddFx(FxKind.Mass, c.X, c.Z);
            _massFlashAt = Time.time;
            Play(_cfg.MassClusterSfx, at);
        }

        Vector3 RandomUnit()
        {
            var v = new Vector3(Gauss(), Gauss(), Gauss());
            return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.up;
        }

        void LayMassQueue()
        {
            if (_massQueue.Count == 0) return;
            var spawner = _status.VesselPrismController;
            var channel = spawner ? spawner.PrismSpawnChannel : null;
            if (!channel) { _massQueue.Clear(); return; }

            var scale = _cfg.MassPrismScale;
            for (int i = 0; i < _cfg.MassPrismsPerFrame && _massQueue.Count > 0; i++)
            {
                var (pos, rot) = _massQueue.Dequeue();
                var ret = channel.RaiseEvent(new PrismEventData
                {
                    ownDomain = _status.Domain,
                    Rotation = rot,
                    SpawnPosition = pos,
                    Scale = scale,
                    PrismType = spawner.SpawnPrismType,
                });
                if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out Prism prism)) continue;

                prism.TargetScale = scale;
                prism.ownerID = _status.PlayerName;
                prism.ChangeTeam(_status.Domain);
                if (Upgraded(Element.Mass)) prism.prismProperties.IsShielded = true;   // Mass L5: Initialize applies it
                prism.Initialize(_status.PlayerName);
                // a stated size: admit it past the pool's scale window AFTER Initialize (whose reset re-clamps it)
                prism.AdmitTargetScale(scale);
                prism.TargetScale = scale;
            }
        }

        // TIME: boosts saved for the pilot. A loop of n motes saves growth^(n-1) boosts of timeBoostSeconds each - the lab's
        // timeBoost x growth^(n-1) seconds, in whole boosts the pilot spends one at a time (the Time icon counts them)
        void FireTime(int n, KiteVec c, Vector3 at)
        {
            int boosts = Mathf.Max(1, Mathf.RoundToInt(KiteNavigatorCore.Grow(1f, n, _cfg.Growth, MaxSavedBoosts)));
            _savedBoosts = Mathf.Min(MaxSavedBoosts, _savedBoosts + boosts);
            AddFx(FxKind.Time, c.X, c.Z);
            Play(_cfg.TimeBoostSfx, at);
        }

        // The pilot spends a saved boost on the trigger (PilotWantsBoost): it runs timeBoostSeconds, the next waits
        // boostCooldown after it ends (none at Time L5), and holding the trigger chains them. With the AI on the hull (the
        // human navigating), it spends them as they come.
        void StepBoost(float dt)
        {
            if (_boosting)
            {
                if (Time.time >= _boostEndsAt) { EndBoost(); return; }
                _status.IsBoosting = true;   // what a boost executor writes; the hull's own boostMultiplier applies
                return;
            }
            bool want = _aiCrew || _humanNavigates ? AIPilotWantsBoost() : PilotWantsBoost();
            if (want && _savedBoosts > 0 && Time.time >= _boostReadyAt)
            {
                _savedBoosts--;
                _boosting = true;
                _boostEndsAt = Time.time + _cfg.TimeBoostSeconds;
                _status.IsBoosting = true;
            }
        }

        // The pilot boosts (Garrett, 2026-10-18: "the pilot should be in control of the boost, use the trigger on gamepad"):
        // the right trigger on a pad, right Shift on the keyboard (both are RightTriggerAnalog), and on a touch screen,
        // where there is no trigger, lifting both thumbs (the touch strategy's Idle). The hull flies on two sticks since
        // round 4d, so the pilot can steer the hull's plane through the mass the kite wants.
        // The AI Grouper's boost (the hull's AI flies whenever the human navigates, and in an AI crew): spend a saved boost
        // while the next race ring is far and roughly ahead, so a boost is not thrown into a turn; outside a race, as they
        // come (the AI pilot has no trigger of its own).
        bool AIPilotWantsBoost()
        {
            if (!_raceRing) return true;
            var to = _raceRing.transform.position - HullPosition;
            float d = to.magnitude;
            return d >= _cfg.AIBoostRingDistance && Vector3.Dot(_vessel.Transform.forward, to / Mathf.Max(1e-3f, d)) > 0.7f;
        }

        bool PilotWantsBoost()
        {
            var input = _player?.InputStatus;
            if (input == null) return false;
            return input.RightTriggerAnalog > 0.5f || input.Idle;
        }

        // The hull's level-5 upgrade in an element: the fleet's replicated gate (the Wrasse's ElementalAbilityMap lists all four
        // elements, unlocking at 5 and relocking below 4).
        bool Upgraded(Element element)
        {
            var abilities = _status?.ElementalAbilityHandler;
            return abilities && abilities.IsUpgradeActive(element);
        }

        void EndBoost()
        {
            if (!_boosting) return;
            _boosting = false;
            _boostReadyAt = Time.time + (Upgraded(Element.Time) ? 0f : _cfg.BoostCooldownSeconds);
            if (UnityLiveness.Alive(_status)) _status.IsBoosting = false;
        }

        // CHARGE: kite power, spent by the next ram on a rival
        void FireCharge(int n, KiteVec c, Vector3 at)
        {
            float add = KiteNavigatorCore.Grow(_cfg.ChargePetals, n, _cfg.Growth, _cfg.ChargeCap);
            _charge = Mathf.Min(_cfg.ChargeCap, _charge + add);
            AddFx(FxKind.Charge, c.X, c.Z);
            Play(_cfg.ChargeSfx, at);
        }

        // ------------------------------------------------------------------ feedback

        void AddFx(FxKind kind, float x, float z)
        {
            if (Effects.Count < 64) Effects.Add(new Fx { Kind = kind, X = x, Z = z, T0 = Time.time });
        }

        void PruneEffects()
        {
            float now = Time.time;
            for (int i = Effects.Count - 1; i >= 0; i--)
            {
                var fx = Effects[i];
                float life = fx.Kind == FxKind.Space ? BlastDelay + BlastDuration + 0.6f : FxLife;
                if (now - fx.T0 > life) Effects.RemoveAt(i);
            }
        }

        static void Play(EventReference evt, Vector3 at)
        {
            if (evt.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(evt, at);
        }
    }
}
