# STOAT — the slingshot vessel (prototype, 2026-10-08)

> **SUPERSEDED ON THE TRIGGERS, 2026-10-09.** The Stoat now flies the round-15 **field dipole** and
> **pathfinder** — `STOAT_DIPOLE.md`. LT/RT are bound to `StoatDipoleLeft/RightAction`; the orbit sling
> below keeps its code, assets and `StoatSlingExecutor` on the prefab, unbound and inert, so rebinding
> `StoatSlingLeft/RightAction` restores it. The hull, the stop on X, the class and the registration
> described here are unchanged.

> **Naming.** Player-facing, the Stoat lays **WORMHOLES**: an **attractor** (pulls) and a
> **repulsor** (pushes), an attractor–repulsor pair. In code they are still the black-hole system's
> types (`BlackHole`, `HolePolarity.Sink` = attractor, `.Source` = repulsor — the names `cece/charming-cerf-alf1j1`
> chose, adopted when that branch was merged here). The real wormhole mechanics are built there on those
> types; both pair styles now live side by side here, switched by `BlackHoleConfig.crystalPairs` (Docs/BLACK_HOLE.md §13 —
> the Black Hole tool's Pair style button, or `blackhole style drift|crystal`).
> What the Stoat flies with today is the PLACEHOLDER: the pull, the lens, the pass-through.

`VesselClassType.Stoat = 14`. A two-thumb hull whose ONE ability lays an **attractor–repulsor wormhole pair**
across itself and lets gravity do the rest: the hull falls toward the attractor and is shoved off
the repulsor, and that asymmetric push is the **slingshot**. Its second input is the Sparrow's
**stop**, verbatim — because the Stoat slings from a standstill.

Status: **PROTOTYPE.** The prefab is a text clone of the Squirrel's (same flight model, colliders,
HUD and camera) with the Squirrel's bindings replaced and its model HIDDEN: the drawn hull is the
Stoat's own **procedural plated body** with the user's **bounding lope** (§2.1, §2.2), both picked in
the Stoat Flight Studio viewer (round 2, Option 2). Its game is **Slingshot** (`GameModes.Slingshot = 64`, `Arcade/SLINGSHOT.md`): a two-lap circuit
race where the sling is the only speed past cruise. **Nothing here has been
run in the editor** — every proof below is offline (§5), the same standing as `Docs/BLACK_HOLE.md`
§0.1. The black hole itself — the pull, the lens, the tides, the pair, the pass-through — is
documented there and not repeated here.

## 1. The loop — orbit and slingshot (the playtest's design, 2026-10-09)

| Input | What happens |
|---|---|
| **LT press** (`InputEvents.LeftStickAction`, 2) | the pair is LAID: the **attractor to the LEFT**, perpendicular to the nose at the orbit radius the squeeze asks for; the **repulsor** mirrored to the right, same distance, same size; this hull's previous pair ends first; if the hull was holding still (X) the stance ENDS — a sling is a launch |
| **LT held** | the hull **ORBITS** the attractor. The squeeze (its hold time on keyboard, over `holdRampSeconds`) sets the radius LIVE: a touch circles wide (`orbitRadiusWide`, 150 u), buried circles tight (`orbitRadiusTight`, 40 u). Keep squeezing and you keep circling, up to `lifetime` (12 s) |
| **LT release** | the **SLINGSHOT**: out along the tangent with a boost (`slingBoostMin`→`Max` × orbit speed by the deepest squeeze, 0.25 → 0.9); the pair is let go — the two holes fall together (accelerating to `driftSpeed`, 40 u/s) and annihilate where their horizons touch |
| **RT** (`RightStickAction`, 1) | the mirror: attractor on the right; pressing the other trigger mid-orbit slings out of the first and into the second — chained turns |
| **X** (`Button1Action`, 6) | `StoatHoldAction` — `ToggleTranslationModeActionSO` in **Sparrow** mode: stop translating and stop laying prisms; press again to move |

**The physics is real, held to a circle.** The attractor's horizon is `1/orbitHorizons` (1/6) of the
radius — never inside 3, the Paczyński–Wiita last stable orbit — and its strength is CHOSEN, not
authored: the one that makes the radius a circular orbit at the hull's speed,
`GM = v²(r − r_s)²/r` (`StoatSlingMath.CircularOrbitGM`). So the turn is exactly what that hole's
gravity would do to a hull at that speed, and prisms feel the same mass. Because r/r_s is fixed, the
hole looks the same size from the hull whatever the squeeze: a tighter turn is a smaller hole,
nearer. The repulsor is retuned with it ("same size and distance").

**How it flies** (`StoatSlingExecutor.StepOrbit`, simulating machine only): each frame the heading
turns along the orbit at ω = v/r (`VesselTransformer.ApplyRotation`, momentum re-aimed with
`SetCourseVelocity`), the hull is eased onto the radius (`TranslateShip`, `radialCorrectionRate`), and
both holes follow the radius (`radiusFollowRate`). An offline simulation of exactly that step held the
radius to within 0.2 u at 60 fps and released on the tangent; in the editor the transformer's own
easing toward its commanded heading is the thing to watch.

**Nobody else is moved.** An owned drift pair is skipped by `BlackHoleVesselPull` for every vessel:
its owner's pull IS the orbit, and a vessel may not move an opposing vessel
(`Docs/ELEMENTAL_ECONOMY.md` §9, LOCKED). Prisms feel every hole. **Anything that flies in comes
out:** a vessel — this one after its slingshot, an opponent or an AI by its own flying — whose centre
crosses a paired black hole's horizon is carried to the point reflection just outside the white
horizon, heading kept (outward there), through `VesselTransformer.SetPose` (trail and camera
carried, a gate watcher sees a teleport) — `BlackHoleVesselPull.TryCarryThrough` — and, once the pair
is let go, keeps the closing white hole's velocity until the pair meets, so the mouth cannot run it back
down. Prisms the same
way (`BlackHoleGravityField`). The Stoat's drawn hull **spaghettifies** near a horizon — stretched
along its length falling in, and the same stretch relaxing as it leaves the white hole (tides are
even under time reversal): `BlackHoleWarp.VesselLogStretch` × `BlackHoleConfig.vesselTideScale`
(0.08), applied by `StoatAnimation` on top of the lope. Other hulls are carried but not yet drawn
stretched (their hull transform is no single component's to scale).

**The autopilot slings** (`AutopilotSling`, simulating machine only): when the AI's target is at least
`aiSlingMinTurnDegrees` (30°) off the nose and `aiSlingMinDistance` (120 u) away, at most every
`aiSlingIntervalSeconds` (3 s), it presses the trigger on the target's side and HOLDS for the arc
the turn needs at its fixed squeeze (`StoatSlingMath.AutopilotHoldSeconds`: angle × r / v), then
releases — both edges through the replicated path. Never assume an AI can use a human's input.

**The orbit runs at the speed being flown** (ω = v/r from `VesselStatus.Speed`, and the hole's
strength from the same v), so the circle drawn is the circle flown; throttle up mid-orbit and the
hole strengthens to keep it. From a standstill (X), a press ends the stance and the orbit starts as
the throttle brings the hull up to speed.

**Networking.** Press and release replicate, so every peer lays its own copy of the pair at the
replicated pose; the squeeze does not, so a remote copy keeps its press-time size. The holes are the
placeholder system's local objects (`Docs/BLACK_HOLE.md` §11 "Not yet").

**The crystal style** (`BlackHoleConfig.crystalPairs`, §13) keeps its first design: the pair is laid
on RELEASE, sized by the deepest squeeze (`StoatSlingMath.Peak` — a let-go trigger sweeps back down
before the release edge), `aheadHorizons` (4) ahead, `halfGapHorizons` (Space, 6 → 9) apart.

## 2. The numbers (`StoatSlingConfigSO`, asset `_SO_Assets/VesselActions/Stoat/StoatSlingConfig.asset`)

| Field | Ships as | What it is |
|---|---|---|
| `holdExponent` | 1.5 | `radius = lerp(wide, tight, squeeze^exponent)` (crystal: the strength curve) |
| `holdRampSeconds` | **3** | seconds of hold that count as a full squeeze on a device with no analog trigger (playtest) |
| `autopilotHold01` | 0.5 | the AI's squeeze |
| `orbitRadiusWide` / `orbitRadiusTight` | 150 / 40 u | the orbit at a touch / buried |
| `orbitReach` | **ElementalFloat, Space, ×1 → ×1.4, floor 1** | multiplies both radii — Space is reach; read live, never cached |
| `orbitHorizons` | **6** | orbit radius ÷ horizon radius (the playtest's 6), floored at 3 |
| `radiusFollowRate` / `radialCorrectionRate` | 3 / 5 per s | how fast the radius follows the squeeze / how hard the hull is held on it |
| `slingBoostMin` / `Max` / `Seconds` | 0.25 / 0.9 × speed, 1.5 s | the slingshot |
| `driftSpeed` | **40 u/s** | how fast the let-go pair closes (playtest); `BlackHoleConfig.pairCloseRampSeconds` (0.6 s) is its run-up |
| `lifetime` | **12 s** | the longest hold (playtest); past it the pair lets go on its own and the hull slings |
| `minStrength` / `maxStrength`, `aheadHorizons`, `halfGapHorizons` | 2 / 12, **4**, **Space 6 → 9** | the crystal style only |
| `holdStartEvent` / `slingEvent` | **empty** | FMOD slots (press / slingshot); wire them in the inspector |

Pure arithmetic in `StoatSlingMath` (`Hold01`, `Peak`, `OrbitRadius`, `OrbitHorizon`,
`CircularOrbitGM`, `OrbitHeading`, `RadialCorrection`, `SlingBoost`, `AutopilotHoldSeconds`,
`Strength`, `PairAxis`, `Midpoint`), held by `StoatSlingTests`. The axis runs attractor → repulsor,
so the LEFT trigger's axis is the hull's **+right** (`BlackHolePairMath.Positions` puts the attractor
at −axis).

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
| `Executors/StoatSlingExecutor.cs` | the per-vessel state: the two squeezes (live + deepest), the orbit, the last pair; lays the pair on press, flies the orbit, slings on release (`SpawnPair(held)`, `LetGo`) |
| `StoatSlingMath.cs` | the pure maths |
| `_SO_Assets/VesselActions/Stoat/` | `StoatSlingConfig`, `StoatSlingLeftAction` (side 0), `StoatSlingRightAction` (side 1), `StoatHoldAction` (stationaryMode 1 = Sparrow) |
| `Resources/ElementalAbilityMaps/Stoat.asset` | Space = Slingshot (Input 2), Time = Hold Still (Input 6), Charge and Mass open — see §6 |
| `_Prefabs/Spacevessels/Stoat.prefab` | authored by `Tools/Build/author_stoat_assets.py` from `Squirrel.prefab` (§4) |
| `StoatHullForm.cs`, `StoatHullBuilder.cs` (Vessel/) | the plated hull: pure geometry + morphs, and the emitter (§2.1) |
| `StoatLopeMath.cs`, `StoatAnimation.cs` (Animation/) | the lope (pure) and the puppetry + morph plumbing (§2.2) |
| `Tools/Build/stoat_hull_harness/` | compiles and RUNS the shipped hull form offline; `--obj` exports it |
| `Tools/Build/author_stoat_assets.py` | the generator + `--check` + `--self-test` (ten negative controls); stage 2 authors the hull and the animation swap |
| `Docs/Studios/StoatFlightStudio.html` | the web studio: fly the Stoat on a gamepad, sling either wormhole style, compare them, log decisions (`Docs/Studios/README.md`; live copy with the shared log: https://claude.ai/artifact/Busc3KW6DmVzbsiA2qxoHc) |

**Why the hold is sampled per frame, not read at release — and why the PEAK slings.** The release
edge is raised when the trigger drops back below the deadzone (0.05), so it reads ~0 by the time
`Release` runs; sampling it there would sling every pair at minimum size (the Gibbon's lesson). Keeping
the LATEST per-frame sample is no better: a let-go trigger sweeps back down through several frames
before it crosses the deadzone, so the last sample is ~0.05 too. The first cut shipped exactly that,
and every gamepad sling would have been a minimum-size nudge; it was caught building the web studio,
whose scripted pad releases the way a thumb does. `StoatSlingMath.Peak` keeps the deepest sample
(`StoatSlingTests.Peak_KeepsTheDeepestSqueezeThroughTheLetGo`).

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
- **`SO_Class_Stoat`** (stage 3; Class 14, owned from the start, the Squirrel's icons as
  placeholders) in `SO_Classlist_All` and `SO_Classlist_Classes` — what a card's Vessels list and
  the hangar name a hull by (CONTRACT.md §1.9); `arcade_mode_lib.VESSELS` knows it.
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
(`Docs/BLACK_HOLE.md` §11 "Not yet"); a touch binding (the executor
answers the autopilot's squeeze, nothing presses for it); the "everything destructible" extension of
the black hole itself.
