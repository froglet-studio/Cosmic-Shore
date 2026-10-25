#!/usr/bin/env python3
"""Offline Regatta race for the Rhino ON ITS RACE LINE: does its ramp drive (RampBoostAIPolicySO) fire there?

The two models before this one each left out half the Rhino's race. rhino_drive_sim.py flies the ramp drive on the old
through-the-centre gate steering with a proportional stick; regatta_line_sim.py flies the real race line with AIPilot's
own stick law and break-off, but at a constant speed. This one joins them:

- the line: regatta_line_sim's gap line over the exported rails at the Rhino's style (Resources/RaceLines/
  Rhino_Regatta), aimed RaceLineStyleSO.Lookahead ahead, with the 2026-10-25 fix (the aim is a path lead, no break-off;
  RegattaRaceLine.AimThroughRing never cuts a mouth);
- the drive: rhino_drive_sim's Policy / NewDriver / executor (the ramp's climb 220, bleed 300, coast 120; x16.8 over a
  50 u/s cruise; the grace band), judging the RING as AIPilot.TickBoostPolicy does (bearing and range to the ring the
  mode reports, the corner at it from the line a look-ahead past it), with the Regatta variant's numbers
  (Resources/AIBoostPolicies/RhinoAIBoostPolicy_Regatta);
- AIPilot's stick law and the Rhino's turn rate ((v x 0.2 + 90) deg/s), the steering scaled down under the drive's
  rotation limit (LimitRotation) exactly as the pilot does;
- path_angle=True (shipped 2026-10-25, AIBoostContext.PathDirection): "is this straight" asked of the line's aim, the
  runway and corner of the ring; lag=True: the nose follows the command at 1.5/s, as an AI hull's does
  (regatta_line_sim.turn_with_lag). path_angle=False is the drive before: judged by the ring round the bend, the line was
  never straight (the ring sat 18-35 deg off the course) and the Rhino ramped 5-10% of a race.

    python3 Tools/Build/rhino_line_sim.py              # the Regatta drive on I1-I4: time, rings, share of the race ramping
    python3 Tools/Build/rhino_line_sim.py --sweep      # the drive's gates against race time
    python3 Tools/Build/rhino_line_sim.py --self-test
"""
import math, os, re, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import situation_features as sf
import rhino_drive_sim as rd
import regatta_line_sim as L
from rhino_drive_sim import add, sub, mul, dot, norm, unit, ang, rotate_toward
from scarab_drive_sim import ai_stick

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
REGATTA_POLICY = os.path.join(ROOT, "Assets/Resources/AIBoostPolicies/RhinoAIBoostPolicy_Regatta.asset")
FIELDS = {"alignDegrees": "align", "releaseAlignDegrees": "ralign", "stickBand": "stick",
          "releaseStickBand": "rstick", "engageRunwaySeconds": "erun", "releaseRunwaySeconds": "rrun",
          "steeringShareOfPlateau": "share", "minHoldSeconds": "hold", "minRestSeconds": "rest",
          "minRearmSeconds": "rearm", "minSpeedGain": "gain", "cornerSpeed": "corner", "minCornerSpeed": "mincorner",
          "brakeMarginUnits": "margin", "brakeLeadSeconds": "lead", "graceShare": "gshare",
          "brakeSteerFloor": "steerfloor", "throttleSlewPerSecond": "slew", "cornerReleaseDegrees": "crel"}
SPEED, RTS, BASE, SHARE, CLEAR, RISK = 0, 0.2, 90, 0.676, 18, 0.4   # Rhino.prefab, Rhino_Regatta race line


def regatta_policy():
    """rhino_drive_sim.Policy with the Regatta variant's serialized numbers."""
    p = rd.Policy()
    try:
        text = open(REGATTA_POLICY, encoding="utf-8").read()
    except OSError:
        return p
    for key, attr in FIELDS.items():
        m = re.search(r"^\s+%s:\s*([-0-9.eE]+)" % key, text, re.M)
        if m:
            setattr(p, attr, float(m.group(1)))
    return p


def race(intensity, pol=None, T=420, dt=1 / 60, own=0, drive=True, path_angle=True, lag=True, look_s=None):
    C = sf.course_from(sf.load_course_js(), intensity)
    gates = C['gates']; n = len(gates); total = 3 * n
    P = [g['p'] for g in gates]; A = [unit(g['a']) for g in gates]
    tight = min(g['r'] for g in gates)
    room = max(0.0, tight - CLEAR * (1.5 + (0.75 - 1.5) * RISK) - L.RAIL_OFFSET)
    line = L.gap_line(C['lanes'], own, L.RAIL_OFFSET + SHARE * room)
    ring_at = [L.nearest(line, p, -1) for p in P]
    pol = pol or regatta_policy()
    ex = rd.Exec(); drv = rd.NewDriver(pol, ex)
    hint = L.nearest(line, P[0], -1)
    pos = line[(hint - 40) % len(line)]; fwd = unit(sub(line[(hint - 38) % len(line)], pos)); hint = -1
    speed = rd.CRUISE; tracked = speed; stick = 0.0; cmd = list(fwd)
    careful, bold = look_s or (L.CAREFUL, L.BOLD); lost = -1
    idx = 0; t = 0.0; ramping = 0.0; braking = 0.0; maxv = 0.0; misses = 0
    while t < T and idx < total:
        g = idx % n; ring = P[g]; ax = A[g]
        hint = L.nearest(line, pos, hint)
        if norm(sub(line[hint], pos)) > 250: hint = L.nearest(line, pos, -1)
        look = min(L.MAX_LOOK, max(L.MIN_LOOK, speed * (careful + (bold - careful) * RISK)))
        if lost != idx:
            aim = L.ring_gated_aim(line, hint, look, ring_at[g], pos, ring, ax, gates[g]['r'], CLEAR)
        else:   # a missed mouth is flown the gate way until threaded (GateRaceController)
            aim = add(ring, mul(unit(sub(ring, pos)), 320))
        after = L.point_ahead(line, ring_at[g], look)
        to_ring = sub(ring, pos)
        # path_angle: "straight" judged on where the pilot is going (the line's aim), the runway and corner on the ring
        c = {'angle': ang(fwd, sub(aim, pos)) if path_angle else ang(fwd, to_ring), 'stick': stick, 'dist': norm(to_ring), 'speed': speed,
             'corner': ang(to_ring, sub(after, ring))}
        if drive:
            drv.tick(c, t, dt, tracked)
        # AIPilot: the stick toward the line's aim, then the drive's rotation limit (LimitRotation over the two axes)
        s = ai_stick(ang(fwd, sub(aim, pos)))
        steer = 2 * s
        lim = drv.limit() if drive else -1
        if lim >= 0 and steer > lim:
            s *= lim / steer; steer = lim
        stick = s
        x = drv.throttle if (drive and drv.throttle >= 0) else 1.0   # Regatta's AIThrottleFloor is 1
        if ex.on and (1 - x) + steer >= rd.GRACE: ex.end()
        if ex.on:
            tgt = x * rd.CRUISE * rd.mult((1 - x) + steer); rate = rd.ACC if tgt >= speed else rd.BLEED
        else:
            tgt = x * rd.CRUISE; rate = rd.RET if speed > tgt else 400
        tracked = tgt
        speed += max(-rate * dt, min(rate * dt, tgt - speed))
        if ex.on: ramping += dt
        if drive and drv.mode == 'brake': braking += dt
        maxv = max(maxv, speed)
        fwd, cmd = L.turn_with_lag(fwd, cmd, sub(aim, pos), math.radians((speed * RTS + BASE) * s) * dt, dt, lag)
        newpos = add(pos, mul(fwd, speed * dt))
        a0 = dot(sub(pos, ring), ax); a1 = dot(sub(newpos, ring), ax)
        if a0 * a1 <= 0 and a0 != a1:
            k = a0 / (a0 - a1); hit = add(pos, mul(sub(newpos, pos), k))
            if norm(sub(hit, ring)) <= gates[g]['r']: idx += 1
            elif norm(sub(hit, ring)) < 1500: misses += 1; lost = idx
        pos = newpos; t += dt
    return {"time": round(t, 1) if idx >= total else None, "rings": idx, "top": round(maxv),
            "ramping": round(ramping / max(t, 1e-6), 2), "braking": round(braking / max(t, 1e-6), 2), "misses": misses}


def score(res):
    return sum(r["time"] if r["time"] else 420 + (24 - r["rings"]) * 10 for r in res)


def _sweep_one(args):
    p = regatta_policy()
    p.align, p.stick, p.erun, p.corner = args
    res = [race(I, p) for I in (1, 2, 3, 4)]
    return round(score(res), 1), args, [r["time"] for r in res], [r["ramping"] for r in res]


def sweep():
    import itertools
    from concurrent.futures import ProcessPoolExecutor
    base = regatta_policy()
    grid = list(itertools.product((12, 20, 30), (0.35, 0.5, 0.7), (0.6, 1.0, 1.5), (base.corner, 600, 1000)))
    with ProcessPoolExecutor() as ex:
        out = sorted(ex.map(_sweep_one, grid))
    print("total s (I1-I4; a stall costs 420 + 10 per ring left), (alignDegrees, stickBand, engageRunwaySeconds, "
          "cornerSpeed), per course, share of the race ramping")
    for o in out[:12]:
        print(o)
    print("shipped:", _sweep_one((base.align, base.stick, base.erun, base.corner)))


def self_test():
    bad = []
    p = regatta_policy()
    if abs(p.corner - 333) > 1: bad.append(f"Regatta variant not read (cornerSpeed {p.corner})")
    cruise = race(1, drive=False)
    if cruise["rings"] < 12 or cruise["top"] > rd.CRUISE + 1: bad.append(f"no-drive race should cruise at 50: {cruise}")
    print("rhino_line_sim self-test: " + ("ok" if not bad else "FAILED " + repr(bad)))
    return 0 if not bad else 1


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        sys.exit(self_test())
    if "--sweep" in sys.argv:
        sweep(); sys.exit(0)
    print("(seconds for 3 laps or None, rings of 24, top u/s, share ramping, share braking, mouths missed)")
    for I in (1, 2, 3, 4):
        print(f"I{I}: cruise {race(I, drive=False)['time']} s | judged by the ring (before) {race(I, path_angle=False)} | "
              f"by the path {race(I)}")
