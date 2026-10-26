---
name: vessel-model
description: Build, replace or improve a Cosmic Shore VESSEL MODEL - the FBX hull itself - headlessly with Blender-as-a-Python-module (bpy), to the fleet convention (the intention), above the work-in-progress artist files. Use for ANY request to make, remake, upgrade, "make a better model for", rig, morph (element shape keys charge/mass/space/time), animate (input-puppetry poses / takes), or re-paint (Domain / Body / Window / Engine slots) a vessel's 3D model - Squirrel, Dolphin, Manta, Serpent, Sparrow, Urchin, Rhino or a new hull - and for "the procedural vessel models look terrible", "placeholder hull", "give the X a real model", "swap the X's rig", or any work on Assets/_Models/Vessel Models/**, Tools/Build/vessel_model/**, or a *HullForm.cs/*HullBuilder.cs procedural hull. Loads the model contract (what Unity, the prefab and the puppetry code bind by NAME), the per-file export frames, the pipeline (spec -> build -> rig -> morphs -> takes -> FBX -> three-reader verify -> contact sheet), the critique loop that raises the bar, and the measured lessons. Pairs with /vessel (the Unity side) and /asset-surgery (binary FBX edits).
---

# Vessel Model Protocol

You are about to make a vessel MODEL: the skinned mesh, armature, element shape keys, takes
and material slots that a Cosmic Shore vessel prefab draws. The repo's earlier attempts at
"procedural" hulls (`ScarabHullForm`, `StoatHullForm`, `ThresherHullForm`, `ButterflyHullForm` (replaced
2026-10-18 by `Butterfly.fbx`, `specs/butterfly.py`), `BrittleStarHullForm`: C# surfaces of revolution and flat wing sheets built at runtime) were
judged "terrible even as placeholders" by the owner (2026-10-17). This skill replaces that
approach with real modelling in Blender, driven from Python, verified by three independent
readers, and LOOKED AT on a contact sheet before anything is written into `Assets/`.

> **Offer, once:** for how a vessel FLIES, its AI or its cameras, the Vessel Studio artifact
> (https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa, `/vessel-studio`) is where the team decides;
> it is under evaluation (Docs/TOOLING_DECISIONS.md). This skill is the model, not the flight.

## 0. The bar, in one paragraph

> Garrett, 2026-10-18, after testing the first rebuilds in the Editor: "the sparrow, dolphin
> and squirrel all appeared to be cheap remakes that were very far from being ready to replace
> the original" - "prove that you can fix one of the remakes first. then convert your learnings
> into the new skills. then we can try to remake another."

**The artist's hull is the base. Build ON it, never from primitives, for any vessel that has
one.** A hull redrawn from lathes, plates and lofts reads as a toy next to the artist's mesh
(LESSONS 38), and no offline render told us so; the owner's Editor did. So the path for an
existing vessel is `rig.import_shipped_model` (the artist's mesh, groups, shape keys, slots
and colours, exactly as the game draws them) + the framework on top: the parked pose baked
into the rest, missing or weak morphs authored or strengthened on the artist's topology,
the artist's takes kept (`takes.retarget_from`) and proven part by part against the artist file
(`take_parity.py`, on by default in verify), the hovering assembly kept (section 0.5), every gate.
The Squirrel is the worked example (`specs/squirrel.py`); the from-scratch specs live in
`specs/experimental/` and never write in place. **One vessel per round**: build it, let the
owner fly it, fold what he says into this skill, then the next. Never rewrite several live
files in one pass again, and never flip a vessel's heading on a measurement (LESSONS 39).

> Garrett, 2026-10-18, after the Butterfly: "excellent. don't forget they are spaceships inspired
> by earth clades. please continue with the rest of the fleet, and get the card images etc.
> updated as well".

**Every vessel is a SPACESHIP inspired by an Earth clade, never the animal itself.** The clade
gives the silhouette, the proportions and the rhythm of the parts (the Squirrel's bushy tail is
a chain of chevron plates, the Dolphin's flukes are swept wings, the Butterfly's four wings
beat on hinges); the ship gives everything you build it from: hard-surface hull plates with
bevels and panel seams, a cockpit or sensor glass where an eye would be, engines and their
glowing mouths, domain colour as plates, trim lines and running lights, and mechanical joints
(hinges, collars, pistons) wherever the animal has a joint. Test every part with one question:
"what is this on the ship?" (an antenna is a sensor mast, an eye a canopy, a vein a structural
rib, a tail a boom). A part with no ship answer is a costume and goes. A hull with no animal
read is a generic fighter and fails the other half. When the fleet is redone in one pass, as
the owner asked on 2026-10-18, each vessel still gets its own spec, gates, sheet, commit and
flight test; one failing hull never holds the others.

> Garrett, 2026-10-17: "not overindexing on the flaws in the various vessel implementations.
> They are all a work in progress, so we should be building a framework that meets our
> intentions not mimic halfbaked examples."

So the bar is the INTENTION, written down once in `references/FLEET_CONVENTION.md` (one file
frame, one naming and rig-role scheme, one slot order, four element morphs with one meaning
each, nine input takes with one sign convention, bone-hosted FX mounts), not the shipped
artist files. Those files are measured in `Docs/VesselModels/` so a session knows what the
prefabs still BIND, which is the only reason any of their quirks survive in a spec, and every
such quirk is a legacy binding with a named migration step, never a rule.

A vessel model is judged from the chase camera (17-250 units behind, 60 degree FOV) at
250 pixels wide. What reads there is SILHOUETTE, the DOMAIN band, the engine mouths and the
cockpit glass; what does not read is anything under 2% of the hull's length. A model meets
the bar when it is a HOVERING ASSEMBLY (section 0.5: distinct parts on their own bones, floating clear across even,
deliberate gaps, aligned and animated together), has a clean silhouette with crisp
hard-surface detail (bevels, sunken panels, collars), a strong domain layout, morphs a pilot
can name (Space = reach, Time = speed, Charge = weapon, Mass = volume), poses that move the
whole hull, and manifold topology (no n-gon star holes; each part one closed shell). Until a prefab is
re-wired to the convention it must also land in that prefab with ZERO edits: same file, same
names, same bone rest poses, same frame (section 2).

## 0.5 A vessel is a HOVERING ASSEMBLY (Garrett, 2026-10-21 - this overrides the old "nothing floats")

> Garrett, 2026-10-21, after flying the Orca: "the original vessels are based largely around the idea of free floating
> parts that are associated and animated together but clearly distinct parts of a hovering assembly. this seems to not
> be observed in the new models and needs correcting in how the vessel models are updated" - "this will also help keep
> the guns associated with the vessel but not have to worry about mating planar surfaces".

**Every vessel is a core plus distinct parts that float clear of it and of each other**, each on its own bone, aligned on
shared axes, and animated TOGETHER (puppetry or takes). Wings, fins, tail segments, engine pods, shells and guns are
separate closed shells across deliberate, even gaps; nothing is welded on with a strut, a root inside the body or a
fillet. The gap is the design: it keeps a gun or an engine visibly part of the ship without mating surfaces, and it lets
each part move (hover, swing, lean, recoil) without tearing a seam.

**Measured on the artist hulls** (`assembly_report.py --fleet`, gap to the nearest other part, % of the mesh diagonal):
| hull | parts that float | the gaps |
|---|---|---|
| Manta | 7 / 7 | wings 3.4-6.5 %, chassis 5.2 % |
| Dolphin | the wings and jaws | wings 5.7 %, jaws 1.2-1.3 % (the jet rings nest at 0.03-0.08 %) |
| Squirrel | 28 / 39 | wing pieces 0.9-1.2 %, front engine tips 0.9 %, back engine frames 0.45 % |
| Serpent | 22 / 27 | tail spikes 0.7 %, bottom tails 0.45 % |
| Sparrow | 14 / 47 | tail segments 0.8-1.1 %, shells 0.4-0.5 %, wing plates as 0.1-0.2 % hairline seams |

So: **major parts (wings, fins, engine pods, guns, tail segments) float 0.5-6.5 % clear; plates within one part may sit
on 0.1-0.2 % hairline seams.** Every hull built from scratch before 2026-10-21 (Butterfly, Thresher, Scarab, Stoat,
Angler, Brittle Star, Shrike, Grouper, the Wrasse's kite, the first Orca) was built to the opposite rule and welds its
parts (the first Orca: 1 / 22 parts float, median gap 0.12 %).

**The gate:** a new hull's spec names its floating parts, and `assembly_report.py <fbx> --floating <parts>` fails when
one touches a neighbour (under 0.4 %) or drifts off (over 10 %). It is watched failing on the first Orca. Squirrel v1's
lesson still holds, read correctly: its parts failed because they were shapeless PILLS scattered off-axis, not because
they floated (LESSONS 12, 49).

## 1. Read before you touch

| Topic | Read |
|---|---|
| **What a vessel model IS** (the intention: frame, names, rig roles, slots, morphs, takes, mounts) and the legacy table per shipped vessel | `references/FLEET_CONVENTION.md` (this directory) |
| What the prefab, Unity and the puppetry code bind by NAME today; per-file export frames; what an in-place replacement must keep | `references/CONTRACT.md` (this directory) |
| The pipeline: modules, commands, the verify gate, the contact sheet | `references/PIPELINE.md` |
| How to shape a hull that reads; the critique loop; part vocabulary | `references/MODELLING.md` |
| Every lesson paid for so far (dated, measured) | `references/LESSONS.md` |
| How a vessel is COLOURED: the colour language (Blue neutral, Fire danger, Lime call to action, Ruby/Gold/Jade domains), the material scheme, the card livery | `Docs/VesselModels/COLOR.md`; numbers in `Tools/Build/vessel_model/vessel_colors.py` |
| The vessel's game contract (abilities, HUD, morph rule, rig swaps) | `/vessel` -> `.claude/skills/vessel/references/CONTRACT.md` section 7 |
| Binary FBX surgery, fileID rules, assimp validation | `/asset-surgery` section 4.8 |
| Per-vessel dossiers (what each artist file contained, measured; the constraints a replacement meets) | `Docs/VesselModels/<Vessel>.md` (README lists them) |

Then run, before any change (Docs/TEAM.md): `git fetch origin` and
`python3 Tools/Build/who_works_where.py query "Assets/_Models/Vessel Models" Assets/_Prefabs/Spacevessels/<Vessel>.prefab`
and say who else works there. Thirteen branches touched `Spacevessels` in the week this skill
was written; a model swap is one file and must not collide with their prefab edits.

## 2. The contract today (legacy bindings an in-place replacement keeps)

`FLEET_CONVENTION.md` says what a model IS; this section says what today's prefabs, `.meta`
files and drivers still bind, so a file written over a live one needs no editor. Every item
below that differs from the convention is a binding to retire (the convention's section 7
names the step), not a design choice to copy. One skinned mesh on one armature, exported as
binary FBX 7400 IN PLACE over the live file:

- **Names are identity.** Unity derives sub-asset fileIDs from name + type and they are NOT
  derivable offline (asset-surgery 4.8c). Keep the file path (its guid), the mesh node name,
  the geometry name, the armature name, every bone name and the slot material NAMES. Renaming
  one node silently detaches whatever the prefab hung on it - a jet floats at the origin with
  no error. A new file beside the old one re-hashes every id and needs the editor.
- **Bones rest where the shipped ones rest, in the NODE chain Unity reads.** Jets, colliders
  and FX are children of bones with offsets in bone space, and the editor rig tools were
  written against those rest poses. Reuse the artist's armature wholesale
  (`rig.import_shipped_armature(ctx.artist(OUTPUT))`, from the pinned git ref, never from the
  file on disk); `skin_rigid` bakes the artist's parked pose into the rest
  (`rig.bake_parked_pose`: the Squirrel, Urchin and Rhino rigs' node transforms differ from
  their bind pose, and Unity builds the bone Transforms from the nodes); prove it with
  `rest_compare.py` against the artist file, whose two readers (Blender's bind-derived rest and
  assimp's node chain) must both say 0.000. New bones are allowed; moved ones are not.
- **Four material slots, shipped order and names.** The game writes slot 0 as the domain
  colour on a SkinnedMeshRenderer (`ShipHelper.ApplyShipMaterial`), flashes slot 3 as the
  engine (`FlareEngine`), and the `.meta` remaps slots by NAME (`externalObjects`). Squirrel /
  Serpent / Sparrow: `Domain, Body, Window, Engine` (in THEIR shipped order - it differs per
  file); Dolphin / Manta rigs: `accent, BASE, windsheild, N`. Every slot must own polygons or
  Unity drops it and shifts the array.
- **Four element shape keys** named `charge / mass / space / time` (token rule in
  `VesselElementalMorphConfigSO.TryResolveElement`; the Squirrel ships them capitalised, the
  Dolphin lowercase - keep the file's case), last-frame weight 100, with normals, each moving
  the hull by 3-15% of its diagonal (the audit calls less than 0.1% INERT, `VesselElementalMorphAuditor.MinShapeTravelFraction`). And NO blend-shape
  animation curves in any take (`export.strip_shape_curves` deletes Blender's residue).
- **Takes by the shipped NAMES** (`<Armature>|<action>`): the Animator-driven vessels bind
  nine single-frame POSES through 2D blend trees (idle, pitchup/down, yawleft/right,
  rollleft/right, speedup/down) and the `.meta` pins each take name to a clip id; the
  code-puppetry vessels (Dolphin, Manta, Rhino, Urchin) bind nothing but keep the names.
  Poses are authored about WORLD axes (`takes.pose_take(world=True)`), so a spec means the same
  thing on any rig. Make them BIG: the Squirrel's top blend tree halves every pose.
- **The file frame is the shipped file's frame** (`export.PRESETS`): `zup` for the Squirrel /
  Sparrow (Z-up, nose file +Y, metres, root nodes identity), `zup_identity` for the Serpent
  (same, written with identity axes: nose on Blender +Y), `yup` for the Dolphin / Rhino rigs
  (Blender's default: Y-up, cm, mesh and armature R(-90,0,0) S(100)), `yup_baked` for the
  Manta / Urchin rigs (mesh node identity with the cm baked in). The mesh node's LOCAL frame
  is where the prefab's colliders are authored, so a different convention rotates them;
  `fbx_probe` the shipped file and read the mesh node's TRS before choosing.
- **Size and nose** match the shipped hull within 5%: cameras, colliders, jets and the tail
  were all tuned to it (Docs/VESSEL_TAIL_AND_JETS.md section 4).

## 3. The pipeline (one command per build)

```
python3 Tools/Build/vessel_model/build.py <spec> --out <scratch>/<vessel>/vN.fbx --sheet-dir <scratch>/<vessel>/vN
python3 Tools/Build/vessel_model/rest_compare.py "<shipped.fbx>" <scratch>/<vessel>/vN.fbx   # bones rest in place?
python3 Tools/Build/vessel_model/fbx_probe.py <file.fbx> --takes                             # anything, measured
python3 Tools/Build/vessel_model/part_map.py "<shipped.fbx>"                                 # where every part/bone sits
python3 Tools/Build/vessel_model/build.py <spec> --check                                     # the drift gate vs the committed file
```

A spec is `Tools/Build/vessel_model/specs/<vessel>.py` exposing `NAME`, `OUTPUT` and
`build(ctx) -> {"armature", "mesh", "spec"}`; copy `specs/squirrel.py` (Animator-driven, rig
reused) or `specs/dolphin.py` (code puppetry, rig reused) or `specs/_wedge.py` (a new rig).
`build.py` runs: `forms` (parts) -> `join` (one mesh, a vertex group per part) -> `rig`
(skin rigid by part) -> `morphs` (deformers over part masks, baked) -> `takes` (poses) ->
`export` (preset, strip, then THREE readers: the repo codec + probe, `assimp info`, a Blender
re-import in a fresh process) -> `render` (Cycles CPU contact sheet). A failed verify prints
every failure and writes nothing you would trust; fix the spec, not the gate.

## 4. The loop that passed (how the bar goes up)

> Garrett, 2026-10-18, on the round-8 Squirrel: "This passes. I upgraded its elements. changed
> domain. I can't tell it isn't the original."

That is the bar for a remake of an existing vessel: **the owner cannot tell it from the
original in play, AND it carries the framework the original lacked.** Eight rounds got there;
the last two are the loop to repeat on every vessel (LESSONS 42 has why each step exists):

1. **Start from the artist's file, not a blank scene.** `rig.import_shipped_model` + `bake_parked_pose(deform_mesh=True)`.
   Gate: rest bounds == the artist's drawn bounds; `rest_compare.py` 0.000.
2. **Keep everything the owner already accepted**: mesh, slots, colours, morphs, takes
   (`takes.retarget_from`). Gate: `take_parity.py` passes with NO allowances.
3. **Add only what the original lacks**, one kind of change at a time, each weighted so it
   follows the existing rig (the Squirrel: a tail spine; a stronger Mass key). Gate: the
   parity gate still passes - an addition must not change how an existing part moves.
4. **Look at the sheet**, then hand ONE vessel to the owner to fly. His words are the result;
   offline renders and gates only say what he will not have to point out.
5. **Fold what he said into this skill before the next vessel** - a new gate if a check could
   have caught it (the parity gate came from "the rear jet ports are staying stationary").

A vessel with NO artist file (the Butterfly) follows PIPELINE "The new-hull path" instead: the
vessel's doc and prefab are the brief, the fleet's artist hulls are the look, and the owner's
flight is still the result.

A change the owner asked for (a new motion, a new silhouette) is the only reason to depart from
the artist's file, and it is declared (`take_parity_allow`, a spec docstring line) so the
gates keep proving everything else is untouched.

What "beats the shipped model" has meant on each hull, so the next one knows the bar:

| vessel | rounds | what the sheet taught |
|---|---|---|
| Squirrel | 8 | **PASSED the owner's eye (2026-10-18): "I can't tell it isn't the original", elements upgraded and domain changed in play.** Six from-scratch rounds were a cheap remake; round 7 put the ARTIST'S hull back with a spine and Mass 2.5% -> 3.7% ("big improvement") but re-authored takes froze the rear engines; round 8 keeps the artist's takes (take_parity 0.001 / 0.1 deg over 311 part-takes) and weights the spine between chevrons. `specs/squirrel.py` is the template for every next vessel |
| Dolphin | 4 | three from-scratch rounds withdrawn; round 4 is the loop's second vessel (2026-10-18): the artist's hull, morphs, materials and ten motion takes, nothing visible added, reproduced to 0.0000 units / 0.00 degrees over 146 part-takes after three reader blind spots were fixed (LESSONS 43). `specs/dolphin.py`; the dossier's weak points are the owner's to pick |
| Butterfly | 1 | **the first NEW hull** (2026-10-18): no artist file, so it replaces the procedural placeholder with a model to the fleet convention and look (`specs/butterfly.py`, PIPELINE "The new-hull path", LESSONS 44). Four hinged wings built as radial grids so the domain field, dark frame, cells and eyespots follow wing anatomy; veins raised in the membrane (floating tubes read as needles); 13,948 tris; integrated by name; a wing sign bug in `ButterflyAnimation` (the Fold closed under the belly) found and fixed. Awaiting the owner's flight |
| Sparrow | 4 | rounds 1-3 from-scratch, withdrawn and it FLEW BACKWARDS (LESSONS 39: the artist's heading is right by definition). Round 4 (2026-10-18, the fleet pass): the artist's hull reproduced exactly through the loop, 466 moving part-takes, 95 part-morphs, materials and rest identical; the artist's 0.2034 mesh-node scale kept (`rig.restore_node_scale`, `mesh_node_scale`). `specs/sparrow.py` |
| Serpent | 3 | rounds 1-2 from-scratch, withdrawn (what held: head on +Y, `zup_identity`). Round 3 (2026-10-18): reproduced exactly through the loop, 110 moving part-takes, 29 part-morphs. `specs/serpent.py` |
| Manta | 5 | rounds 1-4 from-scratch, withdrawn (what held: yaw -Z and roll -Y for "right"). Round 5 (2026-10-18): `Manta_shapekey_rigged.fbx` reproduced exactly, 27 part-morphs, no takes in the file (MantaAnimation puppets in code). The prefab's second, coincident hull (`mantis_shapekey_with_animations.fbx`, VESSEL_CONSTRUCTION 3.4) is untouched. `specs/manta.py` |
| Urchin | 5 | rounds 1-4 from-scratch, withdrawn, and the game never drew it (LESSONS 40). Round 5 (2026-10-18): the RIG SWAP. The prefab draws the artist's rig (`specs/urchin.py`) with four real morphs (mass 4.4, charge 9.3, space 6.0, time 10.9%); the rest is the first key of `urchan_fly` (the saved pose was a one-sided yaw take), takes retargeted as motion relative to rest because Blender cannot hold the holds' 1.75 rest scale; colliders stay on the placeholder parts and the bones follow them (LESSONS 45) |
| Rhino | 4 | rounds 1-3 from-scratch, withdrawn. Round 4 (2026-10-18): the RIG SWAP. The prefab draws the artist's rig (`specs/rhino.py`) with four real morphs (charge 7.0, mass 4.6, space 8.5, time 6.9%); the BIND is the rest (the saved 66.5-degree wing fold is the last frame of a take), gated against a bind-rest reference; the bones follow the placeholder parts, which keep the plough's colliders (LESSONS 45) |
| Thresher | 1 | new hull (2026-10-18, `specs/thresher.py`): a predator hull with gill louvres, nacelles and a three-segment whip boom; the chain hitch at the placeholder's point; found and fixed the swim's yaw sign (the tail swung INTO a turn) |
| Scarab | 1 | new hull (`specs/scarab.py`): faceted elytra shells over an engine core, dozer prow, forge lance, sensor-mast antennae, six landing struts; the hidden Sparrow wrapper removed from the prefab. Answers Garrett's 2026-09-01 "contiguous creature body" note with connected modules; that reading is his to confirm |
| Stoat | 1 | new hull (`specs/stoat.py`): five overlapping armour segments, sensor-fin ears, nacelles, a domain-tipped tail beacon |
| Angler | 1 | new hull (`specs/angler.py`): its own model and `AnglerAnimation` (it wore the Stoat's); ram-scoop jaw, a lure mast with an Engine-slot esca bulb that swells while luring |
| Brittle Star | 1 | new hull (`specs/brittlestar.py`): a five-lobed core, five segmented gun/thruster booms, the halo as a structural beam (Time turns it from a pentagon to a circle); muzzles, nozzles and ring numbers exact |
| Goby | 1 | new hull on a BORROWED rig (2026-10-18, `specs/goby.py`, LESSONS 48): the Goby wore the Squirrel's model, so the new hull keeps the Squirrel's armature, bones (rest 0.0000), node, geometry, slot, shape and take names, and `Goby.prefab` binds it by a guid swap alone; the Squirrel's controller and takes still drive it (the tail chain swims the goby's banded body). Lookout sensor turrets, framed pectoral fans with tip pods, a gill-louvred head, a sail, a swept stub wing carrying the rear nacelles, a rounded caudal fan. Awaiting the owner's flight |
| Orca | 1 | new hull (2026-10-21, `specs/orca.py`): the 4-seat crew vessel (one helm, three gunner turrets) and the first hull rigged for CODE-AIMED turrets: per seat a `<Seat>Yaw` bone exactly at `OrcaCrewCore.Mounts[s] * hullRadius`, a `Pitch` child, and `Muzzle` / `Missile` leaves that skin their tip parts so `optimizeBones` keeps them. Three bespoke guns (railgun, six-barrel "pen" rotary, mortar), each built at unit size and scaled about its pivot, which never moves. The orca's white became CONFORMAL domain armour plates (a smooth outline on the hull surface instead of a stair of painted faces) and the eye patch became a lens part. Round 1 was a blimp under a pole: grow the hull until the pylon stops reading as a neck. Not wired yet: the prefab still draws the Sparrow |
| Shrike | 1 | new hull (`specs/shrike.py`): hooked ram prow over the lance barrel, the eye-mask as a visor band, talon docking clamps (a new grip while docked). The likeliest of the fleet to read as a generic jet: the owner's eye decides |
| Grouper | 2 | new hull (2026-10-20, `specs/grouper.py`): the Wrasse's carrier, sized to the Sparrow it replaces in `Wrasse.prefab` (span 11.9 / length 12.4 against 12.5 / 12.2); ram-scoop mouth over a hinged `jaw`, a sensor / heat-sink crest (`dorsal`), stubby wings with thruster pods, a vectoring tail `tail.1` -> `tail.2`. Round 1's crest read as needles: fins whose chords overlap their spacing read as ONE serrated crest |
| Wrasse Kite | 2 | new body (2026-10-20, `specs/wrassekite.py`): the navigator's 16-unit kite, the first hull whose body is WEIGHTED along a spine (`spine.1` -> `spine.2` -> `tail`, smoothstep hat weights over a 41-station loft) so it swims without a hinge kink; one domain stripe snout to tail. Round 1's long vanes read as planks: ramp a long fin's height in from its front |
| Marlin | 2 | SAVED FOR A SWORDSMAN since 2026-10-21 (Marlin and Jacks became the Jacks; no prefab draws it). Built as a new hull (2026-10-21, `specs/marlin.py`): Marlin and Jacks' billfish on the Rhino's flight rig, nested under the Rhino's fuselage part so `RhinoAnimation` carries it (rig inactive, its bones still turned). Spear, sail (`sail`), sickle fins that FOLLOW the Rhino's plates, crescent `tail.1` -> `tail.2`. Round 1's inset on the concave crescent folded over itself (hatching): paint a concave plate, do not inset it |
| Jack | 3 | new body (2026-10-21, `specs/jack.py`, from the kite's spec and bones): seen from ABOVE in its view, so round 1 (a true-to-clade slim body) read as a line; widen what the camera looks down on and put domain on the BACK |
| Pistol | 3 | new body (2026-10-25, `specs/pistol.py`, LESSONS 52): the Goby and Pistol's second seat, filmed SIDE-ON. Round 1 (a straight tube, flat rings, an egg claw, vertical struts) read as a wasp from the camera's side; round 2's curved boom with pleuron plates, bent struts and a cocked hammer read as a pistol shrimp. A hovering assembly from the first build (19 / 19 parts float). Not wired yet |

## 5. Modelling rules that keep being relearned

- **A hovering assembly, never a welded one** (section 0.5, Garrett 2026-10-21). A pod, a wing, a fin, an engine and
  a gun each float as their own closed shell on their own bone, 0.5-6.5 % of the diagonal clear of their neighbours,
  facing them with parallel edges on shared axes, so the gap reads as designed. No struts, no roots buried in the deck,
  no fillets. (This replaces "nothing floats", a misreading of Squirrel v1, whose separate parts failed as off-axis
  PILLS: the shipped Squirrel is 39 parts and 28 of them float.)
- **Model in the Blender import frame of the shipped file**: nose -Y, up +Z, pilot's left +X
  (Squirrel, Dolphin, Sparrow); the Serpent's file is identity-exported, so its head points
  +Y. `part_map.py` prints the frame with every part's bounds - copy its numbers.
- **Broad overlapping plates, not radiating sticks.** Six feathers at 6-34 degrees of sweep
  with 0.5-0.6 chords read as one wing; the same six at 8-50 degrees with 0.3 chords read as
  a fishbone (Squirrel v2 vs v3).
- **Bevel every hard part (0.008-0.012), sharpen by angle (38-45 degrees), sink the glass and
  the domain panels (`inset_faces` with depth)**: that is what turns a lathe into a machine.
- **Rigid skinning per part**; blend only organic joints. The puppetry rotates parts rigidly
  and a soft joint reads as rubber.
- **Morph with masks and ramps**: `stretch_along(... ramp=)`, `falloff_mask`, `scale_about`
  with axes - a morph that grabs the whole mesh reads as a scale, not an element.
- **Clear the pose and unassign the action before export** (`export_fbx` does): a live pose
  leaks into every baked take (the dirty-pose leak, LESSONS 2026-10-17).
- **The first take is the rest pose Unity imports.** A hull whose takes are all deflections leads with a `rest`
  take (the build adds it, verify refuses a moving first take). The Shrike rested banked left and the Grouper's tail
  wagged about a left offset until it did (LESSONS 51).
- **Do not bake a driver's bug into the model.** The Squirrel's blend tree halves every pose
  and the Sparrow artist's PitchUp was inverted as flown: author the pose by its meaning
  (pitchup = nose up, big enough to read) and file the controller fix as a team task; a
  model that compensates for a half-baked driver is wrong the day the driver is fixed.

## 6. Wiring and hand-off

In-place overwrite is the only path that needs no editor: same guid, same names, same rest
poses. After writing: re-run `fbx_probe`, `rest_compare`, `measure_vessel_models.py --only
<vessel>`, `build.py <spec> --check`, and record the editor steps nobody here can run
(reimport, `FrogletTools > Vessels > Audits > Audit Vessel Elemental Morphs`, `Audit Vessel
Construction`, `Audit Vessel Tails and Jets`, re-bake `Bake Crystal Hull Fusions` where a
vessel has fusion bakes, a flight) in `Docs/UNITY_VERIFICATION_CHECKLIST.md` and as a team task
(`/team-tasks add --area <Vessel> --kind test ...`, one ticket per branch; it names nobody until someone takes it).
The Urchin and Rhino prefabs draw their rebuilt rigs since 2026-10-18 (`urchan_shapekey_with_animations.fbx`,
`rhino_shapekey_with_animations.fbx`, four REAL morphs where the artist rigs had none). The swap was done by
prefab surgery that does all three of a rig swap's jobs (Docs/VESSEL_CONSTRUCTION.md: geometry, materials,
animation): the placeholder's part transforms STAY, with their colliders, jets and gameplay hooks; the rig is
nested under the hull at the fitted offset with its `.meta` remapping the four slots; and the animation keeps
driving the parts while each drawn bone FOLLOWS its part every frame (`RhinoAnimation` / `UrchinAnimation`,
LESSONS 45). It is not editor-verified: the checklist entry and the team tasks hold the steps. For a hull whose
parts can be retired outright, `FrogletTools > Vessels > Wiring > Swap Vessel Rig` in the editor is still the
tool.

Prove the write before you claim it: `git status --short "Assets/_Models/Vessel Models/"` must
show an `M` on the file (LESSONS 24: the first in-place run wrote under `Tools/Assets/` with
every gate green).

Every C# change still needs `/verify-unity`; this skill's own tools are Python and need none,
but `python3 Tools/Build/check_console_logging.py` and the generated-asset gates run on commit.
