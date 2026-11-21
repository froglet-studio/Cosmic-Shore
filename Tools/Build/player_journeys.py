#!/usr/bin/env python3
"""PLAYER JOURNEYS: simulate the personas of Docs/LoopAtlas/model/players.yaml through the atlas's loops, day by day
and minute by minute, and measure where each one activates, where a loop does not fit their time, and who comes back
(Garrett, 2026-11-21: "I want to know what their activation paths look like ... animate their path through their
respective gameplay loops").

A day: the persona comes back with a chance set by their session culture and by how much yesterday paid them. A
sitting: its length comes from the culture; inside it the persona keeps choosing a loop from their stage's weights,
times how well the loop's drivers match theirs, times whether the loop fits the minutes left. A round that does not
fit is counted as a MISFIT (the loop the player wanted and could not start), a round cut short by the end of the
sitting as CUT. A voyage counts as finished when one sitting holds 20 minutes of voyage play, unless voyages are
resumable (the what-if `resumable`), when the minutes carry over. A stage ends on its `exit` rule.

Every rate is a hypothesis from players.yaml, never a measurement: the output says how the personas would move if
the hypotheses hold, and which loops their session patterns cannot reach. Calibrate against analytics first.

    python3 Tools/Build/player_journeys.py            # writes Docs/LoopAtlas/journeys-out/{journeys.json,JOURNEYS.md}
    python3 Tools/Build/player_journeys.py --check    # exit 1 if the outputs are stale
    python3 Tools/Build/player_journeys.py --self-test
"""
from __future__ import annotations

import argparse
import json
import random
import statistics
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import loop_atlas  # noqa: E402

REPO = Path(__file__).resolve().parents[2]
OUT = REPO / "Docs" / "LoopAtlas" / "journeys-out"
DAYS, RUNS, SEED = 28, 300, 7
VOYAGE_S = 1200          # a voyage's shortest run (voyages t[0])
STAY = 1.35              # a player tends to play the loop they are in again
DECAY = 0.95             # interest kept from one day played to the next (novelty fades)
CHURN_GAP = 7            # this many days away in a row and the player has left


def visit_span(n: dict) -> tuple[float, float]:
    """How long one stop in a loop lasts inside a sitting."""
    if n["tier"] <= 4:
        return tuple(n["t"])
    if n["id"] == "arc.activation":
        return (60, 240)  # one Lesson
    return (20, 120)      # a hub or a menu: choosing, building a deck, reading the squad book


def driver_match(p: dict, n: dict) -> float:
    ds = n.get("drivers") or []
    return sum(p["drivers"].get(d, 0.0) for d in ds) / len(ds) if ds else 0.3


def simulate(model: dict, persona: dict, rng: random.Random, resumable=False, trace=False) -> dict:
    nodes = {n["id"]: n for n in model["nodes"]}
    cultures = {c["id"]: c for c in model["players"]["cultures"]}
    stages = {s["id"]: s for s in persona["stages"]}
    stage = persona["stages"][0]
    st = {"sessions": 0, "days": 0, "since": 0.0}
    reached = {stage["id"]: {"day": 0, "minutes": 0.0}}
    played_days, minutes, misfit, cut, rounds = [], 0.0, {}, {}, {}
    by_loop = {}
    voy = {"progress": 0.0, "done": 0, "lost": 0}
    sat_prev, away, events, sessions_log = 1.0, 0, [], []
    interest = 1.0  # novelty fades a little every day played; a new stage, a finished voyage and a good day restore it
    for day in range(DAYS):
        weekend = day % 7 >= 5
        cul = cultures[persona.get("weekend_culture", persona["culture"]) if weekend else persona["culture"]]
        if day > 0:
            social = 0.10 * persona["drivers"].get("crew", 0) if "arc.social" in stage["loops"] or \
                stage["id"] in ("crewed", "crew", "squad") else 0.0
            p = min(0.97, max(0.02, cul["returns"] * interest + social))
            if rng.random() > p:
                away += 1
                if away >= CHURN_GAP:
                    break
                continue
        away = 0
        played_days.append(day)
        pat = cul["weekend" if weekend else "weekday"]
        n_sessions = rng.randint(*pat["sessions"]) or (1 if day == 0 else 0)
        day_sat, day_len = 0.0, 0.0
        for si in range(n_sessions):
            length = rng.uniform(*pat["minutes"]) * 60
            start = day * 86400 + (8 + si * (14 / max(1, n_sessions)) + rng.random()) * 3600
            left, cur, sat = length, None, 0.0
            first = not events
            while left > 15:
                weights = stage.get("weekend") if weekend and stage.get("weekend") else stage["loops"]
                if first and persona["entry"] in nodes:
                    pick, first = persona["entry"], False
                else:
                    opts = []
                    for lid, w in weights.items():
                        n = nodes[lid]
                        lo, _ = visit_span(n)
                        if lo > left:
                            misfit[lid] = misfit.get(lid, 0) + 1
                            continue
                        opts.append((lid, w * (0.5 + driver_match(persona, n)) * (STAY if lid == cur else 1.0)))
                    if not opts:
                        break
                    r, acc, pick = rng.random() * sum(w for _, w in opts), 0.0, opts[-1][0]
                    for lid, w in opts:
                        acc += w
                        if r <= acc:
                            pick = lid
                            break
                n = nodes[pick]
                lo, hi = visit_span(n)
                want = rng.uniform(lo, hi)
                dur = min(want, left)
                full = dur >= lo
                if not full:
                    cut[pick] = cut.get(pick, 0) + 1
                if full and n["tier"] <= 4:
                    rounds[pick] = rounds.get(pick, 0) + 1
                sat += dur * driver_match(persona, n) * (1.0 if full else 0.3)
                if pick.startswith("voy.") and n["tier"] <= 4:
                    voy["progress"] += dur
                    if voy["progress"] >= VOYAGE_S:
                        voy["done"] += 1
                        interest += 0.03
                        sat += VOYAGE_S * 0.25 * driver_match(persona, n)  # a voyage's end pays: the haul, the saga
                        voy["progress"] = 0.0
                by_loop[pick] = by_loop.get(pick, 0.0) + dur
                if trace:
                    events.append([round(start + (length - left)), pick, round(dur), stage["id"]])
                else:
                    events.append(1)
                left -= dur
                cur = pick
            played = length - max(0.0, left)
            if voy["progress"] > 0 and not resumable:
                sat -= 0.5 * voy["progress"] * driver_match(persona, nodes["voyages"])  # the run was lost
            minutes += played / 60
            day_sat += sat
            day_len += played
            if trace:
                sessions_log.append([round(start), round(start + played)])
            if voy["progress"] > 0 and not resumable:
                interest -= 0.04
                voy["lost"] += 1
                voy["progress"] = 0.0
            st["sessions"] += 1
            before = stage["id"]
            stage, st = advance(persona, stages, stage, st, rng, day, minutes, reached, on="session")
            if stage["id"] != before:
                interest += 0.25
        st["days"] += 1
        before = stage["id"]
        stage, st = advance(persona, stages, stage, st, rng, day, minutes, reached, on="day")
        if stage["id"] != before:
            interest += 0.25
        sat_prev = (day_sat / day_len) / 0.6 if day_len else 0.5
        interest = min(1.2, max(0.0, interest * DECAY + 0.10 * (min(1.5, sat_prev) - 0.8)))
        if interest < 0.15:
            break
    return {"played_days": played_days, "minutes": minutes, "reached": reached, "misfit": misfit, "cut": cut,
            "rounds": rounds, "by_loop": by_loop, "voyages": voy, "events": events if trace else None,
            "sessions": sessions_log if trace else None, "last_day": played_days[-1] if played_days else 0}


def advance(persona, stages, stage, st, rng, day, minutes, reached, on):
    ex = stage.get("exit")
    if not ex:
        return stage, st
    go = False
    if on == "session" and "after_sessions" in ex and st["sessions"] >= ex["after_sessions"]:
        go = True
    if on == "day":
        if "after_days" in ex and st["days"] >= ex["after_days"] and "chance_per_day" not in ex:
            go = True
        if "chance_per_day" in ex and st["days"] >= ex.get("after_days", 0) and rng.random() < ex["chance_per_day"]:
            go = True
    if go:
        nxt = stages[ex["to"]]
        reached.setdefault(nxt["id"], {"day": day, "minutes": round(minutes, 1)})
        return nxt, {"sessions": 0, "days": 0}
    return stage, st


def summarise(model, persona, runs, resumable):
    n = len(runs)
    ret = {k: round(sum(any(x >= d for x in r["played_days"]) for r in runs) / n, 3)
           for k, d in (("D1", 1), ("D7", 7), ("D14", 14), ("W4", 21))}
    stages = []
    for s in persona["stages"]:
        hit = [r["reached"][s["id"]] for r in runs if s["id"] in r["reached"]]
        stages.append({"id": s["id"], "name": s["name"], "reach": round(len(hit) / n, 3),
                       "median_day": statistics.median(h["day"] for h in hit) if hit else None,
                       "median_minutes": round(statistics.median(h["minutes"] for h in hit)) if hit else None,
                       "milestone": s.get("milestone")})
    agg = lambda key: {k: round(sum(r[key].get(k, 0) for r in runs) / n, 2)
                       for k in sorted({k for r in runs for k in r[key]})}
    by_loop = agg("by_loop")
    total = sum(by_loop.values()) or 1
    return {"retention": ret, "stages": stages,
            "minutes_per_week": round(statistics.mean(r["minutes"] for r in runs) / (DAYS / 7), 1),
            "share": {k: round(v / total, 3) for k, v in sorted(by_loop.items(), key=lambda kv: -kv[1])},
            "misfit": dict(sorted(agg("misfit").items(), key=lambda kv: -kv[1])[:6]),
            "cut": dict(sorted(agg("cut").items(), key=lambda kv: -kv[1])[:6]),
            "voyages": {"finished": round(sum(r["voyages"]["done"] for r in runs) / n, 2),
                        "abandoned": round(sum(r["voyages"]["lost"] for r in runs) / n, 2)},
            "resumable": resumable}


def economics(model):
    """For each culture and each sitting-sized loop: the share of that culture's sittings long enough to start it."""
    nodes = {n["id"]: n for n in model["nodes"]}
    rng = random.Random(SEED)
    rows = []
    loops = [l for l in ("round.time", "round.charge", "round.ms", "round.regatta", "round.broadside", "voy.cell",
                         "ladder", "arena", "voyages", "toybox") if l in nodes]
    for c in model["players"]["cultures"]:
        samples = [rng.uniform(*c["weekday"]["minutes"]) * 60 for _ in range(500)]
        samples += [rng.uniform(*c["weekend"]["minutes"]) * 60 for _ in range(200)]
        fit = {l: round(sum(s >= nodes[l]["t"][0] for s in samples) / len(samples), 2) for l in loops}
        rows.append({"culture": c["id"], "name": c["name"], "comfort": c["comfort"], "fit": fit})
    return {"loops": loops, "names": {l: nodes[l]["name"] for l in loops},
            "t": {l: nodes[l]["t"] for l in loops}, "rows": rows}


def build(model) -> dict:
    out = {"date": "2026-11-21", "days": DAYS, "runs": RUNS, "personas": [], "economics": economics(model),
           "loops": [{"id": n["id"], "name": n["name"], "tier": n["tier"], "t": n["t"], "parent": n.get("parent"),
                      "drivers": n.get("drivers", [])} for n in model["nodes"]],
           "cultures": model["players"]["cultures"]}
    for p in model["players"]["personas"]:
        base = [simulate(model, p, random.Random(f"{SEED}|{p['id']}|{i}")) for i in range(RUNS)]
        what = [simulate(model, p, random.Random(f"{SEED}|{p['id']}|{i}"), resumable=True) for i in range(RUNS)]
        # the representative player: the run whose minutes are the median, traced in full for the animation
        # the representative player: of the runs that reach the deepest stage at least half the runs reach, the one
        # whose minutes played are the median; traced in full for the animation
        ids = [s["id"] for s in p["stages"]]
        deep = max(k for k, sid in enumerate(ids) if sum(sid in r["reached"] for r in base) >= RUNS / 2)
        pool = [i for i in range(RUNS) if ids[deep] in base[i]["reached"]]
        order = sorted(pool, key=lambda i: base[i]["minutes"])
        mid = order[len(order) // 2]
        tr = simulate(model, p, random.Random(f"{SEED}|{p['id']}|{mid}"), trace=True)
        out["personas"].append({
            "id": p["id"], "name": p["name"], "who": p["who"], "color": p["color"], "culture": p["culture"],
            "weekend_culture": p.get("weekend_culture"), "challenge": p["challenge"],
            "stages": [{"id": s["id"], "name": s["name"], "note": s.get("note"), "milestone": s.get("milestone"),
                        "why": (s.get("exit") or {}).get("why")} for s in p["stages"]],
            "now": summarise(model, p, base, False), "resumable": summarise(model, p, what, True),
            "trace": {"of": f"the median player of the {len(pool) / RUNS:.0%} who reach '{p['stages'][deep]['name']}'",
                      "events": tr["events"], "sessions": tr["sessions"], "reached": tr["reached"],
                      "minutes": round(tr["minutes"])}})
    return out


def report_md(j) -> str:
    o = ["# Player journeys", "",
         f"Generated by `python3 Tools/Build/player_journeys.py` from `model/players.yaml` and the loops: {j['runs']} "
         f"simulated players per persona over {j['days']} days. Every rate is a hypothesis in players.yaml (Proposed "
         "layer), not a measurement; calibrate with analytics before trusting a number. The animated view is the "
         "Player Journeys artifact.", ""]
    for p in j["personas"]:
        n, r = p["now"], p["resumable"]
        o += [f"## {p['name']}", "", p["who"], "",
              f"Still playing on or after (D1, D7, D14, week four): " + ", ".join(f"{k} {v:.0%}" for k, v in n["retention"].items())
              + f". About {n['minutes_per_week']} minutes a week.", "",
              "| Stage | Reached | Median day | Median minutes played | Milestone |", "| --- | --- | --- | --- | --- |"]
        for s in n["stages"]:
            o.append(f"| {s['name']} | {s['reach']:.0%} | {s['median_day'] if s['median_day'] is not None else '-'} | "
                     f"{s['median_minutes'] if s['median_minutes'] is not None else '-'} | {s['milestone'] or ''} |")
        top = ", ".join(f"{k} {v:.0%}" for k, v in list(n["share"].items())[:5])
        o += ["", f"Where the time goes: {top}.",
              f"Loops wanted but too long for the time left (per player, {j['days']} days): "
              + (", ".join(f"{k} {v}" for k, v in n["misfit"].items()) or "none") + ".",
              f"Voyages finished {n['voyages']['finished']}, abandoned mid-voyage {n['voyages']['abandoned']}; "
              f"with resumable voyages: finished {r['voyages']['finished']}, still playing in week four "
              f"{n['retention']['W4']:.0%} -> {r['retention']['W4']:.0%}.", "",
              "**Challenge.** " + p["challenge"], ""]
    e = j["economics"]
    o += ["## Session cultures against our loop timescales", "",
          "The share of each culture's sittings long enough to start a loop (its shortest length).", "",
          "| Culture | " + " | ".join(f"{e['names'][l]} ({int(e['t'][l][0] // 60) or e['t'][l][0]}{'m' if e['t'][l][0] >= 60 else 's'})"
                                     for l in e["loops"]) + " |",
          "| --- |" + " --- |" * len(e["loops"])]
    for row in e["rows"]:
        o.append(f"| {row['name']} | " + " | ".join(f"{row['fit'][l]:.0%}" for l in e["loops"]) + " |")
    return "\n".join(o) + "\n"


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args(argv)
    model = loop_atlas.assemble()
    if a.self_test:
        return self_test(model)
    j = build(model)
    text, md = json.dumps(j, ensure_ascii=False, separators=(",", ":")) + "\n", report_md(j)
    if a.check:
        stale = [p.name for p, t in ((OUT / "journeys.json", text), (OUT / "JOURNEYS.md", md))
                 if not p.exists() or p.read_text(encoding="utf-8") != t]
        print("player_journeys:", "stale " + ", ".join(stale) if stale else "up to date")
        return 1 if stale else 0
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "journeys.json").write_text(text, encoding="utf-8")
    (OUT / "JOURNEYS.md").write_text(md, encoding="utf-8")
    print(f"wrote {OUT.relative_to(REPO)}/journeys.json and JOURNEYS.md")
    for p in j["personas"]:
        n = p["now"]
        print(f"{p['name']:28} D7 {n['retention']['D7']:.0%} W4 {n['retention']['W4']:.0%}  "
              f"{n['minutes_per_week']:6} min/wk  stages " + " ".join(f"{s['reach']:.0%}" for s in n["stages"]))
    return 0


def self_test(model) -> int:
    fails = []
    p = model["players"]["personas"][0]
    a = simulate(model, p, random.Random(1))
    b = simulate(model, p, random.Random(1))
    if a["minutes"] != b["minutes"]:
        fails.append("the simulation is not repeatable from a seed")
    # negative control: a culture whose sittings are 1 minute can never start a 20 minute voyage
    m = json.loads(json.dumps(model))
    m["players"]["cultures"] = [dict(c, weekday={"sessions": [1, 1], "minutes": [1, 1]},
                                     weekend={"sessions": [1, 1], "minutes": [1, 1]}) for c in m["players"]["cultures"]]
    v = next(x for x in m["players"]["personas"] if x["id"] == "voyager")
    r = simulate(m, v, random.Random(2))
    if r["voyages"]["done"] != 0:
        fails.append("a one-minute sitting finished a voyage")
    if not r["misfit"]:
        fails.append("one-minute sittings reported no misfits")
    # resumable voyages can only help a voyage finish
    base = sum(simulate(model, v, random.Random(i))["voyages"]["done"] for i in range(30))
    res = sum(simulate(model, v, random.Random(i), resumable=True)["voyages"]["done"] for i in range(30))
    if res < base:
        fails.append("resumable voyages finished fewer voyages")
    print("player_journeys self-test:", "ok" if not fails else "FAILED")
    for f in fails:
        print("  -", f)
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
