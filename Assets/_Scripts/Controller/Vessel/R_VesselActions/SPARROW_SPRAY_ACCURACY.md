# Sparrow — Spray Accuracy (the walking gun)

> **The rule, in one line:** *hold the trigger and the cone opens — the danger zone grows and
> your hands feel it; let go for an instant and you are pin-accurate again.*

The Sparrow's cannons are a **saturation** weapon, not a marksman's rifle.

| you do | you get |
|---|---|
| tap the trigger (≤ 0.12 s) | a perfectly accurate burst — a scalpel, at a fraction of the volume |
| hold it down | a cone that opens over ~1.6 s to a 1.5° cap, filled at **180 rounds/s** — nothing along the path survives, and the buzz in your hands climbs the whole way |
| release and re-pull | full accuracy back, instantly. This is the "3-shot burst" the design asks for |
| collect Mass crystals | rounds swell **harder** as they fly — 3× over a flight at rest, **6× at Mass 10**. Huge projectiles, earned |

---

## Round 2 (2026-08-13): the guns could not clear a small area, and spread was not why

Playtest report: *"far too difficult to destroy all the prisms in a small area. When I increase
the projectile radius I get the desired results, except that giant bullets from a small vessel is
silly. The hope was that increasing the fire rate and the spread would be a reasonable substitute,
but it was still inadequate."*

That report contains its own diagnosis. **A bigger radius worked because the bullet was missing
most of its own flight path**, and no amount of fire rate or spread can compensate for a weapon
that is structurally blind to the ground between its samples.

### The bug: a projectile is a TELEPORT, not a sweep

`Projectile.MoveProjectileAsync` advances the transform by `Velocity · Δt` each frame, and PhysX
samples that discrete trigger once per physics step. So collisions are only ever tested at the
handful of points the round *lands on* — never along the line between them:

| | muzzle speed | step / frame @60 fps | hit sphere Ø | **path actually tested** |
|---|---|---|---|---|
| SPACE 0 | 375 u/s | 6.25 u | 1.65 | **26%** |
| SPACE 5 | 1875 u/s | 31.25 u | 1.65 | **5%** |
| SPACE 10 | 3375 u/s | 56.25 u | 1.65 | **3%** |

Three-quarters of every shot's path was never tested for collision, and at range it was
97%. Prisms in the gaps were passed straight through — silently, with no miss to see. Halve the
frame rate and it halves again.

**This also explains the pre-existing collider history.** Round 6 of the turret pass shrank the
bullet's hit sphere from a 12 diameter to 1.65 after finding nothing had *authored* the 12 — it
fell out of the tracer mesh's ×20 z-stretch leaking into a `SphereCollider` radius. That was
geometrically correct and it silently removed the thing that had been papering over the tunneling:
a 12-diameter ball closes a 6.25 u step completely. The accident was load-bearing. (Even it was
not enough at range — at SPACE 10 the old ball still only covered 21% of the path.)

### The fix: test the segment, not the landing point

`PrismSpatialIndex.QuerySegment(a, b, radius, results)` — the swept counterpart of `QuerySphere`,
gathering every live prism within `radius` of the segment the round crossed this frame. This is
the fix `SPARROW_TURRET_STANCE.md` named in its follow-ups ("a swept segment query on
`PrismSpatialIndex`, not CCD — a transform teleport bypasses CCD") and the one CLAUDE.md's
anti-pattern list demands (never `Physics.OverlapSphere` against prisms; new query shapes go on
the index).

It is **exactly the effect of a huge bullet with none of the appearance** — which is what the
playtest asked for.

- **`Projectile.sweptPrismDetection`** (opt-in, on for `SparrowProjectile.prefab` and
  `Sparrow Projectile Prism.prefab`) makes the swept query the **sole** owner of prism contact;
  `ProjectileImpactor` suppresses the trigger's prism case so nothing double-dispatches. The
  trigger was never a second chance — it is the thing that was missing 74%.
- **Hits dispatch nearest-first**, which is what makes the sub-SPACE-5 "destroyed on its first
  prism impact" rule mean the *first prism along the path* rather than an arbitrary one.
- **The round is moved to each contact point before its impact fires**, so effects — and the
  Turret Stance's anchor, which places its prism "wherever the bullet would be destroyed" — see
  where the shot actually met the prism, not where the frame's step happened to end.
- **Contact is bounding-sphere**, not an exact capsule-vs-OBB narrowphase: a few instructions
  instead of a narrowphase, erring slightly generous at corners — the right direction for a
  saturation weapon, and still far tighter than the oversized collider it replaces.
- Dispatch reuses `ImpactorBase.AcceptImpacteeFromSweep`, the exact analogue of the shell tier's
  `AcceptImpacteeFromShellContact`: same init gate, same profiler marker, same effect chain.

**Vessels and mines still use the trigger.** The same tunneling applies to them and is a real
follow-up, but a vessel hull is a much bigger target than a trail prism and widening this pass to
the whole impact system is its own change.

### And the tuning the report asked for

With the path actually being tested, spread does what it was supposed to do, so the cone gets
tighter and the volume goes up:

| | round 1 | **round 2** |
|---|---|---|
| `firingRate` | 60 volleys/s | **90** (180 rounds/s) |
| `spread.growthDegreesPerSecond` | 3.2 | **1.0** |
| `spread.maxHalfAngleDegrees` | 4 | **1.5** |

The cone now reaches only 0.88° after a full second of holding and caps at 1.5° (a 1.9 u radius
at the SPACE-0 range of ~72 u). It is a *texture* on the stream rather than a scatter — which is
what it should have been once the rounds started actually connecting.

---

## Round 3 (2026-08-13): rounds GROW as they fly — huge projectiles, earned

Playtest: *"we need to get creative to get these guns to feel fun because right now the only
thing that has felt fun was huge projectiles."*

Round 2 gave every round its whole path back, but it did not change the shape of what a round
deletes: **a thread**. A huge projectile deletes a **tunnel**, and that — not the hit rate — is
what was fun. The answer is to keep huge projectiles and take away the thing that made them
silly: a small vessel firing cannonballs. So rounds now **leave the muzzle small and swell as
they travel**, and MASS decides how much.

> **The split this settles: MASS owns the SUBSTANCE of what you fire. SPACE owns its REACH.**

| | |
|---|---|
| **MASS** quantitative | turret prism stretch (unchanged) **+ in-flight round growth** |
| **MASS 5** | **Shielded Prisms** — returned here from Space 5 by design sign-off |
| **SPACE** quantitative | range (unchanged) |
| **SPACE 5** | **pierce**, on both fire modes — and nothing else now |

### The growth curve

`FullAutoActionSO.ResolveGrowthFactor` → `GrowthFactorForLevel(massLevel, 3, 6)`: linear in
LEVEL with the authored pair anchored at 0 and 10, **extrapolated** (not clamped) across the
element system's full [-5, 15] band.

| Mass level | grows to | end-of-flight hit radius | swath vs. a non-growing round |
|---|---|---|---|
| −5 | 1.5× | 1.24 | 2.2× |
| 0 | **3.0×** | 2.47 | **9×** |
| 5 | 4.5× | 3.71 | 20× |
| 10 | **6.0×** | 4.95 | **36×** |
| 15 | 7.5× | 6.19 | 56× |

Destruction footprint goes as the **square** of the radius, which is why this is worth so much
more than any affordable fire-rate increase — and it is the number the "huge projectiles" report
was really reacting to. For scale: the accidental oversized collider that felt good was 6.0
world radius. Resting Mass now ends its flight at 2.47 and **Mass 10 reaches 4.95** — the fun
size is back, as something you earn rather than something the gun always had.

### Sized honestly, the whole way

The visual and the hit volume are scaled by the **same factor every frame**, so the ratio the
round-6 collider fix established (hit radius = visible cross-section +10%) is invariant through
the flight. What you see is what you hit, at every instant, at every Mass level.

**Cross-section only.** The tracer mesh is a unit sphere at (1.5, 1.5, 20) — a 20-long dart — so
scaling it uniformly at 6× would draw a 120-unit needle across a ~72-unit range. Width is what a
hit volume is made of; length is just the streak. The swept hit radius is therefore scaled
*explicitly* rather than re-derived from `lossyScale`, because a SphereCollider takes the largest
lossy component and that stays the untouched z-stretch.

One deliberate scope line falls out of that: the **swept prism** radius grows, while the PhysX
radius the **vessel/mine** path uses does not (for the dart — the turret's uniformly-scaled
carried sphere does grow on both). Growing bullets against vessels is a Dog Fight balance change,
not a prism-clearing one, so it is not in this pass.

### What was deliberately NOT done

The alternatives considered and declined, so they are not re-derived: **impact shatter** (a kill
cracking its neighbours) and **pierce depth** (rounds boring through N prisms before stopping).
Both break the one-round-one-prism ceiling too, and both were rejected in favour of growth —
"keep everything else the same, we will get this feel through the mass scaling effect." SPACE 5
remains the only thing that lets a round pass through a prism.

---

## The three moving parts

### 1. Rate of fire — 30 → 60 → **90 volleys/s** (180 rounds/s across two muzzles)

Round 6 of the turret pass shrank the bullet's hit sphere 8× and deliberately made the guns
tighter: *"the guns feel tighter is the intended outcome, not a regression to fix by inflating the
sphere again."* That still stands — the forgiveness came back as **path coverage plus volume of
fire**, never as a bigger invisible ball.

### 2. The fire loops are now frame-rate independent — and this was load-bearing

Both fire loops used `await UniTask.Delay(1 / rate)`. A frame-quantized delay can never produce
more than **one volley per frame**, so the authored rate was silently `min(rate, framerate)`:

| authored rate | 60 fps | 30 fps |
|---|---|---|
| 30 volleys/s (old) | 30 ✔ (33 ms ≈ 2 frames, correct by luck) | 30 ✔ |
| 60 volleys/s (naive) | **60**, but only exactly — 16.7 ms *is* one frame | **30** ✘ — half rate |

So raising `firingRate` past ~30 without fixing the loop would have handed 60 fps players double
the fire rate (and, in Dog Fight, double the scoring rate) of 30 fps players.

Both loops now **owe fire in seconds and pay it off in whole volleys** (`owed += Time.deltaTime`,
fire `floor(owed / interval)`), capped at `MaxVolleysPerTick = 4` with the excess *dropped* rather
than carried — after a hitch the gun resumes firing, it does not discharge the stall as a burst.
At 90 volleys/s a 60 fps client fires 1–2 volleys per frame and a 30 fps client fires 3; both put
the same rounds downrange. The cap sustains the full rate down to ~23 fps.

### 3. The cone

`GunSpreadMath.HalfAngleDegrees` — flat zero through the onset window, then linear, then hard
capped:

```
half-angle(t) = clamp( (t − onset) × growth , 0 , max )
```

`GunSpreadMath.Perturb` deflects each round to a point inside that cone, sampling the deflection
as `max × u^bias`. At the shipped **bias 0.5** that is *uniform over the cone's disc*: the whole
danger zone saturates evenly rather than piling every round in the middle. (Bias 1.0 is the
authored alternative — a dense core with a thin halo, so the thing you are aiming at still soaks
most of the fire. It is one field if the even fill reads as too loose in play.)

**One roll per ROUND, not per volley** — the two muzzles scatter independently, which is what
makes the stream a widening cone rather than two widening lines.

**It does not touch `UnityEngine.Random`.** The deflection is a pure integer-hash of a per-vessel
shot serial, for two reasons: the global RNG stream is shared state that deterministic systems
seed (`Random.InitState` for the HexRace track), and a gun drawing from it 120×/s would make
their output depend on how long someone held a trigger; and a hash keeps peers that agree on the
shot count agreeing on where the shot went, which matters for the turret's locally-spawned
prisms. The serial is **monotonic across the session** and deliberately *not* reset per hold —
resetting it would make every trigger pull replay the same deflection sequence, which is a
learnable pattern rather than a stochastic cone.

Note the cap is an **angle**, so miss distance scales with range. A Sparrow at SPACE 0 shoots
~72 u and groups within ~5 u; at SPACE 10 it shoots ~645 u and groups within ~45 u. That is
correct — you are shooting nine times further.

---

## Reset semantics, and the one subtlety in them

Releasing the trigger resets accuracy **completely**, so a release-and-re-pull always buys the
whole onset window back. The reset is deferred by exactly one frame (`GunSprayAccuracy.LateUpdate`)
for one specific reason:

> Toggling the Turret Stance mid-hold makes `SparrowModeSwitchingFireSO` **stop one fire action
> and start the other, synchronously, in the same call stack**. Without the deferral that internal
> hand-off is indistinguishable from a trigger release and would hand the pilot a free accuracy
> reset for flicking stance.

`ReleaseHold()` arms the reset; a `BeginHold()` arriving in the same frame disarms it. The fire
loops run at `PreLateUpdate`, so a real release still lands before the next volley — the deferral
is invisible in play. `BeginHold` is idempotent for the same reason: taking the hold over mid-press
refreshes the profile without restarting the clock.

## The turret stance sprays too — because a turret shot IS a bullet

`SPARROW_TURRET_STANCE.md`'s parity doctrine is unchanged and this pass extends it rather than
carving an exception: *"a turret shot is a bullet — you just see a prism flying, and where the
bullet would have been destroyed the prism stays."* Spread changes where the round goes, so the
prism goes there too. The turret authors **no cone of its own** — `FullAutoBlockShootActionSO.Spread`
forwards `bulletAction.Spread`, exactly as `FireRate`, `FlightTime` and `ResolveSpeed` already do.

The deflection is composed onto the muzzle **pose** (`Quaternion.FromToRotation`), never rebuilt
with `LookRotation`: a turret prism's long axis *is* the shot, so re-referencing roll to world up
would visibly twist every prism.

A held turret burst therefore lays a **scattered volume** of permanent mass instead of a line —
which is a better wall, and the same mechanic the bullets get.

## The fourth haptic feel

The escalating buzz is a deliberate exercise of `Docs/HAPTICS.md` ▸ "Adding / changing a feel"
(dedicated method + extended gate, never the silenced legacy API). It is the game's only
**continuous** feel, which is exactly why it sits at the bottom of the priority order:

```
alert  >  punish  >  skim  >  spray
```

Everything suppresses the spray; the spray suppresses nothing. Being interruptible costs it
nothing — the next pulse is milliseconds away — and it means adding a texture did not make the two
feels the policy is built around any less legible.

Both the **strength** (0.15 → 1.0) and the **cadence** (100 ms → 45 ms) climb with the cone, so it
reads as a gun winding up rather than a constant hum. Local human pilot only: remote players, AI
dogfighters and the Menu_Main autopilot all fire and none of them may buzz your device.

## Files

| File | Role |
|---|---|
| `_Scripts/Utility/GunSpreadMath.cs` | The pure cone math — ramp, hash-sampled deflection, roll-preserving `DeflectionOf`. No Unity state, no global RNG. |
| `R_VesselActions/Data Containers/GunSpreadProfile.cs` | The authored profile (cone + haptic ramp). Serialized on the bullet action. |
| `R_VesselActions/Executors/GunSprayAccuracy.cs` | Per-vessel hold state, the spread clock, the haptic ramp, and the deferred reset. |
| `R_VesselActions/Data Containers/FullAutoActionSO.cs` | Owns `Spread` and `ResolveGrowthFactor`/`GrowthFactorForLevel`; hands the accuracy component to its executor. |
| `R_VesselActions/Data Containers/FullAutoBlockShootActionSO.cs` | Adopts `bulletAction.Spread` — the turret authors no cone. |
| `R_VesselActions/Executors/FullAutoActionExecutor.cs` | Accumulator cadence + per-round deflection for the bullets. |
| `R_VesselActions/Executors/FullAutoBlockShootActionExecutor.cs` | Same for the turret, plus the roll-preserving shot rotation. |
| `Controller/Projectiles/Gun.cs` | `FireGun(..., aimDirection)` — the gun is *handed* a direction; it owns no spread policy and rolls no dice. |
| `Controller/Managers/PrismSpatialIndex.cs` | `QuerySegment` — the swept counterpart of `QuerySphere`, plus the public `DistanceToSegmentSq` metric. |
| `Controller/Projectiles/Projectile.cs` | `sweptPrismDetection`, `SweepPrismsAlong` (nearest-first dispatch, contact-point repositioning), `CacheSweepRadius`. |
| `Controller/ImpactEffects/Impactors/ImpactorBase.cs` | `AcceptImpacteeFromSweep` + `IsSweepDispatch` — the swept analogue of the shell tier's entry point. |
| `Controller/ImpactEffects/Impactors/ProjectileImpactor.cs` | Suppresses the trigger's prism case when the sweep owns it. |
| `Controller/IO/HapticController.cs` | `PlaySpray(strength01)` + the extended gate + the buzz clip. |
| `_Scripts/Tests/Editor/GunSpreadMathTests.cs` | Ramp, cap, cone containment, pole safety, determinism, distribution, roll preservation. |
| `_Scripts/Tests/Editor/SparrowRoundGrowthTests.cs` | The MASS growth curve: anchors, extrapolation, linearity, and that the hit radius tracks the visible cross-section. |
| `_Scripts/Tests/Editor/PrismSweptQueryTests.cs` | The point-to-segment metric: endpoint clamping, degenerate steps, and the shipped mid-step geometry PhysX was missing. |
| `_SO_Assets/VesselActions/Sparrow/FullAutoAction.asset` | The shipped numbers. |
| `_Prefabs/Spacevessels/Sparrow.prefab` | `GunSprayAccuracy` executor + resized pools. |
| `_Prefabs/Projectile/SparrowProjectile.prefab`, `_Prefabs/Trails/Prisms With Pools/Sparrow Projectile Prism.prefab` | `sweptPrismDetection: 1`. |

## Tuning knobs

Everything that moves **both** fire modes lives on `FullAutoAction.asset`:

| Knob | Shipped | Effect |
|---|---|---|
| `firingRate` | **90** | Volleys/s for guns **and** turret. The single lever for volume of fire — and for the turret's permanent-mass rate. |
| `growthFactorAtRestingMass` | **3** | How many times its launch cross-section a round swells to by the end of its flight at resting Mass. |
| `growthFactorAtFullMass` | **6** | The same at Mass 10; the curve is linear in level and extrapolated to [-5, 15]. |
| `spread.onsetSeconds` | **0.12** | Grace window of perfect accuracy at the start of every pull (~11 volleys / 22 rounds). Size it to the burst length that should stay surgical. |
| `spread.growthDegreesPerSecond` | **1.0** | How fast the cone opens. Full at `onset + max/growth` ≈ **1.62 s**; only 0.88° after a full second. |
| `spread.maxHalfAngleDegrees` | **1.5** | The cap (≈1.9 u radius at the SPACE-0 range of 72 u). Raise it and held fire starts missing what you aimed at; drop it to 0 to disable spread entirely (sanctioned opt-out). |
| `spread.distributionBias` | **0.5** | 0.5 = uniform over the disc (even saturation). 1.0 = dense core + thin halo. |
| `spread.hapticFloor01` | **0.15** | Buzz strength before any accuracy is lost — above zero so the gun is felt from round one. |
| `spread.hapticIntervalAtRest` / `AtMaxSpread` | **0.10 / 0.045** | Pulse cadence at each end of the ramp. Keep the max-spread value above ~0.04 s: NiceVibrations holds one clip at a time, so pulses closer than the clip just cut each other off. |

Pool sizes on `Sparrow.prefab` (resized for the doubled rate — the fire rate is the only reason
they are what they are):

| Pool | was | now | why |
|---|---|---|---|
| bullets (`ProjectilePoolManager`) | 25 / 100 / 25 | **90 / 320 / 90** | 180 rounds/s × 0.3 s lifetime ≈ 54 live at once. |
| turret prisms (`BlockProjectilePoolManager`) | 40 / 200 / 90 | **120 / 600 / 260** | Anchored prisms are **never returned**, so every shot past the buffer is a fresh `Instantiate`. |

## Costs this pass takes on, deliberately

- **Turret stance now lays ~180 prisms/s** of permanent world mass (was 60), at ~360 volume/s at
  base scale before the MASS stretch. That is the documented price of "the same rate as its
  bullets", and `firingRate` remains the single lever — **do not** add a turret-only divisor,
  which is exactly the drift the shared-cadence pass closed. Judge it against the host cell's
  phase ladder in play.
- **Dog Fight pace changes a LOT, and by more than the rate alone.** A bullet hit scores 1 against
  a 120-point target; rounds downrange are 3× the pre-branch figure AND each one now actually
  tests its whole path, so landed hits per second rise by considerably more than 3×. Expect the
  point target to need raising. It is authored (FrogletTools ▸ Game Modes ▸ End Game Conditions,
  `GetDogFightPointTarget`), so retuning it is one field and needs **no** code change.
- **Swept queries cost one `QuerySegment` per live bullet per frame** (~54 concurrent at the
  shipped rate). The segment's AABB is thin, so each walks only a handful of 8 m buckets — but it
  is new per-frame work in the projectile path and worth a profiler glance under a sustained hold.

## In-editor verification

Scene: `MinigameDogFight` (best — it has other pilots to shoot at) or `MinigameWildlifeLiberation`.
Sparrow, fire on input 1.

> **The haptic half needs a gamepad or a device.** On a bare desktop editor there are no motors,
> so "I feel nothing" carries no information about whether the ramp is wired. Connect a gamepad
> (the `GamepadRumble` path drives Input System motors) or run on device. The **cone** is fully
> visible on desktop — the tracers fan out — so the spread mechanic can be judged without one.

1. **Tap accuracy.** Tap the trigger repeatedly. Every burst must be a tight line — no visible
   fan at all. This is the onset window; if short taps spread, `onsetSeconds` is too small.
2. **The cone opens.** Hold the trigger on a distant wall and watch the impacts: a point that
   grows into a widening circle over ~1.4 s, then **stops growing**. If it never stops, the cap
   is not being applied.
3. **Release resets.** Hold until fully open, release for a fraction of a second, re-pull. The
   first rounds of the new pull must be dead-on again.
4. **Stance flip does NOT reset.** Hold fire while flying, open the cone fully, then toggle
   Turret Stance (input 6) **without releasing the trigger**. The prisms must start laying at the
   *open* cone — if they come out in a tight line, the deferred-reset hand-off has regressed.
5. **Turret prisms scatter.** Stopped, hold fire: prisms must anchor in a scattered volume, not a
   line — and each one must still point along its own flight (no visible twist/roll on the long
   axis).
6. **Rate.** 180 rounds/s should read as a solid stream. Then **cap the editor to 30 fps**
   (Game view ▸ or `Application.targetFrameRate = 30`) and confirm the stream looks the same
   density — that is the accumulator working. Before this pass it would have halved.
7. **Haptic ramp** (gamepad/device). Hold the trigger: a light buzz from the first round that
   climbs in strength *and* rate for ~1.6 s, then holds steady at the cap. Release → silence.
8. **Haptics stay legible.** While spraying, ram a prism with the hull — the punish **thud** must
   cut cleanly through the buzz. Confirm the buzz never plays for a remote player's Sparrow, an
   AI dogfighter, or the Menu_Main autopilot.
9. **Settings.** Haptics off / level 0 in Settings → the buzz stops with everything else.
10. **No hitching under a long hold.** Both fire modes, 10+ second holds, profiler open. The pools
    were resized for this; if the turret still spikes, raise `bufferSizeTarget` / `maxAddsPerFrame`
    on the Sparrow's `BlockProjectilePoolManager` further.
11. **Console clean.** No `[FullAutoActionExecutor]` / `[FullAutoBlockShoot]` / `[PrismClock]`
    errors during a sustained hold.
12. **MPPM two-client.** Both clients see a spraying Sparrow. Turret prisms land in *approximately*
    the same places on both — see Follow-ups; exact agreement is not expected today.
13. **Asset import.** `FullAutoAction.asset` shows the new **Accuracy** foldout with the shipped
    numbers, and `Sparrow.prefab`'s `VesselActions` node has a **GunSprayAccuracy** child listed in
    `ActionExecutorRegistry._executors` (5 entries).

## Follow-ups

- **Cross-peer turret prism placement.** The deflection is deterministic in the shot serial, so
  peers agree exactly as long as their shot counts agree — but the loops run on each peer's own
  clock, so counts drift and the spread makes that drift *visible* (up to 4°) instead of
  sub-degree. This is the same open item `SPARROW_TURRET_STANCE.md` already records ("the turret's
  prism spawning is not networked at all today"); it settles when the stance becomes
  server-authoritative, not before.
- **No HUD readout.** The cone is visible in the tracers and audible in the hands, but there is no
  on-screen indicator. If one is wanted, the natural home is a ring on the **Space** ability icon
  (Pulsefire Cannons) — which makes it a live-gauge icon and pulls in the rule-9 obligations
  (`tintIconOnUpgrade = false` + a `SetAbilityUpgraded` override re-anchoring rest scales).
  Deliberately out of scope here.
- **Vessels and mines still tunnel.** Swept detection covers prisms only. A hull is a much bigger
  target so it matters far less, but a Sparrow round crossing 6.25 u per frame can still slip past
  a vessel at a glancing angle — and in Dog Fight that is a missed point. Generalizing the sweep to
  the whole impact system (and to every other fast projectile in the game, not just the Sparrow's)
  is the natural next step; it is opt-in per prefab precisely so that can be done deliberately.
- **`placementImmunitySeconds` is now doing more work again.** Round 6 noted 0.2 s was probably too
  long once the hit sphere shrank; at 120 shots/s the shot-vs-shot spacing it guards is tighter
  again. Re-judge it in play rather than assuming either direction.
- **`MaxVolleysPerTick = 4` is a code constant, not an authored field.** It only binds below
  ~23 fps at the shipped rate. If a rate above ~240 volleys/s is ever wanted it must move with it —
  hoist it onto `GunSpreadProfile` at that point rather than raising it blind.
