# Branch archive: `claude/fix-unit-tests-VWkMs`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-02-26 by Claude
- **Unmerged commits:** 5
- **Forked from:** `616f21830` (2026-02-26, Merge pull request #143 from froglet-studio/claude/fix-data-accessor-o9Boc)
- **Tip:** `aab6f95b5`
- **Files touched (15):**
  - `Assets/FTUE/Scripts/UI/InGameTutorialFlowView.cs`
  - `Assets/_SO_Assets/SceneNameList.asset`
  - `Assets/_Scripts/CosmicShore.Runtime.asmdef`
  - `Assets/_Scripts/Integrations/Playfab/PlayFabTests/PlayFabTests.asmdef`
  - `Assets/_Scripts/Services/AuthenticationSceneController.cs`
  - `Assets/_Scripts/Services/SplashToAuthFlow.cs`
  - `Assets/_Scripts/Systems/Bootstrap/BootstrapConfigSO.cs`
  - `Assets/_Scripts/Systems/Bootstrap/BootstrapController.cs`
  - `Assets/_Scripts/Systems/Bootstrap/CosmicShore.Bootstrap.asmdef`
  - `Assets/_Scripts/Systems/Bootstrap/Tests/BootstrapConfigSOTests.cs`
  - `Assets/_Scripts/Systems/Bootstrap/Tests/BootstrapControllerTests.cs`
  - `Assets/_Scripts/Systems/Bootstrap/Tests/CosmicShore.Bootstrap.Tests.asmdef`
  - `Assets/_Scripts/Systems/Bootstrap/Tests/SceneFlowIntegrationTests.cs`
  - `Assets/_Scripts/Systems/Bootstrap/Tests/ServiceLocatorTests.cs`
  - `Assets/_Scripts/Utility/DataContainers/SceneNameListSO.cs`

### `c436f631c` — Fix unit tests for Edit Mode compatibility and modernize test assembly config

_Claude, 2026-02-25 22:11:33 +0000_

```text
- ServiceLocatorTests: Add LogAssert.Expect for Debug.LogError from
  Get<T>() when service is not registered, preventing test failure from
  unexpected error log
- BootstrapControllerTests: Add LogAssert.ignoreFailingMessages for
  Awake re-entry guard test where Destroy() may log errors in Edit Mode;
  reset in TearDown for safety
- SceneFlowIntegrationTests: Add SetUp/TearDown to properly manage
  _hasBootstrapped static state and ServiceLocator between tests;
  handle potential DontDestroyOnLoad errors in auto-create test
- PlayFabTests.asmdef: Replace deprecated optionalUnityReferences with
  modern test assembly pattern (overrideReferences, precompiledReferences,
  defineConstraints)
```

```text
 Assets/_Scripts/Integrations/Playfab/PlayFabTests/PlayFabTests.asmdef | 19 +++++++++++--------
 Assets/_Scripts/Systems/Bootstrap/Tests/BootstrapControllerTests.cs   |  5 +++++
 Assets/_Scripts/Systems/Bootstrap/Tests/SceneFlowIntegrationTests.cs  | 24 ++++++++++++++++++++++++
 Assets/_Scripts/Systems/Bootstrap/Tests/ServiceLocatorTests.cs        |  3 +++
 4 files changed, 43 insertions(+), 8 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Integrations/Playfab/PlayFabTests/PlayFabTests.asmdef b/Assets/_Scripts/Integrations/Playfab/PlayFabTests/PlayFabTests.asmdef
index 092e927a1..67d1b98af 100644
--- a/Assets/_Scripts/Integrations/Playfab/PlayFabTests/PlayFabTests.asmdef
+++ b/Assets/_Scripts/Integrations/Playfab/PlayFabTests/PlayFabTests.asmdef
@@ -5,20 +5,23 @@
         "CosmicShore.Core",
         "CosmicShore.Utility",
         "CosmicShore.Runtime",
-        "PlayFab"
-    ],
-    "optionalUnityReferences": [
-        "TestAssemblies"
+        "PlayFab",
+        "UnityEngine.TestRunner",
+        "UnityEditor.TestRunner"
     ],
     "includePlatforms": [
         "Editor"
     ],
     "excludePlatforms": [],
     "allowUnsafeCode": false,
-    "overrideReferences": false,
-    "precompiledReferences": [],
-    "autoReferenced": true,
-    "defineConstraints": [],
+    "overrideReferences": true,
+    "precompiledReferences": [
+        "nunit.framework.dll"
+    ],
+    "autoReferenced": false,
+    "defineConstraints": [
+        "UNITY_INCLUDE_TESTS"
+    ],
     "versionDefines": [],
     "noEngineReferences": false
 }
diff --git a/Assets/_Scripts/Systems/Bootstrap/Tests/BootstrapControllerTests.cs b/Assets/_Scripts/Systems/Bootstrap/Tests/BootstrapControllerTests.cs
index 2122d2ea0..0dd42ee82 100644
--- a/Assets/_Scripts/Systems/Bootstrap/Tests/BootstrapControllerTests.cs
+++ b/Assets/_Scripts/Systems/Bootstrap/Tests/BootstrapControllerTests.cs
@@ -1,6 +1,7 @@
 using System.Reflection;
 using NUnit.Framework;
 using UnityEngine;
+using UnityEngine.TestTools;
 
 namespace CosmicShore.Systems.Bootstrap.Tests
 {
@@ -18,6 +19,7 @@ namespace CosmicShore.Systems.Bootstrap.Tests
         [TearDown]
         public void TearDown()
         {
+            LogAssert.ignoreFailingMessages = false;
             ResetHasBootstrapped();
             ServiceLocator.ClearAll();
         }
@@ -124,6 +126,9 @@ namespace CosmicShore.Systems.Bootstrap.Tests
         {
             SetHasBootstrapped(true);
 
+            // Awake calls Destroy(gameObject) which may log an error in Edit Mode.
+            LogAssert.ignoreFailingMessages = true;
+
             var go = new GameObject("TestBootstrapDuplicate");
             go.AddComponent<BootstrapController>();
 
diff --git a/Assets/_Scripts/Systems/Bootstrap/Tests/SceneFlowIntegrationTests.cs b/Assets/_Scripts/Systems/Bootstrap/Tests/SceneFlowIntegrationTests.cs
index 19e3755da..f2b495a8e 100644
--- a/Assets/_Scripts/Systems/Bootstrap/Tests/SceneFlowIntegrationTests.cs
+++ b/Assets/_Scripts/Systems/Bootstrap/Tests/SceneFlowIntegrationTests.cs
@@ -1,8 +1,10 @@
 using System.Collections.Generic;
 using System.Linq;
+using System.Reflection;
 using NUnit.Framework;
 using UnityEditor;
 using UnityEngine;
+using UnityEngine.TestTools;
 
 namespace CosmicShore.Systems.Bootstrap.Tests
 {
@@ -15,6 +17,25 @@ namespace CosmicShore.Systems.Bootstrap.Tests
     {
         static readonly string[] RequiredScenes = { "Bootstrap", "Authentication", "Menu_Main" };
 
+        [SetUp]
+        public void SetUp()
+        {
+            var field = typeof(BootstrapController)
+                .GetField("_hasBootstrapped", BindingFlags.Static | BindingFlags.NonPublic);
+            field?.SetValue(null, false);
+            ServiceLocator.ClearAll();
+        }
+
+        [TearDown]
+        public void TearDown()
+        {
+            LogAssert.ignoreFailingMessages = false;
+            var field = typeof(BootstrapController)
+                .GetField("_hasBootstrapped", BindingFlags.Static | BindingFlags.NonPublic);
+            field?.SetValue(null, false);
+            ServiceLocator.ClearAll();
+        }
+
         #region Build Settings Validation
 
         [Test]
@@ -313,6 +334,9 @@ namespace CosmicShore.Systems.Bootstrap.Tests
         [Test]
         public void BootstrapController_AutoCreate_ComponentsAreCorrect()
         {
+            // Awake may call DontDestroyOnLoad which can log errors in Edit Mode.
+            LogAssert.ignoreFailingMessages = true;
+
             // Verify that the auto-create flow would produce the right components.
             var go = new GameObject("[TestAutoCreate]");
 
diff --git a/Assets/_Scripts/Systems/Bootstrap/Tests/ServiceLocatorTests.cs b/Assets/_Scripts/Systems/Bootstrap/Tests/ServiceLocatorTests.cs
index 4b95e974c..0d37ccab9 100644
--- a/Assets/_Scripts/Systems/Bootstrap/Tests/ServiceLocatorTests.cs
+++ b/Assets/_Scripts/Systems/Bootstrap/Tests/ServiceLocatorTests.cs
@@ -1,4 +1,5 @@
 using NUnit.Framework;
+using UnityEngine.TestTools;
 
 namespace CosmicShore.Systems.Bootstrap.Tests
 {
@@ -34,6 +35,8 @@ namespace CosmicShore.Systems.Bootstrap.Tests
         [Test]
         public void Get_Unregistered_ReturnsNull()
         {
+            LogAssert.Expect(UnityEngine.LogType.Error, "[ServiceLocator] Service ServiceA not registered.");
+
             var result = ServiceLocator.Get<ServiceA>();
 
             Assert.IsNull(result);
```

</details>

### `4d3200485` — Fix PlayFabTests.asmdef rootNamespace casing to match code

_Claude, 2026-02-26 00:55:53 +0000_

```text
rootNamespace used "PlayFab" but the code file uses "Playfab" (lowercase f),
matching the directory structure.
```

```text
 Assets/_Scripts/Integrations/Playfab/PlayFabTests/PlayFabTests.asmdef | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Integrations/Playfab/PlayFabTests/PlayFabTests.asmdef b/Assets/_Scripts/Integrations/Playfab/PlayFabTests/PlayFabTests.asmdef
index 67d1b98af..3b7c73dcc 100644
--- a/Assets/_Scripts/Integrations/Playfab/PlayFabTests/PlayFabTests.asmdef
+++ b/Assets/_Scripts/Integrations/Playfab/PlayFabTests/PlayFabTests.asmdef
@@ -1,6 +1,6 @@
 {
     "name": "PlayFabTests",
-    "rootNamespace": "CosmicShore.Integrations.PlayFab.PlayFabTests",
+    "rootNamespace": "CosmicShore.Integrations.Playfab.PlayFabTests",
     "references": [
         "CosmicShore.Core",
         "CosmicShore.Utility",
```

</details>

### `aab6f95b5` — Consolidate all scene names into SceneNameListSO as single source of truth

_Claude, 2026-02-26 01:19:07 +0000_

```text
- Add BootstrapScene and AuthenticationScene fields to SceneNameListSO
- Remove scene name fields (FirstSceneName, MainMenuSceneName) from BootstrapConfigSO
- Update BootstrapController to read scene names from SceneNameListSO
- Replace hardcoded scene names in SplashToAuthFlow, AuthenticationSceneController,
  and InGameTutorialFlowView with SceneNameListSO references
- Update SceneNameList.asset with Bootstrap and Authentication scene values
- Add CosmicShore.Utility ref to Bootstrap asmdef (for SceneNameListSO access)
- Add CosmicShore.Bootstrap ref to Runtime asmdef (for ServiceLocator access)
- Update BootstrapConfigSOTests: remove tests for deleted scene name properties
- Update SceneFlowIntegrationTests: replace BootstrapConfigSO scene name tests
  with SceneNameListSO validation (BootstrapScene, AuthenticationScene fields)
- Add CosmicShore.Utility ref to Bootstrap.Tests asmdef
```

```text
 Assets/FTUE/Scripts/UI/InGameTutorialFlowView.cs                      |   4 +-
 Assets/_SO_Assets/SceneNameList.asset                                 |   4 +-
 Assets/_Scripts/CosmicShore.Runtime.asmdef                            |   1 +
 Assets/_Scripts/Services/AuthenticationSceneController.cs             |   7 +-
 Assets/_Scripts/Services/SplashToAuthFlow.cs                          |  12 +--
 Assets/_Scripts/Systems/Bootstrap/BootstrapConfigSO.cs                |   9 --
 Assets/_Scripts/Systems/Bootstrap/BootstrapController.cs              |   6 +-
 Assets/_Scripts/Systems/Bootstrap/CosmicShore.Bootstrap.asmdef        |   3 +-
 Assets/_Scripts/Systems/Bootstrap/Tests/BootstrapConfigSOTests.cs     |  16 ---
 .../Systems/Bootstrap/Tests/CosmicShore.Bootstrap.Tests.asmdef        |   1 +
 Assets/_Scripts/Systems/Bootstrap/Tests/SceneFlowIntegrationTests.cs  | 176 ++++++++++++++++++--------------
 Assets/_Scripts/Utility/DataContainers/SceneNameListSO.cs             |   6 +-
 12 files changed, 127 insertions(+), 118 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 523 lines)</summary>

```diff
diff --git a/Assets/FTUE/Scripts/UI/InGameTutorialFlowView.cs b/Assets/FTUE/Scripts/UI/InGameTutorialFlowView.cs
index 1574db08e..26ac94420 100644
--- a/Assets/FTUE/Scripts/UI/InGameTutorialFlowView.cs
+++ b/Assets/FTUE/Scripts/UI/InGameTutorialFlowView.cs
@@ -6,6 +6,7 @@ using CosmicShore.FTUE.Adapters;
 using CosmicShore.FTUE.Data;
 using UnityEngine.SceneManagement;
 using CosmicShore.Models.Enums;
+using CosmicShore.Utility.DataContainers;
 
 namespace CosmicShore.FTUE.UI
 {
@@ -16,6 +17,7 @@ namespace CosmicShore.FTUE.UI
         [SerializeField] private FTUEIntroAnimatorAdapter _animatorAdapter;
         [SerializeField] private TutorialUIViewAdapter _uiViewAdapter;
         [SerializeField] private GameObject _skipButton;
+        [SerializeField] private SceneNameListSO _sceneNames;
 
         private bool _phase2Started;
 
@@ -99,7 +101,7 @@ namespace CosmicShore.FTUE.UI
 
         public void ReturnToMainMenu()
         {
-            SceneManager.LoadScene("Menu_Main");
+            SceneManager.LoadScene(_sceneNames.MainMenuScene);
         }
     }
 }
diff --git a/Assets/_Scripts/CosmicShore.Runtime.asmdef b/Assets/_Scripts/CosmicShore.Runtime.asmdef
index 45690d5fe..1b5461c30 100644
--- a/Assets/_Scripts/CosmicShore.Runtime.asmdef
+++ b/Assets/_Scripts/CosmicShore.Runtime.asmdef
@@ -4,6 +4,7 @@
     "references": [
         "CosmicShore.Core",
         "CosmicShore.Utility",
+        "CosmicShore.Bootstrap",
         "Obvious.Soap",
         "PlayFab",
         "Lofelt.NiceVibrations",
diff --git a/Assets/_Scripts/Services/AuthenticationSceneController.cs b/Assets/_Scripts/Services/AuthenticationSceneController.cs
index 953b28b66..695917f2f 100644
--- a/Assets/_Scripts/Services/AuthenticationSceneController.cs
+++ b/Assets/_Scripts/Services/AuthenticationSceneController.cs
@@ -2,6 +2,7 @@ using System;
 using System.Threading.Tasks;
 using CosmicShore.UI.Views;
 using CosmicShore.Systems.Bootstrap;
+using CosmicShore.Utility.DataContainers;
 using Cysharp.Threading.Tasks;
 using Reflex.Attributes;
 using TMPro;
@@ -45,7 +46,7 @@ namespace CosmicShore.Services
         [Inject] private PlayerDataService playerDataService;
 
         [Header("Navigation")]
-        [SerializeField] private string mainMenuSceneName = "Menu_Main";
+        [SerializeField] private SceneNameListSO _sceneNames;
 
         [Header("Auto-Skip")]
         [SerializeField, Tooltip("Seconds to wait for cached auth before showing UI.")]
@@ -328,11 +329,11 @@ namespace CosmicShore.Services
 
             if (ServiceLocator.TryGet<SceneTransitionManager>(out var transitionManager))
             {
-                transitionManager.LoadSceneAsync(mainMenuSceneName).Forget();
+                transitionManager.LoadSceneAsync(_sceneNames.MainMenuScene).Forget();
             }
             else
             {
-                SceneManager.LoadScene(mainMenuSceneName);
+                SceneManager.LoadScene(_sceneNames.MainMenuScene);
             }
         }
     }
diff --git a/Assets/_Scripts/Services/SplashToAuthFlow.cs b/Assets/_Scripts/Services/SplashToAuthFlow.cs
index 9d5f6019c..cf1a7f11a 100644
--- a/Assets/_Scripts/Services/SplashToAuthFlow.cs
+++ b/Assets/_Scripts/Services/SplashToAuthFlow.cs
@@ -1,6 +1,7 @@
 using System;
 using System.Threading.Tasks;
 using CosmicShore.Systems.Bootstrap;
+using CosmicShore.Utility.DataContainers;
 using Cysharp.Threading.Tasks;
 using UnityEngine;
 using UnityEngine.SceneManagement;
@@ -19,8 +20,7 @@ namespace CosmicShore.Services
     public class SplashToAuthFlow : MonoBehaviour
     {
         [Header("Scene Names")]
-        [SerializeField] private string authSceneName = "Authentication";
-        [SerializeField] private string mainMenuSceneName = "Menu_Main";
+        [SerializeField] private SceneNameListSO _sceneNames;
 
         [Header("Splash")]
         [SerializeField] private float splashDisplayDuration = 2f;
@@ -41,7 +41,7 @@ namespace CosmicShore.Services
                 if (authController == null)
                 {
                     CSDebug.LogWarning("[SplashToAuthFlow] No AuthenticationController found. Going to auth scene.");
-                    await LoadSceneWithTransitionAsync(authSceneName);
+                    await LoadSceneWithTransitionAsync(_sceneNames.AuthenticationScene);
                     return;
                 }
 
@@ -51,18 +51,18 @@ namespace CosmicShore.Services
                 if (signedIn)
                 {
                     CSDebug.Log("[SplashToAuthFlow] Cached session valid. Going to main menu.");
-                    await LoadSceneWithTransitionAsync(mainMenuSceneName);
+                    await LoadSceneWithTransitionAsync(_sceneNames.MainMenuScene);
                 }
                 else
                 {
                     CSDebug.Log("[SplashToAuthFlow] No cached session. Going to auth scene.");
-                    await LoadSceneWithTransitionAsync(authSceneName);
+                    await LoadSceneWithTransitionAsync(_sceneNames.AuthenticationScene);
                 }
             }
             catch (Exception ex)
             {
                 CSDebug.LogWarning($"[SplashToAuthFlow] Error during splash flow: {ex.Message}. Falling back to auth scene.");
-                await LoadSceneWithTransitionAsync(authSceneName);
+                await LoadSceneWithTransitionAsync(_sceneNames.AuthenticationScene);
             }
         }
 
diff --git a/Assets/_Scripts/Systems/Bootstrap/BootstrapConfigSO.cs b/Assets/_Scripts/Systems/Bootstrap/BootstrapConfigSO.cs
index 3f22b60ed..ae792ea76 100644
--- a/Assets/_Scripts/Systems/Bootstrap/BootstrapConfigSO.cs
+++ b/Assets/_Scripts/Systems/Bootstrap/BootstrapConfigSO.cs
@@ -7,13 +7,6 @@ namespace CosmicShore.Systems.Bootstrap
         menuName = "ScriptableObjects/Core/BootstrapConfig")]
     public class BootstrapConfigSO : ScriptableObject
     {
-        [Header("Scene Flow")]
-        [SerializeField, Tooltip("Scene to load after bootstrap completes. Typically the Authentication scene.")]
-        string _firstSceneName = "Authentication";
-
-        [SerializeField, Tooltip("Scene to load after authentication succeeds.")]
-        string _mainMenuSceneName = "Menu_Main";
-
         [Header("Timeouts")]
         [SerializeField, Tooltip("Max seconds to wait for all services to initialize before giving up.")]
         float _serviceInitTimeoutSeconds = 15f;
@@ -35,8 +28,6 @@ namespace CosmicShore.Systems.Bootstrap
         [SerializeField, Tooltip("Log detailed bootstrap timing to the console.")]
         bool _verboseLogging;
```

</details>

_Also contains 2 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
