"""
rhino - the artist's RIG, through the artist-hull path (SKILL.md section 4), so the game can DRAW it.

The Rhino is a spaceship inspired by the rhinoceros: a heavy rammer whose 15-unit-tall fuselage is
the plough blade (the hull never destroys; a rammed prism is ploughed aside), with a beak that points
at the energy sword it carries 14 units ahead, two disc-engine pods threaded on standing armour
plates, and eight stepped nozzles down its raked belly. Its prefab drew the 7-mesh placeholder
`Rhino_Test.fbx` until 2026-10-18; it now draws THIS file (team task T-20261017-3b6b), so the rig is
the live model.

Kept, and proven kept by the gates on every build:
  - the artist's mesh (8,549 verts, 16,798 tris), its 12 bones, the four material slots in their
    order with their values (`take_parity.compare_materials`), the nine takes retargeted bone by
    bone (`takes.retarget_from`; RhinoAnimation is code puppetry, so no Animator plays them).
  - THE BIND IS THE REST. The artist's node chain parks the scene at the last frame of
    `rhino_pitchdown` (root 16.8 degrees, the wing chains folded up to 66.5: 25 units wide, a pose
    the game never showed), while the bind is the live silhouette to the millimetre: with the rig
    instanced at z -1.5545 under the prefab's `Rhino_Test (1)`, the fuselage, both armour plates and
    both back plates land on the placeholder's meshes and colliders exactly (only the pods sit 0.34
    forward and 0.06 inboard). So this file writes the bind as the node chain (`bake_park` is NOT
    run) and the parity gate compares against `bindrest_reference()`: the artist file with the nine
    parked Lcl Rotations set to their bind values and NOTHING else touched (takes, mesh, morphs and
    materials are the artist's bytes), so "every part moves as the artist's does" is measured from
    the rest the game draws. `rest_compare.py` against that reference is 0.000; against the raw
    artist file it names the nine parked bones, which is the point.
Added: the four element morphs the artist left EMPTY (one indexed vertex, delta 0), on the artist's
topology, each named after what the element does on this vessel (Docs/VesselModels/Rhino.md 4.2):
  - charge = the WEAPON grows: the blade itself. The crest rises and the keel deepens (the plough
    stands taller, the read from the chase camera, which sees the stern) and the beak reaches
    toward the sword's hilt.
  - mass   = VOLUME: the barrel fattens, the belly sags, the pods swell about their own axes and the
    armour plates thicken.
  - space  = REACH: the armour plates extend along their own axes - the front plates' leading edges
    forward, the back plates' trailing edges aft, both taller - nothing thickens.
  - time   = RATE: the crest rakes aft and down, the beak sharpens, the plates sweep.
  Every morph holds the eight body nozzle mouths and the two pod mouths STILL (the jets are
  prefab-placed; a morph writes mesh only), which the spec asserts on itself.
LEGACY BINDINGS (file path and guid, node / geometry / armature / bone names, slot names and order,
take names, the `yup` frame) are the artist's and untouched.
  file      Assets/_Models/Vessel Models/rhino_shapekey_with_animations.fbx (in place, guid 350f1f00)
  frame     preset "yup": roots R(-90) S(100), vertices in metres, nose -Y in the Blender frame
"""

import math
import os

NAME = "Rhino"
OUTPUT = "Assets/_Models/Vessel Models/rhino_shapekey_with_animations.fbx"

ARMATURE = "rhino"
MESH_NODE = "fusalage.002"
GEOMETRY = "Circle.004"
SLOTS = ("accent.005", "BASE.005", "windsheild.005", "N.005")
SHAPES = ("mass", "charge", "space", "time")   # the artist's names, case and order (fusion bakes key by index)

# The prefab-placed mouths every morph must hold still, in the BLENDER frame of the bind (Unity
# (x, y, z) under Rhino_Test (1) -> Blender (-x, -z - 1.5545, y)): the eight body nozzles
# (Docs/VesselModels/Rhino.md 6.1) and the two pod mouths (the rear face of each pod).
BODY_MOUTHS = [(0.0, 8.828, 3.197), (0.0, 8.344, 1.539),
               (1.359, 7.513, -1.349), (-1.359, 7.513, -1.349),
               (1.220, 4.882, -3.445), (-1.220, 4.882, -3.445),
               (0.803, 1.626, -5.271), (-0.803, 1.626, -5.271)]
POD_AXES = {"jet.l": (5.365, 0.464), "jet.r": (-5.365, 0.464)}   # (x, z) of each pod's axis (along Y)
PLATES = {"wing1.l": (5.415, 0.87, 0.11), "wing1.r": (-5.415, 0.87, 0.11),     # centre of each plate
          "wing2.l": (5.40, 6.76, 0.75), "wing2.r": (-5.40, 6.76, 0.75)}

# the artist mesh at its bind, Blender frame (fbx_probe of the pinned artist file: (-7.998, -7.988, -8.854)
# .. (7.998, 12.42, 6.455), diagonal 30.113); the morphs leave the basis untouched, so the built rest equals it
ARTIST_BIND_BOUNDS = ((-7.998, -7.988, -8.854), (7.998, 12.421, 6.455))


def bindrest_reference(artist_fbx):
    """
    The parity reference: the ARTIST file with its parked node chain set to its bind, nothing else
    changed. Written once beside the cached artist file (git-ignored); deterministic. Returns its path.
    """
    import sys
    import numpy as np
    here = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))  # Tools/Build
    if here not in sys.path:
        sys.path.insert(0, here)
    import fbx_binary as F
    out = os.path.splitext(artist_fbx)[0] + ".bindrest.fbx"
    nodes, version, footer = F.read(artist_fbx)
    objs = [n for n in nodes if n.name == "Objects"][0]
    conns = [n for n in nodes if n.name == "Connections"][0]

    def sval(v):
        return (v.decode("utf-8", "replace") if isinstance(v, bytes) else str(v)).split("\x00")[0]

    models = {o.props[0][1]: o for o in objs.children if o.name == "Model"}
    parent = {}
    for c in conns.children:
        if sval(c.props[0][1]) == "OO" and c.props[1][1] in models and c.props[2][1] in models:
            parent[c.props[1][1]] = c.props[2][1]
    bind = {}
    for o in objs.children:
        if o.name == "Pose":
            for pn in o.find("PoseNode"):
                bind[pn.first("Node").props[0][1]] = np.array(pn.first("Matrix").props[0][1], dtype=float).reshape(4, 4).T

    def euler_xyz(R):   # FBX eEulerXYZ: R = Rz @ Ry @ Rx
        return [math.degrees(a) for a in (math.atan2(R[2, 1], R[2, 2]), math.asin(max(-1.0, min(1.0, -R[2, 0]))), math.atan2(R[1, 0], R[0, 0]))]

    def lcl_rotation(model):
        for pr in model.first("Properties70").children:
            if sval(pr.props[0][1]) == "Lcl Rotation":
                return pr
        return None

    changed = {}
    worst_unparked = 0.0
    for mid, model in models.items():
        if sval(model.props[2][1]) != "LimbNode" or mid not in bind or parent.get(mid) not in bind:
            continue
        L = np.linalg.inv(bind[parent[mid]]) @ bind[mid]
        R = L[:3, :3] / np.linalg.norm(L[:3, :3], axis=0)
        want = euler_xyz(R)
        pr = lcl_rotation(model)
        have = [float(x[1]) for x in pr.props[4:7]] if pr else [0.0, 0.0, 0.0]
        rh = np.array(_euler_matrix(have))
        ang = math.degrees(math.acos(max(-1.0, min(1.0, (np.trace(rh.T @ R) - 1.0) / 2.0))))
        name = sval(model.props[1][1])
        if ang < 0.01:
            worst_unparked = max(worst_unparked, ang)
            continue
        changed[name] = (round(ang, 2), [round(v, 4) for v in want])
        if pr is None:
            raise RuntimeError("bindrest_reference: parked bone %s has no Lcl Rotation record" % name)
        pr.props[4:7] = [("D", float(v)) for v in want]
    F.write(out, nodes, version, footer)
    return out, changed


def _euler_matrix(deg):
    import numpy as np
    x, y, z = (math.radians(v) for v in deg)
    Rx = np.array([[1, 0, 0], [0, math.cos(x), -math.sin(x)], [0, math.sin(x), math.cos(x)]])
    Ry = np.array([[math.cos(y), 0, math.sin(y)], [0, 1, 0], [-math.sin(y), 0, math.cos(y)]])
    Rz = np.array([[math.cos(z), -math.sin(z), 0], [math.sin(z), math.cos(z), 0], [0, 0, 1]])
    return Rz @ Ry @ Rx


def _smooth(a, b, x):
    if b == a:
        return 1.0 if x >= b else 0.0
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def build(ctx):
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    from mathutils import Vector

    artist = ctx.artist(OUTPUT)
    ref, changed = bindrest_reference(artist)
    ctx.log("parity reference %s: %d parked bone(s) set to their bind (%s)" % (
        os.path.basename(ref), len(changed), ", ".join("%s %.1fdeg" % (k, v[0]) for k, v in sorted(changed.items()))))

    # ------------------------------------------------------------------ THE ARTIST'S HULL, AT ITS BIND
    arm, mesh = rig.import_shipped_model(artist)
    assert arm.name == ARMATURE, arm.name
    assert mesh.name == MESH_NODE, mesh.name
    mesh.data.name = GEOMETRY
    assert [m.name for m in mesh.data.materials] == list(SLOTS), [m.name for m in mesh.data.materials]
    bone_names = [b.name for b in arm.data.bones]
    parked = rig.parked_pose_bones(arm)
    takes.clear_pose(arm)   # the BIND is the rest: the parked WIP pose is dropped, not baked
    ctx.log("artist hull: %d verts, %d tris, %d bones; parked pose DROPPED on %d bone(s) (the bind is the live silhouette)" % (
        len(mesh.data.vertices), f.tri_count(mesh), len(bone_names), len(parked)))
    keys = mesh.data.shape_keys
    assert keys is not None and [k.name for k in keys.key_blocks][1:] == list(SHAPES), [k.name for k in keys.key_blocks] if keys else None
    if keys.reference_key.name != "Basis":
        keys.reference_key.name = "Basis"   # morphs.augment_shape reads the basis by this name

    # ------------------------------------------------------------------ MORPHS on the artist topology
    me = mesh.data
    ms = morphs.MaskSet(mesh)
    fuse = ms.mask("fuse")
    co0 = [v.co.copy() for v in me.vertices]

    def field(mask, fn):
        """mask * fn(co) per vertex (fn returns 0..1)."""
        return [m * fn(co0[i]) if m > 0 else 0.0 for i, m in enumerate(mask)]

    def lower_ok(c):   # the belly from the keel nozzle pair aft is nozzle country: nothing there moves
        return 1.0 - _smooth(-0.4, 0.8, c.y)

    def upper_ok(c):   # the upper hull is free forward of the stern nub
        return _smooth(-0.5, 0.6, c.z) * (1.0 - _smooth(5.5, 7.0, c.y))

    def body_ok(c):
        return max(lower_ok(c), upper_ok(c))

    def deform(fn):
        """A deformer from fn(i, co) -> delta Vector."""
        return lambda i, co, n: fn(i, co)

    Z = Vector((0.0, 0.0, 0.0))

    # charge - the blade: crest up, keel down, beak forward toward the hilt
    crest = field(fuse, lambda c: _smooth(2.6, 6.45, c.z) * (1.0 - _smooth(5.4, 6.8, c.y)))
    keel = field(fuse, lambda c: _smooth(-3.8, -8.85, c.z) * lower_ok(c))
    beak = field(fuse, lambda c: _smooth(-2.5, -7.99, c.y))
    charge = [
        deform(lambda i, c: Vector((0.0, 0.0, 1.25 * crest[i])) if crest[i] else Z),
        deform(lambda i, c: Vector((0.0, -0.35 * keel[i], -1.35 * keel[i])) if keel[i] else Z),
        deform(lambda i, c: Vector((0.0, -2.0 * beak[i], -0.25 * beak[i])) if beak[i] else Z),
    ]

    # mass - volume: the barrel, the belly, the pods about their own axes, the plates' thickness
    barrel = field(fuse, lambda c: _smooth(-7.9, -4.5, c.y) * body_ok(c))
    belly = field(fuse, lambda c: _smooth(-2.0, -8.85, c.z) * _smooth(-7.9, -4.5, c.y) * lower_ok(c))
    mass = [
        deform(lambda i, c: Vector((0.40 * c.x * barrel[i], 0.0, 0.0)) if barrel[i] else Z),
        deform(lambda i, c: Vector((0.0, 0.0, -1.4 * belly[i])) if belly[i] else Z),
    ]
    for g, (ax, az) in POD_AXES.items():
        pm = ms.mask(g)
        mass.append(deform(lambda i, c, pm=pm, ax=ax, az=az: Vector(((c.x - ax) * 0.24, 0.0, (c.z - az) * 0.24)) * pm[i] if pm[i] else Z))
    for g, (px, _py, _pz) in PLATES.items():
        pl = ms.mask(g)
        mass.append(deform(lambda i, c, pl=pl, px=px: Vector(((c.x - px) * 0.9, 0.0, 0.0)) * pl[i] if pl[i] else Z))

    # space - reach: plates extend along their own axes (front forward, back aft, both taller)
    space = []
    for g, (px, py, pz) in PLATES.items():
        pl = ms.mask(g)
        front = g.startswith("wing1")
        half = 5.44 if front else 5.66
        k = 0.40 if front else 0.45

        def sp(i, c, pl=pl, py=py, pz=pz, half=half, k=k, front=front):
            if not pl[i]:
                return Z
            d = (py - c.y) if front else (c.y - py)            # distance toward the plate's free end
            reach = k * d * _smooth(0.0, half, d) if d > 0 else 0.0
            return Vector((0.0, -reach if front else reach, 0.24 * (c.z - pz))) * pl[i]
        space.append(deform(sp))

    # time - rate: the crest rakes aft and down, the beak sharpens, the plates sweep
    rake = field(fuse, lambda c: _smooth(0.8, 6.45, c.z) * (1.0 - _smooth(5.0, 6.8, c.y)))
    point = field(fuse, lambda c: _smooth(-2.5, -7.99, c.y))
    time_ = [
        deform(lambda i, c: Vector((0.0, 0.34 * (c.z - 0.8), -0.14 * (c.z - 0.8))) * rake[i] if rake[i] else Z),
        deform(lambda i, c: Vector((-0.40 * c.x * point[i], -0.9 * point[i], 0.0)) if point[i] else Z),
    ]
    for g, (px, py, pz) in PLATES.items():
        pl = ms.mask(g)
        time_.append(deform(lambda i, c, pl=pl, pz=pz: Vector((0.0, 0.42 * (c.z - pz), 0.0)) * pl[i] if pl[i] else Z))

    authored = {"charge": charge, "mass": mass, "space": space, "time": time_}
    for name in SHAPES:
        kb, mx, frac, moved = morphs.augment_shape(mesh, name, authored[name])
        ctx.log("shape %s authored: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))

    # the mouths hold still in every morph (the jets are prefab-placed): any vertex within 1.0 of a body
    # nozzle mouth, and each pod's rear-ring centre, moves under 0.01
    basis = me.shape_keys.key_blocks["Basis"]
    near = []
    for i, c in enumerate(co0):
        if fuse[i] and any((c - Vector(m)).length < 1.0 for m in BODY_MOUTHS):
            near.append(i)
    rear = {}
    for g in POD_AXES:
        pm = ms.mask(g)
        idx = [i for i, w in enumerate(pm) if w > 0.5]
        ymax = max(co0[i].y for i in idx)
        rear[g] = [i for i in idx if co0[i].y > ymax - 0.05]
    for name in SHAPES:
        kb = me.shape_keys.key_blocks[name]
        worst = max(((kb.data[i].co - basis.data[i].co).length for i in near), default=0.0)
        assert worst < 0.01, "shape %s moves a body nozzle mouth by %.3f" % (name, worst)
        for g, idx in rear.items():
            shift = (sum((kb.data[i].co for i in idx), Vector()) - sum((basis.data[i].co for i in idx), Vector())) / len(idx)
            assert shift.length < 0.01, "shape %s moves the %s mouth centre by %.3f" % (name, g, shift.length)
    ctx.log("mouths held: %d vertices near the eight body nozzles and the two pod rear rings stay put in all four morphs" % len(near))

    # ------------------------------------------------------------------ TAKES: the artist's own, retargeted
    acts = takes.retarget_from(arm, artist)
    ctx.log("takes: %d retargeted from the artist file (%s)" % (len(acts), ", ".join(sorted(acts))))

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "yup", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [ARMATURE + "|" + a.name for a in acts.values()],
            "pose_takes": [],          # MOTION takes, 21 frames each: take_parity checks their last frame part by part
            "take_parity": True,
            "take_parity_ref": ref,    # the artist file with its parked WIP pose set to its bind (see the docstring)
            # the artist's four shapes were EMPTY: every one is authored here, so none can match the artist's
            "shape_parity_allow": {s: ["*"] for s in SHAPES},
            # the artist rig at BIND = the live Rhino's silhouette (the default reference is the parked pose,
            # 25 wide); measured on the pinned artist file with no pose (part_map / fbx_probe agree)
            "size_ref_blender": ARTIST_BIND_BOUNDS,
            "shapes": list(SHAPES),
            "slots": list(SLOTS),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
            "sheet_poses": ("rhino_pitchup", "rhino_yawright", "rhino_rollright", "rhino_speedup"),
        },
    }
