"""Substrate-side encounter metrics, complementing common/scorecard.py's feel stats (which mix the pilot's
own velocity into `approach`, so a pilot flying through and away reads as 'receding' for any species).

  heading_to_pilot  mean cos(agent heading, direction to nearest pilot) over agents within 300 u
                    (+1 = all converging, 0 = indifferent/orbiting, -1 = all fleeing)
  pursuit           mean d(distance)/dt from the AGENT's own velocity only (negative = closing in)
  near_frac         fraction of agents within 100 u of a pilot
  ring_sd           over agents within 300 u, sd of their distance to the pilot / mean (low = they hold a ring)
"""
import numpy as np


class Encounter:
    def __init__(self):
        self.h, self.p, self.nf, self.rs = [], [], [], []

    def observe(self, arena, sp):
        P = np.asarray(sp.agent_pos); V = np.asarray(sp.agent_vel)
        if len(P) == 0 or not arena.pilots:
            return
        for pl in arena.pilots:
            d = pl.pos - P; dist = np.linalg.norm(d, axis=1); m = dist < 300
            self.nf.append(float(np.mean(dist < 100)))
            if m.sum() >= 2:
                u = d[m] / np.maximum(dist[m, None], 1e-6)
                sv = np.linalg.norm(V[m], axis=1)
                self.h.append(float(np.mean(np.sum(V[m] * u, 1) / np.maximum(sv, 1e-6))))
                self.p.append(float(np.mean(-np.sum(V[m] * u, 1))))
                self.rs.append(float(np.std(dist[m]) / max(np.mean(dist[m]), 1e-6)))

    def summary(self):
        f = lambda a: round(float(np.mean(a)), 3) if a else None
        return dict(heading_to_pilot=f(self.h), pursuit=f(self.p), near_frac=f(self.nf), ring_sd=f(self.rs))
