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

        [Header("Aggression Thresholds")]
        [Tooltip("Live prism count at or above which the cell enters Elevated.")]
        [SerializeField, Min(1)] int elevatedThreshold = 400;
        [Tooltip("Live prism count at or above which the cell enters Stressed.")]
        [SerializeField, Min(1)] int stressedThreshold = 900;
        [Tooltip("Live prism count at or above which the cell enters Critical (growth halts).")]
        [SerializeField, Min(1)] int criticalThreshold = 1500;
        [Tooltip("Seconds between aggression level re-evaluations.")]
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
        CellAggressionLevel _aggressionLevel = CellAggressionLevel.Calm;
        Coroutine _aggressionPollRoutine;

        // Dedupes AddBlock/RemoveBlock across multiple call paths (flora binding,
        // HealthPrism.Initialize, future direct callers) so _liveBlockCount stays accurate.
        readonly HashSet<Prism> _registeredBlocks = new();

        /// <summary>Current live prism count in this cell (enemy-tracked blocks).</summary>
        public int LiveBlockCount => _liveBlockCount;

        /// <summary>Bucketed stress level driven by <see cref="LiveBlockCount"/>.</summary>
        public CellAggressionLevel AggressionLevel => _aggressionLevel;

        /// <summary>Raised when <see cref="AggressionLevel"/> transitions. New level is the argument.</summary>
        public event Action<CellAggressionLevel> OnAggressionChanged;

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

            _liveBlockCount = 0;
            _aggressionLevel = CellAggressionLevel.Calm;
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

            _liveBlockCount = 0;
            _aggressionLevel = CellAggressionLevel.Calm;
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

        CellAggressionLevel BucketForCount(int count)
        {
            if (count >= criticalThreshold) return CellAggressionLevel.Critical;
            if (count >= stressedThreshold) return CellAggressionLevel.Stressed;
            if (count >= elevatedThreshold) return CellAggressionLevel.Elevated;
            return CellAggressionLevel.Calm;
        }

        IEnumerator PollAggressionLevel()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.1f, aggressionPollInterval));
            while (true)
            {
                var next = BucketForCount(_liveBlockCount);
                if (next != _aggressionLevel)
                {
                    _aggressionLevel = next;
                    try { OnAggressionChanged?.Invoke(next); }
                    catch (Exception e) { CSDebug.LogError($"[Cell {ID}] OnAggressionChanged listener threw: {e}"); }
                }
                yield return wait;
            }
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
            _aggressionLevel = CellAggressionLevel.Calm;
            _liveBlockCount = 0;
            _registeredBlocks.Clear();
        }

        public Vector3 GetExplosionTarget(Domains domain) => countGrids[domain].FindDensestRegion();

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