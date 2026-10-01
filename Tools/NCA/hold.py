"""HOLD a front runner's scores, then let anything that wants to replace it prove it gives nothing up.

The lead's plan: once a front runner scores well enough, hold its scores - accuracy, lossless, organic
feel and the smoothness of its changes - and only then optimise it for the lightest, most
computationally performant and scalable implementation. This file is the hold.

  record   python Tools/NCA/hold.py --model <spec> --record results/hold/<name>.json
           measures the model on every held axis and freezes it as a baseline.
  check    python Tools/NCA/hold.py --model <spec> --baseline results/hold/<name>.json [--out x.json]
           measures a candidate the same way and prints PASS/FAIL per axis; exit code 1 on any FAIL.

Held axes (a candidate FAILS if it is worse than the baseline beyond the tolerance):
  ACCURATE  swarm_eval.evaluate at HOLD_SEEDS (3 samples, loss-8 bar). Per seed: passed may not drop; no
            test the baseline passed may fail; every own-plan loss may rise at most max(0.5, 15%).
  LOSSLESS  scorecard.lossless_and_cost deaths: a lossless baseline holds 0.
  ORGANIC   swarm_feel.in_band at seed 7: an in-band baseline must stay in band, and planar excess may rise
            at most 0.05.
  SMOOTH    swarm_smooth.smooth at seed 7: smoothness may fall at most 0.05; worst lurch at most +25%; worst
            teleport at most max(swarm_smooth.TELEPORT_OK = 1.5, +10%) - up to 1.5x the world's top speed is a
            fast swimmer plus a collision push (the smoothness calibration's own comfort range), not a relocation;
            worst molt/birth burst at most +0.05; no new deaths.
Reported, never gated (it is what the next stage optimises): PERFORMANT ms/step at the grown size, 1 thread,
plus anything in `extra` the caller adds (e.g. scaled-population cost).
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import time

import torch

import scorecard
import swarm_eval as se
import swarm_feel as sf
import swarm_nca as sn
import swarm_smooth as ss

HOLD_SEEDS = (7, 23, 41, 101)


def measure(model, seeds=HOLD_SEEDS, samples=3, log=print):
    t0 = time.time()
    out = dict(seeds=list(seeds), accurate={})
    for s in seeds:
        r = se.evaluate(model, seed=s, samples=samples, full=True, log=lambda *_: None)
        tests = {f"own {k}": dict(ok=v["ok"], rate=v["rate"], loss=v["cross"][k]) for k, v in r["own"].items()}
        for key, v in r["switch"].items():
            to = key.split("->")[1]
            tests[key] = dict(ok=v["ok"], rate=v["rate"], loss=v["cross"][to])
        out["accurate"][str(s)] = dict(passed=r["passed"], feasible=r["feasible"], na=r["na"], tests=tests)
        log(f"  seed {s}: {r['passed']}/{r['feasible']}  ({time.time() - t0:.0f}s)")
    torch.set_num_threads(1)
    lc = scorecard.lossless_and_cost(model, seed=seeds[0])
    torch.set_num_threads(4)
    out["lossless"] = dict(deaths=lc["deaths"], per_1k=lc["deaths_per_1k_tadpole_steps"])
    out["performant"] = dict(ms_per_step=lc["ms_per_step"], grown_n=lc["grown_n"])
    f = sf.feel(model, seed=seeds[0]); ok, checks = sf.in_band(f)
    out["organic"] = dict(in_band=ok, planar_excess=round(sf.planar_excess(f), 3), checks=checks, mean=f["mean"])
    sm = ss.smooth(model, seed=seeds[0], log=lambda *_: None)
    out["smooth"] = {k: sm[k] for k in ("smoothness", "mean", "worst", "deaths")}
    out["smooth"]["events"] = sm["events"]
    out["seconds"] = round(time.time() - t0, 1)
    log(f"  lossless {out['lossless']}  perf {out['performant']}  organic {ok} (planar {out['organic']['planar_excess']})  "
        f"smoothness {sm['smoothness']}  ({out['seconds']}s)")
    return out


def compare(base, cand):
    """[(axis, ok, detail)] - every held axis of `cand` against `base`."""
    res = []
    for s, b in base["accurate"].items():
        c = cand["accurate"].get(s)
        if c is None:
            res.append((f"accurate seed {s}", False, "not measured")); continue
        res.append((f"accurate seed {s} passed", c["passed"] >= b["passed"], f"{c['passed']}/{c['feasible']} vs {b['passed']}/{b['feasible']}"))
        lost = [k for k, v in b["tests"].items() if v["ok"] and not c["tests"].get(k, {}).get("ok", False)]
        res.append((f"accurate seed {s} no lost test", not lost, ", ".join(lost) or "-"))
        worse = [f"{k} {c['tests'][k]['loss']} > {v['loss']}" for k, v in b["tests"].items()
                 if k.startswith("own") and k in c["tests"] and c["tests"][k]["loss"] > v["loss"] + max(0.5, 0.15 * v["loss"])]
        res.append((f"accurate seed {s} own losses", not worse, "; ".join(worse) or "-"))
    if base["lossless"]["deaths"] == 0:
        res.append(("lossless", cand["lossless"]["deaths"] == 0, f"{cand['lossless']['deaths']} deaths"))
    if base["organic"]["in_band"]:
        res.append(("organic band", cand["organic"]["in_band"], str(cand["organic"]["checks"])))
    res.append(("organic planar", cand["organic"]["planar_excess"] <= base["organic"]["planar_excess"] + 0.05,
                f"{cand['organic']['planar_excess']} vs {base['organic']['planar_excess']}"))
    bs, cs = base["smooth"], cand["smooth"]
    res.append(("smoothness", cs["smoothness"] >= bs["smoothness"] - 0.05, f"{cs['smoothness']} vs {bs['smoothness']}"))
    res.append(("smooth lurch", cs["worst"]["lurch"] <= 1.25 * bs["worst"]["lurch"], f"{cs['worst']['lurch']} vs {bs['worst']['lurch']}"))
    res.append(("smooth teleport", cs["worst"]["teleport"] <= max(ss.TELEPORT_OK, 1.1 * bs["worst"]["teleport"]), f"{cs['worst']['teleport']} vs {bs['worst']['teleport']}"))
    for kk in ("molt_burst", "birth_burst"):
        res.append((f"smooth {kk}", cs["worst"][kk] <= bs["worst"][kk] + 0.05, f"{cs['worst'][kk]} vs {bs['worst'][kk]}"))
    res.append(("smooth deaths", cs["deaths"] <= bs["deaths"], f"{cs['deaths']} vs {bs['deaths']}"))
    return res


def main():
    torch.set_num_threads(4)
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", required=True)
    ap.add_argument("--record", default="", help="freeze this model's scores as a baseline at this path")
    ap.add_argument("--baseline", default="", help="check this model against a frozen baseline")
    ap.add_argument("--seeds", default=",".join(map(str, HOLD_SEEDS)))
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    model = se.load_model(a.model)
    m = measure(model, seeds=[int(s) for s in a.seeds.split(",")])
    m["model"] = a.model
    if a.record:
        os.makedirs(os.path.dirname(a.record) or ".", exist_ok=True)
        json.dump(m, open(a.record, "w"), indent=1)
        print(f"recorded baseline {a.record}")
    if a.out:
        json.dump(m, open(a.out, "w"), indent=1)
    if a.baseline:
        rows = compare(json.load(open(a.baseline)), m)
        for axis, ok, d in rows:
            print(f"{'PASS' if ok else 'FAIL'}  {axis:32s} {d}")
        print(f"perf {m['performant']} (baseline {json.load(open(a.baseline))['performant']})")
        sys.exit(0 if all(ok for _, ok, _ in rows) else 1)


if __name__ == "__main__":
    main()
