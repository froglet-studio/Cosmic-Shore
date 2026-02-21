using UnityEngine;

namespace CosmicShore.Game.Spawning
{
    /// <summary>
    /// Generates points along a cylindrical hex layout.
    /// Migrated from PositioningScheme.HexRing.
    /// </summary>
    public class HexRingGenerator : SpawnableBase
    {
        [Header("Hex Ring")]
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

                var rot = Quaternion.Euler(0, 0, (float)rng.NextDouble() * 360f);
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
