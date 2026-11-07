#!/usr/bin/env python3
"""Offline Regatta race for the Scarab AI with the nose lag in, and the steering that leads it (AIPilot.NoseLeadGain).

A MODEL, not the game (no traffic, no rail rams, a constant 324 u/s). It is regatta_line_sim.py's race (the race line,
AIPilot's stick law, the orbit break-off, the missed-mouth rule, RegattaRaceLine.AimThroughRing) with three things that
model leaves at a default:

- the nose lag is ON: the stick turns the COMMANDED heading (VesselTransformer.accumulatedRotation) and the nose follows
  it at 1.5/s (VesselTransformer.LERP_AMOUNT, every AI hull in Regatta). regatta_line_sim's default flies without it,
  which is why it showed the Scarab finishing I1-I4 clean while the Regatta Hull Benchmark (2026-11-03, the real game, I1)
  had it missing 6-20 mouths a race. With the lag in, this model misses 16 a race at I1 and DNFs I4: the defect is back;
- the line is the one a branch ships: "be" (bleeding-edge's Resources/RaceLines/Scarab_Regatta: share 0.788, look-ahead
  0.35 / 0.8 s) or "overnight" (claude/regatta-overnight-balance: the 0.35 AI share cap and 0.6 / 1.0 s);
- every seat is raced: the three lanes (one per domain) times three risks across the seat draw (Risk 0.45 +- 0.15);
- the stick turns the command as the game does: about the judged error's axis by the stick's rate each frame, whether or
  not the command is already past the aim (Pitch / Yaw rotate accumulatedRotation about the nose's axes). The older
  models turn the command TOWARD the aim and stop it there (capped=True), which hides the overshoot below.

Why the Scarab swings wide. AIPilot judges its error from the NOSE and writes a stick that turns the COMMAND. Through
0.67 s of lag the nose is still catching up while the pilot keeps winding the command on, so the command overshoots, then
the nose does: at 324 u/s that is 60-100 u off the line (benchmark), and the ring plane is crossed beside the mouth.

The steerings compared (STEERINGS):
- "today": judge from the nose (AIPilot as shipped);
- "pred s": judge from where the nose will point s seconds from now (the nose slerped 1 - exp(-1.5 s) of the way to the
  command). At s = 0.3 it is nearly as good as the lead on the overnight line, but it still misses 16 mouths a race at
  I3 on bleeding-edge's line, and at s = 1 (judging from the command) it is little better than today: the command stops
  AT the aim and the nose lags behind it, so the hull flies wide the other way. Not shipped;
- "lead g": the fix, AIPilot.NoseLeadGain. Place the command where the lagging nose must be PULLED from: the nose turned
  toward the aim by g times its error (g > 1 carries on past the aim, at most NoseLeadMaxExtraDegrees past it), and steer
  the command onto that point, judged from the command. Under a first-order follow the nose then closes its error g times
  faster (time constant 0.67 / g s), so at g 2-2.5 it settles in about 0.3 s instead of overshooting.

    python3 Tools/Build/scarab_nose_lead_sim.py                 # today vs pred vs lead, both lines, I1-I4
    python3 Tools/Build/scarab_nose_lead_sim.py --look          # the lead crossed with longer look-aheads
    python3 Tools/Build/scarab_nose_lead_sim.py --json out.json # also write the cells
    python3 Tools/Build/scarab_nose_lead_sim.py --fleet         # every line-flying hull, today vs lead 2.5 (+ Manta 9/s)
    python3 Tools/Build/scarab_nose_lead_sim.py --self-test     # the model's claims, with a negative control
"""
import json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import situation_features as sf
import regatta_line_sim as L
from rhino_drive_sim import add, sub, mul, dot, norm, unit, ang, rotate_toward
from scarab_drive_sim import ai_stick

SPEED = 324.0                 # 216 x 1.5: the card's Time 10 ceiling, held (the AI's trigger reads as held)
RTS, BASE = 0.1, 100.0        # Scarab.prefab RotationThrottleScaler, Pitch/YawScaler: (0.1 v + 100) deg/s
NOSE_FOLLOW = 1.5             # VesselTransformer.LERP_AMOUNT
CLEAR, RISK, SPREAD = 12.0, 0.45, 0.15
LINES = {   # LaneShare, careful s, bold s
    "be": (0.788, 0.35, 0.8),
    "overnight": (0.35, 0.6, 1.0),
}
MIN_LOOK, MAX_LOOK = 60.0, 520.0

# --fleet: every hull regatta_line_sim carries, at its constant AI-rival speed and turn law (regatta_line_sim.HULLS), on
# its own line: (LaneShare, careful s, bold s) per line from Resources/RaceLines/<Hull>_Regatta on each branch. The Stoat
# (a sling orbit), the Squirrel (its own Skim Race pilot) and the Urchin (rides its rail) are not on this model.
FLEET_LINES = {
    "Manta":   {"be": (0.809, 0.6, 1.0), "overnight": (0.35, 0.6, 1.0)},
    "Dolphin": {"be": (0.785, 0.35, 0.8), "overnight": (0.35, 0.35, 0.8)},
    "Rhino":   {"be": (0.676, 0.8, 1.4), "overnight": (0.35, 0.8, 1.4)},
    "Serpent": {"be": (0.6, 0.35, 0.8), "overnight": (0.35, 0.35, 0.8)},
    "Sparrow": {"be": (0.55, 0.35, 0.8), "overnight": (0.35, 0.35, 0.8)},
    "Scarab":  {"be": LINES["be"], "overnight": LINES["overnight"]},
}


def hull_params(hull):
    """(speed, RotationThrottleScaler, min(Pitch, Yaw), clearance, risk): the Scarab at Time 10, the rest as
    regatta_line_sim.HULLS has them."""
    if hull == "Scarab": return SPEED, RTS, BASE, CLEAR, RISK
    v, rts, base, _share, clear, risk = L.HULLS[hull]
    return float(v), rts, base, float(clear), risk


def effective_gain(gain, rate):
    """AIPilot.EffectiveNoseLeadGain: the gain is stated at the fleet lag, never under 1, 0 on a nose with no lag."""
    if gain <= 0 or rate <= 0 or math.isinf(rate): return 0.0
    return max(1.0, gain * NOSE_FOLLOW / rate)
# (name, pred seconds, lead gain, lead max extra degrees)
STEERINGS = [("today", 0.0, 0.0, 0.0), ("pred 0.3", 0.3, 0.0, 0.0), ("pred 1.0", 1.0, 0.0, 0.0),
             ("lead 1.5", 0.0, 1.5, 40.0), ("lead 2", 0.0, 2.0, 40.0), ("lead 2.5", 0.0, 2.5, 40.0),
             ("lead 3", 0.0, 3.0, 40.0), ("lead 2.5 x20", 0.0, 2.5, 20.0), ("lead 2.5 x80", 0.0, 2.5, 80.0)]


def lead_share(lead_s, rate=NOSE_FOLLOW):
    """AIPilot.NoseLeadShare: the share of the nose-to-command gap the nose closes in lead_s seconds."""
    return 0.0 if lead_s <= 0 or rate <= 0 else 1.0 - math.exp(-rate * lead_s)


def cross(a, b):
    return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]


def rotate_about(v, axis, rad):
    """v turned rad about axis (Rodrigues); v when the axis is degenerate."""
    m = norm(axis)
    if m < 1e-9 or rad == 0: return v
    k = mul(axis, 1 / m)
    c, s_ = math.cos(rad), math.sin(rad)
    return unit(add(add(mul(v, c), mul(cross(k, v), s_)), mul(k, dot(k, v) * (1 - c))))


def slerp_beyond(f, d, t, extra_deg=150.0):
    """f turned toward d by t times the angle between them (t > 1 carries on PAST d along the same great circle), never
    more than extra_deg past d (nor past 170 deg from f), as AIPilot.NoseLeadDirection does."""
    a = math.radians(ang(f, d))
    if a < 1e-9: return list(f)
    so = math.sin(a)
    if so < 1e-6: return unit(d)
    turn = min(t * a, a + math.radians(extra_deg), math.radians(170.0))
    dd = unit(d)
    # the unit vector in the f-d plane perpendicular to f, toward d
    perp = unit(sub(dd, mul(f, dot(f, dd))))
    return unit(add(mul(f, math.cos(turn)), mul(perp, math.sin(turn))))


def race(intensity, line="be", lead=0.0, own=0, risk=RISK, look_mult=1.0, speed=SPEED, T=300.0, dt=1 / 60,
         gain=0.0, extra=150.0, capped=False, hull="Scarab", nose_rate=NOSE_FOLLOW):
    share, careful, bold = FLEET_LINES[hull][line]
    speed, RTS, BASE, CLEAR, _ = hull_params(hull) if hull != "Scarab" else (speed, globals()["RTS"], globals()["BASE"], globals()["CLEAR"], RISK)
    gain = effective_gain(gain, nose_rate)
    C = sf.course_from(sf.load_course_js(), intensity)
    gates = C['gates']; n = len(gates); total = 3 * n
    P = [g['p'] for g in gates]; A = [unit(g['a']) for g in gates]
    tight = min(g['r'] for g in gates)
    room_clear = CLEAR * (1.5 + (0.75 - 1.5) * risk)          # RaceLineStyleSO.ClearanceAt
    room = max(0.0, tight - room_clear - L.RAIL_OFFSET)
    pts = L.gap_line(C['lanes'], own, L.RAIL_OFFSET + share * room)
    look = min(MAX_LOOK, max(MIN_LOOK, speed * (careful + (bold - careful) * risk) * look_mult))
    omega = speed * RTS + BASE
    R = speed / math.radians(omega)
    run = max(max(0.0, 2 * R - 18), speed * 1.5)
    b = lead_share(lead)
    ring_at = [L.nearest(pts, p, -1) for p in P]
    hint = L.nearest(pts, P[0], -1)
    pos = L.point_ahead(pts, (hint - 40) % len(pts), 0); fwd = unit(sub(pts[(hint - 38) % len(pts)], pos))
    cmd = list(fwd); hint = -1
    idx = 0; t = 0.0; lost = -1; orbit = L.Orbit(); extending = False; ext_t = 0.0; breaks = 0; misses = 0
    wide = []   # distance from the line point nearest the hull, each frame on the line
    while t < T and idx < total:
        g = idx % n; ring = P[g]; ax = A[g]
        on_line = lost != idx
        if on_line:
            hint = L.nearest(pts, pos, hint)
            if norm(sub(pts[hint], pos)) > 250: hint = L.nearest(pts, pos, -1)
            wide.append(norm(sub(pts[hint], pos)))
            aim = L.ring_gated_aim(pts, hint, look, ring_at[g], pos, ring, ax, gates[g]['r'], room_clear)
        else:
            aim = add(ring, mul(unit(sub(ring, pos)), 320))     # GateRaceController's gate way after a miss
        to = sub(aim, pos)
        if on_line:                                             # MarkAimAsPathLead: the line is never judged
            extending = False; orbit.reset()
        elif extending:
            ext_t += dt
            if (norm(to) >= run and ext_t >= 0.6) or ext_t >= 6: extending = False
        else:
            closing = dot(unit(to), fwd) > 0.5
            if (not closing and L.inside_turning_circle(to, fwd, R)) or orbit.tick(to):
                extending = True; ext_t = 0.0; breaks += 1; orbit.reset()
        steer = add(fwd, mul(unit(to), -0.35)) if extending else to
        if gain > 0:
            # the lead: put the COMMAND where the lagging nose has to be pulled from, gain x the nose's error past the
            # nose, and steer the command onto that (judged from the command)
            judge, want = cmd, slerp_beyond(fwd, unit(steer), gain, extra)
        else:
            judge = rotate_toward(fwd, cmd, math.radians(ang(fwd, cmd)) * b) if b > 0 else fwd
            want = steer
        s = ai_stick(ang(judge, want))
        if capped:
            cmd = rotate_toward(cmd, want, math.radians(omega * s) * dt)
        else:
            # the game: the stick turns the command about the judged error's axis (Pitch / Yaw about the nose's axes,
            # from Cross(judged, aim)) by the stick's rate this frame, whether or not the command is already past the aim
            cmd = rotate_about(cmd, cross(judge, want), math.radians(omega * s) * dt)
        fwd = rotate_toward(fwd, cmd, math.radians(ang(fwd, cmd)) * min(1.0, nose_rate * dt))
        newpos = add(pos, mul(fwd, speed * dt))
        a0 = dot(sub(pos, ring), ax); a1 = dot(sub(newpos, ring), ax)
        if a0 * a1 <= 0 and a0 != a1:
            k = a0 / (a0 - a1); hit = add(pos, mul(sub(newpos, pos), k))
            if norm(sub(hit, ring)) <= gates[g]['r']:
                idx += 1
            elif norm(sub(hit, ring)) < 1500:
                misses += 1; lost = idx
        pos = newpos; t += dt
    wide.sort()
    return {"time": round(t, 1) if idx >= total else None, "rings": idx, "misses": misses, "breaks": breaks,
            "wide_p50": round(wide[len(wide) // 2], 1) if wide else None,
            "wide_p95": round(wide[int(len(wide) * 0.95)], 1) if wide else None}


SEATS = [(own, r) for own in (0, 1, 2) for r in (RISK - SPREAD, RISK, RISK + SPREAD)]
DNF_PRICE = 300.0


def grid(intensity, line, steering, look_mult=1.0):
    """Every seat (3 lanes x 3 risks): mean seconds (a DNF priced at 300 s), finishes, misses a race, p95 width."""
    _, pred, gain, extra = steering
    rs = [race(intensity, line, pred, own, risk, look_mult, gain=gain, extra=extra) for own, risk in SEATS]
    secs = [r["time"] if r["time"] is not None else DNF_PRICE for r in rs]
    return {"secs": round(sum(secs) / len(secs), 1), "best": min(secs), "worst": max(secs),
            "finished": sum(r["time"] is not None for r in rs), "seats": len(rs),
            "misses": round(sum(r["misses"] for r in rs) / len(rs), 1),
            "wide_p95": round(sum(r["wide_p95"] or 0 for r in rs) / len(rs), 1)}


def fleet_grid(hull, intensity, line, gain, nose_rate=NOSE_FOLLOW):
    """Every seat of one hull, round its own risk: mean seconds (DNF 300 s), finishes, misses a race."""
    risk0 = hull_params(hull)[4]
    rs = [race(intensity, line, 0.0, own, min(1, max(0, risk0 + d)), gain=gain, extra=40.0, hull=hull,
               nose_rate=nose_rate) for own in (0, 1, 2) for d in (-SPREAD, 0.0, SPREAD)]
    secs = [r["time"] if r["time"] is not None else DNF_PRICE for r in rs]
    return {"secs": round(sum(secs) / len(secs), 1), "finished": sum(r["time"] is not None for r in rs),
            "misses": round(sum(r["misses"] for r in rs) / len(rs), 1)}


def _fleet_job(a):
    return a, fleet_grid(*a)


def fleet():
    from concurrent.futures import ProcessPoolExecutor
    cases = [(h, ln, g, NOSE_FOLLOW) for h in FLEET_LINES for ln in ("be", "overnight") for g in (0.0, 2.5)]
    cases += [("Manta", ln, 0.0, 9.0) for ln in ("be", "overnight")]
    jobs = [(h, I, ln, g, nr) for h, ln, g, nr in cases for I in (1, 2, 3, 4)]
    with ProcessPoolExecutor() as ex:
        res = dict(ex.map(_fleet_job, jobs))
    print("A MODEL: constant speed (each hull's AI-rival median), no traffic, no rams, 9 seats a cell. "
          "secs (misses a race) [finished of 9 when not all]")
    print(f"{'hull':8} {'line':9} {'steering':16} " + "".join(f"{'I' + str(I):>20}" for I in (1, 2, 3, 4)) + f"{'I1-I4':>9}")
    out = {}
    for h, ln, g, nr in cases:
        row = [res[(h, I, ln, g, nr)] for I in (1, 2, 3, 4)]
        name = (f"lead {g}" if g else "today") + ("" if nr == NOSE_FOLLOW else f", nose {nr:g}/s")
        cells = "".join(f"{c['secs']:>9.1f} ({c['misses']:5.1f})" + (f"[{c['finished']}]" if c['finished'] < 9 else "   ")
                        for c in row)
        print(f"{h:8} {ln:9} {name:16} {cells}{sum(c['secs'] for c in row):>9.1f}")
        out[f"{h}|{ln}|{name}"] = row
    return out


def _job(args):
    I, ln, st, lm = args
    return args, grid(I, ln, st, lm)


def sweep(lines, steerings, looks):
    from concurrent.futures import ProcessPoolExecutor
    jobs = [(I, ln, st, lm) for ln in lines for lm in looks for st in steerings for I in (1, 2, 3, 4)]
    out = {}
    with ProcessPoolExecutor() as ex:
        for (I, ln, st, lm), g in ex.map(_job, jobs):
            out[(ln, lm, st[0], I)] = g
    return out


def table(out, lines, steerings, looks):
    print("A MODEL (constant 324 u/s, no traffic, 9 seats a cell). secs = mean of 9 seats, a DNF priced 300 s; "
          "fin = seats finished; miss = missed mouths a race; p95 = 95th-percentile distance off the line, u")
    for ln in lines:
        for lm in looks:
            print(f"\nline {ln}, look-ahead x{lm}")
            print("steering      " + "".join(f"| I{I}: secs fin miss  p95 " for I in (1, 2, 3, 4)) + "| I1-I4 secs")
            for st in steerings:
                row = [out[(ln, lm, st[0], I)] for I in (1, 2, 3, 4)]
                cells = "".join(f"| {g['secs']:7.1f} {g['finished']}/{g['seats']} {g['misses']:4.1f} {g['wide_p95']:5.1f} "
                                for g in row)
                print(f"{st[0]:13} {cells}| {sum(g['secs'] for g in row):7.1f}")


def self_test():
    bad = []
    if abs(lead_share(2 / 3) - (1 - math.exp(-1.0))) > 1e-9: bad.append("lead_share")
    if lead_share(0) != 0 or lead_share(1, 0) != 0: bad.append("lead_share at 0")
    f, d = [0, 0, 1], [1, 0, 0]
    if abs(ang(f, slerp_beyond(f, d, 2.0, 150)) - 170) > 1e-6: bad.append("slerp_beyond's 170 deg cap")
    if abs(ang(f, slerp_beyond(f, [math.sin(0.2), 0, math.cos(0.2)], 2.5, 40)) - 2.5 * math.degrees(0.2)) > 1e-6:
        bad.append("slerp_beyond's gain")
    if abs(ang(f, slerp_beyond(f, d, 2.5, 40)) - 130) > 1e-6: bad.append("slerp_beyond's extra cap")
    # negative control: judging from the nose (today) the lagged Scarab misses mouths at I2 on today's line...
    today = grid(2, "be", STEERINGS[0])
    if today["misses"] < 5: bad.append(f"the model lost today's misses at I2: {today}")
    # ...the lead takes most of them away and is faster...
    led = grid(2, "be", ("lead 2.5", 0.0, 2.5, 40.0))
    if not (led["misses"] <= 0.25 * today["misses"] and led["secs"] < today["secs"]):
        bad.append(f"the lead no longer fixes I2: today {today}, lead 2.5 {led}")
    # ...judging from the command alone (pred 1 s) does not...
    pred = grid(2, "be", ("pred 1.0", 1.0, 0.0, 0.0))
    if pred["misses"] < 4 * led["misses"] + 5: bad.append(f"pred 1.0 s now rivals the lead at I2: {pred}")
    # ...and the older models' capped command (turned toward the aim, stopped there) hides part of today's overshoot
    capped = [race(2, "be", own=0, capped=True)["misses"], race(2, "be", own=0)["misses"]]
    if not capped[0] < capped[1]: bad.append(f"the capped command no longer misses less than the game's: {capped}")
    print("scarab_nose_lead_sim self-test: " + ("ok" if not bad else "FAILED " + repr(bad)))
    return 0 if not bad else 1


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        sys.exit(self_test())
    if "--fleet" in sys.argv:
        out = fleet()
        if "--json" in sys.argv:
            json.dump(out, open(sys.argv[sys.argv.index("--json") + 1], "w"), indent=1)
        sys.exit(0)
    lines = ["be", "overnight"]
    looks = [1.0, 1.5] if "--look" in sys.argv else [1.0]
    out = sweep(lines, STEERINGS, looks)
    table(out, lines, STEERINGS, looks)
    if "--json" in sys.argv:
        path = sys.argv[sys.argv.index("--json") + 1]
        json.dump({f"{k[0]}|look x{k[1]}|{k[2]}|I{k[3]}": v for k, v in out.items()}, open(path, "w"), indent=1)
