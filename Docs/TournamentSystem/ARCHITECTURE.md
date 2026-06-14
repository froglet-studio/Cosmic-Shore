# Tournament System — Architecture

Canonical reference for **Tournament Mode** — the session-level meta (P3 / R2) that strings the
three feature-complete domain minigames into one tournament with a per-player leaderboard.

> **Status:** core engine implemented (code + data assets). The Unity-editor wiring (scene
> content, prefab buttons, inspector references) is listed under **§7 Editor follow-up** and is
> required before the mode runs end-to-end.

---

## 1. What it is

One tournament plays a **fixed lineup** — **Skim Race (HexRace 33) → Joust (34) → Crystal
Capture (35)** — back-to-back. After each game, players earn **placement points** (1st = 10,
2nd = 6, 3rd = 3, 4th = 1; configurable). The cumulative per-player total is the tournament
leaderboard. It appears as a normal card in the Arcade panel (`GameModes.Tournament = 36`).

## 2. The load model — sequential `Single`, no additive

Every transition is a **host-driven `LoadSceneMode.Single` load** via the existing
`SceneLoader`/`NetworkManager.SceneManager`. The NetworkManager / UGS session / Relay and the
`Player` NetworkObjects already **persist across Single loads** (eager-Relay locked design), so
the tournament rides that proven path. **There is no additive scene loading** — it would collide
with per-scene systems (duplicate `ServerPlayerVesselInitializer`, ambiguous
`Scoreboard.gameController`, shared `gameData`/`CameraManager` singletons).

```
Menu_Main → [Arcade card → ArcadeGameConfigureModal ready-up]
  → Tournament (lobby)  → HexRace → Joust → Crystal Capture (each a Single load)
                              └─ Scoreboard.Continue (host) advances; party follows
  → final Scoreboard: Play Again (restart tournament) | Main Menu → Menu_Main
```

## 3. The brain — `TournamentController` (persistent, network-free)

`TournamentController` (`_Scripts/Controller/Arcade/Tournament/`) is a **pure-C# DI singleton**
created eagerly by `AppManager` (so it is alive from bootstrap and survives every Single load).
A static `Instance` lets scene MonoBehaviours reach it (mirrors `PartyInviteController.Instance`).

- **Standings are network-free.** On `gameData.OnMiniGameEnd`, **every peer** folds the
  already-synced `gameData.Results` (the ranked per-player `List<ScoreResult>`) into
  `TournamentDataSO` via `RecordResults` — identical inputs → identical standings, no extra RPC.
  Recording happens *before* the next load's `ResetRuntimeData` clears `Results`.
- **Only the host drives progression** (`BeginFirstGame` / `AdvanceToNextGame` /
  `RestartTournament`) through `gameData.SyncFromArcadeGame(queue[i]) + InvokeGameLaunch()`; the
  existing `SceneLoader` host-guard makes clients follow.
- **Phase is scene-load-driven** (deterministic on every peer): the **lobby scene** load runs
  `StartTournament` (reset standings, `IsActive=true`, `IsTournamentMode=true`); each **queued
  game scene** load sets `CurrentGameIndex`; **Menu_Main** load runs `EndTournament` (clears the
  flags). `TournamentStateMachine` tracks `Idle → Lobby → InGame → Complete`.

Per-game stat reset is automatic (`SceneLoader` → `ResetRuntimeData` + each persistent
`Player.PrepareForNewScene` → `RoundStats.Cleanup`). Cumulative points live in `TournamentDataSO`,
outside that reset, so they survive. AI backfill re-runs per scene; the **AI roster is seeded once**
(first game) into `TournamentDataSO.TournamentAINames` and reused, so name-keyed bot standings
attribute across games (AI `Player` objects are destroyed/recreated each scene).

## 4. End-of-game UI — the Scoreboard is the progression surface

The cinematic was removed (`Ys-bleeding-edge` `dbc7c703`); `EndGameSequencer` halts vessels,
plays the SFX, and raises `OnShowGameEndScreen` → `Scoreboard` (sole end-game UI). In tournament
mode `Scoreboard.ConfigureLobbyButtons` swaps the buttons — **all host-only; clients see none**:

| Situation | Host sees | Hidden |
|---|---|---|
| A game ends, more remain | **Continue** → `TournamentController.AdvanceToNextGame()` | Play Again, Main Menu, Leave |
| Final game ends (tournament over) | **Play Again** (→ `RestartTournament()`) + **Main Menu** | Continue, Leave |
| Any game, on a client | — | all |

`IsLastGame` comes from `TournamentController.Instance.IsLastGame` (derived from
`TournamentDataSO.CurrentGameIndex`).

## 5. Data — `TournamentDataSO`

`_Scripts/Utility/DataContainers/Tournament/TournamentDataSO.cs` (asset:
`_SO_Assets/Tournament/TournamentData.asset`). Authored: `GameQueue` (the 3 `SO_ArcadeGame`s),
`PointsByPlace`, `LobbySceneName`, four `ScriptableEventNoParam`s. Runtime (non-serialized):
`IsActive`, `CurrentGameIndex`, `TournamentAINames`, `Standings`. Key methods:
`RecordResults(results)`, `BuildSortedStandings()` (points desc, tiebreak best placement then
name), `IsLastGame`, `ResetRuntime()`.

## 6. File index

| Role | File |
|---|---|
| Mode enum | `_Scripts/Data/Enums/GameModes.cs` (`Tournament = 36`) |
| Config flag | `_Scripts/Utility/DataContainers/GameDataSO.cs` (`IsTournamentMode`) |
| Data container | `_Scripts/Utility/DataContainers/Tournament/TournamentDataSO.cs` |
| State machine | `_Scripts/Controller/Arcade/Tournament/TournamentStateMachine.cs` |
| Controller (brain) | `_Scripts/Controller/Arcade/Tournament/TournamentController.cs` |
| Lobby scene view | `_Scripts/Controller/Arcade/Tournament/TournamentSceneView.cs` |
| End-game buttons | `_Scripts/UI/Scoreboard.cs` |
| Client flag sync | `_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs` |
| Stable AI roster | `_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs` |
| DI registration | `_Scripts/System/AppManager.cs` |
| Card unlock | `_Scripts/System/Progression/GameModeProgressionService.cs` |
| Data asset | `_SO_Assets/Tournament/TournamentData.asset` (+ 4 `Event_Tournament*.asset`) |
| Arcade card | `_SO_Assets/Games/ArcadeGameTournament.asset` (in `GameLists/ArcadeGames.asset`) |
| Lobby scene | `_Scenes/Multiplayer Scenes/Tournament.unity` |

## 7. Editor follow-up (REQUIRED before the mode runs)

The code + data assets are committed; these Unity-editor steps cannot be done headlessly and are
needed to make the feature work end-to-end:

1. **AppManager (Bootstrap scene):** assign `TournamentData.asset` to the new `tournamentData`
   field on `AppManager`.
2. **Tournament scene** (`Tournament.unity`): it is still the leftover CellularDuel hierarchy —
   rebuild it into a UI-only lobby: strip the gameplay objects (Game/controller/Environment/
   Cell/Spawners), add a `ContainerScope` (`_Prefabs/CORE/ContainerScope.prefab`), add a
   `TournamentSceneView` with its `gameData`/`tournamentData` references + a title/lineup TMP
   (and optional host Start button), and lobby visuals.
3. **Scoreboard Continue button:** in the shared `GameCanvas`/`EndGameStatsPanel` prefab, add a
   **Continue** button GameObject, assign it to `Scoreboard.continueButton`, and wire its
   `onClick` → `Scoreboard.OnContinueButtonPressed()`.
4. **Arcade grid cell:** `ArcadeExploreView` pairs games to grid cells positionally, so add one
   more `GameCard` cell to the Menu_Main arcade grid for the 10th game (the Tournament card),
   else it is silently dropped.
5. **(Optional) cumulative-standings UI:** the standings live in `TournamentDataSO`; surface a
   compact panel on the Scoreboard or the Tournament summary when desired.

## 8. Verification

See the plan's verification section: solo + bot-fill (Continue advances; final game shows Play
Again + Main Menu; bots stable across games), 2-4 players in MPPM (clients show no buttons;
standings identical on every peer), flag hygiene (a normal game after a tournament shows the
standard buttons), and an edit-mode unit test for `TournamentDataSO.RecordResults`.

## 9. Deferred (later P3 plans)

Rewards (blocked on the P8 economy spec), post-tournament share screen, funnel instrumentation,
host-selected/randomized lineups, host-migration beyond existing disconnect handling, full QA
matrix.
