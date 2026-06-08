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

### B2 — 🟢 Joust "jousts left" differs between reveal and scoreboard
The end-game **score reveal** computed a loser's remaining jousts as
**individual** (`needed - localStats.JoustCollisions`) while the **final
scoreboard** computed it as the **domain deficit**
(`needed - SumJoustCollisionsByDomain(domain)`), so in team games a losing player
could see one number on the reveal and a different one on the scoreboard.
Canonical = **domain deficit**. **Done** as part of `REFACTOR.md` R10 (commit
`7ea5b8ae`): both surfaces now read the rule's `ScoreResult` — `JoustScoringRuleSO`
builds the loser line as the domain deficit once (`BuildResults` + `BuildReveal`),
so they can't diverge. The per-mode `MultiplayerJoustEndGameController` /
`MultiplayerJoustScoreboard` overrides were deleted.

### B3 — 🟢 `scoreboardRowStagger` is dead config
`HUDAnimationSettingsSO.scoreboardRowStagger` was never read — `PlayerScoreCard`'s
entrance staggers by `cardEntranceStagger`, so tuning it did nothing. **Done**
(commit `b0d30871`): deleted the unused field. Pure removal, no behavior change.
File: `_Scripts/UI/HUDAnimationSettingsSO.cs`.

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

### B7 — 🟢 End-game vessel podium ranked golf modes backwards
`EndGameVesselDisplayManager.GatherVesselData` ranked players by `RoundStatsList`
sorted **descending by raw `Score`** — a second "who placed where" path that
ignored the rule-produced `gameData.Results`. For golf modes (HexRace, Joust) the
winner's `Score` is a small finish time and the loser's is the `10000+` sentinel,
so descending put the **loser 1st**: a solo HexRace win (5 crystals first) showed
the AI as "1st" and the human as "2nd", and the winner vessel icon
(`EndGameVesselDisplay` keys it off `ranking == 1`) went to the loser. **Done**
(commit `020d7b45`): the podium reads each player's rank from `gameData.Results`
(the SSOT every other end-game surface uses — golf-aware, ranked once by the mode's
`ScoringRuleSO`), keeping the legacy descending-Score sort only as a fallback for
modes that produce no Results (e.g. WildlifeBlitz). CrystalCapture (points) was
already correct and is unchanged. This was the last end-game surface still
re-deriving rank locally — completes `REFACTOR.md` R10 Phase B's consumer migration.
File: `_Scripts/Utility/DataContainers/EndGameVesselDisplayManager.cs`.

---

B1–B4, B6, B7 fixed (verify only — B6 also warrants a visual position check; B2/B7
need the HexRace/Joust end-game play-test). B5 remains scheduled into **R10** (the
unified ranked `ScoreResult` list dissolves it). No open read-through findings
remain.
