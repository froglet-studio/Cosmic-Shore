# Crystal Capture Game Mode — Technical Documentation

## Overview

Crystal Capture is a competitive crystal-collection mode for 1-3 players. Players race to collect the most crystals within a single round. The player with the highest crystal count when the target is reached wins. The mode supports solo play with AI opponents, multiplayer with friends, or mixed human+AI lobbies.

**Key architectural facts:**

- **Single scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameCrystalCaptureMultiplayer_Gameplay.unity` — no separate singleplayer scene
- **Single GameMode enum**: `GameModes.MultiplayerCrystalCapture = 35`
- **Always Netcode**: `MultiplayerCrystalCaptureController` extends the multiplayer controller hierarchy. Even solo play runs through Netcode (host is always active from Menu_Main)
- **Server-authoritative**: Crystal target, winner determination, and final score sync are all server-owned
- **Non-golf scoring**: Higher score = better rank. Score = crystals collected

## Class Hierarchy

```
MiniGameControllerBase (MonoBehaviour + NetworkBehaviour)
  └── MultiplayerMiniGameControllerBase
      └── MultiplayerDomainGamesController
          └── MultiplayerCrystalCaptureController
```

`MultiplayerCrystalCaptureController` is the thinnest controller in the hierarchy — only 15 lines:
- Sets `numberOfRounds = 1`, `numberOfTurnsPerRound = 1`
- Overrides `UseGolfRules => false` (highest crystals wins, not lowest)
- All game flow logic is inherited from the base classes

## Execution Flow

### 1. Game Configuration (Menu_Main)

User selects Crystal Capture from the Arcade screen. `ArcadeGameConfigureModal` opens with configuration controls:

- **Player Count** (1-3): Constrained by `SO_ArcadeGame.MinPlayers` (1) and `MaxPlayers` (3)
- **Intensity** (1-4): Constrained by `SO_ArcadeGame.MinIntensity` and `MaxIntensity`
- **Vessel Selection**: From `SO_ArcadeGame.Captains` list

### 2. Player Count & AI Backfill Decision

When the user clicks "Start Game", `ArcadeGameConfigureModal.SyncAllGameDataForLaunch()` calculates:

```
humanCount = max(1, hostConnectionData.PartyMembers.Count)
aiBackfill = max(0, config.PlayerCount - humanCount)
```

| Scenario | Humans | Selected Players | AI Backfill | Total |
|---|---|---|---|---|
| Solo, selects 1 player | 1 | 1 | 0 | 1 |
| Solo, selects 2 players | 1 | 2 | 1 | 2 |
| Solo, selects 3 players | 1 | 3 | 2 | 3 |
| 2 friends in party, selects 2 | 2 | 2 | 0 | 2 |
| 2 friends in party, selects 3 | 2 | 3 | 1 | 3 |
| 3 friends in party, selects 3 | 3 | 3 | 0 | 3 |

**Data synced to GameDataSO:**

```
gameData.SceneName                = "MinigameCrystalCaptureMultiplayer_Gameplay"
gameData.GameMode                 = GameModes.MultiplayerCrystalCapture
gameData.IsMultiplayerMode        = true
gameData.SelectedPlayerCount      = humanCount
gameData.RequestedAIBackfillCount = aiBackfill
gameData.ActiveSession            = PartySession (if party active)
gameData.SelectedIntensity        = config.Intensity
gameData.selectedVesselClass      = config.SelectedShip.Class
```

Then `gameData.InvokeGameLaunch()` raises the `OnLaunchGame` SOAP event.

### 3. Scene Loading

`SceneLoader.LaunchGame()` (listens to `OnLaunchGame`):

```csharp
var nm = NetworkManager.Singleton;
bool useNetworkSceneLoading = nm != null && nm.IsServer;
LoadSceneAsync(gameData.SceneName, useNetworkSceneLoading).Forget();
```

The application state transitions to `LoadingGame` before scene load begins.

### 4. Scene Initialization

After scene load completes, the following chain runs:

```
Scene Load Complete
│
├─ MultiplayerCrystalCaptureController.OnNetworkSpawn()
│   ├─ numberOfRounds = 1, numberOfTurnsPerRound = 1
│   └─ UseGolfRules = false
│
├─ ServerPlayerVesselInitializerWithAI.OnNetworkSpawn()
│   ├─ [Server] SpawnAIs() — pre-spawns AI players based on RequestedAIBackfillCount
│   ├─ Mark all AI in _processedPlayers set
│   └─ base.OnNetworkSpawn() — subscribe to OnPlayerNetworkSpawnedUlong for humans
│
├─ MultiplayerMiniGameControllerBase.InitializeAfterDelay()
│   ├─ await UniTask.Delay(1000ms)
│   ├─ gameData.InitializeGame() → raises OnInitializeGame
│   └─ [Server] gameData.InvokeSessionStarted() (AppState → InGame)
│   └─ [Server] SetupNewRound()
│       ├─ readyClientCount = 0
│       ├─ RaiseToggleReadyButtonEvent(true) — show Ready button
│       └─ base.SetupNewRound() → timer/round bookkeeping
│
└─ Player.OnNetworkSpawn() [for each human + AI player]
    ├─ gameData.Players.Add(this)
    ├─ Raise OnPlayerNetworkSpawnedUlong(OwnerClientId)
    └─ ServerPlayerVesselInitializer handles vessel spawning
```

### 5. Ready State & Countdown

```
Player sees "Ready" button
│
├─ Player clicks Ready
│   └─ OnReadyClicked_() → RaiseToggleReadyButtonEvent(false) — hide button
│       └─ OnReadyClicked_ServerRpc(playerName)
│           ├─ readyClientCount++
│           ├─ NotifyPlayerReady_ClientRpc(playerName) → game feed: "Player Ready"
│           └─ if readyClientCount == SelectedPlayerCount:
│               ├─ readyClientCount = 0
│               └─ OnReadyClicked_ClientRpc()
│                   └─ StartCountdownTimer() — 3-second countdown
│
└─ Countdown ends
    └─ OnCountdownTimerEnded() [Server only]
        └─ OnCountdownTimerEnded_ClientRpc() [All clients]
            ├─ gameData.SetPlayersActive() — enables vessel input
            └─ gameData.StartTurn() — IsTurnRunning=true, raises OnMiniGameTurnStarted
```

### 6. Gameplay Loop: Crystal Collection & Turn Monitoring

```
gameData.OnMiniGameTurnStarted.Raise()
│
├─ MultiplayerCrystalCaptureHUD.OnMiniGameTurnStarted() [via MultiplayerHUD]
│   ├─ Initialize player score cards (crystals collected per player)
│   ├─ SubscribeToPlayerStats() — cache per-player Action delegates
│   │   └─ stats.OnScoreChanged += handler → UpdatePlayerCard(name, crystals)
│   └─ RefreshAllPlayerCards() — set initial values
│
├─ TurnMonitorController.StartMonitors()
│   └─ NetworkCrystalCollisionTurnMonitor.StartMonitor()
│       ├─ target = GetCrystalCollisionCount() (39 crystals default)
│       ├─ _netCrystalCollisions.Value = target [Server, NetworkVariable]
│       ├─ Subscribe to all players' OnCrystalsCollectedChanged events
│       └─ Push initial values via ServerSideCrystalSync
│
├─ Crystal collected by player:
│   ├─ Collision → OnCrystalCollided → updates RoundStats.CrystalsCollected + Score
│   ├─ OnScoreChanged fires → HUD card updates in real-time
│   └─ ServerSideCrystalSync → onUpdateTurnMonitorDisplay.Raise(remaining)
│
└─ TurnMonitorController.Update() — every frame
    └─ CheckEndOfTurn()
        └─ NetworkCrystalCollisionTurnMonitor.CheckForEndOfTurn()
            └─ return gameData.RoundStatsList.Any(s => s.CrystalsCollected >= target)
                └─ If true → OnTurnEnded() → gameData.InvokeGameTurnConditionsMet()
```

### 7. Winner Determination & Score Sync

When any player collects the target number of crystals:

```
gameData.InvokeGameTurnConditionsMet()
│
└─ MultiplayerMiniGameControllerBase.HandleTurnEnd() [Server only]
    ├─ SyncTurnEnd_ClientRpc() [All clients]
    │   ├─ gameData.ResetPlayers()
    │   └─ OnTurnEndedCustom()
    │
    └─ ExecuteServerTurnEnd() [Server]
        └─ TurnsTakenThisRound++ (now 1 >= numberOfTurnsPerRound)
            └─ ExecuteServerRoundEnd() [Server]
                ├─ SyncRoundEnd_ClientRpc() [All clients]
                ├─ RoundsPlayed++ (now 1 >= numberOfRounds)
                │
                └─ ExecuteServerGameEnd() [Server]
                    └─ SyncGameEnd_ClientRpc() [All clients]
                        ├─ gameData.SortRoundStats(UseGolfRules=false) — descending by Score
                        ├─ gameData.CalculateDomainStats(UseGolfRules=false) — sum per domain
                        ├─ gameData.InvokeWinnerCalculated()
                        │   └─ EndGameCinematicController.OnWinnerCalculated()
                        │       └─ RunCompleteEndGameSequence() (coroutine)
                        ├─ 250ms delay (EndGameAfterDelay UniTask)
                        └─ gameData.InvokeMiniGameEnd()
                            ├─ CrystalCaptureStatsReporter.ReportStats()
                            └─ ApplicationStateMachine → GameOver
```

**Scoring Rules:**

| Player | Score | Rank |
|---|---|---|
| Player with 39 crystals | 39 | 1st (highest) |
| Player with 28 crystals | 28 | 2nd |
| Player with 15 crystals | 15 | 3rd |

Non-golf rules (`UseGolfRules = false`): Higher score = higher rank.

### 8. End Game Cinematic

`MultiplayerCrystalCaptureEndGameController` (extends `EndGameCinematicController`) displays the result screen:

```csharp
// Winner = index 0 after descending sort (highest crystals first)
bool didWin = gameData.RoundStatsList.Count > 0 &&
              gameData.RoundStatsList[0].Name == localName;
```

| Result | Header | Label |
|---|---|---|
| Winner | `"VICTORY"` | `"WON BY X CRYSTAL(S)"` — difference between winner and next best |
| Loser | `"DEFEAT"` | `"LOST BY X CRYSTAL(S)"` — difference between loser and winner |

The score reveal animation displays the player's crystal count numerically.

### 9. Scoreboard

`MultiplayerCrystalCaptureScoreboard` (extends `Scoreboard`):

- Sorts `RoundStatsList` descending by `Score` (highest crystals first)
- Sets domain banner color for first-place player
- Displays each player as `"Name"` / `"X Crystals"`
- Supports both single-player and multiplayer views
- "Play Again" triggers `RequestReplay()` → server resets game state

### 10. Replay & Rematch

Replay flows through `MultiplayerMiniGameControllerBase.RequestReplay()`:

```
Reset:
├─ gameData.ResetStatsDataForReplay()
├─ gameData.ResetPlayers() — teleport to spawn positions
├─ CameraManager.SnapPlayerCameraToTarget()
├─ OnResetForReplay.Raise() — reset UI elements
├─ RaiseToggleReadyButtonEvent(true) — show Ready button again
└─ [Server] SetupNewRound() after 100ms delay
```

Rematch requests broadcast via `RequestRematch_ServerRpc/ClientRpc` with opponent notification panel.

## Elemental Comeback System

`ElementalComebackSystem` (attached in scene alongside the controller):

- **Source**: `ScoreDifferenceSource.Score` — tracks crystal count gap between players directly
- **Effect**: Losing players receive elemental buffs proportional to their score deficit
- **Update interval**: Recalculates every 1 second
- **Comeback profile**: Configured via `SO_ElementalComebackProfile` (per-vessel, per-element weights)
- **Non-golf mode**: Deficit = `leaderScore - myScore` (higher is ahead)

## HUD & UI Components

| Component | Class | Purpose |
|---|---|---|
| In-game HUD | `MultiplayerCrystalCaptureHUD` (extends `MultiplayerHUD`) | Per-player crystal count cards; subscribes to `OnScoreChanged` per player |
| Scoreboard | `MultiplayerCrystalCaptureScoreboard` (extends `Scoreboard`) | End-game player ranking with crystal counts |
| End Game | `MultiplayerCrystalCaptureEndGameController` (extends `EndGameCinematicController`) | Victory/defeat screen with crystal difference |
| Stats Reporter | `CrystalCaptureStatsReporter` | Winner-only UGS stats reporting + vessel telemetry |
| Player Stats | `CrystalCapturePlayerStatsProfile` | Cloud-saved high scores by mode+intensity key |

### HUD Detail

`MultiplayerCrystalCaptureHUD` extends `MultiplayerHUD` with crystal-specific behavior:

- **Score display**: `GetInitialCardValue()` returns `(int)stats.Score` — crystal count
- **Live updates**: `SubscribeToPlayerStats()` caches per-player `Action` delegates in `_scoreChangeHandlers` dictionary, subscribing to `stats.OnScoreChanged`. Each score change updates the player's card via `UpdatePlayerCard(name, crystals)`
- **Cleanup**: `UnsubscribeFromPlayerStats()` removes cached delegates and clears dictionary on `OnDisable()`
- **Base class handles refresh**: `MultiplayerHUD.RefreshAllPlayerCards()` fires on `OnMiniGameTurnStarted` via the SOAP event, calling `GetInitialCardValue()` for each player

### Stats & Telemetry

**UGS Stats Reporting** (winner only, via `CrystalCaptureStatsReporter`):

```csharp
ugsStatsManager.ReportCrystalCaptureStats(
    GameModes.MultiplayerCrystalCapture,
    intensity,
    crystalCount  // winner's total crystals
);
```

Also reports per-vessel telemetry on win via `ugsStatsManager.ReportVesselTelemetry()`.

**Cloud-saved profile** (`CrystalCapturePlayerStatsProfile`):
- `HighScores`: Dictionary keyed by `"MultiplayerCrystalCapture_{intensity}"`, value = best crystal count (higher is better)
- `TryUpdateHighScore()` only updates if new score exceeds current best

## Key Files Reference

| Role | File | Location |
|---|---|---|
| Game controller | `MultiplayerCrystalCaptureController.cs` | `_Scripts/Controller/Arcade/` |
| Base domain games controller | `MultiplayerDomainGamesController.cs` | `_Scripts/Controller/Arcade/` |
| Base multiplayer mini-game | `MultiplayerMiniGameControllerBase.cs` | `_Scripts/Controller/Arcade/` |
| Base mini-game controller | `MiniGameControllerBase.cs` | `_Scripts/Controller/Arcade/` |
| In-game HUD | `MultiplayerCrystalCaptureHUD.cs` | `_Scripts/UI/` |
| Scoreboard | `MultiplayerCrystalCaptureScoreboard.cs` | `_Scripts/UI/` |
| End game controller | `MultiplayerCrystalCaptureEndGameController.cs` | `_Scripts/Utility/DataContainers/` |
| Stats reporter | `CrystalCaptureStatsReporter.cs` | `_Scripts/Controller/Arcade/` |
| Player stats profile | `CrystalCapturePlayerStatsProfile.cs` | `_Scripts/UI/` |
| Crystal turn monitor | `NetworkCrystalCollisionTurnMonitor.cs` | `_Scripts/Controller/Arcade/TurnMonitors/` |
| Base crystal monitor | `CrystalCollisionTurnMonitor.cs` | `_Scripts/Controller/Arcade/TurnMonitors/` |
| Elemental comeback | `ElementalComebackSystem.cs` | `_Scripts/Controller/Arcade/` |
| End game cinematic base | `EndGameCinematicController.cs` | `_Scripts/Utility/DataContainers/` |
| AI vessel spawner | `ServerPlayerVesselInitializerWithAI.cs` | `_Scripts/Controller/Multiplayer/` |
| GameMode enum | `GameModes.cs` | `_Scripts/Data/Enums/` |
| UGS stats manager | `UGSStatsManager.cs` | `_Scripts/UI/` |
| Game scene | `MinigameCrystalCaptureMultiplayer_Gameplay.unity` | `_Scenes/Multiplayer Scenes/` |
| Documentation | `CRYSTALCAPTURE.md` | `_Scripts/Controller/Arcade/` |

## Comparison with HexRace

| Aspect | Crystal Capture | HexRace |
|---|---|---|
| Mode ID | `MultiplayerCrystalCapture = 35` | `HexRace = 33` |
| Max Players | 3 | 4 |
| Rounds × Turns | 1 × 1 | 1 × 1 |
| Scoring | Non-golf (highest crystals wins) | Golf (lowest time wins) |
| Winner Score | Crystal count (e.g., 39) | Race time in seconds (e.g., 45.3) |
| Loser Score | Crystal count (e.g., 28) | 10000 + crystals remaining |
| Track Generation | Scene-placed environment | Procedural (seeded `SegmentSpawner`) |
| Track Seed Sync | N/A | `NetworkVariable<int>` |
| Turn Monitor | `NetworkCrystalCollisionTurnMonitor` | `NetworkCrystalCollisionTurnMonitor` |
| Score Tracker | N/A (score = crystals, tracked by stats) | `HexRaceScoreTracker` (elapsed time) |
| End Game Header | `"VICTORY"` / `"DEFEAT"` + crystal diff | `"VICTORY"` / `"DEFEAT"` + time/crystals |
| Comeback Source | `ScoreDifferenceSource.Score` | `ScoreDifferenceSource.CrystalsCollected` |
| Controller Lines | 15 | ~200 |

## Design Notes

1. **Minimal controller**: `MultiplayerCrystalCaptureController` is intentionally thin. The domain games hierarchy handles all multiplayer flow (ready sync, countdown, turn/round management, end-game). Crystal Capture only needs to set round count and scoring mode.

2. **Score = Crystals**: Unlike HexRace where Score tracks elapsed time (same for all players during the race), Crystal Capture's Score directly equals crystals collected. This simplifies the HUD, scoreboard, and stats — no separate "race time" tracking is needed.

3. **Non-golf scoring**: `UseGolfRules = false` means `SortRoundStats()` sorts descending (highest first). The winner is always at index 0. This is the opposite of HexRace where lower time = better.

4. **Shared turn monitor**: Both Crystal Capture and HexRace use `NetworkCrystalCollisionTurnMonitor` with the same crystal target (39 by default). The monitor checks if any player has reached the target, regardless of scoring mode.

5. **No procedural track**: Crystal Capture uses a scene-placed environment rather than a procedurally generated track. There is no `SegmentSpawner` or seed synchronization.

6. **Winner-only stats**: `CrystalCaptureStatsReporter` only reports to UGS when the local player wins (index 0 after sort). Losers' results are not persisted to the cloud — only displayed in the end-game scoreboard.
