#!/usr/bin/env python3
"""Serpentine (GameModes.SerpentTime) raced offline by the AI Serpent: the Serpent's own Time game, for the AI plan's
time-arcade step (Docs/AI_SYSTEM/AI_PLAN.md section 5; the search is Tools/Compute/evaluators/serpent_fit.py).

A MODEL, not the game. It ports, line for line, what one AI Serpent does in MinigameSerpentTime with the dirty race
switched off (no shapes, walls, shots or cloak; MODEL_LIMITS):

    the course    SerpentTimeCourse.ForIntensity, READ from the C# (settings()), through HeadlongCircuit.Generate
                  (course_port.py, the float32 port), in the shell SerpentTimeCourseTests uses (480..1080, gate 0 on
                  the pole), SerpentTimeCourseSource.DefaultLaps laps (2) of EndConditionOverridesSO's 16-gate target.
                  Held to the shipped C# by Tools/Build/serpent_time_course_harness's serpent_time_parity.jsonl
    the start     SerpentTimeController's start line: startLineStandoff (300 u) behind gate 0 on its axis, pointed
                  through it, at rest (the seat's 90 u ring offset is left out: one AI)
    the gate aim  GateRaceController.ArmRacers' aim provider with the Regatta steering (SerpentTimeController turns
                  AICommitFromApproachPoint on) and the scene's aiCommitDistance / aiApproachLead / aiThroughDistance
                  (read from MinigameSerpentTime.unity): commit from the approach point, through the ring's centre,
                  break off and re-attack after a missed mouth. The objective the boost driver judges is the ring's
                  centre (ReportObjective(..., gate.Position, ...))
    the pilot     AIPilot: the single-stick EasedLeftJoystickPosition from the nose-to-target cross product (manta_redline_
                  sim.stick, TurnHardIfBehind), the orbit break-off (turning-circle test, OrbitDetector, the escape
                  point); no skill throttle: SingleStickVesselTransformer flies full throttle whatever XDiff says
    the hull      SingleStickVesselTransformer: roll -x (0.4 v + 20), yaw x (0.4 v + 80), pitch y (0.4 v + 80) deg/s
                  about the nose's axes onto the commanded rotation, the AI nose following at LERP_AMOUNT 1.5/s; speed
                  lerps 1.5/s toward 50 x boost + 10 (Serpent.prefab, read in hull())
    the pellets   ConsumeBoostActionExecutor: a press burns one pellet (resourceCost 0.25 of a 1.0 tank: four) for
                  boostDuration 6 s at Time 0 (no Time crystals in the Serpentine cell); n burning give boost
                  1 + (boostMultiplier - 1) x n (3 / 5 / 7 / 9x: 160 / 260 / 360 / 460 u/s). NOTHING refills on a
                  clock (2026-10-31): a pellet comes back only while the Serpent's own wall eats its own trail, and
                  this AI only cloaks behind a rival (SerpentTimeController.TickDirtyCloak), so alone it never refills.
                  `kit="clock"` is the economy before 2026-10-31 (3 s burns, a pellet back every 3.6 s), the one
                  every human mark in the flights was flown on
    the driver    PelletBoostAIPolicySO.Driver.Tick: on a straight (course within alignDegrees of the ring, stick
                  under stickBand, not breaking off), light one more pellet when fewer than maxConcurrentBurns burn,
                  (burning + 1) x engageRunwaySeconds of runway is left, the tank holds 1 + reservePellets, and
                  minSecondsBetweenPellets have passed since the last light

pellet_driver() MIRRORS PelletBoostAIPolicySO.Driver.Tick and AIBoostPolicySO.IsStraight / HasRunway; burn() and
boost() mirror ConsumeBoostActionExecutor.Consume / ApplyMultiplier: change them together.

NOT modelled (MODEL_LIMITS): the dirty race (the SerpentineGate shapes, their exits and gold core, the sniper shot,
the gate debris, rival Seed Walls and the cloak), so a gate here is its plain ring; rivals; the lobby difficulty
(GateRaceHandicap: every recorded AI rival but one flew Medium, a late or misjudged belief of the ring; this pilot is
Legendary); the wall refill; emergency steering; frame hitches; the 90 u start ring. The editor is the measurement.

The AI NOSE LEAD (AIPilot.NoseLeadGain, PR #1786; Tools/Build/ai_nose_lead.py is the one shared piece) is a knob OFF by
default: `nose_lead=` on race() and flights() (NoseLead.from_config: None = off, a gain, {"gain", "max_extra_deg"} or a
NoseLead) and `--nose-lead G[,EXTRA]` on the command line. With a gain the pilot judges its error from the COMMAND's
forward and wants the nose turned toward the aim by the effective gain times its error, past the aim (slerp_beyond, at
most max_extra_deg past it, 40 unless given); the stick's frame, the roll / yaw / pitch update about the nose's axes,
the nose's slerp and the pellet driver are untouched, and the break-off's escape steer takes the lead like any aim. The
effective gain is AIPilot.EffectiveNoseLeadGain from this model's own nose rate, once a race: LERP (the fleet 1.5/s, so
the gain is itself) while the nose lags, 0 with nose_lag=False (the nose IS the command: nothing to lead). Measured
2026-11-07 (self-test 7-10): every miss today is the lag's (the lag-free Serpent misses nothing on the 24 courses); at
I1 the 2.5 lead is faster on all 12 races of both genomes with no miss (today 5), and gain 1 alone (judged from the
command, no pull) is not; but at I2-I4 the fleet default (2.5, 40 deg) DNFs 16 of 36 races: a command placed up to 40
deg past an aim already far from the nose pins the single stick at full x AND y, and the roll about the nose's forward
whirls the yaw and pitch axes while the hull flies away from the ring (the Manta's corkscrew, manta_redline_sim.py).
Capped at 20 deg past the aim (--nose-lead 2.5,20) every race finishes: the shipped genome is faster than today at
I1-I3 with no miss (I2 742 vs 770 s, I3 950 vs 999 s over six courses) and within 2% at I4 (1113 vs 1093 s, one miss
each). What a Serpent flies is the AI plan's call (Docs/AI_SYSTEM/AI_PLAN.md); this model only reads the knob, and
Tools/Compute/evaluators/serpent_fit.py does not pass it yet.

    python3 Tools/Build/serpent_time_sim.py                    # the shipped and SerpentTime genomes, I1-I4 x 6 courses
    python3 Tools/Build/serpent_time_sim.py --genome a,s,r,m,g,k   # one genome (align, stick, runway, max, gap, reserve)
    python3 Tools/Build/serpent_time_sim.py --shapes           # the what-if: every gate's shape standing, the AI at an exit
    python3 Tools/Build/serpent_time_sim.py --old-kit          # the pellet economy before 2026-10-31 (3 s, refill 3.6 s)
    python3 Tools/Build/serpent_time_sim.py --nose-lead 2.5    # the same tables, each row followed by its races with the AI
    python3 Tools/Build/serpent_time_sim.py --nose-lead 2.5,20 # nose lead on (a gain, or gain,max_extra_deg); --trace and
                                                               # --flights take it too
    python3 Tools/Build/serpent_time_sim.py --trace 3 1        # one race (intensity, course seed), a line a second
    python3 Tools/Build/serpent_time_sim.py --flights DIR      # race the recorded flights' own courses (see flights())
    python3 Tools/Build/serpent_time_sim.py --self-test
"""
import json
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "Compute", "evaluators"))

import manta_redline_sim as M   # noqa: E402  the shared AIPilot / quaternion / gate pieces, ported once
from ai_nose_lead import NoseLead, slerp_beyond   # noqa: E402  the AI nose lead (AIPilot.NoseLeadGain), one shared piece

COURSE_CS = "Assets/_Scripts/Controller/Arcade/SerpentTime/SerpentTimeCourse.cs"
SOURCE_CS = "Assets/_Scripts/Controller/Arcade/SerpentTime/SerpentTimeCourseSource.cs"
CONTROLLER_CS = "Assets/_Scripts/Controller/Arcade/SerpentTime/SerpentTimeController.cs"
OVERRIDES_CS = "Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs"
SCENE = "Assets/_Scenes/Multiplayer Scenes/MinigameSerpentTime.unity"
PREFAB = "Assets/_Prefabs/Spacevessels/Serpent.prefab"
PELLETS = "Assets/_SO_Assets/VesselActions/Serpent/ConsumeBoostAction.asset"
POLICY = "Assets/_SO_Assets/AI Boost Policies/SerpentAIBoostPolicy.asset"
POLICY_SERPENTTIME = "Assets/Resources/AIBoostPolicies/SerpentAIBoostPolicy_SerpentTime.asset"
PARITY = os.path.join(HERE, "serpent_time_course_harness", "serpent_time_parity.jsonl")
INNER, OUTER = 480.0, 1080.0          # SerpentTimeCourseTests' shell (RaceCourseSource.ResolveShell on the shipped cell)
LERP = 1.5                            # VesselTransformer.LERP_AMOUNT: speed, and the AI nose
DT = 1 / 60
GENOME_FIELDS = ("alignDegrees", "stickBand", "engageRunwaySeconds", "maxConcurrentBurns", "minSecondsBetweenPellets",
                 "reservePellets")
INT_FIELDS = ("maxConcurrentBurns", "reservePellets")
OLD_KIT = {"burn": 3.0, "refill_every": 3.6}   # SERPENTTIME.md "The loop": the pellet economy before 2026-10-31

MODEL_LIMITS = ("Serpentine offline (serpent_time_sim.py): the shipped course, gate aim, AIPilot single stick and orbit "
                "break-off, the hull's turn law and AI nose lag, the pellet executor (four, no refill) and the pellet "
                "driver; one AI, no rivals; the dirty race is OFF (no SerpentineGate shapes, exits, core shots, debris, "
                "Seed Walls or cloak: a gate is its plain ring), no lobby handicap (Legendary), no wall refill, a start on "
                "gate 0's axis 300 u back")


def _read(rel):
    with open(os.path.join(REPO, rel), encoding="utf-8") as fh:
        return fh.read()


def _floats(text):
    return [float(x) for x in re.findall(r"-?[0-9.]+(?:e-?\d+)?", text)]


def _yaml_field(text, key):
    m = re.search(r"^\s+" + re.escape(key) + r":\s*(-?[0-9.]+(?:e-?\d+)?)\s*$", text, re.M)
    if not m:
        raise RuntimeError(f"{key} not found")
    return float(m.group(1))


# ── what the C# and the assets say (read, never retyped) ─────────────────────────────────────────────────────────

def hull():
    """Serpent.prefab's flight numbers and ConsumeBoostAction.asset's pellet."""
    p, c = _read(PREFAB), _read(PELLETS)
    return {"min": _yaml_field(p, "DefaultMinimumSpeed"), "scaler": _yaml_field(p, "DefaultThrottleScaler"),
            "rot": _yaml_field(p, "RotationThrottleScaler"), "pitch": _yaml_field(p, "PitchScaler"),
            "yaw": _yaml_field(p, "YawScaler"), "roll": _yaml_field(p, "RollScaler"),
            "mult": _yaml_field(c, "boostMultiplier"), "burn": _yaml_field(c, "boostDuration"),
            "tank": int(round(1.0 / _yaml_field(c, "resourceCost")))}


def read_genome(rel):
    """A PelletBoostAIPolicySO asset's decision fields, or None when the file is missing."""
    if not os.path.isfile(os.path.join(REPO, rel)):
        return None
    t = _read(rel)
    return {k: _yaml_field(t, k) for k in GENOME_FIELDS}


def scene_steering():
    """MinigameSerpentTime.unity's GateRaceController AI distances (aiCommitDistance, aiApproachLead, aiThroughDistance)."""
    t = _read(SCENE)
    return {k: _yaml_field(t, k) for k in ("aiCommitDistance", "aiApproachLead", "aiThroughDistance")}


def laps():
    m = re.search(r"DefaultLaps\s*=\s*(\d+)\s*;", _read(SOURCE_CS))
    return int(m.group(1))


def gate_target():
    m = re.search(r"DefaultSerpentTimeGateTarget\s*=\s*(\d+)\s*;", _read(OVERRIDES_CS))
    return int(m.group(1))


def start_standoff():
    m = re.search(r"float\s+startLineStandoff\s*=\s*([0-9.]+)f", _read(CONTROLLER_CS))
    return float(m.group(1)) if m else 300.0


def _array_after(text, name, nested=False):
    i = text.index(name)
    j = text.index("}[i - 1]", i)
    return text[i:j + 1]


def settings(intensity):
    """SerpentTimeCourse.ForIntensity with the shell and pole HeadlongCourseSource.BuildCircuit sets, parsed from the C#.
    The floor is computed in float32 exactly as the C# does (factor x FullStackRadius)."""
    import numpy as np
    f = np.float32
    t = _read(COURSE_CS)
    i = max(1, min(4, int(intensity))) - 1
    profile_block = _array_after(t, "CornerProfile = new[]")
    rows = re.findall(r"new\[\]\s*\{([^{}]*)\}", profile_block)
    profile = [_floats(r) for r in rows if r.strip()][i]

    def pick(key):
        block = _array_after(t, key + " = new[]")
        return _floats(block[block.index("{"):block.index("}")])[i]

    h = hull()
    full = f(h["scaler"]) * (f(1.0) + (f(h["mult"]) - f(1.0)) * f(h["tank"])) + f(h["min"])      # SpeedWithPellets(4)
    rate = full * f(h["rot"]) + f(min(h["pitch"], h["yaw"]))
    full_radius = full / (rate * f(math.pi / 180.0))                                               # FullStackRadius
    base = float(_floats(re.search(r"BaseRadius\s*=\s*([0-9.]+)f\s*;", t).group(1))[0])
    return {
        "GateCount": int(re.search(r"GatesPerLap\s*=\s*(\d+)\s*;", t).group(1)),
        "BaseRadius": base,
        "CornerProfile": profile,
        "CornerRadiusFactor": 0.0,
        "CornerFloorRadius": float(f(pick("CornerFloorRadius")) * full_radius),
        "RadialSwing": pick("RadialSwing"),
        "AngularSpread": pick("AngularSpread"),
        "LateralPerturbation": pick("LateralPerturbation"),
        "RingRadius": pick("RingRadius"),
        "AxisJitterDegrees": pick("AxisJitterDegrees"),
        "MaxPresentDegrees": pick("MaxPresentDegrees"),
        "InnerRadius": INNER, "OuterRadius": OUTER, "FirstGateDirection": (0.0, 1.0, 0.0),
    }


def course_for(intensity, seed):
    """One Serpentine circuit; `seed` is a course number (1, 2, ...) mapped to the seed the evaluators share."""
    import course_port as cp
    return cp.course(settings(intensity), cp.seed_for_intensity(20260915 + 104729 * int(seed), intensity), rails=False)


# ── the pellet economy and the driver ────────────────────────────────────────────────────────────────────────────

class Pellets:
    """ConsumeBoostActionExecutor: the tank (in pellets), the burn end times, the boost multiplier."""

    def __init__(self, h, kit="shipped"):
        self.h, self.kit = h, kit
        self.tank = float(h["tank"])
        self.ends = []
        self.burn = OLD_KIT["burn"] if kit == "clock" else h["burn"]
        self.lit = 0

    def held(self):
        return self.tank + 1e-4 * 4    # PelletsHeld: (fuel + FuelEpsilon) / cost, FuelEpsilon 0.0001 of a 0.25 pellet

    def consume(self, t):
        """Consume: a whole pellet in the tank burns for its duration (Time 0: x1)."""
        if self.tank + 1e-4 * 4 < 1.0:
            return False
        self.tank -= 1.0
        self.ends.append(t + max(0.05, self.burn))
        self.lit += 1
        return True

    def tick(self, t, dt):
        """Update: retire burned-out pellets; the old kit also refilled one pellet every 3.6 s."""
        self.ends = [e for e in self.ends if e > t]
        if self.kit == "clock":
            self.tank = min(float(self.h["tank"]), self.tank + dt / OLD_KIT["refill_every"])

    def boost(self):
        """ApplyMultiplier: 1 + (boostMultiplier - 1) x burning."""
        n = len(self.ends)
        return 1.0 + max(0.0, self.h["mult"] - 1.0) * n if n else 1.0


def pellet_driver(g, state, ctx, pellets, t):
    """PelletBoostAIPolicySO.Driver.Tick. `ctx`: angle (heading to the ring, deg), stick (0..1), dist, speed, breaking.
    Returns True when it lit a pellet."""
    if t < state["next_light_at"]:
        return False
    # AIBoostPolicySO.IsStraight
    if ctx["breaking"] or ctx["angle"] > g["alignDegrees"] or ctx["stick"] > g["stickBand"]:
        return False
    burning = len(pellets.ends)
    if burning >= max(1, int(round(g["maxConcurrentBurns"]))):
        return False
    # HasRunway(c, burning + 1): RunwaySeconds = distance / max(speed, 1)
    if ctx["dist"] / max(ctx["speed"], 1.0) < g["engageRunwaySeconds"] * (burning + 1):
        return False
    if pellets.held() < 1.0 + max(0, int(round(g["reservePellets"]))):
        return False
    pellets.consume(t)
    state["next_light_at"] = t + max(0.0, g["minSecondsBetweenPellets"])
    return True


# ── one race ─────────────────────────────────────────────────────────────────────────────────────────────────────

_CACHE = {}


def _consts():
    if "c" not in _CACHE:
        _CACHE["c"] = {"hull": hull(), "steer": scene_steering(), "laps": laps(), "target": gate_target(),
                       "standoff": start_standoff()}
    return _CACHE["c"]


EXIT_BAND_START = 7.0 + 6.0 * 0.5 * math.sqrt(2.0) + 1.5    # SerpentineGate.ExitBandStart: the I1 core's Reach + 1.5
TWIST_PER_GATE = 1.7                                          # SerpentTimeCluster.TwistPerGateRadians


def exit_gate(ring, axis, radius, gate_index, intensity, exit_no):
    """SerpentineGate.Exit: exit `exit_no` of a gate's shape, as (centre, axis, radius). Coil (I1-I2) has two, Maw and
    Iris three; the exit's centre sits mid-band, (ExitBandStart + r - 2) / 2 out, turned by the gate's twist."""
    count = 2 if intensity <= 2 else 3
    s = M.cross(axis, (0.0, 1.0, 0.0))
    if M.dot(s, s) < 1e-4:
        s = M.cross(axis, (1.0, 0.0, 0.0))
    s = M.unit(s)
    u = M.cross(s, axis)
    a = gate_index * TWIST_PER_GATE + 0.5 + exit_no * 2 * math.pi / count
    d = (EXIT_BAND_START + radius - 2.0) * 0.5
    c = M.add(ring, M.mul(M.add(M.mul(s, math.cos(a)), M.mul(u, math.sin(a))), d))
    return c, axis, min(26.0, (radius - 2.0 - EXIT_BAND_START) * 0.5 - 1.5), count


def race(course, genome, kit="shipped", nose_lag=True, limit=600.0, dt=DT, trace=None, race_laps=None, shapes=0,
         nose_lead=None):
    """One AI Serpent flies the race on `course` ({gates: [{p, a, r}]}) with `genome` (the pellet driver's fields).
    `shapes` (an intensity, 0 = off) is a WHAT-IF for the dirty race's standing shapes with the rifle never ready:
    SerpentTimeController.ChooseShapeLine sends the AI at the exit nearest it when it first looks (SerpentineGate.Exit),
    the aim and the missed-mouth test use that exit, and the gate still counts on its ring (ThreadsGate); a crossing
    inside the ring but outside the exit is counted as a weave hit (the burn it costs is not modelled).
    Returns {time (None = not finished), rings, of, top, mean_speed, boost_seconds (pellet-seconds burned), lit,
    misses, breakoffs, weave}.
    `nose_lead` is AIPilot.NoseLeadGain (ai_nose_lead.NoseLead.from_config: None = off, today's steering; a gain;
    {"gain", "max_extra_deg"}; or a NoseLead), stated at the fleet lag and scaled to this model's nose rate once a race;
    0 with no nose lag (nose_lag=False)."""
    C = _consts()
    h, st = C["hull"], C["steer"]
    commit_d, lead_d, through_d = st["aiCommitDistance"], st["aiApproachLead"], st["aiThroughDistance"]
    gates = [(tuple(g["p"]), M.unit(tuple(g["a"])), float(g["r"])) for g in course["gates"]]
    n = len(gates)
    nl = race_laps or C["laps"]
    total = n * nl if race_laps else max(n, int(math.ceil(C["target"] / nl)) * nl)
    pellets = Pellets(h, kit)
    drv = {"next_light_at": 0.0}
    lead_knob = NoseLead.from_config(nose_lead)
    # AIPilot.EffectiveNoseLeadGain, once a race, from THIS model's nose rate: LERP (the fleet 1.5/s, so the gain is
    # itself) while the nose lags, 0 when it has no lag (nose_lag=False, or a dt that closes the whole slerp in one step)
    g_lead = lead_knob.effective(LERP) if nose_lag and LERP * dt < 1.0 else 0.0
    lead_extra = lead_knob.max_extra_deg

    p0, a0, _ = gates[0]
    pos = M.sub(p0, M.mul(a0, C["standoff"]))
    q = M.q_look(a0)
    cmd = q
    v, t = 0.0, 0.0
    idx, locked, side, lead = 0, -1, 1.0, lead_d
    extending, ext_t = False, 0.0
    orbit = M.Orbit()
    prev = None
    stick_def = 0.0
    top = speed_sum = boost_sum = 0.0
    frames = misses = breakoffs = 0
    next_trace = 0.0

    weave, exit_for = 0, -1
    while t < limit and idx < total:
        ring_c, ring_ax, ring_r = gates[idx % n]
        ring, ax, rad = ring_c, ring_ax, ring_r
        if shapes:
            if exit_for != idx:                                     # ChooseShapeLine: the nearest exit, held for the gate
                exit_for = idx
                cnt = 2 if shapes <= 2 else 3
                best = min(range(cnt), key=lambda e: M.norm(M.sub(exit_gate(ring_c, ring_ax, ring_r, idx % n, shapes, e)[0], pos)))
                exit_now = exit_gate(ring_c, ring_ax, ring_r, idx % n, shapes, best)
            ring, ax, rad = exit_now[0], exit_now[1], exit_now[2]
        # GateRaceController's aim provider (Regatta steering): a miss re-latches and breaks off
        if prev is not None and M.plane_crossing(prev, pos, ring, ax, rad) == -1:
            locked = -1
            breakoffs += not extending
            extending, ext_t = True, 0.0
            orbit.reset()
        if idx != locked:
            locked = idx
            side = 1.0 if M.dot(M.sub(pos, ring), ax) >= 0 else -1.0
            leg_from = gates[(idx - 1) % n][0] if idx > 0 else pos
            lead = min(min(lead_d, 0.4 * M.norm(M.sub(leg_from, ring))), 0.8 * commit_d)   # ApproachLeadFor
        rate = v * h["rot"] + min(h["pitch"], h["yaw"])                    # MaxTurnRateDegreesPerSecond
        r_min = v / math.radians(rate) if v > 0 else 0.0
        commit = min(commit_d, lead + 0.5 * r_min)                          # CommitDistanceFor
        to_ring = M.sub(ring, pos)
        approaching = M.dot(to_ring, to_ring) > commit * commit
        if approaching:
            target = M.add(ring, M.mul(ax, side * lead))
        elif M.dot(to_ring, to_ring) > 1.0:
            target = M.add(ring, M.mul(M.unit(to_ring), through_d))
        else:
            target = M.sub(ring, M.mul(ax, side * through_d))

        # AIPilot: the orbit break-off
        to = M.sub(target, pos)
        heading = M.q_rot(q, M.FWD)
        run = max(max(0.0, 2 * r_min - M.CAPTURE), abs(v) * M.RUN_SECONDS)
        if extending:
            ext_t += dt
            if (M.norm(to) >= run and ext_t >= M.BREAK_MIN) or ext_t >= M.BREAK_MAX:
                extending, ext_t = False, 0.0
                orbit.reset()
        else:
            closing = M.dot(M.unit(to), heading) > M.CLOSING_COS
            unreachable = not closing and M.inside_turning_circle(to, heading, r_min)
            if unreachable or orbit.tick(to):
                extending, ext_t = True, 0.0
                orbit.reset()
                breakoffs += 1
        steer = (M.add(pos, M.mul(M.unit(M.add(heading, M.mul(M.unit(to), -M.AWAY_BIAS)), heading),
                                  run * 2 if run > 1 else M.MAX_DISTANCE)) if extending else target)

        # TickBoostPolicy, before the stick (it reads last frame's stick): the objective is the ring's centre
        pellet_driver(genome, drv, {"angle": M.angle(heading, to_ring), "stick": stick_def, "dist": M.norm(to_ring),
                                    "speed": v, "breaking": extending}, pellets, t)

        # AIPilot single stick: EasedLeftJoystickPosition = (x, y) = (a cross.y, -a cross.x)
        dist = M.sub(steer, pos)
        dsq = M.dot(dist, dist)
        if dsq <= 1e-12:
            xs, ys = 0.0, 0.0
        elif g_lead > 0.0:
            # the nose lead (AIPilot.TryNoseLead): want the NOSE turned toward the aim by g_lead times its error, past it
            # (NoseLeadDirection = slerp_beyond), and judge the error from the COMMAND's forward; the stick's frame (the
            # nose's), the roll / yaw / pitch update about the nose's axes below and the nose's slerp are untouched, and
            # the break-off's escape steer takes the lead like any aim (the stick is still computed from the nose there)
            want = tuple(slerp_beyond(heading, M.unit(dist), g_lead, lead_extra))
            xs, ys = M.stick(q, want, dsq, judged=M.unit(M.q_rot(cmd, M.FWD)))
        else:
            xs, ys = M.stick(q, M.unit(dist), dsq)                             # ys = a cross.x = -y
        stick_def = max(abs(xs), abs(ys))

        # SingleStickVesselTransformer.RotateShip: Roll, Yaw, Pitch onto the command, then the nose follows
        k = v * h["rot"]
        cmd = M.q_mul(M.q_axis(-xs * (k + h["roll"]) * dt, M.q_rot(q, M.FWD)), cmd)
        cmd = M.q_mul(M.q_axis(xs * (k + h["yaw"]) * dt, M.q_rot(q, M.UP)), cmd)
        cmd = M.q_mul(M.q_axis(ys * (k + h["pitch"]) * dt, M.q_rot(q, M.RIGHT)), cmd)
        q = M.q_slerp(q, cmd, LERP * dt) if nose_lag else cmd

        # MoveShip: speed lerps toward ThrottleScaler x boost + MinimumSpeed, along the nose
        b = pellets.boost()
        v += (h["scaler"] * b + h["min"] - v) * LERP * dt
        newpos = M.add(pos, M.mul(M.q_rot(q, M.FWD), v * dt))
        crossing = M.plane_crossing(pos, newpos, ring_c, ring_ax, ring_r)   # ThreadsGate: the gate's own ring
        if crossing == 1:
            if shapes and M.plane_crossing(pos, newpos, ring, ax, rad) != 1:
                weave += 1
            idx += 1
        elif crossing == -1:
            misses += 1
        prev, pos = pos, newpos
        top = max(top, v)
        speed_sum += v
        boost_sum += len(pellets.ends) * dt
        frames += 1
        t += dt
        pellets.tick(t, dt)
        if trace is not None and t >= next_trace:
            next_trace += 1.0
            trace.append(f"{t:6.1f}s ring {idx:2d}/{total} v {v:5.0f} burning {len(pellets.ends)} tank {pellets.tank:.1f} "
                         f"stick {xs:+.2f},{ys:+.2f} {'APPROACH' if approaching else 'through '} "
                         f"{'BREAK' if extending else ''} d {M.norm(to_ring):5.0f}")
    return {"time": round(t, 3) if idx >= total else None, "rings": idx, "of": total, "top": round(top),
            "mean_speed": round(speed_sum / max(1, frames), 1), "boost_seconds": round(boost_sum, 2),
            "lit": pellets.lit, "misses": misses, "breakoffs": breakoffs, "weave": weave}


def flat_out(course, race_laps=None):
    """The race's gate-to-gate path at a full stack (460 u/s): the denominator the evaluator scores against."""
    C = _consts()
    h = C["hull"]
    full = h["scaler"] * (1 + (h["mult"] - 1) * h["tank"]) + h["min"]
    return sum(course["legLengths"]) * (race_laps or C["laps"]) / full


# ── the recorded flights ─────────────────────────────────────────────────────────────────────────────────────────

def flight_course(d):
    """A flight record's own course (cosmic-shore.flight/3 ringList: index, x, y, z, ax, ay, az, radius)."""
    gates = [{"p": list(r[1:4]), "a": list(r[4:7]), "r": float(r[7])} for r in d["ringList"]]
    n = len(gates)
    legs = [math.dist(gates[i]["p"], gates[(i + 1) % n]["p"]) for i in range(n)]
    return {"gates": gates, "legLengths": legs}


def flights(folder, genome, kit="shipped", nose_lead=None):
    """Race the model on every recorded SerpentTime flight's own course in `folder` (2-lap, one ring a gate: the course
    since 2026-10-31) and print it beside the recorded AI rivals and the human."""
    import glob
    out = []
    for p in sorted(glob.glob(os.path.join(folder, "flight_SerpentTime_*.json"))):
        d = json.load(open(p, encoding="utf-8"))
        if d.get("laps") != 2 or len({r[7] for r in d["ringList"]}) != 1:
            continue
        c = flight_course(d)
        r = race(c, genome, kit=kit, race_laps=2, nose_lead=nose_lead)
        out.append((os.path.basename(p), d["intensity"], d.get("finished"), d.get("raceSeconds"), r))
    return out


# ── self-test ────────────────────────────────────────────────────────────────────────────────────────────────────

def self_test():
    bad = []
    shipped = read_genome(POLICY)
    # 1. the settings are the C#'s: the ring sizes the doc states, eight gates, a floor above 1.5x the cruise circle
    if [settings(i)["RingRadius"] for i in (1, 2, 3, 4)] != [72.0, 60.0, 50.0, 42.0]:
        bad.append(f"settings: ring radii {[settings(i)['RingRadius'] for i in (1, 2, 3, 4)]} are not 72/60/50/42")
    if settings(3)["CornerProfile"][:3] != [160.0, 150.0, 60.0] or settings(1)["GateCount"] != 8:
        bad.append("settings: the corner profile or gate count was not read from SerpentTimeCourse.cs")
    # 2. parity with the shipped C# (serpent_time_course_harness --parity): the same gates
    import course_port as cp
    rows = []
    if os.path.isfile(PARITY):
        with open(PARITY, encoding="utf-8") as fh:
            rows = [json.loads(line) for line in fh if line.strip()]
    if not rows:
        bad.append(f"no parity rows in {PARITY} (bash Tools/Build/serpent_time_course_harness/run.sh --parity)")
    for row in rows:
        i, seed = row["intensity"], row["seed"]
        ours = cp.course(settings(i), seed, rails=False)
        off = max(M.norm(M.sub(tuple(a["p"]), tuple(b["p"]))) for a, b in zip(ours["gates"], row["gates"]))
        aoff = max(M.norm(M.sub(tuple(a["a"]), tuple(b["a"]))) for a, b in zip(ours["gates"], row["gates"]))
        if len(ours["gates"]) != len(row["gates"]) or off > 0.05 or aoff > 1e-3 or ours["gates"][0]["r"] != row["gates"][0]["r"]:
            bad.append(f"course_port: I{i} seed {seed} gates are {off:.3f} u / axis {aoff:.4f} off the C#")
    # 3. the driver: a straight long runway lights; a turn, a full stick, a break-off or a short runway does not; the
    #    second pellet needs twice the runway; the tank stops at four; the gap holds
    h = hull()
    g = dict(shipped)
    pel, s = Pellets(h), {"next_light_at": 0.0}
    straight = {"angle": 2.0, "stick": 0.0, "dist": 2000.0, "speed": 60.0, "breaking": False}
    if not pellet_driver(g, s, straight, pel, 0.0):
        bad.append("pellet_driver: a long straight did not light a pellet")
    if pellet_driver(g, s, straight, pel, 0.1):
        bad.append("pellet_driver: lit inside minSecondsBetweenPellets")
    for name, change in (("a turn", {"angle": 40.0}), ("a full stick", {"stick": 1.0}), ("a break-off", {"breaking": True}),
                         ("a short runway", {"dist": 60.0 * 1.5 * 2 - 1})):
        p2, s2 = Pellets(h), {"next_light_at": 0.0}
        p2.consume(0.0) if name == "a short runway" else None
        if pellet_driver(g, s2, dict(straight, **change), p2, 1.0):
            bad.append(f"pellet_driver: lit on {name}")
    p3, s3 = Pellets(h), {"next_light_at": 0.0}
    lit = sum(pellet_driver(dict(g, maxConcurrentBurns=9), s3, dict(straight, dist=1e6), p3, k * 1.0) for k in range(10))
    if lit != h["tank"] or p3.boost() != 1 + (h["mult"] - 1) * h["tank"]:
        bad.append(f"Pellets: lit {lit} from a {h['tank']}-pellet tank, boost {p3.boost()}")
    # 4. the shipped AI finishes I1 and I4 courses; deterministic; it burns at most the tank's 24 pellet-seconds
    for i in (1, 4):
        r = race(course_for(i, 1), shipped)
        if r["time"] is None:
            bad.append(f"the shipped AI did not finish I{i} course 1: {r}")
        if r["boost_seconds"] > h["tank"] * h["burn"] + 0.1:
            bad.append(f"I{i}: burned {r['boost_seconds']} pellet-seconds from a tank of {h['tank'] * h['burn']}")
    a, b = race(course_for(3, 2), shipped), race(course_for(3, 2), shipped)
    if a != b:
        bad.append("not deterministic")
    # 5. negative controls: a Serpent that never lights a pellet is slower, and the old clock kit is faster
    c = course_for(2, 1)
    now, never = race(c, shipped), race(c, dict(shipped, reservePellets=h["tank"]))
    if not (now["time"] and never["time"] and never["time"] > now["time"] * 1.1 and never["lit"] == 0):
        bad.append(f"a Serpent that never burns was not slower ({never} vs {now})")
    clock = race(c, shipped, kit="clock")
    if not (clock["time"] and clock["time"] < now["time"]):
        bad.append(f"the old refill-on-a-clock kit was not faster ({clock['time']} vs {now['time']})")
    # 6. the shapes what-if: SERPENTTIME.md's exit radii (26 / 21 / 16 / 12 u), each exit inside its ring's mouth
    radii = [round(exit_gate((0, 0, 0), (0, 0, 1), settings(i)["RingRadius"], 0, i, 0)[2]) for i in (1, 2, 3, 4)]
    if radii != [26, 21, 16, 12]:
        bad.append(f"exit_gate: exit radii {radii} are not 26/21/16/12")
    for i in (1, 4):
        c, _, r, cnt = exit_gate((0, 0, 0), (0, 0, 1), settings(i)["RingRadius"], 3, i, 1)
        if M.norm(c) + r > settings(i)["RingRadius"] or cnt != (2 if i <= 2 else 3):
            bad.append(f"exit_gate: I{i}'s exit is not inside its ring")
    # 7. the AI nose lead (ai_nose_lead.py, AIPilot.NoseLeadGain) is OFF by default: today's race (nose_lead unset) is the
    #    race with NoseLead() / 0 / {} / {"gain": 0} / False, and it is the 2026-11-07 baseline measured BEFORE the lead was
    #    ported (python3 Tools/Build/serpent_time_sim.py, both genomes; a port that moves any of these re-measures)
    serpenttime = read_genome(POLICY_SERPENTTIME)
    genomes = {"shipped": shipped, "serpenttime": serpenttime}
    baseline = {  # (genome, intensity, course): race(course_for(intensity, course), genome), the lead off
        ("shipped", 1, 1): {'time': 126.133, 'rings': 16, 'of': 16, 'top': 260, 'mean_speed': 78.8, 'boost_seconds': 24.07, 'lit': 4, 'misses': 0, 'breakoffs': 0, 'weave': 0},
        ("shipped", 2, 2): {'time': 151.75, 'rings': 16, 'of': 16, 'top': 253, 'mean_speed': 75.6, 'boost_seconds': 24.05, 'lit': 4, 'misses': 0, 'breakoffs': 0, 'weave': 0},
        ("shipped", 3, 1): {'time': 176.217, 'rings': 16, 'of': 16, 'top': 260, 'mean_speed': 73.4, 'boost_seconds': 24.07, 'lit': 4, 'misses': 2, 'breakoffs': 2, 'weave': 0},
        ("shipped", 4, 4): {'time': 180.883, 'rings': 16, 'of': 16, 'top': 260, 'mean_speed': 73.1, 'boost_seconds': 24.03, 'lit': 4, 'misses': 1, 'breakoffs': 1, 'weave': 0},
        ("serpenttime", 1, 1): {'time': 150.533, 'rings': 16, 'of': 16, 'top': 260, 'mean_speed': 75.7, 'boost_seconds': 24.07, 'lit': 4, 'misses': 1, 'breakoffs': 1, 'weave': 0},
        ("serpenttime", 1, 4): {'time': 145.717, 'rings': 16, 'of': 16, 'top': 260, 'mean_speed': 76.2, 'boost_seconds': 24.03, 'lit': 4, 'misses': 1, 'breakoffs': 1, 'weave': 0},
        ("serpenttime", 3, 2): {'time': 188.983, 'rings': 16, 'of': 16, 'top': 260, 'mean_speed': 72.5, 'boost_seconds': 24.03, 'lit': 4, 'misses': 3, 'breakoffs': 3, 'weave': 0},
        ("serpenttime", 4, 2): {'time': 213.117, 'rings': 16, 'of': 16, 'top': 260, 'mean_speed': 71.1, 'boost_seconds': 24.05, 'lit': 4, 'misses': 3, 'breakoffs': 4, 'weave': 0},
    }
    for (gname, i, s), want in baseline.items():
        got = race(course_for(i, s), genomes[gname])
        if got != want:
            bad.append(f"{gname} I{i} course {s} drifted from the 2026-11-07 baseline (re-measure): {got} vs {want}")
    c = course_for(1, 1)
    for form in (NoseLead(), 0, {}, {"gain": 0}, False):
        if race(c, shipped, nose_lead=form) != baseline[("shipped", 1, 1)]:
            bad.append(f"nose_lead={form!r} is not today's race")
    # 8. the lead claim, with its controls: at I1 courses 1 and 4 the _SerpentTime genome misses a mouth through the nose
    #    lag today (150.5 s, 145.7 s; the lag-free Serpent misses nothing anywhere); the 2.5 lead, judged from the command
    #    with the nose pulled from past the aim, threads both and is faster (124.4 s, 131.0 s). The control: gain 1 (judged
    #    from the command WITHOUT the pull) still misses both and is slower than the lead (135.7 s, 148.0 s), and 2.5 with
    #    max_extra_deg 0 IS gain 1 (the command placed on the aim), so the pull past the aim is what helps. A dict with the
    #    config key is the same lead.
    for s in (1, 4):
        c, today = course_for(1, s), baseline[("serpenttime", 1, s)]
        led, one = race(c, serpenttime, nose_lead=2.5), race(c, serpenttime, nose_lead=1.0)
        if not (today["misses"] >= 1 and led["misses"] == 0 and led["time"] and led["time"] < today["time"]):
            bad.append(f"the 2.5 lead no longer threads I1 course {s} faster than today's miss: {led} vs {today}")
        if not (one["misses"] >= 1 and (one["time"] is None or one["time"] > led["time"])):
            bad.append(f"gain 1 (judged from the command, no pull) now does what the 2.5 lead does on I1 course {s}: {one}")
        if race(c, serpenttime, nose_lead=NoseLead(2.5, 0.0)) != one:
            bad.append(f"max_extra_deg 0 is not gain 1 (the command on the aim) on I1 course {s}")
        if s == 1 and race(c, serpenttime, nose_lead={"nose_lead": 2.5}) != led:
            bad.append("{'nose_lead': 2.5} is not the 2.5 lead")
    # 9. what the fleet default (2.5, 40 deg past the aim) costs THIS hull, measured 2026-11-07: at I1 every race of both
    #    genomes is faster with no miss, but at I2-I4 it DNFs 16 of 36. Shipped I2 course 2 is the shape: today 151.8 s and
    #    no miss; the lead stalls at 8 of 16 rings with the single stick pinned at (-1, -1), the roll about the nose's
    #    forward whirling the yaw and pitch axes while the hull flies away from the ring. Capped at 20 deg past the aim it
    #    threads the course (150.8 s, no miss), and every race finishes. A port that changes this re-measures the docstring.
    c, today = course_for(2, 2), baseline[("shipped", 2, 2)]
    stalled, capped = race(c, shipped, nose_lead=2.5), race(c, shipped, nose_lead=NoseLead(2.5, 20.0))
    if not (today["time"] and today["misses"] == 0 and stalled["time"] is None and stalled["rings"] < 12):
        bad.append(f"the 2.5 lead's stall on shipped I2 course 2 has changed (re-measure the docstring): {stalled}")
    if not (capped["time"] and capped["misses"] == 0 and capped["time"] <= today["time"]):
        bad.append(f"2.5 capped at 20 deg no longer threads I2 course 2 within today's {today['time']} s: {capped}")
    # 10. with no nose lag there is nothing to lead (effective gain 0): the lead changes nothing on a nose_lag=False race
    c = course_for(1, 1)
    free = race(c, shipped, nose_lag=False)
    if race(c, shipped, nose_lag=False, nose_lead=2.5) != free or free["misses"]:
        bad.append(f"the lead changed a lag-free race, or the lag-free Serpent misses: {free}")
    print("serpent_time_sim self-test: " + ("ok" if not bad else "FAILED\n  " + "\n  ".join(bad)))
    return 0 if not bad else 1


def table(label, rows):
    fin = sorted(r["time"] for r in rows if r["time"] is not None)
    med = f"{fin[len(fin) // 2]:6.1f}" if fin else "     -"
    return (f"{label:<26} {len(fin):2d}/{len(rows)}  median {med} s  top {max(r['top'] for r in rows):4d}  "
            f"mean v {sum(r['mean_speed'] for r in rows) / len(rows):5.0f}  lit {sum(r['lit'] for r in rows) / len(rows):.1f}  "
            f"misses {sum(r['misses'] for r in rows):3d}  breakoffs {sum(r['breakoffs'] for r in rows):3d}")


def nose_lead_arg(argv):
    """--nose-lead G[,EXTRA]: the AI nose lead a run adds (AIPilot.NoseLeadGain at the fleet lag and, after a comma, how
    far past the aim it may place the command, 40 deg unless given), or None: off, today's steering."""
    if "--nose-lead" not in argv:
        return None
    parts = argv[argv.index("--nose-lead") + 1].split(",")
    return NoseLead(float(parts[0]), float(parts[1]) if len(parts) > 1 else None)


def main(argv):
    if "--self-test" in argv:
        return self_test()
    kit = "clock" if "--old-kit" in argv else "shipped"
    lead = nose_lead_arg(argv)
    genomes = [("SerpentAIBoostPolicy", read_genome(POLICY)), ("_SerpentTime", read_genome(POLICY_SERPENTTIME))]
    if "--genome" in argv:
        vals = [float(x) for x in argv[argv.index("--genome") + 1].split(",")]
        genomes = [("--genome", dict(zip(GENOME_FIELDS, vals)))]
    if "--trace" in argv:
        k = argv.index("--trace")
        i, s = int(argv[k + 1]), int(argv[k + 2])
        log = []
        r = race(course_for(i, s), genomes[0][1], kit=kit, trace=log, nose_lead=lead)
        print("\n".join(log))
        print(r)
        return 0
    if "--flights" in argv:
        folder = argv[argv.index("--flights") + 1]
        for name, g in genomes:
            print(f"genome {name} ({kit} kit{'' if lead is None else f', nose lead {lead.gain:g}'}) on the recorded flights' own courses")
            for fn, i, fin, secs, r in flights(folder, g, kit, nose_lead=lead):
                print(f"  {fn[18:-5]} I{i}  model {r['time']} s ({r['rings']}/{r['of']}, misses {r['misses']}, lit "
                      f"{r['lit']})   recorded race {secs} s {'finished' if fin else 'not finished'}")
        return 0
    seeds = range(1, 1 + (int(argv[argv.index("--seeds") + 1]) if "--seeds" in argv else 6))
    print(f"Serpentine offline, one AI Serpent, {len(seeds)} courses an intensity, {kit} kit. {MODEL_LIMITS}"
          + ("" if lead is None else f". With --nose-lead each genome's row is followed by the same races with the AI nose"
                                      f" lead on: {lead!r} (AIPilot.NoseLeadGain, effective gain {lead.effective(LERP):g})."))
    for i in (1, 2, 3, 4):
        courses = [course_for(i, s) for s in seeds]
        for name, g in genomes:
            print(f"I{i} " + table(name, [race(c, g, kit=kit, shapes=i if "--shapes" in argv else 0) for c in courses]))
            if lead is not None:
                print(f"I{i} " + table(f"  + nose lead {lead.gain:g}", [race(c, g, kit=kit, shapes=i if "--shapes" in argv else 0,
                                                                       nose_lead=lead) for c in courses]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
