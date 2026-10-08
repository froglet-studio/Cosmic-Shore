# Branch archive: `erwan/firebase`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2022-09-14 by Erwan Loisant
- **Unmerged commits:** 1
- **Forked from:** `fd1e1b10f` (2022-09-13, Merge branch 'master' of https://github.com/froglet-studio/Star-Writer)
- **Tip:** `221273ca8`
- **Files touched (258):**
  - `Assets/Editor Default Resources.meta`
  - `Assets/Editor Default Resources/Firebase.meta`
  - `Assets/Editor Default Resources/Firebase/fb_analytics.png`
  - `Assets/Editor Default Resources/Firebase/fb_analytics.png.meta`
  - `Assets/Editor Default Resources/Firebase/fb_analytics_dark.png`
  - `Assets/Editor Default Resources/Firebase/fb_analytics_dark.png.meta`
  - `Assets/Editor Default Resources/Firebase/fb_auth.png`
  - `Assets/Editor Default Resources/Firebase/fb_auth.png.meta`
  - `Assets/Editor Default Resources/Firebase/fb_auth_dark.png`
  - `Assets/Editor Default Resources/Firebase/fb_auth_dark.png.meta`
  - `Assets/Editor Default Resources/Firebase/fb_cloud_messaging.png`
  - `Assets/Editor Default Resources/Firebase/fb_cloud_messaging.png.meta`
  - `Assets/Editor Default Resources/Firebase/fb_cloud_messaging_dark.png`
  - `Assets/Editor Default Resources/Firebase/fb_cloud_messaging_dark.png.meta`
  - `Assets/Editor Default Resources/Firebase/fb_config.png`
  - `Assets/Editor Default Resources/Firebase/fb_config.png.meta`
  - `Assets/Editor Default Resources/Firebase/fb_config_dark.png`
  - `Assets/Editor Default Resources/Firebase/fb_config_dark.png.meta`
  - `Assets/Editor Default Resources/Firebase/fb_crashlytics.png`
  - `Assets/Editor Default Resources/Firebase/fb_crashlytics.png.meta`
  - `Assets/Editor Default Resources/Firebase/fb_crashlytics_dark.png`
  - `Assets/Editor Default Resources/Firebase/fb_crashlytics_dark.png.meta`
  - `Assets/Editor Default Resources/Firebase/fb_database.png`
  - `Assets/Editor Default Resources/Firebase/fb_database.png.meta`
  - `Assets/Editor Default Resources/Firebase/fb_database_dark.png`
  - `Assets/Editor Default Resources/Firebase/fb_database_dark.png.meta`
  - `Assets/Editor Default Resources/Firebase/fb_dynamic_links.png`
  - `Assets/Editor Default Resources/Firebase/fb_dynamic_links.png.meta`
  - `Assets/Editor Default Resources/Firebase/fb_dynamic_links_dark.png`
  - `Assets/Editor Default Resources/Firebase/fb_dynamic_links_dark.png.meta`
  - `Assets/Editor Default Resources/Firebase/fb_functions.png`
  - `Assets/Editor Default Resources/Firebase/fb_functions.png.meta`
  - `Assets/Editor Default Resources/Firebase/fb_functions_dark.png`
  - `Assets/Editor Default Resources/Firebase/fb_functions_dark.png.meta`
  - `Assets/Editor Default Resources/Firebase/fb_invites.png`
  - `Assets/Editor Default Resources/Firebase/fb_invites.png.meta`
  - `Assets/Editor Default Resources/Firebase/fb_invites_dark.png`
  - `Assets/Editor Default Resources/Firebase/fb_invites_dark.png.meta`
  - `Assets/Editor Default Resources/Firebase/fb_storage.png`
  - `Assets/Editor Default Resources/Firebase/fb_storage.png.meta`
  - … and 218 more

### `221273ca8` — Add Firebase

_Erwan Loisant, 2022-09-14 22:29:10 +0200_

```text
 .../Android/com.google.firebase.firebase-components-17.0.0.aar        |  Bin 0 -> 29250 bytes
 .../Android/com.google.firebase.firebase-components-17.0.0.aar.meta   |   34 +
 .../Android/com.google.firebase.firebase-installations-17.0.1.aar     |  Bin 0 -> 42192 bytes
 .../com.google.firebase.firebase-installations-17.0.1.aar.meta        |   34 +
 .../com.google.firebase.firebase-installations-interop-17.0.1.aar     |  Bin 0 -> 6478 bytes
 ...com.google.firebase.firebase-installations-interop-17.0.1.aar.meta |   34 +
 .../com.google.firebase.firebase-measurement-connector-19.0.0.aar     |  Bin 0 -> 10625 bytes
 ...com.google.firebase.firebase-measurement-connector-19.0.0.aar.meta |   34 +
 Assets/Plugins/iOS.meta                                               |    8 +
 Assets/Plugins/iOS/Firebase.meta                                      |    8 +
 Assets/Plugins/iOS/Firebase/libFirebaseCppAnalytics.a                 |  Bin 0 -> 1976000 bytes
 Assets/Plugins/iOS/Firebase/libFirebaseCppAnalytics.a.meta            |   76 ++
 Assets/Plugins/iOS/Firebase/libFirebaseCppApp.a                       |  Bin 0 -> 46311664 bytes
 Assets/Plugins/iOS/Firebase/libFirebaseCppApp.a.meta                  |   76 ++
 Assets/Scenes/_Official Build/MainMenu Design 1.unity                 |   69 ++
 Assets/StreamingAssets.meta                                           |    8 +
 Assets/StreamingAssets/google-services-desktop.json                   |   39 +
 Assets/StreamingAssets/google-services-desktop.json.meta              |    7 +
 Assets/_Prefabs/CORE/Analytics.prefab                                 |   46 ++
 Assets/_Prefabs/CORE/Analytics.prefab.meta                            |    7 +
 Assets/_Scripts/Utility/Singletons/SingletonPersistent.cs             |    2 +-
 Assets/_Scripts/_Core/Game/Managers/AnalyticsManager.cs               |   58 ++
 Assets/_Scripts/_Core/Game/Managers/AnalyticsManager.cs.meta          |   11 +
 Assets/_Scripts/_Core/Game/Managers/GameManager.cs                    |   12 +-
 Assets/google-services.json                                           |   39 +
 Assets/google-services.json.meta                                      |    7 +
 ProjectSettings/AndroidResolverDependencies.xml                       |   73 ++
 ProjectSettings/GvhProjectSettings.xml                                |    8 +
 ProjectSettings/ProjectSettings.asset                                 |    4 +-
 258 files changed, 8619 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 3401 lines)</summary>

```diff
diff --git a/Assets/ExternalDependencyManager/Editor/CHANGELOG.md b/Assets/ExternalDependencyManager/Editor/CHANGELOG.md
new file mode 100755
index 000000000..ee1c7d1a5
--- /dev/null
+++ b/Assets/ExternalDependencyManager/Editor/CHANGELOG.md
@@ -0,0 +1,1362 @@
+# Version 1.2.172 - Jun 23, 2022
+* iOS Resolver - Stop forcing `ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES` to `YES`,
+  which seems to cause problem for some when submitting apps. See #526 for more
+  information.
+
+# Version 1.2.171 - May 11, 2022
+* iOS Resolver - Change `Enable Swift Framework Support Workaround` setting to
+  be `ON` by default since more pods are using Swift Framework now.
+
+# Version 1.2.170 - Apr 4, 2022
+* Android Resolver - Fixes #498 - Fix the path separator of the Maven repo
+  injected to `mainTemplate.gradle`.
+* iOS Resolver - Fixes #470 - Switch default Cocoapods master repo from Github
+  to CDN.
+* iOS Resolver - `Link Framework Statically` setting is now default to `true`.
+  That is, `use_frameworks! :linkage => static` will be added to `Podfile` by
+  default instead of `use_frameworks!`. This can be changed in iOS Resolver
+  settings. This fixes odd behaviors when pods include static libraries, ex.
+  Firebase Analytics.
+* iOS Resolver - Added a workaround when app crashes on launch due to
+  `Library not loaded: @rpath/libswiftCore.dylib` when some pods includes Swift
+  framework. This is turned `OFF` by default and can be changed in iOS Resolver
+  settings.
+
+# Version 1.2.169 - Jan 20, 2022
+* General - Fixes #425 - Change to save `GvhProjectSettings.xml` without
+  Unicode byte order mark (BoM).
+* Android Resolver - Remove reference to `jcenter()`
+* iOS Resolver - Force setting `LANG` when executing Cocoapods in shell mode on
+  Mac.
+
+# Version 1.2.168 - Dec 9, 2021
+* All - Fixes #472 by removing the use of `System.Diagnostics.Debug.Assert`
+* All - Fixed #477 by properly enabling EDM4U libraries for Unity 2021.2+ when
+  the package is installed through `.tgz`
+
+# Version 1.2.167 - Oct 6, 2021
+* All - Moved versioned `.dll` in EDM4U to a versioned folder and remove their
+  version postfix in their filename. For instance, `IOSResolver.dll` will be
+  placed at `ExternalDependencyManager/Editor/1.2.167/Google.IOSResolver.dll`.
+* Android Resolver - Fixed #243 by only using the highest version in
+  `mainTemplate.gradle` when duplicated dependencies are presented.
+* Android Resolver - Added supports to x86_64 to ABI list for Android apps on
+  Chrome OS.
+
+# Version 1.2.166 - Jun 30, 2021
+* All - Fixed #440 and fixed #447 by specifying the parameter type while calling
+  `GetApplicationIdentifier()` Unity API using reflection, due to a new
+  overloaded method introduced in Unity 2021.2.
+* Android Resolver - Fixed #442 by patching `Dependency.IsGreater()` when the
+  version strings end '+'.
+
+# Version 1.2.165 - Apr 28, 2021
+## Bug Fixes
+* Version Handler - Fixed #431 by replacing the use of `HttpUtility.UrlEncode()`
+  which causes NullReferenceException in certain version of Unity.
+* Android Resolver - Check that androidSdkRootPath directory exists before using
+  as sdkPath.
+* Android Resolver - Fixed Android Resolver integration tests with Unity
+  2019.3+.
+
+# Version 1.2.164 - Feb 4, 2021
+## New Features
+* Android Resolver - Added support for Android packages with classifier in their
+  namespaces.
+* iOS Resolver - Added new settings in iOS Resolver to configure generated
+  Podfile.
+* iOS Resolver - Added a new attribute `addToAllTargets` in Dependencies.xml.
+
+## Bug Fixes
+* iOS Resolver - Fixed XML parsing for `bitcodeEnabled` attribute in
+  Dependencies.xml.
+
+# Version 1.2.163 - Dec 15, 2020
+## Bug Fixes
+* Version Handler - Fixed measurement reporting
+
+# Version 1.2.162 - Nov 19, 2020
+## Bug Fixes
+* Version Handler - Improved #413 by preventing Version Handler from running
+  from static constructor when it is disabled.
+* Package Manager Resolver - Remove GPR
+
+# Version 1.2.161 - Oct 12, 2020
+## Bug Fixes
+* Android Resolver - Fixed the issue that Android Resolver does not resolve
+  again before build in Unity 2020 if it failed to resolve previously.
+
+# Version 1.2.160 - Sep 30, 2020
+## Bug Fixes
+* Android Resolver - Fixed a regression that gradleResolver can be null until
+  Initialize() is called.
+* Android Resolver - Fixed a regression that Android Resolver failed in Unity
+  2019.3+ due to `gradleTemplate.properties` not enabled when
+  `mainTemplate.gradle` is not enabled at all.
+
+# Version 1.2.159 - Sep 11, 2020
+## Bug Fixes
+* Android Resolver - Fixed #322 where the Unity editor will lose its target SDK
+  setting between Unity restarts if `>28` is selected in 2019.  This is due to
+  Unity AndroidSdkVersions enum does not contain values above 28.
+* Android Resolver - Fixed #360 where building Android app with Untiy 2019.3+
+  may fail due to Jetifier and AndroidX not enabled properly in generated
+  Gradle project. This fix requires the user to enable
+  `Custom Gradle Properties Template` found under
+  `Player Settings > Settings for Android > Publishing Settings`.
+
+# Version 1.2.158 - Sep 3, 2020
+## Bug Fixes
+* Version Handler: Fixed editor freeze when `-executeMethod` is used in
+  non-batch mode.
+* Android Resolver: Normalized file paths when generating local Maven repo
+  since the path may contains a mix of forward and backward slash on Windows.
+* Export Unity Package: Fixed generation of .unitypackage with tarfile on
+  Windows.
+
+# Version 1.2.157 - Aug 6, 2020
+## Bug Fixes
+* Android Resolver: Delay initialization until active build target is Android
+  and the editor is not in play mode.
+* iOS Resolver: Delay initialization until active build target is iOS
+  and the editor is not in play mode.
+* Export Unity Package: Workaround directory creation racy if multiple export
+  operations are spawned at the same time.
+
+# Version 1.2.156 - June 10, 2020
+## Bug Fixes
+* Android Resolver: Fixed that the generated local repo assets contains
+  redundent labels which are causing Version Handler to failed while
+  uninstalling packages.
+* Android Resolver: Fixed that the repo url injected into mainTemplate.gradle
+  is incorrect when Unity is configured to export gradle project.
+* Android Resolver: Limited to only create local Maven repo when the source
+  repo contains ".srcaar" file.
+
+## Changes
+* All: Described EDM4U analytics data usage in readme.
+
+# Version 1.2.155 - May 14, 2020
+## Bug Fixes
+* All: Fixed compiler error when build with Unity 5.4 or below due to the
+  usage of Rect.zero.
+* All: Ignore cases when checking command line arguments.
+
```

</details>
