using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "MazeData", menuName = "Maze/MazeData")]
public class MazeData : ScriptableObject
{
    [System.Serializable]
    public struct WallData
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    public List<WallData> walls = new List<WallData>();
}
