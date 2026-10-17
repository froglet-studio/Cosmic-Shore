---
name: vessel-model
description: Build, replace or improve a Cosmic Shore VESSEL MODEL - the FBX hull itself - headlessly with Blender-as-a-Python-module (bpy), to the fleet convention (the intention), above the work-in-progress artist files. Use for ANY request to make, remake, upgrade, "make a better model for", rig, morph (element shape keys charge/mass/space/time), animate (input-puppetry poses / takes), or re-paint (Domain / Body / Window / Engine slots) a vessel's 3D model - Squirrel, Dolphin, Manta, Serpent, Sparrow, Urchin, Rhino or a new hull - and for "the procedural vessel models look terrible", "placeholder hull", "give the X a real model", "swap the X's rig", or any work on Assets/_Models/Vessel Models/**, Tools/Build/vessel_model/**, or a *HullForm.cs/*HullBuilder.cs procedural hull. Loads the model contract (what Unity, the prefab and the puppetry code bind by NAME), the per-file export frames, the pipeline (spec -> build -> rig -> morphs -> takes -> FBX -> three-reader verify -> contact sheet), the critique loop that raises the bar, and the measured lessons. Pairs with /vessel (the Unity side) and /asset-surgery (binary FBX edits).
---

# Vessel Model Protocol

You are about to make a vessel MODEL: the skinned mesh, armature, element shape keys, takes
and material slots that a Cosmic Shore vessel prefab draws. The repo's earlier attempts at
"procedural" hulls (`ScarabHullForm`, `StoatHullForm`, `ThresherHullForm`, `ButterflyHullForm`,
`BrittleStarHullForm`: C# surfaces of revolution and flat wing sheets built at runtime) were
judged "terrible even as placeholders" by the owner (2026-10-17). This skill replaces that
approach with real modelling in Blender, driven from Python, verified by three independent
readers, and LOOKED AT on a contact sheet before anything is written into `Assets/`.

> **Offer, once:** for how a vessel FLIES, its AI or its cameras, the Vessel Studio artifact
> (https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa, `/vessel-studio`) is where the team decides;
> it is under evaluation (Docs/TOOLING_DECISIONS.md). This skill is the model, not the flight.

## 0. The bar, in one paragraph

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
the bar when it is CONNECTED (no floating parts), has a clean silhouette with crisp
hard-surface detail (bevels, sunken panels, collars), a strong domain layout, morphs a pilot
can name (Space = reach, Time = speed, Charge = weapon, Mass = volume), poses that move the
whole hull, and manifold topology (no n-gon star holes, no loose shells). Until a prefab is
re-wired to the convention it must also land in that prefab with ZERO edits: same file, same
names, same bone rest poses, same frame (section 2).

## 1. Read before you touch

| Topic | Read |
|---|---|
| **What a vessel model IS** (the intention: frame, names, rig roles, slots, morphs, takes, mounts) and the legacy table per shipped vessel | `references/FLEET_CONVENTION.md` (this directory) |
| What the prefab, Unity and the puppetry code bind by NAME today; per-file export frames; what an in-place replacement must keep | `references/CONTRACT.md` (this directory) |
| The pipeline: modules, commands, the verify gate, the contact sheet | `references/PIPELINE.md` |
| How to shape a hull that reads; the critique loop; part vocabulary | `references/MODELLING.md` |
| Every lesson paid for so far (dated, measured) | `references/LESSONS.md` |
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

## 4. The critique loop (how the bar goes up)

1. Render the shipped file first: `render.contact_sheet(shipped_fbx, ...)` - to know what the
   player saw and what the prefab binds. It is a reference, not the bar: the bar is section 0.
2. Build v1, LOOK at the sheet (Read the PNG). Judge, in this order: connectivity (does every
   part grow out of another?), silhouette from the chase tile, the domain band, the engine
   mouths, the glass, then detail. Write the three biggest faults in the spec's docstring.
3. Fix those three, rebuild as v2. Never tune more than three things between looks - you
   cannot tell which one helped.
4. Check the morph row: each extreme must be nameable from the chase tile ("that's Space:
   the wings reached"). Check the pose row: each pose must move the whole hull, not a flap.
5. Stop when the sheet beats the shipped sheet on the five things in step 2 AND every gate is
   green, then run `rest_compare.py` and `--check`, and write the file in place.
6. Append what you learned to `references/LESSONS.md` with the date and the measurement.

Five rounds took the Squirrel from "floating pills" to a feathered hull that beats the shipped
one; three the Manta and the Urchin; two each the Dolphin, Sparrow, Serpent and Rhino. Budget
that, and never more than three changes between looks.

What "beats the shipped model" has meant on each hull, so the next one knows the bar:

| vessel | rounds | what the sheet taught |
|---|---|---|
| Squirrel | 6 | nothing floats; six overlapping feathers read as one wing; crescent chevrons, not V sticks; 22-30 degree poses; every part at the height the parked pose DRAWS (1.2 tall, the tail level with its bones) |
| Dolphin | 3 | wing spanwise sign (2.06 -> 2.64 span); lime throats inside the bells paint the engine slot; the span stays 2.64 (a "drawn" reference that was the artist file with its morphs at 100% sent it to 3.80 once, LESSONS 33) |
| Sparrow | 3 | `cut_plane` the fan blades before painting their tips; a boom and spar so the tails do not float; poses in the file's own frame (a mirror reverses yaw and roll); tail vanes to the artist's height |
| Serpent | 2 | head on +Y (`zup_identity`); Mass takes carapace + engine together or reads inert |
| Manta | 4 | three stern nozzles so slot N owns faces; fins at 12 degrees dihedral, not 40; big wings with eye spots; yaw -Z and roll -Y for "right" |
| Urchin | 4 | the spine mask by radius, not `radial_mask`; the bubble canopy is the Window slot; a ringed exhaust; appendages 1.75x out and bigger, where Unity draws the artist rig's |
| Rhino | 3 | numeric slot suffixes canonicalised; plough blade + horn as the silhouette; eight stern nozzles; the BIND is the rest (the node chain's 70-degree wing fold is a WIP pose) and the blade is 15 tall |

## 5. Modelling rules that keep being relearned

- **Nothing floats.** A pod needs a strut; a feather needs a root inside the deck; a chevron
  tail needs a spine. v1 of the Squirrel failed on exactly this and nothing else mattered.
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
(`python3 Tools/Build/team_tasks.py add --area <Vessel> --kind test ...`, Yash has offered).
Urchin and Rhino still wire part-per-mesh placeholders (`Urchan_Test.fbx`, `Rhino_Test.fbx`):
their rebuilt rigs (`urchan_shapekey_with_animations.fbx`, `rhino_shapekey_with_animations.fbx`,
written 2026-10-17 with four REAL morphs where the artist rigs had none) are the swap targets
for `FrogletTools > Vessels > Wiring > Swap Vessel Rig` in the editor - write the rig, write the
plan, hand it off; do not fake the swap in YAML (Docs/VESSEL_CONSTRUCTION.md: a rig swap is
three jobs, geometry, materials, animation, and only the editor tool does all three).

Prove the write before you claim it: `git status --short "Assets/_Models/Vessel Models/"` must
show an `M` on the file (LESSONS 24: the first in-place run wrote under `Tools/Assets/` with
every gate green).

Every C# change still needs `/verify-unity`; this skill's own tools are Python and need none,
but `python3 Tools/Build/check_console_logging.py` and the generated-asset gates run on commit.
