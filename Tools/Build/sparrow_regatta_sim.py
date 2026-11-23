#!/usr/bin/env python3
"""Offline Regatta race for the AI SPARROW: its race line, its afterburner driver and the rails it can ram.

Why the AI Sparrow is the slowest hull in Regatta (117-150 s for three laps at I1 in Garrett's Regatta Hull Benchmark,
2026-11-03, against 62-113 s for every other hull), MEASURED from the 16 stored editor races with an AI Sparrow rival
(`--measure`, the five flown since the race line, 2026-11-03, carry its state and velocity at 10 Hz):

- **Its cruise is 35 u/s and its afterburner 197.5 u/s** (Sparrow.prefab: MinimumSpeed 10 + DefaultThrottleScaler 25 at
  full throttle; boosting multiplies the throttle term by boostMultiplier 5 x the Time-10 BoostSpeedMultiplier 1.5). The
  speed eases toward either at 1.4 / s (least squares over the races: 1.39-1.44 / s, targets 34-35 and 196-197 u/s).
  The afterburner is free and unlimited: a Sparrow that lets go of it slows to a crawl.
- **Its AI lets go of the afterburner every 0.6 s** (`HoldBoostAIPolicySO`'s minHoldSeconds), rests 0.4-0.6 s
  (minRestSeconds) and presses again: 93 presses in the 150 s benchmark race, boosted 41-55% of every race, mean speed
  about 110 u/s. The base policy's bands (`SparrowAIBoostPolicy`: engage within 12 deg and a stick under 0.35, let go
  past 25 deg or a stick over 0.6) were set for a straight-to-a-crystal autopilot; on a race line the AI's stick law
  (0.6 at 10 deg of error, `scarab_drive_sim.ai_stick`) passes 0.6 on every bend of the braid, so each press is cut at
  the first frame the hold allows.
- **Rail rams are small**: the AI was slowed against a rail prism 1-5 times a race (four of the five races once at the
  START, beside ring 0), each costing 0.4-2.3 s, 0.4-6.7 s a race. Before the line (2026-10-16/17, I3) the AI Sparrow sat
  within 15 u of a rail 26-48% of a race; since it, 0-3%.
- A Sparrow flown with the afterburner held all race (one stored I1 flight, 2026-10-22) finished three laps in 63.9 s
  at a constant 197.5 u/s and never came within 15 u of a rail.

The model (a STEERING and SPEED model, not the game): `regatta_line_sim`'s race line over the exported rails (the
shipped course, `regatta_course_harness/course.js`), AIPilot's stick law and nose lag, the line's mouth gate
(`RegattaRaceLine.AimThroughRing`) and its miss rule; plus the Sparrow's speed (eases at SPEED_RATE toward CRUISE or
BOOST), `HoldBoostAIPolicySO.Driver.Tick` line for line (the straight is judged on the path, the runway on the ring,
`AIBoostContext`), and rail contact: inside RAM_RANGE of a rail point the speed is multiplied by RAM_HIT's factor,
easing back over its seconds (the measured drop to 6-14 u/s is several prisms at once).
The turn rate is the prefab's (v x RotationThrottleScaler 0.1 + Pitch/Yaw 80 deg/s), so the afterburner turns FASTER in
degrees a second and only slightly wider (113 u radius at 197.5 u/s against 58 u at 35).

    python3 Tools/Build/sparrow_regatta_sim.py                 # shipped bands vs the proposals, I1-I4
    python3 Tools/Build/sparrow_regatta_sim.py --measure DIR   # the AI Sparrow in stored Regatta flights
    python3 Tools/Build/sparrow_regatta_sim.py --drive FILE    # race a drive genome (a JSON of HoldBoostAIPolicySO
                                                               # fields, or a local_run result with "params")
    python3 Tools/Build/sparrow_regatta_sim.py --nose-lead 2.5 # ...each row again with the AI nose lead on
    python3 Tools/Build/sparrow_regatta_sim.py --self-test

The AI nose lead (AIPilot.NoseLeadGain, PR #1786; Tools/Build/ai_nose_lead.py is the shared piece) is race()'s
`nose_lead` knob, OFF by default: a gain over 0 judges the stick's error from the COMMAND and steers it onto the nose
turned toward the line's aim by the gain times its error, past it (at most max_extra_deg, 40), where the lagging nose is
pulled from; the driver keeps the nose's angle (AIBoostContext.Forward) and reads the stick the pilot wrote.
"""
import glob, json, math, os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import situation_features as sf
import regatta_line_sim as L
from rhino_drive_sim import add, sub, mul, dot, norm, unit, ang, rotate_toward
from scarab_drive_sim import ai_stick
from ai_nose_lead import NoseLead, effective_gain, judge_and_want   # the AI nose lead (AIPilot.NoseLeadGain), one shared piece

CRUISE, BOOST, SPEED_RATE = 35.0, 197.5, 1.4      # measured (module doc)
RTS, BASE_TURN = 0.1, 80.0                         # Sparrow.prefab RotationThrottleScaler, Pitch/YawScaler
RAM_RANGE, RAM_COOLDOWN = 8.0, 0.25   # measured: the AI was slowed at 6-10 u from a rail point (p10-p25)
# A rail prism (super-shielded, far past the mass cap) slows the Sparrow x0.5 (maxSlowStrength) easing back over 1 s
# (SparrowVesselChangeSpeedByPrism, VesselChangeSpeedByPrismEffectSO); the hull touches several at once, and overlapping
# factors multiply, which is the measured drop to 6-14 u/s.
RAM_HIT = (0.5, 1.0)

# HoldBoostAIPolicySO bands. SHIPPED mirrors Assets/_SO_Assets/AI Boost Policies/SparrowAIBoostPolicy.asset (every mode
# but Breakwater); BREAKWATER mirrors Resources/AIBoostPolicies/SparrowAIBoostPolicy_Breakwater.asset.
SHIPPED = {"align": 12.0, "ralign": 25.0, "stick": 0.35, "rstick": 0.6, "engage": 1.5, "release": 0.5,
           "min_hold": 0.6, "min_rest": 0.4}
BREAKWATER = dict(SHIPPED, align=45.0, ralign=60.0, stick=1.0, rstick=1.0, engage=2.0, release=1.25)
ALWAYS = dict(SHIPPED, align=180.0, ralign=180.0, stick=1.0, rstick=1.0, engage=0.0, release=0.0)



def bands_from_fields(p):
    """HoldBoostAIPolicySO asset field names (a drive genome, breakwater_fit's space) -> this model's bands."""
    return {"align": p["alignDegrees"], "ralign": p["releaseAlignDegrees"], "stick": p["stickBand"],
            "rstick": p["releaseStickBand"], "engage": p["engageRunwaySeconds"], "release": p["releaseRunwaySeconds"],
            "min_hold": p["minHoldSeconds"], "min_rest": p["minRestSeconds"]}


# The race line the AI flies (Resources/RaceLines/Sparrow_Regatta.asset on bleeding-edge; the benchmark branch capped
# LaneShare at 0.35).
LINE = {"share": 0.55, "clear": 10.0, "risk": 0.5, "careful": 0.35, "bold": 0.8}


def _rail_grid(lanes, cell=40.0):
    g = {}
    for lane in lanes:
        for p in lane:
            g.setdefault((int(p[0] // cell), int(p[1] // cell), int(p[2] // cell)), []).append(p)
    return g, cell


def _near_rail(grid, p):
    g, cell = grid
    k = (int(p[0] // cell), int(p[1] // cell), int(p[2] // cell))
    best = 1e18
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            for dz in (-1, 0, 1):
                for q in g.get((k[0] + dx, k[1] + dy, k[2] + dz), ()):
                    d = (p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 + (p[2] - q[2]) ** 2
                    if d < best: best = d
    return math.sqrt(best)


class Hold:
    """HoldBoostAIPolicySO.Driver.Tick (the rocket is Breakwater's and not modelled here)."""

    def __init__(s, b):
        s.b, s.held, s.changed = b, False, -1e9

    def straight(s, c, release=False):
        a, k = (s.b["ralign"], s.b["rstick"]) if release else (s.b["align"], s.b["stick"])
        return not c["breaking"] and c["angle"] <= a and c["stick"] <= k

    def tick(s, c, now):
        runway = c["dist"] / max(c["speed"], 1.0)
        if s.held:
            if now - s.changed < s.b["min_hold"]: return
            if not s.straight(c, True) or runway < s.b["release"]:
                s.held, s.changed = False, now
            return
        if now - s.changed < s.b["min_rest"]: return
        if s.straight(c) and runway >= s.b["engage"]:
            s.held, s.changed = True, now


def race(intensity, bands=SHIPPED, line=None, T=400.0, dt=1 / 60, own=0, rams=True, start_speed=CRUISE, course=None,
         nose_lead=None):
    """Three laps at `intensity` (or on `course`) with the afterburner driver's `bands`.
    nose_lead: the AI nose lead (AIPilot.NoseLeadGain; ai_nose_lead.NoseLead.from_config: None = OFF, today's steering; a
    gain stated at the fleet lag, a {"gain", "max_extra_deg"} dict or a NoseLead), scaled once to regatta_line_sim.NOSE_FOLLOW,
    the fleet lag this model always flies."""
    line_cfg = dict(LINE, **(line or {}))
    C = course or sf.course_from(sf.load_course_js(), intensity)
    gates = C["gates"]; n = len(gates); total = 3 * n
    P = [g["p"] for g in gates]; A = [unit(g["a"]) for g in gates]
    tight = min(g["r"] for g in gates)
    clear, risk = line_cfg["clear"], line_cfg["risk"]
    room = max(0.0, tight - clear * (1.5 + (0.75 - 1.5) * risk) - L.RAIL_OFFSET)
    path = L.gap_line(C["lanes"], own, L.RAIL_OFFSET + line_cfg["share"] * room)
    look_s = line_cfg["careful"] + (line_cfg["bold"] - line_cfg["careful"]) * risk
    grid = _rail_grid(C["lanes"])
    ring_at = [L.nearest(path, p, -1) for p in P]
    hint = L.nearest(path, P[0], -1)
    pos = L.point_ahead(path, (hint - 40) % len(path), 0)
    fwd = unit(sub(path[(hint - 38) % len(path)], pos)); cmd = list(fwd)
    lead = NoseLead.from_config(nose_lead)
    lead_gain = effective_gain(lead.gain, L.NOSE_FOLLOW)   # AIPilot.EffectiveNoseLeadGain, once a race (0 when off)
    hint = -1
    drv = Hold(bands)
    v = start_speed
    idx, t, lost = 0, 0.0, -1
    misses = ram_n = presses = 0
    ram_at = -1e9; boosted_t = 0.0; was_held = False; dist_flown = 0.0; slows = []
    while t < T and idx < total:
        g = idx % n; ring = P[g]; ax = A[g]
        on_line = lost != idx
        look = min(L.MAX_LOOK, max(L.MIN_LOOK, v * look_s))
        if on_line:
            hint = L.nearest(path, pos, hint)
            if norm(sub(path[hint], pos)) > 250: hint = L.nearest(path, pos, -1)
            aim = L.ring_gated_aim(path, hint, look, ring_at[g], pos, ring, ax, gates[g]["r"], clear)
        else:
            aim = add(ring, mul(unit(sub(ring, pos)), 320))
        to = sub(aim, pos)
        err = ang(fwd, to)
        if lead_gain > 0:
            # the nose lead (AIPilot.NoseLeadGain): judge the error from the COMMAND and steer the command onto the nose
            # turned toward the aim by the effective gain times its error, past it (capped), where the lagging nose is
            # pulled from; the driver below keeps the NOSE's angle (AIBoostContext.Forward is transform.forward) and reads
            # the stick the pilot wrote (LastStickDeflection)
            judge, want = judge_and_want(fwd, cmd, to, lead_gain, lead.max_extra_deg)
        else:
            judge, want = fwd, to
        stick = ai_stick(ang(judge, want))
        # the boost driver: straight judged on the path (where the pilot goes), runway on the ring
        drv.tick({"angle": err, "stick": stick, "dist": norm(sub(ring, pos)), "speed": v,
                  "breaking": False}, t)
        if drv.held and not was_held: presses += 1
        was_held = drv.held
        if drv.held: boosted_t += dt
        target = BOOST if drv.held else CRUISE
        v += (target - v) * min(1.0, SPEED_RATE * dt)
        omega = v * RTS + BASE_TURN
        fwd, cmd = L.turn_with_lag(fwd, cmd, want, math.radians(omega * stick) * dt, dt, True)
        mod = 1.0
        for f, t0, d in list(slows):
            e = (t - t0) / d
            if e >= 1.0: slows.remove((f, t0, d)); continue
            mod *= f + (1.0 - f) * e
        newpos = add(pos, mul(fwd, v * mod * dt))
        a0 = dot(sub(pos, ring), ax); a1 = dot(sub(newpos, ring), ax)
        if a0 * a1 <= 0 and a0 != a1:
            k = a0 / (a0 - a1); hit = add(pos, mul(sub(newpos, pos), k))
            if norm(sub(hit, ring)) <= gates[g]["r"]:
                idx += 1
            elif norm(sub(hit, ring)) < 1500:
                misses += 1; lost = idx
        dist_flown += v * mod * dt
        pos = newpos; t += dt
        if rams and t - ram_at > RAM_COOLDOWN and _near_rail(grid, pos) < RAM_RANGE:
            ram_n += 1; ram_at = t; slows.append((RAM_HIT[0], t, RAM_HIT[1]))
    return {"time": round(t, 1) if idx >= total else None, "rings": idx, "misses": misses, "rams": ram_n,
            "presses": presses, "boosted": round(boosted_t / max(t, 1e-6), 2), "mean_speed": round(dist_flown / t, 1)}


# ------------------------------------------------------------------ the measurement, from stored flights

def measure(paths):
    """Per flight with an AI Sparrow rival: rings, seconds, boosted share, presses, mean hold, speed, rams (slowed
    within 15 u of a rail) and the seconds they cost. Returns a list of dicts."""
    rows = []
    for f in sorted(paths):
        try:
            d = json.load(open(f, encoding="utf-8"))
        except (OSError, ValueError):
            continue
        hulls = d.get("rivalHulls") or []
        if "sparrow" not in hulls or not d.get("rails"):
            continue
        j = hulls.index("sparrow")
        ts = [s[0] for s in d["samples"]]
        R = [r[4 * j:4 * j + 4] for r in d["rivals"]]
        if any(len(r) < 4 for r in R):
            continue
        vel = d.get("rivalVel") or []
        if len(vel) == len(R) and all(len(x) >= 3 * j + 3 for x in vel):
            sp = [norm(x[3 * j:3 * j + 3]) for x in vel]
        else:
            sp = [0.0] + [norm(sub(R[i][:3], R[i - 1][:3])) / max(ts[i] - ts[i - 1], 1e-3) for i in range(1, len(R))]
        state = d.get("rivalState") or []
        has_state = len(state) == len(R) and all(len(x) >= 5 * j + 5 for x in state)
        bits = [int(x[5 * j]) for x in state] if has_state else None
        grid = _rail_grid([[p[:3] for p in r["prisms"]] for r in d["rails"]])
        dmin = [_near_rail(grid, r[:3]) for r in R]
        target = d.get("raceTarget", 24)
        end = next((i for i, r in enumerate(R) if r[3] >= target), len(R) - 1)
        mid = [i for i in range(end) if ts[i] > 3]
        row = {"file": os.path.basename(f), "intensity": d.get("intensity"), "rings": int(R[end][3]),
               "seconds": round(ts[end], 1), "finished": R[end][3] >= target,
               "near15": round(sum(dmin[i] < 15 for i in mid) / max(len(mid), 1), 2),
               "mean_speed": round(sum(sp[i] for i in mid) / max(len(mid), 1), 1)}
        if bits:
            b = [(x & 1) > 0 for x in bits]; slowed = [(x & 8) > 0 for x in bits]
            holds, c = [], 0
            for x in b[:end]:
                if x: c += 1
                elif c: holds.append(c * 0.1); c = 0
            row.update(boosted=round(sum(b[i] for i in mid) / max(len(mid), 1), 2), presses=len(holds),
                       median_hold=sorted(holds)[len(holds) // 2] if holds else 0.0)
            lost, rams, i = 0.0, 0, 0
            while i < end:
                if slowed[i] and dmin[i] < 15:
                    a = i
                    pre = sorted(sp[max(0, a - 10):a] or [sp[a]])[len(sp[max(0, a - 10):a] or [sp[a]]) // 2]
                    k = a
                    while k < len(sp) and (slowed[k] or sp[k] < 0.9 * pre): k += 1
                    flown = sum(sp[a:k]) * 0.1
                    lost += (k - a) * 0.1 - flown / max(pre, 1.0)
                    rams += 1; i = k
                else:
                    i += 1
            row.update(rams=rams, ram_seconds=round(lost, 1))
        rows.append(row)
    return rows


def _job(j):
    return race(j[2], j[1], nose_lead=j[3] if len(j) > 3 else None)


def self_test():
    bad = []
    # the driver: a held boost is let go once the stick passes the release band, and not before min_hold
    h = Hold(SHIPPED)
    c = {"angle": 2.0, "stick": 0.1, "dist": 900.0, "speed": 100.0, "breaking": False}
    h.tick(c, 0.0)
    if not h.held: bad.append("shipped bands did not engage on a straight with runway")
    h.tick(dict(c, stick=0.9), 0.3)
    if not h.held: bad.append("let go inside min_hold")
    h.tick(dict(c, stick=0.9), 0.7)
    if h.held: bad.append("held past the release stick band")
    h2 = Hold(ALWAYS); h2.tick(dict(c, angle=170.0, stick=1.0, dist=1.0), 0.0)
    if not h2.held: bad.append("ALWAYS did not engage")
    # the model reproduces the measured stutter: the shipped bands boost under 70% of an I1 race and press dozens of
    # times; holding the afterburner is at least 30% faster and still threads every ring (the negative control is the
    # shipped row itself: if the model lost the stutter, the two rows would match)
    a, b = race(1, SHIPPED), race(1, ALWAYS)
    if not (a["time"] and a["boosted"] < 0.7 and a["presses"] > 30):
        bad.append(f"shipped bands no longer stutter in the model: {a}")
    if not (b["time"] and a["time"] and b["time"] < 0.7 * a["time"] and b["misses"] == 0):
        bad.append(f"holding the afterburner is not clearly faster: shipped {a}, always {b}")
    # rails: the shipped line at I1 never comes within RAM_RANGE of a rail; with a contact range wider than the line's
    # 30-50 u clearance the same race rams (the contact test is live, not dead code)
    global RAM_RANGE
    keep = RAM_RANGE
    try:
        RAM_RANGE = 45.0
        r = race(1, ALWAYS)
    finally:
        RAM_RANGE = keep
    if r["rams"] == 0 or b["rams"] != 0: bad.append(f"rail contact: shipped range {b}, 45 u range {r}")
    # the nose lead is OFF by default: NoseLead() (gain 0) is the unset knob; a gain changes the race (the knob is live)
    if race(1, ALWAYS, nose_lead=NoseLead()) != b: bad.append("NoseLead() (gain 0) is not the unset lead")
    if race(1, ALWAYS, nose_lead=2.5) == b: bad.append("the nose lead at 2.5 changed nothing (the knob is dead)")
    print("sparrow_regatta_sim self-test: " + ("ok" if not bad else "FAILED " + repr(bad)))
    return 0 if not bad else 1


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        sys.exit(self_test())
    if "--measure" in sys.argv:
        d = sys.argv[sys.argv.index("--measure") + 1]
        for row in measure(glob.glob(os.path.join(d, "**", "flight_Regatta_*.json"), recursive=True)):
            print(json.dumps(row))
        sys.exit(0)
    from concurrent.futures import ProcessPoolExecutor
    rows = [("shipped", SHIPPED), ("Breakwater's", BREAKWATER), ("always", ALWAYS)]
    if "--drive" in sys.argv:
        d = json.load(open(sys.argv[sys.argv.index("--drive") + 1], encoding="utf-8"))
        rows = [("shipped", SHIPPED), ("the drive", bands_from_fields(d.get("params") or d))]
    leads = [None]
    if "--nose-lead" in sys.argv:   # --nose-lead G[,EXTRA]: each row again with the AI nose lead on
        leads.append(NoseLead.from_cli(sys.argv[sys.argv.index("--nose-lead") + 1]))
    jobs = [(name + (f" +lead{nl.gain:g}" if nl else ""), b, I, nl) for name, b in rows for nl in leads for I in (1, 2, 3, 4)]
    with ProcessPoolExecutor() as ex:
        res = list(ex.map(_job, jobs))
    for (name, _, I, _), r in zip(jobs, res):
        print(f"{name:22} I{I}  {json.dumps(r)}")
