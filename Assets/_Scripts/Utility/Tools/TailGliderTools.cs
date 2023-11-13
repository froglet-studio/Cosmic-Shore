#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicShore.Utility.Tools
{
    public class TailGliderTools : Editor
    {
        [MenuItem("FrogletTools/MainScene", false, -1)]
        private static void OpenMainScene()
        {
            // Open the Main Scene in the Editor (do not enter Play Mode)
            EditorSceneManager.OpenScene("Assets/Scenes/Menu_Main.unity", OpenSceneMode.Single);
            Debug.LogFormat("{0} - {1} - Opening Tail Glider Main Menu scene. - Please wait a second for the scene to load.", nameof(TailGliderTools), nameof(OpenMainScene));
        }
        
        [MenuItem("FrogletTools/PhotoBooth", false, -1)]
        private static void OpenPhotoBooth()
        {
            // Open the Photo Booth in the Editor (do not enter Play Mode)
            EditorSceneManager.OpenScene("Assets/Scenes/Tools/PhotoBooth.unity", OpenSceneMode.Single);
            Debug.LogFormat("{0} - {1} - Opening Tail Glider Photo Booth.", nameof(TailGliderTools), nameof(OpenPhotoBooth));
        }
        
        [MenuItem("FrogletTools/RecordingStudio(WIP)", false, -1)]
        private static void OpenRecordingStudio()
        {
            // Open the Photo Booth in the Editor (do not enter Play Mode)
            EditorSceneManager.OpenScene("Assets/Scenes/Tools/Recording Studio.unity", OpenSceneMode.Single);
            Debug.LogFormat("{0} - {1} - Opening Tail Glider Recording Studio.", nameof(TailGliderTools), nameof(OpenRecordingStudio));
        }
        
        [MenuItem("FrogletTools/PlayFabSandbox", false, -1)]
        private static void OpenPlayFabSandbox()
        {
            // Open the Photo Booth in the Editor (do not enter Play Mode)
            EditorSceneManager.OpenScene($"Assets/Scenes/TestScenes/Playfab Sandbox Test/Playfab Sandbox.unity", OpenSceneMode.Single);
            Debug.LogFormat("{0} - {1} - Opening PlayFab Test Sandbox.", nameof(TailGliderTools), nameof(OpenPlayFabSandbox));
        }
    }
}

#endif