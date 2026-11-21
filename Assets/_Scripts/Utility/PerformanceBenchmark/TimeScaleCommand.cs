#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// The DiagnosticsHUD <c>timescale</c> console command: play in slow motion so a slow editor
    /// still tests the game at a representative STEP. At 10 fps a vessel at 300 u/s moves 30 u per
    /// frame, so an AI (or a player) overshoots its line and clips prisms between samples
    /// (Docs/SKIM_RACE_AI.md §8.0c measured it); at <c>timescale 0.25</c> the same 10 fps steps
    /// 7.5 u, which is what 40 fps steps at full speed. Everything driven by scaled time (flight,
    /// AI decisions, physics, race clocks) slows together, so behaviour is the same, only slower.
    ///
    /// <c>timescale</c> reports, <c>timescale 0.25</c> sets (0.05 to 4), <c>timescale off</c> resets
    /// to 1. Released on any active-scene change, so a slow scenario never silently carries into the
    /// next one. Pause, hit-stop and celebration effects set the scale themselves and put it back to
    /// 1 when they end, so re-enter the command after one.
    /// </summary>
    public static class TimeScaleCommand
    {
        public const string CommandName = "timescale";
        const float Min = 0.05f, Max = 4f;
        static bool _set;

        public static string Handle(string[] args)
        {
            string arg = args is { Length: > 0 } ? args[0].ToLowerInvariant() : "";
            if (arg == "")
                return $"time scale {Time.timeScale.ToString("0.###", CultureInfo.InvariantCulture)} | usage: {CommandName} <0.05-4> | off";
            if (arg is "off" or "reset" or "1")
            {
                Release();
                return "time scale 1 (normal speed)";
            }
            if (!float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out float scale) || scale <= 0f)
                return $"usage: {CommandName} <0.05-4> | off";
            scale = Mathf.Clamp(scale, Min, Max);
            Time.timeScale = scale;
            if (!_set)
            {
                _set = true;
                SceneManager.activeSceneChanged += OnSceneChanged;
            }
            return $"time scale {scale.ToString("0.###", CultureInfo.InvariantCulture)} - the game runs at {scale * 100f:0}% speed until '{CommandName} off' or a scene change";
        }

        static void OnSceneChanged(Scene from, Scene to)
        {
            if (!_set) return;
            Release();
            Debug.LogWarning($"[DiagnosticsHUD] {CommandName}: released to 1 on the scene change to '{to.name}'.");
        }

        static void Release()
        {
            if (_set) SceneManager.activeSceneChanged -= OnSceneChanged;
            _set = false;
            Time.timeScale = 1f;
        }
    }
}
#endif
