---
name: fauna-maker
description: Make a NEW Cosmic Shore creature step by step, from "make a <animal>" to a body a person can ride in the Fauna Possession Lab. The short procedural path for any model - where the crystal goes, how to place spindles (limbs) and prisms with exact numbers, a working template to start from, ONE command that checks everything and prints a fix for each failure, and how to put the creature in the lab. Use on "/fauna-maker", "make a creature / fauna / lifeform", "add a <animal> to the lab", "more fauna", "a new species", or whenever asked for a new creature body. For shaping the wire of a limb load /spindle. For why the rules are what they are, and anything that changes a law, load /fauna-creation.
---

# Fauna maker: a creature in seven steps

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

Use `K.elements(prisms)` for `"elements"`. Never type those numbers.

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

## Step 7. Record and hand back

- Add a short round section at the top of `Docs/Studios/FAUNA_LAB.md`: what you made, the check's result, the mesh
  size, the silhouette distance, what a person must judge.
- Run before committing: `celestial.py --all`, `spindle.py --budget --check`, `recreate.py --check`,
  `lab_export.py --check` (all under `Tools/Build/fauna_kit/`), then `python3 Tools/Build/approvals.py check --me <person>`.
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

**Step A, the body in Unity (a tool does it; done for the Ray on 2026-11-17):**

```bash
python3 Tools/Build/fauna_kit/unity_port.py <name>           # writes the assets
python3 Tools/Build/fauna_kit/unity_port.py <name> --check   # the assets are still what the spec makes
python3 Tools/Build/check_network_prefab_hashes.py
python3 Tools/Build/author_lifeform_heart_collection.py --check
python3 Tools/Build/colour_trace.py --check
python3 Tools/Build/fauna_kit/prefab_read.py Assets/_Prefabs/FloraAndFauna/<Species>.prefab --root   # read it: prisms under Body, a crystal at the root
```

It writes one body mesh, the prefab (the QuadFish's re-fitted: one mesh under one Spindle, the prisms its direct
children, the heart at the root), a data asset, four element configs and the network list entry. It adds the
creature to NO cell, so nothing spawns it. Then add an entry to `Docs/UNITY_VERIFICATION_CHECKLIST.md` (copy the
Ray's: it lists what a person must check in the editor), because nothing here opens Unity.

A creature whose body is over 65,535 vertices or whose spec fails `fauna.py check` is refused.

**What Step A does not give the creature** (each is later work, with `/fauna`, `/ecology` and `/asset-surgery`
loaded, and each needs C# and the verify rule): the per-vertex wave and travelling light (it sways by the game's one
shear); limbs the attack can whip (one mesh has none, so it lunges); a carried up; its own ride from the spec's
`possession` block; a place in a cell's spawn profile, the Codex and the Spawn Matrix (`/ecology` first: that
changes the collider budget).
