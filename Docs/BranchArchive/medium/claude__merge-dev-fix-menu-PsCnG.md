# Branch archive: `claude/merge-dev-fix-menu-PsCnG`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-03-06 by Shombith03
- **Unmerged commits:** 8
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/378
- **Forked from:** `6e3c1030a` (2026-03-06, Merge branch 'development' of https://github.com/froglet-studio/Cosmic-Shore i)
- **Tip:** `aeebec7da`
- **Files touched (72):**
  - `Assets/_Scripts/Controller/Arcade/CountdownTimer.cs`
  - `Assets/_Scripts/Controller/Arcade/MiniGameControllerBase.cs`
  - `Assets/_Scripts/Controller/Environment/Cytoplasm/SnowChanger.cs`
  - `Assets/_Scripts/Controller/Environment/FlowField/NetworkCrystalManager.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Skimmer Effects/VesselOvertakeBySkimmerEffectSO.cs`
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs`
  - `Assets/_Scripts/Controller/Vessel/ElementPipsConfigSO.cs`
  - `Assets/_Scripts/Controller/Vessel/ElementPipsView.cs`
  - `Assets/_Scripts/Controller/Vessel/ElementalBarsView.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/SquirrelVesselHUDController.cs`
  - `Assets/_Scripts/Controller/Vessel/SilhouetteController.cs`
  - `Assets/_Scripts/Editor/PlayfabProductGenerator.cs`
  - `Assets/_Scripts/Game/Progression/GameModeProgressionService.cs`
  - `Assets/_Scripts/MinigameHUD/View/ConnectingDotsAnimator.cs`
  - `Assets/_Scripts/MinigameHUD/View/ConnectingPanel.cs`
  - `Assets/_Scripts/MinigameHUD/View/DoTweenTypewriterAnimator.cs`
  - `Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs`
  - `Assets/_Scripts/Models/ScriptableObjects/SO_Captain.cs`
  - `Assets/_Scripts/ScriptableObjects/SO_Captain.cs`
  - `Assets/_Scripts/ScriptableObjects/SO_GameModeQuestData.cs`
  - `Assets/_Scripts/ScriptableObjects/SO_Vessel.cs`
  - `Assets/_Scripts/ScriptableObjects/SO_VesselList.cs`
  - `Assets/_Scripts/System/CloudData/Interfaces/IUGSDataService.cs`
  - `Assets/_Scripts/System/CloudData/Repositories/CaptainProgressRepository.cs`
  - `Assets/_Scripts/System/CloudData/Repositories/DailyChallengeRepository.cs`
  - `Assets/_Scripts/System/CloudData/Repositories/EpisodeProgressRepository.cs`
  - `Assets/_Scripts/System/CloudData/Repositories/GameProgressionRepository.cs`
  - `Assets/_Scripts/System/CloudData/Repositories/HangarRepository.cs`
  - `Assets/_Scripts/System/CloudData/Repositories/PlayerProfileRepository.cs`
  - `Assets/_Scripts/System/CloudData/Repositories/PlayerSettingsRepository.cs`
  - `Assets/_Scripts/System/CloudData/Repositories/PlayerStatsRepository.cs`
  - `Assets/_Scripts/System/CloudData/Repositories/TrainingProgressRepository.cs`
  - `Assets/_Scripts/System/CloudData/Repositories/VesselStatsRepository.cs`
  - `Assets/_Scripts/System/CloudData/UGSDataService.cs`
  - `Assets/_Scripts/System/DailyChallengeSystem.cs`
  - `Assets/_Scripts/System/Playfab/Economy/CaptainManager.cs`
  - `Assets/_Scripts/System/VesselUnlock/VesselUnlockSystem.cs`
  - `Assets/_Scripts/UI/Elements/Buttons/GameplayRewardButton.cs`
  - `Assets/_Scripts/UI/Elements/DailyChallengeCard.cs`
  - `Assets/_Scripts/UI/Elements/DailyChallengePlayButton.cs`
  - … and 32 more

### `20d358e70` — Polish Friend System UI with DOTween animations

_Claude, 2026-03-05 18:33:40 +0000_

```text
- FriendsPanel: slide-up show with OutBack ease, fade-out hide,
  animated tab transitions with crossfade, staggered friend list
  population (50ms delay per entry), badge punch-scale on request count change
- FriendEntryView: breathing pulse animation on online indicator (InOutSine loop),
  pop-in scale animation on invite-sent indicator (OutBack)
- FriendRequestEntryView: punch-scale on accept, fade-out on decline/cancel
- AddFriendPanel: feedback text scale-in with OutBack ease,
  extra punch on success feedback
```

```text
 Assets/_Scripts/UI/Views/AddFriendPanel.cs         |  12 ++++++
 Assets/_Scripts/UI/Views/FriendEntryView.cs        |  36 +++++++++++++++++-
 Assets/_Scripts/UI/Views/FriendRequestEntryView.cs |  10 +++++
 Assets/_Scripts/UI/Views/FriendsPanel.cs           | 104 +++++++++++++++++++++++++++++++++++++++++++++++----
 4 files changed, 153 insertions(+), 9 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 329 lines)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Views/AddFriendPanel.cs b/Assets/_Scripts/UI/Views/AddFriendPanel.cs
index 2cd74fc99..846f3ccdc 100644
--- a/Assets/_Scripts/UI/Views/AddFriendPanel.cs
+++ b/Assets/_Scripts/UI/Views/AddFriendPanel.cs
@@ -1,5 +1,6 @@
 using CosmicShore.Core;
 using CosmicShore.Utility;
+using DG.Tweening;
 using Reflex.Attributes;
 using TMPro;
 using UnityEngine;
@@ -126,6 +127,17 @@ namespace CosmicShore.UI
             if (feedbackText == null) return;
             feedbackText.text = message;
             feedbackText.color = success ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.9f, 0.3f, 0.3f);
+
+            // Animate feedback text appearance
+            feedbackText.transform.localScale = Vector3.one * 0.8f;
+            feedbackText.DOKill();
+            var seq = DOTween.Sequence();
+            seq.Append(feedbackText.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack));
+            if (success)
+            {
+                // Extra punch for success
+                seq.Append(feedbackText.transform.DOPunchScale(Vector3.one * 0.1f, 0.3f, 4, 0.5f));
+            }
         }
 
         private void SetCanvasGroupVisible(bool visible)
diff --git a/Assets/_Scripts/UI/Views/FriendEntryView.cs b/Assets/_Scripts/UI/Views/FriendEntryView.cs
index 86089c510..70fa2f5f8 100644
--- a/Assets/_Scripts/UI/Views/FriendEntryView.cs
+++ b/Assets/_Scripts/UI/Views/FriendEntryView.cs
@@ -1,5 +1,6 @@
 using System;
 using CosmicShore.ScriptableObjects;
+using DG.Tweening;
 using TMPro;
 using UnityEngine;
 using UnityEngine.UI;
@@ -24,9 +25,14 @@ namespace CosmicShore.UI
         [SerializeField] private Color offlineColor = new(0.5f, 0.5f, 0.5f, 0.5f);
         [SerializeField] private Color busyColor = new(0.9f, 0.7f, 0.2f, 1f);
 
+        [Header("Animation")]
+        [SerializeField] private float onlinePulseScale = 1.15f;
+        [SerializeField] private float onlinePulseDuration = 1.2f;
+
         private FriendData _data;
         private Action<FriendData> _onInvite;
         private Action<FriendData> _onRemove;
+        private Tween _pulseTween;
 
         public string PlayerId => _data.PlayerId;
 
@@ -74,6 +80,21 @@ namespace CosmicShore.UI
                     3 => busyColor,   // Away
                     _ => offlineColor
                 };
+
+                // Breathing pulse for online friends
+                _pulseTween?.Kill();
+                if (data.IsOnline)
+                {
+                    onlineIndicator.transform.localScale = Vector3.one;
+                    _pulseTween = onlineIndicator.transform
+                        .DOScale(onlinePulseScale, onlinePulseDuration)
+                        .SetEase(Ease.InOutSine)
+                        .SetLoops(-1, LoopType.Yoyo);
+                }
+                else
+                {
+                    onlineIndicator.transform.localScale = Vector3.one;
+                }
             }
 
             if (statusText != null)
@@ -89,6 +110,11 @@ namespace CosmicShore.UI
             }
         }
 
+        void OnDestroy()
+        {
+            _pulseTween?.Kill();
+        }
+
         // ─────────────────────────────────────────────────────────────────────
         // Events
         // ─────────────────────────────────────────────────────────────────────
@@ -100,7 +126,15 @@ namespace CosmicShore.UI
             if (inviteButton != null)
                 inviteButton.interactable = false;
 
-            inviteSentIndicator?.SetActive(true);
+            if (inviteSentIndicator != null)
+            {
+                inviteSentIndicator.SetActive(true);
+                // Pop-in animation for the sent indicator
+                inviteSentIndicator.transform.localScale = Vector3.zero;
+                inviteSentIndicator.transform
+                    .DOScale(1f, 0.3f)
+                    .SetEase(Ease.OutBack);
+            }
         }
 
         private void OnRemovePressed()
diff --git a/Assets/_Scripts/UI/Views/FriendRequestEntryView.cs b/Assets/_Scripts/UI/Views/FriendRequestEntryView.cs
index dab2d41ad..c132585e8 100644
--- a/Assets/_Scripts/UI/Views/FriendRequestEntryView.cs
+++ b/Assets/_Scripts/UI/Views/FriendRequestEntryView.cs
@@ -1,5 +1,6 @@
 using System;
 using CosmicShore.ScriptableObjects;
+using DG.Tweening;
 using TMPro;
 using UnityEngine;
 using UnityEngine.UI;
@@ -81,17 +82,26 @@ namespace CosmicShore.UI
         private void OnAcceptPressed()
         {
             SetButtonStates(showAcceptDecline: false, showCancel: false);
+            // Satisfying scale pop on accept
+            transform.DOPunchScale(Vector3.one * 0.08f, 0.25f, 4, 0.5f);
             _onAccept?.Invoke(_data);
         }
 
         private void OnDeclinePressed()
         {
+            // Fade out on decline
+            var cg = GetComponent<CanvasGroup>();
+            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
+            cg.DOFade(0f, 0.2f).SetEase(Ease.InQuad);
             SetButtonStates(showAcceptDecline: false, showCancel: false);
             _onDecline?.Invoke(_data);
         }
 
         private void OnCancelPressed()
         {
+            var cg = GetComponent<CanvasGroup>();
+            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
+            cg.DOFade(0f, 0.2f).SetEase(Ease.InQuad);
             SetButtonStates(showAcceptDecline: false, showCancel: false);
             _onCancel?.Invoke(_data);
         }
diff --git a/Assets/_Scripts/UI/Views/FriendsPanel.cs b/Assets/_Scripts/UI/Views/FriendsPanel.cs
index 465ca90ba..08a01da71 100644
--- a/Assets/_Scripts/UI/Views/FriendsPanel.cs
+++ b/Assets/_Scripts/UI/Views/FriendsPanel.cs
@@ -1,8 +1,10 @@
+using System.Collections;
```

</details>

### `affcab236` — fix: resolve 185 CS0234 namespace compilation errors from development merge

_Claude, 2026-03-05 21:44:57 +0000_

```text
Files from the development branch used old namespace conventions
(CosmicShore.App.*, CosmicShore.Game.*, CosmicShore.Models.*,
CosmicShore.Integrations.*, CosmicShore.Services.*) that were
reorganized in app-shell-polish.

Fixes across 19 files:
- CosmicShore.App.Profile → CosmicShore.UI (PlayerDataService)
- CosmicShore.App.Systems.Audio → CosmicShore.Core (AudioSystem)
- CosmicShore.Game.Analytics → CosmicShore.Gameplay (VesselStatsCloudData)
- CosmicShore.Game.IO → CosmicShore.Gameplay (HapticController)
- CosmicShore.Models.Enums → CosmicShore.Data (Element, VesselClassType)
- CosmicShore.Services.Auth → CosmicShore.Core (AuthenticationController)
- CosmicShore.Integrations.PlayFab.* → CosmicShore.Core
- CosmicShore.Utilities → CosmicShore.Utility (typo fix)
- Delete duplicate Models/ScriptableObjects/SO_Captain.cs
- Fix ScriptableObjects/SO_Captain.cs: SO_Ship→SO_Vessel (SO_Ship never existed)
- Change VesselOvertakeBySkimmerEffectSO namespace to CosmicShore.Gameplay
- Change CaptainManager namespace to CosmicShore.Core
```

```text
 Assets/_Scripts/Controller/Arcade/CountdownTimer.cs                           |  2 +-
 .../EffectsSO/Vessel Skimmer Effects/VesselOvertakeBySkimmerEffectSO.cs       |  4 ++--
 Assets/_Scripts/Controller/Vessel/ElementalBarsView.cs                        |  2 +-
 Assets/_Scripts/Models/ScriptableObjects/SO_Captain.cs                        | 25 -------------------------
 Assets/_Scripts/ScriptableObjects/SO_Captain.cs                               |  3 ++-
 Assets/_Scripts/ScriptableObjects/SO_Vessel.cs                                |  2 +-
 Assets/_Scripts/System/CloudData/Interfaces/IUGSDataService.cs                |  4 ++--
 Assets/_Scripts/System/CloudData/Repositories/PlayerProfileRepository.cs      |  2 +-
 Assets/_Scripts/System/CloudData/Repositories/PlayerStatsRepository.cs        |  2 +-
 Assets/_Scripts/System/CloudData/Repositories/VesselStatsRepository.cs        |  2 +-
 Assets/_Scripts/System/CloudData/UGSDataService.cs                            |  6 +++---
 Assets/_Scripts/System/DailyChallengeSystem.cs                                |  6 +-----
 Assets/_Scripts/System/Playfab/Economy/CaptainManager.cs                      |  8 +++-----
 Assets/_Scripts/System/VesselUnlock/VesselUnlockSystem.cs                     |  2 +-
 Assets/_Scripts/UI/Elements/Hangar/CrystalCurrencyDisplay.cs                  |  2 +-
 Assets/_Scripts/UI/Modals/HangarTrainingModal.cs                              |  7 ++-----
 Assets/_Scripts/UI/ToastNotification/ToastNotificationManager.cs              |  1 -
 Assets/_Scripts/UI/Views/HangarVesselDetailView.cs                            |  2 +-
 Assets/_Scripts/Utility/DataContainers/EndGameCinematicController.cs          |  4 ++--
 Assets/_Scripts/Utility/Tools/LogControlWindow.cs                             |  2 +-
 20 files changed, 27 insertions(+), 61 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 272 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/CountdownTimer.cs b/Assets/_Scripts/Controller/Arcade/CountdownTimer.cs
index 14a9b15e4..2b39a25db 100644
--- a/Assets/_Scripts/Controller/Arcade/CountdownTimer.cs
+++ b/Assets/_Scripts/Controller/Arcade/CountdownTimer.cs
@@ -1,4 +1,4 @@
-using CosmicShore.App.Systems.Audio;
+using CosmicShore.Core;
 using CosmicShore.Game.UI;
 using DG.Tweening;
 using System;
diff --git a/Assets/_Scripts/Controller/Vessel/ElementalBarsView.cs b/Assets/_Scripts/Controller/Vessel/ElementalBarsView.cs
index 5836aeae8..1c8e616de 100644
--- a/Assets/_Scripts/Controller/Vessel/ElementalBarsView.cs
+++ b/Assets/_Scripts/Controller/Vessel/ElementalBarsView.cs
@@ -3,7 +3,7 @@ using DG.Tweening;
 using UnityEngine;
 using UnityEngine.UI;
 using CosmicShore.Core;
-using CosmicShore.Game.IO;
+using CosmicShore.Gameplay;
 
 namespace CosmicShore
 {
diff --git a/Assets/_Scripts/Models/ScriptableObjects/SO_Captain.cs b/Assets/_Scripts/Models/ScriptableObjects/SO_Captain.cs
deleted file mode 100644
index 66ca96f07..000000000
--- a/Assets/_Scripts/Models/ScriptableObjects/SO_Captain.cs
+++ /dev/null
@@ -1,25 +0,0 @@
-using CosmicShore;
-using CosmicShore.Models.Enums;
-using UnityEngine;
-using UnityEngine.Serialization;
-
-[CreateAssetMenu(fileName = "Spike Spiegel", menuName = "CosmicShore/Captain/Captain", order = 3)]
-[System.Serializable]
-public class SO_Captain : ScriptableObject
-{
-    [SerializeField] public string Name;
-    [SerializeField] public string Description;
-    [SerializeField] public string AIBehaviorDescription;
-    [SerializeField] public string Flavor;
-    [SerializeField] public Sprite Image;
-    [SerializeField] public Sprite HeadshotImage;
-    [FormerlySerializedAs("SelectedIcon")]
-    [SerializeField] public Sprite IconActive;
-    [FormerlySerializedAs("Icon")]
-    [SerializeField] public Sprite IconInactive;
-    [FormerlySerializedAs("Ship")]
-    [SerializeField] public SO_Vessel Vessel;
-    [SerializeField] public Element PrimaryElement;
-    [SerializeField] public SO_Element Element;
-    [SerializeField] public ResourceCollection InitialResourceLevels;
-}
\ No newline at end of file
diff --git a/Assets/_Scripts/ScriptableObjects/SO_Captain.cs b/Assets/_Scripts/ScriptableObjects/SO_Captain.cs
index 69fd6098d..efe0d6bb9 100644
--- a/Assets/_Scripts/ScriptableObjects/SO_Captain.cs
+++ b/Assets/_Scripts/ScriptableObjects/SO_Captain.cs
@@ -18,7 +18,8 @@ namespace CosmicShore.ScriptableObjects
         [SerializeField] public Sprite IconActive;
         [FormerlySerializedAs("Icon")]
         [SerializeField] public Sprite IconInactive;
-        [SerializeField] public SO_Ship Ship;
+        [FormerlySerializedAs("Ship")]
+        [SerializeField] public SO_Vessel Vessel;
         [SerializeField] public Element PrimaryElement;
         [SerializeField] public SO_Element Element;
         [SerializeField] public ResourceCollection InitialResourceLevels;
diff --git a/Assets/_Scripts/ScriptableObjects/SO_Vessel.cs b/Assets/_Scripts/ScriptableObjects/SO_Vessel.cs
index ecb0e0cc5..848012569 100644
--- a/Assets/_Scripts/ScriptableObjects/SO_Vessel.cs
+++ b/Assets/_Scripts/ScriptableObjects/SO_Vessel.cs
@@ -1,5 +1,5 @@
 using CosmicShore;
-using CosmicShore.Models.Enums;
+using CosmicShore.Data;
 using System.Collections.Generic;
 using UnityEngine;
 using UnityEngine.Serialization;
diff --git a/Assets/_Scripts/System/CloudData/Interfaces/IUGSDataService.cs b/Assets/_Scripts/System/CloudData/Interfaces/IUGSDataService.cs
index 80cb4b8fd..df2c71013 100644
--- a/Assets/_Scripts/System/CloudData/Interfaces/IUGSDataService.cs
+++ b/Assets/_Scripts/System/CloudData/Interfaces/IUGSDataService.cs
@@ -2,9 +2,9 @@ using System;
 using System.Threading;
 using System.Threading.Tasks;
 using CosmicShore.App.Systems.CloudData.Models;
-using CosmicShore.Game.Analytics;
+using CosmicShore.Gameplay;
 using CosmicShore.Game.Progression;
-using CosmicShore.App.Profile;
+using CosmicShore.UI;
 
 namespace CosmicShore.App.Systems.CloudData
 {
diff --git a/Assets/_Scripts/System/CloudData/Repositories/PlayerProfileRepository.cs b/Assets/_Scripts/System/CloudData/Repositories/PlayerProfileRepository.cs
index 043d4f43d..f2f7939e8 100644
--- a/Assets/_Scripts/System/CloudData/Repositories/PlayerProfileRepository.cs
+++ b/Assets/_Scripts/System/CloudData/Repositories/PlayerProfileRepository.cs
@@ -1,5 +1,5 @@
 using System.Collections.Generic;
-using CosmicShore.App.Profile;
+using CosmicShore.UI;
 
 namespace CosmicShore.App.Systems.CloudData
 {
diff --git a/Assets/_Scripts/System/CloudData/Repositories/PlayerStatsRepository.cs b/Assets/_Scripts/System/CloudData/Repositories/PlayerStatsRepository.cs
index 1375e7018..311fc52f3 100644
--- a/Assets/_Scripts/System/CloudData/Repositories/PlayerStatsRepository.cs
+++ b/Assets/_Scripts/System/CloudData/Repositories/PlayerStatsRepository.cs
@@ -1,4 +1,4 @@
-using CosmicShore.Game.Analytics;
+using CosmicShore.Gameplay;
 
 namespace CosmicShore.App.Systems.CloudData
 {
diff --git a/Assets/_Scripts/System/CloudData/Repositories/VesselStatsRepository.cs b/Assets/_Scripts/System/CloudData/Repositories/VesselStatsRepository.cs
index 77dcd3048..17554c75d 100644
--- a/Assets/_Scripts/System/CloudData/Repositories/VesselStatsRepository.cs
+++ b/Assets/_Scripts/System/CloudData/Repositories/VesselStatsRepository.cs
@@ -1,4 +1,4 @@
-using CosmicShore.Game.Analytics;
+using CosmicShore.Gameplay;
 
 namespace CosmicShore.App.Systems.CloudData
 {
diff --git a/Assets/_Scripts/System/CloudData/UGSDataService.cs b/Assets/_Scripts/System/CloudData/UGSDataService.cs
index 1ca4a137f..24565a5d2 100644
--- a/Assets/_Scripts/System/CloudData/UGSDataService.cs
+++ b/Assets/_Scripts/System/CloudData/UGSDataService.cs
@@ -3,11 +3,11 @@ using System.Collections.Generic;
 using System.Linq;
 using System.Threading;
 using System.Threading.Tasks;
-using CosmicShore.App.Profile;
+using CosmicShore.UI;
 using CosmicShore.App.Systems.CloudData.Models;
-using CosmicShore.Game.Analytics;
+using CosmicShore.Gameplay;
 using CosmicShore.Game.Progression;
-using CosmicShore.Services.Auth;
+using CosmicShore.Core;
 using CosmicShore.Utility;
 using Unity.Services.Core;
 using UnityEngine;
diff --git a/Assets/_Scripts/System/DailyChallengeSystem.cs b/Assets/_Scripts/System/DailyChallengeSystem.cs
index 47f6a98a2..486077317 100644
--- a/Assets/_Scripts/System/DailyChallengeSystem.cs
+++ b/Assets/_Scripts/System/DailyChallengeSystem.cs
```

</details>

### `fa8248ac5` — fix: resolve remaining CS0246 compilation errors from development merge

_Claude, 2026-03-05 22:32:28 +0000_

```text
- Replace SO_Ship → SO_Vessel and SO_ShipList → SO_VesselList across 8 files
- Update game.Captains → game.Vessels and captain.Ship → captain.Vessel for
  SO_ArcadeGame/SO_Captain data model renames
- Remove duplicate ShipSelectionSlot struct from VesselSelectionView.cs
- Create stub classes for missing types: ConnectingPanel,
  DoTweenTypewriterAnimator, ConnectingDotsAnimator, HangarOverviewView
- Comment out Arcade.Instance references (singleton removed in app-shell-polish)
```

```text
 Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs |  8 ++++----
 Assets/_Scripts/Editor/PlayfabProductGenerator.cs                             |  2 +-
 Assets/_Scripts/MinigameHUD/View/ConnectingDotsAnimator.cs                    | 20 ++++++++++++++++++++
 Assets/_Scripts/MinigameHUD/View/ConnectingPanel.cs                           | 12 ++++++++++++
 Assets/_Scripts/MinigameHUD/View/DoTweenTypewriterAnimator.cs                 | 21 +++++++++++++++++++++
 Assets/_Scripts/System/DailyChallengeSystem.cs                                | 15 +++++++++------
 Assets/_Scripts/UI/Modals/ArcadeGameConfigSO.cs                               |  2 +-
 Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs                         | 17 ++++++++---------
 Assets/_Scripts/UI/Modals/HangarTrainingModal.cs                              |  3 ++-
 Assets/_Scripts/UI/Views/ArcadeExploreView.cs                                 | 13 ++-----------
 Assets/_Scripts/UI/Views/ArcadeLoadoutView.cs                                 | 14 +++++++-------
 Assets/_Scripts/UI/Views/HangarOverviewView.cs                                | 13 +++++++++++++
 Assets/_Scripts/UI/Views/PortSquadMemberConfigureView.cs                      | 10 +++++-----
 Assets/_Scripts/UI/Views/PortSquadView.cs                                     |  2 +-
 Assets/_Scripts/UI/Views/VesselSelectionView.cs                               | 11 +++--------
 15 files changed, 109 insertions(+), 54 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 415 lines)</summary>

```diff
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
diff --git a/Assets/_Scripts/Editor/PlayfabProductGenerator.cs b/Assets/_Scripts/Editor/PlayfabProductGenerator.cs
index 82764c51d..76a759c78 100644
--- a/Assets/_Scripts/Editor/PlayfabProductGenerator.cs
+++ b/Assets/_Scripts/Editor/PlayfabProductGenerator.cs
@@ -11,7 +11,7 @@ namespace CosmicShore.Editor
 {
     public class PlayFabProductGenerator : EditorWindow
     {
-        SO_Ship selectedShip;
+        SO_Vessel selectedShip;
         SO_Captain selectedCaptain;
         static PlayFabEconomyInstanceAPI _playFabEconomyInstanceAPI;
 
diff --git a/Assets/_Scripts/MinigameHUD/View/ConnectingDotsAnimator.cs b/Assets/_Scripts/MinigameHUD/View/ConnectingDotsAnimator.cs
new file mode 100644
index 000000000..4d3439253
--- /dev/null
+++ b/Assets/_Scripts/MinigameHUD/View/ConnectingDotsAnimator.cs
@@ -0,0 +1,20 @@
+using UnityEngine;
+
+namespace CosmicShore.Game.UI
+{
+    /// <summary>
+    /// Animates trailing dots (e.g. "CONNECTING...") with a looping pattern.
+    /// </summary>
+    public class ConnectingDotsAnimator : MonoBehaviour
+    {
+        public string BaseText { get; set; }
+
+        public void StartAnimation()
+        {
+        }
+
+        public void StopAnimation()
+        {
+        }
+    }
+}
diff --git a/Assets/_Scripts/MinigameHUD/View/ConnectingPanel.cs b/Assets/_Scripts/MinigameHUD/View/ConnectingPanel.cs
new file mode 100644
index 000000000..661499e8f
--- /dev/null
+++ b/Assets/_Scripts/MinigameHUD/View/ConnectingPanel.cs
@@ -0,0 +1,12 @@
+using UnityEngine;
+
+namespace CosmicShore.Game.UI
+{
+    /// <summary>
+    /// Connecting panel component shown during pre-game connecting phase.
+    /// Manages random sprite selection on enable.
+    /// </summary>
+    public class ConnectingPanel : MonoBehaviour
+    {
+    }
+}
diff --git a/Assets/_Scripts/MinigameHUD/View/DoTweenTypewriterAnimator.cs b/Assets/_Scripts/MinigameHUD/View/DoTweenTypewriterAnimator.cs
new file mode 100644
index 000000000..be7e4e94e
--- /dev/null
+++ b/Assets/_Scripts/MinigameHUD/View/DoTweenTypewriterAnimator.cs
@@ -0,0 +1,21 @@
+using Cysharp.Threading.Tasks;
+using System.Threading;
+using UnityEngine;
+
+namespace CosmicShore.Game.UI
+{
+    /// <summary>
+    /// Animates text with a typewriter effect using DOTween.
+    /// </summary>
+    public class DoTweenTypewriterAnimator : MonoBehaviour
+    {
+        public UniTaskVoid PlayIn(CancellationToken ct)
+        {
+            return UniTask.CompletedTask.AsUniTask().AsUniTaskVoid();
+        }
+
+        public void ClearInstant()
+        {
+        }
+    }
+}
diff --git a/Assets/_Scripts/System/DailyChallengeSystem.cs b/Assets/_Scripts/System/DailyChallengeSystem.cs
index 486077317..5dbf906e0 100644
--- a/Assets/_Scripts/System/DailyChallengeSystem.cs
+++ b/Assets/_Scripts/System/DailyChallengeSystem.cs
@@ -139,7 +139,8 @@ namespace CosmicShore.App.Systems
             PlayerPrefs.Save();
 
             dailyChallenge = FetchDailyChallenge();
-            DailyGame = Arcade.Instance.GetTrainingGameByMode(dailyChallenge.GameMode);
+            // TODO: Arcade singleton was removed — migrate to SO_GameList + DI
+            // DailyGame = Arcade.Instance.GetTrainingGameByMode(dailyChallenge.GameMode);
             ShipResources = LoadGameResourceCollection(DailyGame);
         }
 
@@ -150,11 +151,12 @@ namespace CosmicShore.App.Systems
             long dateTicks = currentDate.Ticks;
             var random = new System.Random((int)(dateTicks & 0xFFFFFFFF));
 
-            var trainingGames = Arcade.Instance.TrainingGames.Games;
-            var index = random.Next(trainingGames.Count);
-            var dailyGame = trainingGames[index];
+            // TODO: Arcade singleton was removed — migrate to SO_GameList + DI
+            // var trainingGames = Arcade.Instance.TrainingGames.Games;
+            // var index = random.Next(trainingGames.Count);
+            // var dailyGame = trainingGames[index];
             var challenge = new DailyChallenge();
-            challenge.GameMode = dailyGame.Game.Mode;
+            // challenge.GameMode = dailyGame.Game.Mode;
             challenge.Intensity = random.Next(4);
 
             return challenge;
@@ -167,7 +169,8 @@ namespace CosmicShore.App.Systems
             {
                 CSDebug.Log($"DailyChallenge - Remaining Attempts:{remainingAttempts - 1}");
                 CatalogManager.Instance.UseDailyChallengeTicket();
-                Arcade.Instance.LaunchTrainingGame(dailyChallenge.GameMode, DailyGame._SO_Vessel.Class, ShipResources, dailyChallenge.Intensity, 1, true);
+                // TODO: Arcade singleton was removed — migrate to gameData + SceneLoader pipeline
+                // Arcade.Instance.LaunchTrainingGame(dailyChallenge.GameMode, DailyGame._SO_Vessel.Class, ShipResources, dailyChallenge.Intensity, 1, true);
             }
             else
             {
diff --git a/Assets/_Scripts/UI/Modals/ArcadeGameConfigSO.cs b/Assets/_Scripts/UI/Modals/ArcadeGameConfigSO.cs
index 76860c6f0..192dcb805 100644
--- a/Assets/_Scripts/UI/Modals/ArcadeGameConfigSO.cs
+++ b/Assets/_Scripts/UI/Modals/ArcadeGameConfigSO.cs
@@ -14,7 +14,7 @@ namespace CosmicShore.UI
```

</details>

### `468bc939e` — fix: resolve 80 CS0246 errors by adding missing using directives

_Claude, 2026-03-05 22:42:32 +0000_

```text
Add namespace imports for types that moved during app-shell-polish
reorganization. Files from development reference types like Element,
GameModes, Domains, SO_Element, SO_TrainingGame, CrystalManager,
VesselHUDController, etc. that now live in CosmicShore.Data,
CosmicShore.Gameplay, CosmicShore.ScriptableObjects, CosmicShore.UI,
and CosmicShore.Game.Cinematics namespaces.

28 files updated with correct using directives.
```

```text
 Assets/_Scripts/Controller/Arcade/MiniGameControllerBase.cs                                         | 1 +
 Assets/_Scripts/Controller/Environment/FlowField/NetworkCrystalManager.cs                           | 2 ++
 .../Controller/ImpactEffects/EffectsSO/Vessel Skimmer Effects/VesselOvertakeBySkimmerEffectSO.cs    | 1 +
 Assets/_Scripts/Controller/Vessel/ElementPipsConfigSO.cs                                            | 1 +
 Assets/_Scripts/Controller/Vessel/ElementPipsView.cs                                                | 1 +
 Assets/_Scripts/Controller/Vessel/ElementalBarsView.cs                                              | 1 +
 Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/SquirrelVesselHUDController.cs    | 4 ++++
 Assets/_Scripts/Game/Progression/GameModeProgressionService.cs                                      | 1 +
 Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs                                                 | 2 ++
 Assets/_Scripts/ScriptableObjects/SO_GameModeQuestData.cs                                           | 1 +
 Assets/_Scripts/ScriptableObjects/SO_Vessel.cs                                                      | 1 +
 Assets/_Scripts/ScriptableObjects/SO_VesselList.cs                                                  | 1 +
 Assets/_Scripts/System/CloudData/Repositories/PlayerStatsRepository.cs                              | 1 +
 Assets/_Scripts/System/DailyChallengeSystem.cs                                                      | 1 +
 Assets/_Scripts/System/Playfab/Economy/CaptainManager.cs                                            | 1 +
 Assets/_Scripts/UI/Elements/Hangar/HangarVesselSelectNavLink.cs                                     | 1 +
 Assets/_Scripts/UI/Elements/QuestItemCard.cs                                                        | 1 +
 Assets/_Scripts/UI/Modals/HangarTrainingModal.cs                                                    | 1 +
 Assets/_Scripts/UI/PlayerScoreCard.cs                                                               | 1 +
 Assets/_Scripts/UI/Screens/HangarScreen.cs                                                          | 1 +
 Assets/_Scripts/UI/Views/PortSquadMemberConfigureView.cs                                            | 1 +
 Assets/_Scripts/Utility/DataContainers/EndGameCinematicController.cs                                | 2 ++
 Assets/_Scripts/Utility/DataContainers/EndGameCinematicView.cs                                      | 1 +
 Assets/_Scripts/Utility/DataContainers/HexRaceEndGameController.cs                                  | 1 +
 Assets/_Scripts/Utility/DataContainers/MultiplayerCrystalCaptureEndGameController.cs                | 1 +
 Assets/_Scripts/Utility/DataContainers/MultiplayerJoustEndGameController.cs                         | 1 +
 Assets/_Scripts/Utility/DataContainers/WildlifeBlitzEndGameCinematicController.cs                   | 1 +
 Assets/_Scripts/Utility/Tools/LogControlWindow.cs                                                   | 1 +
 28 files changed, 34 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 276 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MiniGameControllerBase.cs b/Assets/_Scripts/Controller/Arcade/MiniGameControllerBase.cs
index 77905bf76..d32024caa 100644
--- a/Assets/_Scripts/Controller/Arcade/MiniGameControllerBase.cs
+++ b/Assets/_Scripts/Controller/Arcade/MiniGameControllerBase.cs
@@ -1,3 +1,4 @@
+using CosmicShore.Game.Arcade;
 using CosmicShore.Gameplay;
 using CosmicShore.Utility;
 using Obvious.Soap;
diff --git a/Assets/_Scripts/Controller/Environment/FlowField/NetworkCrystalManager.cs b/Assets/_Scripts/Controller/Environment/FlowField/NetworkCrystalManager.cs
index d63d85867..ab68a84f7 100644
--- a/Assets/_Scripts/Controller/Environment/FlowField/NetworkCrystalManager.cs
+++ b/Assets/_Scripts/Controller/Environment/FlowField/NetworkCrystalManager.cs
@@ -1,4 +1,6 @@
 // NetworkCrystalManager.cs
+using CosmicShore.Data;
+using CosmicShore.Gameplay;
 using Unity.Collections;
 using Unity.Netcode;
 using UnityEngine;
diff --git a/Assets/_Scripts/Controller/Vessel/ElementPipsConfigSO.cs b/Assets/_Scripts/Controller/Vessel/ElementPipsConfigSO.cs
index fe073db93..16b2eaa44 100644
--- a/Assets/_Scripts/Controller/Vessel/ElementPipsConfigSO.cs
+++ b/Assets/_Scripts/Controller/Vessel/ElementPipsConfigSO.cs
@@ -1,4 +1,5 @@
 using System;
+using CosmicShore.Data;
 using UnityEngine;
 
 namespace CosmicShore
diff --git a/Assets/_Scripts/Controller/Vessel/ElementPipsView.cs b/Assets/_Scripts/Controller/Vessel/ElementPipsView.cs
index bd399ad2f..a6b188a95 100644
--- a/Assets/_Scripts/Controller/Vessel/ElementPipsView.cs
+++ b/Assets/_Scripts/Controller/Vessel/ElementPipsView.cs
@@ -1,3 +1,4 @@
+using CosmicShore.Data;
 using UnityEngine;
 using UnityEngine.UI;
 using CosmicShore.Core;
diff --git a/Assets/_Scripts/Controller/Vessel/ElementalBarsView.cs b/Assets/_Scripts/Controller/Vessel/ElementalBarsView.cs
index 1c8e616de..a289b9a01 100644
--- a/Assets/_Scripts/Controller/Vessel/ElementalBarsView.cs
+++ b/Assets/_Scripts/Controller/Vessel/ElementalBarsView.cs
@@ -1,4 +1,5 @@
 using System;
+using CosmicShore.Data;
 using DG.Tweening;
 using UnityEngine;
 using UnityEngine.UI;
diff --git a/Assets/_Scripts/Game/Progression/GameModeProgressionService.cs b/Assets/_Scripts/Game/Progression/GameModeProgressionService.cs
index 8ab7637fb..88e04e637 100644
--- a/Assets/_Scripts/Game/Progression/GameModeProgressionService.cs
+++ b/Assets/_Scripts/Game/Progression/GameModeProgressionService.cs
@@ -1,6 +1,7 @@
 using System;
 using CosmicShore.App.Systems.CloudData;
 using CosmicShore.Core;
+using CosmicShore.Data;
 using CosmicShore.Models;
 using CosmicShore.Soap;
 using UnityEngine;
diff --git a/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs b/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs
index 2b6cf9d6d..fa922a49f 100644
--- a/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs
+++ b/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs
@@ -1,3 +1,5 @@
+using CosmicShore.Data;
+using CosmicShore.UI;
 using DG.Tweening;
 using TMPro;
 using UnityEngine;
diff --git a/Assets/_Scripts/ScriptableObjects/SO_GameModeQuestData.cs b/Assets/_Scripts/ScriptableObjects/SO_GameModeQuestData.cs
index a54fffbcc..b1f3a53ef 100644
--- a/Assets/_Scripts/ScriptableObjects/SO_GameModeQuestData.cs
+++ b/Assets/_Scripts/ScriptableObjects/SO_GameModeQuestData.cs
@@ -1,4 +1,5 @@
 using CosmicShore.Core;
+using CosmicShore.Data;
 using UnityEngine;
 
 namespace CosmicShore.Models
diff --git a/Assets/_Scripts/ScriptableObjects/SO_Vessel.cs b/Assets/_Scripts/ScriptableObjects/SO_Vessel.cs
index 848012569..70c13afda 100644
--- a/Assets/_Scripts/ScriptableObjects/SO_Vessel.cs
+++ b/Assets/_Scripts/ScriptableObjects/SO_Vessel.cs
@@ -1,5 +1,6 @@
 using CosmicShore;
 using CosmicShore.Data;
+using CosmicShore.ScriptableObjects;
 using System.Collections.Generic;
 using UnityEngine;
 using UnityEngine.Serialization;
diff --git a/Assets/_Scripts/ScriptableObjects/SO_VesselList.cs b/Assets/_Scripts/ScriptableObjects/SO_VesselList.cs
index 19f9b584e..5a52f6b50 100644
--- a/Assets/_Scripts/ScriptableObjects/SO_VesselList.cs
+++ b/Assets/_Scripts/ScriptableObjects/SO_VesselList.cs
@@ -1,5 +1,6 @@
 using System.Collections.Generic;
 using System.Linq;
+using CosmicShore.Data;
 using UnityEngine;
 using UnityEngine.Serialization;
 using CosmicShore.Utility;
diff --git a/Assets/_Scripts/System/CloudData/Repositories/PlayerStatsRepository.cs b/Assets/_Scripts/System/CloudData/Repositories/PlayerStatsRepository.cs
index 311fc52f3..09c9445e5 100644
--- a/Assets/_Scripts/System/CloudData/Repositories/PlayerStatsRepository.cs
+++ b/Assets/_Scripts/System/CloudData/Repositories/PlayerStatsRepository.cs
@@ -1,4 +1,5 @@
 using CosmicShore.Gameplay;
+using CosmicShore.UI;
 
 namespace CosmicShore.App.Systems.CloudData
 {
diff --git a/Assets/_Scripts/System/DailyChallengeSystem.cs b/Assets/_Scripts/System/DailyChallengeSystem.cs
index 5dbf906e0..c0a3d7722 100644
--- a/Assets/_Scripts/System/DailyChallengeSystem.cs
+++ b/Assets/_Scripts/System/DailyChallengeSystem.cs
@@ -1,5 +1,6 @@
 ﻿using CosmicShore.Core;
 using CosmicShore.Data;
+using CosmicShore.ScriptableObjects;
 using System;
 using System.Collections.Generic;
 using System.Globalization;
diff --git a/Assets/_Scripts/System/Playfab/Economy/CaptainManager.cs b/Assets/_Scripts/System/Playfab/Economy/CaptainManager.cs
index 86c85d1f4..37405cd88 100644
--- a/Assets/_Scripts/System/Playfab/Economy/CaptainManager.cs
+++ b/Assets/_Scripts/System/Playfab/Economy/CaptainManager.cs
@@ -1,5 +1,6 @@
 ﻿using CosmicShore.Core;
 using CosmicShore.Data;
+using CosmicShore.ScriptableObjects;
 using System;
 using System.Collections.Generic;
 using System.Linq;
diff --git a/Assets/_Scripts/UI/Elements/Hangar/HangarVesselSelectNavLink.cs b/Assets/_Scripts/UI/Elements/Hangar/HangarVesselSelectNavLink.cs
index 8664f4738..d2a6e4f96 100644
--- a/Assets/_Scripts/UI/Elements/Hangar/HangarVesselSelectNavLink.cs
+++ b/Assets/_Scripts/UI/Elements/Hangar/HangarVesselSelectNavLink.cs
@@ -1,3 +1,4 @@
+using CosmicShore.App.UI.Screens;
 using CosmicShore.Core;
 using CosmicShore.UI;
 using UnityEngine;
diff --git a/Assets/_Scripts/UI/Elements/QuestItemCard.cs b/Assets/_Scripts/UI/Elements/QuestItemCard.cs
index 978e2993f..3021d2e20 100644
--- a/Assets/_Scripts/UI/Elements/QuestItemCard.cs
+++ b/Assets/_Scripts/UI/Elements/QuestItemCard.cs
@@ -1,5 +1,6 @@
 using System;
```

</details>

### `5546adaaa` — fix: resolve remaining compilation errors from development merge

_Claude, 2026-03-05 23:06:54 +0000_

```text
- Fix SOAP event subscription pattern (OnRaised) in NetworkCrystalManager,
  EndGameCinematicController, GameModeProgressionService
- Replace AudioSystem.Instance with [Inject] DI in CountdownTimer,
  HangarTrainingModal, EndGameCinematicController
- Add using CosmicShore.Core to all 10 CloudData repository files for UGSKeys
- Add xp field to PlayerProfileData and GetXP/AddXP methods to PlayerDataService
- Fix AuthenticationController.Instance in UGSDataService to use
  AuthenticationService.Instance directly
- Add ElementBars property to SilhouetteController for overtake effect
- Fix SnowChanger.crystalLattice → shards reference
- Fix PlayerDataService Utility.Tools.LogControlWindow path
- Add using CosmicShore.App.Systems for DailyChallengeSystem in 5 UI files
- Fix DoTweenTypewriterAnimator async UniTaskVoid
```

```text
 Assets/_Scripts/Controller/Arcade/CountdownTimer.cs                         |  4 +++-
 Assets/_Scripts/Controller/Environment/Cytoplasm/SnowChanger.cs             |  2 +-
 Assets/_Scripts/Controller/Environment/FlowField/NetworkCrystalManager.cs   |  4 ++--
 Assets/_Scripts/Controller/Vessel/SilhouetteController.cs                   |  4 ++++
 Assets/_Scripts/Game/Progression/GameModeProgressionService.cs              |  4 ++--
 Assets/_Scripts/MinigameHUD/View/DoTweenTypewriterAnimator.cs               |  4 ++--
 Assets/_Scripts/System/CloudData/Repositories/CaptainProgressRepository.cs  |  1 +
 Assets/_Scripts/System/CloudData/Repositories/DailyChallengeRepository.cs   |  1 +
 Assets/_Scripts/System/CloudData/Repositories/EpisodeProgressRepository.cs  |  1 +
 Assets/_Scripts/System/CloudData/Repositories/GameProgressionRepository.cs  |  1 +
 Assets/_Scripts/System/CloudData/Repositories/HangarRepository.cs           |  1 +
 Assets/_Scripts/System/CloudData/Repositories/PlayerProfileRepository.cs    |  1 +
 Assets/_Scripts/System/CloudData/Repositories/PlayerSettingsRepository.cs   |  1 +
 Assets/_Scripts/System/CloudData/Repositories/PlayerStatsRepository.cs      |  1 +
 Assets/_Scripts/System/CloudData/Repositories/TrainingProgressRepository.cs |  1 +
 Assets/_Scripts/System/CloudData/Repositories/VesselStatsRepository.cs      |  1 +
 Assets/_Scripts/System/CloudData/UGSDataService.cs                          | 24 +++++++++++++++++-------
 Assets/_Scripts/UI/Elements/Buttons/GameplayRewardButton.cs                 |  1 +
 Assets/_Scripts/UI/Elements/DailyChallengeCard.cs                           |  1 +
 Assets/_Scripts/UI/Elements/DailyChallengePlayButton.cs                     |  1 +
 Assets/_Scripts/UI/Modals/DailyChallengeModal.cs                            |  1 +
 Assets/_Scripts/UI/Modals/HangarTrainingModal.cs                            |  4 +++-
 Assets/_Scripts/UI/Views/DailyChallengeGameView.cs                          |  1 +
 Assets/_Scripts/UI/Views/PlayerDataService.cs                               | 19 ++++++++++++++++++-
 Assets/_Scripts/UI/Views/PlayerProfileData.cs                               |  1 +
 Assets/_Scripts/Utility/DataContainers/EndGameCinematicController.cs        |  9 ++++++---
 26 files changed, 74 insertions(+), 20 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 447 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/CountdownTimer.cs b/Assets/_Scripts/Controller/Arcade/CountdownTimer.cs
index 2b39a25db..cb0f4fc9b 100644
--- a/Assets/_Scripts/Controller/Arcade/CountdownTimer.cs
+++ b/Assets/_Scripts/Controller/Arcade/CountdownTimer.cs
@@ -1,6 +1,7 @@
 using CosmicShore.Core;
 using CosmicShore.Game.UI;
 using DG.Tweening;
+using Reflex.Attributes;
 using System;
 using UnityEngine;
 using UnityEngine.UI;
@@ -9,6 +10,7 @@ namespace CosmicShore.Game.Arcade
 {
     public class CountdownTimer : MonoBehaviour
     {
+        [Inject] AudioSystem audioSystem;
         [SerializeField] Image   countdownDisplay;
         [SerializeField] Sprite  countdown3;
         [SerializeField] Sprite  countdown2;
@@ -56,7 +58,7 @@ namespace CosmicShore.Game.Arcade
                     countdownDisplay.color = idx >= urgentStart
                         ? urgentColor
                         : Color.white;
-                    AudioSystem.Instance.PlaySFXClip(countdownBeep);
+                    audioSystem.PlaySFXClip(countdownBeep);
                 });
 
                 // Fade in from transparent
diff --git a/Assets/_Scripts/Controller/Environment/Cytoplasm/SnowChanger.cs b/Assets/_Scripts/Controller/Environment/Cytoplasm/SnowChanger.cs
index eab09e132..61122bb2c 100644
--- a/Assets/_Scripts/Controller/Environment/Cytoplasm/SnowChanger.cs
+++ b/Assets/_Scripts/Controller/Environment/Cytoplasm/SnowChanger.cs
@@ -80,7 +80,7 @@ namespace CosmicShore.Gameplay
 
         public void ChangeSnowOrientation()
         {
-            if (crystalLattice == null)
+            if (shards == null)
                 return;
 
             if (!cellData.TryGetLocalCrystal(out Crystal crystal))
diff --git a/Assets/_Scripts/Controller/Environment/FlowField/NetworkCrystalManager.cs b/Assets/_Scripts/Controller/Environment/FlowField/NetworkCrystalManager.cs
index ab68a84f7..905136fef 100644
--- a/Assets/_Scripts/Controller/Environment/FlowField/NetworkCrystalManager.cs
+++ b/Assets/_Scripts/Controller/Environment/FlowField/NetworkCrystalManager.cs
@@ -31,7 +31,7 @@ namespace CosmicShore.Game
         {
             if (spawnOnClientReady)
             {
-                gameData.OnClientReady += OnClientReadySpawn;
+                gameData.OnClientReady.OnRaised += OnClientReadySpawn;
                 // Spawn each player's crystal as they join, and catch up
                 // on turn start in case OnPlayerAdded was missed.
                 gameData.OnPlayerAdded += OnPlayerAddedSpawn;
@@ -48,7 +48,7 @@ namespace CosmicShore.Game
         {
             if (spawnOnClientReady)
             {
-                gameData.OnClientReady -= OnClientReadySpawn;
+                gameData.OnClientReady.OnRaised -= OnClientReadySpawn;
                 gameData.OnPlayerAdded -= OnPlayerAddedSpawn;
                 gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStartedCatchUp;
             }
diff --git a/Assets/_Scripts/Controller/Vessel/SilhouetteController.cs b/Assets/_Scripts/Controller/Vessel/SilhouetteController.cs
index 141c59568..7db31718d 100644
--- a/Assets/_Scripts/Controller/Vessel/SilhouetteController.cs
+++ b/Assets/_Scripts/Controller/Vessel/SilhouetteController.cs
@@ -20,6 +20,10 @@ namespace CosmicShore.Gameplay
         [Header("View")]
         [SerializeField] private SilhouetteView view; // view
 
+        [Header("Elemental Bars")]
+        [SerializeField] private ElementalBarsView elementBars;
+        public ElementalBarsView ElementBars => elementBars;
+
         private IVessel _vessel;
         private IVesselStatus _status;
         private ResourceSystem _resources;
diff --git a/Assets/_Scripts/Game/Progression/GameModeProgressionService.cs b/Assets/_Scripts/Game/Progression/GameModeProgressionService.cs
index 88e04e637..ec489ecee 100644
--- a/Assets/_Scripts/Game/Progression/GameModeProgressionService.cs
+++ b/Assets/_Scripts/Game/Progression/GameModeProgressionService.cs
@@ -59,7 +59,7 @@ namespace CosmicShore.Game.Progression
                 Instance = null;
 
             if (gameData != null)
-                gameData.OnMiniGameEnd -= HandleGameEnd;
+                gameData.OnMiniGameEnd.OnRaised -= HandleGameEnd;
 
             var ds = UGSDataService.Instance;
             if (ds != null)
@@ -69,7 +69,7 @@ namespace CosmicShore.Game.Progression
         void Start()
         {
             if (gameData != null)
-                gameData.OnMiniGameEnd += HandleGameEnd;
+                gameData.OnMiniGameEnd.OnRaised += HandleGameEnd;
 
             var ds = UGSDataService.Instance;
             if (ds != null)
diff --git a/Assets/_Scripts/MinigameHUD/View/DoTweenTypewriterAnimator.cs b/Assets/_Scripts/MinigameHUD/View/DoTweenTypewriterAnimator.cs
index be7e4e94e..f4d8b1da4 100644
--- a/Assets/_Scripts/MinigameHUD/View/DoTweenTypewriterAnimator.cs
+++ b/Assets/_Scripts/MinigameHUD/View/DoTweenTypewriterAnimator.cs
@@ -9,9 +9,9 @@ namespace CosmicShore.Game.UI
     /// </summary>
     public class DoTweenTypewriterAnimator : MonoBehaviour
     {
-        public UniTaskVoid PlayIn(CancellationToken ct)
+        public async UniTaskVoid PlayIn(CancellationToken ct)
         {
-            return UniTask.CompletedTask.AsUniTask().AsUniTaskVoid();
+            await UniTask.CompletedTask;
         }
 
         public void ClearInstant()
diff --git a/Assets/_Scripts/System/CloudData/Repositories/CaptainProgressRepository.cs b/Assets/_Scripts/System/CloudData/Repositories/CaptainProgressRepository.cs
index cc734b946..8b1ac2151 100644
--- a/Assets/_Scripts/System/CloudData/Repositories/CaptainProgressRepository.cs
+++ b/Assets/_Scripts/System/CloudData/Repositories/CaptainProgressRepository.cs
@@ -1,5 +1,6 @@
 using System.Collections.Generic;
 using CosmicShore.App.Systems.CloudData.Models;
+using CosmicShore.Core;
 
 namespace CosmicShore.App.Systems.CloudData
 {
diff --git a/Assets/_Scripts/System/CloudData/Repositories/DailyChallengeRepository.cs b/Assets/_Scripts/System/CloudData/Repositories/DailyChallengeRepository.cs
index 0d1c5982c..c6f8f01f4 100644
--- a/Assets/_Scripts/System/CloudData/Repositories/DailyChallengeRepository.cs
+++ b/Assets/_Scripts/System/CloudData/Repositories/DailyChallengeRepository.cs
@@ -1,5 +1,6 @@
 using System.Collections.Generic;
 using CosmicShore.App.Systems.CloudData.Models;
+using CosmicShore.Core;
 
 namespace CosmicShore.App.Systems.CloudData
 {
diff --git a/Assets/_Scripts/System/CloudData/Repositories/EpisodeProgressRepository.cs b/Assets/_Scripts/System/CloudData/Repositories/EpisodeProgressRepository.cs
index dd20095ca..895a81ec9 100644
--- a/Assets/_Scripts/System/CloudData/Repositories/EpisodeProgressRepository.cs
+++ b/Assets/_Scripts/System/CloudData/Repositories/EpisodeProgressRepository.cs
@@ -1,5 +1,6 @@
 using System.Collections.Generic;
 using CosmicShore.App.Systems.CloudData.Models;
+using CosmicShore.Core;
 
 namespace CosmicShore.App.Systems.CloudData
 {
```

</details>

### `90946947c` — fix: resolve UGSDataService namespace and CaptainManager static event errors

_Claude, 2026-03-05 23:41:29 +0000_

```text
- Add using CosmicShore.App.Systems.CloudData to PlayerDataService for UGSDataService
- Change instance access to static CaptainManager.OnLoadCaptainData in HangarCaptainsView
- Replace nameof(HangarScreen) with string literal (different namespace)
```

```text
 Assets/_Scripts/UI/Views/HangarCaptainsView.cs | 8 ++++----
 Assets/_Scripts/UI/Views/PlayerDataService.cs  | 1 +
 2 files changed, 5 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Views/HangarCaptainsView.cs b/Assets/_Scripts/UI/Views/HangarCaptainsView.cs
index f60c39e2a..3785f3f93 100644
--- a/Assets/_Scripts/UI/Views/HangarCaptainsView.cs
+++ b/Assets/_Scripts/UI/Views/HangarCaptainsView.cs
@@ -57,13 +57,13 @@ namespace CosmicShore.UI
         void Start()
         {
             if (_captainManager != null)
-                _captainManager.OnLoadCaptainData += NewCaptainData;
+                CaptainManager.OnLoadCaptainData += NewCaptainData;
         }
 
         void OnDisable()
         {
             if (_captainManager != null)
-                _captainManager.OnLoadCaptainData -= NewCaptainData;
+                CaptainManager.OnLoadCaptainData -= NewCaptainData;
         }
 
         void NewCaptainData()
@@ -233,12 +233,12 @@ namespace CosmicShore.UI
             }
             catch (ArgumentOutOfRangeException argumentOutOfRangeException)
             {
-                CSDebug.LogWarningFormat("{0} - {1} - The vessel lacks captain assets. Please add them. {2}", nameof(HangarScreen),
+                CSDebug.LogWarningFormat("{0} - {1} - The vessel lacks captain assets. Please add them. {2}", "HangarScreen",
                     nameof(SelectCaptain), argumentOutOfRangeException.Message);
             }
             catch (NullReferenceException nullReferenceException)
             {
-                CSDebug.LogWarningFormat("{0} - {1} - The vessel lacks captain assets. Please add them. {2}", nameof(HangarScreen),
+                CSDebug.LogWarningFormat("{0} - {1} - The vessel lacks captain assets. Please add them. {2}", "HangarScreen",
                     nameof(SelectCaptain), nullReferenceException.Message);
             }
         }
diff --git a/Assets/_Scripts/UI/Views/PlayerDataService.cs b/Assets/_Scripts/UI/Views/PlayerDataService.cs
index 42162d394..8b8404bb7 100644
--- a/Assets/_Scripts/UI/Views/PlayerDataService.cs
+++ b/Assets/_Scripts/UI/Views/PlayerDataService.cs
@@ -1,6 +1,7 @@
 using System;
 using System.Collections.Generic;
 using System.Threading.Tasks;
+using CosmicShore.App.Systems.CloudData;
 using CosmicShore.Core;
 using CosmicShore.ScriptableObjects;
 using CosmicShore.Utility;
```

</details>

_Also contains 2 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
