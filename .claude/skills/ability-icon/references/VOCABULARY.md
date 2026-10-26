# The shared pictograms (`Tools/Build/icon_kit/motifs.py`)

The same thing is the same shape on every hull. A motif is added here the first time TWO hulls need
it; a hull's own silhouette lives in its spec until a second hull draws it. Every entry names the
function, what it is in the game, and the rule for using it.

| Motif | Function | In the game | Rule |
|---|---|---|---|
| Prism | `prism(cx, cy, s)` / `prism_lit` (one face cut) | a trail block, mass | corner-on diamond; the lit cut only at s ≥ 11 (it vanishes smaller) |
| Hull | `hull(cx, cy, s, heading)` | the generic vessel | a notched chevron; use when the act is "ahead of you" (Track Projector), never as the subject |
| Arrowhead | `arrowhead(tip, dir, size)` | direction | one weight, one shape, sparingly (an arrow is failure shape 2) |
| Burst | `burst(cx, cy, r, spikes, inner, rot)` | a detonation | ONE jagged polygon, spike lengths varied; a hollow core for "where" |
| Spiked ball | `spiked_ball(cx, cy, r, spikes, spike, rot, width)` | the Urchin; a morning star at `spike` ≥ 0.6 | wide short spikes read as a ball; long thin ones as a star |
| Trail | `trail(pts, w, seg, gap)` | a ribbon of blocks along a path | segments ≥ 14 with gaps ≥ 5, or it fuses at 40 px |
| Trail band | `trail_band(y0, y1, x0, x1, notches)` | the ribbon seen from the side | one band with V notches cut from its top (the Urchin rides it, the Squirrel skims it) |
| Rail | `rail(cx, y_near, y_far, w_near, w_far, ties)` | a track receding | sleepers that cut the full width read as a Christmas tree - the Urchin's spec cuts a centre GROOVE instead |
| Speed wedge | `speed_wedge(x, y, length, w, lean)` | motion | at most two, behind the moving mass, never three lines |
| Ghost | a layer at alpha 0.6 (`draw()` returns `[(mask, 1.0), (ghost, 0.6)]`) | a previous position, a second mode, a spent slot | the SAME shape displaced or dimmed; 0.5 sits on the gates' threshold and vanishes at 40 px (LESSONS 14) |
| Band / ring / disc | `canvas.band`, `canvas.ring`, `canvas.disc` | arcs, orbits, reticles | 96-gons: round things yes, round corners no |
| Hull, real | `mesh_silhouette.silhouette(fbx, pitch, yaw, box, cx, cy, dilate_px, close_px, roll, mirror)` | the vessel doing the act | its own FBX mesh, parallel-projected - never a drawing ("the fidelity is too low. this reads too abstractly"). Pick the view from `mesh_silhouette.py <fbx> --out sheet.png` (the lettered grid; the Squirrel's D = pitch 45 yaw 15, the Dolphin's A = plan view). Check the file frame in `Docs/VesselModels/<Hull>.md` §1.1: a Blender export with the nose at -Y needs `mirror=True`. Thicken 1.2-1.8 px for the 60 px card, no more |
| Hull, posed | `mesh_silhouette.silhouette(fbx, take="<stack name>", ...)` | the hull DOING it (the Sparrow folded for boost) | the authored take skinned offline (`mesh_pose.py`); a state the view swaps in, never a drawn approximation of the pose |
| Plume | `specs/sparrow.py plume(x0, y, length, w0, w1)` (promote to motifs.py on its second hull) | exhaust: a ship under way, at full burn | a tapered bar, chamfered at the engine, square at the tip; two, at the engines' real spacing; its own white part so the hull keeps the domain colour |
| Block | `block(cx, cy, w, h, dp)` | a bare prism in a trail, from front-above | a box with its seams cut; a notched band read as "the profile of a chocolate bar". Gap ≥ 6 px between blocks |
| Plate gauge | not a motif: `AbilityIconBinding.gauge` on the HUD; `card.py card(..., gauge=0..1)` on a sheet | HOW MUCH (a bank, a charge) | the lockup's one linear fill of the lower totem. Never a ring round the hull ("the ring now reads as a skimmer"), never a sprite ladder |
| Chamfered rect | `canvas.chamfered_rect` | a plate, a card, a chip | the style guide's corner sliver on two opposite corners |

**A hull's own silhouette is its mesh**, never a drawing of it (Garrett 2026-10-25: "the fidelity is too low"):
`mesh_silhouette.silhouette(fbx, pitch, yaw, box, cx, cy, dilate_px)` projects the FBX on the kit grid, and
`project_point()` / `nose_vertex()` give the points to compose around; `hull_view.py` writes top / side / front
PNGs to look at first. The Squirrel's view is D (pitch 45, yaw 15, +1.2 px, `specs/squirrel.py`). Such an icon
exempts the `gap` gate by name. A creature / lifeform body is a hexagon (`ngon(6, r, ...)`).

Element marks (bolt, hexagon, black-hole ring, hourglass - the Starlings' cards) are NOT in this
vocabulary for ability icons: the flower owns the element. They remain the Starlings' card faces.
