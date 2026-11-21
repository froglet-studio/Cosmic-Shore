"""
manta - the artist's hull, rebuilt through the passing loop (SKILL.md section 4), the way the
Squirrel (passed in the owner's Editor) and the Dolphin were.

Kept, and proven kept by the gates on every build:
  - the mesh, its rig and every bone rest (`rest_compare.py`); any parked pose is baked into the rest;
  - the four material slots, their order and every material value (`take_parity.compare_materials`);
  - the four element morphs (`compare_shapes`, every part);
  - every take in the file, retargeted bone by bone (`compare`, every part of every take; and
    `compare_moving_nodes`, the moving-bone set per take read straight from the FBX).
Added: nothing visible. This round proves the loop reproduces the Manta exactly; the dossier's weak
points (`Docs/VesselModels/Manta.md` section 8) are the owner's to pick, one declared change at a time.
What the file gains is ownership: it is rebuilt from this spec, every property above is gated, and
`build.py manta --check` catches drift.
LEGACY BINDINGS (file path, node / geometry / armature / bone names, slot names and order, take names,
frame) are the artist's and untouched.
  file      Assets/_Models/Vessel Models/Manta_shapekey_rigged.fbx (in place, same guid)
  frame     preset "yup_baked": Y-up cm file, the mesh node identity with its data baked
"""

NAME = "Manta"
OUTPUT = "Assets/_Models/Vessel Models/Manta_shapekey_rigged.fbx"

ARMATURE = "Armature.002"
MESH_NODE = "manta"
GEOMETRY = "Sphere.018"
SLOTS = ('accent', 'BASE', 'windsheild', 'N')


def build(ctx):
    f, rig, takes = ctx.forms, ctx.rig, ctx.takes

    # ------------------------------------------------------------------ THE ARTIST'S HULL, AS DRAWN
    arm, mesh = rig.import_shipped_model(ctx.artist(OUTPUT))
    assert arm.name == ARMATURE, arm.name
    assert mesh.name == MESH_NODE, mesh.name
    mesh.data.name = GEOMETRY
    assert [m.name for m in mesh.data.materials] == list(SLOTS), [m.name for m in mesh.data.materials]
    bone_names = [b.name for b in arm.data.bones]
    shapes = [k.name for k in mesh.data.shape_keys.key_blocks][1:]
    baked = rig.bake_parked_pose(mesh, arm, deform_mesh=True)
    ctx.log("artist hull: %d verts, %d tris, %d bones, shapes %s; parked pose baked on %d bone(s)" % (
        len(mesh.data.vertices), f.tri_count(mesh), len(bone_names), shapes, len(baked)))

    # ------------------------------------------------------------------ TAKES: the artist's own, retargeted
    acts = takes.retarget_from(arm, ctx.artist(OUTPUT))
    ctx.log("takes: %d retargeted from the artist file (%s)" % (len(acts), ", ".join(sorted(acts))))

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    take_names = [ARMATURE + "|" + a.name for a in acts.values()]
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "yup_baked", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": take_names,
            "pose_takes": take_names,
            "take_parity": True,       # every take, morph and material must match the artist's; nothing is allowed to differ
            "shapes": shapes,
            "slots": list(SLOTS),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.0,         # the artist's morphs are kept as they are (morph parity proves it)
        },
    }
