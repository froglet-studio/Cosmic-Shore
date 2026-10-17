"""
sparrow - the shooter: a bird of prey with a beaked nose, a raised canopy, two fans of eight
feather-blades, a gun pod on each wing root, a missile on each flank under a cluster of armour
shells, and six tail tubes the jets ride. Written IN PLACE over the live model:

LEGACY BINDINGS, not design: the file path, node / geometry / armature / bone names, slot order,
take names and file frame below are what this vessel's prefab, .meta and driver bind TODAY;
.claude/skills/vessel-model/references/FLEET_CONVENTION.md section 7 names the step that retires
each one. Everything under 'Design intent' and the morph / pose meanings follow the convention.


  file      Assets/_Models/Vessel Models/SparrowModel1.fbx (same guid; SparrowModel4.fbx, which
            only lends the Missile Launch clips, is left alone)
  armature  Sparrow_Armature+Mesh   (the artist's: scaled 0.2034, turned 180 about Z - reused as-is)
  mesh node a_Sparrow_Mesh(MedPoly+Shapekeys)     geometry Cube.113
  bones     the shipped 65, reused: a_Body, b_WholeWing.L/.R, b_SpinWing.L/.R + .001-.008 fans,
            b_ShipGun1/2.L/.R, b_SlideChip1-3.L/.R, b_Missile.L/.R, b_Shell1-5.L/.R, b_Tail1-3.L/.R
            (the jets mount on b_Tail1..3 at width x1.5; the bays launch from b_Missile.L/.R)
  takes     Sparrow_Armature+Mesh|{Idle,PitchUp,PitchDown,YawLeft,YawRight,SpeedUp,SpeedDown,
            Speed+PitchUp,Speed+PitchDown,Speed+TurnLeft,Speed+TurnRight}_SparrowAnim (+ the
            Idle_SparrowAnim.006 duplicate the meta lists): 1-frame POSES for the 2D blend trees
  slots     0 Body, 1 Domain, 2 Engine, 3 Window  (the shipped order; slot 0 takes the domain paint)
  frame     preset "zup" (like the Squirrel): nose -Y, up +Z, .L on +X in the Blender frame
  size      x +-6.25, y -4.66 .. +7.57, z -0.82 .. +0.96 ; camera -50

Design: the bird. A beak nose with the canopy sunk behind it; broad shoulders the fans grow
from; eight overlapping blades a side fanning 155 degrees from trailing to leading; a long gun
over each shoulder with a lime muzzle; the missile tucked under three shells; six tail tubes
in a fan with lime mouths. Domain: the blade tips, the shoulder stripe, the shell edges.
Morphs: charge = guns lengthen and the missiles swell; mass = body and shoulders thicken;
space = the fans and tails reach; time = the fans sweep back, the beak and tails stretch.
"""

import math

from _common import flip, retag, box_uv, finish, rot_z, rot_x, rot_y, translate, scale_axis

NAME = "Sparrow"
OUTPUT = "Assets/_Models/Vessel Models/SparrowModel1.fbx"

ARMATURE = "Sparrow_Armature+Mesh"
MESH_NODE = "a_Sparrow_Mesh(MedPoly+Shapekeys)"
GEOMETRY = "Cube.113"
SLOTS = ("Body", "Domain", "Engine", "Window")
H, D, J, W = 0, 1, 2, 3
TAKES = ("Idle", "PitchUp", "PitchDown", "YawLeft", "YawRight", "SpeedUp", "SpeedDown",
         "Speed+PitchUp", "Speed+PitchDown", "Speed+TurnLeft", "Speed+TurnRight")
SIDES = (("L", 1), ("R", -1))

# the eight blades of a fan: (index, angle from +X toward +Y in degrees, length, width)
FAN = [(1, 72, 4.6, 0.52), (2, 58, 4.7, 0.56), (3, 42, 4.8, 0.58), (4, 23, 4.8, 0.58),
       (5, 0, 4.7, 0.56), (6, -28, 4.4, 0.52), (7, -57, 4.0, 0.46), (8, -82, 3.4, 0.40)]
FAN_PIVOT = (1.50, 0.50, -0.10)


def build(ctx):
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    parts = []

    # ------------------------------------------------------------------ BODY (a_Body)
    body = f.fuselage("body_hull", 5.50, nose=(0, -1.55, 0.05), axis=(0, 1, 0), segments=28, stations=[
        (0.00, 0.06, 0.05, 0.05, 2.2, 2.2, 0.00),
        (0.10, 0.34, 0.20, 0.22, 2.8, 2.6, 0.02),
        (0.25, 0.64, 0.36, 0.34, 3.2, 3.0, 0.06),
        (0.45, 1.02, 0.44, 0.38, 3.6, 3.4, 0.08),
        (0.62, 1.00, 0.42, 0.36, 3.6, 3.4, 0.06),
        (0.80, 0.70, 0.34, 0.30, 3.2, 3.0, 0.04),
        (1.00, 0.36, 0.20, 0.18, 2.8, 2.6, 0.02),
    ])
    f.paint(body, H)
    # the canopy: sunk glass behind the beak
    f.inset_faces(body, lambda c, n, i: n.z > 0.55 and -1.05 < c.y < -0.05 and abs(c.x) < 0.30, 0.03, depth=0.04, slot=W)
    # a shoulder stripe of domain either side, and a dorsal stripe aft
    f.paint(body, D, lambda c, n, i: i == H and abs(n.x) > 0.5 and 0.1 < c.y < 2.2 and c.z > 0.05 and abs(c.x) > 0.5)
    f.paint(body, D, lambda c, n, i: i == H and n.z > 0.85 and 0.4 < c.y < 3.6 and abs(c.x) < 0.12)
    for (y0, y1) in ((-0.9, -0.3), (0.3, 1.2), (1.4, 2.3), (2.5, 3.6)):
        f.inset_faces(body, lambda c, n, i, y0=y0, y1=y1: i == H and abs(n.x) > 0.6 and y0 < c.y < y1 and -0.2 < c.z < 0.3, 0.025, depth=0.015)
    finish(f, body, bevel_width=0.015)
    # the tail boom reaches back under the tubes, and a cross spar ties the six tubes to it
    boom = f.fuselage("body_boom", 2.30, nose=(0, 3.60, 0.02), axis=(0, 1, 0), segments=14, stations=[
        (0.00, 0.34, 0.20, 0.18, 2.8, 2.6, 0.00),
        (0.45, 0.30, 0.17, 0.15, 2.8, 2.6, 0.02),
        (1.00, 0.18, 0.10, 0.09, 2.6, 2.4, 0.04),
    ])
    f.paint(boom, H)
    f.paint(boom, D, lambda c, n, i: n.z > 0.85 and abs(c.x) < 0.08)
    finish(f, boom, bevel_width=0.01)
    spar = f.plate("body_spar", f.rounded_rect(4.30, 0.42, r=0.12), thickness=0.22, normal=(0, 1, 0), origin=(0, 5.25, 0.07), right=(1, 0, 0))
    f.paint(spar, H)
    f.paint(spar, D, lambda c, n, i: abs(n.y) > 0.8)
    finish(f, spar, bevel_width=0.012)
    # beak ridge
    beak = f.plate("body_beak", f.blade(1.1, 0.26, tip=0.1), thickness=0.08, normal=(1, 0, 0), origin=(0, -0.75, 0.30), right=(0, -1, 0.15))
    f.paint(beak, D)
    finish(f, beak, bevel_width=0.01)
    a_body = f.join([body, boom, spar, beak], "a_Body", SLOTS)
    retag(f, a_body)
    parts.append(a_body)

    # ------------------------------------------------------------------ SHOULDERS, FANS, GUNS, CHIPS
    for tag, sx in SIDES:
        # shoulder block the fan pivots on (b_WholeWing)
        sh = f.fuselage("b_WholeWing." + tag, 2.30, nose=(sx * 2.05, 0.12, -0.06), axis=(0, 1, 0), segments=18, stations=[
            (0.00, 0.60, 0.20, 0.26, 3.6, 3.2, 0.00),
            (0.30, 0.98, 0.30, 0.34, 4.0, 3.6, 0.02),
            (0.70, 0.96, 0.28, 0.32, 4.0, 3.6, 0.00),
            (1.00, 0.70, 0.18, 0.22, 3.4, 3.0, -0.04),
        ])
        f.paint(sh, H)
        f.paint(sh, D, lambda c, n, i: n.z > 0.8 and 0.5 < c.y < 2.0)
        finish(f, sh, bevel_width=0.012)
        parts.append(sh)
        # the fan root hub (b_SpinWing): a short drum at the pivot
        hub = f.lathe("b_SpinWing." + tag, [(0.0, 0.0), (0.34, 0.0), (0.38, 0.04), (0.38, 0.20), (0.34, 0.24), (0.0, 0.24)],
                      segments=18, axis=(0, 0, 1), origin=(sx * FAN_PIVOT[0], FAN_PIVOT[1], FAN_PIVOT[2] - 0.12))
        f.paint(hub, H)
        f.paint(hub, D, lambda c, n, i: abs(n.z) < 0.5)
        finish(f, hub, bevel_width=0.008)
        parts.append(hub)
        # eight blades
        for (k, ang, ln, wd) in FAN:
            outline = f.feather(ln, wd, tip_round=0.45, root_width=wd * 0.38, segments=12, curve=0.03)
            bl = f.plate("b_SpinWing.%s.%03d" % (tag, k), outline, thickness=0.05, normal=(0, 0, 1), origin=(0, 0, 0), right=(1, 0, 0))
            for cx in (ln * 0.18, ln * 0.26, ln * 0.55, ln * 0.78):
                f.cut_plane(bl, (cx, 0, 0), (1, 0, 0))
            f.paint(bl, H)
            f.paint(bl, D, lambda c, n, i, ln=ln: n.z > 0.4 and c.x > ln * 0.55)     # domain tips on top
            f.paint(bl, D, lambda c, n, i, ln=ln: n.z > 0.4 and ln * 0.18 < c.x < ln * 0.26)  # a root band
            finish(f, bl, bevel_width=0.008)
            # a slight droop outward and the fan angle; the blade root starts 0.3 inside the hub
            m = rot_z(ang * sx) @ rot_y(sx * 3.0) @ translate(0.05, 0, 0)
            f.transform_mesh(bl, m)
            if sx < 0:
                f.transform_mesh(bl, scale_axis(-1, (1, 0, 0)) @ scale_axis(-1, (1, 0, 0)))  # no-op; mirrored by the angle sign
            # stack them slightly so they overlap in z like feathers
            f.transform_mesh(bl, translate(sx * FAN_PIVOT[0], FAN_PIVOT[1], FAN_PIVOT[2] + 0.012 * (k - 4)))
            parts.append(bl)
        # guns: the long outboard barrel and the short inboard one, muzzles forward (-Y)
        g1 = f.lathe("b_ShipGun1." + tag,
                     [(0.0, 0.0), (0.14, 0.0), (0.17, 0.06), (0.17, 0.40), (0.24, 0.46), (0.26, 0.90), (0.22, 1.10), (0.22, 2.60), (0.30, 2.70), (0.30, 3.30), (0.26, 3.44), (0.0, 3.44)],
                     segments=18, axis=(0, -1, 0), origin=(sx * 2.85, 1.19, 0.40))
        f.paint(g1, H)
        f.paint(g1, D, lambda c, n, i: abs(n.y) < 0.5 and -0.6 < c.y < 0.2)
        f.inset_faces(g1, lambda c, n, i: n.y < -0.8, 0.03, depth=0.08, slot=J)
        finish(f, g1, bevel_width=0.008)
        g2 = f.lathe("b_ShipGun2." + tag,
                     [(0.0, 0.0), (0.12, 0.0), (0.15, 0.05), (0.15, 0.9), (0.19, 0.98), (0.19, 2.30), (0.16, 2.40), (0.0, 2.40)],
                     segments=16, axis=(0, -1, 0), origin=(sx * 2.37, 1.95, 0.41))
        f.paint(g2, H)
        f.inset_faces(g2, lambda c, n, i: n.y < -0.8, 0.025, depth=0.06, slot=J)
        finish(f, g2, bevel_width=0.008)
        parts += [g1, g2]
        # three slide chips: vertical plates under the shoulder's leading edge
        for k, yc in ((1, -0.28), (2, -0.60), (3, -0.92)):
            ch = f.plate("b_SlideChip%d.%s" % (k, tag), f.rounded_rect(0.30, 0.54, r=0.08), thickness=0.46,
                         normal=(0, 1, 0), origin=(sx * 1.61, yc, -0.11), right=(1, 0, 0))
            f.paint(ch, H)
            f.paint(ch, D, lambda c, n, i: n.y < -0.5)
            finish(f, ch, bevel_width=0.01)
            parts.append(ch)

        # ------------------------------------------------------------------ MISSILE + SHELLS
        ms = f.lathe("b_Missile." + tag, [(0.0, 0.0), (0.10, 0.08), (0.16, 0.30), (0.17, 1.30), (0.14, 1.55), (0.0, 1.68)],
                     segments=14, axis=(0, -1, 0), origin=(sx * 0.57, 2.84, 0.22))
        f.paint(ms, H)
        f.paint(ms, D, lambda c, n, i: 1.3 < c.y < 1.55)
        finish(f, ms, bevel_width=0.006)
        parts.append(ms)
        shells = [  # (name, centre, size (x,y,z), normal tilt)
            ("b_Shell1." + tag, (sx * 0.75, 2.10, 0.45), (0.72, 1.80, 0.10), (sx * 0.55, 0, 0.84)),
            ("b_Shell2." + tag, (sx * 0.60, 3.00, 0.50), (0.60, 1.50, 0.10), (sx * 0.35, 0, 0.94)),
            ("b_Shell3." + tag, (sx * 0.62, 2.37, -0.40), (0.80, 1.40, 0.10), (sx * 0.45, 0, -0.89)),
            ("b_Shell4." + tag, (sx * 0.47, 3.56, -0.36), (0.56, 0.70, 0.10), (sx * 0.35, 0, -0.94)),
            ("b_Shell5." + tag, (sx * 0.18, 3.37, 0.52), (0.28, 1.05, 0.10), (sx * 0.1, 0, 0.99)),
        ]
        for name, c, size, nrm in shells:
            sp = f.plate(name, f.rounded_rect(size[0], size[1], r=0.08), thickness=size[2], normal=nrm, origin=c, right=(1, 0, 0))
            f.paint(sp, H)
            f.paint(sp, D, lambda cc, n, i, nrm=nrm: n.x * nrm[0] + n.y * nrm[1] + n.z * nrm[2] > 0.8 and abs(cc.x) > 0.0 and ((abs(cc.x * 10) % 3) < 1.0))
            finish(f, sp, bevel_width=0.01)
            parts.append(sp)

        # ------------------------------------------------------------------ TAIL TUBES (the jet hosts)
        tails = [("b_Tail1." + tag, sx * 0.61, 4.58, 3.00, 0.36, 0.88), ("b_Tail2." + tag, sx * 1.18, 4.92, 2.32, 0.26, 0.66), ("b_Tail3." + tag, sx * 1.77, 5.30, 1.46, 0.20, 0.46)]
        for name, cx, y0, ln, r, hz in tails:
            tube = f.lathe(name, [(0.0, 0.0), (r * 0.6, 0.0), (r, 0.10), (r, ln * 0.55), (r * 1.15, ln * 0.60), (r * 1.15, ln * 0.70), (r, ln * 0.74), (r, ln - 0.1), (r * 0.9, ln), (0.0, ln)],
                           segments=16, axis=(0, 1, 0), origin=(cx, y0, 0.07))
            f.paint(tube, H)
            f.paint(tube, D, lambda c, n, i, y0=y0, ln=ln: abs(n.y) < 0.5 and y0 + ln * 0.60 < c.y < y0 + ln * 0.70)
            f.inset_faces(tube, lambda c, n, i: n.y > 0.8, 0.03, depth=0.06, slot=J)
            # a vertical fin on each tube so the tail reads as a tail
            fin = f.plate(name + "_fin", f.blade(ln * 0.7, hz, tip=0.2), thickness=0.04, normal=(1, 0, 0),
                          origin=(cx, y0 + ln * 0.15, 0.07), right=(0, 1, 0))
            f.paint(fin, H)
            f.paint(fin, D, lambda c, n, i: c.z > 0.072 + hz * 0.3)
            finish(f, tube, bevel_width=0.008)
            finish(f, fin, bevel_width=0.008)
            parts += [tube, fin]

    # ------------------------------------------------------------------ JOIN
    mesh = f.join(parts, MESH_NODE, SLOTS)
    mesh.data.name = GEOMETRY
    f.set_smooth(mesh, True)
    f.sharpen_by_angle(mesh, 42)
    box_uv(mesh)
    ctx.log("sparrow joined: %d verts, %d tris" % (len(mesh.data.vertices), f.tri_count(mesh)))

    # ------------------------------------------------------------------ RIG: the shipped armature
    arm = rig.import_shipped_armature(OUTPUT)
    assert arm.name == ARMATURE, arm.name
    bone_names = [b.name for b in arm.data.bones]
    g2b = {}
    for tag, _sx in SIDES:
        for k in (1, 2, 3):
            g2b["b_Tail%d.%s_fin" % (k, tag)] = "b_Tail%d.%s" % (k, tag)
    rig.skin_rigid(mesh, arm, group_to_bone=g2b, default_bone="a_Body")
    ctx.log("rig: %d bones reused" % len(bone_names))

    # ------------------------------------------------------------------ MORPHS
    msk = morphs.MaskSet(mesh)
    blades = msk.mask(*[b for b in bone_names if "SpinWing" in b and b[-1].isdigit()])
    guns = msk.mask(*[b for b in bone_names if "ShipGun" in b])
    missiles = msk.mask(*[b for b in bone_names if "Missile" in b])
    shells = msk.mask(*[b for b in bone_names if "Shell" in b])
    bodym = msk.mask("a_Body")
    shoulders = msk.mask("b_WholeWing.L", "b_WholeWing.R", "b_SpinWing.L", "b_SpinWing.R")
    tails = msk.mask(*[b for b in bone_names if "Tail" in b])
    lo, hi = f.bounds(mesh)
    shapes = {
        "Charge": [  # the weapon: guns lengthen and thicken, the missiles swell, the shells lift
            morphs.stretch_along((0, -1, 0), (0, 1.2, 0), 1.30, guns, ramp=(0.0, 0.5)),
            morphs.scale_about(1.25, (0, 0, 0.4), guns, axes=(1, 0, 1)),
            morphs.inflate(0.07, missiles), morphs.inflate(0.05, shells),
        ],
        "Mass": [  # volume: the body and shoulders thicken
            morphs.scale_about(1.32, (0, 0, 0.05), bodym, axes=(1, 0, 1)),
            morphs.inflate(0.12, bodym), morphs.inflate(0.22, shoulders), morphs.inflate(0.05, blades),
        ],
        "Space": [  # reach: the fans and the tails extend
            morphs.scale_about(1.16, (FAN_PIVOT[0], FAN_PIVOT[1], 0), morphs.falloff_mask(mesh, blades, (0, 0, 0), (1, 0, 0), 0.0, 0.1), axes=(1, 1, 0)),
            morphs.scale_about(1.16, (-FAN_PIVOT[0], FAN_PIVOT[1], 0), morphs.falloff_mask(mesh, blades, (0, 0, 0), (-1, 0, 0), 0.0, 0.1), axes=(1, 1, 0)),
            morphs.stretch_along((0, 1, 0), (0, 4.6, 0), 1.20, tails, ramp=(0.0, 0.5)),
        ],
        "Time": [  # speed: the fans sweep back, the beak and the tails stretch
            morphs.sweep((1, 0, 0), (0, 0, 0), (0, 0.22, 0), morphs.falloff_mask(mesh, blades, (0, 0, 0), (1, 0, 0), 0.0, 0.1)),
            morphs.sweep((-1, 0, 0), (0, 0, 0), (0, 0.22, 0), morphs.falloff_mask(mesh, blades, (0, 0, 0), (-1, 0, 0), 0.0, 0.1)),
            morphs.stretch_along((0, -1, 0), (0, -0.4, 0), 1.30, bodym, ramp=(0.0, 0.6)),
            morphs.stretch_along((0, 1, 0), (0, 4.6, 0), 1.12, tails, ramp=(0.0, 0.5)),
        ],
    }
    for name, defs in shapes.items():
        kb, mx, frac, moved = morphs.add_shape(mesh, name, defs)
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))

    # ------------------------------------------------------------------ POSES (world axes; nose -Y)
    def fans(l_rot, r_rot, hub=True):
        d = {}
        for tag, rot in (("L", l_rot), ("R", r_rot)):
            for (k, *_r) in FAN:
                d["b_SpinWing.%s.%03d" % (tag, k)] = rot
            if hub:
                d["b_SpinWing." + tag] = rot
        return d

    def tails_rot(rot, loc=(0, 0, 0)):
        return {"b_Tail%d.%s" % (k, t): {"rot": rot, "loc": loc} for k in (1, 2, 3) for t, _ in SIDES}

    def guns_rot(rot):
        return {"b_ShipGun%d.%s" % (k, t): rot for k in (1, 2) for t, _ in SIDES}

    def pose(p):
        d = {}
        speed = "Speed" in p
        if speed:
            d.update(fans((0, 0, 22), (0, 0, -22)))          # fold back
            d.update(tails_rot((0, 0, 0), (0, 0.35, 0)))      # tails streak aft
            d.update(guns_rot({"rot": (0, 0, 0), "loc": (0, -0.25, 0)}))
        if "PitchUp" in p:
            d.update(fans((-16, 0, 0), (-16, 0, 0)))
            d.update(tails_rot((-24, 0, 0)))
        elif "PitchDown" in p:
            d.update(fans((16, 0, 0), (16, 0, 0)))
            d.update(tails_rot((24, 0, 0)))
        if "YawLeft" in p or "TurnLeft" in p:
            d.update(fans((0, -10, -14), (0, -10, -14)))      # bank left and skid
            d.update(tails_rot((0, 0, 22)))
        elif "YawRight" in p or "TurnRight" in p:
            d.update(fans((0, 10, 14), (0, 10, 14)))
            d.update(tails_rot((0, 0, -22)))
        if p == "SpeedDown":
            d.update(fans((-6, 0, -20), (-6, 0, 20)))         # spread as an airbrake
            d.update(tails_rot((0, 0, 0), (0, -0.25, 0)))
        return d

    acts = {}
    for p in TAKES:
        acts[p] = takes.pose_take(arm, p + "_SparrowAnim", pose(p))
    acts["Idle.006"] = takes.pose_take(arm, "Idle_SparrowAnim.006", {})
    takes.clear_pose(arm)

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "zup", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [ARMATURE + "|" + p + "_SparrowAnim" for p in TAKES] + [ARMATURE + "|Idle_SparrowAnim.006"],
            "pose_takes": [ARMATURE + "|" + p + "_SparrowAnim" for p in TAKES],
            "shapes": ["Space", "Charge", "Time", "Mass"],
            "slots": list(SLOTS),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.03,
        },
    }
