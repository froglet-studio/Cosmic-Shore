# Branch archive: `claude/merge-bleeding-edge-master-Gt2By`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

## What this branch is

**Merge bleeding-edge into master**

A sync branch whose only own commit is a merge of bleeding-edge into master, carrying along older shared history (sound tweaks, mobile input and build fixes). It contains no feature work of its own.

- **Status:** Sync / merge branch
- **Areas:** branch sync
- **Already in bleeding-edge:** Content is a merge; the shared commits are the same old history found on several other branches. No unique feature work.
- **Risk if deleted:** low
- **Suggestion (2026-10-08):** can be deleted after archiving — It is a one-off merge branch with no unique work.

## Evidence

- **Last commit:** 2026-04-30 by Claude
- **Unmerged commits:** 13
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/505
- **Forked from:** `807d502fb` (2026-04-30, Merge branch 'bleeding-edge' into claude/fix-squirrel-stats-display-Il9I9)
- **Tip:** `e7e083283`
- **Files touched (35):**
  - `Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableEvents/ScriptableEvent.cs`
  - `Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableVariables/VariableReferences/VariableReference.cs`
  - `Assets/Unity Assests/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Electronic Highway Sign SDF.asset`
  - `Assets/_Audio/Sounds/Charles SFX.meta`
  - `Assets/_Audio/Sounds/Charles SFX/Click Cosmic shore.wav`
  - `Assets/_Audio/Sounds/Charles SFX/Click Cosmic shore.wav.meta`
  - `Assets/_Audio/Sounds/Charles SFX/Cosmic shore Boost.wav`
  - `Assets/_Audio/Sounds/Charles SFX/Cosmic shore Boost.wav.meta`
  - `Assets/_Audio/Sounds/Charles SFX/Crystal_Collision_Amplified.wav`
  - `Assets/_Audio/Sounds/Charles SFX/Crystal_Collision_Amplified.wav.meta`
  - `Assets/_Audio/Sounds/Charles SFX/cosmic shore laser.wav`
  - `Assets/_Audio/Sounds/Charles SFX/cosmic shore laser.wav.meta`
  - `Assets/_Audio/Sounds/Charles SFX/drift let go cosmic shore.wav`
  - `Assets/_Audio/Sounds/Charles SFX/drift let go cosmic shore.wav.meta`
  - `Assets/_Audio/Sounds/Charles SFX/explosion.wav`
  - `Assets/_Audio/Sounds/Charles SFX/explosion.wav.meta`
  - `Assets/_Scenes/Menu_Main.unity`
  - `Assets/_Scripts/App/UI/ScreenSwitcher.cs`
  - `Assets/_Scripts/Game/IO/BaseInputStrategy.cs`
  - `Assets/_Scripts/Game/IO/ControllerButtonPress.cs`
  - `Assets/_Scripts/Game/IO/IInputStatus.cs`
  - `Assets/_Scripts/Game/IO/InputController.cs`
  - `Assets/_Scripts/Game/IO/InputStatus.cs`
  - `Assets/_Scripts/Game/IO/TouchInputStrategy.cs`
  - `Assets/_Scripts/Game/UI/Editor.meta`
  - `Assets/_Scripts/Game/UI/Editor/ActiveGameModesWindow.cs`
  - `Assets/_Scripts/Game/UI/Editor/ActiveGameModesWindow.cs.meta`
  - `Assets/_Scripts/Game/UI/Editor/LeaderboardConfigSOEditor.cs`
  - `Assets/_Scripts/Game/UI/Editor/LeaderboardConfigSOEditor.cs.meta`
  - `Assets/_Scripts/Game/UI/HexRaceHUDView.cs`
  - `Assets/_Scripts/Models/Enums/InputDeviceType.cs`
  - `Assets/_Scripts/Models/Enums/InputDeviceType.cs.meta`
  - `Assets/_Scripts/Utility/CSDebug.cs`
  - `Assets/_Scripts/Utility/CSDebug.cs.meta`
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

### `8ae42f2c6` — Changed some sounds

_aradia1, 2026-04-07 13:57:07 -0400_

```text
changed crystal impact and boost sounds to what i demoed last week.
```

```text
 .../Resources/Fonts & Materials/Electronic Highway Sign SDF.asset     | 289 ++------------------------------
 Assets/_Audio/Sounds/Charles SFX.meta                                 |   8 +
 Assets/_Audio/Sounds/Charles SFX/Cosmic shore Boost.wav               | Bin 0 -> 216080 bytes
 Assets/_Audio/Sounds/Charles SFX/Cosmic shore Boost.wav.meta          |  23 +++
 Assets/_Audio/Sounds/Charles SFX/Crystal_Collision_Amplified.wav      | Bin 0 -> 432080 bytes
 Assets/_Audio/Sounds/Charles SFX/Crystal_Collision_Amplified.wav.meta |  23 +++
 Assets/_Audio/Sounds/Charles SFX/drift let go cosmic shore.wav        | Bin 0 -> 1152080 bytes
 Assets/_Audio/Sounds/Charles SFX/drift let go cosmic shore.wav.meta   |  23 +++
 Assets/_Scenes/Menu_Main.unity                                        |  30 ++++
 Assets/_Scripts/Game/UI/Editor.meta                                   |   8 +
 10 files changed, 125 insertions(+), 279 deletions(-)
```

### `b57614aae` — Changed projectile SFX

_aradia1, 2026-04-07 14:20:27 -0400_

```text
Projectile SFX was originally the fly by. made a noise more suited for a laser
```

```text
 Assets/_Audio/Sounds/Charles SFX/Click Cosmic shore.wav      | Bin 0 -> 721682 bytes
 Assets/_Audio/Sounds/Charles SFX/Click Cosmic shore.wav.meta |  23 +++++++++++++++++++++++
 Assets/_Audio/Sounds/Charles SFX/cosmic shore laser.wav      | Bin 0 -> 198080 bytes
 Assets/_Audio/Sounds/Charles SFX/cosmic shore laser.wav.meta |  23 +++++++++++++++++++++++
 Assets/_Audio/Sounds/Charles SFX/explosion.wav               | Bin 0 -> 1060524 bytes
 Assets/_Audio/Sounds/Charles SFX/explosion.wav.meta          |  23 +++++++++++++++++++++++
 Assets/_Scenes/Menu_Main.unity                               |  10 ++++++++++
 7 files changed, 79 insertions(+)
```

_Also contains 3 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
