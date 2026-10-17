"""
_common - helpers every vessel spec reaches for: matrix one-liners, mirrored-part winding
repair, re-tagging a joined sub-assembly as one part, a box-projected UV layer, and the
two assertions a spec should make on itself (bounds and slot counts) before it hands the
mesh to the exporter.
"""

import math

import bpy  # noqa: F401  (must precede bmesh/mathutils: it registers them)
import bmesh
from mathutils import Matrix, Vector


def rot_x(deg):
    return Matrix.Rotation(math.radians(deg), 4, "X")


def rot_y(deg):
    return Matrix.Rotation(math.radians(deg), 4, "Y")


def rot_z(deg):
    return Matrix.Rotation(math.radians(deg), 4, "Z")


def scale_axis(s, axis):
    return Matrix.Scale(s, 4, Vector(axis))


def translate(x, y, z):
    return Matrix.Translation(Vector((x, y, z)))


def aim(from_axis, to_dir):
    """A 4x4 rotation taking unit vector `from_axis` onto direction `to_dir`."""
    a = Vector(from_axis).normalized()
    b = Vector(to_dir).normalized()
    return a.rotation_difference(b).to_matrix().to_4x4()


def flip(obj):
    """A mirrored part has inverted winding; put the normals back outward."""
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.reverse_faces(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    me.update()


def mirror_x(forms, obj, new_name):
    """Right/left twin across the X=0 plane, winding repaired, tagged as its own part."""
    new = forms.mirror_part(obj, new_name, axis="X")
    return new


def retag(forms, obj, name=None):
    """After joining sub-pieces into one part, the part's own group is its name."""
    for vg in list(obj.vertex_groups):
        obj.vertex_groups.remove(vg)
    forms.tag_group(obj, name)
    return obj


def box_uv(obj):
    """A box-projected UV layer: untextured hulls need a layer, not a layout."""
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    uv = bm.loops.layers.uv.get("UVMap") or bm.loops.layers.uv.new("UVMap")
    lo = [min(v.co[i] for v in bm.verts) for i in range(3)]
    hi = [max(v.co[i] for v in bm.verts) for i in range(3)]
    span = [max(hi[i] - lo[i], 1e-6) for i in range(3)]
    for face in bm.faces:
        n = face.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        u_i, v_i = [(1, 2), (0, 2), (0, 1)][ax]
        for loop in face.loops:
            co = loop.vert.co
            loop[uv].uv = ((co[u_i] - lo[u_i]) / span[u_i], (co[v_i] - lo[v_i]) / span[v_i])
    bm.to_mesh(me)
    bm.free()
    me.update()
    return obj


def finish(forms, obj, bevel_width=0.01, sharp_deg=38.0, bevel_segments=1):
    """The common finishing pass for a hard-surface part: bevel, apply, sharp edges."""
    if bevel_width > 0:
        forms.bevel(obj, width=bevel_width, segments=bevel_segments, angle_deg=30)
        forms.apply_modifiers(obj)
    forms.sharpen_by_angle(obj, sharp_deg)
    return obj


def assert_bounds(forms, obj, want_lo, want_hi, tol=0.08):
    lo, hi = forms.bounds(obj)
    for i in range(3):
        if abs(lo[i] - want_lo[i]) > tol or abs(hi[i] - want_hi[i]) > tol:
            raise AssertionError("bounds axis %d: got %.3f..%.3f want %.3f..%.3f" % (i, lo[i], hi[i], want_lo[i], want_hi[i]))
    return lo, hi
