using CosmicShore.Game.Environment;
using UnityEngine;

namespace CosmicShore.Game.Environment
{
    public class SpawnableFlora : SpawnableBase
    {
        [SerializeField] Flora flora;

        protected override SpawnPoint[] GeneratePoints()
        {
            return new[] { new SpawnPoint(Vector3.zero, Quaternion.identity) };
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            if (flora == null) return;
            var instance = Instantiate(flora, container.transform);
            instance.transform.localPosition = Vector3.zero;
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(seed);
        }
    }
}
