# Branch archive: `claude/fix-player-spawning-k3GR2`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-31 by Claude
- **Unmerged commits:** 1
- **Forked from:** `1a310a189` (2026-03-31, Update Menu_Main.unity)
- **Tip:** `c31458fe0`
- **Files touched (3):**
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs`
  - `Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs`
  - `Assets/_Scripts/Utility/DataContainers/GameDataSO.cs`

### `c31458fe0` — fix(spawning): cap player count at MaxTotalPlayers, resolve Unassigned domains before AI spawn

_Claude, 2026-03-31 22:38:50 +0000_

```text
- Add GameDataSO.MaxTotalPlayers (default 4) as single source of truth for
  the player count cap, replacing the hardcoded const in ArcadeGameConfigureModal
- Fix PlayerCountStepper receiving raw game.MaxPlayersAllowed (up to 12 for
  HexRace) instead of the capped value
- Fix HandlePlayerCountSelected clamping against uncapped MaxPlayersAllowed
- Re-initialize stepper bounds dynamically in RefreshPlayerCountStepper so
  party size changes mid-config are reflected immediately
- Add ResolveUnassignedDomains() in ServerPlayerVesselInitializerWithAI to
  assign actual teams to humans who picked Random before AI team balancing
```

```text
 .../Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs     | 40 +++++++++++++++++++++++++++++++++
 Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs                 | 32 ++++++++++++++++++--------
 Assets/_Scripts/Utility/DataContainers/GameDataSO.cs                  |  4 ++++
 3 files changed, 67 insertions(+), 9 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
index 02b5ad601..26f1fa0ef 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
@@ -105,6 +105,10 @@ namespace CosmicShore.Gameplay
                 return;
             }
 
+            // Resolve any human players who picked "Random" (Unassigned) to actual teams
+            // before building team counts, so AI balancing sees every human.
+            ResolveUnassignedDomains();
+
             int aiCount = gameData.RequestedAIBackfillCount;
             Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] SpawnAIs — aiCount={aiCount}</color>");
             if (aiCount <= 0)
@@ -193,6 +197,42 @@ namespace CosmicShore.Gameplay
             return best;
         }
 
+        /// <summary>
+        /// Resolves human players whose NetDomain is Unassigned (Random) to an actual
+        /// team before AI spawning, so BuildTeamCounts() sees every human player.
+        /// Uses the same balanced-assignment algorithm as AI: pick the team with fewest players.
+        /// </summary>
+        void ResolveUnassignedDomains()
+        {
+            var teamCounts = new Dictionary<Domains, int>
+            {
+                { Domains.Jade, 0 },
+                { Domains.Ruby, 0 },
+                { Domains.Gold, 0 }
+            };
+
+            var unassigned = new List<Player>();
+
+            foreach (var p in gameData.Players)
+            {
+                if (p is not Player player) continue;
+                if (player.NetIsAI.Value) continue;
+
+                if (teamCounts.ContainsKey(player.NetDomain.Value))
+                    teamCounts[player.NetDomain.Value]++;
+                else
+                    unassigned.Add(player);
+            }
+
+            foreach (var player in unassigned)
+            {
+                var domain = GetBalancedDomain(teamCounts);
+                player.NetDomain.Value = domain;
+                teamCounts[domain]++;
+                Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Resolved Unassigned domain for '{player.NetName.Value}' → {domain}</color>");
+            }
+        }
+
         VesselClassType PickAIVesselType()
         {
             if (gameList != null)
diff --git a/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs b/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs
index 5d6831f2b..00b36057b 100644
--- a/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs
+++ b/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs
@@ -101,11 +101,15 @@ namespace CosmicShore.UI
         [Header("Network Sync")]
         [SerializeField] private ArcadeConfigSyncManager arcadeConfigSyncManager;
 
-        // Hard cap on the number of players/teams the game supports
-        const int MaxSupportedPlayers = 4;
         const int MaxSupportedTeams = 3;
         const int MinTeams = 1;
 
+        /// <summary>
+        /// Effective max players: reads from GameDataSO.MaxTotalPlayers (shared config).
+        /// Falls back to 4 if gameData isn't injected yet.
+        /// </summary>
+        int MaxSupportedPlayers => gameData != null ? gameData.MaxTotalPlayers : 4;
+
         // Runtime state
         SO_ArcadeGame _selectedGame;
         VideoPlayer   _previewVideo;
@@ -350,9 +354,11 @@ namespace CosmicShore.UI
             // fewer total players than there are humans in the lobby.
             int effectiveMin = Mathf.Max(game.MinPlayersAllowed, CurrentPartyHumanCount);
 
+            int effectiveMax = Mathf.Min(game.MaxPlayersAllowed, MaxSupportedPlayers);
+
             // Component stepper UI (preferred — supports 1-12 range)
             if (playerCountStepper)
-                playerCountStepper.Initialize(effectiveMin, game.MaxPlayersAllowed, config.PlayerCount);
+                playerCountStepper.Initialize(effectiveMin, effectiveMax, config.PlayerCount);
 
             // Inline stepper UI (development UI)
             RefreshPlayerCountStepper();
@@ -491,7 +497,8 @@ namespace CosmicShore.UI
             if (IsClientMode) return;
 
             int effectiveMin = Mathf.Max(_selectedGame.MinPlayersAllowed, CurrentPartyHumanCount);
-            playerCount        = Mathf.Clamp(playerCount, effectiveMin, _selectedGame.MaxPlayersAllowed);
+            int effectiveMax = Mathf.Min(_selectedGame.MaxPlayersAllowed, MaxSupportedPlayers);
+            playerCount        = Mathf.Clamp(playerCount, effectiveMin, effectiveMax);
             config.PlayerCount = playerCount;
 
             if (playerCountStepper)
@@ -556,19 +563,26 @@ namespace CosmicShore.UI
 
         void RefreshPlayerCountStepper()
         {
-            if (playerCountValueText)
-                playerCountValueText.text = config.PlayerCount.ToString();
-
             if (_selectedGame == null) return;
 
             int effectiveMin = Mathf.Max(_selectedGame.MinPlayersAllowed, CurrentPartyHumanCount);
-            int max = Mathf.Min(_selectedGame.MaxPlayersAllowed, MaxSupportedPlayers);
+            int effectiveMax = Mathf.Min(_selectedGame.MaxPlayersAllowed, MaxSupportedPlayers);
+
+            // Clamp config in case party size grew after the modal opened
+            config.PlayerCount = Mathf.Clamp(config.PlayerCount, effectiveMin, effectiveMax);
+
+            if (playerCountValueText)
+                playerCountValueText.text = config.PlayerCount.ToString();
+
+            // Re-initialize the component stepper so its internal min/max stay current
+            if (playerCountStepper)
+                playerCountStepper.Initialize(effectiveMin, effectiveMax, config.PlayerCount);
 
             if (playerCountDecrementButton)
                 playerCountDecrementButton.interactable = config.PlayerCount > effectiveMin && !IsClientMode;
 
             if (playerCountIncrementButton)
-                playerCountIncrementButton.interactable = config.PlayerCount < max && !IsClientMode;
+                playerCountIncrementButton.interactable = config.PlayerCount < effectiveMax && !IsClientMode;
         }
 
         #endregion
diff --git a/Assets/_Scripts/Utility/DataContainers/GameDataSO.cs b/Assets/_Scripts/Utility/DataContainers/GameDataSO.cs
index 5b4f401a4..9b770f079 100644
--- a/Assets/_Scripts/Utility/DataContainers/GameDataSO.cs
+++ b/Assets/_Scripts/Utility/DataContainers/GameDataSO.cs
@@ -54,6 +54,10 @@ namespace CosmicShore.Utility
         
         
         // Game Config / State
+        [Header("Player Count Limits")]
+        [Tooltip("Hard cap on total players (human + AI) per game session. All UI and spawning systems read this.")]
+        public int MaxTotalPlayers = 4;
+
         public string SceneName;
         public GameModes GameMode;
         public string LocalPlayerDisplayName;
```

</details>
