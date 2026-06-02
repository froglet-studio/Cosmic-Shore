// Cell.cs
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Game;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using Random = UnityEngine.Random;
namespace CosmicShore.Gameplay
{
    public class Cell : MonoBehaviour
    {
        enum CellTypeChoiceOptions { Random, IntensityWise }

        [SerializeField] public int ID;

        [Header("Cell Config Selection")]
        [SerializeField] List<CellConfigDataSO> CellConfigs;   // NEW (replaces CellTypes)
        [SerializeField] CellTypeChoiceOptions cellTypeChoiceOptions = CellTypeChoiceOptions.Random;

        [Header("Runtime Data")]
        [SerializeField] CellRuntimeDataSO runtime;
        [Inject] GameDataSO gameData;

        [SerializeField] float nucleusScaleMultiplier = 1f;

        // Local phase recompute interval. Constant rather than a serialized field so
        // existing scene-placed Cells deserialized before this tick existed don't end
        // up with phaseTickIntervalSeconds=0 (the default(float) for new serialized
        // fields), which would silently disable phase advancement.
        const float PhaseTickIntervalSeconds = 0.5f;

        float _nextPhaseTickAt;


        CellConfigDataSO cellConfigData => runtime ? runtime.Config : null;
        public CellConfigDataSO Config => cellConfigData;
        GameObject membrane;
        GameObject nucleus;

        public float NucleusRadius => nucleus ? nucleus.transform.localScale.x : 0f;
        public float MembraneRadius
        {
            get
            {
                if (!membrane) return 0f;
                if (membrane.TryGetComponent<CapsuleMembrane>(out var cm))
                    return cm.Radius;
                return membrane.transform.localScale.x;
            }
        }

        public Dictionary<Domains, BlockCountDensityGrid> countGrids = new();
        public Dictionary<Domains, BlockVolumeDensityGrid> volumeGrids = new();
        readonly Dictionary<Domains, float> teamVolumes = new();
        readonly Dictionary<Domains, int> domainBlockCounts = new();

        readonly List<GameObject> spawnedLifeForms = new();
        // Prism → the domain it was REGISTERED under. RemoveBlock decrements the
        // grids/counts for the registration-time domain, not the prism's current
        // one — so steals / ChangeTeam between Add and Remove can't desync the
        // per-domain bookkeeping (the §2.3.1 phantom-count class of bug).
        readonly Dictionary<Prism, Domains> trackedBlocks = new();
        SnowChanger spawnedCytoplasm;

        // ---------------------------------------------------------------------
        // Static spatial registry. Pooled prefab-spawned objects (trail prisms)
        // use this to find their containing cell — they have no scene identity
        // to wire a CellRuntimeDataSO into, and the per-prefab-asset alternative
        // breaks in multi-cell scenes where one prefab would need to point at
        // every cell's runtime SO at once.
        // ---------------------------------------------------------------------
        static readonly List<Cell> ActiveCells = new();

        /// <summary>
        /// The enabled cell whose membrane contains <paramref name="position"/>,
        /// or null when the position is in open space. O(cells-in-scene) — call
        /// at object lifecycle points (spawn/destroy), not per frame.
        /// </summary>
        public static Cell FindCellContaining(Vector3 position)
        {
            for (int i = 0; i < ActiveCells.Count; i++)
            {
                var c = ActiveCells[i];
                if (c && c.ContainsPosition(position))
                    return c;
            }
            return null;
        }

        /// <summary>
        /// The enabled cell whose transform is closest to <paramref name="position"/>,
        /// or null if no cells are active. Distance is centre-to-point; ties resolve
        /// in iteration order. Useful as a fallback for HUDs that need *a* cell to
        /// read state from when the player isn't inside any (e.g. Menu_Main's
        /// orbital camera, between-cell transit).
        /// </summary>
        public static Cell FindNearestActiveCell(Vector3 position)
        {
            Cell best = null;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < ActiveCells.Count; i++)
            {
                var c = ActiveCells[i];
                if (!c) continue;
                float d = (c.transform.position - position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = c; }
            }
            return best;
        }

        CellPhase phase = CellPhase.Sprout;

        /// <summary>
        /// Live count of unique prisms tracked through Add/RemoveBlock. Read-only signal
        /// for systems that respond to prism load (e.g., LightFaunaManager scales its
        /// fauna population with this so consumption keeps pace with growth, and the
        /// phase system gates flora and fauna behavior on it).
        /// </summary>
        public int LiveBlockCount => trackedBlocks.Count;

        /// <summary>
        /// Live leader by per-domain prism count. Recomputed on demand so the answer
        /// always reflects the current Add/RemoveBlock-driven counts. Returns
        /// <see cref="Domains.Blue"/> (the "no team" sentinel) when the cell has no
        /// prisms tracked yet. Ties resolve in fixed order (Jade > Ruby > Gold > Blue).
        /// </summary>
        public Domains DominantDomain
        {
            get
            {
                Domains leader = Domains.Blue;
                int leaderCount = 0;
                Domains[] order = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };
                foreach (var d in order)
                {
                    if (!domainBlockCounts.TryGetValue(d, out int c)) continue;
                    if (c > leaderCount)
                    {
                        leader = d;
                        leaderCount = c;
                    }
                }
                return leader;
            }
        }

        /// <summary>
        /// Live count of prisms tracked under <paramref name="domain"/>. Mirrors the
        /// per-domain bookkeeping that <see cref="DominantDomain"/> reads, exposed so
        /// HUD widgets (volume wedges, etc.) don't need to walk Add/RemoveBlock state
        /// themselves. Returns 0 for untracked domains.
        /// </summary>
        public int GetDomainBlockCount(Domains domain) =>
            domainBlockCounts.TryGetValue(domain, out int c) ? c : 0;

        /// <summary>
        /// LiveBlockCount at which the cell crosses into Rabid (frenzy). HUD widgets
        /// use this as the "max" — when summed mass approaches it, the cell is about
        /// to enter Level2 aggression and the UI should communicate that.
        /// </summary>
        public int RabidEnterThreshold => ResolveThresholds().RabidEnter;

        /// <summary>
        /// Current phase. Written exclusively by <see cref="CellNetworkSync"/> via
        /// <see cref="ApplyAuthoritativePhaseAndDomain"/> — the server's compute on a
        /// networked cell, or the local-only fallback in single-player. Cell never
        /// recomputes phase itself; it just exposes the inputs.
        /// </summary>
        public CellPhase Phase => phase;

        // ---------------------------------------------------------------------
        // Derived gates — orthogonal projections of Phase the consumers actually
        // care about. The user spec mixes flora and fauna events along a single
        // prism-count axis; these properties decouple the two systems' rules so
        // each consumer reads only what it needs.
        // ---------------------------------------------------------------------

        /// <summary>True while the cell still allows new flora to be planted (Phase &lt; Settled).</summary>
        public bool FloraPlantingEnabled => phase < CellPhase.Settled;

        /// <summary>True while existing flora may still grow new prisms (Phase &lt; Frozen).</summary>
        public bool FloraGrowingEnabled => phase < CellPhase.Frozen;

        /// <summary>True once the cell has crossed the fauna-spawn threshold (Phase &gt;= Quiet).</summary>
        public bool FaunaSpawningEnabled => phase >= CellPhase.Quiet;

        /// <summary>
        /// Fauna aggression level derived from <see cref="Phase"/>:
        ///   Sprout/Quiet/Settled → Level0  (head toward crystal, normal cadence)
        ///   Restless/Frozen      → Level1  (head toward opposing-color centroid)
        ///   Rabid                → Level2  (any-domain centroid, drop friendly avoidance)
        /// </summary>
        public CellAggressionLevel AggressionLevel => phase switch
        {
            CellPhase.Restless => CellAggressionLevel.Level1,
            CellPhase.Frozen => CellAggressionLevel.Level1,
            CellPhase.Rabid => CellAggressionLevel.Level2,
            _ => CellAggressionLevel.Level0,
        };

        /// <summary>
        /// "Controlling color" for fauna spawns. Prefers the cell's live
        /// <see cref="DominantDomain"/> (per-domain prism count leader), then falls
        /// back to gameData's controlling team by remaining volume, then to the local
        /// player's domain (useful in Menu_Main where there is no scored controlling
        /// team), then to Jade as a last resort. Never returns Blue (the "no team"
        /// sentinel) — callers can use it directly without further branching.
        /// </summary>
        public Domains ControllingDomain
        {
            get
            {
                var dominant = DominantDomain;
                if (dominant != Domains.Blue)
                    return dominant;

                if (gameData != null)
                {
                    var top = gameData.GetControllingTeamStatsBasedOnVolumeRemaining();
                    if (top.Team != Domains.Blue && top.Volume > 0f)
                        return top.Team;

                    var local = gameData.LocalRoundStats?.Domain
                                ?? gameData.LocalPlayer?.Domain
                                ?? Domains.Blue;
                    if (local != Domains.Blue)
                        return local;
                }
                return Domains.Jade;
            }
        }

        /// <summary>
        /// Sole entry point for phase mutation. Updates the local field and the
        /// runtime SO's per-cell stats; the runtime SO raises <c>OnPhaseChanged</c>
        /// when the value transitions. Both <see cref="CellNetworkSync"/>'s server
        /// tick and its <c>OnValueChanged</c> client listener route through here so
        /// the runtime SO is the single observable source of truth on every machine.
        /// </summary>
        public void ApplyAuthoritativePhaseAndDomain(CellPhase newPhase, Domains newDominantDomain)
        {
            phase = newPhase;
            if (runtime != null)
                runtime.WriteCellRuntimeStats(ID, LiveBlockCount, newPhase, newDominantDomain);
        }

        void Update()
        {
            // Drive phase locally every tick interval. Server-authoritative replication
            // (CellNetworkSync) overlays this on networked clients via OnValueChanged
            // — server's compute wins when the two diverge — but for single-player and
            // for the server itself this is the only path that advances phase. Without
            // it, no fauna ever spawn because phase stays at Sprout forever.
            if (Time.time < _nextPhaseTickAt) return;
            _nextPhaseTickAt = Time.time + PhaseTickIntervalSeconds;

            var thresholds = ResolveThresholds();
            var newPhase = CellPhaseRules.Compute(LiveBlockCount, phase, in thresholds);
            ApplyAuthoritativePhaseAndDomain(newPhase, DominantDomain);
        }

        CellPhaseThresholds ResolveThresholds()
        {
            var cfg = cellConfigData;
            if (!cfg) return CellPhaseThresholds.Default;

            // Existing CellConfig assets serialized before PhaseThresholds existed
            // deserialize as struct zero — Unity does not apply the C# initializer.
            // Substitute the Default table so legacy biomes don't snap to Rabid the
            // moment the first prism is added.
            var t = cfg.PhaseThresholds;
            return t.IsAllZero ? CellPhaseThresholds.Default : t;
        }

        readonly ICellLifeSpawner intensitySpawner = new IntensityWiseLifeSpawner();
        readonly ICellLifeSpawner randomSpawner = new RandomLifeSpawner();
        ICellLifeSpawner activeSpawner;
        bool postInitilized = false;

        void OnEnable()
        {
            // Spatial registry — lets pooled, prefab-spawned objects (trail prisms)
            // find which cell contains them without per-prefab SO wiring or the
            // deprecated CellControlManager singleton. See FindCellContaining.
            if (!ActiveCells.Contains(this))
                ActiveCells.Add(this);

            // Clear stale config BEFORE subscribing to events.
            // CellRuntimeDataSO is a shared SO asset — Menu_Main's Cell sets
            // runtime.Config to Blob Cell Config, which persists into the next
            // scene. Without clearing here, OnCellItemsUpdated could fire between
            // OnEnable (subscription) and Start (where the clear previously lived),
            // causing InitilizePostFirstCellItem to use the stale config and spawn
            // flora from the wrong CellConfig. This was the root cause of Gyroids
            // appearing on clients in HexRace despite using a Barren Cell Config.
            if (runtime != null)
                runtime.Config = null;

            if (gameData != null)
                gameData.OnInitializeGame.OnRaised += Initialize;

            if (!runtime) return;

            // We keep events ONLY in runtime.
            if (runtime.OnCellItemsUpdated != null)
                runtime.OnCellItemsUpdated.OnRaised += OnCellItemUpdated;

            if (runtime.OnResetForReplay != null)
                runtime.OnResetForReplay.OnRaised += ResetCell;
        }

        void Start()
        {
            // [Inject] fields aren't available in OnEnable. Retry subscription
            // here with deduplicate guard so Initialize() fires on OnInitializeGame.
            if (gameData != null)
            {
                gameData.OnInitializeGame.OnRaised -= Initialize;
                gameData.OnInitializeGame.OnRaised += Initialize;
            }
        }

        void OnDisable()
        {
            ActiveCells.Remove(this);

            if (gameData != null)
                gameData.OnInitializeGame.OnRaised -= Initialize;

            if (runtime != null)
            {
                if (runtime.OnCellItemsUpdated != null)
                    runtime.OnCellItemsUpdated.OnRaised -= OnCellItemUpdated;

                if (runtime.OnResetForReplay != null)
                    runtime.OnResetForReplay.OnRaised -= ResetCell;
            }

            if (spawnedCytoplasm)
            {
                Destroy(spawnedCytoplasm.gameObject);
                spawnedCytoplasm = null;
            }

            StopSpawner();
            runtime?.ResetRuntimeData();
        }

        void ResetCell()
        {
            // Destroy all spawned lifeforms
            for (int i = spawnedLifeForms.Count - 1; i >= 0; i--)
            {
                if (spawnedLifeForms[i]) Destroy(spawnedLifeForms[i]);
            }
            spawnedLifeForms.Clear();
            trackedBlocks.Clear();
            domainBlockCounts.Clear();
            phase = CellPhase.Sprout;

            if (spawnedCytoplasm)
            {
                Destroy(spawnedCytoplasm.gameObject);
                spawnedCytoplasm = null;
            }

            StopSpawner();
            AssignConfig();
            ResetVolumes();

            runtime.EnsureCellStats(ID);
            UpdateCellStats();
        }

        void UpdateCellStats()
        {
            if (!runtime) return;

            runtime.EnsureCellStats(ID);
            var cs = runtime.CellStatsList[ID];
            cs.LifeFormsInCell = spawnedLifeForms.Count;
        }

        /// <summary>
        /// Toggles visibility of all spawned lifeforms (flora/fauna).
        /// Used to hide flora during shape drawing mode and restore after.
        /// </summary>
        public void SetLifeFormsActive(bool active)
        {
            for (int i = spawnedLifeForms.Count - 1; i >= 0; i--)
            {
                if (spawnedLifeForms[i])
                    spawnedLifeForms[i].SetActive(active);
            }
        }

        public void RegisterSpawnedObject(GameObject obj)
        {
            if (!obj) return;
            spawnedLifeForms.Add(obj);
            UpdateCellStats();
        }

        public void UnregisterSpawnedObject(GameObject obj)
        {
            if (spawnedLifeForms.Remove(obj))
                UpdateCellStats();
        }

        void Initialize()
        {
            spawnedLifeForms.Clear();
            trackedBlocks.Clear();
            domainBlockCounts.Clear();
            phase = CellPhase.Sprout;

            // Bind runtime -> this cell
            runtime.Cell = this;
            runtime.EnsureCellStats(ID);

            AssignConfig();
            // SpawnVisuals must run before SetupDensityGrids: the density grids
            // are now sized to the cell's membrane radius, and MembraneRadius
            // reads the membrane GameObject that SpawnVisuals instantiates.
            SpawnVisuals();
            SetupDensityGrids();
            ResetVolumes();

            UpdateCellStats();
        }
        
        void InitilizePostFirstCellItem()
        {
            postInitilized = true;
            if (!cellConfigData)
            {
                CSDebug.LogWarning($"[Cell {ID}] Crystal spawned before Cell Initialized. Attempting lazy init.");
                Initialize();
                if (!cellConfigData) return;
            }

            SpawnCytoplasm();
            ApplyModifiers();
            SpawnCytoplasm();
            StartSpawnerForMode();
        }

        void OnCellItemUpdated()
        {
            if (postInitilized)
                return;
            InitilizePostFirstCellItem();
        }

        void AssignConfig()
        {
            if (CellConfigs == null || CellConfigs.Count == 0)
            {
                CSDebug.LogError($"{nameof(Cell)}: No CellConfigs found to assign.");
                return;
            }

            var index = cellTypeChoiceOptions switch
            {
                CellTypeChoiceOptions.Random => Random.Range(0, CellConfigs.Count),
                CellTypeChoiceOptions.IntensityWise => Mathf.Clamp(gameData.SelectedIntensity.Value - 1, 0, CellConfigs.Count - 1),
                _ => 0
            };

            runtime.Config = CellConfigs[index];
        }

        void SetupDensityGrids()
        {
            // Size the density grids to the cell's actual membrane, not the old
            // hard-coded 1000m cube anchored at world origin. With a 1200m-radius
            // membrane the fixed cube saw only ~14% of the cell's volume — outer-
            // shell mass was invisible to FindDensestRegion, so fauna were never
            // directed at it and it accumulated unconsumed. See
            // Docs/DENSITY_PARTITIONING_AUDIT.md.
            float membraneRadius = MembraneRadius;
            float worldDiameter = membraneRadius > 0f
                ? membraneRadius * 2f
                : 2400f; // fallback when the membrane prefab is missing
            Vector3 cellCenter = transform.position;

            // Dispose any existing grids before replacing them — each holds
            // persistent NativeArrays, and Initialize() can run more than once
            // across a session (e.g. replay).
            foreach (var existing in countGrids.Values)
                existing?.Dispose();

            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
            countGrids.Clear();
            foreach (Domains t in teams)
                countGrids[t] = new BlockCountDensityGrid(t, cellCenter, worldDiameter);

            // Blue-keyed grid accumulates every block regardless of domain so
            // GetDensestRegionAnyDomain() can answer aggression-2 fauna's "head toward
            // nearest centroid" goal — friendly + enemy mass both count. Blue is the
            // "no specific team" sentinel; this grid does double duty as the wildcard.
            countGrids[Domains.Blue] = new BlockCountDensityGrid(Domains.Blue, cellCenter, worldDiameter);
        }

        void SpawnVisuals()
        {
            if (!cellConfigData) return;

            if (cellConfigData.MembranePrefab != null)
                membrane = Instantiate(cellConfigData.MembranePrefab, transform.position, Quaternion.identity);

            if (cellConfigData.NucleusPrefab == null) return;
            nucleus = Instantiate(cellConfigData.NucleusPrefab, transform.position, Quaternion.identity);
            nucleus.transform.localScale *= nucleusScaleMultiplier;
        }

        void ResetVolumes()
        {
            teamVolumes[Domains.Jade] = 0;
            teamVolumes[Domains.Ruby] = 0;
            teamVolumes[Domains.Gold] = 0;
            teamVolumes[Domains.Blue] = 0;
        }

        void ApplyModifiers()
        {
            var cfg = cellConfigData;
            if (!cfg || cfg.CellModifiers == null) return;

            foreach (var modifier in cfg.CellModifiers)
                modifier.Apply(this);
        }

        void SpawnCytoplasm()
        {
            if (!cellConfigData || cellConfigData.CytoplasmPrefab == null) return;

            spawnedCytoplasm = Instantiate(cellConfigData.CytoplasmPrefab, transform.position, Quaternion.identity);
            spawnedCytoplasm.SetOrigin(transform.position);
            spawnedCytoplasm.Initialize();
        }

        void StartSpawnerForMode()
        {
            StopSpawner();

            activeSpawner = cellTypeChoiceOptions == CellTypeChoiceOptions.IntensityWise
                ? intensitySpawner
                : randomSpawner;

            activeSpawner.Start(this, cellConfigData, runtime, gameData);

            CSDebug.Log($"<color=green>[Cell {ID}] Spawner started: {activeSpawner.GetType().Name}</color>");
        }

        void StopSpawner()
        {
            if (activeSpawner == null) return;
            activeSpawner.Stop(this);
            activeSpawner = null;
            CSDebug.Log($"<color=yellow>[Cell {ID}] Spawner stopped</color>");
        }

        internal Transform GetCrystalTransform()
        {
            if (runtime != null && runtime.TryGetLocalCrystal(out var crystal) && crystal)
                return crystal.transform;

            CSDebug.LogWarning($"[Cell {ID}] No crystal found!");
            return null;
        }

        public void AddBlock(Prism block)
        {
            // `is null` (not `!block`) so destroyed-but-non-null Unity refs can still be
            // removed from trackedBlocks via the matching RemoveBlock path; otherwise
            // LiveBlockCount drifts upward when prisms die outside the normal flow.
            if (block is null) return;
            if (trackedBlocks.ContainsKey(block)) return; // already counted

            // Snapshot the domain at registration time — RemoveBlock uses this snapshot
            // so a team change (steal) between Add and Remove can't desync the grids.
            Domains registeredDomain = block ? block.Domain : Domains.Blue;
            trackedBlocks[block] = registeredDomain;

            if (block)
            {
                Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
                foreach (var t in teams)
                    if (t != registeredDomain) countGrids[t].AddBlock(block);

                if (countGrids.TryGetValue(Domains.Blue, out var anyGrid))
                    anyGrid.AddBlock(block);

                domainBlockCounts.TryGetValue(registeredDomain, out int count);
                domainBlockCounts[registeredDomain] = count + 1;
            }
        }

        public void RemoveBlock(Prism block)
        {
            if (block is null) return;
            if (!trackedBlocks.Remove(block, out Domains registeredDomain)) return; // not counted

            if (block)
            {
                Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
                foreach (Domains t in teams)
                    if (t != registeredDomain) countGrids[t].RemoveBlock(block);

                if (countGrids.TryGetValue(Domains.Blue, out var anyGrid))
                    anyGrid.RemoveBlock(block);

                if (domainBlockCounts.TryGetValue(registeredDomain, out int count) && count > 0)
                    domainBlockCounts[registeredDomain] = count - 1;
            }
        }

        /// <summary>
        /// Re-registers a tracked prism whose domain changed (steal / ChangeTeam) so the
        /// per-domain grids and counts move it from the old domain's buckets to the new
        /// one's. No-op for prisms this cell isn't tracking.
        /// </summary>
        public void NotifyBlockDomainChanged(Prism block)
        {
            if (block is null || !trackedBlocks.ContainsKey(block)) return;
            RemoveBlock(block);
            AddBlock(block);
        }

        /// <summary>
        /// Densest region of all blocks NOT belonging to the given domain — the
        /// "nearest opposing-color centroid" for fauna at aggression Level 1.
        /// Empty grids default to the cell anchor (crystal or cell transform)
        /// instead of the grid's bottom-corner sentinel, which otherwise pulled
        /// every fauna querying an empty grid to the world-space −X/−Y/−Z corner.
        /// </summary>
        public Vector3 GetExplosionTarget(Domains domain)
        {
            if (!countGrids.TryGetValue(domain, out var grid) || grid == null)
                return GetCellAnchorPosition();

            var region = grid.FindDensestRegion();
            // LastResultDensity is the peak smoothed density the job found — 0 means
            // the grid is empty. (Checking GetDensityAtPosition(region) here instead
            // would false-negative when sub-voxel interp / mean-shift lands the
            // answer in a low-count voxel adjacent to the true mass.)
            if (grid.LastResultDensity <= 0f)
                return GetCellAnchorPosition();
            return region;
        }

        /// <summary>
        /// Densest region across all domains — the "nearest centroid of any color"
        /// goal for fauna at aggression Level 2. Reads the synthesized
        /// countGrids[Domains.Blue] grid that <see cref="AddBlock"/> populates with
        /// every block regardless of its domain (Blue serves double-duty as the
        /// "no specific team" sentinel and the all-domain wildcard bucket).
        /// </summary>
        public Vector3 GetDensestRegionAnyDomain()
        {
            if (!countGrids.TryGetValue(Domains.Blue, out var anyGrid) || anyGrid == null)
                return GetCellAnchorPosition();

            var region = anyGrid.FindDensestRegion();
            if (anyGrid.LastResultDensity <= 0f)
                return GetCellAnchorPosition();
            return region;
        }

        /// <summary>
        /// Alias for <see cref="GetDensestRegionAnyDomain"/> — historical name from
        /// the gyroid-overflow regulation work, kept so external callers can use
        /// either spelling.
        /// </summary>
        public Vector3 GetPrimaryCentroid() => GetDensestRegionAnyDomain();

        /// <summary>
        /// Fallback position for goal resolution when density grids are empty:
        /// the local crystal if one exists, otherwise the cell's own transform.
        /// Keeps fauna near the cell instead of drifting to the empty-grid corner.
        /// </summary>
        Vector3 GetCellAnchorPosition()
        {
            if (runtime != null && runtime.CrystalTransform)
                return runtime.CrystalTransform.position;
            return transform.position;
        }

        public bool ContainsPosition(Vector3 position)
        {
            // MembraneRadius reads CapsuleMembrane.Radius when present (the authored
            // radius), falling back to localScale.x — strictly more correct than the
            // old raw localScale read for capsule membranes.
            float radius = MembraneRadius;
            if (radius <= 0f) return false;
            return Vector3.Distance(position, transform.position) < radius;
        }

        public void ChangeVolume(Domains domain, float volume)
        {
            teamVolumes.TryAdd(domain, 0);
            teamVolumes[domain] += volume;
        }

        public float GetTeamVolume(Domains domain)
        {
            return teamVolumes.GetValueOrDefault(domain, 0);
        }


        internal Domains GetHostileDomainToLocalLegacy()
        {
            var local = gameData.LocalRoundStats?.Domain ?? Domains.Jade;
            var candidates = new[] { Domains.Ruby, Domains.Gold, Domains.Blue, Domains.Jade };
            return candidates.First(d => d != local);
        }
    }
}