using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using FMODUnity;
using Reflex.Attributes;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Thresher's wrecking ball and chain: owns the chain physics
    /// (<see cref="ThresherChainSolver"/> + <see cref="ThresherChainLinks"/>), resolves what the
    /// ball and the chain do to prisms, and draws and voices all of it.
    ///
    /// <b>One step per frame, from inside the move step.</b> <see cref="ThresherVesselTransformer"/>
    /// calls <see cref="StepChain"/> through the base transformer's external-acceleration seam. If no
    /// transformer stepped it this frame (a hull whose transformer is off — a remote replica after a
    /// pilot swap), <see cref="LateUpdate"/> steps it from the hull's observed motion.
    ///
    /// <b>The ball is always destructive, and prisms are binary.</b> Every frame the ball's path is
    /// swept as a sphere through <see cref="PrismSpatialIndex.QuerySegment"/> (never colliders — a
    /// fast ball would tunnel, and prism collider LOD switches them off), nearest-first:
    /// <list type="bullet">
    /// <item><b>Below smash speed</b> the ball wears its pilot's DOMAIN colour. A rival prism is
    /// destroyed (the ball keeps <c>crushKeep</c> of its speed); a prism of its own domain is
    /// BOUNCED off.</item>
    /// <item><b>At smash speed</b> the ball goes RED and destroys every prism it hits — its own
    /// domain's included — and each hit sets off an explosion that does the same (the Grizzly's
    /// blast with <c>AffectSelfOverride</c> on). The ball ploughs on, keeping <c>plough</c>.</item>
    /// <item><b>Charge L5 "Lit"</b>: a hot ball burns the LIT domain colour, its explosions spare
    /// its own domain (domain-sparing lives in the explosion layer, per the fleet rule), and it
    /// bounces off its own domain's prisms even when hot.</item>
    /// </list>
    /// Every kill goes through <see cref="Prism.Damage"/> with the ball's own velocity as the impact
    /// vector — the Rhino sword's contact-velocity idea at its core — so shields pop and
    /// super-shields hold as for every other hull.
    ///
    /// <b>The chain slices.</b> While the chain is taut (or always, with Space L5 "Reaper Chain")
    /// every rival prism its links touch is <see cref="Prism.Slice"/>d along the plane the chain is
    /// sweeping, with debris at the touching link's own velocity. It never cuts its own domain, and
    /// it skips shielded prisms unless Reaper Chain is active (then the first cut pops the shield).
    ///
    /// <b>Networking: none of its own</b> (the Gibbon's shape). Press and release round-trip through
    /// <c>R_VesselActionHandler</c>, so every peer runs the same chain from the same inputs; a peer
    /// can disagree on a marginal hit by a frame of drift.
    /// </summary>
    public sealed class ThresherExecutor : ShipActionExecutorBase
    {
        [Tooltip("Every Thresher dial: chain physics, elements, explosion, chain cut, feel, look.")]
        [SerializeField] ThresherConfigSO config;

        [Header("Audio (LOCKED FMOD convention: ship EMPTY, never a borrowed event)")]
        [Tooltip("Heavy thud on a smash (ball at smash speed).")]
        [SerializeField] EventReference smashEvent;
        [Tooltip("A slow ball crushing a rival prism.")]
        [SerializeField] EventReference crushEvent;
        [Tooltip("The ball bouncing off a prism of its own domain.")]
        [SerializeField] EventReference bounceEvent;
        [Tooltip("The chain slicing a prism.")]
        [SerializeField] EventReference sliceEvent;
        [Tooltip("Clunk when the ball plants and becomes the pivot.")]
        [SerializeField] EventReference plantEvent;
        [Tooltip("Yank when the left trigger releases the pivot and the ball is dragged after the ship.")]
        [SerializeField] EventReference yankEvent;

        [Header("Materials (optional - runtime fallbacks when empty)")]
        [Tooltip("Ball material: ThresherBallMaterial (ThresherBallFresnelShader). Its body and rim colours are driven " +
                 "per frame through a MaterialPropertyBlock (_DarkColor / _BrightColor).")]
        [SerializeField] Material ballMaterial;
        [Tooltip("Skid-trail material (and the chain's fallback). Must honour vertex colour (Sprites/Default does).")]
        [SerializeField] Material lineMaterial;
        [Tooltip("Chain material: ThresherChainMaterial (ThresherChainFresnelShader - a fresnel TUBE drawn on the " +
                 "chain's ribbon). Body and rim colours are driven per frame through a MaterialPropertyBlock.")]
        [SerializeField] Material chainMaterial;

        [Inject] GameDataSO _gameData;

        IVesselStatus _status;
        ActionExecutorRegistry _registry;
        VesselImpactor _impactor;
        ThresherChainSolver _solver;
        ThresherChainLinks _links;
        ThresherChainSettings _baseSettings;
        ThresherChainSettings _settings;
        bool _seeded;
        bool _payOut;
        bool _plantHeld;

        int _lastStepFrame = -1;
        Vector3 _lastShipPosition;
        Vector3 _lastShipVelocity;

        // Smash bookkeeping
        int _combo;
        float _coolTime;
        float _freezeUntilUnscaled;
        float _nextExplosionTime;
        static bool s_timeScaleHitStopActive;
        readonly List<Prism> _candidates = new List<Prism>(64);
        readonly List<(float t, Prism prism)> _hits = new List<(float, Prism)>(32);
        HashSet<Prism> _touching = new HashSet<Prism>();
        HashSet<Prism> _touchingNext = new HashSet<Prism>();
        readonly HashSet<Prism> _cutThisFrame = new HashSet<Prism>();

        CancellationTokenSource _cts;

        // ------------------------------------------------------------------ public surface

        public bool IsPivoting => _solver != null && _solver.Mode == ThresherMode.Pivot;
        public ThresherMode Mode => _solver?.Mode ?? ThresherMode.Free;
        /// <summary>True while a MULTIPLAYER hit-stop is holding this hull and its ball still.</summary>
        public bool IsVesselFrozen => Time.unscaledTime < _freezeUntilUnscaled;
        /// <summary>Releasing the right trigger now would crack the ball past smash speed.</summary>
        public bool IsReady { get; private set; }
        public float BallSpeed => _solver?.BallSpeed ?? 0f;
        public Vector3 BallPosition => _solver?.BallPosition ?? transform.position;
        /// <summary>The ball is at smash speed: red (or lit), destroys anything, explodes.</summary>
        public bool IsHot => _solver != null && ThresherChainSolver.IsSmash(_solver.BallSpeed, _settings);
        /// <summary>Ball speed as a fraction of smash speed — the HUD heat gauge, and the ball's brightening.</summary>
        public float Gauge01 => _settings.SmashSpeed > 0f ? Mathf.Clamp01(BallSpeed / _settings.SmashSpeed) : 0f;
        /// <summary>How far the chain is let out, 0 (reeled in) to 1 (let out to its current reach).</summary>
        public float ChainOut01 => _solver == null || _settings.MaxLength <= _settings.RestLength ? 0f
            : Mathf.Clamp01((_solver.Length - _settings.RestLength) / (_settings.MaxLength - _settings.RestLength));
        /// <summary>The planted orbit's speed as a fraction of its cap; 0 when not planted.</summary>
        public float Spin01 => IsPivoting && _settings.LockMaxSpeed > 0f ? Mathf.Clamp01(_solver.LockSpeed / _settings.LockMaxSpeed) : 0f;
        public int Combo => _combo;
        /// <summary>The ball's colour this frame (LINEAR; a UI consumer converts with .gamma).</summary>
        public Color BallColorNow => config && _solver != null ? BallColor(out _) : Color.white;
        /// <summary>The READY lime (LINEAR).</summary>
        public Color ReadyColorNow => config ? ReadyColor() : Color.green;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _registry = GetComponentInParent<ActionExecutorRegistry>(true);
            _impactor = _status != null && _status.Transform ? _status.Transform.GetComponent<VesselImpactor>() : null;
            RebuildSolver();
            _seeded = false;
            _payOut = false;
            _plantHeld = false;
            _combo = 0;
            _touching.Clear();
            _touchingNext.Clear();
        }

        void RebuildSolver()
        {
            _baseSettings = config ? config.Dials.ToSettings() : new ThresherDials().ToSettings();
            _settings = _baseSettings;
            if (_solver == null) _solver = new ThresherChainSolver(_settings);
            else _solver.Settings = _settings;
            int links = config ? config.ChainLinks : 12;
            if (_links == null || _links.Count != links + 1) _links = new ThresherChainLinks(links);
        }

        /// <summary>The four element scalings, read live (never cached) and folded into this frame's
        /// settings. Read through <c>EvaluateReplicated</c>, not <c>EvaluateLive</c>: every peer
        /// simulates this ball, and element levels themselves do not replicate, so a remote copy
        /// reading its own (empty) ResourceSystem would fly a different ball. Mass is volume, so the
        /// ball's radius grows with the cube root of its mass.</summary>
        void ApplyElementScaling()
        {
            _settings = _baseSettings;
            if (!config || _status == null) return;
            float mass = Mathf.Max(0.05f, config.BallMassMultiplier.EvaluateReplicated(_status));
            float reach = Mathf.Max(0.05f, config.ChainReachMultiplier.EvaluateReplicated(_status));
            float spin = Mathf.Max(0.05f, config.SpinMultiplier.EvaluateReplicated(_status));
            _settings.BallMass *= mass;
            _settings.BallRadius *= Mathf.Pow(mass, 1f / 3f);
            _settings.MaxLength = Mathf.Max(_settings.RestLength, _settings.MaxLength * reach);
            _settings.LockSpinRate *= spin;
            _settings.LockMaxSpeed *= spin;
            _solver.Settings = _settings;
        }

        // The four level-5 upgrades, each read through the replicated unlock bits at the moment
        // of use (a per-hit / per-release snapshot), never cached.
        R_VesselElementalAbilityHandler Abilities => _status?.ElementalAbilityHandler;
        /// <summary>Charge L5 "Lit": the hot ball spares its own domain (explosion and contact).</summary>
        bool LitActive => Abilities && Abilities.IsUpgradeActive(Element.Charge);
        /// <summary>Mass L5 "Wrecker": a smash costs the ball no speed.</summary>
        bool WreckerActive => Abilities && Abilities.IsUpgradeActive(Element.Mass);
        /// <summary>Space L5 "Reaper Chain": the chain cuts while slack, and cuts shielded prisms.</summary>
        bool ReaperActive => Abilities && Abilities.IsUpgradeActive(Element.Space);
        /// <summary>Time L5 "Slingshot": the unlock yank is never weaker than smash speed.</summary>
        bool SlingshotActive => Abilities && Abilities.IsUpgradeActive(Element.Time);

        // ------------------------------------------------------------------ input edges

        public void BeginPayOut() => _payOut = true;
        public void EndPayOut() => _payOut = false;

        public void BeginPlant()
        {
            if (_solver == null) return;
            _plantHeld = true;
            _solver.Plant();
        }

        public void EndPlant()
        {
            if (_solver == null) return;
            _plantHeld = false;
            bool wasPivot = _solver.Mode == ThresherMode.Pivot;
            _solver.Release(_lastShipVelocity, slingshot: SlingshotActive);
            if (wasPivot) PlayAt(yankEvent, _lastShipPosition);
        }

        void OnEnable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        void OnDisable()
        {
            // The release edge never arrives for a vessel whose input is paused or handed to the
            // autopilot, so a held trigger must not stay held across a disable.
            _payOut = false;
            if (_plantHeld && _solver != null) _solver.Release(_lastShipVelocity);
            _plantHeld = false;
            _freezeUntilUnscaled = 0f;
            _cts?.Cancel();
            ReleaseCamera();
            SetVisualsActive(false);
        }

        void OnDestroy()
        {
            ReleaseCamera();
            _cts?.Dispose();
            _cts = null;
            DestroyVisuals();
        }

        // ------------------------------------------------------------------ the step

        /// <summary>
        /// One chain step. Called by <see cref="ThresherVesselTransformer"/> from inside its move
        /// step; returns the ship's velocity after the rope (towing) or the orbit (pivot).
        /// <paramref name="pivotSteer"/> is the pilot's stick as a world direction (magnitude 0..1)
        /// that tilts a planted orbit at the low <c>pivotSteer</c> rate; ignored while towing.
        /// </summary>
        public Vector3 StepChain(Vector3 shipPosition, Vector3 shipVelocity, float shipCruise, float dt,
                                 Vector3 pivotSteer = default)
        {
            if (_solver == null) RebuildSolver();
            ApplyElementScaling();
            EnsureSeeded(shipPosition, shipVelocity);

            var result = _solver.Step(shipPosition, shipVelocity, _payOut, shipCruise, dt, pivotSteer);
            _lastStepFrame = Time.frameCount;
            _lastShipPosition = shipPosition;
            _lastShipVelocity = result.ShipVelocity;

            if (result.Planted) PlayAt(plantEvent, _solver.Pivot);
            if (_solver.Mode != ThresherMode.Pivot)
                SweepBall(result.BallFrom, _solver.BallPosition);

            _links.Step(shipPosition + result.ShipVelocity * dt, _solver.BallPosition, _solver.Length);
            CutWithChain(dt);

            UpdateCombo(dt);
            IsReady = _payOut && _solver.Mode == ThresherMode.Free &&
                      _solver.PredictReelCrackSpeed(shipPosition, result.ShipVelocity)
                          >= _settings.SmashSpeed * (config ? config.ReadyMargin : 1f);
            return result.ShipVelocity;
        }

        void EnsureSeeded(Vector3 shipPosition, Vector3 shipVelocity)
        {
            // Seed on first use, and RE-seed after a teleport (respawn, pose set): a ball left
            // three chain-lengths away would otherwise be hauled back across the arena by the rope.
            bool stranded = _seeded &&
                (_solver.BallPosition - shipPosition).sqrMagnitude >
                (3f * _settings.MaxLength) * (3f * _settings.MaxLength);
            if (_seeded && !stranded) return;
            Vector3 forward = _status != null && _status.Transform ? _status.Transform.forward : Vector3.forward;
            _solver.Reset(shipPosition, forward, shipVelocity);
            _links.Reset(shipPosition, _solver.BallPosition);
            _seeded = true;
            _touching.Clear();
        }

        void LateUpdate()
        {
            if (_solver == null || _status == null || _status.Transform == null) return;

            // Fallback step for a hull nobody moved through the transformer this frame.
            if (_lastStepFrame != Time.frameCount && !IsVesselFrozen && Time.deltaTime > 0f)
            {
                Vector3 pos = _status.Transform.position;
                Vector3 vel = _seeded ? (pos - _lastShipPosition) / Time.deltaTime : Vector3.zero;
                StepChain(pos, vel, _status.Speed, Time.deltaTime);
            }

            UpdateVisuals();
            UpdateCamera();
        }

        // ------------------------------------------------------------------ the ball

        void SweepBall(Vector3 from, Vector3 to)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable || _status == null) return;

            float radius = _settings.BallRadius;
            const float candidateExtent = 8f;   // prism half-extent allowance; refined below
            index.QuerySegment(from, to, radius + candidateExtent, _candidates);

            Vector3 ab = to - from;
            float abLenSq = ab.sqrMagnitude;
            _hits.Clear();
            for (int i = 0; i < _candidates.Count; i++)
            {
                var prism = _candidates[i];
                if (!prism || prism.destroyed) continue;
                Vector3 centre = prism.transform.position;
                float t = abLenSq > 1e-8f ? Mathf.Clamp01(Vector3.Dot(centre - from, ab) / abLenSq) : 0f;
                float contact = radius + 0.5f * prism.transform.lossyScale.magnitude;
                if ((centre - (from + ab * t)).sqrMagnitude > contact * contact) continue;
                _hits.Add((t, prism));
            }
            _hits.Sort((a, b) => a.t.CompareTo(b.t));

            Domains own = PilotDomain();
            bool lit = LitActive;
            bool wrecker = WreckerActive;

            _touchingNext.Clear();
            for (int i = 0; i < _hits.Count; i++)
            {
                var prism = _hits[i].prism;
                if (!prism || prism.destroyed) continue;
                // The fleet's one own-trail rule: a vessel never hits mass it laid moments ago.
                if (SelfTrailContactConfigSO.SuppressesSkimContact(prism, _status)) continue;

                _touchingNext.Add(prism);
                if (_touching.Contains(prism)) continue;   // one hit per contact

                Vector3 at = from + ab * _hits[i].t;
                float speed = _solver.BallSpeed;
                bool hot = ThresherChainSolver.IsSmash(speed, _settings);
                bool ownDomain = prism.Domain == own;

                if (ownDomain && (!hot || lit))
                {
                    // Bounce: the ball never harms its own domain unless it is red-hot and unlit.
                    Vector3 normal = at - prism.transform.position;
                    if (_solver.Bounce(at, normal)) PlayAt(bounceEvent, at);
                    break;   // the ball's path this frame ends at the bounce
                }

                Vector3 impact = _solver.BallVelocity * (config ? config.DebrisRestitution : 1f / 3f);
                prism.Damage(impact, _status.Domain, _status.PlayerName,
                             debrisSpeedLimit: config ? config.DebrisSpeedLimit : 200f);
                _solver.RegisterHit(ThresherChainSolver.KeepAfterHit(speed, _settings, wrecker));

                if (hot) OnSmash(at, speed, lit);
                else OnCrush(at);
            }

            (_touching, _touchingNext) = (_touchingNext, _touching);
        }

        void OnSmash(Vector3 at, float speed, bool lit)
        {
            float heat = ThresherChainSolver.Heat01(speed, _settings);
            _combo++;
            _coolTime = 0f;
            Explode(at, lit);
            PlayAt(smashEvent, at);
            if (!IsLocalPilot() || !config) return;

            if (_combo == 1)
                HitStop(Mathf.Lerp(config.HitStopMinSeconds, config.HitStopMaxSeconds, heat));
            Shake(Mathf.Lerp(config.ShakeMin, config.ShakeMax, heat) * (_combo == 1 ? 1f : 0.6f), config.ShakeSeconds);
            HapticController.PlaySkim(Mathf.Lerp(config.HapticFloor01, 1f, heat));
        }

        void OnCrush(Vector3 at)
        {
            PlayAt(crushEvent, at);
            if (config && IsLocalPilot())
                Shake(config.ShakeMin * config.CrushShakeFraction, config.ShakeSeconds * 0.5f);
        }

        /// <summary>
        /// The smash explosion. Unlit it destroys every domain's prisms (<c>AffectSelfOverride</c>
        /// true, the Grizzly charged shot's setting); lit it spares the pilot's own domain — the
        /// fleet's rule that domain-sparing lives in the EXPLOSION layer, never in Prism.Damage.
        /// Rate-limited so a hot ball through a row makes a string of blasts, not one per prism.
        /// </summary>
        void Explode(Vector3 at, bool lit)
        {
            if (!config || config.SmashExplosions is not { Length: > 0 } || _status == null) return;
            if (Time.time < _nextExplosionTime) return;
            _nextExplosionTime = Time.time + config.ExplosionMinInterval;

            float diameter = config.ExplosionDiameter * Mathf.Max(0.1f, config.ExplosionSizeMultiplier.EvaluateReplicated(_status));
            Vector3 v = _solver.BallVelocity;
            var init = new AOEExplosion.InitializeStruct
            {
                OwnDomain = _status.Domain,
                AnnonymousExplosion = false,
                Vessel = _status.Vessel,
                OverrideMaterial = _status.AOEExplosionMaterial,
                MaxScale = diameter,
                SpawnPosition = at,
                SpawnRotation = v.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(v) : Quaternion.identity,
                AffectSelfOverride = !lit,
            };
            ExplosionHelper.CreateExplosion(config.SmashExplosions, init, _impactor ? _impactor.DIContainer : null);
        }

        // ------------------------------------------------------------------ the chain

        void CutWithChain(float dt)
        {
            if (!config || _status == null || _links == null) return;
            bool reaper = ReaperActive;
            bool taut = _solver.Mode == ThresherMode.Pivot || _solver.IsTaut;
            if (!taut && !reaper) return;

            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;

            Domains own = PilotDomain();
            float cut = config.ChainCutRadius;
            const float candidateExtent = 8f;
            _cutThisFrame.Clear();

            var pts = _links.Points;
            int n = pts.Length;
            for (int s = 0; s < n - 1; s++)
            {
                Vector3 a = pts[s], b = pts[s + 1];
                index.QuerySegment(a, b, cut + candidateExtent, _candidates);
                if (_candidates.Count == 0) continue;

                Vector3 ab = b - a;
                float abLenSq = ab.sqrMagnitude;
                for (int i = 0; i < _candidates.Count; i++)
                {
                    var prism = _candidates[i];
                    if (!prism || prism.destroyed || _cutThisFrame.Contains(prism)) continue;
                    if (prism.Domain == own) continue;   // the chain never cuts its own domain
                    if (!reaper && prism.prismProperties != null &&
                        (prism.prismProperties.IsShielded || prism.prismProperties.IsSuperShielded)) continue;
                    if (SelfTrailContactConfigSO.SuppressesSkimContact(prism, _status)) continue;

                    Vector3 centre = prism.transform.position;
                    float t = abLenSq > 1e-8f ? Mathf.Clamp01(Vector3.Dot(centre - a, ab) / abLenSq) : 0f;
                    float contact = cut + 0.5f * prism.transform.lossyScale.magnitude;
                    Vector3 onChain = a + ab * t;
                    if ((centre - onChain).sqrMagnitude > contact * contact) continue;

                    // The link's own velocity is the blade's: the chain point moves between the
                    // hull's velocity and the ball's (a lever arm), so the debris does too.
                    Vector3 v = Vector3.Lerp(_links.NodeVelocity(s, dt), _links.NodeVelocity(s + 1, dt), t);
                    Vector3 cutNormal = Vector3.Cross(ab, v);
                    if (cutNormal.sqrMagnitude < 1e-8f) cutNormal = Vector3.Cross(ab, Vector3.up);
                    if (cutNormal.sqrMagnitude < 1e-8f) cutNormal = Vector3.right;

                    _cutThisFrame.Add(prism);
                    prism.Slice(v * config.ChainDebrisRestitution, _status.Domain, _status.PlayerName,
                                onChain, cutNormal.normalized, debrisSpeedLimit: config.DebrisSpeedLimit);
                    PlayAt(sliceEvent, onChain);
                }
            }
        }

        // ------------------------------------------------------------------ feedback

        Domains PilotDomain() => _status?.Player != null ? _status.Domain : Domains.Blue;

        bool IsLocalPilot()
            => _status?.Player != null && _status.IsLocalUser && !_status.AutoPilotEnabled;

        void UpdateCombo(float dt)
        {
            if (_combo == 0) return;
            if (ThresherChainSolver.IsSmash(_solver.BallSpeed, _settings)) { _coolTime = 0f; return; }
            _coolTime += dt;
            if (_coolTime >= (config ? config.ComboGraceSeconds : 0.4f)) _combo = 0;
        }

        void PlayAt(EventReference evt, Vector3 at)
        {
            if (evt.IsNull) return;
            _registry?.AudioSystem?.PlaySFXEvent(evt, at);
        }

        static void Shake(float intensity, float duration)
        {
            if (CameraManager.Instance != null &&
                CameraManager.Instance.GetActiveController() is CustomCameraController cam)
                cam.Shake(intensity, duration);
        }

        /// <summary>
        /// One hit-stop per whip. SOLO: a brief global time-scale drop (the AstroLeague ball's
        /// pattern, restoring to constants so it cannot clobber a pause). MULTIPLAYER: a local
        /// timescale would freeze every peer's world on this client, so only this hull and its
        /// ball hold still.
        /// </summary>
        void HitStop(float seconds)
        {
            if (seconds <= 0f) return;
            if (IsSoloSession()) RunTimeScaleHitStop(seconds).Forget();
            else _freezeUntilUnscaled = Time.unscaledTime + seconds;
        }

        static bool IsSoloSession()
        {
            var nm = NetworkManager.Singleton;
            return nm == null || !nm.IsListening || nm.ConnectedClientsIds.Count <= 1;
        }

        async UniTaskVoid RunTimeScaleHitStop(float seconds)
        {
            if (s_timeScaleHitStopActive || PauseSystem.Paused) return;
            s_timeScaleHitStopActive = true;
            float scale = config ? config.HitStopTimeScale : 0.05f;
            float baseFixedDelta = Time.fixedDeltaTime / Mathf.Max(Time.timeScale, 0.0001f);
            Time.timeScale = scale;
            Time.fixedDeltaTime = baseFixedDelta * Mathf.Max(scale, 0.0001f);
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: true,
                                    cancellationToken: _cts?.Token ?? CancellationToken.None);
            }
            catch (OperationCanceledException) { }
            finally
            {
                Time.timeScale = PauseSystem.Paused ? 0f : 1f;
                Time.fixedDeltaTime = baseFixedDelta;
                s_timeScaleHitStopActive = false;
            }
        }

        // ------------------------------------------------------------------ camera

        // The chase camera this hull is driving (null when it drives none), the distance it has
        // eased it to, and the spectate vantage's frame while detached.
        CustomCameraController _cam;
        float _camDistance;
        float _camWritten = float.NaN;   // the last distance this hull wrote (negative z)
        bool _spectating;
        Vector3 _spectateNormal, _spectateOutward, _spectateUp;
        float _spectateRadius;
        float _spectateQualifyTime;

        /// <summary>
        /// The local pilot's camera, two jobs (<c>THRESHER.md</c> § Camera):
        /// <list type="bullet">
        /// <item><b>Keep the ball in frame.</b> The chase distance is pulled back as far as the
        /// rendered ball needs (<see cref="ThresherCameraFraming.RequiredDistance"/>), FAST, and
        /// eased back toward the prefab's own distance SLOWLY — never nearer than it.</item>
        /// <item><b>Spectate a fast spin.</b> Once a planted orbit circles faster than
        /// <c>spectateSpinRate</c>, the camera blends to a still vantage over the orbit and watches
        /// the ship spin rather than riding it; it re-attaches when the plant is released or the
        /// spin falls back under the release fraction.</item>
        /// </list>
        /// Pose only, through <see cref="CustomCameraController.SetCameraDistance"/> and
        /// <see cref="CustomCameraController.Spectate"/>; FOV belongs to the speed tunnel and is
        /// only READ here. Remote replicas and AI hulls never touch a camera.
        /// </summary>
        void UpdateCamera()
        {
            if (!config || !config.CameraFraming || !_seeded || !IsLocalPilot()) { ReleaseCamera(); return; }

            var cam = CameraManager.Instance != null
                ? CameraManager.Instance.GetActiveController() as CustomCameraController
                : null;
            if (cam != _cam)
            {
                ReleaseCamera();
                _cam = cam;
                if (_cam) _camDistance = Mathf.Abs(_cam.GetCameraDistance());
            }
            if (!_cam || !_cam.Camera || _cam.PlacementAnchor.HasValue) return;

            // Only while the camera is following THIS hull: a cinematic or end-of-match camera that
            // has been pointed elsewhere is not ours to move.
            Transform ship = _status.Transform;
            Transform followed = _cam.FollowTarget;
            if (!followed || (followed != ship && !followed.IsChildOf(ship))) { ReleaseCamera(); return; }

            float dt = Time.unscaledDeltaTime;
            float ballRadius = _settings.BallRadius * config.BallVisualScale;
            float halfFovV = 0.5f * _cam.Camera.fieldOfView * Mathf.Deg2Rad;
            float halfFovH = Mathf.Atan(Mathf.Tan(halfFovV) * Mathf.Max(0.1f, _cam.Camera.aspect));

            // Zoom. The prefab's distance is re-read every frame, so a settings change still owns it.
            float neutral = Mathf.Abs(_cam.NeutralOffsetZ);
            if (neutral <= 0f) neutral = _camDistance;
            Vector3 ballLocal = ship.InverseTransformDirection(_solver.BallPosition - ship.position);
            float height = _cam.GetFollowOffset().y;
            float need = ThresherCameraFraming.RequiredDistance(ballLocal, ballRadius, height,
                halfFovV, halfFovH, config.CameraFramingMargin, config.CameraMinAhead);
            // The REACH envelope (ThresherCameraFraming.ReachDistance): what a ball anywhere on the
            // chain's horizontal circle at its CURRENT length would need. Without it the target
            // tracked the ball round every swing (abeam 161 u, ahead 15 u at full let-out) and the
            // camera pumped in and out at the swing's rhythm; with it the distance follows the
            // winch, and the ball's own position only adds (a ball looped overhead).
            need = Mathf.Max(need, ThresherCameraFraming.ReachDistance(_solver.Length,
                ballRadius, height, halfFovV, halfFovH, config.CameraFramingMargin, config.CameraMinAhead));
            float target = Mathf.Clamp(need, neutral, Mathf.Max(neutral, config.CameraMaxDistance));
            _camDistance = ThresherCameraFraming.Ease(_camDistance, target,
                config.CameraZoomOutRate, config.CameraZoomInRate, dt);
            _camWritten = -_camDistance;
            _cam.SetCameraDistance(_camWritten);
            _cam.MaxFollowTurnRate = config.CameraMaxFollowTurnRate;

            // Spectate.
            float spin = IsPivoting ? ThresherCameraFraming.SpinRate(_solver.LockSpeed, _solver.Length) : 0f;
            // Detach only once the spin has held over the threshold for spectateEngageSeconds, so a
            // tap of LT does not bob the camera up toward the vantage and straight back down.
            bool qualifies = IsPivoting && config.SpectateSpinRate > 0f && spin >= config.SpectateSpinRate;
            _spectateQualifyTime = qualifies ? _spectateQualifyTime + dt : 0f;
            if (!_spectating && qualifies && _spectateQualifyTime >= config.SpectateEngageSeconds)
                BeginSpectate(ship.position);
            else if (_spectating && (!IsPivoting || spin < config.SpectateSpinRate * config.SpectateReleaseFraction))
                EndSpectate();

            if (!_spectating) return;
            Vector3 pivot = _solver.Pivot;
            FollowOrbitPlane(ship.position, pivot, dt);
            _spectateRadius = ThresherCameraFraming.Ease(_spectateRadius, _solver.Length + ballRadius,
                config.CameraZoomOutRate, config.CameraZoomInRate, dt);
            _cam.SpectateBlendSeconds = config.SpectateBlendSeconds;
            _cam.Spectate = new CustomCameraController.SpectateView
            {
                Position = ThresherCameraFraming.SpectatePosition(pivot, _spectateNormal, _spectateOutward,
                    _spectateRadius, Mathf.Min(halfFovV, halfFovH), config.SpectateTiltDegrees, config.CameraFramingMargin),
                LookAt = pivot,
                Up = _spectateUp,
            };
        }

        /// <summary>Freeze the vantage's frame at the moment of detaching: the orbit's axis on the
        /// camera's own side of the plane (so it never flips to watch from underneath), the side
        /// the ship was on, and the screen's up = the way the camera was facing, laid into the
        /// plane — the ship's heading stays "up" across the cut.</summary>
        void BeginSpectate(Vector3 shipPosition)
        {
            Vector3 pivot = _solver.Pivot;
            Vector3 outward = shipPosition - pivot;
            Vector3 normal = Vector3.Cross(outward, _lastShipVelocity);
            if (normal.sqrMagnitude < 1e-6f || outward.sqrMagnitude < 1e-6f) return;
            normal.Normalize();
            if (Vector3.Dot(normal, _cam.transform.position - pivot) < 0f) normal = -normal;

            Vector3 up = Vector3.ProjectOnPlane(_cam.transform.forward, normal);
            if (up.sqrMagnitude < 1e-6f) up = outward;

            _spectateNormal = normal;
            _spectateOutward = outward.normalized;
            _spectateUp = up.normalized;
            _spectateRadius = _solver.Length;
            _spectating = true;
        }

        /// <summary>The pilot can tilt a planted orbit (<c>pivotSteer</c>), so the vantage leans
        /// after the orbit's plane — slowly (<c>spectatePlaneFollowRate</c>), so a steady tilt reads
        /// as the orbit turning in front of a still camera, never as the camera swinging. The axis
        /// stays on the camera's side of the plane; outward and up are re-laid into it.</summary>
        void FollowOrbitPlane(Vector3 shipPosition, Vector3 pivot, float dt)
        {
            Vector3 now = Vector3.Cross(shipPosition - pivot, _lastShipVelocity);
            if (now.sqrMagnitude < 1e-6f) return;
            now.Normalize();
            if (Vector3.Dot(now, _spectateNormal) < 0f) now = -now;
            float t = 1f - Mathf.Exp(-config.SpectatePlaneFollowRate * dt);
            _spectateNormal = Vector3.Slerp(_spectateNormal, now, t).normalized;

            Vector3 outward = Vector3.ProjectOnPlane(_spectateOutward, _spectateNormal);
            if (outward.sqrMagnitude > 1e-6f) _spectateOutward = outward.normalized;
            Vector3 up = Vector3.ProjectOnPlane(_spectateUp, _spectateNormal);
            if (up.sqrMagnitude > 1e-6f) _spectateUp = up.normalized;
        }

        void EndSpectate()
        {
            if (_spectating && _cam) _cam.Spectate = null;
            _spectating = false;
        }

        /// <summary>Hand the camera back: no vantage, and the prefab's own distance — unless
        /// something else has written the distance since this hull last did (a vessel swap's
        /// settings landing first), in which case that write stands.</summary>
        void ReleaseCamera()
        {
            _spectateQualifyTime = 0f;
            if (!_cam) { _cam = null; _spectating = false; _camWritten = float.NaN; return; }
            EndSpectate();
            _cam.MaxFollowTurnRate = 0f;
            float neutral = _cam.NeutralOffsetZ;
            if (neutral < 0f && _cam.GetCameraDistance() == _camWritten) _cam.SetCameraDistance(neutral);
            _cam = null;
            _camWritten = float.NaN;
        }

        // ------------------------------------------------------------------ colours

        SO_ColorSet Palette => _gameData && _gameData.ThemeManagerData ? _gameData.ThemeManagerData.ColorSet : null;

        Color DomainColor(Domains domain)
        {
            var palette = Palette;
            if (domain == Domains.Blue || !palette) return config.FallbackDomainColor;
            return palette.GetDomainSignalColor(domain);
        }

        /// <summary>The LIT domain colour: the domain's shielded-prism rim, the brightest (HDR)
        /// colour the palette authors for it.</summary>
        Color LitColor(Domains domain)
        {
            var palette = Palette;
            if (domain == Domains.Blue || !palette ||
                !palette.TryGetPrismKindColors(domain, PrismKind.Shielded, out var rim, out _))
                return DomainColor(domain) * 2.5f;
            return rim;
        }

        Color DangerColor()
        {
            var palette = Palette;
            if (!palette) return config.FallbackDangerColor;
            var c = palette.GetDangerSignalColor();
            return c.a > 0f ? c : config.FallbackDangerColor;
        }

        Color ReadyColor()
        {
            var palette = Palette;
            if (!palette) return config.FallbackReadyColor;
            var c = palette.GetCtaSignalColor();
            return c.a > 0f ? c : config.FallbackReadyColor;
        }

        /// <summary>The ball's colour this frame: the pilot's domain colour, brightening as it nears
        /// smash speed; RED once it gets there (destructive to all); LIT domain colour instead of
        /// red with the Charge upgrade.</summary>
        Color BallColor(out float emission)
        {
            Domains domain = PilotDomain();
            if (IsHot)
            {
                float heat = ThresherChainSolver.Heat01(_solver.BallSpeed, _settings);
                emission = Mathf.Lerp(2.5f, 4f, heat);
                return LitActive ? LitColor(domain) : DangerColor();
            }
            float g = Gauge01;
            emission = Mathf.Lerp(0.15f, 1.2f, g * g);
            return DomainColor(domain) * Mathf.Lerp(0.45f, 1f, g);
        }

        /// <summary>
        /// The fresnel ball's two colours (<c>ThresherBallFresnelShader</c>): the ball's HUE
        /// (<see cref="BallColor"/>) normalised by its max channel, so brightness is set by the
        /// ball's state and never by how bright the palette happened to author the hue. Cool, the
        /// body rises from under the bloom threshold toward the 0.5 clamp as the ball nears smash
        /// speed and the rim from a soft edge toward 1.0; hot, the body sits ON the clamp (the whole
        /// ball blooms) and the rim whitens with heat. Nothing exceeds 1.0: tonemapping is None,
        /// so a channel over 1 clips and shifts the hue (Docs/PALETTE.md §2.2).
        /// </summary>
        void BallShade(out Color body, out Color rim)
        {
            Color hue = BallColor(out _);
            float g = Gauge01;
            if (IsHot)
            {
                float heat = ThresherChainSolver.Heat01(_solver.BallSpeed, _settings);
                body = WithMaxChannel(hue, config.BallBodyHot);
                rim = Color.Lerp(WithMaxChannel(hue, config.BallRimHot),
                                 new Color(config.BallRimHot, config.BallRimHot, config.BallRimHot, 1f),
                                 heat * config.BallHotRimWhiten);
                return;
            }
            float k = g * g;
            body = WithMaxChannel(hue, Mathf.Lerp(config.BallBodyCool, config.BallBodyNearSmash, k));
            rim = WithMaxChannel(hue, Mathf.Lerp(config.BallRimCool, config.BallRimHot, k));
        }

        /// <summary>The chain tube's two colours: an iron body (<c>chainColor</c>), and a rim in the
        /// ball's hue — dim while slack, brighter while taut (the state in which it cuts) — that
        /// flickers to the palette's CTA lime while READY.</summary>
        void ChainShade(out Color body, out Color rim)
        {
            body = config.ChainColor;
            body.a = 1f;
            bool taut = _solver.Mode == ThresherMode.Pivot || _solver.IsTaut;
            rim = WithMaxChannel(BallColor(out _), taut ? config.ChainRimTaut : config.ChainRimSlack);
            if (!IsReady) return;
            Color lime = WithMaxChannel(ReadyColor(), config.ChainRimReady);
            bool on = Mathf.Repeat(Time.time * config.ReadyFlickerHz, 1f) < 0.5f;
            rim = on ? lime : Color.Lerp(rim, lime, 0.35f);
        }

        static Color WithMaxChannel(Color c, float max)
        {
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            return m > 1e-4f
                ? new Color(c.r / m * max, c.g / m * max, c.b / m * max, 1f)
                : new Color(max, max, max, 1f);
        }

        // ------------------------------------------------------------------ visuals

        // All runtime-built and cosmetic; explicitly a prototype look, replaced wholesale when art
        // lands. Every peer draws its own copy.
        GameObject _root;
        Transform _ball;
        MeshRenderer[] _ballRenderers;
        LineRenderer _chain;
        TrailRenderer _skid;
        TextMeshPro _comboLabel;
        MaterialPropertyBlock _mpb;
        MaterialPropertyBlock _chainMpb;
        float _comboShownScale;
        int _comboShown;

        static Material s_lineMaterial, s_ballMaterial;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");

        void EnsureVisuals()
        {
            if (_root) return;
            var lineMat = LineMaterialOrFallback();
            var ballMat = BallMaterialOrFallback();
            if (!lineMat || !ballMat) return;   // headless: no graphics, no visuals

            _root = new GameObject($"ThresherVisuals::{name}");
            _mpb = new MaterialPropertyBlock();

            // The ball: a heavy core plus six studs, so it reads as a chunky wrecking ball and its
            // roll is visible.
            _ball = new GameObject("Ball").transform;
            _ball.SetParent(_root.transform, false);
            var parts = new List<MeshRenderer>();
            parts.Add(MakePrimitive(PrimitiveType.Sphere, _ball, Vector3.zero, Vector3.one, ballMat));
            Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var a in axes)
                parts.Add(MakePrimitive(PrimitiveType.Cube, _ball, a * 0.5f, Vector3.one * 0.32f, ballMat));
            _ballRenderers = parts.ToArray();

            _chain = MakeLine("Chain", chainMaterial ? chainMaterial : lineMat, false);
            _chain.alignment = LineAlignment.View;   // the fresnel tube is computed across a VIEW-aligned ribbon
            _chainMpb = new MaterialPropertyBlock();

            var skidGo = new GameObject("SkidTrail");
            skidGo.transform.SetParent(_ball, false);
            _skid = skidGo.AddComponent<TrailRenderer>();
            _skid.sharedMaterial = lineMat;
            _skid.time = config ? config.SkidTrailSeconds : 0.6f;
            _skid.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _skid.emitting = false;

            var labelGo = new GameObject("Combo", typeof(RectTransform));
            labelGo.transform.SetParent(_root.transform, false);
            _comboLabel = labelGo.AddComponent<TextMeshPro>();
            _comboLabel.alignment = TextAlignmentOptions.Center;
            _comboLabel.text = string.Empty;
            if (TMP_Settings.defaultFontAsset) _comboLabel.font = TMP_Settings.defaultFontAsset;
        }

        static MeshRenderer MakePrimitive(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            if (go.TryGetComponent(out Collider c)) Destroy(c);   // cosmetic: the sweep is the physics
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return mr;
        }

        LineRenderer MakeLine(string label, Material mat, bool loop)
        {
            var go = new GameObject(label);
            go.transform.SetParent(_root.transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        Material LineMaterialOrFallback()
        {
            if (lineMaterial) return lineMaterial;
            if (s_lineMaterial) return s_lineMaterial;
            var shader = Shader.Find("Sprites/Default");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) return null;
            s_lineMaterial = new Material(shader) { name = "ThresherLine (runtime)", renderQueue = 3000 };
            return s_lineMaterial;
        }

        Material BallMaterialOrFallback()
        {
            if (ballMaterial) return ballMaterial;
            if (s_ballMaterial) return s_ballMaterial;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Sprites/Default");
            if (!shader) return null;
            s_ballMaterial = new Material(shader) { name = "ThresherBall (runtime)" };
            return s_ballMaterial;
        }

        void SetVisualsActive(bool active)
        {
            if (_root) _root.SetActive(active);
        }

        void DestroyVisuals()
        {
            if (_root) Destroy(_root);
            _root = null;
        }

        void UpdateVisuals()
        {
            if (!_seeded || config == null) return;
            EnsureVisuals();
            if (!_root) return;
            if (!_root.activeSelf) _root.SetActive(true);

            float dt = Time.deltaTime;
            Vector3 ship = _status.Transform.position;
            Vector3 ballPos = _solver.BallPosition;
            float speed = _solver.BallSpeed;
            float r = _settings.BallRadius * config.BallVisualScale;

            // Ball: position, roll, colour.
            _ball.position = ballPos;
            _ball.localScale = Vector3.one * (2f * r);
            Vector3 v = _solver.BallVelocity;
            if (v.sqrMagnitude > 1e-4f && r > 1e-4f)
            {
                Vector3 axis = Vector3.Cross(ship - ballPos, v);
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(Vector3.up, v);
                // Capped: a white-hot ball truly rolls ~7000 deg/s, which at 60 fps strobes the studs
                // backwards (wagon-wheel) — read as jitter. The cap keeps the roll legible.
                float rollDegPerSec = Mathf.Min(speed / r * Mathf.Rad2Deg, config.BallMaxVisualSpin);
                if (axis.sqrMagnitude > 1e-6f)
                    _ball.rotation = Quaternion.AngleAxis(rollDegPerSec * dt, axis.normalized) * _ball.rotation;
            }

            Color ballColor = BallColor(out float emission);
            BallShade(out Color body, out Color rim);
            _mpb.Clear();
            _mpb.SetColor(DarkColorId, body);
            _mpb.SetColor(BrightColorId, rim);
            // The runtime fallback material (no ball material assigned) is URP/Unlit.
            _mpb.SetColor(BaseColorId, ballColor);
            _mpb.SetColor(ColorId, ballColor);
            _mpb.SetColor(EmissionColorId, ballColor * emission);
            for (int i = 0; i < _ballRenderers.Length; i++)
                if (_ballRenderers[i]) _ballRenderers[i].SetPropertyBlock(_mpb);

            // Chain: the gameplay links, drawn as a fresnel tube at the width that CUTS. Iron body;
            // the rim takes the ball's hue, brighter while taut (it is slicing), lime-flickering at
            // READY.
            _chain.startWidth = _chain.endWidth = 2f * config.ChainCutRadius;
            ChainShade(out Color chainBody, out Color chainRim);
            _chainMpb.Clear();
            _chainMpb.SetColor(DarkColorId, chainBody);
            _chainMpb.SetColor(BrightColorId, chainRim);
            _chain.SetPropertyBlock(_chainMpb);
            _chain.startColor = _chain.endColor = chainRim;   // the vertex-colour fallback material
            if (_chain.positionCount != _links.Count) _chain.positionCount = _links.Count;
            _chain.SetPositions(_links.Points);

            // Skid trail while planting.
            if (_skid)
            {
                _skid.emitting = _solver.Mode == ThresherMode.Skidding;
                _skid.startWidth = r * 1.4f;
                _skid.endWidth = 0f;
                _skid.startColor = ballColor;
                _skid.endColor = new Color(ballColor.r, ballColor.g, ballColor.b, 0f);
            }

            DrawCombo(ballPos, r, dt, ballColor);
        }

        void DrawCombo(Vector3 ballPos, float r, float dt, Color ballColor)
        {
            if (!_comboLabel) return;
            if (_combo >= 2)
            {
                if (_combo != _comboShown)
                {
                    _comboShown = _combo;
                    _comboLabel.text = "x" + _combo;
                    _comboShownScale = 1.6f;   // pop on each new hit
                }
                _comboShownScale = Mathf.MoveTowards(_comboShownScale, 1f, dt * 4f);
                _comboLabel.fontSize = r * 2.2f * (1f + 0.08f * Mathf.Min(_combo, 12)) * _comboShownScale;
                _comboLabel.color = ballColor;
                _comboLabel.transform.position = ballPos + Vector3.up * (r * 3f);
                var cam = Camera.main;
                if (cam)
                {
                    Vector3 away = _comboLabel.transform.position - cam.transform.position;
                    if (away.sqrMagnitude > 1e-6f)
                        _comboLabel.transform.rotation = Quaternion.LookRotation(away, cam.transform.up);
                }
                _comboLabel.enabled = true;
            }
            else
            {
                _comboShown = 0;
                _comboLabel.enabled = false;
            }
        }
    }
}
