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
(commit `fd0dee09`): the podium reads each player's rank from `gameData.Results`
(the SSOT every other end-game surface uses — golf-aware, ranked once by the mode's
`ScoringRuleSO`), keeping the legacy descending-Score sort only as a fallback for
modes that produce no Results (e.g. WildlifeBlitz). CrystalCapture (points) was
already correct and is unchanged. This was the last end-game surface still
re-deriving rank locally — completes `REFACTOR.md` R10 Phase B's consumer migration.
File: `_Scripts/Utility/DataContainers/EndGameVesselDisplayManager.cs`.

### B8 — 🟢 Client domain boxes built from a stale turn-start snapshot
In multiplayer the in-game domain boxes were wrong on **clients** (correct on the
host). `MultiplayerHUD.InitializeDomainPanels` built the panel set ONCE at
`OnMiniGameTurnStarted`, snapshotting `LocalPlayer.Domain`, each `stats.Domain`, and
`RequestedDomainCount`. On a client those replicate around turn start, so a late
arrival produced a wrong ally/opposing set that was never corrected — and
`RoundStats.n_Domain`'s replication callback raised **no** event, so the HUD never
learned a domain changed; updates routed by the live `stats.Domain` into the stale
panel dict were silently dropped. **Done** (commit `fa2515f7`): `RoundStats`
n_Domain/n_Name callbacks now raise `OnAnyStatChanged`; `MultiplayerHUD` rebuilds the
panel set reactively (idempotent `RebuildDomainPanels` + allocation-free
`DomainLayoutChanged`), reconciles on each stat event, and subscribes `OnPlayerAdded`
for late roster.
Files: `_Scripts/Data/Enums/RoundStats.cs`, `_Scripts/UI/MultiplayerHUD.cs`.

### B9 — 🟢 Client's OWN metric count frozen on its own screen (domain layout)
After B8, domains mapped correctly but a client's OWN crystal count stayed frozen
(e.g. stuck at 6) on the client while correct on the host; remote players' counts
replicated fine. Crystal counting is fully server-authoritative
(`OmniCrystalImpactor` bails on clients; `StatsManager` records server-only), so the
host is the source of truth — but a client re-summing its OWN per-player `RoundStats`
could freeze (owner-side replication of its own value proved unreliable; root cause
not isolated). **Done** (commit `e25290cc`, "Approach B"): clients no longer re-sum
per-player stats for the domain boxes. The server (`MultiplayerDomainGamesController`)
computes each active domain's `ScoringMetrics.SumByDomain(rule.Metric, …)` and
replicates it via 3 NetworkVariables; every peer mirrors it into
`GameDataSO.SetDomainMetricSum` and `MultiplayerHUD` displays it verbatim, so every
client matches the host. Generalizes to all three domain modes via the per-mode
`rule.Metric` (Crystals / Jousts). **Residual:** the LEGACY per-player layout still
reads per-player stats and is NOT covered — `TODOS.md` **TD1**. Needs the 2-human
play-test (HexRace / Joust / CrystalCapture — `TESTS.md` T11/T12).
Files: `_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs`,
`_Scripts/Utility/DataContainers/GameDataSO.cs`, `_Scripts/UI/MultiplayerHUD.cs`.

### B10 — 🟢 Client's OWN profile icon grouped into the wrong domain box
With 2 humans on different domains (host Jade, client Ruby), the client's screen
grouped BOTH players' icons into one domain box (the client's icon landed in the
host's box), while the host's screen grouped them correctly. Same owner-replication
gap as B9: the client's OWN `RoundStats.Domain` (a server-write NetworkVariable)
doesn't reach the owner, so it stayed stale (set once from a pre-sync `Player.Domain`
at init). `Player.OnNetDomainChanged` wrote `RoundStats.Domain` **only on the
server**, trusting `n_Domain` replication to carry it to clients — but the owner never
gets its own. `Player.Domain` (driven by the reliably-replicated `NetDomain`) WAS
correct, which is why the ally box + crystal counts (B9 server-synced) were already
right. **Done** (commit `eb798334`): `Player.OnNetDomainChanged` now writes
`RoundStats.Domain` on **every** peer, sourced from the reliable `NetDomain` instead
of the unreliable per-`RoundStats` `n_Domain`; the `RoundStats.Domain` setter raises
`OnAnyStatChanged` on a client/local set so the HUD regroups. The COUNT fix (B9) is
unaffected — sums are computed from the server's always-correct domains.
Files: `_Scripts/Controller/Player/Player.cs`, `_Scripts/Data/Enums/RoundStats.cs`.

---

B1–B4, B6, B7, B8 fixed (verify only — B6 also warrants a visual position check).
B9 (count) + B10 (domain icon placement) fixed for the **domain** layout (need the
2-human play-test; legacy-layout residual tracked in `TODOS.md` TD1). B5 remains
scheduled into **R10** (the unified ranked `ScoreResult` list dissolves it). No open
read-through findings remain.
