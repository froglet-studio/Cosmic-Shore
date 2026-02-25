using UnityEngine;
using CosmicShore.Game.Environment.Spawning;
namespace CosmicShore.Game.Environment.Spawning.Generators
{
    /// <summary>
    /// Generates points uniformly distributed inside a sphere.
    /// </summary>
    public class SphereUniformGenerator : SpawnableBase
    {
        [Header("Sphere Uniform")]
        [SerializeField] float radius = 250f;
        [SerializeField] int count = 10;
        [SerializeField] Vector3 origin;

        protected override SpawnPoint[] GeneratePoints()
        {
            var points = new SpawnPoint[count];
            for (int i = 0; i < count; i++)
            {
                var pos = RandomInsideUnitSphere() * radius + origin;
                var rot = RandomRotation();
                points[i] = new SpawnPoint(pos, rot);
            }
            return points;
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(radius, count, seed, origin);
        }

        private Vector3 RandomInsideUnitSphere()
        {
            float u = (float)rng.NextDouble();
            float v = (float)rng.NextDouble();
            float w = (float)rng.NextDouble();
            float theta = 2f * Mathf.PI * u;
            float phi = Mathf.Acos(2f * v - 1f);
            float r = Mathf.Pow(w, 1f / 3f);
            return new Vector3(
                r * Mathf.Sin(phi) * Mathf.Cos(theta),
                r * Mathf.Sin(phi) * Mathf.Sin(theta),
                r * Mathf.Cos(phi)
            );
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
