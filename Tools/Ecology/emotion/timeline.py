"""Emotion over TIME: score sliding windows of an encounter, not one 30-40 s average.

An ambusher's emotion is one rare snap; a time-average dilutes it into background. `timeline` records an
encounter once, then reads the probe on WINDOW-second windows every STRIDE seconds and reports the series,
the mean, and the PEAK of each emotion (and of threat = menacing + terrifying).

Caveat: the probe was trained on 30 s windows; `check()` measures how much 8 s windows cost on the archetype
reference families before any timeline is believed.

    python timeline.py check
    python timeline.py species
"""
from __future__ import annotations

import json
import os
import sys

import numpy as np

from probe import EmotionProbe
from sim import VIEWERS, Arena, AffectRecorder, HERE

WINDOW, STRIDE, DT = 8.0, 2.0, 0.1


def record(factory, seed, viewer, seconds=40.0, warm=2.0):
    ar = Arena(seed=seed); p = ar.add_pilot(VIEWERS[viewer]())
    sp = factory(np.random.default_rng(seed * 7919 + 13), p); frames = []
    for k in range(int((seconds + warm) / DT)):
        ar.threats = list(np.asarray(sp.agent_pos)); ar.targets = ar.threats
        sp.step(ar, DT); ar.step(DT)
        if k * DT >= warm:
            H = getattr(sp, "agent_heading", None)
            frames.append((p.pos.copy(), p.vel.copy(), np.array(sp.agent_pos, float), np.array(sp.agent_vel, float),
                           None if getattr(sp, "agent_size", None) is None else np.array(sp.agent_size, float),
                           getattr(sp, "agent_aspect", None), None if H is None else np.array(H, float)))
    return frames, p.radius


def windows(frames, radius, pr, window=WINDOW, stride=STRIDE):
    n, w, s = len(frames), int(window / DT), int(stride / DT); out = []
    for a in range(0, max(1, n - w + 1), s):
        rec = AffectRecorder(DT, pilot_radius=radius)
        for fr in frames[a:a + w]:
            rec.observe(*fr)
        sc = pr.score(rec.features()); out.append(dict(t=round(a * DT, 1), top=sc["top"], p=sc["p"]))
    return out


def summary(series, emotions):
    P = np.array([[w["p"][e] for e in emotions] for w in series])
    threat = P[:, emotions.index("menacing")] + P[:, emotions.index("terrifying")]
    return dict(mean={e: round(float(P[:, i].mean()), 3) for i, e in enumerate(emotions)},
                peak={e: round(float(P[:, i].max()), 3) for i, e in enumerate(emotions)},
                peak_threat=round(float(threat.max()), 3), mean_threat=round(float(threat.mean()), 3),
                tops=[w["top"] for w in series])


def check(pr):
    """Window-level agreement on the reference families (all four of each emotion, 2 seeds, hover)."""
    from archetypes import FAMILIES
    from neutral import NEUTRAL
    fams = [(e, F) for e, fs in FAMILIES.items() for F in fs] + [("neutral", F) for F in NEUTRAL]
    acc_w, acc_mean = [], []
    for e, F in fams:
        for seed in (31, 32):
            fr, r = record(F, seed, "hover")
            ws = windows(fr, r, pr)
            acc_w += [w["top"] == e for w in ws]
            m = summary(ws, pr.emotions)["mean"]; acc_mean.append(max(m, key=m.get) == e)
    return dict(window_acc=round(float(np.mean(acc_w)), 3), mean_of_windows_acc=round(float(np.mean(acc_mean)), 3),
                n_windows=len(acc_w))


def species(pr):
    from species import GAME
    out = {}
    for name in ("worm_attack_cycle", "worm_colony", "shark_r30", "tadpole_flock"):
        fr, r = record(GAME[name], 201, "hover")
        out[f"game/{name}"] = summary(windows(fr, r, pr), pr.emotions)
    return out


if __name__ == "__main__":
    pr = EmotionProbe.load()
    what = sys.argv[1] if len(sys.argv) > 1 else "check"
    res = check(pr) if what == "check" else species(pr)
    p = os.path.join(HERE, "results", "timeline.json"); allr = json.load(open(p)) if os.path.exists(p) else {}
    allr[what] = res; json.dump(allr, open(p, "w"), indent=1)
    print(json.dumps(res, indent=1)[:3000])
