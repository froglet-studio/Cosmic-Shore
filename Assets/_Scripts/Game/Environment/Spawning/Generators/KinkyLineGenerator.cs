using UnityEngine;
using CosmicShore.Game.Environment.Spawning;
namespace CosmicShore.Game.Environment.Spawning.Generators
{
    /// <summary>
    /// Generates points along a path with random direction changes.
    /// Each step applies a random rotation to the forward vector.
    /// </summary>
    public class KinkyLineGenerator : SpawnableBase
    {
        [Header("Kinky Line")]
        [SerializeField] int count = 10;
        [SerializeField] float stepLength = 400f;
        [SerializeField] Vector3 origin;

        protected override SpawnPoint[] GeneratePoints()
        {
            var points = new SpawnPoint[count];
            var currentPos = origin;
            var currentRot = Quaternion.identity;

            for (int i = 0; i < count; i++)
            {
                float altitude = (float)rng.NextDouble() * 20f + 70f; // 70-90
                float azimuth = (float)rng.NextDouble() * 360f;

                currentRot = Quaternion.Euler(0f, 0f, azimuth) * Quaternion.Euler(0f, altitude, 0f);
                currentPos += currentRot * (stepLength * Vector3.forward);

                points[i] = new SpawnPoint(currentPos, currentRot);
            }
            return points;
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(count, stepLength, seed, origin);
        }
    }
}
