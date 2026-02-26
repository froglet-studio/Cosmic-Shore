using UnityEngine;
using CosmicShore.Game.Environment;
using System.Linq;
namespace CosmicShore.Game.Environment
{
    /// <summary>
    /// Generates points at random positions on a cubic voxel grid.
    /// </summary>
    public class CubicGenerator : SpawnableBase
    {
        [Header("Cubic Grid")]
        [SerializeField] int count = 10;
        [SerializeField] int volumeSideLength = 100;
        [SerializeField] int voxelSideLength = 10;
        [SerializeField] Vector3 origin;

        protected override SpawnPoint[] GeneratePoints()
        {
            int steps = volumeSideLength / Mathf.Max(voxelSideLength, 1);
            var points = new SpawnPoint[count];
            for (int i = 0; i < count; i++)
            {
                int x = rng.Next(0, steps) * voxelSideLength;
                int y = rng.Next(0, steps) * voxelSideLength;
                int z = rng.Next(0, steps) * voxelSideLength;
                var pos = new Vector3(x, y, z) + origin;
                var rot = SpawnPoint.LookRotation(-pos.normalized, Vector3.up);
                points[i] = new SpawnPoint(pos, rot);
            }
            return points;
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(count, volumeSideLength, voxelSideLength, seed, origin);
        }
    }
}
