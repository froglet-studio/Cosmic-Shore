# Branch archive: `claude/pull-benchmark-tool-Z9AmX`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

## What this branch is

**Performance benchmark tool and session runner**

Added an editor performance benchmark tool: a window that measures frame-rate metrics, a deterministic mode for repeatable runs, and an automated session runner that launches arcade games at several intensities, presses Go, samples 20s of gameplay and reports reproducibility. It also pulled in mobile input fixes, Android SDK 35 bump and Odin reference cleanup, plus a fix so the chosen vessel is respected when launching arcade games.

- **Status:** Redone elsewhere
- **Areas:** performance benchmarking, editor tooling, mobile input, arcade launch
- **Already in bleeding-edge:** A different benchmark system exists in bleeding-edge: Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_ARCHITECTURE.md, Assets/_Scripts/Editor/AI/SkimRaceBenchmarkWindow.cs, Docs/PRISM_EXPLOSION_BENCHMARK.md. This branch's BenchmarkWindow/PerformanceSampler/DeterministicBenchmarkController files are absent.
- **Risk if deleted:** low
- **Suggestion (2026-10-08):** can be deleted after archiving — Benchmarking was rebuilt under Utility/PerformanceBenchmark; this early version is redundant.

## Evidence

- **Last commit:** 2026-03-08 by Claude
- **Unmerged commits:** 21
- **Forked from:** `0d48ab5b5` (2026-02-25, Merge pull request #78 from froglet-studio/claude/add-missing-sounds-1ZgoJ)
- **Tip:** `e9d01cce5`
- **Files touched (26):**
  - `Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableEvents/ScriptableEvent.cs`
  - `Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableVariables/VariableReferences/VariableReference.cs`
  - `Assets/_Scripts/App/UI/ScreenSwitcher.cs`
  - `Assets/_Scripts/Game/IO/BaseInputStrategy.cs`
  - `Assets/_Scripts/Game/IO/ControllerButtonPress.cs`
  - `Assets/_Scripts/Game/IO/IInputStatus.cs`
  - `Assets/_Scripts/Game/IO/InputController.cs`
  - `Assets/_Scripts/Game/IO/InputStatus.cs`
  - `Assets/_Scripts/Game/IO/TouchInputStrategy.cs`
  - `Assets/_Scripts/Game/Managers/Arcade.cs`
  - `Assets/_Scripts/Game/UI/Editor/ActiveGameModesWindow.cs`
  - `Assets/_Scripts/Game/UI/Editor/ActiveGameModesWindow.cs.meta`
  - `Assets/_Scripts/Game/UI/Editor/LeaderboardConfigSOEditor.cs`
  - `Assets/_Scripts/Game/UI/Editor/LeaderboardConfigSOEditor.cs.meta`
  - `Assets/_Scripts/Game/UI/HexRaceHUDView.cs`
  - `Assets/_Scripts/Models/Enums/InputDeviceType.cs`
  - `Assets/_Scripts/Models/Enums/InputDeviceType.cs.meta`
  - `Assets/_Scripts/Utility/CSDebug.cs`
  - `Assets/_Scripts/Utility/CSDebug.cs.meta`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkReport.cs`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/DeterministicBenchmarkController.cs`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/PerformanceSampler.cs`
  - `ProjectSettings/ProjectSettings.asset`

### `02f683dab` — Remove Sirenix/Odin Inspector references from Soap plugin

_Claude, 2026-03-08 00:15:09 +0000_

```text
The Odin Inspector package is not included in this project, causing
CS0246 build errors. Remove the #if ODIN_INSPECTOR blocks with
Sirenix.OdinInspector.DrawWithUnity attributes from ScriptableEvent
and VariableReference.
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

### `ae26879a0` — Move editor scripts to Editor folder to fix player build errors

_Claude, 2026-03-08 00:31:01 +0000_

```text
LeaderboardConfigSOEditor.cs and ActiveGameModesWindow.cs use UnityEditor
types (Editor, EditorWindow, SerializedObject, etc.) which are not available
in player builds. Moving them to an Editor folder ensures Unity excludes
them from player compilation.
```

```text
 Assets/_Scripts/Game/UI/{ => Editor}/ActiveGameModesWindow.cs          | 0
 Assets/_Scripts/Game/UI/{ => Editor}/ActiveGameModesWindow.cs.meta     | 0
 Assets/_Scripts/Game/UI/{ => Editor}/LeaderboardConfigSOEditor.cs      | 0
 Assets/_Scripts/Game/UI/{ => Editor}/LeaderboardConfigSOEditor.cs.meta | 0
 4 files changed, 0 insertions(+), 0 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 655 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/UI/Editor/ActiveGameModesWindow.cs b/Assets/_Scripts/Game/UI/Editor/ActiveGameModesWindow.cs
new file mode 100644
index 000000000..89efc40b5
--- /dev/null
+++ b/Assets/_Scripts/Game/UI/Editor/ActiveGameModesWindow.cs
@@ -0,0 +1,198 @@
+﻿using System.Collections.Generic;
+using System.Linq;
+using UnityEditor;
+using UnityEngine;
+
+namespace CosmicShore.Game.Analytics
+{
+    public class ActiveGameModesWindow : EditorWindow
+    {
+        private LeaderboardConfigSO config;
+        private SerializedObject serializedConfig;
+        private SerializedProperty activeGameModesProperty;
+        private HashSet<GameModes> selectedModes = new HashSet<GameModes>();
+        private Vector2 scrollPosition;
+        private string searchFilter = "";
+
+        public static void ShowWindow(LeaderboardConfigSO config)
+        {
+            var window = GetWindow<ActiveGameModesWindow>("Active Game Modes");
+            window.config = config;
+            window.minSize = new Vector2(400, 500);
+            window.Initialize();
+            window.Show();
+        }
+
+        private void Initialize()
+        {
+            serializedConfig = new SerializedObject(config);
+            activeGameModesProperty = serializedConfig.FindProperty("activeGameModes");
+            
+            // Load current active modes
+            selectedModes.Clear();
+            if (activeGameModesProperty != null && activeGameModesProperty.isArray)
+            {
+                for (int i = 0; i < activeGameModesProperty.arraySize; i++)
+                {
+                    var element = activeGameModesProperty.GetArrayElementAtIndex(i);
+                    selectedModes.Add((GameModes)element.enumValueIndex);
+                }
+            }
+
+            // If no modes selected, select all by default
+            if (selectedModes.Count == 0)
+            {
+                selectedModes = new HashSet<GameModes>(
+                    System.Enum.GetValues(typeof(GameModes)).Cast<GameModes>()
+                );
+            }
+        }
+
+        private void OnGUI()
+        {
+            if (config == null)
+            {
+                EditorGUILayout.HelpBox("Configuration not found. Please close this window.", MessageType.Error);
+                return;
+            }
+
+            serializedConfig.Update();
+
+            // Header
+            EditorGUILayout.Space(10);
+            EditorGUILayout.LabelField("Select Active Game Modes", EditorStyles.boldLabel);
+            EditorGUILayout.HelpBox("Choose which game modes are currently active in your game. " +
+                "Only active modes will be shown by default in the main editor.", MessageType.Info);
+            EditorGUILayout.Space(5);
+
+            // Search bar
+            EditorGUILayout.BeginHorizontal();
+            EditorGUILayout.LabelField("Search:", GUILayout.Width(60));
+            searchFilter = EditorGUILayout.TextField(searchFilter);
+            if (GUILayout.Button("Clear", GUILayout.Width(50)))
+            {
+                searchFilter = "";
```

</details>

### `ac63345b1` — Fully qualify Editor base class to avoid namespace conflict

_Claude, 2026-03-08 00:35:55 +0000_

```text
The Editor folder introduces a namespace that shadows the UnityEditor.Editor
type. Using the fully qualified name resolves the CS0118 error.
```

```text
 Assets/_Scripts/Game/UI/Editor/LeaderboardConfigSOEditor.cs | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/UI/Editor/LeaderboardConfigSOEditor.cs b/Assets/_Scripts/Game/UI/Editor/LeaderboardConfigSOEditor.cs
index 580b5e77f..2435ab58a 100644
--- a/Assets/_Scripts/Game/UI/Editor/LeaderboardConfigSOEditor.cs
+++ b/Assets/_Scripts/Game/UI/Editor/LeaderboardConfigSOEditor.cs
@@ -6,7 +6,7 @@ using UnityEngine;
 namespace CosmicShore.Game.Analytics
 {
     [CustomEditor(typeof(LeaderboardConfigSO))]
-    public class LeaderboardConfigSOEditor : Editor
+    public class LeaderboardConfigSOEditor : UnityEditor.Editor
     {
         private SerializedProperty leaderboardMappingsProperty;
         private SerializedProperty activeGameModesProperty;
```

</details>

### `3c01f874d` — Remove duplicate serialized fields from HexRaceHUDView

_Claude, 2026-03-08 00:40:38 +0000_

```text
HexRaceHUDView re-declared playerScoreContainer, playerScoreCardPrefab,
and domainColors which are already defined in its parent MiniGameHUDView.
Unity does not support the same serialized field name in both a class and
its parent, causing build failures.
```

```text
 Assets/_Scripts/Game/UI/HexRaceHUDView.cs | 38 +-------------------------------------
 1 file changed, 1 insertion(+), 37 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/UI/HexRaceHUDView.cs b/Assets/_Scripts/Game/UI/HexRaceHUDView.cs
index 040f363df..bff19320f 100644
--- a/Assets/_Scripts/Game/UI/HexRaceHUDView.cs
+++ b/Assets/_Scripts/Game/UI/HexRaceHUDView.cs
@@ -1,42 +1,6 @@
-﻿using System;
-using System.Collections.Generic;
-using System.Linq;
-using CosmicShore.Models.Enums;
-using UnityEngine;
-
 namespace CosmicShore.Game.UI
 {
     public class HexRaceHUDView : MiniGameHUDView
     {
-        [Header("Multiplayer Elements")]
-        [SerializeField] private Transform playerScoreContainer;
-        [SerializeField] private PlayerScoreCard playerScoreCardPrefab;
-
-        [Header("Domain Styling")]
-        [SerializeField] private List<DomainColorDef> domainColors;
-
-        public Transform PlayerScoreContainer => playerScoreContainer;
-        public PlayerScoreCard PlayerScoreCardPrefab => playerScoreCardPrefab;
-
-        public void ClearPlayerList()
-        {
-            foreach (Transform child in playerScoreContainer)
-            {
-                Destroy(child.gameObject);
-            }
-        }
-
-        public Color GetColorForDomain(Domains domain)
-        {
-            var def = domainColors.FirstOrDefault(d => d.Domain == domain);
-            return def.Equals(default(DomainColorDef)) ? Color.white : def.Color;
-        }
-
-        [Serializable]
-        public struct DomainColorDef
-        {
-            public Domains Domain;
-            public Color Color;
-        }
     }
-}
\ No newline at end of file
+}
```

</details>

### `bbc5e2cb5` — Bump Android target SDK from 33 to 35

_Claude, 2026-03-08 00:55:04 +0000_

```text
androidx.credentials:credentials:1.2.0-rc01 requires compileSdk 34+.
The project was set to android-33, causing checkReleaseAarMetadata to
fail during Gradle build. Bumping to 35 satisfies the requirement and
aligns with current Google Play target API level recommendations.
```

```text
 ProjectSettings/ProjectSettings.asset | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

### `1b0bd1c03` — Pull mobile input support from development branch

_Claude, 2026-03-08 01:10:54 +0000_

```text
Cherry-picked the minimum set of changes needed to get touch input
working on mobile:

- InputController: enable TouchInputStrategy, wire it into strategy
  selection for handheld devices
- TouchInputStrategy: add drift detection (finger-lift transitions),
  touch-tuned easing curve, and OnlyLeft/OnlyRight stick actions
- InputStatus: add LeftTriggerAnalog, RightTriggerAnalog properties
  and ActiveInputDevice tracking
- ControllerButtonPress: fix button firing across all contexts, add
  modal-aware dispatch and interactability guards
- InputDeviceType enum: new file for touch/gamepad/keyboard detection
- CSDebug: centralized logger (dependency of updated input files)
```

```text
 Assets/_Scripts/Game/IO/ControllerButtonPress.cs     |  71 +++++++++++--------
 Assets/_Scripts/Game/IO/InputController.cs           |  32 ++++-----
 Assets/_Scripts/Game/IO/InputStatus.cs               |  26 ++++++-
 Assets/_Scripts/Game/IO/TouchInputStrategy.cs        | 122 ++++++++++++++++++++++++++-------
 Assets/_Scripts/Models/Enums/InputDeviceType.cs      |   6 ++
 Assets/_Scripts/Models/Enums/InputDeviceType.cs.meta |   2 +
 Assets/_Scripts/Utility/CSDebug.cs                   | 181 +++++++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Utility/CSDebug.cs.meta              |   2 +
 8 files changed, 371 insertions(+), 71 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 753 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/IO/ControllerButtonPress.cs b/Assets/_Scripts/Game/IO/ControllerButtonPress.cs
index 05720a61c..1275d0b3a 100644
--- a/Assets/_Scripts/Game/IO/ControllerButtonPress.cs
+++ b/Assets/_Scripts/Game/IO/ControllerButtonPress.cs
@@ -8,6 +8,7 @@ using UnityEngine.InputSystem.LowLevel;
 using UnityEngine.InputSystem.XInput;
 using UnityEngine.UI;
 using static CosmicShore.App.UI.ScreenSwitcher;
+using CosmicShore.Utility;
 
 namespace CosmicShore.Game.IO
 {
@@ -32,17 +33,17 @@ namespace CosmicShore.Game.IO
         EventSystem eventSystem;
         ScreenSwitcher screenSwitcher;
         Button button;
-        
+
         bool _wasCanvasGroupInteractableLastFrame;
-        
+
         void Start()
         {
             if (canvasGroup)
                 _wasCanvasGroupInteractableLastFrame = canvasGroup.interactable;
-            
+
             eventSystem = FindAnyObjectByType<EventSystem>();
-            screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
             button = GetComponent<Button>();
+            screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
 
             if (!activationButtonImage)
                 return;
@@ -67,7 +68,7 @@ namespace CosmicShore.Game.IO
                         activationButtonImage.gameObject.SetActive(false);
                         break;
                 }
-                Debug.Log("A DualShock controller is connected.");
+                CSDebug.Log("A DualShock controller is connected.");
             }
             else if (Gamepad.current is XInputController)
             {
@@ -89,7 +90,7 @@ namespace CosmicShore.Game.IO
                         activationButtonImage.gameObject.SetActive(false);
                         break;
                 }
-                Debug.Log("An Xbox controller is connected.");
+                CSDebug.Log("An Xbox controller is connected.");
             }
             else
             {
@@ -99,57 +100,69 @@ namespace CosmicShore.Game.IO
 
         void Update()
         {
+            if (Gamepad.current == null || !Gamepad.current[activationButton].wasPressedThisFrame)
+                return;
+
             // Remove the null check -> after making sure every place of ControllerButtonPress has a canvas group
             // reference added to them
-
             if (canvasGroup)
             {
                 bool wasInteractableLastFrame = _wasCanvasGroupInteractableLastFrame;
-                _wasCanvasGroupInteractableLastFrame = canvasGroup.interactable;    
-                
+                _wasCanvasGroupInteractableLastFrame = canvasGroup.interactable;
+
                 if (!wasInteractableLastFrame)
                     return;
             }
-            
+
+            // Lazy lookup: button's GO may have been inactive during Start()
+            if (screenSwitcher == null)
+                screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
+
             if (screenSwitcher != null)
             {
```

</details>

### `39d893866` — Mark BaseInputStrategy.Ease virtual so TouchInputStrategy can override

_Claude, 2026-03-08 01:14:45 +0000_

```text
TouchInputStrategy provides a touch-tuned easing curve that differs
from the gamepad cosine curve in the base class.
```

```text
 Assets/_Scripts/Game/IO/BaseInputStrategy.cs | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/IO/BaseInputStrategy.cs b/Assets/_Scripts/Game/IO/BaseInputStrategy.cs
index 709f15a85..82eab085f 100644
--- a/Assets/_Scripts/Game/IO/BaseInputStrategy.cs
+++ b/Assets/_Scripts/Game/IO/BaseInputStrategy.cs
@@ -31,7 +31,7 @@ namespace CosmicShore.Game.IO
             inputStatus.InvertThrottleEnabled = status;
         }
 
-        protected float Ease(float input)
+        protected virtual float Ease(float input)
         {
             return input < 0 ?
                 (Mathf.Cos(input * PI_OVER_FOUR) - 1) :
```

</details>

### `9b222e994` — Add missing ActiveInputDevice to IInputStatus and HasActiveModal to ScreenSwitcher

_Claude, 2026-03-08 01:21:45 +0000_

```text
IInputStatus was missing the ActiveInputDevice property that
InputStatus already implements and TouchInputStrategy uses.

ScreenSwitcher was missing HasActiveModal, needed by
ControllerButtonPress to gate input when modals are open.
```

```text
 Assets/_Scripts/App/UI/ScreenSwitcher.cs | 2 ++
 Assets/_Scripts/Game/IO/IInputStatus.cs  | 2 ++
 2 files changed, 4 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/App/UI/ScreenSwitcher.cs b/Assets/_Scripts/App/UI/ScreenSwitcher.cs
index 0740ba611..4870fe7e6 100644
--- a/Assets/_Scripts/App/UI/ScreenSwitcher.cs
+++ b/Assets/_Scripts/App/UI/ScreenSwitcher.cs
@@ -156,6 +156,8 @@ namespace CosmicShore.App.UI
             return GetScreenIdForIndex(currentScreen) == screen;
         }
 
+        public bool HasActiveModal => activeModalStack.Count > 0;
+
         public bool ModalIsActive(ModalWindows modal)
         {
             if (activeModalStack.Count == 0)
diff --git a/Assets/_Scripts/Game/IO/IInputStatus.cs b/Assets/_Scripts/Game/IO/IInputStatus.cs
index b1699fadf..5278f4c64 100644
--- a/Assets/_Scripts/Game/IO/IInputStatus.cs
+++ b/Assets/_Scripts/Game/IO/IInputStatus.cs
@@ -48,6 +48,8 @@ namespace CosmicShore.Game
         Vector2 SingleTouchValue { get; set; }
         Vector3 ThreeDPosition { get; set; }
 
+        InputDeviceType ActiveInputDevice { get; set; }
+
         Quaternion GetGyroRotation();
         void ResetForReplay();
     }
```

</details>

### `829df596c` — Add performance benchmark tool for measuring and comparing frame metrics

_Claude, 2026-03-08 01:53:09 +0000_

```text
Introduces a three-part benchmarking system:
- PerformanceSampler: runtime component that captures per-frame CPU/GPU timing,
  GC allocations, and render stats using ProfilerRecorder and FrameTimingManager
- BenchmarkReport: data model with percentile analysis (P1/P5/P95/P99), jank
  detection, and A/B comparison with delta highlighting
- BenchmarkWindow: editor window (FrogletTools > Benchmark) with tabs for
  running benchmarks, viewing results, comparing saved snapshots, and browsing
  history — reports persist as JSON in /BenchmarkReports/
```

```text
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkReport.cs    | 255 +++++++++++++++
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs    | 625 +++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Utility/Tools/Benchmarking/PerformanceSampler.cs | 218 +++++++++++++
 3 files changed, 1098 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 1116 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkReport.cs b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkReport.cs
new file mode 100644
index 000000000..ede588868
--- /dev/null
+++ b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkReport.cs
@@ -0,0 +1,255 @@
+using System;
+using System.Collections.Generic;
+using System.IO;
+using System.Linq;
+using UnityEngine;
+
+namespace CosmicShore.Utility.Tools.Benchmarking
+{
+    /// <summary>
+    /// Immutable snapshot of a benchmark run. Serializable to JSON for persistence.
+    /// </summary>
+    [Serializable]
+    public class BenchmarkReport
+    {
+        // ── Metadata ────────────────────────────────────────────────────────
+        public string Label;
+        public string GitCommit;
+        public string SceneName;
+        public string Timestamp;
+        public float DurationSeconds;
+        public int TotalFrames;
+
+        // ── Frame Time (ms) ─────────────────────────────────────────────────
+        public float AvgFrameTimeMs;
+        public float MinFrameTimeMs;
+        public float MaxFrameTimeMs;
+        public float MedianFrameTimeMs;
+        public float P1FrameTimeMs;
+        public float P5FrameTimeMs;
+        public float P95FrameTimeMs;
+        public float P99FrameTimeMs;
+        public float StdDevFrameTimeMs;
+
+        // ── FPS (derived) ───────────────────────────────────────────────────
+        public float AvgFps;
+        public float P1Fps;
+        public float P5Fps;
+
+        // ── GPU (when available via FrameTimingManager) ─────────────────────
+        public float AvgGpuTimeMs;
+
+        // ── Memory / GC ─────────────────────────────────────────────────────
+        public long TotalGcAllocBytes;
+        public int GcCollectCount;
+        public long PeakUsedMemoryBytes;
+
+        // ── Render stats ────────────────────────────────────────────────────
+        public long AvgDrawCalls;
+        public long AvgTriangles;
+        public long AvgVertices;
+        public long AvgSetPassCalls;
+
+        // ── Raw frame times (for histogram / custom analysis) ───────────────
+        public List<float> FrameTimeSamples = new();
+
+        // ── Jank metric ─────────────────────────────────────────────────────
+        /// <summary>Percentage of frames that exceeded 2x the average frame time.</summary>
+        public float JankPercent;
+
+        // ── Construction ────────────────────────────────────────────────────
+
+        public static BenchmarkReport Build(
+            string label,
+            string sceneName,
+            float duration,
+            List<float> frameTimesMs,
+            List<float> gpuTimesMs,
+            long gcAllocBytes,
+            int gcCollectCount,
+            long peakMemory,
+            List<long> drawCalls,
+            List<long> triangles,
+            List<long> vertices,
+            List<long> setPassCalls)
```

</details>

### `8a3ae42c4` — Add deterministic mode to benchmark tool for repeatable results

_Claude, 2026-03-08 01:53:09 +0000_

```text
Seeds Random.InitState() with a fixed seed, locks physics timestep,
disables VSync, and uses frame-counted warmup instead of wall-clock time.
The deterministic controller re-seeds immediately before sampling begins
so the measured window sees an identical random sequence every run.
```

```text
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkReport.cs         |  12 +++-
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs         | 104 +++++++++++++++++++++++++++++--
 .../Utility/Tools/Benchmarking/DeterministicBenchmarkController.cs    | 105 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Utility/Tools/Benchmarking/PerformanceSampler.cs      |  46 +++++++++++++-
 4 files changed, 257 insertions(+), 10 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 425 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkReport.cs b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkReport.cs
index ede588868..b1eae85ec 100644
--- a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkReport.cs
+++ b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkReport.cs
@@ -20,6 +20,10 @@ namespace CosmicShore.Utility.Tools.Benchmarking
         public float DurationSeconds;
         public int TotalFrames;
 
+        // ── Deterministic settings ────────────────────────────────────────
+        public bool Deterministic;
+        public int DeterministicSeed;
+
         // ── Frame Time (ms) ─────────────────────────────────────────────────
         public float AvgFrameTimeMs;
         public float MinFrameTimeMs;
@@ -71,7 +75,9 @@ namespace CosmicShore.Utility.Tools.Benchmarking
             List<long> drawCalls,
             List<long> triangles,
             List<long> vertices,
-            List<long> setPassCalls)
+            List<long> setPassCalls,
+            bool deterministic = false,
+            int deterministicSeed = 0)
         {
             var sorted = frameTimesMs.OrderBy(t => t).ToList();
             int count = sorted.Count;
@@ -111,6 +117,10 @@ namespace CosmicShore.Utility.Tools.Benchmarking
                 JankPercent = 100f * sorted.Count(t => t > avg * 2f) / count,
             };
 
+            // Deterministic metadata
+            report.Deterministic = deterministic;
+            report.DeterministicSeed = deterministicSeed;
+
             // Try to grab git commit hash
             report.GitCommit = GetGitCommit();
 
diff --git a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
index 7d65c78ea..0f82ac97d 100644
--- a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
+++ b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
@@ -16,8 +16,15 @@ namespace CosmicShore.Utility.Tools.Benchmarking
         float _warmupSeconds = 2f;
         float _durationSeconds = 10f;
 
+        // ── Deterministic config ─────────────────────────────────────────
+        bool _deterministicMode = true;
+        int _deterministicSeed = 42;
+        int _deterministicWarmupFrames = 120;
+        float _deterministicFixedDt = 0.02f;
+
         // ── State ───────────────────────────────────────────────────────────
         PerformanceSampler _activeSampler;
+        DeterministicBenchmarkController _deterministicController;
         BenchmarkReport _lastReport;
         BenchmarkReport _baselineReport;
         Vector2 _scrollPos;
@@ -211,9 +218,6 @@ namespace CosmicShore.Utility.Tools.Benchmarking
 
             EditorGUILayout.BeginHorizontal();
             GUILayout.Space(12);
-            GUILayout.Label("Warmup (s)", GUILayout.Width(80));
-            _warmupSeconds = EditorGUILayout.FloatField(_warmupSeconds, GUILayout.Width(60));
-            GUILayout.Space(16);
             GUILayout.Label("Duration (s)", GUILayout.Width(80));
             _durationSeconds = EditorGUILayout.FloatField(_durationSeconds, GUILayout.Width(60));
             GUILayout.FlexibleSpace();
@@ -221,8 +225,59 @@ namespace CosmicShore.Utility.Tools.Benchmarking
 
             GUILayout.Space(8);
 
+            // Deterministic mode
+            DrawSection("Deterministic Mode");
+
+            EditorGUILayout.BeginHorizontal();
+            GUILayout.Space(12);
+            _deterministicMode = EditorGUILayout.Toggle(_deterministicMode, GUILayout.Width(16));
+            GUILayout.Label("Enable deterministic mode (removes randomness for repeatable results)");
+            EditorGUILayout.EndHorizontal();
+
```

</details>

### `a38115ef7` — Add automated benchmark session runner with reproducibility analysis

_Claude, 2026-03-08 01:53:09 +0000_

```text
New Session tab in the Benchmark tool that orchestrates multi-iteration
benchmark runs. Loads a selected scene, enters play mode, runs the
benchmark with deterministic settings, saves the report, exits play mode,
and repeats for N iterations. Session state persists across play mode
transitions via EditorPrefs.

After all iterations complete, generates a reproducibility report with
CoV (Coefficient of Variation), min/max spread, and per-iteration
breakdown across avg frame time, FPS, P99, and jank metrics. Includes
a verdict rating (Excellent/Good/Fair/Poor) based on CoV thresholds.
```

```text
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs  |  66 ++++
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs | 157 ++++++++
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs         | 643 +++++++++++++++++++++++++++++++-
 3 files changed, 854 insertions(+), 12 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 989 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs
new file mode 100644
index 000000000..d5611d09c
--- /dev/null
+++ b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs
@@ -0,0 +1,66 @@
+#if UNITY_EDITOR
+
+using System;
+using System.Collections.Generic;
+using UnityEditor;
+using UnityEngine;
+
+namespace CosmicShore.Utility.Tools.Benchmarking
+{
+    /// <summary>
+    /// Persists automated benchmark session state across play mode transitions
+    /// and domain reloads via EditorPrefs JSON.
+    /// </summary>
+    [Serializable]
+    public class BenchmarkSessionConfig
+    {
+        const string EditorPrefsKey = "CosmicShore_BenchmarkSession";
+
+        // ── User Configuration ───────────────────────────────────────────────
+        public string ScenePath = "";
+        public string Label = "Session";
+        public int Iterations = 3;
+        public float DurationSeconds = 15f;
+
+        // ── Deterministic ────────────────────────────────────────────────────
+        public bool Deterministic = true;
+        public int Seed = 42;
+        public int WarmupFrames = 120;
+        public float FixedDt = 0.02f;
+
+        // ── Runtime State ────────────────────────────────────────────────────
+        public bool IsRunning;
+        public int CurrentIteration;
+        public List<string> CompletedReportPaths = new();
+
+        // ── Persistence ──────────────────────────────────────────────────────
+
+        public void Save()
+        {
+            EditorPrefs.SetString(EditorPrefsKey, JsonUtility.ToJson(this));
+        }
+
+        public static BenchmarkSessionConfig Load()
+        {
+            string json = EditorPrefs.GetString(EditorPrefsKey, "");
+            if (string.IsNullOrEmpty(json))
+                return new BenchmarkSessionConfig();
+
+            try
+            {
+                return JsonUtility.FromJson<BenchmarkSessionConfig>(json);
+            }
+            catch
+            {
+                return new BenchmarkSessionConfig();
+            }
+        }
+
+        public static void Clear()
+        {
+            EditorPrefs.DeleteKey(EditorPrefsKey);
+        }
+    }
+}
+
+#endif
diff --git a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs
new file mode 100644
index 000000000..4f22ad0eb
--- /dev/null
+++ b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs
@@ -0,0 +1,157 @@
+using System;
+using System.Collections.Generic;
```

</details>

### `6b0648c4f` — Route benchmark sessions through Arcade bootstrap flow

_Claude, 2026-03-08 01:53:09 +0000_

```text
Session tab now uses GameMode + Vessel dropdowns instead of raw scene
paths. When a session starts, SceneBootstrapper loads Menu_Main as
usual, then the tool calls Arcade.Instance.LaunchArcadeGame() to
launch the game through the proper flow. SceneManager.sceneLoaded
detects when the game scene arrives, then benchmark sampling begins.

This ensures all singletons, GameDataSO, and GameManager initialize
correctly — matching how the game actually runs.
```

```text
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs |  13 +-
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs        | 235 +++++++++++++++++++--------------
 2 files changed, 150 insertions(+), 98 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 451 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs
index d5611d09c..4270c2c94 100644
--- a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs
+++ b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs
@@ -2,6 +2,7 @@
 
 using System;
 using System.Collections.Generic;
+using CosmicShore.Models.Enums;
 using UnityEditor;
 using UnityEngine;
 
@@ -17,11 +18,15 @@ namespace CosmicShore.Utility.Tools.Benchmarking
         const string EditorPrefsKey = "CosmicShore_BenchmarkSession";
 
         // ── User Configuration ───────────────────────────────────────────────
-        public string ScenePath = "";
         public string Label = "Session";
         public int Iterations = 3;
         public float DurationSeconds = 15f;
 
+        // ── Game Launch Configuration ────────────────────────────────────────
+        public GameModes GameMode = GameModes.Freestyle;
+        public VesselClassType Vessel = VesselClassType.Squirrel;
+        public int Intensity = 1;
+
         // ── Deterministic ────────────────────────────────────────────────────
         public bool Deterministic = true;
         public int Seed = 42;
@@ -33,6 +38,12 @@ namespace CosmicShore.Utility.Tools.Benchmarking
         public int CurrentIteration;
         public List<string> CompletedReportPaths = new();
 
+        /// <summary>
+        /// Tracks whether we've already launched the game via Arcade this iteration.
+        /// Reset to false at start of each iteration; set true once Arcade.LaunchArcadeGame runs.
+        /// </summary>
+        public bool GameLaunched;
+
         // ── Persistence ──────────────────────────────────────────────────────
 
         public void Save()
diff --git a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
index 2d6404a8a..b171b17c4 100644
--- a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
+++ b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
@@ -4,8 +4,9 @@ using System;
 using System.IO;
 using System.Collections.Generic;
 using System.Linq;
+using CosmicShore.Core;
+using CosmicShore.Models.Enums;
 using UnityEditor;
-using UnityEditor.SceneManagement;
 using UnityEngine;
 using UnityEngine.SceneManagement;
 
@@ -45,15 +46,44 @@ namespace CosmicShore.Utility.Tools.Benchmarking
 
         // ── Session config (persists across play mode via EditorPrefs) ──────
         [NonSerialized] BenchmarkSessionConfig _session;
-        [NonSerialized] string[] _scenePaths;
-        [NonSerialized] string[] _sceneNames;
-        [NonSerialized] int _selectedSceneIdx;
         [NonSerialized] bool _sessionCallbackRegistered;
+        [NonSerialized] bool _waitingForGameScene;
 
         // ── Session results (loaded after session completes) ────────────────
         [NonSerialized] List<BenchmarkReport> _sessionReports;
         [NonSerialized] BenchmarkSessionSummary _sessionSummary;
 
+        // ── Game mode / vessel names for dropdowns ──────────────────────────
+        static readonly GameModes[] SelectableGameModes =
+        {
+            GameModes.Freestyle,
+            GameModes.WildlifeBlitz,
+            GameModes.CellularDuel,
+            GameModes.Elimination,
+            GameModes.BlockBandit,
+            GameModes.RiskyDriftness,
```

</details>

### `6e63f8ac1` — Point benchmark session at ArcadeGames SO list, cap intensity 1-4

_Claude, 2026-03-08 01:53:10 +0000_

```text
Session tab now loads the active game list from the ArcadeGames
ScriptableObject at editor time, filtering to singleplayer modes with
scenes present in EditorBuildSettings. Game dropdown shows display
names from the SO rather than hardcoded enum values. Intensity slider
capped at 1-4 to match SO_ArcadeGame.MaxIntensity.
```

```text
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs |   1 -
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs        | 112 +++++++++++++++++++++++++--------
 2 files changed, 87 insertions(+), 26 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 177 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs
index 4270c2c94..a9a864255 100644
--- a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs
+++ b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs
@@ -2,7 +2,6 @@
 
 using System;
 using System.Collections.Generic;
-using CosmicShore.Models.Enums;
 using UnityEditor;
 using UnityEngine;
 
diff --git a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
index b171b17c4..9f32df3a6 100644
--- a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
+++ b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
@@ -7,6 +7,7 @@ using System.Linq;
 using CosmicShore.Core;
 using CosmicShore.Models.Enums;
 using UnityEditor;
+using UnityEditor.SceneManagement;
 using UnityEngine;
 using UnityEngine.SceneManagement;
 
@@ -53,20 +54,10 @@ namespace CosmicShore.Utility.Tools.Benchmarking
         [NonSerialized] List<BenchmarkReport> _sessionReports;
         [NonSerialized] BenchmarkSessionSummary _sessionSummary;
 
-        // ── Game mode / vessel names for dropdowns ──────────────────────────
-        static readonly GameModes[] SelectableGameModes =
-        {
-            GameModes.Freestyle,
-            GameModes.WildlifeBlitz,
-            GameModes.CellularDuel,
-            GameModes.Elimination,
-            GameModes.BlockBandit,
-            GameModes.RiskyDriftness,
-            GameModes.Rampage,
-            GameModes.Darts,
-            GameModes.ShootingGallery,
-        };
-        static readonly string[] GameModeNames = SelectableGameModes.Select(m => m.ToString()).ToArray();
+        // ── Arcade game list (loaded from SO at editor time) ─────────────────
+        [NonSerialized] SO_GameList _arcadeGameList;
+        [NonSerialized] SO_ArcadeGame[] _selectableGames;
+        [NonSerialized] string[] _gameDisplayNames;
 
         static readonly VesselClassType[] SelectableVessels =
         {
@@ -125,6 +116,7 @@ namespace CosmicShore.Utility.Tools.Benchmarking
         void OnEnable()
         {
             RefreshSavedReports();
+            RefreshArcadeGameList();
             _session = BenchmarkSessionConfig.Load();
             RegisterSessionCallback();
         }
@@ -502,20 +494,39 @@ namespace CosmicShore.Utility.Tools.Benchmarking
 
             GUILayout.Space(4);
 
-            // Game mode + vessel picker
+            // Game mode from ArcadeGames SO
             DrawSection("Game Configuration");
 
-            int gameModeIdx = Array.IndexOf(SelectableGameModes, _session.GameMode);
-            if (gameModeIdx < 0) gameModeIdx = 0;
+            if (_selectableGames == null || _selectableGames.Length == 0)
+                RefreshArcadeGameList();
 
-            EditorGUILayout.BeginHorizontal();
-            GUILayout.Space(12);
-            GUILayout.Label("Game Mode", GUILayout.Width(100));
-            int newGameModeIdx = EditorGUILayout.Popup(gameModeIdx, GameModeNames);
-            if (newGameModeIdx != gameModeIdx)
-                _session.GameMode = SelectableGameModes[newGameModeIdx];
-            EditorGUILayout.EndHorizontal();
+            if (_selectableGames != null && _selectableGames.Length > 0)
+            {
+                int gameModeIdx = Array.FindIndex(_selectableGames, g => g.Mode == _session.GameMode);
```

</details>

### `eec0c3164` — Use OrganicRematchGames SO instead of ArcadeGames for benchmark list

_Claude, 2026-03-08 01:53:10 +0000_

```text
Points the session tab at the OrganicRematchGames list which is the
actively used game roster. Includes both singleplayer and multiplayer
modes, filtered to those with scenes in build settings. Removed the
singleplayer-only filter since the organic rematch list is the
canonical set of games currently in use.
```

```text
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs | 11 ++++-------
 1 file changed, 4 insertions(+), 7 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
index 9f32df3a6..01ef0df6f 100644
--- a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
+++ b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
@@ -486,7 +486,7 @@ namespace CosmicShore.Utility.Tools.Benchmarking
             EditorGUILayout.HelpBox(
                 "Run a benchmark multiple times through the Arcade bootstrap flow:\n" +
                 "1. Enter play mode (SceneBootstrapper loads Menu_Main)\n" +
-                "2. Arcade.Instance launches the selected game mode + vessel\n" +
+                "2. Arcade launches the selected game from OrganicRematchGames\n" +
                 "3. Once the game scene loads, benchmark sampling begins\n" +
                 "4. After sampling, results are saved and play mode exits\n" +
                 "5. Repeat for N iterations, then generate reproducibility report",
@@ -1374,13 +1374,13 @@ namespace CosmicShore.Utility.Tools.Benchmarking
 
         void RefreshArcadeGameList()
         {
-            // Find the ArcadeGames SO asset
+            // Find the OrganicRematchGames SO asset (the active game list)
             string[] guids = AssetDatabase.FindAssets("t:SO_GameList", new[] { "Assets/_SO_Assets" });
             foreach (string guid in guids)
             {
                 string path = AssetDatabase.GUIDToAssetPath(guid);
                 var list = AssetDatabase.LoadAssetAtPath<SO_GameList>(path);
-                if (list != null && list.name == "ArcadeGames")
+                if (list != null && list.name == "OrganicRematchGames")
                 {
                     _arcadeGameList = list;
                     break;
@@ -1402,14 +1402,11 @@ namespace CosmicShore.Utility.Tools.Benchmarking
                     buildSceneNames.Add(System.IO.Path.GetFileNameWithoutExtension(scene.path));
             }
 
-            // Filter to singleplayer games with scenes in build settings
+            // Include games with scenes in build settings
             var valid = new List<SO_ArcadeGame>();
             foreach (var game in _arcadeGameList.Games)
             {
                 if (game == null) continue;
-                // Skip multiplayer-only modes for benchmarking
-                if (game.IsMultiplayer) continue;
-                // Only include if the scene exists in build settings
                 if (buildSceneNames.Contains(game.SceneName))
                     valid.Add(game);
             }
```

</details>

### `67d3d31d2` — Press Go button and sample 20s of actual gameplay in benchmark sessions

_Claude, 2026-03-08 01:53:10 +0000_

```text
After the game scene loads, the benchmark now:
1. Finds the MiniGameControllerBase and calls OnReadyClicked()
2. Waits 5s for the countdown (4 sprites × 1s + 1s buffer)
3. Starts sampling 20s of actual gameplay performance

This ensures benchmarks capture real gameplay with the vessel flying
around, not just a static scene waiting at the ready screen. The
session tab UI shows countdown progress during the wait phase.
```

```text
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs |  2 +-
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs        | 83 +++++++++++++++++++++++++++++-----
 2 files changed, 73 insertions(+), 12 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 152 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs
index a9a864255..dde44311d 100644
--- a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs
+++ b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs
@@ -19,7 +19,7 @@ namespace CosmicShore.Utility.Tools.Benchmarking
         // ── User Configuration ───────────────────────────────────────────────
         public string Label = "Session";
         public int Iterations = 3;
-        public float DurationSeconds = 15f;
+        public float DurationSeconds = 20f;
 
         // ── Game Launch Configuration ────────────────────────────────────────
         public GameModes GameMode = GameModes.Freestyle;
diff --git a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
index 01ef0df6f..da5f0bc19 100644
--- a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
+++ b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
@@ -5,6 +5,7 @@ using System.IO;
 using System.Collections.Generic;
 using System.Linq;
 using CosmicShore.Core;
+using CosmicShore.Game.Arcade;
 using CosmicShore.Models.Enums;
 using UnityEditor;
 using UnityEditor.SceneManagement;
@@ -49,6 +50,11 @@ namespace CosmicShore.Utility.Tools.Benchmarking
         [NonSerialized] BenchmarkSessionConfig _session;
         [NonSerialized] bool _sessionCallbackRegistered;
         [NonSerialized] bool _waitingForGameScene;
+        [NonSerialized] bool _waitingForCountdown;
+        [NonSerialized] double _countdownWaitEndTime;
+
+        // Countdown is 4 sprites × 1s each; add 1s buffer for scene init before pressing Go
+        const float CountdownDurationSeconds = 5f;
 
         // ── Session results (loaded after session completes) ────────────────
         [NonSerialized] List<BenchmarkReport> _sessionReports;
@@ -452,9 +458,17 @@ namespace CosmicShore.Utility.Tools.Benchmarking
 
                 if (Application.isPlaying)
                 {
-                    string phase = _waitingForGameScene
-                        ? "Waiting for game scene to load via Arcade..."
-                        : "Benchmark running...";
+                    string phase;
+                    if (_waitingForGameScene)
+                        phase = "Waiting for game scene to load via Arcade...";
+                    else if (_waitingForCountdown)
+                    {
+                        float remaining = Mathf.Max(0f, (float)(_countdownWaitEndTime - EditorApplication.timeSinceStartup));
+                        phase = $"Go button pressed — countdown {remaining:F1}s remaining...";
+                    }
+                    else
+                        phase = "Sampling gameplay...";
+
                     EditorGUILayout.BeginHorizontal();
                     GUILayout.Space(12);
                     GUILayout.Label(phase);
@@ -487,9 +501,10 @@ namespace CosmicShore.Utility.Tools.Benchmarking
                 "Run a benchmark multiple times through the Arcade bootstrap flow:\n" +
                 "1. Enter play mode (SceneBootstrapper loads Menu_Main)\n" +
                 "2. Arcade launches the selected game from OrganicRematchGames\n" +
-                "3. Once the game scene loads, benchmark sampling begins\n" +
-                "4. After sampling, results are saved and play mode exits\n" +
-                "5. Repeat for N iterations, then generate reproducibility report",
+                "3. Once the game scene loads, the Go button is pressed automatically\n" +
+                "4. After the countdown, benchmark sampling captures 20s of gameplay\n" +
+                "5. After sampling, results are saved and play mode exits\n" +
+                "6. Repeat for N iterations, then generate reproducibility report",
                 MessageType.None);
 
             GUILayout.Space(4);
@@ -1222,10 +1237,52 @@ namespace CosmicShore.Utility.Tools.Benchmarking
             SceneManager.sceneLoaded -= OnGameSceneLoaded;
             _waitingForGameScene = false;
 
-            CSDebug.Log($"[Benchmark Session] Game scene '{scene.name}' loaded. Starting benchmark...");
+            CSDebug.Log($"[Benchmark Session] Game scene '{scene.name}' loaded. Pressing Go button...");
+
+            // The game scene is loaded — wait a frame for initialization, then press Go
```

</details>

### `c0cd698dc` — Fix Go button timing: wait for scene init before pressing Go

_Claude, 2026-03-08 01:53:10 +0000_

```text
The previous approach used EditorApplication.delayCall which can fire
before Start() has run on the game controller, meaning players haven't
spawned yet when Go is pressed. Now uses EditorApplication.update with
a 3-second timer after scene load to ensure full initialization
(Start, InitializeGame, player spawning) completes before pressing Go.

Also adds a fallback: if no MiniGameControllerBase is found after the
init delay, starts the benchmark anyway instead of retrying forever.
```

```text
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs | 81 ++++++++++++++++++++++++++---------------
 1 file changed, 51 insertions(+), 30 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 130 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
index da5f0bc19..5a581da11 100644
--- a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
+++ b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
@@ -53,8 +53,10 @@ namespace CosmicShore.Utility.Tools.Benchmarking
         [NonSerialized] bool _waitingForCountdown;
         [NonSerialized] double _countdownWaitEndTime;
 
-        // Countdown is 4 sprites × 1s each; add 1s buffer for scene init before pressing Go
-        const float CountdownDurationSeconds = 5f;
+        // Countdown is 4 sprites × 1s each
+        const float CountdownDurationSeconds = 4f;
+        // Wait for scene to fully initialize (Start(), player spawning, etc.) before pressing Go
+        const float SceneInitDelaySeconds = 3f;
 
         // ── Session results (loaded after session completes) ────────────────
         [NonSerialized] List<BenchmarkReport> _sessionReports;
@@ -461,6 +463,11 @@ namespace CosmicShore.Utility.Tools.Benchmarking
                     string phase;
                     if (_waitingForGameScene)
                         phase = "Waiting for game scene to load via Arcade...";
+                    else if (!_goButtonPressed && _sceneLoadedTime > 0)
+                    {
+                        float remaining = Mathf.Max(0f, SceneInitDelaySeconds - (float)(EditorApplication.timeSinceStartup - _sceneLoadedTime));
+                        phase = $"Waiting for scene initialization... {remaining:F1}s";
+                    }
                     else if (_waitingForCountdown)
                     {
                         float remaining = Mathf.Max(0f, (float)(_countdownWaitEndTime - EditorApplication.timeSinceStartup));
@@ -1237,48 +1244,62 @@ namespace CosmicShore.Utility.Tools.Benchmarking
             SceneManager.sceneLoaded -= OnGameSceneLoaded;
             _waitingForGameScene = false;
 
-            CSDebug.Log($"[Benchmark Session] Game scene '{scene.name}' loaded. Pressing Go button...");
+            CSDebug.Log($"[Benchmark Session] Game scene '{scene.name}' loaded. Waiting {SceneInitDelaySeconds}s for initialization...");
 
-            // The game scene is loaded — wait a frame for initialization, then press Go
-            EditorApplication.delayCall += PressGoButton;
+            // Use EditorApplication.update with a timer to wait for full scene initialization
+            // (Start(), player spawning, etc.) before pressing Go. delayCall can fire before
+            // the game loop ticks, which breaks everything.
+            _goButtonPressed = false;
+            _sceneLoadedTime = EditorApplication.timeSinceStartup;
+            EditorApplication.update += OnWaitForGameReady;
         }
 
-        void PressGoButton()
-        {
-            if (!Application.isPlaying) return;
+        [NonSerialized] bool _goButtonPressed;
+        [NonSerialized] double _sceneLoadedTime;
 
-            // Find the MiniGameControllerBase in the scene and press Go
-            var controller = UnityEngine.Object.FindAnyObjectByType<MiniGameControllerBase>();
-            if (controller == null)
+        void OnWaitForGameReady()
+        {
+            if (!Application.isPlaying)
             {
-                // Controller not ready yet — retry next frame
-                EditorApplication.delayCall += PressGoButton;
+                EditorApplication.update -= OnWaitForGameReady;
+                _waitingForCountdown = false;
                 return;
             }
 
-            controller.OnReadyClicked();
-            CSDebug.Log("[Benchmark Session] Go button pressed. Waiting for countdown to finish...");
-
-            // Wait for the countdown to complete before starting the benchmark sampler
-            _waitingForCountdown = true;
-            _countdownWaitEndTime = EditorApplication.timeSinceStartup + CountdownDurationSeconds;
-            EditorApplication.update += OnCountdownWaitUpdate;
-        }
+            double elapsed = EditorApplication.timeSinceStartup - _sceneLoadedTime;
 
-        void OnCountdownWaitUpdate()
-        {
-            if (!Application.isPlaying)
+            // Phase 1: Wait for scene to fully initialize before pressing Go
```

</details>

### `436f0f34b` — Fix benchmark Go button timing: increase init delay from 3s to 8s

_Claude, 2026-03-08 01:53:10 +0000_

```text
MiniGameHUD.minConnectingSeconds is 5s — the Ready button doesn't appear
until after that connecting panel completes. The previous 3s delay meant
OnReadyClicked() fired while the connecting panel was still showing,
before the game was ready to receive it.

Also changed controller-not-found from immediate fallback to retry loop
with 10s additional timeout, so transient init delays don't skip the
Go button press entirely.
```

```text
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs | 12 +++++++-----
 1 file changed, 7 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
index 5a581da11..1b2886bef 100644
--- a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
+++ b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
@@ -55,8 +55,9 @@ namespace CosmicShore.Utility.Tools.Benchmarking
 
         // Countdown is 4 sprites × 1s each
         const float CountdownDurationSeconds = 4f;
-        // Wait for scene to fully initialize (Start(), player spawning, etc.) before pressing Go
-        const float SceneInitDelaySeconds = 3f;
+        // MiniGameHUD.minConnectingSeconds is 5s, then Ready button appears.
+        // Wait long enough for scene init + connecting panel + button appearance.
+        const float SceneInitDelaySeconds = 8f;
 
         // ── Session results (loaded after session completes) ────────────────
         [NonSerialized] List<BenchmarkReport> _sessionReports;
@@ -1283,14 +1284,15 @@ namespace CosmicShore.Utility.Tools.Benchmarking
                     _countdownWaitEndTime = EditorApplication.timeSinceStartup + CountdownDurationSeconds;
                     CSDebug.Log("[Benchmark Session] Go button pressed. Waiting for countdown...");
                 }
-                else
+                else if (elapsed > SceneInitDelaySeconds + 10f)
                 {
-                    // Controller not found — fall back to starting benchmark without Go
-                    CSDebug.Log("[Benchmark Session] No MiniGameControllerBase found. Starting benchmark without Go press.");
+                    // Controller still not found after extended wait — fall back
+                    CSDebug.Log("[Benchmark Session] No MiniGameControllerBase found after extended wait. Starting benchmark without Go press.");
                     EditorApplication.update -= OnWaitForGameReady;
                     _waitingForCountdown = false;
                     AutoStartSessionBenchmark();
                 }
+                // else: keep polling each editor update until controller appears or timeout
                 return;
             }
 
```

</details>

### `85382938b` — Add Main Menu benchmark target to session benchmark tool

_Claude, 2026-03-08 01:53:10 +0000_

```text
Adds a BenchmarkTarget enum (Game / MainMenu) to the Session tab so users
can benchmark the Menu_Main scene directly without launching a game through
Arcade. The MainMenu flow enters play mode, waits a configurable init delay
for the menu to stabilize, then samples performance — skipping the Arcade
launch, Go button, and countdown phases. All existing Game target behavior
is preserved unchanged.
```

```text
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs |  18 +++
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs        | 196 ++++++++++++++++++++++++---------
 2 files changed, 163 insertions(+), 51 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 312 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs
index dde44311d..96c4f57a6 100644
--- a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs
+++ b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs
@@ -7,6 +7,17 @@ using UnityEngine;
 
 namespace CosmicShore.Utility.Tools.Benchmarking
 {
+    /// <summary>
+    /// What scene/flow to benchmark in an automated session.
+    /// </summary>
+    public enum BenchmarkTarget
+    {
+        /// <summary>Benchmark a gameplay scene launched via the Arcade flow.</summary>
+        Game = 0,
+        /// <summary>Benchmark the Main Menu scene directly (no game launch).</summary>
+        MainMenu = 1,
+    }
+
     /// <summary>
     /// Persists automated benchmark session state across play mode transitions
     /// and domain reloads via EditorPrefs JSON.
@@ -21,6 +32,13 @@ namespace CosmicShore.Utility.Tools.Benchmarking
         public int Iterations = 3;
         public float DurationSeconds = 20f;
 
+        // ── Benchmark Target ─────────────────────────────────────────────────
+        public BenchmarkTarget Target = BenchmarkTarget.Game;
+
+        // ── Main Menu Configuration ──────────────────────────────────────────
+        /// <summary>Seconds to wait for Menu_Main to fully initialize before sampling.</summary>
+        public float MenuInitDelaySeconds = 5f;
+
         // ── Game Launch Configuration ────────────────────────────────────────
         public GameModes GameMode = GameModes.Freestyle;
         public VesselClassType Vessel = VesselClassType.Squirrel;
diff --git a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
index 1b2886bef..0ab3f2608 100644
--- a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
+++ b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs
@@ -435,7 +435,10 @@ namespace CosmicShore.Utility.Tools.Benchmarking
 
                 EditorGUILayout.BeginHorizontal();
                 GUILayout.Space(12);
-                GUILayout.Label($"Mode: {_session.GameMode}   |   Vessel: {_session.Vessel}   |   Duration: {_session.DurationSeconds}s");
+                if (_session.Target == BenchmarkTarget.MainMenu)
+                    GUILayout.Label($"Target: Main Menu   |   Duration: {_session.DurationSeconds}s");
+                else
+                    GUILayout.Label($"Mode: {_session.GameMode}   |   Vessel: {_session.Vessel}   |   Duration: {_session.DurationSeconds}s");
                 EditorGUILayout.EndHorizontal();
 
                 // Overall progress bar
@@ -462,7 +465,17 @@ namespace CosmicShore.Utility.Tools.Benchmarking
                 if (Application.isPlaying)
                 {
                     string phase;
-                    if (_waitingForGameScene)
+                    if (_session.Target == BenchmarkTarget.MainMenu)
+                    {
+                        if (_waitingForMenuInit)
+                        {
+                            float remaining = Mathf.Max(0f, (float)(_menuInitEndTime - EditorApplication.timeSinceStartup));
+                            phase = $"Waiting for menu initialization... {remaining:F1}s";
+                        }
+                        else
+                            phase = "Sampling Main Menu...";
+                    }
+                    else if (_waitingForGameScene)
                         phase = "Waiting for game scene to load via Arcade...";
                     else if (!_goButtonPressed && _sceneLoadedTime > 0)
                     {
@@ -505,68 +518,109 @@ namespace CosmicShore.Utility.Tools.Benchmarking
             // ── Configuration ────────────────────────────────────────────────
             DrawSection("Automated Session");
 
-            EditorGUILayout.HelpBox(
-                "Run a benchmark multiple times through the Arcade bootstrap flow:\n" +
-                "1. Enter play mode (SceneBootstrapper loads Menu_Main)\n" +
-                "2. Arcade launches the selected game from OrganicRematchGames\n" +
-                "3. Once the game scene loads, the Go button is pressed automatically\n" +
```

</details>

### `e9d01cce5` — Fix vessel parameter being ignored in Arcade.LaunchArcadeGame and LaunchTrainingGame

_Claude, 2026-03-08 20:29:11 +0000_

```text
When the Arcade launch methods were refactored from MiniGame to gameData,
the vessel parameter stopped being applied — the old MiniGame.PlayerShipType
assignment was commented out but never replaced with
gameData.selectedVesselClass.Value. This caused the benchmark tool (and any
other caller of these methods) to always launch with whatever vessel was
previously selected in the UI rather than the one passed as a parameter.

Also fix LaunchTrainingGame not setting gameData.GameMode.
```

```text
 Assets/_Scripts/Game/Managers/Arcade.cs | 3 +++
 1 file changed, 3 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Managers/Arcade.cs b/Assets/_Scripts/Game/Managers/Arcade.cs
index 08980dd8f..4e8435ed2 100644
--- a/Assets/_Scripts/Game/Managers/Arcade.cs
+++ b/Assets/_Scripts/Game/Managers/Arcade.cs
@@ -89,6 +89,7 @@ namespace CosmicShore.Core
             gameData.IsTraining = false;
             gameData.IsMission = false;
             gameData.GameMode = gameMode;
+            gameData.selectedVesselClass.Value = vessel;
             
             // For multiplayer-capable games with only 1 human player, run locally with AI
             // instead of doing online matchmaking. Use gameData.SelectedPlayerCount (set by
@@ -143,6 +144,8 @@ namespace CosmicShore.Core
             gameData.IsTraining = !isDailyChallenge;
             gameData.IsMission = false;
             gameData.IsMultiplayerMode = false;
+            gameData.GameMode = gameMode;
+            gameData.selectedVesselClass.Value = vessel;
             gameData.SceneName = TrainingGameLookup[gameMode].Game.SceneName;
             gameData.InvokeGameLaunch();
             
```

</details>

_Also contains 2 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
