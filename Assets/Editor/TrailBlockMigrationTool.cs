using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;

namespace CosmicShore.Core.Editor
{
    public class TrailBlockMigrationTool : EditorWindow
    {
        [Serializable]
        private class BlockConfiguration
        {
            // Core TrailBlock data
            public TrailBlockProperties properties;
            public Vector3 minScale;
            public Vector3 maxScale;
            public Vector3 growthVector;
            public float growthRate;
            public float waitTime;
            public Teams team;
            public bool isTransparent;
            public GameObject particleEffect;
            public GameObject fossilBlock;
            public Trail trail;
            public string ownerId;
            public Player player;

            // State flags
            public bool destroyed;
            public bool devastated;
            public string id;
            public int index;
            public bool warp;
            public bool isSmallest;
            public bool isLargest;

            // Current material states
            public Material activeOpaqueMaterial;
            public Material activeTransparentMaterial;
        }

        private Dictionary<string, BlockConfiguration> configurations = new Dictionary<string, BlockConfiguration>();
        private List<TrailBlock> foundBlocks = new List<TrailBlock>();
        private bool readyToMigrate = false;

        [MenuItem("CosmicShore/Optimized Trail Block Migration")]
        public static void ShowWindow()
        {
            GetWindow<TrailBlockMigrationTool>("Trail Block Migration").Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("TrailBlock Migration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("1. Capture Current Configuration"))
            {
                CaptureConfigurations();
            }

            if (configurations.Count > 0)
            {
                EditorGUILayout.LabelField($"Captured {configurations.Count} TrailBlock configurations");

                if (GUILayout.Button("2. Add New Components"))
                {
                    AddNewComponents();
                }

                if (readyToMigrate && GUILayout.Button("3. Migrate and Switch to New System"))
                {
                    MigrateToNewSystem();
                }
            }
        }

        private void CaptureConfigurations()
        {
            configurations.Clear();
            foundBlocks.Clear();

            // Find all TrailBlock prefabs
            var guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var block = prefab.GetComponent<TrailBlock>();
                if (block != null)
                {
                    CaptureBlockConfiguration(block, path);
                    foundBlocks.Add(block);
                }
            }

            // Find scene instances
            var sceneBlocks = FindObjectsOfType<TrailBlock>();
            foreach (var block in sceneBlocks)
            {
                if (!foundBlocks.Contains(block))
                {
                    CaptureBlockConfiguration(block, block.gameObject.name);
                    foundBlocks.Add(block);
                }
            }

            EditorUtility.DisplayDialog("Configuration Captured",
                $"Captured {configurations.Count} TrailBlock configurations", "OK");
        }

        private void CaptureBlockConfiguration(TrailBlock block, string identifier)
        {
            var config = new BlockConfiguration
            {
                properties = block.TrailBlockProperties,
                minScale = block.GetType().GetField("minScale", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(block) as Vector3? ?? Vector3.one * 0.5f,
                maxScale = block.MaxScale,
                growthVector = block.GrowthVector,
                growthRate = block.growthRate,
                waitTime = block.waitTime,
                team = block.Team,
                isTransparent = block.TrailBlockProperties?.IsTransparent ?? false,
                particleEffect = block.ParticleEffect,
                fossilBlock = block.GetType().GetField("FossilBlock")?.GetValue(block) as GameObject,
                trail = block.Trail,
                ownerId = block.ownerID,
                player = block.Player,
                destroyed = block.destroyed,
                devastated = block.devastated,
                id = block.ownerID,
                index = block.TrailBlockProperties.Index,
                isSmallest = block.IsSmallest,
                isLargest = block.IsLargest
            };

            // Capture current material states using reflection if needed
            var matFields = block.GetType().GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            config.activeOpaqueMaterial = matFields.FirstOrDefault(f => f.Name == "ActiveOpaqueMaterial")?.GetValue(block) as Material;
            config.activeTransparentMaterial = matFields.FirstOrDefault(f => f.Name == "ActiveTransparentMaterial")?.GetValue(block) as Material;

            configurations[identifier] = config;
        }

        private void AddNewComponents()
        {
            // Create managers if they don't exist
            var managers = FindObjectOfType<MaterialStateManager>();
            if (managers == null)
            {
                var managersGO = new GameObject("TrailBlockManagers");
                managersGO.AddComponent<MaterialStateManager>();
                managersGO.AddComponent<BlockScaleManager>();
            }

            foreach (var block in foundBlocks)
            {
                var go = block.gameObject;
                EnsureComponent<MaterialPropertyAnimator>(go);
                EnsureComponent<BlockScaleAnimator>(go);
                EnsureComponent<BlockTeamManager>(go);
                EnsureComponent<BlockStateManager>(go);
            }

            readyToMigrate = true;
            EditorUtility.DisplayDialog("Components Added",
                "Added new components to all TrailBlocks", "OK");
        }

        private void MigrateToNewSystem()
        {
            try
            {
                // Replace TrailBlock script with new version
                foreach (var block in foundBlocks)
                {
                    string identifier = AssetDatabase.GetAssetPath(block);
                    if (string.IsNullOrEmpty(identifier)) identifier = block.gameObject.name;

                    if (configurations.TryGetValue(identifier, out var config))
                    {
                        TransferConfiguration(block.gameObject, config);
                    }
                }

                EditorUtility.DisplayDialog("Migration Complete",
                    "Successfully migrated all TrailBlocks to new system", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"Migration failed: {e.Message}\n{e.StackTrace}");
                EditorUtility.DisplayDialog("Migration Failed",
                    $"Error during migration: {e.Message}", "OK");
            }
        }

        private void TransferConfiguration(GameObject go, BlockConfiguration config)
        {
            // First, transfer the base TrailBlock properties
            var trailBlock = go.GetComponent<TrailBlock>();
            if (trailBlock != null)
            {
                var serializedBlock = new SerializedObject(trailBlock);
                TransferSerializedProperties(serializedBlock, config);
                serializedBlock.ApplyModifiedProperties();
            }

            // Give Unity a frame to process the changes
            EditorApplication.delayCall += () =>
            {
                try
                {
                    var scaleAnimator = go.GetComponent<BlockScaleAnimator>();
                    if (scaleAnimator != null)
                    {
                        var serializedScale = new SerializedObject(scaleAnimator);
                        serializedScale.FindProperty("minScale").vector3Value = config.minScale;
                        serializedScale.FindProperty("maxScale").vector3Value = config.maxScale;
                        serializedScale.ApplyModifiedProperties();
                    }

                    // Set up initial team state first
                    var teamManager = go.GetComponent<BlockTeamManager>();
                    if (teamManager != null && config.team != Teams.Unassigned)
                    {
                        teamManager.SetInitialTeam(config.team);
                    }

                    // Then handle material state
                    var materialAnimator = go.GetComponent<MaterialPropertyAnimator>();
                    if (materialAnimator != null)
                    {
                        // Initial material setup will be handled by TeamManager
                    }

                    // Finally, set up the block state
                    var stateManager = go.GetComponent<BlockStateManager>();
                    if (stateManager != null && config.properties != null)
                    {
                        if (config.properties.IsSuperShielded)
                        {
                            EditorApplication.delayCall += () => stateManager.ActivateSuperShield();
                        }
                        else if (config.properties.IsShielded)
                        {
                            EditorApplication.delayCall += () => stateManager.ActivateShield();
                        }
                        else if (config.properties.IsDangerous)
                        {
                            EditorApplication.delayCall += () => stateManager.MakeDangerous();
                        }
                    }

                    // Mark the object as dirty to ensure Unity saves the changes
                    EditorUtility.SetDirty(go);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error during delayed configuration transfer: {e.Message}\n{e.StackTrace}");
                }
            };

            EditorUtility.SetDirty(go);
        }

        private void TransferSerializedProperties(SerializedObject obj, BlockConfiguration config)
        {
            var trailBlock = obj.targetObject as TrailBlock;
            if (trailBlock == null) return;

            // Handle custom class references directly rather than through serialization
            if (config.properties != null)
            {
                // Use reflection to set TrailBlockProperties since it's a custom class
                var propField = trailBlock.GetType().GetField("TrailBlockProperties", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (propField != null)
                {
                    propField.SetValue(trailBlock, config.properties);
                }
            }

            if (config.trail != null)
            {
                // Use reflection to set Trail since it's a custom class
                var trailField = trailBlock.GetType().GetField("Trail", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (trailField != null)
                {
                    trailField.SetValue(trailBlock, config.trail);
                }
            }

            // Transfer all other serialized properties
            var iterator = obj.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;
                switch (iterator.name)
                {
                    case "m_Script":
                        enterChildren = false;
                        break;

                    // Skip these as we handled them above
                    case "TrailBlockProperties":
                    case "Trail":
                        break;

                    // TrailBlock Properties Header
                    case "FossilBlock":
                        iterator.objectReferenceValue = config.fossilBlock;
                        break;
                    case "ParticleEffect":
                        iterator.objectReferenceValue = config.particleEffect;
                        break;

                    // Trail Block Growth Header
                    case "GrowthVector":
                        iterator.vector3Value = config.growthVector;
                        break;
                    case "growthRate":
                        iterator.floatValue = config.growthRate;
                        break;
                    case "waitTime":
                        iterator.floatValue = config.waitTime;
                        break;

                    // Trail Block Status Header
                    case "destroyed":
                        iterator.boolValue = config.destroyed;
                        break;
                    case "devastated":
                        iterator.boolValue = config.devastated;
                        break;
                    case "ID":
                        iterator.stringValue = config.id;
                        break;
                    case "Index":
                        iterator.intValue = config.index;
                        break;
                    case "warp":
                        iterator.boolValue = config.warp;
                        break;
                    case "IsSmallest":
                        iterator.boolValue = config.isSmallest;
                        break;
                    case "IsLargest":
                        iterator.boolValue = config.isLargest;
                        break;

                    // Team Ownership Header
                    case "ownerId":
                        iterator.stringValue = config.ownerId;
                        break;
                    case "Player":
                        iterator.objectReferenceValue = config.player;
                        break;
                }
            }

            // Apply the changes
            obj.ApplyModifiedProperties();

            // Handle Team last to ensure proper initialization
            if (config.team != Teams.Unassigned)
            {
                trailBlock.Team = config.team;
            }
        }

        private T EnsureComponent<T>(GameObject obj) where T : Component
        {
            var component = obj.GetComponent<T>();
            return component == null ? obj.AddComponent<T>() : component;
        }
    }
}