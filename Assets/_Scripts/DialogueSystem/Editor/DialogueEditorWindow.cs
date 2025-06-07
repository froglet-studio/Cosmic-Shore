using CosmicShore.DialogueSystem.Models;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.DialogueSystem.Editor
{
    public class DialogueEditorWindow : EditorWindow
    {
        // -------------------------------------------------------------------
        // Fields & State
        // -------------------------------------------------------------------

        private DialogueSet selectedSet;              // currently selected ScriptableObject
        private int selectedLineIndex = -1;   // index of the line currently “speaking” in preview

        // Scroll positions
        private Vector2 leftScroll, centerScroll, rightScroll;

        // Panel widths
        private float leftPanelWidth = 200f;
        private float centerPanelWidth = 450f;
        private float rightPanelWidth = 280f;

        // Minimum widths so nobody collapses too far
        private const float LEFT_MIN = 150f;
        private const float CENTER_MIN = 320f;
        private const float RIGHT_MIN = 200f;

        // Each set has a background color for the left panel listing
        private readonly Dictionary<DialogueSet, Color> _setBackgroundColors
            = new Dictionary<DialogueSet, Color>();

        // If true, show “Save Changes” button
        private bool _hasUnsavedChanges = false;

        // Object Picker state for Portrait selection
        private int _activeSpritePickerControlID = -1;
        private DialogueSpeaker _slotPickingFor = DialogueSpeaker.Speaker1;
        private const float BOTTOM_BAR_HEIGHT = 32f;  // height of that bottom action row

        [MenuItem("FrogletTools/Dialogue Editor")]
        public static void Open()
        {
            GetWindow<DialogueEditorWindow>("Dialogue Editor");
        }

        // -------------------------------------------------------------------
        // OnGUI: Draw all panels and bottom buttons
        // -------------------------------------------------------------------

        private void OnGUI()
        {
            // ???????????????????????????????????????????????????????
            // 1) TOP: Left / Center / Right panels, resizable
            // ???????????????????????????????????????????????????????
            EditorGUILayout.BeginHorizontal();

            // ----- LEFT PANEL -----
            EditorGUILayout.BeginVertical(GUILayout.Width(leftPanelWidth));
            DrawLeftPanel();
            EditorGUILayout.EndVertical();

            // ----- LEFT/CENTER SPLITTER -----
            leftPanelWidth = SplitterGUILayout.Splitter(
                leftPanelWidth,
                LEFT_MIN,
                position.width - CENTER_MIN - RIGHT_MIN,
                vertical: true
            );

            // Draw the left divider (full height)
            EditorGUI.DrawRect(
                new Rect(leftPanelWidth, 0, 1, position.height),
                Color.gray
            );

            // ----- CENTER PANEL -----
            EditorGUILayout.BeginVertical(GUILayout.Width(centerPanelWidth));
            DrawCenterPanel();
            EditorGUILayout.EndVertical();

            // ----- CENTER/RIGHT SPLITTER -----
            centerPanelWidth = SplitterGUILayout.Splitter(
                centerPanelWidth,
                CENTER_MIN,
                position.width - leftPanelWidth - RIGHT_MIN,
                vertical: true
            );

            // Draw the center divider (full height)
            EditorGUI.DrawRect(
                new Rect(leftPanelWidth + centerPanelWidth + 1, 0, 1, position.height),
                Color.gray
            );

            // ----- RIGHT PANEL -----
            EditorGUILayout.BeginVertical(GUILayout.Width(rightPanelWidth));
            DrawRightPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();



            // ???????????????????????????????????????????????????????
            // 2) BOTTOM ACTION BAR
            // ???????????????????????????????????????????????????????
            GUILayout.Space(4);
            EditorGUILayout.BeginHorizontal(GUILayout.Height(BOTTOM_BAR_HEIGHT));

            // + Add New Set on far left
            GUILayout.Space(8);
            if (GUILayout.Button("+ Add New Set", GUILayout.Width(120)))
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    "New Dialogue Set",
                    "NewDialogueSet",
                    "asset",
                    "Create a new Dialogue Set asset"
                );
                if (!string.IsNullOrEmpty(path))
                {
                    var newSet = ScriptableObject.CreateInstance<DialogueSet>();
                    newSet.setId = System.IO.Path.GetFileNameWithoutExtension(path);
                    AssetDatabase.CreateAsset(newSet, path);
                    AssetDatabase.SaveAssets();
                    selectedSet = newSet;
                    selectedLineIndex = -1;
                    _hasUnsavedChanges = true;
                }
            }

            GUILayout.FlexibleSpace();

            // Add / Test / Link in the center
            GUI.enabled = (selectedSet != null);
            if (GUILayout.Button("Add New Line", GUILayout.Width(120)))
            {
                Undo.RecordObject(selectedSet, "Add Dialogue Line");
                selectedSet.lines.Add(new DialogueLine
                {
                    speaker = DialogueSpeaker.Speaker1,
                    speakerName = "Speaker",
                    text = "New dialogue line...",
                    displayTime = 3f
                });
                EditorUtility.SetDirty(selectedSet);
                _hasUnsavedChanges = true;
                Repaint();
            }
            if (GUILayout.Button("Test In Editor", GUILayout.Width(120)))
                DialogueEditorRuntimeTester.Test(selectedSet);
            if (GUILayout.Button("Link Audio", GUILayout.Width(120)))
                DialogueAudioBatchLinker.LinkMissingAudio(selectedSet);
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            // Save Changes on far right
            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.2f);
            if (_hasUnsavedChanges && GUILayout.Button("Save Changes", GUILayout.Width(140)))
                SaveAllDialogueSets();
            GUI.backgroundColor = Color.white;

            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6);



            // ???????????????????????????????????????????????????????
            // 3) Sprite Picker Closed Handler
            // ???????????????????????????????????????????????????????
            if (Event.current.commandName == "ObjectSelectorClosed" &&
                EditorGUIUtility.GetObjectPickerControlID() == _activeSpritePickerControlID)
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
            GUILayout.Space(8);
            EditorGUILayout.LabelField("Dialogue Sets", EditorStyles.boldLabel);

            leftScroll = EditorGUILayout.BeginScrollView(leftScroll);

            var guids = AssetDatabase.FindAssets("t:DialogueSet");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                DialogueSet set = AssetDatabase.LoadAssetAtPath<DialogueSet>(path);

                if (!_setBackgroundColors.ContainsKey(set))
                    _setBackgroundColors[set] = new Color(0.20f, 0.20f, 0.20f);

                Color bg = _setBackgroundColors[set];
                GUI.backgroundColor = bg;
                GUILayout.BeginHorizontal("box");
                GUI.backgroundColor = Color.white;

                GUIStyle style = (selectedSet == set) ? EditorStyles.whiteLabel : EditorStyles.label;
                if (GUILayout.Button(set.setId, style, GUILayout.ExpandWidth(true)))
                {
                    selectedSet = set;
                    selectedLineIndex = -1;
                }

                Color newColor = EditorGUILayout.ColorField(
                    GUIContent.none,
                    bg,
                    /* showEyedropper: */ false,
                    /* showAlpha: */      false,
                    /* hdr: */           false,
                    GUILayout.Width(20),
                    GUILayout.Height(16)
                );
                if (newColor != bg)
                {
                    _setBackgroundColors[set] = newColor;
                    _hasUnsavedChanges = true;
                }

                GUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            GUILayout.FlexibleSpace();

            EditorGUILayout.EndVertical();
        }

        // -------------------------------------------------------------------
        // Center Panel: Edit set ID, mode, and lines (no bottom buttons here)
        // -------------------------------------------------------------------
        private void DrawCenterPanel()
        {
            float width = Mathf.Max(centerPanelWidth, CENTER_MIN);
            EditorGUILayout.BeginVertical(GUILayout.Width(width));
            GUILayout.Space(8);

            if (selectedSet == null)
            {
                EditorGUILayout.LabelField("Select a Dialogue Set to begin.", GUILayout.ExpandHeight(true));
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField("Dialogue Set Editor", EditorStyles.boldLabel);
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
            DialogueModeType newMode = (DialogueModeType)EditorGUILayout.EnumPopup(
                "Dialogue Mode",
                selectedSet.mode
            );
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(selectedSet, "Change Dialogue Mode");
                selectedSet.mode = newMode;
                EditorUtility.SetDirty(selectedSet);
                _hasUnsavedChanges = true;
            }

            GUILayout.Space(6);
            EditorGUILayout.LabelField("Dialogue Lines", EditorStyles.boldLabel);

            // If no lines exist, show a helper text
            if (selectedSet.lines.Count == 0)
            {
                GUILayout.Space(4);
                EditorGUILayout.HelpBox("No dialogue lines. Use the + button below.", MessageType.Info);
            }

            // Draw each editable dialogue row:
            centerScroll = EditorGUILayout.BeginScrollView(centerScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < selectedSet.lines.Count; i++)
            {
                DrawEditableDialogueRow(selectedSet, i);
            }
            EditorGUILayout.EndScrollView();

            GUILayout.Space(8);
            EditorGUILayout.EndVertical();

            // Draw the splitter handle to allow resizing center|right
            centerPanelWidth = SplitterGUILayout.Splitter(
                centerPanelWidth,
                CENTER_MIN,
                position.width - leftPanelWidth - RIGHT_MIN - 10f,
                true
            );
        }

        private void DrawEditableDialogueRow(DialogueSet set, int i)
        {
            DialogueLine line = set.lines[i];

            Color bgColor = (set.mode == DialogueModeType.Monologue)
                ? DialogueVisuals.GetModeColor(DialogueModeType.Monologue)
                : DialogueVisuals.GetColorForSpeaker(line.speaker);

            GUI.backgroundColor = bgColor;
            EditorGUILayout.BeginHorizontal("box");
            GUI.backgroundColor = Color.white;

            // Speaker dropdown (only in Dialogue mode)
            if (set.mode == DialogueModeType.Dialogue)
            {
                EditorGUI.BeginChangeCheck();
                DialogueSpeaker newSpeaker =
                    (DialogueSpeaker)EditorGUILayout.EnumPopup(line.speaker, GUILayout.Width(80));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(set, "Change Speaker");
                    line.speaker = newSpeaker;
                    EditorUtility.SetDirty(set);
                    _hasUnsavedChanges = true;
                }
            }

            // Speaker name
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.TextField(line.speakerName, GUILayout.Width(100));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(set, "Edit Speaker Name");
                line.speakerName = newName;
                EditorUtility.SetDirty(set);
                _hasUnsavedChanges = true;
            }

            // Dialogue text
            EditorGUI.BeginChangeCheck();
            string newText = EditorGUILayout.TextField(
                line.text,
                GUILayout.MinWidth(160),
                GUILayout.MaxWidth(260)
            );
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(set, "Edit Dialogue Text");
                line.text = newText;
                EditorUtility.SetDirty(set);
                _hasUnsavedChanges = true;
            }

            // Display time
            EditorGUI.BeginChangeCheck();
            float newTime = EditorGUILayout.FloatField(line.displayTime, GUILayout.Width(50));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(set, "Change Display Time");
                line.displayTime = newTime;
                EditorUtility.SetDirty(set);
                _hasUnsavedChanges = true;
            }

            // Remove row
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                Undo.RecordObject(set, "Remove Dialogue Line");
                set.lines.RemoveAt(i);
                EditorUtility.SetDirty(set);
                _hasUnsavedChanges = true;
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                return;
            }

            if (GUILayout.Button(">", GUILayout.Width(20)))
            {
                selectedLineIndex = i;
                //Repaint();
            }

            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;
        }

        // -------------------------------------------------------------------
        // Right Panel: Always-visible preview of the set’s portraits and line text
        // -------------------------------------------------------------------
        private void DrawRightPanel()
        {
            // Larger styles for preview text
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 };
            var bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 14 };

            EditorGUILayout.BeginVertical(GUILayout.Width(rightPanelWidth));
            GUILayout.Space(8);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            if (selectedSet == null)
            {
                GUILayout.Space(4);
                EditorGUILayout.HelpBox("Select a Dialogue Set to preview.", MessageType.Info);
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
                    EditorGUILayout.LabelField(line.speakerName + ":", titleStyle);

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
                int idx = selectedLineIndex;
                if (idx < 0 || idx >= selectedSet.lines.Count)
                    idx = 0;   // fallback to the first line

                // 3) Get that line
                DialogueLine line = selectedSet.lines[idx];

                // 4) Heading: use the line’s speakerName (or setId if you prefer)
                EditorGUILayout.LabelField(line.speakerName + ":", titleStyle);

                GUILayout.Space(8);

                // 5) Body text in larger font, tinted if you like
                Color orig = GUI.contentColor;
                GUI.contentColor = DialogueVisuals.GetModeColor(DialogueModeType.Monologue);
                EditorGUILayout.LabelField(line.text, bodyStyle, GUILayout.ExpandWidth(true));
                GUI.contentColor = orig;
            }


            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws a 96×96 portrait picker button + a small label underneath.
        /// Clicking it opens the built?in Object Picker to assign a Sprite to the set.
        /// </summary>
        private void DrawPortraitPicker(DialogueSet set, DialogueSpeaker speakerSlot, Sprite currentPortrait, string label)
        {
            if (set == null)
            {
                throw new ArgumentNullException(nameof(set));
            }

            EditorGUILayout.BeginVertical(GUILayout.Width(96));

            // reserve the rect
            Rect r = GUILayoutUtility.GetRect(96, 96);

            // always draw a mid-gray background
            EditorGUI.DrawRect(r, new Color(0.25f, 0.25f, 0.25f));

            // draw the sprite (or a placeholder—whiteTexture is fine)
            Texture tex = currentPortrait != null
                ? currentPortrait.texture
                : Texture2D.whiteTexture;
            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);

            // picker click
            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                _activeSpritePickerControlID = EditorGUIUtility.GetControlID(FocusType.Passive) + 1;
                _slotPickingFor = speakerSlot;
                EditorGUIUtility.ShowObjectPicker<Sprite>(
                    currentPortrait, false, "", _activeSpritePickerControlID
                );
                Event.current.Use();
            }

            EditorGUILayout.LabelField(label, EditorStyles.centeredGreyMiniLabel, GUILayout.Width(96));
            EditorGUILayout.EndVertical();
        }



        // -------------------------------------------------------------------
        // Save Utility
        // -------------------------------------------------------------------
        private void SaveAllDialogueSets()
        {
            AssetDatabase.SaveAssets();
            _hasUnsavedChanges = false;
            Debug.Log("[Dialogue Editor] All changes saved.");
        }
    }
}
