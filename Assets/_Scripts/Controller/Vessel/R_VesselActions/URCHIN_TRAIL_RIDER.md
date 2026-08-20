# Urchin Trail Rider — riding prismscapes

> The Urchin's other half — the spike cascade the ride carries you into range to fire — lives in
> **`URCHIN_CHAIN_SPIKES.md`**, and the ability that supplies a road where the map has none in
> **`URCHIN_TRACK_PROJECTOR.md`**. The ride is the delivery system: it turns someone else's
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
`GunVesselTransformer` routes the ride by the prismscape's dimension — and the two rides run
**different logic on purpose**, because each dimension has a different relationship with the
z-axis of the prisms that compose it (trail prisms: z PARALLEL to the trail; surface prisms: z
ORTHOGONAL to the surface):

- On a **trail** the ride is a **rail grind**: the hull sits ON the ribbon and its **attitude
  is entirely the pilot's** — roll, yaw and pitch all run exactly as in free flight (rolling
  spins you around the rail for free, because while riding your forward IS the rail). Only the
  throttle is re-purposed: signed speed up and down the ribbon, forward/reverse resolved from
  the pilot's facing against the trail axis — the dot-product scheme the original used.
- On a **surface** the ride is **marble madness**: momentum-carrying rolling on a smoothed
  continuous plane over the prisms' authored normals, belly eased onto the surface, and running
  off a sheet's **edge wraps the rider around the rim onto the other side**.

Either way, every prism visited pays the same rule: your own **grows**, an enemy's is
**stolen**, a destroyed one is **restored**, and riding recharges spike ammo (doubled over
shielded prisms).

Attaching is **two flags and no reparenting**:

```
contact  →  VesselAttachPrismEffectSO      sets IVesselStatus.IsAttached + .AttachedPrism
            (+ GunsActive, see below)
              │
              ▼
         GunVesselTransformer.MoveShip     edge-detects the flag
              │   true  when !attached  →  TryBeginRide() routes by topology:
              │       DimensionOf == Trail →  trailFollower.Attach(prism)   [RideMode.Trail]
              │       else                 →  surfaceFollower.Attach(prism) [RideMode.Surface]
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
reverse (`reverseLookThreshold`) is retired. The signed throttle IS the direction. (Round 3
first derived "which way is push?" per frame from `sign(dot(forward, Course))` — superseded in
round 4 below, where it was the largest jitter source.)

## The unsmooth ride (round 4): the slide must feel 1-DIMENSIONAL

Live testing of round 3: "horribly unsmooth... horribly jittery." Three mechanical faults
stacked, and the 2D roll's whole model was wrong. All fixed:

**1. Direction was recomputed per frame from a facing dot — and nothing ever rotated the
vessel.** `Slide()` replaces `base.MoveShip()` entirely, and `RotateShip` (the only writer of
`transform.rotation` in flight) lives inside `MoveShip` — so during a ride the hull kept its
attach-instant attitude forever. Meanwhile round 3 derived direction per frame as
`sign(dot(transform.forward, Course))`: a frozen `forward` against a `Course` that follows the
curving ribbon crosses 90° and the sign FLAPS — and every flap ran `SetDirection`, which shifts
the block index ±1. A teleport per flap, potentially every frame. This is the AI break-off
lesson again, in a new costume: **a directional decision must be LATCHED, never recomputed per
frame from live geometry.** Now (round 5 superseded round 4's mechanism, and this paragraph
was left describing the retired one): `GunVesselTransformer` keeps a latched `_facingSign`, flipped only when `dot(transform.forward, RibbonAxis())` crosses `facingFlipThreshold` (0.35) the OTHER way — true hysteresis, so a bend sweeping the axis under a steady nose cannot flap it — and the signed throttle maps onto that sign before `TrailFollower.SetDirection` is told anything. `TrailFollower.Attach` seeds the
initial direction from the arrival Course; nothing named `attachDirection` or `SetRideSign`
ships — those were round 4's names.

**2. Attach snapped the rider to the block's start.** `percentTowardNextBlock = 0` (a 2023
TODO) — a visible backwards jump at every latch. Now seeded by projecting the vessel's actual
touch position onto the segment ahead.

**3. The head of the trail was unrideable.** Round 3's reflection fix stopped the freeze but
introduced a bounce: `Project` reflects at an open ribbon's end, the follower adopted the
flipped direction, the transformer's throttle mapping flipped it back next frame — an
oscillation exactly where you attach (the head is where the trail is being laid). Now the
follower **PARKS**: on a bounce it discards the frame's move entirely (no snap — the rider
stops within one frame-step of the end, bookkeeping untouched) and holds direction, so the ride
continues the instant the trail grows a new head block or the pilot pulls the other way.

**4. Ride attitude is now applied the way free flight applies it.** The ride writes
`accumulatedRotation` and applies it with the same
`Quaternion.Slerp(transform.rotation, accumulatedRotation, LERP_AMOUNT · dt)` as `RotateShip`.
Ride boundaries sync `accumulatedRotation = transform.rotation` in both directions, so no input
backlog fires as an uncommanded turn at attach or detach.

## Trail membership was never stamped (round 6): the wake's blocks belonged to NOTHING

Round-6 playtest asked the right question: "check whether vessels that leave two trails are
properly leaving two separate trails — it feels like it might incorrectly be 2 trails in 1."
The twin ribbons WERE always two separate `Trail` objects (`VesselPrismController.Trail` +
`Trail2`, split when `Gap != 0`) — but **no wake prism was ever a member of either**:
`CreateBlock` called `trail.Add(prism)` yet nothing set `prism.Trail`, and the only stamper in
the codebase was the spawnable builder. Worse, **pool reuse preserved the stale reference**: a
prism that once served a spawnable lay kept that dead container into its wake life. So a wake
block either had NO container (fresh instance — the attach effect's null-Trail gate refused it
with an error) or a STALE one (the gate passed against the WRONG ribbon, `GetBlockIndex`
returned −1, and the ride followed garbage). Every earlier round's 1D misbehaviour had this
under it, and the census misreading the un-containered twin-ribbon blob as one *Surface* is
exactly the "2 trails in 1" feel.

Three changes close it, and they are a set:

1. **`Prism.ResetState` clears trail membership** (`Trail` + the `prismProperties.Trail`
   mirror) — membership never survives into a new pooled life.
2. **`Prism.AssignTrail(trail)`** is the ONE stamping API, and its contract is *call it AFTER
   `Initialize`* (the reset would wipe an earlier stamp). Both layers comply:
   `PrismTrailBuilder.ConfigureLaid` (moved after `Initialize`) and
   `VesselPrismController.CreateBlock` (now stamps at all).
3. **`VesselAttachPrismEffectSO` dropped its null-Trail refusal** — a container-less prism is
   still a prismscape (flora shell → Surface, lone block → Singleton) and the ride ROUTING
   decides how it is ridden; the old gate silently made every container-less prism in the game
   unattachable while logging an error for an ordinary contact.

The same round retuned the wake's GEOMETRY so the two ribbons read as two:
`BaseScale (10,5,5) → (10,2.5,4)` and `Gap 1 → 6` — block width is `BaseScale.x/2 − Gap/2`, so
each ribbon is now a 2-wide, 2.5-tall, 4-long lane with a **6-unit clear gap** (was 4.5-wide
slabs a sliver apart, visually one 10-wide slab). And the ride camera came in to **half
distance** (`UrchinCameraSettingsSO`: followOffset z −40 → −20, dynamic band 30/50 → 15/25).

## The per-prism jerk (round 7): Project lerped along a CHORD on every crossing frame

With membership fixed (round 6), the grind finally ran — and exposed a jerk that had been
waiting under everything: *"a halting discontinuity where it displays a jump to another prism
and back again very quickly... at a periodicity that matches the prisms along the trail."* That
description IS the diagnosis. `Trail.Project`'s walk loop advanced `startIndex` and `nextBlock`
but **never re-read `currentBlock`** — it stayed pinned at the frame's original block. On any
frame whose step crossed a block boundary, the segment length was measured from the ORIGINAL
block to the NEW next block (a two-segment chord), and the final position LERPED ALONG THAT
CHORD, cutting the corner at a parameter computed against the wrong length. The next frame
re-derived cleanly from `(endIndex, finalLerp)` and snapped back onto the true segment: one bad
frame per crossing, at exactly the trail's block periodicity. Fixed with one line
(`currentBlock = TrailList[startIndex]` inside the loop).

The same pass upgraded the ride's geometry from polyline to **Catmull-Rom arc** through the
block centres: a straight lerp is positionally continuous but kinks DIRECTION at every block
centre, which at speed reads as a tick-tick-tick — the opposite of the rollerblade rail-slide
feel the 1D ride is for. `Project` now returns position and heading evaluated on the spline
(outer control points wrap on loops and clamp at an open ribbon's ends, where the arc correctly
degrades to the segment); the `(endIndex, finalLerp)` bookkeeping stays segment-linear, so
nothing upstream changed. Wake prisms' z genuinely points down the trail
(`blockRotation = transform.rotation` at lay time), so the authored-z invariant the dimension
ladder rests on holds for every wake ribbon.

## The hull's domain colour was on the wrong submesh (round 18)

Round 17 fixed the Urchin reading as see-through by swapping each renderer's two materials, so
the opaque one sat in slot 0. That worked, but it treated the symptom. The platform contract is
explicit (`ScarabHullBuilder`): **a MeshRenderer hull is painted on slot 1** — submesh 0 is the
shared body material, submesh 1 is the part that wears the domain colour. The Urchin's FBX
authors its submeshes the OTHER WAY ROUND, so the domain colour was landing on trim while the
hull kept a fixed material — and swapping the slots only moved which material was the fixed one.

`VesselCustomization._domainMaterialSlot` (default **1**, the contract) lets a vessel say which
slot its art wears the domain on, and `VesselHelper.ApplyShipMaterial` takes it as a parameter
(default 1, so no other vessel changes). The Urchin declares **0**, and its authored material
order is restored to what the art shipped with. The result is both fixes at once: the domain
colour lands on the hull, and the material left showing is the opaque accent rather than the
transparent base — nothing is see-through and nothing is mis-coloured.

The slot is clamped to the renderer's material count, because a single-submesh renderer is legal
art and painting its only slot is the sane reading of "wear the domain".

### …and that fix was a NO-OP, because an index is the wrong handle (round 19)

Round 18 did two things in one commit — restored the authored material order (`BlueBase` back to
slot 0) **and** moved the index to 0 — and those two changes cancel exactly. The domain colour
landed on the same submesh it had landed on before, so from the cockpit "changing domain did not
swap the correct material": nothing had changed. It is worth stating the general lesson, because
it is not specific to this vessel:

> **Never move an ARRAY ORDER and the INDEX that reads it in the same change.** One of the two is
> the fix; doing both is a rename. If you catch yourself editing both, you have not decided which
> one is wrong.

The durable repair is to stop indexing at all. `VesselCustomization._domainReplacesMaterial`
(optional; empty keeps the slot-index path for the rest of the fleet) names the **authored
material the domain colour replaces**, and `ResolveDomainSlots` finds every slot wearing it, per
renderer, whatever index it sits at. That is what an index could never be right about here: the
Urchin's `ShroudLeft` shipped with its two materials in the *opposite* order to its twelve
siblings, so no single index paints all thirteen correctly — and the `Body` renderer carries a
third material (`Screen`) besides. The Urchin declares `BlueBaseVesselMaterial`, which is also
the transparent one, so identity painting removes the see-through hull and the mis-coloured hull
with one statement.

Two implementation details are load-bearing:

- The slot map is resolved **once and cached**. After the first paint the slot no longer holds
  the replaced material (it holds the domain material), so re-resolving on a later domain change
  would find nothing and the vessel would silently stop responding to its domain.
- `ResolveDomainSlots` reads `sharedMaterials`, not `materials`. `materials` instantiates a clone
  per slot, and the identity comparison against the asset would then never match.

A geometry wearing none of the named material warns once by name — an authoring mistake that is
otherwise invisible, because a part that simply never recolours looks like a part that is meant
to be a fixed colour.

### Round 21 SUPERSEDES round 20: `GreenAccentVesselMaterial` is the DOMAIN PLACEHOLDER, not a bug

Round 20's measurement was right and its conclusion was wrong. The measurement: `GreenAccent`'s
colours are exactly Jade's ship colours ×2. The wrong conclusion: "hardcoded jade, welded in —
replace it AND the base." Painting both materials made the vessel **uniformly one material**,
which erased the two-tone read entirely.

The authority is the **Squirrel's FBX `.meta`**, whose `externalObjects` names the three material
roles the fleet actually uses:

| FBX material name | Asset | Role |
|---|---|---|
| `Body` | `BlueBaseVesselMaterial` | the dark glassy hull — **never changes** |
| **`Domain`** | `GreenAccentVesselMaterial` | **the accent the runtime REPLACES with the domain ship material** — authored jade only because jade is the menu default |
| `Window` | `ScreenVesselMaterial` | cockpit screen — never changes |

Measured proportions on the Squirrel (the reference the design points at): **Domain 26% / Body
64% / Window 2% / Engine 6%** of polygons. The domain colour is an *accent* on a dark body, with
the window and engines fixed — "swapping just the accent colors, and leaving the screen, base,
and jets their own materials." The Urchin's authored mapping (Body on its ~75% majority submesh,
Domain on its ~25% accent submesh, Window on the Body part's third submesh) matches this model
**exactly** — the original authoring was correct all along, and the one true defect was
`ShroudLeft`'s reversed slot pair, long since normalized.

So the final state: authored order everywhere (`BlueBase` slot 0, `GreenAccent` slot 1, `Screen`
on Body's slot 2), and `_domainReplacesMaterials` names **only `GreenAccentVesselMaterial`**.
On a Ruby pilot: every accent goes ruby, the body stays dark navy glass, the screen stays a
screen — no jade anywhere, and not uniform either.

Model facts established by parsing `Urchan_Test.fbx` directly (binary FBX 7400), for the next
person who suspects "model errors": all 14 geometries declare their materials in the same order
(`Material.004` = Body ~70–80% of polys, `Material.009` = Domain accent, `Material.002` = Window,
Body only); the mirrored halves match poly-for-poly; each prefab renderer wires its own distinct
mesh; **the left gun is clean** — its only anomaly was the prefab-side ShroudLeft slot reversal.
One deliberate judgment call: the Window submesh is only 33 polygons but forms the vessel's
whole FRONT DOME (a third of the silhouette — the pale disc with the glowing rings). That is the
authored art, plausibly the Urchin's "eye", and it is left as authored; if it should read as dark
hull instead, the change is ONE slot (Body renderer slot 2 → `BlueBaseVesselMaterial`).

### ~~The jade that survived a Ruby swap: `GreenAccentVesselMaterial` IS Jade~~ (round 20 — conclusion superseded above; measurements below remain valid)

Naming one material was still not enough, because **both** of the Urchin's authored materials are
domain-bearing and neither is a legitimate neutral. Measured, not inferred:

| | `_Color1` (base face) | `_Color2` (fresnel rim — the part that glows) |
|---|---|---|
| `GreenAccentVesselMaterial` | (0, 0.0941, 0.1882) | (0, **0.7765**, **1.4980**) |
| `OriginalColorSetSO.JadeColors.ShipColor1/2` | (0, 0.0490, 0.0941) | (0, **0.3882**, **0.7490**) |
| ratio | ×1.92, ×2.00 | **×2.00, ×2.00, exact to 7 dp** |

`GreenAccentVesselMaterial` is the **Jade ship colour at 2× intensity, welded into the prefab** —
`ThemeManager.GenerateDomainMaterialSet` drives the live ship material through the very same two
properties (`ShipMaterial.SetColor("_Color1"/"_Color2", colorSet.ShipColor1/2)`). So every slot
wearing it stays jade on every domain, which is exactly what a Ruby pilot saw. And
`BlueBaseVesselMaterial`'s rim is **pure black** `(0, 0, 0)` — a base with no rim, which is why the
parts wearing it read as see-through in round 17. Neither material is a colour the Urchin should
keep, so `_domainReplacesMaterials` is a **list** and the Urchin declares both. Every slot on all
thirteen renderers takes the domain colour; only `Body`'s third material (`ScreenVesselMaterial`,
a neutral grey/blue cockpit screen shared with the Rhino and Dolphin) is left alone.

The general rule this earns:

> **Before deciding which slot "wears the domain", check whether the material you are leaving in
> place is secretly one domain's colour.** A hardcoded palette entry and a neutral base look
> identical in the inspector and are opposites at runtime. The test is one grep: compare the
> material's authored colours against `SO_ColorSet`'s per-domain entries.

**The Urchin FBX is clean — the left gun's "model error" is not a model error.** Parsed directly
from `Urchan_Test.fbx` (binary FBX 7400): 14 `Geometry` records, and **every one declares its
materials in the same order**, `[Material.004, Material.009]` (`Body` adds `Material.002`), with
`LayerElementMaterial` mapped `ByPolygon`. The mirrored halves are exact — `SideShooters`/`.003`
both 276 polys, `SideShooterHold`/`.003` both 400, `UpperJet`/`.003` and `LowerJet`/`.003` all 370,
`UpperJetHold`/`.003` both 324, `LowerJetHold`/`.003` both 281 — and each of the 13 prefab
renderers points at its own distinct mesh in that FBX, none shared. `Material.004` is the majority
submesh on every part (~70–80% of polygons), which is why the *base* material dominates the
silhouette. The only oddity is `ShootPoints` (`Sphere.041`), a **zero-polygon** mesh — the empty
holder for the 18 historical firing-port objects, not wired into the prefab at all and harmless.
The material-order anomaly that did exist was pure prefab authoring — `ShroudLeft` had its two
materials reversed relative to its twelve siblings — and it is gone. Painting every slot makes the
order unable to matter again.

## Each ribbon is its own trail; shielded and skewed prisms ride their envelope (round 17)

**A gapped wake is now TWO SEPARATE SINGLE TRAILS.** You ride the ribbon you touched, on its
own prisms. The spine ride (rounds 12-13: undo each block's stamped lay offset and ride the
corridor between the twins) is retired by design call — it read well, but a trail is a trail,
and consistency across every vessel's wake is worth more than the corridor. `RidePoint` is now
simply the block's own centre, and `Prism.TrailLayOffset` and its spawner stamp are deleted
with it (dead state on every prism otherwise). A consequence, accepted deliberately: a wake laid
by a ROLLING vessel is ridden as the helix it actually is — which is honest, because the ride
follows the mass the pilot laid.

**Shielded mass is ridden as the cuboid its shell is nested in.** Both shield meshes are the
box's *circumscribing dual* — vertices at `OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE` (3×)
the box's half-extents — so the ride scales the half-extents by that constant, read from the
generator rather than guessed. A super-shielded prism's spikes are inside the same envelope, so
one factor covers both tiers.

**A prism at a strange angle is ridden as its silhouette envelope, measured in the TRAIL's
frame.** The surface distance is now the box's SUPPORT along the radial —
`Σ halfᵢ · |axisᵢ · radial|` over the prism's three axes — instead of a cross-section through
its own x/y. A drift block yawed off the ribbon therefore reads as effectively WIDER, exactly
as its sweep along the trail makes it, and the rider goes around it instead of clipping a
corner.

Support rather than a literal trail-aligned AABB, and the reason is worth keeping: an AABB
*about an axis* is not well defined without arbitrarily choosing which way is "up" around the
trail, and the answer changes with that choice. The support needs no such choice, is continuous
as the pilot rolls, and reduces to exactly the box half-extent for a prism square to the ribbon
— so the Sparrow's single-ribbon ride is unchanged.

## Ride the prism's SURFACE, and roll walks you around it (round 16)

Riding a **Sparrow** trail — a SINGLE ribbon, laid with no gap because that vessel flies with
one thumb — put the Urchin *inside* every prism it passed through. A single-ribbon wake has no
lay offset, so its block centres sit exactly on the ride line, and riding the line bare is
riding through the mass.

The ride now sits on the prism's **surface**, and the two halves of that are each derived
rather than authored:

- **Which way round** is the pilot's **roll**, at no cost in state: the radial is the hull's
  own UP, flattened across the trail. Roll the ship and its up sweeps around its forward —
  which while riding IS the trail axis — so the ship walks around the prism's z axis with its
  belly always toward the rail. Attitude stays entirely the pilot's (the round-11 rule) and
  POSITION follows from it, so this can never fight the stick the way the round-5 orbit did.
- **How far** is the exact box cross-section, not a constant. A prism is a scaled cube, so the
  distance from its z axis out to its face along a direction whose prism-x/prism-y components
  are `(u, v)` is `min(halfX/u, halfY/v)`. A wide flat trail is therefore ridden close on its
  broad faces and further out at its edges — which is what makes it read as a surface instead
  of a radius. `rideSurfaceClearance` adds only the hull's own half-thickness on top.

Scales come from `TargetScale` (authored), never the live scale, so a block still blooming in —
or one the payoff is GROWING under the rider — cannot drag the ship around as it changes.

On a GAPPED wake the ride line is the flight spine, which lies in the corridor between the twin
ribbons rather than through any prism, so the same offset is a small clearance within that
corridor and the twin ride is unchanged.

The Urchin's ride camera also came in to **a third** of its distance
(`UrchinCameraSettingsSO`: followOffset z −6.67, dynamic band 5 / 8.33).

## Rings loop, 0D is not rideable, 2D stops fighting your aim (round 15)

Playtest: *"best yet — I could ride both my own and the Squirrel's trail great"*, with three
follow-ups. All three turned out to be one root cause plus two rules.

**Rings were being ridden as Singletons — and six other lay paths were silently broken too.**
Round 13's pool-reuse clear in `ResetState` established that trail membership is stamped AFTER
`Initialize`. Six of the nine lay sites in the project stamped it BEFORE — `BoostRingBuilder`
(the Squirrel's crystal ring), `SpawnableFlower`, `SpawnableCord`, `SpawnableDartBoard`,
`SpawnableRaceTrack` and `SpawnableWaypointTrack` — so every prism they laid came out
container-less, classified by census as a 0D Singleton, and routed to the MARBLE. That is the
"strange behavior" on a ring, and it was a real regression well beyond the Urchin: the HexRace
waypoint track and race track lost their `Trail` too, which `Skimmer` and
`SkimmerAlignPrismEffectSO` both read for trail alignment. All six now call
`Prism.AssignTrail` after `Initialize`.

**A ring is a LOOP.** `SpawnableRings` and `SpawnableDartBoard` now build `new Trail(isLoop:
true)`, so the walks wrap by modulo instead of reflecting at a phantom end and a rider circles
indefinitely in either direction. (Ray-shaped AOEs — `AOERadialBlocks`,
`AOEDangerHemisphereBlocks` — stay open, correctly: a spoke has two ends.)

**0D is not rideable.** `TryBeginRide` refuses `Singleton` outright, so the vessel flies on and
the prism reads as ordinary mass. A lone prism has no extent to travel along, and — as the ring
showed — allowing it is how a mis-stamped ribbon gets ridden as a surface. Genuine singletons
are rare by construction; almost every prism belongs to a 1D or 2D lay.

**2D no longer fights the pilot's aim.** The belly-onto-normal ease is removed. It was a
per-frame torque the pilot had to hold against, restricting pitch and roll to the plane and
fighting the camera — you could not look and shoot where you pleased. **The surface constrains
POSITION, never attitude**, exactly the rule the 1D grind arrived at in round 11 and for the
same reason. Motion still follows the plane, because the crawl direction is the steered forward
PROJECTED onto it: aim freely and you travel by the component that lies along the surface.

## The wake outlives its vessel (round 14): the REAL Squirrel-trail bug all along

Round 13's stamp made the Urchin's own trail ride well — and the Squirrel trail was STILL
chaos, even on dead-straight sections. That ruled out geometry entirely: the 1D follower on a
straight ribbon cannot wander laterally no matter how wrong its offsets are. The chaos had the
MARBLE's signature — and it was the marble.

**`VesselPrismController.OnDisable()` called `ClearTrails()`.** The moment a vessel despawns —
which is exactly what the vessel-changer swap does to the Squirrel — both its `Trail`
containers were EMPTIED while every laid prism still pointed at them. From then on
`DimensionOf` read `TrailList.Count ≤ 1` → Singleton → the ride routed onto the SURFACE
follower: along-z "normals" on trail prisms (the exact inversion), hover spring fighting on
that axis, nearest-ground hopping freely between BOTH ribbons, `OnPrismCrossed` paying and
Mass-shielding every hop. Every reported Squirrel symptom across three rounds — "bobbing",
"between trails", "shielding both trails", "even on a straight section" — was this one line,
which is why every geometry fix helped the live-trail ride (the Urchin's own) and never
touched the Squirrel's.

The fix is the platform law applied to bookkeeping: **the wake OUTLIVES its vessel.** Mass is
conserved — the prisms persist — so their container must persist as live structure too.

- `OnDisable` no longer clears. The `Trail` objects are plain C# state kept alive by the
  prisms that reference them; they die with their last prism, the correct lifetime.
- Explicit resets that MEAN to drop bookkeeping (turn resets, the cell-swap drain) still call
  `ClearTrails()` — and `Trail.Clear()` now **un-stamps membership first**, so even they leave
  honest container-less prisms (classified by census), never members of an empty list. The lie
  is unrepresentable.
- `IsRidable` now also requires `p.Trail == this`: a persistent trail's list can accumulate
  POOL-REUSED entries over a long session (a prism reborn elsewhere, its transform wherever
  its new life put it), and membership is what tells a survivor from a phantom — reused
  entries bridge as holes.

The same round widened the bend buffer the playtest asked for: `facingFlipThreshold` (0.35)
replaces the re-latch band — TRUE hysteresis, the forward/reverse mapping flips only when the
aim crosses well past broadside the OTHER way, so a bend sweeping the axis under a steady nose
holds the latched direction instead of flapping at the apex.

## The stamp (round 13): the spine cannot be RECONSTRUCTED — the lay must record it

Round 12's spine recovery was right about the helix and wrong about the cure. Its claim —
"the block's rotation preserves the lay-time right vector" — is FALSE for exactly the blocks a
drifting Squirrel lays: the lay offset rides the **ship's** right, but the block's rotation can
be a travel-aligned override (`BlockRotationOverride`, set by drift and barrel-roll bridging so
prisms lay along the travel direction). For those blocks `block.right` is the wrong axis, and
subtracting ~10u along a wrong, per-block-varying direction is the reported "bobbing up and
down and over to the other trail" (the flailing hull then contacts and pays both ribbons —
the "shielding both trails").

So the recovery is dead as a concept: **any reconstruction of the lay offset from the block's
own geometry has now failed twice** (fixed-distance-along-right → helix under roll;
undo-along-block-right → wrong axis under the rotation override). The spawner now stamps
`Prism.TrailLayOffset` — the exact world-space vector it added to the spawn position — and
`Trail.RidePoint` subtracts it. Immune to roll, to the rotation override, to per-block boost
gap variance (the round-12 approximation note is void — the stamp is per-block and exact), and
to the payoff growing ridden blocks (no live width read). Cleared on pool reuse, stamped after
`Initialize`, zero for spawnable lays and ungapped wakes (centres already ARE the spine).

## The helix (round 12): a rolling layer BRAIDS its ribbons — ride the spine

Round 11 restored the right transformer and the ride still "orbited like crazy" — because the
RAIL itself was a corkscrew. The lay places every gapped block at
`spine + vesselRight × (width/2 + halfGap)` using the vessel's right **at lay time, roll
included**. A vessel that rolls while flying — the Squirrel, constantly — therefore lays each
of its two ribbons as a **helix braided around its flight path**, radius ≥ ~9.6u (up to ~30u
skim-widened). Every ride line at a fixed offset from the spine along each block's lay-time
right inherits that helix: the block centres (ridden before round 9) and the inner edge (what
round 9's `RidePoint` chose as "the width-independent line") alike. Following a 9.25u-radius
helix at 150 u/s IS orbiting like crazy — in every round so far, under every transformer.

The fix was exact in intent and WRONG in mechanism *(see round 13 — the block-right
reconstruction fails under `BlockRotationOverride`; the lay now stamps the offset)*: `RidePoint` undoes the **entire** lay offset —
`blockPos − blockRight × (width/2 + halfGap)` — recovering the SPINE, the path the laying
vessel's own centre flew. Because the block's rotation preserves the lay-time right vector,
the recovery holds under full roll: the spine is straight where the flight was straight,
curved where it curved, and never braided. `Trail.LateralHalfGap` (stamped by the spawner
alongside `LateralAnchor`) carries the gap half of the offset.

Two consequences, both correct:

- **Both ribbons of a pair map to the SAME spine.** The pair is one wake; whichever ribbon you
  touch, the road is the path the vessel flew — you slide down the corridor between the twin
  ribbons, prisms streaming past on either side. (The payoff still applies to the blocks of
  the ribbon you attached to, as you pass them.)
- **`Attach`'s seeding reads `RidePoint`/`HeadingAt` too**, never raw block positions —
  seeding on the ribbon's helix while the ride runs on the spine was a lay-offset-sized snap
  on the first moving frame.

One approximation is accepted and recorded: `LateralHalfGap` is trail-level, so a spawner
whose `ApplyBoostGap` varies the gap per block (a boosting Sparrow) gets a slightly
approximate spine during the boost. The Squirrel and Urchin gaps are constant.

## Back to what shipped (round 11): the ride sits ON the trail, and the pilot owns attitude

Playtest: *"I attached to a Squirrel trail and tried to go forward and it swung me around.
Backward worked better... we had something years ago on the main branch that felt better than
this."* That was the signal to stop redesigning and go read the original, which is still in
history — `GunShipController.Slide` at `d895f329a`, and the commit that named the intent,
`023d53cc7 "When attached move down the direction you are looking"`.

What the original did, in three lines that matter:

```csharp
transform.position = Vector3.Lerp(currentBlock.position, nextBlock.position, trailLerpAmount);
// ...rotation lerp deliberately commented out - attitude is never touched while sliding...
if (Vector3.Dot(transform.forward, distance) < 0) moveForward = !moveForward;
```

The hull rides **ON** the trail, its **attitude is never touched**, and the direction of travel
is **where you are looking**. Two things I had built on top of that are now removed:

- **The positional orbit** (ride at a radius, parallel-transport a radial, roll to carry it
  around) — and
- **the up-twist** that dragged the hull's up onto that radial every frame.

Both came from an over-literal reading of round 5's *"roll should rotate them around the
trail"*. While riding, the hull's forward IS the rail, so an **ordinary `Roll()` already spins
the pilot around it** — the feature was free all along. The imposed twist instead fought the
stick every frame, and on a curving ribbon (a Squirrel drift line, exactly the test case) the
radial swung as the axis turned and took the hull bodily with it. `Roll()`, `Yaw()` and
`Pitch()` now all run exactly as in free flight.

**And the forward/backward asymmetry had its own cause.** `SeedTrailRide` latched the facing
sign from `dot(nose, axis)` — but you fly INTO a trail, so at the instant of contact the nose
is usually *across* the ribbon, that dot is near zero, and its sign is noise. Push forward and
you were as likely to be sent back the way you came as onward; whichever way the coin landed,
the other one "worked better". It now seeds from the direction the follower latched, which
`Attach` takes from the vessel's **Course** — so push-forward-at-attach always means *keep
going the way I was flying*, and the hysteresis band holds it until you genuinely turn to look
the other way.

Kept from the modern work, because none of it fights the original model: the Catmull-Rom
centreline (a strictly smoother version of the original's segment lerp), hole bridging, the
speed/throttle inertia, `RidePoint`'s width-independent line, the payoff, and a short
`railSettleRate` ease so contact settles onto the rail rather than snapping to it.

## Polishing the grind (round 10): junctions OUT, weight IN

Junctions are **removed** (see the round-8 entry for what was learned and kept). The ride now
does one thing and does it well, and the polish is all about giving the rail WEIGHT and taking
the last steps out of the frame it moves in:

**1. The ride's speed is smoothed, in the follower, where terrain changes happen.**
`TrailFollower` keeps a `_rideSpeed` that chases `Throttle × terrainSpeed × SpeedMultiplier`
(`speedTrackingRate`). This matters most at a **domain boundary**: friendly 150 against hostile
10 is a 15× cliff, and the old walk re-read terrain speed per block *within* a frame and
published each value to `VesselStatus.Speed` in turn, so the ride's speed stepped block to
block and the frame's last block won. One smoothed target turns every cliff — terrain change,
throttle change, release — into a deceleration you can feel.

That replaced the per-block time-accounting walk entirely (`LookAhead` + the `while` loop). A
frame covers ~2.5u at full grind (150 u/s at 60 fps) against blocks 4u and longer, so treating
speed as constant across a frame costs nothing measurable — and it removed `LookAhead`'s
"fewer than two blocks" early-out, which fought the hole bridging by refusing to move at all
on a sparsely-surviving ribbon.

**2. The throttle has inertia, so a reversal SWINGS THROUGH ZERO.** The transformer smooths its
signed throttle (`trailInertiaRate`) and only re-latches direction while that smoothed value is
outside the deadband — flip the stick and the grind coasts down, crosses zero, and picks up the
other way, instead of an instant about-face at whatever speed you were doing. The follower is
now ticked **every frame** rather than only while over the deadband, because it owns the ride's
speed and therefore the coast; cutting the call at the deadband made release a hard stop.

**3. The axis read is CONTINUOUS.** *(Round 11 removed the orbit frame this originally served;
`RibbonAxis()` survives and still matters, because it is what the facing dot is taken against.)*

**3a.** `IndexOrderHeading` is a step function —
it only changes when the block index changes — so parallel-transporting the orbit radial against
it kicked the grind once per block, a tick at exactly the trail's periodicity (the same shape of
defect as the round-7 chord bug, one layer up). `GunVesselTransformer.RibbonAxis()` prefers the
follower's Catmull-Rom tangent (`TravelHeading`, continuous through crossings) re-expressed in
index order, and falls back to the discrete axis when parked or degenerate.

**4. Latching on carries your speed and never pops.** *(The orbit-radius half is superseded by
round 11's `railSettleRate`; the speed and throttle seeding stand, and the facing seed was
corrected again in round 11 — it was still taken from the nose here.)* `Attach` seeds `_rideSpeed` from the
vessel's arrival speed (starting the grind at a dead stop and ramping up brakes the pilot for
latching on, which is backwards); `SeedTrailRide` seeds the grind throttle from the stick, so
holding forward through a contact just keeps going. The orbit radius seeds at the hull's ACTUAL
distance and eases in exponentially (`orbitRadiusSettleRate`) — clamping the seed instead, as
the junction work did, teleported the hull sideways at the instant of contact.

## Riding another vessel's wake (round 9): what the SQUIRREL exposed

> **Read 1–3 as history.** They were junction fixes, and round 10 removed junctions outright —
> those fields are gone. They are kept because they are the evidence behind the two rules worth
> re-applying if junctions ever return. **#4 is live and load-bearing.**

The Urchin's own wake is the easy case — constant block size, straight lay. Test-riding a
**Squirrel** trail broke the ride ("moving me around on strange axes and between both trails"),
and the trails themselves were fine: `Squirrel.prefab` authors `BaseScale.x 20` / `Gap 18.5`,
which is two 0.75-wide ribbons 19.25 apart, laid into two separate `Trail` objects, correctly
stamped. Every fault was in the RIDE, and all three came from the same blind spot — **the
Urchin's wake is not a representative trail**:

**1. A parallel ribbon is not a fork.** The junction probe accepted any nearby ribbon, and a
vessel's own SECOND ribbon runs alongside the first for its entire length — so it was a
candidate at every block crossing, and whichever of the pair happened to win the `|dot|`
comparison took the rider. Each hop was a sideways teleport across the pair's gap (19u on a
Squirrel). Fixed with `junctionParallelThreshold`: **a junction is a DIVERGENCE**, so a branch
running parallel to the ridden ribbon is the same road and is skipped. This is what the rule
was always missing — the round-8 wording ("fork onto the better-aligned trail") is only
meaningful between trails that actually go different ways.

**2. The probe radius was keyed off the ridden block's size.** `4 × largest extent` is
harmless on an Urchin (blocks are a fixed 4u long) and absurd on a Squirrel, whose block scale
is **dynamic** — `SetNormalizedXScale` (driven by skimming) and `SetDotProduct` (drift) against
`maxBlockScale: 5` take blocks to ~40u wide and ~27u long, so the probe swept a **160-unit**
radius and forked onto anything in the arena. It is now a plain world-unit
`junctionSearchRadius` (12u): "another ribbon within reach", independent of what the ridden
vessel's blocks happen to be doing.

**3. The grind radius was unbounded.** `SeedTrailRide` keeps the distance you latched on at,
which is right — but with no ceiling, a fork onto a ribbon 19u away seeded a 19u orbit and flung
the hull around the new rail. Now clamped to `maxOrbitRadius` (8u), with the existing settle
reeling it in.

**4. A gapped wake's block CENTRES are not its spine — and the ride follows the spine now.**
The lay holds each ribbon's INNER EDGE at a constant offset (`xShift = halfWidth + halfGap`),
which is exactly what keeps the pair's gap constant while blocks change width. The corollary
is that the block *centres* swing sideways whenever the width changes: a Squirrel skim-widening
1×→5× moves its ribbon's centreline by ~20 units **in dead-straight flight**. Riding centres
meant being swerved on axes with nothing to do with the flight path. `Trail.LateralAnchor`
(declared by the layer — a prism cannot tell which of its faces points at its sibling) plus
`Trail.RidePoint` recover the width-independent line, and every geometry read in `Project` /
`LookAhead` / `HeadingAt` / the spline control points goes through it. The correction uses the
block's own local right, which is the same axis the lay offset used, so it stays exact even
when the hull is yawed (see below). `LateralAnchor` is 0 for ungapped wakes and for everything
a spawnable lays, where the centres already ARE the spine.

**Known, not changed: a drifting Squirrel's prisms are not axis-aligned to their trail.**
Prisms lay with `blockRotation`, which is the vessel's rotation — and mid-drift the nose is
yawed off the course, so those prisms' z points where the ship was POINTING, not down the
ribbon. The platform already has the mechanism to correct it (`BlockRotationOverride`, which
`BarrelRollController` and `ScarabJukeController` both set "so bridging trail prisms lay
travel-aligned"); the drift simply does not use it. The 1D ride is immune — it derives its axis
from the CURVE of block positions, never from prism rotation — so this is a visual/design call
about what a drift line should look like, not a ride bug. Flagged here because the platform
invariant is stated as *trail prisms have z down the trail*, and for the drift vessel that is
currently false.

## Holes (round 8): a damaged trail still rides

**Trail integrity over missing prisms.** A DESTROYED prism still rides — its object stays in
place as a restorable skeleton, the walk's geometry is intact, and the payoff restores it in
passing. What used to break the ride was a real hole (a null entry at teardown, a pooled-away
object) — `Project`/`LookAhead` dereferenced it — and destroyed mass halted the slide anyway,
because `DestroyedTerrainSpeed` was authored 10 against a friendly 150. Both closed:

- The walks now **BRIDGE holes**: `Trail.IsRidable` (non-null + active — destroyed-in-place
  passes), `TryStepRidable` (one ridable step with the same wrap/reflect end semantics, so the
  end-of-ribbon launch still hears reflections), and every step in `Project`/`LookAhead` routes through
  it. A missing prism splices out of the ribbon: the segment spans the survivors, the spline's
  outer control points fall back to the segment endpoints (`ControlBlockPosition`), and a walk
  that runs out of survivors parks on the last one instead of throwing.
- `DestroyedTerrainSpeed` on `Urchin.prefab` is now **150** (both followers) — riding across a
  destroyed stretch keeps pace, and the payoff re-builds the ribbon under you as you cross.

**Junctions were built here and REMOVED in round 10.** The rule was implemented as designed
(probe the ridden block's neighbourhood on each crossing, fork onto whichever ribbon runs more
along the pilot's facing) and it worked, but it made the single-trail ride harder to judge:
every crossing carried a chance of leaving the rail you were on, and two rounds of tuning went
into stopping it firing when it shouldn't (a vessel's own parallel second ribbon; a probe
radius that scaled with a Squirrel's 40u blocks). **A ride has to be excellent on ONE trail
before choosing between two is worth anything**, so the concept is gone rather than carried
half-tuned. What it left behind is all still here and all still earning its place: hole
bridging, `Trail.HeadingAt`, `SeedTrailRide`, and the orbit-radius settle (which now serves the
attach approach it was always also doing).

If junctions return, the two findings that cost the most to learn are worth keeping: a junction
is a **divergence** (a parallel ribbon is the same road, and a vessel's own second ribbon is
parallel for its whole length), and the probe radius must be in **world units** (block extents
are dynamic on some vessels).

## The rail grind (round 5): each ride's controls map onto its prismscape's z-axis

Round 4 made the rides smooth; playtest feedback set the FEEL, and it demanded **different
logic per dimension** — cohesive to the player, but built on each prismscape's own relationship
with its prisms' z-axis.

**The 1D grind.** Round 4's "hull eases onto the travel heading + direction latched at attach"
is replaced:

- **Throttle = signed speed** up and down the ribbon. Forward/reverse is resolved from the
  pilot's FACING — `dot(vessel.forward, IndexOrderHeading)` — the scheme the original Urchin
  used, and it worked because the reference is the ribbon's **index-order axis**, which never
  flips with travel. (Round 3 failed doing "the same thing" against `Course`, which DOES flip
  with travel and fed back on the decision. Same dot product, opposite stability.) A hysteresis
  band (`facingDeadband`) keeps an aim near broadside from flapping the mapping.
- **Roll ORBITS the hull around the trail.** *(SUPERSEDED in round 11 — the positional orbit
  and its up-twist are removed; roll is now ordinary free-flight roll, which already spins the
  pilot around the rail because their forward is the rail. The rest of this entry stands.)* The rider sits at the radius it latched on at
  (`minOrbitRadius` floor), on a radial kept perpendicular to the curving axis by parallel
  transport; roll input carries it around the ribbon (`orbitDegreesPerSecond`, handedness
  corrected by facing so "roll right" always moves YOUR right). `TrailFollower` became a pure
  centerline kernel to allow this: it publishes `CenterlinePoint`/`TravelHeading` and no longer
  writes the transform — the transformer composes `position = centerline + radial × radius`.
- **Pitch and yaw stay free** — the pilot AIMS while riding (the ride carries you; the spikes
  are why you're here). The only attitude the ride imposes is a twist: up eased radially OUT
  from the ribbon (`trailUpAlignRate`), forward untouched.

**The 2D marble.** Two additions to the round-4 plane model:

- **Momentum** (`surfaceInertiaRate`): the surface velocity CHASES the steered target instead
  of being it — release glides to rest, turns carve arcs, reversing swings through a stop. The
  stored velocity is re-projected onto the tangent plane each frame so momentum follows the
  surface as it curves, and the follower ticks every frame (a hard park would delete the glide).
- **Rim wrap** (`rimWrapMargin`): past the sheet's boundary — no nearer prism took over and the
  rider is outside the ground's in-plane footprint — the target normal blends toward the radial
  from the RIM POINT and the hover anchor becomes that rim point, so the floor swings around
  the edge at hover distance and the rider rolls onto the far side, where the authored normal
  takes over again. The two frames meet continuously at the rim (the anchor difference is
  purely in-plane). One blend, no special-case wrap code — and it composes: a large hole in a
  sheet wraps the rider around the hole's lip, which is exactly what a marble does.

## The 2D roll: a smoothed plane over AUTHORED normals — never boxes

`BlockscapeFollower` is the **2D ride kernel**, and since round 4 it models the AGGREGATE
surface, never the individual boxes — the design requirement is verbatim: *"players should feel
they are smoothly rolling along a curved continuous surface, not from one prism to another. They
should never feel the gaps between prisms or the edges of prisms."*

(Round 3 shipped this as a face crawl with edge folds and hop probes — per-prism box math,
which is structurally incapable of hiding the boxes. Deleted the next day after playtest; do
not bring back per-face logic here.)

- **The normal is AUTHORED, not inferred.** A surface prismscape's prisms are laid with their
  local **Z orthogonal to the surface** (as a trail prism's Z is parallel to its trail), so the
  ground prism's `transform.forward` IS the local normal — no face inference, no folding. The
  sign (a shell has two sides) resolves toward the smoothed ridden normal, so a curved sheet
  cannot flip the floor out from under the vessel.
- **The ridden plane is a SMOOTHED state.** The world normal slerps toward each ground prism's
  authored normal (`normalTrackingRate`), so crossing prisms turns the floor through a
  continuous arc rather than snapping facet to facet.
- **Height is a soft spring** toward `hoverHeight` along the smoothed normal, anchored at the
  ground prism's mid-plane (`hoverTrackingRate`) — prism-to-prism height steps read as swell.
- **Gaps are coasted.** The ground reference is only ever *replaced* by a nearer live prism
  (`PrismSpatialIndex.QuerySphere`, nearest-by-distance), never dropped — over a hole, or when
  the ground is shot out from under the rider, the last plane carries it smoothly until the far
  edge takes over.
- **The pilot keeps full steering.** `Slide()` runs the same protected
  `Roll()`/`Yaw()`/`Pitch()` passes free flight runs; motion is the steered forward projected
  onto the tangent plane (signed by throttle — pull backs up), and the hull's **belly eases
  onto the surface normal** (`surfaceAlignRate`) as a minimal-twist correction ON TOP of the
  steering, never a replacement for it.
- **`OnPrismCrossed`** fires when the ground reference changes prisms — the surface analogue of
  the trail's block crossing, and the event `ApplyPrismscapePayoff` subscribes to.

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

(The other two rows are the spike weapon on CHARGE and the track projector on SPACE — see
`URCHIN_CHAIN_SPIKES.md` and `URCHIN_TRACK_PROJECTOR.md`.)

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

## Running out of rail (round 22): the ribbon ENDS, so you LAUNCH

Reaching the end of an open ribbon used to **park** the rider: `Trail.Project` reflects at a
terminal block, `TrailFollower` discarded the reflected move and zeroed the ride speed, and the
vessel came to rest against the last prism. Every property of that was defensible in isolation —
adopting the bounce made the head unrideable (the transformer's throttle mapping flipped it
straight back the next frame, and the two flips oscillated the rider), and holding the direction
meant the ride resumed the moment the trail grew — but the *outcome* was that the reward for a long
fast grind was **a dead stop at the exact moment the pilot had the most momentum to spend**.

Now the ride ends the way it should: **the ribbon runs out and the vessel flies off it, carrying
the speed the grind was doing.**

Three pieces, and the split between them is the point:

| Piece | Owns |
|---|---|
| `TrailFollower.ReachedEnd` | REPORTING it. The follower is the 1D kernel and never touches `VesselStatus.IsAttached` — it sets a flag, cleared at the top of every `RideTheTrail`, and stops. |
| `GunVesselTransformer.LaunchOffRibbonEnd` | ACTING on it: clear the attach flags this frame, and fence the ribbon just left (`endLaunchReattachGrace`, 0.35 s) so a trail whose end doubles back cannot snatch the launch away in the same breath it was granted. The fence is scoped to **that one ribbon** — fly straight off one rail into another and the new one takes you immediately. |
| `GunVesselTransformer.CarrySpeedIntoFreeFlight` / `TickCarriedSpeed` | The SPEED. |

The reflected move is still discarded, so there is no snap and no oscillation; the frame the ride
ends, the hull is on the terminal block with the pilot's own attitude, and `VesselStatus.Course` is
still the last tangent the projection wrote. Free flight then re-derives `Course` from the nose, so
**you leave along the rail and immediately fly where you are pointing** — aiming the exit is a real
decision, and it is what makes the launch a manoeuvre rather than an ejection.

**A LOOP never reaches this at all** (`Trail.Project` wraps instead of reflecting), so a closed
ribbon — a boost ring, an omnicrystal ring — is still ridden forever. The difference between the two
topologies is now something the pilot can *feel*.

### The speed carry — momentum outlives the ride

A friendly grind runs at **150 u/s** against the Urchin's ~50 u/s free-flight top
(`DefaultThrottleScaler` 50, `DefaultMinimumSpeed` 0), so handing the vessel straight back to
`ComputeThrottleTarget` would delete two thirds of its speed in a frame. Instead:

- `EndRide` reads the ride's last speed and, if it beats what the pilot could fly anyway, writes it
  into the transformer's smoothed cruise field **directly** and arms `_carriedSpeed`. Writing
  `speed` rather than letting it lerp up is what makes the handoff seamless — a lerp would read as
  the vessel *accelerating* after it let go.
- `ComputeThrottleTarget` is overridden to return `max(natural, _carriedSpeed)`. **MAX, not a
  replacement**: the pilot's throttle takes over the instant it can beat the decaying carry, and a
  boost during the glide is not thrown away.
- `TickCarriedSpeed` bleeds the carry toward the natural target at a constant
  `detachSpeedDecayRate` (**12 u/s**, so 150 → 50 spends ~8 seconds), then clears it. Constant-rate
  rather than exponential so the glide has a readable slope and actually *lands*.

**Only EXCESS is carried.** A ride slower than the pilot's own cruise — hostile terrain at 10 u/s —
hands over nothing, so this can never brake a vessel and can never become a free speed floor. It is
strictly momentum the pilot already had.

**Every exit routes through `EndRide`**, so "an Urchin keeps its speed when it lets go" is one rule
with one implementation: running off the end, Slip, and a trail cleared out from under the rider all
carry. That last case used to be the odd one out — it cleared the flags in place and set `attached`
false itself, which meant the `else if` that runs `EndRide` could never fire for it, so `_rideMode`
stayed stale and the ride camera stayed pulled in for the rest of the vessel's life. It now goes
through the same `LeaveRide` as everything else.

**And it must not outlive the pilot.** `ClearLaunchState` runs from `Initialize` (which re-runs on a
live component when a vessel changes hands) and from an overridden `ResetTransformer` (a respawn /
turn reset, which zeroes `speed`). A carry left standing would floor the throttle target back up to
a ride speed the new life never earned.

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
| The 1D centerline kernel | `Controller/Vessel/TrailFollower.cs` — `Attach` (bool; seeds direction, lerp and speed from the touch), `CenterlinePoint`/`TravelHeading`/`IndexOrderHeading` (publishes, never writes the transform), `SetDirection` (range-clamped), `RideTheTrail` (one smoothed speed per frame), `ReachedEnd` + `AttachedTrail` (the launch signal and the ribbon it left), `RideSpeed`, live `Domain` |
| The launch + speed carry | `Controller/Vessel/GunVesselTransformer.cs` — `LaunchOffRibbonEnd`, `IsReattachBlocked`, `CarrySpeedIntoFreeFlight`, `TickCarriedSpeed`, `ComputeThrottleTarget` override, `LeaveRide`, `ResetTransformer` override |
| The 2D ride kernel (marble) | `Controller/Vessel/BlockscapeFollower.cs` — smoothed plane over authored normals (prism Z ⊥ surface), momentum (`surfaceInertiaRate`), hover spring, gap coasting, rim wrap (`ResolveSurfaceFrame`), `OnPrismCrossed`, `SurfaceNormal` |
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
| `FriendlyTerrainSpeed` / `HostileTerrainSpeed` / `DestroyedTerrainSpeed` | `Urchin.prefab` — `TrailFollower` AND `BlockscapeFollower` | **150 / 10 / 150** on both. The 15× hostile gap is what Slipstream buys. Destroyed = friendly pace (round 8): a hole must not halt the slide — the payoff restores the ribbon as you cross it. Keep the trios matched unless the roll should price differently. |
| `trailInertiaRate` | `GunVesselTransformer` (C# default **6**) | 1/s chase of the signed grind throttle — coast on release, and a reversal that swings through zero instead of snapping. |
| `orbitRadiusSettleRate` | `GunVesselTransformer` (C# default **2**) | 1/s ease of the grind radius from the attach distance down to `minOrbitRadius`. Exponential: brisk from far, gentle at the end, never a pop. |
| `speedTrackingRate` | `Urchin.prefab` `TrailFollower` (C# default **5**) | 1/s chase of the ride's speed. The rail's WEIGHT — this is what makes a friendly→hostile boundary (150→10) a braking slide rather than a 15× snap. |
| `orbitRadiusSettleRate` | `GunVesselTransformer` (C# default **1.5**) | u/s reel-in of the grind radius toward `minOrbitRadius` — matters after a wide fork. |
| `growthAmount` | `Urchin.prefab` `GunVesselTransformer` | `ElementalFloat`, element **Mass**, Min 0.6 → Max 1.2, `Value` 1 |
| `rechargeRate` | `Urchin.prefab` `GunVesselTransformer` | 0.1 ammo/s, **×2** on a shielded prism |
| `ammoIndex` | `Urchin.prefab` `GunVesselTransformer` | 0 — the same slot the spike volley spends |
| `throttleDeadband` | `GunVesselTransformer` (C# default **0.1**) | Signed throttle below this magnitude parks the rider. Never let it reach 0: `RideTheTrail` divides by `Throttle × speed`, and XDiff idles NEAR its rest, never exactly on it. |
| `throttleRestPosition` | `GunVesselTransformer` (C# default **0.5**) | The XDiff value that reads as neutral — XDiff RESTS AT 0.5 (`GamepadInputStrategy`). Push above to keep riding the latched direction, pull below to back up. |
| `rideSurfaceClearance` | `GunVesselTransformer` (C# default **1.5**) | Clearance beyond the prism's solved face distance — the hull's own half-thickness. Raise until the ship reads as ON the trail, not sunk in it. |
| `railSettleRate` | `GunVesselTransformer` (C# default **4**) | 1/s decay of the offset the hull had at contact. The ride sits ON the trail; this only stops attaching from snapping it there. |
| `facingFlipThreshold` | `GunVesselTransformer` (C# default **0.35**) | TRUE hysteresis: the forward/reverse mapping flips only when the aim crosses past broadside the OTHER way by this much. Widen if a bend still flaps direction. |
| `hoverHeight` | `BlockscapeFollower` (C# default **2**) | World-unit hover above the ground prism's mid-plane, along the smoothed normal. |
| `normalTrackingRate` | `BlockscapeFollower` (C# default **5**) | 1/s ease of the ridden plane toward the target normal. **This IS the surface feel**: low = long arcs that round off the facets, high = tight tracking. |
| `hoverTrackingRate` | `BlockscapeFollower` (C# default **5**) | 1/s ease of hover error. Soft so prism-to-prism height steps read as swell, not bumps. |
| `groundSearchRadiusScale` | `BlockscapeFollower` (C# default **2.5**) | Ground search radius in multiples of the ground prism's largest extent. Big enough to bridge lattice gaps, small enough not to grab the far wall of a channel. |
| `surfaceInertiaRate` | `BlockscapeFollower` (C# default **4**) | 1/s chase of the steered velocity — the marble's WEIGHT. Low = long glides and drifting arcs, high = direct control. |
| `rimWrapMargin` | `BlockscapeFollower` (C# default **1**) | How far past the ground's footprint (× its largest extent) the rim wrap completes. |
| `ghostSecondsAtRestingTime` / `AtFullTime` | `UrchinSlipAction.asset` | 0.6 → 1.6. `GhostSecondsForLevel` is linear in level, anchored at 0 and 10, **extrapolated** across `[-5, 15]`, floored at 0. |
| `detachImpulse` | `UrchinSlipAction.asset` | **0** — off. Raise if a detach should visibly leave the ribbon rather than sliding off it. |
| `detachSpeedDecayRate` | `GunVesselTransformer` (C# default **12**) | u/s bleed-off of the speed carried off a ride. 150 → the ~50 cruise takes ~8 s. Constant-rate, so the glide has a readable slope and lands rather than trailing off. Only ever removes EXCESS. |
| `endLaunchReattachGrace` | `GunVesselTransformer` (C# default **0.35**) | Seconds after an end-of-ribbon launch during which THAT ribbon cannot re-latch. Scoped to the one trail, so the next rail you aim for still takes you. |
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

The 2D roll adds **bounded spatial-index reads, never physics**: one
`PrismSpatialIndex.QuerySphere` census at a non-trail attach (`PrismscapeTopology.DimensionOf`),
and **one per frame per actively-rolling vessel** (the ground-tracking query — a bucket-grid
lookup over a few blocks' radius, allocation-free via a shared scratch list). Per-frame is the
honest price of continuous ground tracking: the smoothed plane must know the nearest prism every
frame to turn facets into arcs. It is bounded by the search radius (~2.5 ground extents), runs
only while attached to a surface, and at most a handful of vessels can ever be rolling at once.

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
7. **Grind — and it must be SMOOTH and 1-DIMENSIONAL.** Push the speed axis: the vessel slides
   along the ribbon at the radius it latched on at. No jitter, no per-prism hitching, no
   attitude snap at attach or detach. Aim around with pitch/yaw while sliding — moving and
   aiming are independent. Aim DOWN-trail and push: you slide the way you face. Aim UP-trail
   (turn past broadside) and push: you now slide the other way — facing decides forward.
8. **Reverse and orbit.** Pull the speed axis below rest: the grind coasts down, passes
   through zero, and backs down the ribbon — a swing, never an about-face. Hold roll: the hull
   spins around the rail exactly as it would in free flight — nothing fights the stick, and
   the hull does not get swung around on its own.
8a. **The rail has weight (round 10).** Release the stick mid-grind: the ride COASTS to a stop
   rather than cutting dead. Grind from your own trail onto an enemy's: the 150→10 terrain
   change reads as braking, not a snap. Latch on at speed with the stick held forward: you
   carry that speed onto the rail — no stop-and-ramp — and the hull eases in toward the rail
   rather than popping sideways. Grind a long ribbon at full speed: no tick at block
   boundaries in POSITION, HEADING or the orbit frame.
8b. **Roll a surface — marble madness.** Fly into a gyroid or Schwarz-P shell: the vessel
   latches (console: `Riding a Surface prismscape`), the belly eases onto the surface, steering
   stays fully live, and the ride carries MOMENTUM — release the stick and you glide to rest;
   turn hard and you carve an arc. The shell must read as one smooth curved floor: **no edges,
   no bumps at prism boundaries, no dips over gaps**. Roll off the sheet's outer edge: the
   vessel **wraps around the rim onto the other side** and keeps rolling. The payoff runs per
   prism visited — watch the domain convert under you. If the ride facets or bumps, lower
   `normalTrackingRate` / `hoverTrackingRate`; if it feels heavy or floaty, tune
   `surfaceInertiaRate`.
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
16a. **Run off the END of a ribbon (round 22).** Grind your own trail all the way to its last
    prism with the throttle held. The vessel must **fly off it, not stop** — the camera pulls back
    out, free flight resumes, and the speed you carry off should be roughly the grind's 150 and
    then bleed down to the ~50 cruise over ~8 seconds. Watch the speed readout (or just the trail
    wavelength, which is `wavelength / speed`): it should fall steadily, not step.
16b. **The launch does not re-latch itself.** Repeat 16a on a curved trail whose end bends back on
    itself. You must stay OFF it for the 0.35 s grace even if the hull brushes it. Then fly
    straight off one rail into a second one nearby — the second must take you **immediately**;
    the fence is scoped to the ribbon you left, not to attaching in general.
16c. **Coasting to a stop at the end still works.** Ride toward the end and RELEASE the throttle
    before you reach it. The ride coasts down; if it comes to rest before the last prism you stay
    attached. (Only a rider still moving when the ribbon runs out is launched.)
16d. **A LOOP is still infinite.** Ride a boost ring / omnicrystal ring (a closed ribbon). It must
    never launch you — a loop wraps rather than reflecting.
16e. **The carry does not survive a life.** Launch off a ribbon at 150 and, while still gliding,
    end the turn / respawn / swap vessels. The new life must start at its ordinary cruise, not at
    the carried speed.
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
- **`PrismscapeTopology`'s census thresholds are heuristics** (`< 3` neighbours = Singleton,
  `> 55` = Volume, between = Surface, census radius 3× the prism's largest extent). They were
  sized from geometry (an r² patch ≈ 28 same-size blocks, an r³ ball ≈ 113), not measured against
  real gyroid/Schwarz lays. If a flora shell ever classifies as Volume, tune the constants there —
  do not fork a per-caller classifier. Note the census only serves CONTAINER-LESS prisms (flora
  growth); builder-laid surfaces are declared via `Trail.Dimension`.
- **Grown gyroid/Schwarz FLORA prisms are not yet declared 2D.** `AssembledFlora` grows its
  lattice prism-by-prism with no `Trail` container, so those shells classify through the census.
  If the census misreads a young (sparse) flora shell, the right fix is to declare the dimension
  at the layer — the same `Trail.Dimension` pattern the spawnables use, or a future authored
  field on the growth — never a bigger heuristic.
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
