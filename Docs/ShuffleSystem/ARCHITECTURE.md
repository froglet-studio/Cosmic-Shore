# Shuffle — Pointer

**"Maelstrom" is the player-facing display name of the existing Tournament meta-mode** (this doc folder
keeps the legacy "Shuffle" name — the mode was renamed for players). It is **not** a separate game mode.
The Arcade card asset `ArcadeGameTournament.asset` simply carries `DisplayName = "Maelstrom"` (rendered
by `GameCard.GameTitle`). The code, data (`TournamentDataSO`), enum (`GameModes.Tournament = 36`),
controller (`TournamentController`), SO assets, and all wiring keep the **Tournament** name — the **scene
file** was renamed to `Maelstrom.unity` in the v2 rework (only the file name changed).
*Maelstrom and Tournament are the same thing.*

## Renaming the mode (player-facing name) — single source of truth

The player-facing name lives in **exactly one place**: the Arcade card asset
`Assets/_SO_Assets/Games/ArcadeGameTournament.asset` → **`DisplayName`** (currently `Shuffle`).

**To rename the mode for players, change only that one field.** It drives both surfaces automatically:

| Surface | How it reads the name |
|---|---|
| Arcade grid card | `GameCard` sets `GameTitle.text = game.DisplayName` (`_Scripts/UI/Elements/GameCard.cs`) |
| In-scene lobby + summary title | `TournamentSceneView.ModeName` reads `TournamentDataSO.ModeCard.DisplayName`, upper-cased for the banner (`"SHUFFLE"` / `"SHUFFLE RESULTS"`). The `ModeCard` field of `TournamentData.asset` is wired to `ArcadeGameTournament.asset`; if it is ever unwired the title falls back to `"Tournament"`. |

So the card's `DisplayName` is the **single source** — the in-scene title is no longer a separate
hardcoded string (it was `"TOURNAMENT"` before; now it tracks the card).

**Do NOT change these to rename** — they are *internal identifiers*, not player-facing, and renaming
them is risky GUID / scene / build-settings surgery (out of scope unless explicitly requested):

- `GameModes.Tournament = 36` (enum symbol + int)
- `TournamentController` · `TournamentStateMachine` · `TournamentDataSO` · `TournamentSceneView` (classes/files)
- `Maelstrom.unity` (scene file — already renamed once in the v2 rework; don't rename it again to change
  the display name) · `TournamentData.asset` + `_SO_Assets/Tournament/` (data assets) ·
  the `ArcadeGameTournament.asset` *file* name (only its `DisplayName` **field** changes)
- `IsTournamentMode` (the GameDataSO flag) · the `CosmicShore.*` namespaces

**Separate field — the card `Description`:** its own blurb (`ArcadeGameTournament.asset` → `Description`),
not derived from `DisplayName`. Edit it directly if the copy needs updating. It is intentionally
**mode-count-agnostic** ("a lineup of games", not "three games") because more modes will be added.

> **TL;DR for "rename the mode to X":** set `ArcadeGameTournament.asset` `DisplayName = X`
> (and optionally `Description`). Nothing else.

## Canonical architecture → Tournament docs

For everything about how this meta-mode works — the sequential `Single`-load model, the persistent
network-free `TournamentController` brain, standings folded from the synced `GameDataSO.Results`, the
host-only Continue→Summary flow, the data container, file index, and editor wiring — see:

> **`Docs/TournamentSystem/ARCHITECTURE.md`**

If you came here looking for "Shuffle," that is the document you want.

## Planned Shuffle-specific behavior (deferred — not yet built)

These are the behavior changes that distinguish the **Shuffle** card from the original Tournament.
They are **extensions of the existing Tournament infrastructure** — they modify the `Tournament*`
classes; **no new mode, enum, or classes**. Status is per-row:

| # | Delta | Where it lands | Status |
|---|---|---|---|
| 1 | **Randomized lineup** — a random mode + a random intensity ∈ [1..X] per game (X = chosen intensity; pool = N modes × X "experiences" — N is the authored `GameQueue` length, 7 today, so L1=7 … L4=28), with repeat-avoidance, instead of the fixed `GameQueue` | `TournamentDataSO`, `TournamentController` | ✅ **shipped** |
| 2 | **Per-domain scoring `{2,1,0}`** (1st/2nd/3rd domain) instead of per-player `{10,6,3,1}`; standings keyed by `Domain`. Placement is the mode rule's **team-total order** (`ScoringRuleSO.ResolvePlacementOrder` — summed metric per domain, the same aggregation that ends the turn and picks `WinnerDomain`), passed into `RecordResults` by `TournamentController`; the results-only best-player-`Rank` reduction remains only as a fallback (it mis-placed teams whenever a losing team's player tied the top individual score). The **last-placed** domain of a round always earns the table's last entry (0) whatever the domain count — a 2-domain game pays `{2,0}`, so losing never pays toward the race target | `TournamentDataSO`, `ScoringRuleSO.ResolvePlacementOrder` | ✅ **shipped** |
| 3 | **Race to 6 / cap 7 games** (`WinTarget`/`MaxGames`, `IsShuffleComplete`) instead of "played all N" | `TournamentController`, `TournamentDataSO` | ✅ **shipped** |
| 4 | **Real crystal-wallet credit** of the `{2,1,0}` to each local player by their domain's per-game placement (generalized from the winner-only flat reward; cards also show the per-domain badge). Reads `TournamentDataSO.CrystalsForDomain` via an **injected** `TournamentDataSO` — no static singleton reach-through | `Scoreboard.AwardCrystalsToLocalPlayer` / `CardCrystalReward`, `TournamentDataSO.CrystalsForDomain` | ✅ **shipped** |
| 5 | **Between-game summary on the splash (SOAP).** Reuses the existing `BootStatusPanel` view + `Event_BootStatusRequest` channel: `BootStatusBroadcaster.HandleLaunchGame` raises the running standings (`TournamentStandingsFormatter.FormatRunning`) during a shuffle inter-game load instead of its usual `Hide`; its existing `HandleClientReady`→`Hide` clears it. `SceneTransitionManager` owns **only** fades (no `TMP_Text`). | `BootStatusBroadcaster`, `TournamentStandingsFormatter`, `BootStatusPanel` (reused) | ✅ **shipped** (needs `tournamentData` wired — below) |

All five deltas are now **shipped**; the canonical `Docs/TournamentSystem/ARCHITECTURE.md` documents them.

**Editor steps (one-time wiring):**
- **#5 summary:** wire `TournamentData.asset` into `BootStatusBroadcaster.tournamentData` (on the splash
  canvas). The running standings then render on the existing `BootStatusPanel.statusText` during shuffle
  inter-game loads — no new object, and **nothing on `SceneTransitionManager`** (it's pure fades now).
- **#4 reward:** wire `TournamentData.asset` into each domain-game `Scoreboard.tournamentData`
  (`GameCanvas-HexRace.prefab` + the scene-added Scoreboards in Joust / Crystal Capture).
- Both degrade gracefully if unwired (clean splash / flat winner reward).
