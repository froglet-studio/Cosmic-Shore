"""Experiments for the living cell (Direction G, Part 1). From Tools/Ecology:

    python3 -m living_cell.run baseline      # the default cell, several seeds
    python3 -m living_cell.run controls      # every metric's negative control (each must FIRE)
    python3 -m living_cell.run iterate       # composition / dial rounds (results/iterate.json, every run kept)
    python3 -m living_cell.run consistency   # macro vs micro on the same cell (what the one-cohort LOD costs)

A run: build the cell, two pilots (an EXPLORER touring the cell and a WANDERER) fly the whole time, laying
trail; after `burn` seconds each pilot's time is cut into consecutive 5-minute FLIGHTS that are scored.
"""
from __future__ import annotations

import json
import os
import sys
import time
from multiprocessing import Pool

import numpy as np

from .cell import Cell, DEFAULT
from .metrics import EcoRecorder, Flight, eco_metrics, continuity_metrics, replay_distance

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(HERE, "results")


def run_cell(seed=1, cfg=None, minutes=30.0, pilots=("explore", "wander"), burn=600.0, flight_s=300.0, emo=True,
             bug="", dt=0.1, keep_series=True):
    t_wall = time.time()
    c = Cell(seed=seed, cfg=cfg, pilots=pilots, bug=bug)
    eco = EcoRecorder(every=5.0)
    flights, cur = [], []
    n = int(minutes * 60 / dt)
    for i in range(n):
        c.step(dt)
        eco.update(c)
        w = c.w
        if w.t >= burn and pilots:
            if not cur or cur[0].done():
                if cur:
                    flights += [(f.k, f.summary(emo=emo)) for f in cur]
                if w.t + flight_s <= minutes * 60 + 1e-6:
                    cur = [Flight(c, k, t0=w.t, seconds=flight_s) for k in range(len(pilots))]
                else:
                    cur = []
            for f in cur:
                f.update()
    if cur and cur[0].done():
        flights += [(f.k, f.summary(emo=emo)) for f in cur]
    em = eco_metrics(eco.rows, c, burn=min(burn, 300.0))
    cost = np.array(c.cost)
    out = dict(seed=seed, minutes=minutes, cfg={k: v for k, v in (cfg or {}).items()}, bug=bug,
               eco=em, continuity=continuity_metrics(c.w),
               flights=[dict(pilot=pilots[k], **{kk: vv for kk, vv in f.items() if kk != "events" or True}) for k, f in flights],
               cost_ms=dict(mean=round(float(cost.mean() * 1000), 2), p95=round(float(np.percentile(cost, 95) * 1000), 2)),
               stats=dict(rams=c.rams, pilot_kills=c.pilot_kills, crystals=c.w.crystals, shield_refusals=c.w.shield_refusals,
                          expands={k: g.expands for k, g in c.guilds.items()}, absorbs={k: g.absorbs for k, g in c.guilds.items()},
                          prey_kills={k: getattr(g, "prey_kills", 0) for k, g in c.guilds.items()},
                          births={k: g.births for k, g in c.guilds.items()}, starved={k: g.starved for k, g in c.guilds.items()},
                          eaten_by={k: round(v, 1) for k, v in c.w.eaten_by.items()},
                          thief=dict(steals=getattr(c.guilds.get("thief"), "steals", 0), hoarded=getattr(c.guilds.get("thief"), "hoarded", 0),
                                     larder=round(getattr(c.guilds.get("thief"), "larder", 0.0), 1)),
                          fortress=None if not c.fortress else dict(placed=c.fortress.placed, placed_trail=c.fortress.placed_trail,
                                                                     from_hoard=c.fortress.from_hoard, cuts=c.fortress.cuts,
                                                                     repairs=c.fortress.repairs, fill=round(c.fortress.shell_fill(), 3),
                                                                     workers=c.fortress.count(), births=c.fortress.births, starved=c.fortress.starved),
                          traps=None if not c.traps else dict(catches=c.traps.catches, snaps_pilot=c.traps.snaps_pilot, fired=c.traps.fired,
                                                               buds=c.traps.buds, n=len(c.traps.heart)),
                          physarum=None if not c.phys else dict(digested=round(c.phys.digested, 1), tubes=c.phys.n_tubes(),
                                                                 burns=c.phys.burns, reserve=round(c.phys.reserve, 1))),
               wall_s=round(time.time() - t_wall, 1))
    if keep_series:
        out["series"] = eco.rows
    return out


def _job(a):
    return run_cell(**a)


def pool_map(jobs, procs=4):
    with Pool(min(procs, len(jobs))) as p:
        return p.map(_job, jobs)


def flight_table(runs):
    F = [f for r in runs for f in r["flights"]]
    if not F:
        return {}
    m = lambda k: round(float(np.mean([f.get(k, 0) for f in F])), 3)
    out = {k: m(k) for k in ("enc_per_min", "variety", "variety_entropy_bits", "quiet_frac", "hits_per_min", "damage_per_min", "steals_per_min",
                             "emotion_distinct", "emotion_entropy_bits", "threat_range", "threat_peak", "active_encounters", "species_sighted")}
    out["n_flights"] = len(F)
    # replayability: every pair of flights by the same pilot policy from DIFFERENT seeds
    d = []
    for i in range(len(runs)):
        for j in range(i + 1, len(runs)):
            for fa in runs[i]["flights"]:
                for fb in runs[j]["flights"]:
                    if fa["pilot"] == fb["pilot"]:
                        d.append(replay_distance(fa, fb))
    if d:
        out["replay_jaccard"] = round(float(np.mean([x["jaccard_dist"] for x in d])), 3)
        out["replay_species_tv"] = round(float(np.mean([x["species_tv"] for x in d])), 3)
        out["replay_emotion_tv"] = round(float(np.mean([x["emotion_tv"] for x in d])), 3)
    by = {}
    for f in F:
        for s, k in f["enc_by_species"].items():
            by[s] = by.get(s, 0) + k
    out["enc_by_species_total"] = by
    return out


def eco_table(runs):
    E = [r["eco"] for r in runs]
    return dict(shannon_mean=round(float(np.mean([e["shannon_mean"] for e in E])), 3),
                shannon_min=round(float(np.min([e["shannon_min"] for e in E])), 3),
                n_extinct=int(sum(e["persistence"]["n_extinct"] for e in E)),
                n_unrecovered=int(sum(e["persistence"]["n_unrecovered"] for e in E)),
                extinct=[list(e["persistence"]["extinct"]) for e in E],
                freeze_frac=round(float(np.mean([e["freeze_frac"] for e in E])), 3),
                flora_saturated=round(float(np.mean([e["flora_saturated_frac"] for e in E])), 3),
                reversals=[e["reversals"] for e in E],
                cv={k: round(float(np.mean([e["cv"].get(k, 0) for e in E])), 3) for k in E[0]["cv"]},
                audit_max=float(max(e["audit_max"] for e in E)), shield_eaten=int(sum(e["shield_eaten"] for e in E)),
                pop_ins=int(sum(r["continuity"]["pop_ins"] for r in runs)), pop_outs=int(sum(r["continuity"]["pop_outs"] for r in runs)),
                cap_frac={k: round(float(np.mean([e["cap_frac"].get(k, 0) for e in E])), 3) for k in E[0].get("cap_frac", {})},
                soil_frac_of_input=round(float(np.mean([e["soil"]["frac_of_input"] or 0 for e in E])), 3) if "soil" in E[0] else None,
                N_end=round(float(np.mean([e["soil"]["N_end"] for e in E]))) if "soil" in E[0] else None,
                plants_end=round(float(np.mean([e["soil"]["plants_end"] or 0 for e in E]))) if "soil" in E[0] else None,
                births={k: int(np.sum([r["stats"]["births"].get(k, 0) for r in runs])) for k in runs[0]["stats"]["births"]},
                starved={k: int(np.sum([r["stats"]["starved"].get(k, 0) for r in runs])) for k in runs[0]["stats"]["starved"]},
                cost_ms=round(float(np.mean([r["cost_ms"]["mean"] for r in runs])), 2),
                cost_p95=round(float(np.max([r["cost_ms"]["p95"] for r in runs])), 2))


def save(name, obj):
    os.makedirs(RES, exist_ok=True)
    with open(os.path.join(RES, name), "w") as fh:
        json.dump(obj, fh, indent=1, default=lambda o: o.tolist() if hasattr(o, "tolist") else str(o))


def summarise(label, runs):
    s = dict(label=label, eco=eco_table(runs), player=flight_table(runs))
    print(json.dumps(s, indent=None)[:3000])
    return s


def baseline(seeds=(1, 2, 3, 4), minutes=30.0, cfg=None, label="baseline"):
    runs = pool_map([dict(seed=s, cfg=cfg, minutes=minutes) for s in seeds])
    s = summarise(label, runs)
    save(f"{label}.json", dict(summary=s, runs=runs))
    return s, runs


# ==========================================================================================================
# NEGATIVE CONTROLS: a planted failure per metric; each must FIRE (and the default cell must not).
NOFIX = dict(traps=False, fortress=False, physarum=False)
CONTROLS = {
    "persistence": (dict(pack_metab=0.25), "", lambda s: s["eco"]["n_extinct"] > 0),
    "diversity": (dict(NOFIX, species=("grazer",)), "", lambda s: s["eco"]["shannon_min"] < 0.8),
    # round 2: the cell now seeds flora at its grazed level, so a fauna-less cell is still GROWING at minute 12;
    # the planted failure starts it near its cap (the state it would reach), where nothing moves any more
    "freeze": (dict(species=(), flora_seed_frac=0.9), "", lambda s: s["eco"]["freeze_frac"] > 0.3 or s["eco"]["flora_saturated"] > 0.3),
    "audit": (dict(), "leak_birth", lambda s: s["eco"]["audit_max"] > 1.0),
    "shield": (dict(), "eat_shield", lambda s: s["eco"]["shield_eaten"] > 0),
    "continuity": (dict(expand_r=120.0, ahead_r=150.0, absorb_r=160.0), "", lambda s: s["eco"]["pop_ins"] + s["eco"]["pop_outs"] > 0),
    "variety": (dict(NOFIX, species=("pack",), pack_n=60), "", lambda s: s["player"]["variety"] <= 1.0),
    "quiet": (dict(NOFIX, species=("pack",), pack_n=600, pack_cap=800, pack_metab=0.0), "", lambda s: s["player"]["quiet_frac"] < 0.3),
    "emotion_range": (dict(NOFIX, species=("grazer",)), "", lambda s: s["player"]["emotion_distinct"] <= 2.0),
}


def controls(minutes=12.0, seeds=(1, 2), cfg_name="FINAL", tag="controls"):
    """Each control is the RECOMMENDED cell (rounds.FINAL; round 2: FINAL2) with one planted failure."""
    from . import rounds
    FINAL = getattr(rounds, cfg_name)
    jobs, keys = [], []
    for name, (cfg, bug, _) in CONTROLS.items():
        for s in seeds:
            jobs.append(dict(seed=s, cfg=dict(FINAL, **cfg), minutes=minutes, bug=bug, burn=300.0, keep_series=False)); keys.append(name)
    # the recommended cell itself must NOT fire any of them
    for s in seeds:
        jobs.append(dict(seed=s, cfg=FINAL, minutes=minutes, burn=300.0, keep_series=False)); keys.append("clean")
    # replayability control: the SAME seed twice must measure distance 0 (two identical flights)
    jobs += [dict(seed=5, cfg=FINAL, minutes=minutes, burn=300.0, keep_series=False)] * 2; keys += ["replay", "replay"]
    res = pool_map(jobs)
    out = {}
    for name in CONTROLS:
        rs = [r for r, k in zip(res, keys) if k == name]
        s = dict(eco=eco_table(rs), player=flight_table(rs))
        out[name] = dict(fired=bool(CONTROLS[name][2](s)), eco=s["eco"], player={k: v for k, v in s["player"].items() if k != "enc_by_species_total"})
    rc = [r for r, k in zip(res, keys) if k == "clean"]
    sc = dict(eco=eco_table(rc), player=flight_table(rc))
    out["clean"] = dict(fires={n: bool(f(sc)) for n, (_, _, f) in CONTROLS.items()}, eco=sc["eco"],
                        player={k: v for k, v in sc["player"].items() if k != "enc_by_species_total"})
    rr = [r for r, k in zip(res, keys) if k == "replay"]
    d = [replay_distance(a, b) for a, b in zip(rr[0]["flights"], rr[1]["flights"])]
    out["replay"] = dict(fired=all(x["jaccard_dist"] == 0 and x["species_tv"] == 0 for x in d), distances=d)
    for k, v in out.items():
        if k == "clean":
            print("clean cell fires:", {n: f for n, f in v["fires"].items() if f} or "nothing")
        else:
            print(k, "FIRED" if v["fired"] else "did NOT fire")
    save(f"{tag}.json", out)
    return out



# ==========================================================================================================
def iterate(rounds, seeds=(1, 2, 3), minutes=20.0, tag="iterate"):
    """Run named configurations (every run kept in results/<tag>.json, appended across calls)."""
    path = os.path.join(RES, f"{tag}.json")
    allr = json.load(open(path)) if os.path.exists(path) else {}
    jobs, keys = [], []
    for name, cfg in rounds.items():
        for s in seeds:
            jobs.append(dict(seed=s, cfg=cfg, minutes=minutes, keep_series=True)); keys.append(name)
    res = pool_map(jobs)
    for name, cfg in rounds.items():
        rs = [r for r, k in zip(res, keys) if k == name]
        allr[name] = dict(cfg=cfg, summary=dict(eco=eco_table(rs), player=flight_table(rs)),
                          runs=[{k: v for k, v in r.items() if k != "flights"} | dict(flights=[{k: v for k, v in f.items() if k != "emotion_series"} for f in r["flights"]]) for r in rs])
        print(name, json.dumps(allr[name]["summary"])[:1800])
    save(f"{tag}.json", allr)
    return allr



def _consist(a):
    seed, mode, minutes, cfg = a
    c = Cell(seed=seed, cfg=dict(cfg, force_hot=(mode == "micro"), traps=False, fortress=False, physarum=False), pilots=())
    rows = []
    for i in range(int(minutes * 600)):
        c.step(0.1)
        if i % 300 == 0:
            rows.append(dict(t=round(c.w.t), census={k: v for k, v in c.census().items() if k in c.guilds},
                             flora=round(c.biomass()["flora"]), N=round(c.w.N)))
    return dict(seed=seed, mode=mode, rows=rows, audit=c.w.audit(), cost_ms=round(float(np.mean(c.cost)) * 1000, 2),
                prey_kills={k: getattr(g, "prey_kills", 0) for k, g in c.guilds.items()},
                births={k: g.births for k, g in c.guilds.items()}, starved={k: g.starved for k, g in c.guilds.items()})


def consistency(seeds=(1, 2, 3), minutes=6.0, cfg=None, tag="consistency"):
    """The same cell (no pilots, no structures) run all-MACRO (every region a cohort) and all-MICRO (every
    region expanded into individuals). What the one-cohort-per-region simplification costs is the gap."""
    cfg = cfg or {}
    with Pool(4) as p:
        res = p.map(_consist, [(s, m, minutes, cfg) for s in seeds for m in ("macro", "micro")])
    out = dict(runs=res, gap={})
    for sp in res[0]["rows"][-1]["census"]:
        for key in ("count",):
            mac = np.array([[r["census"][sp] for r in x["rows"]] for x in res if x["mode"] == "macro"], float)
            mic = np.array([[r["census"][sp] for r in x["rows"]] for x in res if x["mode"] == "micro"], float)
            out["gap"][sp] = dict(macro_end=round(float(mac[:, -1].mean()), 1), micro_end=round(float(mic[:, -1].mean()), 1),
                                  rel_gap_end=round(float(abs(mac[:, -1].mean() - mic[:, -1].mean()) / max(mic[:, -1].mean(), 1)), 3),
                                  rel_gap_mean=round(float(np.mean(np.abs(mac.mean(0) - mic.mean(0)) / np.maximum(mic.mean(0), 1))), 3))
    fm = np.array([[r["flora"] for r in x["rows"]] for x in res if x["mode"] == "macro"], float)
    fi = np.array([[r["flora"] for r in x["rows"]] for x in res if x["mode"] == "micro"], float)
    out["gap"]["flora"] = dict(macro_end=round(float(fm[:, -1].mean())), micro_end=round(float(fi[:, -1].mean())),
                               rel_gap_mean=round(float(np.mean(np.abs(fm.mean(0) - fi.mean(0)) / np.maximum(fi.mean(0), 1))), 3))
    print(json.dumps(out["gap"], indent=1))
    save(f"{tag}.json", out)
    return out


if __name__ == "__main__":
    what = sys.argv[1] if len(sys.argv) > 1 else "baseline"
    if what == "baseline":
        mins = float(sys.argv[2]) if len(sys.argv) > 2 else 30.0
        baseline(minutes=mins)
    elif what == "controls":
        controls()
    elif what == "controls2":
        controls(cfg_name="FINAL2", tag="r2_controls")
    elif what == "final2":
        from .rounds import FINAL2
        baseline(seeds=(1, 2, 3, 4), minutes=float(sys.argv[2]) if len(sys.argv) > 2 else 45.0, cfg=FINAL2, label="r2_final")
    elif what == "final":
        from .rounds import FINAL
        baseline(seeds=(1, 2, 3, 4), minutes=float(sys.argv[2]) if len(sys.argv) > 2 else 45.0, cfg=FINAL, label="final")
    elif what == "lod":
        from .rounds import FINAL, LOD
        iterate({k: dict(FINAL, **v) for k, v in LOD.items()}, minutes=15.0, seeds=(1, 2), tag="lod")
    elif what == "consistency":
        import importlib
        consistency(cfg=getattr(importlib.import_module("living_cell.rounds"), sys.argv[2]) if len(sys.argv) > 2 else None,
                    minutes=float(sys.argv[3]) if len(sys.argv) > 3 else 6.0, tag=sys.argv[4] if len(sys.argv) > 4 else "consistency")
    elif what == "iterate":
        import importlib
        mod = importlib.import_module("living_cell.rounds")
        seeds = tuple(int(x) for x in sys.argv[4].split(",")) if len(sys.argv) > 4 else (1, 2, 3)
        tag = sys.argv[5] if len(sys.argv) > 5 else "iterate"
        iterate(getattr(mod, sys.argv[2]), minutes=float(sys.argv[3]) if len(sys.argv) > 3 else 20.0, seeds=seeds, tag=tag)
