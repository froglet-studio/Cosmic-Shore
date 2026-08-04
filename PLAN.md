# Plan: Refactor Shape Spawning into Freestyle SegmentSpawner Pipeline

## Goal
Replace the static lobby-based shape selection (ModeSelectTrigger / ShapeSign text objects) with shapes that are **spawnable 3D objects** (made of prisms) living inside the SegmentSpawner rotation alongside helixes, ellipsoids, etc. When the vessel collides with a shape, it triggers shape drawing mode.

## Architecture Overview

### New Flow
1. Player clicks Freestyle → countdown → `BeginFreestyle()` → `SegmentSpawner.Initialize()` spawns segments **including shape objects** (Star, Circle, Lightning, Heart, Smiley)
2. Shapes are `SpawnableBase` subclasses that generate prism trails in recognizable 2D shapes, **plus** attach a trigger collider to the spawned container
3. Vessel flies through freestyle, encounters a shape → collides → fires `ShapeSignEvents.RaiseShapeSelected(shapeDef, worldPos)`
4. Controller shows connecting panel, nukes all freestyle objects, starts shape drawing mode
5. On shape exit → loading/connecting screen → return to freestyle with fresh `SegmentSpawner.Initialize()`

### No More Lobby
- Remove `EnterLobby()` / `ExitLobby()` / `ModeSelectTrigger` flow
- Freestyle starts directly after countdown
- Shape selection happens organically during gameplay via collision

---

## Files to Create

### 1. `SpawnableShapeBase.cs` — Abstract base for collidable shape spawnables
**Path:** `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableShapeBase.cs`

- Extends `SpawnableBase`
- Has `[SerializeField] ShapeDefinition shapeDefinition` — the SO to pass when triggered
- Overrides `Spawn(int intensity)` to:
  1. Call `base.Spawn(intensity)` to get the prism container
  2. Add a `SphereCollider` (isTrigger) to the container sized to the shape's bounding radius
  3. Add a `ShapeCollisionTrigger` MonoBehaviour to the container
- Subclasses only need to implement `GeneratePoints()` / `GenerateTrailData()` + `GetParameterHash()`

### 2. `ShapeCollisionTrigger.cs` — MonoBehaviour attached to spawned shape containers
**Path:** `Assets/_Scripts/Game/Environment/MiniGameObjects/ShapeCollisionTrigger.cs`

- Has a `ShapeDefinition` reference (set by `SpawnableShapeBase` after spawn)
- `OnTriggerEnter(Collider)` → checks for `VesselStatus` → fires `ShapeSignEvents.RaiseShapeSelected(shapeDef, transform.position)`
- One-shot trigger (prevents double-fire)
- Uses a `Rigidbody` (isKinematic) so trigger events fire

### 3. `SpawnableStar.cs` — Star shape (5-pointed)
**Path:** `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableStar.cs`

- Extends `SpawnableShapeBase`
- Generates star points: alternating outer/inner radius vertices connected by prism trails
- Parameters: `outerRadius`, `innerRadius`, `pointCount`, `blockCount`

### 4. `SpawnableCircle.cs` — Circle shape
**Path:** `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCircle.cs`

- Extends `SpawnableShapeBase`
- Generates points around a circle
- Parameters: `radius`, `blockCount`

### 5. `SpawnableLightning.cs` — Lightning bolt shape
**Path:** `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableLightning.cs`

- Extends `SpawnableShapeBase`
- Generates zigzag points forming a lightning bolt
- Parameters: `height`, `width`, `blockCount`

### 6. `SpawnableSmiley.cs` — Smiley face (multi-trail: eyes + mouth)
**Path:** `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableSmiley.cs`

- Extends `SpawnableShapeBase`
- Overrides `GenerateTrailData()` for multiple trails (left eye, right eye, mouth arc)
- Parameters: `radius`, `blockCount`

## Files to Modify

### 7. `SpawnableHeart.cs` — Existing, needs to extend SpawnableShapeBase
- Change parent from `SpawnableBase` → `SpawnableShapeBase`
- Add `ShapeDefinition` field (already has the parametric curve)
- No other changes needed — shape generation is already correct

### 8. `SinglePlayerFreestyleController.cs` — Major refactor
- **Remove**: `_isInLobby`, `_isShapePrep`, `_isFreestylePrep` state flags
- **Remove**: `EnterLobby()`, `ExitLobby()`, lobby-related methods
- **Remove**: `ModeSelectTrigger` references and handlers
- **Simplify**: After initial countdown → `BeginFreestyle()` directly
- **Add**: `HandleShapeSignSelected()` → show connecting panel → nuke freestyle → start shape mode
- **Add**: `OnShapeDrawingFinished()` → show connecting panel → re-initialize freestyle
- Keep `ShapeSignEvents.OnShapeSelected` subscription (shapes fire this via `ShapeCollisionTrigger`)

### 9. `SegmentSpawner.cs` — No code changes needed
- Shape spawnables are `SpawnableBase` subclasses, they plug directly into `spawnableSegments` list via Inspector
- Just need to assign them as SOs in the scene with appropriate weights

---

## Collision Detection Strategy

Spawned shapes are containers of prisms. To detect "vessel entered a shape":
- `SpawnableShapeBase.Spawn()` adds a `SphereCollider` (isTrigger, sized to bounding box) + `Rigidbody` (isKinematic) + `ShapeCollisionTrigger` to the container GameObject
- This is separate from individual prism collisions (which are handled by the impact system)
- The sphere collider encompasses the entire shape, so the vessel triggers it when flying near/through

---

## Scene Setup (Inspector changes, not code)

1. Create `SpawnableStar`, `SpawnableCircle`, `SpawnableLightning`, `SpawnableSmiley` GameObjects in scene (or as prefabs)
2. Assign them to `SegmentSpawner.spawnableSegments` with low weights (~0.05-0.1 each)
3. Assign `ShapeDefinition` SOs to each spawnable
4. Remove `ModeSelectTrigger` objects from scene
5. Remove `signsParent` reference

---

## Transition Flow Detail

### Freestyle → Shape Mode
1. Vessel hits shape's SphereCollider
2. `ShapeCollisionTrigger.OnTriggerEnter()` → `ShapeSignEvents.RaiseShapeSelected(def, pos)`
3. `SinglePlayerFreestyleController.HandleShapeSignSelected()`:
   - `SegmentSpawner.NukeTheTrails()` — destroy all freestyle objects
   - `ClearPlayerTrails()`
   - Show connecting panel via `miniGameHUD.ShowConnectingFlow()`
   - On ready → countdown → `shapeDrawingManager.BeginDrawing()`

### Shape Mode → Freestyle
1. Shape drawing completes → `OnShapeDrawingFinished()`
2. Show connecting panel via `miniGameHUD.ShowConnectingFlow()`
3. On ready → countdown → `BeginFreestyle()` (fresh `SegmentSpawner.Initialize()`)
