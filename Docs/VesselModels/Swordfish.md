# Swordfish: a swordfish ship whose sword is its bill

> A NEW hull (2026-11-08), built by `/vessel-model` from `Tools/Build/vessel_model/specs/swordfish.py` on the RHINO
> RIG'S names (PIPELINE "The borrowed-rig path"), so `Swordfish.prefab` (a Rhino clone) binds it by a guid swap alone
> and `RhinoAnimation` keeps driving it. Brief and controls: `Assets/_Scripts/Controller/Vessel/R_VesselActions/SWORDFISH.md`.

## Brief

Garrett, 2026-11-08: "the swordfish should look like a swordfish (and a space ship of course) ... its sword should be
the sword of the sword fish". The bill is not in this file: it is the blade (`SwordfishBill.asset`,
`Tools/Build/author_swordfish_bill.py`), which rests straight ahead with its hilt in this hull's flat snout.

| clade | ship |
|---|---|
| bill | **the sword**: the blade's mesh, seated in a flat snout as wide as its root (3.1 x 0.9) |
| robust torpedo body | a deep, round gunmetal hull, deepest a third back, tapering to a slim peduncle |
| dark back | a domain stripe down the dorsal ridge, nape to peduncle |
| huge eye | a big sensor in a bezel on each cheek; a glass canopy on the forehead |
| gill slits | three glowing vents behind each eye |
| short lower jaw | a floating chin plate under the bill's root |
| first dorsal | the tall rigid sickle crest, a sunk domain panel in it |
| pectoral fins | long stiff sickles: an inner plate (`wing1.*`) and a floating swept tip in the domain (`wing2.*`) |
| speed | thruster pods slung over the pectorals (`jet.*`, on the Rhino pods' axes), mouths glowing aft |
| anal fin, finlets | a falcate anal fin, a small second dorsal and second anal |
| peduncle keels | one broad keel each side of the tail root |
| lunate tail | a big crescent floating behind the peduncle on a hub, horns in the domain |

## Rig contract

- **File**: `Assets/_Models/Vessel Models/Swordfish.fbx`, guid `f3c4c43042fe49b81e83e80d69711a59`; the `.meta` is the
  Rhino rig's with that guid (the same slot remap). Preset `yup`.
- **Names (the Rhino rig's, unchanged)**: armature `rhino`, mesh node `fusalage.002`, geometry `Circle.004`, twelve
  bones at the Rhino rig's rest (`rest_compare.py` 0.0000 on both readers), slots `accent.005, BASE.005,
  windsheild.005, N.005`, shapes `mass, charge, space, time`, the nine `rhino|rhino_*` takes plus a leading `rest`.
- **In the prefab**: nested under `Rhino_Test (1)` at z -1.5545, exactly where the Rhino rig was. The wing and engine
  parts keep their colliders and jets; the bones follow them (`RhinoAnimation.FollowParts`).
- **Shape keys** (travel in % of the diagonal): mass 4.4 (the body deepens, the pods swell), charge 6.6 (the crest
  stands taller, the snout socket widens), space 7.4 (the pectorals reach, the crescent spreads), time 8.9 (the crest
  rakes, the pectorals sweep, the crescent rakes).

## Measured

| | |
|---|---|
| length (Blender y) | 23.1, snout to tail fork (plus the 48-80 unit bill) |
| span (x) | 20.9 across the pectoral tips |
| height (z) | 14.6, crest tip to lower tail horn |
| triangles | 7,712 |

`build.py swordfish --check` reports OK; three readers pass. Round 1's domain covered the whole upper half of the
body (a superellipse band threshold of 0.72 reaches far down the flank); round 2 keeps it to a stripe along the ridge.

## Open

- The owner's flight is the result. The class card and hangar art still show the Rhino (`render_cards.py` does not
  register the Swordfish yet). Crystal hull fusion reads the Rhino's bakes as stale until the Swordfish has its own.
