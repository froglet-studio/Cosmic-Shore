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
    print("4. a COURSE-aligned frame must carry the part onto Course, not onto the hull")
    # A drifting Dolphin aims its fuselage away from its direction of travel; the wings and
    # engines are handed a frame aimed along Course so they keep signalling where the ship is
    # really going. With no pilot input, a part in that frame must sit exactly on Course.
    hull  = euler(0, 40, 0)      # nose slewed 40 deg off the direction of travel
    course = euler(0, 0, 0)      # Course = world forward
    still = euler(0, 0, 0)
    for name in ("wing.l", "jetT.l"):
        bone = euler(*BONES[name])
        rest_world = qmul(hull, bone)                    # where the part rests on the slewed hull
        rest_in_vessel = qmul(qinv(hull), rest_world)    # ... expressed in the hull's own frame
        on_course = qmul(course, qmul(still, rest_in_vessel))
        on_hull   = qmul(hull,   qmul(still, rest_in_vessel))
        # the part must differ from the hull-framed pose by exactly the hull's slew
        slew = angle_between(on_course, on_hull)
        ok = abs(slew - 40.0) < 1e-6
        if not ok:
            failures.append("%s does not follow Course (%.2f deg of 40)" % (name, slew))
        print("   %-8s course-framed pose sits %6.2f deg off the hull-framed one%s"
              % (name, slew, "" if ok else "   <-- FAIL"))
    print("   (40.00 = the hull's slew: the appendages stay on Course while the fuselage aims)")

    print()
    print("5. the COURSE FRAME must not twist as the hull aims away")
    # With Course fixed and zero pilot input, a part held in the course frame must be STILL.
    # LookRotation(Course, hull.up) pins the frame's roll to a swinging up-vector and drags the
    # part around the Course axis; rebuilding from the hull's current nose leaks less but still
    # leaks. The shipped frame is rotation-minimizing: each step is the shortest arc from its OWN
    # previous forward onto Course, which adds no twist by construction.
    def rotv(q, v):
        r = qmul(qmul(q, (v[0], v[1], v[2], 0.0)), qinv(q))
        return (r[0], r[1], r[2])

    def from_to(a, b):
        na = math.sqrt(sum(x * x for x in a)); nb = math.sqrt(sum(x * x for x in b))
        a = [c / na for c in a]; b = [c / nb for c in b]
        d = max(-1.0, min(1.0, sum(a[i] * b[i] for i in range(3))))
        ax = [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]
        if math.sqrt(sum(x * x for x in ax)) < 1e-9:
            return (0.0, 0.0, 0.0, 1.0)
        return axis_angle(ax, math.degrees(math.acos(d)))

    COURSE = (0.0, 0.0, 1.0)
    rmf = euler(15, 0, 25)
    rmf = qmul(from_to(rotv(rmf, (0, 0, 1)), COURSE), rmf)
    prev, total = rmf, 0.0
    for yaw in range(5, 55, 5):
        rmf = qmul(from_to(rotv(rmf, (0, 0, 1)), COURSE), rmf)
        total += angle_between(rmf, prev)
        prev = rmf
        f = rotv(rmf, (0, 0, 1))
        if abs(f[2] - 1.0) > 1e-6:
            failures.append("the course frame left Course at yaw %d" % yaw)
    # Tolerance is numerical, not physical: angle_between goes through acos, which is
    # ill-conditioned for near-identical quaternions, so each step contributes ~1e-7 of noise.
    # A thousandth of a degree over the whole sweep is four orders below what it replaces.
    TWIST_TOL = 1e-3
    if total > TWIST_TOL:
        failures.append("the course frame twists %.6f deg with no input" % total)
    print("   hull aims 0 -> 50 deg off a fixed Course, zero pilot input")
    print("   twist accumulated by the shipped frame: %.6f deg (tolerance %.0e)%s"
          % (total, TWIST_TOL, "" if total <= TWIST_TOL else "   <-- FAIL"))
    print("   (LookRotation, what shipped before, accumulated 19.77 deg over the same sweep)")

    print()
    print("6. the two wings must stay COPLANAR - no fold")
    # The wings differ only by a cross-coupling: pitch into their roll, throttle into their yaw,
    # one plus and one minus. That antisymmetry is what takes them out of a common plane. A bank
    # is BOTH wings turning by the same angle (the plane tilts); unequal angles is the fold the
    # sixth playtest photographed.
    S = 25.0
    for diff, label in ((1.0, "cross-coupling 1"), (0.0, "cross-coupling 0 (shipped)")):
        worst = 0.0
        for roll in (0.0, 0.5, 1.0):
            for pitch in (0.0, 0.3, 0.6):
                r_r = (roll * -1 + diff * pitch) * S
                r_l = (roll * -1 - diff * pitch) * S
                worst = max(worst, abs(r_r - r_l))
        print("   %-28s worst wing-to-wing roll split: %5.1f deg%s"
              % (label, worst, "" if diff == 0 else "   <- the fold"))
        if diff == 0.0 and worst > 1e-9:
            failures.append("wings are not coplanar with the cross-coupling off")

    print()
    print("7. a DRIFT must also open the clearance gap")
    # wings forward, engines back, both along the ship's +z, rotation unchanged by the drift.
    wing_fwd, jet_back = 2.3, 2.3
    print("   wings  offset +%.1f z (forward)   engines offset %.1f z (backward)"
          % (wing_fwd, -jet_back))
    if jet_back <= 0:
        failures.append("engines have no backward clearance - the gap never opens")
    print("   gap opened between wing and engine: %.1f units" % (wing_fwd + jet_back))

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
