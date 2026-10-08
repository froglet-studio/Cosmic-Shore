# Branch archive: `claude/test-crystal-capture-mode-bnGpP`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-03 by Claude
- **Unmerged commits:** 2
- **Forked from:** `c99fec59e` (2026-03-04, Update MinigameHexRace.unity)
- **Tip:** `7519eea66`
- **Files touched (4):**
  - `Assets/_Scripts/Controller/Arcade/CRYSTALCAPTURE.md`
  - `Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs`
  - `Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs`
  - `Assets/_Scripts/UI/MultiplayerCrystalCaptureHUD.cs`

### `710d88394` — fix(arcade): remove duplicate HUD subscription and dead EndGame override

_Claude, 2026-03-03 20:42:33 +0000_

```text
- Remove duplicate RefreshAllPlayerCards subscription in
  MultiplayerCrystalCaptureHUD that caused double UI refresh on turn
  start (base class already subscribes via GetInitialCardValue)
- Move 250ms end-game delay from dead EndGame() override in
  MultiplayerDomainGamesController into SyncGameEnd_ClientRpc flow
  where it actually executes, giving cinematic setup time before
  OnMiniGameEnd fires
```

```text
 Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs  | 19 -------------------
 Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs | 14 ++++++++++----
 Assets/_Scripts/UI/MultiplayerCrystalCaptureHUD.cs                     | 25 -------------------------
 3 files changed, 10 insertions(+), 48 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
index bd248bbdf..7c4dd24fa 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
@@ -1,7 +1,5 @@
-using System.Collections;
 using System.Linq;
 using CosmicShore.UI;
-using Cysharp.Threading.Tasks;
 using Unity.Netcode;
 using UnityEngine;
 using CosmicShore.Utility;
@@ -102,23 +100,6 @@ namespace CosmicShore.Gameplay
             base.OnResetForReplay();
         }
 
-        protected override void EndGame()
-        {
-            if (!ShowEndGameSequence) return;
-            gameData.SortRoundStats(UseGolfRules);
-            gameData.InvokeWinnerCalculated();
-            if (IsServer)
-            {
-                StartCoroutine(EndGameSyncRoutine());
-            }
-        }
-
-        private IEnumerator EndGameSyncRoutine()
-        {
-            yield return new WaitForSeconds(0.25f);
-            gameData.InvokeMiniGameEnd();
-        }
-
         protected override void OnPlayerLeavingFromSession(string clientId)
         {
             if (ulong.TryParse(clientId, out var id) &&
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs
index 3365ec322..b72c96c75 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs
@@ -194,19 +194,25 @@ namespace CosmicShore.Gameplay
         {
             if (!IsServer)
                 return;
-                
+
             SyncGameEnd_ClientRpc();
         }
-        
+
         [ClientRpc]
         void SyncGameEnd_ClientRpc()
         {
             if (!ShowEndGameSequence) return;
 
             gameData.SortRoundStats(UseGolfRules);
-            gameData.CalculateDomainStats(UseGolfRules); 
-            
+            gameData.CalculateDomainStats(UseGolfRules);
+
             gameData.InvokeWinnerCalculated();
+            EndGameAfterDelay().Forget();
+        }
+
+        async UniTaskVoid EndGameAfterDelay()
+        {
+            await UniTask.Delay(250, DelayType.UnscaledDeltaTime);
             gameData.InvokeMiniGameEnd();
         }
 
diff --git a/Assets/_Scripts/UI/MultiplayerCrystalCaptureHUD.cs b/Assets/_Scripts/UI/MultiplayerCrystalCaptureHUD.cs
index 0de8165bf..506d569b0 100644
--- a/Assets/_Scripts/UI/MultiplayerCrystalCaptureHUD.cs
+++ b/Assets/_Scripts/UI/MultiplayerCrystalCaptureHUD.cs
@@ -1,6 +1,5 @@
 ﻿using System;
 using System.Collections.Generic;
-using System.Linq;
 using CosmicShore.Data;
 
 namespace CosmicShore.UI
@@ -9,23 +8,9 @@ namespace CosmicShore.UI
     {
         private readonly Dictionary<IRoundStats, Action> _scoreChangeHandlers = new();
 
-        protected override void OnEnable()
-        {
-            base.OnEnable();
-            if (gameData != null)
-            {
-                gameData.OnMiniGameTurnStarted.OnRaised += RefreshAllPlayerCards;
-            }
-        }
-
         protected override void OnDisable()
         {
             base.OnDisable();
-            if (gameData != null)
-            {
-                gameData.OnMiniGameTurnStarted.OnRaised -= RefreshAllPlayerCards;
-            }
-            
             _scoreChangeHandlers.Clear();
         }
 
@@ -52,15 +37,5 @@ namespace CosmicShore.UI
             _scoreChangeHandlers.Remove(stats);
         }
 
-        void RefreshAllPlayerCards()
-        {
-            if (gameData?.RoundStatsList == null) return;
-
-            // [Visual Note] Iterates through active players and snaps their scoreboard card crystal values to the server-verified count.
-            foreach (var stats in gameData.RoundStatsList.Where(stats => stats != null))
-            {
-                UpdatePlayerCard(stats.Name, (int)stats.Score);
-            }
-        }
     }
 }
\ No newline at end of file
```

</details>

### `7519eea66` — docs(arcade): add Crystal Capture game mode technical documentation

_Claude, 2026-03-03 21:39:17 +0000_

```text
Comprehensive technical reference covering controller hierarchy,
execution flow, scoring rules, HUD/scoreboard, end-game cinematic,
stats reporting, comeback system, and comparison with HexRace.
```

```text
 Assets/_Scripts/Controller/Arcade/CRYSTALCAPTURE.md | 351 ++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 351 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 357 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/CRYSTALCAPTURE.md b/Assets/_Scripts/Controller/Arcade/CRYSTALCAPTURE.md
new file mode 100644
index 000000000..6dd39ba14
--- /dev/null
+++ b/Assets/_Scripts/Controller/Arcade/CRYSTALCAPTURE.md
@@ -0,0 +1,351 @@
+# Crystal Capture Game Mode — Technical Documentation
+
+## Overview
+
+Crystal Capture is a competitive crystal-collection mode for 1-3 players. Players race to collect the most crystals within a single round. The player with the highest crystal count when the target is reached wins. The mode supports solo play with AI opponents, multiplayer with friends, or mixed human+AI lobbies.
+
+**Key architectural facts:**
+
+- **Single scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameCrystalCaptureMultiplayer_Gameplay.unity` — no separate singleplayer scene
+- **Single GameMode enum**: `GameModes.MultiplayerCrystalCapture = 35`
+- **Always Netcode**: `MultiplayerCrystalCaptureController` extends the multiplayer controller hierarchy. Even solo play runs through Netcode (host is always active from Menu_Main)
+- **Server-authoritative**: Crystal target, winner determination, and final score sync are all server-owned
+- **Non-golf scoring**: Higher score = better rank. Score = crystals collected
+
+## Class Hierarchy
+
+```
+MiniGameControllerBase (MonoBehaviour + NetworkBehaviour)
+  └── MultiplayerMiniGameControllerBase
+      └── MultiplayerDomainGamesController
+          └── MultiplayerCrystalCaptureController
+```
+
+`MultiplayerCrystalCaptureController` is the thinnest controller in the hierarchy — only 15 lines:
+- Sets `numberOfRounds = 1`, `numberOfTurnsPerRound = 1`
+- Overrides `UseGolfRules => false` (highest crystals wins, not lowest)
+- All game flow logic is inherited from the base classes
+
+## Execution Flow
+
+### 1. Game Configuration (Menu_Main)
+
+User selects Crystal Capture from the Arcade screen. `ArcadeGameConfigureModal` opens with configuration controls:
+
+- **Player Count** (1-3): Constrained by `SO_ArcadeGame.MinPlayers` (1) and `MaxPlayers` (3)
+- **Intensity** (1-4): Constrained by `SO_ArcadeGame.MinIntensity` and `MaxIntensity`
+- **Vessel Selection**: From `SO_ArcadeGame.Captains` list
+
+### 2. Player Count & AI Backfill Decision
+
+When the user clicks "Start Game", `ArcadeGameConfigureModal.SyncAllGameDataForLaunch()` calculates:
+
+```
+humanCount = max(1, hostConnectionData.PartyMembers.Count)
+aiBackfill = max(0, config.PlayerCount - humanCount)
+```
+
+| Scenario | Humans | Selected Players | AI Backfill | Total |
+|---|---|---|---|---|
+| Solo, selects 1 player | 1 | 1 | 0 | 1 |
+| Solo, selects 2 players | 1 | 2 | 1 | 2 |
+| Solo, selects 3 players | 1 | 3 | 2 | 3 |
+| 2 friends in party, selects 2 | 2 | 2 | 0 | 2 |
+| 2 friends in party, selects 3 | 2 | 3 | 1 | 3 |
+| 3 friends in party, selects 3 | 3 | 3 | 0 | 3 |
+
+**Data synced to GameDataSO:**
+
+```
+gameData.SceneName                = "MinigameCrystalCaptureMultiplayer_Gameplay"
+gameData.GameMode                 = GameModes.MultiplayerCrystalCapture
+gameData.IsMultiplayerMode        = true
+gameData.SelectedPlayerCount      = humanCount
+gameData.RequestedAIBackfillCount = aiBackfill
+gameData.ActiveSession            = PartySession (if party active)
+gameData.SelectedIntensity        = config.Intensity
+gameData.selectedVesselClass      = config.SelectedShip.Class
+```
+
+Then `gameData.InvokeGameLaunch()` raises the `OnLaunchGame` SOAP event.
+
+### 3. Scene Loading
+
+`SceneLoader.LaunchGame()` (listens to `OnLaunchGame`):
+
+```csharp
+var nm = NetworkManager.Singleton;
+bool useNetworkSceneLoading = nm != null && nm.IsServer;
+LoadSceneAsync(gameData.SceneName, useNetworkSceneLoading).Forget();
+```
+
+The application state transitions to `LoadingGame` before scene load begins.
+
+### 4. Scene Initialization
+
+After scene load completes, the following chain runs:
+
+```
+Scene Load Complete
+│
+├─ MultiplayerCrystalCaptureController.OnNetworkSpawn()
+│   ├─ numberOfRounds = 1, numberOfTurnsPerRound = 1
+│   └─ UseGolfRules = false
+│
+├─ ServerPlayerVesselInitializerWithAI.OnNetworkSpawn()
+│   ├─ [Server] SpawnAIs() — pre-spawns AI players based on RequestedAIBackfillCount
+│   ├─ Mark all AI in _processedPlayers set
+│   └─ base.OnNetworkSpawn() — subscribe to OnPlayerNetworkSpawnedUlong for humans
+│
+├─ MultiplayerMiniGameControllerBase.InitializeAfterDelay()
+│   ├─ await UniTask.Delay(1000ms)
+│   ├─ gameData.InitializeGame() → raises OnInitializeGame
+│   └─ [Server] gameData.InvokeSessionStarted() (AppState → InGame)
+│   └─ [Server] SetupNewRound()
+│       ├─ readyClientCount = 0
+│       ├─ RaiseToggleReadyButtonEvent(true) — show Ready button
+│       └─ base.SetupNewRound() → timer/round bookkeeping
+│
+└─ Player.OnNetworkSpawn() [for each human + AI player]
+    ├─ gameData.Players.Add(this)
+    ├─ Raise OnPlayerNetworkSpawnedUlong(OwnerClientId)
+    └─ ServerPlayerVesselInitializer handles vessel spawning
+```
+
+### 5. Ready State & Countdown
+
+```
+Player sees "Ready" button
+│
+├─ Player clicks Ready
+│   └─ OnReadyClicked_() → RaiseToggleReadyButtonEvent(false) — hide button
+│       └─ OnReadyClicked_ServerRpc(playerName)
+│           ├─ readyClientCount++
+│           ├─ NotifyPlayerReady_ClientRpc(playerName) → game feed: "Player Ready"
+│           └─ if readyClientCount == SelectedPlayerCount:
+│               ├─ readyClientCount = 0
+│               └─ OnReadyClicked_ClientRpc()
+│                   └─ StartCountdownTimer() — 3-second countdown
+│
+└─ Countdown ends
+    └─ OnCountdownTimerEnded() [Server only]
+        └─ OnCountdownTimerEnded_ClientRpc() [All clients]
+            ├─ gameData.SetPlayersActive() — enables vessel input
+            └─ gameData.StartTurn() — IsTurnRunning=true, raises OnMiniGameTurnStarted
+```
+
+### 6. Gameplay Loop: Crystal Collection & Turn Monitoring
+
+```
+gameData.OnMiniGameTurnStarted.Raise()
+│
+├─ MultiplayerCrystalCaptureHUD.OnMiniGameTurnStarted() [via MultiplayerHUD]
+│   ├─ Initialize player score cards (crystals collected per player)
+│   ├─ SubscribeToPlayerStats() — cache per-player Action delegates
```

</details>
