#!/usr/bin/env python3
"""The AI Stoat racing Slingshot on the TRAMPOLINE, ported from the C# (2026-11-06): the model the Slingshot search races.

Why a second file next to stoat_orbit_sim.py (Yash's): that model explored the trampoline before the C# was written, and
three things in it differ from what shipped. This file ports what shipped, reads every number from the assets, and
leaves stoat_orbit_sim.py as it is (its course, geometry and the orbit-era kits are reused from it):

  1. the bounce's kick: VesselTransformer.ApplyVelocityModifiers adds (cos(pi a / T) / 2 + 1) x kick / 1.5 for
     T = slingBoostSeconds, and the shift moves on the hull's FLIGHT clock (position += (speed x course + shift) x
     FlightDeltaTime). stoat_orbit_sim.py moved kick x (1 - a / T) on real time: about a third of the displacement.
     The kick is also capped by StoatSlingMath.WarpCappedKick (warpKickCapCruise x cruise / clock).
  2. the pass: an untouched shell pops when the hull is OUTSIDE it moving away, or its life is out
     (StoatSlingExecutor.StepTrampoline); a hull held in the shell 3 x the soft seconds bounces.
  3. the line: LayTrampoline always carries the hull's turn on (TrampolineCentre with _turnVelocity, eased at 12 /s);
     the --trampoline table that chose the shipped AI rows laid it on the straight line.

Ported line for line: StoatSlingExecutor.AutopilotTrampoline, TrampolineAim, LayTrampoline, StepTrampoline, Bounce,
TrackTurn; StoatSlingMath.TrampolineCentre, TrampolineFlank, TrampolineLineFor, TrampolineStep (with the hard core
TrampolineCoreShare when StoatSlingMath.cs has one: Yash's 30 u shell, Ys-bleeding-edge 2026-11-07), TrampolineTouches,
TryAutopilotSide, SlingBoost, CappedKick, WarpCappedKick, MomentumWarp; StoatPathfinderExecutor's flight clock
(CatchWarpTarget, PressWarpShare, OrbitSpeedBonus, StoatDipoleMath.StepBoost). The gate steering, AIPilot's stick, the
nose lag and the orbit break-off are stoat_orbit_sim.py's (GateRaceController with AICommitFromApproachPoint).

NOT modelled (LIMITS): rivals and their trails, the Orrery's prisms (stoat_orbit_sim.py --orrery counts contacts; a
contact costs nothing here), the hull's roll (its right is level with the world), crystals (the AI never detours for
them), the cell's outer shell (a hull thrown far out flies back at cruise), frame-time jitter. It is a model: the
editor race is the measurement. --flights compares it with the AI Stoats recorded in people's Slingshot races.

    python3 Tools/Build/slingshot_stoat_sim.py --self-test
    python3 Tools/Build/slingshot_stoat_sim.py                 # the shipped AI rows, I1-I4
    python3 Tools/Build/slingshot_stoat_sim.py --flights DIR   # the model on each recorded race's own course
"""
import argparse, glob, json, math, os, re, statistics, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "Compute", "evaluators"))
import stoat_orbit_sim as S
from rhino_drive_sim import add, sub, mul, dot, norm, unit, ang
from scarab_drive_sim import ai_stick
import regatta_line_sim as L

HULL_CONFIG = "Assets/_SO_Assets/VesselActions/Stoat/StoatSlingConfig.asset"
AI_VARIANT = "Assets/Resources/AIBoostPolicies/StoatSlingConfig_Slingshot.asset"
DIPOLE = "Assets/_SO_Assets/VesselActions/Stoat/StoatDipoleConfig.asset"
SO = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/StoatSlingConfigSO.cs"
MATH = "Assets/_Scripts/Controller/Vessel/R_VesselActions/StoatSlingMath.cs"
SCENE = "Assets/_Scenes/Multiplayer Scenes/MinigameSlingshot.unity"
PATHS = [HULL_CONFIG, AI_VARIANT, DIPOLE, SO, MATH, SCENE, "Tools/Build/stoat_orbit_sim.py", "Tools/Build/slingshot_stoat_sim.py"]

# The AI's decisions: StoatSlingConfig's autopilot rows the trampoline reads (through AIModeTuning: the Slingshot copy).
# aiReleaseConeDegrees, aiCatchGiveUpSeconds, aiWarpMinDistance and aiHoldForWindowDegrees are the ORBIT's: the
# trampoline AI lets go at once and never presses for the warp alone (AutopilotTrampoline), so they do nothing here.
AI_FIELDS = ("aiSlingMinTurnDegrees", "aiSlingIntervalSeconds", "aiSlingMinDistance", "trampolineAiTurnScale", "autopilotHold01")
ORBIT_ONLY = ("aiReleaseConeDegrees", "aiCatchGiveUpSeconds", "aiWarpMinDistance", "aiHoldForWindowDegrees")
# The kit: the hull's own config (a person and the AI fly the same shell). Read, never searched.
KIT_FIELDS = ("trampoline", "trampolineAhead", "trampolineRadius", "trampolineLine", "trampolineSoftSeconds", "trampolineSeconds",
              "trampolinePredictDegrees", "slingBoostMin", "slingBoostMax", "slingBoostSeconds", "speedCapShare",
              "warpKickCapCruise", "momentumSeconds", "pressWarpShare", "orbitSpeedBonus", "catchWarpShare", "catchWarpFromPress")
SPAWN_RADIUS = 541.9      # the equatorial spawn ring (every recorded Slingshot race's seats, 2026-10-29..11-07)
LIMITS = ["rivals", "Orrery prisms (contact costs nothing)", "hull roll", "crystals", "the cell's outer shell", "frame jitter"]
TURN_EASE = 12.0          # StoatSlingExecutor.TrackTurn


def _num(text, key):
    m = re.search(r"^\s+" + re.escape(key) + r":\s*(-?[0-9.]+(?:e-?\d+)?)\s*$", text, re.M)
    return float(m.group(1)) if m else None


def _element_value(text, key):
    """An ElementalFloat block's Value (the level-0 number): `key:` then `Value: x` a few lines down."""
    m = re.search(r"^\s+" + re.escape(key) + r":\s*\n(?:\s+\w+:.*\n){0,3}?\s+Value:\s*(-?[0-9.]+)", text, re.M)
    return float(m.group(1)) if m else None


def _so_default(src, key):
    m = re.search(r"\b(?:float|bool|int)\s+" + re.escape(key) + r"\s*=\s*(true|false|-?[0-9.]+)f?\s*;", src)
    if not m: return None
    return {"true": 1.0, "false": 0.0}.get(m.group(1), None) if m.group(1) in ("true", "false") else float(m.group(1))


def read_text(root, rel):
    p = os.path.join(root, rel)
    if not os.path.isfile(p): return ""
    with open(p, encoding="utf-8") as fh:
        return fh.read()


def kit(root=ROOT):
    """Every number the race needs, read from the repo at `root`: the hull's StoatSlingConfig (a field the asset does not
    store takes the SO's initialiser, as Unity loads it), the dipole's warp, the scene's approach, the hard core."""
    hull, so, dip, math_src, scene = (read_text(root, p) for p in (HULL_CONFIG, SO, DIPOLE, MATH, SCENE))
    k = {}
    for f in KIT_FIELDS + AI_FIELDS + ORBIT_ONLY:
        v = _num(hull, f)
        k[f] = v if v is not None else _so_default(so, f)
    k["orbitReach"] = _element_value(hull, "orbitReach") or 1.0
    k["launchPower"] = _element_value(hull, "launchPower") or 1.0
    k["boost"] = _element_value(dip, "boost") or 2.5
    k["boostRise"] = _num(dip, "boostRise") or 20.0
    k["boostFadeSeconds"] = _num(dip, "boostFadeSeconds") or 0.6
    m = re.search(r"TrampolineCoreShare\s*=\s*([0-9.]+)f", math_src)
    k["core"] = float(m.group(1)) if m else 0.0
    for f, d in (("aiCommitDistance", S.COMMIT), ("aiApproachLead", S.LEAD), ("aiThroughDistance", S.THROUGH)):
        k[f] = _num(scene, f) or d
    return k


def ai_rows(root=ROOT, rel=AI_VARIANT):
    """The autopilot rows an AI Stoat reads in Slingshot (AIModeTuning.ForPilot: the mode's copy, else the hull's config;
    a field the copy does not store takes the SO's initialiser)."""
    text, so = read_text(root, rel) or read_text(root, HULL_CONFIG), read_text(root, SO)
    out = {}
    for f in AI_FIELDS + ORBIT_ONLY:
        v = _num(text, f)
        out[f] = v if v is not None else _so_default(so, f)
    return out


# ── StoatSlingMath, line for line ────────────────────────────────────────────────────────────────────────────────

def tramp_line_for(turn_deg, scale):
    return max(0.3, min(0.92, 1.0 - max(0.0, turn_deg) / max(1.0, scale)))


def tramp_centre(pos, fwd, right, bounce_right, ahead, radius, line, turn_axis, turn_rate, speed, predict_deg):
    p, f = list(pos), unit(fwd)
    ahead = max(0.0, ahead)
    turn = min(max(0.0, predict_deg) * math.pi / 180, max(0.0, turn_rate) * ahead / max(1.0, speed))
    if turn > 1e-4 and norm(turn_axis) > 1e-4:
        ax = unit(turn_axis)
        for _ in range(12):
            p = add(p, mul(f, ahead / 12)); f = S.rotate_about(f, ax, turn / 12)
    else:
        p = add(p, mul(f, ahead))
    side = sub(right, mul(f, dot(right, f)))
    side = unit(side) if norm(side) > 1e-4 else right
    return add(p, mul(side, (-1.0 if bounce_right else 1.0) * max(0.0, line) * max(0.0, radius)))


def tramp_flank(pos, centre, right, bounce_right, line, radius):
    to = sub(centre, pos)
    side = sub(right, mul(unit(to), dot(right, unit(to)))) if norm(to) > 1e-4 else right
    side = unit(side) if norm(side) > 1e-4 else right
    return add(centre, mul(side, (1.0 if bounce_right else -1.0) * max(0.0, line) * max(0.0, radius)))


def tramp_step(vel, radial, radius, soft, dt, core):
    d = norm(radial); depth = radius - d
    if depth <= 0 or d < 1e-6: return list(vel)
    n = mul(radial, 1.0 / d)
    vel = add(vel, mul(n, (math.pi / max(0.05, soft)) ** 2 * depth * max(0.0, dt)))
    inward = dot(vel, n)
    if core > 0 and d <= core * radius and inward < 0: vel = sub(vel, mul(n, 2 * inward))
    return vel


def tramp_touches(pos, fwd, centre, radius):
    r = sub(pos, centre)
    return dot(r, r) < radius * radius and dot(fwd, r) < 0


def try_side(to_local_x, to_local_z, objective_range, min_turn, min_dist):
    """TryAutopilotSide on AutopilotGauge's vector (the steering bearing, the objective's range): (presses, left)."""
    left = to_local_x < 0
    if to_local_x * to_local_x + to_local_z * to_local_z < 1e-6 or objective_range < min_dist: return False, left
    return abs(math.degrees(math.atan2(to_local_x, to_local_z))) >= min_turn, left


def momentum_warp(release_clock, timing, age, seconds):
    if seconds <= 0 or age >= seconds or age < 0: return 1.0
    top = 1 + (max(1.0, release_clock) - 1) * max(0.0, min(1.0, timing))
    return 1 + (top - 1) * (1 - age / seconds)


def spawn(seat):
    """A seat on the equatorial spawn ring, facing the centre (the recorded races' first samples)."""
    a = math.radians(90 + 120 * (seat % 3))
    p = [SPAWN_RADIUS * math.cos(a), 0.0, SPAWN_RADIUS * math.sin(a)]
    return p, unit(mul(p, -1))


def race(gates, k, ai, laps=S.LAPS, T=300.0, dt=1 / 60, start=None, trace=None):
    """One AI Stoat's race. k: kit(); ai: the autopilot rows (AI_FIELDS). start: (pos, fwd) or None for seat 0."""
    n = len(gates); total = n * laps
    commit_r, lead_r, through_r = k["aiCommitDistance"], k["aiApproachLead"], k["aiThroughDistance"]
    cruise = S.CRUISE
    radius = k["trampolineRadius"] * max(1.0, k["orbitReach"])
    soft, life, ahead, predict = k["trampolineSoftSeconds"], k["trampolineSeconds"], k["trampolineAhead"], k["trampolinePredictDegrees"]
    catch_top = 1 + (max(1.0, k["boost"]) - 1) * k["catchWarpShare"]
    pos, fwd = start if start else spawn(0)
    pos, fwd = list(pos), unit(fwd); cmd = list(fwd)
    clock = 1.0; R = cruise / math.radians(S.TURN); run = max(max(0.0, 2 * R - 18), cruise * 1.5)
    idx = 0; locked = -1; side = 1.0; lead = lead_r; t = 0.0
    extending = False; ext_t = 0.0; breaks = 0; misses = 0; orbit = L.Orbit()
    tramp = touching = False; centre = None; bounce_right = False; laid_at = 0.0; touch_s = 0.0; bvel = None
    omega = [0.0, 0.0, 0.0]; last_fwd = list(fwd)
    mom_clock, mom_at = 1.0, -1e9
    mods = []          # [amount vector, age, duration, ceiling]
    last_press = -1e9; presses = bounces = passes = 0; dist = 0.0; far = 0.0; warped_t = 0.0
    while t < T and idx < total:
        g = gates[idx % n]; ring, ax = g["p"], g["a"]
        if idx != locked:
            locked = idx
            side = 1.0 if dot(sub(pos, ring), ax) >= 0 else -1.0
            leg_from = gates[(idx - 1) % n]["p"] if idx > 0 else pos
            lead = min(lead_r, S.LEAD_SHARE * norm(sub(leg_from, ring)), S.LEAD_INSIDE_SHARE * commit_r)
        commit = min(commit_r, lead + 0.5 * R)
        approaching = norm(sub(ring, pos)) > commit
        through = add(ring, mul(unit(sub(ring, pos)), through_r))
        aim = add(ring, mul(ax, side * lead)) if approaching else through
        to = sub(aim, pos)
        desired = unit(to) if norm(to) > 1.0 else list(fwd)
        if extending:   # AIPilot.UpdateOrbitBreak
            ext_t += dt
            if (norm(to) >= run and ext_t >= 0.6) or ext_t >= 6: extending = False
        elif (dot(desired, fwd) <= 0.5 and L.inside_turning_circle(to, fwd, R)) or orbit.tick(to):
            extending = True; ext_t = 0.0; breaks += 1; orbit.reset()
        up = [0.0, 1.0, 0.0]
        right = unit(S.cross(up, fwd)) if abs(dot(up, fwd)) < 0.999 else [1.0, 0.0, 0.0]
        speed = cruise * clock     # VesselStatus.Speed: the engine's speed on the flight clock
        # StoatSlingExecutor.AutopilotTrampoline
        steer_at = None
        if tramp and not touching:
            # TrampolineAim: the flank for the turn from the nose to where the gate steering wants to go
            line = tramp_line_for(ang(fwd, to), ai["trampolineAiTurnScale"])
            steer_at = tramp_flank(pos, centre, right, bounce_right, line, radius)
        elif not tramp and ai["aiSlingMinTurnDegrees"] > 0 and t - last_press >= ai["aiSlingIntervalSeconds"]:
            lx, lz = dot(to, right), dot(to, fwd)
            rng = norm(sub(ring, pos))
            press, left = try_side(lx, lz, rng, ai["aiSlingMinTurnDegrees"], ai["aiSlingMinDistance"])
            # the warp press (k["warp_press"]: AutopilotTrampoline reads aiWarpMinDistance, as AutopilotSling does;
            # StoatSlingMath.WantsWarp on AutopilotGauge's vector, whose length is the objective's range)
            if not press and k.get("warp_press") and ai.get("aiWarpMinDistance", 0) > 0 and rng >= ai["aiWarpMinDistance"]:
                press = True
            if press:
                last_press = t; presses += 1
                bounce_right = not left
                # LayTrampoline
                centre = tramp_centre(pos, fwd, right, bounce_right, ahead, radius, k["trampolineLine"],
                                      omega, norm(omega), max(speed, 1.0), predict)
                tramp = True; touching = False; laid_at = t
        # StoatPathfinderExecutor: the flight clock
        held = catch_top * (1 + k["orbitSpeedBonus"]) if touching else \
            (1 + (catch_top - 1) * k["pressWarpShare"]) if tramp and k["catchWarpFromPress"] else 1.0
        target = max(held, momentum_warp(mom_clock, 1.0, t - mom_at, k["momentumSeconds"]))
        warped = target > 1.001
        if warped:
            clock += (max(1.0, target) - clock) * (1 - math.exp(-max(0.1, k["boostRise"]) * dt)); warped_t += dt
        else:
            clock = 1 + (clock - 1) * math.exp(-dt / max(0.01, k["boostFadeSeconds"]))
        speed = cruise * clock
        dtf = dt * clock
        # StoatSlingExecutor.StepTrampoline
        if tramp:
            radial = sub(pos, centre); d = norm(radial); outward = dot(fwd, radial) > 0
            if not touching and tramp_touches(pos, fwd, centre, radius):
                touching = True; touch_s = 0.0; bvel = mul(fwd, max(speed, 1.0))
            if touching:
                if d >= radius or touch_s > 3 * soft:
                    # Bounce: the squeeze's boost x Mass's power, under the release-speed and warp caps
                    engine = max(speed / max(1.0, clock), 1.0)
                    kick = engine * (k["slingBoostMin"] + (k["slingBoostMax"] - k["slingBoostMin"]) * max(0.0, min(1.0, ai["autopilotHold01"])))
                    kick *= max(1.0, k["launchPower"])
                    kick = max(0.0, min(kick, (k["speedCapShare"] - 1) * speed))
                    kick = max(0.0, min(kick, k["warpKickCapCruise"] * engine / max(1.0, clock)))
                    mom_clock, mom_at = max(1.0, clock), t
                    if kick > 0: mods.append([mul(fwd, kick / 1.5), 0.0, k["slingBoostSeconds"], kick * 1.05])
                    bounces += 1; tramp = touching = False
                else:
                    bvel = tramp_step(bvel, radial, radius, soft, dt, k["core"])
                    heading = unit(bvel) if norm(bvel) > 1e-2 else fwd
                    turned = ang(fwd, heading); axis = S.cross(fwd, heading)
                    if turned > 1e-4 and norm(axis) > 1e-8: cmd = S.rotate_about(cmd, unit(axis), math.radians(turned))
                    fwd = heading; touch_s += dt
            elif (outward and d > radius) or t - laid_at >= life:
                tramp = False; passes += 1
        # the stick (AIPilot on the flight clock); the borrowed aim while a shell is up and untouched
        steer = sub(steer_at, pos) if steer_at is not None else (add(fwd, mul(desired, -0.35)) if extending else to)
        s = ai_stick(ang(fwd, steer))
        fwd, cmd = L.turn_with_lag(fwd, cmd, steer, math.radians(S.TURN * s) * dtf, dtf, True)
        # VesselTransformer.ApplyVelocityModifiers: the kicks, on real time, shifting on the flight clock
        shift = [0.0, 0.0, 0.0]; ceiling = 100.0
        for m in mods: m[1] += dt
        mods = [m for m in mods if m[1] < m[2]]
        for m in mods:
            shift = add(shift, mul(m[0], math.cos(m[1] * math.pi / m[2]) / 2 + 1)); ceiling = max(ceiling, m[3])
        if norm(shift) > ceiling: shift = mul(unit(shift), ceiling)
        newpos = add(pos, add(mul(fwd, speed * dt), mul(shift, dtf)))
        a0 = dot(sub(pos, ring), ax); a1 = dot(sub(newpos, ring), ax)
        if a0 * a1 <= 0 and a0 != a1:
            kk = a0 / (a0 - a1); hit = add(pos, mul(sub(newpos, pos), kk))
            if norm(sub(hit, ring)) <= g["r"]:
                idx += 1
            else:
                misses += 1; locked = -1; extending = True; ext_t = 0.0
        dist += norm(sub(newpos, pos)); far = max(far, norm(newpos))
        # TrackTurn: the turn rate, eased
        omega = add(omega, mul(sub(mul(S.cross(last_fwd, fwd), 1 / dt), omega), 1 - math.exp(-TURN_EASE * dt)))
        last_fwd = list(fwd)
        pos = newpos; t += dt
        if trace is not None and int(round(t / dt)) % 6 == 0:
            trace.append((round(t, 2), idx, [round(x, 1) for x in pos], round(speed, 1)))
    return {"time": round(t, 2) if idx >= total else None, "rings": idx, "of": total, "misses": misses, "breaks": breaks,
            "presses": presses, "bounces": bounces, "passes": passes, "speed": round(dist / max(t, 1e-6), 1),
            "warped": round(warped_t / max(t, 1e-6), 3), "far": round(far)}


def course(intensity, seed):
    return S.course(intensity, seed)


def score(k, ai, intensities=(1, 2, 3, 4), seeds=4, seedbase=0, dt=1 / 60, T=300.0):
    out = {}
    for i in intensities:
        rs = [race(course(i, 20260000 + 97 * (seedbase + s) + i), k, ai, dt=dt, T=T, start=spawn(s)) for s in range(seeds)]
        out[i] = rs
    return out


def summary(rs):
    fin = [r["time"] for r in rs if r["time"]]
    return {"finished": f"{len(fin)}/{len(rs)}", "median_s": round(statistics.median(fin), 1) if fin else None,
            "misses": round(statistics.mean(r["misses"] for r in rs), 1), "rings": round(statistics.mean(r["rings"] for r in rs), 1),
            "bounces": round(statistics.mean(r["bounces"] for r in rs), 1), "presses": round(statistics.mean(r["presses"] for r in rs), 1),
            "speed": round(statistics.mean(r["speed"] for r in rs)), "far": max(r["far"] for r in rs)}


# ── the recorded races ───────────────────────────────────────────────────────────────────────────────────────────

def read_flight(path):
    with open(path, encoding="utf-8") as fh:
        d = json.load(fh)
    if d.get("mode") != "Slingshot": return None
    gates = [{"p": [float(r[1]), float(r[2]), float(r[3])], "a": unit([float(r[4]), float(r[5]), float(r[6])]), "r": float(r[7])}
             for r in d.get("ringList") or []]
    t = [s[0] for s in d["samples"]]
    rivals = []
    for kk in range(len(d.get("rivalHulls") or [])):
        rows = [r[4 * kk:4 * kk + 4] for r in d["rivals"]]
        fin = next((t[i] for i, r in enumerate(rows) if r[3] >= 16), None)
        p0 = rows[0][:3]
        later = next((r[:3] for r in rows if norm(sub(r[:3], p0)) > 20), None)
        heading = unit(sub(later, p0)) if later else unit(mul(p0, -1))
        rivals.append({"finish": fin, "rings": int(rows[-1][3]), "start": (p0, heading)})
    return {"date": d.get("date"), "intensity": int(d.get("intensity") or 0), "gates": gates, "rivals": rivals,
            "end": t[-1] if t else 0.0, "path": os.path.basename(path)}


# Which kit each recorded race flew, by its date (the flights carry the editor's local clock, the commits' +05:30).
ERAS = [("2026-11-04 02:13", "2026-11-07 02:42", "60 u shell, turn >= 35, x100 (8abe1fe8)",
         {"trampolineRadius": 60.0, "core": 0.0}, {"aiSlingMinTurnDegrees": 35.0, "trampolineAiTurnScale": 100.0}),
        ("2026-11-07 02:42", "2099-01-01 00:00", "30 u shell + core, turn >= 50, x55 (c459f442)",
         {"trampolineRadius": 30.0, "core": 0.2}, {"aiSlingMinTurnDegrees": 50.0, "trampolineAiTurnScale": 55.0})]


def era_of(date):
    for lo, hi, name, kk, aa in ERAS:
        if lo <= (date or "") < hi: return name, kk, aa
    return None


def flights(folder, k, ai, dt=1 / 60):
    rows = []
    for p in sorted(glob.glob(os.path.join(folder, "*.json"))):
        f = read_flight(p)
        if not f or not f["gates"]: continue
        era = era_of(f["date"])
        if not era: continue
        name, kk, aa = era
        kit_e, ai_e = dict(k, **kk), dict(ai, **aa)
        for r in f["rivals"]:
            m = race(f["gates"], kit_e, ai_e, dt=dt, T=max(300.0, f["end"] + 30), start=r["start"])
            rows.append((f, name, r, m))
    return rows


def self_test():
    bad = []
    k, ai = kit(), ai_rows()
    if not k["trampoline"]: bad.append("the hull's config does not fly the trampoline: this model ports only that kit")
    for f in KIT_FIELDS + AI_FIELDS:
        if k.get(f) is None and ai.get(f) is None: bad.append(f"{f} not found in the asset or the SO")
    # TrampolineLineFor: clamps, and the shipped scale's line for a 0 deg turn is the 0.92 cap
    if tramp_line_for(0, 100) != 0.92 or tramp_line_for(500, 100) != 0.3: bad.append("TrampolineLineFor clamps")
    # the hard core: a fast hull straight in comes straight back, at any speed (StoatSlingTests' FlyThrough)
    for v in (190.0, 300.0):
        vel, p = [0.0, 0.0, -v], [0.0, 0.0, 30.0]
        for _ in range(240):
            vel = tramp_step(vel, p, 30.0, 0.35, 1 / 120, 0.2)
            p = add(p, mul(vel, 1 / 120))
            if norm(p) > 30.0 and dot(vel, p) > 0: break
        if vel[2] <= 0: bad.append(f"the hard core did not send a {v:.0f} u/s hull back")
    # the kick's profile: kick / 1.5 x (cos(pi a / T) / 2 + 1) over T = 1.5 s moves kick x 1 s (on a clock of 1)
    T, kick, acc, a = 1.5, 54.0, 0.0, 0.0
    while a < T:
        a += 1 / 600; acc += kick / 1.5 * (math.cos(a * math.pi / T) / 2 + 1) / 600 if a < T else 0
    if abs(acc - kick) > 0.5: bad.append(f"the kick's displacement over 1.5 s is {acc:.1f} u, not {kick:.0f}")
    # races: the shipped AI rows finish a wide straight course; a Stoat that never presses cruises it at 60 u/s and is
    # slower (the negative control); pressing for every turn of 0 deg is a different race
    wide = [{"p": [0.0, 0.0, 400.0 * (j + 1)], "a": [0.0, 0.0, 1.0], "r": 60.0} for j in range(8)]
    st = ([0.0, 0.0, 0.0], [0.0, 0.0, 1.0])
    never = race(wide, k, dict(ai, aiSlingMinTurnDegrees=0.0), laps=1, start=st)
    if not never["time"] or abs(never["speed"] - S.CRUISE) > 2 or never["presses"]: bad.append(f"a Stoat that never presses: {never}")
    c = course(2, 20260000 + 2)
    shipped = race(c, k, ai, start=spawn(0))
    floor = race(c, k, dict(ai, aiSlingMinTurnDegrees=0.0), start=spawn(0))
    if shipped["presses"] == 0: bad.append(f"the shipped rows never pressed on an I2 course: {shipped}")
    if floor["presses"] != 0: bad.append("aiSlingMinTurnDegrees 0 should never press")
    again = race(c, k, ai, start=spawn(0))
    if again != shipped: bad.append("the same race twice differs (not deterministic)")
    print("slingshot_stoat_sim self-test:", ("FAIL " + "; ".join(bad)) if bad else "ok",
          f"(I2 seed 2: shipped {shipped['time']} s {shipped['bounces']} bounces, never-press {floor['time']} s)")
    return 1 if bad else 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--root", default=ROOT, help="read the kit and the AI rows from this checkout")
    ap.add_argument("--seeds", type=int, default=6)
    ap.add_argument("--flights", help="a folder of recorded flight_Slingshot_*.json")
    ap.add_argument("--dt", type=float, default=1 / 60)
    a = ap.parse_args()
    if a.self_test: sys.exit(self_test())
    k, ai = kit(a.root), ai_rows(a.root)
    if a.flights:
        rows = flights(a.flights, k, ai, dt=a.dt)
        for f, name, r, m in rows:
            real = f"{r['finish']:.1f} s" if r["finish"] else f"DNF {r['rings']}/16 at {f['end']:.0f} s"
            model = f"{m['time']:.1f} s" if m["time"] else f"DNF {m['rings']}/16"
            print(f"{f['date']} I{f['intensity']} {name:<44} real {real:<20} model {model:<12} misses {m['misses']:2d} "
                  f"bounces {m['bounces']:2d} far {m['far']:5d} u")
        return
    print("kit:", {f: k[f] for f in ("trampolineRadius", "core", "trampolineAhead", "trampolineSoftSeconds", "boost")})
    print("AI rows:", {f: ai[f] for f in AI_FIELDS})
    for i, rs in score(k, ai, seeds=a.seeds, dt=a.dt).items():
        print(f"I{i}", summary(rs))


if __name__ == "__main__":
    main()
