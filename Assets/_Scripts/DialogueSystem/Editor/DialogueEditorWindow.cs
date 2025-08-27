#if !LINUX_BUILD
using CosmicShore.DialogueSystem.Models;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace CosmicShore.DialogueSystem.Editor
{
    public class DialogueEditorWindow : EditorWindow
    {
        // -------------------------------------------------------------------
        // Fields & State
        // -------------------------------------------------------------------
        private DialogueSet selectedSet;
        private int selectedLineIndex = -1;
        private Vector2 leftScroll, centerScroll;
        private float leftPanelWidth = 200f;
        private float centerPanelWidth = 900f;
        private float rightPanelWidth = 220f;
        private const float CENTER_MIN = 320f;
        private const float RIGHT_MIN = 200f;
        private readonly Dictionary<DialogueSet, Color> _setBackgroundColors = new();
        private bool _hasUnsavedChanges = false;
        private int _activeSpritePickerControlID = -1;
        private DialogueSpeaker _slotPickingFor = DialogueSpeaker.Speaker1;
        private readonly float bottomBarHeight = 38f;
        private ReorderableList _linesList;
        private DialogueSet _linesListTarget;

        const string deleteGlyph = "\u2716"; // "?"


        // Your DialogueSet folder path (update as needed)
        private static readonly string DialogueSetFolder = "Assets/_Scripts/DialogueSystem/SO";

        [MenuItem("FrogletTools/Dialogue Editor")]
        public static void Open()
        {
            GetWindow<DialogueEditorWindow>("Dialogue Editor");
        }

        private void OnEnable()
        {
            // Load per-set colors
            var guids = AssetDatabase.FindAssets("t:DialogueSet");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string key = $"DialogueEditor_SetBg_{guid}";
                if (EditorPrefs.HasKey(key))
                {
                    Color c = JsonUtility.FromJson<Color>(EditorPrefs.GetString(key));
                    var set = AssetDatabase.LoadAssetAtPath<DialogueSet>(path);
                    _setBackgroundColors[set] = c;
                }
            }
        }

        private void OnGUI()
        {
            float panelSpacing = 10f;
            float totalWidth = position.width;
            float centerPanelWidth = totalWidth - leftPanelWidth - rightPanelWidth - panelSpacing * 2;
            centerPanelWidth = Mathf.Max(centerPanelWidth, 400f);

            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

            // --- Left Panel ---
            EditorGUILayout.BeginVertical(GUILayout.Width(leftPanelWidth));
            DrawLeftPanel();
            EditorGUILayout.EndVertical();

            Rect sep1 = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Width(1), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(sep1, new Color(0.3f, 0.3f, 0.3f, 1f));
            GUILayout.Space(panelSpacing);

            // --- Center Panel ---
            EditorGUILayout.BeginVertical(GUILayout.Width(centerPanelWidth));
            DrawCenterPanel();
            EditorGUILayout.EndVertical();

            Rect sep2 = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Width(1), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(sep2, new Color(0.3f, 0.3f, 0.3f, 1f));
            GUILayout.Space(.22f);

            // --- Right Panel ---
            EditorGUILayout.BeginVertical(GUILayout.Width(rightPanelWidth));
            DrawRightPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            // --- Bottom Bar ---
            GUILayout.Space(4);
            EditorGUILayout.BeginHorizontal(GUILayout.Height(bottomBarHeight));
            GUILayout.Space(8);

            GUILayout.FlexibleSpace();

            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.2f);
            if (_hasUnsavedChanges && GUILayout.Button("Save Changes", GUILayout.Width(140)))
                SaveAllDialogueSets();
            GUI.backgroundColor = Color.white;
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6);

            // --- Object Picker handling (unchanged) ---
            if (Event.current.commandName == "ObjectSelectorClosed"
                && EditorGUIUtility.GetObjectPickerControlID() == _activeSpritePickerControlID)
            {
                Sprite picked = EditorGUIUtility.GetObjectPickerObject() as Sprite;
                if (picked != null && selectedSet != null)
                {
                    Undo.RecordObject(selectedSet, "Assign Portrait");
                    if (_slotPickingFor == DialogueSpeaker.Speaker1)
                        selectedSet.portraitSpeaker1 = picked;
                    else
                        selectedSet.portraitSpeaker2 = picked;

                    EditorUtility.SetDirty(selectedSet);
                    _hasUnsavedChanges = true;
                }
                _activeSpritePickerControlID = -1;
                Repaint();
            }
        }

        // -------------------------------------------------------------------
        // Left Panel: List of DialogueSets, each with its colored background
        // -------------------------------------------------------------------
        private void DrawLeftPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(leftPanelWidth));
            {
                // Header
                Rect headerRect = GUILayoutUtility.GetRect(
                    GUIContent.none,
                    EditorStyles.boldLabel,
                    GUILayout.Height(24),
                    GUILayout.ExpandWidth(true)
                );
                EditorGUI.DrawRect(headerRect, new Color(0.18f, 0.20f, 0.27f, 1f));
                EditorGUI.LabelField(headerRect, "  Dialogue Sets", EditorStyles.boldLabel);
            }

            GUILayout.Space(8);

            leftScroll = EditorGUILayout.BeginScrollView(leftScroll);

            var guids = AssetDatabase.FindAssets("t:DialogueSet", new[] { DialogueSetFolder });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                DialogueSet set = AssetDatabase.LoadAssetAtPath<DialogueSet>(path);

                if (!_setBackgroundColors.ContainsKey(set))
                    _setBackgroundColors[set] = new Color(0.78f, 0.87f, 0.99f, 1f); // pastel blue

                Color rowColor = _setBackgroundColors[set];

                // -- Layout: one rect for this row
                float rowHeight = 24;
                Rect rowRect = GUILayoutUtility.GetRect(1, rowHeight, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(rowRect, rowColor);

                // --- Controls: absolute placement ---
                float iconSize = 18f;
                float spacing = 6f;
                float colorWidth = 28f;

                // Delete button rect
                Rect deleteRect = new Rect(rowRect.x + spacing, rowRect.y + (rowHeight - iconSize) / 2, iconSize, iconSize);

                // Color picker rect
                Rect colorRect = new Rect(rowRect.xMax - colorWidth - spacing, rowRect.y + (rowHeight - iconSize) / 2, colorWidth, iconSize);

                // Label rect (fills the space between delete and color)
                float labelX = deleteRect.xMax + spacing;
                float labelWidth = colorRect.xMin - labelX - spacing;
                Rect labelRect = new Rect(labelX, rowRect.y + 2, labelWidth, rowHeight - 4);

                // -- Delete Icon --
                GUIStyle iconStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.93f, 0.36f, 0.36f, 1f) }
                };
                if (GUI.Button(deleteRect, deleteGlyph, iconStyle))
                {
                    if (EditorUtility.DisplayDialog("Delete Dialogue Set",
                        $"Are you sure you want to delete '{set.setId}'?\n\nThis cannot be undone.",
                        "Delete", "Cancel"))
                    {
                        AssetDatabase.DeleteAsset(path);
                        AssetDatabase.SaveAssets();
                        _setBackgroundColors.Remove(set);
                        if (selectedSet == set) selectedSet = null;
                        selectedLineIndex = -1;
                        GUIUtility.ExitGUI();
                    }
                }

                // -- Set name (as button, for selection) --
                GUIStyle labelStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = selectedSet == set ? FontStyle.Bold : FontStyle.Normal,
                    normal = { textColor = new Color(0.19f, 0.24f, 0.33f, 1f) }
                };
                if (GUI.Button(labelRect, set.setId, labelStyle))
                {
                    selectedSet = set;
                    selectedLineIndex = -1;
                    GUIUtility.ExitGUI();
                }

                // -- Color Picker (absolute position) --
                Color prevColor = _setBackgroundColors[set];
                Color newColor = EditorGUI.ColorField(colorRect, GUIContent.none, prevColor, false, false, false);
                if (newColor != prevColor)
                {
                    _setBackgroundColors[set] = newColor;
                    _hasUnsavedChanges = true;
                    string key = $"DialogueEditor_SetBg_{guid}";
                    EditorPrefs.SetString(key, JsonUtility.ToJson(newColor));
                }
            }

            EditorGUILayout.EndScrollView();

            // --- Add New Set: fixed folder, auto name ---
            if (GUILayout.Button("+ Add New Set", GUILayout.Width(120)))
            {
                // Ensure folder exists
                if (!AssetDatabase.IsValidFolder(DialogueSetFolder))
                    AssetDatabase.CreateFolder("Assets/_Scripts/DialogueSystem/", "SO");

                string baseName = "DialogueSet";
                int number = 1;
                string assetName, assetPath;
                do
                {
                    assetName = $"{baseName}_{number:D2}.asset";
                    assetPath = System.IO.Path.Combine(DialogueSetFolder, assetName);
                    number++;
                } while (System.IO.File.Exists(assetPath));

                var newSet = ScriptableObject.CreateInstance<DialogueSet>();
                newSet.setId = System.IO.Path.GetFileNameWithoutExtension(assetName);
                AssetDatabase.CreateAsset(newSet, assetPath);
                AssetDatabase.SaveAssets();
                selectedSet = newSet;
                selectedLineIndex = -1;
                _hasUnsavedChanges = true;
                GUIUtility.ExitGUI();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }




        // -------------------------------------------------------------------
        // Center Panel: Edit set ID, mode, and lines (or reward)
        // -------------------------------------------------------------------
        private void DrawCenterPanel()
        {
            float width = Mathf.Max(centerPanelWidth, CENTER_MIN);
            EditorGUILayout.BeginVertical(GUILayout.Width(width));

            // Header
            Rect headerRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.boldLabel, GUILayout.Height(24), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(headerRect, new Color(0.24f, 1f, 0.71f, 0.2f)); // Lime  
            EditorGUI.LabelField(headerRect, " Dialogue Set Editor", EditorStyles.boldLabel);

            GUILayout.Space(8);

            if (selectedSet == null)
            {
                EditorGUILayout.LabelField("Select a Dialogue Set to begin.", GUILayout.ExpandHeight(true));
                EditorGUILayout.EndVertical();
                return;
            }

            GUILayout.Space(6);

            // --- ID field ---
            EditorGUI.BeginChangeCheck();
            string newId = EditorGUILayout.TextField("ID", selectedSet.setId);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(selectedSet, "Edit Dialogue Set ID");
                selectedSet.setId = newId;
                EditorUtility.SetDirty(selectedSet);
                _hasUnsavedChanges = true;
            }

            // --- Dialogue Mode dropdown ---
            EditorGUI.BeginChangeCheck();
            var newMode = DrawColoredEnumPopup("Dialogue Mode", selectedSet.mode, new Color(0.24f, 0.36f, 0.67f, 0.90f));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(selectedSet, "Change Dialogue Mode");
                selectedSet.mode = newMode;
                EditorUtility.SetDirty(selectedSet);
                _hasUnsavedChanges = true;
            }

            GUILayout.Space(6);

            if (selectedSet.mode == DialogueModeType.Reward)
            {
                DrawRewardSection(selectedSet);
            }
            else
            {
                DrawDialogueLinesSection(selectedSet);
            }

            GUILayout.Space(6);

            EditorGUILayout.EndVertical();
        }

        private void DrawDialogueLinesSection(DialogueSet set)
        {
            // (Same as before, using ReorderableList logic)
            if (set != null && (_linesList == null || _linesListTarget != set))
            {
                _linesListTarget = set;
                _linesList = new ReorderableList(set.lines, typeof(DialogueLine), true, true, true, true)
                {
                    drawHeaderCallback = rect =>
                    {
                        EditorGUI.DrawRect(new Rect(rect.x - 4, rect.y, rect.width + 8, rect.height), new Color(0.1f, 0.3f, 0.1f, 0.6f));
                        EditorGUI.LabelField(rect, "Dialogue Lines", EditorStyles.boldLabel);
                    }
                };

                _linesList.drawElementBackgroundCallback = (rect, idx, isActive, isFocused) =>
                {
                    Color rowBg;
                    if (idx == _linesList.index)
                        rowBg = new Color(0.361f, 0.423f, 0.757f, 1f);
                    else if (idx % 2 == 0)
                        rowBg = new Color(0.227f, 0.286f, 0.671f, 0.6f);
                    else
                        rowBg = new Color(0.188f, 0.247f, 0.619f, 0.6f);

                    EditorGUI.DrawRect(rect, rowBg);
                };

                _linesList.drawElementCallback = (rect, idx, isActive, isFocused) =>
                {
                    var line = set.lines[idx];
                    rect.y += 2;
                    float h = EditorGUIUtility.singleLineHeight;

                    var prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = (line.speaker == DialogueSpeaker.Speaker1)
                        ? new Color(0.2f, 0.6f, 0.9f)
                        : new Color(0.9f, 0.6f, 0.2f);
                    line.speaker = (DialogueSpeaker)EditorGUI.EnumPopup(new Rect(rect.x, rect.y, 80, h), line.speaker);
                    GUI.backgroundColor = prevBg;

                    line.speakerName = EditorGUI.TextField(new Rect(rect.x + 95, rect.y, 100, h), line.speakerName);

                    float textW = rect.width - 350;
                    line.text = EditorGUI.TextField(new Rect(rect.x + 230, rect.y, textW, h), line.text);

                    line.displayTime = EditorGUI.FloatField(new Rect(rect.x + rect.width - 100, rect.y, 20, h), line.displayTime);

                    if (set.mode == DialogueModeType.Monologue)
                    {
                        // reserve a 20px toggler just before the speaker icon
                        Rect tgRect = new(rect.x + rect.width - 48, rect.y, 20, h);
                        line.isInGameMonologue = EditorGUI.Toggle(
                            tgRect,
                            line.isInGameMonologue
                        );
                    }

                    const string speakerGlyph = "\uD83D\uDD0A";
                    if (GUI.Button(new Rect(rect.x + rect.width - 24, rect.y, 20, h), speakerGlyph, GUIStyle.none))
                        DialogueAudioBatchLinker.LinkMissingAudio(set);
                };

                _linesList.onSelectCallback = list => selectedLineIndex = list.index;
            }

            if (_linesList != null)
            {
                float listWidth = 960f;
                float listHeight = _linesList.GetHeight();
                Rect listRect = EditorGUILayout.GetControlRect(false, listHeight, GUILayout.Width(listWidth));
                _linesList.DoList(listRect);
            }
        }

        private void DrawRewardSection(DialogueSet set)
        {
            if (set.rewardData == null)
                set.rewardData = new RewardData();

            GUILayout.Space(8);

            // 1. Reward Type Enum (teal/cyan)
            EditorGUI.BeginChangeCheck();
            set.rewardData.rewardType = DrawColoredEnumPopup("Reward Type", set.rewardData.rewardType, new Color(0.09f, 0.66f, 0.72f, 0.80f));
            if (EditorGUI.EndChangeCheck()) _hasUnsavedChanges = true;

            // 2. Reward Value (string/int)
            EditorGUI.BeginChangeCheck();
            set.rewardData.rewardValue = EditorGUILayout.TextField("Reward Value", set.rewardData.rewardValue);
            if (EditorGUI.EndChangeCheck()) _hasUnsavedChanges = true;

            // 3. Reward Image
            EditorGUI.BeginChangeCheck();
            set.rewardData.rewardImage = (Sprite)EditorGUILayout.ObjectField("Reward Image", set.rewardData.rewardImage, typeof(Sprite), false);
            if (EditorGUI.EndChangeCheck()) _hasUnsavedChanges = true;

            // 4. Description
            EditorGUI.BeginChangeCheck();
            set.rewardData.description = EditorGUILayout.TextField("Description", set.rewardData.description);
            if (EditorGUI.EndChangeCheck()) _hasUnsavedChanges = true;

            // 5. Rarity Enum (violet)
            EditorGUI.BeginChangeCheck();
            set.rewardData.rarity = DrawColoredEnumPopup("Rarity", set.rewardData.rarity, new Color(0.60f, 0.36f, 0.72f, 0.85f));
            if (EditorGUI.EndChangeCheck()) _hasUnsavedChanges = true;

            // 6. Condition
            EditorGUI.BeginChangeCheck();
            set.rewardData.condition = EditorGUILayout.TextField("Condition", set.rewardData.condition);
            if (EditorGUI.EndChangeCheck()) _hasUnsavedChanges = true;

            // 7. Unlock Trigger
            EditorGUI.BeginChangeCheck();
            set.rewardData.unlockTrigger = EditorGUILayout.TextField("Unlock Trigger", set.rewardData.unlockTrigger);
            if (EditorGUI.EndChangeCheck()) _hasUnsavedChanges = true;

            // 8. Custom Script/Callback
            EditorGUI.BeginChangeCheck();
            set.rewardData.customScript = EditorGUILayout.TextField("Custom Script/Callback", set.rewardData.customScript);
            if (EditorGUI.EndChangeCheck()) _hasUnsavedChanges = true;
        }



        // -------------------------------------------------------------------
        // Right Panel: Always-visible preview of the set�s portraits and line text
        // -------------------------------------------------------------------
        private void DrawRightPanel()
        {
            // Larger styles for preview text
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 };
            var bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 12 };

            EditorGUILayout.BeginVertical(GUILayout.Width(rightPanelWidth));
            Rect headerRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.boldLabel, GUILayout.Height(24), GUILayout.ExpandWidth(true));

            // Adjust the header based on mode
            string headerLabel = selectedSet?.mode == DialogueModeType.Reward ? " Reward Data Preview" : " Data Preview";
            EditorGUI.DrawRect(headerRect, new Color(0.24f, 1f, 0.71f, 0.2f));// Lime  
            EditorGUI.LabelField(headerRect, headerLabel, EditorStyles.boldLabel);

            GUILayout.Space(8);
            if (selectedSet == null)
            {
                GUILayout.Space(4);
                EditorGUILayout.HelpBox("Select a Dialogue Set to preview.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            if (selectedSet.mode == DialogueModeType.Reward)
            {
                DrawRewardPreview(selectedSet.rewardData); // Only this in rewards mode!
                EditorGUILayout.EndVertical();
                return;
            }

            if (selectedSet.mode == DialogueModeType.Dialogue)
            {
                // 1) Two portraits
                EditorGUILayout.BeginHorizontal();
                DrawPortraitPicker(
                    selectedSet,
                    DialogueSpeaker.Speaker1,
                    selectedSet.portraitSpeaker1,
                    "Speaker 1"
                );
                GUILayout.Space(10);
                DrawPortraitPicker(
                    selectedSet,
                    DialogueSpeaker.Speaker2,
                    selectedSet.portraitSpeaker2,
                    "Speaker 2"
                );
                EditorGUILayout.EndHorizontal();

                // 2) Extra gap before the text block
                GUILayout.Space(16);

                // 3) If no line selected, placeholder
                if (selectedLineIndex < 0 || selectedLineIndex >= selectedSet.lines.Count)
                {
                    EditorGUILayout.LabelField(
                        "Add or select a line to see its text.",
                        EditorStyles.centeredGreyMiniLabel
                    );
                }
                else
                {
                    // 4) Show the actual speakerName as heading
                    var line = selectedSet.lines[selectedLineIndex];
                    EditorGUILayout.LabelField(line.speakerName, titleStyle);

                    // 5) Small gap before body text
                    GUILayout.Space(8);

                    // 6) Tint by speaker and draw the line text
                    Color orig = GUI.contentColor;
                    GUI.contentColor = (line.speaker == DialogueSpeaker.Speaker1)
                        ? DialogueVisuals.GetColorForSpeaker(DialogueSpeaker.Speaker1)
                        : DialogueVisuals.GetColorForSpeaker(DialogueSpeaker.Speaker2);

                    EditorGUILayout.LabelField(line.text, bodyStyle, GUILayout.ExpandWidth(true));

                    GUI.contentColor = orig;
                }
            }
            else // Monologue
            {
                // 1) Portrait slot (always gray background)
                DrawPortraitPicker(
                    selectedSet,
                    DialogueSpeaker.Speaker1,
                    selectedSet.portraitSpeaker1,
                    selectedSet.setId
                );

                // 2) More breathing room
                GUILayout.Space(16);

                // --- Determine which line to show ---
                if (selectedSet.lines == null || selectedSet.lines.Count == 0)
                {
                    EditorGUILayout.LabelField("No dialogue lines.", EditorStyles.centeredGreyMiniLabel);
                    EditorGUILayout.EndVertical();
                    return;
                }
                int idx = selectedLineIndex;
                if (idx < 0 || idx >= selectedSet.lines.Count)
                    idx = 0;   // fallback to the first line

                // 3) Get that line
                DialogueLine line = selectedSet.lines[idx];

                // 4) Heading: use the line�s speakerName (or setId if you prefer)
                EditorGUILayout.LabelField(line.speakerName + ":", titleStyle);

                GUILayout.Space(8);

                // 5) Body text in larger font, tinted if you like
                Color orig = GUI.contentColor;
                GUI.contentColor = DialogueVisuals.GetModeColor(DialogueModeType.Monologue);
                EditorGUILayout.LabelField(line.text, bodyStyle, GUILayout.MaxWidth(rightPanelWidth - 48));
                GUI.contentColor = orig;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRewardPreview(RewardData reward)
        {
            if (reward == null)
            {
                EditorGUILayout.LabelField("No reward data.", GUILayout.ExpandHeight(true));
                return;
            }

            GUILayout.Space(12);

            // Show reward image, or placeholder
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Texture2D tex = reward.rewardImage != null ? reward.rewardImage.texture : Texture2D.whiteTexture;
            GUILayout.Label(tex, GUILayout.Width(96), GUILayout.Height(96));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            // Show info fields
            EditorGUILayout.LabelField("Reward Type", reward.rewardType.ToString(), EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Value", reward.rewardValue);
            EditorGUILayout.LabelField("Rarity", reward.rarity.ToString());
            if (!string.IsNullOrEmpty(reward.description))
                EditorGUILayout.LabelField("Description", reward.description, EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(reward.condition))
                EditorGUILayout.LabelField("Condition", reward.condition, EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(reward.unlockTrigger))
                EditorGUILayout.LabelField("Unlock Trigger", reward.unlockTrigger);
            if (!string.IsNullOrEmpty(reward.customScript))
                EditorGUILayout.LabelField("Custom Script/Callback", reward.customScript);
        }


        private void DrawPortraitPicker(DialogueSet set, DialogueSpeaker speakerSlot, Sprite currentPortrait, string label)
        {
            if (set == null)
                throw new ArgumentNullException(nameof(set));

            EditorGUILayout.BeginVertical(GUILayout.Width(96));
            Rect r = GUILayoutUtility.GetRect(96, 96);

            EditorGUI.DrawRect(new Rect(r.x + 4, r.y, r.width, r.height), new Color(0.25f, 0.25f, 0.25f));
            Texture tex = currentPortrait != null ? currentPortrait.texture : Texture2D.whiteTexture;
            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);

            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                _activeSpritePickerControlID = EditorGUIUtility.GetControlID(FocusType.Passive) + 1;
                _slotPickingFor = speakerSlot;
                EditorGUIUtility.ShowObjectPicker<Sprite>(currentPortrait, false, "", _activeSpritePickerControlID);
                Event.current.Use();
            }

            EditorGUILayout.LabelField(label, EditorStyles.centeredGreyMiniLabel, GUILayout.Width(96));
            EditorGUILayout.EndVertical();
        }

        private void SaveAllDialogueSets()
        {
            AssetDatabase.SaveAssets();
            _hasUnsavedChanges = false;
            Debug.Log("[Dialogue Editor] All changes saved.");
        }

        private T DrawColoredEnumPopup<T>(string label, T value, Color bgColor, params GUILayoutOption[] options) where T : Enum
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = bgColor;

            T result = (T)EditorGUILayout.EnumPopup(label, value, options);

            GUI.backgroundColor = prevBg;
            return result;
        }

    }
}
#endif
