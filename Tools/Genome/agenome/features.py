"""Fitness features: how much does a clip look and move like Amoebius? (CONTRACT §3)

Distributional, not frame-aligned: a phenotype never replays the same match, so we compare statistics of the
whole clip (or of 4 s windows) against the pooled statistics of the Amoebius target set.

Groups (each a fixed-length vector per window):
  color    - HSV hue x saturation histogram weighted by value + value histogram
  luma     - luminance percentiles, mean, std
  spectrum - radially averaged log power spectrum of luma (texture scale)
  grad     - gradient magnitude histogram + orientation histogram (edges, lines)
  layout   - 16x9 mean image (rgb) + 16x9 temporal std of luma (where the HUD and the world sit)
  motion   - temporal difference histograms at lags 1 and 4 frames + global motion (camera) magnitude
  texture  - random-filter bank responses at two scales (mean |r| and std r per filter; a 'random CNN style')
  blobs    - bright-pixel fraction and connected-component size histogram (prisms, crystals, beams)

distance(cand, target_stats) -> per-group distances normalized by the target set's own spread (1.0 = as far as a
typical Amoebius window is from Amoebius), and 'total' = weighted mean.
"""
import numpy as np

try:
    from scipy import ndimage
except Exception:  # pragma: no cover
    ndimage = None

GROUPS = ["color", "luma", "spectrum", "grad", "layout", "motion", "texture", "blobs"]
WEIGHTS = {"color": 1.5, "luma": 1.0, "spectrum": 1.0, "grad": 1.0, "layout": 1.0, "motion": 1.0, "texture": 1.5,
           "blobs": 0.75}

_rng = np.random.default_rng(1234)
_FILTERS = _rng.normal(size=(16, 5, 5)).astype(np.float32)
_FILTERS -= _FILTERS.mean(axis=(1, 2), keepdims=True)
_FILTERS /= np.linalg.norm(_FILTERS.reshape(16, -1), axis=1)[:, None, None]


def _luma(fr):
    return (fr[..., 0] * 0.299 + fr[..., 1] * 0.587 + fr[..., 2] * 0.114) / 255.0


def _hsv(fr):
    x = fr.astype(np.float32) / 255.0
    mx = x.max(-1)
    mn = x.min(-1)
    d = mx - mn + 1e-6
    r, g, b = x[..., 0], x[..., 1], x[..., 2]
    h = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) / 6.0
    s = d / (mx + 1e-6)
    return h % 1.0, s, mx


def _radial_bins(h, w, n=12):
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.rfftfreq(w)[None, :]
    r = np.sqrt(fx ** 2 + fy ** 2)
    edges = np.geomspace(1.0 / max(h, w), 0.5, n + 1)
    idx = np.digitize(r, edges) - 1
    return idx, n


_RB = {}


def _conv_valid(img, k):
    # img [T,H,W], k [5,5] -> correlation via stride tricks (small kernels)
    T, H, W = img.shape
    s = np.lib.stride_tricks.sliding_window_view(img, (5, 5), axis=(1, 2))  # T,H-4,W-4,5,5
    return np.tensordot(s, k, axes=([3, 4], [0, 1]))


def window_features(frames, sub=2):
    """frames uint8 [T,144,256,3] -> dict group -> vector (one window)."""
    fr = frames[::sub]
    T = len(fr)
    L = _luma(fr)  # T,H,W
    out = {}
    # color
    h, s, v = _hsv(fr[:, ::2, ::2])
    w = v.ravel()
    hs, _, _ = np.histogram2d(h.ravel(), s.ravel(), bins=[12, 4], range=[[0, 1], [0, 1]], weights=w)
    vh, _ = np.histogram(v.ravel(), bins=16, range=(0, 1))
    hs = hs.ravel() / (hs.sum() + 1e-9)
    vh = vh / (vh.sum() + 1e-9)
    out["color"] = np.concatenate([np.sqrt(hs), np.sqrt(vh)])
    # luma
    pct = np.percentile(L, [5, 25, 50, 75, 95, 99], axis=(1, 2)).T  # T,6
    out["luma"] = np.concatenate([pct.mean(0), [L.mean(), L.std(), pct[:, 4].std()]])
    # spectrum
    key = L.shape[1:]
    if key not in _RB:
        _RB[key] = _radial_bins(*key)
    idx, nb = _RB[key]
    F = np.abs(np.fft.rfft2(L - L.mean(axis=(1, 2), keepdims=True))) ** 2
    Pm = F.mean(0)
    spec = np.array([Pm[idx == k].mean() if (idx == k).any() else 0 for k in range(nb)])
    spec = np.log(spec + 1e-9)
    out["spectrum"] = spec - spec.mean()
    # gradient
    gy = np.diff(L, axis=1)[:, :, :-1]
    gx = np.diff(L, axis=2)[:, :-1, :]
    mag = np.sqrt(gx ** 2 + gy ** 2)
    mh, _ = np.histogram(np.log10(mag.ravel() + 1e-4), bins=12, range=(-4, 0))
    ang = np.arctan2(gy, gx) % np.pi
    oh, _ = np.histogram(ang.ravel(), bins=8, range=(0, np.pi), weights=mag.ravel())
    out["grad"] = np.concatenate([np.sqrt(mh / (mh.sum() + 1e-9)), np.sqrt(oh / (oh.sum() + 1e-9))])
    # layout
    small = fr.reshape(T, 9, 16, 16, 16, 3).mean(axis=(2, 4)) / 255.0  # T,9,16,3
    Ls = L.reshape(T, 9, 16, 16, 16).mean(axis=(2, 4))
    out["layout"] = np.concatenate([small.mean(0).ravel(), Ls.std(0).ravel() * 2.0])
    # motion
    d1 = np.abs(np.diff(L, axis=0)) if T > 1 else np.zeros_like(L)
    lag = max(1, 4 // sub)
    d4 = np.abs(L[lag:] - L[:-lag]) if T > lag else d1
    h1, _ = np.histogram(np.log10(d1.ravel() + 1e-3), bins=8, range=(-3, 0))
    h4, _ = np.histogram(np.log10(d4.ravel() + 1e-3), bins=8, range=(-3, 0))
    gm = _global_motion(L[:, ::2, ::2])
    gmh, _ = np.histogram(np.log10(gm + 1e-2), bins=6, range=(-2, 1.5))
    out["motion"] = np.concatenate([np.sqrt(h1 / (h1.sum() + 1e-9)), np.sqrt(h4 / (h4.sum() + 1e-9)),
                                    np.sqrt(gmh / (gmh.sum() + 1e-9)), [d1.mean() * 10, d4.mean() * 10]])
    # texture: random filter bank on luma at full and half res (subsample frames for speed)
    tf = L[:: max(1, T // 6)]
    feats = []
    for img in (tf, tf[:, ::2, ::2]):
        r = np.stack([_conv_valid(img, k) for k in _FILTERS], 0)  # 16,T,h,w
        a = np.abs(r).mean(axis=(1, 2, 3))
        sd = r.std(axis=(1, 2, 3))
        feats += [np.log(a + 1e-5), np.log(sd + 1e-5)]
    out["texture"] = np.concatenate(feats)
    # blobs
    bright = L[:: max(1, T // 8)] > 0.55
    frac = bright.mean()
    sizes = []
    if ndimage is not None:
        for bm in bright:
            lab, n = ndimage.label(bm)
            if n:
                sizes.append(np.bincount(lab.ravel())[1:])
    sz = np.concatenate(sizes) if sizes else np.zeros(1)
    bh, _ = np.histogram(np.log2(sz + 1), bins=10, range=(0, 14), weights=sz)
    out["blobs"] = np.concatenate([[np.sqrt(frac) * 4, np.log1p(len(sz) / max(1, len(bright))) / 3],
                                   np.sqrt(bh / (bh.sum() + 1e-9))])
    return out


def _global_motion(L):
    """Phase-correlation shift magnitude (pixels at the given res) between consecutive frames."""
    if len(L) < 2:
        return np.zeros(1)
    F = np.fft.rfft2(L)
    out = []
    H, W = L.shape[1:]
    for k in range(len(L) - 1):
        R = F[k + 1] * np.conj(F[k])
        R /= np.abs(R) + 1e-9
        c = np.fft.irfft2(R, s=(H, W))
        i = np.unravel_index(np.argmax(c), c.shape)
        dy = i[0] if i[0] <= H // 2 else i[0] - H
        dx = i[1] if i[1] <= W // 2 else i[1] - W
        out.append(np.hypot(dx, dy))
    return np.array(out)


def windows(frames, win=60, hop=30):
    T = len(frames)
    if T <= win:
        return [frames]
    return [frames[s:s + win] for s in range(0, T - win + 1, hop)]


def clip_features(frames, win=60, hop=30, sub=2):
    """List of window feature dicts for a (target) clip."""
    return [window_features(w, sub=sub) for w in windows(frames, win, hop)]


class TargetStats:
    """Pooled statistics of the Amoebius target set + per-group natural spread."""

    def __init__(self, window_feats):
        self.wf = window_feats
        self.mean = {g: np.mean([w[g] for w in window_feats], 0) for g in GROUPS}
        self.std = {g: np.std([w[g] for w in window_feats], 0) + 1e-3 for g in GROUPS}
        raw = [self._raw(w) for w in window_feats]
        self.scale = {g: float(np.median([r[g] for r in raw])) + 1e-6 for g in GROUPS}

    def _raw(self, f):
        return {g: float(np.sqrt(np.mean(((f[g] - self.mean[g]) / (self.std[g] + 0.05)) ** 2))) for g in GROUPS}

    def distance(self, f):
        raw = self._raw(f)
        d = {g: raw[g] / self.scale[g] for g in GROUPS}
        d["total"] = float(sum(WEIGHTS[g] * d[g] for g in GROUPS) / sum(WEIGHTS.values()))
        return d

    def save(self, path):
        np.savez_compressed(path, **{f"{g}__{i}": w[g] for i, w in enumerate(self.wf) for g in GROUPS})

    @classmethod
    def load(cls, path):
        z = np.load(path)
        n = 1 + max(int(k.split("__")[1]) for k in z.files)
        return cls([{g: z[f"{g}__{i}"] for g in GROUPS} for i in range(n)])


def score(frames, stats, sub=2):
    return stats.distance(window_features(frames, sub=sub))
