"""Body assembly: the leviathan parameter set (a grazer school + a manta body plan) condenses into a creature
and dissolves again, with no script. Attachment relaxes toward sigmoid(sated*(1-hunger) + fear*group_fear +
bias): a fed school rests as one big body; hunger rises (a drive clock, no mass lost), members peel off to
graze, the body dissolves; they eat, sate, and it condenses again. A pilot charging in (fear) also condenses
it (an intimidation display).

Measured each second: assembled fraction (attach > 0.5), shape error = mean distance of assembled members to
their slot / body length, and the body's bounding extent.

    python -m substrate.assembly_demo
"""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from common.arena import Arena, Pilot
from substrate.core import Substrate
from substrate import species as S

HERE = os.path.dirname(__file__)
BODY_LEN = 115.0      # manta_slots: tail tip (-108) .. spine front (+36) along z at scale 1, ~ wing span 80


def run(seed=7, minutes=4.0, dt=0.1, backend="numpy", charge_at=None):
    ar = Arena(seed=seed); ar.scatter_mass(3500)
    pl = ar.add_pilot(Pilot.wanderer())
    sp = Substrate(ar, S.leviathan(seed=seed), backend=backend, spread=120, init_hunger=0.5)
    pl.pos = sp.home + np.array([900.0, 0, 0]); pl.goal = pl.pos.copy()
    rows = []
    for i in range(int(minutes * 60 / dt)):
        if charge_at is not None and charge_at <= ar.t < charge_at + 10:
            pl.policy = "hunter"
        elif charge_at is not None and ar.t >= charge_at + 10:
            pl.policy = "wander"
        sp.step(ar, dt); ar.step(dt)
        if i % 10 == 0:
            a = np.flatnonzero(sp.alive & ~sp.dying)
            att = a[sp.attach[a] > 0.5]
            err = None; ext = None
            if len(att) >= 8 and sp.body_c is not None:
                err = float(np.mean(np.linalg.norm(sp._slot_world(att) - sp.pos[att], axis=1)) / BODY_LEN)
                ext = float(np.ptp(sp.pos[att], axis=0).max())
            loose_ext = float(np.ptp(sp.pos[a], axis=0).max()) if len(a) else 0
            rows.append(dict(t=round(ar.t, 1), n=len(a), assembled=round(len(att) / max(len(a), 1), 3),
                             shape_err=None if err is None else round(err, 3), body_extent=ext and round(ext),
                             swarm_extent=round(loose_ext), hunger=round(float(sp.hunger[a].mean()), 3),
                             fear=round(float(sp.fear[a].mean()), 3)))
    return rows


def cycles(rows, hi=0.6, lo=0.2):
    """Count condense->dissolve cycles (assembled fraction crossing hi then lo)."""
    state, n, ev = 0, 0, []
    for r in rows:
        if state == 0 and r["assembled"] > hi:
            state = 1; ev.append(("condense", r["t"]))
        elif state == 1 and r["assembled"] < lo:
            state = 0; n += 1; ev.append(("dissolve", r["t"]))
    return n, ev


if __name__ == "__main__":
    out = {}
    for seed, charge in ((7, None), (23, None), (41, None), (7, 30.0)):
        rows = run(seed, minutes=6.0, charge_at=charge)
        n, ev = cycles(rows)
        errs = [r["shape_err"] for r in rows if r["shape_err"] is not None and r["assembled"] > 0.6]
        out[f"seed{seed}" + ("_charge" if charge else "")] = dict(cycles=n, events=ev, shape_err_assembled=round(float(np.median(errs)), 3) if errs else None,
                                  rows=rows)
        print(seed, charge, "cycles", n, ev[:8], "median shape err when assembled", out[f"seed{seed}" + ("_charge" if charge else "")]["shape_err_assembled"])
    json.dump(out, open(os.path.join(HERE, "results", "assembly.json"), "w"), indent=1)
