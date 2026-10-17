# The pipeline: `Tools/Build/vessel_model/`

All of it runs with the plain `python3` that has **bpy 5.2** installed (`python3 -c "import bpy"`);
no Blender GUI, no OpenGL. If `import bpy` fails: `pip download bpy` then
`pip install bpy-*.whl --break-system-packages` (a 400 MB wheel; Blender-as-a-module). `assimp`
(`apt-get install assimp-utils`) is the second reader; the third is Blender's own importer.

| module | what it is | the one thing to remember |
|---|---|---|
| `fbx_probe.py <fbx> [--takes] [--json]` | reader: GlobalSettings, hierarchy with rest TRS, meshes with slot polygon counts, shapes with travel fraction, takes with which nodes move (pose vs motion), blend-shape curve count | curves hang off curve nodes by OP links; a 1-key take is a POSE and its deflection is `offset` vs rest, not `range` |
| `part_map.py <fbx>` | reader: every mesh / vertex group / bone in Blender world space after import, whole-model bounds, widest station, which end is the nose | this prints the modelling frame and the targets; copy its numbers into the spec |
| `rest_compare.py A B` | reader: Blender imports both, compares every node's and bone's world rest pose; exit 1 on a move or a lost bone | run it on every build of a reused rig; trust Blender's reader, not hand-rolled FBX matrix math (it was 10.8 degrees wrong on the Squirrel's armature) |
| `forms.py` | the modelling library: `fuselage`, `loft`, `spine_loft`, `lathe`, `wing`, `plate`, `tube_along`, `cone`, `box`, `sphere`; outlines `superellipse`, `airfoil`, `feather`, `chevron`, `crescent`, `blade`, `rounded_rect`; finishing `bevel`, `subsurf`, `solidify`, `mirror`, `inset_faces`, `sharpen_by_angle`, `apply_modifiers`; painting `paint`, `ensure_slots`; assembly `join`, `mirror_part`, `tag_group`, `bounds`, `tri_count` | every part is an object until `join`; `join` leaves a vertex group per part, which is the skin AND the morph mask; `wing(side=+1)` runs to +X when forward=-Y |
| `rig.py` | `BoneSpec` + `build_armature` for new rigs; `import_shipped_armature(fbx)` to reuse an artist's; `skin_rigid(mesh, arm, group_to_bone=, default_bone=)`; `blend_root` for organic joints | mesh and armature stay SIBLINGS (`parent_to_armature=False`), like the shipped files; every vertex ends in some bone (orphans -> default_bone) |
| `morphs.py` | `MaskSet(mesh).mask(*groups)`, deformers `inflate`, `scale_about`, `translate`, `stretch_along(ramp=)`, `sweep`, `taper`; masks `falloff_mask`, `radial_mask`; `add_shape(mesh, name, [deformers])` -> travel | bake AFTER every modifier is applied and the parts are joined; shape key order = call order |
| `takes.py` | `pose_take(arm, name, {bone: (rx,ry,rz) | {"rot","loc"}}, world=True)`, `motion_take`, `standard_pose_set(arm, builder, prefix=)`, `clear_pose` | world=True conjugates each rotation into the bone's rest frame, so (-20,0,0) is "pitch 20 about world X" on every rig |
| `materials.py` | slot names per vessel, studio colours, `assign_studio_materials`, `slot_report` | aliases map the old names (accent=Domain, N=Engine) for renders only; the FILE keeps the shipped names |
| `export.py` | `PRESETS` (zup / zup_identity / yup / yup_baked), `export_fbx(path, objects, preset)`, `strip_shape_curves(path)`, `verify(path, spec)`, `export_and_verify` | clears every armature's pose and unassigns the action before export (the dirty-pose leak); writes with `bake_anim_use_all_actions` (one stack per action); strips the `Key|...` blend-shape curves with the repo codec; verifies with three readers |
| `render.py` | `contact_sheet(fbx, out_dir, tag=, views=, morphs=, poses=)` -> one PNG: 4 views, 4 morph extremes, 4 poses; Cycles CPU ~10 s a frame | renders the EXPORTED file re-imported, with studio clay per slot; the `chase` view is the one that matters |
| `build.py <spec> [--out] [--sheet-dir] [--no-render] [--check]` | the driver; `--check` rebuilds to a temp file and diffs its structure against the committed `OUTPUT` | the spec owns every number; the driver owns the order and the gate |
| `specs/_common.py` | matrix one-liners, `flip` (mirrored winding), `retag`, `box_uv`, `finish` (bevel+apply+sharpen) | import it in every spec |
| `specs/_wedge.py` | the smoke test and the template for a NEW rig | `build.py _wedge --out /tmp/x.fbx` in under a minute proves the install |

## The verify gate (what `export.verify` refuses)

- a bone / armature / mesh node named by the spec that the file lacks
- more than one mesh, an unskinned mesh, a slot with no polygons, a missing material name
- a shape missing or travelling under `min_travel` (default 2%; specs set 2.5-4%)
- a missing take, a pose take that deflects nothing, ANY blend-shape curve in a take
- GlobalSettings that do not match the preset (unit, up, front, coord axes and signs)
- no UV layer, no normals layer
- assimp unable to read it; the Blender re-import missing a bone / shape / action / slot order
- the re-import's world bounds not matching the built mesh's Blender bounds (5%)

`rest_compare.py` is deliberately separate (it needs the shipped file): run it by hand on every
rig-reusing build. `build.py --check` is the drift gate for a committed spec.

Neither gate proves a WRITE. After `build.py <spec>` (no `--out` = in place), prove it with
`git status --short "Assets/_Models/Vessel Models/"`: the first in-place run wrote seven green
models under `Tools/Assets/...` and both gates stayed green (LESSONS 24). The driver prints
`writing IN PLACE over the committed model: <absolute path>`; read that line.

The whole in-place sequence for one vessel, as a session runs it:

```
git show "HEAD:<OUTPUT>" > <scratch>/shipped/<vessel>.fbx          # the artist file, for rest_compare
python3 Tools/Build/vessel_model/build.py <spec> --no-render        # writes OUTPUT in place
python3 Tools/Build/vessel_model/rest_compare.py <scratch>/shipped/<vessel>.fbx "<OUTPUT>"
python3 Tools/Build/vessel_model/build.py <spec> --check            # the spec rebuilds to what is on disk
python3 Tools/Build/measure_vessel_models.py --no-refs --only <file stem>
git status --short "Assets/_Models/Vessel Models/"                  # the M that proves the write
```

## Scratch layout a session should use

```
<scratchpad>/<vessel>/v1.fbx  v1/<vessel>.png   # iterate here, never in Assets/
<scratchpad>/ref_sheets/<vessel>.png            # the shipped file's sheet = the bar
Assets/_Models/Vessel Models/<live file>.fbx    # written only by the final build, in place
```
