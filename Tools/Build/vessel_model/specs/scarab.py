"""
scarab - the Scarab (VesselClassType.Scarab = 12), a dung-beetle-inspired SPACESHIP that forges and
rolls a ball. The fleet's second hull with no artist file: it replaces the procedural placeholder
(ScarabHullBuilder / ScarabHullForm, 13 parts, 3,019 triangles, two submeshes, built at Awake over a
hidden Sparrow model) with a model built to the fleet convention and the fleet's look.

What the owner said about the placeholder (SCARAB.md section 3.0, Garrett 2026-09-01): "it looks more
like a low poly scarab than a space ship with independent floating parts, but it is a good placeholder."
So the target is a MACHINE that reads as a scarab, not a creature body: every beetle part is a ship
part (the clade-to-ship table below), the parts read as assembled modules - armour shells standing on
a dark engine core, struts on hip housings, masts on sockets, a lance on the prow - with hard bevels,
sunken domain panels and seams between them. Every module is joined to the next one by a mechanical
contact (nothing floats: SKILL.md section 5); whether the owner wants real gaps between them
(SCARAB.md section 15.17) is his call and is reported, not invented here.

Camera: ScarabCameraSettingsSO followOffset (0, 0, -50), straight astern at hull height, 60 degree
vertical FOV: 57.7 units of height fill 1080 px, ~18.7 px per unit, so the 10-unit hull is ~180 px
long and ~185 px wide. The STERN carries the read: the two armoured wing cases with their domain
panels and dark striae, the seam between them, the twin engine mouths glowing under the cases, the
six struts under the shell's rim, the two sensor masts and the horn's tip over the dome. Detail under
~0.3 units is for the menus.

Clade -> ship:
  elytra (wing cases)     -> two hinged armour shells (`wing.L` / `wing.R`), sunken domain panels between
                             raised dark striae ribs, a domain trim line round the rim; they hinge open
                             at the centreline (the puppetry's flare) to show the engine core beneath
  pronotum (thorax shield) -> the forward armour collar: a domain field in a dark frame, a dark dorsal
                             keel and a sunken cyan canopy visor on its brow
  clypeus (toothed head)  -> the dozer prow: a flat ram blade with six teeth (the sacred scarab's six) and
                             a domain trim along its edge - the bumper that shoves balls and pilots
  horn                    -> the forge lance (`horn`): a faceted armoured spar with two domain collars and
                             a lime emitter tip, the pilot's aim instrument
  eyes                    -> two cyan sensor domes under the collar's margin
  antennae (lamellate)    -> sensor masts (`antenna.L/.R`) on sockets, each topped by a three-blade
                             domain sensor fan (the lamellate club)
  legs                    -> six landing struts / manipulator arms (`leg.1..3.L/.R`, 1 = aft as on the
                             placeholder): a socket ball on a hip housing, a femur strut, a domain hinge
                             barrel at the knee, a piston tibia and a clawed foot pad; the fore pair
                             carries the digging teeth of a scarab's front tibia
  abdomen (under the cases) -> the engine core: a dark gunmetal fuselage with a flat ventral keel plate,
                             lime vent slits on each flank and on its back (seen when the cases open),
                             and twin engine nacelles at the stern (`jet.L/.R`, bells on `jet.*.mouth`)
  elytral seam            -> a dark spine rail the two shells close onto

Contract (FLEET_CONVENTION sections 1-4 for a NEW vessel; Scarab integration map 2026-10-18):
  file      Assets/_Models/Vessel Models/Scarab.fbx (new guid), preset "zup": nose -Y, up +Z,
            +X = the pilot's LEFT (Unity (x, y, z) = Blender (-x, z, -y))
  nodes     mesh `mesh`, armature `rig`, root bone `hull`
  bones     each head where the placeholder's puppetry pivots that part (ScarabHullForm.Generate at
            the prefab's settings, run offline through Tools/Build/scarab_icon_harness), Blender frame:
              wing.L / wing.R    (0, 0.409, -0.695)        the elytra's centreline hinge
              horn               (0, -3.191, -0.180)
              antenna.L / .R     (+-1.134, -3.109, -0.367)
              leg.1.L / .R       (+-3.338, 2.700, -1.016)  aft
              leg.2.L / .R       (+-3.434, 0.900, -1.055)  middle
              leg.3.L / .R       (+-2.659, -1.391, -0.979) fore
            plus jet.L / jet.R and their .mouth children at the nozzles (FX mounts, rule 2.5)
  size      the placeholder's, within ~6%: span 9.79 (across the feet), length 10.30 (horn tip to the
            cases' apex), height 4.12 (feet to the antenna fans) - the occlusion corridor and the
            speed-tunnel camera size off the circumscribing radius (6.40 as shipped), the hull trigger
            sphere is r 4.5
  slots     0 Domain (panels, trims, fans, knee barrels), 1 Body, 2 Window (canopy, eyes), 3 Engine
  shapes    Charge, Mass, Space, Time at the convention's 4-12% of the diagonal, the placeholder's
            meanings carried onto the model:
              Charge = the prow's teeth run out and the cases' rims flare into armour (the bite);
              Mass   = the shells dome up, the core and collar fatten, every limb rides out with them;
              Space  = the lance, the sensor masts and the struts reach along their own axes;
              Time   = the cases sweep back and taper to a sharper stern, the masts rake back
  takes     none drive it (ScarabAnimation springs the parts in code); four documentation poses for the
            sheet and the studio: flare (cases open), hang (gear down), tuck (gear up, cases swept as at
            speed), aim (lance and masts up)
"""

import math
from _common import box_uv

NAME = "Scarab"
OUTPUT = "Assets/_Models/Vessel Models/Scarab.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3

# the placeholder's pivots, Blender frame, .L side (+X)
ELYTRA_HINGE = (0.0, 0.409, -0.695)
HORN_PIVOT = (0.0, -3.191, -0.180)
ANTENNA_PIVOT = (1.134, -3.109, -0.367)
LEG_PIVOTS = {1: (3.338, 2.700, -1.016), 2: (3.434, 0.900, -1.055), 3: (2.659, -1.391, -0.979)}
# where each strut bends and stands (.L side): knee, foot
LEG_SHAPE = {1: ((4.15, 3.02, -0.72), (4.40, 3.42, -2.00)),
             2: ((4.25, 0.98, -0.76), (4.48, 1.08, -2.02)),
             3: ((3.60, -1.70, -0.68), (3.90, -2.04, -1.98))}
JET_X, JET_Z = 1.20, -0.50
CORE_Z = -0.45


def _lerp_table(table, y):
    """Piecewise-linear lookup in [(y, value), ...] sorted by y."""
    if y <= table[0][0]:
        return table[0][1]
    for (y0, a), (y1, b) in zip(table, table[1:]):
        if y <= y1:
            t = (y - y0) / (y1 - y0)
            return a + (b - a) * t
    return table[-1][1]


def build(ctx):
    import bpy  # noqa: F401
    import bmesh
    from mathutils import Vector, Matrix
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes

    # ------------------------------------------------------------------ helpers
    def shell(name, stations, thickness, mat_fn):
        """
        A thick armour shell lofted through `stations` = [(y, outer)] where `outer` is the OUTER surface
        section [(x, z), ...] (seam -> rim for a case, rim -> rim for the collar). The inner surface is the
        outer one offset inward along the section's normal by `thickness`, so every station is a closed
        section and the shell is a closed solid. Faces are painted by mat_fn(row, column, n_outer):
        columns 0..n-2 are the outer surface, n-1 the rim edge, n..2n-2 the inner surface, 2n-1 the
        closing edge; caps wear Body.
        """
        bm = bmesh.new()
        rings = []
        n = len(stations[0][1])
        for (y, outer) in stations:
            assert len(outer) == n
            inner = []
            for k in range(n):
                a, b = outer[max(k - 1, 0)], outer[min(k + 1, n - 1)]
                tx, tz = b[0] - a[0], b[1] - a[1]
                L = math.hypot(tx, tz) or 1.0
                nx, nz = -tz / L, tx / L
                inner.append((outer[k][0] - nx * thickness, outer[k][1] - nz * thickness))
            loop = list(outer) + list(reversed(inner))
            rings.append([bm.verts.new((x, y, z)) for (x, z) in loop])
        m = len(rings[0])
        for j in range(len(rings) - 1):
            a, b = rings[j], rings[j + 1]
            for i in range(m):
                i2 = (i + 1) % m
                fc = bm.faces.new((a[i], a[i2], b[i2], b[i]))
                fc.material_index = mat_fn(j, i, n)
        for ring in (rings[0], list(reversed(rings[-1]))):
            fc = bm.faces.new(ring)
            fc.material_index = H
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        obj = f.new_mesh_object(name, bm, smooth=True)
        f.ensure_slots(obj, SLOTS)
        return obj

    # The armour's section: FACETS, not a dome. Corners in (u across, v up) of a seam->rim quarter; each
    # facet is split in two so a paint rule can take half a facet. The creases between facets (13-22
    # degrees) are marked sharp on the shells, so the dome reads as cut plate, the way the fleet's
    # sculpted hulls do, instead of a smooth beetle back.
    FACETS = ((0.0, 1.0), (0.34, 0.965), (0.62, 0.80), (0.82, 0.55), (0.94, 0.22), (1.0, 0.0))

    def section(x0, x1, z_top, z_edge, lip=0.0, facets=FACETS, split=2):
        pts = []
        for (u0, v0), (u1, v1) in zip(facets, facets[1:]):
            for s in range(split):
                t = s / split
                u, v = u0 + (u1 - u0) * t, v0 + (v1 - v0) * t
                pts.append((x0 + (x1 - x0) * u, z_edge + (z_top - z_edge) * v))
        pts.append((x1, z_edge))
        pts.append((x1 - lip * 0.4, z_edge - lip))
        return pts

    def finish(obj, bevel=0.02, sharp=38.0):
        if bevel > 0:
            f.bevel(obj, width=bevel, segments=1, angle_deg=30)
            f.apply_modifiers(obj)
        f.sharpen_by_angle(obj, sharp)
        return obj

    def prim(obj, slot=H):
        f.ensure_slots(obj, SLOTS)
        f.paint(obj, slot)
        return obj

    def assemble(objs, name):
        """Join sub-pieces into ONE part object named `name` whose every vertex is in group `name`."""
        for i, o in enumerate(objs):
            o.name = "tmp_%s_%d" % (name, i)
        part = f.join(objs, name, SLOTS)
        for vg in list(part.vertex_groups):
            part.vertex_groups.remove(vg)
        f.tag_group(part, name)
        return part

    parts = []

    # ------------------------------------------------------------------ ELYTRA: two hinged armour shells
    el_w = [(-0.75, 3.30), (-0.40, 3.47), (0.10, 3.62), (0.60, 3.70), (2.30, 3.70), (2.85, 3.58), (3.30, 3.32),
            (3.75, 2.86), (4.10, 2.20), (4.33, 1.45), (4.47, 0.70)]
    el_top = [(-0.75, 1.22), (-0.40, 1.29), (0.10, 1.37), (0.60, 1.44), (1.20, 1.47), (2.30, 1.37), (2.85, 1.22),
              (3.30, 1.02), (3.75, 0.76), (4.10, 0.50), (4.33, 0.30), (4.47, 0.14)]
    el_edge = [(-0.75, -0.40), (2.85, -0.42), (3.75, -0.32), (4.47, -0.10)]
    # two pairs of close stations make the transverse panel gaps that cut each case into three plates
    GAPS = ((0.98, 1.08), (2.48, 2.58))
    el_ys = [-0.75, -0.40, 0.10, 0.60, 0.98, 1.08, 1.75, 2.48, 2.58, 3.05, 3.45, 3.80, 4.10, 4.33, 4.47]
    rows = len(el_ys) - 1
    # outer columns: 0-1 the seam facet, 2-3 the crown, 4-5 the shoulder, 6-7 the flank, 8-9 the skirt, 10 the rim, 11 the lip

    def elytron_mat(j, i, n):
        y = 0.5 * (el_ys[j] + el_ys[j + 1])
        if i >= n - 1:
            return H                                   # the underside and the closing edges
        if 3.62 < y < 3.95 and i in (6, 7):
            return J                                   # a lime running light at each case's tail
        if j == 0 or y > 4.2:
            return H                                   # dark front lip and trailing rim
        if any(a < y < b for a, b in GAPS):
            return H                                   # the transverse panel gaps
        if i in (2, 3) and y < 3.62:
            return D                                   # the long domain plate on the crown, in three segments
        if i == 9 and y < 4.0:
            return D                                   # the domain trim line along the skirt
        return H

    stations = [(y, section(0.10, _lerp_table(el_w, y), _lerp_table(el_top, y), _lerp_table(el_edge, y), lip=0.10))
                for y in el_ys]
    el = shell("wing.L", stations, 0.17, elytron_mat)
    f.sharpen_by_angle(el, 11)
    finish(el, 0.025)
    f.inset_faces(el, lambda c, n, i: i == D, 0.035, depth=0.035)
    f.inset_faces(el, lambda c, n, i: i == J, 0.03, depth=0.04)
    f.inset_faces(el, lambda c, n, i: i == H and n.z > -0.2 and any(a < c.y < b for a, b in GAPS) and abs(c.x) < 3.62,
                  0.012, depth=0.05)
    f.sharpen_by_angle(el, 11)
    parts.append(el)
    parts.append(f.mirror_part(el, "wing.R", axis="X"))

    # ------------------------------------------------------------------ HULL: core, collar, prow, eyes, housings
    hull_bits = []

    # the engine core under the cases: flat ventral keel, rounded flanks, a flat stern the nacelles leave from
    core_len = 7.0
    core = f.fuselage("core", core_len, nose=(0, -2.70, CORE_Z), axis=(0, 1, 0), segments=24, stations=[
        (0.00, 2.10, 0.52, 0.48, 2.6, 4.0, 0.0), (0.06, 2.40, 0.74, 0.78, 2.6, 4.2, 0.0),
        (0.14, 2.62, 0.90, 0.92, 2.6, 4.4, 0.0), (0.24, 2.80, 0.98, 0.98, 2.6, 4.5, 0.0),
        (0.36, 2.90, 1.02, 1.00, 2.6, 4.5, 0.0), (0.48, 2.95, 1.02, 1.00, 2.6, 4.5, 0.0),
        (0.60, 2.95, 1.00, 1.00, 2.6, 4.5, 0.0), (0.71, 2.88, 0.97, 0.97, 2.6, 4.5, 0.0),
        (0.80, 2.78, 0.93, 0.93, 2.6, 4.4, 0.0), (0.88, 2.62, 0.88, 0.88, 2.6, 4.3, 0.0),
        (0.94, 2.45, 0.82, 0.82, 2.6, 4.2, 0.0), (1.00, 2.25, 0.76, 0.76, 2.6, 4.0, 0.0)])
    prim(core)
    # lime vent slits along each flank, three per side between the strut housings, and three on the back
    for (y0, y1) in ((-0.43, 0.27), (1.47, 2.17), (3.07, 3.57)):
        f.paint(core, J, lambda c, n, i, y0=y0, y1=y1: abs(n.x) > 0.83 and y0 < c.y < y1 and -0.62 < c.z < -0.12)
    for (y0, y1) in ((0.13, 0.83), (1.53, 2.23), (2.93, 3.63)):
        f.paint(core, J, lambda c, n, i, y0=y0, y1=y1: n.z > 0.93 and abs(c.x) < 0.75 and y0 < c.y < y1)
    finish(core, 0.02)
    f.inset_faces(core, lambda c, n, i: i == J, 0.03, depth=0.05)
    f.inset_faces(core, lambda c, n, i: n.z < -0.97 and abs(c.x) < 1.93 and -1.83 < c.y < 3.83, 0.06, depth=0.03)
    hull_bits.append(core)

    # the spine rail the two cases close onto (the elytral seam, dark): it follows the crown, never above it
    rail_ys = [y for y in el_ys if -0.40 <= y <= 4.33]
    rail = f.loft("rail", [f.Station((0, y, 0), [(0.13, _lerp_table(el_top, y) - 0.06), (-0.13, _lerp_table(el_top, y) - 0.06),
                                                  (-0.13, 0.25), (0.13, 0.25)]) for y in rail_ys], smooth=False)
    prim(rail)
    finish(rail, 0.015)
    hull_bits.append(rail)

    # the pronotum: the forward armour collar - two domain plates either side of a dark keel, a dark frame,
    # and a sunken canopy visor on its brow
    pr_ys = [-2.90, -2.62, -2.25, -1.80, -1.30, -0.80, -0.30]
    pr_w = [(-2.90, 2.48), (-2.62, 2.80), (-2.25, 3.10), (-1.80, 3.36), (-1.30, 3.55), (-0.80, 3.66), (-0.30, 3.64)]
    pr_top = [(-2.90, 0.98), (-2.62, 1.18), (-2.25, 1.34), (-1.80, 1.45), (-1.30, 1.52), (-0.80, 1.54), (-0.30, 1.52)]
    pr_rows = len(pr_ys) - 1
    PR_FACETS = ((0.0, 1.0), (0.22, 0.975), (0.50, 0.84), (0.76, 0.54), (0.92, 0.20), (1.0, 0.0))

    def collar_section(y):
        w, top, edge = _lerp_table(pr_w, y), _lerp_table(pr_top, y), -0.38
        half = section(0.0, w, top, edge, lip=0.08, facets=PR_FACETS)   # centre -> +X rim
        right = [(-x, z) for (x, z) in reversed(half[1:])]
        return right + half                                             # -X rim -> centre -> +X rim

    def collar_mat(j, i, n):
        if i >= n - 1:
            return H
        k = abs(i + 0.5 - (n - 1) / 2.0)          # 0.5, 1.5 = the keel facet; 2.5, 3.5 = the crown facet ...
        if j == 0 and 1.0 < k < 4.0:
            return W                              # the canopy visor on the brow, split by the keel
        if j == 0 or j == pr_rows - 1:
            return H                              # dark front and rear frame
        if k < 2.0:
            return H                              # the dorsal keel
        if k < 4.0:
            return D                              # the domain plates
        if k > 7.0 and k < 8.0 and 1 <= j <= pr_rows - 2:
            return D                              # a domain trim line down each flank
        return H
    collar = shell("collar", [(y, collar_section(y)) for y in pr_ys], 0.30, collar_mat)
    f.sharpen_by_angle(collar, 11)
    finish(collar, 0.025)
    f.inset_faces(collar, lambda c, n, i: i == W, 0.04, depth=0.07)
    f.inset_faces(collar, lambda c, n, i: i == D, 0.04, depth=0.035)
    f.sharpen_by_angle(collar, 11)
    hull_bits.append(collar)

    # the head and its clypeus: a flat ram blade under the collar's brow
    head = f.fuselage("head", 2.15, nose=(0, -4.62, CORE_Z), axis=(0, 1, 0), segments=28, stations=[
        (0.00, 1.86, 0.06, 0.06, 3.0, 3.0, 0.00), (0.12, 2.04, 0.15, 0.12, 3.2, 3.0, 0.00),
        (0.30, 2.16, 0.26, 0.22, 3.2, 3.2, 0.02), (0.55, 2.24, 0.38, 0.32, 3.0, 3.2, 0.05),
        (0.80, 2.24, 0.45, 0.38, 3.0, 3.2, 0.08), (1.00, 2.18, 0.48, 0.40, 3.0, 3.2, 0.10)])
    prim(head)
    f.paint(head, D, lambda c, n, i: n.z > 0.5 and -4.47 < c.y < -4.07)    # the blade's domain edge trim
    finish(head, 0.02)
    hull_bits.append(head)

    # six teeth along the blade (the sacred scarab's clypeus), dozer teeth on the ram
    for tx, ln, wd in ((0.36, 0.52, 0.40), (1.04, 0.46, 0.36), (1.66, 0.36, 0.32)):
        for sx in (1, -1):
            tooth = f.plate("tooth", f.blade(ln + 0.18, wd, tip=0.18), 0.12, normal=(0, 0, 1),
                            origin=(sx * tx, -4.44, CORE_Z), right=(0, -1, 0))
            hull_bits.append(prim(tooth))

    # sensor eyes: cyan domes under the collar's margin, each in a dark socket ring
    for sx in (1, -1):
        eye = f.sphere("eye", 0.40, center=(sx * 1.98, -3.08, -0.30), segments=14, rings=8, scale=(0.85, 1.05, 0.85))
        hull_bits.append(prim(eye, W))
        ring = f.lathe("eyering", [(0.30, 0.0), (0.44, 0.0), (0.46, 0.08), (0.40, 0.14), (0.30, 0.14)], segments=14,
                       axis=(sx, 0, 0), origin=(sx * 1.92, -3.08, -0.30))
        hull_bits.append(prim(ring))

    # strut hip housings: one per leg, from inside the core out to the socket
    for k, P in LEG_PIVOTS.items():
        for sx in (1, -1):
            hous = f.lathe("hip", [(0.0, 0.0), (0.30, 0.0), (0.34, 0.08), (0.34, 0.40), (0.37, 0.44), (0.37, 0.58), (0.34, 0.62),
                                   (0.34, 0.72), (0.30, 0.82), (0.0, 0.84)],
                           segments=10, axis=(sx, 0, 0), origin=(sx * (P[0] - 0.80), P[1], P[2]))
            prim(hous)
            hull_bits.append(hous)

    hull = assemble(hull_bits, "hull")
    parts.append(hull)

    # ------------------------------------------------------------------ HORN: the forge lance
    horn_pts = [(0, -2.95, -0.40), (0, -3.50, 0.12), (0, -4.05, 0.56), (0, -4.62, 0.92), (0, -5.15, 1.25), (0, -5.52, 1.55),
                (0, -5.74, 1.80)]
    horn_r = [0.40, 0.34, 0.28, 0.22, 0.16, 0.11, 0.04]
    lance = prim(f.tube_along("lance", horn_pts, horn_r, segments=6))
    f.paint(lance, J, lambda c, n, i: c.y < -5.40)
    finish(lance, 0.015)
    horn_bits = [lance]
    for (a, b, r) in (((0, -3.78, 0.35), (0, -3.98, 0.51), 0.35), ((0, -4.75, 1.00), (0, -4.95, 1.13), 0.25)):
        band = prim(f.tube_along("band", [a, b], [r, r], segments=6), D)
        finish(band, 0.012)
        horn_bits.append(band)
    parts.append(assemble(horn_bits, "horn"))

    # ------------------------------------------------------------------ ANTENNAE: sensor masts with three-blade fans
    def antenna(name):
        S = Vector(ANTENNA_PIVOT)
        pts = [S, S + Vector((0.20, -0.25, 0.58)), S + Vector((0.52, -0.15, 1.20)), S + Vector((0.88, 0.16, 1.58)),
               S + Vector((1.02, 0.36, 1.72))]
        mast = prim(f.tube_along("mast", [tuple(p) for p in pts], [0.11, 0.095, 0.08, 0.07, 0.065], segments=8))
        sock = prim(f.sphere("sock", 0.17, center=tuple(S), segments=12, rings=8))
        bits = [mast, sock]
        top = pts[-1]
        # the lamellate club as a sensor fan: three leaf blades in ONE plane that faces astern, spread 32 degrees
        up = (pts[-1] - pts[-2]).normalized()
        nrm = Vector((0, 1, 0))
        nrm = (nrm - up * nrm.dot(up)).normalized()
        side = nrm.cross(up)
        for ang in (-32, 0, 32):
            a = math.radians(ang)
            dirv = (up * math.cos(a) + side * math.sin(a)).normalized()
            yv = nrm.cross(dirv)
            m = Matrix(((dirv.x, yv.x, nrm.x, top.x), (dirv.y, yv.y, nrm.y, top.y), (dirv.z, yv.z, nrm.z, top.z), (0, 0, 0, 1)))
            blade = f.plate("lamella", f.feather(0.78, 0.30, tip_round=0.7, root_width=0.10, segments=6), 0.07,
                            normal=(0, 0, 1), origin=(0, 0, 0), right=(1, 0, 0))
            prim(blade, D)
            f.transform_mesh(blade, m)
            finish(blade, 0.012)
            bits.append(blade)
        return assemble(bits, name)
    ant = antenna("antenna.L")
    parts.append(ant)
    parts.append(f.mirror_part(ant, "antenna.R", axis="X"))

    # ------------------------------------------------------------------ LEGS: landing struts / manipulator arms
    def leg(name, k):
        P = Vector(LEG_PIVOTS[k])
        K, F = (Vector(v) for v in LEG_SHAPE[k])
        bits = [prim(f.sphere("ball", 0.27, center=tuple(P), segments=10, rings=6))]
        femur = prim(f.tube_along("femur", [tuple(P), tuple(P.lerp(K, 0.5) + Vector((0, 0, 0.06))), tuple(K)], [0.19, 0.17, 0.14], segments=6))
        finish(femur, 0.012)
        bits.append(femur)
        knee = prim(f.lathe("knee", [(0.0, 0.0), (0.20, 0.0), (0.23, 0.05), (0.23, 0.31), (0.20, 0.36), (0.0, 0.36)],
                            segments=10, axis=(0, 1, 0), origin=tuple(K - Vector((0, 0.18, 0)))), D)
        bits.append(knee)
        sleeve_end = K.lerp(F, 0.45)
        sleeve = prim(f.tube_along("sleeve", [tuple(K), tuple(sleeve_end)], [0.135, 0.12], segments=6))
        finish(sleeve, 0.01)
        rod = prim(f.tube_along("rod", [tuple(K.lerp(F, 0.35)), tuple(F + Vector((0, 0, 0.12)))], [0.075, 0.07], segments=6))
        bits += [sleeve, rod]
        # the foot: a clawed pad
        d = (F - K)
        out = Vector((d.x, d.y, 0)).normalized()
        pad = prim(f.plate("pad", f.rounded_rect(0.40, 0.32, 0.09), 0.10, normal=(0, 0, 1), origin=tuple(F), right=tuple(out)))
        finish(pad, 0.012)
        bits.append(pad)
        for s in (-1, 1):
            side = Vector((-out.y, out.x, 0)) * (0.11 * s)
            toe = prim(f.cone("toe", 0.06, 0.18, axis=tuple(out + Vector((0, 0, -0.35))), origin=tuple(F + out * 0.16 + side), segments=5))
            bits.append(toe)
        if k == 3:   # the fore pair: a scarab's digging tibia, three teeth on the outer edge
            for t in (0.55, 0.72, 0.89):
                p = K.lerp(F, t)
                tooth = prim(f.cone("dig", 0.07, 0.24, axis=tuple(out), origin=tuple(p), segments=5))
                bits.append(tooth)
        return assemble(bits, name)
    for k in (1, 2, 3):
        lg = leg("leg.%d.L" % k, k)
        parts.append(lg)
        parts.append(f.mirror_part(lg, "leg.%d.R" % k, axis="X"))

    # ------------------------------------------------------------------ ENGINES: twin nacelles under the cases
    def nacelle_case(name):
        case = prim(f.lathe(name, [(0.0, 0.0), (0.70, 0.0), (0.76, 0.06), (0.76, 0.42), (0.70, 0.50), (0.0, 0.50)],
                            segments=16, axis=(0, 1, 0), origin=(JET_X, 3.55, JET_Z)))
        finish(case, 0.015)
        return case

    def nacelle_bell(name):
        bell = prim(f.lathe(name, [(0.0, 0.0), (0.62, 0.0), (0.68, 0.10), (0.66, 0.38), (0.72, 0.48), (0.66, 0.58),
                                    (0.52, 0.60), (0.44, 0.50), (0.0, 0.46)],
                            segments=18, axis=(0, 1, 0), origin=(JET_X, 4.02, JET_Z)))
        f.paint(bell, J, lambda c, n, i: n.y > 0.4 and math.hypot(c.x - JET_X, c.z - JET_Z) < 0.50)
        f.paint(bell, D, lambda c, n, i: math.hypot(c.x - JET_X, c.z - JET_Z) > 0.62 and c.y > 4.44)
        finish(bell, 0.012)
        return bell
    jc = nacelle_case("jet.L")
    jb = nacelle_bell("jet.L.mouth")
    parts += [jc, jb, f.mirror_part(jc, "jet.R", axis="X"), f.mirror_part(jb, "jet.R.mouth", axis="X")]

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
    f.sharpen_by_angle(mesh, 38)
    box_uv(mesh)
    ctx.log("scarab joined: %d verts, %d tris (n-gons triangulated: %d)" % (len(mesh.data.vertices), f.tri_count(mesh), len(big)))

    # ------------------------------------------------------------------ RIG
    B = rig.BoneSpec
    bones = [B("hull", (0, 0, 0), (0, -1.6, 0))]
    for tag, sx in (("L", 1), ("R", -1)):
        h = Vector(ELYTRA_HINGE)
        bones.append(B("wing." + tag, h, h + Vector((sx * 1.8, 1.8, 1.2)), parent="hull"))
    bones.append(B("horn", HORN_PIVOT, (0, -4.6, 0.9), parent="hull"))
    for tag, sx in (("L", 1), ("R", -1)):
        a = Vector((sx * ANTENNA_PIVOT[0], ANTENNA_PIVOT[1], ANTENNA_PIVOT[2]))
        bones.append(B("antenna." + tag, a, a + Vector((sx * 0.55, -0.15, 1.30)), parent="hull"))
        for k, P in LEG_PIVOTS.items():
            p = Vector((sx * P[0], P[1], P[2]))
            K = Vector(LEG_SHAPE[k][0])
            bones.append(B("leg.%d.%s" % (k, tag), p, Vector((sx * K.x, K.y, K.z)), parent="hull"))
        jet = Vector((sx * JET_X, 3.55, JET_Z))
        bones.append(B("jet." + tag, jet, jet + Vector((0, 0.45, 0)), parent="hull"))
        mouth = Vector((sx * JET_X, 4.62, JET_Z))
        bones.append(B("jet.%s.mouth" % tag, mouth, mouth + Vector((0, 0.5, 0)), parent="jet." + tag))
    arm = rig.build_armature(ARMATURE, bones)
    rig.skin_rigid(mesh, arm, default_bone="hull")
    per = {}
    for poly in mesh.data.polygons:
        g = mesh.vertex_groups[mesh.data.vertices[poly.vertices[0]].groups[0].group].name
        per[g] = per.get(g, 0) + len(poly.vertices) - 2
    ctx.log("tris per part: " + ", ".join("%s %d" % kv for kv in sorted(per.items())))
    bone_names = [b.name for b in arm.data.bones]

    # ------------------------------------------------------------------ MORPHS (4-12% of the ~14.8 diagonal)
    ms = morphs.MaskSet(mesh)
    vs = mesh.data.vertices
    case = {sx: ms.mask("wing." + t) for t, sx in (("L", 1), ("R", -1))}
    cases = ms.mask("wing.L", "wing.R")
    hull_m = ms.mask("hull")
    horn_m = ms.mask("horn")
    ants = {sx: ms.mask("antenna." + t) for t, sx in (("L", 1), ("R", -1))}
    legs = {(k, sx): ms.mask("leg.%d.%s" % (k, t)) for k in (1, 2, 3) for t, sx in (("L", 1), ("R", -1))}
    horn_tip = [w * morphs.smoothstep(-5.25, -5.55, v.co.y) for w, v in zip(horn_m, vs)]
    teeth = [w * (1.0 if (v.co.y < -4.50 and v.co.z < -0.25) else 0.0) for w, v in zip(hull_m, vs)]
    # a case's outer rim band: the last fifth of its width at each station, ramped in
    rim = [w * morphs.smoothstep(2.55, 3.35, abs(v.co.x)) for w, v in zip(cases, vs)]
    zero = Vector((0, 0, 0))

    def ride(mask, delta):
        return morphs.translate(delta, mask)

    def rim_flare(i, co, n):
        w = rim[i]
        if w <= 0:
            return zero
        return Vector((math.copysign(0.55, co.x), 0.0, 0.38)) * w

    MASS_BODY, MASS_DOME = 1.18, 1.28
    mass = [
        morphs.scale_about(MASS_BODY, (0, 0.8, CORE_Z), hull_m, axes=(1, 0, 1)),
        *[morphs.scale_about(MASS_DOME, (0, 1.9, CORE_Z), case[sx], axes=(0, 0, 1)) for sx in (1, -1)],
        *[morphs.scale_about(1.10, (0, 1.9, CORE_Z), case[sx], axes=(1, 0, 0)) for sx in (1, -1)],
    ]
    for (k, sx), m in legs.items():   # every strut rides its socket out with the fattening core
        P = LEG_PIVOTS[k]
        mass.append(ride(m, (sx * P[0] * (MASS_BODY - 1), 0, (P[2] - CORE_Z) * (MASS_BODY - 1))))
    for sx in (1, -1):
        mass.append(ride(ants[sx], (sx * ANTENNA_PIVOT[0] * (MASS_BODY - 1), 0, (ANTENNA_PIVOT[2] - CORE_Z) * (MASS_BODY - 1))))

    def along(a, b):
        return (Vector(b) - Vector(a)).normalized()
    space = [morphs.stretch_along(along(HORN_PIVOT, (0, -5.74, 1.80)), HORN_PIVOT, 1.34, horn_m, ramp=(0.3, 1.2))]
    for sx in (1, -1):
        a = (sx * ANTENNA_PIVOT[0], ANTENNA_PIVOT[1], ANTENNA_PIVOT[2])
        space.append(morphs.stretch_along(along(a, (sx * 2.25, -2.70, 1.70)), a, 1.30, ants[sx]))
    for (k, sx), m in legs.items():
        P = Vector((sx * LEG_PIVOTS[k][0], LEG_PIVOTS[k][1], LEG_PIVOTS[k][2]))
        Fv = Vector((sx * LEG_SHAPE[k][1][0], LEG_SHAPE[k][1][1], LEG_SHAPE[k][1][2]))
        space.append(morphs.stretch_along(along(P, Fv), P, 1.28, m))

    shapes = {
        "Charge": [  # the bite: the prow's teeth run out, the cases' rims flare into armour
            morphs.translate((0, -0.80, 0), teeth),
            rim_flare,
            morphs.scale_about(2.2, (0, -5.60, 1.62), horn_tip),
        ],
        "Mass": mass,
        "Space": space,
        "Time": [  # rate: the cases sweep back and taper to a sharper stern, the masts rake back
            *[morphs.stretch_along((0, 1, 0), (0, 1.0, CORE_Z), 1.16, case[sx], ramp=(0.0, 2.0)) for sx in (1, -1)],
            *[morphs.taper((0, 1, 0), (0, 1.0, CORE_Z), 3.5, 0.74, case[sx]) for sx in (1, -1)],
            *[morphs.sweep((0, 0, 1), (0, 0, -0.4), (0, 0.42, 0), ants[sx]) for sx in (1, -1)],
        ],
    }
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))

    # ------------------------------------------------------------------ DOCUMENTATION POSES (code drives the parts)
    # Signs follow ScarabAnimation (Unity Euler composed in the hull frame), carried into the Blender frame:
    #   flare  - a case's outer rim DROPS about the centreline hinge, the seam opens on top (roll -flare on
    #            the right case, +flare on the left, in Unity); Blender roll about +Y, +X side positive
    #   hang   - struts swing down at their sockets (legHang 42); tuck - up (legTuck 30), and the cases ride
    #            the cruise flare (14) with their tails swept OUT (elytraSweep 16, yaw), as at speed
    #   aim    - lance and masts pitch UP (hornScaler 34, antennaScaler 16)
    def pose(p):
        out = {}
        for tag, sx in (("L", 1), ("R", -1)):
            if p == "flare":
                out["wing." + tag] = (0, sx * 55, 0)
            elif p == "hang":
                for k in (1, 2, 3):
                    out["leg.%d.%s" % (k, tag)] = (0, sx * (42 if k != 2 else 42 * 0.65), 0)
            elif p == "tuck":
                out["wing." + tag] = (0, sx * 14, -sx * 16)
                for k in (1, 2, 3):
                    out["leg.%d.%s" % (k, tag)] = (0, -sx * (30 if k != 2 else 30 * 0.65), 0)
            elif p == "aim":
                out["antenna." + tag] = (-16, 0, 0)
        if p == "aim":
            out["horn"] = (-34, 0, 0)
        return out
    names = ("flare", "hang", "tuck", "aim")
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
                "flare": [("wing.L", "z", -1, 0.8), ("wing.R", "z", -1, 0.8)],
                "hang": [("leg.1.L", "z", -1, 0.25), ("leg.1.R", "z", -1, 0.25), ("leg.3.L", "z", -1, 0.25)],
                "tuck": [("leg.1.L", "z", +1, 0.15), ("leg.1.R", "z", +1, 0.15), ("wing.L", "x", +1, 0.15), ("wing.R", "x", -1, 0.15)],
                "aim": [("horn", "z", +1, 0.4)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            # the placeholder's envelope (Blender frame; ScarabHullForm at the prefab's settings), which the
            # corridor, the camera and the hull trigger were tuned to
            "size_ref_blender": ((-4.896, -5.80, -2.151), (4.896, 4.50, 1.971)), "size_tol": (0.06, 0.06, 0.08),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
        },
    }
