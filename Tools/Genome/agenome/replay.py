"""A recorded Amoebius session (targets/sessions/<name>) as a replay source for Renderer.render(replay=...).

    s = Session(path); s.frame(fi) -> {"t", "eye", "r", "u", "fwd", "fov", "inst"}
The camera's Unity quaternion (x, y, z, w) gives right/up/forward in Unity world axes (left-handed, +z forward); the
renderer only needs the three vectors. inst is filled by a builder (inst_fn(session, fi) -> instance lists), or empty.
"""
import json
import math
import os

import numpy as np


def quat_axes(q):
    x, y, z, w = q
    R = np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                  [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                  [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
    return R[:, 0], R[:, 1], R[:, 2]


class Session:
    def __init__(self, path, inst_fn=None, frames=None):
        self.path = path
        self.meta = json.load(open(os.path.join(path, "meta.json")))
        self.state = [json.loads(l) for l in open(os.path.join(path, "state.jsonl"))]
        self.inst_fn = inst_fn
        self.pick = list(frames) if frames is not None else list(range(len(self.state)))

    def real(self, variant=""):
        """uint8 [N,144,256,3] of the picked frames; variant "", "_nohud" or "_sky"."""
        a = np.load(os.path.join(self.path, f"frames_144{variant}.npy"), mmap_mode="r")
        return np.asarray(a[self.pick])

    def frame(self, fi):
        st = self.state[self.pick[fi]]
        c = st["camera"]
        r, u, f = quat_axes(c["rot"])
        inst = self.inst_fn(self, self.pick[fi]) if self.inst_fn else {}
        return {"t": st["t"], "eye": np.array(c["pos"], np.float32), "r": r, "u": u, "fwd": f, "fov": c["fov"], "inst": inst}


def quat_mats(q):
    """[N,4] Unity quaternions (x, y, z, w) -> [N,3,3] rotation matrices (columns = local axes in world)."""
    q = np.asarray(q, np.float64)
    x, y, z, w = q[:, 0], q[:, 1], q[:, 2], q[:, 3]
    R = np.empty((len(q), 3, 3))
    R[:, 0, 0] = 1 - 2 * (y * y + z * z); R[:, 0, 1] = 2 * (x * y - z * w); R[:, 0, 2] = 2 * (x * z + y * w)
    R[:, 1, 0] = 2 * (x * y + z * w); R[:, 1, 1] = 1 - 2 * (x * x + z * z); R[:, 1, 2] = 2 * (y * z - x * w)
    R[:, 2, 0] = 2 * (x * z - y * w); R[:, 2, 1] = 2 * (y * z + x * w); R[:, 2, 2] = 1 - 2 * (x * x + y * y)
    return R


# linear HDR (rim, face) by domain 1 Jade, 2 Ruby, 3 Blue, 4 Gold; the starting guess is the Photic table of
# docs/MECHANICS_SPEC.md section 7, fitted per build by the paired look fit
PALETTE0 = {1: ((0.0, 0.588, 1.135), (0.0, 0.158, 0.342)), 2: ((0.549, 0.0, 1.498), (0.120, 0.007, 0.309)),
            3: ((0.467, 0.537, 1.217), (0.023, 0.023, 0.347)), 4: ((0.996, 0.405, 0.019), (0.238, 0.125, 0.0))}


class PrismWorld:
    """The session's key prism lists as renderer instances (the game's prism graph: ie = -(200 + fresnel power))."""

    def __init__(self, session, palette=None, power=4.0, shield_mix=0.5):
        self.keys = {}
        for l in open(os.path.join(session.path, "prisms_key.jsonl")):
            d = json.loads(l)
            p = d["prisms"]
            self.keys[d["frame"]] = np.array(p, np.float64).reshape(-1, len(p[0]) if p else 13)
        self.kf = np.array(sorted(self.keys))
        self.palette = palette or PALETTE0
        self.power = power
        self.shield_mix = shield_mix
        self.shield_scale = 1.0

    def __call__(self, session, frame):
        k = self.kf[np.argmin(np.abs(self.kf - frame))]
        a = self.keys[int(k)]
        out = {}
        if not len(a):
            return out
        R = quat_mats(a[:, 4:8])
        S = a[:, 8:11]
        dom = a[:, 11].astype(int)
        flags = a[:, 12].astype(int)
        rim = np.array([self.palette.get(d, self.palette[3])[0] for d in dom])
        face = np.array([self.palette.get(d, self.palette[3])[1] for d in dom])
        if a.shape[1] >= 25:  # the recorder's per-prism values: bright rgba, dark rgba, spread xyz, material
            rim, face = a[:, 13:16], a[:, 17:20]
        sh = ((flags & 3) != 0)[:, None] & (a.shape[1] < 25)  # shielded prisms read paler
        rim = np.where(sh, rim + (1.0 - rim / max(rim.max(), 1e-6)) * self.shield_mix * 0.5, rim)
        rows = np.concatenate([a[:, 1:4], R[:, :, 0] * S[:, 0:1], R[:, :, 1] * S[:, 1:2], R[:, :, 2] * S[:, 2:3], rim,
                               np.zeros((len(a), 1)), np.full((len(a), 1), -(200.0 + self.power)), face], 1)
        # the game swaps a shielded prism's box for an octahedron and a super-shielded one's for a stellated
        # octahedron, both with semi-axes 3x the box half-extents (OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE)
        sup = (flags & 2) != 0
        shd = ((flags & 1) != 0) & ~sup
        big = rows.copy()
        big[:, 3:12] *= self.shield_scale
        for name, m, src in (("box", ~sup & ~shd, rows), ("octa", shd, big), ("stella", sup, big)):
            if m.any():
                out[name] = src[m].astype(np.float32)
        return out


def _rows(pos, ax, ay, az, bright, dark, power):
    n = len(pos)
    return np.concatenate([pos, ax, ay, az, bright, np.zeros((n, 1)), np.full((n, 1), -(200.0 + power)), dark], 1).astype(np.float32)


class VesselWorld:
    """Vessels, their jets and tails, and the crystals of a session line as renderer instances.

    A vessel is a dart the size of the hull's mesh with two engine pods; a trail is rebuilt from the vessel's own
    recorded path (emitters in hull space from world.json: four jets, two tails 12 u behind); a crystal is an
    icosahedron of the crystal's world size.
    """
    JETS = ((-1.08, -0.11, -1.61), (1.12, -0.11, -1.61), (-1.81, 0.15, 0.72), (1.82, 0.15, 0.72))
    TAILS = ((4.0, 0.0, -12.0), (-4.0, 0.0, -12.0))

    def __init__(self, session, hull=(4.5, 1.13, 6.7), hull_col=(0.1, 0.13, 0.24), pod_col=(0.7, 0.2, 0.25),
                 jet_secs=0.5, jet_width=0.12, jet_col=(0.1, 0.75, 1.0),
                 tail_secs=4.0, tail_width=1.0, tail_col=(0.0, 0.6, 0.62),
                 crystal=3.8, crystal_bright=(0.055, 0.51, 0.749), crystal_dark=(0.0, 0.02, 0.05)):
        self.__dict__.update(locals())
        self.fps = session.meta.get("fps", 15.0)

    def _trail(self, session, frame, v, emit, secs, width, eye, grad, soft=False):
        """Ribbons as SceneRenderer.CollectRibbon builds them: u is the distance fraction from the emitter to the
        oldest point, the strip faces the camera, colour = grad(u) (rgba, linear)."""
        n = max(int(math.ceil(secs * self.fps)), 1)
        hist = []
        for k in range(n + 1):
            if frame - k < 0:
                break
            w = next((x for x in session.state[frame - k]["vessels"] if x["id"] == v["id"]), None)
            if w is None or (hist and np.linalg.norm(np.array(w["pos"]) - hist[-1][0]) > 80.0):
                break  # a respawn jump is not a trail
            hist.append((np.array(w["pos"], np.float64), quat_mats([w["rot"]])[0]))
        if len(hist) < 2:
            return []
        # resample the path in time so a trail shorter than a recorded line still has its length
        tt = np.linspace(0.0, min(secs * self.fps, len(hist) - 1), 24)
        i0 = np.minimum(tt.astype(int), len(hist) - 2)
        f = (tt - i0)[:, None]
        out = []
        for e in emit:
            P = np.array([h[0] + h[1] @ np.array(e) for h in hist])
            p = P[i0] * (1 - f) + P[i0 + 1] * f
            seg = np.linalg.norm(np.diff(p, axis=0), axis=1)
            tot = seg.sum()
            if tot < 1e-4:
                continue
            u = np.concatenate([[0.0], np.cumsum(seg)]) / tot
            out.append((p, 0.5 * width(u), grad(u), soft))
        return out

    def __call__(self, session, frame):
        st = session.state[frame]
        out = {}
        V = st["vessels"]
        eye = np.array(st["camera"]["pos"])
        box = []
        if V:
            pos = np.array([v["pos"] for v in V], np.float64)
            R = quat_mats([v["rot"] for v in V])
            h = self.hull
            n = len(V)
            hc = np.tile(self.hull_col, (n, 1))
            out["dart"] = _rows(pos, R[:, :, 0] * h[0], R[:, :, 1] * h[1] * 2, R[:, :, 2] * h[2], hc, hc, 0.001)
            pc = np.tile(self.pod_col, (n, 1))
            for sx in (-1.0, 1.0):
                p = pos + R[:, :, 0] * sx * 1.8 + R[:, :, 2] * -0.6
                box.append(_rows(p, R[:, :, 0] * 0.5, R[:, :, 1] * 0.4, R[:, :, 2] * 1.6, pc, pc, 0.001))
            jc, tc = np.array(self.jet_col), np.array(self.tail_col)
            rib = []
            for v in V:
                # vfx_Projectile_02's trail: 0.5 s, 0.3 wide, alpha 1 -> 0; VesselTail.prefab: 4 s, 1 wide tapering
                # from 19 %, alpha 0 / 0.71 / 0 at 6.5 % / 16 % / 100 %
                rib += self._trail(session, frame, v, self.JETS, self.jet_secs, lambda u: np.full_like(u, self.jet_width), eye,
                                   lambda u: np.concatenate([np.tile(jc, (len(u), 1)), (1 - u)[:, None]], 1), soft=True)
                rib += self._trail(session, frame, v, self.TAILS, self.tail_secs,
                                   lambda u: self.tail_width * np.where(u < 0.1875, 1.0, (1 - u) / 0.8125), eye,
                                   lambda u: np.concatenate([np.tile(tc, (len(u), 1)), np.interp(u, [0, 0.0647, 0.1588, 1], [0, 0, 0.706, 0])[:, None]], 1))
            if rib:
                out["lines"] = rib
        C = [c for c in st.get("crystals", []) if not c.get("exploding")]
        if C:
            pos = np.array([c["pos"] for c in C], np.float64)
            s = np.array([c["scale"][0] for c in C])[:, None] / 1.7 * self.crystal
            I = np.eye(3)
            out["icosa"] = _rows(pos, I[0][None] * s, I[1][None] * s, I[2][None] * s,
                                 np.tile(self.crystal_bright, (len(C), 1)), np.tile(self.crystal_dark, (len(C), 1)), 2.0)
        box = [b for b in box if len(b)]
        if box:
            out["box"] = np.concatenate(box, 0)
        return out


class Layers:
    """Several instance builders as one (their per-mesh lists concatenated)."""

    def __init__(self, *fns):
        self.fns = fns

    def __call__(self, session, frame):
        out = {}
        for f in self.fns:
            for k, v in f(session, frame).items():
                out[k] = (out[k] + v if k == "lines" else np.concatenate([out[k], v], 0)) if k in out else v
        return out
