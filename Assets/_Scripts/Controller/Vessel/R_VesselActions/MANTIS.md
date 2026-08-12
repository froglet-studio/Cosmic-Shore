# Mantis — the Astro League striker (vessel design proposal)

> **STATUS: DESIGN PROPOSAL — nothing in this document is implemented.** This is the full design
> for a new vessel class, written for Garrett to mark up before any code or asset lands (the
> `/vessel` design-approval gate). The element→ability map in §4 is mirrored as a proposal row in
> `Docs/ElementalAbilitySystem/FLEET_MAPS.md` §2. Every file/line citation below was verified
> against branch HEAD `0a94cb38` (2026-08-12); authored numbers marked *(proposal)* are first-pass
> tuning, everything else is shipped ground truth the design builds on.

The **Mantis** is a mantis shrimp: the animal that cocks a spring-loaded club and punches with a
cavitation shockwave — a literal short-range cone of destruction that launches whatever it hits.
It is the fleet's first **mode-coupled vessel**: designed for, tuned for, and (initially) playable
only in **Astro League** (`GameModes.AstroLeague = 37`), the Rhino's hypersea soccer. Where the
Rhino is the swordsman — contact play, blade strikes, tip bonuses — the Mantis is the **striker
and goalkeeper**: an agile racer that drifts through the court laying interception trail, snaps
the ball downfield with a ranged cavitation punch, and drops braking walls in front of its own
goal, funded by crystals.

Genre: arcade sports flight — the drift handling of a racer with the burst-play verbs of a sports
game.

- Class: `VesselClassType.Mantis = 12` (next free ID; highest today is `Sparrow = 11`,
  `Assets/_Scripts/Data/Enums/VesselClassType.cs`).
- Prefab: `Assets/_Prefabs/Spacevessels/Mantis.prefab` (name must equal the enum member — auditors
  and `ElementalAbilityMapSO.LoadFor` key off it).
- ⚠ Naming trap for the art pass: the **Manta's** FBX files are named
  `mantis_shapekey_with_animations` / `Manta_shapekey_rigged` (and the placeholder vessels wire
  them). The Mantis's own art must not be confused with — or named into — the Manta's `mantis_*`
  files. The auditors key off prefab/enum names, never FBX names, so this is a human-confusion
  hazard only, but it is a real one.

---

## 1. Kit summary

| Control | Verb | One line |
|---|---|---|
| **Left stick** | Pitch / yaw | Single-stick flight (Sparrow/Serpent family) |
| **RT (analog)** | **Overdrive** throttle | True 0→max analog throttle — the fleet's first |
| **LT (analog)** | **Carve** drift | Analog heading/course decoupling (Squirrel's exact `singleTriggerDrift` scheme) |
| **Right stick (to perimeter)** | **Mantis Strike** nudge | Sparrow-style strafing barrel roll in the stick direction **plus** a short-range lateral cavitation cone that shreds prisms and launches the ball that way |
| **A button** | **Bulwark** | Places a braking wall of prisms far in front, on the flight course — costs a wall charge; **crystals grant wall charges** |

Four abilities, four elements, four icons (charge → mass → space → time):
**Strike · Carve · Bulwark · Overdrive** — full map in §4.

---

## 2. Control model

### 2.1 Input plumbing (ground truth the scheme rides on)

The gamepad naming trap first: on gamepad, `InputEvents.LeftStickAction (2)` / `RightStickAction
(1)` are raised by the **triggers** (LT/RT edge events at `TriggerDeadzone = 0.05f`), not the
sticks — the names come from the touch scheme (`Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs`).
The analog values publish continuously as `InputStatus.LeftTriggerAnalog` / `RightTriggerAnalog`
(owner-write NetworkVariables — readable on remote peers). The A button (`buttonSouth`) raises
`InputEvents.Button1Action (6)` (GamepadInputStrategy.cs:57-61; `InputHintBindingMap` agrees:
`PadButtonSouth → Button1Action`. The enum-file comments saying Button1 = X are stale — code wins.
On desktop the raise site is the live `KeyboardInputStrategy` — the stray file at `Assets/`
root, Space key; `Controller/IO/KeyboardMouseInputStrategy.cs` is dead code no strategy selector
instantiates, a known audit trap). **No dedicated InputEvent exists for right-stick deflection**
— stick direction is polled state only, which is exactly how the Sparrow's `BarrelRollController`
consumes it. (The derived straight-line gestures `FullSpeedStraightAction (0)` /
`MinimumSpeedStraightAction (5)` do fold right-stick components into their `XDiff`/`XSum` math —
the Mantis leaves both unbound, so a stick pinned at the perimeter for a nudge perturbs nothing.)

| Physical control | Plumbing | Mantis binding |
|---|---|---|
| Left stick | `EasedLeftJoystickPosition` | Pitch/yaw via the transformer (§2.2) |
| RT | `InputStatus.RightTriggerAnalog` (+ `RightStickAction` edges) | Throttle read per-frame by `MantisVesselTransformer`; `RightStickAction` left **unbound** (edges may later carry throttle-engage SFX) |
| LT | `InputStatus.LeftTriggerAnalog` (+ `LeftStickAction` edges) | `LeftStickAction (2)` → `[MantisSharpDriftAction, MantisDriftAction, DriftTrailAction]` — the Squirrel stack |
| Right stick | `RightNormalizedJoystickPosition` (polled) | `MantisNudgeController` (§3.1) |
| A (`buttonSouth`) | `Button1Action (6)` | `PlaceWallAction` (§3.3) |
| RB | raises `FlipAction` + feeds binary `InputStatus.Throttle` | unbound (RB's `Throttle` feed is ignored — see §2.2) |
| B / X / Y, LB, D-pad | `Button2Action` / `Button3Action` / — | unbound |

Combo events (`OnlyLeftStickAction (12)` / `OnlyRightStickAction (11)` / `BothSticksAction (13)`)
fire constantly under a two-analog-trigger scheme — the Mantis leaves all three unbound on
gamepad. Touch: the drift stack mirrors the Squirrel's touch overrides (`OnlyLeftStickAction` →
drift). ⚠ **Touch has no raise site for `Button1Action`** (the "Onscreen UI buttons" the enum
comments promise don't exist in code) — the Bulwark is gamepad/keyboard-only until an on-screen
button raising the shared `OnButtonPressed` SOAP event is added (open item, §12).

### 2.2 Overdrive — the fleet's first true analog throttle

No shipped vessel has one: `toggleManualThrottle` is 0 on every prefab, and even enabled it lerps
from a full stop while gamepad `InputStatus.Throttle` is fed by the right **bumper** ("this is
just the boost button", GamepadInputStrategy.cs:47). The two-stick `XDiff` throttle would fight
the left-stick-only steering. So the Mantis gets a small transformer subclass:

**`MantisVesselTransformer : SingleStickVesselTransformer`** — inherits left-stick-only
pitch/yaw/roll (`EasedLeftJoystickPosition`, the Sparrow/Serpent/Grizzly base) and overrides
`ComputeThrottleTarget()`:

```
target = InputStatus.RightTriggerAnalog
         × ThrottleScaler × ThrottleScalerMultiplier.EvaluateLive(VesselStatus)
         + MinimumSpeed
```

replacing the base's implicit full throttle (`SingleStickVesselTransformer.ComputeThrottleTarget`
is `ThrottleScaler * CurrentBoostAmount() + MinimumSpeed` — "full throttle is implicit"). Notes:

- **Zero trigger floors at `MinimumSpeed`, not 0** — formula-consistent with the whole fleet, and
  it avoids the dormant manual-throttle path's true-stop semantics (which interact with
  `IsStationary` and the mode's kickoff `SetInitialSpeed(0)` park). The Mantis is never parked
  mid-rally; it idles forward like everything else.
- The analog read is per-frame in the transformer (the `MantisNudgeController`/
  `ShieldSwipeActionExecutor` precedent — continuous behaviours poll `InputStatus`, they don't
  ride events).
- Speed tracking stays the fleet default (exponential lerp, `LERP_AMOUNT = 1.5f`) — until Time 5
  (§4.4).
- `ThrottleScalerMultiplier` is the **existing** `ElementalFloat` on `VesselTransformer` (the
  Squirrel ships it disabled) — the Mantis enables it as its Time scaling (§4.4), and the map's
  generic Time multiplier is pinned to 1 so `CurrentBoostAmount()` can never double-dip (the
  Mantis has no boost action, but the pin makes that structural).

First-pass numbers *(proposal)*: `DefaultMinimumSpeed 10`, `DefaultThrottleScaler 90` → idle 10,
full-trigger 100, full-trigger at Time L10 ×1.5 = 145. Context: Rhino on the same pitch cruises
10–60 and ramp-boosts to 310 in a straight line; ball top speed 300. The Mantis is faster than a
cruising Rhino everywhere, slower than a committed Rhino ramp — its edge is that full speed is
always available, in any direction, without a straight-line gesture. Speed-tunnel law: nothing to
author (absolute fleet-wide mapping); the Mantis crosses the `minEffectSpeed 70` threshold at
about two-thirds pull (RT ≈ 0.67 base, ≈ 0.44 at Time L10), so the tunnel reads as throttle
feedback for free.

### 2.3 Carve — analog drift on LT

The Squirrel's exact scheme, zero new transformer code:

- Prefab `singleTriggerDrift = 1` → `GetTriggerSum()` returns `LeftTriggerAnalog * 2` — LT's 0→1
  travel spans no-drift → full single → full sharp analogically.
- `LeftStickAction (2)` binds `[MantisSharpDriftAction, MantisDriftAction, DriftTrailAction]`
  (new `DriftActionSO` assets + the shared `DriftTrailAction`): edge events start/stop the drift
  tiers, the live analog value is the intensity.
- Drift decouples heading from course (`Course` slerps between `transform.forward` and the
  drifted course by trigger sum) and **never touches speed** — 100% retention, exactly like the
  Squirrel. On the pitch this is the striker's tool: nose at the ball, momentum carrying you
  across its path, then release LT to snap course back to the nose.
- First-pass authored values *(proposal)*: single `Mult 1.4 / damping 0.5 / sfx on`, sharp
  `Mult 1.8 / damping 0.25 / sfx off` (the Squirrel's shipped values — start from proven feel).

### 2.4 Pitch/yaw on the left stick

Free with `SingleStickVesselTransformer`. First-pass scalers *(proposal)*: Pitch/Yaw/Roll
`100/100/30`, `RotationThrottleScaler 0.1` (slightly livelier than the Sparrow's 80/80/30 — a
striker turns tighter than a gunship).

---

## 3. The three bespoke systems

### 3.1 The nudge — `MantisNudgeController`

Modeled on the Sparrow's `BarrelRollController`
(`Assets/_Scripts/Controller/Vessel/BarrelRollController.cs`) — a plain per-frame poll, not a
`ShipActionSO` — with three deliberate differences:

1. **Right stick, not left.** Fire gate: `RightNormalizedJoystickPosition` radial magnitude ≥
   `perimeterThreshold (1) − ε` — the radially-clamped raw stick, deliberately not the eased
   vector (per-axis easing makes diagonal magnitudes direction-dependent; the Sparrow learned
   this). On the Mantis the right stick is otherwise completely unused (single-stick steering),
   so the nudge collides with nothing.
2. **Cooldown-armed, not boost-armed.** The Sparrow arms one roll per boost press; the Mantis has
   no boost button. The nudge re-arms on a plain cooldown (`nudgeCooldownSeconds 1.5`
   *(proposal)*), displayed as a binary pip (§8). No timer removes anything from the world — a
   cooldown on a vessel ability is input pacing, not ecology decay.
3. **It carries the Strike.** Firing does three things at once (details in §3.2):
   - **Displacement**: `transformer.ModifyVelocity(dir.normalized × nudgeSpeed,
     nudgeDurationSeconds, ignoresTranslationRestriction: true)` — the cosine-eased impulse
     channel (clamped at `velocityModifierMax 100`), `nudgeSpeed 80` / duration `0.5`
     *(proposal)* (Sparrow ships 60/0.6). Direction = `ship.right × stick.x + ship.up × stick.y`
     projected onto the plane ⊥ `VesselStatus.Course` (the Sparrow's exact construction,
     including the `transform.forward` fallback while translation-restricted and the
     `ship.right × rollSign` degenerate fallback).
   - **Visual barrel roll**: 360° smoothstep spin about flight-forward on the **visual child
     only** (the camera reads the root), ±sign from `stick.x`; real root bank
     `rootRollDegrees 15`; `BlockRotationOverride = LookRotation(Speed×Course + VelocityShift)`
     each rolling frame so bridging trail prisms lay travel-aligned; override cleared when done.
   - **The cavitation cone** — fired laterally along the nudge direction.
   `OnDisable` teardown mirrors the Sparrow's: stop routines, clear `BlockRotationOverride`,
   restore visual rest rotation, reset the cooldown state (pool/swap safety).

**Replication.** The Sparrow's roll needs none (displacement rides the owner-authoritative
NetworkTransform) — but the Strike destroys prisms, which are per-peer local GameObjects, so the
fire must replay everywhere. `MantisNudgeController` is a `NetworkBehaviour`:
owner poll → execute locally (displacement + roll + cone, zero-latency) →
`NudgeStrike_ServerRpc(dir, launchSpeed)` → `NudgeStrike_ClientRpc` → every **non-owner** peer
plays the roll visual and spawns its local cone (sender-filtered; the owner already fired). This
is the `R_VesselActionHandler` ServerRpc→ClientRpc re-execution shape with a payload the
InputEvents pipe can't carry (a direction). `launchSpeed` is the **per-use snapshot at fire
time** of the Charge-scaled ball impulse (§4.1) — element levels are local and never replicate,
so the magnitude must travel with the fire, exactly like every other outcome-affecting elemental
read (per-shot snapshot law).

### 3.2 The Strike cone — short-range lateral cone of destruction

Built on **`AOEConicExplosion`** (the Dolphin crystal-blast class,
`Assets/_Scripts/Controller/Projectiles/AOEConicExplosion.cs`) — a live swept cone: per-frame
Burst slab sweep against `PrismSpatialIndex` (`ProcessBatchConeFrame`) for prisms, plus a
CapsuleCollider trigger driven to the same parametric cross-section for non-prism contacts.
⚠ Not `AOERadialBlocks` (the skyburst's class): its `ExplodeAsync` disables the conic trigger
synchronously at detonate (commit `73191b92`), which is why DogFight's blast-catch container
appears to hang off a dead window — the Mantis cone must be the genuinely-sweeping class.

New prefab `MantisStrikeCone.prefab` *(first-pass authoring)*:

| Knob | Value *(proposal)* | Why |
|---|---|---|
| `height` | 60 | Short-range: about one wall-placement's width, a melee-plus punch, not artillery |
| `ExplosionDuration` | 0.35 | A crack, not a bloom — wavefront crosses the full 60u in a third of a second |
| `CoreScale` / half-angle | ~25° core | A punch you aim with the stick, not a room-clearer |
| `proportionalDebris` | 1 | Mandatory — without a ceiling of its own, `Inertia` is dead tuning (every AOE saturates the legacy 33.33 u/s clamp) |
| `debrisRestitution × Inertia` | 1/3 × 1.8 = 0.6 | The Dolphin cone's shipped group — debris at ⅓ the physical read, shatter rate tracking it |
| `ExplosionImpactor.destructive / devastating` | 1 / 1 | Shreds plain trail and walls; `devastate` kills shielded prisms outright |
| `ExplosionImpactor.affectSelf` | **1** | Base kit shreds **your own domain too** — friendly trail, your teammates' trail, and your own Bulwarks are all in the blast. This is the striker's risk knob, and it is what Charge 5 upgrades away (§4.1), mirroring the Dolphin (base cone hits own domain → Space 5 "Clean Blast" spares it) |
| `explosionImpactorDataContainer` | new `MantisStrikeExplosionImpactorDataContainer.asset` | **Never ship it null** — DogFight's skyburst shipped with `{fileID: 0}` and its blast touched nothing for its whole life |
| `ExplosionImpactor.SourceVessel` | stamped at spawn | The DogFight attribution field — the ball reads its striker from this (§5.2) |

Spawned at the hull with its **axis along the nudge direction** (the cone opens along the
container's +Z, and `ExplosionHelper` spawns with the ship's rotation — the Mantis spawns it with
`Quaternion.LookRotation(nudgeDir)` instead; a lateral cone is exactly the case the helper's
ship-rotation default doesn't cover).

What it does to the world, all via shipped machinery:

- **Plain prisms** (trail, Bulwarks): destroyed, debris thrown along apex-radial vectors at the
  proportional contract's speeds. Mass conservation intact — an explosion is an active force.
- **Shielded prisms**: destroyed (`devastating 1`).
- **Super-shielded prisms**: immune, and they **stop the blast** — `ResolveExplosionHit` kills
  the explosion object on super-shielded contact (Burst and physics paths both). The Astro League
  court's 480-prism edge lining is super-shielded, so **the court eats strikes; it can never be
  carved** — and the boundary itself is collider-less math (§5.4). No special casing needed:
  the platform already protects the arena.
- **The ball**: §5.2 — the one platform change this vessel needs.

Collider budget: one CapsuleCollider trigger per live cone (≤ a handful of frames each), zero
physics queries against prisms (Burst spatial-index sweep). Effectively free.

### 3.3 The Bulwark — a wall placed far in front

**Not** the Serpent's `SeedWallActionSO` — that primitive seeds an assembler on the **latest
existing trail prism** and grows a lattice by bonding neighbours; the Bulwark must appear at an
arbitrary remote point on the flight course. The right primitive is the remote block-creation
channel the skybursts use (`AOERadialBlocks.CreateBlock` →
`PrismEventChannelWithReturnSO.RaiseEvent(PrismEventData)` →
`PrismFactory.SpawnInteractivePrism`, channel asset
`Assets/_SO_Assets/Event Channels/Prisms/EventOnSpawnPrismAndReturn.asset`).

New pair: **`PlaceWallActionSO : ShipActionSO`** (stateless config) + **`PlaceWallActionExecutor
: ShipActionExecutorBase`** (state, registered in the prefab's `ActionExecutorRegistry`), bound
to `Button1Action (6)`:

1. **Gate on charges**: `ResourceSystem.Resources[wallChargeIndex].CurrentAmount >=
   wallChargeCost` (resources are normalized 0..1 meters, clamped to `MaxAmount` — **3 charges =
   a full meter, cost ~1/3 each**; the `SeedWallActionSO.enhancementsPerFullAmmo` / Sparrow
   `ammoCost 0.5` idiom). ⚠ Author `wallChargeCost = 0.333`, a hair **under** 1/3: the meter
   clamps at exactly 1.0, and `1.0f − 1/3f − 1/3f` lands a float ulp *below* `1/3f` — an
   exact-1/3 cost lets a full meter place only two walls while the pips still show three. Stated
   here so the epsilon doesn't get "simplified" away. Insufficient → refusal SFX, nothing spawns.
2. **Place**: `center = vessel.position + VesselStatus.Course × placementDistance` — on the
   **course**, not the nose (mid-drift you throw the wall where you're *going*; a deliberate,
   drift-composable choice). `placementDistance` is the Space-scaled parameter (§4.3), base 150
   *(proposal — the mode's kickoff-line distance; arena half-length is 360–540 across
   intensities)*.
3. **Layout**: a 5×3 pane of bricks ⊥ to Course (grid axes = arbitrary-up basis around Course),
   brick `TargetScale (10, 10, 1)`, near-flush spacing → a ~52u × 32u braking pane one brick
   thin *(proposal — see §5.3 for why thin-and-wide is the right shape against this ball)*.
4. **Occupancy**: `PrismSpatialIndex.TryReserve(brickPos, clearRadius)` per brick **before**
   spawning (claim-before-spawn; physics queries are blind to fresh prisms for 0.6s). A brick
   whose claim fails is skipped — partial walls are legal, overlap-spawns are not.
5. **Spawn**: per brick, `PrismEventData { ownDomain = vessel.Domain, SpawnPosition, Rotation,
   Scale, PrismType = PrismType.Interactive }` → pooled spawn → `prism.ownerID`, `prism.Domain`,
   `TargetScale` + `SetGrowthRate` + `Initialize(playerName)` — the one growth engine; the wall
   **blooms in** on the clock (continuity of existence; never tween scales, never a bare
   growthRate write). Bricks grouped into a `Trail` for bookkeeping (the `AOERadialBlocks`
   pattern).
6. **Spend**: `ChangeResourceAmount(wallChargeIndex, -wallChargeCost)`.
7. Bricks are **plain** — unshielded, undangerous. Why not shielded: against the ball a shield is
   a *free pass* — the ball pops it and keeps 100% speed, only an *eaten* prism drags (§6.2) — so
   shielding a goalkeeper wall would defeat it. Why never super-shielded: the ball ignores
   super-shielded mass entirely AND it exits the food web (the locked no-SuperShield-grants rule
   exists for exactly this).

Like every peer-local prism spawn triggered by a replicated action, the wall must replay on all
peers: `PlaceWallActionSO` rides the standard `R_VesselActionHandler` ServerRpc→ClientRpc
re-execution (bound input → replayed on every peer), so each peer lays the same pane — same
inputs, same deterministic layout (position/course replicate via the NetworkTransform; the
executor derives the grid purely from them).

**Wall charges — the crystal economy.** `ResourceSystem.Resources[0] = { Name: "Wall Charges",
maxAmount 1, initialAmount 0.34, resourceGainRate 0 }` — **no passive regen** (the Sparrow
missile-meter pattern); you start each match with one wall banked and earn the rest by collecting
crystals. Grant path: a new `MantisWallChargeByCrystalEffect.asset`
(`VesselChangeResourceByCrystalEffectSO` — its fields sit on the SO's nested `_change`
`ResourceChangeSpec`, so the asset authors them under `_change:` exactly as the Sparrow's does:
`_resourceIndex 0, _resourceAmount 0.334, _overrideAmount 0` — **+1 charge, additive**, unlike
the Sparrow's set-to-full) authored into
`MantisImpactorDataContainer.asset`'s `vesselCrystalEffects` (omni list) **and** all four
per-element elemental-crystal lists (`VesselMassCrystalEffects` / `Charge` / `Space` / `Time`) —
any crystal is a wall charge, whatever else it also does (elemental crystals still seed element
levels through their own standard effects; the charge is additive to that, not instead of it).
Astro League supplies both kinds (§5.5). Charges cap at 3 (the meter clamps); collecting at full
wastes the charge, visible on the pips (§8).

---

## 4. The four abilities × four elements

Convention (fleet-wide): **Space = reach/presence · Time = rate/mobility · Charge = threat/energy
· Mass = size/volume.** One scaled parameter per element; every map multiplier below is **pinned
to 1** with the real scaling on an authored field/`ElementalFloat` — the Dolphin's no-double-dip
pattern (`VesselTransformer.CurrentBoostAmount()` consumes the generic Time multiplier, so pinning
is what makes "no double-dipping" structural rather than situational).

Map asset: `Assets/Resources/ElementalAbilityMaps/Mantis.asset` (exact folder + name, 4 entries,
`UnlockLevel 5 / RelockBelowLevel 4 / LatchPolicy Relock` throughout).

| Element | Ability | Input (map field) | Quantitative (authored field) | L5 upgrade *(proposal)* |
|---|---|---|---|---|
| **Charge (1)** | Mantis Strike | — (right-stick polled; see §8 hints) | Ball-launch impulse: `ballLaunchSpeedMultiplierAtFullCharge` on the strike SO, ×1 → ×2 at L10 (base 140 u/s → 280, under the ball's 300 cap) | **Surgical Strike** — the cone spares your own domain (per-fire `AffectSelfOverride`, snapshotted at fire; the Dolphin "Clean Blast" primitive) |
| **Mass (2)** | Carve (drift trail) | `LeftStickAction (2)` | Trail prism VOLUME: `trailVolume` ElementalFloat 1 → 2.5 on `VesselPrismController`, cube-root per axis (the Squirrel/Dolphin field, verbatim) | **Ablative Wake** — trail prisms arrive shielded ONLY while drifting (`massUpgradeShieldsTrail` + `IsDrifting` — the shipped Squirrel mechanism, zero new code). ⚠ vs the ball this is a *free pass*, not armor (§6.2) — it defends your interception net against swords and fauna, not against the ball itself; the doc says so, the toast should too |
| **Space (3)** | Bulwark | `Button1Action (6)` | Placement DISTANCE: `placementDistanceElemental` ElementalFloat 150 → 300 on the wall SO — reach: at L10 you goal-keep from midfield | **Deep Wall** — the Bulwark arrives two panes deep (a second layer one brick behind the first; double the braking transit — §5.3 — for the same one charge) |
| **Time (4)** | Overdrive | `RightStickAction (1)` (map/hint binding only; no SO bound to the event) | Throttle ceiling: `ThrottleScalerMultiplier` ElementalFloat 1 → 1.5 (the existing dormant `VesselTransformer` field, enabled) | **Hair Trigger** — throttle response becomes immediate: speed tracks the trigger via `SetSpeedTrackingRate` (the Rhino ramp's constant-rate primitive; the rate itself is the Mantis's own knob, first-pass 600 u/s² — §9) instead of the 1.5/s exponential lerp — stop-and-go play, instant feints |

Upgrade-name collision check (shipped + reserved + retired-on-record): Surgical Strike, Ablative
Wake, Deep Wall, Hair Trigger are all free.

### 4.1 Charge — Mantis Strike

Threat/energy owns the punch, the Sparrow-skyburst/Manta-detonation pattern. The scaled
parameter is the **ball impulse**, not the cone's geometry — one parameter per element, and
reach is Space's word (the cone's 60u height stays fixed). The snapshot travels in the fire RPC
(§3.1). `IsUpgradeActive(Element.Charge)` is read at fire and stamped onto the spawned cone
(`AffectSelfOverride`), never re-read mid-blast.

### 4.2 Mass — Carve

The fleet's both drift vessels put Mass on the drift-laid trail (Squirrel "Trail Volume",
Dolphin "Drift Trail" — both `trailVolume` 1→2.5); the Mantis is the third. "Arrive shielded"
upgrades pair with Mass everywhere but the Sparrow.

### 4.3 Space — Bulwark

Reach/presence — placing farther IS the element. Distance, not span: span changes the ecology
budget per placement (§6.3), distance doesn't.

### 4.4 Time — Overdrive

Rate/mobility — the throttle is the mobility ability. The generic map multiplier must be pinned
even here: `CurrentBoostAmount()` multiplies by the generic Time multiplier whenever
`IsBoosting`, and although the Mantis ships no boost action, a shared effect (comeback, fauna
buff) or future kit change could set `IsBoosting` — pin it and the double-dip is impossible by
construction.

### 4.5 Contract-shape note (deliberate deviation, flagged for approval)

Only Carve and Bulwark are `InputEvents → ShipActionSO` bindings — the contract's stated ability
shape. The Strike is a polled NetworkBehaviour (the `BarrelRollController` precedent; the
InputEvents pipe cannot carry a direction vector) and Overdrive is transformer-internal (its map
`Input` exists for hint routing only). The contract reserves unbound map rows for
passive/impact-driven abilities, and the Strike is an active stick ability — which is exactly why
§8 asks for the hint-only `RightStickFlick` member. Named here so the deviation is a decision on
the record, not an accident the auditors trip over later.

Maintained-mechanism law: nothing here holds an element above 10 — wall charges are a
`ResourceSystem` meter (a normalized ammo fraction), not element levels; the elemental layer is
read-only scaling + the four unlock bits (owner-write `NetElementUnlocks` replication, shipped).

---

## 5. Astro League integration

### 5.1 Adding the vessel to the mode — and only the mode

The whole restriction mechanism is the arcade card's `Vessels` list
(`Assets/_SO_Assets/Games/ArcadeGameAstroLeague.asset`, today exactly `[SO_Class_Rhino]`), read by
three enforcement layers that all follow it automatically: `GameDataSO.SyncFromArcadeGame` →
`AllowedVesselClasses` + launcher clamp, `ServerPlayerVesselInitializer.ResolveSpawnVesselType` →
server-side spawn clamp (+ `ServerForceVesselType` NetworkVariable repair), and the AI clamp in
`ServerPlayerVesselInitializerWithAI`. There is **no mode-local vessel check** and none may be
added (ASTROLEAGUE.md's own rule).

- **Playable in Astro League** = append `SO_Class_Mantis.asset` to that one list. Modal carousel,
  launch clamp, server clamp, AI clamp all follow with zero code.
- **⚠ List order is load-bearing**: `ClampVesselToGame` falls back to `AllowedVesselClasses[0]`
  for any illegal hull, and the scene's `aiInitializeDatas` author `vesselClass: 6` (Squirrel) —
  today clamped to Rhino *because Rhino is index 0*. **Rhino stays first**; the Mantis is
  appended. Consequence: every AI stays a Rhino (v1 — see §5.6), and clients arriving in the
  wrong hull are forced to Rhino, never the Mantis.
- **Astro-League-only** = don't add `SO_Class_Mantis` to any other game's `Vessels` list, don't
  add it to `SO_Classlist_Classes` (the Menu_Main hangar list), don't add `Mantis` to
  `VesselChangerToy.DefaultCollection` (the menu freestyle toy's curated six). There is no
  positive "restricted-to-mode" mechanism, only omission from selection surfaces — a first for
  the fleet, and the hangar/unlock story is an open design question (§12).
- **Universality is intact**: the vessel itself carries nothing mode-coupled. Dropped into any
  scene, the drift/throttle/nudge/wall all work; the cone destroys prisms anywhere; only the
  *ball's response* is mode-side (§5.2), exactly as vessel↔ball contact already is for the Rhino.

### 5.2 Launching the ball — the one platform change

Ground truth: **no explosion can reach the ball today.** Three independent reasons, all verified:
the ball carries no `ImpactCollider`; `ExplosionImpactor.AcceptImpactee` switches only on
`VesselImpactor`/`PrismImpactor`; and the ball's own trigger handlers early-out unless the contact
has an `IVessel` ancestor. The DogFight fix (fill the skyburst's null explosion container) is NOT
sufficient here — a container only widens effects for already-supported impactee types.

**Recommended wiring — ball-side, mode-native** (keeps the vessel generic and the ball the owner
of its own contact model, exactly as it is for hulls and blades):

1. `AstroLeagueBall`'s trigger handlers grow one branch: if the entering collider resolves no
   `IVessel`, try `other.TryGetComponent(out ImpactCollider ic) && ic.Impactor is
   ExplosionImpactor exp` (the AOE prefab already carries an `ImpactCollider` for vessel pairs;
   the cone's swept CapsuleCollider is live for the whole blast).
2. **Server-only** (the same gate as every other ball resolution — non-server peers are
   kinematic dead-reckoners): read the blast:
   - direction: `AOEExplosion.CalculateImpactVector(ballCenter).normalized` — the conic override
     radiates from the **apex**, so for a short cone this ≈ the nudge direction with a small
     natural spread depending on where the ball sat in the cone. "Launches the ball in the nudge
     direction", physically.
   - magnitude: **not** the impact vector's (the conic wavefront speed is geometry-derived,
     ~43 u/s at these dimensions — debris tuning, not launch power). The launch speed rides a
     tiny payload component stamped on the cone at spawn (`MantisStrikePayload { float
     LaunchSpeed; }`) carrying the Charge-snapshotted value from the fire RPC (base 140, §4.1).
   - striker: `exp.SourceVessel` — the DogFight attribution field, stamped at spawn.
3. Apply: `rb.linearVelocity = dir × LaunchSpeed` (clamped to `maxSpeed 300`), then feed the
   **existing** strike bookkeeping: `n_LastHitDomain` recolor to the striker's domain,
   `OnStruckServer(vessel, intensity)` (the float payload is hit intensity 0..1 =
   finalSpeed / maxSpeed, not a raw speed) → `AstroLeagueController.HandleBallStruckServer` →
   `_lastStrikers` — **without this, a goal off a Strike is unattributed** (kickoff, no score) —
   plus `Strike_ClientRpc` juice (flash, pop, shake, audio) and the per-vessel
   `vesselStrikeCooldown` latch so one cone can't re-launch the ball every trigger-stay frame.

Alternative (noted, not recommended): a full new impactee type — `BallImpactor : ImpactorBase` on
the ball + a new case in `ExplosionImpactor.AcceptImpactee` + a ball-effects array on
`ExplosionImpactorDataContainerSO`. More platform surface for one consumer; revisit only if a
second mode ever needs blast-reactive objects.

Interaction feel: hull bounce (elastic, up to 2× vessel speed) and blade strikes stay untouched —
the Mantis's *hull* still plays the ball like any vessel (its skimmer doesn't move relative to
the hull, so `SkimmerSwingKinematics` reports not-ready and the hull path applies, by design).
The Strike is the ranged alternative: less raw top-end than a committed Rhino tip-slash, but
aimable in a lateral direction the vessel isn't flying, on a 1.5s cadence.

### 5.3 What a Bulwark actually does to the ball (drag, not backboard)

Verified ball–prism model, which the whole defensive design must respect:

- The ball's SphereCollider **excludes the TrailBlocks layer** — it never bounces off prisms.
  Every physics tick it sweeps `PrismSpatialIndex.QuerySphere` along its travel and resolves by
  domain vs `n_LastHitDomain`:
  - **opposing or neutral + plain** → prism eaten (`Prism.Damage`) + server-side slow:
    `speed ×= ballMass / (ballMass + prismDragMassScale × prismVolume)` (3 / 0.05 authored) —
    **direction preserved, never reversed**;
  - **same color** → prism gets **shielded** (the ball armors friendly mass);
  - **opposing + shielded** → shield popped, prism survives, **no drag that visit**;
  - **super-shielded** → untouched, zero cost.
- So a Bulwark is a **brake pad on the ball's path**. The drag applies **once per physics tick
  over the SUMMED volume eaten that tick** (`velocity ×= mass/(mass + 0.05 × ΣV)`,
  `AstroLeagueBall.ProcessPrismInteractions`) — not per-brick compounding, and at shot speeds
  (140–300 u/s ≈ 3–6u per 0.02s tick) a one-brick-thin pane resolves in a single tick. One pane
  transit (1–2 bricks on the path, same tick) cuts ball speed to `3/(3+5) = 37.5%` or
  `3/(3+10) ≈ 23%` (brick volume 100 at the proposed 10×10×1); a Deep Wall (§4.3) transit lands
  ~13–23% same-tick, down to ~5% when its two panes resolve in separate ticks. That is the
  goalkeeping verb: not a save, a **smother** — the shot arrives at the mouth as a crawl and
  becomes a loose ball.
- A Bulwark **matching the ball's last-hit color is shielded by the ball, not eaten** — your own
  wall only brakes an *opposing-colored* ball. Walls are goalkeeping against the enemy's shots,
  by construction.
- Walls do not persist across rallies: **every goal sweeps the field**
  (`ClearFieldPrismsAsync` destroys all non-super-shielded, non-fauna prisms center-out over
  1.6s). Charges therefore matter per-rally, which is what makes the crystal economy a live
  decision (chase the midfield crystal vs hold position).
- Goal detection is pure plane-crossing math within the mouth disk — a wall can never block the
  *detector*, only kill the ball's momentum before it crosses. Correct and intended.

### 5.4 The court is safe

The play boundary is collider-less analytic math (`AstroLeagueBoundary.Contain`); the nucleus
cage is a Cell-owned visual; the only mode-authored prisms are the 480 **super-shielded**
edge-lining prisms — which no-op `Prism.Damage`/`Consume` and **destroy any explosion that
touches them** (Burst and physics paths both). A Strike into the lining is eaten by the court.
Nothing to author; the platform's existing shield semantics protect the arena.

### 5.5 The crystal economy on this pitch

Two shipped sources feed wall charges (§3.3), no new spawners:

1. **The anchor crystal** — `NetworkCrystalManager` authors one neutral crystal
   (`fixedCrystalCount 1`, `Domains.Blue`) spawning and respawning at random points **inside the
   court radius**. A contested midfield pickup: detour for a wall charge vs stay on the ball.
2. **Fauna drops** — every creature carries an elemental crystal heart dropped **at its death
   position** (`LifeFormCrystal` — the platform-wide lifeform law). The mode's cleanup crew
   (tadpoles/brittlestars/piranhas) patrols **outside** the court while the pitch is Calm
   (`FaunaExclusionRadius` = the court) and pours in at Restless+ — so drops land off-court
   early, on-court exactly when the pitch is crowded. The goal sweep spares fauna bodies and
   does **not** clear crystals, so dropped hearts survive rallies.

Design consequence, stated for playtest: charge income *rises* in the late, silted phase of a
match (more mass → pen opens → grazing → starvation/predation churn → more hearts on the pitch),
which is also when defensive walls matter most. That is emergent, not authored — the food web is
the pacing mechanism, and no part of this design adds or removes crystal sources. Open
verification item: whether the anchor `Crystal.prefab` dispatches as omni (hull) or elemental
(skimmer) on collection, and that the charge pip moves on both paths (§11 step 8).

### 5.6 AI backfill — v1 posture

`AIPilot` has no throttle setter and no stick synthesis (`SetExternalTargetProvider` is the
mode's steering lever; the Sparrow's roll is already inert for AI; trigger synthesis is the
standing Phase 2.5 backlog item) — an AI-driven Mantis would idle at `MinimumSpeed` with a dead
nudge. It **does** have a prefab-authored ability loop (`AIPilot.abilities`: a list of
`{ShipActionSO, Duration, Cooldown}` fired forever on a blind cooldown), so the Bulwark — a
bound `ShipActionSO` — could fire under AI with zero new code, just with nothing aiming the
placement. Not enough to field a competent AI Mantis.
**v1: AI never flies the Mantis** — free, because the scene's `aiInitializeDatas` clamp to
`AllowedVesselClasses[0]` = Rhino (§5.1). The Mantis is human-only until the AI pass:

- throttle synthesis: `AutoPilotEnabled` → treat throttle as 1 (the single-stick "full throttle
  is implicit" semantic the base class already has — one line in `ComputeThrottleTarget`);
- nudge synthesis: fire when the ball is inside a cone-shaped window off the vessel's side and
  the launch direction scores toward the enemy goal (the executor-side AI-trigger-synthesis shape
  CONTRACT §2 prescribes for stick abilities);
- wall synthesis: optional; the blind `AIPilot.abilities` cooldown loop works day one (fires
  wherever the AI happens to be pointing), but the real version is defender-role placement on
  the predicted shot line when charges ≥ 1.

Tracked as a follow-up (§12), not a ship-blocker: the mode is 2–6 players with AI backfill, and
backfill stays all-Rhino, which is today's shipped behavior.

---

## 6. Ecology & platform-law compliance

Invariants touched, per the CLAUDE.md protocol — restated with how each is satisfied:

### 6.1 Mass is conserved; continuity of existence

- The Bulwark **creates** prisms via the standard pooled factory channel with the standard
  bloom-in stamps (`TargetScale` + `SetGrowthRate` + `Initialize`) — the clock-material law's one
  growth engine; no tweens, no bare field writes, colliders/gameplay state final at spawn.
- The Strike **removes** prisms via the standard explosion path — an active force, debris on the
  proportional-impulse contract, erosion-fade dither all shipped. No timers, no TTLs, no decay
  anywhere in the kit: walls persist until eaten (ball/fauna), destroyed (sword/cone), or
  goal-swept (existing mode behavior, an active mode event). The nudge **cooldown** paces input;
  it removes nothing from the world.
- Nothing pops: cone = standard AOE visual; wall = bloom; ball launch = existing strike juice.

### 6.2 Shield semantics (why the defaults are what they are)

Base walls plain (shielded = free pass for the ball, §5.3; super-shielded = forbidden grant AND
useless vs the ball); Ablative Wake shields **trail** for anti-sword/anti-fauna value with the
ball wrinkle documented on the upgrade itself; the Strike is `devastating` so shielded mass dies
to it, and super-shielded mass stops it (court protection for free). Danger prisms: none in this
kit — nothing to gate, and the danger-stays-domain-blind law is untouched.

### 6.3 The volume ladder — the one real retune this vessel forces

The Astro League cell's phase window is authored for Rhino trail (~0.75 volume/prism): Restless
at LiveVolume 30,600 / Frenzy 32,000 over a 30,000 super-shielded lining floor — a +600/+2,000
gameplay band. **A single Bulwark at the proposed brick volume (15 × 100 = 1,500) blows through
the entire Restless window on its own**, snapping the pen open and pouring the piranha crew onto
the pitch. This is the exact "modes must author explicit volumes for vessels with different prism
volume" clause from the ecosystem masterplan, surfacing on the wall dial instead of the trail
dial. And the dials genuinely fight: brick volume IS the braking power (drag scales with volume,
§5.3) — thin cheap bricks don't stop shots.

**Proposal**: keep bricks at volume 100 (real brakes) and retune the mode's `PhaseThresholds`
when the Mantis ships — RestlessEnter/Exit ≈ 33,000 / 32,500, FrenzyEnter/Exit ≈ 36,000 / 35,300
*(first-pass: floor + two live walls + a Rhino-match's worth of trail)* — with the stated side
effect that **Rhino-only matches now silt longer before the cleanup crew releases** (mitigated by
the ball itself eating trail and by the per-goal field sweep). This is a playtest decision, called
out in §11; the alternative (thin bricks, no retune) makes the wall cosmetic and is not
recommended. Mantis trail volume itself: keep near Rhino's (~0.75–1.5 per prism at base Mass) so
Carve doesn't move the ladder question; the Mass ElementalFloat tops at 2.5× like its siblings.

### 6.4 Collider budget (the hard gate)

Per live wall: 15 BoxColliders (pooled prisms, phase-LOD-managed), ≤ ~3 concurrent walls in the
worst case (3 charges, goal sweep clears) → ≤ 45 on a pitch whose lining already carries 480
always-on convex MeshColliders. Strike cone: 1 CapsuleCollider trigger for ≤0.35s per fire,
prism damage via Burst index sweep (zero physics vs prisms). Nudge: 0. Net: noise against the
mode's existing budget; no new query systems, no parallel spatial stores.

### 6.5 The laws with nothing to author

Speed tunnel (absolute fleet mapping; a new vessel authors nothing — validators fail a prefab
that grows its own driver) · occlusion corridor (shader-side + `IsLocalPilot` binding; the one
art note: measure the hull rotation-invariantly and mind the skinned-mesh armature-scale trap
that oversized the Sparrow's corridor 5×) · haptics (nothing added — the two-feel policy stands;
the Strike gets audio/screen juice through the ball's existing `Strike_ClientRpc`, not a new
haptic) · SOAP fail-loud (no null guards on serialized events) · MaterialPropertyBlock over
`renderer.material` everywhere.

---

## 7. New code & assets inventory

New C# (all under existing dirs, existing namespaces):

| File | Contents |
|---|---|
| `_Scripts/Controller/Vessel/MantisVesselTransformer.cs` | `SingleStickVesselTransformer` subclass: RT-analog `ComputeThrottleTarget` (§2.2) + Hair Trigger tracking-rate switch (§4.4) + AI full-throttle arm (§5.6, later) |
| `_Scripts/Controller/Vessel/MantisNudgeController.cs` | NetworkBehaviour: right-stick poll, cooldown arm, displacement + visual roll + cone spawn, fire RPCs with (dir, launchSpeed) snapshot (§3.1) |
| `_Scripts/Controller/Vessel/R_VesselActions/Data Containers/PlaceWallActionSO.cs` | Stateless wall config: charge index/cost, pane layout, `placementDistanceElemental` (§3.3, §4.3) |
| `_Scripts/Controller/Vessel/R_VesselActions/Executors/PlaceWallActionExecutor.cs` | Charge gate, TryReserve, spawn-channel pane lay, spend (§3.3) |
| `_Scripts/Controller/Vessel/MantisStrikePayload.cs` | Tiny per-blast component: Charge-snapshotted `LaunchSpeed` the ball reads (§5.2) |
| `_Scripts/UI/Controller/MantisHUDController.cs` + `_Scripts/UI/View/MantisVesselHUDView.cs` | §8 (controllers' historical drift note: new pairs go in `UI/Controller` + `UI/View`) |
| `AstroLeagueBall.cs` (edit) | The explosion branch: recognize `ExplosionImpactor` via `ImpactCollider`, server-only launch + attribution + juice (§5.2) |

New assets:

| Asset | Notes |
|---|---|
| `_Prefabs/Spacevessels/Mantis.prefab` | Built fresh (clone Sparrow for the single-stick + nudge skeleton — **never** clone the five placeholders, all of which serialize `vesselType: 0`); `VesselStatus.vesselType = 12`; root carries the Netcode trio (`NetworkObject` / `ClientNetworkTransform` / `NetcodeHooks`) and the full `[RequireComponent]` ten (incl. `ElementalBarsController` + `R_ShipElementStatsHandler`); wire `VesselStatus._shipInstance`, `vesselHUDController`, `_nearFieldSkimmer`, and `VesselController.gameData` → `Runtime GameData.asset` — the clone carries all of this, itemized so none of it is assumed |
| `Resources/ElementalAbilityMaps/Mantis.asset` | §4 map, all multipliers pinned 1 |
| `_SO_Assets/VesselActions/Mantis/` | `MantisDriftAction`, `MantisSharpDriftAction`, `PlaceWallAction`, strike-cone config |
| `_Prefabs/Projectile/MantisStrikeCone.prefab` | `AOEConicExplosion` per §3.2 + `MantisStrikeExplosionImpactorDataContainer.asset` (never null) |
| `_SO_Assets/Effects/Effect Containers/VesselContainers/MantisImpactorDataContainer.asset` | Baseline prism trio + `MantisWallChargeByCrystalEffect` in omni + all four elemental lists (§3.3) |
| `_SO_Assets/Effects/Vessel Crystal Effects/MantisWallChargeByCrystalEffect.asset` | `VesselChangeResourceByCrystalEffectSO`, +0.334 additive |
| `_SO_Assets/Camera/MantisCameraSettingsSO.asset` | Start from the Squirrel's (racer framing) |
| `_SO_Assets/Classes/SO_Class_Mantis.asset` | Correct name + location (Falcon/Shrike's `_TEMP` placement is the anti-pattern) |
| `_Prefabs/UI Elements/VesselHUD/MantisHUDVariant.prefab` | §8 |
| Skimmer | Nest `Components/Skimmer.prefab`, override the nested impactor's container with a new `MantisSkimmerImpactorDataContainer.asset`, wire `VesselStatus._nearFieldSkimmer` — then **run Audit Vessel Skimmers**; the container-null and pointer-at-disabled-twin failures are silent by design |

Edits to existing data/code: `VesselClassType.cs` (+`Mantis = 12`) ·
`EnumIntegrityTests.cs` (count 13→14 + `[TestCase]`, same commit as the enum or the suite fails) ·
`Vessel Prefab Container.asset` (+prefab — **mandatory the moment the enum member exists**:
`VesselSpawner` rolls Random over all members and an unregistered class is a LogError storm +
destroyed player) · `DefaultNetworkPrefabs.asset` (+prefab) · `ArcadeGameAstroLeague.asset`
(append after Rhino) · `AstroLeagueSettings.asset` / `Astro League Cell Config.asset`
(PhaseThresholds retune, §6.3). Prism pool: reuse `PrismType.Sparrow` for v1 (a dedicated
`Mantis` PrismType + pool prefab + `PrismFactory` field/case + scene wiring when trail art
lands). Telemetry: `DefaultVesselTelemetry` **on the prefab** with stat SOs wired (the
bootstrapper fallback warns every spawn by design). Animation: minimal `MantisAnimation`
(`VesselAnimation` subclass — a `[RequireComponent]`; element blend shapes are opt-in by art
later). `VesselCustomization._shipGeometries` populated, ≥2 material slots per hull MeshRenderer
(`ShipHelper.ApplyShipMaterial` writes `materials[1]`).

---

## 8. HUD

`MantisHUDVariant.prefab` + controller/view pair, standard lifecycle (initialized hidden, scene
hosts own visibility, symmetric Rebind/Unbind gated on `IsInitializedAsAI || !IsLocalUser`).

- **Four-icon row** (LOCKED order, charge → mass → space → time, left → right):
  **Strike · Carve · Bulwark · Overdrive**, authored at the shared row geometry from the
  variant prefab. Upgrade signal = the standard three layers; any icon doubling as a live gauge
  sets `tintIconOnUpgrade = false` and overrides `SetAbilityUpgraded` re-anchoring rest scales to
  `AbilityIconRestScale(element)` (the Squirrel reference).
- **Element flowers**: fleet-required (`ElementalBarsController` is in the `[RequireComponent]`
  set) — author them, don't rely on the loud runtime fallback: **FrogletTools > Vessels > Wire
  Elemental Petal Bars** on the HUD variant, then assign `ElementalBarsController.elementBars`
  on the vessel prefab.
- **Wall charge pips**: 3 discrete pips as **sibling** images of the Bulwark icon (never the
  ability icon itself — it belongs to the upgrade tint/badge system), driven event-only off
  `ResourceSystem.OnResourceChanged(index, current, max)` → `state = Round(current × 3)` — the
  Sparrow `missileIcons` sprite-state display. Subscribe the HUD controller to `ResourceSystem`
  directly (the Serpent HUD's pattern; the Sparrow reaches the same event transitively, through
  its gun executor's index-filtered `OnAmmoChanged` forward). Pips bloom/wither on change
  (continuity applies to UI).
- **Nudge pip**: one binary ring on/near the Strike icon — armed color ↔ spent-and-recharging,
  `DOFillAmount` wipe + spend punch, the Sparrow `rollChargeIndicator` pattern exactly (binary
  stays visibly binary; a partial fill would read as a meter).
- **Control hints**: LT → Carve and A → Bulwark derive automatically
  (`InputHintBindingMap` → `LeftStickAction`/`Button1Action` → map entries → icons). RT → the
  Time entry's `Input = RightStickAction (1)` places the RT glyph on Overdrive even with no
  ShipActionSO bound to the event (the map is the hint system's first lookup). **The right-stick
  nudge has no hint path at all** — no `InputEvents` member exists for stick deflection and
  `InputHintBindingMap` has no right-stick glyph. Smallest fix *(proposal)*: add a
  `HintBinding.PadRightStick` glyph entry mapped to a new hint-only `InputEvents.RightStickFlick
  = 14` that the Strike's map row declares as its `Input`; no strategy ever raises it, it exists
  so the hint has an address. Flagged for approval (§12) — it grows a shared enum.

---

## 9. Tuning knobs (first pass — all *(proposal)*)

| Knob | Where | Value |
|---|---|---|
| `DefaultMinimumSpeed` / `DefaultThrottleScaler` | prefab transformer | 10 / 90 |
| Pitch/Yaw/Roll scalers · `RotationThrottleScaler` | prefab transformer | 100/100/30 · 0.1 |
| `ThrottleScalerMultiplier` (Time) | prefab transformer ElementalFloat | 1 → 1.5, Enabled |
| Drift single / sharp (`Mult`, damping) | `MantisDriftAction` / `MantisSharpDriftAction` | 1.4, 0.5 / 1.8, 0.25 |
| `nudgeSpeed` / `nudgeDurationSeconds` / `nudgeCooldownSeconds` | `MantisNudgeController` | 80 / 0.5 / 1.5 |
| `rootRollDegrees` / `perimeterThreshold` | `MantisNudgeController` | 15 / 1.0 |
| Cone `height` / duration / core half-angle | `MantisStrikeCone.prefab` | 60 / 0.35 / ~25° |
| Cone debris group (`debrisRestitution` × `Inertia`) | `MantisStrikeCone.prefab` | ⅓ × 1.8 = 0.6 (move together) |
| `ballLaunchSpeed` base / `atFullCharge` | strike SO | 140 / ×2 (ball cap 300) |
| Wall pane / brick scale / brick volume | `PlaceWallActionSO` | 5×3 / (10,10,1) / 100 |
| `placementDistanceElemental` (Space) | `PlaceWallActionSO` | 150 → 300 |
| Wall charges: max / start / cost / crystal grant | prefab `ResourceSystem` + effect asset | 3 / 1 / 1 / +1 |
| Mode `PhaseThresholds` retune | `Astro League Cell Config.asset` | Restless 33,000/32,500 · Frenzy 36,000/35,300 |
| Hair Trigger tracking rate (Time 5) | strike/transformer SO | 600 u/s² |

---

## 10. What this vessel deliberately does NOT have

No boost button (the throttle IS the speed story) · no guns · no danger trail · no SuperShield
grants anywhere · no skimmer swing kinematics (hull-path ball contact is intended; the Strike is
the ranged verb) · no haptics beyond the platform's two feels · no mode-local vessel check, no
parallel wall/crystal/spawner systems — every mechanic above rides a shipped fundamental
(prisms/mass, crystals, elementals, domains, the cell's food web) or a shipped primitive (the
roll, the cone, the spawn channel, the resource meter, the clamp list).

---

## 11. In-editor verification (when implemented — a human at the editor)

Auditors first (all asset-only): **Audit Vessel Ability Rows** (4/4 icons, order, hints),
**Audit Vessel Skimmers** (container + wiring — its failures are silent in play), **Audit Vessel
Elemental Morphs** (reports no-shapes until art lands, expected), **Audit Corridor Vessel Radii**
(hull-only, catch the armature-scale trap), **Validate Speed Tunnel Law**, plus
`EnumIntegrityTests` green. Then in `MinigameAstroLeague` (2 humans via MPPM where noted):

1. **Throttle**: RT at rest → speed eases to 10; full pull → 100; Time L10 (debug-seed) → 145.
   AI Rhinos unaffected. Kickoff park still zeroes speed; first pull recovers.
2. **Drift**: half LT ≈ single-tier feel, full LT = sharp; course visibly decouples from nose;
   trail lays travel-aligned (DriftTrail dot product); speed unchanged while drifting.
3. **Nudge**: right stick to perimeter → lateral shunt + 360° visual roll + cone VFX on the
   correct side; camera does not roll; pip spends and re-arms in 1.5s; holding the stick pinned
   does not re-fire. MPPM: remote peer sees roll + cone (RPC replay), no double-fire on owner.
4. **Strike vs prisms**: own trail + enemy trail + a Bulwark inside the cone → all shredded
   (base `affectSelf 1`); with Charge 5 seeded → own domain's mass survives (Surgical Strike);
   cone into the edge lining → blast dies, lining untouched.
5. **Strike vs ball** (MPPM, client fires): ball inside the cone → launches along the nudge
   direction at ~140 (280 at Charge L10), recolors to striker domain, strike juice plays,
   and a goal scored off it **credits the striker** (attribution — the step that fails if
   `OnStruckServer` isn't fed). Ball outside the 60u height → untouched.
6. **Bulwark**: A with 1+ charge → pane blooms ⊥ course at 150u (300 at Space L10 — measure);
   A at 0 charges → refusal, nothing spawns; bricks respect occupancy (place into own trail —
   no overlaps). Opposing ball through the pane → visible speed kill (summed-volume drag, one
   pane ≈ 23–38% of incoming speed — §5.3); own-colored ball through it → bricks get shielded,
   no drag. Goal → sweep clears walls, crystals survive.
7. **Deep Wall** (Space 5 seeded): one charge → two panes, deeper speed kill.
8. **Charges from crystals**: collect the midfield anchor crystal → +1 pip; collect a fauna-drop
   elemental crystal → +1 pip AND normal element-level seed. ⚠ If the elemental path doesn't
   move the pip, the skim-vs-hull dispatch question (§12) is answered "hull-only" — record which.
9. **Ladder**: place 2 walls + normal trail → Restless crosses at the retuned threshold; pen
   opens; piranhas eat the walls (they are plain prisms — food). Confirm a Rhino-only match
   still reaches Restless before full-time under the retune.
10. **Menu isolation**: Mantis absent from hangar, vessel-changer toy, and every other arcade
    card's carousel.

Anything not verifiable this way gets its 🔴 entry in `Docs/UNITY_VERIFICATION_CHECKLIST.md` at
implementation time.

---

## 12. Open questions & follow-ups (for markup)

1. **Name + ID**: Mantis = 12 (mantis shrimp). Sign off, mindful of the Manta `mantis_*` FBX
   naming hazard (the preamble's naming-trap note).
2. **Element map + upgrade names** (§4): Surgical Strike / Ablative Wake / Deep Wall /
   Hair Trigger — the FLEET_MAPS §2 row awaits markup; per the gate, nothing is authored until
   then.
3. **Hangar/unlock surface**: an Astro-League-only vessel has no precedent — hidden from the
   hangar entirely (pure mode content), or listed-but-locked with `UnlockCost` as its economy
   hook? Affects `SO_Classlist_*` membership and `IsLocked` carousel filtering.
4. **Right-stick hint** (§8): approve the hint-only `InputEvents.RightStickFlick = 14` +
   `PadRightStick` glyph, or accept a hint-less Strike icon (the audit will flag it).
5. **Touch**: no `Button1Action` raise site exists on touch — on-screen Bulwark button (new
   surface) or gamepad/keyboard-only at v1?
6. **AI Mantis** (§5.6): v1 ships human-only (AI backfill stays Rhino via list order); the
   throttle/nudge/wall synthesis trio is the follow-up. Acceptable?
7. **Ladder retune** (§6.3): confirm the PhaseThresholds direction (and that Rhino-only matches
   silting longer is acceptable) — the one change that touches matches with no Mantis in them.
8. **Ball-launch wiring** (§5.2): ball-side branch (recommended) vs new ball impactee type.
   Also: verify at implementation whether the anchor crystal dispatches omni or elemental, and
   whether skimmer-collected crystals reach the vessel-side charge effect (the §11.8 check).
9. **Falcon overlap**: Falcon's placeholder prefab plans a `SeedWallAction` (trail-seeded
   assembler wall). The Bulwark is a different primitive (remote placed pane) — no code
   collision — but two wall-flavored vessels is a fleet-identity question worth a conscious yes.
