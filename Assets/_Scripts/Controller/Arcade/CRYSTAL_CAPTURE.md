# Crystal Capture Game Mode — Technical Documentation

## Overview

Crystal Capture is a competitive crystal-collection mode for 1-4 players. Players race to collect crystals — the player with the most crystals when the turn ends wins. The turn ends either when a player reaches the crystal target (crystal-target mode) or when a timer expires (time-based mode), depending on which turn monitors are wired in the scene. The mode supports solo play with AI opponents, multiplayer with friends, or mixed human+AI lobbies.

**Key architectural facts:**

- **Single scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameCrystalCaptureMultiplayer_Gameplay.unity` — no separate singleplayer scene
- **Single GameMode enum**: `GameModes.MultiplayerCrystalCapture = 35`
- **Always Netcode**: `MultiplayerCrystalCaptureController` extends the multiplayer controller hierarchy. Even solo play runs through Netcode (host is always active from Menu_Main)
- **Server-authoritative**: Winner determination and final score sync are server-owned
- **Non-golf scoring**: Higher score = better rank. Score = CrystalsCollected (mapped directly)
- **Scene reload for replay**: `UseSceneReloadForReplay = true`

## Class Hierarchy

```
MiniGameControllerBase (MonoBehaviour + NetworkBehaviour)
  └── MultiplayerMiniGameControllerBase
      └── MultiplayerDomainGamesController
          └── MultiplayerCrystalCaptureController
```

## Execution Flow

### 1. Game Configuration (Menu_Main)

User selects Crystal Capture from the Arcade screen. `ArcadeGameConfigureModal` opens with configuration controls:

- **Player Count** (1-4): Constrained by `SO_ArcadeGame.MinPlayers` (1) and `MaxPlayers` (4)
- **Intensity** (1-4): Constrained by `SO_ArcadeGame.MinIntensity` (1) and `MaxIntensity` (4)
- **Vessel Selection**: From `SO_ArcadeGame.Vessels` list

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
| Solo, selects 4 players | 1 | 4 | 3 | 4 |
| 2 friends in party, selects 2 | 2 | 2 | 0 | 2 |
| 2 friends in party, selects 4 | 2 | 4 | 2 | 4 |

**Data synced to GameDataSO:**

```
gameData.SceneName                = "MinigameCrystalCaptureMultiplayer_Gameplay"
gameData.GameMode                 = GameModes.MultiplayerCrystalCapture
gameData.IsMultiplayerMode        = true
gameData.SelectedPlayerCount      = humanCount
gameData.RequestedAIBackfillCount = aiBackfill
gameData.SelectedIntensity        = config.Intensity
gameData.selectedVesselClass      = config.SelectedShip.Class
```

Then `gameData.InvokeGameLaunch()` raises the `OnLaunchGame` SOAP event.

### 3. Scene Loading

`SceneLoader.LaunchGame()` (listens to `OnLaunchGame` via SOAP code subscription):

```csharp
var nm = NetworkManager.Singleton;
bool useNetworkSceneLoading = nm != null && nm.IsServer;
LoadSceneAsync(gameData.SceneName, useNetworkSceneLoading).Forget();
```

The application state transitions to `LoadingGame` before scene load begins. Game config is synced to clients by `MultiplayerMiniGameControllerBase.SyncGameConfigToClients_ClientRpc()` in `OnNetworkSpawn()`.

### 4. Scene Initialization

After scene load completes:

```
Scene Load Complete
│
├─ MultiplayerCrystalCaptureController.OnNetworkSpawn()
│   ├─ base.OnNetworkSpawn()  — wires turn-end handler, syncs game config
│   ├─ numberOfRounds = 1, numberOfTurnsPerRound = 1
│   └─ _finalResultsSent = false
│
├─ ServerPlayerVesselInitializerWithAI.OnNetworkSpawn()
│   ├─ [Server] SpawnAIs()  — pre-spawns AI players based on RequestedAIBackfillCount
│   └─ base.OnNetworkSpawn()  — subscribe to OnPlayerNetworkSpawnedUlong for humans
│
├─ MultiplayerMiniGameControllerBase.InitializeAfterDelay()
│   ├─ await UniTask.Delay(1000ms)
│   ├─ gameData.InitializeGame()  → raises OnInitializeGame
│   ├─ [Server] gameData.InvokeSessionStarted()  — AppState → InGame
│   └─ [Server] SetupNewRound()
│       ├─ readyClientCount = 0
│       ├─ RaiseToggleReadyButtonEvent(true)  — show Ready button
│       └─ base.SetupNewRound()  → timer/round bookkeeping
│
└─ Player.OnNetworkSpawn()  [for each human + AI player]
    ├─ gameData.Players.Add(this)
    └─ Raise OnPlayerNetworkSpawnedUlong(OwnerClientId)
```

Unlike HexRace, Crystal Capture has **no deterministic track generation** — the environment is scene-placed.

### 5. Ready State & Countdown

```
Player sees "Ready" button
│
├─ Player clicks Ready
│   └─ OnReadyClicked_() → RaiseToggleReadyButtonEvent(false)
│       └─ OnReadyClicked_ServerRpc(playerName)
│           ├─ readyClientCount++
│           ├─ NotifyPlayerReady_ClientRpc(playerName)  → game feed: "Player Ready"
│           └─ if readyClientCount == humanCount:
│               ├─ readyClientCount = 0
│               └─ OnReadyClicked_ClientRpc()
│                   └─ StartCountdownTimer()
│
└─ Countdown ends
    └─ OnCountdownTimerEnded()  [Server only]
        └─ OnCountdownTimerEnded_ClientRpc()  [All clients]
            ├─ gameData.SetPlayersActive()
            └─ gameData.StartTurn()  — raises OnMiniGameTurnStarted
```

### 6. Gameplay Loop: Crystal Collection & Turn Monitoring

```
gameData.OnMiniGameTurnStarted.Raise()
│
├─ TurnMonitorController.StartMonitors()
│   ├─ NetworkCrystalCollisionTurnMonitor.StartMonitor()  [crystal-target mode]
│   │   ├─ target = GetCrystalCollisionCount()  (inspector > waypoints > default 39)
│   │   ├─ [Server] _netCrystalCollisions.Value = target  [NetworkVariable]
│   │   ├─ [Server] gameData.CrystalTargetCount = target
│   │   ├─ Subscribe to ownStats.OnCrystalsCollectedChanged
│   │   └─ UpdateCrystalsRemainingUI()
│   │
│   └─ NetworkTimeBasedTurnMonitor.StartMonitor()  [time-based mode, if wired]
│       ├─ elapsedTime = 0
│       └─ Periodic UpdateTimerUI_ClientRpc() with countdown display
│
├─ Crystal collected by player:
│   ├─ Collision → updates RoundStats.CrystalsCollected
│   ├─ RoundStats NetworkVariable syncs to all clients
│   └─ NetworkCrystalCollisionTurnMonitor.UpdateCrystalsRemainingUI()
│       └─ onUpdateTurnMonitorDisplay.Raise(remaining.ToString())
│
└─ TurnMonitor.Update() — every frame
    └─ CheckForEndOfTurn()
        ├─ NetworkCrystalCollisionTurnMonitor: Any player CrystalsCollected >= target?
        └─ NetworkTimeBasedTurnMonitor: elapsedTime >= duration?
            └─ If true → OnTurnEnded() → gameData.InvokeGameTurnConditionsMet()
```

**Dual end conditions**: The scene can wire either `NetworkCrystalCollisionTurnMonitor` (crystal target), `NetworkTimeBasedTurnMonitor` (timer), or both. `TurnMonitorController` ends the turn when ANY monitor triggers.

### 7. Winner Determination & Score Sync

Winner detection is **server-authoritative** via `OnTurnEndedCustom()`:

```
TurnMonitor detects end condition → gameData.InvokeGameTurnConditionsMet()
│
├─ MultiplayerMiniGameControllerBase.HandleTurnEnd()  [server]
│   ├─ SyncTurnEnd_ClientRpc()  — notifies all clients
│   │   └─ [All clients] OnTurnEndedCustom()
│   │       └─ MultiplayerCrystalCaptureController.OnTurnEndedCustom()  [server only]
│   │           ├─ Guard: if (_finalResultsSent) return
│   │           ├─ DetermineWinner(): highest CrystalsCollected wins
│   │           ├─ Map CrystalsCollected → Score for ALL players
│   │           ├─ gameData.SortRoundStats(UseGolfRules: false)  — descending
│   │           ├─ gameData.CalculateDomainStats(UseGolfRules: false)
│   │           ├─ _finalResultsSent = true
│   │           └─ SyncFinalScoresSnapshot(winnerName)
│   │               └─ SyncFinalScores_ClientRpc(names[], scores[], domains[], crystals[], winnerName)
│   │                   ├─ Update all RoundStats on all clients
│   │                   ├─ gameData.WinnerName = winnerName
│   │                   ├─ gameData.InvokeWinnerCalculated()
│   │                   └─ gameData.InvokeMiniGameEnd()
│   │
│   └─ ExecuteServerTurnEnd()
│       └─ TurnsTakenThisRound++ → ExecuteServerRoundEnd()
│           └─ HasEndGame=false → SetupNewRound()
│               └─ MultiplayerCrystalCaptureController.SetupNewRound() override
│                   └─ if (_finalResultsSent) return  — suppresses Ready button
│
├─ CrystalCaptureStatsReporter.ReportStats()  [each client, on OnMiniGameEnd]
│   └─ [Winner only] Reports crystal count + vessel telemetry to UGS
```

**Scoring Rules:**

| Player | Score | Rank |
|---|---|---|
| Winner (most crystals, e.g., 25) | 25 | 1st (highest) |
| 2nd place (e.g., 18 crystals) | 18 | 2nd |
| 3rd place (e.g., 12 crystals) | 12 | 3rd |

Non-golf rules (`UseGolfRules = false`): Higher score = higher rank. Scores sorted descending.

### 8. End Game Cinematic

`MultiplayerCrystalCaptureEndGameController` (extends `EndGameCinematicController`) displays the result screen:

```csharp
bool didWin = !string.IsNullOrEmpty(gameData.WinnerName)
           && gameData.WinnerName == localName;
```

| Result | Header | Detail |
|---|---|---|
| Winner | `"VICTORY"` | `"WON BY N CRYSTAL(S)"` — difference vs opponent's best score |
| Loser | `"DEFEAT"` | `"LOST BY N CRYSTAL(S)"` — difference vs winner's score |

The crystal difference is calculated against the opponent's maximum score (supports 2+ players).

### 9. Replay & Rematch

Crystal Capture uses **full network scene reload** for replay (`UseSceneReloadForReplay = true`):

```
Scoreboard.OnPlayAgainButtonPressed()
│
├─ [Multiplayer: 2+ humans]
│   ├─ RequestRematch(playerName) → opponent sees rematch request panel
│   └─ Accept → RequestReplay()
│
└─ [Solo with AI / accepted rematch]
    └─ RequestReplay() → ExecuteReplaySequence()
        └─ ExecuteSceneReloadReplay()
            ├─ gameData.IsReplayReload = true
            ├─ PrepareForSceneReload_ClientRpc()  — fade to black on all clients
            ├─ await 500ms
            ├─ Clear vessel references, despawn vessels
            ├─ gameData.ResetRuntimeData()
            └─ nm.SceneManager.LoadScene(sceneName)  — full scene reload
```

An `OnResetForReplayCustom()` method exists as an in-place reset fallback (resets `_finalResultsSent`, clears crystal counts and scores). This runs only via the `ResetForReplay_ClientRpc` path, which is not the default for Crystal Capture.

## End Conditions

Crystal Capture supports two configurable end conditions via scene-wired turn monitors:

| Mode | Turn Monitor | End Condition | Winner |
|---|---|---|---|
| Crystal Target | `NetworkCrystalCollisionTurnMonitor` | First player reaches crystal target | Highest CrystalsCollected |
| Time-Based | `NetworkTimeBasedTurnMonitor` | Timer expires | Highest CrystalsCollected |

Both modes determine the winner the same way — highest `CrystalsCollected` — but they differ in what triggers the turn to end. The scene's `TurnMonitorController` can wire one or both monitors; the turn ends when ANY monitor triggers.

## HUD & UI Components

| Component | Class | Purpose |
|---|---|---|
| In-game HUD | `MultiplayerCrystalCaptureHUD` (extends `MultiplayerHUD`) | Per-player crystal count cards; subscribes to `OnCrystalsCollectedChanged`; refreshes all cards on turn start |
| Scoreboard | `MultiplayerCrystalCaptureScoreboard` (extends `Scoreboard`) | End-game player ranking; displays "N Crystals" per player; sorts descending (highest first) |
| End Game | `MultiplayerCrystalCaptureEndGameController` (extends `EndGameCinematicController`) | Victory/defeat screen with crystal difference display |
| Stats Reporter | `CrystalCaptureStatsReporter` | Reports winner's crystal count + vessel telemetry to UGS (winner only) |

## Shared State & NetworkVariables

| Variable | Owner | Type | Purpose |
|---|---|---|---|
| `NetworkCrystalCollisionTurnMonitor._netCrystalCollisions` | Server | `NetworkVariable<int>` | Crystal target synced to all clients; `OnValueChanged` writes to `gameData.CrystalTargetCount` |
| `gameData.WinnerName` | Server (via `SyncFinalScores_ClientRpc`) | `string` (non-serialized field) | Authoritative winner identity; non-empty signals "results ready" |
| `gameData.CrystalTargetCount` | Server (via `_netCrystalCollisions.OnValueChanged`) | `int` (non-serialized field) | Crystal target readable by any system |

Note: `MultiplayerCrystalCaptureController` declares **no NetworkVariables**. All network sync is via ClientRpc arrays.

## Stats & Telemetry

**UGS Stats Reporting** (winner only, via `CrystalCaptureStatsReporter`):

```csharp
ugsStatsManager.ReportCrystalCaptureStats(
    gameMode,
    gameData.SelectedIntensity.Value,
    (int)localStats.Score  // crystal count
);
```

Also reports vessel telemetry via `ugsStatsManager.ReportVesselTelemetry()`.

## Key Files Reference

| Role | File | Location |
|---|---|---|
| Game controller | `MultiplayerCrystalCaptureController.cs` | `_Scripts/Controller/Arcade/` |
| Base domain games controller | `MultiplayerDomainGamesController.cs` | `_Scripts/Controller/Arcade/` |
| Base multiplayer mini-game | `MultiplayerMiniGameControllerBase.cs` | `_Scripts/Controller/Arcade/` |
| Base mini-game controller | `MiniGameControllerBase.cs` | `_Scripts/Controller/Arcade/` |
| Crystal turn monitor (network) | `NetworkCrystalCollisionTurnMonitor.cs` | `_Scripts/Controller/Arcade/TurnMonitors/` |
| Crystal turn monitor (base) | `CrystalCollisionTurnMonitor.cs` | `_Scripts/Controller/Arcade/TurnMonitors/` |
| Time turn monitor (network) | `NetworkTimeBasedTurnMonitor.cs` | `_Scripts/Controller/Arcade/TurnMonitors/` |
| Time turn monitor (base) | `TimeBasedTurnMonitor.cs` | `_Scripts/Controller/Arcade/TurnMonitors/` |
| End game controller | `MultiplayerCrystalCaptureEndGameController.cs` | `_Scripts/Utility/DataContainers/` |
| In-game HUD | `MultiplayerCrystalCaptureHUD.cs` | `_Scripts/UI/` |
| Scoreboard | `MultiplayerCrystalCaptureScoreboard.cs` | `_Scripts/UI/` |
| Stats reporter | `CrystalCaptureStatsReporter.cs` | `_Scripts/Controller/Arcade/` |
| Arcade game config modal | `ArcadeGameConfigureModal.cs` | `_Scripts/UI/Modals/` |
| Game SO definition | `SO_ArcadeGame.cs` | `_Scripts/ScriptableObjects/` |
| Scene loader | `SceneLoader.cs` | `_Scripts/System/` |
| GameMode enum | `GameModes.cs` | `_Scripts/Data/Enums/` |
| AI vessel spawner | `ServerPlayerVesselInitializerWithAI.cs` | `_Scripts/Controller/Multiplayer/` |
| Game scene | `MinigameCrystalCaptureMultiplayer_Gameplay.unity` | `_Scenes/Multiplayer Scenes/` |

## SO Asset References

| Asset | Type | Key Values |
|---|---|---|
| Crystal Capture game config | `SO_ArcadeGame` | `Mode=MultiplayerCrystalCapture(35)`, `IsMultiplayer=true`, `MinPlayers=1`, `MaxPlayers=4`, `MinIntensity=1`, `MaxIntensity=4` |
| Arcade config runtime | `ArcadeGameConfigSO` | `Intensity`, `PlayerCount`, `SelectedShip` (runtime state) |

## Design Notes

1. **No dedicated environment generation**: Unlike HexRace's deterministic track with seed sync, Crystal Capture uses a scene-placed environment. No seed NetworkVariable or deterministic spawning is needed.

2. **Dual turn monitors**: The scene can wire either `NetworkCrystalCollisionTurnMonitor` (crystal target) or `NetworkTimeBasedTurnMonitor` (timer), or both. The `TurnMonitorController` ends the turn when ANY monitor triggers. This allows the same controller to support both "race to target" and "timed competition" variants.

3. **Score = CrystalsCollected**: The simplest scoring of the three domain game modes. No time tracking, no penalty scores. `OnTurnEndedCustom()` maps `stats.Score = stats.CrystalsCollected` for all players.

4. **HasEndGame=false + SetupNewRound suppression**: Crystal Capture handles end-game through `OnTurnEndedCustom()` → `SyncFinalScores_ClientRpc()`, which calls `InvokeWinnerCalculated()` + `InvokeMiniGameEnd()`. Setting `HasEndGame=false` prevents the base controller's `SyncGameEnd_ClientRpc` from duplicating these calls. Since `HasEndGame=false` causes `ExecuteServerRoundEnd` to call `SetupNewRound()` instead of `ExecuteServerGameEnd()`, Crystal Capture also overrides `SetupNewRound()` to return immediately when `_finalResultsSent=true`.

5. **UseSceneReloadForReplay=true**: Same as HexRace — full network scene reload for clean state. The in-place reset path (`OnResetForReplayCustom`) is retained as a fallback but not used by default.

6. **No comeback system**: Unlike HexRace (which uses `ElementalComebackSystem`), Crystal Capture has no handicap or catch-up mechanics. It is a straightforward competitive race.

7. **HUD refreshes on turn start**: `MultiplayerCrystalCaptureHUD` subscribes to `OnMiniGameTurnStarted` and calls `RefreshAllPlayerCards()`, ensuring all crystal counts are up to date when the turn begins (important for replay resets).

8. **Solo play supported**: `MinPlayersAllowed=1` allows launching Crystal Capture without a party. AI backfill provides opponents via `ServerPlayerVesselInitializerWithAI`.
