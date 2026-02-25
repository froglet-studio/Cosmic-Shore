using UnityEngine;
using CosmicShore.Game.Environment.Spawning;
namespace CosmicShore.Game.Environment.Spawning.Generators
{
    /// <summary>
    /// Generates points along a tube path with periodic random direction jitter.
    /// </summary>
    public class KinkyTubeGenerator : SpawnableBase
    {
        [Header("Kinky Tube")]
        [SerializeField] int count = 10;
        [SerializeField] float stepLength = 10f;
        [SerializeField] int jitterInterval = 5;
        [SerializeField] int maxJitterAngle = 60;
        [SerializeField] Vector3 origin;

        protected override SpawnPoint[] GeneratePoints()
        {
            var points = new SpawnPoint[count];
            var currentPos = origin;
            var currentRot = Quaternion.identity;

            for (int i = 0; i < count; i++)
            {
                if (i % jitterInterval == 0)
                {
                    currentRot *= Quaternion.Euler(
                        rng.Next(-maxJitterAngle, maxJitterAngle + 1),
                        rng.Next(-maxJitterAngle, maxJitterAngle + 1),
                        rng.Next(-maxJitterAngle, maxJitterAngle + 1)
                    );
                }

                points[i] = new SpawnPoint(currentPos, currentRot);
                currentPos += currentRot * Vector3.forward * stepLength;
            }
            return points;
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(count, stepLength, jitterInterval, maxJitterAngle, seed, origin);
        }
    }
}
