using CosmicShore.Game.Arcade;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using CosmicShore.App.Systems.Squads;
using CosmicShore.Core;
using CosmicShore.Integrations.PlayFab.Economy;

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
        [Range(1, 9)] public int CurrentDifficulty = 5;

        public float IntensityThreshold = 1;  // How much variance is allowed from mission difficulty in a wave
        public float ThreatWaveMinimumPeriodInSeconds = 20;
        int currentSpawnLocationIndex = 0;

        protected override void Start()
        {
            base.Start();
            Hangar.Instance.SetPlayerCaptain(CaptainManager.Instance.GetCaptainByName(SquadSystem.SquadLeader.Name));
            Players[0].defaultShip = SquadSystem.SquadLeader.Ship.Class;
            SquadMateOne.defaultShip = SquadSystem.RogueOne.Ship.Class;
            SquadMateTwo.defaultShip = SquadSystem.RogueTwo.Ship.Class;
        }

        public override void StartNewGame()
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
            float totalWeight = 0f;
            foreach (Threat threat in MissionData.PotentialThreats)
                totalWeight += threat.weight;

            float roll = Random.Range(0, totalWeight);
            float cumulativeWeight = 0;

            foreach (Threat threat in MissionData.PotentialThreats)
            {
                cumulativeWeight += threat.weight;
                if (roll < cumulativeWeight)
                    return threat;
            }

            return MissionData.PotentialThreats[0]; // Fallback in case something goes wrong
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

                foreach (Threat threat in threats)
                {
                    elapsedThreat += threat.threatLevel;

                    Debug.LogWarning($"ThreatWaveCoroutine -  Spawning Threat:{threat.threatName}");

                    if (SpawnLocations != null)
                        ThreatSpawner.SpawnThreat(threat, SpawnLocations[currentSpawnLocationIndex].position);
                    else
                        ThreatSpawner.SpawnThreat(threat);
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