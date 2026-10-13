# Omni Combine Lab

A browser lab for designing how the four elemental crystals (Charge, Mass, Space, Time) **fly together
and become one omni crystal**. It has styles to play, sliders for every part of the motion, a scrubber,
a headless scorecard and a shared decision log.

- **Page (source):** `Docs/Studios/OmniCombineLab.html` (open it in Chrome, or use the live page)
- **Live page:** https://claude.ai/artifact/PRaTKYuNSLhAmBanzatbjo (private until shared from its Share menu; decisions recorded there land in its `decisions` log)
- **Art data:** `blender -b CrystalsRaw.blend --python Tools/Build/bake_omni_crystal_combine.py -- Docs/Studios/data/omni_crystal_combine.json`
  (the `.blend` is the art team's, not in the repo)
- **Lab data block:** `python3 Tools/Build/omni_combine_lab_assets.py` (`--check` fails when the data or the
  shipped colours move and the page has not been rebaked; `--self-test` 6/6)
- **Verify:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/OmniCombineLab.html`

## What the art file has (measured 2026-10-13)

`CrystalsRaw.blend` lays the omni crystal (`1.013`) and the four elemental crystals out in a row. Each
elemental crystal carries ONE shape key, `Key 1`, that moves its plates onto its share of the omni, in
the crystal's own space and at the omni's size. Every crystal is unrotated at scale 1, so `Key 1` lands in
the omni's frame once the crystal's origin reaches the omni's.

| Crystal | Object | Its plates | Lands as | Worst miss |
|---|---|---|---|---|
| Time | `1.011` | 30 rhombi (240 verts) | the omni's 30 rhombi, one to one | 2.9e-4 |
| Space | `Solid.060` | 60 squares (480 verts) | the omni's 60 squares, one to one | 1.4e-5 |
| Mass | `1.014` | 60 triangular prisms (360 verts) | the omni's 20 triangles, **3 stacked per triangle** | 1.2e-5 |
| Charge | `1.012` | 60 pentagon plates (600 verts) | the omni's 12 pentagons, **5 stacked per pentagon** | 1.1e-5 |

Together they cover all 960 of the omni's vertices. **Nothing animates the combine:** no action,
driver or NLA strip touches `Key 1`, and its value is 0 in all four. The artist authored the END POSE;
the motion is this lab's to propose. (`CrystalsRaw_V2.blend` has the same keys on Charge and Time; its
Mass and Space crystals are different, larger models whose `Key 1` does not land on the omni.)

Against the game's models (`Tools/Build/fbx_binary.py`, same day):

- The omni here matches the shipped `OmniCrystalExport1_8-21-25.fbx` to 1e-5 after a uniform scale (0.446).
- Charge, Mass and Time have the shipped vertex counts (600, 360, 240), but they do NOT match the shipped
  FBXs after a uniform scale (worst 0.21, 0.07 and 0.05 of a radius). They are another export, or are
  rotated.
- Space ships `spacecrystalanim.fbx`, a different, animated model.

So the omni end of the combine is the game's own mesh, and the elemental start shapes are the art
file's. Graduating this needs the four elemental meshes re-exported from the same file, or their
shape keys re-solved against the shipped meshes.

## Round 1 (2026-10-13): five proposals and two controls

**What the lab models.** A pure function `pose(t, P)` gives every vertex of every crystal:
- Each crystal flies from its start (the art file's row, a ring, or a tetrahedron) to the omni's
  centre, eased. The path bows upward (`arc`), wraps round the omni (`swirl`), and the crystal tumbles
  about a seeded axis, untwisting to land exactly in the omni's frame.
- Each crystal turns into its share of the omni by blending Basis → `Key 1`, plate by plate. Each plate
  starts at its own seeded moment in a window (`mStart`, `mDur`, `plateSpread`).
- The stacked plates (Mass 3:1, Charge 5:1) need a decision. One plate per stack is the KEEPER: the one
  whose rest centre already points closest to where the stack lands. The extras either overlap (they
  z-fight), fade out with a screen-space dither (the game's own fade technique), or shrink away.
- When the last plate lands, the page can hand over to the real omni mesh (`swap`).

**Styles:**
- **Converge** (lab default): tetrahedron start, a quarter-turn swirl, morph in the second half, extras fade.
- **Art file, literally:** the .blend row, straight lines, `Key 1` blended in lockstep.
- **Spiral in:** a ring of four, a full turn, tumbling, late plate shuffle.
- **One at a time:** Time, Space, Mass, Charge, 0.7 s apart.
- **Plate weave:** fast arrival, then every plate slides in at its own moment.
- **Two controls:** Key 1 off; stacked plates overlap.

**Scorecard** (`Score every style`, headless, 120 samples a second through each combine; seed 7):

| Style | Seam % | Overlap s | Peak (radii/s) | Clearance | Duration s |
|---|---|---|---|---|---|
| Converge | 0.018 | 0 | 4.69 | 1.75 | 2.4 |
| Art file, literally | 0.018 | **0.47** | 2.62 | **0.44** | 2.4 |
| Spiral in | 0.018 | 0 | 8.24 | 2.12 | 3.2 |
| One at a time | 0.018 | 0 | 6.4 | 3.29 | 3.7 |
| Plate weave | 0.018 | 0 | 3.9 | 1.75 | 2.2 |
| Control: Key 1 off | **62.5** | 0 | 4.17 | 1.75 | 2.4 |
| Control: stacked plates overlap | 0.018 | **0.67** | 4.69 | 1.75 | 2.4 |

- **Seam** is the largest gap at the end between a landed vertex and the omni vertex it was authored to
  land on, as % of the omni's radius. 0.018% is Time's 2.9e-4 authored miss, so the hand-over to the real
  omni cannot be seen (the frame before and after the swap differ only by the completion flash).
- **Overlap** is the seconds during which two stacked plates are both visible within 2% of a radius of
  each other.
- **Clearance** is the closest two crystals come while both are beyond 1.5 omni radii from the centre,
  as a multiple of their combined radii. Below 1 they pass through each other in open space.

**Checked**
- `verify_lab.cjs` PASS (desktop and phone, hooks, determinism, layout). Both screenshots read.
- A frame strip of Converge at 0, 0.6, 1.2, 1.7, 2.1, 2.39 and 2.41 s shows the four apart, converging,
  merging, landed, and handed over.
- Both controls move their own column and nothing else: Key 1 off moves the seam from 0.018 to 62.5;
  overlapping stacks move overlap from 0 to 0.67 s.
- `omni_combine_lab_assets.py --check` OK; `--self-test` 6/6, including a combine missing a crystal
  (refused) and a crystal whose arrays disagree (refused).
- The baker refuses to write unless every `Key 1` lands within 1e-3 and all 960 omni vertices are
  covered.

**Found**
1. **The art file holds the END POSE of a combine and no motion.** The survey only found it by testing
   each crystal's `Key 1` against the omni's vertices, not by listing actions. Every action on those
   objects is empty (frames 0-0, no curves).
2. **Flying the art file's own layout in straight lines puts crystals through each other** (clearance
   0.44, and 0.47 s of stacked-plate z-fight with no stack resolution). The row is a layout for viewing,
   not a choreography.
3. **The first clearance metric read 0.1 for every style.** All four meet at one centre, so "how close do
   two crystals come" is always small just before they land. The metric now ignores the zone where they
   are meant to merge (inside 1.5 omni radii).

**Decision needed (designer)**
- The motion: which style, and which knobs.
- How stacked Mass and Charge plates resolve: fade or shrink. Overlap z-fights.
- What triggers the combine in a match, and whether it plays in reverse (the Split playback) anywhere.
- The end size: in the game the omni prefab is scaled ×10 and the elemental prefabs ×1.34–1.5. The lab
  uses the art file's relative sizes.
