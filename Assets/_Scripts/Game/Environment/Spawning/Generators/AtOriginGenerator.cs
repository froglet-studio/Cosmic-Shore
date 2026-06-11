using UnityEngine;

namespace CosmicShore.Game.Spawning
{
    /// <summary>
    /// Generates a single point at the origin with no rotation.
    /// </summary>
    public class AtOriginGenerator : SpawnableBase
    {
        [Header("At Origin")]
        [SerializeField] int count = 1;
        [SerializeField] bool randomRotation;
        [SerializeField] Vector3 origin;

        protected override SpawnPoint[] GeneratePoints()
        {
            var points = new SpawnPoint[count];
            for (int i = 0; i < count; i++)
            {
                Quaternion rot = randomRotation ? RandomRotation() : Quaternion.identity;
                points[i] = new SpawnPoint(origin, rot);
            }
            return points;
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(count, randomRotation, seed, origin);
        }

        private Quaternion RandomRotation()
        {
            float u0 = (float)rng.NextDouble();
            float u1 = (float)rng.NextDouble();
            float u2 = (float)rng.NextDouble();
            float sqrt1MinusU0 = Mathf.Sqrt(1f - u0);
            float sqrtU0 = Mathf.Sqrt(u0);
            return new Quaternion(
                sqrt1MinusU0 * Mathf.Sin(2f * Mathf.PI * u1),
                sqrt1MinusU0 * Mathf.Cos(2f * Mathf.PI * u1),
                sqrtU0 * Mathf.Sin(2f * Mathf.PI * u2),
                sqrtU0 * Mathf.Cos(2f * Mathf.PI * u2)
            );
        }
    }
}
