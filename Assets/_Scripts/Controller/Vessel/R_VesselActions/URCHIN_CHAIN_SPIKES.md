# Urchin Chain Spikes — a projectile that fires its own gun

> The Urchin's other half — latching onto a trail and riding it — lives in
> **`URCHIN_TRAIL_RIDER.md`**, and the ability that supplies a trail where the map has none in
> **`URCHIN_TRACK_PROJECTOR.md`**. They are one loop: the ride carries the vessel into range of
> enemy mass, the spikes convert it. This file covers the spikes, their cascade, and the three
> brakes that bound it.

A spike is a `Projectile` that carries a `LoadedGun`. When it lands on a prism it does three
things, in list order:

1. **Embed** — halts where it struck and stands in the mass for a beat, then fades out and
   returns to its pool (`ProjectileEmbedPrismEffectSO`).
2. **Steal** — flips the prism to the firing domain (`ProjectileStealPrismEffectSO`; mass is
   conserved, nothing is destroyed).
3. **Chain-fire** — pulls its own trigger, spraying the next generation of spikes out of the
   mass it just converted (`ProjectileChainFirePrismEffectSO`).

Each of those children does the same. That is the whole mechanic: there is no cascade
controller, no wavefront object, no scheduled expansion. **The recursion IS the design**, and
it is the modern form of the 2023 enum list `[Stop(12), Steal(7), Fire(13)]` that the surviving
spike prefabs still carry as orphaned YAML.

**Order in the container is load-bearing.** The steal must land before the volley, so the
children are fired at a prism that already wears their own domain and fly straight through it.
Fire before steal and the first thing every child does is re-convert ground the parent already
took — a whole generation spent on territory the cascade owns. That ordering is also the
cascade's primary brake (below).

## The three brakes, in order of authority

| # | Brake | Where | What it bounds |
|---|---|---|---|
| 1 | **Territory conversion** (emergent, PRIMARY) | `Projectile.DisallowImpactOnPrism` — `!friendlyFire && prism.Domain == OwnDomain` | Coverage. The wavefront extinguishes as it eats its own frontier. |
| 2 | **Generation depth** (authored) | `Projectile.ChainGeneration`; zero is terminal | The worst case, per volley. Scaled by CHARGE. |
| 3 | **Volley budget** (load shedding) | `ChainReactionBudget.TryReserveVolley()` | Cost per frame. Never coverage. |

**Brake 1 stays primary and that is a design position, not an implementation detail.** It was
the *only* brake in 2023 — `Projectile.HandleCollision` opened with
`if (trailBlock.Team == Team) return;` — and it is the reason a cascade reads as a conquest
rather than as a fireworks display: fired into open space or into your own territory it stops on
its first hop, and fired into a dense enemy trail it runs, with no code path anywhere deciding
which. Brakes 2 and 3 sit *under* it. If a cascade is dying too early, the first question is
whether it ran out of enemy mass, not whether a counter is too low.

**Brake 3 drops volleys, it does not queue them.** A backlog drained later discharges the hitch
as a burst, which is worse than the hitch — the same rule the gun fire loop already follows. A
dropped volley just ends that branch, which brakes 1 and 2 were about to do anyway. Drops are
counted (`ChainReactionBudget.DroppedVolleys`) and warned about on a 5-second throttle, because
a cap that hides its own truncation reads as "the mechanic is weak" instead of "the mechanic was
shedding". The ceiling is deliberately **global**, not per-vessel: it exists to protect the
frame, and the frame does not care which Urchin filled it.

Brake 3 is claimed **last**, after brakes 1 and 2 have both passed, so a reserved slot is never
wasted on a volley one of the other checks would have refused.

`requireDomainChange` (default on) is a fourth, narrower refusal that belongs to the steal's
semantics rather than to the cascade: `Prism.Steal` is a **no-op on super-shielded mass** and
only **sheds the shield** on shielded mass, so in both cases the prism is still hostile when
the chain-fire effect runs. Spending a generation there drains the chain against a shielded
wall for no territory.

## What the 2023 version actually did, and why it shipped

The original is worth stating precisely, because two of its properties are the reason this
feature has a doc at all. Read at `02bdeaa83:Assets/_Scripts/_Core/Ship/Projectiles/`.

**It had no depth cap, and could not have one.** `LoadedGun.FireGun()` read its **serialized**
`energy` field on every hop:

```csharp
public void FireGun()
{
    FireGun(Ship.Player.transform, speed, Vector3.zero, 1, true, projectileTime, 0, firingPattern, energy);
}   // charge could be used to limit recursion depth   <- the author's own comment, never done
```

`ProjectileFactory` resolves the pool tier from that same number (`>1` SuperEnergized, `>0`
Energized, else Normal), so the base tier is a **fixed-point self-replicator**:
`SpikeProjectile.prefab` serializes `energy: 0`, `FireSpherical` takes the `energy == 0`
tetrahedral branch and spawns **4 children at energy 0**, which are `SpikeProjectile` again.
The higher tiers walked down into it — `SuperEnergizedSpikeProjectile` (`energy: 2`) → 10 at
energy 1 → `EnergizedSpikeProjectile` (`energy: 1`) → 8 at energy 0 → base tier → 4 forever.
Nothing ever terminated except territory. "toned down urchin chain reactions" (`02bdeaa83`) is a
real commit in this repository.

**It leaked its pool, permanently, on success.** `Stop` was implemented as a bare
`StopCoroutine(moveCoroutine)`, and the coroutine it killed was this:

```csharp
public IEnumerator MoveProjectileCoroutine(float projectileTime)
{
    ...
    Destroy(gameObject);   // the ONLY cleanup call the projectile had
}
```

So every spike that actually hit something — i.e. every spike that mattered — was immortal.
In 2023 that was a leaked GameObject. After the object-pool port the identical shape drains the
pool instead, and a drained pool is a gun that silently stops firing.

**Its cascade sprayed from the pilot, not from the prism.** The volley origin was
`Ship.Player.transform`, and `Gun.FireSingle` spawns at `containerTransform.position` — so every
generation's children appeared at the ship, not at the mass their parent had just converted. The
cascade was a repeatedly-refilled sphere around the pilot rather than a spreading front. (The
port had already moved this to `transform.parent`, which is null on a detached spike and
dereferenced inside `FireSingle`.)

Everything else about it was right, and is kept.

## What the restoration changed

**Depth is the number the gun already had.** `Projectile.ChainGeneration` is stamped by
`Gun.FireSingle` from the volley's `energy` — the same int that picks the pool tier — so a
spike's tier and its remaining depth cannot drift apart. `LoadedGun.FireGun()` reads
`_host.ChainGeneration` rather than its serialized field (which survives only as the fallback for
a `LoadedGun` not mounted on a projectile), returns `-1` without firing at zero, and
`FireSpherical`'s own `energy--` is the decrement. Cleared per flight in `Projectile.Initialize`,
or a pooled reissue inherits the previous shot's remaining depth and deepens the chain for free.

**Retirement is explicit.** `Projectile.EmbedAndRetire(dwellSeconds, fadeSeconds)` halts the
mover, waits out the dwell, fades `_Opacity` from the spike's flight value of 0.5 to zero, then
raises `FlightEnded` and calls `ReturnToFactory`. Cancelling `_moveCts` has exactly the 2023
shape — `MoveProjectileAsync` swallows the cancellation and never reaches its tail — so the pool
return has to be owned here. It fades rather than popping because continuity of existence applies
to a spike as much as to a prism. The whole async body is guarded on `FlightGeneration`: an
embed dwell easily outlives a pool round-trip, and acting on a reissued instance would retire
someone else's shot mid-flight.

The container also lists `DetonateSparrowProjectileEndEffect` in `projectileEndEffects`, which is
**not optional**: `MoveProjectileAsync` deliberately does not call `ReturnToFactory`, so a spike
that expires on a **miss** would leak its pool slot — the same defect from the other direction.

**The pool comes down the chain at runtime.** `Gun.SetProjectileFactory` exists because a gun on
a vessel can be authored with its factory and a `LoadedGun` riding a pooled projectile cannot —
the factory is a scene object and the spike is a prefab asset. `Projectile.Initialize` re-supplies
it (and the pilot) **every flight**, because a pooled instance changes hands between pilots and
domains. Without it every chain volley dies on a null factory, silently, one generation in.

**A spike flies in world space.** `LaunchProjectile` reparents to null for any `spike`, because a
chain spike's parent is very often *another spike* that is about to retire into the pool; parented,
it would be dragged along and deactivated with it. `ReturnToFactory` reattaches only if this flight
detached (`_detachedThisFlight`).

### Determinism — the gun may not roll dice

`Gun.FireSpherical`'s golden-spiral volley used `UnityEngine.Random.rotation` to orient itself.
It now uses `Gun.DeterministicOrientation(origin, depth)`: the origin is quantized to a
**0.5-unit** lattice, hashed with the depth, and scrambled into three decorrelated Euler angles
by an integer-only mixer (identical on every platform).

Two separate failures, one fix:

- **Divergence.** Every peer runs this volley — button presses round-trip through
  `R_VesselActionHandler`, and a chain spike fires on each machine independently — so a global
  RNG draw orients the spiral differently per peer and the prismscape diverges on the very first
  cascade. Seeding from the volley's own position and depth means two peers whose spike reached
  the same prism fire the same pattern, so the cascade **re-converges** instead of drifting
  further apart with every generation. The quantum is the tolerance: coarse enough to absorb the
  small positional disagreement between peers simulating the same spike, fine enough that two
  genuinely different volleys do not collide onto one pattern.
- **Stream pollution.** `UnityEngine.Random` is one global stream, and deterministic systems seed
  it — `SegmentSpawner` calls `Random.InitState(seed)` for the HexRace track. A gun firing dozens
  of times a second must not be able to change what the track looks like.

### Domain paint

The spike's material assignment used to live in `Start()`, which runs **once per instance**: on a
fresh object it ran *before* `Initialize` supplied `OwnDomain` (so the spike wore whatever
`Domains.Blue` maps to), and on every subsequent pull from the pool it did not run at all (so a
spike recycled from a Ruby pilot stayed Ruby in a Jade player's hands). It now paints in
`LaunchProjectile` via `ApplySpikeAppearance`, re-reading the live domain per flight for the same
reason the gun re-reads it at fire time — domains re-pick at runtime and must never be
snapshotted.

The material is assigned as `sharedMaterial` (the theme's own per-domain asset, so spikes of a
domain still batch); the per-instance opacity that the launch fade and the embed fade animate
rides a `MaterialPropertyBlock` instead of the per-renderer clone `.material` would mint.

## One trigger, two shots (the 2026-08-18 merge)

The spikes were **two abilities on two triggers** — an aimed "Spike Volley" on SPACE/right and a
free omni "Spike Barrage" on CHARGE/left. They were always one weapon: a spike does the same three
things wherever it lands, and both element dials applied to both. So they are now **one ability on
one trigger**, and the freed left trigger carries the **Track Projector**
(`URCHIN_TRACK_PROJECTOR.md`).

The merge is a **hold**, not a mode switch:

| | **Tap** (press and release quickly) | **Hold, then release** |
|---|---|---|
| Fires on | the PRESS — semi-automatic, one blast per pull | the RELEASE |
| Pattern | `ConcentricRings` — a SHOTGUN: rings of spikes around the aim | `Spherical` — golden-spiral omni burst from the hull |
| Shape | 3 rings at r/3 of a **9°** cone, 3·r spikes each, staggered + 1 center, fired **from every muzzle** = 19 × 2 guns = **38/pull** | `chargedSpikesAtMin` **6** → `chargedSpikesAtMax` **36**, linear in the hold |
| Charge window | — | nothing below `minChargeSeconds` **0.35 s**; full at `maxChargeSeconds` **2.5 s** |
| Ammo | 0.15 | **free** — the hold IS its price |
| Muzzle speed | 60 × CHARGE multiplier | 40 × CHARGE multiplier |
| Depth at Charge 0 → 10 | **1 → 4** | **1 → 4** |
| L5 | CHARGE "Overcharge" — one extra generation AND `ChainRangeFalloff` → 1 | same |

**Holding is strictly additive.** The press fires the aimed blast and *then* starts charging, so a
tap behaves exactly as the old volley's first pull did and the pilot never chooses between the two
shots. `repeatWhileHeld` is authored **off** — the hold now belongs to the charge, so the volley no
longer auto-repeats at 3/s; the field survives for a vessel that wants the old behaviour back with
`chargeEnabled` off.

**The charge is timed on the EXECUTOR, never on the SO.** One asset serves every Urchin in the
match, so a charge timer on it would be last-writer-wins across vessels — the same reason
`ElementalFloat` is banned from these assets. `UrchinSpikeActionExecutor._chargeStartTime` is
per-vessel state on a per-vessel MonoBehaviour, which is where all of it belongs.

**A RELEASE discharges; a TEARDOWN does not.** `End(so)` fires the burst; `End(null)` — the vessel
swap / disable path — drops the charge silently. A vessel handed to a new pilot mid-hold must never
discharge in the previous pilot's name, which is the same rule `Initialize`'s unconditional
detach-first already encodes one level out.

**Element ownership moved with the merge.** CHARGE now owns the whole weapon — reach *and* depth —
because a charge-up mechanic on the charge element is the honest reading, and because SPACE was
needed for the track's length. `ResolveRangeScale` reads `Multiplier(Element.Charge)` and the map's
Charge entry carries the 2.5 the Space entry used to. The two L5s merged with them: **Overcharge**
is now one extra generation *and* no reach falloff — the old CHARGE-5 and SPACE-5 as two halves of
one idea, because an element may only carry one level-5 and "the cascade is overcharged" is a
single thought.

**Round 6 dialed the weapon up** ("dial up the recursive explosions") — this is the shotgun the
old build had, restored deliberately rather than archaeologically:

- **The generation limit is 4** — both assets' `generationsAtFullCharge` moved to the clamp
  ceiling, so a full-Charge cascade runs the deepest depth the pool tiers and clamp were sized
  for. `ChainReactionBudget.VolleysPerFrame` rose 4 → 6 alongside it, so a deep cascade reads
  as a rolling barrage rather than a trickle of dropped branches; the frame ceiling (≤ 6×14 =
  84 chain spikes/frame) is what makes the depth affordable, and territory conversion remains
  the primary brake.
- **The volley is the shotgun**: `Gun.FireRingBlast` — concentric rings around the aim axis,
  ring r of R at cone angle `coneHalfAngle·r/R` with `spikesPerRing·r` spikes, alternate rings
  phase-staggered so the blast fills its own gaps, plus a center spike. Fully deterministic by
  construction: no RNG draw at all, so every peer fires the identical fan. Rate limiting stays
  the executor's owed-seconds loop.
  **Round 10 moved it onto the guns** ("fire from both guns"): one fan per authored muzzle,
  each spun by `360/spikesPerRing · i / muzzleCount` about the aim axis
  (`FireRingBlast(phaseOffsetDegrees)`), so N guns *interleave* into one denser cone instead of
  drawing N copies of the same spokes — which is the whole reason the earlier hull-origin
  version existed. `spikesPerRing` is therefore authored **per muzzle** and was halved 6 → 3 in
  the same pass, so the Urchin's two guns still throw ~38 spikes per pull rather than 74 into a
  pool sized for 160. A vessel that grows a third gun gets a denser blast, which is the honest
  reading of mounting another gun.
- **The barrage is dense**: the ship's own omni burst fires a `sphericalPoints` override
  regardless of depth (previously `2·(energy+3)` — at depth 0 that was FOUR tetrahedral
  spikes). Post-merge that override is the CHARGE count (`chargedSpikesAtMin`→`Max`, 6→36);
  `barrageSpikeCount` (36) survives as the field a non-charged Spherical ability would use. Chain children keep the energy-derived counts, so a cascade's population stays
  bounded by depth. The hull historically carried **18 authored ShootPoint port objects**
  (recovered from the 2023 prefab: left-half + midline positions on a ~0.2-radius model
  sphere, mirrored ≈36 across the ship — e.g. `(0, 0.2, -0.02)`, `(-0.17, 0.07, 0.08)`,
  `(-0.08, -0.18, -0.02)`…); the golden spiral supersedes them with an even sphere and no
  gaps, which is the "we can do better now" half of the request.

The omni burst stays free; what it pays instead is spread and TIME — 36 spikes over the whole
sphere reach far less mass each than 37 down a 9° cone, and a full one costs 2.5 s of held
trigger. Both shots chain from rest and both reach the ceiling at full Charge.

**Both shots' first spike costs no generation — the asymmetry that silenced the barrage
is fixed.** `FireSingle` (the ring volley's path) stamps `energy` unchanged, but `FireSpherical`
decremented before spawning, so the omni barrage came out one tier shallower than the ring volley
from the same authored number. At the barrage's shipped resting depth that meant its spikes landed
**terminal** and it never chain-reacted at all — the reported "the one that shoots in all
directions is not chain reacting".

The decrement is still exactly right for a chain HOP (it *is* the depth ladder), so it is now
conditioned rather than removed: `if (pointsOverride <= 0) energy--`. The ship's own volley is the
only call that authors a point count, and a hop never does, so the override is the honest
discriminator and no new argument was needed. With that, `generations` means the same thing for
both shots — which is now literally true, since they are one ability reading one authored pair.

Every element read is **live, per volley** — a crystal collected mid-hold changes the very next
spike. Reach (`ResolveRangeScale` → `Multiplier(Element.Charge)`) and its per-generation decay
(`ResolveRangeFalloff`) are stamped onto the **gun**, which stamps them onto each projectile;
each spike then hands them to its own `LoadedGun`. That is how the pilot's SPACE level reaches
the last generation of a cascade that may outlive the pilot who started it — by then they may be
dead, respawned, or on the far side of the cell, so nothing may look back up at the vessel.

Both L5 upgrades gate on `R_VesselElementalAbilityHandler.IsUpgradeActive(element)` — the
**replicated** unlock bit — never on a raw local level read. Both change the prismscape, and a
local read desyncs it.

The fire loop pays off **seconds owed** in whole volleys rather than running a
`Delay(interval)` loop, which quantizes to whole frames and silently makes the authored rate
`min(rate, framerate)` — a 30 fps client would fire at half the rate of a 60 fps one. Debt past
`MaxVolleysPerTick` (4) is dropped rather than carried, for the same reason brake 3 drops rather
than queues.

**Running dry does not end the hold.** `FireOnce` returning false breaks the volley and leaves the
loop running, so fire resumes on its own as the ammo meter recovers. Ending the loop there (which
it did briefly) forces the pilot to release and re-press to resume — a gun that reads as *jammed*
rather than *empty*, which is exactly wrong for a vessel whose ammo refills while it flies and
refills twice as fast while it rides a trail. A **destroyed gun** is fatal to the hold and is
checked once per tick, so the two cases are not conflated into one early return.

## Files

| Role | File |
|---|---|
| Embed effect (halt + dwell + fade + pool return) | `ImpactEffects/EffectsSO/Projectile Prism Effects/ProjectileEmbedPrismEffectSO.cs` → `_SO_Assets/Effects/Projectile Prism Effects/ProjectileEmbedPrismEffect.asset` |
| Steal effect (pre-existing) | `.../ProjectileStealPrismEffectSO.cs` → `.../ProjectileStealPrismEffect.asset` |
| Chain-fire effect (the engine) | `.../ProjectileChainFirePrismEffectSO.cs` → `.../ProjectileChainFirePrismEffect.asset` |
| Effect container (**order is load-bearing**) | `_SO_Assets/Effects/Effect Containers/Projectile Containers/UrchinSpikeProjectileImpactContainer.asset` — `[Embed, Steal, ChainFire]` + `DetonateSparrowProjectileEndEffect` |
| Depth / reach carried by the round | `Controller/Projectiles/Projectile.cs` — `ChainGeneration`, `ChainRangeScale`, `ChainRangeFalloff`, `EmbedAndRetire`, `ApplySpikeAppearance`, `SetSpikeOpacity` |
| The gun a spike carries | `Controller/Projectiles/LoadedGun.cs` |
| Volley stamping, factory hand-down, determinism | `Controller/Projectiles/Gun.cs` — `ChainRangeScale`/`ChainRangeFalloff`, `SetProjectileFactory`, `DeterministicOrientation`, `Scramble` |
| Frame brake | `Controller/Projectiles/ChainReactionBudget.cs` |
| Ability config (one asset, both shots) | `R_VesselActions/Data Containers/UrchinSpikeActionSO.cs` → `_SO_Assets/VesselActions/Urchin/UrchinSpikeAction.asset` (`UrchinSpikeVolleyAction` / `UrchinSpikeBarrageAction` retired 2026-08-18) |
| Executor (all per-vessel state) | `R_VesselActions/Executors/UrchinSpikeActionExecutor.cs` |
| Element map | `Assets/Resources/ElementalAbilityMaps/Urchin.asset` |
| Spike prefabs (pool tiers) — **the live ones** | `_Prefabs/Projectile/UrchinSpikeProjectile.prefab` (`energy 0`, speed 40) · `UrchinSpikeProjectileEnergized.prefab` (1) · `UrchinSpikeProjectileSuperEnergized.prefab` (2). Fully wired: trigger SphereCollider (r 0.25) + `Rigidbody` + `Projectile` + `ProjectileImpactor` → `UrchinSpikeProjectileImpactContainer` + `ImpactCollider` + `LoadedGun`. |
| Spike prefabs — **the 2023 originals, historical only** | `_Prefabs/Environment/SpikeProjectile.prefab` (`energy 0`, speed 40) · `EnergizedSpikeProjectile.prefab` (1, 60) · `SuperEnergizedSpikeProjectile.prefab` (2, 80) · `RecursiveSpikeProjectile.prefab` (0, 40). No collider, no impactor; three still carry the orphaned `[Stop, Steal, Fire]` YAML. Nothing points at them. |
| Spike pools (per-vessel) | `_Prefabs/Spacevessels/Urchin.prefab` — a `ProjectileFactory` + three `ProjectilePoolManager`s, `maxSize` 400 / 160 / 48 |
| Asset generator (idempotent, key-validating) | `Tools/Build/author_urchin_assets.py` |

## Round 21: "spikes stick but nothing steals or chains" — the abortable effect chain

The playtest read "spikes are just getting stuck in everything without stealing anything or
beginning their chain reaction." The container order is `[Embed, Steal, ChainFire]` and the
dispatch loop had NO per-effect isolation, so any throw inside Steal killed ChainFire for that
contact — and the throw was *upstream of the domain flip*, so the steal itself never landed
either. Embed runs first, which is why the spikes still visibly stuck. Three repairs, one
diagnosis aid:

1. **Two prism prefabs could not be stolen at all**: `TrailRing.prefab` (the Squirrel's crystal
   rings — 2 of its 3 `PrismTeamManager`s) and `GreenDartBlock.prefab` had `onPrismStolen`
   and/or `_themeManagerData` slots authored `{fileID: 0}`. Under the fail-loud SOAP policy
   there is no null guard, so `PrismTeamManager.Steal` threw at the Raise on every hit. Wired
   (9 slots total) to the same `EventOnPrismStolen` / `ThemeManagerDataContainer` assets every
   sibling prefab wires.
2. **`Steal` now flips FIRST and reports SECOND.** The payload (AttackerName = previous owner)
   is captured before `ChangeTeam`, but the `onPrismStolen.Raise` moved after it: reporting
   must never be able to veto gameplay. A broken listener now costs the stat line, not the
   steal or the cascade.
3. **`ProjectileImpactor` dispatch loops run each effect isolated**
   (`ImpactorBase.RunEffectIsolated`): a throwing effect is reported ONCE per (effect,
   impactor type) with its stack — loud, named — and the rest of the list still runs. The
   companion of `IsEffectSlotEmpty`, same doctrine. If anything else in the field is killing
   the chain, the next playtest's console now names it instead of going silent.
4. **Muzzle-fired rounds were 1.75× oversized** (round 19 regression): `FireSingle` set
   `localScale` under the muzzle's parent chain, and the Urchin's guns carry scale 1.75, which
   multiplied into the spike's size, collider AND sweep radius. The scale is now authored in
   WORLD terms — the container's lossy scale is divided back out, so a hull-origin fire point
   (scale 1) is unchanged and a scaled muzzle no longer leaks into the round.

## Tuning knobs

| Knob | Where | Value |
|---|---|---|
| `dwellSeconds` / `fadeSeconds` | `ProjectileEmbedPrismEffect.asset` | **3.75** / 0.35 — pure look; the steal and the volley have both already happened, so this is free to be long. Tripled from 1.25 because a prism bristling with embedded spikes is worth looking at. `fadeSeconds` must stay above 0 (continuity of existence). |
| `requireDomainChange` | `ProjectileChainFirePrismEffect.asset` | 1 — off makes shielded mass cost the cascade a generation for no territory |
| `VolleysPerFrame` | `ChainReactionBudget` (static, code) | **6**, global across all cascades (round 6: raised from 4 with the depth). At depth 4 a chain volley is up to 14 spikes, so one frame's chain contribution is bounded at **84** live trigger colliders. Raise for reach, lower for frame cost. |
| `generationsAtRestingCharge` / `generationsAtFullCharge` | `UrchinSpikeAction.asset` | **1 → 4** (round 6: "their generation limit should be 4"), shared by both shots. `GenerationsForLevel` is linear in level, anchored at 0 and 10, extrapolated across the element system's `[-5, 15]` band, then clamped to **[0, 4]** — the range the pool tiers and the frame budget are sized for. |
| `chargedSpikesAtMin` / `chargedSpikesAtMax` | `UrchinSpikeAction.asset` | **6 → 36** across the charge window. 36 is the gapless golden-spiral sphere the old free barrage fired; 6 is a barely-held trigger. |
| `minChargeSeconds` / `maxChargeSeconds` | `UrchinSpikeAction.asset` | **0.35 → 2.5** s. The minimum must stay above a human's fastest deliberate tap or every shot ends in a burst; the maximum is also the weapon's slowest honest cadence. |
| `chargedAmmoCost` / `chargedProjectileSpeed` | `UrchinSpikeAction.asset` | **0** (free — the hold is the price) / **40** (an omni burst is a net, not a shot) |
| `barrageSpikeCount` | `UrchinSpikeAction.asset` (Spherical only) | **36** — used only if a Spherical ability fires WITHOUT a charge; the charged release overrides it with the hold-derived count |
| `ringCount` / `spikesPerRing` / `coneHalfAngleDegrees` / `centerSpike` | both spike action assets (ConcentricRings only) | **3 / 3 / 9° / on** → 19 per muzzle × 2 muzzles = **38** spikes per pull. `spikesPerRing` is **per muzzle**. Cone tightened 25° → 15° (round 7) → 9° (round 10, "tighter spread"); `spikesPerRing` halved 6 → 3 in round 10 so moving the blast onto both guns did not double the live-spike count. |

**Spikes always inherit the vessel's live velocity** (round 7). The executor briefly passed
`inheritedVelocity` only while attached to a trail, so every free-flight volley fired as if from
a standing gun — at cruise speed the vessel outran its own shotgun's lateral spikes and the
blast read as dropping behind the ship. `FireSingle` composes
`Velocity = direction × speed + inherited`, so the fan now travels WITH the vessel and the rings
hold their shape relative to the pilot.
| `generationRangeFalloff` | `UrchinSpikeAction.asset` | 0.75. Clamped `[0.05, 1]` by `SetChainRangeFalloff`. 1 = the CHARGE-5 upgrade. |
| `chainsOnChargeUpgrade` | `UrchinSpikeAction.asset` | 1 — the CHARGE-5 extra generation (its other half is the falloff override above) |
| `ammoCost` / `firingRate` | the two spike action assets | 0.15 @ 3/s · 0 @ 1/s |
| `projectileSpeed` / `chargedProjectileSpeed` × CHARGE `MultiplierAtFullLevel` | asset + `Urchin.asset` map | 60 / 40 × (0.4 … **2.5**) |
| CHARGE `MultiplierAtFullLevel` | `Urchin.asset` map | 2.0 / min 0.4 — the generic multiplier; depth itself comes off `GetLevel(Element.Charge)`, not this |
| `MaxVolleysPerTick` | `UrchinSpikeActionExecutor` (const) | 4 |
| `sideLength` | each spike prefab's `LoadedGun` | 2 — how far off the origin each child of a volley is spawned |

## Collider budget

**One pooled trigger collider per live spike, and nothing else.** A spike adds no per-frame CPU:
it is a pooled `Projectile` on layer 12 whose flight is one async mover, its embed is one
scheduled retirement, and its fade is a `MaterialPropertyBlock` write. The prism side of the
impact runs through the existing effect dispatch — no new spatial query, no `Physics.OverlapSphere`,
nothing registered with `PrismSpatialIndex`.

The cascade's fan-out is the whole of the budget question. A spike at depth `g` fires
`2 * (g + 3)` children at depth `g - 1`:

| Depth `G` of the landed spike | Its volley | Then | Then | Worst-case descendants from **one** landed spike |
|---|---|---|---|---|
| 1 | 8 | — | — | 8 |
| 2 | 10 | 8 each | — | 90 |
| 3 | 12 | 10 each | 8 each | 1,092 |
| 4 (clamp ceiling) | 14 | 12 each | 10 each → 8 each | 15,302 |

Those are **worst cases in the sense of "every child lands on hostile mass"**, which brake 1
makes nearly unreachable: each generation converts what it hits, so the next generation is fired
into ground that already wears its own domain.

**The shipped ceiling is depth 4** (round 6 — the design call "dial up the recursive
explosions. their generation limit should be 4" supersedes the depth-2 conservatism this section
originally argued for; the earlier reasoning is kept below because it is why the OTHER limits
exist and are load-bearing). A depth-4 seeded hit is 15,302 spikes in the *theoretical* worst
case, which no frame ever pays: the total is spread across frames by brake 3, capped in
concurrency by the pools, and collapsed in practice by brake 1 — each generation converts what
it hits, so the next fires into friendly ground. What depth 4 actually buys is a LONGER rolling
cascade, not a bigger instantaneous one.

The real bound on *concurrent* colliders is the product of three independent limits, and it is
worth knowing which one is doing the work when the cascade feels wrong:

- **Brake 3** caps volleys at **6/frame** globally (raised from 4 with the depth change). At
  depth 4 a chain volley is up to 14 spikes, so one frame's chain contribution is bounded at
  **84** colliders across every Urchin in the match.
- **Pool depth** caps live spikes outright (`GenericPoolManager.maxSize` on each tier's
  `ProjectilePoolManager`). A cascade that exhausts a tier stops on a factory miss, which is a
  *hard* stop and not a graceful one.
- **`projectileTime` 2 s** is how long each collider lives.

The Urchin's spike pools are authored **on the vessel prefab**, not on the scene: a
`ProjectileFactory` with `maxSize` **400 / 160 / 48** across the Normal / Energized /
SuperEnergized tiers. That is the hard ceiling on live spike colliders — per vessel, so a
four-Urchin match multiplies it. Sizing should be checked against the depth-2 row above once the
cascade is observable, rather than against the depth-1 case a first playtest produces.

## In-editor verification

Nothing below can be checked without play mode; the depth curve is a pure function
(`UrchinSpikeActionSO.GenerationsForLevel`) and the rest is feel plus wiring.

1. **Project compiles with zero errors**, and `python3 Tools/Build/author_urchin_assets.py --check`
   passes (it validates every YAML key against the serialized fields of the class each asset's
   `m_Script` points at — a key Unity does not recognise is silently dropped and the field reads
   its initializer forever).
2. **Open the three spike prefabs and confirm they import clean.** They were authored as YAML
   outside the editor: `_Prefabs/Projectile/UrchinSpikeProjectile{,Energized,SuperEnergized}.prefab`,
   each a trigger `SphereCollider` (r 0.25) + `Rigidbody` + `Projectile` + `ProjectileImpactor`
   (container pre-wired) + `ImpactCollider` + `LoadedGun`. A missing script or an unassigned
   container is what a hand-written prefab gets wrong, and a spike with no impactor passes
   through everything silently.
   The four **2023** prefabs under `_Prefabs/Environment/` are superseded and unreferenced; three
   still carry the orphan keys (`trailBlockImpactEffects: 0c000000070000000d000000` =
   `[Stop, Steal, Fire]`, plus `Team`, `Ship`, `Velocity`, `ProjectileTime`) that the script no
   longer declares. Leave or delete them, but do not wire them.
3. **Sanity-check the pool ceilings.** `Urchin.prefab` carries its own `ProjectileFactory` with
   three `ProjectilePoolManager`s — Normal → `UrchinSpikeProjectile` (capacity 120, **max 400**),
   Energized (40 / **160**), SuperEnergized (12 / **48**). `maxSize` is the real ceiling on live
   spike colliders. It is a **per-vessel** factory, so four Urchins multiply those numbers by
   four; a drained pool is a hard stop (a factory miss), which is the deliberate trade against an
   unbounded collider leak.
4. **Wire the vessel.** `Urchin.prefab` has `R_VesselActionHandler._executors: {fileID: 0}` and
   `_inputEventShipActions: []` — add an `ActionExecutorRegistry` with
   `UrchinSpikeActionExecutor` (assign its `gun`, `muzzles`, `barrageOrigin`) and bind
   `RightStickAction(1)` → `UrchinSpikeAction.asset`, `LeftStickAction(2)` →
   `UrchinTrackAction.asset` (see `URCHIN_TRACK_PROJECTOR.md`).
5. **Give the Urchin an ammo resource.** `ResourceSystem.Resources` on `Urchin.prefab` is `[]`,
   and both `ammoIndex` fields are 0. The volley (`ammoCost` 0.15) will refuse to fire with
   `Invalid ammo index or ResourceSystem`, and `GunVesselTransformer.SlideActions` will throw an
   `ArgumentOutOfRangeException` **every frame of a ride**.
6. **One spike, one steal.** Menu_Main freestyle or any Urchin-playable mode. Fire the volley at
   an enemy trail at Charge 0 (depth 1): the spike stops in the prism, the prism changes domain,
   a burst of children sprays out of it (**8** at depth 1; **14** at the shipped full-Charge depth 4), and the original spike fades out ~**3.75** s later
   instead of popping or standing there forever.
7. **The cascade dies by eating its frontier.** Fire the same volley into a trail that is
   *already yours*: nothing should happen at all beyond the spike stopping — no steal, no
   children. Then fire into open space: the spike expires at 2 s and returns to its pool.
8. **Depth scales with CHARGE.** Collect Charge crystals to level 10 and repeat step 6 — the
   first landing should spray **10**, and each of those should spray again. If it still sprays 8,
   the level read is not reaching `ResolveGenerations`.
9. **Reach scales with CHARGE, and decays.** At Charge 0 the spikes should visibly cover less
   ground than at Charge 10 (muzzle speed × 0.4 vs × 2.5), and within one cascade each generation
   should reach shorter than the last (falloff 0.75). At Charge 5 ("Overcharge") the last
   generation should reach as far as the first AND the cascade should run one tier deeper.
10. **Shielded mass costs no generation.** Shield a prism (Rhino slab or a Squirrel Heavy Trail
    prism) and hit it with a spike: the shield sheds, the prism stays hostile, and **no** children
    are fired. Against a super-shielded prism, nothing at all happens.
11. **Pool integrity — the 2023 regression.** Fire ~50 volleys into dense enemy mass, then keep
    firing. The gun must not go quiet. `No pool registered` or a silent stop means the pool has
    been drained: check that a spike which HIT something returned (step 6's fade) and that a
    spike which MISSED returned (`projectileEndEffects` on the container).
12. **Frame brake reports itself.** Drive a deep cascade into a wall of enemy trail and watch the
    console for `[ChainReactionBudget] Shed a chain volley - ceiling is 6/frame`. Seeing it is
    correct behaviour, not a bug — it is the message that tells you whether a short cascade was
    the design (brake 1) or the budget (brake 3).
13. **Domain paint.** Fire as Jade, die/respawn or swap domain at the domain-changer toy, fire
    again: the spikes must wear the **new** colour. A spike wearing the previous domain's material
    means the paint has drifted back to `Start`.
14. **A TAP is one blast.** Pull and release the right trigger quickly (under 0.35 s). Exactly one
    aimed fan leaves the muzzles and **nothing** fires on the release. Holding the trigger down
    must NOT auto-repeat the fan — `repeatWhileHeld` is authored off.
15. **A HOLD is a blast plus a burst.** Press and hold ~1 s, then let go: the aimed fan fires on
    the press, and an omni sphere of roughly 6–36 spikes leaves the hull on the release. Hold a
    full 2.5 s and count the release — it should read as the same dense sphere the old free
    barrage threw. Ammo is spent only by the press.
16. **The charge cannot survive the vessel.** Hold the trigger and, while still holding, swap
    vessels at the vessel-changer toy (or end the turn). **No** burst may fire. Then press and
    release normally on the new hull — the charge must work from scratch.

### MPPM — two clients

17. **The cascade agrees.** Host and client both watch the same Urchin fire a depth-2+ volley into
    the same trail. The spray pattern and the resulting converted prisms must match on both
    screens. A visibly different fan is `Random.rotation` creeping back into `FireSpherical`.
18. **The unlock bits replicate.** Take the *client's* Urchin to Charge 5 and fire a deep
    cascade; the host must see the same non-decaying reach, the same extra generation, and the
    same converted prisms — with both the tap and the charged burst. A peer that sees a shallower
    or shorter cascade means an L5 gate is reading a local level instead of `IsUpgradeActive`.
19. **A client's steals score.** With the client's Urchin, convert ~20 prisms and check the
    client's own `PrismStolen` / `VolumeStolen` on the scoreboard. Before
    `Player.ReportPrismStolen_ServerRpc` this was **zero** — `StatsManager.PrismStolen` opened
    with `if (!_allowRecord) return;` and `_allowRecord` is false on clients, so a client's steals
    scored nothing at all, for every steal source in the game. Note the **victim's**
    remaining-mass tally still drifts on a client-side steal: only the stealer's half travels,
    because identity on the far side comes from RPC ownership and debiting the victim would mean
    trusting a client-supplied name.

## Follow-ups

- **The AMMO RESOURCE is the blocking item** (verification step 5). `ResourceSystem.Resources` on
  `Urchin.prefab` is still `[]` while every `ammoIndex` is 0, so the aimed volley refuses to fire
  and a ride throws once per frame. Spike prefabs, pools, executors and containers all landed on
  `b3bc963bc`; this did not.
- **Netcode components are not wired.** `Urchin.prefab` has no `NetcodeHooks`,
  `NetworkVesselClientCache`, `NetworkVesselImpactor` or `ClientNetworkTransform`, so multiplayer
  spawn is a separate pass and none of the MPPM verification below can run yet.
- **The four 2023 spike prefabs under `_Prefabs/Environment/` are superseded** by the three under
  `_Prefabs/Projectile/` and referenced by nothing. `RecursiveSpikeProjectile.prefab` in particular
  is an artefact of the same-tier recursion loop with no tier in `ProjectileFactory`. Delete them,
  or keep them explicitly as the historical record this document cites.
- **Edit-mode coverage exists and is narrow.** `_Scripts/Tests/Editor/UrchinChainReactionTests.cs`
  pins the depth curve (`GenerationsForLevel` — extrapolation across `[-5, 15]`, the `[0, 4]`
  clamp, and zero staying REACHABLE because zero is what terminates a cascade), the Slip ghost
  window's non-negativity, and `Gun.DeterministicOrientation`'s repeatability / sub-quantum
  tolerance / variation with origin and depth. `DeterministicOrientation` was widened from
  `internal` to `public` for it, because tests compile into `Assembly-CSharp-Editor` and cannot
  see `Assembly-CSharp` internals. Nothing yet covers the effect trio or the budget.
- **`ChainReactionBudget.VolleysPerFrame` is a public static field, not a config SO.** That is a
  deliberate shortcut for a number nobody has tuned yet; if it survives a balance pass it belongs
  in a ScriptableObject like every other tuning value.
- **`StatsManager.CreditPrismSteal` is only called by the RPC.** Its own docstring says it was
  split out so the client round-trip and the server's local detection "share one accounting rule
  instead of two copies that drift" — but `PrismStolen`'s server branch still hand-rolls the same
  four lines. Point it at the helper.
- **The steal-scoring gap is not recorded in `Docs/ScoringSystem/BUGS.md`.** The `Player.cs`
  docstring says the victim-tally trade "is recorded" there; it is not. Add the entry, because the
  gap predates the Urchin and the trade is the kind of thing that gets re-litigated.
- **Audio.** No spike sound is authored. Per the FMOD convention, the embed, the steal and the
  chain-fire each want their own inspector-exposed `EventReference` on the component that makes the
  noise — shipped **empty**, never pointed at a borrowed event.
- **No AI path.** `AIPilot` has no notion of the tap, the charge, or the track, so an AI Urchin flies but
  does not shoot. The abilities run through the standard executor registry, so this is binding
  work rather than new mechanics.
