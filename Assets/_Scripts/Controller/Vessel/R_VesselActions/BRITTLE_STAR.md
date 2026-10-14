# Brittle Star — the five-pointed one-touch flyer

**Status:** built 2026-10-14 from the Notion design document *Class - Brittle Star* (Design Documents ▸ Class Design
Documents ▸ Sparrow Chain). `VesselClassType.BrittleStar = 16`. Compiled and statically gated offline; **not yet opened
in Unity** — see [In-editor verification](#in-editor-verification).

> *"A complex one-touch flyer. With different modes concentrated on destruction, creation, and maneuverability, the
> Brittle Star is an all-in-one tutorial on the more advanced systems of the game."* Superlative: **best at short-range
> barrages for pinpoint destruction with minimal investment.**

## 1. What it is

A cockpit hub inside a **pentagon support ring**, joined by five thin arms (the animal's). Every point of the pentagon
carries an **engine pod** at the back and a **gun** at the front; the cockpit carries a sixth gun on its nose. It flies on
the fleet's one-thumb model (`SingleStickVesselTransformer`, via `BrittleStarVesselTransformer`).

### Controls

| Control | Gamepad | Keyboard / one-thumb mouse | Does |
|---|---|---|---|
| Speed Mode | **A** | Space | Select Speed Mode (press again: back to Cruise) |
| Creation Mode | **B** | R | Select Creation Mode (press again: Cruise) |
| Destruction Mode | **X** | Q / middle mouse | Select Destruction Mode (press again: Cruise) |
| Fire (held) | **RT** | Right Shift / LMB | Shoot one stream (outside Destruction Mode, which fires on its own) |
| Aim / spread | **right stick** | P ; L ' (no aim on the mouse scheme) | Direction turns the ring (picks RT's turret; in Destruction Mode turns the five arms); deflection opens RT's spread, or moves the arms in and out |
| Ring Detonation | **LT** | Left Shift / RMB | Throw the ring |

Bindings are in the **shared** list (`_inputEventShipActions`), the Sparrow's shape, so touch reaches Fire and Ring
Detonation through its own trigger gestures. Touch has **no** face buttons, so the three modes are not yet reachable on
touch (follow-up F1).

Modes are **exclusive**: one at a time, or **Cruise** (none). All three need the support ring.

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
  thrusters should engage at once"); Overdrive lengthens every flame. The ring turns with the **firing rotation**, and is
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
  block's Time-scaled `BoostSpeedMultiplier` is **off** on this hull — there is no boost, and a second Time dial would
  be undocumented.
- "Speed mode increases the wavelength of the Brittle Star's volume production so as not to increase production output":
  `BrittleStarPrismController.SpacingMultiplier` stretches the base trail's delay by the same factor
  (`speedModeSpacingFollow` = 1), so prisms per second hold — at ×6 the blocks are ~36 u apart. The corner trails'
  spacing stretches too. The spacing follows the hull's **actual** speed over its plain cruise, not the mode's target:
  the hull eases out of ×6 over a second or so, and a spacing keyed to the target (×1 the instant the mode changed)
  laid several times too many prisms while it coasted down — the "momentum hole" the design studio's scorecard found.
- **Speed is the fastest mode; Creation the slowest** (Garrett, 2026-10-14): `BrittleStarRules.ModeSpeed` (tested)
  multiplies the two blends, so Overdrive (all modes on) flies ×6 × ×0.5 = ×3. Destruction Mode flies at cruise.

### 3.2 Creation Mode — MASS
- **A trail behind each ring corner** (Garrett, 2026-10-14; the first cut laid a pentagon of slabs): five lines, one per
  corner, each its own `Trail`, laid `creationOffset` 2 u behind the corner every `creationSpacing` 6 u of path (the
  base trail's wavelength), each block's length along the path and its broad side tangent to the ring. They turn with
  the ring's firing rotation, so aiming while building twists the five lines round each other. Blocks are
  `creationBlockSize` (2.5 × 2.5 × 4.5) × **`creationBlockScale` ×1 → ×1.8 at Mass 10** (`EvaluateReplicated`: every
  peer lays the same size). At most 3 blocks per corner a frame, spread back along the path, excess dropped (rule 17).
- **The slowest mode** (Garrett, 2026-10-14): cruise × `creationSpeedMultiplier` **×0.5** (45 → 22.5 u/s). The corner
  trails keep their 6 u wavelength, so building is slow, deliberate work and production per second falls with the speed.
- While Creation is on, the cockpit's own trail is replaced: the base spawner's pen is held up (`SetSpawnerPaused`,
  written on EDGES only, rule 48). The corners lay only while the spawner is running
  (`VesselPrismController.SpawnerEnabled`, a read-only getter added for this), above 3 u/s and not attached — the fleet
  spawner's own conditions.

### 3.3 The base trail
- "A single small block when not in creation mode": `BaseScale (2.5, 2.5, 4.5)`, wavelength 6, `prismType` Sparrow,
  laid 2 u behind the cockpit, skimmer clearance on.

### 3.4 Destruction Mode (the five arms) and shooting — SPACE
- **Destruction Mode fires on its own: the FIVE ARMS** (Garrett, 2026-10-14). Every turret fires, unspread, at the
  Sparrow's full-auto cadence (`destructionFireRate` 90 rounds/s each, `destructionProjectileSpeed` 1350 u/s, the
  Sparrow's tracer at `destructionProjectileScale` 1) — "such a steady stream that they give the appearance of five arms,
  hence the name brittle star". Spacing between rounds is speed / rate = 15 u, under the tracer's own ~20 u length, so
  each arm reads as solid. The arms run while the mode is on and the trail is running (the turn's clock), and RT adds
  nothing to them.
- **The right stick moves the arms in and out** (Garrett, 2026-10-14; the class document's "drag away from the centre to
  increase spread"): with the stick IN (centred) every arm angles inward to cross the nose axis `armFocusDistance` 120 u
  ahead — the five arms close on one point, the whole vessel a crosshair — and pushed OUT to the rim each splays
  `armSplayDegrees` 25° outward; between, the angle moves linearly with the stick's deflection
  (`BrittleStarRules.ArmDirection`, tested). The stick's **direction** still turns the ring (Garrett, 2026-10-14: "the guns
  lost their rotation control when in Space mode"), and the five arms turn with it — direction rotates, deflection opens.
  On the one-thumb mouse
  scheme there is no right stick, so the arms stay focused; on one-thumb touch the steering thumb is mirrored onto the
  right stick, so hard turns open the arms. Fire is owed in seconds and paid in whole volleys, capped at 4 per frame (rule 17).
- **Fire (RT, held) outside Destruction Mode**: one stream at `fireRate` 12 rounds/s, 700 u/s, from the turret the right
  stick points at — the ring turns (`ringTurnRate` 540°/s, always the short way round, never more than 36°) to bring the
  nearest point onto the aim — or from the **cockpit gun** with the stick inside `aimDeadzone`. Ringless, only the
  cockpit gun is left. The stick's deflection past the deadzone opens a cone up to `maxSpreadDegrees` 16°; each round's
  deflection is a pure hash of a per-shot serial (rule 18), so peers that agree on the shot count agree on every round.
- Rounds are the Sparrow's pooled `SparrowProjectile` (friendly fire on — "players need to be careful not to destroy
  allied blocks" — swept prism and vessel hits, the fleet's scored `VesselCombatHitByBullet`), fired by a `Gun` on the
  `BrittleStarTurrets` object, whose pool is sized for the arms (240 / 640: up to 216 rounds alive at Space 10).
- Flight `projectileTime` 0.3 s × **`rangeMultiplier` ×1 → ×1.6 at Space 10**, so the arms reach 1350 × 2T/π =
  **258 u → 413 u** and RT's stream 134 u → 214 u (rule 34: rounds decelerate). Every round's size ×
  `projectileScaleMultiplier` (Charge, below).
- **Cost to watch:** the arms are 450 rounds/s per hull (the Sparrow's held trigger is 180), each a swept prism query.
  `destructionFireRate` is the knob if a match with several Brittle Stars profiles hot; below ~68/s the 15 u spacing
  opens past the tracer's length and the arms start to read as dashes.

### 3.5 Ring Detonation — CHARGE
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
- **The ring's life** (`BrittleStarRules.RingState`, pure and tested clause by clause):
  - a crystal adds **one** charge; only one can be held;
  - a crystal with a charge already stored is **Overdrive** — all three modes on for `overdriveSeconds` 6;
  - throwing the ring with a charge stored **rebuilds it at once** (`rebuildSeconds` 0.5, the ring blooming back) —
    two rings can go in rapid succession; throwing it without one leaves the hull **ringless and modeless** until a
    crystal builds the next;
  - no cooldown: detonation only needs a ring.
- Crystals: `BrittleStarRingChargeByCrystalEffectSO`, wired into **every** crystal list of
  `BrittleStarImpactorDataContainer` (omni and the four elements — the document says "a crystal"). Both dispatches run on
  every peer, so every copy of the ring and its charge agree.
- **Charge is threat — one idea, two halves** (Rolling Thunder: "larger projectile size and larger AOE on support ring
  detonation"): `blastScale` (the disk's thickness) ×1 → ×4 and `projectileScaleMultiplier` ×1 → ×1.8.

### 3.6 PvP is petals only — how the document's vessel effects were carried (LOCKED, 2026-10-10)

The class document predates the rule *"no other pvp in the game … we should only be affecting petals"*
(`Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`; `Docs/ELEMENTAL_ECONOMY.md` §9, enforced by
`Tools/Build/check_vessel_on_vessel_motion.py`). So:

| Document | Shipped |
|---|---|
| Shots "knock back enemy vessels, reducing their momentum", stacking per hit | Every round on an enemy hull is a **scored bullet hit** (petals ejected, 1 point). Nothing moves or slows the hull. |
| Destruction Mode hits "disable enemy vessel mass production temporarily" | Retired (CONTROL tier). Destruction Mode's five unbroken arms take petals five times as fast (`VesselCombatHitByBullet` still admits one hit per victim per 0.05 s — at most 20 hits, two petals, a second — however many rounds land). |
| Ring blast: knocked back, momentum reduced, trail disabled | The ring explosion's scored **MissileBlast** hit, once per hull per explosion, through `CombatHitDrain.TryAdmit` (a warded pilot is neither scored on nor robbed). |
| Space (Hunter): "increased shot range and **nudge intensity**" | Range only — the nudge went with knockback. |
| Friendly fire "accidentally disable allied mass production" | Friendly fire stays on **mass** (rounds and blasts break allied prisms); nothing touches an allied hull. |

## 4. HUD (`BrittleStarHUDController` / `BrittleStarHUDView`, `BrittleStarHUDVariant.prefab`)

The four-icon row, charge → mass → space → time: **Ring Detonation · Creation · Destruction · Speed**. One gauge per card:
the **Ring** card is full while mounted, fills while rebuilt, is empty while ringless, and turns the CTA lime while a
crystal charge is stored; each **mode** card is that mode's eased switch (0 off, 1 on), all three lime in Overdrive. The
controller polls this vessel's own executor (a serialized reference, rule 14) behind the local-pilot gate. The variant is
a copy of `StoatHUDVariant` (same internal fileIDs, so the vessel's nesting block transfers), with the authored petal bars.

## 5. The elemental map and level 5 (`Resources/ElementalAbilityMaps/BrittleStar.asset`)

| Element | Ability | Input | The one dial | L5 |
|---|---|---|---|---|
| Charge | Ring Detonation | LT (2) | disk thickness ×1 → ×4, rounds ×1 → ×1.8 | **open** |
| Mass | Creation Mode | B (7) | corner-trail blocks ×1 → ×1.8 (the mode flies ×0.5) | **open** |
| Space | Destruction Mode (the five arms; + Fire on RT) | X (8) | range ×1 → ×1.6 | **open** |
| Time | Speed Mode | A (6) | speed ×6 → ×8 | **open** |

`element_ability_table.py BrittleStar`: abilities 4/4, scaling 4/4, L5 gates 0/4, **0 disagreements**.

**The document names no level-5 upgrades, and the vessel skill's design gate forbids inventing one**, so all four are
open slots (empty `UpgradeLabel`). Proposals for Garrett to mark up (none implemented):

| Element | Proposal A | Proposal B |
|---|---|---|
| Charge | **Chain Reaction** — the disk's edge sets off a second, smaller ring explosion where it ends | **Domain-Safe Ring** — the disk spares your own domain's mass (the Sparrow's Charge 5 shape) |
| Mass | **Shielded Corners** — the corner trails arrive shielded (the Stoat / Thresher Mass 5 shape) | **Webbing** — every fifth block bridges to the next corner, stitching the five lines into a tube |
| Space | **Piercing Arms** — the arms' rounds pass through the first prism (the Sparrow's Space 5, `stopOnFirstPrismImpact`) | **Long Arms** — the arms' range ×1.25 |
| Time | **Afterburner Ring** — a thrown ring inherits Speed Mode's speed and fuses later | **Quick Build** — the rebuild is instant |

## 6. AI (input-only)

`AIPilot.abilities` is empty: the executor's autopilot presses the **same replicated controls** a human presses
(`PerformShipControllerActionsReplicated`), on the simulating machine only. It holds the trigger while ≥ 2 rival prisms
or a rival hull lie on the line ahead (min 0.4 s hold / release), switches to **Destruction** in front of a dense wall
(≥ 8), throws the ring into a big cluster (≥ 18) or whenever a stored charge makes it free, and otherwise sits in
**Creation**. **Speed** is decided by `BrittleStarSpeedModeAIPolicySO` — the fleet's boost law (a straight at the
objective with runway, let go before the turn) — which states a wish the executor acts on. Persona-specific AI (the
document's four element personas) is follow-up F3.

## Files

| Role | Path |
|---|---|
| Pure hull geometry + morphs | `_Scripts/Controller/Vessel/BrittleStarHullForm.cs` |
| Hull emitter (meshes, muzzles, materials) | `_Scripts/Controller/Vessel/BrittleStarHullBuilder.cs` |
| Pure rules (ring life, modes, mode speed, aim, spread, arms, the disk, range) | `_Scripts/Controller/Vessel/BrittleStarRules.cs` |
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
| Tests | `_Scripts/Tests/Editor/BrittleStarTests.cs` (24) |
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
| `speedModeSpacingFollow` | `BrittleStarConfig` | 1 |
| `creationBlockSize` / `creationBlockScale` (Mass) | `BrittleStarConfig` | (2.5, 2.5, 4.5) / 1 → 1.8 |
| `creationSpacing` / `creationRadiusScale` / `creationOffset` | `BrittleStarConfig` | 6 u / 1 / 2 u |
| `fireRate` / `projectileSpeed` / `projectileTime` | `BrittleStarConfig` | 12 /s / 700 / 0.3 s |
| `rangeMultiplier` (Space) | `BrittleStarConfig` | 1 → 1.6 |
| `projectileScale` / `projectileScaleMultiplier` (Charge) | `BrittleStarConfig` | 1.6 / 1 → 1.8 |
| `destructionFireRate` / `destructionProjectileSpeed` / `destructionProjectileScale` (the arms) | `BrittleStarConfig` | 90 /s each / 1350 / 1 |
| Round pool | `BrittleStar.prefab` turrets | 240 / max 640 |
| `maxSpreadDegrees` / `aimDeadzone` / `ringTurnRate` | `BrittleStarConfig` | 16° / 0.25 / 540°/s |
| `ringLaunchSpeed` / `ringFuseSeconds` | `BrittleStarConfig` | 240 u/s / 1.1 s |
| `blastScale` (Charge — the disk's thickness) | `BrittleStarConfig` | 1 → 4 (× the ring's 0.22 u half-thickness) |
| `ringBlastSeconds` / `ringBlastFallbackRadius` / `ringBlastBandWidth` | `BrittleStarConfig` | 0.7 s / 200 u / 6 u (radius = the cell's nucleus) |
| `ringBlastHitClass` / `ringBlastHitCooldown` | `BrittleStarConfig` | MissileBlast / 0.5 s |
| `creationSpeedMultiplier` (the slowest mode) | `BrittleStarConfig` | ×0.5 |
| `rebuildSeconds` / `overdriveSeconds` | `BrittleStarConfig` | 0.5 s / 6 s |
| AI thresholds | `BrittleStarConfig` | fire ≥ 2, destruction ≥ 8, detonate ≥ 18 |
| Cruise | `BrittleStar.prefab` transformer | `DefaultThrottleScaler` 35 + `DefaultMinimumSpeed` 10 |
| Turn rates | `BrittleStar.prefab` transformer | the Sparrow's: pitch/yaw 80, roll 30 |
| Base trail | `BrittleStar.prefab` spawner | `BaseScale` (2.5, 2.5, 4.5), wavelength 6, offset 2 |
| Hull proportions | `BrittleStarHullBuilder` field initializers (`BrittleStarHullForm.Defaults`) | ring radius 6 (2.5× the first cut), struts 0.22, arms 0.14 |
| Flames / bank | `BrittleStarAnimation` | lit 3.2 u, ember 0.12 u, bank 28° |
| Camera | `BrittleStarCameraSettingsSO` | offset (0, 0, −38) — no height: the vessel is a crosshair — far 12000 |
| `armFocusDistance` / `armSplayDegrees` (the arms in / out) | `BrittleStarConfig` | 120 u / 25° |

## In-editor verification

Nothing here has been opened in Unity. What has been proved offline: the player and editor reference compiles
(`unity_refcompile`), the 24 edit-mode tests run against the shipped code (`brittlestar_harness`, plus two negative
controls), the peer-press harness (0 divergences), the element table (0 disagreements), and the fleet's static gates.
None of that is a play test. In order:

1. **Freestyle**: Menu → freestyle → Vessel Changer → Brittle Star. Expect: a gold pentagon ring round a hub, domain-
   coloured nozzles and canopy, a cockpit flame; the hull leans into turns and the **outside** pods light as it banks.
   Console clean (no `[BrittleStar]` errors).
2. **Trail**: fly straight; a single small block every ~6 u behind the cockpit.
3. **A** (Speed): every thruster lights and the hull runs at **6×** (45 → 270 u/s); the trail's blocks spread out
   (~36 u apart, same blocks per second). **A** again: back to Cruise.
4. **B** (Creation): the hull slows to **half** cruise (the slowest mode), the cockpit's trail stops and five trails run
   behind the ring's five corners. Push the right stick round: the ring turns and the five lines twist with it. Go
   straight from Speed to Creation: no burst of blocks while the hull coasts down.
5. **X** (Destruction): with no button held, five unbroken streams pour out of the five turrets — five arms, closing on
   one point ~120 u ahead. Push the right stick out: they open to a 25° splay; let it back in: they close again. Push it
   round: the ring — and the five arms with it — turns to follow. They should
   read as solid lines, not dashes. Fly through mass: the arms cut it, your own included (friendly fire). **X** again:
   they stop. **RT** outside it: one stream from the cockpit gun; with the right stick pushed, from the aimed turret;
   full deflection opens a cone.
6. **LT**: the ring flies off and blows on the first mass it touches — a thin band in your domain's colour races out
   across the ring's plane to the width of the nucleus (~780 u) in 0.7 s, cutting a flat slice through every prism it
   passes and nothing above or below it; a rival it crosses loses petals and is not moved. The hull is now **ringless**: A/B/X do nothing, RT fires only the cockpit gun, the Ring card is empty.
7. Collect a **crystal**: a new ring blooms out; collect another: the Ring card turns lime (charge stored). LT twice
   quickly: two rings. Collect two more: **Overdrive** — every mode on for 6 s, all three mode cards lime.
8. **HUD**: four cards in the row, charge → mass → space → time, with the Brittle Star's four icons; run
   **FrogletTools ▸ Vessels ▸ Audit Vessel Ability Rows**, **Audit Vessel Skimmers**, **Audit Vessel Elemental Morphs**
   (expect: procedural, four elements) and **Audit Vessel Construction**.
9. **Element morphs**: raise each element (crystals, or the debug element tool): Mass grows the cockpit, Charge the
   pods and guns, Space the barrels, Time rounds the ring into a circle.
10. **AI**: an arcade/arena match with the Brittle Star in an AI seat: it fires at mass ahead, switches modes, takes
    Speed on straights, and throws its ring into clusters.
11. **Multiplayer (MPPM, two clients)**: one pilot shoots the other: the victim loses petals and is **not** moved. Both
    clients see the same mode, the same five corner trails and the same ring rebuilds.
12. Run the edit-mode suite: `BrittleStarTests`, `EnumIntegrityTests`, `OneThumbVesselCoverageTests`,
    `ToyVesselRosterCoverageTests`, `CrystalHullFusionConfigTests`.

## Follow-ups

- **F1 — touch controls for the modes.** Touch raises no face-button events, so Speed / Creation / Destruction are
  unreachable on touch (the Stoat has the same gap). The document's touch design — drag on the shoot button to aim and
  spread — needs an on-screen control.
- **F2 — level-5 upgrades** (§5): Garrett's call.
- **F3 — persona AI**: the document gives each element a persona (Fabricator builds, Rolling Thunder destroys clumps,
  Hunter chases pilots, Zephyr chases crystals into enemy zones). Today one autopilot serves all four.
- **F4 — FMOD**: ten empty `EventReference` slots on `BrittleStarConfig` (mode cues, fire, ring launch / blast / rebuild,
  charge stored, Overdrive) — audio's to author.
- **F5 — minigames**: *Demolition Derby* (Time/Charge rewards) and *Fauna Frenzy* (Mass/Space rewards) from the
  document (`/arcadegame`).
- **F6 — Vessel Studio page**: a design studio was drafted on the retired branch `cece/brave-galileo-d95fpj`
  (`Docs/Studios/VesselStudio/brittlestar.html`), but it models the class document as written, not Garrett's decisions
  since (×6 Speed, the five arms in/out, corner trails, the 2.5× ring, the nucleus-wide disk, Creation the slowest). It
  needs that pass before it can be the source of truth (`/vessel-studio` §2); until then the numbers are seeded from the C#.
- **F8 — a suspended ring**: the concept art floats the ring round the cockpit on energy; the hull joins them with five
  physical arms. Swapping them for energy links (a flame-like domain-coloured beam per arm) is a hull-builder change.
- **F7 — "Ark maximum range"** (1 cell diameter, a gentle pull back at the edge) is in the document's play-loop section and
  not built.
