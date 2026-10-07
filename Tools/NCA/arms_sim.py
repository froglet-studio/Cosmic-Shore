"""Species 4: a co-evolved PREDATOR swarm and PREY swarm in one 3D cell (Tools/NCA/DISCOVERIES.md, "The four
swarm species"). This file is the WORLD and the two POLICIES; training is arms_train.py, measurement arms_eval.py.

numpy only (no torch in this container, and CUDA buys nothing at this size). Everything is batched over B
independent encounters so an evolution-strategies generation is a handful of big array ops, not Python loops
over agents: state is [B, N, ...] per species, each encounter carries its OWN policy parameters.

Units are game world units and seconds (Tools/Ecology/common/arena.py: a tadpole is ~5 u, a vessel cruises
~120 u/s). The training cell is a 200 u pond, a sixth of a game cell, so one encounter is dense enough to learn
from; every perception radius is well under the cell's diameter, so the policies stay LOCAL.

ASYMMETRY (designed; see NOTE.md for the why):
                     prey (tadpole grazer)          predator (hunter)
  population         120                            8
  body radius        2.5 u                          5 u
  top speed          60 u/s, sustained              45 u/s cruise, 100 u/s BURST on stamina (3 s full, 8 s refill)
  acceleration       300 u/s^2                      150 u/s^2
  turn rate          10 rad/s (turn radius 6 u)     3 rad/s (33 u at burst)
  perception R       50 u                           80 u
  eats               the food field (grazing)       prey (a catch moves the prey's whole mass into the predator)

POLICY (one shared MLP per species, LOCAL inputs only, all in the agent's own body frame):
  self      speed, (pred: stamina, handling), membrane proximity + outward direction if the membrane is within R,
            food at own position + its gradient
  own kind  within R: count, mean offset, mean velocity, nearest offset, mean SIGNAL (a 1-channel call each agent
            emits; the only "secretion", read by neighbours within R)
  other kind within R: count, mean offset, nearest offset, nearest's relative velocity, (pred: crowding at the
            nearest prey = what the confusion rule punishes)
  outputs   acceleration (3, body frame), signal (1), (pred: burst gate)
No census, no global frame, no cell centre unless the membrane itself is within perception.

ECONOMY (mass is conserved; nothing dies on a timer):
  food field  a coarse grid of grazeable mass; prey graze it; it regrows ONLY from the nutrient pool, which is
              refilled by metabolism and by starved bodies. food + pool + prey + predators = constant.
  catch       a predator in contact with a prey catches it with probability 1/(1 + confusion * crowd), crowd = other
              prey within 10 u of the target (the confusion effect: a DESIGNED perceptual limit of the predator);
              the prey's whole mass moves into the predator; the predator then HANDLES it for 1 s (slowed, no catch).
  metabolism  (eco mode) both species burn mass at a base rate plus a speed^2 term; burnt mass goes to the pool.
  starvation  (eco mode) an agent whose mass falls below half a birth mass dies; its body goes back to the pool.
  birth       (eco mode) an agent with twice a birth mass splits in two. Births are funded only by mass eaten.
Training runs a FIXED ROSTER (no births, starvation off, 30 s encounters) so fitness has a stable denominator;
arms_eval runs the OPEN economy for minutes to check collapse (prey extinction, predator starvation).
"""
from __future__ import annotations

import math
from dataclasses import dataclass, field, asdict

import numpy as np

F32 = np.float32


@dataclass
class Cfg:
    R_cell: float = 200.0
    dt: float = 0.1
    n_prey: int = 120
    n_pred: int = 8
    # prey
    prey_v: float = 60.0
    prey_acc: float = 300.0
    prey_turn: float = 10.0
    prey_R: float = 50.0
    prey_r0: float = 5.0          # same-kind soft spacing
    prey_size: float = 2.5
    # predators
    pred_v: float = 45.0
    pred_burst: float = 100.0
    pred_acc: float = 150.0
    pred_turn: float = 3.0
    pred_R: float = 80.0
    pred_r0: float = 14.0
    pred_size: float = 5.0
    stam_drain: float = 1.0 / 3.0  # per second of burst (3 s from full)
    stam_regen: float = 0.125      # per second not bursting (8 s to refill)
    catch_r: float = 7.5
    handle: float = 1.0
    confusion: float = 0.3
    conf_r: float = 10.0
    # food (grid over the cell's bounding cube)
    G: int = 12
    food_patches: int = 6
    food_cap: float = 1.0          # per-cell capacity at a patch centre
    graze_rate: float = 0.08       # mass/s a prey can graze from a full cell
    graze_half: float = 0.3        # Michaelis constant (cell food at which intake is half max)
    regrow: float = 0.05           # per second toward capacity, drawn from the pool
    # economy
    eco: bool = False
    m_prey: float = 1.0
    m_pred: float = 4.0
    prey_burn: tuple = (0.004, 0.008)   # base, *speed^2 (per second, fraction of m_prey)
    pred_burn: tuple = (0.010, 0.030)   # base, *(speed/burst)^2 (per second, fraction of m_pred)
    cap_prey: int = 0              # 0 = n_prey (no births possible); eco runs set slots
    cap_pred: int = 0
    pool0: float = 30.0            # nutrient pool at start (eco)
    pred_split: float = 2.0        # a predator splits at pred_split * m_pred (prey always at 2 * m_prey)
    sated: float = 0.0             # >0: a predator heavier than sated * m_pred cannot burst (a designed satiety gate)

    def caps(self):
        return (self.cap_prey or self.n_prey, self.cap_pred or self.n_pred)


# ----------------------------------------------------------------------------------------------- policies ---

PREY_IN, PREY_OUT = 31, 4
PRED_IN, PRED_OUT = 36, 5
HID = 32


def n_params(d_in, d_out, h=HID):
    return d_in * h + h + h * h + h + h * d_out + d_out


def init_params(rng, d_in, d_out, h=HID, scale=1.0):
    p = []
    for a, b, s in ((d_in, h, 1.0), (h, h, 1.0), (h, d_out, 0.1)):
        p.append(rng.normal(0, scale * s / math.sqrt(a), a * b))
        p.append(np.zeros(b))
    return np.concatenate(p).astype(F32)


def mlp(theta, x, d_in, d_out, h=HID):
    """theta [B, P] (one parameter vector per encounter), x [B, N, d_in] -> [B, N, d_out] in (-1, 1)."""
    B = theta.shape[0]
    o = 0
    def take(n, shape):
        nonlocal o
        t = theta[:, o:o + n].reshape(B, *shape); o += n; return t
    W1 = take(d_in * h, (d_in, h)); b1 = take(h, (1, h))
    W2 = take(h * h, (h, h)); b2 = take(h, (1, h))
    W3 = take(h * d_out, (h, d_out)); b3 = take(d_out, (1, d_out))
    z = np.tanh(np.matmul(x, W1) + b1)
    z = np.tanh(np.matmul(z, W2) + b2)
    return np.tanh(np.matmul(z, W3) + b3)


# ------------------------------------------------------------------------------------------------- world ---

def _norm(v, eps=1e-6):
    return np.sqrt((v * v).sum(-1, keepdims=True) + eps)


def _frame(fwd, up):
    """Re-orthonormalise a body frame (forward, right, up) carried per agent (parallel transport, no world up)."""
    f = fwd / _norm(fwd)
    r = np.cross(f, up)
    rn = _norm(r)
    bad = rn[..., 0] < 1e-3
    if bad.any():                                   # up collinear with forward: pick any perpendicular
        alt = np.cross(f, np.array([0.31, 0.83, 0.47], F32))
        r = np.where(bad[..., None], alt, r); rn = _norm(r)
    r = r / rn
    u = np.cross(r, f)
    return f.astype(F32), r.astype(F32), u.astype(F32)


def _to_local(v, f, r, u):
    """v [..., 3] (broadcast against f/r/u [..., 3]) -> body-frame components (forward, right, up)."""
    return np.stack([(v * f).sum(-1), (v * r).sum(-1), (v * u).sum(-1)], -1)


@dataclass
class State:
    pos: list            # [prey, pred] each [B, N, 3]
    vel: list
    fwd: list
    up: list
    alive: list          # [B, N] bool
    mass: list           # [B, N]
    sig: list            # [B, N] emitted signal
    stam: np.ndarray     # [B, Np]
    hand: np.ndarray     # [B, Np] seconds of handling left
    burst: np.ndarray    # [B, Np] bool, bursting this step
    food: np.ndarray     # [B, G, G, G]
    pool: np.ndarray     # [B]
    t: float = 0.0
    # ledgers
    catches: np.ndarray = None    # [B, Np] catches per predator
    grazed: np.ndarray = None     # [B, Nq] food grazed per prey
    caught_t: np.ndarray = None   # [B, Nq] time a prey was caught (-1 alive)
    events: list = field(default_factory=list)   # (b, t, pred, prey, x, y, z) catches, for eval
    births: np.ndarray = None     # [B, 2]
    starved: np.ndarray = None    # [B, 2]


def food_template(cfg: Cfg, rng, B):
    G = cfg.G
    h = 2 * cfg.R_cell / G
    c = (np.arange(G) + 0.5) * h - cfg.R_cell
    X, Y, Z = np.meshgrid(c, c, c, indexing="ij")
    P = np.stack([X, Y, Z], -1)                                    # [G,G,G,3]
    inside = np.linalg.norm(P, axis=-1) < cfg.R_cell * 0.95
    out = np.zeros((B, G, G, G), F32)
    for b in range(B):
        d = rng.normal(size=(cfg.food_patches, 3)); d /= np.linalg.norm(d, axis=1, keepdims=True)
        cen = d * (cfg.R_cell * np.cbrt(rng.uniform(0.05, 0.6, cfg.food_patches)))[:, None]
        f = np.zeros((G, G, G))
        for k in range(cfg.food_patches):
            f += np.exp(-((P - cen[k]) ** 2).sum(-1) / (2 * 30.0 ** 2))
        out[b] = np.clip(f, 0, 1) * cfg.food_cap * inside
    return out


def _ball(rng, shape, r_hi, r_lo=0.0):
    d = rng.normal(size=shape + (3,)); d /= np.linalg.norm(d, axis=-1, keepdims=True)
    r = np.cbrt(r_lo ** 3 + rng.random(shape) * (r_hi ** 3 - r_lo ** 3))
    return (d * r[..., None]).astype(F32)


def reset(cfg: Cfg, B: int, seed: int | np.ndarray):
    """B encounters. `seed` may be an int (one stream) or an array of B ints (common random numbers: encounters
    with equal seeds start identically)."""
    seeds = np.full(B, seed) if np.isscalar(seed) else np.asarray(seed)
    Nq, Np = cfg.caps()
    pos, vel, fwd, up, alive, mass = [[], []], [[], []], [[], []], [[], []], [[], []], [[], []]
    food = np.zeros((B, cfg.G, cfg.G, cfg.G), F32)
    for b in range(B):
        rng = np.random.default_rng(int(seeds[b]))
        food[b] = food_template(cfg, rng, 1)[0]
        for k, (n, cap, v0) in enumerate(((cfg.n_prey, Nq, cfg.prey_v), (cfg.n_pred, Np, cfg.pred_v))):
            # prey start scattered through the pond; predators start in one loose group off to one side
            if k == 0:
                p = _ball(rng, (cap,), cfg.R_cell * 0.85)
            else:
                c = _ball(rng, (1,), cfg.R_cell * 0.7, cfg.R_cell * 0.5)[0]
                p = c + _ball(rng, (cap,), 25.0)
            d = rng.normal(size=(cap, 3)).astype(F32); d /= np.linalg.norm(d, axis=1, keepdims=True)
            u = rng.normal(size=(cap, 3)).astype(F32)
            pos[k].append(p); vel[k].append(d * v0 * 0.3); fwd[k].append(d); up[k].append(u)
            a = np.zeros(cap, bool); a[:n] = True; alive[k].append(a)
            mass[k].append(np.where(a, cfg.m_prey if k == 0 else cfg.m_pred, 0.0).astype(F32))
    st = State(pos=[np.stack(x).astype(F32) for x in pos], vel=[np.stack(x).astype(F32) for x in vel],
               fwd=[np.stack(x).astype(F32) for x in fwd], up=[np.stack(x).astype(F32) for x in up],
               alive=[np.stack(x) for x in alive], mass=[np.stack(x) for x in mass],
               sig=[np.zeros((B, Nq), F32), np.zeros((B, Np), F32)],
               stam=np.ones((B, Np), F32), hand=np.zeros((B, Np), F32), burst=np.zeros((B, Np), bool),
               food=food, pool=np.full(B, cfg.pool0 if cfg.eco else 0.0, F32))
    for k in range(2):
        st.fwd[k], _, st.up[k] = _frame(st.fwd[k], st.up[k])
    st.catches = np.zeros((B, Np), F32); st.grazed = np.zeros((B, Nq), F32)
    st.burned_q = np.zeros((B, Nq), F32); st.burned_p = np.zeros((B, Np), F32)
    st.attempts = np.zeros((B, Np), F32)
    st.caught_t = np.full((B, Nq), -1.0, F32)
    st.births = np.zeros((B, 2), np.int32); st.starved = np.zeros((B, 2), np.int32)
    st.food0 = food.copy()
    return st


def _cell_index(cfg, pos):
    G = cfg.G
    i = ((pos + cfg.R_cell) / (2 * cfg.R_cell) * G).astype(np.int64)
    return np.clip(i, 0, G - 1)


def _sample_food(cfg, food, pos):
    """food value and central-difference gradient (per unit length, scaled to per-perception) at pos [B,N,3]."""
    B = pos.shape[0]
    G = cfg.G
    idx = _cell_index(cfg, pos)
    bi = np.arange(B)[:, None]
    val = food[bi, idx[..., 0], idx[..., 1], idx[..., 2]]
    grad = np.zeros(pos.shape, F32)
    for a in range(3):
        ip = idx.copy(); ip[..., a] = np.minimum(idx[..., a] + 1, G - 1)
        im = idx.copy(); im[..., a] = np.maximum(idx[..., a] - 1, 0)
        grad[..., a] = food[bi, ip[..., 0], ip[..., 1], ip[..., 2]] - food[bi, im[..., 0], im[..., 1], im[..., 2]]
    return val, grad


def _dist(pa, pb):
    """pairwise distance [B, Na, Nb] by the matmul identity (no [B, Na, Nb, 3] tensor)."""
    d2 = (pa * pa).sum(-1)[..., :, None] + (pb * pb).sum(-1)[..., None, :] - 2 * np.matmul(pa, pb.transpose(0, 2, 1))
    return np.sqrt(np.maximum(d2, 1e-6))


def _pair(pa, pb, R, alive_b, same=False):
    d = _dist(pa, pb)
    m = (d < R) & alive_b[:, None, :]
    if same:
        n = pa.shape[1]
        m &= ~np.eye(n, dtype=bool)[None]
    return d, m


def _gather(X, j):
    """X [B, Nb, C], j [B, Na] -> [B, Na, C]"""
    return np.take_along_axis(X, j[..., None], 1)


def _agg(pa, pb, d, m, vel_b, sig_b, f, r, u, R, vscale, cnt_scale):
    """Neighbour summary in the body frame: count, mean offset, mean velocity, nearest offset, nearest index."""
    mf = m.astype(F32)
    cnt = mf.sum(-1)
    inv = 1.0 / np.maximum(cnt, 1.0)
    mean_off = (np.matmul(mf, pb) * inv[..., None] - pa) * (cnt > 0)[..., None]
    mean_vel = np.matmul(mf, vel_b) * inv[..., None]
    dm = np.where(m, d, np.inf)
    j = dm.argmin(-1)
    dmin = dm.min(-1)
    has = np.isfinite(dmin)
    near = (_gather(pb, j) - pa) * has[..., None]
    msig = (mf * sig_b[:, None, :]).sum(-1) * inv if sig_b is not None else None
    L = lambda v: _to_local(v, f, r, u)
    return dict(cnt=cnt / cnt_scale, off=L(mean_off) / R, vel=L(mean_vel) / vscale, near=L(near) / R, j=j, has=has,
                sig=msig, dmin=np.where(has, dmin, R), d=d)


def observe(cfg: Cfg, st: State, ghost_pred=None, ghost_prey=None):
    """Body-frame local observations for both species. Returns (x_prey [B,Nq,31], x_pred [B,Np,36], aux).
    ghost_pred / ghost_prey: optional (pos [B,G,3], vel [B,G,3]) extra bodies the PREY perceive as predators /
    the PREDATORS perceive as prey (the player test puts the vessel in here). They never enter same-kind sums."""
    Pq, Pp = st.pos
    Vq, Vp = st.vel
    Aq, Ap = st.alive
    Pp_v, Vp_v, Ap_v = Pp, Vp, Ap
    Pq_v, Vq_v, Aq_v = Pq, Vq, Aq
    if ghost_pred is not None:
        Pp_v = np.concatenate([Pp, ghost_pred[0]], 1).astype(F32); Vp_v = np.concatenate([Vp, ghost_pred[1]], 1).astype(F32)
        Ap_v = np.concatenate([Ap, np.ones(ghost_pred[0].shape[:2], bool)], 1)
    if ghost_prey is not None:
        Pq_v = np.concatenate([Pq, ghost_prey[0]], 1).astype(F32); Vq_v = np.concatenate([Vq, ghost_prey[1]], 1).astype(F32)
        Aq_v = np.concatenate([Aq, np.ones(ghost_prey[0].shape[:2], bool)], 1)
    fq, rq, uq = _frame(st.fwd[0], st.up[0])
    fp, rp, up_ = _frame(st.fwd[1], st.up[1])
    st.fwd[0], st.up[0] = fq, uq
    st.fwd[1], st.up[1] = fp, up_
    out = []
    for k in range(2):
        P, V, f, r, u = (Pq, Vq, fq, rq, uq) if k == 0 else (Pp, Vp, fp, rp, up_)
        R = cfg.prey_R if k == 0 else cfg.pred_R
        vmax = cfg.prey_v if k == 0 else cfg.pred_burst
        rad = _norm(P)
        prox = np.clip(1 - (cfg.R_cell - rad[..., 0]) / R, 0, 1)
        mem = _to_local(P / rad, f, r, u) * prox[..., None]
        fv, fg = _sample_food(cfg, st.food, P)
        fg = _to_local(fg, f, r, u)
        sp = np.sqrt((V * V).sum(-1)) / vmax
        out.append((P, V, f, r, u, R, prox, mem, fv, fg, sp))
    # prey <- prey, prey <- pred
    (P, V, f, r, u, R, prox, mem, fv, fg, sp) = out[0]
    d, m = _pair(Pq, Pq, cfg.prey_R, Aq, same=True)
    same = _agg(Pq, Pq, d, m, Vq, st.sig[0], f, r, u, R, cfg.prey_v, 10.0)
    st.dqq = d                                          # reused by repulsion and the confusion rule
    d2, m2 = _pair(Pq, Pp_v, cfg.prey_R, Ap_v)
    oth = _agg(Pq, Pp_v, d2, m2, Vp_v, None, f, r, u, R, cfg.pred_burst, 3.0)
    relv = _gather(Vp_v, oth["j"])
    relv = _to_local(relv - V, f, r, u) / cfg.pred_burst * oth["has"][..., None]
    xq = np.concatenate([sp[..., None], prox[..., None], mem, fv[..., None], fg,
                         same["cnt"][..., None], same["off"], same["vel"], same["near"], same["sig"][..., None],
                         oth["cnt"][..., None], oth["near"], relv, (1 - oth["dmin"] / R)[..., None] * oth["has"][..., None],
                         oth["off"]], -1)
    # pred <- pred, pred <- prey
    (P, V, f, r, u, R, prox, mem, fv, fg, sp) = out[1]
    d, m = _pair(Pp, Pp, cfg.pred_R, Ap, same=True)
    same = _agg(Pp, Pp, d, m, Vp, st.sig[1], f, r, u, R, cfg.pred_burst, 3.0)
    st.dpp = d
    d2, m2 = _pair(Pp, Pq_v, cfg.pred_R, Aq_v)
    oth = _agg(Pp, Pq_v, d2, m2, Vq_v, None, f, r, u, R, cfg.prey_v, 20.0)
    jn = oth["j"]
    relv = _to_local(_gather(Vq_v, jn) - V, f, r, u) / cfg.prey_v * oth["has"][..., None]
    # crowding at the nearest prey (what the confusion rule punishes): row jn of the prey-prey distances
    crowd_all = ((st.dqq < cfg.conf_r) & Aq[:, None, :]).sum(-1)          # [B, Nq] (self excluded: diag is 0 < r)
    if ghost_prey is not None:                          # a ghost is never in a crowd
        crowd_all = np.concatenate([crowd_all, np.ones(ghost_prey[0].shape[:2], crowd_all.dtype)], 1)
    crowd = (_gather(crowd_all[..., None].astype(F32), jn)[..., 0] - 1).clip(0) * oth["has"]
    aux_near_pred = jn
    xp = np.concatenate([sp[..., None], st.stam[..., None], (st.hand / cfg.handle)[..., None], prox[..., None], mem,
                         fv[..., None], fg,
                         same["cnt"][..., None], same["off"], same["vel"], same["near"], same["sig"][..., None],
                         oth["cnt"][..., None], oth["off"], oth["vel"], oth["near"], relv, (crowd / 5.0)[..., None]], -1)
    return xq.astype(F32), xp.astype(F32), dict(fr=[(fq, rq, uq), (fp, rp, up_)], pred_target=aux_near_pred)


def _steer(V, fwd, a_world, dt, acc, vmax, turn):
    """Apply a bounded acceleration, then a turn-rate limit, then the speed cap."""
    vn = V + a_world * (acc * dt)
    s0 = np.sqrt((V * V).sum(-1)); s1 = np.sqrt((vn * vn).sum(-1))
    f0 = np.where(s0[..., None] > 1e-3, V / np.maximum(s0, 1e-6)[..., None], fwd)
    f1 = np.where(s1[..., None] > 1e-3, vn / np.maximum(s1, 1e-6)[..., None], f0)
    c = np.clip((f0 * f1).sum(-1), -1.0, 1.0)
    ang = np.arccos(c)
    lim = turn * dt
    t = np.where(ang > lim, lim / np.maximum(ang, 1e-6), 1.0)
    # slerp f0 -> f1 by t (for ang ~ pi the plane is ill-defined; nudge with an orthogonal vector)
    sa = np.sin(ang)
    ok = sa > 1e-4
    w0 = np.where(ok, np.sin((1 - t) * ang) / np.maximum(sa, 1e-6), 1 - t)
    w1 = np.where(ok, np.sin(t * ang) / np.maximum(sa, 1e-6), t)
    fd = w0[..., None] * f0 + w1[..., None] * f1
    fd = fd / _norm(fd)
    s = np.minimum(s1, vmax if np.isscalar(vmax) else vmax)
    return (fd * s[..., None]).astype(F32), fd.astype(F32)


def _rotate_to_world(a, fr):
    f, r, u = fr
    return a[..., 0:1] * f + a[..., 1:2] * r + a[..., 2:3] * u


def _repel(P, alive, r0, d, k=0.5):
    """soft same-kind spacing from the distance matrix d (pre-move positions): sum_j w_ij (P_i - P_j)."""
    w = np.clip(r0 - d, 0, None) / (r0 * d) * (k * r0) * (alive[:, None, :] & alive[:, :, None])
    n = P.shape[1]
    w[:, np.arange(n), np.arange(n)] = 0
    return w.sum(-1)[..., None] * P - np.matmul(w, P)


def step(cfg: Cfg, st: State, th_prey, th_pred, extra=None, rng=None, record_events=False):
    """One decision + physics step of dt. th_* [B, P] per-encounter parameters. `extra` optional dict for the
    player test: {"pos": [B,3], "vel": [B,3], "radius": r, "seen_by_prey_as_pred": bool, "seen_by_pred_as_prey": bool}
    (handled in observe_with_vessel in arms_eval; here only the physical obstacle)."""
    dt = cfg.dt
    if extra is None:
        xq, xp, aux = observe(cfg, st)
    else:
        xq, xp, aux = observe(cfg, st, extra.get("ghost_pred"), extra.get("ghost_prey"))
    st.last_aux = aux
    oq = mlp(th_prey, xq, PREY_IN, PREY_OUT)
    op = mlp(th_pred, xp, PRED_IN, PRED_OUT)
    return apply(cfg, st, oq, op, aux, extra=extra, rng=rng, record_events=record_events)


def apply(cfg, st, oq, op, aux, extra=None, rng=None, record_events=False):
    dt = cfg.dt
    B = st.pos[0].shape[0]
    Aq, Ap = st.alive
    # --- predators: burst gate on stamina, handling slows them ---
    want = op[..., 4] > 0
    st.burst = want & (st.stam > 0.02) & (st.hand <= 0)
    if cfg.sated > 0:
        st.burst &= st.mass[1] < cfg.sated * cfg.m_pred
    st.stam = np.clip(st.stam + np.where(st.burst, -cfg.stam_drain, cfg.stam_regen) * dt, 0, 1).astype(F32)
    vmax_p = np.where(st.burst, cfg.pred_burst, cfg.pred_v) * np.where(st.hand > 0, 0.5, 1.0)
    st.hand = np.maximum(st.hand - dt, 0).astype(F32)
    # --- steering ---
    aq = _rotate_to_world(oq[..., :3], aux["fr"][0])
    ap = _rotate_to_world(op[..., :3], aux["fr"][1])
    Vq, st.fwd[0] = _steer(st.vel[0], st.fwd[0], aq, dt, cfg.prey_acc, cfg.prey_v, cfg.prey_turn)
    Vp, st.fwd[1] = _steer(st.vel[1], st.fwd[1], ap, dt, cfg.pred_acc, vmax_p, cfg.pred_turn)
    st.sig[0] = oq[..., 3].astype(F32); st.sig[1] = op[..., 3].astype(F32)
    Vq *= Aq[..., None]; Vp *= Ap[..., None]
    P0q, P0p = st.pos[0], st.pos[1]
    Pq = P0q + Vq * dt + _repel(P0q, Aq, cfg.prey_r0, st.dqq) * 1.0
    Pp = P0p + Vp * dt + _repel(P0p, Ap, cfg.pred_r0, st.dpp) * 1.0
    if extra is not None and extra.get("pos") is not None:              # the vessel is a solid obstacle to both
        for P in (Pq, Pp):
            dv = P - extra["pos"][:, None, :]
            dd = _norm(dv)
            push = np.clip(extra["radius"] - dd[..., 0], 0, None)
            P += dv / dd * push[..., None]
    # membrane: pushed back inside, outward velocity removed
    for k, (P, V, size) in enumerate(((Pq, Vq, cfg.prey_size), (Pp, Vp, cfg.pred_size))):
        rad = _norm(P); lim = cfg.R_cell - size
        over = rad[..., 0] > lim
        if over.any():
            n = P / rad
            P[:] = np.where(over[..., None], n * lim, P)
            vout = (V * n).sum(-1, keepdims=True)
            V[:] = np.where(over[..., None] & (vout > 0), V - vout * n, V)
    # --- catches (swept: closest approach of the relative motion within the step) ---
    rel0 = P0q[:, None, :, :] - P0p[:, :, None, :]                 # [B, Np, Nq, 3]
    rel1 = Pq[:, None, :, :] - Pp[:, :, None, :]
    dr = rel1 - rel0
    tt = np.clip(-(rel0 * dr).sum(-1) / np.maximum((dr * dr).sum(-1), 1e-9), 0, 1)
    closest = rel0 + tt[..., None] * dr
    dmin = np.sqrt((closest * closest).sum(-1))
    can = Ap & (st.hand <= 0)
    touch = (dmin < cfg.catch_r) & Aq[:, None, :] & can[:, :, None]
    if touch.any():
        rng = rng or np.random.default_rng()
        bs, ps = np.nonzero(touch.any(-1))
        for b, p in zip(bs, ps):
            cand = np.nonzero(touch[b, p] & st.alive[0][b])[0]
            if len(cand) == 0:
                continue
            q = cand[np.argmin(dmin[b, p, cand])]
            crowd = int(((np.sqrt(((Pq[b] - Pq[b, q]) ** 2).sum(-1)) < cfg.conf_r) & st.alive[0][b]).sum()) - 1
            st.attempts[b, p] += 1
            if rng.random() < 1.0 / (1.0 + cfg.confusion * max(crowd, 0)):
                st.alive[0][b, q] = False
                st.mass[1][b, p] += st.mass[0][b, q]; st.mass[0][b, q] = 0.0
                st.catches[b, p] += 1; st.caught_t[b, q] = st.t
                st.hand[b, p] = cfg.handle
                if record_events:
                    st.events.append((int(b), round(st.t, 2), int(p), int(q), *[float(x) for x in Pq[b, q]]))
    Aq = st.alive[0]
    # --- grazing (Michaelis-Menten per prey, shared out when a cell is crowded) ---
    idx = _cell_index(cfg, Pq)
    G = cfg.G
    flat = (idx[..., 0] * G + idx[..., 1]) * G + idx[..., 2]           # [B, Nq]
    food = st.food.reshape(B, -1)
    fv = np.take_along_axis(food, flat, 1)
    want_g = cfg.graze_rate * dt * fv / (fv + cfg.graze_half) * Aq
    demand = np.zeros_like(food)
    np.add.at(demand, (np.arange(B)[:, None].repeat(flat.shape[1], 1), flat), want_g)
    scale = np.where(demand > food, food / np.maximum(demand, 1e-9), 1.0)
    got = want_g * np.take_along_axis(scale, flat, 1)
    np.add.at(food, (np.arange(B)[:, None].repeat(flat.shape[1], 1), flat), -got)
    st.grazed += got.astype(F32)
    # --- metabolism (always LEDGERED, so training fitness can charge it; only deducted in eco mode) ---
    sq = np.sqrt((Vq * Vq).sum(-1)) / cfg.prey_v
    sp = np.sqrt((Vp * Vp).sum(-1)) / cfg.pred_burst
    burn_q = np.minimum(st.mass[0], (cfg.prey_burn[0] + cfg.prey_burn[1] * sq ** 2) * cfg.m_prey * dt) * Aq
    burn_p = np.minimum(st.mass[1], (cfg.pred_burn[0] + cfg.pred_burn[1] * sp ** 2) * cfg.m_pred * dt) * st.alive[1]
    st.burned_q += burn_q.astype(F32); st.burned_p += burn_p.astype(F32)
    # --- economy (eco mode only): metabolism to the pool, starvation, births, regrowth from the pool ---
    if cfg.eco:
        st.mass[0] = (st.mass[0] + got - burn_q).astype(F32)
        st.mass[1] = (st.mass[1] - burn_p).astype(F32)
        st.pool += burn_q.sum(1) + burn_p.sum(1)
        for k, mb in ((0, cfg.m_prey), (1, cfg.m_pred)):
            starve = st.alive[k] & (st.mass[k] < 0.5 * mb)
            if starve.any():
                st.pool += (st.mass[k] * starve).sum(1)
                st.mass[k] = np.where(starve, 0, st.mass[k]).astype(F32)
                st.alive[k] = st.alive[k] & ~starve
                st.starved[:, k] += starve.sum(1)
            ready = st.alive[k] & (st.mass[k] >= (2.0 if k == 0 else cfg.pred_split) * mb)
            if ready.any():
                for b, i in zip(*np.nonzero(ready)):
                    free = np.nonzero(~st.alive[k][b])[0]
                    if len(free) == 0:
                        continue
                    j = free[0]
                    half = st.mass[k][b, i] / 2
                    st.mass[k][b, i] = half; st.mass[k][b, j] = half
                    st.alive[k][b, j] = True
                    jit = (rng or np.random.default_rng()).normal(0, 1.0, 3).astype(F32)
                    (Pq if k == 0 else Pp)[b, j] = (Pq if k == 0 else Pp)[b, i] + jit
                    (Vq if k == 0 else Vp)[b, j] = -(Vq if k == 0 else Vp)[b, i]
                    st.fwd[k][b, j] = -st.fwd[k][b, i]; st.up[k][b, j] = st.up[k][b, i]
                    if k == 1:
                        st.stam[b, j] = st.stam[b, i]; st.hand[b, j] = 0
                    st.births[b, k] += 1
                    if k == 0:
                        st.caught_t[b, j] = -1; st.grazed[b, j] = 0
        # regrowth toward the template capacity, paid from the pool (never created)
        cap = st.food0.reshape(B, -1)
        need = np.clip(cap - food, 0, None) * cfg.regrow * dt
        tot = need.sum(1)
        frac = np.where(tot > st.pool, st.pool / np.maximum(tot, 1e-9), 1.0)
        add = need * frac[:, None]
        food += add
        st.pool -= add.sum(1)
    else:
        cap = st.food0.reshape(B, -1)
        food += np.clip(cap - food, 0, None) * cfg.regrow * dt
    st.food = food.reshape(st.food.shape).astype(F32)
    st.pos = [Pq.astype(F32), Pp.astype(F32)]
    st.vel = [Vq, Vp]
    st.t += dt
    return st


def total_mass(st: State):
    """Conservation audit (eco mode): food + pool + prey + predators, per encounter."""
    return st.food.reshape(st.food.shape[0], -1).sum(1) + st.pool + (st.mass[0] * st.alive[0]).sum(1) + \
        (st.mass[1] * st.alive[1]).sum(1)


def run(cfg, th_prey, th_pred, B, seed, steps, rng=None, record=None, record_events=False):
    """Roll B encounters for `steps`. record(st) is called after every step if given."""
    st = reset(cfg, B, seed)
    rng = rng or np.random.default_rng(int(np.asarray(seed).ravel()[0]) + 99)
    for _ in range(steps):
        step(cfg, st, th_prey, th_pred, rng=rng, record_events=record_events)
        if record is not None:
            record(st)
    return st


def cfg_dict(cfg):
    d = asdict(cfg)
    return {k: (list(v) if isinstance(v, tuple) else v) for k, v in d.items()}
