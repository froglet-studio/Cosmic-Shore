using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using FMODUnity;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Meerkat's one ability, the WORMHOLE (<c>R_VesselActions/MEERKAT.md</c>), cloned from the Vessel Studio
    /// (<c>Docs/Studios/VesselStudio/meerkat.html</c>: <c>gateHold</c>, <c>gateForm</c>, <c>gateCommit</c>,
    /// <c>stepTunnel</c>, <c>gateTransit</c>, <c>gateOut</c>, <c>aiInput</c>), its arithmetic in
    /// <see cref="MeerkatWormholeMath"/>.
    ///
    /// <list type="bullet">
    /// <item><b>The reticle</b> rides <c>reticleAhead</c> ahead of the nose: the tunnel RT would form now. The right stick
    /// bends it any way (eased, as the page's <c>TX</c>/<c>TY</c>); LT shortens a straighter one. Out of a tunnel it comes
    /// back after <c>reticleDelaySeconds</c>, rippling out of its centre over <c>reticleGrowSeconds</c>. Only its pilot
    /// sees it (<see cref="MeerkatWormholeView"/>).</item>
    /// <item><b>RT</b> (<see cref="Form"/>, once the reticle is 60% grown) forms it: it ripples out of its centre over
    /// <c>formSeconds</c> (never longer than the dive to it), and the hull dives straight in. The last tunnel collapses
    /// at once.</item>
    /// <item><b>The slide</b> (<see cref="StepRide"/>, flown by <see cref="MeerkatVesselTransformer"/>; round 24, Yash:
    /// "make the main tube as same as the roots ... One single, one a network"): the tunnel is a network of one channel
    /// (<see cref="MeerkatRootMath.Single"/>, its centreline the conic arc, its wall the hyperboloid) flown by the roots' own
    /// flight (<see cref="MeerkatRootMath.Step"/>): it cruises along at <c>rootCruise</c>, RT pushes it faster up to
    /// <c>rootMaxSpeed</c>, the left stick moves it round the tube, the wall bounces it; it comes out of the far mouth (or back
    /// out of the way in) at <c>rideExitMultiplier</c> × its speed before the tunnel, and eases back to cruise.</item>
    /// <item><b>Its life</b>: it stays <c>staySeconds</c> once its rider is out, collapses to its centre over
    /// <c>collapseSeconds</c>, ripples the screen's edges there (<see cref="Ripples"/>) and fades over
    /// <see cref="MeerkatWormholeMath.FadeSeconds"/>. Everyone sees it (<see cref="Live"/>: the bridge, the bent
    /// space).</item>
    /// <item><b>The AI</b> (input only, /vessel-ai): once its reticle is back and its ring is far enough, it plans a
    /// tunnel (<see cref="MeerkatWormholeMath.TryPlan"/>), holds its line while it bends the reticle and squeezes LT for
    /// <c>aiShapeSeconds</c>, then presses RT through the REPLICATED press path, as a person's would be.</item>
    /// </list>
    ///
    /// <para><b>The Omnicrystal ability, the roots</b> (<c>MEERKAT.md</c> §7, the artifact's rounds 16-21): fly through an
    /// omnicrystal (<see cref="MeerkatOmnicrystalField"/>) and roots grow at once from the hull, every way, sized by the
    /// last trunk this hull dug (<see cref="MeerkatRootNetwork.Grow"/>); taken inside a tunnel, they grow the moment it is
    /// out. Inside, the hull flies itself (<see cref="StepRoots"/>, <see cref="MeerkatRootMath.Step"/>): it cruises along the
    /// root on its own, the LEFT stick moves it round the root, RT pushes it faster, the walls bounce it, and it goes down
    /// whichever root it moves into; the camera rides the root's centreline behind it. The AI holds RT and steers across
    /// the root for the one that leads nearest its ring. The owner publishes each network it grows (its seed and frame) and
    /// its rider's way out; every peer grows the same roots from them.</para>
    ///
    /// <para><b>Where it runs.</b> The reticle, the ride and the AI run on the simulating machine only
    /// (<see cref="MantaStingActionExecutor.IsSimAuthority"/>). Every peer draws every tunnel: the owner publishes each
    /// one it forms, and its rider's way out, through <see cref="MeerkatWormholeNetworkRelay"/>; every peer runs the
    /// same life from there. The hull's pose replicates as every vessel's does.</para>
    /// </summary>
    public sealed partial class MeerkatWormholeExecutor : ShipActionExecutorBase
    {
        [Header("Config")]
        [Tooltip("Every number of the wormhole (the Vessel Studio's Meerkat keys, by the same names).")]
        [SerializeField] MeerkatWormholeConfigSO config;

        [Tooltip("The game data the domain palette is read from (a tunnel and its bridge take their maker's domain colour).")]
        [SerializeField] GameDataSO gameData;

        /// <summary>One formed tunnel, everyone's (the page's <c>pr.wh</c>).</summary>
        public sealed class Tunnel
        {
            public Vector3 A, T0, Side;
            public float L, S, R, B, M, Theta, FormT;   // round 7: L = S (the shaft) + R (the bend's radius) x Theta
            public MeerkatWormholeMath.Conic Path;       // round 22: its centreline, one conic arc round its middle
            public MeerkatWormholeMath.Window W;
            public bool Dying;
            public float DieT;
            public Color Colour;
            public MeerkatWormholeExecutor Maker;
            public int Seq;
            public float Born;   // when it formed here (its tree's branches grow from then, MeerkatTreeView)
            public float SwallowLook;   // its swallow's next look round its way in (MeerkatSwallow, round 23)
            // [Garrett's kit hook] the kit its owner stamped on it (MeerkatSolitonKit.Kit* bits) and its exit portal's size,
            // replicated with it; Held while a sentinel guards it (it stands as long as its sentinel)
            public byte Kit;
            public float PortalSize;
            public bool Held;

            public Vector3 At(float s, out Vector3 tangent) => MeerkatWormholeMath.At(A, T0, Side, Path, s, out tangent);
            public void Frame(float s, out Vector3 normal, out Vector3 binormal) =>
                MeerkatWormholeMath.Frame(T0, Side, Path, s, out normal, out binormal);
            public float BendAngle(float s) => MeerkatWormholeMath.BendAngle(Path, s);
            /// <summary>The page's <c>whAtX</c>: the centreline at s, carried on straight before the way in and after the way out.</summary>
            public Vector3 AtExtended(float s)
            {
                if (s <= 0f) return A + T0 * s;
                var p = At(Mathf.Min(s, L), out var tangent);
                return s > L ? p + tangent * (s - L) : p;
            }
            public float Radius(float s) => MeerkatWormholeMath.Radius(B, M, L, W.S0, W.S1, s);
            public float Fade => MeerkatWormholeMath.Fade(Dying, DieT);
            public MeerkatWormholeConfigSO Config => Maker ? Maker.config : null;
        }

        /// <summary>The reticle as its pilot sees it (the page's <c>ftLive.ret</c>, or the fading <c>retGhost</c>).</summary>
        public struct Reticle
        {
            public Vector3 A, T0, Side;
            public MeerkatWormholeMath.Shape Shape;
            public MeerkatWormholeMath.Conic Path;   // its centreline (round 22)
            public float S0, S1, Alpha;
            public bool Ghost;
        }

        /// <summary>A collapse's screen-edge ripple: where, and when it started.</summary>
        public struct Ripple
        {
            public Vector3 Centre;
            public float Start;
            public MeerkatWormholeConfigSO Config;
        }

        static readonly List<Tunnel> s_tunnels = new();
        static readonly List<Ripple> s_ripples = new();
        static readonly List<MeerkatWormholeExecutor> s_executors = new();

        // Per-step markers (perf, 2026-10-29: the Lavalamp captures showed 3-8 ms of Update self time with no breakdown).
        // They show up by name in the profiler and in PerfAutoCapture's prof_*.json.
        static readonly ProfilerMarker s_mTunnels = new("Meerkat.Executor.StepTunnels");
        static readonly ProfilerMarker s_mSwallow = new("Meerkat.Swallow.Step");
        static readonly ProfilerMarker s_mRoots = new("Meerkat.Executor.StepRootNetworks");
        static readonly ProfilerMarker s_mRipples = new("Meerkat.Executor.StepRipples");
        static readonly ProfilerMarker s_mCamera = new("Meerkat.Executor.StepCamera");
        static readonly ProfilerMarker s_mAutopilot = new("Meerkat.Executor.Autopilot");
        static readonly ProfilerMarker s_mOmni = new("Meerkat.Executor.StepOmni");
        static readonly ProfilerMarker s_mGrowRoots = new("Meerkat.Executor.GrowRoots");
        static readonly ProfilerMarker s_mRideSoliton = new("Meerkat.Executor.TryRideAnySoliton");
        static readonly ProfilerMarker s_mReticle = new("Meerkat.Executor.StepReticle");

        /// <summary>Every tunnel alive now, everyone's.</summary>
        public static IReadOnlyList<Tunnel> Live => s_tunnels;
        /// <summary>Every collapse ripple still running.</summary>
        public static IReadOnlyList<Ripple> Ripples => s_ripples;
        /// <summary>Every enabled Meerkat (the view draws its pilot's reticle).</summary>
        public static IReadOnlyList<MeerkatWormholeExecutor> Executors => s_executors;

        IVesselStatus _status;
        MeerkatWormholeNetworkRelay _relay;
        ToggleTranslationModeActionExecutor _stance;
        MeerkatSolitonKit _kit;   // [Garrett's kit hook] the soliton kit beside this executor (R_VesselActions/MEERKAT.md section 10)
        Tunnel _lastRide;         // [Garrett's kit hook] the soliton last ridden, and when (not re-entered for RideAgainSeconds)
        float _lastRideAt = -10f;

        // the reticle (the simulating machine)
        bool _retLive;
        float _retAge;
        float _retEmerge;
        Vector3 _retA, _retT0, _retSide = Vector3.right;
        MeerkatWormholeMath.Shape _retShape;
        readonly MeerkatWormholeMath.Conic _retPath = new(), _ghostPath = new();   // the reticle's centreline, and the fading one's
        Vector2 _stick;   // the right stick the reticle reads (the page's r.sx / r.sy, from the frame before)
        Vector2 _eased;   // ...the reticle easing toward it over reticleSmoothSeconds (the page's r.fx / r.fy, round 3)
        float _easedLq;   // ...and toward LT's squeeze (r.flq)
        bool _ltHeld;
        float _ltHeldFor;
        // the reticle fading as a tunnel forms (the page's retGhost)
        Reticle _ghost;
        float _ghostAlpha, _ghostFormT;

        // the ride
        Tunnel _mine;
        Tunnel _ride;
        CustomCameraController _rideCam;   // the camera this hull's ride is holding on the centreline (round 8)
        Tunnel _camTunnel;                 // the tunnel the ride camera follows, through the ride and just past its way out
        float _rideCamBlendSeconds;        // the camera's own SpectateBlendSeconds, put back when the ride lets it go
        bool _ridePiloted;                 // the ride was formed by the pilot, not by the autopilot (the lava lamp's AI)
        bool _wasAutopilot;                // last frame's AutoPilotEnabled, to see the hand-over
        float _camS;                       // how far along that tunnel's line the hull is (past L after the way out)
        float _rideS;
        // the slide (round 24): the tunnel as a network of one, and the flight through it
        List<MeerkatRootMath.Channel> _slideNet;
        MeerkatRootMath.Flight _slide;
        Vector2 _slideAiStick;
        int _seq;
        Transform _tunnelScaleKey;   // the vessel holding the speed tunnel's ride scale (VesselSpeedTunnel.SetEffectScale)

        // the AI
        float _aiHold, _aiL, _aiM, _aiLt;
        bool _aiShaping, _aiPlanned;
        Vector2 _aiStick;
        System.Random _aiRand;

        // the roots (§7): the network this hull grew last, the flight inside it, the omnicrystal it holds
        const float RootCameraBack = 12f;   // the page's CAM_ROOT_BACK: close behind you, so the camera stays inside the root
        MeerkatRootNetwork _roots;
        MeerkatRootMath.Flight _flight;
        CustomCameraController _rootCam;
        int _rootSeq, _omniCrystal = -1, _rootAiPick = -1, _rootAiChan = -1;
        float _lastTrunk;
        uint _omniSeed;
        bool _omniArmed, _rtHeld, _freestyleChecked;
        Vector2 _rootAiStick;
        static MeerkatRootNetwork s_localRoots;
        static Vector3 s_localHull;
        InputEvents? _formInput;
        // the soliton's swallow (round 23): the prisms and crystals its tunnels pull in, carry through and fire out
        readonly MeerkatSwallow _swallow = new();
        readonly List<ShipActionSO> _bindScratch = new();

        public MeerkatWormholeConfigSO Config => config;
        /// <summary>The hull this Meerkat flies.</summary>
        public IVesselStatus Status => _status;
        /// <summary>[Garrett's kit hook] the soliton kit beside this executor, or null.</summary>
        internal MeerkatSolitonKit Kit => _kit;
        /// <summary>The prisms this Meerkat's tunnels are moving now (pulled, carried or fired; round 23).</summary>
        public int SwallowedPrisms => _swallow.PrismCount;
        /// <summary>...and the crystals.</summary>
        public int SwallowedCrystals => _swallow.CrystalCount;
        /// <summary>The tunnel this hull is sliding now, or null (round 25: what it carries keeps behind the rider).</summary>
        internal Tunnel RidingTunnel => _ride;
        /// <summary>How far along it the rider is (u; below 0 while it dives to the mouth).</summary>
        internal float RideAlong => _rideS;
        /// <summary>True while a tunnel flies this hull (from the press to its way out).</summary>
        public bool IsRiding => _ride != null;
        /// <summary>The way out's speed over the way in's (the config's rideExitMultiplier, as <see cref="StepRide"/> applies it).</summary>
        public float RideExitMultiplier => config ? Mathf.Max(0.05f, RideExit) : 1f;
        /// <summary>The reticle's growth, 0..1 (RT forms from <see cref="MeerkatWormholeMath.FormableEmerge"/>); 0 riding.</summary>
        public float Ready01 => _retLive ? _retEmerge : 0f;
        /// <summary>True while RT would form a tunnel.</summary>
        public bool CanForm => _retLive && _retEmerge >= MeerkatWormholeMath.FormableEmerge;
        /// <summary>How far through its ride the hull is, 0..1 (0 when not riding).</summary>
        public float Ride01 => _ride != null ? MeerkatWormholeMath.Clamp01(_rideS / Mathf.Max(1e-3f, _ride.L)) : 0f;
        /// <summary>The AI is shaping its reticle: it holds its line (the page's left stick at rest while it bends).</summary>
        public bool AIHoldsLine => _aiShaping;
        /// <summary>Tunnels this hull has formed since it spawned.</summary>
        public int Formed => _seq;
        /// <summary>True while this hull flies inside its roots (from the omnicrystal to its way out).</summary>
        public bool IsInRoots => _flight != null;
        /// <summary>The roots the local pilot is inside, or null (<see cref="MeerkatTreeView"/> draws their inside).</summary>
        public static MeerkatRootNetwork LocalRootNetwork => s_localRoots;
        /// <summary>The local pilot's hull (the lamp that lights the roots' walls).</summary>
        public static Vector3 LocalHullPosition => s_localHull;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _relay = GetComponentInParent<MeerkatWormholeNetworkRelay>();
            _stance = TryGetComponent<ActionExecutorRegistry>(out var registry) ? registry.Get<ToggleTranslationModeActionExecutor>() : null;
            _kit = GetComponent<MeerkatSolitonKit>();   // [Garrett's kit hook]
            _lastRide = null;
            if (_relay) _relay.Bind(this);
            _retLive = false;
            _retAge = 0f;
            _retSide = Vector3.right;
            _stick = _eased = Vector2.zero;
            _easedLq = 0f;
            _ltHeld = false;
            _ghostAlpha = 0f;
            _ride = null;
            _aiHold = 0f;
            _aiShaping = _aiPlanned = false;
            _formInput = null;
            _aiRand = new System.Random(GetInstanceID());
            _flight = null;
            _omniArmed = _rtHeld = _freestyleChecked = false;
            _omniCrystal = -1;
            if (!s_executors.Contains(this)) s_executors.Add(this);
            if (config == null)
                CSDebug.LogError($"[Meerkat] {name}: MeerkatWormholeExecutor has no MeerkatWormholeConfig - RT will form nothing. " +
                                 "Wire Assets/_SO_Assets/VesselActions/Meerkat/MeerkatWormholeConfig.asset on the prefab.");
        }

        bool IsAuthority => _status != null && MantaStingActionExecutor.IsSimAuthority(_status);

        // ------------------------------------------------------------------ the triggers

        /// <summary>RT: form the tunnel the reticle shows and dive in (the page's <c>gateForm</c>).</summary>
        public void Form()
        {
            _rtHeld = true;   // inside the roots RT pushes you faster while held
            if (!config || !IsAuthority) return;
            if (!_retLive || _retEmerge < MeerkatWormholeMath.FormableEmerge) return;   // still rippling into shape
            Commit();
        }

        /// <summary>LT pressed (a key or a touch ramps it; a pad's analog trigger is read live).</summary>
        public void BeginShorten()
        {
            _ltHeld = true;
            _ltHeldFor = 0f;
        }

        public void EndShorten() => _ltHeld = false;

        /// <summary>RT released.</summary>
        public void EndForm() => _rtHeld = false;

        float RtLive()
        {
            if (_status.AutoPilotEnabled) return 1f;   // the AI holds RT in the roots
            if (!_rtHeld) return 0f;
            var input = _status.InputStatus;
            bool analog = input != null && input.ActiveInputDevice == InputDeviceType.Gamepad;
            return analog ? MeerkatWormholeMath.Clamp01(input.RightTriggerAnalog) : 1f;
        }

        float LtLive()
        {
            if (_status.AutoPilotEnabled) return _aiLt;
            if (!_ltHeld) return 0f;
            var input = _status.InputStatus;
            bool analog = input != null && input.ActiveInputDevice == InputDeviceType.Gamepad;
            return analog
                ? MeerkatWormholeMath.Clamp01(input.LeftTriggerAnalog)
                : MeerkatWormholeMath.Clamp01(_ltHeldFor / Mathf.Max(0.05f, config.KeyRampSeconds));
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (!config || _status == null) return;
            float dt = Time.deltaTime;
            using (s_mTunnels.Auto()) StepTunnels(dt);
            using (s_mSwallow.Auto()) _swallow.Step(this, dt);
            using (s_mRoots.Auto()) StepRootNetworks(dt);
            using (s_mRipples.Auto()) StepRipples();
            TrackLocalPilot();
            if (_ghostAlpha > 0f) _ghostAlpha -= dt / Mathf.Max(0.05f, _ghostFormT);
            if (!IsAuthority) return;
            bool autopilot = _status.AutoPilotEnabled;
            if (autopilot != _wasAutopilot) HandOver();
            _wasAutopilot = autopilot;
            using (s_mCamera.Auto())
            {
                StepSpeedTunnelScale();
                StepRideCamera();
            }
            if (_ltHeld) _ltHeldFor += dt;
            if (autopilot) { using (s_mAutopilot.Auto()) Autopilot(dt); }
            else _aiShaping = false;
            using (s_mOmni.Auto()) StepOmni();
            if (_kit) { using (s_mRideSoliton.Auto()) TryRideAnySoliton(); }   // [Garrett's kit hook] anyone rides any soliton
            using (s_mReticle.Auto()) StepReticle(dt);
        }

        void LateUpdate() => MeerkatWormholeView.DrawOncePerFrame();

        /// <summary>The page's <c>gateHold</c> reticle: on the nose, bent by last frame's stick, shortened by LT.</summary>
        void StepReticle(float dt)
        {
            var hull = _status.Transform;
            bool live = hull && _ride == null && _flight == null && !_status.IsStationary;
            if (!live)
            {
                _retLive = false;
                _retAge = 0f;
                _stick = _eased = Vector2.zero;   // the page makes a fresh reticle: it grows back from straight
                _easedLq = 0f;
                return;
            }
            _retLive = true;
            _retAge += dt;
            _retEmerge = MeerkatWormholeMath.ReticleEmerge(_retAge, config.ReticleDelaySeconds, config.ReticleGrowSeconds);
            Vector3 fwd = hull.forward, right = hull.right, up = hull.up;
            _retA = hull.position + fwd * Mathf.Max(0f, config.ReticleAhead);
            _retT0 = fwd;
            // round 3 (Yash: "a little smoothy, so that it feels good"): the reticle eases toward what the sticks ask for
            float ke = MeerkatWormholeMath.ReticleEase(dt, config.ReticleSmoothSeconds);
            _eased += (_stick - _eased) * ke;
            _easedLq += (LtLive() - _easedLq) * ke;
            var sh = MeerkatWormholeMath.ShapeFor(config.Shape(SpaceLevel()), _easedLq, _eased.x, _eased.y);   // round 23: Space sets its area
            Vector3 c = sh.Stick >= 0.05f ? right * sh.Dx + up * sh.Dy : _retSide;
            c -= fwd * Vector3.Dot(c, fwd);
            _retSide = c.sqrMagnitude > 1e-8f ? c.normalized : right;
            _retShape = sh;
            _retPath.Set(sh.L, sh.Theta, sh.Oval);
            // the right stick bends the reticle from the next frame (the page sets r.sx / r.sy after gateHold)
            _stick = _status.AutoPilotEnabled ? _aiStick
                : _status.InputStatus != null ? _status.InputStatus.EasedRightJoystickPosition : Vector2.zero;
        }

        /// <summary>
        /// The hull's Space level, 0-10 (round 23, Garrett: "at a given Space level a soliton keeps one surface area"): read at
        /// use time, every frame the reticle is shaped, so a crystal taken mid-flight resizes the next tunnel. -1 (the
        /// authored fixed size) when the hull has no element levels.
        /// </summary>
        float SpaceLevel()
        {
            var resources = _status?.ResourceSystem;
            return resources ? Mathf.Clamp(resources.GetLevel(Element.Space), 0, 10) : -1f;
        }

        /// <summary>The page's <c>gateCommit</c>.</summary>
        void Commit()
        {
            var hull = _status.Transform;
            CollapseLast();   // the last one goes back to its centre now (unless a sentinel holds it)
            float vIn = Mathf.Max(1f, _status.Speed), dive = Vector3.Distance(hull.position, _retA) / vIn;
            // round 17: the trunk's size (its shaft against the longest LT digs) sizes the roots of the next omnicrystal
            var shape = config.Shape();
            _lastTrunk = MeerkatWormholeMath.Clamp01((_retShape.S - shape.ShaftMin) / Mathf.Max(1f, shape.ShaftMax - shape.ShaftMin));
            var t = AddTunnel(_retA, _retT0, _retSide, _retShape, MeerkatWormholeMath.FormTime(config.FormSeconds, dive), ++_seq);
            _ghostPath.Set(_retShape.L, _retShape.Theta, _retShape.Oval);
            _ghost = new Reticle { A = _retA, T0 = _retT0, Side = _retSide, Shape = _retShape, Path = _ghostPath, S0 = 0f, S1 = _retShape.L, Ghost = true };
            _ghostAlpha = 1f;
            _ghostFormT = t.FormT;
            // the ride starts at the hull: it dives straight down to the mouth the reticle showed (s < 0), then into the throat
            _ride = t;
            _slideNet = MeerkatRootMath.Single(new SlidePath(t), t.L);
            _slide = MeerkatRootMath.Enter(_slideNet, 0, MeerkatRootNetwork.D(hull.position), vIn, MeerkatRootNetwork.D(hull.up));
            _rideS = (float)_slide.S;
            _ridePiloted = !_status.AutoPilotEnabled;
            _retLive = false;
            _retAge = 0f;
            if (_status.IsTranslationRestricted && _stance) _stance.EndStance();   // the page's `if (stance) stance = false`
            if (_kit) _kit.Stamp(t);   // [Garrett's kit hook] the owner decides the soliton's kit, published with it
            if (_relay) _relay.PublishFormed(t);
            if (_kit) _kit.OnFormed(t);   // [Garrett's kit hook] the exit portal, and a sentinel in Sentinel mode
            PlayOneShot(config.FormEvent, hull.position);
            CSDebug.LogVerbose(CSLogChannel.BlackHole, $"[Meerkat] tunnel {t.Seq}: {t.L:F0} u, bent {t.Theta * Mathf.Rad2Deg:F0} deg");
        }

        Tunnel AddTunnel(Vector3 a, Vector3 t0, Vector3 side, in MeerkatWormholeMath.Shape sh, float formT, int seq)
        {
            var t = new Tunnel
            {
                A = a, T0 = t0, Side = side, L = sh.L, S = sh.S, R = sh.R, B = sh.B, M = sh.M, Theta = sh.Theta, FormT = formT,
                Path = new MeerkatWormholeMath.Conic(sh.L, sh.Theta, sh.Oval),
                W = MeerkatWormholeMath.NewWindow(sh.L), Colour = DomainColour(), Maker = this, Seq = seq, Born = Time.time,
            };
            _mine = t;
            s_tunnels.Add(t);
            return t;
        }

        /// <summary>A remote peer's copy of a tunnel its owner formed (<see cref="MeerkatWormholeNetworkRelay"/>).</summary>
        internal void FormRemote(int seq, Vector3 a, Vector3 t0, Vector3 side, float L, float shaft, float bendRadius, float b,
            float m, float theta, float formT, byte kit, float portalSize)
        {
            if (!config || IsAuthority) return;
            CollapseLast();
            var t = AddTunnel(a, t0, side, new MeerkatWormholeMath.Shape { L = L, S = shaft, R = bendRadius, B = b, M = m, Theta = theta,
                    Oval = MeerkatWormholeMath.OvalOf(config.Shape(), L, theta) },   // the same config, so the same arc as its owner's
                formT, seq);
            t.Kit = kit;   // [Garrett's kit hook] what its owner stamped on it
            t.PortalSize = portalSize;
            if (_kit) _kit.OnFormed(t);
        }

        /// <summary>A new tunnel (or roots) collapses the last one at once, unless a sentinel holds it ([Garrett's kit hook]:
        /// a soliton with a sentinel stands as long as its sentinel).</summary>
        void CollapseLast()
        {
            if (_mine != null && !_mine.Dying && !_mine.Held) MeerkatWormholeMath.CollapseNow(ref _mine.W);
        }

        /// <summary>
        /// [Garrett's kit hook] The page's <c>kitTryRide</c>: anyone rides any soliton, its maker or anyone else. A Meerkat
        /// that flies into a standing soliton's way in (any Meerkat's) is carried through it at the speed it went in with
        /// (and its own Time's rush), out on its last heading. The soliton is untouched by riders.
        /// </summary>
        void TryRideAnySoliton()
        {
            var hull = _status.Transform;
            if (_ride != null || _flight != null || !hull || _status.IsStationary) return;
            for (int i = 0; i < s_tunnels.Count; i++)
            {
                var t = s_tunnels[i];
                if (!MeerkatSwallow.Standing(t)) continue;
                if (t == _lastRide && Time.time - _lastRideAt < MeerkatSolitonMath.RideAgainSeconds) continue;
                if (!MeerkatSolitonMath.CanEnter(hull.position, hull.forward, t.A, t.T0, t.M, out float along)) continue;
                // round 24: a ride is a slide; a rider enters someone's soliton the same way its maker does (MeerkatRootMath.Enter)
                _ride = t;
                _slideNet = MeerkatRootMath.Single(new SlidePath(t), t.L);
                _slide = MeerkatRootMath.Enter(_slideNet, 0, MeerkatRootNetwork.D(hull.position), Mathf.Max(1f, _status.Speed), MeerkatRootNetwork.D(hull.up));
                _rideS = (float)_slide.S;   // entered where the hull meets the way in (CanEnter's `along`)
                _retLive = false;
                _retAge = 0f;
                _aiPlanned = false;
                _lastRide = t;
                _lastRideAt = Time.time;
                return;
            }
        }

        /// <summary>A remote peer hears that its owner's rider came out of tunnel <paramref name="seq"/>.</summary>
        internal void RiderOutRemote(int seq)
        {
            if (IsAuthority || _mine == null || _mine.Seq != seq) return;
            MeerkatWormholeMath.RiderOut(ref _mine.W);
        }

        /// <summary>
        /// The page's <c>gateTransit</c>, flown by <see cref="MeerkatVesselTransformer"/> in place of its own move: where
        /// the tunnel puts the hull this frame, its facing, its speed and its heading. False when the hull is not riding.
        /// <paramref name="cameOut"/> is true on the frame it leaves the way out (its speed is then the entry speed).
        /// </summary>
        public bool StepRide(float dt, Quaternion current, out Vector3 position, out Quaternion rotation, out float speed,
            out Vector3 heading, out bool cameOut)
        {
            position = default;
            rotation = current;
            speed = 0f;
            heading = default;
            cameOut = false;
            var t = _ride;
            var g = _slide;
            if (t == null || g == null) return false;
            if (!s_tunnels.Contains(t))
            {
                _ride = null;   // the tunnel went from under the hull (a despawn): fly on
                _slide = null;
                return false;
            }
            // round 24: the roots' flight, its input read the same way (StepRoots)
            float side, rise, th;
            if (_status.AutoPilotEnabled) { side = _slideAiStick.x; rise = _slideAiStick.y; th = 1f; }
            else
            {
                var input = _status.InputStatus;
                var stick = input != null ? input.EasedLeftJoystickPosition : Vector2.zero;
                side = stick.x;
                rise = stick.y;
                th = RtLive();
            }
            var flight = config.RootFlight();
            if (_kit)   // [Garrett's kit hook] Time: the rush scales the slide's speed; the L5 Boost out its exit (every soliton)
            {
                float rush = Rush;
                flight.Cruise *= rush;
                flight.MaxSpeed *= rush;
                flight.ExitMultiplier = RideExit;
            }
            var res = MeerkatRootMath.Step(g, _slideNet, null, flight, th, side, rise, dt, out var nose);
            var hull = _status.Transform;
            position = MeerkatRootNetwork.V(g.Pos);
            heading = MeerkatRootNetwork.V(nose);
            var up = g.Free ? MeerkatRootNetwork.V(g.Upv) : (hull ? hull.up : Vector3.up);
            rotation = Pose(heading, up, current);
            speed = (float)(g.Free ? System.Math.Sqrt(g.Vel.LengthSq) : g.VIn);
            _rideS = (float)g.S;
            if (res == MeerkatRootMath.FlightResult.Riding) return true;
            // out of the far mouth on its last heading, or back out of the way in (the page's gateOut): out at
            // rideExitMultiplier x the speed before the tunnel (a little boost, which the transformer eases back to cruise)
            cameOut = true;
            speed = (float)g.VIn * Mathf.Max(0.05f, RideExit);
            _ride = null;
            _slide = null;
            _lastRide = t;
            _lastRideAt = Time.time;
            if (t.Maker == this)   // [Garrett's kit hook] a rider of someone else's soliton leaves it untouched
            {
                MeerkatWormholeMath.RiderOut(ref t.W);
                if (_relay) _relay.PublishRiderOut(t.Seq);
            }
            return true;
        }

        /// <summary>
        /// The main tunnel as a tube the roots' flight slides (round 24, the page's <c>slAt</c> / <c>slNear</c> / <c>slR</c>):
        /// its centreline the conic arc (straight on past either end), its wall the full hyperboloid.
        /// </summary>
        sealed class SlidePath : MeerkatRootMath.ITubePath
        {
            readonly Tunnel _t;

            public SlidePath(Tunnel t) => _t = t;

            public MeerkatRootMath.D3 At(double s, out MeerkatRootMath.D3 tangent)
            {
                float f = (float)s;
                Vector3 p, tan;
                if (f <= 0f) { tan = _t.T0; p = _t.A + _t.T0 * f; }
                else if (f >= _t.L) { p = _t.At(_t.L, out tan); p += tan * (f - _t.L); }
                else p = _t.At(f, out tan);
                tangent = MeerkatRootNetwork.D(tan);
                return MeerkatRootNetwork.D(p);
            }

            public double Nearest(MeerkatRootMath.D3 p) =>
                MeerkatWormholeMath.NearestAlong(_t.A, _t.T0, _t.Side, _t.Path, _t.L, MeerkatRootNetwork.V(p));

            public double Radius(double s) =>
                MeerkatWormholeMath.Radius(_t.B, _t.M, _t.L, 0f, _t.L, Mathf.Clamp((float)s, 0f, _t.L));
        }

        // [Garrett's kit hook] Time: the ride's throat with the rush, and its exit with the L5 Boost out (every soliton)
        // round 24 retired rideMidMultiplier (the tunnel is slid): the rush scales the slide's cruise and top speed instead
        float Rush => _kit ? _kit.RideMid(1f) : 1f;
        float RideExit => _kit ? _kit.RideExit(config.RideExitMultiplier) : config.RideExitMultiplier;

        void StepTunnels(float dt)
        {
            for (int i = s_tunnels.Count - 1; i >= 0; i--)
            {
                var t = s_tunnels[i];
                if (t.Maker != this) continue;
                if (t.Dying)
                {
                    t.DieT += dt;
                    if (t.DieT >= MeerkatWormholeMath.FadeSeconds) s_tunnels.RemoveAt(i);
                    continue;
                }
                // [Garrett's kit hook] a soliton a sentinel holds never runs out its stay
                float stay = t.Held ? float.PositiveInfinity : config.StaySeconds;
                if (!MeerkatWormholeMath.Step(ref t.W, t.L, t.FormT, stay, config.CollapseSeconds, dt)) continue;
                t.Dying = true;
                t.DieT = 0f;
                var centre = t.At(t.W.S0, out _);
                if (config.RippleStrength > 0f)
                    s_ripples.Add(new Ripple { Centre = centre, Start = Time.time, Config = config });
                PlayOneShot(config.CollapseEvent, centre);
            }
        }

        static void StepRipples()
        {
            for (int i = s_ripples.Count - 1; i >= 0; i--)
            {
                var r = s_ripples[i];
                if (!r.Config || Time.time - r.Start >= Mathf.Max(0.2f, r.Config.RippleSeconds)) s_ripples.RemoveAt(i);
            }
        }

        /// <summary>
        /// The reticle this machine's pilot sees: the live one, else the one fading as its tunnel forms. Only for the local
        /// human's own hull (an AI's reticle is never drawn).
        /// </summary>
        public bool TryGetReticle(out Reticle reticle, out Reticle ghost, out bool hasGhost)
        {
            reticle = ghost = default;
            hasGhost = false;
            if (!config || _status == null || _status.Player == null || _status.IsInitializedAsAI || !_status.IsLocalUser)
                return false;
            if (_ghostAlpha > 0f)
            {
                ghost = _ghost;
                ghost.Alpha = Mathf.Max(0f, _ghostAlpha) * 0.8f;
                hasGhost = true;
            }
            if (!_retLive || _retEmerge <= 0.001f) return hasGhost;
            // the page's updateWormholes: faint at rest, full while the right stick or LT shapes it, rippling out of its centre
            float rest = MeerkatWormholeMath.Clamp01(config.ReticleIdleAlpha), e = _retEmerge;
            float alpha = (rest + (1f - rest) * MeerkatWormholeMath.Clamp01(Mathf.Max(_retShape.Stick / 0.3f, _easedLq * 2f))) *
                          (0.35f + 0.65f * e);
            reticle = new Reticle
            {
                A = _retA, T0 = _retT0, Side = _retSide, Shape = _retShape, Path = _retPath, Alpha = alpha,
                S0 = e >= 1f ? 0f : 0.5f * _retShape.L * (1f - e), S1 = e >= 1f ? _retShape.L : 0.5f * _retShape.L * (1f + e),
            };
            return true;
        }

        /// <summary>
        /// Round 5 ("use 5x the normal value when going through the wormhole"): while a tunnel flies the hull, the game's speed
        /// tunnel (<see cref="VesselSpeedTunnel"/>, the dolly zoom every vessel has) is <c>rideSpeedTunnelScale</c> times as
        /// strong. It only ever reaches the camera of the vessel the law is driven by (the local pilot's).
        /// </summary>
        void StepSpeedTunnelScale()
        {
            var key = _status.Transform;
            if (_ride != null && key)
            {
                VesselSpeedTunnel.SetEffectScale(config.RideSpeedTunnelScale, key);
                _tunnelScaleKey = key;
            }
            else ReleaseSpeedTunnelScale();
        }

        void ReleaseSpeedTunnelScale()
        {
            if (!_tunnelScaleKey) return;
            VesselSpeedTunnel.ClearEffectScale(_tunnelScaleKey);
            _tunnelScaleKey = null;
        }

        /// <summary>
        /// The ride camera (rounds 8 and 10, Yash: "I want the camera to go inside the tunnel"; "the cameras, why offset should
        /// always be zero, the vessel should be in the center of the screen and this way we don't need to lerp the camera"), the
        /// page's <c>cameraStep</c>. The Meerkat's own camera (<c>MeerkatCameraSettingsSO</c>) sits straight behind the hull
        /// with no offset and no lag, so the hull is always at the centre of the screen. While a tunnel flies the hull, the
        /// camera stays that same distance behind it but ON the tube's centreline, looking at the hull, through
        /// <see cref="CustomCameraController.Spectate"/> with no blend: on the straight run in it is exactly where the follow
        /// camera already was, and round the bend it never cuts the chord out of the tube. Out of the far mouth it keeps to the
        /// tunnel's line until it is past the mouth itself (a straight-behind camera would jump sideways there). Only the local
        /// human pilot's camera, and only while it follows this hull.
        /// </summary>
        void StepRideCamera()
        {
            if (StepRootCamera()) return;
            float dt = Time.deltaTime;
            // only a ride the PILOT formed: the autopilot's (the lava lamp's AI) never moves anyone's camera, and a ride it
            // was in the middle of when the pilot clicked to fly is left to the follow camera the menu just handed over to
            if (_ride != null) { _camTunnel = _ridePiloted ? _ride : null; _camS = _rideS; }
            else if (_camTunnel != null) _camS += Mathf.Max(0f, _status.Speed) * dt;
            var t = _camTunnel;
            bool mine = t != null && s_tunnels.Contains(t) && _status.Player != null && _status.IsLocalUser && !_status.IsInitializedAsAI
                        && !_status.AutoPilotEnabled;
            var cam = mine && CameraManager.Instance != null
                ? CameraManager.Instance.GetActiveController() as CustomCameraController
                : null;
            var hull = _status.Transform;
            if (cam && (cam.PlacementAnchor.HasValue || !cam.FollowTarget ||
                        (cam.FollowTarget != hull && !cam.FollowTarget.IsChildOf(hull))))
                cam = null;   // a cinematic or end-of-match camera pointed elsewhere is not ours to move
            // the follow camera's own distance (its offset is (0, 0, -back)), warp field included, so the hand-over is a cut
            float back = cam ? Mathf.Abs(cam.GetFollowOffset().z) * WarpFieldRuntime.ScaleAt(hull.position) : 0f;
            if (cam && _ride == null && _camS - back >= t.L) cam = null;   // past the mouth: the follow camera is on the line
            if (!cam) _camTunnel = null;
            if (cam != _rideCam) ReleaseRideCamera();
            if (!cam) return;
            if (_rideCam != cam) _rideCamBlendSeconds = cam.SpectateBlendSeconds;
            _rideCam = cam;
            cam.SpectateBlendSeconds = 0f;   // a cut both ways: the two cameras coincide where they hand over
            cam.Spectate = new CustomCameraController.SpectateView
            {
                Position = t.AtExtended(_camS - back), LookAt = hull.position, Up = hull.up,
            };
        }

        void ReleaseRideCamera()
        {
            if (!_rideCam) { _rideCam = null; return; }
            // back to the follow camera, which is on the same line by then: a cut, done now rather than by leaving the
            // blend at 0 (that leaked into every later Spectate, and a camera switched off mid-ride - the menu - kept
            // its stale vantage blended in until it next ran)
            _rideCam.EndSpectateNow();
            _rideCam.SpectateBlendSeconds = _rideCamBlendSeconds;
            _rideCam = null;
        }

        /// <summary>
        /// The pilot took the hull from the autopilot (the lava lamp's AI hands over when you click to fly), or gave it
        /// back. What the AI was shaping is not the pilot's: drop its plan, its held line and its sticks, and let go of
        /// the ride camera so the follow camera the menu hands over to is the one that frames the hull.
        /// </summary>
        void HandOver()
        {
            _aiHold = 0f;
            _aiPlanned = false;
            _aiShaping = false;
            _aiStick = Vector2.zero;
            _aiLt = 0f;
            _camTunnel = null;
            ReleaseRideCamera();
        }

        // ------------------------------------------------------------------ the AI (input only)

        /// <summary>
        /// The page's <c>aiInput</c> wormhole half. The flight is the platform's <c>AIPilot</c> (it steers for the race's
        /// line); this adds the wormhole a person would: plan, bend the reticle toward the ring with the right stick while
        /// holding the line, squeeze LT as the plan says, then press RT. Simulating machine only.
        /// </summary>
        void Autopilot(float dt)
        {
            _aiStick = Vector2.zero;
            _aiLt = 0f;
            _aiShaping = false;
            var ai = _status.AIPilot;
            var hull = _status.Transform;
            if (_flight != null) { RootAutopilot(); return; }
            if (!config.AiWormhole || !ai || !hull) return;
            if (_ride != null)
            {
                _aiHold = 0f;
                _aiPlanned = false;
                SlideAutopilot();
                return;
            }
            float d = Vector3.Distance(hull.position, ai.ObjectivePosition);
            // the bearing of the point it steers for, at the range of the ring it is after (StoatDipoleMath.AutopilotGauge)
            var local = StoatDipoleMath.AutopilotGauge(Quaternion.Inverse(hull.rotation) * (ai.TargetPosition - hull.position), d);
            bool bend = false;
            if (_aiPlanned)
            {
                _aiHold -= dt;
                _aiLt = _aiL;
                bend = true;
                // shaped, and (round 3: the reticle eases) its reticle caught up with its sticks: pull RT
                if (_aiHold <= 0f && (MeerkatWormholeMath.ReticleSettled(_stick, _eased, _aiL, _easedLq) ||
                                      _aiHold <= -MeerkatWormholeMath.AiSettleTimeout))
                {
                    _aiPlanned = false;
                    PressForm();
                }
            }
            else if (d > Mathf.Min(config.AiStraightMinDistance, config.AiBentMinDistance) && CanForm &&
                     MeerkatWormholeMath.TryPlan(config.Plan(TurnRateDegrees(), SpaceLevel()), local, Mathf.Max(1f, _status.Speed),
                         MeerkatWormholeMath.Clamp01(config.AiNoise), (float)_aiRand.NextDouble(),
                         d <= config.AiStraightMinDistance, out float lq, out float m))
            {
                _aiL = _aiLt = lq;
                _aiM = m;
                _aiHold = Mathf.Max(dt, config.AiShapeSeconds);
                _aiPlanned = true;
                bend = true;
            }
            if (!bend) return;
            _aiShaping = true;
            _aiStick = MeerkatWormholeMath.PlanStick(local, _aiM);
        }

        /// <summary>The hull's own turn rate, deg/s: its transformer's YawScaler (the studio's pitchYaw, 120 on the Meerkat).</summary>
        float TurnRateDegrees()
        {
            var transformer = _status.VesselTransformer;
            return transformer ? transformer.YawScaler : 120f;
        }

        /// <summary>RT, as a person presses it: through the replicated press path when the hull binds one.</summary>
        void PressForm()
        {
            var handler = _status.ActionHandler;
            if (handler && ResolveFormInput(handler, out var ie))
            {
                handler.PerformShipControllerActionsReplicated(ie);
                handler.StopShipControllerActionsReplicated(ie);
                return;
            }
            Form();
        }

        /// <summary>Which input event RT's Form is bound to on THIS vessel, read off the binding maps and retried until it
        /// succeeds (they fill after executors initialize, vessel contract rule 6).</summary>
        bool ResolveFormInput(R_VesselActionHandler handler, out InputEvents ie)
        {
            if (_formInput.HasValue) { ie = _formInput.Value; return true; }
            foreach (InputEvents candidate in System.Enum.GetValues(typeof(InputEvents)))
            {
                _bindScratch.Clear();
                handler.CollectBoundActions(candidate, _bindScratch);
                foreach (var action in _bindScratch)
                    if (action is MeerkatWormholeActionSO w && w.Which == MeerkatWormholeActionSO.Trigger.Form)
                    {
                        _formInput = candidate;
                        _bindScratch.Clear();
                        ie = candidate;
                        return true;
                    }
            }
            _bindScratch.Clear();
            ie = default;
            return false;
        }

        // ------------------------------------------------------------------ the roots (§7)

        /// <summary>The page's <c>stepOmni</c>: take an omnicrystal the hull flies through; the roots grow at once from here
        /// (round 17), or, taken inside a tunnel or the roots, the moment it is out (round 21). Simulating machine only.</summary>
        void StepOmni()
        {
            var hull = _status.Transform;
            if (!config.OmniOn || !hull) return;
            if (!RootsGrowHere())
            {
                _omniArmed = false;   // the lava lamp's autopilot: a crystal it took (or was armed with in freestyle) grows nothing
                return;
            }
            if (!_freestyleChecked)
            {
                // a scene with no course (the Toybox): the local pilot's Meerkat lays the omnicrystals ahead of itself
                _freestyleChecked = true;
                if (!MeerkatOmnicrystalField.Instance && _status.IsLocalUser && !_status.IsInitializedAsAI &&
                    !FindAnyObjectByType<GateRaceController>())
                    MeerkatOmnicrystalField.EnsureFreestyle(hull);
            }
            var field = MeerkatOmnicrystalField.Instance;
            if (field && field.TryTake(hull.position, out int index))
            {
                _omniArmed = true;
                _omniCrystal = index;
                _omniSeed = unchecked((uint)(GetInstanceID() * 977 + index * 131 + Mathf.FloorToInt(Time.time * 10f)));
            }
            if (_omniArmed && _ride == null && _flight == null) GrowRootsHere();
        }

        /// <summary>
        /// Perf (2026-10-29): the Menu_Main autopilot (the lava lamp behind the menu, <see cref="CrewStations.MenuLive"/> and
        /// flown by its AI) grows no root networks from omnicrystals unless <c>menuAutopilotRoots</c> is on. Each network is
        /// built in one frame and then drawn and steered every frame, a large share of the lava lamp's fps drop in the
        /// 2026-10-29 captures. A person flying the Meerkat in freestyle still grows them, and every match is unaffected.
        /// </summary>
        bool RootsGrowHere() => !(CrewStations.MenuLive && _status.AutoPilotEnabled && !config.MenuAutopilotRoots);

        /// <summary>The page's <c>rootsHere</c> + <c>labCommit</c>: the network grows from the hull, every way, sized by this
        /// hull's Mass (round 26: <c>networkReach</c>, read now and published, never the last tunnel); the hull is in its
        /// source. The last tunnel and the last network collapse now.</summary>
        void GrowRootsHere()
        {
            using var _ = s_mGrowRoots.Auto();
            _omniArmed = false;
            var hull = _status.Transform;
            CollapseLast();
            if (_roots != null) _roots.CollapseNow();
            float share = config.NetworkShare(_status);   // round 26: Mass sizes the network
            _roots = MeerkatRootNetwork.Grow(config, this, ++_rootSeq, _omniSeed, share, hull.position, hull.forward, hull.right, hull.up);
            _flight = MeerkatRootMath.Enter(_roots.Channels, 0, MeerkatRootNetwork.D(hull.position), Mathf.Max(1f, _status.Speed), _roots.Up);
            _rootAiPick = _rootAiChan = -1;
            if (_status.IsTranslationRestricted && _stance) _stance.EndStance();
            if (_relay) _relay.PublishRoots(_rootSeq, _omniSeed, share, hull.position, hull.forward, hull.right, hull.up, _omniCrystal);
            PlayOneShot(config.RootsEvent, hull.position);
            CSDebug.LogVerbose(CSLogChannel.BlackHole,
                $"[Meerkat] roots {_rootSeq}: {_roots.Channels.Count} channels, reach {share:F2} of the cell");
        }

        /// <summary>A remote peer's copy of the roots its owner grew (<see cref="MeerkatWormholeNetworkRelay"/>).</summary>
        internal void GrowRootsRemote(int seq, uint seed, float size, Vector3 a, Vector3 t0, Vector3 right, Vector3 up, int crystal)
        {
            if (!config || IsAuthority) return;
            CollapseLast();
            if (_roots != null) _roots.CollapseNow();
            _roots = MeerkatRootNetwork.Grow(config, this, seq, seed, size, a, t0, right, up);
            var field = MeerkatOmnicrystalField.Instance;
            if (field) field.MarkTaken(crystal);
        }

        /// <summary>A remote peer hears that its owner's rider came out of roots <paramref name="seq"/>.</summary>
        internal void RootsOutRemote(int seq)
        {
            if (IsAuthority || _roots == null || _roots.Seq != seq) return;
            _roots.RiderOut();
        }

        /// <summary>
        /// The page's <c>labTransit</c> (rounds 20-21), flown by <see cref="MeerkatVesselTransformer"/> in place of its own
        /// move: where the roots put the hull this frame, its facing, speed and heading. The left stick (the eased stick the
        /// hull steers by, Invert Y included) moves it round the root, RT pushes it faster; an AI's stick comes from
        /// <see cref="RootAutopilot"/>. False when the hull is not in its roots. <paramref name="cameOut"/> is true on the
        /// frame it leaves (its speed is then the speed it went in with × <c>rideExitMultiplier</c>).
        /// </summary>
        public bool StepRoots(float dt, out Vector3 position, out Quaternion rotation, out float speed, out Vector3 heading,
            out bool cameOut)
        {
            var hull = _status.Transform;
            position = hull ? hull.position : Vector3.zero;
            rotation = hull ? hull.rotation : Quaternion.identity;
            speed = 0f;
            heading = default;
            cameOut = false;
            var g = _flight;
            if (g == null) return false;
            if (_roots == null || !Contains(MeerkatRootNetwork.Live, _roots))
            {
                _flight = null;   // the roots went from under the hull (a despawn): fly on
                return false;
            }
            float side, rise, th;
            if (_status.AutoPilotEnabled) { side = _rootAiStick.x; rise = _rootAiStick.y; th = 1f; }
            else
            {
                var input = _status.InputStatus;
                var stick = input != null ? input.EasedLeftJoystickPosition : Vector2.zero;
                side = stick.x;
                rise = stick.y;   // the way the stick turns the nose outside (SingleStickVesselTransformer: + y is nose up)
                th = RtLive();
            }
            var res = MeerkatRootMath.Step(g, _roots.Channels, _roots.AliveFn, config.RootFlight(), th, side, rise, dt, out var nose);
            position = MeerkatRootNetwork.V(g.Pos);
            heading = MeerkatRootNetwork.V(nose);
            var up = g.Free ? MeerkatRootNetwork.V(g.Upv) : (hull ? hull.up : Vector3.up);
            rotation = Pose(heading, up, rotation);
            speed = (float)(g.Free ? System.Math.Sqrt(g.Vel.LengthSq) : g.VIn);
            if (res == MeerkatRootMath.FlightResult.Riding) return true;
            // out of a tip (on along it), or back out of the source (turned to face out)
            cameOut = true;
            speed = (float)g.VIn * Mathf.Max(0.05f, config.RideExitMultiplier);
            _flight = null;
            _roots.RiderOut();
            if (_relay) _relay.PublishRootsOut(_roots.Seq);
            return true;
        }

        static Quaternion Pose(Vector3 nose, Vector3 up, Quaternion fallback)
        {
            if (nose.sqrMagnitude < 1e-8f) return fallback;
            up -= nose * Vector3.Dot(up, nose);
            if (up.sqrMagnitude < 1e-8f) up = Vector3.Cross(nose, Mathf.Abs(nose.x) < 0.9f ? Vector3.right : Vector3.up);
            return Quaternion.LookRotation(nose, up);
        }

        static bool Contains(IReadOnlyList<MeerkatRootNetwork> list, MeerkatRootNetwork net)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == net) return true;
            return false;
        }

        /// <summary>This hull's networks dig, stay, collapse and fade (every peer runs the same life).</summary>
        void StepRootNetworks(float dt)
        {
            var live = MeerkatRootNetwork.Live;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var net = live[i];
                if (net.Maker != this) continue;
                if (net.Step(config.StaySeconds, config.CollapseSeconds, dt))
                {
                    MeerkatTreeView.PrepareSkins(net, config);   // no-op once built, or while the skins are off
                    continue;
                }
                if (_roots == net) { _roots = null; _flight = null; }
                net.Remove();
            }
        }

        /// <summary>The local pilot's roots and lamp, for <see cref="MeerkatTreeView"/>.</summary>
        void TrackLocalPilot()
        {
            if (_status.Player == null || !_status.IsLocalUser || _status.IsInitializedAsAI) return;
            s_localRoots = _flight != null && _flight.Free ? _roots : null;
            var hull = _status.Transform;
            if (hull) s_localHull = hull.position;
        }

        /// <summary>The page's AI in a tunnel (round 24, input only): RT held (<see cref="StepRide"/>), and the left stick pushing
        /// it toward the middle of the tube, as in the roots.</summary>
        void SlideAutopilot()
        {
            _slideAiStick = Vector2.zero;
            var g = _slide;
            if (g == null || !g.Free) return;
            MeerkatRootMath.AiStick(g, MeerkatRootMath.D3.Zero, config.RootMove, out double side, out double rise);
            _slideAiStick = new Vector2((float)side, (float)rise);
        }

        /// <summary>
        /// The page's AI in the roots (round 21, input only): RT held, and the left stick pushing it across the root toward
        /// the middle, or, at a fork, toward the side of the root that leads nearest its ring (a rookie sometimes picks
        /// wrong, by <c>aiNoise</c>).
        /// </summary>
        void RootAutopilot()
        {
            _rootAiStick = Vector2.zero;
            _aiShaping = false;
            var g = _flight;
            if (g == null || !g.Free || _roots == null) return;
            var net = _roots.Channels;
            var c = net[g.Chan];
            if (g.Chan != _rootAiChan) { _rootAiChan = g.Chan; _rootAiPick = -1; }
            var want = MeerkatRootMath.D3.Zero;
            var ai = _status.AIPilot;
            if (c.Kids.Count > 1 && ai)
            {
                if (_rootAiPick < 0)
                {
                    var target = MeerkatRootNetwork.D(ai.ObjectivePosition);
                    int bi = 0;
                    double best = double.PositiveInfinity;
                    for (int k = 0; k < c.Kids.Count; k++)
                    {
                        double d = MeerkatRootMath.Reach(net, c.Kids[k], target);
                        if (d < best) { best = d; bi = k; }
                    }
                    if (_aiRand.NextDouble() < 0.35 * MeerkatWormholeMath.Clamp01(config.AiNoise)) bi = (bi + 1) % c.Kids.Count;
                    _rootAiPick = c.Kids[bi];
                }
                var fp = MeerkatRootMath.ChanAt(c, c.L, out var ft);
                var q = MeerkatRootMath.ForkSide(net[_rootAiPick], fp, ft);
                if (q.LengthSq > 1e-6) want = q.Normalized * (0.55 * MeerkatRootMath.Radius(c, c.L, config.RootTip));
            }
            MeerkatRootMath.AiStick(g, want, config.RootMove, out double side, out double rise);
            _rootAiStick = new Vector2((float)side, (float)rise);
        }

        /// <summary>
        /// The page's <c>rootCam</c> (round 20): inside the roots the camera rides the root's centreline
        /// <see cref="RootCameraBack"/> u behind the hull (into the root behind it when that is behind), half the hull's way out
        /// to the wall, looking at the hull, so it is always inside the root. Only the local human pilot's camera. False when
        /// the hull is not in its roots.
        /// </summary>
        bool StepRootCamera()
        {
            var g = _flight;
            if (g == null || !g.Free || _roots == null)
            {
                ReleaseRootCamera();
                return false;
            }
            bool mine = _status.Player != null && _status.IsLocalUser && !_status.IsInitializedAsAI;
            var cam = mine && CameraManager.Instance != null ? CameraManager.Instance.GetActiveController() as CustomCameraController : null;
            var hull = _status.Transform;
            if (cam && (cam.PlacementAnchor.HasValue || !cam.FollowTarget ||
                        (cam.FollowTarget != hull && !cam.FollowTarget.IsChildOf(hull))))
                cam = null;   // a cinematic or end-of-match camera pointed elsewhere is not ours to move
            if (cam != _rootCam) ReleaseRootCamera();
            ReleaseRideCamera();
            _camTunnel = null;
            if (!cam) return true;
            _rootCam = cam;
            var net = _roots.Channels;
            var c = net[g.Chan];
            MeerkatRootMath.ChanAt(c, System.Math.Max(0, System.Math.Min(c.L, g.S)), out var t);
            double hs = g.H.Dot(t) < 0 ? -1 : 1, s = g.S - hs * RootCameraBack;
            MeerkatRootMath.D3 p;
            if (s < 0 && c.Parent >= 0) p = MeerkatRootMath.ChanAt(net[c.Parent], net[c.Parent].L + s, out _);
            else if (s > c.L && c.Kids.Count > 0)
            {
                int k = c.Kids.Contains(g.Prev) ? g.Prev : c.Kids[0];
                p = MeerkatRootMath.ChanAt(net[k], s - c.L, out _);
            }
            else p = MeerkatRootMath.ChanAt(c, s, out _);
            cam.SpectateBlendSeconds = 0f;
            cam.Spectate = new CustomCameraController.SpectateView
            {
                Position = MeerkatRootNetwork.V(p + g.Dr * 0.5), LookAt = hull.position, Up = hull.up,
            };
            return true;
        }

        void ReleaseRootCamera()
        {
            if (!_rootCam) { _rootCam = null; return; }
            _rootCam.Spectate = null;   // back to the follow camera, behind the hull at the root's mouth
            _rootCam = null;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>This pilot's domain colour, the one its tunnels and its bridge-look reticle take.</summary>
        public Color Colour => DomainColour();

        Color DomainColour()
        {
            var colorSet = gameData && gameData.ThemeManagerData ? gameData.ThemeManagerData.ColorSet : null;
            if (colorSet == null || _status?.Player == null) return new Color(0.31f, 0.85f, 1f);   // the page's 0x4fd8ff
            return colorSet.GetDomainSignalColor(_status.Player.Domain);
        }

        static void PlayOneShot(EventReference reference, Vector3 at)
        {
            if (reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, at);
        }

        // A pooled hull switched back on keeps drawing its pilot's reticle (Initialize may not run again).
        void OnEnable()
        {
            if (_status != null && !s_executors.Contains(this)) s_executors.Add(this);
            OmniCrystalImpactor.CollectedByVessel -= OnOmniCrystal;
            OmniCrystalImpactor.CollectedByVessel += OnOmniCrystal;
        }

        /// <summary>
        /// The game's own omni crystal counts as an omnicrystal (Yash, 2026-10-28: "I am collecting omnicrystals in lava lamp,
        /// yet I don't see the network wormholes getting created"): the cell's omni crystal, the freestyle conveyor's, any
        /// mode's, collected by this hull on its simulating machine, arms the roots exactly as a Meerkat omnicrystal does
        /// (<see cref="StepOmni"/> grows them at once, or the moment the hull is out of a tunnel). For the player and the AI.
        /// </summary>
        void OnOmniCrystal(IVesselStatus collector, Vector3 at)
        {
            if (!config || !config.OmniOn || collector != _status || !IsAuthority || !RootsGrowHere()) return;
            _omniArmed = true;
            _omniCrystal = -1;   // not one of the Meerkat field's (the relay's crystal -1 marks none taken)
            _omniSeed = unchecked((uint)(GetInstanceID() * 977 + Mathf.FloorToInt(at.x * 7f + at.y * 13f + at.z * 17f) +
                                         Mathf.FloorToInt(Time.time * 10f)));
        }

        // A vessel swap or despawn: this hull's tunnels go with it, and a ride ends where it is.
        void OnDisable()
        {
            OmniCrystalImpactor.CollectedByVessel -= OnOmniCrystal;
            for (int i = s_tunnels.Count - 1; i >= 0; i--)
                if (s_tunnels[i].Maker == this) s_tunnels.RemoveAt(i);
            s_executors.Remove(this);
            ReleaseSpeedTunnelScale();
            ReleaseRideCamera();
            ReleaseRootCamera();
            _swallow.Release(this);
            var nets = MeerkatRootNetwork.Live;
            for (int i = nets.Count - 1; i >= 0; i--)
                if (nets[i].Maker == this) nets[i].Remove();
            if (s_localRoots == _roots) s_localRoots = null;
            _roots = null;
            _flight = null;
            _omniArmed = _rtHeld = false;
            _camTunnel = null;
            _ride = null;
            _slide = null;
            _slideNet = null;
            _mine = null;
            _lastRide = null;
            _retLive = false;
            _ltHeld = false;
            _formInput = null;
            _aiShaping = _aiPlanned = false;
            _ridePiloted = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            s_tunnels.Clear();
            s_ripples.Clear();
            s_executors.Clear();
            s_localRoots = null;
        }
    }
}
