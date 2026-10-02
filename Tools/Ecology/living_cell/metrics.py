"""What a living cell is scored on: the ECOSYSTEM (does the whole persist, stay diverse, breathe without
freezing, conserve mass) and the PLAYER EXPERIENCE (what a 5-minute flight through it meets and feels).

Every metric here has a negative control in run.py `controls` that must FIRE (a planted failure the metric is
required to report), because a metric nobody has watched fail is a metric nobody should trust.
"""
from __future__ import annotations

import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ECO = os.path.dirname(HERE)
for p in (os.path.join(ECO, "common"), os.path.join(ECO, "emotion")):
    if p not in sys.path:
        sys.path.insert(0, p)
from affect import AffectRecorder          # noqa: E402  (Direction C's feature extractor)
from probe import EmotionProbe              # noqa: E402  (Direction C's frozen probe, results/probe.json)

from .cell import THREAT, ALL               # noqa: E402

LIVING = ("flora", "grazer", "locust", "pack", "thief", "lurker", "snaptrap", "fortress", "physarum")
ENC_R, CLEAR_R, QUIET_R, DEBOUNCE = 200.0, 400.0, 300.0, 10.0
_PROBE = None


def probe():
    global _PROBE
    if _PROBE is None:
        _PROBE = EmotionProbe.load()
    return _PROBE


# ==========================================================================================================
class EcoRecorder:
    """Samples the cell every `every` s: census, biomass, nutrient, prisms, audit."""

    def __init__(self, every=5.0):
        self.every = every; self.next = 0.0
        self.rows = []

    def update(self, cell):
        w = cell.w
        if w.t + 1e-9 < self.next:
            return
        self.next += self.every
        b = cell.biomass()
        self.rows.append(dict(t=round(w.t, 1), census=cell.census(), bio={k: round(v, 1) for k, v in b.items()},
                              N=round(w.N, 1), prisms=int(w.alive[:w.n].sum()), audit=float(w.audit()),
                              hits=len(w.events), crystals=w.crystals))


def shannon(shares):
    p = np.asarray(shares, float); p = p[p > 0]; p = p / p.sum() if p.sum() > 0 else p
    return float(-(p * np.log(p)).sum()) if len(p) else 0.0


def eco_metrics(rows, cell, burn=300.0):
    """The ecosystem scorecard over the rows after `burn` seconds."""
    R = [r for r in rows if r["t"] >= burn] or rows
    names = [n for n in LIVING if n in R[0]["bio"]]
    B = np.array([[r["bio"].get(n, 0.0) for n in names] for r in R])
    H = np.array([shannon(b) for b in B])
    C = {}
    for n in ("grazer", "locust", "pack", "thief", "lurker", "fortress", "snaptrap", "physarum"):
        if n in R[0]["census"]:
            C[n] = np.array([r["census"][n] for r in R], float)
    # persistence: extinction (count 0; for the physarum, no reserve and no tubes) and recovery, WHOLE run
    ext = {}
    for n in C:
        key = "physarum_alive" if n == "physarum" else n
        full = np.array([r["census"].get(key, 0) for r in rows], float)
        z = full <= 0
        if z.any():
            first = int(np.argmax(z)); rec = bool((~z[first:]).any() and z[first:].any() and (full[first:] > 0).any())
            ext[n] = dict(t_extinct=rows[first]["t"], recovered=rec)
    # oscillation without freeze: per-species CV, flora relative swing, turning points of the totals,
    # and FREEZE = fraction of 1-min windows in which nothing moved (every count and the flora changed < 1%)
    cv = {n: round(float(c.std() / max(c.mean(), 1e-9)), 3) for n, c in C.items()}
    flo = B[:, names.index("flora")]
    step = max(1, int(round(60.0 / (R[1]["t"] - R[0]["t"])))) if len(R) > 1 else 1
    frozen = []
    for a in range(0, len(R) - step, step):
        ch = [abs(c[a + step] - c[a]) / max(c[a], 1.0) for c in C.values()]
        ch.append(abs(flo[a + step] - flo[a]) / max(flo[a], 1.0))
        frozen.append(max(ch) < 0.01)
    freeze = float(np.mean(frozen)) if frozen else 1.0
    cap = cell.cfg["n_plants"] * cell.cfg["plant_cap"] * 8.0
    flora_sat = float(np.mean(flo >= 0.95 * cap))
    # breathing: the flora and the grazer+locust totals' number of 10%-reversals (peaks and troughs)
    def reversals(x, frac=0.1):
        if len(x) < 3:
            return 0
        n, last, d = 0, x[0], 0
        for v in x[1:]:
            if d >= 0 and v < last * (1 - frac) and v < last:
                if d > 0: n += 1
                d = -1; last = v
            elif d <= 0 and v > last * (1 + frac):
                if d < 0: n += 1
                d = 1; last = v
            else:
                last = max(last, v) if d >= 0 else min(last, v)
        return n
    herb = C.get("grazer", 0) + C.get("locust", 0)
    audit = max(abs(r["audit"]) for r in rows)
    return dict(
        shannon_mean=round(float(H.mean()), 3), shannon_min=round(float(H.min()), 3),
        shannon_max_possible=round(math.log(len(names)), 3),
        persistence=dict(extinct=ext, n_extinct=len(ext), n_unrecovered=sum(1 for e in ext.values() if not e["recovered"])),
        cv=cv, freeze_frac=round(freeze, 3), flora_saturated_frac=round(flora_sat, 3),
        reversals=dict(flora=reversals(flo), herbivores=reversals(np.asarray(herb, float)) if len(R) else 0,
                       pack=reversals(C["pack"]) if "pack" in C else 0),
        audit_max=float(audit), shield_eaten=int(cell.w.shield_eaten),
        final_census=R[-1]["census"], final_bio=R[-1]["bio"],
        biomass_share_mean={n: round(float(B[:, i].mean() / B.sum(1).mean()), 3) for i, n in enumerate(names)},
    )


def continuity_metrics(world, pop_r=250.0):
    """LOD continuity: nothing may appear or vanish where a pilot can see (closer than pop_r)."""
    c = world.continuity
    pops = [x for x in c if x[3] < pop_r]
    return dict(events=len(c), pop_ins=sum(1 for x in pops if x[2] == "expand"),
                pop_outs=sum(1 for x in pops if x[2] == "absorb"),
                min_dist=round(min((x[3] for x in c), default=float("inf")), 1))


# ==========================================================================================================
class Flight:
    """One pilot's flight through the cell: encounters, quiet, hits, and the frames the emotion probe reads.

    ENCOUNTER: a THREAT species (anything that can strike a pilot) comes within 200 u, having not been within
    400 u in the previous 10 s. It is `active` if any of its members in range was in its striking state
    (gregarious locust, closing pack, a thief on your wake, a gaping lurker, a primed trap, an excited tube,
    a defending fortress) while the encounter lasted. Grazers are logged as SIGHTINGS, never encounters.
    QUIET: nothing in its striking state within 300 u (a sparse cute locust cloud or a sleeping lurker is
    life, not a threat; a pack is always a threat)."""

    K = 24

    def __init__(self, cell, pilot=0, t0=None, seconds=300.0):
        self.c = cell; self.k = pilot
        self.t0 = cell.w.t if t0 is None else t0; self.t1 = self.t0 + seconds
        self.enc = []; self.cur = {}; self.last_close = {}
        self.quiet = []; self.frames = []; self.slots = [None] * self.K
        self.sight = {}

    def done(self):
        return self.c.w.t >= self.t1

    def update(self):
        c = self.c; w = c.w
        if w.t < self.t0 or w.t > self.t1:
            return
        p = w.pilots[self.k]
        sets = c.threat_sets()
        anyq = False
        for sp, (P, act) in sets.items():
            if len(P) == 0:
                continue
            d = np.linalg.norm(P - p.pos, axis=1)
            dmin = float(d.min())
            if sp == "grazer":
                if dmin < ENC_R:
                    self.sight[sp] = self.sight.get(sp, 0) + 1
                continue
            if len(act) and np.any(act & (d < QUIET_R)):
                anyq = True
            a_in = bool(np.any(act[d < ENC_R])) if len(act) else False
            if sp in self.cur:
                self.cur[sp]["active"] |= a_in
                if dmin > CLEAR_R:
                    self.cur.pop(sp)
            elif dmin < ENC_R and w.t - self.last_close.get(sp, -1e9) > DEBOUNCE:
                e = dict(t=round(w.t - self.t0, 1), species=sp, active=a_in)
                self.enc.append(e); self.cur[sp] = e
            if dmin < CLEAR_R:
                self.last_close[sp] = w.t
        self.quiet.append(not anyq)
        # emotion frames: sticky nearest K bodies (a slot keeps its body while it lives and stays inside 700 u)
        keys, P, V, S, A = c.affect_agents()
        if len(keys):
            index = {k: i for i, k in enumerate(keys)}
            d = np.linalg.norm(P - p.pos, axis=1)
            used = set()
            for s in range(self.K):
                k = self.slots[s]
                if k is None or k not in index or d[index[k]] > 700:
                    self.slots[s] = None
                else:
                    used.add(k)
            order = np.argsort(d)
            it = iter(order)
            for s in range(self.K):
                if self.slots[s] is None:
                    for j in it:
                        if keys[j] not in used:
                            self.slots[s] = keys[j]; used.add(keys[j]); break
            sel = [index[k] for k in self.slots if k is not None]
            self.frames.append((p.pos.copy(), p.vel.copy(), P[sel].copy(), V[sel].copy(), S[sel].copy(), A[sel].copy()))
        else:
            self.frames.append((p.pos.copy(), p.vel.copy(), np.zeros((0, 3)), np.zeros((0, 3)), np.zeros(0), np.zeros(0)))

    def emotion(self, dt=0.1, window=8.0, stride=2.0):
        """C's timeline (emotion/timeline.py): the frozen probe on 8 s windows every 2 s."""
        pr = probe(); n = len(self.frames); W = int(window / dt); St = int(stride / dt)
        out = []
        for a in range(0, max(1, n - W + 1), St):
            rec = AffectRecorder(dt, pilot_radius=6.0)
            for pp, pv, P, V, S, A in self.frames[a:a + W]:
                rec.observe(pp, pv, P, V, size=S if len(S) else None, aspect=A if len(A) else None)
            sc = pr.score(rec.features())
            out.append(dict(t=round(a * dt, 1), top=sc["top"],
                            threat=round(sc["p"].get("menacing", 0) + sc["p"].get("terrifying", 0), 3)))
        return out

    def summary(self, emo=True):
        c = self.c; w = c.w
        mins = (min(w.t, self.t1) - self.t0) / 60.0
        species = [e["species"] for e in self.enc]
        cnt = {s: species.count(s) for s in sorted(set(species))}
        allh = [e for e in w.events if e[1] == self.k and self.t0 <= e[0] <= self.t1]
        hits = [e for e in allh if e[3] != "steal"]
        steals = [e for e in allh if e[3] == "steal"]
        hit_by = {}
        for e in allh:
            if e[3] == "steal":
                continue
            hit_by[e[2]] = hit_by.get(e[2], 0) + 1
        out = dict(minutes=round(mins, 2), encounters=len(self.enc), enc_per_min=round(len(self.enc) / max(mins, 1e-9), 2),
                   active_encounters=sum(e["active"] for e in self.enc),
                   variety=len(cnt), variety_entropy_bits=round(shannon(list(cnt.values())) / math.log(2), 3) if cnt else 0.0,
                   enc_by_species=cnt, quiet_frac=round(float(np.mean(self.quiet)), 3) if self.quiet else 1.0,
                   hits_per_min=round(len(hits) / max(mins, 1e-9), 2), hits_by_species=hit_by,
                   steals_per_min=round(len(steals) / max(mins, 1e-9), 2),
                   grazer_sight_s=round(self.sight.get("grazer", 0) * 0.1, 1),
                   seq=[(e["species"], int(e["t"] // 60)) for e in self.enc], events=self.enc)
        if emo and self.frames:
            ws = self.emotion()
            tops = [x["top"] for x in ws]
            emo_set = {t for t in tops if t != "neutral"}
            hist = {t: tops.count(t) for t in sorted(set(tops))}
            th = [x["threat"] for x in ws]
            out.update(emotion_windows=len(ws), emotion_distinct=len(emo_set), emotion_hist=hist,
                       emotion_entropy_bits=round(shannon(list(hist.values())) / math.log(2), 3),
                       threat_range=round(max(th) - min(th), 3) if th else 0.0, threat_peak=round(max(th), 3) if th else 0.0,
                       emotion_series=ws)
        return out


def replay_distance(a, b):
    """How different two flights were: 1 - Jaccard of their (species, minute) encounter sets, and the total-
    variation distance of their species histograms. 0 = the same flight twice."""
    A, B = set(map(tuple, a["seq"])), set(map(tuple, b["seq"]))
    jac = 1.0 - (len(A & B) / len(A | B) if (A | B) else 1.0)
    keys = set(a["enc_by_species"]) | set(b["enc_by_species"])
    pa = np.array([a["enc_by_species"].get(k, 0) for k in keys], float); pb = np.array([b["enc_by_species"].get(k, 0) for k in keys], float)
    tv = 0.5 * np.abs(pa / max(pa.sum(), 1) - pb / max(pb.sum(), 1)).sum() if keys else 0.0
    ea = a.get("emotion_hist", {}); eb = b.get("emotion_hist", {})
    ek = set(ea) | set(eb)
    qa = np.array([ea.get(k, 0) for k in ek], float); qb = np.array([eb.get(k, 0) for k in ek], float)
    etv = 0.5 * np.abs(qa / max(qa.sum(), 1) - qb / max(qb.sum(), 1)).sum() if ek else 0.0
    return dict(jaccard_dist=round(jac, 3), species_tv=round(float(tv), 3), emotion_tv=round(float(etv), 3))
