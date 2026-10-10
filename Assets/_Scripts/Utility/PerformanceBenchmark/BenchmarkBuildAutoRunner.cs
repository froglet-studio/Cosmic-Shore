using System.Collections;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Self-running benchmark for development builds. When the player is launched with the
    /// <c>-csmbench</c> command-line argument (dev build or editor only), it runs the same
    /// warmup → sample → analyze cycle as the editor path and writes a <see cref="BenchmarkReport"/>
    /// JSON (origin = DevBuild) to <c>persistentDataPath/PerfRuns/</c>. Pull that file off the
    /// device and import it in the editor's History tab.
    ///
    /// Strictly gated so it NEVER runs in a normal session: requires both the dev-build/editor
    /// compile guard AND the launch argument.
    ///
    /// The same hook also starts the unattended LOAD-TIME SWEEP: launched with
    /// <c>-csmloadsweep</c> the build records its own cold boot, then launches every arcade card at
    /// every intensity through the normal launch path, records each load with Load Time Insights,
    /// and writes one report per cell plus a worst-cell-first table
    /// (<see cref="LoadSweepRunner"/>, options in <see cref="LoadSweepOptions"/>). The two
    /// arguments are exclusive: a sweep owns the whole session.
    /// </summary>
    public class BenchmarkBuildAutoRunner : MonoBehaviour
    {
        const string LaunchArg = "-csmbench";
        const string OutputFolder = "PerfRuns";

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void MaybeAutoBench()
        {
            var args = System.Environment.GetCommandLineArgs();

            // Before the first scene: the sweep arms the recorder here so the Bootstrap scene's
            // own hook (LoadInsightsRuntime, AfterSceneLoad) finds it armed and records the boot.
            if (HasArg(args, LoadSweepOptions.LaunchArg))
            {
                LoadSweepRunner.Launch(LoadSweepOptions.Parse(args));
                return;
            }

            if (!HasArg(args, LaunchArg)) return;

            var go = new GameObject("[BenchmarkBuildAutoRunner]");
            DontDestroyOnLoad(go);
            go.AddComponent<BenchmarkBuildAutoRunner>();
        }

        static bool HasArg(string[] args, string arg)
        {
            for (int i = 0; i < args.Length; i++)
                if (args[i] == arg) return true;
            return false;
        }

        IEnumerator Start()
        {
            // Let the launch scene settle before measuring.
            yield return new WaitForSeconds(1f);

            // Runtime profiler so spike frames are captured (marker attribution is editor-only).
            UnityEngine.Profiling.Profiler.enabled = true;

            // Reuse the same runner + analysis as the editor. Ship a Resources/BenchmarkConfig
            // asset to override defaults; otherwise a default config is used.
            var config = Resources.Load<BenchmarkConfigSO>("BenchmarkConfig");
            if (config == null) config = ScriptableObject.CreateInstance<BenchmarkConfigSO>();

            var runner = gameObject.AddComponent<PerformanceBenchmarkRunner>();
            runner.Configure(config);
            runner.AutoSave = false; // we write to the dedicated PerfRuns folder below
            runner.StartBenchmark();

            yield return null;
            yield return new WaitWhile(() => runner.IsRunning);

            var report = runner.LastReport;
            if (report != null)
            {
                string path = report.SaveToFile(OutputFolder); // origin = DevBuild (set in PopulateEnvironment)
                Debug.Log($"[Benchmark] Dev-build capture complete - saved to {path}");
            }
            else
            {
                Debug.LogWarning("[Benchmark] Dev-build capture produced no report.");
            }
        }
#endif
    }
}
