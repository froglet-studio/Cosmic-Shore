# Branch archive: `claude/fix-ui-haptics-mixing-2laRC`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-21 by Garrett Milliron
- **Unmerged commits:** 3
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/85
- **Forked from:** `eb9793f53` (2026-02-21, Merge branch 'development' into minigame-polish)
- **Tip:** `f447b4f6e`
- **Files touched (8):**
  - `Assets/_Scripts/Game/Arcade/HexRaceScoreTracker.cs`
  - `Assets/_Scripts/Game/Arcade/Scoring/ElementalCrystalsCollectedBlitzScoring.cs`
  - `Assets/_Scripts/Game/Arcade/Scoring/LifeFormsKilledScoring.cs`
  - `Assets/_Scripts/Game/Arcade/SinglePlayerWildlifeBlitzScoreTracker.cs`
  - `Assets/_Scripts/Game/ImpactEffects/EffectsSO/Helpers/HapticSpec.cs`
  - `Assets/_Scripts/Game/ImpactEffects/EffectsSO/Skimmer Prism Effects/SkimmerScaleHapticWithDistanceByPrismSO.cs`
  - `Assets/_Scripts/Game/ImpactEffects/EffectsSO/Vessel Prism Effects/VesselResetBoostPrismEffectSO.cs`
  - `Assets/_Scripts/Game/Ship/SilhouetteController.cs`

### `d29cddd4d` — Fix UI and haptics leaking between players and AI

_Claude, 2026-02-20 21:20:36 +0000_

```text
Haptics were triggered for all vessels regardless of ownership because
HapticSpec.PlayIfManual only checked AutoPilotEnabled, not IsLocalUser.
Similarly, static events for scoring and visual effects were broadcast
globally without player filtering, causing score contamination and
visual effects from other players/AI appearing on the local device.

Changes:
- HapticSpec: gate PlayIfManual on IsLocalUser so only the local
  player's collisions produce haptic feedback
- SkimmerScaleHapticWithDistanceByPrismSO: add IsLocalUser guard
- SilhouetteController: filter HandleMantaFlowerExplosion to only
  react to this controller's own vessel
- VesselResetBoostPrismEffectSO: pass player name through the
  OnPrismCollision event so listeners can filter
- HexRaceScoreTracker: filter HandlePrismCollision by observed
  vessel's player name instead of resetting for any collision
- ElementalCrystalsCollectedBlitzScoring: filter by local player name
- LifeFormsKilledScoring: filter by local player name
- SinglePlayerWildlifeBlitzScoreTracker: filter both crystal and
  lifeform scoring events by local player name
```

```text
 Assets/_Scripts/Game/Arcade/HexRaceScoreTracker.cs                                               | 7 ++++---
 Assets/_Scripts/Game/Arcade/Scoring/ElementalCrystalsCollectedBlitzScoring.cs                    | 6 ++++--
 Assets/_Scripts/Game/Arcade/Scoring/LifeFormsKilledScoring.cs                                    | 4 +++-
 Assets/_Scripts/Game/Arcade/SinglePlayerWildlifeBlitzScoreTracker.cs                             | 4 +++-
 Assets/_Scripts/Game/ImpactEffects/EffectsSO/Helpers/HapticSpec.cs                               | 2 +-
 .../ImpactEffects/EffectsSO/Skimmer Prism Effects/SkimmerScaleHapticWithDistanceByPrismSO.cs     | 4 ++--
 .../_Scripts/Game/ImpactEffects/EffectsSO/Vessel Prism Effects/VesselResetBoostPrismEffectSO.cs  | 4 ++--
 Assets/_Scripts/Game/Ship/SilhouetteController.cs                                                | 1 +
 8 files changed, 20 insertions(+), 12 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/HexRaceScoreTracker.cs b/Assets/_Scripts/Game/Arcade/HexRaceScoreTracker.cs
index fb9031b3a..fcf712385 100644
--- a/Assets/_Scripts/Game/Arcade/HexRaceScoreTracker.cs
+++ b/Assets/_Scripts/Game/Arcade/HexRaceScoreTracker.cs
@@ -110,9 +110,10 @@ namespace CosmicShore.Game.Arcade
             } 
         }
         
-        void HandlePrismCollision() 
-        { 
-            if (_isTracking) _currentCleanStreak = 0; 
+        void HandlePrismCollision(string playerName)
+        {
+            if (_isTracking && _observedVessel != null && _observedVessel.PlayerName == playerName)
+                _currentCleanStreak = 0;
         }
         void HandleGameEnd()
         {
diff --git a/Assets/_Scripts/Game/Arcade/Scoring/ElementalCrystalsCollectedBlitzScoring.cs b/Assets/_Scripts/Game/Arcade/Scoring/ElementalCrystalsCollectedBlitzScoring.cs
index c7ba0f620..69c59f70e 100644
--- a/Assets/_Scripts/Game/Arcade/Scoring/ElementalCrystalsCollectedBlitzScoring.cs
+++ b/Assets/_Scripts/Game/Arcade/Scoring/ElementalCrystalsCollectedBlitzScoring.cs
@@ -29,9 +29,11 @@ namespace CosmicShore.Game.Arcade.Scoring
 
         void HandleCrystalCollected(string playerName)
         {
+            if (GameData.LocalPlayer == null || GameData.LocalPlayer.Name != playerName) return;
+
             totalCrystalsCollected++;
-            UnityEngine.Debug.Log($"<color=cyan>💎 [COLLECT] {playerName} collected Crystal #{totalCrystalsCollected}! +{scoreMultiplier} pts</color>");
-            
+            UnityEngine.Debug.Log($"<color=cyan>[COLLECT] {playerName} collected Crystal #{totalCrystalsCollected}! +{scoreMultiplier} pts</color>");
+
             Score += scoreMultiplier;
         }
 
diff --git a/Assets/_Scripts/Game/Arcade/Scoring/LifeFormsKilledScoring.cs b/Assets/_Scripts/Game/Arcade/Scoring/LifeFormsKilledScoring.cs
index 01d90539e..ffd5799dd 100644
--- a/Assets/_Scripts/Game/Arcade/Scoring/LifeFormsKilledScoring.cs
+++ b/Assets/_Scripts/Game/Arcade/Scoring/LifeFormsKilledScoring.cs
@@ -29,8 +29,10 @@ namespace CosmicShore.Game.Arcade.Scoring
 
         void HandleLifeFormDeath(string killerName, int cellId)
         {
+            if (GameData.LocalPlayer == null || GameData.LocalPlayer.Name != killerName) return;
+
             totalLifeFormsKilled++;
-            Score += scoreMultiplier; 
+            Score += scoreMultiplier;
         }
 
         public int GetTotalLifeFormsKilled() => totalLifeFormsKilled;
diff --git a/Assets/_Scripts/Game/Arcade/SinglePlayerWildlifeBlitzScoreTracker.cs b/Assets/_Scripts/Game/Arcade/SinglePlayerWildlifeBlitzScoreTracker.cs
index 7a0d4dee1..998c2c04d 100644
--- a/Assets/_Scripts/Game/Arcade/SinglePlayerWildlifeBlitzScoreTracker.cs
+++ b/Assets/_Scripts/Game/Arcade/SinglePlayerWildlifeBlitzScoreTracker.cs
@@ -47,12 +47,14 @@ namespace CosmicShore.Game.Arcade
         
         void OnScoringEvent(string playerName, int cellId)
         {
+            if (gameData.LocalPlayer == null || gameData.LocalPlayer.Name != playerName) return;
             var lifeFormScoring = GetScoring<LifeFormsKilledScoring>();
             if (lifeFormScoring != null) AddScore(lifeFormScoring.ScorePerKill);
         }
 
-        void OnCrystalScoringEvent(string playerName) 
+        void OnCrystalScoringEvent(string playerName)
         {
+            if (gameData.LocalPlayer == null || gameData.LocalPlayer.Name != playerName) return;
             var crystalsCollectedScoring = GetScoring<ElementalCrystalsCollectedBlitzScoring>();
             if (crystalsCollectedScoring != null) AddScore(crystalsCollectedScoring.GetScoreMultiplier());
         }
diff --git a/Assets/_Scripts/Game/ImpactEffects/EffectsSO/Helpers/HapticSpec.cs b/Assets/_Scripts/Game/ImpactEffects/EffectsSO/Helpers/HapticSpec.cs
index 94ffda969..d6a1fcdd6 100644
--- a/Assets/_Scripts/Game/ImpactEffects/EffectsSO/Helpers/HapticSpec.cs
+++ b/Assets/_Scripts/Game/ImpactEffects/EffectsSO/Helpers/HapticSpec.cs
@@ -11,7 +11,7 @@ namespace CosmicShore.Game
         public void PlayIfManual(IVesselStatus status)
         {
             if (status == null) return;
-            if (!status.AutoPilotEnabled)
+            if (!status.AutoPilotEnabled && status.IsLocalUser)
                 HapticController.PlayHaptic(_type);
         }
     }
diff --git a/Assets/_Scripts/Game/Ship/SilhouetteController.cs b/Assets/_Scripts/Game/Ship/SilhouetteController.cs
index 10be2bcbf..01a617338 100644
--- a/Assets/_Scripts/Game/Ship/SilhouetteController.cs
+++ b/Assets/_Scripts/Game/Ship/SilhouetteController.cs
@@ -198,6 +198,7 @@ namespace CosmicShore
 
         private void HandleMantaFlowerExplosion(VesselImpactor vessel)
         {
+            if (_vessel == null || vessel?.Vessel != _vessel) return;
             view?.ShowMantaFlowerOverlay();
         }
 
```

</details>

### `e98d82bbb` — Fix UI and haptics leaking between players and AI

_Claude, 2026-02-20 22:53:36 +0000_

```text
Haptics were triggered for all vessels regardless of ownership because
HapticSpec.PlayIfManual only checked AutoPilotEnabled, not IsLocalUser.
Similarly, static events for scoring and visual effects were broadcast
globally without player filtering, causing score contamination and
visual effects from other players/AI appearing on the local device.

Changes:
- HapticSpec: gate PlayIfManual on IsLocalUser so only the local
  player's collisions produce haptic feedback
- SkimmerScaleHapticWithDistanceByPrismSO: add IsLocalUser guard
- SilhouetteController: filter HandleMantaFlowerExplosion to only
  react to this controller's own vessel
- VesselResetBoostPrismEffectSO: pass player name through the
  OnPrismCollision event so listeners can filter
- HexRaceScoreTracker: filter HandlePrismCollision by observed
  vessel's player name instead of resetting for any collision
- ElementalCrystalsCollectedBlitzScoring: filter by local player name
- LifeFormsKilledScoring: filter by local player name
- SinglePlayerWildlifeBlitzScoreTracker: filter both crystal and
  lifeform scoring events by local player name
```

```text
 Assets/_Scripts/Game/Arcade/HexRaceScoreTracker.cs                                                | 1 +
 Assets/_Scripts/Game/Arcade/Scoring/ElementalCrystalsCollectedBlitzScoring.cs                     | 6 ++++--
 Assets/_Scripts/Game/Arcade/Scoring/LifeFormsKilledScoring.cs                                     | 4 +++-
 Assets/_Scripts/Game/Arcade/SinglePlayerWildlifeBlitzScoreTracker.cs                              | 4 +++-
 Assets/_Scripts/Game/ImpactEffects/EffectsSO/Helpers/HapticSpec.cs                                | 2 +-
 .../Game/ImpactEffects/EffectsSO/Skimmer Prism Effects/SkimmerScaleHapticWithDistanceByPrismSO.cs | 4 ++--
 .../_Scripts/Game/ImpactEffects/EffectsSO/Vessel Prism Effects/VesselResetBoostPrismEffectSO.cs   | 4 ++--
 Assets/_Scripts/Game/Ship/SilhouetteController.cs                                                 | 1 +
 8 files changed, 17 insertions(+), 9 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/HexRaceScoreTracker.cs b/Assets/_Scripts/Game/Arcade/HexRaceScoreTracker.cs
index b8841a327..2f7db0fb9 100644
--- a/Assets/_Scripts/Game/Arcade/HexRaceScoreTracker.cs
+++ b/Assets/_Scripts/Game/Arcade/HexRaceScoreTracker.cs
@@ -79,6 +79,7 @@ namespace CosmicShore.Game.Arcade
 
         // ── Game end ───────────────────────────────────────────────────────────
 
+
         void HandleGameEnd()
         {
             if (_hasReported) return;
diff --git a/Assets/_Scripts/Game/Arcade/Scoring/ElementalCrystalsCollectedBlitzScoring.cs b/Assets/_Scripts/Game/Arcade/Scoring/ElementalCrystalsCollectedBlitzScoring.cs
index c7ba0f620..69c59f70e 100644
--- a/Assets/_Scripts/Game/Arcade/Scoring/ElementalCrystalsCollectedBlitzScoring.cs
+++ b/Assets/_Scripts/Game/Arcade/Scoring/ElementalCrystalsCollectedBlitzScoring.cs
@@ -29,9 +29,11 @@ namespace CosmicShore.Game.Arcade.Scoring
 
         void HandleCrystalCollected(string playerName)
         {
+            if (GameData.LocalPlayer == null || GameData.LocalPlayer.Name != playerName) return;
+
             totalCrystalsCollected++;
-            UnityEngine.Debug.Log($"<color=cyan>💎 [COLLECT] {playerName} collected Crystal #{totalCrystalsCollected}! +{scoreMultiplier} pts</color>");
-            
+            UnityEngine.Debug.Log($"<color=cyan>[COLLECT] {playerName} collected Crystal #{totalCrystalsCollected}! +{scoreMultiplier} pts</color>");
+
             Score += scoreMultiplier;
         }
 
diff --git a/Assets/_Scripts/Game/Arcade/Scoring/LifeFormsKilledScoring.cs b/Assets/_Scripts/Game/Arcade/Scoring/LifeFormsKilledScoring.cs
index 01d90539e..ffd5799dd 100644
--- a/Assets/_Scripts/Game/Arcade/Scoring/LifeFormsKilledScoring.cs
+++ b/Assets/_Scripts/Game/Arcade/Scoring/LifeFormsKilledScoring.cs
@@ -29,8 +29,10 @@ namespace CosmicShore.Game.Arcade.Scoring
 
         void HandleLifeFormDeath(string killerName, int cellId)
         {
+            if (GameData.LocalPlayer == null || GameData.LocalPlayer.Name != killerName) return;
+
             totalLifeFormsKilled++;
-            Score += scoreMultiplier; 
+            Score += scoreMultiplier;
         }
 
         public int GetTotalLifeFormsKilled() => totalLifeFormsKilled;
diff --git a/Assets/_Scripts/Game/Arcade/SinglePlayerWildlifeBlitzScoreTracker.cs b/Assets/_Scripts/Game/Arcade/SinglePlayerWildlifeBlitzScoreTracker.cs
index 7a0d4dee1..998c2c04d 100644
--- a/Assets/_Scripts/Game/Arcade/SinglePlayerWildlifeBlitzScoreTracker.cs
+++ b/Assets/_Scripts/Game/Arcade/SinglePlayerWildlifeBlitzScoreTracker.cs
@@ -47,12 +47,14 @@ namespace CosmicShore.Game.Arcade
         
         void OnScoringEvent(string playerName, int cellId)
         {
+            if (gameData.LocalPlayer == null || gameData.LocalPlayer.Name != playerName) return;
             var lifeFormScoring = GetScoring<LifeFormsKilledScoring>();
             if (lifeFormScoring != null) AddScore(lifeFormScoring.ScorePerKill);
         }
 
-        void OnCrystalScoringEvent(string playerName) 
+        void OnCrystalScoringEvent(string playerName)
         {
+            if (gameData.LocalPlayer == null || gameData.LocalPlayer.Name != playerName) return;
             var crystalsCollectedScoring = GetScoring<ElementalCrystalsCollectedBlitzScoring>();
             if (crystalsCollectedScoring != null) AddScore(crystalsCollectedScoring.GetScoreMultiplier());
         }
diff --git a/Assets/_Scripts/Game/ImpactEffects/EffectsSO/Helpers/HapticSpec.cs b/Assets/_Scripts/Game/ImpactEffects/EffectsSO/Helpers/HapticSpec.cs
index 94ffda969..d6a1fcdd6 100644
--- a/Assets/_Scripts/Game/ImpactEffects/EffectsSO/Helpers/HapticSpec.cs
+++ b/Assets/_Scripts/Game/ImpactEffects/EffectsSO/Helpers/HapticSpec.cs
@@ -11,7 +11,7 @@ namespace CosmicShore.Game
         public void PlayIfManual(IVesselStatus status)
         {
             if (status == null) return;
-            if (!status.AutoPilotEnabled)
+            if (!status.AutoPilotEnabled && status.IsLocalUser)
                 HapticController.PlayHaptic(_type);
         }
     }
diff --git a/Assets/_Scripts/Game/Ship/SilhouetteController.cs b/Assets/_Scripts/Game/Ship/SilhouetteController.cs
index 10be2bcbf..01a617338 100644
--- a/Assets/_Scripts/Game/Ship/SilhouetteController.cs
+++ b/Assets/_Scripts/Game/Ship/SilhouetteController.cs
@@ -198,6 +198,7 @@ namespace CosmicShore
 
         private void HandleMantaFlowerExplosion(VesselImpactor vessel)
         {
+            if (_vessel == null || vessel?.Vessel != _vessel) return;
             view?.ShowMantaFlowerOverlay();
         }
 
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
