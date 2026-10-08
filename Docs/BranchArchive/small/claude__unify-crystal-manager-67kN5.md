# Branch archive: `claude/unify-crystal-manager-67kN5`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-01 by Claude
- **Unmerged commits:** 2
- **Forked from:** `665737340` (2026-03-01, Update Bootstrap.unity)
- **Tip:** `17f308483`
- **Files touched (19):**
  - `Assets/_Scenes/Menu_Main.unity`
  - `Assets/_Scenes/Multiplayer Scenes/ArcadeGameMultiplayer2v2CoOpVsAI.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameCrystalCaptureMultiplayer_Gameplay.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameDuelForCellMultiplayer_Gameplay.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameFreestyleMultiplayer_Gameplay.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameHexRace.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameTournamentMultuplayer.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameWildlifeBlitzMultuplayerCoOp.unity`
  - `Assets/_Scenes/Singleplayer Scenes/MinigameCellularDuel.unity`
  - `Assets/_Scenes/Singleplayer Scenes/MinigameFreestyle.unity`
  - `Assets/_Scenes/Singleplayer Scenes/MinigameWildlifeBlitz.unity`
  - `Assets/_Scripts/Controller/Arcade/SinglePlayerFreestyleController.cs`
  - `Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs`
  - `Assets/_Scripts/Controller/Environment/FlowField/LocalCrystalManager.cs`
  - `Assets/_Scripts/Controller/Environment/FlowField/LocalCrystalManager.cs.meta`
  - `Assets/_Scripts/Controller/Environment/FlowField/NetworkCrystalManager.cs`
  - `Assets/_Scripts/Controller/Environment/FlowField/NetworkCrystalManager.cs.meta`
  - `Assets/_Scripts/Controller/Environment/MiniGameObjects/ShapeDrawingCrystalManager.cs`
  - `Assets/_Scripts/Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs`

### `1994c4e87` — refactor(crystals): unify CrystalManager into single RPC-based implementation

_Claude, 2026-03-01 06:30:25 +0000_

```text
Merge LocalCrystalManager and NetworkCrystalManager into a single
CrystalManager that works in both single-player and multiplayer contexts.

In multiplayer, the server computes spawn/respawn positions and broadcasts
them via ClientRpc. All clients instantiate local (non-networked) crystal
GameObjects at the received positions. Respawn is just another RPC to
update crystal position.

In single-player (no active NetworkManager), the same logic runs locally
without RPCs.

- Remove LocalCrystalManager.cs and NetworkCrystalManager.cs
- Replace NetworkList<Vector3> replication with ClientRpc arrays
- Add dual-path (networked vs local) in RespawnCrystal and ExplodeCrystal
- Update ShapeDrawingCrystalManager to extend unified CrystalManager
- Update SinglePlayerFreestyleController and ShapeDrawingManager references
- Remap script GUIDs in all 11 affected scene files
```

```text
 Assets/_Scenes/Menu_Main.unity                                        |   2 +-
 .../_Scenes/Multiplayer Scenes/ArcadeGameMultiplayer2v2CoOpVsAI.unity |   2 +-
 .../MinigameCrystalCaptureMultiplayer_Gameplay.unity                  |   2 +-
 .../Multiplayer Scenes/MinigameDuelForCellMultiplayer_Gameplay.unity  |   2 +-
 .../Multiplayer Scenes/MinigameFreestyleMultiplayer_Gameplay.unity    |   2 +-
 Assets/_Scenes/Multiplayer Scenes/MinigameHexRace.unity               |   2 +-
 Assets/_Scenes/Multiplayer Scenes/MinigameTournamentMultuplayer.unity |   2 +-
 .../Multiplayer Scenes/MinigameWildlifeBlitzMultuplayerCoOp.unity     |   2 +-
 Assets/_Scenes/Singleplayer Scenes/MinigameCellularDuel.unity         |   2 +-
 Assets/_Scenes/Singleplayer Scenes/MinigameFreestyle.unity            |   2 +-
 Assets/_Scenes/Singleplayer Scenes/MinigameWildlifeBlitz.unity        |   2 +-
 Assets/_Scripts/Controller/Arcade/SinglePlayerFreestyleController.cs  |   2 +-
 Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs    | 424 +++++++++++++++++++++++---------
 .../_Scripts/Controller/Environment/FlowField/LocalCrystalManager.cs  |  72 ------
 .../Controller/Environment/FlowField/LocalCrystalManager.cs.meta      |   3 -
 .../Controller/Environment/FlowField/NetworkCrystalManager.cs         | 214 ----------------
 .../Controller/Environment/FlowField/NetworkCrystalManager.cs.meta    |   3 -
 .../Environment/MiniGameObjects/ShapeDrawingCrystalManager.cs         |  28 +--
 .../Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs     |   2 +-
 19 files changed, 331 insertions(+), 439 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 982 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/SinglePlayerFreestyleController.cs b/Assets/_Scripts/Controller/Arcade/SinglePlayerFreestyleController.cs
index cf7cb422d..8574cff76 100644
--- a/Assets/_Scripts/Controller/Arcade/SinglePlayerFreestyleController.cs
+++ b/Assets/_Scripts/Controller/Arcade/SinglePlayerFreestyleController.cs
@@ -16,7 +16,7 @@ namespace CosmicShore.Gameplay
         [Header("Environment")]
         [SerializeField] SegmentSpawner segmentSpawner;
         [SerializeField] ShapeDrawingManager shapeDrawingManager;
-        [SerializeField] LocalCrystalManager localCrystalManager;
+        [SerializeField] CrystalManager localCrystalManager;
         [SerializeField] Cell cellScript;
 
         [Header("HUD")]
diff --git a/Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs b/Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs
index 53f36d687..bce953458 100644
--- a/Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs
+++ b/Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs
@@ -3,12 +3,12 @@ using System.Collections.Generic;
 using CosmicShore.Utility;
 using Obvious.Soap;
 using Reflex.Attributes;
+using Unity.Collections;
 using Unity.Netcode;
 using UnityEngine;
 using Random = UnityEngine.Random;
 using CosmicShore.Data;
-using CosmicShore.Gameplay;
-using System.Linq;
+
 namespace CosmicShore.Gameplay
 {
     [Serializable]
@@ -18,18 +18,22 @@ namespace CosmicShore.Gameplay
     }
 
     /// <summary>
-    /// Base crystal manager:
-    /// - Handles spawn + respawn logic for multiple crystals.
-    /// - Provides anchor-based spawn positions (pre-authored anchor lists).
-    /// - Batch spawn uses ONE anchor per batch, so all crystals in the batch cluster together.
-    /// - Respawn uses per-crystal "next anchor" progression, so each crystal moves along anchors independently.
+    /// Unified crystal manager that works in both single-player and multiplayer contexts.
+    ///
+    /// In multiplayer the server computes spawn/respawn positions and broadcasts them via
+    /// ClientRpc. Every client (including the host) instantiates purely-local crystal
+    /// GameObjects at the received positions — crystals are never NetworkObjects.
+    ///
+    /// In single-player (no active NetworkManager) the same logic runs locally without RPCs.
+    ///
+    /// Spawn modes:
+    ///   • Crystal Capture / Freestyle — random positions on a unit-sphere around an anchor.
+    ///     Crystal count = player count + extraCrystalsToSpawnBeyondPlayerCount.
+    ///   • Hex Race — pre-defined anchor positions configured per intensity in the inspector.
+    ///     No randomisation; crystals land on the authored positions.
     /// </summary>
-    public abstract class CrystalManager : NetworkBehaviour
+    public class CrystalManager : NetworkBehaviour
     {
-        // IMPORTANT:
-        // We compare Vector3.SqrMagnitude(...) <= MIN_SQR_DISTANCE.
-        // So this constant is "distance squared".
-        // If you want minimum distance of 25 units, set this to 25f * 25f = 625.
         private const float MIN_SQR_SPACE_BTWN_CURRENT_AND_LAST_SPAWN_POS = 25f;
 
         [Header("Dependencies")]
@@ -44,47 +48,239 @@ namespace CosmicShore.Gameplay
         [SerializeField] private IntVariable intensityLevelData;
         [SerializeField] private List<CrystalPositionSet> listOfCrystalPositions;
 
+        [Header("Crystal Configuration")]
         [SerializeField] private bool spawnCrystalWithPlayerDomain;
-        [SerializeField] private int extraCrystalsToSpawnBeyondPlayerCount = 0;
-        
+        [SerializeField] private int extraCrystalsToSpawnBeyondPlayerCount;
+
+        [Header("Spawn Trigger")]
+        [Tooltip("When true, crystals spawn on OnClientReady instead of on turn start.")]
+        [SerializeField] private bool spawnOnClientReady;
+
         // ---------------- Runtime State ----------------
 
-        // Tracks the last spawn position per crystal id (used to keep respawns away from their last position).
         private readonly Dictionary<int, Vector3> lastSpawnPosById = new();
-
-        // Tracks the last anchor index used per crystal id.
-        // Respawn will increment this to use the NEXT anchor index.
         private readonly Dictionary<int, int> lastAnchorIndexByCrystalId = new();
-
-        // Used ONLY for batch spawns (one anchor per batch).
-        // Respawn does NOT use this global index (it uses per-crystal anchor index).
         private int batchAnchorIndex;
-
-        // Used for stable initialization IDs for CellItems
         private int itemsAdded;
-        
+
+        /// <summary>True when a NetworkManager is active and this object is network-spawned.</summary>
+        private bool IsNetworked => IsSpawned;
+
+        // ================================================================
+        // Lifecycle
+        // ================================================================
+
         protected virtual void Awake()
         {
-            // Ensure runtime lists exist
             cellData.CellItems = new List<CellItem>();
             cellData.Crystals ??= new List<Crystal>();
         }
 
-        // ------------------------------------------------------------
-        // CellItem management (unchanged conceptually)
-        // ------------------------------------------------------------
+        public override void OnNetworkSpawn()
+        {
+            SubscribeToEvents();
+        }
+
+        public override void OnNetworkDespawn()
+        {
+            UnsubscribeFromEvents();
+        }
+
+        protected virtual void OnEnable()
+        {
+            // Non-networked path: subscribe immediately.
+            // Networked path: OnNetworkSpawn handles subscription.
+            if (!IsNetworked)
+                SubscribeToEvents();
+        }
+
+        protected virtual void OnDisable()
+        {
+            if (!IsNetworked)
+                UnsubscribeFromEvents();
+        }
+
+        private bool _subscribed;
+
+        private void SubscribeToEvents()
+        {
+            if (_subscribed) return;
+            _subscribed = true;
+
+            if (spawnOnClientReady)
+                gameData.OnClientReady.OnRaised += OnSpawnTrigger;
+            else
+                gameData.OnMiniGameTurnStarted.OnRaised += OnSpawnTrigger;
+
+            gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
```

</details>

### `17f308483` — fix(crystals): always unsubscribe in OnDisable regardless of network state

_Claude, 2026-03-01 06:53:27 +0000_

```text
The previous implementation skipped unsubscription when IsNetworked was
true, which meant a disabled CrystalManager could still receive SOAP
events. Now OnEnable/OnDisable always subscribe/unsubscribe, with the
_subscribed guard preventing double subscription from OnNetworkSpawn.
```

```text
 Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs | 12 ++++++------
 1 file changed, 6 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs b/Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs
index bce953458..eb50ac1fe 100644
--- a/Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs
+++ b/Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs
@@ -88,16 +88,16 @@ namespace CosmicShore.Gameplay
 
         protected virtual void OnEnable()
         {
-            // Non-networked path: subscribe immediately.
-            // Networked path: OnNetworkSpawn handles subscription.
-            if (!IsNetworked)
-                SubscribeToEvents();
+            // Subscribe in OnEnable for both networked and non-networked paths.
+            // The _subscribed guard prevents double subscription when OnNetworkSpawn
+            // also fires. In multiplayer, OnSpawnTrigger() gates on IsServer so
+            // early subscription (before network spawn) is harmless.
+            SubscribeToEvents();
         }
 
         protected virtual void OnDisable()
         {
-            if (!IsNetworked)
-                UnsubscribeFromEvents();
+            UnsubscribeFromEvents();
         }
 
         private bool _subscribed;
```

</details>
