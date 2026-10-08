# Branch archive: `claude/fix-hex-race-spawning-SuO1L`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-03-04 by Claude
- **Unmerged commits:** 7
- **Forked from:** `e43b80025` (2026-03-04, Merge pull request #342 from froglet-studio/claude/hex-race-unique-domains-7HB)
- **Tip:** `3dfb63fc8`
- **Files touched (6):**
  - `Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs`
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs`
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs`
  - `Assets/_Scripts/Controller/Player/Player.cs`
  - `Assets/_Scripts/Controller/Vessel/VesselPrismController.cs`
  - `Assets/_Scripts/System/SceneLoader.cs`

### `ddc289e07` — fix(multiplayer): persistent AI players, pre-despawn vessels on scene transition

_Claude, 2026-03-04 02:50:20 +0000_

```text
- Player.cs: Add PreInitializeAsAI() to mark AI before Spawn(), preventing
  Owner writes block from leaking human identity to AI NetworkVariables.
  Guard Owner writes with !IsInitializedAsAI.

- ServerPlayerVesselInitializerWithAI.cs: Refactor SpawnAIs() into
  EnsureAIPlayersAndVessels() — finds persistent AI players from previous
  scenes, reconfigures them, and spawns new vessels. Creates new AI only
  when needed. AI players now use Spawn(false) for persistence across
  scene transitions. Only vessels are scene-bound.

- ServerPlayerVesselInitializer.cs: Filter AI players from
  ProcessPreExistingPlayers Stage 1 and FindUnprocessedPlayerByOwnerClientId
  to prevent base class from processing AI (which share host's OwnerClientId).

- SceneLoader.cs: Add PreDespawnVessels() before ResetRuntimeData() during
  network scene loads. Explicitly despawns all tracked vessels server-side
  before scene unload, preventing "Invalid Destroy on non-host client" errors
  when client-side scene unload races Netcode despawn messages.
```

```text
 .../_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs  |  12 ++-
 .../Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs     | 186 +++++++++++++++++++++++---------
 Assets/_Scripts/Controller/Player/Player.cs                           |  16 ++-
 Assets/_Scripts/System/SceneLoader.cs                                 |  27 +++++
 4 files changed, 189 insertions(+), 52 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 371 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
index b3e3b39de..3a5210b08 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
@@ -111,9 +111,16 @@ namespace CosmicShore.Gameplay
         {
             // Stage 1: Check gameData.Players (catches players spawned in THIS scene,
             // e.g. AI players whose OnNetworkSpawn() already added them).
+            // Skip AI players — they are handled exclusively by
+            // ServerPlayerVesselInitializerWithAI. AI shares the host's
+            // OwnerClientId, so processing them here would cause
+            // FindUnprocessedPlayerByOwnerClientId to return an AI player
+            // instead of the human player.
             foreach (var p in gameData.Players)
             {
-                if (p is Player netPlayer && netPlayer.IsSpawned)
+                if (p is Player netPlayer && netPlayer.IsSpawned
+                    && !_processedPlayers.Contains(netPlayer.NetworkObjectId)
+                    && !netPlayer.NetIsAI.Value)
                     HandlePlayerNetworkSpawned(netPlayer.OwnerClientId);
             }
 
@@ -338,7 +345,8 @@ namespace CosmicShore.Gameplay
                 if (p is Player netPlayer
                     && netPlayer.IsSpawned
                     && netPlayer.OwnerClientId == ownerClientId
-                    && !_processedPlayers.Contains(netPlayer.NetworkObjectId))
+                    && !_processedPlayers.Contains(netPlayer.NetworkObjectId)
+                    && !netPlayer.NetIsAI.Value)
                 {
                     return netPlayer;
                 }
diff --git a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
index c7ad473c2..72ab8c1f3 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
@@ -1,3 +1,4 @@
+using System.Collections.Generic;
 using CosmicShore.Data;
 using CosmicShore.ScriptableObjects;
 using CosmicShore.Utility;
@@ -9,14 +10,17 @@ namespace CosmicShore.Gameplay
 {
     /// <summary>
     /// Extension of ServerPlayerVesselInitializer:
-    /// spawns server-owned AI players and their vessels, then delegates
-    /// human player handling to the base class via OnPlayerNetworkSpawnedUlong.
+    /// ensures AI players exist (persistent across scene transitions) and spawns
+    /// their vessels, then delegates human player handling to the base class.
     ///
     /// OnNetworkSpawn flow:
-    ///   1. SpawnAIs() — creates AI players + vessels (fires OnPlayerNetworkSpawnedUlong
-    ///      for each, but we haven't subscribed yet so the base ignores them)
+    ///   1. EnsureAIPlayersAndVessels() — finds persistent AI or creates new ones,
+    ///      spawns vessels, initializes pairs
     ///   2. Mark AI players in _processedPlayers so the base never processes them
     ///   3. base.OnNetworkSpawn() — subscribes to event + handles human players going forward
+    ///
+    /// AI players are spawned with DestroyWithScene=false so they persist across
+    /// scene transitions. Only vessels are destroyed with the scene and respawned.
     /// </summary>
     public class ServerPlayerVesselInitializerWithAI : ServerPlayerVesselInitializer
     {
@@ -58,8 +62,8 @@ namespace CosmicShore.Gameplay
             if (playerSpawnPoints != null && playerSpawnPoints.Length > 0)
                 gameData.SetSpawnPositions(playerSpawnPoints);
 
-            // Spawn AIs BEFORE subscribing to OnPlayerNetworkSpawnedUlong.
-            // AI players fire the event during Spawn(), but since we haven't
+            // Ensure AI players + vessels BEFORE subscribing to OnPlayerNetworkSpawnedUlong.
+            // New AI players fire the event during Spawn(), but since we haven't
             // subscribed yet (base.OnNetworkSpawn hasn't run), those events
             // are harmlessly ignored by the base.
             // Wrapped in try-catch to guarantee base.OnNetworkSpawn() always
@@ -68,14 +72,14 @@ namespace CosmicShore.Gameplay
             {
                 try
                 {
-                    Debug.Log("<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Calling SpawnAIs()</color>");
-                    SpawnAIs();
-                    Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] SpawnAIs() complete. gameData.Players.Count={gameData.Players.Count}</color>");
+                    Debug.Log("<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Calling EnsureAIPlayersAndVessels()</color>");
+                    EnsureAIPlayersAndVessels();
+                    Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] EnsureAIPlayersAndVessels() complete. gameData.Players.Count={gameData.Players.Count}</color>");
                 }
                 catch (System.Exception e)
                 {
-                    Debug.LogError($"<color=#FF0000>[FLOW-5AI] [ServerVesselInitWithAI] SpawnAIs FAILED: {e.Message}\n{e.StackTrace}</color>");
-                    CSDebug.LogError($"[ServerPlayerVesselInitializerWithAI] SpawnAIs failed: {e.Message}");
+                    Debug.LogError($"<color=#FF0000>[FLOW-5AI] [ServerVesselInitWithAI] EnsureAIPlayersAndVessels FAILED: {e.Message}\n{e.StackTrace}</color>");
+                    CSDebug.LogError($"[ServerPlayerVesselInitializerWithAI] EnsureAIPlayersAndVessels failed: {e.Message}");
                 }
             }
 
@@ -95,7 +99,13 @@ namespace CosmicShore.Gameplay
             base.OnNetworkSpawn();
         }
 
-        void SpawnAIs()
+        /// <summary>
+        /// Finds persistent AI players from previous scene transitions or creates
+        /// new ones. Spawns vessels for all AI players and initializes pairs.
+        /// AI players are persistent (DestroyWithScene=false); only vessels are
+        /// scene-bound (DestroyWithScene=true).
+        /// </summary>
+        void EnsureAIPlayersAndVessels()
         {
             if (!aiPlayerPrefab)
             {
@@ -105,67 +115,145 @@ namespace CosmicShore.Gameplay
             }
 
             int aiCount = gameData.EnsureMinimumAIBackfill();
-            Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] SpawnAIs — aiCount={aiCount}</color>");
+            Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] EnsureAIPlayersAndVessels — aiCount={aiCount}</color>");
             if (aiCount <= 0)
             {
-                Debug.Log("<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] No AI to spawn (aiCount <= 0)</color>");
+                Debug.Log("<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] No AI needed (aiCount <= 0)</color>");
                 return;
             }
 
             // Use AI profile list for names when available; fall back to aiInitializeDatas templates.
-            System.Collections.Generic.List<AIProfile> profiles = null;
+            List<AIProfile> profiles = null;
             if (aiProfileList != null)
                 profiles = aiProfileList.PickRandom(aiCount);
 
-            for (int i = 0; i < aiCount; i++)
+            // Find persistent AI players from previous scene (survive because Spawn(false))
+            var existingAI = FindPersistentAIPlayers();
+            Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Found {existingAI.Count} persistent AI players</color>");
+
+            // Despawn excess AI if fewer needed now
+            while (existingAI.Count > aiCount)
+            {
+                var excess = existingAI[existingAI.Count - 1];
+                existingAI.RemoveAt(existingAI.Count - 1);
+                Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Despawning excess AI: {excess.NetName.Value}</color>");
+                excess.NetworkObject.Despawn(true);
+            }
+
+            // Reconfigure existing persistent AI — prepare for new scene, spawn vessels
+            for (int i = 0; i < existingAI.Count; i++)
             {
-                var aiPlayerNO = Instantiate(aiPlayerPrefab);
-                GameObjectInjector.InjectRecursive(aiPlayerNO.gameObject, _container);
+                var aiPlayer = existingAI[i];
+                Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Reconfiguring persistent AI[{i}]: {aiPlayer.NetName.Value}</color>");
+
```

</details>

### `b6b72ba47` — fix(multiplayer): add SpawnManager fallback to PreDespawnVessels

_Claude, 2026-03-04 03:00:19 +0000_

```text
Scan SpawnManager.SpawnedObjects as fallback to catch any VesselController
not tracked in gameData.Vessels. Ensures all vessels are despawned
server-side before network scene load, preventing "Invalid Destroy on
non-host client" errors.
```

```text
 Assets/_Scripts/System/SceneLoader.cs | 16 ++++++++++++++++
 1 file changed, 16 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index 85bf3d0e1..5c2fcfcc3 100644
--- a/Assets/_Scripts/System/SceneLoader.cs
+++ b/Assets/_Scripts/System/SceneLoader.cs
@@ -131,6 +131,7 @@ namespace CosmicShore.Core
         /// cleaned up. This prevents "Invalid Destroy on non-host client" errors
         /// that occur when client-side scene unload triggers Destroy() on vessels
         /// before Netcode's despawn messages arrive.
+        /// Clients must never call Despawn/Destroy on spawned NetworkObjects they don't own.
         /// </summary>
         void PreDespawnVessels()
         {
@@ -138,11 +139,26 @@ namespace CosmicShore.Core
             if (nm == null || !nm.IsServer) return;
 
             Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] PreDespawnVessels — Vessels.Count={gameData.Vessels.Count}</color>");
+
+            // Primary path: despawn all tracked vessels
             for (int i = gameData.Vessels.Count - 1; i >= 0; i--)
             {
                 if (gameData.Vessels[i] is VesselController vc && vc.IsSpawned)
                     vc.NetworkObject.Despawn(true);
             }
+
+            // Fallback: scan SpawnManager for any VesselController not in gameData.Vessels
+            // (e.g. vessels added to scene but not yet registered via OnNetworkSpawn)
+            if (nm.SpawnManager != null)
+            {
+                foreach (var kvp in nm.SpawnManager.SpawnedObjects)
+                {
+                    if (kvp.Value != null
+                        && kvp.Value.TryGetComponent<VesselController>(out var vc)
+                        && vc.IsSpawned)
+                        vc.NetworkObject.Despawn(true);
+                }
+            }
         }
 
         /// <summary>
```

</details>

### `f511a8f07` — fix(multiplayer): spawn vessels with destroyWithScene=false to eliminate Invalid Destroy race

_Claude, 2026-03-04 03:33:46 +0000_

```text
Vessels spawned with destroyWithScene=true are auto-destroyed by Unity's
scene unload on all clients. Non-host clients can't destroy spawned
NetworkObjects, causing "Invalid Destroy" errors. PreDespawnVessels()
alone can't fix this — there's a timing race between despawn messages
and scene unload messages arriving at clients on different Netcode channels.

Changes:
- ServerPlayerVesselInitializer: SpawnWithOwnership(clientId, false)
- ServerPlayerVesselInitializerWithAI: vesselNO.Spawn(false)
- SceneLoader.PreDespawnVessels: collect-then-despawn to avoid
  modifying SpawnedObjects dictionary during iteration
- Player.PrepareForNewScene: update comment to reflect new lifecycle

With destroyWithScene=false, scene unload can't auto-destroy vessels.
PreDespawnVessels() sends server-authoritative despawn messages that
clients process whenever they arrive — no race condition.
```

```text
 Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs       |  2 +-
 Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs |  8 +++++---
 Assets/_Scripts/Controller/Player/Player.cs                                   |  2 +-
 Assets/_Scripts/System/SceneLoader.cs                                         | 12 ++++++++----
 4 files changed, 15 insertions(+), 9 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
index 3a5210b08..45b2e8c64 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
@@ -314,7 +314,7 @@ namespace CosmicShore.Gameplay
 
             var networkVessel = Instantiate(shipNetworkObject);
             GameObjectInjector.InjectRecursive(networkVessel.gameObject, _container);
-            networkVessel.SpawnWithOwnership(clientId, true);
+            networkVessel.SpawnWithOwnership(clientId, false);
             networkPlayer.NetVesselId.Value = networkVessel.NetworkObjectId;
             return networkVessel;
         }
diff --git a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
index 72ab8c1f3..326e04561 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
@@ -102,8 +102,10 @@ namespace CosmicShore.Gameplay
         /// <summary>
         /// Finds persistent AI players from previous scene transitions or creates
         /// new ones. Spawns vessels for all AI players and initializes pairs.
-        /// AI players are persistent (DestroyWithScene=false); only vessels are
-        /// scene-bound (DestroyWithScene=true).
+        /// AI players are persistent (DestroyWithScene=false). Vessels also use
+        /// DestroyWithScene=false to avoid "Invalid Destroy" race conditions on
+        /// non-host clients during scene transitions — they are explicitly
+        /// despawned by PreDespawnVessels() before scene loads.
         /// </summary>
         void EnsureAIPlayersAndVessels()
         {
@@ -300,7 +302,7 @@ namespace CosmicShore.Gameplay
 
             vesselNO = Instantiate(shipNetworkObject);
             GameObjectInjector.InjectRecursive(vesselNO.gameObject, _container);
-            vesselNO.Spawn(true);
+            vesselNO.Spawn(false);
             aiPlayer.NetVesselId.Value = vesselNO.NetworkObjectId;
             return true;
         }
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index 74db85da3..7abc2a30e 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -249,7 +249,7 @@ namespace CosmicShore.Gameplay
         {
             Debug.Log($"<color=#00FF00>[FLOW-4] [Player] PrepareForNewScene — OwnerClientId={OwnerClientId}, NetworkObjectId={NetworkObjectId}, IsOwner={IsOwner}</color>");
             // Clear stale references from previous scene.
-            // Vessels have destroyWithScene=true and are already destroyed.
+            // Vessels are explicitly despawned by PreDespawnVessels() before scene load.
             Vessel = null;
             IsActive = false;
             VesselNetId = 0;
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index 5c2fcfcc3..f269870fd 100644
--- a/Assets/_Scripts/System/SceneLoader.cs
+++ b/Assets/_Scripts/System/SceneLoader.cs
@@ -148,16 +148,20 @@ namespace CosmicShore.Core
             }
 
             // Fallback: scan SpawnManager for any VesselController not in gameData.Vessels
-            // (e.g. vessels added to scene but not yet registered via OnNetworkSpawn)
+            // (e.g. vessels added to scene but not yet registered via OnNetworkSpawn).
+            // Collect first, then despawn — calling Despawn() modifies SpawnedObjects.
             if (nm.SpawnManager != null)
             {
+                var strayVessels = new System.Collections.Generic.List<NetworkObject>();
                 foreach (var kvp in nm.SpawnManager.SpawnedObjects)
                 {
                     if (kvp.Value != null
-                        && kvp.Value.TryGetComponent<VesselController>(out var vc)
-                        && vc.IsSpawned)
-                        vc.NetworkObject.Despawn(true);
+                        && kvp.Value.TryGetComponent<VesselController>(out _)
+                        && kvp.Value.IsSpawned)
+                        strayVessels.Add(kvp.Value);
                 }
+                foreach (var no in strayVessels)
+                    no.Despawn(true);
             }
         }
 
```

</details>

### `c097364e7` — fix(multiplayer): trust Netcode scene management, remove manual vessel despawning

_Claude, 2026-03-04 03:58:52 +0000_

```text
Revert vessels to destroyWithScene=true and remove PreDespawnVessels().
Netcode's NetworkSceneManager handles destroyWithScene=true objects
automatically during server-initiated LoadScene() — manual despawning
fights the framework and can cause double-despawn race conditions.
```

```text
 .../_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs  |  2 +-
 .../Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs     |  2 +-
 Assets/_Scripts/Controller/Player/Player.cs                           |  2 +-
 Assets/_Scripts/System/SceneLoader.cs                                 | 46 ---------------------------------
 4 files changed, 3 insertions(+), 49 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
index 45b2e8c64..3a5210b08 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
@@ -314,7 +314,7 @@ namespace CosmicShore.Gameplay
 
             var networkVessel = Instantiate(shipNetworkObject);
             GameObjectInjector.InjectRecursive(networkVessel.gameObject, _container);
-            networkVessel.SpawnWithOwnership(clientId, false);
+            networkVessel.SpawnWithOwnership(clientId, true);
             networkPlayer.NetVesselId.Value = networkVessel.NetworkObjectId;
             return networkVessel;
         }
diff --git a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
index 326e04561..393943c99 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
@@ -302,7 +302,7 @@ namespace CosmicShore.Gameplay
 
             vesselNO = Instantiate(shipNetworkObject);
             GameObjectInjector.InjectRecursive(vesselNO.gameObject, _container);
-            vesselNO.Spawn(false);
+            vesselNO.Spawn(true);
             aiPlayer.NetVesselId.Value = vesselNO.NetworkObjectId;
             return true;
         }
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index 7abc2a30e..6b21f6d35 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -249,7 +249,7 @@ namespace CosmicShore.Gameplay
         {
             Debug.Log($"<color=#00FF00>[FLOW-4] [Player] PrepareForNewScene — OwnerClientId={OwnerClientId}, NetworkObjectId={NetworkObjectId}, IsOwner={IsOwner}</color>");
             // Clear stale references from previous scene.
-            // Vessels are explicitly despawned by PreDespawnVessels() before scene load.
+            // Vessels have destroyWithScene=true — Netcode handles their cleanup during scene transitions.
             Vessel = null;
             IsActive = false;
             VesselNetId = 0;
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index f269870fd..a4a9c5764 100644
--- a/Assets/_Scripts/System/SceneLoader.cs
+++ b/Assets/_Scripts/System/SceneLoader.cs
@@ -125,46 +125,6 @@ namespace CosmicShore.Core
             _sceneTransitionManager?.FadeFromBlack().Forget();
         }
 
-        /// <summary>
-        /// Server-only: despawns all tracked vessels before a network scene load.
-        /// Players stay persistent (DestroyWithScene=false) — only vessels are
-        /// cleaned up. This prevents "Invalid Destroy on non-host client" errors
-        /// that occur when client-side scene unload triggers Destroy() on vessels
-        /// before Netcode's despawn messages arrive.
-        /// Clients must never call Despawn/Destroy on spawned NetworkObjects they don't own.
-        /// </summary>
-        void PreDespawnVessels()
-        {
-            var nm = NetworkManager.Singleton;
-            if (nm == null || !nm.IsServer) return;
-
-            Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] PreDespawnVessels — Vessels.Count={gameData.Vessels.Count}</color>");
-
-            // Primary path: despawn all tracked vessels
-            for (int i = gameData.Vessels.Count - 1; i >= 0; i--)
-            {
-                if (gameData.Vessels[i] is VesselController vc && vc.IsSpawned)
-                    vc.NetworkObject.Despawn(true);
-            }
-
-            // Fallback: scan SpawnManager for any VesselController not in gameData.Vessels
-            // (e.g. vessels added to scene but not yet registered via OnNetworkSpawn).
-            // Collect first, then despawn — calling Despawn() modifies SpawnedObjects.
-            if (nm.SpawnManager != null)
-            {
-                var strayVessels = new System.Collections.Generic.List<NetworkObject>();
-                foreach (var kvp in nm.SpawnManager.SpawnedObjects)
-                {
-                    if (kvp.Value != null
-                        && kvp.Value.TryGetComponent<VesselController>(out _)
-                        && kvp.Value.IsSpawned)
-                        strayVessels.Add(kvp.Value);
-                }
-                foreach (var no in strayVessels)
-                    no.Despawn(true);
-            }
-        }
-
         /// <summary>
         /// Load the main menu scene.
         /// Called by SOAP EventListener (EventOnClickToMainMenuButton).
@@ -184,12 +144,6 @@ namespace CosmicShore.Core
             Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] LoadSceneAsync — sceneName={sceneName}, network={useNetworkSceneLoading}, waitBeforeLoading={waitBeforeLoading}s</color>");
             gameData.InvokeSceneTransition(false);
 
-            // Pre-despawn vessels to prevent "Invalid Destroy" errors on non-host
-            // clients. Players stay persistent — only vessels are cleaned up.
-            // The waitBeforeLoading delay gives clients time to process despawn messages.
-            if (useNetworkSceneLoading)
-                PreDespawnVessels();
-
             gameData.ResetRuntimeData();
             Debug.Log("<color=#FF8C00>[FLOW-3] [SceneLoader] ResetRuntimeData done. Waiting before load...</color>");
 
```

</details>

### `273135ff6` — fix(multiplayer): re-add server pre-despawn to eliminate Invalid Destroy warnings

_Claude, 2026-03-04 04:30:12 +0000_

```text
Netcode's NetworkObject.OnDestroy() logs "Invalid Destroy" on non-host
clients when Unity's scene unload destroys dynamically spawned vessels
while IsSpawned is still true. The check exempts scene objects but not
dynamically spawned objects with destroyWithScene=true.

PreDespawnVessels() explicitly despawns vessels on the server before
LoadScene(), so despawn messages arrive at clients before the scene
event. By the time OnDestroy fires, IsSpawned is already false.
```

```text
 Assets/_Scripts/Controller/Player/Player.cs |  3 ++-
 Assets/_Scripts/System/SceneLoader.cs       | 38 ++++++++++++++++++++++++++++++++++++++
 2 files changed, 40 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index 6b21f6d35..1af9ae59d 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -249,7 +249,8 @@ namespace CosmicShore.Gameplay
         {
             Debug.Log($"<color=#00FF00>[FLOW-4] [Player] PrepareForNewScene — OwnerClientId={OwnerClientId}, NetworkObjectId={NetworkObjectId}, IsOwner={IsOwner}</color>");
             // Clear stale references from previous scene.
-            // Vessels have destroyWithScene=true — Netcode handles their cleanup during scene transitions.
+            // Vessels are explicitly despawned by PreDespawnVessels() before scene load
+            // to avoid "Invalid Destroy" warnings on non-host clients.
             Vessel = null;
             IsActive = false;
             VesselNetId = 0;
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index a4a9c5764..d398a0d81 100644
--- a/Assets/_Scripts/System/SceneLoader.cs
+++ b/Assets/_Scripts/System/SceneLoader.cs
@@ -125,6 +125,41 @@ namespace CosmicShore.Core
             _sceneTransitionManager?.FadeFromBlack().Forget();
         }
 
+        /// <summary>
+        /// Server-only: explicitly despawns all vessels before a network scene load.
+        /// Netcode's destroyWithScene=true relies on Unity's scene unload to destroy objects,
+        /// but NetworkObject.OnDestroy() logs "Invalid Destroy" on non-host clients because
+        /// IsSpawned is still true when the scene unloads. Pre-despawning sends despawn messages
+        /// to clients before the scene event, so IsSpawned=false when OnDestroy fires.
+        /// </summary>
+        void PreDespawnVessels()
+        {
+            var nm = NetworkManager.Singleton;
+            if (nm == null || !nm.IsServer) return;
+
+            // Primary path: despawn all tracked vessels
+            for (int i = gameData.Vessels.Count - 1; i >= 0; i--)
+            {
+                if (gameData.Vessels[i] is VesselController vc && vc.IsSpawned)
+                    vc.NetworkObject.Despawn(true);
+            }
+
+            // Fallback: scan SpawnManager for any VesselController not in gameData.Vessels
+            if (nm.SpawnManager != null)
+            {
+                var strayVessels = new System.Collections.Generic.List<NetworkObject>();
+                foreach (var kvp in nm.SpawnManager.SpawnedObjects)
+                {
+                    if (kvp.Value != null
+                        && kvp.Value.TryGetComponent<VesselController>(out _)
+                        && kvp.Value.IsSpawned)
+                        strayVessels.Add(kvp.Value);
+                }
+                foreach (var no in strayVessels)
+                    no.Despawn(true);
+            }
+        }
+
         /// <summary>
         /// Load the main menu scene.
         /// Called by SOAP EventListener (EventOnClickToMainMenuButton).
@@ -144,6 +179,9 @@ namespace CosmicShore.Core
             Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] LoadSceneAsync — sceneName={sceneName}, network={useNetworkSceneLoading}, waitBeforeLoading={waitBeforeLoading}s</color>");
             gameData.InvokeSceneTransition(false);
 
+            if (useNetworkSceneLoading)
+                PreDespawnVessels();
+
             gameData.ResetRuntimeData();
             Debug.Log("<color=#FF8C00>[FLOW-3] [SceneLoader] ResetRuntimeData done. Waiting before load...</color>");
 
```

</details>

### `1fe3cb65e` — fix(multiplayer): clear stale vessel reference on client during scene transition

_Claude, 2026-03-04 04:52:58 +0000_

```text
Player.Vessel is typed as IVessel (interface), so C# null checks don't
use Unity's overloaded == operator for destroyed objects. After a scene
transition, the persistent Player still holds a stale reference to the
destroyed menu vessel. ProcessPendingPairs() checks player.Vessel != null
which evaluates true for the stale interface reference, skipping
InitializePair() — the new vessel is never initialized on the client.

Fix: call PrepareForNewScene() in ReRegisterPersistentPlayers() on the
client, matching what the server already does via
FindUnprocessedPlayerByOwnerClientId(). This sets Vessel = null before
pending pairs are processed.
```

```text
 Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs | 13 ++++++++-----
 1 file changed, 8 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs b/Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs
index d85b760ee..e00749ab9 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs
@@ -87,13 +87,16 @@ namespace CosmicShore.Gameplay
                     continue;
                 if (!player.IsSpawned) continue;
 
+                // Clear stale vessel reference from previous scene and update config.
+                // Mirrors what the server does via PrepareForNewScene() in
+                // ServerPlayerVesselInitializer.FindUnprocessedPlayerByOwnerClientId().
+                // Without this, player.Vessel holds a destroyed IVessel reference that
+                // passes C# null checks (IVessel is an interface, not UnityEngine.Object),
+                // causing ProcessPendingPairs() to skip InitializePair().
+                player.PrepareForNewScene();
+
                 if (!gameData.Players.Contains(player))
                     gameData.Players.Add(player);
-
-                // Owners update their vessel type to match the new game config
-                // (synced via SyncGameConfigToClients_ClientRpc before scene load).
-                if (player.IsOwner)
-                    player.NetDefaultVesselType.Value = gameData.selectedVesselClass.Value;
             }
         }
 
```

</details>

### `3dfb63fc8` — fix(vessel): guard VesselPrismController.CreateBlock against destroyed object

_Claude, 2026-03-04 05:28:05 +0000_

```text
SpawnLoopAsync runs between await points where the vessel can be
destroyed (PreDespawnVessels, vessel swap, scene unload). The CTS
cancellation in OnDisable/StopSpawn can't interrupt mid-execution
between the while check and the next await. Adding !this guard at
the top of CreateBlock prevents MissingReferenceException when
accessing transform.position on a destroyed component.
```

```text
 Assets/_Scripts/Controller/Vessel/VesselPrismController.cs | 2 ++
 1 file changed, 2 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/VesselPrismController.cs b/Assets/_Scripts/Controller/Vessel/VesselPrismController.cs
index 75e235a08..8a9a1fa7e 100644
--- a/Assets/_Scripts/Controller/Vessel/VesselPrismController.cs
+++ b/Assets/_Scripts/Controller/Vessel/VesselPrismController.cs
@@ -190,6 +190,8 @@ namespace CosmicShore.Gameplay
         /// <summary>Creates a block at offset using PrismFactory via event channel.</summary>
         void CreateBlock(float halfGap, Trail trail)
         {
+            if (!this) return;
+
             if (!_onPrismSpawnedEventChannel)
             {
                 CSDebug.LogError("[PrismSpawner] Prism spawn event channel is not assigned.");
```

</details>
