// MiniGamePrefabLibrarySO.cs
using UnityEngine;

namespace CosmicShore.Tools.MiniGameMaker
{
    [CreateAssetMenu(fileName = "MiniGamePrefabLibrary", menuName = "CosmicShore/Editor/Mini-Game Prefab Library")]
    public sealed class MiniGamePrefabLibrarySO : ScriptableObject
    {
        [Header("Locked Commons")]
        public GameObject dependencySpawnerPrefab;
        public GameObject miniGameCameraPrefab;
        public GameObject environmentPrefab;
        public GameObject gameCanvasPrefab;

        [Header("Configurable")]
        public GameObject playerSpawnerPrefab;
        public GameObject shipSpawnerPrefab;

        [Header("Defaults / Profiles")]
        public ScriptableObject defaultMiniGameData;   // MiniGameDataSO (optional)
        public MiniGameProfileSO defaultProfile;       // <-- ADD THIS
    }
}