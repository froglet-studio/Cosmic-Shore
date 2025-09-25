using UnityEngine;

namespace CosmicShore
{
    public class ThreatSpawner : MonoBehaviour
    {
        [SerializeField] public Transform NodeCenter; // Center of the node - default position to base spawn patterns around
        [SerializeField] public float ConcentratedInvasionRadius = 10f;
        [SerializeField] public float RandomSurfaceScatterRadius = 20f;
        [SerializeField] public float LocalizedAmbushRadius = 5f; 
        [SerializeField] public float SphereInterdictionRadius = 15f; 

        public void SpawnThreat(Threat threat, Domains domain)
        {
            Vector3 spawnPoint = GetSpawnPoint(threat.spawnMode);
            threat.Spawn(spawnPoint, domain);
        }

        public void SpawnThreat(Threat threat, Domains domain, Vector3 spawnOrigin)
        {
            Vector3 spawnPoint = GetSpawnPoint(threat.spawnMode, spawnOrigin);
            threat.Spawn(spawnPoint, domain);
        }

        Vector3 GetSpawnPoint(SpawnMode spawnMode)
        {
            return GetSpawnPoint(spawnMode, NodeCenter.position);
        }

            Vector3 GetSpawnPoint(SpawnMode spawnMode, Vector3 spawnOrigin)
        {
            switch (spawnMode)
            {
                case SpawnMode.ConcentratedInvasion:
                    return spawnOrigin + Random.onUnitSphere * ConcentratedInvasionRadius; // Close proximity spawn
                case SpawnMode.RandomSurfaceScatter:
                    return spawnOrigin + Random.onUnitSphere * RandomSurfaceScatterRadius; // Surface of a larger sphere
                case SpawnMode.LocalizedAmbush:
                    return spawnOrigin + Random.insideUnitSphere * LocalizedAmbushRadius; // Small radius ambush spawn
                case SpawnMode.PathBasedDeployment:
                    return new Vector3(Random.Range(-10f, 10f), Random.Range(-10f, 10f), spawnOrigin.z); // X-Y paths into the node
                case SpawnMode.SphereInterdiction:
                    return spawnOrigin + Random.insideUnitSphere * SphereInterdictionRadius; // Random points within a sphere
                default:
                    return spawnOrigin; // Default to center if no mode is specified
            }
        }
    }
}