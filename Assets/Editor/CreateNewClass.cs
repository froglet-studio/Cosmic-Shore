using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using CosmicShore.Models.Enums;

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
        // Captains
        SO_Captain SO_Captain_Mass;
        SO_Captain SO_Captain_Charge;
        SO_Captain SO_Captain_Space;
        SO_Captain SO_Captain_Time;
        // Freestyle Vessel SO
        SO_Captain SO_Captain_Arcade;
        List<SO_Captain> Captains = new List<SO_Captain>();
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
            // Create an empty instance of the Element_Class Freestyle ScriptableObject
            SO_Captain_Arcade = CreateInstance<SO_Captain>();

            // Create an empty instance of the Element_Class Upgrade ScriptableObject
            SO_Captain_Mass = CreateInstance<SO_Captain>();
            SO_Captain_Charge = CreateInstance<SO_Captain>();
            SO_Captain_Space = CreateInstance<SO_Captain>();
            SO_Captain_Time = CreateInstance<SO_Captain>();
            Captains.Add(SO_Captain_Mass); Captains.Add(SO_Captain_Charge); Captains.Add(SO_Captain_Space); Captains.Add(SO_Captain_Time);
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
                // Create a UpgradeVesselSO assets
                for (int idx = 0; idx < elements.Count; idx++)
                {
                    string newUpgradeVesselName = $"SO_Captain_{newClassName}_{elements[idx]}";
                    Captains[idx] = ScriptableObjectEditor.CreateCaptainScriptableObject(newUpgradeVesselName, elements[idx], false, newClassSOName); //Send string Upgrade also

                    if (Captains[idx] != null)
                    {
                        Debug.Log("Asset created and reference obtained: " + Captains[idx].name);

                    }
                    else
                    {
                        Debug.LogError("Failed to create Vessel asset or obtain reference");
                    }
                }
                // Create a FreestyleVesselSO assets
                SO_Captain_Arcade = ScriptableObjectEditor.CreateFreestyleVesselScriptableObject($"SO_Captain_Arcade_{newClassName}");

                if (SO_Captain_Arcade != null)
                {
                    Debug.Log("Asset created and reference obtained: " + SO_Captain_Arcade.name);

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

        public static SO_Captain CreateCaptainScriptableObject(string name, string element, bool training, string classSOName)
        {
            // Create an instance of the ScriptableObject
            SO_Captain VesselSO = SO_Captain.CreateInstance<SO_Captain>();

            // Set the values of the ScriptableObject
            VesselSO.name = name;                                            //Sets File Name
            VesselSO.Name = name;                                            //Sets Display Name

            string classSOAssetPath = "Assets/_SO_Assets/_TEMP/" + classSOName + ".asset"; //TODO
            VesselSO.Ship = AssetDatabase.LoadAssetAtPath<SO_Ship>(classSOAssetPath); //Sets ClassSO
            VesselSO.InitialResourceLevels = new ResourceCollection(0, 0, 0, 0);

            switch (element)                                                  //Sets Elements
            {
                case "Mass":
                    VesselSO.PrimaryElement = Element.Mass;
                    VesselSO.InitialResourceLevels.Mass = .5f;
                    break;

                case "Charge":
                    VesselSO.PrimaryElement = Element.Charge;
                    VesselSO.InitialResourceLevels.Charge = .5f;              
                    break;

                case "Space":
                    VesselSO.PrimaryElement = Element.Space;
                    VesselSO.InitialResourceLevels.Space = .5f;
                    break;

                case "Time":
                    VesselSO.PrimaryElement = Element.Time;
                    VesselSO.InitialResourceLevels.Time = .5f;              
                    break;
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
            VesselSO.InitialResourceLevels = new ResourceCollection(1, 1, 1, 1);

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
