# Branch archive: `claude/fix-hex-race-multiplayer-6mQs3`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-03-03 by Claude
- **Unmerged commits:** 4
- **Forked from:** `c99fec59e` (2026-03-04, Update MinigameHexRace.unity)
- **Tip:** `332c093f4`
- **Files touched (4):**
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs`
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs`
  - `Assets/_Scripts/Controller/Player/Player.cs`
  - `Assets/_Scripts/System/SceneLoader.cs`

### `286bd6d53` — fix(multiplayer): prevent Invalid Destroy errors on client during scene transition

_Claude, 2026-03-03 21:36:41 +0000_

```text
When transitioning from Menu_Main to a game scene (e.g. HexRace) with
a party of 2 players, the client hit "[Invalid Destroy] Destroy a spawned
NetworkObject on a non-host client is not valid" for both the vessel and
Player objects. This happened because Unity's scene unload destroyed the
NetworkObjects on the client before the server's despawn messages arrived.

Two changes fix this:

1. Player.OnNetworkSpawn() now calls DontDestroyOnLoad(gameObject) so
   Player objects persist across Netcode scene loads. They are already
   rediscovered by ProcessPreExistingPlayers() via ConnectedClients
   after each transition.

2. SceneLoader.LoadSceneAsync() now explicitly despawns all spawned
   vessels on the server before the network scene load. This sends
   despawn messages to clients during the existing waitBeforeLoading
   delay, so by the time clients unload the old scene, the vessels
   are already properly despawned.
```

```text
 Assets/_Scripts/Controller/Player/Player.cs |  7 +++++++
 Assets/_Scripts/System/SceneLoader.cs       | 34 ++++++++++++++++++++++++++++++++++
 2 files changed, 41 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index 09b542fe2..908c46e8d 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -122,6 +122,13 @@ namespace CosmicShore.Gameplay
 
         public override void OnNetworkSpawn()
         {
+            // Players must persist across Netcode scene loads (Menu → Game, Game → Menu).
+            // Move to DontDestroyOnLoad immediately so Unity's scene unload does not
+            // destroy them before the server's despawn messages arrive on clients.
+            // ProcessPreExistingPlayers() in ServerPlayerVesselInitializer rediscovers
+            // persistent Players via nm.ConnectedClients[].PlayerObject after each transition.
+            DontDestroyOnLoad(gameObject);
+
             Debug.Log($"<color=#00FF00>[FLOW-4] [Player] OnNetworkSpawn — OwnerClientId={OwnerClientId}, NetworkObjectId={NetworkObjectId}, IsOwner={IsOwner}, IsServer={IsServer}</color>");
             base.OnNetworkSpawn();
 
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index aee9696d9..669ae7590 100644
--- a/Assets/_Scripts/System/SceneLoader.cs
+++ b/Assets/_Scripts/System/SceneLoader.cs
@@ -1,4 +1,5 @@
 using System;
+using System.Collections.Generic;
 using CosmicShore.Data;
 using CosmicShore.Gameplay;
 using CosmicShore.Utility;
@@ -143,6 +144,15 @@ namespace CosmicShore.Core
         {
             Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] LoadSceneAsync — sceneName={sceneName}, network={useNetworkSceneLoading}, waitBeforeLoading={waitBeforeLoading}s</color>");
             gameData.InvokeSceneTransition(false);
+
+            // Server: explicitly despawn scene-bound vessels before the scene load.
+            // Without this, the client-side scene unload destroys NetworkObjects before
+            // the server's despawn messages arrive, causing "Invalid Destroy" errors.
+            // Must run before ResetRuntimeData() so the waitBeforeLoading delay below
+            // gives time for despawn messages to propagate to clients.
+            if (useNetworkSceneLoading)
+                DespawnSceneVesselsOnServer();
+
             gameData.ResetRuntimeData();
             Debug.Log("<color=#FF8C00>[FLOW-3] [SceneLoader] ResetRuntimeData done. Waiting before load...</color>");
 
@@ -193,6 +203,30 @@ namespace CosmicShore.Core
             nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
         }
 
+        /// <summary>
+        /// Server-only: despawns all spawned vessel NetworkObjects so that despawn
+        /// messages reach clients before the network scene load event. Without this,
+        /// clients hit "[Invalid Destroy] Destroy a spawned NetworkObject on a
+        /// non-host client is not valid" during the scene unload.
+        /// </summary>
+        void DespawnSceneVesselsOnServer()
+        {
+            var nm = NetworkManager.Singleton;
+            if (nm == null || !nm.IsServer) return;
+
+            var toDespawn = new List<NetworkObject>();
+            foreach (var no in nm.SpawnManager.SpawnedObjectsList)
+            {
+                if (no != null && no.IsSpawned && no.TryGetComponent<VesselController>(out _))
+                    toDespawn.Add(no);
+            }
+
+            foreach (var no in toDespawn)
+                no.Despawn(true);
+
+            Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] DespawnSceneVesselsOnServer — despawned {toDespawn.Count} vessel(s)</color>");
+        }
+
         #endregion
 
         #region Restart / Replay
```

</details>

### `32b276c0f` — fix(multiplayer): use Despawn(false) and move despawn to right before scene load

_Claude, 2026-03-03 21:52:25 +0000_

```text
The first fix attempt used Despawn(true) before the 0.5s delay, which:
- Destroyed vessels on the server while in-flight NetworkVariable/Transform
  messages still referenced them (MissingReferenceException)
- Left a window where new vessels could spawn during the delay

Now:
- Despawn(false) removes vessels from SpawnedObjects tracking WITHOUT
  destroying the GameObjects. They stay alive until Unity's scene unload.
  NetworkObject.OnDestroy() no longer finds them in SpawnedObjects and
  skips the "Invalid Destroy" error path on clients.
- Moved the despawn call to immediately before LoadNetworkSceneOnServer()
  so there's no gap between despawn and scene load.
```

```text
 Assets/_Scripts/System/SceneLoader.cs | 22 ++++++++++++----------
 1 file changed, 12 insertions(+), 10 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index 669ae7590..dc550a110 100644
--- a/Assets/_Scripts/System/SceneLoader.cs
+++ b/Assets/_Scripts/System/SceneLoader.cs
@@ -144,15 +144,6 @@ namespace CosmicShore.Core
         {
             Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] LoadSceneAsync — sceneName={sceneName}, network={useNetworkSceneLoading}, waitBeforeLoading={waitBeforeLoading}s</color>");
             gameData.InvokeSceneTransition(false);
-
-            // Server: explicitly despawn scene-bound vessels before the scene load.
-            // Without this, the client-side scene unload destroys NetworkObjects before
-            // the server's despawn messages arrive, causing "Invalid Destroy" errors.
-            // Must run before ResetRuntimeData() so the waitBeforeLoading delay below
-            // gives time for despawn messages to propagate to clients.
-            if (useNetworkSceneLoading)
-                DespawnSceneVesselsOnServer();
-
             gameData.ResetRuntimeData();
             Debug.Log("<color=#FF8C00>[FLOW-3] [SceneLoader] ResetRuntimeData done. Waiting before load...</color>");
 
@@ -181,6 +172,17 @@ namespace CosmicShore.Core
 
             if (nm.IsServer)
             {
+                // Despawn all vessels immediately before the scene load.
+                // Uses Despawn(false) to remove objects from Netcode's SpawnedObjects
+                // tracking WITHOUT destroying the GameObjects. The objects remain alive
+                // until Unity's scene unload destroys them, at which point
+                // NetworkObject.OnDestroy() no longer finds them in SpawnedObjects
+                // and skips the "Invalid Destroy" error path.
+                //
+                // Despawning here (not earlier) ensures no vessels are spawned between
+                // the despawn and the scene load. Netcode's reliable channel guarantees
+                // the despawn messages are ordered before the scene load event on clients.
+                DespawnSceneVesselsOnServer();
                 LoadNetworkSceneOnServer(sceneName);
             }
             else
@@ -222,7 +224,7 @@ namespace CosmicShore.Core
             }
 
             foreach (var no in toDespawn)
-                no.Despawn(true);
+                no.Despawn(false);
 
             Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] DespawnSceneVesselsOnServer — despawned {toDespawn.Count} vessel(s)</color>");
         }
```

</details>

### `2d3c96969` — fix(multiplayer): persist vessels across scene transitions to prevent Invalid Destroy

_Claude, 2026-03-03 23:27:22 +0000_

```text
Vessels were spawned with destroyWithScene=true, causing Unity's scene unload
to destroy them on clients before Netcode could process despawn messages. This
produced "[Invalid Destroy] Destroy a spawned NetworkObject on a non-host client
is not valid" errors during Menu_Main → HexRace transitions.

Following the same pattern used for Players (DontDestroyOnLoad + rediscovery in
new scene), vessels now use destroyWithScene=false so Netcode migrates them to
DDOL during scene transitions. The new DespawnStaleVessels() method in
ServerPlayerVesselInitializer cleans up migrated vessels before spawning new ones.

- SpawnWithOwnership(clientId, false) for human vessels
- Spawn(false) for AI players and vessels
- DespawnStaleVessels() in OnNetworkSpawn() cleans up old vessels
- Removed DespawnSceneVesselsOnServer() from SceneLoader (no longer needed)
```

```text
 .../_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs  | 31 ++++++++++++++++++++++++++-
 .../Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs     |  4 ++--
 Assets/_Scripts/Controller/Player/Player.cs                           |  2 +-
 Assets/_Scripts/System/SceneLoader.cs                                 | 37 ---------------------------------
 4 files changed, 33 insertions(+), 41 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
index 8f96fe272..5c74cbddb 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
@@ -93,6 +93,11 @@ namespace CosmicShore.Gameplay
                 return;
             }
 
+            // Clean up vessels that persisted from the previous scene.
+            // With destroyWithScene=false, Netcode migrates vessels to DDOL during
+            // scene transitions. Despawn them here before spawning new ones.
+            DespawnStaleVessels();
+
             Debug.Log($"<color=#00FF00>[FLOW-5] [ServerVesselInit] OnNetworkSpawn — IsServer=true, subscribing to OnPlayerNetworkSpawnedUlong. gameData.Players.Count={gameData.Players.Count}</color>");
 
             if (playerSpawnPoints != null && playerSpawnPoints.Length > 0)
@@ -300,11 +305,35 @@ namespace CosmicShore.Gameplay
 
             var networkVessel = Instantiate(shipNetworkObject);
             GameObjectInjector.InjectRecursive(networkVessel.gameObject, _container);
-            networkVessel.SpawnWithOwnership(clientId, true);
+            networkVessel.SpawnWithOwnership(clientId, false);
             networkPlayer.NetVesselId.Value = networkVessel.NetworkObjectId;
             return networkVessel;
         }
 
+        /// <summary>
+        /// Despawns vessels that persisted from a previous scene via DontDestroyOnLoad.
+        /// With destroyWithScene=false, Netcode migrates vessels to DDOL during scene
+        /// transitions. This method cleans them up before new vessels are spawned.
+        /// </summary>
+        void DespawnStaleVessels()
+        {
+            var nm = NetworkManager.Singleton;
+            if (nm == null || !nm.IsServer) return;
+
+            var staleVessels = new List<NetworkObject>();
+            foreach (var no in nm.SpawnManager.SpawnedObjectsList)
+            {
+                if (no != null && no.IsSpawned && no.TryGetComponent<VesselController>(out _))
+                    staleVessels.Add(no);
+            }
+
+            foreach (var no in staleVessels)
+                no.Despawn(true);
+
+            if (staleVessels.Count > 0)
+                Debug.Log($"<color=#00FF00>[FLOW-5] [ServerVesselInit] DespawnStaleVessels — despawned {staleVessels.Count} vessel(s) from previous scene</color>");
+        }
+
         /// <summary>
         /// Despawns and destroys a vessel's <see cref="NetworkObject"/>.
         /// Removes it from <see cref="GameDataSO.Vessels"/> tracking.
diff --git a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
index 1169c863c..1249847f4 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
@@ -122,7 +122,7 @@ namespace CosmicShore.Gameplay
                 var aiPlayerNO = Instantiate(aiPlayerPrefab);
                 GameObjectInjector.InjectRecursive(aiPlayerNO.gameObject, _container);
 
-                aiPlayerNO.Spawn(true);
+                aiPlayerNO.Spawn(false);
 
                 var aiPlayer = aiPlayerNO.GetComponent<Player>();
                 if (!aiPlayer)
@@ -214,7 +214,7 @@ namespace CosmicShore.Gameplay
 
             vesselNO = Instantiate(shipNetworkObject);
             GameObjectInjector.InjectRecursive(vesselNO.gameObject, _container);
-            vesselNO.Spawn(true);
+            vesselNO.Spawn(false);
             aiPlayer.NetVesselId.Value = vesselNO.NetworkObjectId;
             return true;
         }
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index 908c46e8d..3510a8566 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -236,7 +236,7 @@ namespace CosmicShore.Gameplay
         {
             Debug.Log($"<color=#00FF00>[FLOW-4] [Player] PrepareForNewScene — OwnerClientId={OwnerClientId}, NetworkObjectId={NetworkObjectId}, IsOwner={IsOwner}</color>");
             // Clear stale references from previous scene.
-            // Vessels have destroyWithScene=true and are already destroyed.
+            // Old vessels are despawned by DespawnStaleVessels() in the new scene's spawner.
             Vessel = null;
             IsActive = false;
             VesselNetId = 0;
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index dc550a110..2e907ac56 100644
--- a/Assets/_Scripts/System/SceneLoader.cs
+++ b/Assets/_Scripts/System/SceneLoader.cs
@@ -1,7 +1,5 @@
 using System;
-using System.Collections.Generic;
 using CosmicShore.Data;
-using CosmicShore.Gameplay;
 using CosmicShore.Utility;
 using Cysharp.Threading.Tasks;
 using Reflex.Attributes;
@@ -172,17 +170,6 @@ namespace CosmicShore.Core
 
             if (nm.IsServer)
             {
-                // Despawn all vessels immediately before the scene load.
-                // Uses Despawn(false) to remove objects from Netcode's SpawnedObjects
-                // tracking WITHOUT destroying the GameObjects. The objects remain alive
-                // until Unity's scene unload destroys them, at which point
-                // NetworkObject.OnDestroy() no longer finds them in SpawnedObjects
-                // and skips the "Invalid Destroy" error path.
-                //
-                // Despawning here (not earlier) ensures no vessels are spawned between
-                // the despawn and the scene load. Netcode's reliable channel guarantees
-                // the despawn messages are ordered before the scene load event on clients.
-                DespawnSceneVesselsOnServer();
                 LoadNetworkSceneOnServer(sceneName);
             }
             else
@@ -205,30 +192,6 @@ namespace CosmicShore.Core
             nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
         }
 
-        /// <summary>
-        /// Server-only: despawns all spawned vessel NetworkObjects so that despawn
-        /// messages reach clients before the network scene load event. Without this,
-        /// clients hit "[Invalid Destroy] Destroy a spawned NetworkObject on a
-        /// non-host client is not valid" during the scene unload.
-        /// </summary>
-        void DespawnSceneVesselsOnServer()
-        {
-            var nm = NetworkManager.Singleton;
-            if (nm == null || !nm.IsServer) return;
-
-            var toDespawn = new List<NetworkObject>();
-            foreach (var no in nm.SpawnManager.SpawnedObjectsList)
-            {
-                if (no != null && no.IsSpawned && no.TryGetComponent<VesselController>(out _))
-                    toDespawn.Add(no);
-            }
-
-            foreach (var no in toDespawn)
-                no.Despawn(false);
-
-            Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] DespawnSceneVesselsOnServer — despawned {toDespawn.Count} vessel(s)</color>");
-        }
-
         #endregion
 
         #region Restart / Replay
```

</details>

### `332c093f4` — fix(build): restore CosmicShore.Gameplay using for CameraManager in SceneLoader

_Claude, 2026-03-03 23:37:05 +0000_

```text
 Assets/_Scripts/System/SceneLoader.cs | 1 +
 1 file changed, 1 insertion(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index 2e907ac56..aee9696d9 100644
--- a/Assets/_Scripts/System/SceneLoader.cs
+++ b/Assets/_Scripts/System/SceneLoader.cs
@@ -1,5 +1,6 @@
 using System;
 using CosmicShore.Data;
+using CosmicShore.Gameplay;
 using CosmicShore.Utility;
 using Cysharp.Threading.Tasks;
 using Reflex.Attributes;
```

</details>
