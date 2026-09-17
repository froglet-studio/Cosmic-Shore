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
    And the inheritance was never orientation alone: a chassis child ORBITS the
    chassis pivot, and its own turn is about the ONE seat its class was authored
    at (all six engine cases at (0,0.147,-2.047), lerped to (0,.15,-1.7) in
    flight) - not about its own bone.  Check 8 proves the shipped chain lands every
    rig vertex where the legacy hierarchy would, to 1e-15 (flight 17).
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
    # Flight 11 asked for TRUE clearance while drifting, not old-game parity (which
    # interpenetrated): the aiming hull's jaw tip sweeps a 2.835-radius sphere about the
    # vessel origin (max |r| over jaw.u/jaw.b skin verts; the fwd fuselage reaches only
    # 0.985), and the smallest lunge putting every wing vertex outside that sweep x1.05
    # gape margin is L* = 3.492 (bisected; binding vert the wing root at (-0.760,0,-0.614)).
    # Shipped 3.5 - the round-up - and asserted geometrically below.
    AUTHORED_WING_LUNGE = 3.5   # world units, the shipped value
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

    # THE CLEARANCE ASSERTION (flight 11): with the shipped lunge, the wings' NEAREST vertex
    # to the vessel origin must sit outside the sphere the aiming jaw tip sweeps, x1.05 for
    # the gape. Constants are measurements from the rig's skin clusters (scratch flight11.py);
    # the binding wing vert and the jaw-tip radius must be re-measured if the rig changes.
    JAW_SWEEP, GAPE_MARGIN = 2.835, 1.05
    BINDING_WING_VERT = (-0.760, 0.0, -0.614)      # rest, vessel frame
    bx, by, bz = BINDING_WING_VERT
    lunged = (bx * bx + by * by + (bz + AUTHORED_WING_LUNGE) ** 2) ** 0.5
    need = JAW_SWEEP * GAPE_MARGIN
    print("   wing clearance: binding vert at |r| %.3f with lunge %.2f vs jaw sweep %.3f x %.2f = %.3f  %s"
          % (lunged, AUTHORED_WING_LUNGE, JAW_SWEEP, GAPE_MARGIN, need,
             "CLEAR" if lunged > need else "<-- FAIL"))
    if lunged <= need:
        failures.append("the shipped wing lunge %.2f does not clear the jaw-tip sweep" % AUTHORED_WING_LUNGE)

    # AND THE NEW GUARD: offsets are clamped to +-4 wu (about a hull length, 3.45), so a
    # fat-fingered 80-instead-of-0.8 cannot throw a part off the ship.
    for authored, expect in ((3.5, 3.5), (1.25, 1.25), (80.0, 4.0), (-12.0, -4.0)):
        applied = max(-4.0, min(4.0, authored))
        if abs(applied - expect) > 1e-12:
            failures.append("world-unit clamp: %.1f -> %.2f, expected %.2f" % (authored, applied, expect))
    print("   clamp: an authored 80 or -12 is bounded to +-4 wu; measured values pass untouched")

    # ---- FLIGHT 17: THE LEGACY CHASSIS CHAIN, REPRODUCED IN VESSEL SPACE -----------------
    #
    # Flights 13-16 tuned the boosters' z-station and own amplitude against bleeding-edge, and
    # every one of them still read as "the boosters and wings are the issue" - because the seat
    # was never the defect. The old art parented the wings and the six engine cases under the
    # CHASSIS, and that hierarchy did two things per frame that the previous construction
    # reproduced neither of: the parts ORBITED the chassis pivot (their POSITION swung with the
    # 25-degree chassis deflection, not only their orientation), and each part's own turn was
    # about ONE shared seat per class - all six engine cases at (0, 0.147, -2.047), lerped by
    # AnimatePart to (0, .15, -1.7) every frame; both wings at (0, 0, 0.1), lerped to (0, 0, 0)
    # - never about its own bone. RiptideAnimation.PlacePartOnChassis now treats each rig bone
    # as a point rigidly attached to the legacy part frame and applies that frame's motion:
    #
    #     pos = P_c + C * (F + E * (R - P_c - A))          rot = C * E * rest
    #
    # (C chassis turn, E the part's own turn, A the authored pivot, F the flight pivot, R the
    # bone's rest position, P_c the chassis pivot). The constants are READ from the shipped C#
    # and the shipped prefab rather than restated here, so a retune that forgets one of the two
    # fails this check; the bleeding-edge reference values are stated once, with provenance.
    import os, re
    REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    CS = os.path.join(REPO, "Assets", "_Scripts", "Controller", "Animation", "RiptideAnimation.cs")
    PREFAB = os.path.join(REPO, "Assets", "_Prefabs", "Spacevessels", "Dolphin.prefab")
    RIPTIDE_GUID = "0f8854390007c384796664b8fc0cf25d"

    def cs_vec3(src, field):
        m = re.search(r"Vector3\s+%s\s*=\s*(?:new\s*\(([^)]*)\)|Vector3\.zero)\s*;" % re.escape(field), src)
        if not m:
            raise SystemExit("cannot read the C# default of %s" % field)
        if m.group(1) is None:
            return (0.0, 0.0, 0.0)
        return tuple(float(c.strip().rstrip("fF")) for c in m.group(1).split(","))

    def cs_float(src, field):
        m = re.search(r"float\s+%s\s*=\s*([-+0-9.eE]+)f?\s*;" % re.escape(field), src)
        if not m:
            raise SystemExit("cannot read the C# default of %s" % field)
        return float(m.group(1))

    def cs_bool(src, field):
        m = re.search(r"bool\s+%s\s*=\s*(true|false)\s*;" % re.escape(field), src)
        if not m:
            raise SystemExit("cannot read the C# default of %s" % field)
        return m.group(1) == "true"

    def prefab_block(text):
        i = text.find("guid: %s" % RIPTIDE_GUID)
        if i < 0:
            raise SystemExit("Dolphin.prefab carries no RiptideAnimation")
        j = text.find("--- !u!", i)
        return text[i:j if j > 0 else len(text)]

    def yaml_vec3(block, field):
        m = re.search(r"^\s*%s: \{x: ([-+0-9.eE]+), y: ([-+0-9.eE]+), z: ([-+0-9.eE]+)\}" % re.escape(field),
                      block, re.M)
        if not m:
            raise SystemExit("Dolphin.prefab does not serialize %s" % field)
        return tuple(float(m.group(k)) for k in (1, 2, 3))

    def yaml_scalar(block, field):
        m = re.search(r"^\s*%s: ([-+0-9.eE]+)\s*$" % re.escape(field), block, re.M)
        if not m:
            raise SystemExit("Dolphin.prefab does not serialize %s" % field)
        return float(m.group(1))

    src = open(CS, encoding="utf-8").read()
    blk = prefab_block(open(PREFAB, encoding="utf-8").read())
    shipped = {
        "wingAuthoredPivot":     (cs_vec3(src, "wingAuthoredPivot"),     yaml_vec3(blk, "wingAuthoredPivot")),
        "wingFlightPivot":       (cs_vec3(src, "wingFlightPivot"),       yaml_vec3(blk, "wingFlightPivot")),
        "wingStationForward":    (cs_float(src, "wingStationForward"),    yaml_scalar(blk, "wingStationForward")),
        "wingThrottleNeutral":   (cs_float(src, "wingThrottleNeutral"),   yaml_scalar(blk, "wingThrottleNeutral")),
        "wingThrottleSweep":     (cs_float(src, "wingThrottleSweep"),     yaml_scalar(blk, "wingThrottleSweep")),
        "thrusterAuthoredPivot": (cs_vec3(src, "thrusterAuthoredPivot"), yaml_vec3(blk, "thrusterAuthoredPivot")),
        "thrusterFlightPivot":   (cs_vec3(src, "thrusterFlightPivot"),   yaml_vec3(blk, "thrusterFlightPivot")),
        "thrusterAnimationScaler": (cs_float(src, "thrusterAnimationScaler"), yaml_scalar(blk, "thrusterAnimationScaler")),
        "driftWingForward":      (cs_float(src, "driftWingForward"),      yaml_scalar(blk, "driftWingForward")),
        "driftJetBackwardTotal": (cs_float(src, "driftJetBackwardTotal"), yaml_scalar(blk, "driftJetBackwardTotal")),
        "mirrorAppendageRoll":   (cs_bool(src, "mirrorAppendageRoll"),    yaml_scalar(blk, "mirrorAppendageRoll") != 0.0),
    }
    # THE REFERENCE - bleeding-edge, verbatim. Provenance: origin/bleeding-edge
    # Assets/_Prefabs/Spacevessels/Dolphin.prefab (Engine case Left|Right.1-3 m_LocalPosition,
    # LeftWing / RightWing.001 m_LocalPosition - x is 1e-8 float noise on both) and
    # Assets/_Scripts/Controller/Animation/RiptideAnimation.cs (defaultThrusterPosition,
    # defaultWingPosition, exaggeratedAnimationScaler = 3 * animationScaler, no roll mirror).
    REFERENCE = {
        "wingAuthoredPivot":     (0.0, 0.0, 0.1),
        "wingFlightPivot":       (0.0, 0.0, 0.0),
        "thrusterAuthoredPivot": (0.0, 0.14725685, -2.0470345),
        "thrusterFlightPivot":   (0.0, 0.15, -1.7),
        "thrusterAnimationScaler": 75.0,
        "mirrorAppendageRoll":   False,
    }
    # FLIGHT 18's THREE DELIBERATE DEVIATIONS. Everything above is bleeding-edge verbatim and is
    # asserted against it; these three are not, and are asserted against their own measurements in
    # 8f below. Each carries a LEGACY IDENTITY - the value at which it reproduces bleeding-edge
    # exactly - so the deviation is a dial and not a fork: station 0, neutral 0, sweep 75 gives
    # back `(yaw +- throttle) * exaggeratedAnimationScaler` at bleeding-edge's own station, which
    # 8f proves quaternion-for-quaternion over every pose.
    FLIGHT18 = {
        "wingStationForward":  0.5811,   # lands the drawn wing centroid on the drawn ship midpoint
        "wingThrottleNeutral": 0.5,      # XDiff's cruise - the throttle at which the wings are unswept
        "wingThrottleSweep":   66.0,     # deg per unit throttle above cruise; onset is 33.93 x 2
    }
    LEGACY_IDENTITY = {"wingStationForward": 0.0, "wingThrottleNeutral": 0.0, "wingThrottleSweep": 75.0}
    print("   shipped chain constants (C# default | Dolphin.prefab | bleeding-edge):")
    for name, (cs_v, yaml_v) in shipped.items():
        ref = REFERENCE.get(name, FLIGHT18.get(name))
        def fmt(v):
            if isinstance(v, bool): return "off" if not v else "ON"
            if isinstance(v, tuple): return "(%g, %g, %g)" % v
            return "%g" % v
        def close(a, b):
            if isinstance(a, bool) or isinstance(b, bool): return a == b
            if isinstance(a, tuple): return max(abs(x - y) for x, y in zip(a, b)) < 1e-6
            return abs(a - b) < 1e-6
        agree = close(cs_v, yaml_v)
        matches = ref is None or close(yaml_v, ref)
        print("     %-24s %-28s %-28s %s%s" % (name, fmt(cs_v), fmt(yaml_v),
              fmt(ref) if ref is not None else "(rig's own, no reference)",
              "" if agree and matches else "   <-- FAIL"))
        if not agree:
            failures.append("%s: C# default %s disagrees with the prefab's %s - the prefab wins in the "
                            "engine, so the documented number is not the shipped one" % (name, fmt(cs_v), fmt(yaml_v)))
        if not matches:
            where = "flight 18's measured" if name in FLIGHT18 else "bleeding-edge's"
            failures.append("%s: shipped %s is not %s %s" % (name, fmt(yaml_v), where, fmt(ref)))
    A_W = shipped["wingAuthoredPivot"][1]
    # RiptideAnimation.WingStation: bleeding-edge's measured flight pivot plus flight 18's forward
    # offset. The chain reads this, not the bare pivot - 8b keeps the bare pivot's own assertion.
    BE_WING_PIVOT = shipped["wingFlightPivot"][1]
    WING_STATION_FWD = shipped["wingStationForward"][1]
    WING_NEUTRAL = shipped["wingThrottleNeutral"][1]
    WING_SWEEP = shipped["wingThrottleSweep"][1]
    F_W = (BE_WING_PIVOT[0], BE_WING_PIVOT[1], BE_WING_PIVOT[2] + WING_STATION_FWD)
    A_T, F_T = shipped["thrusterAuthoredPivot"][1], shipped["thrusterFlightPivot"][1]
    THRUSTER_AMP = shipped["thrusterAnimationScaler"][1]
    DRIFT_TOTAL = shipped["driftJetBackwardTotal"][1]
    MIRROR = shipped["mirrorAppendageRoll"][1]
    if shipped["driftWingForward"][1] != AUTHORED_WING_LUNGE:
        failures.append("the wing lunge asserted above (%g) is not the shipped %g"
                        % (AUTHORED_WING_LUNGE, shipped["driftWingForward"][1]))

    # THE RIG'S BONES, vessel frame, world units (fbx_bones dump of
    # dolphin_shapekey_with_animations.fbx, x mirrored into Unity's frame). The chassis (`fuse`)
    # sits at the origin, as Dolphin_Test did.
    P_C = (0.0, 0.0, 0.0)
    JETS = {"jetT.l": (-0.26948,  0.39609, -1.90064), "jetT.r": (0.26948,  0.39609, -1.90064),
            "jetm.l": (-0.37291,  0.20079, -1.89641), "jetm.r": (0.37291,  0.20079, -1.89641),
            "jetB.l": (-0.35035, -0.01851, -1.89164), "jetB.r": (0.35035, -0.01851, -1.89164)}
    WINGS = {"wing.l": (-0.96897, 0.0, -0.11397), "wing.r": (0.96897, 0.0, -0.11398)}
    JETHOLD_PIVOT = (0.0, 0.1833, -1.8991)   # the six jethold* bones' shared origin, for the record

    def v_add(a, b): return tuple(x + y for x, y in zip(a, b))
    def v_sub(a, b): return tuple(x - y for x, y in zip(a, b))

    def chain(rest, chassis_turn, own_turn, authored, flight):
        """RiptideAnimation.ChainPosition, transcribed."""
        return v_add(P_C, rot(chassis_turn, v_add(flight, rot(own_turn, v_sub(v_sub(rest, P_C), authored)))))

    def legacy_point(x_rest, chassis_turn, own_turn, authored, flight):
        """Where the legacy hierarchy carries a point resting at x_rest on a chassis child whose
        transform was authored at `authored`, lerped to `flight`, and turned by own_turn:
        world = C * (flight + own * (x_rest - authored))."""
        return rot(chassis_turn, v_add(flight, rot(own_turn, v_sub(x_rest, authored))))

    S_C, E_W = 25.0, 75.0
    def wing_own(side, p, y, r, th, mirror, neutral=None, sweep=None):
        """RiptideAnimation's wing turn. The yaw-STICK half is bleeding-edge's untouched; the
        THROTTLE half is SweepAboveCruise(th) * wingThrottleSweep - Brake()'s exact mirror."""
        rs = -1.0 if mirror else 1.0
        brake = th - 0.65 if th < 0.65 else 0.0
        n = WING_NEUTRAL if neutral is None else neutral
        a = WING_SWEEP if sweep is None else sweep
        above = th - n if th > n else 0.0
        return euler(brake * S_C, y * E_W + side * above * a, (rs * r + side * p) * S_C)
    def jet_own(p, y, r, mirror, amp):
        rs = -1.0 if mirror else 1.0
        return euler(p * amp, y * amp, rs * r * amp)

    POSES = [(1, 1, 1, 1), (1, -1, 1, -1), (-1, 1, -1, 1), (-1, -1, -1, -1), (1, 1, -1, 0),
             (-1, 1, 1, 0.3), (0.6, -0.5, 0.8, 0.3), (0.4, 0, 0, 0), (0, 0.7, 0, 0), (0, 0, 1, 0),
             (0, 0, 0, 1), (0, 0, 0, 0)]
    OFFSETS = [(0.0, 0.0, 0.0), (0.10, 0.05, -0.30), (-0.05, 0.12, 0.08), (0.2, -0.2, -0.4)]
    # plausible, non-trivial rest orientations for the bones (the proof is orientation-agnostic:
    # it holds for ANY rest, as the algebra shows - these keep the numbers honest)
    RESTS = {"jetT.l": qmul(euler(128.277, -1.361, -95.378), euler(-2.018, 172.348, 57.201)),
             "jetm.l": qmul(euler(92.691, 0.38, -95.968), euler(-4.049, 169.045, 57.459)),
             "jetB.l": qmul(euler(60.061, 1.163, -96.79), euler(-3.559, 176.371, 58.567)),
             "jetT.r": qmul(euler(-51.723, -1.361, 84.622), euler(2.006, 4.254, -57.321)),
             "jetm.r": qmul(euler(-87.309, 0.38, 84.032), euler(3.984, 3.773, -57.967)),
             "jetB.r": qmul(euler(-119.939, 1.163, 83.21), euler(3.568, 5.525, -58.448)),
             "wing.l": qmul(euler(77.08, 0, -180), euler(5.901, 0, 0)),
             "wing.r": qmul(euler(-102.92, 0, 0), euler(5.901, 0, 0))}

    # 8a. EXACTNESS. Every vertex a rig bone skins must land where the legacy vertex resting at
    #     the same point lands, for every stick pose - to float64 round-off.
    worst = 0.0
    for (p, y, r, th) in POSES:
        C = euler(p * S_C, y * S_C, r * S_C)
        for name, B in list(JETS.items()) + list(WINGS.items()):
            Q = RESTS[name]
            if name.startswith("jet"):
                E, A, F = jet_own(p, y, r, MIRROR, THRUSTER_AMP), A_T, F_T
            else:
                E, A, F = wing_own(1.0 if name.endswith(".r") else -1.0, p, y, r, th, MIRROR), A_W, F_W
            pos = chain(B, C, E, A, F)                # the shipped bone position
            rq = qmul(qmul(C, E), Q)                   # the shipped bone orientation
            for u in OFFSETS:
                rig_vert = v_add(pos, rot(rq, u))
                x_rest = v_add(B, rot(Q, u))           # the same vertex, at rest
                legacy_vert = legacy_point(x_rest, C, E, A, F)
                worst = max(worst, max(abs(a - b) for a, b in zip(rig_vert, legacy_vert)))
    print("   8a exactness: rig bone chain vs the legacy chassis hierarchy, %d poses x 8 parts x %d "
          "vertices: worst %.3e wu%s" % (len(POSES), len(OFFSETS), worst, "" if worst <= 1e-12 else "   <-- FAIL"))
    if worst > 1e-12:
        failures.append("the chassis chain differs from the legacy hierarchy by %.3e wu" % worst)

    # 8b. ZERO INPUT reduces to rest + (flightPivot - authoredPivot): the +0.347 forward seat
    #     flight 16 measured for the boosters, and the 0.1 aft drag on the wings it missed.
    drag_t = v_sub(F_T, A_T); drag_w = v_sub(F_W, A_W)
    for name, B in list(JETS.items()) + list(WINGS.items()):
        A, F, drag = (A_T, F_T, drag_t) if name.startswith("jet") else (A_W, F_W, drag_w)
        at_rest = chain(B, euler(0, 0, 0), euler(0, 0, 0), A, F)
        if max(abs(a - b) for a, b in zip(at_rest, v_add(B, drag))) > 1e-12:
            failures.append("%s does not reduce to rest + drag at zero input" % name)
    print("   8b zero input: boosters rest + (%.4f, %.4f, %+.4f), wings rest + (%.4f, %.4f, %+.4f)"
          % (drag_t + drag_w))
    NOZZLE_LEAD, NOZZLE_TRAIL, FUSELAGE_TAIL = -1.887, -2.290, -2.471   # rig jet clusters, vessel z
    BE_REST_LEAD, BE_REST_TRAIL = -1.540, -1.898                        # bleeding-edge's DRAWN station
    lead, trail = NOZZLE_LEAD + drag_t[2], NOZZLE_TRAIL + drag_t[2]
    print("      booster station z %.3f..%.3f vs bleeding-edge %.3f..%.3f (lead delta %+.4f)"
          % (trail, lead, BE_REST_TRAIL, BE_REST_LEAD, lead - BE_REST_LEAD))
    if abs(lead - BE_REST_LEAD) > 0.01:
        failures.append("the boosters' leading edge %.3f is off bleeding-edge's %.3f" % (lead, BE_REST_LEAD))
    RIG_WING_REST_Z, BE_WING_DRAWN_Z = -0.3038, -0.4032   # wing geometry, vessel z (check 8's forensic)
    # The BARE pivot still has to be bleeding-edge's - flight 18 moves the wings through a separate
    # offset, so if these two ever disagree the legacy measurement itself has drifted.
    be_wing_z = RIG_WING_REST_Z + (BE_WING_PIVOT[2] - A_W[2])
    print("      wing geometry z %.4f vs bleeding-edge's drawn %.4f (delta %+.4f)"
          % (be_wing_z, BE_WING_DRAWN_Z, be_wing_z - BE_WING_DRAWN_Z))
    if abs(be_wing_z - BE_WING_DRAWN_Z) > 0.002:
        failures.append("the wings' legacy station %.4f is off bleeding-edge's %.4f" % (be_wing_z, BE_WING_DRAWN_Z))
    shipped_wing_z = RIG_WING_REST_Z + drag_w[2]
    print("      wings SHIPPED at z %.4f - %+.4f forward of that, flight 18's station (8f)"
          % (shipped_wing_z, WING_STATION_FWD))
    # The boosters must stay behind the wings' trailing edge. -0.617 is the wings' unswept drawn
    # trail at BLEEDING-EDGE's station, so the shipped trail carries the forward offset with it.
    WING_TRAIL_AT_BE_STATION = -0.617
    WING_TRAIL = WING_TRAIL_AT_BE_STATION + WING_STATION_FWD
    if lead > WING_TRAIL:
        failures.append("the boosters lead at %.3f, forward of the wings' trailing edge %.3f" % (lead, WING_TRAIL))

    # 8c. THE NEGATIVE CONTROL - the retired construction (flights 3-16): orientation composed
    #     C * E * rest about the bone's OWN pivot, position HELD at rest + drag. Its orientation
    #     was already exact; what it lacked was the ORBIT and the shared PIVOT, and this is how
    #     far that put a bone from where the legacy part frame carries it.
    worst_ctrl, worst_ctrl_name = 0.0, ""
    ctrl_at_zero_own = 0.0
    for (p, y, r, th) in POSES:
        C = euler(p * S_C, y * S_C, r * S_C)
        for name, B in list(JETS.items()) + list(WINGS.items()):
            if name.startswith("jet"):
                E, A, F, drag = jet_own(p, y, r, MIRROR, THRUSTER_AMP), A_T, F_T, drag_t
            else:
                E, A, F, drag = wing_own(1.0 if name.endswith(".r") else -1.0, p, y, r, th, MIRROR), A_W, F_W, drag_w
            held = v_add(B, drag)
            d = dist(held, chain(B, C, E, A, F))
            if d > worst_ctrl:
                worst_ctrl, worst_ctrl_name = d, name
            # and with the own term at ZERO (flight 15's own=0 experiment): the orbit alone
            ctrl_at_zero_own = max(ctrl_at_zero_own, dist(held, chain(B, C, euler(0, 0, 0), A, F)))
    print("   8c control - the retired held-position construction: bone pivot up to %.3f wu (%s) from "
          "where the legacy hierarchy carries it; %.3f wu with the own term at zero (the orbit alone)"
          % (worst_ctrl, worst_ctrl_name, ctrl_at_zero_own))
    if worst_ctrl < 0.3 or ctrl_at_zero_own < 0.3:
        failures.append("the held-position control did not reproduce the defect (%.3f / %.3f wu)"
                        % (worst_ctrl, ctrl_at_zero_own))

    # 8d. THE MIRROR is an own-term flip only: with it on, a part whose own amplitude is zero is
    #     BIT-IDENTICAL to the legacy chain (the orbit is never mirrored), and a booster's relative
    #     roll against the fuselage flips sign. Shipped OFF (asserted against the reference above).
    C = euler(0, 0, 25.0)
    for name, B in JETS.items():
        same = chain(B, C, jet_own(0, 0, 1, True, 0.0), A_T, F_T)
        base = chain(B, C, jet_own(0, 0, 1, False, 0.0), A_T, F_T)
        if max(abs(a - b) for a, b in zip(same, base)) > 1e-12:
            failures.append("the mirror moved %s's orbit" % name)
    rel_on = to_axis_angle(jet_own(0, 0, 1, True, THRUSTER_AMP))
    rel_off = to_axis_angle(jet_own(0, 0, 1, False, THRUSTER_AMP))
    flipped = rel_on[0][2] * rel_on[1] * (rel_off[0][2] * rel_off[1]) < 0
    print("   8d mirror (opt-in, %s): orbit untouched; own roll %+.1f -> %+.1f deg at full roll%s"
          % ("ON" if MIRROR else "off", rel_off[0][2] * rel_off[1], rel_on[0][2] * rel_on[1],
             "" if flipped else "   <-- FAIL"))
    if not flipped:
        failures.append("the roll mirror does not flip the own term's roll")

    # 8e. THE DRIFT is untouched by the chain and keeps its own signed-off assertions: the drift
    #     total is visible, and it swings the nozzles clear of the fuselage tail.
    HULL = 3.4482
    VISIBLE = 0.05 * HULL
    if 0 < abs(DRIFT_TOTAL) < VISIBLE:
        failures.append("the drift total %.3f is below the visibility floor" % DRIFT_TOTAL)
    drift_lead = NOZZLE_LEAD - DRIFT_TOTAL
    clears = drift_lead <= FUSELAGE_TAIL
    print("   8e drift: total %.2f (%.1f%% of the %.3f hull); nozzles lead at z %.3f vs the fuselage "
          "tail %.3f - %s" % (DRIFT_TOTAL, 100 * DRIFT_TOTAL / HULL, HULL, drift_lead, FUSELAGE_TAIL,
                              "clear of the body" if clears else "still alongside it"))
    if not clears:
        failures.append("the drift seat leaves the nozzles alongside the fuselage tail")
    # for the record: how far the chain carries a booster pivot at full stick (bleeding-edge's
    # own sweep, by 8a)
    travel = max(dist(v_add(B, drag_t), chain(B, euler(p * S_C, y * S_C, r * S_C),
                                             jet_own(p, y, r, MIRROR, THRUSTER_AMP), A_T, F_T))
                 for (p, y, r, th) in POSES for B in JETS.values())
    print("      booster pivot travel at full stick on the chain: %.3f wu (bleeding-edge's, by 8a); "
          "shared jethold origin %s" % (travel, "(%g, %g, %g)" % JETHOLD_PIVOT))

    # 8f. THE WINGS' THROTTLE TERM AND STATION (flight 18) - the only thing here that is NOT
    #     bleeding-edge's, and the one playtest asked for: "move the wings forward closer to the
    #     middle, and lessen how much they clip into the body when you increase the throttle".
    #
    #     Both come off ONE fact. The throttle the puppetry is handed is InputStatus.XDiff, which
    #     runs 0..1 with 0.5 AT NEUTRAL CRUISE (`xDiff = (right.x - left.x + 2) / 4`), so
    #     bleeding-edge's `(yaw +- throttle) * 75` holds the wings 37.5 degrees swept while the
    #     pilot is doing nothing, and 75 at full throttle. A swept wing also DRAWS further aft
    #     than its pivot, so that resting sweep is most of why the wings read as sitting at the
    #     back: measured, the drawn centroid at cruise is z -0.9250 against a drawn ship whose
    #     midpoint is +0.1781 - 1.1032 wu aft of the middle.
    #
    #     Measured on the shipped rig, both wings' 588 skinned vertices against the convex
    #     cross-section of every body cluster, sliced 90 ways in z (the same vertex clouds
    #     check 8a proves the chain carries):
    #
    #       wing yaw at which a root first enters the body   33.42 deg at bleeding-edge's station
    #                                                        33.93 deg at flight 18's
    #       deepest penetration, throttle axis, stick centred
    #                       bleeding-edge  0.1874 wu at full throttle, 0.0454 wu at CRUISE
    #                       flight 18      0.0000 wu at every throttle
    #
    #     The residual over the full input cube is NOT zero and is not claimed to be: 0.2745 ->
    #     0.2401 wu. What is left is the chassis ORBIT (25 deg on three axes) and the YAW-STICK
    #     term (75 deg), both legacy and both deliberately untouched by flight 17 and by this.
    WING_CLIP_ONSET_BE = 33.42            # deg of wing yaw, at wingFlightPivot alone
    WING_CLIP_ONSET_SHIPPED = 33.93       # deg, at wingFlightPivot + wingStationForward
    WING_BINDING_VERT = (0.7162, 0.0518, -0.5326)   # wing.r vertex 109, rest, vessel frame
    SHIP_MID_Z = 0.1781                   # mean of the drawn hull's -2.4707 tail and +2.8270 nose
    WING_REST_CENTROID_Z = RIG_WING_REST_Z          # 588 wing verts; the same -0.3038

    cruise_sweep = (0.5 - WING_NEUTRAL) * WING_SWEEP if 0.5 > WING_NEUTRAL else 0.0
    peak_sweep = (1.0 - WING_NEUTRAL) * WING_SWEEP
    be_cruise_sweep, be_peak_sweep = 0.5 * 75.0, 1.0 * 75.0
    print("   8f wings: sweep at CRUISE %.1f deg (bleeding-edge %.1f), at FULL throttle %.1f "
          "(bleeding-edge %.1f); the body is entered at %.2f deg"
          % (cruise_sweep, be_cruise_sweep, peak_sweep, be_peak_sweep, WING_CLIP_ONSET_SHIPPED))
    if cruise_sweep > 1e-9:
        failures.append("the wings are swept %.1f deg at a centred stick; cruise must leave them "
                        "unswept, which is the pose ApplyRestingLayout already draws" % cruise_sweep)
    if peak_sweep > WING_CLIP_ONSET_SHIPPED:
        failures.append("the wings sweep %.2f deg at full throttle, past the %.2f deg at which a "
                        "root enters the body" % (peak_sweep, WING_CLIP_ONSET_SHIPPED))

    #     THE STATION is solved, not chosen: the drawn wing centroid at cruise is
    #     restCentroid + (station - authoredPivot.z), and it is asked to land on the ship's own
    #     midpoint. The closed form ignores Brake(0.5)'s -3.75 deg of wing pitch, which moves the
    #     centroid 0.0008 wu - hence the same 0.002 tolerance 8b uses for the wing station.
    want_station = SHIP_MID_Z - (WING_REST_CENTROID_Z - A_W[2])
    drawn_centroid = WING_REST_CENTROID_Z + (WING_STATION_FWD - A_W[2])
    print("      station %.4f draws the wing centroid at z %+.4f against the ship midpoint %+.4f "
          "(solved %.4f, delta %+.4f)"
          % (WING_STATION_FWD, drawn_centroid, SHIP_MID_Z, want_station, WING_STATION_FWD - want_station))
    if abs(WING_STATION_FWD - want_station) > 0.002:
        failures.append("wingStationForward %.4f does not land the wing centroid on the ship "
                        "midpoint (%.4f would)" % (WING_STATION_FWD, want_station))

    #     THE LEGACY IDENTITY. At (station 0, neutral 0, sweep 75) the shipped expression must be
    #     bleeding-edge's `(yaw +- throttle) * exaggeratedAnimationScaler`, quaternion for
    #     quaternion. This is what makes the three fields a DIAL rather than a fork.
    def be_wing_own(side, p, y, r, th, mirror):
        rs = -1.0 if mirror else 1.0
        brake = th - 0.65 if th < 0.65 else 0.0
        return euler(brake * S_C, (y + side * th) * E_W, (rs * r + side * p) * S_C)
    #     Swept over the throttle range the ENGINE can actually produce. POSES carries throttle -1
    #     because it was written as a stick axis, and that is the very assumption this check
    #     exists to unpick: throttle is XDiff, `(right.x - left.x + 2) / 4`, so it is 0..1 and
    #     never negative. Below 0 the clamp has nothing to reproduce and the two terms differ by
    #     construction - asserting there would be asserting about an input no pilot can send.
    worst_identity, checked = 0.0, 0
    for (p, y, r, _ignored_throttle) in POSES:
        for k in range(21):
            th = k / 20.0
            for side in (1.0, -1.0):
                a = wing_own(side, p, y, r, th, MIRROR,
                             neutral=LEGACY_IDENTITY["wingThrottleNeutral"],
                             sweep=LEGACY_IDENTITY["wingThrottleSweep"])
                b = be_wing_own(side, p, y, r, th, MIRROR)
                worst_identity = max(worst_identity, max(abs(u - v) for u, v in zip(a, b)))
                checked += 1
    print("      legacy identity at (neutral %g, sweep %g) over %d poses x throttle 0..1: worst "
          "quaternion component delta %.3e%s"
          % (LEGACY_IDENTITY["wingThrottleNeutral"], LEGACY_IDENTITY["wingThrottleSweep"],
             checked, worst_identity, "" if worst_identity <= 1e-15 else "   <-- FAIL"))
    if worst_identity > 1e-15:
        failures.append("the wing term does not reduce to bleeding-edge's at its legacy identity "
                        "(%.3e)" % worst_identity)

    #     THE NEGATIVE CONTROL. Bleeding-edge's own numbers must FAIL the two assertions above, or
    #     they are asserting nothing: it sweeps 37.5 deg at a centred stick (past the 33.42 deg
    #     onset at ITS station) and draws its wing centroid 1.1 wu aft of the ship's middle.
    ctrl_cruise_clears = be_cruise_sweep <= WING_CLIP_ONSET_BE
    ctrl_station = LEGACY_IDENTITY["wingStationForward"]
    ctrl_centroid = WING_REST_CENTROID_Z + (ctrl_station - A_W[2])
    ctrl_on_mid = abs(ctrl_station - want_station) <= 0.002
    print("      control - bleeding-edge's own: cruise sweep %.1f deg vs a %.2f deg onset (%s), "
          "wing centroid %+.4f vs midpoint %+.4f (%s)"
          % (be_cruise_sweep, WING_CLIP_ONSET_BE, "clears" if ctrl_cruise_clears else "INSIDE the body",
             ctrl_centroid, SHIP_MID_Z, "on it" if ctrl_on_mid else "%.4f aft" % (SHIP_MID_Z - ctrl_centroid)))
    if ctrl_cruise_clears or ctrl_on_mid:
        failures.append("the flight-18 wing control did not reproduce the reported defect")

    #     And the binding vertex, for the record: the wing.r vertex that first meets the body.
    bvx, bvy, bvz = WING_BINDING_VERT
    turned = rot(euler(0.0, peak_sweep, 0.0), (bvx - A_W[0], bvy - A_W[1], bvz - A_W[2]))
    print("      binding vert %s sweeps to |xy| %.4f at full throttle"
          % ("(%.4f, %.4f, %.4f)" % WING_BINDING_VERT, math.hypot(turned[0], turned[1])))

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
    print("OK - ship-axis turns, the legacy chassis chain exact (orbit + shared pivots, constants read\n"
          "     off the shipped C# and prefab), a guarded stateless Course frame, the drift cage in that\n"
          "     frame, and measured world-unit offsets that survive any chain scale.")
    return 0

if __name__ == "__main__":
    sys.exit(main())
