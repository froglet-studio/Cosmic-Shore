# Shuffle — Pointer

**This folder keeps a legacy name. The mode is Maelstrom, and so is the code.**

It was called *Shuffle* to players and `Tournament` in code. The player-facing name became
**Maelstrom** first; the code caught up in the 2026-09 naming pass, which renamed the enum member,
the controller, the data SO, the assets and this documentation to match. Enum VALUES were pinned
throughout (`GameModes.Maelstrom = 36`), and `GameModeRenameMigration` translates the cloud-save
keys that stored the old member NAME.

So there is no longer a split to explain: `MaelstromController`, `MaelstromDataSO`,
`ArcadeGameMaelstrom.asset` and `Maelstrom.unity` all say the same word the player sees. **The
folder name is the only thing left that does not** — it is kept deliberately, per the rule that
renaming a folder to match a live feature is how two things come to look like one.

The real content lives in **`Docs/MaelstromSystem/ARCHITECTURE.md`**; this file is a pointer.

## Renaming the mode (player-facing name) — single source of truth

The player-facing name lives in **exactly one place**: the Arcade card asset
`Assets/_SO_Assets/Games/ArcadeGameMaelstrom.asset` → **`DisplayName`** (currently `Maelstrom`).

**To rename the mode for players, change only that one field.** It drives both surfaces automatically:

| Surface | How it reads the name |
|---|---|
| Arcade grid card | `GameCard` sets `GameTitle.text = game.DisplayName` (`_Scripts/UI/Elements/GameCard.cs`) |
| In-scene lobby + summary title | `MaelstromSceneView.ModeName` reads `MaelstromDataSO.ModeCard.DisplayName`, upper-cased for the banner (`"SHUFFLE"` / `"SHUFFLE RESULTS"`). The `ModeCard` field of `MaelstromData.asset` is wired to `ArcadeGameMaelstrom.asset`; if it is ever unwired the title falls back to `"Maelstrom"`. |

So the card's `DisplayName` is the **single source** — the in-scene title is no longer a separate
hardcoded string (it was `"TOURNAMENT"` before; now it tracks the card).

**Do NOT change these to rename** — they are *internal identifiers*, not player-facing, and renaming
them is risky GUID / scene / build-settings surgery (out of scope unless explicitly requested):

- `GameModes.Maelstrom = 36` (enum symbol + int)
- `MaelstromController` · `MaelstromStateMachine` · `MaelstromDataSO` · `MaelstromSceneView` (classes/files)
- `Maelstrom.unity` (scene file — already renamed once in the v2 rework; don't rename it again to change
  the display name) · `MaelstromData.asset` + `_SO_Assets/Maelstrom/` (data assets) ·
  the `ArcadeGameMaelstrom.asset` *file* name (only its `DisplayName` **field** changes)
- `IsMaelstromMode` (the GameDataSO flag) · the `CosmicShore.*` namespaces

**Separate field — the card `Description`:** its own blurb (`ArcadeGameMaelstrom.asset` → `Description`),
not derived from `DisplayName`. Edit it directly if the copy needs updating. It is intentionally
**mode-count-agnostic** ("a lineup of games", not "three games") because more modes will be added.

> **TL;DR for "rename the mode to X":** set `ArcadeGameMaelstrom.asset` `DisplayName = X`
> (and optionally `Description`). Nothing else.

## Canonical architecture → Maelstrom docs

For everything about how this meta-mode works — the sequential `Single`-load model, the persistent
network-free `MaelstromController` brain, standings folded from the synced `GameDataSO.Results`, the
host-only Continue→Summary flow, the data container, file index, and editor wiring — see:

> **`Docs/MaelstromSystem/ARCHITECTURE.md`**

If you came here looking for "Shuffle," that is the document you want.

## Planned Shuffle-specific behavior (deferred — not yet built)

These are the behavior changes that distinguish the **Shuffle** card from the original Maelstrom.
They are **extensions of the existing Maelstrom infrastructure** — they modify the `Maelstrom*`
classes; **no new mode, enum, or classes**. Status is per-row:

| # | Delta | Where it lands | Status |
|---|---|---|---|
| 1 | **Randomized lineup** — a random mode + a random intensity ∈ [1..X] per game (X = chosen intensity; pool = N modes × X "experiences" — N is the authored `GameQueue` length, 7 today, so L1=7 … L4=28), with repeat-avoidance, instead of the fixed `GameQueue` | `MaelstromDataSO`, `MaelstromController` | ✅ **shipped** |
| 2 | **Per-domain scoring `{2,1,0}`** (1st/2nd/3rd domain) instead of per-player `{10,6,3,1}`; standings keyed by `Domain`. Placement is the mode rule's **team-total order** (`ScoringRuleSO.ResolvePlacementOrder` — summed metric per domain, the same aggregation that ends the turn and picks `WinnerDomain`), passed into `RecordResults` by `MaelstromController`; the results-only best-player-`Rank` reduction remains only as a fallback (it mis-placed teams whenever a losing team's player tied the top individual score). The **last-placed** domain of a round always earns the table's last entry (0) whatever the domain count — a 2-domain game pays `{2,0}`, so losing never pays toward the race target | `MaelstromDataSO`, `ScoringRuleSO.ResolvePlacementOrder` | ✅ **shipped** |
| 3 | **Race to 6 / cap 7 games** (`WinTarget`/`MaxGames`, `IsShuffleComplete`) instead of "played all N" | `MaelstromController`, `MaelstromDataSO` | ✅ **shipped** |
| 4 | **Real crystal-wallet credit** of the `{2,1,0}` to each local player by their domain's per-game placement (generalized from the winner-only flat reward; cards also show the per-domain badge). Reads `MaelstromDataSO.CrystalsForDomain` via an **injected** `MaelstromDataSO` — no static singleton reach-through | `Scoreboard.AwardCrystalsToLocalPlayer` / `CardCrystalReward`, `MaelstromDataSO.CrystalsForDomain` | ✅ **shipped** |
| 5 | **Between-game summary on the splash (SOAP).** Reuses the existing `BootStatusPanel` view + `Event_BootStatusRequest` channel: `BootStatusBroadcaster.HandleLaunchGame` raises the running standings (`MaelstromStandingsFormatter.FormatRunning`) during a shuffle inter-game load instead of its usual `Hide`; its existing `HandleClientReady`→`Hide` clears it. `SceneTransitionManager` owns **only** fades (no `TMP_Text`). | `BootStatusBroadcaster`, `MaelstromStandingsFormatter`, `BootStatusPanel` (reused) | ✅ **shipped** (needs `tournamentData` wired — below) |

All five deltas are now **shipped**; the canonical `Docs/MaelstromSystem/ARCHITECTURE.md` documents them.

**Editor steps (one-time wiring):**
- **#5 summary:** wire `MaelstromData.asset` into `BootStatusBroadcaster.tournamentData` (on the splash
  canvas). The running standings then render on the existing `BootStatusPanel.statusText` during shuffle
  inter-game loads — no new object, and **nothing on `SceneTransitionManager`** (it's pure fades now).
- **#4 reward:** wire `MaelstromData.asset` into each domain-game `Scoreboard.tournamentData`
  (`GameCanvas-SkimRace.prefab` + the scene-added Scoreboards in Joust / Crystal Capture).
- Both degrade gracefully if unwired (clean splash / flat winner reward).
