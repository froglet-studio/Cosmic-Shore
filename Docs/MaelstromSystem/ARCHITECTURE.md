# Maelstrom System — Architecture

Canonical reference for **Maelstrom Mode** — the session-level meta (P3 / R2) that strings the
three feature-complete domain minigames into one tournament with a per-player leaderboard.

> **See also:** `MAELSTROM_UX_HANDOFF.md` (same folder) — session handoff for the between-game splash,
> readable dwell, the Shuffle→Maelstrom display rename, and the summary `(You)` owner tag, with sequence
> diagrams, the inspector wiring those features depend on, and recommended next tasks.

> **Status:** implemented end-to-end. Code, data assets, **and** the Unity-editor wiring (scene
> content, prefab buttons, inspector references) are all in place: the Maelstrom scene drives both
> its lobby and summary layouts, and every domain game's Scoreboard carries a host-only Continue
> button (see **§7**, now complete). Remaining work is the **§9 Deferred** backlog (rewards, share
> screen, instrumentation) only.

> **Player-facing name — "Maelstrom":** the Arcade card for this mode is shown to players as **Maelstrom**
> (`ArcadeGameMaelstrom.asset` `DisplayName = "Maelstrom"`, rendered by `GameCard`). The in-scene
> lobby/summary banner is **data-driven from that same field** — `MaelstromSceneView.ModeName` reads
> `MaelstromDataSO.ModeCard.DisplayName` (the `ModeCard` reference is wired to the card asset), so the
> card's `DisplayName` is the **single source** of the player-facing name. The code, data
> (`MaelstromDataSO`), enum (`GameModes.Maelstrom = 36`), controller, and this doc keep the
> **Maelstrom** name — *Shuffle and Maelstrom are the same meta-mode*. (The **scene file** was renamed
> to `Maelstrom.unity` in the v2 rework; only the file name changed — the classes/data/enum stay Maelstrom.)
> **To rename the mode, change
> only the card's `DisplayName`** — full guide + what NOT to touch is in `Docs/ShuffleSystem/ARCHITECTURE.md`
> ("Renaming the mode"). Planned Shuffle-specific *behavior* changes (randomized lineup, per-domain
> `{2,1,0}` scoring + crystal-wallet credit, race-to-6) are tracked in that same doc as future extensions
> of this meta — not a new mode.

---

## 1. What it is

One session plays a **randomized lineup** drawn from the competitive domain games — **Skim Race
(SkimRace 33), Joust (34), Crystal Capture (35), Rampage (2), Peel the Cage (PeelTheCage 39), Scarab
Scramble (43), The Bends (42)**. Each game the host draws a random pool mode (no
immediate repeat) **and** a random intensity in `[1..X]` (X = the lobby-chosen intensity ceiling),
so a higher intensity widens the variety (`7 modes × X` "experiences", L1=7 … L4=28).

**The pool is authored, not coded** — it is `MaelstromData.asset`'s `GameQueue`, and every consumer
(`LoadRandomGame`, `IndexOfSceneName`, the hub's pool string, `ConnectingPanelController`) is
length-agnostic, so adding a mode is one asset edit. Three things a candidate must satisfy:

1. **Domain-scored.** Standings fold through `ScoringRuleSO.ResolvePlacementOrder` (§3), so the mode
   must rank *domains* by team total. All seven are `MultiplayerDomainGamesController` subclasses.
2. **Scene in Build Settings.** `LoadRandomGame` drives a `Single` load by scene name; a missing
   scene fails the round, not the draw.
3. **Player/domain range must contain the Maelstrom card's** (2–4 players, 2+ domains). The drawn
   mode's own card range is *not* re-checked at draw time — a mode capping at 3 players would break
   a 4-player lobby silently.

**Vessel-locked modes need no extra wiring.** Four of the seven are single-hull (Rampage and The
Bends are Dolphin, Peel the Cage is Rhino, Scarab Scramble is Scarab). `GameDataSO.SyncFromArcadeGame`
publishes the drawn card's `Vessels` list into `AllowedVesselClasses` and calls
`ClampSelectedVesselToGame`, so the round forces its own hull and the lobby's vessel pick applies
only to rounds that permit it. This is why the Maelstrom card's own `Vessels` list is a *lobby*
choice, not a session-wide lock.

**Known wrinkle — same-hull adjacency.** `PickRandomModeIndex` avoids repeating the previous
*index*, not the previous *vessel* or *arena*. Rampage and The Bends share both (Dolphin, the cactus
forest), so they can be drawn back-to-back and will read as one mode played twice. Fix, if it
bothers a playtest: widen the avoid-set to the previous mode's first `Vessels` entry. After each
game the active **domains** are ranked **by team total** (the mode rule's summed metric — see §3)
and earn **placement crystals** by domain place (1st = 2, 2nd = 1, 3rd = 0; `PointsByPlace`,
configurable — the **last**-placed domain always earns the table's last entry, 0, so a 2-domain
game pays `{2,0}`: losing never pays toward the race target). The cumulative **per-domain** total is
the leaderboard, and the session is a **race to `WinTarget` (6)** crystals — the first domain to
reach it wins, with a hard **`MaxGames` (7)** cap so a stalemate still ends. It appears as a normal
card in the Arcade panel (`GameModes.Maelstrom = 36`; the card's `DisplayName` is "Shuffle").
*(All Shuffle deltas shipped: per-domain `{2,1,0}` scoring, randomized lineup, race-to-6 / cap-7,
crystal-wallet credit, and the between-game loading-splash summary — see `Docs/ShuffleSystem/ARCHITECTURE.md`.)*

**Lobby minimum — 2 players, 2 domains.** Placement points are meaningless without opposing
teams (and the Joust leg is unplayable on a single domain — see JOUST.md Design Note 8). The Maelstrom
arcade card therefore sets `MinPlayersAllowed=2` and `MinDomainsAllowed=2` (`87658960`); the
configure modal floors both via `ArcadeGameConfigureModal.MinDomainsForGame`, so a solo host
always launches with at least one AI opponent on a second domain. No tournament-specific code is
needed — `MaelstromController` preserves the lobby's player/domain config across all three
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
  → Maelstrom (lobby) → ready-up → random game            (each transition a Single load)
       └─ game ends → Scoreboard.Continue (host) → Maelstrom (HUB: standings) → ready-up → random game → …
  → a domain hits 6 (or the 7-game cap) → Continue → Maelstrom (SUMMARY) → NEXT → results
       └─ Play Again (fresh shuffle) | Main Menu → Menu_Main (lava lamp)
```

The Maelstrom scene serves **three roles** — the intro lobby, the between-round **hub**, and the
end-of-tournament **summary** — and `MaelstromSceneView` picks the layout per load
(phase / `MaelstromController.IsShowingSummary`). Continue is shown on every game's scoreboard (host) and
always returns to the Maelstrom scene; once a domain reaches the target it loads in Summary phase, and
**Play Again / Main Menu live there** (behind the summary's NEXT step), not on the per-game scoreboard.

## 3. The brain — `MaelstromController` (persistent, network-free)

`MaelstromController` (`_Scripts/Controller/Arcade/Maelstrom/`) is a **pure-C# DI singleton**
created eagerly by `AppManager` (so it is alive from bootstrap and survives every Single load).
A static `Instance` lets scene MonoBehaviours reach it (mirrors `PartyInviteController.Instance`).

- **Standings are network-free.** On `gameData.OnMiniGameEnd`, **every peer** folds the
  already-synced `gameData.Results` (the ranked per-player `List<ScoreResult>`) into
  `MaelstromDataSO` via `RecordResults` — identical inputs → identical standings, no extra RPC.
  Domain placement is the mode rule's **team-total order**
  (`ScoringRuleSO.ResolvePlacementOrder` — domains by summed metric, ties → enum order
  Jade→Ruby→Gold; the same aggregation that ends the turn and picks `WinnerDomain`), computed from
  the still-synced `RoundStatsList` and passed in by the controller. The results-only reduction
  (each domain's place = its best player `Rank`) survives **only as a fallback** — it mis-placed
  teams whenever a losing team's player tied the top individual score (the 2v2 "Scurry" 17-vs-20
  regression). Places award `{2,1,0}` via `PointsForPlacement` (last place always earns the last
  entry, 0). Recording happens *before* the next load's `ResetRuntimeData` clears `Results`.
- **Only the host drives progression** (`BeginFirstGame` / `AdvanceToNextGame` /
  `RestartMaelstrom`): it draws a random `(mode, intensity ∈ [1..X])`, sets the per-game intensity
  on `gameData.SelectedIntensity`, then `SyncFromArcadeGame(mode) + InvokeGameLaunch()`. Clients
  follow the Single load (mode = loaded scene; intensity rides the existing config sync) — no shared
  RNG seed. Once `MaelstromDataSO.IsShuffleComplete`, `AdvanceToNextGame` loads the **summary** instead.
- **Phase is scene-load-driven** (deterministic on every peer): the **lobby scene** load runs
  `StartMaelstrom` (reset standings, capture the intensity ceiling, `IsActive=true`,
  `IsMaelstromMode=true`); each **pool game scene** load marks the loaded mode (`CurrentGameIndex`,
  used for repeat-avoidance) and goes `InGame`; **Menu_Main** load runs `EndMaelstrom` (clears the
  flags). `MaelstromStateMachine` tracks `Idle → Lobby → InGame → Complete → Summary` — `Complete`
  is the transient phase while the deciding game's scoreboard is up, `Summary` is the results scene
  itself (Play Again from `Summary` re-enters `InGame`; Main Menu returns to `Idle`).
- **The summary decision is authoritative, not phase-driven (race-to-6 fix).** At the Maelstrom scene
  load, `HandleSceneLoaded` shows the **Summary** when `IsActive && MaelstromDataSO.IsShuffleComplete`
  (deterministic on every peer) — **not** when the transient `Complete` phase happens to be set.
  `HandleMiniGameEnd` still sets `Complete` as a best-effort signal, but that transition only lands when
  the deciding game ends in the `InGame` phase; relying on it alone once let a domain hit `WinTarget` (6)
  yet route back to the hub for another game (the win silently swallowed). `EnterSummary` reaches
  `Summary` from `InGame`/`Lobby`/`Complete`, so the win always surfaces. Covered by
  `MaelstromStateMachineTests` + `MaelstromDataSOTests.IsShuffleComplete_*`.

Per-game stat reset is automatic (`SceneLoader` → `ResetRuntimeData` + each persistent
`Player.PrepareForNewScene` → `RoundStats.Cleanup`). Cumulative points live in `MaelstromDataSO`,
outside that reset, so they survive. AI backfill re-runs per scene; the **AI roster is seeded once**
(first game) into `MaelstromDataSO.MaelstromAINames` and reused, so bot identities stay stable
across games (AI `Player` objects are destroyed/recreated each scene). Standings are keyed by
**domain**, so per-game roster churn never affects the leaderboard.

## 4. End-of-game UI — the Scoreboard is the progression surface

The cinematic was removed (`Ys-bleeding-edge` `dbc7c703`); `EndGameSequencer` halts vessels,
plays the SFX, and raises `OnShowGameEndScreen` → `Scoreboard` (sole end-game UI). In tournament
mode `Scoreboard.ConfigureLobbyButtons` shows **only Continue, host-only**:

| Surface | Host sees | Hidden |
|---|---|---|
| Per-game scoreboard (after EVERY game) | **Continue** → `MaelstromController.AdvanceToNextGame()` | Play Again, Main Menu, Leave |
| Per-game scoreboard, on a client | — | all |
| **Maelstrom hub** (between rounds, shuffle not decided) | ready-up countdown → **START**, then auto-advances to the next random game | — |
| **Maelstrom summary** (shuffle decided) | **NEXT** → reveals results → host-only **Play Again** (→ `RestartMaelstrom()`) + **Main Menu** for everyone (host → `onClickToMainMenu` → Menu_Main for the whole party; client → `PartyInviteController.LeavePartyAndReturnToMenuAsync()` — leaves the party, returns solo) | — |
| Maelstrom hub / summary, on a client | — (follows the host's load; results + **Main Menu** on the summary) | Play Again |

`AdvanceToNextGame` **always** loads the Maelstrom scene (`LoadMaelstromScene`); the hub-vs-summary
choice is made on load from the authoritative, deterministic `MaelstromDataSO.IsShuffleComplete` (a
domain reached `WinTarget`, or `MaxGames` was hit) — **not** the transient `Complete` phase (see §3).
Mid-run it shows the standings **hub** (ready-up → next random game via `BeginNextRound`); once decided it
shows the results **summary**, whose active-panel button reads **NEXT** and reveals the end panel
(`MaelstromSceneView.OnPlayAgainPressed` / `OnMainMenuPressed`), not the Scoreboard. Play Again is
host-only; Main Menu shows on **every peer** — the host's press raises `onClickToMainMenu` (Netcode scene
load takes the whole party back over the live Relay), a client's press goes through
`PartyInviteController.LeavePartyAndReturnToMenuAsync()` (raising the SOAP event on a client would fade
and then defer to the server forever — `SceneLoader.ReturnToMainMenu` skips the load on connected clients).

**All Maelstrom-scene buttons are code-wired only** (`MaelstromSceneView.Awake` adds the listeners) — the
scene must NOT also add inspector `onClick` entries. Duplicate inspector wiring double-fired NEXT / Play
Again / Main Menu into `BeginNextRound`, launching a stray game off the summary (fixed; see
`MAELSTROM_REWORK_SPEC.md` v2.5). The summary cards and the round-card rows order by cumulative Total
Score (highest first), matching the leaderboard.

**Crystal reward (real wallet).** The placement crystals are also *real currency*: on each game's
Scoreboard, `AwardCrystalsToLocalPlayer` credits the **local** human's wallet
(`PlayerDataService.AddCrystals`, source `"shuffle_placement"`) with their domain's per-game `{2,1,0}`,
read from the injected `MaelstromDataSO.CrystalsForDomain(gameData.Results, localDomain,
placement)` — `placement` being the mode rule's team-total order (`ResolvePlacementOrder`) computed
once per show, so the badge/wallet match the standings fold exactly (a plain data-container read,
**not** a static `MaelstromController.Instance` reach-through). Each peer credits only its own local
player, once per game (AI have no wallet); the **last**-placed domain earns 0 (so the 2-domain loser
gets nothing, and 3rd of 3 gets nothing). The wallet write is wrapped in a try/catch: a
`PlayerDataService` hiccup degrades to a logged lost reward, never a missing end-game screen. The
per-player score cards show the same per-domain badge via `CardCrystalReward`. Gated on
`IsMaelstromMode` — outside a shuffle the Scoreboard keeps its original winner-only flat
`winnerCrystalReward`.

**Between-game summary overlay (SOAP — reuses the splash status surface).** `SceneTransitionManager`
owns **only** fades — it holds no UI text. The splash already has a SOLID/SOAP text view,
`BootStatusPanel`, fed by the `ScriptableEventBootStatusRequest` channel. On `OnLaunchGame` (fired on
host *and* clients, when the loading splash goes opaque), `BootStatusBroadcaster.HandleLaunchGame` — the
existing owner of "what the splash shows during a launch" — checks the shuffle state and, if mid-run
(`tournamentData.IsActive && !IsShuffleComplete && GamesPlayed > 0`), raises
`BootStatusRequest{Status, MaelstromStandingsFormatter.FormatRunning(tournamentData)}` instead of its
usual `Hide`. The standings (reduced from local standings on every peer — network-free) read on the
splash for the whole load, then the broadcaster's existing `HandleClientReady`→`Hide` clears them when
the new scene is ready. No new channel, view, or `TMP_Text` — and the controller no longer touches the
splash at all.

**Owner tag.** Scoring is per-DOMAIN (one row per team), so each peer passes its local player's domain
(`gameData.LocalPlayer.Domain`) into the formatter and the matching row is tagged ` <b>(You)</b>` — on
both the between-game splash (`FormatRunning`) and the final summary (`FormatFinal`) — so the owner can
read which team line is theirs. `Domains.Blue` (the no-team sentinel, never a standings row) tags nothing.

**Readable dwell.** A fast scene load would flash the standings by, so the load is held briefly behind
the opaque splash. `SceneLoader.LaunchGame` reads `MaelstromController.MinLoadSplashDwellSeconds`
(non-zero only under the *same* `IsActive && !IsShuffleComplete && GamesPlayed > 0` condition that shows
the standings — value `MaelstromDataSO.BetweenGameSummaryDwellSeconds`, default 2s) and `Max`es it with
the usual pre-load wait before `LoadScene`. Host-only: clients defer the load to the host at the
`LaunchGame` defer guard, so holding the host's `LoadScene` holds the whole party's splash. Zero outside
the window, so the first game, the load into the final summary, and Main-Menu returns are never delayed.

**Restart determinism:** Play Again calls `RestartMaelstrom()` → host loads the Maelstrom scene as a
fresh lobby; every peer resets its standings (keeping the intensity ceiling) when that scene loads while
still in phase `Summary` (`MaelstromController.HandleSceneLoaded` → `RestartFromSummary`), so the wipe is
consistent across the party without extra networking.

**Scoreboard position stability (`660e4d91`):** a tournament shows the Scoreboard once per leg, so
it re-shows the board up to three times in one session — exactly the case that exposed a drift
bug. `Scoreboard.PlayEntranceAnimation` slid the panel in by mutating its own `anchoredPosition`
and never restored it on hide; on a stretch-anchored panel each re-show captured the displaced
position as the new rest target, so the board crept off-base across the Joust / Crystal Capture
legs (SkimRace was immune — it shows the board once then full-scene-reloads). The entrance slide is
now disabled in favour of `Scoreboard.ShowScoreboardImmediate` (authored position, full alpha,
unit banner scale). See JOUST.md / SCURRY.md for the per-mode notes.

**Joust-leg AI hardening (`975271aa`):** because every tournament includes the Joust leg, the
player-seek AI was fixed so bots keep jousting instead of flying off when they lose their target
(empty opponent set / opponent mid-respawn): `AIPilot` now tracks the chosen opponent's live
transform every frame, falls back to the cell centre when no opponent qualifies, and re-acquires
on a faster cadence while unlocked. Full detail in JOUST.md Design Note 12.

## 5. Data — `MaelstromDataSO`

`_Scripts/Utility/DataContainers/Maelstrom/MaelstromDataSO.cs` (asset:
`_SO_Assets/Maelstrom/MaelstromData.asset`). Authored: `GameQueue` (the draw **pool** — the 3
`SO_ArcadeGame`s), `ModeCard` (the mode's own card — player-facing name), `PointsByPlace` (`{2,1,0}`),
`WinTarget` (6), `MaxGames` (7), `LobbySceneName`, four `ScriptableEventNoParam`s. Runtime
(non-serialized): `IsActive`, `CurrentGameIndex` (last loaded pool mode — repeat-avoidance),
`GamesPlayed`, `IntensityCeiling` (X, captured at start; **survives `ResetRuntime`** so Play Again
keeps it), `MaelstromAINames`, `Standings` (a `List<MaelstromDomainStanding>` — **keyed by
`Domains`**, not player). Key methods: `RecordResults(results)` (per-domain fold + `GamesPlayed++`,
see §3), `IsShuffleComplete` (race target reached or game cap hit — drives summary vs next game),
`BuildSortedStandings()` (points desc, tiebreak best placement, then domain enum order Jade→Ruby→Gold),
`PointsForPlacement(place, count)` (last place always earns the table's last entry — 0),
`ResetRuntime()`. `RecordResults` takes an optional `domainPlacementOrder` — the mode rule's
team-total order from `ScoringRuleSO.ResolvePlacementOrder`, passed by the controller (rank-derived
fallback otherwise). Edit-mode coverage: `Assets/_Scripts/Tests/Editor/MaelstromDataSOTests.cs`.

## 6. File index

| Role | File |
|---|---|
| Mode enum | `_Scripts/Data/Enums/GameModes.cs` (`Maelstrom = 36`) |
| Config flag | `_Scripts/Utility/DataContainers/GameDataSO.cs` (`IsMaelstromMode`) |
| Data container (+ `ModeName`, `CrystalsForDomain`) | `_Scripts/Utility/DataContainers/Maelstrom/MaelstromDataSO.cs` |
| Standings text formatting (shared, DRY) | `_Scripts/Utility/DataContainers/Maelstrom/MaelstromStandingsFormatter.cs` |
| State machine | `_Scripts/Controller/Arcade/Maelstrom/MaelstromStateMachine.cs` |
| Controller (brain) | `_Scripts/Controller/Arcade/Maelstrom/MaelstromController.cs` |
| Lobby scene view | `_Scripts/Controller/Arcade/Maelstrom/MaelstromSceneView.cs` |
| End-game buttons + entrance + placement wallet credit (via injected `MaelstromDataSO`) | `_Scripts/UI/Scoreboard.cs` |
| Between-game summary text (SOAP, reuses the splash status surface) | `_Scripts/UI/Screens/BootStatusBroadcaster.cs` (shuffle branch) → `BootStatusPanel` via `Event_BootStatusRequest` |
| Lobby player/domain floor | `_Scripts/UI/Modals/ArcadeGameConfigureModal.cs` (`MinDomainsForGame`) |
| Per-game min domains field | `_Scripts/ScriptableObjects/SO_ArcadeGame.cs` (`MinDomainsAllowed`) |
| Joust-leg opponent-seek AI | `_Scripts/Controller/AI/AIPilot.cs` |
| Client flag sync | `_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs` |
| Stable AI roster | `_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs` |
| DI registration | `_Scripts/System/AppManager.cs` |
| Card unlock | `_Scripts/System/Progression/GameModeProgressionService.cs` |
| Data asset | `_SO_Assets/Maelstrom/MaelstromData.asset` (+ 4 `Event_Maelstrom*.asset`) |
| Arcade card | `_SO_Assets/Games/ArcadeGameMaelstrom.asset` (in `GameLists/ArcadeGames.asset`) |
| Lobby / hub / summary scene | `_Scenes/Multiplayer Scenes/Maelstrom.unity` |

## 7. Editor wiring

The mode runs end-to-end; one **optional** wire remains for the §4 between-game summary overlay
(last bullet).

- **AppManager** — `tournamentData` assigned; `MaelstromController` registered and constructed with
  `gameData` + `tournamentData` + `sceneNames` + `sceneTransitionManager`, created eagerly at bootstrap
  so it survives every Single load.
- **`Maelstrom.unity`** — a UI-only scene whose `MaelstromSceneView` drives the intro lobby, the
  between-round hub, and the results summary (layout chosen per load by phase / `IsShuffleComplete`). The
  **current v2 layout + the exact field-by-field wiring** (active/summary panels, round cards,
  `MaelstromLobbyNetwork` ready-up) is documented in `MAELSTROM_REWORK_SPEC.md` §6 + the v2 sections —
  refer there rather than re-describing it here.
  - **Buttons are code-wired only** — `MaelstromSceneView.Awake` adds `OnReadyButtonPressed` (START/NEXT),
    `OnPlayAgainPressed`, and `OnMainMenuPressed`. Do **NOT** add inspector `onClick` entries: duplicate
    wiring double-fires the press and launches a stray game off the summary (`MAELSTROM_REWORK_SPEC.md`
    v2.5). `onClickToMainMenu` is wired to `EventOnClickToMainMenuButton.asset` — the **same** main-menu
    `ScriptableEventNoParam` the Scoreboard's Main Menu raises and `SceneLoader` listens to.
- **Scoreboard Continue button** — present on the shared end-game canvas
  (`GameCanvas-SkimRace.prefab`, used by all three domain-game scenes — SkimRace, Joust, Crystal
  Capture — plus `EndGameStatsPanel.prefab`) and wired to `OnContinueButtonPressed()`. Host-only,
  shown on every game (see §4).
- **Arcade card + grid cell** — `ArcadeGameMaelstrom.asset` present in `GameLists/ArcadeGames.asset`,
  with `MinPlayersAllowed=2`, `MaxPlayersAllowed=4`, `MinDomainsAllowed=2`, `MinIntensity=1`,
  `MaxIntensity=4` (`87658960`) — so the configure modal floors both player and domain count to 2
  (see §1, *Lobby minimum*).
- **Between-game summary (SOAP — outstanding wires).** Wire `MaelstromData.asset` into
  `BootStatusBroadcaster.tournamentData` (on the splash canvas). The §4 running standings then show on
  the existing `BootStatusPanel.statusText` during shuffle inter-game loads — **no new object, and no
  `TMP_Text`/field on `SceneTransitionManager`** (it owns only fades now). Also wire `MaelstromData.asset`
  into each domain-game `Scoreboard.tournamentData` (`GameCanvas-SkimRace.prefab` + the scene-added
  Scoreboards in Joust / Crystal Capture) for the placement wallet credit + card badge. Unwired, both
  degrade gracefully (clean splash / flat winner reward).

No per-button visibility code lives in the scene — `MaelstromSceneView` and `Scoreboard`
drive it (phase-selected; host-only except the summary's Main Menu, which every peer gets).

## 8. Verification

See the plan's verification section: solo + bot-fill (Continue advances; final game shows Play
Again + Main Menu; bots stable across games), 2-4 players in MPPM (clients show no per-game buttons
and no Play Again, but DO get Main Menu on the final summary — pressing it leaves the party and lands
that client alone in Menu_Main while the rest stay on the summary; standings identical on every peer),
flag hygiene (a normal game after a tournament shows the standard buttons), and an edit-mode unit
test for `MaelstromDataSO.RecordResults`.

## 9. Deferred (later P3 plans)

Rewards (blocked on the P8 economy spec), post-tournament share screen, funnel instrumentation,
host-selected/randomized lineups, host-migration beyond existing disconnect handling, full QA
matrix.
