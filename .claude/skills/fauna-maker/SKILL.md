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

**4. What happens to it when it has no up?** This is where most creatures are lost, so answer it before you draw.
A HyperSea body has no belly and no back: everything off the travel axis exists N times round it (N is 2 or more).
A real animal's one-sided feature (a seahorse's curl, a shark's fin, a snail's shell) therefore appears N times.
Decide now what that looks like and whether it still reads as the animal:
- its signature SHAPE goes on the axis if it can (a tail straight behind, a snout straight ahead);
- what cannot go on the axis is repeated, and the repetition is the design (the Ammon is TWO mirrored coils, like a
  ram's horns; a seahorse might be three curled tails round one trunk, a triskelion);
- if neither keeps the animal recognisable, choose a different animal or a different signature, and say so. A body
  that passes every check and is not the animal is a failure; tell the person it is not there yet.

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
| points on a limb | 4 to 6, the middle ones OFF the straight line between the ends. Two makes a stick, three makes a bent stick |
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
| the STING | exactly as the template: `"kind": "danger", "role": "stinger"`, 4 or longer and 6 times its thickness, on a limb NOTHING grows from, and nothing reaches further back than it. Its back end is at `at` z minus half its length |
| several stings | allowed when they are copies round the axis: `"at": P(radius, z)` with the same radius and z, `"axis": [0, 0, 1]` (not the limb's own direction, or the copies cross), each on its own end limb |

Use `K.elements(prisms)` for `"elements"`. Never type those numbers. (It gives the four body sizes by the game's
law: Time and Charge 1, Mass grown until the heart's swing clears its shields and never under 1, Space 1.6477 x Mass.
Only when a person asks for a bigger Mass form: `K.elements(prisms, mass=1.25)`; the Ray's is.)

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
point, move a middle point off the line)? Is it a stick figure (use a membrane, barbs or a richer design)? Would a
person name the animal from this picture alone? If not, go back to step 1's fourth question; do not go on.

Then its outline against every other body, before it is in the lab:

```bash
python3 Tools/Build/fauna_kit/elements.py --silhouette <name>     # its first line is the BASE body: its distance, its nearest body, the bar
```

If the base body is under the bar, change its OUTLINE now (wider, longer, limbs opened out, a different plan); the
forms in step 6.5 cannot rescue a base that is too like another body.

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
from the one spec, in three layers. Do them in this order. The worked example is the Ray: read `comet`, `storm`,
`leviathan`, `ghost` and the `"forms"` line in `specs/ray.py` before you write yours.

**Layer 1, the game's law. You do nothing.** The game changes the prisms and the size by itself: Time is the base
body, 1.4 x faster; Charge makes every prism danger; Mass shields every plain prism; Space is 1.6477 x the Mass body,
thinned, its prisms needles. Never copy any of that into a spec.

**Layer 2, the element's style. You do nothing.** `elements.py` changes the WIRE of every limb by rule (Charge opens
the braid and adds sparks, Mass adds a strand and thickens it into tubes and slows the beat, Space drops a strand,
makes ribbons, weaves membranes sparser, and applies the law's thinning). Look at what you got before you add anything:

```bash
python3 Tools/Build/fauna_kit/elements.py --check <name>                 # the four mesh sizes
python3 Tools/Build/fauna_kit/elements.py --sheet <name> <scratchpad>/<name>_forms.png
python3 Tools/Build/fauna_kit/elements.py --silhouette <name>            # the BASE body's distance and nearest body: write both down
```

**Layer 3, the creature's own anatomy for each element. THIS is your work, and it is what makes a flagship.** Give
each element ONE feeling and one or two things only this creature grows for it:

| element | the feeling to aim for | what to add (the Ray's answer) | what NOT to do |
|---|---|---|---|
| Time | quick, joyful | things that STREAM and show speed, flung OUT from the widest part (two wingtip streamers) | never change the body: a Time form may only ADD (the tool refuses a `"change"` on Time) |
| Charge | beautiful and dangerous | points: thorns facing forward, a talon, a forked tail (`barbs` with a stated `"dir"`) | no blobs; danger is sharp |
| Mass | heavy, calm, old | structure: struts between two limbs (`to_limb`), a broad fan (a membrane between two short limbs) | nothing thin or fluttering |
| Space | vast, thin, lonely | things that HANG and drift from the OUTER parts, with gaps between them | never one sheet across the whole body; a membrane the form adds is never bigger than the body |

Write one small function for each form, above `SPEC`, and name them in the spec:

```python
N = 5                                              # the spec's own "symmetry": every form uses the SAME N
arms = [K.find(limbs, f"Arm{k}") for k in range(N)]   # pick the body's limbs by NAME, never by place in the list

def leviathan():
    add, change = [], {}
    for k, a in enumerate(K.angles(N)):            # one copy for EVERY angle, or law L6 fails ("has no partner")
        P = lambda r, z, a=a: K.polar(r, a, z)     # K.angles gives radians, which is what K.polar takes
        at = K.along(arms[k], 0.6)[0]              # a point 60% of the way down a limb of the body ...
        r, z = K.rz(at)                            # ... as (radius, z), ready for P
        add.append(K.grown(f"Brace{k}", "Calyx", [P(1.0, -4.0), P(r * 0.5, z + 1.0), P(r, z)], girth=(0.3, 0.2), design="rib", step=2.0))
        change[f"Arm{k}"] = {"barbs": {"count": 12, "length": [4.0, 1.0], "angle": 58.0, "sides": 2, "dir": "tangent", "flutter": 0.1}}
    return {"feeling": "the leviathan: reverence. Braced and still.",      # one short line, 70 characters at most
            "add": add,                                                    # new limbs
            "membranes": [],                                               # {"name", "a", "b", "across"} between two limbs
            "change": change}                                              # the WIRE of limbs the body already has

SPEC = { ..., "forms": {"Time": comet(), "Charge": storm(), "Mass": leviathan(), "Space": ghost()}, ... }
```

A form needs a `"unity"` entry ONLY when the spec already has a `"unity"` block (a creature that is in Unity). Skip it
otherwise.

**Rules for a form. The tool refuses most of these; knowing them saves the round trip.**
- **Never touch the prisms.** Not their number, seats or sizes. An element changes what a prism IS, by the game's law.
- **Add limbs; never move one.** `"change"` may set a limb's wire only: `barbs`, `strands`, `step`, `twist`, `glow`,
  `sway`. A changed key REPLACES that key whole, so a `barbs` change gives every barbs key (count, length, angle,
  sides, dir, flutter). `"barbs": False` means no barbs and no sparks on that limb.
- **Keep the species' look.** A limb keeps at least half its barbs in every form (a feather is not a whip). Turn
  them, shorten them, still them (`flutter` 0.1); do not strip them.
- **Where to hang what you add.** From a limb of the body, with three limits:
  1. never from a limb that carries a sting. If EVERY arm carries a sting, hang from the root limb (the first one):
     a limb must START on its parent, so run each new limb out from the root BETWEEN the arms
     (`K.angles(N, phase=<half an arm step, in degrees>)` puts the copies in the gaps);
  2. if the spec's `"attack"` is `"lunge"`, no limb may end up with three or more children: hang from a limb's tail
     or from outer limbs that have no children yet (law L11);
  3. a limb that ends on another limb (a strut) says `strut["to_limb"] = "<that limb's name>"` so it stays joined.
- **How far it may reach.** The whole form at most 2.5 x the body's own longest side (the check prints REACH when it
  is more: the creature must still be the creature). Not ahead of the spec's `"lead"` (law L1: for forward points on a body with a small lead,
  use barbs with `"dir": [0, 0, 1]` on limbs already there). Not further back than the sting: `fauna.py check` prints
  the sting's z in its table; end what you add ahead of it.
- **A barb's side is stated**: `"dir": [0, 0, 1]` forward, `[0, 0, -1]` back, `"tangent"` round the axis, or the way out
  (`K.polar(1.0, a, 0.0)`). Without it the barbs of mirrored twins point opposite ways.

**The outline (the silhouette check), which is where most forms fail first.** It compares each form's coarse OUTLINE,
seen from three sides, with every other creature and vessel. It measures filled area and reach, so:
- thin lines close to the body change nothing; they neither help nor hurt;
- something hung straight behind the body, or a membrane across the whole body, FILLS the outline in and fails
  (the Ray's first veil read as the Sparrow hull; a jelly's long central tail read as the Urchin);
- what passes is spread OUTWARD: flung from the widest part of the body, 20 u or more beyond it, with gaps between;
- look at which body the check names. If it is a round one (the Urchin, the Rhino), make the form less round: longer
  one way, or with a few far-flung parts. If it is a delta (the Sparrow), leave notches;
- a membrane between two hanging strands is the usual culprit: narrow it, shorten it, or drop it.

A species whose BASE outline a person accepted under the bar (the Plume, the Nettle) is held to its base instead: the
check prints the number each form must reach (reaching it exactly passes). Never buy a pass by stripping the body's
own barbs or limbs. When the outline rule and the size rule pull against each other, the size rule wins: keep the form
under 2.5 x the body, report the outline number, and let the person decide.

**Check, in this order, and fix the first thing that fails:**

```bash
python3 Tools/Build/fauna_kit/fauna.py check <name>                      # RESULT must be PASS; a form's broken law prints BREAKS A LAW and a FIX
python3 Tools/Build/fauna_kit/elements.py --check <name>                 # all four at or under 5,072; read the "lightened" note
python3 Tools/Build/fauna_kit/elements.py --silhouette <name> --form Space   # one form, quick; then without --form for all four
python3 Tools/Build/fauna_kit/elements.py --sheet <name> <scratchpad>/<name>_forms.png      # LOOK at it
python3 Tools/Build/fauna_kit/elements.py --self-test
```

| what you see | do |
|---|---|
| `lightened x1.4` beside a form | not a failure: the form was over 5,072 vertices and its rings were spaced further apart to fit. Up to x1.6 is fine. The vertex count is shown AFTER lightening, so it can go DOWN when you add: watch the factor, not the count |
| lightened over x1.6, or still over the bar | the body is heavy (a base over about 3,500 leaves a Mass form little room). In this order: `step=2.0` on straight struts you add; add fewer things (3 a side at most); `"change"` a heavy limb of the body to a larger `step` in THIS form; reduce barbs, never below half |
| `UNDER 0.41` (or the number printed) on a form | read "The outline" above: spread what the form adds outward, or drop the membrane |
| `BREAKS A LAW: L6` | a copy is missing: loop `K.angles(N)` with the spec's own N |
| `BREAKS A LAW: L5` | you hung a limb from a sting's limb, or it reaches behind the sting |
| `BREAKS A LAW: L1` | what you added reaches ahead of the spec's `"lead"` |
| `BREAKS A LAW: L11` | you gave one limb a third child on a `"lunge"` body |
| `Time is the base body` | remove `"change"` from the Time form; add instead |
| two forms look the same on the sheet | one of them has nothing of its own that shows at that size. Make its one thing BIGGER, not more numerous |
| `BREAKS A LAW: REACH` | the form is over 2.5 x the body. Shorten its longest parts |
| a membrane the form adds is bigger than the body on the sheet | halve it. The creature must still be the creature |
| you need to know what a limb costs | a long ribbon line (`whip`, `vein`) is about 4 vertices a unit of length; a tube strut 9; a barb 8. `fauna.py check` prints each form's total |

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

## Into Unity (decided 2026-11-17: all five; the Ray is done, in four element prefabs)

Garrett: all five grown creatures (Ray, Nettle, Plume, Lantern, Ammon) go to Unity. Only do this when a person asks.
**`references/RAY.md` is the Ray taken the whole way, with what went wrong at each step: read it first.**
The other four wait for a person to open the Ray in the editor once (the first asset of a new kind is looked at
before the rest are made: `/asset-surgery` section 0).

A creature goes to Unity only with its own four forms (step 6.5) and `elements.py --check <name>` passing.

**Step A, the body in Unity (a tool does it):**

```bash
python3 Tools/Build/fauna_kit/unity_port.py <name>           # writes the assets (and removes ones it no longer makes)
python3 Tools/Build/fauna_kit/unity_port.py <name> --check   # the assets are still what the spec makes
python3 Tools/Build/fauna_kit/unity_port.py --self-test
python3 Tools/Build/check_network_prefab_hashes.py
python3 Tools/Build/author_lifeform_heart_collection.py --check
python3 Tools/Build/verify_fauna_heart_seat.py
python3 Tools/Build/colour_trace.py --check
```

Then RUN it, because a file check cannot see behaviour: load `/amoebius`, build the engine, spawn each element's
config through the spawner's own calls (`BindSpeciesData`, `Initialize`, `AssignLineage`, `FaunaNetworkSync.ServerSpawn`)
and read back: no error or exception; every prism active; Mass plain prisms shielded; Charge prisms all danger; the
Space root at its scale with its meshes' vertex counts unchanged. The Ray's run found a fault in the game itself.
Say in the checklist entry what the run showed and that Amoebius is not Unity (it does not draw the spindle shader).

It writes ONE PREFAB FOR EACH ELEMENT (the Time form keeps the creature's name; the others are `<Species>Charge`,
`<Species>Mass`, `<Species>Space`), each with its own meshes and its own material, one data asset, the four element
configs (each pointing at its own prefab, with the body size from the spec's `"elements"`) and four network list
entries. It adds the creature to NO cell, so nothing spawns it. Then write the entry in
`Docs/UNITY_VERIFICATION_CHECKLIST.md` (copy the Ray's), because nothing here opens Unity.

How the body MOVES in Unity, in three lines you can repeat to a person:
- **The wave is in the shader.** Every mesh vertex carries its own wave (the numbers the lab plays); the spindle
  shader adds it (`SpindleGrownSway`). You write nothing for this: give limbs `sway` in the spec (`/spindle` section 3).
- **Prisms ride it on the CPU.** `GrownLimbDriver` carries each prism along its limb's wave, because a prism is a collider.
- **Joints are only for the attack** (step B).

**The Space prefab is authored already thinned, and says so.** The game thins a Space body when it spawns, one mesh
at a time, which tears a body cut into jointed pieces (`python3 Tools/Build/fauna_kit/erosion.py <Species>` measures
it). So the Space form is thinned by the same law when it is made, its prisms are written as the law's needles, and
its prefab carries `spindleAuthoredEroded: 1`. The Space config still says `ErodeSpindle: 1`, like every species.
The tool does all of this and checks it.

A creature whose body is over 65,535 vertices or whose spec fails `fauna.py check` is refused.

**Step B, joints: only so the attack can WHIP (the Ray has them: copy its `"unity"` block).** With no `"unity"`
block the body is whole and the game's attack lunges it. For a whip, name three or more moving parts; `unity_port.py`
cuts each at its hinges and hangs the pieces from joints the attack turns:

```python
"unity": {"limbs": [
    {"name": "VaneA", "wires": ["Lead0", "Trail0", "Web0"],   # the limbs and membranes that make this one moving part
     "cut": "x", "hinges": [3.0, 13.0, 24.0],                 # where it is cut, walking OUT from the body (x: sideways, z: along the tail)
     "axis": [0, 0, 1], "degrees": [9.0, 11.0, 13.0],         # each joint's turning axis; degrees and lag are used only by the fallback below
     "lag": 0.9}]}
```

Rules: a hinge is a straight cut, so it suits flat or straight limbs (a wing, a tail), not a coil. A limb a FORM adds
names the part it rides: `"unity": {"wires": {"VaneA": ["Rib00", "Talon0"]}}` in the form. A species with a move of
its own gets a small class (`RayFauna` is the example) named in `"klass"`; that is C#, so run
`bash Tools/Build/unity_refcompile/run.sh` (after `python3 Tools/Build/unity_refcompile/dotnet_from_nuget.py` once).

**If the shader's wave is wrong in the editor** (a person says the body is still, or pink): put `"sway": "joints"` in
the spec's `"unity"` block and run `unity_port.py <name>` again. The body goes back to the shared SpindleMaterial and
`GrownLimbDriver` beats the joints on the CPU instead. Tell the person which one is in the build.

**Step C, the Spawn Matrix.** Add one row and one description in `Tools/Build/author_spawn_matrix_roster.py` (copy
the Ray's), run it, then `--check`. That is where a person inspects one species alone. A cell's own spawn profile is
`/ecology`'s (it changes the collider budget): do not add a creature to a cell from this skill.

**Not available yet for any grown creature:** a Codex page. The lab's light running down a limb is the lab's stand-in
for the game's own lace on the spindle shader; it is not ported and is not meant to be.
