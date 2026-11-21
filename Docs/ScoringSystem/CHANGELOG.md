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
| `d3305e62`,`05b7c71e`,`ff69baf9`,`7478d0d9`,`a0776649` | R10 Phase A — SkimRace/Joust/Scurry each produce a ranked `List<ScoreResult>` (`ScoreText`/`Secondary`) + shared `FormatTime`. |
| `af07a171` | Per-mode `ScoringRuleSO` — end condition + winner/score routed through the rule. |
| `3014de71` | `MultiplayerHUD` reads `rule.LiveMetric` (SkimRace HUD now shows Crystals); 3 HUD subclasses deleted. |
| `7ea5b8ae` | **Results SSOT** — scoreboard + end-game cinematic read `gameData.Results`; 6 subclasses deleted; **BUGS.md B2 closed**. |
| `21d538d3` | Joust replay → full network scene reload (matches SkimRace/Scurry). |
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
`debb3239` + `4dc95258` (MinigameSkimRace), `e57066b6` (MinigameScurryMultiplayer),
`d2562539` (MinigameJoust_Gameplay).

## 6. Open items (carry into next session — `TODOS.md`)
- **TD1** — server-sync the LEGACY per-player HUD layout (or retire it via R6).
- **TD2** — confirm the Joust scene's domain-HUD wiring (`d2562539` exists; verify it has the ally/opposing containers + `DomainScorePanel`).
- **TD3** — play-test-confirm the domain fixes (B9 counts + B10 icon placement) across SkimRace/Scurry/Joust; then optionally retire Approach B.

**Not yet engine-tested:** all of the above is read-and-reason + edit work. The
multiplayer cases need a 2-human pass (`TESTS.md` T11/T12); the single-player surfaces
need a normal play-test pass before relying on them.

## 7. Doc index
`ARCHITECTURE.md` (design) · `REFACTOR.md` (R-backlog) · `BUGS.md` (B1–B14) ·
`TODOS.md` (TD1–TD3) · `TESTS.md` (T1–T14).

---

## Addendum — Play Again + scoreboard nav gating (branch `claude/amazing-cray-6uqq62`, 2026-06-12)

Follow-up session: the scene-reload replay shipped in `21d538d3` turned out to be
unreachable from the UI in two of the three domain modes — a scene-wiring defect,
not a code one. **Owner-verified in engine** (Joust Play Again loop + button
gating), unlike the read-and-reason caveat above.

| Commit | What |
|---|---|
| `e21c778a` | **BUGS.md B13** — scoreboard Play Again was dead in Joust + Crystal Capture: both scenes remove the GameCanvas-SkimRace prefab's internal `Scoreboard` and add their own, but the prefab `PlayAgainButton.onClick` still targeted the internal one (Joust: explicit null override; CC: removed component) — null-target persistent calls are silently skipped. Retargeted the onClick at the scene-added Scoreboard in both scenes. SkimRace was never affected (keeps the internal Scoreboard, overrides only `gameController`). |
| `3a021e50` | **BUGS.md B14** — host-only nav gating (`ConfigureLobbyButtons`) silently no-oped: the prefab predates the `playAgainButton`/`mainMenuButton` fields, so they were null in every scene and clients saw both buttons. Wired both (+ new `onClickToMainMenu` event field) in all three domain scenes; added `Scoreboard.HideHostNavButtons()` — Play Again hides both buttons on click, Main Menu hides them when `EventOnClickToMainMenuButton` fires (post host-guard), so the host can't spam navigation during the transition. |

Docs: `JOUST.md` §9 rewritten for the scene-reload replay (+ wiring requirements);
`SCURRY.md` §9 / `SKIMRACE.md` §10 updated (rematch flow + deleted per-mode
scoreboard/end-game subclasses scrubbed); `TESTS.md` T8 updated, T13 (replay loop)
+ T14 (nav gating) added.

---

## End-game cinematic removed — `Scoreboard` is the sole end-game UI
The end-game cinematic was deleted; the **`Scoreboard`** is now the only end-game
surface. A slim `EndGameSequencer` (`_Scripts/Utility/DataContainers/`) replaces the
~430-line `EndGameCinematicController`: on `OnWinnerCalculated` it halts every vessel
(`VesselStatus.IsStationary = true` + input pause — nothing flies behind the scoreboard),
plays the GameEnd SFX, then raises `OnShowGameEndScreen` (the existing signal the
`Scoreboard` and `LifeForm` ecology cleanup already consume). The two end-of-game
progression toasts (quest complete / intensity unlocked) are re-homed off
`GameModeProgressionService` events.

Deleted: `EndGameCinematicController`, `EndGameCinematicView`,
`WildlifeBlitzEndGameCinematicController`, `EndGameVesselDisplay`(+`Manager`),
`CinematicCameraController`, `CinematicDefinitionSO`, `SceneCinematicLibrarySO`,
`VesselIconLibrarySO` + the `_SO_Assets/Cinematics/` assets. The controller component
was GUID-swapped to `EndGameSequencer` in the Joust/Scurry/WildlifeBlitz scenes,
`GameCanvas-SkimRace.prefab` and `EndGameStatsPanel.prefab` (preserving the `gameData`
wiring). `ScoringRuleSO.BuildReveal` is retained (abstract) but now unconsumed.

**Editor follow-up:** delete the now-dead cinematic UI GameObjects (score-reveal panel,
vessel podium, cinematic camera) left as missing-scripts in those scenes/prefabs.
