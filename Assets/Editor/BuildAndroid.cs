using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Android APK builder — usable from the Editor menu or the command line.
    ///
    /// Editor:  FrogletTools ▸ Build ▸ Android APK
    /// CLI:     Unity -batchmode -nographics -projectPath . -executeMethod CosmicShore.Editor.BuildAndroid.Build -quit
    ///
    /// Optional CLI args:
    ///   -outputPath path/to/output.apk
    ///   -development        (adds Development Build flag)
    /// </summary>
    public static class BuildAndroid
    {
        private const string DefaultOutputDir = "Builds/Android";
        private const string DefaultApkName = "CosmicShore.apk";

        [MenuItem("FrogletTools/Build/Android APK")]
        public static void BuildFromMenu()
        {
            var outputPath = Path.Combine(DefaultOutputDir, DefaultApkName);
            RunBuild(outputPath, development: false);
        }

        [MenuItem("FrogletTools/Build/Android APK (Development)")]
        public static void BuildFromMenuDev()
        {
            var outputPath = Path.Combine(DefaultOutputDir, DefaultApkName);
            RunBuild(outputPath, development: true);
        }

        /// <summary>
        /// Entry point for command-line builds (e.g. CI).
        /// </summary>
        public static void Build()
        {
            var args = Environment.GetCommandLineArgs();
            var outputPath = GetArgValue(args, "-outputPath")
                             ?? Path.Combine(DefaultOutputDir, DefaultApkName);
            bool development = args.Contains("-development");

            RunBuild(outputPath, development);
        }

        private static void RunBuild(string outputPath, bool development)
        {
            // Ensure output directory exists
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Collect enabled scenes from Build Settings
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildAndroid] No enabled scenes in Build Settings.");
                EditorApplication.Exit(1);
                return;
            }

            // Force Android as the active build target
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[BuildAndroid] Switching active build target to Android...");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            // Build as APK (not AAB)
            EditorUserBuildSettings.buildAppBundle = false;

            var options = BuildOptions.None;
            if (development)
                options |= BuildOptions.Development | BuildOptions.AllowDebugging;

            Debug.Log($"[BuildAndroid] Building APK → {outputPath}");
            Debug.Log($"[BuildAndroid] Scenes ({scenes.Length}): {string.Join(", ", scenes)}");
            Debug.Log($"[BuildAndroid] Development: {development}");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = options,
            });

            var summary = report.summary;

            Debug.Log($"[BuildAndroid] Result: {summary.result}");
            Debug.Log($"[BuildAndroid] Duration: {summary.totalTime}");
            Debug.Log($"[BuildAndroid] Size: {summary.totalSize / (1024 * 1024)} MB");
            Debug.Log($"[BuildAndroid] Warnings: {summary.totalWarnings}  Errors: {summary.totalErrors}");

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[BuildAndroid] Build failed with {summary.totalErrors} error(s).");
                // Exit with error code for CI
                EditorApplication.Exit(1);
            }
            else
            {
                Debug.Log($"[BuildAndroid] APK written to: {Path.GetFullPath(outputPath)}");
            }
        }

        private static string GetArgValue(string[] args, string key)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == key)
                    return args[i + 1];
            }
            return null;
        }
    }
}
