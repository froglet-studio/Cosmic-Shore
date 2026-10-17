# Lessons, dated and measured

Append one entry per lesson, newest at the bottom. State what was measured, not what was felt.

## 2026-10-17 - the first seven models (Garrett: "procedural vessel models are terrible even as placeholders")

1. **Blender runs as a Python module here.** `pip download bpy` fetched a 400 MB wheel
   (bpy 5.2.2 for Python 3.13) and `pip install --break-system-packages` made `import bpy`
   work in plain `python3`. Cycles renders on the CPU (~10 s a 900x700 frame); EEVEE and
   Workbench need an EGL context the container lacks. Everything modelling, rigging,
   shape-key, animation and FBX is therefore real Blender, not a re-implementation.

2. **A C# runtime hull is the wrong tool for a model.** `ScarabHullForm` and its four siblings
   are surfaces of revolution and flat sheets evaluated at runtime; they cannot carry bevels,
   insets, overlapping plates or a sculpted silhouette and every one reads as a placeholder.
   Build the model offline, verify it, ship the FBX; keep the C# for the morph MATH only.

3. **The dirty-pose leak.** Exporting with `bake_anim_use_all_actions=True` while the armature
   still held the last authored pose baked that pose INTO every other take (speedup carried
   speeddown's deflection; speeddown read as rest). Measured by probing each take's wing
   rotation. Fix: unassign the action and zero every pose bone before `export_scene.fbx`
   (`export.export_fbx` does). NLA strips did not fix it; clearing did.

4. **Blender bakes shape-key values into every take** (`Key|<armature>|<action>` curves on the
   BlendShapeChannel deformers) whenever it bakes all actions. The contract wants zero such
   curves. `export.strip_shape_curves` deletes the AnimationCurveNode + AnimationCurve objects
   that target `Deformer` nodes, their Connections and the Definitions counts, with the repo
   codec (72-80 objects per file). Verified by the probe (0 blend-shape curves), assimp and a
   Blender re-import that still lists every bone action.

5. **`FBX_SCALE_NONE` is not "no scaling".** It writes UnitScaleFactor 1 and puts the x100 on
   the root nodes (the Dolphin rig's convention); `FBX_SCALE_UNITS` writes UnitScaleFactor 100
   with the vertices in metres (the Squirrel's). UnitScaleFactor is "centimetres per unit", so
   100 means METRES and Unity imports a metre file 1:1. Match the preset to the shipped file.

6. **Each artist file has its own axis convention**, and the mesh node's LOCAL frame is where
   the prefab's colliders live. Squirrel/Sparrow: Z-up, nose file +Y, coord -X (Blender export
   with axis_forward='-Y', axis_up='Z', bake_space_transform=True reproduces it with identity
   root nodes). Serpent: the same Z-up but written with IDENTITY axes (front -Y), so its
   Blender import frame has the head on +Y. Dolphin/Manta rigs: Blender's default (Y-up, cm,
   R(-90) S(100) roots). Reproduce, never normalise: a Y-up export of the Squirrel would fly
   fine and rotate every bone-space jet offset.

7. **`bake_space_transform` bakes meshes, not armatures.** The armature root keeps the export
   turn as a rotation; the mesh node comes out identity with the turn in its vertices, which
   is exactly the shipped layout (mesh identity, armature carrying a rotation).

8. **Reuse the shipped armature; prove it with Blender, not with matrices.** Importing the
   artist's FBX, dropping its mesh and actions, skinning the new mesh to the imported armature
   and exporting with the matching preset reproduced every bone's world rest pose to 0.000
   units / 0.00 degrees on the Squirrel (42 bones) and the Dolphin (28). A hand-rolled
   `T*R*S` walk of the FBX node transforms disagreed with Blender by a constant 10.8 degrees
   on every Squirrel bone (the artist's armature carries R(71.9,-180,0)); `rest_compare.py`
   therefore reads both files with Blender's importer.

9. **`ignore_leaf_bones=True` eats real bones.** Blender's importer dropped the Dolphin's six
   exhaust bells (`jetin*`, leaf bones that skin 712 vertices each and host the jets) and the
   Squirrel's two `BackEngineInner` bones. Every import in the pipeline passes
   `ignore_leaf_bones=False`.

10. **Pose about world axes, conjugated per bone.** An artist's bones point wherever the artist
    left them (the Squirrel's all along +X, some rolled 90 degrees), so a pose written straight
    into `rotation_euler` pitches one bone and rolls the next. `takes.world_to_bone` computes
    `R_rest^-1 * R_world * R_rest` and the matching local translation; the same pose spec then
    reads correctly on any rig (the Squirrel v5 sheet).

11. **Poses must be big.** The Squirrel's controller halves every pose in its top blend tree;
    the shipped takes drop the tail tip 1.1-1.4 units for pitch and whip it 2.2 units for yaw
    (dossier 3.5). 14-degree curls were invisible; 26-30 degrees read.

12. **Nothing floats; then overlap.** Squirrel v1 (pods and feathers as separate pills) was
    worse than the shipped model on connectivity alone. v2 added forearm/leg struts, a tail
    spine and roots inside the deck; v3 widened the six feathers (0.5-0.6 chord, 6-34 degree
    sweep) so they overlap into one wing; v4 turned thin V chevrons into thick crescent bands.
    Each step was one look at the sheet and three changes.

13. **The wing helper's spanwise sign.** `fwd.cross(up)` with forward -Y and up +Z is -X, so
    `side=+1` ran the wing INTO the hull (Dolphin v1 span 2.06 instead of 2.64). It is now
    `up.cross(fwd)`; part_map's bounds caught it.

14. **Verify catches the boring faults, the sheet catches the real ones.** The gate found an
    unpainted Window slot, a 1.7% Mass morph, a wrong fuselage axis (-9 units) and a
    cm-vs-metre export before any render; none of those is visible on a sheet. The sheet found
    floating parts, fishbone tails and a blob of a Mass morph; none of those fails a gate.

15. **The probe's take reader must follow OP links.** AnimationCurves connect to their
    AnimationCurveNode by an OP connection ("d|X"), not OO; the first version saw "curves on
    43 nodes, 0 move" on every Squirrel pose. A single-key take is a pose: report its offset
    from the node's REST, not its range.

16. **Unity's hull-fusion bakes go stale on a new mesh** (`CrystalHullFusionBaker` stores the
    vertex count and a content hash): after writing any of Squirrel/Dolphin in place, the
    editor must re-run `FrogletTools > Vessels > Wiring > Bake Crystal Hull Fusions`. Hand it
    off as a team task; the runtime falls back to a worker-thread solve meanwhile.

17. **Two more file frames, both measured, neither guessable.** The Serpent's file is written
    with IDENTITY axes (GlobalSettings Front -Y, Coord +X), so Blender's importer applies no
    turn and the head sits on **+Y**; `export.PRESETS["zup_identity"]` (axis_forward='Y',
    axis_up='Z') reproduces it, the spec models nose +Y and the sheet renders with `nose=+1`.
    The Manta and Urchin RIGS ship the mesh node at identity with the centimetre data baked
    into the vertices while the armature carries R(-90) S(100) (`yup_baked`:
    `bake_space_transform=True` + `FBX_SCALE_NONE`); the Dolphin and Rhino rigs carry
    R(-90) S(100) on the mesh node too (`yup`). Run `fbx_probe` on the shipped file and read
    the mesh node's TRS before picking; the wrong one fails `verify`'s bounds check by 100x.

18. **`plate()`'s top is one n-gon.** A "paint the tip Domain" predicate painted nothing on
    the Sparrow's fan blades (slot 0: 0 polygons, caught by the gate) because the whole top
    face has one centre. `forms.cut_plane(obj, point, normal)` (bmesh bisect) before any
    region paint or region inset; then the predicate has faces to choose between.

19. **Strip numeric suffixes when mapping slot names.** The Rhino rig's slots are
    `accent.005 / BASE.005 / windsheild.005 / N.005`; the render came out colourless until
    `materials.canonical_slot` dropped the `.NNN`. The FILE keeps the suffixed names
    (the `.meta` remaps by them); only the studio-colour lookup canonicalises.

20. **A green morph gate is not a morph.** The Urchin's first Charge shape measured 18% travel
    and passed, because the radial mask was inverted and it scaled the whole ball: a Mass
    morph wearing Charge's name. Masks that select "the spines" are written against the
    spines' own geometry (`|co| > R*1.03 and co.y < 0.9R`, which also excludes the exhaust),
    and every morph tile is LOOKED at: "can a pilot name this element from the chase tile?".

21. **Skin to the bone that the part map says carries the vertices.** The Sparrow's fan
    blades skin to the hinge bones `b_SpinWing.{L,R}.00k`; `b_WingPiece*` are the leaf tip
    bones and skin nothing. `part_map.py` prints bone -> vertex counts; name parts after the
    skinning bone, not after the bone whose name sounds like the part.

22. **Every slot must own polygons, and verify says which.** The Manta spec's `N` (engine)
    slot had no faces until three stern nozzles were added and painted; the Serpent's Mass
    morph measured 1.7% until `scale_about` took the carapace and the engine block together.
    Both failed the gate before any render, both were one-line fixes in the spec.

23. **Seven rigs reused, seven exact.** `rest_compare.py` reports 0.000 units / 0.00 degrees
    on every bone of all seven shipped armatures after the rebuild (Squirrel 42 bones, Dolphin
    28, Sparrow, Serpent, Manta, Urchin 38, Rhino), including the artists' scars (the
    Squirrel's R(71.9,-180,0), the Sparrow's 0.2034 scale). Reusing the imported armature
    object is the whole trick; building a "matching" armature from numbers never was.

24. **Two green gates and nothing written.** The first in-place run resolved the repo root one
    `dirname` short, so `build.py <spec>` wrote seven verified models into
    `Tools/Assets/_Models/...`; `--check` then compared each fresh build against the file it
    had just written (green) and `rest_compare` compared the untouched live file against
    `git show HEAD:` of itself (green). Caught by `git status` showing no `M` on the models.
    The driver now prints the absolute path it writes; a session proves a write with
    `git status --short "Assets/_Models/Vessel Models/"` and never with a gate's green alone.

25. **Budget by vessel, not by rule.** Rounds to beat the shipped sheet: Squirrel 5, Manta 3,
    Urchin 3, Dolphin / Sparrow / Serpent / Rhino 2. The first look always finds the three
    big faults (floating parts, a wrong spanwise sign, a fishbone tail); the second confirms
    the fix and finds one more; a third is only needed when a morph or a mask was wrong.

26. **Blender's own bmesh ops are not deterministic; the gate needs them to be.** `build.py
    urchin --check` reported drift the spec did not contain: 3850 vertices one run, 3852 the
    next, one face swapping slots, bounds 3 mm apart. Measured step by step: the sphere is
    identical every run, but `bmesh.ops.create_uvsphere` emits its FACES in a different order
    each process, and `bmesh.ops.inset_region` (pointer-keyed hashes inside) then creates its
    ring vertices in a different order AND at different positions (median 6 mm, max 0.5 units
    on a 0.85-unit hull); a later predicate at `0.26 < |y|` met faces whose centres sat at
    0.2600037 and took them or not. Three fixes, all kept: `forms._inset_region` is our own
    inset (boundary walk in mesh order, even offset from the averaged in-plane normals, rim
    quads, depth push), so positions no longer depend on traversal order; every `paint` /
    `inset_faces` predicate goes through `forms._stable_faces`, which nudges each centre and
    normal by 1e-5 and raises `UnstableSelection` naming the face when the membership flips
    (the Urchin's bands moved to 0.1003 / 0.2585, the Squirrel's visor and aft frames, the
    Sparrow's fin paint); and `bm.normal_update()` after every `bm.from_mesh` before a normal
    is read (`inset_faces`, `box_uv`). Vertex ORDER still varies between runs (harmless to
    Unity and to the gate, which compares counts, bounds, slots and travel); positions do not.
    Determinism is a gate's precondition: `--check` can only mean "the spec drifted" if the
    build cannot.

27. **The shipped files are a work in progress, not the standard** (Garrett, 2026-10-17: "not
    overindexing on the flaws in the various vessel implementations ... building a framework
    that meets our intentions not mimic halfbaked examples"). The first draft of this skill
    had promoted every measured quirk to a rule: four export presets because four artists
    exported four ways, three slot orders because three files differ, 30-degree poses because
    one controller halves them. The convention (`FLEET_CONVENTION.md`) now states the ONE
    intended frame, order, naming and sign convention; the per-file quirks are a legacy table
    with a migration step each (in-place overwrite today, rig swap / prefab re-fit / driver fix
    later), and a spec declares them as bindings, not as design.
