"""Tiny helpers so the flora tools need only numpy: 6-connected components on a voxel mask."""
import numpy as np


def components(mask):
    """Label 6-connected components of a boolean 3D mask (union-find over flat indices). Returns (labels, n)."""
    G = mask.shape
    idx = np.flatnonzero(mask.ravel())
    if not len(idx): return np.zeros(mask.shape, np.int64), 0
    par = {int(i): int(i) for i in idx}

    def f(x):
        while par[x] != x:
            par[x] = par[par[x]]; x = par[x]
        return x
    strides = (G[1] * G[2], G[2], 1)
    S = set(par)
    for i in idx:
        i = int(i); z = i % G[2]; y = (i // G[2]) % G[1]
        for s, ok in ((strides[0], True), (strides[1], y + 1 < G[1]), (1, z + 1 < G[2])):
            j = i + s
            if ok and j in S:
                a, b = f(i), f(j)
                if a != b: par[a] = b
    roots = {}
    lab = np.zeros(int(np.prod(G)), np.int64)
    for i in idx:
        r = f(int(i)); lab[i] = roots.setdefault(r, len(roots) + 1)
    return lab.reshape(G), len(roots)
