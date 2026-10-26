#!/usr/bin/env python3
"""Offline Regatta race for the Scarab AI: today's full-throttle drive against SnapDashAIPolicySO's corner lift.

A MODEL, not the game: a point mass on the Regatta course generator's rings (situation_features.course_from), AIPilot's
own steering law (ai_stick; --linear for the older sims' proportional stick) at the Scarab's turn rate ((v x 0.1 + 100) deg/s at full stick, Scarab.prefab), the
Scarab's integrator (ScarabVesselTransformer: +90 u/s^2 x throttle, -120 u/s^2 only with the throttle released, ceiling
216 x the Time multiplier, 324 u/s at the card's Time 10), the Time-5 Snap Dash (+100 u/s for 0.4 s, at most every 2 s
on a straight with runway) and the gate steering of rhino_drive_sim.py (through the ring's centre, as Regatta flies it).
No orbit break-off: a pilot that circles a point stalls here where the game would break off. Use it to compare drives
and pick defaults; the editor is the measurement.

The corner lift MIRRORS SnapDashAIPolicySO.Driver.Tick: change both together. It is the Rhino's brake rule
(RampBoostAIPolicySO.CornerSpeedFor / BrakePoint) on the Scarab's only brake, the released throttle.

Result (2026-10-25): the corner lift is NOT shipped. On Regatta's through-the-centre steering the full-throttle Scarab
finishes I1-I4 (49-60 s) and the best lift the sweep finds is no lift (223.9 vs 224.1 s in all). The real Scarab's lost
races (5 rings in 50-61 s, two editor races of 2026-10-23) were the race line's break-offs and cut mouths, fixed for
every hull in RegattaRaceLine.AimThroughRing / AIPilot.MarkAimAsPathLead (regatta_line_sim.py: 43.8-53.6 s, no miss).

    python3 Tools/Build/scarab_drive_sim.py            # today vs the corner lift on I1-I4
    python3 Tools/Build/scarab_drive_sim.py --sweep    # the corner-speed / margin / lead grid
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import situation_features as sf
from rhino_drive_sim import add, sub, mul, dot, norm, unit, ang, rotate_toward

ACC, COAST, CEILING, DEADZONE = 90.0, 120.0, 216.0 * 1.5, 0.05
DASH_SPEED, DASH_SECONDS, DASH_COOLDOWN = 100.0, 0.4, 2.0


def omega(v): return v * 0.1 + 100.0   # deg/s at full stick


def ai_stick(err_deg):
    """AIPilot's steering law at racing range (the denominator is min(d^2, _maxDistance = 50), so 50 past 7 u):
    asin(clamp(sin^2(err) x aggressiveness 100 / 50)) in degrees, times the axis's share of the cross product, clamped.
    Roughly cubic: 0.08 stick at 5 deg off, 0.6 at 10, full from about 16."""
    e = math.radians(min(err_deg, 90.0))
    a = math.degrees(math.asin(min(1.0, math.sin(e) ** 2 * 100.0 / 50.0)))
    return min(1.0, a * math.sin(e))


class Policy:  # base AIBoostPolicySO defaults + the Snap Dash fields + the corner lift
    align, ralign, stick, rstick, erun, rrun = 12, 25, 0.35, 0.6, 1.5, 0.5
    corner, mincorner, margin, lead, turn_lift = 150.0, 60.0, 40.0, 0.2, 45.0

    def is_straight(s, c): return c['angle'] <= s.align and c['stick'] <= s.stick
    def runway(s, c): return c['dist'] / max(c['speed'], 1) >= s.erun

    def corner_speed(s, deg):   # RampBoostAIPolicySO.CornerSpeedFor
        th = 90 if deg < 0 else min(179, max(1, deg))
        return max(s.mincorner, s.corner / math.tan(math.radians(th / 2)))

    def wanted(s, c):
        """The speed to be at now: the corner ahead once its brake point is reached, and the turn being flown now when
        the objective is more than turn_lift degrees off the course. inf = hold the throttle."""
        w = math.inf
        vc = s.corner_speed(c['corner'])
        room = c['dist'] - s.margin - c['speed'] * s.lead
        if c['speed'] > vc and room <= (c['speed'] ** 2 - vc ** 2) / (2 * COAST):
            w = vc
        if c['angle'] >= s.turn_lift:
            w = min(w, s.corner_speed(180 - c['angle']))
        return w


THROUGH_CENTRE = True


def race(kind, intensity, T=300, dt=1 / 60, pol=None, real_stick=True):
    C = sf.course_from(sf.load_course_js(), intensity)
    gates = C['gates']; n = len(gates); laps = 3; total = n * laps
    P = [g['p'] for g in gates]; A = [unit(g['a']) for g in gates]
    pos = add(P[0], mul(A[0], 1500)); fwd = unit(sub(P[0], pos)); speed = 0.0
    pol = pol or Policy()
    idx = 0; locked = -1; side = 1; t = 0.0; stick = 0.0; maxv = 0; dash_until = -1; next_dash = 0; lifted = 0.0
    while t < T and idx < total:
        g = idx % n; ring = P[g]; ax = A[g]
        if idx != locked:
            locked = idx; side = 1 if dot(sub(pos, ring), ax) >= 0 else -1
            leg_from = P[(idx - 1) % n] if idx > 0 else pos
            lead = min(480, 0.4 * norm(sub(leg_from, ring)))
        R = max(speed, 1) / math.radians(omega(speed))
        commit = min(420, lead + 0.5 * R)
        approaching = norm(sub(ring, pos)) > commit and norm(sub(add(ring, mul(ax, side * lead)), pos)) > max(60, 0.5 * R)
        through = add(ring, mul(unit(sub(ring, pos)), 320)) if THROUGH_CENTRE else sub(ring, mul(ax, side * 320))
        if approaching: target = add(ring, mul(ax, side * lead)); after = through
        else: target = through; after = P[(idx + 1) % n] if idx + 1 < total else None
        to = sub(target, pos)
        nl = sub(after, target) if after is not None else None
        c = {'angle': ang(fwd, to), 'stick': stick, 'dist': norm(to), 'speed': speed,
             'corner': ang(to, nl) if nl is not None else -1}
        # the drive
        x = 1.0
        if kind == 'lift' and speed > pol.wanted(c):
            x = 0.0
        if x <= DEADZONE: lifted += dt
        if t >= next_dash and pol.is_straight(c) and pol.runway(c) and x > DEADZONE:
            dash_until = t + DASH_SECONDS; next_dash = t + DASH_COOLDOWN
        # pilot steering: proportional on the angle error
        s = ai_stick(c['angle']) if real_stick else min(1.0, c['angle'] / 20.0)
        stick = s
        # the integrator
        speed = speed + x * ACC * dt
        if x <= DEADZONE: speed = max(speed - COAST * dt, 0.0)
        speed = min(max(speed, 0.0), CEILING)
        v_fly = speed + (DASH_SPEED if t < dash_until else 0.0)
        maxv = max(maxv, v_fly)
        fwd = rotate_toward(fwd, to, math.radians(omega(v_fly) * s) * dt)
        newpos = add(pos, mul(fwd, v_fly * dt))
        a0 = dot(sub(pos, ring), ax); a1 = dot(sub(newpos, ring), ax)
        if a0 * a1 <= 0 and a0 != a1:
            k = a0 / (a0 - a1); hit = add(pos, mul(sub(newpos, pos), k))
            if norm(sub(hit, ring)) <= gates[g]['r']: idx += 1
            elif not approaching: locked = -1   # a missed mouth: re-latch the side and the approach point
        pos = newpos; t += dt
    return (round(t, 1) if idx >= total else None), idx, round(maxv), round(lifted / max(t, 1e-6), 2)


def score(res):
    return sum(r[0] if r[0] else 300 + (24 - r[1]) * 10 for r in res)


def _sweep_one(args):
    p = Policy()
    p.corner, p.margin, p.lead, p.turn_lift = args
    res = [race('lift', I, pol=p) for I in (1, 2, 3, 4)]
    return score(res), args, [r[0] for r in res]


def sweep():
    import itertools
    from concurrent.futures import ProcessPoolExecutor
    grid = list(itertools.product((60, 100, 150, 220, 320, 500), (0, 40, 120), (0, 0.2, 0.5), (30, 45, 70, 180)))
    with ProcessPoolExecutor() as ex:
        out = sorted(ex.map(_sweep_one, grid))
    print("total s (I1-I4; a stall costs 300 + 10 per ring left), (cornerSpeed, brakeMarginUnits, brakeLeadSeconds, "
          "turnLiftDegrees), per course")
    for o in out[:12]:
        print(o)


if __name__ == '__main__':
    if "--sweep" in sys.argv:
        sweep()
    else:
        print("(seconds for 3 laps or None = stalled, rings threaded of 24, top speed, share of the race lifted)")
        real = "--linear" not in sys.argv
        for I in (1, 2, 3, 4):
            print(f"I{I}: today", race('today', I, real_stick=real), "  corner lift", race('lift', I, real_stick=real))
