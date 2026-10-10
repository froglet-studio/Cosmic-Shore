// UnityEngine stays OUTSIDE the guard: the class declaration below is unconditional, so
// MonoBehaviour must resolve in Release too (Docs/CONDITIONAL_COMPILATION.md; LoadInsightsRuntime
// and DiagnosticsHUD use the same layout).
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using UnityEngine.SceneManagement;
#endif

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Unattended load-time sweep: records the COLD BOOT, then launches every arcade card at every
    /// intensity through the normal launch path (<see cref="GameDataSO.SyncFromArcadeGame"/> +
    /// <see cref="GameDataSO.ConfigurePlayerCounts"/> + <see cref="GameDataSO.InvokeGameLaunch"/>,
    /// exactly what the arcade modal does), lets Load Time Insights record each one to its own
    /// report, returns to the menu, and finally writes one table a human reads top-down by worst
    /// cell (<see cref="LoadSweepTable"/>), re-written after every cell so a killed sweep still
    /// leaves its partial table.
    ///
    /// Started by <see cref="BenchmarkBuildAutoRunner"/> from <c>-csmloadsweep</c> before the first
    /// scene loads (options: <see cref="LoadSweepOptions"/>), so the Bootstrap scene already finds
    /// the recorder armed. It is a test driver, not part of the game: it never changes how a load
    /// runs, only which load runs next. Editor and development builds only; the body compiles away
    /// in Release.
    ///
    /// Output: <c>{persistentDataPath}/Benchmarks/LoadInsights/sweep_&lt;stamp&gt;.md</c> (+ .json), with
    /// the per-cell <c>load_*.json/.txt</c> reports beside it.
    /// </summary>
    public class LoadSweepRunner : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        const string MenuSceneName = "Menu_Main";
        const string MaelstromModeName = "Maelstrom";
        const float MenuReadyTimeoutSeconds = 240f;
        const float GameSceneArrivalTimeoutSeconds = 60f;
        const float NotRecordedGraceSeconds = 15f;
        const float PollSeconds = 0.25f;
        const float MenuSettleSeconds = 1f;

        public static LoadSweepRunner Active { get; private set; }

        public bool Done { get; private set; }
        public string SummaryPath { get; private set; } = "";

        LoadSweepOptions _options = new();
        GameDataSO _gameData;
        string _stamp = "";
        bool _hasBoot;
        LoadSweepCell _boot = new();
        readonly List<LoadSweepCell> _cells = new();
        bool _prevArmed, _prevBootArmed, _prefsRestored;

        /// <summary>Creates the runner and arms the recorder (boot included). Call before the first scene loads.</summary>
        public static LoadSweepRunner Launch(LoadSweepOptions options)
        {
            if (Active != null) Destroy(Active.gameObject);
            var go = new GameObject("[LoadSweepRunner]");
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<LoadSweepRunner>();
            runner._options = options ?? new LoadSweepOptions();
            runner._stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            runner.ArmRecorder();
            return runner;
        }

        void Awake() => Active = this;

        void OnDestroy()
        {
            RestoreRecorderPrefs();
            if (Active == this) Active = null;
        }

        void OnApplicationQuit() => RestoreRecorderPrefs();

        // The arm flags persist in PlayerPrefs (that is how the editor tab and -csmloadinsights
        // work), so a sweep must put back what it found or every later launch of this build records.
        void ArmRecorder()
        {
            _prevArmed = LoadInsights.Armed;
            _prevBootArmed = LoadInsights.BootArmed;
            LoadInsights.Armed = true;
            LoadInsights.BootArmed = true;
        }

        void RestoreRecorderPrefs()
        {
            if (_prefsRestored) return;
            _prefsRestored = true;
            LoadInsights.Armed = _prevArmed;
            LoadInsights.BootArmed = _prevBootArmed;
        }

        IEnumerator Start()
        {
            yield return Run();
        }

        IEnumerator Run()
        {
            _gameData = FindGameData();
            if (_gameData == null)
            {
                Debug.LogError("[LoadSweep] GameDataSO not found; the sweep cannot launch anything.");
                Finish();
                yield break;
            }

            // 1) The cold boot. The recorder opened it at the first script hook (LoadInsightsRuntime)
            //    and closes it at the menu's OnClientReady; all this runner does is wait for the menu.
            yield return WaitForMenuReady("cold boot");
            var bootReport = LoadInsights.LastReport;
            if (bootReport != null && bootReport.IsColdBoot)
            {
                _hasBoot = true;
                _boot = LoadSweepTable.CellFromReport(bootReport, 0);
                _boot.reportPath = LoadInsights.LastReportPath;
                Debug.Log($"[LoadSweep] Cold boot recorded: {bootReport.totalMs / 1000f:F1}s → {LoadInsights.LastReportPath}");
            }
            else
            {
                Debug.LogWarning("[LoadSweep] No cold-boot report (the recorder host was not available at the " +
                                 "first script hook, or the menu never raised OnClientReady). The cells still run.");
            }

            // 2) The cells.
            var plan = BuildPlan(ResolveCards());
            Debug.Log($"[LoadSweep] {plan.Count} cell(s): intensities [{string.Join(",", _options.Intensities)}], " +
                      $"players '{_options.Players}', repeats {_options.Repeats}, per-cell timeout {_options.CellTimeoutSeconds:F0}s.");
            WriteSummary();

            for (int i = 0; i < plan.Count; i++)
            {
                var step = plan[i];
                yield return new WaitForSecondsRealtime(_options.CooldownSeconds);

                var before = LoadInsights.LastReport;
                Configure(step.card, step.intensity, step.players);
                Debug.Log($"[LoadSweep] Cell {i + 1}/{plan.Count}: {step.card.Mode} I{step.intensity} × {step.players} players " +
                          $"({_gameData.RequestedAIBackfillCount} AI), repeat {step.repeat}.");
                _gameData.InvokeGameLaunch();

                float started = Time.unscaledTime;
                bool timedOut = false;
                while (ReferenceEquals(LoadInsights.LastReport, before))
                {
                    float elapsed = Time.unscaledTime - started;
                    if (!LoadInsights.IsRecording && elapsed > NotRecordedGraceSeconds) break; // never began
                    if (elapsed > _options.CellTimeoutSeconds)
                    {
                        timedOut = true;
                        LoadInsights.AbortLoad($"sweep: cell timeout ({_options.CellTimeoutSeconds:F0}s)");
                        yield return null;
                        break;
                    }
                    yield return new WaitForSecondsRealtime(PollSeconds);
                }

                var report = ReferenceEquals(LoadInsights.LastReport, before) ? null : LoadInsights.LastReport;
                var cell = LoadSweepTable.CellFromReport(report, step.repeat);
                if (report == null)
                {
                    cell.mode = step.card.Mode.ToString();
                    cell.intensity = step.intensity;
                    cell.players = step.players;
                    cell.aiBackfill = Mathf.Max(0, step.players - 1);
                    cell.scene = step.card.SceneName;
                    cell.note = "no report: the recording never began (recorder host absent, or the launch was refused)";
                }
                else
                {
                    cell.reportPath = LoadInsights.LastReportPath;
                    if (timedOut) cell.note = $"sweep timeout after {_options.CellTimeoutSeconds:F0}s";
                }
                _cells.Add(cell);
                WriteSummary();

                // 3) Back to the menu for the next cell. A load that never recorded may still be
                //    loading its scene; give it a bounded chance to arrive before turning around.
                if (report == null)
                    yield return WaitForScene(step.card.SceneName, GameSceneArrivalTimeoutSeconds);

                var loader = FindAnyObjectByType<SceneLoader>();
                if (loader != null) loader.ReturnToMainMenu();
                else Debug.LogError("[LoadSweep] SceneLoader not found; cannot return to the menu.");
                yield return WaitForMenuReady($"after {step.card.Mode} I{step.intensity}");
            }

            Finish();
        }

        // ── Plan ────────────────────────────────────────

        struct PlanStep
        {
            public SO_ArcadeGame card;
            public int intensity;
            public int players;
            public int repeat;
        }

        /// <summary>
        /// The launchable roster: the Arcade singleton's list (the same cards the menu offers,
        /// arcade and arena alike). Every loaded card is the fallback when no roster is up.
        /// </summary>
        static List<SO_ArcadeGame> ResolveCards()
        {
            var cards = new List<SO_ArcadeGame>();
            var roster = Arcade.Instance != null ? Arcade.Instance.ArcadeGames : null;
            if (roster != null && roster.Games != null)
                foreach (var g in roster.Games)
                    if (g != null && !cards.Contains(g)) cards.Add(g);

            if (cards.Count == 0)
                foreach (var g in Resources.FindObjectsOfTypeAll<SO_ArcadeGame>())
                    if (g != null && !string.IsNullOrEmpty(g.SceneName) && !cards.Contains(g)) cards.Add(g);
            return cards;
        }

        List<PlanStep> BuildPlan(List<SO_ArcadeGame> cards)
        {
            var plan = new List<PlanStep>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var card in cards)
            {
                string modeName = card.Mode.ToString();
                seen.Add(modeName);
                if (!_options.WantsMode(modeName)) continue;
                bool explicitlyNamed = _options.Modes != null;
                if (string.Equals(modeName, MaelstromModeName, StringComparison.OrdinalIgnoreCase)
                    && !_options.IncludeMaelstrom && !explicitlyNamed)
                    continue;

                int players = _options.ResolvePlayers(card.MinPlayersAllowed, card.MaxPlayersAllowed);
                foreach (int intensity in _options.Intensities)
                {
                    if (intensity < card.MinIntensity || intensity > card.MaxIntensity) continue;
                    for (int r = 1; r <= _options.Repeats; r++)
                        plan.Add(new PlanStep { card = card, intensity = intensity, players = players, repeat = r });
                }
            }

            if (_options.Modes != null)
                foreach (var wanted in _options.Modes)
                    if (!seen.Contains(wanted))
                        Debug.LogWarning($"[LoadSweep] Mode '{wanted}' is not on the roster; skipped.");
            return plan;
        }

        // ── Launch ──────────────────────────────────────

        /// <summary>Mirrors ArcadeGameConfigureModal.SyncAllGameDataForLaunch for a solo host plus AI backfill.</summary>
        void Configure(SO_ArcadeGame card, int intensity, int players)
        {
            _gameData.SyncFromArcadeGame(card);
            _gameData.IsTraining = false;
            _gameData.IsGeneticTrainingSession = false;
            _gameData.IsMission = false;
            _gameData.IsWeeklyChallenge = false;

            if (_gameData.selectedVesselClass != null && card.Vessels != null && card.Vessels.Count > 0 && card.Vessels[0] != null)
                _gameData.selectedVesselClass.Value = card.Vessels[0].Class;
            if (_gameData.SelectedIntensity != null)
                _gameData.SelectedIntensity.Value = Mathf.Clamp(intensity, card.MinIntensity, card.MaxIntensity);

            _gameData.ConfigurePlayerCounts(players, 1);
            _gameData.SetRequestedAIDomains(null);   // balanced pick: the launch panel's own backstop
            _gameData.SetRequestedAIVessels(null);   // draw hulls from the card
            _gameData.RequestedDomainCount = Mathf.Clamp(Mathf.Min(players, card.MaxDomainsAllowed), card.MinDomainsAllowed, 3);
        }

        static GameDataSO FindGameData()
        {
            var all = Resources.FindObjectsOfTypeAll<GameDataSO>();
            return all != null && all.Length > 0 ? all[0] : null;
        }

        // ── Waits ───────────────────────────────────────

        IEnumerator WaitForMenuReady(string what)
        {
            float deadline = Time.unscaledTime + MenuReadyTimeoutSeconds;
            while (Time.unscaledTime < deadline && SceneManager.GetActiveScene().name != MenuSceneName)
                yield return new WaitForSecondsRealtime(PollSeconds);
            while (Time.unscaledTime < deadline && !(_gameData.LocalPlayer != null && _gameData.LocalPlayer.Vessel != null))
                yield return new WaitForSecondsRealtime(PollSeconds);

            if (Time.unscaledTime >= deadline)
                Debug.LogWarning($"[LoadSweep] The menu was not ready within {MenuReadyTimeoutSeconds:F0}s ({what}); continuing anyway.");
            else
                yield return new WaitForSecondsRealtime(MenuSettleSeconds); // the menu's own OnClientReady listeners settle
        }

        static IEnumerator WaitForScene(string sceneName, float timeoutSeconds)
        {
            float deadline = Time.unscaledTime + timeoutSeconds;
            while (Time.unscaledTime < deadline && SceneManager.GetActiveScene().name != sceneName)
                yield return new WaitForSecondsRealtime(PollSeconds);
        }

        // ── Output ──────────────────────────────────────

        void WriteSummary()
        {
            try
            {
                var any = _hasBoot ? LoadInsights.LastReport : null;
                var summary = new LoadSweepSummary
                {
                    timestamp = _stamp,
                    gitBranch = any?.gitBranch ?? "",
                    gitCommitHash = any?.gitCommitHash ?? "",
                    platform = Application.platform.ToString(),
                    origin = Application.isEditor ? "Editor" : Debug.isDebugBuild ? "DevBuild" : "Release",
                    coldBootTargetSeconds = ResolveConfig()?.ColdBootToMenuTargetSeconds ?? LoadTimeTargets.ColdBootToMenuSeconds,
                    menuToPlayableTargetSeconds = ResolveConfig()?.MenuToPlayableTargetSeconds ?? LoadTimeTargets.MenuToPlayableSeconds,
                    playersPolicy = _options.Players,
                    notes = "Editor numbers are inflated 2-3x versus a development build; only same-source rows compare. " +
                            "Burst compilation must be ON for any row to count (Docs/PERFORMANCE_OPTIMIZATION.md §0).",
                    hasBoot = _hasBoot,
                    boot = _boot,
                    cells = new List<LoadSweepCell>(_cells)
                };

                string dir = Path.Combine(Application.persistentDataPath, LoadInsights.OutputSubfolder);
                Directory.CreateDirectory(dir);
                string baseName = Path.Combine(dir, $"sweep_{_stamp}");
                File.WriteAllText(baseName + ".md", LoadSweepTable.BuildMarkdown(summary));
                File.WriteAllText(baseName + ".json", JsonUtility.ToJson(summary, true));
                SummaryPath = baseName + ".md";
            }
            catch (Exception e)
            {
                Debug.LogError($"[LoadSweep] Could not write the sweep table: {e.Message}");
            }
        }

        static BenchmarkConfigSO ResolveConfig() => Resources.Load<BenchmarkConfigSO>("BenchmarkConfig");

        void Finish()
        {
            Done = true;
            RestoreRecorderPrefs();
            Debug.Log($"[LoadSweep] Done: {_cells.Count} cell(s) → {SummaryPath}");
            if (_options.StayWhenDone) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
#endif
    }
}
