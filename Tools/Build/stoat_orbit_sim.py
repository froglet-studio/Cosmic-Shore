#!/usr/bin/env python3
"""The AI Stoat on the orbit alone (2026-11-02, Yash): how fast it races Slingshot with only Time mode's catch.

Since the field dipole was parked (Docs/Studios/STOAT_FIELD_TRAJECTORY_PARKED.md) the catch sling is the Stoat's only
speed: the hull's flight clock warps only while a pair is held (StoatSlingExecutor.WarpsFlight, from the press). So the
AI's press rule decides its pace, and this model picks its numbers (StoatSlingConfig's ai* rows and autopilotHold01).

Ported line for line: StoatSlingExecutor.AutopilotSling and StepOrbit, StoatSlingMath (CatchPair, Catches, OrbitRadius,
ClearOfRepulsor, OrbitHeading, SettleHeading, RadialCorrection, SlingBoost, SweetSpot, TimedKick),
StoatDipoleMath.StepBoost and AutopilotGauge, GateRaceController's gate steering with AICommitFromApproachPoint on
(through switchback_dolphin_sim's constants), SlingshotCourse.ForIntensity through the shared HeadlongCircuit port
(Tools/Compute/evaluators/course_port.py). The clock warp scales the hull's turn with its speed
(VesselTransformer.FlightDeltaTime), so the 72 u turning circle holds at any warp.

NOT modelled: rivals, prisms, the look of the catch, the human. It is a model: the editor race is the measurement.

    py -3 Tools/Build/stoat_orbit_sim.py               # the table of the shipped and tuned rows
    py -3 Tools/Build/stoat_orbit_sim.py --grid        # the search that chose the tuned row
    py -3 Tools/Build/stoat_orbit_sim.py --nose-lead 2.5   # every row again with the AI nose lead on
    py -3 Tools/Build/stoat_orbit_sim.py --self-test

The AI nose lead (AIPilot.NoseLeadGain, PR #1786; Tools/Build/ai_nose_lead.py is the shared piece) is race()'s
`nose_lead` knob, OFF by default: a gain over 0 judges the stick's error from the COMMAND and steers it onto the nose
turned toward the aim by the gain times its error, past it (at most max_extra_deg, 40), where the lagging nose is pulled
from; off while the field flies the nose (the orbit, the shell) and on the launch frame, on through the break-off.
"""
import argparse, math, os, statistics, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "Compute", "evaluators"))
from rhino_drive_sim import add, sub, mul, dot, norm, unit, ang, rotate_toward
from scarab_drive_sim import ai_stick
from ai_nose_lead import NoseLead, effective_gain, judge_and_want   # the AI nose lead (AIPilot.NoseLeadGain), one shared piece
import regatta_line_sim as L
import course_port as CP

CRUISE, TURN = 60.0, 48.0                       # Stoat.prefab DefaultThrottleScaler, Pitch/Yaw
COMMIT, LEAD, THROUGH = 160.0, 180.0, 120.0     # MinigameSlingshot.unity aiCommitDistance, aiApproachLead, aiThroughDistance
LEAD_SHARE, LEAD_INSIDE_SHARE = 0.4, 0.8        # GateRaceController.ApproachLeadShareOfLeg, the approach-point cap
LAPS = 2                                        # EndConditionOverridesSO.slingshotGateTarget 16 = 2 x 8
# StoatSlingConfig.asset
CATCH_AHEAD, CATCH_SIDE, CATCH_REACH, CATCH_DEGREES = 150.0, 45.0, 2.0, 40.0
WIDE, TIGHT, HOLD_EXP, WHITE_CLEAR = 150.0, 55.0, 1.5, 0.8
FOLLOW_RATE, EASE_RATE, RADIAL_RATE, SETTLE_RATE, MAX_ORBIT = 3.0, 0.8, 5.0, 3.0, 720.0
BOOST_MIN, BOOST_MAX, BOOST_SECONDS, MISS_SHARE = 0.25, 0.9, 1.5, 0.35
SWEET, SWEET_HALF, SWEET_FALL = 90.0, 30.0, 40.0
# the aimed exit (2026-11-03): a release after about a second of orbit launches the ENTRY heading turned 30-45 deg
EXIT_MIN, EXIT_MAX, EXIT_T, EXIT_HALF, EXIT_FALL = 30.0, 45.0, 1.1, 0.3, 0.4
CATCH_WARP_SHARE = 1.15
# The trampoline (Yash, 2026-11-04: "remove the whole orbit ... just keep white hole, make it so that vessels can bounce off
# with extra boost, it will be a 3D trampoline"): a soft shell round a lone white hole, laid AHEAD and line x R to the side
# away from the bounce; inside it a spring pushes the hull out along the shell's normal (StoatSlingMath.TrampolineStep).
TRAMP_AHEAD, TRAMP_R, TRAMP_LINE, TRAMP_SOFT, TRAMP_LIFE, TRAMP_PREDICT_MAX = 140.0, 60.0, 0.75, 0.35, 4.0, 90.0
# the turn a graze gives at 150 u/s: 0.5 -> 83, 0.7 -> 53, 0.9 -> 19 deg, head-on 180 (about (1 - line) x 160)
TRAMP_TURN_SCALE, TRAMP_LINE_MIN, TRAMP_LINE_MAX = 140.0, 0.3, 0.92


def tramp_line_for(turn_deg, scale=TRAMP_TURN_SCALE):
    """StoatSlingMath.TrampolineLineFor: how far off the centre to graze for a bounce of turn_deg."""
    return max(TRAMP_LINE_MIN, min(TRAMP_LINE_MAX, 1.0 - turn_deg / scale))
# StoatDipoleConfig.asset: the boost at Time 0 (x2.5) and its rise and fade
BOOST_TOP, BOOST_RISE, BOOST_FADE = 2.5, 20.0, 0.6
WARP_TOP = 1.0 + (BOOST_TOP - 1.0) * CATCH_WARP_SHARE   # StoatSlingConfigSO.CatchWarpTarget

# The rows as they shipped before the orbit-only kit: nothing presses for the warp, Mass mode's dipole did that job.
HULL_ROW = {"hold": 0.5, "min_turn": 30.0, "cone": 20.0, "give_up": 4.0, "interval": 3.0, "min_dist": 120.0, "warp_dist": 0.0}
VARIANT_ROW = {"hold": 1.0, "min_turn": 7.84, "cone": 15.999, "give_up": 4.0, "interval": 0.941, "min_dist": 123.282, "warp_dist": 0.0}
# What ships since: those numbers (learned from Yash's races) plus the warp press, chosen by --grid (2026-11-02). The hull's
# StoatSlingConfig and StoatSlingConfig_Slingshot both carry it.
TUNED_ROW = dict(VARIANT_ROW, warp_dist=280.0)
# The orbit after Yash's 2026-11-02 play test (StoatSlingConfig's new rows): caught, the hull is drawn in to 0.6 of the
# squeeze's circle at 1.5 /s and flies 15% faster round it; only its own side catches (right = clockwise). The flick launch is
# a person's gesture (the AI lets go by its cone), and the launch's aim and Mass power are not modelled: the kick is x1 at rest.
ORBIT_ROW = dict(TUNED_ROW, inner=0.6, pull=1.5, bonus=0.15, side=True)
# EARNED speed (Grok's review, 2026-11-02, StoatSlingConfig's earned rows): a held pair alone warps to 0.2 of the catch's warp,
# the orbit to all of it, a timed launch keeps it as momentum over 2.5 s (the larger wins, never the sum); a release before 30
# degrees pays nothing; the kick never passes 1.6 x the release speed. The AI lets go in the window whenever its aim (the
# launch's 60 degree cone) reaches the target, and the launch turns toward where its stick points.
EARNED = dict(press_share=0.5, min_sweep=30.0, momentum_s=6.0, cap=1.6, aim_cone=60.0)
EARNED_ROW = dict(ORBIT_ROW, **EARNED)
# What ships (--grid-earned chose it): no warp-only presses, and the AI holds for the window (aiHoldForWindowDegrees 120).
SHIPPED_ROW = dict(EARNED_ROW, warp_dist=0.0, window_ai=120.0)
# The easier sling (Yash, 2026-11-03: "the slingshot mechanic is little hard"; the constants above carry its catch and
# window): drawn in to 0.75 of the circle at 1 /s, so the orbit turns slower and the window lasts 2-3x as long.
SHIPPED_ROW = dict(SHIPPED_ROW, inner=0.75, pull=1.0)
# The aimed exit (Yash, 2026-11-03: "slingshot at 30-45 degrees from angle of entry after a second of orbiting").
SLING_ROW = SHIPPED_ROW
SHIPPED_ROW = dict(SHIPPED_ROW, aimed=True, min_turn=20.0)   # the AI slings only for a turn of 20 deg or more (was 7.84)
# Regatta (MinigameRegatta.unity's approach numbers, three laps of RegattaCourse): the race line and the rails are not
# modelled, so this is the gate steering only. Time 10 is the level regatta_balance.py gives the Stoat on the card.
REGATTA_STEER = {"commit": 420.0, "lead": 480.0, "through": 320.0}


def slingshot_settings(intensity):
    """SlingshotCourse.ForIntensity: 600 u base circle, floors 2 / 1.6 / 1.3 / 1.2 x the 179 u slung circle."""
    i = min(max(int(intensity), 1), 4) - 1
    full = 179.0
    return {
        "GateCount": 8, "BaseRadius": 600.0,
        "CornerProfile": [[90, 72, 58, 48, 38, 28, 16, 10], [110, 90, 60, 40, 25, 18, 12, 5],
                          [150, 135, 60, 6, 4, 3, 2, 1], [140, 135, 70, 6, 4, 3, 2, 1]][i],
        "CornerRadiusFactor": 0.0, "CornerFloorRadius": max(2 * CRUISE / math.radians(TURN), [2.0, 1.6, 1.3, 1.2][i] * full),
        "RadialSwing": 0.42, "AngularSpread": [1.2, 3.0, 2.0, 4.0][i],
        "LateralPerturbation": [80.0, 120.0, 160.0, 200.0][i], "RingRadius": [48.0, 40.0, 32.0, 26.0][i],
        "AxisJitterDegrees": [20.0, 28.0, 36.0, 44.0][i], "MaxPresentDegrees": [50.0, 60.0, 76.0, 76.0][i],
        "InnerRadius": 480.0, "OuterRadius": 1080.0, "FirstGateDirection": (0.0, 1.0, 0.0),
    }


def course(intensity, seed):
    gates = CP.generate(seed, slingshot_settings(intensity))
    return [{"p": [float(x) for x in g["p"]], "a": unit([float(x) for x in g["a"]]), "r": float(g["r"])} for g in gates]


def cross(a, b):
    return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]


def rotate_about(v, axis, rad):
    """Rodrigues: v turned rad about the unit axis (Quaternion.AngleAxis * v)."""
    c, s = math.cos(rad), math.sin(rad)
    return add(add(mul(v, c), mul(cross(axis, v), s)), mul(axis, dot(axis, v) * (1 - c)))


def orbit_radius(hold):
    t = max(0.0, min(1.0, hold)) ** max(0.01, HOLD_EXP)
    r = WIDE + (min(max(TIGHT, 1.0), WIDE) - WIDE) * t
    return min(r, max(1.0, WHITE_CLEAR * 2.0 * CATCH_SIDE))     # ClearOfRepulsor at the poles' distance


def catches(pos, fwd, attractor, r):
    to = sub(attractor, pos); d = norm(to)
    if d <= r: return True
    if d > r * CATCH_REACH: return False
    return ang(fwd, to) >= CATCH_DEGREES


def sweet_spot(deg):
    off = abs(abs(deg) - SWEET)
    if off <= SWEET_HALF: return 1.0
    t = min(1.0, (off - SWEET_HALF) / SWEET_FALL)
    return 1.0 - t * t * (3.0 - 2.0 * t)


def exit_timing(seconds):
    """StoatSlingConfigSO.ExitTiming: the sweet spot's curve in seconds of orbit."""
    off = abs(seconds - EXIT_T)
    if off <= EXIT_HALF: return 1.0
    t = min(1.0, (off - EXIT_HALF) / EXIT_FALL)
    return 1.0 - t * t * (3.0 - 2.0 * t)


def aimed_exit(entry, radial, fwd, to):
    """StoatSlingMath.AimedExit at the angle the AI's stick picks: the one of 30-45 deg nearest its aim."""
    axis = cross(radial, fwd)
    if norm(axis) < 1e-6: return list(entry)
    axis = unit(axis)
    best = None
    for k in range(7):
        d = rotate_about(entry, axis, math.radians(EXIT_MIN + (EXIT_MAX - EXIT_MIN) * k / 6))
        if best is None or ang(d, to) < ang(best, to): best = d
    return unit(best)


def tramp_centre(pos, fwd, right, bounce_left, omega, speed, line=TRAMP_LINE, radius=TRAMP_R, ahead=TRAMP_AHEAD):
    """StoatSlingMath.TrampolineCentre: on the line the hull is flying (its turn carried on, up to 90 deg, when omega is
    given), line x R to the side AWAY from the bounce, so a hull flying on grazes the shell's near flank."""
    p, f = list(pos), list(fwd)
    rate = norm(omega) if omega is not None else 0.0
    if rate > 1e-4:
        axis = unit(omega); turn = min(math.radians(TRAMP_PREDICT_MAX), rate * ahead / max(speed, 1.0)); steps = 12
        for _ in range(steps):
            p = add(p, mul(f, ahead / steps)); f = rotate_about(f, axis, turn / steps)
    else:
        p = add(p, mul(f, ahead))
    side = sub(right, mul(f, dot(right, f)))
    side = unit(side) if norm(side) > 1e-6 else right
    return add(p, mul(side, line * radius if bounce_left else -line * radius))


def tramp_flank(pos, centre, right, bounce_left, line=TRAMP_LINE, radius=TRAMP_R):
    """StoatSlingMath.TrampolineFlank: the point line x R from the centre toward the bounce side, square to the line from
    the hull - where a hull flying at it grazes the shell's near flank and is turned toward the bounce."""
    to = sub(centre, pos); f = unit(to)
    side = sub(right, mul(f, dot(right, f)))
    side = unit(side) if norm(side) > 1e-6 else right
    return add(centre, mul(side, -line * radius if bounce_left else line * radius))


def tramp_step(vel, radial, radius, soft, dt):
    """StoatSlingMath.TrampolineStep: inside the shell the spring (pi / soft)^2 x depth pushes the velocity out along the
    normal. The velocity is carried from the touch, so a head-on hull is stopped and sent back; the hull flies its
    direction at its own speed."""
    d = norm(radial)
    depth = radius - d
    if depth <= 0 or d < 1e-6: return list(vel)
    k = (math.pi / soft) ** 2
    return add(vel, mul(radial, k * depth * dt / d))


def race(gates, row, laps=LAPS, warp=True, T=300.0, dt=1 / 60, orrery=None, nose_lead=None):
    """row: StoatSlingConfig's AI numbers. warp=False is the floor: the hull's cruise, never a press.
    nose_lead: AIPilot.NoseLeadGain (ai_nose_lead.NoseLead.from_config: None = OFF, today's steering; a gain, a
    {"gain", "max_extra_deg"} dict or a NoseLead), stated at the fleet lag and scaled once to this model's nose rate."""
    n = len(gates); total = n * laps
    commit_r, lead_r, through_r = row.get("commit", COMMIT), row.get("lead", LEAD), row.get("through", THROUGH)
    warp_top, kick_rate = row.get("warp_top", WARP_TOP), row.get("kick_rate", 1.0)
    knob = NoseLead.from_config(nose_lead)
    # AIPilot.EffectiveNoseLeadGain ONCE a race, from this model's own nose rate: L.NOSE_FOLLOW, the fleet's 1.5/s that
    # turn_with_lag follows the command at (on the flight clock, like the turn), so it is max(1, gain) here and 0 when off
    lead_gain, lead_extra = effective_gain(knob.gain, L.NOSE_FOLLOW), knob.max_extra_deg
    fwd = unit(gates[0]["p"]); pos = [0.0, 0.0, 0.0]; cmd = list(fwd)   # the hull starts inside the shell, heading to gate 0
    clock = 1.0; R = CRUISE / math.radians(TURN); run = max(max(0.0, 2 * R - 18), CRUISE * 1.5)
    idx = 0; locked = -1; side = 1.0; lead = LEAD; t = 0.0
    extending = False; ext_t = 0.0; breaks = 0; misses = 0; orbit = L.Orbit()
    laid = caught = False; attractor = None; orbit_side_left = False; radius = 0.0; swept = 0.0; last_radial = None; laid_at = 0.0
    steered_out = False
    orbit_s = 0.0; entry = None; aimed = bool(row.get("aimed"))
    tramp = bool(row.get("tramp")); tramp_c = None; tramp_aim = None; bounces = 0; tramp_misses = 0; prev_fwd = list(fwd); omega = [0.0, 0.0, 0.0]
    t_line, t_r, t_ahead = row.get("line", TRAMP_LINE), row.get("radius", TRAMP_R), row.get("ahead", TRAMP_AHEAD)
    # The Orrery (orrery_port.layout): its static prisms in a 24 u hash for contacts, its moons the AI's anchors.
    moons, hits, grid = [], set(), {}
    if orrery is not None:
        moons, prisms, _ = orrery
        for k, pr in enumerate(prisms):
            grid.setdefault(tuple(int(math.floor(c / 24.0)) for c in pr["p"]), []).append(k)
    anchor_presses = 0
    momentum_clock, momentum_at = 1.0, -1e9
    earned = "press_share" in row
    last_press = -1e9; warped_t = 0.0; caught_t = 0.0; presses = 0; catches_n = 0; dist = 0.0; timed = 0
    kick = None
    while t < T and idx < total:
        launched = False   # the frame the sling sets the nose and the command itself: the nose lead stays out of it
        g = gates[idx % n]; ring, ax = g["p"], g["a"]
        if idx != locked:
            locked = idx
            side = 1.0 if dot(sub(pos, ring), ax) >= 0 else -1.0
            leg_from = gates[(idx - 1) % n]["p"] if idx > 0 else pos
            lead = min(lead_r, LEAD_SHARE * norm(sub(leg_from, ring)), LEAD_INSIDE_SHARE * commit_r)
        commit = min(commit_r, lead + 0.5 * R)
        approaching = norm(sub(ring, pos)) > commit
        through = add(ring, mul(unit(sub(ring, pos)), through_r))
        aim = add(ring, mul(ax, side * lead)) if approaching else through
        to = sub(aim, pos)
        desired = unit(to) if norm(to) > 1.0 else list(fwd)
        # AIPilot.UpdateOrbitBreak
        if extending:
            ext_t += dt
            if (norm(to) >= run and ext_t >= 0.6) or ext_t >= 6: extending = False
        elif (dot(desired, fwd) <= 0.5 and L.inside_turning_circle(to, fwd, R)) or orbit.tick(to):
            extending = True; ext_t = 0.0; breaks += 1; orbit.reset()
        # StoatSlingExecutor.AutopilotSling
        if warp:
            if laid and tramp:
                pass   # the AI lets go of the trigger at once: the shell stays up until it is bounced off or passed
            elif laid:
                if not caught:
                    let_go = t - laid_at >= row["give_up"]
                else:
                    off = ang(fwd, to)
                    wide = max(3 * row["cone"], row.get("aim_cone", 0.0))
                    if aimed:
                        # StoatSlingExecutor.AutopilotSling: the exit is set by the entry; let go as the window opens
                        let_go = exit_timing(orbit_s) >= 0.999 or orbit_s >= EXIT_T + EXIT_HALF
                    elif row.get("window_ai"):
                        # hold for the window, then launch whenever the aim can reach the target; past it, let go anyway
                        let_go = (sweet_spot(math.degrees(swept)) >= 0.999 and off <= wide) or math.degrees(swept) >= row["window_ai"]
                    else:
                        let_go = steered_out or off <= row["cone"] or (sweet_spot(math.degrees(swept)) >= 0.999 and off <= wide)
                    steered_out = False
                if let_go:
                    if caught:
                        timing = exit_timing(orbit_s) if aimed else sweet_spot(math.degrees(swept))
                        timed += timing >= 0.999
                        amount = CRUISE * (BOOST_MIN + (BOOST_MAX - BOOST_MIN) * row["hold"]) * (MISS_SHARE + (1 - MISS_SHARE) * timing) * kick_rate
                        if earned:
                            # StoatSlingExecutor.Slingshot: aimed by the stick (toward the AI's aim), nothing before the minimum
                            # sweep, momentum by the timing, the kick under the cap (StoatSlingMath.CappedKick)
                            if aimed:
                                fwd = aimed_exit(entry, last_radial, fwd, to); cmd = list(fwd); launched = True
                            else:
                                reach = min(ang(fwd, to), row["aim_cone"] * ai_stick(ang(fwd, to)))
                                fwd = rotate_toward(fwd, unit(to), math.radians(reach)); cmd = list(fwd); launched = True
                            if math.degrees(swept) < row["min_sweep"]:
                                amount = 0.0
                            else:
                                amount = min(amount, (row["cap"] - 1) * CRUISE * clock)
                                momentum_clock, momentum_at = 1 + (clock - 1) * timing, t
                        if amount > 0: kick = (list(fwd), amount, t)
                    laid = caught = False
            elif row["min_turn"] > 0 and t - last_press >= row["interval"]:
                ring_range = norm(sub(ring, pos))
                f, up = fwd, [0.0, 1.0, 0.0]
                right = unit(cross(up, f)) if abs(dot(up, f)) < 0.999 else [1.0, 0.0, 0.0]
                # StoatSlingExecutor.AutopilotSling's ORBIT ANCHOR: the moon beside this ring; press only where the pair lands
                anchor_state = None
                if moons and row.get("anchor"):
                    near = min(moons, key=lambda m: norm(sub(m["centre"], ring)))
                    if norm(sub(near["centre"], ring)) <= row.get("anchor_reach", 120.0):
                        rel = sub(near["centre"], pos)
                        lz, lx = dot(rel, f), dot(rel, right)
                        ly = dot(rel, unit(cross(f, right)))
                        w = row["anchor"]
                        if lz > CATCH_AHEAD + w: anchor_state = "wait"
                        elif lz >= CATCH_AHEAD - w:
                            ok = CATCH_SIDE - w <= abs(lx) <= CATCH_SIDE + w and abs(ly) <= w
                            anchor_state = ("left" if lx < 0 else "right") if ok else "wait"
                if anchor_state == "wait":
                    pass
                elif anchor_state in ("left", "right"):
                    left = anchor_state == "left"
                    last_press = t; presses += 1; anchor_presses += 1
                    mid = add(pos, mul(f, CATCH_AHEAD))
                    attractor = add(mid, mul(right, -CATCH_SIDE if left else CATCH_SIDE))
                    laid = True; caught = False; orbit_side_left = left
                    radius = orbit_radius(row["hold"]); swept = 0.0; laid_at = t
                off_nose = math.degrees(math.atan2(dot(to, right), dot(to, f)))
                turn = ring_range >= row["min_dist"] and abs(off_nose) >= row["min_turn"]
                warp_press = row["warp_dist"] > 0 and ring_range >= row["warp_dist"]   # StoatSlingMath.WantsWarp
                if anchor_state is None and norm(to) > 1e-3 and (turn or warp_press):
                    left = dot(to, right) < 0
                    last_press = t; presses += 1
                    mid = add(pos, mul(f, CATCH_AHEAD))
                    attractor = add(mid, mul(right, -CATCH_SIDE if left else CATCH_SIDE))
                    laid = True; caught = False; orbit_side_left = left
                    radius = orbit_radius(row["hold"]); swept = 0.0; laid_at = t
                    if tramp:
                        tramp_c = tramp_centre(pos, f, right, left, omega if row.get("predict") else None, CRUISE * clock,
                                               t_line, t_r, t_ahead)
                        # StoatSlingMath.TrampolineAim: the point on the shell's flank the line passes (line x R off the centre,
                        # toward the bounce); the AI steers at it until it touches (StoatSlingExecutor's borrowed steering)
                        tramp_aim = tramp_flank(pos, tramp_c, right, left, t_line, t_r)
        # the flight clock (StoatPathfinderExecutor, StoatDipoleMath.StepBoost): warped while a pair is held, from the press
        if earned:
            # StoatPathfinderExecutor: the press's small warp, the orbit's full one, the launch's momentum - the larger
            held = warp_top * (1 + row.get("bonus", 0.0)) if caught else (1 + (warp_top - 1) * row["press_share"]) if laid else 1.0
            age = t - momentum_at
            mom = 1 + (momentum_clock - 1) * (1 - age / row["momentum_s"]) if 0 <= age < row["momentum_s"] else 1.0
            target = max(held, mom)
            if target > 1.001:
                clock += (target - clock) * (1 - math.exp(-BOOST_RISE * dt)); warped_t += dt
            else:
                clock = 1.0 + (clock - 1.0) * math.exp(-dt / BOOST_FADE)
        elif laid:
            top = warp_top * (1 + row.get("bonus", 0.0) if caught else 1.0)
            clock += (top - clock) * (1 - math.exp(-BOOST_RISE * dt)); warped_t += dt
        else:
            clock = 1.0 + (clock - 1.0) * math.exp(-dt / BOOST_FADE)
        dtf = dt * clock
        v = CRUISE * clock
        # StoatSlingExecutor.StepOrbit
        correction = [0.0, 0.0, 0.0]
        # StoatSlingExecutor.StepTrampoline: touch the shell moving in and the spring turns you; out again moving out, the
        # bounce (the kick and the momentum, every bounce timed); moving away untouched, or its life out, it is passed
        if laid and tramp:
            rad = sub(pos, tramp_c); d = norm(rad); outward = dot(fwd, rad) > 0
            if not caught and d < t_r and not outward:
                caught = True; catches_n += 1; tvel = mul(fwd, v)
            if caught:
                caught_t += dt
                if d < t_r:
                    tvel = tramp_step(tvel, rad, t_r, row.get("soft", TRAMP_SOFT), dt)
                    before = list(fwd); fwd = unit(tvel) if norm(tvel) > 1e-3 else fwd
                    applied = ang(before, fwd); ax2 = cross(before, fwd)
                    if norm(ax2) > 1e-6 and applied > 1e-5: cmd = rotate_about(cmd, unit(ax2), math.radians(applied))
                else:   # out of the shell again: the bounce
                    amount = min(CRUISE * (BOOST_MIN + (BOOST_MAX - BOOST_MIN) * row["hold"]) * kick_rate, (row["cap"] - 1) * CRUISE * clock)
                    momentum_clock, momentum_at = clock, t
                    kick = (list(fwd), amount, t); bounces += 1; timed += 1
                    laid = caught = False
            elif outward or t - laid_at >= TRAMP_LIFE:
                laid = False; tramp_misses += 1
        if laid and not tramp:
            squeeze_r = orbit_radius(row["hold"])
            want = squeeze_r * row.get("inner", 1.0) if caught else squeeze_r
            follow = (row.get("pull", 0.0) or EASE_RATE) if caught else FOLLOW_RATE
            radius += (want - radius) * (1 - math.exp(-follow * dt))
            hull_right = unit(cross([0.0, 1.0, 0.0], fwd)) if abs(fwd[1]) < 0.999 else [1.0, 0.0, 0.0]
            own_side = (dot(sub(attractor, pos), hull_right) < 0) == orbit_side_left       # StoatSlingMath.OnSpinSide
            if not caught and (own_side or not row.get("side")) and catches(pos, fwd, attractor, radius):
                caught = True; catches_n += 1; swept = 0.0; last_radial = sub(pos, attractor); radius = norm(last_radial)
                orbit_s = 0.0; entry = list(fwd)
            if caught:
                caught_t += dt; orbit_s += dt
                to_c = sub(attractor, pos); d = norm(to_c)
                if row.get("steer_out") and d > 1e-3:
                    # StoatSlingMath.SteersOut: the AI's stick turns toward its aim; away from the hole by 90+ deg lets go
                    inward = sub(to_c, mul(fwd, dot(to_c, fwd))); turn_dir = sub(to, mul(fwd, dot(to, fwd)))
                    if norm(inward) > 1e-6 and norm(turn_dir) > 1e-6 and ai_stick(ang(fwd, to)) >= 0.5 and ang(turn_dir, inward) >= 90:
                        steered_out = True
                if d > 1e-3:
                    correction = mul(to_c, (d - radius) * (1 - math.exp(-RADIAL_RATE * dt)) / d)
                step = v / max(1.0, radius) * dt
                u = unit(to_c)
                tangent = sub(fwd, mul(u, dot(fwd, u)))
                if norm(tangent) > 1e-4:
                    tangent = unit(tangent)
                    axis = unit(cross(tangent, u))
                    tangent = rotate_about(tangent, axis, step)
                    miss = ang(fwd, tangent)
                    settle = 1 - math.exp(-SETTLE_RATE * dt)
                    deg = min(miss, max(math.degrees(step), miss * settle))
                    if miss > 1e-4:
                        before = list(fwd)
                        fwd = tangent if deg >= miss else rotate_toward(fwd, tangent, math.radians(deg))
                        applied = ang(before, fwd)
                        ax2 = cross(before, fwd)
                        if norm(ax2) > 1e-6 and applied > 1e-5:
                            cmd = rotate_about(cmd, unit(ax2), math.radians(applied))
                radial = sub(pos, attractor)
                swept += math.radians(ang(last_radial, radial)); last_radial = radial
                if math.degrees(swept) >= MAX_ORBIT:
                    laid = caught = False
        # the stick (AIPilot on the flight clock: the turn scales with the warp)
        steer = add(fwd, mul(desired, -0.35)) if extending else to
        if tramp and laid and not caught and row.get("aim") and tramp_aim is not None:
            if row.get("aim_turn"):
                # the AI grazes as deep as the turn it wants: the angle from its heading to its target picks the line
                hull_r = unit(cross([0.0, 1.0, 0.0], fwd)) if abs(fwd[1]) < 0.999 else [1.0, 0.0, 0.0]
                tramp_aim = tramp_flank(pos, tramp_c, hull_r, orbit_side_left, tramp_line_for(ang(fwd, to), row.get("turn_scale", TRAMP_TURN_SCALE)), t_r)
            steer = sub(tramp_aim, pos)
        if lead_gain > 0 and not (laid and caught) and not launched:
            # the nose lead (AIPilot.NoseLeadGain): judge the error from the COMMAND and steer the command onto the nose
            # turned toward the steer direction by the effective gain times its error, past it (capped) - where the lagging
            # nose is pulled from. On whatever the steer is (the break-off's escape leg, the flank approach), as the game's
            # is; OFF while the field flies the nose (the orbit, the shell: laid and caught) and on the launch frame.
            judge, want = judge_and_want(fwd, cmd, steer, lead_gain, lead_extra)
        else:
            judge, want = fwd, steer
        s = ai_stick(ang(judge, want))
        fwd, cmd = L.turn_with_lag(fwd, cmd, want, math.radians(TURN * s) * dtf, dtf, True)
        shift = [0.0, 0.0, 0.0]
        if kick is not None:
            age = (t - kick[2]) / BOOST_SECONDS
            if age >= 1: kick = None
            else: shift = mul(kick[0], kick[1] * (1 - age) * dt)
        newpos = add(add(add(pos, mul(fwd, v * dt)), correction), shift)
        a0 = dot(sub(pos, ring), ax); a1 = dot(sub(newpos, ring), ax)
        if a0 * a1 <= 0 and a0 != a1:
            k = a0 / (a0 - a1); hit = add(pos, mul(sub(newpos, pos), k))
            if norm(sub(hit, ring)) <= g["r"]:
                idx += 1
            else:
                misses += 1; locked = -1; extending = True; ext_t = 0.0   # re-latch, BreakOffNow
        if grid:
            cell = tuple(int(math.floor(c / 24.0)) for c in newpos)
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    for dz in (-1, 0, 1):
                        for k in grid.get((cell[0] + dx, cell[1] + dy, cell[2] + dz), ()):
                            pr = orrery[1][k]
                            if k not in hits and norm(sub(newpos, pr["p"])) < pr["size"] * 0.5 + 3.0: hits.add(k)
        dist += norm(sub(newpos, pos))
        # the hull's turn rate (rad/s about an axis), eased: what a trampoline laid now predicts the line with
        omega = add(mul(omega, 0.8), mul(cross(prev_fwd, fwd), 0.2 / dt)); prev_fwd = list(fwd)
        pos = newpos; t += dt
    return {"time": round(t, 1) if idx >= total else None, "rings": idx, "misses": misses, "breaks": breaks,
            "warped": round(warped_t / max(t, 1e-6), 2), "caught": round(caught_t / max(t, 1e-6), 2),
            "speed": round(dist / max(t, 1e-6), 1), "presses": presses, "catches": catches_n, "timed": timed,
            "hits": len(hits), "moon_hits": sum(1 for k in hits if orrery[1][k]["size"] < 5.0) if orrery else 0,
            "anchor_presses": anchor_presses, "bounces": bounces, "tramp_misses": tramp_misses}


def score(row, seeds=6, intensities=(1, 2, 3, 4), dt=1 / 30, warp=True, nose_lead=None):
    """Median race seconds per intensity (an unfinished race counts 300 s + 10 per ring left), misses, finishes."""
    out = {}
    for i in intensities:
        rs = [race(course(i, 20260000 + 97 * s + i), row, dt=dt, warp=warp, nose_lead=nose_lead) for s in range(seeds)]
        out[i] = {"median": statistics.median(r["time"] if r["time"] else 300 + 10 * (16 - r["rings"]) for r in rs),
                  "finished": sum(1 for r in rs if r["time"]), "misses": statistics.mean(r["misses"] for r in rs),
                  "speed": statistics.mean(r["speed"] for r in rs), "warped": statistics.mean(r["warped"] for r in rs),
                  "ppm": statistics.mean(60 * r["presses"] / max(1.0, r["time"] or 300.0) for r in rs),
                  "timed": statistics.mean(r["timed"] / max(1, r["catches"]) for r in rs), "n": len(rs),
                  "hit": statistics.mean(r["bounces"] / max(1, r["presses"]) for r in rs)}
    return out


def score_regatta(row, intensities=(1, 2, 3, 4), dt=1 / 30, nose_lead=None):
    """One race per intensity on the shipped Regatta course (three laps), as regatta_balance.py measures it."""
    out = {}
    for i in intensities:
        gates = [{"p": g["p"], "a": unit(g["a"]), "r": g["r"]} for g in CP.regatta_course(i, rails=False)["gates"]]
        r = race(gates, dict(row, **REGATTA_STEER), laps=3, dt=dt, nose_lead=nose_lead)
        out[i] = {"median": r["time"] if r["time"] else 300.0 + 10 * (3 * len(gates) - r["rings"]), "finished": int(bool(r["time"])),
                  "misses": r["misses"], "speed": r["speed"], "warped": r["warped"],
                  "ppm": 60 * r["presses"] / max(1.0, r["time"] or 300.0), "timed": r["timed"] / max(1, r["catches"]), "n": 1}
    return out


def line(name, s):
    cells = " ".join(f"I{i} {v['median']:6.1f}s {v['finished']}/{v['n']} {v['misses']:4.1f}m {v['speed']:4.0f}u/s "
                     f"{v.get('ppm', 0):4.1f}/min {100 * v.get('timed', 0):3.0f}%t" + (f" {100 * v['hit']:3.0f}%hit" if v.get('hit') else "") for i, v in s.items())
    return f"{name:<34} {cells}"


def grid(seeds=4, nose_lead=None):
    rows = []
    for warp_dist in (160.0, 220.0, 280.0, 360.0):
        for min_turn in (7.84, 15.0, 30.0):
            for interval in (0.7, 0.941):
                for min_dist in (123.282, 200.0):
                    for cone in (16.0, 30.0):
                        row = {"hold": 1.0, "min_turn": min_turn, "cone": cone, "give_up": 4.0, "interval": interval,
                               "min_dist": min_dist, "warp_dist": warp_dist}
                        s = score(row, seeds=seeds, nose_lead=nose_lead)
                        loss = sum(math.log(v["median"]) for v in s.values()) / len(s) + 0.05 * sum(v["misses"] for v in s.values()) / len(s)
                        rows.append((loss, row, s))
    rows.sort(key=lambda r: r[0])
    for loss, row, s in rows[:10]:
        print(f"{loss:.3f} {row}")
        print("    " + line("", s))


def score_orrery(row, seeds=8, intensities=(1, 2, 3, 4), dt=1 / 30, nose_lead=None):
    """Slingshot with the Orrery laid (orrery_port.layout): race seconds, prisms hit, moons hit, anchor presses."""
    import orrery_port as O
    out = {}
    for i in intensities:
        rs = []
        for s in range(seeds):
            c = course(i, 20260000 + 97 * s + i)
            rs.append(race(c, row, dt=dt, orrery=O.layout(c, i), nose_lead=nose_lead))
        out[i] = {"median": statistics.median(r["time"] if r["time"] else 300 + 10 * (16 - r["rings"]) for r in rs),
                  "finished": sum(1 for r in rs if r["time"]), "misses": statistics.mean(r["misses"] for r in rs),
                  "speed": statistics.mean(r["speed"] for r in rs), "ppm": statistics.mean(60 * r["presses"] / max(1.0, r["time"] or 300.0) for r in rs),
                  "timed": statistics.mean(r["timed"] / max(1, r["catches"]) for r in rs), "n": len(rs),
                  "hits": statistics.mean(r["hits"] for r in rs), "moon_hits": statistics.mean(r["moon_hits"] for r in rs),
                  "anchor": statistics.mean(r["anchor_presses"] for r in rs)}
    return out


def line_orrery(name, s):
    return f"{name:<30} " + " ".join(f"I{i} {v['median']:5.1f}s {v['finished']}/{v['n']} hit {v['hits']:4.1f} (moon {v['moon_hits']:3.1f}) "
                                      f"anchor {v['anchor']:4.1f} {100 * v['timed']:3.0f}%t" for i, v in s.items())


def grid_earned(seeds=4, nose_lead=None):
    rows = []
    for window_ai in (0, 120.0, 150.0):
        for min_turn in (7.84, 30.0):
            for press_share in (0.2, 0.4):
                for momentum_s in (2.5, 4.0):
                    row = dict(EARNED_ROW, warp_dist=0.0, min_turn=min_turn, press_share=press_share, momentum_s=momentum_s)
                    if window_ai: row["window_ai"] = window_ai
                    sc = score(row, seeds=seeds, nose_lead=nose_lead)
                    loss = sum(math.log(v["median"]) for v in sc.values()) / len(sc) + 0.05 * sum(v["misses"] for v in sc.values()) / len(sc)
                    rows.append((loss, {k: row[k] for k in ("min_turn", "press_share", "momentum_s")} | {"window_ai": window_ai}, sc))
    rows.sort(key=lambda r: r[0])
    for loss, row, sc in rows[:8]:
        print(f"{loss:.3f} {row}")
        print("    " + line("", sc))


def self_test():
    bad = []
    # the geometry: the circle stays clear of the repulsor, a press catches a straight pass at every squeeze
    if abs(orbit_radius(1.0) - 55.0) > 1e-6 or abs(orbit_radius(0.0) - 72.0) > 1e-6: bad.append(f"radius {orbit_radius(1.0)}, {orbit_radius(0.0)}")
    if abs(WARP_TOP - 2.725) > 1e-6: bad.append(f"warp top {WARP_TOP}")
    if abs(sweet_spot(90) - 1) > 1e-9 or sweet_spot(0) > 1e-9 or sweet_spot(180) > 1e-9: bad.append("sweet spot")
    # the course port against SLINGSHOT.md section 2's harness numbers: eight gates, the mouths, a lap near 3700 u
    for i, mouth in zip((1, 2, 3, 4), (48, 40, 32, 26)):
        g = course(i, 20260000 + i)
        lap = sum(norm(sub(g[(k + 1) % 8]["p"], g[k]["p"])) for k in range(8))
        if len(g) != 8 or abs(g[0]["r"] - mouth) > 1e-3 or not 3000 < lap < 4600: bad.append(f"course I{i}: {len(g)} gates, lap {lap:.0f}")
    # negative control: a hull that never presses cruises at 60 u/s, and pressing makes it faster
    wide = [{"p": [0.0, 0.0, 400.0 * (k + 1)], "a": [0.0, 0.0, 1.0], "r": 60.0} for k in range(8)]
    floor = race(wide, HULL_ROW, laps=1, warp=False)
    if not floor["time"] or abs(floor["speed"] - CRUISE) > 3: bad.append(f"the floor is not cruise: {floor}")
    # a press rule that needs a turn never fires dead ahead: that is the hole the warp press closes
    still = race(wide, VARIANT_ROW, laps=1)
    if still["presses"] != 0: bad.append(f"a straight course should not need a turn press: {still}")
    fast = race(wide, dict(VARIANT_ROW, warp_dist=220.0), laps=1)
    if not fast["time"] or fast["time"] >= floor["time"]: bad.append(f"pressing did not beat cruising: {fast} vs {floor}")
    # the nose lead is OFF by default: NoseLead() (gain 0) is the unset knob, and a gain changes a turning race (the knob
    # is live); on the straight course nothing turns, so the lead changes nothing there
    c = course(2, 20260000 + 2)
    today = race(c, ORBIT_ROW, dt=1 / 60)
    if race(c, ORBIT_ROW, dt=1 / 60, nose_lead=NoseLead()) != today: bad.append("NoseLead() (gain 0) is not the unset lead")
    if race(c, ORBIT_ROW, dt=1 / 60, nose_lead=2.5) == today: bad.append("the nose lead at 2.5 changed nothing (the knob is dead)")
    if race(wide, HULL_ROW, laps=1, warp=False, nose_lead=2.5) != floor: bad.append("the lead moved a straight cruise")
    print("stoat_orbit_sim self-test:", "FAIL " + "; ".join(bad) if bad else "ok")
    return 1 if bad else 0


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--grid", action="store_true"); ap.add_argument("--self-test", action="store_true"); ap.add_argument("--seeds", type=int, default=12)
    ap.add_argument("--earned", action="store_true"); ap.add_argument("--grid-earned", action="store_true")
    ap.add_argument("--orrery", action="store_true"); ap.add_argument("--trampoline", action="store_true")
    ap.add_argument("--nose-lead", metavar="G[,EXTRA]", default=None,
                    help="the AI nose lead (AIPilot.NoseLeadGain, stated at the fleet lag; EXTRA = NoseLeadMaxExtraDegrees, 40): "
                         "every table row is printed again with the lead on; --grid / --grid-earned search with it on")
    a = ap.parse_args()
    if a.self_test: sys.exit(self_test())
    lead = NoseLead.from_cli(a.nose_lead)
    if a.grid: grid(nose_lead=lead); sys.exit(0)
    if a.grid_earned: grid_earned(nose_lead=lead); sys.exit(0)

    def put(name, scored, fmt=line):
        """Print a row (scored(nose_lead) -> score dict) and, with --nose-lead, the same row with the lead on."""
        s = scored(None); print(fmt(name, s))
        if lead is not None: print(fmt(f"  + nose lead {lead.gain:g}", scored(lead)))
        return s
    if a.trampoline:
        put("floor: no press, cruise only", lambda nl: score(HULL_ROW, seeds=a.seeds, warp=False, nose_lead=nl))
        put("the aimed exit (shipped 11-03)", lambda nl: score(SHIPPED_ROW, seeds=a.seeds, dt=1 / 60, nose_lead=nl))
        for name, extra in (("TRAMP, laid on the straight line", {}), ("TRAMP, laid on the turning line", {"predict": True}),
                            ("TRAMP, straight line, AI aims at the flank", {"aim": True}),
                            ("TRAMP, turning line, AI aims at the flank", {"predict": True, "aim": True})):
            row = dict(SHIPPED_ROW, aimed=False, tramp=True, **extra)
            put(name, lambda nl, row=row: score(row, seeds=a.seeds, dt=1 / 60, nose_lead=nl))
        for sc_ in (100.0, 160.0, 220.0):
            for mt in (20.0, 35.0, 50.0):
                row = dict(SHIPPED_ROW, aimed=False, tramp=True, aim=True, aim_turn=True, turn_scale=sc_, min_turn=mt)
                put(f"  aims for its turn, x{sc_:.0f}, turn >= {mt:.0f}", lambda nl, row=row: score(row, seeds=a.seeds, dt=1 / 60, nose_lead=nl))
        sys.exit(0)
    if a.orrery:
        print(line_orrery("no Orrery (the shipped AI)", score(SHIPPED_ROW, seeds=a.seeds) and score_orrery(SHIPPED_ROW, seeds=0) or {}) if False else "")
        put("Orrery, the shipped AI", lambda nl: score_orrery(SHIPPED_ROW, seeds=a.seeds, nose_lead=nl), line_orrery)
        for w in (15.0, 22.0, 30.0):
            put(f"Orrery, anchor window {w:.0f} u", lambda nl, w=w: score_orrery(dict(SHIPPED_ROW, anchor=w), seeds=a.seeds, nose_lead=nl), line_orrery)
        sys.exit(0)
    put("floor: no press, cruise only", lambda nl: score(HULL_ROW, seeds=a.seeds, warp=False, nose_lead=nl))
    put("hull's own AI row (30 deg, 3 s)", lambda nl: score(HULL_ROW, seeds=a.seeds, nose_lead=nl))
    put("the learned variant, no warp press", lambda nl: score(VARIANT_ROW, seeds=a.seeds, nose_lead=nl))
    tuned = put("SHIPPED: + warp press at 280 u", lambda nl: score(TUNED_ROW, seeds=a.seeds, dt=1 / 60, nose_lead=nl))
    print("  share of the race with a pair held:", {i: round(v["warped"], 2) for i, v in tuned.items()})
    orbit = put("SHIPPED 11-02: + inner orbit, spin side, steer out", lambda nl: score(ORBIT_ROW, seeds=a.seeds, dt=1 / 60, nose_lead=nl))
    put("Regatta at Time 10, the 11-02 orbit", lambda nl: score_regatta(dict(ORBIT_ROW, warp_top=5.6), dt=1 / 60, nose_lead=nl))
    if a.earned:
        for name, row in (("EARNED, the old AI (press spam)", EARNED_ROW), ("SHIPPED: EARNED, the window AI", SHIPPED_ROW)):
            put(name, lambda nl, row=row: score(row, seeds=a.seeds, dt=1 / 60, nose_lead=nl))
        put("Regatta Time 10, SHIPPED", lambda nl: score_regatta(dict(SHIPPED_ROW, warp_top=5.6), dt=1 / 60, nose_lead=nl))
