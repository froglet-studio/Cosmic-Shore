using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// FrogletTools > Game Modes > Mode Map — every arcade card's launch-screen configuration on
    /// ONE screen, editable in place.
    ///
    /// <para>The data already lives in the right assets (that is the architecture, not a
    /// problem): the objective in the mode's <see cref="ModePreviewDefinitionSO"/>, the controls
    /// filters in <see cref="ModeControlsLibrarySO"/>, the seat counts on the
    /// <see cref="SO_ArcadeGame"/> card. What was missing is the CROSS-SECTION — seeing one
    /// mode's whole story at once, and seeing where two assets disagree. This window aggregates,
    /// flags, and edits; it never stores anything of its own, so it can never drift from the
    /// assets it shows.</para>
    ///
    /// <para>What it flags: an objective metric that disagrees with the mode's own
    /// <see cref="ScoringRuleSO"/> (the drift class that shipped Joust counting crystals), a
    /// metric with no icon in the shared table (the box then honestly draws no icon), and a
    /// previewable mode with no arcade card. Edits go through SerializedObjects — undo, dirty
    /// marking and prefab-safe writes for free — and every touched asset is recorded with the
    /// change ledger so Validate &amp; Push stages exactly what this window wrote.</para>
    ///
    /// <para>Deliberately read-only here: the spawn block (authored from the scenes by
    /// Tools/Build/author_preview_spawns.py — hand edits would be overwritten on the next run)
    /// and the ability rows themselves (derived from the vessel's ElementalAbilityMap; the whole
    /// point is that nobody authors them).</para>
    /// </summary>
    public class ModeMapWindow : EditorWindow
    {
        const string ToolName = "Mode Map";
        const string LibraryPath = "Assets/Resources/ModeControlsLibrary.asset";

        [MenuItem("FrogletTools/Game Modes/Mode Map")]
        [FrogletTool(FrogletToolCategory.GameModes, Importance = 4,
                     Description = "Every card's objective, icon, controls filters and seats on " +
                                   "one editable screen, with drift between assets flagged.")]
        static void Open()
        {
            var window = GetWindow<ModeMapWindow>("Mode Map");
            window.minSize = new Vector2(560, 400);
        }

        class ModeRow
        {
            public ModePreviewDefinitionSO Definition;
            public SerializedObject DefinitionSO;
            public SO_ArcadeGame Card;
            public SerializedObject CardSO;
            public ScoringRuleSO Rule;
        }

        readonly List<ModeRow> _rows = new();
        ModeControlsLibrarySO _library;
        SerializedObject _librarySO;
        FrogletToolShipContext _ship;

        Vector2 _scroll;
        string _search = string.Empty;
        bool _problemsOnly;
        bool _showIconTable = true;

        void OnEnable()
        {
            Build();
            _ship = new FrogletToolShipContext(ToolName)
            {
                CommitType = "feat",
                CommitScope = "arcade",
                Validate = ValidateForShip,
            };
        }

        // ── Data ─────────────────────────────────────────────────────────────────

        void Build()
        {
            _rows.Clear();

            _library = AssetDatabase.LoadAssetAtPath<ModeControlsLibrarySO>(LibraryPath);
            _librarySO = _library ? new SerializedObject(_library) : null;

            // One arcade card per mode (first found wins; the grid has legacy duplicates).
            var cards = new Dictionary<GameModes, SO_ArcadeGame>();
            foreach (var guid in AssetDatabase.FindAssets("t:SO_ArcadeGame"))
            {
                var card = AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (card && !cards.ContainsKey(card.Mode)) cards.Add(card.Mode, card);
            }

            // The mode's own scoring rule, matched by asset name. Two rules predate the mode
            // enum's Multiplayer* prefixes, hence the aliases.
            var rules = new Dictionary<GameModes, ScoringRuleSO>();
            var aliases = new Dictionary<string, GameModes>
            {
                { "CrystalCapture", GameModes.MultiplayerCrystalCapture },
                { "Joust", GameModes.MultiplayerJoust },
                { "CellularDuel", GameModes.MultiplayerCellularDuel },
            };
            foreach (var guid in AssetDatabase.FindAssets("t:ScoringRuleSO"))
            {
                var rule = AssetDatabase.LoadAssetAtPath<ScoringRuleSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (!rule) continue;
                var key = rule.name.Replace("ScoringRule", string.Empty);
                if (!aliases.TryGetValue(key, out var mode) &&
                    !Enum.TryParse(key, ignoreCase: false, out mode))
                    continue;
                if (!rules.ContainsKey(mode)) rules.Add(mode, rule);
            }

            // The previewable set IS the launch-screen set, so definitions drive the list.
            foreach (var guid in AssetDatabase.FindAssets("t:ModePreviewDefinitionSO"))
            {
                var definition = AssetDatabase.LoadAssetAtPath<ModePreviewDefinitionSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (!definition) continue;

                cards.TryGetValue(definition.Mode, out var card);
                rules.TryGetValue(definition.Mode, out var rule);
                _rows.Add(new ModeRow
                {
                    Definition = definition,
                    DefinitionSO = new SerializedObject(definition),
                    Card = card,
                    CardSO = card ? new SerializedObject(card) : null,
                    Rule = rule,
                });
            }

            _rows.Sort((a, b) => string.Compare(RowName(a), RowName(b), StringComparison.Ordinal));
        }

        static string RowName(ModeRow row)
            => row.Card && !string.IsNullOrEmpty(row.Card.DisplayName)
               ? row.Card.DisplayName : row.Definition.Mode.ToString();

        // ── Problems (shared by the pills, the filter, and ship validation) ──────

        bool MetricDisagreesWithRule(ModeRow row)
            => row.Rule && row.Definition.ObjectiveMetric != row.Rule.Metric;

        bool MetricHasNoIcon(ModeRow row)
            => _library && !_library.IconForMetric(row.Definition.ObjectiveMetric);

        bool HasProblem(ModeRow row)
            => MetricDisagreesWithRule(row) || MetricHasNoIcon(row) || !row.Card;

        FrogletToolValidation ValidateForShip()
        {
            int drift = 0, iconless = 0;
            foreach (var row in _rows)
            {
                if (MetricDisagreesWithRule(row)) drift++;
                if (MetricHasNoIcon(row)) iconless++;
            }
            if (drift > 0)
                return FrogletToolValidation.Fail(
                    $"{drift} mode(s) preview a metric their own scoring rule disagrees with — " +
                    "fix the ObjectiveMetric (or the rule) before shipping.");
            var note = iconless > 0 ? $" ({iconless} metric(s) draw no icon — allowed)" : string.Empty;
            return FrogletToolValidation.Pass($"{_rows.Count} modes consistent{note}.");
        }

        // ── GUI ──────────────────────────────────────────────────────────────────

        void OnGUI()
        {
            var accent = FrogletEditorPalette.ColorFor(FrogletToolCategory.GameModes);
            FrogletEditorPalette.Banner("Mode Map",
                "Every card's launch screen in one place — objective, icon, controls, seats.",
                accent);

            using (new EditorGUILayout.HorizontalScope())
            {
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                _problemsOnly = GUILayout.Toggle(_problemsOnly, "Problems only",
                                                 EditorStyles.toolbarButton, GUILayout.Width(96));
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    Build();
            }

            if (_library == null)
            {
                EditorGUILayout.HelpBox($"No ModeControlsLibrarySO at {LibraryPath}.",
                                        MessageType.Error);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawIconTable();
            foreach (var row in _rows)
            {
                if (_problemsOnly && !HasProblem(row)) continue;
                if (!string.IsNullOrEmpty(_search) &&
                    RowName(row).IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                DrawRow(row, accent);
            }
            EditorGUILayout.EndScrollView();

            FrogletToolShipPanel.Draw(_ship, this);
        }

        void DrawIconTable()
        {
            _showIconTable = EditorGUILayout.Foldout(_showIconTable,
                "Metric icons — one sprite per scoring metric, shared by the objective box and " +
                "the micro toast", toggleOnLabelClick: true);
            if (!_showIconTable) return;

            _librarySO.Update();
            EditorGUILayout.PropertyField(_librarySO.FindProperty("ObjectiveIcons"),
                                          new GUIContent("Objective Icons"), includeChildren: true);
            if (_librarySO.ApplyModifiedProperties()) RecordWrite(_library);
            FrogletEditorPalette.HorizontalRule();
        }

        void DrawRow(ModeRow row, Color accent)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawRowHeader(row);
                DrawObjective(row);
                DrawControlsEntry(row);
                DrawCardSeats(row);
            }
            var box = GUILayoutUtility.GetLastRect();
            FrogletEditorPalette.DrawAccentStripe(box, HasProblem(row)
                ? FrogletEditorPalette.Warn : accent);
        }

        void DrawRowHeader(ModeRow row)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"{RowName(row)}  ", FrogletEditorPalette.CardTitle,
                                GUILayout.ExpandWidth(false));
                GUILayout.Label($"{row.Definition.Mode} ({(int)row.Definition.Mode})",
                                FrogletEditorPalette.CardBody, GUILayout.ExpandWidth(false));
                GUILayout.FlexibleSpace();

                Pill(row.Card && row.Card.MinPlayersAllowed >= 2,
                     "sparring partner", FrogletEditorPalette.Info);
                Pill(MetricDisagreesWithRule(row),
                     row.Rule ? $"rule scores {row.Rule.Metric}" : string.Empty,
                     FrogletEditorPalette.Warn);
                Pill(MetricHasNoIcon(row), "no icon", FrogletEditorPalette.Warn);
                Pill(!row.Card, "no card", FrogletEditorPalette.Error);

                PingButton("Preview", row.Definition);
                PingButton("Card", row.Card);
            }
        }

        void DrawObjective(ModeRow row)
        {
            row.DefinitionSO.Update();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(row.DefinitionSO.FindProperty("ObjectiveMetric"),
                                              new GUIContent("Objective"));
                var sprite = _library.IconForMetric(row.Definition.ObjectiveMetric);
                var rect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20));
                if (sprite) GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit);
            }
            EditorGUILayout.PropertyField(row.DefinitionSO.FindProperty("ObjectiveText"),
                                          new GUIContent(" "));
            if (row.DefinitionSO.ApplyModifiedProperties()) RecordWrite(row.Definition);
        }

        void DrawControlsEntry(ModeRow row)
        {
            int index = LibraryEntryIndex(row.Definition.Mode);
            if (index < 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Controls: no library entry (all four abilities, card's hull)",
                                    FrogletEditorPalette.CardBody);
                    if (GUILayout.Button("Add entry", GUILayout.Width(80)))
                        AddLibraryEntry(row.Definition.Mode);
                }
                return;
            }

            _librarySO.Update();
            var entry = _librarySO.FindProperty("Entries").GetArrayElementAtIndex(index);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("Vessel"),
                                              new GUIContent("Controls hull"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("ShowAbilityRows"),
                                              new GUIContent("Abilities"), GUILayout.Width(160));
            }
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("Abilities"),
                                          new GUIContent("Element filter (empty = all)"),
                                          includeChildren: true);
            if (_librarySO.ApplyModifiedProperties()) RecordWrite(_library);
        }

        void DrawCardSeats(ModeRow row)
        {
            if (row.CardSO == null) return;

            row.CardSO.Update();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(row.CardSO.FindProperty("MinPlayersAllowed"),
                                              new GUIContent("Players min"));
                EditorGUILayout.PropertyField(row.CardSO.FindProperty("MaxPlayersAllowed"),
                                              new GUIContent("max"), GUILayout.Width(160));
                EditorGUILayout.PropertyField(row.CardSO.FindProperty("MinIntensity"),
                                              new GUIContent("Intensity min"));
                EditorGUILayout.PropertyField(row.CardSO.FindProperty("MaxIntensity"),
                                              new GUIContent("max"), GUILayout.Width(160));
            }
            if (row.CardSO.ApplyModifiedProperties()) RecordWrite(row.Card);
        }

        // ── Library entry management ─────────────────────────────────────────────

        int LibraryEntryIndex(GameModes mode)
        {
            var entries = _library.Entries;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].Mode == mode) return i;
            return -1;
        }

        void AddLibraryEntry(GameModes mode)
        {
            _librarySO.Update();
            var entries = _librarySO.FindProperty("Entries");
            entries.arraySize++;
            var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("Mode").intValue = (int)mode;
            entry.FindPropertyRelative("Rows").ClearArray();
            entry.FindPropertyRelative("ShowAbilityRows").boolValue = true;
            entry.FindPropertyRelative("Abilities").ClearArray();
            entry.FindPropertyRelative("Vessel").intValue = (int)VesselClassType.Any;
            _librarySO.ApplyModifiedProperties();
            RecordWrite(_library);
        }

        // ── Small pieces ─────────────────────────────────────────────────────────

        static void Pill(bool show, string label, Color color)
        {
            if (!show || string.IsNullOrEmpty(label)) return;
            var size = FrogletEditorPalette.Pill.CalcSize(new GUIContent(label));
            var rect = GUILayoutUtility.GetRect(size.x + 12, 16, GUILayout.ExpandWidth(false));
            FrogletEditorPalette.StatusPill(rect, label, color);
        }

        static void PingButton(string label, UnityEngine.Object asset)
        {
            using (new EditorGUI.DisabledScope(!asset))
                if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(56)))
                    EditorGUIUtility.PingObject(asset);
        }

        static void RecordWrite(UnityEngine.Object asset)
        {
            if (!asset) return;
            FrogletToolChangeLedger.Record(ToolName, AssetDatabase.GetAssetPath(asset));
        }
    }
}
