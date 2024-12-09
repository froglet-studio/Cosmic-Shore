using UnityEngine;

namespace CosmicShore
{
    public class ThreatSpawner : MonoBehaviour
    {
        [SerializeField] public Transform nodeCenter; // Center of the node - default position to base spawn patterns around

        public void SpawnThreat(Threat threat)
        {
            Vector3 spawnPoint = GetSpawnPoint(threat.spawnMode);
            threat.Spawn(spawnPoint);
        }

        public void SpawnThreat(Threat threat, Vector3 spawnOrigin)
        {
            Vector3 spawnPoint = GetSpawnPoint(threat.spawnMode, spawnOrigin);
            threat.Spawn(spawnPoint);
        }

        Vector3 GetSpawnPoint(SpawnMode spawnMode)
        {
            return GetSpawnPoint(spawnMode, nodeCenter.position);
        }

            Vector3 GetSpawnPoint(SpawnMode spawnMode, Vector3 spawnOrigin)
        {
            switch (spawnMode)
            {
                case SpawnMode.ConcentratedInvasion:
                    return spawnOrigin + Random.onUnitSphere * 10f; // Close proximity spawn
                case SpawnMode.RandomSurfaceScatter:
                    return spawnOrigin + Random.onUnitSphere * 20f; // Surface of a larger sphere
                case SpawnMode.LocalizedAmbush:
                    return spawnOrigin + Random.insideUnitSphere * 5f; // Small radius ambush spawn
                case SpawnMode.PathBasedDeployment:
                    return new Vector3(Random.Range(-10f, 10f), Random.Range(-10f, 10f), spawnOrigin.z); // X-Y paths into the node
                case SpawnMode.SphereInterdiction:
                    return spawnOrigin + Random.insideUnitSphere * 15f; // Random points within a sphere
                default:
                    return spawnOrigin; // Default to center if no mode is specified
            }
        }
    }
}