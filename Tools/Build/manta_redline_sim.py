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
                  multiplier 1 + 3 x min(LT, RT), Yawstery yaw 60 deg/s x (RT - LT) about the hull's up
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
a rolling hull. That was AIPilot's roll rule to change, and it changed (below).
THE ROLL DECOUPLING (2026-11-07, brief 22): AIPilot now flies NO roll stick while the lead steers
(NoseLeadSteering.RollStick; ai_nose_lead.roll_stick, roll law "zero", the default), and so does this model; the
lead as first built is the law "led" ({"gain": 2.5, "roll": "led"}), which the checks above use to keep the
corkscrew measured, and "unled" (the lead-off yaw stick as the roll) is a rejected what-if. On 12 fresh courses an
intensity (seedbase 1001) the decoupled 2.5 lead finishes 48 of 48 (shipped bands 23.34, the parent's winner 24.15)
against the led roll's 6 and 11 and the lead off's 22.19 and 18.80: un-trapped, not better; the Manta's fix stays
the nose row (#1780).
Numbers: /mnt/project-files/regatta-training/results/lead-roll-decoupled/.

NOT modelled (LIMITS): rivals and their Yawstery trails; the Sting and Kabloom; the comeback's element levels; the lobby
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
from ai_nose_lead import NoseLead, roll_stick, slerp_beyond            # the AI nose lead (AIPilot.NoseLeadGain), one shared piece

# Manta.prefab
CRUISE, BOOST_MULT, ROT_THROTTLE = 180.0, 4.0, 0.2
PITCH, YAW, ROLL = 50.0, 30.0, 100.0
YAWSTERY_YAW = 60.0                                   # MantaAnalogTurnBoostExecutor.maxYawDegPerSec
THROTTLE_LOW, THROTTLE_HIGH = 0.2, 0.9              # AIPilot defaultThrottleLow / High
CREEP_LOW, CREEP_HIGH = 0.0005, 0.001               # AIPilot throttleIncreaseLow / High, per second
BOOST_BAND, YAWSTERY_BAND = 0.35, 0.75                # the executor's code defaults (no MantaSoarAITuning_Redline)
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
# THE TIME MODEL AS FLOWN (2026-11-11, RedlineCourse's "curve as the race is FLOWN"): since 2026-11-07 (96952eb939,
# c4052cb70e) Soar is the Manta's Time LEVEL-5 upgrade (MantaAnalogTurnBoostExecutor.soarUnlockedBy, the ability map's
# UnlockLevel 5 / RelockBelowLevel 4) and Time raises the CRUISE too (VesselTransformer.ThrottleScalerMultiplier 1 -> 1.5
# over Time 0-10: +5% a level; BoostSpeedMultiplier 1 -> 1.3, +3% a level, while boosting). The race starts every Manta
# at the card's Time level (RedlineCourse.StartingTimeLevelByIntensity, read from the C#). TIME_MODEL "legacy" is the
# hull this model was first written for (Soar live at Time 0, Time scaling Soar alone, start at 0): the 2026-11-07
# baselines in --self-test checks 3-12 were measured on it and are kept as that record.
CRUISE_PER_LEVEL = 0.05
SOAR_UNLOCK, SOAR_RELOCK_BELOW = 5, 4
TIME_MODEL = "flown"
LEGACY_LAPS = 5                                     # the race the legacy baselines were measured on
# RedlineController (2026-11-11): the AI race line (TryRaceLine, aiRaceLine on), the throttle floor by difficulty
# (aiThrottleFloorByDifficulty) and the nose rows (aiNoseFollowByIntensity). DIFFICULTIES is (name, floor, nose rate by
# intensity I1..I4).
AI_RACE_LINE = True
DIFFICULTIES = (("Easy", 0.7, (2, 2, 2.5, 4)), ("Medium", 0.85, (3, 3, 3, 5)),
                ("Hard", 1.0, (6, 6, 6, 6)), ("Legendary", 1.0, (9, 9, 9, 9)))


def _shipped_start_levels():
    """RedlineCourse.StartingTimeLevelByIntensity, read from the C# (the card's StartingElements)."""
    import re
    path = os.path.join(REPO, "Assets", "_Scripts", "Controller", "Arcade", "Redline", "RedlineCourse.cs")
    with open(path, encoding="utf-8") as fh:
        m = re.search(r"StartingTimeLevelByIntensity\s*=\s*\{\s*([0-9, ]+)\}", fh.read())
    if not m:
        raise RuntimeError(f"RedlineCourse.StartingTimeLevelByIntensity not found in {path}")
    return tuple(int(x) for x in m.group(1).split(","))


START_LEVELS = _shipped_start_levels()


def cruise_mult(level, model=None):
    """ThrottleScalerMultiplier at a Time level (1 on the legacy hull)."""
    return 1.0 if (model or TIME_MODEL) == "legacy" else 1.0 + CRUISE_PER_LEVEL * min(MAX_LEVEL, level)


def soar_lit(level, model=None):
    """Soar's level-5 gate (always lit on the legacy hull). The race only climbs, so the relock never fires here."""
    return (model or TIME_MODEL) == "legacy" or level >= SOAR_UNLOCK


def soar_speed(s, level, model=None):
    """RedlineCourse.SoarSpeed: both triggers at s, throttle buried."""
    cruise = CRUISE * cruise_mult(level, model)
    if s <= 0.01 or not soar_lit(level, model):
        return cruise
    return cruise * (1 + (BOOST_MULT - 1) * s) * (1 + TIME_PER_LEVEL * min(MAX_LEVEL, level))


def stick_radius(v):
    """RedlineCourse.StickRadiusAt: the stick alone, 0.2 v + 30 deg/s."""
    return v / math.radians(v * ROT_THROTTLE + YAW)


def fastest_soar(radius, level):
    """RedlineCourse.FastestSoarForCorner(radius, timeLevel)."""
    if not soar_lit(level):
        return 0.0
    if radius >= stick_radius(soar_speed(1.0, level)):
        return 1.0
    if radius <= stick_radius(soar_speed(0.0101, level)):
        return 0.0
    lo, hi = 0.0101, 1.0
    for _ in range(40):
        mid = 0.5 * (lo + hi)
        if stick_radius(soar_speed(mid, level)) > radius:
            hi = mid
        else:
            lo = mid
    return lo


def settle_distance(v_from, v_to, settle=0.1):
    """RedlineCourse.SettleDistance: the ground a speed change takes on the 1.5/s lag."""
    if v_from <= v_to:
        return 0.0
    t = math.log(1.0 / settle) / LERP
    return v_to * t + (v_from - v_to) * (1 - math.exp(-LERP * t)) / LERP


# THE READER (2026-11-11, model only: nothing in the game reads a plan). What each corner asks of the triggers, two ways:
#   plan(), by the course's own corner radius (RaceCourseGeometry.CornerRadius, the circle tangent to both legs at
#   half-leg, the number the course is CUT on): measured, a pilot who lifts by it LOSES 20-45% at I2-I4, because a
#   pilot flying at the mouths' centres has the whole leg to turn and the radius overstates the corner;
#   pursuit_plan(), by flying it: the most Soar at which a hull turning flat out from a gate, at constant speed, still
#   threads the next mouth (inside 80% of it). This is the honest reader, and "reading the course saves" is measured on it.
FLAT, EASE, LIFT, KNIFE = 0, 1, 2, 3
CLEAN_MOUTH = 0.5                                   # RedlineHeat.CleanMouthFraction
EASE_SOAR = 0.2                                     # a corner that still holds this much Soar is an Ease, under it a Lift
READER = "pursuit"                                  # race(pilot="reader") reads pursuit_plan(); "radius" reads plan()


def verb_for(radius, level):
    """A corner of `radius` (u) at a Time level, by the corner radius."""
    if radius < stick_radius(CRUISE * cruise_mult(level)):
        return KNIFE
    if not soar_lit(level) or radius >= stick_radius(soar_speed(1.0, level)):
        return FLAT
    return EASE if fastest_soar(radius, level) >= EASE_SOAR else LIFT


def plan(course, level, settle=0.1):
    """By the corner radius: [(verb, soar, turn_in)] per gate of one lap; `settle` is the fraction of the speed change
    left at the gate (turn_in is the ground it takes from full Soar)."""
    gates = [tuple(g["p"]) for g in course["gates"]]
    n = len(gates)
    top = soar_speed(1.0, level)
    out = []
    for i in range(n):
        r = corner_radius(gates[i - 1], gates[i], gates[(i + 1) % n])
        verb = verb_for(r, level)
        s = 1.0 if verb == FLAT else 0.0 if verb == KNIFE else fastest_soar(r, level)
        out.append((verb, s, 0.0 if verb == FLAT else settle_distance(top, soar_speed(s, level), settle)))
    return out


def _rot_toward(h, d, max_deg):
    a = angle(h, d)
    if a <= max_deg or a < 1e-6:
        return unit(d)
    ax = cross(h, d)
    ax = unit(ax) if norm(ax) > 1e-9 else (0.0, 1.0, 0.0)
    t = math.radians(max_deg)
    return unit(add(add(mul(h, math.cos(t)), mul(cross(ax, h), math.sin(t))), mul(ax, dot(ax, h) * (1 - math.cos(t)))))


def pursuit_threads(p0, h0, gp, ga, gr, v, margin=0.8, dt=1 / 60, tmax=6.0):
    """A hull at p0 heading h0, at constant speed v, turning at its stick's full rate (0.2 v + 30 deg/s) straight at
    gp: does it cross that gate's plane inside `margin` of its mouth?"""
    pos, h, t = p0, unit(h0), 0.0
    w = v * ROT_THROTTLE + YAW
    while t < tmax:
        h = _rot_toward(h, sub(gp, pos), w * dt)
        nxt = add(pos, mul(h, v * dt))
        a, b = dot(sub(pos, gp), ga), dot(sub(nxt, gp), ga)
        if (a <= 0 < b) or (b <= 0 < a) or (a < 0 <= b) or (b < 0 <= a):
            x = add(pos, mul(sub(nxt, pos), a / (a - b) if a != b else 0.0))
            return norm(sub(x, gp)) <= gr * margin
        pos, t = nxt, t + dt
    return False


def pursuit_plan(course, level, settle=0.1):
    """By flying it: [(verb, soar, turn_in)] for the corner AT each gate (the turn from its leg in onto the next mouth)."""
    gates = [(tuple(x["p"]), unit(tuple(x["a"])), float(x["r"])) for x in course["gates"]]
    n = len(gates)
    top = soar_speed(1.0, level)
    out = []
    for g in range(n):
        at, heading = gates[g][0], sub(gates[g][0], gates[g - 1][0])
        tp, ta, tr = gates[(g + 1) % n]
        if pursuit_threads(at, heading, tp, ta, tr, top):
            out.append((FLAT, 1.0, 0.0))
            continue
        if not pursuit_threads(at, heading, tp, ta, tr, soar_speed(0.0, level)):
            out.append((KNIFE, 0.0, settle_distance(top, soar_speed(0.0, level), settle)))
            continue
        lo, hi = 0.0, 1.0
        for _ in range(20):
            mid = 0.5 * (lo + hi)
            if pursuit_threads(at, heading, tp, ta, tr, soar_speed(mid, level)):
                lo = mid
            else:
                hi = mid
        out.append((EASE if lo >= EASE_SOAR else LIFT, lo, settle_distance(top, soar_speed(lo, level), settle)))
    return out


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
         limit=900.0, dt=1 / 60, laps=None, crystals=True, crystal_slack=AI_CRYSTAL_SLACK, trace=None, nose_rate=None,
         nose_lead=None, pilot="ai", start_level=None, time_model=None, turn_slack=0.0, read_settle=0.1,
         out_deg=45.0, react=False, centre_line=None):
    """One AI Manta flies `laps` laps of `course` ({gates: [{p, a, r}]}), steering for a Time crystal on its way when it
    costs under `crystal_slack` u (RedlineController.TryOverrideAim; `crystals`: True = RedlineTimeCrystals' field,
    False = none, or a list of positions). Returns {time (None = not finished), rings, of, top, mean_speed, soar (mean
    boost), misses, breakoffs, level (Time level at the end)}.
    `nose_rate` is VesselTransformer.AINoseFollowPerSecond, the rate a mode may set for an AI's nose (Switchback's lag by
    difficulty): None = unset (the fleet lag, LERP 1.5/s), 0 = no lag (the nose IS the command, as a human's), > 0 = that
    rate per second, applied as Mathf.Min(1, rate x dt). `nose_lag=False` is the same as `nose_rate=0`.
    `nose_lead` is AIPilot.NoseLeadGain (ai_nose_lead.NoseLead.from_config: None = off, a gain, {"gain", "max_extra_deg"}
    or a NoseLead), stated at the fleet lag and scaled to this race's nose rate once; 0 with no nose lag.
    The roll stick while the lead is on is the NoseLead's `roll` law ({"gain": 2.5, "roll": "zero" | "led" | "unled"},
    ai_nose_lead.roll_stick): "zero" (the game's law since the roll decoupling: no roll stick while leading), "led" (the
    lead's judged yaw stick as the roll, which corkscrews a rolling hull) or "unled" (today's lead-off yaw stick). It
    has no effect with the lead off.
    `pilot` (2026-11-11): "ai" is the AI Manta above. The others are PEOPLE: no nose lag, the throttle buried, the same
    mouths' centres, and their own triggers - "bury" (both always flat), "ease" (a habit: eases both to half for EVERY
    corner, at that corner's turn-in, and winds back once it points at the next ring), "band" (a habit: the AI's stick
    bands on a person's stick, easing only as the stick goes over) and "reader" (reads the lap: pursuit_plan's verb at
    each corner, READER above, lifting at its turn-in, and the Yawstery pull out of a knife). `start_level` overrides the card's
    Time level; `time_model` "legacy" flies the hull before 2026-11-07 (module TIME_MODEL otherwise); `turn_slack` adds
    ground (u) to every turn-in. `centre_line` is RedlineController's AI race line (TryRaceLine, 2026-11-11): the AI aims at
    the mouth's centre, judged as a look-ahead (no orbit break-off), until it misses that ring, then flies the gate way
    above to re-attack it."""
    model = time_model or TIME_MODEL
    if laps is None:
        laps = LEGACY_LAPS if model == "legacy" else LAPS
    if centre_line is None:
        centre_line = AI_RACE_LINE and model != "legacy"
    band, yband = bands or (BOOST_BAND, YAWSTERY_BAND)
    follow = (LERP if nose_rate is None or nose_rate < 0 else float(nose_rate)) if nose_lag else 0.0
    if pilot != "ai":
        follow = 0.0                                                # a person's nose IS the command
    follow_step = min(1.0, follow * dt) if follow > 0 else 1.0
    lead_knob = NoseLead.from_config(nose_lead)
    # AIPilot.EffectiveNoseLeadGain, once a race, from THIS race's nose rate (`follow`): max(1, gain x 1.5 / rate), and
    # 0 when the nose has no lag (nose_lag=False, nose_rate=0, or a rate that closes the whole gap in one step)
    g_lead = lead_knob.effective(follow) if follow_step < 1.0 else 0.0
    lead_extra = lead_knob.max_extra_deg
    lead_roll = lead_knob.roll                                # AIPilot's roll law while the lead is on (ai_nose_lead)
    gates = [(tuple(g["p"]), unit(tuple(g["a"])), float(g["r"])) for g in course["gates"]]
    n = len(gates)
    total = n * max(1, laps)
    field = crystals_for(course, intensity) if crystals is True else list(crystals or [])
    back_at = [-1.0] * len(field)                                   # when a taken crystal is back (-1 = standing)
    if start_level is not None:
        level = int(start_level)
    else:
        level = 0 if model == "legacy" else START_LEVELS[max(1, min(4, intensity)) - 1]
    plans = {}

    def plan_at(lv):
        if lv not in plans:
            if pilot == "reader":
                plans[lv] = (pursuit_plan if READER == "pursuit" else plan)(course, lv, read_settle)
            else:
                plans[lv] = [(EASE, 0.5, settle_distance(soar_speed(1.0, lv, model), soar_speed(0.5, lv, model)))] * n
        return plans[lv]

    def person_triggers(xs_now, ys_now, heading_now):
        """The people's triggers (`pilot`): (lt, rt)."""
        if pilot == "bury":
            return 1.0, 1.0
        pl = plan_at(level)
        want, knife = 1.0, False
        if idx > 0:                                             # turning out of the gate just threaded
            verb, s_c, _ = pl[(idx - 1) % n]
            if verb != FLAT and angle(heading_now, sub(gates[idx % n][0], pos)) > out_deg:
                want, knife = min(want, s_c), verb == KNIFE
        along = norm(sub(gates[idx % n][0], pos))
        for k in range(2):                                      # the next two corners' turn-ins
            if idx + k >= total:
                break
            g = (idx + k) % n
            verb, s_c, turn = pl[g]
            if verb != FLAT and along <= turn + turn_slack:
                want = min(want, s_c)
            along += norm(sub(gates[(g + 1) % n][0], gates[g][0]))
        if knife and abs(xs_now) > 0.2:
            return (0.0, 1.0) if xs_now > 0 else (1.0, 0.0)     # Yawstery: the trigger on the turn's side, the other off
        if react:                                               # and eases on the stick as the habit does
            want = min(want, boost_intent(max(abs(xs_now), abs(ys_now)), band))
        return want, want

    skill = max(0.0, min(1.0, intensity * 0.25))
    throttle = THROTTLE_LOW + (THROTTLE_HIGH - THROTTLE_LOW) * skill
    creep = CREEP_LOW + (CREEP_HIGH - CREEP_LOW) * skill

    p0, a0, _ = gates[0]
    pos = sub(p0, mul(a0, START_BACK))
    q = q_look(a0)
    cmd = q
    v, t = 0.0, 0.0
    idx, locked, side, lead = 0, -1, 1.0, LEAD
    line_lost = -1                                                  # GateRaceController's lineLostIndex
    extending, ext_t = False, 0.0
    orbit = Orbit()
    prev = None
    top = soar_sum = speed_sum = 0.0
    frames = misses = breakoffs = clean = 0
    next_trace = 0.0

    while t < limit and idx < total:
        ring, ax, rad = gates[idx % n]

        # GateRaceController's aim provider: the mode's override first (RedlineController.TryOverrideAim)
        aim = crystal_aim(pos, q_rot(q, FWD), ring, field, back_at, crystal_slack) if field else None
        if aim is None and regatta and prev is not None and plane_crossing(prev, pos, ring, ax, rad) == -1:
            line_lost = idx
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
        elif pilot != "ai" or (centre_line and line_lost != idx):
            target = ring                                           # a person (and the race line) flies at the centre
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
        on_line = pilot == "ai" and centre_line and line_lost != idx and aim is None
        if on_line:
            extending = False                                       # MarkAimAsPathLead: no break-off judges the line
        elif extending:
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
        yd = None
        if dsq <= 1e-12:
            xs, ys = 0.0, 0.0
        elif g_lead > 0.0:
            # the nose lead (AIPilot.TryNoseLead): want the NOSE turned toward the aim by g_lead times its error, past it
            # (NoseLeadDirection), and judge the error from the COMMAND's forward; the stick's frame, the triggers and
            # the command update below are untouched, and the break-off's escape steer takes the lead like any aim
            want = tuple(slerp_beyond(q_rot(q, FWD), unit(dist), g_lead, lead_extra))
            xs, ys = stick(q, want, dsq, judged=unit(q_rot(cmd, FWD)))
            # the roll stick with the lead on (AIPilot's roll law): the unled error's yaw stick by default
            yd = roll_stick(xs, stick(q, unit(dist), dsq)[0] if lead_roll == "unled" else xs, lead_roll)
        else:
            xs, ys = stick(q, unit(dist), dsq)
        if yd is None:
            yd = xs                                                 # the roll stick (no belly target in Redline)
        thr = 1.0 if pilot != "ai" else max(0.0, min(1.0, max(throttle, throttle_floor)))
        throttle += creep * dt

        # MantaAnalogTurnBoostExecutor: the triggers, the Yawstery yaw, the Soar
        if pilot in ("ai", "band"):
            lt, rt = triggers(xs, max(abs(xs), abs(ys)), band, yband)
        else:
            lt, rt = person_triggers(xs, ys, q_rot(q, FWD))
        boost = min(lt, rt) if soar_lit(level, model) else 0.0
        nose_up = q_rot(q, UP)
        if abs(rt - lt) > 0.001:
            cmd = q_mul(q_axis((rt - lt) * YAWSTERY_YAW * dt, nose_up), cmd)
        # VesselTransformer.CurrentBoostAmount: the Soar multiplier x the Time level's BoostSpeedMultiplier while boosting
        mult = (1 + (BOOST_MULT - 1) * boost) * (1 + TIME_PER_LEVEL * level) if boost > 0.01 else 1.0
        mult *= cruise_mult(level, model)

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
            a_, b_ = dot(sub(pos, ring), ax), dot(sub(newpos, ring), ax)
            x_ = add(pos, mul(sub(newpos, pos), a_ / (a_ - b_) if a_ != b_ else 0.0))
            clean += norm(sub(x_, ring)) <= CLEAN_MOUTH * rad      # RedlineHeat: through the heart of the mouth
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
            "misses": misses, "breakoffs": breakoffs, "level": level, "clean": clean}


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
    global TIME_MODEL
    TIME_MODEL = "legacy"          # checks 1-12 are the 2026-11-07 record, measured on the hull before Soar's unlock
    try:
        bad = _legacy_checks()
    finally:
        TIME_MODEL = "flown"
    bad += _flown_checks()
    print("manta_redline_sim self-test: " + ("ok" if not bad else "FAILED\n  " + "\n  ".join(bad)))
    return 0 if not bad else 1


def _flown_checks():
    """13-17 (2026-11-11): the race as flown since Soar became the Time level-5 upgrade (REDLINE.md section 0)."""
    bad = []
    import re
    # 13. the flown hull is the shipped one: Manta.prefab's Time multipliers, the ability map's unlock, the card's start
    prefab = open(os.path.join(REPO, "Assets", "_Prefabs", "Spacevessels", "Manta.prefab"), encoding="utf-8").read()
    for name, lo, hi in (("ThrottleScalerMultiplier", 1, 1.5), ("BoostSpeedMultiplier", 1, 1.3)):
        m = re.search(rf"  {name}:\n    Enabled: 1\n    Value: [0-9.]+\n    Min: ([0-9.]+)\n    Max: ([0-9.]+)\n    element: 4\n", prefab)
        if not m or (float(m.group(1)), float(m.group(2))) != (lo, hi):
            bad.append(f"Manta.prefab {name} is not Time {lo} -> {hi} (re-port cruise_mult / soar_speed)")
    amap = open(os.path.join(REPO, "Assets", "Resources", "ElementalAbilityMaps", "Manta.asset"), encoding="utf-8").read()
    m = re.search(r"UnlockLevel: (\d+)\n(?:    [^\n]*\n){0,4}?    UpgradeLabel: Soar", amap)
    if not m or int(m.group(1)) != SOAR_UNLOCK:
        bad.append(f"Manta.asset's Soar does not unlock at Time {SOAR_UNLOCK}")
    card = open(os.path.join(REPO, "Assets", "_SO_Assets", "Games", "ArcadeGameRedline.asset"), encoding="utf-8").read()
    rows = re.findall(r"  - Class: 1\n    Intensity: (\d)\n    Levels:\n(?:      \w+: [0-9.]+\n){3}      Time: ([0-9.]+)\n", card)
    if sorted((int(i), round(float(t) * 10)) for i, t in rows) != [(i + 1, START_LEVELS[i]) for i in range(4)]:
        bad.append(f"the card's StartingElements {rows} are not RedlineCourse.StartingTimeLevelByIntensity {START_LEVELS}")
    if abs(soar_speed(1.0, 6) - 1104.48) > 0.05 or abs(soar_speed(1.0, 10) - 1404.0) > 0.05:
        bad.append(f"soar_speed is not RedlineCourse.SoarTop ({soar_speed(1.0, 6)}, {soar_speed(1.0, 10)})")
    # 14. negative control: on the flown hull a Manta started at Time 0 never Soars (the race before 2026-11-11), and is
    #     at least twice as slow as one started at the card's Time 6
    c = course_for(1, 1)
    lit, dark = race(c, 1, pilot="bury"), race(c, 1, pilot="bury", start_level=0)
    if not (lit["time"] and dark["time"] and dark["time"] > 2 * lit["time"] and dark["soar"] == 0.0):
        bad.append(f"a Time-0 start is not the dark race ({dark}) against the lit one ({lit})")
    # 15. the shipped AI (race line, floor, nose rows) finishes I1 and I4 at Legendary and at Easy
    for i in (1, 4):
        for name, floor, rates in (DIFFICULTIES[0], DIFFICULTIES[3]):
            r = race(course_for(i, 2), i, throttle_floor=floor, nose_rate=rates[i - 1])
            if r["time"] is None:
                bad.append(f"the shipped {name} AI did not finish I{i} course 2: {r}")
    # 16. why the race line: on the gate way the Legendary AI is far slower at I1 (negative control)
    c = course_for(1, 2)
    line, gate = race(c, 1, throttle_floor=1.0, nose_rate=9), race(c, 1, throttle_floor=1.0, nose_rate=9, centre_line=False)
    if not (line["time"] and (gate["time"] is None or gate["time"] > 1.3 * line["time"])):
        bad.append(f"the race line no longer beats the gate way at I1 ({line['time']} vs {gate['time']})")
    # 17. the people: the same race twice is the same race, and the reader on a lap with no costing corner is bury
    if race(c, 1, pilot="reader") != race(c, 1, pilot="reader"):
        bad.append("a person's race is not deterministic")
    if race(c, 1, pilot="reader")["time"] != race(c, 1, pilot="bury")["time"]:
        bad.append("at I1 (no costing corner) the reader does not fly the burier's race")
    return bad


def _legacy_checks():
    bad = []
    # 1. triggers() is the C# shape (MantaAutopilotDriveTests): straight = both full; a full yaw stick at the default bands
    #    is one trigger flat and the other released; inside the Yawstery band the net is zero; band 0 never boosts.
    if triggers(0.0, 0.0, 0.35, 0.75) != (1.0, 1.0):
        bad.append("triggers: a straight stick does not bury both triggers")
    if triggers(1.0, 1.0, 0.35, 0.75) != (0.0, 1.0) or triggers(-1.0, 1.0, 0.35, 0.75) != (1.0, 0.0):
        bad.append("triggers: a full yaw stick is not one trigger flat and the other released")
    lt, rt = triggers(0.6, 0.6, 0.35, 0.75)
    if abs(rt - lt) > 1e-9 or abs(lt - (1 - 0.25 / 0.65)) > 1e-6:
        bad.append(f"triggers: inside the Yawstery band ({lt}, {rt})")
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
        got.pop("clean", None)                                  # counted since 2026-11-11, not in the record
        if got != want:
            bad.append(f"I{i} course {s} drifted from the 2026-11-07 baseline (re-measure): {got} vs {want}")
    c = course_for(1, 1)
    fleet = baseline[(1, 1)]
    for form in (NoseLead(), 0, {}, {"gain": 0}, False):
        if {k: v for k, v in race(c, 1, nose_lead=form).items() if k != "clean"} != fleet:
            bad.append(f"nose_lead={form!r} is not today's race")
    # 10. the lead is applied as AIPilot applies it and only through the nose lag: a gain changes a lagged race (a dict
    #     with the config key is the same gain); the gain is scaled to the race's own nose rate once (on a 9/s nose 2.5
    #     floors to 1: the same race as gain 1, and judging from the command still changes that race); max_extra_deg 0
    #     is gain 1 (the command placed ON the aim); and on a lag-free nose (nose_lag=False, nose_rate=0, or a rate that
    #     closes the whole gap in one step) the lead changes nothing
    led = race(c, 1, nose_lead=2.5)
    if {k: v for k, v in led.items() if k != "clean"} == fleet:
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
    #     (Checks 11 fly the lead as first built, its roll law "led"; check 12 the game's law since the decoupling.)
    LED = "led"
    c = course_for(1, 2)
    today = baseline[(1, 2)]
    led, judged = race(c, 1, nose_lead={"gain": 2.5, "roll": LED}), race(c, 1, nose_lead={"gain": 1.0, "roll": LED})
    if not (led["time"] and led["time"] < 0.8 * today["time"]):
        bad.append(f"the 2.5 lead no longer finishes I1 course 2 well under today's {today['time']} s: {led}")
    if not (judged["time"] and judged["time"] < led["time"]):
        bad.append(f"gain 1 (judged from the command, no pull) no longer beats the 2.5 lead on I1 course 2: {judged} vs {led}")
    trapped = race(course_for(4, 1), 4, nose_lead={"gain": 2.5, "roll": LED})
    if not (baseline[(4, 1)]["misses"] >= 20 and trapped["time"] is None and trapped["rings"] < 10):
        bad.append(f"the 2.5 lead's corkscrew on I4 course 1 has changed (re-measure the docstring): {trapped}")
    # 12. the roll decoupling (AIPilot's NoseLeadSteering.RollStick, ai_nose_lead.roll_stick, 2026-11-07): the game's law
    #     ("zero", the default) flies no roll stick while leading and finishes I4 course 1 (check 11, the "led" law, is
    #     its negative control); "unled" (the lead-off yaw stick as the roll) is a measured what-if; no law touches a
    #     race with the lead off
    free = race(course_for(4, 1), 4, nose_lead=2.5)
    if free != race(course_for(4, 1), 4, nose_lead={"gain": 2.5, "roll": "zero"}) or free["time"] is None:
        bad.append(f"the decoupled 2.5 lead (no roll while leading, the default) no longer finishes I4 course 1: {free}")
    for law in ("led", "unled", "zero"):
        if race(c, 1, nose_lead={"gain": 0, "roll": law}) != race(c, 1):
            bad.append(f"roll law {law} changed a race with the lead off")
    if race(c, 1, nose_lead={"gain": 2.5, "roll": "unled"}) in (race(c, 1, nose_lead=2.5), led):
        bad.append("the unled roll what-if is the same race as another law (is roll_stick wired?)")
    return bad


def nose_lead_arg(argv):
    """--nose-lead G[,EXTRA]: the AI nose lead a run adds (AIPilot.NoseLeadGain at the fleet lag, and how far past the
    aim it may place the command, 40 deg unless given), or None: off, today's steering."""
    if "--nose-lead" not in argv:
        return None
    parts = argv[argv.index("--nose-lead") + 1].split(",")
    return NoseLead(float(parts[0]), float(parts[1]) if len(parts) > 1 else None)


def reading(seeds):
    """--reading (2026-11-11): the race as flown. People (no nose lag, throttle buried, at the mouths' centres): bury,
    the two habits (ease at every corner, ease on the stick) and the reader (RedlineReading's plan read off the course);
    "reading the course saves" = the best habit's median race over the reader's. Then the shipped AI by difficulty, and
    the laps a ~90 s race wants."""
    import statistics
    print(f"Redline as flown (Time {START_LEVELS} start, {LAPS} laps), {len(seeds)} courses an intensity")
    per_lap = {}
    for i in (1, 2, 3, 4):
        courses = [course_for(i, s) for s in seeds]
        med = {}
        for p in ("bury", "ease", "band", "reader"):
            rs = [race(c, i, pilot=p) for c in courses]
            fin = [r["time"] for r in rs if r["time"] is not None]
            med[p] = statistics.median(fin) if fin else float("inf")
            clean = sum(r["clean"] for r in rs) / max(1, sum(r["rings"] for r in rs))
            print(f"I{i} person {p:<7} {len(fin)}/{len(rs)}  median {med[p]:6.1f} s  clean {clean:4.0%}  "
                  f"misses {sum(r['misses'] for r in rs)}")
        best_habit = min(med["bury"], med["ease"], med["band"])
        print(f"I{i} reading the course saves {100 * (best_habit - med['reader']) / best_habit:+.1f}% "
              f"(the best habit {best_habit:.1f} s, the reader {med['reader']:.1f} s)")
        per_lap[i] = min(best_habit, med["reader"]) / LAPS
        for name, floor, rates in DIFFICULTIES:
            rs = [race(c, i, throttle_floor=floor, nose_rate=rates[i - 1]) for c in courses]
            fin = [r["time"] for r in rs if r["time"] is not None]
            print(f"I{i} AI {name:<9} {len(fin)}/{len(rs)}  median {statistics.median(fin) if fin else 0:6.1f} s  "
                  f"misses {sum(r['misses'] for r in rs)}")
    print("laps for a ~90 s race (the model's ideal person): " +
          ", ".join(f"I{i} {90 / per_lap[i]:.1f}" for i in per_lap))
    return 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--reading" in argv:
        return reading(range(1, 1 + (int(argv[argv.index("--seeds") + 1]) if "--seeds" in argv else 6)))
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
    print(f"Redline offline, the AI Manta, {len(seeds)} courses an intensity, bands {bands or (BOOST_BAND, YAWSTERY_BAND)}")
    for i in (1, 2, 3, 4):
        courses = [course_for(i, s) for s in seeds]
        for label, kw in variants:
            print(f"I{i} " + table(label, [race(c, i, bands=bands, **kw) for c in courses]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
