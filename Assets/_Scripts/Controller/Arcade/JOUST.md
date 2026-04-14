# Joust Game Mode — Technical Documentation

## Overview

Joust is a collision-based competitive mode for 2-12 players. Players fly vessels through each other's skimmers to score joust points. The first player to reach the collision target wins; losers are ranked by jousts remaining. The mode supports multiplayer with friends, mixed human+AI lobbies, or solo play with AI backfill (minimum 2 total players required).

**Key architectural facts:**

- **Single scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity` — no separate singleplayer scene
- **Single GameMode enum**: `GameModes.MultiplayerJoust = 34`
- **Always Netcode**: `MultiplayerJoustController` extends the multiplayer controller hierarchy. Even solo play runs through Netcode
- **Server-authoritative**: Collision sync, winner determination, and final score sync are all server-owned
- **Golf scoring**: Lower score = better rank. Winner's score = race time (seconds); losers' score = 99999f
- **In-place reset for replay**: `UseSceneReloadForReplay` is not overridden (inherits `false`)

## Class Hierarchy

```
MiniGameControllerBase (MonoBehaviour + NetworkBehaviour)
  └── MultiplayerMiniGameControllerBase
      └── MultiplayerDomainGamesController
          └── MultiplayerJoustController
```

## Execution Flow

### 1. Game Configuration (Menu_Main)

User selects Joust from the Arcade screen. `ArcadeGameConfigureModal` opens with configuration controls:

- **Player Count** (2-12): Constrained by `SO_ArcadeGame.MinPlayers` (2) and `MaxPlayers` (12)
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
| Solo, selects 2 players | 1 | 2 | 1 | 2 |
| Solo, selects 4 players | 1 | 4 | 3 | 4 |
| Solo, selects 12 players | 1 | 12 | 11 | 12 |
| 2 friends in party, selects 4 | 2 | 4 | 2 | 4 |
| 3 friends in party, selects 3 | 3 | 3 | 0 | 3 |

**Note**: `MinPlayersAllowed=2` — a minimum of 2 total players is required for jousting.

### 3. Scene Loading

`SceneLoader.LaunchGame()` (listens to `OnLaunchGame` via SOAP code subscription):

```csharp
var nm = NetworkManager.Singleton;
bool useNetworkSceneLoading = nm != null && nm.IsServer;
LoadSceneAsync(gameData.SceneName, useNetworkSceneLoading).Forget();
```

The application state transitions to `LoadingGame`. Game config is synced to clients by `MultiplayerMiniGameControllerBase.SyncGameConfigToClients_ClientRpc()` in `OnNetworkSpawn()`.

### 4. Scene Initialization

After scene load completes:

```
Scene Load Complete
│
├─ MultiplayerJoustController.OnNetworkSpawn()
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

Like Crystal Capture, Joust has **no deterministic environment generation** — the environment is scene-placed.

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

### 6. Gameplay Loop: Collision Mechanics & Turn Monitoring

```
gameData.OnMiniGameTurnStarted.Raise()
│
├─ TurnMonitorController.StartMonitors()
│   └─ NetworkJoustCollisionTurnMonitor.StartMonitor()
│       ├─ Subscribe ALL players' OnJoustCollisionChanged events
│       └─ UpdateUI()  — display collisions remaining
│
├─ Vessel-skimmer collision occurs:
│   └─ VesselExplosionBySkimmerEffectSO.Execute()
│       ├─ Speed check: skimmer vessel must be faster than impacting vessel
│       ├─ Anti-spam cooldown check (0.15s per impactor)
│       ├─ Create AOE explosion visual
│       ├─ OnJoustCollision.Raise(impacteeVessel.PlayerName)
│       │   └─ RoundStats.JoustCollisions++  (on the HIT vessel's stats)
│       │       └─ OnJoustCollisionChanged event fires
│       │           └─ NetworkJoustCollisionTurnMonitor.OnCollisionChanged(stats)
│       │               ├─ [Server] SyncCollision_ClientRpc(name, count)
│       │               │   └─ [Clients only] update local stats
│       │               └─ [Client] ReportCollision_ServerRpc(name, count)
│       │                   └─ [Server] accept if count > current, broadcast
│       └─ GameFeedAPI.PostJoust(hitPlayer, hitDomain, hitterPlayer, hitterDomain)
│
└─ TurnMonitor.Update() — every frame
    └─ NetworkJoustCollisionTurnMonitor.CheckForEndOfTurn()  [server only]
        └─ return gameData.RoundStatsList.Any(s => s.JoustCollisions >= CollisionsNeeded)
            └─ If true → OnTurnEnded() → gameData.InvokeGameTurnConditionsMet()
```

### 7. Winner Determination & Score Sync

Winner detection is **server-authoritative** via `OnTurnEndedCustom()`:

```
TurnMonitor detects collision target reached → gameData.InvokeGameTurnConditionsMet()
│
├─ MultiplayerMiniGameControllerBase.HandleTurnEnd()  [server]
│   ├─ SyncTurnEnd_ClientRpc()  — notifies all clients
│   │   └─ [All clients] OnTurnEndedCustom()
│   │       └─ MultiplayerJoustController.OnTurnEndedCustom()  [server only]
│   │           ├─ Guard: if (_finalResultsSent) return
│   │           ├─ CalculateJoustScores_Server():
│   │           │   ├─ currentTime = Time.time - gameData.TurnStartTime
│   │           │   ├─ Winner = player with highest JoustCollisions
│   │           │   ├─ winner.Score = currentTime (elapsed seconds)
│   │           │   └─ All losers: stats.Score = 99999f
│   │           ├─ gameData.SortRoundStats(UseGolfRules: true)  — ascending
│   │           ├─ gameData.CalculateDomainStats(UseGolfRules: true)
│   │           ├─ _finalResultsSent = true
│   │           └─ SyncJoustResults_Authoritative()
│   │               └─ SyncJoustResults_ClientRpc(names[], scores[], collisions[], domains[], winnerName)
│   │                   ├─ Update all RoundStats on all clients
│   │                   ├─ gameData.WinnerName = winnerName
│   │                   ├─ gameData.InvokeWinnerCalculated()
│   │                   └─ gameData.InvokeMiniGameEnd()
│   │
│   └─ ExecuteServerTurnEnd()
│       └─ TurnsTakenThisRound++ → ExecuteServerRoundEnd()
│           └─ HasEndGame=false → SetupNewRound()
│               └─ MultiplayerJoustController.SetupNewRound() override
│                   └─ if (_finalResultsSent) return  — suppresses Ready button
│
├─ JoustStatsReporter.ReportStats()  [each client, on OnMiniGameEnd]
│   └─ [Winner only] Reports time + joust count + vessel telemetry to UGS
```

**Scoring Rules:**

| Player | Score Formula | Example |
|---|---|---|
| Winner (reached collision target first) | Elapsed time in seconds | `32.5` |
| Loser (did not reach target) | `99999f` | `99999` |

Golf rules (`UseGolfRules = true`): Lower score = higher rank. Winner always ranks first. All losers are tied at 99999.

### 8. End Game Cinematic

`MultiplayerJoustEndGameController` (extends `EndGameCinematicController`) displays the result screen:

```csharp
bool didWin = !string.IsNullOrEmpty(gameData.WinnerName)
           && gameData.WinnerName == localName;
```

The end game controller holds a `[SerializeField] JoustCollisionTurnMonitor joustTurnMonitor` reference to read `CollisionsNeeded` for the display.

| Result | Header | Detail | Value |
|---|---|---|---|
| Winner | `"VICTORY"` | `"WON BY N JOUST(S)"` | Finish time (formatted as time) |
| Loser | `"DEFEAT"` | `"LOST BY N JOUST(S)"` | Jousts remaining (integer) |

### 9. Replay & Rematch

Joust uses **in-place reset** for replay (`UseSceneReloadForReplay` inherits base `false`):

```
Scoreboard.OnPlayAgainButtonPressed()
│
├─ [Multiplayer: 2+ humans]
│   ├─ RequestRematch(playerName) → opponent sees rematch request panel
│   └─ Accept → RequestReplay()
│
└─ [Solo with AI / accepted rematch]
    └─ RequestReplay() → ExecuteReplaySequence()
        └─ ResetForReplay_ClientRpc()  [All clients]
            ├─ gameData.ResetStatsDataForReplay()
            ├─ gameData.ResetPlayers()
            ├─ CameraManager.SnapPlayerCameraToTarget()
            ├─ gameData.OnResetForReplay.Raise()
            ├─ OnResetForReplayCustom():
            │   ├─ _finalResultsSent = false
            │   ├─ Clear JoustCollisions = 0 for all players
            │   ├─ Clear Score = 0f for all players
            │   └─ gameData.InvokeTurnStarted()
            └─ RaiseToggleReadyButtonEvent(true)  — show Ready button
```

## Collision Mechanics

The jousting collision system is triggered by `VesselExplosionBySkimmerEffectSO` (`_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Skimmer Effects/`):

### Collision Chain

1. **Trigger**: A `VesselImpactor` (vessel A) physically collides with a `SkimmerImpactor` (skimmer belonging to vessel B)
2. **Speed check**: Vessel B (the skimmer's owner) must be **faster** than vessel A. If not, the collision is ignored
3. **Anti-spam**: A per-impactor cooldown dictionary (`_explosionCooldown = 0.15f`) prevents rapid-fire collisions from the same vessel pair
4. **Effect**: An AOE explosion is created at the collision point
5. **Joust point**: `OnJoustCollision.Raise(vesselB.PlayerName)` — the **hit vessel** (whose skimmer was impacted) receives the joust point, not the vessel that did the impacting
6. **Game feed**: `GameFeedAPI.PostJoust()` posts a two-tone notification showing both players' names and domain colors

**Key insight**: The collision credit goes to the **impactee's vessel** (the one whose skimmer was hit by a faster opponent). This means the faster player — whose skimmer sweeps through the slower player's path — earns the point for the slower player. The design rewards getting jousted while moving fast.

### Network Collision Sync

`NetworkJoustCollisionTurnMonitor` handles collision synchronization across the network with a bifurcated approach:

| Source | Path | Purpose |
|---|---|---|
| Server detects collision | `SyncCollision_ClientRpc(name, count)` | Broadcast to all clients |
| Client detects collision | `ReportCollision_ServerRpc(name, count)` | Report to server for validation |

**Server validation**: `ReportCollision_ServerRpc` only accepts client reports where the reported count is **higher** than the server's current value, preventing stale or duplicate updates.

**Anti-recursion guard**: When the server receives `OnCollisionChanged`, it broadcasts via `SyncCollision_ClientRpc` but does **not** re-assign `JoustCollisions` on itself (which would trigger the setter → fire `OnCollisionChanged` again → infinite recursion). The `SyncCollision_ClientRpc` includes `if (IsServer) return` to prevent the host from double-processing.

## HUD & UI Components

| Component | Class | Purpose |
|---|---|---|
| In-game HUD | `MultiplayerJoustHUD` (extends `MultiplayerHUD`) | Per-player joust collision count cards; subscribes to `OnJoustCollisionChanged` |
| Scoreboard | `MultiplayerJoustScoreboard` (extends `Scoreboard`) | End-game ranking; winner shows time `MM:SS:ms`, losers show `"N Joust(s) Left"`; sorts ascending (golf rules) |
| End Game | `MultiplayerJoustEndGameController` (extends `EndGameCinematicController`) | Victory/defeat with joust difference and time display |
| Stats Reporter | `JoustStatsReporter` | Reports winner's time + joust count + vessel telemetry to UGS (winner only) |

## Shared State & NetworkVariables

| Variable | Owner | Type | Purpose |
|---|---|---|---|
| `RoundStats.n_JoustCollisions` | Server | `NetworkVariable<int>` (per player) | Joust collision count; replicated to all clients via `OnValueChanged` |
| `gameData.WinnerName` | Server (via `SyncJoustResults_ClientRpc`) | `string` (non-serialized field) | Authoritative winner identity; non-empty signals "results ready" |

Note: `MultiplayerJoustController` declares **no NetworkVariables**. `NetworkJoustCollisionTurnMonitor` also uses no NetworkVariables — it syncs collisions purely via `ReportCollision_ServerRpc` / `SyncCollision_ClientRpc`.

## Stats & Telemetry

**UGS Stats Reporting** (winner only, via `JoustStatsReporter`):

```csharp
ugsStatsManager.ReportJoustStats(
    gameMode,
    gameData.SelectedIntensity.Value,
    localStats.JoustCollisions,
    raceTime  // elapsed time in seconds
);
```

Also reports vessel telemetry via `ugsStatsManager.ReportVesselTelemetry()`.

## Key Files Reference

| Role | File | Location |
|---|---|---|
| Game controller | `MultiplayerJoustController.cs` | `_Scripts/Controller/Arcade/` |
| Base domain games controller | `MultiplayerDomainGamesController.cs` | `_Scripts/Controller/Arcade/` |
| Base multiplayer mini-game | `MultiplayerMiniGameControllerBase.cs` | `_Scripts/Controller/Arcade/` |
| Base mini-game controller | `MiniGameControllerBase.cs` | `_Scripts/Controller/Arcade/` |
| Joust turn monitor (network) | `NetworkJoustCollisionTurnMonitor.cs` | `_Scripts/Controller/Arcade/TurnMonitors/` |
| Joust turn monitor (base) | `JoustCollisionTurnMonitor.cs` | `_Scripts/Controller/Arcade/TurnMonitors/` |
| Collision effect SO | `VesselExplosionBySkimmerEffectSO.cs` | `_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Skimmer Effects/` |
| End game controller | `MultiplayerJoustEndGameController.cs` | `_Scripts/Utility/DataContainers/` |
| In-game HUD | `MultiplayerJoustHUD.cs` | `_Scripts/UI/` |
| Scoreboard | `MultiplayerJoustScoreboard.cs` | `_Scripts/UI/` |
| Stats reporter | `JoustStatsReporter.cs` | `_Scripts/Controller/Arcade/` |
| Arcade game config modal | `ArcadeGameConfigureModal.cs` | `_Scripts/UI/Modals/` |
| Game SO definition | `SO_ArcadeGame.cs` | `_Scripts/ScriptableObjects/` |
| Scene loader | `SceneLoader.cs` | `_Scripts/System/` |
| GameMode enum | `GameModes.cs` | `_Scripts/Data/Enums/` |
| AI vessel spawner | `ServerPlayerVesselInitializerWithAI.cs` | `_Scripts/Controller/Multiplayer/` |
| Game feed API | `GameFeedAPI.cs` | `_Scripts/UI/GameEventFeed/` |
| Game scene | `MinigameJoust_Gameplay.unity` | `_Scenes/Multiplayer Scenes/` |

## SO Asset References

| Asset | Type | Key Values |
|---|---|---|
| Joust game config | `SO_ArcadeGame` | `Mode=MultiplayerJoust(34)`, `IsMultiplayer=true`, `MinPlayers=2`, `MaxPlayers=12`, `MinIntensity=1`, `MaxIntensity=4` |
| Arcade config runtime | `ArcadeGameConfigSO` | `Intensity`, `PlayerCount`, `SelectedShip` (runtime state) |

## Design Notes

1. **Collision attribution is counter-intuitive**: The joust point goes to the vessel whose skimmer was hit (the `impactee`), not the vessel that physically collided. The speed check (`impacteeVessel.Speed > impactorVessel.Speed`) ensures only the faster vessel's skimmer-collisions count — essentially rewarding the faster player for "jousting" past a slower opponent.

2. **HasEndGame=false + SetupNewRound suppression**: Joust handles end-game through `OnTurnEndedCustom()` → `SyncJoustResults_ClientRpc()`, which calls `InvokeWinnerCalculated()` + `InvokeMiniGameEnd()`. Setting `HasEndGame=false` prevents the base controller's `SyncGameEnd_ClientRpc` from duplicating these calls. `SetupNewRound()` is overridden to return when `_finalResultsSent=true`.

3. **In-place reset for replay**: Unlike HexRace and Crystal Capture (which use `UseSceneReloadForReplay=true`), Joust uses the default in-place reset path. `OnResetForReplayCustom()` clears `_finalResultsSent`, zeros all `JoustCollisions` and `Score`, then invokes `InvokeTurnStarted()` to restart the HUD.

4. **Infinite recursion fix (commit 3fb2e05)**: The `OnCollisionChanged` handler in `NetworkJoustCollisionTurnMonitor` originally re-assigned `JoustCollisions` on the server side, which triggered the setter → fired `OnCollisionChanged` → infinite recursion. The fix: (1) server path in `OnCollisionChanged` only broadcasts via `SyncCollision_ClientRpc` without re-assigning, (2) `SyncCollision_ClientRpc` includes `if (IsServer) return` to prevent the host from self-updating.

5. **EndGame never triggering fix (commit 6d08fa9)**: Two bugs prevented the end game from working: (1) both the Joust-specific path and the base class path were calling `InvokeMiniGameEnd()` — fixed by adding `HasEndGame=false`, and (2) the `JoustCollisions` setter was gated on `!IsSpawned` (always false in multiplayer), so `OnJoustCollisionChanged` never fired — fixed by making the event always fire.

6. **Anti-spam cooldown**: `VesselExplosionBySkimmerEffectSO` uses a per-impactor cooldown dictionary with `_explosionCooldown = 0.15f` to prevent rapid-fire collisions from the same vessel pair. The dictionary is static (shared across all instances of the SO).

7. **Losers all tied**: All non-winners receive a score of `99999f`, so they are all ranked equally. There is no distinction between 2nd and 3rd place in Joust — only the winner matters.

8. **Minimum 2 players**: `MinPlayersAllowed=2` in the SO asset means Joust cannot be played solo without at least one AI opponent. The effective minimum player count in the UI stepper is `max(game.MinPlayersAllowed, currentPartyHumanCount)`.

9. **No comeback system**: Unlike HexRace (which uses `ElementalComebackSystem`), Joust has no handicap or catch-up mechanics. All players compete on equal footing throughout.

10. **Scoreboard requires inspector wiring**: Both `MultiplayerJoustScoreboard` and `MultiplayerJoustEndGameController` have `[SerializeField] JoustCollisionTurnMonitor joustTurnMonitor` fields that must be wired in the scene inspector to the `NetworkJoustCollisionTurnMonitor` component on the Game object.
