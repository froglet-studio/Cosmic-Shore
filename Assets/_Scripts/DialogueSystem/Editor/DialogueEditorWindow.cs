#if !LINUX_BUILD
using CosmicShore.DialogueSystem.Models;
using Obvious.Soap;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.DialogueSystem.Editor
{
    public class DialogueEditorWindow : EditorWindow
    {
        // -------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------
        private TutorialSequence _selectedSequence;
        private int _selectedInstructionIndex = -1;
        private Vector2 _leftScroll;
        private Vector2 _centerScroll;
        private Vector2 _flowScroll;
        private bool _hasUnsavedChanges;
        private bool _showInstructionPreview; // false = Sequence Flow, true = Instruction Preview
        private Vector2 _previewScroll;

        // Layout
        private const float LEFT_PANEL_WIDTH = 200f;
        private const float RIGHT_PANEL_WIDTH = 250f;
        private const float BOTTOM_BAR_HEIGHT = 36f;
        private const float CARD_PADDING = 10f;
        private const float CARD_GAP = 8f;

        private static readonly string SequenceFolder = "Assets/_Scripts/DialogueSystem/SO";

        // -------------------------------------------------------------------
        // Dark Pastel Color Palette (Unity dark-theme friendly)
        // -------------------------------------------------------------------
        private static readonly Color BgLight = new(0.22f, 0.22f, 0.25f, 1f);
        private static readonly Color BgMedium = new(0.19f, 0.19f, 0.22f, 1f);
        private static readonly Color CardBg = new(0.24f, 0.24f, 0.28f, 1f);
        private static readonly Color CardBorder = new(0.30f, 0.30f, 0.35f, 1f);
        private static readonly Color CardSelected = new(0.26f, 0.28f, 0.36f, 1f);

        private static readonly Color PastelBlue = new(0.35f, 0.50f, 0.70f, 1f);
        private static readonly Color PastelMint = new(0.30f, 0.55f, 0.45f, 1f);
        private static readonly Color PastelPeach = new(0.65f, 0.45f, 0.35f, 1f);
        private static readonly Color PastelLavender = new(0.42f, 0.36f, 0.58f, 1f);
        private static readonly Color PastelPink = new(0.60f, 0.38f, 0.42f, 1f);
        private static readonly Color PastelYellow = new(0.60f, 0.55f, 0.32f, 1f);

        private static readonly Color TextLight = new(0.85f, 0.85f, 0.88f, 1f);
        private static readonly Color TextMuted = new(0.60f, 0.60f, 0.65f, 1f);
        private static readonly Color TextWhite = new(0.95f, 0.95f, 0.97f, 1f);

        private static readonly Color RowDefault = new(0.24f, 0.24f, 0.28f, 1f);
        private static readonly Color RowSelected = new(0.30f, 0.38f, 0.52f, 1f);
        private static readonly Color SeparatorColor = new(0.32f, 0.32f, 0.36f, 1f);

        // Flowchart
        private static readonly Color FlowNodeAuto = new(0.28f, 0.45f, 0.38f, 1f);
        private static readonly Color FlowNodeEvent = new(0.52f, 0.38f, 0.30f, 1f);
        private static readonly Color FlowNodeSelected = new(0.32f, 0.42f, 0.58f, 1f);
        private static readonly Color FlowStartNode = new(0.38f, 0.32f, 0.52f, 1f);
        private static readonly Color FlowEndNode = new(0.50f, 0.34f, 0.38f, 1f);
        private static readonly Color FlowLine = new(0.45f, 0.45f, 0.50f, 1f);

        // Badge text (lighter for readability on dark badge backgrounds)
        private static readonly Color BadgeText = new(0.90f, 0.90f, 0.92f, 1f);

        // Cached styles
        private GUIStyle _headerStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _cardLabelStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _flowNodeStyle;
        private GUIStyle _flowLabelStyle;
        private GUIStyle _buttonStyle;
        private bool _stylesInitialized;

        [MenuItem("FrogletTools/Tutorial Sequence Editor")]
        public static void Open()
        {
            var window = GetWindow<DialogueEditorWindow>("Tutorial Sequence Editor");
            window.minSize = new Vector2(900, 500);
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = TextWhite },
                padding = new RectOffset(8, 8, 4, 4)
            };

            _sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                normal = { textColor = TextMuted },
                padding = new RectOffset(4, 4, 2, 2)
            };

            _cardLabelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                normal = { textColor = TextMuted }
            };

            _badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = TextLight },
                padding = new RectOffset(6, 6, 2, 2)
            };

            _flowNodeStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(4, 4, 4, 4),
                normal = { textColor = TextLight }
            };

            _flowLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 8,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = TextMuted }
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                padding = new RectOffset(6, 6, 3, 3)
            };
        }

        // -------------------------------------------------------------------
        // OnGUI
        // -------------------------------------------------------------------
        private void OnGUI()
        {
            InitStyles();

            // Full window background
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), BgLight);

            float bottomY = position.height - BOTTOM_BAR_HEIGHT;

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

            // Left Panel
            Rect leftRect = new(0, 0, LEFT_PANEL_WIDTH, bottomY);
            GUILayout.BeginArea(leftRect);
            DrawLeftPanel(leftRect);
            GUILayout.EndArea();

            // Left separator
            EditorGUI.DrawRect(new Rect(LEFT_PANEL_WIDTH, 0, 1, bottomY), SeparatorColor);

            // Right Panel (flowchart)
            float rightX = position.width - RIGHT_PANEL_WIDTH;
            Rect rightRect = new(rightX, 0, RIGHT_PANEL_WIDTH, bottomY);
            GUILayout.BeginArea(rightRect);
            DrawRightPanel(rightRect);
            GUILayout.EndArea();

            // Right separator
            EditorGUI.DrawRect(new Rect(rightX - 1, 0, 1, bottomY), SeparatorColor);

            // Center Panel
            float centerX = LEFT_PANEL_WIDTH + 1;
            float centerW = rightX - centerX - 1;
            Rect centerRect = new(centerX, 0, centerW, bottomY);
            GUILayout.BeginArea(centerRect);
            DrawCenterPanel(centerW);
            GUILayout.EndArea();

            EditorGUILayout.EndHorizontal();

            // Bottom bar
            DrawBottomBar(bottomY);
        }

        // -------------------------------------------------------------------
        // Left Panel — Sequence List
        // -------------------------------------------------------------------
        private void DrawLeftPanel(Rect panelRect)
        {
            // Header
            Rect headerRect = new(0, 0, panelRect.width, 32);
            EditorGUI.DrawRect(headerRect, PastelLavender);
            GUI.Label(new Rect(8, 4, panelRect.width - 16, 24), "Sequences", _headerStyle);

            // Scroll area
            Rect scrollArea = new(0, 34, panelRect.width, panelRect.height - 34 - 38);
            _leftScroll = GUI.BeginScrollView(scrollArea, _leftScroll,
                new Rect(0, 0, panelRect.width - 16, GetSequenceListHeight()));

            var guids = AssetDatabase.FindAssets("t:TutorialSequence", new[] { SequenceFolder });
            float y = 4;
            float rowW = panelRect.width - 32;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var seq = AssetDatabase.LoadAssetAtPath<TutorialSequence>(path);
                if (seq == null) continue;

                bool isSelected = _selectedSequence == seq;
                float rowH = 32f;
                Rect rowRect = new(8, y, rowW, rowH);

                // Row background
                Color rowBg = isSelected ? RowSelected : RowDefault;
                DrawRoundedRect(rowRect, rowBg, 4);

                // Click the whole row to select
                if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                {
                    SelectSequence(seq);
                }

                // Sequence label — vertically centered
                string displayName = string.IsNullOrEmpty(seq.sequenceId) ? seq.name : seq.sequenceId;
                var labelStyle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal,
                    normal = { textColor = TextLight },
                    fontSize = 11,
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip
                };
                float badgeW = 28;
                float badgePad = 8;
                Rect labelRect = new(rowRect.x + 10, rowRect.y, rowRect.width - badgeW - badgePad - 14, rowH);
                GUI.Label(labelRect, displayName, labelStyle);

                // Instruction count badge — vertically centered
                int count = seq.instructions?.Count ?? 0;
                Rect badgeRect = new(rowRect.xMax - badgeW - 6, rowRect.y + (rowH - 18) / 2, badgeW, 18);
                DrawRoundedRect(badgeRect, PastelBlue, 9);
                GUI.Label(badgeRect, count.ToString(), _badgeStyle);

                y += rowH + 4;
            }

            GUI.EndScrollView();

            // New Sequence button
            Rect btnRect = new(8, panelRect.height - 34, panelRect.width - 16, 28);
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = PastelMint;
            if (GUI.Button(btnRect, "+ New Sequence", _buttonStyle))
            {
                CreateNewSequence();
            }
            GUI.backgroundColor = prevBg;
        }

        private float GetSequenceListHeight()
        {
            var guids = AssetDatabase.FindAssets("t:TutorialSequence", new[] { SequenceFolder });
            return guids.Length * 36f + 10;
        }

        private void SelectSequence(TutorialSequence seq)
        {
            _selectedSequence = seq;
            _selectedInstructionIndex = -1;
            Repaint();
        }

        private void CreateNewSequence()
        {
            if (!AssetDatabase.IsValidFolder(SequenceFolder))
            {
                string parent = System.IO.Path.GetDirectoryName(SequenceFolder)?.Replace('\\', '/');
                string folder = System.IO.Path.GetFileName(SequenceFolder);
                if (parent != null) AssetDatabase.CreateFolder(parent, folder);
            }

            int number = 1;
            string assetPath;
            do
            {
                assetPath = $"{SequenceFolder}/TutorialSequence_{number:D2}.asset";
                number++;
            } while (System.IO.File.Exists(assetPath));

            var seq = CreateInstance<TutorialSequence>();
            seq.sequenceId = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(seq, assetPath);
            AssetDatabase.SaveAssets();
            SelectSequence(seq);
            _hasUnsavedChanges = true;
        }

        // -------------------------------------------------------------------
        // Center Panel — Instruction Editor
        // -------------------------------------------------------------------
        private void DrawCenterPanel(float panelWidth)
        {
            // Header
            Rect headerRect = new(0, 0, panelWidth, 32);
            EditorGUI.DrawRect(headerRect, PastelBlue);
            var titleStyle = new GUIStyle(_headerStyle) { normal = { textColor = TextLight } };
            GUI.Label(new Rect(8, 4, panelWidth - 16, 24), "Instruction Editor", titleStyle);

            if (_selectedSequence == null)
            {
                var centeredStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 12,
                    normal = { textColor = TextMuted }
                };
                GUI.Label(new Rect(0, 100, panelWidth, 30), "Select a sequence to begin editing", centeredStyle);
                return;
            }

            // Sequence metadata
            float metaY = 38;
            float metaX = 12;
            float fieldW = panelWidth - 24;

            // Sequence ID
            GUI.Label(new Rect(metaX, metaY, 80, 18), "Sequence ID", _cardLabelStyle);
            metaY += 16;
            EditorGUI.BeginChangeCheck();
            string newId = EditorGUI.TextField(new Rect(metaX, metaY, fieldW, 20), _selectedSequence.sequenceId);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_selectedSequence, "Edit Sequence ID");
                _selectedSequence.sequenceId = newId;
                MarkDirty(_selectedSequence);
            }
            metaY += 24;

            // Description
            GUI.Label(new Rect(metaX, metaY, 120, 18), "Description", _cardLabelStyle);
            metaY += 16;
            EditorGUI.BeginChangeCheck();
            string newDesc = EditorGUI.TextField(new Rect(metaX, metaY, fieldW, 20), _selectedSequence.description);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_selectedSequence, "Edit Description");
                _selectedSequence.description = newDesc;
                MarkDirty(_selectedSequence);
            }
            metaY += 24;

            // Trigger Event
            GUI.Label(new Rect(metaX, metaY, 120, 18), "Sequence Trigger Event", _cardLabelStyle);
            metaY += 16;
            EditorGUI.BeginChangeCheck();
            var newTrigger = (ScriptableEventNoParam)EditorGUI.ObjectField(
                new Rect(metaX, metaY, fieldW, 18),
                _selectedSequence.triggerEvent, typeof(ScriptableEventNoParam), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_selectedSequence, "Change Trigger Event");
                _selectedSequence.triggerEvent = newTrigger;
                MarkDirty(_selectedSequence);
            }
            metaY += 22;

            // Completion Event
            GUI.Label(new Rect(metaX, metaY, 120, 18), "Completion Event", _cardLabelStyle);
            metaY += 16;
            EditorGUI.BeginChangeCheck();
            var newCompletion = (ScriptableEventNoParam)EditorGUI.ObjectField(
                new Rect(metaX, metaY, fieldW, 18),
                _selectedSequence.completionEvent, typeof(ScriptableEventNoParam), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_selectedSequence, "Change Completion Event");
                _selectedSequence.completionEvent = newCompletion;
                MarkDirty(_selectedSequence);
            }
            metaY += 26;

            // Separator
            EditorGUI.DrawRect(new Rect(metaX, metaY, fieldW, 1), SeparatorColor);
            metaY += 6;

            // Section label
            GUI.Label(new Rect(metaX, metaY, 200, 20), "Instructions", _sectionStyle);
            metaY += 22;

            // Instructions scroll area
            float scrollTop = metaY;
            float scrollHeight = position.height - BOTTOM_BAR_HEIGHT - scrollTop - 40;
            float contentHeight = GetInstructionsContentHeight(panelWidth);

            Rect scrollViewRect = new(0, scrollTop, panelWidth, scrollHeight);
            Rect contentRect = new(0, 0, panelWidth - 20, contentHeight);
            _centerScroll = GUI.BeginScrollView(scrollViewRect, _centerScroll, contentRect);

            DrawInstructionCards(panelWidth - 20);

            GUI.EndScrollView();

            // Add instruction button
            float addBtnY = scrollTop + scrollHeight + 6;
            Rect addBtnRect = new(12, addBtnY, panelWidth - 24, 26);
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = PastelMint;
            if (GUI.Button(addBtnRect, "+ Add Instruction", _buttonStyle))
            {
                Undo.RecordObject(_selectedSequence, "Add Instruction");
                _selectedSequence.instructions.Add(new TutorialInstruction());
                MarkDirty(_selectedSequence);
            }
            GUI.backgroundColor = prevBg;
        }

        private float GetInstructionsContentHeight(float panelWidth)
        {
            if (_selectedSequence == null) return 100;
            int count = _selectedSequence.instructions.Count;
            // Each card is approximately 220px tall + gap
            return count * (220f + CARD_GAP) + 10;
        }

        private void DrawInstructionCards(float availableWidth)
        {
            if (_selectedSequence == null) return;

            float y = 4;
            float cardWidth = availableWidth - 24;
            int deleteIndex = -1;
            int moveUpIndex = -1;
            int moveDownIndex = -1;

            for (int i = 0; i < _selectedSequence.instructions.Count; i++)
            {
                var inst = _selectedSequence.instructions[i];
                bool isSelected = i == _selectedInstructionIndex;

                float cardHeight = 210f;
                Rect cardRect = new(12, y, cardWidth, cardHeight);

                // Card background with border
                DrawRoundedRect(new Rect(cardRect.x - 1, cardRect.y - 1, cardRect.width + 2, cardRect.height + 2), CardBorder, 6);
                DrawRoundedRect(cardRect, isSelected ? CardSelected : CardBg, 5);

                // Select card on mouse down — but only during Layout/Repaint to avoid
                // consuming events that EditorGUI controls need (TextArea, ObjectField, etc.)
                if (Event.current.type == EventType.MouseDown && cardRect.Contains(Event.current.mousePosition))
                {
                    _selectedInstructionIndex = i;
                    // Do NOT call Event.current.Use() — let child controls handle input
                    Repaint();
                }

                float cx = cardRect.x + CARD_PADDING;
                float cy = cardRect.y + CARD_PADDING;
                float cw = cardRect.width - CARD_PADDING * 2;
                float lineH = EditorGUIUtility.singleLineHeight;

                // --- Row 1: Step badge + SOAP event badge + Move/Delete ---
                // Step number circle
                Rect stepBadge = new(cx, cy, 28, 20);
                DrawRoundedRect(stepBadge, PastelBlue, 10);
                var stepStyle = new GUIStyle(_badgeStyle) { normal = { textColor = TextWhite }, fontStyle = FontStyle.Bold };
                GUI.Label(stepBadge, $"#{i + 1}", stepStyle);

                // SOAP event badge
                float badgeX = cx + 34;
                if (inst.waitForEvent != null)
                {
                    string evtName = inst.waitForEvent.name;
                    if (evtName.Length > 18) evtName = evtName[..18] + "..";
                    Rect evtBadge = new(badgeX, cy, 140, 20);
                    DrawRoundedRect(evtBadge, PastelPeach, 10);
                    GUI.Label(evtBadge, $"\u23F3 {evtName}", _badgeStyle);
                }
                else
                {
                    Rect autoBadge = new(badgeX, cy, 80, 20);
                    DrawRoundedRect(autoBadge, PastelMint, 10);
                    GUI.Label(autoBadge, "AUTO-PLAY", _badgeStyle);
                }

                // Move Up / Move Down / Delete buttons
                float btnW = 22;
                float btnY = cy;
                float btnX = cx + cw - btnW;

                // Delete
                var prevBg2 = GUI.backgroundColor;
                GUI.backgroundColor = PastelPink;
                if (GUI.Button(new Rect(btnX, btnY, btnW, 20), "\u2716", _buttonStyle))
                    deleteIndex = i;
                GUI.backgroundColor = prevBg2;
                btnX -= btnW + 2;

                // Move Down
                GUI.enabled = i < _selectedSequence.instructions.Count - 1;
                if (GUI.Button(new Rect(btnX, btnY, btnW, 20), "\u25BC", _buttonStyle))
                    moveDownIndex = i;
                GUI.enabled = true;
                btnX -= btnW + 2;

                // Move Up
                GUI.enabled = i > 0;
                if (GUI.Button(new Rect(btnX, btnY, btnW, 20), "\u25B2", _buttonStyle))
                    moveUpIndex = i;
                GUI.enabled = true;

                cy += 26;

                // --- Row 2: SOAP Event field ---
                GUI.Label(new Rect(cx, cy, 100, 14), "Wait for Event", _cardLabelStyle);
                cy += 14;
                EditorGUI.BeginChangeCheck();
                var newEvent = (ScriptableEventNoParam)EditorGUI.ObjectField(
                    new Rect(cx, cy, cw, lineH),
                    inst.waitForEvent, typeof(ScriptableEventNoParam), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_selectedSequence, "Change Instruction Event");
                    inst.waitForEvent = newEvent;
                    MarkDirty(_selectedSequence);
                }
                cy += lineH + 4;

                // --- Row 3: Dialogue Text ---
                GUI.Label(new Rect(cx, cy, 100, 14), "Dialogue Text", _cardLabelStyle);
                cy += 14;
                EditorGUI.BeginChangeCheck();
                string newText = EditorGUI.TextArea(new Rect(cx, cy, cw, lineH * 2), inst.dialogueText);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_selectedSequence, "Edit Dialogue Text");
                    inst.dialogueText = newText;
                    MarkDirty(_selectedSequence);
                }
                cy += lineH * 2 + 4;

                // --- Row 4: Avatar + Arrow Targets (two columns) ---
                float halfW = (cw - 8) / 2;

                GUI.Label(new Rect(cx, cy, halfW, 14), "Avatar Icon", _cardLabelStyle);
                GUI.Label(new Rect(cx + halfW + 8, cy, halfW, 14), "Arrow Targets", _cardLabelStyle);
                cy += 14;

                EditorGUI.BeginChangeCheck();
                var newAvatar = (Sprite)EditorGUI.ObjectField(
                    new Rect(cx, cy, halfW, lineH), inst.avatarIcon, typeof(Sprite), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_selectedSequence, "Change Avatar");
                    inst.avatarIcon = newAvatar;
                    MarkDirty(_selectedSequence);
                }

                EditorGUI.BeginChangeCheck();
                var newArrows = (ArrowTarget)EditorGUI.EnumFlagsField(
                    new Rect(cx + halfW + 8, cy, halfW, lineH), inst.arrowTargets);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_selectedSequence, "Change Arrows");
                    inst.arrowTargets = newArrows;
                    MarkDirty(_selectedSequence);
                }
                cy += lineH + 4;

                // --- Row 5: Audio + Auto-Advance ---
                GUI.Label(new Rect(cx, cy, halfW, 14), "Audio Clip", _cardLabelStyle);
                GUI.Label(new Rect(cx + halfW + 8, cy, halfW, 14), "Flow Control", _cardLabelStyle);
                cy += 14;

                EditorGUI.BeginChangeCheck();
                var newClip = (AudioClip)EditorGUI.ObjectField(
                    new Rect(cx, cy, halfW, lineH), inst.audioClip, typeof(AudioClip), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_selectedSequence, "Change Audio");
                    inst.audioClip = newClip;
                    MarkDirty(_selectedSequence);
                }

                // Auto-advance toggle
                EditorGUI.BeginChangeCheck();
                bool newAuto = EditorGUI.ToggleLeft(
                    new Rect(cx + halfW + 8, cy, 110, lineH), "Auto-Advance", inst.autoAdvance);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_selectedSequence, "Toggle Auto-Advance");
                    inst.autoAdvance = newAuto;
                    MarkDirty(_selectedSequence);
                }

                if (inst.autoAdvance)
                {
                    EditorGUI.BeginChangeCheck();
                    float newDelay = EditorGUI.FloatField(
                        new Rect(cx + halfW + 124, cy, halfW - 130, lineH), inst.delayBeforeNext);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_selectedSequence, "Change Delay");
                        inst.delayBeforeNext = Mathf.Max(0f, newDelay);
                        MarkDirty(_selectedSequence);
                    }
                    GUI.Label(new Rect(cx + cw - 10, cy, 14, lineH), "s", _cardLabelStyle);
                }

                y += cardHeight + CARD_GAP;
            }

            // Process deferred actions
            if (deleteIndex >= 0)
            {
                Undo.RecordObject(_selectedSequence, "Delete Instruction");
                _selectedSequence.instructions.RemoveAt(deleteIndex);
                if (_selectedInstructionIndex >= _selectedSequence.instructions.Count)
                    _selectedInstructionIndex = _selectedSequence.instructions.Count - 1;
                MarkDirty(_selectedSequence);
            }
            if (moveUpIndex > 0)
            {
                Undo.RecordObject(_selectedSequence, "Move Instruction Up");
                (_selectedSequence.instructions[moveUpIndex], _selectedSequence.instructions[moveUpIndex - 1]) =
                    (_selectedSequence.instructions[moveUpIndex - 1], _selectedSequence.instructions[moveUpIndex]);
                _selectedInstructionIndex = moveUpIndex - 1;
                MarkDirty(_selectedSequence);
            }
            if (moveDownIndex >= 0 && moveDownIndex < _selectedSequence.instructions.Count - 1)
            {
                Undo.RecordObject(_selectedSequence, "Move Instruction Down");
                (_selectedSequence.instructions[moveDownIndex], _selectedSequence.instructions[moveDownIndex + 1]) =
                    (_selectedSequence.instructions[moveDownIndex + 1], _selectedSequence.instructions[moveDownIndex]);
                _selectedInstructionIndex = moveDownIndex + 1;
                MarkDirty(_selectedSequence);
            }
        }

        // -------------------------------------------------------------------
        // Right Panel — Flowchart Preview / Instruction Preview
        // -------------------------------------------------------------------
        private void DrawRightPanel(Rect panelRect)
        {
            // Header
            Rect headerRect = new(0, 0, panelRect.width, 32);
            EditorGUI.DrawRect(headerRect, PastelLavender);
            string title = _showInstructionPreview ? "Instruction Preview" : "Sequence Flow";
            var titleStyle = new GUIStyle(_headerStyle) { normal = { textColor = TextWhite } };
            GUI.Label(new Rect(8, 4, panelRect.width - 16, 24), title, titleStyle);

            if (_selectedSequence == null)
            {
                var centeredStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    normal = { textColor = TextMuted }
                };
                GUI.Label(new Rect(0, 80, panelRect.width, 20), "No sequence selected", centeredStyle);
                return;
            }

            // Mode toggle button at bottom
            float toggleBtnH = 26;
            float toggleBtnY = panelRect.height - toggleBtnH - 6;
            Rect toggleBtnRect = new(8, toggleBtnY, panelRect.width - 16, toggleBtnH);
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = _showInstructionPreview ? PastelBlue : PastelLavender;
            string toggleLabel = _showInstructionPreview ? "Back to Sequence Flow" : "Switch to Preview";
            if (GUI.Button(toggleBtnRect, toggleLabel, _buttonStyle))
            {
                _showInstructionPreview = !_showInstructionPreview;
                Repaint();
            }
            GUI.backgroundColor = prevBg;

            // Content area (between header and toggle button)
            float contentTop = 36;
            float contentHeight = toggleBtnY - contentTop - 4;

            if (_showInstructionPreview)
                DrawInstructionPreview(panelRect.width, contentTop, contentHeight);
            else
                DrawFlowchartContent(panelRect, contentTop, contentHeight);
        }

        private void DrawInstructionPreview(float panelWidth, float top, float height)
        {
            if (_selectedInstructionIndex < 0 || _selectedInstructionIndex >= _selectedSequence.instructions.Count)
            {
                var centeredStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 11,
                    normal = { textColor = TextMuted }
                };
                GUI.Label(new Rect(0, top + 40, panelWidth, 20), "Select an instruction to preview", centeredStyle);
                return;
            }

            var inst = _selectedSequence.instructions[_selectedInstructionIndex];
            float pad = 10;
            float fieldW = panelWidth - pad * 2;
            float lineH = EditorGUIUtility.singleLineHeight;

            // Calculate content height for scroll
            float contentH = 420;
            Rect scrollArea = new(0, top, panelWidth, height);
            Rect contentArea = new(0, 0, panelWidth - 16, contentH);
            _previewScroll = GUI.BeginScrollView(scrollArea, _previewScroll, contentArea);

            float y = 8;

            // Step badge
            Rect stepBadge = new(pad, y, 60, 20);
            DrawRoundedRect(stepBadge, PastelBlue, 10);
            var stepStyle = new GUIStyle(_badgeStyle) { normal = { textColor = TextWhite }, fontStyle = FontStyle.Bold };
            GUI.Label(stepBadge, $"Step {_selectedInstructionIndex + 1}", stepStyle);
            y += 28;

            // Avatar preview
            GUI.Label(new Rect(pad, y, fieldW, 14), "Avatar Icon", _cardLabelStyle);
            y += 16;
            if (inst.avatarIcon != null)
            {
                float previewSize = Mathf.Min(fieldW, 80);
                Rect previewRect = new(pad, y, previewSize, previewSize);
                DrawRoundedRect(new Rect(previewRect.x - 1, previewRect.y - 1, previewSize + 2, previewSize + 2), CardBorder, 4);
                GUI.DrawTexture(previewRect, inst.avatarIcon.texture, ScaleMode.ScaleToFit);
                y += previewSize + 6;
            }
            else
            {
                Rect placeholderRect = new(pad, y, 80, 80);
                DrawRoundedRect(placeholderRect, CardBorder, 4);
                var placeholderStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { normal = { textColor = TextMuted } };
                GUI.Label(placeholderRect, "No Avatar", placeholderStyle);
                y += 86;
            }

            // Dialogue text preview
            GUI.Label(new Rect(pad, y, fieldW, 14), "Dialogue Text", _cardLabelStyle);
            y += 16;
            Rect textBgRect = new(pad, y, fieldW, 50);
            DrawRoundedRect(textBgRect, new Color(0.18f, 0.18f, 0.21f, 1f), 4);
            var textStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                normal = { textColor = inst.textColor },
                padding = new RectOffset(6, 6, 4, 4),
                fontSize = 11
            };
            string displayText = string.IsNullOrEmpty(inst.dialogueText) ? "(empty)" : inst.dialogueText;
            GUI.Label(new Rect(pad + 2, y + 2, fieldW - 4, 46), displayText, textStyle);
            y += 56;

            // Separator
            EditorGUI.DrawRect(new Rect(pad, y, fieldW, 1), SeparatorColor);
            y += 8;

            // Text Animation Type
            GUI.Label(new Rect(pad, y, fieldW, 14), "Text Animation", _cardLabelStyle);
            y += 16;
            EditorGUI.BeginChangeCheck();
            var newAnimType = (TextAnimationType)EditorGUI.EnumPopup(
                new Rect(pad, y, fieldW, lineH), inst.textAnimationType);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_selectedSequence, "Change Text Animation Type");
                inst.textAnimationType = newAnimType;
                MarkDirty(_selectedSequence);
            }
            y += lineH + 6;

            // Text Animation Speed
            GUI.Label(new Rect(pad, y, fieldW, 14), "Animation Speed", _cardLabelStyle);
            y += 16;
            EditorGUI.BeginChangeCheck();
            float newSpeed = EditorGUI.Slider(new Rect(pad, y, fieldW, lineH), inst.textAnimationSpeed, 0.1f, 10f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_selectedSequence, "Change Text Animation Speed");
                inst.textAnimationSpeed = newSpeed;
                MarkDirty(_selectedSequence);
            }
            y += lineH + 6;

            // Text Color
            GUI.Label(new Rect(pad, y, fieldW, 14), "Text Color", _cardLabelStyle);
            y += 16;
            EditorGUI.BeginChangeCheck();
            Color newColor = EditorGUI.ColorField(new Rect(pad, y, fieldW, lineH), inst.textColor);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_selectedSequence, "Change Text Color");
                inst.textColor = newColor;
                MarkDirty(_selectedSequence);
            }

            GUI.EndScrollView();
        }

        private void DrawFlowchartContent(Rect panelRect, float top, float height)
        {
            float nodeW = panelRect.width - 40;
            float nodeH = 44;
            float nodeGap = 12;
            float arrowH = 16;
            int count = _selectedSequence.instructions.Count;

            float totalHeight = (count + 2) * (nodeH + arrowH + nodeGap) + 20;

            Rect scrollArea = new(0, top, panelRect.width, height);
            Rect contentArea = new(0, 0, panelRect.width - 16, totalHeight);
            _flowScroll = GUI.BeginScrollView(scrollArea, _flowScroll, contentArea);

            float x = 20;
            float y = 10;

            // START node
            Rect startRect = new(x, y, nodeW, 30);
            DrawRoundedRect(startRect, FlowStartNode, 4);
            string startLabel = _selectedSequence.triggerEvent != null
                ? $"START: {_selectedSequence.triggerEvent.name}"
                : "START (no trigger)";
            GUI.Label(startRect, startLabel, _flowNodeStyle);
            y += 30;

            DrawFlowArrow(x + nodeW / 2, y, arrowH);
            y += arrowH + 4;

            for (int i = 0; i < count; i++)
            {
                var inst = _selectedSequence.instructions[i];
                bool isSelected = i == _selectedInstructionIndex;
                bool hasEvent = inst.waitForEvent != null;

                Rect nodeRect = new(x, y, nodeW, nodeH);

                Color nodeBg = isSelected ? FlowNodeSelected : (hasEvent ? FlowNodeEvent : FlowNodeAuto);
                DrawRoundedRect(nodeRect, nodeBg, 4);

                // Click to select + auto-switch to preview
                if (Event.current.type == EventType.MouseDown && nodeRect.Contains(Event.current.mousePosition))
                {
                    _selectedInstructionIndex = i;
                    _showInstructionPreview = true;
                    _centerScroll.y = i * (210f + CARD_GAP);
                    Event.current.Use();
                    Repaint();
                }

                Rect numRect = new(x + 4, y + 2, nodeW - 8, 14);
                var numStyle = new GUIStyle(_flowLabelStyle) { fontStyle = FontStyle.Bold, normal = { textColor = TextWhite } };
                string stepNum = $"Step {i + 1}";
                if (hasEvent) stepNum += " \u23F3";
                GUI.Label(numRect, stepNum, numStyle);

                string currentLabel = inst.stepLabel;
                if (string.IsNullOrEmpty(currentLabel))
                {
                    currentLabel = !string.IsNullOrEmpty(inst.dialogueText)
                        ? (inst.dialogueText.Length > 25 ? inst.dialogueText[..25] + ".." : inst.dialogueText)
                        : "(empty)";
                }

                Rect labelRect = new(x + 4, y + 16, nodeW - 8, 16);
                EditorGUI.BeginChangeCheck();
                string newLabel = EditorGUI.TextField(labelRect, inst.stepLabel, _flowNodeStyle);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_selectedSequence, "Edit Step Label");
                    inst.stepLabel = newLabel;
                    MarkDirty(_selectedSequence);
                }

                if (string.IsNullOrEmpty(inst.stepLabel))
                {
                    var placeholderStyle = new GUIStyle(_flowNodeStyle)
                    {
                        normal = { textColor = TextMuted },
                        fontStyle = FontStyle.Italic
                    };
                    GUI.Label(labelRect, currentLabel, placeholderStyle);
                }

                if (hasEvent)
                {
                    Rect evtRect = new(x, y + nodeH - 12, nodeW, 12);
                    var evtStyle = new GUIStyle(_flowLabelStyle)
                    {
                        fontSize = 8,
                        normal = { textColor = new Color(0.75f, 0.50f, 0.35f) }
                    };
                    GUI.Label(evtRect, $"waits: {inst.waitForEvent.name}", evtStyle);
                }

                y += nodeH;

                if (i < count - 1)
                {
                    DrawFlowArrow(x + nodeW / 2, y, arrowH);
                    y += arrowH + nodeGap;
                }
                else
                {
                    y += 4;
                }
            }

            DrawFlowArrow(x + nodeW / 2, y, arrowH);
            y += arrowH + 4;

            Rect endRect = new(x, y, nodeW, 30);
            DrawRoundedRect(endRect, FlowEndNode, 4);
            string endLabel = _selectedSequence.completionEvent != null
                ? $"END: {_selectedSequence.completionEvent.name}"
                : "END";
            GUI.Label(endRect, endLabel, _flowNodeStyle);

            GUI.EndScrollView();
        }

        private void DrawFlowArrow(float centerX, float topY, float height)
        {
            // Vertical line
            float lineW = 2;
            EditorGUI.DrawRect(new Rect(centerX - lineW / 2, topY, lineW, height - 4), FlowLine);

            // Arrowhead (small triangle)
            float arrowSize = 6;
            float arrowY = topY + height - 4;

            // Draw arrowhead as 3 small rects approximating a triangle
            for (int j = 0; j < 3; j++)
            {
                float w = arrowSize - j * 2;
                EditorGUI.DrawRect(new Rect(centerX - w / 2, arrowY + j * 2, w, 2), FlowLine);
            }
        }

        // -------------------------------------------------------------------
        // Bottom Bar
        // -------------------------------------------------------------------
        private void DrawBottomBar(float topY)
        {
            Rect barRect = new(0, topY, position.width, BOTTOM_BAR_HEIGHT);
            EditorGUI.DrawRect(barRect, BgMedium);
            EditorGUI.DrawRect(new Rect(0, topY, position.width, 1), SeparatorColor);

            // Status text
            if (_selectedSequence != null)
            {
                int count = _selectedSequence.instructions?.Count ?? 0;
                string info = $"{_selectedSequence.sequenceId}  |  {count} instruction{(count != 1 ? "s" : "")}";
                var infoStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = TextMuted } };
                GUI.Label(new Rect(12, topY + 8, 300, 20), info, infoStyle);
            }

            // Save button
            if (_hasUnsavedChanges)
            {
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = PastelMint;
                if (GUI.Button(new Rect(position.width - 112, topY + 5, 100, 26), "Save All", _buttonStyle))
                {
                    AssetDatabase.SaveAssets();
                    _hasUnsavedChanges = false;
                }
                GUI.backgroundColor = prevBg;
            }

            // Delete sequence button
            if (_selectedSequence != null)
            {
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = PastelPink;
                if (GUI.Button(new Rect(position.width - 230, topY + 5, 110, 26), "Delete Sequence", _buttonStyle))
                {
                    string path = AssetDatabase.GetAssetPath(_selectedSequence);
                    if (EditorUtility.DisplayDialog("Delete Sequence",
                        $"Delete '{_selectedSequence.sequenceId}'?\n\nThis cannot be undone.", "Delete", "Cancel"))
                    {
                        AssetDatabase.DeleteAsset(path);
                        AssetDatabase.SaveAssets();
                        _selectedSequence = null;
                        _selectedInstructionIndex = -1;
                        Repaint();
                    }
                }
                GUI.backgroundColor = prevBg;
            }
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------
        private void MarkDirty(Object obj)
        {
            EditorUtility.SetDirty(obj);
            _hasUnsavedChanges = true;
            Repaint();
        }

        private static void DrawRoundedRect(Rect rect, Color color, float radius)
        {
            // IMGUI doesn't have built-in rounded rects, so we approximate
            // Main body
            EditorGUI.DrawRect(new Rect(rect.x + radius, rect.y, rect.width - radius * 2, rect.height), color);
            // Left/right strips
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + radius, radius, rect.height - radius * 2), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - radius, rect.y + radius, radius, rect.height - radius * 2), color);
            // Corner fills (small squares — not truly rounded but close enough for editor UI)
            EditorGUI.DrawRect(new Rect(rect.x + 1, rect.y + 1, radius - 1, radius - 1), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - radius, rect.y + 1, radius - 1, radius - 1), color);
            EditorGUI.DrawRect(new Rect(rect.x + 1, rect.yMax - radius, radius - 1, radius - 1), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - radius, rect.yMax - radius, radius - 1, radius - 1), color);
        }
    }
}
#endif
