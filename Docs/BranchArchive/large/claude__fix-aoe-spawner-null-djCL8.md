# Branch archive: `claude/fix-aoe-spawner-null-djCL8`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

<!-- SUMMARY -->

- **Last commit:** 2026-03-06 by Claude
- **Unmerged commits:** 29
- **Forked from:** `00c41c3d0` (2026-03-06, merge: integrate origin/app-shell-polish into merge branch)
- **Tip:** `81cfca6ec`
- **Files touched (37):**
  - `Assets/_Scenes/Menu_Main.unity`
  - `Assets/_Scripts/Controller/Arcade/CountdownTimer.cs`
  - `Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs`
  - `Assets/_Scripts/Controller/Arcade/MiniGame.cs`
  - `Assets/_Scripts/Controller/Environment/Cell.cs`
  - `Assets/_Scripts/Controller/Environment/Cytoplasm/SnowChanger.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/ExplosionHelper.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Skimmer Effects/VesselOvertakeBySkimmerEffectSO.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/MineImpactor.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/OmniCrystalImpactor.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/ProjectileImpactor.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/VesselImpactor.cs`
  - `Assets/_Scripts/Controller/Managers/PrismStateManager.cs`
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs`
  - `Assets/_Scripts/Controller/Projectiles/AOEBlockSpawner.cs`
  - `Assets/_Scripts/Editor/ElementalFloatEditor.cs`
  - `Assets/_Scripts/Editor/PlayfabProductGenerator.cs`
  - `Assets/_Scripts/System/CloudData/UGSDataService.cs`
  - `Assets/_Scripts/System/DailyChallengeSystem.cs`
  - `Assets/_Scripts/System/Progression/GameModeProgressionService.cs`
  - `Assets/_Scripts/System/Squads/SquadSystem.cs`
  - `Assets/_Scripts/UI/Elements/GameCard.cs`
  - `Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs`
  - `Assets/_Scripts/UI/Modals/FactionMissionModal.cs`
  - `Assets/_Scripts/UI/Modals/HangarTrainingModal.cs`
  - `Assets/_Scripts/UI/Screens/EpisodeScreen.cs`
  - `Assets/_Scripts/UI/Views/ArcadeExploreView.cs`
  - `Assets/_Scripts/UI/Views/HangarCaptainsView.cs`
  - `Assets/_Scripts/UI/Views/PlayerDataService.cs`
  - `Assets/_Scripts/UI/Views/PlayerProfileData.cs`
  - `Assets/_Scripts/UI/Views/PortSquadView.cs`
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

### `622ac3ac3` — fix(daily-challenge): resolve CS0019 and CS0037 errors for DailyChallenge struct

_Claude, 2026-03-06 06:15:15 +0000_

```text
FetchDailyChallenge() returned a non-nullable struct but used `return null`,
and the caller compared the struct to null with `==`. Changed return type to
`DailyChallenge?` (nullable) so both operations are valid.
```

```text
 Assets/_Scripts/System/DailyChallengeSystem.cs | 4 ++--
 1 file changed, 2 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/DailyChallengeSystem.cs b/Assets/_Scripts/System/DailyChallengeSystem.cs
index bf2ec28bb..7144698a3 100644
--- a/Assets/_Scripts/System/DailyChallengeSystem.cs
+++ b/Assets/_Scripts/System/DailyChallengeSystem.cs
@@ -143,12 +143,12 @@ namespace CosmicShore.Core
             PlayerPrefs.SetString(InitializedDatePrefKey, DateTime.UtcNow.Date.ToString("o"));
             PlayerPrefs.Save();
 
-            dailyChallenge = challenge;
+            dailyChallenge = challenge.Value;
             DailyGame = Arcade.Instance.GetTrainingGameByMode(dailyChallenge.GameMode);
             ShipResources = LoadGameResourceCollection(DailyGame);
         }
 
-        DailyChallenge FetchDailyChallenge()
+        DailyChallenge? FetchDailyChallenge()
         {
             if (Arcade.Instance == null || Arcade.Instance.TrainingGames == null || Arcade.Instance.TrainingGames.Games == null || Arcade.Instance.TrainingGames.Games.Count == 0)
             {
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

### `79c73f161` — fix(squads): guard against null CaptainList in SquadSystem property getters

_Claude, 2026-03-06 06:38:10 +0000_

```text
CaptainList is never populated since the captain system was removed from
vessels, causing ArgumentNullException in LINQ .Where() when PortSquadView
accesses SquadLeader/RogueOne/RogueTwo during Start().
```

```text
 Assets/_Scripts/System/Squads/SquadSystem.cs | 7 +++++--
 Assets/_Scripts/UI/Views/PortSquadView.cs    | 3 +++
 2 files changed, 8 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/Squads/SquadSystem.cs b/Assets/_Scripts/System/Squads/SquadSystem.cs
index e23db41b6..d9e1e5a52 100644
--- a/Assets/_Scripts/System/Squads/SquadSystem.cs
+++ b/Assets/_Scripts/System/Squads/SquadSystem.cs
@@ -50,7 +50,8 @@ namespace CosmicShore.Core
                 if (Squad.Equals(default(Squad)))
                     Init();
 
-                return CaptainList.Where(x => x.PrimaryElement == Squad.SquadLeaderElement && x.Vessel.Class == Squad.SquadLeaderClass).FirstOrDefault(); 
+                if (CaptainList == null) return null;
+                return CaptainList.Where(x => x.PrimaryElement == Squad.SquadLeaderElement && x.Vessel.Class == Squad.SquadLeaderClass).FirstOrDefault();
             }
         }
 
@@ -61,7 +62,8 @@ namespace CosmicShore.Core
                 if (Squad.Equals(default(Squad)))
                     Init();
 
-                return CaptainList.Where(x => x.PrimaryElement == Squad.RogueOneElement && x.Vessel.Class == Squad.RogueOneClass).FirstOrDefault(); 
+                if (CaptainList == null) return null;
+                return CaptainList.Where(x => x.PrimaryElement == Squad.RogueOneElement && x.Vessel.Class == Squad.RogueOneClass).FirstOrDefault();
             }
         }
 
@@ -72,6 +74,7 @@ namespace CosmicShore.Core
                 if (Squad.Equals(default(Squad)))
                     Init();
 
+                if (CaptainList == null) return null;
                 return CaptainList.Where(x => x.PrimaryElement == Squad.RogueTwoElement && x.Vessel.Class == Squad.RogueTwoClass).FirstOrDefault(); 
             }
         }
diff --git a/Assets/_Scripts/UI/Views/PortSquadView.cs b/Assets/_Scripts/UI/Views/PortSquadView.cs
index 0bccd8e61..085cde164 100644
--- a/Assets/_Scripts/UI/Views/PortSquadView.cs
+++ b/Assets/_Scripts/UI/Views/PortSquadView.cs
@@ -80,6 +80,9 @@ namespace CosmicShore.UI
 
         public override void UpdateView()
         {
+            if (SquadSystem.CaptainList == null || SquadSystem.CaptainList.Count == 0)
+                return;
+
             SquadSystem.LoadSquad();
             PlayerCaptainButton.Captain = SquadSystem.SquadLeader;
             RogueOneCaptainButton.Captain = SquadSystem.RogueOne;
```

</details>

### `e2010cc3d` — fix(impact): guard against null Impactor in ImpactorBase.OnTriggerEnter

_Claude, 2026-03-06 06:41:21 +0000_

```text
ImpactCollider.Impactor returns null when its serialized impactorObject
field is unassigned (via `as IImpactor` cast). This null propagated
into SkimmerImpactor.AcceptImpactee causing NullReferenceException.
Adding the null check in the base class protects all impactor subclasses.
```

```text
 Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs | 8 ++++++--
 1 file changed, 6 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
index f0704d7f5..0a56bf6b7 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
@@ -29,8 +29,12 @@ namespace CosmicShore.Gameplay
             
             if (!other.TryGetComponent(out IImpactCollider impacteeCollider))
                 return;
-            
-            AcceptImpactee(impacteeCollider.Impactor);
+
+            var impactor = impacteeCollider.Impactor;
+            if (impactor == null)
+                return;
+
+            AcceptImpactee(impactor);
         }
     }
 }
\ No newline at end of file
```

</details>

### `65aaacd19` — fix(impact): guard against null effects and missing container in SkimmerImpactor

_Claude, 2026-03-06 06:56:18 +0000_

```text
The NRE at SkimmerImpactor:134 persists because the actual null is a
null element inside the effects array on the SkimmerImpactorDataContainerSO
asset — DoesEffectExist only checks array existence and length, not
individual elements. Add per-element null checks in all three effect
loops, plus an early return if the container SO itself is unassigned.
```

```text
 Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs | 8 +++++++-
 1 file changed, 7 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs
index 376e134b9..24c9bf094 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs
@@ -123,7 +123,10 @@ namespace CosmicShore.Gameplay
         {
             if (!isInitialized)
                 return;
-            
+
+            if (skimmerImpactorDataContainer == null)
+                return;
+
             switch (impactee)
             {
                 case VesselImpactor shipImpactor:
@@ -131,6 +134,7 @@ namespace CosmicShore.Gameplay
                     if (!DoesEffectExist(evs)) return;
                     foreach (var effect in evs)
                     {
+                        if (effect == null) continue;
                         effect.Execute(shipImpactor, this);
                     }
 
@@ -145,6 +149,7 @@ namespace CosmicShore.Gameplay
 
                     foreach (var effect in esp)
                     {
+                        if (effect == null) continue;
                         effect.Execute(this, prismImpactor);
                     }
 
@@ -160,6 +165,7 @@ namespace CosmicShore.Gameplay
                     if (!DoesEffectExist(esc)) return;
                     foreach (var effect in esc)
                     {
+                        if (effect == null) continue;
                         effect.Execute(this, elementalCrystalImpactor);
                     }
 
```

</details>

### `0db39c828` — fix(impact): add null guards for effect elements across all impactors

_Claude, 2026-03-06 07:07:04 +0000_

```text
The VesselImpactor NRE at line 36 (and the earlier SkimmerImpactor NRE)
are caused by null elements inside effect SO arrays on the data container
assets. DoesEffectExist() only checks array existence/length, not
individual elements.

Fixed all 7 impactor subclasses:
- VesselImpactor: container null guard + per-element null checks (5 loops)
- PrismImpactor: per-element null checks (4 loops)
- ProjectileImpactor: container null guard + per-element null checks (4 loops)
- MineImpactor: per-element null checks (3 loops)
- ExplosionImpactor: per-element null checks (2 loops)
- ElementalCrystalImpactor: per-element null check (1 loop)
- OmniCrystalImpactor: per-element null check (1 loop)
```

```text
 Assets/_Scripts/Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs |  3 +++
 Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs        |  4 +++-
 Assets/_Scripts/Controller/ImpactEffects/Impactors/MineImpactor.cs             |  3 +++
 Assets/_Scripts/Controller/ImpactEffects/Impactors/OmniCrystalImpactor.cs      |  3 +++
 Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs            | 10 +++++-----
 Assets/_Scripts/Controller/ImpactEffects/Impactors/ProjectileImpactor.cs       | 17 +++++++++++++----
 Assets/_Scripts/Controller/ImpactEffects/Impactors/VesselImpactor.cs           | 18 ++++++++++++++++++
 7 files changed, 48 insertions(+), 10 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 253 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs
index 9fe6b7c81..754f704d9 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs
@@ -42,7 +42,10 @@ namespace CosmicShore.Gameplay
             {
                 var data = CrystalImpactData.FromCrystal(Crystal);
                 foreach (var effect in elementalCrystalShipEffects)
+                {
+                    if (effect == null) continue;
                     effect.Execute(skimmerImpactor, this);
+                }
             }
 
             HandleCrystalVisualAndLifetime(skimmerImpactor);
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs
index e8d9b015b..56a8545c8 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs
@@ -101,10 +101,11 @@ namespace CosmicShore.Gameplay
                     if(!DoesEffectExist(vesselExplosionEffects)) return;
                     foreach (var effect in vesselExplosionEffects)
                     {
+                        if (effect == null) continue;
                         effect.Execute(vesselImpactee, this);
                     }
                     break;
-                
+
                 case PrismImpactor prismImpactee:
                     ExecuteCommonPrismCommands(prismImpactee.Prism, impactVector);
                     if (!explosionImpactorDataContainer) return;
@@ -112,6 +113,7 @@ namespace CosmicShore.Gameplay
                     if(!DoesEffectExist(explosionPrismEffects)) return;
                     foreach (var effect in explosionPrismEffects)
                     {
+                        if (effect == null) continue;
                         effect.Execute(this, prismImpactee);
                     }
                     break;
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/MineImpactor.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/MineImpactor.cs
index b5b5458cc..e893ca62b 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/MineImpactor.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/MineImpactor.cs
@@ -32,6 +32,7 @@ namespace CosmicShore.Gameplay
                     if(!DoesEffectExist(mineShipEffects)) return;
                     foreach (var effect in mineShipEffects)
                     {
+                        if (effect == null) continue;
                         effect.Execute(shipImpactee, this);
                     }
                     break;
@@ -39,6 +40,7 @@ namespace CosmicShore.Gameplay
                     if(!DoesEffectExist(mineProjectileEffects)) return;
                     foreach (var effect in mineProjectileEffects)
                     {
+                        if (effect == null) continue;
                         effect.Execute(projectileImpactee, this);
                     }
                     break;
@@ -46,6 +48,7 @@ namespace CosmicShore.Gameplay
                     if(!DoesEffectExist(mineExplosionEffects)) return;
                     foreach (var effect in mineExplosionEffects)
                     {
+                        if (effect == null) continue;
                         effect.Execute(explosionImpactee, this);
                     }
                     break;
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/OmniCrystalImpactor.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/OmniCrystalImpactor.cs
index c1fc82690..b892b06b7 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/OmniCrystalImpactor.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/OmniCrystalImpactor.cs
@@ -57,7 +57,10 @@ namespace CosmicShore.Gameplay
                     {
                         CrystalImpactData data = CrystalImpactData.FromCrystal(Crystal);
                         foreach (var effect in omniCrystalShipEffects)
+                        {
+                            if (effect == null) continue;
                             effect.Execute(shipImpactee, data);
+                        }
```

</details>

### `69b743aae` — fix(explosions): guard against null Vessel and spawnable in AOE explosion pipeline

_Claude, 2026-03-06 07:41:42 +0000_

```text
AOEBlockSpawner.ExplodeAsync threw NRE when Vessel or spawnable was null
during crystal impact explosions. Added null guards in AOEBlockSpawner and
both ExplosionHelper.CreateExplosion overloads to prevent unobserved async
exceptions when VesselStatus.Vessel or ShipTransform is null.
```

```text
 Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/ExplosionHelper.cs | 2 ++
 Assets/_Scripts/Controller/Projectiles/AOEBlockSpawner.cs                     | 6 ++++++
 2 files changed, 8 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/ExplosionHelper.cs b/Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/ExplosionHelper.cs
index fb4fefe00..3a0461119 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/ExplosionHelper.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/ExplosionHelper.cs
@@ -22,6 +22,7 @@ namespace CosmicShore.Gameplay
             if (impactor?.Vessel?.VesselStatus == null) return;
 
             var ss = impactor.Vessel.VesselStatus;
+            if (ss.Vessel == null || ss.ShipTransform == null) return;
             var shipTransform = ss.ShipTransform;
 
             var init = new AOEExplosion.InitializeStruct
@@ -49,6 +50,7 @@ namespace CosmicShore.Gameplay
 
             var proj = impactor.Projectile;
             var ss   = proj.VesselStatus;
+            if (ss?.Vessel == null) return;
 
             var init = new AOEExplosion.InitializeStruct
             {
diff --git a/Assets/_Scripts/Controller/Projectiles/AOEBlockSpawner.cs b/Assets/_Scripts/Controller/Projectiles/AOEBlockSpawner.cs
index 968ae4e05..9d84de4b5 100644
--- a/Assets/_Scripts/Controller/Projectiles/AOEBlockSpawner.cs
+++ b/Assets/_Scripts/Controller/Projectiles/AOEBlockSpawner.cs
@@ -16,6 +16,12 @@ namespace CosmicShore.Gameplay
         {
             try
             {
+                if (Vessel == null || spawnable == null)
+                {
+                    Debug.LogWarning($"[AOEBlockSpawner] ExplodeAsync aborted — Vessel:{Vessel != null}, spawnable:{spawnable != null}", this);
+                    return;
+                }
+
                 // AOEExplosion.Initialize() zeroes localScale to hide the explosion
                 // mesh during its delay. This subclass doesn't use the base explosion
                 // animation, so restore scale before spawning children.
```

</details>

### `81cfca6ec` — fix(explosions): destroy orphaned AOEBlockSpawner on null spawnable

_Claude, 2026-03-06 08:07:47 +0000_

```text
The early-return path was missing Destroy(gameObject), leaving an invisible
AOEBlockSpawner instance in the scene. Now properly cleans up on abort.
```

```text
 Assets/_Scripts/Controller/Projectiles/AOEBlockSpawner.cs | 1 +
 1 file changed, 1 insertion(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Projectiles/AOEBlockSpawner.cs b/Assets/_Scripts/Controller/Projectiles/AOEBlockSpawner.cs
index 9d84de4b5..25e712b5f 100644
--- a/Assets/_Scripts/Controller/Projectiles/AOEBlockSpawner.cs
+++ b/Assets/_Scripts/Controller/Projectiles/AOEBlockSpawner.cs
@@ -19,6 +19,7 @@ namespace CosmicShore.Gameplay
                 if (Vessel == null || spawnable == null)
                 {
                     Debug.LogWarning($"[AOEBlockSpawner] ExplodeAsync aborted — Vessel:{Vessel != null}, spawnable:{spawnable != null}", this);
+                    Destroy(gameObject);
                     return;
                 }
 
```

</details>

_Also contains 13 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
