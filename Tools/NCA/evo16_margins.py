"""evo16: per-transition margin from a held-out measurement (evo16_measure output).

margin = (best other plan - wanted plan) / (best other + wanted), from the 3-sample mean cross scores;
> 0 means the wanted plan is closest. Prints the mean per transition and the minimum over seeds.
    python Tools/NCA/evo16_margins.py results/evo16/heldout.json results/evo16/heldout_round1_genome.json
"""
import json
import sys


def margins(path):
    d = json.load(open(path))
    per = {}
    for r in d["results"].values():
        rows = [(f"{k}->{k}", v["cross"], k) for k, v in r["own"].items()] + \
               [(t, v["cross"], t.split("->")[1]) for t, v in r["switch"].items()]
        for t, cross, want in rows:
            o = min(x for k, x in cross.items() if k != want)
            per.setdefault(t, []).append((o - cross[want]) / (o + cross[want]))
    return per


if __name__ == "__main__":
    ms = [margins(p) for p in sys.argv[1:]]
    print(f"{'transition':16s} " + " ".join(f"{'mean':>7s} {'min':>6s}" for _ in ms))
    for t in ms[0]:
        print(f"{t:16s} " + " ".join(f"{sum(m[t]) / len(m[t]):7.3f} {min(m[t]):6.3f}" for m in ms))
    print(f"{'ALL':16s} " + " ".join(f"{sum(sum(v) for v in m.values()) / sum(len(v) for v in m.values()):7.3f} {min(min(v) for v in m.values()):6.3f}" for m in ms))
