using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Game.Analytics
{
    [CustomEditor(typeof(LeaderboardConfigSO))]
    public class LeaderboardConfigSOEditor : Editor
    {
        private SerializedProperty leaderboardMappingsProperty;
        private SerializedProperty activeGameModesProperty;
        private Dictionary<GameModes, bool> foldouts = new Dictionary<GameModes, bool>();
        private bool showOnlyActive = true;
        private bool expandAll = false;
        private Vector2 scrollPosition;

        // Color scheme
        private static readonly Color headerColor = new Color(0.3f, 0.5f, 0.7f, 0.3f);
        private static readonly Color activeGameModeColor = new Color(0.4f, 0.7f, 0.4f, 0.15f);
        private static readonly Color inactiveGameModeColor = new Color(0.5f, 0.5f, 0.5f, 0.1f);
        private static readonly Color missingMappingColor = new Color(0.8f, 0.6f, 0.2f, 0.2f);

        private void OnEnable()
        {
            leaderboardMappingsProperty = serializedObject.FindProperty("leaderboardMappings");
            activeGameModesProperty = serializedObject.FindProperty("activeGameModes");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Header section
            DrawHeader();

            EditorGUILayout.Space(5);

            // Control panel
            DrawControlPanel();

            EditorGUILayout.Space(5);

            // Get all GameModes enum values
            var allGameModes = System.Enum.GetValues(typeof(GameModes)).Cast<GameModes>().ToList();
            var activeGameModes = GetActiveGameModes();

            // Filter game modes based on toggle
            var displayGameModes = showOnlyActive 
                ? allGameModes.Where(gm => activeGameModes.Contains(gm)).ToList()
                : allGameModes;

            // Group existing mappings by GameMode
            var groupedMappings = GetGroupedMappings();

            // Scrollable area for game modes
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var gameMode in displayGameModes)
            {
                bool isActive = activeGameModes.Contains(gameMode);
                DrawGameModeSection(gameMode, groupedMappings, isActive);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // Bottom action buttons
            DrawActionButtons();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = headerColor;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = originalColor;

            EditorGUILayout.LabelField("Leaderboard Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Configure leaderboard IDs for each game mode and intensity combination.", MessageType.Info);

            EditorGUILayout.EndVertical();
        }

        private void DrawControlPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Filters & Controls", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();

            // Filter toggle
            EditorGUI.BeginChangeCheck();
            showOnlyActive = EditorGUILayout.ToggleLeft("Show Only Active Game Modes", showOnlyActive, GUILayout.Width(200));
            if (EditorGUI.EndChangeCheck())
            {
                // Force repaint when filter changes
                Repaint();
            }

            GUILayout.FlexibleSpace();

            // Expand/Collapse all
            if (GUILayout.Button(expandAll ? "Collapse All" : "Expand All", GUILayout.Width(100)))
            {
                expandAll = !expandAll;
                var activeGameModes = GetActiveGameModes();
                var displayModes = showOnlyActive 
                    ? System.Enum.GetValues(typeof(GameModes)).Cast<GameModes>().Where(gm => activeGameModes.Contains(gm))
                    : System.Enum.GetValues(typeof(GameModes)).Cast<GameModes>();
                
                foreach (var gameMode in displayModes)
                {
                    foldouts[gameMode] = expandAll;
                }
            }

            // Edit Active Game Modes button
            if (GUILayout.Button("Edit Active Modes", GUILayout.Width(120)))
            {
                ShowActiveGameModesWindow();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.5f, 0.8f, 0.5f);
            if (GUILayout.Button("Validate All Mappings", GUILayout.Height(30)))
            {
                ValidateAllMappings();
            }

            GUI.backgroundColor = new Color(0.8f, 0.7f, 0.5f);
            if (GUILayout.Button("Fill All Active Modes", GUILayout.Height(30)))
            {
                FillAllActiveModes();
            }

            GUI.backgroundColor = originalColor;
            EditorGUILayout.EndHorizontal();
        }

        private Dictionary<GameModes, Dictionary<int, int>> GetGroupedMappings()
        {
            var grouped = new Dictionary<GameModes, Dictionary<int, int>>();

            for (int i = 0; i < leaderboardMappingsProperty.arraySize; i++)
            {
                var element = leaderboardMappingsProperty.GetArrayElementAtIndex(i);
                var gameMode = (GameModes)element.FindPropertyRelative("GameMode").enumValueIndex;
                var intensity = element.FindPropertyRelative("Intensity").intValue;

                if (!grouped.ContainsKey(gameMode))
                    grouped[gameMode] = new Dictionary<int, int>();

                grouped[gameMode][intensity] = i;
            }

            return grouped;
        }

        private void DrawGameModeSection(GameModes gameMode, Dictionary<GameModes, Dictionary<int, int>> groupedMappings, bool isActive)
        {
            // Initialize foldout state if needed
            if (!foldouts.ContainsKey(gameMode))
                foldouts[gameMode] = false;

            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = isActive ? activeGameModeColor : inactiveGameModeColor;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = originalColor;

            // Foldout header with active indicator
            var rect = EditorGUILayout.GetControlRect();
            
            // Active indicator dot
            if (isActive)
            {
                var dotRect = new Rect(rect.x + 2, rect.y + 4, 8, 8);
                EditorGUI.DrawRect(dotRect, new Color(0.3f, 0.8f, 0.3f));
            }

            // Game mode name with foldout
            string displayName = isActive ? $"{gameMode}" : $"{gameMode} (Inactive)";
            foldouts[gameMode] = EditorGUI.Foldout(
                new Rect(rect.x + (isActive ? 15 : 0), rect.y, rect.width - 80, rect.height),
                foldouts[gameMode],
                displayName,
                true,
                EditorStyles.foldoutHeader
            );

            // Fill All button
            GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            if (GUI.Button(new Rect(rect.width - 60, rect.y, 70, rect.height), "Fill All"))
            {
                EnsureAllIntensities(gameMode, groupedMappings);
            }
            GUI.backgroundColor = originalColor;

            if (foldouts[gameMode])
            {
                EditorGUI.indentLevel++;

                // Draw intensity fields for 1-4
                for (int intensity = 1; intensity <= 4; intensity++)
                {
                    DrawIntensityField(gameMode, intensity, groupedMappings);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void DrawIntensityField(GameModes gameMode, int intensity, Dictionary<GameModes, Dictionary<int, int>> groupedMappings)
        {
            var originalColor = GUI.backgroundColor;

            // Check if mapping exists
            int mappingIndex = -1;
            if (groupedMappings.ContainsKey(gameMode) && groupedMappings[gameMode].ContainsKey(intensity))
            {
                mappingIndex = groupedMappings[gameMode][intensity];
            }

            string currentValue = "";
            bool hasMissingMapping = false;

            if (mappingIndex >= 0 && mappingIndex < leaderboardMappingsProperty.arraySize)
            {
                var element = leaderboardMappingsProperty.GetArrayElementAtIndex(mappingIndex);
                currentValue = element.FindPropertyRelative("LeaderboardId").stringValue;
                hasMissingMapping = string.IsNullOrEmpty(currentValue);
            }

            // Highlight missing mappings
            if (hasMissingMapping)
            {
                GUI.backgroundColor = missingMappingColor;
            }

            EditorGUILayout.BeginHorizontal();

            // Draw the field
            EditorGUILayout.LabelField($"Intensity {intensity}", GUILayout.Width(80));
            
            EditorGUI.BeginChangeCheck();
            string newValue = EditorGUILayout.TextField(currentValue);
            
            if (EditorGUI.EndChangeCheck())
            {
                if (mappingIndex >= 0 && mappingIndex < leaderboardMappingsProperty.arraySize)
                {
                    // Update existing mapping
                    var element = leaderboardMappingsProperty.GetArrayElementAtIndex(mappingIndex);
                    element.FindPropertyRelative("LeaderboardId").stringValue = newValue;
                }
                else if (!string.IsNullOrEmpty(newValue))
                {
                    // Create new mapping
                    CreateMapping(gameMode, intensity, newValue);
                }
            }

            // Delete button if mapping exists
            if (mappingIndex >= 0 && mappingIndex < leaderboardMappingsProperty.arraySize)
            {
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    DeleteMapping(mappingIndex, gameMode, intensity);
                }
            }

            GUI.backgroundColor = originalColor;
            EditorGUILayout.EndHorizontal();
        }

        private void CreateMapping(GameModes gameMode, int intensity, string leaderboardId)
        {
            int newIndex = leaderboardMappingsProperty.arraySize;
            leaderboardMappingsProperty.InsertArrayElementAtIndex(newIndex);
            
            var element = leaderboardMappingsProperty.GetArrayElementAtIndex(newIndex);
            element.FindPropertyRelative("GameMode").enumValueIndex = (int)gameMode;
            element.FindPropertyRelative("Intensity").intValue = intensity;
            element.FindPropertyRelative("LeaderboardId").stringValue = leaderboardId;
        }

        private void DeleteMapping(int index, GameModes gameMode, int intensity)
        {
            if (index < 0 || index >= leaderboardMappingsProperty.arraySize)
            {
                Debug.LogError($"Invalid index {index} when trying to delete mapping");
                return;
            }

            if (EditorUtility.DisplayDialog("Delete Mapping", 
                $"Delete leaderboard mapping for {gameMode} - Intensity {intensity}?", 
                "Delete", "Cancel"))
            {
                leaderboardMappingsProperty.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                
                // Force a repaint to refresh the groupedMappings
                Repaint();
            }
        }

        private void EnsureAllIntensities(GameModes gameMode, Dictionary<GameModes, Dictionary<int, int>> groupedMappings)
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                if (!groupedMappings.ContainsKey(gameMode) || !groupedMappings[gameMode].ContainsKey(intensity))
                {
                    CreateMapping(gameMode, intensity, $"{gameMode}_Intensity{intensity}");
                }
            }
            serializedObject.ApplyModifiedProperties();
            Repaint();
        }

        private HashSet<GameModes> GetActiveGameModes()
        {
            var activeSet = new HashSet<GameModes>();
            
            if (activeGameModesProperty != null && activeGameModesProperty.isArray)
            {
                for (int i = 0; i < activeGameModesProperty.arraySize; i++)
                {
                    var element = activeGameModesProperty.GetArrayElementAtIndex(i);
                    activeSet.Add((GameModes)element.enumValueIndex);
                }
            }
            
            // If no active modes set, default to all modes
            if (activeSet.Count == 0)
            {
                activeSet = new HashSet<GameModes>(System.Enum.GetValues(typeof(GameModes)).Cast<GameModes>());
            }
            
            return activeSet;
        }

        private void FillAllActiveModes()
        {
            var activeGameModes = GetActiveGameModes();
            var groupedMappings = GetGroupedMappings();
            
            int addedCount = 0;
            foreach (var gameMode in activeGameModes)
            {
                for (int intensity = 1; intensity <= 4; intensity++)
                {
                    if (!groupedMappings.ContainsKey(gameMode) || !groupedMappings[gameMode].ContainsKey(intensity))
                    {
                        CreateMapping(gameMode, intensity, $"{gameMode}_Intensity{intensity}");
                        addedCount++;
                    }
                }
            }
            
            serializedObject.ApplyModifiedProperties();
            EditorUtility.DisplayDialog("Fill Complete", 
                $"Added {addedCount} missing mappings for active game modes.", "OK");
            Repaint();
        }

        private void ShowActiveGameModesWindow()
        {
            ActiveGameModesWindow.ShowWindow(target as LeaderboardConfigSO);
        }

        private void ValidateAllMappings()
        {
            var duplicates = new List<string>();
            var missing = new List<string>();
            var allGameModes = System.Enum.GetValues(typeof(GameModes)).Cast<GameModes>();

            // Check for duplicates
            var leaderboardIds = new HashSet<string>();
            for (int i = 0; i < leaderboardMappingsProperty.arraySize; i++)
            {
                var element = leaderboardMappingsProperty.GetArrayElementAtIndex(i);
                var id = element.FindPropertyRelative("LeaderboardId").stringValue;
                
                if (!string.IsNullOrEmpty(id))
                {
                    if (leaderboardIds.Contains(id))
                        duplicates.Add(id);
                    else
                        leaderboardIds.Add(id);
                }
            }

            // Check for missing combinations
            var grouped = GetGroupedMappings();
            foreach (var gameMode in allGameModes)
            {
                for (int intensity = 1; intensity <= 4; intensity++)
                {
                    if (!grouped.ContainsKey(gameMode) || !grouped[gameMode].ContainsKey(intensity))
                    {
                        missing.Add($"{gameMode} - Intensity {intensity}");
                    }
                }
            }

            // Display results
            string message = "Validation Results:\n\n";
            
            if (duplicates.Count > 0)
                message += $"⚠️ Duplicate IDs found: {string.Join(", ", duplicates)}\n\n";
            
            if (missing.Count > 0)
                message += $"⚠️ Missing mappings:\n{string.Join("\n", missing)}\n\n";
            
            if (duplicates.Count == 0 && missing.Count == 0)
                message += "✅ All mappings are valid!";

            EditorUtility.DisplayDialog("Validation Results", message, "OK");
        }
    }
}