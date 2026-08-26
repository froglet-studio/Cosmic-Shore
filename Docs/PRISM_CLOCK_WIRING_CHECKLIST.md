# Prism Clock Wiring — In-Editor Checklist

Companion to `Docs/PRISM_ANIMATION.md` (§4.4, §6). **STRICT MODE is live: no legacy
fallback.** All four wiring phases below are ✅ wired programmatically in-branch.
Phase 6 / D2 ✅ programmatically complete (including PhaseThresholds re-baseline).
**Live remainder:** Phase 5 (D3) ◐ — Menu_Main freestyle + ring bloom + pause recorded; HexRace skipped; `[PrismClock] shieldShatter` refusal is live. Phase 8/9/10 playtests ◐ — see session log. D4 still NO-GO to retire the pool.
If any graph reverts to unwired, prisms SNAP to end states and the console logs
one `[PrismClock]` error per unwired material.

**Already done programmatically on this branch — not your job:**

- ✅ Clock **properties** are inserted into the three graphs (donor-cloned,
  Hybrid Per Instance, registered in the blackboard), including the four
  `_ShieldMorph*` stamps Auto-Wire now owns: BlockGraph (grow trio + color five +
  flight + shield morph + jiggle + live suction four + `_Location`),
  ExplodingBlockGraph (those plus explode trio), SuctionGraph (suction four —
  SequentialFaceConverger, no Converge). Node splices (corridor, erosion,
  back-face, destruction sight, shield-morph CF, flight, jiggle, live suction)
  stay python-owned (`Tools/Shaders/wire_*.py`, including
  `wire_prism_suction_clock.py`). They should appear on each graph's Blackboard
  when you open it.
- ✅ The HLSL (`PrismClockAnimation.hlsl`), all C# stamps/scheduling/diagnostics,
  and the tools below.

**Your tools:**

- `PrismClockWiringTests` (edit-mode, CI) — iterates the same Specs the menu
  item uses. A ShaderGraph revert that drops a Hybrid-Per-Instance property
  or a family's Custom Function node fails naming the property and the graph.
- `FrogletTools > Ecology > Prism Animation> Validate Clock Wiring` — run after every
  phase. Phases 1–4 are ✅ WIRED: it should show **`RESULT: ✅ ALL REQUIRED WIRING
  PRESENT`**. Also names erosion / back-face / destruction sight and delegates
  the corridor census (`CheckGraphWiring`) so this menu is not silently partial.
  A Custom Function node row ❌ means a graph **reverted**, not that wiring is
  still the next step (`Docs/PRISM_ANIMATION.md` §6).
- `FrogletTools > Ecology > Prism Animation> Auto-Wire Clock Properties` — idempotent
  repair tool: re-adds any Hybrid-Per-Instance clock property that's missing
  (including `_ShieldMorph*`). Node splices stay python-owned. Normally reports
  "already present".
- `FrogletTools > Ecology > Prism Animation> Smoke Test - Re-Bloom Nearby Prisms`
  (play mode) — stamps a from-zero regrow on nearby prisms: wired = smooth GPU
  bloom, **zero `[PrismClock]` errors**, collider at full size throughout;
  unwired = snap + errors. (The CPU animation managers are deleted — there is
  no Animators HUD to watch.)

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

**Cross-ref — do not delete the Serpent cloak family (Prompt 7 item (3)).** This
test is the only live producer of `IsTransparent` prisms (`CloakSeedWallActionExecutor.cs:387`).
See `Docs/PRISM_CLOCK_FOLLOWUP_PROMPTS.md` Prompt 7 §(3).

**UN-DEFERRED 2026-08-04, and re-pointed.** This test used to wait on the
camera↔vessel occlusion system. That system is restored (C1, `Docs/PRISM_ANIMATION.md`
§4.7) — but it is now a **shader-side fade off global uniforms** and deliberately
never sets `prismProperties.IsTransparent`, so it is no longer a source of transparent
prisms to steal. The surviving producer is the **Serpent's cloak**:

1. Fly the **Serpent** (its `CloakSeedWallAction` is bound to
   `InputEvents.RightStickAction` — the right trigger; 15s cooldown).
2. Trigger it to lay a cloaked seed wall. Those prisms take
   `GetTeamTransparentBlockMaterial` (the domain clone of `TransparentPrismMaterial`,
   which rests on ExplodingBlockGraph) and carry `IsTransparent = true`.
3. **Skim one to steal it** (or let a danger/shield repaint land on one).
4. Expect: the recolor **fades over ~0.8s** exactly like an opaque prism, and **zero
   `[PrismClock]` errors** in the console.

Machine-verified already (so a failure here means the graph reverted, not that the
plumbing is wrong): ExplodingBlockGraph declares all five color properties
(`_ColorStartTime`, `_ColorDuration`, `_StartBrightColor`, `_StartDarkColor`,
`_StartSpread`) as Hybrid Per Instance, `TransparentPrismMaterial` compiles against that
graph, and `MaterialPropertyAnimator.ClockColorTransition` binds the transparent material
whenever `IsTransparent` is set — so `bindMaterial.HasProperty("_ColorStartTime")` is true
and `WarnUnwiredMaterial` cannot fire. Re-confirm any time with
**FrogletTools > Ecology > Prism Animation > Validate Clock Wiring**.

If it SNAPS instead of fading, the one live variable is the entity sink:
`Prism.UsesEntityColorSink` is false while an exotic visual (a shield morph) owns the
renderer, and a bind-only transition is the documented behaviour there — steal a plain
cloaked prism, not a cloaked-and-shielded one.

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

## Editor session log (Prompt 11 — measure carrier + close playtest debt)

Record every in-editor pass here. Partial sessions still land value.

| date | who | items | outcome |
|---|---|---|---|
| 2026-08-24 | agent (no Editor) | static `--check` wiring scripts | ✅ `wire_prism_shield_morph.py --check` OK · `wire_prism_jiggle_clock.py --check` wired · `wire_prism_explosion_erosion.py` validated · `verify_prism_sight_composition.py` all properties hold |
| 2026-08-24 | agent (no Editor) | Unity Pipeline / play mode | ❌ `unity command`: no Editor instance · batchmode prism edit tests: compile abort (`CS0619` obsolete in Reflex/UniTask package cache on local 6000.5.9f1) |
| 2026-08-24 | agent (no Editor) | (1) benchmark re-profile · (2) fauna suction · Phases 5/8/9/10 playtests | **not run** — requires human in Editor with Play mode; see `PRISM_EXPLOSION_BENCHMARK.md` § Re-measurement results + `PRISM_ANIMATION.md` §4.6.2 |
| 2026-08-25 | agent (open Editor 6000.3.17f1, `PrismGridExplosionTest`) | (1) 5-run bench + `Prism.Destroy.*` markers | ✅ recorded — series `20260825-041823` FPS + peak-frame 155004 markers in `PRISM_EXPLOSION_BENCHMARK.md`. Setup self 53.5 ms / 10,178 deaths (~5.25 µs). Grid `imp` stayed 0 (blast-only negative control). |
| 2026-08-25 | agent (same Editor session, leftover 47³ lattice) | (2) suction / Consume-on-grid | ✅ HUD recorded, visual unresolved — `Prism.Consume` ×48 (peak **0/48**, eater circled, JSON `suction_playtest.json`) then **0/0**; ×24 then **0/0**; close-cam ×7 (`suction_close2.png`) then **0/0**. Same producer as fauna. |
| 2026-08-25 | agent (same Editor, Netcode `LoadScene(Menu_Main)`) | (2) live Lattice fauna cell | ✅ HUD + still — watch-start **0/4** (`prompt11_watch_start.txt` t=9052.460). PlayerLoop peak **15 imp / 4 exp** (`fauna_watch.json`). Parked Squirrel `(-190, 310, 140)`, TadPoleFauna / Boid. Capture `fauna_suction_live.png`: cyan-blue rectangular implosion debris near hull; lime greens are fauna, not suction. Cheap polls: **0/4** t=9507, **0/6** t=9657 — feeding still in progress; criterion 3 on the *live* cell **not shown** (grid Consume already returned `imp`→0). Criterion 1 visual “converges on moving eater” **not claimed from a still**. |
| 2026-08-25 | agent (same session) | Phase 5 / D3 | ◐ Validate Clock Wiring ✅. Pause ✅ (grid). Menu_Main freestyle **ON** (`menu_freestyle.png`). Ring bloom+collider ✅ t=2025.166, 8 Jade segments, `visualScale` blooming vs target, `worldBox=(8, 1.2, 1.2)` full, collider enabled (`prompt11_menu_playtest.txt`). HexRace **skipped** (would destroy this Play session). Console is **not** zero `[PrismClock]`: `shieldShatter` refused (`service: ON · ents=26085 meshes=5 mats=9`). |
| 2026-08-25 | agent (same session) | Phase 8 corridor | ◐ Radii audit ✅ (Serpent hull **50.40** ⚠). Validator ❌ `ExplodingBlockGraph: is TRANSPARENT` (Shader Graph default generated material). Menu_Main: corridor **active** r=**5.51** (Squirrel hull matches audit). Parked Lattice void around hull (`corridor_parked.png`, `corridor_freestyle.png`). Vessel `IsStationary` — SHATTER-at-speed **not** run. Nose-clearance-at-contact **not** isolated. UV wipe: exploded 4 `QuasicrystalBlock` at ~`(-300, 433, 190)` (`prompt11_lattice_health.txt`); camera stayed at the park; delayed HUD **0/4** (retired); `lattice_explode_uv.png` is **not** a proven UV0 face wipe. |
| 2026-08-25 | agent (same session) | Phase 9 shield morphs | ◐ Grid: Engage NRE at SFX (Bootstrap skip). Menu_Main: `ActivateShield` ×6 **nre=0**; `ActivateSuperShield`+`Damage` ×4 **nre=0**, HUD **0/0** (absorb). BoidBlock explode×4 + `DeactivateShields`×2 nre=0; delayed HUD **4/6** (`prompt11_menu_delay.txt` — those 4 are explode debris, not a proven shatter overlay). Console: **`shieldShatter` refused** (no companion entity). Bloom-from-centres **not** confirmed. Steps 3–6 not run. Step 7 YAML: still `outSlope: 2` — **not “fixed”**. |
| 2026-08-25 | agent (same session) | Phase 10 jiggle | ◐ Steps 1 + 6 still the 2026-08-15 pass. Grid: 8 supers + 8 `Damage`, HUD **0/0**. Menu_Main: 4 super+Damage, HUD **0/0**. Squirrel is not the sword vessel — no super-shield ram observed. Rest-pose return, sustained-fire, visual out-of-phase **not confirmed**. |

**D4 go/no-go (2026-08-25):** **CONDITIONAL GO** on the batched death carrier (explosion markers + Consume-on-grid `imp`→0 + live Lattice fauna HUD **0/4 → peak 15 imp**). **NO-GO to retire the pooled path** — `PrismType.Grow` is live. Live-cell criterion 3 (imp→0 when feeding stops) was **not** shown; feeding was still in progress at t=9657 (`0/6`).

## Phase 5 — Full verification (§4.4 protocol)

**~~Known open issue~~ — ✅ FIXED 2026-08-02 (C13a).** `[PrismClock] STRICT MODE:
no companion render entity to stamp (grow:SpawnablePrism (Clone))` was never a
wiring regression, and it was not the raw-`Instantiate` lay either: a shield
engage-morph held the exotic-visual window across the prism's creation reveal, so
`EnsureRenderEntity` was skipped at the exact instant the one-shot grow stamp
fired. Anatomy: `Docs/PRISM_ANIMATION.md` §3.8 #10; the two rules that close it:
§4.5. Expect **zero** such errors now — if one reappears, the message names the
exact broken gate (`Prism.DescribeRenderEntityState`), so paste it verbatim.

- [x] **Validate Clock Wiring** → `RESULT: ✅ ALL REQUIRED WIRING PRESENT` (2026-08-25, grid session)
- [ ] Full play session (menu freestyle + one HexRace) with **zero `[PrismClock]`
      errors** — ◐ 2026-08-25. Menu_Main freestyle **ON** via Netcode `LoadScene("Menu_Main")` after the grid session (`menu_freestyle.png`, `corridor_freestyle.png`). HexRace **skipped** (a scene load would destroy this Play session). Console is **not** zero `[PrismClock]`: `STRICT MODE: no companion render entity to stamp (shieldShatter)` at `2026-08-25T05:38:40Z`, diagnosis `service: ON · ents=26085 meshes=5 mats=9`, stack `PrismShieldMorph.RequestShatter` ← `PrismOctahedronShield.Disengage` ← `PrismStateManager.ActivateSuperShield` (engaging super **disengages** the octahedron overlay). Gameplay final; visual skipped. Do not tick this box until HexRace runs and that refusal is gone or accepted as a known overlay miss.
- [x] A just-laid ring collides at full size while still visibly blooming — 2026-08-25 Menu_Main t=**2025.166**, 8 Jade trail segments via the spawn channel: `visualScale=(0.080, 0.012, 0.012)` vs `targetScale=(8.000, 1.200, 1.200)`, `worldBox=(8.000, 1.200, 1.200)` already at the **full target**, `colliderEnabled=True`. Source `BenchmarkResults/PrismExplosion/prompt11_menu_playtest.txt`.
- [x] Hitstop / pause freezes prism animation (scaled clock — expected) — 2026-08-25 grid: `Time.timeScale=0` held `PrismClock.Now` at **3404.3060** for 1.5 s real while 24 explosion debris stayed live (`paused_debris.png`; freeze proven by the clock number, not by the still — clock was frozen at explosion t=0 so shards look like resting cubes). Unpause hitch jumped the clock ~+10 s.

## Phase 6 — Deletion pass (D2) + chores — ✅ DONE PROGRAMMATICALLY

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
      effect `EnabledInstances` + `PrismDebris.LiveDebrisCount` —
      `GameLoadSampler.cs:43`, where most deaths now live)
- [x] `TrailViewer` component removed from `Urchin.prefab`; file deleted
- [x] Re-baseline PhaseThresholds — ✅ DONE 2026-08-02: the prompter ran
      `Measure Cell Environment Baselines` and the six freestyle configs were
      re-authored from the pasted output (fresh baseline + Blob deltas, exact;
      `Docs/ECOSYSTEM.md` §18 example updated). Atlantis has no cell config
      (Scurry segment-spawner path), so nothing to author there.

**In-editor sanity after pulling this phase**: open `PrismManagers.prefab` and
`Urchin.prefab` in the inspector — there must be NO "Missing (Mono Script)" rows
(the components were excised, not orphaned). Enter play mode once; the console
must show no compile errors and no `[PrismClock]` errors.

## Phase 7 — Follow-up branches (post-wiring, own PRs)

Tracker items (`Docs/PRISM_ANIMATION.md` §5 C-phase) landing per-path on the wired
graphs, each following the shipped B1/B3 templates: ~~C1 `ClearPrisms` shader-side
occlusion fade~~ (✅ shipped 2026-08-04 — `PrismOcclusionCorridor`, now a PLATFORM LAW wired into every
live-prism graph and bound at `VesselController.Initialize`, §4.7; gates: the `PrismOcclusionCoverageTests`
edit-mode test + FrogletTools > Ecology > Prism Animation > **Validate Occlusion Corridor**) ·
~~C4 `FireTrailBlock` pool/Destroy fix~~ (✅ 2026-08-07 — resolved by deletion; the
scripts were unreachable dead code, see §5 C4) ·
~~C5 turret anchor flight~~ (✅ shipped 2026-08-07 — `PrismFlightClock` +
`_FlightStartTime`/`_FlightDuration`/`_FlightVelocity` on both live-prism graphs, wired by
`Tools/Shaders/wire_prism_flight_clock.py`; gate: **Validate Clock Wiring** now requires
them) ·
~~B4 GPU shield morphs~~ (✅ shipped 2026-08-15 — `PrismShieldMorph` + the four
`_ShieldMorph*` properties on both live-prism graphs, wired by
`Tools/Shaders/wire_prism_shield_morph.py`; the last sanctioned CPU prism ticker,
`PrismOctahedronShieldManager`, is DELETED. Gates: **Validate Clock Wiring** now
requires them, plus the `PrismShieldMorphTests` edit-mode suite. Phase 9 below) ·
~~C6 fauna parent-scale / wither~~ (✅ parent-scale 2026-08-25 — **(b)**
mover-contract, same as locomotion; redundant `GrowToScale`
`NotifyBodyPrismsMoved` deleted. ✅ wither C11 2026-08-25) · C6 remainder
devour/graze suction + boid husk shrink · ~~C7 flora growth~~ (✅ by construction —
closes with C6; `PhyllotacticFlora.cs:432` `EnvironmentPrismPool.Get` → `:439` `AddHealthBlock` → `:440`
`Initialize` → `StampClockGrowth`) · ~~C8 microscene conveyor~~ (✅ shipped
2026-08-02) · ~~C9 cell-swap suction~~ (✅ shipped 2026-08-25 —
`PrismSuctionConverge` + `_Suction*`/`_Location` on both live graphs, wired by
`Tools/Shaders/wire_prism_suction_clock.py`; root scale kept for non-prism
riders. Gates: **Validate Clock Wiring**, `PrismCellSwapSuctionTests`) ·
~~C11 spindle fade~~ (✅ shipped 2026-08-25 —
`PrismDeathClock` on SpindleGraph / AnimatedSpindleGraph, wired by
`Tools/Shaders/wire_prism_spindle_death_clock.py`; ordered wither is
`ForceWither(i * interval)` offsets. Gates: `PrismSpindleDeathClockTests`;
playtest → Prompt 11) · ~~C13b environment-lay pooling~~ (✅ shipped 2026-08-25 —
unbounded `EnvironmentPrismPool`, snap Blue then ChangeTeam clock-lerps;
`EnvironmentPrismPoolTests`. Playtest → Prompt 11). (C10 worm shift is resolved by deletion — the
worm-colony rebuild removed the legacy shift; see Docs/ECOSYSTEM.md §23.)

## Phase 8 — Occlusion corridor (C1) — WIRED PROGRAMMATICALLY, **PLAYTEST PARTIAL (2026-08-25)**

Everything machine-checkable is verified and gated (see below). What no tool here can
answer is whether it *feels* right — that needs a human at the editor.

**Gates that already pass** (re-run them if anything looks wrong; both are asset-only,
no play mode):

- 2026-08-24 (agent, no play mode): `python3 Tools/Shaders/wire_prism_explosion_erosion.py`
  validated · erosion wiring present on `ExplodingBlockGraph.shadergraph`.
- `FrogletTools > Ecology > Prism Animation > **Validate Occlusion Corridor**` — checks
  the HLSL GUID, both graphs' unexposed globals + custom-function node + compile state,
  every material on those graphs, and every prefab carrying a `Prism`.
- `PrismOcclusionCoverageTests` (edit-mode) — the same rules as an automated test, so new
  content authored outside the corridor fails CI-style rather than silently going opaque.

**The playtest.** Load any scene with a local vessel and a dense prism environment
(Menu_Main freestyle is the fastest — fly into the cell wall):

1. Fly so a prism wall sits between the camera and your ship. A cone of prisms centred on
   the ship dissolves; the ship stays visible through it.
2. Move off. The wall returns to fully opaque **immediately** — the gradient band is
   deliberately short, so there should be no lingering half-dissolved mass.
3. Watch the boundary. Sides and base grade at the same rate; there should be **no seam**
   anywhere on the cone, and in particular no crisp semicircular edge on a large plate
   level with the ship.
3b. **Fly into a prism wall and watch the moment of contact.** The prism you hit must be
   FULLY SOLID as it arrives — the fade completes a hull radius short of the ship
   (`PRISM_OCCLUSION_NOSE_CLEARANCE`), so there is a solid buffer the nose sits inside.
   A prism that is still dithering when you strike it means the clearance is too small
   (or 0); conversely, if mass now hides the ship at contact range, it is too large.
4. Hold still ~10s and watch the stipple. It should read as a **cracked lattice of walls**
   (the shipped SHATTER kernel), and the pattern should **evolve** — polygons drifting,
   walls re-drawing — reading as flow, never as flicker. Round flecks mean the kernel is
   back on `..._WORLEY`; triangles mean `..._SHARD`; **face-sized plates flashing in and
   out around the vessel mean `..._SHATTER3D`, which is carried but REJECTED ON LOOK —
   restore `..._SHATTER`.** Kernel 6 (`..._SHARD3D`) is the Lab candidate: volumetric
   roundish blobs / spherical shells, not cracked walls, and it must not plate-flash.
   Coverage is proven offline; look on real mass at speed is unearned — do not Bake as
   CURRENT. (Known open issue, 2026-08-10: at high speed the
   screen-anchored pattern's slide over fast geometry can strobe the bright-face/
   dark-interior contrast; SHATTER3D fixed that and introduced worse. The shipped
   answer is `PRISM_BACKFACE_POWER` (3.0) — it takes the prism's own interior out of
   the gradient band while the exterior is still dissolving, removing the interference
   rather than scrambling it. `PRISM_OCCLUSION_SHATTER_DEPTH_PHASE` ships at **0**: it
   was measured and useful decorrelation needs ~50x the rate the speed budget allows,
   so it is a dead dial, not a lever. Morph-rate and back-face power are the live
   shipped levers; kernel 6 exists in the Lab, look unearned.)
5. Swap vessels (the freestyle vessel-changer toy). The corridor should re-scale to the
   new hull automatically — a bigger ship clears a proportionally bigger cone.
6. Check the console: zero `[PrismOcclusion]` errors. Any that appear name the vessel and
   the number, and mean either an unmeasurable hull or an implausible radius (or, since
   2026-08-10, a transparent prism material — those are off-contract now, see step 7).
7. Shoot a prism wall and watch the debris (2026-08-10/11 — dither IS prism
   transparency). Each face of an exploding prism should show **ONE jagged erosion
   front wiping across it** (solid face → hard irregular line → gone) — never many
   holes eaten from multiple points across a face, no smooth blend, and no dither on
   the edge ITSELF: the only speckle on debris should be the corridor's own, when a
   chunk flies through the cone. A graded debris edge is the retired fringe and reads
   as visual confusion with the tunnel.
   **The wipe must visibly COMPLETE before the piece disappears** — it finishes 15%
   of the fade early by construction (`PRISM_EROSION_END_MARGIN`), on the extended
   7.5s duration. **Watch a SPINNING piece: the front must ride the face through the
   whole tumble** — a wipe that jumps or flickers as pieces rotate is the retired
   position-anchored bug; **orbit the camera around a fading chunk: nothing about the
   wipe may change with the view** — either failure means stale wiring, run
   `python3 Tools/Shaders/wire_prism_explosion_erosion.py` (it migrates old shapes in
   place). A fading chunk inside the corridor shows both effects composed in
   coverage. Cloaked prisms (the cloak-wall ability) should read as a sparse ~1%
   stipple, not a translucent ghost. If any prism blends smoothly, a transparent
   prism material has crept back in — run
   `python3 Tools/Shaders/enable_prism_alpha_clip.py` (it converts strays and
   preserves their authored alpha as coverage).
8. Per-vessel corridor sizing: run **FrogletTools > Vessels > Audit Corridor Vessel
   Radii** (asset-only). Every vessel's hull radius should track its visual bulk, and
   the report names the top contributing renderers — a radius far off the fleet
   median comes with its offender attached. Skinned hulls measure `localBounds` in
   root-bone space (the culling bounds — what actually renders); the old
   `sharedMesh.bounds` read overstated armature-scaled rigs ~5× (the Sparrow's
   oversized corridor).

**2026-08-25 playtest record (Prompt 11).** Grid scene has **no local pilot**, so the corridor is off there by design — steps 1–7 (cone, nose-clearance, SHATTER at speed, debris UV wipe) **were not run**. Step 8 **was**:

| vessel | hull | cone outer / core | notes |
|---|---|---|---|
| Urchin | 0.53 | 0.53 / 0.13 | tiny test mesh |
| Dolphin | 2.86 | 2.86 / 0.72 | |
| Manta | 3.97 | 3.97 / 0.99 | skinned |
| Squirrel | 5.51 | 5.51 / 1.38 | skinned |
| Scarab | 12.32 | 12.32 / 3.08 | same mesh as Sparrow |
| Sparrow | 12.32 | 12.32 / 3.08 | fleet median |
| Rhino | 15.64 | 15.64 / 3.91 | |
| Serpent | **50.40** | 50.40 / 12.60 | **⚠ OUTLIER (> 3× median)** — `SerpentMesh` / `HeadRootBone` |

`FrogletTools > Ecology > Prism Animation > Validate Occlusion Corridor` → **❌ CORRIDOR INCOMPLETE — ExplodingBlockGraph: is TRANSPARENT**. Authored prism `.mat` files all pass `python3 Tools/Shaders/enable_prism_alpha_clip.py --check` (15 opaque + clip). The converter only walks `Assets/_Graphics/Materials`; the validator also sees Shader Graph's default generated material. Do not convert that while Play is running.

Pause-frame 24 explosion debris is **not** a UV-wipe proof: clock frozen at explosion t=0, camera on a super-shield octahedron.

**2026-08-25 Menu_Main continuation (same Play session, Squirrel, Lattice boot world).** Corridor **active**, `TargetRadius=5.51` matching the Squirrel hull audit. Vessel parked `IsStationary=true` at `(-190, 310, 140)` then earlier at `(-180, 280, 90)`.

| step | result |
|---|---|
| 1 cone vs wall | **not isolated** — Lattice is a volumetric forest, not a wall the cone can be compared against. Parked stills (`corridor_parked.png`, `corridor_freestyle.png`) show a cleared volume around the hull; they do not prove cone geometry. |
| 2 nose-clearance buffer | **not isolated** — vessel held stationary; no contact-range ram into mass. |
| 3 SHATTER lattice at speed | **not run** — `IsStationary=true` for the fauna watch. Parked stills show the SHATTER look at rest only. |
| 4 debris UV0 erosion wipe | **not confirmed**. Exploded 4 `QuasicrystalBlock Variant(Clone)` at ~`(-299.6, 433.2, 185.7)`… (`prompt11_lattice_health.txt` t=9462.757). Same-frame HUD **0/6**; delayed cheap poll **0/4** (explosion debris retired or never in view — camera stayed at the park). `lattice_explode_uv.png` is the parked Lattice, **not** a spinning debris face with a jagged UV wipe. |
| 5 cloak stipple | **not run** |
| 6 corridor compose with explosion fade | **not confirmed** (no in-view explosion debris) |
| 7 transparent-material creep | authored `.mat`s `--check` clean; validator still flags the Shader Graph default generated material |
| 8 radii audit | ✅ see table above. Live Squirrel `TargetRadius=5.51` matches. |

**Do not hand-edit these to explore.** `FrogletTools > Ecology > Prism Animation >
**Occlusion Dither Lab**` drives every knob below as a shader global — **live, including in
play mode** — previews them through the shipped GPU code, measures the coverage fidelity
against the shipped baseline, and bakes the result back into the constants. Editing the
`#define`s by hand is for when you already know the number.

**The knobs**, if it needs tuning (all in
`Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl` unless noted):

| Knob | Default | What it does |
|---|---|---|
| `PRISM_OCCLUSION_KERNEL` | `..._SHATTER` | The dither look. `..._SHATTER` a cracked lattice of walls (**shipped** — restored 2026-08-10 after the 3D kernel's same-day rejection) · `..._SHATTER3D` the volumetric world-anchored variant (**carried, REJECTED ON LOOK**: crack planes lying near-parallel to a viewed surface flash face-sized plates — reads as glitchy clipping around the vessel; every fidelity number passed, the look failed) · `..._SHARD3D` Euclidean distance-to-owner fill (**Lab candidate**: coverage proven offline 0.00783; look on real mass at speed unearned — do not Bake as CURRENT) · `..._SHARD` triangular flecking · `..._WORLEY` round flecks · `..._SPIRAL` a corridor-anchored iris · `..._IGN` an even screen-space dissolve. |
| `PRISM_OCCLUSION_SHATTER3D_CELL` / `..._WALL` | `12.0` / `1.2` | Shared by Shatter3D (carried, rejected) and Shard3D (Lab candidate). CELL is the ideal angular size in px on the power-of-two world-size ladder. WALL is a ratio of the cell for Shatter3D only — Shard3D is a radial fill, so the Lab hides WALL. |
| `PRISM_EROSION_WIGGLE` / `..._WIGGLE_FREQ` / `..._END_MARGIN` / `..._FRINGE` / `..._CDF_LO` / `..._CDF_HI` | `0.12` / `2.5` / `0.15` / `0.0` / `-0.02` / `1.02` | The exploding prism's body-anchored fade: **one jagged WIPE per face, anchored to UV0** (spin-proof — mesh attributes can't be moved by vertex animation; the position-anchored version broke under the shatter spin). WIGGLE is the jagged-front amplitude, FREQ the jags per face width, END_MARGIN makes the wipe complete that fraction of the fade EARLY (closes the retirement race; pairs with `PrismExplosion.DefaultDuration` 7.5s = the old 5s × 1.5), FRINGE the dithered dissolve band leading the front — **shipped at 0 (hard edge)**: a graded edge dissolves in the same visual language as the corridor and the two read as one confused surface, and removing it also took the fade-curve error from 0.0296 to 0.00068. Non-zero restores the graded edge. **The CDF pair is fitted to the wipe coordinate over the UV square** — re-run `python3 Tools/Shaders/fit_prism_erosion_cdf.py` after moving WIGGLE or FREQ (MARGIN and FRINGE sit outside the fit and tune freely). |
| `PRISM_OCCLUSION_SHARD_ORIENT` | `..._FIXED` | Shard only. `..._FIXED` all triangles one heading (most legible as a triangle) · `..._FLIP` up/down · `..._SPIN` free per-cell rotation (reads as splinters). |
| `PRISM_OCCLUSION_MORPH_RATE` | `0.3256` | Pattern evolution, cycles/sec. `0` freezes it. Shipped ABOVE the ~`0.25` guideline by deliberate choice after viewing it in motion (1.75% of band pixels flip per frame vs the 1.45% guideline) — a look call, and it cannot affect the fade: coverage is flat across the range. |
| `PRISM_OCCLUSION_NOSE_CLEARANCE` | `1.0` | Where the corridor STOPS, in multiples of the vessel's own hull radius. The fade completes this far SHORT of the vessel plane, leaving a fully solid buffer the ship's whole nose sits inside — without it a prism is still half-dematerialised when the ship hits it and the impact does not read. Measured on-axis (Sparrow, hull 12.32, camera 30 u): cleared 22–28 u out, fading 20→14 u, **solid from 12.3 u through the vessel plane**. The trade: mass inside the buffer is solid and can occlude the ship at contact range — lower toward `0.5` if that bites, `0` restores the old flush-to-the-plane cone. |
| `PRISM_OCCLUSION_SHATTER_DEPTH_PHASE` | `0.0` (**off**) | Shatter only. Shifts each cell's WALL by view depth, leaving the lattice itself still. Coverage-neutral. **Implemented, measured, shipped OFF**: separating a prism's own two faces (~2u apart) needs ~50x the rate the speed budget allows — at 0.02 it is only 16% decorrelated at 2u while flipping 17.9% of band pixels per frame at 300 u/s (ceiling ~1.45%). The dial exists so the conflict is visible, not so it gets turned up. Replaces the retired `PRISM_OCCLUSION_PARALLAX` domain shear, which had the same conflict and moved the WHOLE lattice (coherent crawl the eye tracks — rejected on look 2026-08-11). |
| `PRISM_BACKFACE_POWER` | `3.0` | `alpha^power` on surfaces facing AWAY from the camera. Prisms render two-sided, so the beat's usual second layer is a prism's own interior; sharpening it drops the interior out of the gradient band while the exterior is still dissolving — the only fix that REMOVES the interference rather than scrambling it, and the only one with no temporal cost. Measured both-in-band range: `1.0` (off) 0.09–0.92 · `2.0` 0.28–0.92 · **`3.0` 0.44–0.92** · `4.0` 0.54–0.92. The trade is a look change — interiors vanish earlier, so a mid-fade prism reads as a thinner shell. `1.0` disables it without touching the graph. |
| `PRISM_OCCLUSION_CELL_SIZE` | `6.0` | Fleck size in pixels, shared by SHARD and WORLEY. **Free dial inside 4.5–11 px** (sweet spot 6–8); the CDF fit is scale-invariant, so it does NOT need re-fitting — see the size window below. |
| `PRISM_OCCLUSION_SHARD_AREA` | `1.28607` | Shard only. Normalises the triangle gauge to the same AREA as the circle it replaces — which is also what lets it share the CDF fit. **Changing this one DOES mean re-fitting `..._CDF_*`.** |
| `PRISM_OCCLUSION_SHATTER_CELL` / `..._WALL` | `16.26` / `20.0` | Shatter only, and independent: polygon size and wall repeat, both in pixels. At alpha `a` the dark wall is `(1-a) × WALL` wide. Windows: polygon **8–20 px**, wall up to **~1.25× the polygon** — the wall window is RELATIVE, not absolute (corrected 2026-08-06). No CDF — `frac` of a hash is uniform by construction. |
| `PRISM_OCCLUSION_LIVE_TUNING` | `0` (shipped) | **Design mode.** 1 promotes every knob above to two shader globals and makes the kernel a runtime branch, so the Lab can drive them live. 0 compiles the file exactly as if none of this existed — one kernel, no branch, no uniforms. Design mode costs GPU occupancy (all seven kernels in every prism shader), so **bake and set it to 0 before shipping**; the Lab's "Bake to source + ship mode" button does both. Fail-safe: with nothing published, every dial falls back to its constant, so design mode with the Lab closed looks exactly like shipped mode. |

**The size window (measured 2026-08-06).** `CELL_SIZE` used to carry a "re-fit the CDF or
the fade degrades ~19×" warning. That was wrong: the distance is measured in *cell* units,
so the distribution does not move with the pitch — re-fitting anywhere from 3 to 15 px lands
within noise of the shipped constants and buys nothing. (The 19× figure is what dropping the
remap *entirely* costs.) What actually bounds the dial is **sampling**, at both ends, and
neither end is fittable: below ~4.5 px the shape falls under the pixel floor, and past ~11 px
too few cells span the gradient band, so corridor error climbs (0.019 at 11 px, 0.025 at
15 px — that last one reads as a chunky edge, not a fade). Same failure shape on SHATTER: a
polygon or a wall as large as the gradient band cannot resolve the gradient.
| `PRISM_OCCLUSION_SPIRAL_ARMS` | `3.0` | Spiral only. **Must stay an integer** or a radial scar appears down one side. |
| `OuterRadiusScale` / `InnerRadiusScale` / `CoreAlpha` | `1` / `0.25` / `0` | `Resources/PrismOcclusionConfig` — corridor width and how solid the clear centre is. Multiples of the vessel's own circumscribing radius, so they are vessel-independent. |

**If the corridor does nothing at all**, check in this order: the config asset's `Enabled`;
that the vessel spawned through `VesselController.Initialize` with `IPlayer.IsLocalPilot`
true; then run the validator above.

## Phase 9 — Shield morphs (B4) — WIRED PROGRAMMATICALLY, **PLAYTEST PARTIAL (2026-08-25)**

The engage bloom and the disengage shatter are GPU-clocked and
`PrismOctahedronShieldManager` is deleted (`Docs/PRISM_ANIMATION.md` §4.8). Everything
machine-checkable passes; what no tool can answer is whether it looks right.

**Gates that already pass** (asset-only, no play mode):

- 2026-08-24 (agent): `python3 Tools/Shaders/wire_prism_shield_morph.py --check` — OK
  (both live graphs).
- `python3 Tools/Shaders/wire_prism_shield_morph.py --check` — the splice's structure:
  the four Hybrid-Per-Instance properties, the custom-function node and its HLSL GUID, the
  UV1 + object-space-normal feeds, and that `Prism Sub Graph.Out_Vector3` now reaches the
  vertex chain **only** through the morph.
- `FrogletTools > Ecology > Prism Animation > Validate Clock Wiring` — the same properties
  from the compiled-material side, on every BlockGraph / ExplodingBlockGraph material.
- `PrismShieldMorphTests` (edit-mode) — baked centroids vs the retired CPU formula, the
  wiring, the deleted ticker, and that neither shield has regrown an `Update`/coroutine/
  tween or a CPU mesh rebuilder.

**The playtest.** Anywhere prisms shield: skim a trail to shield it, or load the Skim Race /
Astro League track for super-shields (`SegmentSpawner.SuperShieldSpawnedPrisms`).

1. **Engage**: the octahedron's 8 faces (stella: 24) grow out of their own centres over
   ~0.35 s (~0.45 s), smoothly, from invisible. A shield that appears full-size instantly is
   an unwired graph — check the console for `[PrismClock] ... _ShieldMorphDuration`.
2. **Disengage**: the prism is a box again immediately, and the shield's faces fly outward
   along their normals while shrinking to points over ~0.6 s (~0.7 s). No overlay at all =
   the batched shatter was refused; the console says so once (`shieldShatter`).
3. **Re-engage mid-shatter**: the old shards must keep flying while the new shield blooms —
   they are deliberately not cancelled any more (continuity of existence).
4. **Batching**: with many same-size shields up, the frame's draw calls must not scale with
   the number of shields — the morph runs on the shared mesh, so a hundred blooming shields
   of one size and domain is one batch, exactly like a hundred settled ones.
5. **Birth snap**: environment prisms laid pre-shielded (e.g. `ShieldedSpawnablePrism`) must
   still appear already-armoured with no bloom and no shield SFX — the birth rule
   (`PrismStateManager.IsBirthTransition`).
6. **Pool reuse**: shield a prism, destroy it, let the pool hand it back — the reused prism
   must be a plain box with no residual morph (a stale stamp would collapse it toward its
   own origin, which is the loudest possible symptom).
7. **`BlueBlock.prefab`'s easing changed on purpose** (Duel for Cell / Freestyle MP / 2v2,
   plus both Recording Studios). It and `OctahedronShieldTest.prefab` were the only two
   assets that *serialized* the engage/shatter curves, and they carried a hand-altered
   fast-slow-fast variant (end tangents 2) rather than `EaseInOut`'s zero-tangent
   smoothstep — up to 0.192 apart mid-transition. Every other shield in the game took the
   C# initializer and is byte-identical. If BlueBlock's bloom reads differently from a
   trail prism's, that is this, and it is expected — say so rather than "fixing" it, and
   note that Unity will drop those now-orphaned YAML keys the next time either prefab is
   saved.

**2026-08-25 playtest record (Prompt 11), leftover grid, Bootstrap skipped (`AudioSystem.Instance` is null).** Destruction SFX is already `?.`; shield SFX at `PrismStateManager.ApplyShieldState:227` / `:256` is not.

1. **Engage**: `ActivateShield` ×8 — Engage ran (8 octahedra live after the throw). Then NRE at `:227`. Capture `shield_super_jiggle.png` is a **settled** close octahedron, **not** a 0.35 s bloom-from-centres confirmation.
2. **Disengage**: 8 supers then `DeactivateShields` — flags 8→0 despite NRE at SFX; HUD 0/0 same frame. Shatter overlay **not captured**.
3. **Re-engage mid-shatter**: **not run**.
4. **Batching**: **not run**.
5. **Birth snap**: **not run**.
6. **Pool reuse**: **not run**.
7. **YAML confirmed, not “fixed”**: `BlueBlock.prefab` and `OctahedronShieldTest.prefab` still serialize `engageCurve` `outSlope: 2`. Runtime easing is GPU `smoothstep`. Expected mid-transition deviation up to 0.192 vs the fleet.

**2026-08-25 Menu_Main continuation (AudioSystem present).** `ActivateShield` ×6 **nre=0** (`prompt11_menu_playtest.txt`). `ActivateSuperShield`+`Damage` ×4 **nre=0**, HUD **0/0** (absorb — correct). Later BoidBlock explode×4 + `DeactivateShields`×2 **nre=0**; same-frame HUD **0/12**, delayed **4/6** (`prompt11_menu_delay.txt`) — those 4 are **explode** debris mixed into the same HUD, not a proven shield-shatter overlay. Console: **`[PrismClock] STRICT MODE: no companion render entity to stamp (shieldShatter)`** with `ents=26085` — overlay refused; gameplay already final. Bloom-from-centres (step 1 look) **not** confirmed from `shield_engage.png` / `shield_shatter_menu.png`. Steps 3–6 still **not run**. Step 7 unchanged.

## Phase 10 — Super-shield deflection jiggle (C14) — WIRED PROGRAMMATICALLY, **FIRST PLAYTEST PASSED**

> **2026-08-15:** confirmed in-editor — both graphs import clean (nothing magenta), and the
> deflection reads on vessel impact. Skimmer impact correctly produces none (step 6). The
> steps below stay as the regression checklist; steps 2, 3 and 5 have not been
> specifically exercised yet.
>
> **2026-08-25 (Prompt 11, leftover grid):** 8 leftover prisms `ActivateSuperShield` then
> `Damage` with distinct impact vectors. Super-shields absorbed (HUD stayed **0/0** — jiggle
> is photons-only). Visual out-of-phase wobble **not confirmed** from a still. Rest-pose
> return (step 2) and sustained fire (step 3) **not run**. Step 4: still invulnerable.
>
> **2026-08-25 Menu_Main continuation:** 4 BoidBlocks `ActivateSuperShield` then `Damage`
> (`prompt11_menu_playtest.txt` + `prompt11_menu_explode.txt`). HUD **0/0** (absorb). Squirrel
> is not the sword vessel — no super-shield ram observed; photons-only `Damage` is the
> substitute. Steps 2, 3, 5 still **not visually confirmed**. `shield_super_jiggle.png` is a
> settled octahedron, not a wobble.

A super-shielded prism that is HIT but not destroyed now wobbles and settles instead of
absorbing the hit in total silence. Design + rationale: `Docs/PRISM_ANIMATION.md §4.9`.

**Nothing here needs hand-wiring in the editor.** The graph surgery is
`Tools/Shaders/wire_prism_jiggle_clock.py` (idempotent — re-running prints "already wired"),
and the properties alone can be repaired from
`FrogletTools > Ecology > Prism Animation > Auto-Wire Clock Properties`.

**Gates that already pass** (all asset-only, no play mode):

- 2026-08-24 (agent): `python3 Tools/Shaders/wire_prism_jiggle_clock.py --check` — wired on
  BlockGraph + ExplodingBlockGraph.
- `python3 Tools/Shaders/wire_prism_jiggle_clock.py --check` — re-validates the whole graph
  model plus the splice topology on both graphs.
- `FrogletTools > Ecology > Prism Animation > **Validate Clock Wiring**` — the three
  `_Jiggle*` properties (exposed + Hybrid Per Instance) and the `PrismJiggleClock` node are
  in its required set for BlockGraph and ExplodingBlockGraph.
- `PrismSuperShieldJiggleTests` (edit-mode) — the CPU↔GPU count-match (graph property ⟷
  `[MaterialProperty]` component ⟷ prototype registration), pool hygiene via
  `ClearPrismStamps`, and the config's amplitude clamp/monotonicity.

**What only a human can answer — the playtest.** Any scene with super-shielded mass; the
fastest is the Skim Race / Astro League track lining, or `SegmentSpawner`'s super-shielded
segments. Shoot or ram a super-shielded prism and watch:

1. **It visibly wobbles and settles** — roughly two oscillations over ~0.55 s, spike tips
   moving much further than the core, and each face going its own way rather than the whole
   prism tipping as one rigid block.
2. **It does not drift, grow, or end up rotated.** The wobble must return to *exactly* the
   resting pose. Any permanent offset means the envelope is not reaching zero.
3. **Sustained fire still reads as wobbling, not frozen.** Hold full-auto on one prism: the
   spam gate (`minSecondsBetweenStamps`, default 0.12 s) is what stops each new hit
   restarting the envelope before it can move. If it looks locked mid-tilt, raise it.
4. **Nothing else changed.** The prism is still invulnerable, still skims, still blocks the
   blast. Ordinary (non-super-shielded) prisms must be visually identical to before.
5. **Neighbours are not in lockstep.** A blast that touches several super-shielded prisms
   should make them wobble out of phase with each other.
6. **Skimming never deflects, on any vessel.** Confirmed by playtest 2026-08-15 (jiggle on
   vessel impact, none on skimmer impact — both as desired), and still true after the energy
   sword merge. Four of five skimmer containers carry no prism-damage effect at all; the fifth
   (Rhino forcefield) carries `RhinoSkimmerDamagePrismEffectSO`, which pops or bounces
   super-shielded mass in its own branch and returns before `Damage`. If you ever want a skim
   to deflect, that is a container/effect change on that vessel — not a change here.

**Tuning** — `Resources/PrismSuperShieldJiggleConfig`:

| Field | Default | What it does |
|---|---|---|
| `duration` | `0.55` | Length of one deflection. The envelope hits exactly zero here. |
| `minTiltDegrees` / `maxTiltDegrees` | `2.5` / `6.5` | Peak face tilt for the weakest / hardest hit. Small numbers — the pivot is the prism origin, so a few degrees is a lot of motion at a stella tip. |
| `referenceImpactSpeed` | `120` | Impact speed (u/s) at which the tilt reaches its ceiling. |
| `precessionDegreesPerSecond` | `1260` | How fast the tip direction revolves. |
| `nutationDegreesPerSecond` | `1776` | How fast the tilt magnitude breathes. Keep it non-commensurate with the precession rate or the wobble repeats and reads as a mechanical buzz. |
| `minSecondsBetweenStamps` | `0.12` | Per-prism spam gate (see playtest step 3). |
| `enabled` | `on` | Off costs exactly what the feature cost before it existed. |

**If nothing wobbles at all**, check in this order: the config asset's `enabled`; that the
prism really is super-shielded (`PrismStateManager.CurrentState == SuperShielded`, not merely
`Shielded`); then `--check` above. A `[PrismClock] STRICT MODE` error in the console naming
`_JiggleStartTime` (or `_SuctionStartTime` after a C9 graph revert) means the
graph wiring is gone — re-run the matching `Tools/Shaders/wire_prism_*.py`.

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
| Something re-engaged retired CPU animation managers | `PrismScaleManager` / `MaterialStateManager` / `AdaptiveAnimationManager` were deleted (D2, 2026-08-02) | Law regression — find the caller; those classes must not return |
| Shield appears full-size with no bloom | `_ShieldMorph*` missing on the material's graph | `python3 Tools/Shaders/wire_prism_shield_morph.py`; reimport |
| A prism collapses toward its own origin | A shield-morph stamp survived onto the prism's BOX mesh (no centroids in UV1) | `Disengage` / `Prism.Initialize` must reach `ClearShieldMorphStamp` — check the clear path, not the shader |
| Shields bloom but shatter shows nothing | Batched shatter refused (service off / no world) — logged once as `shieldShatter` | Same fix as any missing companion entity: instanced rendering ON |
| Draw calls scale with the number of *morphing* shields | Something set `SetExoticVisualActive(true)` again, or handed a per-prism mesh to the entity | Nothing should: the morph runs on the SHARED mesh (§4.8) |
| Unstamped live prism still converges / snaps toward origin | `_SuctionDuration` not 0 (unstamped identity is Duration 0 → LegacyState 0 on live graphs) | `ClearSuctionClockStamp` on pool return; prototype Duration default 0 |
| `[PrismClock] ... does not declare _SuctionStartTime` | Live graph missing the suction cluster | `python3 Tools/Shaders/wire_prism_suction_clock.py`; reimport |
| Old world collapses IN PLACE instead of toward the cell centre | `PrismSuctionConverge` missing / Position still fed by flight Add | `--check` the wirer; Converge must be LAST on `VertexDescription.Position` |
| Old world peels face-by-face like a consumed prism | SequentialFaceConverger on a live graph (SuctionGraph look) | Live graphs use `PrismSuctionConverge`, not SequentialFaceConverger. Do not splice Converge into SuctionGraph |
| Cell-swap prisms vanish mid-suction / cull early | `RenderBounds` still the un-sucked box | `StampSuctionToward` must `ResetBoundsToMesh` + `EncapsulateBoundsPoint` (object-space cell centre, padding 2) — same class as explosion bounds |
| Unstamped spindle evaporates / condenses on its own | `_DeathDuration` not 0 (unstamped identity is Duration 0 → LegacyState) | Condense stamps from `Start`; evaporate stamps from `ForceWither`. Default Duration 0 on the graph |
| `[PrismClock] ... does not declare _DeathStartTime` | Spindle graph missing the death cluster | `python3 Tools/Shaders/wire_prism_spindle_death_clock.py`; reimport. Do **not** splice this onto BlockGraph |
| Wither snaps every spindle at once | Missing `i * interval` stamps (per-frame WaitForSeconds cascade came back, or every delay is 0) | `LightFauna.WitherCoroutine` / `LifeForm.WitherToSkeleton` must `ForceWither(i * interval)` after the distance sort |
