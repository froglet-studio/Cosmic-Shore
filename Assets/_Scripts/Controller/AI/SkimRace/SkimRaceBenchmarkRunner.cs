using System;
using System.Collections;
using System.IO;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runs N consecutive Skim Races through the NORMAL game flow and records each one with a
    /// <see cref="SkimRaceRaceRecorder"/>. It is a test driver, not part of the race:
    /// <list type="number">
    /// <item>Waits in the menu for the local player (the normal boot chain), then configures
    /// <c>GameDataSO</c> from the Skim Race arcade card exactly as the arcade launch modal does
    /// (<c>SyncFromArcadeGame</c> + <c>ConfigurePlayerCounts</c>) and raises the normal launch event.</item>
    /// <item>In the race scene it presses the PUBLIC Ready button
    /// (<c>MiniGameControllerBase.OnReadyClicked</c>, the one the HUD wires) for the host seat.
    /// The host seat is left idle on its own domain; the AI backfill seat races.</item>
    /// <item>When the server has declared a result (or the timeout passes) it asks for the
    /// normal Play-Again replay (<c>RequestReplay</c>, a full scene reload).</item>
    /// </list>
    /// It never writes a vessel, crystal, score, course or timer.
    ///
    /// Start it from FrogletTools ▸ AI ▸ Skim Race AI Benchmark, or programmatically with
    /// <see cref="Launch"/> after entering Play mode from the Bootstrap scene.
    /// </summary>
    public class SkimRaceBenchmarkRunner : MonoBehaviour
    {
        [Serializable]
        public class Settings
        {
            public int Races = 10;
            public int Intensity = 4;
            public int TotalPlayers = 2;   // host + AI backfill
            public float LimitSeconds = 70f;
            public float TimeoutSeconds = 150f;
            public float TimeScale = 1f;
            public string Commit = "";
            public string OutputDirectory = "";
            public bool TraceFrames = true;
            [Tooltip("Graphics quality level for the run (-1 = leave the player's setting). The same setting a player picks; it changes frame rate, never gameplay.")]
            public int QualityLevel = -1;
        }

        public static SkimRaceBenchmarkRunner Active { get; private set; }

        Settings _s;
        GameDataSO _gameData;
        string _session;
        string _file;
        int _completed;
        int _successes;
        SkimRaceRaceRecorder _recorder;
        int _previousQuality = -1;

        public int Completed => _completed;
        public int Successes => _successes;
        public string ResultsFile => _file;
        public bool Done { get; private set; }

        public static SkimRaceBenchmarkRunner Launch(Settings settings)
        {
            if (Active != null) Destroy(Active.gameObject);
            var go = new GameObject("[Skim Race AI Benchmark]");
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<SkimRaceBenchmarkRunner>();
            runner.Begin(settings);
            return runner;
        }

        void Begin(Settings s)
        {
            Active = this;
            _s = s;
            _session = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string dir = string.IsNullOrEmpty(s.OutputDirectory)
                ? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "BenchmarkResults", "SkimRaceAI")
                : s.OutputDirectory;
            _file = Path.Combine(dir, $"skimrace_I{s.Intensity}_{_session}.jsonl");
            _gameData = FindGameData();
            if (s.QualityLevel >= 0 && s.QualityLevel < QualitySettings.names.Length)
            {
                _previousQuality = QualitySettings.GetQualityLevel();
                QualitySettings.SetQualityLevel(s.QualityLevel, true);
            }
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
            if (_s != null && _s.TimeScale > 1f) Time.timeScale = 1f;
            if (_previousQuality >= 0) QualitySettings.SetQualityLevel(_previousQuality, true);
        }

        IEnumerator Run()
        {
            if (_gameData == null)
            {
                CSDebug.LogError("[SkimRaceBenchmark] GameDataSO not found; cannot run.");
                Done = true;
                yield break;
            }

            // 1) Menu: wait for the normal boot chain to hand us a local pilot.
            float deadline = Time.unscaledTime + 120f;
            while (Time.unscaledTime < deadline && !(IsMenu() && _gameData.LocalPlayer?.Vessel != null))
                yield return new WaitForSecondsRealtime(0.25f);
            yield return new WaitForSecondsRealtime(1.5f);

            ConfigureLaunch();
            _gameData.InvokeGameLaunch();
            CSDebug.Log($"[SkimRaceBenchmark] launched I{_s.Intensity}, {_s.Races} races -> {_file}");

            for (int race = 0; race < _s.Races; race++)
            {
                // 2) Race scene with a live controller and every seat spawned.
                MiniGameControllerBase controller = null;
                deadline = Time.unscaledTime + 90f;
                while (Time.unscaledTime < deadline)
                {
                    if (SceneManager.GetActiveScene().name == _gameData.SceneName)
                    {
                        controller = FindAnyObjectByType<MiniGameControllerBase>();
                        if (controller != null && SeatsReady() && TrackReady()) break;
                    }
                    yield return new WaitForSecondsRealtime(0.25f);
                }
                if (controller == null)
                {
                    CSDebug.LogError("[SkimRaceBenchmark] Race scene never became ready; stopping.");
                    break;
                }
                yield return new WaitForSecondsRealtime(1f);

                var recGo = new GameObject("[Skim Race Recorder]");
                _recorder = recGo.AddComponent<SkimRaceRaceRecorder>();
                _recorder.Configure(_gameData, _file, _session, _s.Commit, race, _s.LimitSeconds, _s.TimeoutSeconds);
                _recorder.TraceFrames = _s.TraceFrames;

                // 3) Press Ready (the public HUD button) until the countdown has run.
                deadline = Time.unscaledTime + 30f;
                while (!_gameData.IsTurnRunning && Time.unscaledTime < deadline)
                {
                    if (controller != null) controller.OnReadyClicked();
                    yield return new WaitForSecondsRealtime(2f);
                }
                if (_s.TimeScale > 1f) Time.timeScale = _s.TimeScale;

                // 4) Wait for the referee.
                while (_recorder != null && !_recorder.Finished) yield return null;
                Time.timeScale = 1f;
                if (_recorder != null && _recorder.Record != null)
                {
                    _completed++;
                    if (_recorder.Record.success) _successes++;
                }
                yield return new WaitForSecondsRealtime(2.5f);

                if (race + 1 >= _s.Races) break;

                // 5) Play Again (full scene reload). Wait for the old controller to go away.
                controller = FindAnyObjectByType<MiniGameControllerBase>();
                if (controller != null) controller.RequestReplay();
                deadline = Time.unscaledTime + 30f;
                while (controller != null && Time.unscaledTime < deadline) yield return new WaitForSecondsRealtime(0.25f);
            }

            Done = true;
            CSDebug.Log($"[SkimRaceBenchmark] done: {_successes}/{_completed} successful -> {_file}");
        }

        bool IsMenu() => SceneManager.GetActiveScene().name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0;

        bool SeatsReady()
        {
            int expected = Mathf.Max(1, _s.TotalPlayers);
            int n = 0;
            foreach (var p in _gameData.Players)
                if (p != null && p.Vessel != null) n++;
            return n >= expected;
        }

        static bool TrackReady()
        {
            var ctl = FindAnyObjectByType<SkimRaceController>();
            return ctl == null || ctl.TrackSeed != 0;
        }

        void ConfigureLaunch()
        {
            var card = FindSkimRaceCard();
            if (card != null) _gameData.SyncFromArcadeGame(card);
            else
            {
                _gameData.GameMode = GameModes.SkimRace;
                _gameData.SceneName = "MinigameSkimRace";
                _gameData.IsMultiplayerMode = true;
            }
            _gameData.IsTraining = false;
            if (_gameData.selectedVesselClass != null) _gameData.selectedVesselClass.Value = VesselClassType.Squirrel;
            if (_gameData.SelectedIntensity != null) _gameData.SelectedIntensity.Value = Mathf.Clamp(_s.Intensity, 1, 4);
            _gameData.ConfigurePlayerCounts(Mathf.Max(1, _s.TotalPlayers), 1);
            _gameData.RequestedDomainCount = Mathf.Clamp(_s.TotalPlayers, 1, 3);
        }

        static GameDataSO FindGameData()
        {
            var all = Resources.FindObjectsOfTypeAll<GameDataSO>();
            if (all != null && all.Length > 0) return all[0];
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("t:GameDataSO");
            if (guids.Length > 0)
                return UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataSO>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#endif
            return null;
        }

        static SO_ArcadeGame FindSkimRaceCard()
        {
            foreach (var g in Resources.FindObjectsOfTypeAll<SO_ArcadeGame>())
                if (g != null && g.Mode == GameModes.SkimRace) return g;
#if UNITY_EDITOR
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:SO_ArcadeGame"))
            {
                var g = UnityEditor.AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (g != null && g.Mode == GameModes.SkimRace) return g;
            }
#endif
            return null;
        }
    }
}
