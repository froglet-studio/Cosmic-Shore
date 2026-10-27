# Ark, concept B (the valve facing aft): model dossier

`ArkReverse.fbx` is built from `Tools/Build/vessel_model/specs/ark_reverse.py`. Its parts are shared with concept A ([Ark.md](Ark.md), `Ark.fbx`) through `specs/_ark_kit.py`. It's a new hull with no artist file, and no prefab draws it yet.

## The brief, round by round (Garrett, 2026-10-27)
1. "Explore an alternative where the arc is facing the other way. Get re-inspired from the arc clam shell." So the hinge is the PROW and the fan opens AFT: a swept delta with the bridge at its point.
2. Engines with clearance to animate their manoeuvres; proper stellated octahedra the hull is designed around, distributed to protect the other prisms from explosions; shielded prisms as octahedra; danger prisms where the Ark wants to be dangerous; a hangar the escorts launch from; "it should feel big ... like its own level ... a kaiju protected by three vessels and its own regeneration"; corridors, channels, ducts, curving passageways.
3. "Less like a darthvader panel meets a child's toy ... menacing, intimidating ... more torn like cyberpunk ... a touch of whimsy, but the current placement of the prisms looks like it has stars and buttons all over it ... more wickedness."
4. "This is still a bubbly pokemon. Think hierarchical and heterarchical. Look at the dolphin and the details of its contours ... this ark should have 5 hierarchies that have 2 to 5 heterarchies each. It should look sophisticated like it belongs on the cover of a sci fi novel, not like megaknight turned into a toy spaceship."

## Round 5: the form language
The Dolphin's lesson is what its parts are made of:
- crisp faceted masses: a chamfered tube in stepped sections, flat jaw blades, floating swept wings;
- secondary forms on each mass: collars, steps, scalloped notches, ringed cut-outs;
- surface systems over those, running in parallel: contour traces that turn in 45° jogs and end in rings, chevron groups, dot rows;
- then micro detail.

Nothing is round for its own sake, nothing is a spike, and nothing stands on a pedestal.

Round 4's Ark was a dome on a lens with a crown on a plinth, a pergola of glowing pillars under the shell, barrel jets ringed in spikes, and fangs and eyes. Round 5 throws all of that out.

The silhouette is now LOW and LONG. A scallop is a jet-driven bivalve, so the Ark is a closed clam whose two valves gape only at the stern, and its jets fire out of that gape. It stands 69 tall, down from 84.

Five levels of scale, each with two to five parallel systems:

| Level | Scale | The parallel systems (heterarchies) |
|---|---|---|
| L1 silhouette | 100-230 | the **shell fan** of ridged rib blades · the **deck**, the lower valve: a cranked-arrow plate (lip arc, a straight swept leading edge, a chamfered prow) · the **bulwarks** that close the cavity under the shell's edges, so the valves meet in a shadow seam · the **umbo**, the citadel over the hinge where the ribs converge · the **jaws**, two blade mandibles reaching forward past the hangar mouth |
| L2 assemblies | 30-80 | the rib blades (shoulders, a raised crest, shingled tiles) · the umbo's tiers: a boss whose skirt is steep where the ribs meet it and battered (sloped armour) toward the prow, a citadel, and the bridge with its canted visor · the spine's sections stepped by collars · the bulwarks' walkways and glacis · the jet bank in the stern gape, each jet in an octagonal gimbal ring |
| L3 components | 8-25 | the prism sockets (below) · the reactor well with the omni crystal · the keel fin and the sponsons · the hangar mouth and its curving tunnel · the sensor array |
| L4 surface | 1-8 | inset armour plates · contour traces with jogs · vent grilles · port slits · the grooves continued as channels across the boss |
| L5 micro | 0.2-1.5 | trace terminals (a ringed cut-out on each jaw) · running lights · dot clusters |

Glow is kept to lines and slits:
- Domain is the thin neon seams.
- Windows are port slits and the bridge visor.
- Engine glow is the jets, the clamp pads and a few running lights.

The vents and the shell's lip are dark.

## Pictures
- `ArkReverse_closeups.jpg`: rendered in the game's colours. The hull uses `vessel_colors.SCHEME` (gunmetal hull, sapphire glass, Engine as an ember). The domain is Ruby's `ShipColor2` (violet). The prisms are PREVIEWS placed at the socket bones, in Ruby's block colours with Fire for danger. They are the game's own shapes (stellated octahedra, octahedra, slabs) at their real sizes, and they are not part of the model. The panels:
  - the cover three-quarter;
  - the bridge and citadel;
  - the underside;
  - the stern gape;
  - the prow and its mandibles;
  - plan, stern and side;
  - the hangar open;
  - the jets yawing.
- `ArkReverse_sheet.png`: the contact sheet (studio colours): whole-ship views, the four element shapes, four of the takes.

## Scale
**196 from nose to jets**, half a standard nucleus radius (392). The nose is the jaws' knuckles at y −98, and the build asserts nothing reaches ahead of them. The jets end at +98.

It is 216 wide, and 69 tall from the keel to the sensor masts. Around 145k tris.

## The prisms are the game's shapes, and the hull is designed around them
A shielded prism settles into an octahedron with its vertices at 3x the prism's half-extents (`OctahedronMeshGenerator`, `CIRCUMSCRIBING_SCALE` 3). A supershielded prism settles into the stella octangula, with spike tips at the cube corners 3x out (`StellatedOctahedronMeshGenerator`). The Ark's shield sockets hold CUBIC prisms, so their shields are the regular octahedron and the regular stellated octahedron:

| Tier | Prism (half-extent) | Shield | Where | Count |
|---|---|---|---|---|
| supershielded | cube 2.2 | stellation: spikes 6.6 out, 13.2 across | **sunk** where each groove crosses a growth seam (the inner and middle seams alternate groove by groove, so they never form a row), each gripped by four faceted clamp arms with glowing pads. They are the firewalls between the groove cells. | 7 |
| shielded | cube 1.6 | octahedron: vertices 4.8 out | **the brood**: two rows of octahedra in the dark of the mantle cavity under the plates, on glowing round cups, seen through the grooves and the stern gape | 16 |
| prism | slab 3.6 x 1.6 x 9 | (slab) | **gills**: raked aft on rails, SUNK in the grooves so their tops sit at the ribs' shoulders. From above they read as glowing fins down in the trenches. | 67 |
| danger | slab 3.6 x 1.6 x 9 | (slab) | each set FLAT in a slit cut to it, with its edge out, so it reads as a cutting edge rather than a tile: four blades along each swept leading edge (the edge that rams); two along each jaw's inner edge, biting across the launch channel; and a six-bar grille between the jets in the stern gape, edges aft | 18 |

Every shield socket is the prism's own negative. The hole is the shield solid grown by a 1.3 clearance and boolean-subtracted, with its walls painted. The cradle under it is a key block with the same solid subtracted: an X of bars for a stellation, or a round cup for an octahedron. So its glowing walls hold the prism without touching it.

A blade's slit in a FLOATING part (a jaw) clears its cradle by the assembly gap. In the hull (a bulwark) it is a tight slit.

The socket bones are `socket.<tier>.<nn>`: the head at the prism's centre, the tail along it, and Z = the prism's up. Their parent is `hull`, or the jaw for a jaw's blade. Spawn the prism at the bone, at the half-extent in the table, and it sits in its socket. Each cradle is skinned to its own bone, so Unity's `optimizeBones` keeps the bone.

## The level
- **The mantle cavity** is a pillared hall under the shell, closed at the sides by the bulwarks. You fly in through the stern gape between the jets and the grille, or look down into it through the grooves. Inside: the lift pylons, the gills' rails and their columns, the brood on their cups, and the stellations' X-cradles. Three pairs of blue eyes still peer out of the dark near the umbo: the whimsy.
- **The hangar** sits in the spine's prow, between the jaws and under the prow's tip. It has clam-jaw doors (`hangar.door.T/B`) and three launch pads for the three escorts (`hangar.slot.0..2`: the head on the pad, +3 up, the tail toward the door), plus arched frames, a glowing back wall, and a tunnel from the back wall that curves down out through the keel.
- **The bulwarks' tops** are walkways along the leading edges, a shadow seam below the shell, with running lights.
- **The reactor well** is an octagonal shaft through the umbo behind the bridge, with glowing ribs down its walls. The dormant omni crystal hovers in it on a collar and hoop (`omni.ring`, `omni`). A notch in the aft wall shows the crystal from over the ribs.

## Engines that manoeuvre
The eight jets sit in the stern gape, one under each plate.

Each jet pivots 5 behind its intake, inside an octagonal ring (`engine.<k>.ring`, the yaw axis). The jet pitches about the ring's trunnions, and the ring sits at least 2.1 clear of the jet and of both valves.

The takes vector the thrust the way the manoeuvre needs, in the fleet convention:
- `pitchup` / `pitchdown` tilt every nozzle up or down.
- `yawleft` / `yawright` swing every ring.
- `rollleft` / `rollright` tilt the two sides opposite ways.
- `speedup` draws the splayed jets in toward straight aft.
- `speeddown` fans them out (an air brake).

Also `release` (the lance slides 20 aft) and `hangaropen` (the doors swing out, hinged on their outer faces). All are single-frame poses, with `rest` first.

## Shapes
- **Charge:** the lance lengthens and the crystal swells.
- **Mass:** the keel deepens and the jets fatten, staying inside their rings.
- **Space:** the jaws' knuckles reach and the sensor masts rise.
- **Time:** the jets stretch.

None of them moves a plate, a cradle, a socket bone or a hole.

## Gates
- `build.py ark_reverse` (codec + assimp + re-import) and `build.py ark_reverse --check`. The check is repeatable because the kit puts every boolean's inputs in a canonical order, so two builds cut the same mesh.
- `assembly_report.py --floating shell.0..7,umbo,auricle.L,auricle.R,omni.ring,omni,payload,keel,hangar.door.T,hangar.door.B,engine.0..7,engine.0..7.ring`.
- **`pose_clearance.py ArkReverse.fbx --parts "engine.*,payload,hangar.door.*" --takes all`**: plays every take and fails on a part that comes within 0.4% of the diagonal of a neighbour in any pose.

## What round 5 taught (for the next spec that cuts holes)
- **An EXACT boolean is only as good as its target.** The umbo is a polar solid with inset panels. A grid column narrower than twice an inset's thickness turns inside out, the mesh starts intersecting itself, and the solver then silently keeps a few hundred vertices of a 10k-vertex part (or leaves the cutter's cap behind). `_cols` now drops any grid column within a degree or so of a forced one, and the panel insets skip narrow columns.
- **A cutter must be one solid.** Two overlapping cutters joined into one object intersect themselves. Subtract them one at a time.
- **Don't fold a wall into a polar grid where the outline runs across the rays.** The bulwark rising along a straight swept leading edge folded the deck's polar faces over each other, so it is now its own loft along rho.
- **Batter, don't stack.** A vertical wall with rows of windows reads as a building. The same wall leaning back as armour, with one slit row under its edge, reads as a ship. `polar_bm`'s wall scale can now be a function of the angle, so the umbo's skirt is steep where the ribs converge into it and sloped everywhere else.
