# Scoring System — TODOs / Open Items

Loose, not-yet-scheduled work + things to recheck for the Scoring System.
Sequenced cleanups live in `REFACTOR.md` (R-items); confirmed defects + their
fixes in `BUGS.md`; this file holds open TODOs. Read `ARCHITECTURE.md` first.

## Status legend
🔴 open · 🟡 in progress · 🟢 done (verify only) · ⚪ deferred

---

## TD1 — 🔴 [recheck tomorrow] Server-sync the LEGACY per-player HUD layout
**Context.** The in-game multiplayer HUD (`MultiplayerHUD`) has two layouts:
- **Domain layout** (`MultiplayerHUDView.HasDomainPanelWiring == true`) — per-domain
  boxes. **Client-sync fixed** this session: box values come from the
  server-computed `GameDataSO.GetDomainMetricSum` (BUGS.md **B9** / "Approach B").
- **Legacy per-player layout** (fallback when a scene isn't wired) — one
  `PlayerScoreEntry` per player, value = `GetInitialCardValue(stats)` =
  `gameData.ScoringRule.LiveMetric(stats)`, read **directly off each player's
  `RoundStats`** on the client.

**The gap.** Approach B only fixed the **domain** layout. The legacy path still
reads per-player `RoundStats` on the client, so it can exhibit the SAME defect B9
fixed: a client's OWN card can freeze while correct on the host (owner-side
replication of the client's own metric proved unreliable — root cause never
isolated; the server-sum sync side-stepped it for the domain layout).

**Why it's a TODO, not a live bug (yet).** Every in-use domain mode
(SkimRace / Joust / Scurry) is — or should be — wired for the **domain**
layout, so the legacy path isn't exercised in shipped scenes. It only bites if a
scene falls back to legacy (e.g. an unwired Joust scene — see **TD2**).

**Options (decide on recheck):**
- **(a) Retire the legacy layout** — finish `REFACTOR.md` **R6**: once every scene
  uses domain panels, delete `HasDomainPanelWiring` / `_playerCards` /
  `InitializePlayerCards` / `UpdatePlayerCard`, leaving one rendering route.
  Removes the gap by construction. *Preferred if no in-use scene needs legacy.*
- **(b) Server-sync per-player values** — extend Approach B to also replicate a
  per-player metric value the legacy card reads (heavier: per-player, not just the
  3 domain sums). Only if a scene must keep the legacy layout.

**Recheck tomorrow (with tester):** confirm whether ANY in-use scene still relies
on the legacy per-player layout. If none → do **(a)**. If one must keep it → do **(b)**.

Files: `_Scripts/UI/MultiplayerHUD.cs` (`InitializePlayerCards`,
`CreateCardForPlayer`, `UpdatePlayerCard`, `GetInitialCardValue`),
`_Scripts/UI/View/MultiplayerHUDView.cs` (`HasDomainPanelWiring`).

## TD2 — 🔴 [verify] Joust scene domain-HUD wiring
SkimRace (`debb3239`/`4dc95258`), Crystal Capture (`e57066b6`), and Joust
(`d2562539`) scenes all have update commits. The Joust commit **exists**, but it's
unconfirmed whether `MinigameJoust_Gameplay`'s `MultiplayerHUDView` has the
ally/opposing containers + `DomainScorePanel` prefab assigned. If it doesn't, Joust
falls back to the legacy layout (**TD1**) and won't show the domain boxes.
**Action:** open the Joust scene, confirm `HasDomainPanelWiring`; wire it like
SkimRace / CC if missing.

## TD3 — 🟡 [verify + optional cleanup] Confirm domain fixes; consider retiring Approach B
The in-game domain HUD now reads `Player.Domain` (Commit `5442d3d0`) and `RoundStats.Domain`
is a local mirror of the authoritative `NetDomain` (Commit `aaabc1b6`, `n_Domain` retired).
**Status:** B9 + B10 ✅ confirmed in a 2-human engine test; broader mode coverage
(Scurry / Joust) + more scenarios pending.
Verify with the tester (2 humans, separate domains):
- **B10 (icon placement):** each player's profile icon sits in its OWN domain box on the
  client (the reported bug) — ✅ confirmed. Re-check on Crystal Capture + Joust.
- **B9 (counts):** each domain box's number tracks the host — ✅ confirmed. Re-check on CC + Joust.
See `TESTS.md` T11 / T12. Joust is gated on **TD2** (scene wiring).

**Optional cleanup once confirmed:** "Approach B" (server-synced domain sums —
`MultiplayerDomainGamesController` + `GameDataSO.GetDomainMetricSum`) is now **redundant**:
with the domain correct on every peer, the HUD could client-side sum by `Player.Domain`
reading the reliably-replicated per-player metric (`RoundStats.CrystalsCollected` etc.) and
drop the 3 server-sum `NetworkVariable`s. Keep it as harmless robustness until the domain
fixes are play-test-confirmed, then retire if desired.
