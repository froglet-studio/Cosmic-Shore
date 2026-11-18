# Fauna Possession Lab

**Page:** `Docs/Studios/FaunaLab.html` · **Artifact:** https://claude.ai/artifact/PMh24N63DgLuEJphkrbcV4 ·
**Kit:** `Tools/Build/fauna_kit/` · **Skill:** `/fauna-creation` · **Branch:** `cece/peaceful-babbage-tsg0kd`

Garrett, 2026-11-15: "our goal is great fauna, but first our goal is the skill that can make great fauna ... give me a
lab that allows me to control the existing fauna in the game like the jewel wasp does. then get your fauna creation
skill as good as you can make it, and use it to make some new fauna that will broaden the fun a jewel wasp can have. i
should be able to try out each new design in the lab, and give feedback on your fauna to help steer a better tool.
give me a vessel, ark, and prism cluster to attack."

## The brief

- **The decision this lab exists to make:** which creatures are fun to ride as a Jewel Wasp, and what the fauna kit
  must learn next to make better ones. One column per kind of fun (below); a creature is judged by a person riding
  it, and the columns say what the numbers can.
- **Fundamental it serves:** possession (the Jewel Wasp, `JEWEL_WASP.md`): any creature is a vessel for a while.
- **Rules it obeys:** the celestial laws (`/fauna-creation` section 2), PvP is petals only (a strike is a scored hit),
  a game mode never changes a vessel (the wasp's kit is the shipped one; only creatures differ), the ecology's
  invariants (nothing here changes them: nothing here is in the game).
- **The rule it comes closest to breaking:** law L2 (a body is braid spindles). The new creatures keep it. The
  shipped creatures mostly do not (below), and that is theirs to keep until Garrett picks one to redesign.
- **Who judges, and where:** Garrett, on the page, in its Feedback tab.
- **Retire condition for a new creature:** a "cut it" in the feedback log, or a second round in which it still wins
  no column and nobody says it feels good.

## Round 10 (2026-11-18): the body answers its own turning

Garrett, after flying the Ray: "the ripples look good when the ray is moving forward. but its rotations just happen
and the fauna movement doesn't match it ... i want the wings to take on chiral movements that match their rotations".

A body that only rotates is a model on a stick. Every grown creature now reads its own angular velocity, in its own
frame, and answers it four ways. The two sides of the body always do mirrored things, and a body flying straight does
none of them.

| answer | what you see | from |
|---|---|---|
| **Stroke** | the OUTSIDE of a turn beats harder, the inside quieter: the body rows itself round | yaw and pitch rate |
| **Twist** | in a roll the hind edges deflect like ailerons, one side up and the other down | roll rate |
| **Lag** | wingtips, sleeves, streamers and tail trail the rotation, swing past once and settle (a spring) | all three |
| **Bank** | the whole body rolls into the turn and levels out when it ends | yaw rate, and speed |

**One formula, three places.** `SpindleGrownSway.hlsl` in the game's shader (compiled and proved:
`verify_spindle_grown_sway.py` T6 to T9), `GrownLimbDriver.AnswerTheTurn` on the CPU (measures the rotation, runs the
spring, hands the numbers to the body's renderers and carries the prisms by the same formula), and this page
(`stepTurn`, and the same terms in its vertex stage). The numbers are `elements.TURN`; an element scales the lag and
the spring (Time quick, Mass heavy, Space drifting); a spec may state its own `"turn"`.

**Tune it here.** The **Body** tab has four multipliers: Lag, Stroke, Twist, Bank. Fly, set them where the Ray feels
alive in a turn, and say the numbers; they become the creature's own.

**Measured on this page** (the Ray, Time form, sticks hard over): a steady yaw of 1.8 rad/s gives a bank of 34
degrees, the inside wing beating 0.45 x and the outside 1.55 x, the tips trailing 0.20 rad; 1.5 s after release
everything is back within 0.03. A roll of 1.9 rad/s deflects the hind edges 0.23 rad at the tip.

**Run in Amoebius** (the game's own code on the prefabs; it does not draw the spindle shader): 600 frames a creature,
0 errors, 0 exceptions; the driver carries 10 skins and 9 rider homes; the six numbers read back from a skin's
property block; a free-swimming Ray's own turns reach 1.0 to 1.4 rad/s and bank it 18 degrees (Time) and 24 (Mass),
the body's roll equal to the driver's bank; held still, all five answers are exactly 0 and the riders sit on the wave
alone; Mass still shields 8 of 8.

**Not modelled:** the turn does not change how the creature FLIES (its path, its turn rate): it is how the body
looks while it does. In the game a Ray's renderers carry a per-renderer property block for this, so they are not
SRP-batched.

## Round 9 (2026-11-17): the Ray taken to completion, and the skill proved on the other four

Garrett: "fix everything and take it all to their natural conclusion so the skill can get the best possible flagship
example both to refer to and develop the necessary skills to reproduce the start to completion process."

**The Ray in Unity now moves as it does here.** Round 8's Ray moved only at its joints. Now:
- **The wave is in the game's spindle shader.** Every mesh vertex carries the sine and cosine parts of its own wave
  (the numbers this page plays), and `SpindleGrownSway.hlsl`, wired into `SpindleGraph` after `SpindleSway`, adds them
  from the platform clock. It is off (0) for every other material, and off is a bit-identical passthrough (the
  shipped file compiled with clang: `Tools/Shaders/verify_spindle_grown_sway.py`). `Docs/ECOSYSTEM.md` 47.8.
- **Prisms ride it** on the CPU (`GrownLimbDriver`), from the same clock and phase. Joints are only what the attack whips.
- **It glides and rolls** under a rider as it does here: `RayFauna` answers a turn at 0.3 and eases speed at 0.9, and
  takes the rider's frame for its up (`IFaunaLeashFrame`, which the Jewel Wasp implements).
- **The leviathan is bigger**: Mass x 1.25, so Space x 2.06 (one number, `K.elements(prisms, mass=1.25)`).
- **A cut body's pieces keep the body's space**: the old shear sway would have torn the tail 2.6 u at its first hinge.
- **The lace runs along each strand** (UV0), as on the shipped braid; it was one stripe.

**The other four creatures have their four forms** (lab only; pick one, then an element):

| creature | Time | Charge | Mass | Space |
|---|---|---|---|---|
| Plume | the comet: five streamers flung out between the plumes | the storm: barbs turned forward, five talons | the leviathan: braces from the cup to the arms | the ghost: five thin lines drifting far out |
| Lantern | the drift: two ribbons from the comb tails | the corona: forward thorns on every comb, two tines | the nave: eight hoops between combs | the veil: four threads with gauze between |
| Ammon | the jet: paired ribbons from each shell | the corona: thorned whorls, two horns | the elder: a hood | the drift: strands and a sleeve from each shell |
| Nettle | the sprite: four flung ribbons | the thornbell: eight forward spears | the abbey: a girdle of hoops, curtains | the veil: four long strands with gossamer |

Each passes the laws, the 5,072 bar (several lightened, x 1.6 at most) and the outline check (`elements.py --check`,
`--silhouette <name>`). **They were made by smaller models following `/fauna-maker` step 6.5, not by hand, and nobody
has judged them.** They are proposals: ride each and say which to keep. They port to Unity by one command each
(`unity_port.py <name> --dry` passes) once the Ray has been opened in the editor.

**The skill was tested by making it do the work** (the point of this round):
- Round 1, four sessions on a smaller model, one creature each, the skill as written. All finished; each reported
  where the skill failed it. Common: nothing said what the outline check measures or which way to move (all four
  found "spread outward" by trial); law failures inside a form did not fail `fauna.py check`; `barbs: False` handed a
  limb back its design's barbs (a bug); nothing said which key replaces what; a heavy body's budget.
- What changed: step 6.5 rewritten (an example function, where to hang, how far to reach, a section on the outline, a
  table from each failure to its fix); `fauna.py check` fails on a form's broken law and prints its fix;
  `elements.py --check <name>`, `--silhouette <name> --form X` with three decimals and the base body's distance; a
  form over the mesh bar lightens itself; the tool refuses a Time form that changes the body, a form that re-routes a
  limb, and a form that strips more than half a limb's barbs (the first Plume had passed its outline by plucking its
  feathers); a form may reach 2.5 x its body at most.
- Round 2, the Plume again with the rewrite: two edit-and-check rounds (19 tool calls became 11), no kit source read,
  feathers kept.
- Round 3, the smallest model, a new creature from "make a seahorse" to four forms: every check passed in about nine
  rounds with no kit source read, **and the body is not a seahorse** (its own verdict, and mine). The no-up law turned
  the one curl into three and nothing in step 1 had made it decide what that should look like. Step 1 now asks. The
  trial body was not kept. What this says: the skill reproduces the PROCESS on a small model; whether the result is
  the animal, and beautiful, still needs the bigger model's eye or a person's.

**The game's own code was run on the four prefabs, in Amoebius** (the engine under `Port/`, which compiles
`Assets/_Scripts` and reads the real assets). It is not Unity and does not run the spindle shader, so it says nothing
about the wave's look. What it did show, 300 to 750 frames an element:
- the engine builds with the changed source; the four prefabs and their 72-byte meshes load at the asset's own counts;
- each element spawns through the spawner's own order and swims, 0 errors, 0 exceptions;
- nine prisms an element, active, sized; **every rider prism sits exactly on seat + riderSin sin t + riderCos cos t**
  on all four (the driver ran and nothing overwrote it);
- RaySpace: root scale 2.0596, girth factor 0.6069, mesh vertex counts identical before and after tuning (not eroded
  twice), plates the law's needles;
- **and one fault nobody had seen: the element armour never landed.** Ray Mass 0 of 8 shielded, Ray Charge only its
  sting danger; then the SHIPPED QuadFish the same (Mass 0 of 4, Charge 0 of 4). `LightFauna` and `Boid` arm the body
  in `Initialize`, before `AssignLineage` rolls the element. Fixed in `Fauna.AssignLineage` (one line); after it, in
  the same harness: QuadFish Mass 4 of 4, Charge 4 of 4; Ray Mass 8 of 8, Charge 9 of 9; Time and Space unchanged.
  **This changes shipped creatures** (`Docs/ECOSYSTEM.md` 65.4; its own entry in the verification checklist).
- Not done there: the Spawn Matrix itself (a fresh profile stops at the sign-in prompts), so the Rays were spawned by
  a throwaway harness calling what the spawner calls. An engine gap noted for Shombith, not changed: its mesh
  importer keeps each UV channel as two floats, so the wave's z would be dropped there (the Ray's wave has none).

**Checked:** every gate in round 8's list, plus `wire_spindle_grown_sway.py --check` / `--self-test`,
`verify_spindle_grown_sway.py`, `wire_spindle_lod.py --check`, `unity_port.py --self-test` (five planted defects).
**NOT checked: the Unity editor.** The shared spindle shader changed: the first hand test is that every other
spindle still draws (`Docs/UNITY_VERIFICATION_CHECKLIST.md`, the Ray, check 0).

## Round 8 (2026-11-17): the Ray's four elements are four creatures

Garrett: "be sure that you create all four elements and follow the rules for doing so. when differentiating by element
really take things to the next level where these four fauna become our flagship fauna ... emotionally provocative
lifeforms of the highest fidelity that define our own unique whimsical style while remaining scalable and performant.
then be sure that all those learnings further upgrade these skills"; "by four fauna i mean each element of ray".

**The four forms** (`specs/ray.py`: `comet`, `storm`, `leviathan`, `ghost`; `elements.py --sheet ray <png>` draws them):

| element | the form | one feeling | what only this form grows | size | vertices | beat |
|---|---|---|---|---|---|---|
| Time | the comet | joy | a streamer from each wingtip, jointed, whipping behind | x 1 | 2,688 | 0.53 Hz |
| Charge | the storm | awe with fear in it | thorns forward on the leading edges, serrated hind edges, a talon at each wingtip, a trident tail | x 1 | 3,650 | 0.44 Hz |
| Mass | the leviathan | reverence | four ribs vaulting each wing, a tail fluke, thick tube wire | x 1 | 4,575 | 0.27 Hz |
| Space | the ghost | longing | a torn sleeve hanging from the outer half of each hind edge; the law's eroded, ribbon wire | x 1.6477 | 2,838 | 0.30 Hz |

**How a form is made: three layers** (`elements.py`).
1. The game's law, untouched: prisms and size (`Fauna.ArmourBody`, `BaseBodyScale`).
2. The element's style, by rule for every grown creature: strands, wire gauge, section, tempo, sparks.
3. The creature's own anatomy for the element: the spec's `forms` block. A form may add limbs and membranes and change
   a limb's wire; it may not touch the prisms or drop a limb of the body's plan (the self-test holds both).

**Three things the rules turned out to need, each measured:**
- **The game's Space erosion tears a jointed body.** It thins each mesh about its own spine; a wing cut into three
  jointed pieces is drawn toward the middle of each piece. `erosion.py` is a port of `SpindleErosion` (pinned to the
  numbers `FaunaElementalFormTests` pins) and measures it: vertices that touched across a hinge end up 6.7 u apart on
  the Ray. That was true of round 7's Ray at Space. Now the Space form is thinned by the same law when it is made
  (each limb's girth by `ErodedGirth`, the wire by 1/1.6477, each prism the law's needle), and its prefab says
  `spindleAuthoredEroded`, a new field on `Fauna` that makes `ErodeSpindle` leave it alone and report the law's girth.
- **The kit shrank Mass.** `K.elements` took the smallest body whose shields clear the heart's swing; for a body whose
  prisms are far from the heart that is under 1 (the Ray: x 0.58, and Space x 0.96). "Let mass have size": Mass is
  never under the base body now (law L8 says so), so the Ray is x 1 at Mass and x 1.6477 at Space. This also moved the
  Nettle, Plume, Lantern and Ammon (lab only).
- **A form can lose its outline.** The first ghost hung a veil along the whole hind edge; it filled the outline in and
  read as the Sparrow hull (0.35, under the 0.41 bar). Hung from the outer half only, as sleeves, it is 0.44.
  `elements.py --silhouette ray`: Time 0.49 (nearest the Manta), Charge 0.45 (Manta), Mass 0.45 (Goby), Space 0.44
  (Sparrow). Between the Ray's own forms: 0.19 to 0.33 (kin; no bar).

**In Unity:** one prefab an element (`Ray`, `RayCharge`, `RayMass`, `RaySpace`), each with its own meshes, the four
configs pointing at their own prefab, four network entries. A limb a form adds rides a moving part (`wires`) or is one
(`limbs` with `parent` and `origin`: the Time streamers hang from the wingtip joints).

**In the lab:** pick Ray, then Time / Charge / Mass / Space under Ride. Each draws its own body at its own size and
tempo; the HUD names the form.

**Checked:** the gates in the list above and the kit's others; the reference compile (0 errors in changed files).
**NOT checked:** the Unity editor (twelve hand tests in `Docs/UNITY_VERIFICATION_CHECKLIST.md`); whether the four
feelings land, which is Garrett's to judge on the page and in the game. **Not modelled in Unity:** the ripple and the
light, so a sleeve, a fluke or a streamer moves only at its joints there.

**Open:** the other four grown creatures have layers 1 and 2 only, and their Mass forms (Charge too on the Lantern
and Nettle) are over the mesh bar (`elements.py`); each needs its own `forms` and a lighter wire before its port.
Mass at x 1 is the law's floor, not a tuned size: a bigger leviathan is one number in `specs/ray.py`'s `elements`
(and Space follows at 1.6477 x).

## Round 7 (2026-11-17): the Ray completed in Unity, and on the Spawn Matrix

Garrett: "take the ray all the way to completion and place it in the spawn matrix".

- **On the Spawn Matrix:** a Ray row with a station for each element (`author_spawn_matrix_roster.py`).
- **Jointed:** each vane is cut into three pieces and the whip into three, each hanging from a joint
  (`specs/ray.py`, the `unity` block). **`GrownLimbDriver`** turns the joints, each a beat behind the one before, so
  the vanes rise and fall in a wave and the whip sways; the beat quickens and deepens with the Ray's speed.
- **Its attack whips:** three limbs under the body (two vanes, the whip), which is what the game's attack looks for.
- **`RayFauna`:** a LightFauna that feeds on the wing. A rider's suction takes what is round its mouth on every side.
- **Checked:** the reference compile reports 0 errors in the files this branch changed; `unity_port.py ray --check`
  (ten mesh assets read back at the spec's triangle count, every prism at its spec position under the body or a
  joint, three attack limbs, the driver and the class on the root); the Spawn Matrix, network, heart and colour gates.
- **NOT checked: the Unity editor.** Ten hand tests are in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
- **Still different from the lab:** a jointed vane bends at three hinges; it does not ripple across its membrane, and
  no light runs down it. Both need the spindle shader. The slow change of speed and a Codex page are not built.

## Round 6 (2026-11-17): the Ray in Unity, step one

Garrett: "yes start the unity port with the ray".

- **`Tools/Build/fauna_kit/unity_port.py`** writes a grown creature's Unity assets from its spec, and `--check`
  holds them to it. For the Ray: `RayBody.asset` (one native mesh, 2,408 vertices, drawn double-sided),
  `Ray.prefab`, `RayFaunaDataSO.asset`, four `Ray Fauna <Element>.asset` configs, and its entry in the network list.
- **How it is built:** the QuadFish's prefab re-fitted (its components, network wiring and heart kept; its mesh,
  material, scale, fins and swim driver replaced), so the Ray is the shape of body the game already runs: one mesh
  under one Spindle, nine prisms as that Spindle's direct children, the heart at the root.
- **Checked without the editor:** the mesh asset reads back at the spec's counts; every prism reads back at its spec
  position under the Spindle; every guid has an owner; the network identity is unique; the heart-collection and
  colour-trace gates pass; a sheet drawn from the Unity assets themselves shows the Ray (wing, lobes, whip, sting).
  `unity_port.py --self-test` plants a wrong vertex count and an unowned mesh guid; both are caught.
- **NOT checked: anything in the Unity editor.** `Docs/UNITY_VERIFICATION_CHECKLIST.md` has the eight hand tests.
  The first is whether Unity accepts a mesh asset written as text.
- **What the Ray is not yet in Unity:** it spawns nowhere (no cell lists it); its wings do not ripple (the per-vertex
  wave is not in the spindle shader: the body shears as one); it will lunge, not whip (one mesh has no limbs); it
  steers with world up; its glide and feeding on the wing are not written. Each needs C# and is the next step.
- The dossier now reads native mesh assets, so a ported creature can be measured like a shipped one.

## Round 5 (2026-11-17): the method as skills

Garrett, answering round 4's questions: "1. all five. 2. accept them. 3. we need you to turn all of this into skills
so more efficient models can create more fauna. part of that skill is learning how to place prisms, spindles, and
crystals. it needs to be how to make and shape those spindles."

- **Decided: all five grown creatures go to Unity** (Ray, Nettle, Plume, Lantern, Ammon). NOT started: the order of
  work is at the end of `/fauna-maker`.
- **Decided: the Plume's and the Nettle's outlines are accepted.** `silhouette.ACCEPTED` records it, and
  `silhouette.py --check` now exits 0.
- **`/fauna-maker`** (new skill): seven steps from an animal's name to a creature in the lab, with the numbers for
  placing the crystal, the spindles and the prisms, and a table of what to do when something goes wrong.
- **`fauna.py`** (new): `new <name>` writes a working creature from `specs/_template.py`; `check <name>` runs the
  laws, prints where every prism sits against the numbers it must meet, measures the mesh, and prints a FIX for each
  failure; `sheet <name> <png>` draws it.
- **`/spindle` section 2.5**: how to shape a path and a girth, and copyable recipes (an arm, a tendril, a feather, a
  bell rib, a shell, a whip, a wing), with what each sway, glow and barb setting does.
- Checked: the template passes `fauna.py check` as written (955 vertices); a plate planted ahead of the crystal is
  caught with its FIX line (L1 and L3); every kit gate.
- Not checked: the skills have not been followed by another model yet. The first creature made from them by a
  smaller model is the real test, and what it trips on goes into `/fauna-maker`'s last table.

## Round 4 (2026-11-16): further, and spindles from scratch

Garrett: "yes this is a far better direction. now go far in this direction with a few more that really stretch what
you are capable of", and then: "create a whole new skill that can make the spindles from scratch. deconstruct the
current spindles of the fauna bodies ... then remake these bodies with bespoke spindles as another hierarchy of the
design."

### What changed

1. **The `/spindle` skill and `Tools/Build/fauna_kit/spindle.py`.** The shipped spindle meshes taken apart (the braid
   is three strands of a three-sided tube, 225 vertices; the sculpted bodies are ribbons two vertices wide woven into
   nets), a library of eleven designs built from those two parts, the mesh each makes, and a vertex budget.
2. **A new level in the hierarchy:** a creature is limbs, a limb names a spindle design, a design is how its wire is
   made. The Ray and the Nettle were remade with designs.
3. **`organic.py` grew:** barbs (a feather's, a comb's, a tendril's side lines), a limb's sway now ADDS to its
   parent's (a tendril rides its bell), and light that travels down a limb.
4. **The lab:** the travelling light, prisms on grown bodies drawn as light, and a bloom pass (the Wasp tab's last
   slider; 0 is off).
5. **Three new creatures:**
   - **The Plume** (a feather star): ten feathered arms curling back from a small cup, five rising as five fall.
   - **The Lantern** (a comb jelly): a clear woven body lit by the Etherion, eight comb rows with light running down
     them, two long lines.
   - **The Ammon** (a coiled shell): two mirrored spiral shells, light winding into their centres, six feelers.

### Measured

`spindle.py --budget --check` (the mesh each grown body makes; the bar is the Brittlestar's 5,072 vertices):

| creature | limbs | vertices | triangles |
|---|---|---|---|
| Ray | 10 | 2,408 | 4,202 |
| Ammon | 9 | 3,600 | 3,360 |
| Plume | 11 | 4,076 | 3,420 |
| Nettle | 29 | 4,789 | 5,268 |
| Lantern | 19 | 5,006 | 5,240 |

The Nettle as first built in round 3 was 11,508 vertices: the budget refused it, and designs brought it under.

`silhouette.py --check` (bar 0.41): Ray 0.45, Manta 0.45, Lantern 0.43, Ammon 0.43 pass. **The Plume (0.36, nearest
the Urchin hull) and the Nettle (0.40, the Rhino hull) FAIL**, and the command exits 1.

### Checked

`celestial.py --all` (twelve specs) and `--self-test`; `spindle.py --self-test` and `--budget --check`;
`recreate.py --check` (0.00002 u, unchanged); `lab_export.py --check`; `verify_lab.cjs` PASS with the bloom pass on
(no WebGL fault). Read: the Plume, Lantern, Ammon and Nettle in the lab.

### Not done, and what only a person can judge

- **Nothing is in Unity.** `spindle.py --obj` writes limbs as meshes Unity could import; none has been. A grown
  creature needs those assets, a Spindle per limb, and a per-vertex sway in the spindle shader (`/spindle` section 5).
- The vertex budget is not frame time: additive wire, bloom and one mesh a limb are not measured.
- **The prisms still read as boxes** on these bodies, and the red stings are the loudest thing on screen.
- The scorecard was not re-run for the three new creatures.
- Whether they are beautiful: that is the live page, and yours.

### Decisions needed (Garrett)

1. Which of the five grown creatures go to Unity first, if any.
2. The Plume's and the Nettle's outlines: reshape, or accept.
3. What a prism should look like on a grown body.

## Round 3 (2026-11-16): the rules relaxed, and bodies that are grown

Garrett: "keep these. we will test them. but now Relax the fauna rules a bit, so you can get us some fauna that are
more lifelike. these feel like you are using tinker toys, when I want you to be making hyper realistic ethereal
lifeforms in a celestial environment."

### What changed

1. **Kept:** the Manta and round 1's five are in the lab unchanged.
2. **Three laws relaxed** (`/fauna-creation` section 10): a limb may be GROWN (curved, tapered, woven wire, and
   membranes between limbs) instead of a straight braid; a body may be two-fold (flat, two identical faces); wire may
   lead the heart by a stated amount (a bell over the Etherion, lobes round a mouth). Prisms, the sting, mass and the
   heart's rules are as they were.
3. **`organic.py`:** grows limbs along smooth curves and weaves membranes between them. Every vertex carries its
   limb's sway, so the motion is a wave the vertex stage plays, not keyed limbs.
4. **The lab draws grown wire as light:** filaments add where they cross, membranes are a faint veil, prisms ride
   their limb's wave.
5. **Two grown creatures:**
   - **The Ray**: one pair of woven wings rippling from shoulder to tip and from leading edge to hind edge, two lobes
     curling forward round the Etherion, a whip. 77 u long, 68 u across. Rides as the Manta does (a glide, feeding on
     the wing).
   - **The Nettle**: a wide woven bell over the Etherion, which shows through it; eight tendrils from the rim, each
     ending in a sting; four frilled arms beneath. It pulses, and it eats and strikes all round.

### Measured

**The silhouette measure was wrong twice more, and these numbers replace round 2's table.** The Ray first read 0.03
from the Brittlestar: matching each view at its own best pose calls any two flat bodies alike, because edge-on both
are a line. The three views of ONE pose are now compared together, and pooled so a view counts by how much outline it
holds. `python3 Tools/Build/fauna_kit/silhouette.py --check`:

| | nearest look-alike | distance |
|---|---|---|
| shipped bodies (28 hulls, 7 creatures) | each other | 0.20 to 0.52, median 0.36; the bar (upper quartile) is **0.41** |
| **Ray** | the Manta | **0.44** (passes) |
| **Manta** | the Ray | **0.44** (passes) |
| **Nettle** | the Rhino hull | **0.39 (FAILS by 0.02)** |
| archived: Marlin, Basker, Medusa, Nautilus, Teredo | | 0.36, 0.43, 0.43, 0.39, 0.26 |

- The Nettle began 0.24 from the Clawfish (a bulb with a train). Widening the bell into a parasol took it to 0.39; a
  longer train made it worse (0.37) and was undone. It is in the lab as it is, flagged here: the gate is red for it.
- Scorecard (same games): Ray duel 14, brawl 3.8, siege 69.5 s, graze 88, sprint 28.7 s, slalom 33.5 s. The Nettle
  was scored before its bell was widened (0, 6, 28.4 s, 170, 56.7 s, 54.9 s) and not again. Neither wins a column;
  neither was built for one.

### Checked

`celestial.py --all` (nine specs pass the relaxed laws) and `--self-test`; `recreate.py --check` (0.00002 u,
unchanged); `lab_export.py --check`; `verify_lab.cjs` PASS; `silhouette.py --self-test`. Read: the Ray mid-roll and
the first Nettle in the lab. `silhouette.py --check` exits 1 (the Nettle).

### What only a person can judge, and what is not done

- Whether these are lifelike enough. The lab is lines and a veil in stand-in colours: no shading, no bloom, no depth.
- **Nothing here can go into the game yet.** A grown body needs a mesh writer and a per-vertex sway in the spindle
  shader (`/fauna-creation` section 10). The Space erosion of grown wire is not designed.
- The prisms on a grown body are still boxes, and read as boxes.

### Decisions needed (Garrett)

1. **Is this the direction?** If yes, the next round is the route into Unity for one grown creature.
2. **The Nettle's outline:** accept 0.39, or reshape it further.
3. The relaxed laws as written (section 10): two-fold bodies, a lead of a quarter of the length, grown wire.

## Round 2 (2026-11-16): the Manta, and a silhouette of its own

Garrett: "these seem like variations on what we have ... take inspirations from the shark, brittlestar, and worm by
comparing them to their real counterparts ... give us a manta fauna but really try to build something that belongs in
this world and would be called a manta for its hypnotic resemblance, but is of course not the real thing or the
vessel. the fauna and vessels need to have unique silhouettes so they are all distinguished."

### What changed

1. **Round 1's five are archived.** They were one body plan (a spine, radial limbs, a tail sting: the Bee's) and a
   bare spoke star from ahead. They stay in the lab under "Round 1 archive" so their rides can still be compared.
2. **The study** (`/fauna-creation` section 9): what the real clade, the fauna and the vessel each take from the
   names Brittlestar, Shark, Worm and Manta, and the method that follows from it (name the clade's signature shapes
   and motion, take what the vessel did not, re-derive each under the laws, choose a body PLAN and not a limb count).
3. **A silhouette gate** (`Tools/Build/fauna_kit/silhouette.py`, law L12). Every vessel hull (28) and every creature,
   flattened to an outline from the side and from ahead, compared at every roll, nose to tail and along each axis.
   The bar is measured from the fleet each run: a new species must stand further from everything than three in four
   shipped bodies stand from their nearest look-alike.
4. **Limbs that beat.** A spec's spindle can carry a `beat` (a rotation about its root, with a phase), and the lab
   plays it. The Manta's wingbeat is a wave from shoulder to tip; opposite wings mirror, and the second pair runs a
   quarter beat behind the first.
5. **The Manta** (`specs/manta.py`, all twelve laws pass): four rib-fan wings a quarter turn apart (a swept spar of
   three braids, four ribs, a hem closing the hind edge, three thin plates a wing), a lobe curled round the Etherion
   between each pair, a three-braid whip carrying the sting. 57 u long, 64 u across. For a rider it glides (slow to
   gain and lose speed) and feeds on the wing: holding RT eats what passes between its lobes with no wait.

### Measured (SUPERSEDED by round 3: the measure was corrected twice after this table)

`python3 Tools/Build/fauna_kit/silhouette.py --check` (0 = the same outline, 1 = nothing shared):

| | nearest look-alike | distance |
|---|---|---|
| shipped bodies (28 hulls, 7 creatures) | each other | 0.10 to 0.46, median 0.25; the bar (upper quartile) is **0.30** |
| **Manta** (new) | the Honeyguide hull | **0.42** (passes); 0.53 from the Manta hull |
| Brittlestar fauna against the Brittle Star hull | | 0.53 |
| Marlin (archived) | the Thresher hull | 0.31 |
| Basker (archived) | the Rhino hull | 0.43 |
| Medusa (archived) | the Grizzly hull | 0.32 |
| Nautilus (archived) | the Manta | 0.29 |
| Teredo (archived) | the Angler hull | 0.19 |

- The closest shipped pairs are hulls: several sit under 0.2 of each other. That is a finding about the fleet, not
  about fauna, and it is left for whoever owns those hulls.
- The measure agrees with "variations" for the Teredo and the Nautilus, is lukewarm on the Marlin and the Medusa,
  and disagrees on the Basker (0.43). It sees outlines, not body plans: whether the Basker's body comes back is
  Garrett's call.

The Manta on the scorecard (same games as round 1): duel 11, brawl 4, siege 59.5 s, graze 80.4, sprint 30.5 s,
slalom 34.7 s; a rookie: 3.6, 1.2, 116 s, 65.4, 43.3 s, 39.6 s. It wins no column. Feeding on the wing eats little
in the lab's sparse field (80 prisms against the QuadFish's 456): its meal is a line, and the lab has no trail to
fly down. It was built for its shape and its motion this round, not for a column.

### Checked

- `celestial.py --all` (seven specs celestial) and `--self-test`; `recreate.py --check` (0.00002 u, unchanged);
  `lab_export.py --check`; `verify_lab.cjs` PASS; `silhouette.py --self-test`.
- Read: the Manta's three-view sheet (a diamond with lobes and a whip from the side and from above, a cross with
  four short lobes from ahead), the silhouette sheet of all 41 bodies, and the Manta in the lab mid-turn.

### Found

1. **My first silhouette measure was wrong, and its own control said so.** Scaling each view to fill the grid made a
   dart seen end-on read as a disc. One scale per body fixed it, and several distances moved (the Marlin and the Bee
   went from the closest pair in the game to unremarkable).
2. A hull's travel axis is not always z in its model file, so every axis is tried.

### What only a person can judge

Whether it is hypnotic. The lab draws wires as lines in stand-in colours; the wingbeat is there, the game's look is
not. Nothing is in Unity.

### Decisions needed (Garrett)

1. **Is the four-winged Manta the right reading of "no up"?** Two flat wings with identical faces would be closer to
   the animal and closer to the Manta hull's outline; law L6 asks for three-fold or more, so this one is a cross.
2. **Which clade next** (`/fauna-creation` section 9 lists eight body plans the game does not have).
3. Whether any round 1 body comes back, and round 1's open decisions below.

## Round 1 (2026-11-15)

### What was built

1. **The kit learned the creatures the game already has.**
   - `fbx_model.py` reads a model file as Unity imports it (file scale, handedness, a Z-up file's root turn), and
     `prefab_read.py` flattens a prefab with its nested prefab and model instances. Two things the skills said could
     not be done without the editor are now done: a model's MESH fileID is `xxHash64("Type:Mesh-><name>0")`
     (reproduces two shipped ids), and a prism parented to a bone INSIDE a model instance is placed through the rig's
     constraints, which name the bones (the Brittlestar's ten prisms).
   - `dossier.py` measures the seven creatures a wasp can possess (Tadpole, QuadFish, Clawfish, Shark, Brittlestar,
     Bee, Worm Colony): wires, prisms, heart, the limbs the attack whips (the game's own `FaunaAttackDriver.Scan`
     rule on the measured tree), and the numbers a possession reads.
2. **The kit recreates them, and measures the difference** (`recreate.py`). Each creature's body is built from a spec
   by `body.from_spec`, the function a new species goes through, and held against the prefab measured again.
3. **A new law, L11: what a rider gets.** A spec states its top speed, mouth reach and attack, and the attack must
   be one the body can play (the game whips a body with three limb chains under one hub, and lunges any other).
4. **Five new creatures**, as celestial specs (all ten earlier laws and L11 pass): Marlin, Basker, Medusa, Nautilus,
   Teredo. Each brings one move of its own to the same four controls.
5. **The lab.** Ride all twelve with the wasp's shipped numbers (read from `JewelWaspConfig.asset`), in any of the
   four element forms, at a rival vessel (still, looping, evading or attacking), an Ark (108 sockets of its real
   kinds, regrowing one every 2 s) and a prism cluster. Leave the host with A and sting another. A headless
   scorecard rides every creature with an input-only pilot, and a Feedback tab records a rating, tags and a note per
   creature in the shared decision log.

### Measured: the recreation

`python3 Tools/Build/fauna_kit/recreate.py --check` (largest difference between the spec's body and the shipped
prefab, world units at root scale 1; tolerance 0.002):

| Creature | How | prisms | wires | heart | attack limbs | numbers |
|---|---|---|---|---|---|---|
| Tadpole | transcribed | 0.00000 | 0.00000 | 0.00000 | 0.00000 | 0.00000 |
| QuadFish | transcribed | 0.00000 | 0.00000 | 0.00000 | 0.00000 | 0.00000 |
| Clawfish | transcribed | 0.00000 | 0.00000 | 0.00000 | 0.00000 | 0.00000 |
| Shark | transcribed | 0.00000 | 0.00002 | 0.00000 | 0.00000 | 0.00000 |
| Brittlestar | transcribed | 0.00000 | 0.00001 | 0.00000 | 0.00000 | 0.00000 |
| Bee | **rebuilt from rules** | 0.00000 | 0.00002 | 0.00000 | 0.00000 | 0.00000 |
| Worm Colony (head, body, tail) | transcribed | 0.00000 | 0.00001 | 0.00000 | 0.00000 | 0.00000 |

What this does and does not prove:
- **The Bee is the real test.** Its spec is the generative language (braids from a point to a point, prisms by rule),
  and the body rebuilt from those rules matches the shipped prefab to 0.00002 u, wires and attack limbs included.
- **The other six are transcriptions.** Their spec states the shipped meshes by reference and the prisms, heart and
  limbs by pose. The table proves the kit's language can state them and that nothing is lost between a spec and a
  body. It does not prove the kit can generate a Shark from rules: their bodies are sculpted models.
- The first run of this table was not zero. It found a wrong limb match (several limbs share a root joint), a wrong
  attack-limb joint on the Bee (the game's last joint is the braid's own transform, not the limb's tip), and a
  non-unit quaternion on the QuadFish's sheared fins that made identical boxes read 0.015 apart (a metric bug).
- `recreate.py --self-test` plants nine drifts (a tooth moved 0.02, a fin resized, a kind changed, a model shifted,
  a heart moved, a joint moved, a speed changed, a Bee vane and rib moved in the generative spec): all nine measured.

`python3 Tools/Build/fauna_kit/celestial.py --shipped` lists the laws each shipped body breaks (the redesign list of
`/fauna-creation` section 7.1). Every one but the Bee steers with world up; the QuadFish, Clawfish, Shark,
Brittlestar and Worm wear sculpted models (L2); the Tadpole has one health prism and no stinger.

### Measured: the scorecard

`window.__lab.runBatch()` on the page (the Scorecard tab). An input-only pilot, seeds 1-5 (each starts the host up to
30 u off its mark), every creature at scale 1 and its own base speed, the wasp at element level 0.

| Creature | duel (strikes) | brawl (strikes) | siege (s, low) | graze (prisms) | sprint (s, low) | slalom (s, low) |
|---|---|---|---|---|---|---|
| Tadpole | 0 | 1 | 21.91 | 213.6 | 60 | 61.17 |
| QuadFish | 0 | 0 | **7.37** | 456.2 | 34.17 | 34.62 |
| Clawfish | 11 | 4 | 10.45 | 364.6 | 29.76 | 30.94 |
| Shark | 12.8 | 4 | 29.29 | 146.2 | 27.13 | 28.77 |
| Brittlestar | 0 | 4.4 | 13.69 | 331.8 | 38.4 | 38.22 |
| Bee | 0 | 4 | 80.81 | 52 | 36.88 | 36.92 |
| Worm Colony | 0 | 5 | 12.7 | 346.4 | 36.47 | 35.95 |
| Marlin (new, for sprint) | **17.2** | 4.4 | 120 | 17.2 | **20.26** | **27.19** |
| Basker (new, for graze) | 0 | 5 | 8.56 | **649** | 56.96 | 56.68 |
| Medusa (new, for siege) | 0 | 6 | 8.47 | 474 | 39.54 | 40.05 |
| Nautilus (new, for slalom) | 15.8 | 7.6 | 82.95 | 59.6 | 25.48 | 27.68 |
| Teredo (new, for brawl) | 0 | **12** | 42.26 | 123 | 31.82 | 34.02 |
| Control: RT never pressed | 0 | 0 | 120 | 0 | 60 | 61.9 |

The games: **duel**, strikes in 60 s on a vessel that turns away at 44 u/s; **brawl**, strikes in 60 s on a vessel
strafing the host at 80 u/s from outside the shipped strike reach; **siege**, seconds to strip the Ark from 108
sockets to 30; **graze**, prisms eaten in 30 s from a field of 700; **sprint**, seconds to the first strike on a still
vessel 1500 u away; **slalom**, seconds through ten tight gates.

Reading it:
- **Three new creatures win their column clearly:** the Marlin (sprint 20.3 s against the next 25.5), the Basker
  (graze 649 against 474) and the Teredo (brawl 12 against 7.6).
- **Two do not.** The Nautilus ties the Marlin in the slalom (27.68 against 27.19; the five seeds spread 0.8 and
  1.9 s, so that is no difference). The Medusa ties the shipped QuadFish and the Basker at the Ark (8.47, 7.37, 8.56;
  spreads 1.4 to 3.2 s). Neither earned its place on numbers.
- **The shipped creatures are not equal hosts.** Five of seven cannot land one strike on a vessel that turns away at
  44 u/s (at wasp Time level 0 they swim 22 to 42 u/s). The QuadFish is the best shipped eater (mouth 81 u).
- **A rookie pilot** (the second button) loses most on the Nautilus (brawl 7.6 to 0.6, siege never) and the Shark;
  the Basker barely changes (649 to 648.6): it is the forgiving host.
- Noise: the vessel and slalom games moved 0 to 4 across seeds, graze about 30 to 70 prisms.

### Checked

- `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/FaunaLab.html`: PASS (no console or WebGL error, no page
  scroll on desktop, no sideways scroll on a phone, the hook, every SPEC key in SHIPPED and in range, the manual
  clock, the batch byte-identical twice and across two page loads). The stage is WebGL, so its blank check is the
  screenshots, which were read: a Shark from behind, a Medusa eating the Ark, a Basker inside the cluster.
- `celestial.py --all` (six specs celestial), `--self-test` (every law but L4 and L10 catches a planted defect, L11 three),
  `dossier.py --self-test`, `prefab_read.py --self-test` (51 stripped documents obey source XOR instance),
  `recreate.py --check` and `--self-test`, `lab_export.py --check` (the page's data block is what the kit exports),
  `author_bee_assets.py --check` (the Bee's spec gained a possession block; its assets still match).
- `bash .claude/skills/ship/gates.sh`: 111 checks ok; two fail in files this round did not touch
  (`doc_summaries` on `SwitchbackBlastRunTests.cs:580`, and `colour_grade.json` stale), and the reference compile
  was skipped (no .NET SDK in this container; no C# changed).
- The shared decision log: `ArtifactData list decisions` on the published page reads back (empty). Pressing Save in
  the viewer was not exercised.

### Found

1. **The shipped host camera sits inside a big host.** The stand-back is `hostCameraBase + PossessionBodyRadius x
   hostCameraBodies`, and the body radius is `max(2, the root's scale)`, which is the SCALE and not the size: 2 for
   a Shark that is 56 u long, so the camera stands 42 u behind its heart, inside its tail. The lab's camera stands
   back at least 1.6 body lengths (the Wasp tab's last slider; 0 is the game's formula). Not seen in the Unity
   editor: it is read from `Fauna.Possession.cs` and `JewelWaspExecutor.cs`. **Decision needed (Garrett):** whether
   `PossessionBodyRadius` should be the body's measured reach.
2. **The same radius is the strike's reach.** A strike lands within `attackReach 22 + body radius`, so every
   creature that is not a worm reaches 24 u whatever its size. A Brittlestar's arms span 42 u from its hub and its
   whip animates them, but the hit is still 24 u from its root.
3. **Holding RT wastes attacks.** RT attacks at a vessel within three reaches (72 u), the strike can land only from
   0.21 s to 0.6 s later and only within one reach, and the recharge is spent on the press. A pilot who holds RT
   (the rookie) attacks too early every time. The scorecard's skilled pilot presses when the gap will have closed.
4. **Three seeds were one run three times** in the vessel and slalom games until the host's start was seeded.
5. The first scorecard's Siege and Graze columns sat at a lab limit for nearly every creature (the whole cluster
   eaten, the Ark stripped as fast as it regrew). Each game now pins its own conditions and none saturates.
6. A stand-in vessel that chases a slow host orbits it at its own turn radius and never comes inside reach. It now
   flies committed passes.

### What this lab does not model

Listed on the page's About tab: the game's look, a real rival hull and its petals, the Ark's model, course and danger
contact, collisions, starvation and the stomach, a LightFauna's mouth growing with its girth, the murmuration,
multiplayer. The new creatures' own moves and every `turn_rate` are proposals: nothing in the game has them.

### Decisions needed (Garrett)

1. **Which new creatures are worth another round**, from riding them (the Feedback tab).
2. **May a new limb wear a shipped limb mesh** (a Brittlestar arm, a Shark vane) as well as the braid? L2 says braid
   only, so the five new bodies are plain beside the sculpted ones. Reusing shipped limb meshes as a parts library is
   the cheapest way to raise their look; it is a change to L2 and so it is yours.
3. **The body radius** (Found 1 and 2).
4. **Which shipped creature to redesign first** under the celestial laws (`celestial.py --shipped`).

### Next round (proposed)

- Apply the feedback log to the specs (`ArtifactData list decisions` at the start of the round).
- The murmuration as a host. A possessed plant (the wasp's LT).
- Build the kept creatures in Unity with `fauna_kit/build.py`, and their own moves as species overrides
  (`PlayPossessionAttack`, `PossessedSuction`), after a `who_works_where` look and a design brief each.
