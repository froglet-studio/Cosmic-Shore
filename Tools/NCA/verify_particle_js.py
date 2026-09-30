"""Hold particle_core.js equal to particle_nca.ParticleNCA: grow a population in torch, hand
the exact state to Node, then run both N steps at fire rate 1 with budding off (the two
random parts), and compare every position and channel.

    python3 Tools/NCA/verify_particle_js.py --run Tools/NCA/results/particle_regenerating

Negative control: the JS with the collision push sign flipped must DISAGREE.
"""
import argparse
import json
import os
import subprocess
import sys

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from particle_nca import ParticleNCA, State, World, seed_state  # noqa: E402

JS = r"""
const fs = require('fs');
let src = fs.readFileSync(process.argv[2], 'utf8');
if (process.argv[5] === 'flip') src = src.replace('push[i * d + k] -= W.rep', 'push[i * d + k] += W.rep');
const m = {}; new Function('module', src)(m);
const w = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
if (process.argv[5] === 'plain') w.world.corrected = false;
const st = JSON.parse(fs.readFileSync(process.argv[6], 'utf8'));
const ca = m.exports.makeParticleNCA(w);
ca.pos.set(st.pos); ca.s.set(st.s); ca.act.set(st.act);
for (let i = 0; i < +process.argv[4]; i++) ca.step({ fireRate: 1.0, bud: false });
process.stdout.write(JSON.stringify({ pos: Array.from(ca.pos), s: Array.from(ca.s), act: Array.from(ca.act) }));
"""


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", required=True)
    ap.add_argument("--steps", type=int, default=20)
    ap.add_argument("--grow", type=int, default=120)
    a = ap.parse_args()
    ok = verify(a, a.grow, negative=True)
    # a lone seed beside one dormant child: here a particle's OWN alpha (below 1) is the only
    # thing keeping it alive - the case a grown colony never exercises (a JS runner that stored
    # alpha in a Uint8Array killed the seed here and 11/40 colonies went extinct in 3-7 steps)
    ok &= verify(a, 1, negative=False, lone=True)
    print("OK" if ok else "FAIL")
    assert ok


def verify(a, grow, negative, lone=False):
    w = json.load(open(os.path.join(a.run, "weights.json")))
    world = World(**w["world"])
    ca = ParticleNCA(world, w["channel_n"], w["hidden"], w["fire_rate"])
    with torch.no_grad():
        for k in ("w1", "b1", "w2", "b2"):
            getattr(ca, k).copy_(torch.tensor(w[k]))
    torch.manual_seed(0)
    grid = 72 if world.dim == 2 else None
    centre = [36.0, 36.0] if world.dim == 2 else [22.0, 22.0, 11.0]
    with torch.no_grad():
        x = seed_state(1, world, centre)
        for _ in range(grow):
            x = ca(x, fire_rate=1.0, bud=False) if lone else ca(x)
        if lone:
            assert world.dim == 2
            x.pos[0, 1] = x.pos[0, 0] + torch.tensor([-0.94, -0.90])
            x.s[0, 1] = 0
            x.active[0, 1] = True
        start = x.clone()
        for i in range(a.steps):
            x = ca(x, fire_rate=1.0, bud=False)
            if lone and i == 0:        # the case is exercised only if the seed ends step 1 alive on a fractional alpha
                assert bool(x.active[0, 0]) and 0.1 < float(x.s[0, 0, 3]) < 1.0, float(x.s[0, 0, 3])
    stp = os.path.join(HERE, "_pstate.json")
    json.dump({"pos": start.pos[0].reshape(-1).tolist(), "s": start.s[0].reshape(-1).tolist(),
               "act": start.active[0].int().tolist()}, open(stp, "w"))
    js = os.path.join(HERE, "_pverify.js")
    open(js, "w").write(JS)

    def run(mode=""):
        out = subprocess.check_output(["node", js, os.path.join(HERE, "particle_core.js"),
                                       os.path.join(a.run, "weights.json"), str(a.steps), mode, stp])
        return json.loads(out)
    try:
        good = run()
        bad = run("flip") if negative else None
        plain = run("plain") if (world.corrected and negative) else None
    finally:
        os.remove(js); os.remove(stp)
    act = x.active[0].numpy()
    def err(o):
        if not np.array_equal(np.array(o["act"], bool), act):
            return float("inf")
        p = np.array(o["pos"], np.float32).reshape(-1, world.dim)[act]
        s = np.array(o["s"], np.float32).reshape(-1, 16)[act]
        return max(float(np.abs(p - x.pos[0].numpy()[act]).max()), float(np.abs(s - x.s[0].numpy()[act]).max()))
    e = err(good)
    neg = f"  (collision-flip negative control {err(bad):.2e})" if negative else ""
    print(f"{'lone seed + dormant child' if lone else f'grown {grow} steps'}, particles {int(act.sum())} | corrected={world.corrected} | JS vs torch after "
          f"{a.steps} steps: max abs err {e:.2e}{neg}")
    if e >= 1e-3:
        print("  JS particle runner disagrees with torch")
        return False
    if negative:
        assert err(bad) > 1e-2, "negative control did not fire"
    if plain is not None:
        ep = err(plain)
        print(f"corrected-perception negative control (JS with plain perception): {ep:.2e}")
        assert ep > 1e-2, "corrected-perception negative control did not fire"
    return True


if __name__ == "__main__":
    main()
