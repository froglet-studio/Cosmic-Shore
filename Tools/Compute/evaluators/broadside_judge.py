#!/usr/bin/env python3
"""The Broadside JUDGE: each hull's points share in mixed brawls, scored with the real game's code (Plan step: charge-arena-balance).

Broadside (the Charge arena game, GameModes.Broadside) is a brawl, so a hull's balance number is not a time but how much
of a mixed match's points it lands. Every seat is flown by that hull's SHIPPED AI (Legendary) in Amoebius, headless and
faster than real time, through the same flow as the Regatta judge (Port/src/CosmicShore.Player/RaceJudge.cs,
`"mode": "Broadside"`): boot, launch on a fixed frame, every seat placed on a team, press Ready, and read each seat's
RoundStats.CombatPoints. Nothing is modelled: the Boneyard, the hit latch, the verb prices and every pilot are the game's.

    judge(hulls, matches=None, per_hull=15, start="rest", limit_s=180, seed=1) -> RESULT

    The match plan: `per_hull` appearances for every hull, dealt into 4v4 lineups (8 seats, Jade against Ruby; the idle
    host is parked outside the arena so nobody farms it), shuffled with `seed` so each hull meets every other one and
    plays on both teams. One process per match, at most REGATTA_JUDGE_WORKERS (default 4) at once.

    A match is TIMED: the point target is raised out of reach (points_per_pilot 100000) and the match runs `limit_s` of
    game time, so every seat's points are a rate over the same time. A hull's SHARE in a match is its points over the
    match's mean points per seat (1.0 is a fair share); its balance number is the mean share over its matches.

    start  "rest" (default; every hull at level 0, Garrett's "don't cheat": no per-hull rows), "card" (the card's
           StartingElements), "L5" (Time at least 5), or a table {"Manta": {"Time": 5}} in levels.

Command line:
    python3 Tools/Compute/evaluators/broadside_judge.py --hulls Sparrow,Urchin,... [--per-hull 15] [--out r.json]
    python3 Tools/Compute/evaluators/broadside_judge.py --report r.json        the table of an earlier run
    python3 Tools/Compute/evaluators/broadside_judge.py --self-test

Setup: as regatta_judge.py (.NET 10; the player is built into REGATTA_JUDGE_BIN on first use).
"""
import argparse
import concurrent.futures
import json
import math
import os
import random
import sys
import tempfile
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import regatta_judge as rj  # noqa: E402  (build, the player runner, the commit)

# Every hull with a Charge arcade game (Docs/ElementalLoop/STATUS.md section 3, 2026-11-19): all but the Starling.
CHARGE_HULLS = ["Angler", "BrittleStar", "Dolphin", "Goby", "Grizzly", "Honeyguide", "Jacks", "JewelWasp", "Manta",
                "Meerkat", "Orca", "Rhino", "Scarab", "Serpent", "Shrike", "Sparrow", "Squirrel", "Stoat", "Swordfish",
                "Thresher", "TigerMoth", "Urchin", "Wrasse"]
# The hulls Broadside's card seats today (ArcadeGameBroadside.asset Vessels).
CARD_HULLS = ["Manta", "Dolphin", "Swordfish", "Urchin", "Squirrel", "Sparrow", "Scarab"]
SEATS = 8          # 4v4: the card's MaxPlayersAllowed is 9, and the ninth is the parked host
TEAMS = ("Jade", "Ruby")


def plan(hulls, per_hull=15, seed=1):
    """Lineups of SEATS distinct hulls, each hull in about `per_hull` of them, half its matches on each team where the
    count allows. A hull set smaller than SEATS repeats nobody: the lineup is then the whole set."""
    rng = random.Random(seed)
    hulls = list(hulls)
    size = min(SEATS, len(hulls) - len(hulls) % 2)
    n = max(1, math.ceil(len(hulls) * per_hull / size))
    count = {h: 0 for h in hulls}
    side = {h: 0 for h in hulls}
    out = []
    for _ in range(n):
        # The least-played first (random tie-break): every hull reaches per_hull before anyone gets a second helping.
        order = sorted(hulls, key=lambda h: (count[h], rng.random()))
        pick = order[:size]
        rng.shuffle(pick)
        # Teams: the hulls that have played more on Jade go to Ruby, and vice versa.
        pick.sort(key=lambda h: (side[h], rng.random()))
        lineup = [{"hull": h, "domain": TEAMS[0] if i < size // 2 else TEAMS[1]} for i, h in enumerate(pick)]
        for s in lineup:
            count[s["hull"]] += 1
            side[s["hull"]] += 1 if s["domain"] == TEAMS[0] else -1
        out.append(lineup)
    return out


def judge(hulls, matches=None, per_hull=15, start="rest", limit_s=180, seed=1, overrides=None, intensity=2,
          workers=None, workdir=None):
    rj.build()
    t0 = time.time()
    matches = matches or plan(hulls, per_hull, seed)
    workdir = workdir or tempfile.mkdtemp(prefix="broadside-judge-")
    jobs = []
    for i, lineup in enumerate(matches):
        spec = {"mode": "Broadside", "seats": lineup, "seeds": [seed * 1000 + i], "limit_s": limit_s,
                "intensity": intensity, "points_per_pilot": 100000}
        if start is not None:
            spec["start"] = start
        if overrides:
            spec["overrides"] = overrides
        jobs.append((f"m{i:03d}", spec))
    timeout_s = 240 + limit_s
    workers = workers or int(os.environ.get("REGATTA_JUDGE_WORKERS", 0)) or 4
    with concurrent.futures.ThreadPoolExecutor(workers) as ex:
        results = list(ex.map(lambda j: rj._run_one(j[1], timeout_s, workdir, j[0]), jobs))
    out, notes = [], []
    for (tag, spec), res in zip(jobs, results):
        notes.extend(res.get("notes", []))
        for race in res.get("races", []):
            asked = sorted(s["hull"] for s in spec["seats"])
            flown = sorted(st["hull"] for st in race.get("seats", []))
            race["match"] = tag
            race["asked"] = spec["seats"]
            if flown != asked:
                race["outcome"] = f"seat-mismatch: asked {asked}, flew {flown}"
            out.append(race)
    return {"races": out, "wall_s": round(time.time() - t0, 1), "engine_commit": rj._commit(), "backend": "amoebius",
            "start": start, "limit_s": limit_s, "notes": sorted(set(notes)), "workdir": workdir}


def shares(result):
    """{hull: {"share": mean share, "sd", "n", "pts": mean points, "hits": {verb: mean}}} over the valid matches.
    A match counts when it ran its full time and every seat flew the hull it asked for."""
    acc = {}
    for race in result["races"]:
        if race.get("outcome") != "timeout" or not race.get("seats"):
            continue
        pts = [s["points"] for s in race["seats"]]
        mean = sum(pts) / len(pts)
        if mean <= 0:
            continue
        for s in race["seats"]:
            a = acc.setdefault(s["hull"], {"share": [], "pts": [], "hits": {}})
            a["share"].append(s["points"] / mean)
            a["pts"].append(s["points"])
            for verb in ("bullet", "strike", "debuff", "missile"):
                a["hits"].setdefault(verb, []).append(s.get(f"{verb}_hits", 0))
    rows = {}
    for h, a in acc.items():
        n = len(a["share"])
        m = sum(a["share"]) / n
        sd = (sum((x - m) ** 2 for x in a["share"]) / (n - 1)) ** 0.5 if n > 1 else 0.0
        rows[h] = {"share": round(m, 3), "se": round(sd / n ** 0.5, 3), "n": n,
                   "pts": round(sum(a["pts"]) / n, 1),
                   "hits": {v: round(sum(x) / n, 2) for v, x in a["hits"].items()}}
    return rows


def balanced_subset(rows, band=0.10, min_size=2):
    """The largest set of hulls whose shares, RE-NORMALISED to the set's own mean, all sit within +-band. A share is
    measured against the whole field, so this is the first cut; the subset is then re-judged on its own."""
    hs = sorted((h for h in rows if rows[h]["share"] > 0), key=lambda h: rows[h]["share"])
    best = []
    for i in range(len(hs)):
        for j in range(len(hs), i, -1):
            sub = hs[i:j]
            m = sum(rows[h]["share"] for h in sub) / len(sub)
            if all(abs(rows[h]["share"] / m - 1) <= band for h in sub):
                if len(sub) > len(best):
                    best = sub
                break
    return best if len(best) >= min_size else []


def report(result):
    rows = shares(result)
    valid = sum(1 for r in result["races"] if r.get("outcome") == "timeout")
    lines = [f"{valid}/{len(result['races'])} valid matches, start {result.get('start')}, {result.get('limit_s')} s each, "
             f"engine {result.get('engine_commit')}, {result.get('wall_s')} s wall",
             f"{'hull':12} {'share':>6} {'se':>6} {'n':>3} {'pts':>7}  hits/match (bullet strike debuff missile)"]
    for h, r in sorted(rows.items(), key=lambda kv: -kv[1]["share"]):
        hi = r["hits"]
        lines.append(f"{h:12} {r['share']:6.2f} {r['se']:6.3f} {r['n']:3d} {r['pts']:7.1f}  "
                     f"{hi['bullet']:6.1f} {hi['strike']:6.1f} {hi['debuff']:6.1f} {hi['missile']:6.1f}")
    sub = balanced_subset(rows)
    lines.append(f"largest subset within +-10% of its own mean (first cut): {len(sub)}: {', '.join(sub)}")
    return "\n".join(lines)


def self_test():
    # The plan: every hull plays per_hull times, on both teams, 4v4.
    hulls = CHARGE_HULLS
    p = plan(hulls, per_hull=15, seed=1)
    count = {h: sum(1 for m in p for s in m if s["hull"] == h) for h in hulls}
    assert min(count.values()) >= 15 and max(count.values()) - min(count.values()) <= 1, count
    for m in p:
        assert len(m) == SEATS and len({s["hull"] for s in m}) == SEATS
        assert sum(1 for s in m if s["domain"] == "Jade") == SEATS // 2
    jade = {h: sum(1 for m in p for s in m if s["hull"] == h and s["domain"] == "Jade") for h in hulls}
    assert all(abs(2 * jade[h] - count[h]) <= 2 for h in hulls), jade
    # The subset: shares 1.0, 1.05, 0.95 and 2.0 -> the three; nothing within the band -> empty (negative control).
    rows = {h: {"share": s} for h, s in {"A": 1.0, "B": 1.05, "C": 0.95, "D": 2.0}.items()}
    assert sorted(balanced_subset(rows)) == ["A", "B", "C"], balanced_subset(rows)
    assert balanced_subset({"A": {"share": 1.0}, "B": {"share": 2.0}}) == []
    # One real match: four card hulls, 60 s, every seat on its asked team, and someone scores.
    res = judge(["Sparrow", "Scarab", "Urchin", "Squirrel"], matches=[[
        {"hull": "Sparrow", "domain": "Jade"}, {"hull": "Scarab", "domain": "Ruby"},
        {"hull": "Urchin", "domain": "Jade"}, {"hull": "Squirrel", "domain": "Ruby"}]], limit_s=60)
    race = res["races"][0]
    assert race["outcome"] == "timeout", race["outcome"]
    assert {(s["hull"], s["domain"]) for s in race["seats"]} == {("Sparrow", "Jade"), ("Scarab", "Ruby"),
                                                                 ("Urchin", "Jade"), ("Squirrel", "Ruby")}, race["seats"]
    assert sum(s["points"] for s in race["seats"]) > 0, race["seats"]
    print("self-test ok: the plan deals every hull 15+ times on both teams, the subset finder has a negative control, "
          "and a real 60 s match seats every hull on its team and scores")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--hulls", default=",".join(CHARGE_HULLS))
    ap.add_argument("--per-hull", type=int, default=15)
    ap.add_argument("--limit", type=float, default=180)
    ap.add_argument("--start", default="rest")
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--overrides", help="JSON file: [{asset, fields}] in-memory asset edits for this run")
    ap.add_argument("--out")
    ap.add_argument("--report")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        self_test()
        return
    if a.report:
        print(report(json.load(open(a.report))))
        return
    start = json.loads(a.start) if a.start.startswith("{") else a.start
    overrides = json.load(open(a.overrides)) if a.overrides else None
    res = judge([h for h in a.hulls.split(",") if h], per_hull=a.per_hull, start=start, limit_s=a.limit, seed=a.seed,
                overrides=overrides)
    if a.out:
        with open(a.out, "w") as f:
            json.dump(res, f, indent=1)
    print(report(res))


if __name__ == "__main__":
    main()
