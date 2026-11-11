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
