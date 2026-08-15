# Urchin Trail Rider — riding prismscapes

> The Urchin's other half — the spike cascade the ride carries you into range to fire — lives in
> **`URCHIN_CHAIN_SPIKES.md`**. The ride is the delivery system: it turns someone else's
> prismscape into your road, and the road pays as you travel it.

## Prismscapes have a DIMENSION, and the ride's form follows it

`PrismscapeDimension` (`_Scripts/Data/Enums/`) names the ladder the Prisms/Prismscapes
fundamental always implied — "trails are the 1-dimensional case" is in CLAUDE.md's fundamentals
list; this pins the whole family, values = the dimension:

| Value | Name | What it is | How the Urchin rides it |
|---|---|---|---|
| 0 | **Singleton** | a lone prism | (payoff on contact, nothing to traverse) |
| 1 | **Trail** | a ribbon — the player wake, any `Trail`-backed lay | **SLIDE along it** — `TrailFollower` |
| 2 | **Surface** | a shell — gyroid / Schwarz-P flora, walls | **ROLL across it** — `BlockscapeFollower` |
| 3 | **Volume** | a solid — dense lattices | ridden on its **boundary**, locally a Surface |

`PrismscapeTopology.DimensionOf(prism)` classifies on demand: authored evidence first — a prism
carrying a `Trail` takes the dimension the LAYER declared on the container (`Trail.Dimension`,
default 1D for the vessel wake; `SpawnableGyroid`/`SpawnableSchwarzPSurface` declare `Surface`,
because `Trail` is the general lay container and `PrismTrailBuilder` stamps it on everything it
lays — membership evidence, never shape evidence). Container-less prisms (flora growth) get a
neighbourhood census through `PrismSpatialIndex.QuerySphere` — a shell fills a census ball like
an r² patch, a solid like an r³ ball, so the count separates them. Never physics, never
per-frame.

The Urchin flies normally until it touches a prism. Then it **latches on** and
`GunVesselTransformer` routes the ride by the prismscape's dimension: on a **trail**, steering
becomes the ribbon's geometry and the pilot keeps only a signed throttle (push to slide the way
the nose points along the ribbon, pull to slide back); on a **surface**, the pilot keeps full
steering and rolls across the faces, folding around edges and hopping prism-to-prism. Either
way, every prism visited pays the same rule: your own **grows**, an enemy's is **stolen**, a
destroyed one is **restored**, and riding recharges spike ammo (doubled over shielded prisms).

Attaching is **two flags and no reparenting**:

```
contact  →  VesselAttachPrismEffectSO      sets IVesselStatus.IsAttached + .AttachedPrism
            (+ GunsActive, see below)
              │
              ▼
         GunVesselTransformer.MoveShip     edge-detects the flag
              │   true  when !attached  →  TryBeginRide() routes by topology:
              │       prism.Trail != null →  trailFollower.Attach(prism)   [RideMode.Trail]
              │       else                →  surfaceFollower.Attach(prism) [RideMode.Surface]
              │   false when  attached  →  EndRide() detaches both
              ▼
         Slide()  replaces base.MoveShip() entirely while attached
              │
              ├─ Trail:   TrailFollower.RideTheTrail()      writes Speed + Course;
              │           on a prism boundary calls back FinalBlockSlideEffects()
              │
              └─ Surface: Roll(); Yaw(); Pitch();  ← pilot keeps steering
                          BlockscapeFollower.RideTheTrail() writes Speed;
                          on a prism hop raises OnPrismCrossed
              │
              ▼
         GunVesselTransformer.ApplyPrismscapePayoff(prism)   ← the ONE payoff, both dimensions
```

Nothing about that is Urchin-specific in the plumbing. Any vessel whose impactor container lists
`VesselAttachPrismEffect` attaches; only a vessel whose transformer is a `GunVesselTransformer`
gets the grow/steal payoff, and `TrailFollower` type-tests rather than hard-casting for exactly
that reason — a hard cast turns "this vessel rides but does not convert" into an
`InvalidCastException` on the first prism boundary.

## The platform change: a vessel that is RIDING is not RAMMING

`VesselDamagePrismEffectSO` now declines while `IVesselStatus.IsAttached`
(`skipWhileAttached`, default **on**).

This is the fix for the 2023 bug "urchin destroying first block when attaching to a trail", and
the reason it belongs in the shared effect SO rather than in one container's authoring is
structural:

**Riding a trail and ramming it are the same collision.** There is one contact, dispatched
through one flat `vesselPrismEffects` list with **no ordering guarantee** between the attach
effect and the damage effect. So a vessel that can attach will destroy the very prism it latched
onto — and then `TrailFollower.Attach` is handed a prism that is already gone, or the ride begins
on a corpse. There is no authoring order that fixes it, because the attach flag is set by one
effect and read by another in the same list.

Historically the Urchin only escaped this by **listing no damage effect at all**. That is not a
fix, it is an omission that happens to work, and it leaves the trap armed for the next vessel
that attaches. Guarding it once, in the effect that does the damage, means:

- `UrchinImpactorDataContainer` can list `VesselDamagePrismEffect` like every other vessel, so the
  Urchin rams normally when it is *not* riding.
- The next vessel to grow an attach ability inherits the guard instead of rediscovering the bug.
- The rule is stated where the decision is made ("am I about to damage a prism I am riding?")
  rather than encoded as an absence somewhere else.

Note the fleet-wide caveat this interacts with: **a speed effect is per-vessel wiring, not a
platform given** (see `CLAUDE.md` § Impact Effects). Squirrel / Dolphin / Sparrow / Manta carry a
`VesselChangeSpeedByPrismEffectSO`; Rhino and Serpent do not. `UrchinImpactorDataContainer` does
not list one either, so the Urchin currently takes **no speed penalty from any prism**, danger
prisms included. That is an open item, not a design position.

## Attaching arms the guns

`VesselAttachPrismEffectSO` also sets `vesselStatus.GunsActive = true` (`armGunsOnAttach`,
default on). The Urchin's loop is *ride to reach enemy mass, then convert it*, so an attach that
leaves the guns cold makes the ride a movement option with no payoff. This was lost in the
vessel-layer port and is restored **in the effect** rather than in the transformer, so it lands on
whatever attaches, not just on the Urchin.

## What was broken, and what each break looked like

All three of these compiled, and none of them logged anything.

**1. The transformer held the wrong follower.** `GunVesselTransformer.Initialize` did
`GetComponent<BlockscapeFollower>()`. `BlockscapeFollower` is a *surface-crawl* experiment — it
walks over a prism's faces, projecting movement onto whichever face you touched — and it is a
separate, unfinished thing from `TrailFollower`, which projects **along** the trail from a block
index and supports reversing. They expose the same member names (`Attach` / `Detach` /
`RideTheTrail` / `Throttle` / `AttachedPrism`), so the swap compiled silently. But
`BlockscapeFollower` never calls back into `FinalBlockSlideEffects` — it only writes
`vesselData.Speed` — so the entire grow/steal/restore payoff, which is the ability, never ran.
The Urchin prefab carries **both** components, which is why nothing looked missing.

`TrailFollower.Attach` now returns `bool`, which also makes the two no longer signature-compatible
— re-pointing the field at the wrong one is now a compile error rather than a silent regression.

**2. The throttle was a literal zero.**

```csharp
// TODO - Vessel components should not be accessing InputStatus directly.
// var throttle = (InputStatus.XDiff - zeroPosition) / (1 - zeroPosition);
var throttle = 0;
```

`TrailFollower.RideTheTrail` computes `timeToNextBlock = distanceToNextBlock / (Throttle *
terrainSpeed * SpeedMultiplier)`, so a literal zero is an **infinite** time to the next block:
the advance loop never runs, `distanceToTravel` stays 0, and the vessel attaches and then sits
motionless on the ribbon forever with no error. `ReadThrottle()` initially restored the
commented-out 2023 formula (`throttleZeroPosition` 0.2) — **superseded in round 3**, which found
that XDiff's rest moved to 0.5 on the current input scale and replaced the remap with a signed
throttle (see "The frozen slide" below). The deadband survives: sub-threshold throttle is treated
as stationary explicitly, writing `VesselStatus.Speed = 0` instead of dividing by near-zero.

**3. The ride never fed the smoothed cruise speed.** `RideTheTrail` writes `VesselStatus.Speed`
directly, but `VesselTransformer`'s own smoothed `speed` field — the one free flight integrates —
was untouched throughout the ride. So **detaching snapped the pilot back to whatever cruise they
were carrying when they latched on**, possibly minutes earlier. `Slide()` now ends with
`AdvanceSpeed(VesselStatus.Speed)`.

`AdvanceSpeed`, never `ComputeThrottleTarget`: `AdvanceSpeed` is the one path every transformer's
`MoveShip` runs through, so this stays correct for any subclass that overrides the target.

Two smaller corrections came with them:

- **A refused attach releases the flag.** `Attach` now returns false when the prism has no
  `Trail`, or when `Trail.GetBlockIndex` reports the prism is not a member of the trail it names
  (attaching anyway would ride index −1 and walk off the front of the ribbon). On a refusal the
  transformer clears `IsAttached`/`AttachedPrism`, or the vessel is stuck in ride mode with no
  trail under it and free flight never resumes.
- **The camera pull-in was inverted.** It read `if (AutoPilotEnabled)` — so the close camera was
  applied only for AI, which nobody is watching, and never for the pilot. The ride is
  close-quarters and the trail is the thing to read.

## The frozen slide (round 3): "attached but not able to slide"

Live testing reproduced an attach that stuck: the vessel latched, the camera pulled in, and then
nothing moved at any throttle. Three independent causes stacked, and all three are fixed:

**1. `Trail.IndexSafetyCheck` wrapped the HEAD of a non-loop trail to the TAIL.** Both overstep
branches ran `index %= maxRange` before the loop test — so on the open ribbon every player lays,
stepping past the newest prism handed the rider **index 0, the far tail**, and `Project()`
measured its next segment as the straight-line chord across the WHOLE trail. `finalLerp` then
advanced by `1/chordLength` per frame: the vessel inched along an invisible line at a few
hundredths of its speed, which plays as "attached but frozen". A rider at the head is the
*common* case — you attach near where the trail is being laid. The non-loop branch now
**reflects**: overstep past `count-1` bounces to `count-2` with the incrementor flipped, exactly
mirroring how stepping below 0 already bounced to the start. (Loop trails keep the modulo — there
the wrap IS the topology.)

**2. The throttle was remapped around the wrong rest value.** `XDiff` is the dual-stick SPEED
axis and **rests at 0.5** (`GamepadInputStrategy`: `(right.x − left.x + 2) / 4`), not at the 0.2
the 2023 formula assumed — so a hands-off stick read as 37% throttle *creep* while an actual
stop required holding the sticks apart. `ReadThrottle` is now **signed** around
`throttleRestPosition` (0.5): result in [−1, 1], zero at rest.

**3. Reverse was a separate gesture that fought the ping-pong.** The look-over-your-shoulder
reverse (`reverseLookThreshold`) is retired. The signed throttle IS the direction: push slides
the way the nose points along the ribbon, pull slides backward. Facing is resolved per frame as
`sign(dot(forward, Course))` — `Course` is the ribbon heading while riding (the follower writes
it) and at the attach instant it is still the flight course, which points the way the vessel was
moving, the right seed.

## The 2D roll: BlockscapeFollower is now the surface kernel, not an experiment

`BlockscapeFollower` — previously an unfinished face-crawl experiment sitting unused on the
prefab — is now the finished **2D ride kernel**. Three behaviours compose the roll:

- **Face crawl.** Movement is the vessel's forward projected onto the current face's plane, so
  the pilot keeps full steering (`Slide()` runs the same protected `Roll()`/`Yaw()`/`Pitch()`
  passes free flight runs) and the surface constrains position, not attitude.
- **Edge fold.** Stepping off a face wraps around the box edge onto the adjacent face of the
  same prism.
- **Prism hop.** Before folding, the exit point is checked against neighbouring prisms via
  `PrismSpatialIndex.QuerySphere` (the canonical spatial store — never physics, and physics
  would be blind to fresh prisms anyway); a near-enough neighbour becomes the new floor and
  **`OnPrismCrossed`** fires — the surface analogue of the trail's block crossing, and the event
  `ApplyPrismscapePayoff` subscribes to. This is what makes a gyroid or Schwarz-P shell **one
  continuous floor** instead of one prism with invisible walls at its edges.

The rewrite also fixed the kernel's box math: a prism's mesh is a **unit cube scaled by its
transform**, so `InverseTransformPoint` lands in a space where the block spans ±0.5 — but the old
code compared those coordinates against `localScale/2` (WORLD half extents). On a 4×4×1 block the
edge fold fired **four times too late** and the post-fold snap parked the rider several units off
the surface. All box math now runs in local unit space, with the hover offset converted per axis.

The subscription is detach-first in `Initialize` (`-=` then `+=`) because `Initialize` re-runs on
a live component at a vessel swap or Cellular Duel ownership change, and a stale subscription
would pay the previous pilot; `OnDisable` unsubscribes.

## The rider's domain is read live

`TrailFollower` used to snapshot `domain = vesselData.Domain` in `Start()`. Domains re-pick at
runtime — the menu's domain-changer toy, a modal re-pick, an AI reroll,
`NormalizeUnassignedHumans` — and a snapshot taken at spawn leaves the rider treating its own new
trail as **hostile terrain**: riding it at `HostileTerrainSpeed` and stealing it from itself
instead of growing it. It is now a live property reading `vesselData.Domain` per call, defaulting
to `Domains.Blue` (the neutral sentinel) if the status is not resolved yet.

## Element map

| Element | Ability | Input | Scales | L5 |
|---|---|---|---|---|
| **MASS (2)** | **Trail Rider** | **0 — PASSIVE / contact-driven** | `growthAmount` per prism ridden | **Reinforced Wake** — prisms grown while riding arrive shielded |
| **TIME (4)** | **Slip** | `Button2Action(7)` | ghost duration | **Slipstream** — ride HOSTILE trail at friendly speed |

**Trail Rider is bound to no input event**, and that is deliberate: it triggers on contact. The
consequence worth knowing is fleet-general — `R_VesselActionHandler.CollectBoundActions` resolves
an ability's SO by walking the input bindings, so a passive ability can never be found that way.
Its behaviour lives in the transformer and the effect SO, which the vessel already holds
directly. (The Dolphin's passive Charge seeding records the same lesson from the other side: wire
the config on the executor, do not rely on the binding sweep.)

`growthAmount` is evaluated with `ElementalFloat.EvaluateLive(VesselStatus)`, **not** `.Value`.
`.Value` is the serialized default and only moves through a binding registration this component
never performs, so a MASS level that changed mid-ride (a crystal, a comeback buff) would not be
reflected until the vessel respawned. `EvaluateLive` reads the level at use time, so the very next
prism is grown by the new amount.

**Reinforced Wake compounds with the ammo rule.** A shielded prism pays **double** ammo to ride
over, so a fortified lap of your own trail funds the next one. It gates on
`R_VesselElementalAbilityHandler.IsUpgradeActive(Element.Mass)` — the *replicated* unlock bit —
never on a raw local level read, because it changes the prismscape and a local read desyncs it.

**Slipstream is the biggest single number in the vessel.** `Urchin.prefab` authors
`FriendlyTerrainSpeed` **150** against `HostileTerrainSpeed` **10** — a raid across an enemy
ribbon crawls at a fifteenth of the speed of a lap of your own, exactly when you most want to be
moving. At Time 5 `GetTerrainAwareBlockSpeed` returns the friendly speed for hostile mass too,
while the steal still happens under you. Same replicated gate, same reason: the ride writes
`VesselStatus.Speed`, which every peer's view of this vessel depends on.

## Slip — the detach that means something

One button does both halves because they are one intent. Detaching alone drops the vessel into
free flight **still overlapping the ribbon**, whose next prism re-triggers
`VesselAttachPrismEffectSO` and snaps it straight back on. So the ghost window is not a flourish,
it is what makes the detach mean anything.

`UrchinSlipActionExecutor` clears the two flags, optionally nudges `Course` along the vessel's own
up axis (`detachImpulse`, authored **0**), then disables the colliders it collected from
`IVesselStatus.ShipGeometries` for `ResolveGhostSeconds` before re-enabling them.

**The restore lives in a `finally`.** A cancelled `UniTask` never runs its tail, so without it an
interrupted slip — a vessel swap, a turn end, a disable — leaves the Urchin **permanently
intangible**: a vessel that can fly through the entire prismscape, which reads as a physics bug
rather than as a spent ability. `Initialize` also calls `CancelGhost(restore: true)`
unconditionally, *above* any pilot gate, because `Initialize` re-runs on a live component when a
vessel changes hands (a swap, a Cellular Duel ownership change) and a ghost left running would
restore the previous pilot's colliders onto the new one at an arbitrary moment.

## Files

| Role | File |
|---|---|
| Attach effect (the two flags + guns) | `ImpactEffects/EffectsSO/Vessel Prism Effects/VesselAttachPrismEffectSO.cs` |
| The attach guard (**platform**) | `ImpactEffects/EffectsSO/Vessel Prism Effects/VesselDamagePrismEffectSO.cs` — `skipWhileAttached` |
| Flight model / topology routing / payoff | `Controller/Vessel/GunVesselTransformer.cs` — `MoveShip`, `TryBeginRide`, `Slide`, `ReadThrottle`, `SlideActions`, `ApplyPrismscapePayoff` |
| The 1D ride kernel (slide ALONG) | `Controller/Vessel/TrailFollower.cs` — `Attach` (now `bool`), `SetDirection`, `RideTheTrail`, `GetTerrainAwareBlockSpeed`, live `Domain` |
| The 2D ride kernel (roll ACROSS) | `Controller/Vessel/BlockscapeFollower.cs` — face crawl + edge fold + prism hop, `OnPrismCrossed` |
| The dimension ladder | `Data/Enums/PrismscapeDimension.cs` — Singleton 0 / Trail 1 / Surface 2 / Volume 3 |
| The topology classifier | `Controller/Vessel/PrismscapeTopology.cs` — `DimensionOf`, QuerySphere census |
| The head-reflection fix | `Controller/Vessel/Trail.cs` — `IndexSafetyCheck` non-loop branch |
| Slip config | `R_VesselActions/Data Containers/UrchinSlipActionSO.cs` → `_SO_Assets/VesselActions/Urchin/UrchinSlipAction.asset` |
| Slip executor (hull colliders, ghost window) | `R_VesselActions/Executors/UrchinSlipActionExecutor.cs` |
| Vessel impact container | `_SO_Assets/Effects/Effect Containers/VesselContainers/UrchinImpactorDataContainer.asset` — `[Haptics, Attach, Damage, ElementalDebuffByDanger]` |
| Element map | `Assets/Resources/ElementalAbilityMaps/Urchin.asset` |
| Prefab wiring | `_Prefabs/Spacevessels/Urchin.prefab`: `GunVesselTransformer` + `TrailFollower` (1D) + `BlockscapeFollower` (2D) |

## Tuning knobs

| Knob | Where | Value |
|---|---|---|
| `FriendlyTerrainSpeed` / `HostileTerrainSpeed` / `DestroyedTerrainSpeed` | `Urchin.prefab` — `TrailFollower` AND `BlockscapeFollower` | **150 / 10 / 10** on both. The 15× gap is what Slipstream buys. Both trios are now LIVE — the `TrailFollower` one prices the 1D slide, the `BlockscapeFollower` one the 2D roll. Keep them matched unless the roll should price differently. |
| `growthAmount` | `Urchin.prefab` `GunVesselTransformer` | `ElementalFloat`, element **Mass**, Min 0.6 → Max 1.2, `Value` 1 |
| `rechargeRate` | `Urchin.prefab` `GunVesselTransformer` | 0.1 ammo/s, **×2** on a shielded prism |
| `ammoIndex` | `Urchin.prefab` `GunVesselTransformer` | 0 — the same slot the spike volley spends |
| `throttleDeadband` | `GunVesselTransformer` (C# default **0.1**) | Signed throttle below this magnitude parks the rider. Never let it reach 0: `RideTheTrail` divides by `Throttle × speed`, and XDiff idles NEAR its rest, never exactly on it. |
| `throttleRestPosition` | `GunVesselTransformer` (C# default **0.5**) | The XDiff value that reads as neutral — XDiff RESTS AT 0.5 (`GamepadInputStrategy`). Push above to slide the way the nose points, pull below to slide back. |
| `ghostSecondsAtRestingTime` / `AtFullTime` | `UrchinSlipAction.asset` | 0.6 → 1.6. `GhostSecondsForLevel` is linear in level, anchored at 0 and 10, **extrapolated** across `[-5, 15]`, floored at 0. |
| `detachImpulse` | `UrchinSlipAction.asset` | **0** — off. Raise if a detach should visibly leave the ribbon rather than sliding off it. |
| `armGunsOnAttach` | `VesselAttachPrismEffect.asset` | on |
| `skipWhileAttached` | `VesselDamagePrismEffect.asset` | **on** — the platform guard. Turning it off restores the 2023 bug for every attaching vessel. |

The two `GunVesselTransformer` fields marked "C# default" are **not serialized on
`Urchin.prefab`** — the prefab predates them, so they deserialize to their initializers until it
is next saved. That is correct behaviour (and it is how the round-3 rest-position change reached
the prefab without a YAML edit), but it means the inspector will show them only after a re-save.

## Collider budget

**Zero colliders.** The ride adds no colliders: attaching is two flags, the 1D slide is one
`Trail.LookAhead` + `Trail.Project` per frame against the trail's own block list, and the payoff
is direct calls on the prism the rider is standing on. Slip **removes** colliders for the
duration of the ghost (the hull's, temporarily) and adds none.

The 2D roll adds **bounded, event-driven spatial-index reads, never physics**: one
`PrismSpatialIndex.QuerySphere` census at a non-trail attach (`PrismscapeTopology.DimensionOf`)
and one per **edge crossing** while rolling (the hop probe). Both are bucket-grid lookups over a
few blocks' radius against an already-maintained index, allocation-free via shared scratch lists,
and neither runs on a frame where the rider stays on its face.

The one budget-adjacent effect is indirect and belongs to the ecology rather than to physics:
`FinalBlockSlideEffects` calls `Prism.Restore()` on a destroyed prism and `Prism.Grow()` on a
friendly one, so a long lap re-arms colliders that were down and adds volume to a cell's live
total. Mass is conserved by both — nothing is created from nothing and nothing is removed.

## In-editor verification

Nothing below can be checked without play mode.

1. **Project compiles with zero errors.**
2. **Confirm the vessel wiring imported.** `Urchin.prefab` now carries an
   `ActionExecutorRegistry` on `R_VesselActionHandler._executors` and binds `Button2Action(7)` →
   `UrchinSlipAction.asset`. Trail Rider is correctly **unbound** — it is passive. The prefab was
   edited as YAML outside the editor, so check for missing scripts and unassigned references.
3. **`VesselCustomization._shipGeometries` now has 13 entries** — confirm those objects actually
   carry `Collider`s, because `UrchinSlipActionExecutor` collects only the ones that do. With none,
   it warns `found no ShipGeometries` and the slip detaches without phasing out, meaning the vessel
   re-latches on the next prism: exactly the failure the ghost exists to prevent.
4. **The ammo resource is authored** (round 2): `ResourceSystem.Resources[0]` on `Urchin.prefab`
   is the spike/ride meter (gain 0.05, max 1, initial 1). `SlideActions` additionally
   bounds-guards `ammoIndex` before `ChangeResourceAmount` (which indexes uncheckedly), so a
   future prefab with the meter removed degrades to "no recharge" rather than a per-frame throw.
5. **`vesselImpactorDataContainerSO` now points at `UrchinImpactorDataContainer`** — confirm that
   is the container the `VesselImpactor` on the hull actually reads.
6. **Attach.** Menu_Main freestyle or any Urchin-playable mode. Fly into your own trail: the
   vessel should snap onto the ribbon, the camera should pull in, and the stick should stop
   steering — you follow the trail's curve. **The prism you touched must still be there**; a prism
   that explodes on contact means `skipWhileAttached` is not taking effect.
7. **Move.** Push the speed axis: the vessel slides the way the nose points along the ribbon.
   Hands off, it holds still (XDiff rests at 0.5 = zero signed throttle). If it attaches and
   never moves at any input, first suspect the trail HEAD: ride toward the newest prism — a
   vessel that freezes exactly there means the `IndexSafetyCheck` reflection regressed to the
   wrap.
8. **Reverse.** While riding, pull the speed axis below rest: the vessel slides back the way it
   came. Push again: forward. No look gesture is involved.
8b. **Roll a surface.** Fly into a gyroid or Schwarz-P flora prism (or any non-trail shell): the
   vessel should latch and the console should log `Riding a Surface prismscape`. Steering stays
   live — you can yaw/pitch/roll while crawling the face. Crossing a box edge folds around it;
   crossing to an adjacent block hops onto it and pays the payoff (watch the domain convert /
   grow prism-by-prism as you roll). Rolling off the shell's rim with no neighbour folds around
   the edge rather than flying free — detach is Slip's job, same as on a trail.
9. **The payoff runs.** Riding **your own** trail, the prisms you cross should visibly **grow**.
   Riding an **enemy's**, they should change to your domain as you pass. If neither happens but
   you are moving, the transformer resolved `BlockscapeFollower` instead of `TrailFollower`.
10. **Destroyed prisms restore.** Blow a hole in a trail, then ride across it — the destroyed
    prisms should come back as you pass.
11. **Ammo.** Watch the ammo meter climb while riding, and climb visibly faster over shielded
    prisms.
12. **Hostile trail is a slog.** Ride an enemy ribbon at Time 0: it should crawl (10 vs 150). Take
    Time to 5 and repeat: full speed, still stealing.
13. **Reinforced Wake.** Take Mass to 5 and ride your own trail: grown prisms should come up
    **shielded** (octahedron shells), and riding back over them should pay double ammo.
14. **Slip.** While riding, press the slip button: you let go, and for ~0.6 s (Time 0) you can fly
    out through the trail without re-latching. Fly back in afterwards — you must re-latch normally,
    i.e. the colliders came back.
15. **Interrupted slip.** Slip and immediately swap vessels / end the turn / trigger a respawn.
    The hull colliders must be **solid** on the other side. A vessel that can now fly through
    everything means the `finally` restore did not run.
16. **Detach speed.** Ride at full pelt on your own trail, then slip. The vessel must carry a
    sensible speed out of the ride rather than snapping back to the cruise it had when it latched
    on (the `AdvanceSpeed` feedback).
17. **Refused attach.** Touch a prism with no trail (an environment/flora prism, a fauna body
    prism). The vessel must keep flying normally — a vessel that freezes in place has kept
    `IsAttached` set on a refusal.
18. **The rest of the fleet still rams.** Every other vessel's prism collisions must be unchanged
    (they never set `IsAttached`, so `skipWhileAttached` is a no-op for them). Spot-check a
    Squirrel and a Rhino destroying prisms by contact.
19. **A domain change mid-ride.** Ride your own trail, then change domain at the domain-changer
    toy in Menu_Main freestyle and ride the *same* trail. It must now read as **hostile** (slow,
    and it steals). If it still grows, the live `Domain` property has been snapshotted again.

### MPPM — two clients

20. **Both peers see the ride.** Client A attaches and rides; client B must see the vessel
    following the ribbon at the same speed and the same prisms converting. Speed and Course are
    written locally and replicated through the owner-authoritative transform, so a mismatch means
    a terrain-speed branch is resolving differently per peer.
21. **Reinforced Wake replicates.** Take the *client's* Urchin to Mass 5 and ride its own trail —
    the **host** must see the grown prisms arrive shielded. A host that sees unshielded prisms
    means the gate is reading a local level instead of `IsUpgradeActive`.
22. **Slipstream replicates.** Client's Urchin at Time 5 rides a hostile ribbon; the host must see
    it move at the fast speed, not the slow one.
23. **A client's steals score.** Ride ~20 enemy prisms on the client and check the client's own
    `PrismStolen` / `VolumeStolen`. This is the same `Player.ReportPrismStolen_ServerRpc` path the
    spikes use, and before it a client's steals scored nothing at all — for every steal source in
    the game, not just the Urchin.

## Follow-ups

- **Netcode components are not wired** — no `NetcodeHooks`, `NetworkVesselClientCache`,
  `NetworkVesselImpactor` or `ClientNetworkTransform` on `Urchin.prefab`, so the MPPM steps above
  are blocked on a separate multiplayer-spawn pass rather than failing.
- **The roll does not orient the hull to the surface.** `BlockscapeFollower` constrains position
  only; the vessel's up axis is whatever the pilot steers, so rolling the underside of a shell
  can read as flying upside-down along a floor. A gentle DOTween-style alignment of the hull's up
  toward the face normal (photons only — never fight the steering inputs) would sell the roll.
- **`PrismscapeTopology`'s census thresholds are heuristics** (`< 3` neighbours = Singleton,
  `> 55` = Volume, between = Surface, census radius 3× the prism's largest extent). They were
  sized from geometry (an r² patch ≈ 28 same-size blocks, an r³ ball ≈ 113), not measured against
  real gyroid/Schwarz lays. If a flora shell ever classifies as Volume, tune the constants there —
  do not fork a per-caller classifier.
- **`percentTowardNextBlock` is not computed on attach** and `direction` always starts `Forward`
  (both carry TODOs in `TrailFollower.Attach`). So latching mid-prism snaps to that prism's start,
  and latching while flying backwards along a ribbon starts you going the wrong way until you use
  the reverse gesture. Resolving the initial direction from a dot product against `Course` is the
  obvious fix.
- **The Urchin lists no `VesselChangeSpeedByPrismEffectSO`**, so it takes no speed penalty from
  any prism, danger prisms included. It joins Rhino and Serpent as the vessels missing the fleet's
  shared collision read (`speedModifierDuration 1`, `massScaling 0.1`, `maxSlowStrength 0.5`,
  `dangerSlowMultiplier 3`, `dangerSlowDurationMultiplier 3`). Adding it must be checked against
  the ride: a slow applied while attached is meaningless, since `Slide` owns the speed.
- **Edit-mode coverage is narrow.** `_Scripts/Tests/Editor/UrchinChainReactionTests.cs` pins
  `UrchinSlipActionSO.GhostSecondsForLevel`'s non-negativity (a negative window would skip the
  intangibility and re-latch the vessel onto the trail it just left — the ability doing the
  opposite of its purpose). Nothing covers the attach guard, the ride kernel or
  `FinalBlockSlideEffects`.
- **No audio.** The attach, the ride loop, the steal-as-you-pass and the slip each want their own
  inspector-exposed `EventReference` on the component that makes the noise, shipped **empty**.
  The ride in particular is a continuous sound and wants a `StudioEventEmitter` or a small
  controller in the shape of `DriftAudioController`.
- **The ghost is collider-only.** The hull does not visually phase, so a slip currently reads as
  "nothing happened" until you fly through a prism. A dissolve on the hull material would make the
  ability legible; continuity of existence applies (fade, do not blink).
- **No AI path.** `AIPilot` has no notion of attaching, so an AI Urchin never rides. It will
  attach on incidental contact and then sit on the ribbon at zero throttle, which is worth
  checking before shipping AI-backfilled Urchin matches.
