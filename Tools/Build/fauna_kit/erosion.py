#!/usr/bin/env python3
"""THE SPACE SPINDLE, measured without Unity: the game's own erosion (SpindleErosion.cs, Fauna.ErodeSpindle,
FaunaElementalForm, FloraElementalForm.ShapeLeaf), ported line for line.

    python3 Tools/Build/fauna_kit/erosion.py <Prefab name>      # what the game would do to that prefab at Space
    python3 Tools/Build/fauna_kit/erosion.py --self-test        # the port against the numbers the C# tests pin

Why this exists. The law is Garrett's (Docs/ECOSYSTEM.md 64.9, 2026-10-28): "the space spindle needs to be universally
scaled to bigger than mass, but we must erode and thin its thickest sections so the whole spindle gets more spindly".
The game applies it at spawn to every skin of a Space body, EACH SKIN ABOUT ITS OWN SPINE. That is right for a body
with one skin. A jointed body (unity_port.py cuts a wing into pieces, one a joint) is many skins, and each piece is
drawn in toward its own middle: the wing tears at its hinges and crumples. `runtime()` measures exactly that, so the
choice is made on a number: a body the runtime would tear by more than GAP is AUTHORED eroded instead (elements.py's
Space form applies the same law to every limb's girth and wire, and the prisms take the leaf law here). Its Space
config still says ErodeSpindle, as every species' does; its PREFAB says `spindleAuthoredEroded`, and Fauna.ErodeSpindle
then leaves the skins and prisms as authored and reports the law's girth.

The functions:
    girth(width, reference), reference(thickest)     FaunaElementalForm.ErodedGirth / ErosionReference
    Erosion(vertices, anchor)                        SpindleErosion: .has_spine, .thickest, .map(p), .girth_at(p)
    needle(size)                                     the Space prism: ShapeLeaf's shape, as long as its socket less 3%
    runtime(prefab)                                  Fauna.ErodeSpindle on a prefab's skins: how far each one moves
"""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

SPACE_LENGTH_OVER_MASS = 1.6477          # FaunaElementalForm.SpaceLengthOverMass
SPACE_LEAF_ANISOTROPY = 2.11             # FloraElementalForm.SpaceLeafAnisotropy
SPACE_LEAF_VOLUME = 0.4097               # FloraElementalForm.SpaceLeafVolume (checked against the source in the self-test)
FIT_CLEARANCE = 0.97                     # FaunaElementalForm.FitClearance
EXPONENT = 1.0 / SPACE_LEAF_ANISOTROPY   # SpindleGirthExponent
THICKEST_GIRTH = 1.0 / SPACE_LENGTH_OVER_MASS
SECTIONS = 24                            # SpindleErosion.Sections
SPINE_ELONGATION_LIMIT = 0.75            # SpindleErosion.SpineElongationLimit
GAP = 0.5                                # u: a seam the runtime opens wider than this is seen (a wire is ~0.1 thick)


def reference(thickest):
    return thickest * THICKEST_GIRTH ** (1.0 / (1.0 - EXPONENT))


def girth(width, ref):
    return (width / ref) ** (EXPONENT - 1.0) if ref > 0 and width > ref else 1.0


class Erosion:
    """SpindleErosion: the spine is the cloud's principal axis, its width the narrowest one; SECTIONS slices along the
    spine, each slice's half-width across the narrowest axis (smoothed 1-2-1) sets its girth factor."""

    def __init__(self, vertices, anchor):
        v = np.asarray(vertices, dtype=float)
        self.anchor = np.asarray(anchor, dtype=float)
        c = v.mean(0)
        d = v - c
        cov = d.T @ d
        w, vec = np.linalg.eigh(cov)                     # ascending
        self.spine, width = vec[:, 2], vec[:, 0]
        self.elongation = float(np.sqrt(max(0.0, w[1]) / w[2])) if w[2] > 1e-12 else 1.0
        self.has_spine = self.elongation <= SPINE_ELONGATION_LIMIT
        s = (v - self.anchor) @ self.spine
        self.start = float(s.min())
        self.step = max(1e-6, float(s.max() - s.min()) / SECTIONS)
        k = np.clip(((s - self.start) / self.step).astype(int), 0, SECTIONS - 1)
        half = np.zeros(SECTIONS)
        np.maximum.at(half, k, np.abs((v - self.anchor) @ width))
        smooth = half.copy()
        smooth[1:-1] = (half[:-2] + 2 * half[1:-1] + half[2:]) * 0.25
        if not self.has_spine:
            self.girths = np.ones(SECTIONS)
        else:
            ref = reference(float(smooth.max()))
            self.girths = np.array([girth(x, ref) for x in smooth])
        self.thickest = float(self.girths.min())

    def girth_at(self, p):
        x = (np.asarray(p, dtype=float) - self.anchor) @ self.spine
        x = (x - self.start) / self.step - 0.5
        x = np.clip(x, 0, SECTIONS - 1)
        k = np.minimum(x.astype(int), SECTIONS - 2)
        t = x - k
        return self.girths[k] * (1 - t) + self.girths[k + 1] * t

    def map(self, p):
        p = np.asarray(p, dtype=float)
        d = p - self.anchor
        along = np.outer(d @ self.spine, self.spine) if d.ndim == 2 else self.spine * (d @ self.spine)
        g = self.girth_at(p)
        return self.anchor + along + (d - along) * (g[:, None] if d.ndim == 2 else g)

    def off_spine(self, p):
        d = np.asarray(p, dtype=float) - self.anchor
        return np.linalg.norm(d - np.outer(d @ self.spine, self.spine), axis=1)


def needle(size, socket=None):
    """A Space prism (Fauna.ErodeSpindle's refit): ShapeLeaf gives the SHAPE, the prism spans its socket's length less
    3%, and is never thicker than the socket. `socket` is the prism's eroded seat (its own size when the seat did not
    thin). Returns the size in the prism's own axes."""
    size = [float(s) for s in size]
    socket = [float(s) for s in (socket or size)]
    mean = (size[0] * size[1] * size[2]) ** (1 / 3)
    scale = mean * SPACE_LEAF_VOLUME ** (1 / 3)
    shape = [scale * (s / mean) ** SPACE_LEAF_ANISOTROPY for s in size]
    lng = max(range(3), key=lambda i: (shape[i], -i))
    length = socket[lng] * FIT_CLEARANCE
    return [length if i == lng else min(length * shape[i] / shape[lng], socket[i] * FIT_CLEARANCE) for i in range(3)]


def runtime(prefab):
    """What Fauna.ErodeSpindle does to a prefab's skins. Returns (rows, worst): a row a skin (name, vertices, has a
    spine, its thickest girth, how far its vertices move at most, how far it tears from a skin it touched), and the
    widest tear: how far apart two vertices that touched in the prefab (two pieces of one wing, at a hinge) end up."""
    import dossier as D
    path = prefab if prefab.endswith(".prefab") else f"Assets/_Prefabs/FloraAndFauna/{prefab}.prefab"
    body = D.body_of(path)
    skins = [(w["name"], np.asarray(w["verts"], dtype=float)) for w in body["wires"] if len(w["verts"])]
    main = max(range(len(skins)), key=lambda i: (len(skins[i][1]), np.ptp(skins[i][1], axis=0).max()))
    heart = np.asarray(body["heart"]["at"] if body.get("heart") else [0, 0, 0], dtype=float)
    body_map = Erosion(skins[main][1], heart)
    after = []
    for i, (name, v) in enumerate(skins):
        if i == main:
            m, shift = body_map, np.zeros(3)
        else:
            m = Erosion(v, v.mean(0))
            root = v[int(np.argmin(body_map.off_spine(v)))]
            shift = body_map.map(root) - root
        after.append((m, m.map(v) + shift))

    rows, worst = [], 0.0
    for i, (name, v) in enumerate(skins):
        m, w = after[i]
        seam = 0.0
        for j, (_n, u) in enumerate(skins):
            if j == i:
                continue
            d = np.linalg.norm(v[:, None, :] - u[None, :, :], axis=2)
            a, b = np.nonzero(d < 0.6)                           # the vertices of the two skins that touched in the prefab
            if len(a):
                seam = max(seam, float((np.linalg.norm(w[a] - after[j][1][b], axis=1) - d[a, b]).max()))
        worst = max(worst, seam)
        rows.append((name, len(v), m.has_spine, m.thickest, float(np.linalg.norm(w - v, axis=1).max()), seam))
    return rows, worst


def self_test():
    fails = []
    # FaunaElementalFormTests: the thickest section comes back to the Mass girth; thin sections are left whole
    for t in (0.5, 4.48, 40.0):
        if abs(girth(t, reference(t)) - THICKEST_GIRTH) > 1e-4:
            fails.append(f"thickest {t}: girth {girth(t, reference(t))}")
    ref = reference(4.0)
    if girth(ref * 0.5, ref) != 1.0 or girth(ref, ref) != 1.0:
        fails.append("a thin section was eroded")
    if not 3 * girth(3, ref) > 2 * girth(2, ref):
        fails.append("a thick section ended thinner than a thin one")
    # TheBarrel_IsErodedToTheMassGirth_TheTailIsKept_AndTheSpineIsUntouched
    import math
    body = []
    for k in range(61):
        z = float(k)
        r = 4.0 if z <= 30 else 0.5
        for a in range(16):
            t = a * math.pi * 2 / 16
            body.append([r * math.cos(t), r * 1.5 * math.sin(t), z])
    e = Erosion(body, [0, 0, 0])
    if abs(e.thickest - THICKEST_GIRTH) > 0.02:
        fails.append(f"the barrel's thickest girth {e.thickest}")
    m = e.map(np.array([4.0, 0.0, 15.0]))
    if abs(m[2] - 15) > 1e-4 or abs(m[0] - 4 * THICKEST_GIRTH) > 0.1:
        fails.append(f"the barrel point maps to {m}")
    tail = np.array([0.5, 0.75, 55.0])
    if np.linalg.norm(e.map(tail) - tail) > 1e-4:
        fails.append("the thin tail moved")
    # a disc has no spine and is left whole
    disc = [[math.cos(a) * r, math.sin(a) * r, 0.1 * ((i + j) % 2)] for i, a in enumerate(np.linspace(0, 6.28, 40)) for j, r in enumerate((2, 5, 9))]
    if Erosion(disc, [0, 0, 0]).has_spine:
        fails.append("a disc was given a spine")
    # the constants are the source's
    from prefab_read import REPO
    src = open(os.path.join(REPO, "Assets/_Scripts/Utility/DataContainers/FloraElementalForm.cs")).read()
    fauna = open(os.path.join(REPO, "Assets/_Scripts/Utility/DataContainers/FaunaElementalForm.cs")).read()
    ero = open(os.path.join(REPO, "Assets/_Scripts/Controller/Environment/FloraAndFauna/SpindleErosion.cs")).read()
    import re
    for name, text, mine in (("SpaceLeafAnisotropy", src, SPACE_LEAF_ANISOTROPY), ("SpaceLeafVolume", src, SPACE_LEAF_VOLUME),
                             ("SpaceLengthOverMass", fauna, SPACE_LENGTH_OVER_MASS), ("FitClearance", fauna, FIT_CLEARANCE),
                             ("Sections", ero, SECTIONS), ("SpineElongationLimit", ero, SPINE_ELONGATION_LIMIT)):
        m = re.search(r"const (?:float|int) %s = ([0-9.]+)f?;" % name, text)
        if not m or abs(float(m.group(1)) - mine) > 1e-6:
            fails.append(f"{name}: the source says {m.group(1) if m else 'nothing'}, this file {mine}")
    # a needle is long, thin, and inside its socket
    n = needle([0.5, 1.8, 3.6])
    if not (abs(n[2] - 3.6 * FIT_CLEARANCE) < 1e-9 and n[0] < 0.5 and n[1] < 1.8 and n[2] / n[1] > 3.6 / 1.8):
        fails.append(f"needle {n}")
    return fails


def main(argv):
    if "--self-test" in argv:
        fails = self_test()
        for f in fails:
            print("FAIL:", f)
        print("erosion self-test:", "ok" if not fails else f"{len(fails)} problem(s)")
        return 1 if fails else 0
    names = [a for a in argv if not a.startswith("-")]
    if not names:
        print(__doc__)
        return 2
    for name in names:
        rows, worst = runtime(name)
        print(f"{name}: what the game's Space erosion would do to each skin")
        print(f"  {'skin':<14}{'verts':>6}  spine  thickest  moves  torn from its neighbour")
        for n, nv, spine, thick, moved, seam in rows:
            print(f"  {n:<14}{nv:>6}  {'yes' if spine else 'no ':<5}  {thick:8.3f}  {moved:5.2f}  {seam:5.2f}")
        print(f"  widest tear: {worst:.2f} u ->", "AUTHOR this body eroded (elements.py; the prefab says spindleAuthoredEroded)" if worst > GAP
              else "the runtime erosion is safe here")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
