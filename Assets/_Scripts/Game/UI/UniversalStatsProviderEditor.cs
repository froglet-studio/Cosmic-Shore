#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using CosmicShore.Game.Arcade;

namespace CosmicShore.Game.UI
{
    [CustomEditor(typeof(UniversalStatsProvider))]
    public class UniversalStatsProviderEditor : Editor
    {
        // Color scheme
        private static readonly Color TrackerColor = new Color(0.4f, 0.6f, 0.9f);
        private static readonly Color SuccessColor = new Color(0.3f, 0.8f, 0.4f);
        private static readonly Color WarningColor = new Color(0.95f, 0.75f, 0.3f);
        private static readonly Color ErrorColor = new Color(0.9f, 0.35f, 0.35f);
        private static readonly Color ListHeaderColor = new Color(0.5f, 0.5f, 0.5f);
        
        private ReorderableList statBindingsList;
        private SerializedProperty scoreTrackerProp;
        private SerializedProperty statBindingsProp;
        
        private bool showValidation;
        
        private void OnEnable()
        {
            scoreTrackerProp = serializedObject.FindProperty("scoreTracker");
            statBindingsProp = serializedObject.FindProperty("statBindings");
            
            SetupReorderableList();
        }
        
        private void SetupReorderableList()
        {
            statBindingsList = new ReorderableList(
                serializedObject, 
                statBindingsProp, 
                draggable: true, 
                displayHeader: true, 
                displayAddButton: true, 
                displayRemoveButton: true
            );
            
            statBindingsList.drawHeaderCallback = DrawListHeader;
            statBindingsList.drawElementCallback = DrawListElement;
            statBindingsList.onAddCallback = OnAddElement;
            statBindingsList.elementHeight = EditorGUIUtility.singleLineHeight * 3.5f + 10;
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            EditorGUILayout.Space(3);
            DrawTrackerSection();
            EditorGUILayout.Space(8);
            
            DrawStatsSection();
            EditorGUILayout.Space(8);
            
            DrawActionButtons();
            EditorGUILayout.Space(5);
            
            DrawValidationSection();
            
            serializedObject.ApplyModifiedProperties();
        }
        
        #region Drawing Methods
        
        private void DrawTrackerSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);
            
            // Header
            var headerStyle = new GUIStyle(EditorStyles.boldLabel);
            var originalColor = GUI.contentColor;
            GUI.contentColor = TrackerColor;
            EditorGUILayout.LabelField("🎯 Score Tracker", headerStyle);
            GUI.contentColor = originalColor;
            
            EditorGUILayout.Space(3);
            
            // Tracker field
            var prevTracker = scoreTrackerProp.objectReferenceValue;
            EditorGUILayout.PropertyField(scoreTrackerProp, new GUIContent("Tracker Reference"));
            
            // Handle tracker change
            if (scoreTrackerProp.objectReferenceValue != prevTracker)
            {
                if (statBindingsProp.arraySize > 0)
                {
                    if (EditorUtility.DisplayDialog(
                        "Tracker Changed", 
                        "Changing the tracker will clear all stat bindings. Continue?",
                        "Yes", "Cancel"))
                    {
                        statBindingsProp.ClearArray();
                    }
                    else
                    {
                        scoreTrackerProp.objectReferenceValue = prevTracker;
                    }
                }
            }
            
            // Show tracker status
            if (scoreTrackerProp.objectReferenceValue != null)
            {
                var tracker = scoreTrackerProp.objectReferenceValue as BaseScoreTracker;
                var exposable = tracker as IStatExposable;
                
                EditorGUILayout.Space(3);
                
                if (exposable != null)
                {
                    var stats = exposable.GetExposedStats();
                    var statsCount = stats != null ? stats.Count : 0;
                    
                    var statusStyle = new GUIStyle(EditorStyles.miniLabel);
                    statusStyle.normal.textColor = SuccessColor;
                    EditorGUILayout.LabelField(
                        $"✓ {tracker.GetType().Name} ({statsCount} stats available)", 
                        statusStyle
                    );
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"Tracker '{tracker.GetType().Name}' must implement IStatExposable interface to expose stats.",
                        MessageType.Error
                    );
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Assign a ScoreTracker to begin", MessageType.Info);
            }
            
            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }
        
        private void DrawStatsSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);
            
            // Header
            var headerStyle = new GUIStyle(EditorStyles.boldLabel);
            EditorGUILayout.LabelField("📊 Stats to Display", headerStyle);
            
            EditorGUILayout.Space(3);
            
            if (statBindingsProp.arraySize == 0)
            {
                EditorGUILayout.LabelField(
                    "Click + to add stats from your tracker",
                    EditorStyles.centeredGreyMiniLabel
                );
                GUILayout.Space(3);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Drag to reorder. Display order matches list order.",
                    MessageType.None
                );
            }
            
            statBindingsList.DoLayoutList();
            
            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }
        
        private void DrawActionButtons()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            
            var buttonStyle = new GUIStyle(GUI.skin.button) 
            { 
                fontStyle = FontStyle.Bold,
                fontSize = 11
            };
            
            // Show available stats
            GUI.enabled = scoreTrackerProp.objectReferenceValue != null;
            if (GUILayout.Button("📋 Show Available Stats", buttonStyle, GUILayout.Height(32)))
            {
                ShowAvailableStats();
            }
            GUI.enabled = true;
            
            // Preview stats (play mode only)
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("▶ Preview Current Values", buttonStyle, GUILayout.Height(32)))
            {
                PreviewStats();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            if (!Application.isPlaying)
            {
                var miniStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.LabelField("Preview available in Play Mode", miniStyle);
            }
            
            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }
        
        private void DrawValidationSection()
        {
            var provider = target as UniversalStatsProvider;
            
            if (!provider.ValidateBindings(out var errors))
            {
                showValidation = EditorGUILayout.Foldout(showValidation, "⚠ Validation Issues", true);
                
                if (showValidation)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    
                    var errorStyle = new GUIStyle(EditorStyles.miniLabel);
                    errorStyle.normal.textColor = ErrorColor;
                    errorStyle.wordWrap = true;
                    
                    foreach (var error in errors)
                    {
                        EditorGUILayout.LabelField($"  • {error}", errorStyle);
                    }
                    
                    EditorGUILayout.EndVertical();
                }
            }
        }
        
        #endregion
        
        #region Reorderable List Callbacks
        
        private void DrawListHeader(Rect rect)
        {
            var iconRect = new Rect(rect.x + 15, rect.y, 30, rect.height);
            var moduleRect = new Rect(rect.x + 50, rect.y, (rect.width - 50) * 0.5f, rect.height);
            var keyRect = new Rect(rect.x + 50 + (rect.width - 50) * 0.5f, rect.y, (rect.width - 50) * 0.5f, rect.height);
            
            var headerStyle = new GUIStyle(EditorStyles.miniLabel);
            headerStyle.normal.textColor = ListHeaderColor;
            headerStyle.fontStyle = FontStyle.Bold;
            
            EditorGUI.LabelField(iconRect, "", headerStyle);
            EditorGUI.LabelField(moduleRect, "Stat Module", headerStyle);
            EditorGUI.LabelField(keyRect, "Data Source", headerStyle);
        }
        
        private void DrawListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = statBindingsList.serializedProperty.GetArrayElementAtIndex(index);
            var moduleProp = element.FindPropertyRelative("StatModule");
            var keyProp = element.FindPropertyRelative("StatKey");
            
            rect.y += 3;
            var lineHeight = EditorGUIUtility.singleLineHeight;
            
            // Row 1: Icon + Module
            var iconRect = new Rect(rect.x, rect.y, 24, 24);
            var moduleRect = new Rect(rect.x + 30, rect.y, rect.width - 30, lineHeight);
            
            // Draw icon
            if (moduleProp.objectReferenceValue != null)
            {
                var module = moduleProp.objectReferenceValue as StatModuleSO;
                if (module != null && module.Icon != null)
                {
                    EditorGUI.DrawTextureTransparent(iconRect, module.Icon.texture, ScaleMode.ScaleToFit);
                }
                else
                {
                    EditorGUI.LabelField(iconRect, "❓");
                }
            }
            
            EditorGUI.PropertyField(moduleRect, moduleProp, GUIContent.none);
            
            rect.y += lineHeight + 3;
            
            // Row 2: Data source dropdown
            var labelRect = new Rect(rect.x + 30, rect.y, 90, lineHeight);
            var dropdownRect = new Rect(rect.x + 125, rect.y, rect.width - 95, lineHeight);
            
            var miniLabelStyle = new GUIStyle(EditorStyles.miniLabel);
            miniLabelStyle.normal.textColor = Color.gray;
            EditorGUI.LabelField(labelRect, "Data Source:", miniLabelStyle);
            
            DrawKeyDropdown(dropdownRect, keyProp);
            
            rect.y += lineHeight + 3;
            
            // Row 3: Preview
            if (moduleProp.objectReferenceValue != null && !string.IsNullOrEmpty(keyProp.stringValue))
            {
                var previewRect = new Rect(rect.x + 30, rect.y, rect.width - 30, lineHeight);
                var module = moduleProp.objectReferenceValue as StatModuleSO;
                
                var previewStyle = new GUIStyle(EditorStyles.miniLabel);
                previewStyle.normal.textColor = new Color(0.6f, 0.8f, 0.6f);
                
                var previewText = $"Shows: \"{module.Label}\" formatted as {module.FormatType}";
                EditorGUI.LabelField(previewRect, previewText, previewStyle);
            }
        }
        
        private void DrawKeyDropdown(Rect rect, SerializedProperty keyProp)
        {
            var provider = target as UniversalStatsProvider;
            var availableKeys = provider.GetAvailableStatKeys();
            
            if (availableKeys.Count == 0)
            {
                EditorGUI.LabelField(rect, "No stats available", EditorStyles.miniLabel);
                return;
            }
            
            // Add "Select..." as first option
            var options = new List<string> { "-- Select Stat --" };
            options.AddRange(availableKeys);
            
            var currentKey = keyProp.stringValue;
            var selectedIndex = string.IsNullOrEmpty(currentKey) ? 0 : availableKeys.IndexOf(currentKey) + 1;
            if (selectedIndex < 0) selectedIndex = 0;
            
            var newIndex = EditorGUI.Popup(rect, selectedIndex, options.ToArray());
            
            if (newIndex == 0)
            {
                keyProp.stringValue = "";
            }
            else if (newIndex != selectedIndex)
            {
                keyProp.stringValue = availableKeys[newIndex - 1];
            }
        }
        
        private void OnAddElement(ReorderableList list)
        {
            var index = list.serializedProperty.arraySize;
            list.serializedProperty.arraySize++;
            list.index = index;
            
            var element = list.serializedProperty.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("StatModule").objectReferenceValue = null;
            element.FindPropertyRelative("StatKey").stringValue = "";
        }
        
        #endregion
        
        #region Helper Methods
        
        private void ShowAvailableStats()
        {
            var provider = target as UniversalStatsProvider;
            var keys = provider.GetAvailableStatKeys();
            
            if (keys.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "No Stats Available",
                    "The tracker has no exposed stats.\n\n" +
                    "Make sure:\n" +
                    "• A tracker is assigned\n" +
                    "• Tracker implements IStatExposable\n" +
                    "• GetExposedStats() returns values",
                    "OK"
                );
                return;
            }
            
            var message = $"Your tracker exposes {keys.Count} stats:\n\n";
            foreach (var key in keys)
            {
                message += $"  • {key}\n";
            }
            message += "\nUse these as data sources for your stat bindings.";
            
            EditorUtility.DisplayDialog("Available Stats", message, "OK");
        }
        
        private void PreviewStats()
        {
            var provider = target as UniversalStatsProvider;
            var stats = provider.GetStats();
            
            if (stats.Count == 0)
            {
                Debug.LogWarning("═══════════════════════════════════\n" +
                                 "No stats configured or returned\n" +
                                 "═══════════════════════════════════");
                return;
            }
            
            Debug.Log("═══════════════════════════════════");
            Debug.Log($"<color=cyan><b>  STATS PREVIEW ({stats.Count} total)</b></color>");
            Debug.Log("═══════════════════════════════════");
            
            foreach (var stat in stats)
            {
                var icon = stat.Icon != null ? "✓" : " ";
                Debug.Log($"<color=cyan>[{icon}] <b>{stat.Label}</b>: {stat.Value}</color>");
            }
            
            Debug.Log("═══════════════════════════════════");
        }
        
        #endregion
    }
}
#endif