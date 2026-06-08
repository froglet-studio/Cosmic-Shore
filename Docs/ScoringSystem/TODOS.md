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
(HexRace / Joust / CrystalCapture) is — or should be — wired for the **domain**
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
The HexRace (`debb3239`) and Crystal Capture (`e57066b6`) scenes were wired for the
domain HUD this session; **no equivalent Joust scene commit exists.** If
`MinigameJoust_Gameplay`'s `MultiplayerHUDView` lacks the ally/opposing containers
+ `DomainScorePanel` prefab, Joust falls back to the legacy layout (**TD1**) and
won't benefit from the B9 / Approach-B client-sync fix.
**Action:** open the Joust scene, confirm `HasDomainPanelWiring`; if missing, wire
it like HexRace / CC.

## TD3 — 🟢 [verify only] Confirm B9 fix across all three domain modes
Approach B lives in the shared `MultiplayerDomainGamesController` + `MultiplayerHUD`
and is parameterized by `gameData.ScoringRule.Metric`, so it should fix client
domain-box sync for all three modes by construction (no HexRace-specific logic).
Verify with the tester (2 humans, separate domains — each client's OWN domain box
must track the host):
- **HexRace** (metric Crystals) — primary repro, fix under test.
- **Crystal Capture** (metric Crystals) — same crystal mechanism + metric → expected identical.
- **Joust** (metric Jousts) — server sums `RoundStats.JoustCollisions` (server-authoritative); gated on **TD2**.
See `TESTS.md` T11 / T12.
