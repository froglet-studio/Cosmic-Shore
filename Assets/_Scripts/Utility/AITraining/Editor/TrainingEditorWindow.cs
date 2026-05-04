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
            PolicyBootstrap.EnsureInitialized();
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

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Asset Wiring", EditorStyles.boldLabel);
                _scenario = (TrainingScenarioSO)EditorGUILayout.ObjectField("Scenario", _scenario, typeof(TrainingScenarioSO), false);
                _state = (TrainingSessionStateSO)EditorGUILayout.ObjectField("Session State", _state, typeof(TrainingSessionStateSO), false);
                _archive = (TrainingArchiveSO)EditorGUILayout.ObjectField("Archive", _archive, typeof(TrainingArchiveSO), false);
                _telemetry = (TrainingTelemetrySO)EditorGUILayout.ObjectField("Telemetry", _telemetry, typeof(TrainingTelemetrySO), false);

                if (_scenario == null)
                {
                    EditorGUILayout.HelpBox("Assign a TrainingScenarioSO. Right-click in Project: Create → ScriptableObjects → AI Training → Scenario.", MessageType.Info);
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

                    bool defaultOn = GeneRegistry.DefaultEnabledModules.Contains(kv.Key);
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
                    EditorGUILayout.LabelField($"  {e.Key} → fitness {e.Fitness:F1}, gen {e.Generation}");
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
