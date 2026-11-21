> **2026-11-21 - THE SWING IS THE ONLY SLINGSHOT, FOR EVERY PILOT (Yash: "make sure all AI uses our latest slingshot
> mechanics, and delete all other old pathfinding and other mechanics").** A press lays ONE small black hole ahead and to
> the pressed side (RT right, LT left); passing beside it turns your heading toward it; the stick picks the exit angle
> live; the swing's own speed-up is the one slingshot, kept as momentum (`swingKickShare` 0). Every AI Stoat flies the same
> swing in every mode, and nothing is drawn for an AI or a remote Stoat. The field dipole and its predicted path, Time 5's
> Foresight, the catch orbit and its aimed exit, the skim sling, the trampoline and the crystal style are DELETED from the
> code, the assets, the tests and the tools; their record is in git (`STOAT_DIPOLE.md` §6). The omni crystal's ability is
> Fulmen. The mechanics are `STOAT_DIPOLE.md` §1b-§1e; this file is the hull, the prefab and the phone.

> **Naming.** Player-facing, the Stoat lays a small **black hole**. In code it is the black-hole system's own type
> (`BlackHole`, `HolePolarity.Sink`), spawned through `BlackHoleRegistry.Spawn` and owned by the Stoat that laid it.

`VesselClassType.Stoat = 15`. A two-thumb hull whose ONE ability lays a small black hole beside its line and swings round
it: that turn, kept as speed, is the **slingshot**. Its second input is the Sparrow's **stop**, verbatim.

**Size (2026-10-13):** the root Transform is scaled **0.8** (`author_stoat_assets.py` stage 5, `HULL_SCALE`):
the drawn hull, its colliders and skimmers shrink together; `WarpFieldVesselScaler` keeps it as the rest scale.

Its game is **Slingshot** (`GameModes.Slingshot = 64`, `Arcade/SLINGSHOT.md`): a two-lap circuit race where the swing is
the only speed past cruise. The black hole itself — the pull on prisms, the lens, the tides — is documented in
`Docs/BLACK_HOLE.md` and not repeated here.

## 1. The loop — the swing (2026-11-11; one slingshot 2026-11-19)

| Input | What happens |
|---|---|
| **LT press** (`InputEvents.LeftStickAction`, 2) | a small black hole is laid `swingAhead` 150 u ahead and `swingSide` 35 u to the LEFT; this hull's previous hole collapses first; if the hull was holding still (X) the stance ENDS — a swing is a launch |
| fly past it | within `swingReach` (55 u × Space) and with the hole ≥ 40° off the nose, it swings your heading toward itself (ω = v / d, ≤ 150°/s) until you have turned the angle the stick asks (15° away .. 52.5° at rest .. 90° toward), then lets you go on that heading, keeping the swing's warp as momentum |
| **LT release** | while it swings you: let go at once; before it: nothing (the hole stays up until passed or 4 s) |
| **RT** (`RightStickAction`, 1) | the mirror: the hole on the right |
| **X** (`Button1Action`, 6) | `StoatHoldAction` — `ToggleTranslationModeActionSO` in **Sparrow** mode: stop translating and stop laying prisms; press again to move |

**Nobody else is moved.** An owned hole is skipped by `BlackHoleVesselPull` for every vessel: a vessel may not move an
opposing vessel (`Docs/ELEMENTAL_ECONOMY.md` §9, LOCKED). Its owner's turn is flown by `StoatSlingExecutor.StepSwing` on
the simulating machine; prisms feel the hole.

**Drawn for the local human pilot only:** a faint ring at the reach and the dotted EXIT LINE along the heading the stick
asks for now (`StoatPathfinderExecutor`). An AI or remote Stoat draws nothing.

**The autopilot swings** (`StoatSlingExecutor.AutopilotSwing`, the only autopilot): it presses toward a turn it wants (or
for the warp on a long leg) and lets go of the trigger at once; it is let go once it faces its target. Both edges go
through the replicated press path. Never assume an AI can use a human's input.

**Networking.** Press and release replicate, so every peer lays its own copy of the hole at the replicated pose; the
squeeze and the stick do not.

## 2. The numbers (`StoatSlingConfigSO`, asset `_SO_Assets/VesselActions/Stoat/StoatSlingConfig.asset`)

| Field | Ships as | What it is |
|---|---|---|
| `holdRampSeconds` | **1** | seconds of hold that count as a full squeeze on a device with no analog trigger |
| `autopilotHold01` | 1 (Slingshot's copy 0.012) | the AI's squeeze (only the optional second push reads it) |
| `swingAhead` / `swingSide` | 150 / 35 u | where the hole is laid |
| `swingHorizon` / `poleGM` | 0.8 u / 20 000 | its size and its pull on prisms |
| `swingReach` × `orbitReach` | 55 u × (Space ×1 → ×1.4) | where the swing starts — Space is reach |
| `swingCatchDegrees` | 40° | the hole must be this far off the nose (passing beside it) |
| `swingMinDegrees` / `swingMaxDegrees` | 15° / 90° | the stick's range: away from the hole .. toward it |
| `swingDegreesPerSecond` | 150°/s | the fastest the swing turns you |
| `swingSeconds` | 4 s | how long a hole nobody swings round stays up |
| `swingKickShare` | **0** | the share of the second push (squeeze × Mass's `launchPower`, under `speedCapShare` / `warpKickCapCruise`) |
| `catchWarpShare` / `pressWarpShare` / `orbitSpeedBonus` / `momentumSeconds` | 1.15 / 0.5 / 0.15 / 6 s | the warp: its share of the boost, before the swing, the bonus while swinging, the momentum's fade |
| `rippleSeparation` | 90 u | what the Charge ripple a collapsing hole throws reads |
| `aiSlingMinTurnDegrees` / `aiSlingIntervalSeconds` / `aiSlingMinDistance` / `aiWarpMinDistance` / `aiSwingFaceDegrees` | 20° / 0.941 s / 123 u / 0 / 8° | the autopilot (Slingshot flies its trained copy, `Resources/AIBoostPolicies/StoatSlingConfig_Slingshot.asset`) |
| `holdStartEvent`, `swingStartEvent`, `swingFlingEvent` | listed placeholders | FMOD slots |

Pure arithmetic in `StoatSlingMath` (`Hold01`, `Peak`, `AutopilotPress`, `SlingBoost`, `CappedKick`, `WarpCappedKick`,
`MomentumWarp`, `AccretionBonus`, `RingNormal`, `Ring`, `SwingHole`, `SwingStarts`, `SwingAxis`, `SwingTurnDegrees`,
`SwingRate`, `SwingExit`, `SwingAiLetsGo`, `OnSwingSide`), held by `StoatSlingTests`.

## 2.1 The hull — `Stoat.fbx`, a model (2026-10-18; the procedural placeholder is kept until it is accepted)

The hull is **`Assets/_Models/Vessel Models/Stoat.fbx`**, built headlessly with Blender-as-a-module from
**`Tools/Build/vessel_model/specs/stoat.py`** (`/vessel-model`, the new-hull path the Butterfly opened). It replaced the
procedural placeholder (`StoatHullForm` + `StoatHullBuilder`: five diamond plates, a wedge head, a four-plate tail with a
crystal, four strut legs, two slots, flat-shaded), which the owner judged "terrible even as placeholders" (2026-10-17).
It is a **spaceship inspired by a stoat**, not the animal: read from the chase camera (`StoatCameraSettingsSO`
followOffset (0, 6.5, -21), 68 degrees; the root's 0.8 scale makes the 8-unit hull 6.4 world units, ~45 px wide at
1080p, its back toward the pilot) as a long, low, sinuous interceptor.

| Clade | On the ship | Bone |
|---|---|---|
| body | five overlapping armour segments (a chine at the waterline, a raised collar at each back edge) over a dark spine core that shows between them when the lope arches the spine; a sunken domain panel either side of the dark ridge, a domain trim line on each flank and across each collar | `plate.0`, `plate.1`, `hull` (the core plate), `plate.3`, `plate.4` |
| dorsal ridge | three swept emitter fins on plates 0 / 2 / 4, domain-coloured | their plates |
| head and eyes | a pointed prow; the eyes are a split sensor visor (Window glass) sunk either side of the ridge | `nose` |
| ears | two small swept sensor fins, domain-tipped | `nose` |
| haunches | twin engine nacelles, lime mouths and side vents, a domain band; the mouths sit where the prefab's two `VesselJet`s are (Unity (+-0.3, -0.05, -2.15) under `StoatHull`) | `plate.4` |
| legs | four landing struts ending in manoeuvring-thruster pods (Engine nozzles) | `leg.1.L/R` (under plate 1), `leg.2.L/R` (under plate 3) |
| tail | a four-segment dark boom of shingled collars | `tail`, `tail.2`, `tail.3`, `tail.4` |
| black tail tip | a faceted DOMAIN-coloured beacon spike behind a dark collar: the stoat's dark tip, in the domain colour so the domain reads from behind | `tail.5` |

**Contract** (the fleet convention for a new vessel, `FLEET_CONVENTION.md` sections 1-4):

| | |
|---|---|
| file / preset | `Stoat.fbx` (guid `0ef0b84d93774ac39ddc699c59d0821a`), `zup`: nose -Y, up +Z, the pilot's left +X in Blender; in Unity nose +Z |
| nodes | mesh `mesh`, armature `rig`, root bone `hull` |
| bones | 15, every one a child of `hull`, each head at the placeholder's part pivot: plates at Unity z 1.6 / 0.8 / 0 / -0.8 / -1.6, `nose` at the neck (0, 0, 2.0), `tail` .. `tail.4` at (0, 0.065, -2.0 - 0.525 j), `tail.5` at (0, 0.065, -4.1), the legs at the hips (+-0.55 r, -0.55 r) under plates 1 and 3 |
| size | Blender frame x +-0.447, y -3.168 .. 4.82 (nose to beacon tip), z -0.665 .. 0.769: 7.99 long, 0.89 wide, 1.43 tall - the placeholder's 7.96 / 0.90 / 1.48 within 4% (the 1.2 x 1.4 x 7 hit box, the jets and the camera were tuned to it) |
| slots | 0 `Domain`, 1 `Body`, 2 `Window`, 3 `Engine`; the `.meta` remaps all four by name to the fleet's four materials |
| morphs | `Charge` 7.5% of the 8.16 diagonal (the emitter fins grow into tall swept blades, the beacon swells, the visor bulges), `Mass` 4.5% (plates, prow, nacelles and core fatten about the spine, the boom less), `Space` 10.5% (the body draws out x1.12, the boom x1.22, the prow reaches), `Time` 5.3% (the struts lengthen and rake back, the sensor fins prick up and sweep, the prow sharpens) - the placeholder's meanings |
| takes | none drive it; four documentation poses for the sheet and the studio, each with `pose_expect`: `arch`, `land`, `turnright`, `turnleft` (StoatAnimation's lope and turn at their extremes) |
| budget | 10,324 tris, 5,218 verts, UVs, no n-gons, nothing floating (every part overlaps the part it hangs from) |
| sheet | `Docs/VesselModels/sheets/stoat_after.jpg` |

**Integration is by NAME** (an FBX's sub-object fileIDs cannot be computed offline). `author_stoat_assets.py` stage 2
now authors `StoatHull` as a bare Transform holding the two jets and the model (a nested PrefabInstance at identity);
the MeshFilter / MeshRenderer / `StoatHullBuilder` are gone from the prefab. `StoatAnimation` resolves the bones under
its `hullVisual` (the `StoatHull` transform; the placeholder's part names still resolve, and a prefab that still
carries `StoatHullBuilder` is driven exactly as before), finds the `SkinnedMeshRenderer` (`updateWhenOffscreen`, since
the lope lifts the hull 1.6 u), and poses every bone from its REST in the hull transform's frame, so the lope, the
turn bend and the tail swing are the placeholder's, unchanged. The element morphs are now blend shapes the base
`VesselAnimation` drives by name; the builder's `IProceduralElementMorphSource` path runs only while a builder is
present. The spaghettification (`BlackHoleWarp.VesselLogStretch`) and the lope still scale / lift / pitch the
`StoatHull` transform, which now carries the model; `WarpFieldVesselScaler` still scales the root.
`VesselCustomization` paints `StoatHull`, which it resolves to the model's skinned renderer, and replaces
`GreenAccentVesselMaterial` (the Domain slot) by identity.

**Kept until the owner accepts the model:** `StoatHullForm.cs`, `StoatHullBuilder.cs`, `StoatHullTests`,
`Tools/Build/stoat_hull_harness/`. The card images are rendered from the MODEL by
`Tools/Build/vessel_model/render_cards.py Stoat` (`render_stoat_card_icon.py`, which drew the placeholder, was retired
2026-10-18). To go back, restore the stage-2 hull block of `author_stoat_assets.py` from git and rerun it.

## 2.2 The bounding lope — body-only (`StoatLopeMath` + `StoatAnimation`)

The user's tuning: amplitude 1.00 (→ 1.6 u at the body's scale), rate 0.66 (one bound every 1.5 s),
arch 0.45, stretch 0.20, speed link 0. **Body-only**: the drawn hull rises nose-up, arches long at
the top, lands nose-down, bunched, legs reaching — while the vessel's transform, flight path and
prism trail stay straight, so aim and the wake are steady. `StoatAnimation` replaced the Squirrel
clone's `MantaAnimationContoller` on the same component (so `VesselStatus`'s reference holds); it
writes transforms only (hull lift/pitch/stretch, plate arch and tilt, head nod/turn, tail lift and
swing out of a turn, leg reach) - on the model, the bones of §2.1, posed from their rest in the hull's frame -
and the element morphs write vertices only (the model's blend shapes; the builder's on the placeholder). The stick bends the spine into a
turn (12°), turns the head (18°) and swings the tail out (28°). Poses are written directly, not
through the base class's lerp (the Butterfly lesson: it eats a slow periodic motion).


## 3. The parts

| File | Role |
|---|---|
| `Data Containers/StoatSlingConfigSO.cs` | the numbers above |
| `Data Containers/StoatSlingActionSO.cs` | one asset per trigger (`side`), shared and stateless: press → `BeginHold`, release → `Release` |
| `Executors/StoatSlingExecutor.cs` | the per-vessel state: the two squeezes, the hole, the swing; lays the hole on press, flies the swing, lets go, collapses it; the swing's autopilot |
| `StoatSlingMath.cs` | the pure maths |
| `Executors/StoatPathfinderExecutor.cs`, `StoatPathfinderWorldDots.cs` | the flight clock's warp, and the reach ring and exit line (local human pilot only) |
| `_SO_Assets/VesselActions/Stoat/` | `StoatSlingConfig`, `StoatSlingLeftAction` (side 0), `StoatSlingRightAction` (side 1), `StoatHoldAction` (stationaryMode 1 = Sparrow), `StoatDipoleConfig` (the warp), `StoatRippleConfig`, `StoatModeAction` |
| `Resources/ElementalAbilityMaps/Stoat.asset` | Space = Catch Orbit (the swing), Time = Timed Slingshot (the warp; Time 5 open), Charge = Ripple, Mass = Heavy Launch (`STOAT_DIPOLE.md` §5) |
| `_Prefabs/Spacevessels/Stoat.prefab` | authored by `Tools/Build/author_stoat_assets.py`: cloned once from `Squirrel.prefab` (§4), its own vessel since stage 6 |
| `Assets/_Models/Vessel Models/Stoat.fbx`, `Tools/Build/vessel_model/specs/stoat.py` | the hull model and the spec that builds it (§2.1); `build.py stoat --check` is its drift gate |
| `StoatHullForm.cs`, `StoatHullBuilder.cs` (Vessel/) | the procedural placeholder (pure geometry + morphs, and the emitter), off the prefab since 2026-10-18 and kept until the model is accepted |
| `StoatLopeMath.cs`, `StoatAnimation.cs` (Animation/) | the lope (pure) and the puppetry + morph plumbing (§2.2) |
| `Tools/Build/stoat_hull_harness/` | compiles and RUNS the placeholder hull form offline; `--obj` exports it |
| `Tools/Build/author_stoat_assets.py` | the generator + `--check` + `--self-test`; stage 2 authors the hull and the animation swap, stage 6 the own-vessel pass |
| `Tools/Build/stoat_orbit_sim.py` | the offline model of every Stoat kit; `--swing` is the one that ships |
| `Docs/Studios/StoatFlightStudio.html` | the web studio (`Docs/Studios/README.md`; live copy: https://claude.ai/artifact/Busc3KW6DmVzbsiA2qxoHc). It still flies the old field trajectory and catch sling; its 34 deleted keys are marked `lab` |

**Why the hold is sampled per frame — and why the PEAK counts.** The release edge is raised when the trigger drops back
below the deadzone (0.05), so it reads ~0 by the time `Release` runs, and a let-go trigger sweeps back down through
several frames before it. `StoatSlingMath.Peak` keeps the deepest sample (`StoatSlingTests.Peak_KeepsTheDeepestSqueezeThroughTheLetGo`).

**One hole per Stoat.** A new press collapses THIS hull's previous hole first (never another hull's), so a pilot can chain
swings without exhausting `BlackHoleConfig.maxBlackHoles` (16). A refused press (no room) is a verbose log on the
`BlackHole` channel, not an error.

## 4. The prefab — what the generator changed and what it kept

`Stoat.prefab` is `Squirrel.prefab` with exactly these edits (the whole diff is the generator's
`build_clone`; `--check` re-asserts every one of them on the SHIPPED file):

- root GameObject `m_Name` and `VesselStatus._name` → Stoat; `vesselType: 15` (the prefab's ADDRESS
  in the container — `check_vessel_prefab_container.py`).
- **A fresh Netcode identity.** A disk copy keeps the donor's `GlobalObjectIdHash`, and Netcode keys
  its prefab table on that number alone, so the Squirrel or the Stoat would silently never spawn
  (`Tools/Build/check_network_prefab_hashes.py`, ported from the Gibbon branch, which hit this). The
  generator computes the hash the way `NetworkObject.OnValidate` does — XXHash32 of
  `GlobalObjectId_V1-1-<guid>-<fileID>-0` — and proves it by reproducing the Squirrel's own
  `2256742461` from its guid; the editor regenerates the same number, so opening the prefab changes
  nothing. `InScenePlacedSourceGlobalObjectIdHash` is 0.
- Gamepad bindings: 2 → `StoatSlingLeftAction`, 1 → `StoatSlingRightAction`, 6 → `StoatHoldAction` (nothing on B).
  Keyboard, dual-mouse and mouse-keyboard share the gamepad map (`R_VesselActionHandler`: Left/Right
  Shift are the triggers). ~~**Touch overrides are cleared** — there is no touch design yet, so a touch
  Stoat has no abilities (the Gibbon's call, same reason).~~ Superseded 2026-10-21: the touch overrides carry the
  same four entries (§4a).
- The Squirrel's `DriftActionSO`s are NOT bound, so the transformer's `singleTriggerDrift: 1` is
  inert: `ApplyAnalogDrift` acts only after a drift action's `BeginDrift`, which nothing calls.
- `StoatSlingExecutor` (config wired) and `ToggleTranslationModeActionExecutor` (the hull's own
  `VesselPrismController`, no seed assembler, the fleet's `stationaryModeChanged` and
  `OnMiniGameTurnEnd` channels — the guids the Sparrow carries, `check_vessel_shared_channels.py`)
  added on the `ShipActions` object and in `ActionExecutorRegistry._executors`.
- **`SO_Class_Stoat`** (stage 3; Class 15, owned from the start, the Squirrel's icons as
  placeholders) in `SO_Classlist_All` and `SO_Classlist_Classes` — what a card's Vessels list and
  the hangar name a hull by (CONTRACT.md §1.9); `arcade_mode_lib.VESSELS` knows it.
- Registered in `Vessel Prefab Container.asset` and `DefaultNetworkPrefabs.asset`;
  `ToyVesselRoster.Default` lists it; `EnumIntegrityTests` locks 15 (the Thresher took 14) and the
  count, 17 (it had not been bumped for the Butterfly).

**No longer kept (stage 6, 2026-10-13).** The prototype debt this paragraph listed — the Squirrel's hidden model and
its ~4 u hit box, its HUD variant and icons, its tube and drift-trail executors, its camera settings, its telemetry, its
containers — is gone. The Stoat has its own hit box sized to its hull (`StoatBody` / `StoatHitbox`), two jets on its
hips, `StoatHUDVariant`, `StoatCameraSettingsSO` (the same chase, clean keys: the studio's chase camera since
2026-10-10, `STOAT_DIPOLE.md` §3a; far clip 12000, `check_vessel_camera_farclip.py`), `Stoat*ImpactorDataContainer`
(no Squirrel element gates), `StoatAIBoostPolicy` and `DefaultVesselTelemetry`. The full list is the generator's
docstring and `check_stage6`. **The clone is a spent one-shot once committed**:
`author_stoat_assets.py` clones only while `Stoat.prefab` is absent and otherwise stands down and
validates the shipped file, so Squirrel drift can never silently re-author the Stoat.

## 4a. On a phone (2026-10-21)

**The decision.** Garrett, 2026-10-21: "The stoat should join the mobile fleet."

**The bindings.** `Stoat.prefab` `_touchActionOverrides` = the gamepad overrides, entry for entry, the same assets:

| Input (`InputEvents`) | Actions (same guids as the pad's) |
|---|---|
| `LeftStickAction` (2) | `StoatSlingLeftAction` (`5c382e0d…`) |
| `RightStickAction` (1) | `StoatSlingRightAction` (`012d65ba…`) |
| `Button1Action` (6) | `StoatHoldAction` (`9b50ea11…`) |

Owned by `Tools/Build/author_stoat_assets.py` (`bindings()`, `--check`);
`StoatSlingTests.Prefab_BindsEachTriggerToItsOrbit_XToTheHold_AndNothingToB` asserts the touch block equals the pad block.

**The squeeze on glass.** There is no trigger depth on a phone, so `StoatSlingMath.Hold01` ramps by HOLD TIME over
`holdRampSeconds` (1 s), as on keys. The squeeze only sizes the optional second push, which ships off.

**A held button is the pad's trigger.** The card (`AbilityCardPress`) and the extra button (`TouchButtonView`) raise the
same events on finger down / up, through the same handler and the same replicated path. A hidden HUD or a vessel swap
releases a held card (`AbilityCardPress.OnDisable`).

**The autopilot.** On a handheld every `InputController` reports `Touch`, the AI's included, so an AI Stoat on a phone
resolves its presses against the touch overrides: the same assets, the same swing. `ResolveBoundInput` reads every map.

**Prototype, 2026-11-02 (Yash; NEEDS GARRETT'S SIGN-OFF): ThumbLifts.** Lifting the RIGHT thumb raises `LeftStickAction`
while the left thumb flies; lifting the LEFT thumb raises `RightStickAction`. Hold is the one extra disc, and the Mass
card is display-only.

## 5. Proof

2026-11-21: Assembly-CSharp and Assembly-CSharp-Editor compile with Unity's Roslyn (0 errors);
`author_stoat_assets.py --check` and `--self-test` (with a negative control for the deleted dipole executor coming back
and for Time 5's Foresight coming back); `studio_to_unity.py --check` (Stoat: 25 mapped, 0 drift);
`StoatSlingTests.EveryStoatAI_FliesTheSwing_NoOtherSlingshotPathIsLeft` and
`NoAIOrRemoteStoatDrawsAPath_TheLocalHumanOnly`. The EditMode run and the hand test: `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## 6. Design status

**Today's kit (the swing, 2026-11-11; one slingshot 2026-11-19; the only slingshot 2026-11-21):** Space = the swing (map
label Catch Orbit; reach ×1 → ×1.4; L5 Accretion — stolen prisms +4% ripple reach each, to +40%), Time = the warp (map
label Timed Slingshot; L5 open since Foresight was deleted), Charge = Ripple (L5 Siphon), Mass = Heavy Launch (the second
push ×1 → ×2.5, shipped off; L5 Armoured Wake), omni = Fulmen. Open for Yash: Time 5, and whether Mass should scale
something on the swing. The decisions' wording is in `Docs/ElementalAbilitySystem/FLEET_MAPS.md` ▸ Stoat.

## 7. Not yet

The hole's network replication (a peer lays its OWN copy of the hole at the replicated pose; the two copies are not one
object); an analog squeeze on a phone; FMOD events beyond the listed placeholders.
