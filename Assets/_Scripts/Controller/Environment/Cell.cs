// Cell.cs
using System;
using System.Collections;
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

        // ---------------------------------------------------------------------
        // Regulation thresholds with hysteresis.
        //
        // The cell tracks a single prism count (_liveBlockCount) and drives five
        // independent regulation gates along that axis. The rising and falling
        // values differ to buffer against thrashing at the boundary:
        //
        //   0          - flora begin spawning with random domains   (default-on)
        //   1000       - fauna begin spawning in the controlling color (L0)
        //   4000       - flora stop planting
        //   8000       - fauna escalate to L1 (opposing-centroid seeking)
        //   10000      - flora stop growing
        //   15000      - fauna escalate to L2 (any centroid, no friendly avoidance)
        //
        // Tune per-biome via CellConfigDataSO later if needed; for now these live
        // on the Cell so per-scene tuning is possible.
        // ---------------------------------------------------------------------

        [System.Serializable]
        public struct PrismThreshold
        {
            [Min(0)] public int Rising;
            [Min(0)] public int Falling;
        }

        [Header("Regulation Thresholds (prism count, hysteresis)")]
        [Tooltip("Count at or above which fauna begin spawning (in controlling color).")]
        [SerializeField] PrismThreshold faunaSpawnStart = new() { Rising = 1000, Falling = 750 };
        [Tooltip("Count at or above which flora stop planting new instances.")]
        [SerializeField] PrismThreshold floraPlantEnd = new() { Rising = 4000, Falling = 3500 };
        [Tooltip("Count at or above which fauna escalate to aggression Level 1.")]
        [SerializeField] PrismThreshold faunaAggressionL1 = new() { Rising = 8000, Falling = 7000 };
        [Tooltip("Count at or above which flora stop growing existing instances.")]
        [SerializeField] PrismThreshold floraGrowEnd = new() { Rising = 10000, Falling = 9000 };
        [Tooltip("Count at or above which fauna escalate to aggression Level 2 (berserk).")]
        [SerializeField] PrismThreshold faunaAggressionL2 = new() { Rising = 15000, Falling = 13000 };

        [Tooltip("Seconds between regulation state re-evaluations.")]
        [SerializeField, Min(0.1f)] float aggressionPollInterval = 1.5f;


        CellConfigDataSO cellConfigData => runtime ? runtime.Config : null;
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

        readonly List<GameObject> spawnedLifeForms = new();
        SnowChanger spawnedCytoplasm;

        readonly ICellLifeSpawner intensitySpawner = new IntensityWiseLifeSpawner();
        readonly ICellLifeSpawner randomSpawner = new RandomLifeSpawner();
        ICellLifeSpawner activeSpawner;
        bool postInitilized = false;

        // Aggression state. Incremented / decremented in O(1) by AddBlock / RemoveBlock
        // so we never iterate density grids to measure pressure.
        int _liveBlockCount;
        CellAggressionLevel _aggressionLevel = CellAggressionLevel.Level0;
        bool _floraPlantingEnabled = true;   // default on; gated down by rising threshold
        bool _floraGrowingEnabled = true;    // default on; gated down by rising threshold
        bool _faunaSpawningEnabled = false;  // default off; gated up by rising threshold
        Coroutine _aggressionPollRoutine;

        // Dedupes AddBlock/RemoveBlock across multiple call paths (flora binding,
        // HealthPrism.Initialize, future direct callers) so _liveBlockCount stays accurate.
        readonly HashSet<Prism> _registeredBlocks = new();

        /// <summary>Current live prism count in this cell.</summary>
        public int LiveBlockCount => _liveBlockCount;

        /// <summary>Current fauna aggression level derived from <see cref="LiveBlockCount"/> with hysteresis.</summary>
        public CellAggressionLevel AggressionLevel => _aggressionLevel;

        /// <summary>Is the cell currently allowing flora to plant new instances?</summary>
        public bool FloraPlantingEnabled => _floraPlantingEnabled;

        /// <summary>Is the cell currently allowing existing flora to grow new prisms?</summary>
        public bool FloraGrowingEnabled => _floraGrowingEnabled;

        /// <summary>Is the cell currently allowing new fauna to spawn?</summary>
        public bool FaunaSpawningEnabled => _faunaSpawningEnabled;

        /// <summary>Raised when <see cref="AggressionLevel"/> transitions. New level is the argument.</summary>
        public event Action<CellAggressionLevel> OnAggressionChanged;

        /// <summary>
        /// Resolves the "controlling color" for fauna spawns. Falls back to local player
        /// domain (useful in Menu_Main where there is no scored controlling team), then
        /// to Jade as last resort.
        /// </summary>
        public Domains ControllingDomain
        {
            get
            {
                if (gameData != null)
                {
                    var top = gameData.GetControllingTeamStatsBasedOnVolumeRemaining();
                    if (top.Team != Domains.None && top.Team != Domains.Unassigned && top.Volume > 0f)
                        return top.Team;

                    var local = gameData.LocalRoundStats?.Domain
                                ?? gameData.LocalPlayer?.Domain
                                ?? Domains.Unassigned;
                    if (local != Domains.None && local != Domains.Unassigned)
                        return local;
                }
                return Domains.Jade;
            }
        }

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
            StopAggressionPoll();
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

            if (spawnedCytoplasm)
            {
                Destroy(spawnedCytoplasm.gameObject);
                spawnedCytoplasm = null;
            }

            StopSpawner();
            AssignConfig();
            ResetVolumes();

            ResetRegulationState();
            StartAggressionPoll();

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

            // Bind runtime -> this cell
            runtime.Cell = this;
            runtime.EnsureCellStats(ID);

            AssignConfig();
            SetupDensityGrids();
            SpawnVisuals();
            ResetVolumes();

            ResetRegulationState();
            StartAggressionPoll();

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
            if (!block) return;
            if (!_registeredBlocks.Add(block)) return; // already registered

            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
            foreach (var t in teams)
                if (t != block.Domain) countGrids[t].AddBlock(block);

            _liveBlockCount++;
        }

        public void RemoveBlock(Prism block)
        {
            if (!block) return;
            if (!_registeredBlocks.Remove(block)) return; // wasn't tracked

            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
            foreach (Domains t in teams)
                if (t != block.Domain) countGrids[t].RemoveBlock(block);

            if (_liveBlockCount > 0) _liveBlockCount--;
        }

        // Hysteresis rule: when a gate is OPEN (default-on state), it stays open until
        // count crosses the Rising threshold; once closed, it reopens only after count
        // drops below the Falling threshold. Inverted for default-off gates.
        static bool EvaluateDefaultOnGate(bool currentlyOpen, int count, PrismThreshold t)
        {
            return currentlyOpen ? (count < t.Rising) : (count < t.Falling);
        }

        static bool EvaluateDefaultOffGate(bool currentlyOpen, int count, PrismThreshold t)
        {
            return currentlyOpen ? (count >= t.Falling) : (count >= t.Rising);
        }

        CellAggressionLevel BucketForCount(int count, CellAggressionLevel current)
        {
            // Each level acts like a default-off gate: cross Rising to activate, drop
            // below Falling to deactivate. Level2 is strictly above Level1.
            bool atLeastL1 = current >= CellAggressionLevel.Level1
                ? count >= faunaAggressionL1.Falling
                : count >= faunaAggressionL1.Rising;

            bool atLeastL2 = current >= CellAggressionLevel.Level2
                ? count >= faunaAggressionL2.Falling
                : count >= faunaAggressionL2.Rising;

            if (atLeastL2) return CellAggressionLevel.Level2;
            if (atLeastL1) return CellAggressionLevel.Level1;
            return CellAggressionLevel.Level0;
        }

        IEnumerator PollAggressionLevel()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.1f, aggressionPollInterval));
            while (true)
            {
                int count = _liveBlockCount;

                _floraPlantingEnabled = EvaluateDefaultOnGate(_floraPlantingEnabled, count, floraPlantEnd);
                _floraGrowingEnabled  = EvaluateDefaultOnGate(_floraGrowingEnabled,  count, floraGrowEnd);
                _faunaSpawningEnabled = EvaluateDefaultOffGate(_faunaSpawningEnabled, count, faunaSpawnStart);

                var next = BucketForCount(count, _aggressionLevel);
                if (next != _aggressionLevel)
                {
                    _aggressionLevel = next;
                    try { OnAggressionChanged?.Invoke(next); }
                    catch (Exception e) { CSDebug.LogError($"[Cell {ID}] OnAggressionChanged listener threw: {e}"); }
                }

                yield return wait;
            }
        }

        void ResetRegulationState()
        {
            _liveBlockCount = 0;
            _aggressionLevel = CellAggressionLevel.Level0;
            _floraPlantingEnabled = true;
            _floraGrowingEnabled = true;
            _faunaSpawningEnabled = false;
            _registeredBlocks.Clear();
        }

        void StartAggressionPoll()
        {
            StopAggressionPoll();
            _aggressionPollRoutine = StartCoroutine(PollAggressionLevel());
        }

        void StopAggressionPoll()
        {
            if (_aggressionPollRoutine != null)
            {
                StopCoroutine(_aggressionPollRoutine);
                _aggressionPollRoutine = null;
            }
            ResetRegulationState();
        }

        public Vector3 GetExplosionTarget(Domains domain)
        {
            if (!countGrids.TryGetValue(domain, out var grid) || grid == null)
                return GetCellAnchorPosition();

            var region = grid.FindDensestRegion();
            // Empty grids return the grid's bottom-left-back corner (origin minus
            // totalLength/2), which pulls every fauna that queries an empty grid
            // toward the world-space -x/-y/-z corner and causes them to cluster
            // far from any actual prism. Guard by reading back the density at the
            // returned point; if it's zero the grid had nothing to find.
            if (grid.GetDensityAtPosition(region) <= 0)
                return GetCellAnchorPosition();
            return region;
        }

        /// <summary>
        /// Densest region across all domain grids (color-agnostic). Used by fauna at
        /// aggression Level 2 which seek the nearest centroid regardless of color.
        /// Each countGrid tracks blocks-not-of-its-domain, so taking the max density
        /// across all grids yields a reliable proxy for overall prism density.
        /// </summary>
        public Vector3 GetPrimaryCentroid()
        {
            Vector3 best = GetCellAnchorPosition();
            int bestDensity = 0;
            foreach (var kvp in countGrids)
            {
                var grid = kvp.Value;
                if (grid == null) continue;

                var region = grid.FindDensestRegion();
                int d = grid.GetDensityAtPosition(region);
                if (d > bestDensity)
                {
                    bestDensity = d;
                    best = region;
                }
            }
            // If no grid had any density, bestDensity stays 0 and we return the cell
            // anchor (crystal or cell transform) — keeping fauna near the cell
            // instead of drifting toward the empty-grid corner fallback.
            return best;
        }

        /// <summary>
        /// Safe fallback position for goal resolution when density grids are empty:
        /// the local crystal if one exists, otherwise the cell's own transform.
        /// </summary>
        Vector3 GetCellAnchorPosition()
        {
            if (runtime != null && runtime.CrystalTransform)
                return runtime.CrystalTransform.position;
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