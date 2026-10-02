"""Which WormColonyConfig dials make the kaiju's attack READ as a threat? Peak and mean threat (menacing +
terrifying) over 8 s windows, hovering pilot, attack cycle isolated, 3 seeds. -> results/worm_tuning.json"""
import itertools, json, os
import numpy as np
from probe import EmotionProbe
from species import WormColony
from timeline import record, windows, summary
from sim import HERE

pr = EmotionProbe.load(); out = []
RUN_GRID = os.environ.get("ONEBODY") != "1"
grid = dict(lunge=(70, 140, 220), pursue_mult=(1.45, 3.0), telegraph=(1.2, 0.5))
for lunge, pm, tele in (itertools.product(*grid.values()) if RUN_GRID else []):
    pk, mn, tops = [], [], []
    for seed in (201, 202, 203):
        fac = lambda rng, p: WormColony(rng, p, start_dist=(120, 200), always_hunt=True, lunge=lunge, pursue_mult=pm, telegraph=tele)
        fr, r = record(fac, seed, "hover"); s = summary(windows(fr, r, pr), pr.emotions)
        pk.append(s["peak_threat"]); mn.append(s["mean_threat"]); tops += s["tops"]
    row = dict(lunge=lunge, pursuit=round(18 * pm, 1), telegraph=tele, peak_threat=round(float(np.mean(pk)), 3),
               mean_threat=round(float(np.mean(mn)), 3), reads={e: tops.count(e) for e in set(tops)})
    out.append(row); print(row, flush=True)
if RUN_GRID:
    json.dump(out, open(os.path.join(HERE, "results", "worm_tuning.json"), "w"), indent=1)


class OneBody:
    """The same worm presented as ONE long body (its head; radius = half its length; aspect 8) instead of
    8 lockstep agents - does the probe read a jointed body as an 'eerie synchronized group'?"""
    def __init__(self, worm):
        self.w = worm

    def step(self, arena, dt):
        self.w.step(arena, dt)

    @property
    def agent_pos(self): return self.w.agent_pos[:1]
    @property
    def agent_vel(self): return self.w.agent_vel[:1]
    agent_size = np.array([95.0]); agent_aspect = np.array([8.0])
    @property
    def agent_heading(self): return self.w.agent_heading[:1]


def as_one(**kw):
    return lambda rng, p: OneBody(WormColony(rng, p, start_dist=(120, 200), always_hunt=True, **kw))


if __name__ == "__main__" and os.environ.get("ONEBODY") == "1":
    rows = []
    for kw in (dict(), dict(lunge=220, pursue_mult=3.0, telegraph=0.5)):
        pk, mn, tops = [], [], []
        for seed in (201, 202, 203):
            fr, r = record(as_one(**kw), seed, "hover"); s = summary(windows(fr, r, pr), pr.emotions)
            pk.append(s["peak_threat"]); mn.append(s["mean_threat"]); tops += s["tops"]
        row = dict(one_body=True, **kw, peak_threat=round(float(np.mean(pk)), 3), mean_threat=round(float(np.mean(mn)), 3),
                   reads={e: tops.count(e) for e in set(tops)})
        rows.append(row); print(row, flush=True)
    p = os.path.join(HERE, "results", "worm_tuning.json"); d = json.load(open(p)); json.dump(d + rows, open(p, "w"), indent=1)
