"""A recorded Amoebius session (targets/sessions/<name>) as a replay source for Renderer.render(replay=...).

    s = Session(path); s.frame(fi) -> {"t", "eye", "r", "u", "fwd", "fov", "inst"}
The camera's Unity quaternion (x, y, z, w) gives right/up/forward in Unity world axes (left-handed, +z forward); the
renderer only needs the three vectors. inst is filled by a builder (inst_fn(session, fi) -> instance lists), or empty.
"""
import json
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
            self.keys[d["frame"]] = np.array(d["prisms"], np.float64).reshape(-1, 13)
        self.kf = np.array(sorted(self.keys))
        self.palette = palette or PALETTE0
        self.power = power
        self.shield_mix = shield_mix

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
        sh = ((flags & 3) != 0)[:, None]  # shielded prisms read paler
        rim = np.where(sh, rim + (1.0 - rim / max(rim.max(), 1e-6)) * self.shield_mix * 0.5, rim)
        rows = np.concatenate([a[:, 1:4], R[:, :, 0] * S[:, 0:1], R[:, :, 1] * S[:, 1:2], R[:, :, 2] * S[:, 2:3], rim,
                               np.zeros((len(a), 1)), np.full((len(a), 1), -(200.0 + self.power)), face], 1)
        sup = (flags & 2) != 0
        if (~sup).any():
            out["box"] = rows[~sup].astype(np.float32)
        if sup.any():
            out["stella"] = rows[sup].astype(np.float32)
        return out
