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
| L8 | **The four element forms are written down.** Mass: every plain prism shielded, the body grown until the heart's 12.44 u swing clears every shield. Charge: every prism danger, spindles part hardest. Space: 1.6477× Mass's span, spindles eroded where thickest, prisms refit as needles. Time: see §7 (open). | ECOSYSTEM.md §64.8-9, §65 (Garrett 2026-10-27/28) |
| L9 | **No Earth anatomy names** (leg, belly, back, eye, antenna, wing, thorax...). Name a part for what it is here: spine, vane, rib, plate, sting, tine. | this skill |
| L10 | **A premise and a role.** What it is, the feeling it is for ("surrounded ... scary", Garrett on the substrate), and what it does in the food web. Every lifeform feeds and reproduces. | Garrett 2026-10-08; SUBSTRATE_FAUNA.md |

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
- **Space does not thin a stinger.** `Fauna.ErodeSpindle` skips authored danger (`Fauna.cs` ~1081, "a tooth keeps its
  size"). If the stinger should thin on Space, that is a change to the erosion, decided by Garrett.
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

## 6. Open decisions (ask; do not default)

- **Time's body form.** Nothing anywhere gives Time a body expression (it is "as authored"; its only identity is the
  faster clock, and that is written for flora). Every new species asks Garrett until he sets it.
- **Does the stinger thin on Space** (§5)?
- **What a danger prism does in a skeleton** (ECOSYSTEM.md §26.6, §26.9): the worm is excluded from leaving one because
  of its danger; a stinger species needs the answer.
- **Symmetry order per species** where 3 is not obvious.

## 7. Extending this to other lifeforms (the plan)

The kit is spec-first so the same move works for every kingdom:

1. **Fauna retrofits.** Write a spec for each shipped species and run `celestial.py` on it; the breaks are the redesign
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
