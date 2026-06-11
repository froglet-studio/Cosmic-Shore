using UnityEngine;

namespace CosmicShore.Game.Spawning
{
    /// <summary>
    /// Generates points from pre-saved MazeData ScriptableObjects.
    /// </summary>
    public class SavedMazeGenerator : SpawnableBase
    {
        [Header("Saved Maze")]
        [SerializeField] MazeData[] mazeData;
        [SerializeField] float rotationAngle;
        [SerializeField] Vector3 origin;

        protected override SpawnPoint[] GeneratePoints()
        {
            int index = Mathf.Clamp(intensityLevel - 1, 0, mazeData.Length - 1);
            if (mazeData == null || mazeData.Length == 0 || mazeData[index] == null)
                return System.Array.Empty<SpawnPoint>();

            var walls = mazeData[index].walls;
            var rot = Quaternion.Euler(0, 0, rotationAngle);
            var points = new SpawnPoint[walls.Count];

            for (int i = 0; i < walls.Count; i++)
            {
                points[i] = new SpawnPoint(
                    rot * (walls[i].position + origin),
                    rot * walls[i].rotation
                );
            }

            return points;
        }

        protected override int GetParameterHash()
        {
            // MazeData is a ScriptableObject — use instance ID for identity
            int mazeHash = 0;
            if (mazeData != null)
            {
                foreach (var md in mazeData)
                    mazeHash = System.HashCode.Combine(mazeHash, md != null ? md.GetInstanceID() : 0);
            }
            return System.HashCode.Combine(mazeHash, intensityLevel, rotationAngle, seed, origin);
        }
    }
}
