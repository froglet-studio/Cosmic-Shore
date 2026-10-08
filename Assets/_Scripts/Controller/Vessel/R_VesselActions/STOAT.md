# STOAT — the slingshot vessel (prototype, 2026-10-08)

> **Naming.** Player-facing, the Stoat lays **WORMHOLES**: an **attractor** (pulls) and a
> **repulsor** (pushes), an attractor–repulsor pair. In code they are still the black-hole system's
> types (`BlackHole`, `HolePolarity.Black` = attractor, `.White` = repulsor) because the real wormhole
> mechanics are being built on `cece/charming-cerf-alf1j1` on those types (Docs/BLACK_HOLE.md, top).
> What the Stoat flies with today is the PLACEHOLDER: the pull, the lens, the pass-through.

`VesselClassType.Stoat = 14`. A two-thumb hull whose ONE ability lays an **attractor–repulsor wormhole pair**
across itself and lets gravity do the rest: the hull falls toward the attractor and is shoved off
the repulsor, and that asymmetric push is the **slingshot**. Its second input is the Sparrow's
**stop**, verbatim — because the Stoat slings from a standstill.

Status: **PROTOTYPE.** The prefab is a text clone of the Squirrel's (same flight model, colliders,
HUD and camera) with the Squirrel's bindings replaced and its model HIDDEN: the drawn hull is the
Stoat's own **procedural plated body** with the user's **bounding lope** (§2.1, §2.2), both picked in
the Stoat Flight Studio viewer (round 2, Option 2). Its game is **Slingshot** (`Arcade/SLINGSHOT.md`). **Nothing here has been
run in the editor** — every proof below is offline (§5), the same standing as `Docs/BLACK_HOLE.md`
§0.1. The black hole itself — the pull, the lens, the tides, the pair, the pass-through — is
documented there and not repeated here.

## 1. The loop

| Input | What happens |
|---|---|
| **LT press** (`InputEvents.LeftStickAction`, 2) | the squeeze begins; a sound slot (`holdStartEvent`, empty) |
| **LT held** | the trigger's depth (its hold time on keyboard / mouse / touch) is sampled every frame — that is the pair's SIZE |
| **LT release** | the pair is slung: **attractor on the LEFT, repulsor on the RIGHT**, on the hull's own horizontal, `aheadHorizons` horizon radii ahead; the previous pair this hull slung is annihilated first; if the hull was holding still (X) the stance ENDS — a sling is a launch |
| **RT** (`RightStickAction`, 1) | the mirror: attractor on the right, repulsor on the left |
| **X** (`Button1Action`, 6) | `StoatHoldAction` — `ToggleTranslationModeActionSO` in **Sparrow** mode: stop translating and stop laying prisms; press again to move |

A light tap is a **nudge** (strength `minStrength` = 2, horizon radius 4 u); a buried trigger is the
**~90° swing** (`maxStrength` = 12, horizon radius 24 u), along `hold^1.5` so the last bit of travel
counts. The pair lives `lifetime` = 4 s: the two holes drift apart at 20 u/s, stop at 2 s, fall back
and annihilate (`BlackHolePairMath`). Whatever the black hole swallows comes out of the white hole
the point-reflected way, including the hull's own prisms.

**The pull moves only THIS Stoat.** A slung pair is owned (`BlackHole.OwnerVessel`), and
`BlackHoleVesselPull` applies an owned hole only to its owner: a vessel may not move an opposing
vessel (`Docs/ELEMENTAL_ECONOMY.md` §9, LOCKED). Prisms feel every hole; tool/console holes pull
every vessel. **Why the sling ends the stop:** while `IsTranslationRestricted`, the transformer
displaces a vessel only by modifiers flagged `ignoresTranslationRestriction` (the Sparrow's dodge),
so a held-still Stoat would ignore its own pull; `ToggleTranslationModeActionExecutor.EndStance()`
(the turn-end exit, through the controller so it replicates) is called on release instead.

**It works while idle, deliberately.** The holes are laid in the hull's FRAME (position, forward,
right, up), not thrown from its velocity, so a stopped Stoat is the ideal slinger: stop (X), squeeze,
let go, get thrown.

## 2. The numbers (`StoatSlingConfigSO`, asset `_SO_Assets/VesselActions/Stoat/StoatSlingConfig.asset`)

| Field | Ships as | What it is |
|---|---|---|
| `minStrength` / `maxStrength` | 2 / 12 | black hole strength at a touch / fully buried — its horizon radius is `strength × BlackHoleConfig.horizonPerStrength` (2), its pull `strength × gmPerStrength` (2000) |
| `holdExponent` | 1.5 | `strength = lerp(min, max, hold^exponent)` |
| `holdRampSeconds` | 1.2 | seconds of hold that count as a full squeeze on a device with no analog trigger |
| `autopilotHold01` | 0.5 | the AI writes no trigger; every AI sling is this squeeze |
| `aheadHorizons` | 2 | midpoint, in horizon radii ahead of the hull |
| `halfGapHorizons` | **ElementalFloat, Space, 4 → 8, floor 1.5** | each hole's offset to the side, in horizon radii — the one element-scaled number, read live at release (`EvaluateLive`), never cached |
| `driftSpeed` / `lifetime` | 20 u/s / 4 s | the pair's drift and its life, handed to `BlackHoleRegistry.SpawnPair` |
| `holdStartEvent` / `slingEvent` | **empty** | FMOD slots; wire them in the inspector |

Pure arithmetic in `StoatSlingMath` (`Hold01`, `Strength`, `PairAxis`, `Midpoint`), held by
`StoatSlingTests`. The axis runs black → white, so the LEFT trigger's axis is the hull's **+right**:
`BlackHolePairMath.Positions` puts the black hole at −axis. Spin axis = the hull's up, so the frame
drag turns on the plane the pair lies in.

## 2.1 The hull — plated, procedural (`StoatHullForm` + `StoatHullBuilder`)

Round 2, Option 2 at fleet scale (the viewer's numbers ×1.6): five **diamond plates** (a four-sided
cross-section, point up, so the spine reads as a ridge) tapering chest 0.45 → hip 0.26, a **wedge
head**, tetrahedral ears, a **dorsal ridge** of fins on every other plate, a four-plate tail ending in
a **crystal**, four strut legs — 8.0 u nose to crystal, 0.9 u wide. Flat-shaded throughout. **Slot 1
(the domain colour) is the fins, the eyes and the tail crystal**; slot 0 is the plates.
`StoatHullForm` is pure (the Butterfly/Scarab split): `Tools/Build/stoat_hull_harness/run.sh`
compiles and runs the shipped file (topology across the four extremes, every element moves it,
blend@1 == extreme, bounds hold all 16 corners, every face wound outward, size) and `--obj <dir>`
exports the meshes. `StoatHullBuilder` sits on a `StoatHull` GameObject under the root (layer
Ships), is what `VesselCustomization` paints, and switches off the Squirrel model's RENDERERS (its
colliders stay). **Element morphs** (real geometry, `IProceduralElementMorphSource`): Charge —
fins ×2.2, crystal ×1.8, eyes ×1.5; Mass — plates and head thicken ×1.35/×1.2; Space — body ×1.3,
tail ×1.4; Time — legs ×1.8, ears ×1.6.

## 2.2 The bounding lope — body-only (`StoatLopeMath` + `StoatAnimation`)

The user's tuning: amplitude 1.00 (→ 1.6 u at the body's scale), rate 0.66 (one bound every 1.5 s),
arch 0.45, stretch 0.20, speed link 0. **Body-only**: the drawn hull rises nose-up, arches long at
the top, lands nose-down, bunched, legs reaching — while the vessel's transform, flight path and
prism trail stay straight, so aim and the wake are steady. `StoatAnimation` replaced the Squirrel
clone's `MantaAnimationContoller` on the same component (so `VesselStatus`'s reference holds); it
writes transforms only (hull lift/pitch/stretch, plate arch and tilt, head nod/turn, tail lift and
swing out of a turn, leg reach), the builder writes vertices only. The stick bends the spine into a
turn (12°), turns the head (18°) and swings the tail out (28°). Poses are written directly, not
through the base class's lerp (the Butterfly lesson: it eats a slow periodic motion).

## 3. The parts

| File | Role |
|---|---|
| `Data Containers/StoatSlingConfigSO.cs` | the numbers above |
| `Data Containers/StoatSlingActionSO.cs` | one asset per trigger (`side`), shared and stateless: press → `BeginHold`, release → `Release` |
| `Executors/StoatSlingExecutor.cs` | the per-vessel state: the two squeezes, the last pair; samples the hold every frame; calls `BlackHoleRegistry.Annihilate` / `SpawnPair` |
| `StoatSlingMath.cs` | the pure maths |
| `_SO_Assets/VesselActions/Stoat/` | `StoatSlingConfig`, `StoatSlingLeftAction` (side 0), `StoatSlingRightAction` (side 1), `StoatHoldAction` (stationaryMode 1 = Sparrow) |
| `Resources/ElementalAbilityMaps/Stoat.asset` | Space = Slingshot (Input 2), Time = Hold Still (Input 6), Charge and Mass open — see §6 |
| `_Prefabs/Spacevessels/Stoat.prefab` | authored by `Tools/Build/author_stoat_assets.py` from `Squirrel.prefab` (§4) |
| `StoatHullForm.cs`, `StoatHullBuilder.cs` (Vessel/) | the plated hull: pure geometry + morphs, and the emitter (§2.1) |
| `StoatLopeMath.cs`, `StoatAnimation.cs` (Animation/) | the lope (pure) and the puppetry + morph plumbing (§2.2) |
| `Tools/Build/stoat_hull_harness/` | compiles and RUNS the shipped hull form offline; `--obj` exports it |
| `Tools/Build/author_stoat_assets.py` | the generator + `--check` + `--self-test` (ten negative controls); stage 2 authors the hull and the animation swap |

**Why the hold is sampled per frame, not read at release.** The release edge is raised when the
trigger drops back below the deadzone, so it reads ~0 by the time `Release` runs; sampling it there
would sling every pair at minimum size. The Gibbon's lesson, inherited.

**One pair per Stoat.** A new sling annihilates THIS hull's previous pair first (never the tool's or
another hull's), so a pilot can chain nudges without exhausting `BlackHoleConfig.maxBlackHoles` (4 —
two pairs fleet-wide). A refused sling (no room) is a verbose log on the `BlackHole` channel, not an
error.

## 4. The prefab — what the generator changed and what it kept

`Stoat.prefab` is `Squirrel.prefab` with exactly these edits (the whole diff is the generator's
`build_clone`; `--check` re-asserts every one of them on the SHIPPED file):

- root GameObject `m_Name` and `VesselStatus._name` → Stoat; `vesselType: 14` (the prefab's ADDRESS
  in the container — `check_vessel_prefab_container.py`).
- **A fresh Netcode identity.** A disk copy keeps the donor's `GlobalObjectIdHash`, and Netcode keys
  its prefab table on that number alone, so the Squirrel or the Stoat would silently never spawn
  (`Tools/Build/check_network_prefab_hashes.py`, ported from the Gibbon branch, which hit this). The
  generator computes the hash the way `NetworkObject.OnValidate` does — XXHash32 of
  `GlobalObjectId_V1-1-<guid>-<fileID>-0` — and proves it by reproducing the Squirrel's own
  `2256742461` from its guid; the editor regenerates the same number, so opening the prefab changes
  nothing. `InScenePlacedSourceGlobalObjectIdHash` is 0.
- Gamepad bindings: 2 → `StoatSlingLeftAction`, 1 → `StoatSlingRightAction`, 6 → `StoatHoldAction`.
  Keyboard, dual-mouse and mouse-keyboard share the gamepad map (`R_VesselActionHandler`: Left/Right
  Shift are the triggers). **Touch overrides are cleared** — there is no touch design yet, so a touch
  Stoat has no abilities (the Gibbon's call, same reason).
- The Squirrel's `DriftActionSO`s are NOT bound, so the transformer's `singleTriggerDrift: 1` is
  inert: `ApplyAnalogDrift` acts only after a drift action's `BeginDrift`, which nothing calls.
- `StoatSlingExecutor` (config wired) and `ToggleTranslationModeActionExecutor` (the hull's own
  `VesselPrismController`, no seed assembler, the fleet's `stationaryModeChanged` and
  `OnMiniGameTurnEnd` channels — the guids the Sparrow carries, `check_vessel_shared_channels.py`)
  added on the `ShipActions` object and in `ActionExecutorRegistry._executors`.
- Registered in `Vessel Prefab Container.asset` and `DefaultNetworkPrefabs.asset`;
  `ToyVesselRoster.Default` lists it; `EnumIntegrityTests` locks 14 (and the count, which had not
  been bumped for the Butterfly).

Kept, deliberately, as prototype debt: the Squirrel's model (hidden — its renderers off, its colliders
live, so the Stoat's hit box is still the Squirrel's ~4 u box, not the 8 u body), HUD variant and four ability ICONS (the
row will show the Squirrel's art until `author_hull_ability_rows.py` / the icon pass runs for the
Stoat), its two executors (`SquirrelTubeActionExecutor` and the seed executor — unbound, inert), its
camera settings SO (far clip 12000 — `check_vessel_camera_farclip.py`), its telemetry (the default,
`VesselTelemetryBootstrapper` has no Stoat case). **The clone is a spent one-shot once committed**:
`author_stoat_assets.py` clones only while `Stoat.prefab` is absent and otherwise stands down and
validates the shipped file, so Squirrel drift can never silently re-author the Stoat.

## 5. Proof (all offline — no `/verify-unity` in the session that built this)

- `Tools/Build/author_stoat_assets.py --check` OK; `--self-test`: XXHash32 against the reference
  vectors and the Squirrel's own hash, then seven negative controls (wrong class, donor hash kept,
  executor dropped, trigger unbound, container / network entry missing, map lost the hold), each firing.
- `StoatSlingTests` (edit mode, written, not run): the squeeze on a gamepad / without analog
  triggers / on autopilot, strength along the exponent, black on the pressed side, midpoint ahead,
  the enum and roster, and the prefab / map / spawn lists read as text.
- Gates: `check_vessel_prefab_container` (11 hulls distinct), `check_vessel_shared_channels`,
  `check_vessel_camera_farclip`, `check_network_prefab_hashes` (0 collisions),
  `check_elemental_floats --check` (the half-gap is read through `EvaluateLive`),
  `check_console_logging`, `check_conditional_compilation`, `check_abstract_member_implementations`,
  `peer_press_harness --self-test`, and the offline refcompile for player, player-dev and editor.
- `check_generated_assets.py` reports **152 findings on `Stoat.prefab` — every one inherited verbatim
  from `Squirrel.prefab`, 0 new, 0 gone** (the audit was run on both files with the names normalised
  and the finding sets are identical): stale nested-skimmer overrides Unity never prunes
  (`notifyNearbyBlockCount`, `shipImpactEffects`, `skimmerPrismStayEffectsSO`…), HUD-variant
  modification targets that no longer exist, and `R_VesselElementStatsHandler.cs` declaring
  `R_ShipElementStatsHandler` (the fleet-wide file/class rename, vessel skill §2.5). None is the
  Stoat's; cleaning them is a Squirrel pass, and the Stoat would inherit the fix by re-authoring.

## 6. Design status — what is approved and what is not

The user's brief (2026-10-08) fixed: the name, one Space ability that spawns the black–white pair on
the trigger's side with hold = size, a Sparrow-style stop, and the pair's drift-and-annihilate life.
The element PLACEMENT of the stop (Time — rate/mobility) and the Space-scaled number (the half-gap —
reach) are this prototype's choices and are recorded in the map as such; Charge and Mass are
**open design slots**, with the proposals (Charge → the slung strength; Mass → something the squeeze
does not already own) written into `Stoat.asset` and not implemented. No level-5 upgrade exists for
either authored ability — the row reports them LOCKED rather than green, by design.

## 7. Not yet

The ability row's icons and hints; the pair's
network replication (the press/release edges round-trip, so a peer lays its OWN copy of the pair at
the replicated pose, but the two copies are not one object); the annihilation / birth feel
(`Docs/BLACK_HOLE.md` §11 "Not yet"); a touch binding; the hangar / arcade `SO_Class_Stoat` asset and class-list entry
(CONTRACT.md §1.9 — the prototype is flown from the lava-lamp Vessel Changer, which reads
`ToyVesselRoster.Default`); an AI that chooses to sling (the executor
answers the autopilot's squeeze, nothing presses for it); the "everything destructible" extension of
the black hole itself.
