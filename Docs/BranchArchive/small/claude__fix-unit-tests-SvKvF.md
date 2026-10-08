# Branch archive: `claude/fix-unit-tests-SvKvF`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-26 by Claude
- **Unmerged commits:** 1
- **Forked from:** `b3a08bdb9` (2026-02-26, Merge pull request #156 from froglet-studio/claude/fix-screenshot-errors-Q5Xt3)
- **Tip:** `8069c1748`
- **Files touched (6):**
  - `Assets/_Scripts/Systems/Bootstrap/BootstrapController.cs`
  - `Assets/_Scripts/Systems/Bootstrap/CosmicShore.Bootstrap.asmdef`
  - `Assets/_Scripts/Systems/Bootstrap/CosmicShore.Bootstrap.asmdef.meta`
  - `Assets/_Scripts/Systems/Bootstrap/Tests/CosmicShore.Bootstrap.Tests.asmdef`
  - `Assets/_Scripts/Systems/Bootstrap/Tests/CosmicShore.Bootstrap.Tests.asmdef.meta`
  - `Assets/_Scripts/Systems/Bootstrap/Tests/ServiceLocatorTests.cs`

### `8069c1748` — fix(bootstrap): fix unit test failures and add assembly definitions

_Claude, 2026-02-26 03:05:31 +0000_

```text
- Guard DontDestroyOnLoad with Application.isPlaying in
  BootstrapController.SetupPersistentRoot() to prevent edit-mode test
  failures (DontDestroyOnLoad is invalid outside play mode)
- Add LogAssert.Expect to ServiceLocatorTests.Get_Unregistered_ReturnsNull
  to handle the expected Debug.LogError from ServiceLocator.Get when a
  service is not registered (Unity Test Framework fails on unexpected errors)
- Create CosmicShore.Bootstrap.asmdef for proper assembly isolation of
  bootstrap source code (references UniTask, Netcode, UGUI)
- Create CosmicShore.Bootstrap.Tests.asmdef as editor-only test assembly
  with UNITY_INCLUDE_TESTS constraint and NUnit references
```

```text
 Assets/_Scripts/Systems/Bootstrap/BootstrapController.cs                       |  3 ++-
 Assets/_Scripts/Systems/Bootstrap/CosmicShore.Bootstrap.asmdef                 | 18 ++++++++++++++++++
 Assets/_Scripts/Systems/Bootstrap/CosmicShore.Bootstrap.asmdef.meta            |  7 +++++++
 Assets/_Scripts/Systems/Bootstrap/Tests/CosmicShore.Bootstrap.Tests.asmdef     | 24 ++++++++++++++++++++++++
 .../_Scripts/Systems/Bootstrap/Tests/CosmicShore.Bootstrap.Tests.asmdef.meta   |  7 +++++++
 Assets/_Scripts/Systems/Bootstrap/Tests/ServiceLocatorTests.cs                 |  4 ++++
 6 files changed, 62 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Systems/Bootstrap/BootstrapController.cs b/Assets/_Scripts/Systems/Bootstrap/BootstrapController.cs
index 7045cf07a..7c8d0f9d9 100644
--- a/Assets/_Scripts/Systems/Bootstrap/BootstrapController.cs
+++ b/Assets/_Scripts/Systems/Bootstrap/BootstrapController.cs
@@ -105,7 +105,8 @@ namespace CosmicShore.Systems.Bootstrap
             if (_persistentRoot == null)
                 _persistentRoot = transform;
 
-            DontDestroyOnLoad(_persistentRoot.gameObject);
+            if (Application.isPlaying)
+                DontDestroyOnLoad(_persistentRoot.gameObject);
         }
 
         void ConfigurePlatform()
diff --git a/Assets/_Scripts/Systems/Bootstrap/CosmicShore.Bootstrap.asmdef b/Assets/_Scripts/Systems/Bootstrap/CosmicShore.Bootstrap.asmdef
new file mode 100644
index 000000000..50feea10b
--- /dev/null
+++ b/Assets/_Scripts/Systems/Bootstrap/CosmicShore.Bootstrap.asmdef
@@ -0,0 +1,18 @@
+{
+    "name": "CosmicShore.Bootstrap",
+    "rootNamespace": "CosmicShore.Systems.Bootstrap",
+    "references": [
+        "UniTask",
+        "Unity.Netcode.Runtime",
+        "UnityEngine.UI"
+    ],
+    "includePlatforms": [],
+    "excludePlatforms": [],
+    "allowUnsafeCode": false,
+    "overrideReferences": false,
+    "precompiledReferences": [],
+    "autoReferenced": true,
+    "defineConstraints": [],
+    "versionDefines": [],
+    "noEngineReferences": false
+}
diff --git a/Assets/_Scripts/Systems/Bootstrap/Tests/CosmicShore.Bootstrap.Tests.asmdef b/Assets/_Scripts/Systems/Bootstrap/Tests/CosmicShore.Bootstrap.Tests.asmdef
new file mode 100644
index 000000000..0e0a02427
--- /dev/null
+++ b/Assets/_Scripts/Systems/Bootstrap/Tests/CosmicShore.Bootstrap.Tests.asmdef
@@ -0,0 +1,24 @@
+{
+    "name": "CosmicShore.Bootstrap.Tests",
+    "rootNamespace": "",
+    "references": [
+        "UnityEngine.TestRunner",
+        "UnityEditor.TestRunner",
+        "CosmicShore.Bootstrap"
+    ],
+    "includePlatforms": [
+        "Editor"
+    ],
+    "excludePlatforms": [],
+    "allowUnsafeCode": false,
+    "overrideReferences": true,
+    "precompiledReferences": [
+        "nunit.framework.dll"
+    ],
+    "autoReferenced": false,
+    "defineConstraints": [
+        "UNITY_INCLUDE_TESTS"
+    ],
+    "versionDefines": [],
+    "noEngineReferences": false
+}
diff --git a/Assets/_Scripts/Systems/Bootstrap/Tests/ServiceLocatorTests.cs b/Assets/_Scripts/Systems/Bootstrap/Tests/ServiceLocatorTests.cs
index 86712e88b..97c03fc8a 100644
--- a/Assets/_Scripts/Systems/Bootstrap/Tests/ServiceLocatorTests.cs
+++ b/Assets/_Scripts/Systems/Bootstrap/Tests/ServiceLocatorTests.cs
@@ -1,4 +1,6 @@
 using NUnit.Framework;
+using UnityEngine;
+using UnityEngine.TestTools;
 
 namespace CosmicShore.Systems.Bootstrap
 {
@@ -34,6 +36,8 @@ namespace CosmicShore.Systems.Bootstrap
         [Test]
         public void Get_Unregistered_ReturnsNull()
         {
+            LogAssert.Expect(LogType.Error, "[ServiceLocator] Service ServiceA not registered.");
+
             var result = ServiceLocator.Get<ServiceA>();
 
             Assert.IsNull(result);
```

</details>
