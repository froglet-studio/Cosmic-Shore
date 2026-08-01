# Solo-Retirement Program — Verification Checklist (`claude/unified-yash-refactor-9sc0ws`)

One consolidated in-editor/MPPM pass covering everything on this branch: Y1 scoring
unification → C1–C8 (solo retirement, benchmark conversion, `IsMultiplayerMode` removal,
dead-content deletion, standalone-freestyle retirement) → C9 (Cellular Duel retirement) →
C10–C12 (Wildlife Blitz + 2v2 retirement) → Y0 wire-fix wave → M1–M3 (`bleeding-edge` merge).
Steps are ordered to minimize scene churn. Keep the numbering — progress is tracked
against it. (C10 retired Wildlife Blitz: steps 8-11 and 23 are struck.)

Legend: `[x]` verified by owner · `[ ]` pending · ~~struck~~ = obsolete (feature retired
after the step was written; do not test).

> **Status 2026-07-25 — 6 of 33 live steps verified.** Steps 1–6 passed in the editor on
> 2026-07-21; everything since (C9–C12, Y0, and BOTH `bleeding-edge` merges) has been verified
> **statically only** — guid greps, deleted-symbol sweeps, scene/YAML lint, build-settings
> resolution. That catches dangling references and compile breaks; it cannot catch wrong
> gameplay values, a broken end-game sequence, or a HUD that no longer binds. Steps 12–21 and
> 24–37 are the outstanding work, and they gate starting Y3+.
>
> Highest-value first if time is short: **34** (silent failure mode), **29/30** (the two merge
> unions), **35** (compile), then **13–17** (per-mode regression).

## Setup

- [x] **1.** Pull the branch, open in Unity, let it compile — zero compile errors. *(verified 2026-07-21)*
- [x] **2.** Test Runner → EditMode → run all: `EnumIntegrityTests`, `SceneFlowIntegrationTests`, `DomainAssignerTests`, Bootstrap tests — all green. *(verified 2026-07-21)*
- [x] **3.** For Part D, MPPM with one clone (2 humans total). *(verified 2026-07-21)*

## Part A — Boot + lava lamp (C5, C8)

- [x] **4.** Boot → auth → Menu_Main: normal startup, host starts, no errors (C5 deleted the legacy matchmaking path in `MultiplayerSetup` — sign-in host start must be unaffected). *(verified 2026-07-21)*
- [x] **5.** Lava-lamp regression (C8): autopilot vessel drifts behind UI → tap crystal → control + Game UI + vessel HUD. Toys all work (vessel changer keeps domain/speed + HUD re-shows, domain changer, painting, Wanderway). Gamepad **Start** exits; center-tap returns to menu. *(verified 2026-07-21)*
- [x] **6.** Arcade grid contents (C6, C8, C9, C10, C11): every game-list surface now binds OrganicRematchGames (LaunchPartyAllGames was deleted and its holders rewired; the 2v2 card/scene are gone). *(verified 2026-07-21 pre-C9)* — **re-verify post-merge**: the live set is now **seven** cards (HexRace, Joust, Crystal Capture, Maelstrom, AstroLeague, NucleusRush, **Rampage**) and none of the retired ones. See step 28.

## Part A2 — Y0 wire-fix wave (W1-W5, 2026-07-21)

- [ ] **25.** Quest chain alive (Y0.1): Arcade → quest track — quests unlock beyond quest 0,
  intensity gating works, completing a quest's game fires the quest-complete toast.
- [ ] **26.** Joust + CC centerline feeds (Y0.2): covered inside steps 14/15/21 — mid-turn the
  centerline score ticks on BOTH peers (Joust: elapsed time; CC: crystals), final results
  identical host vs client, exactly ONE end sequence (the legacy tracker's duplicate winner
  event is gone).
- [ ] **27.** Touch inversion (Y0.3, touch device/simulator): Settings → Invert Y / Invert
  Throttle now affect touch flight (pitch/roll flip; throttle flips).

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

## Part E — Post-merge with `bleeding-edge` (M1–M3, 2026-07-25)

The merge brought in Rampage, the game-toast system (which replaced `GameEventFeed`), and
finish-time golf scoring for Crystal Capture. These steps cover the seams where the two
branches met — a break here is a *merge-integration* bug, not a regression in either
branch alone.

- [ ] **28.** Arcade grid post-merge: **seven** live cards — HexRace, Joust, Crystal Capture,
  Maelstrom, AstroLeague, NucleusRush, **Rampage**. None of Wildlife Blitz / Cellular Duel /
  Freestyle / 2v2 reappears on ANY surface (grid, rematch list, quest track, CTA).
- [ ] **29.** **Rampage** solo (new mode, ID 2): launches from the card, AI pilots hunt the
  densest hostile-mass region (they should visibly converge on other domains' trails, not
  wander), the match ends on the prism target, scoreboard shows **finish time** for the
  winning domain and the remaining-prisms sentinel for the losers, exactly ONE end sequence,
  Play Again reloads the scene. Watch the Rampage controller in the inspector: its **Scoring**
  slot must hold `RampageScoringRule` (the merge removed a shadowing field — a null here means
  the serialized binding didn't survive).
- [ ] **30.** **Crystal Capture** post-merge (both behaviours must coexist): mid-turn the
  centerline score ticks **crystals** on both peers (my Y0.2 server feed), and at the end the
  scoreboard flips to **finish time** for the winner / remaining-crystals sentinel for the
  losers (their golf scoring). Getting only one of the two = the union resolution regressed.
- [ ] **31.** **Game toasts** replaced the old event feed: a toast appears on player-Ready and
  on a mid-game disconnect. The old `GameEventFeed` strip must be gone entirely (no empty UI
  object left behind). **Expect partial coverage** — `GameToastController`/`GameToastView` live
  on a per-scene panel, and only **HexRace** (script mounted directly) and **Joust**
  (`NotificationUI.prefab` instanced) currently carry one. Crystal Capture, Maelstrom,
  AstroLeague, NucleusRush, Rampage and Menu_Main have no toast UI, so they will show nothing —
  including the Brood Rush wave beat, whose `GameToastAPI.Post` call fires into a scene with no
  listener. This is bleeding-edge's rollout state, not merge damage; wiring the remaining six
  scenes is a follow-up, not part of this branch.
- [ ] **32.** Bootstrap union check (W1 vs their Bootstrap UI rework): boot completes, the new
  heartbeat loader animates, AND the quest chain still works (step 25) — the
  `GameModeProgressionService` component must still be mounted on the `PlayerDataService`
  GameObject in `Bootstrap.unity`.
- [ ] **33.** Build settings: File → Build Settings lists 13 scenes, all resolving (no
  `<missing>` rows), including `MinigameRampage` and `BenchmarkStressTest`.

## Part F — Second `bleeding-edge` merge (M4, 2026-07-25)

- [ ] **34.** **HexRace per-intensity laps** — the highest-value check on this branch, because
  a failure here is silent (no error, just wrong pacing). Upstream added `lapsPerIntensity` to
  the non-network monitor base that Y1.3 had collapsed away; the field was ported by hand onto
  `NetworkCrystalCollisionTurnMonitor`. Confirm the crystal target per intensity:

  | Intensity | Waypoints × laps | Expected target |
  |---|---|---|
  | 1 | 8 × 3 | **24** |
  | 2 | 10 × 3 | **30** |
  | 3 | 28 × 2 | **56** |
  | 4 | 27 × 2 | **54** |

  If intensities 3–4 ask for ~84/81 instead, the port did not take and the scene fell back to the
  flat `optionalLaps` — check that the monitor's inspector shows a **Laps Per Intensity** list of
  `[3,3,2,2]`.

- [ ] **35.** Compile clean on a fresh reload. Two CS0103s from `isMultiplayer` were fixed
  (`GameDataSO.InvokeGameLaunch`, `MultiplayerMiniGameControllerBase.SyncGameConfigToClients_ClientRpc`).
  Any further "does not exist in the current context" error is the same merge hazard in a third
  place — report the exact message rather than working around it.

- [ ] **36.** Cloud profile still loads after the Cloud Save schema swap (`ModeStatsCloudData` /
  `ModeStatsRepository` replaced the per-mode profile family): Profile screen shows stats, and
  Tools → Log Control → UGS shows a populated **Mode Stats** section. Old per-mode cloud buckets
  are gone by design; historical values may not carry over.

- [ ] **37.** Hangar **Train** button no-ops for every vessel (lists are intentionally empty after
  C6/M3) — it must not throw. See the note in Part E's preamble.

## Throughout

- [ ] **24.** Console watch: no NREs, no `[Invalid Destroy]`, no missing-script/reference errors from deleted classes — especially in the benchmark scene, which was surgically decoupled from the deleted blitz stack (C10), and in any scene bleeding-edge touched (the merge is the other place a "missing script" can appear: their asset referencing my deleted script, or mine referencing their deleted `GameFeedAPI`). (The one known pre-existing missing-script component lived in the duel scene — gone with C9; none should remain anywhere.)

---

*Update the checkboxes as you verify; anything that fails → report the step number + console output.*
