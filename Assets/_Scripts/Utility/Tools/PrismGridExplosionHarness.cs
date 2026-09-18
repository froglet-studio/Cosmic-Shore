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
        const string LabCommandName = "lab";
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

        /// <summary>
        /// Seconds the materialization wait tolerates a stalled index count before declaring the
        /// lattice ready anyway. Guards against waiting forever when prisms died during spawn (so
        /// the indexed count can never reach the requested count).
        /// </summary>
        const float MaterializeStallSeconds = 2f;

        /// <summary>How long a warning stays pinned under the readout.</summary>
        const float WarningSeconds = 6f;

        /// <summary>
        /// Lattice lifecycle. Laying (instantiating) and materializing (becoming visible, collidable
        /// and registered in PrismSpatialIndex) are SEPARATE and wildly different in duration:
        /// Prism gates creation completion behind a static, process-wide budget of 6 per frame
        /// (Prism.MaxCreationCompletionsPerFrame), so 6,000 prisms are laid in ~30 frames but take
        /// ~1,000 frames to register. Until a prism registers it has no spatial-index slot AND a
        /// disabled collider, so a blast fired early is invisible to it through both the Burst path
        /// and the physics fallback. Conflating the two would make every early run measure a lattice
        /// that is not there yet.
        /// </summary>
        enum GridPhase
        {
            Idle = 0,
            Laying = 1,
            Materializing = 2,
            Ready = 3,
        }

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
        GridPhase _phase = GridPhase.Idle;
        int _laid;
        int _requested;
        int _sweepCursor;

        Vector3Int _counts;
        Vector3 _gaps;
        float _zoom;

        /// <summary>
        /// Per-site (kind, domain) recipe for the NEXT lay, or null for the legacy uniform
        /// lattice (every prism Plain, in <c>config.GridDomain</c>) that <c>grid</c> produces.
        ///
        /// It exists because `grid` can only ever render `meshes=1 mats=1` — one prefab, one
        /// domain, one tier — which is the most batchable population possible and therefore
        /// says nothing about the boot world's `meshes=3 mats=10`. The measured gap is large:
        /// 103,823 uniform prisms draw in 515 calls (~202 per draw) while the boot world's
        /// ~34,000 draw in 14,244 (~2.4 per draw). Since
        /// <c>PrismRenderService.GetPrototype</c> keys its archetype on (layer, overrideSet)
        /// and NOT on mesh or material, every domain x tier lands in the same ArchetypeChunks
        /// and each chunk fragments into one draw command per distinct (mesh, material) run
        /// inside it. Reproducing that here is the only way to test the claim.
        ///
        /// Assignment is ROUND-ROBIN by site index, deliberately: a blocked layout (all plain,
        /// then all danger) lets chunks come out homogeneous by luck and would quietly prove
        /// nothing.
        /// </summary>
        PrismKind[] _mixKinds;
        Domains[] _mixDomains;

        /// <summary>Human-readable recipe for the HUD row, empty when uniform.</summary>
        string _mixLabel = string.Empty;

        // ── Safety-throttle lifts ────────────────────────────────────────────
        // The gameplay guards (per-frame AOE damage budget, per-frame VFX spawn
        // caps, live-effect pressure shortening) were sized for the CPU-per-effect
        // era. On the clock path a running effect costs no per-frame CPU, so this
        // rig lifts them by default (config.LiftSafetyThrottles) to measure the
        // system UNWEAKENED. Scene-scoped: previous values are captured on apply
        // and restored on destroy, so gameplay defaults are never touched.

        /// <summary>Effectively-unbounded per-frame budget. Not int.MaxValue: the
        /// drain/queue bounds scale it (×8, ×3) in long math before clamping, but a
        /// merely-huge number keeps every downstream int computation trivially safe.</summary>
        const int UnthrottledBudget = 1_000_000;

        bool _throttlesLifted;
        int _prevDamageBudgetOverride;
        int _prevVfxBudgetOverride;
        bool _prevPressureDisabled;

        void ApplyThrottleLifts()
        {
            if (_throttlesLifted || config == null || !config.LiftSafetyThrottles) return;
            _prevDamageBudgetOverride = PrismSpatialIndex.DamageBudgetPerFrameOverride;
            _prevVfxBudgetOverride = PrismFactory.VFXBudgetPerFrameOverride;
            _prevPressureDisabled = PrismFactory.EffectPressureScalingDisabled;
            PrismSpatialIndex.DamageBudgetPerFrameOverride = UnthrottledBudget;
            PrismFactory.VFXBudgetPerFrameOverride = UnthrottledBudget;
            PrismFactory.EffectPressureScalingDisabled = true;
            _throttlesLifted = true;
            Debug.Log("[PrismGridExplosionHarness] Safety throttles LIFTED for this scene: " +
                      "damage budget 48→unbounded, pressure shortening off. " +
                      "VFXBudgetPerFrameOverride is still written (no-op after D4 — death " +
                      "visuals are batched and unthrottled by construction). " +
                      "Restored automatically on scene exit.");
        }

        void RestoreThrottleLifts()
        {
            if (!_throttlesLifted) return;
            PrismSpatialIndex.DamageBudgetPerFrameOverride = _prevDamageBudgetOverride;
            PrismFactory.VFXBudgetPerFrameOverride = _prevVfxBudgetOverride;
            PrismFactory.EffectPressureScalingDisabled = _prevPressureDisabled;
            _throttlesLifted = false;
        }

        // ── Benchmark-driver surface (PrismExplosionBenchmark) ──────────────

        /// <summary>Lattice fully laid, materialized, and index-registered — safe to detonate into.</summary>
        public bool IsReady => _phase == GridPhase.Ready;

        /// <summary>No lattice (fresh scene or post-Clear) — safe to Spawn.</summary>
        public bool IsIdle => _phase == GridPhase.Idle;

        /// <summary>Live prisms currently in the lattice list.</summary>
        public int LivePrismCount => _prisms.Count;

        /// <summary>The lattice dimensions the next Spawn will build.</summary>
        public Vector3Int Counts => _counts;

        /// <summary>The per-axis lattice gaps the next Spawn will use.</summary>
        public Vector3 Gaps => _gaps;

        /// <summary>True while this rig holds the safety-throttle lifts (recorder metadata —
        /// mixed-lift runs are not comparable).</summary>
        public bool ThrottlesLifted => _throttlesLifted;

        /// <summary>The tunables asset (blast radius etc.) — read-only for the recorder's metadata.</summary>
        public PrismGridTestConfigSO Config => config;

        /// <summary>
        /// The blast's final damage radius. Fit-to-lattice (the benchmark spec): the
        /// explosion ends INSCRIBED — its last, largest overlap sphere reaches the
        /// centre of the nearest cube faces (min half-extent + a small epsilon), so
        /// the face-centre prisms are destroyed while edges and corners survive.
        /// Otherwise the authored config radius.
        /// </summary>
        public float EffectiveBlastRadius
        {
            get
            {
                if (config == null) return 0f;
                if (!config.FitBlastToLattice) return config.BlastRadius;
                float halfX = (_counts.x - 1) * 0.5f * _gaps.x;
                float halfY = (_counts.y - 1) * 0.5f * _gaps.y;
                float halfZ = (_counts.z - 1) * 0.5f * _gaps.z;
                // With per-axis gaps the inscribed sphere binds to the TIGHTEST
                // half-extent, and the epsilon must be sized against that axis's
                // own pitch — the neighbour ring it must not claim lives there.
                float inscribed = halfX;
                float bindingGap = _gaps.x;
                if (halfY < inscribed) { inscribed = halfY; bindingGap = _gaps.y; }
                if (halfZ < inscribed) { inscribed = halfZ; bindingGap = _gaps.z; }
                // Epsilon: enough to claim the face-centre prism, small enough that
                // the first ring around it (sqrt(h² + gap²) ≈ h + gap²/2h) survives.
                return inscribed + Mathf.Min(1f, bindingGap * 0.25f);
            }
        }

        /// <summary>
        /// The wavefront's full-expansion time for the next Explode. With a configured
        /// explosion SPEED the duration scales with the blast radius (fixed physical
        /// expansion rate); otherwise it is the prefab's authored duration (fixed sweep
        /// time — bigger blasts expand faster).
        /// </summary>
        public float EffectiveExplosionDuration
        {
            get
            {
                if (config == null || config.ExplosionPrefab == null) return 0f;
                return config.ExplosionSpeed > 0f
                    ? EffectiveBlastRadius / config.ExplosionSpeed
                    : config.ExplosionPrefab.Duration;
            }
        }

        // ── UI ───────────────────────────────────────────────────────────────

        Font _font;
        InputField _countXInput, _countYInput, _countZInput;
        InputField _gapXInput, _gapYInput, _gapZInput;
        Slider _zoomSlider;
        Text _readout;
        string _warning;
        float _warningUntil;

        void Awake()
        {
            if (config == null)
                config = Resources.Load<PrismGridTestConfigSO>("PrismGridTestConfig");

            if (config == null)
            {
                Debug.LogError("[PrismGridExplosionHarness] No PrismGridTestConfigSO assigned or found " +
                               "in Resources. Run FrogletTools > Scene Setup > Setup Prism Grid Explosion Scene.");
                enabled = false;
                return;
            }

            _counts = config.DefaultCounts;
            _gaps = config.DefaultGaps;
            _zoom = config.DefaultZoom;

            if (viewCamera == null) viewCamera = Camera.main;

            ApplyThrottleLifts();
            BuildUI();
            ApplyZoom();
        }

        void Start()
        {
            // BRANCH-PORTABLE manager check (this file runs on both the legacy-CPU
            // baseline branch and the gpu-clock branch for A/B benchmarking): the
            // legacy branches animate through PrismScaleManager, a Singleton<T> that
            // never auto-creates — without PrismManagers.prefab in the scene prisms
            // spawn but never animate. On clock branches the type does not exist
            // (animation rides the GPU clock) and no manager is required. Resolved
            // by reflection so the same source compiles everywhere.
            var legacyManagerType = Type.GetType("CosmicShore.Gameplay.PrismScaleManager, Assembly-CSharp");
            if (legacyManagerType != null &&
                legacyManagerType.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.FlattenHierarchy)?.GetValue(null) == null)
            {
                Debug.LogError("[PrismGridExplosionHarness] PrismScaleManager.Instance is null — " +
                               "add _Prefabs/Environment/PrismManagers.prefab to the scene.");
            }

            // The lay path hard-depends on a populated theme (Prism.ChangeTeam →
            // domain material lookup). ThemeManager.Awake fills the container; in
            // gameplay it lives in Bootstrap, which this scene deliberately skips.
            // Without it, the FIRST laid prism NREs and Spawn appears to do nothing.
            if (FindFirstObjectByType<ThemeManager>() == null)
                Warn("No ThemeManager in scene — Spawn will fail on the first prism. " +
                     "Re-run FrogletTools > Scene Setup > Setup Prism Grid Explosion Scene.");

            DiagnosticsHUD.RegisterCommand(CommandName, HandleGridCommand);
            DiagnosticsHUD.RegisterCommand(LabCommandName, HandleLabCommand);
            // The "prisms" alias that used to live here is RETIRED. PrismStressInjector
            // auto-spawns in every scene and claims that name for its render-only ECS cloud
            // — no GameObjects, no colliders, no MonoBehaviours — which is a completely
            // different measurement from this lattice of real prisms. Registration order is
            // not guaranteed, so `prisms 50000` silently resolved to whichever started last.
            // `grid` is unambiguous; use it.
            PublishStats();
        }

        void Update()
        {
            // Only sweep once the lattice has settled: nothing is destroyed while laying or
            // materializing, so a sweep then is pure overhead inside the measurement window.
            if (_phase == GridPhase.Ready) ReclaimHusks();
        }

        void OnDestroy()
        {
            CancelSpawn();
            RestoreThrottleLifts();
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

        /// <summary>
        /// Prisms of THIS lattice that have actually registered with PrismSpatialIndex — the number
        /// the blast can reach, and the only honest measure of readiness.
        ///
        /// Counted over our own list rather than read off <c>PrismSpatialIndex.LiveCount</c>: that
        /// count is global, so a previous lattice still suctioning out would inflate it and end the
        /// materialization wait early. The scan is a few thousand int reads — negligible next to the
        /// ~6-per-frame registration it is waiting on.
        /// </summary>
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

        // ── Grid geometry ────────────────────────────────────────────────────

        /// <summary>
        /// Cuboid extent per axis. Gaps are centre-to-centre pitch, so N prisms span (N-1)*gap.
        /// </summary>
        Vector3 Extents => new(
            Mathf.Max(0, _counts.x - 1) * _gaps.x,
            Mathf.Max(0, _counts.y - 1) * _gaps.y,
            Mathf.Max(0, _counts.z - 1) * _gaps.z);

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
                    (x - half.x) * _gaps.x,
                    (y - half.y) * _gaps.y,
                    (z - half.z) * _gaps.z);

                int i = lays.Count;
                lays.Add(new PrismLay(
                    new SpawnPoint(pos, Quaternion.identity, scale),
                    _mixDomains != null && _mixDomains.Length > 0
                        ? _mixDomains[i % _mixDomains.Length]
                        : config.GridDomain,
                    _mixKinds != null && i < _mixKinds.Length
                        ? _mixKinds[i]
                        : PrismKind.Plain));
            }

            return lays;
        }

        /// <summary>
        /// Interleaves an exact per-kind census into one site-ordered array: take one from each
        /// non-empty bucket in turn until every bucket is spent. Exact counts (no rounding, no
        /// error diffusion) AND maximal interleaving, which is the property under test — see
        /// <see cref="_mixKinds"/>.
        /// </summary>
        internal static PrismKind[] InterleaveKinds(int plain, int danger, int shielded, int super)
        {
            var remaining = new[]
            {
                Mathf.Max(0, plain),
                Mathf.Max(0, danger),
                Mathf.Max(0, shielded),
                Mathf.Max(0, super),
            };
            int total = remaining[0] + remaining[1] + remaining[2] + remaining[3];
            var outKinds = new PrismKind[total];

            int w = 0;
            while (w < total)
            {
                for (int k = 0; k < 4 && w < total; k++)
                {
                    if (remaining[k] <= 0) continue;
                    remaining[k]--;
                    outKinds[w++] = (PrismKind)k;
                }
            }
            return outKinds;
        }

        /// <summary>The first <paramref name="count"/> PLAYABLE domains, in enum order.</summary>
        internal static Domains[] PlayableDomainCycle(int count)
        {
            var all = new[] { Domains.Jade, Domains.Ruby, Domains.Gold };
            int n = Mathf.Clamp(count, 1, all.Length);
            var cycle = new Domains[n];
            for (int i = 0; i < n; i++) cycle[i] = all[i];
            return cycle;
        }

        // ── Spawn ────────────────────────────────────────────────────────────

        public void Spawn()
        {
            if (config.PrismPrefab == null)
            {
                Warn("No prism prefab configured.");
                return;
            }

            long total = (long)_counts.x * _counts.y * _counts.z;
            if (total <= 0)
            {
                Warn("Counts must all be >= 1.");
                return;
            }

            if (total > config.MaxTotalPrisms)
            {
                Warn($"{total:N0} prisms exceeds the {config.MaxTotalPrisms:N0} cap " +
                     "(PrismGridTestConfig.maxTotalPrisms).");
                return;
            }

            Clear();
            SpawnAsync().Forget();
        }

        // True while THIS harness holds the global load gate open for a build.
        // The gate raises Prism's creation-completion budget 6 → 512/frame and
        // skips the per-prism spawn-stagger wait — behind a covered screen in
        // gameplay; here the whole scene IS the loading screen, and nothing is
        // measured until Ready. A 100k lattice materializes in ~4s instead of
        // ~5 minutes. Ownership is release-once: CancelSpawn (synchronous
        // teardown) and the owning run's finally both route through here.
        bool _holdingLoadGate;

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

            PrismTrailBuilder.SetLoadGateHolding(true);
            _holdingLoadGate = true;

            var lays = BuildLays();
            _requested = lays.Count;
            _laid = 0;
            _phase = GridPhase.Laying;

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

                _laid = _prisms.Count;
                _phase = GridPhase.Materializing;
                await WaitForMaterializationAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // Cancelled by Clear / disable — partial lattice is left as-is for the caller to clear.
            }
            catch (Exception e)
            {
                // A lay failure otherwise dies in UniTask's unobserved-exception handler —
                // visually indistinguishable from "the button did nothing". Pin it where
                // the operator is looking and keep the full stack in the console.
                Warn($"Lay FAILED after {_prisms.Count:N0}/{_requested:N0}: {e.GetType().Name}: {e.Message}");
                Debug.LogException(e, this);
            }
            finally
            {
                // Only settle state if this run still owns the lattice; a Clear/re-Spawn during the
                // await has already moved on, and stomping _phase here would lie about that one
                // (or release the gate the successor is holding).
                if (_spawnCts != null && _spawnCts.Token == ct)
                {
                    _phase = GridPhase.Ready;
                    _laid = _prisms.Count;
                    ReleaseLoadGate();
                }
                PublishStats();
            }
        }

        /// <summary>
        /// Blocks until the prisms have actually registered with PrismSpatialIndex, not merely been
        /// instantiated. Prism completes creation behind a static 6-per-frame budget, so a 6,000-prism
        /// lattice finishes laying in ~30 frames but needs ~1,000 to become blast-visible. Bails out
        /// if the count stalls, which happens whenever prisms died during the wait.
        /// </summary>
        async UniTask WaitForMaterializationAsync(CancellationToken ct)
        {
            int lastCount = -1;
            float stalled = 0f;

            while (IndexedCount() < _requested)
            {
                int current = IndexedCount();
                if (current != lastCount)
                {
                    lastCount = current;
                    stalled = 0f;
                }
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
                while (_phase == GridPhase.Laying && !ct.IsCancellationRequested)
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
            ReleaseLoadGate();
            if (_spawnCts == null) return;
            if (!_spawnCts.IsCancellationRequested) _spawnCts.Cancel();
            _spawnCts.Dispose();
            _spawnCts = null;
            _phase = GridPhase.Idle;
        }

        // ── Explode ──────────────────────────────────────────────────────────

        public void Explode()
        {
            if (config.ExplosionPrefab == null)
            {
                Warn("No explosion prefab configured.");
                return;
            }

            if (!config.DomainsAreDestructive)
            {
                Warn($"Grid and blast are both {config.GridDomain} — prisms will be SHIELDED, not " +
                     "destroyed (AOEExplosion ships affectSelf=false). Change one domain in the config.");
            }
            else if (_phase != GridPhase.Ready)
            {
                // Only indexed prisms have a spatial-index slot and an enabled collider, so an early
                // blast is invisible to everything still materializing.
                Warn($"Detonating at {_phase.ToString().ToLowerInvariant()}: only {IndexedCount():N0} of " +
                     $"{_requested:N0} prisms are indexed and can be hit.");
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
                // AOEExplosion's damage radius = 0.5 (collider radius) * MaxScale, so
                // the inscribed end condition converts as MaxScale = 2 * radius. The
                // wavefront itself stays AOEExplosion's: progressively larger overlap
                // spheres each frame, expanding at speed = MaxScale / ExplosionDuration.
                MaxScale = 2f * EffectiveBlastRadius,
                SpawnPosition = Vector3.zero,
                SpawnRotation = Quaternion.identity,
                // A configured explosion SPEED pins the physical expansion rate:
                // duration = radius / speed. Zero keeps the prefab's authored
                // duration (fixed sweep time regardless of lattice size).
                DurationOverride = config.ExplosionSpeed > 0f
                    ? EffectiveBlastRadius / config.ExplosionSpeed
                    : 0f,
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
            // Which population is standing. A run whose recipe is not on screen is a run whose
            // draw-call number cannot be attributed later — see _mixKinds.
            DiagnosticsHUD.SetStat(StatsSection, "recipe",
                string.IsNullOrEmpty(_mixLabel) ? $"uniform plain · {config.GridDomain}" : _mixLabel);
            DiagnosticsHUD.SetStat(StatsSection, "gaps", $"{_gaps.x:F1}x{_gaps.y:F1}x{_gaps.z:F1}");
            DiagnosticsHUD.SetStat(StatsSection, "phase", _phase.ToString().ToLowerInvariant());
            DiagnosticsHUD.SetStat(StatsSection, "laid", $"{_laid:N0}/{_requested:N0}");
            // The number that actually matters: only indexed prisms are reachable by the blast.
            DiagnosticsHUD.SetStat(StatsSection, "indexed", $"{IndexedCount():N0}/{_requested:N0}");
            DiagnosticsHUD.SetStat(StatsSection, "extents",
                $"{Extents.x:F0}x{Extents.y:F0}x{Extents.z:F0}");
            DiagnosticsHUD.SetStat(StatsSection, "blast",
                $"r {EffectiveBlastRadius:F0} in {EffectiveExplosionDuration:F1}s");
            DiagnosticsHUD.SetStat(StatsSection, "throttles",
                _throttlesLifted ? "LIFTED (unweakened)" : "gameplay defaults");
            // Pure-entity debris in flight (the batched mass-death VFX path). Both
            // families are reported: a blast is all explosions, but a scene with fauna
            // feeding runs suctions through the same carrier, and a stuck implosion
            // count is the first sign the moving-target refresh has wedged.
            DiagnosticsHUD.SetStat(StatsSection, "debris",
                $"{PrismDebris.LiveDebrisCount:N0} exp / {PrismDebris.LiveImplosionDebrisCount:N0} imp");

            UpdateReadout();
        }

        void UpdateReadout()
        {
            if (_readout == null) return;

            float halfDiagonal = Extents.magnitude * 0.5f;
            string coverage = config.FitBlastToLattice
                ? "<color=#80ff80>inscribed — ends at the face centres</color>"
                : EffectiveBlastRadius >= halfDiagonal
                    ? "<color=#80ff80>blast engulfs lattice</color>"
                    : "<color=#ffcc60>blast covers centre only</color>";

            int indexed = IndexedCount();
            string state = _phase switch
            {
                GridPhase.Laying =>
                    $"<color=#ffcc60>laying {_laid:N0}/{_requested:N0}</color>",
                // Prism registers 6/frame process-wide, so this is the long pole — say so, or the
                // operator detonates into a lattice that is not blast-visible yet.
                GridPhase.Materializing =>
                    $"<color=#ffcc60>materializing {indexed:N0}/{_requested:N0} " +
                    $"(~6/frame — wait before detonating)</color>",
                GridPhase.Ready =>
                    $"<color=#80ff80>ready — {indexed:N0} indexed</color> (live {_prisms.Count:N0})",
                _ => "idle",
            };

            SetReadout(
                $"{state}   extents {Extents.x:F0} x {Extents.y:F0} x {Extents.z:F0}   " +
                $"blast r {EffectiveBlastRadius:F0} in {EffectiveExplosionDuration:F1}s " +
                $"vs half-diagonal {halfDiagonal:F0}   {coverage}");
        }

        void SetReadout(string text)
        {
            if (_readout == null) return;
            _readout.text = Time.unscaledTime < _warningUntil
                ? $"{text}\n<color=#ff9060>{_warning}</color>"
                : text;
        }

        /// <summary>
        /// Shows a message that survives the next few stat publishes. Without the expiry stamp a
        /// warning is wiped by whichever PublishStats lands next frame, which is exactly when the
        /// operator is looking away at the thing they just clicked.
        /// </summary>
        void Warn(string message)
        {
            _warning = message;
            _warningUntil = Time.unscaledTime + WarningSeconds;
            Debug.LogWarning($"[PrismGridExplosionHarness] {message}");
            UpdateReadout();
        }

        const string Usage = "usage: grid <x> <y> <z> [gap | gapX gapY gapZ] | grid <total> | " +
                             "grid explode | grid clear | grid zoom <0..1>";

        string HandleGridCommand(string[] args)
        {
            if (args.Length == 0)
                return Usage;

            // Count-first ergonomics: `grid 50000` (also registered as `prisms 50000`)
            // factors a total into a near-cube lattice — the shape operators reach for.
            if (args.Length == 1 && int.TryParse(args[0], out int total) && total > 0)
            {
                int side = Mathf.Max(1, Mathf.RoundToInt(Mathf.Pow(total, 1f / 3f)));
                int x1 = side, y1 = side;
                int z1 = Mathf.Max(1, Mathf.CeilToInt(total / (float)(side * side)));
                _counts = new Vector3Int(x1, y1, z1);
                ClearMixRecipe();
                SyncInputsFromState();
                Spawn();
                return $"spawning {x1}x{y1}x{z1} = {(long)x1 * y1 * z1:N0} prisms (requested {total:N0})";
            }

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

            // One trailing gap applies to all three axes; three set them individually.
            if (args.Length >= 6 &&
                float.TryParse(args[3], out float gx) &&
                float.TryParse(args[4], out float gy) &&
                float.TryParse(args[5], out float gz))
            {
                _gaps = new Vector3(Mathf.Max(0.01f, gx), Mathf.Max(0.01f, gy), Mathf.Max(0.01f, gz));
            }
            else if (args.Length > 3 && float.TryParse(args[3], out float gap))
            {
                _gaps = Vector3.one * Mathf.Max(0.01f, gap);
            }

            _counts = new Vector3Int(Mathf.Max(1, x), Mathf.Max(1, y), Mathf.Max(1, z));
            ClearMixRecipe();
            SyncInputsFromState();
            Spawn();
            return $"spawning {_counts.x}x{_counts.y}x{_counts.z} at gaps {_gaps.x:F1}/{_gaps.y:F1}/{_gaps.z:F1}";
        }

        // ── `lab` — the heterogeneous population ─────────────────────────────

        /// <summary>
        /// Super-shielded ceiling without <c>--force</c>. SuperShielded lazily AddComponents
        /// <c>PrismStellatedOctahedronShield</c>, whose Awake GENERATES A STELLATION MESH PER
        /// PRISM, and both shield tiers swap to an always-on convex MeshCollider that
        /// collider-LOD cannot reclaim (see <see cref="PrismKinds"/>'s own class doc). Shipped
        /// gameplay caps are SuperShielded 1 per microscene. A five-figure super request is a
        /// mesh-generation storm, not a measurement — so the lab REFUSES it rather than letting
        /// the operator discover it as a multi-minute hang.
        /// </summary>
        const int SuperShieldedCeiling = 256;

        /// <summary>Shielded is a collider cost rather than a mesh-generation storm — warn, do not refuse.</summary>
        const int ShieldedWarnAbove = 4096;

        const string LabUsage =
            "usage: lab <count> [plain|danger|shielded|super] [jade|ruby|blue|gold] | " +
            "lab mix plain=N danger=N shielded=N super=N [domains=1|2|3] [--force] | lab clear";

        void ClearMixRecipe()
        {
            _mixKinds = null;
            _mixDomains = null;
            _mixLabel = string.Empty;
        }

        /// <summary>Sizes the lattice to a near-cube holding at least <paramref name="total"/> sites.</summary>
        void SetCubeCountsFor(int total)
        {
            int side = Mathf.Max(1, Mathf.RoundToInt(Mathf.Pow(total, 1f / 3f)));
            int z = Mathf.Max(1, Mathf.CeilToInt(total / (float)(side * side)));
            _counts = new Vector3Int(side, side, z);
        }

        static bool TryParseKind(string token, out PrismKind kind)
        {
            switch (token)
            {
                case "plain": kind = PrismKind.Plain; return true;
                case "danger": kind = PrismKind.Danger; return true;
                case "shielded": kind = PrismKind.Shielded; return true;
                case "super":
                case "supershielded": kind = PrismKind.SuperShielded; return true;
            }
            kind = PrismKind.Plain;
            return false;
        }

        static bool TryParseDomain(string token, out Domains domain)
        {
            switch (token)
            {
                case "jade": domain = Domains.Jade; return true;
                case "ruby": domain = Domains.Ruby; return true;
                case "blue": domain = Domains.Blue; return true;
                case "gold": domain = Domains.Gold; return true;
            }
            domain = Domains.Jade;
            return false;
        }

        string HandleLabCommand(string[] args)
        {
            if (args.Length == 0) return LabUsage;

            string head = args[0].ToLowerInvariant();

            if (head == "clear")
            {
                ClearMixRecipe();
                Clear();
                return "lab cleared (recipe reset; grid emptied)";
            }

            bool force = false;
            for (int i = 0; i < args.Length; i++)
                if (args[i].Equals("--force", StringComparison.OrdinalIgnoreCase)) force = true;

            if (head == "mix") return HandleLabMix(args, force);

            // `lab <count> [kind] [domain]` — one tier, one domain, exact count.
            if (!int.TryParse(head, out int count) || count <= 0) return LabUsage;

            var kind = PrismKind.Plain;
            var domain = config.GridDomain;
            for (int i = 1; i < args.Length; i++)
            {
                string t = args[i].ToLowerInvariant();
                if (t == "--force") continue;
                if (TryParseKind(t, out var k)) { kind = k; continue; }
                if (TryParseDomain(t, out var d)) { domain = d; continue; }
                return $"unrecognised token '{args[i]}'. {LabUsage}";
            }

            string refusal = RefuseIfOverShieldCeiling(
                kind == PrismKind.SuperShielded ? count : 0, force);
            if (refusal != null) return refusal;

            SetCubeCountsFor(count);
            int sites = _counts.x * _counts.y * _counts.z;
            _mixKinds = new PrismKind[sites];
            for (int i = 0; i < sites; i++) _mixKinds[i] = kind;
            _mixDomains = new[] { domain };
            _mixLabel = $"{kind} x{sites:N0} · {domain}";

            SyncInputsFromState();
            Spawn();

            string note = kind == PrismKind.Shielded && sites > ShieldedWarnAbove
                ? $" — WARNING: {sites:N0} shielded prisms mint {sites:N0} always-on convex MeshColliders"
                : string.Empty;
            return $"lab {sites:N0} {kind} {domain}{note}";
        }

        string HandleLabMix(string[] args, bool force)
        {
            int plain = 0, danger = 0, shielded = 0, super = 0, domainCount = 1;

            for (int i = 1; i < args.Length; i++)
            {
                string raw = args[i];
                if (raw.Equals("--force", StringComparison.OrdinalIgnoreCase)) continue;

                int eq = raw.IndexOf('=');
                if (eq <= 0 || eq == raw.Length - 1)
                    return $"expected key=value, got '{raw}'. {LabUsage}";

                string key = raw.Substring(0, eq).ToLowerInvariant();
                if (!int.TryParse(raw.Substring(eq + 1), out int value) || value < 0)
                    return $"'{raw}' needs a non-negative integer. {LabUsage}";

                switch (key)
                {
                    case "plain": plain = value; break;
                    case "danger": danger = value; break;
                    case "shielded": shielded = value; break;
                    case "super":
                    case "supershielded": super = value; break;
                    case "domains": domainCount = value; break;
                    default: return $"unknown key '{key}'. {LabUsage}";
                }
            }

            int total = plain + danger + shielded + super;
            if (total <= 0) return $"mix totals zero prisms. {LabUsage}";
            if (total > config.MaxTotalPrisms)
                return $"{total:N0} exceeds the {config.MaxTotalPrisms:N0} cap " +
                       "(PrismGridTestConfig.maxTotalPrisms).";

            string refusal = RefuseIfOverShieldCeiling(super, force);
            if (refusal != null) return refusal;

            // The lattice is a near-cube, so it holds AT LEAST `total` sites and usually a few
            // more. Sites past the recipe fall back to Plain (BuildLays), which is honest: the
            // census the operator asked for is laid exactly, and the remainder is the cheapest
            // possible filler rather than a silent reweighting of the mix.
            SetCubeCountsFor(total);
            _mixKinds = InterleaveKinds(plain, danger, shielded, super);
            _mixDomains = PlayableDomainCycle(domainCount);
            _mixLabel = $"mix p{plain:N0}/d{danger:N0}/s{shielded:N0}/S{super:N0} · " +
                        $"{_mixDomains.Length} domain{(_mixDomains.Length == 1 ? "" : "s")}";

            SyncInputsFromState();
            Spawn();

            int sites = _counts.x * _counts.y * _counts.z;
            string filler = sites > total ? $" (+{sites - total} plain filler sites)" : string.Empty;
            string note = shielded > ShieldedWarnAbove
                ? $" — WARNING: {shielded:N0} shielded prisms mint that many always-on convex MeshColliders"
                : string.Empty;
            return $"lab mix {total:N0} prisms across {_mixDomains.Length} domain(s){filler}{note}";
        }

        /// <summary>Null when the request is allowed; the refusal text, WITH ITS REASON, when not.</summary>
        string RefuseIfOverShieldCeiling(int superCount, bool force)
        {
            if (force || superCount <= SuperShieldedCeiling) return null;
            return $"REFUSED: {superCount:N0} super-shielded prisms exceeds the lab ceiling of " +
                   $"{SuperShieldedCeiling:N0}. Each one lazily AddComponents " +
                   "PrismStellatedOctahedronShield, whose Awake GENERATES A STELLATION MESH PER " +
                   "PRISM, and swaps to an always-on convex MeshCollider collider-LOD cannot " +
                   "reclaim (shipped gameplay caps are 1 per microscene). That is a " +
                   "mesh-generation storm, not a measurement. Re-run with --force if you meant it.";
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

            // Row 1 — grid dimensions + per-axis gaps.
            CreateLabel("X", panel, new Vector2(8, -8), 14);
            _countXInput = CreateInput(panel, new Vector2(22, -8), 46, _counts.x.ToString(), OnCountsChanged);
            CreateLabel("Y", panel, new Vector2(74, -8), 14);
            _countYInput = CreateInput(panel, new Vector2(88, -8), 46, _counts.y.ToString(), OnCountsChanged);
            CreateLabel("Z", panel, new Vector2(140, -8), 14);
            _countZInput = CreateInput(panel, new Vector2(154, -8), 46, _counts.z.ToString(), OnCountsChanged);
            CreateLabel("gaps", panel, new Vector2(212, -8), 38);
            _gapXInput = CreateInput(panel, new Vector2(250, -8), 46, _gaps.x.ToString("F1"), OnCountsChanged);
            _gapYInput = CreateInput(panel, new Vector2(300, -8), 46, _gaps.y.ToString("F1"), OnCountsChanged);
            _gapZInput = CreateInput(panel, new Vector2(350, -8), 46, _gaps.z.ToString("F1"), OnCountsChanged);

            // Row 2 — actions.
            CreateButton("Spawn", panel, new Vector2(8, -40), 90, Spawn);
            CreateButton("Explode", panel, new Vector2(104, -40), 90, Explode);
            CreateButton("Clear", panel, new Vector2(200, -40), 90, Clear);
            CreateButton("Bench", panel, new Vector2(296, -40), 90,
                () => GetComponent<PrismExplosionBenchmark>()?.StartSeries());

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
            if (float.TryParse(_gapXInput.text, out float gx)) _gaps.x = Mathf.Max(0.01f, gx);
            if (float.TryParse(_gapYInput.text, out float gy)) _gaps.y = Mathf.Max(0.01f, gy);
            if (float.TryParse(_gapZInput.text, out float gz)) _gaps.z = Mathf.Max(0.01f, gz);

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
            if (_gapXInput != null) _gapXInput.text = _gaps.x.ToString("F1");
            if (_gapYInput != null) _gapYInput.text = _gaps.y.ToString("F1");
            if (_gapZInput != null) _gapZInput.text = _gaps.z.ToString("F1");
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
            // Slider.UpdateVisuals overwrites the handle's anchors each frame but never its pivot,
            // so the shared CreateRect pivot of (0,1) would hang the handle down-and-left of the
            // track and push it fully outside at value 1. Centre it.
            handleRT.pivot = new Vector2(0.5f, 0.5f);
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
