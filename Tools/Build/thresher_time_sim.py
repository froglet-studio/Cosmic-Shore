#!/usr/bin/env python3
"""Thresher Time raced offline by the AI Thresher, flown the way MinigameThresherTime flies it: the Thresher's own Time
game, for the AI plan's time-arcade step (Docs/AI_SYSTEM/AI_PLAN.md; the search is
Tools/Compute/evaluators/threshertime_fit.py). A MODEL: every number it prints is a model's, never a measurement of the
game. The editor is the measurement (ThresherTimeSoakRunner, THRESHERTIME.md section 4).

What it ports, and from where:

    the course    ThresherTimeCourse.ForIntensity through HeadlongCircuit.Generate (course_port.py's float32 port,
                  read-only), the race length from EndConditionOverrides.asset (thresherTimeGateTarget) and the laps
                  from MinigameThresherTime.unity (ThresherTimeController.laps), the shell 478-1080 u
    the nests     ThresherTimeNest: the core blocks of every gate (SpecFor, Build), broken when the hull, the ball or
                  the chain touches one; the gate is credited to a pilot passing within the credit radius of an OPEN
                  nest (PassesNear); an open nest grows back regrow seconds after it burst, once no hull is inside
                  its OuterReach (ThresherTimeController.TickNests). `nests=False` races the rings instead (the mode
                  before 2026-11-04, which the 2026-11-02 soak and the people's flights raced): the hull or the ball
                  crossing the mouth (IRingThreadProbe)
    the gate aim  GateRaceController.ArmRacer's provider with the shipped steering (aiCommitFromApproachPoint OFF in
                  the scene): the approach point aiApproachLead (150, at most 40% of the leg) back along the ring's
                  axis until within aiCommitDistance (135), then the through point aiThroughDistance (100) past the
                  ring on its axis
    the pilot     AIPilot (manta_redline_sim's port: the stick, TurnHardIfBehind, the orbit break-off), the SKILL
                  THROTTLE (Lerp(0.2, 0.9, intensity x 0.25) + its creep; Thresher Time sets no AIThrottleFloor), the
                  fleet's AI nose lag (LERP_AMOUNT 1.5/s), Thresher.prefab's turn law (Pitch / Yaw 120, Roll 130,
                  RotationThrottleScaler 0) and speed (15 + 110 x throttle, eased at 1.5/s)
    the sling     ThresherSlingAIPolicySO's driver line for line (WantsPlant, WantsAimedLetGo, WantsUnaimedLetGo,
                  the aimed launch through ThresherExecutor.AimAutopilotLaunch / LayAimTowards at aimPitchRate 120
                  deg/s, the launch view's spin and grip rule), on the planted orbit MEASURED from the shipped
                  ThresherChainSolver at ThresherConfig.asset's dials (Tools/Build/thresher_time_sim/run_probe.sh ->
                  plant_probe.json: orbit speed and radius against time planted and entry speed, when the grip comes
                  up, how fast an aimed launch leaves and how its physics speed falls with the ball's tug), the
                  exit buff (launchExitSpeedMultiplier x2 easing to x1 over launchExitSeconds 8 s, travel only)

THE AI NOSE LEAD (AIPilot.NoseLeadGain, PR #1786; Tools/Build/ai_nose_lead.py is the one shared piece) is race()'s
`nose_lead` knob: None (OFF, today's steering, so every number here and every caller is as it was), a gain, a {"gain",
"max_extra_deg"} dict or a NoseLead. With a gain the pilot judges its stick error from the COMMAND's forward and wants
the NOSE turned toward the aim by the effective gain times its error, past the aim when that gain is over 1 (at most
max_extra_deg past it, never past 170 deg: ai_nose_lead.slerp_beyond = AIPilot.NoseLeadDirection); the cross product
still lands in the nose's frame and the stick still turns the command about the nose's axes (manta_redline_sim.stick's
judged=). The effective gain is AIPilot.EffectiveNoseLeadGain computed ONCE a race from THIS race's nose rate (LERP
1.5/s, the fleet lag Thresher Time leaves, or `nose_rate`): max(1, gain x 1.5 / rate), and 0 on a nose with no lag
(nose_rate 0, or a rate that closes the gap in one step), so a lag-free race is the same race with the lead on or off.
The lead runs only where the stick does, the towing branch (the orbit break-off's escape steer included, and the run
after a launch, where the stick still flies the nose): never while planted, winding, armed or orbiting, where no stick
is computed and the aimed launch steers the orbit plane. Thresher Time sets no NoseLeadGain; `--nose-lead G[,EXTRA]`
prints the lead-on rows under today's.
MEASURED 2026-11-07 (`--nose-lead 2.5`; the self-test's last checks pin it): the lead cuts both ways on the Thresher.
With the SLING on the rings (the mouths the 2026-11-02 soak raced) it is what today's pilot is missing: after a launch
(2288 u/s with the exit buff) the hull, judged from its lagging nose, turns in late and passes 58-72 u from the ring's
centre (the mouth is 96-46 u, I1-I4), so today's shipped genome finishes 3 of the 16 rings courses at I1-I4 (135
launches a race, one every 4.4 s); the 2.5 lead passes 42-55 u and finishes 15 of 16 (I1 118-149 s, I2 178-196 s,
I3 157-303 s, I4 148-417 s), and judging from the command WITHOUT the pull (gain 1, the same race as 2.5 with
max_extra_deg 0) finishes 5, so the pull past the aim is what does it; a nose with no lag at all turns in too hard
toward the approach point and passes 100-130 u wide (it finishes 2). On I1 course 3 the lag-free nose finishes (330.8
s) where today's does not (3 of 16), so that DNF is the lag's, and the lead finishes it in 126.7 s with 15 missed
mouths against today's 80; the soak's own courses (`--soak`) go from 1 of 4 finished to 4 of 4. Read the aggregates,
never one race: the model is chaotic (a 1e-16 change in the wanted direction turns a 600 s DNF into a 74 s finish),
so a probe of gains 2.3-2.7 x caps 35-45 deg on six of these courses is the measurement (89 of 90 finished; gains
0.99-1.01 18 of 54). With the nests nothing finishes either way (each launch still passes 43-58 u from a core at
most ~20 u across, today 61-64 u: THRESHERTIME_MODEL section 3). In CRUISE (no sling) the lead corkscrews the hull away
as it does the Manta's: the roll stick is the yaw stick and turns the command about the nose's forward, so a command
the lead places 40-90 deg from the nose is whirled round it and the nose chases it off the course with the stick
pinned. On the rings at I1 today's cruise finishes every course (174-202 s, no missed mouth) and the 2.5 lead finishes
one of the four (course 1: 4 of 16, the hull turns away at gate 4 and never comes back; a diagnostic copy with the roll
stick zeroed finishes it with the lead on, 219.6 s); with the nests at I3-I4, where today's lagged cruise DNFs on every
course and a lag-free nose finishes 7 of the 8 (I4 course 1: 8 of 16 gates against 386.6 s), the 2.5 lead passes 1-2
gates. The sling masks the corkscrew: a hull turned 70 deg off its objective plants, and the launch snaps nose and
command together. That is AIPilot's roll rule to change, not this model's: the port stays as the game is.

NOT modelled (LIMITS): rivals, their nests and trails (a rival can open your nest, or follow you through yours); the
cage's plates (a hull or ball is taken to pass through them, so I2-I4 cost no speed, no danger-plate hit, no shield
bounce for a Blue pilot whose nests are its own colour); the crystal in every 3rd nest (the Time element's spin-up),
the cell's plants and creatures; Mass Mode's ball trail; the plant's hook (the ship is put on its orbit at the plant,
ahead of the ball, turning toward the objective); the stick's planted steer (pivotSteer); the comeback; the lobby
handicap (GateRaceHandicap); frame hitches. Taken as rigid: the ball trails the hull at rest length while towing.

    python3 Tools/Build/thresher_time_sim.py                      # the shipped genome, I1-I4 x 4 courses
    python3 Tools/Build/thresher_time_sim.py --rings              # the same, the rings instead of the nests
    python3 Tools/Build/thresher_time_sim.py --soak               # the 2026-11-02 soak's courses (seeds 7001, 7002)
    python3 Tools/Build/thresher_time_sim.py --trace 4 1          # one race (intensity, course seed), a line a second
    python3 Tools/Build/thresher_time_sim.py --nose-lead 2.5      # the same rows with the AI nose lead on, under today's
                                                                  # (a gain, or gain,max_extra_deg; --rings, --soak,
                                                                  # --fresh and --trace take it too)
    python3 Tools/Build/thresher_time_sim.py --fresh 6 --params B.json   # genomes on 6 fresh courses an intensity
    python3 Tools/Build/thresher_time_sim.py --probe              # re-run the solver probe (needs .NET 8)
    python3 Tools/Build/thresher_time_sim.py --self-test
"""
import json
import math
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "Compute", "evaluators"))
sys.path.insert(0, HERE)

import manta_redline_sim as M   # noqa: E402  (AIPilot's stick, break-off and the quaternion helpers)
from ai_nose_lead import NoseLead, slerp_beyond   # noqa: E402  (the AI nose lead, AIPilot.NoseLeadGain, one shared piece)

add, sub, mul, dot, cross, norm, unit, angle = M.add, M.sub, M.mul, M.dot, M.cross, M.norm, M.unit, M.angle
FWD, UP, RIGHT = M.FWD, M.UP, M.RIGHT

PROBE = os.path.join(HERE, "thresher_time_sim", "plant_probe.json")
POLICY = "Assets/Resources/AIBoostPolicies/ThresherAIBoostPolicy_ThresherTime.asset"
BASE_POLICY = "Assets/_SO_Assets/AI Boost Policies/ThresherAIBoostPolicy.asset"
POLICY_CS = "Assets/_Scripts/Controller/AI/Boost/ThresherSlingAIPolicySO.cs"
PREFAB = "Assets/_Prefabs/Spacevessels/Thresher.prefab"
SCENE = "Assets/_Scenes/Multiplayer Scenes/MinigameThresherTime.unity"
OVERRIDES = "Assets/Resources/EndConditionOverrides.asset"
CONFIG = "Assets/_SO_Assets/VesselActions/Thresher/ThresherConfig.asset"
COURSE_CS = "Assets/_Scripts/Controller/Arcade/ThresherTime/ThresherTimeCourse.cs"
NEST_CS = "Assets/_Scripts/Controller/Arcade/ThresherTime/ThresherTimeNest.cs"

# The genome: ThresherSlingAIPolicySO's serialized fields (the asset's names; the evaluator's space uses them).
GENOME_FIELDS = ("plantMinTurnDegrees", "plantMinDistance", "slingIntervalSeconds", "releaseConeDegrees",
                 "maxPlantSeconds", "windWhilePlanted", "aimedLaunch", "aimedHoldSeconds", "aimToleranceDegrees")
CODE_DEFAULTS = {"plantMinTurnDegrees": 70.0, "plantMinDistance": 120.0, "slingIntervalSeconds": 1.5,
                 "releaseConeDegrees": 15.0, "maxPlantSeconds": 3.0, "windWhilePlanted": 1.0, "aimedLaunch": 0.0,
                 "aimedHoldSeconds": 1.5, "aimToleranceDegrees": 3.0}

# AIPilot / ServerPlayerVesselInitializerWithAI
THROTTLE_LOW, THROTTLE_HIGH = 0.2, 0.9
CREEP_LOW, CREEP_HIGH = 0.0005, 0.001
LERP = 1.5                                   # VesselTransformer.LERP_AMOUNT: speed, and the AI nose
START_BACK = 400.0                           # a pilot starts this far before gate 0 on its axis (the sims' convention)
HULL_R = 1.1                                 # Thresher.prefab's hull box, half its 2.2 u width
SHELL = (478.0, 1080.0)                      # RaceCourseSource's shell (the nucleus x 1.22 / the fallback 480, 1080)
LIMITS = ("Thresher Time offline (thresher_time_sim.py): the shipped course, nests (core only), gate aim, AIPilot "
          "stick / skill throttle / orbit break-off / nose lag, the sling policy's driver and aimed launch on the shipped "
          "chain solver's measured orbit and launch, the exit buff; one AI, no rivals, no cage plates, no reward "
          "crystals, no plants or creatures, no hook, no lobby handicap")


# ── what the model reads from the repo (never retyped) ─────────────────────────────────────────────────────────────

def _read(rel):
    with open(os.path.join(REPO, rel), encoding="utf-8") as fh:
        return fh.read()


def _yaml_float(text, key, default=None):
    m = re.search(r"^\s+" + re.escape(key) + r":\s*(-?[0-9.]+(?:e-?\d+)?)\s*$", text, re.M)
    return float(m.group(1)) if m else default


def read_genome(rel):
    """A ThresherSlingAIPolicySO asset's genome, the C# initialisers for any field it does not store."""
    path = os.path.join(REPO, rel)
    if not os.path.isfile(path):
        return None
    text = _read(rel)
    return {k: _yaml_float(text, k, CODE_DEFAULTS[k]) for k in GENOME_FIELDS}


def _scene_list(text, key):
    m = re.search(r"^  " + re.escape(key) + r":\s*\n((?:  - .*\n)+)", text, re.M)
    return [float(x.split("-", 1)[1]) for x in m.group(1).strip().splitlines()] if m else None


def _shipped():
    scene = _read(SCENE)
    prefab = _read(PREFAB)
    cfg = _read(CONFIG)
    out = {
        "laps": int(_yaml_float(scene, "laps")),
        "gates": int(_yaml_float(_read(OVERRIDES), "thresherTimeGateTarget")),
        "commit": _yaml_float(scene, "aiCommitDistance"), "lead": _yaml_float(scene, "aiApproachLead"),
        "through": _yaml_float(scene, "aiThroughDistance"),
        "credit": _scene_list(scene, "creditRadiusByIntensity"),
        "regrow": _scene_list(scene, "regrowSecondsByIntensity"),
        "min_speed": _yaml_float(prefab, "DefaultMinimumSpeed"), "scaler": _yaml_float(prefab, "DefaultThrottleScaler"),
        "pitch": _yaml_float(prefab, "PitchScaler"), "yaw": _yaml_float(prefab, "YawScaler"),
        "roll": _yaml_float(prefab, "RollScaler"), "rot_throttle": _yaml_float(prefab, "RotationThrottleScaler"),
        "aim_pitch_rate": _yaml_float(cfg, "aimPitchRate"), "engage": _yaml_float(cfg, "spectateEngageSeconds"),
        "release_fraction": _yaml_float(cfg, "spectateReleaseFraction"),
    }
    if "aiCommitFromApproachPoint: 1" in scene:
        raise RuntimeError("MinigameThresherTime turned on aiCommitFromApproachPoint: port the Regatta steering here")
    return out


SHIPPED = _shipped()


# ── the course: ThresherTimeCourse.ForIntensity ────────────────────────────────────────────────────────────────────

def _course_consts():
    src = _read(COURSE_CS)
    get = lambda name: float(re.search(r"const float " + name + r"\s*=\s*([0-9.]+)f", src).group(1))
    turn, rot = get("ThresherTurnScaler"), get("ThresherRotationThrottleScaler")
    ceiling = get("MaxBallSandbox") * get("GameCruise") / get("SandboxCruise")
    full = ceiling / ((ceiling * rot + turn) * math.pi / 180.0)        # FullSlingRadius
    return full, get("BaseRadius") if "BaseRadius" in src else 800.0


def thresher_time_settings(intensity):
    """ThresherTimeCourse.ForIntensity as plain numbers (course_port's settings dict). ThresherTimeCourse.cs is read
    for the hull constants; the arrays below MIRROR its ForIntensity (the self-test checks them against the file)."""
    i = min(max(int(intensity), 1), 4) - 1
    full = FULL_SLING_RADIUS
    return {
        "GateCount": max(3, -(-SHIPPED["gates"] // SHIPPED["laps"])), "BaseRadius": 800.0,
        "CornerProfile": [[125, 90, 60, 40, 25, 12, 5, 3], [110, 90, 60, 40, 25, 18, 12, 5],
                          [150, 135, 60, 6, 4, 3, 2, 1], [140, 135, 70, 6, 4, 3, 2, 1]][i],
        "CornerRadiusFactor": 0.0, "CornerFloorRadius": [0.80, 0.50, 0.45, 0.42][i] * full,
        "RadialSwing": 0.42, "AngularSpread": [1.2, 4.0, 2.0, 4.0][i],
        "LateralPerturbation": [120.0, 170.0, 215.0, 260.0][i], "RingRadius": [96.0, 72.0, 58.0, 46.0][i],
        "AxisJitterDegrees": [20.0, 28.0, 36.0, 44.0][i], "MaxPresentDegrees": [50.0, 70.0, 72.0, 76.0][i],
        "InnerRadius": SHELL[0], "OuterRadius": SHELL[1], "FirstGateDirection": (0.0, 1.0, 0.0),
    }


FULL_SLING_RADIUS, _ = _course_consts()


def course_for(intensity, seed, raw_seed=False):
    import course_port as cp
    s = seed if raw_seed else cp.seed_for_intensity(20261106 + 104729 * seed, intensity)
    return cp.course(thresher_time_settings(intensity), s, rails=False)


# ── the nests: ThresherTimeNest (core blocks only) ─────────────────────────────────────────────────────────────────

NEST_SPEC = {1: (7, 6.0, 7.0, 0.0, 0.0), 2: (3, 5.0, 4.0, 18.0, 0.0), 3: (1, 5.0, 0.0, 16.0, 0.0),
             4: (1, 3.5, 0.0, 14.0, 30.0)}   # (core blocks, cube, spacing, cage radius, halo radius): SpecFor
PLATE_Z = 5.0


def outer_reach(intensity):
    blocks, cube, spacing, cage, halo = NEST_SPEC[intensity]
    core = (spacing if blocks > 1 else 0.0) + cube
    return max(core, cage + PLATE_Z if cage > 0 else 0.0, halo + PLATE_Z if halo > 0 else 0.0)


def core_blocks(gate_p, gate_a, gate_index, intensity):
    """ThresherTimeNest.Build's core: one block at the centre, the rest round it across the axis."""
    blocks, cube, spacing, _, _ = NEST_SPEC[intensity]
    axis = unit(gate_a)
    ref = (0.0, 1.0, 0.0) if abs(axis[1]) < 0.9 else (1.0, 0.0, 0.0)
    across = unit(cross(axis, ref))
    across = M.q_rot(M.q_axis(gate_index * 37.0, axis), across)
    up = unit(cross(across, axis))
    out = [tuple(gate_p)]
    for k in range(1, blocks):
        a = (k - 1) * 2 * math.pi / (blocks - 1)
        out.append(add(gate_p, add(mul(across, math.cos(a) * spacing), mul(up, math.sin(a) * spacing))))
    return out, cube


def seg_dist(a, b, p):
    return M.seg_point(a, b, p)


# ── the solver probe ───────────────────────────────────────────────────────────────────────────────────────────────

def run_probe():
    out = subprocess.run(["bash", os.path.join(HERE, "thresher_time_sim", "run_probe.sh")], capture_output=True,
                         text=True, cwd=REPO)
    if out.returncode != 0:
        raise RuntimeError("the solver probe failed:\n" + out.stderr[-2000:])
    data = json.loads(out.stdout)
    with open(PROBE, "w", encoding="utf-8") as fh:
        json.dump(data, fh, separators=(",", ":"))
        fh.write("\n")
    return data


def _load_probe():
    if not os.path.isfile(PROBE):
        return {"plant": {"1": []}, "launch": [], "exitMultiplier": 1, "exitSeconds": 0, "launchBoost": 1,
                "spectateSpinRate": 0.9, "restLength": 22.1}   # --probe writes it
    with open(PROBE, encoding="utf-8") as fh:
        d = json.load(fh)
    plant = {float(k): v for k, v in d["plant"].items()}
    d["plant_speeds"] = sorted(plant)
    d["plant_rows"] = plant
    return d


PROBE_DATA = _load_probe()
DT_PROBE = 1.0 / 60.0


def _interp(xs, ys, x):
    if x <= xs[0]:
        return ys[0]
    if x >= xs[-1]:
        return ys[-1]
    for k in range(1, len(xs)):
        if x <= xs[k]:
            t = (x - xs[k - 1]) / (xs[k] - xs[k - 1])
            return ys[k - 1] + (ys[k] - ys[k - 1]) * t
    return ys[-1]


def orbit_at(entry_speed, planted):
    """(orbit speed, radius, spin, grip) `planted` seconds into a plant entered at `entry_speed`: the probe's rows,
    linear between its entry speeds. Above the fastest probed entry the orbit is scaled by speed (lockKeep 1: the
    orbit starts at the speed it was entered with); the radius and grip are the fastest row's."""
    speeds = PROBE_DATA["plant_speeds"]
    rows = PROBE_DATA["plant_rows"]
    k = min(len(rows[speeds[0]]) - 1, max(0, int(planted / DT_PROBE)))

    def at(sp):
        r = rows[sp][k]
        return r[2], r[3] if r[3] > 0 else r[4], r[6]
    if entry_speed <= speeds[0]:
        v, rad, grip = at(speeds[0])
        v *= max(0.3, entry_speed / speeds[0])
    elif entry_speed >= speeds[-1]:
        v, rad, grip = at(speeds[-1])
    else:
        hi = next(s for s in speeds if s >= entry_speed)
        lo = speeds[speeds.index(hi) - 1]
        t = (entry_speed - lo) / (hi - lo)
        (v0, r0, g0), (v1, r1, g1) = at(lo), at(hi)
        v, rad, grip = v0 + (v1 - v0) * t, r0 + (r1 - r0) * t, min(g0, g1)
    v = min(v, PROBE_DATA.get("lockCeiling", 1e9))
    return v, rad, v / rad if rad > 1e-3 else 0.0, grip


def launch_profile(entry_speed, hold):
    """(left_at, [physics speed every 0.25 s for 3 s]) of an aimed launch armed `hold` s into a plant entered at
    `entry_speed`: the probe's launch rows, nearest in hold, linear between its probed entry speeds (clamped at both
    ends). The fall after it is the chain drawing the ball up from the pivot (yank 0): the hull is braked within a
    quarter second, which the exit buff's carry leaves as it is (THRESHER.md 5a)."""
    rows = PROBE_DATA["launch"]
    by = {}
    for r in rows:
        best = by.get(r["cruise"])
        if best is None or abs(r["hold"] - hold) < abs(best["hold"] - hold):
            by[r["cruise"]] = r
    cs = sorted(by)
    if entry_speed <= cs[0]:
        r = by[cs[0]]
        return r["leftAt"], r["after"]
    if entry_speed >= cs[-1]:
        r = by[cs[-1]]
        return r["leftAt"], r["after"]
    hi = next(c for c in cs if c >= entry_speed)
    lo = cs[cs.index(hi) - 1]
    t = (entry_speed - lo) / (hi - lo)
    a, b = by[lo], by[hi]
    return (a["leftAt"] + (b["leftAt"] - a["leftAt"]) * t, [x + (y - x) * t for x, y in zip(a["after"], b["after"])])


EXIT_MULT, EXIT_SECONDS = PROBE_DATA["exitMultiplier"], PROBE_DATA["exitSeconds"]
BOOST, SPIN_RATE, REST = PROBE_DATA["launchBoost"], PROBE_DATA["spectateSpinRate"], PROBE_DATA["restLength"]


def _dials():
    text = _read(CONFIG)
    k = _yaml_float(text, "gameCruise") / _yaml_float(text, "sandboxCruise")
    d = {name: _yaml_float(text, name) for name in ("restLen", "maxLen", "ballR", "maxBall", "ballDrag", "ballMass",
                                                    "tug", "chainSlackRate", "chainRigidRate", "chainDamping",
                                                    "rigidityCurve", "rigiditySlew")}
    return {"rest": d["restLen"] * k, "max": d["maxLen"] * k, "ball_r": d["ballR"] * k, "max_ball": d["maxBall"] * k,
            "drag": d["ballDrag"], "share": d["tug"] * d["ballMass"] / (1 + d["ballMass"]), "slack": d["chainSlackRate"],
            "rigid": d["chainRigidRate"], "damping": d["chainDamping"], "curve": d["rigidityCurve"],
            "slew": d["rigiditySlew"]}


CHAIN = _dials()


def chain_rate(rigidity):
    """ThresherChainSolver.ChainRate."""
    return CHAIN["slack"] * (CHAIN["rigid"] / CHAIN["slack"]) ** (max(0.0, min(1.0, rigidity)) ** CHAIN["curve"])


def tension(dist, radial_speed, rigidity):
    """ThresherChainSolver.Tension: the chain's pull per unit mass (a damped spring past rest, a stiff limit past
    the full length)."""
    w, a = chain_rate(rigidity), 0.0
    if dist > CHAIN["rest"]:
        a += w * w * (dist - CHAIN["rest"]) + 2 * CHAIN["damping"] * w * radial_speed
    if dist > CHAIN["max"]:
        wr = 2 * CHAIN["rigid"]
        a += wr * wr * (dist - CHAIN["max"]) + 1.4 * wr * max(0.0, radial_speed)
    return max(0.0, a)


def tow_ball(ball, ball_v, ship_p, ship_v, rigidity, dt):
    """ThresherChainSolver.StepAnalog's ball, towing with LT at rest: the chain a spring at the current rigidity,
    sub-stepped; the ship's own share of the pull (tug x m / (1 + m), ~6%) is left out (the hull is the flight
    model's)."""
    w_max = max(chain_rate(rigidity), 2 * CHAIN["rigid"] if norm(sub(ball, ship_p)) > CHAIN["max"] * 0.9 else 0.0)
    steps = max(1, int(math.ceil(w_max * dt / 0.15)))
    h = dt / steps
    keep = math.exp(-CHAIN["drag"] * h)
    for _ in range(steps):
        d = sub(ball, ship_p)
        dist = norm(d)
        nn = mul(d, 1.0 / dist) if dist > 1e-5 else (0.0, 0.0, 0.0)
        a = tension(dist, dot(sub(ball_v, ship_v), nn), rigidity) if dist > 1e-5 else 0.0
        ball_v = sub(ball_v, mul(nn, a * (1 - CHAIN["share"]) * h))
        ball_v = mul(ball_v, keep)
        sp = norm(ball_v)
        if sp > CHAIN["max_ball"]:
            ball_v = mul(ball_v, CHAIN["max_ball"] / sp)
        ball = add(ball, mul(ball_v, h))
        ship_p = add(ship_p, mul(ship_v, h))
    return ball, ball_v


def exit_multiplier(t):
    """ThresherChainSolver.LaunchExitMultiplier."""
    return EXIT_MULT + (1.0 - EXIT_MULT) * (t / EXIT_SECONDS) if 0.0 <= t < EXIT_SECONDS else 1.0


def rotate_towards(a, b, max_rad):
    a, b = unit(a), unit(b)
    ang = math.acos(max(-1.0, min(1.0, dot(a, b))))
    if ang <= max_rad or ang < 1e-6:
        return b
    ax = cross(a, b)
    if dot(ax, ax) < 1e-12:
        ax = cross(a, RIGHT) if abs(a[0]) < 0.9 else cross(a, UP)
    ax = unit(ax)
    c, s = math.cos(max_rad), math.sin(max_rad)
    return unit(add(add(mul(a, c), mul(cross(ax, a), s)), mul(ax, dot(ax, a) * (1 - c))))


def project_on_plane(v, n):
    d = dot(n, n)
    return v if d < 1e-15 else sub(v, mul(n, dot(v, n) / d))


def lay_aim_towards(aim_up, to_target, max_rad):
    """ThresherCameraFraming.LayAimTowards: tilt the orbit normal toward the plane holding the target; the aim laid
    into that plane."""
    if dot(to_target, to_target) < 1e-8:
        return aim_up, None
    target = unit(to_target)
    want = project_on_plane(aim_up, target)
    if dot(want, want) > 1e-8:
        aim_up = rotate_towards(aim_up, want, max(0.0, max_rad))
    aim = project_on_plane(target, aim_up)
    return aim_up, (unit(aim) if dot(aim, aim) > 1e-6 else None)


# ── one race ───────────────────────────────────────────────────────────────────────────────────────────────────────

def race(course, intensity, genome=None, nests=True, sling=True, limit=600.0, dt=1 / 60, laps=None, trace=None, trace_step=1.0,
         breakable=True, nose_rate=None, nose_lead=None):
    """One AI Thresher flies the race on `course` ({gates: [{p, a, r}]}) with the sling genome `genome` (a dict of
    GENOME_FIELDS; None = the shipped Thresher Time asset). `sling=False` is the cruise-only control (a seat the
    policy does not drive). Returns {time (None = not finished), gates, of, plants, launches, launch_speed (median,
    with the exit buff, as the soak's Speed reads), launch_off (median degrees off the next ring), nest_breaks,
    misses (a pass by an unbroken nest, or a mouth missed), top}.
    `nose_rate` is VesselTransformer.AINoseFollowPerSecond, the rate a mode may set for an AI's nose: None = unset (the
    fleet lag, LERP 1.5/s, which Thresher Time leaves), 0 = no lag (the nose IS the command), > 0 = that rate per second,
    applied as Mathf.Min(1, rate x dt). `nose_lead` is AIPilot.NoseLeadGain (ai_nose_lead.NoseLead.from_config: None =
    off, a gain, {"gain", "max_extra_deg"} or a NoseLead), stated at the fleet lag and scaled to this race's nose rate
    once (AIPilot.EffectiveNoseLeadGain); 0 with no nose lag. Off by default: today's race."""
    g = dict(read_genome(POLICY) if genome is None else genome)
    sh = SHIPPED
    gates = [(tuple(x["p"]), unit(tuple(x["a"])), float(x["r"])) for x in course["gates"]]
    n = len(gates)
    total = n * (laps or sh["laps"])
    i4 = min(max(int(intensity), 1), 4)
    credit = sh["credit"][i4 - 1]
    regrow = sh["regrow"][i4 - 1]
    reach = outer_reach(i4) + 10.0
    cores = [core_blocks(p, a, k, i4) for k, (p, a, _) in enumerate(gates)]
    nest_open_until = [-1.0] * n     # -1 standing; else open (regrows at that time, or later while a hull is inside)

    skill = max(0.0, min(1.0, i4 * 0.25))
    throttle = THROTTLE_LOW + (THROTTLE_HIGH - THROTTLE_LOW) * skill
    creep = CREEP_LOW + (CREEP_HIGH - CREEP_LOW) * skill
    turn_rate = min(sh["pitch"], sh["yaw"])   # RotationThrottleScaler 0: flat
    # the AI nose: the fleet lag (LERP) unless the mode set a rate, closed by Mathf.Min(1, rate x dt) a frame; and the
    # nose lead's effective gain ONCE a race from THIS race's nose rate (AIPilot.EffectiveNoseLeadGain: max(1, gain x 1.5
    # / rate)), 0 when the nose has no lag (nose_rate 0, or a rate that closes the whole gap in one step)
    follow = LERP if nose_rate is None or nose_rate < 0 else float(nose_rate)
    follow_step = min(1.0, follow * dt) if follow > 0 else 1.0
    lead_knob = NoseLead.from_config(nose_lead)
    g_lead = lead_knob.effective(follow) if follow_step < 1.0 else 0.0
    lead_extra = lead_knob.max_extra_deg

    p0, a0, _ = gates[0]
    pos = sub(p0, mul(a0, START_BACK))
    q = M.q_look(a0)
    cmd = q
    v, t = 0.0, 0.0
    idx, locked, side, lead = 0, -1, 1.0, sh["lead"]
    extending, ext_t = False, 0.0
    orbit = M.Orbit()

    # the sling driver's state (ThresherSlingAIPolicySO.Driver) and the planted orbit
    planted, winding, aiming = False, False, False
    planted_at, let_go_at = 0.0, -1e9
    pivot = None
    orb_u = orb_w = None            # orbit plane: the ship is at pivot + r (cos th u + sin th w)
    theta, entry_speed = 0.0, 0.0
    view_up, qualify_t, drop_t = False, 0.0, 0.0
    aim_up, aim_dir, arm_aim, arm_hold = None, None, None, 0.0
    launch_t, launch_v0, launch_after = -1.0, 0.0, None   # the aimed launch's clock and measured physics speed
    plain_v = None                                       # a plain release's speed, eased to cruise
    ball = sub(pos, mul(a0, REST))
    ball_v = (0.0, 0.0, 0.0)
    rigidity = 0.0

    plants = launches = breaks = misses = 0
    launch_speeds, launch_offs, launch_miss = [], [], []
    miss_now = None
    top = 0.0
    next_trace = 0.0

    def aim_target(self_pos):
        nonlocal locked, side, lead
        ring, ax, _ = gates[idx % n]
        if idx != locked:
            locked = idx
            side = 1.0 if dot(sub(self_pos, ring), ax) >= 0 else -1.0
            leg_from = gates[(idx - 1) % n][0] if idx > 0 else self_pos
            lead = min(sh["lead"], 0.4 * norm(sub(leg_from, ring)))     # ApproachLeadFor (shipped steering)
        to_ring = sub(ring, self_pos)
        if dot(to_ring, to_ring) > sh["commit"] ** 2:
            return add(ring, mul(ax, side * lead)), True
        return sub(ring, mul(ax, side * sh["through"])), False

    while t < limit and idx < total:
        target, approaching = aim_target(pos)
        to = sub(target, pos)
        # the boost driver judges the OBJECTIVE the mode reported, the ring itself (AIPilot.TickBoostPolicy:
        # ObjectivePosition, gate.Position), never the steering point
        to_obj = sub(gates[idx % n][0], pos)
        prev_pos, prev_ball = pos, ball

        if planted or arm_aim is not None:
            # ── planted: the orbit (the probe's), the launch view, the driver's let-go ───────────────────────────
            tp = t - planted_at
            ov, orad, spin, grip = orbit_at(entry_speed, tp)
            theta += ov / max(1e-3, orad) * dt
            radial = add(mul(orb_u, math.cos(theta)), mul(orb_w, math.sin(theta)))
            tangent = add(mul(orb_u, -math.sin(theta)), mul(orb_w, math.cos(theta)))
            newpos = add(pivot, mul(radial, orad))
            heading = tangent
            v = ov
            # UpdateLaunchView: both triggers full on a fast enough spin, held spectateEngageSeconds
            both_full = winding and grip and arm_aim is None
            qualifies = both_full and spin >= SPIN_RATE
            qualify_t = qualify_t + dt if qualifies else 0.0
            if not view_up:
                if qualifies and qualify_t >= sh["engage"]:
                    view_up = True
            elif arm_aim is None and (spin < SPIN_RATE * sh["release_fraction"] or not winding):
                drop_t += dt
                if drop_t >= 0.25:
                    view_up = False
            if arm_aim is not None:
                # an ARMED launch: keep circling to the point whose tangent is the aim, then leave along it
                if dot(tangent, arm_aim) >= math.cos(ov / max(1e-3, orad) * dt * 1.01) or t - arm_hold > 8.0:
                    left, after = launch_profile(entry_speed, arm_hold - planted_at)
                    launches += 1
                    launch_t, launch_v0, launch_after = 0.0, left, after
                    launch_speeds.append(left * EXIT_MULT)
                    nxt = gates[idx % n][0]
                    launch_offs.append(angle(arm_aim, sub(nxt, newpos)))
                    if miss_now is not None:
                        launch_miss.append(miss_now)
                    miss_now = 1e9
                    q = cmd = M.q_look(arm_aim)
                    heading = arm_aim
                    v = left
                    planted, arm_aim, view_up, aiming = False, None, False, False
                    ball, ball_v = pivot, (0.0, 0.0, 0.0)          # yank 0: the ball leaves the pivot at rest
            else:
                if g["aimedLaunch"] >= 0.5 and g["windWhilePlanted"] >= 0.5 and winding and view_up:
                    aiming = True
                    if aim_up is None:
                        aim_up = unit(cross(orb_u, orb_w))
                    aim_up, aim_dir = lay_aim_towards(aim_up, to_obj, math.radians(sh["aim_pitch_rate"]) * dt)
                    if aim_dir is not None:
                        # SteerOrbitPlane: the orbit is laid into the plane the aim's normal names
                        nu = unit(project_on_plane(radial, aim_up))
                        orb_u, orb_w = nu, unit(cross(aim_up, nu))
                        theta = 0.0
                    err = angle(aim_dir, to_obj) if aim_dir is not None else 180.0
                    if tp >= g["maxPlantSeconds"] or (tp >= g["aimedHoldSeconds"] and err <= g["aimToleranceDegrees"]):
                        let_go_at = t
                        winding = False
                        if aim_dir is not None:
                            arm_aim, arm_hold = aim_dir, t       # LetGo with the view up arms the aimed launch
                        else:
                            planted = False
                else:
                    off = angle(heading, to_obj)
                    aims = g["aimedLaunch"] >= 0.5 and g["windWhilePlanted"] >= 0.5
                    go = (tp >= g["maxPlantSeconds"] or (tp >= g["aimedHoldSeconds"] and off <= g["releaseConeDegrees"])) \
                        if aims else (off <= g["releaseConeDegrees"] or tp >= g["maxPlantSeconds"])
                    if go:
                        let_go_at = t
                        planted = winding = False
                        plain_v = ov                             # a plain release: along the tangent, at the orbit's
                        q = cmd = M.q_look(tangent)
                        launch_t = -1.0
            rigidity = min(1.0, rigidity + CHAIN["slew"] * dt) if winding else max(0.0, rigidity - CHAIN["slew"] * dt)
            if planted or arm_aim is not None:
                ball = pivot
                pos = newpos
            else:
                pos = newpos
                aim_up = aim_dir = None
        else:
            # ── towing: AIPilot flies the hull ───────────────────────────────────────────────────────────────────
            heading = M.q_rot(q, FWD)
            r_min = v / math.radians(turn_rate) if v > 0 else 0.0
            run = max(max(0.0, 2 * r_min - M.CAPTURE), abs(v) * M.RUN_SECONDS)
            if extending:
                ext_t += dt
                if (norm(to) >= run and ext_t >= M.BREAK_MIN) or ext_t >= M.BREAK_MAX:
                    extending, ext_t = False, 0.0
                    orbit.reset()
            else:
                closing = dot(unit(to), heading) > M.CLOSING_COS
                if (not closing and M.inside_turning_circle(to, heading, r_min)) or orbit.tick(to):
                    extending, ext_t = True, 0.0
                    orbit.reset()
            if extending:
                escape = unit(add(heading, mul(unit(to), -M.AWAY_BIAS)), heading)
                steer = add(pos, mul(escape, run * 2 if run > 1 else M.MAX_DISTANCE))
            else:
                steer = target
            dist = sub(steer, pos)
            dsq = dot(dist, dist)
            if dsq <= 1e-12:
                xs, ys = 0.0, 0.0
            elif g_lead > 0.0:
                # the nose lead (AIPilot.TryNoseLead): want the NOSE turned toward the aim by g_lead times its error, past
                # it (NoseLeadDirection = slerp_beyond), and judge the error from the COMMAND's forward; the cross product
                # still lands in the nose's frame, the command update below still turns about the nose's axes, and the
                # break-off's escape steer takes the lead like any aim. Never here while planted (no stick runs there).
                want = tuple(slerp_beyond(heading, unit(dist), g_lead, lead_extra))
                xs, ys = M.stick(q, want, dsq, judged=unit(M.q_rot(cmd, FWD)))
            else:
                xs, ys = M.stick(q, unit(dist), dsq)
            thr = max(0.0, min(1.0, throttle))
            throttle += creep * dt

            # the sling driver: plant?
            if sling and not extending and t - let_go_at >= g["slingIntervalSeconds"] and norm(to_obj) >= g["plantMinDistance"] \
                    and angle(heading, to_obj) >= g["plantMinTurnDegrees"]:
                planted, planted_at, plants = True, t, plants + 1
                winding = g["windWhilePlanted"] >= 0.5
                entry_speed = v
                ov, orad, _, _ = orbit_at(entry_speed, 0.0)
                side_dir = project_on_plane(to_obj, heading)
                side_dir = unit(side_dir) if dot(side_dir, side_dir) > 1e-6 else M.q_rot(q, RIGHT)
                # the ship ahead of the ball on its line, turning toward the objective
                orb_u, orb_w = heading, side_dir
                pivot = sub(pos, mul(heading, orad))
                theta = 0.0
                view_up, qualify_t, drop_t, aim_up, aim_dir, aiming = False, 0.0, 0.0, None, None, False
                launch_t, plain_v = -1.0, None
                newpos = pos
            else:
                nose_up = M.q_rot(q, UP)
                cmd = M.q_mul(M.q_axis(xs * sh["roll"] * dt, M.q_rot(q, FWD)), cmd)
                cmd = M.q_mul(M.q_axis(xs * sh["yaw"] * dt, nose_up), cmd)
                cmd = M.q_mul(M.q_axis(ys * sh["pitch"] * dt, M.q_rot(q, RIGHT)), cmd)
                q = M.q_slerp(q, cmd, follow_step)
                cruise = sh["min_speed"] + thr * sh["scaler"]
                travel = 1.0
                if launch_t >= 0.0:
                    launch_t += dt
                    k = launch_t / 0.25
                    pts = [launch_v0] + list(launch_after)
                    phys = pts[min(int(k), len(pts) - 1)] if int(k) + 1 >= len(pts) else \
                        pts[int(k)] + (pts[int(k) + 1] - pts[int(k)]) * (k - int(k))
                    if int(k) + 1 >= len(pts):
                        v += (cruise - v) * LERP * dt
                    else:
                        v = phys
                    travel = exit_multiplier(launch_t)
                    if travel <= 1.0 and launch_t > 3.0:
                        launch_t = -1.0
                elif plain_v is not None:
                    v, plain_v = plain_v, None
                else:
                    v += (cruise - v) * LERP * dt
                newpos = add(pos, mul(M.q_rot(q, FWD), v * travel * dt))
                heading = M.q_rot(q, FWD)
            if planted:
                ball, ball_v = pivot, (0.0, 0.0, 0.0)
            else:
                rigidity = max(0.0, rigidity - CHAIN["slew"] * dt)      # RT up while towing
                ball, ball_v = tow_ball(ball, ball_v, pos, mul(heading, v), rigidity, dt)
                if travel > 1.0:   # the exit buff carries the rig with the hull (ThresherExecutor.CarryRig)
                    ball = add(ball, mul(heading, v * (travel - 1.0) * dt))
            pos = newpos

        top = max(top, v)
        if miss_now is not None and 0.0 <= launch_t < 1.5:
            miss_now = min(miss_now, seg_dist(prev_pos, pos, gates[idx % n][0]))

        # ── the gates: nests broken by the hull, the ball or the chain; credit within the radius of an open nest ──
        if nests:
            for k in range(n):
                if nest_open_until[k] >= 0:
                    if t >= nest_open_until[k] and norm(sub(pos, gates[k][0])) > reach:
                        nest_open_until[k] = -1.0
                    continue
                if norm(sub(pos, gates[k][0])) > 200.0 + v * dt * 2 and norm(sub(ball, gates[k][0])) > 200.0:
                    continue
                blocks, cube = cores[k]
                hit = False
                for c in (blocks if breakable else ()):
                    if seg_dist(prev_pos, pos, c) <= cube * 0.5 + HULL_R or seg_dist(prev_ball, ball, c) <= cube * 0.5 + CHAIN["ball_r"] \
                            or seg_dist(ball, pos, c) <= cube * 0.5 + CHAIN["ball_r"]:
                        hit = True
                        break
                if hit:
                    breaks += 1
                    nest_open_until[k] = t + regrow
            ring = gates[idx % n][0]
            if nest_open_until[idx % n] >= 0 and seg_dist(prev_pos, pos, ring) <= credit:
                idx += 1
            elif seg_dist(prev_pos, pos, ring) <= cores[idx % n][1] * 4 and nest_open_until[idx % n] < 0:
                misses += 1                                     # past the core of a nest it did not break
        else:
            ring, ax, rad = gates[idx % n]
            c1 = M.plane_crossing(prev_pos, pos, ring, ax, rad)
            c2 = M.plane_crossing(prev_ball, ball, ring, ax, rad) if not planted else 0
            if c1 == 1 or c2 == 1:
                idx += 1
            elif c1 == -1:
                misses += 1
        t += dt
        if trace is not None and t >= next_trace:
            next_trace += trace_step
            trace.append(f"{t:6.2f}s gate {idx:2d}/{total} v {v:6.0f} {'PLANT' if planted else ('ARMED' if arm_aim else 'tow  ')} "
                         f"{'view' if view_up else '    '} {'APPROACH' if approaching else 'through '} "
                         f"d {norm(to_obj):5.0f} off {angle(heading, to_obj):5.1f} breaks {breaks}")

    med = lambda xs: round(sorted(xs)[len(xs) // 2], 1) if xs else None
    return {"time": round(t, 3) if idx >= total else None, "gates": idx, "of": total, "plants": plants,
            "launches": launches, "launch_speed": med(launch_speeds), "launch_off": med(launch_offs),
            "launch_miss": med(launch_miss), "nest_breaks": breaks, "misses": misses, "top": round(top)}


# ── reports ────────────────────────────────────────────────────────────────────────────────────────────────────────

def table(label, rows):
    fin = [r["time"] for r in rows if r["time"] is not None]
    med = lambda xs: round(sorted(xs)[len(xs) // 2], 1) if xs else None
    print(f"  {label:<34} finished {len(fin)}/{len(rows)}  median {med(fin)} s  "
          f"gates {[r['gates'] for r in rows]}  launches {[r['launches'] for r in rows]}  "
          f"launch u/s {med([r['launch_speed'] for r in rows if r['launch_speed']])}  "
          f"off {med([r['launch_off'] for r in rows if r['launch_off'] is not None])} deg  "
          f"pass {med([r['launch_miss'] for r in rows if r['launch_miss'] is not None])} u  breaks {[r['nest_breaks'] for r in rows]}")


def self_test():
    bad = []
    src = _read(COURSE_CS)
    for i in range(1, 5):
        s = thresher_time_settings(i)
        prof = re.findall(r"new\[\] \{ ([0-9f., ]+) \}", src)
        want = [float(x.strip().rstrip("f")) for x in prof[i - 1].split(",")]
        if [float(x) for x in s["CornerProfile"]] != want:
            bad.append(f"thresher_time_sim: I{i} corner profile {s['CornerProfile']} != ThresherTimeCourse.cs {want}")
    for name, arr in (("AngularSpread", [1.2, 4.0, 2.0, 4.0]), ("RingRadius", [96, 72, 58, 46]),
                      ("MaxPresentDegrees", [50, 70, 72, 76]), ("LateralPerturbation", [120, 170, 215, 260])):
        m = re.search(name + r"\s*=\s*new\[\]\s*\{([^}]*)\}", src)
        got = [float(x.strip().rstrip("f")) for x in m.group(1).split(",")] if m else None
        if got != [float(x) for x in arr]:
            bad.append(f"thresher_time_sim: {name} {arr} != ThresherTimeCourse.cs {got}")
    nsrc = _read(NEST_CS)
    for i, spec in NEST_SPEC.items():
        m = re.search(rf"{i if i < 4 else '_'} => new Spec\(([^)]*)\)", nsrc)
        tok = lambda x: {"false": 0.0, "true": 1.0}.get(x.strip(), None)
        got = [tok(x) if tok(x) is not None else float(x.strip().rstrip("f")) for x in m.group(1).split(",")] if m else None
        if not got or (got[0], got[1], got[2], got[5], got[8]) != spec:
            bad.append(f"thresher_time_sim: nest I{i} {spec} != ThresherTimeNest.SpecFor {got}")
    if SHIPPED["laps"] * 8 != SHIPPED["gates"]:
        bad.append(f"thresher_time_sim: laps {SHIPPED['laps']} x 8 != the gate target {SHIPPED['gates']}")
    shipped = read_genome(POLICY)
    if shipped is None or abs(shipped["aimedHoldSeconds"] - 1.54) > 1e-6 or shipped["aimedLaunch"] != 1.0:
        bad.append(f"thresher_time_sim: the shipped genome did not read back ({shipped})")
    c = course_for(1, 1)
    a = race(c, 1)
    b = race(c, 1)
    if a != b:
        bad.append("thresher_time_sim: the same race twice gave two results (not deterministic)")
    if a["launches"] < 4:
        bad.append(f"thresher_time_sim: the shipped genome barely launched at I1 ({a})")
    cruise = race(c, 1, sling=False)
    if a["time"] is not None and cruise["time"] is not None and not cruise["time"] > a["time"]:
        bad.append(f"thresher_time_sim: cruise-only ({cruise['time']} s) was not slower than the sling ({a['time']} s)")
    rings = race(c, 1, nests=False)
    if rings["gates"] < 8:
        bad.append(f"thresher_time_sim: the rings race did not get round a lap ({rings})")
    # negative control: a nest the pilot never comes near is never credited (credit needs an open nest)
    if race(c, 1, limit=120.0, breakable=False)["gates"] != 0 or race(c, 1, limit=120.0, sling=False, breakable=False)["gates"] != 0:
        bad.append("thresher_time_sim: a gate was credited with its nest standing (credit needs an open nest)")
    # ── the AI nose lead (AIPilot.NoseLeadGain, ai_nose_lead.py), measured 2026-11-07 ──────────────────────────────
    # (a) OFF unless asked: nose_lead unset is the race recorded as the baseline to the digit (I1 course 1: the shipped
    #     sling, cruise only, the rings; the tables' numbers are their aggregates), and the same race as NoseLead()
    #     (gain 0), 0, {}, {"gain": 0} and False. A port that changes the model re-measures and updates the three dicts.
    baseline = {
        "shipped": {"time": None, "gates": 1, "of": 16, "plants": 135, "launches": 135, "launch_speed": 2288.3, "launch_off": 5.0,
                    "launch_miss": 63.6, "nest_breaks": 1, "misses": 1, "top": 1144},
        "cruise only": {"time": 456.45, "gates": 16, "of": 16, "plants": 0, "launches": 0, "launch_speed": None, "launch_off": None,
                        "launch_miss": None, "nest_breaks": 16, "misses": 273, "top": 88},
        "rings": {"time": 215.933, "gates": 16, "of": 16, "plants": 48, "launches": 48, "launch_speed": 2287.1, "launch_off": 4.4,
                  "launch_miss": 66.2, "nest_breaks": 0, "misses": 37, "top": 1144},
    }
    for name, got in (("shipped", a), ("cruise only", cruise), ("rings", rings)):
        if got != baseline[name]:
            bad.append(f"thresher_time_sim: I1 course 1 {name} drifted from the 2026-11-07 baseline (re-measure): {got} vs {baseline[name]}")
    if race(c, 1, nose_lead=NoseLead()) != a:
        bad.append("thresher_time_sim: nose_lead=NoseLead() (gain 0) is not today's race")
    short = race(c, 1, limit=60.0)
    for form in (0, {}, {"gain": 0}, False):
        if race(c, 1, limit=60.0, nose_lead=form) != short:
            bad.append(f"thresher_time_sim: nose_lead={form!r} is not today's race")
    # (b) the lead is applied as AIPilot applies it and only through the nose lag: a gain changes a lagged race (a dict
    #     with the config key is the same gain); the effective gain is scaled to the race's own nose rate once (on a 9/s
    #     nose 2.5 floors to 1: the same race as gain 1, and judging from the command still changes that race);
    #     max_extra_deg 0 is gain 1 (the command placed ON the aim); and on a lag-free nose (nose_rate 0, inf, or a rate
    #     that closes the whole gap in one step) the lead changes nothing
    led = race(c, 1, limit=120.0, nose_lead=2.5)
    if led == race(c, 1, limit=120.0):
        bad.append("thresher_time_sim: nose_lead 2.5 raced today's race (the lead is not applied)")
    if race(c, 1, limit=120.0, nose_lead={"nose_lead": 2.5}) != led:
        bad.append("thresher_time_sim: {'nose_lead': 2.5} is not the 2.5 lead")
    quick, quick_led = race(c, 1, limit=120.0, nose_rate=9.0), race(c, 1, limit=120.0, nose_rate=9.0, nose_lead=2.5)
    if quick_led != race(c, 1, limit=120.0, nose_rate=9.0, nose_lead=1.0) or quick_led == quick:
        bad.append("thresher_time_sim: on a 9/s nose the 2.5 lead is not gain 1 judged from the command (effective_gain from the race's rate)")
    if race(c, 1, limit=120.0, nose_lead=NoseLead(2.5, 0.0)) != race(c, 1, limit=120.0, nose_lead=1.0):
        bad.append("thresher_time_sim: max_extra_deg 0 is not gain 1 (the command on the aim)")
    for kw in ({"nose_rate": 0}, {"nose_rate": 60.0}, {"nose_rate": float("inf")}):
        if race(c, 1, limit=120.0, nose_lead=2.5, **kw) != race(c, 1, limit=120.0, **kw):
            bad.append(f"thresher_time_sim: the lead changed a lag-free race ({kw})")
    # (c) what the lead measured here (2026-11-07, the docstring). On the RINGS with the sling, I1 course 3: today's
    #     shipped Thresher does not finish (3 of 16 mouths in 600 s) and the same race with no nose lag does (330.8 s),
    #     so the lag is what loses it; the 2.5 lead finishes it (126.7 s) with fewer missed mouths (15 against 80), each
    #     launch passing nearer the ring's centre (55 u against 62). The control is an AGGREGATE, because one race is a
    #     chaotic sample (gain 1.01 finishes this course in 114 s where 1.0 does not): on I2 courses 1-3 today finishes
    #     none, the 2.5 lead finishes all three (196.3, 183.1, 192.3 s) and judging from the command WITHOUT the pull
    #     (gain 1, = 2.5 with max_extra_deg 0) finishes at most one (today none: 10, 10 and 4 of 16 mouths; a probe of
    #     gains 2.3-2.7 x caps 35-45 deg finished 89 of 90 races on these courses, gains 0.99-1.01 18 of 54). And the lead's
    #     cost in CRUISE: with the nests at I4 course 1, where today's lagged cruise passes 8 of 16 gates and a lag-free
    #     nose finishes (386.6 s), the 2.5 lead corkscrews away and passes 1 (the roll stick whirls a command placed far
    #     from the nose round the nose's forward). A port that changes any of this re-measures.
    c3 = course_for(1, 3)
    today3, nolag3, led3 = race(c3, 1, nests=False), race(c3, 1, nests=False, nose_rate=0), race(c3, 1, nests=False, nose_lead=2.5)
    if not (today3["time"] is None and nolag3["time"] is not None):
        bad.append(f"thresher_time_sim: I1 course 3 on the rings is no longer lost to the nose lag (today {today3}, no lag {nolag3})")
    if not (led3["time"] is not None and led3["misses"] < today3["misses"] and led3["launch_miss"] < today3["launch_miss"]):
        bad.append(f"thresher_time_sim: the 2.5 lead no longer finishes I1 course 3 on the rings with fewer misses: {led3} vs today {today3}")
    fin = lambda kw: sum(1 for s in (1, 2, 3) if race(course_for(2, s), 2, nests=False, **kw)["time"] is not None)
    today_fin, led_fin, judged_fin = fin({}), fin({"nose_lead": 2.5}), fin({"nose_lead": 1.0})
    if not (today_fin == 0 and led_fin == 3 and judged_fin <= 1):
        bad.append(f"thresher_time_sim: on I2 courses 1-3 (rings, sling) today finishes {today_fin}, the 2.5 lead {led_fin}, gain 1 "
                   f"{judged_fin} (was 0, 3, 0: re-measure the docstring)")
    c4 = course_for(4, 1)
    today4, nolag4, led4 = race(c4, 4, sling=False), race(c4, 4, sling=False, nose_rate=0), race(c4, 4, sling=False, nose_lead=2.5)
    if not (today4["time"] is None and nolag4["time"] is not None and led4["time"] is None and led4["gates"] < today4["gates"]):
        bad.append(f"thresher_time_sim: the 2.5 lead's corkscrew in the I4 cruise has changed (re-measure the docstring): "
                   f"today {today4}, no lag {nolag4}, lead {led4}")
    return bad


def nose_lead_arg(argv):
    """--nose-lead G[,EXTRA]: the AI nose lead a run adds (AIPilot.NoseLeadGain at the fleet lag, and how far past the
    aim it may place the command, 40 deg unless given), or None: off, today's steering."""
    if "--nose-lead" not in argv:
        return None
    parts = argv[argv.index("--nose-lead") + 1].split(",")
    return NoseLead(float(parts[0]), float(parts[1]) if len(parts) > 1 else None)


def lead_label(lead):
    return "" if lead is None else f" nose lead {lead.gain:g}"


def main(argv):
    if "--probe" in argv:
        d = run_probe()
        print(f"wrote {PROBE}: {len(d['plant'])} plant curves, {len(d['launch'])} launches")
        return 0
    if "--self-test" in argv:
        bad = self_test()
        print("\n".join(bad) or "thresher_time_sim self-test: ok")
        return 1 if bad else 0
    lead = nose_lead_arg(argv)
    leads = [None] if lead is None else [None, lead]     # the flag prints the lead-on rows under today's
    if "--trace" in argv:
        k = argv.index("--trace")
        i, s = int(argv[k + 1]), int(argv[k + 2])
        lines = []
        r = race(course_for(i, s), i, nests="--rings" not in argv, trace=lines, nose_lead=lead)
        print("\n".join(lines))
        print(r)
        return 0
    nests = "--rings" not in argv
    if "--fresh" in argv:
        # The promotion race: the shipped genome, the base genome, the cruise-only control and every --params file
        # (a search result: {"params": {...}} or a bare field dict) on FRESH courses (seedbase 1001).
        k = argv.index("--fresh")
        n = int(argv[k + 1]) if k + 1 < len(argv) and argv[k + 1].isdigit() else 6
        entries = [("shipped (ThresherTime)", read_genome(POLICY), True), ("base ThresherAIBoostPolicy",
                   dict(read_genome(BASE_POLICY), aimedLaunch=read_genome(POLICY)["aimedLaunch"]), True),
                   ("cruise only (no sling)", read_genome(POLICY), False)]
        for j, a in enumerate(argv):
            if a == "--params":
                with open(argv[j + 1], encoding="utf-8") as fh:
                    d = json.load(fh)
                entries.append((os.path.basename(argv[j + 1]), dict(read_genome(POLICY), **(d.get("params") or d)), True))
        print(f"Thresher Time, {'nests' if nests else 'rings'}, {n} fresh courses an intensity (seeds 1001+). MODEL.")
        for i in (1, 2, 3, 4):
            print(f"I{i}")
            for nl in leads:
                for name, g, sl in entries:
                    table(name + lead_label(nl), [race(course_for(i, 1000 + s), i, genome=g, nests=nests, sling=sl, nose_lead=nl)
                                                  for s in range(1, n + 1)])
        return 0
    if "--soak" in argv:
        print("The 2026-11-02 soak's courses (raw seeds 7001, 7002), the RINGS (the mode then). MODEL, not the game.")
        for i in (1, 2):
            for nl in leads:
                rows = [race(course_for(i, s, raw_seed=True), i, nests=False, nose_lead=nl) for s in (7001, 7002)]
                table(f"I{i} aimed launch{lead_label(nl)}", rows)
                table(f"I{i} cruise only{lead_label(nl)}", [race(course_for(i, s, raw_seed=True), i, nests=False, sling=False,
                                                                nose_lead=nl) for s in (7001, 7002)])
        return 0
    print(f"Thresher Time, the shipped genome ({POLICY}), {'nests' if nests else 'rings'}. MODEL, not the game.")
    for i in (1, 2, 3, 4):
        for nl in leads:
            rows = [race(course_for(i, s), i, nests=nests, nose_lead=nl) for s in range(1, 5)]
            table(f"I{i} shipped{lead_label(nl)}", rows)
            table(f"I{i} cruise only{lead_label(nl)}", [race(course_for(i, s), i, nests=nests, sling=False, nose_lead=nl)
                                                       for s in range(1, 5)])
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
