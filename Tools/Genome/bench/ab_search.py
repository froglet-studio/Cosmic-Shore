"""A/B the genome search on a synthetic target: a hidden genome's own renders are the target statistics, so the
best possible distance is known (the hidden genome scores about 1.0 against its own windows) and every arm races to
it from the same seeds at the same render budget. No reels, critic or judge needed: the stat fitness alone.

    python3 Tools/Genome/bench/ab_search.py target OUT           # hidden genomes, target stats, seed genomes
    python3 Tools/Genome/bench/ab_search.py run OUT --arm new --target near --seed 1 [--gens 40 --mu 24 --lam 24]
    python3 Tools/Genome/bench/ab_search.py report OUT            # tables + OUT/report.json

Arms (agenome.search flags): base = the diamond with tournament parents, fixed operators and random tuning (the
search before 2026-11-10); new = pareto survival, lexicase parents, adaptive operators, CMA tuning; the forward arms
base+pareto, base+lexicase, base+aos, base+cma24, base+cma6 (base with one piece of new); and the leave-one-out arms
new-survival, new-parents, new-operators, new-tuner (new with one piece set back to base). Extra agenome.search
flags for every arm of a run go after --: run OUT --arm base -- --span 0.1
"""
import argparse
import glob
import json
import os
import random
import subprocess
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, ROOT)

FRAMES = 30          # 2 s windows at 15 fps: half the islands' clip, so a run fits a small machine
TRAIN_T = [0, 2, 4, 6, 8, 10, 12, 14]
HOLD_T = [16, 18, 20, 22]

BASE = ["--survival", "diamond", "--parents", "tournament", "--operators", "fixed", "--tuner", "random"]
NEW = ["--survival", "pareto", "--parents", "lexicase", "--operators", "adaptive", "--tuner", "cma"]
ARMS = {"base": BASE, "new": NEW}


def _with(arm, flag, value, *more):
    a = list(arm)
    a[a.index(flag) + 1] = value
    return a + list(more)


# leave one out: new with one piece set back to base
for flag, back in [("--survival", "diamond"), ("--parents", "tournament"), ("--operators", "fixed"),
                   ("--tuner", "random")]:
    ARMS["new-" + flag[2:]] = _with(NEW, flag, back)
# forward: base with one piece of new (the CMA tuner at its default 24-byte block and at 6 bytes)
ARMS["base+pareto"] = _with(BASE, "--survival", "pareto")
ARMS["base+lexicase"] = _with(BASE, "--parents", "lexicase")
ARMS["base+aos"] = _with(BASE, "--operators", "adaptive")
ARMS["base+cma24"] = _with(BASE, "--tuner", "cma")
ARMS["base+cma6"] = _with(BASE, "--tuner", "cma", "--cma-block", "6")
# the forward winners combined
ARMS["pareto+cma"] = _with(_with(BASE, "--survival", "pareto"), "--tuner", "cma")
ARMS["pareto+cma+aos"] = _with(ARMS["pareto+cma"], "--operators", "adaptive")
# steward experiments (LEDGER.md)
ARMS["base+cma24op"] = _with(BASE, "--tuner", "cma", "--cma-blocks", "op")  # L7: blocks of whole operators
ARMS["pareto+cmaop"] = ARMS["pareto+cma"] + ["--cma-blocks", "op"]  # L7 inside the pair


def hidden(name):
    """The hidden target genomes: near = the second hand-built seed (amoebius_v2) after five structural edits and
    fresh bytes on a fifth of its parameters (a violet scene with prism trails), so the seeds start close but not on
    it; far = a random 5-node genome (a textured sphere on stripes) that no seed resembles."""
    from agenome.genome import mutate, random_genome
    from agenome.handmade import amoebius_v2
    if name == "near":
        rng = random.Random(106)
        g = amoebius_v2(seed=7)
        for _ in range(5):
            g = mutate(g, rng, structural=1.0)
        for n in g.nodes:
            n.params = [rng.randrange(256) if rng.random() < 0.2 else b for b in n.params]
        return g.repair().ensure_root().prune()
    if name == "far":
        rng = random.Random(23)
        return random_genome(rng, size=10).ensure_root().prune()
    raise ValueError(name)


def cmd_target(a):
    from agenome.features import TargetStats, window_features
    from agenome.genome import random_genome
    from agenome.render import Renderer
    os.makedirs(a.out, exist_ok=True)
    R = Renderer()
    info = {}
    for name in ("near", "far"):
        g = hidden(name)
        feats = {}
        for t in TRAIN_T + HOLD_T:
            feats[t] = window_features(R.render(g, FRAMES, t_shift=float(t)), sub=2)
        tr = TargetStats([feats[t] for t in TRAIN_T])
        ho = TargetStats([feats[t] for t in HOLD_T])
        tr.save(os.path.join(a.out, f"stats_{name}.npz"))
        ho.save(os.path.join(a.out, f"hold_{name}.npz"))
        g.meta = {"name": f"hidden_{name}"}
        g.save(os.path.join(a.out, f"hidden_{name}.json"))
        own = [tr.distance(feats[t])["total"] for t in TRAIN_T]
        held = [tr.distance(feats[t])["total"] for t in HOLD_T]
        fr = R.render(g, FRAMES)
        info[name] = {"zbytes": g.size(), "nodes": len(g.nodes), "own_dist": round(float(np.mean(own)), 4),
                      "own_hold_dist": round(float(np.mean(held)), 4), "luma": round(float(fr.mean() / 255), 3)}
        from PIL import Image
        Image.fromarray(np.concatenate([fr[i] for i in (0, 10, 20, 29)], 1)).save(os.path.join(a.out, f"hidden_{name}.png"))
    # seeds: eight random genomes (search.run adds its three hand-built ones)
    sd = os.path.join(a.out, "seeds")
    os.makedirs(sd, exist_ok=True)
    rng = random.Random(5)
    for k in range(8):
        g = random_genome(rng, size=8 + k % 4).ensure_root().prune()
        g.meta = {"name": f"rand{k}"}
        g.save(os.path.join(sd, f"rand{k}.json"))
    json.dump(info, open(os.path.join(a.out, "targets.json"), "w"), indent=1)
    print(json.dumps(info, indent=1))


def cmd_run(a):
    run = os.path.join(a.out, "runs", f"{a.target}__{a.arm}{a.tag}__s{a.seed}")
    if os.path.exists(os.path.join(run, "done")) and not a.force:
        print("done already:", run)
        return
    if os.path.exists(run):
        import shutil
        shutil.rmtree(run)
    argv = [sys.executable, "-m", "agenome.search", "--run", run,
            "--stats", os.path.join(a.out, f"stats_{a.target}.npz"), "--hold", os.path.join(a.out, f"hold_{a.target}.npz"),
            "--seeds", os.path.join(a.out, "seeds", "*.json"), "--workers", str(a.workers), "--gens", str(a.gens),
            "--mu", str(a.mu), "--lam", str(a.lam), "--frames", str(FRAMES), "--critic-w", "0", "--judge-w", "0",
            "--audit-offsets", "none", "--save-every", "1000", "--log-front", "--seed", str(a.seed)] + ARMS[a.arm] + \
        list(a.extra or [])
    env = dict(os.environ, AGENOME_EXT=os.environ.get("AGENOME_EXT", "0"), PYTHONPATH=ROOT)
    with open(run + ".out", "w") as f:
        p = subprocess.run(argv, cwd=ROOT, env=env, stdout=f, stderr=subprocess.STDOUT)
    if p.returncode == 0:
        open(os.path.join(run, "done"), "w").write("ok")
    print(run, "exit", p.returncode)


def front_metrics(front, ref):
    from agenome.selection import hypervolume
    pts = [tuple(p) for p in front]
    return hypervolume(pts, ref)


def cmd_report(a):
    rows = {}
    for d in sorted(glob.glob(os.path.join(a.out, "runs", "*__*__s*"))):
        if not os.path.exists(os.path.join(d, "done")) or not os.path.exists(os.path.join(d, "log.jsonl")):
            continue
        target, arm, s = os.path.basename(d).split("__")
        L = [json.loads(x) for x in open(os.path.join(d, "log.jsonl"))]
        rows.setdefault((target, arm), []).append((int(s[1:]), L, d))
    out = {}
    for target in sorted({t for t, _ in rows}):
        tinfo = json.load(open(os.path.join(a.out, "targets.json")))[target]
        # one reference point per target for every arm: 1.5 x the worst final best dist of any run, budget 1500 B
        finals = [L[-1]["best_dist"] for (t, _), runs in rows.items() if t == target for _, L, _ in runs]
        ref = (1.5 * max(finals), 1500.0)
        print(f"\n== target {target}: hidden genome {tinfo['zbytes']} B, scores {tinfo['own_dist']} on its own windows "
              f"({tinfo['own_hold_dist']} held out); HV reference {ref[0]:.3f} x 1500 B")
        print(f"{'arm':<15}{'n':>3}{'best dist':>18}{'anytime':>10}{'hold':>9}{'HV':>13}{'bytes @5%':>11}{'evals':>7}")
        for arm in [k for k in ARMS if (target, k) in rows] + sorted(k for t, k in rows if t == target and k not in ARMS):
            runs = rows[(target, arm)]
            best = [L[-1]["best_dist"] for _, L, _ in runs]
            hold = [L[-1]["best_hold"] for _, L, _ in runs]
            evals = [L[-1]["evals"] for _, L, _ in runs]
            # anytime: mean best dist over the run, sampled at the same eval counts for every arm
            grid = np.linspace(0, min(evals), 41)[1:]
            any_ = []
            for _, L, _ in runs:
                e = np.array([r["evals"] for r in L])
                b = np.minimum.accumulate(np.array([r["best_dist"] for r in L]))
                any_.append(float(np.mean([b[max(0, np.searchsorted(e, x, side="right") - 1)] for x in grid])))
            hv = [front_metrics(L[-1]["front"], ref) for _, L, _ in runs]
            small = []
            for _, L, _ in runs:
                fr = L[-1]["front"]
                d0 = min(p[0] for p in fr)
                small.append(min(p[1] for p in fr if p[0] <= d0 * 1.05))
            m = lambda v: f"{np.mean(v):.3f}±{np.std(v) / max(1, np.sqrt(len(v) - 1)):.3f}"
            print(f"{arm:<15}{len(runs):>3}{m(best):>18}{np.mean(any_):>10.3f}{np.mean(hold):>9.3f}"
                  f"{np.mean(hv):>13.2f}{np.mean(small):>11.0f}{int(np.mean(evals)):>7}")
            out[f"{target}/{arm}"] = {"n": len(runs), "seeds": [s for s, _, _ in runs], "best": best, "anytime": any_,
                                      "hold": hold, "hv": hv, "bytes_at_5pct": small, "evals": evals,
                                      "curves": [[[r["evals"], r["best_dist"]] for r in L] for _, L, _ in runs]}
    json.dump(out, open(os.path.join(a.out, "report.json"), "w"))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    t = sub.add_parser("target")
    t.add_argument("out")
    r = sub.add_parser("run")
    r.add_argument("out")
    r.add_argument("--arm", required=True, choices=sorted(ARMS))
    r.add_argument("--target", default="near", choices=["near", "far"])
    r.add_argument("--seed", type=int, default=1)
    r.add_argument("--gens", type=int, default=40)
    r.add_argument("--mu", type=int, default=24)
    r.add_argument("--lam", type=int, default=24)
    r.add_argument("--workers", type=int, default=2)
    r.add_argument("--force", action="store_true")
    r.add_argument("--tag", default="", help="suffix for the arm's run name (for runs with extra flags)")
    r.add_argument("extra", nargs="*", help="extra agenome.search flags, after --")
    p = sub.add_parser("report")
    p.add_argument("out")
    a = ap.parse_args()
    {"target": cmd_target, "run": cmd_run, "report": cmd_report}[a.cmd](a)


if __name__ == "__main__":
    main()
