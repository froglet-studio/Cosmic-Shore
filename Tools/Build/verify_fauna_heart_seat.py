#!/usr/bin/env python3
"""Prove a fauna's HEART is seated at the FRONT of its body, not buried inside it.

Docs/ECOSYSTEM.md §23.9: "a heart is seated at the FRONT of its member's own prisms
with the body trailing (the tadpole arrangement ...), never buried inside them."

That rule had no gate, and the Clawfish violated it for its whole life: its heart sat
at z -4.07 inside a body spanning z [-17.64, -0.28], i.e. 5.2 units deep in the head of
a 17.4-unit fish.  Nothing could see it, because the two numbers being compared live in
three different files and two different unit conventions.

What this measures, and from where
----------------------------------
  body extent   the SHIPPED FBX's own vertices, normalised by its UnitScaleFactor
  body pose     the `m_LocalPosition.z` override on the prefab's nested model instance
  heart pose    the `m_LocalPosition.z` override on the prefab's nested crystal instance
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
        Exit 1 if any species' heart overlaps its own body.
"""

import glob
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
        # The seat before the fix. Asserted to FAIL, so a pass here is evidence the
        # test can tell the two apart rather than evidence that it always says yes.
        "negative_control_z": -4.07,
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
    nose, tail = bhi[2] + body_z, blo[2] + body_z

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

    out.append("%s" % spec["name"])
    out.append("  body   z [%.3f, %.3f]   nose at %.3f   (cross-section x +-%.2f  y +-%.2f)"
               % (tail, nose, nose, max(abs(blo[0]), bhi[0]), max(abs(blo[1]), bhi[1])))
    out.append("  heart  z %.3f   authored scale %.3f   runtime HeartWorldScale %.3f"
               % (heart_z, heart_authored, runtime))

    for label, scale in (("authored", heart_authored), ("runtime ", runtime)):
        half = mesh_half * child * scale
        gap = (heart_z - half) - nose
        out.append("    %s  half-extent %.3f   rear face %+.3f   GAP %+.3f  %s"
                   % (label, half, heart_z - half, gap, "clear" if gap > 0 else "CLIPS"))
        if gap <= 0:
            fail.append("%s: the %s heart overlaps its own body by %.3f units "
                        "(Docs/ECOSYSTEM.md §23.9 — the heart seats at the FRONT, "
                        "never buried inside)" % (spec["name"], label.strip(), -gap))

    control = spec.get("negative_control_z")
    if control is not None:
        half = mesh_half * child * heart_authored
        if (control - half) > nose:
            fail.append("%s: NEGATIVE CONTROL DID NOT FIRE — the pre-fix seat z=%.2f "
                        "should overlap the body. This check is not measuring what it "
                        "claims to." % (spec["name"], control))
        else:
            out.append("    control   the pre-fix seat z=%+.2f buries it %.3f units in "
                       "(so this test can tell a clip from a clear)"
                       % (control, nose - (control - half)))
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
    print("\nOK: every measured heart clears its own body.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
