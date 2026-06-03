# Scoring System — Open Issues

Correctness issues found while documenting the Scoring System. These are
**candidates** surfaced from reading the source; confirm the repro before
fixing. Fix order follows `REFACTOR.md` discipline (one per commit, no
regressions). Read `ARCHITECTURE.md` first.

## Status legend
🔴 open · 🟡 partially mitigated · 🟢 fixed (verify only) · ⚪ deferred

---

### B1 — 🟢 `PlayerScoreCard` empty DataPanels background never re-hides
`ShowSecondaryStat` activated `dataPanelsRoot`, but `HideSecondaryStat` only hid
`secondaryStatText` — so the `DataPanels` background (it has a CanvasRenderer +
Image, defaults active) stayed visible behind cards with no extra stats. **Done**
(commit `47bf46c1`). A naive "also hide the root" is wrong: `crystalRewardRoot`
(`CrystalScore`) is a **child** of `dataPanelsRoot` (`DataPanels`) in the prefab,
so hiding the root would make a winner's "+N" reward invisible whenever they had
no secondary stat. Fix drives the shared root from **both** children via
`RefreshDataPanelsRoot()` (visible iff secondary stat OR crystal reward is
showing), called from all four Show/Hide methods so it's order-independent.
File: `_Scripts/UI/PlayerScoreCard.cs`.

### B2 — ⚪ Joust "jousts left" differs between reveal and scoreboard
The end-game **score reveal**
(`MultiplayerJoustEndGameController.PlayScoreRevealSequence`) computes a loser's
remaining jousts as **individual**: `needed - localStats.JoustCollisions`. The
**final scoreboard** (`MultiplayerJoustScoreboard.FormatPlayerScore`) computes
it as the **domain deficit**: `needed - SumJoustCollisionsByDomain(domain)`. In
team games a losing player can see one number on the reveal and a different one
on the scoreboard. Decide the canonical definition (domain deficit is consistent
with the domain-aggregated design) and use it in both places. Ties into
`REFACTOR.md` R4.
Files: `_Scripts/Utility/DataContainers/MultiplayerJoustEndGameController.cs`,
`_Scripts/UI/MultiplayerJoustScoreboard.cs`.

### B3 — ⚪ `scoreboardRowStagger` is dead config
`HUDAnimationSettingsSO.scoreboardRowStagger` is never read.
`Scoreboard.PopulatePlayerCards` passes the row index to
`PlayerScoreCard.Setup`, but `PlayerScoreCard.PlayEntrance` staggers by
`cardEntranceStagger`, not `scoreboardRowStagger`. Either wire the scoreboard
rows to `scoreboardRowStagger` or delete the field. Cosmetic, but it misleads
anyone tuning the entrance.
Files: `_Scripts/UI/HUDAnimationSettingsSO.cs`, `_Scripts/UI/Scoreboard.cs`,
`_Scripts/UI/PlayerScoreCard.cs`.

### B4 — 🟢 Two crystal-reward amounts can double-award / drift
The winner reward existed twice: `Scoreboard.winnerCrystalReward` (winner-only,
`AwardCrystalsIfLocalWinner`) and `EndGameCinematicController.crystalsPerGame`
(awarded with **no** winner check in `AwardCrystalReward`, skipped only while
`delegateCrystalRewardToScoreboard == true`). A scene flipping the flag to `false`
with `winnerCrystalReward > 0` would award the local winner twice (and the two
amounts could drift). **Resolved by `REFACTOR.md` R3** (commit `ed6517ab`): the
cinematic award path + flag were removed, leaving one value + one award path — the
double-award is now structurally impossible.
Files: `_Scripts/UI/Scoreboard.cs`,
`_Scripts/Utility/DataContainers/EndGameCinematicController.cs`.

### B5 — ⚪ Winner-delta recomputed independently per surface
The "won/lost by N" delta is computed independently inside each mode's
`EndGameCinematicController` and again (for ordering/format) in each scoreboard.
If a mode changes its scoring formula, the two can disagree. **Tracked by
`REFACTOR.md` R10** (one server-authoritative ranked results list) — centralize
result computation on the server so every surface reads the same ordered results.

### B6 — 🟢 Score-card secondary stat never renders (field unwired in prefab)
`secondaryStatText` was unassigned (`fileID: 0`) in the only score-card prefab
(`_Prefabs/UI Elements/In Game/PlayerScoreCard.prefab`), so the secondary line
that `HexRaceScoreboard` ("`N Crystals`") and `MultiplayerJoustScoreboard`
("`N Jousts`") feed through `Scoreboard.ShowMultiplayerView` → `ShowSecondaryStat`
was silently dropped. **Done** (commit `3aa3b5b7`): wired it to the existing
orphaned `TextMeshProUGUI` (`ScoreText`, placeholder "expand to view") under
`DataPanels` — sibling of `CrystalScore`, inactive by default. Data-only prefab
change; composes with B1's `RefreshDataPanelsRoot`. Verify the element's on-card
position visually in a HexRace/Joust end-game.

---

B1, B4 and B6 fixed (verify only — B6 also warrants a visual position check).
B2/B3/B5 are read-through findings — promote to 🔴 with a repro (and a
NetDiag/CSDebug log line where relevant) when picked up.
