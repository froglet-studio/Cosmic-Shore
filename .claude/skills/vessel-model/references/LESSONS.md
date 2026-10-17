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

8. **Reuse the shipped armature, bake its parked pose into the rest, and prove the node chain
   with assimp, never with a hand-rolled walk or with Blender alone.** Importing the artist's
   FBX, dropping its mesh and actions, skinning the new mesh to the imported armature and
   exporting with the matching preset reproduced every bone's BIND pose to 0.000 on all seven,
   and `rest_compare.py` (Blender's reader) said so. It was the wrong question. Blender derives
   a bone's rest from the skin's bind matrix and parks the difference to the file's node
   transform as a POSE (`pose_bone.matrix_basis`); the artist Squirrel parks 10.78 degrees on
   27 of 42 bones (tail bones 0.3-0.5 units, bind-to-posed vertex displacement 0.56), the
   Urchin rig a 1.75 scale and 20 degrees on 16 of 38, the Rhino rig 70 degrees and 3.3 m on 9
   of 12; the Dolphin, Sparrow, Serpent and Manta park nothing. Unity builds the bone
   Transforms from the NODE transforms, so the first written Squirrel moved 42 of 45 nodes in
   Unity (assimp composes the Lcl chain the way an engine does: `rest_compare.py` now runs it
   as a second reader, and its Blender reader compares bones AS DRAWN, i.e. posed) while the
   bind-only reader said 0.000. The fix is `rig.bake_parked_pose` (run by `skin_rigid`): apply
   the parked pose as the new rest WITHOUT moving the mesh, because a spec is modelled against
   the hull as drawn (the sheets render the posed file, `part_map` prints posed bounds), so the
   geometry already sits where the posed bones put it; the first bake also deformed the mesh
   and raised the Squirrel's fans 45 degrees (the pose applied twice). After the bake the
   written node chain equals the artist's (assimp 0 of 45) and the bind equals the node chain.
   The first "refutation" of this finding compared the written
   file with ITSELF (the scratch "shipped" copies had been re-extracted from a HEAD that already
   held the rebuild - lesson 29); a hand-rolled `T*R*S` walk had earlier found the same 10.8
   degrees and been dismissed as wrong. Every gate now reads the ARTIST file from a pinned git
   ref (`build.ARTIST_REF`, `ctx.artist(OUTPUT)`), never the file on disk.

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

21. **Skin to the bone that carries the vertices, and READ the probe, do not remember it.** The
    artist Sparrow skins each fan blade to the LEAF bone under its spoke (`b_WingPiece8Last.L`
    under `b_SpinWing.L.001` ... `b_WingPiece1First.L` under `.008`, 100-161 vertices each) and
    the spokes skin nothing. The first spec had it backwards (blades on the spokes, 0 vertices on
    all 16 WingPiece bones), and this lesson said the opposite of the data; Unity's
    `optimizeBones` strips a bone that skins nothing and has no skinning descendant, which would
    have left 16 dangling clip bindings. `fbx_probe` prints `skins N verts` per bone: the part
    takes the name of the bone with the vertices.

22. **Every slot must own polygons, and verify says which.** The Manta spec's `N` (engine)
    slot had no faces until three stern nozzles were added and painted; the Serpent's Mass
    morph measured 1.7% until `scale_about` took the carapace and the engine block together.
    Both failed the gate before any render, both were one-line fixes in the spec.

23. **Seven rigs reused; "exact" means both readers against the ARTIST file.** Reusing the
    imported armature object (with its parked pose baked into the rest, lesson 8) keeps the
    artists' scars the prefabs depend on (the Squirrel armature's R(71.9,-180,0), the Sparrow
    armature's 0.2034 scale). The Sparrow's MESH node scale (0.2034, vertices pre-scaled) is
    deliberately written as 1.0 with the vertices in metres: nothing is authored under that
    node (dossier 6), and a scaled renderer node is a quirk, not a binding.

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

28. **The gates were weaker than their green.** An adversarial review of the first commit
    found, and three readers confirmed or refuted: (a) `export_and_verify` wrote the live file
    FIRST and verified second, so a red verify left an unverified model over the asset; it now
    writes `<file>.verifying.fbx`, verifies, and `os.replace`s only on green. (b) The round-trip
    bounds check compared the file to the build that produced it (Blender undoing Blender), so
    any self-consistent hull of any size or mirror passed and `yup` could not be told from
    `yup_baked`; verify now asserts the mesh node's (and the cm rigs' armature's) Lcl TRS per
    preset, the extents against the replaced file (10%, `size_ref` / `size_tol` to override),
    left/right symmetry (`symmetric=False` to waive: the Sparrow's artist rest is asymmetric)
    and the slot order against the replaced file (`slot_reorder_from` declares a migration: the
    Urchin rig now has the domain band at slot 0). (c) `build.py --check` compared names only;
    its summary now carries every node's TRS, the skin counts, each take's deflection per node
    and the six axis settings. (d) The only take check was "something moves"; every pose and
    motion take now records the bones it authored (`takes.AUTHORED`) and verify requires the
    file to move exactly those (an idle authored at rest must move nothing). (e) CONFIRMED, after a
    wrong refutation: "the written files move every Unity bone by 10.78 degrees" - lesson 8.
    (f) Every exported material was default grey (0.8, 0.8, 0.8): Unity generates the model's
    materials from the file and the .meta remaps only Body / Domain / Window, so the Engine
    slots would have imported grey; `forms.ensure_slots` now writes the artist file's colour
    per material name (captured by `import_shipped_armature`) and verify compares DiffuseColor
    per slot against the artist file. (g) The slot gate accepted a 4-polygon Window; it is now
    0.5% of the faces (`min_slot_share`), and the Sparrow and Dolphin canopies were cut and
    enlarged to pass it. (h) The Rhino and Urchin rigs' take names were changed to the standard
    nine (nothing binds the old `rhino_*` / `urchan_*` names; recorded in CONTRACT 5). A finding
    with a number in it is checked by a reader that did not produce the number - and against
    the file the number was measured on.

29. **A scratch copy named "shipped" is not the shipped file after the first commit.** The gate
    scripts extracted `git show HEAD:<file>` into `scratch/shipped/` on every run; once the
    rebuilt models were committed, HEAD held the rebuild, so `rest_compare` compared the rebuild
    with itself, the parked-pose probe measured our own file (0 parked bones) and an adversarial
    finding was "refuted" against the wrong file. The artist files now come from a pinned ref
    (`build.ARTIST_REF = "b7f0fbbf3"`, cached under `Tools/Build/vessel_model/.artist_cache/`),
    every spec reuses its armature from there, and every gate compares against it. Name a
    reference by the commit it comes from, never by the branch it was taken from.

30. **The Sparrow flew backwards, and the first rebuild copied it.** The dossier measured it
    (section 1.4: Unity z = Blender y for this file; the jet-hosting `b_Tail*` blades land at
    z +4.6..+7.6, the nose end as flown; the cockpit at z -0.7; the gun muzzles at +1.3 firing
    forward): the artist built the beak at -Y and the engines at +Y, so the shipped Sparrow
    leads with its exhaust and the first spec, modelled "where the shipped blades are", did
    the same - a WIP flaw promoted to a binding. The rebuilt Sparrow is nose-first: beak and
    canopy at +Y, engines at -Y, and the six `b_Tail` bones moved to the engines with
    `rig.move_bones` (orientation kept, so the prefab's jets keep their authored rotation);
    guns, bays, shells and the fan were already placed for a +Y nose. Poses are authored in the
    fleet's nose -Y vocabulary and mirrored through y = 0 (`mirror_y` in the spec). The editor
    confirms the sign in flight (checklist); `rest_compare --allow-moved` names the six bones.
    A slot gate on polygon COUNT then refused a canopy of 18 wide quads: the share is now
    measured by AREA on the re-import (`material_area_share`), which is what the eye sees.

31. **A connected bone ignores pose location.** The Rhino's `jet.l/.r` are children connected
    to `wing1` (head pinned to the parent's tail), so a `speedup` pose that slides the pods aft
    wrote no translation at all and verify's authored-takes check caught it ("the file does not
    move jet.l"). FBX has no notion of connection and the heads and tails stay put, so
    `import_shipped_armature` now disconnects every bone. The same check is why the Urchin's
    scale normalisation and the Sparrow's moved jet bones are DECLARED to the gate
    (`--allow-scale-only`, `--allow-moved`) instead of silently passing or failing.

32. **A gate that is dead reads exactly like a gate that passes.** `export.py` used `json` in
    the artist-reference block without importing it; the `NameError` was swallowed by a broad
    `except Exception` and the size, slot-order and colour checks were OFF for every spec while
    all seven builds printed green (the review's skeptic found it by asking what failure each
    gate had ever produced). Two defects shipped under it: every file carried grey
    `DiffuseColor` (0.8), and the Sparrow and Dolphin were smaller than the artist's envelope.
    The `except` now tolerates only a MISSING artist file and lets a reader bug raise. Before
    trusting a green gate, name a failure it produced (CLAUDE.md's rule for `--check`).

33. **The artist files import with every morph at 100%.** The Manta and Dolphin FBX store
    `DeformPercent 100` as each blend-shape channel's STATIC value, so Blender's importer sets
    all four shape keys to 1.0 and an "as drawn" bounds reader measured the hull inflated by
    Mass + Charge + Space + Time at once: the Manta read 5.52 wide (really 3.95) and the Dolphin
    8.2 (really 5.29), and a pass "corrected" the Dolphin's wing span to 3.80 to meet it. The
    reader (`_POSED_BOUNDS_READER`) now zeroes every shape key before evaluating; the drawn
    size is the REST mesh under the parked pose and nothing else. Corollary: when a reference
    disagrees with the dossier's Unity-measured number by a round factor, suspect the reader.

34. **A right turn is a negative rotation about up, whatever the heading.** Yaw is chiral: the
    whole-body imitation that every artist take uses (the Squirrel's tail swings to the outside
    of the turn, the Dolphin's jaws nose to the pilot's right, the Rhino's +X wing swings
    forward) is `-Z` for yawright on every hull, nose -Y or +Y. Four specs had `+Z` and turned
    left, and the Sparrow's mirror through `y = 0` (which flips X and Z rotations) turned its
    correctly-authored tails the wrong way: a mirrored right turn IS a left turn. Pitch and the
    speed folds survive a mirror; yaw and roll do not. The Sparrow's poses are now written in
    the file's own frame, and `pose_expect` in every spec makes the re-import prove the
    direction of each take (a named part must move the named way by at least so much), which
    is the only check that tells an inverted pose from a correct one.

35. **An imported action poses the armature; a parked pose is only visible with `use_anim=False`.**
    A reader that imports a file WITH its animations gets the last action's frame as the pose,
    so the artist Urchin's 1.75 hold scale (the parked pose Unity draws) read as scale 1.0 and
    our Manta read as if its wing were parked 40 degrees ("cut", the last take). Measure rest and
    park with `use_anim=False`; measure takes with the action assigned and the park cleared.
    Verify now also refuses a rebuilt file whose bind differs from its node chain (a residual
    park; `allow_park` declares one on purpose), since the convention bakes the park into the
    rest so Unity and Blender draw the same hull.

36. **The Urchin is drawn 1.75x out.** The artist rig scales every hold bone 1.75 while the mesh
    was bound at scale 1, so Unity draws the guns, pads, jets and shrouds 1.75x bigger and 1.75x
    further from the ball (the live prefab authors the same 1.75 on its jet and gun nodes - two
    independent authorings of one number is an intention, not a flaw). The first rebuild modelled
    the raw layout against baked bones: a 0.91 envelope where 1.54 is drawn, with every jet 0.2
    from its own bone. The spec now scales the appendages about the origin after modelling them.

37. **Bake a parked pose only when the game draws it.** The Rhino rig's node chain parks its wings
    and jets 70 degrees up from the bind: drawn by that chain the hull is 25 wide and 20 tall with
    the wings standing, a pose nobody has seen in the game (the live placeholder prefab and the
    rig's bind agree on a 16 x 15 x 20 hull). The first rebuild baked it (rest := drawn) and then
    modelled the wings at the bind layout, so each wing sat metres from its own bone and every
    pose swung it about a distant pivot; the same shape the Urchin had with its appendages. The
    Squirrel's park is baked because the live prefab plays it; the Urchin's 1.75 because the live
    prefab authors it; the Rhino keeps the BIND as its rest (`skin_rigid(..., bake_park=False)`),
    declares the twelve moved bones (root and fuse included: the drawn chain also pitches the whole rig 16.9 degrees) to `rest_compare --allow-moved` and the bind envelope as
    `size_ref_blender`. The rule: a mesh part sits ON its bone in whatever rest the file ships;
    `part_map.py` shows both, and a part centred away from its bone's head-to-tail is the
    Urchin / Rhino mistake again.
