"""
butterfly - the fleet's first hull with NO artist file: it replaces the procedural placeholder
(ButterflyHullBuilder / ButterflyHullForm, 2,464 triangles, one material) with a model built to
the fleet's look and the fleet convention (Garrett, 2026-10-18: "the butterfly will show that you
can replace a placeholder with a vessel that meets our aesthetics and standards").

What it must read as, from the 207-unit camera (about 2.6 px per unit at 1080p: the whole hull is
~56 px wide), BUTTERFLY.md section 2: FOUR wings on their own hinges (fore and hind beat 0.18 of a
beat apart), the wings wearing the DOMAIN colour (they are what a pilot reads this vessel's domain
from), the beat, the Mass-mode spread and the Fold that closes the wings over the back. Detail
below ~2 units is for the menus, the codex and close passes; the silhouette and the colour fields
carry the read.

The fleet's look, measured on the artist hulls (style_metrics.py --fleet; Docs/VesselModels sheets):
dark gunmetal sculpted bodies, layered sharp edges, domain colour in fields and trim lines, cyan
glass, lime glows. This hull: a segmented gunmetal body (head with big cyan compound eyes, armoured
thorax with domain trim, a ribbed abdomen with domain bands), four wings each built as a radial
grid from its root so the colour follows real butterfly wing anatomy (dark base, a broad domain
band, a dark outer margin with cyan cells, a domain rim; an eyespot on each hind wing), raised
vein ribs and a costal spar in relief, lime antenna clubs and a lime dust emitter under the thorax.

Contract (FLEET_CONVENTION sections 1-4 for a NEW vessel; Butterfly integration map, 2026-10-18):
  file      Assets/_Models/Vessel Models/Butterfly.fbx (new guid), preset "zup"
  nodes     mesh `mesh`, armature `rig`; root bone `hull`; wings `wing.L` `wing.R` (fore),
            `wing.2.L` `wing.2.R` (hind), each head at the placeholder's hinge so the puppetry,
            the colliders and the skimmers keep their places:
              fore  Unity (-+0.7225, 0.1275, 0.75)  = Blender (+-0.7225, -0.75, 0.1275)
              hind  Unity (-+0.7225, -0.2125, -1.2) = Blender (+-0.7225, +1.2, -0.2125)
            rest dihedral built into the geometry: fore +14 deg, hind -6.3 deg (the placeholder's)
  size      the placeholder's within ~5%: span 21.35, length 11.55, height ~3.9 (the wake, the
            24-unit dust capsule, the 30-unit catcher and the r=6 collider were tuned to it)
  slots     0 Domain (wing fields, rims, trim), 1 Body, 2 Window (eyes, wing cells), 3 Engine (glows)
  shapes    Charge, Mass, Space, Time - the placeholder's meanings at the convention's 4-12% travel:
            Charge = the margins scallop and the apexes flare, the antenna clubs swell (the bite);
            Mass   = the wings broaden and the body fattens (the wide wake's volume);
            Space  = the wings reach along their own spans (reach);
            Time   = the wings sweep back and the abdomen lengthens (rate)
  takes     none drive it (ButterflyAnimation beats the wing bones in code); four documentation
            poses for the sheet and the studio: beatup, beatdown, fold, spread
"""

import math
from _common import box_uv

NAME = "Butterfly"
OUTPUT = "Assets/_Models/Vessel Models/Butterfly.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3

FORE_HINGE = (0.7225, -0.75, 0.1275)    # Blender frame, the .L side (+X = the pilot's left)
HIND_HINGE = (0.7225, 1.20, -0.2125)
FORE_DIHEDRAL = 14.0
HIND_DIHEDRAL = -6.3

# wing outlines in the wing's own plane: u outward along the span, v back along the chord, the hinge at (0, 0)
FORE_OUTLINE = [(0.15, -0.55), (2.5, -1.25), (5.5, -1.95), (8.3, -2.45), (9.8, -2.3), (10.25, -1.45), (10.0, -0.35),
                (9.3, 0.95), (8.2, 2.25), (6.7, 3.55), (5.2, 4.3), (3.6, 4.15), (1.9, 3.4), (0.55, 2.35), (0.05, 0.9)]
HIND_OUTLINE = [(0.1, -0.35), (2.4, -0.62), (4.8, -0.55), (6.6, 0.05), (7.3, 1.2), (7.05, 2.55), (6.2, 3.65),
                (5.1, 4.25), (4.25, 4.45), (3.75, 4.98), (3.3, 5.0), (3.0, 4.5), (2.25, 3.85), (1.2, 3.05), (0.25, 2.2),
                (0.0, 0.9)]
FORE_POLE = (1.0, 0.95)
HIND_POLE = (0.85, 0.95)


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
    import bpy
    import bmesh
    from mathutils import Vector, Matrix
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    parts = []

    # ------------------------------------------------------------------ WINGS
    def wing(name, outline_ctrl, pole, rings, pattern, root_h, tip_h, arch, n_out=56, veins=(), side=1, hinge=(0, 0, 0), dihedral=0.0):
        """
        A wing membrane as a RADIAL grid from `pole` (near the root) to a smooth outline: ring k of K
        is the outline scaled toward the pole by k/K, so a face's ring position t is how far out the
        wing it sits - the parameter real wing patterns follow (bands parallel to the margin), and a
        grid COLUMN runs root to margin the way a real vein does. The membrane is one closed solid
        (root_h -> tip_h thick, gently arched). Veins are columns of the membrane itself: painted
        Body and raised into a ridge on both faces, so the wing is a dark frame holding domain-
        coloured cells (the Monarch's anatomy, and the fleet's dark-frame / glowing-fill language),
        with nothing floating. Every face is painted by pattern(t, s, uv, vein); built flat in
        (u, v), turned by the dihedral about the forward axis and placed at the hinge; R mirrors L.
        """
        O = _catmull(outline_ctrl, n_out)
        P = Vector((pole[0], pole[1]))
        vein_cols = set()
        for (i0, width) in veins:
            for w in range(width):
                vein_cols.add((i0 + w) % n_out)
        bm = bmesh.new()
        lay_t = bm.verts.layers.float.new("wt")
        lay_s = bm.verts.layers.float.new("ws")

        def zof(t, uvp):
            return arch * math.sin(math.pi * min(1.0, t)) * (0.4 + 0.6 * min(1.0, uvp.x / 6.0))

        def ring_pt(k, i):
            t = k / rings
            return t, P + (Vector(O[i]) - P) * t

        def ridge(i):   # a vertex on a vein column's edge rises: both edges of a vein column -> a ridge
            return 1.0 if (i in vein_cols or ((i - 1) % n_out) in vein_cols) else 0.0
        top, bot = [], []
        for k in range(rings + 1):
            rt, rb = [], []
            for i in range(n_out if k > 0 else 1):
                t, q = ring_pt(k, i)
                h = root_h + (tip_h - root_h) * t
                z = zof(t, q)
                rise = 0.05 * ridge(i) * (1.0 - 0.5 * t) if 0 < k < rings else 0.0
                vt = bm.verts.new((q.x, q.y, z + h / 2 + rise))
                vb = bm.verts.new((q.x, q.y, z - h / 2 - rise))
                for v in (vt, vb):
                    v[lay_t] = t
                    v[lay_s] = i / n_out
                rt.append(vt)
                rb.append(vb)
            top.append(rt)
            bot.append(rb)

        def mat_for(k_lo, k_hi, i0, i1):
            t = (k_lo + k_hi) / 2.0 / rings
            s_ = ((i0 + 0.5) % n_out) / n_out
            uvm = (P + ((Vector(O[i0 % n_out]) + Vector(O[i1 % n_out])) / 2 - P) * t)
            return pattern(t, s_, (uvm.x, uvm.y), i0 % n_out in vein_cols)
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
        for i in range(n_out):          # the rim: a closed solid, the edge wears Body
            j = (i + 1) % n_out
            fc = bm.faces.new([top[rings][j], top[rings][i], bot[rings][i], bot[rings][j]])
            fc.material_index = H
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        part = f.new_mesh_object(name, bm, smooth=True)
        f.ensure_slots(part, SLOTS)
        # place: dihedral about the forward axis (Blender Y), then the hinge; the R side mirrored
        d = math.radians(dihedral)
        rot = Matrix(((math.cos(d), 0, -math.sin(d), 0), (0, 1, 0, 0), (math.sin(d), 0, math.cos(d), 0), (0, 0, 0, 1)))
        f.transform_mesh(part, Matrix.Translation(Vector(hinge)) @ rot)
        if side < 0:
            f.transform_mesh(part, Matrix.Scale(-1, 4, Vector((1, 0, 0))))
            part.data.flip_normals()
        return part

    def margin_spots(t, s, n_cells, rows):
        """A row of small cyan cells in the dark margin, one per cell between veins (the Monarch's white spots).
        t is a face's ring CENTRE, (k + 0.5) / K: a row's band must contain one ring centre at the chosen
        ring count (10 fore, 9 hind), or the row vanishes when the grid is coarsened (it did, round 4)."""
        for (t0, t1, phase) in rows:
            if t0 <= t < t1:
                x = (s * n_cells + phase) % 1.0
                return 0.28 < x < 0.60
        return False

    def fore_pattern(t, s, uv, vein):
        if t < 0.20 or vein:
            return H                     # the dark base and the vein frame
        if 0.47 <= t < 0.53 and 0.10 < s < 0.62:
            return H                     # the cross-vein closing the discal cell
        if t >= 0.80:                   # the dark margin: a row of cyan cells on its inner ring, a dark rim outside
            return W if margin_spots(t, s, 13, ((0.80, 0.90, 0.0),)) else H
        if t >= 0.66 and s < 0.20:      # the dark sub-apical bar with its own cells
            return W if margin_spots(t, s, 22, ((0.70, 0.76, 0.25),)) else H
        return D

    def hind_pattern(t, s, uv, vein):
        if t < 0.18 or vein:
            return H
        if 0.42 <= t < 0.48 and 0.12 < s < 0.70:
            return H
        if t >= 0.78:
            return W if margin_spots(t, s, 11, ((0.78, 0.89, 0.0),)) else H
        return D

    n_out = 56
    # vein columns (first column, width): the costa along the leading edge is the widest
    fore_veins = [(0, 3), (7, 1), (12, 1), (17, 1), (22, 1), (27, 1), (32, 1), (38, 1), (45, 1), (52, 1)]
    hind_veins = [(0, 2), (8, 1), (14, 1), (20, 1), (26, 1), (33, 1), (41, 1), (49, 1)]
    for tag, sx in (("L", 1), ("R", -1)):
        parts.append(wing("wing." + tag, FORE_OUTLINE, FORE_POLE, 10, fore_pattern, 0.16, 0.05, 0.32, n_out=n_out,
                          veins=fore_veins, side=sx, hinge=FORE_HINGE, dihedral=FORE_DIHEDRAL))
        parts.append(wing("wing.2." + tag, HIND_OUTLINE, HIND_POLE, 9, hind_pattern, 0.14, 0.05, 0.22, n_out=n_out,
                          veins=hind_veins, side=sx, hinge=HIND_HINGE, dihedral=HIND_DIHEDRAL))

    # ------------------------------------------------------------------ BODY: head, thorax, abdomen
    # one smooth loft nose -Y to tail +Y; ridges between abdomen segments come from the stations
    st = [  # (t, half-width, top, bottom, n_top, n_bottom, dy_up)
        (0.000, 0.10, 0.08, 0.08, 2.2, 2.2, 0.15), (0.025, 0.36, 0.34, 0.28, 2.4, 2.2, 0.17), (0.065, 0.52, 0.50, 0.40, 2.5, 2.3, 0.18),
        (0.115, 0.50, 0.48, 0.40, 2.5, 2.3, 0.17), (0.150, 0.34, 0.34, 0.30, 2.4, 2.2, 0.14),  # neck
        (0.190, 0.62, 0.66, 0.52, 2.6, 2.4, 0.14), (0.260, 0.82, 0.86, 0.66, 2.8, 2.5, 0.12), (0.340, 0.88, 0.90, 0.70, 2.9, 2.5, 0.10),
        (0.420, 0.84, 0.86, 0.66, 2.8, 2.5, 0.08), (0.480, 0.66, 0.66, 0.54, 2.6, 2.4, 0.05),  # thorax
        (0.515, 0.48, 0.48, 0.42, 2.4, 2.3, 0.03),  # waist
    ]
    y = 0.545
    seg_r = 0.62
    seg_i = 0
    while y < 0.985:          # six abdomen segments, each a swell then a pinch
        st.append((y, seg_r, seg_r * 0.98, seg_r * 0.9, 2.5, 2.3, 0.0))
        st.append((min(y + 0.045, 0.99), seg_r * 0.86, seg_r * 0.84, seg_r * 0.78, 2.4, 2.3, 0.0))
        y += 0.075
        seg_r *= 0.86
        seg_i += 1
    st.append((1.0, 0.05, 0.05, 0.05, 2.0, 2.0, 0.0))
    body = f.fuselage("hull", 7.5, nose=(0, -3.75, 0.0), axis=(0, 1, 0), segments=18, stations=st)
    f.ensure_slots(body, SLOTS)
    f.paint(body, H)
    # domain trim: a dorsal line down the thorax and a band on the crest of every abdomen segment
    f.paint(body, D, lambda c, n, i: n.z > 0.78 and abs(c.x) < 0.11 and -2.6 < c.y < 0.1)
    for k in range(seg_i):
        y0 = -3.75 + 7.5 * (0.545 + 0.075 * k) - 0.09
        f.paint(body, D, lambda c, n, i, y0=y0: n.z > 0.35 and y0 < c.y < y0 + 0.18)
    # thorax side plates' lower edge: a domain trim line on each flank
    f.paint(body, D, lambda c, n, i: abs(n.x) > 0.75 and -2.1 < c.y < -0.6 and -0.12 < c.z < 0.0)
    f.subsurf(body, 1)
    f.apply_modifiers(body)
    parts.append(body)

    # compound eyes: Window glass, half sunk into the head
    for sx in (1, -1):
        eye = f.sphere("eye", 0.40, center=(sx * 0.40, -3.30, 0.30), segments=16, rings=10, scale=(0.9, 1.0, 1.0))
        f.ensure_slots(eye, SLOTS)
        f.paint(eye, W)
        parts.append(eye)
    # antennae: thin curving rods ending in lime clubs
    for sx in (1, -1):
        pts = [(sx * 0.16, -3.45, 0.62), (sx * 0.40, -4.10, 1.02), (sx * 0.78, -4.70, 1.30), (sx * 1.08, -5.05, 1.42)]
        rod = f.tube_along("antenna", pts, [0.055, 0.05, 0.045, 0.04], segments=8)
        f.ensure_slots(rod, SLOTS)
        f.paint(rod, H)
        club = f.sphere("club", 0.17, center=(sx * 1.13, -5.15, 1.45), segments=12, rings=8, scale=(1.0, 1.35, 1.0))
        f.ensure_slots(club, SLOTS)
        f.paint(club, J)
        parts += [rod, club]
    # the dust emitter under the thorax (the capsule hangs below the hull): a lime ring in a dark collar
    emitter = f.lathe("emitter", [(0.0, 0.0), (0.42, 0.0), (0.46, 0.06), (0.40, 0.16), (0.24, 0.18), (0.0, 0.18)],
                      segments=20, axis=(0, 0, -1), origin=(0, -1.35, -0.62))
    f.ensure_slots(emitter, SLOTS)
    f.paint(emitter, H)
    f.paint(emitter, J, lambda c, n, i: n.z < -0.6 and math.hypot(c.x, c.y + 1.35) < 0.36)
    parts.append(emitter)
    # thorax shoulder caps over each fore hinge, so every wing grows OUT of the body (nothing floats)
    for sx in (1, -1):
        cap = f.sphere("shoulder", 0.30, center=(sx * 0.66, -0.72, 0.22), segments=14, rings=8, scale=(1.0, 1.5, 0.8))
        f.ensure_slots(cap, SLOTS)
        f.paint(cap, H)
        f.paint(cap, D, lambda c, n, i: n.z > 0.85)
        hcap = f.sphere("hip", 0.26, center=(sx * 0.62, 1.18, -0.16), segments=14, rings=8, scale=(1.0, 1.4, 0.8))
        f.ensure_slots(hcap, SLOTS)
        f.paint(hcap, H)
        parts += [cap, hcap]

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
    ctx.log("butterfly joined: %d verts, %d tris (n-gons triangulated: %d)" % (len(mesh.data.vertices), f.tri_count(mesh), len(big)))

    # ------------------------------------------------------------------ RIG
    def span_dir(dihedral, sx):
        d = math.radians(dihedral)
        return Vector((sx * math.cos(d), 0.0, math.sin(d)))
    B = rig.BoneSpec
    bones = [B("hull", (0, 0, 0), (0, -1.6, 0))]
    for tag, sx in (("L", 1), ("R", -1)):
        fh = Vector((sx * FORE_HINGE[0], FORE_HINGE[1], FORE_HINGE[2]))
        hh = Vector((sx * HIND_HINGE[0], HIND_HINGE[1], HIND_HINGE[2]))
        bones.append(B("wing." + tag, fh, fh + span_dir(FORE_DIHEDRAL, sx) * 3.0, parent="hull"))
        bones.append(B("wing.2." + tag, hh, hh + span_dir(HIND_DIHEDRAL, sx) * 2.5, parent="hull"))
    arm = rig.build_armature(ARMATURE, bones)
    rig.skin_rigid(mesh, arm, default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]

    # ------------------------------------------------------------------ MORPHS (4-12% of the ~24.6 diagonal)
    ms = morphs.MaskSet(mesh)
    me = mesh.data
    wt = [0.0] * len(me.vertices)
    ws = [0.0] * len(me.vertices)
    if "wt" in me.attributes:
        me.attributes["wt"].data.foreach_get("value", wt)
        me.attributes["ws"].data.foreach_get("value", ws)
    fore = {sx: ms.mask("wing." + t) for t, sx in (("L", 1), ("R", -1))}
    hind = {sx: ms.mask("wing.2." + t) for t, sx in (("L", 1), ("R", -1))}
    body_m = ms.mask("hull")
    ant = [1.0 if (v.co.y < -3.5 and v.co.z > 0.5) else 0.0 for v in me.vertices]
    abdomen = [w * (1.0 if v.co.y > 0.4 else 0.0) for w, v in zip(body_m, me.vertices)]

    def scallop(amount, mask, hinge, sx):
        h = Vector((sx * hinge[0], hinge[1], hinge[2]))

        def fn(i, co, n):
            if mask[i] <= 0 or wt[i] < 0.70:
                return Vector((0, 0, 0))
            k = (wt[i] - 0.70) / 0.30
            lobe = 0.55 + 0.45 * math.cos(2 * math.pi * 7 * ws[i])
            out = (co - h)
            out.z *= 0.3
            return out.normalized() * (amount * k * k * lobe)
        return fn

    shapes = {
        "Charge": [  # the bite: the margins scallop and the apexes flare; the antenna clubs swell forward
            *[scallop(1.25, fore[sx], FORE_HINGE, sx) for sx in (1, -1)],
            *[scallop(0.95, hind[sx], HIND_HINGE, sx) for sx in (1, -1)],
            morphs.stretch_along((0, -1, 0), (0, -3.4, 0), 1.35, ant, ramp=(0.0, 0.5)),
        ],
        "Mass": [  # volume: the wings broaden along their chords and the body fattens
            *[morphs.stretch_along((0, 1, 0), (0, FORE_HINGE[1] - 1.0, 0), 1.22, fore[sx]) for sx in (1, -1)],
            *[morphs.scale_about(1.16, (sx * HIND_HINGE[0], HIND_HINGE[1], HIND_HINGE[2]), hind[sx]) for sx in (1, -1)],
            morphs.scale_about(1.22, (0, 0, 0), body_m, axes=(1, 0, 1)),
        ],
        "Space": [  # reach: every wing extends along its own span from its hinge
            *[morphs.stretch_along(span_dir(FORE_DIHEDRAL, sx), (sx * FORE_HINGE[0], FORE_HINGE[1], FORE_HINGE[2]), 1.22, fore[sx]) for sx in (1, -1)],
            *[morphs.stretch_along(span_dir(HIND_DIHEDRAL, sx), (sx * HIND_HINGE[0], HIND_HINGE[1], HIND_HINGE[2]), 1.20, hind[sx]) for sx in (1, -1)],
        ],
        "Time": [  # rate: the wings sweep back, the abdomen lengthens behind the waist
            *[morphs.sweep((sx, 0, 0), (sx * FORE_HINGE[0], 0, 0), (0, 0.24, 0), fore[sx]) for sx in (1, -1)],
            *[morphs.sweep((sx, 0, 0), (sx * HIND_HINGE[0], 0, 0), (0, 0.20, 0), hind[sx]) for sx in (1, -1)],
            morphs.stretch_along((0, 1, 0), (0, 0.4, 0), 1.32, abdomen, ramp=(0.0, 0.4)),
        ],
    }
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))
    for a in ("wt", "ws"):
        if a in me.attributes:
            me.attributes.remove(me.attributes[a])

    # ------------------------------------------------------------------ DOCUMENTATION POSES (code drives the wings)
    def pose(p):
        a = {"beatup": 40.0, "beatdown": -30.0, "fold": 82.0, "spread": 0.0}[p]
        hind = {"beatup": 0.8, "beatdown": 0.8, "fold": 1.0, "spread": 1.0}[p]
        out = {}
        for tag, sx in (("L", 1), ("R", -1)):
            # +X wing tip UP is a negative rotation about +Y; the R side mirrors
            out["wing." + tag] = (0, -sx * a, 0) if p != "spread" else (0, 0, sx * 10)
            out["wing.2." + tag] = (0, -sx * a * hind, 0) if p != "spread" else (0, 0, sx * 6)
        return out
    names = ("beatup", "beatdown", "fold", "spread")
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
            "sheet_poses": names,
            "pose_expect": {
                "beatup": [("wing.L", "z", +1, 1.5), ("wing.R", "z", +1, 1.5), ("wing.2.L", "z", +1, 0.8)],
                "beatdown": [("wing.L", "z", -1, 1.0), ("wing.R", "z", -1, 1.0)],
                "fold": [("wing.L", "z", +1, 2.0), ("wing.R", "z", +1, 2.0), ("wing.L", "x", -1, 1.5)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            # the placeholder's envelope (Blender frame), which the gameplay was tuned to
            "size_ref_blender": ((-10.67, -5.35, -0.85), (10.67, 6.20, 3.04)), "size_tol": (0.06, 0.06, 0.25),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
        },
    }
