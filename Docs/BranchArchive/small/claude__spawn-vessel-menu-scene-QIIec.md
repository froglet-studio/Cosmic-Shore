# Branch archive: `claude/spawn-vessel-menu-scene-QIIec`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-28 by Claude
- **Unmerged commits:** 1
- **Forked from:** `f45146eeb` (2026-02-28, Merge pull request #265 from froglet-studio/claude/restore-spawn-scripts-ztzlG)
- **Tip:** `22f1100da`
- **Files touched (2):**
  - `Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs`
  - `Assets/_Scripts/System/AuthenticationSceneController.cs`

### `22f1100da` — fix(menu): ensure vessel spawns as Squirrel in Menu_Main on every host start

_Claude, 2026-02-28 01:21:46 +0000_

```text
Two timing issues prevented reliable vessel spawning in the menu scene:

1. Stale vessel class: When returning from gameplay, the Player object
   spawned by StartHost() would read selectedVesselClass before
   MainMenuController configured it, picking up the previous session's
   vessel type. Reset selectedVesselClass to Random before StartHost()
   in both MultiplayerSetup and AuthenticationSceneController so the
   Player defers to MainMenuController.ConfigureMenuGameData().

2. Execution order: MultiplayerSetup.Start() and MainMenuController.Start()
   had no guaranteed ordering. If MainMenuController fired InitializeGame
   before the host was running, ServerPlayerVesselInitializer missed the
   event. Add [DefaultExecutionOrder(-10)] to MultiplayerSetup so the
   host is always ready before MainMenuController.Start() runs.
```

```text
 Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs | 18 ++++++++++++++++++
 Assets/_Scripts/System/AuthenticationSceneController.cs    |  7 +++++++
 2 files changed, 25 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs b/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
index e93b5a724..48a8c3ef2 100644
--- a/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
@@ -6,11 +6,20 @@ using Cysharp.Threading.Tasks;
 using Unity.Netcode;
 using Unity.Services.Authentication;
 using Unity.Services.Multiplayer;
+using CosmicShore.Data;
 using CosmicShore.Utility;
 using Reflex.Attributes;
 using CosmicShore.ScriptableObjects;
 namespace CosmicShore.Gameplay
 {
+    /// <summary>
+    /// Handles network host startup and multiplayer session management.
+    ///
+    /// Execution order is set to -10 so Start() runs before MainMenuController (default 0).
+    /// This ensures the Netcode host is running and scene-placed NetworkBehaviours have
+    /// received OnNetworkSpawn before MainMenuController.Start() fires InitializeGame.
+    /// </summary>
+    [DefaultExecutionOrder(-10)]
     public class MultiplayerSetup : MonoBehaviour
     {
         const string PLAYER_NAME_PROPERTY_KEY = "playerName";
@@ -123,6 +132,15 @@ namespace CosmicShore.Gameplay
                     return;
                 }
 
+                // Reset vessel class to Random before starting the host so the
+                // Player object spawned by StartHost() defers its vessel type
+                // assignment. The scene controller (MainMenuController) will set
+                // the actual vessel class in its Start(), which fires the
+                // OnValueChanged callback the Player is waiting for.
+                // Without this, the Player could pick up a stale value from a
+                // previous gameplay session and commit to the wrong vessel type.
+                gameData.selectedVesselClass.Value = VesselClassType.Random;
+
                 CSDebug.Log("[MultiplayerSetup] Starting as Host.");
                 nm.StartHost();
             }
diff --git a/Assets/_Scripts/System/AuthenticationSceneController.cs b/Assets/_Scripts/System/AuthenticationSceneController.cs
index 3ee4bb67c..13b9997b0 100644
--- a/Assets/_Scripts/System/AuthenticationSceneController.cs
+++ b/Assets/_Scripts/System/AuthenticationSceneController.cs
@@ -59,6 +59,7 @@ namespace CosmicShore.Core
         [Inject] private AuthenticationServiceFacade _facade;
         [Inject] private AuthenticationDataVariable _authDataVariable;
         [Inject] private PlayerDataService _playerDataService;
+        [Inject] private GameDataSO _gameData;
         [Inject] private SceneNameListSO _sceneNames;
         [Inject] private SceneTransitionManager _sceneTransitionManager;
         [Inject] private ApplicationStateMachine _appStateMachine;
@@ -506,6 +507,12 @@ namespace CosmicShore.Core
             // Register connection approval so the host's player object is created.
             nm.ConnectionApprovalCallback += OnConnectionApproval;
 
+            // Reset vessel class so the Player spawned by StartHost() defers
+            // to MainMenuController.ConfigureMenuGameData() instead of
+            // committing to a stale value from a previous session or bootstrap.
+            if (_gameData != null)
+                _gameData.selectedVesselClass.Value = VesselClassType.Random;
+
             CSDebug.Log("[AuthScene] Starting network host...");
             nm.StartHost();
 
```

</details>
