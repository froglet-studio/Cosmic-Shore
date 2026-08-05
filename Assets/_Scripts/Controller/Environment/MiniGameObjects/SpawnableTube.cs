using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Gameplay
{
    public class SpawnableTube : SpawnableBase
    {
        [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;
        [SerializeField] int radius = 3;
        [SerializeField] int length = 20;
        [SerializeField] int segments = 8;
        [SerializeField] float blockSize = 1f;

        protected override SpawnPoint[] GeneratePoints()
        {
            var points = new SpawnPoint[length * segments];
            int idx = 0;

            for (int z = 0; z < length; z++)
            {
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * (2 * Mathf.PI / segments);
                    Vector3 position = new Vector3(
                        Mathf.Cos(angle) * radius * blockSize,
                        Mathf.Sin(angle) * radius * blockSize,
                        z * blockSize
                    );

                    // Original: CreateBlock(position, -position.normalized, ...)
                    // Old CreateBlock with flip=true computes forward = position - lookPosition
                    // lookPosition = -position.normalized, so forward = position + position.normalized
                    Vector3 forward = position + position.normalized;
                    var rotation = SpawnPoint.LookRotation(forward, Vector3.up);

                    points[idx++] = new SpawnPoint(position, rotation, Vector3.one * blockSize);
                }
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
            return System.HashCode.Combine(radius, length, segments, blockSize, seed);
        }
    }
}
