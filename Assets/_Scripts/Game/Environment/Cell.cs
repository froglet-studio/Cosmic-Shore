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
        public Dictionary<Domains, BlockVolumeDensityGrid> volumeGrids = new();
        readonly Dictionary<Domains, float> teamVolumes = new();

        readonly List<GameObject> spawnedLifeForms = new();

        readonly ICellLifeSpawner intensitySpawner = new IntensityWiseLifeSpawner();
        readonly ICellLifeSpawner randomSpawner = new RandomLifeSpawner();
        ICellLifeSpawner activeSpawner;
        bool postInitilized = false;

        void OnEnable()
        {
            if (gameData != null)
                gameData.OnInitializeGame.OnRaised += Initialize;

            if (!runtime) return;

            // We keep events ONLY in runtime.
            if (runtime.OnCellItemsUpdated != null)
                runtime.OnCellItemsUpdated.OnRaised += OnCellItemUpdated;

            if (runtime.OnResetForReplay != null)
                runtime.OnResetForReplay.OnRaised += ResetCell;
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
            var nucleus = Instantiate(cellConfigData.NucleusPrefab, transform.position, Quaternion.identity);
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
            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
            foreach (var t in teams)
                if (t != block.Domain) countGrids[t].AddBlock(block);
        }

        public void RemoveBlock(Prism block)
        {
            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
            foreach (Domains t in teams)
                if (t != block.Domain) countGrids[t].RemoveBlock(block);
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