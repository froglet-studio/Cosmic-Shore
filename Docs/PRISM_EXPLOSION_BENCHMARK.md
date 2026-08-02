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
  (`PrismFactory.VFXBudgetPerFrameOverride`, 64 → unbounded: every death's
  effect spawns the same frame), and the live-effect pressure model
  (`PrismFactory.EffectPressureScalingDisabled`: every death animates the full
  5s instead of being squeezed to 0.22s under load). These guards were sized
  for the CPU-per-effect era; on the clock path a live effect costs no
  per-frame CPU, so lifting them is precisely the "what is the new system
  capable of" question. Gameplay scenes never see the overrides. Runs record
  `throttlesLifted` and the report refuses to average lifted and default runs
  silently (they are different workloads).

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
2. `Tools > Cosmic Shore > Setup Prism Grid Explosion Scene` (idempotent —
   authors the config, scene, managers, harness + benchmark component; it is
   also SELF-HEALING: a scene that somehow carries duplicate PrismManagers
   instances — the Singleton<T> managers must exist exactly once — gets the
   extras deleted, with inactive instances counted too).
3. Disable Bootstrap auto-load
   (`Tools > Cosmic Shore > Testing Multiplayer > Do not load Bootstrap Scene on Play`),
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

On either branch: `Tools > Cosmic Shore > Prism Grid Benchmark >
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
