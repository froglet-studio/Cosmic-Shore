# Branch archive: `claude/add-party-game-mode-Go0YH`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

## What this branch is

**Party Game: five-round mini-game playlist mode**

Added a 'Party Game' mode: one session that runs five mini-games back to back (e.g. HexRace, Joust, Crystal Capture) with round tabs, a party scoreboard, a pause panel, ready-up lobby and per-round spawn points. It included a new MinigamePartyGame scene, a PartyGameController and config ScriptableObject, and many null-ref fixes to make existing mini-games run inside it. This is the later of two copies of the same work.

- **Status:** Abandoned experiment
- **Areas:** game modes, Party Game, mini-games, HUD/UI, multiplayer
- **Already in bleeding-edge:** None found: no PartyGameController, PartyPhase, PartyGameConfigSO or MinigamePartyGame scene in bleeding-edge, and no Party member in GameModes. (Bleeding-edge 'Party' code is the unrelated friends/invite lobby.) Continued further on claude/fix-party-game-mode-aALL3.
- **Risk if deleted:** low
- **Suggestion (2026-10-08):** can be deleted after archiving — Fully contained in claude/fix-party-game-mode-aALL3 (the most complete version), and the feature never shipped; archive it.

## Evidence

- **Last commit:** 2026-02-25 by Shombith03
- **Unmerged commits:** 35
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/121
- **Forked from:** `5b806e52b` (2026-02-25, Merge pull request #119 from froglet-studio/claude/fix-widescreen-ui-M7FWR)
- **Tip:** `1881410aa`
- **Files touched (61):**
  - `Assets/_Graphics/UniversalRenderPipelineGlobalSettings.asset`
  - `Assets/_Prefabs/Environment/RacingCellVariant.prefab`
  - `Assets/_Prefabs/Minigame.meta`
  - `Assets/_Prefabs/Minigame/CrystalCapture_Components.prefab`
  - `Assets/_Prefabs/Minigame/CrystalCapture_Components.prefab.meta`
  - `Assets/_Prefabs/Minigame/HexRace_Components.prefab`
  - `Assets/_Prefabs/Minigame/HexRace_Components.prefab.meta`
  - `Assets/_Prefabs/Minigame/Joust_Components.prefab`
  - `Assets/_Prefabs/Minigame/Joust_Components.prefab.meta`
  - `Assets/_Prefabs/UI Elements/PartyPausePanelDataPrefab.prefab`
  - `Assets/_Prefabs/UI Elements/PartyPausePanelDataPrefab.prefab.meta`
  - `Assets/_SO_Assets/Games/ArcadeGamePartyGame.asset`
  - `Assets/_SO_Assets/Games/ArcadeGamePartyGame.asset.meta`
  - `Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset`
  - `Assets/_SO_Assets/Games/PartyGameConfig.asset`
  - `Assets/_SO_Assets/Games/PartyGameConfig.asset.meta`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameCrystalCaptureMultiplayer_Gameplay.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameHexRace.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigamePartyGame.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigamePartyGame.unity.meta`
  - `Assets/_Scripts/App/UI/WidescreenLayoutAdapter.cs.meta`
  - `Assets/_Scripts/Game/AI/AIPilot.cs`
  - `Assets/_Scripts/Game/Arcade/HexRaceController.cs`
  - `Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs`
  - `Assets/_Scripts/Game/Arcade/MultiplayerCrystalCaptureController.cs`
  - `Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs`
  - `Assets/_Scripts/Game/Arcade/MultiplayerJoustController.cs`
  - `Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs`
  - `Assets/_Scripts/Game/Arcade/Party.meta`
  - `Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs`
  - `Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs.meta`
  - `Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs`
  - `Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs.meta`
  - `Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs`
  - `Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs.meta`
  - `Assets/_Scripts/Game/Arcade/Party/PartyPhase.cs`
  - `Assets/_Scripts/Game/Arcade/Party/PartyPhase.cs.meta`
  - `Assets/_Scripts/Game/Arcade/Party/PartyPlayerState.cs`
  - `Assets/_Scripts/Game/Arcade/Party/PartyPlayerState.cs.meta`
  - … and 21 more

### `8be4ccf32` — Add Party Game mode infrastructure with 5-round mini-game orchestration

_Claude, 2026-02-24 09:32:22 +0000_

```text
Introduces the complete code infrastructure for the Party Game mode:
- PartyGameController: NetworkBehaviour that orchestrates lobby, 5 randomized
  mini-game rounds (Crystal Capture, Hex Race, Joust), scoring, and final results.
  Manages player readiness, AI fill for missing players, round transitions,
  and network state sync via NetworkVariables and ClientRPCs.
- PartyPausePanel: Replaces normal pause panel during party mode with tabular
  scoreboard showing 5 round tabs, ready/quit buttons, and game state text.
- PartyPauseButton: On pause, hands vessel to AI and shows party panel instead
  of pausing the game (multiplayer-safe).
- PartyRoundTab: Prefab-friendly UI component for individual round display
  showing player names pre-round and scores post-round.
- PartyEndGameHandler: Overrides end-game cinematic to skip scoreboard between
  rounds, only showing XP on the final round.
- PartyScoreboard/PartyStatsProvider: Final scoreboard showing games-won stats.
- Data models: PartyGameConfigSO, PartyPhase, PartyRoundResult, PartyPlayerState.
- Adds PartyGame = 36 to GameModes enum.
```

```text
 Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs | 121 +++++++
 Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs   |  43 +++
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs | 769 +++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Game/Arcade/Party/PartyPhase.cs          |  29 ++
 Assets/_Scripts/Game/Arcade/Party/PartyPlayerState.cs    |  15 +
 Assets/_Scripts/Game/Arcade/Party/PartyRoundResult.cs    |  31 ++
 Assets/_Scripts/Game/UI/Party/PartyPauseButton.cs        |  98 ++++++
 Assets/_Scripts/Game/UI/Party/PartyPausePanel.cs         | 346 ++++++++++++++++++++
 Assets/_Scripts/Game/UI/Party/PartyRoundTab.cs           | 179 +++++++++++
 Assets/_Scripts/Game/UI/Party/PartyScoreboard.cs         |  62 ++++
 Assets/_Scripts/Game/UI/Party/PartyStatsProvider.cs      |  59 ++++
 Assets/_Scripts/Models/Enums/GameModes.cs                |   1 +
 12 files changed, 1753 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 1829 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs b/Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs
new file mode 100644
index 000000000..570085006
--- /dev/null
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs
@@ -0,0 +1,121 @@
+using CosmicShore.Game.Cinematics;
+using CosmicShore.Soap;
+using UnityEngine;
+using CosmicShore.Utility;
+
+namespace CosmicShore.Game.Arcade.Party
+{
+    /// <summary>
+    /// Overrides the end-game cinematic flow for party mode.
+    /// Runs the cinematic but skips the scoreboard — the party pause panel
+    /// handles result display instead.
+    /// At the final round, XP is shown in the cinematic.
+    /// </summary>
+    public class PartyEndGameHandler : EndGameCinematicController
+    {
+        [Header("Party")]
+        [SerializeField] PartyGameController partyController;
+
+        bool IsFinalRound => partyController != null &&
+                             partyController.CurrentRound + 1 >= partyController.TotalRounds;
+
+        protected override void OnWinnerCalculated()
+        {
+            if (isRunning) return;
+            isRunning = true;
+
+            // Only award XP on the final round
+            if (IsFinalRound)
+            {
+                var xpService = Game.XP.XPRewardService.Instance;
+                if (xpService != null)
+                {
+                    int xp = xpService.AwardXP();
+                    CSDebug.Log($"[PartyEndGame] Final round XP awarded: {xp}");
+                }
+            }
+
+            var localPlayer = gameData.LocalPlayer;
+            if (localPlayer?.Vessel?.VesselStatus != null)
+                cachedBoostMultiplier = localPlayer.Vessel.VesselStatus.BoostMultiplier;
+
+            var cinematic = ResolveCinematicForThisScene();
+            runningRoutine = StartCoroutine(RunPartyEndGameSequence(cinematic));
+        }
+
+        System.Collections.IEnumerator RunPartyEndGameSequence(CinematicDefinitionSO cinematic)
+        {
+            localPlayerWon = DetermineLocalPlayerWon();
+
+            // Run victory lap if configured
+            if (cinematic && cinematic.enableVictoryLap)
+                yield return StartCoroutine(RunVictoryLap(cinematic));
+
+            // Set local vessel to AI during cinematic
+            if (cinematic && cinematic.setLocalVesselToAI)
+                SetLocalVesselAI(true, cinematic.aiCinematicBehavior);
+
+            // Camera sequence
+            if (cinematic && cinematic.cameraSetups is { Count: > 0 })
+                yield return StartCoroutine(RunCameraSequence(cinematic));
+            else
+            {
+                var delay = cinematic ? cinematic.delayBeforeEndScreen : 0.1f;
+                yield return new WaitForSeconds(delay);
+            }
+
+            // Score reveal
+            yield return StartCoroutine(PlayScoreRevealSequence(cinematic));
+
+            // Only show XP on the final round
+            if (IsFinalRound && view)
+                view.ShowXPEarned();
+
+            // Skip the continue button and connecting panel for non-final rounds
```

</details>

### `de7c5f6bb` — Add Party Game scene, SO assets, and meta files

_Claude, 2026-02-24 09:52:32 +0000_

```text
- MinigamePartyGame.unity scene with PartyGameController,
  spawn origins, and 3 disabled mini-game environment stubs
  (CrystalCapture, HexRace, Joust)
- ArcadeGamePartyGame.asset (SO_ArcadeGame, Mode=36, 3 captains)
- PartyGameConfig.asset (5 rounds, 2-3 players, 120s lobby)
- Meta files for all 11 Party scripts and 2 directories
- Scene registered in EditorBuildSettings
```

```text
 Assets/_SO_Assets/Games/ArcadeGamePartyGame.asset              |  35 +++
 Assets/_SO_Assets/Games/ArcadeGamePartyGame.asset.meta         |   8 +
 Assets/_SO_Assets/Games/PartyGameConfig.asset                  |  25 ++
 Assets/_SO_Assets/Games/PartyGameConfig.asset.meta             |   8 +
 Assets/_Scenes/Multiplayer Scenes/MinigamePartyGame.unity      | 587 +++++++++++++++++++++++++++++++++++++++
 Assets/_Scenes/Multiplayer Scenes/MinigamePartyGame.unity.meta |   7 +
 Assets/_Scripts/Game/Arcade/Party.meta                         |   8 +
 Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs.meta  |  11 +
 Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs.meta    |  11 +
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs.meta  |  11 +
 Assets/_Scripts/Game/Arcade/Party/PartyPhase.cs.meta           |  11 +
 Assets/_Scripts/Game/Arcade/Party/PartyPlayerState.cs.meta     |  11 +
 Assets/_Scripts/Game/Arcade/Party/PartyRoundResult.cs.meta     |  11 +
 Assets/_Scripts/Game/UI/Party.meta                             |   8 +
 Assets/_Scripts/Game/UI/Party/PartyPauseButton.cs.meta         |  11 +
 Assets/_Scripts/Game/UI/Party/PartyPausePanel.cs.meta          |  11 +
 Assets/_Scripts/Game/UI/Party/PartyRoundTab.cs.meta            |  11 +
 Assets/_Scripts/Game/UI/Party/PartyScoreboard.cs.meta          |  11 +
 Assets/_Scripts/Game/UI/Party/PartyStatsProvider.cs.meta       |  11 +
 ProjectSettings/EditorBuildSettings.asset                      |   3 +
 20 files changed, 810 insertions(+)
```

### `245bdb106` — Rebuild party game scene with proper infrastructure and environments

_Claude, 2026-02-24 10:25:39 +0000_

```text
- Remove bare Main Camera and Directional Light
- Add Mini Game Main Camera prefab instance (matching reference scenes)
- Add GameCanvas prefab instance
- Add DependencySpawner prefab instance
- Add MultiplayerSetup prefab instance (wired to PartyGameController)
- Add NetworkStatsManager prefab instance
- Add EventSystem with InputSystemUIInputModule
- Add per-environment Spawners with SegmentSpawner configs:
  - Env_CrystalCapture: sphere spawn (R=250) with CC intensity segments
  - Env_HexRace: waypoint track with SpawnableWaypointTrack child
  - Env_Joust: sphere spawn (R=250) with Joust intensity segments
- Add PlayerOrigin per environment
```

```text
 Assets/_Scenes/Multiplayer Scenes/MinigamePartyGame.unity | 1009 +++++++++++++++++++++++++++++++++++++------
 1 file changed, 867 insertions(+), 142 deletions(-)
```

### `2f373cf73` — Create Party Game UI

_Shombith03, 2026-02-24 16:21:50 +0530_

```text
 Assets/_Prefabs/UI Elements/PartyPausePanelDataPrefab.prefab      | 2470 +++++++++++++++++++++++++++++++++++
 Assets/_Prefabs/UI Elements/PartyPausePanelDataPrefab.prefab.meta |    7 +
 Assets/_Scenes/Multiplayer Scenes/MinigamePartyGame.unity         | 1286 +++++++++++++++++-
 3 files changed, 3727 insertions(+), 36 deletions(-)
```

### `bdee72672` — Update PartyRoundTab and PartyPausePanel to match prefab structure

_Claude, 2026-02-24 11:08:53 +0000_

```text
- PartyRoundTab: Replace playerReadyIndicators (Image list) with single
  readyCountText (TMP_Text) matching PartyPausePanelDataPrefab layout.
  Add playerPositionTexts and playerProfileIcons for right-side scoreboard.
  Add winnerLabelText for the "WINNER :" label visibility toggle.

- PartyPausePanel: Add ScrollRect-based auto-scroll to active round tab.
  Scroll triggers on Show(), RoundResults phase, and UpdateRoundResult().
  Forward game state text updates to the active round tab.
  Track _activeRoundIndex for consistent round tab management.
```

```text
 Assets/_Scripts/Game/UI/Party/PartyPausePanel.cs |  78 ++++++++++++++++++++++++++++++++--
 Assets/_Scripts/Game/UI/Party/PartyRoundTab.cs   | 120 +++++++++++++++++++++++++++++++++++++----------------
 2 files changed, 160 insertions(+), 38 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 373 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/UI/Party/PartyPausePanel.cs b/Assets/_Scripts/Game/UI/Party/PartyPausePanel.cs
index 0437f2114..539ddcd7a 100644
--- a/Assets/_Scripts/Game/UI/Party/PartyPausePanel.cs
+++ b/Assets/_Scripts/Game/UI/Party/PartyPausePanel.cs
@@ -31,6 +31,7 @@ namespace CosmicShore.Game.UI.Party
         [Header("Round Tabs")]
         [SerializeField] Transform roundTabContainer;
         [SerializeField] PartyRoundTab roundTabPrefab;
+        [SerializeField] ScrollRect scrollRect;
 
         [Header("Buttons")]
         [SerializeField] Button readyButton;
@@ -47,6 +48,7 @@ namespace CosmicShore.Game.UI.Party
         readonly List<PartyRoundTab> _roundTabs = new();
         PartyPhase _currentPhase;
         bool _isVisible;
+        int _activeRoundIndex;
 
         #region Unity Lifecycle
 
@@ -100,6 +102,10 @@ namespace CosmicShore.Game.UI.Party
             if (finalResultsContainer)
                 finalResultsContainer.SetActive(false);
 
+            // Highlight the first round as active
+            _activeRoundIndex = 0;
+            SetActiveRound(0);
+
             SetReadyButtonInteractable(false);
         }
 
@@ -123,6 +129,9 @@ namespace CosmicShore.Game.UI.Party
 
             if (modalWindowManager)
                 modalWindowManager.ModalWindowIn();
+
+            // Auto-scroll to the current active round
+            ScrollToActiveRound();
         }
 
         public void Hide()
@@ -191,6 +200,7 @@ namespace CosmicShore.Game.UI.Party
                 case PartyPhase.RoundResults:
                     SetReadyButtonInteractable(true);
                     if (readyButtonText) readyButtonText.text = "READY";
+                    ScrollToActiveRound();
                     break;
 
                 case PartyPhase.FinalResults:
@@ -209,6 +219,10 @@ namespace CosmicShore.Game.UI.Party
         public void SetGameStateText(string text)
         {
             if (gameStateText) gameStateText.text = text;
+
+            // Also update the active round tab's game state text
+            if (_activeRoundIndex >= 0 && _activeRoundIndex < _roundTabs.Count)
+                _roundTabs[_activeRoundIndex].SetGameStateText(text);
         }
 
         #endregion
@@ -229,9 +243,8 @@ namespace CosmicShore.Game.UI.Party
         public void OnPlayerReadyChanged(string playerName, bool isReady)
         {
             // Update the current active round tab's ready indicator
-            int activeRound = GetActiveRoundIndex();
-            if (activeRound >= 0 && activeRound < _roundTabs.Count)
-                _roundTabs[activeRound].SetPlayerReady(playerName, isReady);
+            if (_activeRoundIndex >= 0 && _activeRoundIndex < _roundTabs.Count)
+                _roundTabs[_activeRoundIndex].SetPlayerReady(playerName, isReady);
         }
 
         #endregion
@@ -249,8 +262,13 @@ namespace CosmicShore.Game.UI.Party
 
             // Highlight the next round as active
             int nextRound = roundIndex + 1;
+            _activeRoundIndex = nextRound;
+
             for (int i = 0; i < _roundTabs.Count; i++)
                 _roundTabs[i].SetActive(i == nextRound);
```

</details>

### `7286d56d6` — Add References

_Shombith03, 2026-02-24 16:55:38 +0530_

```text
 Assets/_Prefabs/UI Elements/PartyPausePanelDataPrefab.prefab |  37 +++
 Assets/_Scenes/Multiplayer Scenes/MinigamePartyGame.unity    | 499 +++++++++++++++++++++++++++++------------
 2 files changed, 396 insertions(+), 140 deletions(-)
```

### `95fa0722b` — Refine party round flow and clean up UI references

_Claude, 2026-02-24 11:32:31 +0000_

```text
- Remove unused fields from PartyPausePanel (modalWindowManager, panelRoot,
  settingsButton, finalResults inline display)
- Implement end-of-round flow: cinematic → score reveal → continue → party
  pause panel with Ready button (mid-party) or Next button (final round)
- Next button on final round hides panel and triggers PartyScoreboard via
  gameData.InvokeShowGameEndScreen()
- PartyEndGameHandler now shows party pause panel after cinematic instead of
  PartyGameController doing it mid-cinematic
- Add PartyGame (ArcadeGamePartyGame) to OrganicRematchGames list
```

```text
 Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset |  1 +
 Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs    | 64 +++++++++++-------------------
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs    |  3 +-
 Assets/_Scripts/Game/UI/Party/PartyPausePanel.cs            | 90 ++++++++++++-------------------------------
 4 files changed, 48 insertions(+), 110 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 307 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs b/Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs
index 570085006..e1ef2b157 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs
@@ -1,4 +1,5 @@
 using CosmicShore.Game.Cinematics;
+using CosmicShore.Game.UI.Party;
 using CosmicShore.Soap;
 using UnityEngine;
 using CosmicShore.Utility;
@@ -7,14 +8,15 @@ namespace CosmicShore.Game.Arcade.Party
 {
     /// <summary>
     /// Overrides the end-game cinematic flow for party mode.
-    /// Runs the cinematic but skips the scoreboard — the party pause panel
-    /// handles result display instead.
-    /// At the final round, XP is shown in the cinematic.
+    /// After each round: cinematic → score reveal → party pause panel (Ready button).
+    /// After the final round: cinematic → score reveal → party pause panel (Next button → PartyScoreboard).
+    /// XP is only awarded on the final round.
     /// </summary>
     public class PartyEndGameHandler : EndGameCinematicController
     {
         [Header("Party")]
         [SerializeField] PartyGameController partyController;
+        [SerializeField] PartyPausePanel partyPausePanel;
 
         bool IsFinalRound => partyController != null &&
                              partyController.CurrentRound + 1 >= partyController.TotalRounds;
@@ -64,56 +66,34 @@ namespace CosmicShore.Game.Arcade.Party
                 yield return new WaitForSeconds(delay);
             }
 
-            // Score reveal
+            // Score reveal animation
             yield return StartCoroutine(PlayScoreRevealSequence(cinematic));
 
-            // Only show XP on the final round
+            // Show XP only on the final round
             if (IsFinalRound && view)
                 view.ShowXPEarned();
 
-            // Skip the continue button and connecting panel for non-final rounds
-            // The party controller handles the transition
-            if (!IsFinalRound)
+            // Show continue button, wait for tap
+            if (view)
             {
-                // Brief pause then reset
-                yield return new WaitForSeconds(1f);
-                ResetGameForNewRound();
-
-                if (view)
-                {
-                    view.HideXPEarned();
-                    view.HideScoreRevealPanel();
-                }
-
-                // Do NOT invoke OnShowGameEndScreen — the party controller
-                // handles showing the party panel instead of the normal scoreboard
+                view.ShowContinueButton();
+                yield return new WaitUntil(() => !view.IsContinueButtonActive());
             }
-            else
-            {
-                // Final round — show continue button and then the final scoreboard
-                if (view)
-                {
-                    view.ShowContinueButton();
-                    yield return new WaitUntil(() => !view.IsContinueButtonActive());
-                }
 
-                if (view && cinematic)
-                {
-                    view.ShowConnectingPanel();
-                    yield return new WaitForSeconds(cinematic.connectingPanelDuration);
-                    ResetGameForNewRound();
-                }
+            // Reset game state and clean up cinematic UI
+            ResetGameForNewRound();
```

</details>

### `38acd8bf5` — Add offline solo mode and auto-discover mini-game controllers

_Claude, 2026-02-24 11:45:55 +0000_

```text
- Remove manual miniGameControllers serialized list; controllers are now
  auto-discovered from environment GameObjects via GetComponent at spawn
- Add offlineMode toggle and offlineLobbyWaitSeconds (default 10s) to
  PartyGameController for solo play with AI opponents
- StartOfflineLobby: adds local player, fills with AI, waits 10s in lobby
  for atmosphere, then transitions to WaitingForReady
- Update PartyGameConfigSO MinPlayers default to 1 (allows solo play)
- Update PartyGameConfig.asset MinPlayers to 1
- Skip auto-ready logic in OnPlayerJoined during offline mode to prevent
  racing with the offline lobby timer
```

```text
 Assets/_SO_Assets/Games/PartyGameConfig.asset            |  2 +-
 Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs   |  4 +-
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs | 95 +++++++++++++++++++++++++++++++++++++++++++---
 3 files changed, 93 insertions(+), 8 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 167 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs
index 2fefe3268..72f553250 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs
@@ -9,8 +9,8 @@ namespace CosmicShore.Game.Arcade.Party
     public class PartyGameConfigSO : ScriptableObject
     {
         [Header("Players")]
-        [Tooltip("Minimum human players required to start (AI fills remaining slots).")]
-        [Range(1, 3)] public int MinPlayers = 2;
+        [Tooltip("Minimum human players required to start (AI fills remaining slots). Set to 1 for offline/solo mode.")]
+        [Range(1, 3)] public int MinPlayers = 1;
 
         [Tooltip("Maximum players in the party (human + AI).")]
         [Range(2, 3)] public int MaxPlayers = 3;
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index dc8dcd72a..4e88101d7 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -33,9 +33,12 @@ namespace CosmicShore.Game.Arcade.Party
         [Tooltip("Root GameObjects for each mini-game environment. Index must match AvailableMiniGames order in config.")]
         [SerializeField] List<GameObject> miniGameEnvironments = new();
 
-        [Header("Mini-Game Controllers")]
-        [Tooltip("Controllers for each mini-game. Must match AvailableMiniGames order in config.")]
-        [SerializeField] List<MiniGameControllerBase> miniGameControllers = new();
+        [Header("Offline Mode")]
+        [Tooltip("Enable to play solo with AI opponents (no network matchmaking required).")]
+        [SerializeField] bool offlineMode;
+
+        [Tooltip("Seconds to wait in lobby before auto-starting in offline mode.")]
+        [SerializeField] float offlineLobbyWaitSeconds = 10f;
 
         // --- Network state ---
         readonly NetworkVariable<int> _netCurrentRound = new(0);
@@ -47,6 +50,7 @@ namespace CosmicShore.Game.Arcade.Party
         readonly List<PartyRoundResult> _roundResults = new();
         readonly List<PartyPlayerState> _playerStates = new();
         readonly List<GameModes> _recentMiniGames = new();
+        readonly List<MiniGameControllerBase> _miniGameControllers = new();
         int _readyPlayerCount;
         CancellationTokenSource _lobbyCts;
         CancellationTokenSource _roundCts;
@@ -82,6 +86,9 @@ namespace CosmicShore.Game.Arcade.Party
             // Subscribe to player join events so we track who's in the party
             gameData.OnPlayerAdded += HandlePlayerAdded;
 
+            // Auto-discover mini-game controllers from environment GameObjects
+            DiscoverMiniGameControllers();
+
             // Initialize round results
             _roundResults.Clear();
             for (int i = 0; i < config.TotalRounds; i++)
@@ -95,12 +102,19 @@ namespace CosmicShore.Game.Arcade.Party
             {
                 _netLobbyStartTime.Value = Time.realtimeSinceStartup;
                 SetPhase(PartyPhase.Lobby);
-                StartLobbyTimer().Forget();
+
+                if (offlineMode)
+                    StartOfflineLobby().Forget();
+                else
+                    StartLobbyTimer().Forget();
             }
 
-            // Show party panel by default in lobby
+            // Initialize the party panel with round tabs
             if (partyPausePanel)
+            {
+                partyPausePanel.Initialize(config.TotalRounds, _playerStates);
                 partyPausePanel.Show();
+            }
         }
 
         public override void OnNetworkDespawn()
@@ -121,6 +135,29 @@ namespace CosmicShore.Game.Arcade.Party
             base.OnNetworkDespawn();
         }
 
+        /// <summary>
```

</details>

### `74e09e1eb` — Use gameData.IsMultiplayerMode for solo detection instead of manual checkbox

_Claude, 2026-02-24 11:52:43 +0000_

```text
Follows the same pattern as HexRace and other multiplayer games: when the
user selects 1 player in the arcade config modal, Arcade.LaunchArcadeGame
sets IsMultiplayerMode=false, MultiplayerSetup calls StartLocalHostForSoloPlay,
and ServerPlayerVesselInitializer spawns human + AI automatically.

- Remove offlineMode bool and offlineLobbyWaitSeconds serialized fields
- Add IsSoloWithAI property (derives from !gameData.IsMultiplayerMode)
- Rename StartOfflineLobby → StartSoloLobby, now waits for players to
  arrive via HandlePlayerAdded from ServerPlayerVesselInitializer
- Add SoloLobbyWaitSeconds (10s) to PartyGameConfigSO for the lobby timer
- Update PartyGameConfig.asset with SoloLobbyWaitSeconds = 10
```

```text
 Assets/_SO_Assets/Games/PartyGameConfig.asset            |  1 +
 Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs   |  5 ++++-
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs | 52 +++++++++++++++++++---------------------------
 3 files changed, 26 insertions(+), 32 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 122 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs
index 72f553250..dac9fe3f1 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs
@@ -28,9 +28,12 @@ namespace CosmicShore.Game.Arcade.Party
         };
 
         [Header("Timing")]
-        [Tooltip("Seconds to wait for players before filling with AI.")]
+        [Tooltip("Seconds to wait for players in online matchmaking before filling with AI.")]
         public float LobbyWaitTimeSeconds = 120f;
 
+        [Tooltip("Seconds to wait in lobby when playing solo (1 player). Gives the player time to see the lobby.")]
+        public float SoloLobbyWaitSeconds = 10f;
+
         [Tooltip("Countdown seconds before a mini-game round starts.")]
         public float PreRoundCountdownSeconds = 3f;
 
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index 4e88101d7..cf35f9ac5 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -33,13 +33,6 @@ namespace CosmicShore.Game.Arcade.Party
         [Tooltip("Root GameObjects for each mini-game environment. Index must match AvailableMiniGames order in config.")]
         [SerializeField] List<GameObject> miniGameEnvironments = new();
 
-        [Header("Offline Mode")]
-        [Tooltip("Enable to play solo with AI opponents (no network matchmaking required).")]
-        [SerializeField] bool offlineMode;
-
-        [Tooltip("Seconds to wait in lobby before auto-starting in offline mode.")]
-        [SerializeField] float offlineLobbyWaitSeconds = 10f;
-
         // --- Network state ---
         readonly NetworkVariable<int> _netCurrentRound = new(0);
         readonly NetworkVariable<int> _netPhase = new((int)PartyPhase.Lobby);
@@ -55,6 +48,9 @@ namespace CosmicShore.Game.Arcade.Party
         CancellationTokenSource _lobbyCts;
         CancellationTokenSource _roundCts;
 
+        // --- Derived state ---
+        bool IsSoloWithAI => !gameData.IsMultiplayerMode;
+
         // --- Public API ---
         public PartyPhase CurrentPhase => (PartyPhase)_netPhase.Value;
         public int CurrentRound => _netCurrentRound.Value;
@@ -103,8 +99,8 @@ namespace CosmicShore.Game.Arcade.Party
                 _netLobbyStartTime.Value = Time.realtimeSinceStartup;
                 SetPhase(PartyPhase.Lobby);
 
-                if (offlineMode)
-                    StartOfflineLobby().Forget();
+                if (IsSoloWithAI)
+                    StartSoloLobby().Forget();
                 else
                     StartLobbyTimer().Forget();
             }
@@ -238,10 +234,11 @@ namespace CosmicShore.Game.Arcade.Party
         }
 
         /// <summary>
-        /// Offline solo mode: add the local player, fill with AI, wait a short
-        /// lobby period for atmosphere, then auto-transition to WaitingForReady.
+        /// Solo mode (1 player selected): wait for ServerPlayerVesselInitializer to
+        /// spawn players via HandlePlayerAdded, then use a short lobby timer so the
+        /// player can see the lobby before readying up.
         /// </summary>
-        async UniTaskVoid StartOfflineLobby()
+        async UniTaskVoid StartSoloLobby()
         {
             _lobbyCts?.Cancel();
             _lobbyCts = new CancellationTokenSource();
@@ -251,28 +248,21 @@ namespace CosmicShore.Game.Arcade.Party
             {
                 BroadcastGameStateText_ClientRpc("Setting up party...");
 
-                // Add the local player
-                string localName = gameData.LocalPlayer != null
-                    ? gameData.LocalPlayer.Name
-                    : "Player";
```

</details>

### `7645142d7` — Fix party game scene: add NetworkManager, vessel initializers, and environment

_Claude, 2026-02-24 12:23:52 +0000_

```text
- Set ArcadeGamePartyGame MinPlayers to 1 (allows solo play from arcade UI)
- Add NetworkManager prefab to DependencySpawner items (fixes NetworkManager
  missing error that prevented any netcode from working)
- Add ServerPlayerVesselInitializer, ClientPlayerVesselInitializer, and
  NetcodeHooks components to PartyGameManager (enables vessel spawning
  and AI opponent creation)
- Wire NetworkStatsManager's _netcodeHooks reference to the new NetcodeHooks
  component (fixes NullReferenceException in OnDisable)
- Add Cell and Nucleus environment prefabs to scene (visual backdrop)
```

```text
 Assets/_SO_Assets/Games/ArcadeGamePartyGame.asset         |   2 +-
 Assets/_Scenes/Multiplayer Scenes/MinigamePartyGame.unity | 205 ++++++++++++++++++++++++++++++++++++++++++++
 2 files changed, 206 insertions(+), 1 deletion(-)
```

### `ba99518bc` — Add SkyboxModel (BigMembraneVariant) to party game scene

_Claude, 2026-02-24 12:25:19 +0000_

```text
Adds the missing skybox/membrane visual boundary prefab that exists in
all other multiplayer scenes like Joust.
```

```text
 Assets/_Scenes/Multiplayer Scenes/MinigamePartyGame.unity | 69 +++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 69 insertions(+)
```

### `9c8573f54` — Fix HUD null refs and add PlayerScoreContainer to party game scene

_Claude, 2026-02-24 12:38:47 +0000_

```text
- Add null guards for playerScoreContainer/playerScoreCardPrefab in
  MiniGameHUDView, MiniGameHUD, and MultiplayerHUD to prevent
  UnassignedReferenceExceptions when these optional UI elements are
  not wired up
- Create PlayerScoreContainer RectTransform with VerticalLayoutGroup
  in the party game scene's GameCanvas
- Wire playerScoreContainer, playerScoreCardPrefab, and domainColors
  on MiniGameHUDView via prefab instance property overrides
```

```text
 Assets/_Scenes/Multiplayer Scenes/MinigamePartyGame.unity | 193 +++++++++++++++++++++++++++++++++++++++++++-
 Assets/_Scripts/Game/UI/MiniGameHUD.cs                    |   6 +-
 Assets/_Scripts/Game/UI/MultiplayerHUD.cs                 |   4 +
 Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs       |   1 +
 4 files changed, 202 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/UI/MiniGameHUD.cs b/Assets/_Scripts/Game/UI/MiniGameHUD.cs
index 61f7e96ff..3855e4180 100644
--- a/Assets/_Scripts/Game/UI/MiniGameHUD.cs
+++ b/Assets/_Scripts/Game/UI/MiniGameHUD.cs
@@ -210,7 +210,7 @@ namespace CosmicShore.Game.UI
 
         private void SetupLocalPlayerCard()
         {
-            if (gameData.LocalPlayer == null || view.PlayerScoreCardPrefab == null)
+            if (gameData.LocalPlayer == null || view.PlayerScoreCardPrefab == null || view.PlayerScoreContainer == null)
                 return;
 
             var localPlayer = gameData.LocalPlayer;
@@ -237,6 +237,10 @@ namespace CosmicShore.Game.UI
         {
             _aiCards.Clear();
             _aiScoreHandlers.Clear();
+
+            if (view.PlayerScoreCardPrefab == null || view.PlayerScoreContainer == null)
+                return;
+
             AssignAIProfiles();
 
             foreach (var stats in gameData.RoundStatsList)
diff --git a/Assets/_Scripts/Game/UI/MultiplayerHUD.cs b/Assets/_Scripts/Game/UI/MultiplayerHUD.cs
index a5516bfa2..087d27fbb 100644
--- a/Assets/_Scripts/Game/UI/MultiplayerHUD.cs
+++ b/Assets/_Scripts/Game/UI/MultiplayerHUD.cs
@@ -82,6 +82,10 @@ namespace CosmicShore.Game.UI
         {
             view.ClearPlayerList();
             _playerCards.Clear();
+
+            if (view.PlayerScoreCardPrefab == null || view.PlayerScoreContainer == null)
+                return;
+
             AssignAIProfiles();
 
             foreach (var stats in gameData.RoundStatsList)
diff --git a/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs b/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs
index 94cdd45b6..0dc46504f 100644
--- a/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs
+++ b/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs
@@ -125,6 +125,7 @@ namespace CosmicShore.Game.UI
 
         public void ClearPlayerList()
         {
+            if (!playerScoreContainer) return;
             foreach (Transform child in playerScoreContainer)
             {
                 Destroy(child.gameObject);
```

</details>

### `5d70d4588` — Fix duplicate fileIDs (900000001-4) in MinigamePartyGame scene

_Claude, 2026-02-24 13:11:01 +0000_

```text
Two object sets (Spawners + PlayerScoreContainer) shared IDs 900000001-4,
causing Unity to fail loading the scene entirely. Renumbered the
PlayerScoreContainer UI objects to 900100001-4 and updated all references.
```

```text
 Assets/_Scenes/Multiplayer Scenes/MinigamePartyGame.unity | 24 ++++++++++++------------
 1 file changed, 12 insertions(+), 12 deletions(-)
```

### `2811bf938` — Enable Render Graph, disable compatibility mode in URP settings

_Claude, 2026-02-24 13:17:54 +0000_

```text
No custom ScriptableRenderPasses exist in the project and the renderer
has zero custom features, so no migration is needed. Flipped
m_EnableRenderGraph to 1 and m_EnableRenderCompatibilityMode to 0.
```

```text
 Assets/_Graphics/UniversalRenderPipelineGlobalSettings.asset | 4 ++--
 1 file changed, 2 insertions(+), 2 deletions(-)
```

### `879cf532f` — Make PartyGameController drive game flow: start gameplay and add round timer

_Claude, 2026-02-24 13:26:54 +0000_

```text
The party game environments don't have standalone mini-game controllers
(which are NetworkBehaviours designed for standalone scenes). Instead,
PartyGameController now handles the full game flow:

- After countdown, calls InitializeGame/SetPlayersActive/StartTurn via
  ClientRpc so the actual gameplay begins
- Adds a configurable round duration timer (default 60s) as a fallback
  end condition — fires InvokeWinnerCalculated/InvokeMiniGameEnd if no
  game-specific end condition triggers first
- Round timer is auto-cancelled if the game ends normally via gameData
  events
- Adds RoundDurationSeconds to PartyGameConfigSO
```

```text
 Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs   |  5 +++++
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs | 54 ++++++++++++++++++++++++++++++++++++++++++++--
 2 files changed, 57 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 103 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs
index dac9fe3f1..8dc52a457 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs
@@ -42,5 +42,10 @@ namespace CosmicShore.Game.Arcade.Party
 
         [Tooltip("Delay (seconds) after a round ends before showing the party panel.")]
         public float PostRoundDelaySeconds = 2f;
+
+        [Header("Round Duration")]
+        [Tooltip("Maximum duration (seconds) for each mini-game round. The round ends when either " +
+                 "the game-specific end condition fires or this timer expires, whichever comes first.")]
+        public float RoundDurationSeconds = 60f;
     }
 }
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index cf35f9ac5..ba0bc2155 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -150,7 +150,7 @@ namespace CosmicShore.Game.Arcade.Party
                 _miniGameControllers.Add(controller);
 
                 if (!controller)
-                    CSDebug.LogWarning($"[PartyGame] No MiniGameControllerBase on environment '{env.name}'.");
+                    CSDebug.Log($"[PartyGame] No MiniGameControllerBase on '{env.name}'. PartyGameController will drive game flow.");
             }
         }
 
@@ -459,9 +459,18 @@ namespace CosmicShore.Game.Arcade.Party
                 // Notify listeners
                 OnRoundStarting?.Invoke(roundIndex, selectedMode);
 
-                // Reset game data for the round
+                // Reset game data for the round and start gameplay
                 gameData.GameMode = selectedMode;
                 ResetGameDataForRound_ClientRpc((int)selectedMode);
+
+                // Small delay for environment activation to settle
+                await UniTask.Delay(TimeSpan.FromSeconds(0.5f), DelayType.UnscaledDeltaTime, cancellationToken: ct);
+
+                // Start the actual gameplay — this is what the standalone controllers do
+                StartGameplay_ClientRpc();
+
+                // Start a round timer as a fallback end condition
+                RunRoundTimer(ct).Forget();
             }
             catch (OperationCanceledException) { }
         }
@@ -489,6 +498,39 @@ namespace CosmicShore.Game.Arcade.Party
             return chosen;
         }
 
+        /// <summary>
+        /// Fallback round timer. If the game-specific end condition doesn't fire
+        /// within the configured duration, the server forces the round to end.
+        /// </summary>
+        async UniTaskVoid RunRoundTimer(CancellationToken ct)
+        {
+            try
+            {
+                await UniTask.Delay(
+                    TimeSpan.FromSeconds(config.RoundDurationSeconds),
+                    DelayType.UnscaledDeltaTime,
+                    cancellationToken: ct);
+
+                // Timer expired and we're still playing — force end
+                if (IsServer && CurrentPhase == PartyPhase.Playing)
+                {
+                    CSDebug.Log("[PartyGame] Round timer expired. Forcing round end.");
+                    ForceEndRound_ClientRpc();
+                }
+            }
+            catch (OperationCanceledException) { }
+        }
+
+        [ClientRpc]
+        void ForceEndRound_ClientRpc()
+        {
+            gameData.InvokeGameTurnConditionsMet();
+            gameData.SortRoundStats(false);
```

</details>

### `c0a73b949` — Fix NullRefs in AIPilot, PauseMenu and domain pool exhaustion in party mode

_Claude, 2026-02-24 14:49:47 +0000_

```text
- AIPilot.UpdateCellContent: guard against null cellData.Cell when called
  during Initialize() before any cell is active
- PartyGameController.FillWithAI: pick unused domains from player states
  instead of drawing from DomainAssigner pool (already exhausted by
  ServerPlayerVesselInitializer)
- PauseMenu: null-check gameData.LocalPlayer before accessing InputStatus
  in both pause and resume paths — LocalPlayer may not be set in party scene
```

```text
 Assets/_Scripts/Game/AI/AIPilot.cs                       |  2 ++
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs | 19 ++++++++++++++++++-
 Assets/_Scripts/Game/UI/PauseMenu.cs                     |  8 ++++++--
 3 files changed, 26 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/AI/AIPilot.cs b/Assets/_Scripts/Game/AI/AIPilot.cs
index 098c63fdd..ec1d5e3f3 100644
--- a/Assets/_Scripts/Game/AI/AIPilot.cs
+++ b/Assets/_Scripts/Game/AI/AIPilot.cs
@@ -145,6 +145,8 @@ namespace CosmicShore.Game.AI
             if (vessel == null || VesselStatus == null) return;
 
             var activeCell = cellData.Cell;
+            if (activeCell == null) return;
+
             var cellItems = cellData.CellItems;
             float MinDistance = Mathf.Infinity;
             CellItem closestItem = null;
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index ba0bc2155..eca312376 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -315,7 +315,7 @@ namespace CosmicShore.Game.Arcade.Party
             int currentCount = _playerStates.Count;
             for (int i = currentCount; i < config.MaxPlayers; i++)
             {
-                var aiDomain = DomainAssigner.GetDomainsByGameModes(GameModes.PartyGame);
+                var aiDomain = PickUnusedDomain();
                 string aiName = $"AI Pilot {i + 1}";
 
                 _playerStates.Add(new PartyPlayerState
@@ -331,6 +331,23 @@ namespace CosmicShore.Game.Arcade.Party
             }
         }
 
+        /// <summary>
+        /// Picks a domain not already used by existing players.
+        /// Avoids drawing from the DomainAssigner pool which may already
+        /// be exhausted by the ServerPlayerVesselInitializer.
+        /// </summary>
+        Domains PickUnusedDomain()
+        {
+            var usedDomains = new HashSet<Domains>(_playerStates.Select(p => p.Domain));
+            Domains[] candidates = { Domains.Jade, Domains.Ruby, Domains.Gold };
+            foreach (var d in candidates)
+            {
+                if (!usedDomains.Contains(d))
+                    return d;
+            }
+            return Domains.Unassigned;
+        }
+
         /// <summary>
         /// Called when a player leaves mid-party. Replaces them with AI.
         /// AI replacement scores are not recorded.
diff --git a/Assets/_Scripts/Game/UI/PauseMenu.cs b/Assets/_Scripts/Game/UI/PauseMenu.cs
index 965385d32..a35d8b431 100644
--- a/Assets/_Scripts/Game/UI/PauseMenu.cs
+++ b/Assets/_Scripts/Game/UI/PauseMenu.cs
@@ -73,7 +73,9 @@ namespace CosmicShore.App.UI.Screens
         {
             PauseSystem.TogglePauseGame(false);
             Hide();
-            
+
+            if (gameData.LocalPlayer == null) return;
+
             if (!wasLocalPlayerInputPausedBefore)
                 _ = TogglePlayerPauseWithDelay(false);
         }
@@ -85,7 +87,9 @@ namespace CosmicShore.App.UI.Screens
         {
             PauseSystem.TogglePauseGame(true);
             Show();
-            
+
+            if (gameData.LocalPlayer == null) return;
+
             wasLocalPlayerInputPausedBefore = gameData.LocalPlayer.InputStatus.Paused;
             if (!wasLocalPlayerInputPausedBefore)
                 _ = TogglePlayerPauseWithDelay(true);
```

</details>

### `7e1a43e3e` — Fix player movement and add environment prefab instantiation for party rounds

_Claude, 2026-02-24 15:53:38 +0000_

```text
Two fixes:

1. Player can't move after Ready: PauseSystem.Paused (static) carries over
   from menu navigation via ScreenSwitcher, setting Time.timeScale=0 and
   blocking InputController.Update(). No multiplayer controller ever calls
   TogglePauseGame(false). Now StartGameplay_ClientRpc explicitly unpauses
   before activating players.

2. Empty environments: Party scene environments were shell GameObjects with
   no visual content. Added MiniGameEnvironmentEntry config to map each
   GameMode to its environment prefab (Nucleus for Joust/CrystalCapture,
   RacingCellVariant for HexRace). Prefabs are instantiated locally on each
   client when a round starts and destroyed when the round ends. These are
   visual-only prefabs (no NetworkBehaviour) so local instantiation is safe.

Setup: Drag Nucleus.prefab and RacingCellVariant.prefab into the
EnvironmentPrefabs list on PartyGameConfig SO in the inspector.
```

```text
 Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs   | 18 ++++++++++++++++++
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs | 35 +++++++++++++++++++++++++++++++++++
 2 files changed, 53 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 118 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs
index 8dc52a457..ee2c6a7d2 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs
@@ -1,8 +1,21 @@
+using System;
 using System.Collections.Generic;
 using UnityEngine;
 
 namespace CosmicShore.Game.Arcade.Party
 {
+    /// <summary>
+    /// Maps a GameMode to the visual environment prefab that should be
+    /// instantiated when that mini-game round starts.
+    /// </summary>
+    [Serializable]
+    public class MiniGameEnvironmentEntry
+    {
+        public GameModes gameMode;
+        [Tooltip("Prefab to instantiate as the visual environment (Cell, Nucleus, etc.).")]
+        public GameObject environmentPrefab;
+    }
+
     [CreateAssetMenu(
         fileName = "PartyGameConfig",
         menuName = "ScriptableObjects/Party/PartyGameConfig")]
@@ -27,6 +40,11 @@ namespace CosmicShore.Game.Arcade.Party
             GameModes.MultiplayerJoust,
         };
 
+        [Header("Environment Prefabs")]
+        [Tooltip("Map each mini-game mode to its visual environment prefab (Cell, Nucleus, etc.). " +
+                 "These are instantiated locally on each client when a round starts.")]
+        public List<MiniGameEnvironmentEntry> EnvironmentPrefabs = new();
+
         [Header("Timing")]
         [Tooltip("Seconds to wait for players in online matchmaking before filling with AI.")]
         public float LobbyWaitTimeSeconds = 120f;
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index eca312376..a7d6a9db9 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -1,3 +1,4 @@
+using CosmicShore.App.Systems;
 using System;
 using System.Collections.Generic;
 using System.Linq;
@@ -47,6 +48,7 @@ namespace CosmicShore.Game.Arcade.Party
         int _readyPlayerCount;
         CancellationTokenSource _lobbyCts;
         CancellationTokenSource _roundCts;
+        GameObject _activeEnvironmentInstance;
 
         // --- Derived state ---
         bool IsSoloWithAI => !gameData.IsMultiplayerMode;
@@ -128,6 +130,8 @@ namespace CosmicShore.Game.Arcade.Party
             _roundCts?.Cancel();
             _roundCts?.Dispose();
 
+            DestroyEnvironmentInstance();
+
             base.OnNetworkDespawn();
         }
 
@@ -831,6 +835,11 @@ namespace CosmicShore.Game.Arcade.Party
         [ClientRpc]
         void StartGameplay_ClientRpc()
         {
+            // Ensure the game is unpaused — PauseSystem is static and may carry
+            // over a paused state from menu navigation (ScreenSwitcher).
+            // Also restores Time.timeScale = 1 so physics/movement work.
+            PauseSystem.TogglePauseGame(false);
+
             gameData.InitializeGame();
             gameData.SetPlayersActive();
             gameData.StartTurn();
@@ -846,6 +855,13 @@ namespace CosmicShore.Game.Arcade.Party
                 var env = miniGameEnvironments[miniGameIndex];
                 if (env) env.SetActive(true);
             }
```

</details>

### `0aa666207` — Wire environment prefabs in PartyGameConfig SO asset

_Claude, 2026-02-24 15:55:37 +0000_

```text
Pre-configure the EnvironmentPrefabs list so it works out of the box:
- MultiplayerCrystalCapture (35) → Nucleus.prefab
- HexRace (33) → RacingCellVariant.prefab
- MultiplayerJoust (34) → Nucleus.prefab

Also adds RoundDurationSeconds = 60 default.
```

```text
 Assets/_SO_Assets/Games/PartyGameConfig.asset | 8 ++++++++
 1 file changed, 8 insertions(+)
```

### `c771aad4f` — Fix Cell CellConfigs crash and SegmentSpawner initialization in party mode

_Claude, 2026-02-24 16:16:33 +0000_

```text
Root cause: RacingCellVariant.prefab overrode the dead 'CellTypes' field
(renamed to CellConfigs in Cell.cs) with a GUID that no longer exists.
When instantiated, the Cell had empty CellConfigs → crash on InitializeGame.

Fix:
- Update RacingCellVariant to override CellConfigs (not CellTypes) with
  the Barren Cell Config SO used by the standalone HexRace scene
- Explicitly call SegmentSpawner.Initialize() when activating an env
  each round (Start() only fires once, subsequent rounds need this)
- Set InitializeOnStart=0 on CrystalCapture/Joust env spawners to
  prevent double-init (controller now handles all init)
- Parent instantiated env prefabs under the Env_* object so they
  deactivate cleanly with the environment
```

```text
 Assets/_Prefabs/Environment/RacingCellVariant.prefab      | 11 ++++++++---
 Assets/_Scenes/Multiplayer Scenes/MinigamePartyGame.unity |  4 ++--
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs  | 22 +++++++++++++++++-----
 3 files changed, 27 insertions(+), 10 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index a7d6a9db9..42030d35e 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -850,17 +850,27 @@ namespace CosmicShore.Game.Arcade.Party
         {
             DeactivateAllEnvironments();
 
+            Transform envParent = null;
             if (miniGameIndex >= 0 && miniGameIndex < miniGameEnvironments.Count)
             {
                 var env = miniGameEnvironments[miniGameIndex];
-                if (env) env.SetActive(true);
+                if (env)
+                {
+                    env.SetActive(true);
+                    envParent = env.transform;
+
+                    // Initialize SegmentSpawners each round (Start only fires once,
+                    // so subsequent rounds need an explicit Initialize call).
+                    var spawner = env.GetComponentInChildren<SegmentSpawner>();
+                    if (spawner) spawner.Initialize();
+                }
             }
 
-            // Instantiate the visual environment prefab for this game mode
+            // Instantiate the visual environment prefab (Nucleus/Cell) under the env
             if (miniGameIndex >= 0 && miniGameIndex < config.AvailableMiniGames.Count)
             {
                 var mode = config.AvailableMiniGames[miniGameIndex];
-                SpawnEnvironmentPrefab(mode);
+                SpawnEnvironmentPrefab(mode, envParent);
             }
         }
 
@@ -877,14 +887,16 @@ namespace CosmicShore.Game.Arcade.Party
             gameData.ResetStatsDataForReplay();
         }
 
-        void SpawnEnvironmentPrefab(GameModes mode)
+        void SpawnEnvironmentPrefab(GameModes mode, Transform parent = null)
         {
             DestroyEnvironmentInstance();
 
             var entry = config.EnvironmentPrefabs.Find(e => e.gameMode == mode);
             if (entry?.environmentPrefab == null) return;
 
-            _activeEnvironmentInstance = Instantiate(entry.environmentPrefab);
+            _activeEnvironmentInstance = parent
+                ? Instantiate(entry.environmentPrefab, parent)
+                : Instantiate(entry.environmentPrefab);
             _activeEnvironmentInstance.name = $"PartyEnv_{mode}";
         }
 
```

</details>

### `962f834d5` — Add component based systems for claude to work on

_Shombith03, 2026-02-24 21:56:04 +0530_

```text
 Assets/_Prefabs/Minigame.meta                                         |    8 +
 Assets/_Prefabs/Minigame/CrystalCapture_Components.prefab             | 3574 ++++++++++++++++++++++++++++
 Assets/_Prefabs/Minigame/CrystalCapture_Components.prefab.meta        |    7 +
 Assets/_Prefabs/Minigame/HexRace_Components.prefab                    | 2339 +++++++++++++++++++
 Assets/_Prefabs/Minigame/HexRace_Components.prefab.meta               |    7 +
 Assets/_Prefabs/Minigame/Joust_Components.prefab                      | 2825 +++++++++++++++++++++++
 Assets/_Prefabs/Minigame/Joust_Components.prefab.meta                 |    7 +
 Assets/_SO_Assets/Games/PartyGameConfig.asset                         |   16 +-
 .../MinigameCrystalCaptureMultiplayer_Gameplay.unity                  | 3844 ++-----------------------------
 Assets/_Scenes/Multiplayer Scenes/MinigameHexRace.unity               | 2501 ++------------------
 Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity        | 3301 +++-----------------------
 Assets/_Scenes/Multiplayer Scenes/MinigamePartyGame.unity             |  905 ++++----
 12 files changed, 9888 insertions(+), 9446 deletions(-)
```

### `7356bd56d` — Update MinigamePartyGame.unity

_Shombith03, 2026-02-24 22:03:00 +0530_

```text
 Assets/_Scenes/Multiplayer Scenes/MinigamePartyGame.unity | 33 ++++++++++++++++++++++++++++++++-
 1 file changed, 32 insertions(+), 1 deletion(-)
```

### `41be7ecf5` — Update MinigameJoust_Gameplay.unity

_Shombith03, 2026-02-24 22:03:03 +0530_

```text
 Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity | 51 ++++++++++++++++++++++++++++++++++++++++
 1 file changed, 51 insertions(+)
```

### `e3686f876` — Add per-game spawn positions, pause panel toggle, remove runtime env instantiation

_Claude, 2026-02-24 16:42:56 +0000_

```text
- Remove SpawnEnvironmentPrefab/DestroyEnvironmentInstance: envs are now
  embedded in the component prefabs (CrystalCapture_Components, etc.)
  and activated/deactivated directly instead of instantiated at runtime.
- Add UpdateSpawnPositionsFromEnv(): finds ServerPlayerVesselInitializer
  in the activated env and updates gameData spawn positions so players
  spawn at game-mode-specific locations each round.
- Add PlayerOrigins public property on ServerPlayerVesselInitializer to
  expose spawn transforms for PartyGameController consumption.
- Add TogglePartyPanel()/ShowPartyPanel() public methods for the
  volume/pause button to open the PartyPausePanel.
```

```text
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs          | 95 +++++++++++++++++++++----------------
 Assets/_Scripts/Game/Multiplayer/ServerPlayerVesselInitializer.cs |  6 +++
 2 files changed, 59 insertions(+), 42 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 163 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index 42030d35e..3d4bfd4f2 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -10,6 +10,7 @@ using Unity.Collections;
 using Unity.Netcode;
 using UnityEngine;
 using CosmicShore.Utility;
+using CosmicShore.Game;
 
 namespace CosmicShore.Game.Arcade.Party
 {
@@ -48,7 +49,6 @@ namespace CosmicShore.Game.Arcade.Party
         int _readyPlayerCount;
         CancellationTokenSource _lobbyCts;
         CancellationTokenSource _roundCts;
-        GameObject _activeEnvironmentInstance;
 
         // --- Derived state ---
         bool IsSoloWithAI => !gameData.IsMultiplayerMode;
@@ -130,8 +130,6 @@ namespace CosmicShore.Game.Arcade.Party
             _roundCts?.Cancel();
             _roundCts?.Dispose();
 
-            DestroyEnvironmentInstance();
-
             base.OnNetworkDespawn();
         }
 
@@ -850,27 +848,37 @@ namespace CosmicShore.Game.Arcade.Party
         {
             DeactivateAllEnvironments();
 
-            Transform envParent = null;
-            if (miniGameIndex >= 0 && miniGameIndex < miniGameEnvironments.Count)
-            {
-                var env = miniGameEnvironments[miniGameIndex];
-                if (env)
-                {
-                    env.SetActive(true);
-                    envParent = env.transform;
+            if (miniGameIndex < 0 || miniGameIndex >= miniGameEnvironments.Count) return;
 
-                    // Initialize SegmentSpawners each round (Start only fires once,
-                    // so subsequent rounds need an explicit Initialize call).
-                    var spawner = env.GetComponentInChildren<SegmentSpawner>();
-                    if (spawner) spawner.Initialize();
-                }
-            }
+            var env = miniGameEnvironments[miniGameIndex];
+            if (!env) return;
+
+            env.SetActive(true);
+
+            // Initialize SegmentSpawners each round (Start only fires once,
+            // so subsequent rounds need an explicit Initialize call).
+            var spawner = env.GetComponentInChildren<SegmentSpawner>();
+            if (spawner) spawner.Initialize();
+
+            // Update spawn positions from this game mode's environment.
+            // Each env prefab has a ServerPlayerVesselInitializer with _playerOrigins
+            // configured for that game mode's specific spawn layout.
+            UpdateSpawnPositionsFromEnv(env);
+        }
 
-            // Instantiate the visual environment prefab (Nucleus/Cell) under the env
-            if (miniGameIndex >= 0 && miniGameIndex < config.AvailableMiniGames.Count)
+        /// <summary>
+        /// Finds the ServerPlayerVesselInitializer in the activated environment
+        /// and updates gameData spawn positions to that game mode's layout.
+        /// Then repositions all players to the new spawn points.
+        /// </summary>
+        void UpdateSpawnPositionsFromEnv(GameObject env)
+        {
+            var spvi = env.GetComponentInChildren<ServerPlayerVesselInitializer>(true);
+            if (spvi && spvi.PlayerOrigins is { Length: > 0 })
             {
-                var mode = config.AvailableMiniGames[miniGameIndex];
-                SpawnEnvironmentPrefab(mode, envParent);
+                gameData.SetSpawnPositions(spvi.PlayerOrigins);
```

</details>

### `68b4f702a` — Use env CountdownTimer for in-game 3-2-1-GO before enabling vessels

_Claude, 2026-02-24 16:50:29 +0000_

```text
After the party countdown ("Starting in 3..."), PartyGameController now
finds the active mini-game environment's CountdownTimer and uses it for
the in-game 3-2-1-GO visual (same as standalone controllers). Vessels
are enabled (SetPlayersActive + StartTurn) only after the countdown
completes. This replicates the standalone OnReadyClicked → countdown →
play flow without requiring the controller's NetworkObject to be spawned.

Falls back to immediate start if no CountdownTimer is found in the env.
```

```text
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs | 40 ++++++++++++++++++++++++++++++++++++++--
 1 file changed, 38 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index 3d4bfd4f2..6ac6e4259 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -47,6 +47,7 @@ namespace CosmicShore.Game.Arcade.Party
         readonly List<GameModes> _recentMiniGames = new();
         readonly List<MiniGameControllerBase> _miniGameControllers = new();
         int _readyPlayerCount;
+        int _activeMiniGameIndex = -1;
         CancellationTokenSource _lobbyCts;
         CancellationTokenSource _roundCts;
 
@@ -839,8 +840,40 @@ namespace CosmicShore.Game.Arcade.Party
             PauseSystem.TogglePauseGame(false);
 
             gameData.InitializeGame();
-            gameData.SetPlayersActive();
-            gameData.StartTurn();
+
+            // Use the active mini-game environment's CountdownTimer for the
+            // in-game 3-2-1-GO visual, then enable vessels. This replicates the
+            // standalone controller flow (OnReadyClicked → countdown → SetPlayersActive)
+            // without requiring the controller's NetworkObject to be spawned.
+            var countdownTimer = FindActiveCountdownTimer();
+            if (countdownTimer)
+            {
+                countdownTimer.BeginCountdown(() =>
+                {
+                    gameData.SetPlayersActive();
+                    gameData.StartTurn();
+                });
+            }
+            else
+            {
+                // No countdown timer in this env — start immediately.
+                gameData.SetPlayersActive();
+                gameData.StartTurn();
+            }
+        }
+
+        /// <summary>
+        /// Finds the CountdownTimer component in the currently active mini-game environment.
+        /// </summary>
+        CountdownTimer FindActiveCountdownTimer()
+        {
+            if (_activeMiniGameIndex < 0 || _activeMiniGameIndex >= miniGameEnvironments.Count)
+                return null;
+
+            var env = miniGameEnvironments[_activeMiniGameIndex];
+            if (!env || !env.activeSelf) return null;
+
+            return env.GetComponentInChildren<CountdownTimer>();
         }
 
         [ClientRpc]
@@ -848,6 +881,8 @@ namespace CosmicShore.Game.Arcade.Party
         {
             DeactivateAllEnvironments();
 
+            _activeMiniGameIndex = miniGameIndex;
+
             if (miniGameIndex < 0 || miniGameIndex >= miniGameEnvironments.Count) return;
 
             var env = miniGameEnvironments[miniGameIndex];
@@ -897,6 +932,7 @@ namespace CosmicShore.Game.Arcade.Party
 
         void DeactivateAllEnvironments()
         {
+            _activeMiniGameIndex = -1;
             foreach (var env in miniGameEnvironments)
                 if (env) env.SetActive(false);
         }
```

</details>

### `35d206b31` — Rework party flow: auto-start first round, ready per mini-game

_Claude, 2026-02-24 19:20:33 +0000_

```text
- Remove initial WaitingForReady gate: lobby fill now auto-starts
  the first round instead of waiting for a ready click
- Add MiniGameReady phase: env activates and players are positioned
  first, then ready button appears for the mini-game
- Ready click during MiniGameReady triggers BeginMiniGamePlay which
  hides the panel, runs the env's CountdownTimer 3-2-1-GO, then
  calls SetPlayersActive + StartTurn
- Between rounds (RoundResults), ready still advances to next round
- PartyPausePanel handles MiniGameReady phase with enabled ready btn

Flow: Lobby → Randomizing → env activate → MiniGameReady → ready →
countdown → Playing → game end → RoundResults → ready → next round
```

```text
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs | 75 +++++++++++++++++++++++++++-------------------
 Assets/_Scripts/Game/Arcade/Party/PartyPhase.cs          |  3 ++
 Assets/_Scripts/Game/UI/Party/PartyPausePanel.cs         |  8 ++++-
 3 files changed, 55 insertions(+), 31 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 173 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index 6ac6e4259..d1b0b4b08 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -225,12 +225,11 @@ namespace CosmicShore.Game.Arcade.Party
                     DelayType.UnscaledDeltaTime,
                     cancellationToken: ct);
 
-                // Timeout reached — fill with AI and move to ready phase
+                // Timeout reached — fill with AI and start the first round
                 if (CurrentPhase == PartyPhase.Lobby)
                 {
                     FillWithAI();
-                    SetPhase(PartyPhase.WaitingForReady);
-                    BroadcastGameStateText_ClientRpc("All players joined. Ready up!");
+                    StartNextRound().Forget();
                 }
             }
             catch (OperationCanceledException) { }
@@ -268,8 +267,7 @@ namespace CosmicShore.Game.Arcade.Party
 
                 if (CurrentPhase == PartyPhase.Lobby)
                 {
-                    SetPhase(PartyPhase.WaitingForReady);
-                    BroadcastGameStateText_ClientRpc("All players joined. Ready up!");
+                    StartNextRound().Forget();
                 }
             }
             catch (OperationCanceledException) { }
@@ -308,8 +306,7 @@ namespace CosmicShore.Game.Arcade.Party
                     FillWithAI();
 
                 _lobbyCts?.Cancel();
-                SetPhase(PartyPhase.WaitingForReady);
-                BroadcastGameStateText_ClientRpc("All players joined. Ready up!");
+                StartNextRound().Forget();
             }
         }
 
@@ -369,7 +366,9 @@ namespace CosmicShore.Game.Arcade.Party
             CSDebug.Log($"[PartyGame] Player '{playerName}' left. Replaced by AI.");
 
             // If we were waiting for ready and this was the last holdout, check ready state
-            if (CurrentPhase == PartyPhase.WaitingForReady || CurrentPhase == PartyPhase.RoundResults)
+            if (CurrentPhase == PartyPhase.WaitingForReady ||
+                CurrentPhase == PartyPhase.RoundResults ||
+                CurrentPhase == PartyPhase.MiniGameReady)
                 CheckAllPlayersReady();
         }
 
@@ -410,6 +409,10 @@ namespace CosmicShore.Game.Arcade.Party
             {
                 StartNextRound().Forget();
             }
+            else if (CurrentPhase == PartyPhase.MiniGameReady)
+            {
+                BeginMiniGamePlay().Forget();
+            }
         }
 
         void ResetReadyStates()
@@ -455,38 +458,50 @@ namespace CosmicShore.Game.Arcade.Party
 
                 await UniTask.Delay(TimeSpan.FromSeconds(1.5f), DelayType.UnscaledDeltaTime, cancellationToken: ct);
 
-                // Phase: Countdown
-                SetPhase(PartyPhase.Countdown);
+                // Activate the mini-game environment and position players
+                gameData.GameMode = selectedMode;
+                ActivateMiniGameEnvironment_ClientRpc(miniGameIndex);
+                ResetGameDataForRound_ClientRpc((int)selectedMode);
+
+                // Notify listeners
+                OnRoundStarting?.Invoke(roundIndex, selectedMode);
+
+                // Small delay for environment activation to settle
+                await UniTask.Delay(TimeSpan.FromSeconds(0.5f), DelayType.UnscaledDeltaTime, cancellationToken: ct);
+
+                // Phase: MiniGameReady — environment is active, show ready button
+                SetPhase(PartyPhase.MiniGameReady);
```

</details>

### `ab2ac6189` — Two-stage ready: free flight lobby, first ready loads env, second starts game

_Claude, 2026-02-24 20:20:25 +0000_

```text
Full party flow reworked:
1. Enter party → cinematic → free flight (EnableFreeFlightForLobby
   calls SetPlayersActive so vessels move immediately)
2. Lobby fills → Randomizing → "Round 1: Joust" announced
3. WaitingForReady → player clicks ready (1st ready)
4. "Loading..." → activate env, position players at spawn points
5. MiniGameReady → player clicks ready (2nd ready)
6. Hide panel → CountdownTimer 3-2-1-GO → SetPlayersActive + StartTurn
7. Playing → game end → RoundResults → ready → next round

Key changes:
- StartNextRound ends at WaitingForReady (env NOT loaded yet)
- New LoadMiniGameEnvironment: 1st ready triggers env activation
- CheckAllPlayersReady routes: RoundResults→next round,
  WaitingForReady→load env, MiniGameReady→start gameplay
- EnableFreeFlightForLobby_ClientRpc: unpauses + SetPlayersActive
  so players can fly during lobby/randomization
```

```text
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs | 62 ++++++++++++++++++++++++++++++++++++++++++----
 1 file changed, 57 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 132 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index d1b0b4b08..08c1f098a 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -229,6 +229,7 @@ namespace CosmicShore.Game.Arcade.Party
                 if (CurrentPhase == PartyPhase.Lobby)
                 {
                     FillWithAI();
+                    EnableFreeFlightForLobby_ClientRpc();
                     StartNextRound().Forget();
                 }
             }
@@ -267,6 +268,7 @@ namespace CosmicShore.Game.Arcade.Party
 
                 if (CurrentPhase == PartyPhase.Lobby)
                 {
+                    EnableFreeFlightForLobby_ClientRpc();
                     StartNextRound().Forget();
                 }
             }
@@ -306,6 +308,7 @@ namespace CosmicShore.Game.Arcade.Party
                     FillWithAI();
 
                 _lobbyCts?.Cancel();
+                EnableFreeFlightForLobby_ClientRpc();
                 StartNextRound().Forget();
             }
         }
@@ -405,12 +408,19 @@ namespace CosmicShore.Game.Arcade.Party
         {
             if (!_playerStates.All(p => p.IsReady)) return;
 
-            if (CurrentPhase == PartyPhase.WaitingForReady || CurrentPhase == PartyPhase.RoundResults)
+            if (CurrentPhase == PartyPhase.RoundResults)
             {
+                // Between rounds — pick the next game
                 StartNextRound().Forget();
             }
+            else if (CurrentPhase == PartyPhase.WaitingForReady)
+            {
+                // Game announced — load the environment
+                LoadMiniGameEnvironment().Forget();
+            }
             else if (CurrentPhase == PartyPhase.MiniGameReady)
             {
+                // Environment loaded — start gameplay with countdown
                 BeginMiniGamePlay().Forget();
             }
         }
@@ -440,11 +450,10 @@ namespace CosmicShore.Game.Arcade.Party
             {
                 int roundIndex = _netCurrentRound.Value;
 
-                // Phase: Randomizing
+                // Phase: Randomizing — pick and announce the game
                 SetPhase(PartyPhase.Randomizing);
                 BroadcastGameStateText_ClientRpc("Randomizing game...");
 
-                // Randomize mini-game selection
                 int miniGameIndex = PickRandomMiniGame();
                 _netSelectedMiniGameIndex.Value = miniGameIndex;
                 var selectedMode = config.AvailableMiniGames[miniGameIndex];
@@ -458,6 +467,38 @@ namespace CosmicShore.Game.Arcade.Party
 
                 await UniTask.Delay(TimeSpan.FromSeconds(1.5f), DelayType.UnscaledDeltaTime, cancellationToken: ct);
 
+                // Phase: WaitingForReady — show panel with game name, ready button.
+                // Environment is NOT loaded yet; it loads after all players ready up.
+                SetPhase(PartyPhase.WaitingForReady);
+                ResetReadyStates();
+                ForceShowPartyPanel_ClientRpc();
+                BroadcastGameStateText_ClientRpc("Ready up!");
+            }
+            catch (OperationCanceledException) { }
+        }
+
+        /// <summary>
+        /// Called when all players ready during WaitingForReady.
+        /// Shows a loading transition, activates the mini-game environment,
+        /// positions players, then enters MiniGameReady for the gameplay-start ready.
```

</details>

### `6f3368fec` — Update MinigamePartyGame.unity

_Shombith03, 2026-02-25 16:08:31 +0530_

```text
 Assets/_Scenes/Multiplayer Scenes/MinigamePartyGame.unity | 720 ++++++++++++++++----------------------------
 1 file changed, 263 insertions(+), 457 deletions(-)
```

### `7939e6326` — Fix party game flow: ready system, phase callbacks, logging

_Claude, 2026-02-25 11:18:18 +0000_

```text
- Fix initial Lobby phase callback not firing (NetworkVariable.OnValueChanged
  skips when default matches initial value) by manually invoking it
- Fix SetPhase for re-entering same phase (force callback on same-value set)
- Add CSDebug logging to all silent failure paths in ready system
  (OnLocalPlayerReady, OnPlayerReady_ServerRpc, CheckAllPlayersReady)
- Add max player limit check in OnPlayerJoined for solo mode
- Fix ready count tracking: pass server-authoritative count to UI instead of
  accumulating on the client (SetReadyCount/ResetReadyCount on PartyRoundTab)
- Add ResetReadyStates_ClientRpc so UI resets between 1st→2nd ready phases
- Add partyComponentsRoot field for disabling party UI during gameplay and
  re-enabling after cinematic completes
- Fix PartyEndGameHandler: explicit using for XPRewardService, re-enable
  party components after cinematic sequence
- Fix PartyPauseButton: null-safe access to InputStatus and InputController
- Fix ResetGameDataForRound_ClientRpc: guard against empty RoundStatsList
  on first round
- Add ReinitializePanelWithPlayers_ClientRpc for proper panel refresh after
  lobby fills
```

```text
 Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs |  25 +--
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs | 404 ++++++++++++++++++++++++---------------------
 Assets/_Scripts/Game/UI/Party/PartyPauseButton.cs        |  39 ++---
 Assets/_Scripts/Game/UI/Party/PartyPausePanel.cs         |  96 +++++------
 Assets/_Scripts/Game/UI/Party/PartyRoundTab.cs           |  16 +-
 5 files changed, 298 insertions(+), 282 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1406 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs b/Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs
index e1ef2b157..17d232143 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs
@@ -1,5 +1,6 @@
 using CosmicShore.Game.Cinematics;
 using CosmicShore.Game.UI.Party;
+using CosmicShore.Game.XP;
 using CosmicShore.Soap;
 using UnityEngine;
 using CosmicShore.Utility;
@@ -18,6 +19,10 @@ namespace CosmicShore.Game.Arcade.Party
         [SerializeField] PartyGameController partyController;
         [SerializeField] PartyPausePanel partyPausePanel;
 
+        [Header("Party Components")]
+        [Tooltip("Root GameObject for party components. Re-enabled after cinematic so the panel can show.")]
+        [SerializeField] GameObject partyComponentsRoot;
+
         bool IsFinalRound => partyController != null &&
                              partyController.CurrentRound + 1 >= partyController.TotalRounds;
 
@@ -26,10 +31,12 @@ namespace CosmicShore.Game.Arcade.Party
             if (isRunning) return;
             isRunning = true;
 
+            CSDebug.Log($"[PartyEndGame] OnWinnerCalculated. Round={partyController?.CurrentRound}, IsFinal={IsFinalRound}");
+
             // Only award XP on the final round
             if (IsFinalRound)
             {
-                var xpService = Game.XP.XPRewardService.Instance;
+                var xpService = XPRewardService.Instance;
                 if (xpService != null)
                 {
                     int xp = xpService.AwardXP();
@@ -49,15 +56,12 @@ namespace CosmicShore.Game.Arcade.Party
         {
             localPlayerWon = DetermineLocalPlayerWon();
 
-            // Run victory lap if configured
             if (cinematic && cinematic.enableVictoryLap)
                 yield return StartCoroutine(RunVictoryLap(cinematic));
 
-            // Set local vessel to AI during cinematic
             if (cinematic && cinematic.setLocalVesselToAI)
                 SetLocalVesselAI(true, cinematic.aiCinematicBehavior);
 
-            // Camera sequence
             if (cinematic && cinematic.cameraSetups is { Count: > 0 })
                 yield return StartCoroutine(RunCameraSequence(cinematic));
             else
@@ -66,21 +70,17 @@ namespace CosmicShore.Game.Arcade.Party
                 yield return new WaitForSeconds(delay);
             }
 
-            // Score reveal animation
             yield return StartCoroutine(PlayScoreRevealSequence(cinematic));
 
-            // Show XP only on the final round
             if (IsFinalRound && view)
                 view.ShowXPEarned();
 
-            // Show continue button, wait for tap
             if (view)
             {
                 view.ShowContinueButton();
                 yield return new WaitUntil(() => !view.IsContinueButtonActive());
             }
 
-            // Reset game state and clean up cinematic UI
             ResetGameForNewRound();
 
             if (view)
@@ -89,11 +89,16 @@ namespace CosmicShore.Game.Arcade.Party
                 view.HideScoreRevealPanel();
             }
 
-            // Show party pause panel — it will display either "Ready" (mid-party)
-            // or "Next" (final round) based on the phase set by PartyGameController
```

</details>

### `3414b560d` — Fix party mode: disable mini-game controllers, remove free flight, Option B flow

_Claude, 2026-02-25 12:16:13 +0000_

```text
- Disable MiniGameControllerBase components on environments at startup and
  when activating them, preventing KeyNotFoundException from RPC dispatch
  and InitializeAfterDelay interference
- Remove free flight lobby phase (Option B: UI-only entry, wait for players)
- PartyEndGameHandler intercepts scoreboard, shows party panel instead
- PartyGameController now drives all game flow with controllers disabled
```

```text
 Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs |  6 ++---
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs | 58 ++++++++++++++++++++++++++++++++--------------
 2 files changed, 44 insertions(+), 20 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 140 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs b/Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs
index 17d232143..2fb2ad22f 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs
@@ -89,15 +89,15 @@ namespace CosmicShore.Game.Arcade.Party
                 view.HideScoreRevealPanel();
             }
 
-            // Re-enable party components (they were disabled during gameplay)
+            // Intercept the normal scoreboard: re-enable party components and show
+            // the party panel instead of calling gameData.InvokeShowGameEndScreen().
             if (partyComponentsRoot)
                 partyComponentsRoot.SetActive(true);
 
-            // Show party pause panel — "Ready" (mid-party) or "Next" (final round)
             if (partyPausePanel)
                 partyPausePanel.ForceShow();
 
-            CSDebug.Log("[PartyEndGame] Cinematic sequence complete, party panel shown.");
+            CSDebug.Log($"[PartyEndGame] Cinematic complete → party panel shown (isFinal={IsFinalRound}).");
 
             runningRoutine = null;
             isRunning = false;
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index 6a78e5a06..d061ab848 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -16,17 +16,21 @@ namespace CosmicShore.Game.Arcade.Party
     /// <summary>
     /// Orchestrates a full Party Game session: lobby, 5 randomized mini-game rounds,
     /// scoring, and final results. Lives in the party scene for the entire session.
-    /// Mini-game environments are child GameObjects that get enabled/disabled per round.
+    /// Mini-game environments are sibling GameObjects that get enabled/disabled per round.
     ///
-    /// Flow:
-    ///   Enter → PartyGame_Components enabled, ready button disabled
-    ///   → Cinematic → free flight (vessels active)
-    ///   → Lobby fills → Randomizing → "Round 1: Joust"
+    /// Option B (simplified) flow:
+    ///   Enter → PartyGame_Components enabled, party panel shown (UI-only, no free flight)
+    ///   → Wait for players (10s offline / host ready in multiplayer)
+    ///   → Randomizing → "Round 1: Joust"
     ///   → WaitingForReady → READY button (1st ready — accept the game)
-    ///   → "Loading..." → activate env → position players
+    ///   → "Loading..." → activate env (controllers disabled) → position players
     ///   → MiniGameReady → READY button (2nd ready — start playing)
     ///   → Hide panel → 3-2-1-GO countdown → SetPlayersActive + StartTurn
-    ///   → Playing → game ends → RoundResults → ready → next round...
+    ///   → Playing → game ends → cinematic → intercept scoreboard → party panel
+    ///   → RoundResults → ready → next round... (5 total)
+    ///
+    /// Mini-game controllers (MiniGameControllerBase) on environments are DISABLED
+    /// during party mode. PartyGameController drives all game flow instead.
     /// </summary>
     public class PartyGameController : NetworkBehaviour
     {
@@ -98,9 +102,15 @@ namespace CosmicShore.Game.Arcade.Party
             for (int i = 0; i < config.TotalRounds; i++)
                 _roundResults.Add(new PartyRoundResult { RoundIndex = i });
 
-            // Disable all mini-game environments at start
+            // Disable all mini-game environments and their controllers at start.
+            // Controllers must be disabled BEFORE SetActive(false) to prevent their
+            // OnNetworkSpawn → InitializeAfterDelay from interfering with party flow.
             foreach (var env in miniGameEnvironments)
-                if (env) env.SetActive(false);
+            {
+                if (!env) continue;
+                DisableMiniGameControllers(env);
+                env.SetActive(false);
+            }
 
             // Keep party components enabled (user's scene has them on by default)
             if (partyComponentsRoot)
@@ -229,7 +239,6 @@ namespace CosmicShore.Game.Arcade.Party
                 {
                     FillWithAI();
                     ReinitializePanelWithPlayers_ClientRpc();
-                    EnableFreeFlightForLobby_ClientRpc();
                     StartNextRound().Forget();
```

</details>

### `d543bc968` — Simplify party game: delete PartyEndGameHandler, remove partyComponentsRoot

_Claude, 2026-02-25 12:40:08 +0000_

```text
- Delete PartyEndGameHandler — each environment has its own end-game
  handler; PartyGameController just listens for gameData events
- Remove partyComponentsRoot field — was set to PartyGameManager itself,
  which would disable the controller during gameplay. Panel show/hide
  is sufficient.
- Collapse HidePartyUI/ShowPartyUI into simple ShowPanel/HidePanel RPCs
- Strip redundant logging and comments for cleaner code
```

```text
 Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs      | 106 -------------
 Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs.meta |  11 --
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs      | 343 +++++++++++-----------------------------
 3 files changed, 94 insertions(+), 366 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 849 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs b/Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs
deleted file mode 100644
index 2fb2ad22f..000000000
--- a/Assets/_Scripts/Game/Arcade/Party/PartyEndGameHandler.cs
+++ /dev/null
@@ -1,106 +0,0 @@
-using CosmicShore.Game.Cinematics;
-using CosmicShore.Game.UI.Party;
-using CosmicShore.Game.XP;
-using CosmicShore.Soap;
-using UnityEngine;
-using CosmicShore.Utility;
-
-namespace CosmicShore.Game.Arcade.Party
-{
-    /// <summary>
-    /// Overrides the end-game cinematic flow for party mode.
-    /// After each round: cinematic → score reveal → party pause panel (Ready button).
-    /// After the final round: cinematic → score reveal → party pause panel (Next button → PartyScoreboard).
-    /// XP is only awarded on the final round.
-    /// </summary>
-    public class PartyEndGameHandler : EndGameCinematicController
-    {
-        [Header("Party")]
-        [SerializeField] PartyGameController partyController;
-        [SerializeField] PartyPausePanel partyPausePanel;
-
-        [Header("Party Components")]
-        [Tooltip("Root GameObject for party components. Re-enabled after cinematic so the panel can show.")]
-        [SerializeField] GameObject partyComponentsRoot;
-
-        bool IsFinalRound => partyController != null &&
-                             partyController.CurrentRound + 1 >= partyController.TotalRounds;
-
-        protected override void OnWinnerCalculated()
-        {
-            if (isRunning) return;
-            isRunning = true;
-
-            CSDebug.Log($"[PartyEndGame] OnWinnerCalculated. Round={partyController?.CurrentRound}, IsFinal={IsFinalRound}");
-
-            // Only award XP on the final round
-            if (IsFinalRound)
-            {
-                var xpService = XPRewardService.Instance;
-                if (xpService != null)
-                {
-                    int xp = xpService.AwardXP();
-                    CSDebug.Log($"[PartyEndGame] Final round XP awarded: {xp}");
-                }
-            }
-
-            var localPlayer = gameData.LocalPlayer;
-            if (localPlayer?.Vessel?.VesselStatus != null)
-                cachedBoostMultiplier = localPlayer.Vessel.VesselStatus.BoostMultiplier;
-
-            var cinematic = ResolveCinematicForThisScene();
-            runningRoutine = StartCoroutine(RunPartyEndGameSequence(cinematic));
-        }
-
-        System.Collections.IEnumerator RunPartyEndGameSequence(CinematicDefinitionSO cinematic)
-        {
-            localPlayerWon = DetermineLocalPlayerWon();
-
-            if (cinematic && cinematic.enableVictoryLap)
-                yield return StartCoroutine(RunVictoryLap(cinematic));
-
-            if (cinematic && cinematic.setLocalVesselToAI)
-                SetLocalVesselAI(true, cinematic.aiCinematicBehavior);
-
-            if (cinematic && cinematic.cameraSetups is { Count: > 0 })
-                yield return StartCoroutine(RunCameraSequence(cinematic));
-            else
-            {
-                var delay = cinematic ? cinematic.delayBeforeEndScreen : 0.1f;
-                yield return new WaitForSeconds(delay);
-            }
-
-            yield return StartCoroutine(PlayScoreRevealSequence(cinematic));
-
```

</details>

### `969865416` — Fix party mode: guard SPVI null origins, register human player directly

_Claude, 2026-02-25 12:51:50 +0000_

```text
- Guard ServerPlayerVesselInitializer.OnNetworkSpawn against null/empty
  _playerOrigins — disables itself instead of crashing
- Register human player directly in StartSoloLobby if SPVI didn't
  spawn one (covers party mode where SPVI may be absent)
- Fix OnLocalPlayerReady to fall back to _playerStates when
  gameData.LocalPlayer is null (no vessel needed for Ready)
- Call CheckAllPlayersReady after ResetReadyStates for auto-progress
  when all players are AI/already ready
```

```text
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs          | 28 +++++++++++++++++++++++++---
 Assets/_Scripts/Game/Multiplayer/ServerPlayerVesselInitializer.cs |  7 +++++++
 2 files changed, 32 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index 5c3e8f8eb..fcf28ba45 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -243,6 +243,16 @@ namespace CosmicShore.Game.Arcade.Party
                     DelayType.UnscaledDeltaTime,
                     cancellationToken: ct);
 
+                // If no human player was registered (SPVI may not be present or failed),
+                // register one directly so the Ready button works.
+                if (!_playerStates.Any(p => !p.IsAIReplacement))
+                {
+                    string humanName = gameData.LocalPlayer?.Name ?? "Player 1";
+                    var domain = Domains.Jade;
+                    OnPlayerJoined(humanName, domain, false);
+                    CSDebug.Log($"[PartyGame] Registered human player directly: '{humanName}'");
+                }
+
                 if (_playerStates.Count < config.MaxPlayers)
                     FillWithAI();
 
@@ -354,13 +364,22 @@ namespace CosmicShore.Game.Arcade.Party
 
         public void OnLocalPlayerReady()
         {
-            if (gameData.LocalPlayer == null)
+            // Try gameData.LocalPlayer first; fall back to the human entry in _playerStates
+            // (covers party mode where SPVI may not have spawned a vessel yet).
+            string name = gameData.LocalPlayer?.Name;
+
+            if (string.IsNullOrEmpty(name))
             {
-                CSDebug.LogWarning("[PartyGame] OnLocalPlayerReady: gameData.LocalPlayer is null!");
+                var human = _playerStates.FirstOrDefault(p => !p.IsAIReplacement);
+                name = human?.PlayerName;
+            }
+
+            if (string.IsNullOrEmpty(name))
+            {
+                CSDebug.LogWarning("[PartyGame] OnLocalPlayerReady: no player found!");
                 return;
             }
 
-            string name = gameData.LocalPlayer.Name;
             CSDebug.Log($"[PartyGame] OnLocalPlayerReady: '{name}', phase={CurrentPhase}");
             OnPlayerReady_ServerRpc(name);
         }
@@ -411,6 +430,9 @@ namespace CosmicShore.Game.Arcade.Party
                 state.IsReady = state.IsAIReplacement;
 
             ResetReadyStates_ClientRpc();
+
+            // Auto-progress if all players are already ready (e.g., all AI or edge cases)
+            CheckAllPlayersReady();
         }
 
         #endregion
diff --git a/Assets/_Scripts/Game/Multiplayer/ServerPlayerVesselInitializer.cs b/Assets/_Scripts/Game/Multiplayer/ServerPlayerVesselInitializer.cs
index 76019f059..b8bf597a2 100644
--- a/Assets/_Scripts/Game/Multiplayer/ServerPlayerVesselInitializer.cs
+++ b/Assets/_Scripts/Game/Multiplayer/ServerPlayerVesselInitializer.cs
@@ -81,6 +81,13 @@ namespace CosmicShore.Game
                 return;
             }
 
+            if (_playerOrigins is not { Length: > 0 } || _playerOrigins[0] == null)
+            {
+                CSDebug.LogWarning($"[SPVI] _playerOrigins not assigned on '{gameObject.name}', skipping initialization.");
+                enabled = false;
+                return;
+            }
+
             gameData.SetSpawnPositions(_playerOrigins);
 
             NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
```

</details>

### `4a37efa10` — Fix party mode: Ready-gated lobby, env canvas disabled, OnReadyClicked guard

_Claude, 2026-02-25 13:06:20 +0000_

```text
Four fixes:
1. MiniGameControllerBase.OnReadyClicked now checks `enabled` before
   delegating — prevents NPE when environment buttons are clicked but
   controllers are disabled in party mode
2. Solo and multiplayer lobbies now transition to WaitingForReady instead
   of auto-starting rounds. Player must click Ready to begin.
3. ActivateMiniGameEnvironment disables environment Canvas components
   to prevent the mini-game's Ready button, cameras, and scoreboard
   from overlapping party UI
4. UpdateSpawnPositionsFromEnv no longer calls gameData.ResetPlayers()
   which was NPE-ing on players without vessels (spawned by environment
   SPVIs before PartyGameController could disable them)
```

```text
 Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs    |  1 +
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs | 43 +++++++++++++++++++++++++++++++++++++------
 2 files changed, 38 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 101 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs b/Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs
index 1aa721b08..f0fe4b856 100644
--- a/Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs
+++ b/Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs
@@ -29,6 +29,7 @@ namespace CosmicShore.Game.Arcade
         
         public void OnReadyClicked()
         {
+            if (!enabled) return;
             OnReadyClicked_();
         }
         
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index fcf28ba45..c8ba77f39 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -222,7 +222,12 @@ namespace CosmicShore.Game.Arcade.Party
                 {
                     FillWithAI();
                     ReinitializePanelWithPlayers_ClientRpc();
-                    StartNextRound().Forget();
+
+                    // Wait for host to click Ready before starting rounds.
+                    SetPhase(PartyPhase.WaitingForReady);
+                    ResetReadyStates();
+                    ShowPanel_ClientRpc();
+                    BroadcastGameStateText_ClientRpc("Ready to party!");
                 }
             }
             catch (OperationCanceledException) { }
@@ -260,8 +265,11 @@ namespace CosmicShore.Game.Arcade.Party
 
                 ReinitializePanelWithPlayers_ClientRpc();
 
-                if (CurrentPhase == PartyPhase.Lobby)
-                    StartNextRound().Forget();
+                // Wait for the player to click Ready before starting rounds.
+                SetPhase(PartyPhase.WaitingForReady);
+                ResetReadyStates();
+                ShowPanel_ClientRpc();
+                BroadcastGameStateText_ClientRpc("Ready to party!");
             }
             catch (OperationCanceledException) { }
         }
@@ -413,7 +421,12 @@ namespace CosmicShore.Game.Arcade.Party
             switch (CurrentPhase)
             {
                 case PartyPhase.WaitingForReady:
-                    LoadMiniGameEnvironment().Forget();
+                    // First Ready (no game selected yet) → randomize and pick a game.
+                    // Subsequent Ready (game already selected) → load the environment.
+                    if (_netSelectedMiniGameIndex.Value < 0)
+                        StartNextRound().Forget();
+                    else
+                        LoadMiniGameEnvironment().Forget();
                     break;
                 case PartyPhase.MiniGameReady:
                     BeginMiniGamePlay().Forget();
@@ -888,7 +901,8 @@ namespace CosmicShore.Game.Arcade.Party
 
             env.SetActive(true);
             DisableMiniGameControllers(env);
-            CSDebug.Log($"[PartyGame] Activated: '{env.name}' (controllers disabled)");
+            DisableEnvironmentCanvases(env);
+            CSDebug.Log($"[PartyGame] Activated: '{env.name}' (controllers + canvases disabled)");
 
             var spawner = env.GetComponentInChildren<SegmentSpawner>();
             if (spawner) spawner.Initialize();
@@ -927,6 +941,22 @@ namespace CosmicShore.Game.Arcade.Party
             }
         }
 
+        /// <summary>
+        /// Disable the environment's own GameCanvas children to prevent its Ready button,
+        /// cameras, and scoreboard from interfering with the party UI.
+        /// The CountdownTimer is found via GetComponentInChildren on the environment root,
+        /// so it works even if its parent Canvas is disabled.
+        /// </summary>
+        void DisableEnvironmentCanvases(GameObject env)
+        {
```

</details>

### `f5e0bb750` — Guard Player.StartPlayer and ResetForPlay against null Vessel

_Claude, 2026-02-25 13:12:20 +0000_

```text
In party mode, environment SPVIs spawn Player objects during the scene
sweep before PartyGameController can disable them. These players have
no vessel linked, causing NPEs in StartPlayer (Vessel.StartVessel)
and ResetForPlay (Vessel.ResetForPlay).

Early-return when Vessel is null — safe for normal games where Vessel
is always assigned.
```

```text
 Assets/_Scripts/Game/Player/Player.cs | 3 ++-
 1 file changed, 2 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Player/Player.cs b/Assets/_Scripts/Game/Player/Player.cs
index 378953288..09c5bc13a 100644
--- a/Assets/_Scripts/Game/Player/Player.cs
+++ b/Assets/_Scripts/Game/Player/Player.cs
@@ -169,6 +169,7 @@ namespace CosmicShore.Game
 
         public void StartPlayer()
         {
+            if (Vessel == null) return;
             ToggleActive(true);
             Vessel.StartVessel();
             ToggleInputIdle(false);
@@ -188,7 +189,7 @@ namespace CosmicShore.Game
 
         public void ResetForPlay()
         {
-            // Always reset the vessel and make it stationary.
+            if (Vessel == null) return;
             Vessel.ResetForPlay();
             ToggleActive(false);
             
```

</details>

### `1881410aa` — Add dependencies for claude code

_Shombith03, 2026-02-25 22:38:01 +0530_

```text
 Assets/_Scenes/Multiplayer Scenes/MinigamePartyGame.unity          | 878 ++++-------------------------------
 Assets/_Scripts/App/UI/WidescreenLayoutAdapter.cs.meta             |   2 +
 Assets/_Scripts/Game/Arcade/HexRaceController.cs                   |  36 ++
 Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs              |  11 +
 Assets/_Scripts/Game/Arcade/MultiplayerCrystalCaptureController.cs |   3 +
 Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs    |  11 +-
 Assets/_Scripts/Game/Arcade/MultiplayerJoustController.cs          |  31 ++
 Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs   |  68 ++-
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs           | 214 ++++++---
 Assets/_Scripts/Game/Multiplayer/ServerPlayerVesselInitializer.cs  | 216 +++++----
 10 files changed, 535 insertions(+), 935 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1211 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/HexRaceController.cs b/Assets/_Scripts/Game/Arcade/HexRaceController.cs
index 7352aba83..e124536d8 100644
--- a/Assets/_Scripts/Game/Arcade/HexRaceController.cs
+++ b/Assets/_Scripts/Game/Arcade/HexRaceController.cs
@@ -49,6 +49,14 @@ namespace CosmicShore.Game.Arcade
             // Listen for seed changes so late-joining clients can spawn the track
             _netTrackSeed.OnValueChanged += OnTrackSeedChanged;
 
+            // In party mode, skip autonomous track spawning.
+            // PartyGameController will call PartyMode_Activate → SpawnTrackForParty
+            if (IsPartyMode)
+            {
+                CSDebug.Log("[HexRace] OnNetworkSpawn — PARTY MODE, skipping autonomous track spawn.");
+                return;
+            }
+
             if (IsServer)
             {
                 // Server generates the seed after a short delay for intensity sync
@@ -67,6 +75,32 @@ namespace CosmicShore.Game.Arcade
             base.OnNetworkDespawn();
         }
 
+        // ==================== Party Mode API ====================
+
+        public override void PartyMode_Activate()
+        {
+            base.PartyMode_Activate();
+
+            // In party mode, spawn the track when the environment is activated
+            if (IsServer)
+            {
+                _raceEnded = false;
+                _trackSpawned = false;
+                SpawnTrackEarly().Forget();
+            }
+        }
+
+        public override void PartyMode_Deactivate()
+        {
+            base.PartyMode_Deactivate();
+            _raceEnded = false;
+            _trackSpawned = false;
+            WinnerName = "";
+            RaceResultsReady = false;
+        }
+
+        // ==================== Track Generation ====================
+
         /// <summary>
         /// Called on all clients when the server writes a new seed to the NetworkVariable.
         /// </summary>
@@ -130,6 +164,8 @@ namespace CosmicShore.Game.Arcade
             helix.secondOrderRadius = radius;
         }
 
+        // ==================== Race Finish ====================
+
         // Only called by the winner's ScoreTracker
         public void ReportLocalPlayerFinished(float finishTimeSeconds)
         {
diff --git a/Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs b/Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs
index f0fe4b856..6832523b0 100644
--- a/Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs
+++ b/Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs
@@ -26,6 +26,14 @@ namespace CosmicShore.Game.Arcade
         protected virtual bool ShouldResetPlayersOnTurnEnd => false;
         protected virtual bool ShowEndGameSequence => true;
         protected virtual bool UseGolfRules => false;
+
+        /// <summary>
+        /// When true, this controller is being orchestrated by PartyGameController.
+        /// All autonomous lifecycle management (init, ready, countdown, end-game sequence)
+        /// is suppressed. The party controller calls into specific methods as needed.
+        /// Gameplay mechanics (collision tracking, race finishing, crystal counting) still run.
+        /// </summary>
+        public bool IsPartyMode { get; set; }
         
         public void OnReadyClicked()
         {
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
