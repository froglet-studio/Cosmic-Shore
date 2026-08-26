# Joust Game Mode — Technical Documentation

## Overview

Joust is a collision-based competitive mode for 2-12 players. Players score joust points by overtaking **opponents** — sweeping their faster vessel's skimmer past a slower enemy. Overtaking a same-domain teammate scores nothing (it buffs the teammate instead). The first domain to reach the collision target wins; losers are ranked by jousts remaining. The mode supports multiplayer with friends, mixed human+AI lobbies, or solo play with AI backfill (minimum **2 players and 2 domains** required, so there is always an opposing team to joust).

**Key architectural facts:**

- **Single scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity` — no separate singleplayer scene
- **Single GameMode enum**: `GameModes.MultiplayerJoust = 34`
- **Always Netcode**: `MultiplayerJoustController` extends the multiplayer controller hierarchy. Even solo play runs through Netcode
- **Server-authoritative**: Collision sync, winner determination, and final score sync are all server-owned
- **Golf scoring**: Lower score = better rank. Winner's score = race time (seconds); losers' score = 99999f
- **Scene reload for replay**: `UseSceneReloadForReplay = true` (matches HexRace / Crystal Capture)

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
- **Domain (Team) Count** (2-4): Floored at `SO_ArcadeGame.MinDomainsAllowed` (**2** for Joust). The modal computes the default domain count **after** player count is set (commit `22900f8b`), so the stepper defaults to 2 — never 1, which would put every player on one team and leave the AI with no opponent to joust. See Design Note 8.
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
├─ Vessel-skimmer collision occurs (physics observes it PER MACHINE):
│   └─ VesselExplosionBySkimmerEffectSO.Execute()  [wherever locally observed]
│       ├─ Speed check: skimmer vessel must be faster than impacting vessel
│       ├─ Domain check: skimmer vessel and impacting vessel must be on
│       │   different domains — overtaking a teammate scores nothing
│       ├─ OWNER GATE: only the machine that owns the impactee (scoring)
│       │   vessel proceeds — everyone else observed a replica overlap
│       ├─ Anti-spam cooldown check (0.15s per impactee, owner-side)
│       └─ NetworkVesselImpactor.ReportJoust(impactorNetImpactor)
│           └─ ExecuteJoust_ServerRpc → ExecuteJoust_ClientRpc  [ALL machines]
│               └─ VesselImpactor.ExecuteJoustImpact(impactorVesselImpactor)
│                   └─ VesselExplosionBySkimmerEffectSO.ExecuteConfirmed()
│                       ├─ Create AOE explosion visual
│                       ├─ OnJoustCollision.Raise(impacteeVessel.PlayerName)
│                       │   └─ [Server only via StatsManager._allowRecord]
│                       │       RoundStats.JoustCollisions++  (scorer's stats)
│                       │       └─ OnJoustCollisionChanged event fires
│                       │           └─ NetworkJoustCollisionTurnMonitor.OnCollisionChanged
│                       │               └─ [Server] SyncCollision_ClientRpc(name, count)
│                       │                   └─ [Clients only] update local stats
│                       ├─ JoustScored / JoustReceived SFX (per-machine local user)
│                       └─ GameToastAPI.PostJoust(hitPlayer, hitDomain, hitterPlayer, hitterDomain)
│
└─ TurnMonitor.Update() — every frame
    └─ NetworkJoustCollisionTurnMonitor.CheckForEndOfTurn()  [server only]
        └─ return gameData.ScoringRule.IsObjectiveReached(gameData, out _)   // SumByDomain(Jousts) ≥ target
            └─ If true → OnTurnEnded() → gameData.InvokeGameTurnConditionsMet()
```
The end condition is **domain-aggregated**: the turn ends as soon as any active domain's summed JoustCollisions reaches the target. Teammates (humans + AI on the same Domain) hit the joust target together.

### 7. Winner Determination & Score Sync

Winner detection is **server-authoritative** via `OnTurnEndedCustom()`:

> **Source of truth:** the end condition, winning domain, per-player score, and ranked
> results are produced by `JoustScoringRuleSO` (`IsObjectiveReached` / `AssignScores` /
> `BuildResults`) via `gameData.ScoringRule`; the turn monitor + controller delegate to it.
> (The old `gameData.TryGetDomainReachingJoustTarget` / `SumJoustCollisionsByDomain` helpers
> were retired — use `ScoringMetrics.SumByDomain(gameData, Jousts, domain)`.)

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
│   │           │   ├─ Winning DOMAIN = active domain with the highest ScoringMetrics.SumByDomain(gameData, Jousts, …)
│   │           │   ├─ Representative WinnerName = best individual contributor on winning domain
│   │           │   ├─ Every player on the winning domain: stats.Score = currentTime
│   │           │   └─ All other players: stats.Score = 99999f
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

**Scoring Rules (domain-aggregated):**

| Player | Score Formula | Example |
|---|---|---|
| Player on the winning **domain** (the domain whose summed JoustCollisions reached the target first) | Elapsed time in seconds | `32.5` |
| Player on a losing domain | `99999f` | `99999` |

Golf rules (`UseGolfRules = true`): Lower score = higher rank. Every teammate on the winning domain shares the same elapsed-time score (the joust scoreboard's tie-break — `JoustCollisions` descending — orders the finisher above the assist). All losers are tied at 99999.

### 8. End Game (Scoreboard)

There is no end-game cinematic. When the game ends, `EndGameSequencer` halts the vessels, plays the GameEnd SFX, and raises `OnShowGameEndScreen` — the signal the **`Scoreboard`** (and `LifeForm` ecology cleanup) already listen for. The `Scoreboard` is the sole end-game UI: a `"{DOMAIN} VICTORY"` banner plus one ranked `PlayerScoreCard` per player. Card order, primary score and the secondary line all come from `JoustScoringRuleSO` (`Results`) — the single scoring source. Winning-domain teammates share the finish time; losers show jousts remaining.

The old animated per-player `VICTORY`/`DEFEAT` reveal belonged to the removed cinematic. `JoustScoringRuleSO.BuildReveal` is retained but currently unconsumed.

### 9. Replay (Play Again)

Joust uses **full network scene reload** for replay (`UseSceneReloadForReplay = true`), matching HexRace and Crystal Capture. Play Again is **host-only**: the Scoreboard hides the button for non-host clients (`ConfigureLobbyButtons`), and both `Scoreboard.OnPlayAgainButtonPressed` and `MultiplayerMiniGameControllerBase.RequestReplay` guard the call path. The host's replay forces every client to follow via the Netcode scene load.

```
Scoreboard.OnPlayAgainButtonPressed()  [host only]
└─ gameController.RequestReplay()  → MultiplayerJoustController (wired in scene)
    └─ ExecuteReplaySequence() → ExecuteSceneReloadReplay()
        ├─ gameData.IsReplayReload = true
        ├─ PrepareForSceneReload_ClientRpc()  — fade to black on all clients
        ├─ Clear NetVesselId on all spawned players
        ├─ Despawn AI players + all vessels (AI spawn with destroyWithScene=false,
        │   so they must be explicitly despawned or SpawnAIs() would duplicate them)
        ├─ gameData.ResetRuntimeData()  — clears rosters, winner, targets, counters
        └─ nm.SceneManager.LoadScene(gameData.SceneName)  — server-authoritative reload
            │
            ▼ fresh scene
            ├─ MultiplayerJoustController.OnNetworkSpawn()  — _finalResultsSent=false
            ├─ ServerPlayerVesselInitializerWithAI — re-spawns AI (RequestedAIBackfillCount
            │   survives ResetRuntimeData), rediscovers persistent human Players
            ├─ Player.PrepareForNewScene()  — RoundStats.Cleanup() zeroes
            │   JoustCollisions / Score for persistent human players
            ├─ InitializeAfterDelay() consumes IsReplayReload → fade from black on
            │   OnClientReady
            └─ Ready button → countdown → fresh game
```

The pause menu's Restart button routes through the same `RequestReplay()` path (`PauseMenu.OnClickReplayButton`, `gameController` wired in scene).

**Scene wiring requirement (this broke Play Again once):** the Joust scene removes the GameCanvas-HexRace prefab's internal `Scoreboard` component and adds its own scene-level `Scoreboard` (with `gameController` → `MultiplayerJoustController`). The prefab's `PlayAgainButton.onClick` persistent call targets the *internal* prefab Scoreboard, so the scene **must override** `m_OnClick.m_PersistentCalls.m_Calls.Array.data[0].m_Target` on that Button to point at the scene-added Scoreboard. With the override left null (or pointing at the removed component), clicking Play Again silently does nothing. HexRace avoids this by keeping the prefab's internal Scoreboard and overriding only its `gameController`.

**Button gating (host-only + anti-spam):** the Scoreboard's `playAgainButton` (PlayAgainButton GO) and `mainMenuButton` (HomeButton GO) fields are wired in all three domain-game scenes so `ConfigureLobbyButtons` can hide both from non-host clients — only the host navigates; clients follow via the Netcode scene load. Once the host commits a navigation (Play Again clicked, or the main-menu SOAP event `Event_OnClickToMainMenuButton` fires from `PauseMenu.OnClickMainMenu`), `Scoreboard.HideHostNavButtons()` hides both buttons so the transition can't be spam-clicked. The `onClickToMainMenu` field must reference the same event asset PauseMenu raises.

## Collision Mechanics

The jousting collision system is triggered by `VesselExplosionBySkimmerEffectSO` (`_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Skimmer Effects/`):

### Collision Chain

1. **Trigger**: A `VesselImpactor` (vessel A) physically collides with a `SkimmerImpactor` (skimmer belonging to vessel B). Physics observes this independently on every machine (replicated transforms), so `Execute` may fire on any subset of machines for one physical pass
2. **Speed check**: Vessel B (the skimmer's owner) must be **faster** than vessel A. If not, the collision is ignored
3. **Domain check**: Vessel B and vessel A must be on **different domains**. Overtaking a same-domain teammate scores no joust point and spawns no explosion — the teammate is buffed instead (see *Buff / Debuff on Overtake* below). If they share a domain, the collision is ignored
4. **Owner gate (networked)**: only the machine that **owns vessel B** (the scoring impactee — host for AI vessels, the client for its own vessel) may confirm the joust. Its own vessel + skimmer are at their true, non-interpolated positions locally, making it the most reliable observer of its own sweep; ownership also gives exactly-once semantics with no cross-machine dedupe
5. **Anti-spam**: A per-impactee cooldown dictionary (`_explosionCooldown = 0.15f`) on the confirming machine prevents rapid-fire collisions from the same vessel pair
6. **Report & broadcast**: `NetworkVesselImpactor.ReportJoust` → `ExecuteJoust_ServerRpc` → `ExecuteJoust_ClientRpc` → `VesselImpactor.ExecuteJoustImpact` → `ExecuteConfirmed` on **every** machine (offline contexts skip the RPC and run `ExecuteConfirmed` directly)
7. **Effect**: An AOE explosion is created at the collision point (all machines)
8. **Joust point**: `OnJoustCollision.Raise(vesselB.PlayerName)` — the **hit vessel** (whose skimmer was impacted) receives the joust point, not the vessel that did the impacting. The raise happens on all machines; only the server's raise records (`StatsManager._allowRecord`), and replication fans the count back out
9. **Game toast**: `GameToastAPI.PostJoust()` posts the Joust situation on every machine, and only for jousts that actually scored. The copy ("A(pts) jousted B(pts)", domain-colored names, live joust points) comes from `GameToastConfig_Joust.asset`, which also authors the idle hint shown after a minute without a joust

**Key insight**: The collision credit goes to the **impactee's vessel** (the one whose skimmer was hit by a faster opponent). This means the faster player — whose skimmer sweeps through the slower player's path — earns the point for the slower player. The design rewards getting jousted while moving fast.

### Buff / Debuff on Overtake

The Squirrel skimmer runs **two** effect SOs on every vessel-skimmer collision (both wired in `SquirrelSkimmerImpactorDataContainer.asset`):

| Effect SO | Role | Domain behavior |
|---|---|---|
| `VesselExplosionBySkimmerEffectSO` | Joust scoring + explosion VFX | Opponent overtake only — same-domain overtakes are skipped entirely |
| `VesselOvertakeBySkimmerEffectSO` | Elemental buff/debuff on the overtaken vessel | Opponent → temporary debuff; same-domain teammate → temporary buff |

The two effects compose: overtaking an **opponent** scores a joust point *and* debuffs them, while overtaking a **teammate** scores nothing but buffs them. Because scoring is opponent-only, a domain's joust count can never be inflated by teammates bumping into each other.

### Network Collision Sync

**Joust confirmation is owner-authoritative** (mirrors the crystal-impact round-trip in `NetworkVesselImpactor`): the impactee's owner validates the collision locally, then `ReportJoust` → `ExecuteJoust_ServerRpc` → `ExecuteJoust_ClientRpc` runs `ExecuteConfirmed` on every machine. The server's `OnJoustCollision` raise is the only one `StatsManager` records (`_allowRecord` is false on clients), so the joust point is always written on the server exactly once.

`NetworkJoustCollisionTurnMonitor` then handles count synchronization outward:

| Source | Path | Purpose |
|---|---|---|
| Server records collision | `SyncCollision_ClientRpc(name, count)` | Broadcast to all clients |
| Client stat echo | `ReportCollision_ServerRpc(name, count)` | Legacy safety net: server accepts only counts **higher** than its current value |

**Server validation**: `ReportCollision_ServerRpc` only accepts reports where the count is **higher** than the server's current value, preventing stale or duplicate updates. (With owner-authoritative confirmation, clients no longer originate counts — this path now only sees rejected echoes of the server's own sync.)

**Anti-recursion guard**: When the server receives `OnCollisionChanged`, it broadcasts via `SyncCollision_ClientRpc` but does **not** re-assign `JoustCollisions` on itself (which would trigger the setter → fire `OnCollisionChanged` again → infinite recursion). The `SyncCollision_ClientRpc` includes `if (IsServer) return` to prevent the host from double-processing.

## HUD & UI Components

| Component | Class | Purpose |
|---|---|---|
| In-game HUD | `MultiplayerJoustHUD` (extends `MultiplayerHUD`) | Per-player joust collision count cards; subscribes to `OnJoustCollisionChanged` |
| Scoreboard | `MultiplayerJoustScoreboard` (extends `Scoreboard`) | End-game ranking; winner shows time `MM:SS:ms`, losers show `"N Joust(s) Left"`; sorts ascending (golf rules) |
| End Game | `EndGameSequencer` (shared) | Halts vessels, plays GameEnd SFX, raises `OnShowGameEndScreen` → the `Scoreboard` shows results. No cinematic. |
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
| Joust confirm RPC carrier | `NetworkVesselImpactor.cs` | `_Scripts/Controller/ImpactEffects/Impactors/` |
| Confirmed-joust dispatch | `VesselImpactor.cs` | `_Scripts/Controller/ImpactEffects/Impactors/` |
| End-game sequencer | `EndGameSequencer.cs` (shared) | `_Scripts/Utility/DataContainers/` |
| In-game HUD | `MultiplayerJoustHUD.cs` | `_Scripts/UI/` |
| Scoreboard | `MultiplayerJoustScoreboard.cs` | `_Scripts/UI/` |
| Stats reporter | `JoustStatsReporter.cs` | `_Scripts/Controller/Arcade/` |
| Arcade game config modal | `ArcadeGameConfigureModal.cs` | `_Scripts/UI/Modals/` |
| Game SO definition | `SO_ArcadeGame.cs` | `_Scripts/ScriptableObjects/` |
| Scene loader | `SceneLoader.cs` | `_Scripts/System/` |
| GameMode enum | `GameModes.cs` | `_Scripts/Data/Enums/` |
| AI vessel spawner | `ServerPlayerVesselInitializerWithAI.cs` | `_Scripts/Controller/Multiplayer/` |
| AI pilot (opponent seek) | `AIPilot.cs` | `_Scripts/Controller/AI/` |
| End-game scoreboard (shared) | `Scoreboard.cs` | `_Scripts/UI/` |
| Game toast API | `GameToastAPI.cs` | `_Scripts/UI/GameToastSystem/` |
| Game scene | `MinigameJoust_Gameplay.unity` | `_Scenes/Multiplayer Scenes/` |

## SO Asset References

| Asset | Type | Key Values |
|---|---|---|
| Joust game config | `SO_ArcadeGame` | `Mode=MultiplayerJoust(34)`, `IsMultiplayer=true`, `MinPlayers=2`, `MaxPlayers=12`, `MinDomainsAllowed=2`, `MinIntensity=1`, `MaxIntensity=4` |
| Arcade config runtime | `ArcadeGameConfigSO` | `Intensity`, `PlayerCount`, `SelectedShip` (runtime state) |

## Design Notes

1. **Collision attribution is counter-intuitive**: The joust point goes to the vessel whose skimmer was hit (the `impactee`), not the vessel that physically collided. The speed check (`impacteeVessel.Speed > impactorVessel.Speed`) ensures only the faster vessel's skimmer-collisions count — essentially rewarding the faster player for "jousting" past a slower opponent.

2. **HasEndGame=false + SetupNewRound suppression**: Joust handles end-game through `OnTurnEndedCustom()` → `SyncJoustResults_ClientRpc()`, which calls `InvokeWinnerCalculated()` + `InvokeMiniGameEnd()`. Setting `HasEndGame=false` prevents the base controller's `SyncGameEnd_ClientRpc` from duplicating these calls. `SetupNewRound()` is overridden to return when `_finalResultsSent=true`.

3. **Scene reload for replay (commit 21d538d3)**: Joust matches HexRace and Crystal Capture with `UseSceneReloadForReplay=true` — Play Again performs a full network scene reload so all per-round state, environment, and AI re-initialize fresh via `OnNetworkSpawn`. The old in-place `OnResetForReplayCustom()` was removed; `_finalResultsSent` / `_winningDomain` reset in `OnNetworkSpawn`, and persistent human players' `JoustCollisions`/`Score` are zeroed by `Player.PrepareForNewScene()` → `RoundStats.Cleanup()`. See §9 for the scene-wiring requirement on the Play Again button.

4. **Infinite recursion fix (commit 3fb2e05)**: The `OnCollisionChanged` handler in `NetworkJoustCollisionTurnMonitor` originally re-assigned `JoustCollisions` on the server side, which triggered the setter → fired `OnCollisionChanged` → infinite recursion. The fix: (1) server path in `OnCollisionChanged` only broadcasts via `SyncCollision_ClientRpc` without re-assigning, (2) `SyncCollision_ClientRpc` includes `if (IsServer) return` to prevent the host from self-updating.

5. **EndGame never triggering fix (commit 6d08fa9)**: Two bugs prevented the end game from working: (1) both the Joust-specific path and the base class path were calling `InvokeMiniGameEnd()` — fixed by adding `HasEndGame=false`, and (2) the `JoustCollisions` setter was gated on `!IsSpawned` (always false in multiplayer), so `OnJoustCollisionChanged` never fired — fixed by making the event always fire.

6. **Anti-spam cooldown**: `VesselExplosionBySkimmerEffectSO` uses a per-impactee cooldown dictionary with `_explosionCooldown = 0.15f` to prevent rapid-fire collisions from the same vessel pair. The dictionary is static (shared across all instances of the SO) and is only consulted on the machine that confirms the joust (the impactee's owner, or the sole machine offline) — the broadcast path needs no cooldown because a single authority already guarantees one confirmation per pass.

7. **Losers all tied**: All non-winners receive a score of `99999f`, so they are all ranked equally. There is no distinction between 2nd and 3rd place in Joust — only the winner matters.

8. **Minimum 2 players AND 2 domains** (commit `22900f8b`): Joust is opponent-based, so a single team makes it unplayable. Two SO fields enforce this on the configure modal:
   - `MinPlayersAllowed=2` — Joust cannot be played solo without at least one AI opponent. The effective minimum player count in the UI stepper is `max(game.MinPlayersAllowed, currentPartyHumanCount)`.
   - `MinDomainsAllowed=2` — added to `SO_ArcadeGame` (default 1, `[Range(1,3)]`) and set to 2 on the Joust asset. `ArcadeGameConfigureModal.MinDomainsForGame` reads it and floors the domain count everywhere it previously floored at the global `MinDomains` const (stepper init, the PC-change re-clamp, the DC-change clamp, and the client-sync `CommitConfiguration` path).

   The bug this fixed: `DefaultDomainCount` was 1 *and* the default was computed in `Configure()` before `PlayerCount` was set (`ResetState()` leaves PC at 0), so Joust opened with one domain — every player on the same team — and the AI had no opponent to chase, so it idled/flew off. The fix also computes the default domain count **after** player count is initialized, and makes `ComputeMaxDomainCount()` fall back to the hard max when PC is unset and never drop below `MinDomainsForGame`.

9. **Comeback runs on jousts, not Score**: `ElementalComebackSystem` is a REQUIRED component of every party game, so Joust has it — auto-created by `MultiplayerMiniGameControllerBase.OnNetworkSpawn` (only HexRace authors one in-scene). It reads `ScoreDifferenceSource.Jousts` (the per-domain sum of `JoustCollisions`), **not** `Score`: `JoustScoringRuleSO.AssignScores` writes `Score` only at game end — winner a finish time, losers `GolfScoreSentinels.JoustLoserScore` — so a Score-sourced deficit would read a flat zero for the whole match. The trailing DOMAIN's pilots gain all four elements equally at `ComebackRatePerScoreDeficit` levels per joust of team deficit (`ArcadeGameMultiplayerJoust.asset` inherits the default `1.0`), capped at level 10 by `ResourceSystem.SustainedCeiling`.

   **This was dead until the comeback wiring fix.** `EnsureExists` assigned `gameData` one line after `AddComponent`, but Unity runs `OnEnable` *synchronously inside* `AddComponent` — so the system logged `GameDataSO is not assigned!`, returned before subscribing to the turn/game events, and never activated. Every auto-created instance was affected, i.e. every mode except HexRace. This doc previously read "No comeback system: Joust has no handicap or catch-up mechanics", which described the bug rather than the design. Set the asset's rate to `0` if Joust should genuinely opt out.

10. **Scoreboard requires inspector wiring**: The scene-added `Scoreboard` (per-mode scoreboard/cinematic subclasses were deleted in the scoring refactor, and the end-game cinematic itself was removed in favour of `EndGameSequencer` + the base `Scoreboard` — see `Docs/ScoringSystem/CHANGELOG.md`) must have `gameController` wired to the scene's `MultiplayerJoustController`, and the prefab `PlayAgainButton.onClick` must be re-targeted at that scene-added Scoreboard (see §9). `PauseMenu`'s `gameController` / `replayButton` overrides must also point at the Joust controller for the pause-menu Restart path.

11. **Opponent-only scoring**: `VesselExplosionBySkimmerEffectSO` checks `impacteeVessel.Domain != impactorVessel.Domain` before scoring. Without this check, two teammates bumping skimmers each scored joust points, inflating the domain sum to the (low) `collisionsNeeded` target almost instantly — the game ended within a few collisions, before the in-game HUD was visibly in play. Overtake buffs/debuffs are unaffected: `VesselOvertakeBySkimmerEffectSO` still buffs teammates and debuffs opponents regardless. AI pilots already chase opponents only (`AIPilot.SelectClosestOpponent` skips `player.Domain == myDomain`), which complements this rule.

12. **AI keeps jousting when it loses its target** (commit `975271aa`): the player-seek AI (`AIPilot` with `seekPlayers=true`, used for Joust) used to fly off in a straight line whenever its opponent set went empty (e.g. a 1v1 opponent mid-respawn). `UpdatePlayerTarget` only wrote `_targetPosition` when it found a live different-domain opponent; with no fallback the target held its stale/zero value and the vessel coasted along its forward axis. Three changes fix it:
    - The targeting coroutine was split — `UpdatePlayerTarget` now only RE-SELECTS *which* opponent to chase (via `SelectClosestOpponent`), storing the chosen `Transform` in `_targetVesselTransform`; `Update()` reads that opponent's **live** position every frame, so the AI never chases a 0.5s-stale point.
    - `SelectClosestOpponent` falls back to the **cell centre** when no opponent qualifies (mirroring the crystal-seek fallback in `UpdateCellContent`), instead of holding a stale/zero target. While no opponent is locked it re-scans on the faster `playerReacquireInterval` (0.1s) rather than the locked-on `playerSeekUpdateInterval` (0.5s), so a respawning 1v1 target is re-acquired promptly.
    - The div-by-zero early-return (target coincident with the vessel) now zeroes the turn inputs before returning, so the AI flies a clean straight pass-through instead of latching the previous frame's turn input and veering.

13. **Scoreboard stays on-base across re-shows** (commit `660e4d91`): the shared `Scoreboard.PlayEntranceAnimation` slid the panel in by mutating its own `anchoredPosition` (reading the current pos as the rest target, shoving it down by `offset`, tweening back via `DOAnchorPos`) and never restored it on `HideScoreboard()`. On Joust's stretch-anchored panel a re-entrant/interrupted show captured the already-displaced position as the new rest target, so the board drifted off-base in modes that re-show it (Joust / Crystal Capture). HexRace was immune (`HasEndGame=false` → shown once then a full scene reload). The slide is now disabled: `ShowScoreboard` calls `ShowScoreboardImmediate()` (authored position, forces full CanvasGroup alpha + unit banner scale). Re-enable the slide once the rest position is captured at `Awake` and restored on hide.

14. **Owner-authoritative joust confirmation** (`Docs/ScoringSystem/BUGS.md` B16): joust physics is observed independently per machine on interpolation-delayed replicas, while `StatsManager` records only on the server — so a joust that only the jouster's own machine observed used to show a toast + explosion locally yet never score anywhere (the turn monitor's "client reports up" branch was unreachable: nothing ever wrote `JoustCollisions` on a client except the server's own sync). Now only the impactee's **owner** confirms a validated joust and it is broadcast through `NetworkVesselImpactor.ReportJoust → ExecuteJoust_ServerRpc → ExecuteJoust_ClientRpc`, with all feedback (explosion, SFX, toast, scoring raise) running in `ExecuteConfirmed` on every machine. A toast therefore appears **iff** the joust was recorded. The owner is the best observer of its own skimmer sweep (true local positions); AI vessels are host-owned so solo play confirms on the host exactly as before. `VesselOvertakeBySkimmerEffectSO` (buff/debuff) still runs per-machine local detection — transient feel only, no score impact.
