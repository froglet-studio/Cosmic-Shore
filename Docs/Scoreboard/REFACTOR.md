# Scoreboard System — Refactor Backlog

Sequenced cleanup + improvement items for the scoreboard surfaces. Read
`ARCHITECTURE.md` first.

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

### R2 — 🔴 Deduplicate the score-text animation (DRY)
`PlayCounterRoll` / `PlayScorePunch` / `PlayColorFlash` / entrance are
copy-pasted across `PlayerScoreEntry`, `PlayerScoreCard`, and `DomainScorePanel`
(~80 lines ×3). Extract one shared animated-score component/helper driven by
`HUDAnimationSettingsSO`. Pure refactor; verify visuals unchanged.

### R3 — 🔴 Single source of truth for the winner crystal reward
`Scoreboard.winnerCrystalReward` (=5) and
`EndGameCinematicController.crystalsPerGame` (=5) are independent and
hand-synced; the `delegateCrystalRewardToScoreboard` handshake is implicit.
Collapse to one configured value + one award path (see also `BUGS.md` B4).

### R4 — 🔴 Centralize the loser-score sentinel encode/decode
HexRace (`10000 + crystalsLeft`) and Joust (`99999`) encode loser scores as
magic numbers, decoded by literals (`< 10000f` / `< 99999f`) duplicated in the
scoreboards. Put encode + decode behind named constants/helpers shared by the
controller (write) and scoreboard (read) so they cannot drift.

### R5 — 🔴 Unify domain → color resolution
Three paths today (`ThemeManagerData.ColorSet` / `DomainColorPaletteSO` /
`MiniGameHUDView.domainColors`) plus hardcoded banner fallbacks in `Scoreboard`.
Consolidate on the theme palette as the single domain-color source.

### R6 — 🔴 Remove the legacy per-player HUD layout
Once the unified path lands, the domain-panel layout is the only layout. After
migrating remaining scenes, delete `MultiplayerHUDView.HasDomainPanelWiring`,
the `_playerCards` path, and `InitializePlayerCards`/`UpdatePlayerCard` so there
is a single rendering route in `MultiplayerHUD`.

### R7 — ⚪ Replace `PlayerScoreEntry`-as-avatar-chip with a dedicated chip
`DomainScorePanel.AddPlayerIcon` reuses `PlayerScoreEntry` with an empty score
field as an avatar-only chip. Introduce a small dedicated avatar-chip widget for
clarity (low priority).

### R8 — 🔴 Remove `[FLOW-*]` `Debug.Log` spam from `MiniGameHUD`
The colored `[FLOW-HUD]`/`[FLOW-8]` logs are production diagnostic noise
(CLAUDE.md: Debug.Log is not a solution). Remove or gate behind a verbose flag.

### R9 — 🔴 Document/enforce the end-game ordering contract
`Scoreboard.DetermineWinnerDomain` reads `DomainStatsList[0]`, assuming the
controller called `CalculateDomainStats(golf)` (which sorts) **before**
`InvokeShowGameEndScreen()`. Make the contract explicit (doc + assert) so a new
mode can't silently show the wrong banner.

---

## Parking lot
- `CoOpScoreBoard.OppponentScoreTextField` — field-name typo ("Oppponent");
  rename when touched.
- `HUDAnimationSettingsSO.scoreboardRowStagger` appears unused (the scoreboard
  passes the row index but `PlayerScoreCard` uses `cardEntranceStagger`) — see
  `BUGS.md` B3; remove or wire correctly.
- `DuelForCell` in-game HUD wiring is unclear (no dedicated HUD subclass) —
  confirm which HUD the Cellular Duel scene uses during the unified-path work.
