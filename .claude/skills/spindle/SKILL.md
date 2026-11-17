---
name: spindle
description: Make a SPINDLE from scratch - the wire a Cosmic Shore lifeform's limb is made of - and build bodies from bespoke spindle designs. Loads what the shipped spindles measurably are (three-sided tubes braided in threes, and ribbons two vertices wide woven into nets), the design library (core, spar, vein, whip, tendril, frill, rib, rachis, comb, shell, frond), how to write a new design, the mesh a design makes and its vertex budget, and the hierarchy creature -> limb -> spindle design. Use on "/spindle", "make a new spindle", "the spindles look like sticks / tinker toys", "bespoke spindles", "a feathered / frilled / coiled / combed limb", "how many vertices is this creature", "spindle budget", or before touching Tools/Build/fauna_kit/spindle.py, organic.py or a spec's `design=`. Run /fauna-creation for the body and its laws; this skill is the layer beneath it.
---

# Spindles from scratch

Garrett, 2026-11-16: "create a whole new skill that can make the spindles from scratch. deconstruct the current
spindles of the fauna bodies and learn how to make new ones that are beautiful, organic, and performant. then remake
these bodies with bespoke spindles as another hierarchy of the design. this will allow you to really get expressive
and bring them to life."

**The hierarchy.** A creature (`Tools/Build/fauna_kit/specs/<species>.py`, `/fauna-creation`) is limbs. A limb names
a spindle DESIGN. A design says how that limb's wire is made. Change a design and every limb wearing it changes;
give a limb its own keyword and it overrides its design for that limb only.

## 1. What the shipped spindles are (measured: `spindle.py --shipped`)

| mesh | vertices | what it is made of |
|---|---|---|
| the braid (`bonita.fbx` `BezierCurve.001`: every plant, the Tadpole, the Bee) | 225 | 3 strands, each a tube with a THREE-sided section, 25 rings |
| a worm body's wires (`wormbody.fbx`) | 1,020 | 4 strands of the same three-sided tube, 85 rings each |
| a Brittlestar arm segment | 304 / 260 | RIBBONS two vertices wide (151, 68 and 60 rings) |
| a Clawfish fluke, a Shark vane's outer line | 170 / 90 | ribbons (84 and 44 rings) |
| the Shark's body, the Brittlestar's disc, the Clawfish's horn | 2,242 / 2,252 / 739 | ribbons woven into a NET that branches and rejoins, and closed loops |

How it is read: an open tube of k-sided rings has k more vertices than quads, so `k = V - Q` for each connected
part. 3 is a strand, 2 a ribbon, 0 a closed loop, a negative number a net.

What follows from it:
- **Nothing has a round section and nothing is solid.** Three vertices a ring, or two. That is why a whole Shark is
  under 3,000 vertices and a Brittlestar 5,072.
- **Beauty comes from the PATH and the WEAVE, not from polygons**: a curve, a taper, a twist, strands that part and
  rejoin. The sculpted bodies are nets of ribbons.
- So a new design is built from the same two parts and costs what the shipped ones cost.

## 2. A design (`spindle.DESIGNS`; `spindle.py --designs` prints each one's cost)

| key | what it does |
|---|---|
| `strands`, `twist` | how many wires wind round the limb's curve, and how many turns from root to tip (0 = straight ribs: a shell) |
| `section` | 3 = a three-sided tube (reads as a wire with body), 2 = a ribbon (half the cost; right for anything fine) |
| `step` | world units between rings: the mesh's cost. Coarser on long thin limbs |
| `ties` | a cross-tie every n rings (hoops on a shell, rungs on a core) |
| `barbs` | side filaments, always ribbons: `count`, `length` [root, tip], `angle` from the limb, `sides`, `dir: tangent` to lay them round the body, `flutter` |
| `glow` | `bright` [root, tip] and `waves`: light that travels down the limb |
| `wire` | a strand's own half-width, root and tip |

The library today: **core** (a body's axis), **spar** (a leading edge, an arm), **vein** (a hind edge), **whip** (a
tail), **tendril** (a stinging line with side lines), **frill** (lace), **rib** (a bell's rib), **rachis** (a feather:
a shaft and two rows of barbs), **comb** (a row of beating plates), **shell** (a coil's wall: straight ribs, hooped),
**frond** (a toothed leaf edge). A limb takes one with `K.grown(name, parent, points, girth, design="rachis", ...)`;
girth and the path are the limb's, never the design's.

**To make a new design:** name the thing it is in the animal (a feather, a comb, a frill), decide strands / twist /
section / barbs / glow, add it to `DESIGNS` with an `about` line, run `--designs` for its cost and put it on one limb
in the lab before using it widely. A design that only changes a number of an existing one is an override, not a design.

## 2.5 Shaping a spindle: the cookbook

A spindle's shape is its PATH (the points), its GIRTH (root, tip) and its design. Copy a recipe, then move points.
`P(radius, z)` is `K.polar(radius, a, z)` at this copy's angle; z runs negative toward the tail.

**How to shape a path**
- 4 to 6 points. The curve passes through every one.
- Never put the middle points on the straight line between the ends: push them 10 to 30% of the limb's length to
  one side. That is the difference between a limb and a stick.
- One bend reads as a sweep, two as an S (a tendril), a turn back toward the axis at the end reads as a curl.
- Spacing sets speed of curvature: points close together make a tight bend.
- The first point sits ON the parent (`K.along(parent, t)[0]`), or within 3.2 of the crystal for a root limb.

**How to shape a girth**
- `(root, tip)`. A living limb tapers: tip is a third of root or less.
- Thick and few strands reads as bone; thin and many as lace. A shell widens instead: `(3.2, 0.5)` from the mouth in.

**Recipes**

```python
# a sweeping arm or leading edge: out, then back
K.grown(f"Arm{k}", "Core", [P(1.0, -4), P(9, -3), P(18, -8), P(23, -18)], girth=(0.6, 0.15), design="spar",
        sway={"dir": "radial", "amp": 4.0, "waves": 0.6})

# a tendril: an S, long and thin, a wave running down it
K.grown(f"Line{k}", parent, [P(10, -11), P(12, -21), P(9, -33), P(12, -46), P(10, -60)], girth=(0.2, 0.05), design="tendril",
        sway={"dir": "tangent", "amp": 4.5, "waves": 2.0, "phase": 0.8 * k})

# a feathered arm: the rachis design brings the barbs
K.grown(f"Plume{k}", "Cup", [P(1, -4.6), P(9, -3.2), P(18, -8), P(23, -18), P(21, -30), P(15, -38)], girth=(0.45, 0.1), design="rachis")

# a bell or cage rib: starts beside the crystal, arcs over it (SPEC needs "lead": 3.2), comes down to a rim
K.grown(f"Rib{k}", None, [P(0.35, 3.0), P(7, 2.8), P(14, 1.2), P(18.5, -2.5), P(19.5, -6.5), P(18, -9.5)], girth=(0.3, 0.16),
        design="rib", sway={"dir": "radial", "amp": 2.6, "waves": 0.3, "pow": 1.2})

# a coiled shell: a spiral in the plane at angle a, widest at its mouth
K.grown(f"Shell{k}", "Core", K.spiral(a, (11.0, -6.0), 10.0, 2.4, 0.17), girth=(3.2, 0.5), design="shell")

# a whip on the axis
K.grown("Whip", "Core", [[0, 0, -30], [0, 0, -45], [0, 0, -60]], girth=(0.4, 0.07), design="whip",
        sway={"dir": [0, 1, 0], "amp": 4.0, "waves": 1.3})

# a wing: two edges that meet at the tip, and a membrane between them
lead  = K.grown(f"Lead{k}",  "Core", [P(1.2, -2.2), P(12, -1), P(24, -5), P(32, -13), P(34, -17)], girth=(0.8, 0.18), design="spar", sway=beat)
trail = K.grown(f"Trail{k}", "Core", [P(1.0, -21), P(10, -22.5), P(22, -20.5), P(30, -18), P(34, -17)], girth=(0.45, 0.12), design="vein", sway=dict(beat, phase=0.9))
membranes.append({"name": f"Web{k}", "a": f"Lead{k}", "b": f"Trail{k}", "across": 9})
```

**Sway** (`sway=`): `dir` is the way the limb moves (`"radial"` in and out from the axis: a pulse; `"tangent"` round
the axis: a sweep; a vector like `[0, 1, 0]`: a flap); `amp` is how far the TIP moves in world units (5 to 15% of
the limb's length); `waves` is how many wavelengths fit along the limb (under 1: the whole limb swings; 2: it
snakes); `phase` shifts it in time (give neighbours different phases; `3.1416 * (k % 2)` makes alternate limbs
oppose). A membrane's two edges half a beat apart make it ripple.

**Glow** (`glow=`): `bright: [root, tip]`, `waves`: pulses along the limb, `phase`: `0.8 * k` makes the light go
round the body one limb after another.

**Barbs** (`barbs=`, replaces the design's): `count`, `length: [at root, at tip]`, `angle` from the limb (60 leans
toward the tip, 90 stands out), `sides` (1, 2 or more), `dir: "tangent"` lays them round the body, `flutter`.
Barbs are the costliest part: 16 a limb is lush, 30 is heavy.

## 3. Motion and light belong to the limb, and are carried by every vertex

`organic.py` gives each vertex its limb's SWAY as a sine part and a cosine part (so a limb's own wave ADDS to what the
limb it grows from is doing: a tendril rides its bell) and its GLOW (a brightness and a pulse phase). The lab's vertex
stage plays both. Nothing is keyed. A membrane (`"membranes"` in the spec) is woven between two limbs and takes both
from them.

## 4. Performance (`spindle.py --budget [--check]`)

The mesh a limb makes is `spindle.mesh`: each strand swept as its design's section, each barb a ribbon, a membrane its
own lattice. The bar is the heaviest single shipped body, the Brittlestar's **5,072 vertices** a creature.

- Reach for `section: 2`, a larger `step`, fewer barbs or fewer strands, in that order.
- The first Nettle was 11,508 vertices and the gate refused it; with designs it is 4,789.
- What this does NOT measure: draw calls, overdraw of additive wire, or the cost of a per-vertex sway in the game's
  shader. A grown limb is its own mesh, so limbs do not share a mesh the way every braid does. Say so when proposing
  one for the game, and run `/perf-hunt` on a real build before believing a vertex count.

## 5. Into the game (not done; the route)

`spindle.py --obj <species> <dir>` writes each limb as an `.obj` Unity imports (x negated for its importer). Nothing
has been imported, and no `.meta` is written. A grown creature in Unity still needs: its meshes as assets, a
`Spindle` per limb (so the LOD and the phase stamp find it), and a sway that reads a per-vertex offset and phase
(today's `SpindleSway` is one shear along z). Space erosion of grown wire is not designed.

## 6. Gates

```bash
python3 Tools/Build/fauna_kit/spindle.py --self-test     # the shipped braid still reads as three 3-sided strands; a design's mesh counts are exact
python3 Tools/Build/fauna_kit/spindle.py --budget --check
python3 Tools/Build/fauna_kit/celestial.py --all         # the body's laws (/fauna-creation)
python3 Tools/Build/fauna_kit/silhouette.py --check      # an outline of its own (about four minutes)
python3 Tools/Build/fauna_kit/lab_export.py && node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/FaunaLab.html
```

## 7. Round log

- **2026-11-17.** Section 2.5 (the shaping cookbook). `/fauna-maker` is the step-by-step path that uses this skill.
  Garrett accepted the Plume's and the Nettle's outlines and decided all five grown creatures go to Unity (not started).
- **2026-11-16, round 1.** The deconstruction, eleven designs, the mesh builder and budget, inherited sway and glow,
  bloom in the lab. Bodies remade with designs: Ray, Nettle. New: Plume (rachis), Lantern (comb), Ammon (shell).
  Open: the Plume (0.36) and the Nettle (0.40) are under the silhouette bar (0.41); prisms on grown bodies still read
  as boxes; the Unity route (section 5).
