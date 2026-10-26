#!/usr/bin/env python3
"""Offline Switchback race for the AI Dolphin, on the REAL courses people flew (each flight records its 20 gates).

Switchback is the Dolphin's training minigame (Docs/AI_SYSTEM/MINIGAME_TRAINING_PLAN.md). People's I2 races there
(2026-10-26) show the AI Dolphins threading 0-2 of 20 rings, circling the first gate the whole race. This model races a
Dolphin through GateRaceController's gate steering on those exact courses, in both of its shapes:

- regatta_fix=False: Switchback today (`aiCommitFromApproachPoint` off): commit at a fixed 260 u, the "through" point
  220 u past the ring ON ITS AXIS on the far side, nothing special after a missed mouth;
- regatta_fix=True: Regatta's steering (2026-10-15/17): commit no earlier than the approach lead plus half the turning
  radius, the through point on the line from the pilot THROUGH THE RING'S CENTRE, and after a missed mouth re-latch the
  side and break off (`AIPilot.BreakOffNow`).

The Dolphin, as the game flies it (Dolphin.prefab, AIPilot): cruise 68 u/s (no boost modelled: real AI Dolphins boosted
5-16% of these races), turn 110 deg/s at any speed (35 u radius), AIPilot's stick law (scarab_drive_sim.ai_stick) and
the AI nose lag (the nose follows the command at 1.5/s, regatta_line_sim.turn_with_lag). Its COMMIT DRIFT is the core of
it: once the aim is within acos(0.9) = 25.8 deg of the course, the course SNAPS onto the aim and the Dolphin travels that
straight line (holdSpeedWhileDrifting) until the aim swings more than 25.8 deg off it. AIPilot's orbit break-off runs
while it is not drifting (the turning-circle test and the 360 deg sweep, regatta_line_sim.Orbit).

No difficulty belief (that is Hard; the races were Medium), no crystal detours, no rivals: the editor is the measurement.

    python3 Tools/Build/switchback_dolphin_sim.py --dir <folder of flight_Switchback_*.json>
    python3 Tools/Build/switchback_dolphin_sim.py --self-test
"""
import argparse, glob, json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rhino_drive_sim import add, sub, mul, dot, norm, unit, ang, rotate_toward
from scarab_drive_sim import ai_stick
import regatta_line_sim as L

CRUISE, TURN = 68.0, 110.0                      # Dolphin.prefab: DefaultThrottleScaler 68, Pitch/Yaw 110, no speed term
COMMIT_DIST, APPROACH_LEAD, THROUGH = 260.0, 300.0, 220.0   # MinigameSwitchback.unity (the GateRaceController defaults)
LEAD_SHARE = 0.4                                # GateRaceController.ApproachLeadShareOfLeg
COMMIT_COS = 0.9
LEAD_INSIDE_SHARE = 0.8                          # lead_inside: the approach point at most this share of the commit radius                                # AIPilot: LookingAtCrystal = dot(desired, course) >= 0.9


# A race's config: the hull (cruise u/s, turn rate = rts x v + base deg/s, whether its AIPilot drifts) and the mode's
# GateRaceController distances. The Dolphin in Switchback is the default; SPARROW_BREAKWATER is the Sparrow's minigame
# (Sparrow.prefab: ThrottleScaler 50 + MinimumSpeed 10 at the AI's 0.6 skill throttle = 40 u/s, the 36 u/s its AI rivals
# raced at; Pitch/Yaw 80, RotationThrottleScaler 0.1; drift 0, BREAKWATER.md "The AI opens its own doors";
# MinigameBreakwater.unity: commit 240, approach lead 280, through 200).
DOLPHIN_SWITCHBACK = {"cruise": CRUISE, "rts": 0.0, "base": TURN, "drift": True,
                      "commit": COMMIT_DIST, "lead": APPROACH_LEAD, "through": THROUGH}
SPARROW_BREAKWATER = {"cruise": 40.0, "rts": 0.1, "base": 80.0, "drift": False,
                      "commit": 240.0, "lead": 280.0, "through": 200.0,
                      "eye": 18.0 - 12.32 / 2}   # BREAKWATER.md: the 18 u eye, less half the 12.32 u hull; the rest is danger weave
# The Butterfly in Waystation (2026-10-26): Butterfly.prefab DefaultMinimumSpeed 12 + DefaultThrottleScaler 55 at the AI's
# full throttle (WaystationController.AIThrottleFloor 1) = 67 u/s, Pitch/Yaw 45 deg/s, RotationThrottleScaler 0, no drift;
# MinigameWaystation.unity: commit 260, approach lead 300, through 220. Its fold is not modelled: cruise only is the floor.
BUTTERFLY_WAYSTATION = {"cruise": 67.0, "rts": 0.0, "base": 45.0, "drift": False,
                        "commit": 260.0, "lead": 300.0, "through": 220.0}


def course_from_flight(path):
    f = json.load(open(path, encoding="utf-8"))
    cols = f["ringColumns"]
    ix = {c: cols.index(c) for c in ("x", "y", "z", "ax", "ay", "az", "radius")}
    gates = [{"p": [r[ix["x"]], r[ix["y"]], r[ix["z"]]], "a": unit([r[ix["ax"]], r[ix["ay"]], r[ix["az"]]]),
              "r": r[ix["radius"]]} for r in f["ringList"]]
    # the start: where the person was when the recording began (every pilot spawns on the same ring)
    c = f["columns"]
    s0 = f["samples"][0]
    start = [s0[c.index("x")], s0[c.index("y")], s0[c.index("z")]]
    heading = unit([s0[c.index("fx")], s0[c.index("fy")], s0[c.index("fz")]])
    return {"gates": gates, "start": start, "heading": heading, "intensity": f.get("intensity"),
            "file": os.path.basename(path), "person": f.get("raceSeconds")}


SHIPPED_DRIVE = {"align": 12.0, "runway": 2.5, "spend": 0.95}        # Resources DolphinAIBoostPolicy (every mode)
LEARNED_DRIVE = {"align": 27.405, "runway": 0.874, "spend": 0.825}    # DolphinAIBoostPolicy_Switchback (2026-10-26)
MAXM, FILL, DRAIN, LERP = 2.259, 1 / 3.636, 0.4, 1.5                   # dolphin_drive_sim.py: DOLPHIN_ENERGY_ECONOMY.md §2
PEAK_MARGIN, MIN_DIS, MIN_GAIN, MAX_HOLD, GRACE, UP_TO_SPEED = 3.0, 0.2, 30.0, 4.0, 0.25, 0.9


def race(course, regatta_fix=False, drift=True, lag=True, T=240.0, dt=1 / 60, trace=None, lead_inside=None, drive=None,
         cfg=None):
    """drive: None (no charge economy: cruise only) or a SHIPPED_DRIVE / LEARNED_DRIVE dict (an optional "peak": False
    lets each discharge run out instead of drifting again at its peak, a what-if: the game re-drifts at the peak): the
    charge economy of dolphin_drive_sim.py (a drift banks charge and freezes the speed; letting go discharges toward
    cruise x m x m) with ChargeBoostAIPolicySO's drive (let go on a lined-up straight once the meter is full enough; hold the next commit off
    until the discharge peaks) and AIPilot's commit floor (no commit under 0.9 of the throttle target)."""
    """regatta_fix: the steering AICommitFromApproachPoint turns on, which since 2026-10-26 includes the approach point
    kept inside the commit radius (lead_inside defaults to regatta_fix; pass it to test either half alone)."""
    if lead_inside is None:
        lead_inside = regatta_fix
    cfg = cfg or DOLPHIN_SWITCHBACK
    CRUISE_, COMMIT_, LEAD_, THROUGH_ = cfg["cruise"], cfg["commit"], cfg["lead"], cfg["through"]
    drift = drift and cfg["drift"]
    turn_rate = lambda speed: cfg["rts"] * speed + cfg["base"]
    gates = course["gates"]; total = len(gates)
    pos = list(course["start"]); fwd = list(course["heading"]); cmd = list(fwd); travel = list(fwd)
    v = CRUISE_; R = v / math.radians(turn_rate(v)); run = max(max(0.0, 2 * R - 18), v * 1.5)
    idx = 0; locked = -1; side = 1.0; lead = LEAD_; t = 0.0
    drifting = False; orbit = L.Orbit(); extending = False; ext_t = 0.0
    misses = 0; breaks = 0; drift_t = 0.0; weave = 0
    meter = 0.0; pinned = 1.0; discharging = False; hold = False; held_at = -1e9; was_dis = False; top = v
    mm = lambda x: 1 + (MAXM - 1) * x
    target_speed = lambda: CRUISE_ * (mm(meter) * pinned if discharging else 1.0)
    while t < T and idx < total:
        g = gates[idx]; ring, ax = g["p"], g["a"]
        if idx != locked:
            locked = idx
            side = 1.0 if dot(sub(pos, ring), ax) >= 0 else -1.0
            leg_from = gates[idx - 1]["p"] if idx > 0 else pos
            # GateRaceController.ApproachLeadFor: with the Regatta steering on, the approach point never lies outside
            # the commit radius (lead_inside forces it on its own, for the comparison)
            lead = min(LEAD_, LEAD_SHARE * norm(sub(leg_from, ring)))
            if lead_inside:
                lead = min(lead, LEAD_INSIDE_SHARE * COMMIT_)
        commit = min(COMMIT_, lead + 0.5 * R) if regatta_fix else COMMIT_
        approaching = norm(sub(ring, pos)) > commit
        if regatta_fix:
            through = add(ring, mul(unit(sub(ring, pos)), THROUGH_))
        else:
            through = sub(ring, mul(ax, side * THROUGH_))
        aim = add(ring, mul(ax, side * lead)) if approaching else through
        to = sub(aim, pos)
        desired = unit(to) if norm(to) > 1.0 else list(travel)
        # AIPilot.UpdateOrbitBreak: never while drifting
        if drifting:
            extending = False
        elif extending:
            ext_t += dt
            if (norm(to) >= run and ext_t >= 0.6) or ext_t >= 6: extending = False
        else:
            closing = dot(desired, fwd) > 0.5
            if (not closing and L.inside_turning_circle(to, fwd, R)) or orbit.tick(to):
                extending = True; ext_t = 0.0; breaks += 1; orbit.reset()
        # the charge drive (ChargeBoostAIPolicySO.Driver.Tick), before the commit loop as AIPilot ticks it
        if drive is not None:
            if discharging and not was_dis and not hold: hold, held_at = True, t
            was_dis = discharging
            if hold:
                since = t - held_at
                if (discharging and drive.get("peak", True) and since >= MIN_DIS and v >= target_speed() - PEAK_MARGIN) or \
                        (not discharging and since > GRACE) or since > MAX_HOLD:
                    hold = False
            elif drifting and not extending:
                to_ring = sub(ring, pos)
                if (ang(travel, to_ring) <= drive["align"] and ang(fwd, to_ring) <= drive["align"]
                        and norm(to_ring) / max(v, 1) >= drive["runway"] and meter >= drive["spend"]
                        and CRUISE_ * mm(meter) ** 2 - v >= MIN_GAIN):
                    drifting = False; hold = True; held_at = t
                    discharging = meter > 0; travel = list(fwd)
        # the commit drift (held off while a discharge climbs; never under 0.9 of the throttle target)
        can_commit = drive is None or (not hold and v >= UP_TO_SPEED * target_speed())
        if drift and not extending:
            if not drifting and can_commit and dot(desired, travel) >= COMMIT_COS:
                drifting = True; travel = list(desired); discharging = False
            elif drifting and dot(desired, travel) < COMMIT_COS:
                drifting = False
                if drive is not None:
                    discharging = meter > 0; travel = list(fwd)
        if drive is not None:
            if drifting:
                meter = min(1.0, meter + FILL * dt); pinned = mm(meter)
            else:
                if discharging:
                    meter = max(0.0, meter - DRAIN * dt)
                    if meter <= 0: discharging = False
                v += (target_speed() - v) * LERP * dt
            top = max(top, v)
            R = v / math.radians(turn_rate(v)); run = max(max(0.0, 2 * R - 18), v * 1.5)
        if drifting:
            drift_t += dt
            # the nose comes round free, onto the aim (GateRaceController's drift look target)
            fwd = rotate_toward(fwd, desired, math.radians(turn_rate(v)) * dt); cmd = list(fwd)
        else:
            steer = add(fwd, mul(desired, -0.35)) if extending else to
            s = ai_stick(ang(fwd, steer))
            fwd, cmd = L.turn_with_lag(fwd, cmd, steer, math.radians(turn_rate(v) * s) * dt, dt, lag)
            travel = list(fwd)
        newpos = add(pos, mul(travel, v * dt))
        a0 = dot(sub(pos, ring), ax); a1 = dot(sub(newpos, ring), ax)
        if a0 * a1 <= 0 and a0 != a1:
            k = a0 / (a0 - a1); hit = add(pos, mul(sub(newpos, pos), k))
            if norm(sub(hit, ring)) <= g["r"]:
                idx += 1
                if cfg.get("eye") and norm(sub(hit, ring)) > cfg["eye"]: weave += 1   # scored, through the danger weave
            else:
                misses += 1
                if regatta_fix:
                    locked = -1; drifting = False; extending = True; ext_t = 0.0   # re-latch, BreakOffNow
        if trace is not None and int(t / dt) % 30 == 0:
            trace.append((round(t, 1), [round(x) for x in pos], round(norm(sub(ring, pos))), approaching, drifting, extending,
                          round(ang(travel, desired))))
        pos = newpos; t += dt
    return {"time": round(t, 1) if idx >= total else None, "rings": idx, "misses": misses, "breaks": breaks,
            "drift": round(drift_t / max(t, 1e-6), 2), "top": round(top), "weave": weave}


def self_test():
    bad = []
    # a straight course of wide gates on the line ahead: both shapes thread all of it
    gates = [{"p": [0, 0, 300.0 * (i + 1)], "a": [0, 0, 1], "r": 40.0} for i in range(5)]
    straight = {"gates": gates, "start": [0, 0, 0], "heading": [0, 0, 1]}
    for fix in (False, True):
        r = race(straight, regatta_fix=fix)
        if r["rings"] != 5: bad.append(f"straight course, regatta_fix={fix}: {r}")
    # a gate whose axis is across the line of approach: the axis through point (today) locks a drift that misses
    across = {"gates": [{"p": [0, 0, 600.0], "a": [1, 0, 0], "r": 28.0}, {"p": [600.0, 0, 600.0], "a": [1, 0, 0], "r": 28.0}],
              "start": [0, 0, 0], "heading": [0, 0, 1]}
    r0, r1 = race(across, False, T=120), race(across, True, T=120)
    if not (r1["rings"] >= r0["rings"] and r1["rings"] == 2):
        bad.append(f"crossed gate: today {r0}, with the Regatta fix {r1}")
    print("switchback_dolphin_sim self-test: " + ("ok" if not bad else "FAILED " + repr(bad)))
    return 0 if not bad else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir"); ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--mode", default="Switchback", help="Switchback (the Dolphin) or Waystation (the Butterfly, no fold)")
    a = ap.parse_args()
    if a.self_test:
        sys.exit(self_test())
    if a.mode == "Waystation":
        print("(seconds for the race or None, rings threaded, mouths missed, break-offs; the person's time)")
        for p in sorted(glob.glob(os.path.join(a.dir, "flight_Waystation_*.json"))):
            c = course_from_flight(p)
            print(f"{c['file']} (person {c['person']:.0f} s)")
            print(f"   the shipped steering: {race(c, False, cfg=BUTTERFLY_WAYSTATION, T=300)}")
            print(f"   Regatta's steering:   {race(c, True, cfg=BUTTERFLY_WAYSTATION, T=300)}")
        return
    courses = [course_from_flight(p) for p in sorted(glob.glob(os.path.join(a.dir, "flight_Switchback_*.json")))]
    print("(seconds for the 20 gates or None, gates threaded, mouths missed, break-offs, share drifting; the person's time)")
    for c in courses:
        print(f"I{c['intensity']} {c['file']} (person {c['person']:.0f} s)")
        print(f"   Switchback today, the AI's own charge numbers: {race(c, False, drive=SHIPPED_DRIVE)}")
        print(f"   the fix, the AI's own charge numbers:          {race(c, True, drive=SHIPPED_DRIVE)}")
        print(f"   the fix, the charge numbers learned from you:  {race(c, True, drive=LEARNED_DRIVE)}")


if __name__ == "__main__":
    main()
