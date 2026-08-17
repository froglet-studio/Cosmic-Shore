# Crystal Capture Game Mode — Technical Documentation

## Overview

Crystal Capture is a competitive crystal-collection mode for 1-4 players. Teams (the active **domains** Jade / Ruby / Gold) race to collect crystals — the first domain whose summed CrystalsCollected reaches the inspector-configured target wins. Solo play with AI backfill, full multiplayer parties, and mixed human+AI lobbies all flow through the same domain-aggregated trigger.

**Key architectural facts:**

- **Single scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameCrystalCaptureMultiplayer_Gameplay.unity` — no separate singleplayer scene
- **Single GameMode enum**: `GameModes.MultiplayerCrystalCapture = 35`
- **Always Netcode**: `MultiplayerCrystalCaptureController` extends the multiplayer controller hierarchy. Even solo play runs through Netcode (host is always active from Menu_Main)
- **Server-authoritative**: Winner determination and final score sync are server-owned
- **Golf-timed scoring** (finish-time change): winning-domain players' `Score` = match finish
  time (displayed mm:ss:cs); losing players' `Score` = `GolfScoreSentinels` DnfThreshold +
  team crystals remaining (displayed "N Crystals Left"; individual crystals on the secondary
  line). Lower = better, like HexRace
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
│   └─ NetworkCrystalCollisionTurnMonitor.StartMonitor()  [crystal-target mode]
│       ├─ target = GetCrystalCollisionCount()  (inspector > waypoints > default 39)
│       ├─ [Server] _netCrystalCollisions.Value = target  [NetworkVariable]
│       ├─ [Server] gameData.CrystalTargetCount = target
│       ├─ Subscribe to OnCrystalsCollectedChanged on EVERY RoundStats (so the
│       │   HUD's "remaining" readout reflects the local DOMAIN sum, not just self)
│       └─ UpdateCrystalsRemainingUI()  — shows gameData.ScoringRule.Remaining(gameData, localDomain)
│
├─ Crystal collected by player:
│   ├─ Collision → updates RoundStats.CrystalsCollected
│   ├─ RoundStats NetworkVariable syncs to all clients
│   └─ NetworkCrystalCollisionTurnMonitor.UpdateCrystalsRemainingUI()
│       └─ onUpdateTurnMonitorDisplay.Raise(remaining.ToString())
│
└─ TurnMonitor.Update() — every frame
    └─ CheckForEndOfTurn()
        └─ NetworkCrystalCollisionTurnMonitor: gameData.ScoringRule.IsObjectiveReached(gameData, out _)?
            └─ If true → OnTurnEnded() → gameData.InvokeGameTurnConditionsMet()
```

**Domain-aggregated end condition**: The scene wires `NetworkCrystalCollisionTurnMonitor` with a per-domain crystal target. The turn ends as soon as any active domain's summed CrystalsCollected reaches the target — teammates (humans + AI on the same Domain) capture together.

### 7. Winner Determination & Score Sync

Winner detection is **server-authoritative** via `OnTurnEndedCustom()`:

> **Source of truth:** the end condition, winning domain, per-player score, and ranked
> results are produced by `CrystalCaptureScoringRuleSO` (`IsObjectiveReached` / `ResolveWinner`
> / `AssignScores` / `BuildResults`) via `gameData.ScoringRule`; the turn monitor + controller
> delegate to it. (The old `gameData.TryGetDomainReachingCrystalTarget` helper was retired —
> per-domain sums come from `ScoringMetrics.SumByDomain(gameData, Crystals, domain)`.)

```
TurnMonitor detects end condition → gameData.InvokeGameTurnConditionsMet()
│
├─ MultiplayerMiniGameControllerBase.HandleTurnEnd()  [server]
│   ├─ SyncTurnEnd_ClientRpc()  — notifies all clients
│   │   └─ [All clients] OnTurnEndedCustom()
│   │       └─ MultiplayerCrystalCaptureController.OnTurnEndedCustom()  [server only]
│   │           ├─ Guard: if (_finalResultsSent) return
│   │           ├─ DetermineWinner(): active domain with the highest ScoringMetrics.SumByDomain(gameData, Crystals, …);
│   │           │   representative WinnerName = best individual contributor on that domain
│   │           ├─ Map CrystalsCollected → Score for ALL players (individual contribution)
│   │           ├─ gameData.SortRoundStats(UseGolfRules: true)   — ascending (golf)
│   │           ├─ gameData.CalculateDomainStats(UseGolfRules: true)
│   │           ├─ _finalResultsSent = true
│   │           └─ SyncFinalResults(winnerName)
│   │               └─ SyncFinalResults_ClientRpc (shared MultiplayerDomainGamesController tail)(names[], scores[], domains[], crystals[], winnerName)
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
│   └─ [Winning-domain players] Report finish time + vessel telemetry to UGS
```

**Scoring Rules:**

Ranking is **TEAM-major** (matching HexRace/Joust, where the team outcome dominates the
individual one): domains are placed by their summed crystals (`ScoringRuleSO.ResolvePlacementOrder`
— the same aggregation that ends the turn and picks `WinnerDomain`), then teammates rank by
individual crystals (name as the final cross-peer tiebreak). Every winning-domain player ranks
above every losing-domain player — a losing player who ties/outscores the top winner individually
still ranks below the winning team (this previously flipped the round winner in team games:
`Results[0]` sat on the losing domain and clobbered `WinnerDomain` via `SetResults`; the derive is
now unset-only).

| Player (2v2 example) | Score | Rank |
|---|---|---|
| Winning domain, best (e.g., 12 crystals) | 12 | 1st |
| Winning domain, teammate (e.g., 8) | 8 | 2nd |
| Losing domain, best (e.g., 12) | 12 | 3rd |
| Losing domain, teammate (e.g., 5) | 5 | 4th |

Golf rules (`UseGolfRules = true`): lower score = better. Winners carry the match finish time
(`Time.time - gameData.TurnStartTime`, computed server-side in `OnTurnEndedCustom` and
replicated via the snapshot RPC); losers carry `GolfScoreSentinels.EncodeHexRaceLoserScore(teamRemaining)`
(the shared time-golf sentinel scheme — the "HexRace" naming is legacy). Individual crystal
counts still order teammates and fill the scoreboard's secondary line.

### 8. End Game (Scoreboard)

There is no end-game cinematic. When the game ends, `EndGameSequencer` halts the vessels, plays the GameEnd SFX, and raises `OnShowGameEndScreen` — the signal the **`Scoreboard`** (and `LifeForm` ecology cleanup) already listen for. The `Scoreboard` is the sole end-game UI: a `"{DOMAIN} VICTORY"` banner plus one ranked `PlayerScoreCard` per player. Card order, score and the secondary line all come from `CrystalCaptureScoringRuleSO` (`Results`) — the single scoring source (crystal totals / difference vs the opposing domain).

The old animated per-player `VICTORY`/`DEFEAT` reveal belonged to the removed cinematic. `CrystalCaptureScoringRuleSO.BuildReveal` is retained but currently unconsumed.

The crystal difference is calculated against the opponent's maximum score (supports 2+ players).

### 9. Replay (Play Again)

Crystal Capture uses **full network scene reload** for replay (`UseSceneReloadForReplay = true`). Play Again is **host-only** (the old rematch-request flow was removed with the per-mode scoreboard subclasses): `Scoreboard.ConfigureLobbyButtons` hides Play Again + Main Menu for non-host clients, and the call path is guarded in both `Scoreboard.OnPlayAgainButtonPressed` and `RequestReplay`. The host's replay carries every client along via the Netcode scene load.

```
Scoreboard.OnPlayAgainButtonPressed()  [host only]
├─ HideHostNavButtons()  — hide Play Again + Main Menu (anti-spam)
└─ gameController.RequestReplay() → ExecuteReplaySequence()
    └─ ExecuteSceneReloadReplay()
        ├─ gameData.IsReplayReload = true
        ├─ PrepareForSceneReload_ClientRpc()  — fade to black on all clients
        ├─ await 500ms
        ├─ Clear vessel references, despawn AI players + vessels
        ├─ gameData.ResetRuntimeData()
        └─ nm.SceneManager.LoadScene(sceneName)  — full scene reload
```

An `OnResetForReplayCustom()` method exists as an in-place reset fallback (resets `_finalResultsSent`, clears crystal counts and scores). This runs only via the `ResetForReplay_ClientRpc` path, which is not the default for Crystal Capture.

**Scene wiring requirement (this broke Play Again once — `BUGS.md` B13):** the Crystal Capture scene removes the GameCanvas-HexRace prefab's internal `Scoreboard` component and adds its own scene-level `Scoreboard` (with `gameController` → `MultiplayerCrystalCaptureController`). The prefab's `PlayAgainButton.onClick` persistent call targets the *internal* prefab Scoreboard, so the scene **must override** the Button's onClick target to point at the scene-added Scoreboard — without the override the call's target resolves null (removed component) and the click silently no-ops. The scene also wires `mainMenuButton` (HomeButton GO) and `onClickToMainMenu` (`EventOnClickToMainMenuButton.asset`) so client-hiding and the anti-spam hide work (`BUGS.md` B14).

## End Conditions

Crystal Capture ends when the first active domain's summed CrystalsCollected reaches the target. The scene wires a single `NetworkCrystalCollisionTurnMonitor` with `CrystalCollisions` set to the domain target (default 20).

| Turn Monitor | End Condition | Winner |
|---|---|---|
| `NetworkCrystalCollisionTurnMonitor` | First domain whose `ScoringMetrics.SumByDomain(Crystals)` ≥ `CrystalCollisions` (via `gameData.ScoringRule.IsObjectiveReached`) | Domain with highest aggregate; representative `WinnerName` = best individual contributor on the winning domain |

To swap the end condition mode (e.g., timer-based), replace the turn monitor in the scene — the controller drives the rest of the flow through `OnTurnEndedCustom()` regardless of which monitor triggers it.

## HUD & UI Components

| Component | Class | Purpose |
|---|---|---|
| In-game HUD | `MultiplayerCrystalCaptureHUD` (extends `MultiplayerHUD`) | Per-player crystal count cards; subscribes to `OnCrystalsCollectedChanged`; refreshes all cards on turn start |
| Scoreboard | `Scoreboard` (base — per-mode subclass deleted in the scoring refactor; scene-added component, `gameController` wired in inspector) | End-game player ranking; "N Crystals" per card from `CrystalCaptureScoringRuleSO.BuildResults`; team-major order (winning domain's players first, then by individual crystals) |
| End Game | `EndGameSequencer` (shared) | Halts vessels, plays GameEnd SFX, raises `OnShowGameEndScreen` → the `Scoreboard` shows results. No cinematic. |
| Stats Reporter | `CrystalCaptureStatsReporter` | Reports finish time + vessel telemetry to UGS (winning-domain players; `IsFinishTime` gate) |

## Shared State & NetworkVariables

| Variable | Owner | Type | Purpose |
|---|---|---|---|
| `NetworkCrystalCollisionTurnMonitor._netCrystalCollisions` | Server | `NetworkVariable<int>` | Crystal target synced to all clients; `OnValueChanged` writes to `gameData.CrystalTargetCount` |
| `gameData.WinnerName` | Server (via `SyncFinalResults_ClientRpc (shared MultiplayerDomainGamesController tail)`) | `string` (non-serialized field) | Authoritative winner identity; non-empty signals "results ready" |
| `gameData.CrystalTargetCount` | Server (via `_netCrystalCollisions.OnValueChanged`) | `int` (non-serialized field) | Crystal target readable by any system |

Note: `MultiplayerCrystalCaptureController` declares **no NetworkVariables**. All network sync is via ClientRpc arrays.

## Stats & Telemetry

**UGS Stats Reporting** (winner only, via `CrystalCaptureStatsReporter`):

```csharp
ugsStatsManager.ReportCrystalCaptureStats(
    gameMode,
    gameData.SelectedIntensity.Value,
    (int)localStats.Score  // finish time (seconds) — winners only
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
| End-game sequencer | `EndGameSequencer.cs` (shared) | `_Scripts/Utility/DataContainers/` |
| In-game HUD | `MultiplayerCrystalCaptureHUD.cs` | `_Scripts/UI/` |
| Scoreboard | `MultiplayerCrystalCaptureScoreboard.cs` | `_Scripts/UI/` |
| End-game scoreboard (shared base) | `Scoreboard.cs` | `_Scripts/UI/` |
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

1. **No dedicated environment generation**: Unlike HexRace's deterministic track with seed sync, Crystal Capture uses a scene-placed environment. No seed NetworkVariable is needed — but the scene-placed spawnables must themselves be **deterministic from their authored seed**, because every client builds the environment locally. The scene's `SegmentSpawner` maps intensity → structure: `[CliffordTorus, ConcentricSpheres, Helicoid, Atlantis]`. Intensity 4 is `SpawnableAtlantis` (`_Scripts/Controller/Environment/MiniGameObjects/SpawnableAtlantis.cs`) — the ~69k-prism organic garden-city (world-tree, terraces, coral reef mounds, kelp, Möbius causeway, floating atolls, curl-field currents, dune floor) that replaced the gyroid lattice; the gyroid remains Joust's intensity-4 structure. It streams in via `PrismTrailBuilder.LayBudgetedAsync` behind the arena-ready gate and is fully deterministic from the prefab's serialized seed.

2. **Domain-aggregated turn end**: The scene wires `NetworkCrystalCollisionTurnMonitor` with `CrystalCollisions` set to the per-domain target. The turn ends as soon as `gameData.ScoringRule.IsObjectiveReached(gameData, out _)` returns true — i.e., when any active domain's summed CrystalsCollected reaches the target. To swap the trigger (e.g., back to a timer), replace the monitor in the scene.

3. **Score = finish time / loser sentinel (per-player)**: winning-domain players carry the match time, losers the team-remaining sentinel; the scoreboard's secondary stat reads `CrystalsCollected` directly for individual contribution. The winner banner and end-game attribution use the domain aggregate via `WinnerDomain`. NOTE: UGS `CrystalCaptureStats.HighScores` values recorded before this change were crystal counts and shadow real times until cleared server-side (`UGSStatsManager.GetEvaluatedHighScore`).

4. **HasEndGame=false + SetupNewRound suppression**: Crystal Capture handles end-game through `OnTurnEndedCustom()` → `SyncFinalResults_ClientRpc (shared MultiplayerDomainGamesController tail)()`, which calls `InvokeWinnerCalculated()` + `InvokeMiniGameEnd()`. Setting `HasEndGame=false` prevents the base controller's `SyncGameEnd_ClientRpc` from duplicating these calls. Since `HasEndGame=false` causes `ExecuteServerRoundEnd` to call `SetupNewRound()` instead of `ExecuteServerGameEnd()`, Crystal Capture also overrides `SetupNewRound()` to return immediately when `_finalResultsSent=true`.

5. **UseSceneReloadForReplay=true**: Same as HexRace — full network scene reload for clean state. The in-place reset path (`OnResetForReplayCustom`) is retained as a fallback but not used by default.

6. **No comeback system**: Unlike HexRace (which uses `ElementalComebackSystem`), Crystal Capture has no handicap or catch-up mechanics. It is a straightforward competitive race.

7. **HUD refreshes on turn start**: `MultiplayerCrystalCaptureHUD` inherits the base `MultiplayerHUD` refresh on `OnMiniGameTurnStarted` — domain panels (or legacy per-player cards) are initialized from current `RoundStatsList` values, important for replay resets.

8. **Solo play supported**: `MinPlayersAllowed=1` allows launching Crystal Capture without a party. AI backfill provides opponents via `ServerPlayerVesselInitializerWithAI`.

9. **Scoreboard stays on-base across re-shows** (commit `660e4d91`): the shared `Scoreboard.PlayEntranceAnimation` slid the panel in by mutating its own `anchoredPosition` (reading the current pos as the rest target, shoving it down by `offset`, tweening back via `DOAnchorPos`) and never restored it on `HideScoreboard()`. On a stretch-anchored panel a re-entrant/interrupted show captured the already-displaced position as the new rest target, so the board drifted off-base in modes that re-show it — Crystal Capture and Joust. HexRace was immune (`HasEndGame=false` → shown once then a full scene reload). The slide is now disabled: `ShowScoreboard` calls `ShowScoreboardImmediate()` (authored position, forces full CanvasGroup alpha + unit banner scale). Re-enable the slide once the rest position is captured at `Awake` and restored on hide.

10. **Crystal placement volume is one volume, initial and respawn alike**: Crystal Capture authors **no** anchors — the scene's `NetworkCrystalManager` has `listOfCrystalPositions: []` — and the two spawn paths used to disagree about what that meant. The initial batch fell back to a placeholder anchor (`Vector3.forward * 30`) jittered by `Random.onUnitSphere * 35f`, so every crystal started on a 35-unit **shell** offset from the cell centre; every respawn took a different fallback (`Random.insideUnitSphere * crystal.SphereRadius + cellCentre`), a solid **ball of radius 170** centred on the cell. Placement therefore visibly opened up over the course of a match (~4.9x the radius, and a different centre). Both paths now call the single `CrystalManager.GetAnchorlessSpawnPoint()`, and both honour the min-distance retry via `PickSpawnPointAwayFromLast`. That one volume is measured off the **cell nucleus** — `Cell.NucleusWorldRadius`, the renderer-bounds radius the node-control zone itself tests against (**196u** in this mode since the Scurry Cell landed — see note 12) — so crystals fill the cell core at whatever scale *that intensity's* config spawns it (an `IntensityWise` cell picks a different config, hence a different nucleus, per level). `CrystalManager.GetAnchorlessSpawnRadius()` resolves nucleus → `noNucleusSpawnRadius` → crystal `SphereRadius`; the NUCLEUS ALWAYS WINS and no per-scene field can override it (that coupling is platform-wide and locked - a mode that wants a different crystal volume resizes its nucleus), and nudges `Cell.EnsureInitialized()` so placement never depends on whether `OnInitializeGame` beat the first crystal spawn. `noNucleusSpawnRadius` on the scene's `NetworkCrystalManager` is the fallback for a cell with NO nucleus only; `anchorJitterRadius` (35) is the anchored-mode shell and is unchanged for HexRace/Joust, which do author anchors. Do not re-introduce a second fallback.

11. **Player spawns are computed from the cell, not authored transforms**: the scene's four spawn transforms sat at (±50, 0, ±50) — a radius of ~71, i.e. *inside* the ~200-unit nucleus, on one plane, at fixed world positions that suit exactly one cell size. `ServerPlayerVesselInitializer.arrangeSpawnPointsAroundCell` (on for this scene) replaces them with a ring built by `CellSpawnFormation.Build`: players sit on a sphere of `Cell.NucleusWorldRadius + spawnDistanceOutsideNucleus` (40) around the cell centre — **236u** with the Scurry Cell (note 12), each rotated to face the centre, arranged by **total player count** (`SelectedPlayerCount`, humans + AI backfill) — 4 tetrahedral, 3 an equilateral great-circle triangle, 2 antipodal on one axis through the centre, 1 on +Z, 5+ a Fibonacci sphere. Same nucleus reference the crystals use, so both scale with the cell. The ring is built **once** per scene, on the first vessel spawn (the nucleus must exist first) and before `SpawnAIs` in the AI subclass, because `GameDataSO` pops spawn poses from a draw pool — rebuilding mid-spawn would hand two players the same pose. Math is pure and unit-tested (`CellSpawnFormationTests`).

12. **The mode has its own cell: `Scurry Cell Config`, whose nucleus is half-scale.** Crystal Capture used to share `Barren Cell Config` with five other scenes, so its core size was not its own to tune. It now has `Assets/_SO_Assets/Cell Configs/Scurry Cell/Scurry Cell Config.asset` — a clone of Barren differing in exactly one reference: `NucleusPrefab` → `Assets/_Prefabs/Environment/HalfNucleus.prefab` (`localScale 200`, a flat copy of `Nucleus.prefab` following the `BigNucleus` / `BrightNucleus` convention in that folder). It deliberately keeps Barren's `SpawnProfile` — the ecology is unchanged, only the core size is.

    Sizes, all derived from `Node2.fbx`'s ~0.97977875u mesh radius (which is also where the `NucleusR = 392f` constant in `SpawnableCaldera`/`SpawnableOurobor` comes from):

    | | Barren (400) | Scurry (200) |
    |---|---|---|
    | `Cell.NucleusWorldRadius` | 391.91 | **195.96** |
    | node-control zone volume | 2.52e8 | **3.15e7** (×⅛) |
    | crystal spawn ball | 391.91 | **195.96** (8× crystal density) |
    | player spawn ring | 431.91 | **235.96** |
    | membrane (`CapsuleMembrane`) | 1200 | 1200 (unchanged) |

    Ecology impact, stated per the `/ecology` protocol: **no locked invariant is touched** — node control still reads per-domain environment volume inside the nucleus (`Cell.DominantDomain`), the herbivore diet is still spatialized on `IsInsideNucleus` (interior sanctuary / exterior feeding ground), `HasNucleusControlZone` stays true, and a centred sphere stays domain-neutral. It is a size tune, not a semantics change. The territorial claim and the fauna sanctuary both shrink to ⅛ volume, and the ⅞ of the old interior that is now exterior becomes voraciously grazeable. **Collider budget: zero delta** (`Nucleus.prefab` carries no collider) — but note the *density-grid* side: `Cell.AddBlock` registers a prism in the fauna-targeting grids only when it is OUTSIDE the nucleus, so mass in the freed shell now takes up to four `BlockCountDensityGrid.AddBlock` calls it previously skipped. Verify in-editor with **FrogletTools > Ecology > Measure Cell Environment Baselines**; Barren authors no `EnvironmentPrefab`, so the baseline is trail mass only and `PhaseThresholds` stay at `Default`.

13. **No scene-placed duplicate of a Cell-owned visual**: the scene carried a `Nucleus.prefab` instance at world origin, scale 400 — the exact prefab, position and scale the Cell instantiates itself from `NucleusPrefab` in `Cell.SpawnVisuals` — so two coincident nuclei rendered, and the scene copy was invisible to every piece of nucleus bookkeeping (`RefreshNucleusControlRadius` reads the Cell's private `nucleus` field, assigned only by that `Instantiate`). Removed here and in the two other scenes that carried it (Cellular Duel, Freestyle MP). The `SkyboxModel` object in this scene is **not** the same class of thing and stays: it is `MembraneBase`/`BigMembraneVariant` (`SkyboxModel.fbx` at scale 1600), while the config's `MembranePrefab` is `CapsuleMembrane` — a different asset, procedural, radius 1200. It is the scene's backdrop, not a duplicate membrane.

14. **Dead `Cell` overrides swept**: alongside the duplicate nucleus, this scene's Cell instance carried unresolvable prefab-instance modifications naming fields the script has not had for a long time — `CellTypes.Array.data[0]` (pointing at a guid no asset carries), `fauna1`/`fauna2`/`flora1`/`flora2`, `randomSpawnProfile`, `Crystal`. Unity never prunes an unresolvable modification until the object is touched and re-saved, and there is no `[FormerlySerializedAs]` remapping any of them, so they were inert at runtime but read as real wiring. 72 such overrides were removed across 12 scenes and `RacingCellVariant.prefab`. **FrogletTools > Ecology > Audit Cell-Owned Visuals** now reports both this and the scene-placed-duplicate class, so neither can quietly return.

15. **The spawn ring raced `Cell.Initialize` and silently landed players INSIDE the nucleus** (fixed). `CellRuntimeDataSO.Cell` is assigned only inside `Cell.Initialize` (`Cell.cs`), which runs on `OnInitializeGame` behind `MultiplayerMiniGameControllerBase.InitDelayMs` = **1000 ms** — but `EnsureSpawnPosesReady` runs at `preSpawnDelayMs` = **200 ms**, and the AI subclass calls it at `OnNetworkSpawn` (t≈0). So `cellData.Cell` was null, the method took its authored-transform fallback — the old points at (±50, 0, ±50), radius **70.7u**, well inside the 196u nucleus — and latched it for the rest of the scene. Reading `NucleusWorldRadius` that early is the same trap by another route: the nucleus GameObject does not exist yet, the property returns 0, and `0 + spawnDistanceOutsideNucleus` puts everyone 40u from the cell **centre**.

    Fixed on three fronts, none of which depends on timing: `Cell.FindByRuntimeData(runtime)` resolves through the static `ActiveCells` registry the Cell joins in `OnEnable` (immediately available); `Cell.ExpectedNucleusWorldRadius` measures the config's `NucleusPrefab` asset directly (`MeasurePrefabRadius` — mesh bounds × authored scale × `nucleusScaleMultiplier`, the asset-time counterpart of `RefreshNucleusControlRadius`'s `Renderer.bounds` read, and equal to it: 400 → 391.911, 200 → 195.956); and `EnsureSpawnPosesReady` now **latches only on success**, distinguishing a transient miss (retry on the next spawn) from a cell that genuinely has no nucleus configured (authored points are then the only answer). It also logs the ring it built, so the radius is visible in the console rather than inferred from where you woke up. `CrystalManager.GetAnchorlessSpawnRadius` was carrying the same exposure and now takes the same path.

    The retired `Cell.EnsureInitialized()` nudge was the wrong shape for this: forcing `Initialize` early would have picked an `IntensityWise` config before intensity syncs, and — until `SpawnVisuals` was guarded per field in the same pass — a second `Initialize` duplicated the membrane and nucleus, orphaning the first pair.

### Follow-ups (open, deliberately not done in the nucleus/spawn branch)

- **Spawn-ring rollout is a per-mode design call.** `arrangeSpawnPointsAroundCell` is on for
  this scene only. It is wrong for HexRace (deterministic track, its own start line) and
  arguable for Joust / Cellular Duel / 2v2 CoOp, whose authored starts may be intentional.
  Decide per mode, then flip the bool + wire `cellData` in that scene.
- **The `SkyboxModel` backdrop is legacy, but not this branch's call.** `MembraneBase` /
  `BigMembraneVariant` instances sit in seven scenes. They are NOT Cell duplicates (note 13),
  and in both Recording Studio tool scenes — which wire no `CellConfigs` and have
  `m_SkyboxMaterial: {fileID: 0}` — the scene copy is the only geometry that renders.
  Retiring them in favour of the procedural HyperSea skybox is an art decision across all
  seven; do not do it piecemeal.
