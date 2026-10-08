# Branch archive: `claude/review-mobile-apk-build-W3iaf`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

## What this branch is

**Android APK build script and mobile fixes**

Work to get an Android phone build compiling: an Android APK build script with an editor menu and command-line support, re-enabling touch input, wrapping editor-only scripts so they don't break the phone build, removing stale Odin Inspector references, bumping Android target SDK to 35, and adding the Authentication scene to the build list.

- **Status:** Partly landed
- **Areas:** mobile/Android build, build tooling, touch input, compile fixes
- **Already in bleeding-edge:** Partly landed: touch input exists (Assets/_Scripts/Controller/IO/TouchInputStrategy.cs), conditional-compilation gate tooling exists (Tools/Build/check_conditional_compilation.py), and the same Odin/SDK fixes were redone on claude/pull-benchmark-tool-Z9AmX. No BuildAndroid.cs build script in bleeding-edge.
- **Risk if deleted:** low
- **Suggestion (2026-10-08):** can be deleted after archiving — Mobile compile fixes were redone later; the small APK build script is the only unique piece and is preserved in the archive.

## Evidence

- **Last commit:** 2026-02-27 by Garrett Milliron
- **Unmerged commits:** 21
- **Forked from:** `1382fa510` (2026-02-26, Merge branch 'claude/shape-signs-face-player-INiJp' of http://127.0.0.1:38106/)
- **Tip:** `d0447d4a4`
- **Files touched (107):**
  - `.gitignore`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/.cmake/api/v1/query/client-agp/cache-v2`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/.cmake/api/v1/query/client-agp/cmakeFiles-v1`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/.cmake/api/v1/query/client-agp/codemodel-v2`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/.cmake/api/v1/reply/cache-v2-a53911730b7c54ef0bed.json`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/.cmake/api/v1/reply/cmakeFiles-v1-35f905221a425a8663cc.json`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/.cmake/api/v1/reply/codemodel-v2-2ac238f011ccd16656a9.json`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/.cmake/api/v1/reply/directory-.-Debug-d0094a50bb2071803777.json`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/.cmake/api/v1/reply/directory-FramePacing-Debug-7f9c8865fd027a154c90.json`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/.cmake/api/v1/reply/index-2026-02-27T07-32-07-0431.json`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/.cmake/api/v1/reply/target-swappywrapper-Debug-6640978171d301e6a8b8.json`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/CMakeCache.txt`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCCompiler.cmake`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCXXCompiler.cmake`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_C.bin`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_CXX.bin`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeSystem.cmake`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.c`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.o`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.cpp`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.o`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/CMakeFiles/CMakeOutput.log`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/CMakeFiles/TargetDirectories.txt`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/CMakeFiles/cmake.check_cache`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/CMakeFiles/rules.ninja`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/FramePacing/cmake_install.cmake`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/additional_project_files.txt`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/android_gradle_build.json`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/android_gradle_build_mini.json`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/build.ninja`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/build_file_index.txt`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/cmake_install.cmake`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/compile_commands.json`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/compile_commands.json.bin`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/configure_fingerprint.bin`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/metadata_generation_command.txt`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/prefab_config.json`
  - `.utmp/Debug/6y6e5j2l/arm64-v8a/symbol_folder_index.txt`
  - `.utmp/Debug/6y6e5j2l/hash_key.txt`
  - `.utmp/Debug/6y6e5j2l/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfig.cmake`
  - … and 67 more

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

<details><summary>Patch (code/doc/text files, first 80 of 89 lines)</summary>

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

<details><summary>Patch (code/doc/text files, first 80 of 133 lines)</summary>

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

<details><summary>Patch (code/doc/text files, first 80 of 85 lines)</summary>

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

### `30d60555d` — Remove stale Odin Inspector defines from Android scripting symbols

_Claude, 2026-02-26 23:47:33 +0000_

```text
The Android platform had ODIN_INSPECTOR, ODIN_INSPECTOR_3, and
ODIN_INSPECTOR_3_1 scripting define symbols left over from a previous
Odin Inspector installation. These caused CS0246 compile errors in the
Soap plugin's #if ODIN_INSPECTOR blocks when switching to Android,
since Sirenix assemblies are not present in the project.
```

```text
 ProjectSettings/ProjectSettings.asset | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

### `2d0a585fc` — build meata files

_Garrett Milliron, 2026-02-26 18:51:15 -0500_

```text
 Assets/Editor/BuildAndroid.cs.meta                                    |   2 +
 .../Resources/Fonts & Materials/Electronic Highway Sign SDF.asset     | 289 ++------------------------------
 Assets/_Scripts/App/UI/WidescreenLayoutAdapter.cs.meta                |   2 +
 ProjectSettings/ProjectSettings.asset                                 |  10 +-
 4 files changed, 22 insertions(+), 281 deletions(-)
```

### `4bc8b2458` — Remove dead Odin Inspector references from Soap plugin

_Claude, 2026-02-26 23:54:54 +0000_

```text
Odin Inspector is not installed in this project. The #if ODIN_INSPECTOR
guards relied on scripting define symbols that were stale and have been
removed, but Unity can cache old defines and still try to compile these
blocks. Strip the dead code entirely so the Soap plugin compiles
cleanly on all platforms regardless of cached state.
```

```text
 Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableEvents/ScriptableEvent.cs                         | 3 ---
 Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableVariables/VariableReferences/VariableReference.cs | 3 ---
 2 files changed, 6 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableEvents/ScriptableEvent.cs b/Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableEvents/ScriptableEvent.cs
index ac3f8d8f4..0612ec63a 100644
--- a/Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableEvents/ScriptableEvent.cs
+++ b/Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableEvents/ScriptableEvent.cs
@@ -7,9 +7,6 @@ using Object = UnityEngine.Object;
 
 namespace Obvious.Soap
 {
-#if ODIN_INSPECTOR
-    [Sirenix.OdinInspector.DrawWithUnity]
-#endif
     public abstract class ScriptableEvent<T> : ScriptableEventBase, IDrawObjectsInInspector
     {
         [Tooltip("Value used when raising the event in editor.")]
diff --git a/Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableVariables/VariableReferences/VariableReference.cs b/Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableVariables/VariableReferences/VariableReference.cs
index 00fc8cf3e..08ac30ca4 100644
--- a/Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableVariables/VariableReferences/VariableReference.cs
+++ b/Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableVariables/VariableReferences/VariableReference.cs
@@ -1,8 +1,5 @@
 ﻿namespace Obvious.Soap
 {
-#if ODIN_INSPECTOR
-    [Sirenix.OdinInspector.DrawWithUnity]
-#endif
     [System.Serializable]
     public abstract class VariableReference<V, T> where V : ScriptableVariable<T>
     {
```

</details>

### `d12a94b0a` — Wrap editor-only scripts in #if UNITY_EDITOR for Android builds

_Claude, 2026-02-27 00:18:07 +0000_

```text
ActiveGameModesWindow and LeaderboardConfigSOEditor use UnityEditor
types (EditorWindow, Editor, SerializedObject, etc.) but live in a
runtime folder. They compile fine in the editor but fail player builds
since UnityEditor is stripped. Wrapping in #if UNITY_EDITOR excludes
them from player compilation.
```

```text
 Assets/_Scripts/Game/UI/ActiveGameModesWindow.cs     | 6 ++++--
 Assets/_Scripts/Game/UI/LeaderboardConfigSOEditor.cs | 6 ++++--
 2 files changed, 8 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/UI/ActiveGameModesWindow.cs b/Assets/_Scripts/Game/UI/ActiveGameModesWindow.cs
index 89efc40b5..256c09994 100644
--- a/Assets/_Scripts/Game/UI/ActiveGameModesWindow.cs
+++ b/Assets/_Scripts/Game/UI/ActiveGameModesWindow.cs
@@ -1,4 +1,5 @@
-﻿using System.Collections.Generic;
+﻿#if UNITY_EDITOR
+using System.Collections.Generic;
 using System.Linq;
 using UnityEditor;
 using UnityEngine;
@@ -195,4 +196,5 @@ namespace CosmicShore.Game.Analytics
             AssetDatabase.SaveAssets();
         }
     }
-}
\ No newline at end of file
+}
+#endif
\ No newline at end of file
diff --git a/Assets/_Scripts/Game/UI/LeaderboardConfigSOEditor.cs b/Assets/_Scripts/Game/UI/LeaderboardConfigSOEditor.cs
index 8464907b9..8730235b9 100644
--- a/Assets/_Scripts/Game/UI/LeaderboardConfigSOEditor.cs
+++ b/Assets/_Scripts/Game/UI/LeaderboardConfigSOEditor.cs
@@ -1,4 +1,5 @@
-﻿using System.Collections.Generic;
+﻿#if UNITY_EDITOR
+using System.Collections.Generic;
 using System.Linq;
 using UnityEditor;
 using UnityEngine;
@@ -441,4 +442,5 @@ namespace CosmicShore.Game.Analytics
             EditorUtility.DisplayDialog("Validation Results", message, "OK");
         }
     }
-}
\ No newline at end of file
+}
+#endif
\ No newline at end of file
```

</details>

### `e62836a96` — Fix using UnityEditor outside #if UNITY_EDITOR guards

_Claude, 2026-02-27 00:30:38 +0000_

```text
RewindDrawer.cs and TMP_TextInfoDebugTool.cs both had 'using
UnityEditor;' directives outside their #if UNITY_EDITOR preprocessor
blocks. This causes CS0246 errors on Android player builds where
UnityEditor.dll is not referenced.
```

```text
 Assets/Unity Assests/TextMesh Pro/Examples & Extras/Scripts/TMP_TextInfoDebugTool.cs | 2 ++
 Assets/_Scripts/App/Systems/RewindSystem/RewindDrawer.cs                             | 2 +-
 2 files changed, 3 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/App/Systems/RewindSystem/RewindDrawer.cs b/Assets/_Scripts/App/Systems/RewindSystem/RewindDrawer.cs
index 8af3f818c..e77840b45 100644
--- a/Assets/_Scripts/App/Systems/RewindSystem/RewindDrawer.cs
+++ b/Assets/_Scripts/App/Systems/RewindSystem/RewindDrawer.cs
@@ -1,7 +1,7 @@
+#if UNITY_EDITOR
 using UnityEditor;
 using UnityEngine;
 
-#if UNITY_EDITOR
 namespace CosmicShore.App.Systems.RewindSystem
 {
     [CustomPropertyDrawer(typeof(OptionalParticleSettings))]
```

</details>

### `5d34c8a23` — Move using CosmicShore.Utility outside #if UNITY_EDITOR in ResourceDisplay

_Claude, 2026-02-27 00:44:00 +0000_

```text
CSDebug is used in runtime code but the using directive for its
namespace was inside the #if UNITY_EDITOR block, causing CS0103
errors on Android player builds.
```

```text
 Assets/_Scripts/Game/UI/ResourceDisplay.cs | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/UI/ResourceDisplay.cs b/Assets/_Scripts/Game/UI/ResourceDisplay.cs
index 63321fdb6..b2ebadb0b 100644
--- a/Assets/_Scripts/Game/UI/ResourceDisplay.cs
+++ b/Assets/_Scripts/Game/UI/ResourceDisplay.cs
@@ -1,11 +1,11 @@
 using System.Collections;
 using System.Collections.Generic;
+using CosmicShore.Utility;
 using TMPro;
 using UnityEngine;
 using UnityEngine.UI;
 #if UNITY_EDITOR
 using UnityEditor;
-using CosmicShore.Utility;
 #endif
 
 namespace CosmicShore.Game.UI
```

</details>

### `16464689a` — Bump Android targetSdkVersion from 33 to 35

_Claude, 2026-02-27 07:36:52 +0000_

```text
androidx.credentials:credentials:1.2.0-rc01 and
credentials-play-services-auth:1.2.0-rc01 require compileSdk 34+.
The Gradle build was failing at checkDebugAarMetadata because the
launcher was compiled against android-33.

Bumping to 35 satisfies the dependency and aligns with current
Google Play requirements (API 34+ required for new apps/updates).
```

```text
 ProjectSettings/ProjectSettings.asset | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

### `e58fcd917` — pushing all adds

_Garrett Milliron, 2026-02-27 02:43:14 -0500_

```text
 .utmp/Debug/6y6e5j2l/arm64-v8a/CMakeFiles/TargetDirectories.txt       |    5 +
 .utmp/Debug/6y6e5j2l/arm64-v8a/CMakeFiles/cmake.check_cache           |    1 +
 .utmp/Debug/6y6e5j2l/arm64-v8a/CMakeFiles/rules.ninja                 |   64 ++
 .utmp/Debug/6y6e5j2l/arm64-v8a/FramePacing/cmake_install.cmake        |   44 +
 .utmp/Debug/6y6e5j2l/arm64-v8a/additional_project_files.txt           |    1 +
 .utmp/Debug/6y6e5j2l/arm64-v8a/android_gradle_build.json              |   39 +
 .utmp/Debug/6y6e5j2l/arm64-v8a/android_gradle_build_mini.json         |   28 +
 .utmp/Debug/6y6e5j2l/arm64-v8a/build.ninja                            |  190 +++++
 .utmp/Debug/6y6e5j2l/arm64-v8a/build_file_index.txt                   |    2 +
 .utmp/Debug/6y6e5j2l/arm64-v8a/cmake_install.cmake                    |   60 ++
 .utmp/Debug/6y6e5j2l/arm64-v8a/compile_commands.json                  |    7 +
 .utmp/Debug/6y6e5j2l/arm64-v8a/compile_commands.json.bin              |  Bin 0 -> 1472 bytes
 .utmp/Debug/6y6e5j2l/arm64-v8a/configure_fingerprint.bin              |   28 +
 .utmp/Debug/6y6e5j2l/arm64-v8a/metadata_generation_command.txt        |   21 +
 .utmp/Debug/6y6e5j2l/arm64-v8a/prefab_config.json                     |    7 +
 .utmp/Debug/6y6e5j2l/arm64-v8a/symbol_folder_index.txt                |    1 +
 .utmp/Debug/6y6e5j2l/hash_key.txt                                     |   28 +
 .../cmake/games-frame-pacing/games-frame-pacingConfig.cmake           |   18 +
 .../cmake/games-frame-pacing/games-frame-pacingConfigVersion.cmake    |    9 +
 .utmp/tools/debug/arm64-v8a/compile_commands.json                     |    7 +
 .../res/values/crashlytics_build_id.xml                               |    2 +-
 .../res/values/crashlytics_unity_version.xml                          |    2 +-
 Assets/Resources/PerformanceTestRunInfo.json                          |    1 +
 Assets/Resources/PerformanceTestRunInfo.json.meta                     |    7 +
 Assets/Resources/PerformanceTestRunSettings.json                      |    1 +
 Assets/Resources/PerformanceTestRunSettings.json.meta                 |    7 +
 Assets/_Graphics/URP_Asset.asset                                      |    2 +
 Assets/_Graphics/UniversalRenderPipelineGlobalSettings.asset          |   12 +-
 ProjectSettings/ProjectSettings.asset                                 |    2 +-
 50 files changed, 5653 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 3311 lines)</summary>

```diff
diff --git a/.utmp/Debug/6y6e5j2l/arm64-v8a/.cmake/api/v1/reply/cache-v2-a53911730b7c54ef0bed.json b/.utmp/Debug/6y6e5j2l/arm64-v8a/.cmake/api/v1/reply/cache-v2-a53911730b7c54ef0bed.json
new file mode 100644
index 000000000..3b6df0656
--- /dev/null
+++ b/.utmp/Debug/6y6e5j2l/arm64-v8a/.cmake/api/v1/reply/cache-v2-a53911730b7c54ef0bed.json
@@ -0,0 +1,1415 @@
+{
+	"entries" : 
+	[
+		{
+			"name" : "ANDROID_ABI",
+			"properties" : 
+			[
+				{
+					"name" : "HELPSTRING",
+					"value" : "No help, variable specified on the command line."
+				}
+			],
+			"type" : "UNINITIALIZED",
+			"value" : "arm64-v8a"
+		},
+		{
+			"name" : "ANDROID_NDK",
+			"properties" : 
+			[
+				{
+					"name" : "HELPSTRING",
+					"value" : "No help, variable specified on the command line."
+				}
+			],
+			"type" : "UNINITIALIZED",
+			"value" : "D:\\Unity\\6000.0.62f1\\Editor\\Data\\PlaybackEngines\\AndroidPlayer\\NDK"
+		},
+		{
+			"name" : "ANDROID_PLATFORM",
+			"properties" : 
+			[
+				{
+					"name" : "HELPSTRING",
+					"value" : "No help, variable specified on the command line."
+				}
+			],
+			"type" : "UNINITIALIZED",
+			"value" : "android-28"
+		},
+		{
+			"name" : "ANDROID_STL",
+			"properties" : 
+			[
+				{
+					"name" : "HELPSTRING",
+					"value" : "No help, variable specified on the command line."
+				}
+			],
+			"type" : "UNINITIALIZED",
+			"value" : "c++_shared"
+		},
+		{
+			"name" : "ANDROID_SUPPORT_FLEXIBLE_PAGE_SIZES",
+			"properties" : 
+			[
+				{
+					"name" : "HELPSTRING",
+					"value" : "No help, variable specified on the command line."
+				}
+			],
+			"type" : "UNINITIALIZED",
+			"value" : "ON"
+		},
+		{
+			"name" : "CMAKE_ADDR2LINE",
+			"properties" : 
+			[
+				{
+					"name" : "ADVANCED",
+					"value" : "1"
+				},
+				{
+					"name" : "HELPSTRING",
+					"value" : "Path to a program."
```

</details>

### `40802936e` — Fix duplicate serialized fields and gitignore .utmp build artifacts

_Claude, 2026-02-27 07:48:36 +0000_

```text
HexRaceHUDView re-declared playerScoreContainer, playerScoreCardPrefab,
and domainColors that already exist in its parent MiniGameHUDView,
causing Unity serialization warnings during build. Removed all duplicate
declarations since the parent already provides all the functionality.

Also replaced the specific .utmp path in .gitignore with a blanket
/.utmp/ rule and removed the tracked CMake/Gradle build artifacts.
```

```text
 .../3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_CXX.bin           |  Bin 8328 -> 0 bytes
 .../arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeSystem.cmake      |  113 ---
 .../CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.c   |  803 ------------------
 .../CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.o   |  Bin 2616 -> 0 bytes
 .../3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.cpp        |  791 -----------------
 .../3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.o          |  Bin 2568 -> 0 bytes
 .utmp/RelWithDebInfo/5m63396o/arm64-v8a/CMakeFiles/CMakeOutput.log    |  324 -------
 .../5m63396o/arm64-v8a/CMakeFiles/TargetDirectories.txt               |    5 -
 .utmp/RelWithDebInfo/5m63396o/arm64-v8a/CMakeFiles/cmake.check_cache  |    1 -
 .utmp/RelWithDebInfo/5m63396o/arm64-v8a/CMakeFiles/rules.ninja        |   64 --
 .../RelWithDebInfo/5m63396o/arm64-v8a/FramePacing/cmake_install.cmake |   44 -
 .utmp/RelWithDebInfo/5m63396o/arm64-v8a/additional_project_files.txt  |    1 -
 .utmp/RelWithDebInfo/5m63396o/arm64-v8a/android_gradle_build.json     |   39 -
 .../RelWithDebInfo/5m63396o/arm64-v8a/android_gradle_build_mini.json  |   28 -
 .utmp/RelWithDebInfo/5m63396o/arm64-v8a/build.ninja                   |  190 -----
 .utmp/RelWithDebInfo/5m63396o/arm64-v8a/build_file_index.txt          |    2 -
 .utmp/RelWithDebInfo/5m63396o/arm64-v8a/cmake_install.cmake           |   60 --
 .utmp/RelWithDebInfo/5m63396o/arm64-v8a/compile_commands.json         |    7 -
 .utmp/RelWithDebInfo/5m63396o/arm64-v8a/compile_commands.json.bin     |  Bin 1485 -> 0 bytes
 .utmp/RelWithDebInfo/5m63396o/arm64-v8a/configure_fingerprint.bin     |   29 -
 .../RelWithDebInfo/5m63396o/arm64-v8a/metadata_generation_command.txt |   20 -
 .utmp/RelWithDebInfo/5m63396o/arm64-v8a/prefab_config.json            |    7 -
 .utmp/RelWithDebInfo/5m63396o/arm64-v8a/symbol_folder_index.txt       |    1 -
 .utmp/RelWithDebInfo/5m63396o/hash_key.txt                            |   27 -
 .../cmake/games-frame-pacing/games-frame-pacingConfig.cmake           |   18 -
 .../cmake/games-frame-pacing/games-frame-pacingConfigVersion.cmake    |    9 -
 .utmp/tools/debug/arm64-v8a/compile_commands.json                     |    7 -
 .utmp/tools/release/arm64-v8a/compile_commands.json                   |    7 -
 Assets/_Scripts/Game/UI/HexRaceHUDView.cs                             |   38 +-
 84 files changed, 2 insertions(+), 11368 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 6562 lines)</summary>

```diff
diff --git a/.utmp/Debug/6y6e5j2l/arm64-v8a/.cmake/api/v1/reply/cache-v2-a53911730b7c54ef0bed.json b/.utmp/Debug/6y6e5j2l/arm64-v8a/.cmake/api/v1/reply/cache-v2-a53911730b7c54ef0bed.json
deleted file mode 100644
index 3b6df0656..000000000
--- a/.utmp/Debug/6y6e5j2l/arm64-v8a/.cmake/api/v1/reply/cache-v2-a53911730b7c54ef0bed.json
+++ /dev/null
@@ -1,1415 +0,0 @@
-{
-	"entries" : 
-	[
-		{
-			"name" : "ANDROID_ABI",
-			"properties" : 
-			[
-				{
-					"name" : "HELPSTRING",
-					"value" : "No help, variable specified on the command line."
-				}
-			],
-			"type" : "UNINITIALIZED",
-			"value" : "arm64-v8a"
-		},
-		{
-			"name" : "ANDROID_NDK",
-			"properties" : 
-			[
-				{
-					"name" : "HELPSTRING",
-					"value" : "No help, variable specified on the command line."
-				}
-			],
-			"type" : "UNINITIALIZED",
-			"value" : "D:\\Unity\\6000.0.62f1\\Editor\\Data\\PlaybackEngines\\AndroidPlayer\\NDK"
-		},
-		{
-			"name" : "ANDROID_PLATFORM",
-			"properties" : 
-			[
-				{
-					"name" : "HELPSTRING",
-					"value" : "No help, variable specified on the command line."
-				}
-			],
-			"type" : "UNINITIALIZED",
-			"value" : "android-28"
-		},
-		{
-			"name" : "ANDROID_STL",
-			"properties" : 
-			[
-				{
-					"name" : "HELPSTRING",
-					"value" : "No help, variable specified on the command line."
-				}
-			],
-			"type" : "UNINITIALIZED",
-			"value" : "c++_shared"
-		},
-		{
-			"name" : "ANDROID_SUPPORT_FLEXIBLE_PAGE_SIZES",
-			"properties" : 
-			[
-				{
-					"name" : "HELPSTRING",
-					"value" : "No help, variable specified on the command line."
-				}
-			],
-			"type" : "UNINITIALIZED",
-			"value" : "ON"
-		},
-		{
-			"name" : "CMAKE_ADDR2LINE",
-			"properties" : 
-			[
-				{
-					"name" : "ADVANCED",
-					"value" : "1"
-				},
-				{
-					"name" : "HELPSTRING",
-					"value" : "Path to a program."
```

</details>

### `d0447d4a4` — update after pull

_Garrett Milliron, 2026-02-27 04:01:55 -0500_

```text
 Assets/Plugins/Android/FirebaseCrashlytics.androidlib/res/values/crashlytics_build_id.xml | 2 +-
 Assets/Resources/PerformanceTestRunInfo.json                                              | 1 -
 Assets/Resources/PerformanceTestRunInfo.json.meta                                         | 7 -------
 Assets/Resources/PerformanceTestRunSettings.json                                          | 1 -
 Assets/Resources/PerformanceTestRunSettings.json.meta                                     | 7 -------
 ProjectSettings/ProjectSettings.asset                                                     | 2 --
 6 files changed, 1 insertion(+), 19 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/Resources/PerformanceTestRunInfo.json b/Assets/Resources/PerformanceTestRunInfo.json
deleted file mode 100644
index 574fc4afd..000000000
--- a/Assets/Resources/PerformanceTestRunInfo.json
+++ /dev/null
@@ -1 +0,0 @@
-{"TestSuite":"","Date":0,"Player":{"Development":false,"ScreenWidth":0,"ScreenHeight":0,"ScreenRefreshRate":0,"Fullscreen":false,"Vsync":0,"AntiAliasing":0,"Batchmode":false,"RenderThreadingMode":"MultiThreaded","MtRendering":false,"GraphicsJobs":false,"GpuSkinning":true,"Platform":"","ColorSpace":"","AnisotropicFiltering":"","BlendWeights":"","GraphicsApi":"","ScriptingBackend":"IL2CPP","AndroidTargetSdkVersion":"AndroidApiLevel33","AndroidBuildSystem":"Gradle","BuildTarget":"Android","StereoRenderingPath":"MultiPass"},"Hardware":{"OperatingSystem":"","DeviceModel":"","DeviceName":"","ProcessorType":"","ProcessorCount":0,"GraphicsDeviceName":"","SystemMemorySizeMB":0},"Editor":{"Version":"6000.0.62f1","Branch":"6000.0/staging","Changeset":"f99f05b3e950","Date":1761781782},"Dependencies":["com.cysharp.unitask@2.5.10","com.unity.2d.sprite@1.0.0","com.unity.adaptiveperformance@5.1.6","com.unity.adaptiveperformance.samsung.android@5.1.0","com.unity.ads@4.12.0","com.unity.ai.navigation@2.0.9","com.unity.animation.rigging@1.3.0","com.unity.cinemachine@3.1.2","com.unity.collab-proxy@2.10.0","com.unity.device-simulator.devices@1.0.0","com.unity.entities@1.4.2","com.unity.entities.graphics@1.4.15","com.unity.feature.mobile@1.0.0","com.unity.ide.rider@3.0.38","com.unity.ide.visualstudio@2.0.25","com.unity.inputsystem@1.14.2","com.unity.mobile.android-logcat@1.4.6","com.unity.mobile.notifications@2.4.2","com.unity.multiplayer.center@1.0.0","com.unity.multiplayer.center.quickstart@1.0.1","com.unity.multiplayer.playmode@1.6.1","com.unity.multiplayer.tools@2.2.6","com.unity.multiplayer.widgets@1.0.1","com.unity.netcode.gameobjects@2.5.0","com.unity.nuget.newtonsoft-json@3.2.1","com.unity.performance.profile-analyzer@1.2.3","com.unity.purchasing@4.12.2","com.unity.recorder@5.1.2","com.unity.render-pipelines.universal@17.0.4","com.unity.services.analytics@6.2.1","com.unity.services.cloudsave@3.4.0","com.unity.services.core@1.16.0","com.unity.services.leaderboards@2.3.3","com.unity.services.multiplayer@1.1.8","com.unity.test-framework@1.6.0","com.unity.timeline@1.8.9","com.unity.toolchain.win-x86_64-linux-x86_64@2.0.10","com.unity.transport@2.6.0","com.unity.ugui@2.0.0","com.unity.visualeffectgraph@17.0.4","com.veriorpies.parrelsync@1.5.2","jp.hadashikick.vcontainer@1.6.3","com.unity.modules.accessibility@1.0.0","com.unity.modules.ai@1.0.0","com.unity.modules.androidjni@1.0.0","com.unity.modules.animation@1.0.0","com.unity.modules.assetbundle@1.0.0","com.unity.modules.audio@1.0.0","com.unity.modules.cloth@1.0.0","com.unity.modules.director@1.0.0","com.unity.modules.imageconversion@1.0.0","com.unity.modules.imgui@1.0.0","com.unity.modules.jsonserialize@1.0.0","com.unity.modules.particlesystem@1.0.0","com.unity.modules.physics@1.0.0","com.unity.modules.physics2d@1.0.0","com.unity.modules.screencapture@1.0.0","com.unity.modules.terrain@1.0.0","com.unity.modules.terrainphysics@1.0.0","com.unity.modules.tilemap@1.0.0","com.unity.modules.ui@1.0.0","com.unity.modules.uielements@1.0.0","com.unity.modules.umbra@1.0.0","com.unity.modules.unityanalytics@1.0.0","com.unity.modules.unitywebrequest@1.0.0","com.unity.modules.unitywebrequestassetbundle@1.0.0","com.unity.modules.unitywebrequestaudio@1.0.0","com.unity.modules.unitywebrequesttexture@1.0.0","com.unity.modules.unitywebrequestwww@1.0.0","com.unity.modules.vehicles@1.0.0","com.unity.modules.video@1.0.0","com.unity.modules.vr@1.0.0","com.unity.modules.wind@1.0.0","com.unity.modules.xr@1.0.0","com.unity.modules.subsystems@1.0.0","com.unity.modules.hierarchycore@1.0.0","com.unity.shadergraph@17.0.4","com.unity.render-pipelines.core@17.0.4","com.unity.burst@1.8.25","com.unity.collections@2.6.2","com.unity.mathematics@1.3.2","com.unity.sysroot@2.0.10","com.unity.sysroot.linux-x86_64@2.0.9","com.unity.ext.nunit@2.0.5","com.unity.services.qos@1.3.0","com.unity.services.wire@1.4.0","com.unity.services.deployment@1.3.0","com.unity.services.authentication@3.4.1","com.unity.render-pipelines.universal-config@17.0.3","com.unity.bindings.openimageio@1.0.0","com.unity.nuget.mono-cecil@1.11.5","com.unity.profiling.core@1.0.2","com.unity.serialization@3.1.2","com.unity.scriptablebuildpipeline@2.4.3","com.unity.test-framework.performance@3.2.0","com.unity.splines@2.8.2","com.unity.rendering.light-transport@1.0.1","com.unity.searcher@4.9.3","com.unity.services.deployment.api@1.0.0","com.unity.settings-manager@2.1.0"],"Results":[]}
\ No newline at end of file
diff --git a/Assets/Resources/PerformanceTestRunSettings.json b/Assets/Resources/PerformanceTestRunSettings.json
deleted file mode 100644
index 49438ae14..000000000
--- a/Assets/Resources/PerformanceTestRunSettings.json
+++ /dev/null
@@ -1 +0,0 @@
-{"MeasurementCount":-1}
\ No newline at end of file
```

</details>

_Also contains 6 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
