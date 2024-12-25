using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;

namespace CosmicShore.Core.Editor
{
    public class TrailBlockMigrationTool : EditorWindow
    {
        [Serializable]
        private class TrailBlockSnapshot
        {
            public string prefabPath;
            public Vector3 minScale;
            public Vector3 maxScale;
            public Vector3 growthVector;
            public float growthRate;
            public float waitTime;
            public Teams team;
            public string materialState; // "Normal", "Shielded", "SuperShielded", "Dangerous"
            public bool isTransparent;
            public Dictionary<string, object> serializedFieldValues = new Dictionary<string, object>();
        }

        private List<TrailBlockSnapshot> snapshots = new List<TrailBlockSnapshot>();
        private string backupFolder = "Assets/TrailBlockBackups";
        private bool analysisComplete = false;
        private Vector2 scrollPosition;
        private string lastError = "";

        [MenuItem("CosmicShore/Trail Block Migration")]
        public static void ShowWindow()
        {
            GetWindow<TrailBlockMigrationTool>("Trail Block Migration").Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Trail Block Migration - Step 1", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (!string.IsNullOrEmpty(lastError))
            {
                EditorGUILayout.HelpBox(lastError, MessageType.Error);
                EditorGUILayout.Space();
            }

            if (GUILayout.Button("1. Analyze Current TrailBlocks"))
            {
                AnalyzeTrailBlocks();
            }

            if (analysisComplete)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Found {snapshots.Count} TrailBlock instances", EditorStyles.boldLabel);

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                foreach (var snapshot in snapshots)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"Prefab: {snapshot.prefabPath}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Team: {snapshot.team}");
                    EditorGUILayout.LabelField($"Material State: {snapshot.materialState}");
                    EditorGUILayout.LabelField($"Growth Rate: {snapshot.growthRate}");
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space();
                }
                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space();
                if (GUILayout.Button("2. Create Backups"))
                {
                    CreateBackups();
                }

                EditorGUILayout.Space();
                if (GUILayout.Button("3. Add New Components"))
                {
                    PrepareForMigration();
                }
            }
        }

        private void AnalyzeTrailBlocks()
        {
            try
            {
                snapshots.Clear();
                lastError = "";

                // Find all TrailBlock prefabs
                var guids = AssetDatabase.FindAssets("t:Prefab");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var trailBlock = prefab.GetComponent<TrailBlock>();

                    if (trailBlock != null)
                    {
                        var snapshot = CreateSnapshot(trailBlock, path);
                        snapshots.Add(snapshot);
                    }
                }

                // Find all scene instances
                var sceneBlocks = FindObjectsOfType<TrailBlock>();
                foreach (var block in sceneBlocks)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(block))
                    {
                        var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(block);
                        if (!snapshots.Any(s => s.prefabPath == prefabPath))
                        {
                            var snapshot = CreateSnapshot(block, prefabPath);
                            snapshots.Add(snapshot);
                        }
                    }
                }

                analysisComplete = true;
            }
            catch (Exception e)
            {
                lastError = $"Error during analysis: {e.Message}";
                Debug.LogException(e);
            }
        }

        private TrailBlockSnapshot CreateSnapshot(TrailBlock block, string path)
        {
            var snapshot = new TrailBlockSnapshot
            {
                prefabPath = path,
                minScale = block.GetType().GetField("minScale")?.GetValue(block) as Vector3? ?? Vector3.one * 0.5f,
                maxScale = block.MaxScale,
                growthVector = block.GrowthVector,
                growthRate = block.growthRate,
                waitTime = block.waitTime,
                team = block.Team,
                isTransparent = block.TrailBlockProperties?.IsTransparent ?? false,
            };

            // Determine material state
            if (block.TrailBlockProperties?.IsSuperShielded ?? false) snapshot.materialState = "SuperShielded";
            else if (block.TrailBlockProperties?.IsShielded ?? false) snapshot.materialState = "Shielded";
            else if (block.TrailBlockProperties?.IsDangerous ?? false) snapshot.materialState = "Dangerous";
            else snapshot.materialState = "Normal";

            // Capture all serialized field values
            var serializedObject = new SerializedObject(block);
            var iterator = serializedObject.GetIterator();
            while (iterator.NextVisible(true))
            {
                if (iterator.name != "m_Script")
                {
                    snapshot.serializedFieldValues[iterator.name] = GetSerializedValue(iterator);
                }
            }

            return snapshot;
        }

        private void CreateBackups()
        {
            try
            {
                lastError = "";

                // Create backup folder if it doesn't exist
                if (!AssetDatabase.IsValidFolder(backupFolder))
                {
                    var parentFolder = Path.GetDirectoryName(backupFolder);
                    var folderName = Path.GetFileName(backupFolder);
                    AssetDatabase.CreateFolder(parentFolder, folderName);
                }

                // Create timestamped subfolder
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var backupPath = $"{backupFolder}/{timestamp}";
                AssetDatabase.CreateFolder(backupFolder, timestamp);

                // Copy prefabs to backup location
                foreach (var snapshot in snapshots)
                {
                    var fileName = Path.GetFileName(snapshot.prefabPath);
                    var destPath = $"{backupPath}/{fileName}";
                    AssetDatabase.CopyAsset(snapshot.prefabPath, destPath);

                    // Save snapshot data as JSON
                    var jsonPath = $"{backupPath}/{Path.GetFileNameWithoutExtension(fileName)}_snapshot.json";
                    File.WriteAllText(jsonPath, JsonUtility.ToJson(snapshot, true));
                }

                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Backup Complete",
                    $"Created backups in {backupPath}\nTotal prefabs backed up: {snapshots.Count}", "OK");
            }
            catch (Exception e)
            {
                lastError = $"Error during backup: {e.Message}";
                Debug.LogException(e);
            }
        }

        private void PrepareForMigration()
        {
            try
            {
                lastError = "";

                // Create managers GameObject if it doesn't exist
                var managers = FindObjectOfType<MaterialStateManager>();
                if (managers == null)
                {
                    var managersGO = new GameObject("TrailBlockManagers");
                    managersGO.AddComponent<MaterialStateManager>();
                    managersGO.AddComponent<BlockScaleManager>();
                }

                // Add new components to all TrailBlock prefabs
                foreach (var snapshot in snapshots)
                {
                    var prefab = PrefabUtility.LoadPrefabContents(snapshot.prefabPath);
                    var trailBlock = prefab.GetComponent<TrailBlock>();

                    // Add new components if they don't exist
                    EnsureComponent<MaterialPropertyAnimator>(prefab);
                    EnsureComponent<BlockScaleAnimator>(prefab);
                    EnsureComponent<BlockTeamManager>(prefab);
                    EnsureComponent<BlockStateManager>(prefab);

                    PrefabUtility.SaveAsPrefabAsset(prefab, snapshot.prefabPath);
                    PrefabUtility.UnloadPrefabContents(prefab);
                }

                EditorUtility.DisplayDialog("Preparation Complete",
                    "Added new components to all TrailBlock prefabs.\nReady for next migration step.", "OK");
            }
            catch (Exception e)
            {
                lastError = $"Error during preparation: {e.Message}";
                Debug.LogException(e);
            }
        }

        private T EnsureComponent<T>(GameObject obj) where T : Component
        {
            var component = obj.GetComponent<T>();
            if (component == null)
            {
                component = obj.AddComponent<T>();
            }
            return component;
        }

        private object GetSerializedValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue;
                case SerializedPropertyType.Boolean: return prop.boolValue;
                case SerializedPropertyType.Float: return prop.floatValue;
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Vector2: return prop.vector2Value;
                case SerializedPropertyType.Vector3: return prop.vector3Value;
                case SerializedPropertyType.Vector4: return prop.vector4Value;
                case SerializedPropertyType.Quaternion: return prop.quaternionValue;
                case SerializedPropertyType.Color: return prop.colorValue;
                case SerializedPropertyType.ObjectReference: return prop.objectReferenceValue;
                default: return null;
            }
        }
    }
}
