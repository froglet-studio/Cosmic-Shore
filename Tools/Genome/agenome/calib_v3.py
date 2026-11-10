"""Judge v3 calibration: the footage table the viewer and the docs quote (fitness/judge/calibration_max_native_v3.json).

Per window of the held-out reels, both nets score the clip and the larger of the two counts (each net normalized to
its own level), for real footage and for each corruption below, plus the training reels against the held-out ones.

    KERAS_BACKEND=jax python3 -m agenome.calib_v3 OUT.json [--version 3]

A new judge (new references, HOLDOUT or REELS in judge.py) writes a NEW file name and a new --version, and the
judge_v that search.py writes into every row (JudgeState, `self.version = 3`) changes with it.
"""
import argparse, json, time
import numpy as np
from scipy.ndimage import uniform_filter
from .judge import Judge, _windows, sample

ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
ap.add_argument("out")
ap.add_argument("--version", type=int, default=3)
a = ap.parse_args()
Js = [Judge(n, "native", 12) for n in ("inception", "mobilenet")]
J0 = Js[0]
rng = np.random.default_rng(0)
hold_w = [w for p in J0.hold_paths() for w in _windows(np.load(p))]
train_w = [w for p in J0.train_paths() for w in _windows(np.load(p))[::3]]
cases = [
    ("Amoebius, held-out reel", lambda w: w),
    ("Mirrored left to right", lambda w: w[:, :, ::-1].copy()),
    ("Frames shuffled", lambda w: w[rng.permutation(len(w))]),
    ("Hue rotated a third", lambda w: w[..., [1, 2, 0]].copy()),
    ("Frozen on its first frame", lambda w: np.repeat(w[:1], len(w), 0)),
    ("Blurred 9 px", lambda w: uniform_filter(w.astype(np.float32), size=(1, 9, 9, 1)).astype(np.uint8)),
    ("Average colour only", lambda w: np.broadcast_to(w.reshape(-1, 3).mean(0).astype(np.uint8), w.shape).copy()),
    ("Uniform noise", lambda w: rng.integers(0, 256, w.shape, dtype=np.uint8)),
    ("Black", lambda w: np.zeros_like(w)),
]
def score(ws):
    clips = [sample(w, 12) for w in ws]
    per = [J.score_many(clips) for J in Js]          # per[net][window]
    jt = np.array([[p[k]["judge"] for p in per] for k in range(len(ws))])
    jh = np.array([[p[k]["judge_hold"] for p in per] for k in range(len(ws))])
    return jt, jh
out = []
t0 = time.time()
for label, f in cases:
    jt, jh = score([f(w) for w in hold_w])
    out.append({"label": label, "windows": len(hold_w),
                "judge": float(jt.max(1).mean()), "judge_hold": float(jh.max(1).mean()),
                "judge_mean": float(jt.mean(1).mean()), "per_net": [float(x) for x in jt.mean(0)],
                "per_net_hold": [float(x) for x in jh.mean(0)],
                "judge_sd": float(jt.max(1).std())})
    print(f"{label:40s} max {out[-1]['judge']:.3f} (sd {out[-1]['judge_sd']:.3f}) mean {out[-1]['judge_mean']:.3f} "
          f"nets {np.round(jt.mean(0), 3)}  hold-max {out[-1]['judge_hold']:.3f}  {time.time() - t0:.0f}s", flush=True)
jt, jh = score(train_w)
out.insert(1, {"label": "Amoebius, train reels (vs held-out)", "windows": len(train_w),
               "judge": float(jt.max(1).mean()), "judge_hold": float(jh.max(1).mean()),
               "judge_mean": float(jt.mean(1).mean()), "per_net": [float(x) for x in jt.mean(0)],
               "per_net_hold": [float(x) for x in jh.mean(0)], "judge_sd": float(jt.max(1).std())})
print("train windows", out[1])
json.dump({"version": a.version, "agg": "max", "nets": ["inception", "mobilenet"], "n": 12, "holdout": list(J0.holdout),
           "levels": [J.level for J in Js], "levels_hold": [J.level_hold for J in Js], "cases": out},
          open(a.out, "w"), indent=1)
print("wrote", a.out)
