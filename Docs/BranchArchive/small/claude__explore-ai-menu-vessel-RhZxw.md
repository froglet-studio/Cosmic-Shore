# Branch archive: `claude/explore-ai-menu-vessel-RhZxw`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-07 by Claude
- **Unmerged commits:** 2
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/406
- **Forked from:** `78915d84b` (2026-03-07, Update Squirrel.prefab)
- **Tip:** `6a643f7cf`
- **Files touched (6):**
  - `Assets/FTUE/Scripts/Drivers/TutorialFlowController.cs`
  - `Assets/_Scripts/App/UI/MainMenuVesselInteraction/MainMenuVesselReentryController.cs`
  - `Assets/_Scripts/App/UI/MainMenuVesselInteraction/MainMenuVesselTutorialController.cs`
  - `Assets/_Scripts/App/UI/MainMenuVesselInteraction/VesselTutorialUI.cs`
  - `Assets/_Scripts/App/UI/ScreenSwitcher.cs`
  - `Assets/_Scripts/Game/ImpactEffects/Impactors/SkimmerImpactor.cs`

### `03751de38` — Add interactive vessel tutorial and re-entry system for main menu

_Claude, 2026-03-07 18:23:05 +0000_

```text
Introduces a first-launch tutorial that pans the camera behind the AI vessel
and walks the player through speed-up (spread controllers), slow-down (pinch),
and prism-skim steps before returning to the app shell. Also adds an always-
available double-tap re-entry feature to take manual control of the menu vessel.

New files:
- MainMenuVesselTutorialController: FTUE tutorial orchestrator
- MainMenuVesselReentryController: double-tap vessel re-entry
- VesselTutorialUI: overlay for prompts and exit button

Modified:
- ScreenSwitcher: HideAllUI/ShowAllUI convenience methods
- TutorialFlowController: guard to skip old Phase1 when vessel tutorial active
- SkimmerImpactor: static OnPrismSkimmed event for tutorial detection
```

```text
 Assets/FTUE/Scripts/Drivers/TutorialFlowController.cs                 |   8 +-
 .../UI/MainMenuVesselInteraction/MainMenuVesselReentryController.cs   | 284 ++++++++++++++++++++++++++++++++
 .../UI/MainMenuVesselInteraction/MainMenuVesselTutorialController.cs  | 280 +++++++++++++++++++++++++++++++
 Assets/_Scripts/App/UI/MainMenuVesselInteraction/VesselTutorialUI.cs  |  87 ++++++++++
 Assets/_Scripts/App/UI/ScreenSwitcher.cs                              |  31 ++++
 Assets/_Scripts/Game/ImpactEffects/Impactors/SkimmerImpactor.cs       |   7 +-
 6 files changed, 695 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 768 lines)</summary>

```diff
diff --git a/Assets/FTUE/Scripts/Drivers/TutorialFlowController.cs b/Assets/FTUE/Scripts/Drivers/TutorialFlowController.cs
index 7c680bfd5..1569706ba 100644
--- a/Assets/FTUE/Scripts/Drivers/TutorialFlowController.cs
+++ b/Assets/FTUE/Scripts/Drivers/TutorialFlowController.cs
@@ -1,4 +1,5 @@
 using CosmicShore.App.Systems.CTA;
+using CosmicShore.App.UI.MainMenuVesselInteraction;
 using CosmicShore.Events;
 using System.Collections;
 using System.Collections.Generic;
@@ -65,6 +66,11 @@ namespace CosmicShore.FTUE
             if (ftueProgress.ftueDebugKey)
                 return;
 
+            // Skip Phase1 if the new vessel tutorial is handling onboarding
+            if (ftueProgress.currentPhase == TutorialPhase.Phase1_Intro
+                && MainMenuVesselTutorialController.IsActive)
+                return;
+
             if (ftueProgress.currentPhase == TutorialPhase.Phase1_Intro)
             {
                 _currentIndex = 0;
@@ -93,7 +99,7 @@ namespace CosmicShore.FTUE
         }
 
         /// <summary>
-        /// Called by each handler when it�s done.
+        /// Called by each handler when it�s done.
         /// </summary>
         public void StepCompleted()
         {
diff --git a/Assets/_Scripts/App/UI/MainMenuVesselInteraction/MainMenuVesselReentryController.cs b/Assets/_Scripts/App/UI/MainMenuVesselInteraction/MainMenuVesselReentryController.cs
new file mode 100644
index 000000000..57881cb0e
--- /dev/null
+++ b/Assets/_Scripts/App/UI/MainMenuVesselInteraction/MainMenuVesselReentryController.cs
@@ -0,0 +1,284 @@
+using System;
+using System.Collections;
+using CosmicShore.App.Systems;
+using CosmicShore.App.UI;
+using CosmicShore.Game;
+using CosmicShore.Game.CameraSystem;
+using CosmicShore.Soap;
+using CosmicShore.Utility;
+using UnityEngine;
+using UnityEngine.InputSystem;
+using UnityEngine.InputSystem.EnhancedTouch;
+using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
+
+namespace CosmicShore.App.UI.MainMenuVesselInteraction
+{
+    /// <summary>
+    /// Always-available feature on the main menu: double-tap center of screen (or gamepad button)
+    /// to take manual control of the AI vessel. Press B / tap exit button to return to app shell.
+    /// </summary>
+    public class MainMenuVesselReentryController : MonoBehaviour
+    {
+        [Header("Data")]
+        [SerializeField] private GameDataSO gameData;
+
+        [Header("UI References")]
+        [SerializeField] private ScreenSwitcher screenSwitcher;
+        [SerializeField] private GameObject navBar;
+        [SerializeField] private GameObject menu;
+        [SerializeField] private VesselTutorialUI tutorialUI;
+
+        [Header("Camera")]
+        [SerializeField] private float cameraPanDuration = 1.5f;
+        [SerializeField] private float cameraReturnDuration = 1.5f;
+
+        [Header("Double-Tap Settings")]
+        [SerializeField] private float doubleTapWindow = 0.4f;
+        [SerializeField] private float centerRegionFraction = 0.4f;
+
+        private IPlayer player;
+        private IVessel vessel;
+        private bool isControllingVessel;
+        private Coroutine activeCoroutine;
+
+        // Stored menu camera state
+        private Vector3 menuCameraPosition;
+        private Quaternion menuCameraRotation;
+
+        // Double-tap tracking
+        private float lastTapTime;
+
+        private void OnEnable()
+        {
+            EnhancedTouchSupport.Enable();
+        }
+
+        private void OnDisable()
+        {
+            EnhancedTouchSupport.Disable();
+        }
+
+        private void Update()
+        {
+            // Don't process if tutorial is running or we're in a transition
+            if (MainMenuVesselTutorialController.IsActive)
+                return;
+
+            if (isControllingVessel)
+            {
+                // Check for exit: gamepad B or exit button (handled by event)
+                if (IsGamepadBPressed())
+                    ExitVesselControl();
+                return;
+            }
+
+            // Detect double-tap to enter
+            DetectDoubleTap();
+            DetectGamepadEntry();
+        }
+
+        private void DetectDoubleTap()
+        {
+            foreach (var touch in Touch.activeTouches)
+            {
+                if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began)
+                    continue;
+
+                if (!IsInCenterRegion(touch.screenPosition))
+                    continue;
+
+                if (Time.unscaledTime - lastTapTime < doubleTapWindow)
+                {
+                    lastTapTime = 0f;
+                    EnterVesselControl();
+                    return;
+                }
+
+                lastTapTime = Time.unscaledTime;
+            }
+        }
+
+        private void DetectGamepadEntry()
+        {
+            // Press both sticks to enter vessel control on gamepad
+            var gamepad = Gamepad.current;
+            if (gamepad == null) return;
+
+            if (gamepad.leftStickButton.wasPressedThisFrame && gamepad.rightStickButton.isPressed)
+                EnterVesselControl();
+            else if (gamepad.rightStickButton.wasPressedThisFrame && gamepad.leftStickButton.isPressed)
+                EnterVesselControl();
+        }
+
+        private bool IsInCenterRegion(Vector2 screenPosition)
```

</details>

### `6a643f7cf` — Refine vessel tutorial: add ActionHandler subscription, idle toggle, and HOME screen guard

_Claude, 2026-03-07 18:24:48 +0000_

```text
- Toggle ActionHandler subscription when switching between AI and player control
- Set input idle state properly on enter/exit
- Hide HUD explicitly when returning to AI control
- Only detect double-tap re-entry when on HOME screen with no modals open
```

```text
 Assets/_Scripts/App/UI/MainMenuVesselInteraction/MainMenuVesselReentryController.cs  | 11 +++++++++++
 Assets/_Scripts/App/UI/MainMenuVesselInteraction/MainMenuVesselTutorialController.cs |  5 +++++
 2 files changed, 16 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/App/UI/MainMenuVesselInteraction/MainMenuVesselReentryController.cs b/Assets/_Scripts/App/UI/MainMenuVesselInteraction/MainMenuVesselReentryController.cs
index 57881cb0e..d8da9f810 100644
--- a/Assets/_Scripts/App/UI/MainMenuVesselInteraction/MainMenuVesselReentryController.cs
+++ b/Assets/_Scripts/App/UI/MainMenuVesselInteraction/MainMenuVesselReentryController.cs
@@ -72,6 +72,12 @@ namespace CosmicShore.App.UI.MainMenuVesselInteraction
                 return;
             }
 
+            // Only detect re-entry when on HOME screen with no modals open
+            if (screenSwitcher != null
+                && (!screenSwitcher.ScreenIsActive(ScreenSwitcher.MenuScreens.HOME)
+                    || screenSwitcher.HasActiveModal))
+                return;
+
             // Detect double-tap to enter
             DetectDoubleTap();
             DetectGamepadEntry();
@@ -171,6 +177,8 @@ namespace CosmicShore.App.UI.MainMenuVesselInteraction
             // Stop AI, enable player input
             vessel.ToggleAIPilot(false);
             player.InputController.SetPause(false);
+            player.InputController.SetIdle(false);
+            vessel.VesselStatus.ActionHandler.ToggleSubscription(true);
 
             // Pan camera behind vessel
             yield return PanCameraBehindVessel();
@@ -186,7 +194,10 @@ namespace CosmicShore.App.UI.MainMenuVesselInteraction
             tutorialUI.HideAll();
 
             // Pause player input, re-enable AI
+            vessel.VesselStatus.ActionHandler.ToggleSubscription(false);
+            player.InputController.SetIdle(true);
             player.InputController.SetPause(true);
+            vessel.VesselStatus.VesselHUDController.HideHUD();
             vessel.ToggleAIPilot(true);
 
             // Pan camera back to menu
diff --git a/Assets/_Scripts/App/UI/MainMenuVesselInteraction/MainMenuVesselTutorialController.cs b/Assets/_Scripts/App/UI/MainMenuVesselInteraction/MainMenuVesselTutorialController.cs
index f698d37f1..8264d41d1 100644
--- a/Assets/_Scripts/App/UI/MainMenuVesselInteraction/MainMenuVesselTutorialController.cs
+++ b/Assets/_Scripts/App/UI/MainMenuVesselInteraction/MainMenuVesselTutorialController.cs
@@ -113,6 +113,8 @@ namespace CosmicShore.App.UI.MainMenuVesselInteraction
             // Stop AI and give control to player
             vessel.ToggleAIPilot(false);
             player.InputController.SetPause(false);
+            player.InputController.SetIdle(false);
+            vessel.VesselStatus.ActionHandler.ToggleSubscription(true);
 
             // Transition camera behind vessel
             yield return PanCameraBehindVessel();
@@ -253,7 +255,10 @@ namespace CosmicShore.App.UI.MainMenuVesselInteraction
             tutorialUI.HideAll();
 
             // Pause player input and re-enable AI
+            vessel.VesselStatus.ActionHandler.ToggleSubscription(false);
+            player.InputController.SetIdle(true);
             player.InputController.SetPause(true);
+            vessel.VesselStatus.VesselHUDController.HideHUD();
             vessel.ToggleAIPilot(true);
 
             // Pan camera back to menu
```

</details>
