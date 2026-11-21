"""
brittlestar - the Brittle Star (VesselClassType.BrittleStar = 16), a NEW hull with no artist file: it replaces
the procedural placeholder (BrittleStarHullBuilder / BrittleStarHullForm, 844 flat triangles, two materials) with
a model to the fleet convention and look (the Butterfly path, PIPELINE "The new-hull path", LESSONS 44).

What it must read as, from its camera (BrittleStarCameraSettingsSO followOffset (0, 0, -38), straight BEHIND with
no height, 60 deg FOV: ~24.6 px per unit at 1080p, so the 12-unit ring is ~300 px wide and detail down to ~0.1 u
reads): a SPACESHIP that is a brittle star seen from its aboral side. The camera sees the REAR faces, so the
rear is the dorsal side: the aboral disc with its five pairs of radial shields round the main engine, five long
segmented arms whose dorsal plates carry the domain colour, the pentagon halo that frames the crosshair, and the
five thruster mouths that glow.

Clade -> ship (every part answers "what is this on the ship?"):
  central disc        the hull core: an armoured five-lobed disc, the main engine in its aboral centre
  radial shields      five pairs of domain plates on the disc's rear face, one pair at each arm's root
  oral jaws / mouth   the forward cockpit pod: five interradial jaw plates converging on the nose gun
  (the eye spot)      the canopy: a cyan sensor dome on the cockpit pod, standing above the halo's collar
  arm base            a slew-ring BEARING COLLAR round the disc: the ring assembly turns on it (the firing rotation)
  arms / ossicles     five segmented booms, each ossicle an armour block (domain dorsal plate) on a knuckle joint
  arm spines          short radiator spines on each ossicle's flanks (Charge bristles them)
  tube feet           the thruster nacelles at the arm tips, their bells glowing, their plumes the hull's flames
  (the ring)          a structural halo through the five arm tips: the gun ring Ring Detonation throws
  (the weapons)       a gun per arm tip and one on the nose, each a shrouded barrel with a muzzle brake

Contract (FLEET_CONVENTION 1-4 for a NEW vessel; the placeholder's numbers, which the gameplay was tuned to):
  file    Assets/_Models/Vessel Models/BrittleStar.fbx (new guid), preset "zup": nose -Y, up +Z, pilot's left +X.
          Unity (x, y, z) = Blender (-x, z, -y); this spec writes positions in the UNITY frame through U().
  nodes   mesh `mesh`, armature `rig`, root bone `hull` (the disc, the cockpit pod, the canopy) at the origin.
  bones   ring        head (0, 0, -0.35) Unity: the placeholder's Ring pivot. BrittleStarAnimation spins it about
                      the nose axis (the firing rotation) and scales it (thrown: gone; rebuilt: blooms back);
                      everything it parents is what Ring Detonation throws (halo, collar, arms, pods, guns).
          arm.1..5    one per point, arm.N = the placeholder's point N-1 (point 0 straight up, then counter-
                      clockwise as seen from behind: upper-left, lower-left, lower-right, upper-right).
          jet.1..5    the nacelle at each point; jet.N.mouth at the nozzle lip (Unity z -0.85, the placeholder's
                      flame root) and jet.N.flame, the plume, which the animation lengthens by bone scale.
          gun.1..5    the barrel; gun.N.mouth at the muzzle (Unity z 0.9 = BrittleStarHullForm.MuzzlePosition).
          jet / jet.mouth / jet.flame / gun / gun.mouth: the cockpit's engine (nozzle z -0.9) and nose gun
                      (muzzle z 1.65), the placeholder's index -1.
          Every bone skins geometry (the .meta's optimizeBones 1 strips one that skins nothing).
  size    the placeholder's within ~6%: x +-5.954, y -5.074..6.26, z -0.993..1.685, plus its six unit flames to
          z -1.9 (Unity); ring radius 6 and half-thickness 0.22 (Ring Detonation's disk), muzzles and nozzles exact.
  slots   0 Domain (radial shields, dorsal arm plates, halo trim, nacelle bands), 1 Body, 2 Window (canopy),
          3 Engine (bells, plumes).
  shapes  the placeholder's art direction (BRITTLE_STAR.md 2) at the convention's 4-12% travel, and the SAME
          muzzle travel as BrittleStarHullForm.Extreme, so the muzzles the code places from the form sit on the
          drawn muzzles at every weight:
            Charge = the pods (x1.5 girth, x1.3 length) and barrels (x1.6 girth, carried 0.3 forward) grow, and
                     the arm spines bristle (the threat);
            Mass   = the cockpit grows (x1.25 length from its tail, the pod x1.45 girth, the disc x1.12);
            Space  = the barrels lengthen x2.2 (reach);
            Time   = the halo curves from a pentagon into a circle (the Zephyr's ring).
  takes   none drive it (BrittleStarAnimation writes the bones in code); three documentation poses for the sheet:
          bankright / bankleft (the hull's 28-degree lean) and ringturn (the ring turned 36 degrees).
"""

import math
from _common import box_uv

NAME = "BrittleStar"
OUTPUT = "Assets/_Models/Vessel Models/BrittleStar.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3

POINTS = 5
RING_R = 6.0          # BrittleStarHullForm.Defaults.RingRadius
RING_Z = -0.35        # .RingZ
RING_HALF = 0.22      # .StrutThickness (Ring Detonation's disk half-thickness)
POD_TAIL = -0.85      # .PodTailZ (the flame roots)
POD_LEN = 1.0         # .PodLength
BARREL_LEN = 0.75     # .BarrelLength (muzzle at POD_TAIL + POD_LEN + BARREL_LEN = 0.9)
CP_TAIL = -0.9        # .CockpitTailZ
CP_LEN = 2.1          # .CockpitLength (nose at 1.2; nose muzzle at 1.2 + 0.6 * 0.75 = 1.65)
FLAME_R = 0.2         # .FlameRadius (the cockpit's is x1.4)


def U(x, y, z):
    """Unity hull space (x right, y up, z forward) -> the Blender frame the zup preset writes."""
    from mathutils import Vector
    return Vector((-x, -z, y))


def point_angle(i):
    return math.pi * 0.5 + i * 2 * math.pi / POINTS


def build(ctx):
    import bpy
    import bmesh
    from mathutils import Vector
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    BACK = Vector((0, 1, 0))
    FWD = Vector((0, -1, 0))
    by_bone = {}

    def add(bone, obj):
        f.ensure_slots(obj, SLOTS)
        by_bone.setdefault(bone, []).append(obj)
        return obj

    def set_attr(obj, name, values):
        me = obj.data
        a = me.attributes.get(name) or me.attributes.new(name, "FLOAT", "POINT")
        a.data.foreach_set("value", values)

    def radial(i):
        a = point_angle(i)
        return math.cos(a), math.sin(a)

    def chamfer_rect(w, h, c):
        return [(w, -h + c), (w, h - c), (w - c, h), (-w + c, h), (-w, h - c), (-w, -h + c), (-w + c, -h), (w - c, -h)]

    # ------------------------------------------------------------------ the disc (hull): aboral dome, rim, oral cockpit
    NSEG = 60

    def lobed(R, lobe):
        """The disc's outline in the Unity x/y plane: five lobes, one at each arm (point 0 straight up)."""
        pts = []
        for k in range(NSEG):
            t = 2 * math.pi * k / NSEG
            pts.append((R * (1 + lobe * math.cos(5 * (t - math.pi / 2))) * math.cos(t),
                        R * (1 + lobe * math.cos(5 * (t - math.pi / 2))) * math.sin(t)))
        return pts

    def pent(R, roundness):
        """A rounded pentagon whose FLATS face the arms (its corners interradial: the jaws sit between the arms)."""
        pts = []
        apothem = R * math.cos(math.pi / 5)
        mid = (R + apothem) / 2
        for k in range(NSEG):
            t = 2 * math.pi * k / NSEG
            rel = (t - math.pi / 2 + math.pi / 5) % (2 * math.pi / 5) - math.pi / 5   # 0 on a flat's normal (an arm)
            r = roundness * mid + (1 - roundness) * apothem / math.cos(rel)
            pts.append((r * math.cos(t), r * math.sin(t)))
        return pts

    right, up = U(1, 0, 0), U(0, 1, 0)
    disc_st = [  # (Unity z, outline)
        (-0.86, lobed(0.44, 0.0)), (-0.84, lobed(0.74, 0.04)), (-0.79, lobed(1.08, 0.08)), (-0.70, lobed(1.32, 0.09)),
        (-0.58, lobed(1.42, 0.09)), (-0.10, lobed(1.42, 0.09)), (0.04, lobed(1.30, 0.07)), (0.16, pent(1.05, 0.55)),
        (0.42, pent(0.86, 0.35)), (0.78, pent(0.62, 0.30)), (1.06, pent(0.36, 0.35)), (1.20, pent(0.14, 0.5)),
    ]
    disc = f.loft("hull", [f.Station(U(0, 0, z), pts, right=right, up=up) for z, pts in disc_st], cap_start=True, cap_end=True)
    f.paint(disc, H)

    def aboral(c, n, lo, hi, rlo, rhi):
        if n.y < 0.30:  # rear-facing in Blender is +Y
            return False
        ux, uy = -c.x, c.z
        r = math.hypot(ux, uy)
        if not (rlo < r < rhi):
            return False
        a = math.degrees(math.atan2(uy, ux)) - 90.0
        rel = (a + 36.0) % 72.0 - 36.0      # 0 on an arm, +-36 interradial
        return lo < abs(rel) < hi
    # the radial shields: a pair of sunken domain plates on the aboral face at each arm's root
    f.inset_faces(disc, lambda c, n, m: aboral(c, n, 6.0, 24.0, 0.62, 1.30), 0.035, depth=0.03, slot=D)
    # the sensor lenses between them (the brittle star's photoreceptor lenses): five cyan windows, interradial
    f.inset_faces(disc, lambda c, n, m: m == H and aboral(c, n, 30.0, 37.0, 0.75, 1.05), 0.03, depth=0.02, slot=W)
    # the oral jaws: a domain edge along each of the five jaw seams (the pentagon's interradial corners)
    def jaw_seam(c, n, i):
        uz = -c.y
        if not (0.20 < uz < 1.0):
            return False
        a = math.degrees(math.atan2(c.z, -c.x)) - 90.0
        rel = (a % 72.0) - 36.0
        return abs(rel) < 4.6
    f.paint(disc, D, jaw_seam)
    add("hull", disc)
    # the canopy: a cyan sensor dome on the cockpit pod, standing clear of the bearing collar seen from behind
    canopy = f.sphere("canopy", 0.46, center=tuple(U(0, 1.22, 0.38)), segments=20, rings=12, scale=(0.78, 1.55, 1.0))
    f.paint(canopy, W)
    f.paint(canopy, H, lambda c, n, i: c.z < 1.06)
    add("hull", canopy)
    # the canopy's spine fairing back to the disc
    fair = f.tube_along("fairing", [tuple(U(0, 0.95, 0.75)), tuple(U(0, 1.18, 0.30)), tuple(U(0, 1.14, -0.05))], [0.14, 0.22, 0.12], segments=10)
    f.paint(fair, H)
    add("hull", fair)

    # cockpit engine (index -1): housing, bell with the glow, and the plume
    housing = f.lathe("jet", [(0.40, 0.0), (0.47, 0.04), (0.47, 0.10), (0.40, 0.13)], segments=20, axis=tuple(BACK), origin=tuple(U(0, 0, -0.74)))
    f.paint(housing, H)
    f.paint(housing, D, lambda c, n, i: n.y > 0.6)
    add("jet", housing)
    bell = f.lathe("jet.mouth", [(0.34, 0.0), (0.40, 0.14), (0.33, 0.15), (0.26, 0.05)], segments=20, axis=tuple(BACK), origin=tuple(U(0, 0, CP_TAIL + 0.15)))
    f.paint(bell, H)
    f.paint(bell, J, lambda c, n, i: math.hypot(c.x, c.z) < 0.325)
    add("jet.mouth", bell)
    plume = f.cone("jet.flame", FLAME_R * 1.4, 1.0, axis=tuple(BACK), origin=tuple(U(0, 0, CP_TAIL)), segments=10)
    f.paint(plume, J)
    add("jet.flame", plume)
    # the nose gun
    ng0 = CP_TAIL + CP_LEN - CP_LEN * 0.15          # 0.885
    nmz = CP_TAIL + CP_LEN + BARREL_LEN * 0.6       # 1.65: MuzzlePosition(-1)
    ngun = f.lathe("gun", [(0.16, 0.0), (0.16, 0.22), (0.11, 0.30), (0.085, 0.36), (0.085, nmz - ng0)], segments=12, axis=tuple(FWD), origin=tuple(U(0, 0, ng0)))
    f.paint(ngun, H)
    add("gun", ngun)
    nbrake = f.lathe("gun.mouth", [(0.10, 0.0), (0.13, 0.03), (0.13, 0.13), (0.06, 0.13), (0.06, 0.09)], segments=12, axis=tuple(FWD), origin=tuple(U(0, 0, nmz - 0.13)))
    f.paint(nbrake, H)
    add("gun.mouth", nbrake)

    # ------------------------------------------------------------------ the bearing collar (ring): the slew ring
    col_st = [(1.56, -0.50), (1.88, -0.50), (1.98, -0.42), (1.98, -0.12), (1.88, -0.04), (1.56, -0.04)]
    circ = lambda r: [(r * math.cos(2 * math.pi * k / NSEG), r * math.sin(2 * math.pi * k / NSEG)) for k in range(NSEG)]
    collar = f.loft("ring_collar", [f.Station(U(0, 0, z), circ(r), right=right, up=up) for r, z in col_st], close_loop=True)
    f.paint(collar, H)
    add("ring", collar)

    # ------------------------------------------------------------------ the halo (ring): a pentagon beam, Time rounds it
    K = 8
    HR, HA, HC = 0.27, RING_HALF, 0.08    # radial half-width, axial half-depth (the ring's), chamfer
    GW, GD = 0.10, 0.06                   # the rear face's sunken domain channel: half-width, depth
    # the beam's section (u along the outward normal, v along BACK): a chamfered box whose rear face carries a channel
    sect = [(HR, -HA + HC), (HR, HA - HC), (HR - HC, HA), (GW + 0.035, HA), (GW, HA - GD), (-GW, HA - GD),
            (-GW - 0.035, HA), (-HR + HC, HA), (-HR, HA - HC), (-HR, -HA + HC), (-HR + HC, -HA), (HR - HC, -HA)]
    corners = [(RING_R * math.cos(point_angle(i)), RING_R * math.sin(point_angle(i))) for i in range(POINTS)]
    bm = bmesh.new()
    lay = {k: bm.verts.layers.float.new(k) for k in ("h_on", "h_s", "h_t", "h_u", "h_v")}
    rings_v = []
    for s in range(POINTS):
        p0, p1 = corners[s], corners[(s + 1) % POINTS]
        am = (point_angle(s) + point_angle(s + 1)) / 2
        nrm = (math.cos(am), math.sin(am))
        for k in range(K):
            t = k / K
            cx, cy = p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t
            if k == 0:   # the corner: a mitre along the bisector (the corner's radial)
                a0 = point_angle(s)
                d, sc = (math.cos(a0), math.sin(a0)), 1.0 / math.cos(math.pi / 5)
            else:
                d, sc = nrm, 1.0
            ring = []
            for (u, v) in sect:
                pos = U(cx + d[0] * u * sc, cy + d[1] * u * sc, RING_Z) + BACK * v
                bv = bm.verts.new(pos)
                for key, val in (("h_on", 1.0), ("h_s", float(s)), ("h_t", t), ("h_u", u), ("h_v", v)):
                    bv[lay[key]] = val
                ring.append(bv)
            rings_v.append(ring)
    n = len(sect)
    for r in range(len(rings_v)):
        a, b = rings_v[r], rings_v[(r + 1) % len(rings_v)]
        for j in range(n):
            fc = bm.faces.new((a[j], a[(j + 1) % n], b[(j + 1) % n], b[j]))
            v_mid = (sect[j][1] + sect[(j + 1) % n][1]) / 2
            u_mid = (sect[j][0] + sect[(j + 1) % n][0]) / 2
            fc.material_index = D if (abs(u_mid) < GW + 0.03 and v_mid > HA - GD - 1e-4) else H
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    halo = f.new_mesh_object("ring_halo", bm, smooth=False)
    add("ring", halo)

    # ------------------------------------------------------------------ the five arms: ossicles, knuckles, spines
    N_OSS = 8
    R0, R1 = 1.86, RING_R - 0.26
    CURL = 0.55          # the arms flex: every arm bows the same way round (a brittle star's sinuous reach)
    pitch = (R1 - R0) / N_OSS

    def arm_frame(i, r):
        """Centre, tangent and in-plane side (Blender vectors) of arm i at radius r along its bowed line."""
        rx, ry = radial(i)
        s_ = (r - R0) / (R1 - R0)
        lat = CURL * math.sin(math.pi * min(max(s_, 0.0), 1.0))
        dlat = CURL * math.pi / (R1 - R0) * math.cos(math.pi * min(max(s_, 0.0), 1.0))
        cx, cy = rx * r - ry * lat, ry * r + rx * lat
        tx, ty = rx - ry * dlat, ry + rx * dlat
        tl = math.hypot(tx, ty)
        tx, ty = tx / tl, ty / tl
        return U(cx, cy, RING_Z), U(tx, ty, 0), U(-ty, tx, 0)

    for i in range(POINTS):
        bone = "arm.%d" % (i + 1)
        # the root socket: a clevis collar where the arm seats in the bearing collar
        c0, t0, _ = arm_frame(i, R0)
        sock = f.lathe("sock", [(0.40, 0.0), (0.44, 0.06), (0.44, 0.26), (0.36, 0.34)], segments=12, axis=tuple(t0), origin=tuple(c0 - t0 * 0.16))
        f.paint(sock, H)
        f.paint(sock, D, lambda c, n_, k, t0=t0: abs(n_.dot(t0)) < 0.3 and n_.y > 0.5)
        add(bone, sock)
        for k in range(N_OSS):
            r_a = R0 + k * pitch + 0.03
            r_b = r_a + pitch - 0.09
            q = k / (N_OSS - 1)
            w = 0.40 + (0.21 - 0.40) * q
            h = 0.24 + (0.15 - 0.24) * q
            prof = chamfer_rect(w, h, 0.07)
            small = [(x * 0.80, y * 0.84) for (x, y) in prof]
            st = []
            for rr, pr in ((r_a, small), (r_a + 0.06, prof), (r_b - 0.06, prof), (r_b, small)):
                c_, t_, side_ = arm_frame(i, rr)
                st.append(f.Station(c_, pr, right=side_, up=BACK))
            oss = f.loft("oss", st, smooth=False)
            f.paint(oss, H)
            f.inset_faces(oss, lambda c, n_, m: n_.y > 0.93, 0.05, depth=0.03, slot=D)
            add(bone, oss)
            if k < N_OSS - 1:   # the knuckle joint to the next ossicle
                ck, tk, _ = arm_frame(i, r_b - 0.02)
                kn = f.lathe("knuckle", [(w * 0.55, 0.0), (w * 0.62, 0.05), (w * 0.55, 0.12)], segments=10, axis=tuple(tk), origin=tuple(ck))
                f.paint(kn, H)
                add(bone, kn)
            # the arm spines: a radiator spine on each flank, raked toward the tip (Charge bristles them)
            cm, tm, sm = arm_frame(i, (r_a + r_b) / 2)
            for side in (1, -1):
                d = (sm * side + tm * 0.55).normalized()
                root = cm + sm * (side * (w - 0.05))
                L = 0.30 * (1.0 - 0.35 * q)
                sp = f.cone("spine", 0.07 * (1.0 - 0.3 * q), L, axis=tuple(d), origin=tuple(root), segments=5)
                f.paint(sp, H)
                vals_d = [max(0.0, (v.co - root).dot(d)) for v in sp.data.vertices]
                set_attr(sp, "s_d", vals_d)
                for ax, key in ((0, "s_x"), (1, "s_y"), (2, "s_z")):
                    set_attr(sp, key, [d[ax]] * len(sp.data.vertices))
                add(bone, sp)

    # ------------------------------------------------------------------ the five tips: nacelles, bells, plumes, guns
    mz = POD_TAIL + POD_LEN + BARREL_LEN   # 0.9: MuzzlePosition(i).z
    for i in range(POINTS):
        n1 = i + 1
        px, py = RING_R * radial(i)[0], RING_R * radial(i)[1]
        front = POD_TAIL + POD_LEN
        pod = f.lathe("pod", [(0.11, 0.0), (0.22, 0.04), (0.32, 0.16), (0.36, 0.34), (0.36, 0.64), (0.34, 0.74), (0.26, 0.86)],
                      segments=14, axis=tuple(BACK), origin=tuple(U(px, py, front)))
        f.paint(pod, H)
        f.paint(pod, D, lambda c, n_, k, y0=-front: y0 + 0.40 < c.y < y0 + 0.56 or n_.y > 0.45)
        add("jet.%d" % n1, pod)
        bell = f.lathe("bell", [(0.26, 0.0), (0.30, 0.14), (0.25, 0.15), (0.19, 0.05)], segments=14, axis=tuple(BACK),
                       origin=tuple(U(px, py, POD_TAIL + 0.15)))
        f.paint(bell, H)
        c0 = U(px, py, 0)
        f.paint(bell, J, lambda c, n_, k, c0=c0: math.hypot(c.x - c0.x, c.z - c0.z) < 0.245)
        add("jet.%d.mouth" % n1, bell)
        pl = f.cone("plume", FLAME_R, 1.0, axis=tuple(BACK), origin=tuple(U(px, py, POD_TAIL)), segments=8)
        f.paint(pl, J)
        add("jet.%d.flame" % n1, pl)
        g0 = front - 0.1 * POD_LEN
        gun = f.lathe("gun", [(0.15, 0.0), (0.15, 0.30), (0.11, 0.37), (0.08, 0.42), (0.08, mz - g0)], segments=12,
                      axis=tuple(FWD), origin=tuple(U(px, py, g0)))
        f.paint(gun, H)
        f.paint(gun, D, lambda c, n_, k, y0=-g0: y0 - 0.26 < c.y < y0 - 0.14 and abs(n_.y) < 0.5)
        add("gun.%d" % n1, gun)
        brake = f.lathe("brake", [(0.09, 0.0), (0.12, 0.03), (0.12, 0.12), (0.055, 0.12), (0.055, 0.08)], segments=12,
                        axis=tuple(FWD), origin=tuple(U(px, py, mz - 0.12)))
        f.paint(brake, H)
        add("gun.%d.mouth" % n1, brake)

    # ------------------------------------------------------------------ one part per bone, then one mesh
    parts = []
    for bone, objs in by_bone.items():
        if len(objs) == 1:
            obj = objs[0]
            for vg in list(obj.vertex_groups):
                obj.vertex_groups.remove(vg)
            obj.name = bone
        else:
            obj = f.join(objs, bone, SLOTS)
            for vg in list(obj.vertex_groups):
                obj.vertex_groups.remove(vg)
        obj.name = bone
        obj.data.name = bone
        f.tag_group(obj, bone)
        parts.append(obj)
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
    ctx.log("brittlestar joined: %d verts, %d tris (n-gons triangulated: %d)" % (len(mesh.data.vertices), f.tri_count(mesh), len(big)))

    # ------------------------------------------------------------------ RIG
    B = rig.BoneSpec
    bones = [B("hull", (0, 0, 0), tuple(U(0, 0, 1.0))),
             B("ring", tuple(U(0, 0, RING_Z)), tuple(U(0, 0, RING_Z + 1.0)), parent="hull"),
             B("jet", tuple(U(0, 0, -0.74)), tuple(U(0, 0, -0.84)), parent="hull"),
             B("jet.mouth", tuple(U(0, 0, CP_TAIL)), tuple(U(0, 0, CP_TAIL - 0.4)), parent="jet"),
             B("jet.flame", tuple(U(0, 0, CP_TAIL)), tuple(U(0, 0, CP_TAIL - 1.0)), parent="jet.mouth"),
             B("gun", tuple(U(0, 0, ng0)), tuple(U(0, 0, nmz)), parent="hull"),
             B("gun.mouth", tuple(U(0, 0, nmz)), tuple(U(0, 0, nmz + 0.3)), parent="gun")]
    for i in range(POINTS):
        n1 = i + 1
        rx, ry = radial(i)
        px, py = RING_R * rx, RING_R * ry
        bones += [B("arm.%d" % n1, tuple(U(rx * R0, ry * R0, RING_Z)), tuple(U(px, py, RING_Z)), parent="ring"),
                  B("jet.%d" % n1, tuple(U(px, py, RING_Z)), tuple(U(px, py, RING_Z + 0.5)), parent="arm.%d" % n1),
                  B("jet.%d.mouth" % n1, tuple(U(px, py, POD_TAIL)), tuple(U(px, py, POD_TAIL - 0.4)), parent="jet.%d" % n1),
                  B("jet.%d.flame" % n1, tuple(U(px, py, POD_TAIL)), tuple(U(px, py, POD_TAIL - 1.0)), parent="jet.%d.mouth" % n1),
                  B("gun.%d" % n1, tuple(U(px, py, POD_TAIL + POD_LEN - 0.1)), tuple(U(px, py, mz)), parent="arm.%d" % n1),
                  B("gun.%d.mouth" % n1, tuple(U(px, py, mz)), tuple(U(px, py, mz + 0.3)), parent="gun.%d" % n1)]
    arm = rig.build_armature(ARMATURE, bones)
    rig.skin_rigid(mesh, arm, default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]

    # ------------------------------------------------------------------ MORPHS
    ms = morphs.MaskSet(mesh)
    me = mesh.data
    nv = len(me.vertices)

    def attr(name):
        out = [0.0] * nv
        if name in me.attributes:
            me.attributes[name].data.foreach_get("value", out)
        return out
    h_on, h_s, h_t, h_u, h_v = (attr(k) for k in ("h_on", "h_s", "h_t", "h_u", "h_v"))
    s_d, s_x, s_y, s_z = (attr(k) for k in ("s_d", "s_x", "s_y", "s_z"))
    fwd = FWD

    def axis_scale(factor, cx, cy, mask):
        """Scale the girth about a fore-aft axis through Unity (cx, cy): Blender x/z about the axis."""
        c = U(cx, cy, 0)

        def fn(i, co, n_):
            w = mask[i]
            if w <= 0:
                return Vector((0, 0, 0))
            return Vector(((co.x - c.x) * (factor - 1) * w, 0.0, (co.z - c.z) * (factor - 1) * w))
        return fn

    def halo_round(i, co, n_):
        """Time: the pentagon beam onto the circle through its corners, its section turned with it."""
        if h_on[i] < 0.5:
            return Vector((0, 0, 0))
        s, t, u, v = int(round(h_s[i])), h_t[i], h_u[i], h_v[i]
        a = point_angle(s) + (point_angle(s + 1) - point_angle(s)) * t
        target = U(math.cos(a) * (RING_R + u), math.sin(a) * (RING_R + u), RING_Z) + BACK * v
        return target - co

    def bristle(factor):
        def fn(i, co, n_):
            if s_d[i] <= 0:
                return Vector((0, 0, 0))
            return Vector((s_x[i], s_y[i], s_z[i])) * (s_d[i] * (factor - 1))
        return fn

    charge, mass, space = [bristle(3.4)], [], []
    for i in range(POINTS):
        n1 = i + 1
        px, py = RING_R * radial(i)[0], RING_R * radial(i)[1]
        pod_m = ms.mask("jet.%d" % n1)
        bell_m = ms.mask("jet.%d.mouth" % n1)
        gun_m = ms.mask("gun.%d" % n1)
        brk_m = ms.mask("gun.%d.mouth" % n1)
        gun_all = ms.mask("gun.%d" % n1, "gun.%d.mouth" % n1)
        charge += [morphs.stretch_along(tuple(fwd), tuple(U(px, py, POD_TAIL)), 1.3, pod_m),
                   axis_scale(1.5, px, py, pod_m), axis_scale(1.5, px, py, bell_m),
                   morphs.translate(tuple(fwd * (0.3 * POD_LEN)), gun_all), axis_scale(1.6, px, py, gun_all)]
        space += [morphs.stretch_along(tuple(fwd), tuple(U(px, py, POD_TAIL + POD_LEN)), 2.2, gun_m, ramp=(0.0, 0.05)),
                  morphs.translate(tuple(fwd * (1.2 * BARREL_LEN)), brk_m)]
    hull_m = ms.mask("hull")
    nose_gun = ms.mask("gun", "gun.mouth")

    def cockpit_girth(i, co, n_):
        w = hull_m[i]
        if w <= 0:
            return Vector((0, 0, 0))
        uz = -co.y
        k = 0.12 + (0.45 - 0.12) * morphs.smoothstep(0.10, 0.55, uz)
        k *= morphs.smoothstep(-0.88, -0.70, uz)   # the main engine's seat holds
        return Vector((co.x * k, 0.0, co.z * k)) * w
    mass += [morphs.stretch_along(tuple(fwd), tuple(U(0, 0, CP_TAIL)), 1.25, hull_m, ramp=(0.0, 0.25)), cockpit_girth,
             morphs.translate(tuple(fwd * (0.25 * CP_LEN)), nose_gun)]
    space += [morphs.stretch_along(tuple(fwd), tuple(U(0, 0, CP_TAIL + CP_LEN)), 2.2, ms.mask("gun"), ramp=(0.0, 0.02)),
              morphs.translate(tuple(fwd * (1.2 * BARREL_LEN * 0.6)), ms.mask("gun.mouth"))]
    shapes = {"Charge": charge, "Mass": mass, "Space": space, "Time": [halo_round]}
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))
    for a in ("h_on", "h_s", "h_t", "h_u", "h_v", "s_d", "s_x", "s_y", "s_z"):
        if a in me.attributes:
            me.attributes.remove(me.attributes[a])

    # ------------------------------------------------------------------ DOCUMENTATION POSES (code drives the bones)
    poses = {
        # the 28-degree lean into a full right turn (BrittleStarAnimation.bankDegrees): the LEFT side (+X) rises
        "bankright": {"hull": (0, -28, 0)},
        "bankleft": {"hull": (0, 28, 0)},
        # the firing rotation: +36 degrees about the nose carries point 0 (top) toward the pilot's left (+X)
        "ringturn": {"ring": (0, 36, 0)},
    }
    names = ("bankright", "bankleft", "ringturn")
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
                "bankright": [("arm.2", "z", +1, 1.0), ("arm.5", "z", -1, 1.0)],
                "bankleft": [("arm.2", "z", -1, 1.0), ("arm.5", "z", +1, 1.0)],
                "ringturn": [("arm.1", "x", +1, 1.5), ("arm.3", "x", -1, 0.5)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            # the placeholder's envelope in the Blender frame (BrittleStarHullForm at its defaults, exported by
            # brittlestar_harness --obj, plus its six unit flames to Unity z -1.9), which the gameplay was tuned to
            "size_ref_blender": ((-5.954, -1.685, -5.074), (5.954, 1.90, 6.26)), "size_tol": (0.06, 0.06, 0.06),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
        },
    }
