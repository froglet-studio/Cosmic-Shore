using System;
using System.Globalization;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Starts <see cref="ThresherTimeSoakRunner"/> in a DEVELOPMENT player from the command line, the way the editor's
    /// ThresherTimeSoakLauncher starts it in the editor, with the same arguments:
    /// <code>CosmicShore.exe -ttSoak "1,2,3,4:1" -ttSoakOut C:\path\tt_soak.jsonl -ttSoakTag exe -ttSoakQuit</code>
    /// (<c>-ttSoakAimed</c>, <c>-ttSoakBelfryAI</c>, <c>-ttSoakSeed</c>, <c>-ttSoakTimeScale</c>, <c>-ttSoakFps</c> as in
    /// the launcher). It races offline (a local host) and puts the player's own offline preference back when the soak
    /// ends. A release player and the editor ignore the arguments: the runner hands the host seat to the AI through
    /// PilotControl, which exists only in editor and development builds. A tool, never a player path.
    /// </summary>
    public static class ThresherTimeSoakBoot
    {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        const string SoakArg = "-ttSoak";
        const string OfflineKey = "CosmicShore.OfflinePreferred";

        static int _offlineWas;

        static string Arg(string name, string fallback)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : fallback;
        }

        static bool Asked => Array.IndexOf(Environment.GetCommandLineArgs(), SoakArg) >= 0;

        // Before the first scene: the offline preference is read while Bootstrap comes up.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PreferOffline()
        {
            if (!Asked) return;
            _offlineWas = PlayerPrefs.GetInt(OfflineKey, 0);
            PlayerPrefs.SetInt(OfflineKey, 1);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (!Asked) return;
            var inv = CultureInfo.InvariantCulture;
            var parts = Arg(SoakArg, "1:2").Split(':');
            var s = new ThresherTimeSoakRunner.Settings
            {
                Intensities = Array.ConvertAll(parts[0].Split(','), x => int.Parse(x, inv)),
                Races = int.Parse(parts.Length > 1 ? parts[1] : "2", inv),
                Aimed = Arg("-ttSoakAimed", "1") != "0",
                BelfryAI = Arg("-ttSoakBelfryAI", "1") != "0",
                FirstSeed = int.Parse(Arg("-ttSoakSeed", "7001"), inv),
                OutputFile = Arg("-ttSoakOut", "Logs/tt_soak.jsonl"),
                Tag = Arg("-ttSoakTag", ""),
                TimeScale = float.Parse(Arg("-ttSoakTimeScale", "1"), inv),
                TargetFrameRate = int.Parse(Arg("-ttSoakFps", "60"), inv),
                Watch = !Application.isBatchMode,
            };
            ThresherTimeSoakRunner.AllDone -= OnDone;
            ThresherTimeSoakRunner.AllDone += OnDone;
            ThresherTimeSoakRunner.Launch(s);
        }

        static void OnDone(bool ok)
        {
            ThresherTimeSoakRunner.AllDone -= OnDone;
            PlayerPrefs.SetInt(OfflineKey, _offlineWas);
            PlayerPrefs.Save();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-ttSoakQuit") >= 0) Application.Quit(ok ? 0 : 1);
        }
#endif
    }
}
