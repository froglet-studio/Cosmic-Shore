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
| 1 | **Boost** | `ChargeBoostActionExecutor` only | none |

**Energy** is banked by skimming and spent in ONE shot on a crystal:

| event | effect on Energy | authored in |
|---|---|---|
| skim a prism | **+0.006667** (max 1.0, so 150 skims fills it) | `DolphinSkimmerChangeResourceByPrismEffect` |
| skim a DANGER prism, Time L5 active | **+0.02** (×3, so 50 danger skims fills it) | same asset, `_dangerBonusElement` / `_dangerBonusMultiplier` |
| ram a prism | **halved** | `DolphinVesselChangeResourceByPrismEffect` |
| hit a crystal | **set to 0** — spent entirely | `DolphinVesselChangeResourceByCrystalEffect` (`_overrideAmount`) |

Energy has **no passive regeneration** (`resourceGainRate: 0`), which is what makes the skim
the only way to arm the blast.

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
**FrogletTools > Vessels > Wire Dolphin Ability Row** re-binds a broken one without
re-deriving the layout.

| slot | icon | shows |
|---|---|---|
| Charge | omni-crystal + carry pips | crystals in hand, the carry limit, and the recharge fill |
| Mass | the vessel's own 11-step boost ring | the boost banked by drifting |
| Space | cone-blast icon + tally | prisms the last cone claimed |
| Time | the vessel's own jaw silhouettes | banked energy, as a gape |

Two conventions this HUD deviates on, both deliberate:

- `tintIconOnUpgrade = false` — every icon here is a live gauge, so colour is already spoken
  for.
- `showUpgradeBadge = false` — the corner element badge was not asked for and these four
  icons are busy enough. **The persistent scale bump is therefore the only upgrade signal
  this vessel has**, which is why `SetDriftBoost` writes nothing but the ring's sprite: any
  per-event transform write on an icon wipes the bump.

A **pip stands for a SAVED crystal** — one carried beyond the first, which the main icon
already represents. So an un-upgraded Dolphin shows no pips at all, and the mini crystal
appearing *is* Twin Seed becoming visible.

### Why the boost ring writes nothing but its sprite

A swell keyed to the charge and a colour ramp both landed on **every** resource event. The
ring stuttered between discrete scales and killed its own tween doing it. The authored
eleven-sprite gauge is the whole readout.

### Why a skim punches the jaws

One skim moves the gape by a 150th of its range — about 0.12°, invisible. The three signals
wired to a skim are otherwise: a haptic pulse that is a **no-op on desktop**, and a beam VFX
that only draws if the skimmed prism authors a `ParticleEffect`. So the discrete event gets
its own beat: `DolphinVesselHUDController` treats an energy **rise** as the skim (nothing
else raises energy — the blast spends it all, a ram halves it) and punches the jaw pair.

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
| at zero energy | jaws sit slightly open (4.76°/side), NOT shut — hull and Time icon agree |
| keep skimming | model's jaws open toward 23.4° per side (**~150 skims** to full); Time icon matches at every step |
| ram a prism | gape halves |
| hit a crystal | blast fires, gape snaps back to the 4.76° rest, Space icon flashes with a prism count |
| blast at full energy | destruction is a FAN — wide across the jaw plane, narrow across the beam |
| full throttle, no boost | `VesselStatus.Speed` settles at **78** (was 60) |
| hold drift | boost ring steps up; release → speed rises then decays; ring empties |
| hold drift from empty to full | ring fills in **~3.6 s** (was 4) |
| release a full meter | speed peaks near **357** and takes **~2.5 s** to fall back (was 210 / 2 s) |
| fly straight without drifting | ring does **not** climb |
| drift, release, drift again | speed returns to normal — no stuck multiplier |
| Charge to level 5 | second crystal pip appears; two crystals plantable back to back |

Knobs, in order of likely tuning: `DolphinSkimmerChangeResourceByPrismEffect._resourceAmount`
(skim gain), `ChargeBoostAction.chargeTimeToFull` / `dischargeTimeToEmpty` /
`maxBoostMultiplier`, `DeployTeamCrystalAction.cooldown` / `minCooldown`,
`DolphinVesselExplosionByCrystalEffect._min/_max/_coreExplosionScale` (**then `MinJawAngle` /
`MaxJawAngle`** — `_coreExplosionScale` is the only one of the three that does NOT move a jaw
angle, since it sets the blast's width across the beam rather than its gape).
