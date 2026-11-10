"""Baked texture ops: spectral (FFT), linear-algebra (SVD, PCA, eigen-solves), PDE and fractal operators.

All return float32 [N, N, 3] arrays indexed [v, u] (row = uv.y). FFT-made fields are periodic, so they tile.
Byte params follow the SPEC kinds; colour ramps reuse the core's two-colour (a, b) HSV convention.
"""
import math
from functools import lru_cache

import numpy as np

from agenome import glsl
from agenome.ops import Op, P

from .registry import register, hsvp
from .bake import N

TAU = 2 * math.pi
LUMA = np.array([0.299, 0.587, 0.114], np.float32)


# ---------------------------------------------------------------------- helpers
def _rgb(d, pre):
    return np.array(glsl.hsv(d, pre), np.float32)


def ramp(x, d, gamma=1.0):
    x = np.clip(np.nan_to_num(x), 0.0, 1.0)
    if gamma != 1.0:
        x = x ** gamma
    a, b = _rgb(d, "a"), _rgb(d, "b")
    return (a[None, None, :] * (1 - x[..., None]) + b[None, None, :] * x[..., None]).astype(np.float32)


def norm01(x, lo=1.0, hi=99.0):
    a, b = np.percentile(x, [lo, hi])
    return np.clip((x - a) / (b - a + 1e-9), 0, 1)


def zstd(x):
    return (x - x.mean()) / (x.std() + 1e-9)


def luma(img):
    return img @ LUMA


@lru_cache(maxsize=4)
def fgrid(n=N):
    fy = np.fft.fftfreq(n)[:, None] * n  # cycles per tile
    fx = np.fft.fftfreq(n)[None, :] * n
    return fx.astype(np.float32), fy.astype(np.float32)


def _rot_grid(angle, aniso, n=N):
    fx, fy = fgrid(n)
    c, s = math.cos(angle), math.sin(angle)
    u = fx * c + fy * s
    v = (-fx * s + fy * c) / max(1e-3, 1.0 - aniso)
    return u, v


def mixout(src, out, amt):
    return (src * (1 - amt) + out * amt).astype(np.float32)


def resize(a, n=N, order=3):
    from scipy import ndimage
    if a.shape[0] == n and a.shape[1] == n:
        return a
    z = (n / a.shape[0], n / a.shape[1]) + ((1,) if a.ndim == 3 else ())
    return ndimage.zoom(a, z, order=order, mode="grid-wrap" if order else "nearest")


def _input_or_noise(img, seed):
    if img is not None:
        return img
    rng = np.random.default_rng(seed)
    return np.repeat(rng.random((N, N, 1), np.float32), 3, 2)


GAMMA = P("gamma", "exp", 0.25, 4, 128)
DRIFT = P("drift", "scub", 0, 4, 128)
MIX = P("mix", "lin", 0, 1, 255)


# ---------------------------------------------------------------------- 0xA0 tex.spectral
def bake_spectral(d, ins, seed=0):
    rng = np.random.default_rng(int(d["seed"]) * 977 + seed)
    u, v = _rot_grid(d["angle"], d["aniso"])
    r = np.sqrt(u * u + v * v)
    r[0, 0] = 1e9
    beta, fc, bw = d["beta"], d["fc"], d["bw"]
    amp = r ** (-beta * 0.5) * np.exp(-0.5 * (np.log2(np.maximum(r, 1e-6) / fc) / bw) ** 2)
    amp[0, 0] = 0
    ph = rng.random((N, N)) * TAU
    F = amp * np.exp(1j * ph)
    x = zstd(np.fft.ifft2(F).real)
    m = int(d["mode"])
    if m == 0:  # gaussian random field
        y = 0.5 + 0.5 * np.tanh(0.8 * x)
    elif m == 1:  # ridged (sharp crests: mountain ridges, filaments)
        y = (1 - np.tanh(np.abs(x) * 1.2)) ** 2
    elif m == 2:  # billow
        y = np.tanh(np.abs(x) * 0.9)
    elif m == 3:  # contour / level-set lines
        lv = max(1, int(d["levels"]))
        y = 0.5 + 0.5 * np.cos(TAU * lv * (0.5 + 0.5 * np.tanh(0.6 * x)))
        y = y ** 4
    else:  # nodal veins: thin bright lines where the field crosses zero
        w = 0.06 * (17 - max(1, int(d["levels"]))) / 16 + 0.01
        y = np.exp(-(x / w) ** 2)
    return ramp(y, d, d["gamma"])


register(Op(0xA0, "tex.spectral", "T", (), (
    P("beta", "exp", 0.25, 5, 212), P("fc", "exp", 0.5, 96, 90), P("bw", "exp", 0.15, 12, 200),
    P("aniso", "lin", 0, 0.95, 0), P("angle", "lin", 0, math.pi, 0), P("mode", "enum", 0, 5, 0),
    P("levels", "int", 1, 16, 60), P("seed", "int", 0, 255, 0), GAMMA, DRIFT) + hsvp("a", 175, 230, 25) + hsvp("b", 195, 150, 140),
    "FFT spectral synthesis: power-law (1/f^beta) x log-gaussian band at fc, anisotropic; field/ridged/billow/contours/veins",
    "spectral synthesis (Voss / Musgrave)"), family="spectral", bake_t=bake_spectral)


# ---------------------------------------------------------------------- 0xA1 tex.fftfilter
def bake_fftfilter(d, ins, seed=0):
    img = _input_or_noise(ins[0] if ins else None, seed)
    u, v = _rot_grid(d["angle"], 0.0)
    r = np.sqrt(u * u + v * v)
    fc, bw = d["fc"], d["bw"]
    m = int(d["mode"])
    rr = np.maximum(r, 1e-6)
    keep_mean = True
    if m == 0:  # gaussian low-pass = blur (werkkzeug Blur, the corpus' most-dropped op)
        H = np.exp(-0.5 * (r / fc) ** 2)
    elif m == 1:  # high-pass
        H = 1 - np.exp(-0.5 * (r / fc) ** 2)
    elif m == 2:  # log-gaussian band-pass
        H = np.exp(-0.5 * (np.log2(rr / fc) / bw) ** 2)
    elif m == 3:  # oriented band (directional streaks, anamorphic)
        ang = np.arctan2(v, u)
        da = np.angle(np.exp(2j * ang))  # orientation, pi-periodic
        H = np.exp(-0.5 * (da / (0.05 + 0.4 * bw)) ** 2) * np.exp(-0.5 * (r / (fc * 4)) ** 2)
        H[0, 0] = 1
    elif m == 4:  # spectral whitening (keeps phase: edges and structure)
        H = None
    else:  # power-law tilt: |f|^tilt (tilt from bw: <1 smooths, >1 sharpens)
        H = rr ** (math.log2(bw) * 0.75)
        H[0, 0] = 1
    out = np.empty_like(img)
    for c in range(3):
        F = np.fft.fft2(img[..., c])
        if H is None:
            mag = np.abs(F) + 1e-6
            G = F / mag ** (min(1.0, bw / 4))
            G[0, 0] = F[0, 0]
        else:
            G = F * H
        y = np.fft.ifft2(G).real
        if m in (1, 2, 4) and keep_mean:
            y = (y - y.mean()) / (y.std() + 1e-9) * img[..., c].std() + img[..., c].mean()
        out[..., c] = y
    out = (out - out.mean()) * d["gain"] + out.mean()
    return mixout(img, out, d["mix"])


register(Op(0xA1, "tex.fftfilter", "T", ("T",), (
    P("mode", "enum", 0, 6, 0), P("fc", "exp", 0.5, 128, 120), P("bw", "exp", 0.1, 8, 128),
    P("angle", "lin", 0, math.pi, 0), P("gain", "exp", 0.25, 8, 128), MIX, DRIFT),
    "Fourier-domain filter: blur / high-pass / band-pass / oriented streaks / whitening / power-law tilt",
    "werkkzeug Blur, generalized"), family="spectral", bake_t=bake_fftfilter)


# ---------------------------------------------------------------------- 0xA2 tex.convolve
def bake_convolve(d, ins, seed=0):
    a = _input_or_noise(ins[0] if ins else None, seed)
    b = ins[1] if len(ins) > 1 and ins[1] is not None else None
    k = max(3, int(round(d["size"] * N)))
    if b is None:
        yy, xx = np.mgrid[-1:1:k * 1j, -1:1:k * 1j]
        kern = np.exp(-4 * (xx ** 2 + yy ** 2))
    else:
        kern = resize(luma(b), k, order=1)
    kern = np.maximum(kern - (np.median(kern) if int(d["mode"]) == 2 else 0), 0) if int(d["mode"]) != 1 else kern - kern.mean()
    K = np.zeros((N, N), np.float32)
    K[:k, :k] = kern
    K = np.roll(K, (-(k // 2), -(k // 2)), (0, 1))
    if int(d["mode"]) != 1:
        K /= K.sum() + 1e-9
    else:
        K /= np.abs(K).sum() + 1e-9
    FK = np.fft.fft2(K)
    out = np.empty_like(a)
    src = a ** 2.2 if int(d["mode"]) == 2 else a  # bokeh: convolve in light, not in gamma
    for c in range(3):
        y = np.fft.ifft2(np.fft.fft2(src[..., c]) * (np.conj(FK) if int(d["mode"]) == 1 else FK)).real
        out[..., c] = np.abs(y) * 4 if int(d["mode"]) == 1 else y
    if int(d["mode"]) == 2:
        out = np.maximum(out, 0) ** (1 / 2.2)
    return mixout(a, out * d["gain"], d["mix"])


register(Op(0xA2, "tex.convolve", "T", ("T", "t"), (
    P("mode", "enum", 0, 3, 0), P("size", "exp", 0.01, 1, 128), P("gain", "exp", 0.25, 8, 128), MIX, DRIFT),
    "FFT convolution of a by kernel b (any texture is a kernel): smear / matched-filter correlation / bokeh",
    "convolution theorem"), family="spectral", bake_t=bake_convolve)


# ---------------------------------------------------------------------- 0xA3 tex.phaseswap
def bake_phaseswap(d, ins, seed=0):
    a = _input_or_noise(ins[0] if ins else None, seed)
    b = ins[1] if len(ins) > 1 and ins[1] is not None else _input_or_noise(None, seed + 1)
    t = d["amount"]
    out = np.empty_like(a)
    for c in range(3):
        Fa, Fb = np.fft.fft2(a[..., c]), np.fft.fft2(b[..., c])
        ua, ub = Fa / (np.abs(Fa) + 1e-9), Fb / (np.abs(Fb) + 1e-9)
        u = ua * (1 - t) + ub * t
        u /= np.abs(u) + 1e-9
        y = np.fft.ifft2(np.abs(Fa) * u).real
        out[..., c] = (y - y.mean()) / (y.std() + 1e-9) * a[..., c].std() + a[..., c].mean()
    return out * d["gain"]


register(Op(0xA3, "tex.phaseswap", "T", ("T", "t"), (
    P("amount", "lin", 0, 1, 255), P("gain", "exp", 0.25, 4, 128), DRIFT),
    "Fourier magnitude of a with the phase of b (structure of b, texture statistics of a)",
    "Oppenheim-Lim phase swap"), family="spectral", bake_t=bake_phaseswap)


# ---------------------------------------------------------------------- 0xA4 tex.svd
def bake_svd(d, ins, seed=0):
    img = _input_or_noise(ins[0] if ins else None, seed)
    k = max(1, int(d["rank"]))
    m = int(d["mode"])
    out = np.empty_like(img)
    for c in range(3):
        X = img[..., c].astype(np.float64)
        U, s, Vt = np.linalg.svd(X, full_matrices=False)
        if m == 0:  # rank-k approximation: plaid / woven abstraction
            w = np.zeros_like(s)
            w[:k] = 1
        elif m == 1:  # residual: drop the k largest (removes broad shading, keeps detail)
            w = np.ones_like(s)
            w[:k] = 0
        elif m == 2:  # a single singular band
            w = np.zeros_like(s)
            w[min(k - 1, len(s) - 1)] = 1.0 * s[0] / (s[min(k - 1, len(s) - 1)] + 1e-9) ** 0.5 / (s[0] ** 0.5 + 1e-9)
        else:  # singular-value power: s^p (p<1 flattens the spectrum: crisper, p>1 smoother)
            p = d["gain"]
            w = (s / (s[0] + 1e-12)) ** (p - 1)
        y = (U * (s * w)) @ Vt
        if m in (1, 2):
            y = (y - y.mean()) / (y.std() + 1e-9) * X.std() + X.mean()
        out[..., c] = y
    if m != 3:
        out = (out - out.mean()) * d["gain"] + out.mean()
    return mixout(img, out, d["mix"])


register(Op(0xA4, "tex.svd", "T", ("T",), (
    P("mode", "enum", 0, 4, 0), P("rank", "int", 1, 64, 30), P("gain", "exp", 0.25, 4, 128), MIX, DRIFT),
    "singular value decomposition per channel: rank-k approximation / residual / single band / spectrum power",
    "SVD low-rank"), family="linalg", bake_t=bake_svd)


# ---------------------------------------------------------------------- 0xA5 tex.pca
def _patches(L, p=8, stride=4):
    from numpy.lib.stride_tricks import sliding_window_view
    W = sliding_window_view(np.pad(L, ((0, p), (0, p)), mode="wrap"), (p, p))[:N:stride, :N:stride]
    return W.reshape(-1, p * p)


@lru_cache(maxsize=16)
def _eigpatch_cached(key, Lbytes, p):
    L = np.frombuffer(Lbytes, np.float32).reshape(N, N)
    X = _patches(L, p)
    X = X - X.mean(1, keepdims=True)
    C = X.T @ X / len(X)
    w, V = np.linalg.eigh(C)
    return V[:, ::-1].T.reshape(-1, p, p).astype(np.float32)  # eigenpatches, largest first


def bake_pca(d, ins, seed=0):
    img = _input_or_noise(ins[0] if ins else None, seed)
    m = int(d["mode"])
    if m in (0, 1):
        X = img.reshape(-1, 3).astype(np.float64)
        mu = X.mean(0)
        Xc = X - mu
        C = Xc.T @ Xc / len(Xc)
        lam, V = np.linalg.eigh(C)
        lam, V = lam[::-1], V[:, ::-1]
        Y = Xc @ V
        if m == 0:  # reweight principal colour axes + rotate the chroma plane (pc2, pc3)
            Y = Y * np.array([d["w1"], d["w2"], d["w3"]])
            ca, sa = math.cos(d["rot"]), math.sin(d["rot"])
            Y[:, 1], Y[:, 2] = ca * Y[:, 1] - sa * Y[:, 2], sa * Y[:, 1] + ca * Y[:, 2]
        else:  # whitening: equalize the colour axes (decorrelated, saturated palette)
            sd = np.sqrt(np.maximum(lam, 1e-8))
            Y = Y / sd * sd.mean() * np.array([d["w1"], d["w2"], d["w3"]])
        out = (Y @ V.T + mu).reshape(img.shape).astype(np.float32)
    else:  # patch PCA: eigenfilter responses (the data's own oriented edge / blob detectors)
        p = 8
        L = luma(img).astype(np.float32)
        E = _eigpatch_cached(hash(L.tobytes()), L.tobytes(), p)
        k = min(int(d["k"]), len(E) - 1)
        K = np.zeros((N, N), np.float32)
        K[:p, :p] = E[k]
        K = np.roll(K, (-(p // 2), -(p // 2)), (0, 1))
        FK = np.fft.fft2(K)
        R = np.fft.ifft2(np.fft.fft2(L) * np.conj(FK)).real
        if m == 2:  # rectified response
            y = norm01(np.abs(R)) * d["w1"]
            out = img * y[..., None] if d["w2"] > 1.0 else np.repeat(y[..., None], 3, 2)
        else:  # projection back: the structure carried by eigenpatch k alone, added to the image
            B = np.fft.ifft2(np.fft.fft2(R) * FK).real
            B = B / (np.abs(B).max() + 1e-9)
            out = img + (B * d["w1"])[..., None] * np.array([1, 1, 1], np.float32)
    return mixout(img, out, d["mix"])


register(Op(0xA5, "tex.pca", "T", ("T",), (
    P("mode", "enum", 0, 4, 0), P("k", "int", 0, 15, 16), P("w1", "exp", 0.1, 10, 128), P("w2", "exp", 0.1, 10, 128),
    P("w3", "exp", 0.1, 10, 128), P("rot", "lin", 0, TAU, 0), MIX, DRIFT),
    "principal components: colour-axis reweight+chroma rotation / colour whitening / patch-eigenfilter response / projection",
    "PCA / Karhunen-Loeve"), family="linalg", bake_t=bake_pca)


# ---------------------------------------------------------------------- 0xA6 tex.eigen (Laplacian eigenmodes, Chladni)
M_EIG = 64


def _domain(dom, aspect, M=M_EIG):
    yy, xx = np.mgrid[-1:1:M * 1j, -1:1:M * 1j]
    xx = xx * aspect
    r = np.hypot(xx, yy)
    th = np.arctan2(yy, xx)
    if dom == 0:
        m = (np.abs(xx) < 0.98) & (np.abs(yy) < 0.98)
    elif dom == 1:
        m = r < 0.98
    elif dom == 2:
        m = (r < 0.98) & (r > 0.4)
    elif dom == 3:
        m = r < 0.62 + 0.3 * np.cos(5 * th)
    else:
        m = np.ones((M, M), bool)  # periodic torus
    return m


@lru_cache(maxsize=24)
def _eigsolve(dom, aspect_b, pot_b, maskbytes, potbytes, k):
    from scipy.sparse import diags, kron, identity, csr_matrix
    from scipy.sparse.linalg import eigsh
    M = M_EIG
    aspect = 0.5 * 4 ** (aspect_b / 255)
    if maskbytes is not None:
        mask = np.frombuffer(maskbytes, bool).reshape(M, M)
    else:
        mask = _domain(dom, aspect)
    per = dom == 4 and maskbytes is None
    e = np.ones(M)
    D = diags([e[:-1], -2 * e, e[:-1]], [-1, 0, 1], (M, M)).tolil()
    if per:
        D[0, M - 1] = D[M - 1, 0] = 1
    D = csr_matrix(D)
    I = identity(M)
    Lap = kron(I, D) + kron(D, I) * (1.0 / aspect ** 2)
    idx = np.nonzero(mask.ravel())[0]
    A = -Lap.tocsr()[idx][:, idx]
    if potbytes is not None:
        V = np.frombuffer(potbytes, np.float32).reshape(M, M).ravel()[idx]
        A = A + diags(V * (0.002 * 10 ** (pot_b / 255 * 5)))
    k = min(k, len(idx) - 2)
    w, vecs = eigsh(A.tocsc(), k=k, sigma=-1e-3, which="LM")
    o = np.argsort(w)
    full = np.zeros((k, M * M), np.float32)
    full[:, idx] = vecs[:, o].T
    return full.reshape(k, M, M), mask


def bake_eigen(d, ins, seed=0):
    from scipy import ndimage
    M = M_EIG
    inp = ins[0] if ins else None
    dom = int(d["dom"])
    maskbytes = potbytes = None
    if inp is not None:
        Ls = resize(luma(inp), M, order=1).astype(np.float32)
        if dom == 5:
            maskbytes = (Ls > np.median(Ls)).tobytes()
        else:
            potbytes = norm01(Ls).astype(np.float32).tobytes()
    m1, m2 = int(d["mode"]), int(d["mode2"])
    kneed = max(m1, m2) + 2
    kq = 16 if kneed <= 16 else (32 if kneed <= 32 else 64)
    vecs, mask = _eigsolve(min(dom, 4), int(round((math.log(d["aspect"] / 0.5) / math.log(4)) * 255)),
                           int(round(math.log10(d["pot"] / 0.01) / 5 * 255)), maskbytes, potbytes, kq)
    v1 = vecs[min(m1, len(vecs) - 1)]
    v2 = vecs[min(m2, len(vecs) - 1)]
    psi = v1 * math.cos(d["w2"] * math.pi / 2) + v2 * math.sin(d["w2"] * math.pi / 2)
    psi = psi / (np.abs(psi).max() + 1e-9)
    psi = ndimage.zoom(psi, N / M, order=3)
    msk = ndimage.zoom(mask.astype(np.float32), N / M, order=1)
    r = int(d["render"])
    s = d["sharp"]
    if r == 0:
        y = 0.5 + 0.5 * psi
    elif r == 1:
        y = np.minimum(1, (psi ** 2) * s * 0.3)
    elif r == 2:  # Chladni: sand gathers on the nodal lines
        y = np.exp(-(psi * s) ** 2)
    else:
        y = 0.5 + 0.5 * np.cos(psi * s)
    y = y * np.clip(msk, 0, 1)
    return ramp(y, d)


register(Op(0xA6, "tex.eigen", "T", ("t",), (
    P("dom", "enum", 0, 6, 40), P("mode", "int", 0, 60, 40), P("mode2", "int", 0, 60, 70), P("w2", "lin", 0, 1, 0),
    P("aspect", "exp", 0.5, 2, 128), P("pot", "exp", 0.01, 1000, 128), P("render", "enum", 0, 4, 160),
    P("sharp", "exp", 1, 60, 128), DRIFT) + hsvp("a", 0, 0, 20) + hsvp("b", 150, 120, 220),
    "Laplacian eigenmodes of a domain (rect / disk / annulus / star / torus / input mask), or Schrodinger states "
    "in the input's potential: signed field / density / Chladni nodal lines / contours",
    "spectral geometry (Chladni, Kac)"), family="linalg", bake_t=bake_eigen)


# ---------------------------------------------------------------------- 0xA7 tex.rd (Gray-Scott)
def gray_scott(f, k, M, steps, init, seed, inp=None):
    rng = np.random.default_rng(seed)
    U = np.ones((M, M), np.float32)
    V = np.zeros((M, M), np.float32)
    if init == 1 and inp is not None:
        V = (resize(norm01(luma(inp)), M, order=1) > 0.6).astype(np.float32) * 0.5
    elif init == 2:
        yy, xx = np.mgrid[:M, :M]
        V[np.hypot(xx - M / 2, yy - M / 2) < M / 10] = 0.5
    elif init == 3:
        V[:, ::max(4, M // 12)] = 0.5
    else:
        nspots = max(4, M // 6)
        for _ in range(nspots):
            x, y = rng.integers(0, M, 2)
            V[max(0, y - 2):y + 3, max(0, x - 2):x + 3] = 0.5
    V += rng.random((M, M)).astype(np.float32) * 0.02
    U -= V
    Du, Dv, dt = 0.2097, 0.105, 1.0
    for _ in range(steps):
        Lu = np.roll(U, 1, 0) + np.roll(U, -1, 0) + np.roll(U, 1, 1) + np.roll(U, -1, 1) - 4 * U
        Lv = np.roll(V, 1, 0) + np.roll(V, -1, 0) + np.roll(V, 1, 1) + np.roll(V, -1, 1) - 4 * V
        uvv = U * V * V
        U += dt * (Du * Lu - uvv + f * (1 - U))
        V += dt * (Dv * Lv + uvv - (f + k) * V)
    return U, V


def bake_rd(d, ins, seed=0):
    f = d["feed"]
    kc = math.sqrt(f) / 2 - f  # saddle-node boundary of the homogeneous state (Pearson / Munafo map)
    k = max(0.01, kc - d["delta"])
    M = [64, 96, 128][int(d["scale"])]
    steps = int(d["steps"]) * 250
    U, V = gray_scott(f, k, M, steps, int(d["init"]), int(d["seed"]) + seed * 31, ins[0] if ins else None)
    o = int(d["out"])
    y = norm01(V) if o == 0 else (norm01(U) if o == 1 else norm01(np.hypot(*np.gradient(V))))
    return ramp(resize(y.astype(np.float32), N, order=3), d)


register(Op(0xA7, "tex.rd", "T", ("t",), (
    P("feed", "lin", 0.01, 0.09, 61), P("delta", "exp", 0.0005, 0.03, 60), P("scale", "enum", 0, 3, 128),
    P("steps", "int", 2, 16, 128), P("init", "enum", 0, 4, 0), P("out", "enum", 0, 3, 0), P("seed", "int", 0, 255, 0), DRIFT)
    + hsvp("a", 0, 0, 10) + hsvp("b", 120, 180, 220),
    "Gray-Scott reaction-diffusion; (feed, delta below the Pearson boundary) parametrisation keeps most bytes patterned",
    "Turing / Gray-Scott / Pearson 1993"), family="pde", bake_t=bake_rd)


# ---------------------------------------------------------------------- 0xA8 tex.wavelet (Haar)
def haar2(x, levels):
    coefs = []
    a = x
    for _ in range(levels):
        a00, a01, a10, a11 = a[0::2, 0::2], a[0::2, 1::2], a[1::2, 0::2], a[1::2, 1::2]
        lo = (a00 + a01 + a10 + a11) / 2
        h = (a00 - a01 + a10 - a11) / 2
        v = (a00 + a01 - a10 - a11) / 2
        dd = (a00 - a01 - a10 + a11) / 2
        coefs.append((h, v, dd))
        a = lo
    return a, coefs


def ihaar2(a, coefs):
    for h, v, dd in reversed(coefs):
        n = a.shape[0] * 2
        x = np.empty((n, n), a.dtype)
        x[0::2, 0::2] = (a + h + v + dd) / 2
        x[0::2, 1::2] = (a - h + v - dd) / 2
        x[1::2, 0::2] = (a + h - v - dd) / 2
        x[1::2, 1::2] = (a - h - v + dd) / 2
        a = x
    return a


def bake_wavelet(d, ins, seed=0):
    img = _input_or_noise(ins[0] if ins else None, seed)
    L = int(math.log2(N))
    keep, ori = int(d["keep"]), int(d["orient"])
    out = np.empty_like(img)
    for c in range(3):
        a, co = haar2(img[..., c].astype(np.float64), L)
        new = []
        for lev, (h, v, dd) in enumerate(co):
            on = (keep >> lev) & 1
            bands = []
            for b_i, band in enumerate((h, v, dd)):
                g = d["gain"] if (on and (ori >> b_i) & 1) else 0.0
                t = d["shrink"] * np.abs(band).max()
                band = np.sign(band) * np.maximum(np.abs(band) - t, 0)
                bands.append(band * g)
            new.append(tuple(bands))
        out[..., c] = ihaar2(a * d["approx"], new) + img[..., c].mean() * (1 - d["approx"])
    return mixout(img, out, d["mix"])


register(Op(0xA8, "tex.wavelet", "T", ("T",), (
    P("keep", "bits", 0, 255, 255), P("orient", "bits", 0, 7, 7), P("shrink", "lin", 0, 0.5, 0),
    P("gain", "exp", 0.25, 8, 128), P("approx", "lin", 0, 1, 255), MIX, DRIFT),
    "Haar wavelet band select / gain / soft-threshold per level and orientation (blocky multiscale, tech look)",
    "Haar / Mallat"), family="spectral", bake_t=bake_wavelet)


# ---------------------------------------------------------------------- 0xA9 tex.poisson
def bake_poisson(d, ins, seed=0):
    img = _input_or_noise(ins[0] if ins else None, seed)
    L = luma(img)
    fx, fy = fgrid()
    k2 = (TAU / N) ** 2 * (fx ** 2 + fy ** 2)
    k2[0, 0] = 1
    F = np.fft.fft2(L - L.mean())
    m = int(d["mode"])
    s = d["scale"]
    if m in (0, 2):  # potential (inverse Laplacian), or its equipotential contours
        u = np.fft.ifft2(-F / k2 * (k2 > 0)).real
        u = zstd(u)
        if m == 0:
            y = 0.5 + 0.5 * np.tanh(u * 0.7 * s)
        else:
            y = (0.5 + 0.5 * np.cos(TAU * max(1, int(d["levels"])) * norm01(u))) ** 3
        out = np.repeat(y[..., None], 3, 2) * (img.mean((0, 1)) / (img.mean() + 1e-6))
    elif m == 1:  # field strength |grad u|
        u = np.fft.ifft2(-F / k2).real
        gx = np.fft.ifft2(np.fft.fft2(u) * 1j * TAU * fx / N).real
        gy = np.fft.ifft2(np.fft.fft2(u) * 1j * TAU * fy / N).real
        y = norm01(np.hypot(gx, gy) * s)
        out = np.repeat(y[..., None], 3, 2)
    elif m == 3:  # bump / emboss lighting from height = luma (spectral derivatives) - keeps the colours
        gx = np.fft.ifft2(F * 1j * TAU * fx / N).real * s * 8
        gy = np.fft.ifft2(F * 1j * TAU * fy / N).real * s * 8
        nz = 1 / np.sqrt(1 + gx * gx + gy * gy)
        el = d["elev"]
        Lx, Ly, Lz = math.cos(d["angle"]) * math.cos(el), math.sin(d["angle"]) * math.cos(el), math.sin(el)
        sh = np.maximum((-gx * Lx - gy * Ly + Lz) * nz, 0)
        spec = sh ** 24
        out = img * (0.25 + 0.9 * sh[..., None]) + spec[..., None] * 0.6
    else:  # Laplacian (edges / ridges)
        lap = np.fft.ifft2(-F * k2).real
        y = norm01(np.abs(lap) * s)
        out = np.repeat(y[..., None], 3, 2)
    return mixout(img, out * d["gain"], d["mix"])


register(Op(0xA9, "tex.poisson", "T", ("T",), (
    P("mode", "enum", 0, 5, 0), P("scale", "exp", 0.1, 20, 128), P("angle", "lin", 0, TAU, 40),
    P("elev", "lin", 0.05, 1.5, 128), P("levels", "int", 1, 32, 60), P("gain", "exp", 0.25, 4, 128), MIX, DRIFT),
    "FFT Poisson solve on luma: potential / field strength / equipotentials / bump-emboss lighting / Laplacian",
    "werkkzeug Bump+Normals, spectral"), family="pde", bake_t=bake_poisson)


# ---------------------------------------------------------------------- 0xAA tex.flame (IFS fractal flame)
def _variation(k, x, y):
    r2 = x * x + y * y + 1e-9
    r = np.sqrt(r2)
    th = np.arctan2(x, y)
    if k == 0:
        return x, y
    if k == 1:  # sinusoidal
        return np.sin(x), np.sin(y)
    if k == 2:  # spherical
        return x / r2, y / r2
    if k == 3:  # swirl
        s, c = np.sin(r2), np.cos(r2)
        return x * s - y * c, x * c + y * s
    if k == 4:  # horseshoe
        return (x - y) * (x + y) / r, 2 * x * y / r
    if k == 5:  # polar
        return th / math.pi, r - 1
    if k == 6:  # handkerchief
        return r * np.sin(th + r), r * np.cos(th - r)
    return r * np.sin(th * r), -r * np.cos(th * r)  # heart


def chaos_game(maps, var, var2, vmix, sym, iters=150, walkers=6000, seed=0):
    rng = np.random.default_rng(seed)
    nm = len(maps)
    A = np.array([m[0] for m in maps])
    B = np.array([m[1] for m in maps])
    x = rng.uniform(-1, 1, walkers)
    y = rng.uniform(-1, 1, walkers)
    c = rng.random(walkers)
    pts, cols = [], []
    for it in range(iters):
        j = rng.integers(0, nm, walkers)
        a = A[j]
        nx = a[:, 0] * x + a[:, 1] * y + B[j, 0]
        ny = a[:, 2] * x + a[:, 3] * y + B[j, 1]
        vx1, vy1 = _variation(var, nx, ny)
        if vmix > 0:
            vx2, vy2 = _variation(var2, nx, ny)
            nx, ny = vx1 * (1 - vmix) + vx2 * vmix, vy1 * (1 - vmix) + vy2 * vmix
        else:
            nx, ny = vx1, vy1
        if sym > 1:  # rotational symmetry as extra maps (flam3 'symmetry')
            rot = rng.integers(0, sym, walkers) * (TAU / sym)
            cr, sr = np.cos(rot), np.sin(rot)
            nx, ny = nx * cr - ny * sr, nx * sr + ny * cr
        bad = ~np.isfinite(nx) | ~np.isfinite(ny) | (np.abs(nx) > 1e3) | (np.abs(ny) > 1e3)
        nx[bad] = rng.uniform(-1, 1, bad.sum())
        ny[bad] = rng.uniform(-1, 1, bad.sum())
        x, y = nx, ny
        c = (c + j / max(1, nm - 1)) * 0.5
        if it >= 20:
            pts.append(np.stack([x, y], 1))
            cols.append(c.copy())
    return np.concatenate(pts), np.concatenate(cols)


def splat(pts, cols, zoom, gamma, bright, d, center=True):
    if center:
        med = np.median(pts, 0)
        spread = np.percentile(np.abs(pts - med), 98, axis=0).max() + 1e-6
        q = (pts - med) / spread * 0.45 * zoom + 0.5
    else:
        q = pts
    ok = (q[:, 0] >= 0) & (q[:, 0] < 1) & (q[:, 1] >= 0) & (q[:, 1] < 1)
    q, cols = q[ok], cols[ok]
    ix = (q * N).astype(np.int64)
    flat = ix[:, 1] * N + ix[:, 0]
    cnt = np.bincount(flat, minlength=N * N).astype(np.float32).reshape(N, N)
    csum = np.bincount(flat, weights=cols, minlength=N * N).astype(np.float32).reshape(N, N)
    alpha = np.log1p(cnt) / (np.log1p(cnt.max()) + 1e-9)
    cidx = csum / np.maximum(cnt, 1)
    a, b = _rgb(d, "a"), _rgb(d, "b")
    rgb = a[None, None] * (1 - cidx[..., None]) + b[None, None] * cidx[..., None]
    return (rgb * (alpha ** gamma)[..., None] * bright).astype(np.float32)


def bake_flame(d, ins, seed=0):
    rng = np.random.default_rng(int(d["seed"]) * 7 + seed * 1009)
    nm = int(d["maps"])
    ct = d["contract"]
    maps = []
    for _ in range(nm):
        # linear part = R(t1) diag(s1, s2) R(t2), singular values below the contraction (bounded attractor)
        t1, t2 = rng.uniform(0, TAU, 2)
        s1 = ct * rng.uniform(0.6, 1.0)
        s2 = s1 * rng.uniform(-1.0, 1.0)
        R1 = np.array([[math.cos(t1), -math.sin(t1)], [math.sin(t1), math.cos(t1)]])
        R2 = np.array([[math.cos(t2), -math.sin(t2)], [math.sin(t2), math.cos(t2)]])
        a = (R1 @ np.diag([s1, s2]) @ R2).ravel()
        maps.append((a, rng.uniform(-1, 1, 2)))
    pts, cols = chaos_game(maps, int(d["var"]), int(d["var2"]), d["vmix"], int(d["sym"]),
                           seed=int(d["seed"]) + seed)
    return splat(pts, cols, d["zoom"], d["gamma"], d["bright"], d)


register(Op(0xAA, "tex.flame", "T", (), (
    P("maps", "int", 2, 5, 80), P("var", "enum", 0, 8, 100), P("var2", "enum", 0, 8, 100), P("vmix", "lin", 0, 1, 0),
    P("contract", "lin", 0.3, 0.98, 215), P("sym", "int", 1, 8, 40), P("zoom", "exp", 0.25, 4, 128),
    P("gamma", "exp", 0.15, 1.5, 128), P("bright", "exp", 0.1, 8, 128), P("seed", "int", 0, 255, 0), DRIFT)
    + hsvp("a", 170, 200, 200) + hsvp("b", 20, 200, 230),
    "fractal flame: chaos game over 2-5 random contractive affine maps + a nonlinear variation, log-density tonemap",
    "Draves 1992 flam3 / Electric Sheep"), family="fractal", bake_t=bake_flame)


# ---------------------------------------------------------------------- 0xAB tex.attractor
def bake_attractor(d, ins, seed=0):
    a, b, c, e = d["pa"], d["pb"], d["pc"], d["pd"]
    kind = int(d["kind"])
    rng = np.random.default_rng(seed + 5)
    W = 4000
    x = rng.uniform(-0.5, 0.5, W)
    y = rng.uniform(-0.5, 0.5, W)
    pts, cols = [], []
    for it in range(130):
        if kind == 0:  # Clifford
            nx, ny = np.sin(a * y) + c * np.cos(a * x), np.sin(b * x) + e * np.cos(b * y)
        elif kind == 1:  # Peter de Jong
            nx, ny = np.sin(a * y) - np.cos(b * x), np.sin(c * x) - np.cos(e * y)
        else:  # Johnny Svensson
            nx, ny = e * np.sin(a * x) - np.sin(b * y), c * np.cos(a * x) + np.cos(b * y)
        sp = np.hypot(nx - x, ny - y)
        x, y = nx, ny
        if it >= 10:
            pts.append(np.stack([x, y], 1))
            cols.append(np.tanh(sp * 0.7))
    return splat(np.concatenate(pts), np.concatenate(cols), d["zoom"], d["gamma"], d["bright"], d)


register(Op(0xAB, "tex.attractor", "T", (), (
    P("kind", "enum", 0, 3, 0), P("pa", "lin", -3, 3, 68), P("pb", "lin", -3, 3, 195), P("pc", "lin", -3, 3, 170),
    P("pd", "lin", -3, 3, 157), P("zoom", "exp", 0.25, 4, 128), P("gamma", "exp", 0.15, 1.5, 128),
    P("bright", "exp", 0.1, 8, 128), DRIFT) + hsvp("a", 160, 200, 200) + hsvp("b", 30, 220, 230),
    "2D strange attractor density (Clifford / de Jong / Svensson): four bytes of dynamics -> filament nebulae",
    "Pickover / de Jong attractors"), family="fractal", bake_t=bake_attractor)


# ---------------------------------------------------------------------- 0xAC tex.physarum (Jones 2010 transport networks)
def physarum(M, n, steps, sa, sd, ra, speed, decay, seed, inp=None):
    from scipy import ndimage
    rng = np.random.default_rng(seed)
    pos = rng.random((n, 2)) * M
    ang = rng.random(n) * TAU
    trail = np.zeros((M, M), np.float32)
    food = None
    if inp is not None:
        food = resize(norm01(luma(inp)), M, order=1).astype(np.float32)
    for _ in range(steps):
        def sense(da):
            a = ang + da
            q = (pos + sd * np.stack([np.cos(a), np.sin(a)], 1)).astype(np.int64) % M
            return trail[q[:, 1], q[:, 0]]
        fl, fc, fr = sense(sa), sense(0.0), sense(-sa)
        turn = np.where((fc >= fl) & (fc >= fr), 0.0,
                        np.where((fc < fl) & (fc < fr), np.sign(rng.random(n) - 0.5) * ra,
                                 np.where(fl > fr, ra, -ra)))
        ang += turn
        pos = (pos + speed * np.stack([np.cos(ang), np.sin(ang)], 1)) % M
        q = pos.astype(np.int64)
        np.add.at(trail, (q[:, 1], q[:, 0]), 1.0)
        if food is not None:
            trail += food * 0.5
        trail = (0.7 * trail + 0.3 * ndimage.uniform_filter(trail, 3, mode="wrap")) * decay
    return trail


def bake_physarum(d, ins, seed=0):
    M = [96, 128, 192][int(d["scale"])]
    tr = physarum(M, int(d["agents"]), int(d["steps"]) * 40, d["sangle"], d["sdist"], d["rangle"], 1.0, d["decay"],
                  int(d["seed"]) + seed * 13, ins[0] if ins else None)
    y = np.log1p(tr) / (np.log1p(tr).max() + 1e-9)
    return ramp(resize(y.astype(np.float32), N, order=1), d, d["gamma"])


register(Op(0xAC, "tex.physarum", "T", ("t",), (
    P("agents", "exp", 1000, 40000, 160), P("steps", "int", 2, 16, 70), P("sangle", "lin", 0.1, 1.5, 150),
    P("sdist", "exp", 2, 30, 200), P("rangle", "lin", 0.1, 1.5, 40), P("decay", "lin", 0.8, 0.99, 180),
    P("scale", "enum", 0, 3, 200), P("seed", "int", 0, 255, 0), GAMMA, DRIFT) + hsvp("a", 165, 220, 5) + hsvp("b", 140, 160, 220),
    "Physarum transport network (Jones 2010 agents: sense, turn, deposit, diffuse); input = food map: webs, membranes",
    "Physarum polycephalum model"), family="fractal", bake_t=bake_physarum)


# ---------------------------------------------------------------------- 0xAD tex.distance
def bake_distance(d, ins, seed=0):
    from scipy import ndimage
    img = _input_or_noise(ins[0] if ins else None, seed)
    L = luma(img)
    m = L > np.percentile(L, d["thresh"] * 100)
    # periodic distance transform: tile 3x3, take the centre
    big = np.tile(m, (3, 3))
    din = ndimage.distance_transform_edt(big)[N:2 * N, N:2 * N]
    dout = ndimage.distance_transform_edt(~big)[N:2 * N, N:2 * N]
    sdf = (dout - din) / N  # signed, tile units
    mode = int(d["mode"])
    w = d["width"]
    if mode == 0:  # outline / neon edge
        y = np.exp(-(sdf / w) ** 2)
    elif mode == 1:  # glow falloff outside, flat inside
        y = np.where(sdf < 0, 1.0, np.exp(-np.maximum(sdf, 0) / w))
    elif mode == 2:  # contour bands (iso-distance lines)
        y = (0.5 + 0.5 * np.cos(TAU * sdf / w)) ** 4
    else:  # bevel height
        y = np.clip(-sdf / w, 0, 1)
    return ramp(y, d, d["gamma"])


register(Op(0xAD, "tex.distance", "T", ("T",), (
    P("thresh", "lin", 0, 1, 160), P("mode", "enum", 0, 4, 0), P("width", "exp", 0.002, 0.2, 100), GAMMA, DRIFT)
    + hsvp("a", 0, 0, 0) + hsvp("b", 150, 160, 240),
    "threshold the input, Euclidean (periodic) distance transform: neon outlines / glow / iso-contours / bevel",
    "distance transform (Danielsson)"), family="spectral", bake_t=bake_distance)


# ---------------------------------------------------------------------- 0xAE tex.colormatch (DATA PRIOR)
_REELC = None


def _reel_color():
    global _REELC
    if _REELC is None:
        import os
        z = np.load(os.path.join(os.path.dirname(__file__), "data", "reel_color_train_v2.npz"))
        _REELC = (z["mu"].astype(np.float64), z["cov"].astype(np.float64))
    return _REELC


def _sqrtm(C):
    w, V = np.linalg.eigh(C)
    return (V * np.sqrt(np.maximum(w, 1e-10))) @ V.T


def bake_colormatch(d, ins, seed=0):
    """Monge-Kantorovich linear map: the input's colour distribution (mean, covariance, linear light) is moved onto the
    Amoebius training reels' (eigendecompositions of both covariances). The target statistics live in the decoder
    (12 floats in ops_ext/data), NOT in the genome: count them when comparing bytes."""
    img = _input_or_noise(ins[0] if ins else None, seed)
    X = np.maximum(img.reshape(-1, 3).astype(np.float64), 0) ** 2.2
    mu_s, C_s = X.mean(0), np.cov(X.T) + np.eye(3) * 1e-6
    mu_t, C_t = _reel_color()
    C_t = C_t * d["contrast"] ** 2
    Cs_h = _sqrtm(C_s)
    Cs_ih = np.linalg.inv(Cs_h)
    T = Cs_ih @ _sqrtm(Cs_h @ C_t @ Cs_h) @ Cs_ih
    Y = (X - mu_s) @ T.T + mu_t * d["bright"]
    out = np.maximum(Y, 0) ** (1 / 2.2)
    return mixout(img, out.reshape(img.shape).astype(np.float32), d["amount"])


register(Op(0xAE, "tex.colormatch", "T", ("T",), (
    P("amount", "lin", 0, 1, 255), P("contrast", "exp", 0.25, 4, 128), P("bright", "exp", 0.25, 4, 128), DRIFT),
    "DATA PRIOR: optimal-transport (linear Monge-Kantorovich) map of the input's colours onto the Amoebius reels' "
    "colour mean/covariance; the 12 target floats live in the decoder, not the genome",
    "colour transfer (Pitie 2007)"), family="dataprior", bake_t=bake_colormatch)
