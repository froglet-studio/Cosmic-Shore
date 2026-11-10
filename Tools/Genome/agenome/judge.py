"""A fixed perceptual judge: ImageNet CNN features of a clip compared against Amoebius frames (KID).

Why: the fixed statistics are gameable. On 2026-11-10 a 10-node genome tuned on them reached Amoebius' own level
(1.05 train, 1.16 held-out) while this judge put it far from Amoebius (KID 395 on the held-out reel, worse than a
9-px blur of the reel at 212), and across 82 genomes from every front the judge's ranking ran AGAINST the statistics
(Spearman -0.36) and WITH the online critic (+0.73). Unlike the critic it never retrains, so the search can climb it
and a score means the same thing tomorrow.

A clip is N frames sampled evenly from its 60 (default 12), embedded at its own 256x144 size (no resize; the CNN is
fully convolutional and average-pooled) by Keras' InceptionV3 on the JAX backend; MobileNetV2 is the second opinion.
Scores against a reference set of Amoebius frames:

  kid_train, kid_hold   kernel inception distance x 1000 (unbiased MMD^2, cubic kernel) against the training reels
                        (1 to 4) and against the held-out reel 5
  judge, judge_hold     the same, over the level Amoebius itself reaches: the held-out reel's 4-second windows against
                        the training reels set judge = 1.0, the training reels' windows against reel 5 set judge_hold = 1.0

The search climbs `judge` (train reels) and reports `judge_hold`; a genome that only fits reels 1 to 4 shows up there.

  python3 -m agenome.judge calibrate OUT.json [--net inception|mobilenet] [--res native|299]
  python3 -m agenome.judge score OUT.json --front main=pareto.json --front seedbank=... [--genome g.json ...]

Weights: ~/.keras/models, else /mnt/project-files/amoebius-genome/fitness/judge/ (copied there once), else fetched from
storage.googleapis.com. Reference features and calibration are cached per net and size (judge_<net>_<res>.npz).
"""
import argparse
import glob
import json
import os
import sys

os.environ.setdefault("KERAS_BACKEND", "jax")
import numpy as np

REELS = "/mnt/project-files/amoebius-genome/fitness/reels_256x144/reel0*/frames.npy"
HOLDOUT = "reel05"
SHARED = "/mnt/project-files/amoebius-genome/fitness/judge"
N_SAMPLE = 12
NETS = {
    "inception": ("InceptionV3", "inception_v3_weights_tf_dim_ordering_tf_kernels_notop.h5"),
    "mobilenet": ("MobileNetV2", "mobilenet_v2_weights_tf_dim_ordering_tf_kernels_1.0_224_no_top.h5"),
}


def _weights(fname):
    for d in (os.path.expanduser("~/.keras/models"), SHARED):
        p = os.path.join(d, fname)
        if os.path.exists(p):
            return p
    return "imagenet"


def sample(frames, n=N_SAMPLE):
    frames = np.asarray(frames)
    if not n or len(frames) <= n:
        return frames
    return frames[np.linspace(0, len(frames) - 1, n).round().astype(int)]


def kid(X, Y):
    d = X.shape[1]
    kxx = (X @ X.T / d + 1.0) ** 3
    kyy = (Y @ Y.T / d + 1.0) ** 3
    kxy = (X @ Y.T / d + 1.0) ** 3
    m, n = len(X), len(Y)
    v = (kxx.sum() - np.trace(kxx)) / (m * (m - 1)) + (kyy.sum() - np.trace(kyy)) / (n * (n - 1)) - 2 * kxy.mean()
    return float(v * 1000.0)


def _windows(frames, win=60, hop=30):
    return [frames[i:i + win] for i in range(0, len(frames) - win + 1, hop)]


class Judge:
    def __init__(self, net="inception", res="native", n=N_SAMPLE, reels=REELS, holdout=HOLDOUT, cache_dir=None,
                 build=True):
        import keras
        self.net, self.res, self.n = net, res, n
        cls, fname = NETS[net]
        self.shape = (144, 256, 3) if res == "native" else (299, 299, 3)
        self.model = getattr(keras.applications, cls)(weights=_weights(fname), include_top=False, pooling="avg",
                                                      input_shape=self.shape)
        self.paths = sorted(glob.glob(reels))
        self.holdout = holdout
        cache_dir = cache_dir or os.path.expanduser("~/.cache/agenome")
        self.cache = os.path.join(cache_dir, f"judge_{net}_{res}.npz")
        for c in (self.cache, os.path.join(SHARED, os.path.basename(self.cache))):
            if os.path.exists(c):
                z = np.load(c)
                self.train, self.hold = z["train"], z["hold"]
                self.level, self.level_hold = float(z["level"]), float(z["level_hold"])
                return
        if not build:
            raise FileNotFoundError(self.cache)
        tr = np.concatenate([np.load(p)[::2] for p in self.paths if holdout not in p])
        ho = np.concatenate([np.load(p) for p in self.paths if holdout in p])
        self.train, self.hold = self.embed(tr), self.embed(ho)
        self.level = self.level_hold = 1.0
        hw = [self.raw(w) for w in _windows(ho)]
        tw = [self.raw(w) for p in self.paths if holdout not in p for w in _windows(np.load(p))[::3]]
        self.level = float(np.mean([r["kid_train"] for r in hw]))
        self.level_hold = float(np.mean([r["kid_hold"] for r in tw]))
        os.makedirs(cache_dir, exist_ok=True)
        np.savez(self.cache, train=self.train, hold=self.hold, level=self.level, level_hold=self.level_hold)

    def embed(self, frames, batch=64):
        """uint8 (N, 144, 256, 3) -> float32 (N, d)."""
        x = np.asarray(frames)
        if x.shape[1:] != self.shape:
            from PIL import Image
            x = np.stack([np.asarray(Image.fromarray(np.ascontiguousarray(f)).resize(self.shape[1::-1], Image.BILINEAR))
                          for f in x])
        x = x.astype(np.float32) / 127.5 - 1.0  # inception_v3 / mobilenet_v2 preprocess_input
        return np.asarray(self.model.predict(x, batch_size=batch, verbose=0), np.float32)

    def score_feats(self, F):
        kt, kh = kid(F, self.train), kid(F, self.hold)
        return {"kid_train": kt, "kid_hold": kh, "judge": kt / self.level, "judge_hold": kh / self.level_hold}

    def raw(self, frames):
        return self.score_feats(self.embed(sample(frames, self.n)))

    def score_many(self, clips):
        """Several clips (each N frames, already sampled) in one forward pass."""
        if not clips:
            return []
        F = self.embed(np.concatenate(clips))
        out, k = [], 0
        for c in clips:
            out.append(self.score_feats(F[k:k + len(c)]))
            k += len(c)
        return out


def calibration(J):
    """The judge on Amoebius itself and on broken versions of the held-out reel, window by window."""
    from scipy.ndimage import uniform_filter
    rng = np.random.default_rng(0)
    hold = np.concatenate([np.load(p) for p in J.paths if J.holdout in p])
    train_w = [w for p in J.paths if J.holdout not in p for w in _windows(np.load(p))[::3]]
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
    out = []
    for label, f in cases:
        js = [J.raw(f(w)) for w in _windows(hold)]
        out.append({"label": label, **{k: float(np.mean([j[k] for j in js])) for k in js[0]}})
    js = [J.raw(w) for w in train_w]
    out.insert(1, {"label": "Amoebius, train reels (vs held-out)", **{k: float(np.mean([j[k] for j in js])) for k in js[0]}})
    return out


def _load_front(path):
    d = json.load(open(path))
    rows = d if isinstance(d, list) else d.get("members", d.get("pop", []))
    return [r for r in rows if r.get("genome")]


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cmd", choices=["calibrate", "score"])
    ap.add_argument("out")
    ap.add_argument("--net", default="inception", choices=sorted(NETS))
    ap.add_argument("--res", default="native", choices=["native", "299"])
    ap.add_argument("--also", default="mobilenet", help="second-opinion net for `score` ('' for none)")
    ap.add_argument("--n", type=int, default=N_SAMPLE)
    ap.add_argument("--reels", default=REELS)
    ap.add_argument("--front", action="append", default=[], help="name=pareto.json (any island's or the seedbank's)")
    ap.add_argument("--genome", nargs="*", default=[])
    ap.add_argument("--frames", type=int, default=60)
    a = ap.parse_args(argv)
    J = Judge(a.net, a.res, a.n, a.reels)
    if a.cmd == "calibrate":
        res = calibration(J)
        print(f"level (held-out windows vs train reels) {J.level:.1f}; level_hold (train windows vs reel 5) {J.level_hold:.1f}")
        for r in res:
            print(f"{r['label']:40s} judge {r['judge']:6.2f}  judge_hold {r['judge_hold']:6.2f}  "
                  f"kid_train {r['kid_train']:7.1f}  kid_hold {r['kid_hold']:7.1f}", flush=True)
        json.dump({"net": a.net, "res": a.res, "n": a.n, "level": J.level, "level_hold": J.level_hold, "cases": res},
                  open(a.out, "w"), indent=1)
        return
    J2 = Judge(a.also, a.res, a.n, a.reels) if a.also else None
    from .genome import Genome
    from .render import Renderer
    R = Renderer()
    items = [(name, r) for spec in a.front for name, path in [spec.split("=", 1)] for r in _load_front(path)]
    items += [(os.path.basename(p), {"genome": json.load(open(p))}) for p in a.genome]
    out = []
    for name, r in items:
        try:
            g = Genome.from_json(r["genome"])
            fr = sample(R.render(g, a.frames), a.n)
            j = J.score_many([fr])[0]
            j2 = J2.score_many([fr])[0] if J2 else {}
        except Exception as e:  # a genome that no longer renders is reported, not fatal
            print(f"{name}: {type(e).__name__}: {e}", file=sys.stderr)
            continue
        meta = (r.get("genome", {}).get("meta", {}) or {})
        row = {"front": name, "zbytes": g.size(), "nodes": len(g.nodes), "stat": r.get("stat"), "hold": r.get("hold"),
               "crit": r.get("crit"), "dist": r.get("dist"), "src": meta.get("name") or r.get("src") or r.get("name"),
               **j, **{f"{a.also}_{k}": v for k, v in j2.items()}}
        out.append(row)
        print(f"{name:10s} z {row['zbytes']:5d} stat {row['stat'] or 0:6.3f} judge {j['judge']:5.2f} "
              f"judge_hold {j['judge_hold']:5.2f}" + (f"  {a.also} {j2['judge']:5.2f}/{j2['judge_hold']:5.2f}" if j2 else "")
              + f"  {str(row['src'])[:40]}", flush=True)
    json.dump(out, open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
