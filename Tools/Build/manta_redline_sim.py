#!/usr/bin/env python3
"""Redline raced offline by the AI Manta, flown the way MinigameRedline flies it: the Manta's own Time game, for the AI
plan's time-arcade step (Docs/AI_SYSTEM/AI_PLAN.md section 5; the search is Tools/Compute/evaluators/redline_fit.py).

manta_drive_sim.py is Regatta's model: a point mass on Regatta's steering at full throttle with no nose lag. Redline
differs on all three (measured 2026-11-03), so this one ports, line for line, what an AI Manta does there:

    the course    RedlineCourse.ForIntensity through HeadlongCircuit.Generate (course_port.py, the float32 port), the
                  race's laps read from RedlineCourseSource.DefaultLaps (5), and the Time crystals of
                  RedlineTimeCrystals (crystals_for): both held to the shipped C# by redline_course_harness's
                  redline_parity.jsonl in --self-test
    the gate aim  GateRaceController.ArmRacers' aim provider with the Regatta steering, which RedlineController turns
                  on (AICommitFromApproachPoint, 2026-11-03): commit from the approach point, through the ring's
                  centre, break off and re-attack after a missed mouth. `regatta=False` is the old steering Redline
                  flew before (commit at 420 u, a fly-through point on the axis, no re-attack). RedlineController's
                  TryOverrideAim (a live Time crystal on the way, within aiTimeCrystalSlack; 0 as shipped) is asked first
    the pilot     AIPilot: the stick from the nose-to-target cross product (asin(2 sin^2 e) x sin e, TurnHardIfBehind),
                  roll stick = yaw stick, the SKILL THROTTLE (Lerp(0.2, 0.9, skill) + its creep, skill = intensity x
                  0.25 from ServerPlayerVesselInitializerWithAI; Redline sets no AIThrottleFloor, Regatta's is 1), the
                  orbit break-off (turning-circle test, OrbitDetector, the escape point); the platform's crystal detour
                  never fires (GateRaceController.AIDetoursForCrystals is false, and it reads managed crystals only)
    the triggers  MantaAnalogTurnBoostExecutor.AutopilotTriggers (the Soar bands, MantaSoarAITuningSO), Soar = boost
                  multiplier 1 + 3 x min(LT, RT), Yastri yaw 60 deg/s x (RT - LT) about the hull's up
    the hull      VesselTransformer: roll (0.2 v + 100), yaw (0.2 v + 30), pitch (0.2 v + 50) deg/s about the NOSE's
                  axes onto the commanded rotation; the AI nose follows the command at LERP_AMOUNT 1.5/s; speed lerps
                  1.5/s toward throttle x 180 x Soar x the Time level's BoostSpeedMultiplier (Manta.prefab:
                  DefaultThrottleScaler 180, boostMultiplier 4, RotationThrottleScaler 0.2, Pitch 50, Yaw 30, Roll 100;
                  AIPilot throttle 0.2-0.9, ram off); a Time crystal is taken within its 10 u skimmer + 3.55 u

triggers() MIRRORS MantaAnalogTurnBoostExecutor.AutopilotTriggers, stick() AIPilot's steering and crystals_for()
RedlineTimeCrystals.Place: change both together.

NOT modelled (LIMITS): rivals and their Yastri trails; the Sting and Kabloom; the comeback's element levels; the lobby
difficulty (GateRaceHandicap: Easy-Hard fly a late or misjudged BELIEF of the ring and the genome is the same; Legendary
is this pilot); the spawn ring (a pilot starts 400 u before gate 0 on its axis, at rest, facing it); frame hitches; the
Time level's speed is taken as linear in BoostSpeedMultiplier. The editor is the measurement.

    python3 Tools/Build/manta_redline_sim.py                  # the shipped AI, I1-I4 x 6 courses
    python3 Tools/Build/manta_redline_sim.py --what-ifs       # against the old steering, full throttle, no nose lag
    python3 Tools/Build/manta_redline_sim.py --bands 1,0.63   # one pair of Soar bands
    python3 Tools/Build/manta_redline_sim.py --trace 4 1      # one race (intensity, course seed), a line a second
    python3 Tools/Build/manta_redline_sim.py --self-test
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "Compute", "evaluators"))

# Manta.prefab
CRUISE, BOOST_MULT, ROT_THROTTLE = 180.0, 4.0, 0.2
PITCH, YAW, ROLL = 50.0, 30.0, 100.0
YASTRI_YAW = 60.0                                   # MantaAnalogTurnBoostExecutor.maxYawDegPerSec
THROTTLE_LOW, THROTTLE_HIGH = 0.2, 0.9              # AIPilot defaultThrottleLow / High
CREEP_LOW, CREEP_HIGH = 0.0005, 0.001               # AIPilot throttleIncreaseLow / High, per second
BOOST_BAND, YASTRI_BAND = 0.35, 0.75                # the executor's code defaults (no MantaSoarAITuning_Redline)
TRIGGER_DEADZONE = 0.0                              # see triggers(): the AI's triggers are never in the deadzone band
LERP = 1.5                                          # VesselTransformer.LERP_AMOUNT: speed, and the AI nose
# RedlineController / MinigameRedline.unity's GateRaceController and the platform's constants
REGATTA_STEERING = True                             # RedlineController.AICommitFromApproachPoint
THROTTLE_FLOOR = 0.0                                # RedlineController sets no AIThrottleFloor (Regatta's is 1)
COMMIT, LEAD, THROUGH = 420.0, 480.0, 320.0
LEAD_SHARE_OF_LEG, LEAD_SHARE_OF_COMMIT = 0.4, 0.8
REPO = os.path.dirname(os.path.dirname(HERE))


def _shipped_laps():
    """RedlineCourseSource.DefaultLaps, read from the C# (5 since the 2026-10-26 ~90 s target pass; it was 3)."""
    import re
    path = os.path.join(REPO, "Assets", "_Scripts", "Controller", "Arcade", "Redline", "RedlineCourseSource.cs")
    with open(path, encoding="utf-8") as fh:
        m = re.search(r"DefaultLaps\s*=\s*(\d+)\s*;", fh.read())
    if not m:
        raise RuntimeError(f"RedlineCourseSource.DefaultLaps not found in {path}")
    return int(m.group(1))


LAPS = _shipped_laps()
# Time crystals (RedlineTimeCrystals.cs, ported in crystals_for): one Time level each; on the Manta a Time level is Soar's
# top speed, BoostSpeedMultiplier 1 -> 1.3 across levels 0-10 (the ability map; linear here). A crystal is taken when the
# hull's near-field skimmer (Manta.prefab: Skimmer scale 20 x radius 0.5 = 10 u) touches the crystal (3.55 u,
# ElementalCrystalSetSO.CrystalWorldScale), and grows back RedlineController.timeCrystalRegrowSeconds later.
PER_LAP = (0, 1, 2, 3)
ON_LINE_FRACTION, APEX_ALONG_EXIT, APEX_INSET = 0.5, 0.25, 70.0
TIME_PER_LEVEL, MAX_LEVEL = 0.03, 10
PICKUP = 10.0 + 3.55
REGROW = 5.0
AI_CRYSTAL_SLACK = 0.0                              # RedlineController.aiTimeCrystalSlack (off as shipped)
# AIPilot's orbit break-off
CAPTURE, RUN_SECONDS, BREAK_MIN, BREAK_MAX, CLOSING_COS, AWAY_BIAS = 18.0, 1.5, 0.6, 6.0, 0.5, 0.35
SWEEP, PROGRESS, JUMP = 360.0, 0.9, 1.6
MAX_DISTANCE = 50.0                                 # AIPilot._maxDistance (the stick's denominator floor)
START_BACK = 400.0

LIMITS = ("Redline offline (manta_redline_sim.py): the shipped course and Time crystals, gate aim, AIPilot stick / skill "
          "throttle / orbit break-off, the Soar triggers, the hull's turn law and AI nose lag; one AI, no rivals or trails, "
          "no lobby handicap (Legendary), a fixed start 400 u before gate 0, Time levels linear in BoostSpeedMultiplier")


# ── vectors and quaternions (Unity's formulas: AngleAxis(90, up) * forward = right) ─────────────────────────────

def add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def mul(a, k): return (a[0] * k, a[1] * k, a[2] * k)
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def norm(a): return math.sqrt(dot(a, a))


def unit(a, fallback=(0.0, 0.0, 1.0)):
    n = norm(a)
    return (a[0] / n, a[1] / n, a[2] / n) if n > 1e-9 else fallback


def angle(a, b):
    na, nb = norm(a), norm(b)
    if na < 1e-9 or nb < 1e-9:
        return 0.0
    return math.degrees(math.acos(max(-1.0, min(1.0, dot(a, b) / (na * nb)))))


def q_axis(deg, axis):
    h = math.radians(deg) * 0.5
    s = math.sin(h)
    x, y, z = unit(axis)
    return (math.cos(h), x * s, y * s, z * s)


def q_mul(a, b):
    aw, ax, ay, az = a
    bw, bx, by, bz = b
    return (aw * bw - ax * bx - ay * by - az * bz, aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx, aw * bz + ax * by - ay * bx + az * bw)


def q_rot(q, v):
    w, x, y, z = q
    tx, ty, tz = 2 * (y * v[2] - z * v[1]), 2 * (z * v[0] - x * v[2]), 2 * (x * v[1] - y * v[0])
    return (v[0] + w * tx + y * tz - z * ty, v[1] + w * ty + z * tx - x * tz, v[2] + w * tz + x * ty - y * tx)


def q_inv_rot(q, v):
    return q_rot((q[0], -q[1], -q[2], -q[3]), v)


def q_norm(q):
    n = math.sqrt(sum(c * c for c in q))
    return tuple(c / n for c in q)


def q_slerp(a, b, t):
    """Quaternion.Slerp: t clamped to [0, 1], the shorter arc."""
    t = max(0.0, min(1.0, t))
    d = sum(x * y for x, y in zip(a, b))
    if d < 0.0:
        b, d = tuple(-c for c in b), -d
    if d > 0.9995:
        return q_norm(tuple(x + (y - x) * t for x, y in zip(a, b)))
    th = math.acos(d)
    s = math.sin(th)
    wa, wb = math.sin((1 - t) * th) / s, math.sin(t * th) / s
    return q_norm(tuple(wa * x + wb * y for x, y in zip(a, b)))


def q_look(forward):
    """Quaternion.LookRotation(forward, up): the hull at rest on the grid."""
    f = unit(forward)
    up = (0.0, 1.0, 0.0) if abs(f[1]) < 0.99 else (1.0, 0.0, 0.0)
    r = unit(cross(up, f))
    u = cross(f, r)
    m00, m01, m02 = r[0], u[0], f[0]
    m10, m11, m12 = r[1], u[1], f[1]
    m20, m21, m22 = r[2], u[2], f[2]
    tr = m00 + m11 + m22
    if tr > 0:
        s = math.sqrt(tr + 1.0) * 2
        return q_norm((0.25 * s, (m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s))
    if m00 > m11 and m00 > m22:
        s = math.sqrt(1.0 + m00 - m11 - m22) * 2
        return q_norm(((m21 - m12) / s, 0.25 * s, (m01 + m10) / s, (m02 + m20) / s))
    if m11 > m22:
        s = math.sqrt(1.0 + m11 - m00 - m22) * 2
        return q_norm(((m02 - m20) / s, (m01 + m10) / s, 0.25 * s, (m12 + m21) / s))
    s = math.sqrt(1.0 + m22 - m00 - m11) * 2
    return q_norm(((m10 - m01) / s, (m02 + m20) / s, (m12 + m21) / s, 0.25 * s))


FWD, UP, RIGHT = (0.0, 0.0, 1.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0)


# ── the pieces, each mirroring one C# method ───────────────────────────────────────────────────────────────────

def boost_intent(stick, band):
    """MantaAnalogTurnBoostExecutor.AutopilotBoostIntent."""
    if band <= 0:
        return 0.0
    s = abs(stick)
    if s <= band:
        return 1.0
    return max(0.0, min(1.0, 1 - (s - band) / max(1e-4, 1 - band)))


def triggers(yaw, deflection, band, yband):
    """MantaAnalogTurnBoostExecutor.AutopilotTriggers: (lt, rt)."""
    hold = boost_intent(deflection, band)
    lt = rt = hold
    y = abs(yaw)
    if y <= yband:
        return lt, rt
    excess = max(0.0, min(1.0, (y - yband) / max(1e-4, 1 - yband)))
    outer = min(1.0, hold + excess)
    if yaw > 0:
        rt = outer
    else:
        lt = outer
    return lt, rt


def stick(q, desired, dist_sqr, judged=None):
    """AIPilot's stick (XSum, YSum): the cross product of nose and desired direction in the hull's frame, scaled by
    asin(|cross|^2 x 100 / min(d^2, _maxDistance)) degrees, full and on its own axis when the target is behind.
    With the nose lead (AIPilot.NoseLeadGain, Tools/Build/ai_nose_lead.py) the error is judged from the COMMAND's
    forward instead of the nose: pass it as judged (a unit vector) and the lead's wanted direction as desired; the
    cross product still lands in the NOSE's frame (transform.InverseTransformDirection) and the stick still turns the
    command about the nose's axes, as the game does. judged None is today's steering, judged from the nose."""
    f = q_rot(q, FWD) if judged is None else judged
    local = q_inv_rot(q, cross(f, desired))
    a = math.degrees(math.asin(max(-1.0, min(1.0, dot(local, local) * 100.0 / min(dist_sqr, MAX_DISTANCE)))))
    if dot(f, desired) < 0.0:                                  # TurnHardIfBehind
        local = unit(local, (0.0, 1.0, 0.0))
        a = 90.0
    return max(-1.0, min(1.0, a * local[1])), max(-1.0, min(1.0, a * local[0]))


def inside_turning_circle(to, heading, r):
    """PursuitReachability.IsInsideTurningCircle with AIPilot's 18 u capture."""
    d = norm(to)
    if r <= 0 or d < 1e-6:
        return False
    f = unit(heading)
    lat = sub(to, mul(f, dot(to, f)))
    if norm(lat) < 1e-6:
        return False
    centre = mul(unit(lat), r)
    return norm(sub(to, centre)) < max(0.0, r - CAPTURE)


class Orbit:
    """PursuitReachability.OrbitDetector."""

    def __init__(self):
        self.reset()

    def reset(self):
        self.has, self.swept = False, 0.0

    def tick(self, to):
        d = norm(to)
        if d < 1e-6:
            self.reset()
            return False
        b = mul(to, 1.0 / d)
        if not self.has:
            self.has, self.last, self.best, self.last_d, self.swept = True, b, d, d, 0.0
            return False
        if d > self.last_d * JUMP:
            self.last, self.best, self.last_d, self.swept = b, d, d, 0.0
            return False
        self.swept += angle(self.last, b)
        self.last, self.last_d = b, d
        if d < self.best * PROGRESS:
            self.best, self.swept = d, 0.0
            return False
        return self.swept >= SWEEP


def corner_radius(prev, p, nxt):
    """RaceCourseGeometry.CornerRadius: half the shorter leg over tan(half the turn)."""
    inbound, outbound = sub(p, prev), sub(nxt, p)
    turn = angle(inbound, outbound)
    if turn < 1e-4:
        return float("inf")
    return min(norm(inbound), norm(outbound)) * 0.5 / math.tan(math.radians(turn * 0.5))


def crystals_for(course, intensity):
    """RedlineTimeCrystals.Place, line for line: the lap's Time crystals, tightest corner first."""
    gates = [tuple(g["p"]) for g in course["gates"]]
    n = len(gates)
    count = min(PER_LAP[max(1, min(4, int(intensity))) - 1], n) if n >= 3 else 0
    if count <= 0:
        return []
    radius = [corner_radius(gates[(i - 1) % n], gates[i], gates[(i + 1) % n]) for i in range(n)]
    order = sorted(range(n), key=lambda i: (radius[i], i))
    out = []
    for c in order[:count]:
        prev, at, nxt = gates[(c - 1) % n], gates[c], gates[(c + 1) % n]
        if intensity >= 4:
            exit_ = sub(nxt, at)
            along = add(at, mul(exit_, APEX_ALONG_EXIT))
            inside = sub(mul(add(prev, nxt), 0.5), at)
            d = unit(exit_)
            inside = sub(inside, mul(d, dot(inside, d)))
            out.append(add(along, mul(unit(inside), APEX_INSET)) if dot(inside, inside) > 1e-6 else along)
        else:
            out.append(add(prev, mul(sub(at, prev), ON_LINE_FRACTION)))
    return out


def seg_point(a, b, p):
    ab = sub(b, a)
    t = max(0.0, min(1.0, dot(sub(p, a), ab) / dot(ab, ab))) if dot(ab, ab) > 1e-12 else 0.0
    return norm(sub(p, add(a, mul(ab, t))))


def plane_crossing(prev, cur, centre, axis, radius):
    """RaceGateRing.PlaneCrossing: 0 none, 1 inside the mouth, -1 outside it."""
    a0, a1 = dot(sub(prev, centre), axis), dot(sub(cur, centre), axis)
    if a0 * a1 > 0 or abs(a0 - a1) < 1e-9:
        return 0
    hit = add(prev, mul(sub(cur, prev), a0 / (a0 - a1)))
    return 1 if dot(sub(hit, centre), sub(hit, centre)) <= radius * radius else -1


# ── one race ────────────────────────────────────────────────────────────────────────────────────────────────────

def crystal_aim(pos, heading, ring, field, back_at, slack):
    """RedlineController.TryOverrideAim: the live Time crystal ahead of the nose that costs least on the way to the next
    ring, if that costs no more than `slack` over flying straight there; else None."""
    if slack <= 0:
        return None
    direct = norm(sub(ring, pos))
    best, aim = float("inf"), None
    for c, p in enumerate(field):
        if back_at[c] >= 0 or dot(sub(p, pos), heading) <= 0:
            continue
        cost = norm(sub(p, pos)) + norm(sub(ring, p))
        if cost > direct + slack or cost >= best:
            continue
        best, aim = cost, p
    return aim


def race(course, intensity, bands=None, regatta=REGATTA_STEERING, throttle_floor=THROTTLE_FLOOR, nose_lag=True,
         limit=900.0, dt=1 / 60, laps=LAPS, crystals=True, crystal_slack=AI_CRYSTAL_SLACK, trace=None, nose_rate=None):
    """One AI Manta flies `laps` laps of `course` ({gates: [{p, a, r}]}), steering for a Time crystal on its way when it
    costs under `crystal_slack` u (RedlineController.TryOverrideAim; `crystals`: True = RedlineTimeCrystals' field,
    False = none, or a list of positions). Returns {time (None = not finished), rings, of, top, mean_speed, soar (mean
    boost), misses, breakoffs, level (Time level at the end)}.
    `nose_rate` is VesselTransformer.AINoseFollowPerSecond, the rate a mode may set for an AI's nose (Switchback's lag by
    difficulty): None = unset (the fleet lag, LERP 1.5/s), 0 = no lag (the nose IS the command, as a human's), > 0 = that
    rate per second, applied as Mathf.Min(1, rate x dt). `nose_lag=False` is the same as `nose_rate=0`."""
    band, yband = bands or (BOOST_BAND, YASTRI_BAND)
    follow = (LERP if nose_rate is None or nose_rate < 0 else float(nose_rate)) if nose_lag else 0.0
    follow_step = min(1.0, follow * dt) if follow > 0 else 1.0
    gates = [(tuple(g["p"]), unit(tuple(g["a"])), float(g["r"])) for g in course["gates"]]
    n = len(gates)
    total = n * max(1, laps)
    field = crystals_for(course, intensity) if crystals is True else list(crystals or [])
    back_at = [-1.0] * len(field)                                   # when a taken crystal is back (-1 = standing)
    level = 0
    skill = max(0.0, min(1.0, intensity * 0.25))
    throttle = THROTTLE_LOW + (THROTTLE_HIGH - THROTTLE_LOW) * skill
    creep = CREEP_LOW + (CREEP_HIGH - CREEP_LOW) * skill

    p0, a0, _ = gates[0]
    pos = sub(p0, mul(a0, START_BACK))
    q = q_look(a0)
    cmd = q
    v, t = 0.0, 0.0
    idx, locked, side, lead = 0, -1, 1.0, LEAD
    extending, ext_t = False, 0.0
    orbit = Orbit()
    prev = None
    top = soar_sum = speed_sum = 0.0
    frames = misses = breakoffs = 0
    next_trace = 0.0

    while t < limit and idx < total:
        ring, ax, rad = gates[idx % n]

        # GateRaceController's aim provider: the mode's override first (RedlineController.TryOverrideAim)
        aim = crystal_aim(pos, q_rot(q, FWD), ring, field, back_at, crystal_slack) if field else None
        if aim is None and regatta and prev is not None and plane_crossing(prev, pos, ring, ax, rad) == -1:
            locked = -1                                             # re-latch the side, and AIPilot.BreakOffNow
            breakoffs += not extending
            extending, ext_t = True, 0.0
            orbit.reset()
        if idx != locked:
            locked = idx
            side = 1.0 if dot(sub(pos, ring), ax) >= 0 else -1.0
            leg_from = gates[(idx - 1) % n][0] if idx > 0 else pos
            lead = min(LEAD, LEAD_SHARE_OF_LEG * norm(sub(leg_from, ring)))
            if regatta:
                lead = min(lead, LEAD_SHARE_OF_COMMIT * COMMIT)
        rate = v * ROT_THROTTLE + min(PITCH, YAW)                  # VesselTransformer.MaxTurnRateDegreesPerSecond
        r_min = v / math.radians(rate) if v > 0 else 0.0
        commit = min(COMMIT, lead + 0.5 * r_min) if regatta else COMMIT
        to_ring = sub(ring, pos)
        approaching = dot(to_ring, to_ring) > commit * commit
        if aim is not None:
            target = aim
        elif approaching:
            target = add(ring, mul(ax, side * lead))
        elif regatta and dot(to_ring, to_ring) > 1.0:
            target = add(ring, mul(unit(to_ring), THROUGH))
        else:
            target = sub(ring, mul(ax, side * THROUGH))

        # AIPilot: the orbit break-off, then the stick
        to = sub(target, pos)
        heading = q_rot(q, FWD)
        run = max(max(0.0, 2 * r_min - CAPTURE), abs(v) * RUN_SECONDS)
        if extending:
            ext_t += dt
            if (norm(to) >= run and ext_t >= BREAK_MIN) or ext_t >= BREAK_MAX:
                extending, ext_t = False, 0.0
                orbit.reset()
        else:
            closing = dot(unit(to), heading) > CLOSING_COS
            unreachable = not closing and inside_turning_circle(to, heading, r_min)
            orbiting = orbit.tick(to)
            if unreachable or orbiting:
                extending, ext_t = True, 0.0
                orbit.reset()
                breakoffs += 1
        if extending:
            escape = unit(add(heading, mul(unit(to), -AWAY_BIAS)), heading)
            steer = add(pos, mul(escape, run * 2 if run > 1 else MAX_DISTANCE))
        else:
            steer = target
        dist = sub(steer, pos)
        dsq = dot(dist, dist)
        xs, ys = stick(q, unit(dist), dsq) if dsq > 1e-12 else (0.0, 0.0)
        yd = xs                                                     # the roll stick (no belly target in Redline)
        thr = max(0.0, min(1.0, max(throttle, throttle_floor)))
        throttle += creep * dt

        # MantaAnalogTurnBoostExecutor: the triggers, the Yastri yaw, the Soar
        lt, rt = triggers(xs, max(abs(xs), abs(ys)), band, yband)
        boost = min(lt, rt)
        nose_up = q_rot(q, UP)
        if abs(rt - lt) > 0.001:
            cmd = q_mul(q_axis((rt - lt) * YASTRI_YAW * dt, nose_up), cmd)
        # VesselTransformer.CurrentBoostAmount: the Soar multiplier x the Time level's BoostSpeedMultiplier while boosting
        mult = (1 + (BOOST_MULT - 1) * boost) * (1 + TIME_PER_LEVEL * level) if boost > 0.01 else 1.0

        # VesselTransformer.RotateShip: roll, yaw, pitch about the nose's axes onto the command; the nose follows
        k = v * ROT_THROTTLE
        cmd = q_mul(q_axis(yd * (k + ROLL) * dt, q_rot(q, FWD)), cmd)
        cmd = q_mul(q_axis(xs * (k + YAW) * dt, nose_up), cmd)
        cmd = q_mul(q_axis(ys * (k + PITCH) * dt, q_rot(q, RIGHT)), cmd)
        q = q_slerp(q, cmd, follow_step) if follow_step < 1.0 else cmd

        # MoveShipScalar: speed lerps toward throttle x scaler x boost (+ the minimum, 0), along the nose
        v += (thr * CRUISE * mult - v) * LERP * dt
        newpos = add(pos, mul(q_rot(q, FWD), v * dt))
        crossing = plane_crossing(pos, newpos, ring, ax, rad)
        if crossing == 1:
            idx += 1
        elif crossing == -1:
            misses += 1
        for c, p in enumerate(field):
            if back_at[c] < 0 and seg_point(pos, newpos, p) <= PICKUP:
                back_at[c] = t + REGROW
                level = min(MAX_LEVEL, level + 1)
            elif 0 <= back_at[c] <= t:
                back_at[c] = -1.0
        prev, pos = pos, newpos
        top = max(top, v)
        soar_sum += boost
        speed_sum += v
        frames += 1
        t += dt
        if trace is not None and t >= next_trace:
            next_trace += 1.0
            trace.append(f"{t:6.1f}s ring {idx:2d}/{total} v {v:5.0f} thr {thr:.2f} soar {boost:.2f} stick {xs:+.2f},{ys:+.2f} "
                         f"{'APPROACH' if approaching else 'through '} {'BREAK' if extending else ''} d {norm(to_ring):5.0f} "
                         f"Time {level}")
    return {"time": round(t, 3) if idx >= total else None, "rings": idx, "of": total, "top": round(top),
            "mean_speed": round(speed_sum / max(1, frames), 1), "soar": round(soar_sum / max(1, frames), 3),
            "misses": misses, "breakoffs": breakoffs, "level": level}


# ── courses ─────────────────────────────────────────────────────────────────────────────────────────────────────

def course_for(intensity, seed):
    """One Redline circuit (course_port's float32 HeadlongCircuit with RedlineCourse.ForIntensity's settings)."""
    import course_port as cp
    return cp.course(cp.redline_settings(intensity), cp.seed_for_intensity(20260915 + 104729 * int(seed), intensity),
                     rails=False)


def table(label, rows):
    fin = sorted(r["time"] for r in rows if r["time"] is not None)
    med = f"{fin[len(fin) // 2]:6.1f}" if fin else "     -"
    return (f"{label:<34} {len(fin):2d}/{len(rows)}  median {med} s  top {max(r['top'] for r in rows):4d}  "
            f"mean v {sum(r['mean_speed'] for r in rows) / len(rows):5.0f}  soar {sum(r['soar'] for r in rows) / len(rows):.2f}  "
            f"misses {sum(r['misses'] for r in rows):3d}  breakoffs {sum(r['breakoffs'] for r in rows):3d}  "
            f"Time {sum(r['level'] for r in rows) / len(rows):.1f}")


def self_test():
    bad = []
    # 1. triggers() is the C# shape (MantaAutopilotDriveTests): straight = both full; a full yaw stick at the default bands
    #    is one trigger flat and the other released; inside the Yastri band the net is zero; band 0 never boosts.
    if triggers(0.0, 0.0, 0.35, 0.75) != (1.0, 1.0):
        bad.append("triggers: a straight stick does not bury both triggers")
    if triggers(1.0, 1.0, 0.35, 0.75) != (0.0, 1.0) or triggers(-1.0, 1.0, 0.35, 0.75) != (1.0, 0.0):
        bad.append("triggers: a full yaw stick is not one trigger flat and the other released")
    lt, rt = triggers(0.6, 0.6, 0.35, 0.75)
    if abs(rt - lt) > 1e-9 or abs(lt - (1 - 0.25 / 0.65)) > 1e-6:
        bad.append(f"triggers: inside the Yastri band ({lt}, {rt})")
    if triggers(0.0, 0.0, 0.0, 0.75) != (0.0, 0.0):
        bad.append("triggers: a 0 band boosts")
    # 2. the stick: dead ahead none, 90 deg right a full yaw, above a pitch up (negative YSum), behind full
    q = q_look(FWD)
    if stick(q, FWD, 1e6, judged=q_rot(q, FWD)) != stick(q, FWD, 1e6) or stick(q, RIGHT, 1e6, judged=q_rot(q, FWD)) != stick(q, RIGHT, 1e6):
        bad.append("stick judged from the nose's own forward is today's stick")
    if stick(q, RIGHT, 1e6, judged=RIGHT) != (0.0, 0.0) or stick(q, FWD, 1e6, judged=RIGHT)[0] != -1.0:
        bad.append("stick judged from a command that already points right: no error, and FWD reads as left of it")
    if stick(q, FWD, 1e6) != (0.0, 0.0) or stick(q, RIGHT, 1e6)[0] != 1.0 or stick(q, UP, 1e6)[1] != -1.0:
        bad.append("stick: the AIPilot cross-product signs are wrong")
    # 3. the shipped AI finishes every course at I1 and I4, and the same race twice is the same race
    for i in (1, 4):
        for s in (1, 2):
            r = race(course_for(i, s), i)
            if r["time"] is None:
                bad.append(f"the shipped AI did not finish I{i} course {s}: {r}")
    a, b = race(course_for(3, 1), 3), race(course_for(3, 1), 3)
    if a != b:
        bad.append("not deterministic")
    # 4. negative controls: a Manta that never Soars is slower, and full throttle is faster than the I1 skill throttle
    c = course_for(2, 1)
    shipped, never = race(c, 2), race(c, 2, bands=(0.0, 1.0))
    if not (shipped["time"] and (never["time"] is None or never["time"] > shipped["time"] * 1.2)):
        bad.append(f"a Manta that never Soars was not slower ({never['time']} vs {shipped['time']})")
    c = course_for(1, 1)
    if not race(c, 1, throttle_floor=1.0)["time"] < race(c, 1)["time"]:
        bad.append("full throttle was not faster than the I1 skill throttle")
    # 5. why Redline flies the Regatta steering: the old steering does not finish an I3 course the shipped AI does
    c = course_for(3, 1)
    old, now = race(c, 3, regatta=False), race(c, 3)
    if now["time"] is None or (old["time"] is not None and old["time"] < now["time"] * 1.5):
        bad.append(f"the old steering raced I3 course 1 about as well as the Regatta steering ({old['time']} vs {now['time']})")
    # 6. parity with the SHIPPED C# (Tools/Build/redline_course_harness/redline_parity.jsonl, `run.sh --parity`): the
    #    course port lays the same gates, and crystals_for places the same Time crystals on them
    import json
    import course_port as cp
    parity = os.path.join(HERE, "redline_course_harness", "redline_parity.jsonl")
    with open(parity, encoding="utf-8") as fh:
        rows = [json.loads(line) for line in fh if line.strip()]
    if not rows:
        bad.append(f"no parity rows in {parity}")
    for row in rows:
        i, seed = row["intensity"], row["seed"]
        ours = cp.course(cp.redline_settings(i), seed, rails=False)
        off = max(norm(sub(tuple(a["p"]), tuple(b["p"]))) for a, b in zip(ours["gates"], row["gates"]))
        if len(ours["gates"]) != len(row["gates"]) or off > 1.0:
            bad.append(f"course_port: I{i} seed {seed} gates are {off:.2f} u off the C# (re-port RedlineCourse.ForIntensity)")
        placed = crystals_for({"gates": row["gates"]}, i)
        if len(placed) != len(row["crystals"]) or any(norm(sub(p, tuple(c))) > 0.05 for p, c in zip(placed, row["crystals"])):
            bad.append(f"crystals_for: I{i} seed {seed} does not place RedlineTimeCrystals' crystals {placed} vs {row['crystals']}")
    # 7. a Time crystal is a Time level: an AI that steers for them (RedlineController.aiTimeCrystalSlack 150) gains
    #    levels, one with no field none, and the shipped AI (slack 0) takes only what its line crosses
    c = course_for(3, 2)
    with_field, without = race(c, 3, crystal_slack=150.0), race(c, 3, crystals=False)
    if not (with_field["level"] > 0 and without["level"] == 0):
        bad.append(f"the Time crystals fed no level ({with_field['level']} vs {without['level']})")
    # 8. the AI nose rate (VesselTransformer.AINoseFollowPerSecond): unset and 1.5 are the fleet lag, 0 is no lag (the
    #    same race as nose_lag=False), and a different rate is a different race (the negative control)
    c = course_for(1, 1)
    fleet = race(c, 1)
    if race(c, 1, nose_rate=1.5) != fleet or race(c, 1, nose_rate=-1) != fleet:
        bad.append("nose_rate 1.5 / -1 is not the fleet lag")
    if race(c, 1, nose_rate=0) != race(c, 1, nose_lag=False):
        bad.append("nose_rate 0 is not the no-lag race")
    if race(c, 1, nose_rate=4.0) == fleet:
        bad.append("nose_rate 4 raced the fleet lag's race (the rate is not applied)")
    print("manta_redline_sim self-test: " + ("ok" if not bad else "FAILED\n  " + "\n  ".join(bad)))
    return 0 if not bad else 1


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--trace" in argv:
        k = argv.index("--trace")
        i, s = int(argv[k + 1]), int(argv[k + 2])
        log = []
        r = race(course_for(i, s), i, regatta="--old-steering" not in argv,
                 throttle_floor=1.0 if "--floor" in argv else THROTTLE_FLOOR, trace=log)
        print("\n".join(log))
        print(r)
        return 0
    bands = None
    if "--bands" in argv:
        bands = tuple(float(x) for x in argv[argv.index("--bands") + 1].split(","))
    seeds = range(1, 1 + (int(argv[argv.index("--seeds") + 1]) if "--seeds" in argv else 6))
    variants = [("shipped (Regatta steering, skill thr)", {})]
    if "--what-ifs" in argv:
        variants += [("old steering (before 2026-11-03)", {"regatta": False}),
                     ("full throttle (AIThrottleFloor 1)", {"throttle_floor": 1.0}),
                     ("no nose lag", {"nose_lag": False})]
    print(f"Redline offline, the AI Manta, {len(seeds)} courses an intensity, bands {bands or (BOOST_BAND, YASTRI_BAND)}")
    for i in (1, 2, 3, 4):
        courses = [course_for(i, s) for s in seeds]
        for label, kw in variants:
            print(f"I{i} " + table(label, [race(c, i, bands=bands, **kw) for c in courses]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
