---
name: fauna-maker
description: Make a NEW Cosmic Shore creature step by step, from "make a <animal>" to a body a person can ride in the Fauna Possession Lab, and its FOUR ELEMENT FORMS (Time, Charge, Mass, Space - each a creature of its own on the one body plan, by the game's element law). The short procedural path for any model - where the crystal goes, how to place spindles (limbs) and prisms with exact numbers, a working template to start from, ONE command that checks everything and prints a fix for each failure, and how to put the creature in the lab. Use on "/fauna-maker", "make a creature / fauna / lifeform", "add a <animal> to the lab", "more fauna", "a new species", or whenever asked for a new creature body. For shaping the wire of a limb load /spindle. For why the rules are what they are, and anything that changes a law, load /fauna-creation.
---

# Fauna maker: a creature in seven steps, and its four elements

You are making a HyperSea lifeform: a body of glowing wire grown round a crystal. Follow the steps in order. Do not
skip the check in step 5; it tells you exactly what to fix.

**Rules that never bend**
- Work only in `Tools/Build/fauna_kit/specs/<name>.py`. Do not edit `celestial.py`, `organic.py` or any law.
- If a law seems wrong for your creature, STOP and tell the person. Laws are Garrett's.
- This makes a LAB creature. Nothing here puts a creature in the Unity game (see "Into Unity" at the end).
- Before you start: `git fetch origin` and `python3 Tools/Build/who_works_where.py query Tools/Build/fauna_kit`, and
  say who else works there in one line (CLAUDE.md, the team rules).

## Step 1. Translate the animal (write this at the top of the spec)

Answer three questions in one line each:
1. **What would a child draw?** Two or three shapes (a manta: a diamond, a whip, two lobes).
2. **How does it move?** One motion (a slow wave from shoulder to tip).
3. **What does the vessel of that name already look like?** `ls "Assets/_Models/Vessel Models"`. Take something else.

Then choose a body PLAN. Do not reuse a plan by changing a count.

| plan | example to read | made of |
|---|---|---|
| wings woven between two edges | `specs/ray.py` | a core, a leading and a hind edge, a membrane |
| a bell with trailing lines | `specs/nettle.py` | ribs from the crystal, membranes between ribs, tendrils |
| feathered arms round a cup | `specs/plume.py` | a cup, arms with barbs |
| a lit woven body with rows | `specs/lantern.py` | combs from the crystal, membranes, two lines |
| mirrored coils | `specs/ammon.py` | spiral shells (`K.spiral`), feelers |

## Step 2. Start from the working template

```bash
python3 Tools/Build/fauna_kit/fauna.py new <name>
python3 Tools/Build/fauna_kit/fauna.py check <name>      # passes already
```

Change ONE thing, run the check, repeat. Never write a whole body before the first check.

## Step 3. Place the crystal, then the spindles

**The crystal** is always at `[0, 0, 0]` and 3.55 across. You never write it. The creature travels along **+z**, so
the body trails toward **-z**. `P(radius, z)` means: `radius` out from the travel axis, `z` along it.

**Spindles (limbs)** are smooth curves: `K.grown(name, parent, points, girth=(root, tip), design="...")`.

| rule | number |
|---|---|
| points on a limb | 3 to 6. Two makes a stick |
| a ROOT limb (`parent None`) starts | within **3.2** of `[0, 0, 0]`. Usual start: `[0, 0, -1.6]` |
| any other limb starts | ON its parent, within **1.5** of the parent's curve |
| nothing reaches ahead of the crystal | every z is 0 or less, unless SPEC has `"lead": n` (a bell, lobes). `n` is at most a quarter of the body's length |
| the body repeats | `N` times round the axis, N is 2 or more. Write a limb ONCE inside `for k, a in enumerate(K.angles(N)):` |
| a limb on the axis | x and y are exactly 0 |
| girth | `(root, tip)`: 1.0 to 1.7 for a core, 0.4 to 0.8 for an arm, 0.15 to 0.3 for a line, tip a third of root or less. A shell is the other way: `(3.2, 0.5)` |
| names | no leg, belly, back, eye, antenna, foot, head, torso, thorax, abdomen, wing. Say spar, rib, lobe, vane, arm, line |

Order the list as parents first: `"spindles": [core] + limbs + [tail]`.

**A membrane** is woven between two limbs that run the same way (root to tip):
`"membranes": [{"name": "Web0", "a": "Lead0", "b": "Trail0", "across": 6}]`.

Pick each limb's `design` from `/spindle` section 2. Give a limb `sway` so it moves (`/spindle` section 3).

## Step 4. Place the prisms

Prisms are the creature's mass and what kills it. They are boxes that ride a limb.

```python
at, axis = K.along(limb, 0.6)          # a point 60% down the limb, and the way the limb runs there
prisms.append({"name": f"Plate{k}", "spindle": limb["name"], "kind": "health", "at": at, "size": [0.5, 1.6, 3.0], "axis": axis})
```

| rule | number |
|---|---|
| health prisms | 3 or more, on the limbs FURTHEST from the crystal first (it starves from the outside in) |
| distance from the crystal's centre | **4.33** or more |
| the prism's front | behind **z = -1.78** |
| distance from its own limb | **2.5** or less. Always take `at` from `K.along` of the limb named in `"spindle"` |
| size `[thin, wide, long]` | every side from **0.5 to 10**. Long lies along the limb |
| prisms never overlap | move one to a different `t` |
| the STING | exactly as the template: `"kind": "danger", "role": "stinger"`, 4 or longer and 6 times its thickness, on a limb NOTHING grows from, and nothing reaches further back than it |
| several stings | allowed when they are copies round the axis (same radius and z through `P(...)`), each on its own end limb |

Use `K.elements(prisms)` for `"elements"`. Never type those numbers. (It gives the four body sizes by the game's
law: Time and Charge 1, Mass grown until the heart's swing clears its shields and never under 1, Space 1.6477 x Mass.)

`"possession"` needs `top_speed`, `mouth_reach`, `attack`. `attack` is `"whip"` only when three or more limbs grow
from ONE limb; otherwise `"lunge"`.

## Step 5. Check, and do what it says

```bash
python3 Tools/Build/fauna_kit/fauna.py check <name>
```

It prints every broken law with a **FIX** line, a table of where each prism sits against the numbers above, and the
mesh size (bar: 5,072 vertices). Fix the FIRST failure and run it again. When it says PASS:

```bash
python3 Tools/Build/fauna_kit/fauna.py sheet <name> <scratchpad>/<name>.png
```

Open the picture. Ask: from the side, is it the shapes from step 1? Is anything straight that should curve (add a
point, move a middle point off the line)? Is it a stick figure (use a membrane, barbs or a richer design)?

## Step 6. Put it in the lab

1. Add the name to `NEW_SPECIES` in `Tools/Build/fauna_kit/lab_export.py`.
2. Run:
```bash
python3 Tools/Build/fauna_kit/lab_export.py
node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/FaunaLab.html
python3 Tools/Build/fauna_kit/fauna.py check <name> --silhouette     # about four minutes
```
3. The silhouette line must not say TOO ALIKE. If it does, change the OUTLINE (wider, longer, limbs opened out, a
   different plan), never the detail. If it still fails after two tries, keep it, say so, and let the person decide
   (`silhouette.ACCEPTED` records an outline a person accepted).
4. Publish the lab to the SAME artifact (`Docs/Studios/FAUNA_LAB.md` has the link). Never a new one.

## Step 6.5. The four element forms (a creature is four creatures)

Every creature spawns as one of four elements, and each must be told from the others at a glance. You get all four
from the one spec, in three layers. Do them in this order.

**Layer 1, the game's law. You do nothing.** The game changes the prisms and the size by itself: Time is the base
body, 1.4 x faster; Charge makes every prism danger; Mass shields every plain prism; Space is 1.6477 x the Mass body,
thinned, its prisms needles. Never copy any of that into a spec.

**Layer 2, the element's style. You do nothing.** `elements.py` changes the WIRE of every limb by rule (Charge opens
the braid and adds sparks, Mass adds a strand and thickens it into tubes and slows the beat, Space drops a strand,
makes ribbons, weaves membranes sparser, and applies the law's thinning). Look at what you got:

```bash
python3 Tools/Build/fauna_kit/elements.py                              # the four mesh sizes; every form under 5,072
python3 Tools/Build/fauna_kit/elements.py --sheet <name> <scratchpad>/<name>_forms.png
```

**Layer 3, the creature's own anatomy for each element. THIS is your work, and it is what makes a flagship.** Give
each element ONE feeling and one or two things only this creature grows for it. Write a small function for each and
put them in the spec as `"forms"` (copy `comet`, `storm`, `leviathan`, `ghost` in `specs/ray.py`):

| element | the feeling to aim for | what to add (the Ray's answer) | what NOT to do |
|---|---|---|---|
| Time | quick, joyful | things that STREAM behind and show speed (two wingtip streamers) | do not change the body: Time is the base body |
| Charge | beautiful and dangerous | points: thorns facing forward, a talon, a forked tail (`barbs` with a stated `"dir"`) | no blobs; danger is sharp |
| Mass | heavy, calm, old | structure: ribs between two edges (`to_limb`), a broad tail fan (a membrane between two short limbs) | nothing thin or fluttering |
| Space | vast, thin, lonely | things that HANG and drift: long strands with a membrane between neighbours | do not fill the outline in (see the silhouette step) |

```python
"forms": {"Mass": {"feeling": "one line: the name of the form and the feeling",
                   "add": [K.grown(...), ...],                       # new limbs; parent each to a limb of the body
                   "membranes": [{"name": "FlukeWeb", "a": "Fluke0", "b": "Fluke1", "across": 7}],
                   "limbs": {"Lead0": {"barbs": {...}}, "Core": {"barbs": False}},   # change a limb of the body (False: no sparks here)
                   "unity": {"wires": {"VaneA": ["Rib00", ...]}}}}   # which moving part each new limb rides (Into Unity, step B)
```

Rules for a form (the self-test holds them):
- **Never touch the prisms.** Not their number, seats or sizes. An element changes what a prism IS, by the game's law.
- **Never remove or re-route a limb that carries a prism.** Add limbs; change only a limb's wire (`barbs`, `strands`, `glow`).
- Build every added limb inside the `for k, a` loop with `P(radius, z)`, like the body, so it has its twin.
- A sting's limb must stay childless: hang a new tail limb from the limb BEFORE it (the Ray's tines leave the Core).
- A limb that ends on another limb (a rib) says `rib["to_limb"] = "<that limb>"` so it stays joined when both move.
- A barb's side is stated: `"dir": [0, 0, 1]` forward, `[0, 0, -1]` back, or the way out along the limb. Without it
  the barbs of the two twins point opposite ways.

Then check, in this order, and fix the first thing that fails:

```bash
python3 Tools/Build/fauna_kit/elements.py --self-test        # the ray's forms keep the rules above
python3 Tools/Build/fauna_kit/elements.py --check            # every form under the mesh bar and inside the laws
python3 Tools/Build/fauna_kit/elements.py --silhouette <name>   # every form 0.41 or more from every OTHER body
python3 Tools/Build/fauna_kit/elements.py --sheet <name> <scratchpad>/<name>_forms.png      # LOOK at it
```

| what you see | do |
|---|---|
| a form is over the bar | on what the form ADDS: `step=2.0` on straight struts, fewer barbs, `across` 5 on a new membrane |
| `UNDER the bar` on a form | what the form adds filled the outline in. Move it so it leaves gaps (the Ray's veil hung along the whole hind edge read as the Sparrow hull; hung from the outer half only, as sleeves, it passed) |
| two forms look the same on the sheet | one of them has no anatomy of its own yet. Give it the one thing from the table |
| `L5 stinger ... has child limbs` | you hung a new limb from the sting's limb. Hang it from that limb's parent |

A creature with no `"forms"` block still has four forms (layers 1 and 2). That is enough for a first pass in the lab
and NOT enough to ship: a creature that goes to Unity has its own anatomy for all four.

## Step 7. Record and hand back

- Add a short round section at the top of `Docs/Studios/FAUNA_LAB.md`: what you made, the check's result, the mesh
  size, the silhouette distance, what a person must judge.
- Run before committing: `celestial.py --all`, `spindle.py --budget --check`, `elements.py --check`,
  `recreate.py --check`, `lab_export.py --check` (all under `Tools/Build/fauna_kit/`), then `python3 Tools/Build/approvals.py check --me <person>`.
- Say plainly: it is a lab proposal, nothing is in Unity, and whether it is beautiful is the person's to judge.

## When something goes wrong

| what you see | do |
|---|---|
| a FAIL you fixed comes back | you changed one copy by hand. Write the part once inside the `for k, a` loop |
| `L6 ... has no partner` | a coordinate was typed instead of made with `P(radius, z)`; or a point "on the axis" has a tiny x or y |
| `L5 ... not the trailing extremity` | another prism or sting reaches further back. Lengthen the sting's limb or shorten the other |
| the mesh is OVER | on the heaviest limbs: `section=2`, then a larger `step`, then fewer barbs |
| the body looks like sticks | each limb needs 4+ points with the middle ones OFF the straight line; add a membrane or barbs |
| the lab page errors after export | run `verify_lab.cjs` and read the first error; a limb listed before its parent is the usual cause |

## Into Unity (decided 2026-11-17: all five; the Ray's first step is done)

Garrett: all five grown creatures (Ray, Nettle, Plume, Lantern, Ammon) go to Unity. Only do this when a person asks.

**Step A, the body in Unity (a tool does it; done for the Ray's four elements on 2026-11-17):**

```bash
python3 Tools/Build/fauna_kit/unity_port.py <name>           # writes the assets
python3 Tools/Build/fauna_kit/unity_port.py <name> --check   # the assets are still what the spec makes
python3 Tools/Build/check_network_prefab_hashes.py
python3 Tools/Build/author_lifeform_heart_collection.py --check
python3 Tools/Build/colour_trace.py --check
python3 Tools/Build/fauna_kit/prefab_read.py Assets/_Prefabs/FloraAndFauna/<Species>.prefab --root   # read it: prisms under Body, a crystal at the root
```

It writes ONE PREFAB FOR EACH ELEMENT (the Time form keeps the creature's name; the others are `<Species>Charge`,
`<Species>Mass`, `<Species>Space`), each with its own meshes (the QuadFish's prefab re-fitted: meshes under one
Spindle, the prisms its children, the heart at the root), one data asset, the four element configs (each pointing at
its own prefab, with the body size from the spec's `"elements"`) and four network list entries. It adds the creature
to NO cell, so nothing spawns it.

**The Space prefab is authored already thinned, and says so.** The game thins a Space body when it spawns, one mesh
at a time. On a jointed body that tears the wing at its hinges (`python3 Tools/Build/fauna_kit/erosion.py <Species>`
measures it: 6.7 u on the Ray). So the Space form is thinned by the same law when it is made, its prisms are written
as the law's needles, and its prefab carries `spindleAuthoredEroded: 1` (a field on `Fauna`), which tells the game to
leave it alone. The Space config still says `ErodeSpindle: 1`, like every species. `unity_port.py` does all of this
and checks it; you only need to know it when a person asks why the Space prefab differs. Then add an entry to `Docs/UNITY_VERIFICATION_CHECKLIST.md` (copy the
Ray's: it lists what a person must check in the editor), because nothing here opens Unity.

A creature whose body is over 65,535 vertices or whose spec fails `fauna.py check` is refused.

**Step B, joints (the Ray has them: copy its `"unity"` block in `specs/ray.py`).** Without the block the body is one
rigid mesh that the game can only lunge. With it, `unity_port.py` cuts each named limb at its hinges and hangs the
pieces from joints that `GrownLimbDriver` turns:

```python
"unity": {"limbs": [
    {"name": "VaneA", "wires": ["Lead0", "Trail0", "Web0"],   # the limbs and membranes that make this one moving part
     "cut": "x", "hinges": [3.0, 13.0, 24.0],                 # where it is cut, walking OUT from the body (x: sideways, z: along the tail)
     "axis": [0, 0, 1], "degrees": [9.0, 11.0, 13.0],         # each joint's turning axis and sweep; negative mirrors a twin
     "lag": 0.9}]}                                            # how far behind each joint runs: that is the wave
```

A limb a FORM adds rides a moving part in one of two ways (both in the form's `"unity"`):
- `"wires": {"VaneA": ["Rib00", "Talon0"]}`: it is cut and carried with that part (ribs, thorns, a veil, tail tines).
- `"limbs": [{"name": "StreamA", "wires": ["Stream0"], "cut": "z", "hinges": [-19.0, -32.0, -46.0],
  "origin": P(34.5, 0.0), "parent": "VaneA2", "axis": [1, 0, 0], "degrees": [6.0, 9.0, 13.0], "lag": 1.1}]`: it is a
  moving part of its own, hung from another part's joint (`parent`), with its hinge line at `origin` (a streamer on
  a wingtip). Use this only for something long that should whip; each joint is one more object in the game.

Rules: a hinge is a straight cut, so it suits flat or straight limbs (a vane, a tail), not a coil; three or more
limbs under the Body make the attack a whip; give mirrored twins opposite `degrees` so they move together. A
species with a move of its own gets a small class (`RayFauna` is the example) named in `"klass"`; that is C#, so run
`bash Tools/Build/unity_refcompile/run.sh` (after `python3 Tools/Build/unity_refcompile/dotnet_from_nuget.py` once).

**Step C, the Spawn Matrix.** Add one row and one description in `Tools/Build/author_spawn_matrix_roster.py` (copy
the Ray's), run it, then `--check`. That is where a person inspects one species alone. A cell's own spawn profile is
`/ecology`'s (it changes the collider budget): do not add a creature to a cell from this skill.

**Not available yet for any grown creature:** the lab's ripple and travelling light (needs the spindle shader; so in
Unity a veil or a streamer moves only at its joints), a carried up, and a Codex page.
