# Shuffle — Pointer

**"Shuffle" is the player-facing display name of the existing Tournament meta-mode.** It is **not** a
separate game mode. The Arcade card asset `ArcadeGameTournament.asset` simply carries
`DisplayName = "Shuffle"` (rendered by `GameCard.GameTitle`). The code, scene (`Tournament.unity`),
data (`TournamentDataSO`), enum (`GameModes.Tournament = 36`), controller (`TournamentController`),
SO assets, and all wiring keep the **Tournament** name. *Shuffle and Tournament are the same thing.*

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
