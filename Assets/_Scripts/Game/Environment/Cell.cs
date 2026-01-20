using System.Collections;
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
        enum CellTypeChoiceOptions
        {
            Random,
            IntensityWise
        }

        [SerializeField] public int ID;

        [Header("Cell Type Selection")]
        [SerializeField] List<SO_CellType> CellTypes;
        [SerializeField] CellTypeChoiceOptions cellTypeChoiceOptions = CellTypeChoiceOptions.Random;

        [Header("Runtime Data")]
        [SerializeField] CellDataSO cellData;

        [FormerlySerializedAs("miniGameData")]
        [SerializeField] GameDataSO gameData;

        SO_CellType cellType => cellData.CellType;
        GameObject membrane;

        // ---------------------------------------------------------------------
        // RANDOM MODE (legacy knobs) - IntensityWise ignores these.
        // ---------------------------------------------------------------------
        [Header("Random Mode - Spawn Settings (Legacy)")]
        [SerializeField] int FloraTypeCount = 2;
        [SerializeField] bool spawnJade = true;
        [SerializeField] int FaunaTypeCount = 2;

        [SerializeField] float floraSpawnVolumeCeiling = 12000f;
        [SerializeField] float initialFaunaSpawnWaitTime = 10f;
        [SerializeField] float faunaSpawnVolumeThreshold = 1f;
        [SerializeField] float baseFaunaSpawnTime = 60f;
        [SerializeField] bool hasRandomFloraAndFauna;

        // ---------------------------------------------------------------------
        // Density/volume tracking
        // ---------------------------------------------------------------------
        public Dictionary<Domains, BlockCountDensityGrid> countGrids = new Dictionary<Domains, BlockCountDensityGrid>();
        public Dictionary<Domains, BlockVolumeDensityGrid> volumeGrids = new Dictionary<Domains, BlockVolumeDensityGrid>();
        readonly Dictionary<Domains, float> teamVolumes = new Dictionary<Domains, float>();

        // ---------------------------------------------------------------------
        // Intensity-wise routines
        // ---------------------------------------------------------------------
        Coroutine floraRoutine;
        Coroutine faunaRoutine;
        bool hasHandledCrystalSpawn;

        void OnEnable()
        {
            if (gameData != null)
                gameData.OnInitializeGame += Initialize;

            if (cellData != null && cellData.OnCrystalSpawned != null)
                cellData.OnCrystalSpawned.OnRaised += OnCrystalSpawnedInCell;
        }

        void OnDisable()
        {
            if (gameData != null)
                gameData.OnInitializeGame -= Initialize;

            if (cellData != null && cellData.OnCrystalSpawned != null)
                cellData.OnCrystalSpawned.OnRaised -= OnCrystalSpawnedInCell;

            StopLifeRoutines();
            cellData?.ResetRuntimeData();
        }

        void Initialize()
        {
            hasHandledCrystalSpawn = false;

            cellData.Cell = this;
            cellData.EnsureCellStats(ID);

            AssignCellType();

            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };

            countGrids.Clear();
            foreach (Domains t in teams)
                countGrids.Add(t, new BlockCountDensityGrid(t));

            if (cellType == null)
            {
                Debug.LogError($"{nameof(Cell)}: CellType was null after assignment.");
                return;
            }

            if (cellType.MembranePrefab != null)
                membrane = Instantiate(cellType.MembranePrefab, transform.position, Quaternion.identity);

            if (cellType.NucleusPrefab != null)
                Instantiate(cellType.NucleusPrefab, transform.position, Quaternion.identity);

            teamVolumes[Domains.Jade] = 0;
            teamVolumes[Domains.Ruby] = 0;
            teamVolumes[Domains.Gold] = 0;
            teamVolumes[Domains.Blue] = 0;
        }

        void OnCrystalSpawnedInCell()
        {
            if (hasHandledCrystalSpawn) return;
            hasHandledCrystalSpawn = true;

            if (cellType == null)
                return;

            foreach (var modifier in cellType.CellModifiers)
                modifier.Apply(this);

            if (cellTypeChoiceOptions == CellTypeChoiceOptions.IntensityWise)
                StartLifeSpawningIntensityWise();
            else
                SpawnLifeRandomModeLegacy();
        }

        void AssignCellType()
        {
            if (CellTypes == null || CellTypes.Count == 0)
            {
                Debug.LogError("No cell types found to assign to cell!");
                return;
            }

            int index = cellTypeChoiceOptions switch
            {
                CellTypeChoiceOptions.Random => Random.Range(0, CellTypes.Count),
                CellTypeChoiceOptions.IntensityWise => Mathf.Clamp(gameData.SelectedIntensity.Value - 1, 0, CellTypes.Count - 1),
                _ => 0
            };

            cellData.CellType = CellTypes[index];

            if (!cellData.CellType)
                Debug.LogError("Cell type is not assigned. Please assign a valid cell type.");
        }

        // =====================================================================
        // INTENSITY-WISE (finite, SO-driven)
        // Flora amount comes from FloraConfiguration.initialSpawnCount (picked config).
        // Fauna amount comes from PopulationConfiguration.InitialPopulationSpawnCount (per type).
        // Separate initial delays for flora + fauna.
        // =====================================================================

        void StartLifeSpawningIntensityWise()
        {
            if (cellType == null)
                return;

            StopLifeRoutines();

            var cfg = cellType.IntensityLifeFormConfiguration;

            floraRoutine = StartCoroutine(SpawnInitialFloraFromPickedConfig(cfg));
            faunaRoutine = StartCoroutine(SpawnInitialPopulationsFromConfig(cfg));
        }

        void StopLifeRoutines()
        {
            if (floraRoutine != null)
            {
                StopCoroutine(floraRoutine);
                floraRoutine = null;
            }

            if (faunaRoutine != null)
            {
                StopCoroutine(faunaRoutine);
                faunaRoutine = null;
            }
        }

        IEnumerator SpawnInitialFloraFromPickedConfig(SO_CellType.LifeFormConfiguration cfg)
        {
            if (cfg.FloraInitialDelaySeconds > 0f)
                yield return new WaitForSeconds(cfg.FloraInitialDelaySeconds);

            if (cellType.SupportedFlora == null || cellType.SupportedFlora.Count == 0)
                yield break;

            // Pick ONE flora config based on SpawnProbability
            var floraConfig = PickWeighted(cellType.SupportedFlora, f => f.SpawnProbability);
            if (floraConfig == null || floraConfig.Flora == null)
                yield break;

            int count = Mathf.Max(0, floraConfig.initialSpawnCount);
            for (int i = 0; i < count; i++)
            {
                var newFlora = Instantiate(floraConfig.Flora, transform.position, Quaternion.identity);
                newFlora.domain = cfg.SpawnJade ? (Domains)Random.Range(1, 5) : (Domains)Random.Range(2, 5);
                newFlora.Initialize(this);

                if (cfg.FloraSpawnIntervalSeconds > 0f && i < count - 1)
                    yield return new WaitForSeconds(cfg.FloraSpawnIntervalSeconds);
            }
        }

        IEnumerator SpawnInitialPopulationsFromConfig(SO_CellType.LifeFormConfiguration cfg)
        {
            if (cfg.FaunaInitialDelaySeconds > 0f)
                yield return new WaitForSeconds(cfg.FaunaInitialDelaySeconds);

            if (cellType.SupportedFauna == null || cellType.SupportedFauna.Count == 0)
                yield break;

            foreach (var populationConfiguration in cellType.SupportedFauna)
            {
                if (populationConfiguration == null || !populationConfiguration.Population)
                    continue;

                int count = Mathf.Max(0, populationConfiguration.InitialFloraSpawnCount);
                for (int i = 0; i < count; i++)
                {
                    var pop = Instantiate(populationConfiguration.Population, transform.position, Quaternion.identity);
                    pop.domain = GetHostileDomainToLocal();
                    pop.Goal = cellData.CrystalTransform.position;

                    if (cfg.FaunaSpawnIntervalSeconds > 0f && i < count - 1)
                        yield return new WaitForSeconds(cfg.FaunaSpawnIntervalSeconds);
                }
            }
        }

        // =====================================================================
        // RANDOM MODE (legacy behaviour) - unchanged
        // =====================================================================

        void SpawnLifeRandomModeLegacy()
        {
            if (!cellType)
                return;

            if (cellType.SupportedFlora != null && cellType.SupportedFlora.Count > 0)
            {
                for (int i = 0; i < FloraTypeCount; i++)
                {
                    var floraConfiguration = ConfigureFloraLegacy();
                    StartCoroutine(SpawnFloraLegacyLoop(floraConfiguration, spawnJade));
                }
            }

            if (cellType.SupportedFauna != null && cellType.SupportedFauna.Count > 0)
            {
                for (int i = 0; i < FaunaTypeCount; i++)
                {
                    var population = ConfigurePopulationLegacy();
                    StartCoroutine(SpawnPopulationLegacyLoop(population));
                }
            }
        }

        FloraConfiguration ConfigureFloraLegacy() =>
            PickWeighted(cellType.SupportedFlora, f => f.SpawnProbability);

        Population ConfigurePopulationLegacy()
        {
            var picked = PickWeighted(cellType.SupportedFauna, f => f.SpawnProbability);
            return picked != null ? picked.Population : null;
        }

        IEnumerator SpawnFloraLegacyLoop(FloraConfiguration floraConfiguration, bool spawnJadeLocal = true)
        {
            if (floraConfiguration == null || floraConfiguration.Flora == null)
                yield break;

            for (int i = 0; i < floraConfiguration.initialSpawnCount - 1; i++)
            {
                var newFlora = Instantiate(floraConfiguration.Flora, transform.position, Quaternion.identity);
                newFlora.domain = spawnJadeLocal ? (Domains)Random.Range(1, 5) : (Domains)Random.Range(2, 5);
                newFlora.Initialize(this);
            }

            while (true)
            {
                var controllingVolume = gameData.GetControllingTeamStatsBasedOnVolumeRemaining().Item2;
                if (controllingVolume < floraSpawnVolumeCeiling)
                {
                    var newFlora = Instantiate(floraConfiguration.Flora, transform.position, Quaternion.identity);
                    newFlora.domain = spawnJadeLocal ? (Domains)Random.Range(1, 5) : (Domains)Random.Range(2, 5);
                    newFlora.Initialize(this);
                }

                float waitPeriod = floraConfiguration.OverrideDefaultPlantPeriod
                    ? floraConfiguration.NewPlantPeriod
                    : floraConfiguration.Flora.PlantPeriod;

                yield return new WaitForSeconds(waitPeriod);
            }
        }

        IEnumerator SpawnPopulationLegacyLoop(Population population)
        {
            if (population == null)
                yield break;

            yield return new WaitForSeconds(initialFaunaSpawnWaitTime);

            while (true)
            {
                var controllingTeamStat = gameData.GetControllingTeamStatsBasedOnVolumeRemaining();
                var controllingVolume = controllingTeamStat.Item2;

                if (controllingVolume > faunaSpawnVolumeThreshold)
                {
                    var newPopulation = Instantiate(population, transform.position, Quaternion.identity);
                    newPopulation.domain = GetHostileDomainToLocal();
                    newPopulation.Goal = cellData.CrystalTransform.position;
                    yield return new WaitForSeconds(baseFaunaSpawnTime);
                }
                else
                {
                    yield return new WaitForSeconds(2f);
                }
            }
        }

        // =====================================================================
        // Weighted picker (do NOT mutate ScriptableObjects)
        // =====================================================================

        T PickWeighted<T>(IReadOnlyList<T> items, System.Func<T, float> weightSelector)
        {
            if (items == null || items.Count == 0)
                return default;

            float total = 0f;
            for (int i = 0; i < items.Count; i++)
                total += Mathf.Max(0f, weightSelector(items[i]));

            if (total <= 0f)
                return items[0];

            float roll = Random.value * total;
            float cumulative = 0f;

            for (int i = 0; i < items.Count; i++)
            {
                cumulative += Mathf.Max(0f, weightSelector(items[i]));
                if (roll <= cumulative)
                    return items[i];
            }

            return items[^1];
        }

        // =====================================================================
        // Existing helpers / volume / grids
        // =====================================================================

        internal Transform GetCrystalTransform() => cellData.CrystalTransform;

        public void AddBlock(Prism block)
        {
            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
            foreach (Domains t in teams)
            {
                if (t != block.Domain)
                    countGrids[t].AddBlock(block);
            }
        }

        public void RemoveBlock(Prism block)
        {
            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
            foreach (Domains t in teams)
            {
                if (t != block.Domain)
                    countGrids[t].RemoveBlock(block);
            }
        }

        public Vector3 GetExplosionTarget(Domains domain) => countGrids[domain].FindDensestRegion();

        public bool ContainsPosition(Vector3 position)
        {
            if (membrane is null)
                return false;

            return Vector3.Distance(position, transform.position) < membrane.transform.localScale.x;
        }

        public void ChangeVolume(Domains domain, float volume)
        {
            if (!teamVolumes.ContainsKey(domain))
                teamVolumes.Add(domain, 0);

            teamVolumes[domain] += volume;
        }

        public float GetTeamVolume(Domains domain)
        {
            if (!teamVolumes.ContainsKey(domain))
                return 0;

            return teamVolumes[domain];
        }

        Domains GetHostileDomainToLocal()
        {
            var local = gameData.LocalRoundStats?.Domain ?? Domains.Jade;
            var candidates = new[] { Domains.Ruby, Domains.Gold, Domains.Blue, Domains.Jade };
            return candidates.First(d => d != local);
        }
    }
}
