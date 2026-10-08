# Branch archive: `claude/fix-hex-race-spawning-wPmI9`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-03-04 by Claude
- **Unmerged commits:** 6
- **Forked from:** `e43b80025` (2026-03-04, Merge pull request #342 from froglet-studio/claude/hex-race-unique-domains-7HB)
- **Tip:** `7fb03e954`
- **Files touched (6):**
  - `Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs`
  - `Assets/_Scripts/Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs`
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs`
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs`
  - `Assets/_Scripts/Controller/Player/Player.cs`
  - `Assets/_Scripts/System/SceneLoader.cs`

### `10063dd26` — refactor(multiplayer): replace SOAP event-driven spawning with OnClientConnectedCallback

_Claude, 2026-03-04 01:59:58 +0000_

```text
Adopt the Dev branch's simpler spawning approach while keeping the
current branch's infrastructure (Reflex DI, GameObjectInjector, SOAP
for cross-system communication, PrepareForNewScene for persistent
players, shutdownNetworkOnDespawn toggle, vessel swap flow).

Key changes:
- ServerPlayerVesselInitializer: subscribe to OnClientConnectedCallback
  instead of OnPlayerNetworkSpawnedUlong SOAP event. Use
  SpawnManager.GetPlayerNetworkObject(clientId) for direct player
  lookup after delay. Keep ProcessPreExistingPlayers for persistent
  Players surviving scene transitions.
- ServerPlayerVesselInitializerWithAI: override OnClientConnected to
  spawn AIs first (host only), wait for replication, then delegate to
  base for human vessel spawning.
- ClientPlayerVesselInitializer: replace SOAP event-driven pending pair
  queue with UniTask.WaitUntil polling for NetworkObject replication.
  Add DelayInvokeClientReady matching Dev branch pattern.
- Player: remove deferred SOAP spawn event machinery (_spawnEventRaised,
  TryRaiseDeferredSpawnEvent, IsSpawnReady, OnNetDefaultVesselTypeChanged).
  OnNetworkSpawn no longer raises OnPlayerNetworkSpawnedUlong.
- MenuServerPlayerVesselInitializer: docstring update only.
```

```text
 .../_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs  | 142 ++++++++++-------------
 .../Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs       |   4 +-
 .../_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs  | 192 ++++++++++----------------------
 .../Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs     |  83 ++++++--------
 Assets/_Scripts/Controller/Player/Player.cs                           |  80 +------------
 5 files changed, 156 insertions(+), 345 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 875 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs b/Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs
index d85b760ee..f010076ef 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs
@@ -1,8 +1,9 @@
 using System;
-using System.Collections.Generic;
+using System.Threading;
 using CosmicShore.Data;
 using CosmicShore.Gameplay;
 using CosmicShore.Utility;
+using Cysharp.Threading.Tasks;
 using Reflex.Attributes;
 using Unity.Netcode;
 using UnityEngine;
@@ -20,9 +21,8 @@ namespace CosmicShore.Gameplay
     ///   InitializeNewPlayerAndVessel_ClientRpc   → existing client initializes one new pair
     ///   ReplaceVesselForPlayer_ClientRpc         → swap: re-initialize with a new vessel
     ///
-    /// When an RPC arrives but objects haven't replicated yet, pairs are queued.
-    /// OnPlayerNetworkSpawnedUlong + OnVesselNetworkSpawned SOAP events trigger
-    /// re-processing of the queue — zero WaitUntil polling.
+    /// When an RPC arrives but objects haven't replicated yet, pairs are resolved
+    /// via UniTask.WaitUntil polling until the NetworkObjects appear in gameData.
     /// </summary>
     public class ClientPlayerVesselInitializer : NetworkBehaviour
     {
@@ -30,10 +30,6 @@ namespace CosmicShore.Gameplay
 
         [Inject] protected GameDataSO gameData;
 
-        readonly List<(ulong playerNetId, ulong vesselNetId)> _pendingPairs = new();
-        readonly List<(ulong playerNetId, ulong vesselNetId)> _pendingSwaps = new();
-        bool _signalClientReadyWhenDone;
-
         public override void OnNetworkSpawn()
         {
             base.OnNetworkSpawn();
@@ -43,25 +39,7 @@ namespace CosmicShore.Gameplay
 
             // Re-register persistent Players that survived the Netcode scene load
             // but were cleared from gameData.Players by ResetRuntimeData().
-            // Their OnNetworkSpawn() won't re-fire, so we manually re-add them
-            // so ProcessPendingPairs() can resolve (playerNetId, vesselNetId) pairs.
             ReRegisterPersistentPlayers();
-
-            // Subscribe to SOAP events so we can process pending pairs
-            // when objects replicate (event-driven, no polling)
-            gameData.OnPlayerNetworkSpawnedUlong.OnRaised += OnPlayerNetworkSpawnedForPending;
-            gameData.OnVesselNetworkSpawned.OnRaised += ProcessPendingPairs;
-            gameData.OnVesselNetworkSpawned.OnRaised += ProcessPendingSwaps;
-        }
-
-        public override void OnNetworkDespawn()
-        {
-            gameData.OnPlayerNetworkSpawnedUlong.OnRaised -= OnPlayerNetworkSpawnedForPending;
-            gameData.OnVesselNetworkSpawned.OnRaised -= ProcessPendingPairs;
-            gameData.OnVesselNetworkSpawned.OnRaised -= ProcessPendingSwaps;
-            _pendingPairs.Clear();
-            _pendingSwaps.Clear();
-            base.OnNetworkDespawn();
         }
 
         // ---------------------------------------------------------
@@ -91,7 +69,6 @@ namespace CosmicShore.Gameplay
                     gameData.Players.Add(player);
 
                 // Owners update their vessel type to match the new game config
-                // (synced via SyncGameConfigToClients_ClientRpc before scene load).
                 if (player.IsOwner)
                     player.NetDefaultVesselType.Value = gameData.selectedVesselClass.Value;
             }
@@ -111,19 +88,19 @@ namespace CosmicShore.Gameplay
 
         /// <summary>
         /// RPC sent to NEW client: initialize ALL existing player-vessel pairs.
-        /// Fires ClientReady when all pairs are initialized.
+        /// Fires ClientReady after a delay when all pairs should be initialized.
         /// </summary>
         [ClientRpc]
         internal void InitializeAllPlayersAndVessels_ClientRpc(
             ulong[] playerNetIds, ulong[] vesselNetIds,
             ClientRpcParams rpcParams = default)
         {
-            _signalClientReadyWhenDone = true;
+            var ct = this.GetCancellationTokenOnDestroy();
 
             for (int i = 0; i < playerNetIds.Length; i++)
-                _pendingPairs.Add((playerNetIds[i], vesselNetIds[i]));
+                InitializePlayerAndVesselByNetIds(playerNetIds[i], vesselNetIds[i], ct).Forget();
 
-            ProcessPendingPairs();
+            DelayInvokeClientReady(ct).Forget();
         }
 
         /// <summary>
@@ -135,8 +112,8 @@ namespace CosmicShore.Gameplay
             ulong playerNetId, ulong vesselNetId,
             ClientRpcParams rpcParams = default)
         {
-            _pendingPairs.Add((playerNetId, vesselNetId));
-            ProcessPendingPairs();
+            InitializePlayerAndVesselByNetIds(playerNetId, vesselNetId,
+                this.GetCancellationTokenOnDestroy()).Forget();
         }
 
         // ---------------------------------------------------------
@@ -181,74 +158,78 @@ namespace CosmicShore.Gameplay
         /// <summary>
         /// RPC sent to ALL non-host clients when a player swaps their vessel.
         /// The old vessel was already despawned by the server; the new one is replicating.
-        /// Queued until the new vessel's NetworkObject appears.
+        /// Polls until the new vessel appears, then re-initializes.
         /// </summary>
         [ClientRpc]
         internal void ReplaceVesselForPlayer_ClientRpc(
             ulong playerNetId, ulong newVesselNetId,
             ClientRpcParams rpcParams = default)
         {
-            _pendingSwaps.Add((playerNetId, newVesselNetId));
-            ProcessPendingSwaps();
+            WaitAndReplaceVessel(playerNetId, newVesselNetId,
+                this.GetCancellationTokenOnDestroy()).Forget();
         }
 
         // ---------------------------------------------------------
-        // PENDING PAIR RESOLUTION
+        // POLLING-BASED PAIR RESOLUTION
         // ---------------------------------------------------------
 
-        void OnPlayerNetworkSpawnedForPending(ulong _) => ProcessPendingPairs();
-
         /// <summary>
-        /// Tries to resolve pending (playerNetId, vesselNetId) pairs.
-        /// Called when RPCs arrive AND when SOAP events fire (objects replicate).
+        /// Polls until both player and vessel NetworkObjects are available in gameData,
+        /// then initializes the pair. Used by client RPCs when objects haven't replicated yet.
         /// </summary>
-        void ProcessPendingPairs()
+        async UniTaskVoid InitializePlayerAndVesselByNetIds(
+            ulong playerId, ulong vesselId, CancellationToken token)
         {
-            for (int i = _pendingPairs.Count - 1; i >= 0; i--)
-            {
-                var (pId, vId) = _pendingPairs[i];
+            await UniTask.WaitUntil(() =>
+                    gameData.TryGetPlayerByNetworkObjectId(playerId, out _) &&
+                    gameData.TryGetVesselByNetworkObjectId(vesselId, out _),
+                cancellationToken: token);
 
```

</details>

### `cc49044fe` — fix(multiplayer): prevent Invalid Destroy errors on scene transition

_Claude, 2026-03-04 02:30:46 +0000_

```text
Despawn all tracked vessels on the server before Netcode scene load
so clients receive proper despawn messages instead of hitting
"Invalid Destroy" when Unity's scene unload destroys the GameObjects.

Also reinforce DestroyWithScene=false on Player.OnNetworkSpawn() so
Player objects consistently persist across scene transitions on all
clients.
```

```text
 Assets/_Scripts/Controller/Player/Player.cs |  5 +++++
 Assets/_Scripts/System/SceneLoader.cs       | 27 +++++++++++++++++++++++++++
 2 files changed, 32 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index f121fe35e..d7e35670c 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -120,6 +120,11 @@ namespace CosmicShore.Gameplay
         {
             base.OnNetworkSpawn();
 
+            // Players persist across Netcode scene transitions.
+            // Connection approval spawns with destroyWithScene=false internally,
+            // but we reinforce it so all clients consistently preserve Player objects.
+            NetworkObject.DestroyWithScene = false;
+
             // Add to game data early so ServerPlayerVesselInitializer can find us.
             gameData.Players.Add(this);
 
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index aee9696d9..7eaa22293 100644
--- a/Assets/_Scripts/System/SceneLoader.cs
+++ b/Assets/_Scripts/System/SceneLoader.cs
@@ -1,4 +1,5 @@
 using System;
+using System.Collections.Generic;
 using CosmicShore.Data;
 using CosmicShore.Gameplay;
 using CosmicShore.Utility;
@@ -143,6 +144,13 @@ namespace CosmicShore.Core
         {
             Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] LoadSceneAsync — sceneName={sceneName}, network={useNetworkSceneLoading}, waitBeforeLoading={waitBeforeLoading}s</color>");
             gameData.InvokeSceneTransition(false);
+
+            // Server: explicitly despawn and destroy all vessels before the scene transition.
+            // This sends despawn messages to clients so they cleanly remove the objects
+            // before the old scene unloads, preventing "Invalid Destroy" errors on non-host clients.
+            if (useNetworkSceneLoading)
+                DespawnTrackedVesselsOnServer();
+
             gameData.ResetRuntimeData();
             Debug.Log("<color=#FF8C00>[FLOW-3] [SceneLoader] ResetRuntimeData done. Waiting before load...</color>");
 
@@ -193,6 +201,25 @@ namespace CosmicShore.Core
             nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
         }
 
+        /// <summary>
+        /// Despawns all tracked vessels on the server before a scene transition.
+        /// Vessels are spawned with <c>destroyWithScene=true</c> and would otherwise be
+        /// destroyed by Unity's scene unload on non-host clients, triggering
+        /// "Invalid Destroy" errors. Explicit server despawn sends proper despawn
+        /// messages so clients remove the objects cleanly.
+        /// </summary>
+        void DespawnTrackedVesselsOnServer()
+        {
+            if (!IsServer) return;
+
+            var vessels = new List<IVessel>(gameData.Vessels);
+            foreach (var vessel in vessels)
+            {
+                if (vessel is VesselController vc && vc.IsSpawned)
+                    vc.NetworkObject.Despawn(true);
+            }
+        }
+
         #endregion
 
         #region Restart / Replay
```

</details>

### `5fc2954f2` — fix(multiplayer): fix vessel despawn and stale HUD references on scene transition

_Claude, 2026-03-04 02:55:03 +0000_

```text
Two fixes in SceneLoader.LoadSceneAsync():

1. DespawnTrackedVesselsOnServer() now uses NetworkManager.Singleton.IsServer
   instead of the inherited IsServer property. SceneLoader is never spawned
   as a NetworkObject (it's a DI singleton on DontDestroyOnLoad), so the
   inherited property always returned false, silently skipping all despawns.

2. Call PrepareForNewScene() on all persistent Players before despawning
   vessels. This nulls Player.Vessel so MiniGameHUD.HideLocalVesselHUD()
   can't traverse into destroyed VesselHUDView components via the ?.
   chain (which doesn't catch destroyed Unity objects).
```

```text
 Assets/_Scripts/System/SceneLoader.cs | 19 +++++++++++++++----
 1 file changed, 15 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index 7eaa22293..ff27f797a 100644
--- a/Assets/_Scripts/System/SceneLoader.cs
+++ b/Assets/_Scripts/System/SceneLoader.cs
@@ -145,11 +145,21 @@ namespace CosmicShore.Core
             Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] LoadSceneAsync — sceneName={sceneName}, network={useNetworkSceneLoading}, waitBeforeLoading={waitBeforeLoading}s</color>");
             gameData.InvokeSceneTransition(false);
 
-            // Server: explicitly despawn and destroy all vessels before the scene transition.
-            // This sends despawn messages to clients so they cleanly remove the objects
-            // before the old scene unloads, preventing "Invalid Destroy" errors on non-host clients.
+            // Server: clear stale vessel references on persistent Players, then despawn
+            // all vessels before the scene transition. PrepareForNewScene() nulls Player.Vessel
+            // so no code path can traverse into destroyed HUD components (prevents
+            // MissingReferenceException). Despawn sends messages to clients so they cleanly
+            // remove objects before the old scene unloads (prevents "Invalid Destroy" errors).
             if (useNetworkSceneLoading)
+            {
+                foreach (var player in gameData.Players)
+                {
+                    if (player is Player p)
+                        p.PrepareForNewScene();
+                }
+
                 DespawnTrackedVesselsOnServer();
+            }
 
             gameData.ResetRuntimeData();
             Debug.Log("<color=#FF8C00>[FLOW-3] [SceneLoader] ResetRuntimeData done. Waiting before load...</color>");
@@ -210,7 +220,8 @@ namespace CosmicShore.Core
         /// </summary>
         void DespawnTrackedVesselsOnServer()
         {
-            if (!IsServer) return;
+            var nm = NetworkManager.Singleton;
+            if (nm == null || !nm.IsServer) return;
 
             var vessels = new List<IVessel>(gameData.Vessels);
             foreach (var vessel in vessels)
```

</details>

### `8a6aad737` — fix(multiplayer): prepare clients before scene transition to prevent Invalid Destroy

_Claude, 2026-03-04 03:23:40 +0000_

```text
Send PrepareClientsForSceneTransition_ClientRpc before server-side vessel
despawn. On non-host clients, this marks all vessels DestroyWithScene=false
so they survive the scene unload even if despawn messages arrive late.
Also clears stale Player.Vessel references to prevent MissingReferenceException.

The existing 0.5s waitBeforeLoading delay gives clients time to process the
RPC before the server calls nm.SceneManager.LoadScene().
```

```text
 Assets/_Scripts/System/SceneLoader.cs | 36 +++++++++++++++++++++++++++++++-----
 1 file changed, 31 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index ff27f797a..941f68214 100644
--- a/Assets/_Scripts/System/SceneLoader.cs
+++ b/Assets/_Scripts/System/SceneLoader.cs
@@ -145,13 +145,15 @@ namespace CosmicShore.Core
             Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] LoadSceneAsync — sceneName={sceneName}, network={useNetworkSceneLoading}, waitBeforeLoading={waitBeforeLoading}s</color>");
             gameData.InvokeSceneTransition(false);
 
-            // Server: clear stale vessel references on persistent Players, then despawn
-            // all vessels before the scene transition. PrepareForNewScene() nulls Player.Vessel
-            // so no code path can traverse into destroyed HUD components (prevents
-            // MissingReferenceException). Despawn sends messages to clients so they cleanly
-            // remove objects before the old scene unloads (prevents "Invalid Destroy" errors).
+            // Prepare clients BEFORE the server despawns vessels.
+            // The ClientRpc marks vessels DestroyWithScene=false on non-host clients so they
+            // survive the scene unload even if the despawn messages haven't arrived yet.
+            // The 0.5s waitBeforeLoading delay gives clients time to process this RPC.
+            // Server-side: clear stale vessel references, then despawn all vessels.
             if (useNetworkSceneLoading)
             {
+                PrepareClientsForSceneTransition_ClientRpc();
+
                 foreach (var player in gameData.Players)
                 {
                     if (player is Player p)
@@ -289,6 +291,30 @@ namespace CosmicShore.Core
                 CameraManager.Instance.SnapPlayerCameraToTarget();
         }
 
+        /// <summary>
+        /// Prepares non-host clients for a scene transition.
+        /// Marks vessels DestroyWithScene=false so they survive the scene unload
+        /// and get properly cleaned up by the server's despawn messages.
+        /// Clears stale Player.Vessel references to prevent MissingReferenceException.
+        /// </summary>
+        [ClientRpc]
+        void PrepareClientsForSceneTransition_ClientRpc()
+        {
+            if (IsServer) return; // Host handles cleanup directly in LoadSceneAsync
+
+            foreach (var player in gameData.Players)
+            {
+                if (player is Player p)
+                    p.PrepareForNewScene();
+            }
+
+            foreach (var vessel in gameData.Vessels)
+            {
+                if (vessel is VesselController vc && vc.IsSpawned)
+                    vc.NetworkObject.DestroyWithScene = false;
+            }
+        }
+
         /// <summary>
         /// Syncs the host's game configuration to all clients before a network scene load.
         /// Clients update their local GameDataSO so that Player.OnNetworkSpawn() reads the
```

</details>

### `4714ecd5e` — fix(multiplayer): let Netcode handle vessel lifecycle on scene transitions

_Claude, 2026-03-04 03:55:00 +0000_

```text
Stop manually despawning vessels and preparing clients before scene
transitions. Netcode's NetworkSceneManager already handles
destroyWithScene=true objects automatically. Manual despawn created race
conditions where despawn messages arrived on clients after the scene load
message, causing "Invalid Destroy" errors.

Removed:
- DespawnTrackedVesselsOnServer() — redundant, Netcode does this
- PrepareClientsForSceneTransition_ClientRpc() — no longer needed
- Server-side PrepareForNewScene() loop in LoadSceneAsync — handled by
  ServerPlayerVesselInitializer.ProcessPreExistingPlayers() in new scene

Players persist (DestroyWithScene=false) and get re-initialized by the
new scene's initializers. Vessels get destroyed automatically by Netcode.
```

```text
 Assets/_Scripts/System/SceneLoader.cs | 68 ++++-------------------------------------------------------------
 1 file changed, 4 insertions(+), 64 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index 941f68214..c5e4cc35c 100644
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
@@ -145,24 +143,10 @@ namespace CosmicShore.Core
             Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] LoadSceneAsync — sceneName={sceneName}, network={useNetworkSceneLoading}, waitBeforeLoading={waitBeforeLoading}s</color>");
             gameData.InvokeSceneTransition(false);
 
-            // Prepare clients BEFORE the server despawns vessels.
-            // The ClientRpc marks vessels DestroyWithScene=false on non-host clients so they
-            // survive the scene unload even if the despawn messages haven't arrived yet.
-            // The 0.5s waitBeforeLoading delay gives clients time to process this RPC.
-            // Server-side: clear stale vessel references, then despawn all vessels.
-            if (useNetworkSceneLoading)
-            {
-                PrepareClientsForSceneTransition_ClientRpc();
-
-                foreach (var player in gameData.Players)
-                {
-                    if (player is Player p)
-                        p.PrepareForNewScene();
-                }
-
-                DespawnTrackedVesselsOnServer();
-            }
-
+            // Do NOT manually despawn vessels here — Netcode's NetworkSceneManager
+            // handles destroyWithScene=true objects automatically during scene transitions.
+            // Players (DestroyWithScene=false) persist and get re-initialized by
+            // ServerPlayerVesselInitializer.ProcessPreExistingPlayers() in the new scene.
             gameData.ResetRuntimeData();
             Debug.Log("<color=#FF8C00>[FLOW-3] [SceneLoader] ResetRuntimeData done. Waiting before load...</color>");
 
@@ -213,26 +197,6 @@ namespace CosmicShore.Core
             nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
         }
 
-        /// <summary>
-        /// Despawns all tracked vessels on the server before a scene transition.
-        /// Vessels are spawned with <c>destroyWithScene=true</c> and would otherwise be
-        /// destroyed by Unity's scene unload on non-host clients, triggering
-        /// "Invalid Destroy" errors. Explicit server despawn sends proper despawn
-        /// messages so clients remove the objects cleanly.
-        /// </summary>
-        void DespawnTrackedVesselsOnServer()
-        {
-            var nm = NetworkManager.Singleton;
-            if (nm == null || !nm.IsServer) return;
-
-            var vessels = new List<IVessel>(gameData.Vessels);
-            foreach (var vessel in vessels)
-            {
-                if (vessel is VesselController vc && vc.IsSpawned)
-                    vc.NetworkObject.Despawn(true);
-            }
-        }
-
         #endregion
 
         #region Restart / Replay
@@ -291,30 +255,6 @@ namespace CosmicShore.Core
                 CameraManager.Instance.SnapPlayerCameraToTarget();
         }
 
-        /// <summary>
-        /// Prepares non-host clients for a scene transition.
-        /// Marks vessels DestroyWithScene=false so they survive the scene unload
-        /// and get properly cleaned up by the server's despawn messages.
-        /// Clears stale Player.Vessel references to prevent MissingReferenceException.
-        /// </summary>
-        [ClientRpc]
-        void PrepareClientsForSceneTransition_ClientRpc()
-        {
-            if (IsServer) return; // Host handles cleanup directly in LoadSceneAsync
-
-            foreach (var player in gameData.Players)
-            {
-                if (player is Player p)
-                    p.PrepareForNewScene();
-            }
-
-            foreach (var vessel in gameData.Vessels)
-            {
-                if (vessel is VesselController vc && vc.IsSpawned)
-                    vc.NetworkObject.DestroyWithScene = false;
-            }
-        }
-
         /// <summary>
         /// Syncs the host's game configuration to all clients before a network scene load.
         /// Clients update their local GameDataSO so that Player.OnNetworkSpawn() reads the
```

</details>

### `7fb03e954` — fix(multiplayer): pre-despawn vessels on server before scene transition

_Claude, 2026-03-04 04:22:09 +0000_

```text
Server now explicitly despawns all vessel NetworkObjects BEFORE the
waitBeforeLoading delay and nm.SceneManager.LoadScene(). This gives
clients 0.5s to process despawn messages, so no spawned vessels remain
when the scene unloads.

Without this, NGO's NetworkSceneManager despawns vessels and sends the
scene event in the same network tick. On non-host clients, the scene
event triggers Unity's synchronous scene unload before despawn messages
are processed, causing "Invalid Destroy" errors on still-spawned
NetworkObjects. This is a known NGO timing limitation (issues #3144,
#2706).

Uses SpawnManager.SpawnedObjectsList instead of gameData.Vessels to
catch all vessels regardless of tracking state. No ClientRpc needed —
clients process server despawns automatically via NGO's message queue.
```

```text
 Assets/_Scripts/System/SceneLoader.cs | 33 +++++++++++++++++++++++++++++++--
 1 file changed, 31 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index c5e4cc35c..1b80d0142 100644
--- a/Assets/_Scripts/System/SceneLoader.cs
+++ b/Assets/_Scripts/System/SceneLoader.cs
@@ -1,5 +1,7 @@
 using System;
+using System.Collections.Generic;
 using CosmicShore.Data;
+using CosmicShore.Gameplay;
 using CosmicShore.Utility;
 using Cysharp.Threading.Tasks;
 using Reflex.Attributes;
@@ -143,10 +145,16 @@ namespace CosmicShore.Core
             Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] LoadSceneAsync — sceneName={sceneName}, network={useNetworkSceneLoading}, waitBeforeLoading={waitBeforeLoading}s</color>");
             gameData.InvokeSceneTransition(false);
 
-            // Do NOT manually despawn vessels here — Netcode's NetworkSceneManager
-            // handles destroyWithScene=true objects automatically during scene transitions.
+            // Despawn vessels on the server BEFORE the delay so clients receive and process
+            // the despawn messages during the waitBeforeLoading window. Without this, NGO's
+            // NetworkSceneManager despawns vessels and sends the scene event in the same
+            // network tick — clients may process the scene event first, causing Unity's
+            // synchronous scene unload to destroy still-spawned NetworkObjects ("Invalid Destroy").
             // Players (DestroyWithScene=false) persist and get re-initialized by
             // ServerPlayerVesselInitializer.ProcessPreExistingPlayers() in the new scene.
+            if (useNetworkSceneLoading)
+                DespawnAllVesselsOnServer();
+
             gameData.ResetRuntimeData();
             Debug.Log("<color=#FF8C00>[FLOW-3] [SceneLoader] ResetRuntimeData done. Waiting before load...</color>");
 
@@ -197,6 +205,27 @@ namespace CosmicShore.Core
             nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
         }
 
+        /// <summary>
+        /// Despawns all vessel NetworkObjects on the server before a scene transition.
+        /// Iterates <see cref="NetworkSpawnManager.SpawnedObjectsList"/> directly (not
+        /// <c>gameData.Vessels</c>) to catch every spawned vessel regardless of tracking state.
+        /// Despawn messages are sent to clients immediately; the <see cref="waitBeforeLoading"/>
+        /// delay gives clients time to process them before <c>nm.SceneManager.LoadScene()</c>
+        /// triggers the scene unload.
+        /// </summary>
+        void DespawnAllVesselsOnServer()
+        {
+            var nm = NetworkManager.Singleton;
+            if (nm == null || !nm.IsServer) return;
+
+            var spawnedObjects = new List<NetworkObject>(nm.SpawnManager.SpawnedObjectsList);
+            foreach (var netObj in spawnedObjects)
+            {
+                if (netObj != null && netObj.TryGetComponent<VesselController>(out _) && netObj.IsSpawned)
+                    netObj.Despawn(true);
+            }
+        }
+
         #endregion
 
         #region Restart / Replay
```

</details>
