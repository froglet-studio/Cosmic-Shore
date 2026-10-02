"""Markdown summary of the final scorecards (python Tools/Ecology/builders/summarise.py)."""
import json, os
import numpy as np
R = os.path.join(os.path.dirname(os.path.abspath(__file__)), "results")
NAMES = ["nest_v1_logistic", "nest_v2_wasp", "fortress_final", "traps_v2_fair", "wearers_v2_contact", "wearers_v3_moult"]


def agg(st, pol, key):
    v = [x[key] for k, x in st.items() if k.startswith(pol + "/") and x.get(key) is not None]
    return float(np.mean(v)) if v else None


def f(x, d=1):
    return "-" if x is None else (f"{x:.{d}f}" if isinstance(x, float) else str(x))


rows = ["| species | wander hits/min | evader | racer (circuit) | hunter hits/min | telegraph s | hunter kills/min | built (wander) | player trail in it | Jaccard seeds | moves/s (wander) | index queries/s | audit |",
        "|---|---|---|---|---|---|---|---|---|---|---|---|---|"]
for n in NAMES:
    p = os.path.join(R, n + ".json")
    if not os.path.exists(p):
        continue
    c = json.load(open(p)); st = c["structure"]
    rows.append("| " + " | ".join([n, f(c["hits_per_min_wander"], 2), f(c["hits_per_min_evader"], 2),
                                   f(c.get("hits_per_min_circuit"), 2), f(agg(st, "hunter", "hits_per_min"), 2),
                                   f(c["telegraph_s"], 2), f(c["payoff_per_min"], 1), f(agg(st, "wander", "built"), 0),
                                   f(agg(st, "wander", "trail_frac"), 2), f(c["replay"]["jaccard_mean"], 2),
                                   f(agg(st, "wander", "moves_per_s"), 0), f(agg(st, "wander", "queries_per_s"), 0),
                                   f(max(abs(x["audit"]) for x in st.values()), 3)]) + " |")
print("\n".join(rows))
