# Shuffle — Pointer

**"Shuffle" is the player-facing display name of the existing Tournament meta-mode.** It is **not** a
separate game mode. The Arcade card asset `ArcadeGameTournament.asset` simply carries
`DisplayName = "Shuffle"` (rendered by `GameCard.GameTitle`). The code, scene (`Tournament.unity`),
data (`TournamentDataSO`), enum (`GameModes.Tournament = 36`), controller (`TournamentController`),
SO assets, and all wiring keep the **Tournament** name. *Shuffle and Tournament are the same thing.*

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
- `Tournament.unity` (scene) · `TournamentData.asset` + `_SO_Assets/Tournament/` (data assets) ·
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

These are the behavior changes that will eventually distinguish the **Shuffle** card from today's
Tournament. They are **extensions of the existing Tournament infrastructure** — they modify the
`Tournament*` classes; **no new mode, enum, or classes** — and are **not implemented yet**:

| # | Delta | Where it lands |
|---|---|---|
| 1 | **Randomized lineup** — a random mode + a random intensity ∈ [1..X] per game (X = chosen intensity; pool = 3 modes × X "experiences", L1=3 … L4=12), with repeat-avoidance, instead of the fixed `GameQueue` | `TournamentDataSO`, `TournamentController` |
| 2 | **Per-domain scoring `{2,1,0}`** (1st/2nd/3rd domain) instead of per-player `{10,6,3,1}`; standings keyed by `Domain`, placement derived from the synced `GameDataSO.Results` | `TournamentDataSO` |
| 3 | **Race to 6 / cap 7 games** instead of "played all N" | `TournamentController`, `TournamentDataSO` |
| 4 | **Real crystal-wallet credit** of the `{2,1,0}` to each local player (generalize the winner-only flat reward) | `Scoreboard.AwardCrystalsIfLocalWinner` |
| 5 | **Loading-splash summary (~3s)** between games — a reusable text surface fed the running domain standings | `SceneTransitionManager` (net-new surface) |

When these land, fold the detail into the canonical `Docs/TournamentSystem/ARCHITECTURE.md` rather than
growing this pointer.
