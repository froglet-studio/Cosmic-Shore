#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicShore.Utility.Tools
{
    public class FrogletTools : Editor
    {
        [MenuItem("FrogletTools/MainScene", false, -1)]
        private static void OpenMainScene()
        {
            // Open the Main Scene in the Editor (do not enter Play Mode)
            EditorSceneManager.OpenScene("Assets/_Scenes/Menu_Main.unity", OpenSceneMode.Single);
            Debug.LogFormat("{0} - {1} - Opening Tail Glider Main Menu scene. - Please wait a second for the scene to load.", nameof(FrogletTools), nameof(OpenMainScene));
        }
        
        [MenuItem("FrogletTools/PhotoBooth", false, -1)]
        private static void OpenPhotoBooth()
        {
            // Open the Photo Booth in the Editor (do not enter Play Mode)
            EditorSceneManager.OpenScene("Assets/_Scenes/Tools/PhotoBooth.unity", OpenSceneMode.Single);
            Debug.LogFormat("{0} - {1} - Opening Tail Glider Photo Booth.", nameof(FrogletTools), nameof(OpenPhotoBooth));
        }
        
        [MenuItem("FrogletTools/RecordingStudio(WIP)", false, -1)]
        private static void OpenRecordingStudio()
        {
            // Open the Photo Booth in the Editor (do not enter Play Mode)
            EditorSceneManager.OpenScene("Assets/_Scenes/Tools/Recording Studio.unity", OpenSceneMode.Single);
            Debug.LogFormat("{0} - {1} - Opening Tail Glider Recording Studio.", nameof(FrogletTools), nameof(OpenRecordingStudio));
        }
        
        [MenuItem("FrogletTools/PlayFabSandbox", false, -1)]
        private static void OpenPlayFabSandbox()
        {
            // Open the Photo Booth in the Editor (do not enter Play Mode)
            EditorSceneManager.OpenScene($"Assets/_Scenes/TestScenes/Playfab Sandbox Test/Playfab Sandbox.unity", OpenSceneMode.Single);
            Debug.LogFormat("{0} - {1} - Opening PlayFab Test Sandbox.", nameof(FrogletTools), nameof(OpenPlayFabSandbox));
        }
    }
}

#endif