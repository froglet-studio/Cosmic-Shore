# Unity In-Editor Verification Checklist

**Purpose.** Some changes land on shared branches (`bleeding-edge` and the
per-feature branches) without ever being opened in the Unity Editor —
authored and committed by a session that **cannot run the editor**, so no
compile, no play-test, no prefab/asset inspection happened on the author's
side. Those changes are correct on paper but carry editor-side risk: a prefab
import that didn't take, a Variant override that didn't serialize, a rig weight
that reads differently in-scene than in code.

This doc is where that risk gets **recorded once** instead of being re-explained
at the start of every session. When you next open the project in Unity, work the
open items below, tick what you confirm, and delete (or move to "Verified") what
holds up. When you commit code that you could not editor-verify yourself, add an
entry here rather than leaving it in a PR body or a chat message that scrolls away.

**How to use it**
- One `### ` section per unverified change set, newest first.
- Each has: what landed, the concrete **verify in editor** steps, and any
  **first-pass tuning** numbers (these are starting points, expect a balancing
  pass once the thing is observable in context — they are *not* settled).
- Status markers: 🔴 unverified · 🟡 partially confirmed · 🟢 verified in editor.

---

### 🔴 Sparrow Turret Stance — two flight visualizations, still-nothing hardening (`claude/sparrow-prism-attack-hg6n78`)

Authored without a Unity compile or play-test. The stance STILL showed nothing after the
Initialize fix; three surviving silent failure modes are closed or screaming, and the flight
visual now ships in **two live-switchable forms** for A/B judgment. Full mechanics + the
verification list: `_Scripts/Controller/Vessel/R_VesselActions/SPARROW_TURRET_STANCE.md`.

**The A/B.** `FullAutoBlockShootAction.asset` → **Flight Visualization**, read per volley (flip
it in the inspector during play mode; the next shot switches):

- **TranslateAndGrow** — the prism scales up and translates out of the gun into place
  (`PrismFlightClock` vertex offset + grow bloom at the fastest clock rate).
- **ReverseSuction** — the fauna suction shader in reverse: faces stream out of the MOVING shot
  point into the anchored shape (`PrismImplosion.StartGrow` tracking the carried projectile —
  `PrismType.Grow`'s first producer). The real prism is created when the shot lands, so mass
  becomes tangible at arrival (the mid-flight-collider "wart" does not exist in this mode).

**Why nothing showed, most likely:** un-imported flight graph wiring makes viz 1's prisms
teleport straight to ~286 u downrange (that stamp now SCREAMS via `WarnUnwiredMaterial` on
`_FlightStartTime`), and the authored bloom rate had the prism at a few percent of its size on
arrival (executor now pins `GrowthRate = 8`). Also confirm the editor is actually on this
branch — none of this is on `bleeding-edge`.

**Verify in editor:**

1. Asset-only gates first: `python3 Tools/Shaders/wire_prism_flight_clock.py --check`, and
   `FrogletTools > Ecology > Prism Animation > Validate Clock Wiring` (now requires the three
   `_Flight*` properties + `PrismFlightClock` on both graphs).
2. Open BlockGraph + ExplodingBlockGraph — no import errors, `FlightStartTime/Duration/Velocity`
   on the Blackboard. Recovery: `git checkout` the graphs + run Auto-Wire Clock Properties.
3. Stop, hold fire, in **TranslateAndGrow**: prisms visibly leave the muzzles, scale up in
   flight, anchor at ~286 u. `[PrismClock]` errors in the console mean the graph wiring — see
   step 2. Prisms popping in at range with no flight = the same, now with an error naming it.
4. Flip to **ReverseSuction** live: faces stream from the moving shot point into place; the
   real prism appears as the stream completes. This mode uses only long-shipped shader wiring,
   so it doubles as the control: viz 2 working while viz 1 doesn't isolates the new graph edits.
5. Pierce (SPACE 5) / attribution / MASS stretch / MASS-5 shield / MPPM — as documented in
   SPARROW_TURRET_STANCE.md's list.
6. Judge the two visualizations and pick (or keep both). Also judge viz 1's mid-flight collider
   at the destination vs viz 2's tangible-at-arrival.

**Playtest round 2 (2026-08-10, same branch):** shots were very hard to see — three changes on
top, all data + one curve retune:

- **ReverseSuction is now the default** (`flightVisualization: 1`), slowed to **5× the flight
  time** (`suctionDurationMultiplier: 5`): the shot lands and pierces on the bullet clock, the
  faces keep streaming into place for ~1.5 s after it, and the real prism is created 0.2 s
  before the stream completes (tangible at assembly completion — the mid-flight-collider wart
  is gone in this mode).
- **Turret prisms are DANGER prisms** (`fireDangerPrisms: 1`) — danger material, so they stand
  out. Locked-law consequences to verify: they bite the shooter too, and MASS-5 Shielded
  Prisms is suppressed while danger is on. Known cosmetic seam: the stream renders domain
  colors, the revealed prism wears the danger material.
- **Gun range re-anchored, both modes**: base speed 1500 → **750**
  (`FullAutoAction.speedValue.Value`), SPACE curve 2.5 → **4.667**
  (`Sparrow.asset` MultiplierAtFullLevel) — SPACE 0 range halves (~143 u), SPACE 15 unchanged.
  Verify with a Space crystal binge that range visibly stretches toward the old reach.

**Playtest round 3 (2026-08-10):** now SHIELDED full-size shots on the plain flight, range
quartered from the original:

- `firedPrismState: Shielded` (enum replaces the round-2 `fireDangerPrisms` bool — Plain
  restores the MASS-5 gate, Danger restores round 2), `spawnFullSize: 1` (no grow-in; the
  flight is the transition), `flightVisualization: 0` (suction off but kept as the alternate).
- Range: base speed 750 → **375**, SPACE curve 4.667 → **9** — SPACE 0 ≈ 72 u, SPACE 15
  unchanged (~931 u). Verify shots are close-in, LARGE, and octahedron-armored from the
  muzzle; verify a Space binge stretches the reach ~13×.
- Verify the shield birth-snap renders ON THE FLIGHT: the flying shot must be the octahedron,
  not a plain box that armors on arrival — if it flies plain, the birth rule regressed.

**Round-3 follow-up (spread-at-distance):** the flight moved vertices but the spread chain's
distance read the PIVOT (parked at the anchor), so shots rendered with max-range spread from
frame one. `PrismFlightSqrDistance` now feeds `Prism Sub Graph.SqrDistance` on BlockGraph from
the displaced pivot, and the `SqrDistanceSubGraph` node is retired. Verify: a fired prism's
spread/near-look must now be identical to a trail prism laid at the same visible distance,
tightening as it flies; ordinary prisms (trail/environment) must render unchanged. Re-run
`wire_prism_flight_clock.py --check` + Validate Clock Wiring (BlockGraph now requires
`PrismFlightSqrDistance`).

**Playtest round 4 (2026-08-10):** shield onto SPACE 5, bullet-sized hit spheres:

- `firedPrismState: ShieldedAtSpace5` — regular prisms below SPACE 5, shielded at 5+, same
  gate as pierce. Verify the flip at the SPACE-5 unlock: below, plain prisms that stop at
  first impact; at 5+, armored octahedra that pierce. MASS-5's map slot is now open (label
  records the move) — the HUD's Mass icon should no longer show an upgrade state change
  affecting the turret.
- The carried hit volume is now the BULLETS' sphere: unit SphereCollider on
  `Sparrow Projectile Prism.prefab`'s ProjectileCollider child (was a thin box, ~1/24th the
  bullet's cross-section — the round-3 "missing lots" report), scaled in code to
  `collisionDiameter: 12` / `shieldedCollisionDiameter: 18`. Verify prism shots now connect
  on the same aims that bullets connect on, and that shielded shots feel distinctly easier
  to land. Prefab was hand-edited (BoxCollider → SphereCollider, same fileID) — confirm the
  prefab opens clean with the sphere on the child.

**Playtest round 5 (2026-08-10):** friendly fire always on; CHARGE 5 spares only the skyburst:

- Turret prism carried projectile `friendlyFire: 0 → 1` on `Sparrow Projectile Prism.prefab`.
  Verify a turret shot fired into YOUR OWN domain's prisms now damages them (and stops there
  below SPACE 5) — previously it flew straight through friendly mass. Bullets already had
  `friendlyFire: 1`; confirm they still damage own-domain prisms unchanged.
- `ProjectileDetonatorSO` now stamps `AffectSelfOverride = !SpareOwnDomain` on every skyburst
  detonation. Verify: below CHARGE 5 a skyburst blast destroys your own domain's prisms;
  at CHARGE 5+ the blast (and the direct hit) spares them — hit, timeout, and mine
  detonations all flip together. The shared `AOEExplosion.prefab` was NOT edited — confirm
  the Manta crystal explosion still spares own domain as before.
- Self-output guard (round-5 follow-up: fired prisms were exploding as they were shot):
  `Prism.IsProjectileLaid` + owner match in `Projectile.DisallowImpactOnPrism`. Verify a
  turret volley ACCUMULATES prisms — no shot destroys its own prism at the anchor, and a
  steady-aim burst stacks instead of replacing one prism at 30/s. Verify your bullets fly
  through your own fired prisms without destroying them, while still damaging your own
  TRAIL prisms (friendly fire intact), and that a second player's shots DO destroy your
  fired prisms.

**First-pass tuning:** fire rate 30/s + speed 375 (SPACE ×9 at full) + flight 0.3 s on `FullAutoAction.asset`
(shared with the guns); `blockScale (0.8, 0.5, 5)` + `flightVisualization` on
`FullAutoBlockShootAction.asset`; reveal overlap 0.2 s (`RevealOverlapSeconds` in the executor);
turret prism pool 40/90/8 on the Sparrow prefab.

---

### 🔴 Dolphin speed + charged-boost retune (`claude/dolphin-speed-boost-tuning-qgnojw`)

Authored without a Unity compile or play-test. **Two authored numbers changed in
existing serialized assets** — no new keys, no new components, no hand-built YAML
structures — so the import risk is low, but nobody has flown the result.

**What landed.** Four requested deltas, all data, no code:

| quantity | before | after | delta |
|---|---|---|---|
| max cruise speed | 60 | **78** | +30.0% |
| max boost speed (peak of a full discharge) | 210 | **357** | +70.0% |
| boost charge fill rate | 0.250 /s | **0.275 /s** | +10.0% |
| boost drain rate | 0.500 /s | **0.400 /s** | −20.0% |

- `Dolphin.prefab` → `VesselTransformer.DefaultThrottleScaler: 50 → 68`.
  `DefaultMinimumSpeed` deliberately left at **10** — the request was max speed, so the
  throttle top moved and the drift/idle floor did not.
- `ChargeBoostAction.asset` → `maxBoostMultiplier: 2 → 2.259`,
  `chargeTimeToFull: 4 → 3.636`, `dischargeTimeToEmpty: 2 → 2.5`. That asset is
  referenced only by `Dolphin.prefab`, so no other vessel moves.

**The peak multiplier is squared, and that is why 2.259 is not a round number.**
`VesselTransformer.CurrentBoostAmount()` multiplies `BoostMultiplier` (decaying live)
by `ChargedBoostCharge` (pinned at the charge-end value), so the authored peak lands as
`maxBoostMultiplier²`: the real ceiling was `50 × 2² + 10 = 210`, not the 110 the design
doc implied. **This was NOT changed** — it is shipped behaviour on both the executor and
the legacy `ChargeBoostAction`, and "fixing" it inside a tuning pass would halve the
Dolphin's boost unasked. It is now documented in `DOLPHIN_ENERGY_ECONOMY.md` §2. If it
should become a single factor, that is a one-line change plus its own retune — see
Follow-ups there.

**Verify in editor** (Menu_Main, freestyle, Dolphin)

1. **Full throttle, no boost** — `VesselStatus.Speed` settles at **78** (was 60).
2. **Hold drift from an empty meter** — the boost ring fills in **~3.6 s** (was 4).
3. **Release a full meter** — speed peaks near **357** and takes **~2.5 s** to fall
   back (was 210 over 2 s). This is the number most likely to want a balancing pass;
   357 is a big jump and the speed tunnel amplifies how it reads.
4. **Drift → release → drift again** — speed returns to normal, no stuck multiplier
   (the `BeginCharge` clear is untouched, but this is the regression it guards).
5. **The speed tunnel tracks it.** FOV should narrow noticeably harder at the new top
   speed. That coupling is the platform law (`Docs/SPEED_TUNNEL.md`) — absolute and
   fleet-wide, no per-vessel window — so it is the intended consequence, not a bug.
6. **Nothing else moved.** Fly any other vessel; `ChargeBoostAction.asset` and the
   Dolphin prefab are the only things touched.

**Collider budget:** unchanged — no spawning, geometry, or query change.

**First-pass tuning (expect a balancing pass — observe in context first):**

| Knob | Value | Where it lives |
|---|---|---|
| Throttle top | **68** (+ `MinimumSpeed` 10 = 78) | `Dolphin.prefab` → `VesselTransformer.DefaultThrottleScaler` |
| Speed floor | **10** (unchanged) | `Dolphin.prefab` → `VesselTransformer.DefaultMinimumSpeed` |
| Boost peak multiplier | **2.259** (**squared** in use → ×5.103) | `ChargeBoostAction.maxBoostMultiplier` |
| Charge time to full | **3.636 s** | `ChargeBoostAction.chargeTimeToFull` |
| Discharge time to empty | **2.5 s** | `ChargeBoostAction.dischargeTimeToEmpty` |

Max boost speed is `DefaultThrottleScaler × maxBoostMultiplier² + DefaultMinimumSpeed` —
recompute it after touching **either** of the first two rows, they are not independent.

---

### 🔴 Dolphin crystal blast — capsule sweep along the jaw gape (`claude/dolphin-echobliteration-capsule-a0vs26`)

Authored without a Unity compile or play-test. **Unlike most entries here this one
DID hand-author asset YAML** — a `SphereCollider` was rewritten into a
`CapsuleCollider` in place (class id `135` → `136`) — so the first check below is a
genuine import check, not a formality.

**What landed.** The Dolphin's crystal-impact blast no longer sweeps a circular
cone whose radius grows with skim energy. Its cross-section is now a **capsule**
(a 2D stadium): the radius is pinned to a fixed width *across the beam*, and what
energy buys is capsule **length**, extended along the axis the vessel's jaws open
across (container-local up = ship up). A charged blast is a fan — wide in the jaw
plane, narrow across it.

- `AOEConicSweepQueryJob` (Burst) tests point-to-**segment** instead of
  point-to-axis. Same cost class, no extra sqrt.
- `AOEConicExplosion.prefab`'s trigger is a `CapsuleCollider` driven per frame by
  `UpdateCapsuleTrigger`, so the vessel-impact volume and the Burst volume are the
  same shape by construction. A dev-build warning fires if a conic blast opens a
  gape without a capsule trigger.
- `InitializeStruct.CoreScale` / `_coreExplosionScale` carry the capsule diameter,
  authored separately from the empty-charge length so the blast can rest as a
  short capsule instead of a sphere. `0` collapses everything back to the plain
  circular cone — that is what every non-conic caller and the spherical blast get,
  so **no other vessel's blast changed**.
- The jaws (hull + HUD icon) were re-measured against the new geometry and their
  linear approximation retired: both now call one shared
  `RiptideAnimation.GapeAngleAt(t, min, max)`, exact at every charge.
- `AOEExplosion._sphereCollider` → `_triggerCollider`, typed `Collider`, since the
  shape is now the subclass's business.

**Verify in editor**

1. **The hand-authored collider imported.** Open `_Prefabs/Projectile/AOEConicExplosion.prefab`.
   The root must show a **Capsule Collider** (Is Trigger ✓, Radius 0.0667,
   Height 1, Direction **Z-Axis**, Center 0/-0.5/0) — *not* a missing component, a
   Sphere Collider, or a second collider alongside it. If Unity rejected the YAML
   this is where it shows.
2. **It compiles.** Nothing in the branch is `#if`-guarded (the conditional-compilation
   gate passes), but no C# compiler ran on the author's side at all.
3. **Empty-energy blast is unchanged in feel, slightly lozenge-shaped.** Fly to a
   crystal with no banked energy. The blast should look and destroy about as
   before (it is 400 long × 320 wide instead of a 400 sphere).
4. **Charged blast is a FAN.** Bank energy to full, then hit a crystal while flying
   at a dense prism wall. Destruction should be wide in the jaw plane and narrow
   perpendicular to it — roll 90° and fire again to confirm the fan rolls with the
   ship (it is bound to ship-up, not world-up).
5. **The jaws never read fully shut.** At zero energy both the hull's jaws and the
   HUD's Time icon should sit slightly open (4.76°/side), and they should agree
   with each other at *every* charge step, not just at the ends.
6. **Nothing else regressed.** Fire a Manta / Rhino / Squirrel / Serpent crystal
   blast (all spherical) and a Sparrow skyburst — they take the `CoreScale == 0`
   fallback and must be identical to before.

**Collider budget:** unchanged — the conic blast still carries exactly one trigger
collider, swapped sphere → capsule.

**First-pass tuning (expect a balancing pass — observe in context first):**

| Knob | Value | Where it lives |
|---|---|---|
| Capsule length, empty → full | **400 → 2080** | `DolphinVesselExplosionByCrystalEffect._min/_maxExplosionScale` |
| Capsule diameter (fixed) | **320** (radius 160) | `DolphinVesselExplosionByCrystalEffect._coreExplosionScale` |
| Gape half-angle, empty → full | **4.7636° → 23.4287°** | `RiptideAnimation.MinJawAngle` / `MaxJawAngle` (derived from the two above over the prefab's `height: 2400` — **change a scale and these must follow**) |
| Gape axis | **(0,1,0)** container-local = ship up | `AOEConicExplosion.gapeAxis` |

The length/diameter pair is the whole feel: length is reach along the gape,
diameter is how forgiving the aim is across the beam. The jaw angles are *derived*,
not independent — recompute them as `atan((scale / 2) / height)` after any retune.

---

### 🔴 Sparrow stationary stance — roll works stopped, pitch/yaw 3× (`claude/sparrow-strafing-roll-stopped-d2yc7g`)

Authored without a Unity compile or play-test. **Code only — no prefab, scene or
SO asset was touched**, so the editor-side risk is a compile check plus feel.

**What landed.** The strafing roll (`BarrelRollController`) no longer bails out
while the Sparrow is in its stationary/turret stance. Stopped, the boost still
gives no speed, but the roll arms on the same boost press, triggers on the same
full stick deflection, spends the same charge pip, and **strafes the same
distance** — it is the stopped Sparrow's dodge. Rolling does not change the
stance.

- The displacement survives the restriction via a new per-modifier opt-in,
  `ShipVelocityModifier.ignoresTranslationRestriction` (default **false**; only
  the roll sets it, every other `ModifyVelocity` caller is untouched and stays
  fully held while restricted).
- `VesselTransformer` grew a restricted branch: `ApplyVelocityModifiers(
  translationRestricted: true)` + `MoveRestricted()`. It deliberately does not
  write `VesselStatus.Speed` or `Course`.
- Two incidental fixes fall out of that branch — velocity modifiers now **age**
  while restricted (previously they froze and lurched out on stance release),
  and the `StopFlareBody` material write is now edge-triggered instead of
  per-frame (it writes through `renderer.materials[0]`, which clones).
- The roll projects its nudge on current **facing** while stopped, because
  `Course` is stale there (`MoveShip` is what refreshes it).

**Also landed: pitch and yaw run at 3× while stopped.** New serialized
`VesselTransformer.restrictedTurnMultiplier` (default **3**), applied through a
shared `TurnScalar` property to the whole pitch/yaw rate whenever
`IsTranslationRestricted` is set. Applied in **`SingleStickVesselTransformer`**
as well as the base class — that subclass overrides `Pitch`/`Yaw` and is what
both the Sparrow and the Serpent actually run, so a base-only change would have
reached neither. Roll is deliberately unscaled (it is the bank into the turn,
not a turn rate). **The Serpent inherits the same default** — one inspector
field on `Serpent.prefab` to opt out.

**Verify in editor**

1. Project compiles with zero errors. Run the `CosmicShore.Tests.EditMode`
   suite — `ShipModifierTests` gained two cases pinning the new flag.
2. `MinigameFreestyleMultiplayer_Gameplay` (or Menu_Main freestyle), Sparrow.
   **Flying** roll first: boost + full left stick → rolls and strafes, once per
   press. This must be **unchanged** — it is the regression risk.
3. Toggle the stationary/turret stance. Boost + full left stick → **rolls and
   strafes**. Speed does not change. Still once per press (hold boost + hold the
   stick at max = exactly one roll). Charge ring arms and wipes as when flying.
4. After the stopped roll: still stopped, still in stationary fire mode, and no
   trail/bridging prisms were laid.
5. **Stale-course check.** Stopped, rotate to aim well away from the heading you
   had when you stopped, then dodge. The strafe must go where the stick points
   relative to your **current** facing — a skew toward the old heading means the
   projection plane is wrong.
6. **No banked lurch.** Stopped, take a knockback (a Rhino ram, or clip a danger
   prism): you must not move. Release the stance: you must not lurch.
7. **Stopped turn rate.** Flying, time a full 180° yaw. Toggle the stance and
   repeat: roughly **a third** the time. Pitch likewise. Release the stance and
   the rate must drop straight back (the scalar is read per frame — a rate that
   stays fast means it got cached). The bank into the turn is unchanged by
   design, so the stopped turn reads flatter than a flying one.
7b. **Other vessels.** Serpent — stop into its weave stance, take a knockback,
   release: no movement while stopped, no lurch after. Note its pitch/yaw are
   **also 3×** while stopped (same transformer, same default); set
   `restrictedTurnMultiplier` to `1` on `Serpent.prefab` if that is unwanted.
   Any vessel: boosts/bounces/deviation nudges still displace normally while
   flying, and flying turn rates are unchanged everywhere.
8. **MPPM two clients.** Roll while stopped on client A; client B must see the
   same displacement (it replicates through the owner-authoritative
   NetworkTransform, same as the flying roll — no new networked state).

**First-pass tuning** (starting points, not settled)

| Knob | Where | Value |
|---|---|---|
| Dodge distance | `Sparrow.prefab` `BarrelRollController.nudgeSpeed` / `rollDurationSeconds` | 60 / 0.6 — **one number for both stances**. If the stopped dodge should reach further or less far than a flying strafe, that is a new serialized field, not a rescale of this one. |
| Stopped turn rate | `Sparrow.prefab` `VesselTransformer.restrictedTurnMultiplier` | 3 (pitch + yaw only). Sparrow authors PitchScaler/YawScaler 80 with RotationThrottleScaler 0.1, so ~82 °/s flying → ~247 °/s stopped. |

**Open question the author could not resolve** — whether a stopped dodge should
cover the same ground as a flying strafe. Shipped as identical (the simplest
reading of "the same way"); it is one field to split if it plays too strong in
turret stance.

---

### 🔴 Sparrow boost redesign — no overheat, base strafing roll, Elemental Ward (`claude/sparrow-ability-redesign-norbgz`)

Authored without a Unity compile or play-test. Touches a **platform** surface
(`ResourceSystem.ApplyElementalEffect`) plus two vessel prefabs edited as YAML,
so the editor-side risk is real: hand-written prefab documents, a removed
GameObject, a removed resource slot, and renamed serialized fields.

**What landed**

- The Sparrow's overheat mechanic is **deleted** — `OverheatingActionSO`,
  `OverheatingActionExecutor`, the legacy `OverheatingAction`, the
  `OverheatingAction.asset`, the `Heat` resource on the Sparrow prefab, and
  `VesselStatus.IsOverheating`. Input event 7 binds straight to the shared
  `BoostAction.asset`; boost is now unlimited in duration.
- The **strafing roll dropped to base kit** — `BarrelRollController` lost its
  `IsUpgradeActive(Element.Time)` gate. Still one roll per boost press.
- **TIME-5 is now "Elemental Ward"** — a general, source-keyed
  elemental-debuff immunity on `ResourceSystem`
  (`SetElementalDebuffImmunity` / `IsElementallyImmune`), gated in one place:
  the negative branch of `ApplyElementalEffect`. Driven declaratively by the new
  `VesselElementalImmunity` component: **Sparrow** `WhileBoosting` + Time gate,
  **Serpent** `WhileTranslationRestricted` ungated.
- The Sparrow boost icon's radial gauge became a **binary roll-charge pip**
  (`SparrowHUDView.SetRollCharge`), driven by
  `BarrelRollController.OnRollChargeChanged`.
- `SquirrelVesselHUDController` lost its `OverheatingActionExecutor` lookup — it
  compiled against a Sparrow-only component and always resolved to null on a
  Squirrel, so the Squirrel's heat gauge never moved. Pure dead-code removal.

**Verify in editor** (full steps + expected observables:
`_Scripts/Controller/Vessel/R_VesselActions/SPARROW_AFTERBURNER.md` §
"In-editor verification")

1. Project compiles with zero errors; no new console warnings on Sparrow or
   Serpent spawn. **Known pre-existing, not from this branch:** the Sparrow's
   `ElementalBarsController.view` reference (`fileID 7416581124810081342`) is
   already dangling on `bleeding-edge`.
2. **Prefab integrity is the top risk.** Open `Sparrow.prefab` and confirm: no
   missing-script slots; the `OverheatingBoostActionExecutor` child is gone; the
   `ResourceSystem` list reads Missiles / FullAuto / ExhaustBarrage (3 entries,
   no Heat); `SparrowHUDController.barrelRollController` points at the root's
   `BarrelRollController`; `VesselElementalImmunity` is on the root reading
   `WhileBoosting` + `Time`. Then `Serpent.prefab`: `VesselElementalImmunity`
   on the root reading `WhileTranslationRestricted` + `None`.
3. Hold boost 60 s — no force-release, no danger trail, no self-slam.
4. Time at 0: boost + full stick deflection rolls **once** per press.
5. The boost (rightmost) ability icon's ring: full on press, wipes empty with a
   punch on roll, empty until the next press. Never a partial fill.
6. Time ≥ 5 (`ResourceSystem.TimeTestHarness = 0.5`): danger prism **while
   boosting** → element flowers do not dip; **not boosting** → they dip. Slow
   and input-mute land either way (by design).
7. Serpent stopped + danger prism → no flower dip, at any Time level.
8. **MPPM two clients**, both Sparrows, one at Time 5: both machines must agree
   on who resists the drain. This is the replicated-`NetElementUnlocks` path —
   a local level read would pass step 6 and fail here.
9. `FrogletTools > Vessels > Audit Vessel Ability Rows` — Sparrow still 4/4 in
   charge → mass → space → time.

**First-pass tuning** (starting points, not settled)

| Knob | Where | Value |
|---|---|---|
| Boost speed at Time 10 | `Sparrow.asset` Time `MultiplierAtFullLevel` | 1.5 (unchanged — but the hold is now unbounded, so this is the first balance lever) |
| Immunity window | `Sparrow.prefab` `VesselElementalImmunity.condition` | `WhileBoosting` (`Always` = passive ward at Time 5, one field) |
| Roll pip colours | `SparrowHUDVariant.prefab` | armed cyan `0.55/0.9/1`, spent dim grey `0.35/0.4/0.45 @ a 0.5` |
| Roll wipe / punch | same | 0.15 s / 0.3 |

**Open design question the author could not resolve** — whether the ward should
hold `WhileBoosting` (shipped, mirrors the Serpent's stopped stance) or `Always`
at Time 5. With an indefinite boost, `WhileBoosting` means a pilot willing to
fly permanently full-throttle is permanently warded. One inspector field either
way; no code change.

### 🔴 Dolphin elemental pass — skim feedback, drift boost, cone blast (`claude/dolphin-energy-crystal-cooldown-zpvc07`)

Authored without a Unity compile or play-test. Garrett play-tested the HUD/boost
rounds mid-branch, but **the final skim-feedback fix is unconfirmed** — the last
report was still "no skimming indication", after which the branch found (a) the
crackle needs three pieces the Dolphin had none of, and (b) all three skim signals
are individually invisible on desktop. Nobody has yet seen a Dolphin skim work.
Mechanics + full knob list: `_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md`.

**Verify in editor (highest risk first):**

1. **Run `FrogletTools > Vessels > Audit Vessel Skimmers`.** Expect
   `Dolphin  NearFieldSkimmer: 'EnergySkimmer' OK`. This is the branch's headline fix —
   `VesselStatus._nearFieldSkimmer` pointed at a DISABLED legacy skimmer, so
   `Skimmer.Initialize` never reached the object whose trigger fires and
   `SkimmerImpactor` dropped every contact silently. (Serpent is expected to FAIL —
   known, untouched.)
2. **Skim in Menu_Main freestyle.** Fly the Dolphin through cell mass: crackle arcs
   should sweep the skimmer sphere per prism, the HUD jaw icon should punch per skim,
   and the gape (icon + the model's own jaws) should widen toward 18.4° per side as
   energy fills. Watch the console — an unauthored `Prism.ParticleEffect` now logs one
   named warning per prefab instead of throwing per contact.
3. **The boost loop.** Hold drift → the ring steps up; release → speed rises and decays
   as it drains. Flying straight must NOT fill the ring (the passive `resourceGainRate`
   is gone). Drift → release → drift again must return to normal speed (the interrupted
   discharge used to leave `BoostMultiplier` stuck).
4. **Crystal impact.** The cone fires, energy empties, the jaws snap shut, and the Space
   icon flashes with a prism count. At Space L5 the cone must stop damaging your own
   domain's prisms.
5. **Charge L5.** A second crystal pip appears and two team crystals can be planted back
   to back. The deploy preview must be tinted your domain, and bloom/wither rather than
   pop (continuity of existence).
6. **MPPM two-client:** the L5 upgrade effects are gated on the replicated
   `IsUpgradeActive`, so confirm both peers agree on Clean Blast and Twin Seed.

**Hand-authored assets that have never had an editor import round-trip:** the Dolphin
HUD variant's four-icon row, the Dolphin prefab's crackle overlay + controller, and
`DolphinSkimmerChangeResourceByPrismEffect.asset`. Their YAML keys were machine-checked
against the scripts' serialized field sets, but Unity has not re-serialized them.

---

### 🔴 Fauna consumption v3 + shark jaw rig (fauna-consumption-behavior branch, merged)

Landed via PR #614 (`claude/fauna-consumption-behavior-*`) plus the shark-jaw
commit `438070a2`. None of it had a Unity compile or play-test from the author —
it is on the shared branch unverified. Design + mechanics reference:
`Docs/ECOSYSTEM.md` §7 / §7.3 (intentional consumption, the mouth-driven
predator, tiger-shark territoriality, centre focus).

**Verify in editor (the three things most likely to be wrong):**

1. **Jaw prefab import.** Open `Assets/_Models/Fauna/MassSharkFauna.prefab`.
   Confirm `SharkJawDriver` (`_Scripts/Controller/Environment/FloraAndFauna/SharkJawDriver.cs`)
   sits on `Shark_model` alongside the `Animator` + `RigBuilder`, that the two
   mouth `MultiAimConstraint`s and the `MawTarget` it aims at are all present and
   wired, and that weight `0` = FBX swim pose (mouth closed) / weight `1` = aimed
   at `MawTarget` (mouth open). Danger prisms are parented to the jaw bones — check
   the teeth actually gape with the mouth in a play-test (`NotifyBodyPrismsMoved`
   should keep their spatial-index positions honest as the jaw moves).

2. **Elemental Variant on the tadpole config.** Confirm the tadpole's
   `FaunaConfigurationSO` / prefab Variant carries its intended elemental setup
   (that the Variant override actually serialized and points at the creature
   prefab's `Boid`, not the dead `*Population`/manager prefab — see the §7 warning
   that the live spawn path is the cell config, not the scene-placed populations).

3. **Two feeding models coexist.** Confirm both consume paths still compile and
   run side by side without one having been collapsed into the other:
   `LightFauna` (brittlestar/shark) has **no** `_pendingMeals` grazing queue
   (intentional-feeding: approach → face → suction), while `Boid`'s **drone**
   path keeps its `_pendingMeals` burst-pacer (combat). Do not re-add the
   burst-pacer to the forager/intentional types or strip it from the drone path
   (`Docs/ECOSYSTEM.md` §7.3 explains why they differ).

**First-pass tuning (expect a balancing pass — observe in context first):**

| Knob | Value | Where it lives |
|---|---|---|
| Hunt pulse (window / cycle) | **10s open / 20s interval** | `LightFaunaDataSO.huntDurationSeconds` / `huntIntervalSeconds` |
| Tiger-shark territory radius | **r = 600** | `LightFaunaDataSO.territoryRadius` (+ `territoryAnchorDistance`) |
| Jaw open / close | **0.6s open / 1.8s close** | `SharkJawDriver` (open notably faster than close) |
| Herbivore/forager centre focus | **0.35** | `FaunaConfigurationSO.CenterFocusBias` (per-deployment) |

These four are the ones the author flagged as guesses. The jaw transition is
~2.4s total per 20s hunt cycle; the driver early-outs on a single float compare
whenever the mouth is settled, so re-tuning the timings has no perf cost.

---

## 🔴 Dolphin skim economy + jaw CTA + fleet silhouette removal (2026-08-07)

Branch `claude/dolphin-prism-energy-5e4hbq`. None of this was editor-verified — the
prefab surgery was done out-of-editor and machine-validated (no new dangling fileIDs,
no surviving references, C# compiled against a stub harness), but Unity has not
reimported any of it yet.

**What landed**

1. `DolphinSkimmerChangeResourceByPrismEffect._resourceAmount` **0.1 → 0.006666667**
   (15× less energy per skim; ~150 skims to arm the blast, 50 on a danger trail).
2. `DolphinVesselHUDView` blends the Time-slot jaw pair white → `ElementalBarsConfigSO.limeColor`
   across the top 15% of energy (`jawArmingThreshold: 0.85`).
3. The dead vessel **silhouette** removed from 13 vessel + HUD-variant prefabs, plus
   dead `silhouette`/`silhouetteContainer`/`trailContainer` keys and their overrides
   in 13 more files.

**Verify in editor**

1. Open each of the 15 edited prefabs — no *"Missing (Mono Script)"* row that was not
   already there, no broken hierarchy, HUD still lays out. (Sparrow, Rhino, Squirrel,
   Serpent, Manta and the six vessel prefabs lost real GameObjects; the rest lost keys.)
2. Play Menu_Main → freestyle on the **Dolphin**: no `[ElementalBarsController]` runtime
   warning that was not there before, and **no `[DolphinVesselHUDView]` warning at all** — it
   fires once if the shared bars config is missing or the jaw refs carry no Graphic, either of
   which means the lime CTA is silently dead. The four ability icons still bind (FrogletTools >
   Vessels > **Audit Vessel Ability Rows** → Dolphin 4/4, order ✅).
3. Fly the other vessels' HUDs briefly (Sparrow, Squirrel, Rhino, Serpent, Manta) and
   confirm nothing visually disappeared *except* the ship outline.
4. Skim a long time → jaws blend to lime near full; ram a prism → they drop back to white.

**Known pre-existing issues surfaced, NOT fixed here (own branch):**

- `SerpentHUDVariant.prefab` and `VesselHUDPrefab.prefab` carry a component whose script
  guid `57dc27a3f7264d548b51007c0615f701` resolves to **no script in the project** — an
  existing *Missing (Mono Script)* component, unrelated to this change.
- `Dolphin.prefab`'s `ElementalBarsController` has **no `elementBars` key**, so the element
  flowers are created at runtime via `CreateDefaultElementBars()` (which logs a warning).
  Fix with FrogletTools > Vessels > *Bake Elemental Petal Bars Into All Vessel HUDs*.
