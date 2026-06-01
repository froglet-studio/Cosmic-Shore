using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Splats
{
    // Runtime-only bootstrap that drops a SplatCloud GameObject into whichever scene
    // is loaded when the active GameMode is GSplat. This avoids editor-time scene cloning
    // (which broke Reflex DI in the duplicated scene) — instead, the arcade launches the
    // stock MinigameFreestyle scene with vessel=Squirrel, and this loader layers the
    // splat cloud on top once the scene is live.
    public static class SplatCloudRuntimeLoader
    {
        const string GameDataResourcePath = "GameDataSO"; // expected under Assets/Resources/
        const string SplatSetResourcePath = "SplatPrismSet_Synthetic";

        private static bool _subscribed;
        private static SplatPrismSet _cachedSet;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_subscribed) return;
            _subscribed = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var gameData = ResolveGameData();
            if (gameData == null) return;
            if (gameData.GameMode != GameModes.GSplat) return;

            // Only attempt on freshly-loaded (non-additive) gameplay scenes.
            if (!scene.IsValid()) return;

            // Avoid double-spawn if the scene already carries a SplatCloud.
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "SplatCloud") return;

            var set = ResolveSplatSet();
            if (set == null)
            {
                Debug.LogWarning("[SplatCloudRuntimeLoader] GSplat mode active but no SplatPrismSet found at Resources/" + SplatSetResourcePath + ".");
                return;
            }

            SpawnCloud(scene, set);
        }

        private static GameDataSO ResolveGameData()
        {
            // GameDataSO is DI-registered. In a built/play-mode flow it's also discoverable
            // via Resources.Load if the team places the canonical asset in a Resources folder.
            // We try Resources first (zero allocations after first call), then fall back to a
            // Find scan against loaded ScriptableObjects.
            var so = Resources.Load<GameDataSO>(GameDataResourcePath);
            if (so != null) return so;

            var all = Resources.FindObjectsOfTypeAll<GameDataSO>();
            return all != null && all.Length > 0 ? all[0] : null;
        }

        private static SplatPrismSet ResolveSplatSet()
        {
            if (_cachedSet != null) return _cachedSet;
            _cachedSet = Resources.Load<SplatPrismSet>(SplatSetResourcePath);
            return _cachedSet;
        }

        private static void SpawnCloud(Scene scene, SplatPrismSet set)
        {
            var go = new GameObject("SplatCloud");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = new Vector3(0f, 0f, 30f);

            var spawner = go.AddComponent<SplatPrismSpawner>();
            spawner.SetSourceSet(set);
            Debug.Log($"[SplatCloudRuntimeLoader] Spawned SplatCloud into scene '{scene.name}' with {set.points?.Length ?? 0} points.");
        }
    }
}
