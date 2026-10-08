# Branch archive: `claude/fix-arcade-replay-577tb`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-04-09 by Claude
- **Unmerged commits:** 1
- **Forked from:** `2606ca6a5` (2026-04-08, Merge pull request #470 from froglet-studio/claude/fix-modal-state-return-OwxV)
- **Tip:** `1a8e05e50`
- **Files touched (5):**
  - `Assets/_Scripts/Controller/AI/AIPilot.cs`
  - `Assets/_Scripts/Controller/Animation/VesselAnimation.cs`
  - `Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs`
  - `Assets/_Scripts/Controller/Vessel/GunTransformer.cs`
  - `Assets/_Scripts/System/SceneLoader.cs`

### `1a8e05e50` — fix(arcade): prevent MissingReferenceException on destroyed Player during scene transitions

_Claude, 2026-04-09 17:54:56 +0000_

```text
When transitioning between game scenes and Menu_Main, vessels were
despawned with Despawn(false) which kept their GameObjects alive.
During the subsequent scene load, Netcode destroyed AI Players
(destroyWithScene=true) before Unity cleaned up the remaining vessel
GameObjects. In this window, VesselAnimation.Update() and other vessel
components accessed the destroyed Player, causing MissingReferenceException.

Root fix: Changed Despawn(false) to Despawn(true) in both
ClearPlayerVesselReferences() and ExecuteSceneReloadReplay() so vessel
GameObjects are destroyed immediately, preventing stale Update() calls.

Defensive fix: Added destroyed-Player guards in VesselAnimation.Update(),
AIPilot.Update(), and GunTransformer.Update() to gracefully bail out if
the Player reference is destroyed during edge-case timing windows.
```

```text
 Assets/_Scripts/Controller/AI/AIPilot.cs                               | 3 +++
 Assets/_Scripts/Controller/Animation/VesselAnimation.cs                | 9 +++++++++
 Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs | 2 +-
 Assets/_Scripts/Controller/Vessel/GunTransformer.cs                    | 3 +++
 Assets/_Scripts/System/SceneLoader.cs                                  | 2 +-
 5 files changed, 17 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/AI/AIPilot.cs b/Assets/_Scripts/Controller/AI/AIPilot.cs
index 06b686a0c..dad30c1a3 100644
--- a/Assets/_Scripts/Controller/AI/AIPilot.cs
+++ b/Assets/_Scripts/Controller/AI/AIPilot.cs
@@ -276,6 +276,9 @@ namespace CosmicShore.Gameplay
             if (!AutoPilotEnabled)
                 return;
 
+            if (VesselStatus.Player is UnityEngine.Object obj && !obj)
+                return;
+
             if (VesselStatus.IsStationary)
                 return;
 
diff --git a/Assets/_Scripts/Controller/Animation/VesselAnimation.cs b/Assets/_Scripts/Controller/Animation/VesselAnimation.cs
index 7a065fb74..36a867d7e 100644
--- a/Assets/_Scripts/Controller/Animation/VesselAnimation.cs
+++ b/Assets/_Scripts/Controller/Animation/VesselAnimation.cs
@@ -28,6 +28,15 @@ namespace CosmicShore.Gameplay
             if (!_isInitialized)
                 return;
 
+            // Guard against destroyed Player during scene transitions.
+            // Vessels may outlive their Player when Netcode's destroyWithScene
+            // cleanup runs before Unity's scene-object destruction.
+            if (VesselStatus.Player is UnityEngine.Object obj && !obj)
+            {
+                _isInitialized = false;
+                return;
+            }
+
             if (InputStatus.Idle) Idle();
             else if (VesselStatus.IsSingleStickControls) PerformShipPuppetry(InputStatus.EasedLeftJoystickPosition.y, InputStatus.EasedLeftJoystickPosition.x, 0, 0);
             else PerformShipPuppetry(InputStatus.YSum, InputStatus.XSum, InputStatus.YDiff, InputStatus.XDiff);
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs
index 93e86455e..07f2b99fb 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs
@@ -331,7 +331,7 @@ namespace CosmicShore.Gameplay
             {
                 var vessel = gameData.Vessels[i];
                 if (vessel is VesselController vc && vc.IsSpawned)
-                    vc.NetworkObject.Despawn(false);
+                    vc.NetworkObject.Despawn(true);
             }
             gameData.Vessels.Clear();
 
diff --git a/Assets/_Scripts/Controller/Vessel/GunTransformer.cs b/Assets/_Scripts/Controller/Vessel/GunTransformer.cs
index 688d2a3ba..1a8b2876d 100644
--- a/Assets/_Scripts/Controller/Vessel/GunTransformer.cs
+++ b/Assets/_Scripts/Controller/Vessel/GunTransformer.cs
@@ -24,6 +24,9 @@ namespace CosmicShore.Gameplay
 
         void Update()
         {
+            if ((shipInstance as IVesselStatus)?.Player is UnityEngine.Object obj && !obj)
+                return;
+
             var i = 0;
             foreach (var child in GetComponentsInChildren<Transform>())
             {
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index 80f30ff5c..0dbd075e5 100644
--- a/Assets/_Scripts/System/SceneLoader.cs
+++ b/Assets/_Scripts/System/SceneLoader.cs
@@ -213,7 +213,7 @@ namespace CosmicShore.Core
             {
                 var vessel = gameData.Vessels[i];
                 if (vessel is VesselController vc && vc.IsSpawned)
-                    vc.NetworkObject.Despawn(false);
+                    vc.NetworkObject.Despawn(true);
             }
 
             gameData.Vessels.Clear();
```

</details>
