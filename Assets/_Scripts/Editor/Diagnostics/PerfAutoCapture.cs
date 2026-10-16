using System;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Utility.PerformanceBenchmark;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Performance captures with no button (Yash, 2026-10-15: "captures should happen automatic, no Capture button").
    /// Docs/DIAGNOSTICS.md § "The Bug Hunt &amp; Performance badge"; Docs/PERFORMANCE_OPTIMIZATION.md §4.6.
    ///
    /// <para>In Editor Play mode, once per scene per play session, after the scene has run long enough to be the state
    /// the record measures, this runs the HUD's <c>capture &lt;label&gt; 15</c> by itself: burst, diag 15 s, the
    /// Profiler hierarchy (prof switches Profiler recording on for its 180 frames and back off; no window needed),
    /// renderers, cells, one JSON. The scenario table comes from <c>Docs/perf/status.json</c> (written by
    /// <c>Tools/Build/perf_hunt.py</c>: scene, label, afterSeconds), so a scenario scene gets its record label (S2,
    /// SkimRace, S5) at its record time; any other gameplay scene is captured under its own name after
    /// <see cref="DefaultAfterSeconds"/>. When Play stops, the Bug Ledger's one question ("Send to Claude?",
    /// <see cref="BugHuntAutoSend"/>) offers the new file; the daily sweep judges it.</para>
    ///
    /// <para>Skipped, and said so on the badge: Code Optimization = Debug (C# ~5x slower, the sweep would refuse it
    /// anyway), a paused editor, the boot scenes. Costs the play session one 15 s sampling window, a 180-frame
    /// Profiler recording and one renderer census per scene, so the person may see a short hitch at the end of it.
    /// Off on this machine with the EditorPrefs key <see cref="OffKey"/> (no UI by design).</para>
    /// </summary>
    [InitializeOnLoad]
    public static class PerfAutoCapture
    {
        public const string OffKey = "CosmicShore.PerfAutoCapture.Off";
        const float DefaultAfterSeconds = 90f;
        const int CaptureSeconds = 15;
        static readonly HashSet<string> SkippedScenes = new(StringComparer.OrdinalIgnoreCase) { "Bootstrap", "Authentication" };

        [Serializable] class Scenario { public string label; public string scene; public float afterSeconds; }
        [Serializable] class Status { public Scenario[] scenarios; }

        static readonly HashSet<string> s_captured = new(StringComparer.OrdinalIgnoreCase);
        static string s_scene;
        static double s_sceneSince;
        static double s_nextCheck;
        static Scenario[] s_scenarios;

        /// <summary>One line for the badge: what the editor will capture next, or why it will not.</summary>
        public static string StatusLine { get; private set; } = "";

        static bool Enabled => !EditorPrefs.GetBool(OffKey, false);

        static PerfAutoCapture()
        {
            if (Application.isBatchMode) return;
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                s_captured.Clear();
                s_scene = null;
                s_scenarios = ReadScenarios();
                StatusLine = "";
            };
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || !Enabled) { if (!EditorApplication.isPlaying) StatusLine = ""; return; }
            double now = EditorApplication.timeSinceStartup;
            if (now < s_nextCheck) return;
            s_nextCheck = now + 1.0;

            try
            {
                string scene = SceneManager.GetActiveScene().name;
                if (scene != s_scene) { s_scene = scene; s_sceneSince = now; }
                if (string.IsNullOrEmpty(scene) || SkippedScenes.Contains(scene)) { StatusLine = ""; return; }

                var (label, after) = Target(scene);
                if (s_captured.Contains(scene)) { StatusLine = $"Auto-capture: {label} done this session."; return; }
                if (CompilationPipeline.codeOptimization == CodeOptimization.Debug)
                {
                    StatusLine = "Auto-capture off while Code Optimization is Debug (press Release).";
                    return;
                }
                if (EditorApplication.isPaused || DiagnosticsHUD.CaptureRunning) return;

                double left = after - (now - s_sceneSince);
                if (left > 0)
                {
                    StatusLine = $"Auto-capture: {label} in {(int)left / 60}:{(int)left % 60:00}.";
                    return;
                }

                string answer = DiagnosticsHUD.RunConsole($"{DiagnosticsHUD.CaptureCommand} {label} {CaptureSeconds}");
                // The HUD spawns a moment into Play; until it answers, try again next second.
                if (!DiagnosticsHUD.CaptureRunning && answer != null && answer.StartsWith("no DiagnosticsHUD", StringComparison.Ordinal))
                    return;
                s_captured.Add(scene);
                StatusLine = $"Auto-capture: {label} running (15 s, then the Profiler). Stop Play afterwards to send it.";
            }
            catch (Exception e)
            {
                // A measurement must never break a play test.
                StatusLine = $"Auto-capture failed: {e.GetType().Name}";
            }
        }

        static (string label, float after) Target(string scene)
        {
            if (s_scenarios != null)
                foreach (var s in s_scenarios)
                    if (s != null && string.Equals(s.scene, scene, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(s.label))
                        return (s.label, s.afterSeconds > 0f ? s.afterSeconds : DefaultAfterSeconds);
            return (scene, DefaultAfterSeconds);
        }

        static Scenario[] ReadScenarios()
        {
            try
            {
                string path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Docs", "perf", "status.json");
                return File.Exists(path) ? JsonUtility.FromJson<Status>(File.ReadAllText(path))?.scenarios : null;
            }
            catch (Exception)
            {
                return null;   // unknown scenarios: every scene is captured under its own name
            }
        }
    }
}
