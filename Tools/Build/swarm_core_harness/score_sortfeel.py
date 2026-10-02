#!/usr/bin/env python3
"""Round 6: score the SHIPPED sort core with sortfeel's flat wells + wander and the fractional update
(SwarmSortCore.cs, Docs/SWARM_FAUNA.md §12) with the research's UNCHANGED scorers.

    python3 Tools/Build/swarm_core_harness/score_sortfeel.py [--seeds 7,23,41,101] [--samples 3]
            [--modes researchSort,researchSortFeel,researchSortFeelF8,...] [--ref sort,sortfeel,lite8]
            [--smooth researchSort,researchSortFeelF8] [--smooth-seed 7,23,41] [--out results.json]

  1. YARDSTICK: `run.sh yardstick` with sort modes (SortFeelHarness.Yardstick): per seed, plan and sample, grow
     240 steps (the own test) and, for every other element, regrow, swarm_eval.cull_to and run 240 (the 12
     switches). Scored exactly as score_combo.py does: swarm_nca.decode + swarm_loss (default LossCfg) and
     swarm_eval._passes, a test passing on a majority of its samples. research* modes are scored against the
     research plans, game* modes against the one-domain plans (score_grid.one_domain).
  2. REFERENCE (--ref): the Python models themselves through swarm_eval.evaluate at the same seeds -
     sort (results/sort/params.json), sortfeel (results/sortfeel/params.json), lite8 (lite_sortfeel params, frac 8,
     vec_look 1 - the held config).
  3. SMOOTHNESS (--smooth): swarm_smooth.py's events (the 4 standard switches + a strike on every plan) recorded
     step by step from the C# core (`run.sh smoothsort`) and reduced with swarm_smooth._track's formulas,
     transcribed below (lurch, teleport, jerk_rel, molt/birth burst, backtrack, rough, smoothness).
Needs numpy + torch (CPU) for the research code, which is read from origin/cece/gifted-curie-x2cpd0.
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
sys.path.insert(0, HERE)
import score_grid as sg  # noqa: E402

CS_KINDS = ["charge", "mass", "space", "time"]   # C# element order (= research element ids)
_POOL = None
_W = {}


def _winit(nca):
    sys.path.insert(0, nca)
    os.chdir(nca)
    import torch
    import swarm_nca as sn
    torch.set_num_threads(1)
    targets = sn.load_targets(os.path.join(nca, "results", "swarm_targets"))
    _W.update(sn=sn, torch=torch, T=targets, T1={k: sg.one_domain(sn, T) for k, T in targets.items()}, L=sn.LossCfg())


def _wloss(job):
    """(units, research?, [plan kinds], frames) -> {kind: sink loss}. Runs in a worker."""
    units, research, kinds, frames = job
    sn, torch = _W["sn"], _W["torch"]
    T = _W["T"] if research else _W["T1"]
    x = sn.decode(sg.build_swarm(sn, torch, units), 0)
    return {k: float(sn.swarm_loss(x, T[k], _W["L"], frames=frames)[1]["sink"]) for k in kinds}


def _pool(nca, jobs):
    import multiprocessing as mp
    return mp.get_context("fork").Pool(jobs, initializer=_winit, initargs=(nca,))


def _density_env(a):
    """Round 7 (Docs/SWARM_FAUNA.md §14): --density m runs the C# core on the m-times-upsampled plans
    (SwarmPlanData.Upsample, the game's PlanDensity) and lets a body lay m times as fast so it grows in
    the yardstick's 240 steps (SWARM_SORT_GAME LayMax = 5 m, the authored ratio)."""
    env = dict(os.environ)
    if a.density > 1:
        env["SWARM_DENSITY"] = str(a.density)
        extra = f"LayMax={5 * a.density}"
        env["SWARM_SORT_GAME"] = extra + ("," + env["SWARM_SORT_GAME"] if env.get("SWARM_SORT_GAME") else "")
    return env


def _shrink(dump, m):
    """An upsampled body is the plan scaled by m^(1/3) (constant density). Scale every exported position
    back by 1/m^(1/3) so the UNCHANGED swarm_loss compares it with the research's own target: the
    Sinkhorn term is a divergence between NORMALISED distributions, so the extra headcount is not a
    penalty - the question it answers is exactly 'is this the creature's shape'. The feel window is
    shrunk with it, so the feel metrics are in plan units too."""
    if m <= 1:
        return
    k = 1.0 / m ** (1.0 / 3.0)
    for r in dump.get("runs", []):
        for u in r.get("units", []):
            u[0] *= k; u[1] *= k; u[2] *= k
        if "win" in r:
            r["win"] = [[[c * k for c in p] for p in step] for step in r["win"]]


def _shrink_events(events, m):
    """_shrink for swarm_smooth's events: snapshots are unit lists, traces are frames of flat "p" positions.
    Every distance swarm_smooth reads (teleport is an ABSOLUTE step bound) is then in plan units."""
    if m <= 1:
        return
    k = 1.0 / m ** (1.0 / 3.0)
    for ev in events:
        for key in ("snap", "snaps", "tail"):
            for units in ev.get(key, []):
                for u in units:
                    u[0] *= k; u[1] *= k; u[2] *= k
        for key in ("ref", "trace"):
            for f in ev.get(key, []):
                f["p"] = [c * k for c in f["p"]]


def yardstick(a, sn, se, torch, targets, targets1, L, swarm_feel, res):
    exp = os.path.join(tempfile.mkdtemp(prefix="swarm_yard6_"), "yard.json")
    t0 = time.time()
    subprocess.run(["bash", os.path.join(HERE, "run.sh"), "yardstick", sg.PLANS, exp, a.seeds, str(a.samples), a.modes],
                   check=True, env=_density_env(a))
    dump = json.load(open(exp))
    _shrink(dump, a.density)
    print(f"\nC# yardstick: {len(dump['runs'])} records in {time.time() - t0:.0f}s; self-inflicted deaths {dump['selfDeaths']}")
    res["selfDeaths"] = dump["selfDeaths"]
    tests = {}
    live = [r for r in dump["runs"] if not r.get("na")]
    t1 = time.time()
    rows = _POOL.map(_wloss, [(r["units"], r["mode"].startswith("research"), list(sn.KINDS), None) for r in live], chunksize=4)
    print(f"  scored {len(live)} records x 4 plans in {time.time() - t1:.0f}s", flush=True)
    rowof = {id(r): row for r, row in zip(live, rows)}
    for r in dump["runs"]:
        key = (r["mode"], r["seed"], r["kind"], r["want"])
        if r.get("na"):
            tests.setdefault(key, [])
            continue
        row = {k: round(v, 2) for k, v in rowof[id(r)].items()}
        n = sum(1 for u in r["units"] if u[5] > 0.5)
        tests.setdefault(key, []).append(dict(ok=se._passes(row, r["want"], n), row=row, n=n))
        if r["tag"] == "own" and "win" in r:
            P = torch.tensor(r["win"], dtype=torch.float32)
            if P.ndim == 3 and P.shape[1] >= 8:
                m = swarm_feel.metrics(P, torch.tensor(r["winElem"]), r["kind"])   # the one-domain plan has the same geometry
                res["feel"].setdefault(r["mode"], {}).setdefault(r["kind"], []).append(m)
    for (mode, seed, k, want), rs in tests.items():
        d = res["csharp"].setdefault(mode, {}).setdefault(str(seed), {"tests": {}})
        if not rs:
            d["tests"][f"{k}->{want}"] = dict(na=True)
            continue
        oks = [q["ok"] for q in rs]
        d["tests"][f"{k}->{want}"] = dict(ok=sum(oks) * 2 > len(rs), rate=round(sum(oks) / len(rs), 2),
                                         loss=round(statistics.mean(q["row"][want] for q in rs), 2),
                                         n=round(statistics.mean(q["n"] for q in rs)))


def reference(a, nca, se, res):
    specs = {
        "sort": "sort:" + os.path.join(nca, "results", "sort", "params.json"),
        "sortfeel": "sortfeel:" + os.path.join(nca, "results", "sortfeel", "params.json"),
        "lite8": "lite_sortfeel:" + os.path.join(nca, "results", "lite_sortfeel", "params.json") + "?frac=8,vec_look=1",
    }
    import scorecard as scd
    for label in [x for x in a.ref.split(",") if x]:
        model = se.load_model(specs[label])
        for s in [int(x) for x in a.seeds.split(",")]:
            t0 = time.time()
            ev = se.evaluate(model, seed=s, samples=a.samples, full=True, log=lambda *_: None)
            tt = {}
            for k in ev["own"]:
                o = ev["own"][k]; tt[f"{k}->{k}"] = dict(ok=o["ok"], rate=o["rate"], loss=o["cross"][k], n=o["n"])
            for key, v in ev["switch"].items():
                k, to = key.split("->")
                tt[f"{k}->{to}"] = dict(ok=v["ok"], rate=v["rate"], loss=v["cross"][to], n=v["n"])
            for key in ev["na"]:
                tt[key] = dict(na=True)
            res["python"].setdefault(label, {})[str(s)] = {"tests": tt}
            print(f"  Python {label} seed {s}: {ev['passed']}/{ev['feasible']} ({time.time() - t0:.0f}s)", flush=True)
        lc = scd.lossless_and_cost(model, seed=7)
        res["python"][label]["lossless"] = lc
        print(f"  Python {label}: deaths {lc['deaths']}  {lc['ms_per_step']} ms/step @ {lc['grown_n']}", flush=True)


def smoothness(a, sn, torch, targets, targets1, L, res):
    import swarm_smooth as ss
    pairs = ",".join(f"{CS_KINDS.index(k)}:{sn.SWITCH_TO[k]}" for k in sn.KINDS)
    events = []
    for seed in [int(x) for x in str(a.smooth_seed).split(",")]:
        exp = os.path.join(tempfile.mkdtemp(prefix="swarm_smooth6_"), "smooth.json")
        t0 = time.time()
        subprocess.run(["bash", os.path.join(HERE, "run.sh"), "smoothsort", sg.PLANS, exp, str(seed), a.smooth, pairs],
                       check=True, env=_density_env(a))
        evs = json.load(open(exp))["events"]
        _shrink_events(evs, a.density)
        for ev in evs:
            ev["seed"] = seed
        events += evs
        print(f"\nC# smooth export, seed {seed}: {len(evs)} events in {time.time() - t0:.0f}s")
    dump = {"events": events}

    def frames(tr):
        P = torch.tensor([f["p"] for f in tr], dtype=torch.float32).view(len(tr), -1, 3)
        A = torch.tensor([[c == "1" for c in f["a"]] for f in tr])
        H = torch.tensor([[c == "1" for c in f["h"]] for f in tr])
        E = torch.tensor([[ord(c) - 48 for c in f["e"]] for f in tr])
        return P, A, H, E

    for ev in dump["events"]:
        if ev.get("na"):
            continue
        T = targets if ev["mode"].startswith("research") else targets1
        goal = ev["goal"]
        research = ev["mode"].startswith("research")
        snaps = [ev["snap"][0]] + ev["snaps"] + ev["tail"]
        vals = _POOL.map(_wloss, [(u, research, [goal], ss.LOSS_FRAMES) for u in snaps])
        vals = [v[goal] for v in vals]
        nsn = 1 + len(ev["snaps"])
        # reference speed: swarm_smooth._grown (median over 16 steps of the p95 step of members alive at both ends)
        P, A, _, _ = frames(ev["ref"])
        sp_ = []
        for t in range(1, len(P)):
            b = A[t] & A[t - 1]
            if int(b.sum()) >= 4:
                sp_.append(float(torch.quantile((P[t][b] - P[t - 1][b]).norm(dim=-1), 0.95)))
        ref = float(torch.tensor(sp_).median()) if sp_ else 1e-3
        losses = vals[:nsn]
        lt = torch.tensor([losses[-1]] + vals[nsn:])
        noise_rel = float((lt[1:] - lt[:-1]).clamp(min=0).sum() / lt[1:].sum().clamp(min=1e-6))
        P, A, H, E = frames(ev["trace"])
        # ── swarm_smooth._track, transcribed
        n_t = torch.maximum(A.sum(1).float(), A[-1].sum().float()).clamp(min=1)
        both = A[1:] & A[:-1]
        V = (P[1:] - P[:-1]).norm(dim=-1)
        p95 = torch.stack([torch.quantile(V[t][both[t]], 0.95) if int(both[t].sum()) >= 4 else torch.tensor(0.0) for t in range(len(V))])
        p95v = p95[p95 > 0]
        med = float(p95v.median()) if len(p95v) else 1e-4
        lurch = float(p95.max()) / max(med, 1e-4)
        travel = med / max(ref, 1e-4)
        teleport = float(V[both].max()) / 2.0 if int(both.sum()) else 0.0
        a4 = A[3:] & A[2:-1] & A[1:-2] & A[:-3]
        J = (P[3:] - 3 * P[2:-1] + 3 * P[1:-2] - P[:-3]).norm(dim=-1)
        jm = float(J[a4].mean()) if int(a4.sum()) else 0.0
        vm = float(V[both].mean()) if int(both.sum()) else 1e-6
        jerk_rel = jm / max(vm, 1e-6)
        molt = ((E[1:] != E[:-1]) & both).sum(1).float() / n_t[1:]
        born = (H[1:] & ~H[:-1] & A[1:]).sum(1).float() / n_t[1:]
        win = lambda x: float(torch.stack([x[i:i + 8].sum() for i in range(0, max(1, len(x) - 7))]).max())
        Ls = torch.tensor(losses)
        drop = float(Ls[0] - Ls[-1])
        climb = float((Ls[1:] - Ls[:-1]).clamp(min=0).sum())
        backtrack = max(0.0, climb - noise_rel * float(Ls[1:].sum())) / max(float(Ls[0]), 1.0)
        r = dict(loss_start=round(float(Ls[0]), 2), loss_end=round(float(Ls[-1]), 2), backtrack=round(backtrack, 3),
                 travel=round(travel, 2), lurch=round(lurch, 2), teleport=round(teleport, 3), jerk_rel=round(jerk_rel, 3),
                 molt_burst=round(win(molt), 3), birth_burst=round(win(born), 3), deaths=ev["deaths"], n_end=int(A[-1].sum()))
        r["rough"] = round(4 * r["backtrack"] + max(0.0, r["lurch"] / ss.LURCH_OK - 1) + 2 * max(0.0, r["teleport"] / ss.TELEPORT_OK - 1)
                           + max(0.0, r["jerk_rel"] - ss.JERK_OK)
                           + 10 * max(0.0, r["molt_burst"] - ss.BURST_OK) + 10 * max(0.0, r["birth_burst"] - ss.BIRTH_OK), 3)
        res["smooth"].setdefault(ev["mode"], {"events": {}})["events"][f"seed {ev['seed']} {ev['event']}"] = r
    for mode, d in res["smooth"].items():
        evs = list(d["events"].values())
        keys = ["backtrack", "lurch", "teleport", "jerk_rel", "molt_burst", "birth_burst", "rough"]
        d["mean"] = {k: round(statistics.mean(v[k] for v in evs), 3) for k in keys}
        d["worst"] = {k: round(max(v[k] for v in evs), 3) for k in keys}
        d["deaths"] = sum(v["deaths"] for v in evs)
        # swarm_smooth's smoothness is per seed (1 / (1 + mean rough over that seed's 8 events)); report each
        per = {}
        for name, v in d["events"].items():
            per.setdefault(name.split()[1], []).append(v["rough"])
        d["per_seed"] = {sd: round(1.0 / (1.0 + statistics.mean(r)), 3) for sd, r in per.items()}
        d["smoothness"] = round(statistics.mean(d["per_seed"].values()), 3)


def report(res, kinds):
    def summarise(table):
        out = {}
        for s, d in table.items():
            if not isinstance(d, dict) or "tests" not in d:
                continue
            t = d["tests"]
            feas = [v for v in t.values() if not v.get("na")]
            own = [t.get(f"{k}->{k}", {}) for k in kinds]
            out[s] = dict(passed=sum(v["ok"] for v in feas), feasible=len(feas),
                          own=[v.get("loss") for v in own], own_ok=sum(bool(v.get("ok")) for v in own),
                          fails=sorted(f"{k}({v['loss']})" for k, v in t.items() if not v.get("na") and not v["ok"]))
        return out

    print("\nYARDSTICK (swarm_eval: 4 own + 12 switches, majority of samples, loss-8 bar); own = " + " / ".join(kinds))
    rows = [("Python " + k, v) for k, v in res["python"].items()] + [("C# " + k, v) for k, v in res["csharp"].items()]
    for label, table in rows:
        tot = [0, 0]
        for s, v in summarise(table).items():
            own = " / ".join(f"{x:.2f}" if x is not None else "-" for x in v["own"])
            tot[0] += v["passed"]; tot[1] += v["feasible"]
            print(f"  {label:<24} seed {s:>3}: {v['passed']:2d}/{v['feasible']}  own {own}  fails {v['fails']}")
        if tot[1]:
            print(f"  {label:<24} TOTAL     {tot[0]}/{tot[1]}")
    if res["feel"]:
        print("\nFEEL (swarm_feel.metrics over each grown own-plan body's 64-step window, mean)")
        import swarm_feel
        keys = ["speed", "jerk_rel", "planar_frac", "coherence", "jitter", "phase", "stuck", "osc"]
        for label, d in res["feel"].items():
            allm = [m for ms in d.values() for m in ms if "speed" in m]
            if not allm:
                continue
            mean = {q: statistics.mean(m[q] for m in allm) for q in keys}
            # score_sort.py's band test: planar excess = the worst plan's mean planar_frac over its plan's own
            pe = max(max(0.0, statistics.mean(m["planar_frac"] for m in ms if "speed" in m) - ms[0].get("planar_plan", 0.0))
                     for ms in d.values() if any("speed" in m for m in ms))
            b = swarm_feel.BAND
            checks = dict(jerk_rel=b["jerk_rel"][0] <= mean["jerk_rel"] <= b["jerk_rel"][1], osc=mean["osc"] <= b["osc_max"],
                          stuck=mean["stuck"] <= b["stuck_max"], planar=pe <= b["planar_excess_max"])
            verdict = "ORGANIC" if all(checks.values()) else "out (" + ", ".join(k for k, v in checks.items() if not v) + ")"
            res.setdefault("organic", {})[label] = dict(checks=checks, planar_excess=round(pe, 3), mean=mean)
            print(f"  {label:<24} " + "  ".join(f"{q} {mean[q]:.3f}" for q in keys) + f"  planar_excess {pe:.3f} -> {verdict}")
    if res["smooth"]:
        print("\nSMOOTHNESS (swarm_smooth's events + formulas on C# trajectories)")
        for mode, d in res["smooth"].items():
            print(f"  {mode:<24} smoothness {d['smoothness']:.3f} (per seed {d['per_seed']})  deaths {d['deaths']}  mean {d['mean']}")
            print(f"  {'':<24} worst {d['worst']}")
            for name, r in d["events"].items():
                print(f"      {name:<22} {r}")
    if "selfDeaths" in res:
        print(f"\nLOSSLESS: self-inflicted deaths over every C# yardstick run: {res['selfDeaths']}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seeds", default="7,23,41,101")
    ap.add_argument("--samples", type=int, default=3)
    ap.add_argument("--modes", default="researchSort,researchSortFeel,researchSortFeelF8,researchSortFeelD0F8,gameSort,gameSortFeelF8")
    ap.add_argument("--ref", default="")
    ap.add_argument("--smooth", default="")
    ap.add_argument("--smooth-seed", default="7", help="one seed or a comma list (swarm_smooth is one seed; average 2-3)")
    ap.add_argument("--nca", default="")
    ap.add_argument("--out", default="")
    ap.add_argument("--jobs", type=int, default=4)
    ap.add_argument("--density", type=int, default=1, help="round 7: run on m-times-upsampled plans (SWARM_DENSITY)")
    a = ap.parse_args()
    nca = sg.research_dir(a.nca)
    sys.path.insert(0, nca)
    os.chdir(nca)
    import torch
    import swarm_nca as sn
    import swarm_eval as se
    import swarm_feel
    # AFTER the imports: swarm_nca sets torch.set_num_threads(4) at import, and on a busy 4-core box that
    # oversubscribes OpenMP so badly that one swarm_loss takes ~20 s instead of ~0.1 s (measured). The
    # losses are spread over worker processes instead (--jobs), each single-threaded.
    torch.set_num_threads(1)
    global _POOL
    _POOL = _pool(nca, a.jobs)
    targets = sn.load_targets(os.path.join(nca, "results", "swarm_targets"))
    targets1 = {k: sg.one_domain(sn, T) for k, T in targets.items()}
    L = sn.LossCfg()
    res = {"seeds": a.seeds, "samples": a.samples, "csharp": {}, "python": {}, "feel": {}, "smooth": {}}
    if a.modes:
        yardstick(a, sn, se, torch, targets, targets1, L, swarm_feel, res)
    if a.smooth:
        smoothness(a, sn, torch, targets, targets1, L, res)
    if a.ref:
        reference(a, nca, se, res)
    report(res, list(sn.KINDS))
    if a.out:
        json.dump(res, open(a.out, "w"), indent=1)
        print(f"\nwrote {a.out}")


if __name__ == "__main__":
    main()
