"""Paired losses: a genome frame against the real frame at the same game state (lower is better, 0 = identical).

Everything before this compared distributions of frames; with a recorded session (targets/sessions/<name>) the genome
can be rendered under the recorded camera and state, so the frames can be compared one to one.

    look_loss(a, b)   a, b uint8 HxWx3. Sum of
        pyr   L1 on a 4-level average-pooled pyramid in a perceptual-ish space (sRGB -> opponent luma/chroma),
              coarse levels weighted up so a misplaced thin line still pulls toward the right place
        edge  L1 on Sobel magnitude of luma at two scales (thin neon lines and the web are mostly edges)
        hist  1-D Wasserstein on luma and the two chroma channels (palette and exposure, position-free)
    returns (total, parts dict)
"""
import numpy as np

_M = np.array([[0.299, 0.587, 0.114], [0.5, -0.4187, -0.0813], [-0.1687, -0.3313, 0.5]], np.float32)


def _opp(img):
    return (img.astype(np.float32) / 255.0) @ _M.T


def _pool(x):
    h, w = x.shape[0] // 2 * 2, x.shape[1] // 2 * 2
    x = x[:h, :w]
    return 0.25 * (x[0::2, 0::2] + x[1::2, 0::2] + x[0::2, 1::2] + x[1::2, 1::2])


def _sobel(y):
    p = np.pad(y, 1, mode="edge")
    gx = (p[:-2, 2:] + 2 * p[1:-1, 2:] + p[2:, 2:]) - (p[:-2, :-2] + 2 * p[1:-1, :-2] + p[2:, :-2])
    gy = (p[2:, :-2] + 2 * p[2:, 1:-1] + p[2:, 2:]) - (p[:-2, :-2] + 2 * p[:-2, 1:-1] + p[:-2, 2:])
    return np.sqrt(gx * gx + gy * gy + 1e-8)


def _w1(a, b, n=256):
    q = (np.arange(n) + 0.5) / n
    return float(np.abs(np.quantile(a.ravel(), q) - np.quantile(b.ravel(), q)).mean())


def look_loss(a, b, w_pyr=1.0, w_edge=0.5, w_hist=1.0):
    A, B = _opp(a), _opp(b)
    pyr = 0.0
    edge = 0.0
    wsum = 0.0
    la, lb = A, B
    for lvl in range(4):
        w = 2.0 ** lvl
        pyr += w * float(np.abs(la - lb).mean())
        wsum += w
        if lvl in (0, 2):
            edge += float(np.abs(_sobel(la[..., 0]) - _sobel(lb[..., 0])).mean())
        la, lb = _pool(la), _pool(lb)
    pyr /= wsum
    edge /= 2
    hist = sum(_w1(A[..., c], B[..., c]) for c in range(3)) * (1.0 / 3)
    parts = {"pyr": pyr, "edge": edge, "hist": hist}
    return w_pyr * pyr + w_edge * edge + w_hist * hist, parts
