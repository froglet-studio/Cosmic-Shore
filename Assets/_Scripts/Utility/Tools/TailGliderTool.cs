#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace _Scripts.Utility.Tools
{
    public class TailGliderTool : Editor
    {
        [MenuItem("FrogletTools/MainScene", false, -1)]
        private static void OpenMainScene()
        {
            // Open the Main Scene in the Editor (do not enter Play Mode)
            EditorSceneManager.OpenScene("Assets/Scenes/Menu_Main.unity", OpenSceneMode.Single);
            Debug.LogFormat("{0} - {1} - This is Tail Glider Main Menu scene. - Please wait a second for the scene to load.", nameof(TailGliderTool), nameof(OpenMainScene));
        }
        
        [MenuItem("FrogletTools/PhotoBooth", false, -1)]
        private static void OpenPhotoBooth()
        {
            // Open the Photo Booth in the Editor (do not enter Play Mode)
            EditorSceneManager.OpenScene("Assets/Scenes/Tools/PhotoBooth.unity", OpenSceneMode.Single);
            Debug.LogFormat("{0} - {1} - This is Tail Glider Photo Booth.", nameof(TailGliderTool), nameof(OpenPhotoBooth));
        }
    }
}

#endif