# Orca: the 4-seat crew hull

> A NEW hull (2026-10-21), built by `/vessel-model` from `Tools/Build/vessel_model/specs/orca.py` to the fleet
> convention (`.claude/skills/vessel-model/references/FLEET_CONVENTION.md` 1-4). There is no artist file.
> Garrett, 2026-10-21: "it is time to use the vessel modeling tools and really step up the vessel".
> What the crew does: `Assets/_Scripts/Controller/Vessel/Crew/ORCA_CREW.md`. **Not wired yet**: `Orca.prefab` still
> draws the Sparrow's mesh and `OrcaCrewView`'s procedural pods until the model is nested and the turret bones are
> driven from code.

## Brief

A spaceship inspired by the orca, never the animal: one helm and three gunner turrets on one hull.

| clade | ship |
|---|---|
| broad, deep body | the armoured hull (`hull`): a superellipse loft with a conical melon nose and three panel seams |
| eye patch | a long tilted **sensor lens** on each side of the head, set in a dark bezel |
| blowhole | the helm's **canopy**, glass in a dark frame, with a spine rail running back to the pylon |
| white flank sweep, saddle, chin and belly | **conformal domain armour plates**: a flank sweep per side rising toward the tail, a saddle plate behind the pylon with black hull between them, and a chin-and-belly plate. Each plate's outline is a smooth curve on the hull, not a stair of painted faces |
| tall dorsal fin | the **dorsal pylon** (`fin`): a raked airfoil blade with a domain rank band, a domain trim line up its trailing edge (so it reads from behind), and a collar pedestal carrying the SNIPER |
| pectoral fins | the **outriggers** (`wing.L` / `wing.R`): paddles whose chord swells mid-span, with sunk domain panels. Each tip is a nacelle with a domain band, a domain running light and a small ember mouth aft, and a ring seat carrying a turret |
| tail stock | the peduncle (`tail`) behind a domain-trimmed joint collar |
| flukes | the **engine section** (`tail.2`): two broad swept fluke planes, domain underneath and a domain trailing-edge trim on top, carrying the two main engines (`jet.L` / `jet.R`). Each engine's bell is skinned to its `.mouth` child |

### The turrets

Every turret aims straight forward at rest. Each one has:
- a domain-banded base ring and yoke plates (yaw bone);
- trunnion pins, a housing and the gun (pitch bone);
- a muzzle part at the barrel tip (muzzle bone);
- a missile tube on the outboard end of the trunnion pin (pitch bone), whose mouth collar skins to the missile bone.

Every leaf bone skins geometry, so `optimizeBones` cannot strip it. Each turret is built at unit size and scaled
1.35x about its pivot.

| seat | turret | reads as |
|---|---|---|
| Gunner 1, the SPACE seat (dorsal) | **Sniper** | a railgun: a 17.6 u barrel (3x the 5.8 u housing) between two rails, five domain coil rings, a vented muzzle brake with an ember bore, and a scope with a glass lens on a mount block |
| Gunner 2, the MASS seat (port, the pilot's left) | **Fast gun** | a "calligraphy pen": a tapered pen-barrel housing with a domain band, and a six-barrel rotary cluster (ember bores) through a clamp ring and a domain muzzle ring |
| Gunner 3, the CHARGE seat (starboard) | **Bomb** | a mortar: a boxy cradle and a short fat drum with a domain waist band, a flared wide mouth lip and an ember throat |

## Rig contract

- **File**: `Assets/_Models/Vessel Models/Orca.fbx`, guid `9b34bfdd14c8462c8bc138b3938820a2`.
  - The `.meta` is the Grouper's with the guid changed. It remaps all four slots by name: Body `54e78ce1…`
    (BlueBase), Domain `539a8c65…` (GreenAccent, which ThemeManager repaints), Engine `0c127fff…`, Window
    `923d3665…` (Screen).
  - The FBX's own material colours are the pipeline's placeholders. Only the remap makes them the game's.
- **Preset**: `zup`. The spec models in the Blender frame (nose -Y, up +Z, pilot's left +X).
  - Conversion: Unity (x, y, z) = (-bx, bz, -by).
  - Nested under the hull root at identity, scale 1: the nose is +Z and up is +Y.
  - The mesh node is identity in the file. Unity may show a Z-up file's mesh node in the file frame (`LESSONS.md` 47),
    so never take a frame from it.
- **Nodes**: mesh `Orca` and armature `OrcaArmature`, siblings at the top of the file; root bone `hull`.
  - The brief named these so the nested model resolves unambiguously beside the hidden Sparrow model.
  - The convention's generic names would be `mesh` / `rig`.
- **Bones**: 22, skinned rigidly with one bone per part. Rest heads in Unity hull space, read by two readers that
  agree (the FBX node chain composed with the repo codec, and the Blender re-import):

| bone | parent | head (Unity) | skins |
|---|---|---|---|
| `hull` | — | (0, 0, 0) | 3018 verts |
| `fin` | hull | (0, 2.8, 0.4) | 426 |
| `SniperYaw` | fin | **(0, 16.2, -1.8)** | 248 (ring, yoke) |
| `SniperPitch` | SniperYaw | (0, 17.483, -1.8) | 790 (housing, barrel, rails, coils, scope, pins, missile tube) |
| `SniperMuzzle` | SniperPitch | (0, 17.55, 18.315) | 122 (brake) |
| `SniperMissile` | SniperPitch | (-2.227, 17.483, 1.046) | 98 (tube mouth) |
| `wing.L` | hull | (-4.45, -2.1, 5.58) | 576 |
| `FastGunYaw` | wing.L | **(-21.6, 3.15, -4.5)** | 248 |
| `FastGunPitch` | FastGunYaw | (-21.6, 4.838, -4.5) | 726 |
| `FastGunMuzzle` | FastGunPitch | (-21.6, 4.838, 2.52) | 108 (muzzle ring) |
| `FastGunMissile` | FastGunPitch | (-23.962, 4.838, -1.811) | 98 |
| `wing.R` | hull | (4.45, -2.1, 5.58) | 576 |
| `BombYaw` | wing.R | **(21.6, 3.15, -4.5)** | 248 |
| `BombPitch` | BombYaw | (21.6, 5.445, -4.5) | 406 |
| `BombMuzzle` | BombPitch | (21.6, 5.445, -0.99) | 120 (mouth lip) |
| `BombMissile` | BombPitch | (24.165, 5.108, -2.124) | 98 |
| `tail` | hull | (0, 0.48, -9.3) | 216 |
| `tail.2` | tail | (0, 0.5, -12.5) | 280 |
| `jet.L` / `jet.R` | tail.2 | (∓2.2, 0.6, -11.8) | 120 each |
| `jet.L.mouth` / `jet.R.mouth` | jet.* | (∓2.2, 0.6, -14.75) | 140 each (bell); length axis aft |

The three yaw pivots are `OrcaCrewCore.Mounts[s] * hullRadius` (9), exactly.

**Turret bone axes.** Every turret bone's FBX node axes are aligned with the hull:
- local +X is starboard (Unity +X);
- local +Y is the nose (Unity +Z);
- local +Z is up (Unity +Y).

So yaw is about the hull's up and pitch is about the bone's right. How Unity's importer expresses that as a
`localRotation` is not measurable offline. Drive the bones rest-relative in the hull's frame:
1. Capture `localRotation` and the hull's axes in the bone's local frame at rest (`WrasseCrewBodies.Bone.Find` is the
   pattern).
2. Each frame, write `Rest * AngleAxis(yaw, Up)` on the yaw bone and `Rest * AngleAxis(pitch, Right)` on the pitch bone.

The yaw bones are children of `fin` / `wing.L` / `wing.R`, so they ride those parts if a driver ever moves them.

- **Slots**: 0 Domain, 1 Body, 2 Window, 3 Engine.
  - Polygons: 1964 / 6846 / 350 / 494.
  - Surface share (style_metrics): domain 0.18, window 0.020, engine 0.010.
- **Shape keys**, as travel in % of the 65.7 u diagonal:

| key | meaning | what moves | travel |
|---|---|---|---|
| Charge | weapon | the three housings, the cradle and the scope grow 2x along the gun and 1.15x across; the barrels, coils, muzzle parts, drum, pins and missile tubes swell; the base rings widen. No tip moves | 4.6% |
| Mass | volume | the hull fattens 1.38x about its axis, plus a 0.45 u inflate; the peduncle, engine cases and nacelles (widening only) swell; the flukes, pylon and outriggers thicken | 4.0% |
| Space | reach | the railgun barrel telescopes 1.35x; the fast gun's barrels 1.35x; the mortar drum 1.3x; the flukes span 1.25x; the outrigger nacelles telescope fore and aft about the pivot | 9.3% |
| Time | speed | the nose stretches and sharpens; the flukes sweep back; the outriggers bow aft (tips fixed); the nacelle tails stretch aft | 7.3% |

  - No morph moves a turret pivot or a jet `.mouth`.
  - **Space moves the muzzle tips**, which the bones do not follow. At 100%: the sniper brake +6.1 u forward along the
    barrel, the fast gun ring +1.7 u, the bomb lip +1.0 u. A muzzle that must sit on the drawn tip adds
    `aim * offset * spaceWeight`.

- **Takes**: none drive the model. Four documentation poses for the sheet and the studio, each direction-proved by
  `pose_expect` on the re-import:
  - `pitchup`: the flukes curl down.
  - `yawright`: the flukes swing to the outside of the turn.
  - `aimright`: every turret yaws 40° to the pilot's right.
  - `aimup`: every turret pitches 25° up.
- **The Sparrow's Animator** stays on the hidden Sparrow bones. The convention's nine input takes are needed only by an
  Animator-driven model.

## Measured (`fbx_probe.py`, Unity units)

| | Orca | the Sparrow mesh Orca.prefab draws today |
|---|---|---|
| span (x) | **49.34** (-24.57 .. 24.77, turret tips) | 12.50 |
| length (z) | **36.28** (-17.97 fluke tips .. +18.32 sniper muzzle; nose at +16.0) | 12.23 |
| height (y) | **23.89** (-4.01 belly .. +19.89 sniper scope) | 1.78 |
| triangles | 17,536 (8,922 verts) | 25,984 |

- The hull alone runs from the nose at z +16 to the flukes at -18, is 11.2 wide and 8.9 deep.
- The Sparrow hit box, skimmer and camera (-50) that `Orca.prefab` still carries were sized to a 12-unit hull. Those
  belong to the prefab wiring.
- 17.5k triangles is above the convention's inferred 14k ceiling (FLEET_CONVENTION 1.4), and below the Sparrow's and
  Dolphin's 25-26k. The three bespoke turrets cost about 7k of it.

`build.py orca --check` reports OK. All three readers pass: the codec, assimp and the Blender re-import.
`measure_vessel_models.py --only Orca` finds 4 labelled element shapes, all 4 real. `vessel_colors.py --check` is OK.

`style_metrics.py --fleet` places it at:
- smooth 0.59, hard 0.13;
- domain 0.18, window 0.020, engine 0.010;
- 26 domain regions.

All of these are inside the artist band. The metric is informative only.

## What the sheet shows

Sheets: `Docs/VesselModels/sheets/orca_after.jpg`, plus close-ups of the three turrets in `sheets/orca_turrets.jpg`.

- **Chase:** the camera sees the T of a tall pylon over two long outriggers. The pylon's trailing-edge trim and rank
  band, the saddle oval, the flank sweeps, the wing panels, two big engine mouths between the flukes, and a gun pod at
  each tip all read.
- **Front and side:** an orca: a melon head with the two eye-patch lenses, a domain chin and belly, and the tall raked
  fin.
- **Rounds:**
  1. Round 1: a blimp under a pole. The eye patch was painted faces (blocky), the flank band read as a hoop, and the
     turrets were too small.
  2. Rounds 2-4: the turrets were scaled 1.35x about their pivots, the eye patches became lenses, the outriggers became
     paddles, and the hull grew to 26 u nose-to-peduncle so the pylon stopped reading as a neck.
  3. Round 5: the painted domain became conformal plates with smooth outlines.
  4. Round 6: a domain trim line up the pylon's trailing edge, so the fin reads from behind.

## Open

- **Integration (the parent session):** nest the model, hide the Sparrow mesh, drive the turret bones, and retire
  `OrcaCrewView`'s procedural pods. Re-fit the hit box, skimmer and camera to the hull (FLEET_CONVENTION 6: length 34 u
  gives |camera| about 113 by the 0.3 rule; the turret span is 49 u).
- **Card images:** `render_cards.py` has no Orca entry yet, so the class card is not rendered from this file.
- **The owner's flight is the result.**
