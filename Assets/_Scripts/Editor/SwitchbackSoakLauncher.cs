using System;
using System.Globalization;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Plays Switchback races with every seat an AI Dolphin from the command line and quits (SWITCHBACK.md "AI: the hit counter"):
    /// <code>Unity -batchmode -nographics -projectPath . -executeMethod CosmicShore.Editor.SwitchbackSoakLauncher.Run
    ///   -sbSoak "1:4:2" -sbSoakOut Logs/sb_soak.jsonl -sbSoakTag before -sbSoakTimeScale 2 -sbSoakQuit</code>
    /// <c>-sbSoak "intensity:difficulty(1-4):races"</c>. It opens Bootstrap and enters Play; <see cref="SwitchbackSoakRunner"/> does the rest.
    /// </summary>
    [InitializeOnLoad]
    public static class SwitchbackSoakLauncher
    {
        const string PendingKey = "CosmicShore.SwitchbackSoak.Pending";

        static SwitchbackSoakLauncher()
        {
            SwitchbackSoakRunner.AllDone -= OnDone;
            SwitchbackSoakRunner.AllDone += OnDone;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        public static void Run()
        {
            SessionState.SetBool(PendingKey, true);
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
            var parts = Arg("-sbSoak", "1:4:2").Split(':');
            var s = new SwitchbackSoakRunner.Settings
            {
                Intensity = int.Parse(parts[0], CultureInfo.InvariantCulture),
                Difficulty = (AIDifficulty)int.Parse(parts.Length > 1 ? parts[1] : "4", CultureInfo.InvariantCulture),
                Races = int.Parse(parts.Length > 2 ? parts[2] : "2", CultureInfo.InvariantCulture),
                OutputFile = Arg("-sbSoakOut", "Logs/sb_soak.jsonl"),
                Tag = Arg("-sbSoakTag", ""),
                TimeScale = float.Parse(Arg("-sbSoakTimeScale", "1"), CultureInfo.InvariantCulture),
                TargetFrameRate = int.Parse(Arg("-sbSoakFps", "60"), CultureInfo.InvariantCulture),
            };
            SwitchbackSoakRunner.Launch(s);
        }

        static void OnDone(bool ok)
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sbSoakQuit") < 0) return;
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
