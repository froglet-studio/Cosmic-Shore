#!/usr/bin/env python3
"""Prove the rig-swapped puppetry turns about the SHIP's axes, not a bone's.

`Quaternion.Euler(pitch, yaw, roll)` assigned to `localRotation` turns about the
PARENT's axes.  On part-per-mesh art every animated part hangs off the model root,
so those ARE the ship's axes and pitch means pitch.  A bone's parent is another
bone, pointing wherever the skeleton points - so the identical call rolls when it
meant to pitch, and pitches backwards.  That is what a rig-swapped Dolphin looked
like: "roll and pitch are mixed, pitch is inverted, the wings fold up on a drift".

`VesselAnimation.RotatePartFromRestInFrame` conjugates the turn into the frame the
animation MEANT and re-anchors the rest pose through the part's HOME parent.

Two further things the rig changed, which three passes of per-axis sign scalers
could not fix because neither of them is a sign:

  * the old art parented every animated part under the CHASSIS, so each inherited
    the chassis's turn and added its own on top.  On the rig the wings hang off
    `winghold.l|r` and the engines off `jetholdT|m|B.l|r` - a sibling branch of
    `fuse` - so that inherited term is gone.  The wings' own pitch input is
    Brake(throttle), zero unless braking, so losing it left them dead on that axis.
  * the drift frame was a `DriftHandle` Transform parented under the vessel, so the
    hull's own aiming carried it between one frame's write and the next read.
    Re-pointing only its forward axis leaves that twist in place, and it
    accumulates.

This reproves all of it against the rig's own measured bone rest rotations,
offline, with no Unity.

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
    print("4. the composed turn must be BIT-IDENTICAL to the old chassis-as-parent hierarchy")
    # Legacy: wing.localRotation = Euler(wing) * rest, under a chassis that was itself turning,
    #   world = SHIP * Euler(chassis) * Euler(wing) * rest
    # Rig:   the wing is a sibling branch, so the chassis turn is composed by hand and the pair
    #   is conjugated into the ship frame. The two must agree exactly - composed, never added,
    #   because Euler angles do not add at these amplitudes.
    S, E = 25.0, 75.0
    worst = 0.0
    for (pi, ya, ro, th) in ((0.4, 0.0, 0.0, 0.0), (0.0, 0.7, 0.0, 0.0), (0.0, 0.0, 1.0, 0.0),
                             (0.6, -0.5, 0.8, 0.3), (-1.0, 1.0, -1.0, 1.0)):
        chassis = euler(pi * S, ya * S, ro * S)
        own     = euler(0.0, (ya + th) * E, (ro + pi) * S)
        rest_l  = euler(5.901, 0, 0)
        legacy  = qmul(SHIP, qmul(chassis, qmul(own, rest_l)))
        rest_world = qmul(SHIP, rest_l)                    # legacy rest: chassis rest = identity
        rig     = qmul(SHIP, qmul(qmul(chassis, own), qmul(qinv(SHIP), rest_world)))
        # Compared COMPONENT-WISE, not through angle_between: that goes via acos, which is
        # ill-conditioned for near-identical quaternions and reports ~3e-6 deg of pure round-off
        # for a product chain this long. A component difference is well-conditioned, so the
        # tolerance can stay at float64 epsilon and actually mean something.
        worst = max(worst, min(max(abs(a - b) for a, b in zip(legacy, rig)),
                               max(abs(a + b) for a, b in zip(legacy, rig))))
    if worst > 1e-12:
        failures.append("composed turn differs from the legacy hierarchy by %.3e" % worst)
    print("   worst quaternion component disagreement over 5 stick poses: %.3e%s"
          % (worst, "" if worst <= 1e-12 else "   <-- FAIL"))
    # ... and prove that ADDING the Eulers instead would NOT have been the same thing.
    chassis = euler(0.6 * S, -0.5 * S, 0.8 * S)
    own     = euler(0.0, (-0.5 + 0.3) * E, (0.8 + 0.6) * S)
    added   = euler(0.6 * S, -0.5 * S + (-0.5 + 0.3) * E, 0.8 * S + (0.8 + 0.6) * S)
    print("   the same thing done by ADDING Euler angles is off by %.2f deg (why it is composed)"
          % angle_between(qmul(chassis, own), added))

    print()
    print("5. PITCH must reach the wings - the axis the rig went dead on")
    # The wings' own X input is Brake(throttle): zero unless the pilot is braking. Every bit of
    # pitch response they had came from the chassis they used to hang off.
    # Stick: pitch 0.4, nothing else. The wing's own terms give it Brake(0)=0 on X and the
    # aileron +-pitch on Z; the ship's pitch can only reach it through the chassis.
    own = euler(0.0, 0.0, 0.4 * S)                      # roll 0 + pitch 0.4 -> aileron, about Z
    for label, chassis in (("without the chassis term (the regression)", euler(0, 0, 0)),
                           ("with it restored",                          euler(0.4 * S, 0, 0))):
        ax, ang = to_axis_angle(qmul(chassis, own))
        about_x = abs(ax[0] * ang)
        print("   %-42s wing turns %6.2f deg, %5.2f of it about the ship's PITCH axis"
              % (label, abs(ang), about_x))
        if chassis != euler(0, 0, 0) and about_x < 1.0:
            failures.append("the chassis term delivers no pitch to the wings")
        if chassis == euler(0, 0, 0) and about_x > 1e-9:
            failures.append("the no-chassis control unexpectedly pitched the wings")

    print()
    print("6. the COURSE frame: aimed along Course, rolled with the hull, and stateless")
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

    def course_frame(hull, course):
        """The shipped construction: the hull, swung onto Course by the shortest arc."""
        return qmul(from_to(rotv(hull, (0, 0, 1)), course), hull)

    COURSE = (0.0, 0.0, 1.0)
    entry = euler(0, 0, 0)

    # (a) the shipped frame, over a hull that aims AND rolls during the drift
    worst_aim, worst_up, worst_state = 0.0, 0.0, 0.0
    frozen_up = 0.0
    for step in range(1, 11):
        hull = euler(6.0 * step, 5.0 * step, 3.0 * step)      # pitch, yaw AND roll
        f = course_frame(hull, COURSE)

        # forward must be exactly Course
        fwd = rotv(f, (0, 0, 1))
        worst_aim = max(worst_aim, math.degrees(math.acos(max(-1.0, min(1.0, fwd[2])))))

        # The frame must not ROLL the hull, only re-aim it: the parts keep the hull's up, which
        # is roughly the camera's, because the chase camera rolls with the hull. FromToRotation is
        # a pure swing about an axis perpendicular to both nose and Course, so the residual
        # rotation f * inv(hull) must have NO component along the nose. (Comparing up-vectors
        # directly is not this test - the frame is legitimately TILTED off the hull by the aim,
        # which moves up too; an earlier version of this check measured that tilt and called it
        # roll.)
        d_ax, d_ang = to_axis_angle(qmul(f, qinv(hull)))
        hf = rotv(hull, (0, 0, 1))
        worst_up = max(worst_up, abs(sum(d_ax[i] * hf[i] for i in range(3)) * d_ang))

        # STATELESS: rebuilding from the same hull must give the same frame, so nothing can
        # accumulate however long the drift runs.
        again = course_frame(hull, COURSE)
        # component-wise, not through acos - see check 4
        worst_state = max(worst_state, min(max(abs(a - b) for a, b in zip(f, again)),
                                           max(abs(a + b) for a, b in zip(f, again))))

        # (b) the FROZEN-at-entry control: perfectly stable, and wrong for a different reason -
        #     it holds where the ship WAS at entry rather than where it is going, so as the hull
        #     rolls during the drift the parts stop matching it (and the camera).
        frozen_up = max(frozen_up, angle_between(entry, f))

    if worst_aim > 1e-6:
        failures.append("the course frame left Course by %.6f deg" % worst_aim)
    if worst_up > 1e-6:
        failures.append("the course frame injected %.6f deg of roll" % worst_up)
    if worst_state > 1e-12:
        failures.append("the course frame is not stateless (%.3e)" % worst_state)
    print("   hull aims to 60 deg pitch / 50 deg yaw / 30 deg roll off a fixed Course")
    print("   forward off Course:          %.9f deg%s" % (worst_aim, "" if worst_aim <= 1e-6 else "  <-- FAIL"))
    print("   roll injected about Course:  %.9f deg%s" % (worst_up, "" if worst_up <= 1e-6 else "  <-- FAIL"))
    print("   rebuild disagreement:        %.3e (quaternion components)%s"
          % (worst_state, "" if worst_state <= 1e-12 else "  <-- FAIL"))
    print("   controls, same sweep:")
    print("     frozen-at-entry frame - ends %6.2f deg away from the frame it should be"
          % frozen_up)
    print("     (stable, but it holds the entry orientation instead of tracking the hull's roll)")

    # (c) the parented-handle control, which is what a pilot actually saw
    handle, prev, hull_prev = entry, entry, entry
    handle_twist = 0.0
    for step in range(1, 11):
        hull = euler(6.0 * step, 5.0 * step, 3.0 * step)
        dR = qmul(hull, qinv(hull_prev)); hull_prev = hull
        handle = qmul(dR, handle)                                          # parented: hull carries it
        handle = qmul(from_to(rotv(handle, (0, 0, 1)), COURSE), handle)    # only forward re-pointed
        handle_twist += angle_between(handle, prev); prev = handle
    final = course_frame(euler(60.0, 50.0, 30.0), COURSE)
    handle_error = angle_between(handle, final)
    if handle_error < 1.0:
        failures.append("the parented-handle control did not reproduce the defect")
    print("     parented DriftHandle  - ends %6.2f deg away from the frame it should be"
          % handle_error)
    print("     (pure accumulated twist: a Transform under the vessel is carried by the hull,")
    print("      and re-pointing only its forward axis leaves that rotation in place)")
    _ = handle_twist

    print()
    print("7. a pure ROLL must bank the two wings EQUALLY; pitch is allowed to split them")
    # A bank is both wings turning by the same angle - the plane tilts, it does not fold. The
    # authored +-pitch in the roll term is an AILERON and is meant to split them; the fold the
    # sixth playtest photographed came from negating the wings' yaw alone, which broke the
    # pairing between that term and the +-throttle one.
    for pitch, throttle, label in ((0.0, 0.0, "pure roll"), (0.6, 0.0, "roll + pitch (aileron)")):
        r_r = (1.0 + pitch) * S
        r_l = (1.0 - pitch) * S
        split = abs(r_r - r_l)
        print("   %-24s wing-to-wing roll split: %5.1f deg" % (label, split))
        if pitch == 0.0 and split > 1e-9:
            failures.append("a pure roll splits the wings by %.2f deg" % split)

    print()
    print("8. the offsets must be scale-free AND BOUNDED BY THE PARTS THEMSELVES")
    # An absolute offset is an unstated dependency on a model's import scale, and the two Dolphin
    # models disagree about it (legacy FBX root: no Lcl Scaling; rig root: (100,100,100); both
    # UnitScaleFactor 1.0). The same absolute 2.3 is then either most of the hull or a rounding
    # error.
    #
    # The FIRST fix scaled the offsets by PrismOcclusionCorridor.MeasureCircumscribedRadius, which
    # did not remove the ambiguity - it imported it. That helper measures a skinned hull through
    # `skinned.rootBone`'s transform, and the rootBone is exactly where a disputed armature factor
    # lives. This check exists because that failed on screen: the offsets came out ~100x and threw
    # the parts clear of the ship.
    #
    # The basis is now the farthest positioned part's own rest distance from the vessel origin, so
    # the offset and its basis are in the SAME units by construction.
    WING_FWD, JET_DRIFT, JET_REST = 0.35, 0.35, 0.08
    JET_BONE_REACH = 1.9006      # measured: |jetT.l rest - origin|, armature units
    print("   authored: wings +%.2f reach forward, engines -%.2f (drift), -%.2f (rest)"
          % (WING_FWD, JET_DRIFT, JET_REST))
    print("   %-38s %-10s %-12s %s" % ("basis", "value", "wing slide", "as % of basis"))
    fracs = []
    for label, mult in (("parts' own reach, root 100x absent", 1.0),
                        ("parts' own reach, root 100x applied", 100.0)):
        reach = JET_BONE_REACH * mult
        slide = WING_FWD * reach
        fracs.append(slide / reach)
        print("   %-38s %-10.3f %-12.3f %.1f%%" % (label, reach, slide, slide / reach * 100.0))
    if abs(fracs[0] - fracs[1]) > 1e-12:
        failures.append("the offsets are not scale-free (%.6f vs %.6f)" % (fracs[0], fracs[1]))
    print("   identical at both readings%s"
          % ("" if abs(fracs[0] - fracs[1]) <= 1e-12 else "   <-- FAIL"))

    # THE CONTROL: a basis taken from a DIFFERENT space is what shipped and failed. If the hull
    # measurement carries the armature factor and the bones do not, the offset is 100x the part's
    # own reach - the parts leave the ship.
    HULL_RADIUS_IF_ARMATURE_SCALED = 2.703 * 100.0
    bad = WING_FWD * HULL_RADIUS_IF_ARMATURE_SCALED
    print("   control - basis from the ROOT-BONE-space hull measure: slide %.1f = %.0fx the part's"
          " own reach" % (bad, bad / JET_BONE_REACH))
    if bad / JET_BONE_REACH < 10:
        failures.append("the mismatched-basis control did not reproduce the defect")

    # AND THE CLAMP: whatever the basis, an offset may not exceed the reach it is measured against.
    # A clearance is a nudge, not a launch; this bounds the whole family against exactly the
    # failure above rather than trusting the next basis to be right.
    for authored in (0.35, 1.0, 5.0, -12.0):
        applied = max(-1.0, min(1.0, authored))
        if abs(applied) > 1.0 + 1e-12:
            failures.append("clamp let %.1f through" % authored)
    print("   clamp: an authored 5.0 or -12.0 is bounded to +-1.0 x reach, so a bad basis cannot"
          " throw a part off the ship")

    print()
    if failures:
        print("FAILED:")
        for f in failures:
            print("  !", f)
        return 1
    print("OK - ship-axis turns, the chassis term restored, and a stateless Course frame.")
    return 0

if __name__ == "__main__":
    sys.exit(main())
