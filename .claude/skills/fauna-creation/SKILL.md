---
name: fauna-creation
description: Design and build a NEW Cosmic Shore creature (or redesign one) as what it is - a celestial, Etherion-based lifeform of the HyperSea, not an Earth animal. Loads the celestial laws a body must obey (an Etherion heart that leads, a body that is a wireframe of braid spindles, health prisms around it, one long skinny danger prism as its stinger, no sense of up, the four element forms), the design worksheet, the spec-first build (Tools/Build/fauna_kit/: a species spec, celestial.py that says no, preview.py that draws it), and the traps the bee taught. Use for "make a creature", "a new fauna", "design a species", "make <animal> for the game", "a real model for <creature>", any Tools/Build/fauna_kit/** change, or before a fauna prefab's BODY is built or reshaped. Pairs with /fauna (the alive-contract and repairs) and /ecology (the system); run both too.
---

# Fauna creation: celestial lifeforms

**Read this before designing any creature's body.** `/fauna` holds the contract a creature must meet to be ALIVE
(behaviour class, cell data, field parity, heart seat, body prisms) and the repairs; `/ecology` holds the system and its
LOCKED invariants. This skill holds what neither says: **what a creature in this world IS, and how to design one that
obeys its rules.** Run `/ecology` and `/fauna` too; this skill assumes them.

## 0. Why this skill exists (read it once)

Garrett, 2026-11-01, after the bee was built as an Earth insect (thorax, six jointed legs, compound eyes, antennae,
striped belly, wings with a dihedral "up"):

> "i think you need to spend some time making a fauna creation skill first. fauna have rules like bodies made of
> spindles that are wireframe inspired structures suitable for the space element to make thinner. there must be a long
> skinny danger prism as a stinger. other health prisms around its body. it isn't actually a bee. it is a celestial
> etherion based life form. it doesn't really have a sense of up, the celestial life has rules that must be opeyed. you
> must learn how to operate within those constraints. then apply those learnings to the bee, and plan for us to update
> your ability to do this for other lifeforms too."

**The mistake to never repeat:** an Earth animal's NAME is a role and a feeling, not an anatomy. "Bee" means a busy
pollinator that stings; it does not mean legs, a thorax or a belly. Translate the animal into what it DOES, then build
that out of the HyperSea's own parts.

## 1. What a celestial lifeform is

- **It is grown round an Etherion.** "Etherion" is the player's word for a crystal (`Docs/CODEX.md` 5-9). A lifeform's
  heart is its element's Etherion, one size (3.55) for every species, worth one petal (Garrett 2026-10-26, ECOSYSTEM.md
  §64). Growth runs from the crystal: crystal, then spindles from it, then prisms on the spindles (flora skill §2); death
  runs the other way (starvation outside-in, a joust from the heart out; ECOSYSTEM_DESIGN_PRINCIPLES "wither").
- **Its body is a wireframe of spindles.** "Spindles are the skeleton made beautiful: every limb ... is a wire, and the
  game leans into that instead of hiding it" (`Docs/SPINDLE_LOD.md` 23-26). The shipped spindle is one mesh: a braid of
  three strands round a straight centreline, pinched at the root, open at the tip (`bonita.fbx` `BezierCurve.001`). The
  body is an OPEN FRAME of those braids, not a sculpted skin. (Garrett 2026-10-21 drew one line inside "wire": living
  spindles are the organic, tapered braid; a regular synthetic wire is for constructs like switch rings. A celestial body
  is a wireframe OF living braids.)
- **Its mass is its prisms.** Health prisms ride the spindles; they are conserved mass and the surface that kills it.
  Spindles carry no mass and no collider (ECOSYSTEM.md §63.2). Moving spindles are how a player sees mass is alive
  (Garrett, SPINDLE_LOD.md 169-171).
- **It has no up.** The HyperSea has no gravity and its cells have no ground (ECOSYSTEM.md §18.1, "a cell with no up").
  A celestial body repeats round its travel axis; nothing on it is a back, a belly, a top or a bottom.
- **It obeys one law with every other lifeform** ("keep it consistent", Garrett 2026-10-28): the element forms, the
  heart, the wither, mass conservation, no pop-in, no imposed death.

## 2. The laws (each is checked by `Tools/Build/fauna_kit/celestial.py`)

| # | Law | Source |
|---|---|---|
| L1 | **The Etherion leads; the body trails.** The heart sits at the origin and every spindle and prism lies behind it (−z); prisms clear the heart by its radius + 2.55 u. | ECOSYSTEM.md §23.9, §46.2; /fauna §5 |
| L2 | **The body is a wireframe of braid spindles** (3 at least), every one with a spindle path back to the Etherion (a root starts at the heart; a child starts on its parent). No sculpted model skin. | Garrett 2026-11-01; SPINDLE_LOD.md §1; PRINCIPLES "spindle path" (LOCKED) |
| L3 | **Every prism is a DIRECT child of the limb it rides**, sitting on that limb. Otherwise it does not sway and the creature reads stiff. Nothing is parented under a prism. | /fauna §4; PRINCIPLES "living mass is the mass that moves" (LOCKED) |
| L4 | **Health prisms around the body** (3 at least), on the extremities first: starvation withers outside-in, so the outermost mass goes first. | Garrett 2026-11-01; /fauna §2.5 |
| L5 | **A stinger: one long, skinny, authored danger prism** at the trailing extremity, on a terminal limb (length ≥ 4 and ≥ 6× its thickness). Any axis outside the scale animator's [0.5, 10] window is `seat_authored` (`Fauna.SeatAuthoredSize`). The spec declares where the creature feeds (`mouth`), because LightFauna puts the mouth at the authored-danger centroid. | Garrett 2026-11-01; ECOSYSTEM.md §64.9; `LightFauna.cs` 458-465 |
| L6 | **No sense of up.** The body is n-fold symmetric round +z, n ≥ 3, and the species steers with a CARRIED up (parallel-transported, like `SubstrateArms.Frame`), never `Vector3.up`. | Garrett 2026-11-01; ECOSYSTEM.md §18.1 |
| L7 | **Prisms never interpenetrate**; spindles may pass through anything. Fit flush: keep every prism, shrink minimally, at authoring time. | PRINCIPLES (LOCKED); Garrett 2026-10-20 |
| L8 | **The four element forms are written down.** Mass: every plain prism shielded, the body grown until the heart's 12.44 u swing clears every shield. Charge: every prism danger, spindles part hardest. Space: 1.6477× Mass's span, spindles eroded where thickest, every prism refit as a needle, the stinger too. Time: the base body (scale 1), 1.4× faster. | ECOSYSTEM.md §64.8-9, §65 (Garrett 2026-10-27/28); Garrett 2026-11-01 (Time, the stinger) |
| L9 | **No Earth anatomy names** (leg, belly, back, eye, antenna, wing, thorax...). Name a part for what it is here: spine, vane, rib, plate, sting, tine. | this skill |
| L10 | **A premise and a role.** What it is, the feeling it is for ("surrounded ... scary", Garrett on the substrate), and what it does in the food web. Every lifeform feeds and reproduces. | Garrett 2026-10-08; SUBSTRATE_FAUNA.md |
| L12 | **A silhouette of its own.** Seen from far away with no detail, from the side and from ahead, at any roll, a new species stands further from every vessel hull, every shipped creature and every other new species than three in four shipped bodies stand from their nearest look-alike. `Tools/Build/fauna_kit/silhouette.py --check` (it takes about two minutes; the bar is measured from the fleet each run). Section 9. | Garrett 2026-11-16: "the fauna and vessels need to have unique silhouettes so they are all distinguished" |
| L11 | **What a rider gets.** Any creature can be possessed (the Jewel Wasp), so a spec has a `possession` block: `top_speed` (`Fauna.PossessionTopSpeed`), `mouth_reach` (`PossessionMouthReach`) and an `attack` the BODY can play: the game whips a body whose hub carries three limb chains or more and lunges any other (`FaunaAttackDriver.Scan`), whatever the spec hopes; `charge` is an attack of its own (the worm's lunge). Section 8. | Garrett 2026-11-14 (`JEWEL_WASP.md`), 2026-11-15 |

Laws the checker cannot see, and you still owe: nothing pops in or out (spawns bloom, deaths wither); no lifespan or
despawn timer; nothing in the world looks flat (facets read, never washed out); every heart drops as one Etherion;
reshape the body's own meshes, never stretch through the hierarchy (the heart must stay round); the collider budget.

## 3. The design worksheet (fill it before the spec)

1. **The animal's name means:** its role (pollinator, grazer, hunter...), its behaviour (swarms, ambushes, guards...),
   and the feeling (busy, eerie, scary...). Write those three; drop its anatomy.
2. **Symmetry order n.** 3 is the HyperSea's number (three domains, the three-strand braid); 4 and 6 also work. It
   decides everything round the axis.
3. **The spindle graph.** A spine from the Etherion, then the repeating limbs (n of each). Which limbs MOVE (beat, sweep,
   trail) and which is the stinger's terminal limb.
4. **Prism placement.** Health prisms on the extremities and round the spine; sizes clear the heart (L1), never overlap
   (L7), sit on their limb (L3).
5. **The stinger.** Length, thickness, which limb. Whether it is the only weapon, and where the creature feeds.
6. **The element table.** Mass body scale from the checker's swing number; Space = 1.6477 × Mass and eroded; Charge all
   danger; Time as decided.
7. **Motion tier** (/fauna §3): flocking for the whole body, GPU sway for the braids (free), a CPU driver writing
   `localRotation` for limbs that beat. Never `localPosition` on a prism.
8. **Every list** it joins: the cell spawn profiles, the Spawn Matrix and lifeform bench, the Codex, the lifeform
   palette, the network prefab list, the heart and crystal gates (the bee round 2 list is the template).

## 4. The build: spec first

```bash
python3 Tools/Build/fauna_kit/celestial.py <species>            # the laws; exit 1 on any break
python3 Tools/Build/fauna_kit/celestial.py --self-test          # every law catches a planted defect
python3 Tools/Build/fauna_kit/preview.py <species> --out <png>  # four views; LOOK at it before building
python3 Tools/Build/fauna_kit/dossier.py [<creature>] --sheet <png>   # the shipped creatures, measured (section 8)
python3 Tools/Build/fauna_kit/recreate.py --check               # every shipped creature rebuilt from its spec, differenced
python3 Tools/Build/fauna_kit/celestial.py --shipped            # the laws each shipped body breaks (a report)
python3 Tools/Build/fauna_kit/lab_export.py [--check]           # the Fauna Possession Lab's data block, from the kit
```

1. **Write `Tools/Build/fauna_kit/specs/<species>.py`** (`SPEC = {...}`; `specs/bee.py` documents the schema). World
   units at root scale 1, the Etherion at the origin, travel along +z.
2. **Run the checker until it says `celestial`.** Do not argue with a law in code; if one is wrong, take it to Garrett.
3. **Render the preview and read it.** Then show it to the person before building (`SendUserFile`).
4. **Build the prefab FROM the spec** with `Tools/Build/fauna_kit/build.py` (`build_body`), called by the species
   generator (`Tools/Build/author_<species>_assets.py`; `author_bee_assets.py` is the worked example), which reads the
   same dict and holds the prefab to it with `build.read_back`. Render the BUILT prefab with
   `python3 Tools/Build/fauna_kit/render_prefab.py <species> <prefab> --out <png>` (the real braid mesh at the prefab's
   own poses). The old wording of this step, kept for its rules: braid spindles are `TadpoleSpindle`/`Branch` instances, prisms are `HealthBlock`/`DangerBlock`
   instances placed as direct children of their spindle, the root scale uniform (do NOT copy the tadpole's rotated,
   0.4-scaled root), the heart at the origin. Its `--check` holds the prefab to the spec. Keep guids when replacing a
   body.
5. **Configs per element** from the table: `BaseBodyScale`, and on Space `ErodeSpindle: 1` with the 1.6477 span.
6. **Steering with a carried up.** `SafeLookRotation.TryGet(forward, up, ...)` with an up the species carries and
   parallel-transports each step; never the default `Vector3.up`.
7. **The gates:** `celestial.py`, the generator's `--check`, then /fauna §8 (heart seat, heart collection, field parity,
   sway HLSL, the C# gates), the reference compile (player and editor), the console-logging gate, and the collider delta.
8. **Show it.** A render of the built prefab (not only the spec) when it can be made; say what only the editor can judge.

## 5. Traps the bee and the survey found

- **The default up.** `SafeLookRotation.TryGet(forward)` defaults to `Vector3.up`, and `LightFauna`, `Boid`,
  `WormFauna`, `NcaCreatureFauna`, `SubstrateFauna` and `BuilderColonyFauna` all steer that way. A symmetric body
  steered with world up still behaves as if it has a top. The only up-free frame shipped is `SubstrateArms.Frame`.
- **(Resolved 2026-11-01) "Space does not thin a stinger".** `Fauna.ErodeSpindle` used to skip authored danger ("a tooth
  keeps its size"). Garrett: "the stinger can thin on space. the stinger is just a danger prism." The exception is gone:
  every prism is refit on Space, teeth and stingers included, and `celestial.py` L8 rejects `stinger_thins: False`.
- **The mouth follows the danger.** LightFauna puts the mouth at the authored-danger centroid, so a creature whose only
  danger is a rear stinger "eats" from its tail. Declare `mouth`.
- **Copying the tadpole root.** The tadpole's root carries a rotation and a 0.4 scale; a species forked from it inherits
  both, and the spec's numbers stop meaning what they say.
- **The [0.5, 10] clamp.** A stinger longer than 10 or thinner than 0.5 is silently clamped unless seated.
- **The Space opt-in is per config.** The bee's four configs shipped without `ErodeSpindle`, so the Space bee did not
  out-span the Mass bee.
- **A model skin is not a body.** A sculpted FBX under a spindle hides the wireframe and invites Earth anatomy.
- **Measure the Mass swing to the SHIELDS, not the boxes** (the bee, 2026-11-01). A shield is an octahedron reaching
  1.5 x a prism's size, so a body cleared for its boxes puts a Mass heart inside its own armour: the bee's first spec
  said Mass 2.5, and the octahedra need 3.812 (`celestial.shield_dist`, exact point-to-triangle). Space is then
  1.6477 x that, so a too-small Mass makes a too-small Space as well.
- **Roll every repeated part RADIALLY.** A symmetric set of centres is not a symmetric body: a plate posed with an
  arbitrary up turns a different face to the axis on each copy. `celestial.oriented` gives every spindle and prism a
  radial local y, and the builder and the checker share it, so what is checked is what is built.
- **Build from documents you own, not nested spindle prefabs.** A `TadpoleSpindle` instance is a spindle WITH one prism;
  a limb that carries three (the bee's spine) cannot take more without added-object surgery. The builder writes plain
  spindle GameObjects (a `Spindle` + a `Braid` child on the shipped mesh) and nests only the prisms
  (`DynamicHealthBlock` / `DangerBlock`, the worm tail's way), sized by `m_LocalScale` (what `SeatAuthoredSize` admits).
- **(Superseded the same day) "Scale a braid uniformly".** The first build scaled braids uniformly because
  `Fauna.ErodeSpindle` judged a prism's nearest host with one axis's scale (its x column). That measurement is now made
  in the world, and the body skin is the one that spans furthest when several share one mesh (the spine), so a braid is
  stretched along its length like every shipped spindle (below).
- **One hook, not five call sites.** A species with a carried up needs every rotation its class writes to ask it:
  `Boid.SteeringUpFor` (world +y by default) is the one place, and `BeeFauna` overrides it with
  `CelestialFrame.CarryUp`. `LightFauna`, `WormFauna` and the drawn populations still need the same hook.
- **The heart-seat gate does not see a spindle body.** `verify_fauna_heart_seat.py` walks model-instance bodies; a body
  built from a spec is held by `celestial.py` L1 (box clearance) and L8 (shield clearance) instead.
- **A braid is stretched along its length, never scaled up evenly** (the bee's first build, 2026-11-01). Scaling the
  braid uniformly to reach a long limb made every limb a fat, tangled ribbon (the vanes ~4 u across). "A wire is judged by
  its thickness, never by its length" (SPINDLE_LOD.md): the shipped Branch is (1, 1, 6.2) and the tadpole's braid
  (1.5, 1.5, 5), and the Space tadpole erodes such a braid. `build.py` keeps the spec's `girth` across (default 1) and
  stretches z. Render the BUILT prefab (`render_prefab.py`) and look: the spec preview drew idealised braids and could
  not show it.

- **A model's mesh fileID can be derived; its inner transforms are named by the rig** (2026-11-15). Unity's id for a
  model's mesh sub-asset is `xxHash64("Type:Mesh-><name>0")`, signed (`fbx_model.mesh_file_id`; it reproduces the
  QuadFish's and the Shark's shipped ids). The same string does NOT give a Transform's or a GameObject's id. A prism
  parented to a bone INSIDE a model instance is placed another way: a rig GameObject named "D2" whose
  `m_ConstrainedObject` is that stripped transform says it is the bone ending in "D2" (`prefab_read.py`). That is how
  the Brittlestar's ten prisms were found on bones 3 and 5 of each arm. /fauna section 7's "cannot derive" is now
  narrower than it reads.
- **A Z-up model has its ROOT nodes turned** by (0, 0.7071, 0.7071, 0) on import (the Shark, the Brittlestar); a Y-up
  one does not. `dossier.py --self-test` holds `fbx_model.py` to the Shark prefab's own bones (an unpacked copy).
- **State a repeated part in (radius, z) and turn it** (`_kit.polar`), never as a rounded xyz you then rotate: the
  Medusa's five stings differed by 1e-6 in z from six-decimal rounding and law L5 refused the body. The checker was
  right; the spec was fixed.
- **A sheared parent makes a prism's rotation non-unit.** The QuadFish's spindle is (10.14, 10.14, 13.29), so its
  fins' world matrices are sheared; a quaternion read off one without normalising made two identical boxes measure
  0.015 apart. Normalise before comparing (`dossier._rot_of`).
- **`PossessionBodyRadius` is the root's SCALE, not the body's size** (`max(2, lossyScale)`): 2 for a 56 u Shark. A
  rider's camera stand-back and strike reach both read it. A spec that needs more states `possession.body_radius`,
  which is a species override to write in Unity (`Docs/Studios/FAUNA_LAB.md`, Found 1-2: open with Garrett).
- **A trait that only changes a number rarely changes the ride.** The Medusa's longer reach won nothing until the
  scorecard was asked a question reach answers, and then it only tied. Give a new creature a different VERB under
  the same four controls (a charge, a latch, a pulse, a funnel) and let the lab say whether it earned its column.

## 6. Open decisions (ask; do not default)

- **Symmetry order per species** where 3 is not obvious (3 is right for the bee).
- **May a limb wear a shipped limb mesh as well as the braid?** (2026-11-15.) L2 says braid only, and the five new
  bodies are plain beside the sculpted ones. The Brittlestar's arm segments, the Shark's vanes and the worm's ribs are
  all braided wire meshes a spec could place like a braid (a parts library). It changes L2, so it is Garrett's.
- **Should `PossessionBodyRadius` be the body's measured reach?** (Section 5; `FAUNA_LAB.md` Found 1-2.)

Decided by Garrett, 2026-11-01 (now laws, not questions):
- **Time is the base body, faster**: "time makes them faster, but it is the base essentially". Time's `body_scale` is 1
  and its speed band is `TIME_SPEED` (1.4, the shipped tadpole's Time:base band) times the species' own (L8).
- **The stinger thins on Space**: "the stinger is just a danger prism". No danger exception in the erosion (L8).
- **A skeleton leaves danger prisms as they are**: "danger prisms are left behind without need for exceptions".
  `Fauna.LeaveSkeleton` already drops every body prism; never add a danger branch to it.

## 7. Extending this to other lifeforms (the plan)

The kit is spec-first so the same move works for every kingdom:

1. **Fauna retrofits.** (The list exists since 2026-11-15: `specs/shipped/` and `celestial.py --shipped`.) Write a spec for each shipped species and run `celestial.py` on it; the breaks are the redesign
   list (most steer with world up; the shark, brittlestar, quadfish and clawfish are sculpted skins). Fix one species
   per round, Garrett's pick first.
2. **Swarms, colonies, substrates and NCA creatures.** Their MEMBER is the body (a swarm tadpole, a substrate agent): a
   member spec, the same laws, with the population's drawn frame (`SwarmInstance`, `SubstrateArms.Frame`) taking L6.
3. **Flora.** A flora spec on the flora growth law (crystal, spindles, prisms; /flora §2), with flora's own laws added
   (the limb contract, elegance, element expression) and L5 replaced by the flora's danger rule. `celestial.py` grows a
   `kingdom` switch rather than a second checker.
4. **The builder.** One `fauna_kit/build.py` that turns any spec into a prefab (spindle and prism instances, the heart,
   the configs) so a species stops needing its own generator; species generators then only add their behaviour class.
5. **Learnings.** Every species adds what it taught to §5 here, the way `/labmaker` LEARNINGS does.


## 8. Learn the shipped fauna, recreate it, and let a person ride it (2026-11-15)

Garrett: "first our goal is the skill that can make great fauna ... try to get to a point where you can recreate the
existing fauna like you did when you learned to create better vessels." Three tools and one page; the record is
`Docs/Studios/FAUNA_LAB.md`.

**The dossier** (`dossier.py`, with `prefab_read.py` and `fbx_model.py`). Every creature a rider can take (a class that
does not say `CanBePossessed => false`), measured from the prefab, the model files and the config assets: the wires its
spindles draw, every prism with pose, size and kind, the heart's seat, the limbs its attack whips (the game's own
`FaunaAttackDriver.Scan` rule, run on the measured tree) and the numbers its possession reads. Use it before designing:
`dossier.py --sheet <png>` draws all of them from three sides, and it is the fastest way to see what "as good as the
shipped ones" means. A new shipped species joins `dossier.ROSTER`.

**The recreation gate** (`recreate.py --check`). Each shipped creature has a spec; `body.from_spec` builds its body the
way it builds a new species; the gate measures the largest difference to the prefab measured again (prisms, wires,
heart, attack limbs, numbers). Two kinds of spec, and the difference matters when you quote the table:
- **generative**: the Bee (`specs/bee.py`), rebuilt from rules. This is the proof that a spec becomes the body the
  game ships.
- **transcribed**: the other six (`specs/shipped/`, written by `recreate.py --write-specs`). They state the shipped
  meshes by reference. They prove the language can STATE the body and that nothing is lost, not that the kit can
  generate it. Re-run `--write-specs` when a shipped prefab changes on purpose; an unexplained difference is a drift.

**The rider's law and the possession block** (L11). `"possession": {"top_speed", "mouth_reach", "attack"}` are what
the game reads. The rest are PROPOSALS a species brings as its own override, named so a port knows what to write:

| key | what it is | where it would live in Unity |
|---|---|---|
| `turn_rate` | how fast its heading follows the rider's (the rider asks for 10) | its class's response to `TrySteer` (the worm has its own) |
| `charge` | a dash along the aim (`speed_scale`, `seconds`) | `PlayPossessionAttack` (the worm's lunge is the precedent) |
| `suction` | the mouth's shape: `funnel` (a cone, `half_angle`), `all` (every side), `bites_scale` | `PossessedSuction` (the swarm overrides it) |
| `latch` | a strike that rides the hull (`seconds`, `rest`, `recharge_scale`) | the species' attack; the plate latch is the wild precedent |
| `motion` | `pulse`: a kick then a glide it turns in | its class's motion |
| `body_radius`, `aim_cone` | reach and camera; where a strike may aim | `PossessionBodyRadius`; the executor's aim cone |
| `for`, `feel` | the lab column it is designed to win; one line shown to the rider | the lab only |

**The lab** (`Docs/Studios/FaunaLab.html`, https://claude.ai/artifact/PMh24N63DgLuEJphkrbcV4). `lab_export.py` writes
its data block from the kit, so a spec edit plus one command puts a creature in front of a person, in any element
form, against a vessel, an Ark and a prism cluster. The loop for a new creature:
1. worksheet (section 3), spec (`specs/_kit.py`: `polar`, `spindle`, `chain`, `on`; `elements` solves Mass's scale);
2. `celestial.py <species>` until celestial; `preview.py` or `dossier.sheet` and LOOK;
3. add it to `lab_export.NEW_SPECIES`, run `lab_export.py`, then `verify_lab.cjs`;
4. the Scorecard tab: does it win the column its `for` names? A miss is a finding, not a number to tune the yardstick
   round (round 1: the Nautilus and the Medusa tied; they were reported, not rescued);
5. publish to the same artifact; the person rides it and leaves feedback (the Feedback tab);
6. the next round starts with `ArtifactData list decisions` and applies it.
Only then does a creature earn a Unity build (section 4, steps 4-8).

## 9. From a real clade to a HyperSea lifeform (2026-11-16)

Garrett, on round 1's five new creatures: "these seem like variations on what we have. there are hundreds of millions
of species. surely you can take inspirations from the shark, brittlestar, and worm by comparing them to their real
counterparts ... consider the difference between the brittlestar fauna and the vessel and the real world clade. those
are three very different things." He was right, and it was measurable: all five were ONE body plan (a spine, n radial
limbs, a tail sting, which is the Bee), and from ahead every one was a bare spoke star.

**Three things share a name, and each takes something different from it** (the fauna and vessels measured by
`dossier.py` and `silhouette.py`; the real animals from general knowledge, so check a claim before leaning on it):

| name | the real clade | the FAUNA takes | the VESSEL takes |
|---|---|---|---|
| Brittlestar | Ophiuroidea: a small disc, five thin jointed arms it rows and whips with, the mouth under the disc, arms that regrow | the ARMS: a flat five-armed star of braided segments on a 26-bone rig, the arms trailing and dangling behind the hub (`DampedTransform`), a health prism mid-arm and a sting at every tip; a grazer whose attack whips all five | the FIVE-FOLD RING: a pentagon hull with a star inside it, an object that spins and is thrown. From ahead it is a ring; the fauna is a line or a star (0.53 apart) |
| Shark | Selachii: a fusiform body, a jaw of replaceable teeth, stiff paired fins, a long upper tail lobe, a hunter that circles | the JAW and the SWEEP: an open braided skull with five needle teeth round the heart at the FRONT (the only shipped body whose danger leads), two swept vanes, a long tapering tail of five bones, a jaw that works (`SharkJawDriver`); no back fin | no hull is a shark; the nearest outline is the Squirrel's (0.31) |
| Worm | Annelida (a bobbit or sand worm): repeated segments, jaws, a body that moves as a travelling wave | SEGMENTATION ITSELF: every segment is its own lifeform with its own heart (a fanged, shield-plated head; core-prism bodies; a bladed tail), following the leader, shedding and growing segments, lunging | the Serpent hull: one continuous body. The colony is a string of beads (0.16 from the Meerkat hull seen end to end) |
| Manta | Mobula: a diamond of two wings beating in a wave from shoulder to tip, two lobes funnelling food to a wide mouth, a whip with no sting, loops and rolls through its food | (new, `specs/manta.py`) the DIAMOND, the WAVE and the WHIP: four rib-fan wings a quarter turn apart so the diamond shows from any side, lobes curled round the Etherion, a whip that carries the sting, a wingbeat that never holds an up | the Manta hull: a thin flat plane (a bar from the side, a line from ahead; 0.53 from the fauna) |

**The method** (the worksheet of section 3, done properly):
1. **Name the clade's SIGNATURE: what a child would draw, and how it moves.** Two or three shapes and one motion (the
   manta: a diamond, a whip, lobes; a slow wave). Not its anatomy list.
2. **Say what the vessel of that name already took**, and take something else. The silhouette gate holds you to it.
3. **Re-derive each signature under the laws instead of copying it.** No up, so the diamond is drawn four times. The
   heart leads, so the lobes curl round it from behind. Every body carries a sting, so the whip does.
4. **Choose a BODY PLAN, not a limb count.** Plans the game has: a spine with radial vanes (Bee), a jaw cage with
   swept vanes (Shark), a flat rigged star (Brittlestar), a chain of member bodies (Worm), a loop frame with fin
   plates (QuadFish), a hollow horn with flukes (Clawfish), a single braid (Tadpole). New in round 2: **rib-fan
   wings** (a spar chain, ribs, a hem: the wire is the membrane). Changing n on an old plan is a variation.
5. **Give it a motion of its own** as a `beat` on its spindles (a rotation about the limb's root: a CPU driver
   writing `localRotation`, /fauna section 3): `{"axis": "travel" | "tangent" | [x, y, z], "deg", "hz", "phase"}`.
   Phase lags down a chain make the wave. The lab plays it.
6. **`silhouette.py --check`, then the sheet (`--sheet`), and LOOK** before the lab.
7. Record the translation in the spec's `clade` block (`real`, `kept`, `changed`, `vessel`); the lab shows it.

**Plans not yet used, from clades not yet touched** (proposals for the next rounds, one creature each, Garrett picks):
a lattice sphere round the heart (radiolarians); eight comb rows running the body's length, beating in sequence
(comb jellies); feathered arms that branch and branch again (feather stars, basket stars); a chain of linked bells
that pulse in turn (salps, siphonophores); a coiled chain of growing chambers (nautiloids: a spiral, not an umbrella);
a tube with an opening and closing crown (fan worms); paired swimming plates down a segmented body (eurypterids,
anomalocarids); a rosette of soft horns (nudibranchs).

## Round status and what each gate still owes (2026-11-07, measured for Docs/SKILL_TRAINING_PLAN.md 3.7)

**Round 4 (2026-11-16, `Docs/Studios/FAUNA_LAB.md` round 2):** section 9 (the clade method), law L12 and
`silhouette.py` (`--self-test`: a rolled, flipped, scaled copy must read the same and a dart and a disc must not; it
caught the first version scaling each view on its own), limb beats in specs, the Manta (a lab proposal), round 1's
five archived as one body plan.

**Round 3 (2026-11-15, `Docs/Studios/FAUNA_LAB.md`):** the dossier and the recreation gate (seven shipped creatures,
largest difference 0.00002 u; the Bee rebuilt from rules), law L11, five new species as specs and lab proposals
(Marlin, Basker, Medusa, Nautilus, Teredo: nothing in Unity), the Fauna Possession Lab. New gates and their controls:
`dossier.py --self-test` (the mesh id rule and the axis rule each have a control), `prefab_read.py --self-test`,
`recreate.py --self-test` (nine planted drifts), `lab_export.py --check`; `celestial.py --self-test` gained L11's three.
Still owed from the table below: `build.read_back` and `render_prefab.py` self-tests.

**Rounds so far:** one species through the kit, the bee (`Tools/Build/fauna_kit/specs/bee.py`, rounds 1-2 on
2026-11-01, Garrett: "a fine start"); nine fauna models ship without a spec (shark, bonita, swordfish, three worms,
clawfish, mediumfish, brittlestar). **Next unit proposed:** the swarm (Garrett, 2026-10-21: "the swarm feels like a
small pack not ... a murmuration"), or the shark as the first shipped body re-specified. Owner to ask: Garrett.

**Gates and their negative controls** (a negative control is a planted defect asserted to FAIL; "owed" means the
gate is green today without ever having been proven to fire):

| Gate | Script | Negative control today |
|---|---|---|
| the ten celestial laws on the spec | `fauna_kit/celestial.py --self-test` | yes: 15 planted defects (L1-L3, L5-L9); L4 (three health prisms) and L10 (premise) have none |
| the built prefab held to the spec | `author_bee_assets.py --self-test` | yes: 8 planted defects, 8/8 caught |
| `build.read_back` (prefab-to-spec drift) | `fauna_kit/build.py` | owed: no `__main__`; `--self-test` prints nothing and exits 0, a gate-shaped command that passes by doing nothing |
| `render_prefab.py` refuses a prefab that is not its spec | `fauna_kit/render_prefab.py` | owed: the refusal is never asserted |
| the heart clears the body | `verify_fauna_heart_seat.py` | yes: two rejected Clawfish seats inline; but it walks model-instance bodies only, so a kit-built species is not covered |
| the heart is the size the band says | `author_lifeform_heart_sizes.py --check` | owed, and the gate is RETIRED (it stands down green): /fauna section 8 still lists it |
| every heart carries its collection pair | `author_lifeform_heart_collection.py --check` | owed |
| serialized-field parity | `asset-surgery/field_parity.py` | owed: a silent pass, nothing says what was compared |
| the collider-budget delta | none: stated by hand | owed: `author_bee_assets.py report()` prints the numbers, no gate holds them |

**The sheet today:** `preview.py` (the spec, four views) and `render_prefab.py` (the built prefab on the real braid
mesh), no yardstick beside them. The look sheet (`/unity-look`) adds the creature in its cell at the game's distance.

**To promote to A:** give `build.read_back` and `render_prefab.py` a `--self-test` with one planted drift each (the
kit's owner, Garrett); drop the retired heart-size gate from /fauna section 8 or say it is retired there; a round
table per species in this file as each one lands.
