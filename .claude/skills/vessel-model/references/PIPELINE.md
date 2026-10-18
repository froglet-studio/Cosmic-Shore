# The pipeline: `Tools/Build/vessel_model/`

All of it runs with the plain `python3` that has **bpy 5.2** installed (`python3 -c "import bpy"`);
no Blender GUI, no OpenGL. If `import bpy` fails: `pip download bpy` then
`pip install bpy-*.whl --break-system-packages` (a 400 MB wheel; Blender-as-a-module). `assimp`
(`apt-get install assimp-utils`) is the second reader; the third is Blender's own importer.

| module | what it is | the one thing to remember |
|---|---|---|
| `fbx_probe.py <fbx> [--takes] [--json]` | reader: GlobalSettings, hierarchy with rest TRS, meshes with slot polygon counts, shapes with travel fraction, takes with which nodes move (pose vs motion), blend-shape curve count | curves hang off curve nodes by OP links; a 1-key take is a POSE and its deflection is `offset` vs rest, not `range` |
| `part_map.py <fbx>` | reader: every mesh / vertex group / bone in Blender world space after import, whole-model bounds, widest station, which end is the nose | this prints the modelling frame and the targets; copy its numbers into the spec |
| `rest_compare.py A B [--allow-moved a,b]` | reader: Blender imports both and compares every bone AS DRAWN (its posed matrix: the artist files park their node-vs-bind difference as a pose) plus every node's position, rotation and scale; assimp composes both files' node chains the way an engine does and compares again; exit 1 on a move or a lost node from either reader, except names a spec moved on purpose (`rig.move_bones`) | run it against the ARTIST file (`build.artist_path`) on every build of a reused rig; a file whose bones match only in the bind pose fails the assimp reader (LESSONS 8) |
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
- more than one mesh, an unskinned mesh, a slot with no polygons or under 0.5% of the SURFACE
  (area on the re-import; `min_slot_share`, default 0.3%), a missing material name, a slot colour that differs
  from the artist file's (Unity generates the model's materials from the file), a take or
  material the `.meta` pins that the file lacks
- a shape missing or travelling under `min_travel` (default 2%; specs set 2.5-4%)
- a missing take, a pose take that deflects nothing, ANY blend-shape curve in a take
- GlobalSettings that do not match the preset (unit, up, front, coord axes and signs)
- the mesh node's Lcl rotation / scaling (and the cm rigs' armature's) not the preset's: that
  node frame is where the prefab's colliders were authored, and the round-trip bounds check
  cannot see it (Blender undoes its own export)
- extents more than 10% off the file being replaced (`size_ref` from the committed file;
  `size_tol`, or a spec's own `size_ref`, when the change is meant); a hull not left/right
  symmetric (`symmetric=False` waives it for a deliberately asymmetric hull)
- a slot order that differs from the replaced file's without `slot_reorder_from` declaring it
- a take that does not move every bone its pose named (`takes.AUTHORED`, filled by
  `pose_take` / `motion_take`), or an idle authored at rest that moves anything
- no UV layer, no normals layer
- assimp unable to read it; the Blender re-import missing a bone / shape / action / slot order
- the re-import's world bounds not matching the built mesh's Blender bounds (5%)
- a take whose named part does not move the named way by the named minimum (`pose_expect`:
  `{take: [(vertex group, axis, sign, min)]}`, measured on the re-import with the action applied
  as per-group centroid deltas; the only check that catches an inverted pitch or a mirrored yaw)
- a bind pose that differs from the node chain (a residual parked pose; `allow_park` declares one)
- **take parity** (`take_parity.py`, on by default when the artist file is known): any bone-skinned
  part, in any take, whose rigid motion from the drawn rest differs from the artist's part by more
  than max(1.5% of the diagonal, 25% of the artist's travel) in position or max(5 deg, 25%) in
  rotation - a part frozen where the artist's moves, moving where it holds, or turning about the
  wrong axis. `take_parity_allow` = {take or "*": [parts]} declares a meant change. Standalone:
  `python3 Tools/Build/vessel_model/take_parity.py <artist.fbx> <ours.fbx> [--all]`.
- **moving-node parity** (same switch): per take, the set of bones whose FBX curves move differs
  from the artist's - read from the file with no Blender evaluation, so it catches a reader bug
  the Blender-evaluated gate shares with itself (LESSONS 43).
- **morph parity** (same switch): any part of any element morph moving differently at full weight
  (`shape_parity_allow` = {morph: [parts] or ["*"]} declares a strengthened morph).
- **material parity** (same switch): any material property (diffuse, emissive colour and factor,
  transparency, specular) differing from the artist's - an un-remapped slot wears these in the game.

The artist-reference block of `build.py` reads the ARTIST file (`ARTIST_REF`, pinned) with every
shape key at 0 and no action (LESSONS 33, 35) for the size, slot-order and colour references;
only a MISSING artist file is tolerated there, a reader error raises (LESSONS 32).

`rest_compare.py` is deliberately separate (it needs the shipped file): run it by hand on every
rig-reusing build. `build.py --check` is the drift gate for a committed spec.

## The artist-hull path (the default for any vessel that has an artist file)

1. `arm, mesh = rig.import_shipped_model(ctx.artist(OUTPUT))` - the artist's mesh (groups,
   shape keys at 0, slots, colours) and armature; the mesh unparented and its transform applied
   so the node exports at identity like the shipped one; bones disconnected; the park preserved.
2. `rig.bake_parked_pose(mesh, arm, deform_mesh=True)` - the drawn pose becomes the rest for
   mesh, shape keys and bones (rest bounds == the artist's drawn bounds; rest_compare 0.000).
3. Add what the file lacked: parts built against group centroids (`tube_along` segments named
   for the bone they skin to, joined with `forms.join([mesh] + parts, ...)` - the artist's mesh
   first so its keys survive; weight each added part between the bones at its ends so it follows
   any take), `morphs.augment_shape` on a weak morph, `morphs.add_shape` for a missing one.
   **The takes are the artist's**: `takes.retarget_from(arm, ctx.artist(OUTPUT))` keys them bone
   by bone onto the baked rest (names, frames and fps kept). Re-author a take only when the owner
   asks for a different motion, and then list the changed parts in `take_parity_allow`.
4. The usual gates. The spec's docstring names what was added and what was kept.
`specs/squirrel.py` is the worked example; `specs/experimental/*_scratch.py` are the withdrawn
from-scratch builds, kept for their part vocabulary, not loadable by `build.py`.

## The new-hull path (a vessel with NO artist file: the Butterfly, `specs/butterfly.py`)

1. **The brief is the vessel's doc and its prefab, not a picture.** Read what the code binds (bone
   names, hinge positions, which part the puppetry turns), what the camera sees (distance, px per
   unit) and what the tuning was sized to (colliders, skimmers, wake width); put every number in
   the spec's docstring. Keep the replaced hull's hinges and size within a few percent.
2. **Fleet convention for a new vessel** (`FLEET_CONVENTION.md` 1-4): preset `zup`, nodes `mesh` /
   `rig`, root bone `hull`, slots Domain / Body / Window / Engine, shapes Charge / Mass / Space /
   Time at 4-12% travel, a new guid, a `.meta` that remaps all four slots.
3. **Build the parts so the colour follows the anatomy** (radial grids from a wing root, rings
   keyed to where a pattern band sits), with relief IN the surface rather than floating on it.
4. **Measure the look against the fleet**: `style_metrics.py --fleet <ours.fbx>` prints the artist
   hulls' band (smooth / hard edges, slot shares, domain regions and width, density). It is
   INFORMATIVE: the withdrawn from-scratch hulls sat inside the band too, so it cannot pass a hull;
   it only tells you which number to look at on the sheet.
5. **Integrate by NAME** (an FBX's sub-object fileIDs cannot be computed offline): the prefab holds
   the model root (constant fileIDs) and the code resolves bones and the SkinnedMeshRenderer by name
   at runtime; keep the old names resolving too, so the swap is reversible.

Neither gate proves a WRITE. After `build.py <spec>` (no `--out` = in place), prove it with
`git status --short "Assets/_Models/Vessel Models/"`: the first in-place run wrote seven green
models under `Tools/Assets/...` and both gates stayed green (LESSONS 24). The driver prints
`writing IN PLACE over the committed model: <absolute path>`; read that line.

The whole in-place sequence for one vessel, as a session runs it:

```
python3 Tools/Build/vessel_model/build.py <spec> --no-render        # writes OUTPUT in place (temp file, verified, then moved over)
python3 Tools/Build/vessel_model/rest_compare.py "$(python3 -c 'import sys; sys.path.insert(0, "Tools/Build/vessel_model"); import build, <spec>; print(build.artist_path(<spec>.OUTPUT))')" "<OUTPUT>"
python3 Tools/Build/vessel_model/build.py <spec> --check            # the spec rebuilds to what is on disk
python3 Tools/Build/measure_vessel_models.py --no-refs --only <file stem>
git status --short "Assets/_Models/Vessel Models/"                  # the M that proves the write
```

The artist file is `build.artist_path(OUTPUT)`: `git show <build.ARTIST_REF>:<OUTPUT>` cached
under `Tools/Build/vessel_model/.artist_cache/`. NEVER take the reference from HEAD or from
the working tree: after the first in-place write both hold our own rebuild, and a comparison
with it compares the pipeline with itself (LESSONS 29). `build.py` itself reads the artist
file for the size, slot-order and colour references it hands to `verify`.

## Scratch layout a session should use

```
<scratchpad>/<vessel>/v1.fbx  v1/<vessel>.png   # iterate here, never in Assets/
<scratchpad>/ref_sheets/<vessel>.png            # the shipped file's sheet = the bar
Assets/_Models/Vessel Models/<live file>.fbx    # written only by the final build, in place
```
