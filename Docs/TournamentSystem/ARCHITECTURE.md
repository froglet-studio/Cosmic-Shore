# Tournament System — Architecture

Canonical reference for **Tournament Mode** — the session-level meta (P3 / R2) that strings the
three feature-complete domain minigames into one tournament with a per-player leaderboard.

> **Status:** implemented end-to-end. Code, data assets, **and** the Unity-editor wiring (scene
> content, prefab buttons, inspector references) are all in place: the Tournament scene drives both
> its lobby and summary layouts, and every domain game's Scoreboard carries a host-only Continue
> button (see **§7**, now complete). Remaining work is the **§9 Deferred** backlog (rewards, share
> screen, instrumentation) only.

> **Player-facing name — "Maelstrom":** the Arcade card for this mode is shown to players as **Maelstrom**
> (`ArcadeGameTournament.asset` `DisplayName = "Maelstrom"`, rendered by `GameCard`). The in-scene
> lobby/summary banner is **data-driven from that same field** — `TournamentSceneView.ModeName` reads
> `TournamentDataSO.ModeCard.DisplayName` (the `ModeCard` reference is wired to the card asset), so the
> card's `DisplayName` is the **single source** of the player-facing name. The code, scene
> (`Tournament.unity`), data (`TournamentDataSO`), enum (`GameModes.Tournament = 36`), and this doc keep
> the **Tournament** name — *Shuffle and Tournament are the same meta-mode*. **To rename the mode, change
> only the card's `DisplayName`** — full guide + what NOT to touch is in `Docs/ShuffleSystem/ARCHITECTURE.md`
> ("Renaming the mode"). Planned Shuffle-specific *behavior* changes (randomized lineup, per-domain
> `{2,1,0}` scoring + crystal-wallet credit, race-to-6) are tracked in that same doc as future extensions
> of this meta — not a new mode.

---

## 1. What it is

One session plays a **randomized lineup** drawn from the competitive domain games — **Skim Race
(HexRace 33), Joust (34), Crystal Capture (35)**. Each game the host draws a random pool mode (no
immediate repeat) **and** a random intensity in `[1..X]` (X = the lobby-chosen intensity ceiling),
so a higher intensity widens the variety (`3 modes × X` "experiences", L1=3 … L4=12). After each
game the active **domains** are ranked and earn **placement crystals** by domain place
(1st = 2, 2nd = 1, 3rd = 0; `PointsByPlace`, configurable). The cumulative **per-domain** total is
the leaderboard, and the session is a **race to `WinTarget` (6)** crystals — the first domain to
reach it wins, with a hard **`MaxGames` (7)** cap so a stalemate still ends. It appears as a normal
card in the Arcade panel (`GameModes.Tournament = 36`; the card's `DisplayName` is "Shuffle").
*(All Shuffle deltas shipped: per-domain `{2,1,0}` scoring, randomized lineup, race-to-6 / cap-7,
crystal-wallet credit, and the between-game loading-splash summary — see `Docs/ShuffleSystem/ARCHITECTURE.md`.)*

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
  → Tournament (lobby) → random game → random game → …   (each a Single load)
                              └─ Scoreboard.Continue (host) advances after EVERY game; party follows
  → a domain hits 6 (or the 7-game cap) → Continue → Tournament (SUMMARY: all results)
       └─ Play Again (fresh shuffle) | Main Menu → Menu_Main (lava lamp)
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
  `RecordResults` reduces the per-player ranks to **per-domain** placement: each domain's place =
  its best (lowest) player `Rank`, domains ordered by that (ties → enum order Jade→Ruby→Gold), then
  awarded `{2,1,0}`. Recording happens *before* the next load's `ResetRuntimeData` clears `Results`.
- **Only the host drives progression** (`BeginFirstGame` / `AdvanceToNextGame` /
  `RestartTournament`): it draws a random `(mode, intensity ∈ [1..X])`, sets the per-game intensity
  on `gameData.SelectedIntensity`, then `SyncFromArcadeGame(mode) + InvokeGameLaunch()`. Clients
  follow the Single load (mode = loaded scene; intensity rides the existing config sync) — no shared
  RNG seed. Once `TournamentDataSO.IsShuffleComplete`, `AdvanceToNextGame` loads the **summary** instead.
- **Phase is scene-load-driven** (deterministic on every peer): the **lobby scene** load runs
  `StartTournament` (reset standings, capture the intensity ceiling, `IsActive=true`,
  `IsTournamentMode=true`); each **pool game scene** load marks the loaded mode (`CurrentGameIndex`,
  used for repeat-avoidance) and goes `InGame`; **Menu_Main** load runs `EndTournament` (clears the
  flags). `TournamentStateMachine` tracks `Idle → Lobby → InGame → Complete → Summary` — `Complete`
  is the transient phase while the deciding game's scoreboard is up, `Summary` is the results scene
  itself (Play Again from `Summary` re-enters `InGame`; Main Menu returns to `Idle`).

Per-game stat reset is automatic (`SceneLoader` → `ResetRuntimeData` + each persistent
`Player.PrepareForNewScene` → `RoundStats.Cleanup`). Cumulative points live in `TournamentDataSO`,
outside that reset, so they survive. AI backfill re-runs per scene; the **AI roster is seeded once**
(first game) into `TournamentDataSO.TournamentAINames` and reused, so bot identities stay stable
across games (AI `Player` objects are destroyed/recreated each scene). Standings are keyed by
**domain**, so per-game roster churn never affects the leaderboard.

## 4. End-of-game UI — the Scoreboard is the progression surface

The cinematic was removed (`Ys-bleeding-edge` `dbc7c703`); `EndGameSequencer` halts vessels,
plays the SFX, and raises `OnShowGameEndScreen` → `Scoreboard` (sole end-game UI). In tournament
mode `Scoreboard.ConfigureLobbyButtons` shows **only Continue, host-only**:

| Surface | Host sees | Hidden |
|---|---|---|
| Per-game scoreboard (after EVERY game) | **Continue** → `TournamentController.AdvanceToNextGame()` | Play Again, Main Menu, Leave |
| Per-game scoreboard, on a client | — | all |
| **Tournament Summary screen** (after the shuffle is decided) | **Play Again** (→ `RestartTournament()`) + **Main Menu** (→ `onClickToMainMenu` → Menu_Main) | — |
| Tournament Summary screen, on a client | — (results only) | all |

`AdvanceToNextGame` draws + loads the next random game, or — once `TournamentDataSO.IsShuffleComplete`
(a domain reached `WinTarget`, or `MaxGames` was hit) — loads the Tournament scene in Summary phase.
Play Again / Main Menu are handled by `TournamentSceneView` (`OnPlayAgainPressed` / `OnMainMenuPressed`),
not the Scoreboard.

**Crystal reward (real wallet).** The placement crystals are also *real currency*: on each game's
Scoreboard, `AwardCrystalsToLocalPlayer` credits the **local** human's wallet
(`PlayerDataService.AddCrystals`, source `"shuffle_placement"`) with their domain's per-game `{2,1,0}`,
read from the injected `TournamentDataSO.CrystalsForDomain(gameData.Results, localDomain)` (computed
from the synced `Results`, so it's order-independent — and a plain data-container read, **not** a
static `TournamentController.Instance` reach-through). Each peer credits only its own local player,
once per game (AI have no wallet); a 3rd-place domain earns 0. The per-player score cards show the same
per-domain badge via `CardCrystalReward`. Gated on `IsTournamentMode` — outside a shuffle the Scoreboard
keeps its original winner-only flat `winnerCrystalReward`.

**Between-game summary overlay (SOAP — reuses the splash status surface).** `SceneTransitionManager`
owns **only** fades — it holds no UI text. The splash already has a SOLID/SOAP text view,
`BootStatusPanel`, fed by the `ScriptableEventBootStatusRequest` channel. On `OnLaunchGame` (fired on
host *and* clients, when the loading splash goes opaque), `BootStatusBroadcaster.HandleLaunchGame` — the
existing owner of "what the splash shows during a launch" — checks the shuffle state and, if mid-run
(`tournamentData.IsActive && !IsShuffleComplete && GamesPlayed > 0`), raises
`BootStatusRequest{Status, TournamentStandingsFormatter.FormatRunning(tournamentData)}` instead of its
usual `Hide`. The standings (reduced from local standings on every peer — network-free) read on the
splash for the whole load, then the broadcaster's existing `HandleClientReady`→`Hide` clears them when
the new scene is ready. No new channel, view, or `TMP_Text` — and the controller no longer touches the
splash at all.

**Restart determinism:** Play Again calls `RestartTournament()` → host draws + loads a fresh random
game directly; every peer resets its standings when that game loads while still in phase `Summary`
(see `TournamentController.HandleSceneLoaded` — any pool game load in `Summary` is a restart), so the
wipe is consistent across the party without extra networking.

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
`_SO_Assets/Tournament/TournamentData.asset`). Authored: `GameQueue` (the draw **pool** — the 3
`SO_ArcadeGame`s), `ModeCard` (the mode's own card — player-facing name), `PointsByPlace` (`{2,1,0}`),
`WinTarget` (6), `MaxGames` (7), `LobbySceneName`, four `ScriptableEventNoParam`s. Runtime
(non-serialized): `IsActive`, `CurrentGameIndex` (last loaded pool mode — repeat-avoidance),
`GamesPlayed`, `IntensityCeiling` (X, captured at start; **survives `ResetRuntime`** so Play Again
keeps it), `TournamentAINames`, `Standings` (a `List<TournamentDomainStanding>` — **keyed by
`Domains`**, not player). Key methods: `RecordResults(results)` (per-domain fold + `GamesPlayed++`,
see §3), `IsShuffleComplete` (race target reached or game cap hit — drives summary vs next game),
`BuildSortedStandings()` (points desc, tiebreak best placement, then domain enum order Jade→Ruby→Gold),
`ResetRuntime()`. Edit-mode coverage: `Assets/_Scripts/Tests/EditMode/TournamentDataSOTests.cs`.

## 6. File index

| Role | File |
|---|---|
| Mode enum | `_Scripts/Data/Enums/GameModes.cs` (`Tournament = 36`) |
| Config flag | `_Scripts/Utility/DataContainers/GameDataSO.cs` (`IsTournamentMode`) |
| Data container (+ `ModeName`, `CrystalsForDomain`) | `_Scripts/Utility/DataContainers/Tournament/TournamentDataSO.cs` |
| Standings text formatting (shared, DRY) | `_Scripts/Utility/DataContainers/Tournament/TournamentStandingsFormatter.cs` |
| State machine | `_Scripts/Controller/Arcade/Tournament/TournamentStateMachine.cs` |
| Controller (brain) | `_Scripts/Controller/Arcade/Tournament/TournamentController.cs` |
| Lobby scene view | `_Scripts/Controller/Arcade/Tournament/TournamentSceneView.cs` |
| End-game buttons + entrance + placement wallet credit (via injected `TournamentDataSO`) | `_Scripts/UI/Scoreboard.cs` |
| Between-game summary text (SOAP, reuses the splash status surface) | `_Scripts/UI/Screens/BootStatusBroadcaster.cs` (shuffle branch) → `BootStatusPanel` via `Event_BootStatusRequest` |
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

## 7. Editor wiring

The mode runs end-to-end; one **optional** wire remains for the §4 between-game summary overlay
(last bullet).

- **AppManager** — `tournamentData` assigned; `TournamentController` registered and constructed with
  `gameData` + `tournamentData` + `sceneNames` + `sceneTransitionManager`, created eagerly at bootstrap
  so it survives every Single load.
- **`Tournament.unity`** — a UI-only scene whose `TournamentSceneView` drives two mutually-exclusive
  layouts, chosen per load from `TournamentController.IsShowingSummary`:
  - *Lobby* (`lobbyRoot`): `titleText` + `lineupText` (random rotation + "first to N") + a host-only **Start**
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
- **Between-game summary (SOAP — outstanding wires).** Wire `TournamentData.asset` into
  `BootStatusBroadcaster.tournamentData` (on the splash canvas). The §4 running standings then show on
  the existing `BootStatusPanel.statusText` during shuffle inter-game loads — **no new object, and no
  `TMP_Text`/field on `SceneTransitionManager`** (it owns only fades now). Also wire `TournamentData.asset`
  into each domain-game `Scoreboard.tournamentData` (`GameCanvas-HexRace.prefab` + the scene-added
  Scoreboards in Joust / Crystal Capture) for the placement wallet credit + card badge. Unwired, both
  degrade gracefully (clean splash / flat winner reward).

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
