using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Game.Analytics
{
    public class ActiveGameModesWindow : EditorWindow
    {
        private LeaderboardConfigSO config;
        private SerializedObject serializedConfig;
        private SerializedProperty activeGameModesProperty;
        private HashSet<GameModes> selectedModes = new HashSet<GameModes>();
        private Vector2 scrollPosition;
        private string searchFilter = "";

        public static void ShowWindow(LeaderboardConfigSO config)
        {
            var window = GetWindow<ActiveGameModesWindow>("Active Game Modes");
            window.config = config;
            window.minSize = new Vector2(400, 500);
            window.Initialize();
            window.Show();
        }

        private void Initialize()
        {
            serializedConfig = new SerializedObject(config);
            activeGameModesProperty = serializedConfig.FindProperty("activeGameModes");
            
            // Load current active modes
            selectedModes.Clear();
            if (activeGameModesProperty != null && activeGameModesProperty.isArray)
            {
                for (int i = 0; i < activeGameModesProperty.arraySize; i++)
                {
                    var element = activeGameModesProperty.GetArrayElementAtIndex(i);
                    selectedModes.Add((GameModes)element.enumValueIndex);
                }
            }

            // If no modes selected, select all by default
            if (selectedModes.Count == 0)
            {
                selectedModes = new HashSet<GameModes>(
                    System.Enum.GetValues(typeof(GameModes)).Cast<GameModes>()
                );
            }
        }

        private void OnGUI()
        {
            if (config == null)
            {
                EditorGUILayout.HelpBox("Configuration not found. Please close this window.", MessageType.Error);
                return;
            }

            serializedConfig.Update();

            // Header
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Select Active Game Modes", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Choose which game modes are currently active in your game. " +
                "Only active modes will be shown by default in the main editor.", MessageType.Info);
            EditorGUILayout.Space(5);

            // Search bar
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(60));
            searchFilter = EditorGUILayout.TextField(searchFilter);
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                searchFilter = "";
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);

            // Quick action buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All"))
            {
                selectedModes = new HashSet<GameModes>(
                    System.Enum.GetValues(typeof(GameModes)).Cast<GameModes>()
                );
            }
            if (GUILayout.Button("Deselect All"))
            {
                selectedModes.Clear();
            }
            if (GUILayout.Button("Invert Selection"))
            {
                var allModes = System.Enum.GetValues(typeof(GameModes)).Cast<GameModes>();
                var newSelection = new HashSet<GameModes>();
                foreach (var mode in allModes)
                {
                    if (!selectedModes.Contains(mode))
                        newSelection.Add(mode);
                }
                selectedModes = newSelection;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Game modes list
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            var filteredModes = System.Enum.GetValues(typeof(GameModes)).Cast<GameModes>()
                .Where(mode => string.IsNullOrEmpty(searchFilter) || 
                              mode.ToString().ToLower().Contains(searchFilter.ToLower()))
                .OrderBy(mode => mode.ToString());

            int columnCount = 2;
            int currentColumn = 0;

            EditorGUILayout.BeginHorizontal();
            
            foreach (var mode in filteredModes)
            {
                if (currentColumn >= columnCount)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    currentColumn = 0;
                }

                bool isSelected = selectedModes.Contains(mode);
                bool newSelected = EditorGUILayout.ToggleLeft(mode.ToString(), isSelected, GUILayout.Width(180));
                
                if (newSelected != isSelected)
                {
                    if (newSelected)
                        selectedModes.Add(mode);
                    else
                        selectedModes.Remove(mode);
                }

                currentColumn++;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // Selection summary
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Selected: {selectedModes.Count} game modes", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Save/Cancel buttons
            EditorGUILayout.BeginHorizontal();
            
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.5f, 0.8f, 0.5f);
            
            if (GUILayout.Button("Save & Close", GUILayout.Height(30)))
            {
                SaveChanges();
                Close();
            }

            GUI.backgroundColor = new Color(0.8f, 0.5f, 0.5f);
            
            if (GUILayout.Button("Cancel", GUILayout.Height(30)))
            {
                Close();
            }

            GUI.backgroundColor = originalColor;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
        }

        private void SaveChanges()
        {
            if (activeGameModesProperty == null) return;

            activeGameModesProperty.ClearArray();
            
            foreach (var mode in selectedModes.OrderBy(m => m))
            {
                int index = activeGameModesProperty.arraySize;
                activeGameModesProperty.InsertArrayElementAtIndex(index);
                var element = activeGameModesProperty.GetArrayElementAtIndex(index);
                element.enumValueIndex = (int)mode;
            }

            serializedConfig.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
    }
}