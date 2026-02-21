using UnityEngine;

namespace CosmicShore.Game.Spawning
{
    /// <summary>
    /// Generates points at random positions on a 3D grid.
    /// Migrated from PositioningScheme.MazeGrid.
    /// </summary>
    public class MazeGridGenerator : SpawnableBase
    {
        [Header("Maze Grid")]
        [SerializeField] int count = 10;
        [SerializeField] int gridWidth = 10;
        [SerializeField] int gridHeight = 10;
        [SerializeField] int gridThickness = 10;
        [SerializeField] float cellSize = 10f;
        [SerializeField] Vector3 origin;

        protected override SpawnPoint[] GeneratePoints()
        {
            var points = new SpawnPoint[count];
            for (int i = 0; i < count; i++)
            {
                int x = rng.Next(0, gridWidth);
                int y = rng.Next(0, gridHeight);
                int z = rng.Next(0, gridThickness);
                var pos = new Vector3(x * cellSize, y * cellSize, z * cellSize) + origin;
                var rot = Quaternion.Euler(rng.Next(0, 4) * 90, rng.Next(0, 4) * 90, rng.Next(0, 4) * 90);
                points[i] = new SpawnPoint(pos, rot);
            }
            return points;
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(count, gridWidth, gridHeight, gridThickness, cellSize, seed, origin);
        }
    }
}
