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
`OnEnable` hide were kept so scene-authored legacy reward UI (e.g. HexRace's
active `CrystalDisplayBG`) stays hidden. Behavior-preserving; closes `BUGS.md` B4.

### R4 — 🟢 Centralize the loser-score sentinel encode/decode
HexRace (`10000 + crystalsLeft`) and Joust (`99999`) encoded loser scores as
magic numbers decoded by duplicated literals — and HexRace had **two** encode
sites that could drift (controller literal vs `HexRaceScoreTracker.penaltyScoreBase`).
**Done** (commit `68550228`): new static `GolfScoreSentinels` (`CosmicShore.Gameplay`)
holds the constants (`DnfThreshold`, `HexRaceLoserBase`, `JoustLoserScore`) +
helpers (`Encode/DecodeHexRaceCrystalsLeft`, `IsHexRaceLoserScore`,
`IsJoustLoserScore`, `IsFinishTime`). Migrated every write (HexRaceController,
HexRaceScoreTracker, MultiplayerJoustController) and read (HexRaceScoreboard,
HexRaceEndGameController, MultiplayerJoustScoreboard) — **plus** the same-sentinel
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

**Remaining domain-color sources (outside the Scoring System — separate follow-up
if we want truly app-wide single-source):** `DomainColorPaletteSO` via
`SilhouetteConfigSO` / `SquirrelVesselHUDController` (vessel HUD / elemental), and
`GameEventFeed.domainColors` + the static `GameFeedAPI.DomainColors` dictionary
(game feed). All would route to `ThemeManagerData.ColorSet` the same way.

### R6 — 🔴 Remove the legacy per-player HUD layout
Once the unified path lands, the domain-panel layout is the only layout. After
migrating remaining scenes, delete `MultiplayerHUDView.HasDomainPanelWiring`,
the `_playerCards` path, and `InitializePlayerCards`/`UpdatePlayerCard` so there
is a single rendering route in `MultiplayerHUD`.

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

### R10 — 🔴 [discuss-first] One server-authoritative ranked results list
**Root fix for the redundant winner/ranking representations** (subsumes R9's
residue and `BUGS.md` B5). Today "who won / in what order" exists four ways:
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
Discuss-first; do not start before R1's design is agreed.

---

## Parking lot
- `CoOpScoreBoard.OppponentScoreTextField` — field-name typo ("Oppponent");
  rename when touched.
- `HUDAnimationSettingsSO.scoreboardRowStagger` appears unused (the scoreboard
  passes the row index but `PlayerScoreCard` uses `cardEntranceStagger`) — see
  `BUGS.md` B3; remove or wire correctly.
- `DuelForCell` in-game HUD wiring is unclear (no dedicated HUD subclass) —
  confirm which HUD the Cellular Duel scene uses during the unified-path work.
