"""Agent (A) and camera (C) extension ops: flow fields, epicycle pilots, normal-mode bodies, Fourier camera rails.

Opcodes 0xD0-0xD4 (agents) and 0xE0-0xE1 (cameras). Every agent op is a *behaviour*: it appends (op, d) to each
population's behaviour list, exactly like ag.boids / ag.wander, and is applied by a wrapper around
World._step_movers that adds a steering acceleration before the core integrator (which keeps cruise speed, limits
turning and drops prisms). Steering is expressed as acc = gain * (desired_velocity - vel), so every op cooperates
with the core speed clamp and with each other (ops chain: spawn -> curl -> vortex -> trail is a sum of steers).

  ag.curl     divergence-free curl-noise flow: v = sum_k cos(w_k.x + phi_k + e t) (w_k x A_k), the exact curl of the
              vector potential sum_k A_k sin(w_k.x + phi_k + e t). Analytic, 8 terms, no grid.
  ag.fourier  each mover chases its own point on a closed Fourier curve (epicycles, K harmonics); trails lay tunnels.
  ag.modes    graph-Laplacian normal modes (eigh of the k-NN spring network over the spawn positions): the population
              breathes as a sum of modes, omega_m ~ sqrt(lambda_m). Static scatters are displaced directly; movers
              steer toward rest+displacement around their centroid.
  ag.texflow  steer along a texture: luma = potential on the XZ plane (a terrain the swarm rides) or on a sphere;
              gradient (gather in valleys) mixed with its perpendicular (streamlines, contour-following).
  ag.vortex   Biot-Savart swirl around a few line vortices with a Rankine-like core, inflow and updraft (tornadoes).
  cam.fourier camera on a closed Fourier curve (same coefficient scheme as ag.fourier: a shared seed byte + K and
              radius put the camera on the pilots' loop), looking ahead along the path, banking into turns.
  cam.track   overview shot: frames the swarm centroid at a distance proportional to its spread, slow orbit.

install_hooks(sim, render) monkeypatches: sim/render.build_populations, World.__init__/step/_step_movers,
Camera.update and render.look_at (camera roll, for banking).
"""
import math
import sys

import numpy as np

from agenome.ops import Op, P

from .registry import register, EXT

TAU = 2 * math.pi
LUMA = np.array([0.299, 0.587, 0.114], np.float32)


def Pv(name, kind, lo, hi, value):
    """P whose default byte decodes nearest to `value`."""
    return P(name, kind, lo, hi, P(name, kind, lo, hi).encode(value))


def _unit(v, eps=1e-6):
    return v / (np.linalg.norm(v, axis=-1, keepdims=True) + eps)


class AgentHook:
    """Marker object stored in EXT[op].agent: init(world, p, d) -> state; acc(world, p, st, d, dt) -> (n,3) steer;
    static(world, p, st, d, dt) -> None (moves a static population in place). Any may be None."""

    def __init__(self, init=None, acc=None, static=None, build=None, post=None, draw=None):
        self.init, self.acc, self.static, self.build = init, acc, static, build
        self.post, self.draw = post, draw  # post(world, p, st, d, dt) after integration; draw(...) -> {mesh: arr}


class CamHook:
    def __init__(self, init, update):
        self.init, self.update = init, update


# ====================================================================== shared Fourier-curve scheme
def fourier_coeffs(seed, K):
    """(K, 2, 3) cosine / sine coefficient vectors of a closed curve c(s) = sum_k a_k cos(ks) + b_k sin(ks).
    Shared by ag.fourier and cam.fourier: harmonics 1..4 are always drawn in the same order, so K only truncates,
    and the curve is normalised to max radius 1 (for every K). y is kept for agents; the camera scales it by hamp."""
    rng = np.random.default_rng(int(seed) * 7919 + 0xF0F)
    c = rng.normal(size=(4, 2, 3))
    c[0] *= 1.6  # strong fundamental: a loop, not a scribble
    for k in range(4):
        c[k] /= (k + 1) ** 1.25
    c = c[:max(1, min(4, int(K)))]
    s = np.linspace(0, TAU, 256, endpoint=False)
    pts = curve(c, s)
    c = c / (np.linalg.norm(pts, axis=1).max() + 1e-9)
    return c


def curve(c, s, deriv=0):
    """Evaluate the curve (or its derivative order 0/1/2) at phases s (any shape) -> s.shape + (3,)."""
    s = np.asarray(s, np.float64)
    out = np.zeros(s.shape + (3,))
    for k in range(len(c)):
        w = k + 1
        ck, sk = np.cos(w * s)[..., None], np.sin(w * s)[..., None]
        if deriv == 0:
            out += c[k, 0] * ck + c[k, 1] * sk
        elif deriv == 1:
            out += w * (-c[k, 0] * sk + c[k, 1] * ck)
        else:
            out += -w * w * (c[k, 0] * ck + c[k, 1] * sk)
    return out


def _mean_speed(c):
    s = np.linspace(0, TAU, 256, endpoint=False)
    return float(np.linalg.norm(curve(c, s, 1), axis=1).mean()) + 1e-9


# ====================================================================== 0xD0 ag.curl
NCURL = 8


def _curl_init(world, p, d):
    rng = np.random.default_rng(int(d["seed"]) * 613 + 17)
    w = _unit(rng.normal(size=(NCURL, 3))) * (d["freq"] * TAU) * rng.uniform(0.6, 1.6, (NCURL, 1))
    A = rng.normal(size=(NCURL, 3))
    phi = rng.random(NCURL) * TAU
    ev = rng.uniform(0.5, 1.5, NCURL) * np.sign(rng.random(NCURL) - 0.5)
    wxA = np.cross(w, A)
    rms = math.sqrt(0.5 * (np.linalg.norm(wxA, axis=1) ** 2).sum()) + 1e-9
    return {"w": w.astype(np.float32), "wxA": (wxA / rms).astype(np.float32), "phi": phi, "ev": ev}


def _curl_field(st, d, pos, t):
    arg = pos @ st["w"].T + st["phi"] + st["ev"] * d["evolve"] * t  # n, K
    return (np.cos(arg) @ st["wxA"]).astype(np.float32)  # unit rms


def _curl_acc(world, p, st, d, dt):
    v = _curl_field(st, d, p.pos, world.t)
    return d["strength"] * (p.speed * 1.2 * v - p.vel)


def _flow_static(field):
    """Static scatters drift along a flow field with a weak spring back to their spawn position (motes in a current)."""
    def f(world, p, st, d, dt):
        if "rest" not in st:
            st["rest"] = p.pos.copy()
            st["vs"] = 0.05 * float(p.cfg["radius"]) + 0.5
        v = field(st, d, p.pos, world.t)
        p.pos = (p.pos + (v * st["vs"] + 0.15 * (st["rest"] - p.pos)) * dt).astype(np.float32)
    return f


register(Op(0xD0, "ag.curl", "A", ("A",), (
    Pv("freq", "exp", 0.002, 0.5, 0.02), Pv("strength", "exp", 0.05, 20, 1.6), Pv("evolve", "exp", 0.001, 3, 0.15),
    P("seed", "int", 0, 255, 0)),
    "steer along divergence-free curl noise (analytic curl of a sum-of-sinusoids vector potential): smoke flocks",
    "Bridson 2007 curl noise"), family="agents",
    agent=AgentHook(_curl_init, _curl_acc, _flow_static(_curl_field)))


# ====================================================================== 0xD1 ag.fourier
def _fourier_init(world, p, d):
    c = fourier_coeffs(d["seed"], d["K"])
    n = len(p.pos)
    rng = np.random.default_rng(int(d["seed"]) * 31 + 5)
    ph = (np.arange(n) / max(n, 1)) * TAU * d["spread"] + rng.normal(size=n) * 0.02
    off = rng.normal(size=(n, 3)) * d["tube"]
    R = d["radius"]
    om = d["speed"] * p.speed / (_mean_speed(c) * R)  # path speed = speed * cruise speed
    return {"c": c, "ph": ph, "off": off.astype(np.float32), "om": om, "R": R}


def _fourier_acc(world, p, st, d, dt):
    s = st["om"] * world.t + st["ph"]
    R = st["R"]
    tgt = curve(st["c"], s) * R + st["off"]
    vff = curve(st["c"], s, 1) * R * st["om"]
    desired = vff + (tgt - p.pos) * 0.6 * d["gain"]
    return (d["gain"] * (desired - p.vel)).astype(np.float32)


register(Op(0xD1, "ag.fourier", "A", ("A",), (
    Pv("K", "int", 1, 4, 3), Pv("radius", "exp", 0.5, 300, 40), Pv("speed", "exp", 0.05, 4, 1.0),
    Pv("spread", "lin", 0, 1, 0.12), Pv("gain", "exp", 0.05, 20, 2.5), Pv("tube", "exp", 0.01, 50, 2.5),
    P("seed", "int", 0, 255, 0)),
    "each mover chases its own point on a closed Fourier curve (K epicycles, seed coefficients shared with "
    "cam.fourier); spread = phase spread along the loop, tube = per-mover offset (tunnel radius)",
    "epicycles / Fourier series"), family="agents", agent=AgentHook(_fourier_init, _fourier_acc))


# ====================================================================== 0xD2 ag.modes
MAX_EIG = 512


def _knn_laplacian(X, k):
    n = len(X)
    k = max(1, min(k, n - 1))
    try:
        from scipy.spatial import cKDTree
        _, nb = cKDTree(X).query(X, k + 1)
        nb = nb[:, 1:]
    except Exception:
        D = ((X[:, None, :] - X[None, :, :]) ** 2).sum(-1)
        nb = np.argsort(D, 1)[:, 1:k + 1]
    W = np.zeros((n, n))
    W[np.repeat(np.arange(n), k), nb.ravel()] = 1.0
    W = np.maximum(W, W.T)
    return np.diag(W.sum(1)) - W


def _modes_init(world, p, d):
    X = p.pos.astype(np.float64)
    n = len(X)
    ctr = X.mean(0)
    rel = X - ctr
    rms = float(np.sqrt((rel ** 2).sum(1).mean())) + 1e-6
    first, cnt = int(d["first"]), int(d["count"])
    if n < 3:
        return {"none": True}
    if n > MAX_EIG:  # landmarks: modes on a subsample, each point follows its nearest landmark
        rng = np.random.default_rng(1234)
        lm = rng.choice(n, MAX_EIG, replace=False)
        try:
            from scipy.spatial import cKDTree
            _, near = cKDTree(X[lm]).query(X, 1)
        except Exception:
            near = np.argmin(((X[:, None] - X[lm][None]) ** 2).sum(-1), 1)
        Xl = X[lm]
    else:
        near, Xl = None, X
    L = _knn_laplacian(Xl, int(d["k"]))
    try:
        from scipy.linalg import eigh
        hi = min(len(Xl) - 1, first + cnt - 1)
        lam, V = eigh(L, subset_by_index=[0, hi])
    except Exception:
        lam, V = np.linalg.eigh(L)
    idx = [m for m in range(first, first + cnt) if m < len(lam)]
    if not idx:
        return {"none": True}
    lam_s, V_s = lam[idx], V[:, idx] * math.sqrt(len(Xl))  # unit-rms per mode
    if near is not None:
        V_s = V_s[near]
    lam0 = max(lam_s[0], 1e-3)
    omega = TAU * d["freq"] * np.sqrt(np.maximum(lam_s, 1e-6) / lam0)
    rng = np.random.default_rng(int(d["k"]) * 101 + first)
    phase = rng.random(len(idx)) * TAU
    radial = _unit(rel)
    swirl = _unit(np.cross(np.array([0.0, 1.0, 0.0]), rel))
    axes = _unit(rng.normal(size=(len(idx), 3)))
    dm = int(d["dir"])
    dirs = []
    for m in range(len(idx)):
        kind = dm if dm < 3 else m % 3
        dirs.append(radial if kind == 0 else (np.tile(axes[m], (n, 1)) if kind == 1 else swirl))
    weight = 1.0 / np.sqrt(np.arange(1, len(idx) + 1))  # low modes dominate
    return {"rest": rel.astype(np.float32), "ctr": ctr.astype(np.float32), "V": V_s.astype(np.float32),
            "omega": omega, "phase": phase, "dirs": np.stack(dirs).astype(np.float32),
            "amp": d["amp"] * rms * weight / weight.sum() * 1.6, "lam": lam_s}


def _modes_disp(st, t):
    s = np.sin(st["omega"] * t + st["phase"]) * st["amp"]  # m
    return np.einsum("m,nm,mnc->nc", s, st["V"], st["dirs"]).astype(np.float32)


def _modes_acc(world, p, st, d, dt):
    if st.get("none"):
        return None
    ctr = p.pos.mean(0)
    tgt = ctr + st["rest"] + _modes_disp(st, world.t)
    desired = (tgt - p.pos) * 1.2 * d["gain"]
    return (d["gain"] * (desired - p.vel)).astype(np.float32)


def _modes_static(world, p, st, d, dt):
    if st.get("none"):
        return
    p.pos = (st["ctr"] + st["rest"] + _modes_disp(st, world.t + dt)).astype(np.float32)


register(Op(0xD2, "ag.modes", "A", ("A",), (
    Pv("first", "int", 1, 32, 1), Pv("count", "int", 1, 8, 4), Pv("amp", "lin", 0, 1, 0.35),
    Pv("freq", "exp", 0.02, 4, 0.35), P("dir", "enum", 0, 4, 0), Pv("k", "int", 3, 12, 6),
    Pv("gain", "exp", 0.05, 20, 2.0)),
    "normal modes of the k-NN spring graph over the spawn positions (eigh of its Laplacian): the body breathes as a "
    "sum of modes first..first+count-1, omega ~ sqrt(lambda); dir radial/axis/swirl/mixed; statics displaced, "
    "movers steer to centroid+rest+displacement",
    "graph Laplacian eigenmodes / modal analysis"), family="agents",
    agent=AgentHook(_modes_init, _modes_acc, _modes_static))


# ====================================================================== 0xD3 ag.texflow
_TEXCACHE = {}
_CUR_RENDERER = [None]
STATS = {"texflow_gpu": 0, "texflow_bake": 0, "texflow_proxy": 0}


def _value_noise(seed, n=256):
    """Cheap tileable value-noise proxy (3 octaves) used when the T input cannot be evaluated on the CPU."""
    rng = np.random.default_rng(seed)
    out = np.zeros((n, n))
    amp = 1.0
    for cells in (4, 8, 16):
        g = rng.random((cells, cells))
        x = np.arange(n) * cells / n
        i0 = np.floor(x).astype(int)
        f = x - i0
        f = f * f * (3 - 2 * f)
        i1 = (i0 + 1) % cells
        rows = g[i0][:, i0] * (1 - f)[None, :] + g[i0][:, i1] * f[None, :]
        rows2 = g[i1][:, i0] * (1 - f)[None, :] + g[i1][:, i1] * f[None, :]
        out += amp * (rows * (1 - f)[:, None] + rows2 * f[:, None])
        amp *= 0.5
    return out / 1.75


def texture_luma(g, j, seed=0):
    """Luma [256,256] (row = v) of T node j. Order: GPU (the current Renderer's ext Baker: any T subgraph) ->
    direct bake_t for baked ext ops with no inputs -> value-noise proxy."""
    if j is None:
        STATS["texflow_proxy"] += 1
        return _value_noise(seed)
    try:
        from . import bake
        key = bake.subgraph_key(g, j)
    except Exception:
        key = None
    if key is not None and key in _TEXCACHE:
        return _TEXCACHE[key]
    arr = None
    R = _CUR_RENDERER[0]
    if R is not None:
        try:
            from . import bake
            if not hasattr(R, "_ext_baker"):
                R._ext_baker = bake.Baker(R)
            arr = R._ext_baker.eval_input(g, j)
            STATS["texflow_gpu"] += 1
        except Exception:
            arr = None
    if arr is None:
        n = g.nodes[j]
        e = EXT.get(n.op)
        if e is not None and e.bake_t is not None:
            try:
                from agenome.ops import OPS
                o = OPS[n.op]
                dd = {pp.name: pp.decode(b) for pp, b in zip(o.params, n.params)}
                arr = np.asarray(e.bake_t(dd, [None] * len(n.inputs), seed=g.seed), np.float32)
                STATS["texflow_bake"] += 1
            except Exception:
                arr = None
    if arr is None:
        STATS["texflow_proxy"] += 1
        lum = _value_noise(seed * 131 + j)
    else:
        arr = np.nan_to_num(arr)
        lum = arr @ LUMA if arr.ndim == 3 else arr
        lo, hi = np.percentile(lum, [1, 99])
        lum = np.clip((lum - lo) / (hi - lo + 1e-9), 0, 1)
    lum = lum.astype(np.float32)
    if key is not None:
        if len(_TEXCACHE) > 32:
            _TEXCACHE.pop(next(iter(_TEXCACHE)))
        _TEXCACHE[key] = lum
    return lum


def _bilinear(a, u, v):
    n = a.shape[0]
    x, y = (u % 1.0) * n, (v % 1.0) * n
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = x - x0, y - y0
    x0 %= n
    y0 %= n
    x1, y1 = (x0 + 1) % n, (y0 + 1) % n
    return (a[y0, x0] * (1 - fx) * (1 - fy) + a[y0, x1] * fx * (1 - fy) + a[y1, x0] * (1 - fx) * fy
            + a[y1, x1] * fx * fy)


def _texflow_build(g, idx, d):
    ins = g.nodes[idx].inputs
    j = ins[1] if len(ins) > 1 else None
    lum = texture_luma(g, j, seed=int(d["seed"]))
    from scipy import ndimage
    lum = ndimage.gaussian_filter(lum, 2.0, mode="wrap")  # smooth potential: streamlines, not pixel noise
    gv, gu = np.gradient(lum)  # d/drow, d/dcol (per pixel)
    n = lum.shape[0]
    d = dict(d)
    d["_H"], d["_gu"], d["_gv"] = lum, (gu * n).astype(np.float32), (gv * n).astype(np.float32)
    d["_gref"] = float(np.percentile(np.hypot(gu, gv) * n, 60)) + 1e-6
    return d


def _texflow_init(world, p, d):
    return {}


def _texflow_dir(st, d, pos, t=0.0):
    perp = d["perp"]
    if int(d["map"]) == 0:  # plane XZ, tiles every `scale` units; y rides the luma terrain
        S = d["scale"]
        u, v = pos[:, 0] / S + 0.5, pos[:, 2] / S + 0.5
        H = _bilinear(d["_H"], u, v)
        gu, gv = _bilinear(d["_gu"], u, v), _bilinear(d["_gv"], u, v)
        g = np.stack([gu, np.zeros_like(gu), gv], 1)
        pp = np.stack([-gv, np.zeros_like(gu), gu], 1)
        flow = (-(1 - perp) * g + perp * pp) / d["_gref"]
        m = np.linalg.norm(flow, axis=1, keepdims=True)
        flow = flow / np.maximum(m, 1.0)
        flow[:, 1] = np.clip((d["height"] * (H - 0.5) - pos[:, 1]) / (0.15 * d["height"] + 1.0), -1.5, 1.5)
        return flow.astype(np.float32)
    # sphere: u = longitude, v = latitude; ride the shell radius*(1 + relief)
    r = np.linalg.norm(pos, axis=1) + 1e-6
    lon = np.arctan2(pos[:, 2], pos[:, 0])
    lat = np.arccos(np.clip(pos[:, 1] / r, -1, 1))
    u, v = lon / TAU + 0.5, lat / math.pi
    H = _bilinear(d["_H"], u, v)
    gu, gv = _bilinear(d["_gu"], u, v), _bilinear(d["_gv"], u, v)
    e_lon = _unit(np.stack([-np.sin(lon), np.zeros_like(lon), np.cos(lon)], 1))
    e_lat = _unit(np.stack([np.cos(lat) * np.cos(lon), -np.sin(lat), np.cos(lat) * np.sin(lon)], 1))
    sl = np.maximum(np.sin(lat), 0.15)[:, None]
    g = e_lon * (gu[:, None] / (TAU * sl)) + e_lat * (gv[:, None] / math.pi)
    nrm = pos / r[:, None]
    pp = np.cross(nrm, g)
    flow = (-(1 - perp) * g + perp * pp) / (d["_gref"] / 4.0)
    m = np.linalg.norm(flow, axis=1, keepdims=True)
    flow = flow / np.maximum(m, 1.0)
    R0 = d["radius"] + d["height"] * (H - 0.5)
    flow += nrm * np.clip((R0 - r) / (0.15 * d["height"] + 1.0), -1.5, 1.5)[:, None]
    return flow.astype(np.float32)


def _texflow_acc(world, p, st, d, dt):
    f = _texflow_dir(st, d, p.pos)
    # keep heading where the field is flat: blend the field direction with the current heading
    v = f + _unit(p.vel) * np.maximum(0.0, 1.0 - np.linalg.norm(f, axis=1, keepdims=True)) * 0.5
    return (d["strength"] * (p.speed * v - p.vel)).astype(np.float32)


register(Op(0xD3, "ag.texflow", "A", ("A", "t"), (
    P("map", "enum", 0, 2, 0), Pv("scale", "exp", 1, 1000, 160), Pv("perp", "lin", 0, 1, 0.8),
    Pv("strength", "exp", 0.05, 20, 2.0), Pv("height", "exp", 0.1, 300, 25), Pv("radius", "exp", 1, 500, 60),
    P("seed", "int", 0, 255, 0)),
    "steer along a texture field: luma = potential on the XZ plane (tiles every `scale`, the swarm rides it as a "
    "terrain of relief `height`) or on a sphere of `radius`; perp mixes gradient descent (0) with streamlines (1)",
    "flow-field steering (Reynolds)"), family="agents",
    agent=AgentHook(_texflow_init, _texflow_acc, None, build=_texflow_build))


# ====================================================================== 0xD4 ag.vortex
def _vortex_init(world, p, d):
    rng = np.random.default_rng(int(d["seed"]) * 977 + 3)
    M = int(d["count"])
    ang = rng.random(M) * TAU
    rad = d["spread"] * np.sqrt(rng.uniform(0.05, 1.0, M))
    ctr = np.stack([np.cos(ang) * rad, rng.normal(size=M) * d["spread"] * 0.2, np.sin(ang) * rad], 1)
    tilt = d["tilt"]
    ax = _unit(np.array([0.0, 1.0, 0.0]) + rng.normal(size=(M, 3)) * tilt)
    sgn = np.where(rng.random(M) < 0.75, 1.0, -1.0)
    sgn[0] = 1.0
    return {"c0": ctr, "ax": ax, "sgn": sgn, "dph": rng.random(M) * TAU}


def _vortex_field(st, d, pos, t):
    rc = d["core"]
    c = st["c0"]
    if abs(d["drift"]) > 1e-5:  # vortices precess about the y axis
        a = d["drift"] * t
        ca, sa = math.cos(a), math.sin(a)
        c = np.stack([c[:, 0] * ca - c[:, 2] * sa, c[:, 1], c[:, 0] * sa + c[:, 2] * ca], 1)
    v = np.zeros_like(pos, dtype=np.float64)
    for m in range(len(c)):
        a = st["ax"][m]
        rel = pos - c[m]
        rp = rel - (rel @ a)[:, None] * a
        r2 = (rp ** 2).sum(1, keepdims=True)
        prof = 2.0 * rc / (r2 + rc * rc) * np.sqrt(r2 + 1e-9) / (np.sqrt(r2) + 1e-6)  # =1 at r=rc, ~rc/r outside
        tang = np.cross(a, rp) / (np.sqrt(r2) + 1e-6)
        inward = -rp / (np.sqrt(r2) + 1e-6)
        near = np.exp(-r2 / (4 * rc * rc))
        v += st["sgn"][m] * tang * prof + d["inflow"] * inward * np.minimum(prof, 1.0) \
            + d["updraft"] * a[None, :] * near
    return v.astype(np.float32)


def _vortex_acc(world, p, st, d, dt):
    v = _vortex_field(st, d, p.pos, world.t)
    m = np.linalg.norm(v, axis=1, keepdims=True)
    v = v / np.maximum(m, 0.25)  # full steer within ~8 core radii, fading beyond
    return (d["strength"] * (p.speed * v - p.vel * np.minimum(1.0, m / 0.25)))


register(Op(0xD4, "ag.vortex", "A", ("A",), (
    Pv("count", "int", 1, 6, 3), Pv("strength", "exp", 0.05, 20, 2.0), Pv("core", "exp", 0.2, 100, 8),
    Pv("spread", "exp", 0.5, 300, 30), Pv("tilt", "lin", 0, 1, 0.25), Pv("inflow", "lin", 0, 1, 0.35),
    Pv("updraft", "scub", 0, 1, 0.25), Pv("drift", "scub", 0, 1, 0.08), P("seed", "int", 0, 255, 0)),
    "Biot-Savart swirl around `count` seed-placed line vortices (core radius, tilt, inflow toward the axis, updraft "
    "along it, slow precession): tornado / whirlpool flocks",
    "point vortices / Rankine vortex"), family="agents",
    agent=AgentHook(_vortex_init, _vortex_acc, _flow_static(_vortex_field)))


# ====================================================================== 0xD7 ag.ribbon
RIBBON_BUDGET = 30000  # instances per frame for all ribbons (render.py draws at most 32768 per mesh)
RIBBON_MESH = ["box", "wedge", "octa"]  # wedge = true triangular prism (registered by ops_ext.install)


def _ribbon_init(world, p, d):
    if p.kind != "movers":
        return {"none": True}
    n = len(p.pos)
    nrib = max(1, sum(1 for q in world.pops for op, _ in q.behaviours if op == "ag.ribbon"))
    H = int(math.ceil(d["life"] / max(d["interval"], 1e-3))) + 1
    H = max(2, min(H, RIBBON_BUDGET // max(1, n * nrib) + 1, 2048))
    rng = np.random.default_rng(int(n) * 7 + 11)
    return {"H": H, "pos": np.zeros((H, n, 3), np.float32), "ang": np.zeros((H, n), np.float32),
            "t": np.full(H, -1e9, np.float32), "head": 0, "acc": 1.0, "ph": rng.random(n).astype(np.float32) * TAU,
            "count": 0}


def _ribbon_post(world, p, st, d, dt):
    if st.get("none"):
        return
    st["acc"] += dt / max(d["interval"], 1e-3)
    if st["acc"] >= 1.0:
        st["acc"] -= math.floor(st["acc"])
        h = st["head"]
        st["pos"][h] = p.pos
        st["ang"][h] = d["twist"] * world.t + st["ph"]
        st["t"][h] = world.t
        st["head"] = (h + 1) % st["H"]
        st["count"] = min(st["count"] + 1, st["H"])


def _ribbon_draw(world, p, st, d):
    if st.get("none") or st["count"] < 1:
        return None
    H, h, cnt = st["H"], st["head"], st["count"]
    order = (h - 1 - np.arange(cnt)) % H  # newest first
    P_ = np.concatenate([p.pos[None], st["pos"][order]], 0)  # live head + history: (cnt+1, n, 3)
    A_ = np.concatenate([(d["twist"] * world.t + st["ph"])[None], st["ang"][order]], 0)
    T_ = np.concatenate([[world.t], st["t"][order]])
    age = world.t - T_[1:]  # age of each segment's older end
    keep = age < d["life"]
    if not keep.any():
        return None
    a, b = P_[1:][keep], P_[:-1][keep]  # (s, n, 3)
    ang = 0.5 * (A_[1:][keep] + A_[:-1][keep])
    fade = np.clip(1.0 - age[keep] / d["life"], 0, 1)
    s, n = a.shape[:2]
    a, b, ang = a.reshape(-1, 3), b.reshape(-1, 3), ang.reshape(-1)
    seg = b - a
    L = np.linalg.norm(seg, axis=1, keepdims=True)
    ok = (L[:, 0] > 1e-4) & (L[:, 0] < 50.0 * max(d["interval"], 1e-3) * max(p.speed, 1e-3) + 1.0)
    z = seg / (L + 1e-9)
    up = np.tile(np.array([0.0, 1.0, 0.0], np.float32), (len(z), 1))
    near = np.abs(z[:, 1]) > 0.95
    up[near] = (1.0, 0.0, 0.0)
    s0 = _unit(np.cross(up, z))
    u0 = np.cross(z, s0)
    ca, sa = np.cos(ang)[:, None], np.sin(ang)[:, None]
    side = s0 * ca + u0 * sa
    upv = -s0 * sa + u0 * ca
    from agenome.sim import _rgb
    cm = int(d["colormode"])
    base = _rgb(d["h"], d["s"], d["v"])
    if cm == 0:
        col = p.team_rgb[p.team] * 0.6 + base * 0.4
        col = np.tile(col, (s, 1))
    else:
        col = np.tile(base, (s * n, 1))
    emit = (d["emit"] * np.sqrt(np.repeat(fade, n)))[:, None]
    arr = np.concatenate([(a + b) * 0.5, side * d["width"], upv * d["height"], seg * 1.04, col, emit,
                          np.full((s * n, 1), d["edge"], np.float32)], 1)[ok]
    return {RIBBON_MESH[int(d["facets"])]: arr.astype(np.float32)}


register(Op(0xD7, "ag.ribbon", "A", ("A",), (
    Pv("width", "exp", 0.01, 20, 0.25), Pv("height", "exp", 0.01, 20, 2.0), Pv("interval", "exp", 0.02, 2, 0.1),
    Pv("life", "exp", 0.2, 120, 12), Pv("twist", "scub", 0, 6, 0.0), Pv("emit", "exp", 0.01, 20, 1.0),
    Pv("edge", "lin", 0, 4, 2.0), P("facets", "enum", 0, 3, 0), P("colormode", "enum", 0, 2, 0),
    P("h", "hue", default=150), Pv("s", "lin", 0, 1, 0.8), Pv("v", "exp", 0.01, 4.0, 1.0)),
    "continuous extruded wall along each mover's path: one stretched instance per history segment (touching), "
    "width x height cross-section twisting at `twist` rad/s; facets box (flat wall) / wedge (triangular prism wall, the reel look) / octa (beaded)",
    "Cosmic Shore prism walls, continuous"), family="agents",
    agent=AgentHook(_ribbon_init, None, None, post=_ribbon_post, draw=_ribbon_draw))


# ====================================================================== cameras
_ROLL = [0.0]


def _camf_init(cam, d):
    c = fourier_coeffs(d["seed"], d["K"]).copy()
    c[..., 1] *= d["hamp"]
    s = np.linspace(0, TAU, 256, endpoint=False)
    acc = np.linalg.norm(curve(c, s, 2), axis=1)
    return {"c": c, "amax": float(acc.max()) + 1e-6}


def _camf_update(cam, st, d, t, dt):
    c, R, om = st["c"], d["radius"], d["speed"]
    s = om * t + d["phase"]
    eye = curve(c, s) * R
    ahead = curve(c, s + om * d["lookahead"]) * R
    tgt = ahead * (1 - d["aim"])  # aim -> 1 looks at the centre
    # bank into the turn: lateral component of the path acceleration, scaled to +-50 degrees
    vel = curve(c, s, 1)
    acc = curve(c, s, 2)
    f = _unit(vel[None])[0]
    right = np.cross(f, np.array([0.0, 1.0, 0.0]))
    right /= np.linalg.norm(right) + 1e-9
    lat = float(acc @ right) / st["amax"]
    roll = -d["bank"] * math.radians(50) * max(-1.0, min(1.0, lat))
    return eye.astype(np.float32), tgt.astype(np.float32), roll


def _track_init(cam, d):
    return {"eye": None, "tgt": None}


def _track_update(cam, st, d, t, dt):
    w = cam.world
    pts = []
    if w is not None:
        pts = [p.pos for p in w.movers] or [p.pos for p in w.static]
    if pts:
        P_ = np.concatenate(pts, 0)
        ctr = P_.mean(0)
        spread = float(np.percentile(np.linalg.norm(P_ - ctr, axis=1), 85)) + 1.0
    else:
        ctr, spread = np.zeros(3), 20.0
    dist = d["dist"] * spread / math.tan(math.radians(d["fov"]) / 2)
    a = d["speed"] * t + d["phase"]
    el = d["height"]
    want_eye = ctr + dist * np.array([math.cos(a) * math.sqrt(max(0.0, 1 - el * el)), el,
                                      math.sin(a) * math.sqrt(max(0.0, 1 - el * el))])
    if st["eye"] is None:
        st["eye"], st["tgt"] = want_eye, ctr.copy()
    else:
        k = 1.0 - d["lag"] ** (dt * 15.0)
        st["eye"] = st["eye"] + (want_eye - st["eye"]) * k
        st["tgt"] = st["tgt"] + (ctr - st["tgt"]) * min(1.0, k * 1.5)
    return st["eye"].astype(np.float32), st["tgt"].astype(np.float32), 0.0


register(Op(0xE0, "cam.fourier", "C", (), (
    Pv("K", "int", 1, 4, 3), Pv("radius", "exp", 0.5, 1000, 60), Pv("hamp", "lin", 0, 1, 0.3),
    Pv("speed", "exp", 0.005, 2, 0.12), Pv("lookahead", "exp", 0.05, 20, 2.0), Pv("aim", "lin", 0, 1, 0.35),
    Pv("bank", "lin", 0, 1, 0.5), Pv("fov", "lin", 20, 120, 70), P("seed", "int", 0, 255, 0),
    Pv("phase", "lin", 0, TAU, 0)),
    "camera on a closed Fourier curve (seed/K/radius scheme shared with ag.fourier; hamp=1 rides the pilots' loop), "
    "looking `lookahead` seconds ahead (aim blends toward the centre), banking into turns",
    "demo camera spline"), family="cameras", agent=CamHook(_camf_init, _camf_update))

register(Op(0xE1, "cam.track", "C", ("A",), (
    Pv("dist", "exp", 0.3, 8, 1.6), Pv("height", "scub", 0, 1, 0.35), Pv("speed", "scub", 0, 2, 0.12),
    Pv("fov", "lin", 20, 120, 60), Pv("lag", "lin", 0, 0.98, 0.85), Pv("phase", "lin", 0, TAU, 0)),
    "overview: frames the centroid of all movers (statics if none) at dist * spread / tan(fov/2), slow orbit, "
    "lagged", "game camera"), family="cameras", agent=CamHook(_track_init, _track_update))


AGENT_OPS = [n for n, e in EXT.items() if isinstance(e.agent, AgentHook)]
CAM_OPS = [n for n, e in EXT.items() if isinstance(e.agent, CamHook)]


# ====================================================================== hooks
def install_hooks(sim, render):
    """Monkeypatch the core sim / renderer so the ops above run. Idempotent."""
    # recompute here: ops registered after the module-level lists (ag.flow, 0xD5) were missing from them, so ag.flow
    # built and rendered but its behaviour never ran (found 2026-11-10: tuning ag.flow never moved the judge)
    global AGENT_OPS, CAM_OPS
    AGENT_OPS = [n for n, e in EXT.items() if isinstance(e.agent, AgentHook)]
    CAM_OPS = [n for n, e in EXT.items() if isinstance(e.agent, CamHook)]
    if getattr(sim, "_agents_ext_hooked", False):
        return
    sim._agents_ext_hooked = True
    _d = sim._d

    # ---- populations: append our behaviours (recursion goes through sim.build_populations = this wrapper)
    orig_build = sim.build_populations

    def build_populations(g, idx, memo=None, gseed=0):
        if memo is None:
            memo = {}
        if idx is None or idx in memo or g.nodes[idx].op not in AGENT_OPS:
            return orig_build(g, idx, memo, gseed)
        n = g.nodes[idx]
        d = _d(n)
        hook = EXT[n.op].agent
        if hook.build is not None:
            d = hook.build(g, idx, d)
        out = sim.build_populations(g, n.inputs[0] if n.inputs else None, memo, gseed)
        for p in out:
            p.behaviours.append((n.op, d))
        memo[idx] = out
        return [p.copy_shallow() for p in out]

    sim.build_populations = build_populations
    render.build_populations = build_populations

    W = sim.World
    orig_init, orig_step, orig_sm = W.__init__, W.step, W._step_movers

    def w_init(self, pops, *a, **kw):
        orig_init(self, pops, *a, **kw)
        for p in pops:
            p._ext = []
            for k, (op, d) in enumerate(p.behaviours):
                if op in AGENT_OPS:
                    h = EXT[op].agent
                    st = h.init(self, p, d) if h.init else {}
                    p._ext.append((k, op, d, st))

    def w_step(self, dt):
        for p in self.static:
            for _, op, d, st in getattr(p, "_ext", ()):
                h = EXT[op].agent
                if h.static is not None:
                    h.static(self, p, st, d, dt)
        orig_step(self, dt)

    def w_step_movers(self, p, dt):
        ext = getattr(p, "_ext", ())
        if ext:
            acc = np.zeros_like(p.vel)
            for _, op, d, st in ext:
                h = EXT[op].agent
                if h.acc is not None:
                    a = h.acc(self, p, st, d, dt)
                    if a is not None:
                        acc += np.nan_to_num(a).astype(np.float32)
            # cap the steer so a hot gain cannot blow the integrator up at 60 Hz
            cap = 30.0 * max(p.speed, 1e-3)
            m = np.linalg.norm(acc, axis=1, keepdims=True)
            acc *= np.minimum(1.0, cap / (m + 1e-9))
            p.vel = (p.vel + acc * dt).astype(np.float32)
        orig_sm(self, p, dt)
        for _, op, d, st in ext:
            h = EXT[op].agent
            if h.post is not None:
                h.post(self, p, st, d, dt)

    orig_inst = W.instances

    def w_instances(self):
        out = orig_inst(self)
        extra = {}
        for p in self.movers:
            for _, op, d, st in getattr(p, "_ext", ()):
                h = EXT[op].agent
                if h.draw is not None:
                    r = h.draw(self, p, st, d)
                    for m, arr in (r or {}).items():
                        if arr is not None and len(arr):
                            extra.setdefault(m, []).append(arr)
        for m, lst in extra.items():
            if out.get(m) is not None:
                lst = lst + [out[m]]  # ribbons first: render.py truncates each mesh list at 32768
            out[m] = np.concatenate(lst, 0).astype(np.float32)
        return out

    W.__init__, W.step, W._step_movers, W.instances = w_init, w_step, w_step_movers, w_instances

    # ---- cameras
    C = sim.Camera
    orig_cu = C.update

    def c_update(self, t, dt):
        op, d = self.chain[0] if self.chain else (None, None)
        if op in CAM_OPS:
            h = EXT[op].agent
            if not hasattr(self, "_ext_st"):
                self._ext_st = h.init(self, d)
            self.eye, self.tgt, roll = h.update(self, self._ext_st, d, t, dt)
            _ROLL[0] = roll
        else:
            _ROLL[0] = 0.0
        return orig_cu(self, t, dt)  # unknown base op: keeps self.eye / tgt, applies cam.shake on top

    C.update = c_update

    # ---- camera roll (banking) through render.look_at
    orig_look = render.look_at

    def look_at(eye, tgt):
        r, u, f = orig_look(eye, tgt)
        a = _ROLL[0]
        if a:
            ca, sa = math.cos(a), math.sin(a)
            r, u = r * ca + u * sa, -r * sa + u * ca
        return r, u, f

    render.look_at = look_at

    # ---- remember the active Renderer so ag.texflow can evaluate any T input on the GPU
    R = render.Renderer
    orig_render = R.render

    def r_render(self, *a, **kw):
        prev = _CUR_RENDERER[0]
        _CUR_RENDERER[0] = self
        try:
            return orig_render(self, *a, **kw)
        finally:
            _CUR_RENDERER[0] = prev

    R.render = r_render


if "agenome.sim" in sys.modules and "ops_ext" in sys.modules:
    pass  # install_hooks is called by ops_ext.install()


# ====================================================================== 0xD5 ag.flow (requested by the seed-design thread)
def _flow_init(world, p, d):
    return {}


def _flow_field(st, d, pos, t):
    k = TAU / max(d["scale"], 1e-3)
    off = d["drift"] * t
    x, y, z = (pos[:, 0] * k + off), pos[:, 1] * k, pos[:, 2] * k
    A, B, C = d["a"], d["b"], d["c"]
    f = int(d["field"])
    if f == 0:  # Arnold-Beltrami-Childress: chaotic braided lanes + coherent tubes (a Beltrami field, curl v = v)
        v = np.stack([A * np.sin(z) + C * np.cos(y), B * np.sin(x) + A * np.cos(z), C * np.sin(y) + B * np.cos(x)], 1)
    elif f == 1:  # helical Beltrami lanes along y (corkscrew tubes)
        v = np.stack([A * np.cos(y), B * np.ones_like(y) * 0.6, A * np.sin(y)], 1) + 0.3 * np.stack(
            [np.zeros_like(x), C * np.sin(x), np.zeros_like(x)], 1)
    elif f == 2:  # toroidal ring vortex (Hill-like): rolls through a ring of radius scale/2
        r = np.hypot(pos[:, 0], pos[:, 2]) + 1e-6
        R = d["scale"] * 0.5
        dr, dy = r - R, pos[:, 1]
        e = np.stack([pos[:, 0] / r, np.zeros_like(r), pos[:, 2] / r], 1)
        tang = np.stack([-pos[:, 2] / r, np.zeros_like(r), pos[:, 0] / r], 1)
        v = (-dy[:, None] * e * A + dr[:, None] * np.array([0, 1.0, 0]) * A) / (np.hypot(dr, dy)[:, None] + R * 0.3) * R * 0.5
        v = v + tang * C * 0.5
    else:  # Taylor-Green cells (counter-rotating vortex lattice)
        v = np.stack([A * np.sin(x) * np.cos(y) * np.cos(z), -B * np.cos(x) * np.sin(y) * np.cos(z), C * 0.3 * np.sin(z) * np.ones_like(x)], 1)
    n = np.linalg.norm(v, axis=1, keepdims=True)
    return (v / (np.sqrt((n ** 2).mean()) + 1e-6)).astype(np.float32)


def _flow_acc(world, p, st, d, dt):
    v = _flow_field(st, d, p.pos, world.t)
    return d["gain"] * (p.speed * 1.2 * v - p.vel)


register(Op(0xD5, "ag.flow", "A", ("A",), (
    P("field", "enum", 0, 4, 0), Pv("scale", "exp", 2, 500, 60), Pv("a", "lin", 0, 2, 1.0), Pv("b", "lin", 0, 2, 0.7),
    Pv("c", "lin", 0, 2, 0.43), Pv("gain", "exp", 0.05, 20, 2.0), P("drift", "scub", 0, 2, 128)),
    "steer along a closed-form 3D flow: ABC (Beltrami, braided chaotic lanes) / helix / ring vortex / Taylor-Green cells",
    "Arnold-Beltrami-Childress flow"), family="agents",
    agent=AgentHook(_flow_init, _flow_acc, _flow_static(_flow_field)))
