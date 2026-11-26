#!/usr/bin/env python3
"""Rhino Brood Rush lever sweep: every match seats the Rhino plus 8 of the 14 card hulls, 3 teams of 3, AI Medium.

    python3 sweep.py <name> <overrides.json|-> [rosters] [deal_seed]
Prints the Rhino's index (pilot VC+VD / match mean) and writes <name>.json next to this file.
"""
import json, os, random, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Compute", "evaluators"))
import brood_judge as bj  # noqa
import regatta_judge as rj  # noqa

CARD14 = ["Meerkat", "Wrasse", "Urchin", "Honeyguide", "Stoat", "Sparrow", "Scarab", "Squirrel", "Dolphin", "Orca",
          "TigerMoth", "Thresher", "Shrike", "Grizzly"]
HERE = os.path.dirname(os.path.abspath(__file__))


def rosters(n, seed):
    out = []
    for r in range(n):
        rng = random.Random(seed * 1000 + r)
        others = rng.sample(CARD14, 8)
        seats = ["Rhino"] + others
        rng.shuffle(seats)
        out.append([seats[0:3], seats[3:6], seats[6:9]])
    return out


def main():
    name, ov = sys.argv[1], sys.argv[2]
    n = int(sys.argv[3]) if len(sys.argv) > 3 else 6
    seed = int(sys.argv[4]) if len(sys.argv) > 4 else 7
    overrides = json.load(open(ov)) if ov != "-" else None
    # Medium, as Unity ships it: the judge's spec takes "difficulty"; patch it into every job.
    orig = rj._run_one

    def run_one(spec, timeout_s, workdir, tag):
        spec = dict(spec, difficulty="Medium")
        return orig(spec, timeout_s, workdir, tag)
    rj._run_one = run_one
    res = bj.judge(rosters(n, seed), seeds_per_roster=1, seed0=100 + seed * 50, overrides=overrides,
                   workers=int(os.environ.get("PAR", 3)))
    res["overrides"] = overrides
    json.dump(res, open(os.path.join(HERE, name + ".json"), "w"), indent=1)
    rows = bj.table(res["matches"])
    r = rows.get("Rhino", {})
    ok = sum(1 for m in res["matches"] if bj.good(m))
    print(f"{name}: Rhino index {r.get('index')} se {r.get('se')} n {r.get('n')} VC {r.get('vc_share')} VD {r.get('vd_share')} "
          f"win {r.get('win')} | {ok}/{len(res['matches'])} ok, {res['wall_s']} s | notes {res['notes'][:3]}")


if __name__ == "__main__":
    main()
