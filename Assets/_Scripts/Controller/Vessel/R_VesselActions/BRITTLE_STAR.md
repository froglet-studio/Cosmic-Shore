# Brittle Star — the five-pointed one-touch flyer

**Status:** built 2026-10-14 from the Notion design document *Class - Brittle Star* (Design Documents ▸ Class Design
Documents ▸ Sparrow Chain). `VesselClassType.BrittleStar = 16`. Redesigned 2026-10-16 (four modes, §1 and §3) and
**played by Garrett the same day** ("one of the most fun things we've ever made"); the step-by-step walk in
[In-editor verification](#in-editor-verification) has not been reported item by item.

> Research, the design document transcribed, the studio and its scorecards: `Docs/Studios/BRITTLE_STAR.md`. Fly it first in
> the Vessel Studio (`Docs/Studios/VesselStudio/brittlestar.html`, the Unity build set).

> *"A complex one-touch flyer. With different modes concentrated on destruction, creation, and maneuverability, the
> Brittle Star is an all-in-one tutorial on the more advanced systems of the game."* Superlative: **best at short-range
> barrages for pinpoint destruction with minimal investment.**

## 1. What it is

A cockpit hub inside a **pentagon support ring**, joined by five thin arms (the animal's). Every point of the pentagon
carries an **engine pod** at the back and a **gun** at the front; the cockpit carries a sixth gun on its nose. It flies on
the fleet's one-thumb model (`SingleStickVesselTransformer`, via `BrittleStarVesselTransformer`).

### Controls (redesigned 2026-10-16, Garrett)

| Control | Gamepad | Keyboard / one-thumb mouse | Does |
|---|---|---|---|
| Speed Mode | **A** | Space | Select Speed Mode (press again: back to Cruise) |
| Creation Mode | **B** | R | Select Creation Mode (press again: Cruise) |
| Space Mode | **X** | Q / middle mouse | Select Space Mode — the five guns (`Mode.Destruction` in code; press again: Cruise) |
| Charge Mode | **RB** | E | Select Charge Mode — the five cords (press again: Cruise) |
| RT (held, **analog**) | **RT** | Right Shift / LMB (read as a full pull) | Space Mode: the five guns' rate. Charge Mode: the cords from whip (released) to saber (pulled). Every other mode: the one aimed turret's rate |
| Aim | **right stick** | P ; L ' (no aim on the mouse scheme) | Direction turns the ring (brings a turret round; turns the five guns or cords with it); deflection moves the guns / cords in and out |
| Ring Detonation | **LT** | Left Shift / RMB | Throw the ring — only with a spare ring in stock |

Bindings are in the **shared** list (`_inputEventShipActions`), the Sparrow's shape, so touch reaches RT and Ring
Detonation through its own trigger gestures. Charge Mode is on `FlipAction` (InputEvents 3: RB / E), the only free face
control the input layer raises; on touch that event comes from turning the phone (`DeviceOrientationHandler`), so a
landscape flip toggles Charge Mode there. Touch has no face buttons for the other three modes (follow-up F1).

Modes are **exclusive**: one at a time, or **Cruise** (none). All four need the mounted ring (off only for the half
second the ring regrows after a throw).

Concept art from the design studio's first pass: `Docs/Studios/BrittleStar/*.jpg` (the ring interceptor, each mode, the
ring detonation, the Zephyr's circle ring). It draws the ring **suspended** round the cockpit by energy, not joined by
struts — the shipped hull still has physical arms (follow-up F8).

## 2. The hull (`BrittleStarHullForm` / `BrittleStarHullBuilder` / `BrittleStarAnimation`)

- **Ring size** (Garrett, 2026-10-14): radius **6** — 2.5× the first cut — so Creation Mode's five corner trails lie
  12 u apart across the path. The vessel's hit box still spans only the cockpit and the inner arms (4.6 × 4.6 × 2.4):
  the ring is the crosshair's frame, not a 12-unit wall that collides with every prism near the line. The camera
  sits straight behind with **no height offset** — the ring frames what you fly at.
- **Pure geometry**: `BrittleStarHullForm` (settings in, parts out). Part 0 is the **cockpit** (on the builder's own,
  painted renderer); part 1 the **ring** (struts, arms, pods and barrels — one mesh, a child that spins about the nose
  axis and is what Ring Detonation throws); parts 2–7 the six **flames** (domain-coloured cones the animation stretches).
  Every primitive is convex and orients its faces away from a point inside itself, so winding is right by construction;
  the tests assert every part encloses positive volume (the non-tautological half) and that no element flips a face.
- **Element morphs** are the document's art direction: **Mass** — larger cockpit (×1.45 radius); **Charge** — larger
  thrusters and guns (pods ×1.5, barrels ×1.6 thick); **Space** — elongated barrels (×2.2); **Time** — the outer ring
  curves from a pentagon into a **circle** (the Zephyr's art). Bakes and blending are the Stoat's / Thresher's shape.
- **Animation**: the cockpit flame is always lit; the pod flames are embers at rest and light on the **outside of a
  turn** ("the outer thrusters should only engage as the vessel banks"); **Speed Mode lights all five** ("all of the
  thrusters should engage at once"); overheated guns lengthen every flame (`overheatFlame`, the hull venting heat). The ring turns with the **firing rotation**, and is
  gone while thrown and **blooms back** (ease-out-back) while rebuilt. The drawn hull leans 28° into a full-stick turn
  (fleet-scale puppetry, vessel rule 24). Channel discipline: the animation writes transforms only; the builder writes
  vertices and the muzzles' positions.
- **Card icons** are renders of this hull (`Tools/Build/render_brittlestar_card_icon.py`), the four ability icons are
  `Tools/Build/author_brittlestar_icons.py` (the Thresher's icon language).

## 3. The abilities (`BrittleStarExecutor`, tuning `BrittleStarConfigSO`)

### 3.1 Speed Mode — TIME
- **×6 speed** (Garrett, 2026-10-14): cruise × `speedModeMultiplier` (**×6 at rest → ×8 at Time 10**, floored ×4) —
  45 u/s → 270 u/s. The multiplier scales the transformer's throttle TARGET (`BrittleStarVesselTransformer.SpeedModeMultiplier`,
  one writer: the executor), so the hull eases into the new speed through the fleet's `StepTowardTarget` and its turn
  rates follow `speed` as for a boost (the Sparrow's `RotationThrottleScaler` 0.1: 84°/s at cruise, 107°/s at ×6).
  Not `BoostMultiplier` (rule 23: it ratchets) and not `IsBoosting` (every boost listener would react). The Sparrow
  block's Time-scaled `BoostSpeedMultiplier` is **off** on this hull.
- "Speed mode increases the wavelength of the Brittle Star's volume production so as not to increase production output":
  `BrittleStarPrismController.SpacingMultiplier` stretches the base trail's delay by the same factor
  (`speedModeSpacingFollow` = 1), so prisms per second hold — at ×6 the blocks are ~36 u apart. The spacing follows the
  hull's **actual** speed over its plain cruise, not the mode's target (no "momentum hole" while it coasts down).
- **The prisms stretch with it** (Garrett, 2026-10-16: "stretch its prisms so its trail connects better"):
  `BrittleStarPrismController.LengthMultiplier` = the spacing factor × `speedModeStretch` 1 lengthens each block along the
  path (`ApplyBoostScale`), so the fast trail's blocks fill as much of their spacing as the cruise trail's do — a 3 u
  block at ×6 is 18 u long. A stretched prism is a stated size, admitted past the pool's scale window after `Initialize`
  (`OnBlockSpawned`, the base spawner's own pattern for widened lays).
- **Speed is the fastest mode; Creation the slowest** (`BrittleStarRules.ModeSpeed`, tested). Space and Charge Mode fly at
  cruise.

### 3.2 Creation Mode — MASS
- **A trail behind each ring corner**: five lines, one per corner, each its own `Trail`, laid `creationOffset` 2 u behind
  the corner every `creationSpacing` 6 u of path, each block's length along the path and its broad side tangent to the
  ring. They turn with the ring's firing rotation, so aiming while building twists the five lines round each other.
- **The same small block as the base trail** (Garrett, 2026-10-16: "use the same smaller prisms for all 5 prism trails
  in mass mode"): the spawner's own `BaseScale` (`VesselPrismController.TrailBaseScale`, a read-only getter added for this)
  with its **x and y × `prismWidthScale` ×1 → ×2 at Mass 10** (`EvaluateReplicated`; §3.3) — so all six trails always
  lay one block. (Until 2026-10-16 this was `creationBlockScale`, ×1 → ×1.8 on all three axes, corner trails only.)
  At most 3 blocks per corner a frame, excess dropped (rule 17).
- **The slowest mode**: cruise × `creationSpeedMultiplier` **×0.5**.
- While Creation is on, the cockpit's own trail is replaced: the base spawner's pen is held up (`SetSpawnerPaused`, on
  EDGES only, rule 48). The corners lay only while the spawner runs, above 3 u/s and not attached.

### 3.3 The base trail
- "A single small block when not in creation mode": `BaseScale` **(1, 1, 6)** (Garrett, 2026-10-16: "reduce the base
  prism size", then "stretch the prisms in z and decrease them in x and y at base"; was 2.5 × 2.5 × 4.5, then
  1.5 × 1.5 × 3), wavelength 6 — so at cruise the blocks meet end to end — `prismType` Sparrow, laid 2 u behind the
  cockpit, skimmer clearance on. The fleet spawner lays a single (gapless) trail at `BaseScale.x / 2` wide, so the base
  trail's block is 0.5 × 1 × 6 while the corner trails, which read `TrailBaseScale` whole, lay 1 × 1 × 6 (inherited
  from the first cut, not introduced here).
- **MASS is the blocks' girth** (Garrett, 2026-10-16: "mass can control the x and y"): `prismWidthScale` **×1 → ×2 at
  Mass 10** on the x and y of every block the hull lays, never its length — the base trail through the fleet spawner's
  `WidthMultiplier` / `ThicknessMultiplier` (the executor is their single writer on this hull, `ApplyBlockGirth`, and
  resets them on disable), Creation Mode's corners at lay time.
- **Danger trail**: while the guns are OVERHEATED in Space Mode, the spawner lays **danger prisms** — the fleet's
  `EnableDangerMode` / `DisableDangerMode` (the `IsDangerous` flag through `Prism.Initialize`), written on edges only.

### 3.4 The guns and their heat — Space Mode (SPACE) and every other mode
- **One gun, everywhere** (Garrett, 2026-10-16): the Sparrow's pooled `SparrowProjectile` at its own size × Charge
  (`projectileScaleMultiplier` ×1 → ×1.8), `projectileSpeed` **135 u/s**, swelling ×3 in flight (`roundGrowthFactor`),
  detached, stopped by the first prism, inheriting the hull's velocity, friendly fire on mass (the prefab's container),
  a scored bullet hit on a rival hull.
- **Range**: `projectileTime` **4.5 s** — 150% of the 3 s before ("increase base gun range to 150%") — so 135 × 2T/π =
  **387 u**, × `rangeMultiplier` **×1 → ×2 at Space 10** ("double that through the space scaling") = 774 u.
- **Which guns fire** (`BrittleStarRules.GunsFor`, tested): **Space Mode** all five turrets, each along its arm's line —
  stick IN, the five meet the nose axis `armFocusDistance` 120 u ahead; pushed OUT, each splays `armSplayDegrees` 25°
  (`BrittleStarRules.ArmDirection`) — **Charge Mode** none (RT works the cords); **Cruise, Speed and Creation** the ONE
  turret the ring has turned to the aim, straight ahead, **no spread** ("the same as the space gun but only one"). That
  turret holds when the stick centres, as the ring does; only while the ring regrows does the cockpit's own gun fire.
- **RT is analog** ("the trigger should control the firing rate in an analog fashion"): each firing gun shoots
  `fireRate` **90/s × the pull** (the Sparrow's full-auto cadence at a full pull). A press owes one volley at once; fire
  is owed in seconds and paid in whole volleys, at most four a frame (rule 17). The depth is the owner's own analog
  trigger; a device without one (keyboard, mouse), the autopilot and **every remote replica read a press as a full pull**
  — only the press and release replicate (the Thresher's rule, `THRESHER.md` §7).
- **Heat** (`BrittleStarRules.HeatState`, tested): every round adds `heatPerRound` 0.0012, so heat per second is the pull ×
  the guns firing ("proportional to firing rate (trigger pull) and number of guns firing"). Heat bleeds off at `coolRate`
  0.12/s, plus `chargeCoolRate` 0.6/s × how far Charge Mode's cords are out ("using charge lasers cools the guns"). At 1
  the guns **overheat**: nothing fires until heat falls to `overheatRecoverBelow` 0.35, and while overheated **in Space
  Mode the trail lays danger prisms** (§3.3). Measured on the shipped numbers: the five guns at a full pull overheat in
  ~2.4 s; one turret at a full pull (0.108/s) runs under passive cooling and never does; recovery takes ~5.4 s passively,
  ~0.9 s with the cords out.
- **SPACE holds more heat** (Garrett, 2026-10-16: "overheat 3x slower at max… have the maximum heat scale with space"):
  `heatCapacity` **×1 → ×3 at Space 10**, the heat the guns hold in Space 0 units. Heat and cooling are in those units, so
  a full-pull burst of the five guns runs **~7.1 s at Space 10** (three times ~2.4 s) at any rate; `Heat01` stays the
  share of the current capacity (the HUD's card). Recovery comes after shedding the same 0.65 of heat at every Space
  (`HeatState.RecoverMark`), so the lockout stays ~5.4 s — Space lengthens the run, never the wait.
- **Cost**: Space Mode at a full pull fires 450 rounds/s; overheat ends a burst after ~2.4 s at Space 0 (rounds fly
  4.5 s: ~1,100 alive per hull per burst) but ~7.1 s at Space 10 (rounds fly 9 s: **~3,200 alive**). The turrets' pool is
  1400 / max 2400, sized for Space 0: past it `GenericPoolManager` creates rounds on demand and destroys the extras on
  release. Profile a Space 10 burst before raising the pool (F9).

### 3.4a Charge Mode — the five cords (CHARGE)
- **Five cords, one from each turret** (Garrett, 2026-10-15 → 2026-10-16: "make these melee cords behave like whips, so
  pulses send emergent low speed wave motions from a high inertia low elasticity cord that extends to 150% of its current
  length. pulling the trigger should shrink it to the current 100% and rigidify it so they are more like 5 straight
  sabers. this should be analog so they go from rigid to loose"). They grow out of the muzzles as the mode comes on
  (`modeBlendSeconds`), draw back in as it goes off, run only while the trail does and with the ring mounted.
- **The model** (`BrittleStarCord`, pure, tested): a discrete **wave equation on the link directions**. Each of
  `cordLinks` 16 links is a unit vector laid end to end from the muzzle, so the cord cannot stretch (**low elasticity**).
  Each direction is pulled toward the average of its neighbours, `u'' = k (u[i-1] − 2u[i] + u[i+1])`, the muzzle's arm
  line the fixed end and the tip free — a string's wave equation. Nothing is scripted: a turn, a ring flick or a stick
  sweep bends the root and the bend **travels** down the cord at ~√k links a second, reflects off the free tip and rings
  on (**emergent low-speed waves**); the directions live in world space, so the hull turns under the cord and the cord
  follows a moment later (**high inertia**). Semi-implicit Euler, sub-stepped at 1/120 s (stable for h·2√k < 2), so 30
  and 144 fps whip alike (tested within 30%).
- **RT, analog, from whip to saber**: the pull (eased at `cordRigidityRate` 12/s so a digital press does not snap)
  moves every dial at once — length `cordLength` 80 u × `cordLooseLengthScale` **1.5 → 1** (120 u whip → 80 u saber),
  wave stiffness `cordLooseWave` **144** (2026-10-16: 80% of the first 180, "even less stiff") → `cordRigidWave`
  2500 /s², damping `cordLooseDamping` **0.4** → `cordRigidDamping` 14 /s, and a pose spring `rigidity² × cordPose`
  1600 /s² onto the straight arm line. **A full pull is not fully rigid** (Garrett, 2026-10-16: "not completely rigid at
  full press… 30% of the current max elasticity", then 2026-10-17: "further reduce… to 40%"): it shortens the cords all
  the way to 80 u but takes the stiffness dials only `cordMaxRigidity` **0.4** of the way from whip to rigid, so the saber
  keeps 60% of the whip's give (tested:
  a flick bends it more than the rigid dials, under half as far as the whip, and it settles straight). Measured on the
  first whip (180, tests, 72° ring flick): the flick reaches link 4 at ~0.5 s, the middle at ~0.8 s and the tip at
  ~1.3 s, and is still swinging 2.5 s later; the 144 whip carries it ~10% slower.
- **The right stick** works the cords as it works the guns: direction turns the ring and the five cords with it,
  deflection moves them in (meeting ahead) and out (splayed 25°).
- **They slice prisms** — the Thresher's chain cut: every frame each link is swept through `QuerySegment`, and every prism
  within `cordCutRadius` 0.6 × Charge (×1 → ×1.8) is `Prism.Slice`d along the plane that link sweeps, debris at 0.5 of
  the link's velocity (capped 200 u/s). Friendly fire on mass, except this pilot's own fresh trail; a shield pops first;
  super-shielded refuses. A cord across a rival hull is a scored **Strike** at the fleet price, once per victim per
  `cordHitCooldown` 0.5 s, petals only. Cost: 5 × 16 = 80 segment queries a frame per hull in Charge Mode.
- **Look**: `ThresherChainMaterial` (the fresnel tube), a Catmull-Rom curve through the links (`ThresherChainLinks.Smooth`)
  at exactly the width that cuts, iron body under a domain-coloured rim. `cordSliceEvent` ships EMPTY.

### 3.5 Ring Detonation (LT)
- **LT throws the ring** forward at `ringLaunchSpeed` 240 u/s on top of the hull's velocity, spinning. It blows on
  touching any mass (every domain but the pilot's own fresh trail) or an enemy hull, or on its `ringFuseSeconds` 1.1 s
  fuse, in a **ring explosion** (Garrett, 2026-10-14: "a ring explosion that destroys things and strips elements from
  players in a disk that is the diameter of the nucleus and as thick as the ring"):
  - a flat **disk** lying in the ring's plane at the moment it blew, centred where it blew;
  - **as wide as the nucleus**: radius = the `NucleusVisualWorldRadius` of the cell it blew in (the nucleus's geometry,
    not its control radius, which some modes collapse to 0), else that cell's `ExpectedNucleusWorldRadius`, else the
    nearest active cell's, else `ringBlastFallbackRadius` 200 u — the standard cell's nucleus is ~392 u, so the disk is
    ~780 u across;
  - **as thick as the ring**: ± the ring's strut half-thickness (`BrittleStarHullBuilder.RingHalfThickness`, 0.22 u) ×
    **`blastScale` ×1 → ×4 at Charge 10**. A prism is caught when its own half-extent along the disk's normal overlaps
    the disk (`BrittleStarRules.DiskReaches`, tested — a disk, not a sphere: a block a few units off the plane survives);
  - swept out from the ring's own radius to the nucleus's over `ringBlastSeconds` 0.7 s (ease-out, ten queries per
    explosion, not one per frame), **destroying every prism it passes**, any domain — "destroying both enemy and allied
    mass" (shields pop first; super-shielded mass refuses: `Prism.Damage`);
  - **stripping petals** from every rival hull it passes: a scored `MissileBlast` hit through `CombatHitDrain`, once per
    hull per explosion (petals only — nothing is moved);
  - drawn as an expanding band in the domain's colour (`ringBlastBandWidth` 6 u wide, as thick as the disk), one pooled
    dynamic mesh per live explosion.
  The fleet AOE prefab and the plane wave of the first cut are retired.
- **The ring stock** (Garrett, 2026-10-16: "only let them shoot the ring if they have a spare one that automatically
  regrows. they can store two extra rings, and they get 1 ring per crystal"; `BrittleStarRules.RingState`, tested):
  - the hull always carries a ring; **throwing it needs a SPARE**, and the mounted ring then **regrows on its own** over
    `rebuildSeconds` **3** (Garrett, 2026-10-16; was 0.5), blooming back — the hull is never ringless, and the modes are
    off only during those three seconds;
  - a crystal adds **one spare**, up to `maxSpareRings` **2**; at a full stock it adds nothing;
  - a hull starts with no spare, so its first throw needs a crystal.
  This retires the class document's one stored charge, its **Overdrive** (a crystal while charged: all modes at once —
  incoherent with four modes whose RT means different things) and the ringless, modeless state.
- Crystals: `BrittleStarRingChargeByCrystalEffectSO`, wired into **every** crystal list of
  `BrittleStarImpactorDataContainer` (omni and the four elements — the document says "a crystal"). Both dispatches run on
  every peer, so every copy of the stock agrees.
- **Charge is threat** (Rolling Thunder: "larger projectile size and larger AOE on support ring detonation"):
  `blastScale` (the disk's thickness) ×1 → ×4 and `projectileScaleMultiplier` ×1 → ×1.8 on the rounds and the cords' width.

### 3.6 PvP is petals only — how the document's vessel effects were carried (LOCKED, 2026-10-10)

The class document predates the rule *"no other pvp in the game … we should only be affecting petals"*
(`Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`; `Docs/ELEMENTAL_ECONOMY.md` §9, enforced by
`Tools/Build/check_vessel_on_vessel_motion.py`). So:

| Document | Shipped |
|---|---|
| Shots "knock back enemy vessels, reducing their momentum", stacking per hit | Every round on an enemy hull is a **scored bullet hit** (petals ejected, 1 point); a Charge Mode cord across one is a scored **Strike** (8 points, once per victim per 0.5 s). Nothing moves or slows the hull. |
| Destruction Mode hits "disable enemy vessel mass production temporarily" | Retired (CONTROL tier). Space Mode's five guns take petals five times as fast as one; Charge Mode's cords strike. |
| Ring blast: knocked back, momentum reduced, trail disabled | The ring explosion's scored **MissileBlast** hit, once per hull per explosion, through `CombatHitDrain.TryAdmit` (a warded pilot is neither scored on nor robbed). |
| Space (Hunter): "increased shot range and **nudge intensity**" | Range, and the guns' heat capacity (2026-10-16) — the nudge went with knockback. |
| Friendly fire "accidentally disable allied mass production" | Friendly fire stays on **mass** (rounds and blasts break allied prisms); nothing touches an allied hull. |

## 4. HUD (`BrittleStarHUDController` / `BrittleStarHUDView`, `BrittleStarHUDVariant.prefab`)

The four-icon row, charge → mass → space → time: **Charge Mode · Creation · Space Mode · Speed**. One gauge per card, the
CTA lime while that card's mode is on: the **Charge** card is the spare-ring stock (0, half, full of two); the **Space**
card is the guns' heat, in the hot colour (`hotColor`) while overheated; **Creation** and **Speed** are each mode's eased
switch. The controller polls this vessel's own executor (a serialized reference, rule 14) behind the local-pilot gate.
The view keeps its serialized field names (`ringGauge` is the Charge card, `destructionGauge` the Space card) so the
variant's wiring holds. The Charge card still wears the Ring Detonation icon (follow-up F12).

## 5. The elemental map and level 5 (`Resources/ElementalAbilityMaps/BrittleStar.asset`)

| Element | Ability | Input | The one dial | L5 |
|---|---|---|---|---|
| Charge | Charge Mode (the five cords; the card shows the spare rings LT throws) | RB (3) | cord width and rounds ×1 → ×1.8, disk thickness ×1 → ×4 | **open** |
| Mass | Creation Mode | B (7) | corner-trail blocks ×1 → ×1.8 (the mode flies ×0.5) | **open** |
| Space | Space Mode (the five guns; RT fires one turret in the other modes) | X (8) | every gun's range ×1 → ×2 | **open** |
| Time | Speed Mode | A (6) | speed ×6 → ×8 | **open** |

`element_ability_table.py BrittleStar`: abilities 4/4, scaling 4/4, L5 gates 0/4, **0 disagreements**.

**The document names no level-5 upgrades, and the vessel skill's design gate forbids inventing one**, so all four are
open slots (empty `UpgradeLabel`). Proposals for Garrett to mark up (none implemented):

| Element | Proposal A | Proposal B |
|---|---|---|
| Charge | **Shielding Cords** — the cords armour your own mass instead of cutting it (the Thresher's chain) | **Domain-Safe Ring** — the disk spares your own domain's mass (the Sparrow's Charge 5 shape) |
| Mass | **Shielded Corners** — the corner trails arrive shielded (the Stoat / Thresher Mass 5 shape) | **Webbing** — every fifth block bridges to the next corner, stitching the five lines into a tube |
| Space | **Piercing Rounds** — the rounds pass through the first prism (the Sparrow's Space 5) | **Cold Barrels** — Space Mode heats half as fast |
| Time | **Afterburner Ring** — a thrown ring inherits Speed Mode's speed and fuses later | **Quick Build** — the rebuild is instant |

## 6. AI (input-only)

`AIPilot.abilities` is empty: the executor's autopilot presses the **same replicated controls** a human presses
(`PerformShipControllerActionsReplicated`), on the simulating machine only. It holds RT (a full pull) while ≥ 2 rival
prisms or a rival hull lie on the line ahead (min 0.4 s hold / release). Every 3 s it picks a mode: a dense wall (≥ 8)
inside cord reach — or overheated guns with targets near, which the cords cool — takes **Charge** (its held RT makes the
cords sabers); a dense wall further out takes **Space** (unless overheated); otherwise **Speed** (decided by
`BrittleStarSpeedModeAIPolicySO`, the fleet's boost law) or **Creation**. It throws a ring only with a spare: into a big
cluster (≥ 18), or into any cluster when its stock is full (a crystal would be wasted). Persona AI is follow-up F3.

## Files

| Role | Path |
|---|---|
| Pure hull geometry + morphs | `_Scripts/Controller/Vessel/BrittleStarHullForm.cs` |
| Hull emitter (meshes, muzzles, materials) | `_Scripts/Controller/Vessel/BrittleStarHullBuilder.cs` |
| Pure rules (ring life, modes, mode speed, aim, spread, arm direction, the disk, range) | `_Scripts/Controller/Vessel/BrittleStarRules.cs` |
| Pure cord physics (Destruction Mode's five cords) | `_Scripts/Controller/Vessel/BrittleStarCord.cs` |
| Flight model (Speed Mode multiplier) | `_Scripts/Controller/Vessel/BrittleStarVesselTransformer.cs` |
| Base trail (Speed Mode spacing) | `_Scripts/Controller/Vessel/BrittleStarPrismController.cs` |
| Executor (everything) | `R_VesselActions/Executors/BrittleStarExecutor.cs` |
| Controls / tuning | `R_VesselActions/Data Containers/BrittleStarActionSO.cs`, `BrittleStarConfigSO.cs` |
| Crystal pickup | `_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Crystal Effects/BrittleStarRingChargeByCrystalEffectSO.cs` |
| AI Speed Mode policy | `_Scripts/Controller/AI/Boost/BrittleStarSpeedModeAIPolicySO.cs` |
| Animation | `_Scripts/Controller/Animation/BrittleStarAnimation.cs` |
| HUD | `_Scripts/UI/Controller/BrittleStarHUDController.cs`, `_Scripts/UI/View/BrittleStarHUDView.cs`, `_Prefabs/UI Elements/VesselHUD/BrittleStarHUDVariant.prefab` |
| Prefab | `_Prefabs/Spacevessels/BrittleStar.prefab` |
| Assets | `_SO_Assets/VesselActions/BrittleStar/` (config + 5 controls), `_SO_Assets/Camera/BrittleStarCameraSettingsSO.asset`, `…/VesselContainers/BrittleStarImpactorDataContainer.asset`, `…/SkimmerContainers/BrittleStarSkimmerImpactorDataContainer.asset`, `_SO_Assets/AI Boost Policies/BrittleStarAIBoostPolicy.asset`, `_SO_Assets/Classes/SO_Class_BrittleStar.asset`, `Resources/ElementalAbilityMaps/BrittleStar.asset` |
| Tests | `_Scripts/Tests/Editor/BrittleStarTests.cs` (29: five on the cords) |
| Creation tool | `Tools/Build/author_brittlestar_assets.py` (`--check`, `--self-test`) |
| Offline test runner | `Tools/Build/brittlestar_harness/run.sh` (`--self-test`, `--obj DIR`) |
| Art | `Tools/Build/author_brittlestar_icons.py`, `Tools/Build/render_brittlestar_card_icon.py`; concept art `Docs/Studios/BrittleStar/` |

### How the prefab was made

`author_brittlestar_assets.py` clones **Stoat.prefab** at the document level (the fleet's leanest self-owned hull:
procedural, own colliders, own HUD with petal bars, `DefaultVesselTelemetry`) and replaces everything Stoat-specific: a
fresh `GlobalObjectIdHash` for its own guid, the **Sparrow's one-thumb transformer block** on the same fileID (scripted
to `BrittleStarVesselTransformer`; cruise 35 + 10), the spawner swapped to `BrittleStarPrismController`, the hull builder
and animation swapped on the same fileIDs, the Stoat's two VesselJets removed (the hull draws its own flames), every
Stoat executor removed and `BrittleStarExecutor` + a turrets object added, its own camera (0, 0, −38), containers,
skimmer container (`affectSelf` off) and HUD. The clone is a spent one-shot: it stands down once the prefab exists and
`--check` validates the shipped invariants. Tuning assets (config, camera, AI policy) are **seeded** — written once,
then only checked for script and name — so a retune never fails the gate.

The `check_generated_assets.py` findings on this prefab (six Netcode `ShowTopMostFoldoutHeaderGroup` /
`NetworkTransformExpanded` keys, `R_VesselElementStatsHandler.cs` declaring `R_ShipElementStatsHandler`) are byte-
identical on the Stoat and Sparrow and shared by all 16 vessel prefabs — inherited, not introduced (vessel rule 42).

## Tuning knobs

| Knob | Asset | Shipped |
|---|---|---|
| `speedModeMultiplier` (Time) | `BrittleStarConfig` | 6 → 8, floor 4 |
| `speedModeSpacingFollow` / `speedModeStretch` | `BrittleStarConfig` | 1 / 1 (prisms stretch with the spacing) |
| `prismWidthScale` (Mass, × every block's x and y) | `BrittleStarConfig` | 1 → 2 |
| `creationSpacing` / `creationRadiusScale` / `creationOffset` | `BrittleStarConfig` | 6 u / 1 / 2 u |
| `creationSpeedMultiplier` (the slowest mode) | `BrittleStarConfig` | ×0.5 |
| `fireRate` (per gun, × the RT pull) / `projectileSpeed` / `projectileTime` | `BrittleStarConfig` | 90 /s / 135 u/s / 4.5 s (387 u) |
| `rangeMultiplier` (Space) | `BrittleStarConfig` | 1 → 2 |
| `projectileScale` / `projectileScaleMultiplier` (Charge) | `BrittleStarConfig` | 1 (the Sparrow's) / 1 → 1.8 |
| `roundGrowthFactor` (in-flight swell, the Sparrow's look) | `BrittleStarConfig` | ×3 |
| `armFocusDistance` / `armSplayDegrees` (the guns and cords in / out) | `BrittleStarConfig` | 120 u / 25° |
| `aimDeadzone` / `ringTurnRate` | `BrittleStarConfig` | 0.25 / 540°/s |
| `heatPerRound` / `coolRate` / `chargeCoolRate` / `overheatRecoverBelow` | `BrittleStarConfig` | 0.0012 / 0.12 /s / 0.6 /s / 0.35 |
| `heatCapacity` (Space — the heat the guns hold) | `BrittleStarConfig` | 1 → 3 (~2.4 s → ~7.1 s full-pull burst) |
| `cordLength` (the saber) / `cordLooseLengthScale` (the whip) / `cordLinks` / `cordCutRadius` (× Charge) | `BrittleStarConfig` | 80 u / ×1.5 / 16 / 0.6 u |
| `cordLooseWave` / `cordRigidWave` / `cordLooseDamping` / `cordRigidDamping` / `cordPose` / `cordRigidityRate` | `BrittleStarConfig` | 144 / 2500 /s² / 0.4 / 14 /s / 1600 /s² / 12 /s |
| `cordMaxRigidity` (how rigid a full pull makes the cords) | `BrittleStarConfig` | 0.4 (60% of the whip's give kept; 0.7 on 2026-10-16) |
| `cordDebrisRestitution` / `cordDebrisSpeedLimit` / `cordHitCooldown` | `BrittleStarConfig` | 0.5 / 200 u/s / 0.5 s |
| `cordRenderSubdivisions` / `cordColor` / `cordRimBrightness` / `cordMaterial` | `BrittleStarConfig`, executor | 4 / iron / 0.75 / `ThresherChainMaterial` |
| Round pool | `BrittleStar.prefab` turrets | 1400 / max 2400 (~1,100 alive per Space Mode burst at Space 0, ~3,200 at Space 10) |
| `ringLaunchSpeed` / `ringFuseSeconds` | `BrittleStarConfig` | 240 u/s / 1.1 s |
| `blastScale` (Charge — the disk's thickness) | `BrittleStarConfig` | 1 → 4 (× the ring's 0.22 u half-thickness) |
| `ringBlastSeconds` / `ringBlastFallbackRadius` / `ringBlastBandWidth` | `BrittleStarConfig` | 0.7 s / 200 u / 6 u (radius = the cell's nucleus) |
| `ringBlastHitClass` / `ringBlastHitCooldown` | `BrittleStarConfig` | MissileBlast / 0.5 s |
| `maxSpareRings` / `rebuildSeconds` | `BrittleStarConfig` | 2 / 3 s (regrowth) |
| AI thresholds | `BrittleStarConfig` | fire ≥ 2, Space / Charge ≥ 8, detonate ≥ 18 |
| Cruise | `BrittleStar.prefab` transformer | `DefaultThrottleScaler` 35 + `DefaultMinimumSpeed` 10 |
| Turn rates | `BrittleStar.prefab` transformer | the Sparrow's: pitch/yaw 80, roll 30 |
| Base trail | `BrittleStar.prefab` spawner | `BaseScale` (1, 1, 6), wavelength 6, offset 2 |
| Hull proportions | `BrittleStarHullBuilder` field initializers (`BrittleStarHullForm.Defaults`) | ring radius 6 (2.5× the first cut), struts 0.22, arms 0.14 |
| Flames / bank | `BrittleStarAnimation` | lit 3.2 u, ember 0.12 u, bank 28° |
| Camera | `BrittleStarCameraSettingsSO` | offset (0, 0, −38) — no height: the vessel is a crosshair — far 12000 |
| `armFocusDistance` / `armSplayDegrees` (the cords in / out) | `BrittleStarConfig` | 120 u / 25° |

## In-editor verification

Garrett played the 2026-10-16 build and reported it working; no step below has been reported individually. What has been proved offline: the player and editor reference compiles
(`unity_refcompile`), the 31 edit-mode tests run against the shipped code (`brittlestar_harness`, plus three negative
controls), the peer-press harness (0 divergences), the element table (0 disagreements), and the fleet's static gates.
None of that is a play test. In order:

1. **Freestyle**: Menu → freestyle → Vessel Changer → Brittle Star. Expect: a gold pentagon ring round a hub, domain-
   coloured nozzles and canopy, a cockpit flame; the hull leans into turns and the **outside** pods light as it banks.
   Console clean (no `[BrittleStar]` errors).
2. **Trail**: fly straight; a single SMALL block (1.5 × 1.5 × 3) every ~6 u behind the cockpit.
3. **A** (Speed): every thruster lights and the hull runs at **6×** (45 → 270 u/s); the trail's blocks spread out
   (~36 u apart, same blocks per second) and **stretch** with it (~18 u long), so the line still reads joined. **A** again:
   back to Cruise; no burst of short blocks while the hull coasts down.
4. **B** (Creation): the hull slows to **half** cruise, the cockpit's trail stops and five trails of the SAME small block
   run behind the ring's five corners. Push the right stick round: the ring turns and the five lines twist with it.
5. **RT in Cruise / Speed / Creation**: one stream from the turret the ring has turned to the aim (push the stick round to
   pick it; centre it and the turret stays), straight ahead, no spread. Half a pull fires half as fast. It never
   overheats on its own (watch the Space card's heat settle).
6. **X** (Space): RT fires all five turrets, the pull setting the rate; stick in, the five streams meet ~120 u ahead,
   out, they splay 25°. Rounds reach ~387 u (raise Space: up to ~774 u). Hold a full pull: the Space card fills with heat
   and turns red after ~2.4 s (raise Space to 10: ~7.1 s); the guns stop; **your trail turns to danger prisms** while overheated in Space Mode. Release
   and wait ~5 s: the card drops below a third and the guns fire again. Profiler with 3–4 AI Brittle Stars bursting:
   ~1,100 rounds alive per hull.
7. **RB** (Charge): five cords (fresnel tubes in your domain's colour) grow out of the turrets, **120 u long and loose**.
   Flick the right stick round: the ring snaps over and a **wave travels slowly down each cord** (about a second to the
   tip), bounces back and keeps swinging for a few seconds; turn the hull hard and the cords trail and swing. Squeeze RT
   slowly: the cords **shorten to 80 u and stiffen**, becoming five sabers at a full pull that still flex a little when
   you flick the ring (60% of the give stays); ease off, they loosen again (analog). Fly them through mass: prisms are sliced in two (friendly fire, except trail laid in the last
   second). Draw one across a rival: petals only. Overheat in Space Mode, then switch to Charge: the heat drains in about
   a second.
8. **LT** with no spare ring: nothing happens. Collect a **crystal**: the Charge card fills half (one spare); another:
   full (two); a third: still two. **LT**: the ring flies off and blows in the nucleus-wide disk (a flat slice through
   every prism in its plane; a rival it crosses loses petals); a new ring blooms back on the hull over 3 s
   and the card drops a step. The hull is never ringless; the modes drop out only for those three seconds.
9. **HUD**: four cards, charge → mass → space → time; each turns lime while its mode is on. Run **FrogletTools ▸ Vessels
   ▸ Audit Vessel Ability Rows**, **Audit Vessel Skimmers**, **Audit Vessel Elemental Morphs** (expect: procedural, four
   elements) and **Audit Vessel Construction**.
10. **Element morphs**: raise each element (crystals, or the debug element tool): Mass grows the cockpit, Charge the
   pods and guns, Space the barrels, Time rounds the ring into a circle.
11. **AI**: an arcade/arena match with the Brittle Star in an AI seat: it fires at mass ahead, takes Charge Mode's sabers
    to a near wall and Space Mode's guns to a far one, takes Speed on straights, and throws spare rings into clusters.
12. **Multiplayer (MPPM, two clients)**: one pilot shoots the other: the victim loses petals and is **not** moved. Both
    clients see the same mode, the same five corner trails and the same ring stock. Known gap: the analog RT depth does
    not replicate, so the remote copy fires (and heats) at a full pull during a partial one (§3.4).
13. Run the edit-mode suite: `BrittleStarTests`, `EnumIntegrityTests`, `OneThumbVesselCoverageTests`,
    `ToyVesselRosterCoverageTests`, `CrystalHullFusionConfigTests`.

## Follow-ups

- **F1 — touch controls for the modes.** Touch raises no face-button events, so Speed / Creation / Destruction are
  unreachable on touch (the Stoat has the same gap). The document's touch design — drag on the shoot button to aim and
  spread — needs an on-screen control.
- **F2 — level-5 upgrades** (§5): Garrett's call.
- **F3 — persona AI**: the document gives each element a persona (Fabricator builds, Rolling Thunder destroys clumps,
  Hunter chases pilots, Zephyr chases crystals into enemy zones). Today one autopilot serves all four.
- **F4 — FMOD**: twelve empty `EventReference` slots on `BrittleStarConfig` (four mode cues and mode-off, fire, cord slice,
  ring launch / blast / regrow, spare stored, overheat) — audio's to author.
- **F5 — minigames**: *Demolition Derby* (Time/Charge rewards) and *Fauna Frenzy* (Mass/Space rewards) from the
  document (`/arcadegame`).
- **F6 — Vessel Studio page: DONE** (2026-10-14). `Docs/Studios/VesselStudio/brittlestar.html` (merged from the retired
  `cece/brave-galileo-d95fpj`) flies this build by default (round 2): its `SHIPPED` block holds every number here, and
  `python3 Tools/Build/studio_to_unity.py --vessel brittlestar` writes them into `BrittleStarConfig.asset` and the prefab
  (`--check`: 35 mapped, 0 drift). Tune there, then clone with `/artifact-to-unity`. Its round-2 scorecard and findings
  (the disk breaks your own trail in the Frenzy; half-speed Creation slows it; Overdrive still out-pays the ring):
  `Docs/Studios/BRITTLE_STAR.md` §5.2. Not yet published into the one artifact (it goes through `Ys-bleeding-edge`).
- **F9 — profile the two costs** (measured offline only): each ring explosion runs ten `PrismSpatialIndex.QuerySphere`
  calls of up to the nucleus radius (392 u) (the knob: the sweep count in `BrittleStarExecutor.UpdateWaves`), and a Space
  Mode burst keeps ~1,100 swept rounds alive per hull at Space 0 and ~3,200 at Space 10, past the turrets' 2,400 pool
  (the knobs: `heatPerRound` and `heatCapacity` set the burst's length, `fireRate` thins it).
- **R1 — naming drift this branch created (inconsistency → a rename, not a redesign).** "Space Mode" is still spelled
  `Mode.Destruction` / `BrittleStarControl.DestructionMode` / `BrittleStarDestructionMode.asset` / `Destruction01`, and the
  HUD view's `ringGauge` is now the Charge card, `destructionGauge` the Space card — 27 occurrences over 9 files
  (`grep -rcE 'Mode\.Destruction|DestructionMode|destructionGauge|ringGauge|Destruction01'` under `Assets`, 2026-10-16).
  Kept so the enum values, the control asset's guid and the HUD variant's wiring did not move in the redesign. The fix:
  rename the two view fields with `[FormerlySerializedAs]`, the enum members in code (their values are static), and
  `git mv` the control asset (its guid survives); then `author_brittlestar_assets.py --check` and the HUD audit.
- **R2 — a gap this branch walked past (fleet input layer).** `InputEvents` has no fourth face-button event: the gamepad
  raises A / B / X (`Button1-3Action`) and RB (`FlipAction`) only (`GamepadInputStrategy.ProcessButtonInput`), so a
  vessel that needs a fourth mode control must borrow `FlipAction` — which `DeviceOrientationHandler` also raises when a
  phone is turned. Measure before acting: which vessels bind `FlipAction` today (`grep -l "InputEvent: 3" Assets/_Prefabs/Spacevessels/*.prefab`),
  and whether a `Button4Action` (Y / a key / an on-screen button) is wanted fleet-wide (measured 2026-10-16: the
  Brittle Star and the Grizzly bind it). Owner: the input layer.
- **F12 — the Charge card's icon** still shows Ring Detonation (`author_brittlestar_icons.py`): Charge Mode's cords need
  their own icon; the card's gauge (the spare-ring stock) already fits the ring.
- **F13 — replicate RT's depth** (two bytes a frame) so remote copies fire, heat and stiffen at the owner's real pull;
  today they read a press as a full pull (the Thresher has the same gap).
- **F10 — the studio** (`Docs/Studios/BRITTLE_STAR.md` §5.2): publish the page into the one Vessel Studio artifact
  (through `Ys-bleeding-edge`, with the owner's approval); give it a decision log; re-tune its lab-only pilot for x6
  (Hard is not the fastest level on Derby I1 / I4 and Frenzy I4).
- **F11 — `VesselClassType.Falcon = 9`**: the same hull's old name, a non-spawnable Manta copy whose `_TEMP/FalconClassSO`
  is still in `SO_Classlist_All` and `OneThumbVesselCoverageTests`' list (`Docs/claude/ABOUT_PROJECT.md`). Retire it,
  rename it, or keep it as a placeholder: Garrett's call.
- **F8 — a suspended ring**: the concept art floats the ring round the cockpit on energy; the hull joins them with five
  physical arms. Swapping them for energy links (a flame-like domain-coloured beam per arm) is a hull-builder change.
- **F7 — "Ark maximum range"** (1 cell diameter, a gentle pull back at the edge) is in the document's play-loop section and
  not built.
