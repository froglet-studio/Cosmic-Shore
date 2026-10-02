"""Read sibling sessions' species with the frozen probe, on THEIR OWN copy of common/ (they extend it).

    git archive origin/cece/eco-bestiary Tools/Ecology | tar -x -C <dir>
    python siblings.py --root <dir>/Tools/Ecology --kind bestiary   -> JSON on stdout
    python siblings.py --root <dir>/Tools/Ecology --kind substrate

The probe and the affect extractor are loaded from THIS directory by file path, so the sibling's own
`common` package wins on sys.path. The viewer pilot is placed 250 u from the population's centroid and
steered toward it (an encounter is guaranteed, as the substrate's quorum demo also does).
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import os
import sys

import numpy as np

ME = os.path.dirname(os.path.abspath(__file__))


def _load(name, path):
    spec = importlib.util.spec_from_file_location(name, path); m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m); return m


affect = _load("cs_affect", os.path.join(ME, "..", "common", "affect.py"))
probe_mod = _load("cs_probe", os.path.join(ME, "probe.py"))

VIEW = {"hover": 25.0, "cruise": 90.0}


FACE = os.environ.get("FACE_PILOT") == "1"     # experiment: publish a heading toward the pilot (motion unchanged)


TIMELINE = os.environ.get("TIMELINE") == "1"   # also read 8 s windows every 2 s (peak threat, not only the mean)
_WIN = []


def drive(ar, sp, pilot, seconds=40.0, dt=0.1, warm=2.0):
    rec = affect.AffectRecorder(dt, pilot_radius=pilot.radius)
    frames = []
    for k in range(int((seconds + warm) / dt)):
        sp.step(ar, dt); ar.step(dt)
        if k * dt >= warm:
            P = np.asarray(sp.agent_pos); V = np.asarray(sp.agent_vel)
            H = None
            if FACE and len(P):
                H = pilot.pos - P; H = H / np.maximum(np.linalg.norm(H, axis=1, keepdims=True), 1e-9)
            rec.observe(pilot.pos, pilot.vel, P, V, getattr(sp, "agent_size", None), None, H)
            if TIMELINE:
                sz = getattr(sp, "agent_size", None)
                frames.append((pilot.pos.copy(), pilot.vel.copy(), P.copy(), V.copy(), None if sz is None else np.array(sz, float), None, H))
    f = rec.features()
    if TIMELINE:
        pr = probe_mod.EmotionProbe.load(os.environ.get("EMOTION_PROBE")); w, st = int(8.0 / dt), int(2.0 / dt); peaks = []
        for a in range(0, max(1, len(frames) - w + 1), st):
            r2 = affect.AffectRecorder(dt, pilot_radius=pilot.radius)
            for fr in frames[a:a + w]:
                r2.observe(*fr)
            peaks.append(pr.score(r2.features())["p"])
        f = dict(f); f["_timeline"] = peaks
    return f


def place(pilot, centroid):
    pilot.pos = centroid + np.array([250.0, 0.0, 0.0]); pilot.goal = centroid.copy()


def bestiary(root, seeds):
    sys.path.insert(0, root); sys.path.insert(0, os.path.join(root, "bestiary"))
    from common.arena import Pilot
    import importlib
    run = importlib.import_module("run")
    out = {}
    for key in run.SPECIES:
        try:
            mod = importlib.import_module(f"species.{key}")
        except Exception:
            continue
        intended = getattr(mod, "EMOTION", "")
        for v, spd in VIEW.items():
            for s in seeds:
                ar = run.make_arena(s); pl = ar.add_pilot(Pilot("wander", speed=spd, name=v))
                sp = mod.make(ar)
                c = np.asarray(sp.agent_pos).mean(0) if len(sp.agent_pos) else np.zeros(3)
                place(pl, c)
                f = drive(ar, sp, pl)
                out.setdefault(f"bestiary/{key}", dict(intended=intended, runs=[]))["runs"].append(dict(viewer=v, seed=s, f=f))
    return out


def substrate(root, seeds):
    sys.path.insert(0, root)
    from common.arena import Arena, Pilot
    from substrate.core import Substrate
    from substrate import species as S
    conds = {
        "grazer": (lambda s: S.grazer(seed=s), dict(spread=80), 3000, "cute school (curious, never aggressive)"),
        "locust_sparse_fed": (lambda s: S.locust(n0=60, seed=s), dict(spread=500, init_hunger=0.1), 5000, "cute & shy (sparse, fed)"),
        "locust_dense_hungry": (lambda s: S.locust(n0=600, seed=s), dict(spread=70, init_hunger=0.85), 600, "terrifying (dense, hungry)"),
        "pack": (lambda s: S.pack(seed=s), dict(spread=60), 2500, "hunter (stalk ahead, then strike)"),
        "leviathan": (lambda s: S.leviathan(seed=s), dict(spread=80), 3000, "a creature assembled from a school"),
    }
    out = {}
    for name, (mk, kw, mass, intended) in conds.items():
        for v, spd in VIEW.items():
            for s in seeds:
                ar = Arena(seed=s); ar.scatter_mass(mass)
                pl = ar.add_pilot(Pilot("wander", speed=spd, name=v))
                try:
                    sp = Substrate(ar, mk(s), **kw)
                except Exception as e:
                    print("skip", name, e, file=sys.stderr); break
                place(pl, getattr(sp, "home", np.asarray(sp.agent_pos).mean(0)))
                f = drive(ar, sp, pl)
                out.setdefault(f"substrate/{name}", dict(intended=intended, runs=[]))["runs"].append(dict(viewer=v, seed=s, f=f))
    return out


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("--root", required=True); ap.add_argument("--kind", required=True)
    ap.add_argument("--seeds", default="301,302")
    a = ap.parse_args(); seeds = [int(x) for x in a.seeds.split(",")]
    res = (bestiary if a.kind == "bestiary" else substrate)(os.path.abspath(a.root), seeds)
    pr = probe_mod.EmotionProbe.load(os.environ.get("EMOTION_PROBE"))
    for k, r in res.items():
        for run_ in r["runs"]:
            tl = run_["f"].pop("_timeline", None)
            if tl:
                th = [q["menacing"] + q["terrifying"] for q in tl]
                run_["timeline"] = dict(peak_threat=round(max(th), 3), mean_threat=round(float(np.mean(th)), 3),
                                        peak={e: round(max(q[e] for q in tl), 3) for e in tl[0]},
                                        tops=[max(q, key=q.get) for q in tl])
            sc = pr.score(run_["f"]); run_["probe"] = dict(p=sc["p"], top=sc["top"], agreement=sc["agreement"], affect=sc["affect"],
                                                          why=pr.explain(run_["f"], sc["top"], 4))
    print(json.dumps(res))
