using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace CosmicShore
{

    public class CreateNewMiniGame : EditorWindow
    {


        //[SerializeProperty]
        bool manta = false;
        bool rhino = false;
        bool squirrel = false;
        bool urchin = false;

        string sceneTemplatePath = "Assets/_Scenes/MiniGames/Template/MiniGameTemplate.unity";
        string sceneSavePath = "Assets/_Scenes/MiniGames/_New/";
        string newMiniGameName = string.Empty;

        [MenuItem("FrogletTools/Create/MiniGame")]

        public static void GetWindow()
        {
            // This method is called when the user selects the menu item in the Editor          
            EditorWindow wnd = GetWindow<CreateNewMiniGame>();

            wnd.titleContent = new GUIContent("MiniGame Editor Window");

            // Limit size of the window
            wnd.minSize = new Vector2(450, 200);
            wnd.maxSize = new Vector2(1920, 720);
        }
        private void OnGUI()
        {
            GUILayout.Label("MiniGame Options", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space();
            newMiniGameName = EditorGUILayout.TextField("MiniGame Name", newMiniGameName);
            EditorGUILayout.Space();

            GUILayout.Label("Ship Options", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            manta = EditorGUILayout.ToggleLeft("Manta", manta);
            rhino = EditorGUILayout.ToggleLeft("Rhino", rhino);
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical();
            squirrel = EditorGUILayout.ToggleLeft("Squirrel", squirrel);
            urchin = EditorGUILayout.ToggleLeft("Urchin", urchin);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();


            EditorGUILayout.Space();

            GUILayout.Label("Action Options", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space();

            EditorGUILayout.Space();

            GUILayout.Label("Misc Options", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space();

            EditorGUILayout.Space();

            {
                CloneScene();
            }

            //Clones MiniGame Template
            void CloneScene()
            {
                if (newMiniGameName == string.Empty)
                {
                    Debug.Log("Missing MiniGame Name");
                    return;
                }

                // Construct the paths for the template and new scene

                string newPath = sceneSavePath + newMiniGameName + ".unity";

                // Copy the template scene to the new path
                AssetDatabase.CopyAsset(sceneTemplatePath, newPath);

                // Load the newly copied scene

                Scene newScene = EditorSceneManager.OpenScene(newPath, OpenSceneMode.Additive);

                // Set the new scene as the active scene
                EditorSceneManager.SetActiveScene(newScene);



                // Save the changes
                EditorSceneManager.SaveScene(newScene);
            }
        }
    }
}