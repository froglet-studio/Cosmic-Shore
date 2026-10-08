# Branch archive: `claude/review-friend-system-nVgua`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-28 by Claude
- **Unmerged commits:** 1
- **Forked from:** `17d561d31` (2026-02-28, Revert "Merge pull request #263 from froglet-studio/claude/review-player-initi)
- **Tip:** `2ae6505a5`
- **Files touched (2):**
  - `Assets/_Scripts/Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs`
  - `Assets/_Scripts/Controller/Party/HostConnectionService.cs`

### `2ae6505a5` — feat(party): enable shared Menu_Main scene for party members

_Claude, 2026-02-28 06:51:39 +0000_

```text
Three key changes enable party members to roam the menu scene together:

1. Presence lobby drops WithRelayNetwork() — it's now lobby-only for
   player discovery, removing the conflict with the local Netcode host.

2. HostConnectionService transitions from local host to Relay on first
   invite (host) or invite accept (client) via PrepareForPartyNetworkAsync,
   then re-initializes menu vessels on the new Relay host.

3. MenuServerPlayerVesselInitializer supports both server and client paths:
   server spawns vessels per-player with autopilot; client subscribes to
   OnClientReady to activate autopilot and camera after RPC initialization.

Each client independently toggles between autopilot (menu browsing) and
gameplay via MenuCrystalClickHandler, which already operates on the
per-client gameData.LocalPlayer reference.
```

```text
 .../Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs       | 103 ++++++++++++++++++++++++++------
 Assets/_Scripts/Controller/Party/HostConnectionService.cs             |  59 ++++++++++++++----
 2 files changed, 131 insertions(+), 31 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 264 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs b/Assets/_Scripts/Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs
index fdbb12e46..a4f0c9a84 100644
--- a/Assets/_Scripts/Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs
@@ -1,54 +1,119 @@
+using Unity.Netcode;
 using CosmicShore.Utility;
 
 namespace CosmicShore.Gameplay
 {
     /// <summary>
-    /// Menu_Main vessel initializer. Spawns the host vessel on the network,
-    /// initializes it, then activates autopilot.
+    /// Menu_Main vessel initializer. Handles both server and client paths so
+    /// that all party members get a vessel in the shared menu scene.
     ///
-    /// Game data configuration (vessel class, player count, intensity) is handled
-    /// by <see cref="Core.MainMenuController"/> — this class only handles the
-    /// network spawn chain and autopilot activation.
+    /// Server path (host):
+    ///   OnNetworkSpawn → subscribes to OnInitializeGame (via base).
+    ///   When each player is ready → spawns vessel → activates autopilot.
     ///
-    /// Defers spawn setup until <see cref="GameDataSO.OnInitializeGame"/> fires,
-    /// because <c>OnNetworkSpawn</c> runs before <c>Start()</c> in Unity's
-    /// execution order, and <see cref="Core.MainMenuController.Start"/> is what
-    /// configures game data and raises <c>OnInitializeGame</c>.
+    /// Client path (party member):
+    ///   OnNetworkSpawn → subscribes to OnClientReady.
+    ///   When all player-vessel pairs are initialized → starts autopilot,
+    ///   sets camera to follow the local player's vessel.
     ///
-    /// Signals completion via <see cref="GameDataSO.OnMenuReady"/> SOAP event
-    /// so any system can react to the menu being fully interactive.
+    /// Each player starts in autopilot mode. Players can independently toggle
+    /// between autopilot (menu browsing) and gameplay via their own input.
+    ///
+    /// Signals <see cref="GameDataSO.OnMenuReady"/> once after the first
+    /// player is fully initialized (host vessel) so menu UI can activate.
     /// </summary>
     public class MenuServerPlayerVesselInitializer : ServerPlayerVesselInitializer
     {
+        bool _menuReadySignaled;
+
+        /// <summary>
+        /// Overrides the base to support both server and client paths.
+        /// The base disables the component on clients — we instead subscribe
+        /// to OnClientReady to handle client-side autopilot activation.
+        /// </summary>
+        protected override void OnNetworkSpawn()
+        {
+            _menuReadySignaled = false;
+
+            if (NetworkManager.Singleton.IsServer)
+            {
+                // Server: subscribe to OnInitializeGame → spawn vessels
+                base.OnNetworkSpawn();
+            }
+            else
+            {
+                // Client: wait for all player-vessel pairs to be initialized
+                // via RPCs, then activate autopilot for every player.
+                gameData.OnClientReady.OnRaised += HandleClientReady;
+            }
+        }
+
+        protected override void OnNetworkDespawn()
+        {
+            gameData.OnClientReady.OnRaised -= HandleClientReady;
+            base.OnNetworkDespawn();
+        }
+
         /// <summary>
-        /// Menu override: after the base spawns + initializes the vessel, activate autopilot.
+        /// Server: after the base spawns + initializes a vessel, start that
+        /// player in autopilot mode. Only configures the camera for the local
+        /// (host) player's vessel.
         /// </summary>
         protected override void OnPlayerReadyToSpawn(Player player)
         {
             base.OnPlayerReadyToSpawn(player);
-            ActivateAutopilot(player);
+            ActivatePlayerAutopilot(player);
         }
 
-        void ActivateAutopilot(Player player)
+        void ActivatePlayerAutopilot(Player player)
         {
             if (player?.Vessel == null)
             {
-                CSDebug.LogError("[MenuServerVesselInit] LocalPlayer or Vessel not available after initialization.");
+                CSDebug.LogError("[MenuServerVesselInit] Player or Vessel not available after initialization.");
                 return;
             }
 
-            gameData.SetPlayersActive();
-
+            // Start only this player (not all players) to avoid restarting
+            // previously initialized players and disrupting their state.
+            player.StartPlayer();
             player.Vessel.ToggleAIPilot(true);
             player.InputController.SetPause(true);
 
-            if (CameraManager.Instance)
+            // Camera follows the local (host) player's vessel only
+            if (player.IsLocalUser && CameraManager.Instance)
             {
                 var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;
                 CameraManager.Instance.SetupEndCameraFollow(followTarget);
             }
 
-            gameData.InvokeMenuReady();
+            if (!_menuReadySignaled)
+            {
+                _menuReadySignaled = true;
+                gameData.InvokeMenuReady();
+            }
+        }
+
+        /// <summary>
+        /// Client: all player-vessel pairs have been initialized via RPCs.
+        /// Start every player in autopilot mode and set the camera to follow
+        /// the local player's vessel.
+        /// </summary>
+        void HandleClientReady()
+        {
+            gameData.OnClientReady.OnRaised -= HandleClientReady;
+
+            foreach (var player in gameData.Players)
+            {
+                player.StartPlayer();
+                player.Vessel?.ToggleAIPilot(true);
+                player.InputController?.SetPause(true);
+            }
+
+            if (gameData.LocalPlayer?.Vessel != null && CameraManager.Instance)
+            {
+                var followTarget = gameData.LocalPlayer.Vessel.VesselStatus.CameraFollowTarget;
+                CameraManager.Instance.SetupEndCameraFollow(followTarget);
+            }
         }
     }
 }
diff --git a/Assets/_Scripts/Controller/Party/HostConnectionService.cs b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
index 886bd81bc..50df24ba1 100644
--- a/Assets/_Scripts/Controller/Party/HostConnectionService.cs
+++ b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
@@ -17,6 +17,10 @@ namespace CosmicShore.Gameplay
     ///
     /// Writes all state into <see cref="HostConnectionDataSO"/> so every UI
```

</details>
