# Scoring System — Refactor Backlog

Sequenced cleanup + improvement items for the Scoring System (in-game score HUD
+ final scoreboard). Read `ARCHITECTURE.md` first.

## Ground rules (non-negotiable)

These come from the project owner and govern **every** item below:

- **SOAP-first + observer.** Cross-system communication uses SOAP
  `ScriptableEvent` channels + `ScriptableVariable`/data containers. No
  singletons, static events, `FindObjectOfType`, or direct
  MonoBehaviour-to-MonoBehaviour references. No if-null guards on SOAP event
  fields (fail loud). No per-frame polling — react to events.
- **SOLID / DRY / KISS.** One responsibility per type; deduplicate; simplest
  design that works.
- **One unified, always-networked scoring path.** Solo = host + AI;
  domain-aggregated + RPC-synced scoring for solo-host and online alike. No
  single-player-only branches, no `IsMultiplayerMode` forking.
- **Clean out legacy, don't duplicate it.** Remove dead code/docs as we touch
  them rather than leaving them beside the new path.
- **Incremental, no-regression.** One small item per commit; **design agreed in
  this doc first**; isolated, verified change; no temporary fixes, no new code
  smells, no feature breakage. (Same per-commit discipline as
  `Docs/PartySystem/REFACTOR.md`.)

## Status legend
🔴 open · 🟡 partially done · 🟢 done (verify only) · ⚪ deferred

---

## Open design questions (agree before coding)

### Q1 — Unify on the always-networked model; retire `IsMultiplayerMode`
The game always runs as a network host, so a solo game is host + AI. We want
solo-host and online to render scores through the **identical**
domain-aggregated, RPC-synced path. Before writing code we must agree, per
call-site (see `ARCHITECTURE.md` §8 fork map):

- What replaces each `IsMultiplayerMode` read? Candidate signals:
  `NetworkManager.ConnectedClientsIds.Count`, domain membership, `WinnerDomain`,
  or an explicit SOAP state — **not** a player-count boolean.
- `Arcade.cs:107` currently sets the flag `false` for a 1-player game. Under the
  target, what (if anything) distinguishes solo-host presentation (e.g. lobby
  buttons: a solo host still owns Play Again / Main Menu, never "Leave Lobby")?
- Migration order so nothing breaks mid-refactor (data → controllers → UI →
  flag removal).

Output of this discussion becomes item **R1**.

---

## Backlog (sequenced)

### R1 — 🔴 [discuss-first] Remove `IsMultiplayerMode` forking → unified path
Route all scoring/lobby/cinematic behavior through the always-networked host
model; drive per-site behavior off concrete signals (§8) and delete the flag.
Works *through* the **Domain + scoring** fundamentals (consolidation, not a new
system). **Touches** winner calc, score sync, HUD layout selection, lobby
buttons — gated on **Q1** sign-off. Ship in small steps (one fork site / small
group per commit).

### R2 — 🟢 Deduplicate the score-text animation (DRY) — incl. R2b (entrance)
`PlayCounterRoll` / `PlayScorePunch` / `PlayColorFlash` / entrance were
copy-pasted across `PlayerScoreEntry`, `PlayerScoreCard`, and `DomainScorePanel`
(~80 lines ×3). **Done** (commit `3f708a5b`):
- `ScoreNumberAnimator` (new plain C# helper) owns the counter-roll + punch +
  color-flash trio, the displayed value, the base color, and the three tweens —
  composed by all three widgets.
- `CardEntranceAnimator` (new static helper) owns the staggered scale+fade
  entrance shared by the two card widgets (this is R2b, folded in).
Behavior-preserving — all public APIs unchanged, no caller/prefab changes; net
−80 lines. `DomainScorePanel` no longer depends on `DG.Tweening` directly; its
themed base-color path is preserved via `ScoreNumberAnimator.SetBaseColor`.

### R3 — 🟢 Single source of truth for the winner crystal reward
`Scoreboard.winnerCrystalReward` and `EndGameCinematicController.crystalsPerGame`
were independent, hand-synced values joined by an implicit
`delegateCrystalRewardToScoreboard` handshake. **Done** (commit `ed6517ab`): the
cinematic award path was dead in all shipped content (the delegate flag is `true`
in every scene/prefab), so it was removed — `crystalsPerGame`, `crystalRewardText`,
`crystalFadeDuration`, `delegateCrystalRewardToScoreboard`, the `AwardCrystalReward`
coroutine + its call. `Scoreboard.winnerCrystalReward` + `AwardCrystalsIfLocalWinner`
(winner-only) is now the lone value + award path. `crystalRewardRoot` and its
`OnEnable` hide were kept so scene-authored legacy reward UI (e.g. SkimRace's
active `CrystalDisplayBG`) stays hidden. Behavior-preserving; closes `BUGS.md` B4.

### R4 — 🟢 Centralize the loser-score sentinel encode/decode
SkimRace (`10000 + crystalsLeft`) and Joust (`99999`) encoded loser scores as
magic numbers decoded by duplicated literals — and SkimRace had **two** encode
sites that could drift (controller literal vs `SkimRaceScoreTracker.penaltyScoreBase`).
**Done** (commit `68550228`): new static `GolfScoreSentinels` (`CosmicShore.Gameplay`)
holds the constants (`DnfThreshold`, `SkimRaceLoserBase`, `JoustLoserScore`) +
helpers (`Encode/DecodeSkimRaceCrystalsLeft`, `IsSkimRaceLoserScore`,
`IsJoustLoserScore`, `IsFinishTime`). Migrated every write (SkimRaceController,
SkimRaceScoreTracker, JoustController) and read (SkimRaceScoreboard,
SkimRaceEndGameController, JoustScoreboard) — **plus** the same-sentinel
DNF threshold in `UGSStatsManager` + `GameModeProgressionService` (the literals
there were the real drift hazard). Removed the drift-prone `penaltyScoreBase`
serialized field (its only scene value equalled the constant). Behavior-preserving
— every rewrite is algebraically identical to the literal it replaced.

### R5 — 🟢 Unify domain → color resolution
Three paths (`ThemeManagerData.ColorSet` / `DomainColorPaletteSO` /
`MiniGameHUDView.domainColors`) plus **5 hardcoded banner `Color` fields** in
`Scoreboard`. **Done** (commit `53b973b5`): the scoring UI now resolves domain
color from the single canonical source vessels and prisms already use —
`GameDataSO.ThemeManagerData.ColorSet` — via `SO_ColorSet.GetDomainUIColor(domain)`
→ `TrailHighlightColor` (+ a null-safe `ThemeManagerDataContainerSO` wrapper).
Removed the Scoreboard banner-color fields + the `DomainColorPaletteSO` field, and
the `MiniGameHUDView.domainColors` list/`DomainColorDef`/`GetColorForDomain`; the
HUD controllers now share `MiniGameHUD.ResolveDomainColor` (domain panels already
read the theme ColorSet — only the player cards used the list). Also fixed a real
mismatch: the hardcoded banner showed Jade=green / Ruby=red, but the theme's Jade
is teal and Ruby magenta — banner/cards now match the vessels on screen.

**App-wide single-source — DONE** (commits `f957dc0d`, `96bb5fbb`): the remaining
sources outside the scoring UI were migrated to `ThemeManagerData.ColorSet` too.
Game feed: `GameEventFeed` resolves via injected `gameData`; the static
`GameFeedAPI` hardcoded dict is replaced by an `SO_ColorSet` handed to it by
`ThemeManager` at game start. (The feed has since been superseded by the
config-driven toast system — `GameToastAPI` / `GameToastController` in
`_Scripts/UI/GameToastSystem/` — which inherits the same single-source rule.) Vessel HUD: `SquirrelVesselHUDController` and
`SilhouetteController` `[Inject] GameDataSO` and resolve via `GetDomainUIColor`
(silhouette danger → shared `EnvironmentColors.Danger`); their `DomainColorPaletteSO`
fields (`domainColors`, `SilhouetteConfigSO.domainPalette`) were removed.

**Regression + re-unification (Unified Systems S0.1).** After the paragraph above
declared `DomainColorPaletteSO` consumer-free, the Maelstrom/Connecting UI shipped
reading it again (six consumers: `MaelstromSceneView` — which *preferred* the
palette over the theme — `MaelstromPlayerCard`, `MaelstromSummaryPlayerCard`,
`MaelstromDomainScoreView`, `MaelstromRoundCard`, `ConnectingPanelController`).
Those tints were **intentional** (per `MAELSTROM_REWORK_SPEC.md` v2.1: brighter
hues, uniform 0.784 alpha for translucent card backgrounds), so rather than
flattening them they were folded into `SO_ColorSet` as a named role:
`DomainColorSet.UIAccentColor` + `SO_ColorSet.GetDomainUIAccentColor` (falls back
to `GetDomainUIColor` when unauthored — alpha 0). All six consumers now read the
theme (`GameDataSO.ThemeManagerData`), `DomainColorPaletteSO` + its `.asset` are
**deleted**, and the orphaned `domainPalette`/`domainColorPalette` refs in the 5
SilhouetteConfig assets + 5 game scenes were stripped. AstroLeague's inline
palettes went config-side too (`AstroLeagueSettingsSO.goldGoalColor` added;
`AstroLeagueBall`/`AstroLeagueArena` literals removed). Net result: every domain
color (banner, cards, HUD, feed, silhouette, vessels, prisms, Maelstrom accent)
reads one `SO_ColorSet` — the accent's intentional divergence from
`TrailHighlightColor` is now a *named, authored* role in that single source
(flagged to Garrett for a look sign-off).

### R6 — 🟡 Remove the legacy per-player HUD layout
Once the unified path lands, the domain-panel layout is the only layout. After
migrating remaining scenes, delete `MultiplayerHUDView.HasDomainPanelWiring`,
the `_playerCards` path, and `InitializePlayerCards`/`UpdatePlayerCard` so there
is a single rendering route in `MultiplayerHUD`.

*Advanced by commit `3014de71`* — `MultiplayerHUD` now reads `rule.LiveMetric` and the per-mode HUD
subclasses were deleted, so the metric path is unified. **Remaining:** verify/remove the legacy
`_playerCards` layout (`HasDomainPanelWiring`/`InitializePlayerCards`/`UpdatePlayerCard`) once all
scenes use domain-panel wiring.

**Client-sync caveat (BUGS.md B9):** the domain layout's box values are now
server-authoritative (Approach B — `MultiplayerDomainGamesController` syncs per-domain
sums into `GameDataSO`), but the legacy `_playerCards` path still reads per-player
`RoundStats` on the client, so it can show a frozen own-card in multiplayer. Either
retire the layout (this item) or server-sync per-player values — tracked in
`TODOS.md` TD1 (recheck).

### R7 — ⚪ Replace `PlayerScoreEntry`-as-avatar-chip with a dedicated chip
`DomainScorePanel.AddPlayerIcon` reuses `PlayerScoreEntry` with an empty score
field as an avatar-only chip. Introduce a small dedicated avatar-chip widget for
clarity (low priority).

### R8 — 🟢 Remove `[FLOW-*]` `Debug.Log` spam from `MiniGameHUD`
The colored `[FLOW-HUD]`/`[FLOW-8]` logs were production diagnostic noise
(CLAUDE.md: Debug.Log is not a solution). **Done** (commit `b245e41b`): deleted
all 8 statements in `MiniGameHUD` (`Start`, `OnClientReady`, `HandleClientReady`);
the `OperationCanceledException` catch is annotated as an intentional
cancellation swallow. Pure deletion, zero behavior change. Scoped to the Scoring
System only — the `[FLOW-*]` convention in spawn/controller files is untouched.

### R9 — 🟢 Scoreboard banner uses authoritative `WinnerDomain`
`Scoreboard.DetermineWinnerDomain` used to derive the banner winner from
`DomainStatsList[0]` — a second "who won" path that matched the authoritative
winner only because of the loser-sentinel score design (and could diverge on a
tie). **Done** (commit `80b14de4`): the banner now prefers the server-authoritative
`gameData.WinnerDomain` (the same value the cinematic uses), falling back to
`DomainStatsList[0]`/`orderedStats[0]` for modes that don't set it (single-player /
co-op / DuelForCell — `WinnerDomain` stays `Blue`, already reset every scene load +
replay). Behavior-preserving in normal play; fixes the tie divergence. No reset
code needed (existing infra). Full consolidation is **R10**.

### R10 — 🟡 [in progress] One server-authoritative ranked results list
**Root fix for the redundant winner/ranking representations** (subsumes R9's
residue, `BUGS.md` B5, and `BUGS.md` B2 — the Joust "jousts left" divergence,
fixed once `Secondary` is computed server-side). Today "who won / in what order"
exists four ways:
`WinnerName`/`WinnerDomain` (authoritative, synced), sorted `RoundStatsList`,
sorted `DomainStatsList`, and the Scoreboard's local `SortPlayers` →
`orderedStats`. The server already computes the authoritative winner, assigns
scores, sorts, and syncs (`SyncFinalScores_ClientRpc` et al.) — so this is a
**consumer-side consolidation, not a rebuild**.

Target: the server produces one ordered `List<ScoreResult>` (`Rank, Name, Domain,
Score, secondary`) once, syncs it (replacing the parallel name/score/domain arrays
already sent), and every surface reads it — banner = `results[0].Domain`, cards
iterate in order, crystal reward = `results[0]` / winning-domain members, cinematic
reads the same. `WinnerName`/`WinnerDomain`, `DomainStatsList[0]`, and the per-mode
`SortPlayers` overrides collapse into it; per-mode **formatting** (time vs crystals)
stays a consumer concern (SRP).

Sequence **with R1** (unified always-networked path) — both touch the scoring sync
+ data model, so the results structure should be designed once across both.

**Progress (phased, behavior-preserving until Phase B):**
- 🟢 **A1** (`8820f8c8`) — `ScoreResult` + `ScoreResultBuilder` + `GameDataSO.Results`/`SetResults`
  (derives `WinnerName`/`WinnerDomain` from `Results[0]`).
- 🟢 **A1.5** (`d3305e62`) — added `ScoreResult.ScoreText` (formatted primary) + shared `FormatTime`.
- 🟢 **A2/A3/A4** (`05b7c71e`, `ff69baf9`, `7478d0d9`) — SkimRace / Joust / Scurry each
  assemble `gameData.Results` in their `Sync…_ClientRpc` (runs on host + every client) from the
  already-synced arrays; per-mode `ScoreText`/`Secondary` match each scoreboard's `Format*`. Joust
  publishes its target to `gameData.JoustTargetCount` (mirrors `CrystalTargetCount`) instead of a
  new RPC param. No consumer reads `Results` yet → on-screen behavior unchanged.
- 🟢 **B** (consumers) — **done via the `ScoringRuleSO` strategy** (commits `af07a171`,
  `3014de71`, `7ea5b8ae`): a per-mode `ScoringRuleSO` is now the single producer — it owns the end
  condition + `LiveMetric` and builds the ordered `Results`. `MultiplayerHUD` reads `rule.LiveMetric`
  (the SkimRace HUD now shows Crystals, matching the end condition); `Scoreboard` (cards + sort) and
  the cinematic reveal read `gameData.Results`. The per-mode `SortPlayers` / `FormatPlayerScore` /
  `FormatSecondaryStat` overrides + 3 HUD subclasses + 6 scoreboard/cinematic subclasses were
  deleted. **B2 closed** (reveal reads the domain-deficit `ScoreText`).
  **Follow-up** (`fd0dee09`): a post-merge play-test caught one surface this missed — the end-game
  vessel podium (`EndGameVesselDisplayManager`) still ranked by a local descending-`Score` sort
  (golf-inverted → showed the loser 1st in SkimRace). It now reads `gameData.Results` too (BUGS.md
  B7); that was the last end-game surface re-deriving rank locally.
- 🟡 **C** (R1) — partially advanced: `IsLocalUser` → `IsMultiplayerOwner` (commit `10e541fc`, no
  offline single-player branch). Still open: remove the `IsMultiplayerMode` scoring branches
  (`Scoreboard.cs:147,454`) and retire `DomainStatsList[0]` as a winner source.
- 🔴 **D** (next session) — **server-ORDERED results sync**: the rows are still re-SORTED on every
  peer (`SyncFinalScores_ClientRpc` → `rule.BuildResults` over the local `RoundStatsList`), so tied
  rows order differently host vs client. Sort once on the server, ship rows in rank order (+ the
  formatted `ScoreText`/`Secondary` strings), clients build `Results` from the arrays verbatim. Plus
  the per-player `LastCrystalCollectedTime` tie-break (owner rule, pending one ranking decision) and
  the podium name-join hardening. **Full work package, design + commit sequence:
  `RANKING_SYNC_PLAN.md`** (Steps 4–7; includes follow-ups F1 SOAP-ResetOn guard, F2
  `RequestedDomainCount` comment drift, F3 optional ClientReady dedupe).

### R11 — ⚪ [deferred · needs device + UGS testing] Fully retire `GolfScoreSentinels`
Follow-on to **R4**, which deliberately stopped at *centralizing* the sentinel into one file. This
item removes the float-encoded DNF signal entirely. **Deferred by owner decision** — disproportionate
scope/risk for the payoff, and it changes cloud-leaderboard behavior that can't be verified in this
environment.

**Why the sentinel exists:** golf modes (SkimRace, Joust) encode "did this player finish?" into the
`float Score` — winner = real finish time (`< DnfThreshold`), loser = sentinel
(`10000 + crystalsLeft` / `99999`). That one float is the finish/DNF signal read by sort, domain
aggregation, the cloud leaderboard, and quests — which is exactly why removing it reaches so far.

**Agreed design (no new synced field):** replace the float-threshold signal with the already-synced
**`WinnerDomain`** — a player finished ⇔ `stats.Domain == gameData.WinnerDomain`. `Score` becomes a
real value only (winner = finish time; loser = `0`; CC unchanged = crystals). The synced `scores[]`
arrays keep their shape (no RPC signature change); `WinnerDomain`, synced in the same RPC, carries the
outcome. Add `ScoreOutcome {Winner,Loser}` to `ScoreResult` so consumers never re-derive from the float.

**Blast radius (~10 files + tests + cloud):**
- *Rules* — `SkimRaceScoringRuleSO`/`JoustScoringRuleSO`: `AssignScores` loser `Score = 0f`;
  `BuildResults` decides winner/loser via `Domain == WinnerDomain` (not `IsFinishTime`).
- *Generic `GameDataSO` (all modes — risky)* — `SortRoundStats`/`CalculateDomainStats` must become
  winner-aware (with losers at `0`, golf-ascending would otherwise rank losers first).
- *Cloud (highest risk, untestable here)* — `UGSStatsManager.GetEvaluatedHighScore` must take a
  `didFinish` arg (else a loser's `0` overwrites the cloud best time); `ReportSkimRaceStats`/
  `ReportJoustStats` drop the `IsFinishTime` guard and gate on the caller's `didFinish`;
  `SkimRaceScoreTracker` + `JoustStatsReporter` thread `didFinish`.
- *Progression* — `GameModeProgressionService` `RaceTimeUnder`/`WinMatch` switch from
  `IsFinishTime`/`RoundStatsList[0]` to `Domain == WinnerDomain`.
- *Tests* — `GameDataSOTests` asserts on `RoundStatsList[0]`/`DomainStatsList[0]` after the current
  Score-based sort; update to the winner-aware contract.
- *Delete* — `GolfScoreSentinels.cs` (+ `.meta`).

**Exit criteria:** solo + 2-human-team SkimRace/Joust scoreboard + cinematic correct; leaderboard
records the winner's time and a loser does **not** overwrite the cloud best with `0`;
RaceTimeUnder/WinMatch quests fire; CC + WildlifeBlitz unaffected; edit-mode tests green. Land as its
own reviewed PR.

### R12 — ⚪ [deferred · low payoff] Fold per-mode end-game stat providers behind the rule
The end-game stat rows (Best Streak / Longest Drift / Jousts Won) are produced by three
heterogeneous `…StatsProvider`s reading different sources (`SkimRaceScoreTracker`, `VesselTelemetry`,
`RoundStats`). Target: fold them behind the `ScoringRuleSO` (or a thin provider keyed off
`rule.Metric`) and delete the three providers, so the rule is the single producer for *all* end-game
surfaces (matching R10). **Deferred by owner decision** — same refactor-for-low-payoff profile as
R11; the sources are genuinely different, so it's modest risk for little structural gain. Pick up only
when the stat-row surface itself changes.

---

## Related (shipped this session, outside the R-backlog)
- **Joust replay → network scene reload** (commit `21d538d3`) — Joust now replays via a full scene
  reload, matching SkimRace/Scurry, instead of an in-place reset. Gameplay-flow consistency
  (not a scoring-data change), recorded here for traceability with the scoring session.

---

## Parking lot
- `CoOpScoreBoard.OppponentScoreTextField` — field-name typo ("Oppponent");
  rename when touched.
- `HUDAnimationSettingsSO.scoreboardRowStagger` appears unused (the scoreboard
  passes the row index but `PlayerScoreCard` uses `cardEntranceStagger`) — see
  `BUGS.md` B3; remove or wire correctly.
- `DuelForCell` in-game HUD wiring is unclear (no dedicated HUD subclass) —
  confirm which HUD the Cellular Duel scene uses during the unified-path work.
- `GameCanvas-SkimRace.prefab` carries stale Scoreboard-era data: the internal
  `Scoreboard`'s serialized fields predate the current class (old
  `multiplayerController` name, `SinglePlayerBannerColor`, rematch panel refs),
  and the retired rematch UI is still in the prefab with persistent calls to
  deleted methods (`OnAcceptRematch`/`OnDeclineRematch` on the old
  `SkimRaceScoreboard` type). Inert — the rematch panels are removed per-scene
  and a null-target persistent call is skipped — but it caused the wiring
  confusion behind `BUGS.md` B13/B14. Re-save the prefab (refreshes serialized
  field names) and delete the rematch subtree when the prefab is next touched;
  re-check the three scenes' overrides afterward.
