"""
morphs.py - the four element shape keys, as deformations the pilot can read.

The fleet rule (CLAUDE.md, vessel CONTRACT section 7): the hull is an element display. Each of
charge / mass / space / time is a blend shape labelled by that name, and `VesselAnimation`
glides it between 0 and its last-frame weight as the vessel's effective level moves through
[0, 10]. The audit reads MAGNITUDE: a shape whose farthest vertex travels less than
`MinShapeTravelFraction` of the mesh diagonal is INERT, and the real shipped shapes travel
2.5-18% of it. So every morph here is a deformation with a measured travel, never a label.

What each element should mean, fleet-wide, so a pilot learns it once:
    Space  = reach / presence   -> the hull EXTENDS: wings, fins, antennae, span.
    Time   = rate / mobility    -> the hull SHARPENS and SWEEPS: a longer nose, raked wings,
                                   stretched engines - the silhouette of speed.
    Charge = threat / energy    -> the WEAPON grows: guns lengthen, spikes rise, engine
                                   nozzles flare, the hull bristles.
    Mass   = size / volume      -> the hull FATTENS: a fuller body, heavier shoulders.
A spec maps those onto its own parts; this module supplies the deformers and the baker.

A morph is a function (co, masks) -> new co applied to the FINAL joined mesh (after every
modifier is applied, so the topology is the one that ships). Masks are the part vertex
groups left by `forms.join`, so "grow the wings" is `mask('wing.l') + mask('wing.r')` with a
soft falloff at the root so the skin does not tear where a part meets the body.
"""

import math

import bpy
from mathutils import Vector

ELEMENTS = ("charge", "mass", "space", "time")


class MaskSet:
    """Per-vertex weights for every vertex group of a mesh, read once."""

    def __init__(self, obj):
        self.obj = obj
        self.names = [vg.name for vg in obj.vertex_groups]
        self.weights = {n: [0.0] * len(obj.data.vertices) for n in self.names}
        for v in obj.data.vertices:
            for g in v.groups:
                self.weights[obj.vertex_groups[g.group].name][v.index] = g.weight

    def mask(self, *names, default=0.0):
        """Union of groups (max weight)."""
        out = [default] * len(self.obj.data.vertices)
        for n in names:
            w = self.weights.get(n)
            if w is None:
                continue
            for i, x in enumerate(w):
                if x > out[i]:
                    out[i] = x
        return out

    def all(self):
        return [1.0] * len(self.obj.data.vertices)


def smoothstep(a, b, x):
    if b == a:
        return 1.0 if x >= b else 0.0
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


# --------------------------------------------------------------------------- deformers
# Each returns a function (index, co, normal) -> delta Vector. Compose them with `add_shape`.

def inflate(amount, mask, center=None, along_normals=True):
    """Push vertices out along their normal (or away from `center`) by amount*mask."""
    c = Vector(center) if center is not None else None

    def f(i, co, n):
        w = mask[i]
        if w <= 0:
            return Vector((0, 0, 0))
        d = n if (along_normals or c is None) else (co - c).normalized()
        return d * (amount * w)
    return f


def scale_about(factor, pivot, mask, axes=(1, 1, 1)):
    """Scale vertices about a pivot by `factor` on the chosen axes, weighted by mask."""
    p = Vector(pivot)

    def f(i, co, n):
        w = mask[i]
        if w <= 0:
            return Vector((0, 0, 0))
        d = co - p
        s = Vector(((factor - 1) * axes[0] * d.x, (factor - 1) * axes[1] * d.y, (factor - 1) * axes[2] * d.z))
        return s * w
    return f


def translate(delta, mask):
    d = Vector(delta)

    def f(i, co, n):
        return d * mask[i] if mask[i] > 0 else Vector((0, 0, 0))
    return f


def stretch_along(axis, origin, factor, mask, ramp=None):
    """
    Stretch along `axis` from `origin`: a point at distance d along the axis moves to
    d*factor. With `ramp` = (d0, d1) the effect fades in between those distances, so a nose
    can lengthen while the cabin stays put.
    """
    a = Vector(axis).normalized()
    o = Vector(origin)

    def f(i, co, n):
        w = mask[i]
        if w <= 0:
            return Vector((0, 0, 0))
        d = (co - o).dot(a)
        if ramp:
            w *= smoothstep(ramp[0], ramp[1], d)
        return a * (d * (factor - 1) * w)
    return f


def sweep(axis, origin, lean, mask, ramp=None):
    """Shear: move vertices along `lean` in proportion to their distance along `axis`."""
    a = Vector(axis).normalized()
    o = Vector(origin)
    l = Vector(lean)

    def f(i, co, n):
        w = mask[i]
        if w <= 0:
            return Vector((0, 0, 0))
        d = (co - o).dot(a)
        if ramp:
            w *= smoothstep(ramp[0], ramp[1], d)
        return l * (d * w)
    return f


def taper(axis, origin, length, factor_at_end, mask):
    """Shrink (factor<1) or flare (factor>1) the cross-section toward the end of `axis`."""
    a = Vector(axis).normalized()
    o = Vector(origin)

    def f(i, co, n):
        w = mask[i]
        if w <= 0:
            return Vector((0, 0, 0))
        d = (co - o).dot(a)
        t = max(0.0, min(1.0, d / length))
        radial = (co - o) - a * d
        return radial * ((factor_at_end - 1) * t * w)
    return f


def falloff_mask(obj, mask, origin, axis, start, end):
    """Multiply a mask by a smoothstep along an axis (0 before start, 1 after end)."""
    a = Vector(axis).normalized()
    o = Vector(origin)
    out = list(mask)
    for v in obj.data.vertices:
        d = (v.co - o).dot(a)
        out[v.index] *= smoothstep(start, end, d)
    return out


def radial_mask(obj, mask, center, inner, outer):
    """Multiply a mask by distance from a point: 1 inside `inner`, 0 beyond `outer`."""
    c = Vector(center)
    out = list(mask)
    for v in obj.data.vertices:
        d = (v.co - c).length
        out[v.index] *= 1.0 - smoothstep(inner, outer, d)
    return out


# ------------------------------------------------------------------------------ baking

def ensure_basis(obj):
    if obj.data.shape_keys is None:
        obj.shape_key_add(name="Basis", from_mix=False)
    return obj.data.shape_keys.key_blocks["Basis"]


def add_shape(obj, name, deformers):
    """
    Bake one shape key: the sum of every deformer's delta at each vertex. Normals come from
    the basis mesh. Returns (key_block, max_travel, travel_fraction_of_diagonal, moved_count).
    """
    ensure_basis(obj)
    me = obj.data
    me.calc_normals_split() if hasattr(me, "calc_normals_split") else None
    normals = [Vector(v.normal) for v in me.vertices]
    kb = obj.shape_key_add(name=name, from_mix=False)
    basis = obj.data.shape_keys.key_blocks["Basis"]
    lo = Vector((min(v.co.x for v in me.vertices), min(v.co.y for v in me.vertices), min(v.co.z for v in me.vertices)))
    hi = Vector((max(v.co.x for v in me.vertices), max(v.co.y for v in me.vertices), max(v.co.z for v in me.vertices)))
    diag = (hi - lo).length or 1.0
    max_travel = 0.0
    moved = 0
    for v in me.vertices:
        co = basis.data[v.index].co
        delta = Vector((0, 0, 0))
        for d in deformers:
            delta += d(v.index, co, normals[v.index])
        if delta.length > 1e-6:
            moved += 1
        if delta.length > max_travel:
            max_travel = delta.length
        kb.data[v.index].co = co + delta
    kb.value = 0.0
    kb.slider_max = 1.0
    return kb, max_travel, max_travel / diag, moved


def set_weights(obj, **weights):
    """Set shape key values by name (0..1) - for renders of the extremes."""
    if obj.data.shape_keys is None:
        return
    for kb in obj.data.shape_keys.key_blocks:
        if kb.name == "Basis":
            continue
        kb.value = float(weights.get(kb.name, 0.0))
