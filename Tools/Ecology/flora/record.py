"""Record viewer runs for the threat flora and build the playback HTML with the shared viewer (common/viewer.py).

    python record.py [best]      -> out/flora.html (one dropdown entry per species x pilot)

Each run draws the species' own render buffers plus a context layer: the pilots' TRAIL prisms (pale - flora eat
them), the arena's real crystals (cyan), and any lures (lime, from the species). Params come from
results/search_<species>_best.json when present (`best`), else the species defaults.
"""
import json, os, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from harness import FloraArena, resolve  # noqa: E402
from common.arena import Pilot, Recorder  # noqa: E402
from common.viewer import build  # noqa: E402
from search import SPEC  # noqa: E402

NOTES = dict(
    snaptrap="Snap traps: lobes open WIDE and glow (primed), then sweep shut on whatever is between them. Red = danger teeth; the crystal sits in the jaws.",
    spores="Spore bursters: agitation spreads as an alarm wave (glow); swollen pods burst into a drifting danger cloud that re-seeds where it lands.",
    physarum="Physarum: a slime-mould transport network between food (and your trails); bright red pulses run along the tubes - those burn.",
    coral="Reaction-diffusion coral: pink fronts are the stinging growth tips; grey is dead skeleton; flying through plain coral carves it.",
    walker="Walking mimics: thickets that treadmill toward food; the lime 'crystal' on top is bait - thorns erupt when you take it.",
)


class Context:
    def __init__(self, ar, sp, n0):
        self.ar, self.sp, self.n0 = ar, sp, n0

    def render(self, out):
        ar = self.ar
        idx = np.arange(self.n0, len(ar.mass_vol)); idx = idx[ar.mass_alive[idx]]
        out["trails"] = dict(pos=ar.mass_pos[idx], col=np.tile([[0.55, 0.55, 0.7]], (len(idx), 1)), size=np.full(len(idx), 3.0))
        out["crystals"] = dict(pos=ar.crys, col=np.tile([[0.3, 0.95, 1.0]], (len(ar.crys), 1)), size=np.full(len(ar.crys), 12.0))


def record(name, params, policy, seconds=75, every=4, seed=7):
    ar = FloraArena(seed=seed); ar.grove_mass(1600, clumps=20)
    n0 = len(ar.mass_vol)
    sp = resolve(SPEC[name])(ar, params); ar.species = sp
    mk = dict(wander=Pilot.wanderer, reader=lambda: Pilot("reader", speed=120.0, name="reader"),
              cutter=lambda: Pilot("cutter", speed=140.0, name="cutter"),
              courier=lambda: Pilot("courier", speed=120.0, name="courier"))[policy]
    pl = ar.add_pilot(mk()); pl.prev = pl.pos.copy()
    rec = Recorder(every=every); ctx = Context(ar, sp, n0)
    for _ in range(int(seconds / 0.1)):
        sp.step(ar, 0.1)
        if policy == "cutter": sp.cut(ar, pl, pl.prev, pl.pos)
        ar.step(0.1); rec.frame(ar, [sp, ctx])
    os.makedirs(os.path.join(HERE, "out"), exist_ok=True)
    path = os.path.join(HERE, "out", f"{name}_{policy}.json")
    rec.save(path, dict(label=f"{name} vs {policy} ({len(ar.log)} hits)", note=NOTES[name]))
    return path


if __name__ == "__main__":
    use_best = len(sys.argv) > 1 and sys.argv[1] == "best"
    paths = []
    for name in ("snaptrap", "spores", "physarum", "coral", "walker"):
        params = {}
        bp = os.path.join(HERE, "results", f"search_{name}_best.json")
        if use_best and os.path.exists(bp): params = json.load(open(bp))["params"]
        for pol in ("wander", "reader", "cutter"):
            paths.append(record(name, params, pol)); print(paths[-1], flush=True)
    print(build(os.path.join(HERE, "out", "flora.html"), paths, "Threat flora (Direction F)"), "bytes")
