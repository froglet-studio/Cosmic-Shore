# Prism Clock — Follow-Up Branch Prompts

Ready-to-paste prompts for the work remaining after the clock-material migration
branch (`claude/prism-animation-audit-*`). Each is self-contained for a FRESH
session: it names the docs to read first, the scope, the constraints, and the
in-editor test that closes it. Run them as separate branches.

**PRIORITY ORDER (set at ship, 2026-08-02) — work top-down:**

| # | Prompt | Why this rank |
|---|---|---|
| ~~1~~ | ~~**Prompt 2** — C13 environment-lay prisms miss the clock path~~ | ✅ **DONE 2026-08-02** — the cause was the shield engage-morph straddling the creation reveal (not the raw-`Instantiate` lay). Residual **C13b** (pooled lay / spawn repaint) is not a clock fix — re-rank it with the rest. |
| 2 | **Prompt 9** — batched entity debris remainder | Completes the proven, playtest-loved carrier: implosions on the batch path + the measured next bottleneck (`AOE.ResolveDamage` 0.43 ms/kill self, per-kill `PrismEventData` alloc). The benchmark rig is already built to measure it. |
| 3 | **Prompt 1** — transparent-prism occlusion restore | Pre-existing broken system (predates this branch) + it gates the one deferred wiring verification (transparent color fades). |
| 4 | **Prompt 3** — fauna/flora on the clock (ecology) | Per-frame CPU prism writes in every cell scene; wither/devour are ecology-locked visuals that must ride the law. |
| 5 | **Prompt 4** — conveyor + cell-swap suction | The two biggest world-scale per-frame CPU flows left. |
| 6 | **Prompt 6** — B4 shield morphs on the GPU | Retires the last sanctioned CPU ticker (`PrismOctahedronShieldManager`). |
| 7 | **Prompt 5** — projectile prism paths | Real but lower-traffic (Sparrow volleys, fire trails). |
| 8 | **Prompt 7** — C12/B1 cleanup sweep | Simplifications unblocked by the migration; no player-visible change. |
| 9 | **Prompt 8** — HUD/validator upkeep | Ride-along with any of the above, not its own branch. |

Shared context every prompt inherits (do not restate in the session):
`Docs/PRISM_ANIMATION.md` is the LOCKED law — one stamp → GPU clock → one
scheduled end swap, gameplay state final at start, STRICT mode (no CPU
animation tier, fail-loud via `PrismClockDiagnostics`). GPU-first is a strong
prompter preference: never move math from GPU to CPU; camera/target-relative
values may be fed as GLOBAL shader uniforms (one write/frame, not per-prism).
The proven out-of-editor techniques (ShaderGraph JSON synthesis, prefab YAML
surgery, machine validation) are captured in the `/asset-surgery` skill — use it.

---

## Prompt 1 — Restore the transparent-prism occlusion system (then verify its color fades)

> The transparent prism system is not working. Its purpose: limit prism
> occlusion between the camera and the vessel — prisms in that corridor render
> transparent so the player can see their ship, while the rest of the
> environment stays cheap opaque prisms. Read `Docs/PRISM_ANIMATION.md` (§3 C1
> "ClearPrisms shader-side occlusion fade" + §5 tracker) and investigate the
> current state: find what historically set `prismProperties.IsTransparent` /
> swapped prisms to the team transparent material (`MaterialPropertyAnimator.
> SetTransparency`, `ThemeManagerDataContainerSO.GetTeamTransparentBlockMaterial`,
> any `ClearPrisms`-style camera-corridor component), determine why it no longer
> runs, and restore it — redesigned to conform to the clock law: occlusion is
> camera-relative live data, so implement the fade IN THE SHADER off GLOBAL
> uniforms (camera position + vessel position + corridor radius written once per
> frame by one system — the §1 moving-target exception class), not per-prism CPU
> material swaps or per-instance writes. Prisms outside the corridor must cost
> nothing extra. State the collider/draw-call impact. In-editor test: fly so a
> prism wall sits between camera and vessel — the corridor goes see-through and
> recovers when you move off; opaque environment unaffected.
>
> AFTER the system is restored, run the deferred verification from
> `Docs/PRISM_CLOCK_WIRING_CHECKLIST.md` Phase 3: steal a TRANSPARENT prism with
> the skimmer (and watch a danger/shield repaint on one) — the recolor must fade
> ~0.8s on the GPU clock with zero `[PrismClock]` errors (the color cluster is
> already wired into ExplodingBlockGraph; this test was deferred solely on this
> system being down).

## Prompt 2 — C13: environment-lay / SegmentSpawner prisms miss the clock path (live repro)

> **✅ DONE 2026-08-02** (branch `claude/prismclock-render-entity-bug-fe3z2d`). The
> prompt's three suspects were all wrong, and so was its preferred fix: the raw
> `Instantiate` lay is innocent (a *pooled* prism with a `Shielded` kind failed
> identically — `BoostRingBuilder` only escaped by deferring shield kinds to
> `onGrown`). The real cause: a shield **engage-morph** holds `_exoticVisualActive`
> for 0.35 s, `Prism.ApplyRenderPath` refused to create the companion entity while
> that flag was set, and `CreateBlockCoroutine` reveals the prism after 0.1 s — one
> frame under the load gate — so the reveal landed inside the morph and the ONE-SHOT
> grow stamp had nothing to stamp. It hit every `ShieldedSpawnablePrism` (the HexRace
> track block) and every `PrismKind.Shielded`/`SuperShielded` environment prism (the
> freestyle six + the Wanderway palette). Fixed by separating entity existence from
> entity visibility, adding a stamp-site self-heal + fact-based diagnosis, and the
> "birth rule" (a shield engaged during creation snaps — the grow-in bloom is the
> continuity). See `Docs/PRISM_ANIMATION.md` §3.8 #10 + §4.5.
>
> **Residual, still open:** C13b — the *pooling* half (`PrismTrailBuilder.LayOne` →
> pooled pull with the final domain material, killing the `Domains.Blue` → domain
> spawn repaint). Worth doing, but it is not a clock fix and it needs its own
> environment-prefab pool design (the existing pools are `maxSize`-bounded and
> environment mass is never released).

> Live repro from the editor: `[PrismClock] STRICT MODE: no companion render
> entity to stamp (grow:SpawnablePrism (Clone))` raised from
> `PrismScaleAnimator.StampClockGrowth` ← `BeginGrowthAnimation` ←
> `Prism.CreateBlockCoroutine` (Prism.cs ~line 723). `SetRenderVisible(true)`
> runs BEFORE the stamp in that coroutine and calls `EnsureRenderEntity`, so the
> entity creation itself is declining — investigate why for
> SegmentSpawner-instantiated prisms (`SpawnablePrism`/`ShieldedSpawnablePrism`
> clones; also used by the Wanderway `ConveyorToy`): likely suspects are the
> `PrismRenderService.Enabled`/config gate at that moment, a missing/mismatched
> sharedMaterial or meshFilter at entity-creation time, or `activeInHierarchy`
> during streamed lay. Read `Docs/PRISM_ANIMATION.md` §5 C13 (environment-lay
> pooling — these paths bypass PrismFactory pooling via raw Instantiate) and fix
> so every environment-laid prism rides the instanced path and blooms on the
> clock. Prefer routing through the canonical pooled lay path over patching the
> raw-Instantiate one. Test: HexRace track build + Menu_Main Wanderway conveyor
> — zero `[PrismClock]` errors, smooth blooms, DiagnosticsHUD draw calls stay
> batched.

## Prompt 3 — C6/C7: fauna wither + devour + flora growth on the clock (ecology)

> Use the `/ecology` skill (locked invariants; wither-to-crystal + mass
> conservation + continuity of existence are directly in play). Read
> `Docs/PRISM_ANIMATION.md` §5 C6 + C7 and migrate the fauna/flora prism visual
> transitions to the clock: fauna wither (extremity-first evaporation on
> starvation/predation — farthest-from-centre spindles first, emergent from
> geometry), devour suction (already rides `PrismImplosion` — verify each call
> site stamps rather than steps), level-up/growth pulses, and flora leaf growth.
> Every transition = one stamp + scheduled swap; gameplay state (volume, spatial
> index, crystals) final at the stamp. The wither ordering is per-prism start-time
> offsets computed ONCE at death (distance-sorted), not a per-frame cascade.
> Collider budget: state the impact (should be zero — colliders go final at
> stamp). Test: starve a creature (wither runs extremity-inward, crystal drops),
> let fauna graze a trail, watch flora regrow — all smooth, zero `[PrismClock]`
> errors, zero per-frame prism writes in the profiler.

## Prompt 4 — C8/C9: microscene conveyor + cell-swap suction on the clock

> Read `Docs/PRISM_ANIMATION.md` §5 C8 + C9 and `Docs/ToySystem/ARCHITECTURE.md`
> (Wanderway conveyor; Cell Selector). Migrate the two big world-scale suction/
> bloom flows to clock stamps: (C8) the conveyor's recycle — suction-out of the
> scene behind the vessel and bloom-in ahead (conserved stock, `MicrosceneConveyor`)
> — and (C9) `Cell.RequestCellSwap`'s world suction + regrowth (drains 500
> prisms/frame today). Both currently step per-frame state on the CPU. Suction
> stamps need the bounds envelope (`PrismRenderService.EncapsulateBoundsPoint`
> toward the convergence point — same culling bug class as explosions). The
> 500/frame drain cadence may remain as the GAMEPLAY de-registration slicer
> (state, not photons) — but each prism's VISUAL must be one stamp. Test: ride
> the Wanderway and watch recycling at the field edge; swap cells at the Cell
> Selector — both smooth behind/through their veils, zero `[PrismClock]` errors.

## Prompt 5 — C4/C5: projectile prism paths (FireTrailBlock + turret anchors)

> Read `Docs/PRISM_ANIMATION.md` §5 C4 + C5. (C4) `FireTrailBlock` bypasses the
> pool with raw Instantiate/Destroy — route it through PrismFactory pooling and
> clock blooms (a bare Destroy of a visible prism is a continuity-law bug). (C5)
> Sparrow `FullAutoBlockShootActionExecutor` turret anchor prisms fly on a
> per-frame CPU position update; the flight is p(t) = muzzle + dir·min(speed·t,
> stopDistance) — a pure function of time. Move it into the vertex stage off
> stamped {t₀, velocity, stopDistance} per-instance properties on the prism graph
> (pattern-match the explosion's ObjectOffset chain: CPU stamps ONE world-space
> vector, the shader does the world→object conversion with the raw
> inverse-model multiply — never a Direction-mode Transform node, it normalizes).
> Expand RenderBounds to the flight envelope at the stamp. The entity transform
> goes FINAL at the anchor point immediately (collider/gameplay at destination —
> confirm with the prompter if gameplay currently collides mid-flight). Test:
> Sparrow full-auto volley — blocks fly and anchor smoothly, zero errors.

## Prompt 6 — B4: shield morphs on the GPU

> Read `Docs/PRISM_ANIMATION.md` §5 B4 and the shield-octahedron section of
> CLAUDE.md (Key Systems). The octahedron/stellated shield engage + shatter
> morphs still tick on the CPU (`PrismOctahedronShieldManager` — the one
> remaining sanctioned CPU ticker, marked do-not-extend). Migrate: morph
> progress = f(clock, stamp) in the shield shader (per-face bloom offsets are
> per-instance initial conditions), keep the exotic-visual handoff
> (`SetExoticVisualActive`/`SetRenderMeshOverride` — a bare MeshFilter swap
> renders nothing, see CLAUDE.md anti-patterns), and retire the manager when its
> active set is provably always empty. The shared-mesh caches
> (`GetSharedShieldMesh`) stay — same-size prisms must keep batching. Test:
> shield engage/disengage + super-shield + AOE shatter overlays — smooth, zero
> errors, one batch per shield size class.

## Prompt 7 — C12 remainder + B1 simplifications (cleanup sweep)

> Read `Docs/PRISM_ANIMATION.md` §5 C12 + B1's pending list. Small independent
> items, one commit each: (1) `PrismImplosion` wall-clock watchdog → one
> `PrismTimerManager` scheduled check instead of per-instance `Update()`; (2)
> `SkimFxRunner` stretch-beam review (per-frame prism writes? migrate or
> document as vessel-FX-not-prism); (3) `CloakSeedWall` dead code removal; (4)
> `Prism.HoldColliderAtFullSize` deletion (colliders are final-at-start now);
> (5) `CreateBlockCoroutine` spawn-window simplification (the 0.6s
> collider-disable window predates claim-before-spawn — verify against
> `PrismSpatialIndex.TryReserve` and simplify); (6) arena-gate simplification
> (`PrismTrailBuilder` settle logic can read the analytic clock settle times).
> Each: grep blast radius first, brace-check, verify no behavior change beyond
> the stated one.

## Prompt 8 — DiagnosticsHUD + validator upkeep (small, do with any of the above)

> The retired CPU animation managers were deleted (D2, 2026-08-02) — sweep
> `DiagnosticsHUD` / benchmark docs for any remaining "Animators" rows or
> references that read them (GameLoadSampler is already re-sourced to
> `PrismSpatialIndex.LiveCount` + effect `EnabledInstances`), and extend
> `PrismClockWiringValidator` with any new graph the C-phase work touches
> (turret flight properties, shield morph properties) so Validate Clock Wiring
> stays the one-stop wiring truth.

## Prompt 9 — Batched pure-entity debris: the REMAINDER (implosions + death-path self cost)

> The explosion half SHIPPED on the audit branch (2026-08-02): `PrismDebris` +
> `PrismRenderService.SpawnExplosionDebrisBatch` spawn every prism-death
> explosion as batched entities (one `em.Instantiate(prototype, N)`, one
> batched visibility strip, sweep-based batch retirement, full 5s duration,
> pooled path = fallback only), after a lifted-throttle profile showed 2,408
> pool misses costing 1.9s of one frame in `PrismExplosion.OnDisable` alone.
> Read `PrismDebris.cs` for the shipped pattern, then finish the job:
> (1) **Implosions/suction-grow on the same carrier** — add an Implosion-set
> batch spawn (suction stamps `{t₀, duration, ±direction, growDelay,
> location}`); the moving-convergence refresh needs a records list carrying
> the target Transform so the sweep can also update `_Location` for live
> targets (one float3 per record per frame, the §1 documented exception) —
> or keep moving-target implosions pooled and batch only the fixed-point
> majority. (2) **Death-path self cost** — with the carrier fixed, re-profile
> the lifted-throttle blast: `AOE.ResolveDamage` showed ~0.43ms SELF per
> death (1,047ms for 2,408) plus ~1.4KB GC per death (`PrismEventData` is a
> class allocated per kill). Split it with markers (SetupDestruction /
> spatial-index MarkDestroyed / event-channel raise), pool or struct-ify the
> event data, and kill whatever per-death work doesn't earn its keep.
> (3) Once implosions are batched and parity is proven, consider retiring the
> pooled explosion fallback entirely. Measure before/after with the prism-grid
> benchmark (`Docs/PRISM_EXPLOSION_BENCHMARK.md`), throttles lifted.

---

Maintenance: when a prompt ships, delete its section and update
`Docs/PRISM_ANIMATION.md` §5. If a session discovers a new trap or technique,
fold it into the `/asset-surgery` skill (that's the `/ship` retrospective step).
