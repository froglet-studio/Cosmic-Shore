"""The shared threat + feel scorecard (Tools/Ecology/PROGRAM.md §2). numpy only.

A species exposes, each step, through `Probe.observe(arena, species)`:
    species.agent_pos  (n,3)  live agents' positions          (required)
    species.agent_vel  (n,3)  their velocities                (required)
    species.agent_size (n,)   body radius in world units      (optional, default 3)
    species.intent     (n,)   0..1 "about to strike" posture  (optional - the TELEGRAPH channel; e.g. a lurker's
                              gape, a pack's convergence, a spore pod's swelling)
    species.kills      int    agents killed by pilots so far  (optional)
    species.crystals   int    crystals dropped so far         (optional)
and calls `arena.hit(pilot, kind, amount)` when it harms a pilot.

Scores (each run is one species x one pilot policy x one seed; aggregate with `combine`):
    hits_per_min        pilot-harm events per minute
    telegraph_s         median seconds between the nearest agent's intent first exceeding 0.5 and a hit
                        (None if the species never telegraphs). >= ~0.7 s is readable at game speed.
    counterplay         hits/min against an EVADER over hits/min against a WANDERER (lower = flying well
                        matters; ~1 = unavoidable; >1 = fleeing is punished)
    payoff_per_min      kills (and crystals) per minute for a HUNTER pilot
    variety             across seeds: mean pairwise distance between hit-time histograms (0 = scripted repeat)
    feel                motion statistics an emotion probe reads (Direction C refines these):
        size            median body radius
        speed_rel       median agent speed / pilot speed
        approach        mean d(distance to nearest pilot)/dt, NEGATIVE = approaching, among agents within 300 u
        coherence       mean |mean unit velocity| over neighbourhoods (1 = moves as one)
        burstiness      p95 / median of per-step speed (sudden vs steady)
        jerk_rel        mean |third difference of position| / mean step length
"""
from __future__ import annotations

import numpy as np


class Probe:
    def __init__(self, dt: float):
        self.dt = dt
        self.speeds, self.sizes, self.approach, self.coh, self.jerk = [], [], [], [], []
        self.hist = []                 # last 4 positions (for jerk) - assumes stable agent indexing
        self.intent_on = {}            # pilot name -> time intent first crossed 0.5 near it
        self.leads = []
        self.n_hits_seen = 0
        self.pilot_speed = 1.0

    def observe(self, arena, sp):
        P = np.asarray(sp.agent_pos, float); V = np.asarray(sp.agent_vel, float)
        if len(P) == 0:
            return
        size = np.asarray(getattr(sp, "agent_size", np.full(len(P), 3.0)), float)
        self.sizes.append(float(np.median(size)))
        s = np.linalg.norm(V, axis=1); self.speeds.append(s)
        self.pilot_speed = np.mean([p.speed for p in arena.pilots]) if arena.pilots else 1.0
        # approach: radial velocity toward the nearest pilot, for agents within 300 u of it
        for p in arena.pilots:
            d = P - p.pos; dist = np.linalg.norm(d, axis=1); near = dist < 300.0
            if near.any():
                rel = V[near] - p.vel
                self.approach.append(float(np.mean(np.sum(rel * d[near], axis=1) / np.maximum(dist[near], 1e-6))))
        # coherence on up to 256 sampled agents vs their 12 nearest
        idx = np.arange(len(P)) if len(P) <= 256 else arena.rng.choice(len(P), 256, replace=False)
        U = V / np.maximum(s[:, None], 1e-6)
        if len(P) >= 4:
            D = np.linalg.norm(P[idx, None, :] - P[None, :, :], axis=2)
            nn = np.argsort(D, axis=1)[:, :min(12, len(P))]
            self.coh.append(float(np.mean(np.linalg.norm(U[nn].mean(axis=1), axis=1))))
        self.hist.append(P.copy()); self.hist = self.hist[-4:]
        if len(self.hist) == 4 and all(h.shape == P.shape for h in self.hist):
            j = self.hist[3] - 3 * self.hist[2] + 3 * self.hist[1] - self.hist[0]
            st = np.linalg.norm(self.hist[3] - self.hist[2], axis=1)
            self.jerk.append(float(np.linalg.norm(j, axis=1).mean() / max(st.mean(), 1e-6)))
        # telegraph: a hit is credited to an intent that was ALREADY showing before it (process hits first, so
        # an intent that first appears on the frame of the hit is not counted as a telegraph)
        for (t, name, kind, amt) in arena.log[self.n_hits_seen:]:
            if name in self.intent_on:
                self.leads.append(t - self.intent_on.pop(name))
        self.n_hits_seen = len(arena.log)
        it = getattr(sp, "intent", None)
        if it is not None and len(it):
            it = np.asarray(it, float)
            for p in arena.pilots:
                dist = np.linalg.norm(P - p.pos, axis=1); j = int(np.argmin(dist))
                if it[j] > 0.5 and p.name not in self.intent_on:
                    self.intent_on[p.name] = arena.t
                if it[j] < 0.2 and dist[j] > 200:
                    self.intent_on.pop(p.name, None)

    def feel(self):
        sp = np.concatenate(self.speeds) if self.speeds else np.zeros(1)
        med = float(np.median(sp)) if len(sp) else 0.0
        return dict(size=round(float(np.median(self.sizes)) if self.sizes else 0.0, 2),
                    speed_rel=round(med / max(self.pilot_speed, 1e-6), 3),
                    approach=round(float(np.mean(self.approach)) if self.approach else 0.0, 2),
                    coherence=round(float(np.mean(self.coh)) if self.coh else 0.0, 3),
                    burstiness=round(float(np.percentile(sp, 95) / max(med, 1e-6)), 2),
                    jerk_rel=round(float(np.mean(self.jerk)) if self.jerk else 0.0, 3))


def run_score(arena, sp, probe: Probe, minutes: float) -> dict:
    hits = len(arena.log)
    return dict(hits_per_min=round(hits / max(minutes, 1e-6), 2),
                telegraph_s=round(float(np.median(probe.leads)), 2) if probe.leads else None,
                kills_per_min=round(getattr(sp, "kills", 0) / max(minutes, 1e-6), 2),
                crystals=getattr(sp, "crystals", 0),
                hit_times=[round(t, 1) for (t, *_rest) in arena.log],
                feel=probe.feel())


def combine(runs: dict, minutes: float) -> dict:
    """runs: {(policy, seed): run_score dict}. Returns the species' scorecard."""
    def mean(policy, key):
        v = [r[key] for (p, s), r in runs.items() if p == policy and r[key] is not None]
        return float(np.mean(v)) if v else None
    w, e = mean("wander", "hits_per_min"), mean("evader", "hits_per_min")
    tel = [r["telegraph_s"] for r in runs.values() if r["telegraph_s"] is not None]
    # variety: hit-time histograms per seed (wanderer), mean pairwise L1 between normalised histograms
    hs = []
    for (p, s), r in runs.items():
        if p == "wander" and r["hit_times"]:
            h, _ = np.histogram(r["hit_times"], bins=12, range=(0, minutes * 60)); hs.append(h / max(h.sum(), 1))
    var = float(np.mean([np.abs(a - b).sum() for i, a in enumerate(hs) for b in hs[i + 1:]])) if len(hs) > 1 else 0.0
    feels = [r["feel"] for r in runs.values()]
    feel = {k: round(float(np.mean([f[k] for f in feels])), 3) for k in feels[0]} if feels else {}
    return dict(hits_per_min_wander=w, hits_per_min_evader=e,
                counterplay=round(e / w, 2) if w and e is not None and w > 0 else None,
                telegraph_s=round(float(np.median(tel)), 2) if tel else None,
                payoff_per_min=mean("hunter", "kills_per_min"),
                variety=round(var, 3), feel=feel)
