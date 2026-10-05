#!/usr/bin/env python3
"""Score the SHIPPED grid core (SwarmGridCore.cs) with the research's UNCHANGED scorer.

    python3 Tools/Build/swarm_core_harness/score_grid.py [--seeds 7,23,41,108,209] [--no-ref]

What it does:
  1. builds and runs the C# harness in export mode (run.sh export): every plan grown 240 steps from
     the research's seed (16 tadpoles, randn x 2) in RESEARCH mode (SwarmGridCore as hgrid2 runs:
     three domain slots, hunger kills, free laying) and in GAME mode (one domain, hunger never kills,
     funded laying from a full stomach, oriented body, lock 30, seeded with the game's 24);
  2. decodes every exported tadpole through swarm_nca.decode and scores it with swarm_nca.swarm_loss
     (default LossCfg) against all four targets - the cross row whose diagonal is the own-plan loss;
  3. runs the Python hgrid2 itself (results/hgrid2/params.json) at the same seeds as the reference;
  4. computes swarm_feel.metrics on a 64-step window of each grown body (C# grid, Python hgrid2).

GAME mode is scored against the ONE-DOMAIN version of each plan (every unit's slot set to 0): the
game's one-colour law has no domain slots, so the domain term would only measure that the game
dropped them. That is a different Target object handed to the same, unchanged swarm_loss.

The research code is read from the research branch (git archive of Tools/NCA from
origin/cece/gifted-curie-x2cpd0) unless --nca points at a checkout. Needs torch + numpy.
"""
import argparse
import json
import os
import statistics
import subprocess
import sys
import tempfile
import time

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
assert os.path.isdir(os.path.join(REPO, "Assets")), REPO
REF = "origin/cece/gifted-curie-x2cpd0"
KINDS = ("mass", "space", "charge", "time")
PLANS = os.path.join(REPO, "Assets", "_SO_Assets", "Swarm Fauna", "Plans")


def research_dir(arg):
    if arg:
        return arg
    d = tempfile.mkdtemp(prefix="nca_")
    tar = subprocess.run(["git", "-C", REPO, "archive", REF, "Tools/NCA"], check=True, capture_output=True).stdout
    subprocess.run(["tar", "-x", "-C", d], input=tar, check=True)
    return os.path.join(d, "Tools", "NCA")


def build_swarm(sn, torch, units, cap=280):
    """An exported unit row -> one sample of swarm_nca.Swarm (the channels decode() reads)."""
    N = max(cap, len(units))
    pos = torch.zeros(1, N, 3); s = torch.zeros(1, N, sn.C)
    elem = torch.zeros(1, N, dtype=torch.long); dom = torch.zeros(1, N, dtype=torch.long)
    active = torch.zeros(1, N, dtype=torch.bool); hatched = torch.zeros(1, N, dtype=torch.bool)
    for i, u in enumerate(units):
        pos[0, i] = torch.tensor(u[0:3]); elem[0, i] = int(u[3]); dom[0, i] = int(u[4])
        hatched[0, i] = u[5] > 0.5; active[0, i] = True
        s[0, i, sn.A] = u[6]; s[0, i, sn.DIE] = u[7] if u[5] > 0.5 else 0.0
        s[0, i, sn.PR] = torch.tensor(u[8:11]); s[0, i, sn.TI] = torch.tensor(u[11:14])
        s[0, i, sn.FAC] = torch.tensor(u[14:17]); s[0, i, sn.SP] = torch.tensor(u[17:19])
    return sn.Swarm(pos, s, elem, dom, active, hatched, torch.zeros(1, dtype=torch.long))


def one_domain(sn, T):
    """The same plan with every unit in slot 0 (the game's one-colour body)."""
    import copy
    T1 = copy.copy(T)
    T1.frames = [dict((k, v) for k, v in fr.items() if not k.startswith("_") and k != "pc") for fr in T.frames]
    for fr in T1.frames:
        fr["slot"] = fr["slot"] * 0
    T1.slots = 1
    T1.slot_mix = [sum(T.slot_mix), 0, 0]
    return T1


def row(sn, x, targets, L):
    return {k: round(sn.swarm_loss(x, targets[k], L)[1]["sink"], 2) for k in KINDS}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seeds", default="7,23,41,108,209")
    ap.add_argument("--nca", default="")
    ap.add_argument("--no-ref", action="store_true", help="skip the Python hgrid2 reference run")
    ap.add_argument("--out", default="", help="write the full results JSON here")
    ap.add_argument("--modes", default="research,game,field", help="C# modes to export (field = feel only)")
    a = ap.parse_args()
    seeds = [int(x) for x in a.seeds.split(",")]

    nca = research_dir(a.nca)
    sys.path.insert(0, nca)
    import torch
    torch.set_num_threads(4)
    import swarm_nca as sn
    import swarm_feel
    targets = sn.load_targets(os.path.join(nca, "results", "swarm_targets"))
    targets1 = {k: one_domain(sn, T) for k, T in targets.items()}
    L = sn.LossCfg()

    exp = os.path.join(tempfile.mkdtemp(prefix="swarm_export_"), "states.json")
    t0 = time.time()
    subprocess.run(["bash", os.path.join(HERE, "run.sh"), "export", PLANS, exp, ",".join(map(str, seeds)), "240", "64", a.modes], check=True)
    runs = json.load(open(exp))["runs"]
    print(f"\nC# export: {len(runs)} runs in {time.time() - t0:.0f}s")

    res = {"seeds": seeds, "csharp": {}, "python": {}, "feel": {}}
    for r in runs:
        if r["mode"] == "field":     # feel only: the field core has no look state to score
            P = torch.tensor(r["win"], dtype=torch.float32)
            res["feel"].setdefault("csharp_field", {}).setdefault(r["kind"], []).append(
                swarm_feel.metrics(P, torch.tensor(r["winElem"]), None))
            continue
        sw = build_swarm(sn, torch, r["units"])
        x = sn.decode(sw, 0)
        T = targets if r["mode"] == "research" else targets1
        cross = row(sn, x, T, L)
        n = int(sw.hatched[0].sum())
        res["csharp"].setdefault(r["mode"], {}).setdefault(r["kind"], {})[r["seed"]] = dict(cross=cross, n=n, ms=r["ms"])
        P = torch.tensor(r["win"], dtype=torch.float32)
        if P.ndim == 3 and P.shape[1] >= 8:
            m = swarm_feel.metrics(P, torch.tensor(r["winElem"]), r["kind"] if r["mode"] == "research" else None)
            res["feel"].setdefault("csharp_" + r["mode"], {}).setdefault(r["kind"], []).append(m)

    if not a.no_ref:
        import hgrid2_model as hm
        cfg = json.load(open(os.path.join(nca, "results", "hgrid2", "params.json")))["cfg"]
        model = hm.Boid2(sn.World(), hm.Cfg(**cfg))
        t0 = time.time()
        for seed in seeds:
            for k in KINDS:
                gen = sn.make_gen(seed)
                sw = sn.seed_swarm([targets[k]], model.world, gen)
                tt = time.time()
                for _ in range(240):
                    sw = model(sw, gen)
                ms = (time.time() - tt) * 1000 / 240
                x = sn.decode(sw, 0)
                res["python"].setdefault(k, {})[seed] = dict(cross=row(sn, x, targets, L),
                                                           n=int((sw.active[0] & sw.hatched[0]).sum()), ms=round(ms, 2))
                P, e = [], None
                alive = (sw.active[0] & sw.hatched[0]).clone(); el = sw.elem[0].clone()
                for _ in range(65):
                    P.append(sw.pos[0].clone()); sw = model(sw, gen)
                    alive &= (sw.active[0] & sw.hatched[0]) & (sw.elem[0] == el)
                m = swarm_feel.metrics(torch.stack(P)[:, alive], el[alive], k)
                res["feel"].setdefault("python_hgrid2", {}).setdefault(k, []).append(m)
        print(f"Python hgrid2 reference: {len(seeds) * 4} runs in {time.time() - t0:.0f}s")

    # ── report ──
    def own(table, k):
        return [table[k][s]["cross"][k] for s in seeds if s in table.get(k, {})]

    def strict(table, k):
        out = []
        for s in seeds:
            c = table[k][s]["cross"]; n = table[k][s]["n"]
            out.append(n >= sn.MIN_TEST_BODY and c[k] <= sn.MAX_TEST_LOSS and c[k] < min(v for q, v in c.items() if q != k))
        return out

    print("\nOWN-PLAN LOSS (swarm_loss sink, default LossCfg; bar 8)  per seed " + ",".join(map(str, seeds)))
    lines = []
    for label, table in (("python hgrid2", res["python"]), ("C# grid research", res["csharp"].get("research", {})),
                         ("C# grid game (1-domain)", res["csharp"].get("game", {}))):
        if not table:
            continue
        for k in KINDS:
            v = own(table, k)
            ok = strict(table, k)
            ns = [table[k][s]["n"] for s in seeds]
            ms = [table[k][s]["ms"] for s in seeds]
            line = (f"  {label:<24} {k:<6} " + " ".join(f"{x:5.2f}" for x in v) +
                    f"   mean {statistics.mean(v):5.2f}  pass {sum(ok)}/{len(ok)}  n~{round(statistics.mean(ns))}  {statistics.mean(ms):.2f} ms/step")
            print(line); lines.append(line)
    print("\nFEEL (swarm_feel.metrics, mean over plans and seeds)")
    keys = ["speed", "jerk_rel", "planar_frac", "coherence", "jitter", "phase", "stuck", "osc"]
    for label, d in res["feel"].items():
        allm = [m for ms in d.values() for m in ms if "speed" in m]
        if allm:
            print(f"  {label:<18} " + "  ".join(f"{q} {statistics.mean(m[q] for m in allm):.3f}" for q in keys))
    if a.out:
        def keyify(o):
            if isinstance(o, dict):
                return {str(k): keyify(v) for k, v in o.items()}
            if isinstance(o, list):
                return [keyify(v) for v in o]
            return o
        json.dump(keyify(res), open(a.out, "w"), indent=1)
        print(f"\nwrote {a.out}")


if __name__ == "__main__":
    main()
