# Branch archive: `claude/sparrow-missile-launch-animation-xHuCS`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

<!-- SUMMARY -->

- **Last commit:** 2026-04-09 by Braden Hamilton
- **Unmerged commits:** 26
- **Forked from:** `14a193123` (2026-03-07, Merge pull request #414 from froglet-studio/claude/migrate-debug-logs-fNeXt)
- **Tip:** `12a1d0eab`
- **Files touched (45):**
  - `Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableEvents/ScriptableEvent.cs`
  - `Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableVariables/VariableReferences/VariableReference.cs`
  - `Assets/Unity Assests/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Electronic Highway Sign SDF.asset`
  - `Assets/_Animations/SparrowAnimatorController.controller`
  - `Assets/_Graphics/Design Assests/FX/fx_arclightning.mat`
  - `Assets/_Graphics/Materials/Graphs/ShipGraph.shadergraph`
  - `Assets/_Models/Ship Models/SparrowModel3.fbx`
  - `Assets/_Models/Ship Models/SparrowModel3.fbx.meta`
  - `Assets/_Models/Ship Models/SparrowModel4.fbx`
  - `Assets/_Models/Ship Models/SparrowModel4.fbx.meta`
  - `Assets/_Models/Sparrow Missile.fbx`
  - `Assets/_Models/Sparrow Missile.fbx.meta`
  - `Assets/_Prefabs/Projectile/SkyBurstProjectile.prefab`
  - `Assets/_Prefabs/Spaceships/Manta.prefab`
  - `Assets/_Prefabs/Spaceships/Sparrow.prefab`
  - `Assets/_SO_Assets/Camera/SparrowCameraSettingsSO.asset`
  - `Assets/_Scripts/App/UI/ScreenSwitcher.cs`
  - `Assets/_Scripts/Game/Animation/MantaAnimationContoller.cs`
  - `Assets/_Scripts/Game/Animation/SparrowAnimationController.cs`
  - `Assets/_Scripts/Game/IO/BaseInputStrategy.cs`
  - `Assets/_Scripts/Game/IO/ControllerButtonPress.cs`
  - `Assets/_Scripts/Game/IO/IInputStatus.cs`
  - `Assets/_Scripts/Game/IO/InputController.cs`
  - `Assets/_Scripts/Game/IO/InputStatus.cs`
  - `Assets/_Scripts/Game/IO/TouchInputStrategy.cs`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FireGunActionExecutor.cs`
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
  - `Assets/_Scripts/Utility/MobilePerformanceManager.cs.meta`
  - `Assets/_Scripts/Utility/Tools/Benchmarking.meta`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkReport.cs.meta`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs.meta`
  - … and 5 more

### `d6656eaa2` — Fix Sparrow prefab: pull missile mechanics from sparrow-missile-mechanics-zCche

_Claude, 2026-03-07 23:09:12 +0000_

```text
Brings over all Sparrow missile animation work from the user's feature branch:

- FireGunActionExecutor: add OnMissileFired(ammoBeforeFire, ammoCost) event fired
  before ammo is consumed so SparrowAnimationController can pick the correct clip,
  plus OnShotFired static event for telemetry
- SparrowAnimationController: wire Initialize/OnDestroy lifecycle, subscribe to
  OnMissileFired, play "Launch Missile 1" or "Launch Missile 2" on layer 1 with
  proper SetLayerWeight calls and a coroutine to reset the layer after the clip ends
- SparrowAnimatorController.controller: restore the Missile Launch layer (layer 1)
  with "Launch Missile 1" and "Launch Missile 2" states and correct animation GUIDs
  pointing at SparrowModel3
- Sparrow.prefab: bring in position reset (0,0,0), updated model instance to
  SparrowModel3, SkinnedMeshRenderer and animator references wired up
- SparrowModel3.fbx / SparrowModel3.fbx.meta: add the model asset that the prefab
  and animator controller target
```

```text
 Assets/_Animations/SparrowAnimatorController.controller               |  91 ++++++
 Assets/_Models/Ship Models/SparrowModel3.fbx                          | Bin 0 -> 6021116 bytes
 Assets/_Models/Ship Models/SparrowModel3.fbx.meta                     | 528 +++++++++++++++++++++++++++++++
 Assets/_Prefabs/Spaceships/Sparrow.prefab                             | 534 +++++---------------------------
 Assets/_Scripts/Game/Animation/SparrowAnimationController.cs          |  37 ++-
 .../Game/Ship/R_ShipActions/Executors/FireGunActionExecutor.cs        |   9 +-
 6 files changed, 737 insertions(+), 462 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 110 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs b/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
index bf5f0427a..0734d2896 100644
--- a/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
+++ b/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
@@ -1,3 +1,4 @@
+using System.Collections;
 using UnityEngine;
 
 namespace CosmicShore.Game.Animation
@@ -5,6 +6,9 @@ namespace CosmicShore.Game.Animation
     class SparrowAnimationController : VesselAnimation
     {
         [SerializeField] Animator animator;
+        [SerializeField] FireGunActionExecutor missileExecutor;
+
+        const int MissileLaunchLayer = 1;
 
         float currentPitch = 0;
         float currentYaw = 0;
@@ -12,6 +16,35 @@ namespace CosmicShore.Game.Animation
         float currentThrottle = 0;
         float animationSpeed = 3.25f;
 
+        public override void Initialize(IVesselStatus vesselStatus)
+        {
+            base.Initialize(vesselStatus);
+            if (missileExecutor != null)
+                missileExecutor.OnMissileFired += HandleMissileFired;
+        }
+
+        void OnDestroy()
+        {
+            if (missileExecutor != null)
+                missileExecutor.OnMissileFired -= HandleMissileFired;
+        }
+
+        void HandleMissileFired(float ammoBeforeFire, float ammoCost)
+        {
+            var animName = ammoBeforeFire >= 2f * ammoCost ? "Launch Missile 1" : "Launch Missile 2";
+            animator.SetLayerWeight(MissileLaunchLayer, 1f);
+            animator.Play(animName, MissileLaunchLayer);
+            StartCoroutine(ResetMissileLaunchLayer());
+        }
+
+        IEnumerator ResetMissileLaunchLayer()
+        {
+            yield return null; // wait one frame for the animator to enter the new state
+            while (animator.GetCurrentAnimatorStateInfo(MissileLaunchLayer).normalizedTime < 1f)
+                yield return null;
+            animator.SetLayerWeight(MissileLaunchLayer, 0f);
+        }
+
         protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
         {
 
@@ -25,7 +58,7 @@ namespace CosmicShore.Game.Animation
             animator.SetFloat("Yaw", currentYaw);
             animator.SetFloat("Roll", currentRoll);
             animator.SetFloat("Throttle", currentThrottle);
-            
+
         }
 
         protected override void Idle()
@@ -46,4 +79,4 @@ namespace CosmicShore.Game.Animation
 
         protected override void AssignTransforms() { /* NOOP Abstract Implementation */ }
     }
-}
\ No newline at end of file
+}
diff --git a/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FireGunActionExecutor.cs b/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FireGunActionExecutor.cs
index 69d6c535f..d78a0e815 100644
--- a/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FireGunActionExecutor.cs
+++ b/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FireGunActionExecutor.cs
@@ -8,8 +8,12 @@ using UnityEngine;
 
 public class FireGunActionExecutor : ShipActionExecutorBase
 {
+    /// <summary>Static event: each time a gun fires a single shot. Param = player name.</summary>
```

</details>

### `a0f0ce308` — Fix Sparrow prefab: replace MantaAnimationController with SparrowAnimationController

_Claude, 2026-03-08 00:02:38 +0000_

```text
- Swap MantaAnimationController (wrong vessel) for SparrowAnimationController
- Add stripped Animator entry for the new SparrowModel3 FBX (guid 1b0725c5c79aad145835d47b9ecfa510)
- Wire animator field to stripped Animator (fileID 8796843700321160693)
- Wire missileExecutor field to FireGunActionExecutor (fileID 4658886838346608747)
- Restore _shipGeometries[0] to SparrowModel3 root GameObject (fileID 2117239821978650842)

The d6656eaa commit swapped the FBX but left MantaAnimationController in place with
null references, breaking missile animations and geometry caching.
```

```text
 Assets/_Prefabs/Spaceships/Sparrow.prefab | 18 ++++++++++++------
 1 file changed, 12 insertions(+), 6 deletions(-)
```

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

### `5267f456c` — Fix SparrowAnimationController: add public modifier, fix prefab YAML whitespace

_Claude, 2026-03-08 00:27:03 +0000_

```text
- Add `public` to SparrowAnimationController class (Unity requires public MonoBehaviours
  for reliable serialization)
- Restore trailing spaces on m_Name and m_EditorClassIdentifier in prefab YAML
  (null vs empty string difference in Unity's YAML parser)
```

```text
 Assets/_Prefabs/Spaceships/Sparrow.prefab                    | 4 ++--
 Assets/_Scripts/Game/Animation/SparrowAnimationController.cs | 2 +-
 2 files changed, 3 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs b/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
index 0734d2896..45504c6e5 100644
--- a/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
+++ b/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
@@ -3,7 +3,7 @@ using UnityEngine;
 
 namespace CosmicShore.Game.Animation
 {
-    class SparrowAnimationController : VesselAnimation
+    public class SparrowAnimationController : VesselAnimation
     {
         [SerializeField] Animator animator;
         [SerializeField] FireGunActionExecutor missileExecutor;
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

### `c78b93dbb` — Import Sparrow prefab from claude/sparrow-missile-mechanics-zCche

_Claude, 2026-03-08 00:31:54 +0000_

```text
 Assets/_Prefabs/Spaceships/Sparrow.prefab | 92 ++++---------------------------------------------------------
 1 file changed, 6 insertions(+), 86 deletions(-)
```

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

### `c778dc86b` — Make Sparrow visible in game Set up missile shooting animations

_Braden Hamilton, 2026-03-14 21:22:53 -0500_

```text
 .../Resources/Fonts & Materials/Electronic Highway Sign SDF.asset     | 72 ++++++++++++++++++++++++++++-----
 Assets/_Graphics/Materials/Graphs/ShipGraph.shadergraph               |  4 +-
 Assets/_Prefabs/Spaceships/Manta.prefab                               | 41 +++++++++++--------
 Assets/_Prefabs/Spaceships/Sparrow.prefab                             | 49 ++++++++++++++++++----
 Assets/_Scripts/Utility/MobilePerformanceManager.cs.meta              |  2 +
 Assets/_Scripts/Utility/Tools/Benchmarking.meta                       |  8 ++++
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkReport.cs.meta    |  2 +
 .../Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs.meta         |  2 +
 .../Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs.meta        |  2 +
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs.meta    |  2 +
 .../Tools/Benchmarking/DeterministicBenchmarkController.cs.meta       |  2 +
 Assets/_Scripts/Utility/Tools/Benchmarking/PerformanceSampler.cs.meta |  2 +
 ProjectSettings/ProjectSettings.asset                                 | 44 ++++++++++++++++++++
 13 files changed, 196 insertions(+), 36 deletions(-)
```

### `0ab2c33c4` — Set up Missile shooting animations Remake missle shape

_Braden Hamilton, 2026-03-19 16:46:22 -0500_

```text
 .../Resources/Fonts & Materials/Electronic Highway Sign SDF.asset     | 72 +++++----------------------------
 Assets/_Models/Ship Models/SparrowModel3.fbx.meta                     |  2 +-
 Assets/_Prefabs/Spaceships/Sparrow.prefab                             | 35 ++++++++++++----
 Assets/_SO_Assets/Camera/SparrowCameraSettingsSO.asset                | 13 +++---
 4 files changed, 44 insertions(+), 78 deletions(-)
```

### `8e13aad81` — Set Up Missile shooting animations

_Braden Hamilton, 2026-03-19 17:39:25 -0500_

```text
 Assets/_Animations/SparrowAnimatorController.controller |  158 +-
 Assets/_Graphics/Design Assests/FX/fx_arclightning.mat  |    1 +
 Assets/_Models/Sparrow Missile.fbx                      |  Bin 0 -> 57916 bytes
 Assets/_Models/Sparrow Missile.fbx.meta                 |  107 +
 Assets/_Prefabs/Projectile/SkyBurstProjectile.prefab    | 4984 ++++++++++++++++++++++++++++++++++++++++++++-
 5 files changed, 5167 insertions(+), 83 deletions(-)
```

### `b8bddd01d` — Fix SparrowAnimationController: use correct animator state names for missile launch

_Claude, 2026-03-22 23:34:26 +0000_

```text
The state names in SparrowAnimatorController.controller are "Missile Launch 1"
and "Missile Launch 2", but the code was calling animator.Play() with "Launch
Missile 1" / "Launch Missile 2". This mismatch prevented the
Sparrow_Armature+Mesh|Missile Launch 1 animation from playing when a Skyburst
missile is fired. Updated the state name strings to match the controller.
```

```text
 Assets/_Scripts/Game/Animation/SparrowAnimationController.cs | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs b/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
index 45504c6e5..7ede32877 100644
--- a/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
+++ b/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
@@ -31,7 +31,7 @@ namespace CosmicShore.Game.Animation
 
         void HandleMissileFired(float ammoBeforeFire, float ammoCost)
         {
-            var animName = ammoBeforeFire >= 2f * ammoCost ? "Launch Missile 1" : "Launch Missile 2";
+            var animName = ammoBeforeFire >= 2f * ammoCost ? "Missile Launch 1" : "Missile Launch 2";
             animator.SetLayerWeight(MissileLaunchLayer, 1f);
             animator.Play(animName, MissileLaunchLayer);
             StartCoroutine(ResetMissileLaunchLayer());
```

</details>

### `9346a334b` — Fix CS0102: remove duplicate ActiveInputDevice property from IInputStatus

_Claude, 2026-03-22 23:43:42 +0000_

```text
ActiveInputDevice was declared twice in IInputStatus — once among the
boolean properties (line 38) and again after the vector properties
(line 54), causing a CS0102 compiler error. Removed the duplicate
trailing declaration.
```

```text
 Assets/_Scripts/Game/IO/IInputStatus.cs | 2 --
 1 file changed, 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/IO/IInputStatus.cs b/Assets/_Scripts/Game/IO/IInputStatus.cs
index eccc9f857..cb297f2d9 100644
--- a/Assets/_Scripts/Game/IO/IInputStatus.cs
+++ b/Assets/_Scripts/Game/IO/IInputStatus.cs
@@ -51,8 +51,6 @@ namespace CosmicShore.Game
         Vector2 SingleTouchValue { get; set; }
         Vector3 ThreeDPosition { get; set; }
 
-        InputDeviceType ActiveInputDevice { get; set; }
-
         Quaternion GetGyroRotation();
         void ResetForReplay();
     }
```

</details>

### `8585944f9` — Fix CS0102: remove duplicate HasActiveModal property from ScreenSwitcher

_Claude, 2026-03-22 23:48:08 +0000_

```text
HasActiveModal was declared twice with identical implementations.
Removed the second occurrence at line 173.
```

```text
 Assets/_Scripts/App/UI/ScreenSwitcher.cs | 2 --
 1 file changed, 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/App/UI/ScreenSwitcher.cs b/Assets/_Scripts/App/UI/ScreenSwitcher.cs
index 0dc960347..19e286544 100644
--- a/Assets/_Scripts/App/UI/ScreenSwitcher.cs
+++ b/Assets/_Scripts/App/UI/ScreenSwitcher.cs
@@ -170,8 +170,6 @@ namespace CosmicShore.App.UI
             return GetScreenIdForIndex(currentScreen) == screen;
         }
 
-        public bool HasActiveModal => activeModalStack.Count > 0;
-
         public bool ModalIsActive(ModalWindows modal)
         {
             if (activeModalStack.Count == 0)
```

</details>

### `d85789b7f` — Fix issue with Sparrow Model

_Braden Hamilton, 2026-04-08 20:33:10 -0500_

```text
 Assets/_Animations/SparrowAnimatorController.controller      |  12 ++---
 Assets/_Models/Ship Models/SparrowModel3.fbx.meta            |   4 +-
 Assets/_Prefabs/Projectile/SkyBurstProjectile.prefab         | 112 +++++++++++++++++++++++------------------
 Assets/_Prefabs/Spaceships/Sparrow.prefab                    |  71 +++++++++++++++++++++++++-
 Assets/_Scripts/Game/Animation/MantaAnimationContoller.cs    |  35 +++++++++++++
 Assets/_Scripts/Game/Animation/SparrowAnimationController.cs |  64 +++++++++++------------
 Assets/_Scripts/Game/UI/Editor.meta                          |   8 +++
 7 files changed, 218 insertions(+), 88 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 168 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Animation/MantaAnimationContoller.cs b/Assets/_Scripts/Game/Animation/MantaAnimationContoller.cs
index a0cced255..3024325f1 100644
--- a/Assets/_Scripts/Game/Animation/MantaAnimationContoller.cs
+++ b/Assets/_Scripts/Game/Animation/MantaAnimationContoller.cs
@@ -1,4 +1,6 @@
+using System.Reflection;
 using UnityEngine;
+using System.Collections;
 
 namespace CosmicShore.Game.Animation
 {
@@ -6,6 +8,9 @@ namespace CosmicShore.Game.Animation
     {
         [SerializeField] Animator animator;
         [SerializeField] bool hasBoost = false;
+        [SerializeField] FireGunActionExecutor missileExecutor;
+
+        const int MissileLaunching = 1;
 
         float currentPitch = 0;
         float currentYaw = 0;
@@ -13,6 +18,13 @@ namespace CosmicShore.Game.Animation
         float currentThrottle = 0;
         float animationSpeed = 3.25f;
 
+        public override void Initialize(IVesselStatus vesselStatus)
+        {
+            base.Initialize(vesselStatus);
+            if (missileExecutor != null)
+                missileExecutor.OnMissileFired += HandleMissileFired;
+        }
+
         protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
         {
             if (VesselStatus.IsBoosting && hasBoost) animator.SetBool("Boost", true);
@@ -46,5 +58,28 @@ namespace CosmicShore.Game.Animation
         }
 
         protected override void AssignTransforms() { /* NOOP Abstract Implementation */ }
+
+        void OnDestroy()
+        {
+            if (missileExecutor != null)
+                missileExecutor.OnMissileFired -= HandleMissileFired;
+        }
+
+        void HandleMissileFired(float ammoBeforeFire, float ammoCost)
+        {
+            var animName = ammoBeforeFire >= 2f * ammoCost ? "Missile Launch 1" : "Missile Launch 2";
+            animator.SetLayerWeight(MissileLaunching, 1f);
+            animator.Play(animName, MissileLaunching);
+            StartCoroutine(ResetMissileLaunching());
+        }
+
+        IEnumerator ResetMissileLaunching()
+        {
+            yield return null; // wait one frame for the animator to enter the new state
+            while (animator.GetCurrentAnimatorStateInfo(MissileLaunching).normalizedTime < 1f)
+            yield return null;
+            animator.SetLayerWeight(MissileLaunching, 0f);
+        }  
+
     }
 }
\ No newline at end of file
diff --git a/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs b/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
index 7ede32877..12b251dc9 100644
--- a/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
+++ b/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
@@ -3,10 +3,11 @@ using UnityEngine;
 
 namespace CosmicShore.Game.Animation
 {
-    public class SparrowAnimationController : VesselAnimation
+    class SparrowAnimationController : VesselAnimation
     {
         [SerializeField] Animator animator;
         [SerializeField] FireGunActionExecutor missileExecutor;
+        [SerializeField] bool hasBoost = false;
 
```

</details>

### `0767fa161` — I've completed what this branch needed to do.

_Braden Hamilton, 2026-04-08 21:18:09 -0500_

```text
 Assets/_Animations/SparrowAnimatorController.controller               |  10 +-
 Assets/_Models/Ship Models/SparrowModel3.fbx                          | Bin 6021116 -> 0 bytes
 Assets/_Models/Ship Models/SparrowModel4.fbx                          | Bin 0 -> 31467788 bytes
 .../Ship Models/{SparrowModel3.fbx.meta => SparrowModel4.fbx.meta}    |  49 ++-------
 Assets/_Prefabs/Spaceships/Sparrow.prefab                             | 186 ++++++++------------------------
 Assets/_Scripts/Game/Animation/MantaAnimationContoller.cs             |   3 +-
 6 files changed, 64 insertions(+), 184 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Animation/MantaAnimationContoller.cs b/Assets/_Scripts/Game/Animation/MantaAnimationContoller.cs
index 3024325f1..721028fb5 100644
--- a/Assets/_Scripts/Game/Animation/MantaAnimationContoller.cs
+++ b/Assets/_Scripts/Game/Animation/MantaAnimationContoller.cs
@@ -69,7 +69,8 @@ namespace CosmicShore.Game.Animation
         {
             var animName = ammoBeforeFire >= 2f * ammoCost ? "Missile Launch 1" : "Missile Launch 2";
             animator.SetLayerWeight(MissileLaunching, 1f);
-            animator.Play(animName, MissileLaunching);
+            animator.Play(animName, MissileLaunching, 0f); // 0f forces it to restart from the beginning
+            StopAllCoroutines();
             StartCoroutine(ResetMissileLaunching());
         }
 
```

</details>

### `c154411d4` — Fixed small code in SparrowAnimationController

_Braden Hamilton, 2026-04-09 13:36:39 -0500_

```text
 Assets/_Scripts/Game/Animation/SparrowAnimationController.cs | 24 +++++++++++++-----------
 1 file changed, 13 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs b/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
index 12b251dc9..c98e221ee 100644
--- a/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
+++ b/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
@@ -1,5 +1,6 @@
 using System.Collections;
 using UnityEngine;
+using System.Reflection;
 
 namespace CosmicShore.Game.Animation
 {
@@ -9,7 +10,7 @@ namespace CosmicShore.Game.Animation
         [SerializeField] FireGunActionExecutor missileExecutor;
         [SerializeField] bool hasBoost = false;
 
-        const int MissileLaunchLayer = 1;
+        const int MissileLaunching = 1;
 
         float currentPitch = 0;
         float currentYaw = 0;
@@ -68,17 +69,18 @@ namespace CosmicShore.Game.Animation
         void HandleMissileFired(float ammoBeforeFire, float ammoCost)
         {
             var animName = ammoBeforeFire >= 2f * ammoCost ? "Missile Launch 1" : "Missile Launch 2";
-            animator.SetLayerWeight(MissileLaunchLayer, 1f);
-            animator.Play(animName, MissileLaunchLayer);
-            //StartCoroutine(ResetMissileLaunchLayer());
+            animator.SetLayerWeight(MissileLaunching, 1f);
+            animator.Play(animName, MissileLaunching, 0f); // 0f forces it to restart from the beginning
+            StopAllCoroutines();
+            StartCoroutine(ResetMissileLaunching());
         }
 
-        //IEnumerator ResetMissileLaunchLayer()
-        //{
-            //yield return null; // wait one frame for the animator to enter the new state
-           // while (animator.GetCurrentAnimatorStateInfo(MissileLaunchLayer).normalizedTime < 1f)
-               // yield return null;
-            //animator.SetLayerWeight(MissileLaunchLayer, 0f);
-       // }  
+        IEnumerator ResetMissileLaunching()
+        {
+            yield return null; // wait one frame for the animator to enter the new state
+            while (animator.GetCurrentAnimatorStateInfo(MissileLaunching).normalizedTime < 1f)
+                yield return null;
+            animator.SetLayerWeight(MissileLaunching, 0f);
+        }
     }
 }
```

</details>

### `12a1d0eab` — Cleaned up code

_Braden Hamilton, 2026-04-09 15:12:12 -0500_

```text
 Assets/_Prefabs/Spaceships/Sparrow.prefab                 | 22 ----------------------
 Assets/_Scripts/Game/Animation/MantaAnimationContoller.cs | 31 ++-----------------------------
 2 files changed, 2 insertions(+), 51 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Animation/MantaAnimationContoller.cs b/Assets/_Scripts/Game/Animation/MantaAnimationContoller.cs
index 721028fb5..e6db3a397 100644
--- a/Assets/_Scripts/Game/Animation/MantaAnimationContoller.cs
+++ b/Assets/_Scripts/Game/Animation/MantaAnimationContoller.cs
@@ -8,9 +8,7 @@ namespace CosmicShore.Game.Animation
     {
         [SerializeField] Animator animator;
         [SerializeField] bool hasBoost = false;
-        [SerializeField] FireGunActionExecutor missileExecutor;
-
-        const int MissileLaunching = 1;
+      
 
         float currentPitch = 0;
         float currentYaw = 0;
@@ -21,8 +19,7 @@ namespace CosmicShore.Game.Animation
         public override void Initialize(IVesselStatus vesselStatus)
         {
             base.Initialize(vesselStatus);
-            if (missileExecutor != null)
-                missileExecutor.OnMissileFired += HandleMissileFired;
+           
         }
 
         protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
@@ -58,29 +55,5 @@ namespace CosmicShore.Game.Animation
         }
 
         protected override void AssignTransforms() { /* NOOP Abstract Implementation */ }
-
-        void OnDestroy()
-        {
-            if (missileExecutor != null)
-                missileExecutor.OnMissileFired -= HandleMissileFired;
-        }
-
-        void HandleMissileFired(float ammoBeforeFire, float ammoCost)
-        {
-            var animName = ammoBeforeFire >= 2f * ammoCost ? "Missile Launch 1" : "Missile Launch 2";
-            animator.SetLayerWeight(MissileLaunching, 1f);
-            animator.Play(animName, MissileLaunching, 0f); // 0f forces it to restart from the beginning
-            StopAllCoroutines();
-            StartCoroutine(ResetMissileLaunching());
-        }
-
-        IEnumerator ResetMissileLaunching()
-        {
-            yield return null; // wait one frame for the animator to enter the new state
-            while (animator.GetCurrentAnimatorStateInfo(MissileLaunching).normalizedTime < 1f)
-            yield return null;
-            animator.SetLayerWeight(MissileLaunching, 0f);
-        }  
-
     }
 }
\ No newline at end of file
```

</details>

_Also contains 4 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
