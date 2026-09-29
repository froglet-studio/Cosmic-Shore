#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Utility.AITraining.Editor
{
    /// <summary>
    /// FrogletTools / AI Training. Keeper window (re-runnable). WRITER: Quick Setup
    /// creates the asset set under Assets/_SO_Assets/AI Training when it is missing,
    /// and Configure / Deploy / import write the scenario, control, and archive.
    /// Those writes are ledger-recorded and the ship panel is drawn. Do not retire
    /// this window — it is the operator surface.
    ///
    /// Tabs: Configure, Schedule, Inspect, Archive, Deploy. Learn and Stop sit above every tab.
    /// Learn runs one scenario. Learn the queue runs the schedule and resumes its cursor.
    /// </summary>
    public class TrainingEditorWindow : EditorWindow
    {
        enum Tab { Configure, Schedule, Inspect, Archive, Deploy }
        Tab _tab = Tab.Configure;

        TrainingScenarioSO _scenario;
        TrainingSessionStateSO _state;
        TrainingArchiveSO _archive;
        TrainingTelemetrySO _telemetry;
        TrainingControlSO _control;
        TrainingScheduleSO _schedule;

        Vector2 _scroll;
        Vector2 _archiveScroll;
        bool _showGenes;
        string _moduleFilter = "";

        VesselClassType _archiveVessel = VesselClassType.Squirrel;
        GameModes _archiveGame = GameModes.HexRace;
        int _archiveIntensity = 4;
        int _catalogIndex;

        TrainingSessionRunner _activeRunner;
        double _rateAnchor;
        int _rateAnchorEpisodes = -1;
        double _secondsPerEpisode;
        bool _launchQueued;

        const string ToolName = "AI Training";
        const string QuickSetupRoot = "Assets/_SO_Assets/AI Training";

        static readonly FrogletToolShipContext Ship = new FrogletToolShipContext(ToolName)
        {
            ToolScriptPaths = new[] { "Assets/_Scripts/Utility/AITraining/Editor/TrainingEditorWindow.cs" },
        };

        [MenuItem("FrogletTools/AI Training", false, 21)]
        [FrogletTool(FrogletToolCategory.GameModes, Importance = 4,
            Description = "Learn a mode overnight, inspect the best pilot, and play against the archive.",
            DocPath = "Assets/_Scripts/Utility/AITraining/README.md#operator")]
        public static void Open()
        {
            var w = GetWindow<TrainingEditorWindow>("AI Training");
            w.minSize = new Vector2(680, 520);
            w.Show();
        }

        void OnEnable()
        {
            PolicyBootstrap.EnsureInitialized();
            AutoDiscoverAssets();
            NormalizeControlDefaults();
            SyncArchiveFilterFromScenario();
        }

        void AutoDiscoverAssets()
        {
            _control = TrainingPlayModeHook.FindControlAsset();
            if (_scenario == null && _control != null && _control.Scenario != null)
                _scenario = _control.Scenario;
            if (_scenario == null && TrainingModeCatalog.Live.Count > 0)
                _scenario = AssetDatabase.LoadAssetAtPath<TrainingScenarioSO>(TrainingModeCatalog.Live[0].ScenarioPath);
            if (_scenario == null) _scenario = FirstAssetOfType<TrainingScenarioSO>();
            if (_state == null && _control != null && _control.State != null) _state = _control.State;
            if (_state == null) _state = FirstAssetOfType<TrainingSessionStateSO>();
            if (_archive == null && _control != null && _control.Archive != null) _archive = _control.Archive;
            if (_archive == null) _archive = FirstAssetOfType<TrainingArchiveSO>();
            if (_telemetry == null && _control != null && _control.Telemetry != null) _telemetry = _control.Telemetry;
            if (_telemetry == null) _telemetry = FirstAssetOfType<TrainingTelemetrySO>();
            if (_schedule == null && _control != null && _control.Schedule != null) _schedule = _control.Schedule;
            if (_schedule == null)
                _schedule = AssetDatabase.LoadAssetAtPath<TrainingScheduleSO>(QuickSetupRoot + "/Schedule.asset");
            _catalogIndex = CatalogIndexOf(_scenario);
        }

        static T FirstAssetOfType<T>() where T : ScriptableObject
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                _activeRunner = null;
                _rateAnchorEpisodes = -1;
                return;
            }

            if (_activeRunner == null)
                _activeRunner = FindAnyObjectByType<TrainingSessionRunner>();
            int completed = _state != null ? _state.EpisodesCompleted : 0;
            NoteProgress(completed);
            Repaint();
        }

        void NoteProgress(int completed)
        {
            double now = EditorApplication.timeSinceStartup;
            if (_rateAnchorEpisodes < 0)
            {
                _rateAnchor = now;
                _rateAnchorEpisodes = completed;
                return;
            }

            int delta = completed - _rateAnchorEpisodes;
            if (delta <= 0) return;
            _secondsPerEpisode = (now - _rateAnchor) / delta;
        }

        /// <summary>
        /// Operator ETA. A non-positive target is an overnight run. The rate is
        /// measured from completed episodes while the window is open; until the
        /// first one finishes the label is a count only.
        /// </summary>
        public static string FormatEta(int completed, int target, double secondsPerEpisode)
        {
            if (target <= 0) return "Overnight — stops when you press Stop";
            int left = Mathf.Max(0, target - completed);
            if (left == 0) return "Target reached";
            if (secondsPerEpisode <= 0.01) return left + " episodes left";
            int seconds = Mathf.CeilToInt((float)(left * secondsPerEpisode));
            if (seconds >= 3600) return $"{seconds / 3600}h {(seconds % 3600) / 60}m";
            if (seconds >= 60) return $"{seconds / 60}m {seconds % 60}s";
            return seconds + "s";
        }

        void OnGUI()
        {
            var accent = FrogletEditorPalette.ColorFor(FrogletToolCategory.GameModes);
            FrogletEditorPalette.Banner(
                "AI Training",
                "Learn a mode, stop whenever you want, then play against the best pilot.",
                accent);

            DrawActions(accent);
            DrawTabs();

            using (var scope = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scope.scrollPosition;
                switch (_tab)
                {
                    case Tab.Configure: DrawConfigure(); break;
                    case Tab.Schedule: DrawSchedule(); break;
                    case Tab.Inspect: DrawInspect(); break;
                    case Tab.Archive: DrawArchive(); break;
                    case Tab.Deploy: DrawDeploy(); break;
                }
            }

            FrogletToolShipPanel.Draw(Ship, this);
        }

        void DrawActions(Color accent)
        {
            bool playing = Application.isPlaying;
            bool training = playing && _activeRunner != null && _activeRunner.IsRunning;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton("Learn", FrogletEditorPalette.Ok, 160, 32,
                        "Creates the training assets if they are missing, enters Play, and races AI against AI.",
                        enabled: !training))
                    Queue(StartLearn);

                if (FrogletEditorPalette.ColorButton("Stop", FrogletEditorPalette.Error, 120, 32,
                        "Saves every finished match, drops the one in progress, and leaves Play.",
                        enabled: playing))
                    Queue(StopLearn);

                GUILayout.FlexibleSpace();
                string pill = training ? "LEARNING" : playing ? "IN PLAY" : "IDLE";
                var pillColor = training ? FrogletEditorPalette.Ok : playing ? FrogletEditorPalette.Info : FrogletEditorPalette.Muted;
                var pillRect = GUILayoutUtility.GetRect(88, 22, GUILayout.Width(88), GUILayout.Height(22));
                pillRect.y += 5;
                FrogletEditorPalette.StatusPill(pillRect, pill, pillColor);
            }

            if (!playing)
            {
                EditorGUILayout.HelpBox(
                    "Learn enters Play and races the scenario with every seat on autopilot. " +
                    "Stop keeps every finished match and drops the one still in the air. " +
                    "Press Learn again to continue. A plain Play button does not resume training.",
                    MessageType.None);
            }
        }

        void DrawTabs()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Toggle(_tab == Tab.Configure, "Configure", EditorStyles.toolbarButton)) _tab = Tab.Configure;
                if (GUILayout.Toggle(_tab == Tab.Schedule, "Schedule", EditorStyles.toolbarButton)) _tab = Tab.Schedule;
                if (GUILayout.Toggle(_tab == Tab.Inspect, "Inspect", EditorStyles.toolbarButton)) _tab = Tab.Inspect;
                if (GUILayout.Toggle(_tab == Tab.Archive, "Archive", EditorStyles.toolbarButton)) _tab = Tab.Archive;
                if (GUILayout.Toggle(_tab == Tab.Deploy, "Deploy", EditorStyles.toolbarButton)) _tab = Tab.Deploy;
            }
        }

        void Queue(System.Action action)
        {
            if (_launchQueued) return;
            _launchQueued = true;
            EditorApplication.delayCall += () =>
            {
                _launchQueued = false;
                action();
            };
        }

        // ─────────────────────────────────────────────
        //  Configure
        // ─────────────────────────────────────────────
        void DrawConfigure()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Scenario", EditorStyles.boldLabel);

            var live = TrainingModeCatalog.Live;
            if (live.Count > 0)
            {
                var labels = live.Select(r => r.DisplayName).ToArray();
                int shown = _catalogIndex >= 0 ? _catalogIndex : 0;
                EditorGUI.BeginChangeCheck();
                int next = EditorGUILayout.Popup("Mode", shown, labels);
                if (EditorGUI.EndChangeCheck())
                {
                    _catalogIndex = next;
                    var loaded = AssetDatabase.LoadAssetAtPath<TrainingScenarioSO>(live[next].ScenarioPath);
                    if (loaded != null)
                    {
                        _scenario = loaded;
                        SyncArchiveFilterFromScenario();
                    }
                }
            }

            EditorGUI.BeginChangeCheck();
            _scenario = (TrainingScenarioSO)EditorGUILayout.ObjectField("Scenario asset", _scenario, typeof(TrainingScenarioSO), false);
            if (EditorGUI.EndChangeCheck())
            {
                _catalogIndex = CatalogIndexOf(_scenario);
                SyncArchiveFilterFromScenario();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Quick Setup",
                    "Creates Assets/_SO_Assets/AI Training if it is missing. Does not overwrite assets that already exist.")))
                    QuickSetup();
            }

            if (_scenario == null)
            {
                EditorGUILayout.HelpBox("Press Quick Setup, then pick a mode.", MessageType.Info);
                return;
            }

            EnsureControl();
            FrogletEditorPalette.HorizontalRule();
            EditorGUILayout.LabelField("This run", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _scenario.PopulationSize = EditorGUILayout.IntSlider("Population", _scenario.PopulationSize, 2, 64);
            int eliteMax = Mathf.Max(1, _scenario.PopulationSize);
            _scenario.EliteCount = EditorGUILayout.IntSlider("Elites", Mathf.Clamp(_scenario.EliteCount, 1, eliteMax), 1, eliteMax);

            bool locked = CatalogRowFor(_scenario, out var row) && row.VesselLocked;
            using (new EditorGUI.DisabledScope(locked))
                _scenario.Vessel = (VesselClassType)EditorGUILayout.EnumPopup(locked ? "Vessel (locked)" : "Vessel", _scenario.Vessel);
            _scenario.Intensity = EditorGUILayout.IntSlider("Intensity", Mathf.Clamp(_scenario.Intensity, 1, 4), 1, 4);

            _control.TargetEpisodes = EditorGUILayout.IntField(
                new GUIContent("Target episodes", "−1 runs until you press Stop."),
                _control.TargetEpisodes);
            _control.WatchdogSeconds = EditorGUILayout.Slider(
                new GUIContent("Watchdog (seconds)", "Force-ends a match that never reaches its objective and records it as a timeout. Wall-clock, so a frozen time scale still fires. Separate from the episode cap."),
                _control.WatchdogSeconds <= 0f ? 180f : _control.WatchdogSeconds, 30f, 600f);
            _control.SimulationTimeScale = EditorGUILayout.FloatField(
                new GUIContent("Time scale", "Host-only training. 0 or 1 leaves the clock alone. Above 1 speeds the simulation, capped at 8. Not applied while another client is connected, and not applied for Play against trained AI."),
                _control.SimulationTimeScale);
            _control.MuteAudio = EditorGUILayout.Toggle(
                new GUIContent("Mute audio", "Pauses the audio listener for a training launch and restores it on stop."),
                _control.MuteAudio);
            _control.DisableCameraRendering = EditorGUILayout.Toggle(
                new GUIContent("Disable camera", "Disables Camera.main during a training launch. Does not move the camera or cull mass."),
                _control.DisableCameraRendering);
            bool changed = EditorGUI.EndChangeCheck();
            if (changed)
            {
                EditorUtility.SetDirty(_scenario);
                EditorUtility.SetDirty(_control);
                SyncArchiveFilterFromScenario();
            }

            string overnight = _control.TargetEpisodes <= 0 ? "Overnight (until Stop)." : $"Stops after {_control.TargetEpisodes} finished episodes.";
            EditorGUILayout.HelpBox(
                $"{overnight} Episode cap is {_scenario.MaxEpisodeSeconds:0}s. Watchdog is {_control.WatchdogSeconds:0}s. " +
                "Population and elites apply the next time this scenario's session is empty or its vessel / mode / intensity key changes. " +
                "Reset session if you want those sizes now.",
                MessageType.None);

            if (GUILayout.Button("Save settings"))
                SaveOperatorAssets();

            if (GUILayout.Button("Reset session to these settings"))
            {
                if (EditorUtility.DisplayDialog(
                        "Reset training session",
                        "This clears the population, hall of fame, episode count, and the schedule cursor. The archive is kept.",
                        "Reset", "Cancel"))
                    ResetSession();
            }

            FrogletEditorPalette.HorizontalRule();
            _showGenes = EditorGUILayout.Foldout(_showGenes, "Gene search space");
            if (_showGenes) DrawGenes();
        }

        void DrawGenes()
        {
            _moduleFilter = EditorGUILayout.TextField("Filter", _moduleFilter);
            foreach (var kv in GeneRegistry.Modules.OrderBy(m => m.Key))
            {
                if (!string.IsNullOrEmpty(_moduleFilter)
                    && !kv.Key.ToLower().Contains(_moduleFilter.ToLower())
                    && !kv.Value.Any(g => g.ToLower().Contains(_moduleFilter.ToLower())))
                    continue;

                bool defaultOn = GeneRegistry.IsDefaultEnabled(kv.Key);
                EditorGUILayout.LabelField($"Module: {kv.Key} {(defaultOn ? "[default-on]" : "[default-off]")}", EditorStyles.boldLabel);
                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (var geneName in kv.Value)
                    {
                        if (!GeneRegistry.TryGetSpec(geneName, out var spec)) continue;
                        EditorGUILayout.LabelField($"{spec.Name}  [{spec.Min:F3}, {spec.Max:F3}]  default {spec.Default:F3}");
                    }
                }
            }
        }

        // ─────────────────────────────────────────────
        //  Schedule
        // ─────────────────────────────────────────────
        void DrawSchedule()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "One asset runs the night. A slot advances when its episode cap or its wall-clock budget is hit. " +
                "Halt leaves the cursor on the same slot. Each scenario keeps its own archive bucket. " +
                "A crashed slot is logged, retried, then skipped. Learn above still runs one scenario.",
                MessageType.None);

            _schedule = (TrainingScheduleSO)EditorGUILayout.ObjectField(
                "Schedule", _schedule, typeof(TrainingScheduleSO), false);

            if (_schedule == null)
            {
                if (GUILayout.Button("Create schedule"))
                {
                    _schedule = LoadOrCreateAsset<TrainingScheduleSO>(
                        QuickSetupRoot + "/Schedule.asset", SeedOvernightExample);
                    Record(_schedule);
                    AssetDatabase.SaveAssets();
                }
                return;
            }

            if (_state != null)
            {
                string where = _state.ScheduleIndex >= _schedule.Count
                    ? "finished"
                    : $"slot {_state.ScheduleIndex + 1} of {_schedule.Count}";
                EditorGUILayout.LabelField("Resume",
                    $"{where}   evaluations {_state.ScheduleSlotEpisodes}   " +
                    $"wall {_state.ScheduleSlotElapsedSeconds / 3600d:0.00} h   " +
                    $"failures {_state.ScheduleSlotAttempts}");
            }

            if (_schedule.Slots == null) _schedule.Slots = new System.Collections.Generic.List<TrainingScheduleSO.Slot>();

            EditorGUI.BeginChangeCheck();
            int remove = -1;
            int move = 0;
            bool mutated = false;
            for (int i = 0; i < _schedule.Slots.Count; i++)
            {
                var slot = _schedule.Slots[i] ?? new TrainingScheduleSO.Slot();
                _schedule.Slots[i] = slot;
                EditorGUILayout.LabelField($"Slot {i + 1}", EditorStyles.boldLabel);
                using (new EditorGUI.IndentLevelScope())
                {
                    slot.Scenario = (TrainingScenarioSO)EditorGUILayout.ObjectField(
                        "Scenario", slot.Scenario, typeof(TrainingScenarioSO), false);
                    slot.TargetEpisodes = EditorGUILayout.IntField("Target episodes (0 = none)", slot.TargetEpisodes);
                    slot.WallClockHours = EditorGUILayout.FloatField("Wall-clock hours (0 = none)", slot.WallClockHours);
                    slot.MaxAttempts = EditorGUILayout.IntField("Attempts before skip", slot.MaxAttempts);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Up") && i > 0) move = -1;
                        if (GUILayout.Button("Down") && i < _schedule.Slots.Count - 1) move = 1;
                        if (GUILayout.Button("Remove")) remove = i;
                    }
                }
                if (move != 0)
                {
                    int j = i + move;
                    var held = _schedule.Slots[i];
                    _schedule.Slots[i] = _schedule.Slots[j];
                    _schedule.Slots[j] = held;
                    mutated = true;
                    break;
                }
            }
            if (remove >= 0)
            {
                _schedule.Slots.RemoveAt(remove);
                mutated = true;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add current scenario"))
                {
                    _schedule.Slots.Add(new TrainingScheduleSO.Slot
                    {
                        Scenario = _scenario,
                        TargetEpisodes = 0,
                        WallClockHours = 4f,
                        MaxAttempts = 2,
                    });
                    mutated = true;
                }
                if (GUILayout.Button("Queue HexRace 4h then Joust 2h"))
                {
                    _schedule.Slots.Clear();
                    SeedOvernightExample(_schedule);
                    mutated = true;
                }
            }

            if (GUILayout.Button("Restart queue (next Learn the queue starts at slot 1)"))
            {
                if (_state != null)
                {
                    _state.ClearScheduleProgress();
                    EditorUtility.SetDirty(_state);
                    Record(_state);
                }
            }

            if (GUILayout.Button("Learn the queue", GUILayout.Height(28)))
                StartSchedule();

            if (EditorGUI.EndChangeCheck() || mutated)
            {
                EditorUtility.SetDirty(_schedule);
                Record(_schedule);
            }
        }

        void StartSchedule()
        {
            RunQuickSetup(focusWindow: false);
            AutoDiscoverAssets();
            EnsureControl();
            if (_schedule == null || _schedule.Count == 0)
            {
                Debug.LogWarning("[Training] The schedule has no scenarios.");
                return;
            }
            if (_state != null && _state.ScheduleIndex >= _schedule.Count)
                _state.ClearScheduleProgress();

            int index = _state != null ? UnityEngine.Mathf.Max(0, _state.ScheduleIndex) : 0;
            var slot = _schedule.Get(index);
            if (slot == null || slot.Scenario == null)
            {
                Debug.LogWarning("[Training] The schedule slot has no scenario.");
                return;
            }

            _scenario = slot.Scenario;
            _control.Schedule = _schedule;
            _control.Scenario = _scenario;
            _control.State = _state;
            _control.Archive = _archive;
            _control.Telemetry = _telemetry;
            _control.HumanPlaysThisLaunch = false;
            _control.AutoStartOnPlay = true;
            SaveOperatorAssets();
            EditorApplication.isPlaying = true;
        }

        static void SeedOvernightExample(TrainingScheduleSO schedule)
        {
            if (schedule.Slots == null)
                schedule.Slots = new System.Collections.Generic.List<TrainingScheduleSO.Slot>();
            AddCatalogSlot(schedule, "HexRace", 4f);
            AddCatalogSlot(schedule, "Joust", 2f);
        }

        static void AddCatalogSlot(TrainingScheduleSO schedule, string token, float hours)
        {
            for (int i = 0; i < TrainingModeCatalog.Live.Count; i++)
            {
                var row = TrainingModeCatalog.Live[i];
                if (row.Token != token) continue;
                var scenario = AssetDatabase.LoadAssetAtPath<TrainingScenarioSO>(row.ScenarioPath);
                if (scenario == null) return;
                schedule.Slots.Add(new TrainingScheduleSO.Slot
                {
                    Scenario = scenario,
                    TargetEpisodes = 0,
                    WallClockHours = hours,
                    MaxAttempts = 2,
                });
                return;
            }
        }

        // ─────────────────────────────────────────────
        //  Inspect
        // ─────────────────────────────────────────────
        void DrawInspect()
        {
            EditorGUILayout.Space(6);
            if (_state == null)
            {
                EditorGUILayout.HelpBox("No session yet. Press Learn.", MessageType.Info);
                return;
            }

            var pop = _state.Population;
            int generation = pop != null ? pop.Generation : 0;
            int novelty = pop != null ? pop.NoveltyArchiveSize : 0;
            int completed = _state.EpisodesCompleted;
            int target = _control != null ? _control.TargetEpisodes : -1;
            if (_telemetry != null && _telemetry.IsRunning)
                target = _telemetry.EpisodesPlanned;

            EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Generation", generation.ToString());
            EditorGUILayout.LabelField("Best fitness", _state.HallOfFameBestFitness.ToString("F2"));
            EditorGUILayout.LabelField("Episodes", completed.ToString());
            EditorGUILayout.LabelField("Novelty archive", novelty.ToString());
            EditorGUILayout.LabelField("ETA", FormatEta(completed, target, _secondsPerEpisode));

            FrogletEditorPalette.HorizontalRule();
            EditorGUILayout.LabelField("Hall of fame", EditorStyles.boldLabel);
            var best = _state.HallOfFameBest;
            if (best == null || completed <= 0)
                EditorGUILayout.LabelField("No finished episode yet.");
            else
                EditorGUILayout.LabelField(best.Summarize(), EditorStyles.wordWrappedLabel);

            if (_telemetry != null && !string.IsNullOrEmpty(_telemetry.LastEpisodeBreakdown))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Last episode", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(_telemetry.LastEpisodeBreakdown, EditorStyles.wordWrappedLabel);
            }

            if (_state.FitnessHistory != null && _state.FitnessHistory.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Recent fitness", EditorStyles.boldLabel);
                var history = _state.FitnessHistory;
                if (history.Values != null && history.Generations != null && history.Capacity > 0)
                {
                    int shown = Mathf.Min(8, history.Count);
                    for (int i = 0; i < shown; i++)
                    {
                        int slot = (history.Head - 1 - i + history.Capacity) % history.Capacity;
                        EditorGUILayout.LabelField($"  gen {history.Generations[slot]}   {history.Values[slot]:F1}");
                    }
                }
            }
        }

        // ─────────────────────────────────────────────
        //  Archive
        // ─────────────────────────────────────────────
        void DrawArchive()
        {
            EditorGUILayout.Space(6);
            _archive = (TrainingArchiveSO)EditorGUILayout.ObjectField("Archive", _archive, typeof(TrainingArchiveSO), false);
            if (_archive == null)
            {
                EditorGUILayout.HelpBox("Press Quick Setup to create the archive.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Entries: {_archive.Entries.Count}", EditorStyles.boldLabel);
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
                EditorGUILayout.LabelField(entry.Genome != null ? entry.Genome.Summarize() : "No genome", EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("Export JSON…"))
                {
                    var path = EditorUtility.SaveFilePanel("Export Genome", Application.dataPath, $"{entry.Key}.json", "json");
                    if (!string.IsNullOrEmpty(path)) GenomeJson.SaveToFile(entry.Genome, path);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No genome for this vessel, mode, and intensity.", MessageType.Info);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("All entries", EditorStyles.boldLabel);
            using (var scope = new EditorGUILayout.ScrollViewScope(_archiveScroll, GUILayout.Height(180)))
            {
                _archiveScroll = scope.scrollPosition;
                foreach (var e in _archive.Entries.OrderBy(x => x.Key))
                {
                    if (GUILayout.Button($"{e.Key}    fitness {e.Fitness:F1}    gen {e.Generation}", EditorStyles.label))
                    {
                        _archiveVessel = e.Vessel;
                        _archiveGame = e.GameMode;
                        _archiveIntensity = e.Intensity;
                    }
                }
            }

            if (GUILayout.Button("Import JSON into the filter above…"))
            {
                var path = EditorUtility.OpenFilePanel("Import Genome", Application.dataPath, "json");
                if (!string.IsNullOrEmpty(path))
                {
                    var g = GenomeJson.LoadFromFile(path);
                    if (g != null)
                    {
                        _archive.Upsert(_archiveVessel, _archiveGame, _archiveIntensity, g, g.Fitness, g.GenerationBorn, "Imported");
                        EditorUtility.SetDirty(_archive);
                        Record(_archive);
                        AssetDatabase.SaveAssets();
                    }
                }
            }
        }

        // ─────────────────────────────────────────────
        //  Deploy
        // ─────────────────────────────────────────────
        void DrawDeploy()
        {
            EditorGUILayout.Space(6);
            EnsureControl();
            if (_control == null)
            {
                EditorGUILayout.HelpBox("Press Quick Setup first.", MessageType.Info);
                return;
            }

            EditorGUI.BeginChangeCheck();
            _control.DeployArchiveInNormalPlay = EditorGUILayout.Toggle(
                new GUIContent("Use archive in normal play",
                    "When on, AI seats in an ordinary match fly the archive. Training runs ignore this and use the population."),
                _control.DeployArchiveInNormalPlay);
            _control.UseStoredGenomeForLowerIntensity = EditorGUILayout.Toggle(
                new GUIContent("Store a genome per intensity",
                    "Off: intensities 1–3 dither the intensity-4 genome and do not write it back. On: fly a genome stored for that exact intensity, with no extra dither."),
                _control.UseStoredGenomeForLowerIntensity);
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(_control);

            bool hasBest = _state != null && _state.HallOfFameBest != null && _state.EpisodesCompleted > 0
                           && !float.IsNegativeInfinity(_state.HallOfFameBestFitness);
            using (new EditorGUI.DisabledScope(!hasBest || _archive == null || _scenario == null))
            {
                if (GUILayout.Button("Push best genome into archive", GUILayout.Height(28)))
                    PushBest();
            }

            if (!hasBest)
                EditorGUILayout.HelpBox("Learn at least one finished match before pushing a genome.", MessageType.None);

            FrogletEditorPalette.HorizontalRule();
            EditorGUILayout.LabelField("Play against the archive", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Starts the scenario as a normal match. You fly the host seat. AI seats fly the intensity-4 archive genome when that entry exists, and keep AIPilot when it does not. This is not a training run.",
                MessageType.None);

            using (new EditorGUI.DisabledScope(Application.isPlaying || _scenario == null))
            {
                if (FrogletEditorPalette.ColorButton("Play against trained AI", FrogletEditorPalette.Info, 240, 32,
                        "Launch the scenario with you in the host seat and the archive on the AI."))
                    Queue(PlayAgainstArchive);
            }
        }

        void PushBest()
        {
            if (_archive == null || _scenario == null || _state?.HallOfFameBest == null) return;
            _archive.Upsert(_scenario.Vessel, _scenario.GameMode, ArchiveDeployment.TrainedIntensity,
                _state.HallOfFameBest, _state.HallOfFameBestFitness, _state.Population != null ? _state.Population.Generation : 0,
                $"Deployed from the window after {_state.EpisodesCompleted} episodes");
            EditorUtility.SetDirty(_archive);
            Record(_archive);
            AssetDatabase.SaveAssets();
            _archiveVessel = _scenario.Vessel;
            _archiveGame = _scenario.GameMode;
            _archiveIntensity = ArchiveDeployment.TrainedIntensity;
        }

        void PlayAgainstArchive()
        {
            RunQuickSetup(focusWindow: false);
            AutoDiscoverAssets();
            EnsureControl();
            if (_control == null || _scenario == null) return;

            var keyed = ArchiveDeployment.ResolveVessel(_scenario.GameMode, _scenario.Vessel);
            bool exact = _archive != null &&
                         _archive.Find(keyed, _scenario.GameMode, ArchiveDeployment.TrainedIntensity) != null;
            string body = exact
                ? $"You will fly {_scenario.Vessel} in {_scenario.GameMode}. AI seats keyed to {keyed} use the intensity-4 genome. Intensities 1–3 dither it."
                : $"No intensity-4 genome is stored for {keyed} / {_scenario.GameMode}. " +
                  "The match still starts. Those AI seats keep AIPilot.";
            if (!EditorUtility.DisplayDialog("Play against trained AI", body, "Play", "Cancel"))
                return;

            _control.Scenario = _scenario;
            _control.State = _state;
            _control.Archive = _archive;
            _control.Telemetry = _telemetry;
            _control.DeployArchiveInNormalPlay = true;
            _control.HumanPlaysThisLaunch = true;
            _control.AutoStartOnPlay = true;
            EditorUtility.SetDirty(_control);
            Record(_control);
            AssetDatabase.SaveAssets();
            EditorApplication.isPlaying = true;
        }

        void StartLearn()
        {
            RunQuickSetup(focusWindow: false);
            AutoDiscoverAssets();
            EnsureControl();
            NormalizeControlDefaults();
            if (_control == null) return;

            _control.Schedule = null;
            _control.Scenario = _scenario;
            _control.State = _state;
            _control.Archive = _archive;
            _control.Telemetry = _telemetry;
            _control.HumanPlaysThisLaunch = false;
            _control.AutoStartOnPlay = true;
            EditorUtility.SetDirty(_control);
            SaveOperatorAssets();
            EditorApplication.isPlaying = true;
        }

        void StopLearn()
        {
            if (_activeRunner != null && _activeRunner.IsRunning)
                _activeRunner.StopSession();
            EditorApplication.isPlaying = false;
        }

        void ResetSession()
        {
            if (_scenario == null || _state == null) return;
            _state.ResetForScenario(_scenario.Key, _scenario);
            _state.ClearScheduleProgress();
            EditorUtility.SetDirty(_state);
            Record(_state);
            AssetDatabase.SaveAssets();
        }

        void SaveOperatorAssets()
        {
            if (_scenario != null)
            {
                EditorUtility.SetDirty(_scenario);
                Record(_scenario);
            }
            if (_control != null)
            {
                EditorUtility.SetDirty(_control);
                Record(_control);
            }
            AssetDatabase.SaveAssets();
        }

        void EnsureControl()
        {
            if (_control != null) return;
            _control = TrainingPlayModeHook.FindControlAsset();
            if (_control == null)
                _control = LoadOrCreateAsset<TrainingControlSO>(QuickSetupRoot + "/TrainingControl.asset", _ => { });
            NormalizeControlDefaults();
        }

        void NormalizeControlDefaults()
        {
            if (_control == null) return;
            bool dirty = false;
            if (_control.TargetEpisodes == 0)
            {
                _control.TargetEpisodes = -1;
                dirty = true;
            }
            if (_control.WatchdogSeconds <= 0f)
            {
                _control.WatchdogSeconds = 180f;
                dirty = true;
            }
            if (dirty) EditorUtility.SetDirty(_control);
        }

        void SyncArchiveFilterFromScenario()
        {
            if (_scenario == null) return;
            _archiveVessel = _scenario.Vessel;
            _archiveGame = _scenario.GameMode;
            _archiveIntensity = Mathf.Clamp(_scenario.Intensity, 1, 4);
        }

        static int CatalogIndexOf(TrainingScenarioSO scenario)
        {
            if (scenario == null) return 0;
            var live = TrainingModeCatalog.Live;
            for (int i = 0; i < live.Count; i++)
                if (live[i].GameMode == scenario.GameMode) return i;
            return -1;
        }

        static bool CatalogRowFor(TrainingScenarioSO scenario, out TrainingModeCatalog.Row row)
        {
            row = default;
            if (scenario == null) return false;
            return TrainingModeCatalog.TryGet(scenario.GameMode, out row);
        }

        static void Record(UnityEngine.Object asset)
        {
            if (asset == null) return;
            var path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path))
                FrogletToolChangeLedger.Record(ToolName, path);
        }

        [MenuItem("FrogletTools/AI Training/Quick Setup", false, 22)]
        [FrogletTool(FrogletToolCategory.GameModes, Importance = 3,
            Description = "Create the default AI training assets without entering Play mode.",
            DocPath = "Assets/_Scripts/Utility/AITraining/README.md#operator")]
        public static void QuickSetupMenuItem() => RunQuickSetup(focusWindow: true);

        void QuickSetup()
        {
            RunQuickSetup(focusWindow: false);
            _scenario = AssetDatabase.LoadAssetAtPath<TrainingScenarioSO>(TrainingModeCatalog.Live[0].ScenarioPath);
            if (_scenario == null) _scenario = FirstAssetOfType<TrainingScenarioSO>();
            _state = FirstAssetOfType<TrainingSessionStateSO>();
            _archive = FirstAssetOfType<TrainingArchiveSO>();
            _telemetry = FirstAssetOfType<TrainingTelemetrySO>();
            _control = TrainingPlayModeHook.FindControlAsset();
            NormalizeControlDefaults();
            SyncArchiveFilterFromScenario();
            Repaint();
        }

        /// <summary>
        /// Creates (or loads) the standard asset set under <see cref="QuickSetupRoot"/>.
        /// Idempotent — existing assets are not overwritten. Empty cross-references are filled.
        /// </summary>
        public static void RunQuickSetup(bool focusWindow)
        {
            EnsureFolder(QuickSetupRoot);
            EnsureFolder(QuickSetupRoot + "/Profiles");
            EnsureFolder(QuickSetupRoot + "/Scenarios");

            var hex = TrainingModeCatalog.Live[0];
            var fitness = LoadOrCreateAsset<FitnessProfileSO>(hex.ProfilePath, so => so.ApplyFor(hex.GameMode));
            var scenario = LoadOrCreateAsset<TrainingScenarioSO>(hex.ScenarioPath, so => so.ApplyCatalogDefaults(hex));
            if (scenario.FitnessProfile == null)
            {
                scenario.FitnessProfile = fitness;
                EditorUtility.SetDirty(scenario);
                Record(scenario);
            }

            var state = LoadOrCreateAsset<TrainingSessionStateSO>(QuickSetupRoot + "/SessionState.asset",
                so => so.ResetForScenario(scenario.Key, scenario));

            LoadOrCreateAsset<TrainingScheduleSO>(QuickSetupRoot + "/Schedule.asset", SeedOvernightExample);

            var archive = LoadOrCreateAsset<TrainingArchiveSO>(QuickSetupRoot + "/Archive.asset", _ => { });
            var telemetry = LoadOrCreateAsset<TrainingTelemetrySO>(QuickSetupRoot + "/Telemetry.asset", _ => { });

            var control = LoadOrCreateAsset<TrainingControlSO>(QuickSetupRoot + "/TrainingControl.asset", _ => { });
            if (control.Scenario == null) control.Scenario = scenario;
            if (control.State == null) control.State = state;
            if (control.Archive == null) control.Archive = archive;
            if (control.Telemetry == null) control.Telemetry = telemetry;
            if (control.TargetEpisodes == 0) control.TargetEpisodes = -1;
            if (control.WatchdogSeconds <= 0f) control.WatchdogSeconds = 180f;
            EditorUtility.SetDirty(control);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (focusWindow)
            {
                var w = GetWindow<TrainingEditorWindow>("AI Training");
                w.AutoDiscoverAssets();
                w.Repaint();
            }

            CSDebug.LogVerbose(CSLogChannel.AITraining,
                $"[AI Training] Quick Setup complete. Assets at: {QuickSetupRoot}");
        }

        static T LoadOrCreateAsset<T>(string path, System.Action<T> initialize) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            var so = ScriptableObject.CreateInstance<T>();
            initialize?.Invoke(so);
            AssetDatabase.CreateAsset(so, path);
            FrogletToolChangeLedger.Record(ToolName, path);
            return so;
        }

        static void EnsureFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || assetPath == "Assets") return;
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var parent = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(assetPath);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
