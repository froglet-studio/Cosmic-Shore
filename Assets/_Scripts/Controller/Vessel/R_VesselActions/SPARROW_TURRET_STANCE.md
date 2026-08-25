# Sparrow — Turret Stance (MASS)

> **The rule, in one line:** *a turret shot **is** a bullet — you just see a prism flying,
> and where the bullet would have been destroyed the prism stays.*

## The shipped look (2026-08-10, playtest round 3)

- **Shielded, full-size shots on the plain flight** — `flightVisualization: 0`
  (TranslateAndGrow), `firedPrismState: Shielded`, `spawnFullSize: 1`. Every fired prism is
  the octahedron-armored shield prism at its FULL size from its first visible frame — the
  flight out of the gun is itself the continuity transition, so the grow-in bloom is skipped
  (the transform is pre-scaled before `Initialize`, making the creation stamp's start
  fraction ~1). The shield engages at birth, which SNAPS per the §4.5 birth rule — that snap
  is load-bearing here: it settles straight to the shared octahedron mesh on the ENTITY path,
  which is the path the flight offset rides.
- **`firedPrismState` is the playtest dial**: `Plain` (MASS-5 gates the shield as originally
  designed), `Shielded` (every shot armored — current), `Danger` (round 2's look; bites
  everyone incl. the shooter, suppresses shields — locked law). Read per volley, flip live.
- **Range quartered from the original** (round 2 halved it, round 3 halves it again — bullets
  AND turret, shared by design): base speed `FullAutoAction.speedValue.Value` → **375** with
  the SPACE curve at `MultiplierAtFullLevel` **9**, so **SPACE 0 ≈ 72 u** while **SPACE 15 is
  still the original 4875 u/s (≈ 931 u)**. Level 10 lands at 3375 u/s. Progression on SPACE is
  now dramatic: full overcharge reaches 13× the resting range.
- **ReverseSuction survives as the alternate visual** (`suctionDurationMultiplier: 5` kept):
  flip `flightVisualization` to 1 to compare again. Its danger/domain palette seam note from
  round 2 still stands if `Danger` is re-enabled.

## Round 4 (2026-08-10): shield moves to SPACE 5; the hit sphere is the bullets'

- **Shield is now the SPACE-5 upgrade** (`firedPrismState: ShieldedAtSpace5`, the new default) — **superseded in round 8, which returned it to MASS 5 as `ShieldedAtMass5`; read that section first**:
  regular prisms below SPACE 5, shielded at 5+ — the SAME gate and the same moment as the
  bullets' pierce, so the one level-5 SPACE upgrade transforms both fire modes at once
  (bullets pierce; turret shots pierce, arrive armored, and hit wider). The MASS-5 map slot is
  therefore **open again** — its `UpgradeLabel` records the move; per the design-approval gate,
  no replacement was invented. `Plain`/`Shielded`/`Danger` remain as unconditional overrides.
- **Prism shots hit like bullets now.** The report was "same feel as the projectiles… but I was
  missing lots", and the geometry agreed: a bullet flies a sphere trigger of **world diameter
  12** (radius 0.3 × the tracer's ×20 z-scale) while the prism's carried collider was a thin
  0.8×0.5×5 box — ~1/24th the cross-section. The carried collider is now a **unit sphere**
  (radius 0.5) on the prism prefab, sized in code to an authored world diameter:
  `collisionDiameter` (12, = the bullets') for regular shots, `shieldedCollisionDiameter`
  (18) for shielded ones — the armored octahedron reads bigger, so it hits bigger. Same
  trigger + non-kinematic rigidbody + transform mover as the bullets: one collision approach,
  one authored number per shot class. **(Both numbers superseded in round 6 — the bullet
  they were matched to turned out to be 8× oversized. The RULE survives; the values are now
  1.65 / 2.475.)**

## Round 5 (2026-08-10): friendly fire is always on; CHARGE 5 spares only the skyburst

- **Turret prisms now friendly-fire, exactly like the bullets.** The carried projectile on
  `Sparrow Projectile Prism.prefab` shipped with `friendlyFire: 0`, so
  `Projectile.DisallowImpactOnPrism` silently dropped every own-domain prism contact — the
  shot flew straight through friendly mass with no effect and no pierce-stop. It is now
  `friendlyFire: 1`, matching `SparrowProjectile.prefab` (the bullets, which already had it).
  The shared damage effect (`ProjectileDamagePrismEffect`) has no domain gate, so bullets and
  turret prisms damage ALL prisms, own domain included, at every element level.
- **CHARGE-5 'Domain-Safe Skybursts' is the only friendly-fire exemption, and it now covers
  the whole missile.** The direct-hit damage already gated on the per-shot
  `Projectile.SpareOwnDomain` snapshot, but the AOE prefabs' authored `affectSelf: 0` made
  every skyburst BLAST spare own domain at every Charge level — the upgrade was half
  pre-unlocked. `ProjectileDetonatorSO` now passes
  `AffectSelfOverride = !proj.SpareOwnDomain` on every detonation (hit, timeout, mine,
  vessel-strike — all four callers route through the one detonator), so below CHARGE 5 the
  blast friendly-fires like every other Sparrow shot and at 5+ the whole missile goes
  domain-safe. The shared `AOEExplosion.prefab` is untouched — the Manta crystal path keeps
  its own authored/overridden behavior.
- **Placement immunity (iterated to in the same round).** Friendly fire exposed a
  self-interaction: viz 1 parks the prism live at the anchor for its whole flight, so the
  carried projectile arrived AT its own prism and destroyed it every shot — and after an
  identity-based host guard fixed that, the 12-u hit sphere meant even a full-speed spin's
  next shots erased the previous prism, leaving exactly one alive. The shipped rule is ONE
  time window instead of identity/owner special cases: each fired prism carries
  `Prism.ProjectileImmuneUntil` (a `Time.time` deadline; 0 = none, cleared on pool reuse),
  and `Projectile.DisallowImpactOnPrism` skips any prism whose window is open. The turret
  stamps `flightTime + placementImmunitySeconds` at fire (viz 1 — the prism is live from
  fire time) or `placementImmunitySeconds` at landing (viz 2 — created at landing), with
  `placementImmunitySeconds` (0.2) authored on the action SO. The window is vs ALL
  projectiles — sub-second, so the gameplay cost is nil — and once it closes the prism is
  ordinary friendly-fire mass: your own later shots, teammates, and enemies all destroy it
  normally.

## Round 6 (2026-08-11): the hit sphere shrinks to the projectile it draws

Round 4 matched the prism's hit volume to the BULLET's. This round fixes the bullet: its
collider was never sized to the projectile at all.

- **The bullet's hit sphere was 8× its visible radius.** `SparrowProjectile.prefab` draws a
  unit sphere mesh (radius 0.5) at scale `(1.5, 1.5, 20)` — a dart whose visible
  cross-section radius is **0.75** and whose half-length is 10. Its `SphereCollider` was
  `m_Radius 0.3`, and a SphereCollider scales by the **largest** lossy-scale component
  (the z-stretch, 20) — so the hit sphere was **6.0 world radius / 12 diameter**, a ball
  eight times wider than the tracer and comparable to the dart's own length. The z-scale
  leaking into the radius is the whole bug: nothing authored 12, it fell out of the mesh
  stretch.
- **Now the collider is the projectile +10%.** `m_Radius: 0.04125` → `0.04125 × 20 = 0.825`
  world radius = **1.65 diameter**. Author it that way: pick the world radius you want and
  divide by the max scale component; changing the tracer's z-stretch silently rescales the
  hit sphere unless you re-derive.
- **The prism shots follow it, as they must** (round 4's rule stands — one collision
  approach for both fire modes): `collisionDiameter` **12 → 1.65**, and
  `shieldedCollisionDiameter` **18 → 2.475**, preserving the authored ×1.5 so the armored
  octahedron still hits bigger than a plain prism.
- **Placement immunity stays load-bearing** even though the overlap that motivated it is
  mostly gone. The self-delivery case is geometric, not size-dependent: viz 1 parks the
  prism at the anchor and the carried projectile arrives at its centre, so *any* nonzero
  collider hits its own prism on arrival. The window also still separates shots in a spray;
  at this diameter it has far less work to do, so `placementImmunitySeconds` could likely
  come down — judge it in play before touching it.
- **Expect a real drop in aim forgiveness on both fire modes.** A 53× smaller frontal
  cross-section is the point of the change (the collider now matches what the player sees),
  but "the guns feel tighter" is the intended outcome, not a regression to fix by inflating
  the sphere again.

## Round 7 (2026-08-12): the muzzle was 15 units in front of the ship

Rounds 4 and 6 tuned the hit sphere. This round found that the sphere's size never mattered at
close range, because the shot was not being born anywhere near the fight.

**The Sparrow carries TWO pairs of gun transforms — one per fire mode — and they had drifted:**

| executor | fire mode | `LeftGun` / `RightGun` local position |
|---|---|---|
| `FullAutoActionExecutor` | bullets | `(±3.2, 0.4, `**`1.30`**`)` |
| `FullAutoBlockActionExecutor` | turret prism rounds | `(±3.0, 0.4, `**`15.13`**`)` |

A shot is born at its muzzle, so **every turret round spawned 15 units ahead of the nose** and the
first 15 units of its path did not exist. Anything closer than that was un-hittable by turret fire
— it appeared already past them. Surfaced by Dog Fight (a mode built entirely around close passes
in a wreck field: *"shooting with the turrets does not do any damage… maybe because the point of
origin of bullets for the sparrow is too far away from the model"* — exactly right), but the bug
was never mode-specific: it was the turret stance, everywhere, for its whole life.

The turret pair is moved onto the bullets' position, which is what this document's own rule
already said it should be — *a turret shot **is** a bullet*. Both pairs are bare `Transform`s
(no renderer, no VFX, no children), so this only changes where the shot starts.

**Range is unaffected.** `FireOne` computes `anchor = muzzle + forward × range`, so moving the
muzzle back moves the anchor back with it — identical path length, nothing to retune. The prism
now visibly emerges from the gun barrels instead of materialising ahead of the ship.

Guarded by `Tools/Build/author_dogfight_assets.py`, which asserts four gun transforms on the
bullets' position and no transform left at `z = 15.13`. **This is a shared vessel prefab** — a
silent drift here breaks the Sparrow in every mode.

## Round-3 follow-up: the spread rendered at full distance the whole flight

Playtest report: positions were right but the prisms drew as if at maximum range from their
first frame — the distance-driven spread looked maxed the whole way out of the barrel.

Root cause: the flight moves VERTICES (the entity transform is final at the anchor by design),
but the spread chain's distance came from `SqrDistanceSubGraph` = `dot(pivot − camera, ·)` —
the **pivot**, which sits at the anchor for the entire flight. Any look derived from the object
position, not the displaced geometry, reads the destination.

Fix, GPU-side and law-conforming: `PrismFlightSqrDistance` (in `PrismClockAnimation.hlsl`)
computes the same squared camera distance from the pivot **displaced by the flight offset** —
the identical easing formula as `PrismFlightClock` (keep the two in lockstep). It replaces the
subgraph feed into `Prism Sub Graph.SqrDistance` on BlockGraph (wired by
`wire_prism_flight_clock.py` stage 2, which also retires the now-unused `SqrDistanceSubGraph`
node; ExplodingBlockGraph has no distance chain). Unstamped prisms (`Duration 0`) reduce to
exactly the old expression, so nothing else in the game renders differently.

## Round 7 (2026-08-13): the cadence doubles, and both fire modes spray

The parity doctrine extends cleanly to accuracy: a turret shot is a bullet, so it walks off aim on
a held trigger exactly like one and the prism stays wherever that deflected round would have died.
`FullAutoBlockShootActionSO.Spread` forwards `bulletAction.Spread` — the turret authors **no cone
of its own**, same as cadence and speed. Full mechanic, tuning and verification:
**`SPARROW_SPRAY_ACCURACY.md`**. What changes here:

- **`firingRate` 30 → 90** (60 in the first pass, raised again after playtest). Anchored prisms/s
  therefore **60 → 180** and volume/s ~120 → ~360 at base scale (MASS ×1). This is the documented consequence of "the same rate as its bullets" and
  `firingRate` is still the single lever — a turret-only divisor would re-open the drift the
  shared-cadence pass closed. The prism pool was resized for it (see the table below).
- **The cadence is now frame-rate independent.** Both loops replaced `UniTask.Delay(1/rate)` with a
  time accumulator. A whole-frame delay caps the rate at the frame rate, so at 60 volleys/s a 30 fps
  device would have laid prisms at **half** the rate a 60 fps device did. Capped at 4 volleys/tick
  with the excess dropped, so a hitch never discharges as a burst.
- **Shot rotation is the deflected pose**, composed with `Quaternion.FromToRotation` rather than
  rebuilt with `LookRotation` — the prism's long axis *is* the shot, so re-referencing roll to world
  up would visibly twist every prism.
- **`placementImmunitySeconds` matters again.** Round 6 noted 0.2 s was probably too long once the
  hit sphere shrank; at 120 shots/s the shot-vs-shot spacing it guards is tighter again. Re-judge in
  play rather than assuming either direction.

## Round 8 (2026-08-13): the shield comes back to MASS, and rounds grow as they fly

Playtest: *"the only thing that has felt fun was huge projectiles."* The answer was to make
huge projectiles **earned** rather than authored, which settled where the two elements divide:

> **MASS owns the SUBSTANCE of what you fire. SPACE owns its REACH.**

- **`ShieldedAtSpace5` → `ShieldedAtMass5`** (same enum value `3`, so the asset is unchanged).
  Fired prisms arrive armored at **MASS 5**, gated on `IsUpgradeActive(Element.Mass)`. Round 4
  had moved it to Space 5 to make one gate transform both fire modes; with round 8's growth
  also on Mass, the substance/reach split is the cleaner line and this came back by sign-off.
  The Sparrow's map is 4/4 upgrades again and the MASS-5 slot is no longer open.
- **SPACE 5 is now purely pierce**, on both fire modes — unchanged in code, narrowed in the map
  text.
- **Rounds swell as they travel**, bullets and turret shots alike: `ResolveGrowthFactor` on the
  shared bullet action, **3× over the flight at resting Mass, 6× at Mass 10**, linear in level
  across the full [-5, 15] band (1.5× starved, 7.5× at full overcharge). The turret adopts it
  through `bulletAction` like everything else. (The curve itself lives on
  `ElementalScaling.RoundGrowthFactorForLevel` since 2026-08-25, shared with the skyburst
  missile, which authors its own pair and its own shape — these numbers are unchanged.) Its carried hit sphere therefore ends the flight
  at 4.95 diameter at Mass 0 — much closer to the size of the prism the player actually watches
  flying (bounding ≈5.1) than the 1.65 it launches at.

## Two flight visualizations (A/B, live-switchable)

`FullAutoBlockShootAction.asset` → **Flight Visualization** selects how the flying prism is
drawn. The executor reads it **per volley**, so flipping the enum in the inspector during play
mode switches the very next shot — that is the intended way to A/B them. Gameplay is identical
in both: the carried projectile flies, pierces (SPACE-5), and decides where the shot ends.

| | `TranslateAndGrow` (0, DEFAULT since round 3) | `ReverseSuction` (1) |
|---|---|---|
| What you see | The prism itself scales up and translates out of the gun into place | The fauna suction shader **in reverse**: the prism's faces stream out of the **moving shot point** into the final shape at the anchor, over `suctionDurationMultiplier`× the flight time |
| Mechanism | `PrismFlightClock` vertex offset (GPU) + the standard grow bloom (`GrowthRate` pinned to 8 for a visible in-flight bloom) | `PrismImplosion.StartGrow(carriedProjectile, flightTime × mult)` — `_SuctionDirection = −1` with `_Location` tracking the projectile under the documented moving-target exception; the real prism flies as a scale-zero blank and is **created as the stream completes** (scheduled 0.2 s early so the reveal overlaps; the effect's completion is the exactly-once backstop) |
| When mass is tangible | At the **destination from the moment of firing** (gameplay-final-at-start) | At **assembly completion** (the finished stream is the creating force) |
| Early impact (SPACE < 5) | One re-pose to the impact point + stamp settle | Stream cut; the prism is created at the impact point, its own creation bloom carrying the reveal |

`ReverseSuction` is the first producer of `PrismType.Grow` — `PrismFactory.SpawnGrow` was
authored and never reachable until now. The effect rides the `EventOnSpawnPrismAndReturn`
channel (wired on the Sparrow's executor) and takes the shooter's domain colors via
`ConfigureForTeam`. Known cosmetic seam: the stream renders in domain colors and the revealed
prism then wears the danger material — if that flip reads badly in play, the fix is teaching
`ConfigureForTeam`/`SpawnGrow` a danger palette, not disabling danger.

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
abreast for the whole flight and stop at the same range (≈ **72 u** at the shipped 375 u/s ×
0.3 s at SPACE 0 — the figure was ~286 u before round 3 quartered the base speed; at SPACE 10 it
is ~645 u).

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
- **…and it grows the HIT VOLUME too — now by IN-FLIGHT GROWTH, not a static multiplier.**
  2026-08-12 shipped `hitDiameter × √Multiplier(Mass)` here, for a reason that still stands:
  before it, the flying collider was a fixed `collisionDiameter` / `shieldedCollisionDiameter`,
  so Mass made the rounds *look* bigger while they connected exactly as often — a cosmetic buff
  on the one element this vessel's guns are wired to.
  **Round 8 (2026-08-13) replaced it** with `FullAutoActionSO.ResolveGrowthFactor`: the round
  launches at its authored diameter and **swells across the flight**, 3× at resting Mass and 6×
  at Mass 10. Same intent, three changes: it covers **both** fire modes (the √ bump was
  turret-only), it is far larger where it matters (6× vs 1.58×), and it grows the drawn round in
  lockstep so the hit volume stays honest. The two are **not** stacked — that would apply MASS to
  one quantity twice (1.58 × 6 ≈ 9.5× on a 2.475 base, about double the prism's own bounding
  size). See `SPARROW_SPRAY_ACCURACY.md` ▸ "Round 3".
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
| `firingRate` | **90** (was 30 before round 7) | Volleys/s for guns **and** turret. |
| `speedValue.Value` | **375** (was 1500 before round 3's quartering) | Muzzle speed base for both, before the SPACE multiplier (0.4× at rest → 9× at full overcharge). |
| `projectileTime` | **0.3** | Flight time; with the easing curve → ~**72 u** of range at SPACE 0, ~645 u at SPACE 10. |
| `spread.*` | see `SPARROW_SPRAY_ACCURACY.md` | The accuracy-decay cone, shared by both modes (round 7). |

Turret-only, on **`FullAutoBlockShootAction.asset`**: `blockScale` **(0.8, 0.5, 5)** (before
the MASS stretch on z), `rotationOffsetEuler`, `prismType` `Sparrow`.

`waitTime` on `Sparrow Projectile Prism.prefab` is **0** and must stay low: it is the delay
before creation completes, and at 0.5 s the prism was still invisible when its 0.3 s flight
ended.

## Collider / mass budget

| | Original | Cadence parity | **Round 7** |
|---|---|---|---|
| Volleys/s | 14 | 30 | **90** |
| Muzzles | 2 | 2 | 2 |
| **Anchored prisms/s** | 28 | 60 | **180** |
| Volume/s (base scale, MASS ×1) | ~56 | ~120 | **~360** |

A held burst lays permanent mass at **~6.4× the original rate** — ~1,800 prisms in ten seconds,
each a spatial-index registration plus a collider under the usual collider-LOD. That is what
"the same rate as its bullets" costs; the single lever is `FullAutoAction.firingRate`, and it
moves the guns too. Pool sizing on `Sparrow.prefab` follows it: the turret's
`BlockProjectilePoolManager` went to `defaultCapacity 120 / maxSize 600 / bufferSizeTarget 260 /
maxAddsPerFrame 16`, because anchored prisms are **never returned** and every shot past the
buffer is a fresh `Instantiate`.

**Per-frame CPU went down, not up.** The prism costs one stamp and one anchor; the only
per-frame work is the carried projectile's transform, which is exactly what a bullet already
costs. The deleted `MoveAndAnchorAsync` was a per-frame write per live prism.

## Why "still nothing" was still possible after the Initialize fix

Three silent failure modes survived the first fix, all now closed or screaming:

1. **The shader graphs not (re)imported.** The flight properties were spliced into the graphs
   out-of-editor; until Unity reimports them, the per-instance stamp uploads into a property no
   shader reads and the prism **teleports to maximum range (~72 u downrange at SPACE 0) with no flight** —
   invisible to anyone watching the muzzle. This now screams
   (`PrismClockDiagnostics.WarnUnwiredMaterial` on `_FlightStartTime`), and `ReverseSuction`
   does not depend on the new graph wiring at all — it rides the long-shipped SuctionGraph, so
   it is also the control experiment: if viz 2 shows faces streaming and viz 1 shows nothing,
   the flight graph wiring is the problem, run
   `FrogletTools > Ecology > Prism Animation > Auto-Wire Clock Properties` and reimport.
2. **The bloom was too slow to see.** The prism prefab's authored `GrowthRate` (0.01) gives the
   slowest clock bloom (~5 s to settle) — at bullet speed the shot arrived at a few percent of
   its 0.8×0.5×5 size. The executor now pins `GrowthRate = 8` (the ceiling) for turret prisms.
3. **Testing a stale editor.** The branch is `claude/sparrow-prism-attack-hg6n78`; none of this
   is on `bleeding-edge`. If the editor wasn't on the branch (or didn't recompile), the old
   silent-zero-scale path was still what ran.

## In-editor verification

Scene: any Sparrow-playable multiplayer scene (`MinigameWildlifeLiberation` or
`MinigameFreestyleMultiplayer_Gameplay`). Stop with the stationary-mode input (input 6), then
hold fire (input 1). Test BOTH visualizations — select `FullAutoBlockShootAction.asset` and flip
**Flight Visualization** live in play mode.

1. **Something comes out at all.** This was the headline bug — the stance fired invisible
   zero-scale prisms. Prisms must now visibly leave the muzzles (viz 1) or stream into place
   from the moving shot point (viz 2).
2. **Cadence parity.** Fire on the move, then stopped. The rate must be indistinguishable —
   both 30 volleys/s from 2 muzzles.
3. **Speed parity + smooth flight.** Prisms leave as fast as bullets and travel ~72 u at SPACE 0. The
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

- **Tunneling — RESOLVED (2026-08-13), and it was worse than this entry estimated.** Shipped as
  `PrismSpatialIndex.QuerySegment` + `Projectile.sweptPrismDetection`, exactly the swept segment
  query named here. The measured hole was ~74% of the path at the shipped 375 u/s base speed and
  ~97% at high SPACE — it was the actual reason the guns could not clear a small area, and the
  reason the round-6 collider correction (12 → 1.65 diameter) removed so much felt lethality: the
  oversized ball had been closing the per-frame gap. Changing bullet behaviour was the POINT this
  time, and parity holds because both fire modes opt in. See
  `SPARROW_SPRAY_ACCURACY.md` ▸ "Round 2".
- **Anchored-mass rate.** If 60 prisms/s is too much for a cell's phase ladder in practice,
  move `FullAutoAction.firingRate` — not a turret-only divisor, which would re-open the drift
  this pass closed.
- **The spawn window.** A prism is invisible for the 1–2 frames between the pool pull and
  creation completion (~6–12 u of a 72 u flight at SPACE 0). `Docs/PRISM_ANIMATION.md` §4.2 already
  plans to retire that window entirely; it is not turret-specific.
- **`placementImmunitySeconds` is probably now too long.** It was sized (0.2) against a
  12-diameter hit sphere; round 6 shrank that to 1.65, so the shot-vs-shot overlap it was
  compensating for is largely gone and only the geometric self-delivery case still needs
  it. Re-tune after flying round 6 — a value just long enough to cover flight-end settle
  is the target.
- **The same collider trap is latent in two dead prefabs.** `ProjectileFX.prefab` and
  `SparrowExhaustProjectile.prefab` both carry `m_Radius 0.3` at scale `(3, 3, 20)` — a
  6.0-world-radius hit sphere around a projectile of visible radius 1.5, the identical
  z-stretch bug round 6 fixed on `SparrowProjectile`. Both are currently referenced by
  NOTHING (verified by GUID sweep), so they are harmless today; if either is revived,
  re-derive its radius as `desiredWorldRadius / maxScaleComponent` first.
- **The drawn prism and its collider integrate the same easing two different ways.** The
  shader evaluates the CLOSED FORM (`v·(2T/π)·(sin(πt/2T) − 1)`, exact, frame-rate
  independent, reaching the anchor precisely at `t = flightTime`), while the carried
  projectile is moved by `Projectile.MoveProjectileAsync`'s forward-Euler sum of the same
  `cos(πt/2T)` curve. A left-Riemann sum of a decreasing function overestimates, so the
  COLLIDER runs ahead of the prism you see — measured: **+2.2% of range at 120 fps, +4.3%
  at 60, +8.5% at 30**. Both agree exactly at the muzzle and the divergence peaks at the
  end of the flight, where the projectile dies a few percent past where the prism settles.
  This is not a clock-law issue (the prism side is the exact one) and it is not new — it is
  the bullets' existing mover, shared by design. Fixing it means giving
  `MoveProjectileAsync` the closed form, which changes EVERY projectile in the game and so
  belongs in its own branch with its own retune, exactly like the tunneling item above.
- **Placement immunity is local, unreplicated state.** `Prism.ProjectileImmuneUntil` is
  computed from each peer's own `Time.time`, so in a networked match two clients could
  disagree by a frame or two about whether a marginal impact landed inside the window.
  The turret's prism spawning is not networked at all today (local `blockFactory` pull),
  so this is not a new divergence — but it is one more thing to settle when the stance is
  made server-authoritative.
