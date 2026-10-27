# Ark (mothership): model dossier

Built 2026-10-27 by `/vessel-model` from `Tools/Build/vessel_model/specs/ark.py`, at Garrett's request.
Round 2 (the same day) rebuilt it at true scale and added detail. This is **concept A**: the clam valve opening forward. **Concept B**, the valve facing aft, is [ArkReverse.md](ArkReverse.md). The two share their cradle, lance, nacelle and reactor builders (`specs/_ark_kit.py`). It's a new hull with no artist file, and no prefab draws it yet.

- `Ark_sheet.png`: the contact sheet (whole-ship views, the four element shapes, the poses).
- `Ark_closeups.jpg`: close-ups as a passing vessel sees them (bridge, lip, aft, underside, flank). The coloured prisms in these renders are *previews* placed at the socket bones. They are not part of the model: gold is supershielded, white shielded, blue plain, red danger.

## Scale
**196 units from nose to jets**: half a standard nucleus radius (392; `Nucleus.prefab` at scale 400). The nose is the core's rim, just past the shell's lip, and the lance's head reaches the same line. Overall it is 232 wide and 81 tall, about 18 Squirrels long. 97k tris.

## What it is
| Part | Bone | What it is |
|---|---|---|
| Shell | `shell.0`-`shell.7` | One scallop valve. Eight crowned rib plates with three riblets each, split into three shingled armour tiles (the scallop's growth rings). Each tile carries sunken panels. Domain colour runs along the lip band and down the crown of the outer tiles, with running lights at the lip corners. The plates float with a constant 8.4-unit groove between neighbours. |
| Bridge dome | `umbo` | The smooth dome where every rib converges, carrying the **cockpit windshield**: seven panes with mullions, a domain brow and sill. It also has hull panels on its crown, portholes round its flanks, a domain rim, and a sensor mast with a beacon and a dish. |
| Hangar wings | `auricle.L` / `.R` | The scallop's ears: ribbed flanges with a lit hangar mouth (Window) on each outer end, a manoeuvring thruster aft and a comm mast. |
| Core | `hull` | A lens hull under the plates. Its rim wall carries a domain band, a row of portholes and an observation gallery at the prow. Underneath are ventral panels, a domain ring, running lights and two sensor domes. |
| Keel | `keel` | A ventral fin floating under the core, with a domain stripe and running lights. |
| Engines | `engine.L/C/R` | Three floating nacelles: intake lips, domain bands, six cooling fins, and glowing bells with inner cones. |
| Omni reactor | `omni.ring`, `omni` | The dormant omni crystal (Window = inactive glass) floating in a gimballed cage over the engines. The cage has two rings, four containment pylons rising round the crystal with glowing tips, and a glowing collar. Swap the crystal's material to activate it. (Until 2026-10-27 the pylons hung below the crystal; the fix is in `_ark_kit.Kit.reactor`.) |
| Payload | `payload`, `payload.dock` | A lance lying in the centre groove, hovering 1.4 units over three cradles that have regen pads. Its glowing head reaches the nose. |

It is a hovering assembly: `assembly_report.py --floating shell.0..7,umbo,auricle.L,auricle.R,omni.ring,omni,payload,engine.L,engine.C,engine.R,keel` passes.

## Prism sockets (the ark's health)
Each socket has one bone, named `socket.<tier>.<nn>`. The bone's head is at the socketed prism's centre, its tail runs along the prism's length, and its Z (roll) is the surface's up. To regenerate a prism, spawn it at the bone scaled to **4 x 1.6 x 9** (across x up x along). Its top then lies flush with the rib edges.

Each cradle is a U channel: a floor, domain side rails, end stops, two Engine-slot regen pads and a pedestal. The cradle is skinned to its socket bone, so Unity keeps the bone.

| Tier | Where | Count |
|---|---|---|
| supershielded | the two innermost groove rows, nearest the bridge | 14 |
| shielded | the two middle rows | 12 |
| prism | the two outer rows | 12 |
| danger | on the core's margin below the lip, two per plate | 16 |

The payload regrows at the `payload` bone's rest position relative to `payload.dock`.

## Shapes and takes
- **Charge:** the lance lengthens and the crystal swells.
- **Mass:** the belly deepens (the keel rides it) and the nacelles fatten.
- **Space:** the hangar wings slide outward and the masts telescope.
- **Time:** the nacelles stretch back.

No shape moves a plate, a cradle or a socket bone, so a prism spawned on a socket always sits in its cradle.

**Takes:** `rest`, `clap` (the shell lifts its lip 10 degrees about the hinge), `release` (the lance slides 20 units forward out of its cradles).
