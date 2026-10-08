# Branch archive: `claude/investigate-spawning-logic-dMaCu`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-04 by Claude
- **Unmerged commits:** 3
- **Forked from:** `e43b80025` (2026-03-04, Merge pull request #342 from froglet-studio/claude/hex-race-unique-domains-7HB)
- **Tip:** `33e6688ab`
- **Files touched (1):**
  - `Assets/_Scripts/System/SceneLoader.cs`

### `8c7c64292` — fix(multiplayer): despawn vessels before network scene transition to prevent client errors

_Claude, 2026-03-04 00:09:55 +0000_

```text
When transitioning from Menu_Main to a game scene (e.g., HexRace) with a
party of 2+ players, the client's Player and Vessel NetworkObjects were
being destroyed by Unity's scene unload (PreDestroyRecursive) before Netcode
could properly handle them. This caused 5 client-side errors (2x Invalid
Destroy, 2x MissingReferenceException, 1x NetworkTransformMessage Invalid)
and 2 host-side relayed errors.

Root cause: the server relied on DestroyWithScene flags during
nm.SceneManager.LoadScene(), but client-side scene unload raced ahead of
Netcode's despawn message delivery.

Fix: explicitly despawn all tracked vessels on the server before calling
LoadScene(). The existing 500ms waitBeforeLoading delay gives clients time
to process despawn RPCs, so when the scene load event arrives, vessels are
already cleanly gone and Player objects migrate to DontDestroyOnLoad without
interference.
```

```text
 Assets/_Scripts/System/SceneLoader.cs | 30 ++++++++++++++++++++++++++++++
 1 file changed, 30 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index aee9696d9..3afaa0053 100644
--- a/Assets/_Scripts/System/SceneLoader.cs
+++ b/Assets/_Scripts/System/SceneLoader.cs
@@ -142,6 +142,15 @@ namespace CosmicShore.Core
         async UniTaskVoid LoadSceneAsync(string sceneName, bool useNetworkSceneLoading)
         {
             Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] LoadSceneAsync — sceneName={sceneName}, network={useNetworkSceneLoading}, waitBeforeLoading={waitBeforeLoading}s</color>");
+
+            // Despawn all vessels on the server BEFORE the scene transition.
+            // Without this, clients destroy them via Unity's scene unload (PreDestroyRecursive),
+            // causing "Invalid Destroy" Netcode errors — only the server should despawn
+            // spawned NetworkObjects. The waitBeforeLoading delay below gives clients time
+            // to process the despawn RPCs before the scene load event arrives.
+            if (useNetworkSceneLoading)
+                DespawnAllSpawnedVessels();
+
             gameData.InvokeSceneTransition(false);
             gameData.ResetRuntimeData();
             Debug.Log("<color=#FF8C00>[FLOW-3] [SceneLoader] ResetRuntimeData done. Waiting before load...</color>");
@@ -179,6 +188,27 @@ namespace CosmicShore.Core
             }
         }
 
+        /// <summary>
+        /// Server-side despawn of all tracked vessels before a network scene transition.
+        /// Sends despawn RPCs to clients so they cleanly remove vessel NetworkObjects
+        /// before Unity's scene unload destroys them.
+        /// </summary>
+        void DespawnAllSpawnedVessels()
+        {
+            if (!IsServer) return;
+
+            for (int i = gameData.Vessels.Count - 1; i >= 0; i--)
+            {
+                var vessel = gameData.Vessels[i];
+                if (vessel is VesselController vc && vc != null && vc.IsSpawned)
+                {
+                    Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] Despawning vessel: {vc.name} (NetworkObjectId={vc.NetworkObjectId})</color>");
+                    vc.NetworkObject.Despawn(true);
+                }
+            }
+            gameData.Vessels.Clear();
+        }
+
         void LoadNetworkSceneOnServer(string sceneName)
         {
             var nm = NetworkManager.Singleton;
```

</details>

### `48aee773e` — fix(multiplayer): eliminate remaining Invalid Destroy errors during scene transition

_Claude, 2026-03-04 00:41:46 +0000_

```text
Two changes to prevent the 2 remaining "Invalid Destroy" warnings
(Sender=1) during Menu_Main → HexRace party multiplayer transitions:

1. Change Despawn(true) to Despawn(false) for vessels:
   Despawn(true) schedules a deferred Destroy() which creates a race
   window — Unity's PreDestroyRecursive fires OnDestroy() before the
   deferred Destroy resolves, causing the client to send an "Invalid
   Destroy" notification to the server. Despawn(false) just marks
   IsSpawned=false; the scene unload destroys them cleanly.

2. Add MovePlayersToDontDestroyOnLoad ClientRpc:
   The client's Player (spawned in Menu_Main via connection approval)
   sits in the Menu_Main scene. Netcode's built-in scene migration
   races with Unity's PreDestroyRecursive. This ClientRpc explicitly
   moves Players to DontDestroyOnLoad on all clients before the scene
   transition, preventing the race.
```

```text
 Assets/_Scripts/System/SceneLoader.cs | 63 +++++++++++++++++++++++++++++++++++++++++++++++++++++++--------
 1 file changed, 55 insertions(+), 8 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index 3afaa0053..aa7fd4edc 100644
--- a/Assets/_Scripts/System/SceneLoader.cs
+++ b/Assets/_Scripts/System/SceneLoader.cs
@@ -143,13 +143,20 @@ namespace CosmicShore.Core
         {
             Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] LoadSceneAsync — sceneName={sceneName}, network={useNetworkSceneLoading}, waitBeforeLoading={waitBeforeLoading}s</color>");
 
-            // Despawn all vessels on the server BEFORE the scene transition.
-            // Without this, clients destroy them via Unity's scene unload (PreDestroyRecursive),
-            // causing "Invalid Destroy" Netcode errors — only the server should despawn
-            // spawned NetworkObjects. The waitBeforeLoading delay below gives clients time
-            // to process the despawn RPCs before the scene load event arrives.
+            // Prepare for network scene transition:
+            // 1. Move Player objects to DontDestroyOnLoad on all clients (ClientRpc)
+            //    and on the server. Prevents "Invalid Destroy" for Player objects
+            //    whose Netcode scene migration races with Unity's PreDestroyRecursive.
+            // 2. Despawn all vessels (without destroying) so clients mark them as
+            //    IsSpawned=false before the scene load event arrives.
+            // Messages are ordered (TCP/Relay), so the client processes these in order:
+            //    ClientRpc → despawn RPCs → [500ms later] scene load event.
             if (useNetworkSceneLoading)
+            {
+                MovePlayersToDontDestroyOnLoad_ClientRpc();
+                MovePlayersToDontDestroyOnLoad();
                 DespawnAllSpawnedVessels();
+            }
 
             gameData.InvokeSceneTransition(false);
             gameData.ResetRuntimeData();
@@ -190,8 +197,13 @@ namespace CosmicShore.Core
 
         /// <summary>
         /// Server-side despawn of all tracked vessels before a network scene transition.
-        /// Sends despawn RPCs to clients so they cleanly remove vessel NetworkObjects
-        /// before Unity's scene unload destroys them.
+        /// Uses Despawn(false) — despawn without destroying. The GameObjects remain alive
+        /// but are no longer spawned NetworkObjects (IsSpawned=false). Unity's scene unload
+        /// then destroys them cleanly without triggering "Invalid Destroy" notifications,
+        /// because NetworkObject.OnDestroy() sees IsSpawned=false.
+        /// Using Despawn(true) would schedule a deferred Destroy(), creating a window where
+        /// PreDestroyRecursive fires OnDestroy() before the deferred Destroy resolves,
+        /// causing the client to send "Invalid Destroy" to the server.
         /// </summary>
         void DespawnAllSpawnedVessels()
         {
@@ -203,7 +215,7 @@ namespace CosmicShore.Core
                 if (vessel is VesselController vc && vc != null && vc.IsSpawned)
                 {
                     Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] Despawning vessel: {vc.name} (NetworkObjectId={vc.NetworkObjectId})</color>");
-                    vc.NetworkObject.Despawn(true);
+                    vc.NetworkObject.Despawn(false);
                 }
             }
             gameData.Vessels.Clear();
@@ -281,6 +293,41 @@ namespace CosmicShore.Core
                 CameraManager.Instance.SnapPlayerCameraToTarget();
         }
 
+        /// <summary>
+        /// Moves all Player NetworkObjects to DontDestroyOnLoad on every client.
+        /// Prevents "Invalid Destroy" during scene transitions: the client's Player
+        /// (spawned in Menu_Main via connection approval) would otherwise be destroyed
+        /// by Unity's PreDestroyRecursive before Netcode's scene migration can move it.
+        /// The host's Player is typically already in DontDestroyOnLoad (from the
+        /// Auth→Menu_Main transition), so the scene.name check makes it a no-op.
+        /// </summary>
+        [ClientRpc]
+        void MovePlayersToDontDestroyOnLoad_ClientRpc()
+        {
+            MovePlayersToDontDestroyOnLoad();
+        }
+
+        /// <summary>
+        /// Moves all connected Player NetworkObjects to DontDestroyOnLoad.
+        /// Called on the server directly and on clients via ClientRpc.
+        /// </summary>
+        void MovePlayersToDontDestroyOnLoad()
+        {
+            var nm = NetworkManager.Singleton;
+            if (nm == null) return;
+
+            foreach (var kvp in nm.ConnectedClients)
+            {
+                var playerObj = kvp.Value.PlayerObject;
+                if (playerObj != null && playerObj.IsSpawned &&
+                    playerObj.gameObject.scene.name != "DontDestroyOnLoad")
+                {
+                    Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] Moving Player to DontDestroyOnLoad: {playerObj.name} (NetworkObjectId={playerObj.NetworkObjectId})</color>");
+                    DontDestroyOnLoad(playerObj.gameObject);
+                }
+            }
+        }
+
         /// <summary>
         /// Syncs the host's game configuration to all clients before a network scene load.
         /// Clients update their local GameDataSO so that Player.OnNetworkSpawn() reads the
```

</details>

### `33e6688ab` — fix(multiplayer): use LocalClient on clients for DontDestroyOnLoad + move vessels to DDOL

_Claude, 2026-03-04 01:13:06 +0000_

```text
The previous fix (48aee773) didn't work because NetworkManager.ConnectedClients
is a server-only API — on pure clients, it's empty. The ClientRpc that was
supposed to move Player objects to DontDestroyOnLoad did nothing on clients.

Changes:
- MovePlayersToDontDestroyOnLoad() now branches: server uses ConnectedClients,
  client uses nm.LocalClient?.PlayerObject
- New MoveVesselsToDontDestroyOnLoad() moves vessels to DontDestroyOnLoad on
  all peers via ClientRpc (belt-and-suspenders with server-side Despawn(false))
- Consolidated into single PrepareForSceneTransition_ClientRpc()
- New CleanUpStaleVesselsInDDOL() in OnSceneLoaded destroys despawned vessels
  that survived via DontDestroyOnLoad after the new scene loads
```

```text
 Assets/_Scripts/System/SceneLoader.cs | 92 +++++++++++++++++++++++++++++++++++++++++++++++++++--------------
 1 file changed, 73 insertions(+), 19 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index aa7fd4edc..16389ea23 100644
--- a/Assets/_Scripts/System/SceneLoader.cs
+++ b/Assets/_Scripts/System/SceneLoader.cs
@@ -59,6 +59,7 @@ namespace CosmicShore.Core
 
         void OnSceneLoaded(Scene scene, LoadSceneMode mode)
         {
+            CleanUpStaleVesselsInDDOL();
             if (gameData)
                 gameData.InvokeSceneTransition(true);
         }
@@ -144,17 +145,18 @@ namespace CosmicShore.Core
             Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] LoadSceneAsync — sceneName={sceneName}, network={useNetworkSceneLoading}, waitBeforeLoading={waitBeforeLoading}s</color>");
 
             // Prepare for network scene transition:
-            // 1. Move Player objects to DontDestroyOnLoad on all clients (ClientRpc)
-            //    and on the server. Prevents "Invalid Destroy" for Player objects
-            //    whose Netcode scene migration races with Unity's PreDestroyRecursive.
+            // 1. Tell all clients to move Players + Vessels to DontDestroyOnLoad.
+            //    This prevents "Invalid Destroy" regardless of despawn message timing.
+            //    ConnectedClients is server-only, so clients use LocalClient instead.
             // 2. Despawn all vessels (without destroying) so clients mark them as
             //    IsSpawned=false before the scene load event arrives.
             // Messages are ordered (TCP/Relay), so the client processes these in order:
             //    ClientRpc → despawn RPCs → [500ms later] scene load event.
             if (useNetworkSceneLoading)
             {
-                MovePlayersToDontDestroyOnLoad_ClientRpc();
+                PrepareForSceneTransition_ClientRpc();
                 MovePlayersToDontDestroyOnLoad();
+                MoveVesselsToDontDestroyOnLoad();
                 DespawnAllSpawnedVessels();
             }
 
@@ -294,36 +296,88 @@ namespace CosmicShore.Core
         }
 
         /// <summary>
-        /// Moves all Player NetworkObjects to DontDestroyOnLoad on every client.
-        /// Prevents "Invalid Destroy" during scene transitions: the client's Player
-        /// (spawned in Menu_Main via connection approval) would otherwise be destroyed
-        /// by Unity's PreDestroyRecursive before Netcode's scene migration can move it.
-        /// The host's Player is typically already in DontDestroyOnLoad (from the
-        /// Auth→Menu_Main transition), so the scene.name check makes it a no-op.
+        /// Prepares all clients for a network scene transition by moving Players
+        /// and Vessels to DontDestroyOnLoad. This prevents "Invalid Destroy" errors
+        /// caused by Unity's PreDestroyRecursive racing with Netcode's scene migration.
         /// </summary>
         [ClientRpc]
-        void MovePlayersToDontDestroyOnLoad_ClientRpc()
+        void PrepareForSceneTransition_ClientRpc()
         {
             MovePlayersToDontDestroyOnLoad();
+            MoveVesselsToDontDestroyOnLoad();
         }
 
         /// <summary>
-        /// Moves all connected Player NetworkObjects to DontDestroyOnLoad.
-        /// Called on the server directly and on clients via ClientRpc.
+        /// Moves Player NetworkObjects to DontDestroyOnLoad.
+        /// Server path: iterates ConnectedClients (populated only on server/host).
+        /// Client path: uses LocalClient.PlayerObject (ConnectedClients is empty on clients).
+        /// The host's Player is typically already in DontDestroyOnLoad (from the
+        /// Auth→Menu_Main transition), so the scene.name check makes it a no-op.
         /// </summary>
         void MovePlayersToDontDestroyOnLoad()
         {
             var nm = NetworkManager.Singleton;
             if (nm == null) return;
 
-            foreach (var kvp in nm.ConnectedClients)
+            if (nm.IsServer)
+            {
+                foreach (var kvp in nm.ConnectedClients)
+                {
+                    MoveNetworkObjectToDontDestroyOnLoad(kvp.Value.PlayerObject);
+                }
+            }
+            else
+            {
+                MoveNetworkObjectToDontDestroyOnLoad(nm.LocalClient?.PlayerObject);
+            }
+        }
+
+        /// <summary>
+        /// Moves all tracked Vessel GameObjects to DontDestroyOnLoad on this peer.
+        /// Belt-and-suspenders: even if the server's Despawn(false) message hasn't
+        /// arrived yet, the vessel survives scene unload without triggering "Invalid Destroy".
+        /// Stale vessels in DontDestroyOnLoad are cleaned up by CleanUpStaleVesselsInDDOL()
+        /// after the new scene loads.
+        /// </summary>
+        void MoveVesselsToDontDestroyOnLoad()
+        {
+            if (gameData == null) return;
+
+            foreach (var vessel in gameData.Vessels)
+            {
+                if (vessel is VesselController vc && vc != null &&
+                    vc.gameObject.scene.name != "DontDestroyOnLoad")
+                {
+                    Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] Moving vessel to DontDestroyOnLoad: {vc.name} (NetworkObjectId={vc.NetworkObjectId})</color>");
+                    DontDestroyOnLoad(vc.gameObject);
+                }
+            }
+        }
+
+        static void MoveNetworkObjectToDontDestroyOnLoad(NetworkObject netObj)
+        {
+            if (netObj != null && netObj.IsSpawned &&
+                netObj.gameObject.scene.name != "DontDestroyOnLoad")
+            {
+                Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] Moving to DontDestroyOnLoad: {netObj.name} (NetworkObjectId={netObj.NetworkObjectId})</color>");
+                DontDestroyOnLoad(netObj.gameObject);
+            }
+        }
+
+        /// <summary>
+        /// Destroys stale vessel GameObjects that survived a scene transition
+        /// by being moved to DontDestroyOnLoad but are no longer spawned NetworkObjects.
+        /// Called from OnSceneLoaded after the new scene finishes loading.
+        /// </summary>
+        void CleanUpStaleVesselsInDDOL()
+        {
+            var staleVessels = FindObjectsByType<VesselController>(FindObjectsSortMode.None);
+            foreach (var vc in staleVessels)
             {
-                var playerObj = kvp.Value.PlayerObject;
-                if (playerObj != null && playerObj.IsSpawned &&
-                    playerObj.gameObject.scene.name != "DontDestroyOnLoad")
+                if (vc != null && !vc.IsSpawned && vc.gameObject.scene.name == "DontDestroyOnLoad")
                 {
-                    Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] Moving Player to DontDestroyOnLoad: {playerObj.name} (NetworkObjectId={playerObj.NetworkObjectId})</color>");
-                    DontDestroyOnLoad(playerObj.gameObject);
+                    Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] Destroying stale vessel in DDOL: {vc.name}</color>");
+                    Destroy(vc.gameObject);
                 }
             }
         }
```

</details>
