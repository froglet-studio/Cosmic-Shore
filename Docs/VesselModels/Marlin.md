# Marlin: saved for a swordsman (was the hull of Marlin and Jacks)

> **Not drawn by any prefab since 2026-10-21.** Garrett reduced Marlin and Jacks to the Jacks ("save the marlin as a
> proper swordsmen, not overshadowed by the jacks"; `R_VesselActions/JACKS.md`). The model and its spec stay for a
> rapier vessel of its own, which takes a new class id. Everything below describes the model as it was built and drawn.

> A NEW hull (2026-10-21), built by `/vessel-model` from `Tools/Build/vessel_model/specs/marlin.py` to the fleet
> convention (`.claude/skills/vessel-model/references/FLEET_CONVENTION.md` 1-4). There is no artist file. It replaces
> the Rhino rig that `Marlin.prefab` drew (`Assets/_Scripts/Controller/Vessel/R_VesselActions/JACKS.md` §5).

## Brief

Garrett, 2026-10-21: "lets give it some models and get it ready to ship". A spaceship inspired by a marlin, flown on the
Rhino's rig (two sticks and the ramp):

| clade | ship |
|---|---|
| bill | **the spear**: a long tapering ram ahead of the nose, a domain collar at its root |
| torpedo body | a deep gunmetal hull, widest behind the head, tapering to a slim tail boom |
| vertical bars | **domain bars**: eight sunk bands down each flank (the striped marlin's) |
| sail | **the sail** (`sail`), a tall plate with a sunk domain panel. It stands up while the marlin calls the herd and folds back at boost |
| eye | a sensor eye in a bezel per cheek, a glass canopy, three glowing gill vents |
| pectoral fins | long sickle fins (`fin.L` / `fin.R`) with domain tips. They follow the Rhino's front plates |
| engines | two nacelles flanking the rear body, glowing mouths aft |
| caudal keels | two keels on the tail boom (`tail.1`) |
| lunate tail | **the crescent** (`tail.2`), its horns in the domain |

## Rig contract

- **File**: `Assets/_Models/Vessel Models/Marlin.fbx`, guid `6d2b8e4f0a9c4e17b3f5a8c1d2e7b904`.
  - The `.meta` is Grouper's with the guid changed (the same four-slot remap).
  - Preset `zup`: the nose is Unity +Z.
- **Nodes**: mesh `mesh` and armature `rig`; root bone `hull`.
- **Bones**: `hull`; `sail`; `fin.L` / `fin.R`; `tail.1` -> `tail.2`. Rigid skinning, one bone per part.
- **In the prefab**: nested under `Rhino_Test (1)` (the fuselage part `RhinoAnimation` turns) at local z -2, so the
  Rhino's five hull colliders still cover the body (the spear reaches about 4 units past them). The Rhino's rig stays,
  inactive. `MarlinCrewBodies` (retired 2026-10-21) posed the bones by name.
- **Slots**: 0 Domain, 1 Body, 2 Window, 3 Engine.
- **Shape keys**, as travel in % of the diagonal:

| key | meaning | what moves | travel |
|---|---|---|---|
| Charge | weapon | the spear lengthens 1.45×; the sail stands 1.4× taller | 7.7% |
| Mass | volume | the hull deepens 1.45×; the engines swell; the fins ride out | 5.8% |
| Space | reach | the fins reach 1.45× along their span; the crescent spreads | 7.6% |
| Time | speed | the sail folds down and back; the fins sweep; the crescent rakes | 9.2% |

- **Takes**: none drive it. Four documentation poses, each held by `pose_expect`: `beatleft` / `beatright`,
  `sailup`, `flare`.

## Measured

| | |
|---|---|
| length (z) | 22.9 (spear tip to crescent tip) |
| span (x) | 12.2 (the fins) |
| height (y) | 10.1 (keel to sail) |
| triangles | 6,780 |

`build.py marlin --check` reports OK. All three readers pass. `style_metrics.py --gate`: smooth 0.58, hard 0.14,
domain 0.20, window 0.028, engine 0.012, 23 domain regions; every value is inside the artist band.

## What the sheet shows (`Docs/VesselModels/sheets/marlin_after.jpg`)

- A billfish at a glance: the spear, the tall sail and the crescent read from the chase camera and from the side.
- Rounds: round 1's crescent carried an inset panel that folded over its concave outline (a hatched look). Round 2
  paints the horns in the domain instead.

## Open

- The owner's look is the result. Crystal hull fusion still bakes the Rhino's shape until a marlin bake is made.
