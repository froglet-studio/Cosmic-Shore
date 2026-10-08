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
- A `ProfilerRecorder` built from code **records nothing unless started**: `ProfilerRecorderOptions.Default` is `SumAllSamplesInFrame | WrapAround` and does NOT include `StartImmediately`, and the constructor does not start it. Pass `Default | StartImmediately` (or call `Start()`), and report "marker not found" separately from "no samples" — the Crystal Flip Wave Benchmark's first cut printed `n/a` for every stat and the two causes looked identical (`Docs/TIME_CRYSTAL.md` §7)
- GPU instancing enabled on all prism and VFX materials
- Prism scale/material/effect animation is GPU-clock-driven (the clock-material law, `Docs/PRISM_ANIMATION.md`) — the former CPU Jobs+Burst animation managers are deleted
- Burst-compiled spatial queries replace Physics-based AOE prism damage (`PrismSpatialIndex` — see `Docs/SPATIAL_INDEX.md`)
- Cache-line-aware data layouts with hot/cold splitting and bit-packed flags (`PrismSpatialData` / `PrismDamageData` in `PrismSpatialIndex`)
- Growth occupancy checks use `PrismSpatialIndex.TryReserve` (claim-before-spawn), never `Physics.CheckBox` — prism colliders are disabled for the first 0.6s after spawn, so physics queries are structurally blind to fresh prisms

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
