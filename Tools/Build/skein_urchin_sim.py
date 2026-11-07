#!/usr/bin/env python3
"""Skein raced offline by the AI Urchin, flown the way MinigameSkein flies it: the Urchin's own Time game, for the AI
plan's time-arcade step (Docs/AI_SYSTEM/AI_PLAN.md section 5; the search is Tools/Compute/evaluators/skein_fit.py).

The Urchin had no training model. This one ports, line for line where it decides anything, what an AI Urchin does in
Skein, and reads the course off the shipped generator rather than retyping it:

    the course    SkeinCourse.BuildAll's own rails and rings: Tools/Build/skein_course_harness/export_course.sh compiles
                  the shipped SkeinCourse.cs and writes skein_courses.json (each intensity's prefab seed and strand
                  count, the six-attempt seed ladder of SpawnableSkein / SkeinCourseSource; --check is the drift gate)
                  and skein_holdout.json (two other cables per intensity, never flown in the game: the over-fit check)
    the start     SkeinController.TryBuildSpawnPoses: on a 90 u ring 220 u behind the start collar, facing it
    the ride      TrailFollower (Attach's direction seed off dot(Course, heading), RideTheTrail's one smoothed speed:
                  rate 5/s toward |grind throttle| x terrain, 300 own / 20 rival / 300 destroyed; the steal of every
                  rival prism entered, GunVesselTransformer.ApplyPrismscapePayoff) and GunVesselTransformer.Slide (the
                  grind throttle's 6/s weight, the facing hysteresis 0.35 that picks the ride direction from the NOSE,
                  the launch off an open end at 1.2x the ride speed, the 0.35 s re-latch grace for the rail just left)
    free flight   VesselTransformer's scalar flight: speed lerps 1.5/s toward throttle x 65 floored by the CARRIED
                  launch speed, which bleeds 36 u/s^2 (GunVesselTransformer.TickCarriedSpeed); travel along the nose;
                  pitch / yaw / roll 90 deg/s (RotationThrottleScaler 0); the AI nose follows at LERP_AMOUNT 1.5/s
    the latch     a PhysX trigger, sampled each 0.04 s FixedUpdate while the hull teleports each frame (SKEIN.md 7): a
                  prism is entered when the hull's centre is inside its cross-section (6 u wide + 0.5 u of hull, 3 u
                  for a Track Projector rail) at a fixed step and was not at the one before; ghosted during a Slip
    the pilot     AIPilot: the stick from the nose-to-target cross product, roll = yaw stick, ram (XDiff 1 while the
                  aim is within cos 0.9 of the course, else the skill throttle 0.8, i.e. a 0.6 grind / cruise
                  throttle), the orbit break-off off-rail (manta_redline_sim's port of AIPilot / PursuitReachability)
    the gate aim  GateRaceController's provider with MinigameSkein.unity's numbers (commit 260, approach lead 300,
                  through 200, the shipped steering, not Regatta's), asked after SkeinController.TryOverrideAim
    the driver    UrchinAutopilotDriver.TryRideToward / TryProjectTrackToward / TrySpike / TrySlip and
                  UrchinRailAssessment.Scan / Decide / IsDryCrawl / DryCrawlTooLong, line for line, with every
                  UrchinAutopilotConfigSO field (the genome) passed in; SkeinController passes its own rail lead
                  (aiRailLeadDistance 260), so the config's RailLeadDistance is not read in Skein
    what-ifs      (not shipped; the evaluator's `steering` / `seek` args) steering="regatta": the gate aim with
                  GateRaceController.AICommitFromApproachPoint on, as Regatta and Switchback fly it (commit from the
                  approach point, through the CENTRE, re-attack at once after a missed mouth); seek=True: a free Urchin
                  flies for the cable point from which riding forward threads its next ring when that is quicker than
                  flying there (rail at 300 u/s, air at its speed, aim SEEK_AHEAD prisms down the rail), the shape of
                  RegattaController.TryOwnLaneAhead, which Skein does not have
    the kit       Slip: 0.6 s ghost (UrchinSlipAction, Time 0); Track Projector: 100 u, 13 prisms 8 u apart, 3 x 3 x 6,
                  laid max(40, speed x 0.35) ahead along the nose, own colour, 20 s cooldown (UrchinTrackAction);
                  Chain Spikes: 0.15 ammo a tap (UrchinSpikeAction); ammo +0.05/s (ResourceSystem) +0.1/s riding

What this model does NOT see is LIMITS below, copied into every evaluator result. The editor is the measurement.

THE AI NOSE LEAD (AIPilot.NoseLeadGain, PR #1786; Tools/Build/ai_nose_lead.py is the one shared piece) is race()'s
`nose_lead` knob: None (OFF, today's steering: every number above and every caller is as it was), a gain, a
{"gain", "max_extra_deg"} dict or a NoseLead. With a gain the free-flying pilot judges its stick error from the
COMMAND's forward instead of the nose and wants the nose turned toward the aim by the gain times its error, past it
(at most max_extra_deg past, never past 170 deg: ai_nose_lead.slerp_beyond = AIPilot.NoseLeadDirection); the cross
product still lands in the nose's frame and the stick still turns the command about the nose's axes (P.stick's
judged=). The effective gain is AIPilot.EffectiveNoseLeadGain computed ONCE a race from this model's own nose rate
(`nose_rate`: None = the fleet LERP 1.5/s the Urchin flies with; 0 or inf = no lag, the nose IS the command), so it is
0 on a lag-free nose. It is OFF while riding a rail (there the stick only turns the attitude, the position rides the
rail, and the ride direction - the facing hysteresis - reads the nose) and ON in every free flight: the ghost after a
Slip, the carried speed after a launch, and the orbit break-off, whose escape steer takes the lead like any aim. Skein
sets no NoseLeadGain: the lead is a what-if here, not the shipped Urchin.
MEASURED 2026-11-07 (--self-test holds it): the lead does NOT help the Urchin here as the game applies it. The lag is a
real cause of today's misses: at I2 today's steering crosses 9 ring planes beside the mouth and stops at 15/24 in 900 s,
and a lag-free nose (nose_rate 0) finishes in 258.2 s with none. But gain 2.5 is TRAPPED at 3/24 (I3 and I4 at 1/24,
and every hold-out cable but I1's first): at the first through point the aim flips behind the nose (TurnHardIfBehind),
the lead places the command ~78 deg from the nose, and the roll stick - AIPilot's YDiff = XSum, turned about the NOSE's
forward - whirls that command round the nose's axis; the nose chases it, the gap settles at 78 deg, the stick pins at
(1, -1) and the hull corkscrews away from the course (the manta port's finding too, manta_redline_sim.py). Gain 1
(judging from the command, no pull past the aim) is not trapped but does not help either (I2 15/24 with 10 misses; I1
96.7 s against today's 81.5 s), and max_extra_deg 0 is gain 1. On I1 (every ring a collar, no misses today) 2.5
finishes in 108.3 s, the lag-free nose's 108.6 s: the lead does what it is for, and that is slower here because today's
lagging nose happens to latch a rail and ride (ride 0.05, v 103). A scratch copy with the roll stick zeroed while the
lead is on finished I2 in 362.2 s with 2 misses (I3 8/24, I4 4/24 with 9 misses against today's 19): the decoupling the
lead needs is AIPilot's roll rule, not this model's; the port stays as the game is.

    python3 Tools/Build/skein_urchin_sim.py                  # the shipped AI, I1-I4 on the shipped cables
    python3 Tools/Build/skein_urchin_sim.py --holdout        # ... and on the hold-out cables
    python3 Tools/Build/skein_urchin_sim.py --trace 4        # one race (intensity), a line a second
    python3 Tools/Build/skein_urchin_sim.py --config k=v,... # one genome (UrchinAutopilotConfigSO field names)
    python3 Tools/Build/skein_urchin_sim.py --nose-lead 2.5  # ... and the lead-on row under each (G[,EXTRA]: the gain,
                                                            #     the degrees past the aim); --nose-rate R: 0 = no lag
    python3 Tools/Build/skein_urchin_sim.py --self-test
"""
import json
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
import manta_redline_sim as P   # AIPilot's stick, orbit break-off and Unity's quaternion maths, already ported there
from ai_nose_lead import NoseLead, slerp_beyond   # the AI nose lead (AIPilot.NoseLeadGain), the one shared piece

add, sub, mul, dot, cross, norm, unit = P.add, P.sub, P.mul, P.dot, P.cross, P.norm, P.unit
q_axis, q_mul, q_rot, q_inv_rot, q_slerp, q_look = P.q_axis, P.q_mul, P.q_rot, P.q_inv_rot, P.q_slerp, P.q_look
FWD, UP, RIGHT = P.FWD, P.UP, P.RIGHT

COURSES = os.path.join(HERE, "skein_course_harness", "skein_courses.json")
HOLDOUT = os.path.join(HERE, "skein_course_harness", "skein_holdout.json")
CONFIG_ASSET = os.path.join(REPO, "Assets", "Resources", "UrchinAutopilotConfig.asset")
CONFIG_CS = os.path.join(REPO, "Assets", "_Scripts", "Controller", "AI", "Urchin", "UrchinAutopilotConfigSO.cs")

# Urchin.prefab / GunVesselTransformer / TrailFollower (read by vessel_numbers() and held there in --self-test)
CRUISE = 65.0                     # VesselTransformer DefaultThrottleScaler
TURN = 90.0                       # Pitch / Yaw / RollScaler; RotationThrottleScaler 0
FRIENDLY, HOSTILE, DESTROYED = 300.0, 20.0, 300.0
SPEED_TRACK = 5.0                 # TrailFollower.speedTrackingRate
GRIND_INERTIA = 6.0               # GunVesselTransformer.trailInertiaRate
FACING_FLIP = 0.35                # GunVesselTransformer.facingFlipThreshold
DEADBAND = 0.1                    # GunVesselTransformer.throttleDeadband
LAUNCH_KICK = 1.2                 # GunVesselTransformer.endLaunchSpeedKick
REATTACH_GRACE = 0.35             # GunVesselTransformer.endLaunchReattachGrace
DECAY = 36.0                      # GunVesselTransformer.detachSpeedDecayRate
RIDE_RECHARGE = 0.1               # GunVesselTransformer.rechargeRate (ammo / s while riding; x2 on a shielded prism)
AMMO_GAIN = 0.05                  # ResourceSystem Ammo resourceGainRate, once a second
LERP = 1.5                        # VesselTransformer.LERP_AMOUNT: speed, and the AI nose
SKILL_THROTTLE = 0.8              # AIPilot defaultThrottleLow = High = 0.8 on Urchin.prefab (skill does not move it)
THROTTLE_CREEP = 0.001            # AIPilot throttleIncreaseLow = High
REST = 0.5                        # GunVesselTransformer.throttleRestPosition (XDiff rests at 0.5)
# the kit (Assets/_SO_Assets/VesselActions/Urchin/*.asset)
GHOST = 0.6                       # UrchinSlipAction ghostSecondsAtRestingTime (Time level 0)
SPIKE_COST = 0.15                 # UrchinSpikeAction ammoCost
TRACK_LENGTH, TRACK_SPACING, TRACK_HALF = 100.0, 8.0, 1.5
TRACK_FORWARD, TRACK_LEAD_S, TRACK_COOLDOWN = 40.0, 0.35, 20.0
# the latch: SpawnableSkein prismScale (6, 6, 8); the hull's own ~0.5 u (SKEIN.md 7); PhysX fixed step
ARENA_HALF = 3.0
HULL = 0.5
FIXED = 0.04
# SkeinController / MinigameSkein.unity's GateRaceController
RAIL_LEAD = 260.0                 # SkeinController.aiRailLeadDistance (passed to TryRideToward over the config's)
COMMIT, APPROACH_LEAD, THROUGH = 260.0, 300.0, 200.0
LEAD_SHARE_OF_LEG = 0.4
START_STANDOFF, START_RADIUS = 220.0, 90.0
OWN_DOMAIN = "Ruby"               # the AI's colour; a third of every cable is each of Jade / Ruby / Gold

LIMITS = ("Skein offline (skein_urchin_sim.py): the shipped cable and rings (SkeinCourse.cs via its harness), the "
          "ride, launch, latch, Slip, Track Projector and the UrchinAutopilotDriver decisions; one AI, no rivals (the "
          "paint is the authored third each, never repainted by others), a rider on the rail's centre line (no 4.5 u "
          "surface offset), rails ridden as straight chords between prisms (the game's Catmull-Rom), the spikes spend "
          "ammo but convert nothing (their 60 u/s volley trails a grinding rider, who steals each prism it enters "
          "anyway), Time level 0 throughout (no crystals), no lobby handicap (Legendary), one start slot")

CONFIG_FIELDS = ("RailLeadDistance", "AssessIntervalSeconds", "ScanArc", "CaptureFraction", "ClosingFraction",
                 "LaunchPreferArc", "DryCrawlArc", "MaxDryCrawlSeconds", "ReverseLeadTurnRadii", "ParkedSpeed",
                 "ParkedSeconds", "MinSlipIntervalSeconds", "SpikeIntervalSeconds", "SpikeMinAmmo", "TrackAimDegrees",
                 "TrackMinRangeInLengths", "TrackAxisAlignment")


# ── the genome ──────────────────────────────────────────────────────────────────────────────────────────────────

def _field_initialisers():
    """UrchinAutopilotConfigSO's C# initialisers: what an Urchin flies with no asset (LoadDefault's fallback)."""
    src = open(CONFIG_CS, encoding="utf-8").read()
    out = {}
    for name in CONFIG_FIELDS:
        m = re.search(r"public float " + name + r"\s*=\s*(-?[0-9.]+)f?\s*;", src)
        if not m:
            raise RuntimeError(f"UrchinAutopilotConfigSO.{name} not found in {CONFIG_CS}")
        out[name] = float(m.group(1))
    return out


def read_config(path=CONFIG_ASSET):
    """The genome as it ships: Resources/UrchinAutopilotConfig.asset over the C# initialisers."""
    out = _field_initialisers()
    if os.path.isfile(path):
        for line in open(path, encoding="utf-8"):
            m = re.match(r"^\s{2}(\w+):\s*(-?[0-9.]+(?:[eE][-+]?\d+)?)\s*$", line)
            if m and m.group(1) in out:
                out[m.group(1)] = float(m.group(2))
    return out


_SHIPPED = {}


def shipped_config():
    if "c" not in _SHIPPED:
        _SHIPPED["c"] = read_config()
    return _SHIPPED["c"]


def vessel_numbers():
    """The prefab and kit numbers this model hard-codes, read from the shipped assets (held in --self-test)."""
    pre = open(os.path.join(REPO, "Assets", "_Prefabs", "Spacevessels", "Urchin.prefab"), encoding="utf-8").read()
    kit = os.path.join(REPO, "Assets", "_SO_Assets", "VesselActions", "Urchin")
    grab = lambda text, key: float(re.search(r"^\s+" + key + r":\s*(-?[0-9.]+)\s*$", text, re.M).group(1))
    slip = open(os.path.join(kit, "UrchinSlipAction.asset"), encoding="utf-8").read()
    track = open(os.path.join(kit, "UrchinTrackAction.asset"), encoding="utf-8").read()
    spike = open(os.path.join(kit, "UrchinSpikeAction.asset"), encoding="utf-8").read()
    return {"CRUISE": grab(pre, "DefaultThrottleScaler"), "TURN": grab(pre, "YawScaler"),
            "FRIENDLY": grab(pre, "FriendlyTerrainSpeed"), "HOSTILE": grab(pre, "HostileTerrainSpeed"),
            "SKILL_THROTTLE": grab(pre, "defaultThrottleHigh"), "GHOST": grab(slip, "ghostSecondsAtRestingTime"),
            "TRACK_LENGTH": grab(track, "trackLength"), "TRACK_COOLDOWN": grab(track, "cooldown"),
            "SPIKE_COST": grab(spike, "ammoCost")}


# ── the course ──────────────────────────────────────────────────────────────────────────────────────────────────

class Rail:
    """One open Trail: prism centres in index order (the race direction), their arc lengths, their paint."""
    __slots__ = ("pts", "arc", "dom", "half", "track", "id")

    def __init__(self, pts, dom, half, track=False, rid=0, arc=None):
        self.pts = pts
        if arc is None:
            arc = [0.0]
            for k in range(1, len(pts)):
                arc.append(arc[-1] + norm(sub(pts[k], pts[k - 1])))
        self.arc = arc
        self.dom = list(dom)
        self.half = half
        self.track = track
        self.id = rid

    def heading(self, k):
        """Trail.HeadingAt: the central difference in index order."""
        a = self.pts[max(0, k - 1)]
        b = self.pts[min(len(self.pts) - 1, k + 1)]
        return unit(sub(b, a))

    def at(self, s):
        """The centre line at arc s, and the segment index k (s in [arc[k], arc[k+1]])."""
        arc = self.arc
        n = len(arc)
        if s <= 0.0:
            return self.pts[0], 0
        if s >= arc[-1]:
            return self.pts[-1], max(0, n - 2)
        lo, hi = 0, n - 1
        while hi - lo > 1:
            mid = (lo + hi) >> 1
            if arc[mid] <= s:
                lo = mid
            else:
                hi = mid
        a, b = self.pts[lo], self.pts[lo + 1]
        L = arc[lo + 1] - arc[lo]
        f = (s - arc[lo]) / L if L > 1e-9 else 0.0
        return (a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f, a[2] + (b[2] - a[2]) * f), lo


class Grid:
    """A uniform hash of rail segments (by rail id), for the latch's overlap test."""
    CELL = 24.0

    def __init__(self):
        self.cells = {}

    def key(self, p):
        c = self.CELL
        return (int(math.floor(p[0] / c)), int(math.floor(p[1] / c)), int(math.floor(p[2] / c)))

    def add_rail(self, rail):
        pts = rail.pts
        for k in range(len(pts) - 1 if len(pts) > 1 else 1):
            a = pts[k]
            b = pts[k + 1] if len(pts) > 1 else a
            lo = self.key((min(a[0], b[0]) - 4, min(a[1], b[1]) - 4, min(a[2], b[2]) - 4))
            hi = self.key((max(a[0], b[0]) + 4, max(a[1], b[1]) + 4, max(a[2], b[2]) + 4))
            for x in range(lo[0], hi[0] + 1):
                for y in range(lo[1], hi[1] + 1):
                    for z in range(lo[2], hi[2] + 1):
                        self.cells.setdefault((x, y, z), []).append((rail.id, k))

    def overlaps(self, p, rails, out):
        """Add {(rail id, prism index): rail} for every prism whose cross-section holds the point p."""
        for rid, k in self.cells.get(self.key(p), ()):
            rail = rails[rid]
            pts = rail.pts
            a = pts[k]
            b = pts[k + 1] if k + 1 < len(pts) else a
            ab = (b[0] - a[0], b[1] - a[1], b[2] - a[2])
            ap = (p[0] - a[0], p[1] - a[1], p[2] - a[2])
            l2 = ab[0] * ab[0] + ab[1] * ab[1] + ab[2] * ab[2]
            t = 0.0 if l2 < 1e-9 else max(0.0, min(1.0, (ap[0] * ab[0] + ap[1] * ab[1] + ap[2] * ab[2]) / l2))
            d = (ap[0] - ab[0] * t, ap[1] - ab[1] * t, ap[2] - ab[2] * t)
            r = rail.half + HULL
            if d[0] * d[0] + d[1] * d[1] + d[2] * d[2] <= r * r:
                out[(rid, k if t < 0.5 else min(k + 1, len(pts) - 1))] = rail
        return out


_ARENAS = {}
_SEEK = {}
SEEK_CAPTURE, SEEK_ARC, SEEK_AHEAD, SEEK_GAIN, SEEK_APPROACH_COS = 0.8, 1600.0, 3, 0.9, 0.3
SEEK_INTERVAL = 0.25


def arena(course):
    """The cable's static geometry (prism centres, arc lengths, the latch grid), parsed once per course."""
    key = (course["intensity"], course["cableSeed"], len(course["rails"]))
    if key not in _ARENAS:
        rails = []
        grid = Grid()
        for rid, r in enumerate(course["rails"]):
            pts = [tuple(r["pts"][k:k + 3]) for k in range(0, len(r["pts"]), 3)]
            rail = Rail(pts, (), ARENA_HALF, rid=rid)
            rails.append(rail)
            grid.add_rail(rail)
        _ARENAS[key] = (rails, grid, [r["domain"] for r in course["rails"]])
    return _ARENAS[key]


def load_courses(path=COURSES):
    with open(path, encoding="utf-8") as fh:
        return json.load(fh)["courses"]


def course_for(intensity, which="shipped", index=0):
    """One exported cable: the shipped one of an intensity, or a hold-out (index 0 or 1)."""
    rows = [c for c in load_courses(COURSES if which == "shipped" else HOLDOUT) if c["intensity"] == intensity]
    return rows[index]


# ── UrchinRailAssessment, line for line ─────────────────────────────────────────────────────────────────────────

def seg_param(a, b, p):
    ab = sub(b, a)
    l2 = dot(ab, ab)
    return max(0.0, min(1.0, dot(sub(p, a), ab) / l2)) if l2 > 1e-8 else 0.0


def scan(rail, start, step, objective, max_arc):
    """UrchinRailAssessment.Scan (an open rail; every prism present): (valid, min_distance, arc_to_min, arc_walked,
    reaches_end)."""
    pts = rail.pts
    count = len(pts)
    if count <= 0 or start < 0 or start >= count or step == 0:
        return (False, float("inf"), 0.0, 0.0, False)
    step = 1 if step > 0 else -1
    prev = pts[start]
    mn = norm(sub(prev, objective))
    arc_to_min = arc = 0.0
    reaches_end = True
    i = start + step
    while 0 <= i < count:
        p = pts[i]
        seg = norm(sub(p, prev))
        if arc + seg > max_arc:
            reaches_end = False
            break
        t = seg_param(prev, p, objective)
        q = add(prev, mul(sub(p, prev), t))
        d = norm(sub(q, objective))
        if d < mn:
            mn = d
            arc_to_min = arc + seg * t
        arc += seg
        prev = p
        i += step
    return (True, mn, arc_to_min, arc, reaches_end)


RIDE, REVERSE, LEAVE = 0, 1, 2


def threads(sc, capture, dry, dry_arc):
    return sc[0] and sc[1] <= capture and (not dry or sc[2] <= dry_arc)


def decide(ahead, behind, range_now, capture, closing, dry_arc, launch_arc, dry):
    """UrchinRailAssessment.Decide."""
    capture = max(0.0, capture)
    if threads(ahead, capture, dry, dry_arc):
        return RIDE
    if threads(behind, capture, dry, dry_arc):
        return REVERSE
    if not dry:
        closing = max(0.0, min(1.0, closing))
        if ahead[0] and ahead[1] < range_now * closing:
            return RIDE
        if ahead[0] and ahead[4] and ahead[3] <= launch_arc:
            return RIDE
        if behind[0] and behind[1] < range_now * closing * closing:
            return REVERSE
    return LEAVE


def is_dry_crawl(hostile, slowed, convertible, can_afford):
    return hostile and slowed and (not convertible or not can_afford)


def dry_crawl_too_long(dry, since, now, max_seconds):
    return dry and since >= 0.0 and now - since > max_seconds


# ── one race ────────────────────────────────────────────────────────────────────────────────────────────────────

def plane_crossing(prev, cur, centre, axis, radius):
    return P.plane_crossing(prev, cur, centre, axis, radius)


def seek_candidates(course, rails, gate_index, capture_fraction=SEEK_CAPTURE, max_arc=SEEK_ARC, stride=2):
    """The PROPOSED free-flight rail seek (not shipped; `race(seek=True)`): every point of the cable from which riding
    FORWARD threads ring `gate_index` within `max_arc` of rail, as (rail id, prism index, arc to the ring). Cached."""
    key = (course["intensity"], course["cableSeed"], gate_index, capture_fraction, max_arc, stride)
    if key in _SEEK:
        return _SEEK[key]
    g = course["gates"][gate_index]
    ring, r = tuple(g["p"]), float(g["r"]) * capture_fraction
    out = []
    for rail in rails:
        pts, arc = rail.pts, rail.arc
        best = {}
        for j in range(len(pts) - 1):
            a, b = pts[j], pts[j + 1]
            if abs(a[0] - ring[0]) > r + 20 and abs(b[0] - ring[0]) > r + 20:
                continue
            q = add(a, mul(sub(b, a), seg_param(a, b, ring)))
            if norm(sub(q, ring)) > r:
                continue
            k = j
            while k >= 0 and arc[j] - arc[k] <= max_arc:
                if k % stride == 0:
                    d = arc[j] - arc[k]
                    if d < best.get(k, 1e18):
                        best[k] = d
                k -= 1
        out.extend((rail.id, k, d) for k, d in best.items())
    _SEEK[key] = out
    return out


def race(course, cfg=None, limit=900.0, dt=1 / 60, domain=OWN_DOMAIN, track=True, trace=None, start_slot=0.0,
         probe=None, steering="shipped", seek=False, nose_rate=None, nose_lead=None):
    """One AI Urchin races `course` (an exported cable) with the UrchinAutopilotConfigSO genome `cfg` (field names;
    missing fields are the shipped ones). Returns {time (None = not finished), rings, of, ride_share, mean_speed,
    slips, reverses, launches, latches, tracks, flown_s, misses}.
    `nose_rate` is the AI nose's follow rate per second (VesselTransformer.AINoseFollowPerSecond, which Skein does not
    set): None = the fleet LERP 1.5/s, 0 or inf = no lag (the nose IS the command, as a human's); the speed lerp keeps
    LERP whatever the nose does. `nose_lead` is AIPilot.NoseLeadGain (ai_nose_lead.NoseLead.from_config: None = off,
    a gain, {"gain", "max_extra_deg"} or a NoseLead), stated at the fleet lag and scaled to this race's nose rate once;
    0 with no nose lag, and off while riding a rail."""
    c = dict(shipped_config())
    if cfg:
        c.update(cfg)
    # the AI nose: Quaternion.Slerp toward the command by nose_step a frame (LERP_AMOUNT x dt, as shipped), or the
    # command itself when the nose has no lag
    follow = LERP if nose_rate is None else float(nose_rate)
    nose_step = min(1.0, follow * dt) if 0.0 < follow < float("inf") else 1.0
    lead_knob = NoseLead.from_config(nose_lead)
    # AIPilot.EffectiveNoseLeadGain, once a race, from THIS model's nose rate: max(1, gain x 1.5 / rate), 0 with no lag
    g_lead = lead_knob.effective(follow) if nose_step < 1.0 else 0.0
    lead_extra = lead_knob.max_extra_deg
    base, grid, paint = arena(course)
    rails = [Rail(r.pts, [paint[r.id]] * len(r.pts), ARENA_HALF, rid=r.id, arc=r.arc) for r in base]
    track_grid = Grid()
    gates = [(tuple(g["p"]), unit(tuple(g["a"])), float(g["r"])) for g in course["gates"]]
    total = len(gates)

    # SkeinController.TryBuildSpawnPoses: CellSpawnFormation.BuildFacingRing about the start collar's axis
    start, axis = tuple(course["start"]), unit(tuple(course["startAxis"]))
    side = unit(cross(axis, (0.0, 0.0, 1.0)) if abs(axis[2]) < 0.99 else cross(axis, (1.0, 0.0, 0.0)))
    other = cross(axis, side)
    ang = math.radians(start_slot)
    pos = add(sub(start, mul(axis, START_STANDOFF)),
              add(mul(side, START_RADIUS * math.cos(ang)), mul(other, START_RADIUS * math.sin(ang))))
    q = q_look(unit(sub(start, pos)))
    cmd = q
    course_dir = q_rot(q, FWD)        # VesselStatus.Course
    speed = 0.0                       # VesselTransformer.speed (the smoothed cruise field)
    carried = 0.0                     # GunVesselTransformer._carriedSpeed
    t = 0.0
    idx = 0
    ammo = 1.0
    next_ammo_tick = 1.0
    skill_throttle = SKILL_THROTTLE

    # attachment (TrailFollower + GunVesselTransformer)
    att = None                        # the Rail ridden
    s_arc = 0.0
    direction = 1
    ride_speed = 0.0
    grind = 0.0
    facing = 1
    launched_from, reattach_until = None, -1.0
    ghost_until = -1.0
    overlapping = set()
    next_fixed = FIXED

    # UrchinAutopilotDriver.PilotState
    moving_since = -1.0
    next_assess = 0.0
    assessed = None
    verdict = RIDE
    crawling_dry = False
    dry_since = -1.0
    next_spike = next_slip = next_track = 0.0
    track_ready_at = 0.0

    # GateRaceController's provider and AIPilot's break-off
    locked, side_sign, lead = -1, 1.0, APPROACH_LEAD
    extending, ext_t = False, 0.0
    orbit = P.Orbit()

    stats = dict(slips=0, reverses=0, launches=0, latches=0, tracks=0, misses=0, seeks=0)
    missed = False
    next_seek, seek_for, seek_aim, seek_counted = 0.0, -1, None, -1
    ride_frames = frames = 0
    speed_sum = 0.0
    next_trace = 0.0
    dryed = False

    def block_of(rail, s, d):
        _, k = rail.at(s)
        return k if d > 0 else min(k + 1, len(rail.pts) - 1)

    while t < limit and idx < total:
        ring, ax, rad = gates[idx]
        status_speed = ride_speed if att is not None else speed
        hostile = False
        aim = None
        looking_ok = True

        # ── SkeinController.TryOverrideAim: attached -> UrchinAutopilotDriver.TryRideToward ──
        if att is not None:
            k = block_of(att, s_arc, direction)
            hostile = att.dom[k] != domain
            convertible = True                        # no super-shielded mass in Skein
            if hostile and convertible and t >= next_spike and ammo >= SPIKE_COST and ammo >= c["SpikeMinAmmo"]:
                next_spike = t + max(0.1, c["SpikeIntervalSeconds"])
                ammo -= SPIKE_COST                    # the volley converts nothing the ride does not (LIMITS)
            slipped = False
            if moving_since < 0 or status_speed >= c["ParkedSpeed"]:
                moving_since = t
            elif t - moving_since > c["ParkedSeconds"] and t >= next_slip:
                next_slip = t + c["MinSlipIntervalSeconds"]
                moving_since = t
                assessed = None
                slipped = True
            if not slipped:
                if t >= next_assess or assessed is not att:
                    if assessed is not att:
                        dry_since = -1.0
                    next_assess = t + c["AssessIntervalSeconds"]
                    assessed = att
                    slowed = hostile and HOSTILE < FRIENDLY
                    afford = ammo >= SPIKE_COST and ammo >= c["SpikeMinAmmo"]
                    crawling_dry = is_dry_crawl(hostile, slowed, convertible, convertible and afford)
                    step = direction
                    ahead = scan(att, k, step, ring, c["ScanArc"])
                    behind = scan(att, k, -step, ring, c["ScanArc"])
                    verdict = decide(ahead, behind, norm(sub(ring, pos)), max(1.0, rad * c["CaptureFraction"]),
                                     c["ClosingFraction"], c["DryCrawlArc"], c["LaunchPreferArc"], crawling_dry)
                if not crawling_dry:
                    dry_since = -1.0
                elif dry_since < 0:
                    dry_since = t
                if dry_crawl_too_long(crawling_dry, dry_since, t, c["MaxDryCrawlSeconds"]):
                    verdict = LEAVE
                    dryed = True
                if verdict == LEAVE and t >= next_slip:
                    next_slip = t + c["MinSlipIntervalSeconds"]
                    assessed = None
                    dry_since = -1.0
                    slipped = True
            if slipped:
                # UrchinSlipActionExecutor.Slip: IsAttached false and the ghost; next MoveShip ends the ride (EndRide)
                stats["slips"] += 1
                carried_now = ride_speed
                ghost_until = t + GHOST
                overlapping = set()
                pos, _ = att.at(s_arc)
                att = None
                carried = 0.0
                if carried_now > 0 and carried_now > CRUISE * max(0.0, _throttle_axis(1.0)):
                    carried = carried_now
                    speed = carried_now
                course_dir = q_rot(q, FWD)
            else:
                if verdict == REVERSE:
                    r_min = speed / math.radians(TURN) if speed > 0 else 0.0
                    lead_r = max(RAIL_LEAD, r_min * c["ReverseLeadTurnRadii"])
                    aim = add(sub(pos, mul(course_dir, lead_r)), mul(q_rot(q, UP), 0.6 * lead_r))
                else:
                    aim = add(pos, mul(course_dir, RAIL_LEAD))
        # ── off the rail: the Track Projector, then the gate aim (GateRaceController's provider) ──
        if att is None:
            fwd = q_rot(q, FWD)
            if track and t >= next_track and t >= track_ready_at:
                to = sub(ring, pos)
                rng = norm(to)
                if rng > 1e-3:
                    d = mul(to, 1.0 / rng)
                    if (rng >= TRACK_LENGTH * c["TrackMinRangeInLengths"]
                            and P.angle(fwd, d) <= c["TrackAimDegrees"]
                            and abs(dot(ax, d)) >= c["TrackAxisAlignment"]):
                        next_track = t + max(1.0, TRACK_COOLDOWN)
                        track_ready_at = t + TRACK_COOLDOWN
                        off = max(TRACK_FORWARD, speed * TRACK_LEAD_S)
                        mouth = add(pos, mul(fwd, off))
                        n = max(1, int(math.floor(TRACK_LENGTH / TRACK_SPACING)) + 1)
                        pts = [add(mouth, mul(fwd, i * TRACK_SPACING)) for i in range(n)]
                        tr = Rail(pts, [domain] * n, TRACK_HALF, track=True, rid=len(rails))
                        rails.append(tr)
                        track_grid.add_rail(tr)
                        stats["tracks"] += 1
            regatta = steering == "regatta"
            if regatta and missed:
                locked = -1                               # re-latch the side, and AIPilot.BreakOffNow
                extending, ext_t = True, 0.0
                orbit.reset()
            missed = False
            if idx != locked:
                locked = idx
                side_sign = 1.0 if dot(sub(pos, ring), ax) >= 0 else -1.0
                leg_from = gates[idx - 1][0] if idx > 0 else pos
                lead = min(APPROACH_LEAD, LEAD_SHARE_OF_LEG * norm(sub(leg_from, ring)))
                if regatta:
                    lead = min(lead, 0.8 * COMMIT)
            to_ring = sub(ring, pos)
            r_min = speed / math.radians(TURN) if speed > 0 else 0.0
            commit = min(COMMIT, lead + 0.5 * r_min) if regatta else COMMIT
            if dot(to_ring, to_ring) > commit * commit:
                aim = add(ring, mul(ax, side_sign * lead))
            elif regatta and dot(to_ring, to_ring) > 1.0:
                aim = add(ring, mul(unit(to_ring), THROUGH))
            else:
                aim = sub(ring, mul(ax, side_sign * THROUGH))
            if seek and not extending:
                if t >= next_seek or seek_for != idx:
                    next_seek = t + SEEK_INTERVAL
                    seek_for = idx
                    seek_aim = None
                    fly = max(CRUISE, speed)
                    direct = norm(to_ring) / fly
                    best = direct * SEEK_GAIN
                    for rid, kk, arc_left in seek_candidates(course, base, idx):
                        rail = rails[rid]
                        p = rail.pts[kk]
                        to_p = sub(p, pos)
                        dp = norm(to_p)
                        if dp < 1e-6 or (rail is launched_from and t < reattach_until):
                            continue
                        if dot(rail.heading(kk), to_p) < SEEK_APPROACH_COS * dp:
                            continue
                        cost = dp / fly + arc_left / FRIENDLY
                        if cost < best:
                            best = cost
                            seek_aim = rail.pts[min(len(rail.pts) - 1, kk + SEEK_AHEAD)]
                if seek_aim is not None:
                    aim = seek_aim
                    stats["seeks"] += 1 if seek_for != seek_counted else 0
                    seek_counted = seek_for

        # ── AIPilot: the break-off (never while attached), LookingAtCrystal / ram, the stick ──
        heading = course_dir
        to = sub(aim, pos)
        if att is not None:
            extending, ext_t = False, 0.0
            orbit.reset()
        else:
            r_min = speed / math.radians(TURN) if speed > 0 else 0.0
            run = max(max(0.0, 2 * r_min - P.CAPTURE), abs(speed) * P.RUN_SECONDS)
            if extending:
                ext_t += dt
                if (norm(to) >= run and ext_t >= P.BREAK_MIN) or ext_t >= P.BREAK_MAX:
                    extending, ext_t = False, 0.0
                    orbit.reset()
            else:
                closing = dot(unit(to), unit(heading)) > P.CLOSING_COS
                unreachable = not closing and P.inside_turning_circle(to, heading, r_min)
                if unreachable or orbit.tick(to):
                    extending, ext_t = True, 0.0
                    orbit.reset()
            if extending:
                esc = unit(add(unit(heading), mul(unit(to), -P.AWAY_BIAS)), heading)
                aim = add(pos, mul(esc, run * 2 if run > 1 else P.MAX_DISTANCE))
        dist = sub(aim, pos)
        dsq = dot(dist, dist)
        desired = unit(dist) if dsq > 1e-12 else heading
        looking = (not extending) and dot(desired, course_dir) >= 0.9
        xdiff = 1.0 if looking else min(1.0, max(0.0, skill_throttle))
        skill_throttle += THROTTLE_CREEP * dt
        if dsq <= 1e-12:
            xs, ys = 0.0, 0.0
        elif g_lead > 0.0 and att is None:
            # the nose lead (AIPilot.TryNoseLead), in free flight only: want the NOSE turned toward the aim by g_lead
            # times its error, past it (NoseLeadDirection), and judge the error from the COMMAND's forward; the cross
            # product still lands in the nose's frame and the command update below is untouched. The break-off's escape
            # steer takes the lead like any aim. OFF on a rail: there the stick only turns the attitude, the position
            # rides the rail, and the ride direction (facing, above) reads the nose.
            want = tuple(slerp_beyond(q_rot(q, FWD), desired, g_lead, lead_extra))
            xs, ys = P.stick(q, want, dsq, judged=unit(q_rot(cmd, FWD)))
        else:
            xs, ys = P.stick(q, desired, dsq)
        yd = xs
        throttle = max(-1.0, min(1.0, (xdiff - REST) / max(0.01, max(REST, 1 - REST))))   # ReadThrottle

        # ── attitude: Roll / Yaw / Pitch onto the command, the nose follows at the fleet lag ──
        cmd = q_mul(q_axis(yd * TURN * dt, q_rot(q, FWD)), cmd)
        cmd = q_mul(q_axis(xs * TURN * dt, q_rot(q, UP)), cmd)
        cmd = q_mul(q_axis(ys * TURN * dt, q_rot(q, RIGHT)), cmd)

        prev_pos = pos
        if att is not None:
            # GunVesselTransformer.Slide (trail)
            grind += (throttle - grind) * (1 - math.exp(-GRIND_INERTIA * dt))
            moving = abs(grind) > DEADBAND
            k = block_of(att, s_arc, direction)
            heading_idx = att.heading(k)
            facing_dot = dot(q_rot(q, FWD), heading_idx)
            if (facing > 0 and facing_dot < -FACING_FLIP) or (facing < 0 and facing_dot > FACING_FLIP):
                facing = -facing
            if moving:
                travel = facing if grind > 0 else -facing
                if travel != direction:
                    direction = travel
                    if verdict == REVERSE:
                        stats["reverses"] += 1
            # TrailFollower.RideTheTrail
            terrain = FRIENDLY if att.dom[k] == domain else HOSTILE
            ride_speed += (abs(grind) * terrain - ride_speed) * (1 - math.exp(-SPEED_TRACK * dt))
            step = ride_speed * dt
            ns = s_arc + direction * step
            ended = ns < 0.0 or ns > att.arc[-1]
            if ended:
                # out of rail: LAUNCH (LaunchOffRibbonEnd, then EndRide on the next MoveShip)
                stats["launches"] += 1
                launched_from, reattach_until = att, t + REATTACH_GRACE
                carried_now = ride_speed * LAUNCH_KICK
                pos, _ = att.at(s_arc)
                att = None
                carried = 0.0
                if carried_now > CRUISE * max(0.0, throttle):
                    carried = speed = carried_now
                overlapping = set()
            else:
                k_old = k
                s_arc = ns
                pos, _ = att.at(s_arc)
                k = block_of(att, s_arc, direction)
                if k != k_old and att.dom[k] != domain:
                    att.dom[k] = domain                   # ApplyPrismscapePayoff: riding a rival's prism steals it
                tangent = unit(sub(pos, prev_pos), heading)
                course_dir = tangent
                speed += (ride_speed - speed) * min(1.0, LERP * dt)   # AdvanceSpeed(VesselStatus.Speed)
                ammo = min(1.0, ammo + RIDE_RECHARGE * dt)
                ride_frames += 1
            q = q_slerp(q, cmd, nose_step) if nose_step < 1.0 else cmd
        if att is None and prev_pos is pos:
            # VesselTransformer.MoveShipScalar with GunVesselTransformer's carried floor
            q = q_slerp(q, cmd, nose_step) if nose_step < 1.0 else cmd
            natural = max(0.0, throttle * CRUISE)
            if carried > 0:
                carried = max(natural, carried - DECAY * dt)
                if carried <= natural:
                    carried = 0.0
            target = carried if carried > natural else natural
            speed += (target - speed) * min(1.0, LERP * dt)
            course_dir = q_rot(q, FWD)
            pos = add(pos, mul(course_dir, speed * dt))

        # the ring (RaceGateRing.PlaneCrossing on this frame's segment)
        cr = plane_crossing(prev_pos, pos, ring, ax, rad)
        if cr == 1:
            idx += 1
        elif cr == -1:
            stats["misses"] += 1
            missed = att is None

        # the latch: a PhysX trigger at each fixed step
        t += dt
        if t >= next_fixed:
            next_fixed += FIXED
            if att is None and t >= ghost_until:
                now = grid.overlaps(pos, rails, {})
                if track_grid.cells:
                    track_grid.overlaps(pos, rails, now)
                entered = [key for key in now if key not in overlapping]
                overlapping = set(now)
                for key in entered:
                    rail = now[key]
                    if rail is launched_from and t < reattach_until:
                        continue                         # IsReattachBlocked
                    kk = key[1]
                    # TrailFollower.Attach + GunVesselTransformer.SeedTrailRide
                    att = rail
                    h = rail.heading(kk)
                    direction = -1 if dot(course_dir, h) < 0 else 1
                    nxt = kk + direction
                    s_arc = rail.arc[kk]
                    if 0 <= nxt < len(rail.pts):
                        segv = sub(rail.pts[nxt], rail.pts[kk])
                        l2 = dot(segv, segv)
                        if l2 > 1e-6:
                            f = max(0.0, min(1.0, dot(sub(pos, rail.pts[kk]), segv) / l2))
                            s_arc = rail.arc[kk] + direction * f * math.sqrt(l2)
                    ride_speed = max(0.0, speed)
                    nose_vs_travel = -1 if dot(q_rot(q, FWD), course_dir) < 0 else 1
                    facing = direction * nose_vs_travel
                    grind = throttle
                    carried = 0.0
                    assessed = None
                    moving_since = -1.0
                    stats["latches"] += 1
                    break
            elif t < ghost_until:
                overlapping = set()

        if probe is not None:
            probe(t, pos, idx, att, aim, ride_speed if att is not None else speed, q_rot(q, FWD))
        frames += 1
        speed_sum += ride_speed if att is not None else speed
        if trace is not None and t >= next_trace:
            next_trace += 1.0
            state = ("RIDE" if verdict == RIDE else "REV " if verdict == REVERSE else "LEAVE") if att is not None else \
                ("break" if extending else "fly  ")
            trace.append(f"{t:6.1f}s ring {idx:2d}/{total} {state} v {(ride_speed if att is not None else speed):5.0f} "
                         f"rail {att.id if att is not None else '-':>3} d {norm(sub(ring, pos)):6.0f} r {rad:4.0f} "
                         f"ammo {ammo:.2f}")
        if t >= next_ammo_tick:
            next_ammo_tick += 1.0
            ammo = min(1.0, ammo + AMMO_GAIN)
    return {"time": round(t, 3) if idx >= total else None, "rings": idx, "of": total,
            "ride_share": round(ride_frames / max(1, frames), 3), "mean_speed": round(speed_sum / max(1, frames), 1),
            "dry_leave": dryed, **stats}


def _throttle_axis(xdiff):
    return max(-1.0, min(1.0, (xdiff - REST) / max(0.01, max(REST, 1 - REST))))


# ── reports ─────────────────────────────────────────────────────────────────────────────────────────────────────

def ideal_seconds(course):
    """A reference no pilot reaches: the rings' polyline at the friendly grind speed (every metre ridden at 300)."""
    gates = [tuple(g["p"]) for g in course["gates"]]
    path = norm(sub(gates[0], sub(tuple(course["start"]), mul(unit(tuple(course["startAxis"])), START_STANDOFF))))
    path += sum(norm(sub(gates[k + 1], gates[k])) for k in range(len(gates) - 1))
    return path / FRIENDLY


def table(rows):
    width = max([14] + [len(label) for label, _ in rows])    # 14 for today's labels; the lead rows widen it
    for label, r in rows:
        tm = f"{r['time']:6.1f}s" if r["time"] is not None else f"DNF {r['rings']:2d}/{r['of']}"
        print(f"  {label:<{width}} {tm:>10}  ride {r['ride_share']:.2f}  v {r['mean_speed']:5.0f}  "
              f"latches {r['latches']:3d}  launches {r['launches']:3d}  slips {r['slips']:3d}  "
              f"reverses {r['reverses']:3d}  tracks {r['tracks']:2d}  misses {r['misses']:3d}")


def self_test():
    bad = []
    v = vessel_numbers()
    for k, val in v.items():
        if abs(globals()[k] - val) > 1e-6:
            bad.append(f"{k}: the model has {globals()[k]}, the shipped asset {val}")
    cfg = read_config()
    if set(cfg) != set(CONFIG_FIELDS):
        bad.append("read_config did not read every UrchinAutopilotConfigSO field")
    # UrchinRailAssessment.Decide, the cases UrchinRailAssessmentTests pins
    capture = 32.0
    thr = (True, 10.0, 100.0, 300.0, False)
    far = (True, 900.0, 50.0, 300.0, False)
    if decide(thr, far, 1000.0, capture, 0.7, 80.0, 160.0, False) != RIDE:
        bad.append("decide: a rail threading the ring ahead is not ridden")
    if decide(far, thr, 1000.0, capture, 0.7, 80.0, 160.0, False) != REVERSE:
        bad.append("decide: a rail threading the ring behind is not reversed onto")
    if decide(thr, far, 1000.0, capture, 0.7, 80.0, 160.0, True) != LEAVE:
        bad.append("decide: a dry crawl to a ring 100 u of arc away (DryCrawlArc 80) is not left")
    if decide((True, 600.0, 10.0, 100.0, True), far, 1000.0, capture, 0.7, 80.0, 160.0, False) != RIDE:
        bad.append("decide: a rail closing to 0.6 of the range is not ridden")
    if decide((True, 950.0, 10.0, 100.0, True), (True, 950.0, 10.0, 100.0, True), 1000.0, capture, 0.7, 80.0, 160.0,
              False) != RIDE:
        bad.append("decide: an open end 100 u ahead (LaunchPreferArc 160) is not launched off")
    if decide((True, 950.0, 10.0, 400.0, True), far, 1000.0, capture, 0.7, 80.0, 160.0, False) != LEAVE:
        bad.append("decide: a rail going nowhere is not left")
    # the course is the shipped generator's
    shipped = load_courses()
    if [c["intensity"] for c in shipped] != [1, 2, 3, 4] or any(len(c["gates"]) != 24 for c in shipped):
        bad.append("skein_courses.json: not four 24-ring cables (run skein_course_harness/export_course.sh)")
    else:
        pins = [sum(g["strand"] >= 0 for g in c["gates"]) for c in shipped]
        if pins != [0, 7, 11, 22]:
            bad.append(f"skein_courses.json: pinned rings per intensity {pins}, SKEIN.md 6 says 0 / 7 / 11 / 22")
    # races: the shipped AI threads I1 (every ring a collar) and rides some of it; it is deterministic
    c1 = course_for(1)
    a = race(c1)
    b = race(c1)
    if a != b:
        bad.append("race: the same genome raced the same cable differently (not deterministic)")
    if a["time"] is None:
        bad.append(f"race: the shipped AI did not finish I1 ({a})")
    if a["latches"] == 0:
        bad.append("race: the shipped AI never latched a rail on I1 (the latch is not modelled)")
    # negative control: an Urchin that reads every ride as parked (ParkedSpeed huge, ParkedSeconds 0) slips off
    # each rail the moment it latches, never rides, and must be slower on I1 than the shipped genome
    parked = race(c1, {"ParkedSpeed": 1e6, "ParkedSeconds": 0.0})
    if a["time"] is not None and (parked["time"] is None or parked["time"] <= a["time"] or parked["ride_share"] > 0):
        bad.append(f"race: a genome that slips off every rail did not race I1 slower ({parked} vs {a})")
    # the AI nose lead (AIPilot.NoseLeadGain, ai_nose_lead.py)
    # (a) OFF unless asked: unset, NoseLead(), 0, {}, {"gain": 0} and False are today's race, and today's race is the
    #     recorded baseline (python3 Tools/Build/skein_urchin_sim.py with the lead off, 2026-11-07) to the digit on I1
    #     and I2. A port that changes the model re-measures and updates these two (the table's rows are their rounding).
    baseline = {
        1: {"time": 81.467, "rings": 24, "of": 24, "ride_share": 0.048, "mean_speed": 103.2, "dry_leave": False,
            "slips": 0, "reverses": 0, "launches": 2, "latches": 2, "tracks": 2, "misses": 0, "seeks": 0},
        2: {"time": None, "rings": 15, "of": 24, "ride_share": 0.005, "mean_speed": 71.0, "dry_leave": False,
            "slips": 4, "reverses": 0, "launches": 2, "latches": 6, "tracks": 4, "misses": 9, "seeks": 0},
    }
    c2 = course_for(2)
    today2 = race(c2)
    if a != baseline[1]:
        bad.append(f"I1 drifted from the 2026-11-07 baseline (re-measure): {a} vs {baseline[1]}")
    if today2 != baseline[2]:
        bad.append(f"I2 drifted from the 2026-11-07 baseline (re-measure): {today2} vs {baseline[2]}")
    for form in (NoseLead(), 0, {}, {"gain": 0}, False):
        if race(c1, nose_lead=form) != a:
            bad.append(f"nose_lead={form!r} is not today's race")
    # (b) applied as AIPilot applies it and only through the nose lag: a gain changes a lagged race (a dict with the
    #     config key is the same gain); the gain is scaled to this model's own nose rate once (on a 9/s nose 2.5 floors
    #     to 1: the same race as gain 1, and judging from the command still changes that race); max_extra_deg 0 is gain
    #     1 (the command placed ON the aim); the nose lag is modelled (rate 0 is another race); and on a lag-free nose
    #     (rate 0, inf, or one that closes the whole gap in a step) the lead changes nothing
    led1 = race(c1, nose_lead=2.5)
    if led1 == a:
        bad.append("nose_lead 2.5 raced today's race (the lead is not applied)")
    if race(c1, nose_lead={"nose_lead": 2.5}) != led1:
        bad.append("{'nose_lead': 2.5} is not the 2.5 lead")
    quick, quick_led = race(c1, nose_rate=9.0), race(c1, nose_rate=9.0, nose_lead=2.5)
    if quick_led != race(c1, nose_rate=9.0, nose_lead=1.0) or quick_led == quick:
        bad.append("on a 9/s nose the 2.5 lead is not gain 1 judged from the command "
                   "(effective_gain from the model's rate)")
    if race(c1, nose_lead=NoseLead(2.5, 0.0)) != race(c1, nose_lead=1.0):
        bad.append("max_extra_deg 0 is not gain 1 (the command on the aim)")
    nolag1 = race(c1, nose_rate=0)
    if nolag1 == a:
        bad.append("nose_rate 0 raced the lagged race (the nose lag is not modelled)")
    for kw in ({"nose_rate": 0}, {"nose_rate": float("inf")}, {"nose_rate": 60.0}):
        if race(c1, nose_lead=2.5, **kw) != race(c1, **kw):
            bad.append(f"the lead changed a lag-free race ({kw})")
    # (c) what the lead measures here (2026-11-07, the docstring): on I1 the 2.5 lead flies the lag-free nose's race
    #     (within 5 %, no misses); at I2 today's misses are the lag's (a lag-free nose finishes with none), yet the 2.5
    #     lead as the game applies it is TRAPPED (no finish, under 10 rings, fewer than today's) by the roll stick that
    #     whirls a far command round the nose's forward, while gain 1 (judged from the command, no pull past the aim)
    #     is not trapped. A port that changes any of this re-measures.
    if not (led1["time"] is not None and led1["misses"] == 0
            and abs(led1["time"] - nolag1["time"]) <= 0.05 * nolag1["time"]):
        bad.append("on I1 the 2.5 lead no longer flies the lag-free nose's race (re-measure the docstring): "
                   f"{led1} vs {nolag1}")
    nolag2 = race(c2, nose_rate=0)
    if not (today2["time"] is None and today2["misses"] >= 5 and nolag2["time"] is not None and nolag2["misses"] == 0):
        bad.append(f"I2's misses are no longer the lag's (re-measure the docstring): today {today2}, no lag {nolag2}")
    trapped = race(c2, nose_lead=2.5)
    if not (trapped["time"] is None and trapped["rings"] < 10 and trapped["rings"] < today2["rings"]):
        bad.append(f"the 2.5 lead's corkscrew at I2 has changed (re-measure the docstring): {trapped}")
    judged2 = race(c2, nose_lead=1.0)
    if not (judged2["rings"] >= today2["rings"] - 2 and judged2["rings"] > trapped["rings"] + 5):
        bad.append("gain 1 (judged from the command, no pull past the aim) is now trapped at I2 too: "
                   f"{judged2} vs 2.5 {trapped}")
    return bad


def main(argv):
    if "--self-test" in argv:
        bad = self_test()
        for b in bad:
            print("FAIL", b)
        print("skein_urchin_sim self-test:", "ok" if not bad else f"{len(bad)} failure(s)")
        return 1 if bad else 0
    cfg = {}
    if "--config" in argv:
        for kv in argv[argv.index("--config") + 1].split(","):
            k, v = kv.split("=")
            cfg[k] = float(v)
    # the AI nose lead (AIPilot.NoseLeadGain) and the nose rate, what-ifs: Skein sets neither
    nose_rate = float(argv[argv.index("--nose-rate") + 1]) if "--nose-rate" in argv else None
    lead = None
    if "--nose-lead" in argv:
        parts = argv[argv.index("--nose-lead") + 1].split(",")       # G[,EXTRA], as manta_redline_sim takes it
        lead = NoseLead(float(parts[0]), float(parts[1]) if len(parts) > 1 else None)
    if "--trace" in argv:
        i = int(argv[argv.index("--trace") + 1])
        lines = []
        r = race(course_for(i), cfg, trace=lines, nose_rate=nose_rate, nose_lead=lead)
        print("\n".join(lines))
        table([(f"I{i}" if lead is None else f"I{i} lead {lead.gain:g}", r)])
        return 0
    print("Skein, the AI Urchin (model; the editor is the measurement)")
    print("genome:", {**read_config(), **cfg})
    if lead is not None or nose_rate is not None:
        print("nose:", "the fleet lag 1.5/s" if nose_rate is None else f"rate {nose_rate:g}/s", "| lead rows:",
              lead if lead is not None else "none", "(AIPilot.NoseLeadGain, a what-if: Skein sets none)")
    rows = []
    for i in (1, 2, 3, 4):
        c = course_for(i)
        rows.append((f"I{i} shipped", race(c, cfg, nose_rate=nose_rate)))
        if lead is not None:
            rows.append((f"I{i} lead {lead.gain:g}", race(c, cfg, nose_rate=nose_rate, nose_lead=lead)))
        if "--holdout" in argv:
            for h in (0, 1):
                rows.append((f"I{i} hold-out {h}", race(course_for(i, "holdout", h), cfg, nose_rate=nose_rate)))
                if lead is not None:
                    rows.append((f"I{i} hold-out {h} lead {lead.gain:g}",
                                 race(course_for(i, "holdout", h), cfg, nose_rate=nose_rate, nose_lead=lead)))
    table(rows)
    print("ideal (rings' polyline at 300 u/s):", " / ".join(f"I{i} {ideal_seconds(course_for(i)):.0f}s" for i in (1, 2, 3, 4)))
    print("LIMITS:", LIMITS)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
