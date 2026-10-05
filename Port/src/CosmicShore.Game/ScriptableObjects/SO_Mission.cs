// Ported from Assets/_Scripts/ScriptableObjects/SO_Mission.cs (Arc F 2b-iii) — verbatim;
// UnityEngine → CosmicShore.Engine.
using CosmicShore.Engine;
using CosmicShore.Data;
using System;
namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Mission", menuName = "ScriptableObjects/Game/Mission", order = 2)]
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

        public virtual void Spawn(Vector3 spawnPoint, Domains domain)
        {
            var threat = GameObject.Instantiate(threatPrefab, spawnPoint, Quaternion.identity);
            ITeamAssignable iTeam = threat.GetComponent<ITeamAssignable>();
            if (iTeam != null)
                iTeam.SetTeam(domain);
        }
    }
}
