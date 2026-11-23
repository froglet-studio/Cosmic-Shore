using UnityEngine;
using System;
using System.Globalization;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Plays Thresher Time races with every seat an AI Thresher from the command line and quits (THRESHERTIME.md §4 "The
    /// aimed launch"):
    /// <code>Unity -batchmode -nographics -projectPath . -executeMethod CosmicShore.Editor.ThresherTimeSoakLauncher.Run
    ///   -ttSoak "1:3" -ttSoakAimed 1 -ttSoakOut Logs/tt_soak.jsonl -ttSoakTag aimed -ttSoakTimeScale 2 -ttSoakQuit</code>
    /// <c>-ttSoak "intensities:races"</c> (<c>"1,2,3,4:1"</c> plays one race at each intensity in one session),
    /// <c>-ttSoakAimed 0</c> races with the aimed launch off (the A/B's "before"), <c>-ttSoakBelfryAI 0</c> with the
    /// Belfry AI off (the AI flies at the swinging ring's live pose and does not duck), <c>-ttSoakSeed</c> the first course
    /// seed (each race the next). Without <c>-batchmode</c> the races play in the Game view and the AI Diagnose panel opens
    /// on each one, to watch the AIs. It opens Bootstrap and enters Play; <see cref="ThresherTimeSoakRunner"/> does the
    /// rest. The SwitchbackSoakLauncher's shape.
    /// </summary>
    [InitializeOnLoad]
    public static class ThresherTimeSoakLauncher
    {
        const string PendingKey = "CosmicShore.ThresherTimeSoak.Pending";
        const string OfflineKey = "CosmicShore.OfflinePreferred";
        const string OfflineWasKey = "ThresherTimeSoak.OfflineWas";

        static ThresherTimeSoakLauncher()
        {
            ThresherTimeSoakRunner.AllDone -= OnDone;
            ThresherTimeSoakRunner.AllDone += OnDone;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        public static void Run()
        {
            SessionState.SetBool(PendingKey, true);
            // Soak offline (local host on 127.0.0.1), as the Switchback soak does: a Relay hiccup mid-race voids it.
            // The player's own preference is put back when the soak ends.
            SessionState.SetInt(OfflineWasKey, PlayerPrefs.GetInt(OfflineKey, 0));
            PlayerPrefs.SetInt(OfflineKey, 1);
            PlayerPrefs.Save();
            EditorSceneManager.OpenScene("Assets/_Scenes/Bootstrap.unity", OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        static string Arg(string name, string fallback)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : fallback;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            SessionState.SetBool(PendingKey, false);
            var parts = Arg("-ttSoak", "1:2").Split(':');
            var s = new ThresherTimeSoakRunner.Settings
            {
                Intensities = Array.ConvertAll(parts[0].Split(','), x => int.Parse(x, CultureInfo.InvariantCulture)),
                Races = int.Parse(parts.Length > 1 ? parts[1] : "2", CultureInfo.InvariantCulture),
                Aimed = Arg("-ttSoakAimed", "1") != "0",
                BelfryAI = Arg("-ttSoakBelfryAI", "1") != "0",
                FirstSeed = int.Parse(Arg("-ttSoakSeed", "7001"), CultureInfo.InvariantCulture),
                OutputFile = Arg("-ttSoakOut", "Logs/tt_soak.jsonl"),
                Tag = Arg("-ttSoakTag", ""),
                TimeScale = float.Parse(Arg("-ttSoakTimeScale", "1"), CultureInfo.InvariantCulture),
                TargetFrameRate = int.Parse(Arg("-ttSoakFps", "60"), CultureInfo.InvariantCulture),
                Watch = !Application.isBatchMode,
            };
            ThresherTimeSoakRunner.Launch(s);
        }

        static void OnDone(bool ok)
        {
            PlayerPrefs.SetInt(OfflineKey, SessionState.GetInt(OfflineWasKey, 0));
            PlayerPrefs.Save();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-ttSoakQuit") < 0) return;
            double giveUp = EditorApplication.timeSinceStartup + 60.0;
            int code = ok ? 0 : 1;
            EditorApplication.isPlaying = false;
            // Leave Play, then quit; if leaving Play hangs (a network shutdown), quit anyway after a minute.
            void Tick()
            {
                if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup < giveUp) return;
                EditorApplication.update -= Tick;
                EditorApplication.Exit(code);
            }
            EditorApplication.update += Tick;
        }
    }
}
