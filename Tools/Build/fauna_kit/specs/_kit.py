"""Helpers a celestial spec is written with (not a spec: the leading underscore keeps it out of `celestial.py --all`).

A body repeats round its travel axis (law L6), so a spec states ONE limb and `ring` turns it into n. `elements` writes
the four element forms from the body itself: Mass's scale is solved from the shield clearance the checker measures
(celestial.shield_dist), never typed in, and Space follows it by the fixed ratio.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import celestial as C  # noqa: E402


def angles(n, phase=90.0):
    return [math.radians(phase + 360.0 * k / n) for k in range(n)]


def polar(r, a, z):
    """A point r out from the travel axis at angle a, z along it."""
    return [round(r * math.cos(a), 6), round(r * math.sin(a), 6), round(z, 6)]


def lerp(a, b, t):
    return [round(a[i] + (b[i] - a[i]) * t, 6) for i in range(3)]


def spindle(name, parent, a, b, girth=1.0):
    return {"name": name, "parent": parent, "mesh": "braid", "from": list(a), "to": list(b), "girth": girth}


def on(s, name, kind, t, size, role=None, out=0.0):
    """A prism riding spindle s at share t of its length, its long axis along the limb, pushed `out` from the axis."""
    at = lerp(s["from"], s["to"], t)
    if out:
        r = math.hypot(at[0], at[1])
        if r > 1e-6:
            at = [round(at[0] * (r + out) / r, 6), round(at[1] * (r + out) / r, 6), at[2]]
    axis = [round(s["to"][i] - s["from"][i], 6) for i in range(3)]
    p = {"name": name, "spindle": s["name"], "kind": kind, "at": at, "size": list(size), "axis": axis}
    if role:
        p["role"] = role
    return p


def chain(prefix, parent, points, girth=1.0):
    """A limb of several spindles end to end (a lash, a flute): each the child of the one before, so the game's
    FaunaAttackDriver finds it as one chain and whips it."""
    out = []
    for i in range(len(points) - 1):
        out.append(spindle(f"{prefix}_{i}", parent if i == 0 else f"{prefix}_{i - 1}", points[i], points[i + 1], girth))
    return out


def elements(prisms, margin=1.01):
    """The four element forms (law L8). Mass grows until the heart's swing clears every plain prism's shield."""
    plain = [p for p in prisms if p["kind"] == "health"]
    need = max(C.MASS_SWING / max(C.shield_dist(p, [0, 0, 0]), 1e-9) for p in plain)
    mass = math.ceil(need * margin * 100) / 100
    return {"Mass": {"body_scale": mass, "shield_all": True},
            "Charge": {"body_scale": 1.0, "all_danger": True},
            "Space": {"body_scale": round(mass * C.SPACE_OVER_MASS, 4), "erode_spindle": True,
                      "span_over_mass": C.SPACE_OVER_MASS, "stinger_thins": True},
            "Time": {"body_scale": 1.0, "speed_scale": C.TIME_SPEED}}


# ---- grown limbs (organic.py; the relaxed laws of 2026-11-16) ----

def grown(name, parent, points, girth=(0.5, 0.2), strands=3, twist=1.5, sway=None, normal=None, step=0.9):
    """A limb grown along a smooth curve through `points`, tapering from girth[0] to girth[1]."""
    s = {"name": name, "parent": parent, "mesh": "grown", "path": [list(p) for p in points], "from": list(points[0]),
         "to": list(points[-1]), "girth": list(girth), "strands": strands, "twist": twist, "step": step}
    if sway:
        s["sway"] = dict(sway)
    if normal:
        s["normal"] = list(normal)
    return s


def along(s, t):
    """The point a share t of the way down a grown limb, and the direction it runs there."""
    import organic
    pts = organic.smooth(s["path"], 41)
    i = min(39, max(0, int(round(t * 40))))
    a, b = pts[i], pts[i + 1]
    return [round(v, 6) for v in a], [round(b[c] - a[c], 6) for c in range(3)]
