# Scoring System — Branch Changelog

Everything that landed on branch `claude/vibrant-brahmagupta-RaxVE` for the Scoring
System, newest theme last. Commit hashes are the post-rebase values on the branch.
Detailed records live in the sibling docs (`ARCHITECTURE.md`, `REFACTOR.md`,
`BUGS.md`, `TODOS.md`, `TESTS.md`); this file is the one-read overview for review/merge.

---

## 1. Scoring refactor — one `ScoringRuleSO` per mode (REFACTOR.md R10 + R1 residue)
Replaced ~12 per-mode subclasses (HUD / scoreboard / cinematic ×3 + helpers) with a
single per-mode strategy SO. The metric the player watches = the metric that ends the
game = the metric the scoreboard/cinematic show, **by construction**.

| Commit | What |
|---|---|
| `d3305e62`,`05b7c71e`,`ff69baf9`,`7478d0d9`,`a0776649` | R10 Phase A — HexRace/Joust/CrystalCapture each produce a ranked `List<ScoreResult>` (`ScoreText`/`Secondary`) + shared `FormatTime`. |
| `af07a171` | Per-mode `ScoringRuleSO` — end condition + winner/score routed through the rule. |
| `3014de71` | `MultiplayerHUD` reads `rule.LiveMetric` (HexRace HUD now shows Crystals); 3 HUD subclasses deleted. |
| `7ea5b8ae` | **Results SSOT** — scoreboard + end-game cinematic read `gameData.Results`; 6 subclasses deleted; **BUGS.md B2 closed**. |
| `21d538d3` | Joust replay → full network scene reload (matches HexRace/CrystalCapture). |
| `10e541fc` | `IsLocalUser` → `IsMultiplayerOwner` (no offline single-player branch). |
| `f1c29eff` | Docs: deferred **R11** (full `GolfScoreSentinels` retire) + **R12** (stats-provider fold) with designs pre-agreed. |

Smell scorecard: #2 HUD divergence, #3 scoreboard re-derivation, #4 cinematic
re-derivation, #5 SP/MP end-condition fork → fixed. #1 sentinels → resolved-by-
centralization (R4). #6 stats providers → deferred (R12).

## 2. End-game vessel podium — `BUGS.md B7`
| Commit | What |
|---|---|
| `fd0dee09` | `EndGameVesselDisplayManager` ranks from `gameData.Results` (golf-aware) instead of a local descending-`Score` sort that ranked the loser 1st. Last end-game surface still re-deriving rank → completes R10 Phase B. |

## 3. Multiplayer in-game domain HUD — client sync (the hard part)
Domain boxes were wrong on **clients** while correct on the host. The investigation
ran through several layers; the resolved root cause is the **dual-source domain**
(`BUGS.md` B8 → B10).

| Commit | What |
|---|---|
| `fa2515f7` | **B8** — reactive domain-panel rebuild (was a one-shot turn-start snapshot). |
| `e25290cc` | **B9 / "Approach B"** — server computes per-domain metric sums (`MultiplayerDomainGamesController` → `NetworkVariable` → `GameDataSO`); clients display them verbatim. |
| `352ed485` | **B10 attempt (superseded)** — tried writing `RoundStats.Domain` on all peers; didn't render (membership-blind reconcile + only-on-change). |
| `5442d3d0` | **B10 fix 1** — the HUD groups icons + `HasPlayersInDomain` off the authoritative `Player.Domain` (`gameData.Players`); reconcile is membership-aware (layout-signature hash). |
| `aaabc1b6` | **B10 fix 2** — retire `RoundStats.n_Domain`; `RoundStats.Domain` is a local mirror `Player` keeps in sync on every peer from `NetDomain`. One networked source of truth. |

**Root-cause lesson (verified via `Player.prefab`):** `RoundStats` is baked on the
SAME `NetworkObject` as `Player`, so there was never an owner-replication gap. Domain
was simply networked **twice** — `Player.NetDomain` (authoritative) and a derived
`RoundStats.n_Domain` that lagged a round-trip; the HUD mixed the two. Fixed by using
one source (`Player.NetDomain`) everywhere.

## 4. Architecture outcomes
- **Per-mode `ScoringRuleSO`** strategy is the single producer for end condition,
  HUD metric, ranked results, and reveal. (`ARCHITECTURE.md` §3–§5.)
- **One networked domain source:** `Player.NetDomain`. `Player.Domain` + `RoundStats.Domain`
  are local mirrors; `RoundStats.n_Domain` retired. (`ARCHITECTURE.md` §2 + domain section.)
- **Approach B** (server-synced domain sums) remains but is now **redundant** with the
  domain fixed at the source — optional retirement in `TODOS.md` TD3.

## 5. Scenes wired for the domain HUD (manual, by project owner)
`debb3239` + `4dc95258` (MinigameHexRace), `e57066b6` (MinigameCrystalCaptureMultiplayer),
`d2562539` (MinigameJoust_Gameplay).

## 6. Open items (carry into next session — `TODOS.md`)
- **TD1** — server-sync the LEGACY per-player HUD layout (or retire it via R6).
- **TD2** — confirm the Joust scene's domain-HUD wiring (`d2562539` exists; verify it has the ally/opposing containers + `DomainScorePanel`).
- **TD3** — play-test-confirm the domain fixes (B9 counts + B10 icon placement) across HexRace/CrystalCapture/Joust; then optionally retire Approach B.

**Not yet engine-tested:** all of the above is read-and-reason + edit work. The
multiplayer cases need a 2-human pass (`TESTS.md` T11/T12); the single-player surfaces
need a normal play-test pass before relying on them.

## 7. Doc index
`ARCHITECTURE.md` (design) · `REFACTOR.md` (R-backlog) · `BUGS.md` (B1–B10) ·
`TODOS.md` (TD1–TD3) · `TESTS.md` (T1–T12).
