"""
shrike - the second hull with NO artist file: it replaces the procedural placeholder
(ShrikeHullBuilder / ShrikeHullForm, the Shrike Studio's `makeHull`: a hexagonal fuselage, a
head pyramid, a box barrel, two box wings, a box fin and a cone flame; two materials) with a
model built to the fleet's look and the fleet convention (Garrett, 2026-10-18: "they are
spaceships inspired by earth clades").

The clade: the shrike, the "butcher bird" - a compact, big-headed perching hunter with a HOOKED
bill, a black eye-mask, short folded wings with a pale patch, a long graduated tail with pale
edges, and feet that grip a perch (and impale prey on thorns). The ship, part by part:
  hooked bill         -> a hooked RAM PROW (upper mandible) over the Void Lance's barrel (lower
                         mandible); the muzzle brake pokes out just beyond the hook
  black eye-mask      -> a dark VISOR BAND across the cyan canopy, running back along the
                         head's domain cowl like the mask runs from bill through eye
  big head / crown    -> a domain-coloured sensor COWL ahead of the shoulders
  short folded wings  -> swept FINS, layered plates, a domain "wing patch" panel mid-span
  long tail           -> a STABILISER BOOM ending in the main engine nozzle (the flame), with a
                         graduated fan of three stabilisers trimmed in domain on the outer edges
  perching feet       -> two TALON CLAMPS folded under the chest: they deploy when the hull
                         docks to a block (ShrikeAnimation), lime clamp pads in the palms
  pale underparts     -> a domain belly plate; two flank thrusters glow under the wing roots

What reads, from the 30-unit on-axis chase camera (ShrikeCameraSettingsSO followOffset
(0, 0, -30): at 60 degrees and 1080p about 31 px per unit, the hull ~290 px wide; docked the
camera eases back to 150 u, ~6 px per unit): the swept fins and stabiliser fan in silhouette,
the domain cowl, wing patches and tail edges, the canopy with its dark band, three engine glows.

Contract (FLEET_CONVENTION sections 1-4 for a NEW vessel; R_VesselActions/SHRIKE.md section 2):
  file      Assets/_Models/Vessel Models/Shrike.fbx (new guid), preset "zup"
  nodes     mesh `mesh`, armature `rig`; root bone `hull` at the origin (the bank pivots the
            whole Hull transform about it, as it did the placeholder)
  bones     gun        the Void Lance barrel (Charge thickens, Space lengthens it)
            gun.mouth  at the placeholder's Muzzle: Unity (0, -0.75, 6.35) = Blender (0, -6.35, -0.75);
                       the executor draws the lance beam from it; skins the muzzle brake
            wing.L/.R  the swept fins, heads at the fin roots
            leg.L/.R   the talon clamps, heads at the clamp hinges under the chest
            tail       the stabiliser boom and fan
            jet.mouth  the nozzle lip at the placeholder's FlameRoot: Unity (0, 0, -3.5) = Blender (0, 3.5, 0)
            flame      the engine flame, head at the lip; ShrikeAnimation scales it (ember docked,
                       x4 slinging) exactly as it scaled the placeholder's ShrikeFlame child
  size      the placeholder's within ~6%: span 9.17, length 11.77 (muzzle to flame tip), height
            ~2.5 (the 2.6 x 1.6 x 9 hit box, the 3.4 x 2.2 x 10 trigger, the wingtip trails at
            x = +-4.4 and the 3-unit dock radius were tuned to it)
  slots     0 Domain (cowl, wing patches, tail edges, spine, belly), 1 Body, 2 Window (the wraparound visor,
            the scope lens), 3 Engine (nozzle, flame, flank thrusters, muzzle bore, clamp pads)
  shapes    Charge, Mass, Space, Time - the placeholder's meanings at the convention's 4-12%:
            Charge = the lance barrel and muzzle brake thicken, the hooked ram grows (the lance
                     radius is the Charge dial);
            Mass   = the hull fattens about its axis, the talons grow (the nest is the Mass ability);
            Space  = the barrel reaches forward (the lock reach);
            Time   = the fins spread and sweep, the boom and the flame lengthen (the sling)
  takes     none drive it (ShrikeAnimation banks the Hull and scales the flame in code); three
            documentation poses for the sheet and the studio: dock, bankleft, bankright
"""

import math
from _common import box_uv

NAME = "Shrike"
OUTPUT = "Assets/_Models/Vessel Models/Shrike.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3

MUZZLE = (0.0, -6.35, -0.75)      # Blender frame (nose -Y, up +Z, the pilot's left +X)
FLAME_ROOT = (0.0, 3.5, 0.0)
BARREL_Z = -0.75
LEG_HINGE = (0.55, -0.55, -0.80)  # the .L side
WING_ROOT = (0.95, -0.85, 0.20)
WING_DIHEDRAL = 4.0
TAIL_ROOT = (0.0, 0.95, 0.08)


def build(ctx):
    import bpy  # noqa: F401
    import bmesh  # noqa: F401
    from mathutils import Vector, Matrix
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    parts = []
    bone_of = {}   # part object name -> the bone that skins it

    def add(obj, bone, slot=None):
        f.ensure_slots(obj, SLOTS)
        if slot is not None:
            f.paint(obj, slot)
        parts.append(obj)
        bone_of[obj.name] = bone
        return obj

    # ------------------------------------------------------------------ BODY: head, neck, chest, rump
    # one loft nose -Y to rump +Y: a BIG head, a short neck, a deep chest, a rump that hands over to the boom
    y0, y1 = -4.75, 1.30
    L = y1 - y0
    rows = [  # (y, half-width, top, bottom, dy)
        (-4.75, 0.48, 0.50, 0.36, 0.05), (-4.45, 0.82, 0.86, 0.62, 0.08), (-3.95, 1.05, 1.03, 0.76, 0.10),
        (-3.35, 1.08, 1.03, 0.78, 0.10), (-2.80, 0.98, 0.92, 0.78, 0.06), (-2.35, 0.90, 0.82, 0.84, 0.0),
        (-1.75, 1.16, 0.84, 1.00, -0.05), (-0.90, 1.26, 0.84, 1.02, -0.06), (-0.05, 1.12, 0.76, 0.86, -0.02),
        (0.70, 0.80, 0.58, 0.56, 0.03), (1.30, 0.42, 0.36, 0.32, 0.06),
    ]

    def at(y):
        """The loft's outline numbers at y, interpolated between the rows (half-width, top, bottom, dy)."""
        for (ya, *ra), (yb, *rb) in zip(rows, rows[1:]):
            if ya <= y <= yb:
                t = (y - ya) / (yb - ya)
                return [a + (b - a) * t for a, b in zip(ra, rb)]
        return list(rows[0][1:]) if y < rows[0][0] else list(rows[-1][1:])
    # dense stations over the head, so the visor and the mask run as clean bands, not a staircase
    ys = sorted(set([r[0] for r in rows] + [round(-4.75 + 0.14 * k, 3) for k in range(1, 20)]))
    st = [((y - y0) / L, *at(y)[:3], 2.7, 2.3, at(y)[3]) for y in ys]
    body = f.fuselage("hull", L, nose=(0, y0, 0), axis=(0, 1, 0), segments=48, stations=st)
    add(body, "hull", H)

    def ring(c):
        """Where a face sits on the outline: its height as a fraction of the half-height at that y (0 = the
        waterline, 1 = the crown), so a band follows the surface like a painted stripe on a real cowl."""
        w, ht, hb, dy = at(c.y)
        return (c.z - dy) / (ht if c.z >= dy else hb)
    # the cowl: the head wears the domain colour (the shrike's pale crown and throat)
    f.paint(body, D, lambda c, n, i: c.y < -2.62)
    # the visor: a wraparound glass across the head's front and cheeks, sunk into the cowl
    f.inset_faces(body, lambda c, n, i: c.y < -3.02 and -0.13 < ring(c) < 0.81 and (abs(n.x) > 0.35 or n.y < -0.6), 0.04, depth=0.04, slot=W)
    # the MASK: a dark band from the bill's root, across the visor, back past the neck (bill - eye - ear, as on the bird)
    f.inset_faces(body, lambda c, n, i: c.y < -2.10 and 0.29 < ring(c) < 0.55 and (abs(n.x) > 0.25 or n.y < -0.6), 0.03, depth=0.03, slot=H)
    # the belly plate (the pale underparts)
    f.inset_faces(body, lambda c, n, i: n.z < -0.84 and -2.0 < c.y < 0.45, 0.05, depth=0.02, slot=D)
    # flank panels: sunken plates with crisp seams (hard surface, not a lathe)
    f.inset_faces(body, lambda c, n, i: abs(n.x) > 0.62 and -1.95 < c.y < 0.55 and -0.55 < c.z < 0.27, 0.05, depth=0.03, slot=H)
    # back plates either side of the ridge: two sunken armour panels with crisp seams
    f.inset_faces(body, lambda c, n, i: n.z > 0.55 and -2.05 < c.y < 0.38 and 0.2 < abs(c.x) < 0.95, 0.05, depth=0.025, slot=H)
    # shoulder running lights: a domain strip on each flank ahead of the fin root
    f.paint(body, D, lambda c, n, i: abs(n.x) > 0.8 and -2.25 < c.y < -1.62 and 0.28 < ring(c) < 0.47)
    f.sharpen_by_angle(body, 38)
    # the dorsal ridge: a raised domain spine from the neck to the boom
    ridge = f.plate("ridge", [(-2.45, 0.66), (0.95, 0.42), (0.95, 0.52), (-0.9, 0.88), (-2.45, 0.92)], 0.10, normal=(1, 0, 0), right=(0, 1, 0))
    add(ridge, "hull", D)
    # the crown hatch: a dark sensor plate with a cyan slit on the cowl's top
    hatch = f.plate("hatch", [(-4.05, 1.06), (-2.95, 1.0), (-2.95, 1.1), (-3.9, 1.18)], 0.5, normal=(1, 0, 0), right=(0, 1, 0))
    f.bevel(hatch, width=0.03, segments=1, angle_deg=30)
    f.apply_modifiers(hatch)
    add(hatch, "hull", H)

    # ------------------------------------------------------------------ PROW: the hooked ram (upper mandible)
    spine = [(0, -4.45, 0.62), (0, -5.00, 0.52), (0, -5.50, 0.33), (0, -5.88, 0.08), (0, -6.12, -0.18), (0, -6.18, -0.40), (0, -6.06, -0.55)]
    prof = []
    for (w, h) in ((0.58, 0.62), (0.50, 0.52), (0.40, 0.42), (0.31, 0.32), (0.22, 0.22), (0.13, 0.13), (0.04, 0.04)):
        # a pointed arch: a keel on top (the culmen), flat cheeks, a flat underside
        prof.append([(w * math.copysign(abs(math.cos(a)) ** 0.8, math.cos(a)),
                      (h if math.sin(a) > 0 else h * 0.55) * math.copysign(abs(math.sin(a)) ** 1.25 if math.sin(a) > 0 else abs(math.sin(a)) ** 0.6, math.sin(a)))
                     for a in (2 * math.pi * k / 16 for k in range(16))])
    ram = f.spine_loft("ram", spine, prof, up=(0, 0, 1))
    add(ram, "hull", H)
    f.paint(ram, D, lambda c, n, i: n.z > 0.72 and c.y > -5.85)   # the culmen: a domain ridge line
    f.sharpen_by_angle(ram, 35)

    # ------------------------------------------------------------------ THE VOID LANCE (lower mandible)
    barrel = f.lathe("gun", [(0.30, 0.0), (0.30, 0.55), (0.25, 0.75), (0.25, 2.85), (0.30, 2.90), (0.30, 3.25),
                             (0.24, 3.30), (0.21, 4.75)], segments=22, axis=(0, -1, 0), origin=(0, -1.55, BARREL_Z))
    add(barrel, "gun", H)
    f.paint(barrel, D, lambda c, n, i: -4.82 < c.y < -4.43 and abs(n.y) < 0.5)   # the shroud ring
    lens = f.sphere("scope", 0.12, center=(0, -3.5, BARREL_Z + 0.27), segments=12, rings=8, scale=(1, 1.8, 0.8))
    add(lens, "gun", W)
    brake = f.lathe("gun.mouth", [(0.24, 0.0), (0.33, 0.06), (0.33, 0.30), (0.28, 0.34), (0.15, 0.34)],
                    segments=22, axis=(0, -1, 0), origin=(0, -6.31, BARREL_Z))
    add(brake, "gun.mouth", H)
    f.paint(brake, J, lambda c, n, i: n.y < -0.9 and math.hypot(c.x, c.z - BARREL_Z) < 0.2)   # the glowing bore
    f.paint(brake, D, lambda c, n, i: abs(n.y) < 0.5 and -6.52 < c.y < -6.44)
    # the chin strut: the barrel hangs from the head (nothing floats)
    chin = f.plate("chin", [(-4.55, -0.25), (-3.0, -0.25), (-3.0, -0.62), (-4.30, -0.62)], 0.16, normal=(1, 0, 0), right=(0, 1, 0))
    add(chin, "gun", H)

    # ------------------------------------------------------------------ FINS: the short folded wings, swept
    def fin(tag, sx):
        # plan outline (x out, y back) from the root; the trailing edge notched into three primaries
        out = [(0.0, -1.25), (1.0, -0.95), (2.2, -0.25), (3.15, 0.55), (3.62, 1.12), (3.55, 1.40), (3.10, 1.24),
               (2.95, 1.46), (2.45, 1.14), (2.25, 1.32), (1.70, 0.94), (0.95, 0.84), (0.0, 0.80)]
        w = f.plate("wing." + tag, out, 0.20, normal=(0, 0, 1), right=(1, 0, 0))
        for x in (0.7, 1.95, 2.45, 2.85):
            f.cut_plane(w, (x, 0, 0), (1, 0, 0))
        f.cut_plane(w, (0, -1.0, 0), (0.45, -1, 0))
        f.bevel(w, width=0.03, segments=1, angle_deg=30)
        f.apply_modifiers(w)
        f.paint(w, H)
        f.paint(w, D, lambda c, n, i: 1.96 < c.x < 2.44 and abs(n.z) > 0.6)          # the wing patch
        f.paint(w, D, lambda c, n, i: abs(n.z) > 0.6 and (c.y + 1.0) - 0.45 * c.x < -0.02 and c.x > 0.72 and c.y < 0.9)   # leading-edge trim
        d = math.radians(WING_DIHEDRAL)
        rot = Matrix(((math.cos(d), 0, -math.sin(d), 0), (0, 1, 0, 0), (math.sin(d), 0, math.cos(d), 0), (0, 0, 0, 1)))
        f.transform_mesh(w, Matrix.Translation(Vector(WING_ROOT)) @ rot)
        if sx < 0:
            f.transform_mesh(w, Matrix.Scale(-1, 4, Vector((1, 0, 0))))
            w.data.flip_normals()
        add(w, "wing." + tag)
        # the covert plate: a second, shorter plate over the root, the layered look of a folded wing
        cv = f.plate("covert." + tag, [(0.0, -1.1), (0.95, -0.82), (1.75, -0.38), (1.55, 0.32), (0.8, 0.58), (0.0, 0.62)], 0.12,
                     normal=(0, 0, 1), right=(1, 0, 0))
        f.bevel(cv, width=0.025, segments=1, angle_deg=30)
        f.apply_modifiers(cv)
        f.transform_mesh(cv, Matrix.Translation(Vector(WING_ROOT) + Vector((0, 0, 0.13))) @ rot)
        if sx < 0:
            f.transform_mesh(cv, Matrix.Scale(-1, 4, Vector((1, 0, 0))))
            cv.data.flip_normals()
        add(cv, "wing." + tag, H)
        # the shoulder knuckle: a mechanical hinge collar the fin grows out of
        k = f.sphere("shoulder." + tag, 0.32, center=(sx * 1.02, -0.70, 0.26), segments=14, rings=8, scale=(1.0, 1.7, 0.75))
        add(k, "wing." + tag, H)
        f.paint(k, D, lambda c, n, i: n.z > 0.86)
    fin("L", 1)
    fin("R", -1)

    # ------------------------------------------------------------------ TAIL: the stabiliser boom and fan
    boom = f.lathe("tail", [(0.42, 0.0), (0.44, 0.12), (0.44, 0.30), (0.30, 0.42), (0.24, 1.8), (0.27, 1.98), (0.32, 2.10), (0.30, 2.22)],
                   segments=22, axis=(0, 1, 0), origin=TAIL_ROOT)
    add(boom, "tail", H)
    f.paint(boom, D, lambda c, n, i: 2.93 < c.y < 3.13 and abs(n.y) < 0.5)
    bell = f.lathe("jet.mouth", [(0.30, 0.0), (0.38, 0.12), (0.46, 0.30), (0.40, 0.34), (0.20, 0.34)],
                   segments=18, axis=(0, 1, 0), origin=(0, FLAME_ROOT[1] - 0.34, FLAME_ROOT[2]))
    add(bell, "jet.mouth", H)
    f.paint(bell, J, lambda c, n, i: n.y > 0.9)
    flame = f.lathe("flame", [(0.30, 0.0), (0.25, 0.45), (0.13, 1.1), (0.0, 1.6)], segments=18, axis=(0, 1, 0), origin=FLAME_ROOT)
    add(flame, "flame", J)
    # the fan: a dorsal fin and two canted stabilisers, graduated like the shrike's tail, pale-edged
    vfin = f.plate("tailfin", [(1.95, 0.15), (3.30, 0.15), (3.75, 1.38), (3.35, 1.42), (2.45, 0.62)], 0.12, normal=(1, 0, 0), right=(0, 1, 0))
    f.cut_plane(vfin, (0, 0, 1.0), (0, 0.32, 1))
    f.bevel(vfin, width=0.02, segments=1, angle_deg=30)
    f.apply_modifiers(vfin)
    add(vfin, "tail", H)
    f.paint(vfin, D, lambda c, n, i: c.z + 0.32 * c.y > 1.84 and abs(n.x) > 0.5)
    for sx, tag in ((1, "L"), (-1, "R")):
        sb = f.plate("stab." + tag, [(0.0, 2.05), (0.0, 3.45), (1.45, 4.08), (1.85, 3.90), (1.25, 2.95)], 0.10, normal=(0, 0, 1), right=(1, 0, 0))
        f.cut_plane(sb, (1.1, 0, 0), (1, -0.25, 0))
        f.bevel(sb, width=0.02, segments=1, angle_deg=30)
        f.apply_modifiers(sb)
        f.paint(sb, H)
        f.paint(sb, D, lambda c, n, i: c.x - 0.25 * c.y > 0.40 and abs(n.z) > 0.5)
        f.transform_mesh(sb, Matrix.Translation(Vector((0, 0, 0.05))) @ Matrix.Rotation(math.radians(-14), 4, "Y"))
        if sx < 0:
            f.transform_mesh(sb, Matrix.Scale(-1, 4, Vector((1, 0, 0))))
            sb.data.flip_normals()
        add(sb, "tail")

    # ------------------------------------------------------------------ FLANK THRUSTERS under the fin roots
    for sx in (1, -1):
        pod = f.lathe("pod", [(0.10, 0.0), (0.26, 0.22), (0.30, 0.55), (0.30, 1.15), (0.25, 1.32)],
                      segments=20, axis=(0, 1, 0), origin=(sx * 1.00, -0.10, -0.20))
        add(pod, "hull", H)
        f.paint(pod, J, lambda c, n, i: n.y > 0.9)
        f.paint(pod, D, lambda c, n, i: 0.55 < c.y < 0.72 and abs(n.y) < 0.5)

    # ------------------------------------------------------------------ TALON CLAMPS (the perching feet), folded
    def talon(tag, sx):
        hx, hy, hz = sx * LEG_HINGE[0], LEG_HINGE[1], LEG_HINGE[2]
        strut = f.tube_along("leg." + tag, [(hx, hy, hz), (sx * 0.60, 0.05, -0.97), (sx * 0.62, 0.52, -1.0)], [0.13, 0.11, 0.10], segments=10)
        add(strut, "leg." + tag, H)
        hub = f.sphere("knuckle." + tag, 0.17, center=(sx * 0.62, 0.58, -1.0), segments=12, rings=8)
        add(hub, "leg." + tag, H)
        f.paint(hub, D, lambda c, n, i: n.z > 0.5)
        pad = f.sphere("pad." + tag, 0.10, center=(sx * 0.62, 0.66, -1.12), segments=10, rings=6, scale=(1.2, 1.0, 0.5))
        add(pad, "leg." + tag, J)
        for k, dx in enumerate((-0.13, 0.0, 0.13)):
            x = sx * (0.62 + dx)
            claw = f.tube_along("claw.%s%d" % (tag, k), [(x, 0.62, -1.0), (x, 0.92, -1.06), (x, 1.06, -1.13), (x, 1.02, -1.20)],
                                [0.065, 0.05, 0.035, 0.012], segments=8)
            add(claw, "leg." + tag, H)
    talon("L", 1)
    talon("R", -1)

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
    ctx.log("shrike joined: %d verts, %d tris (n-gons triangulated: %d)" % (len(mesh.data.vertices), f.tri_count(mesh), len(big)))

    # masks by PART, read before the skin folds the sub-parts into their bones
    ms = morphs.MaskSet(mesh)
    group = lambda *names: ms.mask(*names)
    m_body = group("hull", "ridge", "hatch", "pod", "pod.001", "chin")
    m_ram = group("ram")
    m_gun = group("gun", "gun.mouth", "scope")
    m_brake = group("gun.mouth")
    m_wing = {1: group("wing.L", "shoulder.L", "covert.L"), -1: group("wing.R", "shoulder.R", "covert.R")}
    m_leg = {}
    m_leg[1] = ms.mask(*[n for n in ms.names if n in ("leg.L", "knuckle.L", "pad.L") or n.startswith("claw.L")])
    m_leg[-1] = ms.mask(*[n for n in ms.names if n in ("leg.R", "knuckle.R", "pad.R") or n.startswith("claw.R")])
    m_tail = group("tail", "tailfin", "stab.L", "stab.R", "jet.mouth")
    m_flame = group("flame")

    # ------------------------------------------------------------------ RIG
    B = rig.BoneSpec
    lh = lambda sx: Vector((sx * LEG_HINGE[0], LEG_HINGE[1], LEG_HINGE[2]))
    bones = [
        B("hull", (0, 0, 0), (0, -1.6, 0)),
        B("gun", (0, -1.55, BARREL_Z), (0, -3.0, BARREL_Z), parent="hull"),
        B("gun.mouth", MUZZLE, (MUZZLE[0], MUZZLE[1] - 0.6, MUZZLE[2]), parent="gun"),
        B("tail", TAIL_ROOT, (0, 2.6, TAIL_ROOT[2]), parent="hull"),
        B("jet.mouth", FLAME_ROOT, (0, FLAME_ROOT[1] + 0.5, FLAME_ROOT[2]), parent="tail"),
        B("flame", FLAME_ROOT, (0, FLAME_ROOT[1] + 1.2, FLAME_ROOT[2]), parent="jet.mouth"),
    ]
    for tag, sx in (("L", 1), ("R", -1)):
        root = Vector((sx * WING_ROOT[0], WING_ROOT[1], WING_ROOT[2]))
        bones.append(B("wing." + tag, root, root + Vector((sx * 2.0, 1.0, 0.1)), parent="hull"))
        bones.append(B("leg." + tag, lh(sx), lh(sx) + Vector((0, 0.9, -0.15)), parent="hull"))
    arm = rig.build_armature(ARMATURE, bones)
    rig.skin_rigid(mesh, arm, group_to_bone=dict(bone_of), default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]

    # ------------------------------------------------------------------ MORPHS (4-12% of the ~14 diagonal)
    me = mesh.data
    barrel_axis = (0, -1, 0)
    barrel_root = (0, -1.55, BARREL_Z)
    shapes = {
        "Charge": [  # the lance grows: the barrel and brake thicken, the hooked ram swells and drops its hook
            morphs.scale_about(1.85, barrel_root, m_gun, axes=(1, 0, 1)),
            morphs.scale_about(1.38, (0, -4.55, 0.40), m_ram),
        ],
        "Mass": [  # volume: the hull fattens about its axis, the clamps grow (the nest is Mass's)
            morphs.scale_about(1.30, (0, 0, 0), m_body, axes=(1, 0, 1)),
            *[morphs.translate((sx * 0.32, 0, 0), m_wing[sx]) for sx in (1, -1)],
            *[morphs.scale_about(1.45, (sx * LEG_HINGE[0], LEG_HINGE[1], LEG_HINGE[2]), m_leg[sx]) for sx in (1, -1)],
        ],
        "Space": [  # reach: the lance extends forward
            morphs.stretch_along(barrel_axis, barrel_root, 1.18, m_gun, ramp=(2.4, 3.6)),
            morphs.translate((0, -0.75, 0), m_brake),
        ],
        "Time": [  # rate: the fins spread and sweep back, the boom stretches, the flame lengthens
            *[morphs.stretch_along((sx, 0, 0), (sx * WING_ROOT[0], 0, 0), 1.16, m_wing[sx]) for sx in (1, -1)],
            *[morphs.sweep((sx, 0, 0), (sx * WING_ROOT[0], 0, 0), (0, 0.16, 0), m_wing[sx]) for sx in (1, -1)],
            morphs.stretch_along((0, 1, 0), TAIL_ROOT, 1.12, [max(a, b) for a, b in zip(m_tail, m_flame)]),
            morphs.stretch_along((0, 1, 0), FLAME_ROOT, 1.55, m_flame),
        ],
    }
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))

    # ------------------------------------------------------------------ DOCUMENTATION POSES (code drives the hull)
    poses = {
        "dock": {"leg.L": (-80, 0, 0), "leg.R": (-80, 0, 0)},     # the clamps swing down to grip
        "bankleft": {"hull": (0, 24, 0)},                         # the lean ShrikeAnimation gives a full-stick turn
        "bankright": {"hull": (0, -24, 0)},
    }
    names = ("dock", "bankleft", "bankright")
    for p in names:
        takes.pose_take(arm, p, poses[p])
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
                "dock": [("leg.L", "z", -1, 0.3), ("leg.R", "z", -1, 0.3)],
                "bankleft": [("wing.L", "z", -1, 0.6), ("wing.R", "z", +1, 0.6)],
                "bankright": [("wing.R", "z", -1, 0.6), ("wing.L", "z", +1, 0.6)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            # the placeholder's envelope (Blender frame, flame included), which the gameplay was tuned to
            "size_ref_blender": ((-4.58, -6.67, -1.10), (4.58, 5.10, 1.40)), "size_tol": (0.06, 0.06, 0.12),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
        },
    }
