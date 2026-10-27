"""
_ark_kit - the builders the Ark concepts share (`ark.py`, the clam valve opening forward; `ark_reverse.py`, the valve
opening aft): grid-keeping lofts and polar solids that later detail passes inset by face index, the socket cradle a
prism regenerates into, the payload lance and its hover cradles, the engine nacelle, the omni reactor cage, and the
part / bone bookkeeping. A spec owns its layout (where the hinge is, which way the fan opens, the dome); the kit owns
the parts that must look the same on every Ark, so a change to a cradle or the lance lands on both concepts.
"""

import math

SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3


def _canon_name(name):
    """A material's slot name without Blender's .001 suffix."""
    import re
    return re.sub(r"\.\d+$", "", name)


class Kit:
    def __init__(self, ctx, slots=SLOTS):
        import bmesh
        from mathutils import Vector, Matrix
        self.ctx, self.f, self.slots = ctx, ctx.forms, slots
        self.bmesh, self.Vector, self.Matrix = bmesh, Vector, Matrix
        self.parts = []
        self.socket_bones = []      # (name, head, tail, up)

    # ------------------------------------------------------------------ part bookkeeping
    def tag(self, obj, pid):
        me = obj.data
        a = me.attributes.get("pid") or me.attributes.new("pid", "FLOAT", "POINT")
        a.data.foreach_set("value", [float(pid)] * len(me.vertices))
        return obj

    def piece(self, obj, slot, pid, bevel=0.0, angle=30):
        f = self.f
        f.ensure_slots(obj, self.slots)
        if slot is not None:
            f.paint(obj, slot)
        if bevel > 0:
            f.bevel(obj, width=bevel, segments=1, angle_deg=angle)
            f.apply_modifiers(obj)
        self.tag(obj, pid)
        return obj

    def bone_part(self, objs, name):
        f = self.f
        o = objs[0] if len(objs) == 1 else f.join(objs, name, self.slots)
        o.name = name
        for vg in list(o.vertex_groups):
            o.vertex_groups.remove(vg)
        f.tag_group(o, name)
        self.parts.append(o)
        return o

    # ------------------------------------------------------------------ mesh builders that keep a face grid
    def loft_bm(self, rings, slot=H, zipper=None):
        """Rings (lists of Vectors, same count) -> a capped solid. Returns (bm, grid, caps): grid[t][i] is the face
        between ring t and t+1 at ring point i; caps are the two end caps' faces. A cap is a fan about the ring's centre,
        or with `zipper=(ntop, nbot)` (a ring that is a top chain of ntop points across, then a bottom chain of nbot
        points back) a strip stitched between the two chains, which stays flat-shaded on a crowned section."""
        bmesh, Vector = self.bmesh, self.Vector
        bm = bmesh.new()
        vs = [[bm.verts.new(p) for p in rg] for rg in rings]
        n = len(rings[0])
        grid = []
        for a, b in zip(vs, vs[1:]):
            row = []
            for i in range(n):
                j = (i + 1) % n
                fc = bm.faces.new((a[i], a[j], b[j], b[i]))
                fc.material_index = slot
                row.append(fc)
            grid.append(row)
        caps = []
        for rg, rev in ((vs[0], True), (vs[-1], False)):
            if zipper:
                nt_, nb_ = zipper
                T, Bc = rg[:nt_], list(reversed(rg[nt_:]))
                ti, bi, cap = 0, 0, []
                while ti < nt_ - 1 or bi < nb_ - 1:
                    if bi == nb_ - 1 or (ti < nt_ - 1 and (ti + 1) / (nt_ - 1) <= (bi + 1) / (nb_ - 1)):
                        tri = (T[ti], T[ti + 1], Bc[bi])
                        ti += 1
                    else:
                        tri = (T[ti], Bc[bi + 1], Bc[bi])
                        bi += 1
                    fc = bm.faces.new(tri)
                    fc.material_index = slot
                    cap.append(fc)
                caps.append(cap)
                continue
            c = sum((v.co for v in rg), Vector()) / n
            cv = bm.verts.new(c)
            cap = []
            for i in range(n):
                j = (i + 1) % n
                fc = bm.faces.new((cv, rg[j], rg[i]) if rev else (cv, rg[i], rg[j]))
                fc.material_index = slot
                cap.append(fc)
            caps.append(cap)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.normal_update()
        return bm, grid, caps

    def polar_bm(self, phis, ts, rmax, ztop, zbot, wall, at, centre, slot=H):
        """A solid over the polar region rho <= rmax(phi) about `centre` (x, y), with `at(phi, rho)` -> (x, y): a top
        surface on rings at fractions `ts` (ascending, < 1), a rim wall at rmax with loops at z fractions `wall` between
        the top rim and the bottom rim (or (fraction, radius scale) pairs, for a stepped edge; the scale may be a function of
        phi, for an edge that is steep on one side and battered on another), and a bottom surface on the same rings. Returns (bm, bands): bands[k][j] is the
        face between loop k and k+1 at column j (loops: top rings outward, wall, bottom rings inward)."""
        bmesh, Vector = self.bmesh, self.Vector
        bm = bmesh.new()
        loops = []

        def P(phi, rho, z):
            x, y = at(phi, rho)
            return Vector((x, y, z))
        for t in ts:
            loops.append([P(p, t * rmax(p), ztop(*at(p, t * rmax(p)))) for p in phis])
        for w in wall:
            # a wall loop is a z fraction between the top rim and the bottom rim, or (fraction, radius scale) for a
            # stepped edge: a ledge, a recessed band, a lip
            fz, rs = (w, 1.0) if not isinstance(w, tuple) else w
            row = []
            for p in phis:
                x, y = at(p, rmax(p))
                xs, ys = at(p, rmax(p) * (rs(p) if callable(rs) else rs))
                row.append(Vector((xs, ys, ztop(x, y) + (zbot(x, y) - ztop(x, y)) * fz)))
            loops.append(row)
        for t in reversed(ts):
            loops.append([P(p, t * rmax(p), zbot(*at(p, t * rmax(p)))) for p in phis])
        vl = [[bm.verts.new(p) for p in lp] for lp in loops]
        n = len(phis)
        bands = []
        for a, b in zip(vl, vl[1:]):
            row = []
            for j in range(n):
                k = (j + 1) % n
                fc = bm.faces.new((a[j], a[k], b[k], b[j]))
                fc.material_index = slot
                row.append(fc)
            bands.append(row)
        cx, cy = centre
        for lp, z in ((vl[0], ztop(cx, cy)), (vl[-1], zbot(cx, cy))):
            cv = bm.verts.new(Vector((cx, cy, z)))
            for j in range(n):
                fc = bm.faces.new((cv, lp[j], lp[(j + 1) % n]))
                fc.material_index = slot
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.normal_update()
        return bm, bands

    def inset(self, bm, faces, thickness, depth, slot=None):
        """Inset a face region (kept deterministic by forms._inset_region); returns the region's new inner faces."""
        if not faces:
            return []
        inner = self.f._inset_region(bm, faces, thickness, depth, slot)
        bm.normal_update()
        return inner

    def finish_bm(self, name, bm):
        return self.f.new_mesh_object(name, bm)

    def frame_matrix(self, centre, along, up):
        al = along.normalized()
        upn = (up - al * up.dot(al)).normalized()
        ac = al.cross(upn).normalized()
        M = self.Matrix((ac, al, upn)).transposed().to_4x4()
        M.translation = centre
        return M, ac, al, upn

    def boxm(self, nm, size, off, M, slot, pid, bevel=0.0):
        b = self.f.box(nm, size, center=off)
        self.f.transform_mesh(b, M)
        return self.piece(b, slot, pid, bevel=bevel)

    # ------------------------------------------------------------------ the parts every Ark shares
    def cradle(self, name, centre, along, up, pid, prism, core_top, column=False, ped_len=None, band=True):
        """A socket: a U channel under a prism of `prism` = (across, up, along) centred at `centre`, with a floor,
        domain side rails, end stops, two Engine-slot regen pads, and a pedestal down to the core (`core_top(x, y)`):
        a box, or with `column=True` a round column with a flared foot and head and (unless `band=False`) a domain band
        (a tall support in open space). With `ped_len` the pedestal runs that far straight back along the socket's -up instead (a socket on
        a wall). Its own bone `name` is recorded with head at the prism's centre, tail along it, Z = the prism's up."""
        Vector, Matrix, f = self.Vector, self.Matrix, self.f
        pw, ph, pl = prism
        M, ac, al, upn = self.frame_matrix(centre, along, up)
        fz = -ph / 2 - 0.3
        out = [self.boxm("floor", (pw + 1.0, pl + 0.8, 0.6), (0, 0, fz), M, H, pid, bevel=0.12)]
        for sy in (-1, 1):
            out.append(self.boxm("pad", (pw * 0.6, pl * 0.36, 0.14), (0, sy * pl * 0.22, fz + 0.36), M, J, pid))
            out.append(self.boxm("stop", (pw + 1.0, 0.45, ph * 0.5), (0, sy * (pl / 2 + 0.2), fz + 0.25 + ph * 0.22), M, H, pid, bevel=0.08))
        for sx in (1, -1):
            out.append(self.boxm("rail", (0.5, pl + 0.4, ph * 0.7), (sx * (pw / 2 + 0.4), 0, -ph * 0.15 - 0.1), M, D, pid, bevel=0.08))
        floor_bottom = centre + upn * (fz - 0.3)
        if ped_len is not None:
            out.append(self.boxm("pedestal", (pw * 0.55, pl * 0.55, ped_len), (0, 0, fz - 0.3 - ped_len / 2 + 0.1), M, H, pid, bevel=0.1))
        elif column:
            out += self.column(floor_bottom, core_top(floor_bottom.x, floor_bottom.y) - 0.3, 1.1, pid, band=band)
        else:
            ped_h = max(0.6, floor_bottom.z - core_top(floor_bottom.x, floor_bottom.y) + 0.3)
            ped = f.box("pedestal", (pw * 0.55, pl * 0.55, ped_h), center=(0, 0, 0))
            f.transform_mesh(ped, Matrix.Translation(Vector((floor_bottom.x, floor_bottom.y, floor_bottom.z - ped_h / 2 + 0.1))) @
                             Matrix.Rotation(math.atan2(-al.x, al.y), 4, "Z"))
            out.append(self.piece(ped, H, pid, bevel=0.1))
        self.bone_part(out, name)
        self.socket_bones.append((name, centre, centre + al * (pl / 2), upn))

    def lance(self, p_rear, pax, plen, core_top, pay_r, gap, fracs=(0.14, 0.40, 0.66)):
        """The payload: a lance from `p_rear` along `pax` (`plen` long, a 14-long glowing head, four domain-edged fins)
        as bone part `payload`, hovering `gap` over cradles at `fracs` of its length (bone part `payload.dock`: domain
        prongs, a regen pad, a pedestal to the core). Returns (pay_mid, p_tip, dock_head)."""
        Vector, f = self.Vector, self.f
        p_tip = p_rear + pax * plen
        head_s = plen - 14.0
        k = head_s / 42.0                              # the shaft was drawn for a 42-long shaft (a 56-long lance)
        shaft = f.lathe("shaft", [(0.0, 0.0), (0.9, 0.0), (1.3, 0.6), (pay_r, 2.0 * k), (pay_r, 6.0 * k), (2.05, 6.3 * k), (2.05, 8.5 * k),
                                  (pay_r, 8.8 * k), (pay_r, 20.0 * k), (2.1, 20.4 * k), (2.1, 24.0 * k), (pay_r, 24.4 * k),
                                  (1.75, 34.0 * k), (1.5, 36.0 * k), (2.2, 36.8 * k), (2.2, 39.5 * k), (1.6, 40.5 * k),
                                  (1.2, head_s + 0.2), (0.0, head_s + 0.2)], segments=16,
                        axis=tuple(pax), origin=tuple(p_rear))
        f.ensure_slots(shaft, self.slots)
        f.paint(shaft, H)
        f.paint(shaft, D, lambda c, n, i: 6.22 * k < (c - p_rear).dot(pax) < 8.58 * k or 20.3 * k < (c - p_rear).dot(pax) < 24.1 * k)
        f.paint(shaft, J, lambda c, n, i: (c - p_rear).dot(pax) < 0.2)
        parts = [self.piece(shaft, None, 40, bevel=0.05)]
        head = f.lathe("head", [(0.0, -0.4), (1.3, 0.0), (2.6, 4.0), (1.6, 10.0), (0.0, 14.0)], segments=4,
                       axis=tuple(pax), origin=tuple(p_rear + pax * head_s), up=(0, 0, 1))
        f.set_smooth(head, False)
        parts.append(self.piece(head, J, 41))
        for a in (45.0, 135.0, 225.0, 315.0):
            ac0 = pax.cross(Vector((0, 0, 1))).normalized()
            up0 = ac0.cross(pax).normalized()
            rd = ac0 * math.cos(math.radians(a)) + up0 * math.sin(math.radians(a))
            fin = f.plate("fin", [(0.6, pay_r - 0.2), (6.4, pay_r - 0.2), (5.4, pay_r + 1.5), (1.4, pay_r + 1.7)], 0.32,
                          normal=tuple(pax.cross(rd)), origin=tuple(p_rear), right=tuple(pax))
            f.ensure_slots(fin, self.slots)
            f.paint(fin, H)
            f.paint(fin, D, lambda c, n, i, rd=rd: (c - p_rear).dot(rd) > pay_r + 1.0 and abs(n.dot(pax.cross(rd))) < 0.5)
            parts.append(self.piece(fin, None, 42, bevel=0.04))
        self.bone_part(parts, "payload")
        dock = []
        for frac in fracs:
            c = p_rear + pax * (plen * frac)
            M, ac, al, upn = self.frame_matrix(c, pax, Vector((0, 0, 1)))
            # the lance floats over the cradle (a hovering assembly): the prongs' tops sit `gap` under its widest collar
            ptop = -2.2 - gap
            for sx in (1, -1):
                dock.append(self.boxm("prong", (0.6, 2.4, 2.0), (sx * 2.0, 0, ptop - 1.0), M, D, 43, bevel=0.08))
            dock.append(self.boxm("fbase", (5.2, 2.8, 0.7), (0, 0, ptop - 2.35), M, H, 43, bevel=0.1))
            dock.append(self.boxm("fpad", (2.6, 2.0, 0.14), (0, 0, ptop - 1.93), M, J, 43))
            bottom = c + upn * (ptop - 2.7)
            ped_h = max(0.8, bottom.z - core_top(bottom.x, bottom.y) + 0.3)
            ped = f.lathe("fped", [(0.0, 0.0), (1.0, 0.0), (1.0, ped_h - 0.6), (1.4, ped_h - 0.3), (1.4, ped_h), (0.0, ped_h)],
                          segments=12, axis=(0, 0, 1), origin=tuple(bottom - Vector((0, 0, ped_h - 0.15))))
            dock.append(self.piece(ped, H, 43))
        self.bone_part(dock, "payload.dock")
        pay_mid = p_rear + pax * (plen * 0.5)
        return pay_mid, p_tip, pay_mid - Vector((0, 0, 2.2 + gap + 2.0))

    def nacelle(self, name, o, axis, ln, rr, segments=24):
        """An engine as bone part `name`: from `o` along `axis`, `ln` long, radius `rr` - an intake lip with an Engine
        core, a domain band, six cooling fins round a flared bell, and a glowing bell with an inner cone."""
        Vector, f = self.Vector, self.f
        axis = Vector(axis).normalized()
        k_ = ln / 37.5                                 # the profile was drawn for a 37.5-long nacelle
        prof = [(0.0, 0.6), (rr * 0.45, 0.6), (rr * 0.84, 0.2), (rr * 0.96, 0.9), (rr * 0.93, 2.6), (rr * 0.88, 3.0),
                (rr, 5.5 * k_), (rr, 12.5 * k_), (rr * 1.06, 12.9 * k_), (rr * 1.06, 16.0 * k_), (rr, 16.4 * k_), (rr, 27.0 * k_),
                (rr * 0.86, 30.5 * k_), (rr * 0.94, 34.5 * k_), (rr * 1.03, ln), (rr * 0.93, ln), (rr * 0.80, ln - 3.0),
                (rr * 0.50, ln - 3.8), (rr * 0.32, ln - 1.6), (0.0, ln - 0.6)]
        nac = f.lathe("nacelle", prof, segments=segments, axis=tuple(axis), origin=tuple(o))
        if segments <= 12:
            f.set_smooth(nac, False)                   # a faceted nacelle reads hard, not soft
        f.ensure_slots(nac, self.slots)
        f.paint(nac, H)

        def s_of(c):
            return (c - o).dot(axis)

        def r_of(c):
            d = c - o
            return (d - axis * d.dot(axis)).length
        f.paint(nac, D, lambda c, n, i: 12.75 * k_ < s_of(c) < 16.15 * k_)
        f.paint(nac, J, lambda c, n, i: s_of(c) > ln - 4.2 and r_of(c) < rr * 0.87)
        f.paint(nac, J, lambda c, n, i: s_of(c) < 0.65 and r_of(c) < rr * 0.42)
        ep = [self.piece(nac, None, 50, bevel=0.08)]
        u = axis.cross(Vector((0, 0, 1))).normalized()
        v = u.cross(axis).normalized()
        for a in range(6):
            ang = 2 * math.pi * (a + 0.5) / 6
            rd = u * math.cos(ang) + v * math.sin(ang)
            fin = f.plate("efin", [(27.5 * k_, rr * 0.92), (34.0 * k_, rr * 0.98), (33.0 * k_, rr + 2.2), (28.5 * k_, rr + 2.0)], 0.45,
                          normal=tuple(axis.cross(rd)), origin=tuple(o), right=tuple(axis))
            ep.append(self.piece(fin, H, 51, bevel=0.05))
        self.bone_part(ep, name)

    def reactor(self, collar_o, pylon_r=16.5, pylon_drop=10.5, pylon_len=20.5, ring_segments=64):
        """The omni reactor: a glowing collar at `collar_o`, the dormant omni crystal (Window glass, 20 tall) hovering
        over it in a gimballed cage - a domain ring, a vertical ring and four containment pylons at `pylon_r`, from
        `pylon_drop` below the crystal's centre, `pylon_len` tall. Bone parts `omni.ring` and `omni`; returns the
        crystal's centre."""
        Vector, f = self.Vector, self.f
        cz = collar_o.z
        collar = f.lathe("collar", [(0.0, 0.0), (5.5, 0.0), (7.0, 0.8), (7.0, 1.8), (5.8, 3.0), (3.0, 3.2), (0.0, 3.2)], segments=28,
                         axis=(0, 0, 1), origin=tuple(collar_o))
        f.ensure_slots(collar, self.slots)
        f.paint(collar, H)
        f.paint(collar, J, lambda c, n, i: n.z > 0.8 and c.z > cz + 3.0)
        f.paint(collar, D, lambda c, n, i: cz + 0.85 < c.z < cz + 1.75)
        omni = collar_o + Vector((0, 0, 3.2 + 1.8 + 10.0))
        n = ring_segments
        rpts = [omni + Vector((12.0 * math.cos(2 * math.pi * i / n), 12.0 * math.sin(2 * math.pi * i / n), 0)) for i in range(n + 1)]
        ring1 = f.tube_along("ring1", [tuple(p) for p in rpts], [1.0] * (n + 1), segments=12 if n > 16 else 4, cap=False)
        vpts = [omni + Vector((0, 13.2 * math.cos(2 * math.pi * i / n), 13.2 * math.sin(2 * math.pi * i / n))) for i in range(n + 1)]
        ring2 = f.tube_along("ring2", [tuple(p) for p in vpts], [0.6] * (n + 1), segments=10 if n > 16 else 4, cap=False)
        cage = [self.piece(collar, None, 60, bevel=0.06), self.piece(ring1, D, 61), self.piece(ring2, H, 61)]
        for a in range(4):
            ang = 2 * math.pi * (a + 0.5) / 4
            rd = Vector((math.cos(ang), math.sin(ang), 0))
            # forms.plate draws the outline's second axis along normal x right, so normal = rd x Z puts it on +Z: the
            # pylon RISES from its foot (with Z x rd it hung below the crystal, its glowing tip at the bottom)
            nrm = rd.cross(Vector((0, 0, 1)))
            pyl = f.plate("pylon", [(-1.2, 0.0), (1.2, 0.0), (0.9, pylon_len - 2.5), (0.0, pylon_len), (-0.9, pylon_len - 2.5)], 0.9,
                          normal=tuple(nrm), origin=tuple(omni + rd * pylon_r - Vector((0, 0, pylon_drop))), right=tuple(rd))
            f.ensure_slots(pyl, self.slots)
            f.paint(pyl, H)
            f.paint(pyl, J, lambda c, n, i: c.z > omni.z - pylon_drop + pylon_len - 1.5)
            cage.append(self.piece(pyl, None, 62, bevel=0.06))
        self.bone_part(cage, "omni.ring")
        crys = f.lathe("omni", [(0.0, -10.0), (5.2, -1.6), (6.0, 0.0), (5.2, 1.6), (0.0, 10.0)], segments=6,
                       axis=(0, 0, 1), origin=tuple(omni))
        f.set_smooth(crys, False)
        self.bone_part([self.piece(crys, W, 63)], "omni")
        return omni

    def column(self, top, bottom_z, r, pid, band=True):
        """A round support from `top` (a point) straight down to `bottom_z`: a flared foot, a flared head and a domain
        band, so a tall support in open space reads as built, not as a stick. Returns its pieces."""
        Vector, f = self.Vector, self.f
        h = max(top.z - bottom_z, 0.8)
        o = Vector((top.x, top.y, bottom_z))
        prof = [(0.0, 0.0), (r * 1.7, 0.0), (r * 1.7, 0.35), (r, min(1.4, h * 0.3)), (r, h - min(1.4, h * 0.3)),
                (r * 1.5, h - 0.35), (r * 1.5, h), (0.0, h)]
        col = f.lathe("column", prof, segments=14, axis=(0, 0, 1), origin=tuple(o))
        f.ensure_slots(col, self.slots)
        f.paint(col, H)
        if band and h > 4.0:
            mid = bottom_z + h * 0.5
            f.paint(col, D, lambda c, n, i: mid - 0.61 < c.z < mid + 0.59 and abs(n.z) < 0.5)
        return [self.piece(col, None, pid)]

    def octa_mesh(self, name, centre, frame, s):
        """The game's SHIELDED prism as it settles (OctahedronMeshGenerator): an octahedron with its six vertices at
        +-s along the socket frame's axes (s = 3 x the prism's half-extent, CIRCUMSCRIBING_SCALE). `frame` = (across,
        along, up) unit vectors. One closed shell, flat-shaded."""
        bmesh, Vector = self.bmesh, self.Vector
        ac, al, up = frame
        bm = bmesh.new()
        P = {}
        for key, d in (("+x", ac), ("-x", -ac), ("+y", al), ("-y", -al), ("+z", up), ("-z", -up)):
            P[key] = bm.verts.new(centre + d * s)
        for sx in ("+x", "-x"):
            for sy in ("+y", "-y"):
                for sz in ("+z", "-z"):
                    bm.faces.new((P[sx], P[sy], P[sz]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return self.f.new_mesh_object(name, bm, smooth=False)

    def stella_mesh(self, name, centre, frame, s):
        """The game's SUPERSHIELDED prism as it settles (StellatedOctahedronMeshGenerator): the stella octangula, two
        interpenetrating tetrahedra; its inscribed octahedron has vertices at +-s on the frame's axes and its eight
        spike tips sit at the cube corners (+-s, +-s, +-s). With a cubic prism it is the REGULAR stellated octahedron.
        The visible surface (24 triangles, three per spike) as one closed shell, flat-shaded."""
        bmesh, Vector = self.bmesh, self.Vector
        ac, al, up = frame
        bm = bmesh.new()
        V = {}
        for i, d in enumerate((ac, al, up)):
            V[(i, 1)] = bm.verts.new(centre + d * s)
            V[(i, -1)] = bm.verts.new(centre - d * s)
        for sx in (1, -1):
            for sy in (1, -1):
                for sz in (1, -1):
                    T = bm.verts.new(centre + (ac * sx + al * sy + up * sz) * s)
                    vx, vy, vz = V[(0, sx)], V[(1, sy)], V[(2, sz)]
                    for a_, b_ in ((vx, vy), (vy, vz), (vz, vx)):
                        bm.faces.new((T, a_, b_))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return self.f.new_mesh_object(name, bm, smooth=False)

    def shield_cutter(self, kind, centre, frame, s, clearance, slot=D):
        """The negative of a shield solid grown by `clearance` all round: subtracted from a hull part it cuts a socket
        that is the prism's own shape, its walls painted `slot` (the boolean transfers the cutter's material)."""
        make = self.stella_mesh if kind == "stella" else self.octa_mesh
        # growing the solid's scale by g moves every face out by g * s * (face distance / s): for both solids the
        # nearest face plane sits s / sqrt(3) from the centre, so g = 1 + clearance * sqrt(3) / s
        cut = make("cut_" + kind, centre, frame, s * (1.0 + clearance * math.sqrt(3.0) / s))
        self.f.ensure_slots(cut, self.slots)
        self.f.paint(cut, slot)
        return cut

    def subtract(self, obj, cutters):
        """Boolean-subtract every cutter from `obj` (EXACT solver, the cutters' materials transferred onto the cut
        walls), then delete the cutters. Returns obj."""
        import bpy
        f = self.f
        if not cutters:
            return obj
        cut = cutters[0] if len(cutters) == 1 else f.join(cutters, "cutters", self.slots)
        f.ensure_slots(obj, self.slots)
        self.canon(obj)
        self.canon(cut)
        m = obj.modifiers.new("Boolean", "BOOLEAN")
        m.operation = "DIFFERENCE"
        m.object = cut
        m.solver = "EXACT"
        if hasattr(m, "material_mode"):
            m.material_mode = "TRANSFER"
        f.apply_modifiers(obj)
        # TRANSFER appends any cutter material the object lacks; fold the slots back to the canonical four by name
        me = obj.data
        idx = {mat.name: i for i, mat in enumerate(me.materials) if mat is not None}
        canon = {n: i for i, n in enumerate(self.slots)}
        remap = [canon.get(_canon_name(n), H) for n, i in sorted(idx.items(), key=lambda kv: kv[1])]
        if len(me.materials) > len(self.slots):
            mi = [0] * len(me.polygons)
            me.polygons.foreach_get("material_index", mi)
            me.polygons.foreach_set("material_index", [remap[i] if i < len(remap) else H for i in mi])
            while len(me.materials) > len(self.slots):
                me.materials.pop()
        bpy.data.objects.remove(cut, do_unlink=True)
        bpy.context.view_layer.update()                # drop the removed cutter from the view layer's object list
        return obj

    def canon(self, obj):
        """Rewrite a mesh in a CANONICAL order - vertices by position, each face starting at its lowest vertex, faces
        sorted - keeping slots and smooth flags. The EXACT boolean's result (its vertex and face counts too) depends on
        its inputs' order, and that order drifts between processes (bmesh ops walk pointer-keyed hashes), so without
        this two builds of one spec cut different meshes and `build.py --check` reports drift that is not in the spec.
        Drops vertex groups and point attributes: call it before a part is tagged."""
        import bpy
        me = obj.data
        co = [tuple(v.co) for v in me.vertices]
        order = sorted(range(len(co)), key=lambda i: (round(co[i][0], 4), round(co[i][1], 4), round(co[i][2], 4)))
        new_of = {old: new for new, old in enumerate(order)}
        faces = []
        for p in me.polygons:
            vs = [new_of[v] for v in p.vertices]
            k = vs.index(min(vs))
            faces.append((tuple(vs[k:] + vs[:k]), p.material_index, bool(p.use_smooth)))
        faces.sort()
        new = bpy.data.meshes.new(me.name)
        new.from_pydata([co[i] for i in order], [], [fc[0] for fc in faces])
        new.polygons.foreach_set("material_index", [fc[1] for fc in faces])
        new.polygons.foreach_set("use_smooth", [fc[2] for fc in faces])
        for mat in me.materials:
            new.materials.append(mat)
        new.update()
        for vg in list(obj.vertex_groups):
            obj.vertex_groups.remove(vg)
        obj.data = new
        bpy.data.meshes.remove(me)
        return obj

    def shield_socket(self, name, kind, centre, frame, s, pid, support_z, clearance=1.3):
        """The cradle a shield solid regenerates into, as bone part `name`: under the solid a key block - an X of two
        bars under a stellation (its bottom is the X of two tetrahedron edges), a square block under an octahedron (its
        bottom is a vertex) - with the solid's own shape grown by `clearance` cut out of it, so the cradle is the exact
        negative of the prism's underside and its glowing (Engine) walls hold it without touching; then a column down
        to `support_z` (pass None for a cradle that hangs in space). The octahedron's cup is round. Records the socket bone (head at the solid's centre,
        tail along it, Z = its up) and returns the pieces' object."""
        Vector, Matrix, f = self.Vector, self.Matrix, self.f
        ac, al, up = frame
        M = Matrix((ac, al, up)).transposed().to_4x4()
        M.translation = centre
        if kind == "stella":
            ln, w, hgt = 2.0 * s, 2.2, s * 0.55
            top = -s + hgt * 0.62
            blocks = []
            for (bx, by) in ((ln, w), (w, ln)):
                b = f.box("xbar", (bx, by, hgt), center=(0, 0, top - hgt / 2))
                f.transform_mesh(b, M @ Matrix.Rotation(math.radians(45.0), 4, "Z"))
                blocks.append(b)
            key = f.join(blocks, "key", self.slots)
        else:
            # a ROUND cup: a square block minus the octahedron leaves four corner posts reaching up past the cut, toward
            # whatever plate the octahedron is set through
            hgt = s * 0.5
            top = -s + hgt * 0.3
            key = f.lathe("cup", [(0.0, 0.0), (s * 0.5, 0.0), (s * 0.5, hgt), (0.0, hgt)], segments=16, axis=(0, 0, 1),
                          origin=(0.0, 0.0, top - hgt))
            f.transform_mesh(key, M)
        f.ensure_slots(key, self.slots)
        f.paint(key, H)
        self.subtract(key, [self.shield_cutter(kind, centre, frame, s, clearance, slot=J)])
        parts = [self.piece(key, None, pid)]
        if support_z is not None:
            foot = centre + up * (top - hgt)
            parts += self.column(foot, support_z, 1.4 if kind == "stella" else 1.1, pid)
        self.bone_part(parts, name)
        self.socket_bones.append((name, centre, centre + al * s, up))
        return parts

    # ------------------------------------------------------------------ join and rig helpers
    def join(self, mesh_node):
        """Join every bone part into the one mesh, triangulate n-gons, smooth with sharp edges, box UVs."""
        from _common import box_uv
        f, bmesh = self.f, self.bmesh
        mesh = f.join(self.parts, mesh_node, self.slots)
        mesh.data.name = mesh_node
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
        return mesh

    def socket_bone_specs(self, B, parent="hull"):
        return [B(nm, tuple(head), tuple(tail), parent=parent) for nm, head, tail, up in self.socket_bones]

    def roll_sockets(self, arm):
        """Roll every socket bone so its Z is the prism's up."""
        import bpy
        bpy.context.view_layer.objects.active = arm
        bpy.ops.object.mode_set(mode="EDIT")
        for nm, head, tail, upn in self.socket_bones:
            arm.data.edit_bones[nm].align_roll(upn)
        bpy.ops.object.mode_set(mode="OBJECT")
