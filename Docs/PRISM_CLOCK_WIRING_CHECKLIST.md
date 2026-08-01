# Prism Clock Wiring — In-Editor Checklist

Companion to `Docs/PRISM_ANIMATION.md` (§4.4, §6). **STRICT MODE is live: no legacy
fallback.** Until the node wiring below lands, prisms SNAP to end states and the
console logs one `[PrismClock]` error per unwired material.

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

## Phase 1 — BlockGraph grow nodes — ✅ WIRED PROGRAMMATICALLY IN-BRANCH

Done out-of-editor and committed: Time node + `PrismGrowScale` Custom Function
(source = `PrismClockAnimation.hlsl`, GUID pinned by its committed `.meta`) +
property feeds + Multiply spliced into the one edge that fed Vertex ▸ Position
(`Prism Sub Graph #1 → Multiply.A`, `Scale → Multiply.B`, `Multiply → Position`).
Every object reference machine-validated.

**Your test (nothing to build):**

- [ ] Pull. If git complains about an untracked
      `PrismClockAnimation.hlsl.meta` (your editor generated one locally before the
      committed meta existed), delete the local file and pull again — the graph
      references the committed GUID.
- [ ] Open the project; let it import. If BlockGraph goes magenta or errors (not
      expected — the wiring is donor-schema-exact), run
      **Validate Clock Wiring** and tell me what it says; `git checkout` the graph
      reverts cleanly.
- [ ] Play: **Squirrel right-trigger ring must grow smooth** (per-vertex GPU bloom).
      Trail lay and gyroid growth too. Run the **Smoke Test** menu item; DiagnosticsHUD
      "Animators" rows stay **0 active**.
- [ ] Report back — then I wire ExplodingBlockGraph (test: vessel collision debris)
      and BlockGraph colors (test: skimmer steal) the same way.

## Phase 2 — BlockGraph color nodes

Same file. Properties already present.

- [ ] Add a **Custom Function** node — Source same file, Name **`PrismColorLerp`**.
      Inputs: `Clock` Float · `StartTime` Float · `Duration` Float · `StartBright`
      Vector4 · `StartDark` Vector4 · `StartSpread` Vector3 · `TargetBright` Vector4
      · `TargetDark` Vector4 · `TargetSpread` Vector3. Outputs: `Bright` Vector4 ·
      `Dark` Vector4 · `Spread` Vector3.
- [ ] Wire: Time.Time → `Clock`; `ColorStartTime` → `StartTime`, `ColorDuration` →
      `Duration`, `StartBrightColor`/`StartDarkColor`/`StartSpread` → the Start
      inputs; **the EXISTING `BrightColor`/`DarkColor`/`Spread` property nodes →
      the Target inputs** (the bound material's values ARE the lerp targets).
- [ ] Re-route: every place the graph consumed `_BrightColor`/`_DarkColor`/`_Spread`
      directly now consumes the node's `Bright`/`Dark`/`Spread` outputs.
- [ ] Save → Validate → play: shield engage / steal / danger transitions fade
      smoothly; BlockGraph `[PrismClock]` errors gone.

## Phase 3 — ExplodingBlockGraph nodes

Open `Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph`.

- [ ] Custom Function **`PrismExplosionClock`** — Inputs: `Clock` Float ·
      `StartTime` Float · `Speed` Float · `Duration` Float · `Velocity` Vector3 ·
      `LegacyAmount` Float · `LegacyOpacity` Float. Outputs: `Amount` Float ·
      `Opacity` Float · `WorldOffset` Vector3.
- [ ] Wire: Time.Time → `Clock`; `ExplodeStartTime`/`ExplodeSpeed`/`ExplodeDuration`
      → `StartTime`/`Speed`/`Duration`; existing `Velocity` property → `Velocity`;
      existing `ExplosionAmount` property → `LegacyAmount`; existing `Opacity`
      property → `LegacyOpacity` (the Legacy inputs keep `TransparentPrismMaterial`
      — a LIVE prism material resting at `_ExplosionAmount = 0` — rendering
      correctly).
- [ ] Re-route: `Amount` replaces downstream `_ExplosionAmount` uses; `Opacity`
      replaces `_Opacity` uses.
- [ ] `WorldOffset` is WORLD-space: Transform node (World → Object, Type
      **Direction**) → **Add** to the object-space vertex position → Vertex ▸ Position.
- [ ] **Transparent live prisms bloom**: also add the `PrismGrowScale` cluster here
      (grow properties already exist on this graph's Blackboard). Tip: copy-paste
      the Time + Custom Function + Multiply nodes from BlockGraph, then re-drag the
      LOCAL Blackboard properties into the inputs (don't paste property nodes across
      graphs — drag this graph's own).
- [ ] Save → Validate → play: debris flies/shatters/fades smoothly; transparent
      prisms bloom on spawn and render correctly at rest.

## Phase 4 — SuctionGraph nodes

Open `Assets/_Graphics/Materials/Graphs/PrismGraphs/SuctionGraph.shadergraph`.

- [ ] Custom Function **`PrismSuctionClock`** — Inputs: `Clock` Float · `StartTime`
      Float · `Duration` Float · `Direction` Float · `GrowDelay` Float ·
      `LegacyState` Float. Output: `State` Float.
- [ ] Wire: Time.Time → `Clock`; `SuctionStartTime`/`SuctionDuration`/
      `SuctionDirection`/`SuctionGrowDelay` → their inputs; existing `State`
      property → `LegacyState`. The `State` output replaces every downstream
      `_State` use. (`_Location` stays untouched — live moving-target exception.)
- [ ] Save → Validate → play: fauna grazing sucks prisms into the moving creature
      smoothly.

## Phase 5 — Full verification (§4.4 protocol)

- [ ] **Validate Clock Wiring** → `RESULT: ✅ ALL REQUIRED WIRING PRESENT`
- [ ] Full play session (menu freestyle + one HexRace) with **zero `[PrismClock]`
      errors**
- [ ] DiagnosticsHUD Animators: `PrismScaleManager` / `MaterialStateManager` **0
      active** everywhere, under any load
- [ ] A just-laid ring collides at full size while still visibly blooming
- [ ] Hitstop / pause freezes prism animation (scaled clock — expected)

## Phase 6 — Deletion pass (D2) + chores — only after Phase 5 is green

- [ ] Delete `PrismScaleManager.cs` + its scene components
- [ ] Delete `MaterialStateManager.cs` + scene components
- [ ] `PrismEffectsManager.cs`: delete `ProcessExplosions`/`ProcessImplosions`, both
      Burst jobs, the explosion/implosion registration APIs/lists — KEEP the class
      (clock convergence tracking + dev zombie audit)
- [ ] Delete `AdaptiveAnimationManager.cs` once both subclasses are gone
- [ ] Cleanup PR: strip dead manager-era fields from `MaterialPropertyAnimator`
      (`IsAnimating`, `AnimationProgress`, `Start*4`, `OnAnimationComplete`) and
      `PrismScaleAnimator` (`IsScaling`, `LastStepTime`)
- [ ] Remove `TrailViewer` from `Assets/_Prefabs/Spacevessels/Urchin.prefab`, delete the file
- [ ] Re-baseline PhaseThresholds (volume is final at spawn now):
      `Tools > Cosmic Shore > Measure Cell Environment Baselines` + update cell
      configs per `Docs/ECOSYSTEM.md` §18

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
| Snaps, property exists, error persists | Not Hybrid Per Instance / wrong reference | Validator names it; fix Node Settings |
| Everything magenta after a graph edit | Graph failed to compile | Undo / `git checkout` the `.shadergraph`, redo; Auto-Wire self-rolls-back |
| Growth smooth but colors pop | Phase 2 outputs not re-routed | Finish the `Bright`/`Dark`/`Spread` re-route |
| Transparent prisms snap on spawn | `PrismGrowScale` cluster missing on ExplodingBlockGraph | Phase 3 last step |
| `[PrismClock] ... no companion render entity` | Instanced rendering off / no ECS world | `PrismRenderConfig` ▸ Use Instanced Rendering ON |
| DiagnosticsHUD shows active CPU animators | Something re-engaged a retired manager | Law regression — find the caller; it should not exist |
