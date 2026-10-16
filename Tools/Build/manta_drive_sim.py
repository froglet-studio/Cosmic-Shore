#!/usr/bin/env python3
"""Offline Regatta race for the Manta AI: the Soar drive (boost by how straight the stick is, MantaAnalogTurnBoostExecutor)
at its stick bands, and a corner-aware variant that also drops Soar ahead of a corner (sized by the corner after the
objective, as the Rhino's brake). Result 2026-10-17: the corner-aware variant is SLOWER on every course (the Manta's turn
rate grows with speed, 0.2 v + 30 deg/s, so its radius barely shrinks when it slows: 237 u at 720 u/s, 156 u at 180) and
it was not shipped; wider stick bands are faster (--bands): Regatta flies 0.8 / 0.85 (MantaSoarAITuning_Regatta).

A MODEL, not the game. A point mass on the Regatta course generator's rings flies the gate steering as Regatta does
(approach point, then through the ring's CENTRE; see rhino_drive_sim.py) with the Manta's numbers (Manta.prefab):
cruise 180 u/s, boost multiplier 4 (Soar 720 u/s), speed lerp 1.5/s toward the target, stick turn rate
(0.2 v + 30) deg/s (RotationThrottleScaler 0.2, the smaller of Pitch 50 / Yaw 30), Yastri trigger yaw 60 deg/s at a full
net pull. The stick is proportional to the angle error, as rhino_drive_sim.py. No break-off and no rotation follow lag:
the editor is the measurement.

triggers() MIRRORS MantaAnalogTurnBoostExecutor.AutopilotTriggers: change both together. cap_for() exists only here.

    python3 Tools/Build/manta_drive_sim.py            # default bands vs Regatta's, and the corner-aware variant
    python3 Tools/Build/manta_drive_sim.py --bands    # the boost / pivot band grid
    python3 Tools/Build/manta_drive_sim.py --sweep    # the corner-aware variant's corner speed / margin grid
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import situation_features as sf
from rhino_drive_sim import add, sub, mul, dot, norm, unit, ang, rotate_toward

CRUISE, MULT, LERP, YASTRI_YAW = 180.0, 4.0, 1.5, 60.0
BOOST_BAND, YASTRI_BAND = 0.35, 0.75


class Tuning:
    corner, mincorner, margin, lead = 520.0, 180.0, 80.0, 0.2


def intent(stick, band):
    if band <= 0: return 0.0
    s = abs(stick)
    if s <= band: return 1.0
    return max(0.0, min(1.0, 1 - (s - band) / max(1e-4, 1 - band)))


def triggers(yaw, stick, band, yband, cap=1.0):
    hold = min(intent(stick, band), cap)
    lt = rt = hold
    y = abs(yaw)
    if y > yband:
        ex = max(0.0, min(1.0, (y - yband) / max(1e-4, 1 - yband)))
        outer = min(1.0, hold + ex)
        if yaw > 0: rt = outer
        else: lt = outer
    return lt, rt


def corner_speed(deg, t=Tuning):
    th = 90 if deg < 0 else min(179, max(1, deg))
    return max(t.mincorner, t.corner / math.tan(math.radians(th / 2)))


def brake_distance(v, vc, cruise, k):
    """Units to fall from v to vc with every trigger released: v - cruise decays as e^-kt."""
    if v <= vc or vc <= cruise: return 0.0 if v <= vc else float('inf')
    tt = math.log((v - cruise) / (vc - cruise)) / k
    return (v - vc) / k + cruise * tt


def cap_for(dist, corner_deg, v, t=Tuning):
    """The most Soar the drive may hold now: none inside the brake zone while above the corner speed, then just enough to
    hold the corner speed."""
    vc = max(corner_speed(corner_deg, t), CRUISE * 1.05)
    if dist - t.margin - v * t.lead > brake_distance(v, vc, CRUISE, LERP): return 1.0
    if v > vc + 5: return 0.0
    return max(0.0, min(1.0, (vc / CRUISE - 1) / (MULT - 1)))


def race(aware, intensity, T=300, dt=1 / 60, bands=None):
    band, yband = bands or (BOOST_BAND, YASTRI_BAND)
    C = sf.course_from(sf.load_course_js(), intensity)
    gates = C['gates']; n = len(gates); total = n * 3
    P = [g['p'] for g in gates]; A = [unit(g['a']) for g in gates]
    pos = add(P[0], mul(A[0], 1500)); fwd = unit(sub(P[0], pos)); v = CRUISE
    idx, locked, side, lead, t, top = 0, -1, 1, 480, 0.0, 0
    while t < T and idx < total:
        g = idx % n; ring, ax = P[g], A[g]
        if idx != locked:
            locked = idx; side = 1 if dot(sub(pos, ring), ax) >= 0 else -1
            lead = min(480, 0.4 * norm(sub(P[(idx - 1) % n] if idx > 0 else pos, ring)))
        R = v / math.radians(0.2 * v + 30)
        approach = add(ring, mul(ax, side * lead))
        approaching = norm(sub(ring, pos)) > min(420, lead + 0.5 * R) and norm(sub(approach, pos)) > max(60, 0.5 * R)
        through = add(ring, mul(unit(sub(ring, pos)), 320))
        target, after = (approach, through) if approaching else (through, P[(idx + 1) % n] if idx + 1 < total else None)
        to = sub(target, pos)
        err = ang(fwd, to); stick = min(1.0, err / 20.0)
        corner = ang(to, sub(after, target)) if after is not None else -1
        cap = cap_for(norm(to), corner, v) if aware else 1.0
        lt, rt = triggers(stick, stick, band, yband, cap)
        boost = min(lt, rt); net = abs(rt - lt)
        v += (CRUISE * (1 + (MULT - 1) * boost) - v) * LERP * dt
        top = max(top, v)
        rate = (0.2 * v + 30) * stick + YASTRI_YAW * net
        fwd = rotate_toward(fwd, to, math.radians(rate) * dt)
        newpos = add(pos, mul(fwd, v * dt))
        a0, a1 = dot(sub(pos, ring), ax), dot(sub(newpos, ring), ax)
        if a0 * a1 <= 0 and a0 != a1:
            k = a0 / (a0 - a1)
            if norm(sub(add(pos, mul(sub(newpos, pos), k)), ring)) <= gates[g]['r']: idx += 1
        pos = newpos; t += dt
    return (round(t, 1) if idx >= total else None), idx, round(top)


def _sweep_one(a):
    Tuning.corner, Tuning.margin, Tuning.lead = a
    res = [race(True, I) for I in (1, 2, 3, 4)]
    return sum(r[0] if r[0] else 300 + (24 - r[1]) * 10 for r in res), a, [r[0] for r in res]


REGATTA_BANDS = (0.8, 0.85)   # MantaSoarAITuning_Regatta.asset


def _bands_one(b):
    res = [race(False, I, bands=b) for I in (1, 2, 3, 4)]
    return sum(r[0] if r[0] else 300 + (24 - r[1]) * 10 for r in res), b, [r[0] for r in res]


if __name__ == '__main__':
    if "--bands" in sys.argv:
        from concurrent.futures import ProcessPoolExecutor
        grid = [(b, y) for b in (0.2, 0.35, 0.5, 0.65, 0.8, 0.9) for y in (0.5, 0.75, 0.85, 0.95) if y >= b]
        with ProcessPoolExecutor() as ex:
            out = sorted(ex.map(_bands_one, grid))
        print("total s (I1-I4), (BoostStickBand, YastriStickBand), per course")
        for o in out[:10]: print(o)
    elif "--sweep" in sys.argv:
        import itertools
        from concurrent.futures import ProcessPoolExecutor
        grid = list(itertools.product((260, 360, 440, 520, 640, 800), (0, 80, 200), (0.0, 0.2, 0.5)))
        with ProcessPoolExecutor() as ex:
            out = sorted(ex.map(_sweep_one, grid))
        print("total s (I1-I4), (cornerSpeed, brakeMarginUnits, brakeLeadSeconds), per course")
        for o in out[:10]: print(o)
    else:
        print("(seconds for 3 laps or None = stalled, rings threaded, top speed)")
        for I in (1, 2, 3, 4):
            print(f"I{I}: default bands", race(False, I), f" Regatta bands {REGATTA_BANDS}", race(False, I, bands=REGATTA_BANDS),
                  " corner-aware (not shipped)", race(True, I))
