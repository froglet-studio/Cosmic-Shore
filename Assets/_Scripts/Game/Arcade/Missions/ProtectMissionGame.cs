using CosmicShore.Game.Arcade;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using CosmicShore.Systems.Squads;
using CosmicShore.Core;
using CosmicShore.Integrations.PlayFab.Economy;
using System.Linq;
using CosmicShore.Game;
using CosmicShore.Soap;

namespace CosmicShore
{
    public class ProtectMissionGame : MiniGame
    {

        [Header("Mission Configuration")]
        [SerializeField] SO_Mission MissionData;
        [SerializeField] List<Transform> SpawnLocations;
        [SerializeField] Player SquadMateOne;
        [SerializeField] Player SquadMateTwo;
        [SerializeField] Player HostileAIOne;
        [SerializeField] Player HostileAITwo;
        [SerializeField] Player HostileAIThree;
        [SerializeField] List<VesselClassType> EnemyShipClasses = new()
        {
            VesselClassType.Rhino,
            VesselClassType.Manta,
            VesselClassType.Dolphin,
            VesselClassType.Serpent,
            VesselClassType.Sparrow,
            VesselClassType.Squirrel,
        };

        [Range(1, 9)] public int CurrentDifficulty = 5;
        [SerializeField] float faunaOnlyLimit = 1000; // If the team volume is above this limit, only FaunaPrefab threats will spawn

        [SerializeField] private CellRuntimeDataSO cellData;
        
        public float IntensityThreshold = 1;  // How much variance is allowed from mission difficulty in a wave
        public float ThreatWaveMinimumPeriodInSeconds = 20;
        int currentSpawnLocationIndex = 0;
        Threat[] faunaThreats = new Threat[0];
        Cell node;

        protected override void Start()
        {
            base.Start();
            CurrentDifficulty = IntensityLevel;
            Hangar.Instance.SetPlayerCaptain(CaptainManager.Instance.GetCaptainByName(SquadSystem.SquadLeader.Name));

            // TODO - Cannot modify player datas directly... need other way of initialization.
            /*Players[0].ShipType = SquadSystem.SquadLeader.Vessel.Class;
            SquadMateOne.ShipType = SquadSystem.RogueOne.Vessel.Class;
            SquadMateTwo.ShipType = SquadSystem.RogueTwo.Vessel.Class;
            HostileAIOne.ShipType = EnemyShipClasses[Random.Range(0, EnemyShipClasses.Count)];
            HostileAITwo.ShipType = EnemyShipClasses[Random.Range(0, EnemyShipClasses.Count)];
            HostileAIThree.ShipType = EnemyShipClasses[Random.Range(0, EnemyShipClasses.Count)];*/

            faunaThreats = MissionData.PotentialThreats.Where(threat => threat.threatPrefab.TryGetComponent<Fauna>(out _)).ToArray();
            node = cellData.Cell; // CellControlManager.Instance.GetNearestCell(Vector3.zero);
        }

        protected override void StartNewGame()
        {
            StartCoroutine(ThreatWaveCoroutine());
            base.StartNewGame();
            SquadMateOne.gameObject.SetActive(true);
            SquadMateTwo.gameObject.SetActive(true);
            HostileAIOne.gameObject.SetActive(true);
        }

        // Method to roll a single threat based on weights
        Threat RollThreat()
        {
            Debug.LogWarning("jade volume: node.GetTeamVolume(Teams.Jade)");
            var threats = node.GetTeamVolume(Domains.Jade) > faunaOnlyLimit ? faunaThreats : MissionData.PotentialThreats;
            float totalWeight = 0f;
            foreach (Threat threat in threats)
                totalWeight += threat.weight;

            float roll = Random.Range(0, totalWeight);
            float cumulativeWeight = 0;

            foreach (Threat threat in threats)
            {
                cumulativeWeight += threat.weight;
                if (roll < cumulativeWeight)
                    return threat;
            }

            return threats[0]; // Fallback in case something goes wrong
        }

        // Method to generate a wave of threats
        List<Threat> GenerateThreatWave(int targetThreatLevel)
        {
            List<Threat> wave = new List<Threat>();

            Threat selectedThreat = RollThreat();

            wave.Add(selectedThreat);
            int currentThreatLevel = selectedThreat.threatLevel;

            // Keep adding the same type of threat until the wave reaches the target threat level
            while (currentThreatLevel < targetThreatLevel)
            {
                selectedThreat = RollThreat();
                // Check if adding this threat will exceed the target threat level
                if (currentThreatLevel + selectedThreat.threatLevel <= targetThreatLevel)
                {
                    wave.Add(selectedThreat);
                    currentThreatLevel += selectedThreat.threatLevel;
                }
                else
                {
                    break; // Stop if no more threats can fit within the target level
                }
            }

            return wave;
        }

        

        public ThreatSpawner ThreatSpawner;

        float elapsedTime;
        float elapsedThreat;

        public IEnumerator ThreatWaveCoroutine()
        {
            //yield return new WaitForSeconds(3);

            var threatTeam = Domains.Gold;

            if (ThreatSpawner == null)
                ThreatSpawner = FindAnyObjectByType<ThreatSpawner>();

            float startTime = Time.time;

            var targetThreatPerTime = CurrentDifficulty / 9f;

            // Calculate target threat level for the wave within the threshold range
            int targetThreatLevel = Random.Range(
                Mathf.FloorToInt(CurrentDifficulty - IntensityThreshold),
                Mathf.FloorToInt(CurrentDifficulty + IntensityThreshold)
            );

            while (true)
            {
                var threats = GenerateThreatWave(targetThreatLevel);

                threatTeam = threatTeam == Domains.Gold ? Domains.Ruby : Domains.Gold;

                foreach (Threat threat in threats)
                {
                    elapsedThreat += threat.threatLevel;

                    Debug.LogWarning($"ThreatWaveCoroutine -  Spawning Threat:{threat.threatName}");

                    if (SpawnLocations != null)
                        ThreatSpawner.SpawnThreat(threat, threatTeam, SpawnLocations[currentSpawnLocationIndex].position);
                    else
                        ThreatSpawner.SpawnThreat(threat, threatTeam);
                }

                if (SpawnLocations != null)
                    // Cycle through spawn locals for each wave
                    currentSpawnLocationIndex = (currentSpawnLocationIndex + 1) % SpawnLocations.Count;

                elapsedTime = Time.time - startTime;

                var timeToTarget = (elapsedThreat / targetThreatPerTime) - elapsedTime;

                Debug.LogWarning($"ThreatWaveCoroutine -  elapsedTime:{elapsedTime}, elapsedThreat:{elapsedThreat}, timeToTarget:{timeToTarget}");

                yield return new WaitForSeconds(Mathf.Max(ThreatWaveMinimumPeriodInSeconds, timeToTarget));
            }
        }
    }
}