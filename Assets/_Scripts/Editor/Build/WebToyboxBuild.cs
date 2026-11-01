using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
    /// The browser Toybox build: a WebGL player of Menu_Main's freestyle toybox with every toy, single
    /// player, no Unity Gaming Services (<c>Docs/WEBGL_TOYBOX.md</c>). This script IS the build: Unity
    /// Build Automation, the command line and the menu all run it, so the same commit always produces
    /// the same player.
    ///
    /// <para><b>What it sets, for this build only.</b> The four scenes the toybox needs
    /// (<see cref="Scenes"/>, whatever the global Build Settings list holds) and the
    /// <see cref="WebToybox.Define"/> scripting define, passed through
    /// <see cref="BuildPlayerOptions.extraScriptingDefines"/> so it never lands in
    /// <c>ProjectSettings.asset</c> and no other build can carry it. The WebGL player settings
    /// (compression, memory, template, file hashing) are committed in <c>ProjectSettings.asset</c>
    /// and only read here, into the manifest.</para>
    ///
    /// <para><b>Nothing persists.</b> <c>-buildVersion</c> is put back after the build. A BUILD tool, not an asset writer: it writes no assets and records
    /// nothing in the tool ledger. Its only output is under the git-ignored <c>Builds/</c>, so it
    /// draws no ship panel (<c>Docs/TOOLING.md</c> §6).</para>
    ///
    /// Batchmode (what Unity Build Automation runs; exits non-zero on failure):
    /// <code>
    /// Unity -quit -batchmode -nographics -buildTarget WebGL -projectPath &lt;proj&gt; \
    ///       -executeMethod CosmicShore.Editor.WebToyboxBuild.Build \
    ///       [-buildOutput Builds/WebGLToybox] [-buildVersion 0.2.1] [-buildCommit &lt;sha&gt;] \
    ///       [-development] -logFile -
    /// </code>
    /// The output folder holds <c>index.html</c> (the <c>FrogletToybox</c> template), <c>Build/</c>,
    /// <c>StreamingAssets/</c> (the FMOD banks) and <c>build_manifest.txt</c>.
    ///
    /// <para><b>Unity Build Automation</b> builds with its own pipeline, so it does not call
    /// <see cref="Build"/>: its WebGL target names <see cref="PreExport"/> as the Pre-Export Method,
    /// which makes UBA's throwaway checkout build exactly what <see cref="Build"/> builds.</para>
    /// </summary>
    public static class WebToyboxBuild
    {
        /// <summary>The toybox's scenes, in build-index order. Bootstrap must stay first. The voyage scene rides along
        /// because the app shell's Voyage window sets sail into it (<c>VoyageLaunch</c>); the sim farm's player
        /// (<c>SimFarmBuild</c>) builds this same list, so its escort sims load it too.</summary>
        public static readonly string[] Scenes =
        {
            "Assets/_Scenes/Bootstrap.unity",
            "Assets/_Scenes/Authentication.unity",
            "Assets/_Scenes/Menu_Main.unity",
            "Assets/_Scenes/Voyage.unity",
        };

        /// <summary>Default output folder, relative to the project root. Git-ignored (<c>/[Bb]uilds/</c>).</summary>
        public const string DefaultOutputFolder = "Builds/WebGLToybox";

        /// <summary>The WebGL template the website's loader page expects (<c>Assets/WebGLTemplates/</c>).</summary>
        public const string Template = "PROJECT:FrogletToybox";

        // ──────────────────────────────────────────────
        //  Entry points
        // ──────────────────────────────────────────────

        [MenuItem("FrogletTools/Build/WebGL Toybox", false, 123)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 3,
            Description = "Browser Toybox WebGL build: four scenes, FROGLET_WEB_TOYBOX, Builds/WebGLToybox.",
            DocPath = "Docs/WEBGL_TOYBOX.md")]
        public static void Build() => Run(development: HasFlag("-development"));

        [MenuItem("FrogletTools/Build/WebGL Toybox (Development)", false, 124)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 2,
            Description = "Development WebGL build of the browser Toybox (logging FMOD, profiler).",
            DocPath = "Docs/WEBGL_TOYBOX.md")]
        public static void BuildDevelopment() => Run(development: true);

        /// <summary>
        /// Unity Build Automation's Pre-Export Method (<c>CosmicShore.Editor.WebToyboxBuild.PreExport</c>)
        /// for the WebGL Toybox target. UBA builds with its own pipeline and reads the GLOBAL settings, so
        /// on its throwaway checkout this rewrites them to match <see cref="Build"/>: the Build Settings
        /// scene list becomes exactly <see cref="Scenes"/> (enabled, in order), and
        /// <see cref="WebToybox.Define"/> is appended to the WebGL scripting defines.
        ///
        /// <para><b>UBA only. Never run it in a developer's editor:</b> it rewrites
        /// <c>EditorBuildSettings.asset</c> and <c>ProjectSettings.asset</c>, which would put the toybox's
        /// define and scene list into every later build from that checkout. No menu item calls it, and it
        /// refuses to run outside batchmode. It throws when a scene is missing, so UBA fails the build
        /// rather than silently building the full game.</para>
        /// </summary>
        public static void PreExport()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException(
                    "WebToyboxBuild.PreExport is Unity Build Automation's pre-export step and rewrites the " +
                    "global scene list and scripting defines. It refuses to run in an interactive editor; use " +
                    "FrogletTools > Build > WebGL Toybox there.");

            string[] scenes = RequireScenes();
            EditorBuildSettings.scenes = scenes.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();

            var webGL = NamedBuildTarget.WebGL;
            var defines = PlayerSettings.GetScriptingDefineSymbols(webGL)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (!defines.Contains(WebToybox.Define))
            {
                defines.Add(WebToybox.Define);
                PlayerSettings.SetScriptingDefineSymbols(webGL, string.Join(";", defines));
            }

            Debug.Log($"[Build] UBA pre-export: WebGL Toybox scene list ({string.Join(", ", scenes)}) and " +
                      $"WebGL defines '{PlayerSettings.GetScriptingDefineSymbols(webGL)}'.");
        }

        // ──────────────────────────────────────────────
        //  Build
        // ──────────────────────────────────────────────

        static void Run(bool development)
        {
            bool batch = Application.isBatchMode;
            bool ok = false;
            string savedVersion = PlayerSettings.bundleVersion;

            try
            {
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                    throw new InvalidOperationException(
                        "Web Build Support is not installed for this editor. Unity Hub > Installs > this " +
                        "version > Add modules > Web Build Support.");

                // Switching platform reimports every asset, so it is a deliberate step done once per
                // machine, never a side effect of a build menu. Batchmode passes -buildTarget WebGL.
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                    throw new InvalidOperationException(
                        "The editor is not on Web. File > Build Profiles > Web > Switch Platform (it reimports " +
                        "assets once), then build again. Batchmode: pass -buildTarget WebGL.");

                string outputFolder = GetArg("-buildOutput") ?? DefaultOutputFolder;
                string version = GetArg("-buildVersion");
                string[] scenes = RequireScenes();

                if (PlayerSettings.WebGL.template != Template)
                    Debug.LogWarning($"[Build] WebGL template is '{PlayerSettings.WebGL.template}', not '{Template}'. " +
                                     "The website's loader page needs the FrogletToybox template's canvas and messages.");

                if (!string.IsNullOrWhiteSpace(version))
                    PlayerSettings.bundleVersion = version;

                Directory.CreateDirectory(outputFolder);
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputFolder,
                    target = BuildTarget.WebGL,
                    targetGroup = BuildTargetGroup.WebGL,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                    extraScriptingDefines = new[] { WebToybox.Define },
                };

                Debug.Log($"[Build] WebGL Toybox {(development ? "Development" : "Release")} " +
                          $"{PlayerSettings.bundleVersion} -> {Path.GetFullPath(outputFolder)}  " +
                          $"({scenes.Length} scenes, define {WebToybox.Define})");

                BuildReport build;
                using (UnityPipelineReleaseGuard.DeclareBuildScenes(scenes))
                    build = BuildPipeline.BuildPlayer(options);

                BuildSummary summary = build.summary;
                if (summary.result != BuildResult.Succeeded)
                    throw new Exception($"Build {summary.result}. {summary.totalErrors} error(s). See the log above.");

                Debug.Log($"[Build] Succeeded in {summary.totalTime:hh\\:mm\\:ss}. " +
                          $"Size: {summary.totalSize / (1024f * 1024f):F1} MB. Output: {Path.GetFullPath(outputFolder)}");

                WriteBuildManifest(outputFolder, development, scenes);
                ok = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Build] WebGL Toybox FAILED: {ex.Message}\n{ex}");
                if (!batch) EditorUtility.DisplayDialog("WebGL Toybox build stopped", ex.Message, "OK");
            }
            finally
            {
                PlayerSettings.bundleVersion = savedVersion;
            }

            if (batch) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Every toybox scene, checked to exist; throws naming the missing one.</summary>
        static string[] RequireScenes()
        {
            foreach (string path in Scenes)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                    throw new FileNotFoundException(
                        $"The browser Toybox needs {path}, and there is no scene there. Update WebToyboxBuild.Scenes.");
            return Scenes.ToArray();
        }

        // ──────────────────────────────────────────────
        //  Manifest
        // ──────────────────────────────────────────────

        // Read by name, so the manifest names whatever this editor has without the script depending on
        // API newer than the editor it compiles in (the memory settings arrived in 2021.2).
        static readonly string[] ManifestSettings =
        {
            "compressionFormat", "decompressionFallback", "nameFilesAsHashes", "dataCaching",
            "exceptionSupport", "template", "initialMemorySize", "maximumMemorySize", "memoryGrowthMode",
            "powerPreference", "threadsSupport",
        };

        /// <summary>
        /// Drops a manifest next to the player, recording what was built and with which WebGL settings,
        /// so a deployed build can be traced to its commit and configuration.
        /// </summary>
        static void WriteBuildManifest(string outputFolder, bool development, string[] scenes)
        {
            try
            {
                var text = new StringBuilder()
                    .Append($"version={PlayerSettings.bundleVersion}\n")
                    .Append($"unity={Application.unityVersion}\n")
                    .Append("target=WebGL\n")
                    .Append($"configuration={(development ? "development" : "release")}\n")
                    .Append($"define={WebToybox.Define}\n")
                    .Append($"scenes={string.Join(",", scenes)}\n");

                foreach (string name in ManifestSettings)
                {
                    var property = typeof(PlayerSettings.WebGL).GetProperty(name, BindingFlags.Public | BindingFlags.Static);
                    text.Append($"webgl.{name}={(property != null ? property.GetValue(null) : "n/a")}\n");
                }

                text.Append($"webgl.codeOptimization={CodeOptimization()}\n")
                    .Append($"commit={GetArg("-buildCommit") ?? "unknown"}\n")
                    .Append($"builtAtUtc={DateTime.UtcNow:o}\n");

                File.WriteAllText(Path.Combine(outputFolder, "build_manifest.txt"), text.ToString());
            }
            catch (Exception ex)
            {
                // A missing manifest is not worth failing a good build over.
                Debug.LogWarning($"[Build] Could not write build manifest: {ex.Message}");
            }
        }

        // UnityEditor.WebGL.UserBuildSettings lives in the Web module's editor extension, which exists only
        // where Web Build Support is installed, so it is reached by reflection (as the Android build
        // reaches its debug-symbol settings).
        static string CodeOptimization()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.WebGL.UserBuildSettings", false))
                .FirstOrDefault(t => t != null);
            var property = type?.GetProperty("codeOptimization", BindingFlags.Public | BindingFlags.Static);
            return property != null ? property.GetValue(null)?.ToString() ?? "n/a" : "n/a";
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
