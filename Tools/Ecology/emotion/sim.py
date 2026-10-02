"""Run a species in the shared arena against one viewer pilot and read its affect features."""
from __future__ import annotations

import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
from common.arena import Arena, Pilot, Recorder          # noqa: E402
from common.affect import AffectRecorder, FEATURES       # noqa: E402

VIEWERS = {
    # the player who stopped to look, the player cruising through, the player fleeing
    "hover": lambda: Pilot("wander", speed=25.0, turn=1.5, name="hover"),
    "cruise": lambda: Pilot("wander", speed=90.0, turn=1.8, name="cruise"),
    "evade": lambda: Pilot("evader", speed=110.0, turn=2.0, name="evade"),
}


def run(factory, seed: int, viewer: str = "hover", seconds: float = 30.0, dt: float = 0.1,
        record: bool = False, mass: int = 0, warmup: float = 2.0):
    """factory(rng, pilot) -> species with step(arena, dt), agent_pos/vel/size (+aspect/heading optional).
    Returns (features dict, recorder or None)."""
    ar = Arena(seed=seed)
    if mass:
        ar.scatter_mass(mass)
    p = ar.add_pilot(VIEWERS[viewer]())
    rng = np.random.default_rng(seed * 7919 + 13)
    sp = factory(rng, p)
    aff = AffectRecorder(dt, pilot_radius=p.radius)
    rec = Recorder(every=2) if record else None
    n = int(seconds / dt); w = int(warmup / dt)
    for k in range(n + w):
        ar.threats = list(np.asarray(sp.agent_pos))
        ar.targets = ar.threats
        sp.step(ar, dt)
        ar.step(dt)
        if k >= w:
            aff.observe(p.pos, p.vel, sp.agent_pos, sp.agent_vel, getattr(sp, "agent_size", None),
                        getattr(sp, "agent_aspect", None), getattr(sp, "agent_heading", None),
                        getattr(sp, "agent_body_id", None))
            if rec is not None:
                rec.frame(ar, [sp])
    return aff.features(), rec


def vec(feat: dict, keys=FEATURES) -> np.ndarray:
    return np.array([feat.get(k, 0.0) for k in keys], float)
