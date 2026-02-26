using UnityEngine;
using CosmicShore.Game.Environment;
namespace CosmicShore.Game.Environment
{
    /// <summary>
    /// Generates points along the surface of a cylinder with random angle tilt.
    /// </summary>
    public class CylinderSurfaceGenerator : SpawnableBase
    {
        [Header("Cylinder Surface")]
        [SerializeField] int count = 10;
        [SerializeField] float radius = 250f;
        [SerializeField] float spacing = 400f;
        [SerializeField] Vector3 origin;

        protected override SpawnPoint[] GeneratePoints()
        {
            var points = new SpawnPoint[count];
            for (int i = 0; i < count; i++)
            {
                var pos = new Vector3(
                    radius * Mathf.Sin(i),
                    radius * Mathf.Cos(i),
                    i * spacing
                ) + origin;

                var axis = Vector3.forward
                           + (((float)rng.NextDouble() - 0.5f) * Vector3.right)
                           + (((float)rng.NextDouble() - 0.5f) * Vector3.up);
                var rot = Quaternion.AngleAxis((float)rng.NextDouble() * 180f, axis.normalized);

                points[i] = new SpawnPoint(pos, rot);
            }
            return points;
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(count, radius, spacing, seed, origin);
        }
    }
}
