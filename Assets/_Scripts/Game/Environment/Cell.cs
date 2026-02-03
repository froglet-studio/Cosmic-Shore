using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Soap;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace CosmicShore.Game
{
    public class Cell : MonoBehaviour
    {
        enum CellTypeChoiceOptions { Random, IntensityWise }

        [SerializeField] public int ID;

        [Header("Cell Type Selection")]
        [SerializeField] List<SO_CellType> CellTypes;
        [SerializeField] CellTypeChoiceOptions cellTypeChoiceOptions = CellTypeChoiceOptions.Random;

        [Header("Runtime Data")]
        [SerializeField] CellDataSO cellData;
        [SerializeField] GameDataSO gameData;
        
        [SerializeField] float nucleusScaleMultiplier = 1f;

        [Header("Random Mode Profile")]
        [SerializeField] CellRandomSpawnProfileSO randomSpawnProfile;
        public CellRandomSpawnProfileSO RandomSpawnProfile => randomSpawnProfile;

        SO_CellType cellType => cellData.CellType;
        GameObject membrane;

        public Dictionary<Domains, BlockCountDensityGrid> countGrids = new();
        public Dictionary<Domains, BlockVolumeDensityGrid> volumeGrids = new();
        readonly Dictionary<Domains, float> teamVolumes = new();
        
        private List<GameObject> spawnedLifeForms = new List<GameObject>();

        readonly ICellLifeSpawner intensitySpawner = new IntensityWiseLifeSpawner();
        readonly ICellLifeSpawner randomSpawner = new RandomLifeSpawner();
        ICellLifeSpawner activeSpawner;

        void OnEnable()
        {
            if (gameData != null)
                gameData.OnInitializeGame += Initialize;

            if (cellData == null) return;
            if (cellData.OnCrystalSpawned != null)
                cellData.OnCrystalSpawned.OnRaised += OnCrystalSpawnedInCell;

            if (cellData.OnResetForReplay != null)
                cellData.OnResetForReplay.OnRaised += ResetCell;
        }

        void OnDisable()
        {
            if (gameData != null)
                gameData.OnInitializeGame -= Initialize;

            if (cellData != null)
            {
                if (cellData.OnCrystalSpawned != null)
                    cellData.OnCrystalSpawned.OnRaised -= OnCrystalSpawnedInCell;

                if (cellData.OnResetForReplay != null)
                    cellData.OnResetForReplay.OnRaised -= ResetCell;
            }
            StopSpawner();
            cellData?.ResetRuntimeData();
        }

        void ResetCell()
        {
            Debug.Log($"<color=yellow>[Cell {ID}] ═══ RESET FOR REPLAY ═══</color>");
            
            // Clean up lifeforms
            for (int i = spawnedLifeForms.Count - 1; i >= 0; i--)
            {
                if (spawnedLifeForms[i]) Destroy(spawnedLifeForms[i]);
            }
            spawnedLifeForms.Clear();
            
            // Stop spawner
            StopSpawner();
            
            // CRITICAL FIX: Reassign CellType (Initialize() won't run again on replay)
            AssignCellType();
            
            // Reset volumes
            ResetVolumes();
            
            // Update stats
            cellData.EnsureCellStats(ID);
            UpdateCellStats();
            
            Debug.Log($"<color=green>[Cell {ID}] Reset complete - CellType: {cellType?.name} - Ready for new crystal spawn</color>");
        }

        void UpdateCellStats()
        {
            if (!cellData) return;

            cellData.EnsureCellStats(ID);
            var cs = cellData.CellStatsList[ID];
            cs.LifeFormsInCell = spawnedLifeForms.Count;

            UpdateLifeFormCountUI();
        }

        void UpdateLifeFormCountUI()
        {
            if (cellData?.OnCellItemsUpdated)
            {
                cellData.OnCellItemsUpdated.Raise();
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
            {
                UpdateCellStats();
            }
        }

        void Initialize()
        {
            spawnedLifeForms.Clear();

            cellData.Cell = this;
            cellData.EnsureCellStats(ID);

            AssignCellType();
            SetupDensityGrids();
            SpawnVisuals();
            ResetVolumes();
            
            UpdateCellStats();
            
            Debug.Log($"<color=cyan>[Cell {ID}] Initialized with CellType: {cellType?.name}</color>");
        }

        void OnCrystalSpawnedInCell()
        {
            if (!cellType) 
            {
                Debug.LogError($"[Cell {ID}] No CellType assigned!");
                return;
            }

            Debug.Log($"<color=green>[Cell {ID}] ═══ CRYSTAL SPAWNED - Starting LifeForm Spawner ═══</color>");
            
            ApplyModifiers();
            StartSpawnerForMode();
        }
        
        void AssignCellType()
        {
            if (CellTypes == null || CellTypes.Count == 0)
            {
                Debug.LogError($"{nameof(Cell)}: No cell types found to assign.");
                return;
            }

            var index = cellTypeChoiceOptions switch
            {
                CellTypeChoiceOptions.Random => Random.Range(0, CellTypes.Count),
                CellTypeChoiceOptions.IntensityWise => Mathf.Clamp(gameData.SelectedIntensity.Value - 1, 0, CellTypes.Count - 1),
                _ => 0
            };

            cellData.CellType = CellTypes[index];
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
            if (!cellType) return;

            if (cellType.MembranePrefab != null)
                membrane = Instantiate(cellType.MembranePrefab, transform.position, Quaternion.identity);

            if (cellType.NucleusPrefab == null) return;
            var nucleus = Instantiate(cellType.NucleusPrefab, transform.position, Quaternion.identity);
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
            if (cellType.CellModifiers == null) return;
            foreach (var modifier in cellType.CellModifiers)
                modifier.Apply(this);
        }

        void StartSpawnerForMode()
        {
            StopSpawner();

            activeSpawner = cellTypeChoiceOptions == CellTypeChoiceOptions.IntensityWise
                ? intensitySpawner
                : randomSpawner;

            activeSpawner.Start(this, cellType, cellData, gameData);
            
            Debug.Log($"<color=green>[Cell {ID}] Spawner started: {activeSpawner.GetType().Name}</color>");
        }

        void StopSpawner()
        {
            if (activeSpawner == null) return;
            activeSpawner.Stop(this);
            activeSpawner = null;
            Debug.Log($"<color=yellow>[Cell {ID}] Spawner stopped</color>");
        }

        internal Transform GetCrystalTransform() => 
            !cellData.TryGetLocalCrystal(out Crystal crystal) ? null : crystal.transform;

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