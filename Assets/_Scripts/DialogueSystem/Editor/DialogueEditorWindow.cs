#if !LINUX_BUILD
using CosmicShore.DialogueSystem.Models;
using System.Collections.Generic;
using Obvious.Soap;
using UnityEditor;
using UnityEditorInternal;
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
        private Vector2 _mainScroll;
        private bool _hasUnsavedChanges;
        private ReorderableList _instructionsList;
        private TutorialSequence _listTarget;
        private readonly HashSet<int> _collapsedCards = new();

        // Layout
        private const float LEFT_PANEL_WIDTH = 260f;
        private const float BOTTOM_BAR_HEIGHT = 38f;
        private const string DELETE_GLYPH = "\u2716";

        private static readonly string SequenceFolder = "Assets/_Scripts/DialogueSystem/SO";

        // Colors
        private static readonly Color HeaderBg = new(0.14f, 0.14f, 0.18f, 1f);
        private static readonly Color AccentTeal = new(0.18f, 0.78f, 0.78f, 1f);
        private static readonly Color AccentGreen = new(0.24f, 1f, 0.71f, 0.25f);
        private static readonly Color CardBgEven = new(0.20f, 0.22f, 0.28f, 1f);
        private static readonly Color CardBgOdd = new(0.17f, 0.19f, 0.24f, 1f);
        private static readonly Color CardBgSelected = new(0.28f, 0.34f, 0.52f, 1f);
        private static readonly Color RowDefault = new(0.22f, 0.24f, 0.30f, 1f);
        private static readonly Color RowSelected = new(0.30f, 0.50f, 0.70f, 1f);
        private static readonly Color DeleteRed = new(0.93f, 0.36f, 0.36f, 1f);
        private static readonly Color SeparatorColor = new(0.3f, 0.3f, 0.3f, 1f);

        [MenuItem("FrogletTools/Tutorial Sequence Editor")]
        public static void Open()
        {
            var window = GetWindow<DialogueEditorWindow>("Tutorial Sequence Editor");
            window.minSize = new Vector2(800, 500);
        }

        // -------------------------------------------------------------------
        // OnGUI
        // -------------------------------------------------------------------
        private void OnGUI()
        {
            GUILayout.Space(4);
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

            // Left Panel
            EditorGUILayout.BeginVertical(GUILayout.Width(LEFT_PANEL_WIDTH));
            DrawLeftPanel();
            EditorGUILayout.EndVertical();

            // Separator
            Rect sep = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.Width(1), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(sep, SeparatorColor);
            GUILayout.Space(8);

            // Main Panel
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawMainPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            DrawBottomBar();
        }

        // -------------------------------------------------------------------
        // Left Panel — Sequence List
        // -------------------------------------------------------------------
        private void DrawLeftPanel()
        {
            // Header
            Rect headerRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.boldLabel,
                GUILayout.Height(28), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(headerRect, HeaderBg);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = AccentTeal }
            };
            EditorGUI.LabelField(headerRect, "  Tutorial Sequences", headerStyle);

            GUILayout.Space(6);
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            var guids = AssetDatabase.FindAssets("t:TutorialSequence", new[] { SequenceFolder });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var seq = AssetDatabase.LoadAssetAtPath<TutorialSequence>(path);
                if (seq == null) continue;

                bool isSelected = _selectedSequence == seq;
                float rowHeight = 32f;
                Rect rowRect = GUILayoutUtility.GetRect(1, rowHeight, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(rowRect, isSelected ? RowSelected : RowDefault);

                // Delete button
                float iconSize = 18f;
                float pad = 6f;
                Rect deleteRect = new(rowRect.x + pad, rowRect.y + (rowHeight - iconSize) / 2, iconSize, iconSize);
                var deleteStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = DeleteRed }
                };
                if (GUI.Button(deleteRect, DELETE_GLYPH, deleteStyle))
                {
                    if (EditorUtility.DisplayDialog("Delete Tutorial Sequence",
                        $"Delete '{seq.sequenceId}'?\n\nThis cannot be undone.", "Delete", "Cancel"))
                    {
                        AssetDatabase.DeleteAsset(path);
                        AssetDatabase.SaveAssets();
                        if (_selectedSequence == seq)
                        {
                            _selectedSequence = null;
                            _instructionsList = null;
                        }
                        GUIUtility.ExitGUI();
                    }
                }

                // Label with instruction count badge
                float labelX = deleteRect.xMax + pad;
                float badgeWidth = 28f;
                float labelWidth = rowRect.xMax - labelX - badgeWidth - pad * 2;
                Rect labelRect = new(labelX, rowRect.y + 2, labelWidth, rowHeight - 4);

                string displayName = string.IsNullOrEmpty(seq.sequenceId) ? seq.name : seq.sequenceId;
                var labelStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal,
                    normal = { textColor = isSelected ? Color.white : new Color(0.8f, 0.8f, 0.85f) }
                };
                if (GUI.Button(labelRect, displayName, labelStyle))
                {
                    _selectedSequence = seq;
                    _selectedInstructionIndex = -1;
                    _instructionsList = null;
                    _collapsedCards.Clear();
                    GUIUtility.ExitGUI();
                }

                // Instruction count badge
                int count = seq.instructions?.Count ?? 0;
                Rect badgeRect = new(rowRect.xMax - badgeWidth - pad, rowRect.y + (rowHeight - 18) / 2, badgeWidth, 18);
                EditorGUI.DrawRect(badgeRect, new Color(0.1f, 0.1f, 0.14f, 0.8f));
                var badgeStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    normal = { textColor = AccentTeal },
                    fontSize = 10
                };
                EditorGUI.LabelField(badgeRect, count.ToString(), badgeStyle);

                GUILayout.Space(1);
            }

            EditorGUILayout.EndScrollView();

            GUILayout.Space(4);
            if (GUILayout.Button("+ New Sequence", GUILayout.Height(28)))
            {
                CreateNewSequence();
            }
            GUILayout.Space(4);
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
            _selectedSequence = seq;
            _selectedInstructionIndex = -1;
            _instructionsList = null;
            _collapsedCards.Clear();
            _hasUnsavedChanges = true;
            GUIUtility.ExitGUI();
        }

        // -------------------------------------------------------------------
        // Main Panel — Sequence Editor
        // -------------------------------------------------------------------
        private void DrawMainPanel()
        {
            // Header
            Rect headerRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.boldLabel,
                GUILayout.Height(28), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(headerRect, AccentGreen);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            EditorGUI.LabelField(headerRect, "  Sequence Editor", headerStyle);

            GUILayout.Space(6);

            if (_selectedSequence == null)
            {
                EditorGUILayout.LabelField("Select a tutorial sequence to begin editing.",
                    EditorStyles.centeredGreyMiniLabel, GUILayout.ExpandHeight(true));
                return;
            }

            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);

            DrawSequenceHeader();
            GUILayout.Space(10);
            DrawInstructionsList();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSequenceHeader()
        {
            var seq = _selectedSequence;

            // Sequence ID
            EditorGUI.BeginChangeCheck();
            string newId = EditorGUILayout.TextField("Sequence ID", seq.sequenceId);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(seq, "Edit Sequence ID");
                seq.sequenceId = newId;
                MarkDirty(seq);
            }

            // Description
            EditorGUI.BeginChangeCheck();
            string newDesc = EditorGUILayout.TextField("Description (Designer Notes)", seq.description);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(seq, "Edit Sequence Description");
                seq.description = newDesc;
                MarkDirty(seq);
            }

            GUILayout.Space(6);

            // SOAP Events section
            DrawSectionLabel("SOAP Event Triggers");

            EditorGUI.BeginChangeCheck();
            var newTrigger = (ScriptableEventNoParam)EditorGUILayout.ObjectField(
                "Trigger Event", seq.triggerEvent, typeof(ScriptableEventNoParam), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(seq, "Change Trigger Event");
                seq.triggerEvent = newTrigger;
                MarkDirty(seq);
            }

            EditorGUI.BeginChangeCheck();
            bool newAutoPlay = EditorGUILayout.Toggle("Auto-Play Next Sequence", seq.autoPlayNext);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(seq, "Toggle Auto-Play Next");
                seq.autoPlayNext = newAutoPlay;
                MarkDirty(seq);
            }

            if (seq.autoPlayNext)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                var newCompletion = (ScriptableEventNoParam)EditorGUILayout.ObjectField(
                    "Completion Event", seq.completionEvent, typeof(ScriptableEventNoParam), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(seq, "Change Completion Event");
                    seq.completionEvent = newCompletion;
                    MarkDirty(seq);
                }
                EditorGUI.indentLevel--;
            }
        }

        // -------------------------------------------------------------------
        // Instructions List — ReorderableList with card-style elements
        // -------------------------------------------------------------------
        private void DrawInstructionsList()
        {
            var seq = _selectedSequence;
            if (seq == null) return;

            if (_instructionsList == null || _listTarget != seq)
            {
                _listTarget = seq;
                BuildReorderableList(seq);
            }

            DrawSectionLabel("Instructions (Linear Flow)");
            GUILayout.Space(4);

            float listHeight = _instructionsList.GetHeight();
            Rect listRect = EditorGUILayout.GetControlRect(false, listHeight);
            _instructionsList.DoList(listRect);
        }

        private void BuildReorderableList(TutorialSequence seq)
        {
            _instructionsList = new ReorderableList(seq.instructions, typeof(TutorialInstruction),
                true, true, true, true);

            // Header
            _instructionsList.drawHeaderCallback = rect =>
            {
                EditorGUI.DrawRect(new Rect(rect.x - 4, rect.y, rect.width + 8, rect.height),
                    new Color(0.12f, 0.14f, 0.18f, 0.9f));
                int count = seq.instructions.Count;
                EditorGUI.LabelField(rect,
                    $"  {count} Instruction{(count != 1 ? "s" : "")} — drag to reorder",
                    EditorStyles.boldLabel);
            };

            // Element height (dynamic based on collapse state)
            _instructionsList.elementHeightCallback = idx =>
            {
                if (_collapsedCards.Contains(idx))
                    return 28f;
                return 160f;
            };

            // Background
            _instructionsList.drawElementBackgroundCallback = (rect, idx, isActive, isFocused) =>
            {
                if (idx < 0) return;
                Color bg;
                if (idx == _instructionsList.index)
                    bg = CardBgSelected;
                else
                    bg = idx % 2 == 0 ? CardBgEven : CardBgOdd;
                EditorGUI.DrawRect(rect, bg);
            };

            // Draw each instruction card
            _instructionsList.drawElementCallback = (rect, idx, isActive, isFocused) =>
            {
                if (idx < 0 || idx >= seq.instructions.Count) return;
                var inst = seq.instructions[idx];
                rect.y += 2;
                rect.height -= 4;
                float h = EditorGUIUtility.singleLineHeight;
                float pad = 4f;

                // --- Step number badge + collapse toggle ---
                bool collapsed = _collapsedCards.Contains(idx);
                Rect foldRect = new(rect.x, rect.y, 20, h);
                string foldIcon = collapsed ? "\u25B6" : "\u25BC"; // right/down triangle
                if (GUI.Button(foldRect, foldIcon, EditorStyles.miniLabel))
                {
                    if (collapsed) _collapsedCards.Remove(idx);
                    else _collapsedCards.Add(idx);
                }

                Rect badgeRect = new(rect.x + 22, rect.y, 40, h);
                var badgeStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = AccentTeal },
                    fontSize = 12
                };
                EditorGUI.LabelField(badgeRect, $"#{idx + 1}", badgeStyle);

                // Preview text on collapsed line
                if (collapsed)
                {
                    string preview = inst.dialogueText ?? "(empty)";
                    if (preview.Length > 60) preview = preview[..60] + "...";
                    Rect previewRect = new(rect.x + 66, rect.y, rect.width - 70, h);
                    EditorGUI.LabelField(previewRect, preview, EditorStyles.miniLabel);
                    return;
                }

                float y = rect.y + h + pad;
                float fieldWidth = rect.width - 10;

                // --- Dialogue Text (TextArea) ---
                Rect textLabelRect = new(rect.x + 4, y, 90, h);
                EditorGUI.LabelField(textLabelRect, "Dialogue Text", EditorStyles.miniLabel);
                y += h;
                Rect textRect = new(rect.x + 4, y, fieldWidth, h * 2.5f);
                EditorGUI.BeginChangeCheck();
                string newText = EditorGUI.TextArea(textRect, inst.dialogueText);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(seq, "Edit Instruction Text");
                    inst.dialogueText = newText;
                    MarkDirty(seq);
                }
                y += h * 2.5f + pad;

                // --- Row: Avatar Icon | Arrow Targets ---
                float halfWidth = (fieldWidth - 10) / 2;

                // Avatar Icon
                Rect avatarLabelRect = new(rect.x + 4, y, halfWidth, h);
                EditorGUI.LabelField(avatarLabelRect, "Avatar Icon", EditorStyles.miniLabel);
                Rect arrowLabelRect = new(rect.x + halfWidth + 14, y, halfWidth, h);
                EditorGUI.LabelField(arrowLabelRect, "Arrow Targets", EditorStyles.miniLabel);
                y += h;

                Rect avatarRect = new(rect.x + 4, y, halfWidth, h);
                EditorGUI.BeginChangeCheck();
                var newAvatar = (Sprite)EditorGUI.ObjectField(avatarRect, inst.avatarIcon, typeof(Sprite), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(seq, "Change Avatar Icon");
                    inst.avatarIcon = newAvatar;
                    MarkDirty(seq);
                }

                // Arrow Targets (flags enum)
                Rect arrowRect = new(rect.x + halfWidth + 14, y, halfWidth, h);
                EditorGUI.BeginChangeCheck();
                var newArrows = (ArrowTarget)EditorGUI.EnumFlagsField(arrowRect, inst.arrowTargets);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(seq, "Change Arrow Targets");
                    inst.arrowTargets = newArrows;
                    MarkDirty(seq);
                }
                y += h + pad;

                // --- Row: Audio Clip | Auto-Advance + Delay ---
                Rect audioLabelRect = new(rect.x + 4, y, halfWidth, h);
                EditorGUI.LabelField(audioLabelRect, "Audio Clip", EditorStyles.miniLabel);
                Rect flowLabelRect = new(rect.x + halfWidth + 14, y, halfWidth, h);
                EditorGUI.LabelField(flowLabelRect, "Flow Control", EditorStyles.miniLabel);
                y += h;

                Rect audioRect = new(rect.x + 4, y, halfWidth, h);
                EditorGUI.BeginChangeCheck();
                var newClip = (AudioClip)EditorGUI.ObjectField(audioRect, inst.audioClip, typeof(AudioClip), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(seq, "Change Audio Clip");
                    inst.audioClip = newClip;
                    MarkDirty(seq);
                }

                // Auto-advance toggle
                Rect autoRect = new(rect.x + halfWidth + 14, y, 100, h);
                EditorGUI.BeginChangeCheck();
                bool newAuto = EditorGUI.ToggleLeft(autoRect, "Auto-Advance", inst.autoAdvance);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(seq, "Toggle Auto-Advance");
                    inst.autoAdvance = newAuto;
                    MarkDirty(seq);
                }

                // Delay field (only when auto-advance is on)
                if (inst.autoAdvance)
                {
                    Rect delayRect = new(rect.x + halfWidth + 120, y, halfWidth - 110, h);
                    EditorGUI.BeginChangeCheck();
                    float newDelay = EditorGUI.FloatField(delayRect, inst.delayBeforeNext);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(seq, "Change Auto-Advance Delay");
                        inst.delayBeforeNext = Mathf.Max(0f, newDelay);
                        MarkDirty(seq);
                    }
                    Rect delayLabel = new(delayRect.xMax + 2, y, 20, h);
                    EditorGUI.LabelField(delayLabel, "s", EditorStyles.miniLabel);
                }
            };

            // Selection callback
            _instructionsList.onSelectCallback = list =>
            {
                _selectedInstructionIndex = list.index;
            };

            // Add callback — insert a new empty instruction
            _instructionsList.onAddCallback = list =>
            {
                Undo.RecordObject(seq, "Add Instruction");
                seq.instructions.Add(new TutorialInstruction());
                MarkDirty(seq);
            };

            // Remove callback
            _instructionsList.onRemoveCallback = list =>
            {
                if (list.index >= 0 && list.index < seq.instructions.Count)
                {
                    Undo.RecordObject(seq, "Remove Instruction");
                    seq.instructions.RemoveAt(list.index);
                    _collapsedCards.Remove(list.index);
                    MarkDirty(seq);
                }
            };

            // Reorder callback
            _instructionsList.onReorderCallback = list =>
            {
                Undo.RecordObject(seq, "Reorder Instructions");
                _collapsedCards.Clear();
                MarkDirty(seq);
            };
        }

        // -------------------------------------------------------------------
        // Bottom Bar
        // -------------------------------------------------------------------
        private void DrawBottomBar()
        {
            GUILayout.Space(2);
            EditorGUILayout.BeginHorizontal(GUILayout.Height(BOTTOM_BAR_HEIGHT));
            GUILayout.Space(8);

            // Info label
            if (_selectedSequence != null)
            {
                int count = _selectedSequence.instructions?.Count ?? 0;
                string info = $"{_selectedSequence.sequenceId}  |  {count} instruction{(count != 1 ? "s" : "")}";
                EditorGUILayout.LabelField(info, EditorStyles.miniLabel, GUILayout.Width(300));
            }

            GUILayout.FlexibleSpace();

            // Save button
            if (_hasUnsavedChanges)
            {
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
                if (GUILayout.Button("Save All", GUILayout.Width(100), GUILayout.Height(26)))
                {
                    AssetDatabase.SaveAssets();
                    _hasUnsavedChanges = false;
                }
                GUI.backgroundColor = prevBg;
            }

            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------
        private void MarkDirty(Object obj)
        {
            EditorUtility.SetDirty(obj);
            _hasUnsavedChanges = true;
        }

        private static void DrawSectionLabel(string text)
        {
            GUILayout.Space(4);
            Rect r = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.boldLabel,
                GUILayout.Height(22), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.16f, 0.18f, 0.22f, 0.8f));
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.7f, 0.8f, 0.9f) }
            };
            EditorGUI.LabelField(new Rect(r.x + 8, r.y, r.width - 8, r.height), text, style);
        }
    }
}
#endif
