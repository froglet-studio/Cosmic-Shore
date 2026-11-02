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
    /// The Meerkat's one ability, the SOLITON TUNNEL (<c>R_VesselActions/MEERKAT.md</c>), cloned from the Vessel Studio
    /// (<c>Docs/Studios/VesselStudio/meerkat.html</c>: <c>gateHold</c>, <c>gateForm</c>, <c>gateCommit</c>,
    /// <c>stepTunnel</c>, <c>gateTransit</c>, <c>gateOut</c>, <c>aiInput</c>), its arithmetic in
    /// <see cref="SolitonTunnelShapeMath"/>.
    ///
    /// <list type="bullet">
    /// <item><b>The reticle</b> rides <c>reticleAhead</c> ahead of the nose: the tunnel RT would form now. The right stick
    /// bends it any way (eased, as the page's <c>TX</c>/<c>TY</c>); LT shortens a straighter one. Out of a tunnel it comes
    /// back after <c>reticleDelaySeconds</c>, rippling out of its centre over <c>reticleGrowSeconds</c>. Only its pilot
    /// sees it (<see cref="SolitonTunnelView"/>).</item>
    /// <item><b>RT</b> (<see cref="Form"/>, once the reticle is 60% grown) forms it: it ripples out of its centre over
    /// <c>formSeconds</c> (never longer than the dive to it), and the hull dives straight in. The last tunnel collapses
    /// at once.</item>
    /// <item><b>The slide</b> (<see cref="StepRide"/>, flown by <see cref="MeerkatVesselTransformer"/>; Yash, 2026-10-29:
    /// "a slip and slide, gravity toward the inner surface, momentum decides where you come out, like a slingshot"): the
    /// tunnel (a <see cref="SolitonTunnel"/>, an environmental fundamental) is slid by a <see cref="SolitonTunnelRider"/>:
    /// radial gravity presses the hull to the inner wall, it slides and now and then bounces, RT gives a small push
    /// (<see cref="SolitonTunnelConfigSO.Thrust"/>), and nothing steers it; it comes out of the far mouth (or back out of
    /// the way in) with the velocity it had × the config's <c>exitMultiplier</c>, the way it was going.</item>
    /// <item><b>Its life</b>: it stays <c>staySeconds</c> once its rider is out, collapses to its centre over
    /// <c>collapseSeconds</c> and fades over
    /// <see cref="SolitonTunnelShapeMath.FadeSeconds"/>. Everyone sees it (<see cref="Live"/>: the bridge, the bent
    /// space).</item>
    /// <item><b>The AI</b> (input only, /vessel-ai): once its reticle is back and its ring is far enough, it plans a
    /// tunnel (<see cref="SolitonTunnelShapeMath.TryPlan"/>), holds its line while it bends the reticle and squeezes LT for
    /// <c>aiShapeSeconds</c>, then presses RT through the REPLICATED press path, as a person's would be.</item>
    /// </list>
    ///
    /// <para><b>The Omnicrystal ability, the Soliton Network</b> (<c>MEERKAT.md</c> §7): fly through an omnicrystal
    /// (<see cref="MeerkatOmnicrystalField"/>, or the game's omni crystal) and a Soliton Network grows at once from the hull,
    /// every way, sized by its Mass (<see cref="SolitonNetwork.Grow(SolitonTunnelConfigSO, ISolitonTunnelMaker, int, uint, float, Vector3, Vector3, Vector3, Vector3)"/>,
    /// the config's <c>networkConfig</c>); taken inside a tunnel, it grows the moment the hull is out. Inside, the hull
    /// slides it the same way (<see cref="StepNetwork"/>) and goes down whichever channel it slides into; the camera rides
    /// the channel's centreline behind it. The AI holds RT. The owner publishes each network it grows (its seed and frame)
    /// and its rider's way out; every peer grows the same network from them.</para>
    ///
    /// <para><b>Where it runs.</b> The reticle, the ride and the AI run on the simulating machine only
    /// (<see cref="MantaStingActionExecutor.IsSimAuthority"/>). Every peer draws every tunnel: the owner publishes each
    /// one it forms, and its rider's way out, through <see cref="MeerkatSolitonNetworkRelay"/>; every peer runs the
    /// same life from there. The hull's pose replicates as every vessel's does.</para>
    /// </summary>
    public sealed class MeerkatSolitonExecutor : ShipActionExecutorBase, ISolitonTunnelMaker
    {
        [Header("Config")]
        [Tooltip("The Meerkat's Soliton numbers (its reticle, kit, AI); the tunnel's and the network's own are in their SolitonTunnelConfigSO.")]
        [SerializeField] MeerkatSolitonConfigSO config;

        [Tooltip("The game data the domain palette is read from (a tunnel and its bridge take their maker's domain colour).")]
        [SerializeField] GameDataSO gameData;

        /// <summary>The reticle as its pilot sees it (the page's <c>ftLive.ret</c>, or the fading <c>retGhost</c>).</summary>
        public struct Reticle
        {
            public Vector3 A, T0, Side;
            public SolitonTunnelShapeMath.Shape Shape;
            public SolitonTunnelShapeMath.Conic Path;   // its centreline (round 22)
            public float S0, S1, Alpha;
            public bool Ghost;
        }

        static readonly List<MeerkatSolitonExecutor> s_executors = new();

        // Per-step markers (perf, 2026-10-29: the Lavalamp captures showed 3-8 ms of Update self time with no breakdown).
        // They show up by name in the profiler and in PerfAutoCapture's prof_*.json.
        static readonly ProfilerMarker s_mTunnels = new("Meerkat.Executor.StepTunnels");
        static readonly ProfilerMarker s_mSwallow = new("Meerkat.Swallow.Step");
        static readonly ProfilerMarker s_mNetworks = new("Meerkat.Executor.StepNetworks");
        static readonly ProfilerMarker s_mCamera = new("Meerkat.Executor.StepCamera");
        static readonly ProfilerMarker s_mAutopilot = new("Meerkat.Executor.Autopilot");
        static readonly ProfilerMarker s_mOmni = new("Meerkat.Executor.StepOmni");
        static readonly ProfilerMarker s_mGrowNetwork = new("Meerkat.Executor.GrowNetwork");
        static readonly ProfilerMarker s_mRideSoliton = new("Meerkat.Executor.TryRideAnySoliton");
        static readonly ProfilerMarker s_mReticle = new("Meerkat.Executor.StepReticle");

        /// <summary>Every tunnel alive now, everyone's (made or placed): <see cref="SolitonTunnel.Live"/>.</summary>
        public static IReadOnlyList<SolitonTunnel> Live => SolitonTunnel.Live;
        /// <summary>Every enabled Meerkat (the view draws its pilot's reticle).</summary>
        public static IReadOnlyList<MeerkatSolitonExecutor> Executors => s_executors;

        IVesselStatus _status;
        MeerkatSolitonNetworkRelay _relay;
        ToggleTranslationModeActionExecutor _stance;
        MeerkatSolitonKit _kit;   // [Garrett's kit hook] the soliton kit beside this executor (R_VesselActions/MEERKAT.md section 10)
        SolitonTunnel _lastRide;         // [Garrett's kit hook] the soliton last ridden, and when (not re-entered for RideAgainSeconds)
        float _lastRideAt = -10f;

        // the reticle (the simulating machine)
        bool _retLive;
        float _retAge;
        float _retEmerge;
        Vector3 _retA, _retT0, _retSide = Vector3.right;
        SolitonTunnelShapeMath.Shape _retShape;
        readonly SolitonTunnelShapeMath.Conic _retPath = new(), _ghostPath = new();   // the reticle's centreline, and the fading one's
        Vector2 _stick;   // the right stick the reticle reads (the page's r.sx / r.sy, from the frame before)
        Vector2 _eased;   // ...the reticle easing toward it over reticleSmoothSeconds (the page's r.fx / r.fy, round 3)
        float _easedLq;   // ...and toward LT's squeeze (r.flq)
        bool _ltHeld;
        float _ltHeldFor;
        // the reticle fading as a tunnel forms (the page's retGhost)
        Reticle _ghost;
        float _ghostAlpha, _ghostFormT;

        // the ride
        SolitonTunnel _mine;
        SolitonTunnel _ride;
        CustomCameraController _rideCam;   // the camera this hull's ride is holding on the centreline (round 8)
        SolitonTunnel _camTunnel;                 // the tunnel the ride camera follows, through the ride and just past its way out
        float _rideCamBlendSeconds;        // the camera's own SpectateBlendSeconds, put back when the ride lets it go
        bool _ridePiloted;                 // the ride was formed by the pilot, not by the autopilot (the lava lamp's AI)
        bool _wasAutopilot;                // last frame's AutoPilotEnabled, to see the hand-over
        float _camS;                       // how far along that tunnel's line the hull is (past L after the way out)
        float _rideS;
        // the slide (2026-10-29, the slip-and-slide): the tunnel as a network of one, slid by a vessel-agnostic rider
        readonly SolitonTunnelRider _slider = new();
        int _seq;
        Transform _tunnelScaleKey;   // the vessel holding the speed tunnel's ride scale (VesselSpeedTunnel.SetEffectScale)

        // the AI
        float _aiHold, _aiL, _aiM, _aiLt;
        bool _aiShaping, _aiPlanned;
        Vector2 _aiStick;
        System.Random _aiRand;

        // the Soliton Network (§7): the network this hull grew last, the rider sliding a network, the omnicrystal it holds
        const float NetworkCameraBack = 12f;   // the page's CAM_ROOT_BACK: close behind you, so the camera stays inside the channel
        SolitonNetwork _network;
        readonly SolitonTunnelRider _netRider = new();
        CustomCameraController _networkCam;
        int _networkSeq, _omniCrystal = -1;
        uint _omniSeed;
        bool _omniArmed, _rtHeld, _freestyleChecked;
        static SolitonNetwork s_localNetwork;
        static Vector3 s_localHull;
        InputEvents? _formInput;
        // the soliton's swallow (round 23): the prisms and crystals its tunnels pull in, carry through and fire out
        readonly MeerkatSwallow _swallow = new();
        readonly List<ShipActionSO> _bindScratch = new();

        public MeerkatSolitonConfigSO Config => config;
        /// <summary>The hull this Meerkat flies.</summary>
        public IVesselStatus Status => _status;
        /// <summary>[Garrett's kit hook] the soliton kit beside this executor, or null.</summary>
        internal MeerkatSolitonKit Kit => _kit;
        /// <summary>The prisms this Meerkat's tunnels are moving now (pulled, carried or fired; round 23).</summary>
        public int SwallowedPrisms => _swallow.PrismCount;
        /// <summary>...and the crystals.</summary>
        public int SwallowedCrystals => _swallow.CrystalCount;
        /// <summary>The tunnel this hull is sliding now, or null (round 25: what it carries keeps behind the rider).</summary>
        internal SolitonTunnel RidingTunnel => _ride;
        /// <summary>How far along it the rider is (u; below 0 while it dives to the mouth).</summary>
        internal float RideAlong => _rideS;
        /// <summary>True while a tunnel flies this hull (from the press to its way out).</summary>
        public bool IsRiding => _ride != null;
        /// <summary>The way out's speed over the speed inside (the tunnel config's exitMultiplier, with the kit's Boost out, as
        /// <see cref="StepRide"/> applies it).</summary>
        public float RideExitMultiplier => config ? Mathf.Max(0.05f, RideExit(config.RideExitMultiplier)) : 1f;
        /// <summary>The reticle's growth, 0..1 (RT forms from <see cref="SolitonTunnelShapeMath.FormableEmerge"/>); 0 riding.</summary>
        public float Ready01 => _retLive ? _retEmerge : 0f;
        /// <summary>True while RT would form a tunnel.</summary>
        public bool CanForm => _retLive && _retEmerge >= SolitonTunnelShapeMath.FormableEmerge;
        /// <summary>How far through its ride the hull is, 0..1 (0 when not riding).</summary>
        public float Ride01 => _ride != null ? SolitonTunnelShapeMath.Clamp01(_rideS / Mathf.Max(1e-3f, _ride.L)) : 0f;
        /// <summary>The AI is shaping its reticle: it holds its line (the page's left stick at rest while it bends).</summary>
        public bool AIHoldsLine => _aiShaping;
        /// <summary>Tunnels this hull has formed since it spawned.</summary>
        public int Formed => _seq;
        /// <summary>True while this hull slides a Soliton Network (from the omnicrystal, or a placed network's source, to its way out).</summary>
        public bool IsInNetwork => _netRider.Riding;
        /// <summary>The network the local pilot is inside, or null (the flight and the camera read it).</summary>
        public static SolitonNetwork LocalNetwork => s_localNetwork;
        /// <summary>The local pilot's hull (the lamp that lights the network's walls).</summary>
        public static Vector3 LocalHullPosition => s_localHull;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _relay = GetComponentInParent<MeerkatSolitonNetworkRelay>();
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
            _slider.Clear();
            _netRider.Clear();
            _omniArmed = _rtHeld = _freestyleChecked = false;
            _omniCrystal = -1;
            if (!s_executors.Contains(this)) s_executors.Add(this);
            if (config == null)
                CSDebug.LogError($"[Meerkat] {name}: MeerkatSolitonExecutor has no MeerkatSolitonConfig - RT will form nothing. " +
                                 "Wire Assets/_SO_Assets/VesselActions/Meerkat/MeerkatSolitonConfig.asset on the prefab.");
        }

        bool IsAuthority => _status != null && MantaStingActionExecutor.IsSimAuthority(_status);

        // ------------------------------------------------------------------ the triggers

        /// <summary>RT: form the tunnel the reticle shows and dive in (the page's <c>gateForm</c>).</summary>
        public void Form()
        {
            _rtHeld = true;   // inside a tube RT gives the slide its small push while held
            if (!config || !IsAuthority) return;
            if (!_retLive || _retEmerge < SolitonTunnelShapeMath.FormableEmerge) return;   // still rippling into shape
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
            if (_status.AutoPilotEnabled) return 1f;   // the AI holds RT inside a tube
            if (!_rtHeld) return 0f;
            var input = _status.InputStatus;
            bool analog = input != null && input.ActiveInputDevice == InputDeviceType.Gamepad;
            return analog ? SolitonTunnelShapeMath.Clamp01(input.RightTriggerAnalog) : 1f;
        }

        float LtLive()
        {
            if (_status.AutoPilotEnabled) return _aiLt;
            if (!_ltHeld) return 0f;
            var input = _status.InputStatus;
            bool analog = input != null && input.ActiveInputDevice == InputDeviceType.Gamepad;
            return analog
                ? SolitonTunnelShapeMath.Clamp01(input.LeftTriggerAnalog)
                : SolitonTunnelShapeMath.Clamp01(_ltHeldFor / Mathf.Max(0.05f, config.KeyRampSeconds));
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (!config || _status == null) return;
            float dt = Time.deltaTime;
            using (s_mTunnels.Auto()) StepTunnels(dt);
            using (s_mSwallow.Auto()) _swallow.Step(this, dt);
            using (s_mNetworks.Auto()) StepNetworks(dt);
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

        void LateUpdate() => SolitonTunnelView.DrawOncePerFrame();

        /// <summary>The page's <c>gateHold</c> reticle: on the nose, bent by last frame's stick, shortened by LT.</summary>
        void StepReticle(float dt)
        {
            var hull = _status.Transform;
            bool live = hull && _ride == null && !_netRider.Riding && !_status.IsStationary;
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
            _retEmerge = SolitonTunnelShapeMath.ReticleEmerge(_retAge, config.ReticleDelaySeconds, config.ReticleGrowSeconds);
            Vector3 fwd = hull.forward, right = hull.right, up = hull.up;
            _retA = hull.position + fwd * Mathf.Max(0f, config.ReticleAhead);
            _retT0 = fwd;
            // round 3 (Yash: "a little smoothy, so that it feels good"): the reticle eases toward what the sticks ask for
            float ke = SolitonTunnelShapeMath.ReticleEase(dt, config.ReticleSmoothSeconds);
            _eased += (_stick - _eased) * ke;
            _easedLq += (LtLive() - _easedLq) * ke;
            var sh = SolitonTunnelShapeMath.ShapeFor(config.Shape(SpaceLevel()), _easedLq, _eased.x, _eased.y);   // round 23: Space sets its area
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
            var t = AddTunnel(_retA, _retT0, _retSide, _retShape, SolitonTunnelShapeMath.FormTime(config.FormSeconds, dive), ++_seq);
            _ghostPath.Set(_retShape.L, _retShape.Theta, _retShape.Oval);
            _ghost = new Reticle { A = _retA, T0 = _retT0, Side = _retSide, Shape = _retShape, Path = _ghostPath, S0 = 0f, S1 = _retShape.L, Ghost = true };
            _ghostAlpha = 1f;
            _ghostFormT = t.FormT;
            // the ride starts at the hull: it dives straight down to the mouth the reticle showed (s < 0), then into the throat
            _ride = t;
            _slider.Enter(t, hull.position, vIn, hull.up);
            _rideS = _slider.Along;
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

        SolitonTunnel AddTunnel(Vector3 a, Vector3 t0, Vector3 side, in SolitonTunnelShapeMath.Shape sh, float formT, int seq)
        {
            var t = SolitonTunnel.Make(config.TunnelConfig, this, DomainColour(), seq, a, t0, side, sh, formT);
            _mine = t;
            SolitonTunnel.Add(t);
            return t;
        }

        /// <summary>A remote peer's copy of a tunnel its owner formed (<see cref="MeerkatSolitonNetworkRelay"/>).</summary>
        internal void FormRemote(int seq, Vector3 a, Vector3 t0, Vector3 side, float L, float shaft, float bendRadius, float b,
            float m, float theta, float formT, byte kit, float portalSize)
        {
            if (!config || IsAuthority) return;
            CollapseLast();
            var t = AddTunnel(a, t0, side, new SolitonTunnelShapeMath.Shape { L = L, S = shaft, R = bendRadius, B = b, M = m, Theta = theta,
                    Oval = SolitonTunnelShapeMath.OvalOf(config.Shape(), L, theta) },   // the same config, so the same arc as its owner's
                formT, seq);
            t.Kit = kit;   // [Garrett's kit hook] what its owner stamped on it
            t.PortalSize = portalSize;
            if (_kit) _kit.OnFormed(t);
        }

        /// <summary>A new tunnel (or roots) collapses the last one at once, unless a sentinel holds it ([Garrett's kit hook]:
        /// a soliton with a sentinel stands as long as its sentinel).</summary>
        void CollapseLast()
        {
            _mine?.CollapseNow();
        }

        /// <summary>
        /// [Garrett's kit hook] The page's <c>kitTryRide</c>: anyone rides any soliton, its maker or anyone else, and any
        /// Soliton Tunnel placed in the scene (an environmental fundamental, <see cref="SolitonTunnelPlacer"/>). A Meerkat
        /// that flies into a standing tunnel's way in is slid through it from the speed it went in with (and its own Time's
        /// rush). A placed Soliton Network (no maker) is ridden the same way from its source. The tube is untouched by riders
        /// that did not make it.
        /// </summary>
        void TryRideAnySoliton()
        {
            var hull = _status.Transform;
            if (_ride != null || _netRider.Riding || !hull || _status.IsStationary) return;
            var live = SolitonTunnel.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var t = live[i];
                if (!MeerkatSwallow.Standing(t)) continue;
                if (t == _lastRide && Time.time - _lastRideAt < MeerkatSolitonMath.RideAgainSeconds) continue;
                if (!SolitonTunnelRider.AtWayIn(t, hull.position, hull.forward)) continue;
                // a rider enters someone's soliton the same way its maker does (SolitonTunnelRider.Enter)
                _ride = t;
                _slider.Enter(t, hull.position, Mathf.Max(1f, _status.Speed), hull.up);
                _rideS = _slider.Along;   // entered where the hull meets the way in
                _retLive = false;
                _retAge = 0f;
                _aiPlanned = false;
                _lastRide = t;
                _lastRideAt = Time.time;
                return;
            }
            var nets = SolitonNetwork.Live;
            for (int i = 0; i < nets.Count; i++)
            {
                var n = nets[i];
                if (n.Maker != null || !SolitonTunnelRider.AtSource(n, hull.position, hull.forward)) continue;   // placed networks only
                _netRider.Enter(n, 0, hull.position, Mathf.Max(1f, _status.Speed));
                _retLive = false;
                _retAge = 0f;
                _aiPlanned = false;
                return;
            }
        }

        /// <summary>A remote peer hears that its owner's rider came out of tunnel <paramref name="seq"/>.</summary>
        internal void RiderOutRemote(int seq)
        {
            if (IsAuthority || _mine == null || _mine.Seq != seq) return;
            _mine.RiderOut();
        }

        /// <summary>
        /// The SLIP-AND-SLIDE through a tunnel (Yash, 2026-10-29), flown by <see cref="MeerkatVesselTransformer"/> in place of
        /// its own move: where the tunnel puts the hull this frame, its facing, its speed and its heading. Radial gravity
        /// presses it to the inner wall, it slides and now and then bounces, RT gives a small push
        /// (<see cref="SolitonTunnelConfigSO.Thrust"/>); there is no steering inside. False when the hull is not riding.
        /// <paramref name="cameOut"/> is true on the frame it leaves: its speed and heading are then the SLINGSHOT, the velocity
        /// it had × the exit multiplier (the kit's Boost out included), the way it was going.
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
            if (t == null || !_slider.Riding) return false;
            if (!_slider.TubeStillLive)
            {
                _ride = null;   // the tunnel went from under the hull (a despawn): fly on
                _slider.Clear();
                return false;
            }
            // [Garrett's kit hook] Time: the rush scales the slide's speeds and push; the L5 Boost out its exit (every soliton)
            var flight = _slider.Settings(Rush);
            flight.ExitMultiplier = RideExit((float)flight.ExitMultiplier);
            var hull = _status.Transform;
            var res = _slider.Step(flight, RtLive(), dt, current, hull ? hull.up : Vector3.up, out position, out rotation,
                out speed, out heading, out _);
            _rideS = _slider.Along;
            if (res == SolitonTunnelMath.FlightResult.Riding) return true;
            // out of the far mouth, or back out of the way in: the slingshot
            cameOut = true;
            _ride = null;
            _slider.Clear();
            _lastRide = t;
            _lastRideAt = Time.time;
            if (ReferenceEquals(t.Maker, this))   // [Garrett's kit hook] a rider of someone else's soliton leaves it untouched
            {
                t.RiderOut();
                if (_relay) _relay.PublishRiderOut(t.Seq);
            }
            return true;
        }

        // [Garrett's kit hook] Time: the rush scales the slide's speeds and push; its exit takes the L5 Boost out (every soliton)
        float Rush => _kit ? _kit.RideMid(1f) : 1f;
        float RideExit(float exit) => _kit ? _kit.RideExit(exit) : exit;

        /// <summary>This hull's tunnels form, stay, collapse and fade (every peer runs the same life).</summary>
        void StepTunnels(float dt)
        {
            var live = SolitonTunnel.Live;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var t = live[i];
                if (!ReferenceEquals(t.Maker, this)) continue;
                if (!t.Step(dt, out bool collapsed)) { SolitonTunnel.Remove(t); continue; }
                if (collapsed) PlayOneShot(config.CollapseEvent, t.At(t.W.S0, out _));
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
            float rest = SolitonTunnelShapeMath.Clamp01(config.ReticleIdleAlpha), e = _retEmerge;
            float alpha = (rest + (1f - rest) * SolitonTunnelShapeMath.Clamp01(Mathf.Max(_retShape.Stick / 0.3f, _easedLq * 2f))) *
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
            if (StepNetworkCamera()) return;
            float dt = Time.deltaTime;
            // only a ride the PILOT formed: the autopilot's (the lava lamp's AI) never moves anyone's camera, and a ride it
            // was in the middle of when the pilot clicked to fly is left to the follow camera the menu just handed over to
            if (_ride != null) { _camTunnel = _ridePiloted ? _ride : null; _camS = _rideS; }
            else if (_camTunnel != null) _camS += Mathf.Max(0f, _status.Speed) * dt;
            var t = _camTunnel;
            bool mine = t != null && Contains(SolitonTunnel.Live, t) && _status.Player != null && _status.IsLocalUser && !_status.IsInitializedAsAI
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
            if (_netRider.Riding) return;   // inside a network the AI holds RT (RtLive) and slides: no steering inside
            if (!config.AiWormhole || !ai || !hull) return;
            if (_ride != null)
            {
                _aiHold = 0f;
                _aiPlanned = false;   // in a tunnel it holds RT and slides
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
                if (_aiHold <= 0f && (SolitonTunnelShapeMath.ReticleSettled(_stick, _eased, _aiL, _easedLq) ||
                                      _aiHold <= -SolitonTunnelShapeMath.AiSettleTimeout))
                {
                    _aiPlanned = false;
                    PressForm();
                }
            }
            else if (d > Mathf.Min(config.AiStraightMinDistance, config.AiBentMinDistance) && CanForm &&
                     SolitonTunnelShapeMath.TryPlan(config.Plan(TurnRateDegrees(), SpaceLevel()), local, Mathf.Max(1f, _status.Speed),
                         SolitonTunnelShapeMath.Clamp01(config.AiNoise), (float)_aiRand.NextDouble(),
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
            _aiStick = SolitonTunnelShapeMath.PlanStick(local, _aiM);
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
                    if (action is MeerkatSolitonActionSO w && w.Which == MeerkatSolitonActionSO.Trigger.Form)
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

        // ------------------------------------------------------------------ the Soliton Network (§7)

        /// <summary>The page's <c>stepOmni</c>: take an omnicrystal the hull flies through; a Soliton Network grows at once
        /// from here (round 17), or, taken inside a tunnel or a network, the moment it is out (round 21). Simulating machine
        /// only.</summary>
        void StepOmni()
        {
            var hull = _status.Transform;
            if (!config.OmniOn || !hull) return;
            if (!NetworkGrowsHere())
            {
                _omniArmed = false;   // the menu's autopilot: a crystal it took (or was armed with in freestyle) grows nothing
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
            if (_omniArmed && _ride == null && !_netRider.Riding) GrowNetworkHere();
        }

        /// <summary>
        /// Perf (2026-10-29): the Menu_Main autopilot (<see cref="CrewStations.MenuLive"/>, flown by its AI) grows no Soliton
        /// Networks from omnicrystals unless <c>menuAutopilotNetworks</c> is on. Each network is built in one frame and then
        /// drawn every frame, a large share of the menu's fps drop in the 2026-10-29 captures. A person flying the Meerkat in
        /// freestyle still grows them, and every match is unaffected.
        /// </summary>
        bool NetworkGrowsHere() => !(CrewStations.MenuLive && _status.AutoPilotEnabled && !config.MenuAutopilotNetworks);

        /// <summary>The page's <c>rootsHere</c> + <c>labCommit</c>: a Soliton Network (the config's <c>networkConfig</c>)
        /// grows from the hull, every way, sized by this hull's Mass (round 26: <c>networkReach</c>, read now and published);
        /// the hull is in its source. The last tunnel and the last network collapse now.</summary>
        void GrowNetworkHere()
        {
            using var _ = s_mGrowNetwork.Auto();
            _omniArmed = false;
            var hull = _status.Transform;
            CollapseLast();
            if (_network != null) _network.CollapseNow();
            float share = config.NetworkShare(_status);   // round 26: Mass sizes the network
            _network = SolitonNetwork.Grow(config.NetworkConfig, this, ++_networkSeq, _omniSeed, share, hull.position, hull.forward, hull.right, hull.up);
            _netRider.Enter(_network, 0, hull.position, Mathf.Max(1f, _status.Speed));
            if (_status.IsTranslationRestricted && _stance) _stance.EndStance();
            if (_relay) _relay.PublishNetwork(_networkSeq, _omniSeed, share, hull.position, hull.forward, hull.right, hull.up, _omniCrystal);
            PlayOneShot(config.NetworkEvent, hull.position);
            CSDebug.LogVerbose(CSLogChannel.BlackHole,
                $"[Meerkat] Soliton Network {_networkSeq}: {_network.Channels.Count} channels, reach {share:F2} of the cell");
        }

        /// <summary>A remote peer's copy of the network its owner grew (<see cref="MeerkatSolitonNetworkRelay"/>).</summary>
        internal void GrowNetworkRemote(int seq, uint seed, float size, Vector3 a, Vector3 t0, Vector3 right, Vector3 up, int crystal)
        {
            if (!config || IsAuthority) return;
            CollapseLast();
            if (_network != null) _network.CollapseNow();
            _network = SolitonNetwork.Grow(config.NetworkConfig, this, seq, seed, size, a, t0, right, up);
            var field = MeerkatOmnicrystalField.Instance;
            if (field) field.MarkTaken(crystal);
        }

        /// <summary>A remote peer hears that its owner's rider came out of network <paramref name="seq"/>.</summary>
        internal void NetworkOutRemote(int seq)
        {
            if (IsAuthority || _network == null || _network.Seq != seq) return;
            _network.RiderOut();
        }

        /// <summary>
        /// The SLIP-AND-SLIDE through a Soliton Network (rounds 20-21, the slide since 2026-10-29), flown by
        /// <see cref="MeerkatVesselTransformer"/> in place of its own move: where the network puts the hull this frame, its
        /// facing, speed and heading. Radial gravity to the wall, a bounce now and then, RT's small push, no steering; at a
        /// fork it goes down whichever channel it slides into. False when the hull is not in a network.
        /// <paramref name="cameOut"/> is true on the frame it leaves: its speed and heading are then the slingshot.
        /// </summary>
        public bool StepNetwork(float dt, out Vector3 position, out Quaternion rotation, out float speed, out Vector3 heading,
            out bool cameOut)
        {
            var hull = _status.Transform;
            position = hull ? hull.position : Vector3.zero;
            rotation = hull ? hull.rotation : Quaternion.identity;
            speed = 0f;
            heading = default;
            cameOut = false;
            if (!_netRider.Riding) return false;
            var net = _netRider.Network;
            if (!_netRider.TubeStillLive)
            {
                _netRider.Clear();   // the network went from under the hull (a despawn): fly on
                return false;
            }
            var flight = _netRider.Settings(Rush);
            flight.ExitMultiplier = RideExit((float)flight.ExitMultiplier);
            var res = _netRider.Step(flight, RtLive(), dt, rotation, hull ? hull.up : Vector3.up, out position, out rotation,
                out speed, out heading, out _);
            if (res == SolitonTunnelMath.FlightResult.Riding) return true;
            // out of a tip, or back out of the source: the slingshot
            cameOut = true;
            _netRider.Clear();
            if (ReferenceEquals(net.Maker, this))
            {
                net.RiderOut();
                if (_relay) _relay.PublishNetworkOut(net.Seq);
            }
            return true;
        }

        static bool Contains<T>(IReadOnlyList<T> list, T item) where T : class
        {
            for (int i = 0; i < list.Count; i++) if (ReferenceEquals(list[i], item)) return true;
            return false;
        }

        /// <summary>This hull's networks grow, stay, collapse and fade (every peer runs the same life).</summary>
        void StepNetworks(float dt)
        {
            var live = SolitonNetwork.Live;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var net = live[i];
                if (!ReferenceEquals(net.Maker, this)) continue;
                if (net.Step(dt)) continue;
                if (_network == net) _network = null;
                if (_netRider.Network == net) _netRider.Clear();
                net.Remove();
            }
        }

        /// <summary>The local pilot's network (<see cref="LocalNetwork"/>).</summary>
        void TrackLocalPilot()
        {
            if (_status.Player == null || !_status.IsLocalUser || _status.IsInitializedAsAI) return;
            var g = _netRider.Flight;
            s_localNetwork = g != null && g.Free ? _netRider.Network : null;
            var hull = _status.Transform;
            if (hull) s_localHull = hull.position;
        }

        /// <summary>
        /// The page's <c>rootCam</c> (round 20): inside a network the camera rides the channel's centreline
        /// <see cref="NetworkCameraBack"/> u behind the hull (into the channel behind it when that is behind), half the hull's
        /// way out to the wall, looking at the hull, so it is always inside. Only the local human pilot's camera. False when
        /// the hull is not in a network.
        /// </summary>
        bool StepNetworkCamera()
        {
            var g = _netRider.Flight;
            var network = _netRider.Network;
            if (g == null || !g.Free || network == null)
            {
                ReleaseNetworkCamera();
                return false;
            }
            bool mine = _status.Player != null && _status.IsLocalUser && !_status.IsInitializedAsAI;
            var cam = mine && CameraManager.Instance != null ? CameraManager.Instance.GetActiveController() as CustomCameraController : null;
            var hull = _status.Transform;
            if (cam && (cam.PlacementAnchor.HasValue || !cam.FollowTarget ||
                        (cam.FollowTarget != hull && !cam.FollowTarget.IsChildOf(hull))))
                cam = null;   // a cinematic or end-of-match camera pointed elsewhere is not ours to move
            if (cam != _networkCam) ReleaseNetworkCamera();
            ReleaseRideCamera();
            _camTunnel = null;
            if (!cam) return true;
            _networkCam = cam;
            var net = network.Channels;
            var c = net[g.Chan];
            SolitonTunnelMath.ChanAt(c, System.Math.Max(0, System.Math.Min(c.L, g.S)), out var t);
            double hs = g.H.Dot(t) < 0 ? -1 : 1, s = g.S - hs * NetworkCameraBack;
            SolitonTunnelMath.D3 p;
            if (s < 0 && c.Parent >= 0) p = SolitonTunnelMath.ChanAt(net[c.Parent], net[c.Parent].L + s, out _);
            else if (s > c.L && c.Kids.Count > 0)
            {
                int k = c.Kids.Contains(g.Prev) ? g.Prev : c.Kids[0];
                p = SolitonTunnelMath.ChanAt(net[k], s - c.L, out _);
            }
            else p = SolitonTunnelMath.ChanAt(c, s, out _);
            cam.SpectateBlendSeconds = 0f;
            cam.Spectate = new CustomCameraController.SpectateView
            {
                Position = SolitonNetwork.V(p + g.Dr * 0.5), LookAt = hull.position, Up = hull.up,
            };
            return true;
        }

        void ReleaseNetworkCamera()
        {
            if (!_networkCam) { _networkCam = null; return; }
            _networkCam.Spectate = null;   // back to the follow camera, behind the hull at the channel's mouth
            _networkCam = null;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>This pilot's domain colour, the one its tunnels and its reticle take (tinted by their
        /// <see cref="SolitonTunnelConfigSO"/>).</summary>
        public Color Colour => DomainColour();

        /// <summary><see cref="ISolitonTunnelMaker.DomainColour"/>: this pilot's domain colour.</summary>
        Color ISolitonTunnelMaker.DomainColour => DomainColour();

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
        /// mode's, collected by this hull on its simulating machine, arms a Soliton Network exactly as a Meerkat omnicrystal does
        /// (<see cref="StepOmni"/> grows them at once, or the moment the hull is out of a tunnel). For the player and the AI.
        /// </summary>
        void OnOmniCrystal(IVesselStatus collector, Vector3 at)
        {
            if (!config || !config.OmniOn || collector != _status || !IsAuthority || !NetworkGrowsHere()) return;
            _omniArmed = true;
            _omniCrystal = -1;   // not one of the Meerkat field's (the relay's crystal -1 marks none taken)
            _omniSeed = unchecked((uint)(GetInstanceID() * 977 + Mathf.FloorToInt(at.x * 7f + at.y * 13f + at.z * 17f) +
                                         Mathf.FloorToInt(Time.time * 10f)));
        }

        // A vessel swap or despawn: this hull's tunnels go with it, and a ride ends where it is.
        void OnDisable()
        {
            OmniCrystalImpactor.CollectedByVessel -= OnOmniCrystal;
            SolitonTunnel.RemoveAllBy(this);
            s_executors.Remove(this);
            ReleaseSpeedTunnelScale();
            ReleaseRideCamera();
            ReleaseNetworkCamera();
            _swallow.Release(this);
            var nets = SolitonNetwork.Live;
            for (int i = nets.Count - 1; i >= 0; i--)
                if (ReferenceEquals(nets[i].Maker, this)) nets[i].Remove();
            if (s_localNetwork != null && (s_localNetwork == _network || s_localNetwork == _netRider.Network)) s_localNetwork = null;
            _network = null;
            _netRider.Clear();
            _omniArmed = _rtHeld = false;
            _camTunnel = null;
            _ride = null;
            _slider.Clear();
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
            s_executors.Clear();
            s_localNetwork = null;
        }
    }
}
