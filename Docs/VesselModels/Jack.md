# Jack: the herders of Marlin and Jacks

> A NEW body (2026-10-21), built by `/vessel-model` from `Tools/Build/vessel_model/specs/jack.py`, which started from
> the Wrasse kite's spec so one swim drives both (`WrasseCrewBodies`, `MarlinCrewBodies`). There is no artist file.
> Until now the jacks were rings and dots (`R_VesselActions/MARLIN_JACKS.md` §5).

## Brief

Garrett, 2026-10-21: "lets give it some models and get it ready to ship". Two crevalle-jack craft run by the player's
thumbs, seen mostly from straight above in the jacks' view:

| clade | ship |
|---|---|
| deep, compressed body | a short, tall gunmetal hull lofted on a flexible spine (`spine.1` -> `spine.2`) |
| scutes | **the domain scute line** along each rear flank, a domain blaze on the head, and a **saddle** on the back so the top-down view finds the team colour |
| blunt head, big eye | a sensor eye in a bezel per cheek, a small canopy, glowing gill vents |
| sickle dorsal / anal | two control vanes, domain on their tall front lobes |
| pectoral fins | two long sickle vanes (`fin.L` / `fin.R`) |
| forked tail | **the forked vane** (`tail`), domain lobe tips, behind two small engines |

## Rig contract

- **File**: `Assets/_Models/Vessel Models/Jack.fbx`, guid `3a8f1c6e2b5d4e79a0c4f7b1e9d2a658`. The `.meta` is Grouper's
  with the guid changed. Preset `zup`.
- **Nodes**: mesh `mesh` and armature `rig`; root bone `hull`.
- **Bones**: the kite's: `hull`; `spine.1` -> `spine.2` -> `tail`; `fin.L` / `fin.R`. The body and its two vanes are
  weighted along the spine; every other part is rigid.
- **Size**: 16 units snout to fork, centred on its origin. Two instances in `Marlin.prefab` ('Jack A', 'Jack B'),
  scaled by `MarlinCrewBodies.jackModelScale` (2.5: 40 units long).
- **Shape keys**: Charge (the head juts, the dorsal lobe stands) 4.2%; Mass (the body deepens) 10.3%; Space (the fins
  reach, the fork spreads) 12.7%; Time (the vanes sweep, the fork stretches) 10.7%.
- **Takes**: three documentation poses, `beatleft` / `beatright`, `flare`.

## Measured

`build.py jack --check` reports OK; all three readers pass. `style_metrics.py --gate`: smooth 0.70, hard 0.13,
domain 0.15, window 0.020, engine 0.006, 11 domain regions, 14.0 tris per unit area. Smooth sits at the band's top
(0.693); round 3 traded body density against it.

## What the sheet shows (`Docs/VesselModels/sheets/jack_after.jpg`)

- Side: a deep jack with a forked tail and the scute line. Top: a slim dart with the domain saddle.
- Rounds: round 1 was too slim from above (a line in the top-down view) and the scutes did not show; round 2 widened the
  body 1.6× and the scute band; round 3 added the saddle.
