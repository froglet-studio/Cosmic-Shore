# HexRace - Condensed Reference

> Extracted verbatim from `CLAUDE.md` (2026-07-23) so the root file stays a lean
> rules-and-routing dictionary. This is the canonical home of this content now -
> update it here, and keep the corresponding CLAUDE.md digest in sync.
> Canonical deep-dive: `Assets/_Scripts/Controller/Arcade/HEXRACE.md`.

### HexRace Game Mode

HexRace is a competitive crystal-collection racing mode (1-4 players) using a **single unified scene** (`MinigameHexRace.unity`). There is no separate singleplayer scene — all games run through Netcode regardless of player count. Solo play uses AI backfill via `ServerPlayerVesselInitializerWithAI`. See `Assets/_Scripts/Controller/Arcade/HEXRACE.md` for the full technical reference.

#### Architecture

```
MiniGameControllerBase (MonoBehaviour + NetworkBehaviour)
  └── MultiplayerMiniGameControllerBase
      └── MultiplayerDomainGamesController
          └── HexRaceController
```

**SO config**: `SO_ArcadeGame` asset — `Mode=HexRace(33)`, `IsMultiplayer=true`, `MinPlayers=1`, `MaxPlayers=4`, `GolfScoring=true`

#### Execution Flow

```
ArcadeGameConfigureModal.OnStartGameClicked()
  ├─ SyncAllGameDataForLaunch():
  │   ├─ gameData.SceneName = "MinigameHexRace"
  │   ├─ gameData.GameMode = GameModes.HexRace
  │   ├─ gameData.SelectedPlayerCount = humanCount
  │   └─ gameData.RequestedAIBackfillCount = max(0, config.PlayerCount - humanCount)
  └─ gameData.InvokeGameLaunch() → OnLaunchGame SOAP event
      └─ SceneLoader.LaunchGame()
          ├─ AppState → LoadingGame
          ├─ Network scene load (host always active from Menu_Main)
          └─ Game config synced to clients by MultiplayerMiniGameControllerBase.OnNetworkSpawn()
```

#### Player Count & AI Backfill

| Humans in Party | Selected Players | AI Backfill | Total |
|---|---|---|---|
| 1 (solo) | 1 | 0 | 1 |
| 1 (solo) | 2 | 1 | 2 |
| 1 (solo) | 4 | 3 | 4 |
| 2 (party) | 2 | 0 | 2 |
| 2 (party) | 4 | 2 | 4 |
| 3 (party) | 3 | 0 | 3 |

#### Track Spawning

Server generates a random seed (after 1500ms delay for intensity sync) → writes to `_netTrackSeed` NetworkVariable → all clients spawn identical track via `SegmentSpawner.Initialize()`. Clients receive the seed through three redundant paths: immediate read at spawn, `OnValueChanged` callback, or poll fallback (100ms × 50 attempts). `HexRaceController` sets `segmentSpawner.ExternalResetControl = true` to own the track lifecycle.

| Parameter | Formula | Base |
|---|---|---|
| Segments | `base * Intensity` | 10 |
| Straight Line Length | `base / Intensity` | 400 |
| Helix Radius | `Intensity / 1.3` | — |

#### Race Rules

- **Crystal target**: Resolved by `NetworkCrystalCollisionTurnMonitor.GetCrystalCollisionCount()`: inspector `CrystalCollisions` field (if non-zero) > `SpawnableWaypointTrack` waypoints > default 39. Synced to all clients via `NetworkCrystalCollisionTurnMonitor._netCrystalCollisions` NetworkVariable → `gameData.CrystalTargetCount`
- **Turn monitor (domain-aggregated)**: `NetworkCrystalCollisionTurnMonitor` calls `gameData.ScoringRule.IsObjectiveReached(gameData, out _)` every frame (server only) — the turn ends when any active domain's summed CrystalsCollected (`ScoringMetrics.SumByDomain`) reaches the target, so AI and human teammates finish the race together
- **Winner detection (domain-aggregated)**: Server-authoritative via `HexRaceController.OnTurnEndedCustom()` — finds the first active domain whose summed crystals reach the target (Jade → Ruby → Gold tie-break), sets `_raceEnded=true`, picks the best individual contributor on that domain as the representative `WinnerName`, calculates all scores, broadcasts via `SyncFinalScores_ClientRpc`
- **Scoring**: Every player on the winning domain gets `Score = finishTime` (seconds). Losing-domain players get `Score = 10000 + domainCrystalsRemaining` — the penalty reflects the team's deficit, so teammates on the same losing domain tie on Score. Golf rules (`UseGolfRules=true`): lower = better
- **Score sync**: `SyncFinalScores_ClientRpc()` broadcasts all player scores + winner name to all clients, then calls `InvokeWinnerCalculated()` + `InvokeMiniGameEnd()`
- **HasEndGame=false**: Prevents base controller from calling `SyncGameEnd_ClientRpc` (which would duplicate `InvokeMiniGameEnd`). `SetupNewRound()` is overridden to return when `_raceEnded=true`, suppressing the Ready button
- **Comeback**: `ElementalComebackSystem` reads `gameData.SumCrystalsCollectedByDomain` for the leader and the player's own domain — buffs are sized to the **team** deficit, so players on the leading domain don't get a buff even when they personally trail their teammates

#### End Game

- `HexRaceEndGameController` reads `gameData.WinnerName` (set by server via `SyncFinalScores_ClientRpc`)
- Winner sees "VICTORY" + race time (formatted mm:ss:cs); losers see "DEFEAT" + crystals remaining
- `HexRaceScoreboard` displays all players ranked by score (golf rules — sorts ascending)
- **Replay**: Full network scene reload (`UseSceneReloadForReplay=true`). `OnResetForReplayCustom()` was removed — all race state, track, and environment are destroyed with the scene and re-initialized fresh via `OnNetworkSpawn`. Fade to black → scene reload → fade from black on `OnClientReady`

#### Shared State & NetworkVariables

| Variable | Owner | Purpose |
|---|---|---|
| `HexRaceController._netTrackSeed` | Server | Deterministic track seed (NetworkVariable) |
| `NetworkCrystalCollisionTurnMonitor._netCrystalCollisions` | Server | Crystal target synced to clients (NetworkVariable); writes to `gameData.CrystalTargetCount` |
| `gameData.WinnerName` | Server (via ClientRpc) | Authoritative winner identity; non-empty = results ready |
| `gameData.CrystalTargetCount` | Server (via `_netCrystalCollisions.OnValueChanged`) | Crystal target readable by any system |

#### Key Files — HexRace

| Role | File | Location |
|---|---|---|
| Game controller | `HexRaceController.cs` | `_Scripts/Controller/Arcade/` |
| Domain games base | `MultiplayerDomainGamesController.cs` | `_Scripts/Controller/Arcade/` |
| Score tracker | `HexRaceScoreTracker.cs` | `_Scripts/Controller/Arcade/` |
| Crystal turn monitor | `NetworkCrystalCollisionTurnMonitor.cs` | `_Scripts/Controller/Arcade/TurnMonitors/` |
| Track spawner | `SegmentSpawner.cs` | `_Scripts/Controller/Environment/MiniGameObjects/` |
| End game controller | `HexRaceEndGameController.cs` | `_Scripts/Utility/DataContainers/` |
| In-game HUD | `HexRaceHUD.cs` | `_Scripts/UI/` |
| Scoreboard | `HexRaceScoreboard.cs` | `_Scripts/UI/` |
| Elemental comeback | `ElementalComebackSystem.cs` | `_Scripts/Controller/Arcade/` |
| Stats provider | `HexRaceStatsProvider.cs` | `_Scripts/Controller/Arcade/` |
| Player stats profile | `HexRacePlayerStatsProfile.cs` | `_Scripts/UI/` |
| Full documentation | `HEXRACE.md` | `_Scripts/Controller/Arcade/` |

#### HexRace Patterns to Follow

- **Server authority via OnTurnEndedCustom**: Winner detection runs on the server in `OnTurnEndedCustom()`. `HexRaceScoreTracker` only handles local elapsed-time tracking and UGS stats reporting — it does not participate in winner determination.
- **Deterministic track**: All clients spawn identical tracks from shared seed + intensity. `SegmentSpawner` uses `Random.InitState(seed)`. Three redundant sync paths (immediate, OnValueChanged, poll fallback) ensure reliability.
- **Golf scoring**: `UseGolfRules = true` — lower score = better rank. Winner time (seconds) always ranks above loser penalty (10000+).
- **Scene reload for replay**: Use `UseSceneReloadForReplay = true` — do not implement in-place reset. Flora/fauna/environment don't fully reset in-place.
- **Comeback system**: Use `ElementalComebackSystem` with `ScoreDifferenceSource.CrystalsCollected` for HexRace (not Score, since Score tracks elapsed time equally for all). Leader and player values are read as domain aggregates via `GameDataSO.SumCrystalsCollectedByDomain`, so comeback buffs scale with the **team** deficit.
- **Single scene**: Do not create separate singleplayer/multiplayer scenes. AI backfill handles solo play within the same Netcode pipeline.
- **Crystal target sync**: Server writes target to `NetworkCrystalCollisionTurnMonitor._netCrystalCollisions` NetworkVariable, which syncs to `gameData.CrystalTargetCount` on all clients.
- **Domain-aggregated scoring**: HexRace, Joust, and Crystal Capture all end on a **per-domain** sum via the mode's `ScoringRuleSO.IsObjectiveReached` (over `ScoringMetrics.SumByDomain`). At most three scores ever exist (Jade / Ruby / Gold); teammates contribute to the same domain total. The in-game `MultiplayerHUD` shows the local player's domain panel to the left of the centered player score and 1-2 opposing-domain panels to the right when its `MultiplayerHUDView` has the `allyDomainContainer` / `opposingDomainsContainer` / `domainPanelPrefab` wiring; otherwise it falls back to the legacy per-player layout.
