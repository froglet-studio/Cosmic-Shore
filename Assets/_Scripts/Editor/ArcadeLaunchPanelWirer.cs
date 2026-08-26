using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Editor.Froglet;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace CosmicShore.Editor
{
    /// <summary>
    /// One-click wiring for the arcade's <b>one launch panel per card</b>
    /// (<c>Docs/ArcadeLaunch/ARCHITECTURE.md</c>): it finds the authored UI in Menu_Main by NAME,
    /// turns the two hand-authored rows into prefabs, adds and wires every component, and registers
    /// the Maelstrom's window with the screen switcher.
    ///
    /// <para><b>It is a REPORT first and a writer second.</b> Scan lists what it found, what it
    /// would write, and - the part that matters - everything it cannot do, so nothing is silently
    /// half-wired. Anything ambiguous is delegated rather than guessed: a wrong reference here is
    /// invisible until a player opens the card.</para>
    ///
    /// <para><b>Everything is wired IN THE SCENE, deliberately.</b> The arcade modal is a prefab
    /// instance and the Maelstrom modal is a scene object, and a prefab cannot hold a reference to
    /// a scene object - so <c>launchPanels</c> can only ever live as a scene-instance override.
    /// Since that one reference has to be an override, wiring the rest into the prefab would split
    /// the panel's setup across two files for no gain. Menu_Main is the only scene these modals
    /// appear in, which is what makes that acceptable here rather than the drift
    /// <c>Docs/GAMECANVAS.md</c> warns about.</para>
    ///
    /// <para>Idempotent: re-running never overwrites a reference somebody set by hand, and never
    /// re-creates a prefab that already exists.</para>
    ///
    /// <para>ONE-OFF. Retire it through the ship panel once its output is pushed.</para>
    /// </summary>
    public class ArcadeLaunchPanelWirer : EditorWindow
    {
        const string ToolName = "Arcade Launch Panel Wirer";
        const string ScenePath = "Assets/_Scenes/Menu_Main.unity";
        const string RowPrefabFolder = "Assets/_Prefabs/UI Elements/ArcadeLaunch";

        // Names the human authored. Kept in one place because a rename here is the single most
        // likely reason a future run finds nothing.
        const string ArcadeModalName = "ArcadeGameConfigureModal";
        const string MaelstromModalName = "MaelstromGameConfigurationModal";
        const string ConfigContentName = "ConfigurationContent";
        const string GameViewName = "GameView";
        const string ConfigDetailName = "ConfigurationDetailView";

        // CANDIDATES, not names. A hand-authored container gets renamed as the layout settles
        // ("GameListDescriptionDescription" became "GameListDescription" between two runs of this
        // tool), and a wirer that only matches the name it was written against reports "not found"
        // for an object sitting in plain sight. Every lookup tries these, then falls back to what
        // the object IS - see FindListContainer.
        static readonly string[] ControlsNames = { "ControlsDescription", "Controls", "ControlsPanel" };
        static readonly string[] AbilityRowNames = { "AbilityControlRow", "AbilityContent", "AbiiltyContent" };
        static readonly string[] PoolListNames =
            { "GameListDescription", "GameListDescriptionDescription", "GameList", "PoolList" };
        static readonly string[] PoolRowNames =
            { "MaelstromPoolRow", "GameCard", "Game DescriptionBG", "GameListRow" };

        readonly List<string> _found = new();
        readonly List<string> _wrote = new();
        readonly List<string> _todo = new();
        readonly List<string> _problems = new();
        Vector2 _scroll;
        FrogletToolShipContext _ship;

        [MenuItem("FrogletTools/Interface/Arcade Launch Panel Wirer")]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 5,
                     Description = "Wire the one-panel arcade launch surface and the Maelstrom's " +
                                   "own window: build the two row prefabs, add and connect every " +
                                   "component, register the modal. Reports what it cannot do.")]
        static void Open() => GetWindow<ArcadeLaunchPanelWirer>("Arcade Launch Panels").minSize =
            new Vector2(520f, 520f);

        void OnEnable()
        {
            _ship = new FrogletToolShipContext(ToolName)
            {
                ToolScriptPaths = new[] { "Assets/_Scripts/Editor/ArcadeLaunchPanelWirer.cs" },
                CommitType = "feat",
                CommitScope = "arcade",
            };
        }

        void OnGUI()
        {
            FrogletEditorPalette.Banner("Arcade Launch Panels",
                "Wire the one-panel launch surface. Scan first - it reports what it cannot do.",
                FrogletEditorPalette.Violet);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton("Scan", FrogletEditorPalette.Info, 120f))
                    Run(dryRun: true);

                if (FrogletEditorPalette.ColorButton("Wire Everything", FrogletEditorPalette.Ok, 160f))
                    Run(dryRun: false);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            Section("Found", _found, MessageType.None);
            Section("Wrote", _wrote, MessageType.Info);
            Section("YOU need to do these", _todo, MessageType.Warning);
            Section("Problems", _problems, MessageType.Error);
            EditorGUILayout.EndScrollView();

            FrogletToolShipPanel.Draw(_ship, this);
        }

        static void Section(string title, List<string> lines, MessageType type)
        {
            if (lines.Count == 0) return;
            GUILayout.Label($"{title} ({lines.Count})", FrogletEditorPalette.SectionHeader);
            EditorGUILayout.HelpBox(string.Join("\n", lines), type);
        }

        // ── The run ──────────────────────────────────────────────────────────────

        void Run(bool dryRun)
        {
            _found.Clear(); _wrote.Clear(); _todo.Clear(); _problems.Clear();

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
            {
                _problems.Add($"Open {ScenePath} first - this tool wires that scene's modals.");
                return;
            }

            var arcadeModal = FindComponentInScene<ArcadeGameConfigureModal>();
            if (!arcadeModal)
            {
                _problems.Add($"No ArcadeGameConfigureModal in the scene. Nothing can be wired " +
                              "without it - it is the one authority the panels report to.");
                return;
            }
            _found.Add($"ArcadeGameConfigureModal on '{Path(arcadeModal.transform)}'");

            var written = new List<string>();

            var minigamePanel = WireMinigamePanel(arcadeModal, dryRun, written);
            var maelstromPanel = WireMaelstromPanel(dryRun, written);
            WirePanelList(arcadeModal, minigamePanel, maelstromPanel, dryRun);
            WireScreenSwitcher(maelstromPanel, dryRun);

            WireMaelstromCard(arcadeModal, dryRun);
            ReportPreviewCoverage();
            AddStandingTodos();

            if (dryRun)
            {
                _wrote.Insert(0, "DRY RUN - nothing was written. Press 'Wire Everything' to apply.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            written.Add(ScenePath);

            FrogletToolChangeLedger.Record(ToolName, written);
            AssetDatabase.SaveAssets();
        }

        // ── The minigame panel (inside the arcade modal) ─────────────────────────

        ArcadeLaunchPanel WireMinigamePanel(ArcadeGameConfigureModal modal, bool dryRun,
                                            List<string> written)
        {
            var content = FindChild(modal.transform, ConfigContentName);
            if (!content)
            {
                _problems.Add($"'{ConfigContentName}' not found under {ArcadeModalName}. That is " +
                              "the object MinigameLaunchPanel goes on.");
                return null;
            }
            _found.Add($"Minigame panel root: '{Path(content)}'");
            ReportChildren(content);

            // No early-out on a null panel: during a dry run nothing is created, and the rest of
            // this method is exactly the part the human is scanning FOR.
            var panel = Ensure<MinigameLaunchPanel>(content.gameObject, dryRun, _wrote);
            var so = Edit(panel);

            // ── Controls block ──
            var controls = FindListContainer(content, ControlsNames);
            if (controls)
            {
                _found.Add($"Controls block: '{Path(controls)}'");
                var controlsPanel = Ensure<VesselControlsPanel>(controls.gameObject, dryRun, _wrote);
                SetRef(so, "controlsPanel", controlsPanel, dryRun);

                var rowHost = RowHost(controls);
                if (rowHost != controls)
                    _found.Add($"Rows go under the scroll content: '{Path(rowHost)}'");

                var row = FindRowTemplate(rowHost, AbilityRowNames, needsIcon: true);
                if (row)
                {
                    _found.Add($"Ability row template: '{Path(row)}'");
                    var prefab = MakeRowPrefab<VesselControlRow>(row.gameObject, "AbilityControlRow",
                                                                 dryRun, written, WireControlRow);
                    var cso = Edit(controlsPanel);
                    SetRef(cso, "rowPrefab", prefab, dryRun);
                    SetRef(cso, "rowContainer", rowHost, dryRun);

                    // MUST be wired: unlike the glyph set and the bars config, the vessel-prefab
                    // table does not live in Resources, so a row has no way to reach a vessel's
                    // real HUD icons without it and falls back to the prefab's placeholder.
                    var prefabs = LoadOne<VesselPrefabContainer>();
                    if (prefabs) SetRef(cso, "vesselPrefabs", prefabs, dryRun);
                    else _problems.Add("No VesselPrefabContainer asset found - ability rows will " +
                                       "draw placeholder icons instead of each vessel's real ones.");

                    Apply(cso, dryRun);
                }
                else
                {
                    _todo.Add($"No row template under '{Path(controls)}'. Author ONE row - an " +
                              "Image and a TMP_Text - and re-run; the tool turns it into the row " +
                              "prefab the block instantiates. It matches by name first and then " +
                              "by what the object IS, so the name does not have to be exact.");
                }
            }
            else
            {
                _todo.Add($"No controls container under '{ConfigContentName}' - the controls block " +
                          "will not be drawn. It is the child that is neither GameView nor " +
                          "ConfigurationDetailView and carries a layout group.");
            }

            // ── Briefing + preview, from GameView ──
            WireGameView(so, FindChild(content, GameViewName), dryRun, wantVideo: false);

            // ── The controls the modal drives ──
            WireModalControls(so, FindChild(content, ConfigDetailName), dryRun, ConfigDetailName);

            Apply(so, dryRun);
            return panel;
        }

        // ── The Maelstrom panel (its own modal window) ───────────────────────────

        ArcadeLaunchPanel WireMaelstromPanel(bool dryRun, List<string> written)
        {
            var modalGo = FindInScene(MaelstromModalName);
            if (!modalGo)
            {
                _todo.Add($"No '{MaelstromModalName}' in the scene, so the Maelstrom keeps the " +
                          "minigame panel. Build it and re-run.");
                return null;
            }
            _found.Add($"MaelstromGameConfigurationModal on '{Path(modalGo.transform)}'");

            // Its window. A plain ModalWindowManager, deliberately NOT a second
            // ArcadeGameConfigureModal: closed modals stay ACTIVE in this project (they hide via
            // CanvasGroup), so a second instance would sit subscribed to ArcadeConfigSyncManager
            // alongside the first and both would open on a client's commit.
            var window = modalGo.GetComponent<ModalWindowManager>();
            if (!window)
            {
                if (!dryRun) window = Undo.AddComponent<ModalWindowManager>(modalGo);
                _wrote.Add($"ModalWindowManager on '{MaelstromModalName}'");
            }
            var wso = Edit(window);
            if (wso != null)
            {
                var type = wso.FindProperty("ModalType");
                if (type != null && type.enumValueIndex != (int)ScreenSwitcher.ModalWindows.MAELSTROM_GAME_CONFIGURE)
                {
                    if (!dryRun)
                        type.enumValueIndex = (int)ScreenSwitcher.ModalWindows.MAELSTROM_GAME_CONFIGURE;
                    Apply(wso, dryRun);
                    _wrote.Add($"'{MaelstromModalName}'.ModalType = MAELSTROM_GAME_CONFIGURE");
                }
            }

            var content = FindChild(modalGo.transform, ConfigContentName);
            if (!content)
            {
                _problems.Add($"'{ConfigContentName}' not found under {MaelstromModalName}.");
                return null;
            }
            ReportChildren(content);

            var panel = Ensure<MaelstromLaunchPanel>(content.gameObject, dryRun, _wrote);
            var so = Edit(panel);
            SetRef(so, "hostModal", window, dryRun);

            // ── Pool list ──
            var list = FindListContainer(content, PoolListNames);
            if (list)
            {
                _found.Add($"Pool list container: '{Path(list)}'" +
                           (list.GetComponent<LayoutGroup>() ? " (has a layout group)" : ""));
                var listView = Ensure<MaelstromPoolListView>(list.gameObject, dryRun, _wrote);
                SetRef(so, "poolList", listView, dryRun);

                var listHost = RowHost(list);
                var lso = Edit(listView);
                SetRef(lso, "rowContainer", listHost, dryRun);

                var row = FindRowTemplate(listHost, PoolRowNames, needsIcon: false);
                if (row)
                {
                    _found.Add($"Pool row template: '{Path(row)}'");
                    var prefab = MakeRowPrefab<MaelstromPoolEntry>(row.gameObject, "MaelstromPoolRow",
                                                                    dryRun, written, WirePoolEntry);
                    SetRef(lso, "rowPrefab", prefab, dryRun);
                }
                else
                {
                    _todo.Add($"No row template under '{Path(list)}'. Author ONE row (a " +
                              "TMP_Text, optionally an Image) and re-run.");
                }

                var tournament = LoadOne<TournamentDataSO>();
                if (tournament) SetRef(lso, "tournamentData", tournament, dryRun);
                else _problems.Add("No TournamentDataSO found - the pool list has nothing to read.");

                Apply(lso, dryRun);
            }
            else
            {
                _todo.Add($"No pool-list container under the Maelstrom's '{ConfigContentName}' - " +
                          "the child that is neither GameView nor ConfigurationDetailView and " +
                          "carries a layout group.");
            }

            WireGameView(so, FindChild(content, GameViewName), dryRun, wantVideo: true);
            WireModalControls(so, FindChild(content, ConfigDetailName), dryRun,
                              $"{MaelstromModalName}/{ConfigDetailName}");

            Apply(so, dryRun);
            return panel;
        }

        // ── Shared wiring ────────────────────────────────────────────────────────

        /// <summary>
        /// The GameView block: title, favourite, briefing, and either the live preview window
        /// (a minigame) or a clip (the Maelstrom). The two are mutually exclusive by design -
        /// a mode with no arena of its own has nothing to stand up.
        /// </summary>
        void WireGameView(SerializedObject panel, Transform gameView, bool dryRun, bool wantVideo)
        {
            if (!gameView)
            {
                // panel may be null on a scan (nothing is created), so the message names the
                // WINDOW rather than the component that does not exist yet.
                _todo.Add($"No '{GameViewName}' under the " +
                          (wantVideo ? MaelstromModalName : ArcadeModalName) +
                          $"'s '{ConfigContentName}' - no title, briefing or preview will be drawn.");
                return;
            }

            SetRef(panel, "gameNameText", FindComponentInChildren<TMP_Text>(gameView, "Game Name"), dryRun);
            SetRef(panel, "favoriteIcon", gameView.GetComponentInChildren<FavoriteIcon>(true), dryRun);

            var briefing = Ensure<GameBriefingView>(gameView.gameObject, dryRun, _wrote);
            SetRef(panel, "briefing", briefing, dryRun);

            // ONE text field: the description and the tips take turns in it. There is no
            // separate 'Tip' object to find - see GameBriefingView.
            var body = FindComponentInChildren<TMP_Text>(gameView, "Game Description");
            if (!body)
            {
                var texts = gameView.GetComponentsInChildren<TMP_Text>(true);
                // The title is the other text in this block; the body is the OTHER one.
                body = texts.FirstOrDefault(t => t.name != "Game Name") ?? texts.FirstOrDefault();
            }

            var bso = Edit(briefing);
            SetRef(bso, "bodyText", body, dryRun);
            Apply(bso, dryRun);

            if (!body)
                _todo.Add($"'{Path(gameView)}' has no TMP_Text for the briefing to write into.");

            var preview = gameView.Find("Preview");
            if (!preview)
            {
                _todo.Add($"No 'Preview' under '{Path(gameView)}'.");
                return;
            }

            if (wantVideo)
            {
                var view = Ensure<ModeVideoView>(preview.gameObject, dryRun, _wrote);
                if (!preview.GetComponent<VideoPlayer>())
                {
                    if (!dryRun) Undo.AddComponent<VideoPlayer>(preview.gameObject);
                    _wrote.Add($"VideoPlayer on '{Path(preview)}'");
                }
                SetRef(panel, "videoView", view, dryRun);

                var vso = Edit(view);
                SetRef(vso, "surface", preview.GetComponentInChildren<RawImage>(true), dryRun);
                Apply(vso, dryRun);

                if (preview.GetComponentInChildren<RawImage>(true) == null)
                    _todo.Add($"'{Path(preview)}' has no RawImage - the clip has no surface to " +
                              "render into. Add one and re-run.");

                // Ask the ASSET rather than telling the human to check: a standing instruction
                // they have already carried out is noise, and noise is what makes a TODO list
                // stop being read.
                var card = LoadTournamentCard();
                if (card && !card.PreviewVideo)
                    _todo.Add($"'{card.name}' has no Preview Video. It is the ONLY card that gets " +
                              "one - every other mode previews live and must never fall back to " +
                              "a video.");
                else if (!card)
                    _todo.Add("Could not find the Tournament card to check its Preview Video.");
                return;
            }

            TidyPreviewFrame(preview, dryRun);

            var window = preview.GetComponent<ModePreviewWindow>();
            if (window) SetRef(panel, "previewWindow", window, dryRun);
            else
                _todo.Add($"No ModePreviewWindow on '{Path(preview)}'. Run FrogletTools > Scene " +
                          "Setup > Setup Mode Preview - it builds the window's surface, status " +
                          "label and focus button - then re-run this tool.");
        }

        /// <summary>
        /// The intensity row, the domain tiles and the Start button. These are the controls the
        /// MODAL drives: the panel only exposes them.
        /// </summary>
        void WireModalControls(SerializedObject panel, Transform detail, bool dryRun, string where)
        {
            if (!detail)
            {
                _todo.Add($"No '{ConfigDetailName}' under {where} - no intensity row, domain tiles " +
                          "or Start button will be found.");
                return;
            }

            var intensity = detail.GetComponentsInChildren<IntensitySelectButton>(true)
                                  .OrderBy(b => b.transform.GetSiblingIndex()).ToArray();
            SetList(panel, "intensityButtons", intensity, dryRun);

            // Four is the whole ladder. Anything else is almost always a duplicated group left
            // over from the old two-screen layout, and it matters: InitializeScreen1Controls
            // walks this list by INDEX to decide which level each button stands for, so a fifth
            // entry silently mislabels the row.
            if (intensity.Length != 0 && intensity.Length != 4)
                _problems.Add($"{where} has {intensity.Length} IntensitySelectButtons, not 4. " +
                              "Delete the duplicates (the old screens each carried a group) - the " +
                              "row is read BY INDEX, so extras mislabel the intensities.");

            var tiles = detail.GetComponentsInChildren<DomainInfoData>(true).ToArray();
            SetList(panel, "domainTiles", tiles, dryRun);

            if (tiles.Length > 3)
                _found.Add($"{where}: {tiles.Length} domain tiles. If one of them is BLUE that is " +
                           "fine - Blue is the 'no team' sentinel and the modal hides it at " +
                           "runtime. More than one extra means duplicates.");

            // The Start button by name, then by label, then give up and say so - guessing which
            // Button in a panel launches the game is exactly the wrong thing to be confident about.
            var start = FindComponentInChildren<Button>(detail, "Play Button (1)")
                     ?? FindComponentInChildren<Button>(detail, "Play Button")
                     ?? FindComponentInChildren<Button>(detail, "Start Button")
                     ?? detail.GetComponentsInChildren<Button>(true)
                              .FirstOrDefault(b => LooksLikeStart(b));
            if (start) SetRef(panel, "startButton", start, dryRun);
            else
                _todo.Add($"Could not identify the Start button under {where}. Assign it on the " +
                          "panel by hand - the tool will not guess which Button launches a game.");

            var lobby = detail.GetComponentInChildren<LobbySlotRow>(true);
            if (lobby) SetRef(panel, "lobbyRow", lobby, dryRun);
        }

        static bool LooksLikeStart(Button b)
        {
            var label = b.GetComponentInChildren<TMP_Text>(true);
            return label && label.text.IndexOf("start", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// The one reference that CANNOT live in the prefab: the arcade modal is a prefab instance
        /// and the Maelstrom panel is a scene object, so this list is necessarily a scene override.
        /// </summary>
        void WirePanelList(ArcadeGameConfigureModal modal, ArcadeLaunchPanel minigame,
                           ArcadeLaunchPanel maelstrom, bool dryRun)
        {
            var so = Edit(modal);
            var list = so?.FindProperty("launchPanels");
            if (list == null)
            {
                _problems.Add("ArcadeGameConfigureModal has no 'launchPanels' field - the code on " +
                              "this branch is older than this tool.");
                return;
            }

            // The Maelstrom is not one of the grid's cards any more, so the modal needs its own
            // way to find that card when a button asks for it (OpenMaelstrom).
            var tournament = LoadOne<TournamentDataSO>();
            if (tournament) SetRef(so, "tournamentData", tournament, dryRun);

            var roster = LoadOne<SO_GameList>();
            if (roster) SetRef(so, "gameList", roster, dryRun);

            var panels = new List<ArcadeLaunchPanel>();
            if (minigame) panels.Add(minigame);
            if (maelstrom) panels.Add(maelstrom);
            if (panels.Count == 0)
            {
                // Ordinary on a SCAN - the panel components are created by the write pass, so
                // there is nothing to point the list at yet. Say what would happen rather than
                // going quiet, which would read as "this step is not needed".
                if (dryRun)
                    _wrote.Add("WOULD set ArcadeGameConfigureModal.launchPanels once the panel " +
                               "components exist (they are created by the write pass).");
                return;
            }

            if (!dryRun)
            {
                list.arraySize = panels.Count;
                for (int i = 0; i < panels.Count; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = panels[i];
            }
            Apply(so, dryRun);
            _wrote.Add($"ArcadeGameConfigureModal.launchPanels = [{string.Join(", ", panels.Select(p => p.GetType().Name))}] " +
                       "(a scene override by necessity - a prefab cannot reference a scene object)");
        }

        void WireScreenSwitcher(ArcadeLaunchPanel maelstrom, bool dryRun)
        {
            if (!maelstrom || !maelstrom.HostModal) return;

            var switcher = FindComponentInScene<ScreenSwitcher>();
            if (!switcher)
            {
                _todo.Add("No ScreenSwitcher in the scene - add the Maelstrom modal to its Modals " +
                          "list by hand, or its close will not unwind the modal stack.");
                return;
            }

            var so = Edit(switcher);
            var list = so?.FindProperty("Modals");
            if (list == null)
            {
                _problems.Add("ScreenSwitcher has no 'Modals' list.");
                return;
            }

            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == maelstrom.HostModal)
                    return;                                   // already registered

            if (!dryRun)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = maelstrom.HostModal;
            }
            Apply(so, dryRun);
            _wrote.Add("ScreenSwitcher.Modals += the Maelstrom modal");
        }

        // ── Row prefabs ──────────────────────────────────────────────────────────

        /// <summary>
        /// Turn a hand-authored row into the prefab its list instantiates, wire the component on
        /// it, and DEACTIVATE the scene copy.
        ///
        /// <para>Deactivate, never delete. Which child is "the row" is the one judgement in this
        /// tool that can be wrong - it is matched by name and then by shape, and a description
        /// block sitting where a row template would sit looks the same to both. A layout group
        /// ignores an inactive child, so a correct guess costs nothing and reads as the ordinary
        /// disabled-template idiom; a wrong one costs the human a click to undo instead of their
        /// authoring. Deletion was the only irreversible thing here, and it was guarding against a
        /// stray row.</para>
        /// </summary>
        GameObject MakeRowPrefab<T>(GameObject source, string prefabName, bool dryRun,
                                    List<string> written, Action<T> wire) where T : Component
        {
            string path = $"{RowPrefabFolder}/{prefabName}.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing)
            {
                _found.Add($"Row prefab already exists: {path}");

                // Still deactivate the scene copy. Returning early because the PREFAB exists left
                // the authored template rendering its placeholder ("Press RT to active drift") on
                // top of every real row - the second run of a wirer has to reach the same end
                // state as the first, not just skip the work the first one did.
                if (source.activeSelf && !dryRun)
                {
                    Undo.RecordObject(source, "Deactivate row template");
                    source.SetActive(false);
                    _wrote.Add($"Deactivated the leftover scene template '{Path(source.transform)}'.");
                }
                return existing;
            }

            if (dryRun)
            {
                _wrote.Add($"WOULD create {path} from '{Path(source.transform)}' and deactivate " +
                           "the scene copy. CHECK THAT OBJECT IS REALLY THE ROW TEMPLATE - it is " +
                           "the one thing here the tool can get wrong.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(RowPrefabFolder))
            {
                var parent = System.IO.Path.GetDirectoryName(RowPrefabFolder).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(RowPrefabFolder));
            }

            var component = source.GetComponent<T>() ?? Undo.AddComponent<T>(source);
            wire?.Invoke(component);

            var prefab = PrefabUtility.SaveAsPrefabAsset(source, path, out bool ok);
            if (!ok || !prefab)
            {
                _problems.Add($"Could not save {path} - the scene copy is untouched.");
                return null;
            }

            Undo.RecordObject(source, "Deactivate row template");
            source.SetActive(false);

            written.Add(path);
            _wrote.Add($"{path} - built from '{Path(source.transform)}', which is now DEACTIVATED " +
                       "(the panel instantiates its own rows). If that was the wrong object, " +
                       "re-enable it and delete the prefab.");
            return prefab;
        }

        static void WireControlRow(VesselControlRow row)
        {
            var so = new SerializedObject(row);
            var t = row.transform;
            SetRefStatic(so, "icon", FindComponentInChildren<Image>(t, "Icon")
                                     ?? t.GetComponentInChildren<Image>(true));
            SetRefStatic(so, "descriptionText",
                         FindComponentInChildren<TMP_Text>(t, "Game Description")
                         ?? t.GetComponentInChildren<TMP_Text>(true));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Only the NAME. The icon slot is deliberately left empty: "the first Image in the row" is
        /// the row's BACKGROUND far more often than it is an icon slot, and writing the mode's card
        /// sprite into a background repaints the whole row - which is exactly what turned every
        /// Maelstrom pool row into a cyan slab. A row's own art belongs to the row; wire an icon by
        /// hand if one is genuinely authored for it.
        /// </summary>
        static void WirePoolEntry(MaelstromPoolEntry entry)
        {
            var so = new SerializedObject(entry);
            var t = entry.transform;
            SetRefStatic(so, "nameText", FindComponentInChildren<TMP_Text>(t, "GameTitle")
                                         ?? FindComponentInChildren<TMP_Text>(t, "Game Description Text")
                                         ?? t.GetComponentInChildren<TMP_Text>(true));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Standing instructions ────────────────────────────────────────────────

        /// <summary>
        /// Which playable cards have no preview definition, so "why does THAT one say preview not
        /// available?" is answered in the report rather than by opening seventeen assets.
        ///
        /// <para>Only cards whose SCENE exists are listed: the arcade carries ~24 single-player
        /// cards whose scenes were deleted, and those are correctly unpreviewable - naming them
        /// here would bury the handful that are a real gap.</para>
        /// </summary>
        void ReportPreviewCoverage()
        {
            var library = Resources.Load<ModePreviewLibrarySO>(ModePreviewLibrarySO.ResourcePath);
            if (!library)
            {
                _problems.Add("No ModePreviewLibrary in Resources - every card will say " +
                              "'LEVEL PREVIEW NOT AVAILABLE'.");
                return;
            }

            var scenes = new HashSet<string>(
                AssetDatabase.FindAssets("t:Scene")
                             .Select(g => System.IO.Path.GetFileNameWithoutExtension(
                                 AssetDatabase.GUIDToAssetPath(g))));

            var missing = AssetDatabase.FindAssets("t:SO_ArcadeGame")
                .Select(g => AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c && c.Mode != CosmicShore.Data.GameModes.Tournament)
                .Where(c => !string.IsNullOrEmpty(c.SceneName) && scenes.Contains(c.SceneName))
                .Where(c => library.Resolve(c.Mode) == null)
                .Select(c => c.DisplayName)
                .OrderBy(n => n)
                .ToList();

            if (missing.Count == 0)
            {
                _found.Add("Preview coverage: every playable card has a definition.");
                return;
            }

            _todo.Add($"{missing.Count} playable card(s) have NO ModePreviewDefinition and will " +
                      $"say 'LEVEL PREVIEW NOT AVAILABLE': {string.Join(", ", missing)}. Author " +
                      "one each (ScriptableObjects > Game > Mode Preview) pointing at the mode's " +
                      "own CellConfigDataSO, and add it to the library. Cards whose scene no " +
                      "longer exists are excluded - those are correctly unpreviewable.");
        }

        /// <summary>
        /// Point the quick-play card at the Maelstrom.
        ///
        /// <para>That card is the Maelstrom's entry point now that the meta-mode is out of the
        /// arcade grid. Two things have to happen together: the button gains a call to
        /// <c>OpenMaelstrom</c>, and <c>QuickPlayButton</c> - which subscribes itself in Start and
        /// launches HexRace instantly - is switched OFF. Leaving it on means one click both opens
        /// the panel and launches a different game, and the launch wins.</para>
        /// </summary>
        void WireMaelstromCard(ArcadeGameConfigureModal modal, bool dryRun)
        {
            var quickPlay = FindComponentInScene<QuickPlayButton>();
            if (!quickPlay)
            {
                _todo.Add("No QuickPlayButton in the scene, so the Maelstrom has no entry point. " +
                          "Wire a button's onClick to ArcadeGameConfigureModal.OpenMaelstrom().");
                return;
            }

            var button = quickPlay.GetComponent<Button>();
            if (!button)
            {
                _problems.Add($"'{Path(quickPlay.transform)}' has a QuickPlayButton but no Button.");
                return;
            }

            _found.Add($"Quick-play card: '{Path(quickPlay.transform)}'");

            bool alreadyWired = false;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentTarget(i) == modal &&
                    button.onClick.GetPersistentMethodName(i) == nameof(ArcadeGameConfigureModal.OpenMaelstrom))
                    alreadyWired = true;

            if (!alreadyWired)
            {
                if (!dryRun)
                {
                    Undo.RecordObject(button, "Wire Maelstrom card");
                    UnityEventTools.AddVoidPersistentListener(
                        button.onClick, modal.OpenMaelstrom);
                    EditorUtility.SetDirty(button);
                }
                _wrote.Add($"'{quickPlay.name}'.onClick += ArcadeGameConfigureModal.OpenMaelstrom()");
            }

            if (quickPlay.enabled)
            {
                if (!dryRun)
                {
                    Undo.RecordObject(quickPlay, "Disable QuickPlayButton");
                    quickPlay.enabled = false;
                    EditorUtility.SetDirty(quickPlay);
                }
                _wrote.Add("QuickPlayButton DISABLED - it launches HexRace instantly on the same " +
                           "click, and that launch would win over the panel opening.");
            }
        }

        /// <summary>
        /// The white rectangle behind a preview frame.
        ///
        /// <para><see cref="ModePreviewWindow"/> disables its RawImage in every non-live state,
        /// precisely so nothing draws while there is no camera. What then shows through is whatever
        /// the frame has BEHIND it - and these frames still carry the old video path's placeholder
        /// Image, which is white. So "the preview is a white box" is not the window failing; it is
        /// the window correctly showing nothing, in front of something.</para>
        /// </summary>
        void TidyPreviewFrame(Transform preview, bool dryRun)
        {
            if (!preview) return;

            var window = preview.GetComponent<ModePreviewWindow>();
            var surface = preview.GetComponentInChildren<RawImage>(true);

            foreach (var image in preview.GetComponentsInChildren<Image>(true))
            {
                if (!image || !image.enabled) continue;
                // A Button's own target graphic is the focus hit-area and must keep raycasting.
                if (image.GetComponent<Button>()) continue;

                if (!dryRun)
                {
                    Undo.RecordObject(image, "Hide preview placeholder");
                    image.enabled = false;
                    EditorUtility.SetDirty(image);
                }
                _wrote.Add($"Disabled placeholder Image '{image.name}' behind the preview frame " +
                           "(it is what shows as a white box whenever the preview is not live).");
            }

            if (!surface)
                _problems.Add($"'{Path(preview)}' has no RawImage - the preview has no surface.");

            if (window)
            {
                var wso = Edit(window);
                var label = wso?.FindProperty("statusLabel");
                if (label != null && !label.objectReferenceValue)
                    _todo.Add($"ModePreviewWindow on '{Path(preview)}' has no statusLabel, so " +
                              "'LEVEL PREVIEW NOT AVAILABLE' and 'LOADING…' render as an empty " +
                              "frame. Wire a TMP_Text.");
            }
        }

        void AddStandingTodos()
        {
            _todo.Add("DELETE from the arcade modal once this run looks right: GameDetailView, " +
                      "SquadMateSelection, VesselSelection. They are the old second screen and " +
                      "nothing reads them any more.");
            _todo.Add("Author the flight rows on VesselControlsPanel > Flight Controls: two " +
                      "placeholders ship (left stick / right stick) with no icons. They are the " +
                      "one part of the controls block that cannot be derived - an axis belongs to " +
                      "the input scheme, not to a vessel.");
            _todo.Add("Write SO_ArcadeGame.Tips on the cards you care about. The briefing cycles " +
                      "description -> tip -> tip -> description in ONE text field; with no tips a " +
                      "card simply shows its description and never rotates, which is the correct " +
                      "resting state, not a broken one.");
            _todo.Add("PLAY-TEST: open a minigame card (panel + live preview + controls sweep), " +
                      "change intensity (the preview rebuilds only for Ribcage / Dog Fight / The " +
                      "Bends / Wildlife Liberation), open the Maelstrom card (its own window, clip, " +
                      "pool list growing with intensity), and close both ways - the X and gamepad B.");
        }

        // ── Small helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// A SerializedObject, or null for a component that does not exist yet.
        ///
        /// <para>Load-bearing on a SCAN: Ensure deliberately adds nothing during a dry run, so
        /// every component it would create is null, and <c>new SerializedObject(null)</c> throws.
        /// A report that dies on the first not-yet-created component would tell the human about
        /// one problem and hide every other - which is the opposite of what a scan is for.</para>
        /// </summary>
        static SerializedObject Edit(UnityEngine.Object target)
            => target ? new SerializedObject(target) : null;

        static void Apply(SerializedObject so, bool dryRun)
        {
            if (so != null && !dryRun) so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// List what the tool can SEE under a panel root. When a lookup misses, the next question
        /// is always "what is actually there?" - printing it turns a rename into something the
        /// human diagnoses at a glance instead of by re-reading this file.
        /// </summary>
        void ReportChildren(Transform content)
        {
            var names = new List<string>();
            foreach (Transform child in content)
                names.Add(child.name + (child.GetComponent<LayoutGroup>() ? " [layout]" : ""));

            _found.Add($"'{content.name}' children: {string.Join(", ", names)}");
        }

        static T Ensure<T>(GameObject go, bool dryRun, List<string> log) where T : Component
        {
            var existing = go.GetComponent<T>();
            if (existing) return existing;

            if (dryRun)
            {
                log.Add($"WOULD add {typeof(T).Name} to '{go.name}'");
                return null;
            }

            log.Add($"{typeof(T).Name} on '{go.name}'");
            return Undo.AddComponent<T>(go);
        }

        /// <summary>
        /// Never overwrites a reference somebody set by hand: re-running a wirer must be safe, and
        /// a hand-made fix is the most valuable thing in the file.
        /// </summary>
        void SetRef(SerializedObject so, string field, UnityEngine.Object value, bool dryRun)
        {
            if (so == null || !value) return;
            var prop = so.FindProperty(field);
            if (prop == null) { _problems.Add($"No field '{field}' on {so.targetObject.GetType().Name}."); return; }
            if (prop.objectReferenceValue) return;

            if (!dryRun) prop.objectReferenceValue = value;
            _wrote.Add($"{so.targetObject.GetType().Name}.{field} = {value.name}");
        }

        static void SetRefStatic(SerializedObject so, string field, UnityEngine.Object value)
        {
            var prop = so?.FindProperty(field);
            if (prop == null || !value || prop.objectReferenceValue) return;
            prop.objectReferenceValue = value;
        }

        void SetList(SerializedObject so, string field, UnityEngine.Object[] values, bool dryRun)
        {
            if (so == null) return;
            var prop = so.FindProperty(field);
            if (prop == null) { _problems.Add($"No list '{field}' on {so.targetObject.GetType().Name}."); return; }
            if (values.Length == 0) return;
            if (prop.arraySize > 0) return;          // already authored - leave it alone

            if (!dryRun)
            {
                prop.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            _wrote.Add($"{so.targetObject.GetType().Name}.{field} = {values.Length} item(s)");
        }

        static T FindComponentInScene<T>() where T : Component
            => Resources.FindObjectsOfTypeAll<T>()
                        .FirstOrDefault(c => c.gameObject.scene.IsValid() &&
                                             !EditorUtility.IsPersistent(c));

        static GameObject FindInScene(string name)
            => Resources.FindObjectsOfTypeAll<GameObject>()
                        .FirstOrDefault(g => g.name == name && g.scene.IsValid() &&
                                             !EditorUtility.IsPersistent(g));

        /// <summary>
        /// The container a list of rows is built under: one of <paramref name="names"/> if present,
        /// else the child of <paramref name="content"/> that is neither the GameView nor the
        /// ConfigurationDetailView and carries a LAYOUT GROUP.
        ///
        /// <para>A layout group is what a row container IS - it exists to arrange children the
        /// panel creates - so it identifies one even after a rename, which names have already
        /// failed to survive twice on this panel.</para>
        /// </summary>
        static Transform FindListContainer(Transform content, string[] names)
        {
            var named = FindChildAny(content, names);
            if (named) return named;

            foreach (Transform child in content)
            {
                if (child.name == GameViewName || child.name == ConfigDetailName) continue;
                if (child.GetComponent<LayoutGroup>()) return child;
            }
            return null;
        }

        /// <summary>
        /// Where rows actually go. A block wrapped in a ScrollRect must build its rows under the
        /// scroll's CONTENT, not under the block root - rows parented to the root sit outside the
        /// viewport's mask and render on top of everything below them, which is exactly the
        /// overlapping wall of text the scroll view was added to fix.
        /// </summary>
        static Transform RowHost(Transform container)
        {
            var scroll = container.GetComponentInChildren<ScrollRect>(true);
            if (scroll && scroll.content) return scroll.content;
            return container;
        }

        static Transform FindChildAny(Transform root, string[] names)
        {
            foreach (var name in names)
            {
                var hit = FindChild(root, name);
                if (hit) return hit;
            }
            return null;
        }

        /// <summary>
        /// The row a list should clone: a child matching one of <paramref name="names"/> if one
        /// exists, else the first child that simply LOOKS like a row.
        ///
        /// <para>Structure, not spelling. A hand-authored row is named by a human and the first run
        /// of this tool found nothing because the object was called "AbiiltyContent" - which is
        /// obviously the right object to anyone looking at it and invisible to an exact match. What
        /// a row IS is "a direct child carrying the graphics the row draws", and that is checkable.</para>
        /// </summary>
        static Transform FindRowTemplate(Transform container, string[] names, bool needsIcon)
        {
            var named = FindChildAny(container, names);
            if (named) return named;

            foreach (Transform child in container)
            {
                // Inactive children are NOT skipped: a template this tool already turned into a
                // prefab is left disabled, and a re-run has to recognise it rather than pick the
                // next child and make a second prefab out of it.
                if (child.GetComponentInChildren<TMP_Text>(true) == null) continue;
                if (needsIcon && child.GetComponentInChildren<Image>(true) == null) continue;
                return child;
            }
            return null;
        }

        static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name == name) return t;
            return null;
        }

        static T FindComponentInChildren<T>(Transform root, string name) where T : Component
        {
            var child = root && root.name == name ? root : FindChild(root, name);
            return child ? child.GetComponent<T>() : null;
        }

        /// <summary>The Maelstrom's own arcade card, via the tournament asset that names it.</summary>
        static SO_ArcadeGame LoadTournamentCard()
        {
            var data = LoadOne<TournamentDataSO>();
            if (data && data.ModeCard) return data.ModeCard;

            return AssetDatabase.FindAssets("t:SO_ArcadeGame")
                .Select(g => AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(c => c && c.Mode == CosmicShore.Data.GameModes.Tournament);
        }

        static T LoadOne<T>() where T : ScriptableObject
        {
            var guid = AssetDatabase.FindAssets($"t:{typeof(T).Name}").FirstOrDefault();
            return guid == null ? null
                : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        }

        static string Path(Transform t)
        {
            var parts = new List<string>();
            for (var c = t; c; c = c.parent) parts.Add(c.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
