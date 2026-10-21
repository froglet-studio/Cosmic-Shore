# Orca: the 4-seat crew hull

> A NEW hull (2026-10-21), built by `/vessel-model` from `Tools/Build/vessel_model/specs/orca.py` to the fleet
> convention (`.claude/skills/vessel-model/references/FLEET_CONVENTION.md` 1-4). There is no artist file.
> Garrett, 2026-10-21: "it is time to use the vessel modeling tools and really step up the vessel".
> What the crew does: `Assets/_Scripts/Controller/Vessel/Crew/ORCA_CREW.md`. Wired into `Orca.prefab` by the parent session
> (`OrcaHullRig` drives the turret bones by name).

## Brief

A spaceship inspired by the orca, never the animal: one helm and three gunner turrets on one hull.

**Round 2 (2026-10-21): a hovering assembly.** Garrett flew round 1 in Unity and said: "the original vessels are
based largely around the idea of free floating parts that are associated and animated together but clearly distinct
parts of a hovering assembly. this seems to not be observed in the new models and needs correcting", and "this will
also help keep the guns associated with the vessel but not have to worry about mating planar surfaces".

The artist hulls show the gaps he means:
- the Manta's wings float 3.4-6.5% of the diagonal clear of the chassis;
- the Dolphin's wings float 5.7% clear;
- the Sparrow's tail segments float 0.8-1.1% clear, and its wing pieces 0.1-0.2%.

So every major part of the Orca is its own floating piece on its own bone. The designed gaps are 1.55-2.2% of the
66 u diagonal, with the facing edges parallel. Nothing is mated: there are no struts, root fillets, pedestals or ring
seats.

| clade | ship |
|---|---|
| broad, deep body | the **core** (`hull`): a superellipse loft with a conical melon nose, three panel seams, and the tail stock behind a domain-trimmed collar |
| eye patch | a long tilted **sensor lens** on each side of the head, set in a dark bezel |
| blowhole | the helm's **canopy**, glass in a dark frame, with a spine rail behind it |
| white flank sweep, saddle, chin and belly | **conformal domain armour plates** on the core: a flank sweep per side rising toward the tail, a saddle plate behind the dorsal blade with black hull between them, and a chin-and-belly plate. Each plate's outline is a smooth curve on the hull, not a stair of painted faces |
| tall dorsal fin | the **dorsal blade** (`fin`), hovering 1.6 u over the back. Its root edge follows the hull's back line, so the gap is even. It is a raked airfoil with a domain rank band, a domain trim line up its trailing edge (so it reads from behind) and a domain tip cap. The SNIPER hovers about 1.45 u over the flat tip, at its exact pivot |
| pectoral fins | the **outrigger chain**: three floating paddle plates per side (`wing.L.1..3`, `wing.R.1..3`), each with a sunk domain panel, stepping out and up from the core. Their end faces are parallel planes 1.15 u apart. Each chain ends at the turret's **pod** (`pod.L` / `pod.R`): a nacelle with a domain band, a domain running light and a small ember mouth aft. The FAST GUN (port, the pilot's left) and the BOMB (starboard) hover 1.0-1.1 u over their pods, at their exact pivots |
| flukes | the **engine section**, floating behind the tail stock: two engine pods (`engine.L` / `engine.R`), each with its glowing bell skinned to its `jet.X.mouth` child, between two fluke blades (`fluke.L` / `fluke.R`). The flukes are domain underneath with a domain trailing-edge trim on top |

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
- **Bones**: 28, skinned rigidly with one bone per part. Every floating part's bone head sits at the part's centre.
  Rest heads in Unity hull space, read by two readers that agree (the FBX node chain composed with the repo codec,
  and the Blender re-import):

| bone | parent | head (Unity) | skins |
|---|---|---|---|
| `hull` | — | (0, 0, 0) | 3062 verts (core) |
| `fin` | hull | (0, 9.887, 0.75) | 176 (dorsal blade) |
| `wing.L.1` / `.2` / `.3` | hull | (-8.292, -1.259, 2.85) / (-12.825, -0.752, 0.004) / (-17.358, -0.275, -2.724) | 76 each |
| `wing.R.1` / `.2` / `.3` | hull | (8.292, -1.259, 2.85) / (12.825, -0.752, 0.004) / (17.358, -0.275, -2.724) | 76 each |
| `pod.L` / `pod.R` | hull | (∓21.6, -0.1, -4.3) | 244 each (nacelle, running light) |
| `fluke.L` / `fluke.R` | hull | (∓7.9, 0.5, -15.967) | 140 each |
| `engine.L` / `engine.R` | hull | (∓1.9, 0.5, -15.05) | 120 each (case) |
| `jet.L.mouth` / `jet.R.mouth` | engine.* | (∓1.9, 0.5, -16.85) | 140 each (bell); length axis aft, the plume host |
| `SniperYaw` | hull | **(0, 16.2, -1.8)** | 248 (ring, yoke) |
| `SniperPitch` | SniperYaw | (0, 17.483, -1.8) | 790 (housing, barrel, rails, coils, scope, pins, missile tube) |
| `SniperMuzzle` | SniperPitch | (0, 17.55, 18.315) | 122 (brake) |
| `SniperMissile` | SniperPitch | (-2.228, 17.483, 1.046) | 98 (tube mouth) |
| `FastGunYaw` | hull | **(-21.6, 3.15, -4.5)** | 248 |
| `FastGunPitch` | FastGunYaw | (-21.6, 4.838, -4.5) | 726 |
| `FastGunMuzzle` | FastGunPitch | (-21.6, 4.838, 2.52) | 108 (muzzle ring) |
| `FastGunMissile` | FastGunPitch | (-23.962, 4.838, -1.811) | 98 |
| `BombYaw` | hull | **(21.6, 3.15, -4.5)** | 248 |
| `BombPitch` | BombYaw | (21.6, 5.445, -4.5) | 406 |
| `BombMuzzle` | BombPitch | (21.6, 5.445, -0.99) | 120 (mouth lip) |
| `BombMissile` | BombPitch | (24.165, 5.108, -2.124) | 98 |

The three yaw pivots are `OrcaCrewCore.Mounts[s] * hullRadius` (9), exactly.

**Parents.** The yaw bones moved from `fin` / `wing.L` / `wing.R` to `hull` in round 2. No floating part carries
a turret, and `OrcaHullRig` sets each yaw bone's pose in the hull's frame anyway.

**Turret bone axes.** Every turret bone's FBX node axes are aligned with the hull:
- local +X is starboard (Unity +X);
- local +Y is the nose (Unity +Z);
- local +Z is up (Unity +Y).

At rest each barrel axis is the bone's forward. Yaw is about the hull's up and pitch is about the bone's right.
How Unity's importer expresses that as a `localRotation` is not measurable offline, so drive the bones rest-relative
in the hull's frame (`OrcaHullRig` does).

**Floating-part bone axes.** `pod.*` and `engine.*` are aligned with the hull (engines point aft). The plates point
along the chain's span, the flukes outboard, and `fin` up. A driver that animates a floating part should also capture
its rest and rotate in the hull's frame.

**Gaps between major parts.** Nearest vertex distance to any other major part, measured on the written file
(`partgaps.py`; the diagonal is 66.17 u):

| part | nearest part | gap |
|---|---|---|
| fin (dorsal blade) | Sniper turret (the hull is farther) | 1.47 u, 2.22% |
| Sniper turret | fin | 1.47 u, 2.22% |
| wing.L.1 / wing.R.1 | hull | 1.08 u, 1.63% |
| wing.*.2 | wing.*.1 | 1.15 u, 1.74% |
| wing.*.3 | wing.*.2 | 1.16 u, 1.76% |
| pod.L | wing.L.3 | 1.22 u, 1.84% |
| FastGun turret | pod.L | 1.36 u, 2.05% |
| pod.R / Bomb turret | each other | 1.12 u, 1.70% |
| engine.* / fluke.* | each other | 1.03 u, 1.55% |

The parent's `islands2.py` finds 77 islands, the Sparrow's count, and all of the 60 biggest are separate. Its
smallest gaps (0.1-0.3%) are pieces inside one part, the way the artist hulls' jet pieces sit at about 0.05%:
- each turret's ring and yoke;
- each pod and its running light;
- each engine case and its bell;
- the core's lenses and plates.

- **Slots**: 0 Domain, 1 Body, 2 Window, 3 Engine.
  - Polygons: 1994 / 6136 / 350 / 494.
  - Surface share (style_metrics): domain 0.20, window 0.024, engine 0.011.
- **Shape keys**, as travel in % of the 66.2 u diagonal:

| key | meaning | what moves | travel |
|---|---|---|---|
| Charge | weapon | the three housings, the cradle and the scope grow 2x along the gun and 1.15x across; the barrels, coils, muzzle parts, drum, pins and missile tubes swell; the base rings widen. No tip moves | 4.6% |
| Mass | volume | the core fattens: 1.2x wide, the belly 1.62x deeper, and the back only 1.12x, so the blade's hover survives; plus a 0.25 u inflate. The blade, plates, pods, engines and flukes thicken. The outrigger chain rides out with the flank (1.2 / 0.8 / 0.4 u), so its gaps stay open | 4.3% |
| Space | reach | the railgun barrel telescopes 1.35x; the fast gun's barrels 1.35x; the mortar drum 1.3x; the flukes reach 1.3x out from their roots; the pods telescope fore and aft about the pivot | 9.3% |
| Time | speed | the nose stretches and sharpens; the flukes sweep back from their roots; the outrigger chain bows aft (its ends fixed); the pod tails stretch aft | 5.6% |

  - No morph moves a turret pivot or a jet `.mouth`.
  - **Space moves the muzzle tips**, which the bones do not follow. At 100%: the sniper brake +6.1 u forward along the
    barrel, the fast gun ring +1.7 u, the bomb lip +1.0 u. A muzzle that must sit on the drawn tip adds
    `aim * offset * spaceWeight`.

- **Takes**: none drive the model. Four documentation poses for the sheet and the studio, each direction-proved by
  `pose_expect` on the re-import:
  - `pitchup`: the flukes and engines tip and drop.
  - `yawright`: they swing to the outside of the turn.
  - A hovering part hinges on nothing, so these poses turn AND move each part.
  - `aimright`: every turret yaws 40° to the pilot's right.
  - `aimup`: every turret pitches 25° up.
- **The Sparrow's Animator** stays on the hidden Sparrow bones. The convention's nine input takes are needed only by an
  Animator-driven model.

## Measured (`fbx_probe.py`, Unity units)

| | Orca | the Sparrow mesh Orca.prefab draws today |
|---|---|---|
| span (x) | **49.34** (-24.57 .. 24.77, turret tips) | 12.50 |
| length (z) | **37.05** (-18.73 fluke tips .. +18.32 sniper muzzle; nose at +16.0) | 12.23 |
| height (y) | **23.89** (-4.01 belly .. +19.89 sniper scope) | 1.78 |
| triangles | 16,280 (8,292 verts) | 25,984 |

- The core alone runs from the nose at z +16 to the tail stock at -12.6, is 11.2 wide and 8.9 deep. The engine section floats behind it, to -18.7.
- The Sparrow hit box, skimmer and camera (-50) that `Orca.prefab` still carries were sized to a 12-unit hull. Those
  belong to the prefab wiring.
- 16.3k triangles is above the convention's inferred 14k ceiling (FLEET_CONVENTION 1.4), and below the Sparrow's and
  Dolphin's 25-26k. The three bespoke turrets cost about 7k of it.

`build.py orca --check` reports OK. All three readers pass: the codec, assimp and the Blender re-import.
`measure_vessel_models.py --only Orca` finds 4 labelled element shapes, all 4 real. `vessel_colors.py --check` is OK.

`style_metrics.py --fleet` places it at:
- smooth 0.58, hard 0.16;
- domain 0.20, window 0.024, engine 0.011;
- 30 domain regions, 77 shells.

All of these are inside the artist band. The metric is informative only.

## What the sheet shows

Sheets: `Docs/VesselModels/sheets/orca_after.jpg`, plus close-ups in `sheets/orca_turrets.jpg` (the three turrets, the rear, the underside).

- **Chase:** a hovering assembly. The dorsal blade floats over the back with the railgun over its tip. Three gold-panelled
  plates step out to a gun pod on each side, like the Sparrow's wing pieces. Two big glowing engine pods float between
  the two fluke blades. The blade's trailing-edge trim and rank band, the saddle oval and the flank sweeps all read.
- **Front and side:** an orca: a melon head with the two eye-patch lenses, a domain chin and belly, and the tall raked
  fin.
- **Rounds:**
  1. Round 1: a blimp under a pole. The eye patch was painted faces (blocky), the flank band read as a hoop, and the
     turrets were too small.
  2. Rounds 2-4: the turrets were scaled 1.35x about their pivots, the eye patches became lenses, the outriggers became
     paddles, and the hull grew to 26 u nose-to-peduncle so the pylon stopped reading as a neck.
  3. Round 5: the painted domain became conformal plates with smooth outlines.
  4. Round 6: a domain trim line up the pylon's trailing edge, so the fin reads from behind.
  5. Round 7, the owner's first flight: the parts were mated, so the hull was rebuilt as a hovering assembly.
     - The pylon became a blade hovering over the back.
     - Each outrigger became a chain of three floating plates.
     - The pedestals and ring seats went, so the turrets hover over the fin tip and the pods.
     - The engine section floats.
     - First look: thin panels on the plates read as a dashed line. Larger panels made them read as plates.

## Open

- **Integration (the parent session):** `Orca.prefab` nests this file and `OrcaHullRig` drives the turret bones.
  - Its floating-part list names `fin`, `wing.*.1..2`, `engine.*` and `fluke.*`.
  - Round 2 adds `wing.L.3` / `wing.R.3` (the plate next to each pod) and `pod.L` / `pod.R`. A part the list does not
    name stays at rest.
  - The hit box, skimmer and camera still need re-fitting to the hull (FLEET_CONVENTION 6: length 34 u gives a camera
    distance of about 113 by the 0.3 rule; the turret span is 49 u).
- **Card images:** `render_cards.py` has no Orca entry yet, so the class card is not rendered from this file.
- **The owner's flight is the result.**
