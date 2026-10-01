"""Parity check: the C# shell (creature_port/CreatureShell.cs) against the Python reference
(creature_model.CreatureRule._shell). Captures (input, state, output) at every shell call of scripted
encounters (fly-by, circle, cruise, a cull for the tell) on all four plans, with the tell jitter off (the one
random term), and replays each case in C#. Reports the worst position / startle difference and the C#
per-step cost.

    python Tools/NCA/creature_port_check.py        # needs dotnet 8 (DOTNET=... to override)
"""
import json
import math
import os
import subprocess
import sys

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import swarm_eval as se  # noqa: E402
import creature_model as cm  # noqa: E402

PORT = os.path.join(HERE, "creature_port")


def capture(seed=5):
    T = sn.load_targets()
    cases = []
    for k in sn.KINDS:
        m = cm.CreatureRule(tell_jitter=0.0)
        orig = m._shell

        def hooked(sw, gen, m=m, orig=orig):
            st = m._st
            rec = dict(pos=sw.pos[0].tolist(), elem=sw.elem[0].tolist(), active=sw.active[0].tolist(), hatched=sw.hatched[0].tolist(),
                       off=st["off"][0].tolist(), startle=st["startle"][0].tolist(), threat=float(st["threat"][0]),
                       swell=float(st["swell"][0]), tell=float(st["tell"][0]), maj=int(st["maj"][0]), t=int(st["t"][0]),
                       vessels=[[*map(float, v[0]), float(v[1]), *map(float, v[2])] for v in m.vessels] if m.react else [])
            orig(sw, gen)
            rec["out_pos"] = sw.pos[0].tolist(); rec["out_startle"] = m._st["startle"][0].tolist(); rec["out_threat"] = float(m._st["threat"][0])
            if m.vessels or rec["tell"] > 0 or len(cases) % 25 == 0:
                cases.append(rec)
        m._shell = hooked
        gen = sn.make_gen(seed)
        sw = sn.seed_swarm([T[k]], m.world, gen)
        for _ in range(240):
            sw = m(sw, gen)
        al = sw.active[0] & sw.hatched[0]; c = sw.pos[0][al].mean(0).numpy(); rms = float(((sw.pos[0][al] - sw.pos[0][al].mean(0)) ** 2).sum(-1).mean().sqrt())
        d = np.array([0.8, 0.25, 0.55]); d /= np.linalg.norm(d)
        start = c - d * 2.5 * rms
        for i in range(int(5 * rms / 3)):                      # fly-by
            m.vessels = [(start + d * 3 * i, 0.6 * rms, d * 3)]; sw = m(sw, gen)
        for i in range(40):                                    # circle (slow: mobbing)
            a = 0.8 / (1.6 * rms) * i
            m.vessels = [(c + 1.6 * rms * np.array([math.cos(a), 0, math.sin(a)]), 0.4 * rms, 0.8 * np.array([-math.sin(a), 0, math.cos(a)]))]; sw = m(sw, gen)
        for i in range(40):                                    # cruise past (escort)
            m.vessels = [(c + np.array([0, 0, 1.5 * rms]) + np.array([1.6, 0, 0]) * (i - 20), 0.35 * rms, np.array([1.6, 0, 0]))]; sw = m(sw, gen)
        m.vessels = []
        se.cull_to(sw, 0, sn.SWITCH_TO[k], gen)                # the tell
        for _ in range(40):
            sw = m(sw, gen)
    return cases


def main():
    torch.set_num_threads(4)
    cases = capture()
    path = os.path.join(HERE, "runs", "creature", "port_cases.json")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    json.dump(dict(cfg=cm.DEFAULTS, cases=cases), open(path, "w"))
    print(f"{len(cases)} cases -> {path}")
    dotnet = os.environ.get("DOTNET", os.path.expanduser("~/.dotnet/dotnet"))
    r = subprocess.run([dotnet, "run", "-c", "Release", "--project", PORT, "--", path], capture_output=True, text=True)
    print(r.stdout[-3000:], r.stderr[-3000:])
    out = os.path.join(HERE, "results", "creature", "port_check.json")
    res = json.loads(r.stdout.strip().splitlines()[-1])
    json.dump(res, open(out, "w"), indent=1)


if __name__ == "__main__":
    main()
