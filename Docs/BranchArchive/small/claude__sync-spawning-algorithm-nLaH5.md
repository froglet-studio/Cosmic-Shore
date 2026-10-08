# Branch archive: `claude/sync-spawning-algorithm-nLaH5`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-03 by Claude
- **Unmerged commits:** 1
- **Forked from:** `e43b80025` (2026-03-04, Merge pull request #342 from froglet-studio/claude/hex-race-unique-domains-7HB)
- **Tip:** `6da9eb23f`
- **Files touched (4):**
  - `Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs`
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs`
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs`
  - `Assets/_Scripts/Controller/Player/Player.cs`

### `6da9eb23f` — refactor(multiplayer): unify AI and human player spawning through same pipeline

_Claude, 2026-03-03 23:50:14 +0000_

```text
ServerPlayerVesselInitializerWithAI now uses the same spawning algorithm as
Menu_Main's MenuServerPlayerVesselInitializer. Instead of AI players having
a separate code path (SpawnAIs → TrySpawnVesselForAI → manual init), they
now go through the base class's unified pipeline:

- SpawnAIPlayerObjects() only creates AI Player NetworkObjects and sets
  their NetworkVariables (name, vessel type, domain, IsAI) — no vessels
- base.OnNetworkSpawn() → ProcessPreExistingPlayers() catches AI players
  and routes them through HandlePlayerNetworkSpawnedAsync like humans
- Vessels spawn via SpawnVesselForPlayer (same as human players)
- Client notification via NotifyClients RPCs (previously skipped for AI)
- OnPlayerReadyToSpawnAsync override configures AIPilot after vessel spawn
  (same pattern as MenuServerPlayerVesselInitializer's ActivateAutopilot)

Removed TrySpawnVesselForAI and the "mark AI as processed" bypass. AI and
human players now share identical spawn timing (pre/post delays) and
client replication paths.
```

```text
 Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs            |   4 +-
 .../_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs  |   2 +-
 .../Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs     | 133 +++++++++++++++-----------------
 Assets/_Scripts/Controller/Player/Player.cs                           |   6 +-
 4 files changed, 68 insertions(+), 77 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 250 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs b/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
index 1e421007b..624687baf 100644
--- a/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
@@ -86,8 +86,8 @@ namespace CosmicShore.Gameplay
             if (gameData.IsMultiplayerMode)
             {
                 // DestroyPlayerAndVessel() was removed here because it races with
-                // ServerPlayerVesselInitializerWithAI.SpawnAIs(). Both run during
-                // scene Start(): SpawnAIs() adds AI to gameData.Players, then this
+                // ServerPlayerVesselInitializerWithAI.SpawnAIPlayerObjects(). Both run during
+                // scene Start(): SpawnAIPlayerObjects() adds AI to gameData.Players, then this
                 // method destroys them. Scene-transition cleanup already happens via
                 // SceneLoader.LoadSceneAsync() → ResetRuntimeData() + destroyWithScene.
                 ExecuteMultiplayerSetup().Forget();
diff --git a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
index b3e3b39de..446358079 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
@@ -181,7 +181,7 @@ namespace CosmicShore.Gameplay
 
             // Assign domain if not already set.
             // Persistent players get their domain in PrepareForNewScene() (called by FindUnprocessedPlayerByOwnerClientId).
-            // AI players get their domain in SpawnAIs() and are marked as processed (never reach here).
+            // AI players get their domain in SpawnAIPlayerObjects() (already set before reaching here).
             // New human players joining mid-game need assignment now.
             if (player.NetDomain.Value is Domains.Unassigned or Domains.None)
                 player.NetDomain.Value = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);
diff --git a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
index c7ad473c2..5f0353782 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
@@ -1,6 +1,8 @@
+using System.Threading;
 using CosmicShore.Data;
 using CosmicShore.ScriptableObjects;
 using CosmicShore.Utility;
+using Cysharp.Threading.Tasks;
 using Reflex.Injectors;
 using Unity.Netcode;
 using UnityEngine;
@@ -9,14 +11,25 @@ namespace CosmicShore.Gameplay
 {
     /// <summary>
     /// Extension of ServerPlayerVesselInitializer:
-    /// spawns server-owned AI players and their vessels, then delegates
-    /// human player handling to the base class via OnPlayerNetworkSpawnedUlong.
+    /// spawns server-owned AI Player objects, then delegates ALL vessel spawning
+    /// (AI and human) to the base class's unified pipeline.
+    ///
+    /// This mirrors the Menu_Main pattern where MenuServerPlayerVesselInitializer
+    /// overrides OnPlayerReadyToSpawnAsync to add post-spawn behavior (autopilot).
+    /// Here, the override configures the AIPilot after the base spawns the vessel.
     ///
     /// OnNetworkSpawn flow:
-    ///   1. SpawnAIs() — creates AI players + vessels (fires OnPlayerNetworkSpawnedUlong
-    ///      for each, but we haven't subscribed yet so the base ignores them)
-    ///   2. Mark AI players in _processedPlayers so the base never processes them
-    ///   3. base.OnNetworkSpawn() — subscribes to event + handles human players going forward
+    ///   1. SpawnAIPlayerObjects() — creates AI Player NetworkObjects and sets their
+    ///      NetworkVariables (name, vessel type, domain, IsAI). No vessel spawning.
+    ///      AI players fire OnPlayerNetworkSpawnedUlong, but the base hasn't
+    ///      subscribed yet so the events are harmlessly ignored.
+    ///   2. base.OnNetworkSpawn() — subscribes to OnPlayerNetworkSpawnedUlong,
+    ///      then ProcessPreExistingPlayers() catches all AI + human players
+    ///      and routes them through the standard pipeline:
+    ///      preSpawnDelay → SpawnVesselForPlayer → InitializePlayerAndVessel
+    ///      → postSpawnDelay → NotifyClients (RPCs to non-host clients).
+    ///   3. OnPlayerReadyToSpawnAsync override — after the base spawns and
+    ///      initializes each player's vessel, configures AIPilot for AI players.
     /// </summary>
     public class ServerPlayerVesselInitializerWithAI : ServerPlayerVesselInitializer
     {
@@ -53,49 +66,60 @@ namespace CosmicShore.Gameplay
             DomainAssigner.Initialize();
 
             // Set scene-specific spawn positions before AI spawning.
-            // base.OnNetworkSpawn() also sets them, but AI spawns happen first
-            // (before base runs), so positions must be configured here.
+            // base.OnNetworkSpawn() also sets them, but AI Player objects need
+            // spawn positions configured first (for AddPlayer → SetPoseOfVessel).
             if (playerSpawnPoints != null && playerSpawnPoints.Length > 0)
                 gameData.SetSpawnPositions(playerSpawnPoints);
 
-            // Spawn AIs BEFORE subscribing to OnPlayerNetworkSpawnedUlong.
-            // AI players fire the event during Spawn(), but since we haven't
-            // subscribed yet (base.OnNetworkSpawn hasn't run), those events
-            // are harmlessly ignored by the base.
+            // Spawn AI Player objects BEFORE subscribing to OnPlayerNetworkSpawnedUlong.
+            // AI players fire the event when their NetworkVariables are set
+            // (via TryRaiseDeferredSpawnEvent), but since we haven't subscribed yet
+            // (base.OnNetworkSpawn hasn't run), those events are harmlessly ignored.
+            // The base's ProcessPreExistingPlayers() catches them afterwards.
             // Wrapped in try-catch to guarantee base.OnNetworkSpawn() always
             // runs — otherwise no human players would be processed.
             if (spawnAIOnServerReady)
             {
                 try
                 {
-                    Debug.Log("<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Calling SpawnAIs()</color>");
-                    SpawnAIs();
-                    Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] SpawnAIs() complete. gameData.Players.Count={gameData.Players.Count}</color>");
+                    Debug.Log("<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Calling SpawnAIPlayerObjects()</color>");
+                    SpawnAIPlayerObjects();
+                    Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] SpawnAIPlayerObjects() complete. gameData.Players.Count={gameData.Players.Count}</color>");
                 }
                 catch (System.Exception e)
                 {
-                    Debug.LogError($"<color=#FF0000>[FLOW-5AI] [ServerVesselInitWithAI] SpawnAIs FAILED: {e.Message}\n{e.StackTrace}</color>");
-                    CSDebug.LogError($"[ServerPlayerVesselInitializerWithAI] SpawnAIs failed: {e.Message}");
+                    Debug.LogError($"<color=#FF0000>[FLOW-5AI] [ServerVesselInitWithAI] SpawnAIPlayerObjects FAILED: {e.Message}\n{e.StackTrace}</color>");
+                    CSDebug.LogError($"[ServerPlayerVesselInitializerWithAI] SpawnAIPlayerObjects failed: {e.Message}");
                 }
             }
 
-            // Mark all AI players as processed so the base skips them
-            int aiMarked = 0;
-            foreach (var p in gameData.Players)
-            {
-                if (p is Player aiPlayer && aiPlayer.NetIsAI.Value)
-                {
-                    _processedPlayers.Add(aiPlayer.NetworkObjectId);
-                    aiMarked++;
-                }
-            }
-            Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Marked {aiMarked} AI players as processed. Calling base.OnNetworkSpawn()</color>");
-
-            // Now subscribe (via base) and handle human players going forward
+            // Subscribe and process ALL players (AI + human) through the unified
+            // pipeline. ProcessPreExistingPlayers() catches AI players already in
+            // gameData.Players and routes them through HandlePlayerNetworkSpawnedAsync
+            // → OnPlayerReadyToSpawnAsync (our override) → ConfigureAIPilot.
+            Debug.Log("<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Calling base.OnNetworkSpawn() — unified pipeline for all players</color>");
             base.OnNetworkSpawn();
         }
 
-        void SpawnAIs()
+        /// <summary>
+        /// After the base spawns and initializes the vessel, configure AIPilot for AI players.
+        /// Same pattern as MenuServerPlayerVesselInitializer.OnPlayerReadyToSpawnAsync
+        /// which calls ActivateAutopilot after base.
+        /// </summary>
+        protected override async UniTask OnPlayerReadyToSpawnAsync(Player player, CancellationToken ct)
+        {
+            await base.OnPlayerReadyToSpawnAsync(player, ct);
+
+            if (player.NetIsAI.Value)
+                ConfigureAIPilot(player);
+        }
+
```

</details>
