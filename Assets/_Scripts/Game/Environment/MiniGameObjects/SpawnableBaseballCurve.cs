using CosmicShore.Game.Ship;
using CosmicShore.Game.Environment.Spawning;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game.Environment.MiniGameObjects
{
    public class SpawnableBaseballCurve : SpawnableBase
    {
        [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;

        public float radius = 1.0f;
        public int numSegments = 16;
        public float seamWidth = 0.2f;

        public float b = 0.5f;
        public float c = 0.75f;

        protected override SpawnTrailData[] GenerateTrailData()
        {
            var seam1Points = new SpawnPoint[numSegments];
            var seam2Points = new SpawnPoint[numSegments];

            for (int i = 0; i < numSegments; i++)
            {
                float t = i / (float)numSegments * 2.0f * Mathf.PI;
                float x = radius * Mathf.Cos(Mathf.PI / 2.0f - c) * Mathf.Cos(t) * Mathf.Cos(t / 2.0f + c * Mathf.Sin(2.0f * t));
                float y = radius * Mathf.Cos(Mathf.PI / 2.0f - c) * Mathf.Cos(t) * Mathf.Sin(t / 2.0f + c * Mathf.Sin(2.0f * t));
                float z = radius * Mathf.Sin(Mathf.PI / 2.0f - c) * Mathf.Cos(t);

                var position1 = new Vector3(x, y, z);
                var position2 = new Vector3(x, y, z + seamWidth);

                seam1Points[i] = new SpawnPoint(position1, Quaternion.identity, Vector3.one);
                seam2Points[i] = new SpawnPoint(position2, Quaternion.identity, Vector3.one);
            }

            return new[]
            {
                new SpawnTrailData(seam1Points, false, domain),
                new SpawnTrailData(seam2Points, false, domain)
            };
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            foreach (var td in trailData)
                SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(seed, radius, numSegments, seamWidth, b, c);
        }
    }
}
