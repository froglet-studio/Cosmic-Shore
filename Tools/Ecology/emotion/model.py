"""The emotion probe's models: an interpretable multinomial logistic model and a prototype (nearest-centroid)
model over the standardised affect features. numpy + scipy only.

Held-out protocols (what "agreement" means here):
  lofo      leave-one-FAMILY-out: hold out family k of EVERY emotion, train on the other two. The probe
            must recognise an emotion from a mechanism it never saw. The headline number.
  within    5-fold random CV over variants (families shared between train and test) - the easy number.
  viewer    train on hover+cruise viewers, test on an EVADING viewer it never saw.
"""
from __future__ import annotations

import json
import math

import numpy as np
from scipy.optimize import minimize

from archetypes import EMOTIONS


def load(path, keys):
    rows = json.load(open(path))["rows"]
    X = np.array([[r["f"].get(k, 0.0) for k in keys] for r in rows], float)
    y = np.array([EMOTIONS.index(r["emotion"]) for r in rows])
    fam = np.array([r["family_idx"] for r in rows])
    return X, y, fam, rows


class Std:
    def fit(self, X):
        self.mu = X.mean(0); self.sd = X.std(0) + 1e-6; return self

    def __call__(self, X):
        return np.clip((X - self.mu) / self.sd, -6, 6)


class Logistic:
    """Multinomial logistic regression, L2. Interpretable: W[k, j] is how many log-odds of emotion k one
    standard deviation of feature j buys."""
    def __init__(self, lam=0.3):
        self.lam = lam

    def fit(self, X, y, K=len(EMOTIONS)):
        self.std = Std().fit(X); Z = self.std(X); n, d = Z.shape
        Y = np.eye(K)[y]

        def f(w):
            W = w[:K * d].reshape(K, d); b = w[K * d:]
            L = Z @ W.T + b; L -= L.max(1, keepdims=True)
            P = np.exp(L); P /= P.sum(1, keepdims=True)
            loss = -np.sum(Y * np.log(P + 1e-12)) / n + self.lam * np.sum(W * W) / 2
            G = (P - Y) / n
            gW = G.T @ Z + self.lam * W; gb = G.sum(0)
            return loss, np.concatenate([gW.ravel(), gb])
        r = minimize(f, np.zeros(K * d + K), jac=True, method="L-BFGS-B", options=dict(maxiter=2000))
        self.W = r.x[:K * d].reshape(K, d); self.b = r.x[K * d:]
        return self

    def proba(self, X):
        L = self.std(X) @ self.W.T + self.b; L -= L.max(1, keepdims=True)
        P = np.exp(L); return P / P.sum(1, keepdims=True)


class Prototype:
    """Nearest centroid in standardised space, feature-weighted by a per-feature relevance (between-class
    over within-class variance), softmax(-d^2 / T)."""
    def fit(self, X, y, K=len(EMOTIONS)):
        self.std = Std().fit(X); Z = self.std(X)
        self.C = np.array([Z[y == k].mean(0) for k in range(K)])
        within = np.mean([Z[y == k].var(0) for k in range(K)], axis=0) + 0.05
        between = self.C.var(0)
        self.w = between / within; self.w /= self.w.mean()
        D = self._d2(Z)
        # temperature: make the mean true-class margin ~ log(4)
        self.T = max(np.median(np.sort(D, 1)[:, 1] - np.sort(D, 1)[:, 0]) / math.log(4), 1e-3)
        return self

    def _d2(self, Z):
        return np.sum(self.w * (Z[:, None, :] - self.C[None]) ** 2, axis=2)

    def proba(self, X):
        D = self._d2(self.std(X)); L = -D / self.T; L -= L.max(1, keepdims=True)
        P = np.exp(L); return P / P.sum(1, keepdims=True)


def make(kind, **kw):
    return Logistic(**kw) if kind == "logistic" else Prototype()


def lofo(X, y, fam, kind="logistic", **kw):
    acc, conf = [], np.zeros((len(EMOTIONS),) * 2, int)
    for k in sorted(set(fam)):
        tr, te = fam != k, fam == k
        m = make(kind, **kw).fit(X[tr], y[tr])
        p = m.proba(X[te]).argmax(1)
        acc.append(np.mean(p == y[te]))
        for a, b in zip(y[te], p):
            conf[a, b] += 1
    return float(np.mean(acc)), conf


def within(X, y, kind="logistic", folds=5, seed=0, **kw):
    rng = np.random.default_rng(seed); idx = rng.permutation(len(y)); part = np.array_split(idx, folds)
    acc = []
    for i in range(folds):
        te = part[i]; tr = np.concatenate([part[j] for j in range(folds) if j != i])
        m = make(kind, **kw).fit(X[tr], y[tr]); acc.append(np.mean(m.proba(X[te]).argmax(1) == y[te]))
    return float(np.mean(acc))


def transfer(Xtr, ytr, Xte, yte, kind="logistic", **kw):
    m = make(kind, **kw).fit(Xtr, ytr)
    return float(np.mean(m.proba(Xte).argmax(1) == yte))


def permutation_importance(model, X, y, keys, reps=5, seed=0):
    """Per emotion: the drop in mean P(true emotion) on rows of that emotion when feature j is shuffled
    across all rows. Held-out rows should be passed in."""
    rng = np.random.default_rng(seed)
    P0 = model.proba(X)[np.arange(len(y)), y]
    out = np.zeros((len(EMOTIONS), len(keys)))
    for j in range(len(keys)):
        drops = np.zeros(len(EMOTIONS))
        for _ in range(reps):
            Xp = X.copy(); Xp[:, j] = rng.permutation(Xp[:, j])
            P = model.proba(Xp)[np.arange(len(y)), y]
            drops += np.array([np.mean((P0 - P)[y == k]) if (y == k).any() else 0 for k in range(len(EMOTIONS))])
        out[:, j] = drops / reps
    return out
