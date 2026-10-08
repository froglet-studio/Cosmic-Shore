# Branch archive: `claude/build-mobile-apk-RcGXY`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-02-26 by Claude
- **Unmerged commits:** 8
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/89
- **Forked from:** `ff81fafd8` (2026-02-25, Merge pull request #139 from froglet-studio/claude/add-missing-sounds-1ZgoJ)
- **Tip:** `6bdd9a4ac`
- **Files touched (4):**
  - `Assets/Editor/BuildAndroid.cs`
  - `Assets/_Scripts/Game/IO/InputController.cs`
  - `Assets/_Scripts/Game/IO/TouchInputStrategy.cs`
  - `ProjectSettings/EditorBuildSettings.asset`

### `32e112f55` — Re-enable touch input for mobile (Android/iOS) builds

_Claude, 2026-02-21 10:11:13 +0000_

```text
- Restore TouchInputStrategy in InputController: create, initialize, and
  select it when SystemInfo.deviceType == DeviceType.Handheld
- Add InvertY and InvertThrottle support to TouchInputStrategy.Reparameterize()
  to match parity with GamepadInputStrategy and KeyboardInputStrategy
- Strategy priority: Gamepad > Touch (handheld) > Keyboard (desktop)

The touch input was previously commented out. All three target games
(HexRace, Joust, CrystalCapture) are already in the build scene list
and the Android build configuration (IL2CPP, ARM64+ARMv7, SDK 28-33)
is ready. The project can now be built as an APK from Unity.
```

```text
 Assets/_Scripts/Game/IO/InputController.cs    | 21 ++++++++++-----------
 Assets/_Scripts/Game/IO/TouchInputStrategy.cs | 11 +++++++++++
 2 files changed, 21 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/IO/InputController.cs b/Assets/_Scripts/Game/IO/InputController.cs
index 7506f313b..2e3abc6e1 100644
--- a/Assets/_Scripts/Game/IO/InputController.cs
+++ b/Assets/_Scripts/Game/IO/InputController.cs
@@ -22,6 +22,7 @@ namespace CosmicShore.Game.IO
         private IInputStrategy currentStrategy;
         private GamepadInputStrategy gamepadStrategy;
         private KeyboardInputStrategy keyboardStrategy;
+        private TouchInputStrategy touchStrategy;
         private DeviceOrientationHandler orientationHandler;
 
         private bool isInitialized;
@@ -125,8 +126,8 @@ namespace CosmicShore.Game.IO
         {
             if (Gamepad.current != null)
                 currentStrategy = gamepadStrategy;
-            //else if (SystemInfo.deviceType == DeviceType.Handheld)
-            //    currentStrategy = touchStrategy;
+            else if (SystemInfo.deviceType == DeviceType.Handheld)
+                currentStrategy = touchStrategy;
             else
                 currentStrategy = keyboardStrategy;
 
@@ -135,16 +136,14 @@ namespace CosmicShore.Game.IO
 
         private void InitializeStrategies()
         {
-            //touchStrategy = new TouchInputStrategy();
-            //keyboardMouseStrategy = new KeyboardMouseInputStrategy();
             gamepadStrategy = new GamepadInputStrategy();
             keyboardStrategy = new KeyboardInputStrategy();
+            touchStrategy = new TouchInputStrategy();
             orientationHandler = new DeviceOrientationHandler();
 
-            //touchStrategy.Initialize(vessel);
-            //keyboardMouseStrategy.Initialize(vessel);
             gamepadStrategy.Initialize(InputStatus);
             keyboardStrategy.Initialize(InputStatus);
+            touchStrategy.Initialize(InputStatus);
             orientationHandler.Initialize(InputStatus, this);
         }
 
@@ -164,11 +163,11 @@ namespace CosmicShore.Game.IO
         {
             IInputStrategy newStrategy = null;
 
-            if (Gamepad.current != null && newStrategy != gamepadStrategy)
+            if (Gamepad.current != null)
                 newStrategy = gamepadStrategy;
-            //else if (Mouse.current.rightButton.isPressed)
-            //    newStrategy = keyboardMouseStrategy;
-            else 
+            else if (SystemInfo.deviceType == DeviceType.Handheld)
+                newStrategy = touchStrategy;
+            else
                 newStrategy = keyboardStrategy;
 
             if (newStrategy != null && newStrategy != currentStrategy)
@@ -176,7 +175,7 @@ namespace CosmicShore.Game.IO
                 currentStrategy?.OnStrategyDeactivated();
                 currentStrategy = newStrategy;
                 currentStrategy.OnStrategyActivated();
-                
+
                 // Re-sync settings when switching strategies
                 SyncInvertSettings();
             }
diff --git a/Assets/_Scripts/Game/IO/TouchInputStrategy.cs b/Assets/_Scripts/Game/IO/TouchInputStrategy.cs
index bb925bd71..aac944bc4 100644
--- a/Assets/_Scripts/Game/IO/TouchInputStrategy.cs
+++ b/Assets/_Scripts/Game/IO/TouchInputStrategy.cs
@@ -221,6 +221,17 @@ namespace CosmicShore.Game.IO
             inputStatus.YSum = -Ease(rightNormalizedJoystickPosition.y + leftNormalizedJoystickPosition.y);
             inputStatus.XDiff = (rightNormalizedJoystickPosition.x - leftNormalizedJoystickPosition.x + 2) / 4;
             inputStatus.YDiff = Ease(rightNormalizedJoystickPosition.y - leftNormalizedJoystickPosition.y);
+
+            if (inputStatus.InvertYEnabled)
+            {
+                inputStatus.YSum *= -1f;
+                inputStatus.YDiff *= -1f;
+            }
+
+            if (inputStatus.InvertThrottleEnabled)
+            {
+                inputStatus.XDiff = 1f - inputStatus.XDiff;
+            }
         }
 
         private void PerformSpeedAndDirectionalEffects()
```

</details>

### `6de572cde` — Add Authentication scene to build settings for mobile APK

_Claude, 2026-02-22 13:11:13 +0000_

```text
The SplashToAuthFlow loads the Authentication scene by name at runtime.
Without it in EditorBuildSettings, the build would crash on scene load.
```

```text
 ProjectSettings/EditorBuildSettings.asset | 3 +++
 1 file changed, 3 insertions(+)
```

### `3643cdce4` — Add Android APK build script with Editor menu and CLI support

_Claude, 2026-02-26 19:30:32 +0000_

```text
Adds FrogletTools > Build > Android APK menu items (release and development)
and a CLI entry point for CI: -executeMethod CosmicShore.Editor.BuildAndroid.Build

Outputs to Builds/Android/CosmicShore.apk (already gitignored).
```

```text
 Assets/Editor/BuildAndroid.cs | 127 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 127 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/Editor/BuildAndroid.cs b/Assets/Editor/BuildAndroid.cs
new file mode 100644
index 000000000..55955f483
--- /dev/null
+++ b/Assets/Editor/BuildAndroid.cs
@@ -0,0 +1,127 @@
+using System;
+using System.IO;
+using System.Linq;
+using UnityEditor;
+using UnityEditor.Build.Reporting;
+using UnityEngine;
+
+namespace CosmicShore.Editor
+{
+    /// <summary>
+    /// Android APK builder — usable from the Editor menu or the command line.
+    ///
+    /// Editor:  FrogletTools ▸ Build ▸ Android APK
+    /// CLI:     Unity -batchmode -nographics -projectPath . -executeMethod CosmicShore.Editor.BuildAndroid.Build -quit
+    ///
+    /// Optional CLI args:
+    ///   -outputPath path/to/output.apk
+    ///   -development        (adds Development Build flag)
+    /// </summary>
+    public static class BuildAndroid
+    {
+        private const string DefaultOutputDir = "Builds/Android";
+        private const string DefaultApkName = "CosmicShore.apk";
+
+        [MenuItem("FrogletTools/Build/Android APK")]
+        public static void BuildFromMenu()
+        {
+            var outputPath = Path.Combine(DefaultOutputDir, DefaultApkName);
+            RunBuild(outputPath, development: false);
+        }
+
+        [MenuItem("FrogletTools/Build/Android APK (Development)")]
+        public static void BuildFromMenuDev()
+        {
+            var outputPath = Path.Combine(DefaultOutputDir, DefaultApkName);
+            RunBuild(outputPath, development: true);
+        }
+
+        /// <summary>
+        /// Entry point for command-line builds (e.g. CI).
+        /// </summary>
+        public static void Build()
+        {
+            var args = Environment.GetCommandLineArgs();
+            var outputPath = GetArgValue(args, "-outputPath")
+                             ?? Path.Combine(DefaultOutputDir, DefaultApkName);
+            bool development = args.Contains("-development");
+
+            RunBuild(outputPath, development);
+        }
+
+        private static void RunBuild(string outputPath, bool development)
+        {
+            // Ensure output directory exists
+            var dir = Path.GetDirectoryName(outputPath);
+            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
+                Directory.CreateDirectory(dir);
+
+            // Collect enabled scenes from Build Settings
+            var scenes = EditorBuildSettings.scenes
+                .Where(s => s.enabled)
+                .Select(s => s.path)
+                .ToArray();
+
+            if (scenes.Length == 0)
+            {
+                Debug.LogError("[BuildAndroid] No enabled scenes in Build Settings.");
+                EditorApplication.Exit(1);
+                return;
+            }
+
+            // Force Android as the active build target
+            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
+            {
+                Debug.Log("[BuildAndroid] Switching active build target to Android...");
+                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
+            }
+
+            // Build as APK (not AAB)
+            EditorUserBuildSettings.buildAppBundle = false;
+
+            var options = BuildOptions.None;
+            if (development)
+                options |= BuildOptions.Development | BuildOptions.AllowDebugging;
+
+            Debug.Log($"[BuildAndroid] Building APK → {outputPath}");
+            Debug.Log($"[BuildAndroid] Scenes ({scenes.Length}): {string.Join(", ", scenes)}");
+            Debug.Log($"[BuildAndroid] Development: {development}");
+
+            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
+            {
+                scenes = scenes,
+                locationPathName = outputPath,
+                target = BuildTarget.Android,
+                options = options,
+            });
+
+            var summary = report.summary;
+
+            Debug.Log($"[BuildAndroid] Result: {summary.result}");
+            Debug.Log($"[BuildAndroid] Duration: {summary.totalTime}");
+            Debug.Log($"[BuildAndroid] Size: {summary.totalSize / (1024 * 1024)} MB");
+            Debug.Log($"[BuildAndroid] Warnings: {summary.totalWarnings}  Errors: {summary.totalErrors}");
+
+            if (summary.result != BuildResult.Succeeded)
+            {
+                Debug.LogError($"[BuildAndroid] Build failed with {summary.totalErrors} error(s).");
+                // Exit with error code for CI
+                EditorApplication.Exit(1);
+            }
+            else
+            {
+                Debug.Log($"[BuildAndroid] APK written to: {Path.GetFullPath(outputPath)}");
+            }
+        }
+
+        private static string GetArgValue(string[] args, string key)
+        {
+            for (int i = 0; i < args.Length - 1; i++)
+            {
+                if (args[i] == key)
+                    return args[i + 1];
+            }
+            return null;
+        }
+    }
+}
```

</details>

### `769344aca` — Default Android build to development, clear production keystore

_Claude, 2026-02-26 19:32:11 +0000_

```text
Development builds use Unity's debug keystore — no custom keystore
needed. The script explicitly clears any stale production keystore
path from ProjectSettings so the build won't fail looking for a
missing .keystore file. Pass -release (CLI) or use the Release menu
item when a production keystore is configured.
```

```text
 Assets/Editor/BuildAndroid.cs | 32 ++++++++++++++++++++++----------
 1 file changed, 22 insertions(+), 10 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/Editor/BuildAndroid.cs b/Assets/Editor/BuildAndroid.cs
index 55955f483..cd3de1918 100644
--- a/Assets/Editor/BuildAndroid.cs
+++ b/Assets/Editor/BuildAndroid.cs
@@ -10,43 +10,44 @@ namespace CosmicShore.Editor
     /// <summary>
     /// Android APK builder — usable from the Editor menu or the command line.
     ///
-    /// Editor:  FrogletTools ▸ Build ▸ Android APK
+    /// Editor:  FrogletTools ▸ Build ▸ Android APK (Development)
     /// CLI:     Unity -batchmode -nographics -projectPath . -executeMethod CosmicShore.Editor.BuildAndroid.Build -quit
     ///
     /// Optional CLI args:
     ///   -outputPath path/to/output.apk
-    ///   -development        (adds Development Build flag)
+    ///   -release        (strips Development flag, requires proper keystore)
     /// </summary>
     public static class BuildAndroid
     {
         private const string DefaultOutputDir = "Builds/Android";
         private const string DefaultApkName = "CosmicShore.apk";
 
-        [MenuItem("FrogletTools/Build/Android APK")]
+        [MenuItem("FrogletTools/Build/Android APK (Development)")]
         public static void BuildFromMenu()
         {
             var outputPath = Path.Combine(DefaultOutputDir, DefaultApkName);
-            RunBuild(outputPath, development: false);
+            RunBuild(outputPath, development: true);
         }
 
-        [MenuItem("FrogletTools/Build/Android APK (Development)")]
-        public static void BuildFromMenuDev()
+        [MenuItem("FrogletTools/Build/Android APK (Release)")]
+        public static void BuildFromMenuRelease()
         {
             var outputPath = Path.Combine(DefaultOutputDir, DefaultApkName);
-            RunBuild(outputPath, development: true);
+            RunBuild(outputPath, development: false);
         }
 
         /// <summary>
         /// Entry point for command-line builds (e.g. CI).
+        /// Defaults to development build; pass -release for production.
         /// </summary>
         public static void Build()
         {
             var args = Environment.GetCommandLineArgs();
             var outputPath = GetArgValue(args, "-outputPath")
                              ?? Path.Combine(DefaultOutputDir, DefaultApkName);
-            bool development = args.Contains("-development");
+            bool release = args.Contains("-release");
 
-            RunBuild(outputPath, development);
+            RunBuild(outputPath, development: !release);
         }
 
         private static void RunBuild(string outputPath, bool development)
@@ -79,6 +80,18 @@ namespace CosmicShore.Editor
             // Build as APK (not AAB)
             EditorUserBuildSettings.buildAppBundle = false;
 
+            // For development builds, use Unity's debug keystore so no custom
+            // keystore is required. Clear any stale production keystore path
+            // that may be baked into ProjectSettings.
+            if (development)
+            {
+                PlayerSettings.Android.useCustomKeystore = false;
+                PlayerSettings.Android.keystoreName = string.Empty;
+                PlayerSettings.Android.keystorePass = string.Empty;
+                PlayerSettings.Android.keyaliasName = string.Empty;
+                PlayerSettings.Android.keyaliasPass = string.Empty;
+            }
+
             var options = BuildOptions.None;
             if (development)
                 options |= BuildOptions.Development | BuildOptions.AllowDebugging;
@@ -105,7 +118,6 @@ namespace CosmicShore.Editor
             if (summary.result != BuildResult.Succeeded)
             {
                 Debug.LogError($"[BuildAndroid] Build failed with {summary.totalErrors} error(s).");
-                // Exit with error code for CI
                 EditorApplication.Exit(1);
             }
             else
```

</details>

### `6bdd9a4ac` — Fix CS0234: fully qualify System.Environment to avoid CosmicShore.Environment conflict

_Claude, 2026-02-26 19:33:57 +0000_

```text
 Assets/Editor/BuildAndroid.cs | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/Editor/BuildAndroid.cs b/Assets/Editor/BuildAndroid.cs
index cd3de1918..fc4f42163 100644
--- a/Assets/Editor/BuildAndroid.cs
+++ b/Assets/Editor/BuildAndroid.cs
@@ -42,7 +42,7 @@ namespace CosmicShore.Editor
         /// </summary>
         public static void Build()
         {
-            var args = Environment.GetCommandLineArgs();
+            var args = System.Environment.GetCommandLineArgs();
             var outputPath = GetArgValue(args, "-outputPath")
                              ?? Path.Combine(DefaultOutputDir, DefaultApkName);
             bool release = args.Contains("-release");
```

</details>

_Also contains 3 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
