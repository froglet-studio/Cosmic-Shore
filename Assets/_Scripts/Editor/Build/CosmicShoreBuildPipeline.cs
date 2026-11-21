using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Scripted Windows x64 and Android build entry points (Android in
    /// <c>CosmicShoreBuildPipeline.Android.cs</c>). This is the SOURCE OF TRUTH for what a shipping
    /// build is: the in-editor Build Profile is a convenience wrapper, but CI (UGS Build Automation)
    /// and the release checklist both drive the build through here so the same commit always
    /// produces the same player.
    ///
    /// Batchmode usage (what the build server runs):
    /// <code>
    /// Unity -quit -batchmode -nographics -projectPath &lt;proj&gt; \
    ///       -executeMethod CosmicShore.Editor.CosmicShoreBuildPipeline.BuildWindowsRelease \
    ///       -buildOutput Builds/Windows64 -buildVersion 0.2.0 [-buildNumber 42] [-buildCommit &lt;sha&gt;] \
    ///       [-releaseChannel playtest] [-ugsEnvironment production] -logFile -
    /// </code>
    /// The version, build number, commit, channel and UGS environment go into the player's build
    /// stamp (<see cref="BuildStampProcessor"/>, Docs/LAUNCH/BUILD_STAMP.md), which also puts
    /// <c>bundleVersion</c> back after the build. <see cref="WindowsDefine"/> is passed for this
    /// build only and never written into <c>ProjectSettings.asset</c>.
    ///
    /// Exits non-zero on failure so the build server actually fails the job instead of publishing
    /// a half-written depot.
    /// </summary>
    public static partial class CosmicShoreBuildPipeline
    {
        /// <summary>Executable name inside the build folder. Also the SteamPipe launch target.</summary>
        const string ExecutableName = "CosmicShore.exe";

        /// <summary>Default output folder, relative to the project root. Git-ignored.</summary>
        const string DefaultOutputFolder = "Builds/Windows64";

        /// <summary>
        /// Define added to Windows player builds, mirroring the LINUX_BUILD define on the Linux
        /// profile. Use it for desktop-only code paths that must not compile into mobile players.
        /// Passed through <see cref="BuildPlayerOptions.extraScriptingDefines"/>, so it applies to
        /// this build only (it used to be written into the Standalone defines and left there).
        /// </summary>
        const string WindowsDefine = "WINDOWS_BUILD";

        // ──────────────────────────────────────────────
        //  Entry points
        // ──────────────────────────────────────────────

        [MenuItem("FrogletTools/Build/Windows x64 (Release)", false, 120)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 5,
            Description = "Release player build - the artifact that ships.")]
        public static void BuildWindowsRelease() => Run(development: false);

        [MenuItem("FrogletTools/Build/Windows x64 (Development)", false, 121)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 4,
            Description = "Development player build with the profiler attached.")]
        public static void BuildWindowsDevelopment() => Run(development: true);

        [MenuItem("FrogletTools/Build/Reveal Build Folder", false, 122)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 2,
            Description = "Open the last build output in the file browser.")]
        public static void RevealBuildFolder()
        {
            string path = Path.GetFullPath(DefaultOutputFolder);
            Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        // ──────────────────────────────────────────────
        //  Build
        // ──────────────────────────────────────────────

        static void Run(bool development)
        {
            bool batch = Application.isBatchMode;

            try
            {
                string outputFolder = GetArg("-buildOutput") ?? DefaultOutputFolder;
                bool useMono = HasFlag("-useMono");

                // -buildVersion / CS_BUILD_VERSION is applied (and put back) by BuildStampProcessor.
                string[] scenes = RequireScenes();

                ConfigurePlayerSettings(development, useMono);

                string outputPath = Path.Combine(outputFolder, ExecutableName);
                Directory.CreateDirectory(outputFolder);

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = development
                        ? BuildOptions.Development | BuildOptions.AllowDebugging
                        : BuildOptions.None,
                    extraScriptingDefines = new[] { WindowsDefine },
                };

                Debug.Log($"[Build] Windows x64 {(development ? "Development" : "Release")} " +
                          $"-> {Path.GetFullPath(outputPath)}  ({scenes.Length} scenes)");

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;

                if (summary.result != BuildResult.Succeeded)
                    throw new Exception(
                        $"Build {summary.result}. {summary.totalErrors} error(s). See the log above.");

                Debug.Log($"[Build] Succeeded in {summary.totalTime:hh\\:mm\\:ss}. " +
                          $"Size: {summary.totalSize / (1024f * 1024f):F1} MB. " +
                          $"Output: {Path.GetFullPath(outputFolder)}");

                WriteBuildManifest(outputFolder, development);

                if (batch) EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Build] FAILED: {ex.Message}\n{ex}");
                if (batch) EditorApplication.Exit(1);
                else throw;
            }
            finally
            {
                // Puts bundleVersion back and removes the stamp even when the build failed
                // (a failed build never reaches the postprocess callback).
                BuildStampProcessor.Cleanup();
            }
        }

        /// <summary>
        /// Applies the shipping player configuration. Deliberately does NOT touch managed stripping:
        /// Reflex DI, Netcode for GameObjects, and Newtonsoft all resolve types by reflection, and
        /// raising the stripping level silently removes types they need at runtime. Leave it at the
        /// project default unless a link.xml is authored first.
        /// </summary>
        static void ConfigurePlayerSettings(bool development, bool useMono)
        {
            var standalone = NamedBuildTarget.Standalone;

            var backend = useMono ? ScriptingImplementation.Mono2x : ScriptingImplementation.IL2CPP;
            if (PlayerSettings.GetScriptingBackend(standalone) != backend)
            {
                PlayerSettings.SetScriptingBackend(standalone, backend);
                Debug.Log($"[Build] Scripting backend -> {backend}");
            }

            if (backend == ScriptingImplementation.IL2CPP)
            {
                PlayerSettings.SetIl2CppCompilerConfiguration(
                    standalone,
                    development ? Il2CppCompilerConfiguration.Debug : Il2CppCompilerConfiguration.Release);
            }

            // Desktop players must never inherit the mobile "keep the screen awake" behaviour or a
            // portrait default; both are mobile-only concerns that read as bugs on PC.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.runInBackground = true;
        }

        static string[] EnabledScenes() =>
            EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        /// <summary>The enabled scenes; throws when there are none, warns when Bootstrap is not first.</summary>
        static string[] RequireScenes()
        {
            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
                throw new InvalidOperationException(
                    "No enabled scenes in Build Settings. A player with no scenes cannot boot.");

            if (!scenes[0].EndsWith("Bootstrap.unity", StringComparison.OrdinalIgnoreCase))
                Debug.LogWarning(
                    $"[Build] First scene is '{scenes[0]}', not Bootstrap.unity. " +
                    "Bootstrap MUST be build index 0 or DI registration and the splash flow never run.");
            return scenes;
        }

        /// <summary>
        /// Drops a build manifest next to the player. The upload script reads it so the Steam build
        /// description records exactly which commit produced the depot.
        /// </summary>
        static void WriteBuildManifest(string outputFolder, bool development)
        {
            try
            {
                // The player's own stamp: Player Settings are already put back by now.
                var stamp = BuildStampProcessor.LastStamp;
                string manifest =
                    $"version={stamp?.version ?? PlayerSettings.bundleVersion}\n" +
                    $"buildNumber={stamp?.buildNumber}\n" +
                    $"unity={Application.unityVersion}\n" +
                    $"configuration={(development ? "development" : "release")}\n" +
                    $"backend={PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone)}\n" +
                    $"commit={stamp?.commit ?? GetArg("-buildCommit") ?? "unknown"}\n" +
                    $"ref={stamp?.gitRef}\n" +
                    $"channel={stamp?.channel}\n" +
                    $"ugsEnvironment={stamp?.ugsEnvironment}\n" +
                    $"builtAtUtc={stamp?.builtAtUtc ?? DateTime.UtcNow.ToString("o")}\n";

                File.WriteAllText(Path.Combine(outputFolder, "build_manifest.txt"), manifest);
            }
            catch (Exception ex)
            {
                // A missing manifest is not worth failing a good build over.
                Debug.LogWarning($"[Build] Could not write build manifest: {ex.Message}");
            }
        }

        // ──────────────────────────────────────────────
        //  CLI args
        // ──────────────────────────────────────────────

        static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        static bool HasFlag(string name) =>
            Environment.GetCommandLineArgs().Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    }
}
