"""
dolphin - the artist's hull, rebuilt through the passing loop (SKILL.md section 4), as the proof
that the loop reproduces a SECOND vessel the way it reproduced the Squirrel.

Kept, and proven kept by the gates on every build:
  - the mesh, its 28-bone rig and every bone rest (`rest_compare.py` 0.000; the Dolphin parks no pose);
  - the four material slots, their order and every material value (`take_parity.compare_materials`:
    the un-remapped engine slot `N.001` wears the file's own blue emissive in the game);
  - the four element morphs `mass` / `charge` / `space` / `time` (`compare_shapes`, every part);
  - the ten MOTION takes (21 frames at 24 fps), retargeted bone by bone (`compare`, every part of
    every take). No Animator plays them - `RiptideAnimation` puppets the bones by NAME in code -
    but the file keeps them for the swapper and the editor tools.
Added: nothing visible. The Dolphin is the reproduction test; the dossier's weak points (a 10-
polygon window, `mass` widening the wingspan, a `space` keel the chase camera cannot see, six jet
mouths that merge into one sheet) are the owner's to pick, one declared change at a time.
What the file gains is ownership: it is rebuilt from this spec, every property above is gated,
and `build.py dolphin --check` catches drift.
LEGACY BINDINGS (file path, node / geometry / armature / bone names - the misspelt `dolohin`
mesh node included - slot names and order, take names, frame) are the artist's and untouched.
  file      Assets/_Models/Vessel Models/dolphin_shapekey_with_animations.fbx (in place, same guid)
  frame     preset "yup": roots R(-90) S(100), vertices in metres, nose -Y in the Blender frame
"""

NAME = "Dolphin"
OUTPUT = "Assets/_Models/Vessel Models/dolphin_shapekey_with_animations.fbx"

ARMATURE = "dolphin"
MESH_NODE = "dolohin"
GEOMETRY = "Chassis"
SLOTS = ("accent.001", "BASE.001", "windsheild.001", "N.001")


def build(ctx):
    f, rig, takes = ctx.forms, ctx.rig, ctx.takes

    # ------------------------------------------------------------------ THE ARTIST'S HULL, AS DRAWN
    arm, mesh = rig.import_shipped_model(ctx.artist(OUTPUT))
    assert arm.name == ARMATURE, arm.name
    assert mesh.name == MESH_NODE, mesh.name
    mesh.data.name = GEOMETRY
    assert [m.name for m in mesh.data.materials] == list(SLOTS), [m.name for m in mesh.data.materials]
    bone_names = [b.name for b in arm.data.bones]
    baked = rig.bake_parked_pose(mesh, arm, deform_mesh=True)  # a no-op on the Dolphin, kept so the recipe is the same
    ctx.log("artist hull: %d verts, %d tris, %d bones, shapes %s; parked pose baked on %d bone(s)" % (
        len(mesh.data.vertices), f.tri_count(mesh), len(bone_names),
        [k.name for k in mesh.data.shape_keys.key_blocks][1:], len(baked)))

    # ------------------------------------------------------------------ TAKES: the artist's own, retargeted
    acts = takes.retarget_from(arm, ctx.artist(OUTPUT))
    ctx.log("takes: %d retargeted from the artist file" % len(acts))

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "yup", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [ARMATURE + "|" + a.name for a in acts.values()],
            "pose_takes": [],          # MOTION takes, 21 frames each: take_parity checks their last frame part by part
            "take_parity": True,       # every take, morph and material must match the artist's; nothing is allowed to differ
            "shapes": ["mass", "charge", "space", "time"],
            "slots": list(SLOTS),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.03,
        },
    }
