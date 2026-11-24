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

The AI NOSE LEAD (AIPilot.NoseLeadGain, PR #1786; Tools/Build/ai_nose_lead.py is the piece every model shares) is a knob
on race(), nose_lead (None, a gain, a {"gain", "max_extra_deg"} dict or a NoseLead; `--nose-lead G` on the command line
prints each table row again with it on), OFF by default, so every number in this file stands until it is set. With a
gain the pilot judges its error from the COMMAND and steers the command onto the nose turned toward the aim by the gain
times its error (past the aim, at most max_extra_deg past it), so the lagging nose is pulled from beyond the aim instead
of overshooting; the gain is scaled once per race by this model's nose rate (regatta_line_sim.NOSE_FOLLOW, the fleet's
1.5/s: no hull here has a rate of its own) and is 0 with lag=False. It is off while the Dolphin drifts (no stick is
written there: the nose comes round free) and on through an orbit break-off (the stick still runs, on the escape steer).
Measured 2026-11-07 (self_test() holds the first): the Dolphin on its shipped charge drive through a climbing slalom
misses 3 mouths in 65.0 s through the lag alone (20.8 s clean with lag=False); the lead at 2.5 makes that 47.5 s and 1
miss, where judging from the command without the pull (gain 1) gives 130 s and 19. It is not a free win in this model:
on the self-test's 3-ring loop the same Dolphin goes from 14.4 s clean to 22.0 s with a miss, the Dolphin's cruise races
(drifting 90% of the time) barely move, and the 48 deg/s Stoat at 140 u/s is worse on most courses (its 40 deg extra
unwinds at 48 deg/s, most of a second: the gain was set for the Scarab's 132 deg/s), though on the Warpline table's
two-lap tight circuit 2.5 takes it from 112 s and 6 misses to 88 s and 2.

    python3 Tools/Build/switchback_dolphin_sim.py --dir <folder of flight_Switchback_*.json>
    python3 Tools/Build/switchback_dolphin_sim.py --dir <folder> --mode Warpline --nose-lead 2.5
    python3 Tools/Build/switchback_dolphin_sim.py --self-test
"""
import argparse, glob, json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rhino_drive_sim import add, sub, mul, dot, norm, unit, ang, rotate_toward
from scarab_drive_sim import ai_stick
from ai_nose_lead import NoseLead, judge_and_want
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
# The Sparrow's held boost in Breakwater (HoldBoostAIPolicySO, race() models its Tick): the boosted speed is the 135 u/s
# Yash held all race in his 2026-10-26 races. SPARROW_BOOST_BASE is the base policy's bands (the variant's until
# 2026-10-26); SPARROW_BOOST_BREAKWATER mirrors Resources/AIBoostPolicies/SparrowAIBoostPolicy_Breakwater.asset.
SPARROW_BOOST_BASE = {"boost_speed": 135.0, "boost_align": 12, "boost_release_align": 25, "boost_stick": 0.35,
                      "boost_release_stick": 0.6, "boost_engage": 1.5, "boost_release": 0.5,
                      "boost_min_hold": 0.6, "boost_min_rest": 0.4}
SPARROW_BOOST_BREAKWATER = dict(SPARROW_BOOST_BASE, boost_align=45, boost_release_align=60, boost_stick=1.0,
                                boost_release_stick=1.0, boost_engage=2.0, boost_release=1.25)
# The Stoat in Warpline (2026-10-27): Stoat.prefab DefaultThrottleScaler 60, MinimumSpeed 0, Pitch/Yaw 48 deg/s,
# RotationThrottleScaler 0; MinigameWarpline.unity: commit 160, approach lead 180, through 120; two laps of five rings.
# The field pair's warp (StoatDipoleExecutor's path-watching autopilot) is modelled as a held boost to ~140 u/s, the
# AI rivals' median in Yash's races: laid while the ring is further than 1.5 x autopilotLetGoNear at any bearing, let go
# at autopilotLetGoNear. The pair's bend of the path is not modelled, so turns are the hull's alone (pessimistic).
STOAT_WARPLINE = {"cruise": 60.0, "rts": 0.0, "base": 48.0, "drift": False,
                  "commit": 160.0, "lead": 180.0, "through": 120.0}


def stoat_warp(let_go_near, speed=140.0, min_hold=0.8, max_hold=15.0, relay=0.25):
    """StoatDipoleConfig's path-watching hold as a boost (race() models it): lay at 1.5 x let_go_near, let go at it, or
    after max_hold seconds (autopilotMaxHoldSeconds); the next pair relay seconds later (autopilotRelaySeconds)."""
    return {"boost_speed": speed, "boost_align": 180, "boost_release_align": 180, "boost_stick": 1.0,
            "boost_release_stick": 1.0, "boost_engage": 0.0, "boost_release": 0.0,
            "boost_engage_dist": 1.5 * let_go_near, "boost_release_dist": let_go_near,
            "boost_min_hold": min_hold, "boost_max_hold": max_hold, "boost_min_rest": relay}


# The Butterfly in Waystation (2026-10-26): Butterfly.prefab DefaultMinimumSpeed 12 + DefaultThrottleScaler 55 at the AI's
# full throttle (WaystationController.AIThrottleFloor 1) = 67 u/s, Pitch/Yaw 45 deg/s, RotationThrottleScaler 0, no drift;
# MinigameWaystation.unity: commit 260, approach lead 300, through 220. Its fold is not modelled: cruise only is the floor.
BUTTERFLY_WAYSTATION = {"cruise": 67.0, "rts": 0.0, "base": 45.0, "drift": False,
                        "commit": 260.0, "lead": 300.0, "through": 220.0}
# The Stoat in Warpline with NO warp (2026-10-26): Stoat.prefab Pitch/Yaw 48 deg/s, RotationThrottleScaler 0; 140 u/s, the median
# speed of the AI Stoat rivals in Yash's I3 races (its pairs warp it; the warp and the poles' pull are not modelled);
# MinigameWarpline.unity: commit 160, approach lead 180, through 120, two laps of five rings.
STOAT_WARPLINE_NO_WARP = {"cruise": 140.0, "rts": 0.0, "base": 48.0, "drift": False,
                  "commit": 160.0, "lead": 180.0, "through": 120.0}


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
         cfg=None, nose_lead=None):
    """drive: None (no charge economy: cruise only) or a SHIPPED_DRIVE / LEARNED_DRIVE dict (an optional "peak": False
    lets each discharge run out instead of drifting again at its peak, a what-if: the game re-drifts at the peak): the
    charge economy of dolphin_drive_sim.py (a drift banks charge and freezes the speed; letting go discharges toward
    cruise x m x m) with ChargeBoostAIPolicySO's drive (let go on a lined-up straight once the meter is full enough; hold the next commit off
    until the discharge peaks) and AIPilot's commit floor (no commit under 0.9 of the throttle target).
    nose_lead: the AI nose lead (AIPilot.NoseLeadGain, Tools/Build/ai_nose_lead.py): None / 0 is today's steering; a
    gain (2.5 is what #1786 ships Regatta), a {"gain", "max_extra_deg"} dict or an ai_nose_lead.NoseLead switches it on.
    Its gain is scaled ONCE per race by this model's nose rate (L.NOSE_FOLLOW) and is 0 with lag=False."""
    """regatta_fix: the steering AICommitFromApproachPoint turns on, which since 2026-10-26 includes the approach point
    kept inside the commit radius (lead_inside defaults to regatta_fix; pass it to test either half alone)."""
    if lead_inside is None:
        lead_inside = regatta_fix
    cfg = cfg or DOLPHIN_SWITCHBACK
    CRUISE_, COMMIT_, LEAD_, THROUGH_ = cfg["cruise"], cfg["commit"], cfg["lead"], cfg["through"]
    drift = drift and cfg["drift"]
    turn_rate = lambda speed: cfg["rts"] * speed + cfg["base"]
    # the nose lead's gain, ONCE per race from THIS model's own nose rate: L.NOSE_FOLLOW, the fleet's 1.5/s that
    # turn_with_lag lags the nose at (no hull cfg here carries a rate of its own); 0 with lag=False, where the nose IS
    # the command and has nothing to lead (AIPilot.EffectiveNoseLeadGain). Off by default: today's numbers stand.
    nose_lead = NoseLead.from_config(nose_lead)
    lead_gain = nose_lead.effective(L.NOSE_FOLLOW) if lag else 0.0
    gates = course["gates"]; total = len(gates)
    pos = list(course["start"]); fwd = list(course["heading"]); cmd = list(fwd); travel = list(fwd)
    v = CRUISE_; R = v / math.radians(turn_rate(v)); run = max(max(0.0, 2 * R - 18), v * 1.5)
    idx = 0; locked = -1; side = 1.0; lead = LEAD_; t = 0.0
    drifting = False; orbit = L.Orbit(); extending = False; ext_t = 0.0
    misses = 0; breaks = 0; drift_t = 0.0; weave = 0
    meter = 0.0; pinned = 1.0; discharging = False; hold = False; held_at = -1e9; was_dis = False; top = v
    boosting = False; boost_share = 0.0; stick = 0.0
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
        elif cfg.get("boost_speed"):
            # A held boost (HoldBoostAIPolicySO.Driver.Tick, AIBoostPolicySO.IsStraight / StillStraight / HasRunway /
            # TurnAhead): judged by the ring (AIPilot.TickBoostPolicy), its bearing off the course and its runway at the
            # current speed, and the stick the pilot wrote last frame; held at least boost_min_hold s, rested
            # boost_min_rest s.
            to_ring = sub(ring, pos); off = ang(travel, to_ring); runway = norm(to_ring) / max(v, 1.0)
            since = t - held_at
            if boosting:
                if extending or since > cfg.get("boost_max_hold", 1e9) or (since >= cfg.get("boost_min_hold", 0.6) and
                                 (off > cfg.get("boost_release_align", 25) or stick > cfg.get("boost_release_stick", 0.6)
                                  or runway < cfg.get("boost_release", 0.5)
                                  or norm(to_ring) < cfg.get("boost_release_dist", 0.0))):
                    boosting = False; held_at = t
            elif since >= cfg.get("boost_min_rest", 0.4) and not extending and off <= cfg.get("boost_align", 12) \
                    and stick <= cfg.get("boost_stick", 0.35) and runway >= cfg.get("boost_engage", 1.5) \
                    and norm(to_ring) > cfg.get("boost_engage_dist", 0.0):
                boosting = True; held_at = t
            if boosting: boost_share += dt
            tgt = cfg["boost_speed"] if boosting else CRUISE_
            v += (tgt - v) * LERP * dt
            top = max(top, v)
            R = v / math.radians(turn_rate(v)); run = max(max(0.0, 2 * R - 18), v * 1.5)
        if drifting:
            drift_t += dt
            # the nose comes round free, onto the aim (GateRaceController's drift look target)
            fwd = rotate_toward(fwd, desired, math.radians(turn_rate(v)) * dt); cmd = list(fwd)
        else:
            steer = add(fwd, mul(desired, -0.35)) if extending else to
            # the nose lead (AIPilot.TryNoseLead, NoseLeadDirection): with a gain the error is judged from the COMMAND
            # and the command is steered onto the nose turned toward the steer by the gain times its error (past it, at
            # most max_extra_deg past); gain 0 is today's (fwd, steer). On the break-off steer too; never while drifting
            judge, want = judge_and_want(fwd, cmd, steer, lead_gain, nose_lead.max_extra_deg)
            s = stick = ai_stick(ang(judge, want))
            fwd, cmd = L.turn_with_lag(fwd, cmd, want, math.radians(turn_rate(v) * s) * dt, dt, lag)
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
            "drift": round(drift_t / max(t, 1e-6), 2), "top": round(top), "weave": weave,
            "boost": round(boost_share / max(t, 1e-6), 2)}


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
    # the Sparrow's held boost: wide gates on a straight line, so boosting is faster than cruising and threads them all
    sp = {"gates": [{"p": [0, 0, 600.0 * (i + 1)], "a": [0, 0, 1], "r": 40.0} for i in range(5)],
          "start": [0, 0, 0], "heading": [0, 0, 1]}
    cruise_ = race(sp, True, cfg=SPARROW_BREAKWATER, T=300)
    boost_ = race(sp, True, cfg=dict(SPARROW_BREAKWATER, **SPARROW_BOOST_BREAKWATER), T=300)
    if not (boost_["rings"] == 5 and cruise_["time"] and boost_["time"] and boost_["time"] < 0.6 * cruise_["time"]
            and boost_["boost"] > 0.5):
        bad.append(f"Sparrow boost on a straight: cruise {cruise_}, boosting {boost_}")
    # the Stoat warping into narrow rings round a corner: let go late and the 167 u circle at warp speed overshoots
    loop = {"gates": [{"p": [0, 0, 900.0], "a": [0, 0, 1], "r": 30.0}, {"p": [700.0, 0, 1300.0], "a": [1, 0, 0], "r": 30.0},
                      {"p": [1400.0, 0, 700.0], "a": [0, 0, -1], "r": 30.0}], "start": [0, 0, 0], "heading": [0, 0, 1]}
    late = race(loop, True, cfg=dict(STOAT_WARPLINE, **stoat_warp(60)), T=200)
    early = race(loop, True, cfg=dict(STOAT_WARPLINE, **stoat_warp(250)), T=200)
    if not (early["rings"] == 3 and early["misses"] <= late["misses"]):
        bad.append(f"Stoat warp: let go at 60 u {late}, at 250 u {early}")
    # The AI nose lead (ai_nose_lead.py), measured 2026-11-07.
    # (a) Off by default, today's numbers pinned: nose_lead None, NoseLead(), 0 and {"gain": 0} are one steering, and
    # these are the dicts the lead-off runs printed before the knob existed (the 58 break-offs are the orbit path).
    pinned = ((dict(course=loop, regatta_fix=True, T=200),
               {"time": 40.7, "rings": 3, "misses": 0, "breaks": 0, "drift": 0.94, "top": 68, "weave": 0, "boost": 0.0}),
              (dict(course=loop, regatta_fix=True, T=200, drive=SHIPPED_DRIVE),
               {"time": 14.4, "rings": 3, "misses": 0, "breaks": 1, "drift": 0.6, "top": 304, "weave": 0, "boost": 0.0}),
              (dict(course=loop, regatta_fix=False, T=200),
               {"time": None, "rings": 1, "misses": 0, "breaks": 58, "drift": 0.55, "top": 68, "weave": 0, "boost": 0.0}),
              (dict(course=across, regatta_fix=True, T=200),
               {"time": 19.2, "rings": 2, "misses": 2, "breaks": 0, "drift": 0.86, "top": 68, "weave": 0, "boost": 0.0}),
              (dict(course=loop, regatta_fix=True, T=200, cfg=BUTTERFLY_WAYSTATION),
               {"time": 41.5, "rings": 3, "misses": 0, "breaks": 0, "drift": 0.0, "top": 67, "weave": 0, "boost": 0.0}))
    for kw, today in pinned:
        r = race(**kw)
        if r != today: bad.append(f"today's numbers moved: {kw} -> {r}, were {today}")
        for off in (NoseLead(), 0, {"gain": 0}, {"nose_lead": None}):
            if race(nose_lead=off, **kw) != r: bad.append(f"nose_lead={off!r} is not today's steering: {kw}")
    # (b) What the lead buys, with a negative control: the Dolphin on its shipped charge drive through a climbing slalom
    # (eight 25 u rings 400 u apart, swinging 150 u sideways and 80 u up and down) with the Regatta steering misses 3
    # mouths in 65.0 s today through the nose lag alone (lag=False threads it in 20.8 s with none); the lead at 2.5
    # makes that 47.5 s and 1 miss (2.0 and 3.0 within a second of it). The control: judging from the command WITHOUT
    # the pull past the aim (gain 1, which is bit for bit what 2.5 with max_extra_deg 0 collapses to here: both ask for
    # the aim itself) is far worse than both, 130.0 s and 19 misses, so it is the pull that threads the rings.
    slalom3 = {"gates": [{"p": [150.0 * (1 if i % 2 == 0 else -1), 80.0 * (1 if i % 3 == 0 else -1), 400.0 * (i + 1)],
                          "a": [0.0, 0.0, 1.0], "r": 25.0} for i in range(8)], "start": [0, 0, 0], "heading": [0, 0, 1]}
    today_, led_, g1_, x0_ = (race(slalom3, True, drive=SHIPPED_DRIVE, nose_lead=nl)
                              for nl in (None, 2.5, 1.0, NoseLead(2.5, 0.0)))
    if not (today_["time"] and today_["misses"] >= 2):
        bad.append(f"the climbing slalom no longer shows the lag's misses: {today_}")
    if not (led_["time"] and led_["misses"] < today_["misses"] and led_["time"] < today_["time"]):
        bad.append(f"the lead at 2.5 no longer fixes the climbing slalom: today {today_}, lead {led_}")
    if not (g1_["misses"] > led_["misses"] and (g1_["time"] is None or g1_["time"] > led_["time"])):
        bad.append(f"judging from the command without the pull now rivals the lead: gain 1 {g1_}, lead 2.5 {led_}")
    if x0_ != g1_: bad.append(f"2.5 with no extra should be gain 1 here: {x0_} vs {g1_}")
    # (c) A nose with no lag has nothing to lead: with lag=False the lead on or off is one race (every cell of the
    # 2026-11-07 hull x course sweep, 144 of 144, agreed).
    if race(slalom3, True, drive=SHIPPED_DRIVE, lag=False) != race(slalom3, True, drive=SHIPPED_DRIVE, lag=False, nose_lead=2.5):
        bad.append("the lead changed a lag-free race")
    print("switchback_dolphin_sim self-test: " + ("ok" if not bad else "FAILED " + repr(bad)))
    return 0 if not bad else 1


def laps_of(course, path):
    """A lapped circuit (Warpline): the recorded ring set flown again until the race's ring target."""
    target = int(json.load(open(path, encoding="utf-8")).get("raceTarget") or 0)
    gates = course["gates"]
    if gates and target > len(gates):
        course = dict(course, gates=[gates[k % len(gates)] for k in range(target)])
    return course


def _rows(label, nose_lead):
    """A row of main()'s tables as its (label, extra race kwargs): today's steering and, with --nose-lead G, the same
    race again with the AI nose lead on at gain G, labelled, printed right under it."""
    yield label, {}
    if nose_lead is not None:
        yield f"{label}, nose lead {nose_lead:g}", {"nose_lead": nose_lead}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir"); ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--mode", default="Switchback",
                    help="Switchback (the Dolphin), Waystation (the Butterfly, no fold), Breakwater (the Sparrow's boost) "
                         "or Warpline (the Stoat's warp)")
    ap.add_argument("--nose-lead", type=float, default=None, metavar="G",
                    help="also race every row with the AI nose lead on at gain G (AIPilot.NoseLeadGain; 2.5 is what "
                         "#1786 ships Regatta), printed under today's row; off without it")
    a = ap.parse_args()
    if a.self_test:
        sys.exit(self_test())
    G = a.nose_lead
    if a.mode == "Waystation":
        print("(seconds for the race or None, rings threaded, mouths missed, break-offs; the person's time)")
        for p in sorted(glob.glob(os.path.join(a.dir, "flight_Waystation_*.json"))):
            c = course_from_flight(p)
            print(f"{c['file']} (person {c['person']:.0f} s)")
            for name, fix in (("the shipped steering", False), ("Regatta's steering", True)):
                for label, extra in _rows(name, G):
                    print(f"   {label + ': ':22}{race(c, fix, cfg=BUTTERFLY_WAYSTATION, T=300, **extra)}")
        return
    if a.mode == "Warpline":
        print("(seconds for both laps or None, rings threaded, mouths missed, break-offs; the person's time)")
        for p in sorted(glob.glob(os.path.join(a.dir, "flight_Warpline_*.json"))):
            c = laps_of(course_from_flight(p), p)
            print(f"I{c['intensity']} {c['file']} (person {c['person']:.0f} s)")
            rows = [("Regatta steering, 140 u/s, no warp", True, STOAT_WARPLINE_NO_WARP)]
            for name, fix, near in (("shipped steering, warp let go 60 u", False, 60),
                                    ("Regatta steering, warp let go 60 u", True, 60),
                                    ("Regatta steering, warp let go 250 u", True, 250)):
                # the shipped hold (15 s at most), then the Warpline variant's learned 1.0-2.36 s
                hold = {"min_hold": 1.0, "max_hold": 2.357} if near == 250 else {}
                rows.append((name, fix, dict(STOAT_WARPLINE, **stoat_warp(near, **hold))))
            for name, fix, cfg in rows:
                for label, extra in _rows(name, G):
                    r = race(c, fix, cfg=cfg, T=400, **extra)
                    print(f"   {label:36}: {r['time']} s, {r['rings']}, missed {r['misses']}, breaks {r['breaks']}")
        return
    if a.mode == "Breakwater":
        print("(seconds for one lap of stations or None, stations threaded, through the danger weave, share boosting)")
        for p in sorted(glob.glob(os.path.join(a.dir, "flight_Breakwater_*.json"))):
            c = course_from_flight(p)
            for name, boost in (("cruise only", {}), ("the base bands", SPARROW_BOOST_BASE),
                                ("Breakwater's bands", SPARROW_BOOST_BREAKWATER)):
                for label, extra in _rows(name, G):
                    r = race(c, True, cfg=dict(SPARROW_BREAKWATER, **boost), T=300, **extra)
                    print(f"   I{c['intensity']} {c['file']} {label:18}: {r['time']} s, {r['rings']}, weave {r['weave']}, "
                          f"boosting {r.get('boost', 0):.0%}")
        return
    courses = [course_from_flight(p) for p in sorted(glob.glob(os.path.join(a.dir, "flight_Switchback_*.json")))]
    print("(seconds for the 20 gates or None, gates threaded, mouths missed, break-offs, share drifting; the person's time)")
    for c in courses:
        print(f"I{c['intensity']} {c['file']} (person {c['person']:.0f} s)")
        for name, fix, drive in (("Switchback today, the AI's own charge numbers", False, SHIPPED_DRIVE),
                                 ("the fix, the AI's own charge numbers", True, SHIPPED_DRIVE),
                                 ("the fix, the charge numbers learned from you", True, LEARNED_DRIVE)):
            for label, extra in _rows(name, G):
                print(f"   {label + ': ':47}{race(c, fix, drive=drive, **extra)}")


if __name__ == "__main__":
    main()
