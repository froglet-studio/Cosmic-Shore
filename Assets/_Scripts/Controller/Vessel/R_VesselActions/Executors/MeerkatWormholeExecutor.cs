using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using FMODUnity;
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
    /// <item><b>The ride</b> (<see cref="StepRide"/>, flown by <see cref="MeerkatVesselTransformer"/>): the tunnel flies
    /// the hull, at its entry speed at both mouths and <c>throatSpeed</c> × it at the throat, and the hull comes out
    /// with the speed it went in with, on the tunnel's last heading.</item>
    /// <item><b>Its life</b>: it stays <c>staySeconds</c> once its rider is out, collapses to its centre over
    /// <c>collapseSeconds</c>, ripples the screen's edges there (<see cref="Ripples"/>) and fades over
    /// <see cref="MeerkatWormholeMath.FadeSeconds"/>. Everyone sees it (<see cref="Live"/>: the bridge, the bent
    /// space).</item>
    /// <item><b>The AI</b> (input only, /vessel-ai): once its reticle is back and its ring is far enough, it plans a
    /// tunnel (<see cref="MeerkatWormholeMath.TryPlan"/>), holds its line while it bends the reticle and squeezes LT for
    /// <c>aiShapeSeconds</c>, then presses RT through the REPLICATED press path, as a person's would be.</item>
    /// </list>
    ///
    /// <para><b>Where it runs.</b> The reticle, the ride and the AI run on the simulating machine only
    /// (<see cref="MantaStingActionExecutor.IsSimAuthority"/>). Every peer draws every tunnel: the owner publishes each
    /// one it forms, and its rider's way out, through <see cref="MeerkatWormholeNetworkRelay"/>; every peer runs the
    /// same life from there. The hull's pose replicates as every vessel's does.</para>
    /// </summary>
    public sealed class MeerkatWormholeExecutor : ShipActionExecutorBase
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
            public float L, B, M, Theta, FormT;
            public MeerkatWormholeMath.Window W;
            public bool Dying;
            public float DieT;
            public Color Colour;
            public MeerkatWormholeExecutor Maker;
            public int Seq;

            public Vector3 At(float s, out Vector3 tangent) => MeerkatWormholeMath.At(A, T0, Side, L, Theta, s, out tangent);
            public void Frame(float s, out Vector3 normal, out Vector3 binormal) =>
                MeerkatWormholeMath.Frame(T0, Side, L, Theta, s, out normal, out binormal);
            public float Radius(float s) => MeerkatWormholeMath.Radius(B, M, L, W.S0, W.S1, s);
            public float Fade => MeerkatWormholeMath.Fade(Dying, DieT);
            public MeerkatWormholeConfigSO Config => Maker ? Maker.config : null;
        }

        /// <summary>The reticle as its pilot sees it (the page's <c>ftLive.ret</c>, or the fading <c>retGhost</c>).</summary>
        public struct Reticle
        {
            public Vector3 A, T0, Side;
            public MeerkatWormholeMath.Shape Shape;
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

        /// <summary>Every tunnel alive now, everyone's.</summary>
        public static IReadOnlyList<Tunnel> Live => s_tunnels;
        /// <summary>Every collapse ripple still running.</summary>
        public static IReadOnlyList<Ripple> Ripples => s_ripples;
        /// <summary>Every enabled Meerkat (the view draws its pilot's reticle).</summary>
        public static IReadOnlyList<MeerkatWormholeExecutor> Executors => s_executors;

        IVesselStatus _status;
        MeerkatWormholeNetworkRelay _relay;
        ToggleTranslationModeActionExecutor _stance;

        // the reticle (the simulating machine)
        bool _retLive;
        float _retAge;
        float _retEmerge;
        Vector3 _retA, _retT0, _retSide = Vector3.right;
        MeerkatWormholeMath.Shape _retShape;
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
        float _rideS, _rideSIn, _vIn;
        Quaternion _q0;
        int _seq;
        Transform _tunnelScaleKey;   // the vessel holding the speed tunnel's ride scale (VesselSpeedTunnel.SetEffectScale)

        // the AI
        float _aiHold, _aiL, _aiM, _aiLt;
        bool _aiShaping, _aiPlanned;
        Vector2 _aiStick;
        System.Random _aiRand;
        InputEvents? _formInput;
        readonly List<ShipActionSO> _bindScratch = new();

        public MeerkatWormholeConfigSO Config => config;
        /// <summary>True while a tunnel flies this hull (from the press to its way out).</summary>
        public bool IsRiding => _ride != null;
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

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _relay = GetComponentInParent<MeerkatWormholeNetworkRelay>();
            _stance = TryGetComponent<ActionExecutorRegistry>(out var registry) ? registry.Get<ToggleTranslationModeActionExecutor>() : null;
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
            StepTunnels(dt);
            StepRipples();
            if (_ghostAlpha > 0f) _ghostAlpha -= dt / Mathf.Max(0.05f, _ghostFormT);
            if (!IsAuthority) return;
            StepSpeedTunnelScale();
            if (_ltHeld) _ltHeldFor += dt;
            if (_status.AutoPilotEnabled) Autopilot(dt);
            else _aiShaping = false;
            StepReticle(dt);
        }

        void LateUpdate() => MeerkatWormholeView.DrawOncePerFrame();

        /// <summary>The page's <c>gateHold</c> reticle: on the nose, bent by last frame's stick, shortened by LT.</summary>
        void StepReticle(float dt)
        {
            var hull = _status.Transform;
            bool live = hull && _ride == null && !_status.IsStationary;
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
            var sh = MeerkatWormholeMath.ShapeFor(config.Shape(), _easedLq, _eased.x, _eased.y);
            Vector3 c = sh.Stick >= 0.05f ? right * sh.Dx + up * sh.Dy : _retSide;
            c -= fwd * Vector3.Dot(c, fwd);
            _retSide = c.sqrMagnitude > 1e-8f ? c.normalized : right;
            _retShape = sh;
            // the right stick bends the reticle from the next frame (the page sets r.sx / r.sy after gateHold)
            _stick = _status.AutoPilotEnabled ? _aiStick
                : _status.InputStatus != null ? _status.InputStatus.EasedRightJoystickPosition : Vector2.zero;
        }

        /// <summary>The page's <c>gateCommit</c>.</summary>
        void Commit()
        {
            var hull = _status.Transform;
            if (_mine != null && !_mine.Dying) MeerkatWormholeMath.CollapseNow(ref _mine.W);   // the last one goes back to its centre now
            float vIn = Mathf.Max(1f, _status.Speed), dive = Vector3.Distance(hull.position, _retA) / vIn;
            var t = AddTunnel(_retA, _retT0, _retSide, _retShape, MeerkatWormholeMath.FormTime(config.FormSeconds, dive), ++_seq);
            _ghost = new Reticle { A = _retA, T0 = _retT0, Side = _retSide, Shape = _retShape, S0 = 0f, S1 = _retShape.L, Ghost = true };
            _ghostAlpha = 1f;
            _ghostFormT = t.FormT;
            // the ride starts at the hull: it dives straight down to the mouth the reticle showed (s < 0), then into the throat
            _ride = t;
            _rideS = -Vector3.Distance(hull.position, t.A);
            _rideSIn = 0f;
            _vIn = vIn;
            _q0 = Quaternion.FromToRotation(hull.forward, t.T0) * hull.rotation;
            _retLive = false;
            _retAge = 0f;
            if (_status.IsTranslationRestricted && _stance) _stance.EndStance();   // the page's `if (stance) stance = false`
            if (_relay) _relay.PublishFormed(t);
            PlayOneShot(config.FormEvent, hull.position);
            CSDebug.LogVerbose(CSLogChannel.BlackHole, $"[Meerkat] tunnel {t.Seq}: {t.L:F0} u, bent {t.Theta * Mathf.Rad2Deg:F0} deg");
        }

        Tunnel AddTunnel(Vector3 a, Vector3 t0, Vector3 side, in MeerkatWormholeMath.Shape sh, float formT, int seq)
        {
            var t = new Tunnel
            {
                A = a, T0 = t0, Side = side, L = sh.L, B = sh.B, M = sh.M, Theta = sh.Theta, FormT = formT,
                W = MeerkatWormholeMath.NewWindow(sh.L), Colour = DomainColour(), Maker = this, Seq = seq,
            };
            _mine = t;
            s_tunnels.Add(t);
            return t;
        }

        /// <summary>A remote peer's copy of a tunnel its owner formed (<see cref="MeerkatWormholeNetworkRelay"/>).</summary>
        internal void FormRemote(int seq, Vector3 a, Vector3 t0, Vector3 side, float L, float b, float m, float theta, float formT)
        {
            if (!config || IsAuthority) return;
            if (_mine != null && !_mine.Dying) MeerkatWormholeMath.CollapseNow(ref _mine.W);
            AddTunnel(a, t0, side, new MeerkatWormholeMath.Shape { L = L, B = b, M = m, Theta = theta }, formT, seq);
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
            if (t == null) return false;
            if (!s_tunnels.Contains(t))
            {
                _ride = null;   // the tunnel went from under the hull (a despawn): fly on
                return false;
            }
            float v = MeerkatWormholeMath.RideSpeed(_vIn, config.ThroatSpeed, _rideS, t.L);
            _rideS = Mathf.Min(t.L, _rideS + v * dt);
            if (_rideS < 0f)
            {
                position = t.A + t.T0 * _rideS;   // still diving to the mouth
                heading = t.T0;
            }
            else
            {
                position = t.At(_rideS, out heading);
                t.Frame(_rideS, out _, out var binormal);
                rotation = MeerkatWormholeMath.RideRotation(binormal, t.Theta, t.L, _rideS, _rideSIn, _q0).normalized;
            }
            speed = v;
            if (_rideS >= t.L - 1e-6f)
            {
                // the page's gateOut: out with the speed you went in with
                cameOut = true;
                speed = _vIn;
                _ride = null;
                MeerkatWormholeMath.RiderOut(ref t.W);
                if (_relay) _relay.PublishRiderOut(t.Seq);
            }
            return true;
        }

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
                if (!MeerkatWormholeMath.Step(ref t.W, t.L, t.FormT, config.StaySeconds, config.CollapseSeconds, dt)) continue;
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
                A = _retA, T0 = _retT0, Side = _retSide, Shape = _retShape, Alpha = alpha,
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
            if (!config.AiWormhole || !ai || !hull) return;
            if (_ride != null)
            {
                _aiHold = 0f;
                _aiPlanned = false;
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
                     MeerkatWormholeMath.TryPlan(config.Plan(), local, Mathf.Max(1f, _status.Speed),
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
        }

        // A vessel swap or despawn: this hull's tunnels go with it, and a ride ends where it is.
        void OnDisable()
        {
            for (int i = s_tunnels.Count - 1; i >= 0; i--)
                if (s_tunnels[i].Maker == this) s_tunnels.RemoveAt(i);
            s_executors.Remove(this);
            ReleaseSpeedTunnelScale();
            _ride = null;
            _mine = null;
            _retLive = false;
            _ltHeld = false;
            _formInput = null;
            _aiShaping = _aiPlanned = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            s_tunnels.Clear();
            s_ripples.Clear();
            s_executors.Clear();
        }
    }
}
