# Branch archive: `claude/fix-volume-scoring-multiplayer-5hBSs`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-19 by Claude
- **Unmerged commits:** 1
- **Forked from:** `520ada315` (2026-03-19, Add Dog Dight Event Effect SO)
- **Tip:** `929c781f2`
- **Files touched (9):**
  - `Assets/_Scripts/Game/Arcade/BaseScoreTracker.cs`
  - `Assets/_Scripts/Game/Arcade/FriendlyPrismsDestroyedScoring.cs`
  - `Assets/_Scripts/Game/Arcade/HostilePrismsDestroyedScoring.cs`
  - `Assets/_Scripts/Game/Arcade/PrismsCreatedScoring.cs`
  - `Assets/_Scripts/Game/Arcade/Scoring/BaseScoring.cs`
  - `Assets/_Scripts/Game/Arcade/Scoring/CrystalsCollectedScoring.cs`
  - `Assets/_Scripts/Game/Arcade/Scoring/FriendlyVolumeDestroyedScoring.cs`
  - `Assets/_Scripts/Game/Arcade/Scoring/HostileVolumeDestroyedScoring.cs`
  - `Assets/_Scripts/Game/Arcade/Scoring/VolumeCreatedScoring.cs`

### `929c781f2` — Fix multiplayer scoring: use per-player score tracking in BaseScoring

_Claude, 2026-03-19 19:42:39 +0000_

```text
BaseScoring had a single Score field shared across all players. When
multiple players triggered events, the last player's value overwrote
the previous, causing incorrect scores when CalculateTotalScore summed
scoring contributions for a different player.

Added Dictionary-backed per-player scores (GetScoreForPlayer/SetScoreForPlayer)
to BaseScoring. Updated all 7 affected scoring subclasses and CalculateTotalScore
to use per-player tracking. Scoring modes without per-player context (LifeFormsKilled,
ElementalCrystalsCollectedBlitz) fall back to the global Score property.
```

```text
 Assets/_Scripts/Game/Arcade/BaseScoreTracker.cs                       |  2 +-
 Assets/_Scripts/Game/Arcade/FriendlyPrismsDestroyedScoring.cs         |  2 +-
 Assets/_Scripts/Game/Arcade/HostilePrismsDestroyedScoring.cs          |  2 +-
 Assets/_Scripts/Game/Arcade/PrismsCreatedScoring.cs                   |  2 +-
 Assets/_Scripts/Game/Arcade/Scoring/BaseScoring.cs                    | 33 +++++++++++++++++++++++++++++----
 Assets/_Scripts/Game/Arcade/Scoring/CrystalsCollectedScoring.cs       |  2 +-
 Assets/_Scripts/Game/Arcade/Scoring/FriendlyVolumeDestroyedScoring.cs |  2 +-
 Assets/_Scripts/Game/Arcade/Scoring/HostileVolumeDestroyedScoring.cs  |  2 +-
 Assets/_Scripts/Game/Arcade/Scoring/VolumeCreatedScoring.cs           |  2 +-
 9 files changed, 37 insertions(+), 12 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 169 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/BaseScoreTracker.cs b/Assets/_Scripts/Game/Arcade/BaseScoreTracker.cs
index 28c3487bc..1a65343c3 100644
--- a/Assets/_Scripts/Game/Arcade/BaseScoreTracker.cs
+++ b/Assets/_Scripts/Game/Arcade/BaseScoreTracker.cs
@@ -106,7 +106,7 @@ namespace CosmicShore.Game.Arcade
             if (!gameData.TryGetRoundStats(playerName, out var roundStats))
                 return;
 
-            float totalScore = scoringArray.Sum(scoring => scoring.Score);
+            float totalScore = scoringArray.Sum(scoring => scoring.GetScoreForPlayer(playerName));
 
             roundStats.Score = totalScore;
         }
diff --git a/Assets/_Scripts/Game/Arcade/FriendlyPrismsDestroyedScoring.cs b/Assets/_Scripts/Game/Arcade/FriendlyPrismsDestroyedScoring.cs
index 4100c6dea..df6851b8c 100644
--- a/Assets/_Scripts/Game/Arcade/FriendlyPrismsDestroyedScoring.cs
+++ b/Assets/_Scripts/Game/Arcade/FriendlyPrismsDestroyedScoring.cs
@@ -36,7 +36,7 @@ namespace CosmicShore.Game.Arcade
         void UpdateScore(IRoundStats roundStats)
         {
             // Score for this scoring rule = friendly prisms destroyed * multiplier
-            Score = roundStats.FriendlyPrismsDestroyed * scoreMultiplier;
+            SetScoreForPlayer(roundStats.Name, roundStats.FriendlyPrismsDestroyed * scoreMultiplier);
 
             // Recompute total across all scoring rules for this player
             ScoreTracker.CalculateTotalScore(roundStats.Name);
diff --git a/Assets/_Scripts/Game/Arcade/HostilePrismsDestroyedScoring.cs b/Assets/_Scripts/Game/Arcade/HostilePrismsDestroyedScoring.cs
index 8e063e57e..a0dc85360 100644
--- a/Assets/_Scripts/Game/Arcade/HostilePrismsDestroyedScoring.cs
+++ b/Assets/_Scripts/Game/Arcade/HostilePrismsDestroyedScoring.cs
@@ -36,7 +36,7 @@ namespace CosmicShore.Game.Arcade
         void UpdateScore(IRoundStats roundStats)
         {
             // Score for this scoring rule = hostile prisms destroyed * multiplier
-            Score = roundStats.HostilePrismsDestroyed * scoreMultiplier;
+            SetScoreForPlayer(roundStats.Name, roundStats.HostilePrismsDestroyed * scoreMultiplier);
 
             // Recompute total across all scoring rules for this player
             ScoreTracker.CalculateTotalScore(roundStats.Name);
diff --git a/Assets/_Scripts/Game/Arcade/PrismsCreatedScoring.cs b/Assets/_Scripts/Game/Arcade/PrismsCreatedScoring.cs
index 6ffb39f36..b2bc39613 100644
--- a/Assets/_Scripts/Game/Arcade/PrismsCreatedScoring.cs
+++ b/Assets/_Scripts/Game/Arcade/PrismsCreatedScoring.cs
@@ -34,7 +34,7 @@ namespace CosmicShore.Game.Arcade
 
         void UpdateScore(IRoundStats roundStats)
         {
-            Score = roundStats.BlocksCreated * scoreMultiplier;
+            SetScoreForPlayer(roundStats.Name, roundStats.BlocksCreated * scoreMultiplier);
             ScoreTracker.CalculateTotalScore(roundStats.Name);
         }
     }
diff --git a/Assets/_Scripts/Game/Arcade/Scoring/BaseScoring.cs b/Assets/_Scripts/Game/Arcade/Scoring/BaseScoring.cs
index b38dfde0e..1ca575496 100644
--- a/Assets/_Scripts/Game/Arcade/Scoring/BaseScoring.cs
+++ b/Assets/_Scripts/Game/Arcade/Scoring/BaseScoring.cs
@@ -1,4 +1,5 @@
 using System;
+using System.Collections.Generic;
 using CosmicShore.Core;
 using CosmicShore.Soap;
 using UnityEngine;
@@ -9,12 +10,19 @@ namespace CosmicShore.Game.Arcade.Scoring
     [System.Serializable]
     public abstract class BaseScoring
     {
+        /// <summary>
+        /// Global score for scoring modes that don't track per-player
+        /// (e.g. LifeFormsKilled, ElementalCrystalsCollectedBlitz).
+        /// </summary>
         public float Score { get; protected set; }
+
+        readonly Dictionary<string, float> _playerScores = new();
+
         protected float scoreMultiplier;
 
         protected GameDataSO GameData;
         protected IScoreTracker ScoreTracker;
-        
+
         protected BaseScoring(IScoreTracker tracker, GameDataSO data, float scoreMultiplier = 145.65f)
         {
             ScoreTracker = tracker;
@@ -24,13 +32,30 @@ namespace CosmicShore.Game.Arcade.Scoring
 
         public abstract void Subscribe();
         public abstract void Unsubscribe();
-        
+
+        /// <summary>
+        /// Get the score contribution for a specific player.
+        /// Falls back to the global Score if no per-player value is stored.
+        /// </summary>
+        public float GetScoreForPlayer(string playerName)
+        {
+            return _playerScores.TryGetValue(playerName, out var score) ? score : Score;
+        }
+
+        /// <summary>
+        /// Set the score contribution for a specific player.
+        /// </summary>
+        protected void SetScoreForPlayer(string playerName, float value)
+        {
+            _playerScores[playerName] = value;
+        }
+
         protected bool TryGetRoundStats(string playerName, out IRoundStats roundStats)
         {
             roundStats = null;
-            if (GameData.TryGetRoundStats(playerName, out roundStats)) 
+            if (GameData.TryGetRoundStats(playerName, out roundStats))
                 return true;
-            
+
             CSDebug.LogError($"Didn't find RoundStats for player: {playerName}");
             return false;
         }
diff --git a/Assets/_Scripts/Game/Arcade/Scoring/CrystalsCollectedScoring.cs b/Assets/_Scripts/Game/Arcade/Scoring/CrystalsCollectedScoring.cs
index 9dfc81239..80077264d 100644
--- a/Assets/_Scripts/Game/Arcade/Scoring/CrystalsCollectedScoring.cs
+++ b/Assets/_Scripts/Game/Arcade/Scoring/CrystalsCollectedScoring.cs
@@ -58,7 +58,7 @@ namespace CosmicShore.Game.Arcade.Scoring
                 _ => 0
             };*/
             
-            Score = roundStats.CrystalsCollected * scoreMultiplier;
+            SetScoreForPlayer(roundStats.Name, roundStats.CrystalsCollected * scoreMultiplier);
             ScoreTracker.CalculateTotalScore(roundStats.Name);
         }
     }
diff --git a/Assets/_Scripts/Game/Arcade/Scoring/FriendlyVolumeDestroyedScoring.cs b/Assets/_Scripts/Game/Arcade/Scoring/FriendlyVolumeDestroyedScoring.cs
index 6e662e872..4eede9d76 100644
--- a/Assets/_Scripts/Game/Arcade/Scoring/FriendlyVolumeDestroyedScoring.cs
+++ b/Assets/_Scripts/Game/Arcade/Scoring/FriendlyVolumeDestroyedScoring.cs
@@ -34,7 +34,7 @@ namespace CosmicShore.Game.Arcade.Scoring
         void UpdateScore(IRoundStats roundStats)
         {
             // Penalty: destroying your own / friendly volume
-            Score = -roundStats.FriendlyVolumeDestroyed * scoreMultiplier;
+            SetScoreForPlayer(roundStats.Name, -roundStats.FriendlyVolumeDestroyed * scoreMultiplier);
             ScoreTracker.CalculateTotalScore(roundStats.Name);
         }
     }
diff --git a/Assets/_Scripts/Game/Arcade/Scoring/HostileVolumeDestroyedScoring.cs b/Assets/_Scripts/Game/Arcade/Scoring/HostileVolumeDestroyedScoring.cs
index 26fd0f28e..5d47748f9 100644
--- a/Assets/_Scripts/Game/Arcade/Scoring/HostileVolumeDestroyedScoring.cs
+++ b/Assets/_Scripts/Game/Arcade/Scoring/HostileVolumeDestroyedScoring.cs
@@ -34,7 +34,7 @@ namespace CosmicShore.Game.Arcade.Scoring
         void UpdateScore(IRoundStats roundStats)
         {
```

</details>
