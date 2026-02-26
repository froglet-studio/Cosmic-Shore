using UnityEngine;
using CosmicShore.Game.Environment;
namespace CosmicShore.Game.Environment
{
    /// <summary>
    /// Generates points along a sinusoidal tube path.
    /// </summary>
    public class CurvyTubeGenerator : SpawnableBase
    {
        [Header("Curvy Tube")]
        [SerializeField] int count = 10;
        [SerializeField] float curviness = 0.5f;
        [SerializeField] float tubeRadius = 20f;
        [SerializeField] Vector3 origin;

        protected override SpawnPoint[] GeneratePoints()
        {
            var points = new SpawnPoint[count];
            for (int i = 0; i < count; i++)
            {
                float t = i * 0.1f;
                var pos = new Vector3(
                    Mathf.Sin(t * curviness) * tubeRadius,
                    t * 10,
                    Mathf.Cos(t * curviness) * tubeRadius
                ) + origin;

                var lookTarget = pos + Vector3.up * 10;
                var rot = SpawnPoint.LookRotation(pos, lookTarget, Vector3.up);
                points[i] = new SpawnPoint(pos, rot);
            }
            return points;
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(count, curviness, tubeRadius, seed, origin);
        }
    }
}
