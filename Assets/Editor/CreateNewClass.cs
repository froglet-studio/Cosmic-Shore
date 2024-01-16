using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

/*
    get class name
    Create "Class"ClassSO
    TODO Create Prefab or GameObject and assign "Class"ClassSO to it
    TODO Add "Class"ClassSO to Hangar in Main_menu scene
    TODO Add to SO_Class and SO_AllShips  DO we need both? and should we rename to SO_AllClasses

    TODO For all 4 Elements create "ElementClass"TrainingGameSO
    TODO For all 4 Elements append AllGamesSO with "ElementClass"TrainingGameSO

    TODO For all 4 Elements create "ElementClass"TrainingScene (formally MiniGames) 
    TODO For all 4 Elements create "ElementClass"TrainingVesselSO with 1 Element maxed out

    TODO For all 4 Elements create "ElementClass"UpgradeVesselSO
    
    TODO Create one "Class"FreestyleVesselSO
    TODO Add to Freestyle Game SO
 
 */

namespace CosmicShore
{
    public class CreateNewClass : EditorWindow
    {
        string newClassName = string.Empty;

        List<string> elements = new List<string>() { "Time", "Mass", "Charge", "Space" };


        string classSavePath = "Assets/_SO_Assets/_TEMP/";  //TODO /Class/_New/" or finalized folder
        string trainingVesselSavePath = "Assets/_SO_Assets/_TEMP/";  //TODO /Vessel/Training/_New/" or finalized folder
        string UpgradeVesselSavePath = "Assets/_SO_Assets/_TEMP/";  //TODO /Vessel/Upgrade/_New/" or finalized folder
        string FreestyleVesselSavePath = "Assets/_SO_Assets/_TEMP/";  //TODO /Vessel/Freestyle/_New/" or finalized folder

        #region Scriptable Objects
        // Class SO
        SO_Ship newClassSO;
        // Training Vessel SO
        SO_Vessel newTrainingVesselSO_1;
        SO_Vessel newTrainingVesselSO_2;
        SO_Vessel newTrainingVesselSO_3;
        SO_Vessel newTrainingVesselSO_4;
        // Upgrade Vessel SO
        SO_Vessel newUpgradeVesselSO_1;
        SO_Vessel newUpgradeVesselSO_2;
        SO_Vessel newUpgradeVesselSO_3;
        SO_Vessel newUpgradeVesselSO_4;
        // Freestyle Vessel SO
        SO_Vessel newFreestyleVesselSO_1;
        #endregion

        private void OnEnable()
        {
            //IMPORTANT Instances of Scriptable Objects must be created here
            CreateClassSOInstanceFromType();
            CreateElementClassSOInstancesFromType();
        }

        #region Create Empty Scriptable Objects
        // Create Empty Class Scriptable Objects
        private void CreateClassSOInstanceFromType()
        {
            // Create an instance of the Class ScriptableObject
            newClassSO = SO_Ship.CreateInstance<SO_Ship>();
        }

        // Create Empty Vessel Scriptable Objects
        private void CreateElementClassSOInstancesFromType()
        {
            List<SO_Vessel> TrainingVessels = new List<SO_Vessel>();
            // Create an empty instance of the Element_Class Training ScriptableObject
            newTrainingVesselSO_1 = SO_Vessel.CreateInstance<SO_Vessel>();
            newTrainingVesselSO_2 = SO_Vessel.CreateInstance<SO_Vessel>();
            newTrainingVesselSO_3 = SO_Vessel.CreateInstance<SO_Vessel>();
            newTrainingVesselSO_4 = SO_Vessel.CreateInstance<SO_Vessel>();
            TrainingVessels.Add(newTrainingVesselSO_1);TrainingVessels.Add(newTrainingVesselSO_2); TrainingVessels.Add(newTrainingVesselSO_3); TrainingVessels.Add(newTrainingVesselSO_4);

            List<SO_Vessel> UpgradeVessels = new List<SO_Vessel>();
            // Create an empty instance of the Element_Class Upgrade ScriptableObject
            newUpgradeVesselSO_1 = SO_Vessel.CreateInstance<SO_Vessel>();
            newUpgradeVesselSO_2 = SO_Vessel.CreateInstance<SO_Vessel>();
            newUpgradeVesselSO_3 = SO_Vessel.CreateInstance<SO_Vessel>();
            newUpgradeVesselSO_4 = SO_Vessel.CreateInstance<SO_Vessel>();
            UpgradeVessels.Add(newUpgradeVesselSO_1); UpgradeVessels.Add(newUpgradeVesselSO_2); UpgradeVessels.Add(newUpgradeVesselSO_3); UpgradeVessels.Add(newUpgradeVesselSO_4);

            // Create an empty instance of the Element_Class Freestyle ScriptableObject
            newFreestyleVesselSO_1 = SO_Vessel.CreateInstance<SO_Vessel>();
        }

        #endregion


        [MenuItem("FrogletTools/Create/Class")]
        public static void GetWindow()
        {
            // This method is called when the user selects the menu item in the Editor          
            EditorWindow wnd = GetWindow<CreateNewClass>();

            wnd.titleContent = new GUIContent("MiniGame Editor Window");

            // Limit size of the window
            wnd.minSize = new Vector2(450, 200);
            wnd.maxSize = new Vector2(1920, 720);
        }
        private void OnGUI()
        {
            //Get new Class name
            GUILayout.Label("Enter new Class name", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space();
            newClassName = EditorGUILayout.TextField("Class Name", newClassName);  
            EditorGUILayout.Space();

            if (GUILayout.Button("Create Class"))
            {
                // Create a ClassSO asset
                newClassSO = ScriptableObjectEditor.CreateClassScriptableObject(newClassName);

                if (newClassSO != null)
                {
                    Debug.Log("Asset created and reference obtained: " + newClassSO.name);

                    // Add the asset to the SO_ShipList
                    string assetPath = "Assets/_SO_Assets/ships/" + name + ".asset"; //TODO
                    //SO_ShipList SO_Classes = AssetDatabase.
                }
                else
                {
                    Debug.LogError("Failed to create class asset or obtain reference");
                }

                // Create a VesselSO asset
                newTrainingVesselSO_1 = ScriptableObjectEditor.CreateVesselScriptableObject(name, elements[0]);  //  TODO Create loops here

                if (newTrainingVesselSO_1 != null)
                {
                    Debug.Log("Asset created and reference obtained: " + newTrainingVesselSO_1.name);

                }
                else
                {
                    Debug.LogError("Failed to create Vessel asset or obtain reference");
                }
            }
        }
    }

    public class ScriptableObjectEditor
    {
        
        public static SO_Ship CreateClassScriptableObject(string name)
        {
            // Create an instance of the ScriptableObject
            SO_Ship ClassSO = SO_Ship.CreateInstance<SO_Ship>();

            // Set the value of the ScriptableObject
            ClassSO.name = name;                                            //TODO add values?

            // Save the asset to the "Assets" directory
            string assetPath = "Assets/_SO_Assets/_TEMP/" + name + ".asset"; //TODO
            AssetDatabase.CreateAsset(ClassSO, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Return a reference to the created asset
            return AssetDatabase.LoadAssetAtPath<SO_Ship>(assetPath);
        }

        public static SO_Vessel CreateVesselScriptableObject(string name,string element)
        {
            // Create an instance of the ScriptableObject
            SO_Vessel VesselSO = SO_Vessel.CreateInstance<SO_Vessel>();

            // Set the value of the ScriptableObject
            string assetName = element + name;
            VesselSO.name = assetName;                                            //TODO add values?  use List elements and not element

            // Save the asset to the "Assets" directory
            string assetPath = "Assets/_SO_Assets/_TEMP/" + element + name + ".asset"; //TODO
            AssetDatabase.CreateAsset(VesselSO, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Return a reference to the created asset
            return AssetDatabase.LoadAssetAtPath<SO_Vessel>(assetPath);
        }
    }
}
