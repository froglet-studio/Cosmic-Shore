# Prism System Performance Audit

## Executive Summary

When enormous numbers of prisms explode simultaneously, the performance bottleneck is a **cascade of per-object overhead**: each destroyed prism spawns a pooled explosion/implosion effect with its own MonoBehaviour + per-frame MaterialPropertyBlock updates + per-frame position updates via UniTask coroutines. Meanwhile, the surviving prisms continue running 4-5 MonoBehaviours each, coroutines for timed shields, and physics colliders. The current architecture already uses Unity Jobs + Burst for scale and material animation batching, but the explosion/implosion VFX path, the collision system, and the per-prism component overhead remain entirely on the main thread using GameObjects.

---

## Current Architecture Overview

### Per-Prism Component Stack (5-6 MonoBehaviours per prism)

Each active prism is a **full GameObject** with:

| Component | Purpose | Per-Frame Cost |
|---|---|---|
| `Prism` | Core lifecycle, coroutines, events | Coroutine ticks, event dispatch |
| `PrismScaleAnimator` | Scale lerp registration | Property reads, manager registration |
| `MaterialPropertyAnimator` | Material state transitions | PropertyBlock get/set when animating |
| `PrismTeamManager` | Domain ownership | Material lookups on team change |
| `PrismStateManager` | Shield/danger state machine | Coroutines for timed shields |
| `PrismImpactor` (on prefab) | Collision dispatch | OnTriggerEnter per collision |
| `BoxCollider` | Physics volume | Broad/narrow phase per frame |
| `MeshRenderer` | Rendering | Draw call per visible prism |

**At 500 prisms**: ~3,000 active MonoBehaviour instances + 500 active colliders.
**At 2,000 prisms**: ~12,000 MonoBehaviour instances + 2,000 colliders.

### What's Already Optimized (Jobs + Burst)

1. **PrismScaleManager** (`PrismScaleManager.cs:64-72`): `IJobParallelFor` + `[BurstCompile]` for scale lerping. Batch size 128.
2. **MaterialStateManager** (`MaterialStateManager.cs:69-76`): `IJobParallelFor` + `[BurstCompile]` for color/spread interpolation. Batch size 128.
3. **AdaptiveAnimationManager** (`AdaptiveAnimationManager.cs:109-163`): Dynamic frame-skipping (1x-12x) based on performance pressure.
4. **Object Pooling** (`GenericPoolManager.cs`): Unity ObjectPool<T> with async buffer maintenance, batch release.

### What's NOT Optimized (Main Thread Bottlenecks)

1. **Explosion/Implosion VFX** - individual UniTask coroutines per effect
2. **Physics colliders** - per-prism BoxCollider in Unity's PhysX
3. **Per-prism MonoBehaviour overhead** - 5-6 components per object
4. **Material instancing** - `renderer.material` (not `sharedMaterial`) in several paths
5. **Unbounded trail growth** - no cap on total prisms in scene
6. **Event cascade on mass destruction** - per-prism event raises

---

## Bottleneck Analysis: Mass Explosion Scenario

When N prisms explode simultaneously, here is the frame-by-frame cost:

### Frame 0: Destruction Trigger
For each destroyed prism:
1. `Prism.Damage()` → `Prism.Explode()` (`Prism.cs:263-276`)
2. `SetupDestruction()` disables collider + renderer, raises `_onTrailBlockDestroyedEventChannel` event
3. `OnBlockImpactedEventChannel.RaiseEvent()` → PrismFactory receives it synchronously
4. `PrismFactory.SpawnExplosion()` (`PrismFactory.cs:172-179`):
   - Gets PrismExplosion from pool (activates GameObject)
   - Sets scale, configures team colors via MaterialPropertyBlock
   - Calls `TriggerExplosion(velocity)` which starts a new UniTask

**Cost for N=500 simultaneous explosions in one frame:**
- 500 × `SetupDestruction()` calls (each disables components, raises events)
- 500 × `PrismFactory.OnPrismSpawnedEventRaised()` synchronous calls
- 500 × pool Get (GameObject.SetActive(true))
- 500 × `ConfigureForTeam()` (GetPropertyBlock + SetPropertyBlock)
- 500 × new `CancellationTokenSource` allocation
- 500 × `UniTask.Forget()` scheduling

### Frames 1-300 (5 seconds of explosion animation):
Each active PrismExplosion runs **per-frame** (`PrismExplosion.cs:115-168`):
- `ct.ThrowIfCancellationRequested()`
- Position update: `transform.position = initialPosition + duration * velocity`
- `_renderer.GetPropertyBlock(_mpb)`
- `_mpb.SetFloat(ExplosionAmountID, ...)`
- `_mpb.SetFloat(OpacityID, ...)`
- `_renderer.SetPropertyBlock(_mpb)`
- `UniTask.Yield()` (schedules next frame)

**Per frame cost for 500 active explosions:**
- 500 × transform.position write
- 500 × GetPropertyBlock + SetPropertyBlock (2 API calls each = 1,000)
- 500 × UniTask continuations
- 500 × MeshRenderer draw calls (explosion meshes are full prism geometry)

### Physics Aftermath
Destroyed prisms disable their colliders, so PhysX load decreases. But if there are 2,000 total prisms and 500 explode, the remaining 1,500 colliders are still in the broad phase. PhysX broad phase scales roughly O(n log n).

---

## Recommendations (Ordered by Impact-to-Effort Ratio)

---

### 1. Batch Explosion/Implosion VFX into a Jobs-Based Manager

**Current**: Each PrismExplosion/PrismImplosion runs its own UniTask coroutine with per-frame `GetPropertyBlock`/`SetPropertyBlock` and `transform.position` updates.

**Proposed**: Create an `ExplosionAnimationManager` (following the existing `AdaptiveAnimationManager` pattern) that:
- Collects all active explosions into a NativeArray
- Runs a single `IJobParallelFor` + `[BurstCompile]` to compute positions, explosion amounts, and opacities
- Applies results in a single batched loop using a shared MaterialPropertyBlock
- Eliminates 500 individual UniTask continuations per frame

**Implementation**: Mirror `PrismScaleManager` pattern exactly.

```
struct ExplosionAnimationData {
    float3 initialPosition;
    float3 velocity;
    float elapsed;
    float maxDuration;
    float speed;
}
```

**Expected benefit**:
- **CPU**: 60-80% reduction in explosion frame cost. Eliminates per-explosion UniTask scheduling overhead (~2-4μs each × 500 = 1-2ms saved), replaces 500 individual GetPropertyBlock/SetPropertyBlock pairs with one batched pass, and moves position/opacity math to Burst-compiled worker threads.
- **At 500 explosions**: From ~8-12ms/frame to ~2-4ms/frame for the explosion system.

---

### 2. Convert Explosion/Implosion Rendering to GPU Instancing or Particle System

**Current**: Each PrismExplosion is a full GameObject with MeshRenderer. 500 explosions = 500 draw calls (before dynamic batching).

**Proposed** (two options):

**Option A - GPU Instanced Rendering (moderate effort)**:
- Use `Graphics.DrawMeshInstanced()` or `Graphics.RenderMeshInstanced()` with a NativeArray of matrices + MaterialPropertyBlock arrays
- Eliminates all explosion GameObjects from rendering pipeline
- Single draw call for all explosions of the same mesh/material
- Explosion data (position, scale, explosion amount, opacity) passed as per-instance shader properties

**Option B - VFX Graph / Particle System (lower effort, different visual)**:
- Replace mesh-based explosion with a VFX Graph that accepts spawn events
- Send burst of N particles with position/velocity/scale data
- GPU-simulated particles, zero per-frame CPU cost after spawn

**Expected benefit**:
- **Rendering**: 500 draw calls → 1 draw call (Option A) or 0 CPU draw calls (Option B). At 500 explosions this saves ~3-6ms of render thread time.
- **CPU**: Eliminates all per-explosion transform updates from the render pipeline.
- **Combined with Recommendation 1**: Explosion system goes from ~12-18ms total to ~1-3ms total.

---

### 3. Convert Prisms to DOTS Entities (Full ECS Conversion)

**Current**: Each prism is a GameObject with 5-6 MonoBehaviours, a BoxCollider, and a MeshRenderer.

**Proposed**: Convert prism data into ECS entities using the already-installed `com.unity.entities` v1.4.2 and `com.unity.entities.graphics` v1.4.15 packages:

**ECS Components (IComponentData)**:
```csharp
// Core data
struct PrismData : IComponentData {
    float3 position;
    quaternion rotation;
    float3 scale;
    float3 targetScale;
    float growthRate;
    float volume;
    int domain;         // Jade/Ruby/Gold/Unassigned
    int state;          // Normal/Shielded/SuperShielded/Dangerous
    int trailIndex;
    bool destroyed;
    bool devastated;
}

// Animation state (only on actively animating prisms)
struct ScaleAnimation : IComponentData, IEnableableComponent {
    float3 currentScale;
    float3 targetScale;
    float growthRate;
}

// Material animation
struct MaterialAnimation : IComponentData, IEnableableComponent {
    float4 startBright, targetBright;
    float4 startDark, targetDark;
    float3 startSpread, targetSpread;
    float progress, duration;
}
```

**ECS Systems (ISystem)**:
- `PrismScaleSystem` - replaces PrismScaleManager + PrismScaleAnimator
- `PrismMaterialSystem` - replaces MaterialStateManager + MaterialPropertyAnimator
- `PrismStateSystem` - replaces PrismStateManager (timed shields via elapsed time, no coroutines)
- `PrismDestructionSystem` - replaces Prism.Explode/Implode logic, batches destruction events

**Rendering via Entities Graphics**:
- Use `RenderMeshArray` + `MaterialMeshInfo` for automatic GPU instancing
- Entities Graphics handles LOD, culling, and batched draw calls automatically
- Per-entity shader properties via `MaterialOverrideComponent<T>`

**Physics via custom spatial queries** (not Unity Physics package):
- Replace BoxColliders with a spatial hash grid (NativeMultiHashMap)
- Prisms are static after placement - no need for continuous PhysX simulation
- Query the grid on vessel/projectile movement (only moving objects query)
- Eliminates entire PhysX broad phase for prisms

**Expected benefit**:
- **Memory**: ~90% reduction per prism. From ~2-4KB per prism (GameObject + 6 MonoBehaviours + collider + renderer metadata) to ~100-200 bytes per entity (pure data).
- **CPU**: Eliminates all MonoBehaviour overhead (no virtual Update calls, no coroutine scheduler). Scale/material animations already batched via Jobs but now with zero managed-to-native marshaling overhead.
- **Rendering**: Automatic GPU instancing via Entities Graphics. 2,000 identical prisms rendered in 1-4 draw calls instead of 2,000.
- **Physics**: Spatial hash query is O(1) per query vs O(n log n) broad phase. At 2,000 prisms, saves ~2-5ms/frame of PhysX time.
- **At 2,000 prisms**: Total prism system cost drops from ~15-25ms/frame to ~2-5ms/frame.
- **Scaling ceiling**: Can handle 10,000-50,000 prisms before hitting similar performance walls.

**Risk**: This is the highest-effort recommendation. Requires rewriting the interaction between GameObjects (vessels, projectiles) and entity prisms via hybrid bridge. Recommend implementing incrementally - start with rendering only, then add systems.

---

### 4. Eliminate Material Instancing Leaks

**Current problem**: Several code paths use `renderer.material` (which clones the material) instead of `renderer.sharedMaterial` or MaterialPropertyBlock:

1. **`MaterialPropertyAnimator.ValidateMaterials()`** (`MaterialPropertyAnimator.cs:121-123`):
   ```csharp
   MeshRenderer.material = activeTransparentMaterial; // CLONES material
   ```
2. **`MaterialPropertyAnimator.UpdateMaterial()` completion callback** (`MaterialPropertyAnimator.cs:180`):
   ```csharp
   MeshRenderer.material = cachedPrism.prismProperties.IsTransparent ?
       transparentMaterial : opaqueMaterial; // CLONES material
   ```
3. **`MaterialPropertyAnimator.SetTransparency()`** (`MaterialPropertyAnimator.cs:192`):
   ```csharp
   MeshRenderer.material = transparent ? activeTransparentMaterial : activeOpaqueMaterial; // CLONES
   ```
4. **`Prism.OnDestroy()`** (`Prism.cs:369-373`):
   ```csharp
   Destroy(meshRenderer.material); // Cleaning up the cloned material
   ```
   This confirms the clone is known and "cleaned up," but the allocation + GC pressure still occurs.

**Proposed**: Switch all material swaps to `renderer.sharedMaterial` and use MaterialPropertyBlock exclusively for per-instance properties. The MaterialPropertyBlock system is already in place - the material swaps are only needed for switching between opaque/transparent render queues. Consider using a single shader with an `_Opacity` property and a `_RenderQueue` override instead of two separate materials.

**Expected benefit**:
- **GC pressure**: Eliminates material clone allocations. Each clone is ~1-2KB. At 500 prisms changing state, that's 0.5-1MB of garbage per event.
- **Frame spikes**: Reduces GC collection pauses that can cause 5-15ms hitches.
- **Memory**: Steady-state memory reduction of 1-4MB for scenes with many prisms.

---

### 5. Cap Active Explosion/Implosion Effects

**Current**: No limit on concurrent explosion effects. If 500 prisms die at once, 500 PrismExplosion objects activate and run for up to 5 seconds.

**Proposed**:
- Set a maximum concurrent explosion limit (e.g., 50-100)
- When the limit is hit, either:
  - Skip spawning new explosion effects (distant/small prisms don't need individual effects)
  - Replace distant explosions with a single particle burst
  - Use LOD-based selection: only nearby explosions get full mesh effects
- Priority system: larger prisms and closer prisms get effects first

**Implementation**: Add a counter to PrismFactory or the explosion pool manager. Before spawning, check count. If over limit, either skip or spawn a cheaper alternative.

**Expected benefit**:
- **CPU**: Hard cap prevents worst-case explosion cost from growing linearly. At cap of 100: worst case ~4ms vs uncapped 500: ~18ms.
- **Rendering**: Caps draw calls at a known maximum.
- **Perceptual**: Players cannot visually distinguish 50 vs 500 simultaneous explosions in a chaotic scene.

---

### 6. Replace Per-Prism Coroutines with Timer Data

**Current**: `PrismStateManager` uses `StartCoroutine` for timed shields (`PrismStateManager.cs:56-57`, `117-123`):
```csharp
activeStateCoroutine = StartCoroutine(TimedShieldCoroutine(duration));
```
And `Prism.Initialize()` uses `StartCoroutine(CreateBlockCoroutine(...))` (`Prism.cs:131`).

Each coroutine is a heap allocation and adds to Unity's coroutine scheduler overhead.

**Proposed**: Replace with a centralized timer system:
- Store `shieldEndTime` as a float on PrismStateManager
- In a single manager Update(), iterate all prisms with active timers and check `Time.time >= shieldEndTime`
- Or, with ECS: use `IEnableableComponent` and a system that checks elapsed time

Similarly, replace `CreateBlockCoroutine` with a delayed-activation manager that checks timestamps.

**Expected benefit**:
- **CPU**: Eliminates coroutine scheduler overhead. At 500 prisms with timed shields: saves ~0.5-1ms/frame of coroutine scheduling.
- **GC**: Eliminates `WaitForSeconds` heap allocations (~40 bytes each).
- **Scaling**: Timer check is O(n) with no allocations vs coroutines which have per-instance overhead.

---

### 7. Spatial Partitioning for Collision Detection

**Current**: All prisms have `BoxCollider` components. Unity's PhysX handles broad phase for all colliders every fixed update frame. Prisms are static after placement but still participate in the full broad phase.

**Proposed**:
- Mark prism colliders as `Static` (if they don't move after creation)
- Or replace physics colliders entirely with a spatial hash/grid (builds on existing `BlockDensityGrid` pattern)
- Moving objects (vessels, projectiles, skimmers) query the spatial grid instead of relying on PhysX triggers

**Expected benefit**:
- **CPU**: PhysX broad phase for 2,000 static + 10 dynamic objects: ~2-4ms. With spatial hash: ~0.1-0.3ms for the 10 dynamic queries.
- **Memory**: Eliminates PhysX internal data structures per collider (~200-500 bytes each).
- **At 2,000 prisms**: 80-95% reduction in collision detection cost.

---

### 8. Pool Size Tuning and Global Prism Budget

**Current**: Each of the 7 prism pools has `maxSize = 100` (configurable in inspector), but there's no global cap. Theoretical max: 700 prisms + unlimited explosions/implosions. Trail lists grow unbounded.

**Proposed**:
- Implement a global prism budget (e.g., 1,000-2,000 max active prisms)
- When budget is exceeded, recycle the oldest/farthest prisms (return to pool)
- Trail.Add() should check budget before adding
- Explosion pool should have a hard cap (see Recommendation 5)

**Expected benefit**:
- **Guarantees**: Prevents unbounded growth that leads to progressive frame time degradation.
- **Memory**: Caps peak memory usage for prism-related objects.
- **Predictability**: Game designers can tune the budget per platform (mobile vs desktop).

---

## Performance Benefit Summary

| # | Recommendation | Effort | CPU Savings (500 explosions) | Scaling Impact |
|---|---|---|---|---|
| 1 | Batch explosion VFX into Jobs manager | Medium | 6-8ms/frame | Linear → constant |
| 2 | GPU instanced explosion rendering | Medium | 3-6ms/frame (render thread) | N draw calls → 1 |
| 3 | Full DOTS/ECS conversion | High | 10-20ms/frame total | 10-50x capacity increase |
| 4 | Fix material instancing leaks | Low | 5-15ms GC spikes eliminated | Removes GC pressure |
| 5 | Cap concurrent explosion effects | Low | Up to 15ms worst-case removed | Hard performance ceiling |
| 6 | Replace coroutines with timers | Low | 0.5-1ms/frame | Eliminates per-object alloc |
| 7 | Spatial partitioning for collisions | Medium-High | 2-4ms/frame | O(n log n) → O(k) |
| 8 | Global prism budget | Low | Prevents degradation | Guarantees frame budget |

### Recommended Implementation Order

**Phase 1 - Quick wins (Low effort, immediate impact):**
- Recommendation 5: Cap explosion effects
- Recommendation 4: Fix material instancing
- Recommendation 6: Replace coroutines with timers
- Recommendation 8: Global prism budget

**Phase 2 - Explosion system overhaul (Medium effort):**
- Recommendation 1: Jobs-based explosion manager
- Recommendation 2: GPU instanced explosion rendering

**Phase 3 - Architecture transformation (High effort, highest payoff):**
- Recommendation 7: Spatial partitioning
- Recommendation 3: Full DOTS/ECS conversion (incremental)

---

## Appendix: Key File Locations

| File | Path | Role |
|---|---|---|
| Prism.cs | `Assets/_Scripts/Game/Ship/Prism.cs` | Core prism lifecycle |
| PrismFactory.cs | `Assets/_Scripts/Game/Prisms/PrismFactory.cs` | Spawning/pooling dispatch |
| PrismExplosion.cs | `Assets/_Scripts/Utility/Effects/PrismExplosion.cs` | Per-explosion UniTask animation |
| PrismImplosion.cs | `Assets/_Scripts/Utility/Effects/PrismImplosion.cs` | Per-implosion UniTask animation |
| PrismScaleManager.cs | `Assets/_Scripts/Game/Managers/PrismScaleManager.cs` | Burst-compiled scale batching |
| MaterialStateManager.cs | `Assets/_Scripts/Game/Managers/MaterialStateManager.cs` | Burst-compiled material batching |
| AdaptiveAnimationManager.cs | `Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs` | Dynamic frame-skipping base |
| GenericPoolManager.cs | `Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs` | Object pooling base |
| PrismScaleAnimator.cs | `Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs` | Per-prism scale component |
| MaterialPropertyAnimator.cs | `Assets/_Scripts/Game/Environment/Prisms/MaterialPropertyAnimator.cs` | Per-prism material component |
| PrismStateManager.cs | `Assets/_Scripts/Game/Managers/PrismStateManager.cs` | Shield/danger state machine |
| PrismTeamManager.cs | `Assets/_Scripts/Game/Managers/PrismTeamManager.cs` | Team ownership + material |
| VesselPrismController.cs | `Assets/_Scripts/Game/Ship/VesselPrismController.cs` | Trail spawning loop |
| Trail.cs | `Assets/_Scripts/Game/Ship/Trail.cs` | Unbounded prism list |
| BlockDensityGrid.cs | `Assets/_Scripts/Game/Managers/BlockDensityGrid.cs` | Spatial density (Jobs-based) |
