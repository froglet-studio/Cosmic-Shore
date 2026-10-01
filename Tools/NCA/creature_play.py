"""A PLAYABLE creature in the browser: results/creature/play.html.

The simulation runs live in JavaScript: field's slot body (a line-for-line port of field_port/FieldSwarmCore.cs,
itself checked against field_swarm.py) with field's native startle, plus the creature shell (a port of
creature_port/CreatureShell.cs, itself checked against creature_model.py to 8e-6) and fear-suppresses-breeding.
You fly the vessel with the mouse; nothing is scripted. The four body plans are exported from swarm_nca's targets.

    python Tools/NCA/creature_play.py
"""
import json
import os
import sys

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import field_swarm as fs  # noqa: E402

OUT = os.path.join(HERE, "results", "creature", "play.html")


def export():
    T = sn.load_targets()
    plans = []
    for k in sn.KINDS:
        p = fs.Plan(T[k], 6)
        plans.append(dict(kind=k, n=int(p.n), nslots=int((p.slot_mix > 0).sum()), order=[int(x) for x in p.order],
                          elem=p.elem.tolist(), slot=p.slot.tolist(), mix=p.mix.tolist(), slotMix=p.slot_mix.tolist(),
                          P=np.round(p.P, 2).reshape(len(p.P), -1).tolist()))
    gen = sn.make_gen(7)
    seeds = []
    for k in sn.KINDS:
        sw = sn.seed_swarm([T[k]], sn.World(), gen)
        m = sw.active[0]
        seeds.append(dict(pos=np.round(sw.pos[0][m].numpy(), 2).tolist(), elem=sw.elem[0][m].tolist(), dom=sw.dom[0][m].tolist()))
    return dict(plans=plans, seeds=seeds)


def main():
    torch.set_num_threads(2)
    data = export()
    page = open(os.path.join(HERE, "creature_play.html.tpl")).read()
    open(OUT, "w").write(page.replace("/*DATA*/", json.dumps(data, separators=(",", ":"))))
    print(OUT, os.path.getsize(OUT) // 1024, "KB")


if __name__ == "__main__":
    main()
