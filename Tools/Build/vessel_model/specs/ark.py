"""
ark - a NEW hull with no artist file: the ARK, a large mothership (Garrett, 2026-10-27: "a large mothership that is
decorated with prisms, shielded prisms, danger prisms, supershielded prisms. these are the ark's health and they will
regenerate into socketed locations that should look like the prism belongs there. base the superstructure off half an
arc clam shell. the ark should have locations for an inactive visible omni crystal that thematically powers the vessel
and a payload that players can grab and use as a weapon that the ark will regenerate").
A spaceship inspired by a scallop (Pectinidae): ONE valve of the shell, never the animal.

THE READ: from the chase camera (behind and above) it must say "a clam shell carrying a crown of prisms": a fan of
ribbed hull plates radiating from a hinge, a prism lying in every groove between the ribs as if the groove was cut for
it, a ring of danger prisms on the lip, a glass crystal hovering in a ring over the hinge, and one long spear lying in
the centre groove and reaching past the lip like a bowsprit.

Clade -> ship:
  the valve's radial ribs -> EIGHT SHELL PLATES (bones `shell.0` .. `shell.7`), each a crowned rib plate fanning from
                             the hinge, floating clear of its neighbours across a constant 5.4-unit groove; domain trim
                             on each plate's lip
  the grooves between ribs -> PRISM SOCKETS: every groove carries cradles cut to the prism (a U channel with an
                             Engine-slot regen emitter in its floor, on a pedestal off the core). A socketed prism's top
                             lies flush with the rib edges, so it reads as part of the shell
  the umbo (hinge boss)   -> the HUB plate (bone `hub`) and the OMNI CRADLE: a ring (bone `omni.ring`) hovering over
                             the hub with the dormant omni crystal (bone `omni`, Window slot = inactive glass) floating
                             in it; the crystal "powers" the ark
  the shell margin (lip)  -> the RIM SOCKETS: one danger-prism cradle per plate, lying along the lip
  the mantle / body       -> the CORE (root `hull`): a lens hull under the plates, domain band at its waist, a bridge
                             window across its prow under the lip
  the hinge ligament      -> the ENGINE BANK behind the hinge: three floating nacelles (bones `engine.L` / `engine.C`
                             / `engine.R`) with Engine mouths
  (no clade part)         -> the PAYLOAD (bone `payload`): a spear lying in the centre groove on two forked clamps, its
                             head past the lip. Players grab it; the ark regrows it in its clamps (the clamp emitters)

SOCKETS. One bone per socket, `socket.<tier>.<nn>`, head at the socketed prism's CENTRE, tail along the prism's length
(toward the lip for groove sockets, along the lip for rim sockets), bone Z (roll) = the surface's up. Each socket's
cradle mesh is skinned to its bone, so Unity's bone optimisation keeps it. The socket prism is PRISM_W x PRISM_H x
PRISM_L (across x up x along): spawn the prism at the bone, scaled to that, and it sits in the groove.
  supershielded  the innermost groove sockets, nearest the omni crystal (7)
  shielded       the middle groove sockets (6)
  prism          the outer groove sockets (6)
  danger         the rim sockets, one per plate (8)
The payload's regen point is `payload.dock` (a bone at the spear's rest centre, skinned to the clamps).

A HOVERING ASSEMBLY (vessel-model section 0.5): the shell plates, hub, ring, crystal, payload and engines float clear;
`assembly_report.py Ark.fbx --floating shell.0,...,shell.7,hub,omni.ring,omni,payload,engine.L,engine.C,engine.R`.

Contract (FLEET_CONVENTION sections 1-4 for a NEW body):
  file      Assets/_Models/Vessel Models/Ark.fbx (guid 3a7c5e1f9b2d4c60a8e4f1b7d3c9e205), preset "zup"
  frame     Blender: nose (the lip) -Y, up +Z, pilot's left +X. Unity (x, y, z) = Blender (-x, z, -y): NOSE = Unity +Z.
            Origin: the core's centre line, on the deck plane.
  nodes     mesh `mesh` (identity, metres), armature `rig`; root bone `hull`
  size      ~112 wide, ~104 long (spear tip to engine mouths), ~30 tall: a mothership, ~9x the Squirrel's length.
  slots     0 Domain, 1 Body, 2 Window, 3 Engine
  shapes    Charge (weapon): the payload spear lengthens and the omni crystal swells;
            Mass (volume):   the core and plates bulk out (taller, a little wider);
            Space (reach):   the shell fans wider about the hinge;
            Time (rate):     the engine nacelles stretch back
            (the socket bones do not move with the shapes; the cradles do, so runtime prisms ride the bones and the
            shapes stay modest where the sockets are)
  takes     rest (first); clap (the shell valve lifts its lip about the hinge, the hub with it); release (the payload
            slides forward out of its clamps). Single-frame poses, no blend-shape curves.
"""

import math
from _common import box_uv

NAME = "Ark"
OUTPUT = "Assets/_Models/Vessel Models/Ark.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3

# ---------------------------------------------------------------- layout (Blender frame: nose -Y, up +Z, left +X)
HY = 40.0                 # the hinge (umbo) on the centre line; the fan opens toward -Y
R = 64.0                  # the plates' outer radius (the lip)
RC = R + 6.0              # the core's radius (it shows a margin past the lip for the rim sockets)
THETA = 62.0              # the fan's half angle, degrees
NPLATE = 8                # plates; NPLATE - 1 grooves (one on the centre line: the payload's)
GROOVE = 5.4              # constant groove width between plates
RHO0 = 30.0               # where the plates begin (inside: the hub)
HUB_R = 26.0              # the hub's radius
Z0, HDOME = 3.0, 13.0     # deck height, the dome's rise
CROWN = 0.9               # each rib plate's crown over its edges
PLATE_T = 1.2
CORE_DROP = 3.9           # core top below the dome surface

# the socketed prism (across, up, along), in metres
PRISM_W, PRISM_H, PRISM_L = 3.0, 1.5, 7.0
SOCKET_RHO = {"supershielded": 37.0, "shielded": 47.5, "prism": 58.0}

# the payload spear along the centre groove
PAY_RHO0, PAY_RHO1 = 43.0, 72.0
PAY_R = 1.3


def dome(x, y):
    """The valve's top surface height at (x, y): highest a little ahead of the hinge, falling to the lip and toward
    the fan's flanks."""
    dx, dy = x, HY - y
    rho = math.hypot(dx, dy)
    th = math.degrees(math.atan2(dx, dy)) if rho > 1e-6 else 0.0
    u = min(rho / R, 1.15)
    rise = HDOME * (1.0 - u ** 2.1) * (1.0 - 0.38 * min(1.0, (th / THETA) ** 2))
    return Z0 + max(rise, -1.0)


def fan_dir(phi_deg):
    p = math.radians(phi_deg)
    return (math.sin(p), -math.cos(p))


def fan_perp(phi_deg):
    """Unit vector across the fan toward increasing phi."""
    p = math.radians(phi_deg)
    return (math.cos(p), math.sin(p))


PHI = [-THETA + k * (2 * THETA / NPLATE) for k in range(NPLATE + 1)]   # plate edges; PHI[1:-1] are the grooves
GROOVES = PHI[1:-1]


def core_hw(y):
    """The core's half width at y: the fan's outline, rounded behind the hinge."""
    d = HY - y
    fan = 0.0
    if 0 < d < RC:
        fan = min(math.sqrt(max(RC * RC - d * d, 0.0)), d * math.tan(math.radians(THETA + 4)))
    tail = 0.0
    WT, LT = 22.0, 16.0
    if abs(y - HY) < LT:
        tail = WT * math.sqrt(1 - ((y - HY) / LT) ** 2)
    return max(fan, tail)


def build(ctx):
    import bpy  # noqa: F401
    import bmesh
    from mathutils import Vector, Matrix
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    parts = []
    PID = {"n": 100}

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
        o.name = name
        for vg in list(o.vertex_groups):
            o.vertex_groups.remove(vg)
        f.tag_group(o, name)
        parts.append(o)
        return o

    def ring_loft(name, rings, cap=True):
        """A closed solid from cross-section rings (lists of Vectors, same count, consistent winding), capped by
        triangle fans at both ends."""
        bm = bmesh.new()
        vs = [[bm.verts.new(p) for p in rg] for rg in rings]
        n = len(rings[0])
        for a, b in zip(vs, vs[1:]):
            for i in range(n):
                j = (i + 1) % n
                bm.faces.new((a[i], a[j], b[j], b[i]))
        if cap:
            for rg, rev in ((vs[0], True), (vs[-1], False)):
                c = sum((v.co for v in rg), Vector()) / n
                cv = bm.verts.new(c)
                for i in range(n):
                    j = (i + 1) % n
                    bm.faces.new((cv, rg[j], rg[i]) if rev else (cv, rg[i], rg[j]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return f.new_mesh_object(name, bm)

    def surf_normal(x, y):
        e = 0.25
        gx = (dome(x + e, y) - dome(x - e, y)) / (2 * e)
        gy = (dome(x, y + e) - dome(x, y - e)) / (2 * e)
        return Vector((-gx, -gy, 1.0)).normalized()

    # ================================================================== SHELL PLATES (shell.0 .. shell.7)
    NS, NR = 12, 22
    for k in range(NPLATE):
        pa, pb = PHI[k], PHI[k + 1]
        oa = GROOVE / 2 if k > 0 else 0.0
        ob = GROOVE / 2 if k < NPLATE - 1 else 0.0
        da, db, na, nb = fan_dir(pa), fan_dir(pb), fan_perp(pa), fan_perp(pb)
        rings = []
        for t in range(NR + 1):
            rho = RHO0 + (R - RHO0) * t / NR
            ea = Vector((rho * da[0] + oa * na[0], HY + rho * da[1] + oa * na[1], 0))
            eb = Vector((rho * db[0] - ob * nb[0], HY + rho * db[1] - ob * nb[1], 0))
            top, bot = [], []
            for s in range(NS + 1):
                u = s / NS
                p = ea.lerp(eb, u)
                z = dome(p.x, p.y) + CROWN * math.sin(math.pi * u) ** 0.8
                top.append(Vector((p.x, p.y, z)))
                bot.append(Vector((p.x, p.y, z - PLATE_T)))
            rings.append(top + list(reversed(bot)))
        pl = ring_loft("plate", rings)
        f.ensure_slots(pl, SLOTS)
        f.paint(pl, H)
        f.paint(pl, D, lambda c, n, i: math.hypot(c.x, HY - c.y) > R - 3.2 and n.z > 0.3)
        nm = "shell.%d" % k
        bone_part([piece(pl, None, 10 + k, bevel=0.12)], nm)

    # ================================================================== HUB (the umbo) + OMNI CRADLE
    hub_rings = []
    for t in range(13):
        rho = 2.0 + (HUB_R - 2.0) * t / 12
        top, bot = [], []
        for s in range(25):
            ph = -THETA - 6 + (2 * THETA + 12) * s / 24
            d = fan_dir(ph)
            x, y = rho * d[0], HY + rho * d[1]
            z = dome(x, y) + 0.6
            top.append(Vector((x, y, z)))
            bot.append(Vector((x, y, z - 1.5)))
        hub_rings.append(top + list(reversed(bot)))
    hub = ring_loft("hub", hub_rings)
    f.ensure_slots(hub, SLOTS)
    f.paint(hub, H)
    f.paint(hub, D, lambda c, n, i: HUB_R - 2.5 < math.hypot(c.x, HY - c.y) and n.z > 0.3)
    bone_part([piece(hub, None, 20, bevel=0.12)], "hub")

    OMNI = Vector((0.0, HY - 13.0, dome(0, HY - 13.0) + 0.6 + 8.5))
    ring_pts = [OMNI + Vector((6.2 * math.cos(2 * math.pi * i / 48), 6.2 * math.sin(2 * math.pi * i / 48), 0)) for i in range(49)]
    oring = f.tube_along("oring", [tuple(p) for p in ring_pts], [0.75] * len(ring_pts), segments=10, cap=False)
    f.ensure_slots(oring, SLOTS)
    f.paint(oring, D)
    ring2 = [OMNI + Vector((6.2 * math.cos(2 * math.pi * i / 48), 0, 6.2 * math.sin(2 * math.pi * i / 48))) for i in range(49)]
    oring2 = f.tube_along("oring2", [tuple(p) for p in ring2], [0.45] * len(ring2), segments=8, cap=False)
    f.ensure_slots(oring2, SLOTS)
    f.paint(oring2, H)
    # the ring's mount: a squat pylon cap under the ring, floating over the hub (a collar, not a strut)
    collar = f.lathe("collar", [(0.0, 0.0), (3.4, 0.0), (3.8, 0.6), (2.6, 1.6), (0.0, 1.6)], segments=24,
                     axis=(0, 0, 1), origin=(OMNI.x, OMNI.y, dome(OMNI.x, OMNI.y) + 1.4))
    f.ensure_slots(collar, SLOTS)
    f.paint(collar, H)
    f.paint(collar, J, lambda c, n, i: n.z > 0.7)
    bone_part([piece(oring, None, 21), piece(oring2, None, 21), piece(collar, None, 21, bevel=0.08)], "omni.ring")
    # the dormant omni crystal: a faceted bipyramid, Window glass while inactive
    crys = f.lathe("omni", [(0.0, -4.6), (2.6, -0.6), (2.9, 0.0), (2.6, 0.6), (0.0, 4.6)], segments=6,
                   axis=(0, 0, 1), origin=tuple(OMNI))
    f.ensure_slots(crys, SLOTS)
    f.paint(crys, W)
    f.set_smooth(crys, False)
    bone_part([piece(crys, None, 22)], "omni")

    # ================================================================== CORE (hull): the lens under the plates
    y_front, y_back = HY - RC + 0.4, HY + 15.6
    core_rings = []
    NA = 40
    for t in range(41):
        y = y_front + (y_back - y_front) * (t / 40) ** 1.0
        hw = max(core_hw(y), 0.6)
        rg = []
        for a in range(NA):
            ang = 2 * math.pi * a / NA
            ca, sa = math.cos(ang), math.sin(ang)
            u = math.copysign(abs(ca) ** (2 / 2.6), ca)
            x = hw * u
            ztop = min(dome(x, y) - CORE_DROP, Z0 + HDOME - CORE_DROP)
            zbot = -5.5 * (1 - min(1.0, abs(u)) ** 2) - 1.0
            zm = 0.0
            e = abs(sa) ** (2 / 2.6)
            z = zm + ((max(ztop, zm + 0.5) - zm) if sa >= 0 else (zbot - zm)) * e
            rg.append(Vector((x, y, z)))
        core_rings.append(rg)
    core = ring_loft("core", core_rings)
    f.ensure_slots(core, SLOTS)
    f.paint(core, H)
    f.paint(core, D, lambda c, n, i: abs(c.z) < 0.55 and abs(n.z) < 0.6)
    f.paint(core, W, lambda c, n, i: c.y < y_front + 7.0 and -1.6 < c.z < -0.5 and abs(c.x) < 22 and n.y < -0.3)
    hull_parts = [piece(core, None, 1)]

    # ================================================================== SOCKETS: cradles cut to the prism
    socket_bones = []      # (name, head, tail, roll_up)

    def cradle(name, centre, along, up, pid):
        """A U channel under a prism of PRISM_W x PRISM_H x PRISM_L centred at `centre`: a floor with an Engine-slot
        regen emitter, two side lips, and a pedestal down to the core."""
        al = along.normalized()
        upn = (up - al * up.dot(al)).normalized()
        ac = al.cross(upn).normalized()
        M = Matrix((ac, al, upn)).transposed().to_4x4()
        M.translation = centre

        def boxp(nm, size, off, slot, emit=False):
            b = f.box(nm, size, center=off)
            f.transform_mesh(b, M)
            f.ensure_slots(b, SLOTS)
            f.paint(b, slot)
            if emit:
                f.paint(b, J, lambda c, n, i: n.dot(upn) > 0.7)
            return piece(b, None, pid, bevel=0.06)
        floor_z = -PRISM_H / 2 - 0.25
        out = [boxp("floor", (PRISM_W + 0.9, PRISM_L + 0.6, 0.5), (0, 0, floor_z), H),
               boxp("emitter", (PRISM_W * 0.55, PRISM_L * 0.8, 0.12), (0, 0, floor_z + 0.31), H, emit=True)]
        for sx in (1, -1):
            out.append(boxp("lip", (0.3, PRISM_L + 0.6, PRISM_H * 0.7), (sx * (PRISM_W / 2 + 0.3), 0, -PRISM_H * 0.15 - 0.1), D))
        ped_h = max(0.4, CORE_DROP - PRISM_H - 0.5 + 0.4)
        out.append(boxp("pedestal", (PRISM_W * 0.6, PRISM_L * 0.6, ped_h), (0, 0, floor_z - 0.25 - ped_h / 2 + 0.05), H))
        bone_part(out, name)
        socket_bones.append((name, centre, centre + al * (PRISM_L / 2), upn))

    tiers = [("supershielded", 0), ("shielded", 0), ("prism", 0)]
    count = {"supershielded": 0, "shielded": 0, "prism": 0, "danger": 0}
    pid_s = 100
    for g in GROOVES:
        d = fan_dir(g)
        for tier, _ in tiers:
            rho = SOCKET_RHO[tier]
            if abs(g) < 1e-6 and rho > PAY_RHO0 - PRISM_L / 2 - 1:
                continue                              # the centre groove's outer half carries the payload
            x, y = rho * d[0], HY + rho * d[1]
            upn = surf_normal(x, y)
            top = Vector((x, y, dome(x, y)))
            centre = top - upn * (PRISM_H / 2)
            x2, y2 = (rho + 1) * d[0], HY + (rho + 1) * d[1]
            along = Vector((x2, y2, dome(x2, y2))) - top
            nm = "socket.%s.%02d" % (tier, count[tier])
            count[tier] += 1
            cradle(nm, centre, along, upn, pid_s)
            pid_s += 1
    for k in range(NPLATE):
        pm = 0.5 * (PHI[k] + PHI[k + 1])
        d, n = fan_dir(pm), fan_perp(pm)
        rho = R + 2.6
        x, y = rho * d[0], HY + rho * d[1]
        upn = surf_normal(x, y)
        top = Vector((x, y, max(dome(x, y), Z0 - 0.4)))
        centre = top - upn * (PRISM_H / 2) + Vector((0, 0, 0.0))
        along = Vector((n[0], n[1], 0))
        nm = "socket.danger.%02d" % count["danger"]
        count["danger"] += 1
        cradle(nm, centre, along, upn, pid_s)
        pid_s += 1
    ctx.log("sockets: %s" % count)

    # ================================================================== PAYLOAD (the spear) + its dock
    pd = fan_dir(0.0)
    def gpt(rho, lift=0.0):
        x, y = rho * pd[0], HY + rho * pd[1]
        return Vector((x, y, dome(x, y) + lift))
    p_rear, p_tip = gpt(PAY_RHO0, 0.2), gpt(PAY_RHO1, 0.0)
    p_tip.z = p_rear.z - 2.0                          # held nearly level, the head dipping past the lip
    pax = (p_tip - p_rear).normalized()
    plen = (p_tip - p_rear).length
    spear = f.lathe("spear", [(0.0, 0.0), (0.9, 0.0), (1.2, 0.8), (PAY_R, 2.0), (PAY_R, plen * 0.62), (1.1, plen * 0.66),
                              (PAY_R * 1.25, plen * 0.70), (0.25, plen), (0.0, plen)], segments=12,
                    axis=tuple(pax), origin=tuple(p_rear))
    f.ensure_slots(spear, SLOTS)
    f.paint(spear, H)
    f.paint(spear, D, lambda c, n, i: 0.25 < (c - p_rear).dot(pax) / plen < 0.55)
    f.paint(spear, J, lambda c, n, i: (c - p_rear).dot(pax) / plen > 0.86 or (c - p_rear).dot(pax) < 0.3)
    bone_part([piece(spear, None, 30, bevel=0.05)], "payload")
    # the dock: two forked clamps under the spear, regen emitters glowing in their throats
    dock = []
    for frac in (0.18, 0.5):
        c = p_rear + pax * (plen * frac)
        upn = Vector((0, 0, 1))
        ac = pax.cross(upn).normalized()
        for sx in (1, -1):
            b = f.box("fork", (0.5, 2.2, 2.6), center=(0, 0, 0))
            M = Matrix((ac, pax, upn)).transposed().to_4x4()
            M.translation = c + ac * (sx * (PAY_R + 0.45)) - upn * 1.0
            f.transform_mesh(b, M)
            dock.append(piece(b, D, 31, bevel=0.06))
        base = f.box("forkbase", (2 * PAY_R + 1.4, 2.2, 0.6), center=(0, 0, 0))
        M = Matrix((ac, pax, upn)).transposed().to_4x4()
        M.translation = c - upn * (PAY_R + 1.4)
        f.transform_mesh(base, M)
        f.ensure_slots(base, SLOTS)
        f.paint(base, H)
        f.paint(base, J, lambda cc, n, i: n.z > 0.7)
        dock.append(piece(base, None, 31, bevel=0.06))
        ped = f.box("forkped", (1.6, 1.6, 3.0), center=(0, 0, 0))
        M.translation = c - upn * (PAY_R + 3.1)
        f.transform_mesh(ped, M)
        dock.append(piece(ped, H, 31))
    bone_part(dock, "payload.dock")
    pay_mid = p_rear + pax * (plen * 0.5)

    # ================================================================== ENGINE BANK (behind the hinge)
    eng = {}
    for nm, ex, ez, rr in (("engine.L", 14.0, 1.0, 3.4), ("engine.C", 0.0, -1.2, 4.2), ("engine.R", -14.0, 1.0, 3.4)):
        y0 = y_back - 1.8 + (2.5 if nm == "engine.C" else -4.0)
        ln = 13.0
        o = (ex, y0, ez)
        nac = f.lathe("nacelle", [(0.0, 0.0), (rr * 0.55, 0.0), (rr, ln * 0.18), (rr * 1.04, ln * 0.6), (rr * 0.88, ln),
                                  (rr * 0.62, ln), (rr * 0.62, ln - 0.6), (0.0, ln - 0.6)], segments=20,
                      axis=(0, 1, 0), origin=o)
        f.ensure_slots(nac, SLOTS)
        f.paint(nac, H)
        f.paint(nac, D, lambda c, n, i, y0=y0: y0 + ln * 0.42 < c.y < y0 + ln * 0.52)
        f.paint(nac, J, lambda c, n, i, y0=y0: c.y > y0 + ln - 0.7 and n.y > -0.2 and math.hypot(c.x - o[0], c.z - o[2]) < rr * 0.7)
        eng[nm] = (Vector(o), ln)
        bone_part([piece(nac, None, 40, bevel=0.08)], nm)

    bone_part(hull_parts, "hull")

    # ================================================================== JOIN, n-gons out
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
    ctx.log("ark joined: %d verts, %d tris" % (len(mesh.data.vertices), f.tri_count(mesh)))

    # ================================================================== RIG
    B = rig.BoneSpec
    HINGE = Vector((0, HY, dome(0, HY)))
    bones = [B("hull", (0, 0, 0), (0, -20.0, 0))]
    for k in range(NPLATE):
        pm = 0.5 * (PHI[k] + PHI[k + 1])
        d = fan_dir(pm)
        tip = Vector((R * d[0], HY + R * d[1], dome(R * d[0], HY + R * d[1])))
        bones.append(B("shell.%d" % k, tuple(HINGE), tuple(tip), parent="hull"))
    bones.append(B("hub", tuple(HINGE), tuple(HINGE + Vector((0, -HUB_R, 0))), parent="hull"))
    bones.append(B("omni.ring", tuple(OMNI - Vector((0, 0, 6.0))), tuple(OMNI), parent="hub"))
    bones.append(B("omni", tuple(OMNI), tuple(OMNI + Vector((0, 0, 4.6))), parent="omni.ring"))
    bones.append(B("payload.dock", tuple(pay_mid - Vector((0, 0, PAY_R + 1.4))), tuple(pay_mid - Vector((0, 0, PAY_R + 1.4)) + pax * 4.0), parent="hull"))
    bones.append(B("payload", tuple(pay_mid), tuple(p_tip), parent="payload.dock"))
    for nm, (o, ln) in eng.items():
        bones.append(B(nm, tuple(o), tuple(o + Vector((0, ln, 0))), parent="hull"))
    for nm, head, tail, upn in socket_bones:
        bones.append(B(nm, tuple(head), tuple(tail), parent="hull"))
    arm = rig.build_armature(ARMATURE, bones)
    # roll each socket bone so its Z axis is the surface's up (the prism's up)
    import bpy as _bpy
    _bpy.context.view_layer.objects.active = arm
    _bpy.ops.object.mode_set(mode="EDIT")
    for nm, head, tail, upn in socket_bones:
        eb = arm.data.edit_bones[nm]
        eb.align_roll(upn)
    _bpy.ops.object.mode_set(mode="OBJECT")
    rig.skin_rigid(mesh, arm, default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]

    # ================================================================== MORPHS (3-15 % of the diagonal)
    me = mesh.data
    ms = morphs.MaskSet(mesh)
    m = {n: ms.mask(n) for n in bone_names}
    nv = len(me.vertices)
    shellm = [max(m["shell.%d" % k][i] for k in range(NPLATE)) for i in range(nv)]
    socketm = [max((m[nm][i] for nm, *_ in socket_bones), default=0.0) for i in range(nv)]

    def rot_about(axis, pivot, deg, mask):
        Rm = Matrix.Rotation(math.radians(deg), 3, Vector(axis))
        p = Vector(pivot)

        def fn(i, co, n):
            if mask[i] <= 0:
                return Vector((0, 0, 0))
            return (Rm @ (co - p) + p - co) * mask[i]
        return fn

    def bulk(i, co, n):
        # everything but the sockets and the free parts above the hub bulks out about the deck
        w = max(m["hull"][i], shellm[i], m["hub"][i], socketm[i])
        return Vector((co.x * 0.08, 0, co.z * 0.32)) * w if w > 0 else Vector((0, 0, 0))

    def ride(mask, anchor, field):
        dv = field(Vector(anchor))

        def fn(i, co, n):
            return dv * mask[i] if mask[i] > 0 else Vector((0, 0, 0))
        return fn
    bulk_field = lambda co: Vector((co.x * 0.08, 0, co.z * 0.32))
    omni_rider = [max(m["omni"][i], m["omni.ring"][i]) for i in range(nv)]
    pay_rider = [max(m["payload"][i], m["payload.dock"][i]) for i in range(nv)]
    eng_rider = [max(m[e][i] for e in eng) for i in range(nv)]

    shapes = {
        "Charge": [morphs.stretch_along(tuple(pax), tuple(p_rear), 1.45, m["payload"]),
                   morphs.scale_about(1.35, tuple(OMNI), m["omni"])],
        "Mass": [bulk, ride(omni_rider, OMNI, bulk_field), ride(pay_rider, pay_mid, bulk_field),
                 ride(eng_rider, (0, HY, 0), bulk_field)],
        "Space": [rot_about((0, 0, 1), tuple(HINGE), 0.22 * 0.5 * (PHI[k] + PHI[k + 1]), m["shell.%d" % k])
                  for k in range(NPLATE)],
        "Time": [morphs.stretch_along((0, 1, 0), tuple(o), 1.65, m[e]) for e, (o, ln) in eng.items()],
    }
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))
    if "pid" in me.attributes:
        me.attributes.remove(me.attributes["pid"])

    # ================================================================== TAKES
    # world degrees in the Blender frame: -X lifts a point ahead of the pivot (the lip rises about the hinge)
    poses = {
        "clap": {**{"shell.%d" % k: (-12, 0, 0) for k in range(NPLATE)}, "hub": (-6, 0, 0)},
        "release": {"payload": {"rot": (0, 0, 0), "loc": tuple(pax * 14.0)}},
    }
    names = ("clap", "release")
    for p in names:
        takes.pose_take(arm, p, poses[p])
    takes.clear_pose(arm)

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 2) for v in lo), tuple(round(v, 2) for v in hi)))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "zup", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [ARMATURE + "|" + p for p in names],
            "pose_takes": [ARMATURE + "|" + p for p in names],
            "sheet_poses": names,
            "pose_expect": {
                "clap": [("shell.3", "z", +1, 1.0)],
                "release": [("payload", "y", -1, 5.0)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            "size_ref_blender": (tuple(lo), tuple(hi)),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.03,
        },
    }
