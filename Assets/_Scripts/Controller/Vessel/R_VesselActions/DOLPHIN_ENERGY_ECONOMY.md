# Dolphin — the energy economy, the drift boost, and the four gauges

Design owner: Garrett. The map is **APPROVED + SHIPPED**
(`Assets/Resources/ElementalAbilityMaps/Dolphin.asset`) — that asset and this file are the
record. Element→ability table and the no-double-dip rule live in
`Docs/ElementalAbilitySystem/FLEET_MAPS.md § Dolphin`; this file is the mechanics detail
beside the code.

---

## 1. The spine: one meter, one sink

The Dolphin has **two** resources and they are not the same thing:

| slot | name | who writes it | passive gain |
|---|---|---|---|
| 0 | **Energy** | skim / prism ram / crystal impact | none |
| 1 | **Boost** | `ChargeBoostActionExecutor` + the prism ram | none |

**A prism ram costs HALF of BOTH meters.** Energy and Boost are separate resources with separate
sinks, but they share one punish: fly into mass and you lose half of everything you banked. The
two halves are authored as two effects in `DolphinImpactorDataContainer.vesselPrismEffects`
(`DolphinVesselChangeResourceByPrismEffect` on slot 0, `DolphinVesselChangeBoostByPrismEffect`
on slot 1), each with its own `retainedFraction` (0.5) so they can be tuned apart if the ram
turns out to bite harder on one than the other.

### A ram also costs SPEED — on the fleet's terms, not the Dolphin's

The Dolphin shipped with **no `VesselChangeSpeedByPrismEffectSO` in its chain at all**, so a
prism collision — danger prism included — did nothing whatsoever to its speed. The asset existed
(`DolphinVesselChangeSpeedByPrism`, authored at `duration 0.5` / `maxSlowStrength 0.8`) and was
referenced by no container: authored once, never wired, and invisible because a vessel that
simply doesn't slow reads as a vessel that's fast.

It now carries the **Squirrel's exact numbers**, because a prism is a prism and the collision
read should not depend on which hull hit it:

| | normal prism | danger prism |
|---|---|---|
| slow strength | `min(volume × 0.1, 0.5)` | `0.5 × 3` → clamps to a **full stop** |
| recovery | **1 s**, linear back to full throttle | **3 s**, linear |

`massScaling: 0.1` against `maxSlowStrength: 0.5` means anything of volume ≥ 5 saturates, so in
practice a normal prism halves the throttle for a second and a danger prism parks you for three.
Both recover linearly from full strength (`VesselTransformer.ApplyThrottleModifiers` lerps the
modifier back to 1 across its duration) — the bite is instant, the climb out is not.

Two properties come free with the shared effect and are the reason to use it rather than author
a Dolphin-specific slow:

- **Your own trail doesn't brake you** — `VesselChangeSpeedByPrismEffectSO` skips non-danger
  prisms of your own domain. You skim your own mass, you don't plow through it.
- **Danger is not safe to its own domain** (locked design), so the full stop lands on the owner
  of the danger trail exactly as hard as on anyone else.

The Dolphin does **not** take the Squirrel's `VesselResetBoostPrismEffect` (which zeroes boost
outright). Its boost punish is the halving above — a deliberately different, gentler design for
a vessel whose boost is bought with drift-seconds rather than picked up.

**Energy** is banked by skimming and spent in ONE shot on a crystal:

| event | effect on Energy | authored in |
|---|---|---|
| skim a prism | **+0.006667** (max 1.0, so 150 skims fills it) | `DolphinSkimmerChangeResourceByPrismEffect` |
| ram a prism | **halved** | `DolphinVesselChangeResourceByPrismEffect` |
| hit a crystal | **set to 0** — spent entirely | `DolphinVesselChangeResourceByCrystalEffect` (`_overrideAmount`) |

Energy has **no passive regeneration** (`resourceGainRate: 0`), which is what makes the skim
the only way to arm the blast.

A danger prism pays the same as any other prism here. The Dolphin briefly carried a Time-5
"Live Current" bonus (`×3` on danger skims, via
`SkimmerChangeResourceByPrismEffectSO._dangerBonusElement`); Time 5 was re-scoped on 2026-08-18
to **Drift Ward** (elemental-debuff immunity while drifting), so `_dangerBonusElement` is back
to `None` on `DolphinSkimmerChangeResourceByPrismEffect` and Time 5 grants exactly one upgrade.
The generic 10× danger multiplier on `SkimmerBoostPrismEffect` is a different effect on a
different resource (boost, not energy) and is gated on CHARGE — it is untouched by this.

### Energy IS the jaw gape

The crystal impact releases a conic AOE whose **capsule length** lerps with energy:

```
MaxScale   = lerp(400, 2080, energy) × sizeMultiplier      (capsule LENGTH = cone base DIAMETER)
CoreScale  = 320                     × sizeMultiplier      (capsule DIAMETER — fixed)
height     = 2400                    × sizeMultiplier      (axial reach)
gapeAngle  = atan((MaxScale / 2) / height)
           = atan(lerp(400, 2080, energy) / 4800)           ← sizeMultiplier cancels
coreAngle  = atan((CoreScale / 2) / height) = atan(1/15) = 3.81°   (never moves)
```

`sizeMultiplier` is the SPACE scaling, and it multiplies **all three** — self-similarly —
precisely so it cannot steal the angle energy just set. So:

- **Energy owns the GAPE** (4.76° empty → **23.43°** full, per side).
- **Space owns the SIZE** (×0.35 … ×2 of the whole blast, angles unchanged).

`AOEConicExplosion.prefab` authors `height: 2400`; `DolphinVesselExplosionByCrystalEffect`
authors `_minExplosionScale: 400` / `_maxExplosionScale: 2080` / `_coreExplosionScale: 320`.

**Change any of them and `RiptideAnimation.MinJawAngle` / `MaxJawAngle` must follow** (§3).

`_coreExplosionScale` is authored **separately from** `_minExplosionScale` on purpose. They used
to be one number, which forced the resting blast to be a sphere (length == diameter). Splitting
them lets the blast rest as a **short capsule**: 400 long by 320 wide, so it already has a 40-unit
half-length at empty energy.

#### The destruction volume is a CAPSULE sweep, not a circular cone

The blast does not open out equally in every direction — it opens the way the **jaws** open.
The swept volume's cross-section at axial depth `s` is a 2D **stadium**: a disc of the
closed-jaw radius, dragged along the gape axis (the container is spawned with the ship's
rotation, so the authored `AOEConicExplosion.gapeAxis = (0,1,0)` is ship UP — exactly the axis
`RiptideAnimation` pivots `jaw.u` / `jaw.b` across).

```
core half-width  = 3.81° worth of s                   fixed — never grows with energy
gape half-length = (gapeAngle − 3.81°) worth of s     all of what energy buys
tip extent       = core + gape = gapeAngle worth of s
```

Measured at the base plane (`s = height`), that is:

| energy | capsule length | capsule radius | half-length | gape | across the beam |
|---|---|---|---|---|---|
| empty | 400 | 160 | **40** | 4.76° | 3.81° |
| full | 2080 | 160 | 880 | **23.43°** | 3.81° |

So at **empty** energy the blast is already a short capsule — a stubby lozenge, not a ball — and
at **full** energy it is a fan: **23.43° across the gape, still 3.81° across the beam.** The
capsule's tips land exactly on the rendered cone's base circle, so the damage volume stays
inscribed in the cone the player sees; it simply no longer fills it off-axis.

Setting `_coreExplosionScale` to 0 collapses the capsule back to the plain circular cone (radius
== the empty length / 2, zero half-length at rest) — that is the fallback every non-conic blast
takes, and the shape this path had before the capsule landed.

Both the Burst query (`AOEConicSweepQueryJob`, one point-to-segment distance instead of a
point-to-axis one — same cost class) and the trigger collider carry the shape: the trigger is a
`CapsuleCollider` whose radius is pinned to the core and whose length spans the base diameter
(`AOEConicExplosion.UpdateCapsuleTrigger`), so the two can't diverge. **Do not put a
`SphereCollider` back on `AOEConicExplosion.prefab`** — a dev-build warning fires if a blast
opens a gape without a capsule trigger, because then vessel impacts silently keep the old
circular envelope.

Everything else about the blast is untouched: the rendered cone, the wavefront speed, the
opacity fade, the impulse/debris contract, friendly fire, and the Space reach multiplier.

---

## 2. The boost is bought with the drift, not with time

`LeftStickAction` (the Dolphin's drift) starts **four** actions, one of which is
`ChargeBoostAction`. `StartAction` → `BeginCharge`, `StopAction` → `BeginDischarge`:

- **hold drift** → the Boost meter fills over `chargeTimeToFull: 3.636` s (×1.5 at Time 10).
- **release drift** → it drains over `dischargeTimeToEmpty: 2.5` s, `BoostMultiplier` riding the
  remaining charge from `maxBoostMultiplier: 2.259` back down to 1.

### The speed ladder, and why the peak multiplier is squared

`VesselTransformer.ComputeThrottleTarget` is `XDiff × ThrottleScaler × ThrottleScalerMultiplier
× CurrentBoostAmount() + MinimumSpeed`, and `CurrentBoostAmount()` multiplies **two** boost
terms during a discharge: `BoostMultiplier` (which the discharge routine rewrites every tick as
it decays 2.259 → 1) **and** `ChargedBoostCharge` (pinned at the value the charge ended on, so
2.259 off a full meter). The authored peak is therefore **squared** at the top of a discharge —
`maxBoostMultiplier²`, not `maxBoostMultiplier`. That is shipped behaviour on both the executor
and the legacy `ChargeBoostAction`, and it is what the numbers below are tuned against; do not
change it as a side-effect of a tuning pass.

| quantity | formula | value |
|---|---|---|
| max cruise speed | `ThrottleScaler(68) × 1 + MinimumSpeed(10)` | **78** |
| max boost speed | `68 × 2.259² + 10` | **357** |
| charge fill rate | `1 / chargeTimeToFull` | 0.275 /s |
| boost drain rate | `1 / dischargeTimeToEmpty` | 0.40 /s |

`ThrottleScalerMultiplier` is authored `Enabled: 0` on the Dolphin, so it evaluates to its
serialized `Value: 1` and the map's Time multiplier reaches boost speed only through
`CurrentBoostAmount` (also 1 at the resting level). `MinimumSpeed` is deliberately **not**
scaled with the top speed — the floor is the drift/idle speed and was left as authored.

Speed is a platform-law input, not a private number: the speed tunnel
(`Docs/SPEED_TUNNEL.md`) maps speed to FOV **absolutely and fleet-wide**, so a faster Dolphin
reaches deeper into the tunnel because it *is* faster. That is the intended coupling — there is
no per-vessel window to re-normalize.

**Do not re-add a passive `resourceGainRate` to the Boost slot, and do not re-add
`rechargeCooldownSeconds`.** Both were present and both broke the stated design in the same
way — the meter filled (or refused to fill) for reasons that had nothing to do with drifting:

- a 0.1/s trickle meant the ring climbed while flying straight, and boost was available to a
  pilot who never drifted;
- a 4 s recharge lockout meant that for four seconds after every boost, *holding the drift
  banked nothing* — the trickle was the only thing hiding it, so removing only the trickle
  left the ability with no working fill path at all.

They have to be reasoned about together. Both are now `0`.

`BeginCharge` also clears `BoostMultiplier` / `IsBoosting`: re-drifting interrupts a running
discharge, and cancelling that task only throws *inside* the loop — it never reaches the tail
that restores the speed. Without the clear, anyone who drifted twice in a row kept a partial
boost multiplier permanently.

### A ram halves the boost — and the meter is only half of "the boost"

`DolphinVesselChangeBoostByPrismEffect` scales resource slot 1 by `retainedFraction`, and that
alone would barely be felt mid-boost, because the meter is not the only thing driving the speed.
`CurrentBoostAmount()` above multiplies **two** terms during a discharge and only one of them
re-reads the meter:

| term | who writes it | re-reads the meter? |
|---|---|---|
| `BoostMultiplier` | the discharge loop, every 0.1 s tick | **yes** — self-corrects |
| `ChargedBoostCharge` | pinned at the value the CHARGE ended on | **no** — never read again |

`BoostMultiplier` therefore needs nothing: halving the meter halves it on the next tick, for
free. The **pinned snapshot does** — left alone it keeps paying full price on half the product,
and nothing ever re-reads it, so a ram mid-boost would barely be felt. The effect scales it by
the same fraction, which is exact without any reference to `ChargeBoostActionSO`: the term is
`1 + (maxBoostMultiplier − 1) × meter`, so scaling the meter by `f` is scaling its distance
above 1 by `f`. It is scaled **only while `IsChargedBoostDischarging`** — the one state
`CurrentBoostAmount` reads it in; outside a discharge it is stale bookkeeping that the next
`BeginCharge` overwrites anyway.

**`BoostMultiplier` is deliberately never written by this effect**, and that is not an
optimization — it is a serialized, *authored* field on `VesselStatus` (4 on the Dolphin) that
boost sources fall back to when they don't write it themselves (`BoostActionSO` only flips
`IsBoosting`; `VesselResetBoostPrismEffectSO` restores it to an authored base). Scaling it in
place would ratchet that authored number toward 1 a little further on every ram, permanently,
with nothing in the game to restore it — a creeping nerf disguised as a punish. The meter is
the only durable thing a ram may touch.

**Ramming while still DRIFTING is repaid, and that is intended.** The charge loop is running, so
it refills the halved meter from where the ram left it and re-derives `ChargedBoostCharge` along
the way — a ram taken mid-drift costs the pilot drift-*seconds*, not a bank. The pilot is still
doing the thing that banks boost; there is no reason for the meter to stay punched while they do
it. The punish is durable in the two states that matter: mid-discharge (below) and between
boosts, where nothing refills it.

Concretely, ramming at the peak of a full discharge: meter 1 → 0.5, `ChargedBoostCharge`
2.259 → 1.630 immediately, `BoostMultiplier` 2.259 → 1.630 within one 0.1 s tick — speed factor
5.10 → 2.66, and the discharge runs out in half the time it had left. The HUD's boost ring
follows for free: `Resource.CurrentAmount`'s setter always raises `OnResourceChange`, which is
what `DolphinVesselHUDController.PushDriftBoost` binds to, so the ring drops on the ram whether
the executor or an impact effect wrote the meter.

### The drift is a momentum-preserving slide — the whole velocity is frozen, not just its direction

The Dolphin authors `driftDamping: 0` (`DolphinDriftAction.asset`), so its drift already froze the
velocity's **direction**: `MoveShip` stops re-pointing `Course` at `transform.forward` and flies the
heading the vessel carried in while the hull rotates freely on top of it. Its **magnitude** kept
moving, though — `AdvanceSpeed` went on tracking `ComputeThrottleTarget()` every frame, so the
throttle stick (and any boost state change) still stretched and shrank the slide underneath the
locked heading. Half a lock reads as a bug, not a mechanic.

`VesselTransformer.holdSpeedWhileDrifting` (authored **on** for the Dolphin, off for every other
vessel) closes the other half:

| | before | now |
|---|---|---|
| velocity direction | locked at drift start (`driftDamping: 0`) | unchanged |
| velocity magnitude | throttle-driven, live | **latched at drift start, held for the drift** |
| throttle during drift | drives speed | **inert** — the target is still computed, it just never reaches `speed` |

Mechanically: `BeginDrift` → `RefreshDriftSpeedHold()` latches the current smoothed cruise `speed`
on the **rising edge** of the hold, and `AdvanceSpeed` pins `speed` to that value until `EndDrift`
releases it. The pin sits in `AdvanceSpeed` rather than in `ComputeThrottleTarget` because
`AdvanceSpeed` is the one path *every* transformer's `MoveShip` runs through — a subclass that
overrides the target (`SingleStickVesselTransformer`) is covered without knowing drift exists.

Four things are deliberately **outside** the hold:

- **`throttleMultiplier`** (the `ModifyThrottle` channel) stays live, so a danger prism's full-stop
  slow bites a drifting Dolphin exactly as hard as a flying one. Danger prisms are not safe to
  anybody (locked design) and a drift is not a shield. Mechanically this is `MoveShip` applying
  `speed * throttleMultiplier` *after* `AdvanceSpeed`'s `_driftSpeedHeld` early-return, so the
  hold pins the cruise speed and the modifier still scales the frame's output.
  **This clause was aspirational until the speed effect was wired (§1).** The hold was built to
  leave the channel live, but nothing on the Dolphin was calling `ModifyThrottle` on a prism
  collision, so "the vessel still slows mid-drift" could not have been observed — a good reminder
  that a correctly-designed passthrough proves nothing if no one is pushing anything through it.
- **`velocityShift`** (the `ModifyVelocity` channel) stays live — knockback, dodges and AOE impulses
  still displace a drifting vessel.
- **`_speedTrackingRate`** is untouched, so a ramp boost mid-ramp resumes on release instead of
  being silently swallowed by the pinned value.
- **The release**, not the ease-out. `EndDrift` hands the throttle back the instant the pilot lets
  go — the same instant `BeginDischarge` starts, which has to be able to accelerate immediately.
  (The non-gamepad course ease-out keeps easing after that; only the speed unlocks early.)

The hold is **binary**, while the course lock is analog (`driftAmount = clamp01(triggerSum)`): on a
gamepad the speed latches the moment the left trigger crosses the deadzone, at which point the
course is only fractionally locked. That is the deliberate simple reading of "lock the magnitude";
if a feathered trigger ends up wanting a feathered lock, the blend point is
`RefreshDriftSpeedHold` → `AdvanceSpeed` (`Lerp(target, held, driftAmount)`), not a new field.

**Known consequence — the drift now carries boost speed.** `BeginCharge` kills `BoostMultiplier` /
`IsBoosting` at the top of every drift, so before this change re-drifting during a discharge bled
the boost speed away over the next second. Now that speed is what gets latched: drift → release →
re-drift *at the peak of the discharge* pins the vessel near **357** for as long as the drift is
held, while banking the next boost. If that reads as a ratchet in play, the fix is a ceiling on the
captured value (clamp `_heldDriftSpeed` to the unboosted cruise target, 78), not the removal of the
hold — but it is a real balance change and wants a play-test before it is decided.

---

## 2a. The drift is a momentum LOCK — and why it has to be

The Dolphin runs the **vector flight model** (`vectorFlightModel: 1`) with
`driftThrottlePolicy: Locked`. Full model, the no-drift identity proof and the constraints it had
to respect are in `SQUIRREL_DRIFT.md` §3–§4; this section is the Dolphin's half and the reasoning
that pins it here.

### The requirement

**Flying straight at max speed and pulling the drift trigger must not cost you any speed.** You
entered the drift fast; you keep it.

### Why the shipped Dolphin failed that, long before any of this

The drift is the boost charge (§2), so pulling the trigger calls
`ChargeBoostActionExecutor.BeginCharge`, and that method clears `BoostMultiplier`, `IsBoosting`
and `IsChargedBoostDischarging` outright. It has to: re-entering the charge cancels a running
discharge, a cancelled UniTask never runs its tail, and without the clear a
drift → release → drift left the multiplier frozen forever — a permanent free speed bonus.

The side effect is a **cliff in the throttle target**. `CurrentBoostAmount()` collapses to 1 the
instant you drift, so `ComputeThrottleTarget()` falls from the boosted peak
(`68 × 2.259² + 10 = 357`) to plain cruise (`68 × 1 + 10 = 78`).

On the **scalar** model that cliff *is* a slowdown, unavoidably: there, `speed` is a value that
chases the target every frame, so a target drop drags the speed down with it (357 → 350 on the
first frame, ~139 within a second). **No tuning fixes this on the scalar path** — the speed has
nowhere else to live. It is the same class of problem as the drift's thrust direction: the model
cannot express what the design wants.

None of the drift's other three actions (`DolphinDriftAction`, `DriftTrailAction`,
`ShardToggleAction`) touch speed, and the drift trigger does not feed `XDiff` (that is stick-derived;
`LeftStickAction` is raised by the left **trigger**). The boost cancel is the whole of it.

### What Locked does

Under the vector model, speed is **state**, not a tracked target. `Locked` sets nose acceleration
to zero for the drift's duration, and the Dolphin's authored grip is **0**
(`DolphinDriftAction.driftDamping: 0`), so the velocity vector is left completely untouched —
**direction and magnitude both**:

| entering a drift at a boosted 357 u/s | frame 1 | frame 60 | frame 240 |
|---|---|---|---|
| scalar (the shipped defect) | 350.0 | **139.1** | → 78 |
| vector + `Live` | 350.0 | 237.5 | 62.7 |
| **vector + `Locked` (shipped)** | **357.0** | **357.0** | **357.0** |

Hold the drift and you keep exactly the momentum you entered with, aimed exactly where you entered
it. Release, the discharge raises the target again, and nose acceleration resumes from the speed
you still have.

That is not a consolation prize for the fix — it is the better mechanic. The Dolphin's drift is
already a commitment (it is how you bank the boost), and freezing momentum makes the commitment
concrete: you are spending your line, not your speed.

Note `Live` does **not** satisfy the requirement. Its nose thrust tracks the collapsed target, which
above that target means *negative* acceleration — a gentler version of the same slowdown.

### The round-1 miss (do not re-derive this)

`Locked` shipped once, was reported as "loses a ton of speed when the drift is initiated" and "seems
to control its speed during the drift", and was reverted to the scalar path. **Both symptoms were a
bug in the new model's drift overshoot ceiling, not in the policy**: it clamped `|v|` to
`ComputeThrottleTarget() × 1.25` outright, so the frozen 357 was immediately crushed to ~55 (the
collapsed target × 1.25), and because that ceiling is computed from `XDiff` the scissor throttle
appeared to be a speed dial. The ceiling now takes the pre-thrust speed as a floor — it bounds gain
and never brakes (`VesselTransformer.ShapeSpeed`) — and with it fixed, `Locked` does exactly what it
says. The revert was the wrong call; this section exists so it is not made twice.

### What did NOT change

The drift's other three actions are untouched, and so is the charge/discharge economy — the boost
still fills while drifting and discharges on release. `throttleMultiplier` and `velocityShift` stay
live throughout, so a drifting Dolphin is **not** immune to danger prisms or knockback.

Watch the discharge on release: the old concern was that a hold had to be released at `EndDrift`
rather than at the end of the ease-out *because that instant starts the discharge and it must
accelerate immediately*. Under the vector model the drift simply ends, nose acceleration resumes,
and the discharge has already raised `ComputeThrottleTarget` — and you are starting from the speed
you kept, not from a decayed one, so there is less to make up than there ever was.

### Correcting the record

Earlier notes described a `holdSpeedWhileDrifting` boolean that disabled the throttle for the
drift's duration, with `RefreshDriftSpeedHold` / `_driftSpeedHeld` / `_heldDriftSpeed` machinery.
**No such flag has ever existed in this repository** — absent from `VesselTransformer.cs`, from that
file's entire git history, and from `Dolphin.prefab`. What shipped was the plain scalar model with
the throttle live throughout the drift, and the boost-cancel cliff described above. There was never
a workaround to retire; there was a defect to fix.

---

## 3. The hull reads out the blast

`RiptideAnimation` opens the model's jaws with Energy, so a pilot can see how wide their next
blast is without looking at the HUD. Two authored angles bracket the gape and **both must equal
the blast's gape half-angle** at their end of the range:

| | authored | = | source |
|---|---|---|---|
| `MinJawAngle` | **4.7636°** | `atan((400 / 2) / 2400)` | `_minExplosionScale` / cone height |
| `MaxJawAngle` | **23.4287°** | `atan((2080 / 2) / 2400)` | `_maxExplosionScale` / cone height |

`MaxJawAngle` was 21° against an 18.43° cone until this was measured, and 18.43° against a
23.43° blast until the capsule length went to 130%.

Since the blast became a capsule sweep (§1), this is no longer just a matched number: the jaws
and the blast open **across the same axis**, so the hull's silhouette IS the blast's silhouette
in that plane. Perpendicular to the gape the blast stays at its 3.81° core. **The jaws are never
fully shut** — at empty energy the blast is a short capsule with a real 4.76° gape, and a closed
jaw would misreport it as nothing.

The HUD's jaw icon takes its whole range from `RiptideAnimation.MinJawAngleDegrees` /
`MaxJawAngleDegrees` (`DolphinVesselHUDController` → `view.SetJawAngleRange`), so the cockpit and
the hull cannot disagree; the blast is the only third party, hence the rule above.

**The linear approximation is gone.** The jaws used to lerp `0 → MaxJawAngle` while the true
half-angle is `atan(lerp(min, max, e) / (2 × height))` — exact only at full energy, off by up to
~5° elsewhere. Both the hull and the icon now call the one shared
`RiptideAnimation.GapeAngleAt(t, min, max)`, which lerps the **tangents** and takes the
arctangent:

```
tan(angle(t)) = lerp(min, max, t) / (2 × height) = lerp(tan(minAngle), tan(maxAngle), t)
```

That identity is why the fix needs nothing but the two authored angles — the
vessel-animation → impact-effect dependency the old note worried about never has to exist.

The jaw meter is bound **symmetrically across `OnEnable`/`OnDisable`**, not
`Initialize`/`OnDisable`. It used to subscribe in `Initialize` only, so one disable/enable
cycle (pooling, vessel swap, HUD toggle, scene transition) dropped the subscription for good
and the gape froze.

---

## 4. The four gauges

Fleet-standard row (charge → mass → space → time, the same order as the element flowers), on
the Squirrel's exact anchor bands. `DolphinHUDVariant.prefab` is authored;
**FrogletTools > Vessels > Wire Vessel Ability Row** re-binds a broken one without
re-deriving the layout.

| slot | icon | shows |
|---|---|---|
| Charge | a generated blast-PROFILE capsule + a two-line living tally | the next blast's cross-section (extent = energy, roundness = Charge), and what the last blast did to pilots and creatures |
| Mass | omni-crystal icon | the seeding recharge, and — by colour — whether the next seed is a free-for-all crystal or a team-locked one |
| Space | the vessel's own jaw silhouettes + a prism tally | banked energy, as a gape — **lime when full** — and prisms the last cone claimed |
| Time | the vessel's own 11-step boost ring | the boost banked by drifting |

Two conventions this HUD deviates on, both deliberate:

- `tintIconOnUpgrade = false` — every icon here is a live gauge, so colour is already spoken
  for.
- `showUpgradeBadge = false` — the corner element badge was not asked for and these four
  icons are busy enough. **The persistent scale bump is therefore the only upgrade signal
  this vessel has**, which is why `SetDriftBoost` writes nothing but the ring's sprite: any
  per-event transform write on an icon wipes the bump.

*(The row above was re-cut on **2026-08-17**, when the whole elemental map was re-assigned so each
element owns one orthogonal dimension of the single crystal-blast act. Charge took the Echo Sight
and the blast's thickness, Mass took crystal seeding, Space narrowed to reach, and every slot moved
band. The **carry/yield pips are retired** along with Twin Seed — the ability plants exactly one
crystal per cycle at every level, and the Mass upgrade changes the crystal's TIER instead, which the
slot says in colour. Full record: `DOLPHIN_CRYSTAL_SEEDING.md` §8–§12.)*

### Why the boost ring writes nothing but its sprite

A swell keyed to the charge and a colour ramp both landed on **every** resource event. The
ring stuttered between discrete scales and killed its own tween doing it. The authored
eleven-sprite gauge is the whole readout.

### The jaws go lime when the blast is armed

The gape answers "how wide is my next blast"; it does **not** answer "am I done banking".
Reading a full meter off an angle means eyeballing 23.4° against 21° — so the last stretch of
the bank is also carried by colour: the jaw pair blends from its authored white to the CTA lime
across the top **15%** of energy (`jawArmingThreshold: 0.85`) and sits solid lime at full.

Three things make this legal on a HUD whose upgrade tint is deliberately off (§4):

- **The lime is not authored here.** It is `ElementalBarsConfigSO.limeColor` — the *same* value a
  maxed element flower shows — so "this is topped out" is one colour across the whole HUD. Do not
  copy the literal into this view; a second copy is a second thing to retune.
- **It cannot contest the upgrade signal.** The row's Time icon is `JawIcon`, a fully transparent
  container (`a: 0`); the visible gauge is the two jaw halves beneath it. The base class tints the
  icon, this view tints the halves, and `tintIconOnUpgrade` is off regardless — so the persistent
  scale bump stays the sole upgrade signal, exactly as §4 requires.
- **It is a ramp, not a switch.** A hard flip at exactly full would pop on a single skim out of
  ~150 and vanish the instant you ram a prism (a ram halves energy). The 15% ramp reads as the
  meter filling in.

### Why a skim punches the jaws

One skim moves the gape by a 150th of its range — about 0.12°, invisible. The other signals
wired to a skim are the forcefield crackle across the skimmer sphere and a haptic pulse that
is a **no-op on desktop**. So the discrete event gets its own beat:
`DolphinVesselHUDController` treats an energy **rise** as the skim (nothing else raises
energy — the blast spends it all, a ram halves it) and punches the jaw pair.

---

## 5. The skimmer, and the trap that hid it

The Dolphin's skimming was dead from the day it was authored, and produced **no error**:

> `VesselController.Initialize` initializes **only** the skimmers reachable through
> `VesselStatus.NearFieldSkimmer` / `FarFieldSkimmer`, and `SkimmerImpactor` drops every
> contact while `skimmer.IsInitialized` is false.

The Dolphin carried an active `EnergySkimmer` doing the physics *and* a disabled legacy
nested `Skimmer.prefab` instance — and `_nearFieldSkimmer` pointed at the **disabled** one.
Perfect wiring on the object that never ran; no wiring on the object that did.

**FrogletTools > Vessels > Audit Vessel Skimmers** checks this from assets alone (no play
mode): assignment, active state up the whole ancestor chain, the
impactor/`ImpactCollider`/trigger-collider/`Rigidbody` the trigger path needs, and whether
the container holds any prism effects. *(Serpent still fails it — its `_nearFieldSkimmer`
resolves to an inactive `VacuumSkimmer`.)*

### The crackle takes three pieces, not one

`SkimmerForcefieldCracklePrismEffectSO.Execute` returns silently unless **all three** exist:

1. the effect in the skimmer's `SkimmerImpactorDataContainerSO`,
2. a `ForcefieldCrackleController` on the **impactor's own GameObject**,
3. an overlay `MeshRenderer` assigned to that controller.

The Squirrel gets (2) and (3) free because its skimmer *is* `Skimmer.prefab`, which carries
both. The Dolphin's `EnergySkimmer` is a standalone object and needed all three added. The
audit checks (2) and (3) whenever a container asks for (1).

### The crackle is the Dolphin's ONLY skim visual

The legacy `SkimmerFXPrismEffectSO` — the per-prism beam, marked `[Obsolete]` in code as
"replaced by `SkimmerForcefieldCracklePrismEffectSO`" — was added to the Dolphin's container
as the interim answer to "a skim produces no feedback at all", *before* the crackle was
wired. Once the crackle landed, both ran: a beam stretching from the hull to every prism in
the sphere **on top of** the crackle, which reads as noise on a vessel that skims ~150 prisms
to fill its meter. The beam is now removed from `DolphinSkimmerImpactorDataContainer`; the
container holds the resource gain, the haptic, and the crackle, and nothing else. The
Squirrel still runs both — this is a Dolphin decision, not a platform one.

### The skimmer no longer resizes itself on init

`Skimmer.ApplyScaleIfChanged` writes `localScale` from its `ElementalFloat` **even when
`Enabled` is off** — `EvaluateLive` returns the serialized `Value` in that case. The Dolphin
authored `Value: 30` against a transform of `20`, so merely *initializing* the skimmer grew
its reach by half. `Value` now matches the authored transform: world radius **10**, against
the Squirrel's resting 7.5.

---

## 6. In-editor verification

Play Menu_Main, enter freestyle on the Dolphin.

| check | expect |
|---|---|
| **FrogletTools > Vessels > Audit Vessel Skimmers** | Dolphin `NearFieldSkimmer: 'EnergySkimmer' OK` |
| **FrogletTools > Vessels > Audit Vessel Ability Rows** | Dolphin: map complete, 4/4 icons, order ✅ |
| fly through cell mass | crackle arcs across the skimmer sphere per prism; jaw icon punches; gape widens |
| fly through cell mass | **no beam** stretches from the hull to the skimmed prisms — the crackle is the only skim visual |
| at zero energy | jaws sit slightly open (4.76°/side), NOT shut — hull and Time icon agree |
| keep skimming | model's jaws open toward 23.4° per side (**~150 skims** to full); Time icon matches at every step |
| cross ~85% energy | Time icon's jaws start blending white → lime; solid lime at full |
| ram a prism at full | gape halves AND the jaws drop back to white |
| ram a prism | gape halves |
| bank a full boost ring, then ram a prism before releasing | ring drops to half a step-for-step; the following release peaks near cruise+half, not 357 |
| ram a prism at the PEAK of a discharge | speed drops within a tick, and the boost runs out in half the time it had left |
| ram a prism with an empty boost ring | nothing happens to speed — half of zero is zero |
| ram a prism WHILE holding the drift | ring drops, then climbs again from there — the ram cost drift-seconds, not the bank |
| ram prisms repeatedly, then trigger any OTHER boost source | it is as strong as it ever was — a ram scales the meter, never the vessel's authored `boostMultiplier` |
| hit a crystal | blast fires, gape snaps back to the 4.76° rest, Space icon flashes with a prism count |
| blast at full energy | destruction is a FAN — wide across the jaw plane, narrow across the beam |
| full throttle, no boost | `VesselStatus.Speed` settles at **78** (was 60) |
| drift at cruise, then work the throttle stick | speed does **not** move — heading swings, magnitude is pinned at the value it had when the drift began |
| drift from a slow crawl | it stays a slow crawl for the whole drift (the lock is "hold what you had", not "hold top speed") |
| release the drift | throttle authority returns immediately and speed resumes tracking (into the boost discharge) |
| ram a danger prism mid-drift | the vessel still slows — `throttleMultiplier` is outside the hold |
| ram an opposing normal prism | throttle drops to ~half instantly, climbs back over **1 s** — same feel as the Squirrel |
| ram a DANGER prism | **dead stop**, climbing back over **3 s** — same feel as the Squirrel, and it lands on the danger trail's owner too |
| ram your OWN (non-danger) trail | no braking at all — own-domain prisms are skipped |
| hold drift | boost ring steps up; release → speed rises then decays; ring empties |
| hold drift from empty to full | ring fills in **~3.6 s** (was 4) |
| release a full meter | speed peaks near **357** and takes **~2.5 s** to fall back (was 210 / 2 s) |
| fly straight without drifting | ring does **not** climb |
| drift, release, drift again, then release and fly straight | speed settles back to the ordinary 78 cruise — no stuck boost multiplier. (Note the second drift now HOLDS whatever the discharge had reached; the thing under test is that nothing is stuck once you stop drifting.) |
| Charge to level 5 | second crystal pip appears; each seeding cycle now plants two crystals (`DOLPHIN_CRYSTAL_SEEDING.md`) |

The **vessel silhouette** that used to sit in this HUD is gone — it had been dead since its driver
(`SilhouetteController`) became `ElementalBarsController`, but the GameObjects survived in 13 vessel
and HUD prefabs, still rendering a static ship outline nothing updated. Removed fleet-wide, along
with the dead `silhouette` / `silhouetteContainer` / `trailContainer` serialized keys and the
prefab-instance overrides that wired them. The empty `Silhouette` containers in the *minigame* HUD
family (`GameCanvas.prefab`, `Panels/MiniGameHUD.prefab`, `Panels/VesselHUD.prefab`) were left in
place: they hold no renderers, and `GameCanvas` is the shared prefab of `Docs/GAMECANVAS.md`.

Knobs, in order of likely tuning: `DolphinSkimmerChangeResourceByPrismEffect._resourceAmount`
(skim gain), `DolphinVesselChangeResourceByPrismEffect.retainedFraction` /
`DolphinVesselChangeBoostByPrismEffect.retainedFraction` (how hard a ram bites each meter),
`DolphinVesselChangeSpeedByPrism.maxSlowStrength` / `speedModifierDuration` (**currently pinned
to the Squirrel's values on purpose — moving either un-shares the fleet's collision read**),
`ChargeBoostAction.chargeTimeToFull` / `dischargeTimeToEmpty` /
`maxBoostMultiplier`, `DeployTeamCrystalAction.cooldown` / `minCooldown`,
`DolphinVesselExplosionByCrystalEffect._min/_max/_coreExplosionScale` (**then `MinJawAngle` /
`MaxJawAngle`** — `_coreExplosionScale` is the only one of the three that does NOT move a jaw
angle, since it sets the blast's width across the beam rather than its gape).
