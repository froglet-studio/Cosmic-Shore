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
- **Vessel Selection**: From `SO_ArcadeGame.Vessels` list (Squirrel, Manta, Sparrow)

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

`SceneLoader.LaunchGame()` (listens to `OnLaunchGame` via SOAP code subscription):

`SceneLoader` is a `MonoBehaviour` singleton living in Bootstrap (DontDestroyOnLoad). It subscribes to SOAP events in code — no per-scene `EventListenerNoParam` wiring.

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

Game config (intensity, player count, AI backfill, etc.) is synced to clients by `MultiplayerMiniGameControllerBase.SyncGameConfigToClients_ClientRpc()` in the game scene's `OnNetworkSpawn()`, not by SceneLoader.

### 4. HexRace Scene Initialization

After scene load completes, the following chain runs:

```
Scene Load Complete
│
├─ HexRaceController.OnNetworkSpawn()
│   ├─ base.OnNetworkSpawn()  — wires turn-end handler, syncs game config
│   ├─ numberOfRounds = 1, numberOfTurnsPerRound = 1
│   ├─ segmentSpawner.ExternalResetControl = true  — prevent auto-reset on replay
│   ├─ Subscribe to _netTrackSeed.OnValueChanged
│   ├─ [Server] SpawnTrackEarly().Forget()  (1500ms delay, then generate seed)
│   ├─ [Client, seed already set] SpawnTrackLocally(_netTrackSeed.Value)
│   └─ [Client, seed not yet set] StartSeedPoll()  — poll fallback (100ms × 50 attempts)
│
├─ ServerPlayerVesselInitializerWithAI.OnNetworkSpawn()
│   ├─ [Server] SpawnAIs()  — pre-spawns AI players based on RequestedAIBackfillCount
│   ├─ Mark all AI in _processedPlayers set
│   └─ base.OnNetworkSpawn()  — subscribe to OnPlayerNetworkSpawnedUlong for humans
│
├─ MultiplayerMiniGameControllerBase.OnNetworkSpawn()
│   ├─ [Server] SyncGameConfigToClients_ClientRpc()  — syncs intensity, player count, AI backfill, team count to clients
│   └─ InitializeAfterDelay().Forget()
│
├─ MultiplayerMiniGameControllerBase.InitializeAfterDelay()
│   ├─ await UniTask.Delay(1000ms)
│   ├─ gameData.InitializeGame()  → raises OnInitializeGame
│   ├─ [Replay reload] Subscribe to OnClientReady → FadeFromBlackOnReplay
│   ├─ [Server] gameData.InvokeSessionStarted()  — AppState → InGame
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

**Client track seed synchronization** has three redundant paths to ensure reliability:

| Path | Trigger | When |
|---|---|---|
| Immediate | `_netTrackSeed.Value != 0` at spawn time | Client joined after server set seed |
| OnValueChanged | `_netTrackSeed.OnValueChanged` callback | Normal flow: client spawned before server writes seed |
| Poll fallback | `WaitForTrackSeed()` — polls every 100ms for up to 5s | Edge case: `OnValueChanged` missed initial sync |

All three paths call `SpawnTrackLocally()`, which is guarded by `_trackSpawned` to prevent double-spawning.

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

**SegmentSpawner** (`Assets/_Scripts/Controller/Environment/MiniGameObjects/SegmentSpawner.cs`):
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
│       ├─ [Server] _netCrystalCollisions.Value = target  [NetworkVariable]
│       ├─ [Server] gameData.CrystalTargetCount = target
│       ├─ Subscribe to ownStats.OnCrystalsCollectedChanged
│       └─ UpdateCrystalsRemainingUI()
│
├─ Every frame: HexRaceScoreTracker.Update()
│   └─ _elapsedRaceTime += Time.deltaTime
│       └─ gameData.LocalRoundStats.Score = _elapsedRaceTime  (live race time)
│
├─ Crystal collected by player:
│   ├─ Collision → OnCrystalCollided → updates RoundStats.CrystalsCollected
│   ├─ RoundStats NetworkVariable syncs CrystalsCollected to all clients
│   └─ NetworkCrystalCollisionTurnMonitor.UpdateCrystalsRemainingUI()
│       └─ onUpdateTurnMonitorDisplay.Raise(remaining.ToString())
│
└─ TurnMonitorController.Update() — every frame
    └─ CheckEndOfTurn()
        └─ NetworkCrystalCollisionTurnMonitor.CheckForEndOfTurn()
            └─ return gameData.RoundStatsList.Any(s => s.CrystalsCollected >= target)
                └─ If true → OnTurnEnded() → gameData.InvokeGameTurnConditionsMet()
```

### 8. Winner Determination & Score Sync

When any player collects all crystals, the turn monitor detects the condition and the turn ends. Winner detection is **server-authoritative** via `OnTurnEndedCustom()`:

```
TurnMonitorController.CheckEndOfTurn()  [server, every frame]
│   └─ NetworkCrystalCollisionTurnMonitor.CheckForEndOfTurn()
│       └─ return gameData.RoundStatsList.Any(s => s.CrystalsCollected >= target)
│           └─ If true → gameData.InvokeGameTurnConditionsMet()
│
├─ MultiplayerMiniGameControllerBase.HandleTurnEnd()  [server]
│   ├─ SyncTurnEnd_ClientRpc()  — notifies all clients
│   │   └─ [All clients] OnTurnEndedCustom()
│   │       └─ HexRaceController.OnTurnEndedCustom()  [server only — guard: if (!IsServer) return]
│   │           ├─ Guard: if (_raceEnded) return
│   │           ├─ Find winner: first player with CrystalsCollected >= target
│   │           ├─ _raceEnded = true
│   │           ├─ winner.Score = elapsed race time (from LocalRoundStats.Score)
│   │           ├─ For each non-winner:
│   │           │   └─ stats.Score = 10000 + (target - stats.CrystalsCollected)
│   │           ├─ gameData.SortRoundStats(UseGolfRules: true)
│   │           ├─ gameData.CalculateDomainStats(UseGolfRules: true)
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
│               └─ HexRaceController.SetupNewRound() override
│                   └─ if (_raceEnded) return  — suppresses Ready button after race ends
│
├─ HexRaceScoreTracker.HandleGameEnd()  [each client, on OnMiniGameTurnEnd]
│   ├─ Calculates local finalScore (race time for winner, 10000+remaining for losers)
│   └─ [Winner only] Reports UGS stats + vessel telemetry to UGSStatsManager
```

**`SetupNewRound()` suppression**: After the turn→round→game flow completes, the base controller calls `SetupNewRound()` (because `HasEndGame=false`). `HexRaceController` overrides this to return immediately when `_raceEnded=true`, preventing the Ready button from appearing after the race ends.

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
// Single source of truth from server — set by SyncFinalScores_ClientRpc
bool didWin = !string.IsNullOrEmpty(gameData.WinnerName)
           && gameData.WinnerName == localName;
```

| Result | Header | Label | Value |
|---|---|---|---|
| Winner | `"VICTORY"` | `"RACE TIME"` | Race time (formatted as mm:ss) |
| Loser | `"DEFEAT"` | `"CRYSTALS LEFT"` | Crystals remaining (integer) |

### 10. Replay & Rematch

HexRace uses **full network scene reload** for replay (`UseSceneReloadForReplay = true`). The in-place `OnResetForReplayCustom()` method was removed — flora, fauna, and environment spawners don't fully reset in-place, so a clean scene reload is required.

**Replay flow** (triggered by Scoreboard "Play Again" button):

```
Scoreboard.OnPlayAgainButtonPressed()
│
├─ [Multiplayer: 2+ humans]
│   ├─ RequestRematch(playerName) → RequestRematch_ServerRpc → RequestRematch_ClientRpc
│   │   └─ Opponent sees "PlayerName wants a rematch!" panel (YES/NO)
│   ├─ YES → OnAcceptRematch() → RequestReplay()
│   └─ NO → OnDeclineRematch() → NotifyRematchDeclined()
│
└─ [Solo with AI / accepted rematch]
    └─ RequestReplay() → [Client] RequestReplay_ServerRpc → ExecuteReplaySequence()

ExecuteReplaySequence()  [Server]
│
├─ Guard: if (_isResetting) return
├─ _isResetting = true
├─ UseSceneReloadForReplay=true → ExecuteSceneReloadReplay().Forget()
│
└─ ExecuteSceneReloadReplay()
    ├─ gameData.IsReplayReload = true
    ├─ PrepareForSceneReload_ClientRpc()  — all clients:
    │   ├─ gameData.IsReplayReload = true
    │   └─ sceneTransitionManager.SetFadeImmediate(1f)  — instant fade to black
    ├─ await UniTask.Delay(500ms)  — wait for fade
    ├─ Clear vessel references:
    │   ├─ For each player: NetVesselId = 0
    │   ├─ For each vessel: NetworkObject.Despawn(false)
    │   └─ gameData.Vessels.Clear()
    ├─ gameData.ResetRuntimeData()
    └─ nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single)
        └─ Scene destroyed + reloaded → fresh OnNetworkSpawn for everything

Post-Reload (via InitializeAfterDelay):
├─ gameData.IsReplayReload detected → subscribe to OnClientReady
├─ OnClientReady fires (vessel spawned) → FadeFromBlackOnReplay()
│   └─ sceneTransitionManager.FadeFromBlack()  — smooth fade in
└─ Normal initialization continues (SetupNewRound, Ready button, etc.)
```

**Rematch request UI** (multiplayer with 2+ humans):

| Panel | Recipient | Auto-Dismiss |
|---|---|---|
| "Waiting for Response..." | Requester (sender) | 2 seconds |
| "PlayerName wants a rematch!" (YES/NO) | Opponent | None — waits for button press |
| "Rematch declined" | Requester (if NO pressed) | 2 seconds |

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

## Shared State & NetworkVariables

| Variable | Owner | Type | Purpose |
|---|---|---|---|
| `HexRaceController._netTrackSeed` | Server | `NetworkVariable<int>` | Deterministic track seed — all clients spawn identical track |
| `NetworkCrystalCollisionTurnMonitor._netCrystalCollisions` | Server | `NetworkVariable<int>` | Crystal target synced to all clients; `OnValueChanged` writes to `gameData.CrystalTargetCount` |
| `gameData.WinnerName` | Server (via `SyncFinalScores_ClientRpc`) | `string` (non-serialized field) | Authoritative winner identity; non-empty signals "results ready" |
| `gameData.CrystalTargetCount` | Server (via `_netCrystalCollisions.OnValueChanged`) | `int` (non-serialized field) | Crystal target readable by any system (controller, HUD, end game) |

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
| Track spawner | `SegmentSpawner.cs` | `_Scripts/Controller/Environment/MiniGameObjects/` |
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
| HexRace game config | `SO_ArcadeGame` | `Mode=HexRace`, `IsMultiplayer=true`, `MinPlayers=1`, `MaxPlayers=4`, `GolfScoring=true`, `Vessels=[Squirrel, Manta, Sparrow]` |
| Arcade config runtime | `ArcadeGameConfigSO` | `Intensity`, `PlayerCount`, `SelectedShip` (runtime state) |

## Design Notes

1. **No separate singleplayer scene**: The original `MultiplayerHexRace` concept was consolidated into a single scene. All games run through Netcode regardless of player count. Solo games run as a host with AI-spawned opponents.

2. **Server-authoritative winner detection**: Winner detection runs entirely on the server via `OnTurnEndedCustom()`, which fires when `SyncTurnEnd_ClientRpc` is sent to all clients. The server finds the first player with enough crystals, sets `_raceEnded=true`, calculates all scores, and broadcasts via `SyncFinalScores_ClientRpc`. `HexRaceScoreTracker` only handles local elapsed-time tracking and UGS stats reporting — it does not participate in winner determination.

3. **Deterministic track**: All clients must produce identical tracks from the same seed + intensity. The `SegmentSpawner` uses `Random.InitState(seed)` before spawning to ensure determinism.

4. **Crystal target resolution**: The crystal target is resolved by `CrystalCollisionTurnMonitor.GetCrystalCollisionCount()` in priority order: (1) inspector `CrystalCollisions` field if non-zero, (2) `SpawnableWaypointTrack` waypoint count × laps, (3) default 39. The resolved target is synced to all clients via `NetworkCrystalCollisionTurnMonitor._netCrystalCollisions` NetworkVariable and published to `gameData.CrystalTargetCount`.

5. **Comeback mechanics**: The `ElementalComebackSystem` is critical for competitive balance — it buffs losing players proportionally to their crystal deficit, preventing runaway victories. Configured via `SO_ElementalComebackProfile` with per-vessel, per-element weights.

6. **HasEndGame=false + SetupNewRound suppression**: `HexRaceController` sets `HasEndGame => false` to prevent the base controller's turn→round→game flow from calling `SyncGameEnd_ClientRpc` (which would duplicate `InvokeMiniGameEnd`). HexRace handles end-game entirely through `OnTurnEndedCustom()` → `SyncFinalScores_ClientRpc()`. Since `HasEndGame=false` causes `ExecuteServerRoundEnd` to call `SetupNewRound()` instead of `ExecuteServerGameEnd()`, `HexRaceController` also overrides `SetupNewRound()` to return immediately when `_raceEnded=true`, preventing the Ready button from reappearing.

7. **Unified TurnMonitorController**: The scene uses a single `TurnMonitorController` class that orchestrates all turn monitors. It handles both singleplayer (`OnEnable`) and multiplayer (`OnNetworkSpawn`) lifecycle automatically. The turn monitor subclasses (e.g., `NetworkCrystalCollisionTurnMonitor`) handle their own network sync internally.

8. **DI-injected config**: `ArcadeGameConfigureModal` uses `[Inject]` for `GameDataSO` and `HostConnectionDataSO` (not `[SerializeField]`). Both are DI-registered in `AppManager`.

9. **Full scene reload for replay**: HexRace uses `UseSceneReloadForReplay = true` instead of in-place reset. Flora, fauna, and environment spawners don't fully reset in-place, so a clean network scene reload ensures pristine state. The `OnResetForReplayCustom()` method was removed entirely — all race state, track, and environment objects are destroyed with the scene and re-initialized fresh via `OnNetworkSpawn`.

10. **ExternalResetControl**: `HexRaceController` sets `segmentSpawner.ExternalResetControl = true` on spawn to prevent `SegmentSpawner` from auto-resetting on `OnResetForReplay` events. Since HexRace uses scene reload, the track lifecycle is managed entirely by the controller (seed generation → `SpawnTrackLocally()` → scene destruction on replay).

11. **Client seed poll fallback**: In addition to the `OnValueChanged` callback on `_netTrackSeed`, clients start a polling fallback (`WaitForTrackSeed`) that checks the NetworkVariable every 100ms for up to 5 seconds. This covers edge cases where `OnValueChanged` doesn't fire for the initial sync and the `SpawnTrack_ClientRpc` was sent before the client spawned.

12. **Vessel flexibility**: While Squirrel is the primary racing vessel, HexRace supports multiple vessel types via `SO_ArcadeGame.Captains`. Players can select any available vessel.
