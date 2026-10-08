# Branch archive: `Audio_Playground`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

## What this branch is

**FMOD engine and boost sounds prototype**

An audio experimentation branch that added the FMOD audio middleware and built vessel engine sounds, boost and crystal pickup sounds, with variations for all four elements. The final commit claims the FMOD engine sound system is complete. Most changed files are the FMOD plugin itself.

- **Status:** Redone elsewhere
- **Areas:** audio, FMOD, vessel engine sound
- **Already in bleeding-edge:** bleeding-edge has the FMOD plugin (Assets/Plugins/FMOD/Resources/FMODStudioSettings.asset), a LOCKED FMOD exposed-field convention (Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md) and many audio commits (e.g. b76e7ab0c 'fix(audio): vessel audio follows the pilot when a live hull changes hands').
- **Risk if deleted:** low
- **Suggestion (2026-10-08):** can be deleted after archiving — FMOD integration and vessel audio landed in bleeding-edge under a stricter convention; this branch was a playground.

## Evidence

- **Last commit:** 2026-04-23 by aradia1
- **Unmerged commits:** 15
- **Forked from:** `0d48ab5b5` (2026-02-25, Merge pull request #78 from froglet-studio/claude/add-missing-sounds-1ZgoJ)
- **Tip:** `4159b869b`
- **Files touched (649):**
  - `.DS_Store`
  - `Assets/Plugins/FMOD.meta`
  - `Assets/Plugins/FMOD/Cache.meta`
  - `Assets/Plugins/FMOD/Cache/Editor.meta`
  - `Assets/Plugins/FMOD/Cache/Editor/FMODStudioCache.asset`
  - `Assets/Plugins/FMOD/Cache/Editor/FMODStudioCache.asset.meta`
  - `Assets/Plugins/FMOD/FMODUnity.asmdef`
  - `Assets/Plugins/FMOD/FMODUnity.asmdef.meta`
  - `Assets/Plugins/FMOD/LICENSE.txt`
  - `Assets/Plugins/FMOD/LICENSE.txt.meta`
  - `Assets/Plugins/FMOD/README.txt`
  - `Assets/Plugins/FMOD/README.txt.meta`
  - `Assets/Plugins/FMOD/Resources.meta`
  - `Assets/Plugins/FMOD/Resources/FMODStudioSettings.asset`
  - `Assets/Plugins/FMOD/Resources/FMODStudioSettings.asset.meta`
  - `Assets/Plugins/FMOD/addons.meta`
  - `Assets/Plugins/FMOD/addons/Haptics.meta`
  - `Assets/Plugins/FMOD/addons/Haptics/Scripts.meta`
  - `Assets/Plugins/FMOD/addons/Haptics/Scripts/FMODHaptics.cs`
  - `Assets/Plugins/FMOD/addons/Haptics/Scripts/FMODHaptics.cs.meta`
  - `Assets/Plugins/FMOD/addons/Haptics/Scripts/FMODUnityHaptics.asmdef`
  - `Assets/Plugins/FMOD/addons/Haptics/Scripts/FMODUnityHaptics.asmdef.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Editor.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Editor/FMODUnityResonanceEditor.asmdef`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Editor/FMODUnityResonanceEditor.asmdef.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Editor/FmodResonanceAudioRoomEditor.cs`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Editor/FmodResonanceAudioRoomEditor.cs.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Editor/Localization.cs`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Editor/Localization.cs.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Editor/zh_hans.po`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Editor/zh_hans.po.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Scripts.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Scripts/FMODUnityResonance.asmdef`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Scripts/FMODUnityResonance.asmdef.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Scripts/FmodResonanceAudio.cs`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Scripts/FmodResonanceAudio.cs.meta`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Scripts/FmodResonanceAudioRoom.cs`
  - `Assets/Plugins/FMOD/addons/ResonanceAudio/Scripts/FmodResonanceAudioRoom.cs.meta`
  - `Assets/Plugins/FMOD/images.meta`
  - … and 609 more

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

### `6cb341b02` — Fmod Added

_aradia1, 2026-03-31 15:11:49 -0400_

```text
Also changed some SFX
```

```text
 .../Metadata/AudioFile/{c294463a-1a78-47d8-a300-56ddc8484acd}.xml     |   20 +
 .../Metadata/AudioFile/{c2a44391-345e-4004-8137-aeec286ee9bb}.xml     |   20 +
 .../Metadata/AudioFile/{f25dfa27-9594-443f-9ae8-81147771def5}.xml     |   20 +
 Cosmic Shore/Metadata/Bank/{ddcf0e06-466d-4a76-a442-d98541b57b7e}.xml |   14 +
 .../Metadata/BankFolder/{512e2d1c-0895-454a-84d2-a755196e4e70}.xml    |    4 +
 .../EffectPresetFolder/{8f865bc7-a556-461b-9672-306f9e76e692}.xml     |    4 +
 .../EncodingSetting/{1b97285b-9622-4b05-a309-a4768d7e1c8d}.xml        |   17 +
 .../EncodingSetting/{87b27cde-3b20-4fa5-8c14-6b925adb7fd6}.xml        |   17 +
 .../Metadata/Event/{2b9d52ee-bcc0-45e3-83d6-7cbe8e848f9e}.xml         |  503 ++++
 .../Metadata/Event/{942c0807-260e-4954-8d1c-c321d5b954b3}.xml         |  764 ++++++
 .../Metadata/Event/{b6e8ca72-664d-4ff3-ad7d-23999488da61}.xml         |  467 ++++
 .../Metadata/EventFolder/{1f4d41c2-e383-444c-98e8-039b17731601}.xml   |    8 +
 .../Metadata/EventFolder/{a0fa6654-cb9c-4b6f-9e9c-19b57077078f}.xml   |   11 +
 .../Metadata/EventFolder/{e6a87a8b-70e8-4897-bd28-b6fc22d84a8e}.xml   |   11 +
 Cosmic Shore/Metadata/Master.xml                                      |   28 +
 Cosmic Shore/Metadata/Mixer.xml                                       |   11 +
 .../ParameterPreset/{175bc0ad-4956-4412-9a04-d664abb7a075}.xml        |   25 +
 .../ParameterPreset/{9542312e-c17e-41b9-bdb1-d2211af1407c}.xml        |   22 +
 .../ParameterPreset/{f757ff2e-c47d-487c-b46e-5423a1e27ad1}.xml        |   22 +
 .../ParameterPresetFolder/{13129644-f1bd-481c-a39a-9d5da342acdb}.xml  |    4 +
 .../Metadata/Platform/{468997fc-fc2d-42c3-8c05-f7f8b3d3ac65}.xml      |   17 +
 .../ProfilerFolder/{b06aee77-9cce-4dd3-b4b8-e1e5fbc12bd0}.xml         |    4 +
 .../Metadata/Return/{f01f6fe1-eafc-49f4-bea1-89e913b2c2ab}.xml        |   36 +
 .../Metadata/SandboxFolder/{6a830f59-8bd2-4cc8-bc52-c81b927ddf5c}.xml |    4 +
 .../Metadata/SnapshotGroup/{24710ab5-1642-4fc3-9620-e3dbb124d75b}.xml |    8 +
 Cosmic Shore/Metadata/Tags.xml                                        |    8 +
 Cosmic Shore/Metadata/Workspace.xml                                   |   35 +
 ProjectSettings/GvhProjectSettings.xml                                |    4 +
 ProjectSettings/QualitySettings.asset                                 |   36 +-
 512 files changed, 48905 insertions(+), 237 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 34884 lines)</summary>

```diff
diff --git a/Assets/Plugins/FMOD/FMODUnity.asmdef b/Assets/Plugins/FMOD/FMODUnity.asmdef
new file mode 100644
index 000000000..a9fdfc32a
--- /dev/null
+++ b/Assets/Plugins/FMOD/FMODUnity.asmdef
@@ -0,0 +1,51 @@
+{
+    "name": "FMODUnity",
+    "references": [
+        "Unity.Timeline",
+        "Unity.Addressables",
+        "Unity.ResourceManager",
+        "Unity.RenderPipelines.Universal.Runtime"
+    ],
+    "includePlatforms": [],
+    "excludePlatforms": [],
+    "allowUnsafeCode": true,
+    "overrideReferences": false,
+    "precompiledReferences": [],
+    "autoReferenced": true,
+    "defineConstraints": [
+        "UNITY_2021_3_OR_NEWER"
+    ],
+    "versionDefines": [
+        {
+            "name": "com.unity.timeline",
+            "expression": "1.0.0",
+            "define": "UNITY_TIMELINE_EXIST"
+        },
+        {
+            "name": "com.unity.addressables",
+            "expression": "1.0.0",
+            "define": "UNITY_ADDRESSABLES_EXIST"
+        },
+        {
+            "name": "com.unity.modules.physics",
+            "expression": "1.0.0",
+            "define": "UNITY_PHYSICS_EXIST"
+        },
+        {
+            "name": "com.unity.modules.physics2d",
+            "expression": "1.0.0",
+            "define": "UNITY_PHYSICS2D_EXIST"
+        },
+        {
+            "name": "com.unity.urp",
+            "expression": "1.0.0",
+            "define": "UNITY_URP_EXIST"
+        },
+        {
+            "name": "com.unity.ugui",
+            "expression": "1.0.0",
+            "define": "UNITY_UI_EXIST"
+        }
+    ],
+    "noEngineReferences": false
+}
\ No newline at end of file
diff --git a/Assets/Plugins/FMOD/LICENSE.txt b/Assets/Plugins/FMOD/LICENSE.txt
new file mode 100644
index 000000000..2ce86fa43
--- /dev/null
+++ b/Assets/Plugins/FMOD/LICENSE.txt
@@ -0,0 +1,1053 @@
+                    FMOD END USER LICENCE AGREEMENT
+                    ===============================
+
+This End User Licence Agreement (EULA) is a legal agreement between you and 
+Firelight Technologies Pty Ltd (ACN 099 182 448) (us or we) and governs your
+use of FMOD Studio and FMOD Engine, together the Software.
+
+1. GRANT OF LICENCE
+
+1.1 FMOD Studio
+
+This EULA grants you the right to use FMOD Studio, being the desktop 
+application for adaptive audio content creation, for all use, including
+Commercial use, subject to the following:
+
+    i.  FMOD Studio is used to create content for use with the FMOD Engine 
```

</details>

### `f7ddfd4c5` — Engine sounds added

_aradia1, 2026-03-31 15:45:24 -0400_

```text
Everything is there for a coder to do what they need too.
```

```text
 Cosmic Shore/.cache/{048237b9-4ef6-47df-a54d-9b290faede6d}.pdc        | Bin 0 -> 414088 bytes
 Cosmic Shore/.cache/{8e8f4ed1-505c-4ade-99d0-9dc3dfa2966c}.pdc        | Bin 0 -> 1424408 bytes
 .../Metadata/Event/{942c0807-260e-4954-8d1c-c321d5b954b3}.xml         | 760 --------------------------------
 Cosmic Shore/.unsaved/metadataFileMapping.data                        | Bin 526 -> 0 bytes
 Cosmic Shore/Assets/VEHInt_Bus City Transit_S6k33-08.wav              | Bin 0 -> 15658102 bytes
 Cosmic Shore/Assets/VEHInt_Car Wash_S6k04-97.wav                      | Bin 0 -> 53544972 bytes
 .../Metadata/AudioFile/{048237b9-4ef6-47df-a54d-9b290faede6d}.xml     |  23 +
 .../Metadata/AudioFile/{8e8f4ed1-505c-4ade-99d0-9dc3dfa2966c}.xml     |  23 +
 .../Metadata/Event/{2b9d52ee-bcc0-45e3-83d6-7cbe8e848f9e}.xml         | 212 ++++++++-
 .../Metadata/Event/{942c0807-260e-4954-8d1c-c321d5b954b3}.xml         | 149 ++++++-
 .../Metadata/Event/{b6e8ca72-664d-4ff3-ad7d-23999488da61}.xml         | 233 +++++++++-
 .../ParameterPreset/{9542312e-c17e-41b9-bdb1-d2211af1407c}.xml        |   2 +-
 12 files changed, 612 insertions(+), 790 deletions(-)
```

### `1f9c6219f` — Added boost and crystal sounds

_aradia1, 2026-03-31 18:54:32 -0400_

```text
 Assets/Plugins/FMOD/Resources/FMODStudioSettings.asset                |   6 +-
 Assets/_Audio/Sounds/Cosmic shore Boost.mp3                           | Bin 0 -> 30720 bytes
 Assets/_Audio/Sounds/Cosmic shore Boost.mp3.meta                      |  23 +
 Assets/_Audio/Sounds/drift let go cosmic shore.wav                    | Bin 0 -> 1152080 bytes
 Assets/_Audio/Sounds/drift let go cosmic shore.wav.meta               |  23 +
 Assets/_Scenes/Menu_Main.unity                                        |  20 +
 Cosmic Shore/.cache/fsbcache/Desktop/1755D38C.fobj                    | Bin 0 -> 48156 bytes
 Cosmic Shore/.cache/fsbcache/Desktop/26ECECB3.fobj                    | Bin 0 -> 738857 bytes
 Cosmic Shore/.cache/fsbcache/Desktop/69478467.fobj                    | Bin 0 -> 374379 bytes
 Cosmic Shore/.cache/fsbcache/Desktop/9BC6029E.fobj                    | Bin 0 -> 643164 bytes
 Cosmic Shore/.cache/fsbcache/Desktop/A93AB0D6.fobj                    | Bin 0 -> 95139 bytes
 Cosmic Shore/.cache/fsbcache/Desktop/F44089F7.fobj                    | Bin 0 -> 55389 bytes
 Cosmic Shore/.cache/{7551d20b-481e-4c1f-8ef2-27e2c9bc2d2f}.pdc        | Bin 0 -> 11576 bytes
 .../Metadata/Event/{942c0807-260e-4954-8d1c-c321d5b954b3}.xml         | 800 +++++++++++++++++++++++++++++++
 .../Metadata/Event/{b6e8ca72-664d-4ff3-ad7d-23999488da61}.xml         | 819 ++++++++++++++++++++++++++++++++
 .../ParameterPreset/{9542312e-c17e-41b9-bdb1-d2211af1407c}.xml        |  30 ++
 Cosmic Shore/.unsaved/metadataFileMapping.data                        | Bin 0 -> 1088 bytes
 Cosmic Shore/Assets/Crystal_Collision_Amplified.wav                   | Bin 0 -> 432080 bytes
 Cosmic Shore/Build/Desktop/Master.strings.bank                        | Bin 868 -> 986 bytes
 Cosmic Shore/Build/Desktop/SFX.bank                                   | Bin 0 -> 1975488 bytes
 .../Metadata/AudioFile/{7551d20b-481e-4c1f-8ef2-27e2c9bc2d2f}.xml     |  20 +
 Cosmic Shore/Metadata/Bank/{3b7e8e6a-defe-46c2-8201-c749ea9220ba}.xml |  11 +
 .../Metadata/Event/{2b9d52ee-bcc0-45e3-83d6-7cbe8e848f9e}.xml         | 250 +++++++---
 .../Metadata/Event/{497aba84-20c1-49f9-9b42-7b657b029ab5}.xml         | 202 ++++++++
 .../Metadata/Event/{942c0807-260e-4954-8d1c-c321d5b954b3}.xml         | 155 ++----
 .../Metadata/Event/{b6e8ca72-664d-4ff3-ad7d-23999488da61}.xml         | 120 +++++
 .../Metadata/EventFolder/{973179a4-8803-4e6c-8ca6-89d010ec9ca1}.xml   |  11 +
 .../ParameterPreset/{9542312e-c17e-41b9-bdb1-d2211af1407c}.xml        |   2 +-
 fmod_editor.log                                                       |  30 ++
 30 files changed, 2534 insertions(+), 170 deletions(-)
```

### `8e887ac42` — Fmod updated to include all 4 elements

_aradia1, 2026-04-07 13:14:18 -0400_

```text
 Cosmic Shore/Assets/Cosmic shore Mass ship 1.wav                      | Bin 0 -> 4608080 bytes
 Cosmic Shore/Assets/Cosmic shore Mass ship 2.wav                      | Bin 0 -> 4608080 bytes
 Cosmic Shore/Assets/Cosmic shore Mass ship 3.wav                      | Bin 0 -> 3456080 bytes
 Cosmic Shore/Assets/Cosmic shore Mass ship 4.wav                      | Bin 0 -> 4608080 bytes
 Cosmic Shore/Assets/Cosmic shore space ship 2.wav                     | Bin 0 -> 9216080 bytes
 Cosmic Shore/Assets/Cosmic shore space ship 3.wav                     | Bin 0 -> 9216080 bytes
 Cosmic Shore/Assets/Cosmic shore space ship 4.wav                     | Bin 0 -> 9216080 bytes
 Cosmic Shore/Assets/Cosmic shore time ship 1.wav                      | Bin 0 -> 6912080 bytes
 Cosmic Shore/Assets/Cosmic shore time ship 2.wav                      | Bin 0 -> 6912080 bytes
 .../Metadata/AudioFile/{0477d3b5-cf10-47b2-a26b-8613589ea27e}.xml     |  23 +
 .../Metadata/AudioFile/{08f14a97-9b71-44bc-9336-381ac53dced6}.xml     |  23 +
 .../Metadata/AudioFile/{2a0fa2e0-f419-41ff-bfaa-402f08443454}.xml     |  23 +
 .../Metadata/AudioFile/{30e878ca-3be2-470b-98a4-f8e6e653d179}.xml     |  23 +
 .../Metadata/AudioFile/{3b2a5ac4-570b-46ac-91a1-1666af65f37d}.xml     |  23 +
 .../Metadata/AudioFile/{57d9522e-36ca-468e-b721-9b667528deb7}.xml     |  23 +
 .../Metadata/AudioFile/{6505ce88-8f56-4407-aa8f-b9d20b0c4d73}.xml     |  23 +
 .../Metadata/AudioFile/{9cc0d97d-f570-43d5-8ceb-fe3d21528479}.xml     |  23 +
 .../Metadata/AudioFile/{cb706aba-2235-4c93-a097-cd3f0ab197c7}.xml     |  23 +
 .../Metadata/AudioFile/{eb3b5491-a3d0-4fd4-bff8-1e3858fbedd8}.xml     |  23 +
 .../Metadata/Event/{05c73e9c-d556-4a1d-a268-e26c182454ae}.xml         | 727 ++++++++++++++++++++++++++
 .../Metadata/Event/{4c38bbab-c38c-42bf-8eac-3f2cc7652507}.xml         | 494 ++++++++++++++++++
 .../Metadata/Event/{7d4f02b7-c072-4d30-8436-ddbef3518eb7}.xml         | 891 ++++++++++++++++++++++++++++++++
 .../Metadata/Event/{942c0807-260e-4954-8d1c-c321d5b954b3}.xml         | 401 +++++++++++---
 .../Metadata/Event/{c4d6e9dd-cccb-4e2e-83d8-fb037cff1d69}.xml         | 251 +++++++++
 .../ParameterPreset/{60b04e19-90a9-4f74-9967-51970180263c}.xml        |  22 +
 .../ParameterPreset/{7724ba14-b911-4f82-9d12-36ac674ac9d1}.xml        |  22 +
 .../ParameterPreset/{c0287b44-f804-4545-a6e0-438fd63042a7}.xml        |  22 +
 .../ParameterPreset/{d4367733-8480-410d-a74f-02dd7bc0091c}.xml        |  22 +
 fmod_editor.log                                                       |  16 +-
 53 files changed, 3201 insertions(+), 1728 deletions(-)
```

### `4159b869b` — Fmod engine system complete

_aradia1, 2026-04-23 14:29:01 -0400_

```text
 Cosmic Shore/Assets/Cosmic shore Charge 3 ship.wav                    |  Bin 0 -> 6336080 bytes
 Cosmic Shore/Assets/Cosmic shore Mass ship 5.wav                      |  Bin 0 -> 4608080 bytes
 Cosmic Shore/Assets/Cosmic shore drift hold layer 2.wav               |  Bin 0 -> 936080 bytes
 Cosmic Shore/Assets/Cosmic shore drift hold.wav                       |  Bin 0 -> 936080 bytes
 Cosmic Shore/Assets/drift let go cosmic shore.wav                     |  Bin 0 -> 1152080 bytes
 Cosmic Shore/Build/Desktop/Master.bank                                |  Bin 1036 -> 1380 bytes
 Cosmic Shore/Build/Desktop/Master.strings.bank                        |  Bin 986 -> 1438 bytes
 Cosmic Shore/Build/Desktop/SFX.bank                                   |  Bin 1975488 -> 4668576 bytes
 .../Metadata/AudioFile/{2e496504-cfca-40f3-8198-52c35c9c29a8}.xml     |   20 +
 .../Metadata/AudioFile/{5e685cb8-ef3d-4d02-b07f-3fa127bc1efc}.xml     |   23 +
 .../Metadata/AudioFile/{86c86198-8042-4bfc-9c7f-4f48fea3b5d6}.xml     |   20 +
 .../Metadata/AudioFile/{87219161-a2c0-43fa-8739-41d93013906a}.xml     |   23 +
 .../Metadata/AudioFile/{9ae247a1-5800-410c-99f2-47ad8871e848}.xml     |   20 +
 .../Metadata/AudioFile/{bbb35f13-85ed-4aa3-8212-8039e43059d1}.xml     |   23 +
 .../Metadata/AudioFile/{cc97f6b2-1e61-4e76-9f8d-1e5b24598139}.xml     |   23 +
 .../Metadata/AudioFile/{d6a0f5a2-8ecb-4b85-a4dc-37b56ff517b2}.xml     |   20 +
 .../Metadata/Event/{05c73e9c-d556-4a1d-a268-e26c182454ae}.xml         |  351 ++++++---
 .../Metadata/Event/{06b780fb-e09d-46a6-a8a3-f42c28791206}.xml         |  110 +++
 .../Metadata/Event/{10e1c214-2d0b-4b6f-a94e-759c9828221f}.xml         |  331 +++++++++
 .../Metadata/Event/{2b9d52ee-bcc0-45e3-83d6-7cbe8e848f9e}.xml         |   21 +-
 .../Metadata/Event/{4c38bbab-c38c-42bf-8eac-3f2cc7652507}.xml         |   81 ++-
 .../Metadata/Event/{7d4f02b7-c072-4d30-8436-ddbef3518eb7}.xml         |  307 +++++++-
 .../Metadata/Event/{942c0807-260e-4954-8d1c-c321d5b954b3}.xml         |  822 ++++++++++++++++-----
 .../Metadata/Event/{b6e8ca72-664d-4ff3-ad7d-23999488da61}.xml         |    4 +-
 .../Metadata/Event/{c4d6e9dd-cccb-4e2e-83d8-fb037cff1d69}.xml         |  519 +++++++++++--
 .../ParameterPreset/{175bc0ad-4956-4412-9a04-d664abb7a075}.xml        |    6 +
 .../ParameterPreset/{265c0599-865f-4e02-a38c-6ae97e62dffd}.xml        |   19 +
 .../ParameterPreset/{f757ff2e-c47d-487c-b46e-5423a1e27ad1}.xml        |    8 +-
 fmod_editor.log                                                       |   19 +-
 92 files changed, 4603 insertions(+), 509 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1210 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/Audio/ShipAudioController.cs b/Assets/_Scripts/Game/Ship/Audio/ShipAudioController.cs
new file mode 100644
index 000000000..bfbfb0e4f
--- /dev/null
+++ b/Assets/_Scripts/Game/Ship/Audio/ShipAudioController.cs
@@ -0,0 +1,1204 @@
+using System.Collections.Generic;
+using CosmicShore.Core;
+using FMOD.Studio;
+using FMODUnity;
+using UnityEngine;
+
+namespace CosmicShore.Game.Audio
+{
+    /// <summary>
+    /// Drives the FMOD "space ship engine main" loop for a single vessel.
+    ///
+    /// Usage:
+    ///   - Attach to the vessel root prefab (same GameObject as the Vessel /
+    ///     IVesselStatus components).
+    ///   - Assign <see cref="engineEvent"/> to "event:/space ship engine main"
+    ///     in the inspector.
+    ///
+    /// Velocity source:
+    ///   Cosmic Shore vessels are moved via transform.position, not a
+    ///   Rigidbody. This component measures world-space velocity each frame
+    ///   as (position - lastPosition) / deltaTime, smooths it, normalises it
+    ///   against <see cref="maxShipVelocity"/>, and pushes the result into
+    ///   the event's "Speed" parameter.
+    ///
+    /// Spatialisation:
+    ///   - By default (<see cref="onlyAudibleToController"/> = true) the
+    ///     engine audio is ONLY created for the ship the current client is
+    ///     controlling. Remote ships and AI ships never instantiate the
+    ///     FMOD event on this client, so other players' ships are silent
+    ///     here. This matches the intent "ship audio is only heard by the
+    ///     person controlling the ship".
+    ///   - If <see cref="onlyAudibleToController"/> is disabled, the
+    ///     legacy behaviour applies: remote ships attach to their own
+    ///     transform (normal 3D attenuation), and the LOCAL player's ship
+    ///     attaches to the FMOD StudioListener instead, so the instance's
+    ///     3D position is always the listener's position. That makes it
+    ///     effectively 2D -> always audible, even when the camera pulls
+    ///     away from the ship.
+    ///   - If the local-vs-remote check fails (e.g. Player not set), the
+    ///     instance falls back to attaching to the ship transform.
+    ///
+    /// Ownership is not guaranteed to be known at Start() (VesselController
+    /// assigns the Player after Initialize()), so instance creation is
+    /// deferred until we can resolve local-vs-remote. See
+    /// <see cref="TryEvaluateAndCreate"/>.
+    /// </summary>
+    [DisallowMultipleComponent]
+    public class ShipAudioController : MonoBehaviour
+    {
+        enum AttachMode
+        {
+            /// <summary>No routing decision yet (instance freshly created).</summary>
+            None,
+            /// <summary>Attached to this ship's transform (3D spatialisation).</summary>
+            Ship,
+            /// <summary>Attached to the listener's transform (effectively 2D / always audible).</summary>
+            Listener
+        }
+
+        [Header("FMOD Event")]
+        [SerializeField, Tooltip("FMOD event for the looping ship engine.")]
+        EventReference engineEvent;
+
+        [SerializeField, Tooltip(
+            "Extra FMOD events to play in parallel with the main engine event " +
+            "(Option C: driving nested child events directly from code). " +
+            "Use this when the parent engine event contains nested/referenced " +
+            "child events that don't receive the parent's local parameter " +
+            "values. List each child event here and this controller will " +
+            "create, attach, start, and parameter-drive them alongside the " +
+            "parent. IMPORTANT: if you list a child event here, remove or " +
+            "disable its nested event instrument inside the parent event in " +
+            "FMOD Studio, otherwise you'll hear double playback.")]
+        EventReference[] additionalEngineLayers;
```

</details>

_Also contains 2 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
