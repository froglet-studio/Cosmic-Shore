using System.Collections;
using System.Collections.Generic;
using System.Text;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Drives a benchmark across multiple scenes in one pass: for each scene it loads it,
    /// runs a benchmark with the shared config, collects the report, then moves on. Survives
    /// scene loads via DontDestroyOnLoad and reports progress so an editor window can poll it.
    ///
    /// Caveat: scenes are loaded directly via <see cref="SceneManager.LoadSceneAsync"/>, so
    /// networked game scenes that depend on the Bootstrap → host → spawn pipeline will be
    /// benchmarked in their uninitialized state. Self-contained scenes (Menu_Main, the
    /// single-player minigame scenes) sweep faithfully. Each scene must be in Build Settings.
    /// </summary>
    public class BenchmarkSweepRunner : MonoBehaviour
    {
        public struct SweepEntry
        {
            public string sceneName;
            public string reportPath;
            public float avgFps;
            public float p1Fps;
            public float p99FrameTimeMs;
            public string grade;
            public bool loaded;
        }

        public static BenchmarkSweepRunner Instance { get; private set; }

        public bool IsSweeping { get; private set; }
        public bool IsComplete { get; private set; }
        public int CurrentIndex { get; private set; }
        public int TotalScenes { get; private set; }
        public string CurrentScene { get; private set; } = "";
        public string CombinedSummary { get; private set; } = "";
        public readonly List<SweepEntry> Results = new();

        BenchmarkConfigSO _config;
        BenchmarkDataSO _data;
        GameDataSO _gameData;
        string _sweepTag;
        List<string> _scenes;

        public static BenchmarkSweepRunner StartSweep(
            List<string> scenes, BenchmarkConfigSO config, BenchmarkDataSO data,
            GameDataSO gameData, string sweepTag)
        {
            if (Instance != null)
                Destroy(Instance.gameObject);

            var go = new GameObject("[BenchmarkSweepRunner]");
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<BenchmarkSweepRunner>();
            runner._scenes = new List<string>(scenes);
            runner._config = config;
            runner._data = data;
            runner._gameData = gameData;
            runner._sweepTag = string.IsNullOrEmpty(sweepTag) ? "sweep" : sweepTag;
            runner.StartCoroutine(runner.RunSweep());
            return runner;
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        IEnumerator RunSweep()
        {
            IsSweeping = true;
            IsComplete = false;
            Results.Clear();
            TotalScenes = _scenes.Count;

            for (int i = 0; i < _scenes.Count; i++)
            {
                CurrentIndex = i;
                CurrentScene = _scenes[i];

                var loadOp = SceneManager.LoadSceneAsync(_scenes[i], LoadSceneMode.Single);
                if (loadOp == null)
                {
                    CSDebug.LogWarning($"[BenchmarkSweep] Scene '{_scenes[i]}' could not be loaded (not in Build Settings?). Skipping.");
                    Results.Add(new SweepEntry { sceneName = _scenes[i], loaded = false, grade = "-" });
                    continue;
                }

                while (!loadOp.isDone) yield return null;

                // Let the scene's Awake/Start/spawn settle before measuring.
                for (int f = 0; f < 5; f++) yield return null;

                var runnerGo = new GameObject("[PerformanceBenchmarkRunner]");
                var runner = runnerGo.AddComponent<PerformanceBenchmarkRunner>();
                runner.Configure(_config, _data, _gameData);
                runner.StartBenchmark();

                // StartBenchmark sets state synchronously; wait for the whole warmup+sample.
                yield return null;
                yield return new WaitWhile(() => runner != null && runner.IsRunning);

                var report = runner != null ? runner.LastReport : null;
                var entry = new SweepEntry { sceneName = _scenes[i], loaded = true };
                if (report?.statistics != null)
                {
                    entry.reportPath = runner.LastReportPath;
                    entry.avgFps = report.statistics.avgFps;
                    entry.p1Fps = report.statistics.p1Fps;
                    entry.p99FrameTimeMs = report.statistics.p99FrameTimeMs;
                    entry.grade = BenchmarkGrade.Evaluate(report.statistics);

                    // Tag this run so the History tab can group the whole sweep together.
                    BenchmarkHistory.TagReport(report.reportId, _sweepTag, _config.OutputFolder);
                }
                else
                {
                    entry.grade = "-";
                }
                Results.Add(entry);

                if (runnerGo != null) Destroy(runnerGo);
                yield return null;
            }

            BuildCombinedSummary();
            IsComplete = true;
            IsSweeping = false;
            CSDebug.Log($"[BenchmarkSweep] Complete — {Results.Count} scene(s) benchmarked.\n{CombinedSummary}");
        }

        void BuildCombinedSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Multi-Scene Sweep '{_sweepTag}' — {Results.Count} scene(s)");
            sb.AppendLine();

            float fpsSum = 0f;
            int measured = 0;
            foreach (var r in Results)
            {
                if (!r.loaded)
                {
                    sb.AppendLine($"  [skip] {r.sceneName} (not loadable)");
                    continue;
                }
                sb.AppendLine($"  [{r.grade}] {r.sceneName}: {r.avgFps:F1} fps  (p1 {r.p1Fps:F1}, p99 {r.p99FrameTimeMs:F1} ms)");
                fpsSum += r.avgFps;
                measured++;
            }

            sb.AppendLine();
            sb.AppendLine(measured > 0
                ? $"  Mean avg FPS across {measured} scene(s): {fpsSum / measured:F1}"
                : "  No scenes were measured.");

            CombinedSummary = sb.ToString();
        }
    }
}
