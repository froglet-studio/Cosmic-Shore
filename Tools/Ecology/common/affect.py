"""Affect features: what motion and size alone say to the pilot watching (Tools/Ecology/emotion/, Direction C).

numpy only. `AffectRecorder.observe(...)` once per step, `features()` at the end -> a flat dict of
bounded, mostly scale-free numbers. The PILOT is the viewer: every relational feature is measured from the
nearest pilot, because an emotion is a relation between a creature and someone looking at it.

Each feature is tied to a line of the literature (full table in emotion/LITERATURE.md):

  size_log        log2(body radius / pilot hull radius).  Kindchenschema: small = cute; Heider-Simmel's
                  "big triangle" read as the bully.
  extent_log      log2(radius of gyration of the engaged group / pilot hull) - a swarm of tiny things can
                  be one huge thing (the assembled-whale case).
  roundness       1 / body aspect (length/width).  Baby schema: round = cute.  Species may publish
                  `agent_aspect`; default 1.5 (unknown) and `roundness_known` = 0.
  count_log       log2(1 + agents engaged within ENGAGE of the pilot).
  speed_rel       median agent speed / pilot speed (Pollick 2001: speed ~ arousal).
  accel_rel       median |a| * T / pilot speed (Saerbeck & Bartneck 2010: acceleration ~ arousal).
  jerk_rel        |third difference| / step (Laban Flow: bound vs free; Pollick: jerk ~ arousal).
  curvature       median turn rate (rad/s) of moving agents (Saerbeck & Bartneck: curvature).
  approach        mean of the agent's OWN velocity toward the pilot / pilot speed, engaged agents
                  (approach/avoidance).  Uses the agent's velocity, not the relative one, so a pilot flying
                  into a parked rock does not read as the rock approaching.
  loom            sqrt of the p95 rate at which the agents FILL the pilot's view (fraction of the view sphere
                  per second, positive part).  Large AND fast-expanding - the looming stimulus (Schiff 1962;
                  mouse looming studies; PMC11126809 finds looming drives AROUSAL more than valence).
  tau_inv         p90 of d(log solid angle)/dt (1/s): Lee's tau, time-to-contact inverse, size-free.
  view_fill       p90 of the fraction of the pilot's view sphere the agents cover (log-ish, 0..1).
  proximity       p50 of log2(nearest distance / pilot radius), negated and squashed to 0..1 (1 = touching).
  gaze            mean cos(heading, direction to pilot) over engaged agents.  Wolfpack effect (Gao,
                  McCarthy & Scholl 2010): things that FACE you read as pursuing you even when they don't.
                  Heading = `agent_heading` if published, else the velocity direction (held through stops).
  pursuit         fraction of engaged agent-time that is heat-seeking: the agent's VELOCITY within ~32 deg of
                  the pilot and moving (Gao, Newman & Scholl 2009, chasing subtlety: under ~30 deg off
                  heat-seeking reads as a chase; by 120 deg it is not seen at all).
  orbit           tangential / total speed for engaged agents (circling).
  encircle        directional coverage of the pilot by agents within CLOSE: (1 - |mean unit vector|) x
                  min(1, n/6).  1 = surrounded.
  converge        -d(mean engaged distance)/dt / pilot speed, positive = closing as a group.
  coherence       mean |mean unit velocity| over 12-nearest neighbourhoods (1 = one mind).
  synchrony       mean pairwise correlation of agents' speed FLUCTUATIONS (too-perfect coordination; the
                  uncanny reading of robotic/regular motion).
  regularity      1 - (cross-agent std of normalised speed / mean) ... high = identical gait across members.
  stillness       fraction of agent-time with speed < 8% of pilot speed.
  burst           still-then-sudden events per agent-minute: >= 0.5 s below the still threshold, then
                  above 50% pilot speed within 0.6 s (predator freeze-then-strike; Tremoulet & Feldman:
                  speed change = animacy).  Squashed by log1p.
  bounce          oscillatory power fraction x periodicity: share of velocity variance in the 0.4..4 Hz
                  band (deviation from a 2 s moving mean), times the peak autocorrelation in that band.
                  ("light, bouncy" = happy/cute).
  wobble_hz       dominant frequency of that oscillation (Hz) - a slow undulation and a quick hop differ.
  approach_retreat sign changes per minute of the approach velocity while within ENGAGE (curious
                  puppy / playful tag), log1p.
  unpredict       1 s constant-velocity prediction error / 1 s path length (Laban Space: indirect; animacy).
  mimicry         max over lags 0..1 s of the correlation between agent velocity and PILOT velocity
                  (an echo that copies you is eerie).
  speed_cv        coefficient of variation of each agent's speed over time (steady vs changeable).

Temporal features use stable agent indexing over up to MAX_TRACK agents; when the agent count changes the
track restarts (births/deaths), so a species that churns every step loses only those features.
"""
from __future__ import annotations

import math

import numpy as np

ENGAGE = 400.0          # u: the bubble inside which a creature is "in the pilot's encounter"
CLOSE = 250.0           # u: the bubble for encirclement
MAX_TRACK = 48
SPEED_REF = 100.0       # u/s: the yardstick for every speed feature (a vessel's typical cruise). Fixed, not the
                        # viewer's own speed, so a creature reads the same to a hovering and a cruising pilot.
FEATURES = ("size_log", "extent_log", "roundness", "count_log", "speed_rel", "accel_rel", "jerk_rel",
            "curvature", "approach", "loom", "tau_inv", "view_fill", "proximity", "gaze", "pursuit", "orbit",
            "encircle", "converge", "coherence", "synchrony", "regularity", "stillness", "burst",
            "bounce", "wobble_hz", "approach_retreat", "unpredict", "mimicry", "speed_cv")


def _unit(v):
    n = np.linalg.norm(v, axis=-1, keepdims=True)
    return v / np.maximum(n, 1e-9), n[..., 0]


class AffectRecorder:
    def __init__(self, dt: float, pilot_radius: float = 6.0):
        self.dt, self.pr = dt, pilot_radius
        self.rows = {k: [] for k in ("approach", "loom", "fill", "prox", "gaze", "orbit", "encircle",
                                     "meand", "coh", "size", "extent", "count", "aspect", "tau", "pursue")}
        self.track_P, self.track_H, self.pilot_V = [], [], []
        self.track_n = None
        self.segments = []            # finished (P (T,n,3), pilot_V (T,3)) tracks
        self.pilot_speed = []
        self.heading = None
        self.prev_logomega = None
        self.aspect_known = False

    # ------------------------------------------------------------------------------------------------
    def observe(self, pilot_pos, pilot_vel, P, V, size=None, aspect=None, heading=None):
        """One step. pilot_*: (3,) the VIEWER. P, V: (n,3). size: (n,) body radius. aspect: (n,) or scalar
        length/width. heading: (n,3) facing (optional)."""
        P = np.asarray(P, float).reshape(-1, 3); V = np.asarray(V, float).reshape(-1, 3)
        n = len(P)
        pp = np.asarray(pilot_pos, float); pv = np.asarray(pilot_vel, float)
        self.pilot_speed.append(float(np.linalg.norm(pv)))
        if n == 0:
            self._cut(); return
        size = np.full(n, 3.0) if size is None else np.broadcast_to(np.asarray(size, float), (n,))
        if aspect is not None:
            self.aspect_known = True
            self.rows["aspect"].append(float(np.median(np.broadcast_to(np.asarray(aspect, float), (n,)))))
        # heading: published, else velocity direction held through stops
        sp = np.linalg.norm(V, axis=1)
        if heading is not None:
            H = _unit(np.asarray(heading, float).reshape(-1, 3))[0]
        else:
            if self.heading is None or len(self.heading) != n:
                self.heading = np.tile([0.0, 0.0, 1.0], (n, 1))
            mv = sp > 1e-3
            self.heading[mv] = V[mv] / sp[mv, None]
            H = self.heading.copy()
        d = pp - P; dist = np.linalg.norm(d, axis=1); dh = d / np.maximum(dist[:, None], 1e-9)
        eng = dist < ENGAGE
        self.rows["size"].append(float(np.median(size)))
        self.rows["prox"].append(float(dist.min()))
        if eng.any():
            Ve, de, dhe = V[eng], dist[eng], dh[eng]
            self.rows["count"].append(int(eng.sum()))
            self.rows["approach"].append(float(np.mean(np.sum(Ve * dhe, axis=1))))
            g = np.sum(H[eng] * dhe, axis=1)
            self.rows["gaze"].append(float(np.mean(g)))
            ve_dir = np.sum(Ve * dhe, axis=1) / np.maximum(np.linalg.norm(Ve, axis=1), 1e-9)
            self.rows["pursue"].append(float(np.mean((ve_dir > 0.85) & (np.linalg.norm(Ve, axis=1) > 0.08 * SPEED_REF))))
            ve_n = np.linalg.norm(Ve, axis=1)
            radial = np.sum(Ve * dhe, axis=1)
            tang = np.sqrt(np.maximum(ve_n ** 2 - radial ** 2, 0))
            self.rows["orbit"].append(float(np.sum(tang) / max(np.sum(ve_n), 1e-9)))
            self.rows["meand"].append(float(de.mean()))
            Pe = P[eng]
            self.rows["extent"].append(float(np.sqrt(np.mean(np.sum((Pe - Pe.mean(0)) ** 2, axis=1))) + np.median(size[eng])))
            close = dist < CLOSE
            if close.sum() >= 2:
                R = np.linalg.norm(dh[close].mean(0))
                self.rows["encircle"].append(float((1 - R) * min(1.0, close.sum() / 6)))
            else:
                self.rows["encircle"].append(0.0)
        else:
            for k in ("approach", "gaze", "orbit", "encircle", "pursue"):
                self.rows[k].append(np.nan)
            self.rows["meand"].append(np.nan); self.rows["count"].append(0)
            self.rows["extent"].append(float(np.median(size)))
        # optical: solid angle of all agents (small-angle discs), capped at the full sphere
        ang = np.arctan2(size, np.maximum(dist, 1e-6))
        omega = float(np.sum(2 * math.pi * (1 - np.cos(ang))))
        self.rows["fill"].append(min(1.0, omega / (4 * math.pi)))
        lo = math.log(max(omega, 1e-9))
        if self.prev_logomega is not None:
            self.rows["tau"].append((lo - self.prev_logomega[0]) / self.dt)
            self.rows["loom"].append((omega - self.prev_logomega[1]) / (4 * math.pi) / self.dt)
        self.prev_logomega = (lo, omega)
        # coherence
        if n >= 4:
            idx = np.arange(n) if n <= 128 else np.linspace(0, n - 1, 128).astype(int)
            U = V / np.maximum(sp[:, None], 1e-9)
            D = np.linalg.norm(P[idx, None, :] - P[None, :, :], axis=2)
            nn = np.argsort(D, axis=1)[:, :min(12, n)]
            self.rows["coh"].append(float(np.mean(np.linalg.norm(U[nn].mean(axis=1), axis=1))))
        # tracks
        k = min(n, MAX_TRACK)
        if self.track_n != n:
            self._cut(); self.track_n = n
        sel = np.linspace(0, n - 1, k).astype(int)
        self.track_P.append(P[sel].copy()); self.track_H.append(H[sel].copy()); self.pilot_V.append(pv.copy())

    def _cut(self):
        if len(self.track_P) >= 8:
            self.segments.append((np.array(self.track_P), np.array(self.pilot_V)))
        self.track_P, self.track_H, self.pilot_V = [], [], []
        self.track_n = None

    # ------------------------------------------------------------------------------------------------
    def features(self) -> dict:
        self._cut()
        dt = self.dt
        ps = SPEED_REF
        r = {k: np.asarray(v, float) for k, v in self.rows.items()}
        nm = lambda a, f=np.nanmean, d=0.0: float(f(a)) if len(a) and np.isfinite(a).any() else d
        f = {}
        f["size_log"] = math.log2(max(nm(r["size"], np.nanmedian, 3.0), 0.1) / self.pr)
        f["extent_log"] = math.log2(max(nm(r["extent"], np.nanmedian, 3.0), 0.1) / self.pr)
        f["roundness"] = 1.0 / max(nm(r["aspect"], np.nanmedian, 1.5), 1.0)
        f["count_log"] = math.log2(1 + nm(r["count"], np.nanmean, 0.0))
        f["approach"] = nm(r["approach"]) / ps
        loom = r["loom"]; loom = loom[np.isfinite(loom)]
        f["loom"] = math.sqrt(float(np.percentile(np.maximum(loom, 0), 95))) if len(loom) else 0.0
        tau = r["tau"]; tau = tau[np.isfinite(tau)]
        f["tau_inv"] = float(np.percentile(np.maximum(tau, 0), 90)) if len(tau) else 0.0
        f["view_fill"] = float(np.percentile(r["fill"], 90)) ** 0.25 if len(r["fill"]) else 0.0
        if len(r["prox"]):
            q = math.log2(max(float(np.percentile(r["prox"], 20)), 1.0) / self.pr)
            f["proximity"] = 1.0 / (1.0 + math.exp(q - 4.0))       # ~0.5 at 16 hulls (~100 u)
        else:
            f["proximity"] = 0.0
        f["gaze"] = nm(r["gaze"])
        f["pursuit"] = nm(r["pursue"])
        f["orbit"] = nm(r["orbit"])
        f["encircle"] = nm(r["encircle"])
        md = r["meand"]
        if np.isfinite(md).sum() > 3:
            dd = np.diff(md) / dt; dd = dd[np.isfinite(dd)]
            f["converge"] = float(-np.mean(np.clip(dd, -3 * ps, 3 * ps))) / ps if len(dd) else 0.0
        else:
            f["converge"] = 0.0
        f["coherence"] = nm(r["coh"], d=0.0)
        f.update(self._temporal(ps))
        f["roundness_known"] = 1.0 if self.aspect_known else 0.0
        return {k: round(float(v), 4) for k, v in f.items()}

    def _temporal(self, ps):
        dt = self.dt
        keys = ("speed_rel", "accel_rel", "jerk_rel", "curvature", "synchrony", "regularity", "stillness",
                "burst", "bounce", "wobble_hz", "approach_retreat", "unpredict", "mimicry", "speed_cv")
        acc = {k: [] for k in keys}; wts = []
        for P, PV in self.segments:
            T, n, _ = P.shape
            V = np.diff(P, axis=0) / dt                        # (T-1,n,3)
            s = np.linalg.norm(V, axis=2)                      # (T-1,n)
            A = np.diff(V, axis=0) / dt
            a = np.linalg.norm(A, axis=2)
            acc["speed_rel"].append(np.median(s) / ps)
            acc["accel_rel"].append(np.median(a) * 1.0 / ps)
            if T >= 5:
                J = P[3:] - 3 * P[2:-1] + 3 * P[1:-2] - P[:-3]
                acc["jerk_rel"].append(np.mean(np.linalg.norm(J, axis=2)) / max(np.mean(s) * dt, 1e-6))
            else:
                acc["jerk_rel"].append(0.0)
            U = V / np.maximum(s[..., None], 1e-9)
            cosang = np.clip(np.sum(U[1:] * U[:-1], axis=2), -1, 1)
            mv = (s[1:] > 0.08 * ps) & (s[:-1] > 0.08 * ps)
            tr = np.arccos(cosang) / dt
            acc["curvature"].append(float(np.median(tr[mv])) if mv.any() else 0.0)
            still = s < 0.08 * ps
            acc["stillness"].append(still.mean())
            # still-then-burst events
            need = int(round(0.5 / dt)); win = int(round(0.6 / dt)); ev = 0
            for j in range(n):
                run = 0
                for t in range(len(s)):
                    if still[t, j]:
                        run += 1
                    else:
                        if run >= need and s[t:t + win, j].max(initial=0) > 0.5 * ps:
                            ev += 1
                        run = 0
            minutes = len(s) * dt / 60.0
            acc["burst"].append(math.log1p(ev / max(n * minutes, 1e-6)))
            # oscillation: deviation of V from a 2 s moving mean
            m = max(3, int(round(2.0 / dt)))
            if len(V) > 2 * m:
                ker = np.ones(m) / m
                Vm = np.apply_along_axis(lambda x: np.convolve(x, ker, mode="same"), 0, V)
                D = (V - Vm)[m:-m]
                tot = np.sum((V[m:-m] - V[m:-m].mean(0)) ** 2) + 1e-9
                frac = float(np.sum(D ** 2) / tot)
                # autocorrelation over lags in 0.25..2.5 s, averaged over agents & axes
                x = D - D.mean(0)
                denom = np.sum(x * x, axis=0) + 1e-9            # (n,3)
                best, best_l = 0.0, 0
                for lag in range(max(1, int(0.25 / dt)), min(len(x) - 1, int(2.5 / dt)) + 1):
                    ac = np.sum(x[lag:] * x[:-lag], axis=0) / denom
                    w = denom / denom.sum()
                    c = float(np.sum(ac * w))
                    if c > best:
                        best, best_l = c, lag
                acc["bounce"].append(min(frac, 1.0) * best)
                acc["wobble_hz"].append(1.0 / (best_l * dt) if best_l and best > 0.2 else 0.0)
                # synchrony: correlation of speed fluctuations across agents
                sd = s[m:-m] - np.apply_along_axis(lambda x: np.convolve(x, ker, mode="same"), 0, s)[m:-m]
                if n >= 2 and sd.std(0).min() > 1e-6:
                    C = np.corrcoef(sd.T); iu = np.triu_indices(n, 1)
                    acc["synchrony"].append(float(np.mean(C[iu])))
                else:
                    acc["synchrony"].append(1.0 if n >= 2 and s.std() < 1e-6 and s.mean() > 1e-3 else 0.0)
            else:
                acc["bounce"].append(0.0); acc["wobble_hz"].append(0.0); acc["synchrony"].append(0.0)
            # regularity: members move at the same normalised speed profile
            if n >= 2:
                spread = s.std(axis=1).mean() / max(s.mean(), 1e-6)
                acc["regularity"].append(1.0 / (1.0 + 4 * spread))
            else:
                acc["regularity"].append(0.0)
            acc["speed_cv"].append(float(np.mean(s.std(0) / np.maximum(s.mean(0), 1e-6))))
            # unpredictability: 1 s constant-velocity prediction
            L = int(round(1.0 / dt))
            if T > L + 2:
                pred = P[1:-L] + V[:-L] * (L * 1.0) * dt * 1.0
                err = np.linalg.norm(P[1 + L:] - pred, axis=2)
                path = np.array([np.sum(s[t:t + L], axis=0) * dt for t in range(len(s) - L)])
                acc["unpredict"].append(float(np.median(err[:len(path)] / np.maximum(path, 0.05 * ps))))
            else:
                acc["unpredict"].append(0.0)
            acc["approach_retreat"].append(0.0)        # computed from the observe-time rows instead
            # mimicry: correlation of agent velocity with pilot velocity, lags 0..1 s
            pv = PV[1:len(V) + 1]
            best = 0.0
            pvc = pv - pv.mean(0)
            for lag in range(0, min(int(1.0 / dt), len(V) - 2) + 1):
                Va = V[lag:] - V[lag:].mean(0); Pa = pvc[:len(Va)]
                num = np.sum(Va * Pa[:, None, :], axis=(0, 2))
                den = np.sqrt(np.sum(Va ** 2, axis=(0, 2)) * np.sum(Pa ** 2)) + 1e-9
                best = max(best, float(np.mean(num / den)))
            acc["mimicry"].append(best)
            wts.append(T * n)
        out = {}
        w = np.asarray(wts, float)
        for k in keys:
            v = np.asarray(acc[k], float)
            out[k] = float(np.sum(v * w) / w.sum()) if len(v) and w.sum() > 0 else 0.0
        out["approach_retreat"] = self._approach_retreat()
        return out

    def _approach_retreat(self):
        a = np.asarray(self.rows["approach"], float)
        ok = np.isfinite(a)
        if ok.sum() < 10:
            return 0.0
        x = a[ok]
        thr = 0.15 * max(np.std(x), 1e-6) + 1e-3
        sgn = np.sign(np.where(np.abs(x) > thr, x, 0))
        sgn = sgn[sgn != 0]
        flips = int(np.sum(sgn[1:] != sgn[:-1])) if len(sgn) > 1 else 0
        minutes = ok.sum() * self.dt / 60.0
        return math.log1p(flips / max(minutes, 1e-6))
