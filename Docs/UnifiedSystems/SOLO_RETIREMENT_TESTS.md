# Solo-Retirement Program — Verification Checklist (`claude/unified-yash-refactor-9sc0ws`)

One consolidated in-editor/MPPM pass covering everything on this branch: Y1 scoring
unification → C1–C8 (solo retirement, benchmark conversion, `IsMultiplayerMode` removal,
dead-content deletion, standalone-freestyle retirement) → C9 (Cellular Duel retirement).
Steps are ordered to minimize scene churn. Keep the numbering — progress is tracked
against it. (C10 retired Wildlife Blitz: steps 8-11 and 23 are struck.)

Legend: `[x]` verified by owner · `[ ]` pending · ~~struck~~ = obsolete (feature retired
after the step was written; do not test).

## Setup

- [x] **1.** Pull the branch, open in Unity, let it compile — zero compile errors. *(verified 2026-07-21)*
- [x] **2.** Test Runner → EditMode → run all: `EnumIntegrityTests`, `SceneFlowIntegrationTests`, `DomainAssignerTests`, Bootstrap tests — all green. *(verified 2026-07-21)*
- [x] **3.** For Part D, MPPM with one clone (2 humans total). *(verified 2026-07-21)*

## Part A — Boot + lava lamp (C5, C8)

- [x] **4.** Boot → auth → Menu_Main: normal startup, host starts, no errors (C5 deleted the legacy matchmaking path in `MultiplayerSetup` — sign-in host start must be unaffected). *(verified 2026-07-21)*
- [x] **5.** Lava-lamp regression (C8): autopilot vessel drifts behind UI → tap crystal → control + Game UI + vessel HUD. Toys all work (vessel changer keeps domain/speed + HUD re-shows, domain changer, painting, Wanderway). Gamepad **Start** exits; center-tap returns to menu. *(verified 2026-07-21)*
- [x] **6.** Arcade grid contents (C6, C8, C9, C10): no solo cards, no Freestyle card, no Cellular Duel card, no Wildlife Blitz card. Expected: HexRace, Joust, Crystal Capture, Maelstrom (OrganicRematchGames); the LaunchPartyAllGames surfaces now render zero cards. *(verified 2026-07-21 pre-C9/C10 — re-check that the Duel + Blitz cards are gone)*

## Part B — Solo runs (solo = party of one + AI)

- ~~**7.** Cellular Duel solo~~ — **OBSOLETE**: Cellular Duel retired (C9, 2026-07-21). Do not test.
- ~~**8.** Wildlife Blitz solo — WIN~~ — **OBSOLETE**: Wildlife Blitz retired (C10, 2026-07-21). Do not test.
- ~~**9.** Wildlife Blitz solo — LOSS~~ — **OBSOLETE**: Wildlife Blitz retired (C10, 2026-07-21). Do not test.
- ~~**10.** Blitz solo with AI teammates~~ — **OBSOLETE**: Wildlife Blitz retired (C10, 2026-07-21). Do not test.
- ~~**11.** Blitz Play Again~~ — **OBSOLETE**: Wildlife Blitz retired (C10, 2026-07-21). Do not test.
- [ ] **12.** Benchmark (C3, decoupled in C10): Settings → Run Benchmark → loads via Netcode, auto-starts in ~1 s with no Ready click, your Squirrel + AI-crowd-size AI Squirrels on distinct spawn points, never ends (no scoring HUD any more); Exit returns to menu.

## Part C — Regression on untouched modes (the Y1.2/C2 shared tail touched all of them)

- [ ] **13.** HexRace solo: 3 AI backfill, ends at crystal target, VICTORY/DEFEAT + time, ranked scoreboard, Play Again (scene reload).
- [ ] **14.** Joust solo: normal end; winner name = top jouster on the winning domain (accepted Y1.2 delta).
- [ ] **15.** Crystal Capture solo: target/remaining display correct, normal end.
- [ ] **16.** AstroLeague + NucleusRush solo: quick sanity — launch, score, end sequence fires once.
- [ ] **17.** Maelstrom/Tournament solo (C5 touched `TournamentController`): full chain HexRace → Joust → Crystal Capture, standings fold, race-to-6, summary screen.

## Part D — 2-human party (MPPM)

- [ ] **18.** Party: invite → client joins → both vessels visible in the menu lava lamp; each toggles freestyle independently (C8 safety).
- [ ] **19.** Presence (C5 intentional change): while one player is in ANY game — including solo — the other sees the match name in the friends list.
- [ ] **20.** HexRace 2-human: centerline + domain boxes live on both machines, identical final results host vs client, single end sequence, Play Again, then the menu-cycle (T15): game → menu → another game, no dead second end-game.
- [ ] **21.** Joust + Crystal Capture 2-human: same checks (also proves the C5 config-RPC change — client receives correct intensity/player count).
- ~~**22.** Duel 2-human~~ — **OBSOLETE**: Cellular Duel retired (C9, 2026-07-21). Do not test.
- ~~**23.** Blitz 2-human co-op~~ — **OBSOLETE**: Wildlife Blitz retired (C10, 2026-07-21). Do not test.

## Throughout

- [ ] **24.** Console watch: no NREs, no `[Invalid Destroy]`, no missing-script/reference errors from deleted classes — especially in the benchmark scene, which was surgically decoupled from the deleted blitz stack (C10). (The one known pre-existing missing-script component lived in the duel scene — gone with C9; none should remain anywhere.)

---

*Update the checkboxes as you verify; anything that fails → report the step number + console output.*
