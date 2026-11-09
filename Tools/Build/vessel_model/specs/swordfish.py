"""
swordfish - a NEW hull for the Swordfish (VesselClassType 30, R_VesselActions/SWORDFISH.md), on the RHINO'S RIG.

Garrett, 2026-11-08: "the swordfish should look like a swordfish (and a space ship of course) ... its sword should be
the sword of the sword fish, so in the home position ... the model should look the most sword fish like." Until now
the Swordfish flew the Rhino's model (the split of 2026-11-07 cloned Rhino.prefab).

THE SWORD IS THE BILL. The bill is not modelled here: the sword is the vessel's own blade (the ForceFieldSkimmer the
triggers pose, ShieldSwipeActionExecutor), and its mesh is the bill (`Tools/Build/author_swordfish_bill.py` writes
`SwordfishBill.asset`, the flat tapering rostrum the blade draws). At rest the blade lies straight ahead with its
hilt in this hull's snout, so the hull and the blade read as one swordfish; a trigger swings it out to that side,
both stand it straight up. So the snout here is FLAT and as wide as the bill's root: the upper jaw runs on into it.

Clade -> ship (every part answers "what is this on the ship?"):
  bill                -> the SWORD (the blade, above), seated in a flat snout socket
  robust torpedo body -> a deep, round gunmetal hull, deepest a third of the way back, tapering to a slim peduncle
  countershading      -> a DOMAIN saddle along the dorsal ridge, nape to peduncle (the dark back of the fish)
  huge eye            -> a big sensor eye in a bezel on each cheek; a glass canopy on the forehead
  gill slits          -> three glowing vents behind each eye (Engine)
  short lower jaw     -> a floating chin plate under the bill's root
  first dorsal fin    -> the tall rigid SICKLE CREST standing just behind the head, a sunk domain panel in it
  pectoral fins       -> long stiff sickles low on the flanks: an inner plate (wing1.*) and its swept tip (wing2.*),
                         floating clear of each other, both swinging with the sword (RhinoAnimation's sword layer)
  speed               -> two thruster pods slung over the pectorals (jet.*), glowing mouths aft
  anal / 2nd dorsal   -> a falcate anal fin and two finlets near the tail
  peduncle keels      -> one broad keel each side of the tail root
  lunate tail         -> the big crescent vane, horns in the domain, floating behind the peduncle

Built ON the Rhino's rig, which is what Swordfish.prefab binds (the prefab is a clone of Rhino.prefab): the armature
`rhino`, all twelve bones (names, rest poses, hierarchy), the mesh node `fusalage.002`, the geometry `Circle.004`, the
slot names and order, the shape names and order and the nine take names are the Rhino rig's exactly (PIPELINE "The
borrowed-rig path"). So `Swordfish.fbx`, under its own guid, binds every reference the prefab's nested model makes by a
guid swap alone, and RhinoAnimation keeps driving it: each pectoral plate follows its prefab wing part
('Wing front left/right'), each pod its engine part, and the sword layer swings both wings with the blade.
  file      Assets/_Models/Vessel Models/Swordfish.fbx (new guid; its .meta is the Rhino rig's with that guid)
  frame     preset "yup" (the Rhino rig's): nose -Y, up +Z, .l on +X in the Blender frame; Unity (x, y, z) under
            'Rhino_Test (1)' = Blender (-x, -z - 1.5545, y), so the snout tip (0, -9.4, 0.3) is Unity (0, 0.3, 7.85)
  bones     fuse (body, crest, fins, tail); wing1.l/.r (pectoral inner plates); wing2.l/.r (their sickle tips);
            jet.l/.r (the pods, on the Rhino pods' axes); root and winghold* skin nothing, as in the Rhino rig
  size      a swordfish, not a rhino: 23 units snout to tail fork (the Rhino 20.4), 19.6 across the pectorals
            (16.0), 14.6 crest to tail horn (15.3); declared in size_ref_blender
  slots     0 accent (domain), 1 BASE (body), 2 windsheild (eye, canopy), 3 N (gill vents, pod mouths)
  shapes    charge = the weapon: the crest stands taller and the snout socket widens;
            mass   = volume: the body deepens and the pods swell;
            space  = reach: the pectorals reach out along their span and the crescent spreads;
            time   = speed: the crest rakes back, the pectorals sweep, the crescent rakes
  takes     the Rhino rig's nine (rhino|rhino_*), retargeted unchanged: RhinoAnimation is code puppetry, so no
            Animator plays them; the names are kept because the .meta pins them
"""

import math
import os
from _common import box_uv

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))))

NAME = "Swordfish"
OUTPUT = "Assets/_Models/Vessel Models/Swordfish.fbx"
RIG_FROM = "Assets/_Models/Vessel Models/rhino_shapekey_with_animations.fbx"   # the rig the prefab binds

ARMATURE = "rhino"
MESH_NODE = "fusalage.002"
GEOMETRY = "Circle.004"
SLOTS = ("accent.005", "BASE.005", "windsheild.005", "N.005")
SHAPES = ("mass", "charge", "space", "time")    # the Rhino rig's names, case and order
D, H, W, J = 0, 1, 2, 3

# The layout, in the Blender frame: nose -Y, up +Z, the pilot's left +X.
SNOUT_Y = -9.40          # the snout tip, where the sword's hilt seats
SNOUT_Z = 0.30
BODY_END = 9.20          # the peduncle's end
TAIL_HUB = (0.0, 9.55, 0.10)
TAIL_END = 13.70
PEC_ROOT = (3.15, -3.55, -1.25)   # the pectoral's leading-edge root (the .l side)
PEC_SPAN, PEC_DIHEDRAL, PEC_SWEEP = 4.10, -16.0, 1.70
TIP_SPAN, TIP_DIHEDRAL, TIP_SWEEP = 3.30, -24.0, 3.10
POD_X, POD_Z, POD_Y0, POD_LEN, POD_R = 5.365, 0.464, -0.95, 4.80, 0.78   # on the Rhino pods' axes
CREST_Y0, CREST_LEN, CREST_H = -5.10, 3.70, 5.40

# (y, half-width, half-height top, half-height bottom, centre lift)
BODY_STATIONS = [
    (SNOUT_Y, 1.46, 0.42, 0.40, SNOUT_Z),
    (-8.90, 1.62, 0.78, 0.64, 0.30),
    (-8.00, 1.92, 1.42, 1.08, 0.26),
    (-6.80, 2.30, 2.12, 1.58, 0.22),
    (-5.20, 2.62, 2.66, 1.98, 0.18),
    (-3.40, 2.78, 2.92, 2.16, 0.15),
    (-1.20, 2.70, 2.82, 2.08, 0.12),
    (1.20, 2.40, 2.46, 1.84, 0.10),
    (3.60, 1.92, 1.92, 1.46, 0.10),
    (6.00, 1.24, 1.24, 0.98, 0.10),
    (8.00, 0.66, 0.66, 0.54, 0.10),
    (BODY_END, 0.44, 0.44, 0.40, 0.10),
]


def _lerp_station(y, k):
    st = BODY_STATIONS
    if y <= st[0][0]:
        return st[0][k]
    for a, b in zip(st, st[1:]):
        if y <= b[0]:
            t = (y - a[0]) / (b[0] - a[0])
            return a[k] + (b[k] - a[k]) * t
    return st[-1][k]


def half_w(y):
    return _lerp_station(y, 1)


def mid_z(y):
    return _lerp_station(y, 4)


def top_z(y):
    return mid_z(y) + _lerp_station(y, 2)


def bot_z(y):
    return mid_z(y) - _lerp_station(y, 3)


def _smooth(a, b, x):
    if b == a:
        return 1.0 if x >= b else 0.0
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def build(ctx):
    import bpy
    import bmesh
    from mathutils import Vector, Matrix
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes

    # ------------------------------------------------------------------ THE RHINO'S RIG, as Swordfish.prefab binds it
    rig_from = os.path.join(REPO, RIG_FROM)
    arm, donor = rig.import_shipped_model(rig_from)
    assert arm.name == ARMATURE, arm.name
    assert [m.name for m in donor.data.materials] == list(SLOTS), [m.name for m in donor.data.materials]
    takes.clear_pose(arm)            # the Rhino rig's BIND is its rest (specs/rhino.py); nothing parked to bake
    donor_data = donor.data
    bpy.data.objects.remove(donor, do_unlink=True)   # its slot colours stay in bpy.data; the swordfish is new geometry
    bpy.data.meshes.remove(donor_data)               # and its datablock goes, which holds the name the file must carry
    bpy.context.view_layer.update()
    bone_names = [b.name for b in arm.data.bones]
    ctx.log("the Rhino's rig: %d bones (%s)" % (len(bone_names), ", ".join(bone_names)))

    parts = []

    def tag(obj, pid):
        me = obj.data
        a = me.attributes.get("pid") or me.attributes.new("pid", "FLOAT", "POINT")
        a.data.foreach_set("value", [float(pid)] * len(me.vertices))
        return obj

    def piece(obj, slot, pid, bevel=0.0):
        f.ensure_slots(obj, SLOTS)
        if slot is not None:
            f.paint(obj, slot)
        if bevel > 0:
            f.bevel(obj, width=bevel, segments=1, angle_deg=30)
            f.apply_modifiers(obj)
        tag(obj, pid)
        return obj

    def bone_part(objs, name):
        o = objs[0] if len(objs) == 1 else f.join(objs, name, SLOTS)
        o.name = name + "_part"
        for vg in list(o.vertex_groups):
            o.vertex_groups.remove(vg)
        f.tag_group(o, name)
        return o

    def vplate(name, outline, thickness, origin):
        """A plate in the body's mid plane (x = 0): outline points are (along +Y from origin, up +Z), clockwise."""
        return f.plate(name, list(reversed(outline)), thickness, normal=(1, 0, 0), origin=origin, right=(0, 1, 0))

    body_parts = []

    # ------------------------------------------------------------------ THE BODY (fuse)
    length = BODY_END - SNOUT_Y
    dense = []
    for k in range(41):
        t = k / 40
        y = SNOUT_Y + t * length
        # the snout is a flat jaw (low exponents top and bottom read as a beak, not a dome)
        flat = 1.0 - _smooth(-9.4, -7.6, y)
        dense.append((t, half_w(y), _lerp_station(y, 2), _lerp_station(y, 3),
                      2.3 + 1.4 * flat, 2.2 + 1.4 * flat, mid_z(y)))
    body = f.fuselage("body", length, nose=(0, SNOUT_Y, 0.0), axis=(0, 1, 0), segments=32, stations=dense)
    f.ensure_slots(body, SLOTS)
    f.paint(body, H)

    def band(c):
        mid = mid_z(c.y)
        hh = _lerp_station(c.y, 2) if c.z >= mid else _lerp_station(c.y, 3)
        return (c.z - mid) / max(hh, 1e-3)
    # the dark back: a domain saddle along the dorsal ridge from the nape to the peduncle
    f.paint(body, D, lambda c, n, i: band(c) > 0.90 and abs(c.x) < 0.75 * half_w(c.y) and -7.4 < c.y < 8.4)
    f.inset_faces(body, lambda c, n, i: i == D, 0.04, depth=0.035, slot=D)
    body_parts.append(piece(body, None, 1))

    # the chin: a short floating lower jaw under the bill's root
    chin = f.lathe("chin", [(0.0, 0.0), (0.18, 0.12), (0.46, 0.80), (0.58, 1.55), (0.0, 1.62)], segments=14,
                   axis=(0, 1, 0), origin=(0, SNOUT_Y + 0.05, bot_z(SNOUT_Y + 0.9) - 0.32))
    for v in chin.data.vertices:   # flatten it into a jaw plate
        v.co.x *= 1.55
        v.co.z = (v.co.z - (bot_z(SNOUT_Y + 0.9) - 0.32)) * 0.62 + (bot_z(SNOUT_Y + 0.9) - 0.32)
    body_parts.append(piece(chin, H, 2, bevel=0.01))

    # the eyes: a big sensor in a bezel on each cheek; three gill vents behind each
    for sx in (1, -1):
        y0 = -7.05
        cx = sx * half_w(y0) * 0.88
        cz = mid_z(y0) + 0.30 * _lerp_station(y0, 2)
        axis = Vector((sx * 0.94, -0.30, 0.16)).normalized()
        bez = f.lathe("bezel", [(0.0, -0.08), (0.52, -0.08), (0.57, 0.0), (0.49, 0.08), (0.0, 0.08)], segments=20,
                      axis=tuple(axis), origin=(cx, y0, cz))
        body_parts.append(piece(bez, H, 3))
        eye = f.sphere("eye", 0.40, center=tuple(Vector((cx, y0, cz)) + axis * 0.10), segments=18, rings=10)
        body_parts.append(piece(eye, W, 3))
        for k in range(3):
            y = -5.70 + k * 0.42
            gx = half_w(y) * 0.985
            lv = f.box("gill", (0.12, 0.13, 1.25), center=(0, 0, 0))
            f.transform_mesh(lv, Matrix.Translation(Vector((sx * gx, y, mid_z(y) - 0.15 * _lerp_station(y, 3))))
                             @ Matrix.Rotation(math.radians(sx * 12), 4, "Y") @ Matrix.Rotation(math.radians(-16), 4, "X"))
            body_parts.append(piece(lv, J, 4, bevel=0.012))

    # the canopy on the forehead, ahead of the crest
    cy = -6.55
    frame = f.sphere("canopyframe", 1.0, center=(0, cy + 0.10, top_z(cy) - 0.36), segments=22, rings=11,
                     scale=(0.90, 1.70, 0.58))
    body_parts.append(piece(frame, H, 5))
    can = f.sphere("canopy", 1.0, center=(0, cy, top_z(cy) - 0.24), segments=22, rings=11, scale=(0.76, 1.48, 0.56))
    body_parts.append(piece(can, W, 5))

    # the first dorsal: the tall rigid SICKLE CREST, floating just clear of the ridge
    croot = max(top_z(CREST_Y0 + s * CREST_LEN / 6) for s in range(7)) + 0.22
    L, Hc = CREST_LEN, CREST_H
    lead = [(0.0, 0.0), (0.10, Hc * 0.24), (0.38, Hc * 0.50), (0.95, Hc * 0.72), (1.85, Hc * 0.90),
            (3.05, Hc * 0.99), (3.85, Hc)]
    trail = [(3.55, Hc * 0.90), (3.05, Hc * 0.70), (2.80, Hc * 0.46), (2.88, Hc * 0.22), (L, 0.0)]
    crest = vplate("crest", lead + trail, 0.16, (0, CREST_Y0, croot))
    f.ensure_slots(crest, SLOTS)
    f.paint(crest, H)
    f.inset_faces(crest, lambda c, n, i: abs(n.x) > 0.9 and c.z > croot + 0.9, 0.22, depth=0.03, slot=D)
    body_parts.append(piece(crest, None, 6, bevel=0.012))

    # the finlets: a small second dorsal and second anal near the tail; the falcate anal fin under the rear body
    d2 = vplate("dorsal2", [(0.0, 0.0), (0.25, 0.55), (0.80, 0.78), (0.70, 0.30), (1.00, 0.0)], 0.09,
                (0, 7.10, top_z(7.6) + 0.14))
    body_parts.append(piece(d2, D, 7, bevel=0.008))
    a2 = vplate("anal2", [(0.0, 0.0), (1.00, 0.0), (0.70, -0.30), (0.80, -0.70), (0.25, -0.50)], 0.09,
                (0, 7.10, bot_z(7.6) - 0.14))
    body_parts.append(piece(a2, H, 7, bevel=0.008))
    an = vplate("anal", [(0.0, 0.0), (2.10, 0.0), (1.85, -0.40), (1.95, -1.20), (2.40, -2.05), (1.40, -1.55),
                         (0.45, -0.80)], 0.12, (0, 3.70, bot_z(4.6) - 0.20))
    body_parts.append(piece(an, H, 7, bevel=0.01))

    # the peduncle keels: one broad keel each side of the tail root
    for sx in (1, -1):
        y0, y1 = 7.30, 9.50
        hw0 = half_w(8.4)
        kl = f.plate("keel", [(0.0, -0.18), (0.0, 0.18), (y1 - y0, 0.05), (y1 - y0, -0.05)][::-1], 0.62,
                     normal=(sx, 0, 0), origin=(sx * (hw0 + 0.52), y0, mid_z(8.4)), right=(0, 1, 0))
        body_parts.append(piece(kl, H, 8, bevel=0.02))

    # the lunate tail: a big crescent floating behind the peduncle, on a hub; horns in the domain
    flen = TAIL_END - TAIL_HUB[1]
    clead = [(flen * 0.10, 0.80), (flen * 0.30, 2.30), (flen * 0.55, 3.90), (flen * 0.80, 5.20), (flen, 6.00)]
    ctrail = [(flen * 0.82, 4.70), (flen * 0.62, 3.20), (flen * 0.48, 1.55), (flen * 0.42, 0.0)]
    cres = ([(0.0, -0.36), (0.0, 0.36)] + clead + ctrail + [(x, -z) for (x, z) in reversed(ctrail[:-1])]
            + [(x, -z) for (x, z) in reversed(clead)])
    vane = vplate("crescent", cres, 0.16, (0, TAIL_HUB[1], TAIL_HUB[2]))
    f.ensure_slots(vane, SLOTS)
    f.paint(vane, H)
    f.paint(vane, D, lambda c, n, i: abs(c.z - TAIL_HUB[2]) > 3.5)
    body_parts.append(piece(vane, None, 9))
    hub = f.lathe("hub", [(0.0, -0.05), (0.40, -0.05), (0.46, 0.25), (0.36, 0.62), (0.0, 0.70)], segments=16,
                  axis=(0, 1, 0), origin=(0, TAIL_HUB[1] - 0.10, TAIL_HUB[2]))
    body_parts.append(piece(hub, H, 9))
    parts.append(bone_part(body_parts, "fuse"))

    # ------------------------------------------------------------------ PECTORALS: inner plates (wing1) and sickle tips (wing2)
    span_dir = {}
    tip_root = {}
    for side, sx in (("l", 1), ("r", -1)):
        root = Vector((sx * PEC_ROOT[0], PEC_ROOT[1], PEC_ROOT[2]))
        inner = f.wing("pec", root=tuple(root), span=PEC_SPAN, chord_root=2.70, chord_tip=1.45, sweep=PEC_SWEEP,
                       dihedral_deg=PEC_DIHEDRAL, thickness=0.10, sections=5, segments=16, side=sx, taper_power=1.0,
                       tip_round=False)
        f.ensure_slots(inner, SLOTS)
        f.paint(inner, H)
        parts.append(bone_part([piece(inner, None, 10, bevel=0.008)], "wing1." + side))
        d = math.radians(PEC_DIHEDRAL)
        span_dir[sx] = Vector((sx * math.cos(d), 0.0, math.sin(d)))
        # the sickle tip floats a gap beyond the plate's tip, continuing its leading edge
        le_tip = root + span_dir[sx] * PEC_SPAN + Vector((0, PEC_SWEEP, 0))
        tr = le_tip + span_dir[sx] * 0.36 + Vector((0, 0.10, 0))
        tip_root[sx] = tr
        outer = f.wing("pectip", root=tuple(tr), span=TIP_SPAN, chord_root=1.30, chord_tip=0.30, sweep=TIP_SWEEP,
                       dihedral_deg=TIP_DIHEDRAL, thickness=0.09, sections=6, segments=16, side=sx, taper_power=0.75)
        f.ensure_slots(outer, SLOTS)
        f.paint(outer, D)
        parts.append(bone_part([piece(outer, None, 11, bevel=0.006)], "wing2." + side))

        # the pod over the pectoral: a thruster nacelle on the Rhino pod's axis, its mouth glowing aft
        x = sx * POD_X
        y_end = POD_Y0 + POD_LEN
        prof = [(0.0, 0.0), (0.30, 0.12), (0.62, 0.75), (POD_R, 2.0), (POD_R * 0.96, 3.6), (POD_R * 0.86, POD_LEN - 0.06),
                (POD_R * 0.62, POD_LEN - 0.06), (POD_R * 0.56, POD_LEN - 0.30), (0.0, POD_LEN - 0.30)]
        nac = f.lathe("pod", prof, segments=18, axis=(0, 1, 0), origin=(x, POD_Y0, POD_Z))
        f.ensure_slots(nac, SLOTS)
        f.paint(nac, H)
        f.paint(nac, D, lambda c, n, i: POD_Y0 + 1.70 < c.y < POD_Y0 + 2.15)
        f.paint(nac, J, lambda c, n, i, x=x, y_end=y_end: c.y > y_end - 0.36 and math.hypot(c.x - x, c.z - POD_Z) < POD_R * 0.64)
        parts.append(bone_part([piece(nac, None, 12)], "jet." + side))

    # ------------------------------------------------------------------ JOIN, n-gons out
    mesh = f.join(parts, MESH_NODE, SLOTS)
    mesh.name = MESH_NODE
    mesh.data.name = GEOMETRY
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
    ctx.log("swordfish joined: %d verts, %d tris (n-gons triangulated: %d)" % (len(mesh.data.vertices), f.tri_count(mesh), len(big)))

    rig.skin_rigid(mesh, arm, default_bone="fuse", bake_park=False)
    me = mesh.data

    # ------------------------------------------------------------------ MORPHS (4-12% of the ~33 diagonal)
    nv = len(me.vertices)
    pid = [0.0] * nv
    me.attributes["pid"].data.foreach_get("value", pid)

    def pmask(*ids):
        s = set(float(i) for i in ids)
        return [1.0 if p in s else 0.0 for p in pid]
    ms = morphs.MaskSet(mesh)
    inner = {1: ms.mask("wing1.l"), -1: ms.mask("wing1.r")}
    tipm = {1: ms.mask("wing2.l"), -1: ms.mask("wing2.r")}
    pods = {1: ms.mask("jet.l"), -1: ms.mask("jet.r")}
    crestm = pmask(6)
    vanem = pmask(9)
    bodym = pmask(1, 3, 4, 5)
    snoutm = [m * (1.0 - _smooth(-9.4, -7.4, v.co.y)) for m, v in zip(pmask(1), me.vertices)]

    def deepen(factor, mask):
        def fn(i, co, n):
            if mask[i] <= 0:
                return Vector((0, 0, 0))
            mid = mid_z(co.y)
            return Vector((co.x * (factor - 1) * 0.6, 0, (co.z - mid) * (factor - 1))) * mask[i]
        return fn

    def rake(mask, back, down, base):
        def fn(i, co, n):
            if mask[i] <= 0:
                return Vector((0, 0, 0))
            h = max(0.0, co.z - base)
            return Vector((0, back * h, -down * h)) * mask[i]
        return fn

    def side(mask, sx):
        return [w if v.co.x * sx > 0 else 0.0 for w, v in zip(mask, me.vertices)]

    shapes = {
        "charge": [  # the weapon: the crest stands taller, the snout socket widens to seat a bigger blade
            morphs.scale_about(1.42, (0, CREST_Y0 + 1.0, croot), crestm, axes=(0, 0.25, 1)),
            morphs.scale_about(1.30, (0, SNOUT_Y, SNOUT_Z), snoutm, axes=(1, 0, 1)),
        ],
        "mass": [  # volume: the body deepens, the pods swell about their own axes
            deepen(1.52, bodym),
            *[morphs.scale_about(1.42, (sx * POD_X, POD_Y0 + 2.4, POD_Z), pods[sx], axes=(1, 0.4, 1)) for sx in (1, -1)],
            *[morphs.translate((sx * 0.55, 0, 0), side(pods[sx], sx)) for sx in (1, -1)],
            *[morphs.translate((sx * 0.45, 0, 0), inner[sx]) for sx in (1, -1)],
            *[morphs.translate((sx * 0.45, 0, 0), tipm[sx]) for sx in (1, -1)],
        ],
        "space": [  # reach: the pectorals reach along their span, the crescent spreads
            *[morphs.stretch_along(tuple(span_dir[sx]), (sx * PEC_ROOT[0], PEC_ROOT[1], PEC_ROOT[2]), 1.32, inner[sx],
                                   ramp=(0.3, 2.0)) for sx in (1, -1)],
            *[morphs.translate(tuple(span_dir[sx] * (PEC_SPAN * 0.32)), tipm[sx]) for sx in (1, -1)],
            *[morphs.stretch_along(tuple(span_dir[sx]), tuple(tip_root[sx]), 1.38, tipm[sx]) for sx in (1, -1)],
            morphs.scale_about(1.32, TAIL_HUB, vanem, axes=(0.3, 0.5, 1)),
        ],
        "time": [  # speed: the crest rakes back, the pectorals sweep, the crescent rakes
            rake(crestm, 0.42, 0.20, croot),
            *[morphs.sweep((sx, 0, 0), (sx * PEC_ROOT[0], 0, 0), (0, 0.42, 0), inner[sx]) for sx in (1, -1)],
            *[morphs.sweep((sx, 0, 0), (sx * PEC_ROOT[0], 0, 0), (0, 0.42, 0), tipm[sx]) for sx in (1, -1)],
            morphs.sweep((0, 0, 1), (0, 0, TAIL_HUB[2]), (0, 0.24, 0), [m if v.co.z > TAIL_HUB[2] else 0.0 for m, v in zip(vanem, me.vertices)]),
            morphs.sweep((0, 0, -1), (0, 0, TAIL_HUB[2]), (0, 0.24, 0), [m if v.co.z < TAIL_HUB[2] else 0.0 for m, v in zip(vanem, me.vertices)]),
        ],
    }
    for name in SHAPES:
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))
    if "pid" in me.attributes:
        me.attributes.remove(me.attributes["pid"])

    # ------------------------------------------------------------------ TAKES: the Rhino rig's nine, retargeted unchanged
    acts = takes.retarget_from(arm, rig_from)
    takes.clear_pose(arm)
    # the Rhino rig's first take ('rhino') deflects the wings and pods, and Unity imports a model's default pose from
    # its first take: lead with a rest take so the hull rests at its bind (LESSONS 51)
    takes.rest_take(arm)
    takes.clear_pose(arm)
    lead = ARMATURE + "|" + takes.REST_TAKE
    ctx.log("takes: %d retargeted from the Rhino rig (%s)" % (len(acts), ", ".join(sorted(acts))))

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "yup", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [lead] + [ARMATURE + "|" + a.name for a in acts.values()],
            "lead_take": lead,
            "pose_takes": [],
            "take_parity": False,   # new geometry on the Rhino's bones: the takes ARE the Rhino rig's, the parts are new
            "shapes": list(SHAPES),
            "slots": list(SLOTS),
            # a swordfish's envelope, declared: snout to tail fork, across the pectorals, crest to the lower horn
            "size_ref_blender": ((-9.8, -9.4, -6.0), (9.8, 13.7, 8.7)),
            "size_tol": (0.08, 0.06, 0.10),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
            "sheet_poses": ("rhino_pitchup", "rhino_yawright", "rhino_rollright", "rhino_speedup"),
        },
    }
