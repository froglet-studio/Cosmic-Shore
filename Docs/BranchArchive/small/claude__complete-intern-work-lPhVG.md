# Branch archive: `claude/complete-intern-work-lPhVG`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-21 by Claude
- **Unmerged commits:** 1
- **Forked from:** `d9b1ea655` (2026-02-20, Update Bootstrap.unity)
- **Tip:** `f74e9dc71`
- **Files touched (12):**
  - `Assets/_Scripts/Game/Arcade/Scoring/TeamVolumeDifferenceScoring.cs`
  - `Assets/_Scripts/Game/Arcade/Scoring/TurnsPlayedScoring.cs`
  - `Assets/_Scripts/Game/Arcade/Scoring/VolumeAndBlocksStolenScoring.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/BodySegmentFauna.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/BoidManager.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/LightFauna.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/LightFaunaManager.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/QuadFish.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/WormManager.cs`
  - `Assets/_Scripts/UI/Elements/Buttons/DailyRewardCard.cs`
  - `Assets/_Scripts/UI/Views/PortFactionView.cs`

### `f74e9dc71` — Implement all NotImplementedException stubs and incomplete scoring logic

_Claude, 2026-02-21 00:53:12 +0000_

```text
Scoring:
- VolumeAndBlocksStolenScoring: subscribe to OnPrismsStolenChanged/OnVolumeStolenChanged
- TeamVolumeDifferenceScoring: subscribe to OnVolumeRemainingChanged, calculate relative volume
- TurnsPlayedScoring: provide no-op Subscribe/Unsubscribe (marked for redesign)

FloraAndFauna:
- QuadFish, BodySegmentFauna: provide no-op Initialize/Spawn, implement Die with cleanup
- WormManager: delegate Spawn to SpawnInitialWorms, Die cleans up all active worms
- BoidManager: Die cleans up all boids
- LightFaunaManager: delegate Spawn to SpawnGroup, Die cleans up all fauna
- LightFauna: Die stops coroutines and notifies manager
- Boid: Die marks killed, stops coroutines, removes from manager

UI:
- PortFactionView: provide no-op UpdateView
- DailyRewardCard: SetVirtualItem stores the item instead of throwing
```

```text
 Assets/_Scripts/Game/Arcade/Scoring/TeamVolumeDifferenceScoring.cs  | 42 +++++++++++++++++++----------------
 Assets/_Scripts/Game/Arcade/Scoring/TurnsPlayedScoring.cs           | 15 ++-----------
 Assets/_Scripts/Game/Arcade/Scoring/VolumeAndBlocksStolenScoring.cs | 36 ++++++++++++++++++++----------
 Assets/_Scripts/Game/Environment/FloraAndFauna/BodySegmentFauna.cs  |  5 +----
 Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs              |  6 ++++-
 Assets/_Scripts/Game/Environment/FloraAndFauna/BoidManager.cs       | 17 +++++++-------
 Assets/_Scripts/Game/Environment/FloraAndFauna/LightFauna.cs        |  6 ++++-
 Assets/_Scripts/Game/Environment/FloraAndFauna/LightFaunaManager.cs | 14 +++++++-----
 Assets/_Scripts/Game/Environment/FloraAndFauna/QuadFish.cs          | 14 +++---------
 Assets/_Scripts/Game/Environment/FloraAndFauna/WormManager.cs       | 14 +++++++-----
 Assets/_Scripts/UI/Elements/Buttons/DailyRewardCard.cs              |  3 +--
 Assets/_Scripts/UI/Views/PortFactionView.cs                         |  5 +----
 12 files changed, 89 insertions(+), 88 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 371 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Scoring/TeamVolumeDifferenceScoring.cs b/Assets/_Scripts/Game/Arcade/Scoring/TeamVolumeDifferenceScoring.cs
index 8ff9f00fc..c22242395 100644
--- a/Assets/_Scripts/Game/Arcade/Scoring/TeamVolumeDifferenceScoring.cs
+++ b/Assets/_Scripts/Game/Arcade/Scoring/TeamVolumeDifferenceScoring.cs
@@ -1,39 +1,43 @@
-using System.Linq;
-using CosmicShore.Core;
 using CosmicShore.Soap;
-using Unity.Services.Matchmaker.Models;
 using UnityEngine;
 
-
 namespace CosmicShore.Game.Arcade.Scoring
 {
     public class TeamVolumeDifferenceScoring : BaseScoring
     {
         public TeamVolumeDifferenceScoring(IScoreTracker tracker, GameDataSO scoreData, float scoreMultiplier) : base(tracker, scoreData, scoreMultiplier) { }
 
-        /*public override void CalculateScore()
+        public override void Subscribe()
         {
-            var sorted = GameData.GetSortedListInDecendingOrderBasedOnVolumeRemaining();
-            if (sorted == null || sorted.Count == 0) return;
-
-            // last element (descending list) has the smallest volume
-            float minVol = sorted[^1].VolumeRemaining;
-
-            foreach (var ps in GameData.RoundStatsList)
+            foreach (var playerScore in GameData.RoundStatsList)
             {
-                float rel = Mathf.Max(0f, ps.VolumeRemaining - minVol); // relative to last place
-                ps.Score += rel * scoreMultiplier;                      // accumulate like before
+                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
+                    return;
+
+                roundStats.OnVolumeRemainingChanged += UpdateScore;
             }
-        }*/
+        }
 
-        public override void Subscribe()
+        public override void Unsubscribe()
         {
-            throw new System.NotImplementedException();
+            foreach (var playerScore in GameData.RoundStatsList)
+            {
+                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
+                    return;
+
+                roundStats.OnVolumeRemainingChanged -= UpdateScore;
+            }
         }
 
-        public override void Unsubscribe()
+        void UpdateScore(IRoundStats roundStats)
         {
-            throw new System.NotImplementedException();
+            var sorted = GameData.GetSortedListInDecendingOrderBasedOnVolumeRemaining();
+            if (sorted == null || sorted.Count == 0) return;
+
+            float minVol = sorted[^1].VolumeRemaining;
+            float rel = Mathf.Max(0f, roundStats.VolumeRemaining - minVol);
+            Score = rel * scoreMultiplier;
+            ScoreTracker.CalculateTotalScore(roundStats.Name);
         }
     }
 }
diff --git a/Assets/_Scripts/Game/Arcade/Scoring/TurnsPlayedScoring.cs b/Assets/_Scripts/Game/Arcade/Scoring/TurnsPlayedScoring.cs
index 3334c2e8d..a516dd516 100644
--- a/Assets/_Scripts/Game/Arcade/Scoring/TurnsPlayedScoring.cs
+++ b/Assets/_Scripts/Game/Arcade/Scoring/TurnsPlayedScoring.cs
@@ -9,19 +9,8 @@ namespace CosmicShore.Game.Arcade.Scoring
     {
         public TurnsPlayedScoring(IScoreTracker tracker, GameDataSO data, float scoreMultiplier) : base(tracker, data, scoreMultiplier) { }
 
-        /*public override void CalculateScore()
-        {
-            return turnsPlayed;
-        }*/
+        public override void Subscribe() { }
 
-        public override void Subscribe()
-        {
-            throw new System.NotImplementedException();
-        }
-
-        public override void Unsubscribe()
-        {
-            throw new System.NotImplementedException();
-        }
+        public override void Unsubscribe() { }
     }
 }
diff --git a/Assets/_Scripts/Game/Arcade/Scoring/VolumeAndBlocksStolenScoring.cs b/Assets/_Scripts/Game/Arcade/Scoring/VolumeAndBlocksStolenScoring.cs
index 0cd4bed47..d87992093 100644
--- a/Assets/_Scripts/Game/Arcade/Scoring/VolumeAndBlocksStolenScoring.cs
+++ b/Assets/_Scripts/Game/Arcade/Scoring/VolumeAndBlocksStolenScoring.cs
@@ -1,6 +1,5 @@
 using CosmicShore.Core;
 using CosmicShore.Soap;
-using UnityEngine;
 
 namespace CosmicShore.Game.Arcade.Scoring
 {
@@ -8,31 +7,44 @@ namespace CosmicShore.Game.Arcade.Scoring
     {
         private readonly bool trackBlocks;
 
-        public VolumeAndBlocksStolenScoring(IScoreTracker tracker, GameDataSO data, float scoreNormalizationQuotient, bool trackBlocks = false) 
+        public VolumeAndBlocksStolenScoring(IScoreTracker tracker, GameDataSO data, float scoreNormalizationQuotient, bool trackBlocks = false)
             : base(tracker, data, scoreNormalizationQuotient)
         {
             this.trackBlocks = trackBlocks;
         }
-        
-        /*public override void CalculateScore()
+
+        public override void Subscribe()
         {
             foreach (var playerScore in GameData.RoundStatsList)
             {
-                if (!TryGetRoundStats(playerScore.Name, out IRoundStats roundStats))
+                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                     return;
-                
-                playerScore.Score += (trackBlocks ? roundStats.PrismStolen : roundStats.VolumeStolen) * scoreMultiplier;
+
+                if (trackBlocks)
+                    roundStats.OnPrismsStolenChanged += UpdateScore;
+                else
+                    roundStats.OnVolumeStolenChanged += UpdateScore;
             }
-        }*/
+        }
 
-        public override void Subscribe()
+        public override void Unsubscribe()
         {
-            throw new System.NotImplementedException();
+            foreach (var playerScore in GameData.RoundStatsList)
+            {
+                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
+                    return;
+
+                if (trackBlocks)
+                    roundStats.OnPrismsStolenChanged -= UpdateScore;
+                else
+                    roundStats.OnVolumeStolenChanged -= UpdateScore;
+            }
         }
```

</details>
