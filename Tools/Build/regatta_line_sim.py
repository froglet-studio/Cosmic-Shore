#!/usr/bin/env python3
"""Offline Regatta race on the hull's RACE LINE, with AIPilot's orbit break-off: does the break-off fire on the line?

A STEERING model, not the game. Each hull flies at a constant speed (its median speed as an AI rival in people's editor
Regatta races, regatta_rival_report.py) with its own turn law ((v x RotationThrottleScaler + min(Pitch, Yaw)) deg/s, the
prefab's numbers) and AIPilot's own stick law (scarab_drive_sim.ai_stick), on:

- the race line RegattaController.TryRaceLine flies: RegattaRaceLine.BuildGapLine over the exported rails (the gap
  between the two lanes that are not the pilot's, at RaceLineStyleSO.LaneRadius from the spine; the hull's
  Resources/RaceLines/<Hull>_Regatta numbers at its style risk), aimed RaceLineStyleSO.Lookahead ahead of the nearest
  line point;
- AIPilot.UpdateOrbitBreak as shipped: the OrbitDetector (360 deg swept without the range falling below 0.9 of the
  window's best) and the inside-the-turning-circle test (only when not closing, cos 0.5; capture radius 18), then an
  escape leg (heading + 0.35 x away, until the aim is RequiredApproachRun away, 0.6-6 s);
- GateRaceController's miss rule: a crossing of the ring's plane outside the mouth leaves the line for that ring and flies
  the gate way (through the ring's centre, 320 u past it) until it is threaded.

The fix (2026-10-25), both halves shipped:
- exempt=True: the race line's aim is a lead point on a path, like a rail's, which UpdateOrbitBreak already exempts, so
  the break-off never judges it (AIPilot.MarkAimAsPathLead, called by GateRaceController for the line's aim);
- gate=True, only_when_cut=True: when the chord to the look-ahead point would cross the next ring's plane outside the
  mouth less the hull's room, aim straight through the line's crossing point instead (RegattaRaceLine.AimThroughRing).

    python3 Tools/Build/regatta_line_sim.py              # every styled hull, I1-I4, before vs the fix
    python3 Tools/Build/regatta_line_sim.py --self-test
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import situation_features as sf
from rhino_drive_sim import add, sub, mul, dot, norm, unit, ang, rotate_toward
from scarab_drive_sim import ai_stick

RAIL_OFFSET = 22.0   # RegattaCourse.DefaultRails.LaneOffset
# hull: (speed u/s as an AI rival, RotationThrottleScaler, min(Pitch, Yaw), LaneShare, HullClearance, Risk)
HULLS = {
    "Manta":   (267, 0.2, 30, 0.809, 16, 0.5),
    "Dolphin": (120, 0.0, 110, 0.785, 12, 0.5),
    "Rhino":   (130, 0.2, 90, 0.676, 18, 0.4),
    "Scarab":  (322, 0.1, 100, 0.6, 12, 0.45),
    "Serpent": (157, 0.4, 80, 0.6, 12, 0.5),
    "Sparrow": (80, 0.1, 80, 0.55, 10, 0.5),
}
CAREFUL, BOLD, MIN_LOOK, MAX_LOOK = 0.35, 0.8, 60, 520


def gap_line(lanes, own, radius):
    """RegattaRaceLine.BuildGapLine(lanes[own+1], lanes[own+2], lanes[own], radius), matched by nearest point."""
    a, b, c = lanes[(own + 1) % 3], lanes[(own + 2) % 3], lanes[own]

    def match(lst, p, guess):
        best, bd = guess, 1e18
        for k in range(guess - 40, guess + 41):
            i = k % len(lst)
            d = dot(sub(lst[i], p), sub(lst[i], p))
            if d < bd: best, bd = i, d
        return best

    pts = []
    for i, pa in enumerate(a):
        pb = b[match(b, pa, i * len(b) // len(a))]
        pc = c[match(c, pa, i * len(c) // len(a))]
        spine = mul(add(add(pa, pb), pc), 1 / 3)
        gap = sub(mul(add(pa, pb), 0.5), spine)
        m = norm(gap)
        pts.append(add(spine, mul(gap, radius / m)) if m > 1e-4 else spine)
    return pts


def nearest(line, p, hint):
    n = len(line)
    rng = range(n) if hint < 0 else range(hint - 60, hint + 61)
    best, bd = (hint if hint >= 0 else 0), 1e18
    for k in rng:
        i = k % n
        d = dot(sub(line[i], p), sub(line[i], p))
        if d < bd: best, bd = i, d
    return best


def point_ahead(line, i, dist):
    n = len(line)
    left = dist
    for _ in range(n):
        j = (i + 1) % n
        seg = norm(sub(line[j], line[i]))
        if seg >= left:
            return add(line[i], mul(sub(line[j], line[i]), left / max(seg, 1e-6)))
        left -= seg; i = j
    return line[i]


def along(line, i, j):
    """Distance along the closed line from point i forward to point j."""
    n = len(line); d = 0.0
    while i != j:
        k = (i + 1) % n; d += norm(sub(line[k], line[i])); i = k
    return d


def chord_cuts_mouth(pos, aim, ring, axis, radius, room):
    """Whether the straight line from the pilot to its aim crosses the ring's plane outside the mouth less the hull's
    room (RaceGateRing.PlaneCrossing's test, on the chord the pilot is about to fly). False when it does not reach the
    plane at all."""
    a0 = dot(sub(pos, ring), axis); a1 = dot(sub(aim, ring), axis)
    if a0 * a1 > 0 or a0 == a1:
        return False
    k = a0 / (a0 - a1)
    hit = add(pos, mul(sub(aim, pos), k))
    return norm(sub(hit, ring)) > max(0.0, radius - room)


def ring_gated_aim(line, hint, look, ring_i, pos, ring=None, axis=None, radius=0.0, room=0.0):
    """The fix's aim: the look-ahead point, unless the ring's crossing point (the line point nearest its centre) is
    nearer along the line than the look-ahead - then the crossing point, pushed straight on through it from the pilot
    by what is left of the look-ahead, so the pilot flies at the mouth and never cuts across it."""
    left = along(line, hint, ring_i)
    free = point_ahead(line, hint, look)
    if left > look:
        return free
    if ring is not None and not chord_cuts_mouth(pos, free, ring, axis, radius, room):
        return free
    cross = line[ring_i]
    return add(cross, mul(unit(sub(cross, pos)), max(0.0, look - left)))


class Orbit:   # PursuitReachability.OrbitDetector
    def __init__(s): s.reset()
    def reset(s): s.has = False; s.swept = 0.0
    def tick(s, to):
        d = norm(to)
        if d < 1e-6: s.reset(); return False
        b = mul(to, 1 / d)
        if not s.has:
            s.has, s.last, s.best, s.lastd, s.swept = True, b, d, d, 0.0; return False
        if d > s.lastd * 1.6:
            s.last, s.best, s.lastd, s.swept = b, d, d, 0.0; return False
        s.swept += ang(s.last, b); s.last, s.lastd = b, d
        if d < s.best * 0.9:
            s.best, s.swept = d, 0.0; return False
        return s.swept >= 360


def inside_turning_circle(to, heading, R, capture=18.0):
    d = norm(to)
    if R <= 0 or d < 1e-6: return False
    f = unit(heading)
    lateral = norm(sub(to, mul(f, dot(to, f))))
    if lateral < 1e-6: return False
    # inside the circle of radius R tangent to the heading on the target's side: |to - R n|^2 < (R - c)^2
    n = unit(sub(to, mul(f, dot(to, f))))
    centre = mul(n, R)
    return norm(sub(to, centre)) < max(0.0, R - capture)


def race(hull, intensity, exempt, T=240, dt=1 / 60, own=0, gate=False, only_when_cut=False):
    v, rts, base, share, clear, risk = HULLS[hull]
    C = sf.course_from(sf.load_course_js(), intensity)
    gates = C['gates']; n = len(gates); total = 3 * n
    P = [g['p'] for g in gates]; A = [unit(g['a']) for g in gates]
    tight = min(g['r'] for g in gates)
    room = max(0.0, tight - clear * (1.5 + (0.75 - 1.5) * risk) - RAIL_OFFSET)
    line = gap_line(C['lanes'], own, RAIL_OFFSET + share * room)
    look = min(MAX_LOOK, max(MIN_LOOK, v * (CAREFUL + (BOLD - CAREFUL) * risk)))
    omega = v * rts + base
    R = v / math.radians(omega)
    run = max(max(0.0, 2 * R - 18), v * 1.5)
    ring_at = [nearest(line, p, -1) for p in P]
    hint = nearest(line, P[0], -1)
    pos = point_ahead(line, (hint - 40) % len(line), 0); fwd = unit(sub(line[(hint - 38) % len(line)], pos))
    hint = -1
    idx = 0; t = 0.0; lost = -1; orbit = Orbit(); extending = False; ext_t = 0.0; breaks = 0; misses = 0
    while t < T and idx < total:
        g = idx % n; ring = P[g]; ax = A[g]
        on_line = lost != idx
        if on_line:
            hint = nearest(line, pos, hint)
            if norm(sub(line[hint], pos)) > 250: hint = nearest(line, pos, -1)
            if not gate:
                aim = point_ahead(line, hint, look)
            elif only_when_cut:
                aim = ring_gated_aim(line, hint, look, ring_at[g], pos, ring, ax, gates[g]['r'], clear)
            else:
                aim = ring_gated_aim(line, hint, look, ring_at[g], pos)
        else:
            aim = add(ring, mul(unit(sub(ring, pos)), 320))
        to = sub(aim, pos)
        # AIPilot.UpdateOrbitBreak
        if exempt and on_line:
            extending = False; orbit.reset()
        elif extending:
            ext_t += dt
            if (norm(to) >= run and ext_t >= 0.6) or ext_t >= 6: extending = False
        else:
            closing = dot(unit(to), fwd) > 0.5
            if (not closing and inside_turning_circle(to, fwd, R)) or orbit.tick(to):
                extending = True; ext_t = 0.0; breaks += 1; orbit.reset()
        steer = add(fwd, mul(unit(to), -0.35)) if extending else to
        s = ai_stick(ang(fwd, steer))
        fwd = rotate_toward(fwd, steer, math.radians(omega * s) * dt)
        newpos = add(pos, mul(fwd, v * dt))
        a0 = dot(sub(pos, ring), ax); a1 = dot(sub(newpos, ring), ax)
        if a0 * a1 <= 0 and a0 != a1:
            k = a0 / (a0 - a1); hit = add(pos, mul(sub(newpos, pos), k))
            if norm(sub(hit, ring)) <= gates[g]['r']:
                idx += 1
            elif norm(sub(hit, ring)) < 1500:
                misses += 1; lost = idx
        pos = newpos; t += dt
    return {"time": round(t, 1) if idx >= total else None, "rings": idx, "rpm": round(60 * idx / t, 1),
            "breaks": breaks, "misses": misses}


def _before_after(job):
    h, I = job
    return race(h, I, False), race(h, I, True, gate=True, only_when_cut=True)


def self_test():
    bad = []
    o = Orbit(); c = [0, 0, 0]
    # a constant-range lead point swept round a full circle IS read as an orbit by the shipped detector...
    fired = any(o.tick([100 * math.cos(math.radians(a)), 0, 100 * math.sin(math.radians(a))]) for a in range(0, 400, 2))
    if not fired: bad.append("detector never fires on a constant-range sweep")
    # ...and a closing approach is not
    o = Orbit()
    if any(o.tick([0, 0, 1000 - 5 * i]) for i in range(150)): bad.append("detector fired on a straight approach")
    r = race("Scarab", 1, exempt=True)
    if r["breaks"] != 0: bad.append("the exemption still broke off on the line")
    before, after = race("Scarab", 3, False), race("Scarab", 3, True, gate=True, only_when_cut=True)
    if not (before["breaks"] + before["misses"] > 0 and after["breaks"] + after["misses"] == 0):
        bad.append(f"Scarab I3: before {before}, after {after} (the fix no longer clears the misses, or the model lost them)")
    print("regatta_line_sim self-test: " + ("ok" if not bad else "FAILED " + repr(bad)))
    return 0 if not bad else 1


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        sys.exit(self_test())
    from concurrent.futures import ProcessPoolExecutor
    jobs = [(h, I) for h in HULLS for I in (1, 2, 3, 4)]
    print("hull     I  before (s / rings, rings/min, break-offs, misses)       the fix")
    with ProcessPoolExecutor() as ex:
        for (h, I), (a, b) in zip(jobs, ex.map(_before_after, jobs)):
            f = lambda r: f"{str(r['time']):>5} / {r['rings']:2d}, {r['rpm']:5.1f} r/min, {r['breaks']:2d} brk, {r['misses']:2d} miss"
            print(f"{h:8} I{I}  {f(a)}     {f(b)}")
