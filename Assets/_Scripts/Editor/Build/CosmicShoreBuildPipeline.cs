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
    /// Scripted Windows x64 build entry points. This is the SOURCE OF TRUTH for what a shipping
    /// build is: the in-editor Build Profile is a convenience wrapper, but CI (UGS Build Automation)
    /// and the release checklist both drive the build through here so the same commit always
    /// produces the same player.
    ///
    /// Batchmode usage (what the build server runs):
    /// <code>
    /// Unity -quit -batchmode -nographics -projectPath &lt;proj&gt; \
    ///       -executeMethod CosmicShore.Editor.CosmicShoreBuildPipeline.BuildWindowsRelease \
    ///       -buildOutput Builds/Windows64 -buildVersion 0.2.0 -logFile -
    /// </code>
    ///
    /// Exits non-zero on failure so the build server actually fails the job instead of publishing
    /// a half-written depot.
    /// </summary>
    public static class CosmicShoreBuildPipeline
    {
        /// <summary>Executable name inside the build folder. Also the SteamPipe launch target.</summary>
        const string ExecutableName = "CosmicShore.exe";

        /// <summary>Default output folder, relative to the project root. Git-ignored.</summary>
        const string DefaultOutputFolder = "Builds/Windows64";

        /// <summary>
        /// Define added to Windows player builds, mirroring the LINUX_BUILD define on the Linux
        /// profile. Use it for desktop-only code paths that must not compile into mobile players.
        /// </summary>
        const string WindowsDefine = "WINDOWS_BUILD";

        // ──────────────────────────────────────────────
        //  Entry points
        // ──────────────────────────────────────────────

        [MenuItem("FrogletTools/Build/Windows x64 (Release)", false, 100)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 5,
            Description = "Release player build - the artifact that ships.")]
        public static void BuildWindowsRelease() => Run(development: false);

        [MenuItem("FrogletTools/Build/Windows x64 (Development)", false, 101)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 4,
            Description = "Development player build with the profiler attached.")]
        public static void BuildWindowsDevelopment() => Run(development: true);

        [MenuItem("FrogletTools/Build/Reveal Build Folder", false, 120)]
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
                string version = GetArg("-buildVersion");
                bool useMono = HasFlag("-useMono");

                if (!string.IsNullOrWhiteSpace(version))
                {
                    PlayerSettings.bundleVersion = version;
                    Debug.Log($"[Build] Version stamped: {version}");
                }

                string[] scenes = EnabledScenes();
                if (scenes.Length == 0)
                    throw new InvalidOperationException(
                        "No enabled scenes in Build Settings. A player with no scenes cannot boot.");

                if (!scenes[0].EndsWith("Bootstrap.unity", StringComparison.OrdinalIgnoreCase))
                    Debug.LogWarning(
                        $"[Build] First scene is '{scenes[0]}', not Bootstrap.unity. " +
                        "Bootstrap MUST be build index 0 or DI registration and the splash flow never run.");

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

            AddDefineIfMissing(standalone, WindowsDefine);

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

        static void AddDefineIfMissing(NamedBuildTarget target, string define)
        {
            string defines = PlayerSettings.GetScriptingDefineSymbols(target);
            var parts = defines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Contains(define)) return;

            parts.Add(define);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", parts));
            Debug.Log($"[Build] Added scripting define '{define}'.");
        }

        static string[] EnabledScenes() =>
            EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        /// <summary>
        /// Drops a build manifest next to the player. The upload script reads it so the Steam build
        /// description records exactly which commit produced the depot.
        /// </summary>
        static void WriteBuildManifest(string outputFolder, bool development)
        {
            try
            {
                string manifest =
                    $"version={PlayerSettings.bundleVersion}\n" +
                    $"unity={Application.unityVersion}\n" +
                    $"configuration={(development ? "development" : "release")}\n" +
                    $"backend={PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone)}\n" +
                    $"commit={GetArg("-buildCommit") ?? "unknown"}\n" +
                    $"builtAtUtc={DateTime.UtcNow:o}\n";

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
