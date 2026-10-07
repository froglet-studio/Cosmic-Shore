# Shader & Visual Development

> Moved verbatim from the root `CLAUDE.md`, which indexes every topic file. Paths in this file are relative to the repository root.

## Shader & Visual Development

### HLSL / Shader Graph

- Custom Function nodes use HLSL files stored in a consistent location
- Function signatures must follow Shader Graph conventions (proper `_float` suffix usage, sampler declarations)
- Blend shapes are converted to textures for shader-driven animation (no controller scripts — animation is entirely GPU-driven for performance)
- Edge detection, prism rendering, Shepard tone effects, and speed trail scaling are active shader systems
- Procedural HyperSea skybox shader with Andromeda galaxy, domain-warped nebulae, and configurable star density

### Performance Standards

- Use `Unity.Profiling.ProfilerMarker` with `using (marker.Auto())` for profiling, not manual `Begin`/`EndSample`
- Watch for `Gfx.WaitForPresentOnGfxThread` bottlenecks — usually indicates GPU sync issues, not CPU
- Static batching, object pooling, and draw call management are always priorities
- Test with profiler before and after optimization changes — don't assume improvement
- GPU instancing enabled on all prism and VFX materials
- Prism scale/material/effect animation is GPU-clock-driven (the clock-material law, `Docs/PRISM_ANIMATION.md`) — the former CPU Jobs+Burst animation managers are deleted
- Burst-compiled spatial queries replace Physics-based AOE prism damage (`PrismSpatialIndex` — see `Docs/SPATIAL_INDEX.md`)
- Cache-line-aware data layouts with hot/cold splitting and bit-packed flags (`PrismSpatialData` / `PrismDamageData` in `PrismSpatialIndex`)
- Growth occupancy checks use `PrismSpatialIndex.TryReserve` (claim-before-spawn), never `Physics.CheckBox` — prism colliders are disabled for the first 0.6s after spawn, so physics queries are structurally blind to fresh prisms

**The performance record and how to measure (2026-10-07) - start here before any perf work:**

- **Read first:** `Docs/PERFORMANCE_OPTIMIZATION.md` (where the game stands, what was done, the ranked lever
  list, how to measure without fooling yourself). Then `Docs/MEMORY_AUDIT.md` (what the game HOLDS) and
  `Docs/PLATFORM_UNIFICATION.md` (device tiers: Desktop / MobileHigh / MobileLow, the trail cap, one codebase
  for Windows, iOS and Android). The next session's brief: `Docs/prompts/PERFORMANCE_NEXT_SESSION_PROMPT.md`.
- **Instruments in the game:** the console commands `diag` (per-system timings, a report to JSON),
  `prof` (the Profiler Hierarchy as JSON; flags Burst jobs that ran as managed code), `freeze` / `ab`
  (a same-state A/B in one line), `renderers` (a renderer census; `hide <prefix>` / `show` to A/B a family's
  culling cost), the `DiagnosticsHUD`, and the Performance Benchmark tool
  (`_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md`). `diag` records the editor's Code Optimization
  mode: **measure in Release** (the bug icon, bottom right); Debug runs all C# about 5x slower and a number taken
  there says nothing about a build.
- **Every per-frame cost gets a `ProfilerMarker` named `System.Part`** (`SkimRace.Pilot.Decide`,
  `SkimRace.Driver.TrackMpc`, `RaceTrailCap.Hold`, ...) so `prof` and `diag` can name it; the AI's are listed
  in `Docs/SKIM_RACE_AI.md` §12.
- **Allocation gates:** `Tools/Build/check_mathf_params_alloc.py` - a 3-argument `Mathf.Min/Max/...` allocates a
  `params` array per call (the Skim Race pilot allocated 840 a frame); use `MathfNoAlloc`.
  `Tools/Build/scan_perframe_allocations.py` scans per-frame code for allocations. `Decide` now allocates nothing.
- **The editor runs Mono, in double precision, at Debug optimization**; a Release .NET number does not predict
  it. The Skim Race simulator's `SKIMRACE_RUNTIME=mono` mode does (`Docs/SKIM_RACE_AI.md` §8.0f-g, §14.4), and
  its float rewrite shows the trap: folding `Vector3` operators into float expressions changes the last bits
  on the editor's Mono unless every value the `Vector3` form rounded is rounded with an explicit `(float)`.
- **Spread a burst across seats and frames:** several AI seats re-planning in one frame paid the whole cost at
  once; `SkimRaceReplanGate` lets one re-plan claim a frame and the next seat waits one frame
  (`Docs/SKIM_RACE_AI.md` §8.0i) - the pattern for any periodic heavy step several actors run.
- **Frame rate is a gameplay input, not just a cost:** contacts are tested on the 0.04 s fixed step and the
  trail is laid at most once per frame, so a thing tuned at one frame rate behaves differently at another
  (`Docs/SKIM_RACE_AI.md` §14 - the AI is now tuned across 62/36/20 fps). Measure a frame-rate-sensitive
  system at several frame rates before calling it tuned.

### Prism System Performance

The prism system was the most performance-critical gameplay system, and its rendering is now
**solved**: each prism keeps a GameObject (colliders, gameplay components) but its
`MeshRenderer` is disabled and it draws through an **instanced Entities Graphics companion
entity** (`PrismRenderService`, `Docs/PRISM_ECS_MIGRATION.md`). Measured: **103,823 prisms at
4.8 ms CPU vs 61.9 ms on the legacy path (12.9×)**, and 8,436 boot-world prisms in **23 draw
calls**. Prism animation is GPU-clocked (`Docs/PRISM_ANIMATION.md`), deaths are batched pure
entities, and spatial queries go through `PrismSpatialIndex`.

What still costs is what hangs **off** prisms — flora spindles (one GameObject renderer per grown
lattice prism), growth `Instantiate`, and GameObject existence itself. Current state, the ranked
lever list and the measuring method: **`Docs/PERFORMANCE_OPTIMIZATION.md`**. The pre-ECS audit
that used to be linked here (`_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md`) described an
architecture that no longer exists and was deleted 2026-09-22.
