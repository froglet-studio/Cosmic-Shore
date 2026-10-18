using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The Android release build: a signed App Bundle (<c>.aab</c>) for Google Play, checked by
    /// <see cref="AndroidBundleCheck"/> before anyone uploads it. Setup and the Play Console steps:
    /// <c>Docs/ANDROID_BUILD.md</c>.
    ///
    /// <para><b>Signing.</b> The upload key comes from the <c>CS_ANDROID_*</c> environment variables
    /// when <see cref="KeystoreEnv"/> is set (CI, or a PC that prefers it); otherwise from Player
    /// Settings &gt; Android &gt; Publishing Settings, whose passwords Unity keeps only until the
    /// editor closes. With neither, the build stops: an App Bundle signed with the debug key is
    /// rejected by Play.</para>
    ///
    /// <para><b>Version code.</b> Minutes since 2026-01-01 UTC
    /// (<see cref="AndroidBundleCheck.VersionCodeAt"/>), so every build is higher than the last
    /// without anyone editing Player Settings. <c>-buildVersionCode N</c> overrides it.</para>
    ///
    /// <para><b>Debug symbols.</b> The native symbol table goes into the bundle, so Play's
    /// "you've not uploaded debug symbols" warning does not appear and crash reports are readable
    /// (<see cref="EmbedDebugSymbols"/>).</para>
    ///
    /// <para><b>Nothing persists.</b> The version, version code, keystore fields, app category, debug
    /// symbol settings and the App Bundle switch are put back after the build, so it never leaves
    /// <c>ProjectSettings.asset</c> modified, and never writes a keystore path into it. Its only
    /// output is under the git-ignored <c>Builds/</c>, so it is not a writer tool in the
    /// <c>Docs/TOOLING.md</c> sense and carries no ship panel.</para>
    ///
    /// Batchmode:
    /// <code>
    /// Unity -quit -batchmode -nographics -buildTarget Android -projectPath &lt;proj&gt; \
    ///       -executeMethod CosmicShore.Editor.CosmicShoreBuildPipeline.BuildAndroidRelease \
    ///       -buildOutput Builds/Android [-buildVersion 0.2.1] [-buildVersionCode 412345] -logFile -
    /// </code>
    /// </summary>
    public static partial class CosmicShoreBuildPipeline
    {
        /// <summary>Default Android output folder, relative to the project root. Git-ignored.</summary>
        const string AndroidOutputFolder = "Builds/Android";

        /// <summary>Path to the upload keystore. When set, signing comes from these variables.</summary>
        public const string KeystoreEnv = "CS_ANDROID_KEYSTORE";
        public const string KeystorePassEnv = "CS_ANDROID_KEYSTORE_PASS";
        /// <summary>Optional; defaults to <see cref="DefaultKeyAlias"/>.</summary>
        public const string KeyAliasEnv = "CS_ANDROID_KEY_ALIAS";
        /// <summary>Optional; defaults to the keystore password, which is what keytool does too.</summary>
        public const string KeyPassEnv = "CS_ANDROID_KEY_PASS";

        /// <summary>The alias <c>Docs/ANDROID_BUILD.md</c> tells everyone to create.</summary>
        public const string DefaultKeyAlias = "cosmicshore-upload";

        // ──────────────────────────────────────────────
        //  Entry points
        // ──────────────────────────────────────────────

        [MenuItem("FrogletTools/Build/Android App Bundle (Release)", false, 110)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 5,
            Description = "Signed .aab for Google Play, checked for everything Play rejects. Docs/ANDROID_BUILD.md.")]
        public static void BuildAndroidRelease() => RunAndroid();

        [MenuItem("FrogletTools/Build/Check Android App Bundle...", false, 111)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 3,
            Description = "Run the pre-upload Play check on an existing .aab.")]
        public static void CheckAndroidBundle()
        {
            string path = EditorUtility.OpenFilePanel("Android App Bundle", AndroidOutputFolder, "aab");
            if (string.IsNullOrEmpty(path)) return;

            var report = AndroidBundleCheck.Check(path, PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
            WriteCheckReport(path, report);
            ShowCheckResult(path, report, built: false);
        }

        // ──────────────────────────────────────────────
        //  Build
        // ──────────────────────────────────────────────

        static void RunAndroid()
        {
            bool batch = Application.isBatchMode;
            bool ok = false;
            var saved = AndroidBuildSettingsSnapshot.Capture();

            try
            {
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                    throw new InvalidOperationException(
                        "Android Build Support is not installed for this editor. Unity Hub > Installs > " +
                        "this version > Add modules > Android Build Support (with OpenJDK and Android SDK & NDK).");

                // Switching platform reimports every asset, so it is a deliberate step done once per
                // machine, never a side effect of a build menu. Batchmode passes -buildTarget Android.
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                    throw new InvalidOperationException(
                        "The editor is not on Android. File > Build Profiles > Android > Switch Platform " +
                        "(it reimports assets once), then build again. Batchmode: pass -buildTarget Android.");

                string outputFolder = GetArg("-buildOutput") ?? AndroidOutputFolder;
                string version = GetArg("-buildVersion");
                string[] scenes = RequireScenes();

                RequireAndroidPlayerSettings();
                string signing = ApplyUploadKey();

                if (!string.IsNullOrWhiteSpace(version))
                    PlayerSettings.bundleVersion = version;

                int versionCode = ResolveVersionCode(GetArg("-buildVersionCode"));
                PlayerSettings.Android.bundleVersionCode = versionCode;
                SetAppCategoryGame();
                EmbedDebugSymbols();

                EditorUserBuildSettings.buildAppBundle = true;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

                Directory.CreateDirectory(outputFolder);
                string outputPath = Path.Combine(outputFolder,
                    $"CosmicShore-{PlayerSettings.bundleVersion}-{versionCode}.aab");

                Debug.Log($"[Build] Android App Bundle {PlayerSettings.bundleVersion} (code {versionCode}), " +
                          $"signed with the {signing} -> {Path.GetFullPath(outputPath)}  ({scenes.Length} scenes)");

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None,
                };

                BuildReport build = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = build.summary;
                if (summary.result != BuildResult.Succeeded)
                    throw new Exception(
                        $"Build {summary.result}. {summary.totalErrors} error(s). See the log above.");

                Debug.Log($"[Build] Succeeded in {summary.totalTime:hh\\:mm\\:ss}. " +
                          $"Size: {new FileInfo(outputPath).Length / (1024f * 1024f):F1} MB.");

                var report = AndroidBundleCheck.Check(outputPath,
                    PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
                WriteCheckReport(outputPath, report);

                if (batch)
                {
                    if (!report.Passed)
                        throw new Exception("The App Bundle check failed; Play would reject this bundle. See the report above.");
                }
                else
                {
                    ShowCheckResult(outputPath, report, built: true);
                }

                ok = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Build] Android FAILED: {ex.Message}\n{ex}");
                if (!batch) EditorUtility.DisplayDialog("Android build stopped", ex.Message, "OK");
            }
            finally
            {
                saved.Restore();
            }

            if (batch) EditorApplication.Exit(ok ? 0 : 1);
        }

        static int ResolveVersionCode(string requested)
        {
            if (!string.IsNullOrWhiteSpace(requested))
            {
                if (!int.TryParse(requested, out int code) || code <= 0)
                    throw new ArgumentException($"-buildVersionCode '{requested}' is not a positive whole number.");
                return code;
            }

            return Math.Max(PlayerSettings.Android.bundleVersionCode + 1, AndroidBundleCheck.VersionCodeAt(DateTime.UtcNow));
        }

        /// <summary>
        /// Fails on the Player Settings Play rejects outright. They are already right on
        /// bleeding-edge (<c>Docs/PLATFORM_UNIFICATION.md</c> §3.9), so a failure here means a
        /// local edit; the build does not silently change them back.
        /// </summary>
        static void RequireAndroidPlayerSettings()
        {
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
                throw new InvalidOperationException(
                    "Android's scripting backend is not IL2CPP, and Play requires 64-bit code, which needs IL2CPP. " +
                    "Player Settings > Android > Other Settings > Scripting Backend.");

            if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
                throw new InvalidOperationException(
                    "ARM64 is not a target architecture, and Play requires 64-bit. " +
                    "Player Settings > Android > Other Settings > Target Architectures.");

            int target = (int)PlayerSettings.Android.targetSdkVersion;
            if (target != 0 && target < AndroidBundleCheck.RequiredTargetSdk)
                throw new InvalidOperationException(
                    $"Target API level is {target}; Play requires {AndroidBundleCheck.RequiredTargetSdk}. " +
                    "Player Settings > Android > Other Settings > Target API Level.");
        }

        /// <summary>
        /// Points the build at the upload key, from the environment or from Publishing Settings,
        /// and returns a description for the log. Never logs a password.
        /// </summary>
        static string ApplyUploadKey()
        {
            string keystore = Environment.GetEnvironmentVariable(KeystoreEnv);
            if (!string.IsNullOrEmpty(keystore))
            {
                if (!File.Exists(keystore))
                    throw new FileNotFoundException($"{KeystoreEnv} points at {keystore}, and there is no file there.");

                string storePass = Environment.GetEnvironmentVariable(KeystorePassEnv);
                if (string.IsNullOrEmpty(storePass))
                    throw new InvalidOperationException($"{KeystoreEnv} is set but {KeystorePassEnv} is not.");

                string alias = Environment.GetEnvironmentVariable(KeyAliasEnv);
                if (string.IsNullOrEmpty(alias)) alias = DefaultKeyAlias;
                string keyPass = Environment.GetEnvironmentVariable(KeyPassEnv);
                if (string.IsNullOrEmpty(keyPass)) keyPass = storePass;

                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = Path.GetFullPath(keystore);
                PlayerSettings.Android.keystorePass = storePass;
                PlayerSettings.Android.keyaliasName = alias;
                PlayerSettings.Android.keyaliasPass = keyPass;
                return $"upload key from {KeystoreEnv} ({Path.GetFileName(keystore)}, alias {alias})";
            }

            if (PlayerSettings.Android.useCustomKeystore && !string.IsNullOrEmpty(PlayerSettings.Android.keystoreName))
            {
                if (!File.Exists(PlayerSettings.Android.keystoreName))
                    throw new FileNotFoundException(
                        $"Publishing Settings points at the keystore {PlayerSettings.Android.keystoreName}, and there is no file there.");
                if (string.IsNullOrEmpty(PlayerSettings.Android.keyaliasName))
                    throw new InvalidOperationException("Publishing Settings has a keystore but no key alias selected.");
                if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
                    throw new InvalidOperationException(
                        "Publishing Settings has the keystore but not its passwords; Unity forgets them whenever the " +
                        "editor restarts. Enter both under Player Settings > Android > Publishing Settings, then build again.");

                return $"upload key from Publishing Settings ({Path.GetFileName(PlayerSettings.Android.keystoreName)}, " +
                       $"alias {PlayerSettings.Android.keyaliasName})";
            }

            throw new InvalidOperationException(
                "No upload key. Play rejects a bundle signed with the debug key. Either select the key under " +
                "Player Settings > Android > Publishing Settings > Custom Keystore, or set " +
                $"{KeystoreEnv} and {KeystorePassEnv}. Docs/ANDROID_BUILD.md section 1.");
        }

        // PlayerSettings.Android.appCategory (a string, "game") is newer than the API this code is
        // compiled against outside Unity, so it is reached by reflection. Absent, the bundle check
        // still reports the category the manifest actually carries.
        static readonly PropertyInfo AppCategoryProperty =
            typeof(PlayerSettings.Android).GetProperty("appCategory", BindingFlags.Public | BindingFlags.Static);

        static void SetAppCategoryGame()
        {
            if (AppCategoryProperty == null || AppCategoryProperty.PropertyType != typeof(string))
            {
                Debug.LogWarning("[Build] This editor has no PlayerSettings.Android.appCategory; the bundle check reports the category the manifest carries.");
                return;
            }

            string before = AppCategoryProperty.GetValue(null) as string;
            if (before == "game") return;

            AppCategoryProperty.SetValue(null, "game");
            Debug.LogWarning($"[Build] Application Category is '{before}'; this build uses 'game'. " +
                             "Set it in Player Settings > Android > Other Settings so every build agrees.");
        }

        // UnityEditor.Android.UserBuildSettings.DebugSymbols lives in UnityEditor.Android.Extensions,
        // which exists only on a machine with Android Build Support installed. A direct reference
        // would stop this whole editor assembly compiling on every PC without the module, so it is
        // reached by reflection, like appCategory above.
        // Resolved on use and retried while missing, never cached as null: this class also loads for
        // the Windows build, possibly before Unity has loaded the Android extension assembly.
        static Type _debugSymbolsType;

        static Type DebugSymbolsType => _debugSymbolsType ??= AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("UnityEditor.Android.UserBuildSettings+DebugSymbols", false))
            .FirstOrDefault(type => type != null);

        static PropertyInfo DebugSymbolLevelProperty =>
            DebugSymbolsType?.GetProperty("level", BindingFlags.Public | BindingFlags.Static);

        static PropertyInfo DebugSymbolFormatProperty =>
            DebugSymbolsType?.GetProperty("format", BindingFlags.Public | BindingFlags.Static);

        /// <summary>
        /// Puts the native symbol table into the bundle, where Play picks it up on upload
        /// (<c>BUNDLE-METADATA/com.android.tools.build.debugsymbols/</c>), and writes a symbols zip beside
        /// it for a manual upload. Without them, Play warns "This App Bundle contains native code, and
        /// you've not uploaded debug symbols", and crash and ANR reports show bare addresses.
        ///
        /// <para>Symbol Table, not Full: it is enough to name every function in a native stack trace,
        /// and Full adds file and line data many times its size. The metadata is never delivered to a
        /// device, so the download is unchanged.</para>
        /// </summary>
        static void EmbedDebugSymbols()
        {
            if (DebugSymbolLevelProperty is not { CanWrite: true } || DebugSymbolFormatProperty is not { CanWrite: true })
            {
                Debug.LogWarning("[Build] This editor has no UserBuildSettings.DebugSymbols; the bundle ships without " +
                                 "native debug symbols, and the check will say so.");
                return;
            }

            // A recommendation, not a requirement: if Unity renames these values, warn and build on.
            try
            {
                DebugSymbolLevelProperty.SetValue(null, Enum.Parse(DebugSymbolLevelProperty.PropertyType, "SymbolTable"));
                DebugSymbolFormatProperty.SetValue(null, Enum.Parse(DebugSymbolFormatProperty.PropertyType, "IncludeInBundle, Zip"));
            }
            catch (ArgumentException ex)
            {
                Debug.LogWarning($"[Build] Could not turn on native debug symbols ({ex.Message}); the bundle ships without them.");
            }
        }

        // ──────────────────────────────────────────────
        //  Report
        // ──────────────────────────────────────────────

        static void WriteCheckReport(string aabPath, AndroidBundleCheck.Report report)
        {
            string text = report.Format();
            try { File.WriteAllText(aabPath + ".check.txt", text); }
            catch (Exception ex) { Debug.LogWarning($"[Build] Could not write the check report: {ex.Message}"); }

            if (report.Passed) Debug.Log("[Build] " + text);
            else Debug.LogError("[Build] " + text);
        }

        static void ShowCheckResult(string aabPath, AndroidBundleCheck.Report report, bool built)
        {
            string head = report.Passed
                ? (report.Warnings.Count == 0
                    ? "Ready to upload to Play internal testing."
                    : $"Ready to upload, with {report.Warnings.Count} warning(s):\n\n- " + string.Join("\n- ", report.Warnings))
                : "Play would reject this bundle:\n\n- " + string.Join("\n- ", report.Errors);

            EditorUtility.DisplayDialog(built ? "Android App Bundle built" : "Android App Bundle check",
                $"{head}\n\nFull report: {Path.GetFileName(aabPath)}.check.txt", "OK");
            EditorUtility.RevealInFinder(aabPath);
        }

        /// <summary>
        /// Every setting the Android build changes, captured before and put back after, so a
        /// build leaves Player Settings exactly as it found them.
        /// </summary>
        sealed class AndroidBuildSettingsSnapshot
        {
            string _version;
            int _versionCode;
            bool _useCustomKeystore;
            string _keystoreName, _keystorePass, _keyaliasName, _keyaliasPass;
            object _appCategory;
            object _debugSymbolLevel, _debugSymbolFormat;
            bool _buildAppBundle, _exportProject;

            public static AndroidBuildSettingsSnapshot Capture() => new()
            {
                _version = PlayerSettings.bundleVersion,
                _versionCode = PlayerSettings.Android.bundleVersionCode,
                _useCustomKeystore = PlayerSettings.Android.useCustomKeystore,
                _keystoreName = PlayerSettings.Android.keystoreName,
                _keystorePass = PlayerSettings.Android.keystorePass,
                _keyaliasName = PlayerSettings.Android.keyaliasName,
                _keyaliasPass = PlayerSettings.Android.keyaliasPass,
                _appCategory = AppCategoryProperty?.GetValue(null),
                _debugSymbolLevel = DebugSymbolLevelProperty?.GetValue(null),
                _debugSymbolFormat = DebugSymbolFormatProperty?.GetValue(null),
                _buildAppBundle = EditorUserBuildSettings.buildAppBundle,
                _exportProject = EditorUserBuildSettings.exportAsGoogleAndroidProject,
            };

            public void Restore()
            {
                PlayerSettings.bundleVersion = _version;
                PlayerSettings.Android.bundleVersionCode = _versionCode;
                PlayerSettings.Android.useCustomKeystore = _useCustomKeystore;
                PlayerSettings.Android.keystoreName = _keystoreName;
                PlayerSettings.Android.keystorePass = _keystorePass;
                PlayerSettings.Android.keyaliasName = _keyaliasName;
                PlayerSettings.Android.keyaliasPass = _keyaliasPass;
                if (_appCategory != null && AppCategoryProperty is { CanWrite: true })
                    AppCategoryProperty.SetValue(null, _appCategory);
                if (_debugSymbolLevel != null && DebugSymbolLevelProperty is { CanWrite: true })
                    DebugSymbolLevelProperty.SetValue(null, _debugSymbolLevel);
                if (_debugSymbolFormat != null && DebugSymbolFormatProperty is { CanWrite: true })
                    DebugSymbolFormatProperty.SetValue(null, _debugSymbolFormat);
                EditorUserBuildSettings.buildAppBundle = _buildAppBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = _exportProject;
            }
        }
    }
}
