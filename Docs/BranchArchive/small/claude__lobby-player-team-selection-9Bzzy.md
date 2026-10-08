# Branch archive: `claude/lobby-player-team-selection-9Bzzy`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-03 by Claude
- **Unmerged commits:** 1
- **Forked from:** `e43b80025` (2026-03-04, Merge pull request #342 from froglet-studio/claude/hex-race-unique-domains-7HB)
- **Tip:** `c929e31a3`
- **Files touched (9):**
  - `Assets/_Scripts/Controller/Multiplayer/DomainAssigner.cs`
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs`
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs`
  - `Assets/_Scripts/Controller/Multiplayer/Tests/DomainAssignerTests.cs`
  - `Assets/_Scripts/Controller/Player/Player.cs`
  - `Assets/_Scripts/ScriptableObjects/SO_ArcadeGame.cs`
  - `Assets/_Scripts/UI/Elements/PlayerCountStepper.cs`
  - `Assets/_Scripts/UI/Elements/TeamSelectionPanel.cs`
  - `Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs`

### `c929e31a3` — feat(lobby): add player count stepper (1-12) and team selection (Jade/Ruby/Gold)

_Claude, 2026-03-03 23:26:20 +0000_

```text
- Raise SO_ArcadeGame.MaxPlayers Range cap from 3 to 12 (per-mode configurable)
- Add NetPreferredDomain NetworkVariable (Owner-writable) to Player for team preference
- Rewrite DomainAssigner with GetBalancedAIDomains() for team-balanced AI assignment
- Create PlayerCountStepper UI component (+/- buttons replacing 4 discrete buttons)
- Create TeamSelectionPanel UI component (3 team buttons: Jade, Ruby, Gold)
- Integrate stepper and team panel into ArcadeGameConfigureModal
- ServerPlayerVesselInitializer reads NetPreferredDomain for human domain assignment
- ServerPlayerVesselInitializerWithAI uses balanced team assignment for AI spawning
- Add 8 new unit tests for DomainAssigner team-balancing logic
- Preserve legacy DomainAssigner API for co-op modes (backward compatible)
```

```text
 Assets/_Scripts/Controller/Multiplayer/DomainAssigner.cs              | 107 +++++++++++++++++++++++-----
 .../_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs  |   7 +-
 .../Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs     |  43 ++++++++++-
 Assets/_Scripts/Controller/Multiplayer/Tests/DomainAssignerTests.cs   | 122 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/Player/Player.cs                           |  10 ++-
 Assets/_Scripts/ScriptableObjects/SO_ArcadeGame.cs                    |   2 +-
 Assets/_Scripts/UI/Elements/PlayerCountStepper.cs                     |  76 ++++++++++++++++++++
 Assets/_Scripts/UI/Elements/TeamSelectionPanel.cs                     |  86 ++++++++++++++++++++++
 Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs                 |  32 +++------
 9 files changed, 439 insertions(+), 46 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 671 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/DomainAssigner.cs b/Assets/_Scripts/Controller/Multiplayer/DomainAssigner.cs
index 9fad0aa5d..461d2d7b3 100644
--- a/Assets/_Scripts/Controller/Multiplayer/DomainAssigner.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/DomainAssigner.cs
@@ -9,34 +9,101 @@ namespace CosmicShore.Gameplay
 {
     public static class DomainAssigner
     {
-        private static List<Domains> availableDomains = new ();
+        public static readonly Domains[] PlayableDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };
+
+        private static List<Domains> availableDomains = new();
 
         /// <summary>
-        /// Picks a unique random team from all Domains (excluding None, Unassigned, Blue).
-        /// If all are already assigned, logs an error and returns Domains.Unassigned.
+        /// Assigns domains to AI players to balance teams given human preferences.
+        /// Returns a list of Domains for AI players (length = aiCount).
+        /// AI are assigned to the smallest team first (greedy round-robin).
+        /// </summary>
+        public static List<Domains> GetBalancedAIDomains(List<Domains> humanDomains, int aiCount)
+        {
+            if (aiCount <= 0)
+                return new List<Domains>();
+
+            // Count humans per playable team
+            var teamCounts = new Dictionary<Domains, int>();
+            foreach (var d in PlayableDomains)
+                teamCounts[d] = 0;
+
+            foreach (var d in humanDomains)
+            {
+                if (teamCounts.ContainsKey(d))
+                    teamCounts[d]++;
+            }
+
+            // Assign AI to smallest team first (greedy balance)
+            var aiDomains = new List<Domains>(aiCount);
+            for (int i = 0; i < aiCount; i++)
+            {
+                var smallest = GetSmallestTeam(teamCounts);
+                aiDomains.Add(smallest);
+                teamCounts[smallest]++;
+            }
+
+            return aiDomains;
+        }
+
+        /// <summary>
+        /// Returns the domain with the fewest players. Breaks ties deterministically
+        /// by preferring the earlier domain in PlayableDomains order.
+        /// </summary>
+        static Domains GetSmallestTeam(Dictionary<Domains, int> teamCounts)
+        {
+            var smallest = PlayableDomains[0];
+            int smallestCount = teamCounts[smallest];
+
+            for (int i = 1; i < PlayableDomains.Length; i++)
+            {
+                var d = PlayableDomains[i];
+                if (teamCounts[d] < smallestCount)
+                {
+                    smallest = d;
+                    smallestCount = teamCounts[d];
+                }
+            }
+
+            return smallest;
+        }
+
+        /// <summary>
+        /// Returns whether a domain is a valid playable team (Jade, Ruby, or Gold).
+        /// </summary>
+        public static bool IsPlayableDomain(Domains domain)
+        {
+            return domain is Domains.Jade or Domains.Ruby or Domains.Gold;
+        }
+
+        #region Legacy API (backward compatibility)
+
+        /// <summary>
+        /// Picks a unique random team from the pool (excluding None, Unassigned, Blue).
+        /// If the pool is empty, returns Domains.Unassigned.
+        /// Legacy: used by single-player mode and co-op modes.
         /// </summary>
         static Domains GetAvailableDomain()
         {
+            if (availableDomains.Count == 0)
+            {
+                CSDebug.LogWarning("[DomainAssigner] No domains left in legacy pool.");
+                return Domains.Unassigned;
+            }
+
             int idx = UnityEngine.Random.Range(0, availableDomains.Count);
             var chosen = availableDomains[idx];
-
-            // Mark it as used
             availableDomains.RemoveAt(idx);
             return chosen;
         }
-    
+
         /// <summary>
-        /// TEMP Method to assign domains to players based on game modes,
-        /// later need to transfer this logic to support all game modes and co-op
-        /// with specified player count per domain
+        /// Legacy method to assign domains based on game modes.
+        /// Co-op modes return Jade; competitive modes pick from pool.
+        /// Prefer GetBalancedAIDomains() for team-based modes with AI backfill.
         /// </summary>
         public static Domains GetDomainsByGameModes(GameModes gameMode)
         {
-            // If no teams left, log a warning and return Unassigned instead of
-            // silently re-initializing.  Re-initializing mid-session was the root
-            // cause of duplicate / swapped domains in 3-player games because the
-            // fresh pool could hand out a domain that was already assigned to
-            // another player earlier in the same session.
             if (availableDomains.Count == 0)
             {
                 CSDebug.LogWarning("[DomainAssigner] No domains left in pool. " +
@@ -44,22 +111,24 @@ namespace CosmicShore.Gameplay
                 return Domains.Unassigned;
             }
 
-            // Considering in co-op modes, all local users will be assigned to Jade Domain
-            return gameMode is GameModes.Multiplayer2v2CoOpVsAI or GameModes.MultiplayerWildlifeBlitzGame ? Domains.Jade : GetAvailableDomain();
+            return gameMode is GameModes.Multiplayer2v2CoOpVsAI or GameModes.MultiplayerWildlifeBlitzGame
+                ? Domains.Jade
+                : GetAvailableDomain();
         }
 
         /// <summary>
-        /// Clears all assigned teams (use when restarting or resetting game).
+        /// Resets the legacy unique-domain pool. Call before each session.
         /// </summary>
         public static void Initialize()
         {
             availableDomains.Clear();
-            // Get all valid teams (excluding reserved ones)
             availableDomains = Enum.GetValues(typeof(Domains))
                 .Cast<Domains>()
                 .Where(t => t is not (Domains.None or Domains.Unassigned or Domains.Blue))
                 .ToList();
-            CSDebug.Log("[DomainAssigner] 🔄 Cleared assigned domains cache.");
+            CSDebug.Log("[DomainAssigner] Cleared assigned domains cache.");
         }
+
+        #endregion
     }
 }
```

</details>
