using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using FMODUnity;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Thresher's wrecking ball: owns the chain (<see cref="ThresherChainSolver"/>), smashes prisms
    /// along the ball's swept path, and draws and voices all of it.
    ///
    /// <b>One step per frame, from inside the move step.</b> <see cref="ThresherVesselTransformer"/>
    /// calls <see cref="StepChain"/> through the base transformer's external-acceleration seam, so
    /// the rope is resolved in the same frame and order as thrust. If no transformer stepped it this
    /// frame (a hull whose transformer is switched off — a remote replica in a pilot swap), the
    /// executor steps itself in <see cref="LateUpdate"/> from the hull's observed motion, so the
    /// ball never freezes in mid-air on anyone's screen.
    ///
    /// <b>Smashing.</b> Every frame the ball's path from where it WAS to where it IS is swept as a
    /// sphere of <c>BallRadius</c> through <see cref="PrismSpatialIndex.QuerySegment"/> — not a
    /// collider, which a fast ball would tunnel through and which prism collider LOD switches off
    /// away from vessels — and the hits are resolved nearest-first. At smash speed or faster a hit
    /// goes through the normal prism damage path, <see cref="Prism.Damage"/>, with the ball's OWN
    /// velocity as the impact vector (the Rhino sword's contact-velocity idea, reduced to its core:
    /// a point mass's contact velocity IS its velocity), so shields pop and super-shields hold
    /// exactly as they do for every other vessel and the debris flies along the ball's path. The
    /// ball then keeps <c>plough</c> of its speed. Slower, it CHIPS: the ball passes through, and
    /// three chips break the prism. Each prism is hit once per contact, so a shielded one takes a
    /// second pass, as it would from any other hull.
    ///
    /// <b>Networking: none of its own,</b> deliberately — the same shape as the Gibbon's tether.
    /// Press and release round-trip through <c>R_VesselActionHandler</c>, so both edges run on every
    /// peer, and the hull's motion replicates; each peer runs the same chain from the same inputs.
    /// A peer's ball can differ by a frame of drift, so its smashes can differ at the margin.
    /// </summary>
    public sealed class ThresherExecutor : ShipActionExecutorBase
    {
        [Tooltip("Every Thresher dial: chain physics, impact feel, look.")]
        [SerializeField] ThresherConfigSO config;

        [Header("Audio (LOCKED FMOD convention: ship EMPTY, never a borrowed event)")]
        [Tooltip("Heavy thud on a smash.")]
        [SerializeField] EventReference smashEvent;
        [Tooltip("Chip: a too-slow ball nicking a prism.")]
        [SerializeField] EventReference chipEvent;
        [Tooltip("Clunk when the ball plants and becomes the pivot.")]
        [SerializeField] EventReference plantEvent;
        [Tooltip("Yank when the left trigger releases the pivot and the ball is dragged after the ship.")]
        [SerializeField] EventReference yankEvent;

        [Header("Materials (optional - runtime fallbacks when empty)")]
        [Tooltip("Ball material. Its colour is driven per frame through a MaterialPropertyBlock (_BaseColor / _Color).")]
        [SerializeField] Material ballMaterial;
        [Tooltip("Chain, gauge ring and skid-trail material. Must honour vertex colour (Sprites/Default does).")]
        [SerializeField] Material lineMaterial;

        IVesselStatus _status;
        ActionExecutorRegistry _registry;
        ThresherChainSolver _solver;
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
        static bool s_timeScaleHitStopActive;
        readonly List<Prism> _candidates = new List<Prism>(64);
        readonly List<(float t, Prism prism)> _hits = new List<(float, Prism)>(32);
        HashSet<Prism> _touching = new HashSet<Prism>();
        HashSet<Prism> _touchingNext = new HashSet<Prism>();
        readonly Dictionary<Prism, (float stamp, int count)> _chips = new Dictionary<Prism, (float, int)>();

        CancellationTokenSource _cts;

        // ------------------------------------------------------------------ public surface

        public bool IsPivoting => _solver != null && _solver.Mode == ThresherMode.Pivot;
        public ThresherMode Mode => _solver?.Mode ?? ThresherMode.Free;
        /// <summary>True while a MULTIPLAYER hit-stop is holding this hull and its ball still.</summary>
        public bool IsVesselFrozen => Time.unscaledTime < _freezeUntilUnscaled;
        public bool IsReady { get; private set; }
        public float BallSpeed => _solver?.BallSpeed ?? 0f;
        public Vector3 BallPosition => _solver?.BallPosition ?? transform.position;
        /// <summary>Ball speed as a fraction of smash speed — the gauge ring's fill.</summary>
        public float Gauge01 => _settings.SmashSpeed > 0f ? Mathf.Clamp01(BallSpeed / _settings.SmashSpeed) : 0f;
        public int Combo => _combo;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _registry = GetComponentInParent<ActionExecutorRegistry>(true);
            RebuildSolver();
            _seeded = false;
            _payOut = false;
            _plantHeld = false;
            _combo = 0;
            _chips.Clear();
            _touching.Clear();
            _touchingNext.Clear();
        }

        void RebuildSolver()
        {
            _settings = config ? config.Dials.ToSettings() : new ThresherDials().ToSettings();
            if (_solver == null) _solver = new ThresherChainSolver(_settings);
            else _solver.Settings = _settings;
        }

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
            _solver.Release(_lastShipVelocity);
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
            SetVisualsActive(false);
        }

        void OnDestroy()
        {
            _cts?.Dispose();
            _cts = null;
            DestroyVisuals();
        }

        // ------------------------------------------------------------------ the step

        /// <summary>
        /// One chain step. Called by <see cref="ThresherVesselTransformer"/> from inside its move
        /// step; returns the ship's velocity after the rope (towing) or the orbit (pivot).
        /// </summary>
        public Vector3 StepChain(Vector3 shipPosition, Vector3 shipVelocity, float shipCruise, float dt)
        {
            if (_solver == null) RebuildSolver();
            EnsureSeeded(shipPosition, shipVelocity);

            var result = _solver.Step(shipPosition, shipVelocity, _payOut, shipCruise, dt);
            _lastStepFrame = Time.frameCount;
            _lastShipPosition = shipPosition;
            _lastShipVelocity = result.ShipVelocity;

            if (result.Planted) PlayAt(plantEvent, _solver.Pivot);
            if (_solver.Mode != ThresherMode.Pivot)
                SweepAndSmash(result.BallFrom, _solver.BallPosition);

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
        }

        // ------------------------------------------------------------------ smashing

        void SweepAndSmash(Vector3 from, Vector3 to)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;

            float radius = _settings.BallRadius;
            // Candidates by CENTRE, with an allowance for the prism's own extent; refined below.
            const float candidateExtent = 8f;
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
                if (ThresherChainSolver.IsSmash(speed, _settings))
                {
                    Smash(prism, at, speed);
                    _solver.RegisterHit(ThresherChainSolver.KeepAfterHit(speed, _settings));
                }
                else if (prism.Domain != _status.Domain)
                {
                    // A lazy tow must not grind your own team's mass away; chips are for rivals.
                    Chip(prism, at);
                    _solver.RegisterHit(_settings.ChipKeep);
                }
            }

            (_touching, _touchingNext) = (_touchingNext, _touching);
        }

        void Smash(Prism prism, Vector3 at, float speed)
        {
            Vector3 impact = _solver.BallVelocity * (config ? config.DebrisRestitution : 1f / 3f);
            prism.Damage(impact, _status.Domain, _status.PlayerName,
                         debrisSpeedLimit: config ? config.DebrisSpeedLimit : 200f);
            _chips.Remove(prism);

            float heat = ThresherChainSolver.Heat01(speed, _settings);
            _combo++;
            _coolTime = 0f;

            PlayAt(smashEvent, at);
            if (!IsLocalPilot()) return;

            if (config)
            {
                if (_combo == 1)
                    HitStop(Mathf.Lerp(config.HitStopMinSeconds, config.HitStopMaxSeconds, heat));
                Shake(Mathf.Lerp(config.ShakeMin, config.ShakeMax, heat) * (_combo == 1 ? 1f : 0.6f), config.ShakeSeconds);
                HapticController.PlaySkim(Mathf.Lerp(config.HapticFloor01, 1f, heat));
            }
        }

        void Chip(Prism prism, Vector3 at)
        {
            float stamp = prism.prismProperties != null ? prism.prismProperties.TimeCreated : 0f;
            int count = _chips.TryGetValue(prism, out var rec) && Mathf.Approximately(rec.stamp, stamp)
                ? rec.count + 1
                : 1;

            int toBreak = config ? config.Dials.ChipsToBreak : 3;
            if (count >= toBreak)
            {
                _chips.Remove(prism);
                prism.Damage(_solver.BallVelocity * (config ? config.DebrisRestitution : 1f / 3f),
                             _status.Domain, _status.PlayerName,
                             debrisSpeedLimit: config ? config.DebrisSpeedLimit : 200f);
            }
            else
            {
                _chips[prism] = (stamp, count);
            }

            if (_chips.Count > 256) PruneChips();

            PlayAt(chipEvent, at);
            if (config && IsLocalPilot())
                Shake(config.ShakeMin * config.ChipShakeFraction, config.ShakeSeconds * 0.5f);
        }

        void PruneChips()
        {
            var dead = new List<Prism>();
            foreach (var kv in _chips)
                if (!kv.Key || kv.Key.destroyed ||
                    kv.Key.prismProperties == null ||
                    !Mathf.Approximately(kv.Key.prismProperties.TimeCreated, kv.Value.stamp))
                    dead.Add(kv.Key);
            foreach (var p in dead) _chips.Remove(p);
        }

        void UpdateCombo(float dt)
        {
            if (_combo == 0) return;
            if (ThresherChainSolver.IsSmash(_solver.BallSpeed, _settings)) { _coolTime = 0f; return; }
            _coolTime += dt;
            if (_coolTime >= (config ? config.ComboGraceSeconds : 0.4f)) _combo = 0;
        }

        // ------------------------------------------------------------------ feedback

        bool IsLocalPilot()
            => _status?.Player != null && _status.IsLocalUser && !_status.AutoPilotEnabled;

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

        // ------------------------------------------------------------------ visuals

        // All runtime-built and cosmetic; explicitly a prototype look, replaced wholesale when art
        // lands. Every peer draws its own copy.
        GameObject _root;
        Transform _ball;
        MeshRenderer[] _ballRenderers;
        LineRenderer _chain;
        LineRenderer _gauge;
        TrailRenderer _skid;
        TextMeshPro _comboLabel;
        MaterialPropertyBlock _mpb;
        Vector3[] _links, _linksPrev;
        float _comboShownScale;
        int _comboShown;

        static Material s_lineMaterial, s_ballMaterial;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        const int GaugeSegments = 48;

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

            _chain = MakeLine("Chain", lineMat, false);
            _gauge = MakeLine("Gauge", lineMat, true);

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

            int n = Mathf.Max(2, config ? config.ChainLinks : 12) + 1;
            _links = new Vector3[n];
            _linksPrev = new Vector3[n];
            _chain.positionCount = n;
            _linksSeeded = false;
        }

        bool _linksSeeded;

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
            float heat = ThresherChainSolver.Heat01(speed, _settings);
            float r = _settings.BallRadius * config.BallVisualScale;

            // Ball: position, roll, heat colour.
            _ball.position = ballPos;
            _ball.localScale = Vector3.one * (2f * r);
            Vector3 v = _solver.BallVelocity;
            if (v.sqrMagnitude > 1e-4f && r > 1e-4f)
            {
                Vector3 axis = Vector3.Cross(ship - ballPos, v);
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(Vector3.up, v);
                if (axis.sqrMagnitude > 1e-6f)
                    _ball.rotation = Quaternion.AngleAxis(speed / r * Mathf.Rad2Deg * dt, axis.normalized) * _ball.rotation;
            }

            Color ballColor = HeatColor(speed);
            _mpb.Clear();
            _mpb.SetColor(BaseColorId, ballColor);
            _mpb.SetColor(ColorId, ballColor);
            _mpb.SetColor(EmissionColorId, ballColor * Mathf.Lerp(0f, 3f, Gauge01 * Gauge01 + heat));
            for (int i = 0; i < _ballRenderers.Length; i++)
                if (_ballRenderers[i]) _ballRenderers[i].SetPropertyBlock(_mpb);

            // Chain: cosmetic verlet links pinned to the hull and the ball.
            StepChainVisual(ship, ballPos, dt);
            float width = _settings.BallRadius * config.ChainWidthFraction;
            _chain.startWidth = _chain.endWidth = width;
            Color chainColor = config.IronColor * 2.2f;
            chainColor.a = 1f;
            if (IsReady)
            {
                bool on = Mathf.Repeat(Time.time * config.ReadyFlickerHz, 1f) < 0.5f;
                chainColor = on ? config.GoldColor : Color.Lerp(chainColor, config.GoldColor, 0.35f);
            }
            _chain.startColor = _chain.endColor = chainColor;
            _chain.SetPositions(_links);

            // Gauge ring: fills toward smash speed, billboarded to the camera.
            DrawGauge(ballPos, r * config.GaugeRadiusScale, heat);

            // Skid trail while planting.
            if (_skid)
            {
                _skid.emitting = _solver.Mode == ThresherMode.Skidding;
                _skid.startWidth = r * 1.4f;
                _skid.endWidth = 0f;
                _skid.startColor = config.GoldColor;
                _skid.endColor = new Color(config.GoldColor.r, config.GoldColor.g, config.GoldColor.b, 0f);
            }

            DrawCombo(ballPos, r, dt);
        }

        Color HeatColor(float speed)
        {
            float smash = Mathf.Max(1e-3f, _settings.SmashSpeed);
            if (speed < smash)
            {
                // Iron until half smash speed, then warming through a dull ember to gold.
                float warm = Mathf.InverseLerp(0.5f * smash, smash, speed);
                Color ember = Color.Lerp(config.IronColor, new Color(0.55f, 0.12f, 0.03f), warm);
                return Color.Lerp(ember, config.GoldColor, warm * warm);
            }
            return Color.Lerp(config.GoldColor, config.WhiteHotColor, ThresherChainSolver.Heat01(speed, _settings));
        }

        void StepChainVisual(Vector3 a, Vector3 b, float dt)
        {
            int n = _links.Length;
            if (!_linksSeeded)
            {
                for (int i = 0; i < n; i++)
                    _links[i] = _linksPrev[i] = Vector3.Lerp(a, b, i / (float)(n - 1));
                _linksSeeded = true;
            }

            // Verlet with light damping (space has no gravity: a slack chain just drifts).
            const float damping = 0.92f;
            for (int i = 1; i < n - 1; i++)
            {
                Vector3 p = _links[i];
                _links[i] += (p - _linksPrev[i]) * damping;
                _linksPrev[i] = p;
            }

            float seg = _solver.Length / (n - 1);
            for (int iter = 0; iter < 6; iter++)
            {
                _links[0] = a;
                _links[n - 1] = b;
                for (int i = 0; i < n - 1; i++)
                {
                    Vector3 d = _links[i + 1] - _links[i];
                    float len = d.magnitude;
                    if (len <= seg || len < 1e-5f) continue;   // max-distance only: links can go slack
                    Vector3 corr = d * ((len - seg) / len * 0.5f);
                    if (i != 0) _links[i] += corr;
                    if (i + 1 != n - 1) _links[i + 1] -= corr;
                }
            }
            _links[0] = a;
            _links[n - 1] = b;
        }

        void DrawGauge(Vector3 centre, float radius, float heat)
        {
            float fill = Gauge01;
            int count = Mathf.Max(2, Mathf.RoundToInt(GaugeSegments * fill) + 1);
            _gauge.loop = fill >= 0.999f;
            _gauge.positionCount = count;
            var cam = Camera.main;
            Vector3 right = cam ? cam.transform.right : Vector3.right;
            Vector3 up = cam ? cam.transform.up : Vector3.up;
            for (int i = 0; i < count; i++)
            {
                // Clockwise from twelve o'clock, sweeping `fill` of a full turn.
                float a = Mathf.PI * 0.5f - (i / (float)(count - 1)) * fill * Mathf.PI * 2f;
                _gauge.SetPosition(i, centre + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius);
            }
            float w = _settings.BallRadius * 0.12f;
            _gauge.startWidth = _gauge.endWidth = w;
            Color c = fill >= 1f
                ? Color.Lerp(config.GoldColor, config.WhiteHotColor, heat)
                : Color.Lerp(new Color(0.5f, 0.45f, 0.4f, 0.5f), config.GoldColor, fill * fill);
            _gauge.startColor = _gauge.endColor = c;
            _gauge.enabled = fill > 0.02f;
        }

        void DrawCombo(Vector3 ballPos, float r, float dt)
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
                float size = r * 2.2f * (1f + 0.08f * Mathf.Min(_combo, 12)) * _comboShownScale;
                _comboLabel.fontSize = size;
                _comboLabel.color = Color.Lerp(config.GoldColor, config.WhiteHotColor,
                                               ThresherChainSolver.Heat01(_solver.BallSpeed, _settings));
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
