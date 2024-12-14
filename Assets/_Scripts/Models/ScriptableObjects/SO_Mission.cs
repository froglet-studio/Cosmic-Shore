using UnityEngine;

namespace CosmicShore
{
    [CreateAssetMenu(fileName = "New Mission", menuName = "CosmicShore/Game/Mission", order = 2)]
    [System.Serializable]
    public class SO_Mission : SO_Game
    {
        [Range(1, 9)] public int MinDifficulty = 1;
        [Range(1, 9)] public int MaxDifficulty = 9;

        public Threat[] PotentialThreats;
    }

    [System.Serializable]
    public enum SpawnMode
    {
        ConcentratedInvasion,
        RandomSurfaceScatter,
        LocalizedAmbush,
        PathBasedDeployment,
        SphereInterdiction,
        //FlashSpawn,
        //PincerSpawn,
        //RandomClusteredSpawn,
        //OrbitingSpawn,
        //IncrementalReinforcement,
        //CentralizedBurst,
        //RangedFlankingSpawn
    }

    // Base class for all threats
    [System.Serializable]
    public class Threat
    {
        public string threatName;
        public int threatLevel;
        public float weight;
        public GameObject threatPrefab;
        public SpawnMode spawnMode;

        public virtual void Spawn(Vector3 spawnPoint, Teams team)
        {
            var threat = GameObject.Instantiate(threatPrefab, spawnPoint, Quaternion.identity);
            ITeamAssignable iTeam = threat.GetComponent<ITeamAssignable>();
            if (iTeam != null)
                iTeam.SetTeam(team);
        }
    }

    // Derived Boss class with additional properties and defeat conditions
    /*
    [System.Serializable]
    public class Boss : Threat
    {
        public float health;
        public float volumeThreshold;
        public float resonanceFrequency;

        public Boss(string name, int level, float weight, GameObject prefab, SpawnMode mode, float health, float threshold, float resonance)
        {
            threatName = name;
            threatLevel = level;
            this.weight = weight;
            threatPrefab = prefab;
            spawnMode = mode;
            this.health = health;
            volumeThreshold = threshold;
            resonanceFrequency = resonance;
        }

        // Boss-specific defeat condition methods

        public bool IsDefeatedByCaddisDestruction(Caddis caddis)
        {
            return caddis != null;
        }

        public bool IsDefeatedByVolume(float playerVolume)
        {
            return playerVolume >= volumeThreshold;
        }

        public bool IsDefeatedByColorConversion(float convertedVolume, float requiredVolume)
        {
            return convertedVolume >= requiredVolume;
        }

        public bool IsDefeatedByResonancePattern(float patternVolume, float requiredFrequency)
        {
            return Mathf.Approximately(patternVolume, requiredFrequency);
        }
    }
    */
}