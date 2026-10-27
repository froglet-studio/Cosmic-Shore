# Ark, concept B (the valve facing aft): model dossier

Built 2026-10-27 by `/vessel-model` from `Tools/Build/vessel_model/specs/ark_reverse.py`. Garrett asked for it: "this is one concept. now lets explore an alternative where the arc is facing the other way. get re-inspired from the arc clam shell. and have it face the opposite direction."

Concept A is [Ark.md](Ark.md) (`Ark.fbx`): the hinge is aft and the fan opens forward to the nose. Here the **hinge is the prow and the fan opens aft**, so the shell is a swept delta with the bridge at its point. It's a new hull with no artist file, and no prefab draws it yet.

- `ArkReverse_sheet.png`: the contact sheet.
- `ArkReverse_closeups.jpg`: close-ups as a passing vessel sees them (whole, bridge, jets, flank, reactor, underside). The coloured prisms in these renders are *previews* placed at the socket bones, not part of the model: gold is supershielded, white shielded, blue plain, red danger.

## Scale
**196 from nose to jets** (half a standard nucleus radius). The nose is the core's rim ahead of the bridge; the jets are the two centre mouths. It is 231 wide and 88 tall to the reactor pylons' tips. 108k tris.

## What the scallop gave it, beyond the turn-around
| Part | Bone | What it is |
|---|---|---|
| Bridge dome | `umbo` | Where the ribs converge, now the PROW. It carries a seven-pane cockpit windshield looking straight ahead (domain brow and sill), crown panels, flank portholes, and a sensor mast with a beacon and a dish. |
| Canard hangars | `auricle.L` / `.R` | The ears, flanking the bridge. Each has a long lit LAUNCH SLOT in its leading edge (fighters leave forward), a thruster aft and a comm mast. |
| Shell | `shell.0`-`shell.7` | Eight rib plates fanning aft: riblets, three shingled tiles, sunken panels, a domain lip band and crown stripe, lip running lights. |
| Jets | `engine.0`-`engine.7` | One floating jet behind each rib, splayed with the fan. The corrugations end in thrust. |
| Mantle eyes | `hull` | A scallop has a row of blue eyes along its mantle edge. Here a blue sensor eye on a domain stalk peeks from the end of each groove, between the jets. |
| Leading edges | (sockets) | The delta's swept edges, lined with danger prisms on the core's flank margin. |
| Omni reactor | `omni.ring`, `omni` | The dormant omni crystal (Window = inactive glass) in its gimballed cage: two rings, four rising pylons with glowing tips, and a glowing collar. It hovers over the court behind the bridge, where every rib converges, and is the beacon the chase camera sees over the whole fan. |
| Payload | `payload`, `payload.dock` | A stinger lance lying in the centre groove and pointing aft, hovering over three regen cradles. Its glowing head sits between the two centre jets, so players grab it from behind. |
| Core, keel | `hull`, `keel` | As concept A: domain band, portholes, an observation gallery under the bridge, ventral panels, a domain ring, lights, sensor domes, a keel fin. |

It is a hovering assembly: `assembly_report.py --floating shell.0..7,umbo,auricle.L,auricle.R,omni.ring,omni,payload,engine.0..7,keel` passes.

## Prism sockets
These are the same cradles and bone contract as concept A (`_ark_kit.Kit.cradle`). Each socket has a bone named `socket.<tier>.<nn>` with its head at the prism's centre, its tail along it, and Z = up. The prism is 4 x 1.6 x 9.

| Tier | Where | Count |
|---|---|---|
| supershielded | the two innermost groove rows, nearest the reactor | 14 |
| shielded | the two middle rows (the centre groove keeps its first) | 13 |
| prism | the two outer rows, by the jets | 12 |
| danger | along the two swept leading edges, four per side | 8 |

## Shapes and takes
- **Charge:** the lance lengthens aft and the crystal swells.
- **Mass:** the belly deepens (the keel rides it) and the jets fatten.
- **Space:** the canard hangars slide outward and the masts telescope.
- **Time:** the jets stretch.

No shape moves a plate, a cradle or a socket bone.

**Takes:** `rest`, `clap` (the trailing lip lifts 10 degrees about the hinge), `release` (the lance slides 20 aft).
