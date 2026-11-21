"""
_wedge - the pipeline's smoke test: a generic delta-wing hull that exercises every stage.

Not a vessel the game ships. It exists so `build.py _wedge --out /tmp/x.fbx` proves the whole
chain (forms -> join -> rig -> morphs -> poses -> export -> strip -> three-reader verify ->
sheet) in under a minute, and so a new spec has a complete worked example to copy.
"""

import math

NAME = "Wedge"
OUTPUT = ""  # not shipped

SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3


def build(ctx):
    f, rig, morphs, takes, mats = ctx.forms, ctx.rig, ctx.morphs, ctx.takes, ctx.materials
    L = 6.0  # hull length in Blender units = Unity units

    # ---- body: nose on -Y, tail on +Y, belly flat, back domed
    body = f.fuselage("fuse", L, nose=(0, -L * 0.5, 0), axis=(0, 1, 0), stations=[
        (0.00, 0.05, 0.04, 0.03, 2.0, 2.0, 0.00),
        (0.10, 0.28, 0.22, 0.14, 2.4, 3.0, 0.02),
        (0.30, 0.55, 0.40, 0.22, 2.6, 3.5, 0.06),
        (0.55, 0.70, 0.46, 0.26, 2.8, 4.0, 0.08),
        (0.80, 0.60, 0.40, 0.24, 2.8, 4.0, 0.06),
        (1.00, 0.35, 0.22, 0.16, 2.6, 3.0, 0.02),
    ], segments=32)
    f.paint(body, H)
    # canopy: the upper faces in the front third wear glass
    f.paint(body, W, lambda c, n, i: n.z > 0.55 and -L * 0.35 < c.y < -L * 0.05 and abs(c.x) < 0.3)
    # a domain band along the top spine behind the canopy
    f.paint(body, D, lambda c, n, i: n.z > 0.2 and -L * 0.05 < c.y < L * 0.42 and abs(c.x) < 0.45)
    f.sharpen_by_angle(body, 45)

    # ---- wings (right, then mirrored)
    wr = f.wing("wing.r", root=(0.45, -L * 0.05, -0.05), span=2.6, chord_root=2.2, chord_tip=0.7,
                sweep=1.6, dihedral_deg=6, thickness=0.09, sections=6, segments=20, side=1)
    f.paint(wr, D)
    f.paint(wr, H, lambda c, n, i: n.z < -0.3)  # underside dark
    wl = f.mirror_part(wr, "wing.l")

    # ---- engine pods on the wing roots
    pod_profile = [(0.0, 0.0), (0.16, 0.08), (0.24, 0.35), (0.26, 1.1), (0.22, 1.6), (0.26, 1.75), (0.0, 1.78)]
    pr = f.lathe("jet.r", pod_profile, segments=20, axis=(0, 1, 0), origin=(0.95, L * 0.12, -0.12))
    f.paint(pr, H)
    f.paint(pr, J, lambda c, n, i: c.y > L * 0.12 + 1.55)  # the nozzle mouth
    pl = f.mirror_part(pr, "jet.l")

    # ---- tail fin
    fin = f.plate("fin", [(0.0, 0.0), (0.9, 0.0), (1.05, 0.35), (0.8, 1.1), (0.55, 1.15), (0.3, 0.6)],
                  thickness=0.06, normal=(1, 0, 0), origin=(0, L * 0.3, 0.3), right=(0, 1, 0))
    f.paint(fin, D)

    mesh = f.join([body, wr, wl, pr, pl, fin], "WedgeMesh", SLOTS)
    f.set_smooth(mesh, True)
    f.sharpen_by_angle(mesh, 50)
    ctx.log("wedge joined: %d verts" % len(mesh.data.vertices))

    # ---- UVs: a simple projection so the exporter has a layer (the game's shader needs none)
    _box_uv(mesh)

    # ---- rig
    arm = rig.build_armature("WedgeArmature", [
        rig.BoneSpec("fuse", (0, L * 0.1, 0), (0, -L * 0.3, 0)),
        rig.BoneSpec("wing.r", (0.45, 0.0, -0.05), (2.5, 0.0, -0.05), parent="fuse"),
        rig.BoneSpec("wing.l", (-0.45, 0.0, -0.05), (-2.5, 0.0, -0.05), parent="fuse"),
        rig.BoneSpec("jet.r", (0.95, L * 0.12, -0.12), (0.95, L * 0.12 + 1.5, -0.12), parent="fuse"),
        rig.BoneSpec("jet.l", (-0.95, L * 0.12, -0.12), (-0.95, L * 0.12 + 1.5, -0.12), parent="fuse"),
        rig.BoneSpec("fin", (0, L * 0.3, 0.3), (0, L * 0.3 + 0.6, 1.2), parent="fuse"),
    ])
    rig.skin_rigid(mesh, arm, default_bone="fuse")

    # ---- element morphs
    ms = morphs.MaskSet(mesh)
    wings = ms.mask("wing.l", "wing.r")
    hull = ms.mask("fuse")
    jets = ms.mask("jet.l", "jet.r")
    finm = ms.mask("fin")
    shapes = {}
    shapes["charge"] = [morphs.stretch_along((0, 1, 0), (0, L * 0.12, 0), 1.45, jets),
                        morphs.scale_about(1.25, (0, L * 0.12 + 1.6, -0.12), jets, axes=(1, 0, 1))]
    shapes["mass"] = [morphs.inflate(0.38, hull), morphs.inflate(0.06, wings)]
    shapes["space"] = [morphs.scale_about(1.35, (0, 0, 0), wings, axes=(1, 0, 0)),
                       morphs.scale_about(1.3, (0, L * 0.3, 0.3), finm, axes=(0, 0, 1))]
    shapes["time"] = [morphs.stretch_along((0, -1, 0), (0, L * 0.1, 0), 1.3, hull, ramp=(0.0, 1.5)),
                      morphs.sweep((1, 0, 0), (0, 0, 0), (0, 0.35, 0), wings)]
    for name, defs in shapes.items():
        kb, mx, frac, moved = morphs.add_shape(mesh, name, defs)
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))

    # ---- the nine standard poses
    def pose(p):
        a = 22.0
        if p == "idle":
            return {}
        if p == "pitchup":
            return {"fin": (0, 0, 0), "wing.r": (-a * 0.4, 0, 0), "wing.l": (-a * 0.4, 0, 0), "jet.r": (-a * 0.5, 0, 0), "jet.l": (-a * 0.5, 0, 0)}
        if p == "pitchdown":
            return {"wing.r": (a * 0.4, 0, 0), "wing.l": (a * 0.4, 0, 0), "jet.r": (a * 0.5, 0, 0), "jet.l": (a * 0.5, 0, 0)}
        if p == "yawright":
            return {"fin": (0, 0, -a), "jet.r": (0, 0, -a * 0.3), "jet.l": (0, 0, -a * 0.3)}
        if p == "yawleft":
            return {"fin": (0, 0, a), "jet.r": (0, 0, a * 0.3), "jet.l": (0, 0, a * 0.3)}
        if p == "rollright":
            return {"wing.r": (0, a * 0.6, 0), "wing.l": (0, -a * 0.6, 0)}
        if p == "rollleft":
            return {"wing.r": (0, -a * 0.6, 0), "wing.l": (0, a * 0.6, 0)}
        if p == "speedup":
            return {"wing.r": (0, 0, -a * 0.5), "wing.l": (0, 0, a * 0.5), "jet.r": {"loc": (0, 0.25, 0)}, "jet.l": {"loc": (0, 0.25, 0)}}
        if p == "speeddown":
            return {"wing.r": (0, 0, a * 0.4), "wing.l": (0, 0, -a * 0.4)}
        return {}
    acts = takes.standard_pose_set(arm, pose)

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    bounds_blender = (tuple(lo), tuple(hi))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "armature": "WedgeArmature", "mesh_node": "WedgeMesh",
            "bones": ["fuse", "wing.r", "wing.l", "jet.r", "jet.l", "fin"],
            "takes": ["WedgeArmature|" + p for p in acts],
            "pose_takes": ["WedgeArmature|" + p for p in acts],
            "shapes": ["charge", "mass", "space", "time"],
            "slots": list(SLOTS),
            "bounds_blender": bounds_blender, "bounds_tol": 0.05,
            "min_travel": 0.02,
        },
    }


def _box_uv(obj):
    import bmesh
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    uv = bm.loops.layers.uv.get("UVMap") or bm.loops.layers.uv.new("UVMap")
    lo = [min(v.co[i] for v in bm.verts) for i in range(3)]
    hi = [max(v.co[i] for v in bm.verts) for i in range(3)]
    span = [max(hi[i] - lo[i], 1e-6) for i in range(3)]
    for face in bm.faces:
        n = face.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        u_i, v_i = [(1, 2), (0, 2), (0, 1)][ax]
        for loop in face.loops:
            co = loop.vert.co
            loop[uv].uv = ((co[u_i] - lo[u_i]) / span[u_i], (co[v_i] - lo[v_i]) / span[v_i])
    bm.to_mesh(me)
    bm.free()
    me.update()
