# Urchin Track Projector — bring your own rail

> The Urchin's other three abilities: **`URCHIN_CHAIN_SPIKES.md`** (the one-trigger spike
> weapon), **`URCHIN_TRAIL_RIDER.md`** (the ride, and the launch off the end of a ribbon).
> This is the ability that makes the other two usable in open space.

## The problem it solves

Everything the Urchin is good at runs on someone's prismscape. It grinds a ribbon at 150 u/s,
recharges its ammo by riding, grows its own trail and steals everyone else's — and away from a
trail it is an ordinary ship with a shotgun, flying at a 50 u/s cruise with nothing to latch onto.
Big cells have long empty stretches. So the vessel can now **bring its own rail**: press the left
trigger and a straight, single-lane stretch of trail forms out in front of the nose.

It is not a wall, a weapon or a trap. It is **one lane of ordinary conserved trail mass in the
pilot's own domain**, so every system in the game already knows what to do with it:

- the ride reads it as **friendly terrain** (150 u/s) and **grows** it as it passes
  (`GunVesselTransformer.ApplyPrismscapePayoff` → `Prism.Grow`),
- at **Mass 5** ("Reinforced Wake") the prisms you ride over come up **shielded** — so a lap of
  your own ramp armours it, at the cost of double ride-ammo on the next lap and of dropping those
  prisms out of the cell's fauna targeting grids (shielded mass is never prey),
- an opposing Urchin rides it as hostile terrain at 10 u/s and **steals** it under itself
  (`Prism.Steal`, same method, else branch),
- **fauna graze it like any other trail** — not by assertion: `BoostRingBuilder.LayOne` calls
  `Prism.Initialize`, which registers with `PrismSpatialIndex`, whose `BindCell` calls
  `Cell.AddBlock`. That is the same registration path the vessel wake takes, so the track lands in
  the cell's targeting grids and per-domain volume sums exactly like any other prism,
- it is destroyed only by an active force, never by a clock.

Nothing here is a new fundamental. It is Prisms/Prismscapes + Domain, placed on purpose.

## What a press does

| | |
|---|---|
| Input | `LeftStickAction` (2) — the left trigger |
| Element | **SPACE** — reach — scaling the track's LENGTH |
| Cooldown | **20 s**, flat. The same cooldown the Squirrel's boost-ring trigger carries |
| Cost | none but the cooldown |
| Geometry | `trackLength` (100 u) of prisms at `prismSpacing` (8 u), each `prismScale` (3 × 3 × 6) |
| Placement | `max(forwardOffset, speed × leadSeconds)` ahead of the hull, along the **NOSE** |
| L5 (Space) | **Long Haul** — `upgradeExtraLength` (100 u) more track |

**Along the nose, not along `Course`.** The ramp forms where the pilot is *aiming*, which is what
makes placing it a decision — you can throw a rail across the gap you are about to cross, or down
the line you want to attack along, rather than always along the line you happen to be travelling.

**Leading it by speed** (`speed × leadSeconds`, floored at `forwardOffset`) is the same idea the
Squirrel's tube uses: a fast Urchin needs room to line up, a stopped one must not have the track
materialise inside its own hull.

## Why it is laid through `BoostRingBuilder`

`BoostRingBuilder.LayOne` is the shared pooled-prism primitive behind the omnicrystal ring, the
joust ring and the Squirrel's tube. Using it buys two properties the ability lives or dies on, and
both of them are the *opposite* of what an ordinary trail lay does:

1. **A full-size collider from frame 0.** Track prisms come from the dedicated `PrismType.Boost`
   pool (waitTime 0, fast bloom). Under the clock law the transform is final at stamp, so the
   authored collider is already full-size while the visual blooms. The ordinary wake's
   `waitTillOutsideSkimmer` delay exists precisely to let a vessel get **clear** of the mass
   it lays; a launch ramp you cannot hit at grind speed is not a ramp.
2. **Trail membership stamped AFTER `Initialize`.** Pool reuse clears membership
   (`Prism.ResetState`), so a stamp made before `Initialize` is silently wiped and the prisms read
   as container-less **Singletons** — which the dimension ladder correctly refuses to rail-grind.
   This is the `AssignTrail`-after-`Initialize` contract from `URCHIN_TRAIL_RIDER.md` § round 6;
   the builder already honours it. **Do not lay these prisms by hand.**

**One deploy = one `Trail`**, declared `PrismscapeDimension.Trail`. That is what makes the stretch a
ribbon in its own right: you latch onto it, grind its whole length, and **launch off its end**
rather than finding yourself halfway along an earlier deploy. It is deliberately left **open**, not
a loop — running out of rail is the ability's ending, and the end-of-ribbon launch is what turns
that ending into the payoff.

## Why the cooldown is not elemental

The Squirrel's tube scales its cooldown on TIME. This one does not, and that is not an oversight:
**TIME is Slip's element on this vessel**, and a second Time consumer would be exactly the
double-dip the one-parameter-per-element convention exists to prevent. SPACE owns the length; the
cooldown is a flat authored number, matched to the Squirrel's 20 s so the fleet's two "place a
structure" abilities share one cadence.

## Element map

| Element | Ability | Input | Scales | L5 |
|---|---|---|---|---|
| **SPACE (3)** | **Track Projector** | `LeftStickAction(2)` | track LENGTH | **Long Haul** — +100 u of track |

`ResolveLength` reads the SPACE multiplier **authored on the SO** (`lengthMultiplierAtFullSpace`
2.0, floored at 0.4), with the map's generic Space multiplier pinned to **1.0** — the Squirrel
pattern, so the trade is visible in the asset and nothing double-dips.

**"Long Haul" gates on `IsUpgradeActive(Element.Space)` — the replicated unlock bit — never on a
raw local level read.** The track is prismscape: two peers laying different-length rails is a
divergent world. The continuous SPACE dial underneath it is a local level read and shares the
vessel's standing multiplayer gap (`URCHIN_BACKLOG.md` U1); it is the same trade every element dial
on this vessel already makes, and it closes when a replicated element-LEVEL surface lands.

## Files

| Role | File |
|---|---|
| Ability config | `R_VesselActions/Data Containers/UrchinTrackActionSO.cs` → `_SO_Assets/VesselActions/Urchin/UrchinTrackAction.asset` |
| Executor (cooldown, the lay, pool teardown) | `R_VesselActions/Executors/UrchinTrackActionExecutor.cs` |
| The pooled-prism primitive | `Controller/Environment/Spawning/BoostRingBuilder.cs` — `LayOne` |
| The ribbon container | `Controller/Vessel/Trail.cs` — `Dimension`, `Add` |
| Prefab wiring | `_Prefabs/Spacevessels/Urchin.prefab` — `VesselActions/TrackActionExecutor`, registered in `ActionExecutorRegistry._executors`, bound to `InputEvent 2` |
| Element map | `Assets/Resources/ElementalAbilityMaps/Urchin.asset` |
| Asset generator (idempotent, key-validating) | `Tools/Build/author_urchin_assets.py` |

## Tuning knobs

| Knob | Where | Value |
|---|---|---|
| `trackLength` | `UrchinTrackAction.asset` | **100** u — the requested length. Scaled by SPACE. |
| `prismSpacing` | `UrchinTrackAction.asset` | **8** u between prism centres. The vessel's own wake lays at its wavelength (10) and the ride bridges gaps happily, so this is a look-and-catchability dial: tighter is easier to fly into and costs more prisms. |
| `prismScale` | `UrchinTrackAction.asset` | **(3, 3, 6)** — a little heavier than one of the vessel's own wake ribbons (2 × 2.5 × 4), because this is a rail you *meant* to place and it has to be catchable at grind speed. **Z is the length ALONG the track** and must stay the long axis: the whole 1D ride rests on trail prisms being authored with z parallel to the ribbon. |
| `forwardOffset` / `leadSeconds` | `UrchinTrackAction.asset` | **40** u / **0.35** s. At the 50 u/s cruise the lead is 17.5 u, so the floor is what binds; a 150 u/s launch places it 52 u out. |
| `spawnPerFrame` | `UrchinTrackAction.asset` | **8** prisms/frame → a 13-prism track lays in 2 frames. |
| `cooldown` | `UrchinTrackAction.asset` | **20** s, flat. Matched to `SquirrelTubeAction.cooldown`. |
| `lengthMultiplierAtFullSpace` / `minLengthMultiplier` | `UrchinTrackAction.asset` | **2.0** / **0.4** — 200 u at Space 10, 40 u at the deficit floor. |
| `upgradeExtraLength` | `UrchinTrackAction.asset` | **100** u — the Space-5 bonus stretch. |

## Collider budget

**13 prisms per deploy** at the authored numbers (`floor(100 / 8) + 1`), rising to **26** at Space 5
with Long Haul, and **38** at the arithmetic worst case (Space 15 overcharge: `200 × 1.5 + 100` =
400 u). Each is a `PrismKind.Plain` prism on a LOD-cullable `BoxCollider`, held at full size from
frame 0 — no `MeshCollider`, so collider-LOD can reclaim them by phase like any other trail mass.

One deploy per Urchin per 20 s, so a four-Urchin lobby adds at most ~2.6 prisms/second of standing
mass, against a cell whose live population is in the thousands. It is the cheapest structure-placing
ability in the fleet (the Squirrel's tube lays 8 prisms per ring on a ring count the intensity
picks; the PeelTheCage lays 10,000+).

**Volume, for the cell's phase ladder**: a track prism is 3 × 3 × 6 = **54** volume (3.4× the
nominal 16), so a deploy adds **~702** to the host cell's `LiveVolume` and a Space-5 deploy ~1,400.
A cell's `FrenzyEnterVolume` is in the tens of thousands, so this cannot move the ladder on its own
— but it is real mass and it counts, so a mode that expects many Urchins should be measured rather
than assumed.

**Per-prism CPU**: zero beyond the ordinary clock-stamped bloom — transform and collider are final
at stamp; the GPU owns the visual. A later `Grow()` from a ride is unaffected.

The prisms are **conserved**: nothing removes them on a clock. `Cleanup` returns them to the pool at
a turn boundary — the same active, explicit event class as a scene load — and a live match removes
them only by an active force (a spike cascade, a rival's ride, a vessel ramming them, fauna).

**The teardown is scoped by TRAIL IDENTITY, not just by "we laid it".** A track prism that was
destroyed mid-match and recycled is, by then, someone else's mass: pool reuse clears trail
membership (`Prism.ResetState`) and the next lay site stamps its own. So `Cleanup` returns a prism
only while `p.Trail` is still one of the ribbons this executor laid. Returning everything the
executor ever touched would yank a live prism out of whatever it had become — a hazard the shared
`SquirrelTubeActionExecutor` teardown still carries.

## In-editor verification

Nothing below can be checked without play mode.

1. **Project compiles with zero errors**, and `Urchin.prefab` shows a **TrackActionExecutor** child
   under `VesselActions` with `prismSpawnChannel` and `OnMiniGameTurnEnd` wired, and a third entry
   in `ActionExecutorRegistry._executors`.
2. **Fly an Urchin in open space and pull the LEFT trigger.** A straight line of prisms in your own
   domain colour blooms in ~40 u ahead of the nose, running ~100 u further out.
3. **Fly into it.** The vessel latches on and grinds it at friendly speed (150). The ammo meter
   climbs while you ride.
4. **Ride to the far end.** You launch off it, keeping the grind's speed, which bleeds back to
   cruise over ~8 s (see `URCHIN_TRAIL_RIDER.md` § "Running out of rail").
5. **Pull the trigger again immediately** — nothing happens; the cooldown is 20 s. Time it.
6. **Aim somewhere else and deploy** — the track follows the NOSE, not the flight direction.
7. **Space 5**: collect Space crystals to level 5 and deploy. The track is ~100 u longer. Below
   level 5 (relock at 4) it is back to the base length.
8. **Shoot it.** Track prisms take damage like any other prism; a spike cascade converts them.
9. **Turn end**: end the turn and confirm the tracks are gone (returned to the pool, not destroyed)
   and no console errors follow.

### MPPM — two clients

10. Deploy a track on client A. Client B sees the same prisms in A's domain colour, and can ride
    them as hostile terrain (10 u/s) while stealing them under itself.
11. Deploy on A at Space 5 and on B at Space 0 — A's track is visibly longer, on **both** screens
    (the upgrade gates on the replicated bit).

## Follow-ups

- **No HUD.** The Urchin still has no `UrchinHUDVariant.prefab` (`URCHIN_BACKLOG.md` U3), so the
  cooldown is invisible. `UrchinTrackActionExecutor.CooldownRemaining01` is the surface a cooldown
  icon binds to the day the HUD exists — it is exposed and unread on purpose.
- **No deploy sound.** `deployEvent` ships **empty** (an unwired `EventReference` is a visible TODO;
  a borrowed event is an invisible one). Wire it when the ability has a voice.
- **No placement preview.** The track just appears, like the Squirrel's tube. If it wants a preview,
  it should be the tube's too — that is a shared "place a structure" problem, not this ability's.
