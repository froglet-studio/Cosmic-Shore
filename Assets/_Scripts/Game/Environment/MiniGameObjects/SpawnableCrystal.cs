using CosmicShore.Game.Spawning;
using UnityEngine;

namespace CosmicShore.Game
{
    public class SpawnableCrystal : SpawnableBase
    {
        [SerializeField] Crystal Crystal;

        protected override SpawnPoint[] GeneratePoints()
        {
            return new[] { new SpawnPoint(Vector3.zero, Quaternion.identity) };
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            if (Crystal == null) return;
            var crystal = Instantiate(Crystal, container.transform);
            crystal.transform.localPosition = Vector3.zero;
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(seed);
        }
    }
}
