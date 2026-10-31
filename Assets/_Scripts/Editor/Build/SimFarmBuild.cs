using System;
using System.IO;
using System.Linq;
using System.Text;
using CosmicShore.Core;
using CosmicShore.Editor.Froglet;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The <b>escort sim farm</b> player: a Windows x64 development player of the browser Toybox's boot path (Bootstrap,
    /// Authentication, Menu_Main; <see cref="WebToyboxBuild.Scenes"/>) with <see cref="WebToybox.Define"/>, so it runs with
    /// no Unity Gaming Services at all (no sign-in, lobby, Relay) and hosts on the in-memory <c>SinglePlayerTransport</c>:
    /// many copies run side by side, each playing Ark escorts for <c>Tools/SimFarm/farm.py</c> through
    /// <c>ArkEscortSim</c>'s <c>-arkEscortSim @job.json</c>. A tool, never shipped.
    ///
    /// <para><b>Nothing persists.</b> The define goes through <see cref="BuildPlayerOptions.extraScriptingDefines"/>; the
    /// scripting backend (IL2CPP, about 30 min; <c>-mono</c> builds in about 10), the product name (<see cref="ProductName"/>, so the farm's PlayerPrefs
    /// and persistent data never mix with the game's) and the fullscreen mode are set for the build and put back after it,
    /// whatever happens. Output is under the git-ignored <c>Builds/</c> unless <c>-buildOutput</c> names a folder.</para>
    ///
    /// <code>
    /// Unity -quit -batchmode -nographics -buildTarget StandaloneWindows64 -projectPath &lt;proj&gt; \
    ///       -executeMethod CosmicShore.Editor.SimFarmBuild.Build [-buildOutput D:\VoyageFarm\build] [-mono] -logFile &lt;log&gt;
    /// </code>
    /// </summary>
    public static class SimFarmBuild
    {
        public const string DefaultOutputFolder = "Builds/SimFarm";
        public const string ExeName = "CosmicShoreSimFarm.exe";

        /// <summary>The farm player's product name: its own PlayerPrefs key and persistentDataPath folder.</summary>
        public const string ProductName = "Cosmic Shore SimFarm";

        [MenuItem("FrogletTools/Build/Escort Sim Farm (Windows)", false, 125)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 1,
            Description = "Windows dev player for the Ark escort sim farm: toybox scenes, FROGLET_WEB_TOYBOX, IL2CPP, Builds/SimFarm.",
            DocPath = "Tools/SimFarm/README.md")]
        public static void Build()
        {
            bool batch = Application.isBatchMode;
            bool ok = false;
            var standalone = NamedBuildTarget.Standalone;
            var savedBackend = PlayerSettings.GetScriptingBackend(standalone);
            string savedProduct = PlayerSettings.productName;
            var savedFullscreen = PlayerSettings.fullScreenMode;
            bool savedResizable = PlayerSettings.resizableWindow;
            bool savedRunInBackground = PlayerSettings.runInBackground;
            bool savedVisibleInBackground = PlayerSettings.visibleInBackground;
            bool savedSplash = PlayerSettings.SplashScreen.show;

            try
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                    throw new InvalidOperationException(
                        $"The editor is on {EditorUserBuildSettings.activeBuildTarget}, not StandaloneWindows64. " +
                        "Batchmode: pass -buildTarget StandaloneWindows64 (it reimports assets once).");

                string outputFolder = GetArg("-buildOutput") ?? DefaultOutputFolder;
                string[] scenes = WebToyboxBuild.Scenes.ToArray();
                foreach (string path in scenes)
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                        throw new FileNotFoundException($"The sim farm needs {path}, and there is no scene there.");

                bool il2cpp = !HasFlag("-mono");
                PlayerSettings.SetScriptingBackend(standalone, il2cpp ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);
                PlayerSettings.productName = ProductName;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.resizableWindow = true;
                PlayerSettings.runInBackground = true;
                PlayerSettings.visibleInBackground = true;
                try { PlayerSettings.SplashScreen.show = false; } catch (Exception) { /* licence-dependent */ }

                Directory.CreateDirectory(outputFolder);
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = Path.Combine(outputFolder, ExeName),
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.Development,
                    extraScriptingDefines = new[] { WebToybox.Define, "WINDOWS_BUILD" },
                };

                Debug.Log($"[Build] Sim farm player ({(il2cpp ? "IL2CPP" : "Mono")}, development) -> " +
                          $"{Path.GetFullPath(options.locationPathName)} ({scenes.Length} scenes, define {WebToybox.Define})");

                BuildReport build;
                using (UnityPipelineReleaseGuard.DeclareBuildScenes(scenes))
                    build = BuildPipeline.BuildPlayer(options);

                BuildSummary summary = build.summary;
                if (summary.result != BuildResult.Succeeded)
                    throw new Exception($"Build {summary.result}. {summary.totalErrors} error(s). See the log above.");

                Debug.Log($"[Build] Sim farm player succeeded in {summary.totalTime:hh\\:mm\\:ss}, " +
                          $"{summary.totalSize / (1024f * 1024f):F1} MB.");
                File.WriteAllText(Path.Combine(outputFolder, "build_manifest.txt"), new StringBuilder()
                    .Append($"exe={ExeName}\nproduct={ProductName}\nunity={Application.unityVersion}\n")
                    .Append($"target=StandaloneWindows64\nbackend={(il2cpp ? "IL2CPP" : "Mono")}\nconfiguration=development\n")
                    .Append($"defines={WebToybox.Define};WINDOWS_BUILD\nscenes={string.Join(",", scenes)}\n")
                    .Append($"commit={GetArg("-buildCommit") ?? "unknown"}\nbuiltAtUtc={DateTime.UtcNow:o}\n").ToString());
                ok = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Build] Sim farm player FAILED: {ex.Message}\n{ex}");
                if (!batch) EditorUtility.DisplayDialog("Sim farm build stopped", ex.Message, "OK");
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(standalone, savedBackend);
                PlayerSettings.productName = savedProduct;
                PlayerSettings.fullScreenMode = savedFullscreen;
                PlayerSettings.resizableWindow = savedResizable;
                PlayerSettings.runInBackground = savedRunInBackground;
                PlayerSettings.visibleInBackground = savedVisibleInBackground;
                try { PlayerSettings.SplashScreen.show = savedSplash; } catch (Exception) { }
                AssetDatabase.SaveAssets();
            }

            if (batch) EditorApplication.Exit(ok ? 0 : 1);
        }

        static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        static bool HasFlag(string name) =>
            Environment.GetCommandLineArgs().Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    }
}
