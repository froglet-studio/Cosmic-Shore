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
        readonly HashSet<Prism> trackedBlocks = new();
        SnowChanger spawnedCytoplasm;

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
        /// <see cref="Domains.None"/> when the cell has no prisms tracked yet.
        /// Ties resolve in fixed order (Jade > Ruby > Gold > Blue) so two clients with
        /// the same per-domain counts pick the same leader.
        /// </summary>
        public Domains DominantDomain
        {
            get
            {
                Domains leader = Domains.None;
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
        /// Current phase. Written exclusively by <see cref="CellNetworkSync"/> via
        /// <see cref="ApplyAuthoritativePhaseAndDomain"/> — the server's compute on a
        /// networked cell, or the local-only fallback in single-player. Cell never
        /// recomputes phase itself; it just exposes the inputs.
        /// </summary>
        public CellPhase Phase => phase;

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
            SetupDensityGrids();
            SpawnVisuals();
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
            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };
            countGrids.Clear();
            foreach (Domains t in teams)
                countGrids[t] = new BlockCountDensityGrid(t);

            // None-keyed grid accumulates every block regardless of domain so
            // GetDensestRegionAnyDomain() can answer aggression-2 fauna's "head toward
            // nearest centroid" goal — friendly + enemy mass both count.
            countGrids[Domains.None] = new BlockCountDensityGrid(Domains.None);
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
            if (!trackedBlocks.Add(block)) return; // already counted

            if (block)
            {
                Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
                foreach (var t in teams)
                    if (t != block.Domain) countGrids[t].AddBlock(block);

                if (countGrids.TryGetValue(Domains.None, out var anyGrid))
                    anyGrid.AddBlock(block);

                domainBlockCounts.TryGetValue(block.Domain, out int count);
                domainBlockCounts[block.Domain] = count + 1;
            }
        }

        public void RemoveBlock(Prism block)
        {
            if (block is null) return;
            if (!trackedBlocks.Remove(block)) return; // not counted

            if (block)
            {
                Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
                foreach (Domains t in teams)
                    if (t != block.Domain) countGrids[t].RemoveBlock(block);

                if (countGrids.TryGetValue(Domains.None, out var anyGrid))
                    anyGrid.RemoveBlock(block);

                if (domainBlockCounts.TryGetValue(block.Domain, out int count) && count > 0)
                    domainBlockCounts[block.Domain] = count - 1;
            }
        }

        public Vector3 GetExplosionTarget(Domains domain) => countGrids[domain].FindDensestRegion();

        /// <summary>
        /// Densest region across all domains, used by aggression-2 fauna whose goal
        /// drops the opposing-domain qualifier and seeks the heaviest mass concentration
        /// regardless of who owns it. Falls back to the cell's transform position if
        /// the all-domain grid wasn't initialized (defensive — Initialize seeds it).
        /// </summary>
        public Vector3 GetDensestRegionAnyDomain()
        {
            if (countGrids.TryGetValue(Domains.None, out var anyGrid))
                return anyGrid.FindDensestRegion();
            return transform.position;
        }

        public bool ContainsPosition(Vector3 position)
        {
            if (membrane is null) return false;
            return Vector3.Distance(position, transform.position) < membrane.transform.localScale.x;
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