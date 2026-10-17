"""
forms.py - the modelling library behind every generated vessel.

Everything here builds geometry in MODEL space with these conventions (fixed, because the
exporter and the game depend on them):

    +Y  up            Blender's -Y is the NOSE ... see export.py for the axis story: the hull is
    -Y  nose          modelled with its nose on -Y, its belly on -Z and its right wing on +X, so
    +X  right wing    that Blender's default FBX export (axis_forward='-Z', axis_up='Y') lands it
    +Z  tail          in Unity with the nose on +Z, up on +Y, right on +X.

    Wait - read that again: BLENDER is Z-up. In Blender's own frame the hull is modelled
    nose = -Y, up = +Z, right = +X, and the standard export converts that to Unity's
    nose = +Z, up = +Y, right = +X. All functions in this file take and return BLENDER-frame
    coordinates. `export.py` proves the conversion on every build.

The library is deliberately low-level: lofts, lathes, wings and plates built directly with
bmesh, plus finishing (bevel, subdivision, mirror, sharp edges by angle) applied through
modifiers that are then APPLIED, so the mesh that reaches the exporter is plain polygons. The
point is control: a vessel is a dozen parts each shaped by a short list of numbers, and every
number is one a session can turn while watching the render.

Each part is a separate object while it is shaped; `join()` merges them into the one skinned
mesh and leaves a VERTEX GROUP per part behind. Those groups are what the rig skins by and what
the element morphs mask by, so naming a part is naming its bone.
"""

import math

import bpy
import bmesh
from mathutils import Vector, Matrix, Euler

# ----------------------------------------------------------------------------- scene

def fresh_scene():
    """An empty scene with no default cube, camera or light."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.fps = 30
    return scene


def _link(obj):
    bpy.context.collection.objects.link(obj)
    return obj


def new_mesh_object(name, bm, smooth=True):
    """Turn a bmesh into a linked object. Faces smooth unless told otherwise."""
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.update()
    if smooth:
        me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    obj = bpy.data.objects.new(name, me)
    _link(obj)
    return obj


def select_only(obj):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


# --------------------------------------------------------------------------- profiles

def superellipse(w, h, n=2.5, segments=32, h_bottom=None, n_bottom=None, phase=0.0):
    """
    A closed 2D outline, counter-clockwise, starting at +X. `w` is the half-width, `h` the
    half-height above the centreline, `h_bottom` (default h) the half-height below it, `n`
    the squareness (2 = ellipse, 4+ = rounded box, 1 = diamond). Different exponents above and
    below the waterline make a hull that is flat-bellied and domed on top with one call.
    """
    if h_bottom is None:
        h_bottom = h
    if n_bottom is None:
        n_bottom = n
    pts = []
    for i in range(segments):
        t = 2 * math.pi * i / segments + phase
        c, s = math.cos(t), math.sin(t)
        hh = h if s >= 0 else h_bottom
        nn = n if s >= 0 else n_bottom
        x = w * math.copysign(abs(c) ** (2.0 / nn), c)
        y = hh * math.copysign(abs(s) ** (2.0 / nn), s)
        pts.append((x, y))
    return pts


def blend_profiles(a, b, t):
    """Linear blend of two outlines with the same point count."""
    return [(a[i][0] * (1 - t) + b[i][0] * t, a[i][1] * (1 - t) + b[i][1] * t) for i in range(len(a))]


def scale_profile(p, sx, sy=None):
    if sy is None:
        sy = sx
    return [(x * sx, y * sy) for x, y in p]


def airfoil(chord=1.0, thickness=0.12, camber=0.0, segments=24, te_thickness=0.0):
    """
    A closed airfoil outline in the (chord, thickness) plane: x runs 0..chord from leading
    edge to trailing edge, y is thickness. Upper surface first (LE -> TE), then lower back.
    NACA-4-digit-like thickness distribution; `camber` bows the mean line upward.
    """
    def yt(xn):
        return 5 * thickness * (0.2969 * math.sqrt(xn) - 0.1260 * xn - 0.3516 * xn ** 2 + 0.2843 * xn ** 3 - 0.1015 * xn ** 4)

    def yc(xn):
        return camber * (4 * xn * (1 - xn))  # simple parabolic camber line

    upper, lower = [], []
    half = max(4, segments // 2)
    for i in range(half + 1):
        # cosine spacing packs points at the leading edge where curvature is
        xn = 0.5 * (1 - math.cos(math.pi * i / half))
        t = yt(xn) + (te_thickness * 0.5 * xn)
        c = yc(xn)
        upper.append((xn * chord, (c + t) * chord))
        lower.append((xn * chord, (c - t) * chord))
    pts = upper[:-1] + list(reversed(lower[1:]))  # closed loop, LE once, TE once
    # ensure counter-clockwise when viewed with x right, y up
    return pts


# ------------------------------------------------------------------------------ lofts

class Station:
    """One ring of a loft: a centre, a frame and a 2D outline drawn in that frame."""

    __slots__ = ("center", "right", "up", "points")

    def __init__(self, center, points, right=(1, 0, 0), up=(0, 0, 1)):
        self.center = Vector(center)
        self.right = Vector(right).normalized()
        self.up = Vector(up).normalized()
        self.points = points

    def ring(self):
        return [self.center + self.right * x + self.up * y for (x, y) in self.points]


def loft(name, stations, cap_start=True, cap_end=True, close_loop=False, smooth=True):
    """
    Skin a sequence of Stations (all with the same point count) into quads. Caps are n-gons
    (triangulated by the exporter). `close_loop` joins the last ring back to the first.
    """
    n = len(stations[0].points)
    for s in stations:
        assert len(s.points) == n, "every station needs the same point count"
    bm = bmesh.new()
    rings = []
    for s in stations:
        rings.append([bm.verts.new(p) for p in s.ring()])
    bm.verts.ensure_lookup_table()
    pairs = list(zip(rings, rings[1:]))
    if close_loop:
        pairs.append((rings[-1], rings[0]))
    for a, b in pairs:
        for i in range(n):
            j = (i + 1) % n
            try:
                bm.faces.new((a[i], a[j], b[j], b[i]))
            except ValueError:
                pass  # degenerate (coincident ring points at a tip)
    if not close_loop:
        if cap_start:
            try:
                bm.faces.new(list(reversed(rings[0])))
            except ValueError:
                pass
        if cap_end:
            try:
                bm.faces.new(rings[-1])
            except ValueError:
                pass
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return new_mesh_object(name, bm, smooth=smooth)


def spine_loft(name, spine, profiles, up=(0, 0, 1), cap_start=True, cap_end=True, smooth=True):
    """
    Loft along a polyline `spine` (list of 3D points) with one 2D outline per point. The frame
    at each station is built from the local tangent and the given up hint, so a curved
    fuselage, a drooping tail or a serpent body keep their cross-sections perpendicular.
    """
    pts = [Vector(p) for p in spine]
    stations = []
    upv = Vector(up)
    for i, p in enumerate(pts):
        if i == 0:
            t = (pts[1] - pts[0])
        elif i == len(pts) - 1:
            t = (pts[-1] - pts[-2])
        else:
            t = (pts[i + 1] - pts[i - 1])
        t.normalize()
        right = t.cross(upv)
        if right.length < 1e-6:
            right = Vector((1, 0, 0))
        right.normalize()
        u = right.cross(t).normalized()
        stations.append(Station(p, profiles[i], right=right, up=u))
    return loft(name, stations, cap_start=cap_start, cap_end=cap_end, smooth=smooth)


def fuselage(name, length, stations, segments=32, nose=(0, 0, 0), axis=(0, -1, 0), up=(0, 0, 1)):
    """
    The common hull builder: a straight spine from `nose` along `axis` of `length`, with
    `stations` = list of (t in 0..1, w, h_top, h_bottom, n_top, n_bottom, dy_up) describing
    the outline at that fraction of the length. `dy_up` lifts the outline's centre along the
    up axis (a drooping nose, a raised spine). Point 0 of every outline is on +right.
    """
    axis = Vector(axis).normalized()
    upv = Vector(up).normalized()
    right = axis.cross(upv).normalized()
    sts = []
    for (t, w, ht, hb, nt, nb, dy) in stations:
        c = Vector(nose) + axis * (t * length) + upv * dy
        prof = superellipse(w, ht, n=nt, segments=segments, h_bottom=hb, n_bottom=nb)
        sts.append(Station(c, prof, right=right, up=upv))
    return loft(name, sts, cap_start=True, cap_end=True)


# ----------------------------------------------------------------------------- lathes

def lathe(name, profile_rz, segments=32, axis=(0, 1, 0), origin=(0, 0, 0), up=(0, 0, 1), cap=True):
    """
    Surface of revolution. `profile_rz` is a list of (radius, distance-along-axis) pairs from
    the front to the back; a zero radius at either end closes it to a point. Engine pods,
    nozzles, domes, spines and spikes are all this.
    """
    axis = Vector(axis).normalized()
    upv = Vector(up)
    if abs(axis.dot(upv)) > 0.99:
        upv = Vector((1, 0, 0))
    right = axis.cross(upv).normalized()
    upv = right.cross(axis).normalized()
    stations = []
    for (r, z) in profile_rz:
        c = Vector(origin) + axis * z
        rr = max(r, 1e-5)
        pts = [(rr * math.cos(2 * math.pi * i / segments), rr * math.sin(2 * math.pi * i / segments)) for i in range(segments)]
        stations.append(Station(c, pts, right=right, up=upv))
    return loft(name, stations, cap_start=cap, cap_end=cap)


# ------------------------------------------------------------------------------ wings

def wing(name, root, span, chord_root, chord_tip, sweep=0.0, dihedral_deg=0.0, twist_deg=0.0,
         thickness=0.10, camber=0.0, sections=6, segments=20, side=1, forward=(0, -1, 0),
         up=(0, 0, 1), taper_power=1.0, tip_round=True):
    """
    A lofted airfoil wing. `root` is the leading-edge point at the root; the wing runs along
    +X (side=1) or -X (side=-1) for `span`; `sweep` moves the tip LE back (along -forward)
    by that distance; `dihedral_deg` tilts it up; `twist_deg` washes the tip out. Chord runs
    along -forward (the trailing edge is behind the leading edge).
    """
    fwd = Vector(forward).normalized()
    upv = Vector(up).normalized()
    out = upv.cross(fwd).normalized() * side  # spanwise direction: +X for side=1 when forward=-Y, up=+Z
    dih = math.radians(dihedral_deg)
    stations = []
    for i in range(sections + 1):
        t = i / sections
        tt = t ** taper_power
        chord = chord_root + (chord_tip - chord_root) * tt
        if tip_round and i == sections:
            chord *= 0.35
        le = Vector(root) + out * (span * t) * math.cos(dih) + upv * (span * t) * math.sin(dih) - fwd * (sweep * tt)
        prof = airfoil(chord=chord, thickness=thickness, camber=camber, segments=segments)
        tw = math.radians(twist_deg) * t
        # the section plane: x along -forward (chord), y along up, rotated by twist about span
        rot = Matrix.Rotation(tw, 3, out)
        sx = rot @ (-fwd)
        sy = rot @ upv
        stations.append(Station(le, prof, right=sx, up=sy))
    return loft(name, stations, cap_start=True, cap_end=True)


def plate(name, outline_xy, thickness, normal=(0, 0, 1), origin=(0, 0, 0), right=(1, 0, 0), bevel_depth=0.0):
    """A flat plate: a closed 2D outline extruded `thickness` along `normal`. Fins, blades, rudders."""
    nrm = Vector(normal).normalized()
    r = Vector(right).normalized()
    u = nrm.cross(r).normalized()
    r = u.cross(nrm).normalized()
    a = Station(Vector(origin) - nrm * (thickness / 2), outline_xy, right=r, up=u)
    b = Station(Vector(origin) + nrm * (thickness / 2), outline_xy, right=r, up=u)
    return loft(name, [a, b], cap_start=True, cap_end=True, smooth=False)


def box(name, size, center=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return new_mesh_object(name, bm, smooth=False)


def sphere(name, radius, center=(0, 0, 0), segments=24, rings=12, scale=(1, 1, 1)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=radius)
    bmesh.ops.scale(bm, vec=Vector(scale), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return new_mesh_object(name, bm)


def tube_along(name, points, radii, segments=12, cap=True):
    """A tube (varying radius) along a polyline. Tendrils, booms, chains, antennae."""
    profs = []
    for r in radii:
        rr = max(r, 1e-5)
        profs.append([(rr * math.cos(2 * math.pi * i / segments), rr * math.sin(2 * math.pi * i / segments)) for i in range(segments)])
    return spine_loft(name, points, profs, cap_start=cap, cap_end=cap)


# -------------------------------------------------------------------------- transforms

def move(obj, delta):
    obj.location = obj.location + Vector(delta)
    return obj


def rotate(obj, euler_deg, order="XYZ"):
    obj.rotation_euler = Euler([math.radians(a) for a in euler_deg], order)
    return obj


def scale(obj, s):
    if isinstance(s, (int, float)):
        s = (s, s, s)
    obj.scale = Vector(s)
    return obj


def apply_transform(obj):
    """Bake location/rotation/scale into the mesh so joins and exports see model space."""
    select_only(obj)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def transform_mesh(obj, matrix):
    """Apply a Matrix directly to the vertices (no ops, no selection games)."""
    me = obj.data
    for v in me.vertices:
        v.co = matrix @ v.co
    me.update()
    return obj


# ---------------------------------------------------------------------------- finishing

def set_smooth(obj, smooth=True):
    obj.data.polygons.foreach_set("use_smooth", [smooth] * len(obj.data.polygons))
    obj.data.update()
    return obj


def sharpen_by_angle(obj, angle_deg=40.0):
    """
    Mark edges sharper than `angle_deg` as sharp, so smooth shading breaks at panel edges and
    the exporter writes split normals (Unity imports them as-is). Crisp hard surface without a
    modifier.
    """
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    thr = math.radians(angle_deg)
    for e in bm.edges:
        if len(e.link_faces) == 2:
            a = e.calc_face_angle(None)
            if a is not None and a > thr:
                e.smooth = False
        else:
            e.smooth = False
    bm.to_mesh(me)
    bm.free()
    me.update()
    return obj


def bevel(obj, width=0.02, segments=2, angle_deg=30.0, profile=0.7, clamp=True):
    m = obj.modifiers.new("Bevel", "BEVEL")
    m.width = width
    m.segments = segments
    m.limit_method = "ANGLE"
    m.angle_limit = math.radians(angle_deg)
    m.profile = profile
    m.use_clamp_overlap = clamp
    m.harden_normals = False
    return obj


def subsurf(obj, levels=1):
    m = obj.modifiers.new("Subdivision", "SUBSURF")
    m.levels = levels
    m.render_levels = levels
    return obj


def crease_sharp_edges(obj, angle_deg=50.0, crease=1.0):
    """Crease edges over an angle so a subdivided part keeps its hard lines."""
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    layer = bm.edges.layers.float.get("crease_edge") or bm.edges.layers.float.new("crease_edge")
    thr = math.radians(angle_deg)
    for e in bm.edges:
        if len(e.link_faces) == 2:
            a = e.calc_face_angle(None)
            if a is not None and a > thr:
                e[layer] = crease
    bm.to_mesh(me)
    bm.free()
    me.update()
    return obj


def solidify(obj, thickness, offset=0.0):
    m = obj.modifiers.new("Solidify", "SOLIDIFY")
    m.thickness = thickness
    m.offset = offset
    return obj


def mirror(obj, axis="X", merge=True, merge_dist=1e-4):
    m = obj.modifiers.new("Mirror", "MIRROR")
    m.use_axis = (axis == "X", axis == "Y", axis == "Z")
    m.use_mirror_merge = merge
    m.merge_threshold = merge_dist
    return obj


def apply_modifiers(obj):
    select_only(obj)
    for m in list(obj.modifiers):
        try:
            bpy.ops.object.modifier_apply(modifier=m.name)
        except RuntimeError as e:
            raise RuntimeError("could not apply %s on %s: %s" % (m.name, obj.name, e))
    return obj


def decimate_to(obj, ratio):
    m = obj.modifiers.new("Decimate", "DECIMATE")
    m.ratio = ratio
    return obj


def remesh_voxel(obj, voxel_size):
    m = obj.modifiers.new("Remesh", "REMESH")
    m.mode = "VOXEL"
    m.voxel_size = voxel_size
    return obj


def boolean(obj, cutter, op="DIFFERENCE", solver="EXACT"):
    m = obj.modifiers.new("Boolean", "BOOLEAN")
    m.operation = op
    m.object = cutter
    m.solver = solver
    return obj


class UnstableSelection(Exception):
    """A paint / inset predicate puts a face's centre or normal within SELECTION_MARGIN of its threshold."""


SELECTION_MARGIN = 1e-5


def _stable_faces(faces_iter, predicate, what):
    """
    Evaluate `predicate(center, normal, material_index)` over faces and refuse any selection that
    flips when a centre or normal is nudged by SELECTION_MARGIN. Why: bmesh ops such as
    inset_region iterate pointer-keyed hashes, so the vertices they create differ by ~1e-7 from
    run to run; a threshold that sits ON a face centre (the Urchin's latitude band at
    |y| < 0.26 met faces at 0.2600037) then selects a different face set on every build, the
    vertex count wobbles by 2 and `build.py --check` reports drift that is not in the spec.
    `faces_iter` yields (face, center, normal, material_index); returns the selected faces.
    """
    sel, unstable = [], []
    d = SELECTION_MARGIN
    nudges = [Vector((d, 0, 0)), Vector((-d, 0, 0)), Vector((0, d, 0)), Vector((0, -d, 0)), Vector((0, 0, d)), Vector((0, 0, -d))]
    for face, c, n, i in faces_iter:
        base = bool(predicate(c, n, i))
        for v in nudges:
            if bool(predicate(c + v, n, i)) != base or bool(predicate(c, n + v, i)) != base:
                unstable.append((c.copy(), n.copy(), i))
                break
        if base:
            sel.append(face)
    if unstable:
        c, n, i = unstable[0]
        raise UnstableSelection(
            "%s: %d face(s) sit within %g of the predicate's threshold (first: centre (%.6f, %.6f, %.6f) normal "
            "(%.4f, %.4f, %.4f) slot %d); move the threshold off the face centres so every build selects the same faces"
            % (what, len(unstable), d, c.x, c.y, c.z, n.x, n.y, n.z, i))
    return sel


def _inset_region(bm, faces, thickness, depth=0.0, slot=None):
    """
    A DETERMINISTIC region inset. `bmesh.ops.inset_region` walks pointer-keyed hashes, so the
    ring vertices it creates come out in a different ORDER and at different positions (median
    6 mm, max 0.5 units on a 0.85-unit hull) from one process to the next; two builds of the
    same spec then differ, and `build.py --check` cannot tell drift from noise (LESSONS 26).
    This walks the selected faces in mesh order, offsets every boundary vertex inward along
    the average in-plane normal of its boundary edges (even offset: divided by the cosine of
    the half-angle, floored so a sharp corner cannot shoot off), rebuilds the region's faces on
    the new ring, adds one rim quad per boundary edge, and optionally sinks the region by
    `depth` along its faces' normals. Returns the list of inner faces.
    """
    sel = set(f.index for f in faces)
    boundary = []  # (edge, loop of the selected face along it), in face/loop order
    seen = set()
    for f in faces:
        for l in f.loops:
            e = l.edge
            if e.index in seen:
                continue
            if len(e.link_faces) < 2 or any(lf.index not in sel for lf in e.link_faces):
                seen.add(e.index)
                boundary.append((e, l))
    inward, vert_of = {}, {}
    for e, l in boundary:
        f = l.face
        a, b = l.vert, l.link_loop_next.vert
        edir = (b.co - a.co)
        if edir.length < 1e-12:
            continue
        inw = f.normal.cross(edir.normalized())
        if inw.length < 1e-12:
            continue
        inw.normalize()
        for v in (a, b):
            inward.setdefault(v.index, []).append(inw)
            vert_of[v.index] = v
    newv = {}
    for vi, dirs in inward.items():
        v = vert_of[vi]
        d = Vector((0, 0, 0))
        for x in dirs:
            d += x
        if d.length < 1e-9:
            d = dirs[0].copy()
        d.normalize()
        cos_half = max(min(d.dot(dirs[0]), 1.0), 0.25)
        newv[vi] = bm.verts.new(v.co + d * (thickness / cos_half))
    inner, replaced, push = [], [], {}
    for f in faces:
        verts = [newv.get(v.index, v) for v in f.verts]
        if all(nv is v for nv, v in zip(verts, f.verts)):
            nf = f  # an interior face: nothing to rebuild
        else:
            nf = bm.faces.new(verts)
            nf.material_index = f.material_index
            nf.smooth = f.smooth
            replaced.append(f)
        if slot is not None:
            nf.material_index = slot
        inner.append(nf)
        if depth:
            for v in verts:
                push.setdefault(id(v), [v, []])[1].append(-f.normal * depth)
    for e, l in boundary:
        a, b = l.vert, l.link_loop_next.vert
        if a.index not in newv or b.index not in newv:
            continue
        rim = bm.faces.new([a, b, newv[b.index], newv[a.index]])
        rim.material_index = l.face.material_index
        rim.smooth = l.face.smooth
    if replaced:
        bmesh.ops.delete(bm, geom=replaced, context="FACES")
    for v, vecs in push.values():
        acc = Vector((0, 0, 0))
        for x in vecs:
            acc += x
        v.co += acc / len(vecs)
    return inner


def inset_faces(obj, predicate, thickness, depth=0.0, slot=None):
    """
    Inset every face passing `predicate(face_center, face_normal, material_index)` and push
    it in by `depth`; the inner faces take material `slot` if given. Panel lines, vents, the
    glass of a canopy sunk into its frame - all this. Deterministic (`_inset_region`).
    """
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    # bm.from_mesh does not compute face normals: without this, `f.normal` is whatever memory held
    # and a face near the predicate's threshold flips between runs (LESSONS 26).
    bm.normal_update()
    bm.verts.ensure_lookup_table()
    bm.edges.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    faces = _stable_faces(((f, f.calc_center_median(), f.normal, f.material_index) for f in bm.faces), predicate, "inset_faces on %s" % obj.name)
    if faces:
        _inset_region(bm, faces, thickness, depth, slot)
    bm.to_mesh(me)
    bm.free()
    me.update()
    return obj


def paint(obj, slot, predicate=None):
    """Set the material index of every face (or every face passing predicate(center, normal, index))."""
    me = obj.data
    if predicate is None:
        me.polygons.foreach_set("material_index", [slot] * len(me.polygons))
    else:
        for p in _stable_faces(((p, Vector(p.center), Vector(p.normal), p.material_index) for p in me.polygons), predicate, "paint on %s" % obj.name):
            p.material_index = slot
    me.update()
    return obj


def ensure_slots(obj, names):
    """Give the object exactly these material slots, in order, creating materials by name."""
    while len(obj.data.materials) < len(names):
        obj.data.materials.append(None)
    for i, n in enumerate(names):
        mat = bpy.data.materials.get(n) or bpy.data.materials.new(n)
        obj.data.materials[i] = mat
    return obj


# --------------------------------------------------------------------------- assembly

def tag_group(obj, group_name=None, weight=1.0):
    """Put every vertex of obj into a vertex group (default: the object's name)."""
    name = group_name or obj.name
    vg = obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
    vg.add(list(range(len(obj.data.vertices))), weight, "REPLACE")
    return obj


def join(parts, name, slots):
    """
    Merge parts into one mesh named `name` with material slots `slots` (list of names, by
    index). Each part keeps a vertex group of its own name (its skin/morph mask). Parts must
    already use the same slot indices (paint them with the shared index convention first).
    """
    for p in parts:
        ensure_slots(p, slots)
        if not p.vertex_groups.get(p.name):
            tag_group(p)
    select_only(parts[0])
    for p in parts:
        p.select_set(True)
    bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    obj.data.name = name
    return obj


def mirror_part(obj, new_name, axis="X", group_name=None):
    """
    A mirrored COPY of a part (left wing from right wing), with the group name swapped so the
    rig can tell them apart: 'wing.R' -> 'wing.L' by default.
    """
    me = obj.data.copy()
    new = bpy.data.objects.new(new_name, me)
    _link(new)
    new.location = obj.location
    new.rotation_euler = obj.rotation_euler
    new.scale = obj.scale
    m = Matrix.Scale(-1, 4, Vector((1, 0, 0)) if axis == "X" else (Vector((0, 1, 0)) if axis == "Y" else Vector((0, 0, 1))))
    for v in me.vertices:
        v.co = m @ v.co
    # a mirror flips winding; put it back
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.reverse_faces(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    me.update()
    for vg in list(new.vertex_groups):
        new.vertex_groups.remove(vg)
    tag_group(new, group_name or new_name)
    return new


def bounds(obj):
    """World-space AABB (min, max) of a mesh object."""
    mw = obj.matrix_world
    pts = [mw @ v.co for v in obj.data.vertices]
    if not pts:
        return Vector((0, 0, 0)), Vector((0, 0, 0))
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


# ------------------------------------------------------------------- outline helpers
# 2D outlines for plate(): all counter-clockwise, in the plate's (right, up) frame.

def rounded_rect(w, h, r=None, corner_segments=4, cx=0.0, cy=0.0):
    """A rounded rectangle centred on (cx, cy)."""
    r = min(r if r is not None else min(w, h) * 0.25, w / 2, h / 2)
    pts = []
    corners = ((w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180), (w / 2 - r, -h / 2 + r, 270))
    for (ccx, ccy, a0) in corners:
        for i in range(corner_segments + 1):
            a = math.radians(a0 + 90 * i / corner_segments)
            pts.append((cx + ccx + r * math.cos(a), cy + ccy + r * math.sin(a)))
    return pts


def feather(length, width, tip_round=0.6, root_width=None, segments=10, curve=0.0):
    """
    A feather / petal / wing-plate outline from the root (x=0) to a rounded tip (x=length),
    widest at `width` near 60% of the length, narrowing to `root_width` at the root. `curve`
    bows the whole outline sideways (y) toward the tip.
    """
    root_width = width * 0.55 if root_width is None else root_width
    upper, lower = [], []
    for i in range(segments + 1):
        t = i / segments
        w = root_width + (width - root_width) * math.sin(math.pi * min(t / 0.6, 1.0) / 2) if t < 0.6 else width * (1 - ((t - 0.6) / 0.4) ** 2 * (1 - tip_round * 0.15))
        if t >= 0.6:
            w = width * math.sqrt(max(0.0, 1 - ((t - 0.6) / 0.4) ** 2)) * (0.3 + 0.7 * tip_round) + width * 0.08 * (1 - t)
        yoff = curve * t * t
        upper.append((t * length, yoff + w / 2))
        lower.append((t * length, yoff - w / 2))
    return upper + list(reversed(lower))


def chevron(width, depth, thickness_frac=0.35, sweep=0.0):
    """
    A V-shaped plate outline (open side toward +y): width across the arms, depth from the
    point to the arm tips, arm thickness as a fraction of depth. The Squirrel's tail is nine
    of these; a rib cage, a cheveroned hull band or a spine fin is the same shape.
    """
    t = depth * thickness_frac
    return [
        (0.0, 0.0), (width / 2, depth + sweep), (width / 2, depth + sweep + t * 0.6),
        (width / 2 - t * 0.9, depth + sweep + t * 0.6), (0.0, t * 1.1 + sweep * 0.0),
        (-width / 2 + t * 0.9, depth + sweep + t * 0.6), (-width / 2, depth + sweep + t * 0.6), (-width / 2, depth + sweep),
    ]


def blade(length, width, tip=0.15, base=1.0):
    """A tapered blade / fin outline from the base (x=0, full width) to a narrow tip."""
    return [(0, -width * base / 2), (length, -width * tip / 2), (length, width * tip / 2), (0, width * base / 2)]


def cone(name, radius, length, axis=(0, 1, 0), origin=(0, 0, 0), segments=16, tip_radius=0.0, base_cap=True):
    """A cone/frustum from `origin` along `axis`."""
    prof = [(radius, 0.0), (tip_radius if tip_radius > 0 else 0.0, length)]
    return lathe(name, prof, segments=segments, axis=axis, origin=origin, cap=base_cap)


def crescent(outer_r, inner_r, span_deg=150.0, segments=12, tips_sharp=0.6):
    """
    A crescent band outline: an arc of radius outer_r down to inner_r across span_deg,
    symmetric about +y, open toward -y... i.e. the horns point toward -y. The tips thin out
    by tips_sharp (0 = square ends, 1 = needle points). Tail plates, ribs, collars, eyebrows.
    """
    a0 = math.radians(-span_deg / 2)
    a1 = math.radians(span_deg / 2)
    outer, inner = [], []
    for i in range(segments + 1):
        t = i / segments
        a = a0 + (a1 - a0) * t
        # measured from +y: x = r sin a, y = r cos a
        edge = 1.0 - tips_sharp * (abs(2 * t - 1) ** 3)
        r_in = inner_r + (outer_r - inner_r) * (1 - edge)
        outer.append((outer_r * math.sin(a), outer_r * math.cos(a)))
        inner.append((r_in * math.sin(a), r_in * math.cos(a)))
    return outer + list(reversed(inner))


def cut_plane(obj, point, normal):
    """
    Slice every face of a part along a plane (no geometry removed). A `plate()` has ONE top
    polygon, so a paint rule on "the tip third" sees one face centre in the middle and paints
    nothing; cut the plate first and the rule has faces to choose from. Also the way to put a
    panel line exactly where a design wants it.
    """
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=Vector(point), plane_no=Vector(normal).normalized(), dist=1e-6)
    bm.to_mesh(me)
    bm.free()
    me.update()
    return obj
