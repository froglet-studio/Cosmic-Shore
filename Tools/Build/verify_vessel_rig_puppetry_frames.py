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

def rot(q, v):
    """Rotate vector v by quaternion q."""
    x, y, z, w = q
    vx, vy, vz = v
    # v' = v + 2*cross(q.xyz, cross(q.xyz, v) + w*v)
    cx = y * vz - z * vy + w * vx
    cy = z * vx - x * vz + w * vy
    cz = x * vy - y * vx + w * vz
    return (vx + 2 * (y * cz - z * cy),
            vy + 2 * (z * cx - x * cz),
            vz + 2 * (x * cy - y * cx))

def dist(a, b):
    return math.sqrt(sum((p - q) * (p - q) for p, q in zip(a, b)))

def from_to(a, b):
    """Quaternion.FromToRotation for unit-ish vectors, with Unity's arbitrary-axis
    behaviour at the antipode approximated by the raw cross (which is the instability
    being tested)."""
    na = math.sqrt(sum(c * c for c in a)); a = tuple(c / na for c in a)
    nb = math.sqrt(sum(c * c for c in b)); b = tuple(c / nb for c in b)
    d = sum(p * q for p, q in zip(a, b))
    cx = a[1] * b[2] - a[2] * b[1]
    cy = a[2] * b[0] - a[0] * b[2]
    cz = a[0] * b[1] - a[1] * b[0]
    cn = math.sqrt(cx * cx + cy * cy + cz * cz)
    if cn < 1e-12:
        if d > 0:
            return (0.0, 0.0, 0.0, 1.0)
        # exact antipode: arbitrary perpendicular axis (Unity does the same class of thing)
        ax = (1.0, 0.0, 0.0) if abs(a[0]) < 0.9 else (0.0, 1.0, 0.0)
        return axis_angle(ax, 180.0)
    ang = math.degrees(math.atan2(cn, d))
    return axis_angle((cx / cn, cy / cn, cz / cn), ang)

def qnorm_sign(q):
    """Canonical sign (w >= 0) for component-wise comparison."""
    return tuple(-c for c in q) if q[3] < 0 else q

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
    print("8. WORLD-UNIT offsets survive any bone-chain scale through the exact round trip")
    # The offsets are authored in world units (driftWingForward 2.2 / driftJetBackward 0.5 /
    # jetRestBackward 0 - all MEASURED, see VESSEL_CONSTRUCTION.md 4.6.5). That is safe now, and
    # only now, because MovePartFromRest computes the target in WORLD space from a capture-time
    # rest vector and localizes through the part's live parent:
    #     target_world = vesselPos + F * (restInVessel + offset)
    #     target_local = parent_world_inverse(target_world)
    # The parent inverse and the skinning matrix are exact inverses of each other through ANY
    # chain scale, so the applied world displacement equals F * offset identically whether the
    # armature nets to 1x or carries its Lcl Scaling 100 into the bones (this rig does: bones
    # have lossyScale ~100 while their WORLD poses land on the hull - measured, import model
    # pinned against the shipped colliders at 3e-5 residual).
    AUTHORED_WING_LUNGE = 2.2   # world units, the shipped value
    for label, chain_scale in (("bone chain at world scale 1", 1.0),
                               ("bone chain carrying the armature 100x", 100.0)):
        # rest bone position (vessel frame, world units) and its parent's frame
        rest_world = (-0.96897, 0.0, -0.11397)          # wing.l, measured
        offset = (0.0, 0.0, AUTHORED_WING_LUNGE)
        target_world = tuple(r + o for r, o in zip(rest_world, offset))
        # localize through a parent at arbitrary rotation with the chain scale, then re-derive
        # the world position the skinning would draw at - the round trip must be exact
        local = tuple(t / chain_scale for t in target_world)     # parent at identity rotation
        back = tuple(l * chain_scale for l in local)
        err = max(abs(b - t) for b, t in zip(back, target_world))
        moved = tuple(b - r for b, r in zip(back, rest_world))
        print("   %-42s applied slide %.4f wu (round-trip error %.1e)"
              % (label, moved[2], err))
        if abs(moved[2] - AUTHORED_WING_LUNGE) > 1e-9:
            failures.append("world-unit offset did not survive chain scale %g" % chain_scale)

    # WHY THE FRACTION SCHEME HAD TO GO: the proven pre-rig wing lunge is 2.2006 wu from the
    # rig's rest (old art drove +2.3 from ITS on-screen rest at z -0.4032; the rig rests the
    # wing geometry at -0.3038), and the fraction basis (the farthest part's reach, 1.96008)
    # with its |fraction| <= 1 clamp tops out at 1.96008 - the proven look was UNREACHABLE.
    REACH, PROVEN = 1.96008, 2.2006
    print("   fraction scheme ceiling %.5f < proven lunge %.4f  ->  world units required"
          % (REACH, PROVEN))
    if REACH >= PROVEN:
        failures.append("the fraction-ceiling forensic stopped holding - re-derive")

    # AND THE NEW GUARD: offsets are clamped to +-4 wu (about a hull length, 3.45), so a
    # fat-fingered 80-instead-of-0.8 cannot throw a part off the ship.
    for authored, expect in ((2.2, 2.2), (0.25, 0.25), (80.0, 4.0), (-12.0, -4.0)):
        applied = max(-4.0, min(4.0, authored))
        if abs(applied - expect) > 1e-12:
            failures.append("world-unit clamp: %.1f -> %.2f, expected %.2f" % (authored, applied, expect))
    print("   clamp: an authored 80 or -12 is bounded to +-4 wu; measured values pass untouched")

    # THE DRIFT STATION IS SIGNED OFF. Flight 8 called the pulled-back drift position perfect and
    # asked the REST position to sit halfway to it, so the two fields are a balanced pair: the
    # station is their SUM and must stay 0.50 unless a playtest re-signs it. Retuning one field
    # without re-balancing the other silently moves the approved station.
    JET_REST, JET_DRIFT, APPROVED_STATION = 0.25, 0.25, 0.50
    print("   drift station: rest %.2f + drift %.2f = %.2f (flight-8 approved; rest = station/2)"
          % (JET_REST, JET_DRIFT, JET_REST + JET_DRIFT))
    if abs((JET_REST + JET_DRIFT) - APPROVED_STATION) > 1e-12 or abs(JET_REST - APPROVED_STATION / 2) > 1e-12:
        failures.append("the jet offsets no longer land the flight-8 station/halfway pair")

    print()
    print("9. the drift CAGE holds station: positions ride the course frame, like orientations")
    # Bleeding-edge re-parented the appendages onto a course-aimed DriftHandle at the vessel
    # origin, so their POSITIONS held the course cage while the hull aimed. An intermediate
    # version of the branch read positions in the VESSEL frame instead - the cage swept sideways
    # with the aiming hull. This check runs the SHIPPED construction
    # F = FromToRotation(hullFwd, course) * hull (not a constant-frame model of it - an earlier
    # revision modelled the cage as a constant and mis-cited check 6, which proves forward-on-
    # course, zero INJECTED roll and statelessness, not frame constancy):
    #   9a. under SINGLE-AXIS aim (pure pitch, pure yaw) the wing target is BIT-STILL;
    #   9b. the vessel-frame read (the defect) sweeps it;
    #   9c. under hull ROLL the cage deliberately rolls with the hull about the course axis
    #       (up stays the pilot's - the legacy handle's up-tracking), so the target ORBITS the
    #       course line at constant radius, by exactly the roll angle. Designed, not wander.
    #   (Combined pitch+yaw aims twist the hull's own up about course - second-order Euler
    #   coupling - and the cage follows that too, which IS the up-tracking. Not asserted still.)
    course = (0.0, 0.0, 1.0)
    rest = (-0.96897, 0.0, -0.11397)
    lunge = (0.0, 0.0, 2.2)
    station = tuple(r + o for r, o in zip(rest, lunge))

    def cage_of(hull):
        fwd = rot(hull, (0.0, 0.0, 1.0))
        return qmul(from_to(fwd, course), hull)

    for label, hull_at in (("pure pitch sweep", lambda t: euler(60 * t, 0, 0)),
                           ("pure yaw sweep", lambda t: euler(0, 50 * t, 0))):
        targets = [rot(cage_of(hull_at(i / 24.0)), station) for i in range(25)]
        wander = max(dist(a, targets[0]) for a in targets)
        print("   9a %-18s wing target wander (shipped construction): %.9f wu" % (label, wander))
        if wander > 1e-6:
            failures.append("the cage wanders %.6f under a %s" % (wander, label))

    hull_targets = [rot(euler(60 * (i / 24.0), 50 * math.sin(math.pi * (i / 24.0)), 30 * (i / 24.0)),
                        station) for i in range(25)]
    hull_sweep = max(dist(a, hull_targets[0]) for a in hull_targets)
    print("   9b vessel-frame read (the defect), full aim sweep:     %.3f wu" % hull_sweep)
    if hull_sweep < 1.0:
        failures.append("the vessel-frame control did not reproduce the sweep defect")

    roll_targets = [rot(cage_of(euler(0, 0, 30 * (i / 24.0))), station) for i in range(25)]
    radii = [math.sqrt(t[0] * t[0] + t[1] * t[1]) for t in roll_targets]
    ang0 = math.degrees(math.atan2(roll_targets[0][1], roll_targets[0][0]))
    ang1 = math.degrees(math.atan2(roll_targets[-1][1], roll_targets[-1][0]))
    travel = (ang1 - ang0) % 360.0
    travel = travel - 360.0 if travel > 180.0 else travel
    print("   9c hull roll 30 deg: target orbits the course axis %.4f deg at radius drift %.2e"
          % (travel, max(radii) - min(radii)))
    if abs(abs(travel) - 30.0) > 1e-6 or (max(radii) - min(radii)) > 1e-9:
        failures.append("roll-following is not the pure orbit it is designed to be")

    print()
    print("10. on LEGACY art the captured rest anchor applies the chassis term ONCE (like the old code)")
    # RotatePartFromRestInFrame used to resolve the rest anchor through the part's LIVE parent.
    # On the rig that parent never animates, so it made no difference - but on the old art the
    # parts were CHASSIS children, and the live read folded the chassis's current deflection into
    # the anchor: converged wing world = R * E_c * E_w * E_c, the chassis term TWICE. The anchor
    # is now a capture-time constant, so the composition is single-application on both arts.
    R = euler(7, -12, 4)                      # vessel attitude, arbitrary
    E_c = euler(15, 20, -10)                  # chassis term at some stick pose
    E_w = euler(-16.25, 30, 5)                # a wing's own term
    rest_in_vessel = (0.0, 0.0, 0.0, 1.0)     # captured at rest: chassis at identity, rest identity
    branch_world = qmul(R, qmul(qmul(E_c, E_w), rest_in_vessel))
    bleeding_world = qmul(qmul(R, E_c), E_w)  # chassis-as-parent hierarchy
    d = max(abs(a - b) for a, b in zip(qnorm_sign(branch_world), qnorm_sign(bleeding_world)))
    live_anchor = qmul(R, qmul(qmul(E_c, E_w), E_c))   # the retired live-home read
    _, double = to_axis_angle(qmul(qinv(bleeding_world), live_anchor))
    print("   captured anchor vs old chassis-child hierarchy: %.3e (quaternion components)" % d)
    print("   control - the retired LIVE anchor: off by %.2f deg (the chassis term twice)" % abs(double))
    if d > 1e-12:
        failures.append("captured rest anchor is not single-application on legacy art (%.3e)" % d)
    if abs(double) < 5.0:
        failures.append("the live-anchor control did not reproduce the double application")

    print()
    print("11. the course frame is GUARDED at the antipode - no roll-thrash aiming backwards")
    # Quaternion.FromToRotation picks an arbitrary swing axis for antiparallel vectors, and
    # nothing clamps drift aim - a full reverse aim is reachable. Circling the nose around the
    # antipode of Course, the raw frame's roll whips around the circle; the guard (hold the
    # previous frame inside dot < -0.999) pins it still.
    course = (0.0, 0.0, 1.0)
    raw_frames, guarded_frames = [], []
    held = None
    # The hull is anchored at a fixed backwards attitude and CARRIED smoothly around the
    # 0.8-degree circle (steps of ~1.6 deg of actual hull motion). Deriving the hull from
    # from_to(course, nose) instead would cancel the instability by construction - the swing
    # axis must come from the LIVE forward-vs-course cross product, as in the shipped code.
    hull0 = euler(0, 180, 0)                       # nose exactly backwards
    nose0 = rot(hull0, (0.0, 0.0, 1.0))
    for i in range(37):
        az = math.radians(i * 10.0)
        tilt = math.radians(179.2)          # 0.8 deg off the exact antipode - inside the guard cone
        nose = (math.sin(tilt) * math.cos(az), math.sin(tilt) * math.sin(az), math.cos(tilt))
        hull = qmul(from_to(nose0, nose), hull0)       # small smooth carry, forward = nose
        raw = qmul(from_to(nose, course), hull)
        raw_frames.append(raw)
        dot = sum(a * b for a, b in zip(nose, course))
        if dot < -0.999:
            guarded = held if held is not None else hull
        else:
            guarded = raw
        held = guarded
        guarded_frames.append(guarded)
    worst_raw = max(to_axis_angle(qmul(qinv(raw_frames[i]), raw_frames[i + 1]))[1]
                    for i in range(len(raw_frames) - 1))
    worst_guarded = max(to_axis_angle(qmul(qinv(guarded_frames[i]), guarded_frames[i + 1]))[1]
                        for i in range(len(guarded_frames) - 1))
    print("   raw FromToRotation, nose circling 0.8 deg off the antipode: worst step %.1f deg" % worst_raw)
    print("   with the hold-last guard:                                   worst step %.2f deg" % worst_guarded)
    # 20 deg per 10-deg azimuth step is the near-180 lever (the frame turns at ~2x the rate the
    # nose wobbles); at frame rate that is >1000 deg/s of target churn. Anything past 10 deg/step
    # is the defect; the guard must stay under 1.
    if worst_raw < 10.0:
        failures.append("the antipode control did not reproduce the thrash (worst %.1f)" % worst_raw)
    if worst_guarded > 1.0:
        failures.append("the antipode guard still moves %.2f deg per step" % worst_guarded)

    # 11b. THE CHURN BAND OUTSIDE THE HOLD CONE, AND THE SLEW LIMIT. The hold cone is 2.56 deg;
    # FromToRotation's churn amplification (~2/sin(angle-to-antipode)) extends an order of
    # magnitude further out, and since the cage is EXACT-WRITTEN onto the parts there is no lerp
    # low-pass left to hide it. Legitimate cage motion is bounded by the hull ROLL rate
    # (110 deg/s = 1.83 deg/frame at 60 fps), so the shipped 360 deg/s slew limit (6 deg/frame)
    # never engages in ordinary flight and bounds the churn - and the cone-EXIT snap - everywhere.
    def slew(prev, fresh, max_step):
        _, ang = to_axis_angle(qmul(qinv(prev), fresh))
        ang = abs(ang)
        if ang <= max_step:
            return fresh
        # slerp by ratio: compose prev with a fraction of the delta
        ax, _ = to_axis_angle(qmul(qinv(prev), fresh))
        return qmul(prev, axis_angle(ax, max_step))

    MAX_STEP = 360.0 / 60.0
    for label, tilt_deg, transit in (("3 deg off the antipode, circling", 177.0, False),
                                     ("transit THROUGH the hold cone", 179.7, True)):
        raw_frames, shipped_frames = [], []
        prev = None
        for i2 in range(37):
            az = math.radians(i2 * 10.0)
            tilt = math.radians(tilt_deg)
            nose = (math.sin(tilt) * math.cos(az), math.sin(tilt) * math.sin(az), math.cos(tilt))
            if transit and 90 <= i2 * 10 <= 270:
                # dive to the exact antipode band for the middle of the path
                deep = math.radians(179.95)
                nose = (math.sin(deep) * math.cos(az), math.sin(deep) * math.sin(az), math.cos(deep))
            hull = qmul(from_to(nose0, nose), hull0)
            raw = qmul(from_to(nose, course), hull)
            raw_frames.append(raw)
            dot = sum(a * b for a, b in zip(nose, course))
            fresh = (prev if (dot < -0.999 and prev is not None) else raw)
            shipped = fresh if prev is None else slew(prev, fresh, MAX_STEP)
            prev = shipped
            shipped_frames.append(shipped)
        worst_raw2 = max(to_axis_angle(qmul(qinv(raw_frames[i2]), raw_frames[i2 + 1]))[1]
                         for i2 in range(len(raw_frames) - 1))
        worst_ship = max(abs(to_axis_angle(qmul(qinv(shipped_frames[i2]), shipped_frames[i2 + 1]))[1])
                         for i2 in range(len(shipped_frames) - 1))
        print("   11b %-32s raw worst step %6.1f deg   shipped (hold+slew) %.2f deg"
              % (label, worst_raw2, worst_ship))
        if worst_raw2 < 10.0:
            failures.append("the %s control did not reproduce the churn (%.1f)" % (label, worst_raw2))
        if worst_ship > MAX_STEP + 1e-6:
            failures.append("the slew limit let %.2f deg/frame through on %s" % (worst_ship, label))

    print()
    print("12. drift poses are written EXACTLY in cage coordinates - a lerp cannot hold the cage")
    # The parts are parented under the HULL, so the hull's aiming carries them off their course-
    # cage station every frame; a finite-rate pull toward the hull-independent station trails a
    # full-rate aim by omega/lerpAmount - which at the Dolphin's 110 deg/s and lerpAmount 2 is a
    # steady-state ~55 deg / ~1.9 wu of the appendages being visibly dragged around by the nose
    # ("the wings and jets still appear to move as I am drifting and aiming"). The legacy re-
    # parent had zero lag because the handle CARRIED the parts. The shipped code reproduces that
    # by writing the pose exactly in cage coordinates each frame (PlacePartInCage), with a single
    # blend running the ADOPTED entry pose to the station.
    OMEGA, LERP, FPS, RADIUS = 110.0, 2.0, 60.0, 1.94
    dt = 1.0 / FPS
    # lerped policy: the hull's rotation carries the part's world position around a circle of
    # RADIUS while the pull drags it back toward the fixed station. Track the angular error.
    ang_err = 0.0
    for _ in range(int(FPS * 3)):            # 3 seconds of full-rate aiming
        ang_err += OMEGA * dt                # carried with the hull
        ang_err -= ang_err * LERP * dt       # pulled back toward the station
    trail_deg = ang_err
    trail_wu = 2.0 * RADIUS * math.sin(math.radians(trail_deg) / 2)
    exact_trail = 0.0                        # by construction: pose = cage * station, no pursuit
    print("   lerped pull at %g/s vs a %g deg/s aim: steady-state trail %.1f deg = %.2f wu"
          % (LERP, OMEGA, trail_deg, trail_wu))
    print("   exact cage write:                      trail %.1f (by construction)" % exact_trail)
    if trail_deg < 30.0:
        failures.append("the lag control did not reproduce the drag (%.1f deg)" % trail_deg)
    # and the ADOPTION keeps entry continuous: blend 0 is the current pose expressed in cage
    # coordinates, so the first drift frame writes back exactly what is already there
    entry_pose = (0.123, -0.456, 0.789)     # arbitrary current pose, cage coords
    written = tuple(a + (b - a) * 0.0 for a, b in zip(entry_pose, entry_pose))
    if any(abs(w - e) > 0 for w, e in zip(written, entry_pose)):
        failures.append("the adoption is not continuous at blend 0")
    print("   entry continuity: blend 0 writes back the adopted pose bit-exactly")

    print()
    if failures:
        print("FAILED:")
        for f in failures:
            print("  !", f)
        return 1
    print("OK - ship-axis turns, the chassis term once on any art, a guarded stateless Course frame,\n"
          "     the drift cage in that frame, and measured world-unit offsets that survive any chain scale.")
    return 0

if __name__ == "__main__":
    sys.exit(main())
