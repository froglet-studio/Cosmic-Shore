"""
squirrel - the fleet's reference hull: a vaporwave arcade racer with a squirrel's head, a fan
of feather-plates for wings, four engine pods (two forward "paws", two aft) and a long chevron
tail that drifts behind it. Written IN PLACE over the artist file so the prefab, the animator
controller and the .meta's pinned clips keep binding:

LEGACY BINDINGS, not design: the file path, node / geometry / armature / bone names, slot order,
take names and file frame below are what this vessel's prefab, .meta and driver bind TODAY;
.claude/skills/vessel-model/references/FLEET_CONVENTION.md section 7 names the step that retires
each one. Everything under 'Design intent' and the morph / pose meanings follow the convention.


  file      Assets/_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx (same guid)
  armature  a_Squirrel_Armature            (take names are '<armature>|<action>')
  mesh node a_SquirrelShipMesh_nearfinal   (its SkinnedMeshRenderer is what the prefab strips)
  geometry  Plane.011
  takes     squirrelanim_idle/pitchup/pitchdown/yawleft/yawright/rollleft/rollright/speedup/speeddown
            (single-frame POSES the controller's 2D blend trees mix by Pitch/Yaw/Roll/Throttle)
  bones     the shipped 42, same names, same parenting (WingPieces under WholeWing, Tails under
            WholeTail), because the jets and the prefab's references hang off them by name
  slots     0 Domain, 1 Body, 2 Window, 3 Engine
  size      x +-2.25, y -1.9 .. +4.56 (nose -Y), z -0.69 .. +0.77 Blender units = Unity units
            (camera -17, tails authored at (+-4, 0, -12): the hull must stay this size)

Design intent (what makes it read as the Squirrel from the chase camera, 17 units back):
  - a HEAD: a faceted helmet with two swept ears and a sunken cyan visor - the one silhouette
    cue that says "squirrel" rather than "fighter"
  - a FAN of six feather plates each side, domain-coloured on top, sweeping back and down,
    so the wing reads as fur/feathers rather than a slab
  - four PODS with lime mouths: the forward pair are the paws it reaches with, the aft pair
    push; a domain band rings each
  - the TAIL: nine bevelled chevrons on a drooping arc, the longest feature, striped so motion
    reads along it when the tail curls in a turn
Element morphs: charge = the pods lengthen and their mouths flare, the ears sharpen; mass = the
helmet, body and belly fill out; space = the tail reaches further and the fan spreads wider;
time = the head and body stretch forward and the fan sweeps back - the silhouette of speed.
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


def build(ctx):
    f, rig, morphs, takes, mats = ctx.forms, ctx.rig, ctx.morphs, ctx.takes, ctx.materials
    parts = []

    # ------------------------------------------------------------------ HEAD
    # helmet: a faceted dome, nose at y=-1.35, back at y=-0.25; the visor is sunk into its front
    helmet = f.fuselage("bbone_UpperHelmet", 1.20, nose=(0, -1.42, 0.26), axis=(0, 1, 0), segments=28, stations=[
        (0.00, 0.10, 0.06, 0.08, 2.2, 2.2, 0.00),
        (0.12, 0.32, 0.22, 0.20, 2.6, 2.4, 0.05),
        (0.35, 0.44, 0.34, 0.24, 3.0, 2.6, 0.10),
        (0.65, 0.46, 0.36, 0.22, 3.2, 2.6, 0.10),
        (0.90, 0.38, 0.26, 0.16, 3.0, 2.4, 0.05),
        (1.00, 0.28, 0.14, 0.10, 2.6, 2.2, 0.00),
    ])
    f.paint(helmet, H)
    # the visor: the whole front of the dome above the jaw line, sunk in and glassed
    f.inset_faces(helmet, lambda c, n, i: n.z > -0.1 and n.y < 0.25 and -1.425 < c.y < -0.78 and abs(c.x) < 0.42, 0.025, depth=0.03, slot=W)
    # a domain crest along the top centreline behind the visor
    f.paint(helmet, D, lambda c, n, i: i == H and n.z > 0.7 and -0.70 < c.y < -0.30 and abs(c.x) < 0.09)
    f.bevel(helmet, width=0.012, segments=1, angle_deg=28)
    f.apply_modifiers(helmet)
    f.sharpen_by_angle(helmet, 38)
    parts.append(helmet)

    # chin / jaw under the helmet
    chin = f.fuselage("bbone_LowerHelmet", 0.95, nose=(0, -1.36, -0.02), axis=(0, 1, 0), segments=20, stations=[
        (0.00, 0.14, 0.06, 0.08, 2.4, 2.4, 0.00),
        (0.30, 0.40, 0.14, 0.18, 3.0, 2.4, 0.00),
        (0.70, 0.46, 0.16, 0.20, 3.2, 2.6, 0.00),
        (1.00, 0.40, 0.14, 0.14, 3.0, 2.4, 0.00),
    ])
    f.paint(chin, H)
    f.paint(chin, D, lambda c, n, i: n.z < -0.6 and -1.1 < c.y < -0.7)  # a domain jaw plate
    f.bevel(chin, width=0.01, segments=1, angle_deg=30)
    f.apply_modifiers(chin)
    f.sharpen_by_angle(chin, 40)
    parts.append(chin)

    # ears: two swept blades on the helmet crown, one part (the artist's single Ears bone)
    ears = []
    for tag, sx in SIDES:
        ear = f.plate("ear_" + tag, f.blade(0.40, 0.34, tip=0.25, base=1.0), thickness=0.06,
                      normal=(1, 0, 0), origin=(sx * 0.20, -0.70, 0.56), right=(0, -0.35, 0.94))
        f.paint(ear, H)
        f.paint(ear, D, lambda c, n, i, sx=sx: n.x * sx < -0.5)  # inner face wears the domain
        f.bevel(ear, width=0.012, segments=1, angle_deg=30)
        f.apply_modifiers(ear)
        ears.append(ear)
    ears_part = f.join(ears, "bbone_Ears", SLOTS)
    _retag(f, ears_part)
    parts.append(ears_part)

    # cheeks: curved plates either side of the jaw
    for tag, sx in SIDES:
        ch = f.plate("bbone_cheek." + tag, f.rounded_rect(0.46, 0.36, r=0.1), thickness=0.07,
                     normal=(sx, 0.0, 0.15), origin=(sx * 0.56, -0.86, -0.02), right=(0, 1, 0))
        f.paint(ch, D)
        f.paint(ch, H, lambda c, n, i, sx=sx: n.x * sx < 0.2)
        f.bevel(ch, width=0.012, segments=1, angle_deg=30)
        f.apply_modifiers(ch)
        parts.append(ch)

    # ------------------------------------------------------------------ BODY
    # the main body: from behind the helmet to the tail root, a flat-bellied cabin
    body = f.fuselage("abone_MAIN_Cockpit", 1.55, nose=(0, -1.10, 0.05), axis=(0, 1, 0), segments=28, stations=[
        (0.00, 0.26, 0.20, 0.14, 2.8, 2.6, 0.00),
        (0.18, 0.38, 0.33, 0.22, 3.0, 3.2, 0.03),
        (0.45, 0.41, 0.36, 0.26, 3.2, 3.6, 0.04),
        (0.75, 0.38, 0.30, 0.24, 3.0, 3.4, 0.02),
        (1.00, 0.30, 0.18, 0.18, 2.8, 3.0, -0.02),
    ])
    f.paint(body, H)
    # cockpit glass: a sunken strip on the top deck
    f.inset_faces(body, lambda c, n, i: n.z > 0.75 and -0.80 < c.y < -0.25 and abs(c.x) < 0.17, 0.02, depth=0.025, slot=W)
    # domain stripe down the spine
    f.paint(body, D, lambda c, n, i: i == H and n.z > 0.8 and -0.2 < c.y < 0.45 and abs(c.x) < 0.10)
    # panel lines: the flanks are cut into three sunken panels a side
    for (y0, y1) in ((-0.95, -0.55), (-0.45, -0.05), (0.05, 0.40)):
        f.inset_faces(body, lambda c, n, i, y0=y0, y1=y1: i == H and abs(n.x) > 0.55 and y0 < c.y < y1 and c.z > -0.05, 0.02, depth=0.012)
    f.bevel(body, width=0.012, segments=1, angle_deg=28)
    f.apply_modifiers(body)
    f.sharpen_by_angle(body, 38)
    parts.append(body)

    # belly keel
    belly = f.fuselage("bbone_LowerBody", 1.75, nose=(0, -0.58, -0.20), axis=(0, 1, 0), segments=20, stations=[
        (0.00, 0.22, 0.14, 0.10, 2.8, 2.2, 0.00),
        (0.30, 0.34, 0.20, 0.26, 3.4, 2.4, 0.00),
        (0.65, 0.34, 0.20, 0.30, 3.4, 2.4, 0.02),
        (1.00, 0.24, 0.12, 0.16, 3.0, 2.2, 0.06),
    ])
    f.paint(belly, H)
    f.paint(belly, D, lambda c, n, i: n.z < -0.85 and -0.2 < c.y < 0.9 and abs(c.x) < 0.12)  # keel stripe
    f.bevel(belly, width=0.01, segments=1, angle_deg=30)
    f.apply_modifiers(belly)
    f.sharpen_by_angle(belly, 40)
    parts.append(belly)

    # shoulder deck: a wide thin plate the wing fans grow from
    deck = f.fuselage("bbone_BodyWing", 2.30, nose=(0, -0.95, -0.02), axis=(0, 1, 0), segments=24, stations=[
        (0.00, 0.34, 0.06, 0.06, 4.0, 4.0, 0.00),
        (0.25, 0.62, 0.09, 0.08, 4.5, 4.5, 0.02),
        (0.50, 0.72, 0.10, 0.09, 4.5, 4.5, 0.00),
        (0.75, 0.64, 0.09, 0.08, 4.5, 4.5, -0.04),
        (1.00, 0.36, 0.05, 0.05, 4.0, 4.0, -0.10),
    ])
    f.paint(deck, H)
    f.paint(deck, D, lambda c, n, i: n.z > 0.8 and 0.25 < abs(c.x) < 0.58)
    f.bevel(deck, width=0.01, segments=1, angle_deg=30)
    f.apply_modifiers(deck)
    parts.append(deck)

    # ------------------------------------------------------------------ WINGS: six feathers a side
    # each row: (name index, root x, root y, root z, length, width, yaw deg (sweep back +), pitch deg (droop +), roll deg)
    feathers = [  # roots sit INSIDE the deck so every plate grows out of the hull; the six
        # overlap into ONE wing surface (yaw spread 6 -> 34 deg, broad chords), front plate highest
        (1, 0.50, -0.92, 0.20, 1.05, 0.50, 6, -8, 2),
        (2, 0.48, -0.58, 0.15, 1.10, 0.56, 12, -4, 4),
        (3, 0.50, -0.20, 0.09, 1.12, 0.60, 18, 2, 7),
        (4, 0.48, 0.20, 0.01, 1.00, 0.56, 24, 8, 10),
        (5, 0.44, 0.58, -0.10, 0.86, 0.46, 29, 14, 13),
        (6, 0.40, 0.92, -0.22, 0.70, 0.36, 34, 20, 15),
    ]
    for tag, sx in SIDES:
        for (k, rx, ry, rz, ln, wd, yaw, pitch, roll) in feathers:
            outline = f.feather(ln, wd, tip_round=0.7, root_width=wd * 0.45, segments=10, curve=0.02)
            # the plate lies flat (normal +Z), its length along +X (outboard), then is turned
            fe = f.plate("bbone_WingPiece%d.%s" % (k, tag), outline, thickness=0.06, normal=(0, 0, 1),
                         origin=(0, 0, 0), right=(1, 0, 0))
            f.paint(fe, D)
            f.paint(fe, H, lambda c, n, i: n.z < -0.4)                # dark underside
            f.paint(fe, H, lambda c, n, i: n.z > 0.4 and c.x < ln * 0.22)  # dark root cap on top
            f.bevel(fe, width=0.012, segments=2, angle_deg=30)
            f.apply_modifiers(fe)
            # orient: yaw back (about Z), droop (about Y when outboard is X), then mirror for side
            m = _rot_z(math.radians(-yaw * sx)) @ _rot_y(math.radians(pitch * sx)) @ _rot_x(math.radians(roll))
            m = _scale_x(sx) @ m if sx < 0 else m
            f.transform_mesh(fe, m)
            if sx < 0:
                _flip(fe)
            f.transform_mesh(fe, _translate(sx * rx, ry, rz))
            parts.append(fe)

    # ------------------------------------------------------------------ STRUTS (nothing floats)
    # forearms: from the shoulder deck out and forward to each front pod's base
    for tag, sx in SIDES:
        arm_ = f.tube_along("arm_" + tag, [(sx * 0.40, -0.55, 0.06), (sx * 1.10, -0.85, 0.16), (sx * 1.75, -1.05, 0.22), (sx * 1.92, -1.10, 0.24)],
                            [0.13, 0.12, 0.11, 0.09], segments=12)
        f.paint(arm_, H)
        f.paint(arm_, D, lambda c, n, i: n.z > 0.6)
        f.bevel(arm_, width=0.01, segments=1, angle_deg=30)
        f.apply_modifiers(arm_)
        parts.append(arm_)
        # hind legs: from the belly back and out to each aft pod
        leg = f.tube_along("leg_" + tag, [(sx * 0.26, 0.55, -0.18), (sx * 0.70, 0.75, -0.32), (sx * 1.12, 0.95, -0.40)],
                           [0.13, 0.12, 0.10], segments=12)
        f.paint(leg, H)
        f.paint(leg, D, lambda c, n, i: n.z > 0.6)
        f.bevel(leg, width=0.01, segments=1, angle_deg=30)
        f.apply_modifiers(leg)
        parts.append(leg)
    # tail spine: a tapered boom the chevrons ride on, following the droop
    spine = f.tube_along("bbone_WholeTail", [(0, 1.05, -0.08), (0, 1.60, -0.30), (0, 2.50, -0.46), (0, 3.40, -0.44), (0, 4.40, -0.26)],
                         [0.17, 0.14, 0.12, 0.09, 0.05], segments=12)
    f.paint(spine, H)
    f.paint(spine, D, lambda c, n, i: n.z > 0.7)
    f.bevel(spine, width=0.008, segments=1, angle_deg=30)
    f.apply_modifiers(spine)
    parts.append(spine)

    # ------------------------------------------------------------------ PODS
    # forward pods (the paws): mouth faces FORWARD (-Y). Base + tip + trim ring, three bones.
    for tag, sx in SIDES:
        cx, cz = sx * 1.92, 0.24
        base = f.lathe("bbone_FrontEngineBase." + tag,
                       [(0.0, 0.0), (0.17, 0.02), (0.25, 0.10), (0.27, 0.30), (0.30, 0.34), (0.30, 0.42), (0.27, 0.46), (0.27, 0.62), (0.22, 0.80), (0.0, 0.82)],
                       segments=22, axis=(0, 1, 0), origin=(cx, -1.52, cz))
        f.paint(base, H)
        f.paint(base, D, lambda c, n, i: -1.30 < c.y < -1.12 and abs(n.y) < 0.5)  # domain band
        tip = f.lathe("bbone_FrontEngineTip." + tag,
                      [(0.0, 0.0), (0.19, 0.0), (0.22, 0.08), (0.21, 0.30), (0.18, 0.34), (0.0, 0.34)],
                      segments=20, axis=(0, 1, 0), origin=(cx, -1.88, cz))
        f.paint(tip, H)
        # the lime mouth: the front cap faces, sunk in
        f.inset_faces(tip, lambda c, n, i: n.y < -0.8, 0.03, depth=0.05, slot=J)
        trim = f.lathe("bbone_FrontEngineTrim." + tag,
                       [(0.25, 0.0), (0.30, 0.04), (0.31, 0.22), (0.27, 0.30), (0.25, 0.30), (0.25, 0.0)],
                       segments=20, axis=(0, 1, 0), origin=(cx, -1.78, cz), cap=False)
        f.paint(trim, D)
        f.paint(trim, H, lambda c, n, i: n.y > 0.5 or (c.z < cz - 0.1))
        for p_ in (base, tip, trim):
            f.bevel(p_, width=0.01, segments=1, angle_deg=30)
            f.apply_modifiers(p_)
            f.sharpen_by_angle(p_, 40)
            parts.append(p_)

    # aft pods: mouths face BACK (+Y), slung under the wing root. Frame + inner nozzle.
    for tag, sx in SIDES:
        cx, cz = sx * 1.12, -0.40
        frame = f.lathe("bbone_BackEngineFrame." + tag,
                        [(0.0, 0.0), (0.18, 0.02), (0.27, 0.14), (0.29, 0.40), (0.32, 0.44), (0.32, 0.52), (0.29, 0.56), (0.29, 0.70), (0.26, 0.90), (0.22, 0.95), (0.0, 0.95)],
                        segments=22, axis=(0, 1, 0), origin=(cx, 0.68, cz))
        f.paint(frame, H)
        f.paint(frame, D, lambda c, n, i: 0.953 < c.y < 1.15 and abs(n.y) < 0.5)
        inner = f.lathe("bbone_BackEngineInner." + tag,
                        [(0.0, 0.0), (0.17, 0.0), (0.21, 0.10), (0.22, 0.22), (0.19, 0.30), (0.0, 0.30)],
                        segments=20, axis=(0, 1, 0), origin=(cx, 1.45, cz))
        f.paint(inner, H)
        f.inset_faces(inner, lambda c, n, i: n.y > 0.8, 0.03, depth=0.05, slot=J)
        for p_ in (frame, inner):
            f.bevel(p_, width=0.01, segments=1, angle_deg=30)
            f.apply_modifiers(p_)
            f.sharpen_by_angle(p_, 40)
            parts.append(p_)

    # ------------------------------------------------------------------ TAIL: nine chevrons
    tail_rows = [  # (name, y centre, width, z centre, depth)
        ("bbone_Tail1First", 1.43, 0.70, -0.33, 0.30), ("bbone_Tail2", 1.78, 0.87, -0.43, 0.32),
        ("bbone_Tail3", 2.14, 1.02, -0.51, 0.34), ("bbone_Tail4", 2.50, 1.10, -0.55, 0.36),
        ("bbone_Tail5", 2.85, 1.12, -0.56, 0.36), ("bbone_Tail6", 3.21, 1.08, -0.53, 0.35),
        ("bbone_Tail7", 3.56, 0.97, -0.47, 0.33), ("bbone_Tail8", 3.91, 0.80, -0.40, 0.31),
        ("bbone_Tail9Last", 4.25, 0.54, -0.30, 0.28),
    ]
    for idx, (name, yc, wd, zc, dp) in enumerate(tail_rows):
        # a crescent plate: horns trailing back (+Y), the band's belly forward; closely spaced
        outline = f.crescent(wd * 0.60, wd * 0.60 - dp * 1.05, span_deg=150, segments=14, tips_sharp=0.35)
        cv = f.plate(name, outline, thickness=0.20 if idx < 7 else 0.14, normal=(0, 0, 1),
                     origin=(0, yc + dp * 0.1, zc), right=(1, 0, 0))
        f.transform_mesh(cv, _rot_z(math.pi) @ _translate(0, 0, 0)) if False else None
        # the crescent opens toward -y by construction; flip it so the horns trail aft
        f.transform_mesh(cv, _translate(0, 2 * (yc + dp * 0.1), 0) @ _scale_y(-1))
        _flip(cv)
        f.paint(cv, H)
        f.paint(cv, D, lambda c, n, i: n.z > 0.5)                     # domain on top of every chevron
        if idx % 2 == 1:
            f.paint(cv, H, lambda c, n, i: n.z > 0.5 and abs(c.x) < wd * 0.18)  # alternate rib pattern
        f.bevel(cv, width=0.02, segments=2, angle_deg=30)
        f.apply_modifiers(cv)
        parts.append(cv)

    # ------------------------------------------------------------------ JOIN
    mesh = f.join(parts, MESH_NODE, SLOTS)
    mesh.data.name = GEOMETRY
    f.set_smooth(mesh, True)
    f.sharpen_by_angle(mesh, 42)
    _box_uv(mesh)
    ctx.log("squirrel joined: %d verts, %d tris" % (len(mesh.data.vertices), f.tri_count(mesh)))

    # ------------------------------------------------------------------ RIG: the shipped armature, reused
    # Names, order, hierarchy and every rest transform come from the artist file, so the four
    # id-mounted jets (bone-space offsets on BackEngineFrame/Inner and FrontEngineTip) keep
    # landing in the pod mouths and the editor tools written against this skeleton still apply.
    # rest_compare.py proves the written file's bones rest where the shipped ones do.
    arm = rig.import_shipped_armature(ctx.artist(OUTPUT))
    assert arm.name == ARMATURE, arm.name
    bone_names = [b.name for b in arm.data.bones]
    rig.skin_rigid(mesh, arm, group_to_bone={"arm_L": "bbone_WholeWing.L", "arm_R": "bbone_WholeWing.R",
                                             "leg_L": "bbone_BackEngineFrame.L", "leg_R": "bbone_BackEngineFrame.R"},
                   default_bone="abone_MAIN_Cockpit")
    ctx.log("rig: %d bones reused from the shipped file" % len(bone_names))
    bones = arm.data.bones  # for the mask builders below

    # ------------------------------------------------------------------ MORPHS
    ms = morphs.MaskSet(mesh)
    pods = ms.mask(*[b.name for b in bones if "Engine" in b.name])
    pod_mouths = ms.mask(*[b.name for b in bones if "EngineTip" in b.name or "EngineInner" in b.name or "EngineTrim" in b.name])
    head = ms.mask("bbone_UpperHelmet", "bbone_LowerHelmet", "bbone_cheek.L", "bbone_cheek.R")
    ears = ms.mask("bbone_Ears")
    hull = ms.mask("abone_MAIN_Cockpit", "bbone_LowerBody", "bbone_BodyWing")
    fan = ms.mask(*[b.name for b in bones if "WingPiece" in b.name])
    tail = ms.mask(*[b.name for b in bones if "Tail" in b.name and "Whole" not in b.name])
    shapes = {
        "Charge": [  # threat: pods lengthen and their mouths flare; the ears sharpen upward
            morphs.stretch_along((0, -1, 0), (0, -0.76, 0), 1.35, ms.mask("bbone_FrontEngineTip.L", "bbone_FrontEngineTip.R", "bbone_FrontEngineTrim.L", "bbone_FrontEngineTrim.R"), ramp=(0.0, 0.3)),
            morphs.stretch_along((0, 1, 0), (0, 0.68, 0), 1.30, ms.mask("bbone_BackEngineInner.L", "bbone_BackEngineInner.R"), ramp=(0.0, 0.3)),
            morphs.scale_about(1.35, (0, 0, 0.24), pod_mouths, axes=(0, 0, 1)),
            morphs.scale_about(1.18, (0, 0, 0), pod_mouths, axes=(1, 0, 0)),
            morphs.stretch_along((0, 0, 1), (0, 0, 0.45), 1.45, ears, ramp=(0.0, 0.1)),
        ],
        "Mass": [  # volume: head, body and belly fill out; the fan plates and pods thicken
            morphs.scale_about(1.22, (0, 0, 0.02), hull, axes=(1, 0, 1)),
            morphs.inflate(0.10, hull), morphs.inflate(0.11, head), morphs.inflate(0.035, fan),
            morphs.scale_about(1.12, (0, 0, 0), pods, axes=(1, 0, 1)),
        ],
        "Space": [  # reach: the tail reaches further back and the fan spreads wider
            morphs.stretch_along((0, 1, 0), (0, 1.25, 0), 1.22, tail, ramp=(0.0, 0.4)),
            morphs.scale_about(1.28, (0, 0, 0), fan, axes=(1, 0, 0)),
        ],
        "Time": [  # rate: the head and body stretch forward, the fan and tail sweep back
            morphs.stretch_along((0, -1, 0), (0, -0.60, 0), 1.22, ms.mask("bbone_UpperHelmet", "bbone_LowerHelmet", "bbone_cheek.L", "bbone_cheek.R", "bbone_Ears", "abone_MAIN_Cockpit"), ramp=(0.0, 0.5)),
            morphs.sweep((1, 0, 0), (0, 0, 0), (0, 0.30, 0), fan),  # x -> +y lean (both sides: |x|)
            morphs.sweep((-1, 0, 0), (0, 0, 0), (0, 0.30, 0), morphs.falloff_mask(mesh, fan, (0, 0, 0), (-1, 0, 0), 0.0, 0.01)),
            morphs.stretch_along((0, 1, 0), (0, 1.25, 0), 1.10, tail, ramp=(0.0, 0.4)),
        ],
    }
    for name, defs in shapes.items():
        kb, mx, frac, moved = morphs.add_shape(mesh, name, defs)
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))

    # ------------------------------------------------------------------ POSES (deg about WORLD X=pitch, Y=roll, Z=yaw; nose -Y)
    # The tail bones are SIBLINGS under bbone_WholeTail (not a chain), so the gross swing lives on
    # WholeTail and the per-chevron curl rides on top. The pods are siblings of the wing: `follow`
    # makes them ride the wing's bank so the forearm struts stay in their sockets. Magnitudes are
    # big on purpose: the controller's top blend tree HALVES every pose (dossier 3.2); the artist's
    # takes drop the tail tip 1.1-1.4 units for pitch and whip it 2.2-2.5 sideways for yaw.
    PODS = ("bbone_FrontEngineBase", "bbone_FrontEngineTip", "bbone_FrontEngineTrim")

    def tail_curl(sign, amount, axis=0):
        out = {}
        for i, (name, *_r) in enumerate(tail_rows):
            v = [0, 0, 0]
            v[axis] = sign * amount * (0.25 + 0.75 * i / 8.0)
            out[name] = tuple(v)
        return out

    def wing_bank(l_rot, r_rot, pods=True):
        """The whole wing (shoulder, feathers) and, riding it, the front pods."""
        out = {}
        for tag, rot in (("L", l_rot), ("R", r_rot)):
            for k in range(1, 7):
                out["bbone_WingPiece%d.%s" % (k, tag)] = rot
            out["bbone_WholeWing." + tag] = rot
            if pods:
                for pb in PODS:
                    out["%s.%s" % (pb, tag)] = takes.follow(arm, "bbone_WholeWing." + tag, "%s.%s" % (pb, tag), rot)
        return out

    def pose(p):
        if p == "idle":
            return {}
        if p == "pitchup":    # nose up: the whole tail drops (-X about its root), chevrons curl further, head lifts, wing noses up
            d = {"bbone_WholeTail": (-30, 0, 0)}
            d.update(tail_curl(-1, 14))
            d.update(wing_bank((-16, 0, 0), (-16, 0, 0)))
            d.update({"bbone_UpperHelmet": (10, 0, 0), "bbone_LowerHelmet": (10, 0, 0), "bbone_Ears": (14, 0, 0)})
            return d
        if p == "pitchdown":
            d = {"bbone_WholeTail": (30, 0, 0)}
            d.update(tail_curl(1, 14))
            d.update(wing_bank((16, 0, 0), (16, 0, 0)))
            d.update({"bbone_UpperHelmet": (-10, 0, 0), "bbone_LowerHelmet": (-10, 0, 0), "bbone_Ears": (-14, 0, 0)})
            return d
        if p in ("yawleft", "yawright"):
            # nose -Y, pilot's left +X: a right turn noses toward -X, so the tail swings to the OUTSIDE,
            # +X, which is a NEGATIVE rotation about Z for a tail that points +Y (measured on the artist's
            # yawright: tail tip dx +2.46)
            s = -1 if p == "yawright" else 1
            d = {"bbone_WholeTail": (0, 0, s * 40)}
            d.update(tail_curl(s, 12, axis=2))
            d.update(wing_bank((0, 0, s * 12), (0, 0, s * 12), pods=False))
            d.update({"bbone_UpperHelmet": (0, 0, -s * 9), "bbone_LowerHelmet": (0, 0, -s * 9), "bbone_Ears": (0, 0, -s * 10)})
            return d
        if p in ("rollleft", "rollright"):  # a bank about the hull's long axis: wings and pods go round, the tail twists
            s = 1 if p == "rollright" else -1
            d = wing_bank((0, s * 32, 0), (0, s * 32, 0))
            d["bbone_WholeTail"] = (0, s * 18, 0)
            d.update(tail_curl(s, 12, axis=1))
            d.update({"bbone_cheek.L": (0, 0, s * 10), "bbone_cheek.R": (0, 0, s * 10)})
            return d
        if p == "speedup":   # everything streamlines: the fan folds back, the paws reach, the tail straightens
            d = wing_bank((0, 0, 18), (0, 0, -18), pods=False)
            d.update({"%s.%s" % (pb, t): {"loc": (0, -dy, 0)} for t in ("L", "R") for pb, dy in zip(PODS, (0.10, 0.16, 0.14))})
            d["bbone_Ears"] = (-10, 0, 0)
            d["bbone_WholeTail"] = (6, 0, 0)
            d.update(tail_curl(1, 4))
            return d
        if p == "speeddown":  # the fan spreads as an airbrake, the paws pull in, the tail flares
            d = wing_bank((-6, 0, -14), (-6, 0, 14), pods=False)
            d.update({"%s.%s" % (pb, t): {"loc": (0, dy, 0)} for t in ("L", "R") for pb, dy in zip(PODS, (0.08, 0.12, 0.10))})
            d["bbone_Ears"] = (6, 0, 0)
            d["bbone_WholeTail"] = (-8, 0, 0)
            d.update(tail_curl(-1, 6))
            return d
        return {}

    acts = takes.standard_pose_set(arm, pose, prefix=TAKE_PREFIX)

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "zup", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [ARMATURE + "|" + TAKE_PREFIX + p for p in takes.STANDARD_POSES],
            "pose_takes": [ARMATURE + "|" + TAKE_PREFIX + p for p in takes.STANDARD_POSES],
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.025,
        },
    }


# ------------------------------------------------------------------ small matrix helpers

def _rot_x(a):
    from mathutils import Matrix
    return Matrix.Rotation(a, 4, "X")


def _rot_y(a):
    from mathutils import Matrix
    return Matrix.Rotation(a, 4, "Y")


def _rot_z(a):
    from mathutils import Matrix
    return Matrix.Rotation(a, 4, "Z")


def _scale_x(sx):
    from mathutils import Matrix, Vector
    return Matrix.Scale(sx, 4, Vector((1, 0, 0)))


def _scale_y(sy):
    from mathutils import Matrix, Vector
    return Matrix.Scale(sy, 4, Vector((0, 1, 0)))


def _translate(x, y, z):
    from mathutils import Matrix, Vector
    return Matrix.Translation(Vector((x, y, z)))


def _flip(obj):
    """A mirrored part has inverted winding; put the normals back outward."""
    import bmesh
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.reverse_faces(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    me.update()


def _retag(f, obj):
    """After joining sub-pieces into one part, the part's own group is its name."""
    for vg in list(obj.vertex_groups):
        obj.vertex_groups.remove(vg)
    f.tag_group(obj)


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
