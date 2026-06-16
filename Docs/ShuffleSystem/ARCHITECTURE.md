# Shuffle System — Architecture

Canonical reference for **Shuffle Mode** — a session-level meta-mode (the **sibling of Tournament**)
that strings a **randomized, variable-length** sequence of the competitive domain minigames into one
**"race to 6"** with a per-**domain** crystal score.

> **Status:** specified, not yet implemented. This document is the **design of record** agreed before
> building. Shuffle reuses the Tournament meta-mode pattern almost wholesale
> (`Docs/TournamentSystem/ARCHITECTURE.md`); the **§10 Differences-from-Tournament** table is the
> authoritative delta, and **§13 Build order** is the implementation backlog. Read the Tournament doc
> first — everything not called out as a difference is identical.

---

## 1. What it is

One shuffle session plays a **random sequence** of the competitive domain minigames — currently
**Skim Race (HexRace 33), Joust (34), Crystal Capture (35)** — back-to-back, until a domain wins.

- **Scoring is per DOMAIN.** After each game the active domains are ranked and awarded
  **omni-crystal currency by placement: 1st = 2, 2nd = 1, 3rd = 0** (`PointsByPlace = {2,1,0}`).
- **The crystals are real.** They are credited to each player's persistent wallet
  (`PlayerDataService.AddCrystals`) **and** are simultaneously the shuffle's running score — one number,
  two jobs.
- **Race to 6.** The first domain whose cumulative shuffle crystals reach **≥ 6** wins. There is a hard
  cap of **7 games** to cover ties (e.g. a `6-6-2` finish).
- Multiplayer meta; **solo plays through Netcode with AI backfill**, exactly like Tournament.
- Appears as an Arcade card. Planned mode id: **`GameModes.Shuffle = 37`** (7 and 31 stay reserved).

```
Menu_Main → [Arcade card → ArcadeGameConfigureModal ready-up]
  → Shuffle (brief intro)  → random game → random game → … (each a Single load)
        each game ends → its own Scoreboard.Continue (host) → loading splash shows the
        latest DOMAIN summary (~3s) while the next game loads → repeat
  → a domain hits ≥6 (or 7 games played) → Continue → Shuffle (END SUMMARY: winner + standings)
       └─ Play Again (new shuffle) | Main Menu → Menu_Main (lava lamp)
```

## 2. Scoring & currency — per domain, real crystals

- **Domain placement is derived network-free** from the already-synced
  `GameDataSO.Results` (the ranked, best-first per-player `List<ScoreResult>`): walk the ranked list and
  the order in which each **active domain first appears** is that domain's placement (1st / 2nd / 3rd).
  Identical inputs on every peer → identical domain order, no extra RPC. This mirrors Tournament's
  network-free standings; the only change is keying by `Domain` instead of player name.
- `ShuffleDataSO.PointsByPlace = {2, 1, 0}` → crystals for the 1st / 2nd / 3rd domain. Places beyond the
  table score 0 (there are at most three active domains).
- **Cumulative standings are keyed by Domain** (Jade / Ruby / Gold) and folded on every peer in
  `gameData.OnMiniGameEnd`, **before** the next Single load's `ResetRuntimeData` clears `Results`.
- **Wallet credit.** Each peer credits its **local human player's** wallet by that player's *domain*
  placement reward, **once per game**. This generalizes `Scoreboard.AwardCrystalsIfLocalWinner` from the
  current winner-only flat `winnerCrystalReward = 5` to placement-based `{2,1,0}`. AI players have no
  wallet, so they are not credited (their domain standings still accrue for the race).
- **Win check** (deterministic on every peer):
  `IsComplete = anyDomainTotal ≥ WinTarget(6) || GamesPlayed == MaxGames(7)`.

## 3. Randomized lineup — intensity sets the draw pool

- An **"experience"** is a **(game mode, intensity level)** pair. With the 3 domain modes and a shuffle
  intensity of **X**, the draw pool is `3 modes × X intensities = 3X` experiences — so **L1 = 3, L2 = 6,
  L3 = 9, L4 = 12 experiences**, matching the spec.
- **Each game, the host draws** a random mode (with repeat-avoidance against the immediately-previous
  mode) **and** a random intensity in **[1 .. X]**, then:
  `gameData.SelectedIntensity = drawnIntensity` → `gameData.SyncFromArcadeGame(drawnMode)` →
  `gameData.InvokeGameLaunch()`. Clients **follow** the host's Single load and learn the mode from the
  loaded scene + synced config — **no seed or selection RPC** (host-authoritative draw, exactly like
  Tournament's host-only progression).
- The **number of games played** is governed by the race-to-6 / cap-7 win check (§2), **independent** of
  the pool size — the pool size is variety, not length.

## 4. The load model — sequential `Single` (unchanged from Tournament)

Identical to `Docs/TournamentSystem/ARCHITECTURE.md` §2: every transition is a host-driven
`LoadSceneMode.Single` load via `SceneLoader` / `NetworkManager.SceneManager`; the NetworkManager / UGS
session / Relay and the `Player` NetworkObjects persist across loads; the host drives, clients follow.
**No additive loading, no new NetworkBehaviour** for the meta.

## 5. The brain — `ShuffleController` (persistent, network-free)

A near-clone of `TournamentController`: a **pure-C# DI singleton** created eagerly by `AppManager`
(alive from bootstrap, survives every Single load), with a static `Instance` so the Scoreboard's
Continue button and `ShuffleSceneView` can reach it. It listens to `gameData.OnMiniGameEnd` +
`SceneManager.sceneLoaded`, owns a `ShuffleStateMachine`, and:

- folds the synced results into **per-domain** standings + the local wallet credit (§2);
- on `OnMiniGameEnd`, evaluates `IsComplete` (§2);
- `AdvanceToNextGame()` (host, from Scoreboard Continue) → loads the **end summary** when complete,
  else **draws + loads the next random game** (§3);
- phase is scene-load-driven and deterministic on every peer (mirrors Tournament).

The **AI roster is seeded once** into `ShuffleDataSO.ShuffleAINames` and reused across games so name-keyed
bot attribution survives the per-scene AI respawn (same mechanism Tournament uses via
`ServerPlayerVesselInitializerWithAI`).

## 6. Between-match flow — **no new ready gate**

The existing per-game flow is reused end to end. The **only** new piece is the loading-splash summary
(§7):

```
game ends → the game's own Scoreboard + Continue button (host-only, ALREADY wired)
  → [host taps Continue] → loading splash shows the latest DOMAIN summary for ~3s
        (the next game loads in the background; the splash stays up until load completes, as usual)
  → new game's camera cinematic (existing) → countdown Ready check (existing) → play
  → repeat until a domain reaches 6 (or 7 games) → Continue → Shuffle END summary screen
```

- **No new networked ready gate.** The host-only Scoreboard `Continue` is **generalized** to route to
  `ShuffleController.Instance.AdvanceToNextGame()` when `gameData.IsShuffleMode` (today it routes to
  `TournamentController` under `IsTournamentMode`). `Scoreboard.ConfigureLobbyButtons` shows Continue for
  both metas, host-only.
- The per-game **countdown Ready** after each load is the existing one — unchanged.

## 7. Loading-splash summary overlay (net-new UI)

There is **no pre-existing loading-tip / tooltip system** to reuse — `SceneTransitionManager` only fades
to opaque black (no text, no per-mode content). So this is net-new, built as a **reusable** surface:

- Add a **message text surface** (a `TMP_Text`) to the persistent loading-splash overlay that
  `SceneTransitionManager` already owns (it adopts the Bootstrap splash canvas and is
  `DontDestroyOnLoad`). Expose it via e.g. `SceneTransitionManager.SetOverlayMessage(string)` /
  `ClearOverlayMessage()`. Built reusable (any future loading tips ride the same surface), per the
  project's universality principle — not a shuffle-only hack.
- Between shuffle games, **each peer sets that text from its own local domain standings** (the same
  render as the end summary), so the running score is visible during the load. Because standings are
  identical on every peer (§2), no networking is needed to populate it.
- A **minimum hold** (~3s, configurable) guarantees the summary is visible even when the load is fast;
  the splash then clears on the next scene's ready/cinematic as usual.
- **Editor step (human):** add a `TMP_Text` child to the loading-splash overlay canvas and wire it to the
  new serialized field on `SceneTransitionManager` (steps in §12).

## 8. Data — `ShuffleDataSO`

A near-clone of `TournamentDataSO`, **keyed by Domain**. Authored (asset) fields:

- `GamePool : List<SO_ArcadeGame>` — the eligible modes (the 3 domain games for the MVP).
- `PointsByPlace : List<int> = {2, 1, 0}` — crystals by domain placement.
- `WinTarget = 6`, `MaxGames = 7`.
- `EndSceneName = "Shuffle"`, four `ScriptableEventNoParam`s (Started / GameResultRecorded /
  StandingsChanged / Completed).

Runtime (non-serialized): `IsActive`, `GamesPlayed`, `Standings : List<ShuffleDomainStanding>`
(per-domain cumulative crystals + per-game placement history), `ShuffleAINames`, `LastPlayedMode`
(repeat-avoidance). Methods: `RecordResults(results)`, `BuildSortedStandings()`, `IsComplete`,
`ResetRuntime()` — direct analogues of the Tournament data API.

## 9. Intro + end screen — `ShuffleSceneView` + `Shuffle.unity`

A near-clone of `TournamentSceneView` on a `Shuffle.unity` scene with two phase-selected layouts:

- **Intro** (fresh start): brief, **auto-advancing** banner ("SHUFFLE — first domain to 6 wins"). Unlike
  Tournament it shows **no fixed lineup** (the sequence is random). The intro's scene load is the clean,
  deterministic **per-peer "shuffle started"** signal (reset standings, `IsActive = true`,
  `IsShuffleMode = true`) — the same role Tournament's lobby load plays.
- **Summary** (after a domain wins / cap reached): winner domain + final per-domain standings, with
  host-only **Play Again** (new shuffle) and **Main Menu** (→ the shared `onClickToMainMenu` event →
  Menu_Main lava lamp).

> Open choice (§15): we could skip the intro and start straight into game 1, but keeping the brief intro
> reuses Tournament's proven deterministic start signal — **recommended**.

## 10. Differences from Tournament (authoritative delta)

| Aspect | Tournament | **Shuffle** |
|---|---|---|
| Lineup | Fixed 3 games (authored `GameQueue`) | **Randomized** draw from a pool (§3) |
| Length | Always 3 | **Variable** — race to 6, cap 7 (§2) |
| Per-game intensity | One intensity for all games (lobby) | **Random in [1..X]** per game; X = shuffle intensity (§3) |
| Points table | `{10, 6, 3, 1}` placement points | **`{2, 1, 0}`** placement crystals (§2) |
| Standings keyed by | **Player** (display name) | **Domain** (Jade/Ruby/Gold) (§2) |
| End condition | Play all 3, highest total | **First domain ≥ 6** (early stop) (§2) |
| Currency | Meta score only | **Real crystals** to the wallet (`AddCrystals`) (§2) |
| Between games | Host Continue → next | Host Continue → **loading-splash summary (~3s)** → next (§6,§7) |
| Intro scene | Shows fixed lineup | Brief banner, **no lineup** (random) (§9) |

Everything else — sequential `Single` loads, host-drives/clients-follow, network-free standings, AI roster
seeded once, eager DI singleton with static `Instance`, phase state machine, dual-layout scene, flag sync
to clients, `HasEndGame=false` per-game controllers — is **identical** to Tournament.

## 11. File index

| Role | File | New / Reuse |
|---|---|---|
| Mode enum | `_Scripts/Data/Enums/GameModes.cs` (`Shuffle = 37`) | edit |
| Config flag | `_Scripts/Utility/DataContainers/GameDataSO.cs` (`IsShuffleMode`) | edit |
| Data container | `_Scripts/Utility/DataContainers/Shuffle/ShuffleDataSO.cs` | **new** |
| State machine | `_Scripts/Controller/Arcade/Shuffle/ShuffleStateMachine.cs` | **new** |
| Controller (brain) | `_Scripts/Controller/Arcade/Shuffle/ShuffleController.cs` | **new** |
| Intro/summary view | `_Scripts/Controller/Arcade/Shuffle/ShuffleSceneView.cs` | **new** |
| End-game Continue + placement award | `_Scripts/UI/Scoreboard.cs` | edit |
| Loading-splash message surface | `_Scripts/System/Bootstrap/SceneTransitionManager.cs` | edit |
| Client flag sync | `_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs` | edit |
| Stable AI roster | `_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs` | reuse |
| DI registration | `_Scripts/System/AppManager.cs` | edit |
| Card unlock | `_Scripts/System/Progression/GameModeProgressionService.cs` | edit |
| Data asset | `_SO_Assets/Shuffle/ShuffleData.asset` (+ 4 `Event_Shuffle*.asset`) | **new** |
| Arcade card | `_SO_Assets/Games/ArcadeGameShuffle.asset` (in `GameLists/ArcadeGames.asset`) | **new** |
| Intro/summary scene | `_Scenes/Multiplayer Scenes/Shuffle.unity` | **new** |

## 12. Editor wiring (planned)

- **AppManager** — assign `ShuffleData.asset`; register + inject `ShuffleController` so it is created
  eagerly at bootstrap (mirror the Tournament rows).
- **`Shuffle.unity`** — UI-only scene with `ShuffleSceneView` driving the intro + summary roots
  (host-only buttons; `onClickToMainMenu` → the same `EventOnClickToMainMenuButton.asset` the Scoreboard
  uses). Register in Build Settings.
- **Loading-splash `TMP_Text`** — in the Bootstrap splash canvas (the overlay `SceneTransitionManager`
  adopts), add a `TMP_Text` child positioned for the summary, then wire it to the new serialized field on
  `SceneTransitionManager`. *(Human step — call it out in the commit that adds the field.)*
- **Scoreboard Continue** — already present on the shared end-game canvas (`GameCanvas-HexRace.prefab`,
  used by all three domain games); no new button, just the `IsShuffleMode` routing in code.
- **Arcade card** — add `ArcadeGameShuffle.asset` to `GameLists/ArcadeGames.asset` with
  `MinPlayersAllowed=2`, `MinDomainsAllowed=2` (a domain race needs ≥2 domains, same reasoning as the
  Tournament card).

## 13. Build order (commits)

1. **(this commit) Architecture doc** — `Docs/ShuffleSystem/ARCHITECTURE.md` + doc-index entries.
2. Data/enum/flag/DI: `GameModes.Shuffle = 37`, `GameDataSO.IsShuffleMode` + client sync, `ShuffleDataSO`
   (+ assets), AppManager registration.
3. Brain: `ShuffleController` + `ShuffleStateMachine` — random (mode, intensity ∈ [1..X]) draw, `{2,1,0}`
   per-domain fold, placement→wallet credit, race-to-6 / cap-7 win check.
4. Scoreboard `Continue` generalization (route to `ShuffleController` under `IsShuffleMode`).
5. Loading-splash message surface on `SceneTransitionManager` + shuffle pushes the domain summary + the
   ~3s minimum hold.
6. `ShuffleSceneView` + `Shuffle.unity` + arcade card + progression unlock.
7. Verification (§14) + an edit-mode test for the `{2,1,0}` domain fold and the race-to-6 / cap-7 check.

## 14. Verification

- **Solo + AI backfill:** Continue advances; the loading splash shows the running domain summary each
  game; a domain reaches 6 (or 7 games) → end summary with the winning domain; bot domains accrue across
  games.
- **2–4 players in MPPM:** clients show no host buttons; per-domain standings are **identical on every
  peer**; only the host drives loads; each peer credits its **own** local player's wallet once per game
  (no double-credit, no crediting remote players).
- **Flag hygiene:** a normal (non-shuffle) game after a shuffle session shows the standard Scoreboard
  buttons (`IsShuffleMode` cleared on Menu_Main return).
- **Edit-mode unit test:** `ShuffleDataSO.RecordResults` (domain order from `Results`, `{2,1,0}` award,
  history append) + `IsComplete` (race-to-6 and cap-7 boundaries).

## 15. Open / deferred

- **Intro vs no-intro** (§9) — recommend keeping the brief auto-advancing intro for the deterministic
  per-peer start signal.
- **Repeat-avoidance window** — avoid only the immediately-previous mode (proposed), or no repeats until
  the pool is exhausted?
- **Tie handling at the cap** — if two domains finish ≥6 on the same game (or the 7-game cap hits with a
  tie), break by higher total, then best single placement, then enum order (Jade→Ruby→Gold) for
  cross-peer determinism. Confirm the tiebreak ladder.
- **Economy interplay** beyond the crystal credit (bonuses, streaks) — deferred to the P8 economy spec,
  as with Tournament rewards.
