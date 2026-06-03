# Scoring System — Open Issues

Correctness issues found while documenting the Scoring System. These are
**candidates** surfaced from reading the source; confirm the repro before
fixing. Fix order follows `REFACTOR.md` discipline (one per commit, no
regressions). Read `ARCHITECTURE.md` first.

## Status legend
🔴 open · 🟡 partially mitigated · 🟢 fixed (verify only) · ⚪ deferred

---

### B1 — 🟡 `PlayerScoreCard` secondary-stat panel never re-hides its root
`ShowSecondaryStat` activates both `secondaryStatText` **and** `dataPanelsRoot`,
but `HideSecondaryStat` only deactivates `secondaryStatText` — it leaves
`dataPanelsRoot` active. Cards are recreated each game so it rarely shows, but
the asymmetry is wrong. Fix: `HideSecondaryStat` should also deactivate
`dataPanelsRoot` (or the card should manage the root from a single place).
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

### B4 — ⚪ Two crystal-reward amounts can double-award / drift
The winner reward exists twice: `Scoreboard.winnerCrystalReward` (awarded in
`AwardCrystalsIfLocalWinner`) and `EndGameCinematicController.crystalsPerGame`
(awarded in `AwardCrystalReward`, skipped only while
`delegateCrystalRewardToScoreboard == true`). If a scene sets
`delegateCrystalRewardToScoreboard = false` **and** leaves
`winnerCrystalReward > 0`, the local winner is awarded twice; the two amounts
can also drift out of sync. Single source of truth — `REFACTOR.md` R3.
Files: `_Scripts/UI/Scoreboard.cs`,
`_Scripts/Utility/DataContainers/EndGameCinematicController.cs`.

### B5 — ⚪ Winner-delta recomputed independently per surface
The "won/lost by N" delta is computed independently inside each mode's
`EndGameCinematicController` and again (for ordering/format) in each scoreboard.
If a mode changes its scoring formula, the two can disagree. **Tracked by
`REFACTOR.md` R10** (one server-authoritative ranked results list) — centralize
result computation on the server so every surface reads the same ordered results.

---

No 🔴 confirmed-reproduced bugs yet — the above are read-through findings.
Promote to 🔴 with a repro (and a NetDiag/CSDebug log line where relevant) when
picked up.
