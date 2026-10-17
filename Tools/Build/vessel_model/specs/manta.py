"""
manta - the fleet's flying wing: a manta ray with long swept blades carrying eye-spots, a
canopy on a (now volumetric) fuselage, an X-tail of four fins and a trident stern over the
bomb bay. Drawn four times over (Manta, Falcon, Shrike, Termite). Written IN PLACE over the
live rig:

LEGACY BINDINGS, not design: the file path, node / geometry / armature / bone names, slot order,
take names and file frame below are what this vessel's prefab, .meta and driver bind TODAY;
.claude/skills/vessel-model/references/FLEET_CONVENTION.md section 7 names the step that retires
each one. Everything under 'Design intent' and the morph / pose meanings follow the convention.


  file      Assets/_Models/Vessel Models/Manta_shapekey_rigged.fbx (same guid; the mantis file is
            an inactive duplicate and is left alone)
  armature  Armature.002   mesh node  manta   geometry  Sphere.018
  bones     the shipped 15 (chassis root; chassis.001; Winghold.L/R -> Wing.L/R; chassis.003 ->
            WingThold/WingBhold .L/.R -> WingT/WingB .L/.R), reused: every collider hangs off a bone
            and the prefab re-poses the holds (+18 degrees) on top of THIS bind pose
  takes     none are bound (code puppetry: MantaAnimation); nine documentary poses carry the
            mantis file's names: Armature.002|{pitch up, pitch down, yaw left, yaw right, roll left,
            roll right, speedup, speeddown, cut}
  slots     0 accent (DOMAIN - the eye-spots and FlareBody), 1 BASE, 2 windsheild, 3 N (ENGINE glow)
  frame     preset "yup_baked": Y-up cm file, mesh node identity with the data baked, the armature
            at R(-90,0,0) S(100) - how this file was shipped
  size      Blender frame (nose -Y): x +-1.976, y -0.418 .. +1.857, z -0.357 .. +0.228 ; camera -30
            (the origin sits 0.418 behind the nose: the skimmer and the tail assume it)

Design: beat the shipped flatness. A fuselage with volume (0.42 wide, 0.45 tall instead of
0.32), a sunk canopy, wing-root fillets; two long swept blades, thick enough to catch light,
with a big domain eye-spot each and a lime trailing-edge stripe; four fins in an X with lime
edges; a trident of three spikes over the stern bay. Keep the planform the camera knows.
Morphs (dossier 4.1): charge = the belly and stern swell and the trident rises; mass = the
wings, fins and body thicken (no slide); space = the wing and fin tips extend (no droop);
time = the nose lengthens, everything sweeps aft, the fins fold toward the body.
"""

import math

from _common import flip, retag, box_uv, finish, rot_x, rot_y, rot_z, translate

NAME = "Manta"
OUTPUT = "Assets/_Models/Vessel Models/Manta_shapekey_rigged.fbx"

ARMATURE = "Armature.002"
MESH_NODE = "manta"
GEOMETRY = "Sphere.018"
SLOTS = ("accent", "BASE", "windsheild", "N")
D, H, W, J = 0, 1, 2, 3
TAKES = ("pitch up", "pitch down", "yaw left", "yaw right", "roll left", "roll right", "speedup", "speeddown", "cut")
SIDES = (("L", 1), ("R", -1))


def build(ctx):
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    parts = []

    # ------------------------------------------------------------------ FUSELAGE (chassis)
    hull = f.fuselage("chassis_hull", 1.88, nose=(0, -0.418, 0.0), axis=(0, 1, 0), segments=24, stations=[
        (0.00, 0.05, 0.03, 0.03, 2.4, 2.4, 0.00),
        (0.12, 0.22, 0.12, 0.10, 2.8, 2.6, 0.01),
        (0.30, 0.36, 0.20, 0.14, 3.2, 3.0, 0.03),
        (0.52, 0.42, 0.24, 0.15, 3.4, 3.2, 0.03),
        (0.75, 0.38, 0.20, 0.13, 3.2, 3.0, 0.02),
        (0.92, 0.26, 0.12, 0.09, 2.8, 2.6, 0.01),
        (1.00, 0.12, 0.05, 0.04, 2.4, 2.4, 0.00),
    ])
    f.paint(hull, H)
    # the canopy: sunk glass over the forward third
    f.inset_faces(hull, lambda c, n, i: n.z > 0.55 and -0.30 < c.y < 0.30 and abs(c.x) < 0.17, 0.012, depth=0.02, slot=W)
    # the bomb bay: a sunk belly panel aft, and a dorsal domain stripe
    f.inset_faces(hull, lambda c, n, i: i == H and n.z < -0.6 and 0.55 < c.y < 1.25 and abs(c.x) < 0.22, 0.012, depth=0.015, slot=D)
    f.paint(hull, D, lambda c, n, i: i == H and n.z > 0.85 and 0.40 < c.y < 1.30 and abs(c.x) < 0.05)
    finish(f, hull, bevel_width=0.006)
    # wing-root fillets: a flattened spine that blends the blades into the body
    fillet = f.fuselage("chassis_fillet", 1.30, nose=(0, -0.05, -0.02), axis=(0, 1, 0), segments=18, stations=[
        (0.00, 0.40, 0.05, 0.05, 4.0, 4.0, 0.00),
        (0.40, 0.78, 0.07, 0.06, 4.5, 4.5, 0.00),
        (0.75, 0.74, 0.06, 0.06, 4.5, 4.5, 0.00),
        (1.00, 0.44, 0.04, 0.04, 4.0, 4.0, 0.00),
    ])
    f.paint(fillet, H)
    finish(f, fillet, bevel_width=0.005)
    # the trident: three spikes over the stern bay
    spikes = []
    for k, (sx, sz, ang) in enumerate(((0.0, 0.10, 0), (0.16, 0.02, 14), (-0.16, 0.02, -14))):
        sp = f.cone("spike_%d" % k, 0.045, 0.55, axis=(math.sin(math.radians(ang)), math.cos(math.radians(ang)), 0.12), origin=(sx, 1.28, sz), segments=10)
        f.paint(sp, D)
        spikes.append(sp)
    # three stern nozzles under the trident: the engine glow and the node a jet can mount on
    nozzles = []
    for k, (nx, nz) in enumerate(((0.0, -0.02), (0.19, -0.06), (-0.19, -0.06))):
        # big enough that the Engine slot covers 0.3% of the hull's surface (verify's floor): the mouths must read
        nz_ = f.lathe("nozzle_%d" % k, [(0.0, 0.0), (0.085, 0.0), (0.11, 0.04), (0.11, 0.22), (0.095, 0.27), (0.0, 0.27)],
                      segments=14, axis=(0, 1, 0), origin=(nx, 1.26, nz))
        f.paint(nz_, H)
        f.inset_faces(nz_, lambda c, n, i: n.y > 0.8, 0.012, depth=0.03, slot=J)
        f.sharpen_by_angle(nz_, 35)
        nozzles.append(nz_)
    chassis = f.join([hull, fillet] + spikes + nozzles, "chassis", SLOTS)
    retag(f, chassis)
    parts.append(chassis)

    # ------------------------------------------------------------------ WINGS (Wing.L / Wing.R)
    for tag, sx in SIDES:
        w = f.wing("Wing." + tag, root=(sx * 0.36, 0.05, 0.0), span=1.62, chord_root=0.74, chord_tip=0.30,
                   sweep=0.78, dihedral_deg=0.0, twist_deg=-3.0, thickness=0.15, camber=0.02, sections=9,
                   segments=18, side=sx, taper_power=1.0)
        f.paint(w, H)
        # the leading edge wears the domain; the trailing edge glows (the engine stripe)
        f.paint(w, D, lambda c, n, i: n.y < -0.55)
        f.paint(w, J, lambda c, n, i: n.y > 0.6)
        finish(f, w, bevel_width=0.006, sharp_deg=45)
        # the eye-spot: a lens on the top surface, two thirds out, ringed dark
        eye = f.lathe("Wing_eye." + tag, [(0.0, 0.0), (0.12, 0.0), (0.135, 0.01), (0.135, 0.022), (0.09, 0.032), (0.0, 0.036)],
                      segments=18, axis=(0, 0, 1), origin=(sx * 1.22, 0.62, 0.040))
        f.paint(eye, D)
        f.paint(eye, H, lambda c, n, i: abs(n.z) < 0.5)
        f.sharpen_by_angle(eye, 35)
        parts += [w, eye]

    # ------------------------------------------------------------------ FINS (WingT / WingB)
    for tag, sx in SIDES:
        for name, zc, droop in (("WingT." + tag, 0.14, 1), ("WingB." + tag, -0.23, -1)):
            fin = f.wing(name, root=(sx * 0.42, 0.95, zc), span=0.66, chord_root=0.52, chord_tip=0.16,
                         sweep=0.42, dihedral_deg=droop * 12.0, twist_deg=0.0, thickness=0.11, sections=6,
                         segments=14, side=sx, taper_power=0.9)
            f.paint(fin, H)
            f.paint(fin, D, lambda c, n, i: n.y < -0.5)
            f.paint(fin, J, lambda c, n, i: n.y > 0.6)
            finish(f, fin, bevel_width=0.005, sharp_deg=45)
            parts.append(fin)

    # ------------------------------------------------------------------ JOIN
    mesh = f.join(parts, MESH_NODE, SLOTS)
    mesh.data.name = GEOMETRY
    f.set_smooth(mesh, True)
    f.sharpen_by_angle(mesh, 44)
    box_uv(mesh)
    ctx.log("manta joined: %d verts, %d tris" % (len(mesh.data.vertices), f.tri_count(mesh)))

    # ------------------------------------------------------------------ RIG: the shipped armature
    arm = rig.import_shipped_armature(ctx.artist(OUTPUT))
    assert arm.name == ARMATURE, arm.name
    bone_names = [b.name for b in arm.data.bones]
    rig.skin_rigid(mesh, arm, group_to_bone={"Wing_eye.L": "Wing.L", "Wing_eye.R": "Wing.R"}, default_bone="chassis")
    ctx.log("rig: %d bones reused" % len(bone_names))

    # ------------------------------------------------------------------ MORPHS (dossier 4.1)
    msk = morphs.MaskSet(mesh)
    body = msk.mask("chassis")
    wings = msk.mask("Wing.L", "Wing.R")
    fins = msk.mask("WingT.L", "WingT.R", "WingB.L", "WingB.R")
    stern = morphs.falloff_mask(mesh, body, (0, 0, 0), (0, 1, 0), 0.5, 1.0)
    trident = morphs.falloff_mask(mesh, body, (0, 0, 0), (0, 1, 0), 1.25, 1.35)
    shapes = {
        "charge": [  # the bay runs hot: the belly and stern swell, the trident rises
            morphs.scale_about(1.35, (0, 0, 0.0), stern, axes=(1, 0, 1)),
            morphs.inflate(0.06, stern),
            morphs.stretch_along((0, 1, 0), (0, 1.28, 0), 1.5, trident, ramp=(0.0, 0.05)),
        ],
        "mass": [  # the hull thickens: wings, fins and body, no slide
            morphs.scale_about(1.7, (0, 0, 0), wings, axes=(0, 0, 1)),
            morphs.scale_about(1.7, (0, 0, 0), fins, axes=(0, 0, 1)),
            morphs.scale_about(1.3, (0, 0, 0.03), body, axes=(1, 0, 1)),
            morphs.inflate(0.03, body),
        ],
        "space": [  # reach: the wing tips and fin tips extend, flat
            morphs.scale_about(1.32, (0, 0, 0), morphs.falloff_mask(mesh, wings, (0, 0, 0), (1, 0, 0), 0.4, 0.8), axes=(1, 0, 0)),
            morphs.scale_about(1.32, (0, 0, 0), morphs.falloff_mask(mesh, wings, (0, 0, 0), (-1, 0, 0), 0.4, 0.8), axes=(1, 0, 0)),
            morphs.scale_about(1.3, (0, 0.88, 0), fins, axes=(1, 1, 0)),
        ],
        "time": [  # speed: the nose lengthens, everything sweeps aft, the fins fold in
            morphs.stretch_along((0, -1, 0), (0, 0.2, 0), 1.35, body, ramp=(0.0, 0.4)),
            morphs.sweep((1, 0, 0), (0, 0, 0), (0, 0.30, 0), morphs.falloff_mask(mesh, wings, (0, 0, 0), (1, 0, 0), 0.0, 0.05)),
            morphs.sweep((-1, 0, 0), (0, 0, 0), (0, 0.30, 0), morphs.falloff_mask(mesh, wings, (0, 0, 0), (-1, 0, 0), 0.0, 0.05)),
            morphs.scale_about(0.75, (0, 0.88, 0), fins, axes=(1, 0, 0)),
            morphs.translate((0, 0.25, 0), fins),
        ],
    }
    for name, defs in shapes.items():
        kb, mx, frac, moved = morphs.add_shape(mesh, name, defs)
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))

    # ------------------------------------------------------------------ POSES (world axes; nose -Y)
    fin_names = ["WingT.L", "WingT.R", "WingB.L", "WingB.R"]

    def pose(p):
        if p == "pitch up":
            return {"Wing.L": (-24, 0, 0), "Wing.R": (-24, 0, 0), **{n: (-24, 0, 0) for n in fin_names}}
        if p == "pitch down":
            return {"Wing.L": (24, 0, 0), "Wing.R": (24, 0, 0), **{n: (24, 0, 0) for n in fin_names}}
        if p in ("yaw left", "yaw right"):
            s = 1 if p == "yaw right" else -1
            return {"Wing.L": (0, 0, s * 21), "Wing.R": (0, 0, s * 21), **{n: (0, 0, -s * 21) for n in fin_names}}
        if p in ("roll left", "roll right"):
            s = 1 if p == "roll right" else -1
            return {"Wing.L": (0, s * 27, 0), "Wing.R": (0, s * 27, 0), **{n: (0, s * 27, 0) for n in fin_names}}
        if p == "speedup":
            return {"Wing.L": (0, 0, 60), "Wing.R": (0, 0, -60), "WingT.L": (0, 27, 0), "WingB.L": (0, -27, 0), "WingT.R": (0, -27, 0), "WingB.R": (0, 27, 0)}
        if p == "speeddown":
            return {"Wing.L": (0, 0, -30), "Wing.R": (0, 0, 30), "WingT.L": (0, -27, 0), "WingB.L": (0, 27, 0), "WingT.R": (0, 27, 0), "WingB.R": (0, -27, 0)}
        if p == "cut":
            return {"Wing.L": (0, 0, 40), "Wing.R": (0, 0, -40)}
        return {}

    acts = {}
    for p in TAKES:
        acts[p] = takes.pose_take(arm, p, pose(p))
    takes.clear_pose(arm)

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "yup_baked", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [ARMATURE + "|" + p for p in TAKES],
            "pose_takes": [ARMATURE + "|" + p for p in TAKES],
            "sheet_poses": ("pitch up", "yaw right", "roll right", "speedup"),
            "shapes": ["charge", "mass", "space", "time"],
            "slots": list(SLOTS),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
        },
    }
