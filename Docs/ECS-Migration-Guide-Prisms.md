# ECS Migration Guide: Cosmic Shore Prisms

## Where You Stand

The codebase is already ~60% DOTS-native in practice:

| Layer | Current State |
|-------|--------------|
| Data layout | Cache-packed NativeArrays (`PrismSpatialData` 16B, `PrismDamageData` 8B) |
| Compute | 5 Burst-compiled `IJobParallelFor` across AOE, scale, material, effects, density |
| ECS components | Already defined in `PrismComponents.cs` — `PrismData`, `ScaleAnimation`, `MaterialAnimation`, `ShieldTimer`, `ExplosionEffect`, `ImplosionEffect` |
| Lifecycle | Centralized managers with register/unregister patterns (effectively entity lifecycle) |
| MonoBehaviour | Still the backbone — 5 RequireComponents per prism, coroutine init, event callbacks |

The existing ECS components in `PrismComponents.cs` map cleanly to the current systems. The migration is essentially replacing the plumbing (MonoBehaviour lifecycle, singletons, callbacks) while keeping the data layout and job logic already in place.

### Key Files

| File | Path | Role |
|------|------|------|
| `PrismComponents.cs` | `Assets/_Scripts/Game/ECS/Components/PrismComponents.cs` | ECS component definitions |
| `PrismAOERegistry.cs` | `Assets/_Scripts/Game/Managers/PrismAOERegistry.cs` | Hot/cold NativeArray AOE processing |
| `PrismScaleManager.cs` | `Assets/_Scripts/Game/Managers/PrismScaleManager.cs` | Burst-compiled scale animation |
| `MaterialStateManager.cs` | `Assets/_Scripts/Game/Managers/MaterialStateManager.cs` | Burst-compiled material animation |
| `PrismTimerManager.cs` | `Assets/_Scripts/Game/Managers/PrismTimerManager.cs` | Centralized shield timers |
| `PrismEffectsManager.cs` | `Assets/_Scripts/Game/Managers/PrismEffectsManager.cs` | Burst-compiled explosion/implosion VFX |
| `Prism.cs` | `Assets/_Scripts/Game/Ship/Prism.cs` | Core MonoBehaviour (migration target) |
| `PrismFactory.cs` | `Assets/_Scripts/Game/Prisms/PrismFactory.cs` | Spawn factory with pool managers |

---

## Recommended Phased Migration

### Phase 0: Hybrid Bridge

Convert `PrismAOERegistry` from a singleton managing NativeArrays to an `ISystem` reading from ECS components. This is the lowest-risk, highest-reward step because:

- The registry already stores data in NativeArrays separate from GameObjects
- `AOESpatialQueryJob` is already Burst-compiled and doesn't touch managed types
- You just need to swap the data source from manual arrays to an `EntityQuery`

```
Before: PrismAOERegistry._spatial[i].Position  (manual NativeArray)
After:  EntityQuery over PrismData components   (ECS-managed memory)
```

Each prism MonoBehaviour gets a companion entity via a `PrismEntityBridge` component that holds the `Entity` reference. The MonoBehaviour pushes state changes to the entity; the `ISystem` reads from entities for batch processing.

#### Implementation sketch

```csharp
// New bridge component on the MonoBehaviour side
public class PrismEntityBridge : MonoBehaviour
{
    public Entity Entity;
    public EntityManager EntityManager;
}

// In Prism.Initialize(), after existing setup:
var world = World.DefaultGameObjectInjectionWorld;
var em = world.EntityManager;
var entity = em.CreateEntity(
    typeof(PrismData),
    typeof(LocalTransform)
);
em.SetComponentData(entity, new PrismData
{
    Position = transform.position,
    Domain = (int)Domain,
    Volume = prismProperties.volume,
    IsShielded = prismProperties.IsShielded ? (byte)1 : (byte)0,
    // ...
});
bridge.Entity = entity;
bridge.EntityManager = em;
```

The `AOESpatialQueryJob` remains identical — it just reads from `EntityQuery<PrismData, LocalTransform>` instead of the manual `_spatial` NativeArray.

---

### Phase 1: Animation Systems

Replace three singleton managers with three ISystems:

| Current Manager | New ISystem | Reads | Writes |
|----------------|-------------|-------|--------|
| `PrismScaleManager` | `ScaleAnimationSystem` | `ScaleAnimation` (enabled) | `LocalTransform.Scale` |
| `MaterialStateManager` | `MaterialAnimationSystem` | `MaterialAnimation` (enabled) | Shared component or managed component for MaterialPropertyBlock |
| `PrismTimerManager` | `ShieldTimerSystem` | `ShieldTimer` (enabled) | Disables `ShieldTimer`, updates `PrismData.IsShielded` |

All three already use `IJobParallelFor` — the job logic ports directly. The `IEnableableComponent` pattern on the existing structs is exactly right for toggling without archetype changes.

#### Scale system example

```csharp
[BurstCompile]
public partial struct ScaleAnimationSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        new ScaleAnimationJob { DeltaTime = dt }
            .ScheduleParallel();
    }
}

[BurstCompile]
partial struct ScaleAnimationJob : IJobEntity
{
    public float DeltaTime;

    void Execute(
        ref ScaleAnimation anim,
        ref LocalTransform transform,
        EnabledRefRW<ScaleAnimation> enabled)
    {
        var diff = anim.TargetScale - anim.CurrentScale;
        if (math.lengthsq(diff) <= 0.01f)
        {
            anim.CurrentScale = anim.TargetScale;
            transform.Scale = anim.TargetScale.x; // uniform
            enabled.ValueRW = false;
            return;
        }

        float lerpSpeed = math.clamp(anim.GrowthRate * DeltaTime, 0.05f, 0.1f);
        anim.CurrentScale = math.lerp(anim.CurrentScale, anim.TargetScale, lerpSpeed);
        transform.Scale = anim.CurrentScale.x;
    }
}
```

This mirrors the existing `UpdateScalesJob` in `PrismScaleManager.cs` almost line-for-line.

---

### Phase 2: Effects

Convert `PrismEffectsManager` explosion/implosion tracking to entities using `ExplosionEffect` and `ImplosionEffect` components. The pooling model changes from `GenericPoolManager<T>` to archetype-based entity reuse (disable + re-enable).

#### VFX rendering decision

| Option | Approach | Tradeoff |
|--------|----------|----------|
| Entities Graphics | `MaterialMeshInfo` for mesh-based effects (scale/color animation on quads) | Best perf, more migration work |
| Managed companion | Sync ECS state to a `ParticleSystem` via companion component | Easier transition, slight overhead |

The existing `PrismEffectsManager` already uses `MaterialPropertyBlock` with shader property IDs (`_ExplosionAmount`, `_Opacity`, `_State`, `_Location`). The Entities Graphics path would replace these with `MaterialOverrideComponent` or a custom Burst material system.

---

### Phase 3: Full Prism Entity

Remove `MonoBehaviour Prism` entirely. Each prism becomes a pure entity:

```
Entity:
  PrismData              (core state)
  LocalTransform         (position/rotation/scale)
  ScaleAnimation         (enableable)
  MaterialAnimation      (enableable)
  ShieldTimer            (enableable)
  RenderMesh / MaterialMeshInfo  (Entities Graphics)
  PhysicsShape           (static collider, replaces BoxCollider)
```

The 5 `RequireComponent` MonoBehaviours on `Prism.cs` (currently `MaterialPropertyAnimator`, `PrismScaleAnimator`, `PrismTeamManager`, `PrismStateManager`, plus `BoxCollider`/`MeshRenderer`) collapse into component data on a single entity. For 3,000 prisms this eliminates ~15,000 MonoBehaviour Update overhead entries.

---

### Phase 4: Networking

The codebase uses Netcode for GameObjects 2.5.0 (`Packages/manifest.json`), which wraps `NetworkBehaviour` around MonoBehaviours.

#### Option 1: Keep vessels as hybrid (recommended)

Vessels stay as `NetworkBehaviour` GameObjects; prisms are pure ECS entities owned by vessel entities via a `VesselOwner` component. Network replication happens at the vessel level, prism state is deterministic from vessel actions.

#### Option 2: Migrate to Netcode for Entities

Full DOTS networking. Much larger scope, but gives server-authoritative entity replication for prisms. Only worth it if per-prism network sync is required.

For most game modes, **option 1 is sufficient** — prisms are spawned locally based on vessel commands, and the AOE/damage logic is deterministic.

---

## Migration Blockers & Solutions

| Blocker | Impact | Solution |
|---------|--------|----------|
| Coroutine in `Prism.Initialize()` | Low | Replace with `ShieldTimer` component + creation-delay system |
| `Action<T>` events (~6 sites in `Prism.cs`) | Low | Component state flags + system polling, or `EntityCommandBuffer` |
| SOAP event channels (`ScriptableEventPrismStats`, etc.) | Medium | Keep for cross-system communication (UI, scoring). Don't migrate these to ECS — they're fine as-is |
| VContainer DI | Medium | ECS systems don't use DI. Pass dependencies via singleton components or system state |
| `Trail` (`List<Prism>`) | Medium | `DynamicBuffer<PrismTrailElement>` on vessel entity |
| `MaterialPropertyBlock` rendering | Medium | Entities Graphics `MaterialMeshInfo` + `MaterialOverrideComponent` or custom Burst material system |
| Netcode for GameObjects | High | Keep vessels hybrid (Phase 4, option 1) |

---

## What NOT to Migrate

Some things are better left as MonoBehaviours:

- **Vessels/Ships** — Complex, networked, input-driven, few in count (~2-8). No perf benefit from ECS.
- **UI/HUD** — Already works fine with UGUI/Canvas.
- **Camera/Cinemachine** — No reason to change.
- **Scene management** — MonoBehaviour lifecycle is fine here.
- **Scoring/Events** — The SOAP `ScriptableObject` pattern is elegant and decoupled; ECS doesn't improve it.

---

## Component Mapping Quick Reference

```
MonoBehaviour World              ->  ECS World
─────────────────────────────────────────────────
Prism.cs                         ->  PrismData
PrismScaleAnimator               ->  ScaleAnimation (IEnableableComponent)
MaterialPropertyAnimator         ->  MaterialAnimation (IEnableableComponent)
PrismStateManager                ->  ShieldTimer (IEnableableComponent) + PrismData flags
PrismTeamManager                 ->  PrismData.Domain
PrismAOERegistry._spatial[]      ->  EntityQuery<PrismData, LocalTransform>
PrismAOERegistry._damage[]       ->  EntityQuery<PrismData> (Volume, Domain fields)
PrismAOERegistry._prisms[]       ->  Entity references (no managed array)
PrismScaleManager                ->  ScaleAnimationSystem : ISystem
MaterialStateManager             ->  MaterialAnimationSystem : ISystem
PrismTimerManager                ->  ShieldTimerSystem : ISystem
PrismEffectsManager              ->  ExplosionSystem + ImplosionSystem : ISystem
GenericPoolManager<Prism>        ->  Archetype reuse (enable/disable entities)
```

---

## Recommendation

Start with **Phase 0 (hybrid bridge)**. It's low-risk because:

1. The MonoBehaviour `Prism` still works as the authoring/interaction layer
2. The Burst jobs don't change — they just read from ECS memory instead of manual NativeArrays
3. You can A/B test the two paths with the existing `_useBatchProcessing` toggle pattern
4. If anything breaks, the Physics fallback path still works

Once Phase 0 proves stable, Phases 1-2 are mechanical — each singleton manager becomes an `ISystem` with almost identical job code. Phase 3 is the real cutover where you drop the MonoBehaviour entirely.
