"""
goby - a NEW hull (no artist file) for the Goby, the front half of the Goby and Pistol crew vessel
(VesselClassType 18, .claude/skills/duo-vessel/references/designs/goby-and-pistol.md). Until now the Goby
flew in the Squirrel's model (Goby.prefab is a Squirrel clone: its flight, abilities, Animator and jets).

The brief: the goby is the shrimp's EYES - it stands lookout at the burrow mouth while the nearly blind
pistol shrimp keeps an antenna on it - and in the game it zips ahead, almost impossible to catch. A
spaceship inspired by a goby, never the fish (Garrett, 2026-10-18). Clade -> ship:
  high-set eyes      -> twin LOOKOUT sensor turrets on top of the head: glass domes on domain collars
  broad blunt head   -> a wide rounded prow shell over a jaw; the mouth is an intake scoop that glows
  gill covers        -> armoured cheek scoops with domain louvre slits
  first dorsal fin   -> a tall swept sensor sail with a dark leading spar
  big pectoral fans  -> two fans of three broad overlapping ray plates, a thruster pod at each fan's tip
  pelvic sucker disc -> a ventral docking clamp ring under the chest (where the Pistol rides)
  pelvic / anal fins -> three swept keel plates per side under the rear body
  long body          -> a sleeved fuselage that tapers into the tail, banded in the domain colour
  second dorsal/anal -> long low fins along the tail's top and bottom
  rounded caudal fin -> a horizontal rounded fan of domain plates on raised ribs
  speed              -> twin rear nacelles flanking the tail root

Built ON the Squirrel's rig, which is what the Goby's prefab binds: the armature, every bone (names, rest
poses, hierarchy), the mesh node and geometry names, the slot order and names, the shape names and the take
names are the Squirrel's, exactly. Unity derives a model's sub-object ids from type + name
(fileIdsGeneration 2; one mesh id is shared by thirteen TriBlock files, measured 2026-10-18), so this file
under its own guid binds every reference Goby.prefab makes into the Squirrel model - the skinned renderer,
the Animator, the root bone, the four jet hosts - by swapping the model's guid in the prefab alone. The
Squirrel's Animator controller keeps driving it: the takes here are the Squirrel's own, retargeted bone for
bone (takes.retarget_from), and every part is skinned to the Squirrel bone whose part sat where it sits, so
each take moves the goby the way it moved the squirrel (the tail chain swims, the fans bank, the pods tilt).
  file      Assets/_Models/Vessel Models/Goby.fbx (new guid; its .meta is the Squirrel's with that guid)
  frame     preset "zup": nose -Y, up +Z, .L on +X in the Blender frame
  nodes     mesh `a_SquirrelShipMesh_nearfinal` (geometry `Plane.011`), armature `a_Squirrel_Armature`
  bones     the Squirrel's 42, at its rest; jets hang on bbone_BackEngineFrame/Inner.L/R (rear nacelles)
            and bbone_FrontEngineTip.L/R (the fan pods' tips), so the nacelle mouths sit there
  size      the Squirrel's drawn hull within a few percent (x +-2.25, y -1.94 .. 4.74, z -0.36 .. 0.77):
            the colliders, jets, skimmer and camera were tuned to it
  slots     0 Domain (fans, sails, fins, caudal fan, bands, eye collars, louvres), 1 Body, 2 Window (eyes,
            canopy), 3 Engine (nacelle and pod mouths, the intake throat)
  shapes    Time = the fans sweep back and the tail draws out (speed); Mass = the head, cheeks and body
            swell (volume); Space = the fans, sails and caudal fan spread wide (reach); Charge = the lookout
            turrets rise and the gill scoops flare open (the primer's targeting)
  takes     the Squirrel's nine poses (a_Squirrel_Armature|squirrelanim_*), retargeted unchanged
"""

import math
import os
from _common import box_uv, finish

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))))

NAME = "Goby"
OUTPUT = "Assets/_Models/Vessel Models/Goby.fbx"
RIG_FROM = "Assets/_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx"   # the rig and takes the prefab binds

ARMATURE = "a_Squirrel_Armature"
MESH_NODE = "a_SquirrelShipMesh_nearfinal"
GEOMETRY = "Plane.011"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3
TAKE_PREFIX = "squirrelanim_"

# the Squirrel's drawn hull (Blender frame), which the colliders, jets and camera were tuned to
SIZE_REF = ((-2.248, -1.944, -0.364), (2.248, 4.744, 0.766))

# the Squirrel's tail parts' centres (part_map.py on the shipped file): the rear body bends between them
TAIL_BONES = ["bbone_Tail1First", "bbone_Tail2", "bbone_Tail3", "bbone_Tail4", "bbone_Tail5",
              "bbone_Tail6", "bbone_Tail7", "bbone_Tail8", "bbone_Tail9Last"]
TAIL_Y = [1.499, 1.875, 2.246, 2.616, 2.985, 3.363, 3.737, 4.105, 4.409]
TAIL_Z = [0.027, 0.014, -0.007, -0.013, -0.022, -0.034, -0.054, -0.073, -0.070]
BODY_ANCHOR_Y = 1.00     # in front of it the rear body is the mid-body's (bbone_LowerBody, the root)

NOSE_Y = -1.55           # the prow's tip
NECK_Y = -0.42           # where the head meets the chest
CHEST_END = 0.40         # chest (cockpit) -> mid-body
TAIL_TIP = 4.74


def superellipse_pts(w, ht, hb, n=28, p_top=2.6, p_bot=2.2, start=0.0, end=2 * math.pi):
    """A closed or partial superellipse outline in (x, z): `start`..`end` radians, top exponent p_top, bottom p_bot."""
    pts = []
    steps = n
    for i in range(steps + (0 if end - start >= 2 * math.pi - 1e-6 else 1)):
        t = start + (end - start) * i / steps
        c, s = math.cos(t), math.sin(t)
        if s >= 0:
            x = w * math.copysign(abs(c) ** (2 / p_top), c)
            z = ht * abs(s) ** (2 / p_top)
        else:
            x = w * math.copysign(abs(c) ** (2 / p_bot), c)
            z = -hb * abs(s) ** (2 / p_bot)
        pts.append((x, z))
    return pts


def blade_outline(length, width, tip=0.55, root=0.55, n=10):
    """A broad rounded ray plate along +X: a narrow root, the widest third of the way out, a round tip."""
    top, bot = [], []
    for i in range(n + 1):
        t = i / n
        x = length * t
        swell = math.sin(math.pi * min(1.0, t / 0.85) * 0.5) if t < 0.85 else math.cos((t - 0.85) / 0.15 * math.pi * 0.5)
        hw = 0.5 * width * (root + (1 - root) * swell) if t < 0.85 else 0.5 * width * swell
        top.append((x, max(hw, 0.004)))
        bot.append((x, -max(hw * (1 - 0.15 * t), 0.004)))
    return top + list(reversed(bot))[1:-1]


def build(ctx):
    import bpy  # noqa: F401
    import bmesh
    from mathutils import Vector, Matrix
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    Station = f.Station
    parts = []

    # ------------------------------------------------------------------ THE SQUIRREL'S RIG, as Goby.prefab binds it
    rig_from = os.path.join(REPO, RIG_FROM)
    arm, donor = rig.import_shipped_model(rig_from)
    assert arm.name == ARMATURE, arm.name
    baked = rig.bake_parked_pose(donor, arm, deform_mesh=True)   # the shipped file's rest is already the drawn pose: expect none
    donor_data = donor.data
    bpy.data.objects.remove(donor, do_unlink=True)                # its skin and slot colours were read; the goby is new geometry
    bpy.data.meshes.remove(donor_data)                            # and free its datablock, which holds the name Plane.011 the file must carry
    bpy.context.view_layer.update()
    bone_names = [b.name for b in arm.data.bones]
    ctx.log("the Squirrel's rig: %d bones, parked pose baked on %d" % (len(bone_names), len(baked)))

    def head(b):
        return Vector(rig.bone_world_head(arm, b))

    def new(obj, slot=H):
        f.ensure_slots(obj, SLOTS)
        f.paint(obj, slot)
        parts.append(obj)
        return obj

    # ------------------------------------------------------------------ HEAD: prow shell over a jaw (UpperHelmet / LowerHelmet)
    # (y, half-width, top, bottom): broad and blunt, as a goby's head is
    HEAD = [(NOSE_Y, 0.12, 0.08, 0.07), (NOSE_Y + 0.05, 0.26, 0.17, 0.14), (NOSE_Y + 0.16, 0.38, 0.25, 0.20),
            (-1.18, 0.47, 0.31, 0.25), (-0.95, 0.51, 0.34, 0.28), (-0.70, 0.51, 0.35, 0.29), (NECK_Y - 0.02, 0.49, 0.35, 0.30)]
    upper = f.loft("bbone_UpperHelmet", [Station((0, y, 0.0), superellipse_pts(w, ht, hb, n=24, start=-0.12, end=math.pi + 0.12))
                                          for (y, w, ht, hb) in HEAD])
    new(upper)
    # the prow's domain blaze: a sunk panel down the top of the head, between and in front of the turrets
    f.inset_faces(upper, lambda c, n, k: n.z > 0.75 and abs(c.x) < 0.10 and -1.40 < c.y < -0.55, 0.012, depth=0.010, slot=D)
    # the seam with the jaw: a domain trim line low on each flank
    f.inset_faces(upper, lambda c, n, k: abs(n.x) > 0.80 and c.z < 0.06 and -1.25 < c.y < NECK_Y - 0.08 and k == H, 0.010, depth=0.006, slot=D)
    finish(f, upper, bevel_width=0.010, sharp_deg=40)

    jaw = f.loft("bbone_LowerHelmet", [Station((0, y + 0.02, 0.0), superellipse_pts(w * 0.98, ht, hb, n=24, start=math.pi - 0.10, end=2 * math.pi + 0.10))
                                       for (y, w, ht, hb) in HEAD[1:]])
    new(jaw)
    # the mouth: a wide intake scoop across the front of the jaw, its throat glowing (Engine: it flashes with the boost)
    f.inset_faces(jaw, lambda c, n, k: n.y < -0.45 and c.z < -0.02 and abs(c.x) < 0.30, 0.020, depth=0.05, slot=J)
    finish(f, jaw, bevel_width=0.010, sharp_deg=40)

    # ------------------------------------------------------------------ THE LOOKOUT: twin sensor turrets (Ears)
    for tag, sx in (("L", 1), ("R", -1)):
        cx, cy = sx * 0.21, -0.92
        collar = f.lathe("eye_collar.%s" % tag, [(0.005, 0.0), (0.135, 0.0), (0.145, 0.06), (0.150, 0.16), (0.138, 0.20), (0.020, 0.20)],
                         segments=20, axis=(0, 0, 1), origin=(cx, cy, 0.20))
        new(collar)
        f.paint(collar, D, lambda c, n, k: c.z > 0.33)                               # a domain ring round each turret's rim
        finish(f, collar, bevel_width=0.008, sharp_deg=38)
        dome = f.sphere("eye_dome.%s" % tag, 0.128, center=(cx, cy - 0.01, 0.40), segments=20, rings=10, scale=(1.0, 1.05, 0.85))
        new(dome, W)
        # a dark sensor band across each dome: the turret looks forward and out
        f.paint(dome, H, lambda c, n, k: abs(n.z) < 0.18)
    # ------------------------------------------------------------------ GILL SCOOPS (cheeks): armoured plates with louvres
    for tag, sx in (("L", 1), ("R", -1)):
        outline = [(-0.98, -0.16), (-0.98, 0.10), (-0.86, 0.20), (-0.62, 0.22), (-0.46, 0.14), (-0.40, 0.00), (-0.46, -0.16), (-0.66, -0.24), (-0.88, -0.24)]
        cheek = f.plate("bbone_cheek.%s" % tag, outline, 0.05, normal=(sx, 0, 0), origin=(sx * 0.515, 0, -0.03), right=(0, 1, 0))
        new(cheek)
        # three louvre slits along the back of the plate
        f.inset_faces(cheek, lambda c, n, k, sx=sx: n.x * sx > 0.7 and any(abs(c.y - yy) < 0.025 for yy in (-0.70, -0.60, -0.50)) and abs(c.z) < 0.14,
                      0.008, depth=0.012, slot=D)
        finish(f, cheek, bevel_width=0.010, sharp_deg=38)
        f.transform_mesh(cheek, Matrix.Translation(Vector((sx * 0.515, -0.69, -0.03))) @ Matrix.Rotation(math.radians(-14 * sx), 4, "Z")
                         @ Matrix.Translation(Vector((-sx * 0.515, 0.69, 0.03))))

    # ------------------------------------------------------------------ CHEST (Cockpit): the forward fuselage and the canopy
    CHEST = [(NECK_Y - 0.12, 0.48, 0.34, 0.29), (NECK_Y + 0.05, 0.49, 0.35, 0.31), (-0.05, 0.48, 0.34, 0.32), (CHEST_END, 0.46, 0.33, 0.31)]
    chest = f.loft("abone_MAIN_Cockpit", [Station((0, y, 0.0), superellipse_pts(w, ht, hb, n=28)) for (y, w, ht, hb) in CHEST])
    new(chest)
    # the canopy behind the turrets: glass sunk into the top of the chest
    f.inset_faces(chest, lambda c, n, k: n.z > 0.80 and abs(c.x) < 0.17 and NECK_Y < c.y < -0.02, 0.020, depth=0.014, slot=W)
    # a domain collar band where the head meets the chest
    f.paint(chest, D, lambda c, n, k: NECK_Y - 0.10 < c.y < NECK_Y + 0.00 and n.z > -0.3)
    finish(f, chest, bevel_width=0.010, sharp_deg=40)

    # ------------------------------------------------------------------ MID-BODY (LowerBody, the root bone)
    MID = [(CHEST_END - 0.08, 0.455, 0.325, 0.305), (0.80, 0.43, 0.31, 0.29), (BODY_ANCHOR_Y + 0.15, 0.41, 0.30, 0.28)]
    mid = f.loft("bbone_LowerBody", [Station((0, y, 0.0), superellipse_pts(w, ht, hb, n=28)) for (y, w, ht, hb) in MID])
    new(mid)
    f.paint(mid, D, lambda c, n, k: 0.62 < c.y < 0.74 and n.z > -0.35)                # a body band
    finish(f, mid, bevel_width=0.010, sharp_deg=40)

    # the docking clamp under the chest (the pelvic sucker disc): where the Pistol rides
    clamp = f.lathe("clamp", [(0.03, 0.0), (0.26, 0.0), (0.27, 0.035), (0.20, 0.07), (0.10, 0.07), (0.06, 0.04)], segments=24,
                    axis=(0, 0, -1), origin=(0, -0.08, -0.25))
    new(clamp)
    f.paint(clamp, D, lambda c, n, k: n.z < -0.6 and 0.12 < math.hypot(c.x, c.y + 0.08) < 0.22)
    finish(f, clamp, bevel_width=0.008, sharp_deg=38)

    # ------------------------------------------------------------------ FIRST DORSAL: a swept sensor sail (BodyWing)
    sail_outline = [(-0.30, 0.0), (0.62, 0.0), (0.48, 0.14), (0.20, 0.32), (-0.02, 0.44), (-0.14, 0.45), (-0.24, 0.30)]
    sail = f.plate("bbone_BodyWing", sail_outline, 0.045, normal=(1, 0, 0), origin=(0, 0.20, 0.27), right=(0, 1, 0))
    new(sail, D)
    f.paint(sail, H, lambda c, n, k: c.z < 0.31)                                        # the sail's root is hull
    finish(f, sail, bevel_width=0.010, sharp_deg=38)
    spar = f.tube_along("sail_spar", [(0, -0.12, 0.27), (0, -0.06, 0.52), (0, 0.05, 0.71)], [0.035, 0.028, 0.016], segments=8)
    new(spar)

    # ------------------------------------------------------------------ REAR BODY: one sleeved, banded loft that bends between the tail bones
    rear_st = []
    for (y, r) in ((BODY_ANCHOR_Y - 0.05, 0.40), (1.45, 0.375), (1.90, 0.34), (2.35, 0.295), (2.80, 0.245), (3.25, 0.195), (3.65, 0.15), (3.98, 0.11), (4.12, 0.06)):
        z = float(sum(TAIL_Z[i] for i in range(len(TAIL_Y)) if abs(TAIL_Y[i] - y) < 0.4) / max(1, sum(1 for t in TAIL_Y if abs(t - y) < 0.4)))
        rear_st.append(Station((0, y, z), superellipse_pts(r, r * 0.76, r * 0.72, n=24)))
    rear = f.loft("rear", rear_st)
    new(rear)
    # the goby's bands: domain rings round the top and flanks, dark underneath
    for yy in (1.55, 2.25, 2.95, 3.55):
        f.paint(rear, D, lambda c, n, k, yy=yy: abs(c.y - yy) < 0.09 and n.z > -0.4)
    finish(f, rear, bevel_width=0.008, sharp_deg=40)

    # second dorsal and anal fins: long low plates along the rear body's top and bottom
    fin2 = f.plate("fin2", [(1.05, 0.0), (2.55, 0.0), (2.35, 0.13), (1.80, 0.22), (1.25, 0.24), (1.08, 0.12)], 0.035,
                   normal=(1, 0, 0), origin=(0, 0, 0.22), right=(0, 1, 0))
    new(fin2, D)
    f.paint(fin2, H, lambda c, n, k: c.z < 0.25)
    finish(f, fin2, bevel_width=0.008, sharp_deg=38)
    anal = f.plate("anal", [(1.25, 0.0), (2.40, 0.0), (2.30, -0.10), (1.85, -0.17), (1.40, -0.18), (1.27, -0.08)], 0.035,
                   normal=(1, 0, 0), origin=(0, 0, -0.20), right=(0, 1, 0))
    new(anal, D)
    f.paint(anal, H, lambda c, n, k: c.z > -0.23)
    finish(f, anal, bevel_width=0.008, sharp_deg=38)

    # the rounded caudal fan: domain plates on three raised dark ribs, horizontal (the chase camera sees its face)
    fan_outline = [(0.0, 3.86), (0.15, 3.86), (0.34, 4.12), (0.50, 4.40), (0.56, 4.58), (0.50, 4.69), (0.33, 4.74), (0.12, 4.745),
                   (-0.12, 4.745), (-0.33, 4.74), (-0.50, 4.69), (-0.56, 4.58), (-0.50, 4.40), (-0.34, 4.12), (-0.15, 3.86)]
    fan = f.plate("fan", fan_outline, 0.05, normal=(0, 0, 1), origin=(0, 0, TAIL_Z[-1] + 0.01), right=(1, 0, 0))
    new(fan, D)
    f.paint(fan, H, lambda c, n, k: c.y < 3.98)
    finish(f, fan, bevel_width=0.010, sharp_deg=38)
    for k_, ang in enumerate((-32, 0, 32)):
        a = math.radians(ang)
        p0 = Vector((0, 3.90, TAIL_Z[-1] + 0.04))
        p1 = p0 + Vector((math.sin(a), math.cos(a), 0)) * 0.78
        rib = f.tube_along("fan_rib.%d" % k_, [tuple(p0), tuple(p1)], [0.03, 0.016], segments=6)
        new(rib)

    # ------------------------------------------------------------------ PECTORAL FANS (WingPiece1-3) and their pods (FrontEngine*)
    ROOT = Vector((0.44, -0.55, 0.06))
    fan_plates = {"bbone_WingPiece1.L": (40, 1.25, 0.86, 9, 0.020), "bbone_WingPiece2.L": (9, 1.20, 0.90, 12, 0.045),
                  "bbone_WingPiece3.L": (-26, 1.05, 0.80, 7, 0.0)}
    for bone, (ang, length, width, dihedral, dz) in fan_plates.items():
        pl = f.plate(bone, blade_outline(length, width, root=0.45), 0.045, normal=(0, 0, 1), origin=(0, 0, 0), right=(1, 0, 0))
        new(pl, H)
        # a domain panel sunk into a dark frame on both faces (the fleet idiom: domain as plates, not as a fill)
        f.inset_faces(pl, lambda c, n, k: abs(n.z) > 0.9, 0.055, depth=0.010, slot=D)
        finish(f, pl, bevel_width=0.010, sharp_deg=38)
        f.transform_mesh(pl, Matrix.Translation(ROOT + Vector((0, 0, dz))) @ Matrix.Rotation(math.radians(-ang), 4, "Z")
                         @ Matrix.Rotation(math.radians(-dihedral), 4, "Y"))
        # a dark leading spar along each plate's front edge
        a = math.radians(ang)
        d = Vector((math.cos(a), -math.sin(a), math.sin(math.radians(dihedral))))
        sp = f.tube_along("spar." + bone[len("bbone_"):], [tuple(ROOT + Vector((0, 0, dz + 0.03))), tuple(ROOT + Vector((0, 0, dz + 0.03)) + d * length * 0.92)],
                          [0.030, 0.014], segments=6)
        new(sp)
    shoulder = f.lathe("shoulder.L", [(0.02, 0.0), (0.16, 0.0), (0.17, 0.08), (0.15, 0.16), (0.02, 0.16)], segments=16,
                       axis=(1, 0, 0), origin=(ROOT.x - 0.08, ROOT.y, ROOT.z))
    new(shoulder)
    f.paint(shoulder, D, lambda c, n, k: 0.06 < c.x - (ROOT.x - 0.08) < 0.10)
    finish(f, shoulder, bevel_width=0.008, sharp_deg=38)

    # the fan pod at WingPiece1's tip: nacelle (Base), its fairing ring and fin (Trim), the vectoring nozzle (Tip, the jets' host)
    PX, PZ = 1.92, 0.02
    base = f.lathe("bbone_FrontEngineBase.L", [(0.07, 0.0), (0.17, 0.10), (0.19, 0.30), (0.18, 0.60), (0.12, 0.74), (0.06, 0.76)],
                   segments=16, axis=(0, 1, 0), origin=(PX, -1.50, PZ))
    new(base)
    f.paint(base, J, lambda c, n, k: c.y > -0.80 and n.y > 0.5)                          # the aft mouth
    finish(f, base, bevel_width=0.010, sharp_deg=40)
    strut = f.tube_along("pod_strut.L", [(1.10, -1.15, 0.10), (1.80, -1.28, 0.05)], [0.075, 0.08], segments=10)
    new(strut)
    trim = f.lathe("bbone_FrontEngineTrim.L", [(0.20, 0.0), (0.225, 0.04), (0.225, 0.16), (0.20, 0.20)], segments=16, axis=(0, 1, 0), origin=(PX, -1.52, PZ))
    new(trim, D)
    finish(f, trim, bevel_width=0.008, sharp_deg=38)
    pod_fin = f.plate("pod_fin.L", [(-1.40, 0.0), (-1.00, 0.0), (-1.10, 0.20), (-1.30, 0.24)], 0.035, normal=(1, 0, 0), origin=(PX, 0, PZ + 0.16), right=(0, 1, 0))
    new(pod_fin, D)
    finish(f, pod_fin, bevel_width=0.008, sharp_deg=38)
    tip = f.lathe("bbone_FrontEngineTip.L", [(0.02, -0.05), (0.10, 0.0), (0.155, 0.10), (0.17, 0.30), (0.16, 0.36)], segments=16,
                  axis=(0, 1, 0), origin=(PX, -1.86, PZ))
    new(tip)
    f.paint(tip, J, lambda c, n, k: c.y < -1.80)                                         # the vectoring nozzle's glow
    finish(f, tip, bevel_width=0.008, sharp_deg=40)

    # ------------------------------------------------------------------ REAR NACELLES (BackEngineFrame / Inner: the jets' hosts)
    NX, NZ = 1.12, -0.10
    nac = f.lathe("bbone_BackEngineFrame.L", [(0.04, 0.0), (0.12, 0.08), (0.165, 0.30), (0.17, 0.62), (0.16, 0.78)], segments=16,
                  axis=(0, 1, 0), origin=(NX, 0.74, NZ))
    new(nac)
    f.paint(nac, D, lambda c, n, k: (0.80 < c.y < 0.88) or (1.15 < c.y < 1.29 and n.z > -0.3))
    finish(f, nac, bevel_width=0.010, sharp_deg=40)
    pylon = f.tube_along("pylon.L", [(0.30, 1.02, NZ + 0.02), (NX - 0.10, 1.04, NZ)], [0.07, 0.06], segments=8)
    new(pylon)
    mouth = f.lathe("bbone_BackEngineInner.L", [(0.155, 0.0), (0.165, 0.04), (0.155, 0.12), (0.12, 0.13), (0.08, 0.08), (0.02, 0.08)], segments=16,
                    axis=(0, 1, 0), origin=(NX, 1.52, NZ))
    new(mouth, J)
    f.paint(mouth, H, lambda c, n, k: math.hypot(c.x - NX, c.z - NZ) > 0.135)
    finish(f, mouth, bevel_width=0.006, sharp_deg=40)

    # ------------------------------------------------------------------ KEEL FINS (WingPiece4-6): swept plates under the rear body
    # the pelvic fins as a swept stub wing of three overlapping plates that carries the nacelles (nothing hangs)
    keel_plates = {"bbone_WingPiece4.L": ((0.30, 0.50, -0.06), -14, 0.95, 0.44), "bbone_WingPiece5.L": ((0.30, 0.82, -0.08), -10, 0.95, 0.40),
                   "bbone_WingPiece6.L": ((0.30, 1.12, -0.09), -32, 0.80, 0.30)}
    for bone, (root, ang, length, width) in keel_plates.items():
        pl = f.plate(bone, blade_outline(length, width, root=0.85), 0.04, normal=(0, 0, 1), origin=(0, 0, 0), right=(1, 0, 0))
        new(pl, H)
        # dark plates, a domain stripe sunk along each trailing edge
        f.inset_faces(pl, lambda c, n, k: abs(n.z) > 0.9, 0.03, depth=0.0, slot=H)
        f.paint(pl, D, lambda c, n, k, w=width: abs(n.z) < 0.9 and c.y > 0.12 * w)
        finish(f, pl, bevel_width=0.008, sharp_deg=38)
        f.transform_mesh(pl, Matrix.Translation(Vector(root)) @ Matrix.Rotation(math.radians(-ang), 4, "Z") @ Matrix.Rotation(math.radians(5), 4, "Y"))

    # ------------------------------------------------------------------ MIRROR every .L part to .R, onto the R bone nearest its mirror
    def r_bone(lb):
        if not lb.endswith(".L"):
            return None
        hm = head(lb)
        hm = Vector((-hm.x, hm.y, hm.z))
        cands = [b for b in bone_names if b.endswith(".R") and b.split(".")[0].rstrip("0123456789") == lb.split(".")[0].rstrip("0123456789")]
        return min(cands, key=lambda b: (head(b) - hm).length)
    for p in list(parts):
        if p.name.endswith(".L") and not p.name.startswith("eye_"):     # the eyes and cheeks are built per side already
            if p.name.startswith("bbone_cheek"):
                continue
            rb = r_bone(p.name) if p.name in bone_names else p.name[:-2] + ".R"
            parts.append(f.mirror_part(p, rb, axis="X"))
    # sub-parts that ride a bone without being named for it (the .R twins ride the R bone nearest their mirror)
    g2b = {"eye_collar.L": "bbone_Ears", "eye_collar.R": "bbone_Ears", "eye_dome.L": "bbone_Ears", "eye_dome.R": "bbone_Ears",
           "clamp": "bbone_LowerBody", "sail_spar": "bbone_BodyWing",
           "shoulder.L": "bbone_WingPiece2.L", "pod_strut.L": "bbone_FrontEngineBase.L", "pod_fin.L": "bbone_FrontEngineTrim.L",
           "pylon.L": "bbone_BackEngineFrame.L"}
    for lb in fan_plates:
        g2b["spar." + lb[len("bbone_"):]] = lb
    for k in [k for k in g2b if k.endswith(".L") and not k.startswith("eye_")]:
        g2b[k[:-2] + ".R"] = r_bone(g2b[k])

    # ------------------------------------------------------------------ JOIN, n-gons out
    for p in parts:
        f.set_smooth(p, True)
    mesh = f.join(parts, MESH_NODE, SLOTS)
    mesh.data.name = GEOMETRY
    assert mesh.data.name == GEOMETRY, mesh.data.name
    stray = mesh.vertex_groups.get(MESH_NODE)
    if stray:
        mesh.vertex_groups.remove(stray)
    bm = bmesh.new()
    bm.from_mesh(mesh.data)
    big = [fc for fc in bm.faces if len(fc.verts) > 4]
    if big:
        bmesh.ops.triangulate(bm, faces=big, quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.to_mesh(mesh.data)
    bm.free()
    f.set_smooth(mesh, True)
    f.sharpen_by_angle(mesh, 40)
    box_uv(mesh)
    ctx.log("goby joined: %d verts, %d tris (n-gons triangulated: %d); groups %s" % (
        len(mesh.data.vertices), f.tri_count(mesh), len(big), sorted(vg.name for vg in mesh.vertex_groups)))

    # masks BEFORE skinning (skinning folds the sub-part groups into their bones)
    ms = morphs.MaskSet(mesh)
    head_m = ms.mask("bbone_UpperHelmet", "bbone_LowerHelmet")
    cheeks = {s: ms.mask("bbone_cheek.%s" % s) for s in ("L", "R")}
    eyes = ms.mask("eye_collar.L", "eye_collar.R", "eye_dome.L", "eye_dome.R")
    body = ms.mask("abone_MAIN_Cockpit", "bbone_LowerBody", "clamp")
    rear_m = ms.mask("rear", "fin2", "anal")
    fan_m = ms.mask("fan", "fan_rib.0", "fan_rib.1", "fan_rib.2")
    sail_m = ms.mask("bbone_BodyWing", "sail_spar")
    pect = {}
    for s in ("L", "R"):
        names = [g for g in ms.names if (g.startswith("bbone_WingPiece") or g.startswith("spar.WingPiece")) and g.endswith(".%s" % s)
                 and int(g.split("WingPiece")[1][0]) <= 3] + ["shoulder.%s" % s]
        pect[s] = ms.mask(*names)
    pods = {s: ms.mask(*[g for g in ms.names if ("FrontEngine" in g or "pod_" in g) and g.endswith(s)]) for s in ("L", "R")}
    keels = ms.mask(*[g for g in ms.names if g.startswith("bbone_WingPiece") and int(g[len("bbone_WingPiece")]) >= 4])
    # which R bones the mirrored pectoral plates landed on (the artist swapped WingPiece5/6 on the R side): the masks above
    # read the PART names, so a pectoral plate is any WingPiece part within the fan's reach of the shoulder
    for s, sx in (("L", 1), ("R", -1)):
        extra = [w if (v.co.x * sx > 0.3 and v.co.y < 0.15 and v.co.z > -0.08) else 0.0 for w, v in zip(ms.mask(*[g for g in ms.names if "WingPiece" in g]), mesh.data.vertices)]
        pect[s] = [max(a, b) for a, b in zip(pect[s], extra)]
    nacelles = ms.mask(*[g for g in ms.names if "BackEngine" in g or g.startswith("pylon")])

    # ------------------------------------------------------------------ RIG: the rear body bends between the Squirrel's tail bones
    anchors = [(BODY_ANCHOR_Y, "bbone_LowerBody")] + list(zip(TAIL_Y, TAIL_BONES))
    for gname in ("rear", "fin2", "anal", "fan", "fan_rib.0", "fan_rib.1", "fan_rib.2"):
        sg = mesh.vertex_groups.get(gname)
        if sg is None:
            continue
        for v in mesh.data.vertices:
            if not any(g.group == sg.index for g in v.groups):
                continue
            y = (mesh.matrix_world @ v.co).y
            if y <= anchors[0][0]:
                pairs = [(anchors[0][1], 1.0)]
            elif y >= anchors[-1][0]:
                pairs = [(anchors[-1][1], 1.0)]
            else:
                k = max(i for i in range(len(anchors)) if anchors[i][0] <= y)
                (ya, ba), (yb, bb) = anchors[k], anchors[k + 1]
                t = (y - ya) / (yb - ya)
                pairs = [(ba, 1.0 - t), (bb, t)]
            for bname, wgt in pairs:
                g = mesh.vertex_groups.get(bname) or mesh.vertex_groups.new(name=bname)
                g.add([v.index], wgt, "REPLACE")
        mesh.vertex_groups.remove(sg)
    rig.skin_rigid(mesh, arm, group_to_bone=g2b, default_bone="bbone_LowerBody", bake_park=False)
    skinned = {mesh.vertex_groups[g.group].name for v in mesh.data.vertices for g in v.groups if g.weight > 0}
    unskinned = [b for b in bone_names if b not in skinned and b not in ("bbone_WholeWing.L", "bbone_WholeWing.R", "bbone_WholeTail")]
    if unskinned:
        raise AssertionError("bones that skin nothing (Unity would strip them, and a take on them would move nothing): %s" % unskinned)

    # ------------------------------------------------------------------ MORPHS (Squirrel names, capitalised; 4-12% of the ~8.1 diagonal)
    me = mesh.data

    def where(mask, pred):
        return [w if pred(v.co) else 0.0 for w, v in zip(mask, me.vertices)]
    shapes = {
        "Time": [   # speed: the fans sweep back and fold flat, the pods follow, the tail draws out long
            morphs.sweep((1, 0, 0), (ROOT.x, 0, 0), (0, 0.42, 0), pect["L"]),
            morphs.sweep((-1, 0, 0), (-ROOT.x, 0, 0), (0, 0.42, 0), pect["R"]),
            morphs.translate((0, 0.45, 0), pods["L"]), morphs.translate((0, 0.45, 0), pods["R"]),
            morphs.stretch_along((0, 1, 0), (0, BODY_ANCHOR_Y, 0), 1.10, [max(a, b) for a, b in zip(rear_m, fan_m)]),
        ],
        "Mass": [   # volume: the head, cheeks and body swell about the spine
            morphs.scale_about(1.48, (0, -0.85, 0), head_m, axes=(1, 0, 1)),
            morphs.scale_about(1.45, (0, 0.2, 0), body, axes=(1, 0, 1)),
            morphs.scale_about(1.38, (0, 2.2, 0), rear_m, axes=(1, 0, 1)),
            morphs.inflate(0.09, [max(x, y_) for x, y_ in zip(head_m, body)]),
            morphs.translate((0.34, 0.02, 0), cheeks["L"]), morphs.translate((-0.34, 0.02, 0), cheeks["R"]),
            morphs.scale_about(1.45, (1.12, 0, -0.10), nacelles_side(nacelles, me, 1), axes=(1, 0, 1)),
            morphs.scale_about(1.45, (-1.12, 0, -0.10), nacelles_side(nacelles, me, -1), axes=(1, 0, 1)),
            morphs.translate((0.16, 0, 0), nacelles_side(nacelles, me, 1)), morphs.translate((-0.16, 0, 0), nacelles_side(nacelles, me, -1)),
        ],
        "Space": [  # reach: the fans and keels spread wide, the sail and caudal fan open out
            morphs.scale_about(1.32, tuple(ROOT), pect["L"], axes=(1, 1, 0)),
            morphs.scale_about(1.32, (-ROOT.x, ROOT.y, ROOT.z), pect["R"], axes=(1, 1, 0)),
            morphs.translate((0.30, 0, 0), pods["L"]), morphs.translate((-0.30, 0, 0), pods["R"]),
            morphs.scale_about(1.30, (0, 0.5, -0.2), keels, axes=(1, 1, 1)),
            morphs.stretch_along((0, 0, 1), (0, 0, 0.27), 1.35, sail_m),
            morphs.scale_about(1.30, (0, 3.90, 0), fan_m, axes=(1, 1, 0)),
        ],
        "Charge": [  # the primer's targeting: the lookout turrets rise on their mounts, the gill scoops flare open
            morphs.translate((0, -0.04, 0.30), eyes),
            morphs.translate((0.16, 0.06, 0), where(cheeks["L"], lambda co: co.y > -0.75)),
            morphs.translate((-0.16, 0.06, 0), where(cheeks["R"], lambda co: co.y > -0.75)),
            morphs.translate((0, -0.10, -0.06), where(head_m, lambda co: co.z < -0.05 and co.y < -1.1)),
        ],
    }
    for name in ("Time", "Mass", "Space", "Charge"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))

    # ------------------------------------------------------------------ TAKES: the Squirrel's own nine, retargeted unchanged
    acts = takes.retarget_from(arm, rig_from)
    takes.clear_pose(arm)
    ctx.log("takes: %d retargeted from the Squirrel (%s)" % (len(acts), ", ".join(sorted(acts))))

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "zup", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [ARMATURE + "|" + TAKE_PREFIX + p for p in takes.STANDARD_POSES],
            "pose_takes": [ARMATURE + "|" + TAKE_PREFIX + p for p in takes.STANDARD_POSES],
            "take_parity": False,    # new geometry on the Squirrel's bones: the takes ARE the Squirrel's (retargeted), the parts are new
            "shapes": ["Time", "Mass", "Space", "Charge"],
            "slots": list(SLOTS),
            "size_ref_blender": SIZE_REF, "size_tol": (0.06, 0.06, 0.15),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.035,
        },
    }


def nacelles_side(mask, me, sx):
    return [w if v.co.x * sx > 0 else 0.0 for w, v in zip(mask, me.vertices)]
