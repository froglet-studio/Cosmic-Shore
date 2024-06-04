using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using CosmicShore.Core;

/*
    get class name
    Create "Class"ClassSO
    TODO Create Prefab or GameObject and assign "Class"ClassSO to it
    TODO Add "Class"ClassSO to Hangar in Main_menu scene
    TODO Add to SO_Class and SO_AllShips  DO we need both? and should we rename to SO_AllClasses

    For all 4 Elements create "ElementClass"TrainingGameSO
    TODO For all 4 Elements append AllGamesSO with "ElementClass"TrainingGameSO

    TODO For all 4 Elements create "ElementClass"TrainingScene (formally MiniGames) 
    TODO For all 4 Elements create "ElementClass"TrainingVesselSO with 1 Element maxed out

    For all 4 Elements create "ElementClass"UpgradeVesselSO
    
     Create one "Class"FreestyleVesselSO
    TODO Add to Freestyle Game SO
 
 */

namespace CosmicShore
{
    public class CreateNewClass : EditorWindow
    {
        string newClassName = string.Empty;
        string newClassSOName = string.Empty;

        [SerializeField] GameObject classPrefab = null;

        List<string> elements = new List<string>() { "Time", "Mass", "Charge", "Space" };


        /*string classSavePath = "Assets/_SO_Assets/_TEMP/";  //TODO /Class/_New/" or finalized folder
        string trainingVesselSavePath = "Assets/_SO_Assets/_TEMP/";  //TODO /Vessel/Training/_New/" or finalized folder
        string UpgradeVesselSavePath = "Assets/_SO_Assets/_TEMP/";  //TODO /Vessel/Upgrade/_New/" or finalized folder
        string FreestyleVesselSavePath = "Assets/_SO_Assets/_TEMP/";  //TODO /Vessel/Freestyle/_New/" or finalized folder*/

        #region Scriptable Objects
        // Class SO
        SO_Ship newClassSO;
        // Training Vessel SO
        SO_Captain newTrainingVesselSO_1;
        SO_Captain newTrainingVesselSO_2;
        SO_Captain newTrainingVesselSO_3;
        SO_Captain newTrainingVesselSO_4;
        // Upgrade Vessel SO
        SO_Captain newUpgradeVesselSO_1;
        SO_Captain newUpgradeVesselSO_2;
        SO_Captain newUpgradeVesselSO_3;
        SO_Captain newUpgradeVesselSO_4;
        // Freestyle Vessel SO
        SO_Captain newFreestyleVesselSO_1;
        //Lists of VesselSO's
        List<SO_Captain> TrainingVessels = new List<SO_Captain>();
        List<SO_Captain> UpgradeVessels = new List<SO_Captain>();
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
            
            // Create an empty instance of the Element_Class Training ScriptableObject
            newTrainingVesselSO_1 = SO_Captain.CreateInstance<SO_Captain>();
            newTrainingVesselSO_2 = SO_Captain.CreateInstance<SO_Captain>();
            newTrainingVesselSO_3 = SO_Captain.CreateInstance<SO_Captain>();
            newTrainingVesselSO_4 = SO_Captain.CreateInstance<SO_Captain>();
            TrainingVessels.Add(newTrainingVesselSO_1);TrainingVessels.Add(newTrainingVesselSO_2); TrainingVessels.Add(newTrainingVesselSO_3); TrainingVessels.Add(newTrainingVesselSO_4);

            
            // Create an empty instance of the Element_Class Upgrade ScriptableObject
            newUpgradeVesselSO_1 = SO_Captain.CreateInstance<SO_Captain>();
            newUpgradeVesselSO_2 = SO_Captain.CreateInstance<SO_Captain>();
            newUpgradeVesselSO_3 = SO_Captain.CreateInstance<SO_Captain>();
            newUpgradeVesselSO_4 = SO_Captain.CreateInstance<SO_Captain>();
            UpgradeVessels.Add(newUpgradeVesselSO_1); UpgradeVessels.Add(newUpgradeVesselSO_2); UpgradeVessels.Add(newUpgradeVesselSO_3); UpgradeVessels.Add(newUpgradeVesselSO_4);

            // Create an empty instance of the Element_Class Freestyle ScriptableObject
            newFreestyleVesselSO_1 = SO_Captain.CreateInstance<SO_Captain>();
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
            classPrefab = EditorGUILayout.ObjectField("Class Prefab", classPrefab, typeof(GameObject), false) as GameObject;

            if (GUILayout.Button("Create Class"))
            {
                newClassSOName = newClassName + "ClassSO";
                // Create a ClassSO asset
                newClassSO = ScriptableObjectEditor.CreateClassScriptableObject(newClassSOName);

                if (newClassSO != null)
                {
                    Debug.Log("Asset created and reference obtained: " + newClassSO.name);

                    // TODO Add the asset to the SO_ShipList
                    string assetPath = "Assets/_SO_Assets/ships/" + name + ".asset";                    //TODO
                                                                                                        //SO_ShipList SO_Classes = AssetDatabase.
                    if(classPrefab != null)
                    {
                        classPrefab.name = newClassSOName;
                        Debug.Log("Asset created and reference obtained: " + classPrefab.name);
                    }
                    else
                    {
                        Debug.LogError("Failed to create class Prefab");
                    }
                      
                }
                else
                {
                    Debug.LogError("Failed to create class asset or obtain reference");
                }

                // Create a TrainingVesselSO assets
                for (int idx = 0; idx < elements.Count; idx++)
                {
                    string newTrainingVesselName = elements[idx].ToString() + newClassName + "TrainingSO";
                    TrainingVessels[idx] = ScriptableObjectEditor.CreateVesselScriptableObject(newTrainingVesselName, elements[idx], true, newClassSOName); //Send string training also

                    if (TrainingVessels[idx] != null)
                    {
                        Debug.Log("Asset created and reference obtained: " + TrainingVessels[idx].name);

                    }
                    else
                    {
                        Debug.LogError("Failed to create Vessel asset or obtain reference");
                    }
                }
                // Create a UpgradeVesselSO assets
                for (int idx = 0; idx < elements.Count; idx++)
                {
                    string newUpgradeVesselName = elements[idx].ToString() + newClassName + "UpgradeSO";
                    UpgradeVessels[idx] = ScriptableObjectEditor.CreateVesselScriptableObject(newUpgradeVesselName, elements[idx], false, newClassSOName); //Send string Upgrade also

                    if (UpgradeVessels[idx] != null)
                    {
                        Debug.Log("Asset created and reference obtained: " + UpgradeVessels[idx].name);

                    }
                    else
                    {
                        Debug.LogError("Failed to create Vessel asset or obtain reference");
                    }
                }
                // Create a FreestyleVesselSO assets
                string newFreestyleVesselName = newClassName + "FreestyleSO";
                newFreestyleVesselSO_1 = ScriptableObjectEditor.CreateFreestyleVesselScriptableObject(newFreestyleVesselName);

                if (newFreestyleVesselSO_1 != null)
                {
                    Debug.Log("Asset created and reference obtained: " + newFreestyleVesselSO_1.name);

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

        public static SO_Captain CreateVesselScriptableObject(string name,string element, bool training, string classSOName)
        {
            // Create an instance of the ScriptableObject
            SO_Captain VesselSO = SO_Captain.CreateInstance<SO_Captain>();

            // Set the values of the ScriptableObject
            VesselSO.name = name;                                            //Sets File Name
            VesselSO.Name = name;                                            //Sets Display Name

            string classSOAssetPath = "Assets/_SO_Assets/_TEMP/" + classSOName + ".asset"; //TODO
            VesselSO.Ship = AssetDatabase.LoadAssetAtPath<SO_Ship>(classSOAssetPath); //Sets ClassSO

            switch (element)                                                  //Sets Elements
            {
                case "Mass":
                    VesselSO.PrimaryElement = Element.Mass;
                    if (training) { VesselSO.InitialMass = 1; }                            
                    break;

                case "Charge":
                    VesselSO.PrimaryElement = Element.Charge;
                    if (training) { VesselSO.InitialCharge = 1; }                 
                    break;

                case "Space":
                    VesselSO.PrimaryElement = Element.Space;
                    if (training) { VesselSO.InitialSpace = 1; }
                    break;

                case "Time":
                    VesselSO.PrimaryElement = Element.Time;
                    if (training) { VesselSO.InitialTime = 1; }                    
                    break;
                    //default: 
                    //Debug.Log("Element not found while creating VesselSO.");
            }


            // Save the asset to the "Assets" directory
            string assetPath = "Assets/_SO_Assets/_TEMP/" + name + ".asset"; //TODO
            AssetDatabase.CreateAsset(VesselSO, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Return a reference to the created asset
            return AssetDatabase.LoadAssetAtPath<SO_Captain>(assetPath);
        }

        public static SO_Captain CreateFreestyleVesselScriptableObject(string name)
        {
            // Create an instance of the ScriptableObject
            SO_Captain VesselSO = SO_Captain.CreateInstance<SO_Captain>();

            // Set the value of the ScriptableObject
            VesselSO.name = name;                                            //Sets File Name 
            VesselSO.Name = name;                                            //Sets Display Name
            //VesselSO.PrimaryElement =                                      //TODO set all elements to max
            
            VesselSO.InitialMass = 1;
            VesselSO.InitialCharge = 1;
            VesselSO.InitialSpace = 1;
            VesselSO.InitialTime = 1;

            // Save the asset to the "Assets" directory
            string assetPath = "Assets/_SO_Assets/_TEMP/" + name + ".asset"; //TODO
            AssetDatabase.CreateAsset(VesselSO, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Return a reference to the created asset
            return AssetDatabase.LoadAssetAtPath<SO_Captain>(assetPath);
        }       
    }
}
