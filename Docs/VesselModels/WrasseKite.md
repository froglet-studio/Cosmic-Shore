# Wrasse Kite: the navigator's craft

> A NEW body (2026-10-20), built by `/vessel-model` from `Tools/Build/vessel_model/specs/wrassekite.py`
> to the fleet convention (`.claude/skills/vessel-model/references/FLEET_CONVENTION.md` 1-4). There is no
> artist file. It is the kite the Wrasse's navigator slings round the Grouper on its invisible rope
> (`Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md`).

## Brief

Garrett, 2026-10-20: "give the wrasse and grouper proper 3d vessels with animations". The craft is inspired by the
bluestreak cleaner wrasse and is never the animal itself:

| clade | ship |
|---|---|
| slender body | a long, narrow gunmetal hull lofted on 41 stations over a flexible spine (`spine.1` → `spine.2`), so the craft swims as it is slung |
| one bold lateral stripe | **the domain stripe**: one band per flank from snout to tail, widening aft as the bluestreak's does, sunk in a dark frame |
| pointed snout | **sensor probe**: a needle mast ahead of the nose with a glowing (Window) sensor tip |
| eye | a sensor eye in a bezel set IN the stripe on each cheek, plus a small cockpit canopy on the back |
| gills | three glowing vent slots per side behind the eye |
| long dorsal / anal fins | two long, low **control vanes** along the back and the belly. Each ramps in from its front, eases off aft and carries a domain edge trim |
| pectoral fins | two small control vanes on hinge fairings (`fin.L` / `fin.R`), with domain tips |
| small fan tail | **vectoring fan** (`tail`) with a sunk domain panel, behind two small engines whose glowing mouths flank the peduncle |

## Rig contract

- **File**: `Assets/_Models/Vessel Models/WrasseKite.fbx`, guid `c56064d56acc061a124729744a5966fa`.
  - The `.meta` is Thresher's with the guid changed: the same four-slot remap.
  - Preset `zup`: the nose lands at Unity +Z and up at +Y with the model root at identity.
- **Nodes**: mesh `mesh` and armature `rig`; root bone `hull`.
- **Bones and rest heads** in Unity space (x right, y up, z forward, root-relative):

| bone | parent | head (Unity) | skin |
|---|---|---|---|
| `hull` | — | (0, 0, 0) | the head parts (probe, eyes, canopy, gills, fairings) are rigid on it, and the body's front is blended onto it |
| `spine.1` | hull | (0, 0, 1.0) | blended |
| `spine.2` | spine.1 | (0, 0, -2.4) | blended; the two engines are rigid on it |
| `tail` | spine.2 | (0, 0, -5.5) | the fan is rigid on it; the peduncle is blended |
| `fin.L` | hull | (-0.50, -0.22, 3.40) | rigid |
| `fin.R` | hull | (0.50, -0.22, 3.40) | rigid |

- **Spine weights**: the body and its two vanes are weighted by smoothstep hat functions along the length. The peaks sit
  at Blender y −1.6 (hull), 0.7 (spine.1), 3.9 (spine.2) and 6.1 (tail), which is Unity z +1.6, −0.7, −3.9 and −6.1.
- **Slots**: 0 Domain, 1 Body, 2 Window, 3 Engine. Polygons per slot: 718 / 2599 / 384 / 264.
- **Shape keys**, as travel in % of the 16.5 diagonal:

| key | meaning | what moves | travel |
|---|---|---|---|
| Charge | weapon | the probe extends 0.95; the dorsal vane stands 1.9× | 5.8% |
| Mass | volume | the body and its vanes deepen 1.55×; the engines swell; the fins ride out | 4.2% |
| Space | reach | the pectoral vanes reach 1.75×; the fan spreads 1.65×; the anal vane stands taller | 10.5% |
| Time | speed | the vanes sweep back; the body sleeks 6% longer; the fan stretches | 6.0% |

- **Takes**: none drive it yet. Three documentation poses, each held by `pose_expect`:
  - `beatleft` / `beatright`: the spine swims, 7° / 11° / 16° down the chain.
  - `flare`: the pectoral vanes brake down 40°.

## Measured (`fbx_probe.py`, Unity units)

| | |
|---|---|
| length (z) | **16.04** (-8.00 fan tip .. +8.04 probe tip), centred on its origin |
| span (x) | 2.84 (the pectoral vanes) |
| height (y) | 2.32 (-1.02 .. +1.30) |
| triangles | 7,112 (3,606 verts) |

`build.py wrassekite --check` reports OK. All three readers pass: the codec, assimp (7,112 faces, 3 animations,
4 materials) and the Blender re-import.

`style_metrics.py --fleet` places it at:
- smooth 0.66, hard 0.14;
- domain 0.24, window 0.035, engine 0.010;
- 8 domain regions.

Every value is inside the artist band.

## What the sheet shows (`Docs/VesselModels/sheets/wrassekite_after.jpg`)

- **Overall:** a slim dart with the needle probe ahead.
- **Side:** one bold domain band runs snout to tail.
- **Tail:** a broad domain fan, with the engine mouths glowing either side of the peduncle.
- **Swim poses:** the beat poses bend the whole body smoothly along the blended spine, with no hinge kink.
- **Rounds:**
  - Round 1: the dorsal vane read as a flat plank, and the stripe was too thin to see.
  - Round 2: the vanes ramp in and taper, and the stripe was widened.

## Open

- **Integrated by name** (2026-10-20): nested under the Wrasse root as `WrasseKite`; `WrasseCrewBodies` places it where
  the kite flies (replicated, so every peer sees it), swims the spine from the kite's speed and flaps the fins.
- The owner's look is the result.
