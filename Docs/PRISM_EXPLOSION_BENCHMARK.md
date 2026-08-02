# Prism Explosion A/B Benchmark — legacy-cpu vs gpu-clock

The pre-merge performance comparison for the clock-material migration: FPS as a
function of time across the FULL explosion interval, repeated runs per variant,
one report comparing the envelopes. Built on Yash's prism-grid explosion rig
(`claude/prism-grid-explosion-scene-bi74f9`).

## What one run records

Rebuild the lattice from scratch (identical initial conditions every run) →
wait Ready + settle 1.5s → record **1s pre-roll baseline** (resting lattice) →
**detonate** → record every frame's unscaled delta time for **6s** (the
explosion runs 5s; the margin catches the tail). Each run is one JSON in
project-root **`BenchmarkResults/PrismExplosion/`** — outside Assets, so
results **survive branch switches** and the two experiments accumulate into
the same folder. The variant label is auto-detected (`legacy-cpu` when
`PrismScaleManager` exists, else `gpu-clock`) along with the git branch.

## Experiment protocol (run both, any order)

### NEW — gpu-clock (this branch)

1. Check out `claude/prism-animation-audit-95mlpu`, open the project.
2. `Tools > Cosmic Shore > Setup Prism Grid Explosion Scene` (idempotent —
   authors the config, scene, managers, harness + benchmark component).
3. Disable Bootstrap auto-load
   (`Tools > Cosmic Shore > Testing Multiplayer > Do not load Bootstrap Scene on Play`),
   press Play.
4. Set the grid size you want to stress (the X/Y/Z/gap fields — the SAME size
   for both experiments), then press **Bench** (or console `bench 5`).
   Five runs execute unattended; watch the "Bench" rows on the DiagnosticsHUD
   (F7). `bench stop` cancels.

### OLD — legacy-cpu (bleeding-edge baseline)

```bash
git checkout -b bench-legacy origin/bleeding-edge
git cherry-pick 8436342f cf382420   # Yash's harness (grid rig + readiness fix)
git cherry-pick 0af666b4            # the A/B benchmark layer (branch-portable)
git cherry-pick c08024bd            # loud lay failures + 'prisms N' + empty-lattice guard
git cherry-pick 3b9efbf5            # ThemeManager self-provisioning (zero-prism root cause —
                                    # present on bleeding-edge too; re-run the setup tool after)
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
  process-wide, so a 6k lattice takes ~1,000 frames between runs. That wait is
  correctness (detonating into a half-registered lattice measures nothing).
- The husk sweep runs in the Ready phase on both branches (same rig cost both
  sides).
- **Gyroid-scale gate (prompter's rule)**: the comparison runs AFTER the
  gyroid prism scale matches bleeding-edge (see the diagnosis machinery in
  `PrismRenderService.DescribeGrowStampTarget` — the strict-mode error now
  names the broken gate), so the two variants render equivalent work.
