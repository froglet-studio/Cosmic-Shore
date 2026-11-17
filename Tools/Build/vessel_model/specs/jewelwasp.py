"""
jewelwasp - a NEW hull (no artist file) for the Jewel Wasp, vessel class 31, the possession vessel
(R_VesselActions/JEWEL_WASP.md). Until now it flew in the Butterfly's model, which the Tiger Moth it was cloned from
wears.

The brief: a jewel wasp stings a cockroach's brain and walks it home by its antenna; this one stings a creature's heart
crystal and flies it. It is small and quick, and while it rides a host it is seen NESTED beside the heart crystal at a
quarter of its size, so the silhouette must read at a few dozen pixels: a big-eyed head, a waisted body, a long banded
gaster with a needle, four narrow wings. A spaceship inspired by the wasp, never the insect (Garrett, 2026-10-18).
Clade -> ship:
  head + compound eyes -> a sensor head module floating ahead of the thorax, its two big compound eyes canopy glass
  elbowed antennae     -> two long elbowed SENSOR MASTS with domain tips: what a wasp leads its host by
  mandibles            -> two clamp prongs under the head (they open with Charge, the bite)
  thorax               -> the armoured core: a domain scutum plate on a gunmetal hull, panel seams, the wing hinges
  red femora           -> six folding LANDING CLAMPS under the core, domain thigh plates (how it grips a heart)
  petiole (the waist)  -> a coupling COLLAR floating between core and gaster, the hovering assembly's gap made a part
  gaster               -> the reactor pod on its own bone (`abdomen`), banded in the domain colour
  sting                -> the INJECTOR needle at the gaster's tip, its point glowing (Engine)
  wings                -> four narrow membrane blades on the Butterfly's hinges: domain cells in a dark vein frame,
                          a dark stigma on each fore wing's leading edge, glass cells in the dark tip
  speed                -> twin thruster mouths at the core's back, flanking the collar (Engine)

Built ON the Butterfly's rig, which is what the Jewel Wasp's prefab binds: the armature `rig`, the mesh node `mesh`,
the root bone `hull` and the four wing bones (`wing.L`, `wing.R`, `wing.2.L`, `wing.2.R`) at the Butterfly's rest, the
slot order and names and the shape names. So this file under its own guid binds every reference JewelWasp.prefab
makes into the Butterfly model by a guid swap alone (author_jewel_wasp_assets.py), and ButterflyAnimation keeps
beating the four wings by name. Two NEW bones: `head` (the head module, parented to `hull`) and `abdomen` (the gaster
and its needle, parented to `hull`, its head at the waist), which JewelWaspExecutor curls under the body on the sting.
  file      Assets/_Models/Vessel Models/JewelWasp.fbx (new guid; its .meta is the Butterfly's with that guid)
  frame     preset "zup": nose -Y, up +Z, .L on +X in the Blender frame
  size      length 14.4 (antenna tips to the needle), span 17.3, height 3.0: the code draws it at HullScale 0.45 free and
            NestedScale 0.22 nested, so it is the hull's PROPORTIONS that were briefed, not the Butterfly's envelope
  slots     0 Domain (wing cells, scutum, bands, thigh plates, mast tips), 1 Body, 2 Window (eyes, wing-tip cells),
            3 Engine (thruster mouths, the needle's point)
  shapes    Charge = the needle draws out and the clamps open (the sting, the attack's recharge);
            Mass   = the gaster swells and the core broadens (volume: Root grows what it feeds);
            Space  = the sensor masts reach and the wings lengthen along their spans (reach: sting range, suction);
            Time   = the wings sweep back and the gaster draws out (rate: a host's speed, the burst)
  takes     ButterflyAnimation beats the wings in code and JewelWaspExecutor curls the gaster; the file carries the
            documentation poses beatup, beatdown, fold, spread and sting (the gaster curled under, needle forward)
"""

import math
from _common import box_uv

NAME = "JewelWasp"
OUTPUT = "Assets/_Models/Vessel Models/JewelWasp.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3

# the Butterfly's hinges and rest dihedrals: the wing bones rest exactly where its prefab's puppetry expects them
FORE_HINGE = (0.7225, -0.75, 0.1275)
HIND_HINGE = (0.7225, 1.20, -0.2125)
FORE_DIHEDRAL = 8.0
HIND_DIHEDRAL = -4.0

HEAD_Y = (-4.55, -2.75)          # the head module, nose -Y
CORE_Y = (-2.45, 1.85)           # the thorax core
WAIST_Y = (2.05, 2.55)           # the coupling collar
GASTER_Y = (2.75, 6.55)          # the reactor pod
NEEDLE_Y = (6.40, 7.75)          # the injector
WAIST = (0.0, 2.30, -0.05)       # the abdomen bone's head: the gaster curls about the waist
SPAN_ROOT_U = 0.38               # the wing blade starts this far out from its hinge: the gap to the core

# wing outlines in the wing's own plane: u outward along the span, v back along the chord, the hinge at (0, 0)
FORE_OUTLINE = [(SPAN_ROOT_U, -0.22), (1.8, -0.40), (3.6, -0.40), (5.4, -0.20), (6.9, 0.18), (7.75, 0.70), (7.95, 1.20),
                (7.55, 1.62), (6.5, 1.92), (5.0, 2.02), (3.5, 1.86), (2.1, 1.45), (1.0, 0.95), (SPAN_ROOT_U, 0.45)]
HIND_OUTLINE = [(SPAN_ROOT_U, -0.12), (1.5, -0.25), (2.9, -0.22), (4.1, 0.05), (4.95, 0.50), (5.15, 0.95), (4.75, 1.32),
                (3.7, 1.52), (2.5, 1.45), (1.4, 1.12), (0.7, 0.72), (SPAN_ROOT_U, 0.32)]
FORE_POLE = (1.15, 0.55)
HIND_POLE = (1.0, 0.45)


def _catmull(pts, n_out):
    """A closed Catmull-Rom curve through `pts`, resampled to n_out points evenly in parameter."""
    m = len(pts)
    out = []
    for k in range(n_out):
        s = k * m / n_out
        i = int(s)
        t = s - i
        p0, p1, p2, p3 = (pts[(i - 1) % m], pts[i % m], pts[(i + 1) % m], pts[(i + 2) % m])
        t2, t3 = t * t, t * t * t
        out.append(tuple(0.5 * ((2 * p1[c]) + (-p0[c] + p2[c]) * t + (2 * p0[c] - 5 * p1[c] + 4 * p2[c] - p3[c]) * t2
                                + (-p0[c] + 3 * p1[c] - 3 * p2[c] + p3[c]) * t3) for c in range(2)))
    return out


def build(ctx):
    import bpy  # noqa: F401
    import bmesh
    from mathutils import Vector, Matrix
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    parts = []
    bone_of = {}             # part name -> bone

    def add(part, bone):
        f.ensure_slots(part, SLOTS)
        bone_of[part.name] = bone
        parts.append(part)
        return part

    # ------------------------------------------------------------------ WINGS (the Butterfly's radial-grid membrane)
    def wing(name, outline_ctrl, pole, rings, pattern, root_h, tip_h, arch, n_out=48, veins=(), side=1, hinge=(0, 0, 0),
             dihedral=0.0):
        """A membrane as a radial grid from `pole` to a smooth outline (ring k of K is the outline scaled toward the pole
        by k/K, so a face's ring position t is how far out the wing it sits), one closed solid; veins are grid columns
        painted Body and raised into a ridge. Built flat in (u, v), turned by the dihedral, placed at the hinge."""
        O = _catmull(outline_ctrl, n_out)
        P = Vector((pole[0], pole[1]))
        vein_cols = set()
        for (i0, width) in veins:
            for w in range(width):
                vein_cols.add((i0 + w) % n_out)
        bm = bmesh.new()

        def zof(t, q):
            return arch * math.sin(math.pi * min(1.0, t)) * (0.4 + 0.6 * min(1.0, q.x / 5.0))

        def ridge(i):
            return 1.0 if (i in vein_cols or ((i - 1) % n_out) in vein_cols) else 0.0
        top, bot = [], []
        for k in range(rings + 1):
            rt, rb = [], []
            for i in range(n_out if k > 0 else 1):
                t = k / rings
                q = P + (Vector(O[i]) - P) * t
                h = root_h + (tip_h - root_h) * t
                z = zof(t, q)
                rise = 0.035 * ridge(i) * (1.0 - 0.5 * t) if 0 < k < rings else 0.0
                rt.append(bm.verts.new((q.x, q.y, z + h / 2 + rise)))
                rb.append(bm.verts.new((q.x, q.y, z - h / 2 - rise)))
            top.append(rt)
            bot.append(rb)

        def mat_for(k_lo, k_hi, i0, i1):
            t = (k_lo + k_hi) / 2.0 / rings
            s_ = ((i0 + 0.5) % n_out) / n_out
            return pattern(t, s_, i0 % n_out in vein_cols)
        for layer, flip in ((top, False), (bot, True)):
            for i in range(n_out):
                j = (i + 1) % n_out
                vs = [layer[0][0], layer[1][i], layer[1][j]]
                fc = bm.faces.new(list(reversed(vs)) if flip else vs)
                fc.material_index = H
                for k in range(1, rings):
                    vs = [layer[k][i], layer[k + 1][i], layer[k + 1][j], layer[k][j]]
                    fc = bm.faces.new(list(reversed(vs)) if flip else vs)
                    fc.material_index = mat_for(k, k + 1, i, j)
        for i in range(n_out):
            j = (i + 1) % n_out
            fc = bm.faces.new([top[rings][j], top[rings][i], bot[rings][i], bot[rings][j]])
            fc.material_index = H
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        part = f.new_mesh_object(name, bm, smooth=True)
        f.ensure_slots(part, SLOTS)
        d = math.radians(dihedral)
        rot = Matrix(((math.cos(d), 0, -math.sin(d), 0), (0, 1, 0, 0), (math.sin(d), 0, math.cos(d), 0), (0, 0, 0, 1)))
        f.transform_mesh(part, Matrix.Translation(Vector(hinge)) @ rot)
        if side < 0:
            f.transform_mesh(part, Matrix.Scale(-1, 4, Vector((1, 0, 0))))
            part.data.flip_normals()
        return part

    # Outline index 0 sits at the root's leading corner and runs out along the LEADING edge to the tip (around index
    # n/2) and back along the trailing edge, so s near 0 is the leading edge.
    def fore_pattern(t, s, vein):
        if t < 0.22 or vein:
            return H                                   # the dark root and the vein frame
        if 0.56 <= t < 0.70 and s < 0.13:
            return H                                   # the stigma: a dark cell on the leading edge
        if t >= 0.86:
            x = (s * 15) % 1.0
            return W if (t < 0.93 and 0.30 < x < 0.62) else H   # the dark tip with a row of glass cells
        return D

    def hind_pattern(t, s, vein):
        if t < 0.22 or vein:
            return H
        if t >= 0.86:
            x = (s * 11) % 1.0
            return W if (t < 0.93 and 0.30 < x < 0.62) else H
        return D

    n_out = 48
    fore_veins = [(0, 3), (6, 1), (11, 1), (16, 1), (21, 1), (26, 1), (31, 1), (37, 1), (43, 1)]
    hind_veins = [(0, 2), (8, 1), (15, 1), (22, 1), (30, 1), (39, 1)]
    for tag, sx in (("L", 1), ("R", -1)):
        add(wing("wing." + tag, FORE_OUTLINE, FORE_POLE, 9, fore_pattern, 0.12, 0.04, 0.16, n_out=n_out,
                 veins=fore_veins, side=sx, hinge=FORE_HINGE, dihedral=FORE_DIHEDRAL), "wing." + tag)
        add(wing("wing.2." + tag, HIND_OUTLINE, HIND_POLE, 8, hind_pattern, 0.10, 0.04, 0.10, n_out=n_out,
                 veins=hind_veins, side=sx, hinge=HIND_HINGE, dihedral=HIND_DIHEDRAL), "wing.2." + tag)

    # ------------------------------------------------------------------ CORE (the thorax): the hull bone
    y0, y1 = CORE_Y
    L = y1 - y0
    core_st = [  # (t, half-width, top, bottom, n_top, n_bottom, dy_up)
        (0.00, 0.22, 0.20, 0.18, 2.2, 2.2, 0.10), (0.05, 0.46, 0.46, 0.38, 2.5, 2.3, 0.10),
        (0.14, 0.60, 0.64, 0.50, 2.7, 2.4, 0.10), (0.30, 0.66, 0.78, 0.56, 2.9, 2.5, 0.10),
        (0.46, 0.64, 0.80, 0.56, 2.9, 2.5, 0.08), (0.60, 0.58, 0.70, 0.52, 2.8, 2.4, 0.04),
        (0.74, 0.54, 0.60, 0.48, 2.7, 2.4, 0.00), (0.88, 0.46, 0.50, 0.42, 2.6, 2.3, -0.02),
        (0.96, 0.34, 0.38, 0.34, 2.4, 2.2, -0.03), (1.00, 0.16, 0.18, 0.16, 2.2, 2.2, -0.03),
    ]
    core = f.fuselage("hull", L, nose=(0, y0, 0.0), axis=(0, 1, 0), segments=24, stations=core_st)
    f.ensure_slots(core, SLOTS)
    f.paint(core, H)
    # the scutum: a domain plate across the core's back, its edge a dark seam
    f.paint(core, D, lambda c, n, i: n.z > 0.55 and abs(c.x) < 0.42 and -2.05 < c.y < 0.9)
    f.inset_faces(core, lambda c, n, m: m == D, 0.035, depth=-0.02)
    # flank seam lines: the side plates' lower edges, domain trim
    f.paint(core, D, lambda c, n, i: abs(n.x) > 0.80 and -1.7 < c.y < 0.6 and -0.10 < c.z < 0.02)
    f.subsurf(core, 1)
    f.apply_modifiers(core)
    add(core, "hull")

    # twin thruster mouths at the core's back, flanking the collar
    for sx in (1, -1):
        noz = f.lathe("thruster", [(0.0, 0.0), (0.20, 0.0), (0.24, 0.10), (0.22, 0.42), (0.17, 0.46), (0.0, 0.46)],
                      segments=18, axis=(0, 1, 0), origin=(sx * 0.36, 1.45, -0.20))
        f.ensure_slots(noz, SLOTS)
        f.paint(noz, H)
        f.paint(noz, J, lambda c, n, i: n.y > 0.35 and c.y > 1.70)
        add(noz, "hull")

    # six landing clamps under the core: a domain thigh plate, a dark shin folded back, a claw
    for k, (yl, spread) in enumerate(((-1.55, 0.95), (-0.55, 1.05), (0.55, 1.00))):
        for sx in (1, -1):
            root = (sx * 0.40, yl, -0.62)
            knee = (sx * (0.40 + spread * 0.55), yl + 0.05, -1.05)
            foot = (sx * (0.40 + spread * 0.62), yl + 0.75, -1.38)
            thigh = f.tube_along("clamp", [root, knee], [0.11, 0.09], segments=8)
            f.ensure_slots(thigh, SLOTS)
            f.paint(thigh, D)
            shin = f.tube_along("clamp", [knee, foot], [0.075, 0.05], segments=8)
            f.ensure_slots(shin, SLOTS)
            f.paint(shin, H)
            add(thigh, "hull")
            add(shin, "hull")

    # ------------------------------------------------------------------ HEAD (its own bone): floats ahead of the core
    h0, h1 = HEAD_Y
    head_st = [
        (0.00, 0.20, 0.16, 0.14, 2.2, 2.2, -0.05), (0.10, 0.48, 0.42, 0.34, 2.4, 2.2, -0.02),
        (0.32, 0.70, 0.62, 0.50, 2.6, 2.3, 0.02), (0.58, 0.74, 0.66, 0.52, 2.7, 2.4, 0.04),
        (0.82, 0.62, 0.56, 0.44, 2.6, 2.3, 0.05), (1.00, 0.26, 0.24, 0.20, 2.2, 2.2, 0.05),
    ]
    head = f.fuselage("head", h1 - h0, nose=(0, h0, 0.10), axis=(0, 1, 0), segments=22, stations=head_st)
    f.ensure_slots(head, SLOTS)
    f.paint(head, H)
    f.paint(head, D, lambda c, n, i: n.z > 0.80 and abs(c.x) < 0.16)       # a domain crest line
    f.subsurf(head, 1)
    f.apply_modifiers(head)
    add(head, "head")
    # the compound eyes: big glass ovals on the head's sides, half sunk
    for sx in (1, -1):
        eye = f.sphere("eye", 0.46, center=(sx * 0.56, -3.80, 0.24), segments=18, rings=12, scale=(0.62, 1.15, 1.0))
        f.ensure_slots(eye, SLOTS)
        f.paint(eye, W)
        add(eye, "head")
    # the clamp prongs (mandibles) under the head's front
    for sx in (1, -1):
        prong = f.tube_along("prong", [(sx * 0.20, -4.30, -0.20), (sx * 0.34, -4.80, -0.28), (sx * 0.14, -5.10, -0.30)],
                             [0.09, 0.07, 0.02], segments=8)
        f.ensure_slots(prong, SLOTS)
        f.paint(prong, H)
        add(prong, "head")
    # the elbowed sensor masts: up from the brow, an elbow, then forward; domain tips
    for sx in (1, -1):
        pts = [(sx * 0.18, -4.25, 0.58), (sx * 0.30, -4.60, 1.05), (sx * 0.48, -4.95, 1.34),   # the scape, up
               (sx * 0.78, -5.65, 1.50), (sx * 1.08, -6.30, 1.36), (sx * 1.28, -6.85, 1.08)]   # the flagellum, forward
        mast = f.tube_along("mast", pts, [0.075, 0.07, 0.065, 0.055, 0.05, 0.045], segments=8)
        f.ensure_slots(mast, SLOTS)
        f.paint(mast, H)
        f.paint(mast, D, lambda c, n, i: c.y < -6.25)
        add(mast, "head")

    # ------------------------------------------------------------------ COLLAR (the petiole), floating in the waist
    w0, w1 = WAIST_Y
    collar = f.lathe("collar", [(0.0, 0.0), (0.18, 0.0), (0.26, 0.08), (0.26, w1 - w0 - 0.08), (0.18, w1 - w0), (0.0, w1 - w0)],
                     segments=18, axis=(0, 1, 0), origin=(0, w0, -0.05))
    f.ensure_slots(collar, SLOTS)
    f.paint(collar, H)
    f.paint(collar, J, lambda c, n, i: abs(c.y - (w0 + w1) / 2) < 0.13 and abs(n.y) < 0.4)   # the coupling glows
    add(collar, "hull")

    # ------------------------------------------------------------------ GASTER (the abdomen bone) + the needle
    g0, g1 = GASTER_Y
    GL = g1 - g0
    gst = [(0.00, 0.22, 0.20, 0.20, 2.2, 2.2, 0.0)]
    bands = []
    t = 0.05
    r = 0.62
    for k in range(5):        # five segments: each a swell then a pinch, growing to the widest then tapering
        r_seg = [0.62, 0.92, 1.04, 0.92, 0.66][k]
        gst.append((t, r_seg, r_seg * 0.92, r_seg * 0.86, 2.4, 2.2, -0.04 * k))
        gst.append((min(t + 0.13, 0.97), r_seg * 0.90, r_seg * 0.84, r_seg * 0.80, 2.3, 2.2, -0.04 * k - 0.02))
        bands.append(g0 + GL * t)
        t += 0.19
    gst.append((1.0, 0.16, 0.14, 0.14, 2.0, 2.0, -0.22))
    gaster = f.fuselage("abdomen", GL, nose=(0, g0, -0.05), axis=(0, 1, 0), segments=24, stations=gst)
    f.ensure_slots(gaster, SLOTS)
    f.paint(gaster, H)
    for yb in bands[:4]:      # a domain band on each segment's crest
        f.paint(gaster, D, lambda c, n, i, yb=yb: n.z > 0.25 and yb - 0.10 < c.y < yb + 0.34)
    f.subsurf(gaster, 1)
    f.apply_modifiers(gaster)
    add(gaster, "abdomen")
    n0, n1 = NEEDLE_Y
    needle = f.lathe("needle", [(0.0, 0.0), (0.14, 0.0), (0.12, 0.40), (0.07, 0.90), (0.0, n1 - n0)],
                     segments=12, axis=(0, 1, 0), origin=(0, n0, -0.27))
    f.ensure_slots(needle, SLOTS)
    f.paint(needle, H)
    f.paint(needle, J, lambda c, n, i: c.y > n1 - 0.62)
    add(needle, "abdomen")

    # ------------------------------------------------------------------ JOIN, n-gons out
    mesh = f.join(parts, MESH_NODE, SLOTS)
    mesh.data.name = MESH_NODE
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
    ctx.log("jewel wasp joined: %d verts, %d tris (n-gons triangulated: %d)" % (len(mesh.data.vertices), f.tri_count(mesh), len(big)))

    # ------------------------------------------------------------------ RIG: the Butterfly's five bones + head, abdomen
    def span_dir(dihedral, sx):
        d = math.radians(dihedral)
        return Vector((sx * math.cos(d), 0.0, math.sin(d)))
    B = rig.BoneSpec
    bones = [B("hull", (0, 0, 0), (0, -1.6, 0))]
    for tag, sx in (("L", 1), ("R", -1)):
        fh = Vector((sx * FORE_HINGE[0], FORE_HINGE[1], FORE_HINGE[2]))
        hh = Vector((sx * HIND_HINGE[0], HIND_HINGE[1], HIND_HINGE[2]))
        # the Butterfly's bone TAILS, exactly (the rest is head + roll; ButterflyAnimation turns about the rest frame)
        bones.append(B("wing." + tag, fh, fh + Vector((sx * math.cos(math.radians(14.0)), 0, math.sin(math.radians(14.0)))) * 3.0, parent="hull"))
        bones.append(B("wing.2." + tag, hh, hh + Vector((sx * math.cos(math.radians(-6.3)), 0, math.sin(math.radians(-6.3)))) * 2.5, parent="hull"))
    bones.append(B("head", (0, -2.95, 0.10), (0, -4.2, 0.10), parent="hull"))
    bones.append(B("abdomen", WAIST, (WAIST[0], WAIST[1] + 2.4, WAIST[2] - 0.1), parent="hull"))
    arm = rig.build_armature(ARMATURE, bones)
    groups = {vg.name: bone_of.get(vg.name, "hull") for vg in mesh.vertex_groups}
    rig.skin_rigid(mesh, arm, group_to_bone=groups, default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]

    # ------------------------------------------------------------------ MORPHS (4-12% of the ~23 diagonal)
    ms = morphs.MaskSet(mesh)
    me = mesh.data
    fore = {sx: ms.mask("wing." + t) for t, sx in (("L", 1), ("R", -1))}
    hind = {sx: ms.mask("wing.2." + t) for t, sx in (("L", 1), ("R", -1))}
    gaster_m = ms.mask("abdomen")
    core_m = ms.mask("hull")
    head_m = ms.mask("head")
    needle_m = [w * (1.0 if v.co.y > NEEDLE_Y[0] + 0.05 else 0.0) for w, v in zip(gaster_m, me.vertices)]
    prong_m = [w * (1.0 if (v.co.y < -4.25 and v.co.z < -0.12) else 0.0) for w, v in zip(head_m, me.vertices)]
    mast_m = [w * (1.0 if (v.co.z > 0.55 and v.co.y < -4.15) else 0.0) for w, v in zip(head_m, me.vertices)]
    pod_m = [w * (1.0 if v.co.y < NEEDLE_Y[0] + 0.05 else 0.0) for w, v in zip(gaster_m, me.vertices)]
    core_body = [w * (1.0 if v.co.z > -0.70 else 0.0) for w, v in zip(core_m, me.vertices)]   # not the clamps

    def splay(amount):
        def fn(i, co, n):
            if prong_m[i] <= 0:
                return Vector((0, 0, 0))
            k = max(0.0, min(1.0, (-4.25 - co.y) / 0.85))
            return Vector((math.copysign(1.0, co.x) * amount * k, 0, 0))
        return fn
    shapes = {
        "Charge": [  # the sting: the needle draws out of the pod and the clamp prongs open
            morphs.translate((0, 1.25, -0.05), needle_m),
            splay(0.42),
        ],
        "Mass": [    # volume: the pod swells, the core broadens
            morphs.scale_about(1.85, (0, (GASTER_Y[0] + GASTER_Y[1]) / 2, -0.10), pod_m, axes=(1, 0.50, 1)),
            morphs.scale_about(1.28, (0, -0.3, 0.0), core_body, axes=(1, 0, 1)),
            morphs.translate((0, 0.80, 0.02), needle_m),          # the needle rides the pod's tip back
        ],
        "Space": [   # reach: the masts reach, every wing extends along its span from its hinge
            morphs.stretch_along((0, -1, 0.35), (0, -4.25, 0.55), 1.45, mast_m),
            *[morphs.stretch_along(span_dir(FORE_DIHEDRAL, sx), (sx * FORE_HINGE[0], FORE_HINGE[1], FORE_HINGE[2]), 1.20, fore[sx]) for sx in (1, -1)],
            *[morphs.stretch_along(span_dir(HIND_DIHEDRAL, sx), (sx * HIND_HINGE[0], HIND_HINGE[1], HIND_HINGE[2]), 1.18, hind[sx]) for sx in (1, -1)],
        ],
        "Time": [    # rate: the wings sweep back, the pod draws out behind the waist
            *[morphs.sweep((sx, 0, 0), (sx * FORE_HINGE[0], 0, 0), (0, 0.26, 0), fore[sx]) for sx in (1, -1)],
            *[morphs.sweep((sx, 0, 0), (sx * HIND_HINGE[0], 0, 0), (0, 0.22, 0), hind[sx]) for sx in (1, -1)],
            morphs.stretch_along((0, 1, 0), (0, GASTER_Y[0], 0), 1.22, gaster_m),
        ],
    }
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))

    # ------------------------------------------------------------------ DOCUMENTATION POSES (code drives the hull)
    def pose(p):
        if p == "sting":   # the gaster curls under the core about the waist, the needle swinging forward
            return {"abdomen": (-72.0, 0, 0)}
        a = {"beatup": 40.0, "beatdown": -30.0, "fold": 82.0, "spread": 0.0}[p]
        hk = {"beatup": 0.8, "beatdown": 0.8, "fold": 1.0, "spread": 1.0}[p]
        out = {}
        for tag, sx in (("L", 1), ("R", -1)):
            out["wing." + tag] = (0, -sx * a, 0) if p != "spread" else (0, 0, sx * 10)
            out["wing.2." + tag] = (0, -sx * a * hk, 0) if p != "spread" else (0, 0, sx * 6)
        return out
    names = ("beatup", "beatdown", "fold", "spread", "sting")
    for p in names:
        takes.pose_take(arm, p, pose(p))
    takes.clear_pose(arm)

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "zup", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [ARMATURE + "|" + p for p in names],
            "pose_takes": [ARMATURE + "|" + p for p in names],
            "sheet_poses": ("beatup", "fold", "spread", "sting"),
            "pose_expect": {
                "beatup": [("wing.L", "z", +1, 1.2), ("wing.R", "z", +1, 1.2), ("wing.2.L", "z", +1, 0.6)],
                "beatdown": [("wing.L", "z", -1, 0.8), ("wing.R", "z", -1, 0.8)],
                "fold": [("wing.L", "z", +1, 1.6), ("wing.R", "z", +1, 1.6), ("wing.L", "x", -1, 1.2)],
                "sting": [("abdomen", "z", -1, 1.2), ("abdomen", "y", -1, 0.6)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            # the brief's own envelope (a new hull: the code scales it, nothing in the prefab was tuned to it)
            "size_ref_blender": ((-8.6, -6.95, -1.45), (8.6, 7.80, 1.95)), "size_tol": (0.08, 0.08, 0.25),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.035,
        },
    }


# the hovering assembly: the core (with its collar, clamps and thrusters), the head module, the gaster pod, four wings
FLOATING = "hull,head,abdomen,wing.L,wing.R,wing.2.L,wing.2.R"
