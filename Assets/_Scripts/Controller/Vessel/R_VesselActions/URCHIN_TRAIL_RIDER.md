# Urchin Trail Rider — flying into a trail and riding it

> The Urchin's other half — the spike cascade the ride carries you into range to fire — lives in
> **`URCHIN_CHAIN_SPIKES.md`**. The ride is the delivery system: it turns someone else's trail
> into your road, and the road pays as you travel it.

The Urchin flies normally until it touches a prism that belongs to a trail. Then it **latches
on**, and steering stops being the pilot's stick and becomes the ribbon's geometry — the only
input still consumed is throttle magnitude, plus a look-over-your-shoulder gesture that reverses
direction along the trail. Crossing from one prism to the next pays: your own trail **grows**
under you, an enemy's is **stolen** as you pass over it, a destroyed prism is **restored**, and
riding recharges spike ammo (doubled over shielded prisms).

Attaching is **two flags and no reparenting**:

```
contact  →  VesselAttachPrismEffectSO      sets IVesselStatus.IsAttached + .AttachedPrism
            (+ GunsActive, see below)
              │
              ▼
         GunVesselTransformer.MoveShip     edge-detects the flag
              │   true  when !attached  →  trailFollower.Attach(prism)
              │   false when  attached  →  trailFollower.Detach()
              ▼
         Slide()  replaces base.MoveShip() entirely while attached
              │
              ▼
         TrailFollower.RideTheTrail()      writes VesselStatus.Speed + .Course,
              │                             and on a prism boundary calls back:
              ▼
         GunVesselTransformer.FinalBlockSlideEffects()   ← the payoff
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
motionless on the ribbon forever with no error. `ReadThrottle()` restores the commented-out
formula (`throttleZeroPosition` 0.2, so a resting stick reads as zero rather than as a permanent
crawl) and `throttleDeadband` (0.05) treats sub-threshold throttle as stationary explicitly,
writing `VesselStatus.Speed = 0` instead of dividing by something near zero.

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
| Flight model / edge detection / payoff | `Controller/Vessel/GunVesselTransformer.cs` — `MoveShip`, `Slide`, `ReadThrottle`, `SlideActions`, `FinalBlockSlideEffects` |
| The ride kernel | `Controller/Vessel/TrailFollower.cs` — `Attach` (now `bool`), `SetDirection`, `RideTheTrail`, `GetTerrainAwareBlockSpeed`, live `Domain` |
| The surface-crawl experiment (**not** the ride) | `Controller/Vessel/BlockscapeFollower.cs` |
| Slip config | `R_VesselActions/Data Containers/UrchinSlipActionSO.cs` → `_SO_Assets/VesselActions/Urchin/UrchinSlipAction.asset` |
| Slip executor (hull colliders, ghost window) | `R_VesselActions/Executors/UrchinSlipActionExecutor.cs` |
| Vessel impact container | `_SO_Assets/Effects/Effect Containers/VesselContainers/UrchinImpactorDataContainer.asset` — `[Haptics, Attach, Damage, ElementalDebuffByDanger]` |
| Element map | `Assets/Resources/ElementalAbilityMaps/Urchin.asset` |
| Prefab wiring | `_Prefabs/Spacevessels/Urchin.prefab`: `GunVesselTransformer` + `TrailFollower` (+ a vestigial `BlockscapeFollower`) |

## Tuning knobs

| Knob | Where | Value |
|---|---|---|
| `FriendlyTerrainSpeed` / `HostileTerrainSpeed` / `DestroyedTerrainSpeed` | `Urchin.prefab` `TrailFollower` | **150 / 10 / 10.** The 15× gap is what Slipstream buys. `BlockscapeFollower` on the same GameObject serializes an identical trio and is now read by nothing — edit the `TrailFollower` one. |
| `growthAmount` | `Urchin.prefab` `GunVesselTransformer` | `ElementalFloat`, element **Mass**, Min 0.6 → Max 1.2, `Value` 1 |
| `rechargeRate` | `Urchin.prefab` `GunVesselTransformer` | 0.1 ammo/s, **×2** on a shielded prism |
| `ammoIndex` | `Urchin.prefab` `GunVesselTransformer` | 0 — the same slot the spike volley spends |
| `throttleDeadband` | `GunVesselTransformer` (C# default **0.05**) | Below this the rider is stationary. Never let it reach 0: `RideTheTrail` divides by `Throttle × speed`. |
| `throttleZeroPosition` | `GunVesselTransformer` (C# default **0.2**) | Stick centre; throttle is remapped from here to 1 |
| `reverseLookThreshold` | `GunVesselTransformer` (C# default **−0.6**) | `dot(forward, Course)` past which, on the throttle, the ride reverses |
| `ghostSecondsAtRestingTime` / `AtFullTime` | `UrchinSlipAction.asset` | 0.6 → 1.6. `GhostSecondsForLevel` is linear in level, anchored at 0 and 10, **extrapolated** across `[-5, 15]`, floored at 0. |
| `detachImpulse` | `UrchinSlipAction.asset` | **0** — off. Raise if a detach should visibly leave the ribbon rather than sliding off it. |
| `armGunsOnAttach` | `VesselAttachPrismEffect.asset` | on |
| `skipWhileAttached` | `VesselDamagePrismEffect.asset` | **on** — the platform guard. Turning it off restores the 2023 bug for every attaching vessel. |

The three `GunVesselTransformer` fields marked "C# default" are **not serialized on
`Urchin.prefab`** — the prefab predates them, so they deserialize to their initializers until it
is next saved. That is correct behaviour, but it means the inspector will show them only after a
re-save.

## Collider budget

**Zero.** The ride adds no colliders and no spatial queries: attaching is two flags, the ride is
one `Trail.LookAhead` + `Trail.Project` per frame against the trail's own block list, and the
payoff is direct calls on the prism the rider is standing on. Slip **removes** colliders for the
duration of the ghost (the hull's, temporarily) and adds none.

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
4. **Give the Urchin an ammo resource — STILL OPEN.** `ResourceSystem.Resources` is `[]` and
   `ammoIndex` is 0, so `SlideActions` throws an `ArgumentOutOfRangeException` **every frame of a
   ride** (`ResourceSystem.ChangeResourceAmount` indexes without a bounds check). Nothing else
   about the ride will be observable until this is fixed.
5. **`vesselImpactorDataContainerSO` now points at `UrchinImpactorDataContainer`** — confirm that
   is the container the `VesselImpactor` on the hull actually reads.
6. **Attach.** Menu_Main freestyle or any Urchin-playable mode. Fly into your own trail: the
   vessel should snap onto the ribbon, the camera should pull in, and the stick should stop
   steering — you follow the trail's curve. **The prism you touched must still be there**; a prism
   that explodes on contact means `skipWhileAttached` is not taking effect.
7. **Move.** On the throttle, the vessel travels the ribbon. Off the throttle, it holds still.
   If it attaches and never moves at any throttle, `ReadThrottle` is returning zero — check
   `throttleZeroPosition` against the stick's actual rest value.
8. **Reverse.** While riding, look back past ~126° from your course and hold throttle: direction
   flips and you travel back the way you came.
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

- **The AMMO RESOURCE is the blocking item.** `ResourceSystem.Resources` on `Urchin.prefab` is
  still `[]` while `ammoIndex` is 0, so `SlideActions` throws an `ArgumentOutOfRangeException`
  every frame of a ride. Executors, containers, ship geometries and pools all landed on
  `b3bc963bc`; this did not, and nothing about the ride is observable until it does.
- **Netcode components are not wired** — no `NetcodeHooks`, `NetworkVesselClientCache`,
  `NetworkVesselImpactor` or `ClientNetworkTransform` on `Urchin.prefab`, so the MPPM steps above
  are blocked on a separate multiplayer-spawn pass rather than failing.
- **`BlockscapeFollower` is still on `Urchin.prefab` and does nothing.** It is a surface-crawl
  experiment with no caller now, and it sits on the same GameObject as the `TrailFollower`
  carrying an identical `FriendlyTerrainSpeed 150 / HostileTerrainSpeed 10 /
  DestroyedTerrainSpeed 10` trio — so the prefab shows the ride's tuning **twice**, and only one
  copy is live. That is exactly how the transformer's field got re-pointed in the first place.
  Either finish the experiment as its own thing or remove the component.
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
