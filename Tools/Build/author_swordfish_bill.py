#!/usr/bin/env python3
"""
Author the Swordfish's SWORD: the mesh its blade draws, shaped as a swordfish's bill.

Garrett, 2026-11-08: "its sword should be the sword of the sword fish". The Swordfish's sword is the vessel's blade
(the ForceFieldSkimmer the triggers pose, ShieldSwipeActionExecutor; R_VesselActions/SWORDFISH.md). It drew Unity's
built-in capsule; it now draws this mesh, so at rest, lying straight ahead with its hilt in the hull's flat snout
(Tools/Build/vessel_model/specs/swordfish.py), it is the fish's bill.

The mesh lives in the CAPSULE'S OWN SPACE, so nothing that reads the blade changes: the hilt at local y = -1 and the
tip at +1 (the executor's hilt anchor, `bladeHalfExtentLocal` 1, pins y = -1 at the mount), its cross-section inside
the capsule's 0.5 radius (the CapsuleCollider that cuts is untouched), and the transform's scale still sets its size
(x 1.5, y = the Space-driven length 24..40, z 4.8). The bill is FLAT: wide along local z (laid level by the rest
rotation in Swordfish.prefab, which turns local z to the hull's right and local y to its nose) and thin along local
x. Its root is the snout's own width and depth (3.1 x 0.9 world units against the snout's 2.9 x 0.8), so the jaw
runs on into the sword; it tapers to a point, its edges sharp and a low ridge down its middle.

    python3 Tools/Build/author_swordfish_bill.py           # write Assets/_Models/Vessel Models/SwordfishBill.asset
    python3 Tools/Build/author_swordfish_bill.py --check   # the drift gate (exit 1 on drift)
"""

import hashlib
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
import author_primitive_meshes as P  # noqa: E402  (Builder, emit: the project's Mesh-asset writer)

NAME = "SwordfishBill"
OUT = os.path.join(REPO, "Assets", "_Models", "Vessel Models", NAME + ".asset")
GUID = hashlib.md5(b"CosmicShore/VesselModels/SwordfishBill").hexdigest()

STATIONS = 44          # rings from hilt to tip, packed toward the tip
SEGMENTS = 28          # points round each ring
ROOT_HALF_WIDTH = 0.32  # local z: x 4.8 scale -> 1.54 world each side at the root
ROOT_HALF_THICK = 0.30  # local x: x 1.5 scale -> 0.45 world each side at the root
EDGE_EXPONENT = 1.45    # superellipse exponent of the section: under 2 sharpens the two cutting edges
RIDGE = 0.18            # a low ridge along the middle, as a fraction of the thickness


def profile(t):
    """Half-width and half-thickness at t (0 hilt .. 1 tip): a long, gently tapering blade drawn to a point."""
    w = ROOT_HALF_WIDTH * (1.0 - t) ** 0.72
    h = ROOT_HALF_THICK * (1.0 - t) ** 0.85
    # the root swells a little into the snout socket
    swell = 1.0 + 0.10 * max(0.0, 1.0 - t / 0.06)
    return w * swell, h * swell


def section_point(t, a):
    w, h = profile(t)
    c, s = math.cos(a), math.sin(a)
    e = 2.0 / EDGE_EXPONENT
    z = w * math.copysign(abs(c) ** e, c)
    x = h * math.copysign(abs(s) ** e, s) * (1.0 + RIDGE * (1.0 - abs(c)))
    y = -1.0 + 2.0 * t
    return (x, y, z)


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def norm(v):
    m = math.sqrt(dot(v, v)) or 1.0
    return (v[0] / m, v[1] / m, v[2] / m)


def build():
    b = P.Builder()
    ts = [1.0 - (1.0 - k / STATIONS) ** 1.35 for k in range(STATIONS)]   # denser toward the tip; t < 1 here
    ring = [[section_point(t, 2 * math.pi * j / SEGMENTS) for j in range(SEGMENTS)] for t in ts]
    tip = (0.0, 1.0, 0.0)
    hilt = (0.0, -1.0, 0.0)

    def surface_normal(i, j):
        # central differences round the ring and along the blade
        p = ring[i]
        around = sub(p[(j + 1) % SEGMENTS], p[(j - 1) % SEGMENTS])
        nxt = ring[i + 1][j] if i + 1 < len(ring) else tip
        prv = ring[i - 1][j] if i > 0 else p[j]
        along = sub(nxt, prv)
        n = norm(cross(along, around))
        out = (p[j][0], 0.0, p[j][2])        # the outward sense: away from the blade's axis
        return n if dot(n, out) >= 0 else (-n[0], -n[1], -n[2])

    def tri(a, c, d, outward):
        pa, pc, pd = b.verts[a][0], b.verts[c][0], b.verts[d][0]
        if dot(cross(sub(pc, pa), sub(pd, pa)), outward) < 0:
            c, d = d, c
        b.tri(a, c, d)

    idx = [[b.v(ring[i][j], surface_normal(i, j), (j / SEGMENTS, ts[i])) for j in range(SEGMENTS)]
           for i in range(len(ring))]
    for i in range(len(ring) - 1):
        for j in range(SEGMENTS):
            k = (j + 1) % SEGMENTS
            mid = ring[i][j]
            out = (mid[0], 0.0, mid[2])
            tri(idx[i][j], idx[i + 1][j], idx[i + 1][k], out)
            tri(idx[i][j], idx[i + 1][k], idx[i][k], out)
    # the point: each last-ring edge closes on its own tip vertex (so the normals stay the face's)
    last = len(ring) - 1
    for j in range(SEGMENTS):
        k = (j + 1) % SEGMENTS
        pa, pb = ring[last][j], ring[last][k]
        n = norm(cross(sub(pb, pa), sub(tip, pa)))
        out = (pa[0] + pb[0], 0.0, pa[2] + pb[2])
        if dot(n, out) < 0:
            n = (-n[0], -n[1], -n[2])
        a = b.v(pa, n, (j / SEGMENTS, ts[last]))
        c = b.v(pb, n, (k / SEGMENTS, ts[last]))
        t_ = b.v(tip, n, ((j + 0.5) / SEGMENTS, 1.0))
        tri(a, c, t_, n)
    # the hilt cap, facing -y (it sits inside the snout socket)
    down = (0.0, -1.0, 0.0)
    centre = b.v(hilt, down, (0.5, 0.0))
    cap = [b.v(ring[0][j], down, (0.5 + 0.5 * math.cos(2 * math.pi * j / SEGMENTS), 0.5 + 0.5 * math.sin(2 * math.pi * j / SEGMENTS)))
           for j in range(SEGMENTS)]
    for j in range(SEGMENTS):
        tri(centre, cap[j], cap[(j + 1) % SEGMENTS], down)
    return b


def emit():
    body, _ = P.emit(NAME, build())
    meta = P.META.format(guid=GUID)
    return body, meta


def main():
    body, meta = emit()
    if "--check" in sys.argv:
        drift = []
        for path, want in ((OUT, body), (OUT + ".meta", meta)):
            have = open(path, newline="").read() if os.path.exists(path) else ""
            if have != want:
                drift.append(os.path.relpath(path, REPO))
        for d in drift:
            print("DRIFT", d)
        print("FAIL: re-run without --check" if drift else "OK: the Swordfish's bill matches")
        return 1 if drift else 0
    with open(OUT, "w", newline="") as f:
        f.write(body)
    with open(OUT + ".meta", "w", newline="") as f:
        f.write(meta)
    print("wrote %s (guid %s)" % (os.path.relpath(OUT, REPO), GUID))
    return 0


if __name__ == "__main__":
    sys.exit(main())
