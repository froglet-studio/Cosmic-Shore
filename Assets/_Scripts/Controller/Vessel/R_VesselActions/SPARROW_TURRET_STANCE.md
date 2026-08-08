# Sparrow — Turret Stance (MASS)

> **The rule, in one line:** *a turret shot **is** a bullet — you just see a prism flying,
> and where the bullet would have been destroyed the prism stays.*

While the Sparrow is stopped (`IVesselStatus.IsTranslationRestricted`), its guns fire
**prisms**. Everything about the shot is the bullet's: fire rate, muzzle speed, eased flight
path, impact effects, and the SPACE-5 gate on whether it pierces. Exactly two things differ:

1. **What you see flying is the prism**, not a tracer.
2. **Where the bullet would be destroyed** — a stopping prism impact, or its lifetime
   expiring — the prism **stays there** as permanent world mass, instead of the shot simply
   vanishing.

Nothing else is meant to differ. Piercing in particular is **not** a turret perk: below
SPACE 5 a turret prism is stopped by the first prism it hits (and anchors there); at 5+ it
pierces on to the end of its path (and anchors there). Same gate, same upgrade, both modes.

## Why the parity is structural

Authoring the turret's cadence next to the gun's drifts the moment anyone retunes one, and
it had: the turret shipped at **14 shots/s and 150 u/s** against guns firing **30 shots/s at
1500 u/s** — a 2× cadence gap and a 10× speed gap that no single edit could close.

So `FullAutoBlockShootActionSO` authors no cadence at all. It holds a reference to the
vessel's `FullAutoActionSO` and derives:

| Turret quantity | Comes from |
|---|---|
| Fire rate | `FullAutoActionSO.FiringRate` |
| Muzzle speed | `FullAutoActionSO.ResolveSpeed(status)` — authored base × the live SPACE multiplier |
| Flight time / range | `FullAutoActionSO.ProjectileTime`, on the bullets' easing curve |
| Pierce | `IsUpgradeActive(Element.Space)` → `stopOnFirstPrismImpact: !piercing` |

`ResolveSpeed` lives on the SO (not in the gun executor) precisely so both fire modes call
the same method. Retune the cannons and the turret follows; there is no second number.

Only the genuinely turret-specific things are authored here: the prism's shape and which
pool it comes from.

## The bug this fixes: every turret prism was invisible

The path pulled a prism from `BlockProjectileFactory` and **never called
`Prism.Initialize`**. That is the documented pool-spawn entry point every other pooled-prism
spawner in the project uses, and it is the only thing that starts `CreateBlockCoroutine` —
the sole writer of `IsCreationComplete = true`. Without it:

- `PrismScaleAnimator.Awake` had already set `localScale` to zero, and
  `BeginGrowthAnimation()` early-returns on `if (prism != null && !prism.IsCreationComplete)`
  — so the prism **stayed at scale zero for its entire life**;
- its child `ProjectileCollider` inherits `lossyScale` 0, so the trigger had zero volume and
  could never register a hit — no damage either, not just no visuals;
- `SetRenderVisible(true)` was never reached, so on the instanced render path there was
  nothing to draw at all.

The loop never threw, so there was no console error. The stance fired a stream of invisible,
intangible nothings, silently. `FireOne` now calls `prism.Initialize(_status.PlayerName)`
after setting the target scale (Initialize reads the authored target off the scale animator),
and the prism blooms, becomes visible, gets a collider, and registers with the ecosystem the
same way every other prism in the game does.

## The flight is on the GPU clock (`Docs/PRISM_ANIMATION.md` §5 C5)

The old flight was a per-frame CPU transform write — exactly what the clock-material law
forbids. It is now one stamp:

- The prism is **spawned at the flight's END POINT**, with everything final there: collider,
  volume, spatial-index registration, MASS-5 shield.
- `PrismRenderService.StampFlight(handle, t₀, duration, worldVelocity)` writes three
  per-instance properties, and `PrismFlightClock` (vertex stage, both live-prism graphs)
  walks the visual in from the muzzle. **The CPU writes nothing to the prism between the
  stamp and the anchor.**
- `RenderBounds` are reset to the mesh and expanded by the object-space muzzle offset, or the
  prism would frustum-cull against its anchor box and pop in halfway down the shot.

The easing is the **bullets'** easing. `Projectile.MoveProjectileAsync` steps by
`cos(t·π/2T)`, so distance travelled is its integral, `v·(2T/π)·sin(t·π/2T)`; the shader
evaluates the same closed form. A turret prism and a bullet released at the same instant stay
abreast for the whole flight and stop at the same range (≈ **286 u** at the shipped
1500 u/s × 0.3 s).

### The prompt's open question, answered

C5 said *"the entity transform goes FINAL at the anchor point immediately (collider/gameplay
at destination — confirm with the prompter if gameplay currently collides mid-flight)."*

**It does, and it must** — piercing means destroying everything along the path. The
resolution is that the thing which collides mid-flight is **not the prism**: it is the
prism's carried `Projectile`, detached at the muzzle and flown by
`Projectile.LaunchProjectile` — literally the bullets' mover. A projectile is gameplay, not
prism animation, so it keeps the ordinary per-frame transform contract, and *that* is what
frees the prism's own transform to be final at the destination from the stamp.

Two death points, one handler. `Projectile.FlightEnded` is raised at both — lifetime expiry
in `MoveProjectileAsync`, and a stopping prism impact in `ProjectileImpactor` — with a bool
saying which. That event **is** "wherever the bullet would be destroyed", made addressable:

- **Timeout** → the prism is already exactly where it was stamped. Just `ClearFlightStamp`.
  Zero transform writes.
- **Stopping impact** → interruption = re-stamp, which the law sanctions: one
  `NotifyPositionChanged()` to move the mass to the impact point (spatial index, shell and
  the render matrix in one call), then `ClearFlightStamp`. The visual does not jump — the
  shader had already drawn it there.

`Projectile.IsCarriedByHost` makes `ReturnToFactory` a no-op for a carried projectile: it
belongs to a pooled *prism*, not the projectile pool, and the null-factory branch would
otherwise `Destroy` the host's child on the first stopping impact.

### The one deliberate wart — judge this in play

Because the prism is spawned at the destination with gameplay state final, its **own** collider
and spatial-index registration go live there the moment the shot is fired — roughly 0.3 s before
the visual arrives. For ~0.3 s there is tangible, ecosystem-visible mass at maximum range that the
player watches the prism still flying toward, and on a stopping impact that mass then relocates to
the impact point.

This is what `PRISM_ANIMATION.md` §1 prescribes ("gameplay state goes final at start") and what C5
asks for by name ("collider/gameplay at destination"), and it is the reason the flight can cost
zero CPU. It was flagged in review as a possible gameplay bug, and that judgement genuinely needs a
human at the controls — a third party flying through the anchor point during the flight would hit
a prism they cannot see there yet.

**If it feels wrong**, the remedy is small and local: keep the prism's `blockCollider` down and
defer the `PrismSpatialIndex` registration until `AnchorPrism`, which is what the pre-clock code
did. That costs a narrow suppression flag on `Prism` (because `CreateBlockCoroutine` owns the
collider enable) and moves the mass accounting off "final at start". Do not solve it by putting the
prism back on a CPU flight.

### Degradation if a flight is cancelled

`Projectile.MoveProjectileAsync` swallows `OperationCanceledException` without running its tail, so
a flight cancelled by destruction never raises `FlightEnded` and its prism keeps a live flight
stamp and an inflated bounds envelope. The visual is still correct — the shader clamps at
`Duration`, so it rests exactly on the anchor — and the next `Prism.Initialize` on that pooled
instance clears the stamp. It is a cull-efficiency loss on a torn-down scene, not a visible defect.

## MASS still owns the stance

- **MASS quantitative** stretches the fired prism's long axis (`blockScale.z ×
  Multiplier(Mass)`), read live per volley. Volume is `x·y·z`, so the stretch feeds
  `Cell.LiveVolume` — *volume is the spine*.
- **MASS level-5 "Shielded Prisms"** is now a **pre-`Initialize` flag**
  (`prismProperties.IsShielded`), so the shield is part of the prism's **birth** and snaps
  (`Docs/PRISM_ANIMATION.md` §4.5) instead of morphing on arrival — one less exotic-visual
  window on the hot path. Regular shield only, never SuperShield: shielded mass is still
  edible by fauna via devastate, which is what keeps the food-web sink intact
  (`Docs/ECOSYSTEM.md` §16).

## Files

| File | Role |
|---|---|
| `R_VesselActions/Data Containers/FullAutoActionSO.cs` | The bullets — and the single authored home of cadence/speed/flight time. `ResolveSpeed` is shared with the turret. |
| `R_VesselActions/Data Containers/FullAutoBlockShootActionSO.cs` | Turret config. Adopts the bullet action; authors only prism shape + pool. |
| `R_VesselActions/Executors/FullAutoBlockShootActionExecutor.cs` | Fire loop, the flight stamp, and the anchor. |
| `Controller/Projectiles/Projectile.cs` | `FlightEnded` (both death points), `IsCarriedByHost`. |
| `Controller/ImpactEffects/Impactors/ProjectileImpactor.cs` | Raises `FlightEnded` on a stopping prism impact. |
| `_Graphics/Materials/Graphs/PrismClockAnimation.hlsl` | `PrismFlightClock` — the vertex-stage flight. |
| `Controller/ECS/Rendering/PrismRenderProperties.cs` | `_FlightStartTime` / `_FlightDuration` / `_FlightVelocity` overrides. |
| `Controller/ECS/Rendering/PrismRenderService.cs` | `StampFlight` / `ClearFlightStamp` + prototype defaults. |
| `Tools/Shaders/wire_prism_flight_clock.py` | Splices the properties + custom function + vertex `Add` into both live-prism graphs. Idempotent; `--check` validates. |
| `_Scripts/Editor/PrismClockGraphWirer.cs`, `PrismClockWiringValidator.cs` | In-editor repair + the gate that fails loud if the wiring regresses. |
| `_SO_Assets/VesselActions/Sparrow/FullAutoBlockShootAction.asset` | Wires `bulletAction` → `FullAutoAction.asset`. |
| `_SO_Assets/.../SparrowPrismProjectileImpactContainer.asset` | Turret prism impact chain — the bullets' own prism effect. |
| `_Prefabs/Trails/Prisms With Pools/Sparrow Projectile Prism.prefab` | The pooled prism. `waitTime` 0.5 → **0**. |

## Tuning knobs

Everything that moves both fire modes lives on **`FullAutoAction.asset`**:

| Knob | Value | Effect |
|---|---|---|
| `firingRate` | **30** | Volleys/s for guns **and** turret. |
| `speedValue.Value` | **1500** | Muzzle speed base for both, before the SPACE multiplier (0.4×–2.5×). |
| `projectileTime` | **0.3** | Flight time; with the easing curve → ~286 u of range at base speed. |

Turret-only, on **`FullAutoBlockShootAction.asset`**: `blockScale` **(0.8, 0.5, 5)** (before
the MASS stretch on z), `rotationOffsetEuler`, `prismType` `Sparrow`.

`waitTime` on `Sparrow Projectile Prism.prefab` is **0** and must stay low: it is the delay
before creation completes, and at 0.5 s the prism was still invisible when its 0.3 s flight
ended.

## Collider / mass budget

| | Before | After |
|---|---|---|
| Volleys/s | 14 | **30** |
| Muzzles | 2 | 2 |
| **Anchored prisms/s** | 28 | **60** |
| Volume/s (base scale, MASS ×1) | ~56 | **~120** |

A held burst lays permanent mass at **~2.1× the previous rate** — ~600 prisms in ten seconds,
each a spatial-index registration plus a collider under the usual collider-LOD. That is what
"the same rate as its bullets" costs; the single lever is `FullAutoAction.firingRate`, and it
moves the guns too.

**Per-frame CPU went down, not up.** The prism costs one stamp and one anchor; the only
per-frame work is the carried projectile's transform, which is exactly what a bullet already
costs. The deleted `MoveAndAnchorAsync` was a per-frame write per live prism.

## In-editor verification

Scene: any Sparrow-playable multiplayer scene (`MinigameWildlifeLiberation` or
`MinigameFreestyleMultiplayer_Gameplay`). Stop with the stationary-mode input (input 6), then
hold fire (input 1).

1. **Something comes out at all.** This was the headline bug — the stance fired invisible
   zero-scale prisms. Prisms must now visibly leave the muzzles.
2. **Cadence parity.** Fire on the move, then stopped. The rate must be indistinguishable —
   both 30 volleys/s from 2 muzzles.
3. **Speed parity + smooth flight.** Prisms leave as fast as bullets and travel ~286 u. The
   flight must be **smooth**, not a snap or a pop-in: that is the GPU stamp working. A prism
   that appears at maximum range with no visible travel means the flight stamp failed — check
   the console for `[PrismClock] flight:` and run **Validate Clock Wiring**.
4. **No pop-in mid-flight.** If prisms vanish and reappear partway down the shot, the
   `RenderBounds` envelope is wrong (frustum culling against the anchor box).
5. **Pierce is SPACE-gated.** Below Space 5, a shot must stop at the first enemy prism it
   hits **and leave its prism right there**. At Space 5+, the same shot must destroy several
   prisms in a line and leave its prism at the far end. Both behaviours, same input.
6. **Anchor.** The prism stays, becomes solid, and behaves as ordinary world mass.
7. **Own-domain pass-through.** Firing through your own anchored prisms must not damage them.
8. **Attribution.** Kills credit you on the scoreboard; `SparrowVesselTelemetry.PrismBlocksShot`
   ticks once per prism fired.
9. **MASS.** Collect Mass crystals — fired prisms visibly lengthen. At Mass 5+ they arrive
   shielded (octahedron) and are still destructible/edible.
10. **Console clean.** No `[FullAutoBlockShoot]` errors, no `[PrismClock]` errors, and
    specifically no "No player found to deal damage to prism!" (that means the carried
    projectile lost its `Initialize`).
11. **Pool reuse.** Fire, exit to menu, re-enter and fire again — prisms drawn from the
    recycled pool must still fly and hit (the old code deactivated the collider child
    permanently and never re-activated it).
12. **MPPM two-client.** Prisms appear in the same places on both; pierce state comes from the
    shooter's own replicated `NetElementUnlocks`.
13. **The deliberate wart (see above).** With a second player, have them fly through the point a
    held burst is anchoring at while shots are still in the air. They will collide with prisms
    whose visuals have not arrived. Decide whether that is acceptable; the remedy is documented.
14. **No hitching under a sustained hold.** The turret prism pool was resized for 60/s
    (defaultCapacity 40, bufferSizeTarget 90, maxAddsPerFrame 8) because anchored prisms are never
    returned, so every shot past the buffer is a fresh `Instantiate`. Watch the profiler during a
    long hold; if it still spikes, raise `bufferSizeTarget`/`maxAddsPerFrame` on the Sparrow's
    `BlockProjectilePoolManager` further.

**Shader wiring gates** (asset-only, no play mode): `python3
Tools/Shaders/wire_prism_flight_clock.py --check` must print OK for both graphs, and
`FrogletTools > Ecology > Prism Animation > Validate Clock Wiring` must show
`_FlightStartTime` / `_FlightDuration` / `_FlightVelocity` and the `PrismFlightClock` node
present on BlockGraph and ExplodingBlockGraph.

## Follow-ups

- **Tunneling.** Bullets and turret shots alike are discrete triggers moved by transform
  writes (`m_CollisionDetection: 0`), so at 1500 u/s they advance ~25 u/frame and can pass
  through a thin prism unregistered. The right fix is a swept segment query on
  `PrismSpatialIndex` (`Docs/SPATIAL_INDEX.md`), not CCD — a transform teleport bypasses CCD.
  Not done here: it would change bullet behaviour too, and parity is the point.
- **Anchored-mass rate.** If 60 prisms/s is too much for a cell's phase ladder in practice,
  move `FullAutoAction.firingRate` — not a turret-only divisor, which would re-open the drift
  this pass closed.
- **The spawn window.** A prism is invisible for the 1–2 frames between the pool pull and
  creation completion (~25–50 u of a 286 u flight). `Docs/PRISM_ANIMATION.md` §4.2 already
  plans to retire that window entirely; it is not turret-specific.
