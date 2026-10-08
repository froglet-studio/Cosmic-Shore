# Branch archive: `claude/fix-blue-fauna-spawn-3ov7j`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-05-03 by Claude
- **Unmerged commits:** 1
- **Forked from:** `d1036ff0b` (2026-05-02, Merge pull request #510 from froglet-studio/claude/fix-fauna-garbage-cleanup-Y)
- **Tip:** `651340805`
- **Files touched (5):**
  - `Assets/_Scripts/Controller/Environment/Cell.cs`
  - `Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs`
  - `Assets/_Scripts/Tests/EditMode/CellLifeSpawnerDomainTests.cs`
  - `Assets/_Scripts/Tests/EditMode/CellLifeSpawnerDomainTests.cs.meta`

### `651340805` — fix(fauna): exclude Blue from cell control + let rabid fauna eat same-domain mass

_Claude, 2026-05-03 16:59:43 +0000_

```text
Two related bugs surfaced after flying around as a Jade Squirrel in Menu_Main:

1. Blue fauna spawned in cells even though no player was on the Blue team.
   Root cause: CellLifeSpawnerBase.PickRandomDomain included Blue in its
   candidate list, so flora rolled Blue ~25% of the time. Blue flora produce
   Blue prisms; Blue prism count overtook Jade in domainBlockCounts; Cell.
   DominantDomain returned Blue; IntensityWiseLifeSpawner.TrySpawnFauna read
   host.ControllingDomain → Blue and spawned Blue fauna. Fix: drop Blue from
   PickRandomDomain candidates, harden DominantDomain + ControllingDomain to
   refuse Blue regardless of how many environmental Blue prisms (gyroids,
   spawnable shapes, walls) exist in the cell. Same-purpose cleanup of
   SetupDensityGrids and the legacy GetHostileDomainToLocalLegacy candidate
   list.

2. Jade fauna in a Jade-only cell never consumed Jade mass even at very high
   prism counts (no opposing mass = no consumption trigger). At Rabid (cell
   crosses 15k prisms by default), fauna already targeted the densest region
   of any domain via the partition system (GetDensestRegionAnyDomain), but
   the consume check still required block.Domain != domain — so Jade fauna
   reached the densest Jade region and circled it without eating. Fix: at
   Rabid, drop the same-domain consume guard so the cell can regulate its
   own overload.

Includes edit-mode tests locking the contract that PickRandomDomain never
returns Blue.
```

```text
 Assets/_Scripts/Controller/Environment/Cell.cs                     |  33 ++++++++---
 Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs      |  10 +++-
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs |  13 ++++-
 Assets/_Scripts/Tests/EditMode/CellLifeSpawnerDomainTests.cs       | 103 +++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Tests/EditMode/CellLifeSpawnerDomainTests.cs.meta  |   2 +
 5 files changed, 149 insertions(+), 12 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 263 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index 5c741ec82..59016b513 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -75,8 +75,13 @@ namespace CosmicShore.Gameplay
         /// Live leader by per-domain prism count. Recomputed on demand so the answer
         /// always reflects the current Add/RemoveBlock-driven counts. Returns
         /// <see cref="Domains.None"/> when the cell has no prisms tracked yet.
-        /// Ties resolve in fixed order (Jade > Ruby > Gold > Blue) so two clients with
+        /// Ties resolve in fixed order (Jade > Ruby > Gold) so two clients with
         /// the same per-domain counts pick the same leader.
+        /// Only player-controllable domains are eligible — Blue is reserved for
+        /// environmental decoration (gyroids, spawnable shapes) and is excluded
+        /// from cell control even when Blue prisms exist in the cell. Without this
+        /// filter, environmental Blue prisms would gradually elect Blue as the
+        /// dominant domain and trigger Blue fauna spawns in Menu_Main.
         /// </summary>
         public Domains DominantDomain
         {
@@ -84,7 +89,7 @@ namespace CosmicShore.Gameplay
             {
                 Domains leader = Domains.None;
                 int leaderCount = 0;
-                Domains[] order = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };
+                Domains[] order = { Domains.Jade, Domains.Ruby, Domains.Gold };
                 foreach (var d in order)
                 {
                     if (!domainBlockCounts.TryGetValue(d, out int c)) continue;
@@ -141,33 +146,38 @@ namespace CosmicShore.Gameplay
         /// <see cref="DominantDomain"/> (per-domain prism count leader), then falls
         /// back to gameData's controlling team by remaining volume, then to the local
         /// player's domain (useful in Menu_Main where there is no scored controlling
-        /// team), then to Jade as a last resort. Never returns None or Unassigned —
-        /// callers can use it directly without further branching.
+        /// team), then to Jade as a last resort. Never returns None, Unassigned, or
+        /// Blue — callers can use it directly without further branching.
+        /// Blue is reserved for environmental decoration and is never a valid
+        /// controlling domain regardless of which fallback layer answers.
         /// </summary>
         public Domains ControllingDomain
         {
             get
             {
                 var dominant = DominantDomain;
-                if (dominant != Domains.None && dominant != Domains.Unassigned)
+                if (IsValidControllingDomain(dominant))
                     return dominant;
 
                 if (gameData != null)
                 {
                     var top = gameData.GetControllingTeamStatsBasedOnVolumeRemaining();
-                    if (top.Team != Domains.None && top.Team != Domains.Unassigned && top.Volume > 0f)
+                    if (IsValidControllingDomain(top.Team) && top.Volume > 0f)
                         return top.Team;
 
                     var local = gameData.LocalRoundStats?.Domain
                                 ?? gameData.LocalPlayer?.Domain
                                 ?? Domains.Unassigned;
-                    if (local != Domains.None && local != Domains.Unassigned)
+                    if (IsValidControllingDomain(local))
                         return local;
                 }
                 return Domains.Jade;
             }
         }
 
+        static bool IsValidControllingDomain(Domains d) =>
+            d == Domains.Jade || d == Domains.Ruby || d == Domains.Gold;
+
         /// <summary>
         /// Sole entry point for phase mutation. Updates the local field and the
         /// runtime SO's per-cell stats; the runtime SO raises <c>OnPhaseChanged</c>
@@ -399,7 +409,12 @@ namespace CosmicShore.Gameplay
 
         void SetupDensityGrids()
         {
-            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };
+            // Only the player-controllable domains get per-domain "opposing mass"
+            // grids — Blue is environmental decoration only and never spawns fauna,
+            // so a Blue grid would never be queried. (Cell.AddBlock still increments
+            // domainBlockCounts[Blue] for environmental Blue prisms, but Blue is
+            // filtered out of DominantDomain so it can't elect Blue control.)
+            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
             countGrids.Clear();
             foreach (Domains t in teams)
                 countGrids[t] = new BlockCountDensityGrid(t);
@@ -594,7 +609,7 @@ namespace CosmicShore.Gameplay
         internal Domains GetHostileDomainToLocalLegacy()
         {
             var local = gameData.LocalRoundStats?.Domain ?? Domains.Jade;
-            var candidates = new[] { Domains.Ruby, Domains.Gold, Domains.Blue, Domains.Jade };
+            var candidates = new[] { Domains.Ruby, Domains.Gold, Domains.Jade };
             return candidates.First(d => d != local);
         }
     }
diff --git a/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs b/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs
index c5c72d9c3..fdfffe728 100644
--- a/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs
+++ b/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs
@@ -73,7 +73,15 @@ namespace CosmicShore.Gameplay
 
         protected Domains PickRandomDomain(Domains? excluded)
         {
-            var candidates = new List<Domains>(4) { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };
+            // Only Jade, Ruby, and Gold can control cells — Blue is reserved for
+            // environmental decoration (gyroids, spawnable shapes, walls) and is
+            // explicitly excluded from player assignment by DomainAssigner. Spawning
+            // Blue flora here was the root cause of Blue fauna appearing in
+            // Menu_Main: random Blue flora produced Blue prisms, Blue prism count
+            // overtook the player's domain in domainBlockCounts, Cell.DominantDomain
+            // returned Blue, and IntensityWiseLifeSpawner.TrySpawnFauna read
+            // host.ControllingDomain → Blue.
+            var candidates = new List<Domains>(3) { Domains.Jade, Domains.Ruby, Domains.Gold };
             if (excluded.HasValue) candidates.Remove(excluded.Value);
 
             return candidates.Count == 0
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
index d01fe0ab8..44bb29666 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
@@ -161,6 +161,15 @@ namespace CosmicShore.Gameplay
             // still push us away so we don't clip through enemy mass.
             bool dropFriendlyAvoidance = phase >= CellPhase.Rabid;
 
+            // Rabid fauna also consume same-domain mass — the cell has crossed the
+            // overload threshold (default 15,000 prisms) and needs any available
+            // mouth to bring it back down. Without this, a Jade-only cell can climb
+            // arbitrarily high without ever triggering consumption: rabid Jade fauna
+            // would head to the densest Jade region (via GetDensestRegionAnyDomain)
+            // but the Domain != domain check would silently skip every Jade prism
+            // they reach, leaving them to circle the centroid forever.
+            bool consumeAnyDomain = phase >= CellPhase.Rabid;
+
             var nearbyColliders = Physics.OverlapSphere(transform.position, detectionRadius);
 
             foreach (var collider in nearbyColliders)
@@ -194,7 +203,7 @@ namespace CosmicShore.Gameplay
                     if (distance < separationRadius && !(dropFriendlyAvoidance && sameDomain))
                         separation += diff.normalized / distance;
 
-                    if (distance < consumeRadius && otherHealthBlock.LifeForm && otherHealthBlock.LifeForm.domain != domain)
+                    if (distance < consumeRadius && otherHealthBlock.LifeForm && (!sameDomain || consumeAnyDomain))
                         otherHealthBlock.Consume(transform, domain, PLAYER_NAME, true);
 
                     continue;
@@ -202,7 +211,7 @@ namespace CosmicShore.Gameplay
 
                 // Handle blocks
                 Prism block = collider.GetComponent<Prism>();
-                if (block && block.Domain != domain && distance < consumeRadius)
```

</details>
