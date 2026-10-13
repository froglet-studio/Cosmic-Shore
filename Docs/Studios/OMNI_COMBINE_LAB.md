# Omni Combine Lab

A browser lab for designing how the four elemental crystals (Charge, Mass, Space, Time) **fly together
and become one omni crystal**. It has styles to play, sliders for every part of the motion, a scrubber,
a headless scorecard and a shared decision log.

- **Page (source):** `Docs/Studios/OmniCombineLab.html` (open it in Chrome, or use the live page)
- **Live page:** https://claude.ai/artifact/PRaTKYuNSLhAmBanzatbjo (private until shared from its Share menu; decisions recorded there land in its `decisions` log)
- **Art data:** `blender -b CrystalsRaw.blend --python Tools/Build/bake_omni_crystal_combine.py -- Docs/Studios/data/omni_crystal_combine.json`
  (the `.blend` is the art team's, not in the repo)
- **Lab data block:** `python3 Tools/Build/omni_combine_lab_assets.py` (`--check` fails when the data or the
  shipped colours move and the page has not been rebaked; `--self-test` 9/9)
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

## Round 2 (2026-10-13): the designer's order and morphs, with every effect running

**The decision (logged on the page):** each crystal starts doing its own animation and effects, and the
combine hands over to the omni doing all four of its effects. Space goes first, because it lays down the
negative space the others fill. Charge goes next, with a symmetric morph: each petal's back sucks in so its
three back sides become one, then the five petals become the pentagon's sides as their centres fuse and
flatten. Mass gets the same treatment into the triangles. Time locks in last with no morph. The timing
knobs are the designer's round-1 pick (stagger 1.2, flight 4.6, arc 0.4, no swirl, tumble 0.25, morph
0.1 to 0.9, no flash).

**The timeline (the lab default):**

| Time | What happens |
|---|---|
| 0 to 2.5 s | The four idle, each doing its own effect. |
| 2.5 s | Space sets off. |
| 3.7 s | Charge sets off. |
| 4.9 s | Mass sets off. |
| 6.1 s | Time sets off. |
| 10.7 s | The last plate lands, and the page hands over to the real omni mesh with its four effects fading in over 0.7 s. |
| 10.7 to 13.7 s | The omni holds, doing its four effects, then the loop restarts. |

Each crystal's idle effect settles over 0.7 s once it sets off.

**The morphs.** These are derived from the art file's own end pose (`Key 1`), not invented. The derivation
is in `omni_combine_lab_assets.py`, and its results are baked into the page.

- **Charge** (`wedge_targets`). Each petal is a pentagonal slab with:
  - a tip (3° off its site's axis);
  - two shoulders (26°);
  - two back points (34°).

  The morph runs in two stages:
  - **Stage 1**, the first 35% of the morph (`suck`): the back points slide onto the line between the
    shoulders, so the petal's three back edges become one and the petal is a triangle.
  - **Stage 2**: the five petals untwist together about the site axis (−13.1°, the same at all 12 sites).
    Each tip goes to the pentagon's centre, the shoulders go to the corners of one pentagon side, and the
    back points go to the same place along that side. The five wedges then tile the pentagon. This is
    checked: their area sums to the face's to 1e-4, and no point leaves the face's plane by more than 1e-5.
- **Mass**: the same, with three wedges per triangle. The prisms are already triangles, so there is no
  suck-in stage. They untwist by 31.51°, the tips fuse at the centroid, and the base corners go to a side's
  corners.
- **Time** (`time_rigid`): per rhombus, the rotation that carries its rest shape onto its slot (Kabsch).
  The rhombus flies and turns rigidly, with no deformation. Its worst residual against the omni is
  0.0068, which is 0.4% of the omni's radius.
- **Space**: its `Key 1` (the squares were already one to one).

A plot of one site through the morph (looking down the site axis) shows the expected shapes:
- Charge's five petals go from slabs, to triangles at w=0.35, to a flat pentagon of five wedges at w=1.
- Mass's three prisms go to three thirds of the triangle.

**The effects come from the game's own assets,** not guesses. `omni_combine_lab_assets.py` reads them and
`--check` fails if they move.

- **Time: the flip wave** (`TimeCrystalFlipWaveProfile.asset`, `FlipWave.Plan`).
  - Loop 2 s, first ring at 0.2 s, stagger 0.2 s, with the lead ring's and later rings' curves.
  - Rings go outward from one of the 12 five-fold vertices, sized 5/5/10/5/5.
  - A new start vertex is drawn each loop.
- **Space: the 5-point spin, then the 3-point spin.** The idle comes from the art file's own animated copy,
  `Solid.055` (4 shape keys over 120 frames). The baker replays it and refuses to write unless the replay
  matches Blender's evaluated mesh at 5 frames. Timing comes from `SpaceCrystalAnimator` (1 s a spin, then
  a 0.1 s pause).
- **Charge: bolts on every slab edge** (`ChargeCrystal.hlsl`: speed 0.85, length 0.14, duty 0.35). The
  crystal also tumbles at 20°/s, as `JustRotate` does on `CrystalCharge.prefab`, and the tumble unwinds in
  flight so the crystal lands in the omni's frame.
- **Mass: the depth prisms** (`CrystalDepthPrismProfile.asset`: open −0.88, seal 1, reach 2, start size
  0.5).
- **The omni:**
  - the flip wave on its 30 rhombi;
  - the squares weave on its 60 squares (`omni_space_squares.json`);
  - bolts on its 12 pentagons;
  - the depth prisms on its 20 triangles.

**Scorecard** (seed 7; 60 samples a second from the first departure to the last landing):

| Style | Seam % | Overlap s | Peak (radii/s) | Clearance | Duration s |
|---|---|---|---|---|---|
| **Designer's order (round 2)**, the lab default | **0.001** | 0 | 2.05 | 2.6 | 8.2 |
| Round-1 pick, Key 1 morphs | 0.018 | 0 | 2.1 | 2.6 | 8.2 |
| All four at once | 0.001 | 0 | 2.78 | 1.41 | 3.4 |
| Archive: Converge (round 1) | 0.018 | 0 | 4.94 | 1.81 | 2.4 |
| Archive: Spiral in (round 1) | 0.018 | 0 | 8.24 | 2.17 | 3.2 |
| Control: no morph | **62.5** | 0 | 1.88 | 2.6 | 8.2 |
| Control: Key 1 stacks overlap | 0.018 | **3.55** | 2.1 | 2.6 | 8.2 |

The designer's morphs land closer than `Key 1`: the seam drops from 0.018% to 0.001%. The wedge targets
are computed from the omni's own vertices. Time's rigid pose is compared against the omni separately (see
Checked). The designer's morphs never stack plates, so they have no overlap to resolve, and round 1's
fade/shrink question goes away for them.

**Checked**
- `verify_lab.cjs` PASS (desktop and phone). Both screenshots read: at 2 s the four idle with their effects
  (Charge's bolts, Time's flip, Space's spin, Mass's prisms).
- A frame strip of the whole default:
  - idle effects run first;
  - Space, Charge, Mass and Time arrive in that order;
  - the landing is clean at 10.69 s;
  - the omni effects are running at 12.5 s.
- A close-up strip of Charge (tinted, 4.3 to 8.4 s) shows pentagons building out of five wedges on the
  shell.
- The flip rig finds 12 start vertices and rings of 5/5/10/5/5 on both the Time crystal and the omni,
  matching the game's plan.
- `omni_combine_lab_assets.py --check` OK. `--self-test` 9/9, including three new negative controls:
  - a Charge petal moved to another pentagon is refused ("omni plate 64 gathers 6 plates, expected 5");
  - a Time rhombus 30% too big, which would need a morph to land, is refused ("misses the omni (worst
    0.068)");
  - the shipped wedges and lock-in pass.
- Both controls still move only their own columns.

**Found**
1. **The art file has a slip in Time's `Key 1`.** In rhombus 2, corners 9 and 129 are swapped between the
   rhombus's inner and outer faces. Round 1's land check passed it, because each corner still lands on *an*
   omni vertex. Only the rigid fit exposed it: the plain Kabsch residual was 0.061 against 0.0068 for every
   other rhombus. The lab's fit leaves the two corners out and reports them (`artSlips`). Under round 1's `Key 1`
   morph those two corners cross through the slab on the way in. **For the artist:** swap `Key 1` on vertices 9
   and 129 of `1.011`.
2. **Five-fold vertices can't be found from shared corners on an exploded shell.** The plates don't touch,
   so "vertices where five plates meet" found 60 candidates instead of 12. The fix: tilt each rhombus's
   outward axis by atan(1/φ) ≈ 31.7° along its long diagonal (the angle from a rhombic triacontahedron
   face to its five-fold vertex), then cluster within 4°. That gives 12.
3. **The game's Space crystal is not the art file's.** It ships kites (`author_space_crystal_mesh.py`); the
   art file and this lab show squares. The idle spin is the art file's animation, which matches the game's
   5-point/3-point rhythm, but not its mesh.

**Not modelled** (also on the page's About panel):
- bolts are drawn as 1-pixel lines;
- the depth prisms are stateless (the game's ratchet keeps state);
- the shading is a fresnel, not the game's shaders;
- the sizes are the art file's, not the prefabs'.

**Decision needed (designer)**
- Whether the morph reads right in motion, especially Charge's suck-in share (`suck`, 35%), and the
  `plateSpread` across sites.
- What triggers the combine in a match.
- The end size.
- Graduation needs the elemental meshes re-exported from this file (or the targets re-solved against the
  shipped meshes), and Space's kites reconciled with the squares.

## In the game (2026-10-13): the Elemental Converter on the Ark screen

**The decision:** "the goal will be to get the effects smoothly keeping a continuity as they go from
individual crystal to combined omnicrystal. we can keep each animation separate and slow because each
movement is hypnotic and beautiful ... if it renders in game each time then we can have a few parameters that
it randomizes through a range so it never views it exactly the same each time. give me a elemental converter
in app shell. place it in the ark which you will need to reactivate. for now it should just play this
animation when i use it."

**What plays.** Menu_Main's Ark screen is open again. It shows the four crystals idling with their own
effects and a CONVERT button. A press sends them in from exactly where they are (the press only sets the
lead to "now"), Space, Charge, Mass, Time, each landing as the next sets off. The omni then dithers in over
the landed plates (0.6 s) and wakes its four effects (0.7 s). A second press plays a new combine. The camera
circles slowly and frames what is on stage NOW, eased over 1.6 s, so it dollies in as each crystal lands and
ends close on the omni.

| Piece | Where | What it is |
|---|---|---|
| `OmniCombineCore` | `Assets/_Scripts/Utility/` | The lab's `pose` / `omniPose`, flip rig, seeding and hash, ported line for line to pure C# (doubles, no UnityEngine) |
| `OmniCombineData.json` | `Assets/_SO_Assets/Environment/OmniCombine/` | The lab's data block in Unity's axes and a JsonUtility schema (`Tools/Build/author_omni_combine_asset.py`, `--check`, `--self-test` 8/8) |
| `OmniCombineConfigSO` / `OmniCombineConfig.asset` | `Controller/Environment/Crystals/`, same folder | The data, the materials, and the per-play RANGES (`Tools/Build/author_elemental_converter.py`) |
| `OmniCombinePlayer` | `Controller/Environment/Crystals/` | Builds the meshes and writes them every frame |
| `ElementalConverterView` | `Assets/_Scripts/UI/Elements/` | The Ark screen: an off-screen stage, a hand-stepped camera (screen-pixel × 2 supersampling, 4× MSAA, SMAA), the picture, the title and CONVERT |

**Separate, slow, and never the same twice.** The game's defaults differ from the lab's in one way: the
next crystal sets off as the last one lands (stagger 0.95 to 1.05 of the flight), so each animation plays on
its own. Every press draws, from `OmniCombineConfig.asset`:
- lead 2.2 to 3.2 s;
- start distance 2.7 to 3.3 radii;
- the whole layout turned 0 to 360°;
- flight 4.4 to 5.4 s;
- arc 0.25 to 0.55;
- swirl ±0.15 turns;
- tumble 0.15 to 0.35 turns;
- site spread 0.35 to 0.65;
- Charge's suck share 0.3 to 0.4;
- a fresh seed (each crystal's tumble axis, each site's morph delay, the flip wave's start vertices);
- a random phase on the flip, spin and squares clocks.

From the press to the omni fully awake takes about 19 to 24 s.

**How the effects carry across.**
- **Idle:** each crystal's idle effect is the lab's, read from the game's own numbers, and fades as it sets
  off.
- **Hand-off:** the omni appears ON TOP of the landed plates before it moves anything, so there is no frame
  where a plate is missing. Its four effects then ramp in from rest.
- **Materials:** Space and Time wear `OmniCrystalBody`, the omni's own body material and the same lime pair
  every elemental crystal ships in, so those plates do not change look at the hand-off. Charge wears
  `ChargeCrystalMaterial`, so its pentagons do change from the Charge body to the omni body as the omni
  dithers over them.

**Mass is the game's own crystal (revision 2, 2026-10-13).** The designer asked for "the mass crystal so it is
consistent with the gameplay, even if it means rebuilding". The first build drew the art file's Mass slabs in the
shell material with the lab's stateless CPU depth model. Now:
- **The mesh** is `MassCrystalPrisms.asset`, the 60 prisms the game draws. `author_omni_combine_asset.py`
  reads its vertex buffer, finds it is the art file's Mass crystal turned a quarter turn about Z (plate
  centres agree to 4e-4 after the turn and a 0.7355 scale; the prisms are thinner), and re-derives the wedge
  morph on the GAME mesh with the lab's own `wedge_targets`. The untwist is 31.61° (31.51° on the art mesh).
  The tiling gate passes.
- **The look and the depth effect** are the game's: `MassDepthPrisms.mat` on `OmniDepthPrismShader`, each
  column stepped by `DepthRatchet` with `CrystalDepthPrismProfile.asset` against the converter's camera, as
  `CrystalDepthPrisms` does in play. The Mass object sits at the crystal's flight centre, so the columns
  rise from the crystal's own centre. The effect ramps to sealed as the crystal sets off.
- **The omni's triangles** are the game's `OmniCrystalTriangles.asset` on `OmniDepthPrisms.mat`, turned onto
  the omni by a rotation the generator solves and gates (every corner within 1e-4; found at 2e-5). The body
  has its 20 triangle holes cut, as `CrystalDepthPrisms` cuts them in play. When the omni wakes, its
  triangles start riding their columns.
- **The hand-off is exact by construction:**
  - As each Mass prism morphs, its column centre (TEXCOORD2) slides from its own centre to its triangle's.
  - The depth shader's shape (thickness, top-face height, plate radius, reach, split, start size) blends from
    the Mass material's values to the triangles'. The Mass values are scaled from the game Mass mesh's units
    into the stage's.
  - So the three landed wedges extrude exactly as the omni's triangle prism does, and the shader's map is
    affine per plate.
  - The game meshes carry their thickness (Mass 6.25, omni 8, baked by `thicken()` in
    `author_omni_crystal_triangles.py`; the depth materials ship `_Thickness 1`). The combine morphs the THIN
    prisms the art's plates are, so the generator takes the baked thickness back out of each mesh (gated: the
    thin triangles must land on the art omni within 1e-4, and a wrong thickness is refused) and writes it to
    the data (`depth.massThickness`, `depth.omniThickness`). The player multiplies it into the material's
    `_Thickness` for the morphing Mass prisms only; the triangle renderer draws its thick mesh as authored.
    Stretching about the top face composes by multiplying, so the landed Mass draws exactly the thick mesh.

**Revision 4 (2026-10-13): the game's round-5 effects.** The designer: "now polish the elemental conversion
animation in the app shell", straight after round 5 of the Omni Motion Lab graduated. The converter had kept every
effect from before round 5, so what it showed no longer matched the game:
- the Charge edge crawl on both the Charge crystal and the omni's pentagons;
- the squares' three-key weave, which shrinks the squares mid-move;
- a flip wave on its own 2 s clock, flipping while the squares moved;
- Mass columns stepped without the horizon seal or the hidden rise. Against the new −0.25 / −0.97 profile this would
  have shown far fewer prisms standing out than the game does.

It now plays the game's round 5 through the game's own code:

| What | In the converter | Shared with the game |
|---|---|---|
| Mass (waiting crystal, the omni's triangles) | columns stepped against the converter camera, sealed at the horizon, rising only while hidden | `CrystalDepthPrisms.StepColumns` (extracted from its `LateUpdate`) and `TryGetPlates` |
| The Charge crystal, waiting | the **seam walk**, carried on the crystal's rigid frame (`OmniCombineCore.CrystalFrame`); its crawl held at 0. It fades as the crystal sets off and is gone before any petal morphs | `ChargeArcs`, `CrystalChargeArcsProfile.asset`, `ChargeCrystalArcs.mat` |
| The woken omni's Charge | **chained flux arcs**; the first discharge starts charging as the omni wakes | the same core, `OmniChargeArcs.mat` |
| The woken omni's Space and Time | the **cadence** (pentagon turn, wave, triangle turn, wave, 0.15 s gaps); each square turn ONE rigid hop; one **cued** flip wave per wave beat. The first beat starts once the omni is fully awake, so its first move is a whole pentagon turn | `OmniCrystalCadence.asset`, `OmniCadence`, `SquareTurnFit` (extracted from `CrystalSpaceSquares`' baker, so the blend shapes and the converter turn a square the same way), `OmniCombineCore.OmniPose`'s game-motion path |
| Thicker plates | the omni's squares, rhombi and pentagons **swell from 1.0 to 1.1 as its effects wake**. The landed plates are the thin art plates, so the hand-off has no step | `OmniCombineMotion.Swell`, the same thickening the game's shaped twin applies |

The lab path is untouched: `OmniCombineParams.GameMotion` is off in the lab defaults, and the parity harness still
matches the lab to 7e-7. `OmniChargeEdges.mat` and `OmniChargeEdgesShader` are deleted (the converter was their last
user).

**Proved offline:**
- `Tools/Build/converter_round5_harness` runs the shipped `OmniCombineCore`, `OmniCombineMotion`, `SquareTurnFit`,
  `OmniCadence` and `ChargeArcs` on the shipped `OmniCombineData.json`. All 15 checks pass:
  - all 60 squares fit the data's keys as rigid turns;
  - full swell is 1.1× thick (3e-6), and the triangles do not move;
  - a finished turn lands congruent (4e-5 R), and squares stay rigid mid-turn (2e-7 R);
  - with no wave cued nothing moves; a cued wave lands every rhombus congruent (7e-6 R);
  - `CrystalFrame` composed as the player composes it reproduces `Pose` (2.5e-7 R);
  - both crystals read as Charge sites;
  - the arc ends sit on the swelled pentagons (7e-7 R);
  - the lab path ignores the new arguments.
- Two negative controls fire: the keys out of order are refused, and the frame composed the other way round misses by
  0.78 R.
- `author_elemental_converter.py --self-test` had gone vacuous. The converter is now on the merge base, so its "base
  scene" was already edited and two refusals never reached the code they guard. Both were red. It now edits the scene
  from before the converter landed and asserts that it does; 11/11.

**Revision 7 (2026-10-13): the lab's morphs back; a bit of extra spacing; arrive from the camera's side.** The
designer: "you have embellished heavily on the morphs. revert to the morphs that elegantly undergo minimal
transformations to be exactly where they need to be. the point was it is better if the time rhombuses come in with a bit
of extra spacing and then come down to perfect spacing in order to socket in from the outside instead of sliding through
on the same radius or coming out to higher radii from within the structure ... the solution i want has all its clipping
occluded or unnoticeable, but leaves the minimal transformations preserved."

- **What the lab's morph actually does at the end.** Measured on the shipped data: a crystal's shell is 90% formed
  while its centre is still 0.3–0.5 omni radii out, coming down off its arc. The last stretch is the finished shell
  sliding home. The rhombi on its leading side rise out of their holes, the ones at its sides slide along at slot
  radius, and only the trailing side comes in from outside.
- **Why spacing alone cannot fix it.** A crystal arriving from one side can only bring its leading rhombi in from
  outside by going round the omni (revision 6's swing, retired) or by swelling to about twice the omni's size.
  - A bit of extra spacing on its own barely helps: Time's camera-facing rhombi stay inside 26% of the late morph,
    against 30% before.
  - Retiming the flight's ease does no better: 19–41%.
  - Lifting plates radially onto a floor whips them at 36 R/s.
- **What shipped.** The designer chose the third option: arrive from the camera's side.
  - Revision 6's `SocketPath` is gone, and the morphs are the lab's again.
  - `SocketSpacing` (0.08–0.12, drawn per play): each plate of every crystal but Space morphs toward its slot pushed
    out by that fraction of the slot's radius. Over the last 15% of its morph (`OmniCombineCore.SettleFrom`) the extra
    eases to nothing, so the plate comes down to perfect spacing.
  - `ApproachTurn`, set by `OmniCombineCore.FaceCamera` when the crystals set off: each crystal's flight turns about
    the vertical, the shorter way round. The turn starts at nothing, so a crystal sets off from exactly where it idles,
    and is complete by 70% of the eased flight, while the crystal is well clear of the omni.
  - The camera aimed for is where it will be as each crystal arrives. The player reads the camera's position and how
    fast it circles from what the view hands it each frame. The rhombi that have to cross the omni then do it on its
    far side, behind it.
- **Measured** (`converter_round5_harness`, 24 random plays against the real orbit, 18° pitch at 5°/s):
  - Camera-facing rhombi are inside the omni late in their morph 0.5% (Charge), 6.5% (Mass) and 0.6% (Time) of the
    time, against 24.6%, 19.0% and 33.0% arriving straight in (the negative control).
  - Arrival is within 2.9° of the camera's azimuth.
  - Landings are 7e-8 R. The spacing matches its formula to 6e-7 R.
  - Space is untouched.
  - The lab path (`SocketSpacing` 0, no turn) matches the lab to 7e-7.
- **Correction, round 8.** The 0.5 / 6.5 / 0.6% figures above came from a proxy: a rhombus counted as visible if its
  hole faces the camera. Ray-casting against every triangle actually drawn, the turn does NOT occlude the clipping.
  - Intersections between an incoming crystal's triangles and the landed ones stay visible about 15% of the time on
    every approach. Time: 208/s straight in, 212/s from the camera's side, 221/s along the camera's line of sight
    (azimuth and elevation), 177/s from above. Arriving from behind is the best of them: 124/s for Time, 59 for Mass,
    160 for Charge.
  - The clipping is the crystal entering the omni and growing inside it, between 40% and 90% of its flight. Arriving
    from the camera's side shows the entry, and arriving from behind shows the growth.
  - The turn does stop camera-facing rhombi from rising out of their holes, which is what the designer saw on Time.
  - Charge and Mass already had the same turn as Time. They land into a sparse structure (Space only, then Space and
    Charge), so there is little landed geometry to hide them.
- **Round 8: the approach moves with the camera** (designer: "optimize the camera side approach to move with the
  camera").
  - The aim is no longer fixed at CONVERT. Each frame the player hands the play the camera's unwrapped azimuth
    (`OmniCombineParams.CameraAzimuth`), and every path turns by however far the camera has circled since it was aimed.
  - `FaceCamera` still uses the expected orbit rate, but only to pick the shorter way round.
  - Arrival is within 0.1° of the live camera, at the predicted rate, at twice it, or with the camera stopped.
    Aimed once without following (the negative control), a faster camera leaves a crystal 162° behind.
  - Ray-cast, the visible clipping is unchanged by this (Time 221/s, Mass 82/s, Charge 188/s, against 209, 91 and 187
    straight in). Following guarantees the side a crystal arrives from, not what it hides.
- **Cost.** A crystal that has to come round from the far side moves at up to 4.5 omni radii a second, against 1.9
  arriving straight in. Turning only to within a tolerance of the camera trades that speed straight back for visible
  clipping: at ±30° Time is inside 4% of the time, at ±60° 12%.

**Revision 6 (2026-10-13): socket in from outside; five seamless strands.** The designer: "the time crystal appeared to
come in from inside. they should all (except the first) be separating more as if they are eating the whole and then
socketing into place from the outside. The 5 streams that touch the pentagon tip are great, but then there is an abrupt
seam and then a different effect ... keep all 5 streams and have them connect from one to the other with as much
coaxial definition as you can for that length instead of creating any seams at all."

- **Socket from outside** (retired in revision 7: the designer read it as an embellishment). Space lands first, so the later crystals arrive near the centre. Time's plates moved
  straight from there to their slots: outward, through the omni. The harness's negative control measures it at up to
  76% inside the slot radius. Now, for every crystal but the first, each plate's CENTRE follows
  `OmniCombineCore.SocketPath`:
  - out to a shell `socketLift` (0.35–0.6, drawn per play) omni radii beyond its slot;
  - round the omni on that shell, along the great circle, to above its slot;
  - straight down into it.

  Its shape morph and its turn are untouched. The path starts exactly where the unmorphed plate is and ends exactly in
  the slot. Measured: no plate dips inside its slot radius, the last approach is within 0.1° of radial, the landings
  are exact (7e-8 R), and Space is unchanged. The lab path (`SocketLift` 0) still matches the lab to 7e-7.
- **Five seamless strands** (in play and here). The thick middle beam is gone. Each of the five strands is ONE plasma
  ribbon from its source corner to its sink corner:
  - fanned onto the corners at each end, as before;
  - between, a coaxial bundle `fluxBundle` (0.016 R) off the centre line, the five exactly 72° apart;
  - the bundle carried along the arc in a rotation-minimizing frame and making `fluxTurns` (1) whole turns, a braided
    cable;
  - all five bending on one shared noise, so they writhe together and stay coaxial.

  Two things had to change for that:
  - The corners are now paired by their ORDER round the arc. "Turn the source onto the sink" reverses that order as
    seen along the arc, which leaves one face and enters the other, and every strand crossed mid-arc.
  - Through the middle the five are evenly spaced, because the corners, seen obliquely, are not.

  Measured on the real omni over every source-sink pair: closest approach between two strands 0.0182 R (the ring's own
  spacing is 0.0188 R), landings 2e-7 R, sharpest bend 8° per step. With no bundle the five collapse onto one line,
  which is the negative control.

**Revision 5 (2026-10-13): one hue, conducting columns, plasma.** The designer: "the charge pentagons come in with the
wrong hue ... unify the starting hue so the charge matches at base and still has the contrast with the lit state. detect
when a charge effect will bolt through a mass column on the omni crystal and light it up appropriately instead of
reading as a clip. the discrete units along the lighting bolt are separating in a way that reads as an effect not
plasma. use the plasma effect that triggers when the squirrel skims a prism as a reference."

- **One hue.** The Charge crystal wore `ChargeCrystalMaterial` (its own lime, 0.59 / 0.92 / 0.16). The omni body then
  dithered over its landed pentagons in the omni's lime (0.49 / 0.99 / 0), so they came in the wrong hue. Charge now
  wears `OmniCrystalBody` like Space and Time, and its seam walk draws on `OmniChargeArcs`. The base matches at the
  hand-off, and the only contrast is the lit state. The edge-crawl bake on the converter's Charge mesh is gone with
  it (`chargeMaterial` and `chargeArcsMaterial` left `OmniCombineConfigSO`).
- **Conducting columns.** `ChargeArcs.FluxStrikes` tests each flux arc's path against every triangle prism's body at
  its column's place (`CrystalDepthPrisms.ColumnBody`). A prism the path runs through glows in `_StrikeColor` while the
  arc holds: full where the path runs through its middle, nothing at its edge. `OmniDepthPrismShader` reads the glow
  from `_DepthState.w`. In play `CrystalChargeArcs` writes it through `CrystalDepthPrisms.Strike`; here
  `OmniCombinePlayer.StrikeTriangles` does. Measured on the real omni: a prism at the top of its column is three radii
  out, above every arc. The hits are prisms part-way up near the silhouette: 2,664 of 9,600 frames at places 0.6–0.9.
  Sealed prisms are never struck.
- **Plasma** (`ChargeArcMeshWriter.Plasma`, after `ForcefieldCrackle.hlsl`):
  - each bolt is ONE continuous ribbon whose vertices are shared from piece to piece, so it never parts;
  - it bends on smooth FBM value noise that flows along it instead of re-rolling, held to zero only near the ends of
    a bolt that must land;
  - its cross-section is the crackle's: a Gaussian glow and a hot core, the colour running glow → core by heat²;
  - a thinner sub-filament rides beside it at 0.4.

  The seam walk is one trail through every lit seam. Each point fades from when the bolt LEFT it, so it fades
  smoothly to nothing at its tail. Fading from when a point was struck stepped the brightness at every hold, and cut
  the tail off at a third of its brightness when the old seam died.


**Picture quality (revision 2).** The first build rendered 1280 px along the picture's long edge and stretched
it over the screen with no anti-aliasing: low resolution and jagged. It now renders at the picture's
on-screen PIXEL size times 2 (an exact box filter on the way down), with 4× MSAA on the target. It uses the
game camera's post AA, or SMAA High when the game has none, and HDR. All of these are inspector fields on
`ElementalConverterView` (`supersampling`, `msaaSamples`, `maxTextureSize`). The CONVERT button sits 56 units
above the bottom. The nav bar is at the TOP of the screen, so the first build's 230-unit clearance was
guarding against nothing.

**How it was proved without an editor.**
- `Tools/Build/omni_combine_parity/run.sh` compiles the shipped `OmniCombineCore.cs` in .NET and runs it
  against the lab page's own `__lab.pose` / `omniPose` in headless Chromium: worst vertex 7e-7, across 14
  crystal times and 5 omni times, seed 7.
- Two planted defects fail it (a flip salt off by one: 0.7 off; the lab's seeding order: 2.5 off).
- The same run plays the shipped Unity-frame asset and checks:
  - every crystal reaches its target;
  - Time locks in 9e-6 off its rigid pose;
  - CONVERT after a 300 s wait moves nothing;
  - after a 1000 s wait, Charge moves at most 0.039 omni radii a frame.
- `unity_refcompile` player and editor configs: 0 errors in project code. The negative controls fire on the
  view, the player, the test file and ScreenSwitcher.
- Not opened in Unity: `Docs/UNITY_VERIFICATION_CHECKLIST.md` has the steps.

**Found**
1. **Unity's FBX import is a REFLECTION, and the Charge crystal is chiral.** Blender (x, y, z) becomes
   Unity (−x, z, −y), with determinant −1. Positions map with M, but a rotation AXIS maps with −M (a
   pseudovector), and so does a quaternion's vector part. A straight copy of the lab's data would have
   untwisted every petal the wrong way. The generator re-proves both rotations in the written frame (the best
   rotation about each site axis against the baked twist; Time's lock-in against its rigid pose). Mapping
   the axis as an ordinary vector is its negative control.
2. **A wedge does not land on a vertex.** Charge's and Mass's targets put the tip at a face's CENTRE and the
   backs along its SIDES. The first landing check measured every crystal against the nearest omni vertex,
   and "failed" Charge by 0.39. Space and Time are checked against vertices; the wedges are checked against
   their targets and the tiling gate.
3. **The art data shares a slab's corners across its faces.** Drawn welded, the crystals would shade smooth.
   Worse, the game's discharge bake decides a crease by "two triangles that do NOT share raw vertices", so a
   welded Charge crystal has no creases and no discharge. The player groups each plate's triangles into
   faces (the bake's own 20°) and gives each face its own corners. That gives 420 Charge faces (60 × 7) and
   724 on the omni.
4. **The Charge tumble accumulates while the four wait.** The lab unwinds the whole accumulated turn over
   the flight; after a minute's wait that is twenty laps backwards in five seconds. The core takes the turn
   modulo a whole turn: the same pose, at most one turn to unwind.
5. **ARK was never just closed.** Two arcade rules still pointed at it:
   - it was host-only in a party;
   - an arcade card pulled a party guest onto it (`FollowHostToArcadeScreen`).

   That second rule was a no-op only because ARK was disabled. Opening ARK without removing both would
   have dragged every guest onto the converter whenever the host opened a card.

6. **Revision 3 (after the second look): three faults, three causes.**
   - **"The charge crystal has a vestigial rotation"**: the converter ported `JustRotate` (20°/s) from
     `CrystalCharge.prefab` as an idle tumble. It is off in the converter (`OmniCombineParams.IdleTumble`; the
     lab keeps it, so parity still holds).
   - **"Its edge effect is not working, on the elemental crystal or the omni"**: both discharge shaders time
     their bolts with `_Time.y`, and `ScreenSwitcher` runs every screen but HOME at `Time.timeScale = 0`, so
     on the Ark screen the bolts stood still. `ChargeCrystal.shader` and `OmniChargeEdgesShader.shader` now take
     an optional `_ArcClock` (default −1 = `_Time.y`, so every shipped material is unchanged). The converter
     writes its own unscaled clock through the property block. DXC compiles every pass of both shaders; a
     misspelt use fails.
   - **"A jitter where the whole object looks like it is constantly miscalculating position"**: float
     precision. The stage sat at y −150000, where a float resolves 1/64 of a unit, on an omni 4 units
     across, so every vertex snapped by ~0.4% of the radius as the camera circled. The stage is now at
     −30000 (1/512 of a unit) and 10× larger (`omniWorldRadius` 40), about 0.005% of the radius. That is far
     below a pixel. (The Toy Box's preview stage at −90000 has the same exposure, at 1/128 of a unit.)
7. **The shipped elemental meshes ARE the art file's, turned.** Round 1 said Charge, Mass and Time "do NOT
   match the shipped FBXs after a uniform scale", but it never tried a rotation.
   - Mass is the art Mass turned a quarter turn about Z.
   - The omni's triangle prisms are the art omni's turned by a five-fold rotation.
   - Charge's plate directions align with the art Charge's under one proper rotation once Unity's x-flip
     import is applied, and not without it, which is the chirality showing.
   - Time aligns too, though its symmetry makes that weak evidence.

   So the converter could put Charge and Time on their game meshes the same way Mass now is.

**Not modelled / next**
- **Charge and Time:** they are still the art file's meshes (finding 6 says the game's would fit). Time wears
  the omni body material, not its in-game Lime Fresnel on the rigged FBX.
- **Space:** its idle spin is the art file's, on squares, not the shipped kites.
- **The economy:** the converter does not yet spend elemental crystals or credit omni crystals; no such
  balances exist yet.
- **The Mass size:** the converter keeps the art file's relative sizes (Mass at 0.47 of the omni's radius). In
  play the Mass crystal is 0.29 of the omni's.
