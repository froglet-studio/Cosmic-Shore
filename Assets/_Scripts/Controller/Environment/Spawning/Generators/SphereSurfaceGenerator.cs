using UnityEngine;
using CosmicShore.Gameplay;
using System.Linq;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Generates points distributed on the surface of a sphere
    /// with angular spread controlled by difficulty angle.
    /// </summary>
    public class SphereSurfaceGenerator : SpawnableBase
    {
        [Header("Sphere Surface")]
        [SerializeField] float radius = 250f;
        [SerializeField] int count = 10;
        [SerializeField] int difficultyAngle = 90;
        [SerializeField] Vector3 origin;

        protected override SpawnPoint[] GeneratePoints()
        {
            var points = new SpawnPoint[count];
            for (int i = 0; i < count; i++)
            {
                float azimuth = rng.Next(i * (360 / Mathf.Max(count, 1)), i * (360 / Mathf.Max(count, 1)) + 20);
                float elevation = rng.Next(
                    Mathf.Max(difficultyAngle - 20, 40),
                    Mathf.Max(difficultyAngle + 20, 40));

                var pos = Quaternion.Euler(0, 0, azimuth) *
                          (Quaternion.Euler(0, elevation, 0) * (radius * Vector3.forward)) + origin;

                var rot = Quaternion.LookRotation(-pos.normalized, Vector3.up);
                points[i] = new SpawnPoint(pos, rot);
            }
            return points;
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(radius, count, difficultyAngle, seed, origin);
        }
    }
}
