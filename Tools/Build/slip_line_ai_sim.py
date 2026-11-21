#!/usr/bin/env python3
"""Offline Slip Line race for the AI Honeyguide, with the RAILS as walls: the Rivals floor of the Honeyguide's Time card.

Garrett flew Slip Line I1 on 2026-11-13 and answered the fun card "fun almost, rung right, weakest: Rivals". His flight
(bugledger/gradi, flight_SlipLine_Honeyguide_20261113_203852.json) shows why: the Medium AI rival lost about 65 s at the
tenth ring. It crossed that ring's mouth in a jump the race did not count (98 u in one sample), then circled the ring's
FAR-SIDE point in the shipped gate steering (commit at a fixed 135 u, the through point on the ring's axis on the side
latched when the leg began), broke off four times and came back onto the same circle, and spent two charges sliding back
along the next ring's rail toward the ring it was circling. Garrett won by sitting still on the same ring for a minute.

This model flies the AI through SlipLineController's steering in two shapes, on the SHIPPED courses and rails
(`Tools/Build/slip_line_harness/run.sh --export I seed count` writes them from SlipLineCourse.cs itself):

- `shipped`: GateRaceController's gate way as MinigameSlipLine.unity sets it (commit 135, approach lead 150, through
  100, `aiCommitFromApproachPoint` off), plus the rail coach (SlipLineCourse.ChooseRail and RailPursuit, lead 40).
- `line`: what this change ships. The commit, the through point and the missed-mouth break-off of the Regatta steering
  (`AICommitFromApproachPoint => true`, which ten other gate races already turn on), and the RACE LINE between rides:
  a lead point down the straight ring-to-ring line, the one line the course test `The_line_flown_ring_to_ring_never_
  touches_a_rail` holds clear of every rail. A missed mouth is still re-attacked the gate way (the platform asks the race
  line only while the pilot has not just missed this mouth).

The hull, as the game flies an AI Honeyguide (Honeyguide.prefab and the recorded rivals): 37 u/s cruise (the recorded
rival's median), turn 80 + 0.1 v deg/s, AIPilot's stick law and nose lag, the orbit break-off (turning circle and the
360 degree sweep) except while the aim is a lead point. The Ratel: charges at the Medium rate (3.6 a minute, between the
harness's Easy 3.0 and Hard 4.3), colours at random, battery of 3. The slip (HoneyguideCrew): a rail of a held colour
within 5.5 u, the nose inside 45 degrees of it either way, rides to that end at 480 u/s (ramp 0.15 s) and spends the
charge, if AISlipHelps passes (the landing at least min(40, half the distance) nearer the goal). A rail it does not ride
is a wall: touching one (within 2.5 u) kills the speed into it and slows the hull to 40% for a second (the recorded
"slowed" bit: the rival spent 13.6 s slowed). Not modelled: the Flit and the Ratel's cards, crystals, rival contact.

`--miss N` makes the first threading of ring N not count (what the record shows), so the recovery is measured.

    python3 Tools/Build/slip_line_ai_sim.py --courses <folder of I1.jsonl..I4.jsonl> [--seeds 60] [--miss 9]
    python3 Tools/Build/slip_line_ai_sim.py --flight <flight_SlipLine_*.json> --rails <rails.json> [--miss 9]
    python3 Tools/Build/slip_line_ai_sim.py --self-test
"""
import argparse, json, math, os, random, statistics, sys
from multiprocessing import Pool

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rhino_drive_sim import add, sub, mul, dot, norm, unit, ang
from scarab_drive_sim import ai_stick
import regatta_line_sim as L

CRUISE_SHIPPED, TURN_BASE, TURN_RTS = 37.0, 80.0, 0.1
# The Medium AI with SlipLineController's throttle floor (Redline's {0.7, 0.85, 1, 1} by difficulty): Honeyguide.prefab
# MinimumSpeed 10 + ThrottleScaler 50 x 0.85. A person at full throttle cruises at 60.
CRUISE_FLOOR_MEDIUM = 10.0 + 50.0 * 0.85
COMMIT, LEAD, THROUGH, RAIL_LEAD = 135.0, 150.0, 100.0, 40.0     # MinigameSlipLine.unity
LEAD_SHARE_LEG, LEAD_SHARE_COMMIT = 0.4, 0.8                      # GateRaceController.ApproachLeadFor
ZIP, ZIP_RAMP, EXIT, CONTACT, CONE, REACH = 480.0, 0.15, 8.0, 5.5, 45.0, 400.0
RAIL_PAST, KEEP, MIN_SAVE, PLAN_CRUISE = 40.0, 0.7, 1.0, 60.0     # SlipLineCourse (its clock assumes 60 u/s)
WALL, SLOW, SLOW_S = 2.5, 0.4, 1.0
AI_SLIP_MIN_GAIN = 40.0
CHARGE_PER_MIN, CAP = 3.6, 3
RACE_LINE_LEAD = 60.0


def load_courses(path):
    out = []
    for line in open(path, encoding="utf-8"):
        c = json.loads(line)
        rings = [{"p": r[0], "a": unit(r[1]), "r": r[2]} for r in c["rings"]]
        rails = [{"s": r[0], "e": r[1], "c": r[2], "ring": r[3], "chained": bool(r[4])} for r in c["rails"]]
        out.append({"seed": c["seed"], "intensity": c["intensity"], "rings": rings, "rails": prep(rails)})
    return out


def prep(rails):
    for r in rails:
        d = sub(r["e"], r["s"]); r["len"] = norm(d); r["d"] = unit(d)
    return rails


def start_of(rings):
    a = rings[0]["a"]
    return sub(rings[0]["p"], mul(a, 260.0)), list(a)            # SlipLineCourse.StartPoses: 260 u behind ring 0


def seg_point_dist(p, a, b):
    ab = sub(b, a); l2 = dot(ab, ab)
    t = 0.0 if l2 < 1e-9 else max(0.0, min(1.0, dot(sub(p, a), ab) / l2))
    return norm(sub(p, add(a, mul(ab, t)))), t


def ride_seconds(length):
    ramp = 0.5 * ZIP * ZIP_RAMP
    return math.sqrt(2 * length * ZIP_RAMP / ZIP) if length <= ramp else ZIP_RAMP + (length - ramp) / ZIP


def saving(frm, rail, ring, after):
    plain = (norm(sub(ring["p"], frm)) + norm(sub(after, ring["p"]))) / PLAN_CRUISE
    land = add(rail["e"], mul(rail["d"], EXIT))
    return plain - (norm(sub(rail["s"], frm)) / PLAN_CRUISE + ride_seconds(rail["len"]) + norm(sub(after, land)) / PLAN_CRUISE)


def leg_start(rings, k):
    return sub(rings[0]["p"], mul(rings[0]["a"], 260.0)) if k == 0 else rings[k - 1]["p"]


def nth_best_ahead(rings, rails, frm, nth, colour):
    s = sorted((saving(leg_start(rings, r["ring"]), r, rings[r["ring"]],
                       rings[r["ring"] + 1]["p"] if r["ring"] + 1 < len(rings) else rings[r["ring"]]["p"])
                for r in rails if r["c"] == colour and r["ring"] > frm and not r["chained"]), reverse=True)
    return s[nth - 1] if len(s) >= nth else 0.0


def choose_rail(hull, rings, rails, nxt, battery):
    """SlipLineCourse.ChooseRail (the chain's second rail is not credited here: I4 rides read slightly pessimistic)."""
    if not battery: return -1
    full = len(battery) >= CAP
    after = rings[nxt + 1]["p"] if nxt + 1 < len(rings) else rings[nxt]["p"]
    best, best_save = -1, 0.0
    for i, r in enumerate(rails):
        if r["ring"] != nxt or r["chained"] or r["c"] not in battery: continue
        s = saving(hull, r, rings[nxt], after)
        if s < MIN_SAVE: continue
        if not full and s < KEEP * nth_best_ahead(rings, rails, nxt, battery.count(r["c"]), r["c"]): continue
        if s > best_save: best, best_save = i, s
    return best


def rail_pursuit(hull, r):
    along = max(0.0, min(r["len"], dot(sub(hull, r["s"]), r["d"])))
    return add(r["s"], mul(r["d"], min(r["len"], along + RAIL_LEAD)))


def race_line_aim(rings, idx, pos):
    """The ring-to-ring line into ring idx: a lead point down it past the hull's foot, never past the ring. None once the
    hull is past the ring's plane along the line without having threaded it (a mouth missed, or a crossing the race did
    not count): the gate way re-attacks it."""
    a = leg_start(rings, idx); b = rings[idx]["p"]
    d = sub(b, a); L_ = norm(d); u = unit(d)
    foot = dot(sub(pos, a), u)
    if foot > L_: return None
    return add(a, mul(u, min(L_, max(0.0, foot) + RACE_LINE_LEAD))) if foot + RACE_LINE_LEAD <= L_ else \
        add(b, mul(u, foot + RACE_LINE_LEAD - L_))


def race(course, steering="shipped", seed=0, miss=None, T=400.0, dt=1 / 30, trace=None, cruise=None):
    rng = random.Random(seed * 7919 + 1)
    CRUISE = cruise or CRUISE_SHIPPED
    rings, rails = course["rings"], course["rails"]
    total = len(rings)
    pos, fwd = start_of(rings)
    cmd = list(fwd)
    fix = steering != "shipped"
    use_line = steering == "line"
    v = CRUISE; slow_left = 0.0
    idx = 0; locked = -1; side = 1.0; lead = LEAD; t = 0.0
    line_lost = -1
    orbit = L.Orbit(); extending = False; ext_t = 0.0
    battery = []; next_charge = rng.expovariate(CHARGE_PER_MIN / 60.0)
    plan_ring, plan = -1, -1
    rides = hits = misses = breaks = wasted = 0
    ring_start = 0.0; worst_ring = 0.0; worst_at = -1
    missed_once = set()
    ride = None   # (path end, rail dir, speed)
    while t < T and idx < total:
        # the Ratel: a charge in a colour it does not choose
        if t >= next_charge:
            battery.append(rng.randint(1, 3))
            if len(battery) > CAP: battery.pop(0)
            next_charge = t + rng.expovariate(CHARGE_PER_MIN / 60.0)
        if ride is not None:
            end, d, sp = ride
            sp = min(ZIP, sp + ZIP / ZIP_RAMP * dt)
            step = sp * dt; to = sub(end, pos); rem = norm(to)
            newpos = end if rem <= step else add(pos, mul(unit(to), step))
            before_idx = idx
            idx, _ = thread(rings, idx, pos, newpos, miss, missed_once)
            if idx != before_idx:
                if t - ring_start > worst_ring: worst_ring, worst_at = t - ring_start, before_idx
                ring_start = t
            pos = newpos
            if rem <= step:
                ride = None; fwd = list(d); cmd = list(d); v = CRUISE
            else:
                ride = (end, d, sp)
            t += dt
            continue
        g = rings[idx]; ring, ax = g["p"], g["a"]
        if idx != locked:
            locked = idx
            side = 1.0 if dot(sub(pos, ring), ax) >= 0 else -1.0
            lead = min(LEAD, LEAD_SHARE_LEG * norm(sub(leg_start(rings, idx), ring)))
            if fix: lead = min(lead, LEAD_SHARE_COMMIT * COMMIT)
        # the coach: the rail chosen for this leg (once per leg), held while its colour is held and the hull is not past it
        lead_aim = False
        aim = None
        if plan_ring != idx:
            plan_ring, plan = idx, choose_rail(pos, rings, rails, idx, battery)
        if plan >= 0:
            r = rails[plan]
            if r["c"] not in battery or dot(sub(pos, r["s"]), r["d"]) > r["len"] - RAIL_PAST:
                plan = -1
            else:
                aim = rail_pursuit(pos, r); lead_aim = True
        R = v / math.radians(TURN_BASE + TURN_RTS * v)
        if aim is None and use_line and line_lost != idx:
            aim = race_line_aim(rings, idx, pos); lead_aim = aim is not None
        if aim is None:
            commit = min(COMMIT, lead + 0.5 * R) if fix else COMMIT
            approaching = norm(sub(ring, pos)) > commit
            if fix:
                through = add(ring, mul(unit(sub(ring, pos)), THROUGH))
            else:
                through = sub(ring, mul(ax, side * THROUGH))
            aim = add(ring, mul(ax, side * lead)) if approaching else through
        to = sub(aim, pos)
        desired = unit(to) if norm(to) > 1.0 else list(fwd)
        run = max(max(0.0, 2 * R - 18), v * 1.5)
        if lead_aim:
            extending = False; orbit.reset()
        elif extending:
            ext_t += dt
            if (norm(to) >= run and ext_t >= 0.6) or ext_t >= 6: extending = False
        else:
            closing = dot(desired, fwd) > 0.5
            if (not closing and L.inside_turning_circle(to, fwd, R)) or orbit.tick(to):
                extending = True; ext_t = 0.0; breaks += 1; orbit.reset()
        steer = add(fwd, mul(desired, -0.35)) if extending else to
        s = ai_stick(ang(cmd, steer))
        fwd, cmd = L.turn_with_lag(fwd, cmd, unit(steer), math.radians((TURN_BASE + TURN_RTS * v) * s) * dt, dt, True)
        if slow_left > 0: slow_left -= dt
        v = CRUISE * (SLOW if slow_left > 0 else 1.0)
        newpos = add(pos, mul(fwd, v * dt))
        # the slip: a held-colour rail within the contact radius, the nose inside the cone, either way along it
        caught = None
        for i, r in enumerate(rails):
            if abs(r["ring"] - idx) > 1 or r["c"] not in battery: continue
            dd, tt = seg_point_dist(newpos, r["s"], r["e"])
            if dd > CONTACT: continue
            a_fwd = ang(fwd, r["d"]); a_back = ang(fwd, mul(r["d"], -1))
            if min(a_fwd, a_back) > CONE: continue
            forward = a_fwd <= a_back
            here = add(r["s"], mul(r["d"], tt * r["len"]))
            end = r["e"] if forward else r["s"]
            run_len = min(REACH, norm(sub(end, here)))
            if run_len < 1.0: continue
            d = r["d"] if forward else mul(r["d"], -1)
            land = add(here, mul(d, run_len + EXIT))
            goal = coach_goal(rings, rails, plan, idx)
            before, after = norm(sub(newpos, goal)), norm(sub(land, goal))
            if after > before - min(AI_SLIP_MIN_GAIN, 0.5 * before): continue
            caught = (i, land, d, forward)
            break
        if caught is not None:
            i, land, d, forward = caught
            battery.remove(rails[i]["c"])
            rides += 1
            if not forward or rails[i]["ring"] < idx: wasted += 1
            ride = (land, d, v)
            plan = -1 if plan == i else plan
        else:
            # a wall: any rail it does not ride (the one it means to ride counts too until it is caught)
            for r in rails:
                if abs(r["ring"] - idx) > 1: continue
                dd, _ = seg_point_dist(newpos, r["s"], r["e"])
                if dd < WALL:
                    if slow_left <= 0: hits += 1
                    slow_left = SLOW_S
                    # the speed into the wall is lost: slide along it
                    along_d = r["d"] if dot(fwd, r["d"]) >= 0 else mul(r["d"], -1)
                    fwd = unit(add(mul(along_d, 0.9), mul(fwd, 0.1))); cmd = list(fwd)
                    newpos = add(pos, mul(fwd, CRUISE * SLOW * dt))
                    break
        before_idx = idx
        idx, missed_now = thread(rings, idx, pos, newpos, miss, missed_once)
        if missed_now == "outside":
            misses += 1
            if fix:
                locked = -1; line_lost = before_idx; extending = True; ext_t = 0.0
        if idx != before_idx:
            took = t - ring_start
            if took > worst_ring: worst_ring, worst_at = took, before_idx
            ring_start = t
        if trace is not None and int(t / dt) % 15 == 0:
            trace.append((round(t, 1), idx, [round(x) for x in pos], round(norm(sub(ring, pos))), len(battery), extending))
        pos = newpos; t += dt
    took = t - ring_start
    if idx < total and took > worst_ring: worst_ring, worst_at = took, idx
    return {"time": round(t, 1) if idx >= total else None, "rings": idx, "rides": rides, "wasted": wasted,
            "hits": hits, "misses": misses, "breaks": breaks, "worst_ring_s": round(worst_ring, 1), "worst_at": worst_at}


def coach_goal(rings, rails, plan, idx):
    """HoneyguideCrew.AIGoal: the coach's answer while a rail is planned (the ring after the rail's ring), else the ring
    the race steers for (the objective the gate race reports)."""
    if plan >= 0:
        k = rails[plan]["ring"]
        return rings[k + 1]["p"] if k + 1 < len(rings) else add(rings[k]["p"], mul(rings[k]["a"], 200.0))
    return rings[idx]["p"]


def thread(rings, idx, p0, p1, miss, missed_once):
    """RaceGateRing.PlaneCrossing for the ring the pilot needs: inside the mouth threads it (either way), outside is a
    miss. `miss`: that ring's first inside crossing is not counted (the record's uncounted jump)."""
    if idx >= len(rings): return idx, None
    g = rings[idx]
    a0 = dot(sub(p0, g["p"]), g["a"]); a1 = dot(sub(p1, g["p"]), g["a"])
    if a0 * a1 > 0 or a0 == a1: return idx, None
    k = a0 / (a0 - a1); x = add(p0, mul(sub(p1, p0), k))
    if norm(sub(x, g["p"])) <= g["r"]:
        if miss is not None and idx == miss and idx not in missed_once:
            missed_once.add(idx); return idx, "uncounted"
        return idx + 1, "threaded"
    return idx, "outside"


STEERINGS = {"shipped": ("shipped", None), "line": ("line", None), "line+floor": ("line", CRUISE_FLOOR_MEDIUM)}


def _job(args):
    course, name, seed, miss = args
    steering, cruise = STEERINGS[name]
    return course["intensity"], name, race(course, steering, seed, miss, cruise=cruise)


def table(folder, seeds, miss, steerings=("shipped", "line", "line+floor"), procs=None):
    jobs = []
    for I in (1, 2, 3, 4):
        path = os.path.join(folder, f"I{I}.jsonl")
        if not os.path.exists(path): continue
        for c in load_courses(path)[:seeds]:
            for st in steerings:
                jobs.append((c, st, c["seed"], miss))
    with Pool(procs) as pool:
        res = pool.map(_job, jobs, chunksize=4)
    rows = {}
    for I, st, r in res:
        rows.setdefault((I, st), []).append(r)
    print(f"Slip Line, AI Honeyguide (Medium Ratel), {seeds} shipped courses per intensity"
          + (f", ring {miss}'s first threading not counted" if miss is not None else ""))
    print("| I | steering | finished | median time (s) | worst | rides | wasted rides | wall touches | slowest ring (median, worst) |")
    print("|---|---|---|---|---|---|---|---|---|")
    for (I, st), rs in sorted(rows.items()):
        fin = [r["time"] for r in rs if r["time"] is not None]
        med = statistics.median(fin) if fin else float("nan")
        worst = max(fin) if fin else float("nan")
        print(f"| I{I} | {st} | {len(fin)}/{len(rs)} | {med:.1f} | {worst:.1f} | "
              f"{statistics.mean(r['rides'] for r in rs):.1f} | {statistics.mean(r['wasted'] for r in rs):.2f} | "
              f"{statistics.mean(r['hits'] for r in rs):.1f} | "
              f"{statistics.median(r['worst_ring_s'] for r in rs):.1f}, {max(r['worst_ring_s'] for r in rs):.1f} |")
    return rows


def flight_course(flight, rails_path):
    f = json.load(open(flight, encoding="utf-8"))
    rings = [{"p": r[1:4], "a": unit(r[4:7]), "r": r[7]} for r in f["ringList"]]
    rails = [{"s": r[0], "e": r[1], "c": r[2], "ring": r[3], "chained": bool(r[4])}
             for r in json.load(open(rails_path, encoding="utf-8"))["rails"]]
    return {"seed": 0, "intensity": f.get("intensity", 1), "rings": rings, "rails": prep(rails)}


def self_test():
    bad = []
    # a bent course (every shipped leg bends 15-50 degrees off its ring's axis): both steerings thread it
    bent = [{"p": [0, 0, 400.0], "a": unit([0.3, 0, 1]), "r": 72.0}, {"p": [100.0, 0, 800.0], "a": unit([-0.3, 0, 1]), "r": 72.0},
            {"p": [0, 0, 1200.0], "a": unit([0.3, 0, 1]), "r": 72.0}]
    for st in ("shipped", "line"):
        r = race({"seed": 0, "intensity": 1, "rings": bent, "rails": []}, st, T=200)
        if r["rings"] != 3 or not r["time"] or r["time"] > 1300 / CRUISE_SHIPPED + 8: bad.append(f"bent, {st}: {r}")
    # the shipped gate way's degenerate case, kept as a known limit: a ring whose axis IS the leg puts the approach point
    # (150 u out) outside the 135 u commit radius on the line flown, and the model's pilot circles it (the Switchback
    # Dolphin shape, GateRaceController.ApproachLeadShareOfCommit); the race line threads it
    rings = [{"p": [0, 0, 400.0 * (i + 1)], "a": [0, 0, 1], "r": 72.0} for i in range(3)]
    straight = {"seed": 0, "intensity": 1, "rings": rings, "rails": []}
    if race(straight, "line", T=200)["rings"] != 3: bad.append("straight, line: not threaded")
    # a rail of a held colour on the line ahead is ridden, and it saves time (the battery is filled by hand)
    rail = prep([{"s": [0, 0, 420.0], "e": [0, 0, 840.0], "c": 1, "ring": 1, "chained": False}])
    ridden = {"seed": 0, "intensity": 1, "rings": rings, "rails": rail}
    global CHARGE_PER_MIN
    keep = CHARGE_PER_MIN
    CHARGE_PER_MIN = 600.0
    r1 = race(ridden, "line", T=200)
    CHARGE_PER_MIN = keep
    if not (r1["rides"] >= 1 and r1["time"] and r1["time"] < race(straight, "line", T=200)["time"] - 3):
        bad.append(f"a rail on the line is ridden and saves time: {r1}")
    # the recorded failure, negative control: a ring whose first threading is not counted strands the shipped
    # steering far longer than the fixed one (a turn on the course makes the far-side circle)
    turn = [{"p": [0, 0, 400.0], "a": [0, 0, 1], "r": 72.0}, {"p": [300.0, 0, 700.0], "a": unit([1, 0, 1]), "r": 72.0},
            {"p": [700.0, 0, 750.0], "a": [1, 0, 0], "r": 72.0}]
    tc = {"seed": 0, "intensity": 1, "rings": turn, "rails": []}
    s0 = race(tc, "shipped", miss=1, T=300); s1 = race(tc, "line", miss=1, T=300)
    if not (s1["time"] and (s0["time"] is None or s0["worst_ring_s"] > s1["worst_ring_s"] + 5)):
        bad.append(f"uncounted ring: shipped {s0}, line {s1}")
    print("self-test:", "ok" if not bad else "FAILED")
    for b in bad: print("  ", b)
    return 0 if not bad else 1


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--courses"); ap.add_argument("--seeds", type=int, default=60)
    ap.add_argument("--flight"); ap.add_argument("--rails")
    ap.add_argument("--miss", type=int); ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--runs", type=int, default=40, help="--flight: Ratel seeds to fly it with")
    a = ap.parse_args()
    if a.self_test: return self_test()
    if a.courses:
        table(a.courses, a.seeds, a.miss); return 0
    if a.flight and a.rails:
        c = flight_course(a.flight, a.rails)
        for st in STEERINGS:
            steering, cruise = STEERINGS[st]
            rs = [race(c, steering, s, a.miss, cruise=cruise) for s in range(a.runs)]
            fin = [r["time"] for r in rs if r["time"]]
            print(st, f"finished {len(fin)}/{len(rs)}", "median %.1f s" % statistics.median(fin) if fin else "",
                  "slowest ring median %.1f s, worst %.1f s" % (statistics.median(r["worst_ring_s"] for r in rs),
                                                              max(r["worst_ring_s"] for r in rs)),
                  "wall touches %.1f" % statistics.mean(r["hits"] for r in rs))
        return 0
    ap.print_help(); return 2


if __name__ == "__main__":
    sys.exit(main())
