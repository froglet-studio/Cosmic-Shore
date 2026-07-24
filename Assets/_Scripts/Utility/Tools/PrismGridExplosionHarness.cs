#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
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
    /// Prism-grid explosion test rig.
    ///
    /// Spawns a parameterised cuboid lattice of REAL prisms centred on the world origin, then
    /// detonates the Dolphin's giant blast into the middle of it on demand. Fills the gap between
    /// the two existing prism harnesses: <c>PrismRenderStressTest</c>/<c>PrismStressInjector</c>
    /// spawn a render-only ECS cloud (nothing damageable), and <c>AOEBenchmarkRunner</c> registers
    /// synthetic index entries with damage application deliberately excluded. This one exercises
    /// the whole chain — mass spawn + grow-in, instanced rendering at a known count, and Burst AOE
    /// damage WITH real destruction — at a count you choose.
    ///
    /// Ecosystem invariants (CLAUDE.md): prisms grow in via the normal <c>Prism.Initialize()</c>
    /// path and are removed only by an ACTIVE force (the blast, or an explicit operator Clear).
    /// There is no TTL, decay, or idle culler anywhere in this rig — mass is conserved.
    ///
    /// Performance readout is the standard <c>DiagnosticsHUD</c>, which auto-spawns in every scene:
    /// F7 shows the overlay, F5 (or "Run Ns") records a report to
    /// Documents/CosmicShore Diagnostics/. This component draws no perf overlay of its own — it
    /// publishes rows into the HUD's "PrismGrid" section and registers a "grid" console command.
    /// </summary>
    public class PrismGridExplosionHarness : MonoBehaviour
    {
        const string StatsSection = "PrismGrid";
        const string CommandName = "grid";
        const string OwnerPrefix = "PrismGridTest";
        const string GridRootName = "[PrismGrid]";

        /// <summary>
        /// List entries examined per frame by the husk sweep. Prism.Explode leaves the GameObject
        /// alive but hidden (the VFX is a separate pooled object), and PrismTrailBuilder
        /// instantiates rather than pools, so a blast leaves thousands of inert husks behind.
        /// Reclaiming them is the pool-return this rig doesn't have — it is NOT a decay timer;
        /// every prism it frees was already killed by an active force.
        /// </summary>
        const int HusksScannedPerFrame = 512;

        /// <summary>Suction end-scale — never exactly zero, so lossyScale stays well-formed.</summary>
        const float SuctionScale = 0.002f;

        [Header("Configuration")]
        [Tooltip("Tunables asset. When empty the harness falls back to any PrismGridTestConfigSO in " +
                 "Resources, so the rig still runs if the scene reference is lost.")]
        [SerializeField] private PrismGridTestConfigSO config;

        [Tooltip("Camera framing the lattice. Falls back to Camera.main when empty.")]
        [SerializeField] private Camera viewCamera;

        // ── Runtime state ────────────────────────────────────────────────────

        Trail _trail;
        Transform _gridRoot;
        readonly List<Prism> _prisms = new();
        CancellationTokenSource _spawnCts;
        bool _spawning;
        int _laid;
        int _requested;
        int _sweepCursor;

        Vector3Int _counts;
        float _gap;
        float _zoom;

        // ── UI ───────────────────────────────────────────────────────────────

        Font _font;
        InputField _countXInput, _countYInput, _countZInput, _gapInput;
        Slider _zoomSlider;
        Text _readout;

        void Awake()
        {
            if (config == null)
                config = Resources.Load<PrismGridTestConfigSO>("PrismGridTestConfig");

            if (config == null)
            {
                Debug.LogError("[PrismGridExplosionHarness] No PrismGridTestConfigSO assigned or found " +
                               "in Resources. Run Tools > Cosmic Shore > Setup Prism Grid Explosion Scene.");
                enabled = false;
                return;
            }

            _counts = config.DefaultCounts;
            _gap = config.DefaultGap;
            _zoom = config.DefaultZoom;

            if (viewCamera == null) viewCamera = Camera.main;

            BuildUI();
            ApplyZoom();
        }

        void Start()
        {
            // Prism animation/material managers are Singleton<T>, which never auto-creates —
            // without PrismManagers.prefab in the scene prisms spawn but never animate.
            if (PrismScaleManager.Instance == null)
            {
                Debug.LogError("[PrismGridExplosionHarness] PrismScaleManager.Instance is null — " +
                               "add _Prefabs/Environment/PrismManagers.prefab to the scene.");
            }

            DiagnosticsHUD.RegisterCommand(CommandName, HandleGridCommand);
            PublishStats();
        }

        void Update()
        {
            if (!_spawning) ReclaimHusks();
        }

        void OnDestroy()
        {
            CancelSpawn();
            DiagnosticsHUD.UnregisterCommand(CommandName);
            DiagnosticsHUD.ClearStats(StatsSection);
        }

        /// <summary>
        /// Frees the GameObjects of prisms the blast already killed, sweeping a bounded window per
        /// frame from a rolling cursor so the cost is independent of lattice size — a full-list scan
        /// every frame would cost more than the husks do (Prism has no Update; a husk is inert).
        /// The live count therefore converges over a second or so rather than snapping, which is
        /// fine: it is a readout, not a gameplay quantity.
        /// </summary>
        void ReclaimHusks()
        {
            if (_prisms.Count == 0)
            {
                _sweepCursor = 0;
                return;
            }

            int scans = Mathf.Min(HusksScannedPerFrame, _prisms.Count);
            bool changed = false;

            for (int n = 0; n < scans; n++)
            {
                if (_sweepCursor >= _prisms.Count) _sweepCursor = 0;

                var prism = _prisms[_sweepCursor];
                if (prism != null && !prism.destroyed)
                {
                    _sweepCursor++;
                    continue;
                }

                // Swap-remove is O(1) and order here is meaningless. The slot now holds a
                // not-yet-scanned prism, so the cursor deliberately does not advance.
                int last = _prisms.Count - 1;
                _prisms[_sweepCursor] = _prisms[last];
                _prisms.RemoveAt(last);
                if (prism != null) Destroy(prism.gameObject);
                changed = true;
            }

            if (changed) PublishStats();
        }

        // ── Grid geometry ────────────────────────────────────────────────────

        /// <summary>
        /// Cuboid extent per axis. Gap A is centre-to-centre pitch, so N prisms span (N-1)*A.
        /// </summary>
        Vector3 Extents => new(
            Mathf.Max(0, _counts.x - 1) * _gap,
            Mathf.Max(0, _counts.y - 1) * _gap,
            Mathf.Max(0, _counts.z - 1) * _gap);

        /// <summary>Lattice sites, centred on the origin so the cuboid's middle IS (0,0,0).</summary>
        List<PrismLay> BuildLays()
        {
            var scale = config.PrismScale == Vector3.zero
                ? config.PrismPrefab.transform.localScale
                : config.PrismScale;

            var lays = new List<PrismLay>(_counts.x * _counts.y * _counts.z);
            var half = new Vector3(
                (_counts.x - 1) * 0.5f,
                (_counts.y - 1) * 0.5f,
                (_counts.z - 1) * 0.5f);

            for (int x = 0; x < _counts.x; x++)
            for (int y = 0; y < _counts.y; y++)
            for (int z = 0; z < _counts.z; z++)
            {
                var pos = new Vector3(
                    (x - half.x) * _gap,
                    (y - half.y) * _gap,
                    (z - half.z) * _gap);

                lays.Add(new PrismLay(
                    new SpawnPoint(pos, Quaternion.identity, scale),
                    config.GridDomain));
            }

            return lays;
        }

        // ── Spawn ────────────────────────────────────────────────────────────

        public void Spawn()
        {
            if (config.PrismPrefab == null)
            {
                SetReadout("<color=#ff8080>No prism prefab configured.</color>");
                return;
            }

            long total = (long)_counts.x * _counts.y * _counts.z;
            if (total <= 0)
            {
                SetReadout("<color=#ff8080>Counts must all be >= 1.</color>");
                return;
            }

            if (total > config.MaxTotalPrisms)
            {
                SetReadout($"<color=#ff8080>{total:N0} prisms exceeds the {config.MaxTotalPrisms:N0} " +
                           "cap (PrismGridTestConfig.maxTotalPrisms).</color>");
                return;
            }

            Clear();
            SpawnAsync().Forget();
        }

        async UniTaskVoid SpawnAsync()
        {
            CancelSpawn();
            _spawnCts = new CancellationTokenSource();
            var ct = _spawnCts.Token;

            var lays = BuildLays();
            _requested = lays.Count;
            _laid = 0;
            _spawning = true;

            _gridRoot = new GameObject(GridRootName).transform;
            _gridRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _trail = new Trail();

            PublishStats();

            try
            {
                // Progress ticker runs alongside the lay so the readout moves while prisms stream in.
                TrackProgress(ct).Forget();

                // PrismTrailBuilder is THE canonical lay-a-prism primitive — do not inline its
                // Instantiate → ChangeTeam → TargetScale → Trail → Initialize sequence here.
                await PrismTrailBuilder.LayBatched(
                    config.PrismPrefab, lays, _gridRoot, _trail,
                    OwnerPrefix, config.PrismsPerFrame, ct, _prisms);
            }
            catch (OperationCanceledException)
            {
                // Cancelled by Clear / disable — partial lattice is left as-is for the caller to clear.
            }
            finally
            {
                _spawning = false;
                _laid = _prisms.Count;
                PublishStats();
            }
        }

        async UniTaskVoid TrackProgress(CancellationToken ct)
        {
            try
            {
                while (_spawning && !ct.IsCancellationRequested)
                {
                    _laid = _prisms.Count;
                    PublishStats();
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelled by Clear / disable — nothing to report.
            }
        }

        void CancelSpawn()
        {
            if (_spawnCts == null) return;
            if (!_spawnCts.IsCancellationRequested) _spawnCts.Cancel();
            _spawnCts.Dispose();
            _spawnCts = null;
            _spawning = false;
        }

        // ── Explode ──────────────────────────────────────────────────────────

        public void Explode()
        {
            if (config.ExplosionPrefab == null)
            {
                SetReadout("<color=#ff8080>No explosion prefab configured.</color>");
                return;
            }

            if (!config.DomainsAreDestructive)
            {
                SetReadout($"<color=#ffcc60>Grid and explosion are both {config.GridDomain} — the blast " +
                           "will NOT destroy prisms (AOEExplosion does not affect its own domain). " +
                           "Change one domain in PrismGridTestConfig.</color>");
            }

            // Mirrors ExplosionHelper.SpawnAllAndDetonate, minus the DI inject: this scene has no
            // Reflex container, and AOEExplosion's [Inject] gameData is null-guarded at every use.
            var aoe = Instantiate(config.ExplosionPrefab);
            aoe.Initialize(new AOEExplosion.InitializeStruct
            {
                OwnDomain = config.ExplosionDomain,
                AnnonymousExplosion = true,
                Vessel = null,
                OverrideMaterial = ResolveExplosionMaterial(aoe),
                MaxScale = config.MaxScale,
                SpawnPosition = Vector3.zero,
                SpawnRotation = Quaternion.identity,
            });
            aoe.Detonate();
        }

        /// <summary>
        /// AOEExplosion assigns Material straight onto its renderer, so a null here renders the
        /// blast invisible. Fall back to whatever the prefab already ships with.
        /// </summary>
        Material ResolveExplosionMaterial(AOEExplosion instance)
        {
            if (config.OverrideMaterial != null) return config.OverrideMaterial;
            return instance.TryGetComponent(out MeshRenderer mr) ? mr.sharedMaterial : null;
        }

        // ── Clear ────────────────────────────────────────────────────────────

        /// <summary>
        /// Operator-driven teardown (a tool reset, not a simulation mechanic — this rig has no
        /// decay or TTL).
        ///
        /// The lattice is suctioned toward the origin and then freed, mirroring
        /// <c>Microscene.RecycleAsync</c>'s "sanctioned continuity transition" — the continuity law
        /// lists suction-toward-a-point as a legal exit, and one container-scale animation costs
        /// nothing. Deliberately NOT per-prism <c>Prism.Damage</c>: that raises the block-impacted
        /// channel per prism, so clearing a 6,000-prism lattice would mint 6,000 simultaneous
        /// explosion VFX and wreck the very measurement this scene exists to take.
        ///
        /// <c>Prism.OnDestroy</c> unregisters each prism from every PrismSpatialIndex view and
        /// destroys its companion render entity, so freeing the root leaves no stale state.
        /// </summary>
        public void Clear()
        {
            CancelSpawn();

            // Hand the outgoing lattice to the suction task and detach immediately, so a Spawn
            // issued mid-suction builds a fresh root instead of racing this one.
            var outgoingRoot = _gridRoot;
            var outgoing = new List<Prism>(_prisms);

            _prisms.Clear();
            _trail = null;
            _gridRoot = null;
            _laid = 0;
            _requested = 0;
            PublishStats();

            SuctionAndFreeAsync(outgoingRoot, outgoing).Forget();
        }

        /// <summary>
        /// Suction the lattice toward the origin, then free it. Mirrors
        /// <c>Microscene.AnimateScaleAsync</c> — including the per-frame
        /// <c>NotifyPositionChanged()</c> sweep, which is what actually makes the transition
        /// visible: a prism draws through an instanced companion entity whose matrix is pushed
        /// explicitly, never polled, so scaling the parent alone would move nothing on screen.
        /// </summary>
        async UniTaskVoid SuctionAndFreeAsync(Transform root, List<Prism> prisms)
        {
            if (root == null) return;

            root.gameObject.name = GridRootName + " (clearing)";

            float seconds = config.ClearSeconds;
            if (seconds > 0f && prisms.Count > 0)
            {
                float elapsed = 0f;
                while (elapsed < seconds && root != null)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / seconds);
                    float eased = t * t * (3f - 2f * t); // smoothstep, matching Microscene / Toy.BloomIn

                    // Never exactly zero — keeps lossyScale well-formed (Microscene.SuctionScale).
                    root.localScale = Vector3.one * Mathf.LerpUnclamped(1f, SuctionScale, eased);

                    for (int i = 0; i < prisms.Count; i++)
                    {
                        var prism = prisms[i];
                        if (prism) prism.NotifyPositionChanged();
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }

            // Prism.OnDestroy unregisters from every PrismSpatialIndex view and destroys the
            // companion render entity, so freeing the root leaves no stale state behind.
            if (root != null) Destroy(root.gameObject);
        }

        // ── Camera ───────────────────────────────────────────────────────────

        /// <summary>Sets zoom and keeps the slider in step (used by the console command).</summary>
        public void SetZoom(float value)
        {
            _zoom = Mathf.Clamp01(value);
            if (_zoomSlider != null) _zoomSlider.SetValueWithoutNotify(_zoom);
            ApplyZoom();
        }

        void ApplyZoom()
        {
            if (viewCamera == null) return;

            float far = Mathf.Max(config.NearDistance + 1f, Extents.z * config.FarDistanceMultiplier);
            float dist = Mathf.Lerp(config.NearDistance, far, _zoom);

            // -Z placement so the camera's +Z forward points back at the origin. NearDistance is
            // never 0: LookAt from exactly the origin is a degenerate zero-length direction.
            viewCamera.transform.position = new Vector3(0f, 0f, -dist);
            viewCamera.transform.LookAt(Vector3.zero);

            if (viewCamera.farClipPlane < dist + Extents.magnitude)
                viewCamera.farClipPlane = dist + Extents.magnitude + 1000f;
        }

        // ── Diagnostics ──────────────────────────────────────────────────────

        void PublishStats()
        {
            DiagnosticsHUD.SetStat(StatsSection, "counts", $"{_counts.x}x{_counts.y}x{_counts.z}");
            DiagnosticsHUD.SetStat(StatsSection, "gap", _gap.ToString("F1"));
            DiagnosticsHUD.SetStat(StatsSection, "prisms", _spawning
                ? $"{_laid:N0}/{_requested:N0}"
                : $"{_prisms.Count:N0}");
            DiagnosticsHUD.SetStat(StatsSection, "extents",
                $"{Extents.x:F0}x{Extents.y:F0}x{Extents.z:F0}");
            DiagnosticsHUD.SetStat(StatsSection, "blast r", config.BlastRadius.ToString("F0"));

            UpdateReadout();
        }

        void UpdateReadout()
        {
            if (_readout == null) return;

            float halfDiagonal = Extents.magnitude * 0.5f;
            string coverage = config.BlastRadius >= halfDiagonal
                ? "<color=#80ff80>blast engulfs lattice</color>"
                : "<color=#ffcc60>blast covers centre only</color>";

            string state = _spawning
                ? $"spawning {_laid:N0}/{_requested:N0}"
                : $"live {_prisms.Count:N0}";

            SetReadout(
                $"{state}   extents {Extents.x:F0} x {Extents.y:F0} x {Extents.z:F0}   " +
                $"blast r {config.BlastRadius:F0} vs half-diagonal {halfDiagonal:F0}   {coverage}");
        }

        void SetReadout(string text)
        {
            if (_readout != null) _readout.text = text;
        }

        const string Usage = "usage: grid <x> <y> <z> [gap] | grid explode | grid clear | grid zoom <0..1>";

        string HandleGridCommand(string[] args)
        {
            if (args.Length == 0)
                return Usage;

            switch (args[0].ToLowerInvariant())
            {
                case "explode":
                    Explode();
                    return "detonated at origin";
                case "clear":
                    Clear();
                    return "grid cleared";
                case "zoom":
                    if (args.Length < 2 || !float.TryParse(args[1], out float z01))
                        return "usage: grid zoom <0..1>";
                    SetZoom(Mathf.Clamp01(z01));
                    return $"zoom {_zoom:F2}";
            }

            if (args.Length < 3 ||
                !int.TryParse(args[0], out int x) ||
                !int.TryParse(args[1], out int y) ||
                !int.TryParse(args[2], out int z))
                return Usage;

            if (args.Length > 3 && float.TryParse(args[3], out float gap)) _gap = gap;

            _counts = new Vector3Int(Mathf.Max(1, x), Mathf.Max(1, y), Mathf.Max(1, z));
            SyncInputsFromState();
            Spawn();
            return $"spawning {_counts.x}x{_counts.y}x{_counts.z} at gap {_gap:F1}";
        }

        // ── UI construction (mirrors DiagnosticsHUD.BuildUI's code-built idiom) ──

        void BuildUI()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            EnsureEventSystem();

            var canvasGO = new GameObject("PrismGridCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Well below DiagnosticsHUD's 32760 so the perf overlay always draws on top.
            canvas.sortingOrder = 100;

            // Bottom-left panel, clear of the DiagnosticsHUD panel in the top-left.
            var panel = CreateRect("Panel", canvasGO.transform, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(8, 148), new Vector2(560, 140));
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);

            // Row 1 — grid dimensions.
            CreateLabel("X", panel, new Vector2(8, -8), 20);
            _countXInput = CreateInput(panel, new Vector2(28, -8), 54, _counts.x.ToString(), OnCountsChanged);
            CreateLabel("Y", panel, new Vector2(90, -8), 20);
            _countYInput = CreateInput(panel, new Vector2(110, -8), 54, _counts.y.ToString(), OnCountsChanged);
            CreateLabel("Z", panel, new Vector2(172, -8), 20);
            _countZInput = CreateInput(panel, new Vector2(192, -8), 54, _counts.z.ToString(), OnCountsChanged);
            CreateLabel("gap A", panel, new Vector2(254, -8), 46);
            _gapInput = CreateInput(panel, new Vector2(302, -8), 60, _gap.ToString("F1"), OnCountsChanged);

            // Row 2 — actions.
            CreateButton("Spawn", panel, new Vector2(8, -40), 90, Spawn);
            CreateButton("Explode", panel, new Vector2(104, -40), 90, Explode);
            CreateButton("Clear", panel, new Vector2(200, -40), 90, Clear);

            // Row 3 — zoom.
            CreateLabel("zoom", panel, new Vector2(8, -72), 40);
            _zoomSlider = CreateSlider(panel, new Vector2(52, -72), 310, _zoom, OnZoomChanged);

            // Row 4 — readout.
            var readoutRT = CreateRect("Readout", panel, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(8, -102), new Vector2(544, 32));
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

        /// <summary>
        /// Find-or-reuse: DiagnosticsHUD also ensures an EventSystem, and Unity errors on a second
        /// one. Whichever of us runs first wins.
        /// </summary>
        void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        /// <summary>Re-reads every dimension field; the edited value arrives via the field itself.</summary>
        void OnCountsChanged(string editedValue)
        {
            if (int.TryParse(_countXInput.text, out int x)) _counts.x = Mathf.Max(1, x);
            if (int.TryParse(_countYInput.text, out int y)) _counts.y = Mathf.Max(1, y);
            if (int.TryParse(_countZInput.text, out int z)) _counts.z = Mathf.Max(1, z);
            if (float.TryParse(_gapInput.text, out float gap)) _gap = Mathf.Max(0.01f, gap);

            ApplyZoom(); // far distance follows the Z extent
            PublishStats();
        }

        void OnZoomChanged(float value)
        {
            _zoom = value;
            ApplyZoom();
        }

        void SyncInputsFromState()
        {
            if (_countXInput != null) _countXInput.text = _counts.x.ToString();
            if (_countYInput != null) _countYInput.text = _counts.y.ToString();
            if (_countZInput != null) _countZInput.text = _counts.z.ToString();
            if (_gapInput != null) _gapInput.text = _gap.ToString("F1");
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
