"""Measure a co-evolved predator/prey run (arms_train.py). Writes Tools/NCA/results/arms/.

  matrix    the CROSS-GENERATION PLAY MATRIX: predator snapshot i vs prey snapshot j, catches per minute per
            encounter (8 predators, 120 prey, 30 s, the same 8 seeds in every cell). Rows newer than columns
            winning = real progress; a rock-paper-scissors pattern = cycling.
  curve     catch rate over training (from the training log) and the diagonal of the matrix.
  behave    named behaviours with numbers (arms_metrics.py) for chosen generation pairs, 3 seeds x 60 s.
  feel      swarm_feel's organic band per species.
  eco       the OPEN economy for 5 minutes (births funded by mass eaten, starvation, conserved mass): collapse?
  perf      ms/step for one realistic encounter (8 predators, 120 prey), one thread.
  player    a scripted vessel (Tools/Ecology/common/arena.py Pilot) flies through: do the predators turn on it,
            do the prey hide behind it?
  record    a viewer recording (Tools/Ecology/common/viewer.py data contract) + GIFs.

    python Tools/NCA/arms_eval.py --run runs/arms_a1 all
    python Tools/NCA/arms_eval.py --run runs/arms_a1 matrix --every 200
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import time

os.environ.setdefault("OMP_NUM_THREADS", "1")
os.environ.setdefault("OPENBLAS_NUM_THREADS", "1")

import numpy as np
import multiprocessing as mp

import arms_sim as A
import arms_metrics as M

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "results", "arms")
sys.path.insert(0, os.path.join(HERE, "..", "Ecology"))


def load_snaps(run):
    d = os.path.join(run, "snaps")
    fs = sorted(f for f in os.listdir(d) if f.endswith(".npz"))
    return {int(f[1:6]): np.load(os.path.join(d, f)) for f in fs}


def pick_gens(snaps, every, last=None):
    gs = sorted(snaps)
    if last is not None:
        gs = [g for g in gs if g <= last]
    sel = [g for g in gs if g % every == 0]
    if gs[-1] not in sel:
        sel.append(gs[-1])
    return sel


# ------------------------------------------------------------------------------------------------ matrix ---

def _cell(args):
    cfg, thq, thp, seeds, steps = args
    B = len(seeds)
    st = A.run(cfg, np.repeat(thq[None], B, 0).astype(np.float32), np.repeat(thp[None], B, 0).astype(np.float32),
               B, seeds, steps, rng=np.random.default_rng(int(seeds[0]) + 5))
    return st.catches.sum(1), (st.caught_t[:, :cfg.n_prey] >= 0).mean(1)


def matrix(run, every, seeds=8, secs=30.0, workers=4, last=None):
    snaps = load_snaps(run)
    gens = pick_gens(snaps, every, last)
    cfg = A.Cfg()
    steps = int(secs / cfg.dt)
    sd = np.arange(1, seeds + 1) * 7919 + 100000          # held-out seeds (training draws random 31-bit seeds)
    jobs = [(cfg, snaps[j]["thq"], snaps[i]["thp"], sd, steps) for i in gens for j in gens]
    t0 = time.time()
    with mp.Pool(workers) as pool:
        res = pool.map(_cell, jobs)
    n = len(gens)
    C = np.array([r[0].mean() for r in res]).reshape(n, n) / (secs / 60)
    S = np.array([r[0].std() / np.sqrt(len(r[0])) for r in res]).reshape(n, n) / (secs / 60)
    # progress statistics: does a newer predator beat older prey more than older predators did, and vice versa?
    newer_pred = [C[i, j] - C[j, j] for i in range(n) for j in range(n) if i > j]      # newer pred vs prey j, minus prey j's own-era pred
    newer_prey = [C[i, i] - C[i, j] for i in range(n) for j in range(n) if j > i]      # pred i's own-era prey minus newer prey j
    # cycling check: for each prey column, is the best predator the newest? intransitive triples?
    intrans = 0; triples = 0
    for a in range(n):
        for b in range(a + 1, n):
            for c in range(b + 1, n):
                triples += 1
                # predator-vs-prey dominance between generation pairs: x beats y if C[x,y] > C[y,x]
                ab, bc, ca = C[a, b] - C[b, a], C[b, c] - C[c, b], C[c, a] - C[a, c]
                if (ab > 0 and bc > 0 and ca > 0) or (ab < 0 and bc < 0 and ca < 0):
                    intrans += 1
    out = dict(gens=gens, catch_per_min=np.round(C, 2).tolist(), sem=np.round(S, 2).tolist(),
               rows="predator generation", cols="prey generation", seeds=sd.tolist(), secs=secs,
               newer_pred_gain=round(float(np.mean(newer_pred)), 2) if newer_pred else None,
               newer_pred_gain_pos_share=round(float(np.mean(np.array(newer_pred) > 0)), 3) if newer_pred else None,
               newer_prey_gain=round(float(np.mean(newer_prey)), 2) if newer_prey else None,
               newer_prey_gain_pos_share=round(float(np.mean(np.array(newer_prey) > 0)), 3) if newer_prey else None,
               intransitive_triples=intrans, triples=triples, seconds=round(time.time() - t0, 1),
               note="newer_pred_gain: mean over i>j of C[i,j]-C[j,j] (a newer predator vs an old prey, relative to "
                    "that prey's contemporaries); newer_prey_gain: mean over j>i of C[i,i]-C[i,j] (how much a newer prey "
                    "lowers an old predator's catch). Both positive = monotone progress on both sides.")
    return out


# --------------------------------------------------------------------------------------------- recording ---

def record(cfg, thq, thp, seed, secs, vessel=None, ghost=True):
    """One encounter, every step stored. vessel: an Ecology Pilot driven by an Arena of radius R_cell
    (Tools/Ecology/common/arena.py); ghost=True puts it in both species' perception (prey: as a predator;
    predators: as prey). It is always a solid obstacle."""
    st = A.reset(cfg, 1, seed)
    rng = np.random.default_rng(seed + 11)
    steps = int(secs / cfg.dt)
    keys = dict(qp=[], qv=[], qa=[], pp=[], pv=[], pa=[], pb=[], vp=[], pt=[], fd=[])
    arena = None
    if vessel is not None:
        from common.arena import Arena
        arena = Arena(seed=seed, R=cfg.R_cell * 0.95, nucleus=0.0, grid_h=40.0)
        arena.add_pilot(vessel)
    thq = thq[None].astype(np.float32); thp = thp[None].astype(np.float32)
    for _ in range(steps):
        extra = None
        if arena is not None:
            pl = arena.pilots[0]
            arena.targets = list(st.pos[1][0][st.alive[1][0]])     # a hunter pilot chases predators
            arena.threats = list(st.pos[1][0][st.alive[1][0]])
            vp = pl.pos[None, None].astype(np.float32); vv = pl.vel[None, None].astype(np.float32)
            extra = dict(pos=pl.pos[None].astype(np.float32), radius=pl.radius + 4.0)
            if ghost:
                extra.update(ghost_pred=(vp, vv), ghost_prey=(vp, vv))
            keys["vp"].append(pl.pos.copy())
        A.step(cfg, st, thq, thp, extra=extra, rng=rng, record_events=True)
        if arena is not None:
            arena.step(cfg.dt)
            keys["pt"].append(st.last_aux["pred_target"][0].copy())
        for k, v in (("qp", st.pos[0]), ("qv", st.vel[0]), ("qa", st.alive[0]), ("pp", st.pos[1]), ("pv", st.vel[1]),
                     ("pa", st.alive[1]), ("pb", st.burst), ("fd", st.food)):
            keys[k].append(v[0].copy())
    rec = {k: np.array(v) for k, v in keys.items() if len(v)}
    rec["events"] = [(e[1], e[2], e[3], e[4], e[5], e[6]) for e in st.events]
    rec["dt"] = cfg.dt; rec["R_cell"] = cfg.R_cell
    rec["food"] = st.food0[0]
    rec["food_final"] = st.food[0]
    rec["attempts"] = float(st.attempts.sum()); rec["catches"] = float(st.catches.sum())
    rec["grazed"] = float(st.grazed[0].sum())
    return rec


def behave(cfg, thq, thp, seeds=(11, 12, 13), secs=60.0):
    """named behaviours + feel averaged over seeds."""
    per = []
    feels = dict(prey=[], pred=[])
    for s in seeds:
        rec = record(cfg, thq, thp, s, secs)
        b = dict(prey=M.prey_metrics(rec), pred=M.pred_metrics(rec, cfg),
                 catch_per_min=round(rec["catches"] / (secs / 60), 2),
                 confusion_fail_share=round(1 - rec["catches"] / max(rec["attempts"], 1), 3),
                 grazed_per_prey=round(rec["grazed"] / cfg.n_prey, 3))
        per.append(b)
        w0 = int(10 / cfg.dt)       # feel window: 20 s after a 10 s warm-up, on agents alive throughout
        w1 = w0 + int(20 / cfg.dt)
        feels["prey"].append(M.feel(rec["qp"][w0:w1], rec["qa"][w0:w1], cfg.prey_v * cfg.dt))
        feels["pred"].append(M.feel(rec["pp"][w0:w1], rec["pa"][w0:w1], cfg.pred_v * cfg.dt))
    return dict(seeds=list(seeds), secs=secs, mean=_mean_tree(per), per_seed=per), \
        {k: _mean_tree([f for f in v if "jerk_rel" in f]) for k, v in feels.items()}


def _mean_tree(items):
    """mean of nested dicts of numbers (None-safe); booleans -> share True."""
    if not items:
        return None
    if isinstance(items[0], dict):
        keys = items[0].keys()
        return {k: _mean_tree([it.get(k) for it in items if isinstance(it, dict)]) for k in keys}
    vals = [v for v in items if v is not None]
    if not vals:
        return None
    if isinstance(vals[0], bool):
        return round(float(np.mean(vals)), 3)
    if isinstance(vals[0], (int, float, np.floating, np.integer)):
        return round(float(np.mean(vals)), 3)
    return vals[0]


def organic(f):
    if not f or f.get("jerk_rel") is None:
        return None
    ok = dict(jerk_rel=M.BAND["jerk_rel"][0] <= f["jerk_rel"] <= M.BAND["jerk_rel"][1], osc=f["osc"] <= M.BAND["osc_max"],
              stuck=f["stuck"] <= M.BAND["stuck_max"])
    return dict(ok=all(ok.values()), checks=ok)


# ------------------------------------------------------------------------------------------------- economy --

def eco(thq, thp, seeds=(21, 22, 23, 24), minutes=5.0, sample_s=5.0):
    cfg = A.Cfg(eco=True, cap_prey=480, cap_pred=48)
    B = len(seeds)
    st = A.reset(cfg, B, np.array(seeds))
    rng = np.random.default_rng(5)
    TQ = np.repeat(thq[None], B, 0).astype(np.float32); TP = np.repeat(thp[None], B, 0).astype(np.float32)
    m0 = A.total_mass(st)
    series = []
    steps = int(minutes * 60 / cfg.dt); every = int(sample_s / cfg.dt)
    for k in range(steps):
        A.step(cfg, st, TQ, TP, rng=rng)
        if (k + 1) % every == 0:
            series.append(dict(t=round(st.t, 1), prey=st.alive[0].sum(1).tolist(), pred=st.alive[1].sum(1).tolist(),
                               food=np.round(st.food.reshape(B, -1).sum(1), 2).tolist(), pool=np.round(st.pool, 2).tolist()))
    resid = (A.total_mass(st) - m0)
    last = series[-1]
    return dict(cfg=A.cfg_dict(cfg), seeds=list(seeds), minutes=minutes, series=series,
                final_prey=last["prey"], final_pred=last["pred"],
                prey_extinct=[int(x == 0) for x in last["prey"]], pred_extinct=[int(x == 0) for x in last["pred"]],
                births=st.births.tolist(), starved=st.starved.tolist(), catches=st.catches.sum(1).tolist(),
                mass_residual_max=float(np.abs(resid).max()), mass_total=float(m0.mean()))


# ----------------------------------------------------------------------------------------------- perf ------

def perf(thq, thp, steps=300):
    cfg = A.Cfg()
    st = A.reset(cfg, 1, 3)
    TQ = thq[None].astype(np.float32); TP = thp[None].astype(np.float32)
    rng = np.random.default_rng(1)
    for _ in range(20):
        A.step(cfg, st, TQ, TP, rng=rng)
    t = time.perf_counter()
    for _ in range(steps):
        A.step(cfg, st, TQ, TP, rng=rng)
    ms = 1000 * (time.perf_counter() - t) / steps
    return dict(ms_per_step=round(ms, 3), n_pred=cfg.n_pred, n_prey=cfg.n_prey, threads=1, batch=1,
                note="numpy prototype, dense O(N^2) neighbour matrices; a Burst port with a spatial hash is the game's cost")


# --------------------------------------------------------------------------------------------- player ------

def player(cfg, thq, thp, seeds=(31, 32, 33), secs=40.0):
    """A wandering vessel at 120 u/s through the pond (arena Pilot.wanderer). Conditions:
       seen     the vessel enters perception (prey see a predator, predators see prey) and is solid
       unseen   the vessel is only solid
    Metrics: predator share within 40 u of the vessel; share of predator-steps whose NEAREST 'prey' is the vessel
    (it has become the target); contacts (a predator within catch range of the hull); prey share within 30 u;
    prey 'shadowing' (near prey on the far side of the vessel from their nearest predator)."""
    from common.arena import Pilot
    out = {}
    for cond, ghost in (("seen", True), ("unseen", False)):
        per = []
        for s in seeds:
            rec = record(cfg, thq, thp, s, secs, vessel=Pilot.wanderer(speed=120.0), ghost=ghost)
            V = rec["vp"]                                    # [T,3]
            dp = np.linalg.norm(rec["pp"] - V[:, None], axis=-1)
            dq = np.linalg.norm(rec["qp"] - V[:, None], axis=-1)
            pa, qa = rec["pa"], rec["qa"]
            near_p = float((dp[pa] < 40).mean())
            tgt = float((rec["pt"] == cfg.n_prey)[pa].mean()) if ghost else None
            contacts = int(((dp < cfg.catch_r + 6.0) & pa).sum())
            near_q = float((dq[qa] < 30).mean())
            # shadow: prey within 30 u of the vessel, cos(angle(prey - vessel, vessel - nearest predator)) > 0.5
            sh = []
            for t in range(0, len(V), 5):
                sel = qa[t] & (dq[t] < 30)
                if not sel.any() or not pa[t].any():
                    continue
                P = rec["pp"][t][pa[t]]
                j = np.linalg.norm(P - V[t], axis=1).argmin()
                a = rec["qp"][t][sel] - V[t]; b = V[t] - P[j]
                cs = (a @ b) / np.maximum(np.linalg.norm(a, axis=1) * np.linalg.norm(b), 1e-6)
                sh.extend((cs > 0.5).tolist())
            # predator approach speed toward the vessel when within 60 u (positive = closing)
            clos = []
            for t in range(1, len(V)):
                m = pa[t] & (dp[t] < 60)
                clos.extend(((dp[t - 1][m] - dp[t][m]) / cfg.dt).tolist())
            per.append(dict(pred_near_share=near_p, pred_targets_vessel_share=tgt, contacts=contacts,
                            prey_near_share=near_q, prey_shadow_share=float(np.mean(sh)) if sh else None,
                            pred_closing_speed=float(np.mean(clos)) if clos else None,
                            catch_per_min=rec["catches"] / (secs / 60)))
        out[cond] = _mean_tree(per)
    # the null for "near": share of the pond volume within 40 / 30 u of a point (ignoring the wall)
    out["null_near_pred_40u"] = round((40 / cfg.R_cell) ** 3, 4)
    out["null_near_prey_30u"] = round((30 / cfg.R_cell) ** 3, 4)
    return out


# ------------------------------------------------------------------------------------------ viewer + gif ---

def viewer_json(rec, cfg, path, label, note, every=2):
    """Write a Tools/Ecology/common/viewer.py run (its Recorder data contract). Food is shown as mass points (one per
    rich grid cell, alive while the cell still holds food)."""
    G = cfg.G
    c = (np.arange(G) + 0.5) / G * 2 * cfg.R_cell - cfg.R_cell
    X, Y, Z = np.meshgrid(c, c, c, indexing="ij")
    f0 = rec["food"]
    cells = np.argwhere(f0 > 0.25)
    fpos = np.stack([X[tuple(cells.T)], Y[tuple(cells.T)], Z[tuple(cells.T)]], -1) if len(cells) else np.zeros((0, 3))
    mass0 = dict(pos=np.round(fpos, 1).tolist(), elem=[1] * len(fpos), style=[4] * len(fpos))   # jade = food
    frames = []
    T = len(rec["qp"])
    for t in range(0, T, every):
        qa, pa = rec["qa"][t], rec["pa"][t]
        burst = rec["pb"][t][pa]
        fr = dict(t=round(t * cfg.dt, 2),
                  pilots=[np.round(rec["vp"][t], 1).tolist()] if "vp" in rec else [],
                  alive=np.packbits(rec["fd"][t][tuple(cells.T)] > 0.25 if len(cells) else np.zeros(0, bool)).tobytes().hex(),
                  species=dict(
                      prey=dict(pos=np.round(rec["qp"][t][qa], 1).tolist(),
                                col=np.round(np.tile([0.45, 0.9, 1.0], (int(qa.sum()), 1)), 2).tolist(),
                                size=[3.0] * int(qa.sum())),
                      predators=dict(pos=np.round(rec["pp"][t][pa], 1).tolist(),
                                     col=np.round(np.where(burst[:, None], [1.0, 0.25, 0.15], [0.85, 0.45, 0.2]), 2).tolist(),
                                     size=np.where(burst, 9.0, 7.0).tolist())))
        frames.append(fr)
    meta = dict(label=label, note=note, R=cfg.R_cell, mass_n_max=len(fpos), focus=[0, 0, 0, cfg.R_cell * 2.2])
    with open(path, "w") as fh:
        json.dump(dict(meta=meta, mass0=mass0, frames=frames), fh, separators=(",", ":"))
    return path


def gif(rec, cfg, path, t0=0.0, t1=None, every=1, size=360, title=""):
    """A small orthographic GIF (view slowly orbiting): prey cyan, predators orange (red while bursting), short
    trails, catches flash white, the vessel (if any) a white triangle."""
    from PIL import Image, ImageDraw
    T = len(rec["qp"])
    k0 = int(t0 / cfg.dt); k1 = T if t1 is None else min(T, int(t1 / cfg.dt))
    frames = []
    R = cfg.R_cell
    ev = rec["events"]
    for k in range(k0, k1, every):
        ang = 0.6 + 0.004 * k
        ca, sa = np.cos(ang), np.sin(ang)
        def proj(P):
            x = P[..., 0] * ca + P[..., 2] * sa
            z = -P[..., 0] * sa + P[..., 2] * ca
            y = P[..., 1] * 0.94 + z * 0.34
            s = (size / 2 - 8) / R
            return np.stack([size / 2 + x * s, size / 2 - y * s], -1), z
        im = Image.new("RGB", (size, size), (6, 9, 16))
        dr = ImageDraw.Draw(im)
        dr.ellipse([8, 8, size - 8, size - 8], outline=(32, 52, 100))
        for tr in range(1, 5):                                      # trails
            kk = max(k0, k - 2 * tr)
            for key, akey, col in (("qp", "qa", (40, 90, 110)), ("pp", "pa", (120, 60, 30))):
                a = rec[akey][k]
                p0, _ = proj(rec[key][kk][a]); p1, _ = proj(rec[key][max(k0, kk - 2)][a])
                for u, v in zip(p0, p1):
                    dr.line([tuple(u), tuple(v)], fill=col)
        a = rec["qa"][k]
        pq, zq = proj(rec["qp"][k][a])
        for (x, y), z in zip(pq, zq):
            b = int(np.clip(180 + z / R * 70, 90, 255))
            dr.ellipse([x - 1.6, y - 1.6, x + 1.6, y + 1.6], fill=(int(b * 0.45), int(b * 0.9), b))
        a = rec["pa"][k]
        pp, _ = proj(rec["pp"][k][a])
        bb = rec["pb"][k][a]
        for (x, y), bu in zip(pp, bb):
            c = (255, 70, 40) if bu else (230, 140, 60)
            dr.ellipse([x - 4, y - 4, x + 4, y + 4], fill=c)
        if "vp" in rec:
            (vx, vy), _ = proj(rec["vp"][k])
            dr.polygon([(vx, vy - 6), (vx - 5, vy + 5), (vx + 5, vy + 5)], outline=(255, 255, 255))
        for e in ev:
            if 0 <= (k * cfg.dt - e[0]) < 0.5:
                pt, _ = proj(np.array(e[3:6]))
                dr.ellipse([pt[0] - 7, pt[1] - 7, pt[0] + 7, pt[1] + 7], outline=(255, 255, 255))
        dr.text((10, size - 18), f"{title} t={k * cfg.dt:4.1f}s", fill=(150, 160, 190))
        frames.append(im)
    frames[0].save(path, save_all=True, append_images=frames[1:], duration=int(1000 * cfg.dt * every), loop=0, optimize=True)
    return path


# ----------------------------------------------------------------------------------------------- main ------

def curve(run):
    L = [json.loads(l) for l in open(os.path.join(run, "log.jsonl"))]
    w = 20
    out = []
    for i in range(0, len(L), w):
        ch = L[i:i + w]
        out.append(dict(gen=ch[0]["gen"], cur_catch_pm=round(np.mean([r["cur_catch_pm"] for r in ch]), 2),
                        cur_caught=round(np.mean([r["cur_caught"] for r in ch]), 3),
                        cur_netq=round(np.mean([r["cur_netq"] for r in ch]), 3),
                        cur_fpred=round(np.mean([r["cur_fpred"] for r in ch]), 3),
                        predVpool=round(np.mean([r["predVpool_catch_pm"] for r in ch]), 2),
                        preyVpool=round(np.mean([r["preyVpool_catch_pm"] for r in ch]), 2)))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("what", nargs="+", choices=["matrix", "curve", "behave", "eco", "perf", "player", "record", "all"])
    ap.add_argument("--run", default=os.path.join(HERE, "runs", "arms_a1"))
    ap.add_argument("--every", type=int, default=200)
    ap.add_argument("--last", type=int, default=None)
    ap.add_argument("--gens", default="", help="comma list of generation pairs pred:prey for behave/record, e.g. 400:400,2000:2000")
    ap.add_argument("--out", default=OUT)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    what = set(a.what)
    if "all" in what:
        what = {"matrix", "curve", "behave", "eco", "perf", "player", "record"}
    snaps = load_snaps(a.run)
    last = max(snaps) if a.last is None else max(g for g in snaps if g <= a.last)
    pairs = [tuple(int(x) for x in p.split(":")) for p in a.gens.split(",") if p] or [(last, last)]
    cfg = A.Cfg()
    ev = json.load(open(os.path.join(a.out, "eval.json"))) if os.path.exists(os.path.join(a.out, "eval.json")) else {}
    if "curve" in what:
        ev["curve"] = curve(a.run)
    if "matrix" in what:
        m = matrix(a.run, a.every, last=a.last)
        json.dump(m, open(os.path.join(a.out, "matrix.json"), "w"), indent=1)
        print("matrix", json.dumps({k: v for k, v in m.items() if k not in ("catch_per_min", "sem")}))
    if "behave" in what:
        feel = json.load(open(os.path.join(a.out, "feel.json"))) if os.path.exists(os.path.join(a.out, "feel.json")) else {}
        ev.setdefault("behave", {})
        for gp, gq in pairs:
            b, f = behave(cfg, snaps[gq]["thq"], snaps[gp]["thp"])
            ev["behave"][f"{gp}:{gq}"] = b
            for k in f:
                f[k]["organic"] = organic(f[k])
            feel[f"{gp}:{gq}"] = f
            print("behave", gp, gq, json.dumps(b["mean"])[:2000])
            print("feel", json.dumps(f))
        json.dump(feel, open(os.path.join(a.out, "feel.json"), "w"), indent=1)
    if "eco" in what:
        ev.setdefault("eco", {})
        for gp, gq in pairs:
            e = eco(snaps[gq]["thq"], snaps[gp]["thp"])
            ev["eco"][f"{gp}:{gq}"] = e
            print("eco", gp, gq, {k: e[k] for k in ("final_prey", "final_pred", "births", "starved", "mass_residual_max")})
    if "perf" in what:
        ev["perf"] = perf(snaps[last]["thq"], snaps[last]["thp"])
        print("perf", ev["perf"])
    if "player" in what:
        ev.setdefault("player", {})
        for gp, gq in pairs:
            ev["player"][f"{gp}:{gq}"] = player(cfg, snaps[gq]["thq"], snaps[gp]["thp"])
            print("player", gp, gq, json.dumps(ev["player"][f"{gp}:{gq}"]))
    ev["locality"] = ("local: every input is the agent's own state, neighbours within its perception radius "
                      "(prey 50 u, predators 80 u, in a 200 u cell), the food at its own position, and the membrane "
                      "only when it is within R; all vectors in the agent's own body frame; no census, no global frame")
    json.dump(ev, open(os.path.join(a.out, "eval.json"), "w"), indent=1)
    if "record" in what:
        from common.arena import Pilot
        runs = []
        for gp, gq in pairs:
            rec = record(cfg, snaps[gq]["thq"], snaps[gp]["thp"], 41, 60.0)
            runs.append(viewer_json(rec, cfg, os.path.join(a.out, f"encounter_g{gp}_g{gq}.json"),
                                    f"pred g{gp} vs prey g{gq}", f"Co-evolved predators (orange; red = bursting) vs prey "
                                    f"(cyan). Green prisms are the grazeable food field (a cell blinks out when grazed below 25%). {len(rec['events'])} catches in 60 s."))
            gif(rec, cfg, os.path.join(a.out, f"encounter_g{gp}_g{gq}.gif"), 0, 20, every=2, title=f"P{gp} v Q{gq}")
            recv = record(cfg, snaps[gq]["thq"], snaps[gp]["thp"], 31, 40.0, vessel=Pilot.wanderer(speed=120.0), ghost=True)
            runs.append(viewer_json(recv, cfg, os.path.join(a.out, f"vessel_g{gp}_g{gq}.json"),
                                    f"vessel through P{gp}/Q{gq}", "A scripted wanderer vessel (120 u/s) flies through; "
                                    "prey perceive it as a predator, predators as prey (never trained against)."))
            gif(recv, cfg, os.path.join(a.out, f"vessel_g{gp}_g{gq}.gif"), 0, 15, every=2, title=f"vessel P{gp} v Q{gq}")
        from common.viewer import build
        print(build(os.path.join(a.out, "arms_viewer.html"), runs, "Arms race: predator vs prey"), "bytes")


if __name__ == "__main__":
    main()
