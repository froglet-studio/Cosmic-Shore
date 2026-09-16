#!/usr/bin/env python3
"""Prove a fauna's HEART is seated inside its own BODY CAVITY, ahead of its own prisms.

Docs/ECOSYSTEM.md §23.9 says a heart seats at the FRONT of its member's own prisms with
the body trailing — the tadpole arrangement — never buried inside them. §45/§46 add the
other half, which §23.9 never had to state because no species had a hollow body: a heart
that is ahead of the prisms and ALSO ahead of the body's own mouth is not seated at all,
it is floating in front of the creature. The Clawfish shipped both failures in turn: for
two years at z −4.07, five units deep inside its head, and then — after the first fix —
at z +1.32, 1.6 units in FRONT of the open mouth, which reads as something the fish is
about to swallow.

The rule this gate holds
------------------------
  1. the whole heart lies BEHIND the body's mouth plane;
  2. the seat is the SHALLOWEST one that satisfies (1) — the horn narrows going back, so
     how far the heart shows through the hull grows monotonically with depth, and
     shallowest-and-legal is therefore also most-enclosed;
  3. the heart is AHEAD of every one of the creature's own body prisms (§23.9).

It also MEASURES, and reports without gating, how far the heart's sphere protrudes past
the body's outer surface — because on this fleet a heart is deliberately about as wide as
its creature (the QuadFish's is 1.98 world scale inside a 17.5-unit fish), so "fully
enclosed with clearance" is a standard nothing shipped would pass and asserting it would
be asserting a fiction.

What it measures, and from where
--------------------------------
  body extent   the SHIPPED FBX's own vertices, normalised by its UnitScaleFactor
  body pose     the `m_LocalPosition.z` override on the prefab's nested model instance
  heart pose    the `m_LocalPosition.z` override on the prefab's nested crystal instance
  prism poses   the `m_LocalPosition.z` overrides on the nested HealthBlock instances
  heart size    BOTH sizes, because a heart has two:
                  * the AUTHORED prefab scale, which is what the prefab view shows
                  * the RUNTIME size, which `LifeFormCrystal.ApplyHeartSize` forces from
                    the config's `HeartWorldScale` (Docs/ECOSYSTEM.md §40)
                A seat that clears one and not the other is a seat that looks right in
                exactly one of the two places anyone ever looks at it.

Three traps this walks around, all recorded in .claude/skills/asset-surgery:
  * raw FBX extents from two files are NOT comparable — a file declaring UnitScaleFactor
    1 lands at 1/100 of its raw numbers, one declaring 100 lands 1:1.  The Clawfish
    body (unit 1) and the crystal mesh (unit 1) are both 100x their Unity size on disk.
  * `m_Modifications` rows WRAP across lines, so a one-line regex over the block matches
    nothing and reads as "no overrides".
  * a nested prefab instance's pose is NOT a `!u!4` document — it lives as
    propertyPath/value rows inside its own instance block.

Usage:  python3 Tools/Build/verify_fauna_heart_seat.py [--check]
        Exit 1 if any species' heart is not seated in its own cavity.
"""

import glob
import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx_binary  # noqa: E402

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# Species whose body is ONE nested model instance and whose heart is ONE nested crystal
# instance — the shape this checker can measure exactly.  A species assembled from
# HealthPrisms (Shark, Brittlestar) or from members (WormColony) is a different walk and
# is deliberately NOT guessed at here; see "Not covered" below.
SPECIES = [
    {
        "name": "Clawfish",
        "prefab": "Assets/_Prefabs/FloraAndFauna/Clawfish.prefab",
        "body_fbx": "Assets/_Models/Fauna/ClawfishTest.fbx",
        "body_fid": "-8679921383154817045",   # ClawfishTest.fbx root transform
        "heart_fid": "6588436222817790493",   # CrystalSpace.prefab root transform
        "heart_prefab": "Assets/_Prefabs/Environment/CrystalSpace.prefab",
        "heart_fbx": "Assets/_Models/spacecrystalanim.fbx",
        "configs": "Assets/_SO_Assets/Lifeforms/Clawfish Fauna *.asset",
        "prism_fid": "5222650486365209692",   # HealthBlock.prefab root transform
        # Both seats this species has shipped, asserted to FAIL. A pass is then evidence
        # the test can tell a seat from a float, not evidence that it always says yes.
        # Each names the rule it must break, because the two failed DIFFERENTLY and a
        # control that fires for the wrong reason proves nothing about the right one.
        "negative_control_z": [(1.32, "mouth"), (-4.07, "depth")],
    },
]


def fbx_bounds(path):
    """(lo, hi) of every vertex in an FBX, in UNITY units."""
    nodes, _version, _footer = fbx_binary.read(path)
    top = {n.name: n for n in nodes}
    unit = 1.0
    props = top["GlobalSettings"].first("Properties70")
    for child in (props.children if props else []):
        vals = [v for _t, v in child.props]
        if vals and vals[0] == "UnitScaleFactor":
            unit = float(vals[-1])
    # Unity's importer applies the cm->m conversion: a file declaring 100 lands 1:1,
    # a file declaring 1 lands at 1/100 of its raw numbers.
    scale = 0.01 if unit == 1.0 else 1.0

    lo = [float("inf")] * 3
    hi = [float("-inf")] * 3
    for node in nodes:
        if node.name != "Objects":
            continue
        for geo in node.find("Geometry"):
            verts = geo.first("Vertices")
            if not verts or not verts.props:
                continue
            flat = verts.props[0][1]
            for axis in range(3):
                comp = flat[axis::3]
                if not comp:
                    continue
                lo[axis] = min(lo[axis], min(comp) * scale)
                hi[axis] = max(hi[axis], max(comp) * scale)
    return lo, hi


def instance_override(text, target_fid, property_path):
    """One `m_Modifications` value for a given nested instance's transform.

    Rows WRAP — the `- target: {fileID: N, guid: ...,` line runs onto a second line and
    the value sits one line BELOW its propertyPath — so this walks lines rather than
    running a regex across the block.
    """
    lines = text.split("\n")
    want = "propertyPath: %s" % property_path
    for i, line in enumerate(lines):
        if line.strip() != want or i + 1 >= len(lines):
            continue
        context = "\n".join(lines[max(0, i - 3):i])
        if ("fileID: %s," % target_fid) in context:
            return float(lines[i + 1].split("value:")[1].strip())
    return None


# How far past the shallowest legal seat the heart may sit. Zero would be brittle against
# float rounding in an authored value; a quarter unit on a 17-unit fish is a tenth of the
# heart's own radius, so a seat that passes is a seat nobody moved by eye.
DEPTH_SLACK_LIMIT = 0.25


def all_overrides(text, target_fid, property_path):
    """Every `m_Modifications` value for a property across ALL instances of one source."""
    lines = text.split("\n")
    want = "propertyPath: %s" % property_path
    out = []
    for i, line in enumerate(lines):
        if line.strip() != want or i + 1 >= len(lines):
            continue
        context = "\n".join(lines[max(0, i - 3):i])
        if ("fileID: %s," % target_fid) in context:
            out.append(float(lines[i + 1].split("value:")[1].strip()))
    return out


def body_triangles(path, z_offset):
    """The LARGEST connected component of an FBX, as triangles in prefab space.

    Largest, not all: the Clawfish's two tail flukes are separate shells, and a ray cast
    against them would report the FLUKE as the body's outer surface at a z where the horn
    is far narrower.
    """
    import collections
    nodes, _v, _f = fbx_binary.read(path)
    top = {n.name: n for n in nodes}
    geo = top["Objects"].find("Geometry")[0]
    flat = geo.first("Vertices").props[0][1]
    pts = [(flat[i] / 100.0, flat[i + 1] / 100.0, flat[i + 2] / 100.0 + z_offset)
           for i in range(0, len(flat), 3)]
    idx = geo.first("PolygonVertexIndex").props[0][1]
    polys, cur = [], []
    for a in idx:
        if a < 0:
            cur.append(-a - 1)
            polys.append(cur)
            cur = []
        else:
            cur.append(a)

    parent = list(range(len(pts)))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[ra] = rb

    welded = collections.defaultdict(list)
    for i, p in enumerate(pts):
        welded[(round(p[0], 4), round(p[1], 4), round(p[2], 4))].append(i)
    for ids in welded.values():
        for i in ids[1:]:
            union(ids[0], i)
    for poly in polys:
        for a in poly[1:]:
            union(poly[0], a)

    groups = collections.defaultdict(list)
    for i in range(len(pts)):
        groups[find(i)].append(i)
    root = find(max(groups.values(), key=len)[0])

    tris = []
    for poly in polys:
        if find(poly[0]) != root:
            continue
        for i in range(1, len(poly) - 1):
            tris.append((pts[poly[0]], pts[poly[i]], pts[poly[i + 1]]))
    return tris


def _ray_triangle(origin, direction, a, b, c):
    e1 = [b[i] - a[i] for i in range(3)]
    e2 = [c[i] - a[i] for i in range(3)]
    h = [direction[1] * e2[2] - direction[2] * e2[1],
         direction[2] * e2[0] - direction[0] * e2[2],
         direction[0] * e2[1] - direction[1] * e2[0]]
    det = sum(e1[i] * h[i] for i in range(3))
    if abs(det) < 1e-12:
        return None
    inv = 1.0 / det
    s = [origin[i] - a[i] for i in range(3)]
    u = inv * sum(s[i] * h[i] for i in range(3))
    if u < 0.0 or u > 1.0:
        return None
    q = [s[1] * e1[2] - s[2] * e1[1],
         s[2] * e1[0] - s[0] * e1[2],
         s[0] * e1[1] - s[1] * e1[0]]
    v = inv * sum(direction[i] * q[i] for i in range(3))
    if v < 0.0 or u + v > 1.0:
        return None
    t = inv * sum(e2[i] * q[i] for i in range(3))
    return t if t > 1e-7 else None


def protrusion(body_path, z_offset, centre, radius, samples=600):
    """(worst protrusion past the hull, the body's widest radius) — or None.

    A ray that hits nothing has left through the body's OPEN mouth, which is where the
    heart is meant to be visible from; only directions that meet the hull can protrude.
    """
    tris = body_triangles(body_path, z_offset)
    if not tris:
        return None
    golden = math.pi * (3.0 - math.sqrt(5.0))
    worst = 0.0
    widest = 0.0
    for p in (v for t in tris for v in t):
        widest = max(widest, math.hypot(p[0], p[1]))
    for i in range(samples):
        y = 1.0 - 2.0 * (i + 0.5) / samples
        r = math.sqrt(max(0.0, 1.0 - y * y))
        theta = golden * i
        d = (r * math.cos(theta), y, r * math.sin(theta))
        hits = [t for t in (_ray_triangle(centre, d, *tri) for tri in tris) if t is not None]
        if hits:
            worst = max(worst, radius - max(hits))
    return max(0.0, worst), widest


def check(spec):
    """Returns (ok, [lines to print], [failures])."""
    out, fail = [], []
    prefab = open(os.path.join(REPO, spec["prefab"]), encoding="utf-8").read()

    body_z = instance_override(prefab, spec["body_fid"], "m_LocalPosition.z") or 0.0
    heart_z = instance_override(prefab, spec["heart_fid"], "m_LocalPosition.z")
    heart_authored = instance_override(prefab, spec["heart_fid"], "m_LocalScale.z")
    if heart_z is None or heart_authored is None:
        fail.append("%s: could not read the heart's pose out of %s"
                    % (spec["name"], spec["prefab"]))
        return False, out, fail

    blo, bhi = fbx_bounds(os.path.join(REPO, spec["body_fbx"]))
    mouth, tail = bhi[2] + body_z, blo[2] + body_z

    # the heart's own geometry: its mesh half-extent times the crystal prefab's
    # authored child scale (the crystal ROOT carries no mesh - only the model child does)
    clo, chi = fbx_bounds(os.path.join(REPO, spec["heart_fbx"]))
    mesh_half = max(max(abs(v) for v in clo), max(abs(v) for v in chi))
    crystal = open(os.path.join(REPO, spec["heart_prefab"]), encoding="utf-8").read()
    child = float(re.search(r"m_LocalScale: \{x: ([\d.]+)", crystal).group(1))

    runtime = 0.0
    for cfg in sorted(glob.glob(os.path.join(REPO, spec["configs"]))):
        m = re.search(r"HeartWorldScale: ([\d.]+)", open(cfg, encoding="utf-8").read())
        if m:
            runtime = max(runtime, float(m.group(1)))
    if runtime <= 0.0:
        fail.append("%s: no config authors a HeartWorldScale" % spec["name"])
        return False, out, fail

    prisms = all_overrides(prefab, spec["prism_fid"], "m_LocalPosition.z")

    out.append("%s" % spec["name"])
    out.append("  body   z [%.3f, %.3f]   mouth at %.3f   (cross-section x +-%.2f  y +-%.2f)"
               % (tail, mouth, mouth, max(abs(blo[0]), bhi[0]), max(abs(blo[1]), bhi[1])))
    out.append("  heart  z %.3f   authored scale %.3f   runtime HeartWorldScale %.3f"
               % (heart_z, heart_authored, runtime))
    out.append("  prisms z %s" % (", ".join("%.2f" % p for p in sorted(prisms)) or "NONE"))

    if not prisms:
        fail.append("%s: carries NO body prisms — it cannot be killed by shooting "
                    "(Docs/ECOSYSTEM.md §24)" % spec["name"])

    # 1 + 2: behind the mouth, and no deeper than it has to be
    biggest = max(heart_authored, runtime)
    half = mesh_half * child * biggest
    front = heart_z + half
    if front > mouth:
        fail.append("%s: the heart's front face is at %+.3f, %.3f units AHEAD of the "
                    "mouth plane %+.3f — it reads as something the fish is about to "
                    "swallow, not as a heart (Docs/ECOSYSTEM.md §46)"
                    % (spec["name"], front, front - mouth, mouth))
    else:
        out.append("    enclosure  front face %+.3f is %.3f behind the mouth %+.3f"
                   % (front, mouth - front, mouth))
    slack = mouth - front
    if 0.0 <= slack:
        out.append("    depth      %.3f units deeper than the shallowest legal seat "
                   "(the horn narrows going back, so shallowest = most enclosed)" % slack)
        if slack > DEPTH_SLACK_LIMIT:
            fail.append("%s: the heart sits %.3f units deeper than it needs to "
                        "(limit %.2f) — move it forward to %+.3f"
                        % (spec["name"], slack, DEPTH_SLACK_LIMIT, heart_z + slack))

    # 3: §23.9 — the heart leads, the prisms trail
    if prisms and max(prisms) >= heart_z:
        fail.append("%s: a body prism at z %+.3f is AHEAD of the heart at %+.3f "
                    "(Docs/ECOSYSTEM.md §23.9 — the heart seats at the FRONT of its own "
                    "prisms with the body trailing)" % (spec["name"], max(prisms), heart_z))
    elif prisms:
        out.append("    §23.9      the heart leads its own prisms by %.2f units"
                   % (heart_z - max(prisms)))

    # reported, never gated: how far the heart shows through its own hull
    show = protrusion(os.path.join(REPO, spec["body_fbx"]), body_z,
                      (0.0, 0.0, heart_z), half)
    if show is not None:
        out.append("    showing    the heart shows through the hull by at most %.3f "
                   "units (%.0f%% of the body's widest radius) — reported, not gated: "
                   "on this fleet a heart is about as wide as its creature"
                   % (show[0], 100.0 * show[0] / show[1] if show[1] else 0.0))

    for label, scale in (("authored", heart_authored), ("runtime ", runtime)):
        h = mesh_half * child * scale
        out.append("    %s  half-extent %.3f   front face %+.3f   %s"
                   % (label, h, heart_z + h,
                      "behind the mouth" if heart_z + h <= mouth else "AHEAD OF THE MOUTH"))

    for control, rule in spec.get("negative_control_z", []):
        over = (control + half) - mouth
        if rule == "mouth":
            fired, note = over > 0.0, "puts %.3f units of heart past the mouth" % over
        else:
            fired, note = (-over) > DEPTH_SLACK_LIMIT, \
                "buries it %.3f units deeper than the shallowest legal seat" % (-over)
        if not fired:
            fail.append("%s: NEGATIVE CONTROL DID NOT FIRE — the shipped-and-rejected "
                        "seat z=%+.2f should break the '%s' rule. This check is not "
                        "measuring what it claims to." % (spec["name"], control, rule))
        else:
            out.append("    control    the rejected seat z=%+.2f %s (so this test can "
                       "tell a seat from a float, and a seat from a burial)"
                       % (control, note))
    return not fail, out, fail


def main():
    any_fail = []
    print("fauna heart seat — Docs/ECOSYSTEM.md §23.9\n")
    for spec in SPECIES:
        _ok, out, fail = check(spec)
        print("\n".join(out))
        any_fail += fail
        print()

    print("Not covered (a different body walk, deliberately not guessed at):")
    print("  Shark / Brittlestar   body is HealthPrism children, not one model instance")
    print("  WormColony            the body is its MEMBERS; each carries its own heart")
    print("  QuadFish              heart sits at the ORIGIN inside a body 3.7x its own")
    print("                        width - centred and symmetric, and play-tested as is")

    if any_fail:
        print()
        for f in any_fail:
            print("FAIL: %s" % f)
        return 1
    print("\nOK: every measured heart is seated in its own cavity.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
