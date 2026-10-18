#!/usr/bin/env python3
"""Offline Regatta race for the Dolphin AI: ChargeBoostAIPolicySO's first drive against its re-drift-at-the-peak drive.

A MODEL, not the game. A point mass on the Regatta course generator's rings (situation_features.course_from) flies the
gate steering (approach point, mouth, commit distance, as rhino_drive_sim.py) with the Dolphin's numbers: cruise 68 u/s,
turn 110 deg/s (RotationThrottleScaler 0), speed lerp 1.5/s toward its target (the scalar model, VesselTransformer
LERP_AMOUNT). The AIPilot COMMIT LOOP owns the drift: drift when the course is within ~25 deg of the steering point
(dot >= 0.9; the course then locks on it and the speed freezes, holdSpeedWhileDrifting), let go when it is not. Holding
the drift banks charge (1 / 3.636 s); letting go discharges it (drains at 0.4/s, target = cruise x m(meter) x m(at
release), m = 1 + 1.259 x meter: DOLPHIN_ENERGY_ECONOMY.md §2).

The Python drivers MIRROR ChargeBoostAIPolicySO.Driver.Tick (old = before 2026-10-17): change both together.

    python3 Tools/Build/dolphin_drive_sim.py               # old vs new on I1-I4
    python3 Tools/Build/dolphin_drive_sim.py --straight    # the 1D straight economy only
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import situation_features as sf
from rhino_drive_sim import add, sub, mul, dot, norm, unit, ang, rotate_toward

CRUISE, MAXM, FILL, DRAIN, LERP, TURN = 68.0, 2.259, 1 / 3.636, 0.4, 1.5, 110.0
ALIGN, RUNWAY, SPEND, MAX_HOLD, GRACE = 12.0, 2.5, 0.95, 4.0, 0.25
PEAK_MARGIN, MIN_DIS, MIN_GAIN = 3.0, 0.2, 30.0


def m(x): return 1 + (MAXM - 1) * x


class Driver:
    def __init__(s, new): s.new, s.hold, s.req, s.at, s.was = new, False, False, -1e9, False

    def tick(s, t, c, ship):
        dis = ship.discharging
        if s.new and dis and not s.was and not s.hold:
            s.hold, s.at = True, t
        s.was = dis
        if s.hold:
            since = t - s.at
            peaked = s.new and dis and since >= MIN_DIS and ship.speed >= ship.target() - PEAK_MARGIN
            if peaked or (not dis and since > GRACE) or since > MAX_HOLD:
                s.hold = False
            return
        if not ship.drifting: return
        if c['angle'] > ALIGN or c['nose'] > ALIGN: return
        if c['dist'] / max(ship.speed, 1) < RUNWAY: return
        if ship.meter < SPEND: return
        if s.new and CRUISE * m(ship.meter) ** 2 - ship.speed < MIN_GAIN: return
        s.req, s.hold, s.at = True, True, t


class Ship:
    def __init__(s, pos, fwd):
        s.pos, s.fwd, s.course, s.speed = pos, fwd, fwd, CRUISE
        s.meter, s.pinned, s.drifting, s.discharging = 0.0, 1.0, False, False

    def target(s):
        return CRUISE * (m(s.meter) * s.pinned if s.discharging else 1.0)

    def drift(s):
        s.drifting, s.discharging = True, False

    def release(s):
        s.drifting = False
        s.discharging = s.meter > 0
        s.course = s.fwd

    def step(s, dt):
        if s.drifting:
            s.meter = min(1.0, s.meter + FILL * dt)
            s.pinned = m(s.meter)
        else:
            if s.discharging:
                s.meter = max(0.0, s.meter - DRAIN * dt)
                if s.meter <= 0: s.discharging = False
            s.speed += (s.target() - s.speed) * LERP * dt


THROUGH_CENTRE = True   # GateRaceController in Regatta: the through point lies on the line through the ring's centre


def race(new, intensity, T=300, dt=1 / 60):
    C = sf.course_from(sf.load_course_js(), intensity)
    gates = C['gates']; n = len(gates); total = n * 3
    P = [g['p'] for g in gates]; A = [unit(g['a']) for g in gates]
    start = add(P[0], mul(A[0], 1500))
    ship = Ship(start, unit(sub(P[0], start))); drv = Driver(new)
    idx, locked, side, lead, t, top = 0, -1, 1, 480, 0.0, 0
    while t < T and idx < total:
        g = idx % n; ring, ax = P[g], A[g]
        if idx != locked:
            locked = idx; side = 1 if dot(sub(ship.pos, ring), ax) >= 0 else -1
            lead = min(480, 0.4 * norm(sub(P[(idx - 1) % n] if idx > 0 else ship.pos, ring)))
        R = ship.speed / math.radians(TURN)
        approach = add(ring, mul(ax, side * lead))
        approaching = norm(sub(ring, ship.pos)) > min(420, lead + 0.5 * R) and norm(sub(approach, ship.pos)) > max(60, 0.5 * R)
        through = add(ring, mul(unit(sub(ring, ship.pos)), 320)) if THROUGH_CENTRE else sub(ring, mul(ax, side * 320))
        target = approach if approaching else through
        to = sub(target, ship.pos)
        c = {'angle': ang(ship.course, to), 'nose': ang(ship.fwd, to), 'dist': norm(to)}
        drv.tick(t, c, ship)
        if drv.req:
            drv.req = False
            if ship.drifting: ship.release()
        looking = dot(unit(to), unit(ship.course)) >= 0.9
        if looking and not ship.drifting and not drv.hold:
            ship.course = unit(to); ship.drift()
        elif ship.drifting and not looking:
            ship.release()
        # the nose turns toward the steering point in and out of the drift; the course follows it only outside one
        s_ = min(1.0, ang(ship.fwd, to) / 20.0)
        ship.fwd = rotate_toward(ship.fwd, to, math.radians(TURN * s_) * dt)
        if not ship.drifting: ship.course = ship.fwd
        ship.step(dt)
        top = max(top, ship.speed)
        newpos = add(ship.pos, mul(ship.course, ship.speed * dt))
        a0, a1 = dot(sub(ship.pos, ring), ax), dot(sub(newpos, ring), ax)
        if a0 * a1 <= 0 and a0 != a1:
            k = a0 / (a0 - a1)
            if norm(sub(add(ship.pos, mul(sub(newpos, ship.pos), k)), ring)) <= gates[g]['r']: idx += 1
        ship.pos = newpos; t += dt
    return (round(t, 1) if idx >= total else None), idx, round(top)


def straight(new, T=60, dt=0.01):
    """A straight with no corners: average speed when the drive alone decides."""
    ship = Ship([0, 0, 0], [0, 0, 1]); drv = Driver(new); t = dist = 0.0
    ship.drift()
    while t < T:
        c = {'angle': 0.0, 'nose': 0.0, 'dist': 1e9}
        drv.tick(t, c, ship)
        if drv.req:
            drv.req = False; ship.release()
        if not ship.drifting and not drv.hold: ship.drift()
        ship.step(dt); dist += ship.speed * dt; t += dt
    return round(dist / T)


if __name__ == '__main__':
    print(f"straight, average u/s: old {straight(False)}  new {straight(True)}")
    if "--straight" not in sys.argv:
        print("(seconds for 3 laps or None = stalled, rings threaded, top speed)")
        for I in (1, 2, 3, 4):
            print(f"I{I}: old", race(False, I), " new", race(True, I))
