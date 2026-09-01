#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Utility.AITraining.Editor
{
    /// <summary>
    /// FrogletTools / AI Training. The single user-facing surface for the framework.
    ///
    /// Tabs:
    ///   - Run     — pick a scenario + state asset, press Start
    ///   - Search  — see the live gene registry and which modules are toggled on
    ///   - Archive — review/edit per (vessel × game × intensity) deployments
    ///   - Schedule — queue multiple scenarios for unattended overnight rotation
    ///
    /// All UI reads are cheap (no per-paint scene scans). The window auto-repaints
    /// while the runner is active by polling its IsRunning flag.
    /// </summary>
    public class TrainingEditorWindow : EditorWindow
    {
        enum Tab { Run, Search, Archive, Schedule }
        Tab _tab = Tab.Run;

        // Run tab
        TrainingScenarioSO _scenario;
        TrainingSessionStateSO _state;
        TrainingArchiveSO _archive;
        TrainingTelemetrySO _telemetry;
        Vector2 _runScroll;

        // Search tab
        Vector2 _searchScroll;
        string _moduleFilter = "";

        // Archive tab
        Vector2 _archiveScroll;
        VesselClassType _archiveVessel = VesselClassType.Manta;
        GameModes _archiveGame = GameModes.HexRace;
        int _archiveIntensity = 4;

        // Schedule tab
        readonly List<TrainingScenarioSO> _schedule = new();
        Vector2 _scheduleScroll;
        int _scheduleEpisodesPerEntry = 200;

        TrainingSessionRunner _activeRunner;

        [MenuItem("FrogletTools/AI Training", false, 21)]
        public static void Open()
        {
            var w = GetWindow<TrainingEditorWindow>("AI Training");
            w.minSize = new Vector2(640, 460);
            w.Show();
        }

        void OnEnable()
        {
            PilotTuningGenes.EnsureRegistered();
            AutoDiscoverAssets();
        }

        /// <summary>
        /// Looks for matching SO assets anywhere in the project so the user doesn't
        /// have to drag four references in by hand on first open. Runs at every
        /// OnEnable but only assigns slots that are currently empty.
        /// </summary>
        void AutoDiscoverAssets()
        {
            if (_scenario == null) _scenario = FirstAssetOfType<TrainingScenarioSO>();
            if (_state == null) _state = FirstAssetOfType<TrainingSessionStateSO>();
            if (_archive == null) _archive = FirstAssetOfType<TrainingArchiveSO>();
            if (_telemetry == null) _telemetry = FirstAssetOfType<TrainingTelemetrySO>();
        }

        static T FirstAssetOfType<T>() where T : ScriptableObject
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        void Update()
        {
            // Repaint while running so the user sees fitness updates.
            if (_activeRunner != null && _activeRunner.IsRunning) Repaint();
            else if (Application.isPlaying)
            {
                // Pick up runner once Play is entered and the scene contains one.
                if (_activeRunner == null)
                    _activeRunner = FindAnyObjectByType<TrainingSessionRunner>();
            }
        }

        void OnGUI()
        {
            DrawTabs();
            switch (_tab)
            {
                case Tab.Run: DrawRunTab(); break;
                case Tab.Search: DrawSearchTab(); break;
                case Tab.Archive: DrawArchiveTab(); break;
                case Tab.Schedule: DrawScheduleTab(); break;
            }
        }

        void DrawTabs()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Toggle(_tab == Tab.Run, "Run", EditorStyles.toolbarButton)) _tab = Tab.Run;
                if (GUILayout.Toggle(_tab == Tab.Search, "Search Space", EditorStyles.toolbarButton)) _tab = Tab.Search;
                if (GUILayout.Toggle(_tab == Tab.Archive, "Archive", EditorStyles.toolbarButton)) _tab = Tab.Archive;
                if (GUILayout.Toggle(_tab == Tab.Schedule, "Schedule", EditorStyles.toolbarButton)) _tab = Tab.Schedule;
            }
        }

        // ─────────────────────────────────────────────
        //  Run tab
        // ─────────────────────────────────────────────
        void DrawRunTab()
        {
            using (var scope = new EditorGUILayout.ScrollViewScope(_runScroll))
            {
                _runScroll = scope.scrollPosition;

                DrawLearnHero();

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Asset Wiring", EditorStyles.boldLabel);
                _scenario = (TrainingScenarioSO)EditorGUILayout.ObjectField("Scenario", _scenario, typeof(TrainingScenarioSO), false);
                _state = (TrainingSessionStateSO)EditorGUILayout.ObjectField("Session State", _state, typeof(TrainingSessionStateSO), false);
                _archive = (TrainingArchiveSO)EditorGUILayout.ObjectField("Archive", _archive, typeof(TrainingArchiveSO), false);
                _telemetry = (TrainingTelemetrySO)EditorGUILayout.ObjectField("Telemetry", _telemetry, typeof(TrainingTelemetrySO), false);

                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Quick Setup (advanced)",
                        "Creates the default asset set without entering Play mode. Use Learn for the one-click flow."),
                        GUILayout.Height(22)))
                    {
                        QuickSetup();
                    }
                    if (GUILayout.Button(new GUIContent("Re-Discover Assets",
                        "Searches the project for the assets and assigns the first match in each empty slot."), GUILayout.Height(22)))
                    {
                        AutoDiscoverAssets();
                    }
                }

                if (_scenario == null)
                {
                    EditorGUILayout.HelpBox(
                        "No scenario assigned. Press Learn for the one-click flow, or Quick Setup to create the assets without entering play mode.",
                        MessageType.Info);
                    return;
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Scenario Summary", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Game Mode: {_scenario.GameMode}");
                EditorGUILayout.LabelField($"Vessel: {_scenario.Vessel}");
                EditorGUILayout.LabelField($"Intensity: {_scenario.Intensity}");
                EditorGUILayout.LabelField($"Population: {_scenario.PopulationSize}, Elite: {_scenario.EliteCount}");
                EditorGUILayout.LabelField($"Episode max: {_scenario.MaxEpisodeSeconds}s");

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Search Space (current registry)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Modules: {GeneRegistry.Modules.Count}, Genes: {GeneRegistry.Specs.Count}");

                EditorGUILayout.Space(6);
                if (_state != null)
                {
                    EditorGUILayout.LabelField("Session State", EditorStyles.boldLabel);
                    var pop = _state.Population;
                    EditorGUILayout.LabelField($"Generation: {pop.Generation}");
                    EditorGUILayout.LabelField($"Episodes Completed: {_state.EpisodesCompleted}");
                    EditorGUILayout.LabelField($"Hall of Fame Best Fitness: {_state.HallOfFameBestFitness:F2}");

                    var best = _state.HallOfFameBest;
                    if (best != null)
                        EditorGUILayout.LabelField($"Best summary: {best.Summarize()}");
                }

                EditorGUILayout.Space(10);

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (_activeRunner == null || !_activeRunner.IsRunning)
                        {
                            if (GUILayout.Button("Start Session", GUILayout.Height(28)))
                                StartActiveRunner();
                        }
                        else
                        {
                            if (GUILayout.Button("Stop Session", GUILayout.Height(28)))
                                _activeRunner.StopSession();
                        }

                        using (new EditorGUI.DisabledScope(_state == null || _state.HallOfFameBest == null || _archive == null))
                        {
                            if (GUILayout.Button("Deploy Best to Archive", GUILayout.Height(28)))
                                _activeRunner?.DeployBestToArchive();
                        }
                    }
                }

                if (!Application.isPlaying)
                    EditorGUILayout.HelpBox("Enter Play mode in a game scene that already spawns AI players, then press Start Session.", MessageType.Info);

                DrawLoopStatus();

                if (_telemetry != null && _telemetry.IsRunning)
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Generation: {_telemetry.Generation}");
                    EditorGUILayout.LabelField($"Episodes: {_telemetry.EpisodesCompleted}");
                    EditorGUILayout.LabelField($"Best fitness: {_telemetry.CurrentBestFitness:F2}");
                    EditorGUILayout.LabelField($"Last episode: {_telemetry.LastEpisodeFitness:F2}");
                    if (!string.IsNullOrEmpty(_telemetry.LastEpisodeBreakdown))
                        EditorGUILayout.LabelField(_telemetry.LastEpisodeBreakdown);
                }
            }
        }

        /// <summary>
        /// What the loop is doing RIGHT NOW, in the terms the user asked about:
        /// is it in a match, is anything flying, and is the AI actually in control?
        /// A generation counter that has not moved for ten minutes cannot tell you
        /// whether the trainer is thinking hard or wedged on a Ready button; this can.
        /// </summary>
        void DrawLoopStatus()
        {
            if (!Application.isPlaying) return;

            var driver = FindAnyObjectByType<TrainingMatchDriver>();
            var gameData = FindGameData();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Loop", EditorStyles.boldLabel);

            if (driver == null)
            {
                EditorGUILayout.HelpBox(
                    "No match driver yet — still in the menu / loading. It is created when the game scene loads.",
                    MessageType.Info);
                return;
            }

            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            bool turnRunning = gameData != null && gameData.IsTurnRunning;

            int flying = 0, muted = 0, total = 0;
            if (gameData?.Players != null)
            {
                foreach (var p in gameData.Players)
                {
                    if (p?.Vessel == null) continue;
                    total++;
                    var st = p.Vessel.VesselStatus;
                    if (st?.AIPilot != null && st.AutoPilotEnabled) flying++;
                    if (p.InputStatus != null && p.InputStatus.Paused) muted++;
                }
            }

            EditorGUILayout.LabelField($"Scene: {scene}");
            EditorGUILayout.LabelField($"Match: {(turnRunning ? "RUNNING" : "waiting for GO")}");
            EditorGUILayout.LabelField($"Vessels: {flying}/{total} on autopilot, {muted}/{total} human input muted");
            EditorGUILayout.LabelField($"Game speed: {Time.timeScale:0.##}×");

            if (total > 0 && flying < total)
                EditorGUILayout.HelpBox(
                    $"{total - flying} vessel(s) are not on autopilot. The driver re-asserts every frame, so this " +
                    "should clear within a frame of a vessel spawning — if it persists, that vessel has no AIPilot.",
                    MessageType.Warning);
        }

        void StartActiveRunner()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Not Playing", "Enter Play mode first.", "OK");
                return;
            }

            var go = GameObject.Find("[AI Training Runner]");
            if (go == null) go = new GameObject("[AI Training Runner]");
            var runner = go.GetComponent<TrainingSessionRunner>();
            if (runner == null) runner = go.AddComponent<TrainingSessionRunner>();

            var gameData = FindGameData();
            var cellData = FindCellData();
            runner.Configure(_scenario, _state, _archive, _telemetry, gameData, cellData);
            runner.StartSession();
            _activeRunner = runner;
        }

        // ─────────────────────────────────────────────
        //  Learn (one-click)
        // ─────────────────────────────────────────────
        void DrawLearnHero()
        {
            EditorGUILayout.Space(6);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("One-Click Training", EditorStyles.boldLabel);

                bool isPlaying = Application.isPlaying;
                bool hasSession = isPlaying && _activeRunner != null && _activeRunner.IsRunning;

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(hasSession))
                    {
                        var label = isPlaying
                            ? new GUIContent("Learn (running)", "Training is already running.")
                            : new GUIContent("Learn",
                                "Creates default assets if needed, marks the active scenario for auto-launch, " +
                                "enters Play mode, and runs AI-vs-AI matches forever. Press Stop or Unity's " +
                                "Stop button to interrupt — everything except the in-progress match is preserved.");
                        if (GUILayout.Button(label, GUILayout.Height(40)))
                            StartLearn();
                    }

                    using (new EditorGUI.DisabledScope(!isPlaying))
                    {
                        if (GUILayout.Button(new GUIContent("Stop",
                            "Stops the running session and exits Play mode. The in-progress match is discarded; " +
                            "all completed matches are already saved."), GUILayout.Height(40), GUILayout.Width(120)))
                            StopLearn();
                    }
                }

                if (!isPlaying)
                {
                    EditorGUILayout.HelpBox(
                        "Press Learn. The tool will create defaults, enter Play, drive Bootstrap → Auth → Menu → " +
                        "the scenario's game scene, and run AI-vs-AI matches in a loop. Walk away. Come back. " +
                        "Press Stop. Your archive holds the best of every completed match.",
                        MessageType.None);
                }
                else if (hasSession)
                {
                    EditorGUILayout.LabelField($"Generation {_telemetry?.Generation ?? 0}, " +
                        $"Episode {_telemetry?.EpisodesCompleted ?? 0}, " +
                        $"Best fitness {_telemetry?.CurrentBestFitness ?? 0:F1}");
                }
            }
        }

        void StartLearn()
        {
            // Step 1: ensure default assets exist.
            RunQuickSetup(focusWindow: false);
            AutoDiscoverAssets();

            // Step 2: ensure a TrainingControlSO exists, point it at the active scenario,
            // and flip AutoStartOnPlay. The play-mode hook (TrainingPlayModeHook) reads
            // this when EnteredPlayMode fires and creates the auto-launcher.
            var control = TrainingPlayModeHook.FindControlAsset();
            if (control == null)
                control = LoadOrCreateAsset<TrainingControlSO>(QuickSetupRoot + "/TrainingControl.asset", _ => { });
            control.Scenario = _scenario;
            control.State = _state;
            control.Archive = _archive;
            control.Telemetry = _telemetry;
            control.AutoStartOnPlay = true;
            EditorUtility.SetDirty(control);
            AssetDatabase.SaveAssets();

            // Step 3: OPEN BOOTSTRAP, THEN PLAY.
            //
            // EditorApplication.isPlaying plays whatever scenes are currently loaded in
            // the hierarchy — NOT the first scene in Build Settings. Pressing Learn from
            // a game scene, a tool scene or an empty one therefore skipped AppManager
            // entirely: no DI, no auth, no ApplicationState, no OnClientReady, and the
            // launcher sat waiting for a menu that was never going to load. The tool has
            // to put the editor in the state the flow assumes rather than assume it.
            if (!EnsureBootstrapSceneOpen()) return;

            EditorApplication.isPlaying = true;
        }

        /// <summary>
        /// Opens the first enabled scene in Build Settings (Bootstrap) so the normal
        /// app flow runs. Returns false if the user cancelled the save prompt or the
        /// build settings have no enabled scene — in both cases we do NOT enter play
        /// mode, because a half-set-up run is worse than no run.
        /// </summary>
        static bool EnsureBootstrapSceneOpen()
        {
            string bootstrapPath = EditorBuildSettings.scenes
                .FirstOrDefault(s => s != null && s.enabled && !string.IsNullOrEmpty(s.path))?.path;

            if (string.IsNullOrEmpty(bootstrapPath))
            {
                Debug.LogError("[Training] No enabled scene in Build Settings — cannot start the boot flow. " +
                               "Add Bootstrap as the first build scene and press Learn again.");
                return false;
            }

            var active = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (active.path == bootstrapPath)
                return true;

            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;   // user cancelled

            Debug.Log($"[Training] Opening '{System.IO.Path.GetFileNameWithoutExtension(bootstrapPath)}' so the boot flow can run.");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                bootstrapPath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            return true;
        }

        void StopLearn()
        {
            if (_activeRunner != null && _activeRunner.IsRunning)
                _activeRunner.StopSession();
            // Exiting play mode triggers the hook's HandleExiting which clears AutoStartOnPlay.
            EditorApplication.isPlaying = false;
        }

        // ─────────────────────────────────────────────
        //  Quick Setup
        // ─────────────────────────────────────────────

        const string QuickSetupRoot = "Assets/_SO_Assets/AI Training";

        [MenuItem("FrogletTools/AI Training/Quick Setup", false, 22)]
        public static void QuickSetupMenuItem() => RunQuickSetup(focusWindow: true);

        void QuickSetup()
        {
            RunQuickSetup(focusWindow: false);
            // After setup, snap the just-created assets into our wiring slots.
            _scenario = FirstAssetOfType<TrainingScenarioSO>();
            _state = FirstAssetOfType<TrainingSessionStateSO>();
            _archive = FirstAssetOfType<TrainingArchiveSO>();
            _telemetry = FirstAssetOfType<TrainingTelemetrySO>();
            Repaint();
        }

        /// <summary>
        /// Creates (or loads) the standard default asset set under
        /// <see cref="QuickSetupRoot"/>: a Scenario, a Session State, an Archive, a
        /// Telemetry container, and a Fitness Profile. Wires them together so the
        /// Run tab is one click away from training.
        ///
        /// Idempotent — running it twice does not overwrite existing assets, just
        /// re-fills any empty cross-references.
        /// </summary>
        public static void RunQuickSetup(bool focusWindow)
        {
            EnsureFolder(QuickSetupRoot);

            var fitness = LoadOrCreateAsset<FitnessProfileSO>(QuickSetupRoot + "/FitnessProfile_Default.asset",
                so => so.ApplyRacingDefaults());

            var scenario = LoadOrCreateAsset<TrainingScenarioSO>(QuickSetupRoot + "/Scenario_HexRace_Manta.asset",
                _ => { /* TrainingScenarioSO.Reset already populates defaults */ });
            if (scenario.FitnessProfile == null)
            {
                scenario.FitnessProfile = fitness;
                EditorUtility.SetDirty(scenario);
            }

            var state = LoadOrCreateAsset<TrainingSessionStateSO>(QuickSetupRoot + "/SessionState.asset",
                so => so.ResetForScenario(scenario.Key, scenario));

            var archive = LoadOrCreateAsset<TrainingArchiveSO>(QuickSetupRoot + "/Archive.asset", _ => { });
            var telemetry = LoadOrCreateAsset<TrainingTelemetrySO>(QuickSetupRoot + "/Telemetry.asset", _ => { });

            // Wire up the control asset so the Learn button + play-mode hook know
            // which scenario to drive. AutoStartOnPlay stays false until Learn is pressed.
            var control = LoadOrCreateAsset<TrainingControlSO>(QuickSetupRoot + "/TrainingControl.asset", _ => { });
            if (control.Scenario == null) control.Scenario = scenario;
            if (control.State == null) control.State = state;
            if (control.Archive == null) control.Archive = archive;
            if (control.Telemetry == null) control.Telemetry = telemetry;
            EditorUtility.SetDirty(control);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (focusWindow)
            {
                var w = GetWindow<TrainingEditorWindow>("AI Training");
                w.AutoDiscoverAssets();
                w.Repaint();
            }

            Debug.Log($"[AI Training] Quick Setup complete. Assets at: {QuickSetupRoot}\n" +
                      $"Scenario: {scenario.name}, State: {state.name}, Fitness: {fitness.name}.");
        }

        static T LoadOrCreateAsset<T>(string path, System.Action<T> initialize) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var so = ScriptableObject.CreateInstance<T>();
            initialize?.Invoke(so);
            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var parent = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(assetPath);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static CosmicShore.Utility.GameDataSO FindGameData()
        {
            // Pick the first GameDataSO present in the project.
            var guids = AssetDatabase.FindAssets("t:GameDataSO");
            if (guids.Length == 0) return null;
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<CosmicShore.Utility.GameDataSO>(path);
        }

        static CosmicShore.Utility.CellRuntimeDataSO FindCellData()
        {
            var guids = AssetDatabase.FindAssets("t:CellRuntimeDataSO");
            if (guids.Length == 0) return null;
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<CosmicShore.Utility.CellRuntimeDataSO>(path);
        }

        // ─────────────────────────────────────────────
        //  Search tab
        // ─────────────────────────────────────────────
        void DrawSearchTab()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Registered Search Space", EditorStyles.boldLabel);
            _moduleFilter = EditorGUILayout.TextField("Filter (module or gene)", _moduleFilter);

            EditorGUILayout.Space(6);
            using (var scope = new EditorGUILayout.ScrollViewScope(_searchScroll))
            {
                _searchScroll = scope.scrollPosition;
                foreach (var kv in GeneRegistry.Modules.OrderBy(m => m.Key))
                {
                    if (!string.IsNullOrEmpty(_moduleFilter)
                        && !kv.Key.ToLower().Contains(_moduleFilter.ToLower())
                        && !kv.Value.Any(g => g.ToLower().Contains(_moduleFilter.ToLower())))
                        continue;

                    bool defaultOn = GeneRegistry.IsDefaultEnabled(kv.Key);
                    EditorGUILayout.LabelField(
                        $"Module: {kv.Key} {(defaultOn ? "[default-on]" : "[default-off]")}",
                        EditorStyles.boldLabel);

                    using (new EditorGUI.IndentLevelScope())
                    {
                        foreach (var geneName in kv.Value)
                        {
                            if (!GeneRegistry.TryGetSpec(geneName, out var spec)) continue;
                            EditorGUILayout.LabelField($"{spec.Name}  range=[{spec.Min:F3}, {spec.Max:F3}]  default={spec.Default:F3}");
                        }
                    }
                    EditorGUILayout.Space(4);
                }
            }
        }

        // ─────────────────────────────────────────────
        //  Archive tab
        // ─────────────────────────────────────────────
        void DrawArchiveTab()
        {
            EditorGUILayout.Space(6);
            _archive = (TrainingArchiveSO)EditorGUILayout.ObjectField("Archive", _archive, typeof(TrainingArchiveSO), false);
            if (_archive == null)
            {
                EditorGUILayout.HelpBox("Assign a TrainingArchiveSO.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Total Entries: {_archive.Entries.Count}", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _archiveVessel = (VesselClassType)EditorGUILayout.EnumPopup("Vessel", _archiveVessel);
                _archiveGame = (GameModes)EditorGUILayout.EnumPopup("Mode", _archiveGame);
                _archiveIntensity = EditorGUILayout.IntSlider("Intensity", _archiveIntensity, 1, 4);
            }

            var entry = _archive.Find(_archiveVessel, _archiveGame, _archiveIntensity);
            if (entry != null)
            {
                EditorGUILayout.LabelField($"Fitness: {entry.Fitness:F2}");
                EditorGUILayout.LabelField($"Trained: {entry.TrainedUtc}");
                EditorGUILayout.LabelField($"Generation: {entry.Generation}");
                EditorGUILayout.LabelField($"Genome: {entry.Genome?.Summarize()}");
                if (entry.Roster != null && entry.Roster.Count > 0)
                {
                    EditorGUILayout.LabelField($"Roster ({entry.Roster.Count} personalities):", EditorStyles.boldLabel);
                    foreach (var g in entry.Roster)
                        EditorGUILayout.LabelField($"  {PilotTuningGenes.PersonalityName(g)}  fit={g.Fitness:F1}");
                }
                if (GUILayout.Button("Export JSON…"))
                {
                    var path = EditorUtility.SaveFilePanel("Export Genome",
                        Application.dataPath, $"{entry.Key}.json", "json");
                    if (!string.IsNullOrEmpty(path)) GenomeJson.SaveToFile(entry.Genome, path);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No entry yet for this combination.", MessageType.Info);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("All Entries", EditorStyles.boldLabel);
            using (var scope = new EditorGUILayout.ScrollViewScope(_archiveScroll, GUILayout.Height(220)))
            {
                _archiveScroll = scope.scrollPosition;
                foreach (var e in _archive.Entries.OrderBy(x => x.Key))
                    EditorGUILayout.LabelField($"  {e.Key} → fitness {e.Fitness:F1}, gen {e.Generation}, roster {e.Roster?.Count ?? 0}");
            }

            if (GUILayout.Button("Import Genome From JSON…"))
            {
                var path = EditorUtility.OpenFilePanel("Import Genome", Application.dataPath, "json");
                if (!string.IsNullOrEmpty(path))
                {
                    var g = GenomeJson.LoadFromFile(path);
                    if (g != null)
                    {
                        _archive.Upsert(_archiveVessel, _archiveGame, _archiveIntensity, g, g.Fitness, g.GenerationBorn, "Imported");
                        EditorUtility.SetDirty(_archive);
                    }
                }
            }
        }

        // ─────────────────────────────────────────────
        //  Schedule tab
        // ─────────────────────────────────────────────
        void DrawScheduleTab()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Overnight Schedule", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Add scenarios to the queue. The runner will train each one for the configured number of episodes, then move to the next. Best genome from each is auto-deployed to the archive between rotations.",
                MessageType.Info);

            using (var scope = new EditorGUILayout.ScrollViewScope(_scheduleScroll, GUILayout.Height(200)))
            {
                _scheduleScroll = scope.scrollPosition;
                for (int i = 0; i < _schedule.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _schedule[i] = (TrainingScenarioSO)EditorGUILayout.ObjectField(_schedule[i], typeof(TrainingScenarioSO), false);
                        if (GUILayout.Button("X", GUILayout.Width(24))) { _schedule.RemoveAt(i); i--; }
                    }
                }
            }

            if (GUILayout.Button("+ Add Slot")) _schedule.Add(null);

            _scheduleEpisodesPerEntry = EditorGUILayout.IntSlider("Episodes per scenario", _scheduleEpisodesPerEntry, 10, 5000);

            EditorGUILayout.Space(6);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || _schedule.All(s => s == null)))
            {
                if (GUILayout.Button("Run Schedule", GUILayout.Height(28)))
                    EditorUtility.DisplayDialog("Schedule",
                        "Schedule queueing is configuration-only in this version: enable startOnEnable on the runner inside the desired scene to chain scenarios. " +
                        "A future iteration will drive the chain from this window.", "OK");
            }
        }
    }
}
#endif
