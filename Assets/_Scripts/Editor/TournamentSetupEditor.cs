#if UNITY_EDITOR

using CosmicShore.Game.Arcade.Tournament;
using CosmicShore.Soap;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Editor utility for setting up Tournament mode components in scenes.
    /// Use FrogletTools > Tournament menu items after opening the target scene.
    /// </summary>
    public static class TournamentSetupEditor
    {
        const string GameDataPath = "Assets/_SO_Assets/DataContainers";

        [MenuItem("FrogletTools/Tournament/Add TournamentManager to Scene")]
        static void AddTournamentManager()
        {
            if (Object.FindAnyObjectByType<TournamentManager>() != null)
            {
                Debug.Log("[TournamentSetup] TournamentManager already exists in the scene.");
                EditorUtility.DisplayDialog("Tournament Setup",
                    "TournamentManager already exists in the current scene.", "OK");
                return;
            }

            var go = new GameObject("TournamentManager");
            var tm = go.AddComponent<TournamentManager>();

            // Try to wire up GameDataSO automatically
            var gameData = FindGameDataAsset();
            if (gameData != null)
            {
                var so = new SerializedObject(tm);
                var prop = so.FindProperty("gameData");
                if (prop != null)
                {
                    prop.objectReferenceValue = gameData;
                    so.ApplyModifiedProperties();
                }
            }

            Undo.RegisterCreatedObjectUndo(go, "Add TournamentManager");
            Selection.activeGameObject = go;

            Debug.Log("[TournamentSetup] TournamentManager added. " +
                      "Make sure this scene loads before any game scene (e.g. Menu_Main or Bootstrap).");
            EditorUtility.DisplayDialog("Tournament Setup",
                "TournamentManager added to the scene.\n\n" +
                "Place it in your main/bootstrap scene so it persists across game scenes.\n\n" +
                (gameData != null
                    ? "GameDataSO was auto-wired."
                    : "IMPORTANT: Assign the GameDataSO field in the Inspector."),
                "OK");
        }

        [MenuItem("FrogletTools/Tournament/Add Tournament UI to Game Scene")]
        static void AddTournamentUIToGameScene()
        {
            var gameData = FindGameDataAsset();

            // Add TournamentEndGameController
            if (Object.FindAnyObjectByType<TournamentEndGameController>() == null)
            {
                var endGameGo = new GameObject("TournamentEndGameController");
                var endGame = endGameGo.AddComponent<TournamentEndGameController>();

                if (gameData != null)
                {
                    var so = new SerializedObject(endGame);
                    var prop = so.FindProperty("gameData");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = gameData;
                        so.ApplyModifiedProperties();
                    }
                }

                Undo.RegisterCreatedObjectUndo(endGameGo, "Add TournamentEndGameController");
                Debug.Log("[TournamentSetup] TournamentEndGameController added.");
            }
            else
            {
                Debug.Log("[TournamentSetup] TournamentEndGameController already exists.");
            }

            // Add TournamentScoreboard
            if (Object.FindAnyObjectByType<TournamentScoreboard>() == null)
            {
                var scoreboardGo = new GameObject("TournamentScoreboard");
                var scoreboard = scoreboardGo.AddComponent<TournamentScoreboard>();

                if (gameData != null)
                {
                    var so = new SerializedObject(scoreboard);
                    var prop = so.FindProperty("gameData");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = gameData;
                        so.ApplyModifiedProperties();
                    }
                }

                Undo.RegisterCreatedObjectUndo(scoreboardGo, "Add TournamentScoreboard");
                Debug.Log("[TournamentSetup] TournamentScoreboard added. " +
                          "Wire up the UI references (panels, text, buttons) in the Inspector.");
            }
            else
            {
                Debug.Log("[TournamentSetup] TournamentScoreboard already exists.");
            }

            EditorUtility.DisplayDialog("Tournament Setup",
                "Tournament UI components added to the scene.\n\n" +
                "Next steps:\n" +
                "1. Wire TournamentEndGameController.regularScoreboardButtons to the " +
                "existing scoreboard's button container\n" +
                "2. Create UI panels for TournamentScoreboard and wire text/button references\n" +
                (gameData != null ? "\nGameDataSO was auto-wired." : "\nIMPORTANT: Assign GameDataSO fields in the Inspector."),
                "OK");
        }

        [MenuItem("FrogletTools/Tournament/Add TournamentGameLauncher to Scene")]
        static void AddTournamentGameLauncher()
        {
            if (Object.FindAnyObjectByType<TournamentGameLauncher>() != null)
            {
                Debug.Log("[TournamentSetup] TournamentGameLauncher already exists in the scene.");
                EditorUtility.DisplayDialog("Tournament Setup",
                    "TournamentGameLauncher already exists in the current scene.", "OK");
                return;
            }

            var go = new GameObject("TournamentGameLauncher");
            var launcher = go.AddComponent<TournamentGameLauncher>();

            var gameData = FindGameDataAsset();
            if (gameData != null)
            {
                var so = new SerializedObject(launcher);

                var gameDataProp = so.FindProperty("gameData");
                if (gameDataProp != null)
                    gameDataProp.objectReferenceValue = gameData;

                // Try to find ArcadeGameConfigSO
                var configGuids = AssetDatabase.FindAssets("t:ArcadeGameConfigSO");
                if (configGuids.Length > 0)
                {
                    var configPath = AssetDatabase.GUIDToAssetPath(configGuids[0]);
                    var config = AssetDatabase.LoadAssetAtPath<ArcadeGameConfigSO>(configPath);
                    var configProp = so.FindProperty("config");
                    if (configProp != null)
                        configProp.objectReferenceValue = config;
                }

                so.ApplyModifiedProperties();
            }

            Undo.RegisterCreatedObjectUndo(go, "Add TournamentGameLauncher");
            Selection.activeGameObject = go;

            Debug.Log("[TournamentSetup] TournamentGameLauncher added. " +
                      "Wire the startGameRequestedEvent field to the same ScriptableEventNoParam " +
                      "used by ArcadeGameConfigureModal.");
            EditorUtility.DisplayDialog("Tournament Setup",
                "TournamentGameLauncher added.\n\n" +
                "Wire the 'startGameRequestedEvent' field to the same ScriptableEventNoParam " +
                "used by ArcadeGameConfigureModal's startGameRequestedEvent.",
                "OK");
        }

        static GameDataSO FindGameDataAsset()
        {
            var guids = AssetDatabase.FindAssets("t:GameDataSO");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<GameDataSO>(path);
            }
            return null;
        }
    }
}

#endif
