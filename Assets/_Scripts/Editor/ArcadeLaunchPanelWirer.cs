using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Editor.Froglet;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using TMPro;
using UnityEditor;
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
        const string ControlsDescName = "ControlsDescription";
        const string AbilityRowName = "AbilityContent";
        const string GameViewName = "GameView";
        const string ConfigDetailName = "ConfigurationDetailView";
        const string PoolListName = "GameListDescriptionDescription";
        const string PoolRowName = "GameCard";

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

            var panel = Ensure<MinigameLaunchPanel>(content.gameObject, dryRun, _wrote);
            if (!panel) return null;

            var so = new SerializedObject(panel);

            // ── Controls block ──
            var controls = FindChild(content, ControlsDescName);
            if (controls)
            {
                var controlsPanel = Ensure<VesselControlsPanel>(controls.gameObject, dryRun, _wrote);
                SetRef(so, "controlsPanel", controlsPanel, dryRun);

                var row = FindChild(controls, AbilityRowName);
                if (row)
                {
                    var prefab = MakeRowPrefab<VesselControlRow>(row.gameObject, "AbilityControlRow",
                                                                 dryRun, written, WireControlRow);
                    var cso = new SerializedObject(controlsPanel);
                    SetRef(cso, "rowPrefab", prefab, dryRun);
                    SetRef(cso, "rowContainer", controls, dryRun);
                    if (!dryRun) cso.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    _todo.Add($"No '{AbilityRowName}' under '{ControlsDescName}'. Author ONE row " +
                              "(an icon Image plus a TMP_Text) and re-run - the tool turns it into " +
                              "the row prefab the block instantiates.");
                }
            }
            else
            {
                _todo.Add($"No '{ControlsDescName}' under '{ConfigContentName}' - the controls " +
                          "block will not be drawn.");
            }

            // ── Briefing + preview, from GameView ──
            WireGameView(so, FindChild(content, GameViewName), dryRun, wantVideo: false);

            // ── The controls the modal drives ──
            WireModalControls(so, FindChild(content, ConfigDetailName), dryRun, ConfigDetailName);

            if (!dryRun) so.ApplyModifiedPropertiesWithoutUndo();
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
            if (window)
            {
                var wso = new SerializedObject(window);
                var type = wso.FindProperty("ModalType");
                if (type != null && type.enumValueIndex != (int)ScreenSwitcher.ModalWindows.MAELSTROM_GAME_CONFIGURE)
                {
                    if (!dryRun)
                    {
                        type.enumValueIndex = (int)ScreenSwitcher.ModalWindows.MAELSTROM_GAME_CONFIGURE;
                        wso.ApplyModifiedPropertiesWithoutUndo();
                    }
                    _wrote.Add($"'{MaelstromModalName}'.ModalType = MAELSTROM_GAME_CONFIGURE");
                }
            }

            var content = FindChild(modalGo.transform, ConfigContentName);
            if (!content)
            {
                _problems.Add($"'{ConfigContentName}' not found under {MaelstromModalName}.");
                return null;
            }

            var panel = Ensure<MaelstromLaunchPanel>(content.gameObject, dryRun, _wrote);
            if (!panel) return null;

            var so = new SerializedObject(panel);
            SetRef(so, "hostModal", window, dryRun);

            // ── Pool list ──
            var list = FindChild(content, PoolListName);
            if (list)
            {
                var listView = Ensure<MaelstromPoolListView>(list.gameObject, dryRun, _wrote);
                SetRef(so, "poolList", listView, dryRun);

                var lso = new SerializedObject(listView);
                SetRef(lso, "rowContainer", list, dryRun);

                var row = FindChild(list, PoolRowName);
                if (row)
                {
                    var prefab = MakeRowPrefab<MaelstromPoolEntry>(row.gameObject, "MaelstromPoolRow",
                                                                    dryRun, written, WirePoolEntry);
                    SetRef(lso, "rowPrefab", prefab, dryRun);
                }
                else
                {
                    _todo.Add($"No '{PoolRowName}' under '{PoolListName}'. Author ONE row and " +
                              "re-run.");
                }

                var tournament = LoadOne<TournamentDataSO>();
                if (tournament) SetRef(lso, "tournamentData", tournament, dryRun);
                else _problems.Add("No TournamentDataSO found - the pool list has nothing to read.");

                if (!dryRun) lso.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                _todo.Add($"No '{PoolListName}' under the Maelstrom's '{ConfigContentName}'.");
            }

            WireGameView(so, FindChild(content, GameViewName), dryRun, wantVideo: true);
            WireModalControls(so, FindChild(content, ConfigDetailName), dryRun,
                              $"{MaelstromModalName}/{ConfigDetailName}");

            if (!dryRun) so.ApplyModifiedPropertiesWithoutUndo();
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
                _todo.Add($"No '{GameViewName}' under '{panel.targetObject.name}' - no title, " +
                          "briefing or preview will be drawn.");
                return;
            }

            SetRef(panel, "gameNameText", FindComponentInChildren<TMP_Text>(gameView, "Game Name"), dryRun);
            SetRef(panel, "favoriteIcon", gameView.GetComponentInChildren<FavoriteIcon>(true), dryRun);

            var briefing = Ensure<GameBriefingView>(gameView.gameObject, dryRun, _wrote);
            SetRef(panel, "briefing", briefing, dryRun);

            var bso = new SerializedObject(briefing);
            SetRef(bso, "descriptionText",
                   FindComponentInChildren<TMP_Text>(gameView, "Game Description"), dryRun);
            SetRef(bso, "tipText", FindComponentInChildren<TMP_Text>(gameView, "Tip"), dryRun);
            if (!dryRun) bso.ApplyModifiedPropertiesWithoutUndo();

            if (FindComponentInChildren<TMP_Text>(gameView, "Tip") == null)
                _todo.Add($"'{Path(gameView)}' has no child named 'Tip' - the rotating play tip " +
                          "has nowhere to draw. Add a TMP_Text called 'Tip' and re-run. (Cards " +
                          "also need SO_ArcadeGame.Tips written; every one is empty today.)");

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

                var vso = new SerializedObject(view);
                SetRef(vso, "surface", preview.GetComponentInChildren<RawImage>(true), dryRun);
                if (!dryRun) vso.ApplyModifiedPropertiesWithoutUndo();

                if (preview.GetComponentInChildren<RawImage>(true) == null)
                    _todo.Add($"'{Path(preview)}' has no RawImage - the clip has no surface to " +
                              "render into. Add one and re-run.");

                _todo.Add("Assign the Maelstrom card's clip: ArcadeGameTournament.asset → " +
                          "Preview Video. It is the ONLY card that gets one - every other mode " +
                          "previews live and must never fall back to a video.");
                return;
            }

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

            SetList(panel, "intensityButtons",
                    detail.GetComponentsInChildren<IntensitySelectButton>(true)
                          .OrderBy(b => b.transform.GetSiblingIndex()).ToArray(), dryRun);

            SetList(panel, "domainTiles",
                    detail.GetComponentsInChildren<DomainInfoData>(true).ToArray(), dryRun);

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
            var so = new SerializedObject(modal);
            var list = so.FindProperty("launchPanels");
            if (list == null)
            {
                _problems.Add("ArcadeGameConfigureModal has no 'launchPanels' field - the code on " +
                              "this branch is older than this tool.");
                return;
            }

            var panels = new List<ArcadeLaunchPanel>();
            if (minigame) panels.Add(minigame);
            if (maelstrom) panels.Add(maelstrom);
            if (panels.Count == 0) return;

            if (!dryRun)
            {
                list.arraySize = panels.Count;
                for (int i = 0; i < panels.Count; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = panels[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
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

            var so = new SerializedObject(switcher);
            var list = so.FindProperty("Modals");
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
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            _wrote.Add("ScreenSwitcher.Modals += the Maelstrom modal");
        }

        // ── Row prefabs ──────────────────────────────────────────────────────────

        /// <summary>
        /// Turn a hand-authored row into the prefab its list instantiates, wire the component on it,
        /// and remove the scene copy - the panel builds its own rows from the prefab, so leaving the
        /// original behind would draw one row nothing drives.
        /// </summary>
        GameObject MakeRowPrefab<T>(GameObject source, string prefabName, bool dryRun,
                                    List<string> written, Action<T> wire) where T : Component
        {
            string path = $"{RowPrefabFolder}/{prefabName}.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing)
            {
                _found.Add($"Row prefab already exists: {path}");
                return existing;
            }

            if (dryRun)
            {
                _wrote.Add($"WOULD create {path} from '{Path(source.transform)}' and delete the " +
                           "scene copy.");
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

            Undo.DestroyObjectImmediate(source);
            written.Add(path);
            _wrote.Add($"{path} (and removed the scene copy - the panel instantiates rows itself)");
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

        static void WirePoolEntry(MaelstromPoolEntry entry)
        {
            var so = new SerializedObject(entry);
            var t = entry.transform;
            SetRefStatic(so, "icon", t.GetComponentInChildren<Image>(true));
            SetRefStatic(so, "nameText", FindComponentInChildren<TMP_Text>(t, "GameTitle")
                                         ?? t.GetComponentInChildren<TMP_Text>(true));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Standing instructions ────────────────────────────────────────────────

        void AddStandingTodos()
        {
            _todo.Add("DELETE from the arcade modal once this run looks right: GameDetailView, " +
                      "SquadMateSelection, VesselSelection. They are the old second screen and " +
                      "nothing reads them any more.");
            _todo.Add("Author the flight rows on VesselControlsPanel > Flight Controls: two " +
                      "placeholders ship (left stick / right stick) with no icons. They are the " +
                      "one part of the controls block that cannot be derived - an axis belongs to " +
                      "the input scheme, not to a vessel.");
            _todo.Add("PLAY-TEST: open a minigame card (panel + live preview + controls sweep), " +
                      "change intensity (the preview rebuilds only for Ribcage / Dog Fight / The " +
                      "Bends / Wildlife Liberation), open the Maelstrom card (its own window, clip, " +
                      "pool list growing with intensity), and close both ways - the X and gamepad B.");
        }

        // ── Small helpers ────────────────────────────────────────────────────────

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
