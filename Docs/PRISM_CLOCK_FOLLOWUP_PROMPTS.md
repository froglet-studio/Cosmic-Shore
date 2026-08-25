# Prism Clock — Follow-Up Branch Prompts

Ready-to-paste prompts for the work remaining after the clock-material migration
branch. Each is self-contained for a FRESH session: it names the docs to read
first, the scope, the constraints, and the in-editor test that closes it. Run
them as separate branches.

> **Audited + re-set 2026-08-15; Prompt 10 shipped 2026-08-24.** Four statements
> in the 2026-08-07 revision were not merely stale but **actively harmful** —
> a session following them would have deleted a live code path, resurrected
> deleted code, deleted the only asset the last outstanding Phase-3 test
> depends on, and re-wired already-wired graphs. Prompt 10 closed that drift.

**PRIORITY ORDER (re-set 2026-08-24) — work top-down:**

| # | Prompt | Why this rank |
|---|---|---|
| 1 | **Prompt 11** — the one editor session (measure + every outstanding playtest) | Six separate items all need the same scarce resource — a human in the editor. Three shipped systems (corridor, shield morph, jiggle remainder) have never been look-verified, D3/Phase 5 has never run, and the debris carrier has never been measured. One session instead of six, and it is the gate on D4. |
| 2 | **Prompt 12** — CI-gate the clock wiring | The five foundational clusters (grow, color, explosion, suction, flight) have **no automated gate at all**: `PrismClockWiringValidator` is a menu item with zero callers. A graph revert on any of them merges silently today. B4/C1/C14 each shipped a CI-run edit-mode test; the core did not. |
| 3 | **Prompt 8** — validator coverage (re-scoped) | Its original ask is code-complete; what replaced it is real — four live shader families the validator never names, and an Auto-Wire → Validate loop that no longer closes. Pairs naturally with Prompt 12. No longer a ride-along. |
| 4 | **Prompt 4** — C9 cell-swap suction (re-scoped, C8 shipped) | The largest world-scale per-frame CPU flow left. Ranked below the gates because it needs a prerequisite decision: `StampSuctionClock` **silently no-ops on live prisms** today. |
| 5 | **Prompt 13** — C11 spindle `_DeathAnimation` fade | The only §5 tracker row with zero coverage anywhere in this doc. Since prisms leave as a skeleton *before* the wither runs, this is the *entire* remaining CPU cost of the wither visual, not a leftover. |
| 6 | **Prompt 3** — C6 remainder (re-scoped; C7 closes ✅) | Two parent-transform scale animations that re-sync every child prism's entity matrix per frame. Real but low-traffic, and needs a design ruling first. |
| 7 | **Prompt 7** — C12/B1 cleanup sweep (re-scoped) | All six items verified still open, but two had false premises and are corrected below. No player-visible change. |
| 8 | **Prompt 9b** — D4: retire the pooled path (gated on Prompt 11) | A refactor, not a deletion. The pooled `PrismImplosion` has a live gameplay consumer (`PrismType.Grow` / Sparrow ReverseSuction) — do not delete that surface. |
| 9 | **Prompt 14** — C13b environment-lay pooling | Not a clock fix. Kills the `Domains.Blue` → domain spawn repaint and alloc churn. Told to be "re-ranked with the rest" on 2026-08-02 and never was; this row is that re-rank. |
| 10 | **Prompt 15** — `ShapeDrawingManager` ruling (§3.8 #8) | A textbook clock-law violation on dormant code — which is exactly why every sweep has missed it. Cheap to decide; expensive if Phase-2 shape drawing is revived carrying it. |
| 11 | **Prompt 16** — corridor dither strobe successor | An open *look* problem the checklist names explicitly, with the successor direction already reasoned out and two live levers holding the line. Pure polish; no correctness risk in deferring. |

**Shipped — do not re-open.** Their prompt bodies are deleted where following
them would now cause harm; the DONE blocks below keep the lesson.

| Prompt | Outcome |
|---|---|
| ~~Prompt 1~~ — transparent-prism occlusion restore | ✅ **DONE 2026-08-04** — C1, restored as a shader-side corridor off two global uniforms and promoted to a PLATFORM LAW (`Docs/PRISM_ANIMATION.md` §4.7). Playtest still outstanding → Prompt 11. |
| ~~Prompt 2~~ — C13a environment-lay prisms miss the clock path | ✅ **DONE 2026-08-02** — cause was the shield engage-morph straddling the creation reveal, not the raw-`Instantiate` lay. Residual C13b → **Prompt 14**. |
| ~~Prompt 5~~ — projectile prism paths | ✅ **DONE 2026-08-07** — C5 shipped as `PrismFlightClock`; C4 resolved by deletion (`cc9a1f5b`). |
| ~~Prompt 6~~ — B4 shield morphs on the GPU | ✅ **DONE 2026-08-15** — PR #729, `37f9596a`. **Phase B is complete**; the last sanctioned CPU prism ticker is deleted. |
| ~~Prompt 9~~ (build half) — batched entity debris | ✅ **DONE 2026-08-04** — implosions on the batch carrier + the death-path marker split. Editor half → Prompt 11; D4 → Prompt 9b. |
| ~~Prompt 10~~ — cross-doc truth reconcile | ✅ **DONE 2026-08-24** — Grow is live (Sparrow ReverseSuction); §6 is a completed-handoff; PhaseThresholds re-baseline ✅ 2026-08-02; C7 ✅ by construction; unsatisfiable Animators-HUD rows gone. Do not re-open. |

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
  matching override component existing on the entity's prototype
  (`PrismRenderService.cs:429-460`). The `Prism` override set carries grow,
  color, flight, shieldMorph and jiggle — **not suction**. Check the override set
  and the graph before assuming a stamp reaches the screen.

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
> **(c)** §7 Enforcement is the edit-mode suites (CI via `bleeding-edge-guard.yml`)
> plus the two FrogletTools validators (editor-only, **not** CI — Prompt 12).
>
> **(d)** PhaseThresholds re-baseline is ✅ DONE 2026-08-02 (checklist Phase 6;
> `Docs/ECOSYSTEM.md` §18). Not open work.
>
> **(e)** C7 ✅ by construction. Verified chain:
> `PhyllotacticFlora.cs:432` Instantiate → `:439` `AddHealthBlock` → `:440`
> `Initialize` → `Prism.BeginGrowthAnimation` → `PrismScaleAnimator.cs:219-237`
> (`StampClockGrowth`). Closes with C6; no flora-specific clock work.
>
> **(f)** Cell suction/drain cites are `Cell.cs:2097-2108` /
> `:2191-2206` / `:2218` (the original prompt's `:1932` / `:2015` were already
> stale). `PrismFlightClock` is a function in `PrismClockAnimation.hlsl`.
> `GameLoadSampler` also adds `PrismDebris.LiveDebrisCount` (`:43`).
>
> **(g)** Unsatisfiable DiagnosticsHUD Animators rows deleted. Phase 5 is
> Validate Clock Wiring + zero `[PrismClock]` errors. Phase 6 title is
> "✅ DONE PROGRAMMATICALLY".
>
> **(h)** Checklist Phase 3 ↔ Prompt 7 item (3): Serpent cloak
> (`CloakSeedWallActionExecutor.cs:387`) is the only live `IsTransparent`
> producer. Do not delete that family.

## Prompt 11 — The one editor session: measure the carrier, then close every outstanding playtest

> Six items have been waiting on the same scarce resource — a human in the
> editor. Do them in one session, in this order; each is independently
> recordable, so a partial session still lands value. **Read
> `Docs/PRISM_CLOCK_WIRING_CHECKLIST.md` Phases 5, 8, 9, 10 and
> `Docs/PRISM_EXPLOSION_BENCHMARK.md` first** — they carry the exact steps and
> the acceptance criteria. Record every result *in those files*; a playtest
> nobody wrote down did not happen.
>
> **(1) Measure the batched-debris carrier** (gates D4 / Prompt 9b). Run the
> prism-grid benchmark per `PRISM_EXPLOSION_BENCHMARK.md` § "Re-profiling the
> death path", throttles lifted, and record the five `Prism.Destroy.*` markers'
> total+self ms plus GC/frame for the detonation frame, against a run at
> `f0ddfc21`. The markers exist (`Prism.cs:1084-1088`) so this is an attribution,
> not a guess. **The doc's ~0.43 ms SELF/death figure is STALE** — it was measured
> with `PrismExplosion.OnDisable` (1,863 ms) sitting unmarked inside the same
> region, which `f0ddfc21` removed. That file has never been edited since it was
> created; you are filling in the blanks, not correcting numbers.
>
> **(2) Playtest the suction half** (also gates D4). The grid rig produces ZERO
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
> Deliverable: the checklist phases ticked with observations, the benchmark table
> filled in, and a one-line go/no-go on D4.

## Prompt 12 — CI-gate the clock wiring (new)

> The five foundational clock clusters — **grow, color, explosion, suction,
> flight** — have no automated protection whatsoever. `PrismClockWiringValidator`
> is a `[MenuItem]` with **zero callers** anywhere in `Assets/` (the only hit
> outside its own file is a doc comment in `PrismOcclusionDiagnostics.cs:39`), so
> a ShaderGraph revert, a bad merge, or a reimport that drops a property block
> merges green today. Meanwhile the three *newest* systems each shipped an
> edit-mode test that DOES run in CI (`bleeding-edge-guard.yml` runs
> `-testPlatform EditMode`): `PrismShieldMorphTests`, `PrismOcclusionCoverageTests`,
> `PrismSuperShieldJiggleTests`. The core is the unprotected part.
>
> Close it the way the project already closes this class of problem — an
> edit-mode test under an `Editor/` folder (see CLAUDE.md: a test anywhere else
> compiles into the player and breaks the Windows build at the linker). Parse the
> two live-prism graphs' JSON and assert every required property is present with
> `hlslDeclarationOverride: 3` (Hybrid Per Instance) plus the Custom Function node
> for each family, reusing `PrismClockWiringValidator`'s Specs as the single
> source of truth rather than duplicating the list. `PrismShieldMorphTests` is the
> pattern to copy — it already does exactly this for the four `_ShieldMorph*`
> properties.
>
> Second half: **none of the nine `Tools/Shaders/*.py` scripts run in CI** (only
> `Tools/Build/check_conditional_compilation.py` does). Several have a `--check`
> mode that is the authoritative gate for a family the C# validator does not
> cover — `wire_prism_backface_fade.py`, `wire_prism_destruction_sight.py`,
> `enable_prism_alpha_clip.py` in particular. Wire the `--check` invocations into
> the guard workflow next to the existing python step; they need no Unity licence
> and cost seconds.
>
> Test: revert one property block in `BlockGraph.shadergraph` locally → the new
> test fails with a message naming the property and the graph. Restore → green.

## Prompt 13 — C11: spindle `_DeathAnimation` fade on the clock (new)

> Use the `/ecology` skill. `Docs/PRISM_ANIMATION.md` §5 **C11** is the only
> tracker row with no coverage anywhere in this doc, and it is not a leftover: a
> withering creature **leaves its body prisms standing as a skeleton**
> (`LightFauna.cs:247 LeaveSkeleton()` runs *before* the wither ordering), so the
> spindles are what the wither visual actually is. C11 is therefore the entire
> remaining CPU cost of that visual, not a fringe of it.
>
> The per-frame code is `Spindle.cs`: `SetFadeValue` (`:229`),
> `EvaporateCoroutine` (`:237`, stepping `deathAnimation += Time.deltaTime *
> animationSpeed` at `:257`) and `CondenseCoroutine` (`:275`). Migrate to a stamp
> + clock input on the spindle graphs (`SpindleGraph` / `AnimatedSpindleGraph`) —
> same shape as the prism color cluster: state final at the stamp, one scheduled
> settle, no per-frame writes.
>
> Two constraints specific to this one. **The ordering must survive**: a starving
> creature withers extremity-first (farthest-from-the-heart spindles evaporate
> before the core — an ecology-LOCKED visual), and a jousted one runs the same
> geometry backwards from the heart outward. That ordering is already computed
> ONCE at death (`LightFauna.cs:250-252`, distance-sorted) — carry it as per-spindle
> start-time offsets in the stamp, never as a per-frame cascade. And note the
> current MPB fade **excludes the renderer from the SRP Batcher**; state whether
> the migration recovers that, because it is the perf argument for doing it.
>
> Test: starve a creature and joust one — both run in the correct direction,
> smooth, with zero per-frame spindle writes in the profiler and the crystal
> still dropping at the right moment.

## Prompt 3 — C6 remainder: two parent-scale animations (re-scoped)

> **C7 is done by construction — do not migrate flora.** Leaf spawn already
> routes `PhyllotacticFlora.cs:432` Instantiate → `:439` `AddHealthBlock` →
> `:440` `Initialize` → `BeginGrowthAnimation` →
> `PrismScaleAnimator.StampClockGrowth`. Tracker row is ✅.
>
> **The wither does not touch prisms either** (see Prompt 13 — the prisms are
> left standing as a skeleton and the fade is the *spindle's*). What is genuinely
> left of C6 is narrow and needs a design ruling before any code:
>
> **`Fauna.GrowToScale` (`Fauna.cs:585-597`)** — the one true per-frame
> prism-entity write in the whole ecology. It animates the creature ROOT's
> `localScale` (`:592`) and then calls `NotifyBodyPrismsMoved()` (`:593` →
> `:763-772`), re-syncing every body prism's entity matrix every frame.
> **`WormFauna.GlideScales` (`WormFauna.cs:483-494`)** is the same shape,
> worm-colony only.
>
> The ruling you need first: **a per-prism grow stamp cannot express a PARENT
> transform scale** — the entity matrix is the composed world matrix. So either
> (a) set the root scale final immediately and re-stamp each body prism's own
> grow clock toward its new composed `localToWorld`, or (b) declare parent scale
> part of the mover contract, as locomotion already is. Decide with the prompter
> before writing code; (b) is defensible and cheap, (a) is the law-pure answer.
>
> **Free either way:** delete the `NotifyBodyPrismsMoved()` call at `Fauna.cs:593`.
> It is redundant — `Boid.cs:780`, `LightFauna.cs:945` and `WormFauna.cs:319-320`
> all already sync every frame from their own `Update`.
>
> **Out of scope unless the prompter rules them in:** `Fauna.GrowCrystalWithPop`
> (`:552-583`) and `Crystal.Grow` (`Crystal.cs:520-543`) are crystal transforms,
> not prisms; `Boid.FadeOutAndRemove` (`:580-591`) is a husk shrink with no
> prisms. `LifeFormCrystal` has zero coroutines — the original prompt named it in
> error.
>
> Collider budget: zero (colliders go final at stamp). Test: trigger a Squirrel
> Space-5 joust growth and a worm-colony glide — smooth, zero `[PrismClock]`
> errors, zero per-frame prism writes in the profiler.

## Prompt 4 — C9: cell-swap world suction on the clock (re-scoped)

> **C8 shipped 2026-08-02 — do not re-migrate the conveyor.**
> `Microscene.AnimateScaleAsync` is deleted and the per-frame
> `NotifyPrismPositions` sweep is gone. Use it as the **reference
> implementation**: `Microscene.cs:153 RecycleAsync` → `:169 StampCollapseAsync`
> → `:178 Prism.HideForTransport` → `:179` ONE `SetPositionAndRotation`,
> bracketed by `Prism.BeginBulkTransport`/`EndBulkTransport`.
>
> Read `Docs/PRISM_ANIMATION.md` §5 C9 + §3.8 #1 and `Docs/ToySystem/ARCHITECTURE.md`
> (Cell Selector). Migrate `Cell.RequestCellSwap`'s retiring-world suction.
>
> **Read this before scoping — the obvious approach silently does nothing.**
> `PrismRenderService.StampSuctionClock` gates on
> `HasComponent<PrismSuctionStartTimeOverride>` (`PrismRenderService.cs:969`), and
> the `PrismRenderOverrideSet.Prism` prototype adds grow / color / flight /
> shieldMorph / jiggle but **no suction components** (`:429-460`) — only the
> `Implosion` set does. Likewise only `SuctionGraph.shadergraph` carries
> `PrismSuctionClock`; neither `BlockGraph` nor `ExplodingBlockGraph` does. A
> literal suction stamp on a live prism returns `false` today. So choose:
> - **Extend the suction cluster to live prisms** — add the four
>   `PrismSuction*Override` components (+ `PrismImplosionLocationOverride`) to the
>   `Prism` override set and splice `PrismSuctionClock_float`
>   (`PrismClockAnimation.hlsl:132`) into both live-prism graphs. True convergence
>   on the cell centre; costs graph surgery.
> - **Or reuse C8's technique** — a grow-clock re-stamp toward zero via
>   `Prism.TargetScale`, which already works on live prisms with **no** graph or
>   entity work. Each prism collapses IN PLACE rather than converging.
>
> Decide which visual is wanted before starting.
>
> Sites — all in `Assets/_Scripts/Controller/Environment/Cell.cs`. **Line numbers
> are a hint, the symbol is the anchor**: this file keeps shifting, so re-grep
> before trusting a number. Verified 2026-08-24:
> - **Stamp at** `RetireWorldIntoSuctionRoot` (`:2218`) — it re-parents the
>   authored environment as ONE container (`:2226-2227`), so a per-prism stamp
>   needs an explicit `GetComponentsInChildren<Prism>` there, or reuse the walk
>   `ReleaseRetiredWorld` already does (`:2196`).
> - **Delete** the per-frame root `localScale` write — the `while (elapsed <
>   duration)` suction loop at `:2097-2108`, writing at `:2106`. Keep a plain
>   wall-clock wait for `duration`.
> - **Keep** `ReleaseRetiredWorld`'s drain cadence (`:2191-2206`, `const int
>   PrismsPerFrame = 500` at `:2195`) — that is gameplay de-registration, state
>   not photons.
>
> Three things not to miss: pair any suction stamp with `ResetBoundsToMesh` +
> `EncapsulateBoundsPoint(objectPoint(cellCentre), padding)` — copy the shape at
> `PrismImplosion.cs:315-320`, it is the same culling bug class as explosions; add
> `ClearSuctionClockStamp` coverage for the pooled prisms returned to their pool at
> `Cell.cs:2110-2119` (`ClearPrismStamps` does not include it); and note the
> retiring root also carries **non-prism** objects — membrane / nucleus /
> cytoplasm are re-parented onto it at `Cell.cs:2259-2263`, and lifeform bodies
> just above — which ride the root transform correctly today. Removing the root
> animation removes their suction, so either keep the root scale for those and
> stamp only prisms, or give them their own transition.
>
> Test: swap cells at the Cell Selector — smooth behind the veil, zero
> `[PrismClock]` errors, and the old world genuinely converges rather than
> snapping.

## Prompt 7 — C12 remainder + B1 simplifications (cleanup sweep, re-scoped)

> Read `Docs/PRISM_ANIMATION.md` §5 C12 + B1's pending list. Small independent
> items, one commit each. **Two of the six had false premises in the previous
> revision of this doc — the corrections are load-bearing, not pedantic.**
>
> **(1) `PrismImplosion` wall-clock watchdog → scheduler.** `PrismImplosion.cs:438`
> still has a `void Update()`; `PrismExplosion` has none and schedules at `:376`.
> Careful: the watchdog deliberately does **not** gate on `IsActive` (comment
> `:441-447`), so a `ScheduleAction` issued from `StartImplosion` would miss the
> failure mode it covers (pool re-activation where `StartImplosion` never runs).
> Schedule from `OnEnable`, cancel/re-arm at `StartImplosion` / `StartGrow` /
> `OnEffectComplete`.
>
> **(2) `SkimFxRunner` stretch-beam review.** Verified: it writes only the beam
> (`:83`/`:85`), never a prism — the per-frame loop at `:65` with
> `UniTask.Yield` at `:78`/`:93` is vessel FX, not prism animation. **The
> deliverable is recording that outcome** in §5 C12 so it stops being
> re-litigated (`PRISM_ANIMATION.md:406` and `:1826` still list it pending).
> Optional and out of clock-law scope: it runs one loop per skim contact and
> `Instantiate`/`Destroy`s per contact instead of pooling.
>
> **(3) `CloakSeedWall` — delete ONE file, not the family.** ⚠ The previous
> wording invited deleting all of it. Only
> `_Scripts/Controller/Vessel/VesselActions/CloakSeedWallAction.cs` (+ meta, guid
> `e4f432f2…`) is dead — the legacy `ShipAction`, zero asset references.
> `CloakSeedWallActionSO.cs`, `CloakSeedWallActionExecutor.cs` and
> `_SO_Assets/VesselActions/Serpent/CloakSeedWallAction.asset` are all wired into
> `Serpent.prefab`, and the executor (`:387`) is the project's **only live
> `IsTransparent` producer** — which the last outstanding checklist **Phase 3**
> transparent-steal test depends on (`PRISM_CLOCK_WIRING_CHECKLIST.md` § Phase 3).
> Deleting it would make that test unrunnable.
>
> **(4) `Prism.HoldColliderAtFullSize` — the premise "colliders are final-at-start
> now" is false on this very path.** ⚠ `HoldColliderAtFullSizeCoroutine`
> (`Prism.cs:298-331`) writes `transform.localScale` (`:323`) **and**
> `blockCollider.size` (`:325-328`) inside `while (!destroyed && blockCollider)`
> (`:302`, yielding at `:330`) — it is itself a surviving per-frame prism-transform
> writer, which makes it *more* worth removing, not less. There is exactly **one**
> live caller, `BoostRingBuilder.cs:115` (the other six hits are comments/doc
> prose). Deleting it means replacing two things that caller depends on: a
> full-size collider from frame 0 on a just-spawned ring, AND the deferred
> `onGrown` callback that delays `Shielded`/`SuperShielded` application until the
> bloom ends.
>
> **(5) `CreateBlockCoroutine` spawn-window simplification.** The 0.6 s
> collider-disable window is `Prism.cs:31` / `:843-861`. Before shortening it,
> confirm the trail and environment lay paths also claim before spawn — the only
> `PrismSpatialIndex.TryReserve` callers found are the growth/assembler spawners
> (Gyroid, SchwarzP, PhyllotacticFlora), so the window may still be the sole
> protection for `PrismTrailBuilder.LayOne` and `PrismFactory`.
>
> **(6) Arena-gate simplification is blocked — two commits, not one.**
> `PrismTrailBuilder.cs:264 PollArenaReady` → `:292 SettleGrowWatch` (2000 at
> `:213`) → `:342 CompleteGrowthImmediately`. No per-prism analytic grow end time
> (start stamp + duration) is exposed on `Prism`/`PrismScaleAnimator`. Expose it
> first, then rewrite the settle logic.
>
> **B1's PhaseThresholds re-baseline is DONE 2026-08-02** (Prompt 10(d) closed
> the tracker contradiction). Do not re-author the six freestyle configs.
>
> Each: grep blast radius first, brace-check, verify no behavior change beyond
> the stated one.

## Prompt 8 — Validator coverage + the wirer divergence (re-scoped)

> **The original ask is already done — do not redo it.** `DiagnosticsHUD.cs` has
> zero references to Animators or to any deleted manager; `GameLoadSampler` is
> re-sourced (and adds `PrismDebris.LiveDebrisCount` at `:43`); and
> `PrismClockWiringValidator` already requires all four `_ShieldMorph*`, all
> three `_Flight*` and all three `_Jiggle*`. Residual present-tense Animators-HUD
> text was struck in Prompt 10(g).
>
> What is actually open:
>
> **(1) Four live shader families the validator never names.** Both live-prism
> graphs carry them and nothing in-editor checks any of them:
> `PrismOcclusionFade` (delegate to / cross-reference `PrismOcclusionWiringValidator`
> so "Validate Clock Wiring" is not silently partial), `PrismErosionFade`
> (ExplodingBlockGraph — no new properties, so check the CF node + edge shape),
> `PrismBackFaceFade` (both graphs, no properties), and `PrismDestructionSight`
> plus its five unexposed globals `_PrismSightApex` / `Axis` / `Gape` / `Params` /
> `Strength` (both graphs). Today the last two are covered *only* by
> `Tools/Shaders/wire_prism_backface_fade.py --check` and
> `wire_prism_destruction_sight.py --check`, and **nothing runs those** (Prompt 12
> fixes the running; this fixes the in-editor gate).
>
> **(2) Auto-Wire → Validate no longer closes.** `PrismClockGraphWirer` has no
> `_ShieldMorph*` properties while `PrismClockWiringValidator` requires all four
> (the shield morph was wired by python, not by the C# wirer). Either add them to
> the wirer, or make the wirer explicitly declare which families it delegates to
> the `Tools/Shaders` scripts — silently diverging is the thing to fix.
>
> **(3) Rule on `SuctionGraph`'s corridor exclusion, and write the ruling down.**
> It is in the clock validator's Specs but excluded from
> `PrismOcclusionDiagnostics.WiredPrismShaderNames` (`:54-58`, BlockGraph and
> ExplodingBlockGraph only) and from `PrismOcclusionWiringValidator.GraphPaths`.
> Consequence: batched implosion debris renders with `ImplodingPrismMaterial` on
> `SuctionGraph` (`PrismDebris.ConfigureImplosion:234` reads `sharedMaterial` off
> `PrismImplosion.prefab`, that material's only reference), so **suctioning mass
> can never fade in the occlusion corridor**. Either wire it, or record it as a
> deliberate exclusion the way the four `KnownLegacyPrismPrefabs` are
> (`PrismOcclusionWiringValidator.cs:60-66`).

## Prompt 9b — D4: retire the pooled explosion/implosion path (gated on Prompt 11)

> **Gate:** Prompt 11 items (1) and (2) — the measurement and the fauna
> playtest — must land first. Read `Docs/PRISM_ANIMATION.md` §4.6 + §4.6.1 before
> starting; they carry the carrier's rules and the retirement assessment.
>
> **It is a refactor, not a deletion.** The pool prefabs are the batched path's
> CONFIG source (`PrismDebris.Configure` / `ConfigureImplosion` read mesh /
> material / layer / clamp band / duration off them), and two consumers read
> `EnabledInstances` (the `PrismEffectsManager` zombie audit and
> `GameLoadSampler`). Decide where the authored effect config lives *first*.
>
> ⚠ **New blocker the tracker does not know about.** The previous revision of this
> prompt told you to "fold in the now-provably-dead `PrismType.Grow` surface
> (`PrismFactory.SpawnGrow`, `PrismImplosion.StartGrow`/`growDelay`,
> `PrismEventData.OnGrowCompleted`) — it has no producer anywhere in Assets."
> **That is false as of 2026-08-09.** It has a live producer: the Sparrow turret's
> ReverseSuction visual
> (`FullAutoBlockShootActionExecutor.cs:476`, dispatched at `PrismFactory.cs:198`
> → `SpawnGrow` `:448`). So the pooled `PrismImplosion` is now a live **gameplay
> carrier**, not just a fallback route — D4 must either port `StartGrow` onto the
> batched carrier or keep the pooled class for it. Do not delete that surface.
> Mirror claims in `PRISM_ANIMATION.md` §3.8 #8 / §4.6.1 are closed (Prompt 10a).

## Prompt 14 — C13b: environment-lay pooling (finally its own row)

> Read `Docs/PRISM_ANIMATION.md` §5 **C13b**. `PrismTrailBuilder.LayOne` raw-
> `Instantiate`s environment prisms with the final domain material already applied,
> which kills the `Domains.Blue` → domain **spawn repaint** and churns allocations.
> Route it through the canonical pooled lay path.
>
> **This is not a clock fix** (C13a was, and it shipped 2026-08-02 — a *pooled*
> prism with a `Shielded` kind failed identically, so pooling was never the cause).
> It is worth doing on its own merits, and it needs its own design: the existing
> pools are `maxSize`-bounded and **environment mass is never released**, so a
> naive pool-through either destroys conserved mass on release (an ecology-law
> breach) or instantiates forever. Design the environment-prefab pool before
> touching the call site.
>
> Adjacent, same class, worth folding in or at least naming so the next session
> does not re-discover them: `PhyllotacticFlora.cs:432` and
> `BranchingFlora.cs:180` also spawn leaves with raw `Instantiate`.
>
> Test: build a freestyle cell environment and the Wanderway belt — prisms spawn
> `Domains.Blue` and repaint to domain on the clock, allocation churn drops in the
> profiler, and the conserved stock is unchanged across a cell swap.

## Prompt 15 — Rule on `ShapeDrawingManager` (§3.8 #8 has no tracker row)

> `Docs/PRISM_ANIMATION.md` §3.8 #8 flags it and **no §5 row owns it**, which is
> why every sweep has missed it. `ShapeDrawingManager.cs:434-450` snapshots
> `prisms[i].transform.localScale`, then inside a per-frame loop writes
> `prisms[i].transform.position = Vector3.Lerp(...)` **and**
> `prisms[i].transform.localScale = Vector3.Lerp(...)` — a textbook clock-law
> violation that also bypasses the render bridge and the spatial index.
>
> It is **dormant**: zero `.unity`/`.prefab` references (its wiring died with the
> removed `MinigameFreestyle.unity`; shape drawing is the deferred lava-lamp Phase
> 2). That is the whole reason it survives.
>
> Decide, with the prompter, between the two outcomes this project already has
> precedent for: **resolve by deletion** (the C4/C10 outcome — migrating a path
> nothing can execute ships an untested one), or **migrate + give it a §5 row**
> if Phase-2 shape drawing is genuinely coming back. Either way the deliverable
> includes a tracker row so it stops being invisible. If deletion: grep the GUID
> across all scenes/prefabs first, and check what else in the shape-drawing family
> (`SegmentSpawner` shape triggers, `ShapeDrawingCrystalManager`,
> `EndShapeDetailHUD`) goes with it.

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

---

Maintenance: when a prompt ships, move its row to the **Shipped** table with the
date + commit, delete the prompt body if following it would now cause harm
(otherwise keep it under a `✅ DONE` block for the lesson), and update
`Docs/PRISM_ANIMATION.md` §5. **That protocol lapsed between 2026-08-07 and
2026-08-15 — four items shipped without it; Prompt 10 closed the resulting
drift on 2026-08-24.** If a session discovers a new trap or technique,
fold it into the `/asset-surgery` skill (that's the `/ship` retrospective step).
