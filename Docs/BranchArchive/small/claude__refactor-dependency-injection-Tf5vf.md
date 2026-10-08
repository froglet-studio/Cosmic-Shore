# Branch archive: `claude/refactor-dependency-injection-Tf5vf`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-21 by Claude
- **Unmerged commits:** 1
- **Forked from:** `7349a2da3` (2026-02-21, Refactor GameSetting and AudioSystem to use Reflex DI instead of singleton acc)
- **Tip:** `f28ec9cf3`
- **Files touched (25):**
  - `Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs`
  - `Assets/_Scripts/Game/Managers/CameraManager.cs`
  - `Assets/_Scripts/Game/Managers/GameManager.cs`
  - `Assets/_Scripts/Game/Managers/NetworkGameManager.cs`
  - `Assets/_Scripts/Game/Multiplayer/ClientPlayerVesselInitializer.cs`
  - `Assets/_Scripts/Game/Ship/ClearPrisms.cs`
  - `Assets/_Scripts/Game/Ship/GunVesselTransformer.cs`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Executors/CameraZoomFollowScaleProvider.cs`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Executors/ZoomOutActionExecutor.cs`
  - `Assets/_Scripts/Game/Ship/ShipActions/ZoomOutAction.cs`
  - `Assets/_Scripts/Game/Ship/VesselCameraCustomizer.cs`
  - `Assets/_Scripts/Integrations/Playfab/Authentication/AuthenticationView.cs`
  - `Assets/_Scripts/Integrations/Playfab/Economy/CaptainManager.cs`
  - `Assets/_Scripts/Integrations/Playfab/PlayerData/PlayerDataController.cs`
  - `Assets/_Scripts/Systems/AppManager.cs`
  - `Assets/_Scripts/Systems/RewindSystem/RewindSystem.cs`
  - `Assets/_Scripts/Systems/Xp/XpHandler.cs`
  - `Assets/_Scripts/UI/Modals/ProfileModal.cs`
  - `Assets/_Scripts/UI/Views/InviteNotificationUI.cs`
  - `Assets/_Scripts/UI/Views/OnlinePlayersPanel.cs`
  - `Assets/_Scripts/UI/Views/PartyArcadeView.cs`
  - `Assets/_Scripts/UI/Views/PartyManager.cs`
  - `Assets/_Scripts/UI/Views/PlayerDataService.cs`
  - `Assets/_Scripts/Utility/DataContainers/EndGameCinematicController.cs`
  - `Assets/_Scripts/Utility/PopupManager.cs`

### `f28ec9cf3` — Refactor 6 singletons to use Reflex dependency injection

_Claude, 2026-02-21 02:28:13 +0000_

```text
Replace static singleton patterns with Reflex [Inject] field injection for:
- PlayerDataService: removed static Instance, registered via AppManager
- PartyManager: removed static Instance, registered via AppManager
- PlayerDataController: removed SingletonPersistent<T> base, registered via AppManager
  (static events and PlayerProfile kept static for broad subscriber access)
- PopupManager: removed static _instance, converted static methods to instance methods
- CameraManager: removed Singleton<T> base, registered via AppManager
- RewindSystem: removed static Instance (no external consumers)

Updated ~20 consumer files to use [Inject] instead of .Instance access.
XpHandler static methods now accept PlayerDataController as a parameter.
CaptainManager receives PlayerDataController via [Inject] and passes it through.
```

```text
 Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs      |  6 ++++--
 Assets/_Scripts/Game/Managers/CameraManager.cs                        | 11 ++++++----
 Assets/_Scripts/Game/Managers/GameManager.cs                          |  8 +++----
 Assets/_Scripts/Game/Managers/NetworkGameManager.cs                   |  4 ++--
 Assets/_Scripts/Game/Multiplayer/ClientPlayerVesselInitializer.cs     |  6 ++++--
 Assets/_Scripts/Game/Ship/ClearPrisms.cs                              |  6 +++---
 Assets/_Scripts/Game/Ship/GunVesselTransformer.cs                     | 13 ++++++------
 .../Ship/R_ShipActions/Executors/CameraZoomFollowScaleProvider.cs     |  4 +++-
 .../Game/Ship/R_ShipActions/Executors/ZoomOutActionExecutor.cs        |  8 ++++---
 Assets/_Scripts/Game/Ship/ShipActions/ZoomOutAction.cs                |  4 +++-
 Assets/_Scripts/Game/Ship/VesselCameraCustomizer.cs                   |  8 ++++---
 .../Integrations/Playfab/Authentication/AuthenticationView.cs         |  6 ++++--
 Assets/_Scripts/Integrations/Playfab/Economy/CaptainManager.cs        |  8 +++++--
 .../_Scripts/Integrations/Playfab/PlayerData/PlayerDataController.cs  | 25 +++++++++++++++-------
 Assets/_Scripts/Systems/AppManager.cs                                 | 25 ++++++++++++++++++++++
 Assets/_Scripts/Systems/RewindSystem/RewindSystem.cs                  | 21 ++++++-------------
 Assets/_Scripts/Systems/Xp/XpHandler.cs                               | 13 ++++++------
 Assets/_Scripts/UI/Modals/ProfileModal.cs                             |  7 ++++---
 Assets/_Scripts/UI/Views/InviteNotificationUI.cs                      | 23 +++++++++++---------
 Assets/_Scripts/UI/Views/OnlinePlayersPanel.cs                        | 19 ++++++++++-------
 Assets/_Scripts/UI/Views/PartyArcadeView.cs                           | 24 ++++++++++++---------
 Assets/_Scripts/UI/Views/PartyManager.cs                              | 18 +++++++++-------
 Assets/_Scripts/UI/Views/PlayerDataService.cs                         | 23 ++++++++------------
 Assets/_Scripts/Utility/DataContainers/EndGameCinematicController.cs  |  7 +++++--
 Assets/_Scripts/Utility/PopupManager.cs                               | 37 +++++++--------------------------
 25 files changed, 185 insertions(+), 149 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1119 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs b/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
index 07a293ddf..028e38c20 100644
--- a/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
+++ b/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
@@ -2,6 +2,7 @@ using System;
 using CosmicShore.Systems;
 using CosmicShore.Game.UI;
 using Cysharp.Threading.Tasks;
+using Reflex.Attributes;
 using Unity.Collections;
 using Unity.Netcode;
 using UnityEngine;
@@ -10,6 +11,7 @@ namespace CosmicShore.Game.Arcade
 {
     public abstract class MultiplayerMiniGameControllerBase : MiniGameControllerBase
     {
+        [Inject] CameraManager cameraManager;
         [Header("Multiplayer")]
         [SerializeField] protected MultiplayerSetup multiplayerSetup;
         
@@ -265,8 +267,8 @@ namespace CosmicShore.Game.Arcade
 
             // Snap player camera to the vessel's new spawn position after
             // ResetPlayers teleported it, clearing any stale cinematic position.
-            if (CameraManager.Instance)
-                CameraManager.Instance.SnapPlayerCameraToTarget();
+            if (cameraManager)
+                cameraManager.SnapPlayerCameraToTarget();
 
             if (gameData.OnResetForReplay != null)
                 gameData.OnResetForReplay.Raise();
diff --git a/Assets/_Scripts/Game/Managers/CameraManager.cs b/Assets/_Scripts/Game/Managers/CameraManager.cs
index ce60c9c94..8c38bc748 100644
--- a/Assets/_Scripts/Game/Managers/CameraManager.cs
+++ b/Assets/_Scripts/Game/Managers/CameraManager.cs
@@ -3,14 +3,17 @@ using CosmicShore.Core;
 using CosmicShore.Game;
 using CosmicShore.Game.CameraSystem;
 using CosmicShore.Soap;
-using CosmicShore.Utilities;
 using Obvious.Soap;
 using Unity.Cinemachine;
 using UnityEngine;
 using UnityEngine.SceneManagement;
 
 
-public class CameraManager : Singleton<CameraManager>
+/// <summary>
+/// Manages all game cameras (player, death, end, main menu).
+/// Registered as a value in the Reflex root container via AppManager.
+/// </summary>
+public class CameraManager : MonoBehaviour
 {
     [SerializeField]
     CellRuntimeDataSO cellData;
@@ -48,9 +51,9 @@ public class CameraManager : Singleton<CameraManager>
     private Camera _vCam;
     private IVesselStatus vesselStatus;
 
-    public override void Awake()
+    void Awake()
     {
-        base.Awake();
+        DontDestroyOnLoad(gameObject);
         _playerCamera = GetOrFindCameraController("CM PlayerCam");
         _deathCamera = GetOrFindCameraController("CM DeathCam");
         endCamera = GetOrFindCameraController("CM EndCam") as CustomCameraController;
diff --git a/Assets/_Scripts/Game/Managers/GameManager.cs b/Assets/_Scripts/Game/Managers/GameManager.cs
index 0198f0f75..e1e86c42d 100644
--- a/Assets/_Scripts/Game/Managers/GameManager.cs
+++ b/Assets/_Scripts/Game/Managers/GameManager.cs
@@ -1,20 +1,20 @@
 using System;
 using CosmicShore.Systems;
 using CosmicShore.Game;
-using CosmicShore.Utilities;
 using CosmicShore.Soap;
 using Obvious.Soap;
+using Reflex.Attributes;
 using UnityEngine;
 using UnityEngine.SceneManagement;
 using Cysharp.Threading.Tasks;
 using Unity.Netcode;
-using UnityEngine.Serialization;
 
 namespace CosmicShore.Core
 {
     [DefaultExecutionOrder(0)]
     public class GameManager : NetworkBehaviour
     {
+        [Inject] protected CameraManager cameraManager;
         const float WAIT_FOR_SECONDS_BEFORE_SCENELOAD = 0.5f;
 
         [SerializeField] SceneNameListSO _sceneNames;
@@ -41,8 +41,8 @@ namespace CosmicShore.Core
             gameData.ResetStatsDataForReplay();
             InvokeOnResetForReplay();
 
-            if (CameraManager.Instance)
-                CameraManager.Instance.SnapPlayerCameraToTarget();
+            if (cameraManager)
+                cameraManager.SnapPlayerCameraToTarget();
         }
 
         public virtual void ReturnToMainMenu() => LoadSceneAsync(_sceneNames.MainMenuScene).Forget();
diff --git a/Assets/_Scripts/Game/Managers/NetworkGameManager.cs b/Assets/_Scripts/Game/Managers/NetworkGameManager.cs
index bca6b5d37..7ed86447f 100644
--- a/Assets/_Scripts/Game/Managers/NetworkGameManager.cs
+++ b/Assets/_Scripts/Game/Managers/NetworkGameManager.cs
@@ -23,8 +23,8 @@ namespace CosmicShore.Core
         {
             InvokeOnResetForReplay();
 
-            if (CameraManager.Instance)
-                CameraManager.Instance.SnapPlayerCameraToTarget();
+            if (cameraManager)
+                cameraManager.SnapPlayerCameraToTarget();
         }
     }
 }
\ No newline at end of file
diff --git a/Assets/_Scripts/Game/Multiplayer/ClientPlayerVesselInitializer.cs b/Assets/_Scripts/Game/Multiplayer/ClientPlayerVesselInitializer.cs
index 9194ede5b..4edf5fed6 100644
--- a/Assets/_Scripts/Game/Multiplayer/ClientPlayerVesselInitializer.cs
+++ b/Assets/_Scripts/Game/Multiplayer/ClientPlayerVesselInitializer.cs
@@ -2,6 +2,7 @@
 using CosmicShore.Core;
 using CosmicShore.Soap;
 using Cysharp.Threading.Tasks;
+using Reflex.Attributes;
 using Unity.Netcode;
 using UnityEngine;
 
@@ -10,6 +11,7 @@ namespace CosmicShore.Game
 {
     public class ClientPlayerVesselInitializer : NetworkBehaviour
     {
+        [Inject] CameraManager cameraManager;
         [SerializeField] 
         ThemeManagerDataContainerSO themeManagerData;
         
@@ -82,8 +84,8 @@ namespace CosmicShore.Game
             // AddPlayer teleports the vessel to its spawn position.
             // Re-snap the camera so it starts at the correct location
             // instead of the pre-teleport position.
-            if (player.IsLocalUser && CameraManager.Instance)
-                CameraManager.Instance.SnapPlayerCameraToTarget();
+            if (player.IsLocalUser && cameraManager)
+                cameraManager.SnapPlayerCameraToTarget();
         }
```

</details>
