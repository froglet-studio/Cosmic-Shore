"""Fortress repair-after-cut experiment + the standard scorecard.
python Tools/Ecology/builders/run_fortress.py"""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from common.arena import Arena, Pilot, Recorder
from common.scorecard import Probe
from builders.harness import ram, evaluate, save_card, OUT, TRAIL_EVERY
from builders.fortress import Fortress

CUTS = (150.0, 200.0, 250.0)          # three cutting passes after a build-up phase


def cut_run(seed, mend="both", minutes=5.0, dt=0.1, record=False, n=48, **kw):
    ar = Arena(seed=seed); ar.scatter_mass(1500); ar.struct_owner = {}
    sp = Fortress(ar, seed=seed, mend=mend, n=n, **kw)
    A = sp.lat.anchor
    far = [A + np.array(v, float) for v in ((400, 0, 0), (0, 400, 0), (-400, 0, 0), (0, -400, 0))]
    far = [f * min(1.0, 1000 / np.linalg.norm(f)) for f in far]
    p = Pilot.circuit(far, speed=140.0, name="cutter"); p.trail_every = TRAIL_EVERY
    ar.add_pilot(p); p.pos = far[0].copy()
    rng = np.random.default_rng(seed + 77)
    rec = Recorder(every=3) if record else None; hud = []
    plan = []
    for tc in CUTS:
        d = rng.normal(size=3); d /= np.linalg.norm(d)
        plan.append((tc - 3.0, [A - d * 420, A + d * 420]))   # line up 420 u out, fly straight through the core
    steps = int(minutes * 60 / dt)
    for _ in range(steps):
        for (ts, wps) in plan:
            if abs(ar.t - ts) < dt / 2:
                p.waypoints = [w for w in wps] + far; p._wp = 0; p.pos = wps[0].copy()
                v = wps[1] - wps[0]; p.vel = v / np.linalg.norm(v) * p.speed; p.ram = True
            if abs(ar.t - (ts + 9.0)) < dt / 2:
                p.ram = False
        sp.step(ar, dt); ram(ar, [sp], dt); ar.step(dt)
        if rec:
            rec.frame(ar, [sp])
            if rec.k % rec.every == 0:
                hud.append(f"built {sp.lat.n_built()}  open wounds {len(sp.open_breach)}  mended {len(sp.repairs)}")
    res = dict(seed=seed, mend=mend, built=sp.lat.n_built(), audit=round(ar.audit(), 6),
               cuts=[sp.repair_stats(tc - 3.0) for tc in CUTS], repair_trail=sp.repair_trail,
               repaired=len(sp.repairs), moves_per_s=round(ar.moves / (minutes * 60), 1),
               trail_frac=sp.metrics(ar, minutes)["trail_frac"], hits=len(ar.log))
    if rec:
        os.makedirs(OUT, exist_ok=True)
        path = os.path.join(OUT, f"fortress_cut_{mend}_{seed}.json")
        rec.save(path, dict(label=f"fortress cut test, mend={mend} (seed {seed})", note=sp.note, hud=hud))
        res["recording"] = path
    return res, sp


def summarise(rows):
    t50 = [c["t50"] for r in rows for c in r["cuts"] if c.get("t50") is not None]
    t90 = [c["t90"] for r in rows for c in r["cuts"] if c.get("t90") is not None]
    ncut = sum(c.get("cut_sites", 0) for r in rows for c in r["cuts"])
    nref = sum(c.get("refilled", 0) for r in rows for c in r["cuts"])
    healed = sum(1 for r in rows for c in r["cuts"] if c.get("t90") is not None)
    tot = sum(1 for r in rows for c in r["cuts"] if c.get("cut_sites"))
    return dict(cuts_healed_90=f"{healed}/{tot}", refill_frac=round(nref / max(ncut, 1), 3),
                t50_median=float(np.median(t50)) if t50 else None, t90_median=float(np.median(t90)) if t90 else None,
                sites_per_cut=round(ncut / max(tot, 1), 1),
                repair_from_trail=round(sum(r["repair_trail"] for r in rows) / max(sum(r["repaired"] for r in rows), 1), 3))


if __name__ == "__main__":
    out = {}
    recs = []
    for mend in (sys.argv[1:] or ("none", "gap", "alarm", "both")):
        rows = []
        for sd in (7, 23, 41):
            r, _ = cut_run(sd, mend=mend, record=(sd == 7 and mend in ("none", "both")))
            if "recording" in r: recs.append(r.pop("recording"))
            rows.append(r)
            print(mend, sd, json.dumps({k: r[k] for k in ("built", "audit", "cuts", "repair_trail", "moves_per_s")}), flush=True)
        out[mend] = dict(summary=summarise(rows), runs=rows)
        print(mend, out[mend]["summary"], flush=True)
    os.makedirs(os.path.join(os.path.dirname(__file__), "results"), exist_ok=True)
    json.dump(out, open(os.path.join(os.path.dirname(__file__), "results", "fortress_repair.json"), "w"), indent=1)
    if recs:
        from common.viewer import build
        build(os.path.join(OUT, "fortress_cut.html"), recs, "Fortress: repair after cut")
