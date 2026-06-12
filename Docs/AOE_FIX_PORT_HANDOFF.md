# AOE Fix Port Handoff — bleeding-edge

## What this branch proved

Branch `claude/ecs-migration-guide-Db42i` (based on `development`) identified and
fixed a critical performance defect in the AOE explosion → prism damage path:

- **Root cause**: `ExplosionImpactor.OnTriggerEnter` consumed 76.1% of frame time
  (61.55 ms self, 350 calls, 0.9 MB alloc per frame).
- **Defect 1 — silent fallback**: `BeginBatchProcessing()` returns without setting
  `_useBatchProcessing = true` when the registry singleton is null. Every explosion
  silently falls back to Physics OnTriggerEnter against all prisms.
- **Defect 2 — redundant PhysX work**: Even when batch processing IS active, the
  AOEExplosion's SphereCollider stays enabled, so PhysX still computes all trigger
  pairs (350+ per frame) even though `OnTriggerEnter` skips them.

### Fixes applied (development codebase paths)

| File (development) | Change |
|---|---|
| `Assets/_Scripts/Game/ImpactEffects/Impactors/ExplosionImpactor.cs` | `Debug.LogWarning` on fallback; `IsBatchProcessing` property; `ForceLegacyPhysics` static toggle for A/B; 3 ProfilerMarkers |
| `Assets/_Scripts/Game/Projectiles/AOEExplosion.cs` | Disable SphereCollider during batch processing; restore on all exit paths; 1 ProfilerMarker |
| `Assets/_Scripts/Game/Managers/PrismAOERegistry.cs` | 5 ProfilerMarkers; `RegisterSynthetic()` + `ClearAll()` for benchmarking |
| `Assets/_Scripts/Game/Managers/PrismEffectsManager.cs` | 2 ProfilerMarkers |
| `Assets/_Scripts/Utility/Tools/AOEBenchmarkOverlay.cs` | Runtime F9 IMGUI overlay (ProfilerRecorder-based) |
| `Assets/_Scripts/Utility/Tools/AOEBenchmarkRunner.cs` | Automated Physics/Burst/ECS comparison benchmark |
| `Assets/_Scripts/Utility/Tools/FrogletTools.cs` | Menu items for overlay + benchmark runner |

### Squirrel impact analysis

Squirrel triggers AOEExplosions through two paths, both terminating in the fixed code:

1. **Crystal collision**: `SquirrelVesselExplosionByCrystalEffect` → `ExplosionHelper.CreateExplosion`
   → AOEExplosion → ExplosionImpactor. Fires on every crystal pickup (0.15s cooldown, scale 400).
2. **Skimmer jousting**: `SquirrelSkimmerImpactorDataContainer` → `VesselExplosionBySkimmerEffect`
   → same path. Fires when a faster vessel skims past a slower one.

In a racing session with trail prisms accumulating, these scale-400 explosions resolve
against potentially thousands of prisms — the exact hot path that was profiled at 76% frame time.

## bleeding-edge target mapping

bleeding-edge reorganized scripts into `Assets/_Scripts/Controller/` and renamed
`PrismAOERegistry` → `PrismSpatialIndex`. The same two defects exist there.

| development path | bleeding-edge path |
|---|---|
| `_Scripts/Game/ImpactEffects/Impactors/ExplosionImpactor.cs` | `_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs` |
| `_Scripts/Game/Projectiles/AOEExplosion.cs` | `_Scripts/Controller/Projectiles/AOEExplosion.cs` |
| `_Scripts/Game/Managers/PrismAOERegistry.cs` | `_Scripts/Controller/Managers/PrismSpatialIndex.cs` |
| `_Scripts/Game/Managers/PrismEffectsManager.cs` | `_Scripts/Controller/Managers/PrismEffectsManager.cs` |

bleeding-edge already has:
- The same Burst `AOESpatialQueryJob` and hot/cold data split
- `MAX_NEW_HITS_PER_FRAME = 48` throttle
- `BeginBatchProcessing` / `ProcessBatchFrame` / `EndBatchProcessing` API
- A mature `PerformanceBenchmark` framework in `_Scripts/Utility/PerformanceBenchmark/`

bleeding-edge does NOT have:
- The SphereCollider disable during batch processing
- The fallback warning log
- Any ProfilerMarkers on the AOE hot path
- The `ForceLegacyPhysics` A/B toggle
- The AOE-specific benchmark runner (their framework measures whole-frame stats)

## Port priority

1. SphereCollider disable + fallback warning (highest value, smallest diff)
2. ProfilerMarkers on AOE hot path (11 markers across 4 files)
3. AOE benchmark runner (adapt `RegisterSynthetic`/`ClearAll` to `PrismSpatialIndex`)
4. Benchmark overlay (optional — their `DiagnosticsHUD` may already cover this)
