# Branch archive: `claude/fix-game-modes-Offj3`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-04-03 by Claude
- **Unmerged commits:** 6
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/466
- **Forked from:** `9f3d78fbd` (2026-04-03, Update Menu_Main.unity)
- **Tip:** `958006713`
- **Files touched (5):**
  - `Assets/_Scenes/Multiplayer Scenes/MinigameCrystalCaptureMultiplayer_Gameplay.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity`
  - `Assets/_Scripts/Controller/Environment/Cell.cs`
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs`
  - `Assets/_Scripts/Utility/DataContainers/GameDataSO.cs`

### `69600b8ff` — fix(scenes): upgrade Joust and CrystalCapture to ServerPlayerVesselInitializerWithAI

_Claude, 2026-04-03 16:56:10 +0000_

```text
Both scenes were using the base ServerPlayerVesselInitializer which lacks
DomainAssigner.Initialize(), AI backfill spawning, and had empty
playerSpawnPoints. Upgraded to ServerPlayerVesselInitializerWithAI
(matching HexRace) and wired spawn points, AI player prefab, AI init
data templates, and AI profile list.
```

```text
 .../Multiplayer Scenes/MinigameCrystalCaptureMultiplayer_Gameplay.unity | 25 +++++++++++++++++++++----
 Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity          | 31 +++++++++++++++++++++++++++----
 2 files changed, 48 insertions(+), 8 deletions(-)
```

### `85e681b39` — fix(scenes): add ContainerScope prefab to Joust and CrystalCapture scenes

_Claude, 2026-04-03 17:48:22 +0000_

```text
Both scenes were missing the Reflex ContainerScope prefab instance that
HexRace has, causing all [Inject] fields (gameData, etc.) to be null.
This produced NullReferenceExceptions in OnNetworkSpawn for
ServerPlayerVesselInitializerWithAI, NetworkScoreTracker,
NetworkVolumeUIController, and MultiplayerMiniGameControllerBase.
```

```text
 .../MinigameCrystalCaptureMultiplayer_Gameplay.unity                  | 69 +++++++++++++++++++++++++++++++++
 Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity        | 69 +++++++++++++++++++++++++++++++++
 2 files changed, 138 insertions(+)
```

### `cc746c6b9` — fix(scene): remove missing script component from CrystalCapture scene

_Claude, 2026-04-03 17:50:06 +0000_

```text
The Game GameObject had a MonoBehaviour referencing a deleted script
(GUID 9f7a7af0f6bd4545ba6e0f5f622342c0, had a cellData field). This
caused the "referenced script on this Behaviour" warning on scene load.
```

```text
 Assets/_Scenes/Multiplayer Scenes/MinigameCrystalCaptureMultiplayer_Gameplay.unity | 16 ++--------------
 1 file changed, 2 insertions(+), 14 deletions(-)
```

### `b7ec34727` — fix(multiplayer): respect player's chosen team domain in single-team mode

_Claude, 2026-04-03 19:14:18 +0000_

```text
When RequestedTeamCount=1, NormalizeHumanDomains() was building a
validDomains set containing only Jade (TeamDomains[0]), rejecting the
player's chosen domain (e.g. Ruby) and defaulting everyone to Jade.

Fix: when teamCount=1, accept whatever domain the human player chose.
Also updated BuildTeamCounts() to include the player's actual domain
in single-team mode so AI backfill joins the correct team.
```

```text
 Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs | 21 +++++++++++++++------
 Assets/_Scripts/Utility/DataContainers/GameDataSO.cs                          | 13 ++++++++-----
 2 files changed, 23 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
index 9dbae6288..59ea492c4 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
@@ -205,18 +205,15 @@ namespace CosmicShore.Gameplay
         void NormalizeHumanDomains()
         {
             int teamCount = Mathf.Clamp(gameData.RequestedTeamCount, 1, 3);
-            var validDomains = new HashSet<Domains>();
-            for (int i = 0; i < teamCount; i++)
-                validDomains.Add(GameDataSO.TeamDomains[i]);
 
-            // Find the first human player's domain to use as the party team
+            // Find the first human player's chosen domain
             Domains partyDomain = Domains.Unassigned;
             foreach (var p in gameData.Players)
             {
                 if (p is Player player && !player.NetIsAI.Value)
                 {
                     var domain = player.NetDomain.Value;
-                    if (validDomains.Contains(domain) && domain != Domains.Unassigned)
+                    if (domain != Domains.Unassigned && domain != Domains.None)
                     {
                         partyDomain = domain;
                         break;
@@ -224,10 +221,22 @@ namespace CosmicShore.Gameplay
                 }
             }
 
-            // If no valid domain found, pick the first valid team
+            // If no human has a valid domain, pick the first team
             if (partyDomain == Domains.Unassigned)
                 partyDomain = GameDataSO.TeamDomains[0];
 
+            // When multiple teams are active, ensure the party domain is within
+            // the valid team set. Single-team mode always respects the player's choice.
+            if (teamCount > 1)
+            {
+                var validDomains = new HashSet<Domains>();
+                for (int i = 0; i < teamCount; i++)
+                    validDomains.Add(GameDataSO.TeamDomains[i]);
+
+                if (!validDomains.Contains(partyDomain))
+                    partyDomain = GameDataSO.TeamDomains[0];
+            }
+
             // Assign all human players to the party domain
             foreach (var p in gameData.Players)
             {
diff --git a/Assets/_Scripts/Utility/DataContainers/GameDataSO.cs b/Assets/_Scripts/Utility/DataContainers/GameDataSO.cs
index 08d343004..3ef9b1de4 100644
--- a/Assets/_Scripts/Utility/DataContainers/GameDataSO.cs
+++ b/Assets/_Scripts/Utility/DataContainers/GameDataSO.cs
@@ -606,20 +606,23 @@ namespace CosmicShore.Utility
             for (int i = 0; i < teamCount; i++)
                 counts[TeamDomains[i]] = 0;
 
+            // When teamCount=1, include whatever domain players actually have
+            // so that AI backfill joins the same team (e.g. Ruby, not always Jade).
             foreach (var p in Players)
             {
                 if (p is not Player player) continue;
 
                 var domain = player.NetDomain.Value;
-                if (counts.ContainsKey(domain))
+                if (!counts.ContainsKey(domain) && teamCount == 1
+                    && domain != Domains.Unassigned && domain != Domains.None)
                 {
-                    counts[domain]++;
+                    counts[domain] = 0;
                 }
+
+                if (counts.ContainsKey(domain))
+                    counts[domain]++;
                 else
-                {
-                    // Player has a domain outside the active set — count on first team
                     counts[TeamDomains[0]]++;
-                }
             }
 
             return counts;
```

</details>

### `34e8a1da3` — fix(cell): defer OnInitializeGame subscription for DI injection timing

_Claude, 2026-04-03 19:22:27 +0000_

```text
Cell.cs was converted from [SerializeField] GameDataSO to [Inject],
but OnEnable() runs before Reflex injection (which happens between
Awake and Start). gameData was null in OnEnable(), so the
OnInitializeGame subscription never happened and Initialize() never
ran — preventing flora/gyroid spawning.

Added deferred subscription pattern: try in OnEnable(), retry with
duplicate guard in Start() when the injected field is available.
```

```text
 Assets/_Scripts/Controller/Environment/Cell.cs | 24 +++++++++++++++++++++---
 1 file changed, 21 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index 47e54ab44..5a93c5944 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -55,10 +55,11 @@ namespace CosmicShore.Gameplay
         ICellLifeSpawner activeSpawner;
         bool postInitilized = false;
 
+        bool _subscribedToInitialize;
+
         void OnEnable()
         {
-            if (gameData != null)
-                gameData.OnInitializeGame.OnRaised += Initialize;
+            TrySubscribeToInitialize();
 
             if (!runtime) return;
 
@@ -70,10 +71,27 @@ namespace CosmicShore.Gameplay
                 runtime.OnResetForReplay.OnRaised += ResetCell;
         }
 
+        void Start()
+        {
+            // Deferred subscription: [Inject] fields are populated after Awake()
+            // but before Start(), so OnEnable() may run before gameData is injected.
+            TrySubscribeToInitialize();
+        }
+
+        void TrySubscribeToInitialize()
+        {
+            if (_subscribedToInitialize || gameData == null) return;
+            gameData.OnInitializeGame.OnRaised += Initialize;
+            _subscribedToInitialize = true;
+        }
+
         void OnDisable()
         {
-            if (gameData != null)
+            if (gameData != null && _subscribedToInitialize)
+            {
                 gameData.OnInitializeGame.OnRaised -= Initialize;
+                _subscribedToInitialize = false;
+            }
 
             if (runtime != null)
             {
```

</details>

### `958006713` — Revert "fix(cell): defer OnInitializeGame subscription for DI injection timing"

_Claude, 2026-04-03 19:25:22 +0000_

```text
This reverts commit 34e8a1da3784c350ad11f71013cb620b3b70d867.
```

```text
 Assets/_Scripts/Controller/Environment/Cell.cs | 24 +++---------------------
 1 file changed, 3 insertions(+), 21 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index 5a93c5944..47e54ab44 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -55,11 +55,10 @@ namespace CosmicShore.Gameplay
         ICellLifeSpawner activeSpawner;
         bool postInitilized = false;
 
-        bool _subscribedToInitialize;
-
         void OnEnable()
         {
-            TrySubscribeToInitialize();
+            if (gameData != null)
+                gameData.OnInitializeGame.OnRaised += Initialize;
 
             if (!runtime) return;
 
@@ -71,27 +70,10 @@ namespace CosmicShore.Gameplay
                 runtime.OnResetForReplay.OnRaised += ResetCell;
         }
 
-        void Start()
-        {
-            // Deferred subscription: [Inject] fields are populated after Awake()
-            // but before Start(), so OnEnable() may run before gameData is injected.
-            TrySubscribeToInitialize();
-        }
-
-        void TrySubscribeToInitialize()
-        {
-            if (_subscribedToInitialize || gameData == null) return;
-            gameData.OnInitializeGame.OnRaised += Initialize;
-            _subscribedToInitialize = true;
-        }
-
         void OnDisable()
         {
-            if (gameData != null && _subscribedToInitialize)
-            {
+            if (gameData != null)
                 gameData.OnInitializeGame.OnRaised -= Initialize;
-                _subscribedToInitialize = false;
-            }
 
             if (runtime != null)
             {
```

</details>
