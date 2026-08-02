# Prism Clock Wiring — In-Editor Checklist

Companion to `Docs/PRISM_ANIMATION.md` (§4.4, §6). **STRICT MODE is live: no legacy
fallback.** All four wiring phases below are ✅ wired programmatically in-branch —
what remains is in-editor verification (Phase 5) and the deletion pass (Phase 6).
If any graph reverts to unwired, prisms SNAP to end states and the console logs
one `[PrismClock]` error per unwired material.

**Already done programmatically on this branch — not your job:**

- ✅ All 18 clock **properties** are inserted into the three graphs
  (donor-cloned, Hybrid Per Instance, registered in the blackboard): BlockGraph
  (grow trio + color five), ExplodingBlockGraph (explode trio + grow trio for
  transparent live prisms), SuctionGraph (suction four). They should appear on each
  graph's Blackboard when you open it.
- ✅ The HLSL (`PrismClockAnimation.hlsl`), all C# stamps/scheduling/diagnostics,
  and the tools below.

**Your tools:**

- `Tools > Cosmic Shore > Prism Animation > Validate Clock Wiring` — run after every
  phase. Out of the box it should show every property row ✅ and the Custom Function
  node rows ❌ — the node wiring is exactly what's left.
- `Tools > Cosmic Shore > Prism Animation > Auto-Wire Clock Properties` — idempotent
  repair tool: re-adds any clock property that's missing (e.g. after a graph revert).
  Normally reports "already present".
- `Tools > Cosmic Shore > Prism Animation > Smoke Test - Re-Bloom Nearby Prisms`
  (play mode) — stamps a from-zero regrow on nearby prisms: wired = smooth GPU
  bloom with 0 active CPU animators; unwired = snap + errors.

**First open**: if Unity flags anything on importing the modified graphs (they were
edited out-of-editor — expected clean, every block is schema-exact), the recovery is
`git checkout` of the `.shadergraph` + run Auto-Wire Clock Properties in-editor,
which does the identical insertion with automatic rollback on import error.

---

## Phase 1 — BlockGraph grow nodes — ✅ WIRED + PLAYTEST-CONFIRMED

Done out-of-editor and committed: `_PrismClock` global feed + `PrismGrowScale` Custom Function
(source = `PrismClockAnimation.hlsl`, GUID pinned by its committed `.meta`) +
property feeds + Multiply spliced into the one edge that fed Vertex ▸ Position
(`Prism Sub Graph #1 → Multiply.A`, `Scale → Multiply.B`, `Multiply → Position`).
Every object reference machine-validated.

**✅ PLAYTEST-CONFIRMED** — Squirrel right-trigger ring, trail lay, and gyroid
growth all bloom smoothly on the GPU clock.

## Phase 2 — BlockGraph color nodes — ✅ WIRED + PLAYTEST-CONFIRMED

Done and committed: `PrismColorLerp` CF intercepts the three property→subgraph
feeds (existing `BrightColor`/`DarkColor`/`Spread` nodes → Target inputs; CF
outputs → subgraph; start colors + times from new property nodes; Clock ←
`_PrismClock`).

**✅ PLAYTEST-CONFIRMED** — skimmer-steal repaints fade smoothly (0.8s)
instead of snapping. Shield engage/danger repaints likewise. (The octahedron
shield MORPH itself is still the CPU-ticked B4 item — only its color fade is
clock-driven.)

<details><summary>Manual steps (reference only — already done)</summary>

- [ ] Add a **Custom Function** node — Source same file, Name **`PrismColorLerp`**.
      Inputs: `Clock` Float · `StartTime` Float · `Duration` Float · `StartBright`
      Vector4 · `StartDark` Vector4 · `StartSpread` Vector3 · `TargetBright` Vector4
      · `TargetDark` Vector4 · `TargetSpread` Vector3. Outputs: `Bright` Vector4 ·
      `Dark` Vector4 · `Spread` Vector3.
- [ ] Wire: the `PrismClock` property node → `Clock`; `ColorStartTime` → `StartTime`, `ColorDuration` →
      `Duration`, `StartBrightColor`/`StartDarkColor`/`StartSpread` → the Start
      inputs; **the EXISTING `BrightColor`/`DarkColor`/`Spread` property nodes →
      the Target inputs** (the bound material's values ARE the lerp targets).
- [ ] Re-route: every place the graph consumed `_BrightColor`/`_DarkColor`/`_Spread`
      directly now consumes the node's `Bright`/`Dark`/`Spread` outputs.
- [ ] Save → Validate → play: shield engage / steal / danger transitions fade
      smoothly; BlockGraph `[PrismClock]` errors gone.

- Save → Validate → play: shield engage / steal / danger transitions fade
  smoothly; BlockGraph `[PrismClock]` errors gone.

</details>

## Phase 3 — ExplodingBlockGraph nodes — ✅ WIRED + PLAYTEST-CONFIRMED (color cluster added after, one test left)

Done and committed: `PrismExplosionClock` CF (Amount/Opacity re-routes + object-
space flight offset added into the vertex chain) + the `PrismGrowScale` cluster
(transparent live prisms bloom).

**Fix round (after first playtest — wrong debris direction + wrong culling):**

- **Direction (GPU-side, locked)**: the original wiring converted the velocity
  world→object with a **Direction-mode Transform node**, which emits
  `TransformWorldToObjectDir` — and that function **NORMALIZES**: the magnitude
  is destroyed and the direction re-skews under the prism's non-uniform scale.
  The conversion now lives **inside `PrismExplosionClock` itself** as a raw,
  unnormalized `mul((float3x3)GetWorldToObjectMatrix(), Velocity·t)`. The CPU
  stamps ONE world-space `_Velocity`, shared by the flight offset AND the
  shatter-spin axis chain — all matrix math stays on the GPU. (An interim CPU
  conversion (`_ExplodeVelocityOS`) shipped briefly and was reverted on the
  prompter's direction: nothing moves from GPU to CPU, matrix math especially.)
- **Culling**: `RenderBounds` are reset to the mesh's authored bounds then
  expanded one-shot at stamp time to the whole flight envelope
  (`PrismRenderService.ResetBoundsToMesh` + `ExpandBoundsForClockAnimation`) —
  the entity matrix never moves, so without this the debris frustum-culled
  against the unexploded box (visible faces vanished / off-screen faces drew).
  Bounds are the one legitimate CPU-side computation: frustum culling itself
  runs on the CPU, and the envelope is one-shot initial-conditions data, not
  animation. Reset-before-expand keeps pooled reuse from compounding envelopes.

**✅ PLAYTEST-CONFIRMED 2026-08-02** — debris flies in the impact direction,
shatters, and fades smoothly on the GPU clock, and stays visible across the
whole flight.

**Color cluster added (post-confirmation wrap-up):** the color five properties +
the `PrismColorLerp` cluster are now wired into this graph too (bright/dark
intercepted at the Prism Sub Graph feeds, spread at the explosion spread-chain
Add — same shape as BlockGraph). Transparent live prisms now FADE on
steal/repaint instead of snapping, and the last expected one-time
`[PrismClock] _ColorStartTime` errors on transparent materials are gone.

**Test: steal a TRANSPARENT prism with your skimmer** (or watch a danger/shield
repaint on one) — the recolor fades over ~0.8s like the opaque prisms do, with
zero `[PrismClock]` errors.

<details><summary>Manual steps (reference only — already done)</summary>

- [ ] Custom Function **`PrismExplosionClock`** — Inputs: `Clock` Float ·
      `StartTime` Float · `Speed` Float · `Duration` Float · `Velocity` Vector3 ·
      `LegacyAmount` Float · `LegacyOpacity` Float. Outputs: `Amount` Float ·
      `Opacity` Float · `ObjectOffset` Vector3.
- [ ] Wire: the `PrismClock` property node → `Clock`; `ExplodeStartTime`/`ExplodeSpeed`/`ExplodeDuration`
      → `StartTime`/`Speed`/`Duration`; the existing world-space `Velocity`
      property → `Velocity` (the SAME node that feeds the shatter-spin chain —
      the HLSL does the world→object conversion internally, unnormalized);
      existing `ExplosionAmount` property → `LegacyAmount`; existing `Opacity`
      property → `LegacyOpacity` (the Legacy inputs keep `TransparentPrismMaterial`
      — a LIVE prism material resting at `_ExplosionAmount = 0` — rendering
      correctly).
- [ ] Re-route: `Amount` replaces downstream `_ExplosionAmount` uses; `Opacity`
      replaces `_Opacity` uses.
- [ ] `ObjectOffset` is OBJECT-space: **Add** it to the object-space vertex
      position → Vertex ▸ Position. No Transform node anywhere in this chain.
- [ ] **Transparent live prisms bloom**: also add the `PrismGrowScale` cluster here
      (grow properties + `PrismClock` already exist on this graph's Blackboard).
      Tip: copy-paste the Custom Function + Multiply nodes from BlockGraph, then
      re-drag the LOCAL Blackboard properties (including `PrismClock`) into the
      inputs — don't paste property nodes across graphs.
- [ ] Save → Validate → play: debris flies/shatters/fades smoothly; transparent
      prisms bloom on spawn and render correctly at rest.

- Save → Validate → play: debris flies/shatters/fades smoothly; transparent
  prisms bloom on spawn and render correctly at rest.

</details>

## Phase 4 — SuctionGraph nodes — ✅ WIRED + PLAYTEST-CONFIRMED

Done and committed: `PrismSuctionClock` CF (Clock ← `_PrismClock`, the suction
four → their inputs, existing `_State` property → `LegacyState`; the `State`
output replaces the ONE downstream `_State` use — `SequentialFaceConverger`'s
LerpAmount). `_Location` stays untouched (live moving-target exception, Position-
mode world→object transform — correct math, unlike the explosion's Direction-mode
transform that lost scale/magnitude). Bounds ship with it: the stamp sites
(`PrismImplosion.StampClockStrict`) reset to the mesh then encapsulate the
convergence point (`ResetBoundsToMesh` + `EncapsulateBoundsPoint`), and the
per-frame location refresh keeps the envelope covering a wandering sink at
no-op cost while it stays inside.

**✅ PLAYTEST-CONFIRMED 2026-08-02** — prisms suck smoothly into the moving
creature (and fauna-spawned prisms grow OUT of it via `StartGrow`, the reverse
suction), staying visible across the whole collapse.

<details><summary>Manual steps (reference only — already done)</summary>

- [ ] Custom Function **`PrismSuctionClock`** — Inputs: `Clock` Float · `StartTime`
      Float · `Duration` Float · `Direction` Float · `GrowDelay` Float ·
      `LegacyState` Float. Output: `State` Float.
- [ ] Wire: the `PrismClock` property node → `Clock`; `SuctionStartTime`/`SuctionDuration`/
      `SuctionDirection`/`SuctionGrowDelay` → their inputs; existing `State`
      property → `LegacyState`. The `State` output replaces every downstream
      `_State` use. (`_Location` stays untouched — live moving-target exception.)
- [ ] Save → Validate → play: fauna grazing sucks prisms into the moving creature
      smoothly.

</details>

## Phase 5 — Full verification (§4.4 protocol)

- [ ] **Validate Clock Wiring** → `RESULT: ✅ ALL REQUIRED WIRING PRESENT`
- [ ] Full play session (menu freestyle + one HexRace) with **zero `[PrismClock]`
      errors**
- [ ] DiagnosticsHUD Animators: `PrismScaleManager` / `MaterialStateManager` **0
      active** everywhere, under any load
- [ ] A just-laid ring collides at full size while still visibly blooming
- [ ] Hitstop / pause freezes prism animation (scaled clock — expected)

## Phase 6 — Deletion pass (D2) + chores — ✅ DONE PROGRAMMATICALLY (one item left)

Executed in-branch 2026-08-02 (every removal machine-verified reference-free
across code AND scenes/prefabs before deletion):

- [x] `PrismScaleManager.cs` deleted; its component removed from
      `PrismManagers.prefab` (the only asset reference)
- [x] `MaterialStateManager.cs` deleted; component removed from `PrismManagers.prefab`
- [x] `PrismEffectsManager.cs` slimmed: `ProcessExplosions`/`ProcessImplosions`, both
      Burst jobs, job-data structs, and the registration APIs/lists deleted — class
      KEPT (clock convergence tracking + dev zombie audit)
- [x] `AdaptiveAnimationManager.cs` deleted (no other subclasses; the
      `AdaptivePerformanceSetting` graphics setting is documented INERT)
- [x] Dead manager-era surface stripped: `MaterialPropertyAnimator` (`IsAnimating`,
      `AnimationProgress`, `OnAnimationComplete`, `Current*`, `*4` mirrors,
      `Duration`, manager registration) and `PrismScaleAnimator` (`IsScaling`,
      `LastStepTime`, `OwnerPrism`, `Initialize`, manager registration); callers
      updated (`Prism`, `FullAutoBlockShootActionExecutor`); benchmark counts
      re-sourced (`GameLoadSampler` → `PrismSpatialIndex.LiveCount` +
      effect `EnabledInstances`)
- [x] `TrailViewer` component removed from `Urchin.prefab`; file deleted
- [ ] **YOURS, in-editor** — re-baseline PhaseThresholds (volume is final at spawn
      now): `Tools > Cosmic Shore > Measure Cell Environment Baselines` + update
      cell configs per `Docs/ECOSYSTEM.md` §18

**In-editor sanity after pulling this phase**: open `PrismManagers.prefab` and
`Urchin.prefab` in the inspector — there must be NO "Missing (Mono Script)" rows
(the components were excised, not orphaned). Enter play mode once; the console
must show no compile errors and no `[PrismClock]` errors.

## Phase 7 — Follow-up branches (post-wiring, own PRs)

Tracker items (`Docs/PRISM_ANIMATION.md` §5 C-phase) landing per-path on the wired
graphs, each following the shipped B1/B3 templates: C1 `ClearPrisms` shader-side
occlusion fade · C4 `FireTrailBlock` pool/Destroy fix · C5 turret anchor flight ·
C6 fauna wither/devour/level-up · C7 flora growth · C8 microscene conveyor ·
C9 cell-swap suction · C10 worm shift · C11 spindle fade · C13 environment-lay
pooling · B4 GPU shield morphs.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Prism snaps + `[PrismClock] ... does not declare '_X'` | Property missing (graph reverted?) | Run **Auto-Wire Clock Properties**; reimport |
| Pops in fully grown, NO errors | Clock domains mismatched (the original Time-node bug) or `_PrismClock` exposed/not published | Clock input must come from the `_PrismClock` property node (unexposed global); validator checks it |
| Snaps, property exists, error persists | Not Hybrid Per Instance / wrong reference | Validator names it; fix Node Settings |
| Everything magenta after a graph edit | Graph failed to compile | Undo / `git checkout` the `.shadergraph`, redo; Auto-Wire self-rolls-back |
| Growth smooth but colors pop | Phase 2 outputs not re-routed | Finish the `Bright`/`Dark`/`Spread` re-route |
| Transparent prisms snap on spawn | `PrismGrowScale` cluster missing on ExplodingBlockGraph | Phase 3 last step |
| Debris flies in the wrong direction | A Direction-mode Transform node in the velocity chain — `TransformWorldToObjectDir` NORMALIZES (magnitude gone, skewed by non-uniform scale) | The conversion lives inside `PrismExplosionClock`'s HLSL (raw unnormalized `GetWorldToObjectMatrix()` multiply); feed it the world-space `_Velocity` directly |
| Debris vanishes mid-flight / draws when it shouldn't | `RenderBounds` still the unexploded box | Stamp site must call `ResetBoundsToMesh` + `ExpandBoundsForClockAnimation` (any vertex-displacing clock animation needs this) |
| `[PrismClock] ... no companion render entity` | Instanced rendering off / no ECS world | `PrismRenderConfig` ▸ Use Instanced Rendering ON |
| DiagnosticsHUD shows active CPU animators | Something re-engaged a retired manager | Law regression — find the caller; it should not exist |
