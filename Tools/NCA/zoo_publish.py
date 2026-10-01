"""Publish the zoo: the elite map, the most distinct elites, and their full evaluation.

    python Tools/NCA/zoo_publish.py select [--k 6]   # map.json + map.svg + elites.json (names to edit)
    python Tools/NCA/zoo_publish.py finalize         # per elite: eval16 (3 samples, all tiers), strike probe,
                                                     # rollout summary + tests_passed, cost; headline elite's
                                                     # summary/rollout/probe/eval16 at results/zoo/
    then python Tools/NCA/zoo_showcase.py            # playback page per elite

Distinctness: farthest-point selection over the elites in a normalised descriptor space (every
descriptor scaled by its 5-95% range over all valid individuals).
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import time
from concurrent.futures import ProcessPoolExecutor

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import zoo_search as zs  # noqa: E402

OUT = os.path.join(HERE, "results", "zoo")
DESC = ("stance", "live", "drama", "heal", "loose", "touched", "latency")


def _norm(valid):
    X = np.array([[r["desc"][k] for k in DESC] for r in valid], float)
    lo, hi = np.percentile(X, 5, 0), np.percentile(X, 95, 0)
    return lo, np.maximum(hi - lo, 1e-6)


def select(k=6, keep=None):
    recs = zs.load_log()
    valid = [r for r in recs if r.get("ok")]
    elites = zs.build_map(recs)
    lo, sc = _norm(valid)
    vec = lambda r: (np.array([r["desc"][d] for d in DESC], float) - lo) / sc
    cells = sorted(elites)
    json.dump(dict(axes=zs.AXES, evaluated=len(recs), valid=len(valid), rejected=len(recs) - len(valid),
                   cells=[dict(cell=list(c), div=elites[c]["div"], desc=elites[c]["desc"], g=elites[c]["g"]) for c in cells]),
              open(os.path.join(OUT, "map.json"), "w"), indent=1)
    svg(elites)
    pool = [elites[c] for c in cells]
    V = np.array([vec(r) for r in pool])
    chosen = [int(np.argmin([r["div"] for r in pool]))] if keep is None else list(keep)
    while len(chosen) < min(k, len(pool)):
        d = np.min(((V[:, None] - V[chosen][None]) ** 2).sum(-1), 1)
        d[chosen] = -1
        chosen.append(int(np.argmax(d)))
    out = []
    for i in chosen:
        r = pool[i]
        out.append(dict(slug=f"cell_{'_'.join(map(str, zs.cell(r['desc'])))}", name="?", blurb="?", cell=list(zs.cell(r["desc"])),
                        div=r["div"], desc=r["desc"], g=r["g"], per=r["per"], wander=0.25))
    npath = os.path.join(OUT, "names.json")
    names = json.load(open(npath)) if os.path.exists(npath) else {}
    for e in out:
        e.update(names.get(e["slug"], {}))
    json.dump(out, open(os.path.join(OUT, "elites.json"), "w"), indent=1)
    print(f"{len(recs)} evaluated, {len(valid)} valid, {len(elites)} cells")
    for e in out:
        print(e["slug"], e["div"], {k: e["desc"][k] for k in DESC + ("ms",)})
        on = [x for x in ("breathe", "jitter", "orbit", "burst", "curious", "hunt", "rush") if e["g"][x + "_on"]]
        print("   on:", on, "bristle" if e["g"]["bristle"] else "", {x: e["g"][x] for x in ("flee_c", "flee_m", "flee_s", "flee_t",
              "mob_c", "mob_m", "mob_s", "mob_t", "mob_speed", "inflate_c", "swirl", "morph_steps", "align_k", "vscale")})


def svg(elites):
    """The map as an SVG: one panel per drama bin, stance across, liveliness up, colour = crispness."""
    ns = [len(zs.AXES[a]) + 1 for a in zs.AXES]
    cw, ch, pad = 26, 22, 30
    pw = ns[0] * cw + pad
    W = ns[2] * pw + 20; H = ns[1] * ch + 70
    divs = [r["div"] for r in elites.values()]
    lo, hi = min(divs), max(divs)
    s = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" font-family="sans-serif" font-size="10">',
         f'<rect width="{W}" height="{H}" fill="#0b1020"/>']
    for dz in range(ns[2]):
        x0 = 20 + dz * pw
        s.append(f'<text x="{x0}" y="14" fill="#dfe6f5">drama bin {dz}</text>')
        for sx in range(ns[0]):
            for ly in range(ns[1]):
                c = (sx, ly, dz)
                y = 22 + (ns[1] - 1 - ly) * ch
                if c in elites:
                    t = (elites[c]["div"] - lo) / max(hi - lo, 1e-6)
                    col = f"rgb({int(60 + 180 * t)},{int(200 - 120 * t)},{int(170 - 60 * t)})"
                else:
                    col = "#1a2238"
                s.append(f'<rect x="{x0 + sx * cw}" y="{y}" width="{cw - 2}" height="{ch - 2}" fill="{col}"/>')
        s.append(f'<text x="{x0}" y="{22 + ns[1] * ch + 12}" fill="#8a96b3">flee -> mob</text>')
    s.append(f'<text x="20" y="{H - 8}" fill="#8a96b3">rows: liveliness (up = livelier). colour: green = crisper body, red = blurrier. '
             f'{len(elites)} cells filled</text></svg>')
    open(os.path.join(OUT, "map.svg"), "w").write("\n".join(s))


def _final_one(e):
    import torch
    torch.set_num_threads(1)
    import swarm_eval as se
    import swarm_nca as sn
    import swarm_probe
    import zoo_model as zm
    t0 = time.time()
    g = e["g"]
    ev = se.evaluate(zm.make(g), samples=3, full=True, log=lambda *a: None)
    pr = swarm_probe.probe(zm.make(g))
    model = zm.make(g)
    data, summary = sn.rollout(model, 240)
    passed, close = sn.tests_passed(summary)
    return dict(slug=e["slug"], eval16=ev, probe=pr, summary=summary, data=data, passed8=passed, close8=round(close, 2),
                sec=round(time.time() - t0))


def finalize(headline=0):
    import swarm_nca as sn
    import zoo_model as zm
    elites = json.load(open(os.path.join(OUT, "elites.json")))
    with ProcessPoolExecutor(4) as ex:
        res = list(ex.map(_final_one, elites))
    for e, r in zip(elites, res):
        d = os.path.join(OUT, "elites", e["slug"])
        os.makedirs(d, exist_ok=True)
        ev = r["eval16"]
        json.dump(ev, open(os.path.join(d, "eval16.json"), "w"), indent=1)
        json.dump(dict(strike=r["probe"], behaviour=e["per"]), open(os.path.join(d, "probe.json"), "w"), indent=1)
        r["summary"]["meta"] = {"device": "cpu", "tag": f"zoo/{e['slug']}", "note": f"zoo elite '{e['name']}': zoo_model.py",
                                "cfg": zm.cfg_to_dict(zm.genome_to_cfg(e["g"]))}
        r["summary"]["rule"] = "params.json"
        json.dump(r["summary"], open(os.path.join(d, "summary.json"), "w"), indent=1)
        json.dump(dict(genome=e["g"], cfg=zm.cfg_to_dict(zm.genome_to_cfg(e["g"])), name=e["name"]),
                  open(os.path.join(d, "params.json"), "w"), indent=1)
        heal = {k: v["heal"] for k, v in r["probe"].items()}
        e["card"] = {"eval16 (3 samples, all tiers)": f"{ev['passed']}/{ev['feasible']}",
                     "strict tests_passed (rollout)": f"{r['passed8']}/8",
                     "strike heal (whale/jelly/puffer/dragon)": " / ".join(str(heal[k]) for k in sn.KINDS),
                     "stance (log2 crowd vs inert)": e["desc"]["stance"], "idle speed (vox/step)": e["desc"]["live"],
                     "switch drama (peak radius ratio)": e["desc"]["drama"], "heal steps": e["desc"]["heal"],
                     "looseness": e["desc"]["loose"], "touched by a pass": e["desc"]["touched"],
                     "switch latency (steps)": e["desc"]["latency"], "ms / step (numpy, worst plan)": e["desc"]["ms"]}
        e["eval16"] = dict(passed=ev["passed"], feasible=ev["feasible"], na=ev["na"])
        e["passed8"] = r["passed8"]; e["heal"] = heal
        print(e["slug"], e["name"], e["card"], f"{r['sec']}s", flush=True)
        if elites.index(e) == headline:
            json.dump(sn.pack(r["data"], 240), open(os.path.join(OUT, "rollout.json"), "w"))
            json.dump(r["summary"], open(os.path.join(OUT, "summary.json"), "w"), indent=1)
            json.dump(ev, open(os.path.join(OUT, "eval16.json"), "w"), indent=1)
            json.dump(dict(strike=r["probe"], behaviour=e["per"]), open(os.path.join(OUT, "probe.json"), "w"), indent=1)
            json.dump(dict(genome=e["g"], cfg=zm.cfg_to_dict(zm.genome_to_cfg(e["g"])), name=e["name"]),
                      open(os.path.join(OUT, "params.json"), "w"), indent=1)
    json.dump(elites, open(os.path.join(OUT, "elites.json"), "w"), indent=1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["select", "finalize"])
    ap.add_argument("--k", type=int, default=6)
    ap.add_argument("--headline", type=int, default=0)
    a = ap.parse_args()
    os.makedirs(OUT, exist_ok=True)
    if a.cmd == "select":
        select(a.k)
    else:
        finalize(a.headline)


if __name__ == "__main__":
    main()
