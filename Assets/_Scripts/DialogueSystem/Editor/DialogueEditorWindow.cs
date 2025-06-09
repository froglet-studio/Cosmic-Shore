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
        private float centerPanelWidth = 650f;
        private float rightPanelWidth = 320f;

        private const float CENTER_MIN = 320f;
        private const float RIGHT_MIN = 200f;

        private readonly Dictionary<DialogueSet, Color> _setBackgroundColors
            = new Dictionary<DialogueSet, Color>();

        private bool _hasUnsavedChanges = false;

        // Object Picker state for Portrait selection
        private int _activeSpritePickerControlID = -1;
        private DialogueSpeaker _slotPickingFor = DialogueSpeaker.Speaker1;
 
        private readonly float bottomBarHeight = 38f; // Height of your button bar + padding

        // persistence keys
        const string kLeftBgKey = "DialogueEditor_LeftBg";
        const string kCenterBgKey = "DialogueEditor_CenterBg";
        const string kRightBgKey = "DialogueEditor_RightBg";

        // current panel colors (defaults chosen subtle and dark)
        private Color leftPanelBg = new Color(0.12f, 0.12f, 0.12f);
        private Color centerPanelBg = new Color(0.10f, 0.10f, 0.10f);
        private Color rightPanelBg = new Color(0.10f, 0.10f, 0.10f);

        private void OnEnable()
        {
            // Load per?Set colors
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

            // 1) Define your “standard” widths and spacing
            float leftPanelWidth = 200f;
            float rightPanelWidth = 250f;
            float panelSpacing = 10f;
            float totalWidth = position.width;
            float centerPanelWidth = totalWidth
                                   - leftPanelWidth
                                   - rightPanelWidth
                                   - panelSpacing * 2;
            centerPanelWidth = Mathf.Max(centerPanelWidth, 400f);

            // 2) Top padding
            GUILayout.Space(8);

            // 3) Main Horizontal Layout
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

            // — Left Panel —
            EditorGUILayout.BeginVertical(GUILayout.Width(leftPanelWidth));
            DrawLeftPanel();

            EditorGUILayout.EndVertical();

            // spacer
            GUILayout.Space(panelSpacing);

            // — Center Panel —
            EditorGUILayout.BeginVertical(GUILayout.Width(centerPanelWidth));
            DrawCenterPanel();
            EditorGUILayout.EndVertical();

            // spacer
            GUILayout.Space(panelSpacing);

            // — Right Panel —
            EditorGUILayout.BeginVertical(GUILayout.Width(rightPanelWidth));
            DrawRightPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            // — Bottom Bar (unchanged) —
            GUILayout.Space(4);
            EditorGUILayout.BeginHorizontal(GUILayout.Height(bottomBarHeight));
            GUILayout.Space(8);
            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.2f);   // Turquoise
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
                // reserve a 24px header strip
                Rect headerRect = GUILayoutUtility.GetRect(
                    GUIContent.none,
                    EditorStyles.boldLabel,
                    GUILayout.Height(24),
                    GUILayout.ExpandWidth(true)
                );
                // draw dark-grey accent
                EditorGUI.DrawRect(headerRect, new Color(0.15f, 0.15f, 0.20f));
                // draw the title
                EditorGUI.LabelField(headerRect, "  Dialogue Sets", EditorStyles.boldLabel);
            }

            GUILayout.Space(8);

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

                    string key = $"DialogueEditor_SetBg_{guid}";
                    EditorPrefs.SetString(key, JsonUtility.ToJson(newColor));
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

            Rect headerRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.boldLabel, GUILayout.Height(24), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(headerRect, new Color(0.86f, 0.08f, 0.24f));  
            EditorGUI.LabelField(headerRect, " Dialogue Set Editor", EditorStyles.boldLabel);
            
            GUILayout.Space(8);

            if (selectedSet == null)
            {
                EditorGUILayout.LabelField("Select a Dialogue Set to begin.", GUILayout.ExpandHeight(true));
                EditorGUILayout.EndVertical();
                return;
            }

            //EditorGUILayout.LabelField("Dialogue Set Editor", EditorStyles.boldLabel);
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
            // *** Only vertical scroll, no horizontal! ***
            centerScroll = EditorGUILayout.BeginScrollView(centerScroll, false, false, GUILayout.ExpandHeight(true));
            for (int i = 0; i < selectedSet.lines.Count; i++)
            {
                DrawEditableDialogueRow(selectedSet, i);
            }
            EditorGUILayout.EndScrollView();

            // --- Button Block ---
            GUILayout.Space(10); // Small gap above buttons

            // Background "card"
            Rect btnCardRect = GUILayoutUtility.GetRect(centerPanelWidth - 30, 54, GUILayout.ExpandWidth(true));
            GUI.BeginGroup(btnCardRect);
            GUI.backgroundColor = new Color(0.75f, 0.75f, 0.75f); 
            GUI.Box(new Rect(0, 0, btnCardRect.width, btnCardRect.height), GUIContent.none);
            GUI.backgroundColor = Color.white;

            // Centered buttons
            float btnWidth = 100;
            float gap = 16;
            float totalBtnWidth = btnWidth * 3 + gap * 2;
            float xOffset = (btnCardRect.width - totalBtnWidth) / 2;
            float yOffset = (btnCardRect.height - 32) / 2;

            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.2f);
            if (GUI.Button(new Rect(xOffset, yOffset, btnWidth, 32), "Add New Line"))
            {
                string path = EditorUtility.SaveFilePanelInProject(
    "New Dialogue Set",
    "NewDialogueSet",
    "asset",
    "Create a new Dialogue Set asset"
);
                if (!string.IsNullOrEmpty(path))
                {
                    var newSet = CreateInstance<DialogueSet>();
                    newSet.setId = System.IO.Path.GetFileNameWithoutExtension(path);
                    AssetDatabase.CreateAsset(newSet, path);
                    AssetDatabase.SaveAssets();
                    selectedSet = newSet;
                    selectedLineIndex = -1;
                    _hasUnsavedChanges = true;
                }
            }
            
            GUI.backgroundColor = new Color(1f, 0f, 0f, 0.35f);
            if (GUI.Button(new Rect(xOffset + btnWidth + gap, yOffset, btnWidth, 32), "Test In Editor"))
            {
                DialogueEditorRuntimeTester.Test(selectedSet);
            }
            
            GUI.backgroundColor = new Color(0f, 0f, 1f, 0.35f);
            if (GUI.Button(new Rect(xOffset + (btnWidth + gap) * 2, yOffset, btnWidth, 32), "Link Audio"))
            {
                DialogueAudioBatchLinker.LinkMissingAudio(selectedSet);
            }
            GUI.EndGroup();
            EditorGUILayout.EndVertical();

            // Draw the splitter handle to allow resizing center|right
            centerPanelWidth = SplitterGUILayout.Splitter(
                centerPanelWidth,
                CENTER_MIN,
                position.width - leftPanelWidth - RIGHT_MIN - 10f,
                true
            );
        }

        // ?????????????????????????????????????????????????????????????????????????????
        // 5) DrawEditableDialogueRow ? flexible text?field width
        // ?????????????????????????????????????????????????????????????????????????????
        private void DrawEditableDialogueRow(DialogueSet set, int i)
        {
            DialogueLine line = set.lines[i];
            Color bgColor = (set.mode == DialogueModeType.Monologue)
                ? DialogueVisuals.GetModeColor(DialogueModeType.Monologue)
                : DialogueVisuals.GetColorForSpeaker(line.speaker);


            GUI.backgroundColor = bgColor;
            EditorGUILayout.BeginHorizontal("box");
            GUI.backgroundColor = Color.white;

            if (set.mode == DialogueModeType.Dialogue)
            {
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(set, "Change Speaker");
                    line.speaker = (DialogueSpeaker)EditorGUILayout.EnumPopup(line.speaker, GUILayout.Width(80));
                    EditorUtility.SetDirty(set);
                    _hasUnsavedChanges = true;
                }
            }

            // Speaker name (fixed width OK)
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(set, "Edit Speaker Name");
                line.speakerName = EditorGUILayout.TextField(line.speakerName, GUILayout.Width(100));
                EditorUtility.SetDirty(set);
                _hasUnsavedChanges = true;
            }

            // ? This now shrinks/grows to fit exactly, no cropping
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(set, "Edit Dialogue Text");
                line.text = EditorGUILayout.TextField(line.text, GUILayout.ExpandWidth(true));
                EditorUtility.SetDirty(set);
                _hasUnsavedChanges = true;
            }

            // Display time (fixed width OK)
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(set, "Change Display Time");
                line.displayTime = EditorGUILayout.FloatField(line.displayTime, GUILayout.Width(50));
                EditorUtility.SetDirty(set);
                _hasUnsavedChanges = true;
            }

            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                Undo.RecordObject(set, "Remove Dialogue Line");
                set.lines.RemoveAt(i);
                EditorUtility.SetDirty(set);
                _hasUnsavedChanges = true;
                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
                return;
            }

            if (GUILayout.Button(">", GUILayout.Width(20)))
                selectedLineIndex = i;

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
            var bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 12 };

            EditorGUILayout.BeginVertical(GUILayout.Width(rightPanelWidth));

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Sprite Preview", EditorStyles.boldLabel);

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
                EditorGUILayout.LabelField(line.text, bodyStyle, GUILayout.MaxWidth(rightPanelWidth - 48));
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
