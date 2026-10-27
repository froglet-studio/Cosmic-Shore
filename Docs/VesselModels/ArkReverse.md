# Ark, concept B (the valve facing aft): model dossier

`ArkReverse.fbx` is built from `Tools/Build/vessel_model/specs/ark_reverse.py`. Its parts are shared with concept A ([Ark.md](Ark.md), `Ark.fbx`) through `specs/_ark_kit.py`. It's a new hull with no artist file, and no prefab draws it yet.

## The brief, round by round (Garrett, 2026-10-27)
1. "Explore an alternative where the arc is facing the other way. Get re-inspired from the arc clam shell." So the hinge is the PROW and the fan opens AFT: a swept delta with the bridge at its point.
2. Engines with clearance to animate their manoeuvres; proper stellated octahedra the hull is designed around, distributed to protect the other prisms from explosions; shielded prisms as octahedra; danger prisms where the Ark wants to be dangerous; a hangar the escorts launch from; "it should feel big ... like its own level ... a kaiju protected by three vessels and its own regeneration"; corridors, channels, ducts, curving passageways.
3. "Less like a darthvader panel meets a child's toy ... menacing, intimidating ... more torn like cyberpunk ... a touch of whimsy, but the current placement of the prisms looks like it has stars and buttons all over it ... more wickedness."

## Pictures
- `ArkReverse_closeups.jpg`: rendered in the game's colours. The hull uses `vessel_colors.SCHEME` (gunmetal hull, sapphire glass, Engine as an ember); the domain is Ruby's `ShipColor2` (violet). The prisms are PREVIEWS placed at the socket bones, in Ruby's block colours with Fire for danger: the game's own shapes (stellated octahedra, octahedra, slabs) at their real sizes. They are not part of the model.
- `ArkReverse_sheet.png`: the contact sheet (studio colours): whole-ship views, the four element shapes, four of the takes.

## Scale
**196 from nose to jets** (half a standard nucleus radius, 392). The nose is the core's rim ahead of the bridge. The hangar's frame and lower fangs stand about 2 proud of it. It is 231 wide and about 107 tall from the keel to the crown's tallest spike. Around 140k tris.

## The prisms are the game's shapes, and the hull is designed around them
A shielded prism settles into an octahedron with its vertices at 3x the prism's half-extents (`OctahedronMeshGenerator`, `CIRCUMSCRIBING_SCALE` 3). A supershielded prism settles into the stella octangula, with spike tips at the cube corners 3x out (`StellatedOctahedronMeshGenerator`). The Ark's shield sockets hold CUBIC prisms, so their shields are the regular octahedron and the regular stellated octahedron:

| Tier | Prism (half-extent) | Shield | Where | Count |
|---|---|---|---|---|
| supershielded | cube 2.2 | stellation: spikes 6.6 out, 13.2 across | **clutched and sunk** where each groove crosses a growth seam (the inner and middle seams, alternating groove by groove, so they never form a row); only the upper spikes rise from the socket, gripped by four hooked talons glowing at the tips. One in each pincer, two in the prow's cheeks. These are the firewalls between the groove cells. | 11 |
| shielded | cube 1.6 | octahedron: vertices 4.8 out | **the brood**: in the dark of the mantle cavity under each plate, on glowing round cups, seen through the stern maw, the grooves and the rips | 16 |
| prism | slab 3.6 x 1.6 x 9 | (slab) | **gills**: standing in the grooves, raked aft, on rails (the scallop's gill lamellae). From above they read as rows of thin fins, not tiles. | 70 |
| danger | slab 3.6 x 1.6 x 9 | (slab) | **teeth**: fangs hanging from the plates' lip and rising from the core's rim (the stern maw into the cavity), saw-blades raked back along the swept leading edges, fangs at the hangar's jaw | 26 |

Every shield socket is the prism's own negative. The hole is the shield solid grown by a 1.3 clearance and boolean-subtracted, with its walls painted. The cradle under it is a key block with the same solid subtracted: an X of bars for a stellation, whose underside is the X of two tetrahedron edges, or a round cup for an octahedron. So its glowing walls hold the prism without touching it.

The socket bones are `socket.<tier>.<nn>`: the head at the prism's centre, the tail along it, and Z = the prism's up. Their parent is `hull`, or the pincer for a pincer's stellation. Spawn the prism at the bone, at the half-extent in the table, and it sits in its socket. Each cradle is skinned to its own bone, so Unity's `optimizeBones` keeps the bone.

## Menace (round 4)
- **A face**: two slit eyes (the cockpit's glass), each a wedge that starts as a point by the nose and opens outboard. Over each eye is an armoured brow that juts out over the glass, and the two brows meet in a V. A crown of raked spikes with a beacon. Below, the hangar's glowing maw with fangs in its jaw. The shell's corrugations climb the dome's flanks and back.
- **Torn armour**: the groove edges and the leading edges are ripped back in steps. Five plates are torn open over exposed girders, their rips glowing hot, with cables hanging from them.
- **Pincers**: the ears are now heavy forward pincers either side of the bridge, with teeth along their inner edges and a launch slot low in each jaw.
- Faceted octagonal jets and gimbal rings, an octagonal reactor cage, a serrated keel, barbs raked down along the rim, cables slung between the lift pylons.
- Domain colour is reduced to thin neon seams. The rim's windows are sparse red slits.
- **The whimsy**: three pairs of blue eyes under slanted brows peer out of the cavity's dark. And the omni crystal.

## The level
- **The mantle cavity**: the shell floats 14 above the core, making a pillared hall. You fly in through the stern maw (between its fangs) or the open front flanks, past the lift pylons, the gills' rails, the brood and the stellations' X-cradles.
- **The hangar**: under the bridge, with clam-jaw doors (`hangar.door.T/B`) and three launch pads for the three escorts (`hangar.slot.0..2`: the head on the pad, +3 up, the tail toward the door), plus arched frames and a glowing back wall.
- **Two curving ducts** run from the flank walls to the stern under the cavity floor, ringed with octagonal frames and lights.
- **The promenade** is recessed round the core's rim. The parapets run along the leading edges. The belly carries radial ribs (the lower valve).

## Engines that manoeuvre
Each jet pivots 6 behind its intake, inside an octagonal ring (`engine.<k>.ring`, the yaw axis; the jet pitches about the ring's trunnions) with at least 2.3 of clearance. The takes vector the thrust the way the manoeuvre needs, in the fleet convention:
- `pitchup` / `pitchdown` tilt every nozzle up or down.
- `yawleft` / `yawright` swing every ring.
- `rollleft` / `rollright` tilt the two sides opposite ways.
- `speedup` draws the splayed jets in toward straight aft.
- `speeddown` fans them out (an air brake).

Also `release` (the lance slides 20 aft) and `hangaropen` (the doors swing out, hinged on their outer faces). All are single-frame poses, with `rest` first.

## Shapes
- **Charge:** the lance lengthens and the crystal swells.
- **Mass:** the keel deepens and the jets fatten, staying inside their rings.
- **Space:** the pincers' tips reach and the crown's spikes rise.
- **Time:** the jets stretch.

None of them moves a plate, a cradle, a socket bone or a hole.

## Gates
- `build.py ark_reverse` (codec + assimp + re-import) and `build.py ark_reverse --check` (repeatable: the kit puts every boolean's inputs in a canonical order, so two builds cut the same mesh).
- `assembly_report.py --floating shell.0..7,umbo,auricle.L,auricle.R,omni.ring,omni,payload,keel,hangar.door.T,hangar.door.B,engine.0..7,engine.0..7.ring`
- **`pose_clearance.py ArkReverse.fbx --parts "engine.*,payload,hangar.door.*" --takes all`**: the new reader that plays every take and fails on a part that comes within 0.4 % of the diagonal of a neighbour in any pose. It found the rings brushing the nacelles' bands, the lance cradle under the tail fins, and the lower door dipping to the bay floor, and all three were fixed.
