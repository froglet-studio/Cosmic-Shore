"""Species 2 - WALL MENDERS / FORTRESS COLONY. A colony walls its core in with a closed shell of stolen prisms,
and when a vessel cuts through it, the wall closes back up.

Construction: the royal-chamber template of the nest weavers, at high closure (the shell the nest v0 built
everywhere - a negative for a nest, exactly what a fortress wants).

Mending is emergent, from two local cues and no repair order:
  * ALARM: a destroyed structure prism releases alarm pheromone at its site; alarm diffuses over the lattice and
    decays. A laden worker that smells alarm climbs its gradient instead of walking its usual bearing.
  * GAP RULE: an empty site in the template band that is surrounded by wall (many occupied 26-neighbours) is a
    hole; its deposit probability is boosted by the neighbour count (Theraulaz & Bonabeau's "fill where the
    configuration is nearly closed"). Holes close from the rim inward, which reads as the wound knitting.
Supply: menders steal whatever loose mass is near the breach - very often the cutting pilot's own trail.

Ablations (`mend`): "none" (no alarm, no gap boost - repair is ordinary building), "gap" (gap rule only),
"alarm" (alarm only), "both".
"""
from __future__ import annotations

import numpy as np

from builders.core import N26
from builders.nest import NestWeavers


class Fortress(NestWeavers):
    color = (0.95, 0.35, 0.45)
    note = ("Fortress colony: a shell of stolen prisms around its core. A vessel that cuts through leaves a wound; "
            "alarm + a gap-filling rule pull laden menders to it and the wall knits shut - often with the cutter's "
            "own trail.")

    def __init__(self, arena, seed=0, n=48, Rc=40.0, w=7.0, mend="both", gap_gain=2.5, alarm_gain=1.0, **kw):
        super().__init__(arena, seed=seed, n=n, Rc=Rc, w=w, k_cement=0.6, nucleate=0.02, name="fortress", **kw)
        self.mend, self.gap_gain, self.alarm_gain = mend, gap_gain, alarm_gain
        self.alarm = np.zeros_like(self.cement)
        self.cuts = []                 # (t, site) of every breach
        self.repairs = []              # (t, site) a breach site re-filled
        self.open_breach = set()
        self.repair_trail = 0          # breach sites re-filled with a pilot's trail prism

    def deposit_score(self, arena, site):
        p = super().deposit_score(arena, site)
        if p <= 0:
            return p
        if self.mend in ("gap", "both"):
            nb = self.lat.count(site)
            p *= 1.0 + self.gap_gain * max(0, nb - 4) / 6.0
        return min(1.0, p)

    def home(self, arena, k):
        if self.mend in ("alarm", "both"):
            site = self.lat.site_of(self.agent_pos[k])
            if self.lat.inside(site) and self.alarm[site] > 0.02:
                # climb the local alarm gradient (6 probes, one lattice step)
                best, bv = None, self.alarm[site]
                for o in N26[::3]:
                    q = (site[0] + o[0], site[1] + o[1], site[2] + o[2])
                    if self.lat.inside(q) and self.alarm[q] > bv:
                        best, bv = q, self.alarm[q]
                if best is not None:
                    return self.lat.pos(best)
                return self.lat.pos(site)
        return super().home(arena, k)

    def on_placed(self, arena, i, site):
        super().on_placed(arena, i, site)
        if site in self.open_breach:
            self.open_breach.discard(site); self.repairs.append((arena.t, site))
            self.repair_trail += int(arena.mass_trail[i])

    def behave(self, arena, dt):
        before = set(self.lat.sites.values())
        super().behave(arena, dt)                   # sweeps destroyed sites into self.breaches
        for (t, s) in self.breaches[len(self.cuts):]:
            self.cuts.append((t, s)); self.open_breach.add(s)
            if self.mend in ("alarm", "both"):
                self.alarm[s] += self.alarm_gain
        if self.tick % 3 == 0 and self.alarm.max() > 1e-3:
            a = self.alarm
            sm = np.zeros_like(a)
            sm[1:] += a[:-1]; sm[:-1] += a[1:]; sm[:, 1:] += a[:, :-1]; sm[:, :-1] += a[:, 1:]
            sm[:, :, 1:] += a[:, :, :-1]; sm[:, :, :-1] += a[:, :, 1:]
            self.alarm = (0.95 * (0.5 * a + 0.5 / 6 * sm)).astype(np.float32)

    def repair_stats(self, cut_t, frac=0.9):
        """Time from the first cut at/after cut_t until `frac` of the sites cut in that pass are re-filled
        (by ANY prism - the colony does not remember which prism was where)."""
        cut = [(t, s) for (t, s) in self.cuts if cut_t <= t < cut_t + 8.0]
        if not cut:
            return dict(cut_sites=0)
        sites = {s for _, s in cut}; t0 = min(t for t, _ in cut)
        filled = {}
        for t, s in self.repairs:
            if s in sites and s not in filled and t >= t0:
                filled[s] = t
        ts = sorted(filled.values())
        need = int(np.ceil(frac * len(sites)))
        return dict(cut_sites=len(sites), refilled=len(filled),
                    t50=round(ts[int(np.ceil(0.5 * len(sites))) - 1] - t0, 1) if len(ts) >= np.ceil(0.5 * len(sites)) else None,
                    t90=round(ts[need - 1] - t0, 1) if len(ts) >= need else None)

    def metrics(self, arena, minutes):
        m = super().metrics(arena, minutes)
        m["cuts"] = len(self.cuts); m["repaired"] = len(self.repairs)
        return m
