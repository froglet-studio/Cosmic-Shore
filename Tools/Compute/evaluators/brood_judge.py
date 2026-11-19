#!/usr/bin/env python3
"""The Brood Rush JUDGE: balance the Mass-Space arena game with the real game's code (Plan step: massspace-arena).

Every seat is flown by that hull's SHIPPED AI (Legendary) in Amoebius, headless, in teams of at most three
(Port/src/CosmicShore.Player/BroodJudge.cs, `--race-judge` with "mode": "BroodRush"). Nothing is modelled: the cell,
its nucleus claim, the fauna waves, every pilot and the volume books are the game's own C#.

The measure is NET CONTROL OF SPACE (Docs/ElementalLoop/PLAYBOOK.md, Garrett 2026-11-18): each pilot's scoreboard
total, VC (volume it created in its own domain) + VD (volume it took off the opposing domains), from the match's own
VolumeLedger. With two teams the difference of team totals is the difference of volume left, so a pilot's VC + VD is
its share of its team's net control. A hull's BALANCE INDEX is the mean over its matches of its VC + VD divided by the
mean VC + VD of every pilot in that match: 1.00 is the field, and the bar is about 0.90-1.10.

Matches are dealt in ROUNDS: each round shuffles the hulls (round seed) into 3v3 matches of six distinct hulls (arena
seating: each hull once a match), so every hull meets many teammates and rivals. One game process plays one roster for
`seeds_per_roster` seeds (one boot, Play Again between seeds); at most 4 processes a box (OOM otherwise).

    python3 Tools/Compute/evaluators/brood_judge.py --hulls Rhino,Manta,... --rounds 15 --out fleet.json
    python3 Tools/Compute/evaluators/brood_judge.py --report fleet.json [--subset]
    python3 Tools/Compute/evaluators/brood_judge.py --teams Rhino,Manta,Dolphin/Serpent,Sparrow,Squirrel --seeds 1,2

Setup as regatta_judge.py (.NET 10; the player is built into REGATTA_JUDGE_BIN from this checkout).
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

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import regatta_judge as rj  # noqa: E402  (build, _run_one, _commit)

# Every hull with a Mass or Space kit (Docs/ElementalLoop/STATUS.md, 2026-11-19), plus the Squirrel, which the card
# seats. The Starling has no kit yet.
FLEET = ["Angler", "BrittleStar", "Dolphin", "Goby", "Grizzly", "Honeyguide", "Jacks", "JewelWasp", "Manta", "Meerkat",
         "Orca", "Rhino", "Scarab", "Serpent", "Shrike", "Sparrow", "Squirrel", "Stoat", "Swordfish", "Thresher",
         "TigerMoth", "Urchin", "Wrasse"]
CARD = ["Squirrel", "Manta", "Dolphin", "Rhino", "Swordfish", "Serpent", "Sparrow"]
BAR = 0.10


def deal(hulls, rounds, team_size=3, seed=0):
    """Rosters of two teams of `team_size`, every hull once a round (a short last match is filled from the round's
    start, never with a hull already in it)."""
    per = 2 * team_size
    out = []
    for r in range(rounds):
        rng = random.Random(seed * 1000 + r)
        order = list(hulls)
        rng.shuffle(order)
        rem = len(order) % per
        if rem:
            last = order[-rem:]
            order += [h for h in order if h not in last][:per - rem]
        for i in range(0, len(order), per):
            m = order[i:i + per]
            out.append([m[:team_size], m[team_size:]])
    return out


def judge(rosters, seeds_per_roster=1, seed0=1, limit_s=300, workers=None, workdir=None, intensity=2, overrides=None,
          start=None, ai_objective=None):
    rj.build()
    t0 = time.time()
    workdir = workdir or tempfile.mkdtemp(prefix="brood-judge-")
    jobs = []
    for i, teams in enumerate(rosters):
        seeds = [seed0 + i * seeds_per_roster + k for k in range(seeds_per_roster)]
        spec = {"mode": "BroodRush", "teams": teams, "seeds": seeds, "limit_s": limit_s, "intensity": intensity}
        if overrides:
            spec["overrides"] = overrides
        if start is not None:
            spec["start"] = start
        if ai_objective:
            spec["ai_objective"] = ai_objective      # "nucleus": the AI steers for the nucleus (experiment, BroodJudge.cs)
        jobs.append((f"m{i:03d}", spec))
    timeout_s = 150 + seeds_per_roster * (limit_s + 120)
    workers = workers or int(os.environ.get("REGATTA_JUDGE_WORKERS", 0)) or min(4, len(jobs))
    with concurrent.futures.ThreadPoolExecutor(workers) as ex:
        results = list(ex.map(lambda j: rj._run_one(j[1], timeout_s, workdir, j[0]), jobs))
    matches, notes = [], []
    for (tag, spec), res in zip(jobs, results):
        notes.extend(res.get("notes", []))
        asked = sorted(h for t in spec["teams"] for h in t)
        for race in res.get("races", []):
            race["roster"] = spec["teams"]
            race["tag"] = tag
            flown = sorted(s["hull"] for s in race.get("seats", []))
            if flown and flown != asked:
                race["outcome"] = "hull-mismatch"
                notes.append(f"{tag} seed {race.get('seed')}: asked {asked}, flew {flown}")
            matches.append(race)
    return {"matches": matches, "wall_s": round(time.time() - t0, 1), "engine_commit": rj._commit(),
            "notes": sorted(set(notes)), "workdir": workdir}


def score(seat):
    return (seat.get("vc") or 0) + (seat.get("vd") or 0)


def good(m):
    return m.get("outcome") in ("ended", "timeout") and len(m.get("seats", [])) >= 2


def table(matches, hulls=None):
    """{hull: row}: balance index (mean of score / match mean), its standard error, VC and VD shares, team win rate,
    seconds in the nucleus, matches."""
    acc = {}
    for m in filter(good, matches):
        seats = m["seats"]
        mean = sum(score(s) for s in seats) / len(seats)
        if mean <= 0:
            continue
        vc_mean = sum(s["vc"] for s in seats) / len(seats) or 1
        vd_mean = sum(s["vd"] for s in seats) / len(seats) or 1
        for s in seats:
            if hulls and s["hull"] not in hulls:
                continue
            a = acc.setdefault(s["hull"], {"idx": [], "vc": [], "vd": [], "win": [], "nuc": [], "held": [], "lost": []})
            a["idx"].append(score(s) / mean)
            a["vc"].append(s["vc"] / vc_mean)
            a["vd"].append(s["vd"] / vd_mean)
            a["win"].append(1.0 if m.get("winner") == s["domain"] else 0.0)
            a["nuc"].append(s.get("nucleus_s") or 0)
            a["held"].append(s.get("held") or 0)
            a["lost"].append(s.get("lost") or 0)
    rows = {}
    for h, a in acc.items():
        n = len(a["idx"])
        mu = sum(a["idx"]) / n
        sd = (sum((x - mu) ** 2 for x in a["idx"]) / (n - 1)) ** 0.5 if n > 1 else 0.0
        avg = lambda k: sum(a[k]) / n  # noqa: E731
        rows[h] = {"index": round(mu, 3), "se": round(sd / math.sqrt(n), 3), "n": n, "vc_share": round(avg("vc"), 2),
                   "vd_share": round(avg("vd"), 2), "win": round(avg("win"), 2), "nucleus_s": round(avg("nuc"), 1),
                   "held": round(avg("held")), "lost": round(avg("lost"))}
    return rows


def largest_subset(rows, bar=BAR):
    """The largest set of hulls whose indexes all sit within +-bar of the set's own mean (indexes renormalised to the
    set). Exhaustive over contiguous windows of the sorted indexes, which is where the largest such set always lies."""
    hs = sorted(rows, key=lambda h: rows[h]["index"])
    best = []
    for i in range(len(hs)):
        for j in range(i + 1, len(hs) + 1):
            w = hs[i:j]
            mu = sum(rows[h]["index"] for h in w) / len(w)
            if all(abs(rows[h]["index"] / mu - 1) <= bar for h in w) and len(w) > len(best):
                best = w
    return best


def report(res, hulls=None):
    rows = table(res["matches"], hulls)
    sub = largest_subset(rows)
    ok = sum(1 for m in res["matches"] if good(m))
    lines = [f"{ok}/{len(res['matches'])} matches scored, engine {res.get('engine_commit')}",
             f"{'hull':12} {'index':>6} {'se':>6} {'n':>3} {'VC':>5} {'VD':>5} {'win':>5} {'nuc s':>6}  in subset"]
    for h in sorted(rows, key=lambda h: -rows[h]["index"]):
        r = rows[h]
        lines.append(f"{h:12} {r['index']:6.3f} {r['se']:6.3f} {r['n']:3d} {r['vc_share']:5.2f} {r['vd_share']:5.2f} "
                     f"{r['win']:5.2f} {r['nucleus_s']:6.1f}  {'yes' if h in sub else ''}")
    lines.append(f"largest subset within +-{int(BAR * 100)}%: {len(sub)} hulls: {', '.join(sub)}")
    return "\n".join(lines), rows, sub


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--hulls", default=",".join(FLEET))
    ap.add_argument("--rounds", type=int, default=15)
    ap.add_argument("--seeds-per-roster", type=int, default=1)
    ap.add_argument("--deal-seed", type=int, default=0)
    ap.add_argument("--teams", help="one roster: A,B,C/D,E,F")
    ap.add_argument("--seeds", default="1")
    ap.add_argument("--limit", type=float, default=300)
    ap.add_argument("--overrides", help="JSON file: [{asset, fields}] in-memory asset edits for this run")
    ap.add_argument("--start", help="starting element levels: card (default), rest, or a JSON table")
    ap.add_argument("--report", help="print the table of a saved result")
    ap.add_argument("--out")
    a = ap.parse_args()
    hulls = [h for h in a.hulls.split(",") if h]
    if a.report:
        res = json.load(open(a.report))
        print(report(res, None)[0])
        return
    overrides = json.load(open(a.overrides)) if a.overrides else None
    start = None if not a.start else json.loads(a.start) if a.start.startswith("{") else a.start
    if a.teams:
        rosters = [[t.split(",") for t in a.teams.split("/")]]
        seeds = [int(s) for s in a.seeds.split(",")]
        res = judge(rosters, seeds_per_roster=len(seeds), seed0=seeds[0], limit_s=a.limit, overrides=overrides, start=start)
    else:
        rosters = deal(hulls, a.rounds, seed=a.deal_seed)
        res = judge(rosters, seeds_per_roster=a.seeds_per_roster, limit_s=a.limit, overrides=overrides, start=start)
    print(report(res, None)[0])
    if a.out:
        with open(a.out, "w") as f:
            json.dump(res, f, indent=1)


if __name__ == "__main__":
    main()
