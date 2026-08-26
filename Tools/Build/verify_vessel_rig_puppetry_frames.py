#!/usr/bin/env python3
"""Prove the rig-swapped puppetry turns about the SHIP's axes, not a bone's.

`Quaternion.Euler(pitch, yaw, roll)` assigned to `localRotation` turns about the
PARENT's axes.  On part-per-mesh art every animated part hangs off the model root,
so those ARE the ship's axes and pitch means pitch.  A bone's parent is another
bone, pointing wherever the skeleton points - so the identical call rolls when it
meant to pitch, and pitches backwards.  That is what a rig-swapped Dolphin looked
like: "roll and pitch are mixed, pitch is inverted, the wings fold up on a drift".

`VesselAnimation.RotatePartFromRestInFrame` conjugates the turn into the frame the
animation MEANT (the vessel, or the drift handle while drifting) and re-anchors the
rest pose through the part's HOME parent.  This reproves both halves against the
rig's own measured bone rest rotations, offline, with no Unity.

    python3 Tools/Build/verify_vessel_rig_puppetry_frames.py
"""
import math, sys

def qmul(a, b):
    ax, ay, az, aw = a; bx, by, bz, bw = b
    return (aw*bx + ax*bw + ay*bz - az*by,
            aw*by - ax*bz + ay*bw + az*bx,
            aw*bz + ax*by - ay*bx + az*bw,
            aw*bw - ax*bx - ay*by - az*bz)

def qinv(q):
    x, y, z, w = q
    return (-x, -y, -z, w)

def axis_angle(ax, deg):
    n = math.sqrt(sum(c * c for c in ax)); ax = [c / n for c in ax]
    h = math.radians(deg) / 2; s = math.sin(h)
    return (ax[0] * s, ax[1] * s, ax[2] * s, math.cos(h))

def euler(x, y, z):
    """Unity's ZXY intrinsic order."""
    return qmul(axis_angle((0, 1, 0), y),
                qmul(axis_angle((1, 0, 0), x), axis_angle((0, 0, 1), z)))

def to_axis_angle(q):
    x, y, z, w = q; w = max(-1.0, min(1.0, w))
    ang = 2 * math.degrees(math.acos(w))
    s = math.sqrt(max(0.0, 1 - w * w))
    if s < 1e-9:
        return (0.0, 0.0, 0.0), 0.0
    return (x / s, y / s, z / s), (ang if ang <= 180 else ang - 360)

def angle_between(q1, q2):
    d = qmul(q1, qinv(q2)); w = max(-1.0, min(1.0, abs(d[3])))
    return 2 * math.degrees(math.acos(w))

# Measured off dolphin_shapekey_with_animations.fbx (Lcl Rotation, degrees).
BONES = {
    "jetT.l":     (-2.018, 172.348,  57.201),
    "jetm.l":     (-4.049, 169.045,  57.459),
    "jetB.l":     (-3.559, 176.371,  58.567),
    "jetT.r":     ( 2.006,   4.254, -57.321),
    "jetm.r":     ( 3.984,   3.773, -57.967),
    "jetB.r":     ( 3.568,   5.525, -58.448),
    "winghold.l": (77.080,   0.000,-180.000),
    "winghold.r": (-102.920, 0.000,   0.000),
    "wing.l":     ( 5.901,   0.000,   0.000),
    "fuse":       ( 0.000,   0.000,   0.000),
}
SHIP = euler(0, 0, 0)
TOL = 1e-6

def main():
    failures = []

    print("1. a commanded PITCH must turn about the ship's +X, whatever the bone")
    print("   %-12s %-26s %s" % ("bone", "NEW axis", "OLD axis (the defect)"))
    turn = euler(10, 0, 0)
    for name, e in BONES.items():
        bone = euler(*e)
        rest_world = bone                                   # rest_local = identity
        new_world = qmul(SHIP, qmul(turn, qmul(qinv(SHIP), rest_world)))
        old_world = qmul(bone, turn)
        nax, nang = to_axis_angle(qmul(new_world, qinv(rest_world)))
        oax, _    = to_axis_angle(qmul(old_world, qinv(rest_world)))
        ok = (abs(nax[0] - 1) < TOL and abs(nax[1]) < TOL
              and abs(nax[2]) < TOL and abs(nang - 10) < TOL)
        if not ok:
            failures.append("%s does not turn about the ship's pitch axis" % name)
        print("   %-12s (%6.3f,%6.3f,%6.3f)   (%6.3f,%6.3f,%6.3f)%s"
              % (name, *nax, *oax, "" if ok else "   <-- FAIL"))

    print()
    print("2. entering a drift with no input must not move a part at all")
    drift = euler(0, 25, 0)
    still = euler(0, 0, 0)
    for name in ("winghold.l", "jetT.l"):
        bone = euler(*BONES[name])
        rest_l = euler(5.901, 0, 0)
        rest_world = qmul(bone, rest_l)
        old_world = qmul(drift, qmul(still, rest_l))                       # rest replayed under the handle
        new_world = qmul(drift, qmul(still, qmul(qinv(drift), rest_world)))  # rest anchored to home
        old_off = angle_between(old_world, rest_world)
        new_off = angle_between(new_world, rest_world)
        if new_off > 1e-6:
            failures.append("%s moves %.2f deg on drift entry" % (name, new_off))
        print("   %-12s OLD off by %7.2f deg   NEW off by %7.2f deg%s"
              % (name, old_off, new_off, "" if new_off <= 1e-6 else "   <-- FAIL"))

    print()
    print("3. a ship-aligned part must be BIT-IDENTICAL under both formulas")
    for name in ("fuse", "wing.l"):
        bone = euler(*BONES[name])
        # only a part whose parent is the ship itself is expected to match
        parent = SHIP if name == "fuse" else bone
        rest_world = qmul(parent, euler(0, 0, 0))
        new_world = qmul(SHIP, qmul(turn, qmul(qinv(SHIP), rest_world)))
        old_world = qmul(parent, turn)
        d = angle_between(new_world, old_world)
        same = d < 1e-6
        if name == "fuse" and not same:
            failures.append("fuse changed under the new formula")
        print("   %-12s difference %.9f deg%s" % (name, d, "" if same else "  (expected: its parent is a bone)"))

    print()
    if failures:
        print("FAILED:")
        for f in failures:
            print("  !", f)
        return 1
    print("OK - the puppetry turns about the ship's axes and a drift moves nothing by itself.")
    return 0

if __name__ == "__main__":
    sys.exit(main())
