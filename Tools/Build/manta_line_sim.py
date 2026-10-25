#!/usr/bin/env python3
"""Offline Regatta race for the Manta ON ITS RACE LINE: its Soar (MantaAnalogTurnBoostExecutor.AutopilotTriggers) on the
real line, with AIPilot's own stick law and the line's 2026-10-25 fixes.

manta_drive_sim.py flew the Soar on the old through-the-centre gate steering with a proportional stick, and
regatta_line_sim.py flew the line at a constant 267 u/s, so neither raced the Manta that ships: the line at the Regatta
bands (Resources/AIBoostPolicies/MantaSoarAITuning_Regatta, 0.975 / 0.975 since 2026-10-24) where it holds Soar
(cruise 180 x 4 = 720 u/s, lerp 1.5/s) except on its hardest corners, turning at (0.2 v + 30) deg/s plus the Yastri
trigger pivot (60 deg/s at a full net pull). This one does, and also re-tries the corner cap manta_drive_sim modelled
and dropped (cut Soar before a corner the Manta cannot make at speed), because on the line a missed 72 u mouth costs a
re-attack the old model never charged.

    python3 Tools/Build/manta_line_sim.py              # bands: default / the old Regatta / shipped, and the corner cap
    python3 Tools/Build/manta_line_sim.py --sweep      # the corner cap's corner speed / margin / lead grid
    python3 Tools/Build/manta_line_sim.py --self-test
"""
import math, os, re, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import situation_features as sf
import manta_drive_sim as md
import regatta_line_sim as L
from rhino_drive_sim import add, sub, mul, dot, norm, unit, ang, rotate_toward
from scarab_drive_sim import ai_stick

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
REGATTA_TUNING = os.path.join(ROOT, "Assets/Resources/AIBoostPolicies/MantaSoarAITuning_Regatta.asset")
SHARE, CLEAR, RISK = 0.809, 16, 0.5   # Resources/RaceLines/Manta_Regatta
DEFAULT_BANDS = (md.BOOST_BAND, md.YASTRI_BAND)
OLD_REGATTA_BANDS = (0.8, 0.85)       # 2026-10-17 to 10-24


def regatta_bands():
    try:
        text = open(REGATTA_TUNING, encoding="utf-8").read()
        b = float(re.search(r"BoostStickBand:\s*([0-9.]+)", text).group(1))
        y = float(re.search(r"YastriStickBand:\s*([0-9.]+)", text).group(1))
        return b, y
    except (OSError, AttributeError):
        return OLD_REGATTA_BANDS


NOSE_FOLLOW = 1.5     # VesselTransformer.LERP_AMOUNT: AI, autopilot and remote hulls keep this nose lag (2026-10-21)
RAM_DISTANCE = 18.0   # u from a rail prism's centre: the prism's half-size (9-12) plus a little hull


class RailGrid:
    """The exported rails' prism centres in a 32 u hash grid, for the nearest-rail distance each frame."""
    def __init__(s, lanes, cell=32.0):
        s.cell, s.g = cell, {}
        for lane in lanes:
            for q in lane:
                s.g.setdefault(tuple(int(math.floor(x / cell)) for x in q), []).append(q)

    def nearest(s, p):
        k = [int(math.floor(x / s.cell)) for x in p]
        best = 1e9
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    for q in s.g.get((k[0] + dx, k[1] + dy, k[2] + dz), ()):
                        d = norm(sub(p, q))
                        if d < best: best = d
        return best


def race(intensity, bands=None, cap=None, T=300, dt=1 / 60, own=0, look_s=None, ram_cost=True, stick_lag=0.0, nose=None, blend=0.5, max_look=None):
    """look_s: (careful, bold) look-ahead seconds in place of the Manta_Regatta style's. ram_cost: a ram on a rail that is
    not the pilot's colour halves the speed (MantaVesselChangeSpeedByPrism: maxSlowStrength 0.5, which every rail prism
    reaches; its own colour's rail is exempt), as the game does; the line flies the gap opposite its own rail.
    stick_lag: seconds of first-order lag between the stick asked for and the turn flown (a robustness check: the
    model otherwise turns the instant the stick moves, which flatters a short look-ahead).
    nose: None (the nose IS the command, the older models), "nose" (the game: the stick turns the COMMANDED heading
    and the nose follows it at VesselTransformer's fleet lag, LERP_AMOUNT 1.5/s, which AI hulls keep; the pilot judges
    its error from the nose, as AIPilot does), "command" (the same lag, the pilot judging from the command), or
    "pred" (judging from the nose moved blend of the way to the command: where the nose is about to point)."""
    """cap: None (the shipped drive, no corner cap) or md.Tuning-like (corner, mincorner, margin, lead)."""
    band, yband = bands or regatta_bands()
    C = sf.course_from(sf.load_course_js(), intensity)
    gates = C['gates']; n = len(gates); total = 3 * n
    P = [g['p'] for g in gates]; A = [unit(g['a']) for g in gates]
    tight = min(g['r'] for g in gates)
    room = max(0.0, tight - CLEAR * (1.5 + (0.75 - 1.5) * RISK) - L.RAIL_OFFSET)
    line = L.gap_line(C['lanes'], own, L.RAIL_OFFSET + SHARE * room)
    ring_at = [L.nearest(line, p, -1) for p in P]
    hint = L.nearest(line, P[0], -1)
    pos = line[(hint - 40) % len(line)]; fwd = unit(sub(line[(hint - 38) % len(line)], pos)); hint = -1
    careful, bold = look_s or (L.CAREFUL, L.BOLD)
    harmful = RailGrid([C['lanes'][(own + 1) % 3], C['lanes'][(own + 2) % 3]])
    s_prev = 0.0
    cmd = list(fwd)
    v = md.CRUISE; idx = 0; t = 0.0; soar = 0.0; top = 0.0; misses = 0; lost = -1; ram = 0.0; rams = 0; was_in = False
    while t < T and idx < total:
        g = idx % n; ring = P[g]; ax = A[g]
        if lost != idx:
            hint = L.nearest(line, pos, hint)
            if norm(sub(line[hint], pos)) > 250: hint = L.nearest(line, pos, -1)
            look = min(max_look or L.MAX_LOOK, max(L.MIN_LOOK, v * (careful + (bold - careful) * RISK)))
            aim = L.ring_gated_aim(line, hint, look, ring_at[g], pos, ring, ax, gates[g]['r'], CLEAR)
        else:   # a missed mouth is flown the gate way until threaded (GateRaceController)
            aim = add(ring, mul(unit(sub(ring, pos)), 320))
        to = sub(aim, pos)
        if nose == "command": judge = cmd
        elif nose == "pred": judge = rotate_toward(fwd, cmd, math.radians(ang(fwd, cmd)) * blend)   # where the nose is going
        else: judge = fwd
        s_want = ai_stick(ang(judge, to))
        s = s_want if stick_lag <= 0 else s_prev + (s_want - s_prev) * min(1.0, dt / stick_lag)
        s_prev = s
        c_cap = 1.0
        if cap is not None:
            after = L.point_ahead(line, ring_at[g], look)
            corner = ang(sub(ring, pos), sub(after, ring))
            c_cap = md.cap_for(norm(sub(ring, pos)), corner, v, cap)
        lt, rt = md.triggers(s, s, band, yband, c_cap)
        boost = min(lt, rt); net = abs(rt - lt)
        v += (md.CRUISE * (1 + (md.MULT - 1) * boost) - v) * md.LERP * dt
        if boost > 0.5: soar += dt
        top = max(top, v)
        turn = math.radians((0.2 * v + 30) * s + md.YASTRI_YAW * net) * dt
        if nose is None:
            fwd = rotate_toward(fwd, to, turn)
        else:
            cmd = rotate_toward(cmd, to, turn)
            fwd = rotate_toward(fwd, cmd, math.radians(ang(fwd, cmd)) * min(1.0, NOSE_FOLLOW * dt))
        newpos = add(pos, mul(fwd, v * dt))
        a0 = dot(sub(pos, ring), ax); a1 = dot(sub(newpos, ring), ax)
        if a0 * a1 <= 0 and a0 != a1:
            k = a0 / (a0 - a1); hit = add(pos, mul(sub(newpos, pos), k))
            if norm(sub(hit, ring)) <= gates[g]['r']: idx += 1
            elif norm(sub(hit, ring)) < 1500: misses += 1; lost = idx
        pos = newpos; t += dt
        inside = harmful.nearest(pos) < RAM_DISTANCE
        if inside: ram += dt
        if inside and not was_in:
            rams += 1
            if ram_cost: v *= 0.5
        was_in = inside
    return {"time": round(t, 1) if idx >= total else None, "rings": idx, "top": round(top),
            "soar": round(soar / max(t, 1e-6), 2), "misses": misses, "rams": rams, "ram_s": round(ram, 1)}


def score(res):
    return sum(r["time"] if r["time"] else 300 + (24 - r["rings"]) * 10 for r in res)


class Cap:
    def __init__(s, corner, margin, lead, mincorner=180.0):
        s.corner, s.margin, s.lead, s.mincorner = corner, margin, lead, mincorner


def _one(args):
    kind, I, x = args
    if kind == "bands": return args, race(I, bands=x)
    return args, race(I, cap=Cap(*x))


def _sweep_one(x):
    res = [race(I, cap=Cap(*x)) for I in (1, 2, 3, 4)]
    return round(score(res), 1), x, [r["time"] for r in res], sum(r["misses"] for r in res)


def sweep():
    import itertools
    from concurrent.futures import ProcessPoolExecutor
    grid = list(itertools.product((260, 360, 440, 520, 640, 800, 1000), (0, 80, 200), (0.0, 0.2, 0.5)))
    with ProcessPoolExecutor() as ex:
        out = sorted(ex.map(_sweep_one, grid))
    shipped = [race(I) for I in (1, 2, 3, 4)]
    print("total s (I1-I4; a stall costs 300 + 10 per ring left), (cornerSpeed, brakeMarginUnits, brakeLeadSeconds), "
          "per course, misses")
    for o in out[:10]:
        print(o)
    print("shipped (no cap):", round(score(shipped), 1), [r["time"] for r in shipped], sum(r["misses"] for r in shipped))


def self_test():
    bad = []
    if race(3, ram_cost=False)["time"] >= race(3)["time"]: bad.append("a ram must cost time")
    if regatta_bands() != (0.975, 0.975): bad.append(f"Regatta bands not read: {regatta_bands()}")
    r = race(1)
    if not r["time"] or r["soar"] < 0.5: bad.append(f"the shipped Manta should finish I1 mostly in Soar: {r}")
    never = race(1, bands=(0.0, 1.0))
    if never["top"] > md.CRUISE + 1: bad.append(f"band 0 must never Soar: {never}")
    print("manta_line_sim self-test: " + ("ok" if not bad else "FAILED " + repr(bad)))
    return 0 if not bad else 1


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        sys.exit(self_test())
    if "--sweep" in sys.argv:
        sweep(); sys.exit(0)
    from concurrent.futures import ProcessPoolExecutor
    jobs = [("bands", I, b) for b in (DEFAULT_BANDS, OLD_REGATTA_BANDS, regatta_bands()) for I in (1, 2, 3, 4)]
    jobs += [("cap", I, (520, 80, 0.2)) for I in (1, 2, 3, 4)]
    with ProcessPoolExecutor() as ex:
        for (kind, I, x), r in ex.map(_one, jobs):
            print(f"{kind} {x} I{I}: {r}")
