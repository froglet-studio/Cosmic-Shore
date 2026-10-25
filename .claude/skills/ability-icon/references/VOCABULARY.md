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
| Chamfered rect | `canvas.chamfered_rect` | a plate, a card, a chip | the style guide's corner sliver on two opposite corners |

Element marks (bolt, hexagon, black-hole ring, hourglass - the Starlings' cards) are NOT in this
vocabulary for ability icons: the flower owns the element. They remain the Starlings' card faces.
