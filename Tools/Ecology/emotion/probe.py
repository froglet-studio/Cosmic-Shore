"""The emotion probe as a reusable scorer: load results/probe.json, score an affect feature dict.

    from emotion.probe import EmotionProbe
    probe = EmotionProbe.load()
    out = probe.score(features)      # {"p": {emotion: prob}, "top": "...", "affect": {valence, arousal, threat}}

The probe is an ENSEMBLE of two interpretable judges trained on the archetype reference set:
  * a multinomial logistic model (each weight = log-odds per standard deviation of a feature), and
  * a sub-prototype model (k-means centres inside each emotion = its named mechanisms).
Their probabilities are averaged. `agreement` reports whether the two judges pick the same emotion - a
search should not trust a creature only one judge believes.

AFFECT coordinates place the six emotions in a continuous space so a near miss is a near miss:
  valence, arousal  - Russell (1980) circumplex of affect.
  threat            - Fanselow & Lester (1988) predatory imminence continuum: pre-encounter (0), post-
                      encounter (a predator is there and attending: menace), circa-strike (contact is
                      imminent: terror).
These coordinates are OUR placement of the six words, not measured; they make errors graded.
"""
from __future__ import annotations

import json
import math
import os

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
EMOTIONS = ("cute", "playful", "eerie", "majestic", "menacing", "terrifying")
AFFECT = {           # valence, arousal, threat
    "cute":       (+0.8, 0.35, 0.00),
    "playful":    (+0.7, 0.75, 0.10),
    "eerie":      (-0.5, 0.30, 0.45),
    "majestic":   (+0.5, 0.20, 0.15),
    "menacing":   (-0.6, 0.55, 0.70),
    "terrifying": (-0.9, 0.95, 1.00),
}
A = np.array([AFFECT[e] for e in EMOTIONS])


def affect_of(p):
    p = np.asarray(p, float)
    v = p @ A
    return dict(valence=round(float(v[0]), 3), arousal=round(float(v[1]), 3), threat=round(float(v[2]), 3))


class EmotionProbe:
    def __init__(self, d):
        self.d = d
        self.keys_l, self.keys_s = d["logistic"]["features"], d["subproto"]["features"]
        L, S = d["logistic"], d["subproto"]
        self.Lmu, self.Lsd, self.W, self.b = (np.array(L[k]) for k in ("mu", "sd", "W", "b"))
        self.Smu, self.Ssd, self.Cs, self.lab, self.w, self.T = (np.array(S[k]) for k in ("mu", "sd", "centres", "labels", "w", "T"))

    @staticmethod
    def load(path=None):
        return EmotionProbe(json.load(open(path or os.path.join(HERE, "results", "probe.json"))))

    def _pl(self, f):
        x = np.array([f.get(k, 0.0) for k in self.keys_l]); z = np.clip((x - self.Lmu) / self.Lsd, -6, 6)
        l = self.W @ z + self.b; l -= l.max(); p = np.exp(l); return p / p.sum()

    def _ps(self, f):
        x = np.array([f.get(k, 0.0) for k in self.keys_s]); z = np.clip((x - self.Smu) / self.Ssd, -6, 6)
        D = np.sum(self.w * (z[None] - self.Cs) ** 2, axis=1)
        dk = np.array([D[self.lab == k].min() for k in range(len(EMOTIONS))])
        l = -dk / float(self.T); l -= l.max(); p = np.exp(l); return p / p.sum()

    def score(self, f):
        pl, ps = self._pl(f), self._ps(f); p = 0.5 * (pl + ps)
        return dict(p={e: round(float(x), 4) for e, x in zip(EMOTIONS, p)}, top=EMOTIONS[int(p.argmax())],
                    logistic=EMOTIONS[int(pl.argmax())], subproto=EMOTIONS[int(ps.argmax())],
                    agreement=bool(pl.argmax() == ps.argmax()), affect=affect_of(p),
                    _pl=pl, _ps=ps)

    def explain(self, f, emotion, top=5):
        """Which features push this creature toward `emotion` in the logistic judge (contribution =
        weight x standardised value)."""
        k = EMOTIONS.index(emotion)
        x = np.array([f.get(q, 0.0) for q in self.keys_l]); z = np.clip((x - self.Lmu) / self.Lsd, -6, 6)
        c = self.W[k] * z - (self.W * z).mean(0)
        order = np.argsort(-np.abs(c))[:top]
        return [(self.keys_l[i], round(float(c[i]), 2)) for i in order]

    def advise(self, f, target, top=4):
        """What to change to move this creature toward `target`: the logistic features whose contribution
        to (target - current top) is most negative, with the direction to move each. Design hints, not laws -
        a feature can only move as far as the species' own rules let it."""
        cur = self.score(f)["top"]
        if cur == target:
            return []
        kt, kc = EMOTIONS.index(target), EMOTIONS.index(cur)
        x = np.array([f.get(q, 0.0) for q in self.keys_l]); z = np.clip((x - self.Lmu) / self.Lsd, -6, 6)
        dw = self.W[kt] - self.W[kc]
        gain = dw * z                      # how much each feature currently helps target over current
        order = np.argsort(gain)[:top]
        return [(self.keys_l[i], "raise" if dw[i] > 0 else "lower", round(float(gain[i]), 2)) for i in order]
