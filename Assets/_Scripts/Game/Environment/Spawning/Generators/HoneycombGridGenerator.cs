using UnityEngine;

namespace CosmicShore.Game.Spawning
{
    /// <summary>
    /// Generates points on a hexagonal honeycomb grid.
    /// </summary>
    public class HoneycombGridGenerator : SpawnableBase
    {
        [Header("Honeycomb Grid")]
        [SerializeField] int count = 10;
        [SerializeField] int gridWidth = 10;
        [SerializeField] int gridHeight = 10;
        [SerializeField] float cellSize = 10f;
        [SerializeField] Vector3 origin;

        protected override SpawnPoint[] GeneratePoints()
        {
            var points = new SpawnPoint[count];
            for (int i = 0; i < count; i++)
            {
                int row = rng.Next(0, gridHeight);
                int col = rng.Next(0, gridWidth);
                float x = col * cellSize * 1.5f;
                float z = row * cellSize * Mathf.Sqrt(3) +
                          (col % 2 == 0 ? 0 : cellSize * Mathf.Sqrt(3) / 2f);

                var pos = new Vector3(x, 0, z) + origin;
                var rot = Quaternion.Euler(0, rng.Next(0, 6) * 60, 0);
                points[i] = new SpawnPoint(pos, rot);
            }
            return points;
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(count, gridWidth, gridHeight, cellSize, seed, origin);
        }
    }
}
