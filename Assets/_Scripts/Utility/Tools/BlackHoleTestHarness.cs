#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility.PerformanceBenchmark;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Black hole test rig (Docs/BLACK_HOLE.md §7) — the gravity counterpart of
    /// <see cref="PrismGridExplosionHarness"/>, from which its lay path and its panel are taken.
    ///
    /// Lays a parameterised field of REAL prisms centred on the origin — a cuboid lattice or the
    /// spheroid inscribed in it, jittered — then either parks a black hole at the centre and
    /// watches the field orbit, warp and drain into it, or drives a hole THROUGH the field along
    /// +X and watches it drag mass along, bend what it passes and swallow what it reaches. The
    /// physics and the warp are the shipped systems reading the shipped <c>BlackHoleConfig</c>;
    /// nothing here is a special case, so what this scene shows is what a hole does anywhere.
    ///
    /// Ecosystem invariants (CLAUDE.md): prisms grow in via the normal <c>Prism.Initialize()</c>
    /// path and leave only by an ACTIVE force — a horizon, or an explicit operator Clear. No TTL,
    /// no decay, no idle culler.
    ///
    /// Readout is the standard <c>DiagnosticsHUD</c>; this component publishes rows into its
    /// "BlackHoleTest" section and registers a <c>bhtest</c> console command beside the global
    /// <c>blackhole</c> one (<c>BlackHoleConsole</c>), which also works here for anything the
    /// buttons do not cover.
    /// </summary>
    public class BlackHoleTestHarness : MonoBehaviour
    {
        const string StatsSection = "BlackHoleTest";
        const string CommandName = "bhtest";
        const string OwnerPrefix = "BlackHoleTest";
        const string FieldRootName = "[BlackHoleField]";
        const int HusksScannedPerFrame = 512;
        const float SuctionScale = 0.002f;
        const float MaterializeStallSeconds = 2f;
        const float WarningSeconds = 6f;

        enum FieldPhase
        {
            Idle = 0,
            Laying = 1,
            Materializing = 2,
            Ready = 3,
        }

        [Header("Configuration")]
        [Tooltip("Tunables asset. When empty the harness falls back to any BlackHoleTestConfigSO in " +
                 "Resources, so the rig still runs if the scene reference is lost.")]
        [SerializeField] private BlackHoleTestConfigSO config;

        [Tooltip("Camera framing the field. Falls back to Camera.main when empty. It is driven by a " +
                 "MouseOrbitCamera (right-drag pan, left-drag orbit, wheel / middle-drag zoom), added at " +
                 "runtime if the camera does not carry one.")]
        [SerializeField] private Camera viewCamera;

        Trail _trail;
        Transform _fieldRoot;
        readonly List<Prism> _prisms = new();
        CancellationTokenSource _spawnCts;
        FieldPhase _phase = FieldPhase.Idle;
        int _laid;
        int _requested;
        int _sweepCursor;
        bool _holdingLoadGate;

        BlackHoleTestConfigSO.FieldShape _shape;
        Vector3Int _counts;
        Vector3 _gaps;
        float _zoom;
        MouseOrbitCamera _orbit;

        BlackHole _flyHole;
        float _flyDespawnX;

        Font _font;
        InputField _countInput, _gapInput, _strengthInput;
        Slider _zoomSlider;
        Text _readout;
        string _warning;
        float _warningUntil;

        public bool IsReady => _phase == FieldPhase.Ready;
        public int LivePrismCount => _prisms.Count;

        Vector3 Extents => new(
            Mathf.Max(0, _counts.x - 1) * _gaps.x,
            Mathf.Max(0, _counts.y - 1) * _gaps.y,
            Mathf.Max(0, _counts.z - 1) * _gaps.z);

        void Awake()
        {
            if (config == null)
                config = Resources.Load<BlackHoleTestConfigSO>("BlackHoleTestConfig");
            if (config == null)
            {
                Debug.LogError("[BlackHoleTestHarness] No BlackHoleTestConfigSO assigned or found in Resources. " +
                               "Run FrogletTools > Scene Setup > Setup Black Hole Test Scene.");
                enabled = false;
                return;
            }

            _shape = config.DefaultShape;
            _counts = config.DefaultCounts;
            _gaps = config.DefaultGaps;
            _zoom = config.DefaultZoom;
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera != null && !viewCamera.TryGetComponent(out _orbit))
                _orbit = viewCamera.gameObject.AddComponent<MouseOrbitCamera>();

            BuildUI();
            ApplyZoom(reframe: true, snap: true);
        }

        void Start()
        {
            // The lay path hard-depends on a populated theme (Prism.ChangeTeam → domain material
            // lookup); in gameplay ThemeManager lives in Bootstrap, which this scene skips.
            if (FindFirstObjectByType<ThemeManager>() == null)
                Warn("No ThemeManager in scene — Spawn will fail on the first prism. " +
                     "Re-run FrogletTools > Scene Setup > Setup Black Hole Test Scene.");

            DiagnosticsHUD.RegisterCommand(CommandName, HandleCommand);
            PublishStats();
        }

        void Update()
        {
            SyncZoomSliderFromCamera();

            if (_phase == FieldPhase.Ready) ReclaimHusks();

            // The fly-through hole retires itself once it is past the far edge.
            if (_flyHole != null && !_flyHole.IsDespawning && _flyHole.transform.position.x >= _flyDespawnX)
            {
                _flyHole.BeginDespawn();
                _flyHole = null;
                PublishStats();
            }
        }

        void OnDestroy()
        {
            CancelSpawn();
            DiagnosticsHUD.UnregisterCommand(CommandName);
            DiagnosticsHUD.ClearStats(StatsSection);
        }

        /// <summary>
        /// Frees the GameObjects of prisms a horizon already consumed, a bounded window per frame.
        /// The live count converges over a second rather than snapping — a readout, not gameplay.
        /// </summary>
        void ReclaimHusks()
        {
            if (_prisms.Count == 0) { _sweepCursor = 0; return; }
            int scans = Mathf.Min(HusksScannedPerFrame, _prisms.Count);
            bool changed = false;
            for (int n = 0; n < scans; n++)
            {
                if (_sweepCursor >= _prisms.Count) _sweepCursor = 0;
                var prism = _prisms[_sweepCursor];
                if (prism != null && !prism.destroyed) { _sweepCursor++; continue; }
                int last = _prisms.Count - 1;
                _prisms[_sweepCursor] = _prisms[last];
                _prisms.RemoveAt(last);
                if (prism != null) Destroy(prism.gameObject);
                changed = true;
            }
            if (changed) PublishStats();
        }

        int IndexedCount()
        {
            int n = 0;
            for (int i = 0; i < _prisms.Count; i++)
            {
                var prism = _prisms[i];
                if (prism != null && !prism.destroyed && prism.SpatialIndexId >= 0) n++;
            }
            return n;
        }

        // ── Field geometry ───────────────────────────────────────────────────

        /// <summary>
        /// Lattice sites centred on the origin, cut to the chosen shape, jittered. The spheroid
        /// keeps a site when its normalised radius (per-axis against the half-extent) is ≤ 1.
        /// </summary>
        List<PrismLay> BuildLays()
        {
            var scale = config.PrismScale == Vector3.zero
                ? config.PrismPrefab.transform.localScale
                : config.PrismScale;
            var half = new Vector3((_counts.x - 1) * 0.5f, (_counts.y - 1) * 0.5f, (_counts.z - 1) * 0.5f);
            var halfExtent = new Vector3(
                Mathf.Max(half.x * _gaps.x, 0.5f * _gaps.x),
                Mathf.Max(half.y * _gaps.y, 0.5f * _gaps.y),
                Mathf.Max(half.z * _gaps.z, 0.5f * _gaps.z));
            float jitter = config.Jitter;
            var rng = new System.Random(20261007);

            var lays = new List<PrismLay>();
            for (int x = 0; x < _counts.x; x++)
            for (int y = 0; y < _counts.y; y++)
            for (int z = 0; z < _counts.z; z++)
            {
                var pos = new Vector3((x - half.x) * _gaps.x, (y - half.y) * _gaps.y, (z - half.z) * _gaps.z);
                if (_shape == BlackHoleTestConfigSO.FieldShape.Spheroid)
                {
                    var n = new Vector3(pos.x / halfExtent.x, pos.y / halfExtent.y, pos.z / halfExtent.z);
                    if (n.sqrMagnitude > 1f) continue;
                }
                if (jitter > 0f)
                {
                    pos += new Vector3(
                        ((float)rng.NextDouble() * 2f - 1f) * jitter * _gaps.x,
                        ((float)rng.NextDouble() * 2f - 1f) * jitter * _gaps.y,
                        ((float)rng.NextDouble() * 2f - 1f) * jitter * _gaps.z);
                }
                var rot = config.RandomRotation
                    ? Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f)
                    : Quaternion.identity;
                lays.Add(new PrismLay(new SpawnPoint(pos, rot, scale), config.FieldDomain));
            }
            return lays;
        }

        /// <summary>Site count after the shape cut, without laying anything (for the readout).</summary>
        long ProjectedCount()
        {
            long total = (long)_counts.x * _counts.y * _counts.z;
            if (_shape == BlackHoleTestConfigSO.FieldShape.Cuboid) return total;
            // The inscribed ellipsoid holds pi/6 of its bounding box.
            return (long)Math.Round(total * Math.PI / 6.0);
        }

        // ── Spawn ────────────────────────────────────────────────────────────

        public void Spawn()
        {
            if (config.PrismPrefab == null) { Warn("No prism prefab configured."); return; }
            long total = (long)_counts.x * _counts.y * _counts.z;
            if (total <= 0) { Warn("Counts must all be >= 1."); return; }
            if (total > config.MaxTotalPrisms)
            {
                Warn($"{total:N0} sites exceeds the {config.MaxTotalPrisms:N0} cap (BlackHoleTestConfig.maxTotalPrisms).");
                return;
            }
            Clear();
            SpawnAsync().Forget();
        }

        void ReleaseLoadGate()
        {
            if (!_holdingLoadGate) return;
            _holdingLoadGate = false;
            PrismTrailBuilder.SetLoadGateHolding(false);
        }

        async UniTaskVoid SpawnAsync()
        {
            CancelSpawn();
            _spawnCts = new CancellationTokenSource();
            var ct = _spawnCts.Token;

            // The load gate raises Prism's creation-completion budget so the field materializes in
            // seconds, not minutes — the whole scene IS the loading screen until Ready.
            PrismTrailBuilder.SetLoadGateHolding(true);
            _holdingLoadGate = true;

            var lays = BuildLays();
            _requested = lays.Count;
            _laid = 0;
            _phase = FieldPhase.Laying;

            _fieldRoot = new GameObject(FieldRootName).transform;
            _fieldRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _trail = new Trail();
            PublishStats();

            try
            {
                TrackProgress(ct).Forget();
                await PrismTrailBuilder.LayBatched(config.PrismPrefab, lays, _fieldRoot, _trail,
                    OwnerPrefix, config.PrismsPerFrame, ct, _prisms);
                _laid = _prisms.Count;
                _phase = FieldPhase.Materializing;
                await WaitForMaterializationAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // Cancelled by Clear / disable — the partial field is left for the caller to clear.
            }
            catch (Exception e)
            {
                Warn($"Lay FAILED after {_prisms.Count:N0}/{_requested:N0}: {e.GetType().Name}: {e.Message}");
                Debug.LogException(e, this);
            }
            finally
            {
                if (_spawnCts != null && _spawnCts.Token == ct)
                {
                    _phase = FieldPhase.Ready;
                    _laid = _prisms.Count;
                    ReleaseLoadGate();
                }
                PublishStats();
            }
        }

        async UniTask WaitForMaterializationAsync(CancellationToken ct)
        {
            int lastCount = -1;
            float stalled = 0f;
            while (IndexedCount() < _requested)
            {
                int current = IndexedCount();
                if (current != lastCount) { lastCount = current; stalled = 0f; }
                else
                {
                    stalled += Time.unscaledDeltaTime;
                    if (stalled >= MaterializeStallSeconds) return;
                }
                PublishStats();
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        async UniTaskVoid TrackProgress(CancellationToken ct)
        {
            try
            {
                while (_phase == FieldPhase.Laying && !ct.IsCancellationRequested)
                {
                    _laid = _prisms.Count;
                    PublishStats();
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException) { }
        }

        void CancelSpawn()
        {
            ReleaseLoadGate();
            if (_spawnCts == null) return;
            if (!_spawnCts.IsCancellationRequested) _spawnCts.Cancel();
            _spawnCts.Dispose();
            _spawnCts = null;
            _phase = FieldPhase.Idle;
        }

        // ── Black holes ──────────────────────────────────────────────────────

        /// <summary>Park a hole at the field's centre.</summary>
        public BlackHole SpawnCentreHole(float strength)
        {
            if (_phase != FieldPhase.Ready)
                Warn($"Field is {_phase.ToString().ToLowerInvariant()}: only {IndexedCount():N0} of {_requested:N0} prisms are indexed and can be pulled.");
            var hole = BlackHoleRegistry.Spawn(Vector3.zero, strength, Vector3.zero, config.SpinAxis);
            PublishStats();
            return hole;
        }

        /// <summary>Drive a hole through the field along +X, from one margin past the far edge to the other.</summary>
        public BlackHole FlyThrough(float strength, float speed)
        {
            if (_flyHole != null && !_flyHole.IsDespawning)
            {
                _flyHole.BeginDespawn();
                _flyHole = null;
            }
            float halfX = Extents.x * 0.5f + config.FlyMargin;
            var start = new Vector3(-halfX, 0f, 0f);
            _flyDespawnX = halfX;
            _flyHole = BlackHoleRegistry.Spawn(start, strength, new Vector3(speed, 0f, 0f), config.SpinAxis);
            PublishStats();
            return _flyHole;
        }

        public void DespawnHoles()
        {
            BlackHoleRegistry.DespawnAll();
            _flyHole = null;
            PublishStats();
        }

        // ── Clear ────────────────────────────────────────────────────────────

        /// <summary>
        /// Operator teardown: suction the field toward the origin and free it, mirroring the grid
        /// rig's sanctioned continuity transition. Not per-prism Damage — that would mint one VFX
        /// per prism.
        /// </summary>
        public void Clear()
        {
            CancelSpawn();
            var outgoingRoot = _fieldRoot;
            var outgoing = new List<Prism>(_prisms);
            _prisms.Clear();
            _trail = null;
            _fieldRoot = null;
            _laid = 0;
            _requested = 0;
            PublishStats();
            SuctionAndFreeAsync(outgoingRoot, outgoing).Forget();
        }

        async UniTaskVoid SuctionAndFreeAsync(Transform root, List<Prism> prisms)
        {
            if (root == null) return;
            root.gameObject.name = FieldRootName + " (clearing)";
            float seconds = config.ClearSeconds;
            if (seconds > 0f && prisms.Count > 0)
            {
                float elapsed = 0f;
                while (elapsed < seconds && root != null)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / seconds);
                    float eased = t * t * (3f - 2f * t);
                    root.localScale = Vector3.one * Mathf.LerpUnclamped(1f, SuctionScale, eased);
                    for (int i = 0; i < prisms.Count; i++)
                    {
                        var prism = prisms[i];
                        if (prism) prism.NotifyPositionChanged();
                    }
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }
            if (root != null) Destroy(root.gameObject);
        }

        // ── Camera ───────────────────────────────────────────────────────────

        public void SetZoom(float value)
        {
            _zoom = Mathf.Clamp01(value);
            if (_zoomSlider != null) _zoomSlider.SetValueWithoutNotify(_zoom);
            ApplyZoom(reframe: false, snap: false);
        }

        /// <summary>Back to the home view: looking at the field's centre along +Z, at the slider's distance.</summary>
        public void FrameView()
        {
            ApplyZoom(reframe: true, snap: false);
        }

        float FarDistance() => Mathf.Max(config.NearDistance + 1f,
            Mathf.Max(Extents.x, Extents.y, Extents.z) * config.FarDistanceMultiplier);

        float DistanceForZoom(float zoom01) => Mathf.Lerp(config.NearDistance, FarDistance(), zoom01);

        /// <summary>
        /// Frame the field. The camera's transform belongs to the <see cref="MouseOrbitCamera"/>, so
        /// the harness only ever says WHERE home is and HOW FAR to sit: <paramref name="reframe"/>
        /// re-homes on the field's centre and returns there (spawn, resize, F); otherwise only the
        /// distance changes and the player's pan and angles are kept (the zoom slider).
        /// </summary>
        void ApplyZoom(bool reframe, bool snap)
        {
            if (viewCamera == null) return;
            float dist = DistanceForZoom(_zoom);
            if (_orbit != null)
            {
                _orbit.SetHome(Vector3.zero, dist);
                if (reframe) _orbit.FrameHome(snap);
                else _orbit.SetDistance(dist, snap);
            }
            else
            {
                viewCamera.transform.position = new Vector3(0f, 0f, -dist);
                viewCamera.transform.LookAt(Vector3.zero);
            }
            if (viewCamera.farClipPlane < dist + Extents.magnitude)
                viewCamera.farClipPlane = dist + Extents.magnitude + 1000f;
        }

        /// <summary>The wheel and middle-drag zoom too; keep the slider showing where the camera is.</summary>
        void SyncZoomSliderFromCamera()
        {
            if (_orbit == null || _zoomSlider == null) return;
            float near = config.NearDistance, far = FarDistance();
            float z = Mathf.Clamp01(Mathf.InverseLerp(near, far, _orbit.Distance));
            if (Mathf.Abs(z - _zoom) < 1e-3f) return;
            _zoom = z;
            _zoomSlider.SetValueWithoutNotify(z);
        }

        // ── Diagnostics ──────────────────────────────────────────────────────

        void PublishStats()
        {
            DiagnosticsHUD.SetStat(StatsSection, "field", $"{_shape.ToString().ToLowerInvariant()} {_counts.x}x{_counts.y}x{_counts.z} @ {_gaps.x:F0}");
            DiagnosticsHUD.SetStat(StatsSection, "phase", _phase.ToString().ToLowerInvariant());
            DiagnosticsHUD.SetStat(StatsSection, "laid", $"{_laid:N0}/{_requested:N0}");
            DiagnosticsHUD.SetStat(StatsSection, "indexed", $"{IndexedCount():N0}/{_requested:N0}");
            DiagnosticsHUD.SetStat(StatsSection, "holes", $"{BlackHoleRegistry.Count} live");
            DiagnosticsHUD.SetStat(StatsSection, "bodies", $"{BlackHoleGravityField.BodyCount:N0} under gravity");
            DiagnosticsHUD.SetStat(StatsSection, "captured", $"{BlackHoleGravityField.CapturedTotal:N0}");
            DiagnosticsHUD.SetStat(StatsSection, "warp", $"{BlackHoleWarp.LiveSlotCount} holes stretching");
            UpdateReadout();
        }

        void UpdateReadout()
        {
            if (_readout == null) return;
            int indexed = IndexedCount();
            string state = _phase switch
            {
                FieldPhase.Laying => $"<color=#ffcc60>laying {_laid:N0}/{_requested:N0}</color>",
                FieldPhase.Materializing => $"<color=#ffcc60>materializing {indexed:N0}/{_requested:N0} (wait before spawning a hole)</color>",
                FieldPhase.Ready => $"<color=#80ff80>ready — {indexed:N0} indexed</color> (live {_prisms.Count:N0})",
                _ => $"idle — {ProjectedCount():N0} sites projected",
            };
            var config01 = BlackHoleRegistry.Config;
            float s = StrengthFromInput();
            SetReadout(
                $"{state}   extents {Extents.x:F0} x {Extents.y:F0} x {Extents.z:F0}   " +
                $"strength {s:F1}: horizon {config01.HorizonRadius(s):F1}, influence {config01.InfluenceRadius(s):F0}   " +
                $"holes {BlackHoleRegistry.Count}, bodies {BlackHoleGravityField.BodyCount:N0}, captured {BlackHoleGravityField.CapturedTotal:N0}\n" +
                CameraHint);
        }

        /// <summary>The camera's controls, on the panel the operator is already reading. ASCII only.</summary>
        const string CameraHint =
            "<color=#9aa4b8>camera: RMB drag pan | LMB drag (or Alt+RMB) orbit | wheel / MMB drag zoom | WASD pan, Q/E turn, Shift fast | F frame</color>";

        void SetReadout(string text)
        {
            if (_readout == null) return;
            _readout.text = Time.unscaledTime < _warningUntil
                ? $"{text}\n<color=#ff9060>{_warning}</color>"
                : text;
        }

        void Warn(string message)
        {
            _warning = message;
            _warningUntil = Time.unscaledTime + WarningSeconds;
            Debug.LogWarning($"[BlackHoleTestHarness] {message}");
            UpdateReadout();
        }

        float StrengthFromInput()
        {
            if (_strengthInput != null &&
                float.TryParse(_strengthInput.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s >= 0f)
                return s;
            return config.CentreStrength;
        }

        const string Usage = "usage: bhtest <total> | bhtest shape cuboid|spheroid | bhtest spawn | bhtest hole [strength] | " +
                             "bhtest fly [strength] [speed] | bhtest despawn | bhtest clear | bhtest zoom <0..1> | bhtest frame";

        string HandleCommand(string[] args)
        {
            if (args.Length == 0) return Usage;

            if (args.Length == 1 && int.TryParse(args[0], out int total) && total > 0)
            {
                int side = Mathf.Max(1, Mathf.RoundToInt(Mathf.Pow(total, 1f / 3f)));
                _counts = new Vector3Int(side, side, side);
                SyncInputsFromState();
                Spawn();
                return $"spawning a {side}^3 {_shape.ToString().ToLowerInvariant()} (~{ProjectedCount():N0} prisms)";
            }

            switch (args[0].ToLowerInvariant())
            {
                case "shape":
                    if (args.Length < 2) return Usage;
                    _shape = args[1].ToLowerInvariant() == "cuboid"
                        ? BlackHoleTestConfigSO.FieldShape.Cuboid
                        : BlackHoleTestConfigSO.FieldShape.Spheroid;
                    PublishStats();
                    return $"shape {_shape.ToString().ToLowerInvariant()} (takes effect on the next spawn)";
                case "spawn":
                    Spawn();
                    return $"spawning {_shape.ToString().ToLowerInvariant()} {_counts.x}x{_counts.y}x{_counts.z}";
                case "hole":
                {
                    float strength = config.CentreStrength;
                    if (args.Length > 1 && !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out strength))
                        return Usage;
                    var hole = SpawnCentreHole(strength);
                    return hole == null ? "spawn refused (see console)" : $"hole #{hole.Id} strength {strength:F1} at the centre";
                }
                case "fly":
                {
                    float strength = config.FlyStrength, speed = config.FlySpeed;
                    if (args.Length > 1 && !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out strength))
                        return Usage;
                    if (args.Length > 2 && !float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out speed))
                        return Usage;
                    var hole = FlyThrough(strength, speed);
                    return hole == null ? "spawn refused (see console)" : $"hole #{hole.Id} flying +X at {speed:F0} u/s";
                }
                case "despawn":
                    DespawnHoles();
                    return "despawning every hole";
                case "clear":
                    Clear();
                    return "field cleared";
                case "frame":
                    FrameView();
                    return "framing the field";
                case "zoom":
                    if (args.Length < 2 || !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float z01))
                        return "usage: bhtest zoom <0..1>";
                    SetZoom(Mathf.Clamp01(z01));
                    return $"zoom {_zoom:F2}";
            }
            return Usage;
        }

        // ── UI construction (mirrors PrismGridExplosionHarness.BuildUI's code-built idiom) ──

        void BuildUI()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            EnsureEventSystem();

            var canvasGO = new GameObject("BlackHoleTestCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;   // below DiagnosticsHUD's 32760

            var panel = CreateRect("Panel", canvasGO.transform, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(8, 164), new Vector2(620, 156));
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);

            // Row 1 — field size, pitch, hole strength.
            CreateLabel("side", panel, new Vector2(8, -8), 34);
            _countInput = CreateInput(panel, new Vector2(44, -8), 46, _counts.x.ToString(), OnFieldChanged);
            CreateLabel("gap", panel, new Vector2(98, -8), 30);
            _gapInput = CreateInput(panel, new Vector2(130, -8), 46, _gaps.x.ToString("F0", CultureInfo.InvariantCulture), OnFieldChanged);
            CreateLabel("strength", panel, new Vector2(186, -8), 62);
            _strengthInput = CreateInput(panel, new Vector2(250, -8), 50, config.CentreStrength.ToString("F1", CultureInfo.InvariantCulture), _ => UpdateReadout());
            CreateButton(_shape == BlackHoleTestConfigSO.FieldShape.Spheroid ? "Spheroid" : "Cuboid", panel, new Vector2(310, -8), 90, ToggleShape);

            // Row 2 — actions.
            CreateButton("Spawn field", panel, new Vector2(8, -40), 100, Spawn);
            CreateButton("Hole at centre", panel, new Vector2(114, -40), 110, () => SpawnCentreHole(StrengthFromInput()));
            CreateButton("Fly-through", panel, new Vector2(230, -40), 100, () => FlyThrough(config.FlyStrength, config.FlySpeed));
            CreateButton("Despawn holes", panel, new Vector2(336, -40), 110, DespawnHoles);
            CreateButton("Clear", panel, new Vector2(452, -40), 70, Clear);

            // Row 3 — zoom.
            CreateLabel("zoom", panel, new Vector2(8, -72), 40);
            _zoomSlider = CreateSlider(panel, new Vector2(52, -72), 400, _zoom, v => { _zoom = v; ApplyZoom(reframe: false, snap: false); });
            CreateButton("Frame (F)", panel, new Vector2(460, -72), 90, FrameView);

            // Row 4 — readout.
            var readoutRT = CreateRect("Readout", panel, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(8, -102), new Vector2(604, 48));
            _readout = readoutRT.gameObject.AddComponent<Text>();
            _readout.font = _font;
            _readout.fontSize = 12;
            _readout.color = Color.white;
            _readout.supportRichText = true;
            _readout.alignment = TextAnchor.UpperLeft;
            _readout.horizontalOverflow = HorizontalWrapMode.Wrap;
            _readout.verticalOverflow = VerticalWrapMode.Overflow;
            UpdateReadout();
        }

        Button _shapeButton;

        void ToggleShape()
        {
            _shape = _shape == BlackHoleTestConfigSO.FieldShape.Spheroid
                ? BlackHoleTestConfigSO.FieldShape.Cuboid
                : BlackHoleTestConfigSO.FieldShape.Spheroid;
            if (_shapeButton != null)
            {
                var label = _shapeButton.GetComponentInChildren<Text>();
                if (label != null) label.text = _shape == BlackHoleTestConfigSO.FieldShape.Spheroid ? "Spheroid" : "Cuboid";
            }
            PublishStats();
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        void OnFieldChanged(string editedValue)
        {
            if (int.TryParse(_countInput.text, out int side)) _counts = new Vector3Int(Mathf.Max(1, side), Mathf.Max(1, side), Mathf.Max(1, side));
            if (float.TryParse(_gapInput.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float gap))
                _gaps = Vector3.one * Mathf.Max(0.01f, gap);
            ApplyZoom(reframe: true, snap: false);
            PublishStats();
        }

        void SyncInputsFromState()
        {
            if (_countInput != null) _countInput.text = _counts.x.ToString();
            if (_gapInput != null) _gapInput.text = _gaps.x.ToString("F0", CultureInfo.InvariantCulture);
        }

        RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        Text CreateLabel(string text, Transform parent, Vector2 pos, float width)
        {
            var rt = CreateRect("Lbl_" + text, parent, new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(width, 22));
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = 13;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            t.text = text;
            return t;
        }

        InputField CreateInput(Transform parent, Vector2 pos, float width, string value, Action<string> onChanged)
        {
            var rt = CreateRect("Input", parent, new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(width, 22));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.12f, 0.12f, 0.16f, 0.95f);
            var field = rt.gameObject.AddComponent<InputField>();
            field.lineType = InputField.LineType.SingleLine;
            var textRT = CreateRect("Text", rt, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            textRT.anchoredPosition = Vector2.zero;
            textRT.sizeDelta = Vector2.zero;
            var t = textRT.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = 13;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            t.supportRichText = false;
            field.textComponent = t;
            field.text = value;
            field.onEndEdit.AddListener(v => onChanged(v));
            return field;
        }

        Button CreateButton(string label, Transform parent, Vector2 pos, float width, Action onClick)
        {
            var rt = CreateRect("Btn_" + label, parent, new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(width, 24));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.25f, 0.3f, 0.4f, 0.95f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            var labelRT = CreateRect("Label", rt, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            labelRT.anchoredPosition = Vector2.zero;
            labelRT.sizeDelta = Vector2.zero;
            var t = labelRT.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = 13;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = label;
            if (label == "Spheroid" || label == "Cuboid") _shapeButton = btn;
            return btn;
        }

        Slider CreateSlider(Transform parent, Vector2 pos, float width, float value, Action<float> onChanged)
        {
            var rt = CreateRect("Zoom", parent, new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(width, 22));
            var slider = rt.gameObject.AddComponent<Slider>();
            var bgRT = CreateRect("Background", rt, new Vector2(0, 0.35f), new Vector2(1, 0.65f), Vector2.zero, Vector2.zero);
            bgRT.anchoredPosition = Vector2.zero;
            bgRT.sizeDelta = Vector2.zero;
            var bg = bgRT.gameObject.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);
            var fillArea = CreateRect("FillArea", rt, new Vector2(0, 0.35f), new Vector2(1, 0.65f), Vector2.zero, Vector2.zero);
            fillArea.anchoredPosition = Vector2.zero;
            fillArea.sizeDelta = Vector2.zero;
            var fillRT = CreateRect("Fill", fillArea, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            fillRT.anchoredPosition = Vector2.zero;
            fillRT.sizeDelta = Vector2.zero;
            var fill = fillRT.gameObject.AddComponent<Image>();
            fill.color = new Color(0.35f, 0.55f, 0.8f, 0.95f);
            var handleArea = CreateRect("HandleArea", rt, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            handleArea.anchoredPosition = Vector2.zero;
            handleArea.sizeDelta = Vector2.zero;
            var handleRT = CreateRect("Handle", handleArea, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(14, 0));
            handleRT.pivot = new Vector2(0.5f, 0.5f);   // Slider.UpdateVisuals never writes the pivot
            var handle = handleRT.gameObject.AddComponent<Image>();
            handle.color = Color.white;
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v => onChanged(v));
            return slider;
        }
    }
}
#endif
