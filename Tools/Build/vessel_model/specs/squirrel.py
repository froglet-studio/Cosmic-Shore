"""
squirrel - the artist's hull, kept, with the framework on top (the artist-hull path, LESSONS 38).

The first rebuild redrew this vessel from primitives and the owner judged it a cheap remake
(2026-10-18). This spec keeps the artist's geometry, slots and colours EXACTLY as the game
draws them and adds what the file lacked:
  - the parked pose the player sees is baked into the rest (mesh and bones), so the rest IS the
    drawn hull and every take is authored against what the player sees;
  - a tail SPINE in ten segments, each weighted between the bones at its ends, so the nine
    floating ribs hang on something and the spine bends with them in every take;
  - the artist's nine takes, retargeted onto the baked rest bone by bone (takes.retarget_from),
    so every part moves exactly as the artist's does and take_parity proves it;
  - Mass strengthened from 2.46% of the diagonal (the fleet floor; invisible from the chase
    camera) to above 4%, on the hull and pods; Time / Space / Charge kept as the artist made them.
LEGACY BINDINGS (file path, node / geometry / armature / bone names, slot order, take names,
frame) are the artist's and are untouched: the prefab, its .meta and the Animator bind them.
  file      Assets/_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx (in place, same guid)
  takes     a_Squirrel_Armature|squirrelanim_{idle,pitchup,pitchdown,yawleft,yawright,rollleft,
            rollright,speedup,speeddown}  (single-frame POSES the controller's 2D blend trees mix)
  slots     0 Domain, 1 Body, 2 Window, 3 Engine (the artist's order and colours)
  frame     preset "zup": nose -Y, up +Z, .L on +X in the Blender frame (the importer's frame)
"""

import math

NAME = "Squirrel"
OUTPUT = "Assets/_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx"

ARMATURE = "a_Squirrel_Armature"
MESH_NODE = "a_SquirrelShipMesh_nearfinal"
GEOMETRY = "Plane.011"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3
TAKE_PREFIX = "squirrelanim_"

SIDES = (("L", 1), ("R", -1))  # the artist's .L is +X in Blender
CHEVRONS = ["bbone_Tail1First", "bbone_Tail2", "bbone_Tail3", "bbone_Tail4", "bbone_Tail5",
            "bbone_Tail6", "bbone_Tail7", "bbone_Tail8", "bbone_Tail9Last"]


def build(ctx):
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    from mathutils import Vector

    # ------------------------------------------------------------------ THE ARTIST'S HULL, AS DRAWN
    arm, mesh = rig.import_shipped_model(ctx.artist(OUTPUT))
    assert arm.name == ARMATURE, arm.name
    bone_names = [b.name for b in arm.data.bones]
    baked = rig.bake_parked_pose(mesh, arm, deform_mesh=True)  # the artist parks 27 bones ~11 degrees: the drawn hull becomes the rest
    ctx.log("artist hull: %d verts, %d tris, %d bones; parked pose baked on %d bone(s)" % (
        len(mesh.data.vertices), f.tri_count(mesh), len(bone_names), len(baked)))

    def centroid(group):
        vg = mesh.vertex_groups[group]
        pts = [mesh.matrix_world @ v.co for v in mesh.data.vertices if any(g.group == vg.index and g.weight > 0.5 for g in v.groups)]
        return sum(pts, Vector()) / len(pts)

    # ------------------------------------------------------------------ THE SPINE the chevrons hang on
    # the artist's nine chevrons float (dossier 9, "chips in orbit"); a tapered boom runs from inside the
    # body through every chevron's centre to just past the last. Each segment is WEIGHTED between the two
    # bones at its ends by how far along it a vertex sits, so it stretches and bends between chevrons
    # whatever the takes do to them (a segment rigidly bound to one chevron detached from the next one)
    cents = [centroid(b) for b in CHEVRONS]
    TAIL_ROOT = Vector((0.0, cents[0].y - 0.40, cents[0].z + 0.01))
    # the body bone the root sits in: the dominant group of the artist vertex nearest the root
    near = min(mesh.data.vertices, key=lambda v: ((mesh.matrix_world @ v.co) - TAIL_ROOT).length)
    root_bone = mesh.vertex_groups[max(near.groups, key=lambda g: g.weight).group].name
    tip = cents[-1] + (cents[-1] - cents[-2]).normalized() * 0.20
    spine_pts = [TAIL_ROOT] + [Vector((0.0, c.y, c.z)) for c in cents] + [Vector((0.0, tip.y, tip.z))]
    spine_bones = [root_bone] + CHEVRONS + [CHEVRONS[-1]]
    spine_r = [0.17, 0.155, 0.14, 0.125, 0.11, 0.10, 0.09, 0.08, 0.065, 0.05, 0.035]
    segs = []
    for k in range(len(spine_pts) - 1):
        seg = f.tube_along("spine_%d" % k, [tuple(spine_pts[k]), tuple(spine_pts[k + 1])], [spine_r[k], spine_r[k + 1]], segments=12)
        f.paint(seg, H)
        f.paint(seg, D, lambda c, n, i: n.z > 0.7)   # a domain stripe along the top, like the chevrons' tops
        f.bevel(seg, width=0.008, segments=1, angle_deg=30)
        f.apply_modifiers(seg)
        segs.append(seg)
    mesh = f.join([mesh] + segs, MESH_NODE, SLOTS)   # the artist's mesh is active: its shape keys and groups survive the join
    mesh.data.name = GEOMETRY
    stray = mesh.vertex_groups.get(MESH_NODE)        # join tags the artist mesh with a group of its own name: not a bone
    if stray:
        mesh.vertex_groups.remove(stray)
    for k in range(len(spine_pts) - 1):
        sg = mesh.vertex_groups["spine_%d" % k]
        a, b = spine_pts[k], spine_pts[k + 1]
        ga = mesh.vertex_groups.get(spine_bones[k]) or mesh.vertex_groups.new(name=spine_bones[k])
        gb = mesh.vertex_groups.get(spine_bones[k + 1]) or mesh.vertex_groups.new(name=spine_bones[k + 1])
        ab = b - a
        for v in mesh.data.vertices:
            if any(g.group == sg.index for g in v.groups):
                t = max(0.0, min(1.0, ((mesh.matrix_world @ v.co) - a).dot(ab) / ab.length_squared))
                if ga.name == gb.name:
                    ga.add([v.index], 1.0, "REPLACE")
                else:
                    ga.add([v.index], 1.0 - t, "REPLACE")
                    gb.add([v.index], t, "REPLACE")
        mesh.vertex_groups.remove(sg)
    ctx.log("spine: %d segments from %s to the tail tip, weighted between their end bones; hull now %d verts, %d tris" % (
        len(segs), root_bone, len(mesh.data.vertices), f.tri_count(mesh)))
    bones = arm.data.bones

    # ------------------------------------------------------------------ MORPHS: the artist's four, Mass strengthened
    ms = morphs.MaskSet(mesh)
    hull = ms.mask("abone_MAIN_Cockpit", "bbone_LowerBody", "bbone_BodyWing", "bbone_UpperHelmet", "bbone_LowerHelmet",
                   "bbone_cheek.L", "bbone_cheek.R")
    pods = ms.mask(*[b.name for b in bones if "Engine" in b.name])
    kb, mx, frac, moved = morphs.augment_shape(mesh, "Mass", [morphs.inflate(0.30, hull), morphs.inflate(0.14, pods)])
    ctx.log("shape Mass strengthened: travel %.3f (%.1f%% of diag), %d verts" % (mx, 100 * frac, moved))
    ctx.log("shapes Time / Space / Charge: the artist's, kept")

    # ------------------------------------------------------------------ TAKES: the artist's own, retargeted
    # Re-authoring them lost the rear-engine motion and turned the roll into a panel twist (Garrett,
    # 2026-10-18); the artist's takes are what the Animator's blend trees were tuned with. They are
    # solved per bone from the artist file's posed matrices, so every bone lands where the artist's
    # lands with the parked pose baked into our rest, and the spine, weighted between the chevrons,
    # bends with whatever the chevrons do. take_parity proves it part by part on the written file.
    acts = takes.retarget_from(arm, ctx.artist(OUTPUT))
    ctx.log("takes: %d retargeted from the artist file (%s)" % (len(acts), ", ".join(sorted(acts))))

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "zup", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [ARMATURE + "|" + TAKE_PREFIX + p for p in takes.STANDARD_POSES],
            "pose_takes": [ARMATURE + "|" + TAKE_PREFIX + p for p in takes.STANDARD_POSES],
            # every part must move in every take as the artist's part does (take_parity, LESSONS 41); the
            # spine is new (no artist part on its blend), the Mass key is strengthened on purpose
            "take_parity": True,
            "shapes": ["Time", "Mass", "Space", "Charge"],
            "slots": list(SLOTS),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.025,
        },
    }
