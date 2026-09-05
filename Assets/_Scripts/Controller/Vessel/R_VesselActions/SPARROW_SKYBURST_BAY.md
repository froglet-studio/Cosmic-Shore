# Sparrow Skyburst Missile Bay — bay-open animation + bay-anchored launch

The Sparrow's model has always carried two missiles in a ventral bay (`b_Missile.L` /
`b_Missile.R` on the rig, with `b_Shell*` / `b_SlideChip*` bay doors), and a pair of
bay-open/launch animations was authored for it long ago — but none of it ever shipped on
trunk: the vessel ran the shared `MantaAnimationContoller`, the launch clips lived only on
the abandoned `claude/sparrow-missile-launch-animation-xHuCS` branch, and the skyburst
projectile rendered a stellated wedge polyhedron from `Vessel_Wedge_Scene (4).fbx`, spawned
from a `Gun Point` transform floating 9.9 u ahead of the vessel origin.

This change makes the skyburst fire **from the bay, as the bay animation ejects it, with the
missile model as the projectile**:

1. **Press** → ammo is deducted and `FireGunActionExecutor.OnMissileFired(bool usedRightBay)`
   fires immediately. `SparrowAnimationController` plays the matching bay clip on the additive
   **Missile Launching** animator layer (weight 1 for the clip, back to 0 when it completes).
2. **Bay opens** (~0.16 s at the authored 2.5× state speed), the animated bay missile ejects.
3. **Spawn** — after `SkyBurstGunAction.launchDelaySeconds` (0.2 s) the live projectile spawns
   at the **live bay bone's position** (aim/rotation from the gun, so flight matches course),
   carrying the extracted `Sparrow Missile.fbx` mesh. The animated missile returns into the
   closed bay (reads as the next round loading).

## Which bay is which (measured from the FBX takes, not assumed)

| Clip | Take length | Animates | Ejection window (raw take time) |
|---|---|---|---|
| `Missile Launch 1` | 0.88 s (played at 2.5×) | `b_Missile.R` — RIGHT bay | departs 0.4 s, peak 0.64 s |
| `Missile Launch 2` | 0.88 s (played at 2.5×) | `b_Missile.L` — LEFT bay | departs 0.4 s, peak 0.64 s |

Side selection lives in ONE place — `FireGunActionExecutor.Fire`:
`ammoBefore >= 2 × ammoCost` → right bay (first missile of a pair), else left. The animation
and the spawn both consume that event parameter, so they can never disagree.

## The donor-clip arrangement (why SparrowModel4.fbx is in the repo)

`SparrowModel1.fbx` (the shipped vessel model) has no launch takes. `SparrowModel4.fbx`
(imported from the old branch, guid `ebe998f747168104ca3e85b7295e47be`) carries the two
"Missile Launch" takes on a **bone-for-bone identical rig** (verified: identical bone list,
same `Sparrow_Armature+Mesh` root, and matching numeric scale — model1 imports at FBX-unit
100 × globalScale 1, model4 at FBX-unit 1 × globalScale 100, so curve translations land 1:1).
`SparrowAnimatorController.controller` references model4's clips by internalID
(`2132225776441424335` / `3552259950087936227`); Unity binds clip curves by transform path,
and every bone path resolves on model1's hierarchy. Curves targeting model4-only mesh nodes
(`Cube.113` blend-shape channels, `a_Body`) simply do not bind — and even a binding
blend-shape curve could not fight the element hull morphs, because `VesselAnimation.LateUpdate`
is the single writer of blend-shape weights (the documented defense).

**Model4 is an animation donor only. Do not wire it as a visible model** — its mesh object
names differ from model1's, so the prefab's `SkinnedMeshRenderer` references, the corridor
hull measurement, and `VesselCustomization` are all authored against model1.

## The missile grows as it travels (MASS)

The skyburst swells over its flight using the same machinery the Sparrow's bullets do — one
curve (`ElementalScaling.RoundGrowthFactorForLevel`, linear in the integer Mass level,
extrapolated across the whole `[-5, 15]` band) with its own authored endpoint pair on
`SkyBurstGunAction.asset`. **MASS owns the SUBSTANCE of what you fire**, so every round the
vessel launches grows with it; this is that one parameter reaching the third thing the Sparrow
fires, not a second Mass knob. CHARGE still owns the blast radius — different quantity,
different element.

**It swells EARLY and then holds.** `Projectile.flightGrowthCompleteAt01` is **0.2**: the
missile reaches full size in the first fifth of its flight — **0.6 s, about 70 u from the bay** —
and flies the remaining 80% at that size. That is the opposite shape from the bullets, which are
still growing when they arrive, and it is the point: a tracer's size reports how far it has come,
whereas a missile should read as a fixed object you are watching cross the arena. Growth is also
**uniform** (`flightGrowthUniform`), unlike the cross-section-only rule the hit-volume path uses — that rule
exists because the tracer mesh is a 20-long dart whose length is a *streak*, and the missile is a
compact round whose length is the round.

Once the swell finishes the round stops re-writing its transform at all (`RoundGrowthRamp.IsComplete`
latches it), so holding for 80% of the flight costs nothing.

| Mass level | −5 | 0 (rest) | 5 | 10 | 15 |
|---|---|---|---|---|---|
| Growth factor | 14× | **20×** | 26× | **32×** | 38× |
| Missile length | 23.2 u | 33.2 u | 43.1 u | 53.1 u | 63.0 u |
| Missile girth | 5.3 u | 7.6 u | 9.9 u | 12.2 u | 14.5 u |
| Nose past the hit sphere, per end | 3.1 u | 8.1 u | 13.1 u | 18.0 u | 23.0 u |

To take Mass out of it and fly one fixed size, author both endpoints equal.

**The model IS the hit volume — the other way of satisfying the same law.** Since PR #786 the
platform rule is *MASS in-flight growth is a HIT VOLUME, not a size*: a round's model stays the
size it left the muzzle and a see-through `chargeField` shell, sized every frame to the swept hit
radius, is what carries the read. That is right for the bullets, whose model is a 20-long tracer
**streak** — a smear, not a body; growing it drew a cannonball.

The skyburst has a readable **body** worth growing, and no growing hit volume for a shell to draw.
So it satisfies the same law from the other end: **the model grows and the sphere collider is
fitted to it, every frame.** `Projectile.flightGrowthTarget` selects that path, and
`ModelHitRadius` / `ModelHitCentre` do the fit:

- **Radius = the model at its WIDEST across the flight axis.** For a body of revolution that is
  its radius — never the bounding-box *diagonal*, which would overstate a round missile by √2.
- **The sphere's FRONT surface sits exactly on the model's tip.** That is the contract, and the
  reason the tail is free to trail: a model may stick out the **back** of its collider (a tail
  that has already passed you cannot cause a false read) but never out the **front**, where the
  nose would visibly reach a target before the hit registered.

Both are linear in growth, because the model scales about the root origin — so the fit is one
multiply, and the settle latch stops it entirely once the size is final.

| Mass level | −5 (14×) | **0 (20×)** | 5 (26×) | 10 (32×) | 15 (38×) |
|---|---|---|---|---|---|
| Hit radius | 2.67 u | **3.81 u** | 4.95 u | 6.10 u | 7.24 u |
| Model length | 23.2 u | 33.2 u | 43.1 u | 53.1 u | 63.0 u |
| Tail behind the sphere | 17.9 u | 25.6 u | 33.2 u | 40.9 u | 48.6 u |

Nothing hardcodes the missile: the fit is measured per flight from the growth target's own
renderer bounds through the renderer→root matrix, so it is correct for any nesting, rotation or
child scale.

### What this costs, stated plainly

**The missile's reach dropped.** It used to hit with a fixed **8.5 u** sphere; at resting Mass it
now hits with **3.81 u** — 45%. That is a deliberate Dog Fight balance change (a missile hit is 50
points), made because the 8.5 was never authored: it is `0.85 × ProjectileScale 10` arithmetic
that happened to dwarf the 1.7 u model nobody could see. `SparrowRoundGrowthTests` pins 3.81 so a
future change has to argue with the number rather than drift past it.

Three consequences worth knowing:

- **Reach now varies with MASS**, from 2.67 u to 7.24 u. It did not before. Mass buying reach is
  consistent with *MASS owns the substance of what you fire*, but it is new for this weapon.
- **Reach is small for the first fifth of the flight** (0.19 u at the muzzle, rising to full at
  20%). The missile is leaving the bay through that window, so it will not detonate on something
  it brushes past the hull — arguably a feature, but it is a change.
- **The forward overhang is gone**; what remains is up to 48 u of tail behind the sphere, which is
  the permitted direction.

## It has a TAIL, and that word is precise

The missile carries the fleet's **tail** — the long streak whose whole job is legibility at range
(`Docs/VESSEL_TAIL_AND_JETS.md` §4.2). Not a *jet* (a short plume on an engine node, tuned for the
pilot flying that engine) and not a *trail* (conserved prism mass, which this is emphatically not:
the tail carries no collider, no state, and destroying it destroys nothing). It is the first
non-vessel in the game to have one, and it earns it — the round crosses ~360 u in three seconds and
a hit is worth 50 points in Dog Fight, so everyone in the arena has a reason to see it coming and to
know whose it is.

It is the **shared `VesselTail.prefab`**, nested — not a copy — so a retune of the tail's look
reaches the missile with no second edit. What is missile-specific is decided in code, per flight,
off the measurement `CaptureModelHitSphere` already takes for the hit sphere:

| | value | where |
|---|---|---|
| mount | the model's measured **rear face**, scaled by growth (the mirror of the nose fit) | `Projectile.TailMount` |
| width | `0.4 × the round's own body diameter` — 3.05 u at resting Mass | `Projectile.TailWidth` |
| colour | the firing pilot's **domain**, re-read per flight | `Projectile.PaintTail` → `TailGradient` |
| lifetime | the shared prefab's 4 s | `VesselTail.prefab` |

Deriving the width is not optional: the round swells **14×–38×** with MASS and a `TrailRenderer`'s
width is world-space, so a fixed ribbon would be a thread behind a 63 u missile. Because both
numbers come out of the same measurement the collider is fitted to, the tail and the hit volume can
never disagree about how big the round is.

Two things a pooled round needs that a vessel does not, both in `Projectile`:

- **`ReclaimTail`** clears the ribbon at launch. A `TrailRenderer` records world-space points, so
  without it every reissue draws one straight line from wherever the last missile detonated to this
  one's bay.
- **`ReleaseTailToFade`** cuts the tail loose at retirement, 0.025 s after detonation, so several
  hundred units of ribbon fade out where they were laid instead of blinking out with the round.
  That is continuity of existence, not polish. It comes home when the fade ends or when the round
  is fired again, whichever is first — a 20-deep pool cycles faster than a 4 s ribbon.

`SparrowRoundGrowthTests` pins the mount sign, the rear-face fit, the width curve and the 0.4.

## The missile is stocked by DESTROYING MASS, not by crystals (2026-09)

The skyburst used to be crystal-stocked: the tank held two rockets, never regenerated, and the
only refuel was flying into an omni crystal
(`SparrowVesselChangeResourceByCrystalEffect`, which set it full). **That effect is retired.**
Missiles are now bought by taking the arena apart — every HOSTILE prism this pilot destroys puts
`ammoPerPrism` (0.02) back in the rack, so 25 prisms buy a rocket and 50 refill it.

`VesselRearmOnPrismDestruction` on the vessel root does it, and **it listens on the prism-destroyed
SOAP channel rather than hanging off an impact effect.** That is not a stylistic choice — a Sparrow
destroys prisms five ways (full-auto bullets, turret prism rounds, a missile's direct hit, a
missile's BLAST, a hull ram), and the blast is both the biggest source and the one an effect
structurally cannot see: while the spatial index is up, `ExplosionImpactor` resolves prism damage
through the Burst batch path (`PrismSpatialIndex.ProcessExplosionFrame`), which dispatches **no**
per-prism effects at all. An `ExplosionPrismEffectSO` wired for this would run only on the Physics
fallback — almost never — and would look correct in code the whole time. `Prism` raises the
destroyed channel from ONE place on every route, so counting what actually happened notices every
producer by construction. *A rule enforced at one PRODUCER can only ever see that producer*, which
is the Scarab ball-ceiling finding reached from a different direction.

**Only hostile mass pays**, via `StatsManager.IsFriendlyEnvironmentPrism` — the platform's own
rule, so "which mass is worth something to me" has one answer. Your own trail, a teammate's trail
and environment mass wearing your colour are free of charge; `Domains.Blue` neutral mass is hostile
to everyone. Without that gate a pilot could park and reload off their own ribbon.

**THE TANK HAS TO AGREE ACROSS PEERS — and the first cut of this got that wrong.** It shipped
ungated on network ownership, justified as "a replica's copy of the tank is inert, because only the
owner fires." **That premise is false.** An ability press is replicated as a PRESS, not as a
decision: the owner sends it to the server and `R_VesselActionHandler.SendButtonPressed_ClientRpc`
replays it on EVERY peer, where `FireGunActionExecutor.Fire` reads *that peer's* own tank and returns
early if it is short. So a replica whose tank had drifted low silently spawned no missile — no model,
no tail, no proximity fuze, no warhead — and a victim on that machine took none of the debuff the
shooter's machine said landed.

Spending is convergent (every peer spends on the same replayed press). **EARNING is not**: fauna and
flora are spawned per-peer from local `Random` rolls (`CellNetworkSync` — the very reason
`Player.ReportFaunaKill_ServerRpc` exists), so two machines genuinely destroy different mass, and
destruction ordering races diverge even over identical mass. The crystal refill this replaced was
**self-healing by accident** — an unfiltered ClientRpc doing a set-to-FULL, so every peer
resynchronised on every pickup. Removing it turned a self-correcting drift into a ratcheting one.

So `VesselRearmOnPrismDestruction` is a `NetworkBehaviour`, and the OWNER publishes its tank as an
**idempotent SET**, rate-limited to `syncIntervalSeconds` (1 s) and only when the value actually
moved — the same shape `SalvoController.RefuelDomainMissiles_ClientRpc` uses, and for the same
reason. Local crediting stays, so the common case (trail mass, which IS convergent) needs no round
trip and has no latency; the broadcast is the correction, ≤1 RPC/s per Sparrow. A SET rather than a
delta because a delta has to arrive exactly once to be right, while a set converges however many
arrive, in any order, and however many were dropped. It errs deliberately toward replicas being
slightly OVER: a replica never initiates a shot, so an over-full replica is harmless while a short
one eats the missile.

*General trap: "only the owner reads this" is a claim about every reader, and a replicated INPUT
means the replicas run the same code the owner does. Check what the press replays into before
concluding a replica's state is inert.*

### The crystal changed jobs — it now grants a WARD

Freed from stocking missiles, the omni crystal grants **8 seconds of elemental-debuff immunity**
(`SparrowVesselWardByCrystalEffect` → `VesselTimedElementalWard` on the vessel root, warding
`ElementalDebuffSources.All`). Danger prisms, blasts and skimmer overtakes leave your element
levels alone for the window; buffs still land, and the ward PREVENTS new debuffs rather than
cleansing live ones. Re-collecting REFRESHES rather than stacks, so a crystal run cannot be banked
into a permanent state.

`VesselTimedElementalWard` is the **event-driven** half of the platform's debuff immunity, the
sibling of the condition-driven `VesselElementalImmunity` (which the Sparrow also carries, for its
TIME-5 boost ward). Neither can express the other: a window that opens on an event and closes on a
clock has no condition to poll. They are separate components so a caller can find the one it means,
and grants are keyed on the granting component so two wards on one hull compose.

**Checked against the mono-vessel-mode rule** (`/vessel` §4.31 — a defensive ability is a MODE-level
rule wherever its vessel is mandatory, and the comeback system hands level-5 kit to whoever is
LOSING): Dog Fight scores gunnery hits with `requireDebuffableVictim: false`, Salvo scores prisms
destroyed and Wildlife Liberation scores creatures killed. None of the three scores on an event a
debuff ward can deny, so a warded Sparrow is still fully scoreable.

## The proximity fuze, and the warhead behind it (2026-09)

**The missile goes off NEAR things now.** `Projectile.proximityFuzeRadiusMultiplier` (20 on the
skyburst, 0 — off — on every other round) detonates the round when something worth detonating on
comes within **20× its own live hit radius**.

It trips on exactly two things and nothing else:

- an **opposing VESSEL**, identified through the `ImpactCollider` its hull carries, and
- a living **FAUNA's heart**, the same `Crystal.IsEmbedded` surface the Squirrel jousts.

Never on a prism — a fuze that armed on mass would detonate the instant a rocket left a hull inside
any trail — and never on FLORA, whose hearts stand in their thousands in a seeded cell. Never on
your own domain either, vessels and creatures alike: detonating on a teammate is pure grief, and
the rule does not depend on an upgrade state that would make the fuze behave differently at
different element levels.

**The arming delay is emergent, not authored.** The fuze radius is a multiple of the round's CURRENT
hit radius, and the missile leaves the bay at 1/20th of its grown size — so a round still clearing
the hull is barely sensitive at all (3.8 u) and reaches full reach exactly when the model finishes
swelling (`flightGrowthCompleteAt01` 0.2, ~0.6 s out).

**It is an explicit overlap, not a second trigger collider**, for three reasons: a 150-unit trigger
dragged through a cell would mint thousands of PhysX pairs per frame that all get discarded (the
problem `AOEExplosion.ApplyPrismExclusion` exists to solve); a trigger would arrive through
`AcceptImpactee` and run the round's DIRECT-hit effect list, spinning a pilot and scoring a missile
strike on a near miss; and an overlap ignores the collision matrix, which is what lets one query see
both vessels (layer 8) and crystals (layer 9). Same reasoning `ExplosionImpactor.SweepCrystals`
records. A fuzed round also switches its own direct-hit collider off before detonating, so the
quarter-second the detonation parks it there cannot run a strike chain on a rocket that has already
gone off.

### Two blasts, and what each is for

**The existing explosion is untouched.** `DetonateEndEffect` still spawns `AOEConicSkyBurst` (the
radial prism rays it lays) and `AOEExplosion` (the sphere that destroys prisms, MaxScale 100–170 by
CHARGE). That is the blast aimed at the ARENA.

**`AOEMissileWarhead.prefab` is the new one, and it is aimed at LIVING things**: it debuffs every
pilot it engulfs (`MissileWarheadDebuffByExplosionEffect`, −0.5 on all four elements for 4 s — the
same numbers the Dolphin/Scarab blast ships, forked so a missile retune cannot move theirs) and
KILLS the creatures it engulfs (`MissileWarheadWitherLifeformEffect`).

It touches no prism at all. `AOEExplosion.affectsPrisms` (new, default true) is honoured in ONE
place — `ExplosionImpactor.BeginBatchProcessing` — so every explosion SHAPE respects it even though
each owns its own `ExplodeAsync`; the prefab additionally authors the TrailBlocks layer into its
trigger's Exclude Layers so the Physics fallback cannot reach one either. Note this is **not** the
same as clearing `destructive`: a non-destructive blast still reaches every prism it engulfs and
ARMOURS it (`ExecuteCommonPrismCommands`' accept branch calls `ActivateShield`), which on a 95-unit
sphere would temporarily shield half an arena.

**The fauna kill is the Squirrel's joust, reached by an explosion.** `ILifeFormEntity.Jousted`
stamps `LifeformDeathStyle.Jousted` and runs the sealed death path, so the creature does not
detonate: its heart is freed at the strike, its soft tissue unravels FROM THE HEART OUTWARD, and its
body prisms are left standing as a skeleton the food web then grazes (`Docs/ECOSYSTEM.md §26`). Mass
conserved, continuity honoured, spawn immunity respected, and the kill attributed to the firing
pilot so `ScoringMetric.LifeformsKilled` credits it.

Two deliberate differences from the vessel joust, both worth knowing:

- **Nobody takes the heart.** A jousting vessel reaches in and collects it
  (`VesselWitherLifeformByCrystalEffectSO.TakeHeart`); a blast is standing off at range and does
  not, so the heart drops as an ordinary pickup — the same end state a starvation death reaches.
  That is a balance decision as much as a fictional one: a rocket that killed a dozen creatures
  would otherwise hand its pilot a dozen elemental crystals in one frame.
- **There is no speed contest.** The vessel joust requires the pilot to be moving faster than its
  target; a blast simply reaches everything inside it.

Blast → heart is dispatched by an explicit overlap (`ExplosionImpactor.SweepLifeformHearts`) for
exactly the reason the crystal sweep is: Crystals(9) × Explosions(10) is **disabled** in the
collision matrix, so a `case` in `AcceptImpactee` would compile, read correctly and never fire.

**The overlap is TWO queries, not one, and a full buffer is a truncated result.**
`Physics.OverlapSphereNonAlloc` fills its buffer in unspecified broadphase order and silently
discards the rest, and every discriminating test the fuze makes runs AFTER the fill — so "it takes
the first qualifying hit and stops" is not a defence, because the query cannot be asked for only
qualifying hits. Layer 9 (Crystals) is dominated by things the fuze REJECTS: flora hearts (one
always-on collider per plant), own-domain creature hearts, and free crystal drops. Any of those can
fill a shared buffer and push the one opposing hull out of the result, so an armed rocket flies
through a pilot — order-dependently, with nothing logged. Splitting the query makes the VESSEL half
**exact**: a Ships-only buffer can only ever hold vessels, so with a lobby of at most a handful it
cannot truncate at all. The crowded layer gets a bigger buffer plus grow-and-retry (`OverlapGrowing`,
capped at 1024). `ExplosionImpactor.SweepLifeformHearts` carries the same treatment. *General trap: a
fixed scratch buffer is only safe when the buffer is filled by the things you are looking FOR; when
the layer is shared with things you reject, capacity is a correctness property, not a perf tuning.*

**A CORPSE IS NOT A TARGET, and `IsEmbedded` does not say so.** A creature with a progressive wither
re-homes its heart onto the cell at the TOP of its death and deliberately leaves it embedded and
uncollectable for the whole animation (`Docs/ECOSYSTEM.md` §26), so a corpse's heart keeps matching
for seconds. Both the fuze and the warhead's kill now test `ILifeFormEntity.IsDying` explicitly.
Without it a Sparrow that gunned a shark and fired through the same volume had the rocket armed and
spent by the corpse it just made — and the warhead then re-ran the sealed death: **a second
`LifeformsKilled` credit for one creature**, and, because the joust stamps the style first, the heart
freed while the wither was still eating inward, which §26 forbids ("the heart is the LAST thing
standing"). The root fix is one line in the platform: `Fauna.Predated` declines a creature that has
already died. It never had that guard — it tested `_consumedAsPrey`, which only `Predated` itself
sets, so a starvation or body-prism death walked past it — while `LifeForm.Jousted` has always
carried the equivalent `dying` check. **`Fauna` is a SIBLING of `LifeForm`, not a subclass**, so it
simply never inherited it, and `Fauna.Jousted`'s own doc comment already promised the behaviour the
guard restores. `ILifeFormEntity.IsDying` publishes what both types were gating on privately.

### The geometry, and where the two multipliers come from

Both the fuze and the warhead are multiples of the **same** base — the round's own live hit radius —
so they are one scale and can be read against each other at a glance. `SparrowMissileFuzeTests`
asserts the ORDERING (warhead ≥ fuze) rather than the values, because a proximity kill that could
not catch what tripped it is the one way this mechanic reads as broken.

> **REACH IS NOT CAPTURE, and the ordering alone quietly implies that it is.** The warhead being the
> larger radius says only that it *outreaches* the distance the fuze fired at. It does not say it
> arrives: the blast is not a sphere that exists at full size, it GROWS as
> `radius(t) = R·sin(t/D · π/2)` over its `ExplosionDuration`, and a vessel takes the debuff when the
> sphere *contains* it (a trigger enter is an overlap, not a surface crossing). So against a target
> already moving away when the fuze tripped, the sphere has to close the 25%→20% margin before it
> finishes expanding. At the originally-authored **0.5 s** that bought **~40 u/s** — below every
> vessel's cruise, and far below the missile's own ~120 u/s — so a rocket could detonate beside a
> pilot and reach nobody while the radius test passed. The warhead ships at **0.15 s** (≈130 u/s),
> which covers ordinary flight and deliberately does **not** cover a boosting escape: outrunning a
> missile you saw coming is a fair outcome, being immune to one at cruise is not.
> `TheWarheadExpandsFastEnoughToCatchOrdinaryFlight` pins it as a SPEED the geometry must cover, so
> the duration and both multipliers stay free to retune as long as the mechanic still works.
> *General trap: an assertion about two SIZES cannot establish a claim about a moving target — check
> whether the thing you sized also has to arrive somewhere in time.*

> **The warhead never affects its own domain, and that is NOT the CHARGE-5 flag the prism blasts
> take.** `ProjectileDetonatorSO` hands the prism blasts `AffectSelfOverride = !proj.SpareOwnDomain`
> — the "Domain-Safe Skybursts" snapshot — and for a blast that destroys MASS that is a real choice.
> The warhead destroys no mass at all; its whole payload is an elemental debuff on VESSELS, and
> there is no level at which a pilot should debuff themselves or a wingman. Taking the snapshot did
> exactly that: it is TRUE *below* Charge 5, `AcceptImpactee` then accepts own-domain vessels, and a
> 95-unit sphere centred at most a fuze-radius (76 u) away put the shooter reliably inside its own
> blast — at precisely the close range the fuze exists to encourage. It now passes `false`
> unconditionally. *General trap: a flag named for one decision gets reused for a second one that
> merely looks similar; ask what the flag is actually ABOUT before borrowing it.*

| MASS level | growth | hit radius | fuze radius (×20) | warhead radius (×25) |
|---|---|---|---|---|
| −5 | 14× | 2.67 u | 53.3 u | 66.7 u |
| **0 (rest)** | **20×** | **3.81 u** | **76.2 u** | **95.3 u** |
| 5 | 26× | 4.95 u | 99.1 u | 123.8 u |
| 10 | 32× | 6.10 u | 121.9 u | 152.4 u |
| 15 | 38× | 7.24 u | 144.8 u | 181.0 u |
| *at launch* | *1×* | *0.19 u* | *3.81 u* | *4.76 u* |

For scale: the existing prism blast is radius 50–85 u depending on CHARGE, so at resting MASS the
warhead is slightly the larger of the two and the pair reads as one blast with a wider outer
shockwave.

**The reading of "25× the missile collider".** The request said the fuze is 20× the current collider
and the warhead 25× "the missile collider". Both multipliers are anchored to the **round's own hit
radius** — the same base — because that is the only reading in which the mechanic functions: the
blast must reach at least as far as the fuze that trips it, and 25× of the already-20×-ed fuze
sphere would be a **1,905 u** radius, larger than the Boneyard (520) and larger than Wildlife
Liberation's whole roam band (1,180), i.e. one rocket debuffing every pilot and killing every
creature in the match. If the other reading is wanted, it is a one-field change:
`warheadBlastRadiusMultiplier` on `SkyBurstProjectile.prefab`, 25 → 500.

### What this costs elsewhere, stated plainly

- **Dog Fight** (`DOGFIGHT.md`): a missile hit is 50 points and the mode runs to 90. Missiles now
  detonate ~76 u from an enemy instead of on contact, so the EXISTING conic/sphere blast (radius up
  to 85) will routinely catch a pilot the rocket would previously have missed — the mode gets
  faster. The warhead deliberately carries **no** combat-hit effect, so it does not add a second
  50-point event; scoring stays exactly where it was (direct hit + the conic blast, sharing one
  latch).
- **Salvo** (`SALVO.md`): that mode's premise was "the tank never regenerates and the only refuel is
  an omni crystal". Missiles now self-fund from the wreckage the mode exists to destroy, which
  weakens the crystal-run rhythm. Salvo's **wingman reload is untouched** and still the reason to
  play together — it is a mode-level bonus on top of the new platform rule — but the balance of the
  two wants a playtest.
- **Every mode**, in one direction only: a skyburst detonation now also spawns a blast that debuffs
  pilots and kills fauna. Outside the Sparrow modes that is mostly invisible; in Wildlife Liberation
  it is a real new kill source, which is the point.

## Files

| File | Role |
|---|---|
| `Assets/_Models/Sparrow Missile.fbx` | The missile pulled out of the Sparrow model (guid `98d8cb0114a1ad04e9682869849be719`, from the old branch — the skyburst projectile prefab references its mesh + embedded material). **Subdivided 2 Catmull-Clark levels in place** (624 → 9,984 tris) so it holds up at 20x; same guid, same mesh fileID, same bounding box |
| `Tools/Build/subdivide_sparrow_missile.py` | The subdivision, with `--check` proving the shipped mesh's invariants (all-quad, closed, material split, authored bounds, unit normals) |
| `Tools/Build/fbx_binary.py` | Round-trip codec for binary FBX 7.x — lets a tool edit an artist-authored file and write it back without a modelling package |
| `Assets/_Models/Vessel Models/SparrowModel4.fbx` | Animation donor: the two "Missile Launch" takes |
| `Assets/_Animations/SparrowAnimatorController.controller` | + additive layer **Missile Launching** (index 1, default weight 0) with states `Missile Launch 1` / `Missile Launch 2` at 2.5× speed |
| `_Scripts/Controller/Animation/SparrowAnimationController.cs` | Resurrected (was dead code; the prefab ran `MantaAnimationContoller`): identical puppetry + bay-layer driving off `OnMissileFired` |
| `_Scripts/Controller/Vessel/R_VesselActions/Executors/FireGunActionExecutor.cs` | `OnMissileFired(bool)` at press; bay-bone lazy resolution BY NAME (`b_Missile.R`/`.L`, warn + muzzle fallback); delayed bay-anchored spawn (UniTask, cancelled on disable/turn end/destroy) |
| `_Scripts/Controller/Vessel/R_VesselActions/Data Containers/FireGunActionSO.cs` | + `launchDelaySeconds` (0 = legacy instant muzzle spawn — FullAuto-class actions unaffected); + the MASS growth pair and `ResolveGrowthFactor` |
| `_Scripts/Controller/Vessel/ElementalScaling.cs` | `RoundGrowthFactorForLevel` / `RoundGrowthFactor` — the ONE in-flight growth curve, moved here off `FullAutoActionSO` so the bullets and the missile cannot drift apart |
| `_Scripts/Controller/Projectiles/Projectile.cs` | + the TAIL (`tail`, `tailWidthPerBodyDiameter`, `TailMount`/`TailWidth`, and the pooled-round reclaim/release pair); + `flightGrowthTarget` (empty = the root, i.e. every existing round unchanged), `flightGrowthUniform` and `flightGrowthCompleteAt01`; the launch pass rebases a child target off its authored scale so a pooled reissue cannot compound last flight's growth, and re-arms the settled latch |
| `_Scripts/Controller/Projectiles/RoundGrowthRamp.cs` | The growth SHAPE as a pure function — swell across the whole flight, or swell early and hold — plus the latch that lets a settled round stop writing its transform |
| `Assets/_SO_Assets/VesselActions/Sparrow/SkyBurstGunAction.asset` | `launchDelaySeconds: 0.2`; `growthFactorAtRestingMass: 20` / `growthFactorAtFullMass: 32` |
| `Assets/_Prefabs/Spacevessels/Components/VesselTail.prefab` | The shared tail, nested here. Also stripped of six dead disabled particle systems in the same pass — they were free on a vessel and would not have been on a 20-deep projectile pool (`Docs/VESSEL_TAIL_AND_JETS.md` §6) |
| `_Scripts/Controller/Vessel/TailGradient.cs` | The one composition of a tail's colour gradient, shared by `VesselTailAndJets` and `Projectile` so the two cannot drift |
| `Assets/_Prefabs/Projectile/SkyBurstProjectile.prefab` | `Projectile.flightGrowthTarget` → `MissileVisual`, `flightGrowthUniform: 1`, `flightGrowthCompleteAt01: 0.2`; `SphereCollider` re-authored to the **launch** fit (`r 0.019053`, centred on the model's tip) and re-fitted per frame while the model swells. Visual moved to a `MissileVisual` child: missile mesh + embedded material (+ 2× `BlueBaseOpaqueVesselMaterial` submeshes), rotated X+90° so the nose (+Y in mesh space, the radially-symmetric end) points along flight (+Z), child scale 2 (≈1.7 u world at ProjectileScale 10 — matches the bay missile's world size, armature scale 0.2034 × 8.3-unit mesh). Root scale/collider untouched → the gameplay hit sphere is byte-identical |
| `_Scripts/Controller/Vessel/VesselRearmOnPrismDestruction.cs` | **NEW** — the missile economy: listens on the prism-destroyed SOAP channel, credits this pilot's HOSTILE prism kills into the weapon's own ammo index. On the channel rather than an effect because the blast's kills never dispatch a per-prism effect |
| `_Scripts/Controller/Vessel/VesselTimedElementalWard.cs` | **NEW** — the event-driven half of debuff immunity: `Grant(seconds)`, refresh-not-stack, revoked on disable. Sibling of the condition-driven `VesselElementalImmunity` |
| `_Scripts/.../Vessel Crystal Effects/VesselWardByCrystalEffectSO.cs` | **NEW** — the crystal's new job. Stateless SO; the timer lives on the vessel's ward component |
| `_Scripts/.../Abstract Effect Types/ExplosionLifeformCrystalEffectSO.cs` | **NEW** — blast → a living lifeform's HEART. The explosion-side twin of `VesselLifeformCrystalEffectSO` |
| `_Scripts/.../Explosion Crystal Effects/ExplosionWitherLifeformByCrystalEffectSO.cs` | **NEW** — the Squirrel's joust death, reached by a blast. No heart award, no speed contest |
| `_Scripts/Controller/Projectiles/Projectile.cs` | + the PROXIMITY FUZE (`proximityFuzeRadiusMultiplier`, the overlap, the end-the-flight break) and the WARHEAD hand-off (`warheadBlast`, `warheadBlastRadiusMultiplier`, `HitRadiusWorld`, `TryBeginDetonation`) |
| `_Scripts/.../EffectsSO/ProjectileDetonatorSO.cs` | Spawns the warhead alongside the request's own prefabs — the ONE place every detonation path funnels through, so it cannot fire on some and not others |
| `_Scripts/Controller/Projectiles/AOEExplosion.cs` | + `affectsPrisms` (default true) — a blast whose whole payload is aimed at living things |
| `_Scripts/.../Impactors/ExplosionImpactor.cs` | + `SweepLifeformHearts` (the crystal sweep's twin — growable buffer, declines a corpse); `AffectsPrisms` honoured on BOTH paths, the batch entry and the Physics fallback, because the batch early-return is exactly the state in which the fallback runs |
| `_Scripts/.../Containers/ExplosionImpactorDataContainerSO.cs` | + `explosionLifeformCrystalEffects` |
| `Assets/_Prefabs/Projectile/AOEMissileWarhead.prefab` | **NEW** — the debuff/kill blast. `affectsPrisms: 0`, TrailBlocks excluded on the trigger, collider radius 0.5 (which `ProjectileDetonatorSO` assumes when it doubles a radius into a MaxScale diameter) |
| `_SO_Assets/Effects/Effect Containers/Explosion Containers/MissileWarheadExplosionImpactorDataContainer.asset` | **NEW** — [debuff pilots, joust creatures]. Deliberately carries no combat-hit effect: the missile already scores once through its direct hit + conic blast |
| `_SO_Assets/Effects/Vessel Crystal Effects/SparrowVesselWardByCrystalEffect.asset` | **NEW** — 8 s. Replaces `SparrowVesselChangeResourceByCrystalEffect`, which is DELETED rather than orphaned |
| `_Scripts/Tests/Editor/SparrowMissileFuzeTests.cs` | **NEW** — the asset gate: warhead ≥ fuze at every MASS level, the warhead's prism abstinence, the collider the detonator assumes, and the Sparrow's swapped crystal wiring |
| `Assets/_Prefabs/Spacevessels/Sparrow.prefab` | + `VesselRearmOnPrismDestruction` + `VesselTimedElementalWard` on the root. Animation component swapped `MantaAnimationContoller` → `SparrowAnimationController` (same fileID, same serialized fields) + `missileExecutor` wired to the SkyBurst executor |

## Tuning knobs

| Knob | Where | Shipped | Meaning |
|---|---|---|---|
| `launchDelaySeconds` | `SkyBurstGunAction.asset` | 0.2 | Press → projectile spawn. The animated missile departs at 0.4 s ÷ 2.5 = 0.16 s; 0.2 lands the handoff just as it clears the hull |
| State speed | animator states `Missile Launch 1/2` | 2.5 | Whole bay cycle ≈ 0.35 s |
| Visual scale | `SkyBurstProjectile.prefab` → `MissileVisual.localScale` | 2 | World missile length ≈ 1.66 u at ProjectileScale 10 |
| Mesh resolution | `subdivide_sparrow_missile.py --levels` | 2 | Catmull-Clark steps. Each step is 4x the triangles; drop to 1 (2,496 tris, 16-sided) if 9,984 is too heavy |
| Bay side predicate | `FireGunActionExecutor.Fire` | ammo ≥ 2×cost → right | Keep single-sourced; do not re-derive in the animation |
| `growthFactorAtRestingMass` / `growthFactorAtFullMass` | `SkyBurstGunAction.asset` | 20 / 32 | HOW MUCH the missile swells — and, since the collider follows the model, its REACH too (3.81 u at rest). There is no size ceiling any more; there is a balance consequence. See the section above before raising it |
| `flightGrowthCompleteAt01` | `SkyBurstProjectile.prefab` → `Projectile` | 0.2 | WHEN. Fraction of the flight the swell takes; it holds after. 1 = swell all the way in, the tracer's shape |
| `tailWidthPerBodyDiameter` | `SkyBurstProjectile.prefab` → `Projectile` | 0.4 | The tail's ribbon width as a fraction of the round's own body diameter (3.05 u at resting Mass). 0 hides the tail. Derived from the Sparrow's own `widthScale` 2.5 on a ~6.4 u hull, not play-tested |
| `proximityFuzeRadiusMultiplier` | `SkyBurstProjectile.prefab` → `Projectile` | 20 | How close something has to get, as a multiple of the round's OWN hit radius (76.2 u at resting MASS). 0 turns the fuze off and the missile detonates only on contact or at end of life |
| `warheadBlastRadiusMultiplier` | `SkyBurstProjectile.prefab` → `Projectile` | 25 | The debuff/kill blast's radius, off the SAME base (95.3 u at rest). Must stay ≥ the fuze multiplier or a proximity kill cannot catch what tripped it — `SparrowMissileFuzeTests` fails the build if it does not |
| `ammoPerPrism` | `Sparrow.prefab` → `VesselRearmOnPrismDestruction` | 0.02 | Ammunition per HOSTILE prism destroyed. The tank is 0..1 and a rocket costs 0.5, so 25 prisms per missile, 50 for a full rack |
| `hostileMassOnly` | `Sparrow.prefab` → `VesselRearmOnPrismDestruction` | on | Off makes your own trail a self-service reload. Almost certainly not what you want |
| `wardSeconds` | `SparrowVesselWardByCrystalEffect.asset` | 8 | How long an omni crystal's debuff ward lasts. Refreshes, never stacks |
| `wardedSources` | `Sparrow.prefab` → `VesselTimedElementalWard` | All (−1) | WHAT the crystal's ward stops. Narrow it to promise less (the Dolphin's drift ward is `DangerPrism` alone) |
| `debuffMagnitude` / `debuffDuration` | `MissileWarheadDebuffByExplosionEffect.asset` | −0.5 / 4 s | What the warhead does to a pilot. Forked from the Dolphin/Scarab blast's numbers so a missile retune does not move theirs |
| `faunaOnly` | `MissileWarheadWitherLifeformEffect.asset` | on | Off lets the warhead kill FLORA too — a whole grown plant per rocket, through its heart |
| `sparesOwnDomain` | `MissileWarheadWitherLifeformEffect.asset` | **off** | Off = wildlife is quarry whatever colour it wears. Deliberately the effect's OWN decision, NOT the blast's friendly-fire flag: fauna spawn in ONE colour, so borrowing that flag let the CHARGE-5 *prism* upgrade switch off wildlife kills in the one mode scored on them |
| `ExplosionDuration` | `AOEMissileWarhead.prefab` | **0.15 s** | How fast the sphere reaches full size — i.e. how fast a target can be moving away and still be caught (~130 u/s here; 0.5 s bought only ~40). Reach is not capture; see the geometry section |
| `syncIntervalSeconds` | `Sparrow.prefab` → `VesselRearmOnPrismDestruction` | 1 s | How often the OWNER publishes its missile tank to the other peers as an idempotent SET. 0 disables the correction and accepts per-peer drift, which makes a replica silently skip drawing the missile |
| Growth target / uniform | `SkyBurstProjectile.prefab` → `Projectile` | `MissileVisual` / on | Selects the model-IS-the-hit-volume path: the model grows and the collider is fitted to it. The only prefab in the game that sets it. Clearing it puts the missile on the shell path, where it would not grow at all — it has no `chargeField` |

## In-editor verification

**The bay + growth pass:** see the 🔴 entries in `Docs/UNITY_VERIFICATION_CHECKLIST.md`
(authored without a Unity compile/play-test — the donor-clip path binding and the visual seam
both need eyes).

**The tail:** also authored without a Unity compile. It is recorded through the current loop
rather than that checklist — its PR body's *Verification status* section, which `/qa-backlog`
scans into `Docs/QA/QA_BACKLOG.md`. The two things most worth an eye are the pooled-round cases:
a reissued missile must not draw a straight ribbon from the last detonation to its launch bay
(`ReclaimTail`'s `Clear()`), and a detonating one's ribbon must fade over ~4 s where it was laid
rather than vanish with the round (`ReleaseTailToFade`). Everything else is a look call on the
0.4 width fraction.

## Follow-ups

- **Hit-sphere vs. visual mismatch — CLOSED, and the bullet that stood here was stale when it
  was written.** It described the sphere as a fixed world radius 8.5 (`0.85 × ProjectileScale
  10`), which is the *pre-growth* number: the growth pass in the same commit put this round on the
  model-IS-the-hit-volume path, so the sphere is re-fitted to the model every frame — **3.81 u**
  at resting Mass, rising with it (§ the growth table above). The sphere and the visual are the
  same object by construction; there is no mismatch left to invert. Against a **pilot** it is moot
  a second way now: the proximity fuze reaches **20×** that radius (76.2 u at rest) and trips
  first, so a missile detonates near a vessel long before the direct-hit sphere is asked anything.
  The sphere still governs contact with everything the fuze mask excludes — prisms above all.
  *General trap: a Follow-ups section outlives the section that answered it, and a stale follow-up
  reads exactly like an open one.*
- The root `ParticleSystem` exhaust was tuned against the 15 u wedge and never re-tuned. It sits
  on the ROOT, so it does not grow with the model — against a 33 u missile it is the most likely
  thing to read as wrong. Needs a size pass. **The tail sharpens this**: it is now the only thing
  streaming off this round that is neither measured off the model nor domain-coloured, so if the
  stern reads as two unrelated effects, that is the one to fix (or delete — the tail may simply
  have made it redundant).
- Remote peers: the bay animation rides the same executor event as the local projectile spawn,
  so it plays wherever the projectile spawns — if skyburst fire is ever server-relayed rather
  than locally simulated per client, the bay animation follows automatically.
