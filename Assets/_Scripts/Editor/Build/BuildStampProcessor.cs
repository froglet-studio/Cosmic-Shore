using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using CosmicShore.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The build inputs a pipeline passes to stamp a player: each one a command-line argument or
    /// an environment variable (the argument wins), so Unity Build Automation, GameCI and the
    /// <c>CosmicShoreBuildPipeline</c> entry points all set them the same way.
    /// Docs/LAUNCH/BUILD_STAMP.md lists them.
    /// </summary>
    public static class BuildStampInputs
    {
        public const string VersionArg = "-buildVersion";
        public const string VersionEnv = "CS_BUILD_VERSION";
        public const string BuildNumberArg = "-buildNumber";
        public const string BuildNumberEnv = "CS_BUILD_NUMBER";
        public const string CommitArg = "-buildCommit";
        public const string RefArg = "-buildRef";
        public const string RefEnv = "CS_BUILD_REF";
        public const string ChannelArg = "-releaseChannel";
        public const string ChannelEnv = "CS_RELEASE_CHANNEL";
        public const string UgsEnvironmentArg = "-ugsEnvironment";

        /// <summary>The channel a build reports when no pipeline names one: a person built it.</summary>
        public const string LocalChannel = "local";

        /// <summary>The build number a Windows or WebGL build reports when no pipeline names one.</summary>
        public const string LocalBuildNumber = "local";

        public static string Version => Arg(VersionArg) ?? Env(VersionEnv);
        public static string BuildNumber => Arg(BuildNumberArg) ?? Env(BuildNumberEnv);
        public static string Channel => Arg(ChannelArg) ?? Env(ChannelEnv);
        public static string UgsEnvironment => Arg(UgsEnvironmentArg) ?? Env(BuildStamp.UgsEnvironmentEnv);

        /// <summary><c>-buildCommit</c>, then <c>GITHUB_SHA</c>, then <c>git rev-parse HEAD</c>, then "unknown".</summary>
        public static string Commit => Arg(CommitArg) ?? Env("GITHUB_SHA") ?? Git("rev-parse HEAD") ?? "unknown";

        /// <summary>
        /// <c>-buildRef</c> / <c>CS_BUILD_REF</c>, then <c>GITHUB_REF_NAME</c>, then the tag on HEAD,
        /// then the branch; empty when none is known (a detached checkout with no tag).
        /// </summary>
        public static string Ref
        {
            get
            {
                string r = Arg(RefArg) ?? Env(RefEnv) ?? Env("GITHUB_REF_NAME") ?? Git("describe --tags --exact-match HEAD");
                if (r != null) return r;
                string branch = Git("rev-parse --abbrev-ref HEAD");
                return branch == null || branch == "HEAD" ? string.Empty : branch;
            }
        }

        public static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return Clean(args[i + 1]);
            return null;
        }

        public static string Env(string name) => Clean(Environment.GetEnvironmentVariable(name));

        /// <summary>Trimmed, single-line, at most 128 characters; null when empty. Stamp values are shown on screen.</summary>
        public static string Clean(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var sb = new StringBuilder();
            foreach (char c in value.Trim())
                if (!char.IsControl(c)) sb.Append(c);
            string s = sb.ToString();
            if (s.Length > 128) s = s.Substring(0, 128);
            return s.Length == 0 ? null : s;
        }

        static string Git(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = Path.GetDirectoryName(Application.dataPath),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                if (p == null) return null;
                string output = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(5000) || p.ExitCode != 0) return null;
                return Clean(output);
            }
            catch (Exception)
            {
                return null; // no git on this machine (UBA without .git, a zip checkout)
            }
        }
    }

    /// <summary>
    /// Writes the player's build stamp before every build (<see cref="BuildStamp"/>), and applies
    /// <c>-buildVersion</c> / <c>-buildNumber</c> to Player Settings for that build only.
    /// <see cref="BuildStampCleanup"/> puts Player Settings back and removes the stamp once the build
    /// is done, so a build never leaves <c>ProjectSettings.asset</c> or <c>Assets/</c> modified.
    ///
    /// <para>Refuses the build (<see cref="BuildFailedException"/>) when the UGS environment is not
    /// development / production, or when an Android build number is not a positive whole number
    /// (it becomes the versionCode).</para>
    ///
    /// <para>The stamp is a generated, git-ignored file it deletes itself, not an authored asset:
    /// this is not a writer tool in the <c>Docs/TOOLING.md</c> sense and records nothing in the
    /// tool ledger.</para>
    /// </summary>
    public sealed class BuildStampProcessor : IPreprocessBuildWithReport
    {
        public const string ResourcesFolder = "Assets/Resources";
        public const string StampFolder = ResourcesFolder + "/Generated";
        public const string StampAssetPath = StampFolder + "/build_stamp.json";

        /// <summary>Early, so every later callback (and the release guards) sees the final Player Settings.</summary>
        public int callbackOrder => -1000;

        /// <summary>The stamp of the build in progress or the last one made in this editor session.</summary>
        public static BuildStampData LastStamp { get; private set; }

        static PlayerSettingsSnapshot _pending;

        public void OnPreprocessBuild(BuildReport report)
        {
            bool development = ReleaseBuildKind.IsDevelopment(report);
            BuildTarget target = report.summary.platform;

            string environment = ResolveEnvironmentOrRefuse(BuildStampInputs.UgsEnvironment, development);

            // A previous build that failed before its postprocess ran left its values in place.
            Cleanup();
            _pending = PlayerSettingsSnapshot.Capture();
            EditorApplication.delayCall += Cleanup; // a failed build never reaches the postprocess

            string version = BuildStampInputs.Version;
            if (version != null)
                PlayerSettings.bundleVersion = version;

            string buildNumber = ApplyBuildNumber(target, BuildStampInputs.BuildNumber);

            var stamp = new BuildStampData
            {
                version = PlayerSettings.bundleVersion,
                buildNumber = buildNumber,
                commit = BuildStampInputs.Commit,
                gitRef = BuildStampInputs.Ref,
                channel = BuildStampInputs.Channel ?? BuildStampInputs.LocalChannel,
                ugsEnvironment = environment,
                builtAtUtc = DateTime.UtcNow.ToString("o"),
                development = development,
            };

            WriteStamp(stamp);
            LastStamp = stamp;
            Debug.Log($"[Build] Stamp: {BuildStamp.Describe(stamp)} ref '{stamp.gitRef}' " +
                      $"({(development ? "development" : "release")} {target}).");
        }

        /// <summary>The environment rule, with the build's refusal: a bad name fails the build.</summary>
        public static string ResolveEnvironmentOrRefuse(string explicitValue, bool development)
        {
            try
            {
                return BuildStamp.ResolveUgsEnvironment(explicitValue, development, isEditor: false);
            }
            catch (ArgumentException ex)
            {
                throw new BuildFailedException(ex.Message);
            }
        }

        /// <summary>
        /// An Android build number is its versionCode: it must be a positive whole number. Refuses
        /// anything else (<see cref="BuildFailedException"/>); null when none was given.
        /// </summary>
        public static int? ParseAndroidBuildNumber(string requested)
        {
            if (requested == null) return null;
            if (!int.TryParse(requested, out int code) || code <= 0)
                throw new BuildFailedException(
                    $"Build number '{requested}' is not a positive whole number; an Android build number is its versionCode " +
                    $"({BuildStampInputs.BuildNumberArg} / {BuildStampInputs.BuildNumberEnv}).");
            return code;
        }

        /// <summary>Sets the platform's build number when one was given, and returns the number the stamp records.</summary>
        static string ApplyBuildNumber(BuildTarget target, string requested)
        {
            switch (target)
            {
                case BuildTarget.Android:
                    // The Android pipeline's own -buildVersionCode wins, as it does there.
                    int? code = ParseAndroidBuildNumber(BuildStampInputs.Arg("-buildVersionCode") ?? requested);
                    if (code.HasValue) PlayerSettings.Android.bundleVersionCode = code.Value;
                    return PlayerSettings.Android.bundleVersionCode.ToString();
                case BuildTarget.iOS:
                    if (requested != null) PlayerSettings.iOS.buildNumber = requested;
                    return PlayerSettings.iOS.buildNumber;
                default:
                    // Windows and WebGL have no build number in Player Settings; the stamp carries it.
                    return requested ?? BuildStampInputs.LocalBuildNumber;
            }
        }

        static void WriteStamp(BuildStampData stamp)
        {
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                throw new BuildFailedException($"{ResourcesFolder} is missing; the build stamp has nowhere to go.");
            if (!AssetDatabase.IsValidFolder(StampFolder))
                AssetDatabase.CreateFolder(ResourcesFolder, "Generated");

            File.WriteAllText(StampAssetPath, JsonUtility.ToJson(stamp, true));
            AssetDatabase.ImportAsset(StampAssetPath, ImportAssetOptions.ForceSynchronousImport);

            if (AssetDatabase.LoadAssetAtPath<TextAsset>(StampAssetPath) == null)
                throw new BuildFailedException($"The build stamp at {StampAssetPath} did not import as a TextAsset.");
        }

        /// <summary>
        /// Puts Player Settings back and deletes the stamp. Safe to call any number of times; the
        /// postprocess, the editor's next tick after a failed build, and the scripted pipelines'
        /// <c>finally</c> blocks all call it.
        /// </summary>
        public static void Cleanup()
        {
            EditorApplication.delayCall -= Cleanup;

            if (_pending != null)
            {
                _pending.Restore();
                _pending = null;
            }

            if (AssetDatabase.IsValidFolder(StampFolder))
                AssetDatabase.DeleteAsset(StampFolder);
        }

        sealed class PlayerSettingsSnapshot
        {
            string _version;
            string _iosBuildNumber;
            int _androidVersionCode;

            public static PlayerSettingsSnapshot Capture() => new PlayerSettingsSnapshot
            {
                _version = PlayerSettings.bundleVersion,
                _iosBuildNumber = PlayerSettings.iOS.buildNumber,
                _androidVersionCode = PlayerSettings.Android.bundleVersionCode,
            };

            public void Restore()
            {
                if (PlayerSettings.bundleVersion != _version) PlayerSettings.bundleVersion = _version;
                if (PlayerSettings.iOS.buildNumber != _iosBuildNumber) PlayerSettings.iOS.buildNumber = _iosBuildNumber;
                if (PlayerSettings.Android.bundleVersionCode != _androidVersionCode)
                    PlayerSettings.Android.bundleVersionCode = _androidVersionCode;
            }
        }
    }

    /// <summary>Last of the postprocess callbacks: restores Player Settings and removes the stamp (<see cref="BuildStampProcessor.Cleanup"/>).</summary>
    public sealed class BuildStampCleanup : IPostprocessBuildWithReport
    {
        public int callbackOrder => 10000;

        public void OnPostprocessBuild(BuildReport report) => BuildStampProcessor.Cleanup();
    }
}
