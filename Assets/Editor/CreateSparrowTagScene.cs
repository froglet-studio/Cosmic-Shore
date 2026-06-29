using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CosmicShore.Game.Arcade;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Menu item: FrogletTools > Create > Sparrow Tag Scene
    ///
    /// Duplicates MinigameCellularDuel, then:
    ///   1. Swaps SinglePlayerCellularDuelController → SparrowTagController
    ///   2. Sets scoring config to JoustCollisions (ship-to-ship hits)
    ///   3. Saves the result as MinigameSparrowTag.unity
    ///
    /// TimeBasedTurnMonitor is already present in the source scene and is
    /// reused as-is (default 60 s — adjust duration in the Inspector after creation).
    /// </summary>
    public static class CreateSparrowTagScene
    {
        const string SourceScene = "Assets/_Scenes/Singleplayer Scenes/MinigameCellularDuel.unity";
        const string DestScene   = "Assets/_Scenes/Singleplayer Scenes/MinigameSparrowTag.unity";

        [MenuItem("FrogletTools/Create/Sparrow Tag Scene")]
        static void Build()
        {
            if (!System.IO.File.Exists(SourceScene))
            {
                Debug.LogError($"[SparrowTag] Source scene not found: {SourceScene}");
                return;
            }

            // ── 1. Duplicate source scene ─────────────────────────────────────
            AssetDatabase.CopyAsset(SourceScene, DestScene);
            AssetDatabase.Refresh();

            // ── 2. Open new scene ─────────────────────────────────────────────
            var scene = EditorSceneManager.OpenScene(DestScene, OpenSceneMode.Single);

            // ── 3. Swap controller ────────────────────────────────────────────
            var oldController = Object.FindFirstObjectByType<SinglePlayerCellularDuelController>();
            if (oldController != null)
            {
                SwapController(oldController);
            }
            else
            {
                // Controller might live on a prefab override; fall back to base type
                var baseController = Object.FindFirstObjectByType<MiniGameControllerBase>();
                if (baseController is SinglePlayerCellularDuelController duelCtrl)
                    SwapController(duelCtrl);
                else
                    Debug.LogWarning("[SparrowTag] Could not find SinglePlayerCellularDuelController — add SparrowTagController manually.");
            }

            // ── 4. Update ScoreTracker → JoustCollisions ──────────────────────
            var scoreTracker = Object.FindFirstObjectByType<ScoreTracker>();
            if (scoreTracker != null)
            {
                var so = new SerializedObject(scoreTracker);
                var configs = so.FindProperty("scoringConfigs");
                configs.arraySize = 1;
                var entry = configs.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("Mode").enumValueIndex = (int)ScoringModes.JoustCollisions;
                entry.FindPropertyRelative("Multiplier").floatValue = 1f;
                so.ApplyModifiedProperties();
                Debug.Log("[SparrowTag] ScoreTracker → JoustCollisions x1");
            }
            else
            {
                Debug.LogWarning("[SparrowTag] ScoreTracker not found — set ScoringConfig to JoustCollisions manually.");
            }

            // ── 5. Save ───────────────────────────────────────────────────────
            EditorSceneManager.SaveScene(scene, DestScene);
            AssetDatabase.Refresh();

            Debug.Log($"[SparrowTag] Scene created: {DestScene}");
            Debug.Log("[SparrowTag] TODO: In the Inspector, set TimeBasedTurnMonitor.Duration to your desired match length (e.g. 120 seconds).");
        }

        static void SwapController(SinglePlayerCellularDuelController old)
        {
            var go = old.gameObject;

            // Read shared base-class references before destroying
            var oldSo            = new SerializedObject(old);
            var gameDataRef      = oldSo.FindProperty("gameData").objectReferenceValue;
            var countdownRef     = oldSo.FindProperty("countdownTimer").objectReferenceValue;
            var toggleButtonRef  = oldSo.FindProperty("_onToggleReadyButton").objectReferenceValue;
            var rounds           = oldSo.FindProperty("numberOfRounds").intValue;
            var turnsPerRound    = oldSo.FindProperty("numberOfTurnsPerRound").intValue;

            Object.DestroyImmediate(old);

            var newCtrl = go.AddComponent<SparrowTagController>();
            var newSo   = new SerializedObject(newCtrl);

            newSo.FindProperty("gameData").objectReferenceValue             = gameDataRef;
            newSo.FindProperty("countdownTimer").objectReferenceValue       = countdownRef;
            newSo.FindProperty("_onToggleReadyButton").objectReferenceValue = toggleButtonRef;
            newSo.FindProperty("numberOfRounds").intValue                   = rounds;
            newSo.FindProperty("numberOfTurnsPerRound").intValue            = turnsPerRound;

            newSo.ApplyModifiedProperties();
            Debug.Log($"[SparrowTag] Replaced controller on '{go.name}'");
        }
    }
}
