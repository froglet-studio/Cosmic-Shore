# Tournament System — Architecture

Canonical reference for **Tournament Mode** — the session-level meta (P3 / R2) that strings the
three feature-complete domain minigames into one tournament with a per-player leaderboard.

> **Status:** implemented end-to-end. Code, data assets, **and** the Unity-editor wiring (scene
> content, prefab buttons, inspector references) are all in place: the Tournament scene drives both
> its lobby and summary layouts, and every domain game's Scoreboard carries a host-only Continue
> button (see **§7**, now complete). Remaining work is the **§9 Deferred** backlog (rewards, share
> screen, instrumentation) only.

---

## 1. What it is

One tournament plays a **fixed lineup** — **Skim Race (HexRace 33) → Joust (34) → Crystal
Capture (35)** — back-to-back. After each game, players earn **placement points** (1st = 10,
2nd = 6, 3rd = 3, 4th = 1; configurable). The cumulative per-player total is the tournament
leaderboard. It appears as a normal card in the Arcade panel (`GameModes.Tournament = 36`).

**Lobby minimum — 2 players, 2 domains.** Placement points are meaningless without opposing
teams (and the Joust leg is unplayable on a single domain — see JOUST.md Design Note 8). The Tournament
arcade card therefore sets `MinPlayersAllowed=2` and `MinDomainsAllowed=2` (`87658960`); the
configure modal floors both via `ArcadeGameConfigureModal.MinDomainsForGame`, so a solo host
always launches with at least one AI opponent on a second domain. No tournament-specific code is
needed — `TournamentController` preserves the lobby's player/domain config across all three
`Single` loads (`SyncFromArcadeGame` only sets scene/mode/multiplayer, never the counts).

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
                              └─ Scoreboard.Continue (host) advances on EVERY game; party follows
  → Continue after the last game → Tournament (SUMMARY: all results)
       └─ Play Again (restart whole tournament) | Main Menu → Menu_Main (lava lamp)
```

The Tournament scene is **both** the intro lobby and the end-of-tournament **summary**;
`TournamentSceneView` picks the layout from `TournamentController.IsShowingSummary`
(phase `Summary`). Continue is shown on every game's scoreboard (host); the final Continue
loads the Tournament scene in Summary phase, and **Play Again / Main Menu live there**, not on
the per-game scoreboard.

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
  flags). `TournamentStateMachine` tracks `Idle → Lobby → InGame → Complete → Summary` — `Complete`
  is the transient phase while the last game's scoreboard is up, `Summary` is the results scene
  itself (Play Again from `Summary` re-enters `InGame`; Main Menu returns to `Idle`).

Per-game stat reset is automatic (`SceneLoader` → `ResetRuntimeData` + each persistent
`Player.PrepareForNewScene` → `RoundStats.Cleanup`). Cumulative points live in `TournamentDataSO`,
outside that reset, so they survive. AI backfill re-runs per scene; the **AI roster is seeded once**
(first game) into `TournamentDataSO.TournamentAINames` and reused, so name-keyed bot standings
attribute across games (AI `Player` objects are destroyed/recreated each scene).

## 4. End-of-game UI — the Scoreboard is the progression surface

The cinematic was removed (`Ys-bleeding-edge` `dbc7c703`); `EndGameSequencer` halts vessels,
plays the SFX, and raises `OnShowGameEndScreen` → `Scoreboard` (sole end-game UI). In tournament
mode `Scoreboard.ConfigureLobbyButtons` shows **only Continue, host-only**:

| Surface | Host sees | Hidden |
|---|---|---|
| Per-game scoreboard (EVERY game, incl. last) | **Continue** → `TournamentController.AdvanceToNextGame()` | Play Again, Main Menu, Leave |
| Per-game scoreboard, on a client | — | all |
| **Tournament Summary screen** (after last game) | **Play Again** (→ `RestartTournament()`) + **Main Menu** (→ `onClickToMainMenu` → Menu_Main) | — |
| Tournament Summary screen, on a client | — (results only) | all |

`AdvanceToNextGame` loads the next game, or — on the last game (`TournamentDataSO.IsLastGame`) —
loads the Tournament scene in Summary phase. Play Again / Main Menu are handled by
`TournamentSceneView` (`OnPlayAgainPressed` / `OnMainMenuPressed`), not the Scoreboard.

**Restart determinism:** Play Again calls `RestartTournament()` → host loads game 1 directly;
every peer resets its standings when game 1 loads while still in phase `Summary` (see
`TournamentController.HandleSceneLoaded`), so the wipe is consistent across the party without any
extra networking.

**Scoreboard position stability (`660e4d91`):** a tournament shows the Scoreboard once per leg, so
it re-shows the board up to three times in one session — exactly the case that exposed a drift
bug. `Scoreboard.PlayEntranceAnimation` slid the panel in by mutating its own `anchoredPosition`
and never restored it on hide; on a stretch-anchored panel each re-show captured the displaced
position as the new rest target, so the board crept off-base across the Joust / Crystal Capture
legs (HexRace was immune — it shows the board once then full-scene-reloads). The entrance slide is
now disabled in favour of `Scoreboard.ShowScoreboardImmediate` (authored position, full alpha,
unit banner scale). See JOUST.md / CRYSTAL_CAPTURE.md for the per-mode notes.

**Joust-leg AI hardening (`975271aa`):** because every tournament includes the Joust leg, the
player-seek AI was fixed so bots keep jousting instead of flying off when they lose their target
(empty opponent set / opponent mid-respawn): `AIPilot` now tracks the chosen opponent's live
transform every frame, falls back to the cell centre when no opponent qualifies, and re-acquires
on a faster cadence while unlocked. Full detail in JOUST.md Design Note 12.

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
| End-game buttons + entrance (`ShowScoreboardImmediate`) | `_Scripts/UI/Scoreboard.cs` |
| Lobby player/domain floor | `_Scripts/UI/Modals/ArcadeGameConfigureModal.cs` (`MinDomainsForGame`) |
| Per-game min domains field | `_Scripts/ScriptableObjects/SO_ArcadeGame.cs` (`MinDomainsAllowed`) |
| Joust-leg opponent-seek AI | `_Scripts/Controller/AI/AIPilot.cs` |
| Client flag sync | `_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs` |
| Stable AI roster | `_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs` |
| DI registration | `_Scripts/System/AppManager.cs` |
| Card unlock | `_Scripts/System/Progression/GameModeProgressionService.cs` |
| Data asset | `_SO_Assets/Tournament/TournamentData.asset` (+ 4 `Event_Tournament*.asset`) |
| Arcade card | `_SO_Assets/Games/ArcadeGameTournament.asset` (in `GameLists/ArcadeGames.asset`) |
| Lobby scene | `_Scenes/Multiplayer Scenes/Tournament.unity` |

## 7. Editor wiring — complete

All editor wiring is committed; the mode runs end-to-end.

- **AppManager** — `tournamentData` assigned; `TournamentController` registered and injected, so it
  is created eagerly at bootstrap and survives every Single load.
- **`Tournament.unity`** — a UI-only scene whose `TournamentSceneView` drives two mutually-exclusive
  layouts, chosen per load from `TournamentController.IsShowingSummary`:
  - *Lobby* (`lobbyRoot`): `titleText` + `lineupText` (ordered game lineup) + a host-only **Start**
    button (`hostStartButton`) wired to `OnHostStartPressed()` — or, if no button, the host
    auto-advances after `autoStartDelaySeconds`.
  - *Summary* (`summaryRoot`): `resultsText` (final standings + per-game placements) + host-only
    **Play Again** → `OnPlayAgainPressed()` and **Main Menu** → `OnMainMenuPressed()`. The view's
    `onClickToMainMenu` is wired to `EventOnClickToMainMenuButton.asset` — the **same** main-menu
    `ScriptableEventNoParam` the Scoreboard's Main Menu raises and `SceneLoader` listens to.
- **Scoreboard Continue button** — present on the shared end-game canvas
  (`GameCanvas-HexRace.prefab`, used by all three domain-game scenes — HexRace, Joust, Crystal
  Capture — plus `EndGameStatsPanel.prefab`) and wired to `OnContinueButtonPressed()`. Host-only,
  shown on every game (see §4).
- **Arcade card + grid cell** — `ArcadeGameTournament.asset` present in `GameLists/ArcadeGames.asset`,
  with `MinPlayersAllowed=2`, `MaxPlayersAllowed=4`, `MinDomainsAllowed=2`, `MinIntensity=1`,
  `MaxIntensity=4` (`87658960`) — so the configure modal floors both player and domain count to 2
  (see §1, *Lobby minimum*).

No per-button visibility code lives in the scene — `TournamentSceneView` and `Scoreboard`
drive it (host-only, phase-selected).

## 8. Verification

See the plan's verification section: solo + bot-fill (Continue advances; final game shows Play
Again + Main Menu; bots stable across games), 2-4 players in MPPM (clients show no buttons;
standings identical on every peer), flag hygiene (a normal game after a tournament shows the
standard buttons), and an edit-mode unit test for `TournamentDataSO.RecordResults`.

## 9. Deferred (later P3 plans)

Rewards (blocked on the P8 economy spec), post-tournament share screen, funnel instrumentation,
host-selected/randomized lineups, host-migration beyond existing disconnect handling, full QA
matrix.
