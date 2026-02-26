using CosmicShore.Game.Ship;
using CosmicShore.Game.Environment;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game.Environment
{
    public class SpawnableHeart : SpawnableBase
    {
        [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;
        [SerializeField] int blockCount = 60;

        protected override SpawnPoint[] GeneratePoints()
        {
            var points = new SpawnPoint[blockCount];
            for (int block = 0; block < blockCount; block++)
            {
                var t = ((float)block / blockCount) * Mathf.PI * 2;
                var x = Mathf.Pow(Mathf.Sin(t), 3) * 16;
                var y = (13 * Mathf.Cos(t)) - (5 * Mathf.Cos(2 * t)) - (2 * Mathf.Cos(3 * t)) - (Mathf.Cos(4 * t));
                var position = new Vector3(x, y, 0);
                var rotation = SpawnPoint.LookRotation(position, Vector3.zero, Vector3.up);
                points[block] = new SpawnPoint(position, rotation, Vector3.one);
            }
            return points;
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            foreach (var td in trailData)
                SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(blockCount, seed);
        }
    }
}
