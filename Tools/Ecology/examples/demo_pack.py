"""A deliberately naive pack (template for the harness, NOT a result): agents chase the nearest pilot with
separation, raise `intent` as they close, bite inside 15 u. Run: python Tools/Ecology/examples/demo_pack.py"""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from common.arena import Arena, Pilot, Recorder
from common.scorecard import Probe, run_score, combine


class DemoPack:
    def __init__(self, arena, n=24, speed=100.0):
        c = arena._ball(1, 300, 700)[0]
        self.agent_pos = c + arena.rng.normal(0, 30, (n, 3)); self.agent_vel = np.zeros((n, 3))
        self.agent_size = np.full(n, 4.0); self.intent = np.zeros(n); self.speed = speed
        self.cool = np.zeros(n); self.kills = 0; self.alive = np.ones(n, bool)

    def step(self, arena, dt):
        P = self.agent_pos
        for i in range(len(P)):
            if not self.alive[i]: continue
            tgt = min(arena.pilots, key=lambda p: np.linalg.norm(p.pos - P[i]))
            d = tgt.pos - P[i]; dist = np.linalg.norm(d)
            sep = P[i] - P; sd = np.linalg.norm(sep, axis=1); m = (sd > 0) & (sd < 20)
            v = d / max(dist, 1e-6) + (sep[m] / sd[m, None] ** 2).sum(0) * 10
            self.agent_vel[i] = v / max(np.linalg.norm(v), 1e-6) * self.speed
            self.intent[i] = np.clip(1 - dist / 150, 0, 1)
            self.cool[i] -= dt
            if dist < 15 and self.cool[i] <= 0: arena.hit(tgt, "bite"); self.cool[i] = 2.0
            if tgt.policy == "hunter" and dist < tgt.radius + 4: self.alive[i] = False; self.kills += 1
        self.agent_pos = P + self.agent_vel * dt
        arena.targets = list(self.agent_pos[self.alive]); arena.threats = list(self.agent_pos[self.alive])

    def render(self, out):
        a = self.alive
        out["pack"] = dict(pos=self.agent_pos[a], col=np.tile([1.0, 0.3, 0.3], (a.sum(), 1)), size=self.agent_size[a])


if __name__ == "__main__":
    dt, minutes = 0.1, 1.0; runs = {}; rec_paths = []
    out = os.path.join(os.path.dirname(__file__), "out"); os.makedirs(out, exist_ok=True)
    for policy, mk in (("wander", Pilot.wanderer), ("evader", Pilot.evader), ("hunter", Pilot.hunter)):
        for seed in (7, 23):
            ar = Arena(seed=seed); ar.scatter_mass(1500); ar.add_pilot(mk())
            sp = DemoPack(ar); pr = Probe(dt); rec = Recorder(every=3)
            for _ in range(int(minutes * 60 / dt)):
                sp.step(ar, dt); ar.step(dt); pr.observe(ar, sp); rec.frame(ar, [sp])
            runs[(policy, seed)] = run_score(ar, sp, pr, minutes)
            if seed == 7:
                p = os.path.join(out, f"demo_{policy}.json"); rec.save(p, dict(label=f"demo pack vs {policy}", note="naive template pack")); rec_paths.append(p)
    print(json.dumps(combine(runs, minutes), indent=1))
    from common.viewer import build
    print(build(os.path.join(out, "demo.html"), rec_paths, "Demo pack"), "bytes")
