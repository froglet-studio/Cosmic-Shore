# Branch archive: `claude/fix-menu-text-pptr-B1TSn`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

## What this branch is

**Compile and null fixes, March 2026**

A batch of small fixes merged together in early March 2026: resolving compile errors across ~20 files, removing an orphaned unlock-vessel button from the main menu, null checks for daily challenge and prism shield audio, and switching the arcade explore screen to dependency injection for audio. It is a subset of the merge-dev-to-app-shell integration branch.

- **Status:** Sync / merge branch
- **Areas:** compile fixes, main menu UI, daily challenge, audio
- **Already in bleeding-edge:** Same fixes appear in rewritten form in bleeding-edge (DailyChallengeSystem.cs Arcade.Instance null guard; ArcadeExploreView.cs injected AudioSystem). All commits contained in claude/merge-dev-to-app-shell-EpONe.
- **Risk if deleted:** low
- **Suggestion (2026-10-08):** can be deleted after archiving — Strict subset of another integration branch; code since rewritten.

## Evidence

- **Last commit:** 2026-03-06 by Claude
- **Unmerged commits:** 18
- **Forked from:** `00c41c3d0` (2026-03-06, merge: integrate origin/app-shell-polish into merge branch)
- **Tip:** `2b51fdd3e`
- **Files touched (24):**
  - `Assets/_Scenes/Menu_Main.unity`
  - `Assets/_Scripts/Controller/Arcade/CountdownTimer.cs`
  - `Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs`
  - `Assets/_Scripts/Controller/Arcade/MiniGame.cs`
  - `Assets/_Scripts/Controller/Environment/Cell.cs`
  - `Assets/_Scripts/Controller/Environment/Cytoplasm/SnowChanger.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Skimmer Effects/VesselOvertakeBySkimmerEffectSO.cs`
  - `Assets/_Scripts/Controller/Managers/PrismStateManager.cs`
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs`
  - `Assets/_Scripts/Editor/ElementalFloatEditor.cs`
  - `Assets/_Scripts/Editor/PlayfabProductGenerator.cs`
  - `Assets/_Scripts/System/CloudData/UGSDataService.cs`
  - `Assets/_Scripts/System/DailyChallengeSystem.cs`
  - `Assets/_Scripts/System/Progression/GameModeProgressionService.cs`
  - `Assets/_Scripts/UI/Elements/GameCard.cs`
  - `Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs`
  - `Assets/_Scripts/UI/Modals/FactionMissionModal.cs`
  - `Assets/_Scripts/UI/Modals/HangarTrainingModal.cs`
  - `Assets/_Scripts/UI/Screens/EpisodeScreen.cs`
  - `Assets/_Scripts/UI/Views/ArcadeExploreView.cs`
  - `Assets/_Scripts/UI/Views/HangarCaptainsView.cs`
  - `Assets/_Scripts/UI/Views/PlayerDataService.cs`
  - `Assets/_Scripts/UI/Views/PlayerProfileData.cs`
  - `Assets/_Scripts/Utility/DataContainers/EndGameCinematicController.cs`

### `ebd39445f` — fix(compile): resolve CS0234, CS0246, CS0111 errors in VesselOvertakeBySkimmerEffectSO and Cell

_Claude, 2026-03-06 03:59:32 +0000_

```text
- Replace invalid `CosmicShore.Game.IO` using with correct `CosmicShore.Data` and `CosmicShore.UI`
  namespaces to resolve missing `Element` and `ElementalBarsView` types
- Remove duplicate `SpawnCytoplasm()` method in Cell.cs (lines 231-237 vs 256-263)
```

```text
 Assets/_Scripts/Controller/Environment/Cell.cs                                                  | 8 --------
 .../ImpactEffects/EffectsSO/Vessel Skimmer Effects/VesselOvertakeBySkimmerEffectSO.cs           | 4 ++--
 2 files changed, 2 insertions(+), 10 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index 9319778e7..5c1e77ddf 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -228,14 +228,6 @@ namespace CosmicShore.Gameplay
             nucleus.transform.localScale *= nucleusScaleMultiplier;
         }
 
-        void SpawnCytoplasm()
-        {
-            if (cellConfigData.CytoplasmPrefab == null) return;
-
-            var snowChanger = Instantiate(cellConfigData.CytoplasmPrefab, transform.position, Quaternion.identity);
-            snowChanger.Initialize();
-        }
-
         void ResetVolumes()
         {
             teamVolumes[Domains.Jade] = 0;
```

</details>

### `dcb744108` — fix(compile): resolve compilation errors across 11 files

_Claude, 2026-03-06 04:11:16 +0000_

```text
- Fix ScriptableEventNoParam += / -= to use .OnRaised pattern (EndGameCinematicController, ElementalComebackSystem, GameModeProgressionService)
- Replace AudioSystem.Instance with [Inject] AudioSystem DI (EndGameCinematicController, CountdownTimer, GameCard)
- Add missing xp field to PlayerProfileData and GetXP() to PlayerDataService
- Fix LogControlWindow namespace from Utility.Tools to CosmicShore.Utility
- Fix CallToActionTargetType namespace from Systems.CTA to CosmicShore.Data
- Remove nonexistent domain field from IPlayer.InitializeData in MiniGame
- Add CosmicShore.Game using for CapsuleMembrane in Cell.cs
- Remove stale crystalLattice reference in SnowChanger
- Fix CaptainManager.OnLoadCaptainData static event access in HangarCaptainsView
```

```text
 Assets/_Scripts/Controller/Arcade/CountdownTimer.cs                  |  4 +++-
 Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs         |  4 ++--
 Assets/_Scripts/Controller/Arcade/MiniGame.cs                        |  1 -
 Assets/_Scripts/Controller/Environment/Cell.cs                       |  1 +
 Assets/_Scripts/Controller/Environment/Cytoplasm/SnowChanger.cs      |  3 ---
 Assets/_Scripts/System/Progression/GameModeProgressionService.cs     |  4 ++--
 Assets/_Scripts/UI/Elements/GameCard.cs                              |  8 +++++---
 Assets/_Scripts/UI/Views/HangarCaptainsView.cs                       |  4 ++--
 Assets/_Scripts/UI/Views/PlayerDataService.cs                        |  6 +++++-
 Assets/_Scripts/UI/Views/PlayerProfileData.cs                        |  1 +
 Assets/_Scripts/Utility/DataContainers/EndGameCinematicController.cs | 11 +++++++----
 11 files changed, 28 insertions(+), 19 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 271 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/CountdownTimer.cs b/Assets/_Scripts/Controller/Arcade/CountdownTimer.cs
index 4489d6ced..690887d33 100644
--- a/Assets/_Scripts/Controller/Arcade/CountdownTimer.cs
+++ b/Assets/_Scripts/Controller/Arcade/CountdownTimer.cs
@@ -1,6 +1,7 @@
 using CosmicShore.Core;
 using CosmicShore.UI;
 using DG.Tweening;
+using Reflex.Attributes;
 using System;
 using UnityEngine;
 using UnityEngine.UI;
@@ -9,6 +10,7 @@ namespace CosmicShore.Gameplay
 {
     public class CountdownTimer : MonoBehaviour
     {
+        [Inject] AudioSystem audioSystem;
         [SerializeField] Image   countdownDisplay;
         [SerializeField] Sprite  countdown3;
         [SerializeField] Sprite  countdown2;
@@ -56,7 +58,7 @@ namespace CosmicShore.Gameplay
                     countdownDisplay.color = idx >= urgentStart
                         ? urgentColor
                         : Color.white;
-                    AudioSystem.Instance.PlaySFXClip(countdownBeep);
+                    audioSystem.PlaySFXClip(countdownBeep);
                 });
 
                 // Fade in from transparent
diff --git a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
index cbb402f6b..5f1a14ae8 100644
--- a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
+++ b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
@@ -65,7 +65,7 @@ namespace CosmicShore.Gameplay
 
             gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
             gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
-            gameData.OnMiniGameEnd += OnGameEnded;
+            gameData.OnMiniGameEnd.OnRaised += OnGameEnded;
 
             if (debugLogging)
                 CSDebug.Log("[ElementalComebackSystem] Enabled and subscribed to game events.");
@@ -76,7 +76,7 @@ namespace CosmicShore.Gameplay
             if (gameData == null) return;
             gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
             gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
-            gameData.OnMiniGameEnd -= OnGameEnded;
+            gameData.OnMiniGameEnd.OnRaised -= OnGameEnded;
         }
 
         void OnTurnStarted()
diff --git a/Assets/_Scripts/Controller/Arcade/MiniGame.cs b/Assets/_Scripts/Controller/Arcade/MiniGame.cs
index 1aced2600..2104a16bc 100644
--- a/Assets/_Scripts/Controller/Arcade/MiniGame.cs
+++ b/Assets/_Scripts/Controller/Arcade/MiniGame.cs
@@ -202,7 +202,6 @@ namespace CosmicShore.Gameplay
                 IPlayer.InitializeData data = new()
                 {
                     vesselClass = playerShipTypeInitialized ? PlayerVesselType : defaultPlayerVesselType,
-                    domain = PlayerTeams[i],
                     PlayerName = i == 0 ? PlayerDataController.PlayerProfile.DisplayName : PlayerNames[i],
                     // PlayerUUID = PlayerNames[i]
                 };
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index 5c1e77ddf..e7acb8d40 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -1,6 +1,7 @@
 // Cell.cs
 using System.Collections.Generic;
 using System.Linq;
+using CosmicShore.Game;
 using CosmicShore.Gameplay;
 using CosmicShore.Utility;
 using Reflex.Attributes;
diff --git a/Assets/_Scripts/Controller/Environment/Cytoplasm/SnowChanger.cs b/Assets/_Scripts/Controller/Environment/Cytoplasm/SnowChanger.cs
index eab09e132..b8366c877 100644
--- a/Assets/_Scripts/Controller/Environment/Cytoplasm/SnowChanger.cs
+++ b/Assets/_Scripts/Controller/Environment/Cytoplasm/SnowChanger.cs
@@ -80,9 +80,6 @@ namespace CosmicShore.Gameplay
```

</details>

### `e87487b91` — fix: resolve compilation errors across 8 files

_Claude, 2026-03-06 04:26:04 +0000_

```text
- HangarCaptainsView: access static OnLoadCaptainData via class, not instance
- UGSDataService: replace removed AuthenticationController.Instance with
  UGS AuthenticationService.Instance.SignedIn event directly
- ArcadeGameConfigureModal: add [Inject] HostConnectionDataSO field, use
  inherited audioSystem instead of AudioSystem.Instance, add stub methods
  for ShowVesselSelectionScreen and ShowSquadMateSelectionScreen
- FactionMissionModal: use inherited audioSystem from ModalWindowManager
- HangarTrainingModal: use inherited audioSystem from ModalWindowManager
- EpisodeScreen: inject IAPManager via DI instead of .Instance
- PrismStateManager: inject AudioSystem via DI instead of .Instance
- ServerPlayerVesselInitializerWithAI: rename Captains to Vessels and
  access SO_Vessel.Class directly (property was renamed)
```

```text
 Assets/_Scripts/Controller/Managers/PrismStateManager.cs                      |  7 +++++--
 Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs |  8 ++++----
 Assets/_Scripts/System/CloudData/UGSDataService.cs                            | 25 +++++++++++++------------
 Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs                         | 16 +++++++++++++++-
 Assets/_Scripts/UI/Modals/FactionMissionModal.cs                              |  2 +-
 Assets/_Scripts/UI/Modals/HangarTrainingModal.cs                              |  2 +-
 Assets/_Scripts/UI/Screens/EpisodeScreen.cs                                   |  5 ++++-
 Assets/_Scripts/UI/Views/HangarCaptainsView.cs                                |  6 ++----
 8 files changed, 45 insertions(+), 26 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 240 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Managers/PrismStateManager.cs b/Assets/_Scripts/Controller/Managers/PrismStateManager.cs
index 275a069a4..59501a872 100644
--- a/Assets/_Scripts/Controller/Managers/PrismStateManager.cs
+++ b/Assets/_Scripts/Controller/Managers/PrismStateManager.cs
@@ -3,6 +3,7 @@ using UnityEngine;
 using System.Collections;
 using System;
 using CosmicShore.Core;
+using Reflex.Attributes;
 
 namespace CosmicShore.Gameplay
 {
@@ -19,6 +20,8 @@ namespace CosmicShore.Gameplay
         [Header("Data Containers")] [SerializeField]
         ThemeManagerDataContainerSO _themeManagerData;
 
+        [Inject] AudioSystem _audioSystem;
+
         private Prism prism;
         private MaterialPropertyAnimator materialAnimator;
         private PrismTeamManager teamManager;
@@ -108,7 +111,7 @@ namespace CosmicShore.Gameplay
             CurrentState = BlockState.Shielded;
 
             SyncAOERegistryShieldState();
-            AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.ShieldActivate);
+            _audioSystem.PlayGameplaySFX(GameplaySFXCategory.ShieldActivate);
         }
 
         private void ApplyNormalState()
@@ -127,7 +130,7 @@ namespace CosmicShore.Gameplay
             SyncAOERegistryShieldState();
 
             if (wasShielded)
-                AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.ShieldDeactivate);
+                _audioSystem.PlayGameplaySFX(GameplaySFXCategory.ShieldDeactivate);
         }
 
         private void SyncAOERegistryShieldState()
diff --git a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
index 701a9851b..02b5ad601 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
@@ -198,11 +198,11 @@ namespace CosmicShore.Gameplay
             if (gameList != null)
             {
                 var game = FindGameByMode(gameData.GameMode);
-                if (game != null && game.Captains is { Count: > 0 })
+                if (game != null && game.Vessels is { Count: > 0 })
                 {
-                    var captain = game.Captains[Random.Range(0, game.Captains.Count)];
-                    if (captain?.Ship != null && vesselPrefabContainer.TryGetShipPrefab(captain.Ship.Class, out _))
-                        return captain.Ship.Class;
+                    var vessel = game.Vessels[Random.Range(0, game.Vessels.Count)];
+                    if (vessel != null && vesselPrefabContainer.TryGetShipPrefab(vessel.Class, out _))
+                        return vessel.Class;
                 }
             }
             return VesselClassType.Sparrow;
diff --git a/Assets/_Scripts/System/CloudData/UGSDataService.cs b/Assets/_Scripts/System/CloudData/UGSDataService.cs
index 5ac850cf1..81a7a48cd 100644
--- a/Assets/_Scripts/System/CloudData/UGSDataService.cs
+++ b/Assets/_Scripts/System/CloudData/UGSDataService.cs
@@ -7,6 +7,7 @@ using CosmicShore.Gameplay;
 using CosmicShore.UI;
 using CosmicShore.Core;
 using CosmicShore.Utility;
+using Unity.Services.Authentication;
 using Unity.Services.Core;
 using UnityEngine;
 
@@ -96,23 +97,23 @@ namespace CosmicShore.Core
             if (Instance == this)
                 Instance = null;
 
-            var auth = AuthenticationController.Instance;
-            if (auth != null)
-                auth.OnSignedIn -= HandleSignedIn;
+            if (UnityServices.State == ServicesInitializationState.Initialized &&
+                AuthenticationService.Instance != null)
```

</details>

### `aeb257734` — fix(ui): replace AudioSystem.Instance with DI injection in ArcadeExploreView

_Claude, 2026-03-06 04:53:11 +0000_

```text
AudioSystem no longer has a static Instance property. Use [Inject] to
get the DI-registered singleton instead.
```

```text
 Assets/_Scripts/UI/Views/ArcadeExploreView.cs | 3 ++-
 1 file changed, 2 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Views/ArcadeExploreView.cs b/Assets/_Scripts/UI/Views/ArcadeExploreView.cs
index 3fa39c77c..3b280c7b8 100644
--- a/Assets/_Scripts/UI/Views/ArcadeExploreView.cs
+++ b/Assets/_Scripts/UI/Views/ArcadeExploreView.cs
@@ -18,6 +18,7 @@ namespace CosmicShore.UI
     {
         [Header("Game Selection View")]
         [Inject] SO_GameList GameList;
+        [Inject] AudioSystem audioSystem;
         [SerializeField] GameObject GameSelectionView;
         [SerializeField] Transform GameSelectionGrid;
         [SerializeField] ArcadeDPadNav ArcadeDPadNav;
@@ -154,7 +155,7 @@ namespace CosmicShore.UI
 
         public void PlaySelectedGame()
         {
-            AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.LetsGo);
+            audioSystem.PlayMenuAudio(MenuAudioCategory.LetsGo);
             LoadoutSystem.SaveGameLoadOut(SelectedGame.Mode, new Loadout(MiniGame.IntensityLevel, MiniGame.NumberOfPlayers, MiniGame.PlayerVesselType, SelectedGame.Mode, SelectedGame.IsMultiplayer));
             Arcade.Instance.LaunchArcadeGame(SelectedGame.Mode, MiniGame.PlayerVesselType, MiniGame.ResourceCollection, MiniGame.IntensityLevel, MiniGame.NumberOfPlayers, SelectedGame.IsMultiplayer, false);
         }
```

</details>

### `93c211a94` — fix(editor): add missing CosmicShore.ScriptableObjects using to PlayfabProductGenerator

_Claude, 2026-03-06 05:00:03 +0000_

```text
The SO_Captain type requires the CosmicShore.ScriptableObjects namespace.
```

```text
 Assets/_Scripts/Editor/PlayfabProductGenerator.cs | 1 +
 1 file changed, 1 insertion(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/PlayfabProductGenerator.cs b/Assets/_Scripts/Editor/PlayfabProductGenerator.cs
index d3c6c9854..8780ba9ef 100644
--- a/Assets/_Scripts/Editor/PlayfabProductGenerator.cs
+++ b/Assets/_Scripts/Editor/PlayfabProductGenerator.cs
@@ -3,6 +3,7 @@ using UnityEditor;
 using PlayFab;
 using System;
 using CosmicShore.Core;
+using CosmicShore.ScriptableObjects;
 using CosmicShore.Utility;
 
 public class PlayFabProductGenerator : EditorWindow
```

</details>

### `df8d4d91a` — fix(editor): add missing CosmicShore.Gameplay using to ElementalFloatEditor

_Claude, 2026-03-06 05:07:56 +0000_

```text
The ElementalFloat class lives in the CosmicShore.Gameplay namespace,
but ElementalFloatEditor.cs was missing the using directive, causing
CS0246 compile error.
```

```text
 Assets/_Scripts/Editor/ElementalFloatEditor.cs | 1 +
 1 file changed, 1 insertion(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/ElementalFloatEditor.cs b/Assets/_Scripts/Editor/ElementalFloatEditor.cs
index 766e3518f..0385cb383 100644
--- a/Assets/_Scripts/Editor/ElementalFloatEditor.cs
+++ b/Assets/_Scripts/Editor/ElementalFloatEditor.cs
@@ -2,6 +2,7 @@
 using UnityEditor;
 using System.Collections.Generic;
 using System.Reflection;
+using CosmicShore.Gameplay;
 using CosmicShore.Utility;
 
 public class ElementalFloatEditor : EditorWindow
```

</details>

### `f6a64643e` — fix(prism): null-check AudioSystem in PrismStateManager shield methods

_Claude, 2026-03-06 05:34:24 +0000_

```text
Prisms spawned via SpawnableRings (explosion chain) bypass the DI
pipeline, so [Inject] AudioSystem is null. Add null-conditional
operator to prevent NullReferenceException on Menu_Main startup.
```

```text
 Assets/_Scripts/Controller/Managers/PrismStateManager.cs | 4 ++--
 1 file changed, 2 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Managers/PrismStateManager.cs b/Assets/_Scripts/Controller/Managers/PrismStateManager.cs
index 59501a872..131bc41d9 100644
--- a/Assets/_Scripts/Controller/Managers/PrismStateManager.cs
+++ b/Assets/_Scripts/Controller/Managers/PrismStateManager.cs
@@ -111,7 +111,7 @@ namespace CosmicShore.Gameplay
             CurrentState = BlockState.Shielded;
 
             SyncAOERegistryShieldState();
-            _audioSystem.PlayGameplaySFX(GameplaySFXCategory.ShieldActivate);
+            _audioSystem?.PlayGameplaySFX(GameplaySFXCategory.ShieldActivate);
         }
 
         private void ApplyNormalState()
@@ -130,7 +130,7 @@ namespace CosmicShore.Gameplay
             SyncAOERegistryShieldState();
 
             if (wasShielded)
-                _audioSystem.PlayGameplaySFX(GameplaySFXCategory.ShieldDeactivate);
+                _audioSystem?.PlayGameplaySFX(GameplaySFXCategory.ShieldDeactivate);
         }
 
         private void SyncAOERegistryShieldState()
```

</details>

### `5871008a7` — fix(daily-challenge): guard against null Arcade.Instance in FetchDailyChallenge

_Claude, 2026-03-06 06:03:08 +0000_

```text
DailyChallengeSystem.Start() calls FetchDailyChallenge() which accesses
Arcade.Instance.TrainingGames.Games, but Arcade (another SingletonPersistent)
may not be initialized yet when DailyChallengeSystem.Start() runs. This caused
a NullReferenceException on every Menu_Main load.

Added null checks in FetchDailyChallenge() and early-return in SelectDailyGame()
so the system gracefully defers when Arcade isn't ready. The existing
DailyGame == null fallback at line 100 ensures it will retry on next access.
```

```text
 Assets/_Scripts/System/DailyChallengeSystem.cs | 14 ++++++++++++--
 1 file changed, 12 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/DailyChallengeSystem.cs b/Assets/_Scripts/System/DailyChallengeSystem.cs
index 3f2637ac3..bf2ec28bb 100644
--- a/Assets/_Scripts/System/DailyChallengeSystem.cs
+++ b/Assets/_Scripts/System/DailyChallengeSystem.cs
@@ -135,18 +135,28 @@ namespace CosmicShore.Core
 
         void SelectDailyGame()
         {
+            var challenge = FetchDailyChallenge();
+            if (challenge == null)
+                return;
+
             ChallengeDate = DateTime.UtcNow.Date;
             PlayerPrefs.SetString(InitializedDatePrefKey, DateTime.UtcNow.Date.ToString("o"));
             PlayerPrefs.Save();
 
-            dailyChallenge = FetchDailyChallenge();
+            dailyChallenge = challenge;
             DailyGame = Arcade.Instance.GetTrainingGameByMode(dailyChallenge.GameMode);
             ShipResources = LoadGameResourceCollection(DailyGame);
         }
 
         DailyChallenge FetchDailyChallenge()
         {
-            // Use the 32 least significant bits (& 0xFFFFFFFF) of the tick count from today's date in GMT as the random seed 
+            if (Arcade.Instance == null || Arcade.Instance.TrainingGames == null || Arcade.Instance.TrainingGames.Games == null || Arcade.Instance.TrainingGames.Games.Count == 0)
+            {
+                Debug.LogWarning("DailyChallengeSystem: Arcade not ready, deferring daily challenge fetch.");
+                return null;
+            }
+
+            // Use the 32 least significant bits (& 0xFFFFFFFF) of the tick count from today's date in GMT as the random seed
             DateTime currentDate = DateTime.UtcNow.Date;
             long dateTicks = currentDate.Ticks;
             var random = new System.Random((int)(dateTicks & 0xFFFFFFFF));
```

</details>

### `2b51fdd3e` — fix(scene): remove orphaned UnlockVesselButton from Menu_Main

_Claude, 2026-03-06 06:16:12 +0000_

```text
The UnlockVesselButton GameObject (fileID 1243877416) referenced a
non-existent parent RectTransform (fileID 1836587354), causing a
"Broken text PPtr" error on scene load. The button had no OnClick
handler wired and no other objects referenced it — safe to delete.
```

```text
 Assets/_Scenes/Menu_Main.unity | 120 -----------------------------------------------------------------------
 1 file changed, 120 deletions(-)
```

_Also contains 9 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
