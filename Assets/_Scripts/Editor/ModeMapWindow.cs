using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using UnityEditor;
using UnityEngine;
using HintBinding = CosmicShore.UI.InputDeviceIconSetSwitcher.HintBinding;

namespace CosmicShore.Editor
{
    /// <summary>
    /// FrogletTools > Game Modes > Mode Map — the arcade launch screen's data, laid out THE WAY
    /// THE ARCADE PAGE LAYS IT OUT: pick a game on the left, and the right half reads as that
    /// card's page — OBJECTIVE (with the icon at a size you can actually see), VESSELS (one
    /// button per hull the card offers, each opening a detailed section of its four abilities
    /// and their controls), CONTROLS BLOCK (what the card lists, with the element filter as four
    /// plain toggles), SEATS &amp; INTENSITY, and the assets behind it all.
    ///
    /// <para>The window stores nothing of its own — every field edits the real asset through a
    /// SerializedObject (undo works), so it can never drift from what the game reads. Anything
    /// that would drift ANYWAY is said in plain sentences at the top of the page: an objective
    /// metric the mode's own scoring rule disagrees with, a metric with no icon, a mode with no
    /// card.</para>
    ///
    /// <para><b>Scan</b> re-reads the whole project — a vessel added yesterday shows up in the
    /// VESSELS section's "every vessel in the project" list, ready to be added to a card with
    /// one button.</para>
    ///
    /// <para>Deliberately read-only: the spawn block (authored from the scenes by
    /// Tools/Build/author_preview_spawns.py) and the ability rows themselves (derived from each
    /// vessel's ElementalAbilityMap — this window SHOWS that derivation per hull, which is the
    /// codex, but nobody authors the rows).</para>
    /// </summary>
    public class ModeMapWindow : EditorWindow
    {
        const string ToolName = "Mode Map";
        const string LibraryPath = "Assets/Resources/ModeControlsLibrary.asset";
        const float ListWidth = 200f;
        const float ObjectiveIconSize = 72f;
        const float VesselTileSize = 72f;

        static readonly Element[] ElementOrder =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        [MenuItem("FrogletTools/Game Modes/Mode Map")]
        [FrogletTool(FrogletToolCategory.GameModes, Importance = 4,
                     Description = "Every arcade card as a page: objective + icon, per-vessel " +
                                   "ability sections, controls filters, seats. Scan finds new " +
                                   "vessels; drift between assets is called out in plain words.")]
        static void Open()
        {
            var window = GetWindow<ModeMapWindow>("Mode Map");
            window.minSize = new Vector2(760, 480);
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
        readonly List<SO_Vessel> _allVessels = new();
        readonly Dictionary<ElementalAbilityMapSO, SerializedObject> _mapSOs = new();
        ModeControlsLibrarySO _library;
        SerializedObject _librarySO;
        ElementalBarsConfigSO _barsConfig;
        FrogletToolShipContext _ship;

        GameModes _selectedMode;
        VesselClassType _selectedVessel = VesselClassType.Any;
        Vector2 _listScroll, _pageScroll;
        string _search = string.Empty;
        bool _showAllVessels;
        bool _showIconTable;

        void OnEnable()
        {
            Scan();
            _ship = new FrogletToolShipContext(ToolName)
            {
                CommitType = "feat",
                CommitScope = "arcade",
                Validate = ValidateForShip,
            };
        }

        // ── Scan — re-read the whole project (modes, cards, rules, vessels) ──────

        void Scan()
        {
            _rows.Clear();
            _allVessels.Clear();
            _mapSOs.Clear();

            _library = AssetDatabase.LoadAssetAtPath<ModeControlsLibrarySO>(LibraryPath);
            _librarySO = _library ? new SerializedObject(_library) : null;

            // The element petal shapes - the HUD's own element language (element identity is
            // SHAPE, never colour), so an ability's element reads here exactly as in the game.
            _barsConfig = Resources.Load<ElementalBarsConfigSO>("ElementalBarsConfig");

            foreach (var guid in AssetDatabase.FindAssets("t:SO_Vessel"))
            {
                var vessel = AssetDatabase.LoadAssetAtPath<SO_Vessel>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (vessel) _allVessels.Add(vessel);
            }
            _allVessels.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

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
            if (FindRow(_selectedMode) == null && _rows.Count > 0)
                SelectMode(_rows[0]);
        }

        static string RowName(ModeRow row)
            => row.Card && !string.IsNullOrEmpty(row.Card.DisplayName)
               ? row.Card.DisplayName : row.Definition.Mode.ToString();

        ModeRow FindRow(GameModes mode)
        {
            foreach (var row in _rows)
                if (row.Definition.Mode == mode) return row;
            return null;
        }

        void SelectMode(ModeRow row)
        {
            _selectedMode = row.Definition.Mode;
            _selectedVessel = EffectiveHull(row);
        }

        // ── Problems, said once, shared by the pills / filter / ship validation ──

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
                    "fix the objective (or the rule) before shipping.");
            var note = iconless > 0 ? $" ({iconless} metric(s) draw no icon — allowed)" : string.Empty;
            return FrogletToolValidation.Pass($"{_rows.Count} modes consistent{note}.");
        }

        // ── GUI ──────────────────────────────────────────────────────────────────

        void OnGUI()
        {
            var accent = FrogletEditorPalette.ColorFor(FrogletToolCategory.GameModes);
            FrogletEditorPalette.Banner("Mode Map",
                "Pick a game on the left. Its page reads like the arcade card — and edits the " +
                "real assets in place.", accent);

            if (_library == null)
            {
                EditorGUILayout.HelpBox($"No ModeControlsLibrarySO at {LibraryPath}.",
                                        MessageType.Error);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawModeList(accent);
                DrawPage(FindRow(_selectedMode), accent);
            }

            DrawIconTable();
            FrogletToolShipPanel.Draw(_ship, this);
        }

        // ── Left: the game list ──────────────────────────────────────────────────

        void DrawModeList(Color accent)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
            {
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                if (GUILayout.Button(new GUIContent("Scan project",
                        "Re-read every card, preview, scoring rule and vessel — a newly added " +
                        "vessel shows up in the VESSELS section after this.")))
                    Scan();

                _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
                foreach (var row in _rows)
                {
                    if (!string.IsNullOrEmpty(_search) &&
                        RowName(row).IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    bool selected = row.Definition.Mode == _selectedMode;
                    var label = (HasProblem(row) ? "⚠ " : string.Empty) + RowName(row);
                    var prev = GUI.backgroundColor;
                    if (selected) GUI.backgroundColor = accent;
                    if (GUILayout.Button(label, selected
                            ? EditorStyles.miniButtonMid : EditorStyles.miniButton,
                            GUILayout.Height(24)))
                        SelectMode(row);
                    GUI.backgroundColor = prev;
                }
                EditorGUILayout.EndScrollView();
            }
        }

        // ── Right: the selected game's page ──────────────────────────────────────

        void DrawPage(ModeRow row, Color accent)
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (row == null)
                {
                    EditorGUILayout.HelpBox("No previewable modes found.", MessageType.Info);
                    return;
                }

                _pageScroll = EditorGUILayout.BeginScrollView(_pageScroll);

                DrawPageHeader(row);
                DrawProblemSentences(row);
                DrawObjectiveSection(row);
                DrawVesselsSection(row, accent);
                DrawControlsSection(row);
                DrawSeatsSection(row);
                DrawAssetsSection(row);

                EditorGUILayout.EndScrollView();
            }
        }

        void DrawPageHeader(ModeRow row)
        {
            GUILayout.Label(RowName(row), FrogletEditorPalette.Title);
            GUILayout.Label($"{row.Definition.Mode} (mode id {(int)row.Definition.Mode})",
                            FrogletEditorPalette.Subtitle);
        }

        void DrawProblemSentences(ModeRow row)
        {
            if (MetricDisagreesWithRule(row))
                EditorGUILayout.HelpBox(
                    $"This page's objective counts {row.Definition.ObjectiveMetric}, but the mode " +
                    $"actually scores {row.Rule.Metric} ({row.Rule.name}). Pick the same metric " +
                    "in OBJECTIVE below, or change the rule.", MessageType.Warning);
            if (MetricHasNoIcon(row))
                EditorGUILayout.HelpBox(
                    $"No icon is authored for {row.Definition.ObjectiveMetric} — the objective " +
                    "box on the card will show text only. Add a sprite in the Metric Icons " +
                    "table at the bottom of this window.", MessageType.Warning);
            if (!row.Card)
                EditorGUILayout.HelpBox(
                    "No SO_ArcadeGame card found for this mode — it has a preview but nothing in " +
                    "the arcade grid launches it.", MessageType.Warning);
        }

        static void SectionHeader(string title, string blurb)
        {
            FrogletEditorPalette.HorizontalRule();
            GUILayout.Label(title, FrogletEditorPalette.SectionHeader);
            if (!string.IsNullOrEmpty(blurb))
                GUILayout.Label(blurb, FrogletEditorPalette.Subtitle);
            GUILayout.Space(2);
        }

        // ── OBJECTIVE ────────────────────────────────────────────────────────────

        void DrawObjectiveSection(ModeRow row)
        {
            SectionHeader("OBJECTIVE",
                "What the card's objective box shows: this icon, this line, and a counter that " +
                "climbs as the preview is played.");

            row.DefinitionSO.Update();
            using (new EditorGUILayout.HorizontalScope())
            {
                var iconRect = GUILayoutUtility.GetRect(ObjectiveIconSize, ObjectiveIconSize,
                                                        GUILayout.Width(ObjectiveIconSize));
                FrogletEditorPalette.DrawCard(iconRect, FrogletEditorPalette.Surface,
                                              FrogletEditorPalette.Muted.WithAlpha(0.4f));
                var sprite = _library.IconForMetric(row.Definition.ObjectiveMetric);
                if (sprite)
                    GUI.DrawTexture(ShrinkRect(iconRect, 6f), sprite.texture, ScaleMode.ScaleToFit);
                else
                    GUI.Label(iconRect, "no\nicon", FrogletEditorPalette.CardBody);

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.PropertyField(row.DefinitionSO.FindProperty("ObjectiveMetric"),
                                                  new GUIContent("What scores"));
                    EditorGUILayout.PropertyField(row.DefinitionSO.FindProperty("ObjectiveText"),
                                                  new GUIContent("How you win"));
                }
            }
            if (row.DefinitionSO.ApplyModifiedProperties()) RecordWrite(row.Definition);
        }

        // ── VESSELS ──────────────────────────────────────────────────────────────

        void DrawVesselsSection(ModeRow row, Color accent)
        {
            SectionHeader("VESSELS",
                "The hulls this card offers. Click one to open its section; the hull marked " +
                "CONTROLS is the one the card's controls block describes.");

            if (!row.Card)
            {
                EditorGUILayout.HelpBox("No card, so no vessel list to show.", MessageType.Info);
                return;
            }

            var effective = EffectiveHull(row);
            var listed = row.Card.Vessels ?? new List<SO_Vessel>();

            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var vessel in listed)
                {
                    if (!vessel) continue;
                    DrawVesselTile(vessel, vessel.Class == _selectedVessel,
                                   vessel.Class == effective, accent);
                }
                GUILayout.FlexibleSpace();
            }

            var selected = FindVessel(listed, _selectedVessel);
            if (selected) DrawVesselDetail(row, selected, effective);

            DrawAllVesselsFoldout(row, listed);
        }

        void DrawVesselTile(SO_Vessel vessel, bool selected, bool isControlsHull, Color accent)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(VesselTileSize)))
            {
                var rect = GUILayoutUtility.GetRect(VesselTileSize, VesselTileSize,
                                                    GUILayout.Width(VesselTileSize));
                FrogletEditorPalette.DrawCard(rect,
                    selected ? accent.WithAlpha(0.25f) : FrogletEditorPalette.Surface,
                    selected ? accent : FrogletEditorPalette.Muted.WithAlpha(0.4f),
                    selected ? 2f : 1f);

                var picture = VesselPicture(vessel);
                if (picture)
                    GUI.DrawTexture(ShrinkRect(rect, 5f), picture.texture, ScaleMode.ScaleToFit);
                else
                    GUI.Label(rect, vessel.Class.ToString(), FrogletEditorPalette.CardBody);

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                    _selectedVessel = vessel.Class;

                var name = string.IsNullOrEmpty(vessel.Name) ? vessel.Class.ToString() : vessel.Name;
                GUILayout.Label(name, FrogletEditorPalette.CardBody, GUILayout.Width(VesselTileSize));
                if (isControlsHull)
                {
                    var pillRect = GUILayoutUtility.GetRect(VesselTileSize, 14,
                                                            GUILayout.Width(VesselTileSize));
                    FrogletEditorPalette.StatusPill(pillRect, "CONTROLS", FrogletEditorPalette.Ok);
                }
            }
        }

        void DrawVesselDetail(ModeRow row, SO_Vessel vessel, VesselClassType effective)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var name = string.IsNullOrEmpty(vessel.Name) ? vessel.Class.ToString() : vessel.Name;
                GUILayout.Label($"{name} — {vessel.Class}", FrogletEditorPalette.CardTitle);
                if (!string.IsNullOrEmpty(vessel.Description))
                    GUILayout.Label(vessel.Description, FrogletEditorPalette.CardBodyWrapped);

                var map = ElementalAbilityMapSO.LoadFor(vessel.Class);
                if (!map)
                {
                    EditorGUILayout.HelpBox(
                        "No ElementalAbilityMap authored for this hull yet — the card shows no " +
                        "ability rows for it.", MessageType.Info);
                }
                else
                {
                    GUILayout.Label("The petal is the element's own shape. Names are EDITABLE — " +
                                    "they write straight into the ability map every mode and the " +
                                    "HUD read. 'this card' is what appears in this game mode.",
                                    FrogletEditorPalette.Subtitle);

                    var mapSO = MapSO(map);
                    mapSO.Update();
                    var filter = _library.AbilitiesFor(row.Definition.Mode);
                    foreach (var element in ElementOrder)
                        DrawAbilityLine(row, map, mapSO, element, filter);
                    if (mapSO.ApplyModifiedProperties()) RecordWrite(map);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (vessel.Class != effective &&
                        GUILayout.Button("Describe THIS hull on the card", GUILayout.Width(210)))
                        SetControlsHull(row, vessel.Class);
                    if (LibraryHullOverride(row) != VesselClassType.Any &&
                        GUILayout.Button("Back to card default", GUILayout.Width(150)))
                        SetControlsHull(row, VesselClassType.Any);
                    GUILayout.FlexibleSpace();
                    PingButton("Ability map", map);
                }
            }
        }

        SerializedObject MapSO(ElementalAbilityMapSO map)
        {
            if (!_mapSOs.TryGetValue(map, out var so) || so == null)
                _mapSOs[map] = so = new SerializedObject(map);
            return so;
        }

        void DrawAbilityLine(ModeRow row, ElementalAbilityMapSO map, SerializedObject mapSO,
                             Element element, List<Element> filter)
        {
            var entry = map.GetEntry(element);
            int index = EntryIndex(map, element);
            bool shown = !(filter is { Count: > 0 } && !filter.Contains(element));

            using (new EditorGUILayout.HorizontalScope())
            {
                // The element, in the game's own language: its petal SHAPE.
                var petalRect = GUILayoutUtility.GetRect(18, 18, GUILayout.Width(18));
                var petal = _barsConfig ? _barsConfig.GetPetalSprite(element) : null;
                if (petal) GUI.DrawTexture(petalRect, petal.texture, ScaleMode.ScaleToFit);

                GUILayout.Label(element.ToString().ToUpperInvariant(),
                                FrogletEditorPalette.SectionLabel, GUILayout.Width(56));

                if (entry == null || index < 0)
                {
                    GUILayout.Label("(no slot in this map)", FrogletEditorPalette.CardBody);
                    return;
                }

                var entryProp = mapSO.FindProperty("entries").GetArrayElementAtIndex(index);
                EditorGUILayout.PropertyField(entryProp.FindPropertyRelative("AbilityLabel"),
                                              GUIContent.none, GUILayout.MinWidth(110));

                GUILayout.Label(ControlText(entry.Input), FrogletEditorPalette.CardBody,
                                GUILayout.Width(96));

                GUILayout.Label($"L{entry.UnlockLevel}:", FrogletEditorPalette.CardBody,
                                GUILayout.Width(24));
                EditorGUILayout.PropertyField(entryProp.FindPropertyRelative("UpgradeLabel"),
                                              GUIContent.none, GUILayout.MinWidth(90));

                bool next = GUILayout.Toggle(shown, "this card", GUILayout.Width(76));
                if (next != shown) ToggleAbilityFilter(row, element, next);
            }
        }

        static int EntryIndex(ElementalAbilityMapSO map, Element element)
        {
            var entries = map.Entries;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].Element == element) return i;
            return -1;
        }

        void DrawAllVesselsFoldout(ModeRow row, List<SO_Vessel> listed)
        {
            _showAllVessels = EditorGUILayout.Foldout(_showAllVessels,
                "Every vessel in the project (press Scan after adding a new one)",
                toggleOnLabelClick: true);
            if (!_showAllVessels) return;

            foreach (var vessel in _allVessels)
            {
                if (listed.Contains(vessel)) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    var name = string.IsNullOrEmpty(vessel.Name) ? vessel.name : vessel.Name;
                    GUILayout.Label($"{name} — {vessel.Class}", FrogletEditorPalette.CardBody);
                    GUILayout.FlexibleSpace();
                    PingButton("Ping", vessel);
                    if (GUILayout.Button("Add to card", GUILayout.Width(90)))
                        AddVesselToCard(row, vessel);
                }
            }
        }

        // ── CONTROLS BLOCK ───────────────────────────────────────────────────────

        void DrawControlsSection(ModeRow row)
        {
            SectionHeader("CONTROLS BLOCK",
                "What the card's controls box lists. The rows themselves are derived from the " +
                "hull's ability map above — here you choose WHICH of its four abilities this " +
                "card talks about.");

            var mode = row.Definition.Mode;
            bool showRows = _library.AbilityRowsFor(mode);

            bool newShowRows = EditorGUILayout.ToggleLeft(
                "Show the hull's ability rows on this card", showRows);
            if (newShowRows != showRows) SetShowAbilityRows(row, newShowRows);

            using (new EditorGUI.DisabledScope(!newShowRows))
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Abilities shown", FrogletEditorPalette.SectionLabel,
                                GUILayout.Width(100));
                var filter = _library.AbilitiesFor(mode);
                bool all = filter == null || filter.Count == 0;
                foreach (var element in ElementOrder)
                {
                    bool on = all || filter.Contains(element);
                    bool next = GUILayout.Toggle(on, element.ToString(), EditorStyles.miniButtonMid,
                                                 GUILayout.Width(64));
                    if (next != on) ToggleAbilityFilter(row, element, next);
                }
            }
            GUILayout.Label("All four on = the card shows the vessel's whole row (stored as no " +
                            "filter). To hide everything, untick the toggle above instead.",
                            FrogletEditorPalette.Subtitle);
        }

        // ── SEATS & INTENSITY ────────────────────────────────────────────────────

        void DrawSeatsSection(ModeRow row)
        {
            SectionHeader("SEATS & INTENSITY",
                "The card's player and intensity ranges — the stepper and the intensity row " +
                "read exactly these.");

            if (row.CardSO == null) return;

            row.CardSO.Update();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(row.CardSO.FindProperty("MinPlayersAllowed"),
                                              new GUIContent("Players min"));
                EditorGUILayout.PropertyField(row.CardSO.FindProperty("MaxPlayersAllowed"),
                                              new GUIContent("max"), GUILayout.Width(180));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(row.CardSO.FindProperty("MinIntensity"),
                                              new GUIContent("Intensity min"));
                EditorGUILayout.PropertyField(row.CardSO.FindProperty("MaxIntensity"),
                                              new GUIContent("max"), GUILayout.Width(180));
            }
            if (row.CardSO.ApplyModifiedProperties()) RecordWrite(row.Card);

            GUILayout.Label(row.Card.MinPlayersAllowed >= 2
                    ? "Seats 2+ players, so the preview spawns an AI sparring partner."
                    : "Solo-previewable — no sparring partner in the preview.",
                FrogletEditorPalette.Subtitle);
        }

        // ── ASSETS ───────────────────────────────────────────────────────────────

        void DrawAssetsSection(ModeRow row)
        {
            SectionHeader("BEHIND THIS PAGE",
                "The assets this page edits. The spawn block is authored from the mode's own " +
                "scene by Tools/Build/author_preview_spawns.py — edit the scene and re-run, " +
                "never by hand.");

            using (new EditorGUILayout.HorizontalScope())
            {
                PingButton("Preview", row.Definition);
                PingButton("Card", row.Card);
                PingButton("Controls", _library);
                PingButton("Rule", row.Rule);
                GUILayout.FlexibleSpace();
            }
            GUILayout.Space(6);
        }

        // ── Metric icon table (bottom, shared by every mode) ─────────────────────

        void DrawIconTable()
        {
            FrogletEditorPalette.HorizontalRule();
            _showIconTable = EditorGUILayout.Foldout(_showIconTable,
                "Metric Icons — one sprite per scoring metric, shared by the objective box and " +
                "the micro toast", toggleOnLabelClick: true);
            if (!_showIconTable) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var entry in _library.ObjectiveIcons)
                {
                    if (entry == null) continue;
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(52)))
                    {
                        var rect = GUILayoutUtility.GetRect(48, 48, GUILayout.Width(48));
                        FrogletEditorPalette.DrawCard(rect, FrogletEditorPalette.Surface,
                                                      FrogletEditorPalette.Muted.WithAlpha(0.4f));
                        if (entry.Icon)
                            GUI.DrawTexture(ShrinkRect(rect, 4f), entry.Icon.texture,
                                            ScaleMode.ScaleToFit);
                        GUILayout.Label(entry.Metric.ToString(), FrogletEditorPalette.CardBody,
                                        GUILayout.Width(48));
                    }
                }
                GUILayout.FlexibleSpace();
            }

            _librarySO.Update();
            EditorGUILayout.PropertyField(_librarySO.FindProperty("ObjectiveIcons"),
                                          new GUIContent("Edit the table"), includeChildren: true);
            if (_librarySO.ApplyModifiedProperties()) RecordWrite(_library);
        }

        // ── Library writes (entry auto-created on first edit) ────────────────────

        VesselClassType LibraryHullOverride(ModeRow row)
            => _library.VesselFor(row.Definition.Mode);

        VesselClassType EffectiveHull(ModeRow row)
        {
            var overridden = LibraryHullOverride(row);
            if (overridden != VesselClassType.Any) return overridden;
            var listed = row.Card ? row.Card.Vessels : null;
            if (listed != null)
                foreach (var vessel in listed)
                    if (vessel) return vessel.Class;
            return VesselClassType.Any;
        }

        void SetControlsHull(ModeRow row, VesselClassType hull)
        {
            var entry = EnsureEntry(row.Definition.Mode);
            entry.FindPropertyRelative("Vessel").intValue = (int)hull;
            _librarySO.ApplyModifiedProperties();
            RecordWrite(_library);
        }

        void SetShowAbilityRows(ModeRow row, bool show)
        {
            var entry = EnsureEntry(row.Definition.Mode);
            entry.FindPropertyRelative("ShowAbilityRows").boolValue = show;
            _librarySO.ApplyModifiedProperties();
            RecordWrite(_library);
        }

        void ToggleAbilityFilter(ModeRow row, Element element, bool on)
        {
            var current = _library.AbilitiesFor(row.Definition.Mode);
            var shown = new List<Element>();
            foreach (var e in ElementOrder)
            {
                bool was = current == null || current.Count == 0 || current.Contains(e);
                if (e == element ? on : was) shown.Add(e);
            }
            if (shown.Count == 0) return;   // hide-all is the toggle above, not an empty filter

            var entry = EnsureEntry(row.Definition.Mode);
            var abilities = entry.FindPropertyRelative("Abilities");
            abilities.ClearArray();
            if (shown.Count < ElementOrder.Length)     // all four on = stored as "no filter"
            {
                abilities.arraySize = shown.Count;
                for (int i = 0; i < shown.Count; i++)
                    abilities.GetArrayElementAtIndex(i).intValue = (int)shown[i];
            }
            _librarySO.ApplyModifiedProperties();
            RecordWrite(_library);
        }

        SerializedProperty EnsureEntry(GameModes mode)
        {
            _librarySO.Update();
            var entries = _librarySO.FindProperty("Entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                var candidate = entries.GetArrayElementAtIndex(i);
                if (candidate.FindPropertyRelative("Mode").intValue == (int)mode)
                    return candidate;
            }
            entries.arraySize++;
            var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("Mode").intValue = (int)mode;
            entry.FindPropertyRelative("Rows").ClearArray();
            entry.FindPropertyRelative("ShowAbilityRows").boolValue = true;
            entry.FindPropertyRelative("Abilities").ClearArray();
            entry.FindPropertyRelative("Vessel").intValue = (int)VesselClassType.Any;
            return entry;
        }

        void AddVesselToCard(ModeRow row, SO_Vessel vessel)
        {
            if (row.CardSO == null) return;
            row.CardSO.Update();
            var vessels = row.CardSO.FindProperty("Vessels");
            vessels.arraySize++;
            vessels.GetArrayElementAtIndex(vessels.arraySize - 1).objectReferenceValue = vessel;
            row.CardSO.ApplyModifiedProperties();
            RecordWrite(row.Card);
        }

        // ── Small pieces ─────────────────────────────────────────────────────────

        static SO_Vessel FindVessel(List<SO_Vessel> list, VesselClassType vesselClass)
        {
            foreach (var vessel in list)
                if (vessel && vessel.Class == vesselClass) return vessel;
            return null;
        }

        static Sprite VesselPicture(SO_Vessel vessel)
        {
            if (vessel.IconActive) return vessel.IconActive;
            if (vessel.PreviewImage) return vessel.PreviewImage;
            return vessel.CardSilohoutteActive;
        }

        /// <summary>Both controls for an ability, in words: "RT · Left Shift", or "passive".</summary>
        static string ControlText(InputEvents input)
        {
            var pad = ControlLabel(InputHintBindingMap.BindingFor(input, keyboard: false));
            var key = ControlLabel(InputHintBindingMap.BindingFor(input, keyboard: true));
            if (pad == null && key == null) return "passive — no button";
            if (pad != null && key != null) return $"{pad} · {key}";
            return pad ?? key;
        }

        static string ControlLabel(HintBinding binding) => binding switch
        {
            HintBinding.None => null,
            HintBinding.PadButtonSouth => "A",
            HintBinding.PadButtonNorth => "Y",
            HintBinding.PadButtonEast => "B",
            HintBinding.PadButtonWest => "X",
            HintBinding.PadLeftShoulder => "LB",
            HintBinding.PadRightShoulder => "RB",
            HintBinding.PadLeftTrigger => "LT",
            HintBinding.PadRightTrigger => "RT",
            HintBinding.PadDpadUp => "D-Pad Up",
            HintBinding.PadDpadDown => "D-Pad Down",
            HintBinding.PadDpadLeft => "D-Pad Left",
            HintBinding.PadDpadRight => "D-Pad Right",
            HintBinding.KeyLeftShift => "Left Shift",
            HintBinding.KeyRightShift => "Right Shift",
            HintBinding.KeySpace => "Space",
            HintBinding.KeyTab => "Tab",
            HintBinding.KeyQ => "Q",
            HintBinding.KeyE => "E",
            HintBinding.KeyF => "F",
            HintBinding.MouseLeft => "Left Click",
            HintBinding.MouseRight => "Right Click",
            _ => binding.ToString(),
        };

        static Rect ShrinkRect(Rect rect, float by)
            => new(rect.x + by, rect.y + by, rect.width - by * 2f, rect.height - by * 2f);

        static void PingButton(string label, UnityEngine.Object asset)
        {
            using (new EditorGUI.DisabledScope(!asset))
                if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(64)))
                    EditorGUIUtility.PingObject(asset);
        }

        static void RecordWrite(UnityEngine.Object asset)
        {
            if (!asset) return;
            FrogletToolChangeLedger.Record(ToolName, AssetDatabase.GetAssetPath(asset));
        }
    }
}
