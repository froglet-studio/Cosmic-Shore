# Prism Explosion A/B Benchmark — legacy-cpu vs gpu-clock

The pre-merge performance comparison for the clock-material migration: FPS as a
function of time across the FULL explosion interval, repeated runs per variant,
one report comparing the envelopes. Built on Yash's prism-grid explosion rig
(`claude/prism-grid-explosion-scene-bi74f9`) plus the salvaged dolphin-explosion
coverage work (`claude/dolphin-explosion-prism-coverage-qtbstp`).

## The spec'd workload

- **The lattice**: a **47³ = 103,823-prism cube** (the nearest odd-sided cube to
  100k — odd so an exact centre prism exists on every face). Authored by the
  setup tool; both experiments must use it unchanged. Gaps are **per-axis**
  (config `defaultGaps`, panel `gaps` X/Y/Z fields, `grid x y z [gap | gx gy gz]`)
  — unequal gaps make the inscribed blast bind to the tightest axis.
- **The blast**: the spherical AOEExplosion's own wavefront — **progressively
  larger overlap spheres each frame**, expanding at
  `speed = MaxScale / ExplosionDuration`, with the salvaged lossless deferral
  guaranteeing every prism the sphere contains actually dies (never capped by
  how long the VFX ran). The sweep time is a parameter: config
  **`explosionSpeed`** (radius world-units/second) pins the physical expansion
  rate — duration = final radius / speed, so a bigger lattice takes
  proportionally longer to sweep. At 0 (default) the prefab's authored 2s
  duration holds and any blast crosses its lattice in 2s regardless of size.
- **The end condition**: the blast **ends inscribed** (Fit Blast To Lattice,
  default ON) — the final overlap sphere reaches the centre of each cube face,
  so the **face-centre prisms are destroyed** while edges and corners survive
  (~π/6 ≈ 52% of the cube dies, ~54k prisms).
- **Safety throttles LIFTED** (config `liftSafetyThrottles`, default ON): the
  rig measures the system **unweakened**. The harness scene-scopes three
  overrides and restores them on exit — the per-frame AOE damage budget
  (`PrismSpatialIndex.DamageBudgetPerFrameOverride`, 48 → unbounded: prisms
  die the frame the wavefront contains them, no ~19s trickle-drain tail), the
  per-frame destruction-VFX spawn caps
  (`PrismFactory.VFXBudgetPerFrameOverride` — **no-op after D4 2026-08-25**:
  death is unthrottled by construction on the batched carrier; the harness
  still writes the override so lift/restore compiles), and the live-effect pressure model
  (`PrismFactory.EffectPressureScalingDisabled`: every death animates the full
  5s instead of being squeezed to 0.22s under load). These guards were sized
  for the CPU-per-effect era; on the clock path a live effect costs no
  per-frame CPU, so lifting them is precisely the "what is the new system
  capable of" question. Gameplay scenes never see the overrides. Runs record
  `throttlesLifted` and the report refuses to average lifted and default runs
  silently (they are different workloads).
- **Batched pure-entity debris (gpu-clock side, `f0ddfc21`)**: on this branch a
  prism death's explosion VFX is a batch-instantiated ENTITY, not a pooled
  GameObject — the first lifted-throttle profile showed the pooled carrier
  (2,408 pool misses, `PrismExplosion.OnDisable` 1,863 ms in one frame) costing
  orders of magnitude more than the effect itself. The lifted burst therefore
  measures entity + GPU cost, not pool churn. Legacy keeps its authored effect
  path — that architectural difference is exactly what the A/B measures.
- **Suction debris on the same carrier (2026-08-04)**: implosions now batch too
  (`SpawnImplosionDebrisBatch`). **This does not change the blast numbers** — an
  AOE lattice detonation produces zero implosions, because every AOE/projectile/
  skimmer/ram death routes `Prism.Damage → Explode`, and the only producer of an
  implosion anywhere in the project is `Prism.Consume` (fauna feeding). Judge the
  implosion half in a scene with fauna, not on the grid rig; the grid's `debris`
  HUD row reports both families (`N exp / N imp`) so a stuck suction count is
  visible if one ever appears there.

## Re-profiling the death path (2026-08-04)

The lifted-throttle profile that motivated the entity carrier attributed
**~0.43 ms of SELF time per death** to `AOE.ResolveDamage` (≈1,047 ms for 2,408
deaths). That marker wraps a whole drain and nothing inside it was instrumented,
so the figure is an upper bound on "everything a death does" — and the single
largest thing it contained, `PrismExplosion.OnDisable` at 1,863 ms, has since
been removed by `f0ddfc21`. **Treat the 0.43 ms as stale until re-measured.**

`Prism` now emits five markers so the re-measurement is attributable rather than
a single bucket:

| marker | what it covers |
|---|---|
| `Prism.Destroy.Setup` | `SetupDestruction` total (pose capture, collider/animator stand-down, volume, flags) |
| `Prism.Destroy.SpatialIndex` | `PrismSpatialIndex.MarkDestroyed` → `UnbindCell` → `Cell.RemoveBlock` → the three density grids |
| `Prism.Destroy.StatRaise` | the `PrismStats` SOAP raise → `StatsManager.PrismDestroyed` and everything it fans out to |
| `Prism.Destroy.SFX` | `AudioSystem.PlayGameplaySFX` (throttled, so mostly an early-out) |
| `Prism.Destroy.EffectRequest` | the prism event-channel raise → `PrismFactory` → `PrismDebris` queue |

`Prism.Destroy.Setup` NESTS the SpatialIndex and StatRaise scopes, so its *self*
time is the prism-local work and its children are the two fan-outs. Expect
`StatRaise` to dominate: `StatsManager.PrismDestroyed` looks up the attacker and
the victim and then writes six `RoundStats` properties, each of which raises its
own change event into the HUD.

**What to record.** Run the standard 5-run `bench`, throttles lifted, and capture
alongside the FPS envelope: the five markers' total + self ms for the detonation
frame, and the GC allocated-per-frame figure. Compare against a run at
`f0ddfc21` for the delta attributable to this pass, whose measurable claims are:

- zero managed allocation on a plain trail-prism death (was: one `PrismEventData`
  class, one `Domains[3]`, and two LINQ lookups' worth of closure + delegate +
  boxed enumerator per death);
- one `transform` pose read per death instead of four, and one
  `transform.position` read per death inside `Cell.RemoveBlock` instead of three.

These are structural claims about allocation and interop counts, not predictions
about frame time — the benchmark is what says whether they matter.

### Re-measurement results (Prompt 11 / D4 gate)

Record each session here. **A playtest nobody wrote down did not happen.**

| run | branch / commit | grid | throttles | detonation-frame markers (total ms → self ms) | GC B/frame (detonation) | notes |
|---|---|---|---|---|---|---|
| **f0ddfc21 baseline** | gpu-clock @ `f0ddfc21` | 47³ (spec) | lifted | **not instrumented** — only `AOE.ResolveDamage` bucket: ~1,047 ms total / **~0.43 ms SELF per death** (2,408 deaths); included `PrismExplosion.OnDisable` 1,863 ms same frame | not recorded | Stale upper bound; see § "Re-profiling" above. Left as-is — this file fills blanks, it does not rewrite that row. |
| **2026-08-24** | `ten-branch` (agent, no Editor) | — | — | **not run** | — | Static wiring `--check` only |
| **2026-08-25** | `ten-branch` @ Unity 6000.3.17f1, Editor play | 47³ (103,823) | lifted (`blastRadius` 691, `explosionDuration` 3.0) | **peak death-frame 155004, 10,178 samples** — see per-marker grid below | **885,437 B** (whole frame, not death-path-only) | FPS envelope: series `20260825-041823` (5 runs). Markers: `BenchmarkResults/PrismExplosion/destroy_markers_playloop.json` from a PlayerLoop.Update sampler (EditorApplication.update recorders always peak at 0). Marker-era FPS series `20260825-042851` is **contaminated** (JSON writer on the hot path; mean 28.5 FPS) — do not use it for the envelope. |

**Detonation-frame markers (2026-08-25, peak frame 155004, 10,178 deaths).** Recorders sampled inside PlayerLoop Update. Setup **nests** SpatialIndex + StatRaise; Setup self = Setup total − those two. SFX and EffectRequest are siblings after Setup returns. Sum of (Setup self + children + siblings) is the death-path wall time on that frame.

| marker | total ms | self ms | ≈ µs/death |
|---|---|---|---|
| `Prism.Destroy.Setup` | 60.865 | **53.462** | 5.98 total / **5.25 self** |
| `Prism.Destroy.SpatialIndex` | 5.872 | — (child of Setup) | 0.58 |
| `Prism.Destroy.StatRaise` | 1.530 | — (child of Setup) | 0.15 |
| `Prism.Destroy.SFX` | 0.735 | 0.735 | 0.07 |
| `Prism.Destroy.EffectRequest` | 8.349 | 8.349 | 0.82 |
| **sum (self + children + siblings)** | **69.948** | | **6.87** |

Vs the stale **430 µs SELF/death** `AOE.ResolveDamage` figure: Setup total/death is **~72× cheaper**. **StatRaise did not dominate** (the doc expected it would) — Setup self is 53.5 of 69.9 ms. GC 885,437 B/frame ≈ 87 B/death *if attributed entirely to deaths*; that does **not** prove the “zero managed allocation on a plain trail-prism death” claim (whole-frame GC includes other work). `f0ddfc21` remains the uninstrumented baseline row.

**Debris HUD caveat.** The sampler stored `PrismDebris.LiveDebrisCount` at write time, not at the peak frame. Peak-frame sample is 0/0 because debris spawns in `LateUpdate` (execution order 29000) after Update. First post-peak snapshot (~246 frames later) was also `0 exp / 0 imp`. EffectRequest 8.3 ms implies the queue ran; do not treat 0 as “batch spawn failed” without a same-frame HUD capture. Grid blast-only: **`imp` stayed 0** (negative control — AOE never implodes).

**FPS envelope — series `20260825-041823`** (use this, not 042851). FPS = `n_frames / sum(dt)` (true mean). Phases 0–1 / 1–3 / 3+ are after preroll. First detonation of a cold session often pays a ~1.09 s hitch; later runs often do not.

| run | n | mean | pre | post | min fps | min@postF | dt>0.1 | 0–1s | 1–3s | 3+s |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 1064 | 50.6 | 59.5 | 50.2 | **0.92** | 13 | **1.088s @ t_post≈1.31s** | 10.7 | 47.4 | 53.5 |
| 2 | 1144 | 54.4 | 60.0 | 54.2 | 19.68 | 109 | none | 59.9 | 33.6 | 56.3 |
| 3 | 1054 | 50.2 | 60.0 | 49.7 | **0.85** | 577 | **1.171s @ t_post≈13.0s** + 0.131s | 59.5 | 34.0 | 50.9 |
| 4 | 1132 | 53.8 | 60.0 | 53.5 | 10.03 | 505 | none (min dt 0.100s) | 59.6 | 33.7 | 55.5 |
| 5 | 1122 | 53.4 | 60.0 | 53.1 | 11.25 | 1060 | none | 58.2 | 25.9 | 56.0 |

Extra 1-run `20260825-042339_run01.json`: n=1099, mean 52.3, pre 59.9, post 51.9, min 13.71, no dt>0.1.

**D4 (this file's half):** explosion-side measurement is **GO**. Pair with `Docs/PRISM_ANIMATION.md` §4.6.2 (Consume-on-grid `imp`→0 **and** live Lattice fauna HUD **0/4 → peak 15 imp**; live-cell retirement to 0 not shown — feeding still in progress at t=9657). One-line: **death pooling retired 2026-08-25 (Prompt 9b); Grow stays pooled.** The harness still writes `VFXBudgetPerFrameOverride` (no-op after D4).

Markers nest: Setup's self time excludes SpatialIndex + StatRaise children.
`Prism.cs` emits them at lines 1110–1114 (not 1084–1088).

## What one run records

Rebuild the lattice from scratch (identical initial conditions every run;
the harness holds the global load gate during builds, so 100k materializes in
seconds, not minutes) → settle 1.5s → record **1s pre-roll baseline** →
**detonate** → record every frame's unscaled delta time for **20s**. With the
default throttle lifts that window covers the visual wavefront plus the full
5s per-prism debris/fade tail (~54k concurrent clock effects at peak — the
honest stress). With throttles at gameplay defaults it instead covers the
48-destructions/frame backlog drain (~19s for the inscribed kill at 60fps; the
frame-locked drain makes slower variants show longer tails, which is signal).
Each run is one JSON in project-root
**`BenchmarkResults/PrismExplosion/`** — outside Assets, so results **survive
branch switches** and the two experiments accumulate into the same folder. The
variant label is auto-detected (`legacy-cpu` when `PrismScaleManager` exists,
else `gpu-clock`) along with the git branch.

## Experiment protocol (run both, any order)

### NEW — gpu-clock (this branch)

1. Check out `claude/prism-animation-audit-95mlpu`, open the project.
2. `FrogletTools > Scene Setup > Setup Prism Grid Explosion Scene` (idempotent —
   authors the config, scene, managers, harness + benchmark component; it is
   also SELF-HEALING: a scene that somehow carries duplicate PrismManagers
   instances — the Singleton<T> managers must exist exactly once — gets the
   extras deleted, with inactive instances counted too).
3. Disable Bootstrap auto-load
   (`FrogletTools > Scene Setup > Testing Multiplayer> Do not load Bootstrap Scene on Play`),
   press Play.
4. The setup tool authors the spec'd 47³ grid — leave the X/Y/Z/gap fields
   alone so both experiments run the identical workload. Press **Bench** (or
   console `bench 5`). Five runs execute unattended (each ≈ 20s recording +
   a fast gate-boosted rebuild); watch the "Bench" rows on the DiagnosticsHUD
   (F7). `bench stop` cancels.

### OLD — legacy-cpu (bleeding-edge baseline)

```bash
git checkout -b bench-legacy origin/bleeding-edge
git cherry-pick 8436342f cf382420   # Yash's harness (grid rig + readiness fix)
git cherry-pick 6090f42e 060d160c f913f7e4
                                    # dolphin-explosion coverage salvage, ORIGINALS —
                                    # authored against bleeding-edge, so they apply clean
                                    # there (the audit branch carries reconciled ports).
                                    # Same blast semantics on both variants = fair A/B.
git cherry-pick 0af666b4            # the A/B benchmark layer (branch-portable)
git cherry-pick c08024bd            # loud lay failures + 'prisms N' + empty-lattice guard
git cherry-pick 3b9efbf5            # ThemeManager self-provisioning (zero-prism root cause —
                                    # present on bleeding-edge too; re-run the setup tool after)
git cherry-pick 02aceaae            # 100k-cube spec + inscribed blast + load-gate rebuilds
                                    # (expect a small PrismExplosion conflict: keep the
                                    # legacy file's manager plumbing, take the incoming
                                    # PressuredDuration/DefaultDuration block if absent —
                                    # f913f7e4 already gave legacy its own pressure path,
                                    # so on conflict simply keep THEIRS=legacy for that file)
                                    # Do NOT cherry-pick f0ddfc21 (batched pure-entity
                                    # debris): it IS part of the gpu-clock architecture
                                    # being measured, not benchmark tooling — legacy runs
                                    # its pooled-GameObject effect path as authored.
git cherry-pick ca92704d            # per-axis gaps + explosion speed + throttle lifts +
                                    # manager dedupe. Conflict guidance:
                                    #  - PrismExplosion.cs: keep LEGACY's file wholesale
                                    #    (its pressure model lives in PrismEffectsManager),
                                    #    then HAND-ADD the one-line gate at the top of
                                    #    legacy's PressuredDuration (wherever it lives):
                                    #      if (PrismFactory.EffectPressureScalingDisabled)
                                    #          return DefaultDuration;
                                    #  - PrismSpatialIndex.cs / PrismFactory.cs: take the
                                    #    incoming override blocks (DamageBudgetPerFrame-
                                    #    Override / VFXBudgetPerFrameOverride /
                                    #    EffectPressureScalingDisabled + Effective*
                                    #    properties) and retarget the same use sites the
                                    #    diff shows — the salvage commits already gave
                                    #    legacy the same constants, so hunks land close.
                                    # WITHOUT this commit the two variants run different
                                    # workloads (legacy trickles 48 kills/frame while the
                                    # clock branch kills same-frame) — the lift must exist
                                    # on BOTH sides for a fair A/B.
```

Then steps 2–4 exactly as above — same scene tool, same grid size, same Bench
button. The runs self-label `legacy-cpu`. (The benchmark layer compiles on
both worlds by construction: the deleted-manager references are
reflection/`PrismStateManager`-based, and the recorder touches no
branch-specific API.)

### Report

On either branch: `FrogletTools > Performance > Prism Grid Benchmark>
Generate Comparison Report`. Reads every run JSON in the folder and writes:

- **`report.md`** — per-variant summary (mean / p50 / 1% low / min FPS +
  pre-roll baseline, ±sd across runs), the **gpu-clock vs legacy-cpu delta**
  (window mean, 1% low, and the baseline gap so explosion-specific cost is
  separable), per-phase means (0–1s blast, 1–3s mid-flight, 3s+ fade tail),
  and the per-run table.
- **`curves.csv`** — the envelope: per-variant **mean/min/max FPS per 100ms
  bin** from −1s (pre-roll) to +6s across all runs. Plot t vs FPS; the
  min/max band across the repeats IS the envelope.

`Tools > … > Open Results Folder` jumps to the files. The report warns if the
two variants were run with different grid sizes.

## Validity notes

- **Same grid, same machine, same power profile** for both experiments; close
  other apps. Editor play mode is fine for a relative comparison (both
  variants pay the same editor overhead); a development build tightens the
  absolute numbers if wanted.
- Runs rebuild the lattice every time — materialization is ~6 prisms/frame
  process-wide during play — the harness sidesteps this by holding the global
  load gate during builds (512/frame), which is safe because nothing records
  until Ready. Detonating into a half-registered lattice measures nothing; the
  Ready gate is correctness.
- The husk sweep runs in the Ready phase on both branches (same rig cost both
  sides).
- Both variants must carry the SAME blast-coverage semantics (the dolphin
  salvage commits) — otherwise the new branch destroys more prisms than the
  old and the comparison measures workload, not architecture. The cherry-pick
  recipe above guarantees this.
