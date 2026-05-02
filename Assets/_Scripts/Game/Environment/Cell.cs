// Cell.cs
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Soap;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CosmicShore.Game
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
        [SerializeField] GameDataSO gameData;

        [SerializeField] float nucleusScaleMultiplier = 1f;


        CellConfigDataSO cellConfigData => runtime ? runtime.Config : null;
        GameObject membrane;

        public Dictionary<Domains, BlockCountDensityGrid> countGrids = new();
        public Dictionary<Domains, BlockCountDensityGrid> selfDensityGrids = new();
        public Dictionary<Domains, BlockVolumeDensityGrid> volumeGrids = new();
        readonly Dictionary<Domains, float> teamVolumes = new();
        readonly Dictionary<Domains, int> prismCounts = new();

        static readonly List<Cell> ActiveCells = new();
        static readonly Domains[] FaunaDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        readonly List<GameObject> spawnedLifeForms = new();

        readonly ICellLifeSpawner intensitySpawner = new IntensityWiseLifeSpawner();
        readonly ICellLifeSpawner randomSpawner = new RandomLifeSpawner();
        ICellLifeSpawner activeSpawner;
        bool postInitilized = false;

        void OnEnable()
        {
            if (!ActiveCells.Contains(this)) ActiveCells.Add(this);

            if (gameData != null)
                gameData.OnInitializeGame += Initialize;

            if (!runtime) return;

            // We keep events ONLY in runtime.
            if (runtime.OnCellItemsUpdated != null)
                runtime.OnCellItemsUpdated.OnRaised += OnCellItemUpdated;

            if (runtime.OnResetForReplay != null)
                runtime.OnResetForReplay.OnRaised += ResetCell;
        }

        void OnDisable()
        {
            ActiveCells.Remove(this);

            if (gameData != null)
                gameData.OnInitializeGame -= Initialize;

            if (runtime != null)
            {
                if (runtime.OnCellItemsUpdated != null)
                    runtime.OnCellItemsUpdated.OnRaised -= OnCellItemUpdated;

                if (runtime.OnResetForReplay != null)
                    runtime.OnResetForReplay.OnRaised -= ResetCell;
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

            UpdateCellStats();
        }
        
        void InitilizePostFirstCellItem()
        {
            postInitilized = true;
            if (!cellConfigData)
            {
                Debug.LogWarning($"[Cell {ID}] Crystal spawned before Cell Initialized. Attempting lazy init.");
                Initialize();
                if (!cellConfigData) return;
            }

            ApplyModifiers();
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
                Debug.LogError($"{nameof(Cell)}: No CellConfigs found to assign.");
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
            countGrids.Clear();
            selfDensityGrids.Clear();
            prismCounts.Clear();
            foreach (Domains t in FaunaDomains)
            {
                countGrids[t] = new BlockCountDensityGrid(t);
                selfDensityGrids[t] = new BlockCountDensityGrid(t);
                prismCounts[t] = 0;
            }
        }

        void SpawnVisuals()
        {
            if (!cellConfigData) return;

            if (cellConfigData.MembranePrefab != null)
                membrane = Instantiate(cellConfigData.MembranePrefab, transform.position, Quaternion.identity);

            if (cellConfigData.NucleusPrefab == null) return;
            var nucleus = Instantiate(cellConfigData.NucleusPrefab, transform.position, Quaternion.identity);
            nucleus.transform.localScale *= nucleusScaleMultiplier;
        }

        void ResetVolumes()
        {
            foreach (Domains t in FaunaDomains)
                teamVolumes[t] = 0;
        }

        void ApplyModifiers()
        {
            var cfg = cellConfigData;
            if (!cfg || cfg.CellModifiers == null) return;

            foreach (var modifier in cfg.CellModifiers)
                modifier.Apply(this);
        }

        void StartSpawnerForMode()
        {
            StopSpawner();

            activeSpawner = cellTypeChoiceOptions == CellTypeChoiceOptions.IntensityWise
                ? intensitySpawner
                : randomSpawner;

            activeSpawner.Start(this, cellConfigData, runtime, gameData);

            Debug.Log($"<color=green>[Cell {ID}] Spawner started: {activeSpawner.GetType().Name}</color>");
        }

        void StopSpawner()
        {
            if (activeSpawner == null) return;
            activeSpawner.Stop(this);
            activeSpawner = null;
            Debug.Log($"<color=yellow>[Cell {ID}] Spawner stopped</color>");
        }

        internal Transform GetCrystalTransform()
        {
            if (runtime != null && runtime.TryGetLocalCrystal(out var crystal) && crystal)
                return crystal.transform;

            Debug.LogWarning($"[Cell {ID}] No crystal found!");
            return null;
        }

        public void AddBlock(Prism block)
        {
            if (!block) return;

            foreach (var t in FaunaDomains)
                if (t != block.Domain && countGrids.TryGetValue(t, out var hostileGrid))
                    hostileGrid.AddBlock(block);

            if (selfDensityGrids.TryGetValue(block.Domain, out var selfGrid))
                selfGrid.AddBlock(block);

            if (prismCounts.ContainsKey(block.Domain))
                prismCounts[block.Domain]++;
        }

        public void RemoveBlock(Prism block)
        {
            if (!block) return;

            foreach (Domains t in FaunaDomains)
                if (t != block.Domain && countGrids.TryGetValue(t, out var hostileGrid))
                    hostileGrid.RemoveBlock(block);

            if (selfDensityGrids.TryGetValue(block.Domain, out var selfGrid))
                selfGrid.RemoveBlock(block);

            if (prismCounts.ContainsKey(block.Domain))
                prismCounts[block.Domain] = Mathf.Max(0, prismCounts[block.Domain] - 1);
        }

        public Vector3 GetExplosionTarget(Domains domain) =>
            countGrids.TryGetValue(domain, out var grid) ? grid.FindDensestRegion() : transform.position;

        // Densest cluster of own-domain prisms - used by rabid fauna to seek their own mass.
        public Vector3 GetSelfDomainTarget(Domains domain) =>
            selfDensityGrids.TryGetValue(domain, out var grid) ? grid.FindDensestRegion() : transform.position;

        public int GetPrismCount(Domains domain) =>
            prismCounts.TryGetValue(domain, out var count) ? count : 0;

        public static Cell FindContainingCell(Vector3 position)
        {
            for (int i = 0; i < ActiveCells.Count; i++)
            {
                var cell = ActiveCells[i];
                if (cell && cell.ContainsPosition(position)) return cell;
            }
            return null;
        }

        public static void RegisterPrism(Prism prism)
        {
            if (!prism) return;
            var cell = FindContainingCell(prism.transform.position);
            if (cell) cell.AddBlock(prism);
        }

        public static void UnregisterPrism(Prism prism)
        {
            if (!prism) return;
            var cell = FindContainingCell(prism.transform.position);
            if (cell) cell.RemoveBlock(prism);
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
            return FaunaDomains.First(d => d != local);
        }
    }
}