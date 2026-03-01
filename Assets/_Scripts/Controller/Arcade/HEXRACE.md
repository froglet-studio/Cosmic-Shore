# HexRace Game Mode — Technical Documentation

## Overview

HexRace is a competitive crystal-collection racing mode for 1-4 players. Players race along a procedurally generated track, collecting crystals. The first player to collect all crystals wins; losers are ranked by crystals remaining. The mode supports solo play with AI opponents, multiplayer with friends, or mixed human+AI lobbies.

**Key architectural facts:**

- **Single scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameHexRace.unity` — no separate singleplayer scene
- **Single GameMode enum**: `GameModes.HexRace = 33` — no separate multiplayer variant
- **Always Netcode**: `HexRaceController` extends the multiplayer controller hierarchy. Even solo play runs through Netcode (host is always active from Menu_Main)
- **Server-authoritative**: Track seed, crystal target, winner determination, and final score sync are all server-owned
- **Golf scoring**: Lower score = better rank. Winner's score = race time (seconds); losers' score = 10000 + crystals remaining

## Class Hierarchy

```
MiniGameControllerBase (MonoBehaviour + NetworkBehaviour)
  └── MultiplayerMiniGameControllerBase
      └── MultiplayerDomainGamesController
          └── HexRaceController
```

## Execution Flow

### 1. Game Configuration (Menu_Main)

User selects HexRace from the Arcade screen. `ArcadeGameConfigureModal` opens with configuration controls:

- **Player Count** (1-4): Constrained by `SO_ArcadeGame.MinPlayers` (1) and `MaxPlayers` (4)
- **Intensity** (1-4): Constrained by `SO_ArcadeGame.MinIntensity` (1) and `MaxIntensity` (4)
- **Vessel Selection**: From `SO_ArcadeGame.Captains` list (Dolphin, Squirrel, Sparrow, Rhino, etc.)

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
| 3 friends in party, selects 3 | 3 | 3 | 0 | 3 |

**Data synced to GameDataSO:**

```
gameData.SceneName              = "MinigameHexRace"     // SO_ArcadeGame.SceneName
gameData.GameMode               = GameModes.HexRace
gameData.IsMultiplayerMode      = true                  // SO_ArcadeGame.IsMultiplayer
gameData.SelectedPlayerCount    = humanCount            // actual humans
gameData.RequestedAIBackfillCount = aiBackfill          // AI to spawn
gameData.ActiveSession          = PartySession          // Relay session (if party active)
gameData.SelectedIntensity      = config.Intensity
gameData.selectedVesselClass    = config.SelectedShip.Class
```

Then `gameData.InvokeGameLaunch()` raises the `OnLaunchGame` SOAP event.

### 3. Scene Loading

`SceneLoader.LaunchGame()` (listens to `OnLaunchGame`):

```csharp
var nm = NetworkManager.Singleton;
bool useNetworkSceneLoading = nm != null && nm.IsServer;
LoadSceneAsync(gameData.SceneName, useNetworkSceneLoading).Forget();
```

| Condition | Loading Method | When |
|---|---|---|
| Host running (always from Menu_Main) | Network scene load (`nm.SceneManager.LoadScene`) | Normal flow |
| No NetworkManager | Local scene load (`SceneManager.LoadScene`) | Edge case / fallback |

The application state transitions to `LoadingGame` before scene load begins.

### 4. HexRace Scene Initialization

After scene load completes, the following chain runs:

```
Scene Load Complete
│
├─ HexRaceController.OnNetworkSpawn()
│   ├─ numberOfRounds = 1, numberOfTurnsPerRound = 1
│   ├─ Subscribe to _netTrackSeed.OnValueChanged
│   ├─ [Server] SpawnTrackEarly().Forget()  (1500ms delay, then generate seed)
│   └─ [Client, late join] SpawnTrackLocally(_netTrackSeed.Value) if seed already set
│
├─ ServerPlayerVesselInitializerWithAI.OnNetworkSpawn()
│   ├─ [Server] SpawnAIs()  — pre-spawns AI players based on RequestedAIBackfillCount
│   ├─ Mark all AI in _processedPlayers set
│   └─ base.OnNetworkSpawn()  — subscribe to OnPlayerNetworkSpawnedUlong for humans
│
├─ MultiplayerMiniGameControllerBase.InitializeAfterDelay()
│   ├─ await UniTask.Delay(1000ms)
│   ├─ gameData.InitializeGame()  → raises OnInitializeGame
│   └─ [Server] SetupNewRound()
│       ├─ readyClientCount = 0
│       ├─ RaiseToggleReadyButtonEvent(true)  — show Ready button
│       └─ base.SetupNewRound()  → timer/round bookkeeping
│
└─ Player.OnNetworkSpawn()  [for each human + AI player]
    ├─ gameData.Players.Add(this)
    ├─ Raise OnPlayerNetworkSpawnedUlong(OwnerClientId)
    └─ ServerPlayerVesselInitializer handles vessel spawning
```

### 5. Track Generation

Track generation is **deterministic** — all clients spawn an identical track from a shared seed.

**Server generates seed** (`HexRaceController.SpawnTrackEarly()`):

```csharp
await UniTask.Delay(1500ms);  // wait for intensity sync
int generatedSeed = (seed != 0) ? seed : Random.Range(int.MinValue, int.MaxValue);
_netTrackSeed.Value = generatedSeed;  // NetworkVariable → triggers all clients
```

**All clients spawn track** (`SpawnTrackLocally()`):

```csharp
segmentSpawner.Seed = trackSeed;
segmentSpawner.NumberOfSegments = scaleNumberOfSegmentsWithIntensity
    ? baseNumberOfSegments * Intensity
    : baseNumberOfSegments;
segmentSpawner.StraightLineLength = scaleLengthWithIntensity
    ? baseStraightLineLength / Intensity
    : baseStraightLineLength;
ApplyHelixIntensity();
segmentSpawner.Initialize();
```

**Intensity Scaling:**

| Parameter | Formula | Intensity 1 | Intensity 2 | Intensity 3 | Intensity 4 |
|---|---|---|---|---|---|
| Number of Segments | `base * Intensity` | 10 | 20 | 30 | 40 |
| Straight Line Length | `base / Intensity` | 400 | 200 | 133 | 100 |
| Helix Radius | `Intensity / 1.3` | 0.77 | 1.54 | 2.31 | 3.08 |

**SegmentSpawner** (`Assets/_Scripts/Controller/Arcade/SegmentSpawner.cs`):
- Deterministic spawning via seeded `Random.InitState(seed)`
- Each segment slot calls `SelectSpawnable(currentIntensity)` to pick a track piece
- Domain cycling: segments cycle through active player domains (Jade, Ruby, Gold) for color theming
- Segments positioned along Z-axis: `index * StraightLineLength` offset
- Crystals are spawned as part of track segments (via `SpawnableWaypointTrack` waypoints)

### 6. Ready State & Countdown

```
Player sees "Ready" button
│
├─ Player clicks Ready
│   └─ OnReadyClicked_() → RaiseToggleReadyButtonEvent(false)  — hide button
│       └─ OnReadyClicked_ServerRpc(playerName)
│           ├─ readyClientCount++
│           ├─ NotifyPlayerReady_ClientRpc(playerName)  → game feed: "Player Ready"
│           └─ if readyClientCount == SelectedPlayerCount:
│               ├─ readyClientCount = 0
│               └─ OnReadyClicked_ClientRpc()
│                   └─ StartCountdownTimer()  — 3-second countdown
│
└─ Countdown ends
    └─ OnCountdownTimerEnded()  [Server only]
        └─ OnCountdownTimerEnded_ClientRpc()  [All clients]
            ├─ gameData.SetPlayersActive()  — enables vessel input
            └─ gameData.StartTurn()  — IsTurnRunning=true, raises OnMiniGameTurnStarted
```

**Note**: All players (including AI) must be ready. AI players are automatically marked ready by `ServerPlayerVesselInitializerWithAI`.

### 7. Race Loop: Crystal Collection & Turn Monitoring

```
gameData.OnMiniGameTurnStarted.Raise()
│
├─ HexRaceScoreTracker.HandleTurnStarted()
│   ├─ _hasReported = false, _elapsedRaceTime = 0
│   ├─ Cache local vessel reference + VesselTelemetry
│   └─ _isTracking = true  → Update() starts counting elapsed time
│
├─ TurnMonitorController.StartMonitors()
│   └─ NetworkCrystalCollisionTurnMonitor.StartMonitor()
│       ├─ target = GetCrystalCollisionCount()  (39 crystals default)
│       ├─ _netCrystalCollisions.Value = target  [Server, NetworkVariable]
│       ├─ controller.SetCrystalsToFinishServer(target)
│       ├─ Subscribe to all players' OnCrystalsCollectedChanged events
│       └─ Push initial values via ServerSideCrystalSync
│
├─ Every frame: HexRaceScoreTracker.Update()
│   └─ _elapsedRaceTime += Time.deltaTime
│       └─ gameData.LocalRoundStats.Score = _elapsedRaceTime  (live race time)
│
├─ Crystal collected by player:
│   ├─ Collision → OnCrystalCollided → updates RoundStats.CrystalsCollected
│   ├─ ServerSideCrystalSync(stats)
│   │   └─ controller.NotifyCrystalsCollected(name, count)
│   └─ UpdateCrystalsRemainingUI()
│       └─ onUpdateTurnMonitorDisplay.Raise(remaining.ToString())
│
└─ TurnMonitorController.Update() — every frame
    └─ CheckEndOfTurn()
        └─ NetworkCrystalCollisionTurnMonitor.CheckForEndOfTurn()
            └─ return gameData.RoundStatsList.Any(s => s.CrystalsCollected >= target)
                └─ If true → OnTurnEnded() → gameData.InvokeGameTurnConditionsMet()
```

### 8. Winner Determination & Score Sync

When any player collects all crystals, the turn ends. The winner's local `HexRaceScoreTracker` reports to the server:

```
HexRaceScoreTracker.HandleGameEnd()
│
├─ crystalsRemaining = turnMonitor.GetRemainingCrystalsCountToCollect()
├─ isWinner = (crystalsRemaining <= 0)
├─ finalScore = isWinner ? _elapsedRaceTime : (10000 + crystalsRemaining)
├─ gameData.LocalRoundStats.Score = finalScore
│
├─ [Winner only]:
│   ├─ ugsStatsManager.ReportHexRaceStats(mode, intensity, cleanStreak, drift, jousts, score)
│   ├─ ugsStatsManager.ReportVesselTelemetry(telemetry, vesselType)
│   └─ controller.ReportLocalPlayerFinished(finalScore)
│       └─ ReportPlayerFinished_ServerRpc(finishTime, playerName)
│
└─ [Server] HexRaceController.ReportPlayerFinished_ServerRpc()
    ├─ Guard: if (_raceEnded) return  — only first finisher counts
    ├─ _raceEnded = true
    ├─ winnerStats.Score = finishTimeSeconds
    ├─ For each non-winner:
    │   └─ stats.Score = 10000 + (crystalsToFinish - stats.CrystalsCollected)
    ├─ gameData.SortRoundStats(UseGolfRules: true)  — lower time = rank 1
    ├─ gameData.CalculateDomainStats(UseGolfRules: true)
    └─ SyncFinalScoresSnapshot(winnerName)
        └─ SyncFinalScores_ClientRpc(names[], scores[], domains[], crystals[], winnerName)
            ├─ Update all RoundStats on all clients
            ├─ WinnerName = winnerName, RaceResultsReady = true
            ├─ gameData.InvokeWinnerCalculated()
            └─ gameData.InvokeMiniGameEnd()
```

**Scoring Rules:**

| Player | Score Formula | Example |
|---|---|---|
| Winner (collected all 39 crystals) | Race time in seconds | `45.3` |
| Loser (collected 30/39 crystals) | `10000 + remaining` | `10009` |
| Loser (collected 20/39 crystals) | `10000 + remaining` | `10019` |

Golf rules (`UseGolfRules = true`): Lower score = higher rank. Winner always ranks first.

### 9. End Game Cinematic

`HexRaceEndGameController` (extends `EndGameCinematicController`) displays the result screen:

```csharp
// Single source of truth from server
bool didWin = hexRaceController.RaceResultsReady
           && hexRaceController.WinnerName == gameData.LocalPlayer.Name;
```

| Result | Header | Label | Value |
|---|---|---|---|
| Winner | `"VICTORY"` | `"RACE TIME"` | Race time (formatted as mm:ss) |
| Loser | `"DEFEAT"` | `"CRYSTALS LEFT"` | Crystals remaining (integer) |

### 10. Replay & Rematch

`HexRaceController.OnResetForReplayCustom()`:

```
Reset:
├─ _raceEnded = false, _trackSpawned = false
├─ WinnerName = "", RaceResultsReady = false
├─ Clear all RoundStats scores + crystals
├─ [Server] Reset NetworkVariables (_netCrystalsToFinish, _netTrackSeed)
├─ [Server] SpawnTrackEarly().Forget()  — re-generate track with new seed
└─ RaiseToggleReadyButtonEvent(true)  — show Ready button again
```

Replay flows through `MultiplayerMiniGameControllerBase.RequestReplay()`:
- Client → `RequestReplay_ServerRpc()` → Server → `ResetForReplay_ClientRpc()` (all clients)
- Rematch requests broadcast via `RequestRematch_ServerRpc/ClientRpc` with opponent notification

## Elemental Comeback System

`ElementalComebackSystem` (attached in scene alongside `HexRaceController`):

- **Source**: `ScoreDifferenceSource.CrystalsCollected` — tracks crystal gap between players
- **Effect**: Losing players receive elemental buffs proportional to their crystal deficit
- **Example**: With `SpaceWeight=1`, a player 4 crystals behind the leader gets Space element +4, growing their skimmer
- **Update interval**: Recalculates every 1 second
- **Comeback profile**: Configured via `SO_ElementalComebackProfile` (per-vessel, per-element weights)

## HUD & UI Components

| Component | Class | Purpose |
|---|---|---|
| In-game HUD | `HexRaceHUD` (extends `MultiplayerHUD`) | Per-player crystal count cards; subscribes to `OnOmniCrystalsCollectedChanged` |
| HUD View | `HexRaceHUDView` (extends `MiniGameHUDView`) | Visual layout for HexRace HUD |
| Scoreboard | `HexRaceScoreboard` (extends `Scoreboard`) | End-game player ranking display |
| Stats Provider | `HexRaceStatsProvider` (extends `ScoreboardStatsProvider`) | Provides clean streak, drift, joust stats for scoreboard (WIP) |
| End Game | `HexRaceEndGameController` (extends `EndGameCinematicController`) | Victory/defeat screen with score reveal animation |
| Player Stats | `HexRacePlayerStatsProfile` | Cloud-saved best race times by mode+intensity key |

## NetworkVariable Inventory

| Variable | Owner | Type | Purpose |
|---|---|---|---|
| `HexRaceController._netTrackSeed` | Server | `NetworkVariable<int>` | Deterministic track seed — all clients spawn identical track |
| `HexRaceController._netCrystalsToFinish` | Server | `NetworkVariable<int>` | Crystal collection target (default 39) |
| `NetworkCrystalCollisionTurnMonitor._netCrystalCollisions` | Server | `NetworkVariable<int>` | Crystal target for UI display |
| `HexRaceController.WinnerName` | Server (via ClientRpc) | `string` (local) | Authoritative winner identity |
| `HexRaceController.RaceResultsReady` | Server (via ClientRpc) | `bool` (local) | Flag: final scores have been synced |

## Stats & Telemetry

**UGS Stats Reporting** (winner only, via `HexRaceScoreTracker`):

```csharp
ugsStatsManager.ReportHexRaceStats(
    GameModes.HexRace,
    intensity,
    squirrelTelemetry?.MaxCleanStreak ?? 0,
    vesselTelemetry.MaxDriftTime,
    squirrelTelemetry?.JoustsWon ?? 0,
    finalScore  // race time
);
```

**Cloud-saved profile** (`HexRacePlayerStatsProfile`):
- `BestMultiplayerRaceTimes`: Dictionary keyed by `"HexRace_{intensity}"`, value = best race time (lower is better)
- Stored via `UGSStatsManager.ReportScore()` → `PlayerDataService` → Unity Cloud Save

## Key Files Reference

| Role | File | Location |
|---|---|---|
| Game controller | `HexRaceController.cs` | `_Scripts/Controller/Arcade/` |
| Base multiplayer controller | `MultiplayerDomainGamesController.cs` | `_Scripts/Controller/Arcade/` |
| Base multiplayer mini-game | `MultiplayerMiniGameControllerBase.cs` | `_Scripts/Controller/Arcade/` |
| Base mini-game controller | `MiniGameControllerBase.cs` | `_Scripts/Controller/Arcade/` |
| Score tracker | `HexRaceScoreTracker.cs` | `_Scripts/Controller/Arcade/` |
| Stats provider | `HexRaceStatsProvider.cs` | `_Scripts/Controller/Arcade/` |
| Crystal turn monitor | `NetworkCrystalCollisionTurnMonitor.cs` | `_Scripts/Controller/Arcade/TurnMonitors/` |
| Base crystal monitor | `CrystalCollisionTurnMonitor.cs` | `_Scripts/Controller/Arcade/TurnMonitors/` |
| Track spawner | `SegmentSpawner.cs` | `_Scripts/Controller/Arcade/` |
| End game controller | `HexRaceEndGameController.cs` | `_Scripts/Utility/DataContainers/` |
| In-game HUD | `HexRaceHUD.cs` | `_Scripts/UI/` |
| HUD view | `HexRaceHUDView.cs` | `_Scripts/UI/` |
| Scoreboard | `HexRaceScoreboard.cs` | `_Scripts/UI/` |
| Player stats profile | `HexRacePlayerStatsProfile.cs` | `_Scripts/UI/` |
| Elemental comeback | `ElementalComebackSystem.cs` | `_Scripts/Controller/Arcade/` |
| Arcade game config modal | `ArcadeGameConfigureModal.cs` | `_Scripts/UI/Modals/` |
| Arcade game config SO | `ArcadeGameConfigSO.cs` | `_Scripts/UI/Modals/` |
| Game SO definition | `SO_ArcadeGame.cs` | `_Scripts/ScriptableObjects/` |
| Scene loader | `SceneLoader.cs` | `_Scripts/System/` |
| GameMode enum | `GameModes.cs` | `_Scripts/Data/Enums/` |
| Game scene | `MinigameHexRace.unity` | `_Scenes/Multiplayer Scenes/` |
| UGS stats manager | `UGSStatsManager.cs` | `_Scripts/UI/` |
| AI vessel spawner | `ServerPlayerVesselInitializerWithAI.cs` | `_Scripts/Controller/Multiplayer/` |

## SO Asset References

| Asset | Type | Key Values |
|---|---|---|
| HexRace game config | `SO_ArcadeGame` | `Mode=HexRace`, `IsMultiplayer=true`, `MinPlayers=1`, `MaxPlayers=4`, `GolfScoring=true` |
| Arcade config runtime | `ArcadeGameConfigSO` | `Intensity`, `PlayerCount`, `SelectedShip` (runtime state) |

## Design Notes

1. **No separate singleplayer scene**: The original `MultiplayerHexRace` concept was consolidated into a single scene. All games run through Netcode regardless of player count. Solo games run as a host with AI-spawned opponents.

2. **Server authority is critical**: Only the server can declare the race ended (`_raceEnded` flag). If two players finish on the same frame, only the first `ServerRpc` through the gate wins. This prevents race conditions.

3. **Deterministic track**: All clients must produce identical tracks from the same seed + intensity. The `SegmentSpawner` uses `Random.InitState(seed)` before spawning to ensure determinism.

4. **Crystal count default**: 39 crystals comes from the track's `SpawnableWaypointTrack` waypoint count. This can be overridden via `crystalsToFinishOverride` (inspector) or `_netCrystalsToFinish` (NetworkVariable, set by turn monitor).

5. **Comeback mechanics**: The `ElementalComebackSystem` is critical for competitive balance — it buffs losing players proportionally to their crystal deficit, preventing runaway victories.

6. **Vessel flexibility**: While Squirrel is the primary racing vessel, HexRace supports multiple vessel types via `SO_ArcadeGame.Captains`. Players can select any available vessel.
