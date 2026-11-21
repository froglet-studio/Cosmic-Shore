"""
urchin - the artist's RIG, kept, with four real element morphs: the file the Urchin prefab now
DRAWS (the rig swap, 2026-10-18; LESSONS 40: "rebuilt" means the game draws it).

The Urchin prefab drew the part-per-mesh placeholder `Urchan_Test.fbx` (13 MeshFilters puppeted
by `UrchinAnimation`); the artist also made this rigged version of the same artwork, whose four
element shapes shipped EMPTY and which nothing drew. This spec takes the artist-hull path
(PIPELINE.md, LESSONS 38-42) on that rig:

  - the artist's mesh, 38-bone armature, slots, colours, takes and shape-key names, imported
    whole (`rig.import_shipped_model`) and proven kept by every parity gate;
  - THE REST IS THE ARTIST'S OWN FREE-FLIGHT POSE. The rig's node chain parks the last frame of
    `urchan_yawright` (root -9.9 deg, fuse -14.1, every pod hold 10.5, the shrouds 19-41 deg, all
    one-sided: a twisted, asymmetric ship nobody has seen in the game), while NINE of its eleven
    takes start from one pose: the bind with the six pod holds (`winggrowrot*`) at 1.75, which is
    also `urchan_ball`'s last frame and exactly the layout the placeholder prefab draws in free
    flight (its jet and gun nodes at localScale 1.75; measured per part, `Docs/VesselModels/
    Urchin.md` rig-swap note). LESSONS 37: bake a parked pose only when the game draws it. So the
    reference this spec builds on and is gated against is the artist file with its rest set to the
    first key of `urchan_fly` (`rested_reference`: the codec rewrites the Lcl transforms of the
    animated nodes and nothing else - mesh, skin, bind pose, takes and materials are the artist's
    bytes). `bake_parked_pose(deform_mesh=True)` then bakes only that 1.75: the drawn rest.
  - FOUR REAL MORPHS on the artist topology (the shapes keep the file's lowercase names and order
    mass / charge / space / time), each one the element's ability on this hull and readable from
    the chase camera, which sits BEHIND the ball (followOffset (0, 0.83, -6.67)): what reads there
    is the silhouette's width and height, the ball's size and the pods' outline, not anything
    along the line of sight.
      mass   the ball fattens about its centre (Trail Rider: what it rides grows) and the pod shells
             thicken - the one part the pilot sees most gets 23% wider;
      charge the weapon grows: both side-shooter barrels lengthen forward and thicken, and the gear
             rings round them grow and throw their teeth out (Chain Spikes) - the side discs stand taller;
      space  reach: all six pods stand further out along their own radials (Track Projector's
             longer rail) - the silhouette widens, the free-flight push-out continued;
      time   speed: the four engine pods sweep back about the ball and in toward the axis, and
             their jet nozzles stretch aft (Slip) - the silhouette narrows and rakes.
Added geometry: none. LEGACY BINDINGS (file path and guid, node / geometry / armature / bone names,
slot names and order, take names, frame) are the artist's and untouched.
  file      Assets/_Models/Vessel Models/urchan_shapekey_with_animations.fbx (in place, same guid)
  frame     preset "yup_baked": mesh node identity with the cm baked in, armature R(-90) S(100);
            Blender frame nose -Y, up +Z, the artist's .l on +X (the pilot's left in Unity)
  prefab    Urchin.prefab nests this file at localScale 0.474905 (VesselRigSwapper's measured fit:
            the rig is the placeholder at 2.105x); UrchinAnimation drives the placeholder's part
            transforms as before and the pod-hold bones FOLLOW them, so the 12 box colliders, the
            four VesselJets and the muzzles on those transforms stay on the drawn pods.
"""

import math
import os
import sys

NAME = "Urchin"
OUTPUT = "Assets/_Models/Vessel Models/urchan_shapekey_with_animations.fbx"

ARMATURE = "Armature"
MESH_NODE = "Body.001"
GEOMETRY = "Sphere.042"
SLOTS = ("BASE", "accent", "windsheild", "N")   # the artist's order; the prefab paints by identity (accent)
SHAPES = ("mass", "charge", "space", "time")    # the artist's names, case and order

REST_TAKE = "urchan_fly"                         # its first key is the free-flight rest the game draws
HOLDS = ("winggrowrotT.l", "winggrowrotT.r", "winggrowrotM.l", "winggrowrotM.r", "winggrowrotB.l", "winggrowrotB.r")
JET_PODS = {"T.l": ("jetT.l", "wingconrotT.l"), "T.r": ("jetT.r", "wingconrotT.r"),
            "B.l": ("jetB.l", "wingconrotB.l"), "B.r": ("jetB.r", "wingconrotB.r")}
GUN_PODS = {"M.l": ("gunM.l", "sheildconrot.l"), "M.r": ("gunM.r", "sheildconrot.r")}

# morph magnitudes (Blender units = metres in the drawn rest; the drawn diagonal is ~2.3)
MASS_BALL_SCALE = 1.23      # ball radius 0.43 -> 0.53
MASS_SHELL = 0.025          # pod shells thicken along their normals
CHARGE_BARREL = 1.8         # barrel length, from its breech forward
CHARGE_BORE = 1.3           # barrel thickness
CHARGE_RING = 1.3           # the gear ring round the barrel, in its own plane (the part the chase camera sees)
CHARGE_TEETH = 1.15         # and its teeth again, radially, over the outer 40%
SPACE_OUT = 0.14            # every pod along its own radial
TIME_SWEEP_DEG = 12.0       # the engine pods rake back about the ball centre
TIME_NOZZLE = 1.2           # the jet domes stretch aft from their front


def _s(raw):
    return raw.decode("utf-8", "replace") if isinstance(raw, bytes) else str(raw)


def rested_reference(src, take=REST_TAKE):
    """
    The artist file with its REST (every animated node's Lcl Translation / Rotation / Scaling)
    set to the FIRST KEY of `take`, written beside the cached artist file. Pure data surgery with
    the repo codec: the mesh, skin clusters, bind pose, every take and every material stay the
    artist's bytes, so this is the artist's own pose promoted to rest, not a rebuild. Returns
    (path, {node: (prop, old, new)}) for every value that changed.
    """
    here = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    sys.path.insert(0, os.path.dirname(here))
    import fbx_binary as F
    dst = os.path.join(os.path.dirname(src), os.path.splitext(os.path.basename(src))[0] + ".rest_%s.fbx" % take)
    nodes, version, footer = F.read(src)
    top = {n.name: n for n in nodes}
    objs, conns = top["Objects"], top["Connections"]
    by_id = {o.props[0][1]: o for o in objs.children if o.props}
    oo_children, op = {}, []
    for c in conns.children:
        if c.name != "C" or len(c.props) < 3:
            continue
        kind, a, b = _s(c.props[0][1]), c.props[1][1], c.props[2][1]
        if kind == "OO":
            oo_children.setdefault(b, []).append(a)
        elif kind == "OP":
            op.append((a, b, _s(c.props[3][1]) if len(c.props) > 3 else ""))

    def name_of(o):
        return _s(o.props[1][1]).split("\x00")[0]

    stacks = [o for o in objs.children if o.name == "AnimationStack" and name_of(o).split("|")[-1] == take]
    if len(stacks) != 1:
        raise RuntimeError("rested_reference: %d stacks named %r in %s" % (len(stacks), take, src))
    changes = {}
    for lid in oo_children.get(stacks[0].props[0][1], []):
        layer = by_id.get(lid)
        if layer is None or layer.name != "AnimationLayer":
            continue
        for cid in oo_children.get(lid, []):
            cn = by_id.get(cid)
            if cn is None or cn.name != "AnimationCurveNode":
                continue
            vals = [0.0, 0.0, 0.0]
            p70 = cn.first("Properties70")
            for p in (p70.children if p70 else []):
                key = _s(p.props[0][1])
                if key in ("d|X", "d|Y", "d|Z"):
                    vals["XYZ".index(key[-1])] = float(p.props[4][1])
            for s, d, ch in op:
                if d == cid and s in by_id and by_id[s].name == "AnimationCurve" and ch in ("d|X", "d|Y", "d|Z"):
                    kv = by_id[s].first("KeyValueFloat")
                    if kv is not None and kv.props and kv.props[0][1]:
                        vals["XYZ".index(ch[-1])] = float(kv.props[0][1][0])
            for s, d, prop in op:
                if s != cid or prop not in ("Lcl Translation", "Lcl Rotation", "Lcl Scaling"):
                    continue
                model = by_id.get(d)
                if model is None or model.name != "Model":
                    continue
                mp = model.first("Properties70")
                row = next((p for p in mp.children if _s(p.props[0][1]) == prop), None)
                if row is None:
                    unit = 1.0 if prop == "Lcl Scaling" else 0.0
                    row = F.Node("P", [("S", prop.encode()), ("S", prop.encode()), ("S", b""), ("S", b"A+"),
                                       ("D", unit), ("D", unit), ("D", unit)])
                    mp.children.append(row)
                old = [float(row.props[i][1]) for i in (4, 5, 6)]
                row.props[4:7] = [("D", v) for v in vals]
                if max(abs(a - b) for a, b in zip(old, vals)) > 1e-4:
                    changes.setdefault(name_of(model), []).append((prop, [round(v, 4) for v in old], [round(v, 4) for v in vals]))
    if ARMATURE in changes:
        raise RuntimeError("rested_reference: %r's first key moves the armature object itself: %s" % (take, changes[ARMATURE]))
    F.write(dst, nodes, version, footer)
    return dst, changes


def retarget_deformation(takes, arm, ref_path, ref_rest):
    """
    `takes.retarget_from`, solved so every bone's DEFORMATION (posed matrix times the inverse of its
    rest) equals the reference's, not its posed matrix. The two are the same when both rests are
    equal; they differ here because Blender cannot hold a rest SCALE: the reference's pod holds rest
    at 1.75 and their children inherit it, while our baked rest carries the 1.75 in the geometry and
    the bone lengths at scale 1. Matching posed matrices then scales the children a second time (the
    first build: 90 part-takes off, the children of every hold moving where the artist's hold still).
    `ref_rest` = {bone: the reference's armature-space matrix at its drawn rest}, read from the
    imported reference before the bake. Names, frames, fps and the AUTHORED record are kept exactly
    as retarget_from keeps them.
    """
    import bpy
    import take_parity
    from mathutils import Matrix, Vector
    dump = take_parity.retarget_dump(ref_path)
    bpy.context.scene.render.fps = dump["fps"]

    def depth(b):
        d = 0
        while b.parent:
            b, d = b.parent, d + 1
        return d
    order = sorted(arm.pose.bones, key=lambda pb: depth(pb.bone))
    to_ours = {pb.name: ref_rest[pb.name].inverted() @ pb.bone.matrix_local for pb in arm.pose.bones}
    acts = {}
    for take, rec in dump["takes"].items():
        act = bpy.data.actions.new(rec["name"].split("|")[-1])
        takes._assign_action(arm, act)
        prev, moved = {}, set()
        for fr in sorted(rec["frames"], key=int):
            mats = rec["frames"][fr]
            takes.clear_pose(arm)
            bpy.context.view_layer.update()
            for pb in order:
                if pb.name in mats:
                    pb.matrix = Matrix(mats[pb.name]) @ to_ours[pb.name]
                    bpy.context.view_layer.update()
            for pb in arm.pose.bones:
                loc, q, sc = pb.matrix_basis.decompose()
                if loc.length < 1e-4:
                    loc = Vector((0.0, 0.0, 0.0))
                if math.degrees(q.angle) < 1e-2:
                    q = q.__class__((1.0, 0.0, 0.0, 0.0))
                if max(abs(x - 1.0) for x in sc) < 1e-4:
                    sc = Vector((1.0, 1.0, 1.0))
                pb.location = loc
                pb.scale = sc
                pb.rotation_mode = "XYZ"
                pb.rotation_euler = q.to_euler("XYZ", prev[pb.name]) if pb.name in prev else q.to_euler("XYZ")
                prev[pb.name] = pb.rotation_euler.copy()
                if loc.length > 0.01 or math.degrees(q.angle) > 2.0 or max(abs(s - 1) for s in sc) > 0.01:
                    moved.add(pb.name)
                pb.keyframe_insert("location", frame=int(fr))
                pb.keyframe_insert("rotation_euler", frame=int(fr))
                pb.keyframe_insert("scale", frame=int(fr))
        takes.AUTHORED[act.name] = sorted(moved)
        acts[take] = act
    takes.clear_pose(arm)
    if arm.animation_data:
        arm.animation_data.action = None
    return acts


def build(ctx):
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    from mathutils import Matrix, Vector

    # ------------------------------------------------------------------ THE ARTIST'S RIG, AT ITS FREE-FLIGHT REST
    artist = ctx.artist(OUTPUT)
    ref, changed = rested_reference(artist)
    ctx.log("reference: the artist file with its rest at %r's first key (%d nodes re-rested: %s)" % (
        REST_TAKE, len(changed), ", ".join(sorted(changed))))
    arm, mesh = rig.import_shipped_model(ref)
    assert arm.name == ARMATURE, arm.name
    assert mesh.name == MESH_NODE, mesh.name
    mesh.data.name = GEOMETRY
    assert [m.name for m in mesh.data.materials] == list(SLOTS), [m.name for m in mesh.data.materials]
    assert [k.name for k in mesh.data.shape_keys.key_blocks][1:] == list(SHAPES), [k.name for k in mesh.data.shape_keys.key_blocks]
    bone_names = [b.name for b in arm.data.bones]
    ref_rest = {pb.name: pb.matrix.copy() for pb in arm.pose.bones}   # the reference AS DRAWN, for the takes
    baked = rig.bake_parked_pose(mesh, arm, deform_mesh=True)
    assert sorted(baked) == sorted(HOLDS), "the free-flight rest should differ from the bind on the six holds only: %s" % baked
    ctx.log("artist rig: %d verts, %d tris, %d bones; the 1.75 pod-hold scale baked on %s" % (
        len(mesh.data.vertices), f.tri_count(mesh), len(bone_names), ", ".join(baked)))

    # ------------------------------------------------------------------ PARTS (the skinning groups) at the drawn rest
    ms = morphs.MaskSet(mesh)
    co = [mesh.matrix_world @ v.co for v in mesh.data.vertices]

    def centroid(*groups):
        w = ms.mask(*groups)
        pts = [co[i] for i, x in enumerate(w) if x > 0.5]
        return sum(pts, Vector()) / len(pts), pts

    def rotate_about_origin(axis, deg, mask):
        R = Matrix.Rotation(math.radians(deg), 3, Vector(axis).normalized())

        def fn(i, c, n):
            w = mask[i]
            return (R @ c - c) * w if w > 0 else Vector((0, 0, 0))
        return fn

    fuse = ms.mask("fuse")
    shells = ms.mask(*[g[1] for g in JET_PODS.values()] + [g[1] for g in GUN_PODS.values()])

    # ------------------------------------------------------------------ MASS: the ball fattens, the shells thicken
    mass = [morphs.scale_about(MASS_BALL_SCALE, (0, 0, 0), fuse), morphs.inflate(MASS_SHELL, shells)]

    # ------------------------------------------------------------------ CHARGE: the side shooters grow
    charge = []
    for tag, (gun, ring) in GUN_PODS.items():
        gc, gpts = centroid(gun)
        breech = max(p.y for p in gpts)                       # nose is -Y: the barrel's rear end
        charge.append(morphs.stretch_along((0, -1, 0), (0, breech, 0), CHARGE_BARREL, ms.mask(gun)))
        charge.append(morphs.scale_about(CHARGE_BORE, (gc.x, gc.y, gc.z), ms.mask(gun), axes=(1, 0, 1)))
        rc, rpts = centroid(ring)
        r_out = max(math.hypot(p.y - rc.y, p.z - rc.z) for p in rpts)
        ring_m = ms.mask(ring)
        # the teeth only: a radial stretch in the ring's plane (y, z) that fades in over the outer 40%
        teeth = [w * morphs.smoothstep(0.6 * r_out, r_out, math.hypot(co[i].y - rc.y, co[i].z - rc.z)) for i, w in enumerate(ring_m)]
        charge.append(morphs.scale_about(CHARGE_RING, (rc.x, rc.y, rc.z), ring_m, axes=(0, 1, 1)))
        charge.append(morphs.scale_about(CHARGE_TEETH, (rc.x, rc.y, rc.z), teeth, axes=(0, 1, 1)))

    # ------------------------------------------------------------------ SPACE: every pod stands further out
    space = []
    for tag, groups in list(JET_PODS.items()) + list(GUN_PODS.items()):
        pc, _ = centroid(*groups)
        space.append(morphs.translate(tuple(pc.normalized() * SPACE_OUT), ms.mask(*groups)))

    # ------------------------------------------------------------------ TIME: the engine pods rake back and in
    time_ = []
    for tag, (jet, shell) in JET_PODS.items():
        pc, _ = centroid(jet, shell)
        radial = Vector((pc.x, 0.0, pc.z)).normalized()
        axis = radial.cross(Vector((0, 1, 0)))                 # rotating the radial about radial x +Y carries it toward +Y (aft)
        time_.append(rotate_about_origin(axis, TIME_SWEEP_DEG, ms.mask(jet, shell)))
        jc, jpts = centroid(jet)
        front = min(p.y for p in jpts)
        time_.append(morphs.stretch_along((0, 1, 0), (0, front, 0), TIME_NOZZLE, ms.mask(jet)))

    for name, defs in (("mass", mass), ("charge", charge), ("space", space), ("time", time_)):
        kb, mx, frac, moved = morphs.augment_shape(mesh, name, defs)   # the artist's keys exist and are EMPTY: names, case and order kept
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))

    # ------------------------------------------------------------------ TAKES: the artist's eleven, retargeted
    acts = retarget_deformation(takes, arm, ref, ref_rest)
    ctx.log("takes: %d retargeted from the artist file, deformation for deformation (%s)" % (len(acts), ", ".join(sorted(acts))))

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "yup_baked", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [ARMATURE + "|" + a.name for a in acts.values()],
            "pose_takes": [],        # MOTION takes (21 / 41 frames): take_parity checks their last frame part by part
            # every take, morph and material is proven against the artist file AT ITS FREE-FLIGHT REST (the
            # docstring says why); the four morphs are new on purpose (the artist's shipped empty)
            "take_parity": True,
            "take_parity_ref": ref,
            "artist_path": ref,
            "shape_parity_allow": {s: ["*"] for s in SHAPES},
            "size_ref_blender": ctx.export.blender_posed_bounds(ref),
            "shapes": list(SHAPES),
            "slots": list(SLOTS),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
            "sheet_poses": ("urchan_pitchup", "urchan_yawright", "urchan_rollright", "urchan_ball"),
        },
    }
