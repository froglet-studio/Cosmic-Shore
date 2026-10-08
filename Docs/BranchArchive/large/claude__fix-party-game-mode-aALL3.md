# Branch archive: `claude/fix-party-game-mode-aALL3`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

<!-- SUMMARY -->

- **Last commit:** 2026-02-27 by Claude
- **Unmerged commits:** 61
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/129
- **Forked from:** `df1bf2a97` (2026-02-27, Create MenuServerPlayerVesselInitializer.cs.meta)
- **Tip:** `6fe53546f`
- **Files touched (86):**
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
  - `Assets/_Scripts/Controller/Arcade/HexRaceController.cs`
  - `Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs`
  - `Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs`
  - `Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs`
  - `Assets/_Scripts/Controller/Arcade/NetworkScoreTracker.cs`
  - `Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs`
  - `Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkJoustCollisionTurnMonitor.cs`
  - `Assets/_Scripts/Controller/Environment/FlowField/NetworkCrystalManager.cs`
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs`
  - `Assets/_Scripts/Game/AI/AIPilot.cs`
  - `Assets/_Scripts/Game/Arcade/HexRaceController.cs`
  - `Assets/_Scripts/Game/Arcade/HexRaceScoreTracker.cs`
  - `Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs`
  - `Assets/_Scripts/Game/Arcade/MultiplayerCrystalCaptureController.cs`
  - `Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs`
  - `Assets/_Scripts/Game/Arcade/MultiplayerJoustController.cs`
  - `Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs`
  - `Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs`
  - … and 46 more

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

### `16a7390cd` — Fix party mode KeyNotFoundException: move vessel spawning to always-active PartyVesselSpawner

_Claude, 2026-02-25 17:18:03 +0000_

```text
The CPVI on environment GameObjects (which start disabled) was never registered
with Netcode's RPC table. When the SPVI tried calling ClientRpc on it after
activation, it threw KeyNotFoundException.

Fix: Created PartyVesselSpawner on PartyGameManager (always active at scene load,
registered with Netcode). It contains its own ClientRpc for player/vessel
initialization, eliminating the dependency on environment CPVI entirely.

- New: PartyVesselSpawner.cs — handles vessel + AI spawning and repositioning
- SPVI: removed SpawnVesselsForParty() and the party CPVI call chain
- PartyGameController: uses PartyVesselSpawner instead of environment SPVI for
  spawning; all environment SPVIs are now InertMode
```

```text
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs          |  75 +++++----
 Assets/_Scripts/Game/Arcade/Party/PartyVesselSpawner.cs           | 319 ++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Game/Multiplayer/ServerPlayerVesselInitializer.cs |  38 -----
 3 files changed, 359 insertions(+), 73 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 518 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index 0f172a61b..c252c5b38 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -20,9 +20,9 @@ namespace CosmicShore.Game.Arcade.Party
     /// KEY DESIGN:
     /// - Mini-game controllers have IsPartyMode = true → suppresses autonomous lifecycle,
     ///   but gameplay mechanics (collisions, race finish, crystals) still work.
-    /// - Environment SPVIs use a 3-state PartyMode:
-    ///     SpawnMode (first round) → spawns vessels + AI when triggered
-    ///     InertMode (subsequent rounds) → fully inert, vessels repositioned
+    /// - PartyVesselSpawner (on always-active PartyGameManager) handles vessel spawning
+    ///   so ClientRpcs are never called on NetworkBehaviours from disabled GameObjects.
+    /// - Environment SPVIs are always InertMode — they only provide spawn origin data.
     /// - Environment canvases are disabled to prevent UI conflicts.
     /// - Scene camera is disabled during gameplay, re-enabled between rounds.
     /// </summary>
@@ -38,6 +38,10 @@ namespace CosmicShore.Game.Arcade.Party
         [Header("UI")]
         [SerializeField] PartyPausePanel partyPausePanel;
 
+        [Header("Vessel Spawning")]
+        [Tooltip("Lives on PartyGameManager (always active). Handles vessel spawning so RPCs are safe.")]
+        [SerializeField] PartyVesselSpawner vesselSpawner;
+
         [Header("Camera")]
         [Tooltip("The scene-level camera (Mini Game Main Camera). Disabled during gameplay so the vessel camera takes over.")]
         [SerializeField] Camera sceneCamera;
@@ -500,11 +504,9 @@ namespace CosmicShore.Game.Arcade.Party
                 BroadcastGameStateText_ClientRpc("Loading...");
                 gameData.GameMode = selectedMode;
 
-                // Determine SPVI mode: SpawnMode for first round, InertMode for subsequent
+                // SPVI is always InertMode — PartyVesselSpawner handles spawning
                 bool isFirstRound = !_vesselsSpawned;
-                var spviMode = isFirstRound
-                    ? ServerPlayerVesselInitializer.PartyModeState.SpawnMode
-                    : ServerPlayerVesselInitializer.PartyModeState.InertMode;
+                var spviMode = ServerPlayerVesselInitializer.PartyModeState.InertMode;
 
                 ActivateMiniGameEnvironment_ClientRpc(miniGameIndex, (int)spviMode);
                 ResetGameDataForRound_ClientRpc((int)selectedMode);
@@ -512,10 +514,17 @@ namespace CosmicShore.Game.Arcade.Party
 
                 if (isFirstRound)
                 {
-                    // Trigger vessel spawning on the first-round environment's SPVI
-                    TriggerVesselSpawning(miniGameIndex);
+                    // Spawn vessels via PartyVesselSpawner (on always-active GameObject — safe RPCs)
+                    var spawnOrigins = GetSpawnOriginsFromEnv(miniGameIndex);
+                    if (vesselSpawner && spawnOrigins is { Length: > 0 })
+                    {
+                        vesselSpawner.SpawnVesselsForParty(spawnOrigins);
+                    }
+                    else
+                    {
+                        CSDebug.LogError($"[PartyGame] Cannot spawn vessels — vesselSpawner={vesselSpawner != null}, origins={spawnOrigins?.Length ?? 0}");
+                    }
 
-                    // Wait for vessels to spawn and initialize
                     BroadcastGameStateText_ClientRpc("Spawning vessels...");
                     await UniTask.Delay(TimeSpan.FromSeconds(3f), DelayType.UnscaledDeltaTime, cancellationToken: ct);
 
@@ -524,8 +533,8 @@ namespace CosmicShore.Game.Arcade.Party
                 }
                 else
                 {
-                    // Vessels already exist — reposition them to this environment's spawn points
-                    RepositionPlayersToEnvironment_ClientRpc(miniGameIndex);
+                    // Vessels already exist — reposition via PartyVesselSpawner
+                    RepositionPlayersViaSpawner_ClientRpc(miniGameIndex);
                     await UniTask.Delay(TimeSpan.FromSeconds(0.5f), DelayType.UnscaledDeltaTime, cancellationToken: ct);
                 }
 
@@ -540,26 +549,16 @@ namespace CosmicShore.Game.Arcade.Party
         }
 
         /// <summary>
-        /// Server-only: finds the SPVI on the given environment and triggers vessel spawning.
+        /// Returns spawn origin transforms from the SPVI on the given environment.
```

</details>

### `d7b7f7c57` — fix: keep GameCanvas enabled in party mode so HUD and countdown are visible

_Claude, 2026-02-25 17:37:23 +0000_

```text
DisableEnvironmentCanvases was killing ALL Canvas components including
the GameCanvas that holds MiniGameHUD and CountdownTimer. The IsPartyMode
flag already suppresses the mini-game's ready button and lifecycle UI,
so the blanket canvas disable was unnecessary. Now we explicitly ensure
the GameCanvas stays active when environments load.

This fixes both reported issues:
1. GameCanvas no longer turned off when mini-game environment activates
2. Countdown timer + HUD visible, so StartGameplay actually shows gameplay
```

```text
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs | 23 ++++++++++++++++-------
 1 file changed, 16 insertions(+), 7 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index c252c5b38..483aadeec 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -3,6 +3,7 @@ using System;
 using System.Collections.Generic;
 using System.Linq;
 using System.Threading;
+using CosmicShore.Game.UI;
 using CosmicShore.Game.UI.Party;
 using CosmicShore.Soap;
 using Cysharp.Threading.Tasks;
@@ -23,7 +24,7 @@ namespace CosmicShore.Game.Arcade.Party
     /// - PartyVesselSpawner (on always-active PartyGameManager) handles vessel spawning
     ///   so ClientRpcs are never called on NetworkBehaviours from disabled GameObjects.
     /// - Environment SPVIs are always InertMode — they only provide spawn origin data.
-    /// - Environment canvases are disabled to prevent UI conflicts.
+    /// - Environment GameCanvas stays enabled for HUD; IsPartyMode suppresses mini-game UI.
     /// - Scene camera is disabled during gameplay, re-enabled between rounds.
     /// </summary>
     public class PartyGameController : NetworkBehaviour
@@ -963,8 +964,8 @@ namespace CosmicShore.Game.Arcade.Party
             SetPartyModeOnEnvironment(env, partyModeState);
 
             env.SetActive(true);
-            DisableEnvironmentCanvases(env);
-            CSDebug.Log($"[PartyGame] Activated: '{env.name}' (SPVI={partyModeState}, canvases disabled)");
+            EnableGameCanvas(env);
+            CSDebug.Log($"[PartyGame] Activated: '{env.name}' (SPVI={partyModeState}, GameCanvas enabled)");
 
             // Initialize segment spawner if present
             var spawner = env.GetComponentInChildren<SegmentSpawner>();
@@ -1064,12 +1065,20 @@ namespace CosmicShore.Game.Arcade.Party
             }
         }
 
-        void DisableEnvironmentCanvases(GameObject env)
+        /// <summary>
+        /// Re-enables the GameCanvas in the environment so the MiniGameHUD, countdown timer,
+        /// and gameplay UI are visible. The IsPartyMode flag on the controller already
+        /// suppresses the mini-game's own ready button and lifecycle UI.
+        /// </summary>
+        void EnableGameCanvas(GameObject env)
         {
-            var canvases = env.GetComponentsInChildren<Canvas>(true);
-            foreach (var canvas in canvases)
+            var gameCanvas = env.GetComponentInChildren<GameCanvas>(true);
+            if (gameCanvas)
             {
-                canvas.enabled = false;
+                var canvas = gameCanvas.GetComponent<Canvas>();
+                if (canvas) canvas.enabled = true;
+                gameCanvas.gameObject.SetActive(true);
+                CSDebug.Log($"[PartyGame] GameCanvas enabled on '{env.name}'");
             }
         }
 
```

</details>

### `1a4a7bcb8` — fix: hide party panel on environment load and skip MiniGameReady phase

_Claude, 2026-02-25 17:51:23 +0000_

```text
LoadMiniGameEnvironment now hides the panel and proceeds directly to
gameplay (countdown + turn start) instead of showing the panel and
requiring a third Ready press via MiniGameReady phase. This matches
the expected flow: pick game → load environment → play.

Also adds a debug log to MultiplayerDomainGamesController.OnReadyClicked_
when IsPartyMode is true so the silent no-op is visible in console.
```

```text
 Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs |  6 +++++-
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs        | 21 ++++++++++++++++-----
 2 files changed, 21 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs b/Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs
index ae7365c5d..3aeb2bf6e 100644
--- a/Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs
+++ b/Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs
@@ -41,7 +41,11 @@ namespace CosmicShore.Game.Arcade
         protected override void OnReadyClicked_()
         {
             // In party mode, the party panel handles ready — ignore mini-game ready clicks
-            if (IsPartyMode) return;
+            if (IsPartyMode)
+            {
+                CSDebug.Log("[MultiplayerDomainGamesController] OnReadyClicked_ ignored — IsPartyMode=true. Use the party panel's Ready button instead.");
+                return;
+            }
 
             RaiseToggleReadyButtonEvent(false);
             OnReadyClicked_ServerRpc(gameData.LocalPlayer.Name);
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index 483aadeec..bb1c0c8e9 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -428,6 +428,8 @@ namespace CosmicShore.Game.Arcade.Party
                     else
                         LoadMiniGameEnvironment().Forget();
                     break;
+                // MiniGameReady phase is no longer used — LoadMiniGameEnvironment
+                // now proceeds directly to gameplay. Kept as safety fallback.
                 case PartyPhase.MiniGameReady:
                     BeginMiniGamePlay().Forget();
                     break;
@@ -539,12 +541,21 @@ namespace CosmicShore.Game.Arcade.Party
                     await UniTask.Delay(TimeSpan.FromSeconds(0.5f), DelayType.UnscaledDeltaTime, cancellationToken: ct);
                 }
 
-                SetPhase(PartyPhase.MiniGameReady);
-                ResetReadyStates();
-                ShowPanel_ClientRpc();
-
                 string modeName = GetMiniGameDisplayName(selectedMode);
-                BroadcastGameStateText_ClientRpc($"{modeName} — Ready to play!");
+                BroadcastGameStateText_ClientRpc($"{modeName} — Starting...");
+
+                // Hide panel and go straight to gameplay — no extra ready step
+                HidePanel_ClientRpc();
+                DisableSceneCamera_ClientRpc();
+
+                await UniTask.Delay(
+                    TimeSpan.FromSeconds(config.PostCountdownDelaySeconds),
+                    DelayType.UnscaledDeltaTime,
+                    cancellationToken: ct);
+
+                SetPhase(PartyPhase.Playing);
+                StartGameplay_ClientRpc();
+                RunRoundTimer(ct).Forget();
             }
             catch (OperationCanceledException) { }
         }
```

</details>

### `d483c773f` — fix: let mini-game's own ready button work in party mode

_Claude, 2026-02-25 18:02:05 +0000_

```text
Removed IsPartyMode guards from the ready/setup flow so the mini-game's
native Ready button drives countdown + SetPlayersActive + StartTurn:

- MiniGameControllerBase: RaiseToggleReadyButtonEvent no longer blocked
- MultiplayerMiniGameControllerBase: SetupNewRound/SetupNewTurn show
  ready button, PartyMode_Activate calls InitializeAfterDelay
- MultiplayerDomainGamesController: OnReadyClicked_ and ServerRpc flow
  through, SetupNewRound shows ready button
- PartyGameController: LoadMiniGameEnvironment hides party panel and
  unpauses but lets mini-game handle gameplay start

The ExecuteServerGameEnd guard remains so the party controller still
owns round transitions and scoring.
```

```text
 Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs            |  3 ---
 Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs  | 15 +--------------
 Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs | 12 +++++-------
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs         | 19 +++++++++++--------
 4 files changed, 17 insertions(+), 32 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 132 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs b/Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs
index 6832523b0..e2d9dd5ec 100644
--- a/Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs
+++ b/Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs
@@ -55,9 +55,6 @@ namespace CosmicShore.Game.Arcade
         
         protected void RaiseToggleReadyButtonEvent(bool enable)
         {
-            // In party mode, don't raise the mini-game's own ready button events —
-            // the party panel handles all UI.
-            if (IsPartyMode) return;
             _onToggleReadyButton?.Raise(enable);
         }
         
diff --git a/Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs b/Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs
index 3aeb2bf6e..78d4c8838 100644
--- a/Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs
+++ b/Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs
@@ -40,13 +40,6 @@ namespace CosmicShore.Game.Arcade
 
         protected override void OnReadyClicked_()
         {
-            // In party mode, the party panel handles ready — ignore mini-game ready clicks
-            if (IsPartyMode)
-            {
-                CSDebug.Log("[MultiplayerDomainGamesController] OnReadyClicked_ ignored — IsPartyMode=true. Use the party panel's Ready button instead.");
-                return;
-            }
-
             RaiseToggleReadyButtonEvent(false);
             OnReadyClicked_ServerRpc(gameData.LocalPlayer.Name);
         }
@@ -54,9 +47,6 @@ namespace CosmicShore.Game.Arcade
         [ServerRpc(RequireOwnership = false)]
         void OnReadyClicked_ServerRpc(string playerName)
         {
-            // In party mode, don't process mini-game ready clicks
-            if (IsPartyMode) return;
-
             readyClientCount++;
 
             // Debug log to help track this state if issues persist
@@ -93,10 +83,7 @@ namespace CosmicShore.Game.Arcade
                 readyClientCount = 0;
             }
 
-            // In party mode, don't show the mini-game's ready button
-            if (!IsPartyMode)
-                RaiseToggleReadyButtonEvent(true);
-
+            RaiseToggleReadyButtonEvent(true);
             base.SetupNewRound();
         }
 
diff --git a/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs b/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
index bdb51c2aa..5f5f7edb2 100644
--- a/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
+++ b/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
@@ -71,7 +71,11 @@ namespace CosmicShore.Game.Arcade
                 gameData.OnMiniGameTurnEnd.OnRaised += HandleTurnEnd;
             }
 
-            CSDebug.Log($"[{GetType().Name}] PartyMode_Activate — subscribed to turn events.");
+            // Initialize the mini-game and show its ready button so the
+            // player can click Ready to start the countdown + gameplay.
+            InitializeAfterDelay().Forget();
+
+            CSDebug.Log($"[{GetType().Name}] PartyMode_Activate — subscribed to turn events, initializing.");
         }
 
         /// <summary>
@@ -215,18 +219,12 @@ namespace CosmicShore.Game.Arcade
         protected override void SetupNewTurn()
         {
             base.SetupNewTurn();
-
-            // In party mode, don't show the mini-game's ready button
-            if (IsPartyMode) return;
             if (IsServer) ShowReadyButton_ClientRpc();
         }
```

</details>

### `bbbd8df5d` — fix: auto-resolve PartyVesselSpawner if not wired in Inspector

_Claude, 2026-02-25 18:11:37 +0000_

```text
vesselSpawner field was null at runtime causing "Cannot spawn vessels"
error. Added FindAnyObjectByType fallback in OnNetworkSpawn so the
spawner is found automatically when not assigned in the Inspector.
```

```text
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs | 10 ++++++++++
 1 file changed, 10 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index 864e0d9f3..04f2dfa4e 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -97,6 +97,16 @@ namespace CosmicShore.Game.Arcade.Party
             gameData.OnMiniGameEnd += OnMiniGameEnded;
             gameData.OnPlayerAdded += HandlePlayerAdded;
 
+            // Auto-resolve vesselSpawner if not assigned in Inspector
+            if (!vesselSpawner)
+            {
+                vesselSpawner = FindAnyObjectByType<PartyVesselSpawner>();
+                if (vesselSpawner)
+                    CSDebug.Log("[PartyGame] Auto-resolved PartyVesselSpawner reference.");
+                else
+                    CSDebug.LogError("[PartyGame] PartyVesselSpawner not found in scene! Vessel spawning will fail.");
+            }
+
             _roundResults.Clear();
             for (int i = 0; i < config.TotalRounds; i++)
                 _roundResults.Add(new PartyRoundResult { RoundIndex = i });
```

</details>

### `da9b55d3d` — fix: bypass all RPCs on deactivated mini-game controllers in party mode

_Claude, 2026-02-25 18:39:59 +0000_

```text
Mini-game environments are SetActive(false) during network spawn, so
Netcode never registers their RPCs. All RPC calls from the mini-game
controller throw KeyNotFoundException in party mode.

Fix: Since the host is both client and server in party mode (no remote
human players), all RPC work is done locally instead:

- OnReadyClicked_: start countdown via lambda callback, not ServerRpc
- HandleTurnEnd: do turn-end work locally, skip SyncTurnEnd_ClientRpc
- ExecuteServerRoundEnd: skip SyncRoundEnd_ClientRpc
- SetupNewRound/SetupNewTurn: raise ready button event locally

Also fix NetworkStatsManager.OnDisable null ref when duplicate singleton
is destroyed before _netcodeHooks is assigned.
```

```text
 Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs  | 22 ++++++++++++++++++++++
 Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs | 33 +++++++++++++++++++++++++++++----
 Assets/_Scripts/Game/Multiplayer/NetworkStatsManager.cs          |  3 ++-
 3 files changed, 53 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 110 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs b/Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs
index 78d4c8838..fb10a3909 100644
--- a/Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs
+++ b/Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs
@@ -41,6 +41,28 @@ namespace CosmicShore.Game.Arcade
         protected override void OnReadyClicked_()
         {
             RaiseToggleReadyButtonEvent(false);
+
+            if (IsPartyMode)
+            {
+                // Mini-game env was deactivated during network spawn — RPCs
+                // are not registered. Start countdown directly since the host
+                // is both client and server in party mode.
+                if (countdownTimer != null)
+                {
+                    countdownTimer.BeginCountdown(() =>
+                    {
+                        gameData.SetPlayersActive();
+                        gameData.StartTurn();
+                    });
+                }
+                else
+                {
+                    gameData.SetPlayersActive();
+                    gameData.StartTurn();
+                }
+                return;
+            }
+
             OnReadyClicked_ServerRpc(gameData.LocalPlayer.Name);
         }
 
diff --git a/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs b/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
index 5f5f7edb2..09ea445ae 100644
--- a/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
+++ b/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
@@ -142,7 +142,20 @@ namespace CosmicShore.Game.Arcade
         void HandleTurnEnd()
         {
             if (!IsServer) return;
-            SyncTurnEnd_ClientRpc();
+
+            if (IsPartyMode)
+            {
+                // RPCs not registered — do turn-end work locally.
+                // Host is the only human client in party mode.
+                if (ShouldResetPlayersOnTurnEnd)
+                    gameData.ResetPlayers();
+                OnTurnEndedCustom();
+            }
+            else
+            {
+                SyncTurnEnd_ClientRpc();
+            }
+
             ExecuteServerTurnEnd();
         }
 
@@ -171,7 +184,11 @@ namespace CosmicShore.Game.Arcade
         void ExecuteServerRoundEnd()
         {
             if (!IsServer) return;
-            SyncRoundEnd_ClientRpc();
+
+            // Sync to remote clients (not needed in party mode — RPCs not registered)
+            if (!IsPartyMode)
+                SyncRoundEnd_ClientRpc();
+
             gameData.RoundsPlayed++;
             gameData.InvokeMiniGameRoundEnd();
             OnRoundEndedCustom();
@@ -219,13 +236,21 @@ namespace CosmicShore.Game.Arcade
         protected override void SetupNewTurn()
         {
             base.SetupNewTurn();
-            if (IsServer) ShowReadyButton_ClientRpc();
+
+            if (IsPartyMode)
+                RaiseToggleReadyButtonEvent(true);
```

</details>

### `01115126a` — fix: defer env deactivation so crystal managers get OnNetworkSpawn + fix player name lookup

_Claude, 2026-02-25 19:08:00 +0000_

```text
Two root causes fixed:

1. Crystal spawning: NetworkCrystalManager subscribes to
   OnMiniGameTurnStarted in OnNetworkSpawn(). When environments were
   deactivated synchronously in PartyGameController.OnNetworkSpawn,
   crystal managers on those envs never got their OnNetworkSpawn called.
   Fix: delay env deactivation by one frame via DeactivateEnvironmentsDeferred()
   so all scene-placed NetworkObjects finish spawning first. This also
   fixes RPC registration and NetworkList connectivity.

2. Player not found: StartSoloLobby registered the human player using
   gameData.LocalPlayer?.Name which is null before vessels spawn,
   falling back to "Player 1". Later, OnLocalPlayerReady used the real
   name from LocalPlayer. Fix: use LocalPlayerDisplayName (set at login)
   as primary source, and reconcile placeholder names in HandlePlayerAdded.
```

```text
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs | 52 ++++++++++++++++++++++++++++++++++++++++++++--
 1 file changed, 50 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 99 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index 04f2dfa4e..1922e28ef 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -113,13 +113,22 @@ namespace CosmicShore.Game.Arcade.Party
 
             _vesselsSpawned = false;
 
+            // Set IsPartyMode on all environments immediately so their
+            // OnNetworkSpawn callbacks know they're in party mode.
             foreach (var env in miniGameEnvironments)
             {
                 if (!env) continue;
                 SetPartyModeOnEnvironment(env, ServerPlayerVesselInitializer.PartyModeState.InertMode);
-                env.SetActive(false);
             }
 
+            // CRITICAL: Delay deactivation by one frame so all scene-placed
+            // NetworkObjects (crystal managers, mini-game controllers, etc.)
+            // get their OnNetworkSpawn called and RPCs registered BEFORE
+            // the environments are disabled. Without this delay, OnNetworkSpawn
+            // never fires on objects inside environments, breaking crystals,
+            // RPCs, and NetworkList replication.
+            DeactivateEnvironmentsDeferred().Forget();
+
             // Scene camera stays on during lobby (shows skybox/nucleus)
             if (sceneCamera) sceneCamera.enabled = true;
 
@@ -160,6 +169,20 @@ namespace CosmicShore.Game.Arcade.Party
             base.OnNetworkDespawn();
         }
 
+        async UniTaskVoid DeactivateEnvironmentsDeferred()
+        {
+            // Wait one frame so Netcode finishes its spawn sweep on all scene objects.
+            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
+
+            foreach (var env in miniGameEnvironments)
+            {
+                if (!env) continue;
+                env.SetActive(false);
+            }
+
+            CSDebug.Log("[PartyGame] Environments deactivated (deferred — all OnNetworkSpawn complete).");
+        }
+
         #endregion
 
         #region Phase Management
@@ -217,6 +240,21 @@ namespace CosmicShore.Game.Arcade.Party
             if (player != null)
                 isAI = player.IsInitializedAsAI;
 
+            // If this is the human player but was registered under a fallback name
+            // (e.g. "Player 1" because LocalPlayer was null during solo lobby setup),
+            // update the existing entry to use the real name.
+            if (!isAI && !_playerStates.Any(p => p.PlayerName == playerName))
+            {
+                var placeholder = _playerStates.FirstOrDefault(p =>
+                    !p.IsAIReplacement && p.PlayerName != playerName);
+                if (placeholder != null)
+                {
+                    CSDebug.Log($"[PartyGame] Renaming player '{placeholder.PlayerName}' → '{playerName}'");
+                    placeholder.PlayerName = playerName;
+                    return;
+                }
+            }
+
             CSDebug.Log($"[PartyGame] HandlePlayerAdded: '{playerName}', domain={domain}, isAI={isAI}");
             OnPlayerJoined(playerName, domain, isAI);
         }
@@ -267,7 +305,11 @@ namespace CosmicShore.Game.Arcade.Party
 
                 if (!_playerStates.Any(p => !p.IsAIReplacement))
                 {
-                    string humanName = gameData.LocalPlayer?.Name ?? "Player 1";
+                    // Use LocalPlayerDisplayName (set during login/profile init)
+                    // because LocalPlayer may be null until vessels are spawned.
+                    string humanName = !string.IsNullOrEmpty(gameData.LocalPlayerDisplayName)
+                        ? gameData.LocalPlayerDisplayName
```

</details>

### `bbb4b686f` — fix: properly sequence end-game cinematic before round transition in party mode

_Claude, 2026-02-25 19:33:25 +0000_

```text
The end-game cinematic was not showing because ForceEndRound_ClientRpc
fired both InvokeWinnerCalculated (starts cinematic) and InvokeMiniGameEnd
(triggers CompleteRound → deactivates environment) simultaneously. The
environment was deactivated while the cinematic coroutine was still running.

Changes:
- Remove InvokeMiniGameEnd from ForceEndRound_ClientRpc — let cinematic
  finish first
- Subscribe PartyGameController to OnShowGameEndScreen (fires when cinematic
  completes) to trigger CompleteRound at the right time
- Add IsPartyMode to EndGameCinematicController — skip connecting panel and
  ResetGameForNewRound in party mode (party controller handles transitions)
- Set IsPartyMode on cinematic controllers in SetPartyModeOnEnvironment
- Add 60s safety fallback in case cinematic never fires ShowGameEndScreen
```

```text
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs             | 47 +++++++++++++++++++++++++++++++++-
 Assets/_Scripts/Utility/DataContainers/EndGameCinematicController.cs |  6 ++++-
 2 files changed, 51 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 124 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index 1922e28ef..8f13880ea 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -3,6 +3,7 @@ using System;
 using System.Collections.Generic;
 using System.Linq;
 using System.Threading;
+using CosmicShore.Game.Cinematics;
 using CosmicShore.Game.UI;
 using CosmicShore.Game.UI.Party;
 using CosmicShore.Soap;
@@ -97,6 +98,9 @@ namespace CosmicShore.Game.Arcade.Party
             gameData.OnMiniGameEnd += OnMiniGameEnded;
             gameData.OnPlayerAdded += HandlePlayerAdded;
 
+            if (gameData.OnShowGameEndScreen != null)
+                gameData.OnShowGameEndScreen.OnRaised += OnShowGameEndScreen;
+
             // Auto-resolve vesselSpawner if not assigned in Inspector
             if (!vesselSpawner)
             {
@@ -161,6 +165,9 @@ namespace CosmicShore.Game.Arcade.Party
             gameData.OnMiniGameEnd -= OnMiniGameEnded;
             gameData.OnPlayerAdded -= HandlePlayerAdded;
 
+            if (gameData.OnShowGameEndScreen != null)
+                gameData.OnShowGameEndScreen.OnRaised -= OnShowGameEndScreen;
+
             _lobbyCts?.Cancel();
             _lobbyCts?.Dispose();
             _roundCts?.Cancel();
@@ -688,6 +695,20 @@ namespace CosmicShore.Game.Arcade.Party
                 {
                     CSDebug.Log("[PartyGame] Round timer expired.");
                     ForceEndRound_ClientRpc();
+
+                    // Safety fallback: if OnShowGameEndScreen never fires (e.g., no
+                    // cinematic controller on this environment), force CompleteRound
+                    // after a generous timeout so the party doesn't get stuck.
+                    await UniTask.Delay(
+                        TimeSpan.FromSeconds(60),
+                        DelayType.UnscaledDeltaTime,
+                        cancellationToken: ct);
+
+                    if (IsServer && CurrentPhase == PartyPhase.Playing)
+                    {
+                        CSDebug.LogWarning("[PartyGame] Cinematic timeout — forcing CompleteRound.");
+                        gameData.InvokeMiniGameEnd();
+                    }
                 }
             }
             catch (OperationCanceledException) { }
@@ -700,7 +721,10 @@ namespace CosmicShore.Game.Arcade.Party
             gameData.SortRoundStats(false);
             gameData.CalculateDomainStats(false);
             gameData.InvokeWinnerCalculated();
-            gameData.InvokeMiniGameEnd();
+            // DO NOT fire InvokeMiniGameEnd here — the EndGameCinematicController
+            // needs to run its full sequence first (score reveal → Continue button).
+            // OnShowGameEndScreen fires when the cinematic finishes, which triggers
+            // CompleteRound via OnShowGameEndScreen handler below.
         }
 
         void OnMiniGameWinnerCalculated()
@@ -746,6 +770,21 @@ namespace CosmicShore.Game.Arcade.Party
             CompleteRound().Forget();
         }
 
+        /// <summary>
+        /// Called after the EndGameCinematicController finishes its full sequence
+        /// (score reveal → Continue button → hide). This is the signal to transition
+        /// to the next round. We fire InvokeMiniGameEnd so other systems (score trackers,
+        /// etc.) get notified, and OnMiniGameEnded triggers CompleteRound.
+        /// </summary>
+        void OnShowGameEndScreen()
+        {
+            if (!IsServer) return;
+            if (CurrentPhase != PartyPhase.Playing) return;
+
```

</details>

### `b40400f9f` — fix: proper OnEnable/OnDisable lifecycle for NetworkBehaviours in party mode

_Claude, 2026-02-25 20:01:07 +0000_

```text
Root cause: when party mode toggles environments via SetActive, OnDisable
unsubscribes events but OnEnable/OnNetworkSpawn never re-subscribes because
OnNetworkSpawn only fires once at scene load. This broke turn monitoring,
crystal spawning, score tracking, and end-game detection for all rounds
after the first.

Changes:
- NetworkTurnMonitorController: re-subscribe in OnEnable when IsSpawned
  (was empty override that killed all event subscriptions on reactivation)
- NetworkCrystalManager: add OnEnable/OnDisable to manage crystal event
  subscriptions so only the active environment's manager responds
- NetworkScoreTracker: add OnEnable/OnDisable to manage score event
  subscriptions so only the active environment's tracker fires RPCs
- PartyGameController: disable MiniGamePlayerSpawnerAdapter in party mode
  to prevent duplicate player/vessel spawning; remove OnMiniGameEnd
  subscription (game-specific controllers fire it before cinematic);
  OnShowGameEndScreen now triggers CompleteRound after cinematic finishes
```

```text
 Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs                  | 39 +++++++++++++++++++++++++++-------
 Assets/_Scripts/Game/Arcade/NetworkTurnMonitorController.cs         |  8 ++++++-
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs            | 40 ++++++++++++++++++++---------------
 Assets/_Scripts/Game/Environment/FlowField/NetworkCrystalManager.cs | 32 ++++++++++++++++++++++++++--
 4 files changed, 91 insertions(+), 28 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 234 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs b/Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs
index 58b8b555f..cf85cf7b5 100644
--- a/Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs
+++ b/Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs
@@ -7,26 +7,49 @@ namespace CosmicShore.Game.Arcade
     {
         public override void OnNetworkSpawn()
         {
-            if (!IsServer)
-                return;
+            if (!IsServer) return;
+            SubscribeScoreEvents();
+        }
+
+        public override void OnNetworkDespawn()
+        {
+            if (!IsServer) return;
+            UnsubscribeScoreEvents();
+        }
+
+        /// <summary>
+        /// Re-subscribe when the environment is reactivated (party mode SetActive
+        /// toggling). Prevents inactive environments' score trackers from
+        /// responding to events and firing conflicting RPCs.
+        /// </summary>
+        private void OnEnable()
+        {
+            if (IsSpawned && IsServer)
+                SubscribeScoreEvents();
+        }
+
+        private void OnDisable()
+        {
+            if (!IsServer) return;
+            UnsubscribeScoreEvents();
+        }
 
+        void SubscribeScoreEvents()
+        {
             gameData.OnInitializeGame += InitializeScoringMode;
             gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
             gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
             gameData.OnMiniGameEnd += CalculateWinnerOnServer;
-            OnClickToMainMenu.OnRaised += OnTurnEnded;
+            if (OnClickToMainMenu) OnClickToMainMenu.OnRaised += OnTurnEnded;
         }
 
-        public override void OnNetworkDespawn()
+        void UnsubscribeScoreEvents()
         {
-            if (!IsServer)
-                return;
-
             gameData.OnInitializeGame -= InitializeScoringMode;
             gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
             gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
             gameData.OnMiniGameEnd -= CalculateWinnerOnServer;
-            OnClickToMainMenu.OnRaised -= OnTurnEnded;
+            if (OnClickToMainMenu) OnClickToMainMenu.OnRaised -= OnTurnEnded;
         }
 
         private void CalculateWinnerOnServer()
diff --git a/Assets/_Scripts/Game/Arcade/NetworkTurnMonitorController.cs b/Assets/_Scripts/Game/Arcade/NetworkTurnMonitorController.cs
index 895a207d4..d213e9373 100644
--- a/Assets/_Scripts/Game/Arcade/NetworkTurnMonitorController.cs
+++ b/Assets/_Scripts/Game/Arcade/NetworkTurnMonitorController.cs
@@ -4,6 +4,12 @@ namespace CosmicShore.Game.Arcade
     {
         protected override void OnEnable()
         {
+            // Re-subscribe when the environment is reactivated (e.g., party mode
+            // toggling SetActive). OnDisable (base) already unsubscribes + stops
+            // monitors, so we must re-subscribe here. Guard on IsSpawned to avoid
+            // subscribing before OnNetworkSpawn has fired during initial scene load.
+            if (IsSpawned)
+                SubscribeToEvents();
         }
 
         public override void OnNetworkSpawn()
@@ -23,4 +29,4 @@ namespace CosmicShore.Game.Arcade
```

</details>

### `90c87deef` — fix: scorecards, game end, and cleanup for party mode mini-games

_Claude, 2026-02-25 20:50:00 +0000_

```text
4 root causes fixed:

1. Scorecards/HUD never show: MultiplayerHUD has RequireClientReady=true
   but InvokeClientReady was only fired on round 1 by PartyVesselSpawner.
   Now fires InvokeClientReady_ClientRpc after repositioning on round 2+.
   Also raises OnResetForReplay to start the HUD's connecting panel flow.

2. CrystalCapture game end broken: ExecuteServerGameEnd returned early in
   party mode without firing InvokeWinnerCalculated. HexRace/Joust worked
   because they have custom RPCs. Now fires SortRoundStats +
   CalculateDomainStats + InvokeWinnerCalculated in party mode (the
   isRunning guard in EndGameCinematicController prevents double-starts
   for HexRace/Joust whose RPCs fire events first).

3. Game state counters not reset: RoundsPlayed and TurnsTakenThisRound
   persisted across party rounds. CrystalCapture (numberOfRounds=1) would
   immediately trigger game end on second play. Now reset in
   ResetGameDataForRound_ClientRpc.

4. Vessel state leaks between rounds: EndGameCinematicController skipped
   ResetGameForNewRound() in party mode, leaving AI enabled, input
   disabled, camera stuck. Now always calls ResetGameForNewRound() —
   only the connecting panel is skipped in party mode.
```

```text
 Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs     | 13 ++++++++++---
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs             | 24 ++++++++++++++++++++++++
 Assets/_Scripts/Utility/DataContainers/EndGameCinematicController.cs |  6 +++++-
 3 files changed, 39 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 89 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs b/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
index 09ea445ae..49c665497 100644
--- a/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
+++ b/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
@@ -212,11 +212,18 @@ namespace CosmicShore.Game.Arcade
         {
             if (!IsServer) return;
 
-            // In party mode, don't run the mini-game's own end sequence.
-            // The PartyGameController listens for gameData events and handles transitions.
             if (IsPartyMode)
             {
-                CSDebug.Log($"[{GetType().Name}] ExecuteServerGameEnd — party mode, skipping SyncGameEnd.");
+                // In party mode, trigger the end-game cinematic locally.
+                // InvokeWinnerCalculated starts the cinematic → score reveal → Continue.
+                // Don't fire InvokeMiniGameEnd — CompleteRound is driven by
+                // OnShowGameEndScreen after the cinematic finishes.
+                // The isRunning guard in EndGameCinematicController prevents double-starts
+                // when game-specific controllers (HexRace, Joust) fire this first.
+                gameData.SortRoundStats(UseGolfRules);
+                gameData.CalculateDomainStats(UseGolfRules);
+                gameData.InvokeWinnerCalculated();
+                CSDebug.Log($"[{GetType().Name}] ExecuteServerGameEnd — party mode, fired WinnerCalculated.");
                 return;
             }
 
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index f0e236179..67147e58d 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -608,6 +608,11 @@ namespace CosmicShore.Game.Arcade.Party
                     // Vessels already exist — reposition via PartyVesselSpawner
                     RepositionPlayersViaSpawner_ClientRpc(miniGameIndex);
                     await UniTask.Delay(TimeSpan.FromSeconds(0.5f), DelayType.UnscaledDeltaTime, cancellationToken: ct);
+
+                    // Signal client-ready so MultiplayerHUD (RequireClientReady=true)
+                    // proceeds past its wait loop and shows the ready button.
+                    // For round 1, PartyVesselSpawner.DelayInvokeClientReady handles this.
+                    InvokeClientReady_ClientRpc();
                 }
 
                 string modeName = GetMiniGameDisplayName(selectedMode);
@@ -1111,8 +1116,27 @@ namespace CosmicShore.Game.Arcade.Party
         void ResetGameDataForRound_ClientRpc(int gameMode)
         {
             gameData.GameMode = (GameModes)gameMode;
+
+            // Reset per-minigame counters so the new round starts fresh.
+            // Without this, CrystalCapture (numberOfRounds=1) would immediately
+            // trigger ExecuteServerGameEnd on its second play.
+            gameData.TurnsTakenThisRound = 0;
+            gameData.RoundsPlayed = 0;
+
             if (gameData.RoundStatsList != null && gameData.RoundStatsList.Count > 0)
                 gameData.ResetStatsDataForReplay();
+
+            // Raise OnResetForReplay to clean up stale crystals, trails, prisms,
+            // and lifeforms from a previous play of this mini-game environment.
+            // Also starts MiniGameHUD's connecting panel → ready button flow.
+            if (gameData.OnResetForReplay != null)
+                gameData.OnResetForReplay.Raise();
+        }
+
+        [ClientRpc]
+        void InvokeClientReady_ClientRpc()
+        {
+            gameData.InvokeClientReady();
         }
 
         /// <summary>
diff --git a/Assets/_Scripts/Utility/DataContainers/EndGameCinematicController.cs b/Assets/_Scripts/Utility/DataContainers/EndGameCinematicController.cs
index d3b7ab9ac..3a4a2e36c 100644
--- a/Assets/_Scripts/Utility/DataContainers/EndGameCinematicController.cs
+++ b/Assets/_Scripts/Utility/DataContainers/EndGameCinematicController.cs
@@ -118,9 +118,13 @@ namespace CosmicShore.Game.Cinematics
             {
                 view.ShowConnectingPanel();
                 yield return new WaitForSeconds(cinematic.connectingPanelDuration);
-                ResetGameForNewRound();
             }
```

</details>

### `223476df2` — fix: resolve cinematic definition by mini-game scene name in party mode

_Claude, 2026-02-25 20:58:32 +0000_

```text
EndGameCinematicController.ResolveCinematicForThisScene() used
SceneManager.GetActiveScene().name which is always "MinigamePartyGame"
in party mode, never the individual mini-game scene name. The cinematic
library has entries keyed by mini-game scene names (MinigameHexRace,
MinigameJoust_Gameplay, etc.) so the lookup always failed, returning
null and skipping the entire score reveal sequence.

Fix:
- PartyVesselSpawner: expose GetSceneNameForMode() to map GameModes
  to scene names via the existing SO_GameList
- PartyGameController: set gameData.SceneName per round before
  activating the environment
- EndGameCinematicController: fall back to gameData.SceneName when
  the active scene lookup fails and IsPartyMode is true
```

```text
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs             |  6 ++++++
 Assets/_Scripts/Game/Arcade/Party/PartyVesselSpawner.cs              |  5 +++++
 Assets/_Scripts/Utility/DataContainers/EndGameCinematicController.cs | 11 ++++++++++-
 3 files changed, 21 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index 67147e58d..b85b6c534 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -576,6 +576,12 @@ namespace CosmicShore.Game.Arcade.Party
                 BroadcastGameStateText_ClientRpc("Loading...");
                 gameData.GameMode = selectedMode;
 
+                // Set SceneName so EndGameCinematicController can resolve the correct
+                // cinematic definition (it normally uses the active scene name, but in
+                // party mode the active scene is always the party scene).
+                if (vesselSpawner)
+                    gameData.SceneName = vesselSpawner.GetSceneNameForMode(selectedMode) ?? "";
+
                 // SPVI is always InertMode — PartyVesselSpawner handles spawning
                 bool isFirstRound = !_vesselsSpawned;
                 var spviMode = ServerPlayerVesselInitializer.PartyModeState.InertMode;
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyVesselSpawner.cs b/Assets/_Scripts/Game/Arcade/Party/PartyVesselSpawner.cs
index 6ddc10096..c51d7f8ca 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyVesselSpawner.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyVesselSpawner.cs
@@ -225,6 +225,11 @@ namespace CosmicShore.Game.Arcade.Party
             return VesselClassType.Sparrow;
         }
 
+        public string GetSceneNameForMode(GameModes mode)
+        {
+            return FindGameByMode(mode)?.SceneName;
+        }
+
         SO_ArcadeGame FindGameByMode(GameModes mode)
         {
             if (gameList?.Games == null) return null;
diff --git a/Assets/_Scripts/Utility/DataContainers/EndGameCinematicController.cs b/Assets/_Scripts/Utility/DataContainers/EndGameCinematicController.cs
index 3a4a2e36c..b9c5c069a 100644
--- a/Assets/_Scripts/Utility/DataContainers/EndGameCinematicController.cs
+++ b/Assets/_Scripts/Utility/DataContainers/EndGameCinematicController.cs
@@ -340,7 +340,16 @@ namespace CosmicShore.Game.Cinematics
                 CSDebug.Log($"Found cinematic definition for scene: {sceneName}");
                 return fromLibrary;
             }
-            
+
+            // In party mode the active scene is the party scene, not the mini-game.
+            // Fall back to gameData.SceneName which the party controller sets per round.
+            if (IsPartyMode && gameData && !string.IsNullOrEmpty(gameData.SceneName) &&
+                sceneCinematicLibrary && sceneCinematicLibrary.TryGet(gameData.SceneName, out var fromGameData))
+            {
+                CSDebug.Log($"Found cinematic definition via gameData.SceneName: {gameData.SceneName}");
+                return fromGameData;
+            }
+
             CSDebug.LogWarning($"No cinematic definition found for scene: {sceneName}");
             return null;
         }
```

</details>

### `9aca357e1` — fix: crystal spawning in party mode for HexRace and CrystalCapture

_Claude, 2026-02-25 21:26:19 +0000_

```text
In party mode, environment GameObjects are deactivated during network
spawn and reactivated per round via SetActive. This leaves
NetworkCrystalManager.IsSpawned potentially false after reactivation
(same root cause as RPCs being unregistered on mini-game controllers).

When IsSpawned is false:
- OnEnable skipped event subscription (gated on IsSpawned)
- OnTurnStarted returned early (IsServer false when !IsSpawned)
- No positions generated → no crystals spawned

Fix adds IsPartyMode flag to CrystalManager (set via
SetPartyModeOnEnvironment alongside controllers and cinematics) and
falls back to direct local spawning when IsSpawned is unreliable:

- OnEnable: subscribe when IsPartyMode even if !IsSpawned
- OnTurnStarted: use SpawnBatchIfMissing() fallback (like
  LocalCrystalManager) when IsPartyMode && !IsSpawned
- OnResetForReplay/OnTurnEnded: destroy crystals directly
- RespawnCrystal/ExplodeCrystal: bypass ServerRpc/ClientRpc

This mirrors the established party mode pattern where
MultiplayerDomainGamesController.OnReadyClicked_ already bypasses
RPCs with a direct local countdown for the same reason.
```

```text
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs            |  6 ++++
 Assets/_Scripts/Game/Environment/FlowField/CrystalManager.cs        |  6 ++++
 Assets/_Scripts/Game/Environment/FlowField/NetworkCrystalManager.cs | 68 ++++++++++++++++++++++++++++++++---
 3 files changed, 76 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 171 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index b85b6c534..b5dab91c6 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -1201,6 +1201,12 @@ namespace CosmicShore.Game.Arcade.Party
                 cc.IsPartyMode = true;
             }
 
+            var crystalManagers = env.GetComponentsInChildren<CrystalManager>(true);
+            foreach (var cm in crystalManagers)
+            {
+                cm.IsPartyMode = true;
+            }
+
             // Disable MiniGamePlayerSpawnerAdapter — in party mode, PartyVesselSpawner
             // handles player/vessel spawning. The adapter would spawn duplicates when
             // gameData.InitializeGame() fires OnInitializeGame.
diff --git a/Assets/_Scripts/Game/Environment/FlowField/CrystalManager.cs b/Assets/_Scripts/Game/Environment/FlowField/CrystalManager.cs
index 1433100e1..c4b91b90e 100644
--- a/Assets/_Scripts/Game/Environment/FlowField/CrystalManager.cs
+++ b/Assets/_Scripts/Game/Environment/FlowField/CrystalManager.cs
@@ -31,6 +31,12 @@ namespace CosmicShore.Game
         // If you want minimum distance of 25 units, set this to 25f * 25f = 625.
         private const float MIN_SQR_SPACE_BTWN_CURRENT_AND_LAST_SPAWN_POS = 25f;
 
+        /// <summary>
+        /// When true, this crystal manager is being orchestrated by PartyGameController.
+        /// Network-based spawning is bypassed since the host is both server and client.
+        /// </summary>
+        public bool IsPartyMode { get; set; }
+
         [Header("Dependencies")]
         [SerializeField] protected GameDataSO gameData;
         [SerializeField] protected CellRuntimeDataSO cellData;
diff --git a/Assets/_Scripts/Game/Environment/FlowField/NetworkCrystalManager.cs b/Assets/_Scripts/Game/Environment/FlowField/NetworkCrystalManager.cs
index f69225867..f01122da6 100644
--- a/Assets/_Scripts/Game/Environment/FlowField/NetworkCrystalManager.cs
+++ b/Assets/_Scripts/Game/Environment/FlowField/NetworkCrystalManager.cs
@@ -34,10 +34,13 @@ namespace CosmicShore.Game
         /// Re-subscribe when the environment is reactivated (party mode SetActive
         /// toggling). OnDisable already unsubscribes so events from inactive
         /// environments don't fire and spawn invisible crystals.
+        /// In party mode, subscribe even when IsSpawned is false — the environment
+        /// was deactivated during network spawn so IsSpawned may be unreliable,
+        /// but the host is both server and client so direct spawning works.
         /// </summary>
         private void OnEnable()
         {
-            if (IsSpawned)
+            if (IsSpawned || IsPartyMode)
                 SubscribeToCrystalEvents();
         }
 
@@ -54,6 +57,7 @@ namespace CosmicShore.Game
                 gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
 
             gameData.OnResetForReplay.OnRaised += OnResetForReplay;
+            gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
 
             if (n_Positions != null)
                 n_Positions.OnListChanged += OnPositionsChanged;
@@ -67,6 +71,7 @@ namespace CosmicShore.Game
                 gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
 
             gameData.OnResetForReplay.OnRaised -= OnResetForReplay;
+            gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
 
             if (n_Positions != null)
                 n_Positions.OnListChanged -= OnPositionsChanged;
@@ -78,14 +83,42 @@ namespace CosmicShore.Game
 
         void OnResetForReplay()
         {
+            // In party mode without a working NetworkObject, clear crystals directly.
+            if (IsPartyMode && !IsSpawned)
+            {
+                DestroyCrystals();
+                CSDebug.Log("[NetworkCrystalManager] Party mode reset — crystals destroyed.");
+                return;
+            }
```

</details>

### `ff9b283e2` — fix: party mode crystal spawning — bypass NetworkList, fix timing

_Claude, 2026-02-25 22:15:26 +0000_

```text
Root causes:
- IsPartyMode && !IsSpawned fallback never triggered because IsSpawned
  stays true after SetActive(false) — all party mode code paths fell
  through to the unreliable NetworkList/RPC mechanism
- Both HexRace and CrystalCapture use spawnOnClientReady=true, which
  fires OnClientReady at 0.5s (round 2+) before Cell.Initialize at 1s,
  causing null refs in SnowChangerManager

Fixes:
- NetworkCrystalManager: remove !IsSpawned from ALL party mode checks,
  always use direct local spawning via SpawnBatchIfMissing()
- In party mode, subscribe to OnMiniGameTurnStarted instead of
  OnClientReady to ensure Cell is initialized before crystal spawn
- Add unsubscribe-before-subscribe guard against double subscriptions
- SnowChangerManager: add null safety for cellData.Config and
  cellData.CellTransform, stay subscribed until cell is ready
```

```text
 Assets/_Scripts/Game/Environment/Cytoplasm/SnowChangerManager.cs    | 13 ++++++++--
 Assets/_Scripts/Game/Environment/FlowField/NetworkCrystalManager.cs | 45 ++++++++++++++++++++++-------------
 2 files changed, 40 insertions(+), 18 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 133 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/Cytoplasm/SnowChangerManager.cs b/Assets/_Scripts/Game/Environment/Cytoplasm/SnowChangerManager.cs
index f40cf80cb..ffd475d34 100644
--- a/Assets/_Scripts/Game/Environment/Cytoplasm/SnowChangerManager.cs
+++ b/Assets/_Scripts/Game/Environment/Cytoplasm/SnowChangerManager.cs
@@ -22,13 +22,22 @@ namespace CosmicShore.Game
         {
             if (!cellData.TryGetLocalCrystal(out _))
                 return;
-            
+
+            // Don't unsubscribe until we can actually spawn — Cell.Initialize
+            // may not have run yet (party mode timing: crystals can spawn
+            // before the cell is fully initialized).
+            if (cellData.Config == null || cellData.CellTransform == null)
+                return;
+
             cellData.OnCellItemsUpdated.OnRaised -= OnCellItemsUpdated;
             SpawnSnows();
         }
-        
+
         private void SpawnSnows()
         {
+            if (cellData.Config == null || cellData.Config.CytoplasmPrefab == null || cellData.CellTransform == null)
+                return;
+
             var snowChanger = Instantiate(cellData.Config.CytoplasmPrefab, cellData.CellTransform.position, Quaternion.identity);
             snowChanger.Initialize();
         }
diff --git a/Assets/_Scripts/Game/Environment/FlowField/NetworkCrystalManager.cs b/Assets/_Scripts/Game/Environment/FlowField/NetworkCrystalManager.cs
index f01122da6..136182628 100644
--- a/Assets/_Scripts/Game/Environment/FlowField/NetworkCrystalManager.cs
+++ b/Assets/_Scripts/Game/Environment/FlowField/NetworkCrystalManager.cs
@@ -51,7 +51,15 @@ namespace CosmicShore.Game
 
         void SubscribeToCrystalEvents()
         {
-            if (spawnOnClientReady)
+            // Unsubscribe first to prevent double-subscription when both
+            // OnNetworkSpawn and OnEnable fire in the same lifecycle.
+            UnsubscribeFromCrystalEvents();
+
+            // In party mode, always use OnMiniGameTurnStarted (not OnClientReady)
+            // to avoid timing issues: Cell.Initialize runs after 1s via InitializeAfterDelay,
+            // but OnClientReady fires at 0.5s on round 2+. Spawning crystals before Cell
+            // init causes null refs in SnowChangerManager and other cell-dependent systems.
+            if (spawnOnClientReady && !IsPartyMode)
                 gameData.OnClientReady += OnClientReadySpawn;
             else
                 gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
@@ -65,10 +73,10 @@ namespace CosmicShore.Game
 
         void UnsubscribeFromCrystalEvents()
         {
+            // Always unsubscribe from both paths to be safe
             if (spawnOnClientReady)
                 gameData.OnClientReady -= OnClientReadySpawn;
-            else
-                gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
+            gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
 
             gameData.OnResetForReplay.OnRaised -= OnResetForReplay;
             gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
@@ -83,10 +91,13 @@ namespace CosmicShore.Game
 
         void OnResetForReplay()
         {
-            // In party mode without a working NetworkObject, clear crystals directly.
-            if (IsPartyMode && !IsSpawned)
+            // In party mode, bypass NetworkList and destroy crystals directly.
+            // The host is both server and client so no replication is needed.
+            // IsSpawned/IsServer may be unreliable after environment deactivation/reactivation.
+            if (IsPartyMode)
             {
                 DestroyCrystals();
+                serverBatchAnchorIndex = 0;
                 CSDebug.Log("[NetworkCrystalManager] Party mode reset — crystals destroyed.");
                 return;
             }
@@ -101,9 +112,9 @@ namespace CosmicShore.Game
```

</details>

### `99d842379` — docs: add comprehensive party game mode change report

_Claude, 2026-02-26 10:09:46 +0000_

```text
Covers all 11 new files, 21 modified files, architecture overview,
crystal spawning fix details, and known issues/remaining work.
```

```text
 PARTY_CHANGES_REPORT.md | 528 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 528 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 534 lines)</summary>

```diff
diff --git a/PARTY_CHANGES_REPORT.md b/PARTY_CHANGES_REPORT.md
new file mode 100644
index 000000000..1dcc4d131
--- /dev/null
+++ b/PARTY_CHANGES_REPORT.md
@@ -0,0 +1,528 @@
+# Party Game Mode — Complete Change Report
+
+**Branch:** `claude/fix-party-game-mode-aALL3`
+**Generated:** 2026-02-26
+**Commits:** 40+ commits from initial infrastructure through crystal spawning fixes
+
+---
+
+## Table of Contents
+
+1. [New Files Created](#1-new-files-created)
+2. [Modified Files](#2-modified-files)
+3. [Scene & Asset Changes](#3-scene--asset-changes)
+4. [Architecture Overview](#4-architecture-overview)
+5. [Known Issues & Remaining Work](#5-known-issues--remaining-work)
+
+---
+
+## 1. New Files Created
+
+### 1.1 `Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs` (1305 lines)
+
+**Purpose:** Main orchestrator for the party game session. Manages lobby, randomized mini-game rounds, scoring, and final results.
+
+**Key Design Decisions:**
+- Mini-game controllers have `IsPartyMode = true` → suppresses autonomous lifecycle, but gameplay mechanics (collisions, race finish, crystals) still work
+- `PartyVesselSpawner` (on always-active PartyGameManager) handles vessel spawning so ClientRpcs are never called on disabled GameObjects
+- Environment SPVIs are always `InertMode` — they only provide spawn origin data
+- Environment `GameCanvas` stays enabled for HUD; `IsPartyMode` suppresses mini-game-specific UI
+- Scene camera is disabled during gameplay, re-enabled between rounds
+
+**Key Methods:**
+
+| Method | Purpose |
+|--------|---------|
+| `OnNetworkSpawn()` | Subscribes to network variables, events, initializes lobby flow |
+| `RunLobbyAsync()` | Fills AI slots after solo wait, transitions to WaitingForReady |
+| `OnLocalPlayerReady()` | Handles ready button presses across different phases |
+| `RunRoundAsync()` | Full round lifecycle: randomize → activate environment → spawn/reposition → countdown → gameplay → end-game cinematic → score |
+| `ActivateMiniGameEnvironment()` | Enables correct env, disables others, sets `IsPartyMode` on controllers/crystal managers |
+| `DeactivateMiniGameEnvironment()` | Cleans up env after round, resets mini-game controller state |
+| `CompleteRound()` | Records round results, advances to next round or final results |
+| `OnMiniGameWinnerCalculated()` | Captures winner from mini-game controllers (HexRace, Joust, CrystalCapture) |
+| `GetPartyWinner()` | Returns the player with the most games won |
+| `static GetMiniGameDisplayName()` | Maps `GameModes` enum to display names |
+| `OnQuitParty()` | Shuts down NetworkManager and loads main menu |
+
+**Network State:**
+- `NetworkVariable<int> _netCurrentRound` — current round index
+- `NetworkVariable<int> _netPhase` — current `PartyPhase` enum value
+- `NetworkVariable<int> _netSelectedMiniGameIndex` — which mini-game is active
+
+---
+
+### 1.2 `Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs` (69 lines)
+
+**Purpose:** ScriptableObject configuration for party game parameters.
+
+```csharp
+[CreateAssetMenu(fileName = "PartyGameConfig", menuName = "ScriptableObjects/Party/PartyGameConfig")]
+public class PartyGameConfigSO : ScriptableObject
+{
+    [Range(1, 3)] public int MinPlayers = 1;
+    [Range(2, 3)] public int MaxPlayers = 3;
+    [Range(1, 10)] public int TotalRounds = 5;
+    public List<GameModes> AvailableMiniGames;       // HexRace, Joust, CrystalCapture
+    public List<MiniGameEnvironmentEntry> EnvironmentPrefabs;
+    public float LobbyWaitTimeSeconds = 120f;
+    public float SoloLobbyWaitSeconds = 10f;
+    public float PreRoundCountdownSeconds = 3f;
+    public float PostCountdownDelaySeconds = 1f;
+    public float PostRoundDelaySeconds = 2f;
+    public float RoundDurationSeconds = 60f;
+}
```

</details>

### `9e8310380` — fix: three party mode bugs — HUD cards, snow spawning, winner scoring

_Claude, 2026-02-26 10:34:56 +0000_

```text
Bug 1 — HUD not showing score cards:
- Add IsPartyMode flag to GameDataSO, set by PartyGameController
- In MiniGameHUD.ResetForReplay(), skip the 5s connecting panel and
  pre-game cinematic in party mode — the party controller handles the
  lobby/ready flow; the HUD just needs to be visible for score display
- Cards are created by InitializePlayerCards() on OnMiniGameTurnStarted
  which fires after countdown, when RoundStatsList is populated

Bug 2 — SnowChangerManager not spawning cytoplasm:
- Add TrySpawnIfReady() called both on OnEnable and OnCellItemsUpdated
- On re-enable (party mode round 2+), immediately check if cell data is
  already initialized and crystals exist — spawn without waiting for event
- Add _spawned guard to prevent double-spawning within a round
- Add diagnostic logging when spawn conditions aren't met

Bug 3 — Wrong winner declared:
- ForceEndRound_ClientRpc now takes a golfRules parameter instead of
  hardcoding false — HexRace and Joust use golf rules (lower wins)
- Add IsGolfRulesForCurrentMode() helper matching each controller's
  UseGolfRules: HexRace=true, Joust=true, CrystalCapture=false
- OnMiniGameWinnerCalculated re-sorts with correct golf rules as safety
- Add _winnerCapturedThisRound guard to prevent double-counting when
  InvokeWinnerCalculated fires multiple times (game controller + score
  tracker's delayed 500ms path via OnMiniGameEnd)
- NetworkScoreTracker: unsubscribe-before-subscribe in SubscribeScoreEvents
  to prevent double-subscription from OnNetworkSpawn + OnEnable
```

```text
 Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs               |  4 ++++
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs         | 46 +++++++++++++++++++++++++++++++++-----
 Assets/_Scripts/Game/Environment/Cytoplasm/SnowChangerManager.cs | 28 ++++++++++++++++++-----
 Assets/_Scripts/Game/UI/MiniGameHUD.cs                           | 10 +++++++++
 Assets/_Scripts/Utility/DataContainers/GameDataSO.cs             |  2 ++
 5 files changed, 79 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 260 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs b/Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs
index cf85cf7b5..8448438e6 100644
--- a/Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs
+++ b/Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs
@@ -36,6 +36,10 @@ namespace CosmicShore.Game.Arcade
 
         void SubscribeScoreEvents()
         {
+            // Unsubscribe first to prevent double-subscription when both
+            // OnNetworkSpawn and OnEnable fire (party mode SetActive toggling).
+            UnsubscribeScoreEvents();
+
             gameData.OnInitializeGame += InitializeScoringMode;
             gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
             gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index b5dab91c6..d441b3d75 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -65,6 +65,7 @@ namespace CosmicShore.Game.Arcade.Party
         int _activeMiniGameIndex = -1;
         MultiplayerMiniGameControllerBase _activeMiniGameController;
         bool _vesselsSpawned; // True after first round spawns vessels
+        bool _winnerCapturedThisRound; // Prevents double-counting from multiple InvokeWinnerCalculated calls
         CancellationTokenSource _lobbyCts;
         CancellationTokenSource _roundCts;
 
@@ -121,6 +122,7 @@ namespace CosmicShore.Game.Arcade.Party
                 _roundResults.Add(new PartyRoundResult { RoundIndex = i });
 
             _vesselsSpawned = false;
+            gameData.IsPartyMode = true;
 
             // Set IsPartyMode on all environments immediately so their
             // OnNetworkSpawn callbacks know they're in party mode.
@@ -177,6 +179,8 @@ namespace CosmicShore.Game.Arcade.Party
             _roundCts?.Cancel();
             _roundCts?.Dispose();
 
+            gameData.IsPartyMode = false;
+
             base.OnNetworkDespawn();
         }
 
@@ -571,6 +575,7 @@ namespace CosmicShore.Game.Arcade.Party
                 int roundIndex = _netCurrentRound.Value;
                 var selectedMode = config.AvailableMiniGames[miniGameIndex];
 
+                _winnerCapturedThisRound = false;
                 CSDebug.Log($"[PartyGame] LoadMiniGameEnvironment: envIndex={miniGameIndex}, mode={selectedMode}, vesselsExist={_vesselsSpawned}");
 
                 BroadcastGameStateText_ClientRpc("Loading...");
@@ -709,7 +714,7 @@ namespace CosmicShore.Game.Arcade.Party
                 if (IsServer && CurrentPhase == PartyPhase.Playing)
                 {
                     CSDebug.Log("[PartyGame] Round timer expired.");
-                    ForceEndRound_ClientRpc();
+                    ForceEndRound_ClientRpc(IsGolfRulesForCurrentMode());
 
                     // Safety fallback: if OnShowGameEndScreen never fires (e.g., no
                     // cinematic controller on this environment), force CompleteRound
@@ -730,11 +735,11 @@ namespace CosmicShore.Game.Arcade.Party
         }
 
         [ClientRpc]
-        void ForceEndRound_ClientRpc()
+        void ForceEndRound_ClientRpc(bool golfRules)
         {
             gameData.InvokeGameTurnConditionsMet();
-            gameData.SortRoundStats(false);
-            gameData.CalculateDomainStats(false);
+            gameData.SortRoundStats(golfRules);
+            gameData.CalculateDomainStats(golfRules);
             gameData.InvokeWinnerCalculated();
             // DO NOT fire InvokeMiniGameEnd here — the EndGameCinematicController
             // needs to run its full sequence first (score reveal → Continue button).
@@ -747,9 +752,21 @@ namespace CosmicShore.Game.Arcade.Party
             if (!IsServer) return;
             if (CurrentPhase != PartyPhase.Playing) return;
 
```

</details>

### `0d7108764` — fix: let game-specific turn monitors drive round end in party mode

_Claude, 2026-02-26 10:58:13 +0000_

```text
RunRoundTimer was force-ending ALL games after 60s via ForceEndRound_ClientRpc,
which called InvokeGameTurnConditionsMet(). This triggered HexRaceScoreTracker
.HandleGameEnd() → ReportPlayerFinished_ServerRpc → KeyNotFoundException because
the HexRaceController's RPC table is broken after party mode SetActive toggling.

Changes:
- RunRoundTimer: changed from 60s game timer to 5-minute safety-only fallback.
  Each game mode has its own turn monitor that handles the real end condition:
    HexRace → NetworkCrystalCollisionTurnMonitor (all crystals collected)
    Joust → NetworkJoustCollisionTurnMonitor (3 jousts reached)
    CrystalCapture → NetworkTimeBasedTurnMonitor (round timer expires)

- ForceEndRound_ClientRpc: removed InvokeGameTurnConditionsMet() call to prevent
  triggering game-specific score trackers that call ServerRpcs on controllers with
  broken RPC registries. Safety fallback just sorts existing scores and declares
  a winner.

- HexRaceScoreTracker.HandleGameEnd: skip ReportLocalPlayerFinished ServerRpc and
  SortAndInvokeResults in party mode — the base controller's ExecuteServerGameEnd
  handles sort + InvokeWinnerCalculated via the natural turn monitor flow.
```

```text
 Assets/_Scripts/Game/Arcade/HexRaceScoreTracker.cs       | 10 ++++++++--
 Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs | 37 ++++++++++++++++++++++++++-----------
 2 files changed, 34 insertions(+), 13 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 94 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/HexRaceScoreTracker.cs b/Assets/_Scripts/Game/Arcade/HexRaceScoreTracker.cs
index 018be95b6..3d8fc47a8 100644
--- a/Assets/_Scripts/Game/Arcade/HexRaceScoreTracker.cs
+++ b/Assets/_Scripts/Game/Arcade/HexRaceScoreTracker.cs
@@ -122,10 +122,16 @@ namespace CosmicShore.Game.Arcade
                 if (UGSStatsManager.Instance && _vesselTelemetry != null && _observedVessel != null)
                     UGSStatsManager.Instance.ReportVesselTelemetry(_vesselTelemetry, _observedVessel.VesselType.ToString());
 
-                if (controller) controller.ReportLocalPlayerFinished(finalScore);
+                // In party mode, skip the ServerRpc — the base controller's
+                // ExecuteServerGameEnd handles sort + InvokeWinnerCalculated.
+                // Calling ReportPlayerFinished_ServerRpc in party mode causes
+                // KeyNotFoundException because the controller's RPC table may
+                // be broken after SetActive toggling.
+                if (controller && !gameData.IsPartyMode)
+                    controller.ReportLocalPlayerFinished(finalScore);
             }
 
-            if (controller == null)
+            if (controller == null && !gameData.IsPartyMode)
                 SortAndInvokeResults();
         }
 
diff --git a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
index d441b3d75..27f5bafac 100644
--- a/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
+++ b/Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs
@@ -702,25 +702,37 @@ namespace CosmicShore.Game.Arcade.Party
             return chosen;
         }
 
+        /// <summary>
+        /// Safety-only fallback timer. Each game mode has its own turn monitor that
+        /// handles the real end condition:
+        ///   HexRace  → NetworkCrystalCollisionTurnMonitor (all crystals collected)
+        ///   Joust    → NetworkJoustCollisionTurnMonitor   (3 jousts reached)
+        ///   CrystalCapture → NetworkTimeBasedTurnMonitor  (round timer expires)
+        ///
+        /// This timer only fires if something goes wrong and the game never ends
+        /// naturally. It does NOT call InvokeGameTurnConditionsMet to avoid
+        /// triggering game-specific score trackers that call ServerRpcs on
+        /// controllers with broken RPC registries (party mode SetActive toggling).
+        /// </summary>
         async UniTaskVoid RunRoundTimer(CancellationToken ct)
         {
+            const float safetyTimeoutSeconds = 300f; // 5 minutes — generous safety net
+
             try
             {
                 await UniTask.Delay(
-                    TimeSpan.FromSeconds(config.RoundDurationSeconds),
+                    TimeSpan.FromSeconds(safetyTimeoutSeconds),
                     DelayType.UnscaledDeltaTime,
                     cancellationToken: ct);
 
                 if (IsServer && CurrentPhase == PartyPhase.Playing)
                 {
-                    CSDebug.Log("[PartyGame] Round timer expired.");
+                    CSDebug.LogWarning("[PartyGame] Safety timer expired (5 min) — force-ending round.");
                     ForceEndRound_ClientRpc(IsGolfRulesForCurrentMode());
 
-                    // Safety fallback: if OnShowGameEndScreen never fires (e.g., no
-                    // cinematic controller on this environment), force CompleteRound
-                    // after a generous timeout so the party doesn't get stuck.
+                    // If OnShowGameEndScreen never fires (no cinematic), force CompleteRound.
                     await UniTask.Delay(
-                        TimeSpan.FromSeconds(60),
+                        TimeSpan.FromSeconds(30),
                         DelayType.UnscaledDeltaTime,
                         cancellationToken: ct);
 
@@ -737,14 +749,17 @@ namespace CosmicShore.Game.Arcade.Party
         [ClientRpc]
         void ForceEndRound_ClientRpc(bool golfRules)
         {
-            gameData.InvokeGameTurnConditionsMet();
+            // DO NOT call InvokeGameTurnConditionsMet() here. That raises
+            // OnMiniGameTurnEnd which triggers game-specific score trackers
+            // (e.g., HexRaceScoreTracker.HandleGameEnd) that call ServerRpcs
+            // on controllers whose RPC table may be broken after party mode
```

</details>

### `f39d72c9a` — fix: HexRace wrong winner + Crystal Capture missing timer in party mode

_Claude, 2026-02-26 11:27:19 +0000_

```text
HexRace: AI players had score=0 (never set) while the human winner had
a positive race time. With golf rules (lower=better), AI appeared as
winner. Added OnTurnEndedCustom override to HexRaceController that sets
penalty scores (10000 + crystals remaining) for all non-winning players
before ExecuteServerGameEnd sorts — mirrors ReportPlayerFinished_ServerRpc
logic that is skipped in party mode.

Crystal Capture: NetworkTimeBasedTurnMonitor routed timer UI updates
through a ClientRpc, which fails silently in party mode after SetActive
toggling. Bypass the RPC in party mode and update the display directly
via the ScriptableEvent (same path joust/hex race monitors use).
```

```text
 Assets/_Scripts/Game/Arcade/HexRaceController.cs                      | 50 +++++++++++++++++++++++++++++++++
 .../_Scripts/Game/Arcade/TurnMonitors/NetworkTimeBasedTurnMonitor.cs  | 11 +++++++-
 2 files changed, 60 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 83 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/HexRaceController.cs b/Assets/_Scripts/Game/Arcade/HexRaceController.cs
index e124536d8..312658b4d 100644
--- a/Assets/_Scripts/Game/Arcade/HexRaceController.cs
+++ b/Assets/_Scripts/Game/Arcade/HexRaceController.cs
@@ -164,6 +164,56 @@ namespace CosmicShore.Game.Arcade
             helix.secondOrderRadius = radius;
         }
 
+        // ==================== Turn End / Scoring ====================
+
+        /// <summary>
+        /// In party mode, ReportPlayerFinished_ServerRpc is skipped (RPC table may be
+        /// broken after SetActive toggling). This override sets proper scores for ALL
+        /// players so the sort in ExecuteServerGameEnd produces the correct winner.
+        /// Mirrors the logic in ReportPlayerFinished_ServerRpc: the first player to
+        /// collect all crystals gets their race time; all others get a penalty score.
+        /// </summary>
+        protected override void OnTurnEndedCustom()
+        {
+            base.OnTurnEndedCustom();
+            if (!IsServer || !IsPartyMode) return;
+            if (_raceEnded) return;
+            _raceEnded = true;
+
+            int crystalsToFinish = ResolveCrystalsToFinishTarget();
+            float currentTime = Time.time - gameData.TurnStartTime;
+
+            // Find the winner — first player who collected all crystals
+            string winnerName = "";
+            foreach (var stats in gameData.RoundStatsList)
+            {
+                if (stats.CrystalsCollected >= crystalsToFinish)
+                {
+                    winnerName = stats.Name;
+                    break;
+                }
+            }
+
+            CSDebug.Log($"[HexRace] OnTurnEndedCustom (party). Winner='{winnerName}' Time={currentTime:F2}s " +
+                      $"Players=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.CrystalsCollected}c/{s.Score:F1}s"))}]");
+
+            foreach (var stats in gameData.RoundStatsList)
+            {
+                if (stats.Name == winnerName)
+                {
+                    // Winner keeps their race time (already set by HexRaceScoreTracker.Update)
+                    // but cap it to currentTime in case Update hasn't run this frame
+                    if (stats.Score <= 0f)
+                        stats.Score = currentTime;
+                }
+                else
+                {
+                    int crystalsLeft = Mathf.Max(0, crystalsToFinish - stats.CrystalsCollected);
+                    stats.Score = 10000f + crystalsLeft;
+                }
+            }
+        }
+
         // ==================== Race Finish ====================
 
         // Only called by the winner's ScoreTracker
diff --git a/Assets/_Scripts/Game/Arcade/TurnMonitors/NetworkTimeBasedTurnMonitor.cs b/Assets/_Scripts/Game/Arcade/TurnMonitors/NetworkTimeBasedTurnMonitor.cs
index b9ee78efc..cfe7c89c9 100644
--- a/Assets/_Scripts/Game/Arcade/TurnMonitors/NetworkTimeBasedTurnMonitor.cs
+++ b/Assets/_Scripts/Game/Arcade/TurnMonitors/NetworkTimeBasedTurnMonitor.cs
@@ -7,7 +7,16 @@ namespace CosmicShore.Game.Arcade
     {
         protected override void UpdateTimerUI()
         {
-            FixedString32Bytes message = GetTimeToDisplay(); 
+            // In party mode, RPCs on scene-placed NetworkObjects may not work
+            // reliably after SetActive toggling. Update the display directly
+            // via the ScriptableEvent (same path the base class uses).
+            if (gameData != null && gameData.IsPartyMode)
+            {
+                InvokeUpdateTurnMonitorDisplay(GetTimeToDisplay());
+                return;
+            }
+
+            FixedString32Bytes message = GetTimeToDisplay();
```

</details>

### `89c18947a` — fix: Joust scores not updating in party mode due to broken RPCs

_Claude, 2026-02-26 12:03:05 +0000_

```text
NotifyCollision() and OnTurnEndedCustom() called ClientRpcs
unconditionally. In party mode, the RPC table is broken from
SetActive toggling, causing silent failures. Now both methods
skip RPCs in party mode — stats propagate locally via
NetworkVariable.OnValueChanged, and ExecuteServerGameEnd handles
the sort + InvokeWinnerCalculated.
```

```text
 Assets/_Scripts/Game/Arcade/MultiplayerJoustController.cs | 14 ++++++++++++--
 1 file changed, 12 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/MultiplayerJoustController.cs b/Assets/_Scripts/Game/Arcade/MultiplayerJoustController.cs
index e2854c306..ae9d96dea 100644
--- a/Assets/_Scripts/Game/Arcade/MultiplayerJoustController.cs
+++ b/Assets/_Scripts/Game/Arcade/MultiplayerJoustController.cs
@@ -62,7 +62,12 @@ namespace CosmicShore.Game.Arcade
             if (!IsServer) return;
             var stats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
             if (stats != null) stats.JoustCollisions = collisionCount;
-            NotifyCollision_ClientRpc(playerName, collisionCount);
+
+            // In party mode, skip the ClientRpc — RPC table may be broken after
+            // SetActive toggling. The NetworkVariable.OnValueChanged callback
+            // fires locally on the host, so the HUD still updates.
+            if (!IsPartyMode)
+                NotifyCollision_ClientRpc(playerName, collisionCount);
         }
 
         public void ReportCollisionToServer(string playerName, int collisionCount)
@@ -100,7 +105,12 @@ namespace CosmicShore.Game.Arcade
             if (_finalResultsSent) return;
 
             CalculateJoustScores_Server();
-            SyncJoustResults_Authoritative();
+
+            // In party mode, skip the ClientRpc sync — RPC table is broken after
+            // SetActive toggling. ExecuteServerGameEnd handles sort + InvokeWinnerCalculated.
+            if (!IsPartyMode)
+                SyncJoustResults_Authoritative();
+
             _finalResultsSent = true;
         }
 
```

</details>

### `a034d96b8` — fix: auto-start minigames in party mode, bypassing broken ready button

_Claude, 2026-02-26 17:30:18 +0000_

```text
The root cause of all party mode issues (no UI, no scores, no ready
button) was that gameData.StartTurn() never fired. The turn only starts
after the ready button is clicked → countdown ends → StartTurn(). In
party mode, the SOAP event chain to show the ready button was unreliable
(nested prefab event listeners after SetActive toggling), so the button
never appeared, the turn never started, and the HUD/scores/turn monitors
never initialised.

Fix: call OnReadyClicked() directly from InitializeAfterDelay() in
party mode. This triggers the countdown → SetPlayersActive → StartTurn
flow automatically, which fires OnMiniGameTurnStarted and bootstraps
the entire game: HUD player cards, score event subscriptions, and turn
monitor start.
```

```text
 Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs | 7 +++++++
 1 file changed, 7 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs b/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
index 49c665497..186bf794b 100644
--- a/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
+++ b/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
@@ -120,6 +120,13 @@ namespace CosmicShore.Game.Arcade
                 gameData.InitializeGame();
                 if (!IsServer) return;
                 SetupNewRound();
+
+                // In party mode, auto-start the game — no manual ready-click required.
+                // This triggers the countdown → SetPlayersActive → StartTurn, which
+                // fires OnMiniGameTurnStarted and initialises HUD, score cards, and
+                // turn monitors.
+                if (IsPartyMode)
+                    OnReadyClicked();
             }
             catch (OperationCanceledException) { }
         }
```

</details>

### `947ba06d1` — fix: bypass unreliable IsServer checks in party mode game flow

_Claude, 2026-02-26 18:54:13 +0000_

```text
After environment deactivation/reactivation in party mode,
NetworkBehaviour.IsServer returns false because IsSpawned becomes
unreliable (documented in NetworkCrystalManager). This caused the
entire game flow to abort: InitializeAfterDelay bailed at the
IsServer check, so SetupNewRound/OnReadyClicked/StartTurn never ran,
crystals never spawned, turn monitors never started, and scores never
initialized.

Add IsEffectiveServer (IsServer || IsPartyMode) to MiniGameControllerBase
and TurnMonitor, then replace all IsServer gates in the party mode flow
path across controllers, turn monitors, and score trackers. In party mode
the host is always both server and client, so IsPartyMode safely implies
server authority.

For HexRace, bypass NetworkVariable writes (which also fail when
IsSpawned is unreliable) by spawning tracks directly and storing
crystals-to-finish in a local field.
```

```text
 Assets/_Scripts/Game/Arcade/HexRaceController.cs                      | 57 ++++++++++++++++++++++++++-------
 Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs                 |  8 +++++
 Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs       |  6 ++--
 Assets/_Scripts/Game/Arcade/MultiplayerJoustController.cs             |  4 +--
 Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs      | 19 ++++++-----
 Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs                    | 30 +++++++++++++----
 .../Game/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs    | 15 ++++++---
 .../Game/Arcade/TurnMonitors/NetworkJoustCollisionTurnMonitor.cs      |  6 ++--
 Assets/_Scripts/Game/Arcade/TurnMonitors/TurnMonitor.cs               |  8 +++++
 9 files changed, 114 insertions(+), 39 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 456 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/HexRaceController.cs b/Assets/_Scripts/Game/Arcade/HexRaceController.cs
index 312658b4d..acecd45d8 100644
--- a/Assets/_Scripts/Game/Arcade/HexRaceController.cs
+++ b/Assets/_Scripts/Game/Arcade/HexRaceController.cs
@@ -33,6 +33,7 @@ namespace CosmicShore.Game.Arcade
         private bool _trackSpawned;
         private readonly NetworkVariable<int> _netTrackSeed = new(0);
         private readonly NetworkVariable<int> _netCrystalsToFinish = new(0);
+        private int _partyCrystalsToFinish; // Local fallback for party mode (NetworkVariable writes unreliable)
 
         // Single source of truth for who won — set authoritatively by server, read by end game controller
         public string WinnerName { get; private set; } = "";
@@ -57,7 +58,7 @@ namespace CosmicShore.Game.Arcade
                 return;
             }
 
-            if (IsServer)
+            if (IsEffectiveServer)
             {
                 // Server generates the seed after a short delay for intensity sync
                 SpawnTrackEarly().Forget();
@@ -81,15 +82,28 @@ namespace CosmicShore.Game.Arcade
         {
             base.PartyMode_Activate();
 
-            // In party mode, spawn the track when the environment is activated
-            if (IsServer)
+            // In party mode, spawn the track when the environment is activated.
+            // Use IsEffectiveServer: IsServer may be false after env deactivation/reactivation.
+            if (IsEffectiveServer)
             {
                 _raceEnded = false;
                 _trackSpawned = false;
-                SpawnTrackEarly().Forget();
+                SpawnTrackForParty();
             }
         }
 
+        /// <summary>
+        /// In party mode, spawn the track directly without NetworkVariable (IsSpawned
+        /// may be unreliable after environment deactivation/reactivation). The host is
+        /// the only client so no replication is needed.
+        /// </summary>
+        private void SpawnTrackForParty()
+        {
+            if (_trackSpawned || !segmentSpawner) return;
+            int generatedSeed = (seed != 0) ? seed : Random.Range(int.MinValue, int.MaxValue);
+            SpawnTrackLocally(generatedSeed);
+        }
+
         public override void PartyMode_Deactivate()
         {
             base.PartyMode_Deactivate();
@@ -126,7 +140,7 @@ namespace CosmicShore.Game.Arcade
 
         protected override void OnCountdownTimerEnded()
         {
-            if (!IsServer) return;
+            if (!IsEffectiveServer) return;
 
             // Ensure track seed is set for any edge case where early spawn was missed
             if (_netTrackSeed.Value == 0)
@@ -176,7 +190,7 @@ namespace CosmicShore.Game.Arcade
         protected override void OnTurnEndedCustom()
         {
             base.OnTurnEndedCustom();
-            if (!IsServer || !IsPartyMode) return;
+            if (!IsEffectiveServer || !IsPartyMode) return;
             if (_raceEnded) return;
             _raceEnded = true;
 
@@ -254,6 +268,7 @@ namespace CosmicShore.Game.Arcade
 
         int ResolveCrystalsToFinishTarget()
         {
+            if (IsPartyMode && _partyCrystalsToFinish > 0) return _partyCrystalsToFinish;
             if (_netCrystalsToFinish.Value > 0) return _netCrystalsToFinish.Value;
             if (crystalsToFinishOverride > 0) return crystalsToFinishOverride;
             return 39;
@@ -261,13 +276,22 @@ namespace CosmicShore.Game.Arcade
```

</details>

### `bf6b95571` — Revert "fix: bypass unreliable IsServer checks in party mode game flow"

_Claude, 2026-02-26 18:59:58 +0000_

```text
This reverts commit 947ba06d1d7934c491abd5a8a6074db81c097ef2.
```

```text
 Assets/_Scripts/Game/Arcade/HexRaceController.cs                      | 57 +++++++--------------------------
 Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs                 |  8 -----
 Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs       |  6 ++--
 Assets/_Scripts/Game/Arcade/MultiplayerJoustController.cs             |  4 +--
 Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs      | 19 +++++------
 Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs                    | 30 ++++-------------
 .../Game/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs    | 15 +++------
 .../Game/Arcade/TurnMonitors/NetworkJoustCollisionTurnMonitor.cs      |  6 ++--
 Assets/_Scripts/Game/Arcade/TurnMonitors/TurnMonitor.cs               |  8 -----
 9 files changed, 39 insertions(+), 114 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 456 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/HexRaceController.cs b/Assets/_Scripts/Game/Arcade/HexRaceController.cs
index acecd45d8..312658b4d 100644
--- a/Assets/_Scripts/Game/Arcade/HexRaceController.cs
+++ b/Assets/_Scripts/Game/Arcade/HexRaceController.cs
@@ -33,7 +33,6 @@ namespace CosmicShore.Game.Arcade
         private bool _trackSpawned;
         private readonly NetworkVariable<int> _netTrackSeed = new(0);
         private readonly NetworkVariable<int> _netCrystalsToFinish = new(0);
-        private int _partyCrystalsToFinish; // Local fallback for party mode (NetworkVariable writes unreliable)
 
         // Single source of truth for who won — set authoritatively by server, read by end game controller
         public string WinnerName { get; private set; } = "";
@@ -58,7 +57,7 @@ namespace CosmicShore.Game.Arcade
                 return;
             }
 
-            if (IsEffectiveServer)
+            if (IsServer)
             {
                 // Server generates the seed after a short delay for intensity sync
                 SpawnTrackEarly().Forget();
@@ -82,28 +81,15 @@ namespace CosmicShore.Game.Arcade
         {
             base.PartyMode_Activate();
 
-            // In party mode, spawn the track when the environment is activated.
-            // Use IsEffectiveServer: IsServer may be false after env deactivation/reactivation.
-            if (IsEffectiveServer)
+            // In party mode, spawn the track when the environment is activated
+            if (IsServer)
             {
                 _raceEnded = false;
                 _trackSpawned = false;
-                SpawnTrackForParty();
+                SpawnTrackEarly().Forget();
             }
         }
 
-        /// <summary>
-        /// In party mode, spawn the track directly without NetworkVariable (IsSpawned
-        /// may be unreliable after environment deactivation/reactivation). The host is
-        /// the only client so no replication is needed.
-        /// </summary>
-        private void SpawnTrackForParty()
-        {
-            if (_trackSpawned || !segmentSpawner) return;
-            int generatedSeed = (seed != 0) ? seed : Random.Range(int.MinValue, int.MaxValue);
-            SpawnTrackLocally(generatedSeed);
-        }
-
         public override void PartyMode_Deactivate()
         {
             base.PartyMode_Deactivate();
@@ -140,7 +126,7 @@ namespace CosmicShore.Game.Arcade
 
         protected override void OnCountdownTimerEnded()
         {
-            if (!IsEffectiveServer) return;
+            if (!IsServer) return;
 
             // Ensure track seed is set for any edge case where early spawn was missed
             if (_netTrackSeed.Value == 0)
@@ -190,7 +176,7 @@ namespace CosmicShore.Game.Arcade
         protected override void OnTurnEndedCustom()
         {
             base.OnTurnEndedCustom();
-            if (!IsEffectiveServer || !IsPartyMode) return;
+            if (!IsServer || !IsPartyMode) return;
             if (_raceEnded) return;
             _raceEnded = true;
 
@@ -268,7 +254,6 @@ namespace CosmicShore.Game.Arcade
 
         int ResolveCrystalsToFinishTarget()
         {
-            if (IsPartyMode && _partyCrystalsToFinish > 0) return _partyCrystalsToFinish;
             if (_netCrystalsToFinish.Value > 0) return _netCrystalsToFinish.Value;
             if (crystalsToFinishOverride > 0) return crystalsToFinishOverride;
             return 39;
@@ -276,22 +261,13 @@ namespace CosmicShore.Game.Arcade
```

</details>

### `d66760f08` — fix: use IsServerSafe() fallback for broken IsServer after SetActive toggling

_Claude, 2026-02-26 19:36:45 +0000_

```text
After environments are deactivated/reactivated via SetActive in party mode,
NetworkBehaviour.IsServer returns false because IsSpawned becomes stale. This
broke the entire game flow: ready button, UI, scoring, turn monitors.

Added IsServerSafe() extension on NetworkBehaviour that falls back to
NetworkManager.Singleton.IsServer when the per-behaviour check fails.
Applied it to all party-mode-affected code paths in controllers, score
trackers, and turn monitors. Guarded NetworkVariable writes with IsSpawned
checks and local fallbacks for when the network object state is unreliable.

Vessel spawning unchanged — PartyVesselSpawner still handles it.
Non-party multiplayer unaffected — IsServerSafe checks IsServer first.
```

```text
 Assets/_Scripts/Game/Arcade/HexRaceController.cs                          | 29 +++++++++++++++++++++--------
 Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs           |  5 +++--
 Assets/_Scripts/Game/Arcade/MultiplayerJoustController.cs                 |  5 +++--
 Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs          | 15 ++++++++-------
 Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs                        | 14 ++++++++------
 .../Game/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs        | 28 +++++++++++++++++++---------
 .../_Scripts/Game/Arcade/TurnMonitors/NetworkJoustCollisionTurnMonitor.cs |  7 ++++---
 Assets/_Scripts/Utility/ClassExtensions/NetcodeExtensions.cs              | 15 +++++++++++++++
 8 files changed, 81 insertions(+), 37 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 400 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/HexRaceController.cs b/Assets/_Scripts/Game/Arcade/HexRaceController.cs
index 312658b4d..73784ac33 100644
--- a/Assets/_Scripts/Game/Arcade/HexRaceController.cs
+++ b/Assets/_Scripts/Game/Arcade/HexRaceController.cs
@@ -1,4 +1,5 @@
 using System.Linq;
+using CosmicShore.Utility.ClassExtensions;
 using Cysharp.Threading.Tasks;
 using Unity.Collections;
 using Unity.Netcode;
@@ -82,7 +83,7 @@ namespace CosmicShore.Game.Arcade
             base.PartyMode_Activate();
 
             // In party mode, spawn the track when the environment is activated
-            if (IsServer)
+            if (this.IsServerSafe())
             {
                 _raceEnded = false;
                 _trackSpawned = false;
@@ -118,15 +119,25 @@ namespace CosmicShore.Game.Arcade
         {
             // Small delay to ensure all clients have joined and intensity is synced
             await UniTask.Delay(1500, DelayType.UnscaledDeltaTime);
-            if (!IsServer || _trackSpawned) return;
+            if (!this.IsServerSafe() || _trackSpawned) return;
 
             int generatedSeed = (seed != 0) ? seed : Random.Range(int.MinValue, int.MaxValue);
-            _netTrackSeed.Value = generatedSeed;
+
+            // In party mode after SetActive toggling, IsSpawned may be unreliable
+            // so the NetworkVariable write could fail. Spawn the track locally as fallback.
+            if (IsSpawned)
+            {
+                _netTrackSeed.Value = generatedSeed;
+            }
+            else
+            {
+                SpawnTrackLocally(generatedSeed);
+            }
         }
 
         protected override void OnCountdownTimerEnded()
         {
-            if (!IsServer) return;
+            if (!this.IsServerSafe()) return;
 
             // Ensure track seed is set for any edge case where early spawn was missed
             if (_netTrackSeed.Value == 0)
@@ -176,7 +187,7 @@ namespace CosmicShore.Game.Arcade
         protected override void OnTurnEndedCustom()
         {
             base.OnTurnEndedCustom();
-            if (!IsServer || !IsPartyMode) return;
+            if (!this.IsServerSafe() || !IsPartyMode) return;
             if (_raceEnded) return;
             _raceEnded = true;
 
@@ -261,13 +272,15 @@ namespace CosmicShore.Game.Arcade
 
         public void SetCrystalsToFinishServer(int value)
         {
-            if (!IsServer) return;
-            _netCrystalsToFinish.Value = Mathf.Max(1, value);
+            if (!this.IsServerSafe()) return;
+            int clamped = Mathf.Max(1, value);
+            if (IsSpawned)
+                _netCrystalsToFinish.Value = clamped;
         }
 
         public void NotifyCrystalsCollected(string playerName, int crystalsCollected)
         {
-            if (!IsServer) return;
+            if (!this.IsServerSafe()) return;
             var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
             if (stat != null)
                 stat.CrystalsCollected = crystalsCollected;
diff --git a/Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs b/Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs
index fb10a3909..88b6b7013 100644
--- a/Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs
+++ b/Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs
```

</details>

### `7ffdfd837` — fix: guard InitializeAfterDelay against deactivated environments

_Claude, 2026-02-27 00:28:22 +0000_

```text
The autonomous InitializeAfterDelay from OnNetworkSpawn fires after 1000ms
even when the environment has already been deactivated (the UniTask has no
cancellation token tied to the GameObject). With IsServerSafe() returning
true via NetworkManager.Singleton, all 3 environments' init tasks would
fire into the shared GameDataSO, causing duplicate game state.

Added gameObject.activeInHierarchy check after the delay to bail out if
the environment was deactivated while waiting.

Also fixed NetworkScoreTracker.OnEnable to require IsSpawned or IsPartyMode
before subscribing — prevents premature subscription during initial scene
load (before OnNetworkSpawn) where IsServerSafe() returns true via the
global NetworkManager singleton.
```

```text
 Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs | 8 ++++++++
 Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs               | 8 +++++---
 2 files changed, 13 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs b/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
index 095a54f20..3eef74431 100644
--- a/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
+++ b/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
@@ -118,6 +118,14 @@ namespace CosmicShore.Game.Arcade
             try
             {
                 await UniTask.Delay(InitDelayMs, DelayType.UnscaledDeltaTime);
+
+                // If the environment was deactivated while we waited (e.g. the
+                // initial spawn → deactivate cycle in party mode), bail out.
+                // Without this, autonomous init from OnNetworkSpawn would fire
+                // gameData.InitializeGame() on all 3 deactivated environments
+                // into the shared GameDataSO.
+                if (!gameObject.activeInHierarchy) return;
+
                 gameData.InitializeGame();
                 if (!this.IsServerSafe()) return;
                 SetupNewRound();
diff --git a/Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs b/Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs
index e11b5ad94..1ea978dc4 100644
--- a/Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs
+++ b/Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs
@@ -21,12 +21,14 @@ namespace CosmicShore.Game.Arcade
 
         /// <summary>
         /// Re-subscribe when the environment is reactivated (party mode SetActive
-        /// toggling). IsSpawned may be unreliable after toggling, so fall back to
-        /// the global NetworkManager check via IsServerSafe.
+        /// toggling). Requires IsSpawned (normal) or IsPartyMode (reactivation —
+        /// IsSpawned may be stale after SetActive toggling) so we don't subscribe
+        /// too early during initial scene load (before OnNetworkSpawn).
         /// </summary>
         private void OnEnable()
         {
-            if (this.IsServerSafe())
+            bool spawned = IsSpawned || (gameData != null && gameData.IsPartyMode);
+            if (spawned && this.IsServerSafe())
                 SubscribeScoreEvents();
         }
 
```

</details>

### `6fe53546f` — fix: re-apply party mode fixes to new Controller/Arcade architecture

_Claude, 2026-02-27 12:07:31 +0000_

```text
After merging app-shell-polish, game controllers moved from Game/Arcade/
to Controller/Arcade/ (namespace CosmicShore.Gameplay). This commit
re-applies the IsServerSafe() and party mode fixes to the new locations:

- MultiplayerMiniGameControllerBase: IsServerSafe, PartyMode_Activate/Deactivate,
  activeInHierarchy guard, party auto-start, local RPC fallbacks
- MultiplayerDomainGamesController: IsServerSafe in OnCountdownTimerEnded, SetupNewRound
- NetworkScoreTracker: IsServerSafe, OnEnable/OnDisable for SetActive toggling
- NetworkCrystalCollisionTurnMonitor: IsServerSafe, local crystal target fallback
- NetworkJoustCollisionTurnMonitor: IsServerSafe in OnCollisionChanged, CheckForEndOfTurn
- NetworkCrystalManager: OnEnable/OnDisable for party mode, party reset via ResetSpawnState
- ServerPlayerVesselInitializer: PartyModeState enum, InertMode/SpawnMode guards
- HexRaceController, MultiplayerJoustController: fix stale ClassExtensions using
```

```text
 Assets/_Scripts/Controller/Arcade/HexRaceController.cs                |   1 -
 Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs |   6 +-
 Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs       |   1 -
 .../_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs   | 159 +++++++++++++++++++++++---------
 Assets/_Scripts/Controller/Arcade/NetworkScoreTracker.cs              |  49 ++++++++--
 .../Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs         |  18 ++--
 .../Arcade/TurnMonitors/NetworkJoustCollisionTurnMonitor.cs           |   9 +-
 .../Controller/Environment/FlowField/NetworkCrystalManager.cs         |  60 +++++++++++-
 .../_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs  |  50 ++++++++++
 9 files changed, 279 insertions(+), 74 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 644 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/HexRaceController.cs b/Assets/_Scripts/Controller/Arcade/HexRaceController.cs
index 8bf3073ae..3d1dad6b4 100644
--- a/Assets/_Scripts/Controller/Arcade/HexRaceController.cs
+++ b/Assets/_Scripts/Controller/Arcade/HexRaceController.cs
@@ -1,5 +1,4 @@
 using System.Linq;
-using CosmicShore.Utility.ClassExtensions;
 using Cysharp.Threading.Tasks;
 using Unity.Collections;
 using Unity.Netcode;
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
index 28d8564f2..957c041bd 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
@@ -20,9 +20,7 @@ namespace CosmicShore.Gameplay
 
         protected override void OnCountdownTimerEnded()
         {
-            if (!IsServer)
-                return;
-
+            if (!this.IsServerSafe()) return;
             OnCountdownTimerEnded_ClientRpc();
         }
 
@@ -79,7 +77,7 @@ namespace CosmicShore.Gameplay
 
         protected override void SetupNewRound()
         {
-            if (IsServer)
+            if (this.IsServerSafe())
             {
                 readyClientCount = 0;
             }
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs
index 0e1f45512..6d6a7ef07 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs
@@ -1,6 +1,5 @@
 // MultiplayerJoustController.cs
 using System.Linq;
-using CosmicShore.Utility.ClassExtensions;
 using Unity.Collections;
 using Unity.Netcode;
 using UnityEngine;
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs
index cf54a9055..cd58f5c28 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs
@@ -24,6 +24,14 @@ namespace CosmicShore.Gameplay
         {
             base.OnNetworkSpawn();
 
+            // In party mode, skip all autonomous lifecycle management.
+            // The PartyGameController will call into us when needed.
+            if (IsPartyMode)
+            {
+                CSDebug.Log($"[{GetType().Name}] OnNetworkSpawn — PARTY MODE, skipping autonomous init.");
+                return;
+            }
+
             if (IsServer)
             {
                 gameData.OnMiniGameTurnEnd.OnRaised += HandleTurnEnd;
@@ -40,12 +48,53 @@ namespace CosmicShore.Gameplay
                 gameData.OnMiniGameTurnEnd.OnRaised -= HandleTurnEnd;
                 gameData.OnSessionStarted.OnRaised -= SubscribeToSessionEvents;
             }
-            
+
             UnsubscribeFromSessionEvents();
-            
+
             base.OnNetworkDespawn();
         }
 
+        // ==================== Party Mode API ====================
+        // These methods are called by PartyGameController to drive gameplay
+        // without the controller's own lifecycle getting in the way.
+
```

</details>

_Also contains 3 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
