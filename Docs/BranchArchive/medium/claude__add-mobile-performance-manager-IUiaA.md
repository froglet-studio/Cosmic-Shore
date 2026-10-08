# Branch archive: `claude/add-mobile-performance-manager-IUiaA`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-03-07 by Claude
- **Unmerged commits:** 5
- **Forked from:** `14a193123` (2026-03-07, Merge pull request #414 from froglet-studio/claude/migrate-debug-logs-fNeXt)
- **Tip:** `9b6262467`
- **Files touched (10):**
  - `Assets/_Scripts/Utility/MobilePerformanceManager.cs`
  - `Assets/_Scripts/Utility/MobilePerformanceManager.cs.meta`
  - `Assets/_Scripts/Utility/Tools/Benchmarking.meta`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkReport.cs.meta`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs.meta`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs.meta`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs.meta`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/DeterministicBenchmarkController.cs.meta`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/PerformanceSampler.cs.meta`

### `a2548ed49` — Add MobilePerformanceManager for mobile-specific quality settings

_Claude, 2026-03-06 21:39:53 +0000_

```text
SingletonPersistent that auto-creates via RuntimeInitializeOnLoadMethod.
On mobile platforms, applies: 60fps target, hard-only shadows at low
resolution, reduced particle raycast budget, disabled soft particles,
realtime reflection probes, and anisotropic filtering.
```

```text
 Assets/_Scripts/Utility/MobilePerformanceManager.cs | 50 ++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 50 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/MobilePerformanceManager.cs b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
new file mode 100644
index 000000000..9e72f1d19
--- /dev/null
+++ b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
@@ -0,0 +1,50 @@
+using CosmicShore.Utilities;
+using UnityEngine;
+using UnityEngine.Rendering;
+
+namespace CosmicShore.Utility
+{
+    public class MobilePerformanceManager : SingletonPersistent<MobilePerformanceManager>
+    {
+        public static bool IsMobile { get; private set; }
+
+        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
+        static void AutoCreate()
+        {
+            if (Instance == null)
+            {
+                var go = new GameObject(nameof(MobilePerformanceManager));
+                go.AddComponent<MobilePerformanceManager>();
+            }
+        }
+
+        public override void Awake()
+        {
+            base.Awake();
+
+            if (Instance != this)
+                return;
+
+            IsMobile = Application.isMobilePlatform;
+
+            if (IsMobile)
+                ApplyMobileSettings();
+        }
+
+        void ApplyMobileSettings()
+        {
+            Application.targetFrameRate = 60;
+            QualitySettings.shadows = ShadowQuality.HardOnly;
+            QualitySettings.shadowResolution = ShadowResolution.Low;
+            QualitySettings.particleRaycastBudget = 16;
+            QualitySettings.softParticles = false;
+            QualitySettings.realtimeReflectionProbes = false;
+            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
+
+            CSDebug.Log($"[MobilePerformanceManager] Mobile settings applied: " +
+                        $"targetFrameRate=60, shadows=HardOnly, shadowResolution=Low, " +
+                        $"particleRaycastBudget=16, softParticles=false, " +
+                        $"realtimeReflectionProbes=false, anisotropicFiltering=Disable");
+        }
+    }
+}
```

</details>

### `c383a998b` — meta pushes

_Garrett Milliron, 2026-03-07 18:11:12 -0500_

```text
 Assets/_Scripts/Utility/MobilePerformanceManager.cs.meta                            | 2 ++
 Assets/_Scripts/Utility/Tools/Benchmarking.meta                                     | 8 ++++++++
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkReport.cs.meta                  | 2 ++
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs.meta           | 2 ++
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs.meta          | 2 ++
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs.meta                  | 2 ++
 Assets/_Scripts/Utility/Tools/Benchmarking/DeterministicBenchmarkController.cs.meta | 2 ++
 Assets/_Scripts/Utility/Tools/Benchmarking/PerformanceSampler.cs.meta               | 2 ++
 8 files changed, 22 insertions(+)
```

### `75b5be064` — Fix missing UnityEngine.Rendering using for ShadowResolution

_Claude, 2026-03-07 23:19:50 +0000_

```text
 Assets/_Scripts/Utility/MobilePerformanceManager.cs | 1 +
 1 file changed, 1 insertion(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/MobilePerformanceManager.cs b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
index 9319416eb..10cad5299 100644
--- a/Assets/_Scripts/Utility/MobilePerformanceManager.cs
+++ b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
@@ -1,4 +1,5 @@
 using UnityEngine;
+using UnityEngine.Rendering;
 using CosmicShore.Utilities;
 using CosmicShore.Utility;
 
```

</details>

### `9b6262467` — Fix mobile build: wrap BenchmarkSessionSummary in UNITY_EDITOR guard

_Claude, 2026-03-07 23:21:32 +0000_

```text
BenchmarkSessionSummary references BenchmarkSessionConfig which is
editor-only (uses EditorPrefs). Added #if UNITY_EDITOR to fix CS0246
on mobile builds. Also restored missing UnityEngine.Rendering using.
```

```text
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs | 4 ++++
 1 file changed, 4 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs
index 4f22ad0eb..96891ede6 100644
--- a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs
+++ b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs
@@ -1,3 +1,5 @@
+#if UNITY_EDITOR
+
 using System;
 using System.Collections.Generic;
 using System.Linq;
@@ -155,3 +157,5 @@ namespace CosmicShore.Utility.Tools.Benchmarking
         }
     }
 }
+
+#endif
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
