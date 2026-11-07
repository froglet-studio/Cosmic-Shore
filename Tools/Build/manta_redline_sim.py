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

THE AI NOSE LEAD (AIPilot.NoseLeadGain, PR #1786; Tools/Build/ai_nose_lead.py is the one shared piece) is race()'s
`nose_lead` knob: None (OFF, today's steering, so every number above and every caller is as it was), a gain, a
{"gain", "max_extra_deg"} dict or a NoseLead. With a gain the pilot judges its stick error from the COMMAND's forward
and wants the NOSE turned toward the aim by the effective gain times its error, past the aim when that gain is over 1
(at most max_extra_deg past it, never past 170 deg: ai_nose_lead.slerp_beyond = AIPilot.NoseLeadDirection); the cross
product still lands in the nose's frame and the stick still turns the command about the nose's axes (stick(judged=)).
The effective gain is AIPilot.EffectiveNoseLeadGain computed ONCE a race from THIS race's nose rate (`follow`, the
fleet 1.5/s or `nose_rate`): max(1, gain x 1.5 / rate), and 0 on a nose with no lag (nose_lag=False, nose_rate=0, or
a rate that closes the gap in one step), so a lag-free race is the same race with the lead on or off. Redline has no
drift, rail or shell mode; the orbit break-off keeps the lead (its stick is still computed, aimed at the escape
point). Redline sets no NoseLeadGain; Regatta carries it per hull on RaceLineStyleSO (shipped 0 until the AI owner's
yes), and `--nose-lead G` prints the lead-on rows beside the shipped ones.
MEASURED 2026-11-07 (`--nose-lead 2.5 --seeds 2`, a gain 1-3 x max_extra 10-40 deg scan, self-test check 11): the lead
does NOT work on the Manta here as the game applies it. At I1, where the lag costs today's Manta 70-80 s a race (course
2: 188.8 s, 107.5 s with no lag; a lagged nose never holds the straight stick the Soar wants), judging from the command
helps: gain 1 finishes every I1 course in 114-128 s against today's 179-198 s, and 2.5 finishes courses 2 and 6 in
121-127 s; but the pull past the aim buys nothing (gain 1 beats 2.5 wherever both finish) and 2.5 does not finish I1
courses 1, 3, 4 and 5. From I2 up the lead TRAPS the hull: at I4 every gain from 1 to 3 at every max_extra from 10 to 40
deg passes 1-8 of 40 rings in 900 s (today: 40/40 in 400-430 s with 32-33 missed mouths). Read off the race: the roll
stick is the yaw stick (AIPilot: YDiff = XSum without a belly target) and turns the command about the NOSE's forward.
Judged from the nose the roll never moves the nose's own forward; but a command the lead places 40-90 deg from the nose
is swung round the nose's forward by it, the nose chases a command whirling about a body-fixed axis (the gap settles at
~86 deg) and the hull corkscrews away from the course with the stick pinned full. A diagnostic copy with the roll stick
zeroed while the lead is on finished every race at 2.5 (I4 courses 1-2: 533 s / 19 misses and 272 s / 10 misses against
today's 428.6 / 33 and 400.65 / 32), so the lead would need its roll decoupled from the judged stick before it can help
a rolling hull. That is AIPilot's roll rule to change, not this model's: the port stays as the game is.

NOT modelled (LIMITS): rivals and their Yastri trails; the Sting and Kabloom; the comeback's element levels; the lobby
difficulty (GateRaceHandicap: Easy-Hard fly a late or misjudged BELIEF of the ring and the genome is the same; Legendary
is this pilot); the spawn ring (a pilot starts 400 u before gate 0 on its axis, at rest, facing it); frame hitches; the
Time level's speed is taken as linear in BoostSpeedMultiplier. The editor is the measurement.

    python3 Tools/Build/manta_redline_sim.py                  # the shipped AI, I1-I4 x 6 courses
    python3 Tools/Build/manta_redline_sim.py --what-ifs       # against the old steering, full throttle, no nose lag,
                                                              # and the AI nose lead at the Scarab reference's 2.5
    python3 Tools/Build/manta_redline_sim.py --nose-lead 2.5  # the shipped rows and the same races with the nose lead on
                                                              # (a gain, or gain,max_extra_deg); --trace takes it too
    python3 Tools/Build/manta_redline_sim.py --bands 1,0.63   # one pair of Soar bands
    python3 Tools/Build/manta_redline_sim.py --trace 4 1      # one race (intensity, course seed), a line a second
    python3 Tools/Build/manta_redline_sim.py --self-test
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "Compute", "evaluators"))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
from ai_nose_lead import NoseLead, slerp_beyond            # the AI nose lead (AIPilot.NoseLeadGain), one shared piece

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
         limit=900.0, dt=1 / 60, laps=LAPS, crystals=True, crystal_slack=AI_CRYSTAL_SLACK, trace=None, nose_rate=None,
         nose_lead=None, lead_roll="judged"):
    """One AI Manta flies `laps` laps of `course` ({gates: [{p, a, r}]}), steering for a Time crystal on its way when it
    costs under `crystal_slack` u (RedlineController.TryOverrideAim; `crystals`: True = RedlineTimeCrystals' field,
    False = none, or a list of positions). Returns {time (None = not finished), rings, of, top, mean_speed, soar (mean
    boost), misses, breakoffs, level (Time level at the end)}.
    `nose_rate` is VesselTransformer.AINoseFollowPerSecond, the rate a mode may set for an AI's nose (Switchback's lag by
    difficulty): None = unset (the fleet lag, LERP 1.5/s), 0 = no lag (the nose IS the command, as a human's), > 0 = that
    rate per second, applied as Mathf.Min(1, rate x dt). `nose_lag=False` is the same as `nose_rate=0`.
    `nose_lead` is AIPilot.NoseLeadGain (ai_nose_lead.NoseLead.from_config: None = off, a gain, {"gain", "max_extra_deg"}
    or a NoseLead), stated at the fleet lag and scaled to this race's nose rate once; 0 with no nose lag.
    `lead_roll` is a WHAT-IF for AIPilot's roll rule while the lead is on (no effect with the lead off): "judged" (the
    game today: the roll stick is the lead's judged yaw stick, which corkscrews a rolling hull), "nose" (the roll stick
    is today's lead-off yaw stick, judged from the nose toward the aim) or "off" (no roll stick while leading)."""
    if lead_roll not in ("judged", "nose", "off"):
        raise ValueError(f"lead_roll must be judged, nose or off, not {lead_roll!r}")
    band, yband = bands or (BOOST_BAND, YASTRI_BAND)
    follow = (LERP if nose_rate is None or nose_rate < 0 else float(nose_rate)) if nose_lag else 0.0
    follow_step = min(1.0, follow * dt) if follow > 0 else 1.0
    lead_knob = NoseLead.from_config(nose_lead)
    # AIPilot.EffectiveNoseLeadGain, once a race, from THIS race's nose rate (`follow`): max(1, gain x 1.5 / rate), and
    # 0 when the nose has no lag (nose_lag=False, nose_rate=0, or a rate that closes the whole gap in one step)
    g_lead = lead_knob.effective(follow) if follow_step < 1.0 else 0.0
    lead_extra = lead_knob.max_extra_deg
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
        if dsq <= 1e-12:
            xs, ys = 0.0, 0.0
        elif g_lead > 0.0:
            # the nose lead (AIPilot.TryNoseLead): want the NOSE turned toward the aim by g_lead times its error, past it
            # (NoseLeadDirection), and judge the error from the COMMAND's forward; the stick's frame, the triggers and
            # the command update below are untouched, and the break-off's escape steer takes the lead like any aim
            want = tuple(slerp_beyond(q_rot(q, FWD), unit(dist), g_lead, lead_extra))
            xs, ys = stick(q, want, dsq, judged=unit(q_rot(cmd, FWD)))
        else:
            xs, ys = stick(q, unit(dist), dsq)
        yd = xs                                                     # the roll stick (no belly target in Redline)
        if g_lead > 0.0 and lead_roll != "judged" and dsq > 1e-12:  # the what-if roll rules (see race's docstring)
            yd = stick(q, unit(dist), dsq)[0] if lead_roll == "nose" else 0.0
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
    # 9. the AI nose lead (AIPilot.NoseLeadGain, ai_nose_lead.py) is OFF unless asked: unset, NoseLead(), 0, {}, {"gain": 0}
    #    and False are today's race, and today's race is the recorded baseline (`--seeds 2`, 2026-11-07) to the digit. A
    #    port that changes the model re-measures and updates these eight (the table's numbers are their aggregates).
    baseline = {
        (1, 1): {"time": 180.25, "rings": 40, "of": 40, "top": 268, "mean_speed": 146.6, "soar": 0.301, "misses": 0, "breakoffs": 0, "level": 0},
        (1, 2): {"time": 188.817, "rings": 40, "of": 40, "top": 264, "mean_speed": 139.2, "soar": 0.266, "misses": 0, "breakoffs": 0, "level": 0},
        (2, 1): {"time": 158.667, "rings": 40, "of": 40, "top": 364, "mean_speed": 178.7, "soar": 0.207, "misses": 0, "breakoffs": 3, "level": 1},
        (2, 2): {"time": 174.583, "rings": 40, "of": 40, "top": 390, "mean_speed": 176.6, "soar": 0.202, "misses": 0, "breakoffs": 3, "level": 0},
        (3, 1): {"time": 179.017, "rings": 40, "of": 40, "top": 454, "mean_speed": 194.5, "soar": 0.117, "misses": 0, "breakoffs": 6, "level": 0},
        (3, 2): {"time": 172.45, "rings": 40, "of": 40, "top": 530, "mean_speed": 202.3, "soar": 0.138, "misses": 3, "breakoffs": 7, "level": 0},
        (4, 1): {"time": 428.6, "rings": 40, "of": 40, "top": 701, "mean_speed": 302.8, "soar": 0.235, "misses": 33, "breakoffs": 48, "level": 0},
        (4, 2): {"time": 400.65, "rings": 40, "of": 40, "top": 701, "mean_speed": 291.2, "soar": 0.213, "misses": 32, "breakoffs": 48, "level": 0},
    }
    for (i, s), want in baseline.items():
        got = race(course_for(i, s), i)
        if got != want:
            bad.append(f"I{i} course {s} drifted from the 2026-11-07 baseline (re-measure): {got} vs {want}")
    c = course_for(1, 1)
    fleet = baseline[(1, 1)]
    for form in (NoseLead(), 0, {}, {"gain": 0}, False):
        if race(c, 1, nose_lead=form) != fleet:
            bad.append(f"nose_lead={form!r} is not today's race")
    # 10. the lead is applied as AIPilot applies it and only through the nose lag: a gain changes a lagged race (a dict
    #     with the config key is the same gain); the gain is scaled to the race's own nose rate once (on a 9/s nose 2.5
    #     floors to 1: the same race as gain 1, and judging from the command still changes that race); max_extra_deg 0
    #     is gain 1 (the command placed ON the aim); and on a lag-free nose (nose_lag=False, nose_rate=0, or a rate that
    #     closes the whole gap in one step) the lead changes nothing
    led = race(c, 1, nose_lead=2.5)
    if led == fleet:
        bad.append("nose_lead 2.5 raced today's race (the lead is not applied)")
    if race(c, 1, nose_lead={"nose_lead": 2.5}) != led:
        bad.append("{'nose_lead': 2.5} is not the 2.5 lead")
    quick, quick_led = race(c, 1, nose_rate=9.0), race(c, 1, nose_rate=9.0, nose_lead=2.5)
    if quick_led != race(c, 1, nose_rate=9.0, nose_lead=1.0) or quick_led == quick:
        bad.append("on a 9/s nose the 2.5 lead is not gain 1 judged from the command (effective_gain from the race's rate)")
    if race(c, 1, nose_lead=NoseLead(2.5, 0.0)) != race(c, 1, nose_lead=1.0):
        bad.append("max_extra_deg 0 is not gain 1 (the command on the aim)")
    for kw in ({"nose_lag": False}, {"nose_rate": 0}, {"nose_rate": 60.0}):
        if race(c, 1, nose_lead=2.5, **kw) != race(c, 1, **kw):
            bad.append(f"the lead changed a lag-free race ({kw})")
    # 11. what the lead measured here (2026-11-07, the docstring): at I1 course 2 today's lagged Manta needs 188.8 s and
    #     the 2.5 lead finishes it in 127.2 s; the control is inverted against the Scarab's: judging from the command
    #     alone (gain 1, 115.2 s) is faster still, so the pull past the aim is not what helps a Manta. And at I4 course 1,
    #     where today's steering misses 33 mouths through the lag, the 2.5 lead does NOT finish (3 of 40 rings in 900
    #     s): the roll stick about the nose's forward whirls a command placed far from the nose and the hull corkscrews
    #     away. A port that changes any of this re-measures.
    c = course_for(1, 2)
    today, led, judged = baseline[(1, 2)], race(c, 1, nose_lead=2.5), race(c, 1, nose_lead=1.0)
    if not (led["time"] and led["time"] < 0.8 * today["time"]):
        bad.append(f"the 2.5 lead no longer finishes I1 course 2 well under today's {today['time']} s: {led}")
    if not (judged["time"] and judged["time"] < led["time"]):
        bad.append(f"gain 1 (judged from the command, no pull) no longer beats the 2.5 lead on I1 course 2: {judged} vs {led}")
    trapped = race(course_for(4, 1), 4, nose_lead=2.5)
    if not (baseline[(4, 1)]["misses"] >= 20 and trapped["time"] is None and trapped["rings"] < 10):
        bad.append(f"the 2.5 lead's corkscrew on I4 course 1 has changed (re-measure the docstring): {trapped}")
    # 12. the lead_roll what-if (AIPilot's roll rule while leading): "judged" is the game's rule and the default; it does
    #     nothing with the lead off; with the roll stick off the 2.5 lead finishes I4 course 1 (negative control: judged
    #     corkscrews, check 11); a bad value is refused
    if race(c, 1, lead_roll="off") != today or race(c, 1, nose_lead=2.5, lead_roll="judged") != led:
        bad.append("lead_roll changed a race it should not (lead off, or the game's own 'judged' rule)")
    unrolled = race(course_for(4, 1), 4, nose_lead=2.5, lead_roll="off")
    if unrolled["time"] is None:
        bad.append(f"the 2.5 lead with no roll stick no longer finishes I4 course 1: {unrolled}")
    try:
        race(c, 1, lead_roll="sideways")
        bad.append("lead_roll accepted a value it does not know")
    except ValueError:
        pass
    print("manta_redline_sim self-test: " + ("ok" if not bad else "FAILED\n  " + "\n  ".join(bad)))
    return 0 if not bad else 1


def nose_lead_arg(argv):
    """--nose-lead G[,EXTRA]: the AI nose lead a run adds (AIPilot.NoseLeadGain at the fleet lag, and how far past the
    aim it may place the command, 40 deg unless given), or None: off, today's steering."""
    if "--nose-lead" not in argv:
        return None
    parts = argv[argv.index("--nose-lead") + 1].split(",")
    return NoseLead(float(parts[0]), float(parts[1]) if len(parts) > 1 else None)


def main(argv):
    if "--self-test" in argv:
        return self_test()
    lead = nose_lead_arg(argv)
    if "--trace" in argv:
        k = argv.index("--trace")
        i, s = int(argv[k + 1]), int(argv[k + 2])
        log = []
        r = race(course_for(i, s), i, regatta="--old-steering" not in argv,
                 throttle_floor=1.0 if "--floor" in argv else THROTTLE_FLOOR, trace=log, nose_lead=lead)
        print("\n".join(log))
        print(r)
        return 0
    bands = None
    if "--bands" in argv:
        bands = tuple(float(x) for x in argv[argv.index("--bands") + 1].split(","))
    seeds = range(1, 1 + (int(argv[argv.index("--seeds") + 1]) if "--seeds" in argv else 6))
    variants = [("shipped (Regatta steering, skill thr)", {})]
    if lead is not None:
        variants.append((f"AI nose lead {lead.gain:g} (+{lead.max_extra_deg:g} deg max)", {"nose_lead": lead}))
    if "--what-ifs" in argv:
        variants += [("old steering (before 2026-11-03)", {"regatta": False}),
                     ("full throttle (AIThrottleFloor 1)", {"throttle_floor": 1.0}),
                     ("no nose lag", {"nose_lag": False})]
        if lead is None:
            variants.append(("AI nose lead 2.5 (the Scarab's gain)", {"nose_lead": 2.5}))
    print(f"Redline offline, the AI Manta, {len(seeds)} courses an intensity, bands {bands or (BOOST_BAND, YASTRI_BAND)}")
    for i in (1, 2, 3, 4):
        courses = [course_for(i, s) for s in seeds]
        for label, kw in variants:
            print(f"I{i} " + table(label, [race(c, i, bands=bands, **kw) for c in courses]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
