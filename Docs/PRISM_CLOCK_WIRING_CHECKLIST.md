# Prism Clock Wiring — In-Editor Checklist

Companion to `Docs/PRISM_ANIMATION.md` (§4.4 wiring, §6 handoff). **STRICT MODE is
live: there is no legacy fallback.** Until each graph below is wired, prisms on its
materials SNAP to end states and the console logs one `[PrismClock]` error per
material naming the missing property. Each phase below removes a family of those
errors and makes that family GPU-smooth.

Your two verification tools (already in the branch):

- **`Tools > Cosmic Shore > Prism Animation > Validate Clock Wiring`** — run after
  every phase; it checks the graph source (properties + Hybrid Per Instance flags +
  node presence) AND the compiled materials (`HasProperty`, the same ground truth the
  runtime diagnostics use), and prints exactly what remains.
- **`Tools > Cosmic Shore > Prism Animation > Smoke Test - Re-Bloom Nearby Prisms`**
  (play mode) — stamps a from-zero regrow on up to 500 nearby prisms. Wired = smooth
  GPU bloom, 0 active CPU animators, full-size collision throughout. Unwired = snap +
  errors.

Adding a property (same 4 clicks every time): open the graph (double-click the
`.shadergraph`) → Blackboard **+** → pick the type → name it → in the **Graph
Inspector ▸ Node Settings** set **Reference** to the EXACT name given below and
**Shader Declaration = Hybrid Per Instance**. Reference names must match exactly —
the C# stamps and the validator check these strings.

---

## Phase 1 — BlockGraph grow (the smooth-ring fix, ~15 min)

File: `Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph`

- [ ] Add property **Float** `_GrowStartTime`, default 0, Hybrid Per Instance
- [ ] Add property **Float** `_GrowRate`, default 0, Hybrid Per Instance
- [ ] Add property **Vector3** `_GrowStartFrac`, default (1,1,1), Hybrid Per Instance
- [ ] Add a **Time** node (its `Time` output is the clock)
- [ ] Add a **Custom Function** node: Node Settings ▸ Type **File**, Source
      **`Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl`**, Name
      **`PrismGrowScale`**. Inputs (exact names/types): `Clock` Float, `StartTime`
      Float, `Rate` Float, `StartFrac` Vector3. Output: `Scale` Vector3.
- [ ] Wire: Time.Time → `Clock`; drag the three properties onto `StartTime` /
      `Rate` / `StartFrac`.
- [ ] Vertex stage: multiply the **object-space vertex position at the START of the
      vertex chain** by `Scale` (Multiply node, componentwise). If the Master
      Stack's Vertex ▸ Position is fed by the SpreadSubGraph chain, insert the
      Multiply on that chain's base **Position (Object)** input — the bloom must
      scale the prism about its origin *before* the spread offset is added. If
      Vertex ▸ Position is unconnected, add a Position node (Space: Object) →
      Multiply (× `Scale`) → Vertex Position.
- [ ] **Save Asset** (top-left) and let it reimport.
- [ ] Run **Validate Clock Wiring** → BlockGraph grow rows all ✅ (color rows still ❌ — expected until Phase 2).
- [ ] Play mode sanity: lay trail / make a ring with the Squirrel right trigger →
      **smooth** growth; run the **Smoke Test** menu item; DiagnosticsHUD
      "Animators" rows stay **0 active**.

## Phase 2 — BlockGraph colors (domain paint / steal / danger / shield transitions)

Same file.

- [ ] Add **Float** `_ColorStartTime` 0 · **Float** `_ColorDuration` 0 — Hybrid Per Instance
- [ ] Add **Color** `_StartBrightColor` (1,1,1,1) · **Color** `_StartDarkColor`
      (1,1,1,1) · **Vector3** `_StartSpread` (0,0,0) — all Hybrid Per Instance
- [ ] Add **Custom Function** node: Source same HLSL file, Name **`PrismColorLerp`**.
      Inputs: `Clock` Float, `StartTime` Float, `Duration` Float, `StartBright`
      Vector4, `StartDark` Vector4, `StartSpread` Vector3, `TargetBright` Vector4,
      `TargetDark` Vector4, `TargetSpread` Vector3. Outputs: `Bright` Vector4,
      `Dark` Vector4, `Spread` Vector3.
- [ ] Wire: Time.Time → `Clock`; the five new properties → their inputs;
      **`TargetBright`/`TargetDark`/`TargetSpread` ← the EXISTING `_BrightColor` /
      `_DarkColor` / `_Spread` property nodes** (the bound material's values ARE the
      lerp targets — this is why no `_Target*` properties exist).
- [ ] Re-route: everywhere the graph previously consumed `_BrightColor` /
      `_DarkColor` / `_Spread` directly, consume the node's `Bright` / `Dark` /
      `Spread` outputs instead.
- [ ] Save Asset → **Validate** → BlockGraph fully ✅.
- [ ] Play sanity: fly through a crystal (shield engage), steal a prism, overheat a
      trail — transitions fade smoothly; no `[PrismClock]` errors for BlockGraph
      materials.

## Phase 3 — ExplodingBlockGraph (destruction debris + transparent live prisms)

File: `Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph`

- [ ] Add **Float** `_ExplodeStartTime` 0 · **Float** `_ExplodeSpeed` 0 · **Float**
      `_ExplodeDuration` 0 — Hybrid Per Instance
- [ ] Add **Custom Function** node, Name **`PrismExplosionClock`**. Inputs: `Clock`
      Float, `StartTime` Float, `Speed` Float, `Duration` Float, `Velocity` Vector3,
      `LegacyAmount` Float, `LegacyOpacity` Float. Outputs: `Amount` Float,
      `Opacity` Float, `WorldOffset` Vector3.
- [ ] Wire: Time.Time → `Clock`; the three new properties → `StartTime`/`Speed`/
      `Duration`; existing **`_Velocity`** property → `Velocity`; existing
      **`_ExplosionAmount`** property → `LegacyAmount`; existing **`_Opacity`**
      property → `LegacyOpacity` (the Legacy inputs keep `TransparentPrismMaterial` —
      a LIVE prism material resting at `_ExplosionAmount = 0` — rendering correctly).
- [ ] Re-route: `Amount` replaces every downstream use of `_ExplosionAmount`;
      `Opacity` replaces `_Opacity`.
- [ ] Vertex stage: `WorldOffset` is a WORLD-space translation — Transform node
      (World → Object, Type **Direction**) → Add to the object-space vertex
      position → Vertex Position.
- [ ] **Transparent live prisms bloom too**: repeat Phase 1 on this graph (the grow
      trio + `PrismGrowScale` multiply). The validator lists these as "optional" —
      skip them and transparent prisms will snap (loudly) on spawn while opaque ones
      bloom.
- [ ] Save → **Validate** → Play sanity: destroy trail prisms — debris flies,
      shatters, fades smoothly; a transparent (skimmed-through) prism still renders
      correctly at rest.

## Phase 4 — SuctionGraph (consume / implosion)

File: `Assets/_Graphics/Materials/Graphs/PrismGraphs/SuctionGraph.shadergraph`

- [ ] Add **Float** `_SuctionStartTime` 0 · `_SuctionDuration` 0 ·
      `_SuctionGrowDelay` 0 and **Float** `_SuctionDirection` **1** — Hybrid Per Instance
- [ ] Add **Custom Function** node, Name **`PrismSuctionClock`**. Inputs: `Clock`
      Float, `StartTime` Float, `Duration` Float, `Direction` Float, `GrowDelay`
      Float, `LegacyState` Float. Output: `State` Float.
- [ ] Wire: Time.Time → `Clock`; the four new properties; existing **`_State`**
      property → `LegacyState`. `State` output replaces every downstream `_State` use.
      (`_Location` stays as-is — it is the moving convergence target, refreshed live
      per the documented exception.)
- [ ] Save → **Validate** → Play sanity: let fauna graze a trail — prisms suck into
      the moving creature smoothly.

## Phase 5 — Full verification (Docs/PRISM_ANIMATION.md §4.4 protocol)

- [ ] **Validate Clock Wiring** → `RESULT: ✅ ALL REQUIRED WIRING PRESENT`
- [ ] A full play session (menu freestyle + one HexRace) with a **clean console** —
      zero `[PrismClock]` errors
- [ ] DiagnosticsHUD "Animators": `PrismScaleManager` / `MaterialStateManager` read
      **0 active** in every scene, under any load
- [ ] A just-laid ring collides at full size while still visibly blooming
- [ ] AstroLeague hitstop / pause freezes prism animation (scaled clock — expected)

## Phase 6 — Deletion pass (D2) + chores

Only after Phase 5 is green:

- [ ] Delete `PrismScaleManager.cs` + its scene components (search scenes for the
      component; banner in the file lists the contract)
- [ ] Delete `MaterialStateManager.cs` + scene components
- [ ] In `PrismEffectsManager.cs`: delete `ProcessExplosions` / `ProcessImplosions`,
      both Burst jobs, the explosion/implosion registration APIs and lists — KEEP the
      class (clock convergence tracking + dev zombie audit live there)
- [ ] Delete `AdaptiveAnimationManager.cs` once both subclasses are gone
- [ ] Code cleanup PR: strip the now-dead manager-era fields from
      `MaterialPropertyAnimator` (`IsAnimating`, `AnimationProgress`, `Start*4`
      mirrors, `OnAnimationComplete`) and `PrismScaleAnimator` (`IsScaling`,
      `LastStepTime`)
- [ ] Remove the `TrailViewer` component from `Assets/_Prefabs/Spacevessels/Urchin.prefab`,
      then delete `TrailViewer.cs`
- [ ] Re-baseline PhaseThresholds (volume is now final at spawn):
      `Tools > Cosmic Shore > Measure Cell Environment Baselines`, update the cell
      configs per `Docs/ECOSYSTEM.md` §18

## Phase 7 — Follow-up branches (post-wiring, own PRs)

The remaining tracker items (`Docs/PRISM_ANIMATION.md` §5, C-phase) now land as
small per-path changes on the wired graphs, each following the shipped B1/B3
templates: C1 `ClearPrisms` shader-side occlusion fade · C4 `FireTrailBlock`
pool/Destroy fix · C5 turret anchor flight · C6 fauna wither/devour/level-up ·
C7 flora growth · C8 microscene conveyor · C9 cell-swap suction · C10 worm shift ·
C11 spindle fade · C13 environment-lay pooling · B4 GPU shield morphs.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Prism snaps + `[PrismClock] ... does not declare '_X'` | Property missing or Reference name mismatch | Add the property / fix the Reference string exactly; reimport |
| Snaps, property exists, no error gone after reimport | Not Hybrid Per Instance | Node Settings ▸ Shader Declaration = Hybrid Per Instance |
| Everything magenta after a graph edit | Graph failed to compile | Undo the last node change (or `git checkout` the .shadergraph) and redo |
| Colors pop but growth is smooth | Phase 2 not done / outputs not re-routed | Finish Phase 2 re-route |
| Transparent prisms snap on spawn | Grow trio missing on ExplodingBlockGraph | Phase 3 optional step |
| `[PrismClock] ... no companion render entity` | Instanced rendering off / no ECS world | `PrismRenderConfig` ▸ Use Instanced Rendering ON |
| DiagnosticsHUD shows active CPU animators | Something re-engaged a retired manager | That's a law regression — find the caller, it should not exist |
