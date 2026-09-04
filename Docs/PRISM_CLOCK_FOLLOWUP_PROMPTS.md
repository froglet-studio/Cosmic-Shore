# Prism Clock — Follow-Up Branch Prompts

Ready-to-paste prompts for the work remaining after the clock-material migration
branch. Each is self-contained for a FRESH session: it names the docs to read
first, the scope, the constraints, and the in-editor test that closes it. Run
them as separate branches.

> **Audited + re-set 2026-08-15; Prompt 10 shipped 2026-08-24; Prompt 12,
> Prompt 8, Prompt 4, Prompt 13, Prompt 3, Prompt 9b, Prompt 14, and Prompt 15 shipped 2026-08-25.** Four statements in the 2026-08-07 revision were not
> merely stale but **actively harmful** — a session following them would have
> deleted a live code path, resurrected deleted code, deleted the only asset
> the last outstanding Phase-3 test depends on, and re-wired already-wired
> graphs. Prompt 10 closed that drift.

**PRIORITY ORDER (re-set 2026-08-25) — work top-down:**

| # | Prompt | Why this rank |
|---|---|---|
| 1 | **Prompt 11** — the one editor session (measure + remaining outstanding playtests) | Nine items all need a human in the editor. D4's measurement gate (items 1+2) landed; Prompt 9b retired death pooling; Prompt 14 shipped C13b pooling. Remaining look-verifies: corridor, shield morph, jiggle remainder, D3/Phase 5, C9 Cell Selector swap, C11 starve/joust wither, C6 parent-scale (worm glide only — the Space-5 growth half is void since `Docs/ECOSYSTEM.md` §40 deleted `GrowToScale`), **C13b Blue→domain spawn repaint + Wanderway conservation**. |
| 2 | ~~**Prompt 7**~~ — C12/B1 cleanup sweep | ✅ **DONE 2026-08-25** — six items closed (watchdog→scheduler; SkimFxRunner = vessel FX recorded; dead CloakSeedWallAction.cs only; HoldCollider deleted; CreateBlock window kept+documented; analytic grow end + arena settle). |
| 3 | ~~**Prompt 9b**~~ — D4: retire pooled *death* spawn | ✅ **DONE 2026-08-25** — death pooling retired (batch-only; declined request dropped). Grow stays pooled (Sparrow ReverseSuction). Prefabs remain CONFIG. |
| 4 | ~~**Prompt 14**~~ — C13b environment-lay pooling | ✅ **DONE 2026-08-25** — unbounded `EnvironmentPrismPool`; snap Blue then ChangeTeam clock-lerps. Flora HealthPrism Instantiates folded. Playtest → Prompt 11. |
| 5 | ~~**Prompt 15**~~ — `ShapeDrawingManager` ruling | ✅ **DONE 2026-08-25** — resolved by deletion (C15). Unreachable after `MinigameFreestyle.unity`; exclusive dependents gone; SOAP events kept (inert landmine). |
| 6 | **Prompt 16** — corridor dither strobe successor | Kernel 6 (SHARD3D) exists; coverage proven offline (0.00783). Look-at-speed on real mass is the remaining gate. Do not Bake as CURRENT. |

**Shipped — do not re-open.** Their prompt bodies are deleted where following
them would now cause harm; the DONE blocks below keep the lesson.

| Prompt | Outcome |
|---|---|
| ~~Prompt 1~~ — transparent-prism occlusion restore | ✅ **DONE 2026-08-04** — C1, restored as a shader-side corridor off two global uniforms and promoted to a PLATFORM LAW (`Docs/PRISM_ANIMATION.md` §4.7). Playtest still outstanding → Prompt 11. |
| ~~Prompt 2~~ — C13a environment-lay prisms miss the clock path | ✅ **DONE 2026-08-02** — cause was the shield engage-morph straddling the creation reveal, not the raw-`Instantiate` lay. Residual C13b shipped as **Prompt 14** (2026-08-25). |
| ~~Prompt 5~~ — projectile prism paths | ✅ **DONE 2026-08-07** — C5 shipped as `PrismFlightClock`; C4 resolved by deletion (`cc9a1f5b`). |
| ~~Prompt 6~~ — B4 shield morphs on the GPU | ✅ **DONE 2026-08-15** — PR #729, `37f9596a`. **Phase B is complete**; the last sanctioned CPU prism ticker is deleted. |
| ~~Prompt 9~~ (build half) — batched entity debris | ✅ **DONE 2026-08-04** — implosions on the batch carrier + the death-path marker split. Editor half → Prompt 11; D4 death pooling → Prompt 9b (shipped). |
| ~~Prompt 9b~~ — D4 retire pooled death spawn | ✅ **DONE 2026-08-25** — factory `SpawnExplosion`/`SpawnImplosion` are batch-only; declined request warns once and drops. Authored config stays on the pool prefabs. Explosion pool never Get()d. **Grow kept** (`StartGrow` / `OnGrowCompleted` / Sparrow ReverseSuction). Do not re-open as a deletion of `PrismImplosion`. |
| ~~Prompt 10~~ — cross-doc truth reconcile | ✅ **DONE 2026-08-24** — Grow is live (Sparrow ReverseSuction); §6 is a completed-handoff; PhaseThresholds re-baseline ✅ 2026-08-02; C7 ✅ by construction; unsatisfiable Animators-HUD rows gone. Do not re-open. |
| ~~Prompt 12~~ — CI-gate the clock wiring | ✅ **DONE 2026-08-25** — `PrismClockWiringTests` iterates `PrismClockWiringValidator.Specs`; ten `Tools/Shaders/*.py --check` in `bleeding-edge-guard.yml` + `unity-ci.yml` (Prompt 4 added `wire_prism_suction_clock.py`; Prompt 13 added `wire_prism_spindle_death_clock.py`). The menu item still has zero callers; the Specs it prints now fail CI. Do not re-open. |
| ~~Prompt 7~~ — C12/B1 cleanup sweep | ✅ **DONE 2026-08-25** — watchdog→scheduler; SkimFxRunner = vessel FX; dead `CloakSeedWallAction.cs` only; HoldCollider deleted; CreateBlock window kept+documented; analytic grow end + arena settle. |
| ~~Prompt 8~~ — validator coverage + wirer divergence | ✅ **DONE 2026-08-25** — Specs names erosion / back-face / sight; Validate Clock Wiring ANDs `CheckGraphWiring`; Auto-Wire stamps `_ShieldMorph*` and declares python-owned splices; SuctionGraph is a named live corridor exclusion. Do not re-open. |
| ~~Prompt 4~~ — C9 cell-swap world suction | ✅ **DONE 2026-08-25** — true suction on live prisms (`PrismSuctionConverge` + the four `_Suction*` stamps + `_Location` on BlockGraph / ExplodingBlockGraph). Root `localScale` kept for non-prism riders. Do not re-open. |
| ~~Prompt 13~~ — C11 spindle `_DeathAnimation` fade | ✅ **DONE 2026-08-25** — `PrismDeathClock` on SpindleGraph / AnimatedSpindleGraph; ordered wither is per-spindle `StartTime` offsets. Playtest outstanding → Prompt 11. Do not re-open. |
| ~~Prompt 3~~ — C6 remainder parent-scale | ✅ **DONE 2026-08-25** — ruling **(b)**: parent scale is mover-contract, same as locomotion. Root lerp kept; redundant `GrowToScale` `NotifyBodyPrismsMoved` deleted. Playtest outstanding → Prompt 11. Do not re-open. |
| ~~Prompt 14~~ — C13b environment-lay pooling | ✅ **DONE 2026-08-25** — unbounded prefab-keyed `EnvironmentPrismPool`; snap Blue then ChangeTeam clock-lerps Blue→domain. Flora HealthPrism Instantiates folded. Named not folded: Boid / SpawnableBase / SpawnableCord. Playtest outstanding → Prompt 11. Do not re-open as a maxSize-bounded pool or an `OnReturnToPool` wire. |
| ~~Prompt 15~~ — `ShapeDrawingManager` ruling | ✅ **DONE 2026-08-25** — **resolved by deletion** (C15). GUID only on its own `.meta`. Exclusive dependents deleted. SOAP events kept on live prism prefabs (inert — **never Raise them**). Do not re-open as a clock migration. |

Shared context every prompt inherits (do not restate in the session):
`Docs/PRISM_ANIMATION.md` is the LOCKED law — one stamp → GPU clock → one
scheduled end swap, gameplay state final at start, STRICT mode (no CPU
animation tier, fail-loud via `PrismClockDiagnostics`). GPU-first is a strong
prompter preference: never move math from GPU to CPU; camera/target-relative
values may be fed as GLOBAL shader uniforms (one write/frame, not per-prism).
The proven out-of-editor techniques (ShaderGraph JSON synthesis, prefab YAML
surgery, machine validation) are captured in the `/asset-surgery` skill — use it.

**Two facts worth carrying into any of these**, both learned on shipped
branches and both non-obvious:

- **Per-face data belongs on the MESH, not on the instance.** B4's prompt asked
  for "per-face bloom offsets as per-instance initial conditions". As shipped
  there is *no* per-face per-instance data at all: face centroids are baked into
  TEXCOORD1 on the **shared** mesh, so only four scalars are per-instance and
  same-size shields stay in ONE batch through the whole animation. Reach for a
  mesh channel before you reach for instance data.
- **A stamp API can silently no-op.** `PrismRenderService.Stamp*` gates on the
  matching override component existing on the entity's prototype. The `Prism`
  override set now carries grow, color, flight, shieldMorph, jiggle, **and
  suction** (C9 / Prompt 4 closed the gap that made `StampSuctionClock` return
  `false` on live prisms). The lesson is unchanged: check the prototype *and*
  the graph before assuming a stamp reaches the screen. SuctionGraph still
  owns SequentialFaceConverger; live graphs use `PrismSuctionConverge`.

---

## ~~Prompt 10~~ — Cross-doc truth reconcile ✅ DONE 2026-08-24

> Documentation-only. Closed so a fresh session is not led into a regression.
> **Do not re-run this prompt** — several of its own cited line numbers were
> already stale (do not trust them; the verified sites are below).
>
> **(a)** `PrismType.Grow` is LIVE. First producer 2026-08-09: Sparrow turret
> ReverseSuction (`FullAutoBlockShootActionExecutor.cs:476`, self-documented at
> `:55`), dispatched `PrismFactory.cs:198` → `SpawnGrow` `:448` → pooled
> `PrismImplosion.StartGrow` `:263`. D4 must keep that surface or port it onto
> the batched carrier. §4.6.1 opener no longer licenses deleting `PrismImplosion`.
>
> **(b)** §6 is a completed-handoff. Live remainder is Phase 5 / D3 + Phases 8/9/10.
>
> **(c)** §7 Enforcement is the edit-mode suites (CI via `bleeding-edge-guard.yml`,
> including `PrismClockWiringTests` which iterates Specs) plus python `--check`
> (Prompt 12) plus the two FrogletTools validators (editor-only; Specs are
> now CI-gated).
>
> **(d)** PhaseThresholds re-baseline is ✅ DONE 2026-08-02 (checklist Phase 6;
> `Docs/ECOSYSTEM.md` §18). Not open work.
>
> **(e)** C7 ✅ by construction. Verified chain:
> `PhyllotacticFlora.cs:432` `EnvironmentPrismPool.Get` (C13b 2026-08-25) → `:439` `AddHealthBlock` → `:440`
> `Initialize` → `Prism.BeginGrowthAnimation` → `PrismScaleAnimator.cs:219-237`
> (`StampClockGrowth`). Closes with C6; no flora-specific clock work.
>
> **(f)** Cell suction/drain cites are `StampRetiredWorldSuction` /
> `ReleaseRetiredWorld` / `RetireWorldIntoSuctionRoot` (line numbers drift;
> C9 shipped 2026-08-25 — GPU stamp + rider-scale wait). `PrismFlightClock` is
> a function in `PrismClockAnimation.hlsl`. `GameLoadSampler` also adds
> `PrismDebris.LiveDebrisCount` (`:43`).
>
> **(g)** Unsatisfiable DiagnosticsHUD Animators rows deleted. Phase 5 is
> Validate Clock Wiring + zero `[PrismClock]` errors. Phase 6 title is
> "✅ DONE PROGRAMMATICALLY".
>
> **(h)** Checklist Phase 3 ↔ Prompt 7 item (3): Serpent cloak
> (`CloakSeedWallActionExecutor.cs:387`) is the only live `IsTransparent`
> producer. Do not delete that family.

## Prompt 11 — The one editor session: measure the carrier, then close every outstanding playtest

> Nine items have been waiting on the same scarce resource — a human in the
> editor. Do them in one session, in this order; each is independently
> recordable, so a partial session still lands value. **Read
> `Docs/PRISM_CLOCK_WIRING_CHECKLIST.md` Phases 5, 8, 9, 10 and
> `Docs/PRISM_EXPLOSION_BENCHMARK.md` first** — they carry the exact steps and
> the acceptance criteria. Record every result *in those files* (C9 Cell Selector
> and C11 starve/joust also in `Docs/UNITY_VERIFICATION_CHECKLIST.md`); a playtest nobody wrote down
> did not happen.
>
> **(1) Measure the batched-debris carrier** (was the gate on D4 / Prompt 9b —
> death pooling shipped 2026-08-25; still record the numbers). Run the
> prism-grid benchmark per `PRISM_EXPLOSION_BENCHMARK.md` § "Re-profiling the
> death path", throttles lifted, and record the five `Prism.Destroy.*` markers'
> total+self ms plus GC/frame for the detonation frame, against a run at
> `f0ddfc21`. The markers exist (`Prism.cs:1084-1088`) so this is an attribution,
> not a guess. **The doc's ~0.43 ms SELF/death figure is STALE** — it was measured
> with `PrismExplosion.OnDisable` (1,863 ms) sitting unmarked inside the same
> region, which `f0ddfc21` removed. That file has never been edited since it was
> created; you are filling in the blanks, not correcting numbers.
>
> **(2) Playtest the suction half** (was also the D4 gate; fauna record is in
> §4.6.2). The grid rig produces ZERO
> implosions — every AOE death routes `Damage → Explode`; the only producer is
> `Prism.Consume`, i.e. fauna feeding. Watch a cell with fauna: mass converges on
> the eater **as it moves**, nothing pops, and the harness HUD's `debris` row
> (`N exp / N imp`) returns to `0 imp` when feeding stops. There is currently no
> place in the docs holding such a record — make one in §4.6.
>
> **(3) Phase 5 / D3 — the full verification pass.** Validate Clock Wiring →
> `RESULT: ✅ ALL REQUIRED WIRING PRESENT`, then a full play session with
> **zero `[PrismClock]` errors**, a just-laid ring colliding at full size while
> blooming, and hitstop/pause freezing prism animation. The old Animators-HUD
> rows are gone (Prompt 10g shipped) — do not hunt for
> `PrismScaleManager` / `MaterialStateManager` counts.
>
> **(4) Phase 8 — occlusion corridor (C1), 8 steps, never playtested.** Includes
> the nose-clearance buffer, the SHATTER lattice look at speed, the debris UV
> erosion wipe, and the per-vessel radii audit (**FrogletTools > Vessels > Audit
> Corridor Vessel Radii**).
>
> **(5) Phase 9 — shield morphs (B4), 7 steps, never playtested.** Note the one
> deliberate deviation to confirm rather than "fix": `BlueBlock.prefab` and
> `OctahedronShieldTest.prefab` serialized a hand-altered `AnimationCurve` (end
> tangents 2) and now ease like the fleet, a stated change of up to 0.192.
>
> **(6) Phase 10 — super-shield jiggle (C14).** First playtest passed; finish the
> remaining steps.
>
> **(7) C9 cell-swap suction (Prompt 4, shipped 2026-08-25).** Cell Selector in
> Menu_Main freestyle: swap cells. Old world **converges on the cell centre**
> behind the veil (does not snap, does not collapse in place). Zero `[PrismClock]`
> errors. Membrane / nucleus / cytoplasm still suction with the root. Record in
> `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
>
> **(8) C11 spindle death fade (Prompt 13, shipped 2026-08-25).** Starve a
> creature and joust one. Starvation withers extremity-first; joust withers
> heart-outward. Smooth, crystal drops at the right moment (starvation: when
> the wither reaches the core; joust: already freed at strike). Zero per-frame
> spindle writes in the profiler. Record in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
> **Same session, C6 parent-scale (Prompt 3, shipped 2026-08-25):** ~~a Squirrel
> Space-5 joust also fires `GrowToScale` — the body bloom is a root lerp, not
> a per-prism grow stamp.~~ **VOID since 2026-08-26** — `Fauna.GrowToScale` was
> deleted with lifeform levels (`Docs/ECOSYSTEM.md` §40) and that joust now
> `Nourish()`es rather than growing anything. Watch a worm-colony glide, which
> is the whole remaining test. Smooth; zero extra per-frame prism writes beyond
> locomotion's existing `NotifyBodyPrismsMoved` / `SyncBodyPrismsToIndex`.
> Record in the Prompt 3 UNITY checklist section.
>
> **(9) C13b environment-lay pooling (Prompt 14, shipped 2026-08-25).** Build a
> freestyle cell environment and the Wanderway belt. Prisms spawn `Domains.Blue`
> and **repaint to domain on the clock** (not Jade-from-frame-0). Profiler:
> environment-lay allocation churn drops vs a raw-Instantiate baseline.
> Conserved stock is **unchanged** across a Cell Selector swap (Wanderway belt
> stays; authored environment TryReleases). Record in
> `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
>
> Deliverable: the checklist phases ticked with observations, the benchmark table
> filled in, the C9 / C11 / C6-parent-scale / C13b look-verifies recorded, and a one-line go/no-go on D4.

## Prompt 12 — CI-gate the clock wiring ✅ DONE 2026-08-25

> Documentation-only remainder. The gate is live; **do not re-run this prompt**.
>
> **Lesson.** A `[MenuItem]` with zero callers is not a gate. Specs is the SoT —
> `PrismClockWiringTests` iterates it (grow / color / explosion / suction / flight
> plus shield-morph and jiggle, and since Prompt 8 also erosion / back-face /
> destruction sight; Prompt 4 added live-graph `PrismSuctionConverge`), under
> `Assets/_Scripts/Tests/Editor/` so it compiles into
> `Assembly-CSharp-Editor` and not the player. Python `--check` is the gate for
> the *node splices* that stay python-owned (and for opaque+clip / corridor
> topology); both run in `bleeding-edge-guard.yml` (python in `validate`; tests
> in the Unity EditMode job) and `unity-ci.yml`. Ten python gates as of Prompt 13
> (`wire_prism_spindle_death_clock.py` — spindle graphs only; do **not** dump
> `PrismDeathClock` onto BlockGraph Specs). `verify_prism_sight_composition.py`
> (needs clang++) and `fit_prism_erosion_cdf.py` (a fitter) stay out. `_PrismClock`
> is a global and is deliberately not asserted as Hybrid Per Instance.

## Prompt 13 — C11: spindle `_DeathAnimation` fade on the clock ✅ DONE 2026-08-25

> **Do not re-run this prompt.** C8 (Wanderway conveyor) and C9 (cell-swap suction)
> stay shipped — do not re-migrate them. Do **not** dump `PrismDeathClock` onto
> BlockGraph / ExplodingBlockGraph Specs; those graphs never carried `_DeathAnimation`.
>
> **Visual.** A withering creature **leaves its body prisms standing as a skeleton**
> (`LeaveSkeleton()` / `LeaveAsSkeleton` still run *before* any spindle stamp), so
> the spindles are the wither visual. `PrismDeathClock_float` on SpindleGraph and
> AnimatedSpindleGraph only: `State = Direction < 0 ? 1−p : p` off `_PrismClock`.
> Duration 0 → LegacyState (graphs leave it unconnected, default 0 = visible).
> Stamp `_DeathStartTime` / `_DeathDuration` / `_DeathDirection` once via MPB;
> `PrismTimerManager.ScheduleAction` settle; `SetPropertyBlock(null)` at settle.
>
> **Ordering (ecology-LOCKED).** Distance-sorted once at death; carried as
> `ForceWither(i * interval)` start-time offsets, never a per-frame cascade.
> Starvation = farthest-from-heart first; joust = heart-outward. Heart release
> still waits until the wither has reached the core (`count × interval` after
> the first stamp, matching the old wait that followed the LAST `ForceWither`).
>
> **SRP honesty.** A renderer WITH an MPB is still excluded from the SRP Batcher
> for the fade's ~1s — unique staggered StartTimes cannot share quantized fade
> materials without collapsing the wither order. What this recovers is (1) zero
> per-frame CPU and (2) SRP Batcher AFTER settle. Do not fade HealthPrisms
> (`additionalRenderedObjects` stays an explicit list). Collider budget: zero
> new colliders.
>
> Test (human in editor → `Docs/UNITY_VERIFICATION_CHECKLIST.md`): starve a
> creature and joust one — both directions, smooth, crystal at the right moment.

## Prompt 3 — C6 remainder: two parent-scale animations (re-scoped) ✅ DONE 2026-08-25

> **Do not re-run this prompt.** C7 stays done by construction. Wither stays C11
> (prisms stand as a skeleton; fade is the spindle's). Crystals
> (`Fauna.GrowCrystalWithPop` / `Crystal.Grow`) and `Boid.FadeOutAndRemove`
> stay out of scope. C6 remainder is devour/graze suction + boid husk shrink.
>
> **Ruling: (b)** — parent scale is mover-contract, same class as locomotion.
> A per-prism grow stamp cannot express a parent transform (the entity matrix
> is the composed world matrix). (a) — snap the root final and compensate with
> grow-clock stamps toward the new composed `localToWorld` — was rejected:
> `GrowToScale` is a creature-rig scale, not a prism blooming to a new leaf
> (that's C7). `GlideScales` is the scale twin of `FollowChain`. Cost of (b)
> is zero extra vs locomotion.
>
> **What shipped.** `Fauna.GrowToScale` still lerps the creature ROOT
> `localScale` (continuity — never a pop). The redundant
> `NotifyBodyPrismsMoved()` inside the lerp is **deleted** — `Boid`,
> `LightFauna`, and `WormFauna.SyncBodyPrismsToIndex` already re-sync every
> `Update`. `WormFauna.GlideScales` was already on that path (no extra notify).
> Colliders ride the live transform (zero new colliders; more honest for
> volume than snapping them final at stamp).
>
> ⚠ **SUPERSEDED 2026-08-26 — half of this is now gone.** `Docs/ECOSYSTEM.md`
> §40 retired lifeform LEVELS outright, and `Fauna.GrowToScale` /
> `GrowCrystalWithPop` were deleted with them: a lifeform is sized at spawn and
> never re-sized mid-life. `WormFauna.GlideScales` is the ONLY parent-scale
> animation left, and the (b) ruling still governs it. The Squirrel Space-5
> joust no longer grows anything — it `Nourish()`es (feeds/breeds) — so that
> half of the test below is **void**; run the worm-colony glide alone.
>
> Test (human in editor → `Docs/UNITY_VERIFICATION_CHECKLIST.md`): ~~Squirrel
> Space-5 joust growth +~~ worm-colony glide — smooth, zero `[PrismClock]`
> errors, zero *extra* per-frame prism writes in the profiler (locomotion
> writes remain; `GlideScales` must not add a second sync).

## Prompt 4 — C9: cell-swap world suction on the clock ✅ DONE 2026-08-25

> **Do not re-run this prompt.** C8 (Wanderway conveyor) stays shipped 2026-08-02
> — do not re-migrate it. C9 is visible *world retirement*, not off-screen
> transport: no `HideForTransport`, no `BeginBulkTransport` (stamps are
> `SetComponentData`; those two are for unseen conveyor mass).
>
> **Visual decision.** True suction — whole-prism lerp toward the cell centre —
> not C8's grow-clock collapse-in-place. `PrismSuctionClock_float` only outputs
> `State`; SuctionGraph's SequentialFaceConverger is the fauna-consumption look
> (per-face). Live graphs splice `PrismSuctionConverge_float` LAST on
> `VertexDescription.Position` (after the flight Add). World→object of the
> **point** (`w=1`); Shader Graph's Direction-mode Transform node normalizes —
> never use it here. Duration 0 → LegacyState; live graphs leave LegacyState
> unconnected (default 0 = identity). Do NOT add `_State` /
> `PrismImplosionStateOverride` to the live Prism set.
>
> **The stamp-API trap (closed).** `StampSuctionClock` gated on
> `HasComponent<PrismSuctionStartTimeOverride>` and returned `false` because the
> Prism prototype had no suction cluster (only the Implosion set did) and only
> SuctionGraph carried the clock. Fix: four `PrismSuction*Override` +
> `PrismImplosionLocationOverride` on `PrismRenderOverrideSet.Prism`;
> `PrismSuctionClock` + `PrismSuctionConverge` on BlockGraph and
> ExplodingBlockGraph (`Tools/Shaders/wire_prism_suction_clock.py`). Do not
> `AddComponentData` on live entities (archetype move) — components go on the
> prototype.
>
> **Sites.** `Cell.StampRetiredWorldSuction` walks `GetComponentsInChildren<Prism>`
> after `RetireWorldIntoSuctionRoot` and calls `Prism.StampSuctionToward` (disable
> collider, stamp, `ResetBoundsToMesh` + `EncapsulateBoundsPoint`). The root
> `localScale` wait loop is **kept for non-prism riders** (membrane / nucleus /
> cytoplasm / lifeform spindles) — instanced prism entities ignore parent scale,
> which is the bug; one write/frame on one transform is not the C9 inventory
> item. `ReleaseRetiredWorld` drain cadence (500/frame) is unchanged — gameplay
> de-registration, state not photons. Pooled returns call `ClearSuctionClockStamp`
> (`ClearPrismStamps` now includes it).
>
> Test (human in editor → `Docs/UNITY_VERIFICATION_CHECKLIST.md`): Cell Selector
> swap, smooth behind the veil, zero `[PrismClock]` errors, old world converges.

## Prompt 7 — C12 remainder + B1 simplifications ✅ DONE 2026-08-25

> Documentation + cleanup remainder. **Do not re-run this prompt.**
>
> **Lesson.** Two earlier wordings were actively harmful: (3) would have deleted the
> only live `IsTransparent` producer (checklist Phase 3); (4)/(5)/(6) needed the
> corrected premises (HoldCollider was itself a per-frame writer; TryReserve is
> growth-only so the 0.6 s window stays; arena settle needed an exposed analytic
> grow end first) — not "colliders already final / just shorten waitTime / just
> rewrite settle."
>
> **Outcomes.**
> 1. `PrismImplosion` watchdog → `PrismTimerManager` from `OnEnable` (re-arm after
>    stamp; cancel on complete/disable; never gates on `IsActive`).
> 2. `SkimFxRunner` writes only the beam particle — vessel FX, not prism animation;
>    recorded in §5 C12 / inventory so it stops being re-litigated.
> 3. Deleted only `CloakSeedWallAction.cs` (+ meta). SO / executor / asset kept on Serpent.
> 4. `HoldColliderAtFullSize` DELETED; `BoostRingBuilder.LayOne` applies all kinds at lay.
> 5. `CreateBlockCoroutine` 0.6 s window **kept** — `TryReserve` is growth/assembler-only;
>    trail/`PrismFactory` still rely on the disable window.
> 6. Exposed `PrismScaleAnimator.AnalyticSettleTime` / `Prism.AnalyticGrowSettleTime`;
>    arena `SettleGrowWatch` waits on clock predicates (no force-snap).

## Prompt 8 — Validator coverage + the wirer divergence ✅ DONE 2026-08-25

> Documentation-only remainder. The in-editor gate is live; **do not re-run this prompt**.
>
> **Lesson.**
> 1. Validate Clock Wiring ANDs `PrismOcclusionWiringValidator.CheckGraphWiring` so
>    the menu is not silently partial. Corridor props/CF stay off clock Specs —
>    one SoT, delegated not duplicated.
> 2. Specs names erosion / back-face / destruction sight (CF + load-bearing
>    edges). Unexposed globals (`_PrismSight*`) live in `UnexposedGlobals`, never
>    `RequiredProps` (that list asserts Hybrid Per Instance).
> 3. Auto-Wire stamps `_ShieldMorph*` on both live graphs AND declares that node
>    splices stay python-owned (`Tools/Shaders/wire_*.py`). Adding the properties
>    without naming the split would still silently diverge on the next CF revert.
> 4. SuctionGraph is a named **live** corridor exclusion (consumption VFX —
>    `ImplodingPrismMaterial` off `PrismImplosion.prefab` via
>    `PrismDebris.ConfigureImplosion`). Different from `KnownLegacyPrismPrefabs`
>    (DEAD). Do not add it to `WiredPrismShaderNames` without wiring
>    `PrismOcclusionFade` — `IsCorridorCapable` would then fail every suction
>    material at runtime.

## ~~Prompt 9b~~ — D4: retire the pooled explosion/implosion *death* path

✅ **DONE 2026-08-25.** Death pooling retired. Grow kept. Record of the
constraint that made it a refactor:

- Factory `SpawnExplosion` / `SpawnImplosion` are batch-only. A declined
  request **warns once and returns null** — no `Get()` of a pooled death
  GameObject.
- Authored config stays on the pool prefabs (`PrismDebris.Configure` /
  `ConfigureImplosion`). No new SO. Explosion pool is never Get()d and is
  not prewarmed. Implosion pool prewarm 64 → 12 (Grow-sized).
- `GameLoadSampler`: explosions = `PrismDebris.LiveDebrisCount`; implosions
  = `LiveImplosionDebrisCount` + `PrismImplosion.EnabledInstances`.
- **Do not delete `PrismImplosion` / `StartGrow` / `OnGrowCompleted`.**
  Sparrow ReverseSuction (`FullAutoBlockShootActionExecutor` →
  `PrismFactory.SpawnGrow`) is a live producer. Batched implosion has no
  completion-callback machinery; Grow stayed on the pooled class.
- Gate (Prompt 11 items 1+2) is recorded in `Docs/PRISM_ANIMATION.md` §4.6.2
  + `Docs/PRISM_EXPLOSION_BENCHMARK.md`. Do not re-open as a deletion.

## ~~Prompt 14~~ — C13b: environment-lay pooling (finally its own row)

✅ **DONE 2026-08-25.** Dedicated unbounded prefab-keyed `EnvironmentPrismPool`.
Record of the design that made it its own row:

- **This is not a clock fix.** C13a shipped 2026-08-02 — a pooled prism with a
  Shielded kind failed identically. Pooling was never the cause.
- **Snap Blue, then ChangeTeam clock-lerps.** A raw Instantiate cloned the
  prefab already wearing the final domain material, so `ChangeTeam(Blue→Jade)`
  stamped a Jade→Jade no-op. Do **not** restore "final domain material from
  frame 0" — that is the bug the row exists to kill.
- The existing vessel-trail pools cap inactive capacity and Destroy overflow
  on Release. Environment mass is never released during gameplay, so a naive
  pool-through either destroys conserved mass (ecology-law breach) or
  instantiates forever. `EnvironmentPrismPool` is unbounded and **never**
  Destroy-on-overflow.
- **Never wire the prism's pool-return delegate on Get.**
  `Cell.RetireWorldIntoSuctionRoot` gathers loose trail iff that delegate is
  set; wiring it would vacuum Wanderway stock. Membership = issued dict +
  `TryRelease`. Vessel trail still `ReturnToPool` before the drain.
- Flora HealthPrism Instantiates folded (`PhyllotacticFlora` / `BranchingFlora`
  / `AssembledFlora`). Named, not folded: `Boid.cs` body, `SpawnableBase`
  non-prism `leafPrefab`, `SpawnableCord`.
- Playtest outstanding → Prompt 11 item (9). Do not re-open as a
  capacity-capped pool, an Interactive pool reuse, or a C13a clock-path fix.

## ~~Prompt 15~~ — Rule on `ShapeDrawingManager` (§3.8 had no tracker row)

✅ **DONE 2026-08-25.** **Resolved by deletion** (C15), the C4/C10 outcome.
Flagged in §3.8 with no §5 row, which is why every sweep missed it — now
owned as §3.8 **#11** + §5 **C15**.

- `ShrinkPrismsIntoShape` per-frame `transform.position` / `localScale` Lerp,
  no `SyncRenderTransform` / `NotifyPositionChanged`. Unreachable: GUID
  `d375b1129a0a4e29b505296c9e510bdc` lived only on its own `.meta` after
  `MinigameFreestyle.unity` was removed.
- Exclusive dependents deleted with it: `ShapeDrawingCrystalManager`,
  `EndShapeDetailHUD`, `ShapeScoreDisplay`, `ShapeScoreData`.
- **Kept:** `ShapeDefinition` (painting toy), `SpawnableShapeBase` + spawnable
  shapes, `ShapeSign` / `ShapeCollisionTrigger` / `SpawnableShapeSign` /
  `ModeSelectTrigger`, `SegmentSpawner` (SkimRace live), SOAP events
  `EventOnShapeGameModeStarted` / `EventOnShapePrismReturnToPool` (wired on
  live prism prefabs to `Prism.ReturnToPool` — only the deleted manager
  `Raise()`d them; **never Raise them**; do not strip the EventListeners).
- Do **not** re-open as a clock migration of unreachable code. Do **not**
  delete `ShapeDefinition`, the painting converter, the SOAP events, or
  `SegmentSpawner`. The painting toy is the successor (scoreless).

## Prompt 16 — Corridor dither strobe: the 3D-SHARD successor

> `Docs/PRISM_CLOCK_WIRING_CHECKLIST.md` (Phase 8, ~`:323-331`) and
> `PRISM_ANIMATION.md` §4.7 record this as a **known open issue** (2026-08-10)
> with the design space already explored — read both before proposing anything, so
> you do not re-derive a rejected candidate.
>
> State of play: surfaces stacked along one camera ray read the same
> screen-anchored threshold and moiré-beat. **SHATTER3D** (Voronoi polyhedra cut
> by crack planes) passed every fidelity number and was **REJECTED ON LOOK** the
> day it shipped — a crack plane lying near-parallel to a viewed surface makes a
> face-sized plate share one threshold and flash. **The depth-parallax domain
> shear was also rejected**: it moved the whole lattice, so at speed it crawled
> coherently and read as worse flicker than the beat.
> `PRISM_OCCLUSION_SHATTER_DEPTH_PHASE` ships at **0** — measured, useful
> decorrelation needs ~50× the rate the speed budget allows. That leaves
> `PRISM_BACKFACE_POWER` and `PRISM_OCCLUSION_MORPH_RATE` as the only live levers.
>
> The noted successor direction is **3D-SHARD: a distance-to-owner fill**, whose
> level sets are closed surfaces and therefore cannot lie flat against a face —
> the specific failure that killed SHATTER3D.
>
> Two rules this prompt inherits from the ones that failed: a candidate must pass
> the coverage-fidelity number **and earn its look on real mass at speed**, and a
> fix that moves the pattern globally cannot win against speed. Use
> **FrogletTools > Ecology > Prism Animation > Occlusion Dither Lab** — it drives
> kernel + scale as shader globals live in play mode through the shipped GPU code,
> runs the real |coverage − alpha| admission rule against the shipped baseline
> measured in the same pass, and bakes the winner back into the constants. Do not
> judge by editing `#define`s.

**Status 2026-08-25 — implemented, awaiting look.** Kernel 6 is in
`PrismOcclusionCorridor.hlsl` (dispatch both LIVE and shipped chains). CDF
LO=0.155 / HI=0.915, clang compiled |coverage−alpha| = 0.00783 (python fit
0.00740). Glancing-plane proof: SHATTER3D is constant on constructed crack
planes (415/417 < 1e-4); SHARD3D F1 varies on all 417/417 (min 0.0178). Lab
popup includes Shard3D. **Do not mark DONE. Do not Bake as CURRENT.** Judge
in Occlusion Dither Lab on real mass at speed.

---

Maintenance: when a prompt ships, move its row to the **Shipped** table with the
date + commit, delete the prompt body if following it would now cause harm
(otherwise keep it under a `✅ DONE` block for the lesson), and update
`Docs/PRISM_ANIMATION.md` §5. **That protocol lapsed between 2026-08-07 and
2026-08-15 — four items shipped without it; Prompt 10 closed the resulting
drift on 2026-08-24; Prompt 12, Prompt 8, Prompt 9b, Prompt 14, and Prompt 15 shipped 2026-08-25 under it.** If a session
discovers a new trap or technique,
fold it into the `/asset-surgery` skill (that's the `/ship` retrospective step).
