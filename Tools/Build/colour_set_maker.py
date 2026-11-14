#!/usr/bin/env python3
"""Make a colour set: the /colour-set skill's tool (.claude/skills/colour-set/SKILL.md).

A set is drafted from an inspiration (a photograph, a style, a mood, a film's palette, an animal) as twelve INTENT
pigments, a rim and a face per domain. This tool finds the nearest set that keeps every rule of
Tools/Build/colour_sets.py, shows where the six families can live, samples a photograph's pigments, and draws the sets
as the block shader paints them. colour_sets.py stays the judge: whatever `fit` returns is re-checked by it.

    python3 Tools/Build/colour_set_maker.py atlas                       # where each family's rims and faces can live
    python3 Tools/Build/colour_set_maker.py sample photo.jpg [--k 8]    # a photograph's dominant pigments
    python3 Tools/Build/colour_set_maker.py fit draft.json [--out set.json] [--seed 1] [--iters 30000]
    python3 Tools/Build/colour_set_maker.py fit nocturne               # re-fit a set already in colour_sets.json
    python3 Tools/Build/colour_set_maker.py preview nocturne nacre --out sheet.png [--draft draft.json]
    python3 Tools/Build/colour_set_maker.py hero nocturne --out nocturne.jpg   # the lab's picture of one set
    python3 Tools/Build/colour_set_maker.py --self-test

A DRAFT is one set in colour_sets.json's shape ({"key": {...}}), its pigments where the inspiration puts them; swatches
may be omitted. `fit` moves each pigment as little as it can (OKLab distance of its swatch, plus its range and spread)
until every rule holds, rounds to the repo's precision (hue 1 degree, sat 0.001, L 0.01, rho 0.001), re-checks with
colour_sets.set_problems and prints the set ready to paste. A draft may pin a pigment (`"pin": true`) so it is never
moved, and name `"objective": "vision"` to maximise rule 5 for its vision instead of staying near its intent, or
`"objective": "name"` to make a vision set's rims look as much like their own names as its viewer allows (for a viewer
no lawful set exists for; the set then declares the `name_margin` it reached).
"""
import argparse
import itertools
import json
import math
import os
import random
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
import colour_domains  # noqa: E402
import colour_sets as cs  # noqa: E402

DOMAINS, ROLES = cs.DOMAINS, cs.ROLES
TRUTH = colour_domains.load()
N_CELL = np.array([TRUTH["cells"][f]["n"] for f in DOMAINS])        # (6, 3)
C_CELL = np.array([TRUTH["cells"][f]["c"] for f in DOMAINS])        # (6,)
HALF = TRUTH["margin"] / 2
EPS = {"band": 0.002, "gap": 0.002, "form": 0.005, "name": 0.004, "voice": 0.004, "apart": 0.003, "vision": 0.002}
BOUNDS = {"hue": (0.0, 360.0), "sat": (0.005, 0.6), "Lm": (0.04, 0.98), "Lh": (0.02, 0.2), "rho": (0.008, 0.06)}
STEP0 = {"hue": 6.0, "sat": 0.02, "Lm": 0.03, "Lh": 0.02, "rho": 0.005}
KEYS = ("hue", "sat", "Lm", "Lh", "rho")


# ----------------------------------------------------------------------------- vector colour maths
M1 = np.array([[0.4122214708, 0.5363325363, 0.0514459929], [0.2119034982, 0.6806995451, 0.1073969566],
               [0.0883024619, 0.2817188376, 0.6299787005]])
M2 = np.array([[0.2104542553, 0.7936177850, -0.0040720468], [1.9779984951, -2.4285922050, 0.4505937099],
               [0.0259040371, 0.7827717662, -0.8086757660]])
M2I = np.linalg.inv(M2)
M1I = np.linalg.inv(M1)


def lab_of_lin(lin):
    lms = np.cbrt(lin @ M1.T)
    return lms @ M2.T


def lin_of_lab(lab):
    lms = lab @ M2I.T
    return (lms ** 3) @ M1I.T


def gamma(v):
    v = np.clip(v, 0.0, 1.0)
    return np.where(v <= 0.0031308, 12.92 * v, 1.055 * np.power(v, 1 / 2.4) - 0.055)


def linear(v):
    v = np.clip(v, 0.0, 1.0)
    return np.where(v <= 0.04045, v / 12.92, np.power((v + 0.055) / 1.055, 2.4))


def clearance(lab, fam):
    """Signed OKLab distance into family `fam` beyond the band's edge (positive: in the family, clear of the band).
    lab: (..., 3); fam: index or array of indices broadcastable to lab[..., 0]."""
    score = np.asarray(lab, float) @ N_CELL.T + C_CELL               # (..., 6)
    fam = np.broadcast_to(np.asarray(fam), score.shape[:-1])
    own = np.take_along_axis(score, fam[..., None], -1)[..., 0]
    dn = np.linalg.norm(N_CELL[fam][..., None, :] - N_CELL, axis=-1)  # (..., 6), 0 against itself
    with np.errstate(divide="ignore", invalid="ignore"):
        d = (own[..., None] - score) / dn
    d = np.where(dn > 0, d, np.inf)
    return d.min(-1) - HALF


# ----------------------------------------------------------------------------- the set as numbers
class Model:
    """Twelve pigments as an array P (12, 5): hue, sat, L mid, L half-span, rho; row 2*d + r for domain d, role r."""

    def __init__(self, s, defs):
        self.s, self.lw = s, cs.laws(defs)
        self.style = defs.get("styles", {}).get(s.get("style")) or {}
        self.viewer = s.get("vision", "typical")
        self.typical = self.viewer == "typical"
        if not self.typical:     # the game's anchors as this viewer sees them (colour_sets.vision_prototypes)
            protos = cs.vision_prototypes(TRUTH, self.viewer)
            self.proto = np.array([p for f in DOMAINS for p in protos[f]])
            self.proto_fam = np.array([i for i, f in enumerate(DOMAINS) for _ in protos[f]])
        self.P = np.array([[p["hue"], p["sat"], cs.mid(p), (p["L"][1] - p["L"][0]) / 2, p["rho"]]
                           for _, _, p in cs.pigments(s)], float)
        self.pins = np.array([bool(p.get("pin")) for _, _, p in cs.pigments(s)])
        self.intent = self.P.copy()
        self.intent_lab = self.swatch_lab(self.P)
        dom = np.repeat(np.arange(6), 2)
        self.pairs = np.array([(i, j) for i, j in itertools.combinations(range(12), 2) if dom[i] != dom[j]])
        self.rim_pairs = np.array(list(itertools.combinations(range(6), 2)))
        self.vision = s.get("vision")
        self.fields = writer_fields()

    @staticmethod
    def direction(P):
        h = np.radians(P[:, 0])
        return np.stack([P[:, 1] * np.cos(h), P[:, 1] * np.sin(h)], 1)

    def swatch_lab(self, P):
        d = self.direction(P)
        return np.stack([P[:, 2], d[:, 0] * P[:, 2], d[:, 1] * P[:, 2]], 1)

    # each term returns a non-negative violation (0 when the rule holds)
    def violations(self, P):
        s, lw, out = self.s, self.lw, {}
        d2 = self.direction(P)
        lab = self.swatch_lab(P)
        lin = lin_of_lab(lab)
        out["gamut"] = 20 * (np.clip(lin - 0.9995, 0, None).sum() + np.clip(0.0005 - lin, 0, None).sum())
        L0, L1 = P[:, 2] - P[:, 3], P[:, 2] + P[:, 3]
        out["range"] = np.clip(0.02 - L0, 0, None).sum() + np.clip(L1 - 1.0, 0, None).sum()
        # rules 1 and 2 on the cross-domain pairs, a 17 x 17 sweep of each pair's L ranges
        i, j = self.pairs[:, 0], self.pairs[:, 1]
        t = np.linspace(0, 1, 17)
        La = L0[i, None, None] + (L1 - L0)[i, None, None] * t[None, :, None]
        Lb = L0[j, None, None] + (L1 - L0)[j, None, None] * t[None, None, :]
        dx = d2[i, 0, None, None] * La - d2[j, 0, None, None] * Lb
        dy = d2[i, 1, None, None] * La - d2[j, 1, None, None] * Lb
        planar = np.hypot(dx, dy) - P[i, 4, None, None] * La - P[j, 4, None, None] * Lb
        g = np.hypot(La - Lb, np.clip(planar, 0, None)).min((1, 2))
        out["band"] = np.clip(s["margin"] + EPS["band"] - g, 0, None).sum()
        pg = np.linalg.norm(d2[i] - d2[j], axis=1) - P[i, 4] - P[j, 4]
        out["gap"] = np.clip(s["pigment_gap"] + EPS["gap"] - pg, 0, None).sum()
        rim, face = P[0::2], P[1::2]
        out["form"] = np.clip(s["min_lightness_contrast"] + EPS["form"] - np.abs(rim[:, 2] - face[:, 2]), 0, None).sum()
        dh = np.abs((rim[:, 0] - face[:, 0] + 180) % 360 - 180)
        out["hue"] = np.clip(max(s.get("min_hue_contrast", 0), self.style.get("min_hue_contrast", 0)) + 0.6 - dh, 0, None).sum() / 90
        if "max_hue_contrast" in self.style:
            out["hue"] += np.clip(dh - self.style["max_hue_contrast"] + 0.6, 0, None).sum() / 90
        # rule 4: every rim reads as its family as swatch, light and paint (and Lime at the crystal body's brightness)
        rim_lin = np.clip(lin[0::2], 0, None)
        unit = rim_lin / np.maximum(rim_lin.max(1, keepdims=True), 1e-9)
        light_lab = lab_of_lin(linear(gamma(unit)))          # the light reading, as the screen quantises it
        paint_lab = lab_of_lin(linear(unit))                 # the same numbers read as sRGB
        fam = np.arange(6)
        paint_v = np.clip(np.linalg.norm(d2[1::2] - d2[0::2], axis=1) - rim[:, 4] - face[:, 4] + 0.002, 0, None) * 2
        allowed = self.style.get("faces", cs.VOICES)
        a, b = self.rim_pairs[:, 0], self.rim_pairs[:, 1]
        if self.typical:
            name = np.clip(EPS["name"] - clearance(lab[0::2], fam), 0, None)
            name += np.clip(EPS["name"] - clearance(light_lab, fam), 0, None)
            name += np.clip(EPS["name"] - clearance(paint_lab, fam), 0, None)
            lime = DOMAINS.index("lime")
            body = lab_of_lin(unit[lime] * lw["crystal_body"])
            name[lime] += max(0.0, EPS["name"] - float(clearance(body, lime)))
            out["name"] = name.sum()
            # rule 6: each face keeps a voice
            fam_v = np.clip(EPS["voice"] - clearance(lab[1::2], fam), 0, None)
            quiet_v = np.clip(face[:, 2] + face[:, 3] - lw["night_line"] + 0.003, 0, None)
            # rule 7: the rims apart as light and as paint
            sep = lw["rim_separation"] + EPS["apart"]
            out["apart"] = (np.clip(sep - np.linalg.norm(light_lab[a] - light_lab[b], axis=1), 0, None).sum()
                            + np.clip(sep - np.linalg.norm(paint_lab[a] - paint_lab[b], axis=1), 0, None).sum())
        else:
            # rules 4 and 6 as this viewer sees them: nearer its own family's anchors than any other family's
            need = s.get("name_margin", lw["name_margin"]) + EPS["name"]
            seen_light, seen_hud = self.see(unit), self.see(linear(unit))
            out["name"] = (np.clip(need - self.margin(seen_light, fam), 0, None).sum()
                           + np.clip(need - self.margin(seen_hud, fam), 0, None).sum())
            fam_v = np.clip(EPS["voice"] - self.margin(self.see(np.clip(lin[1::2], 0, None)), fam), 0, None)
            top = lin_of_lab(np.stack([face[:, 2] + face[:, 3], d2[1::2, 0] * (face[:, 2] + face[:, 3]),
                                       d2[1::2, 1] * (face[:, 2] + face[:, 3])], 1))
            quiet_v = np.clip(self.see(np.clip(top, 0, None))[:, 0] - lw["night_line"] + 0.003, 0, None)
            out["apart"] = 0.0                                      # rule 5 keeps the rims apart for this viewer
        stack = [v for k, v in (("family", fam_v), ("paint", paint_v), ("quiet", quiet_v)) if k in allowed]
        out["voice"] = np.min(np.stack(stack), 0).sum()
        # style ranges
        st = self.style
        v = 0.0
        if "rim_L" in st:
            v += (np.clip(st["rim_L"][0] - rim[:, 2], 0, None) + np.clip(rim[:, 2] - st["rim_L"][1], 0, None)).sum()
        if "face_L" in st:
            v += (np.clip(st["face_L"][0] - face[:, 2], 0, None) + np.clip(face[:, 2] - st["face_L"][1], 0, None)).sum()
        if "face_chroma" in st:
            c = face[:, 1] * face[:, 2]
            v += (np.clip(st["face_chroma"][0] - c, 0, None) + np.clip(c - st["face_chroma"][1], 0, None)).sum()
        if "face_band" in st:
            v += max(0.0, face[:, 2].max() - face[:, 2].min() - st["face_band"] + 0.005)
        out["style"] = v
        # the asset writer's field check: no painted field (whitened tiers included) inside another domain's pigment
        f = self.fields
        units = np.clip(lin, 0, None) / np.maximum(np.clip(lin, 0, None).max(1, keepdims=True), 1e-9)   # (12, 3)
        h = units[f["row"]]
        h = np.where(f["srgb"][:, None], gamma(h), h)
        col = f["peak"][:, None] * (h + (1 - h) * f["white"][:, None])
        col = np.where(f["srgb"][:, None], linear(np.clip(col, 0, 1)), col)
        fl = lab_of_lin(np.clip(col, 1e-9, None))
        fdir = fl[:, 1:] / np.maximum(fl[:, :1], 1e-6)                                          # (F, 2)
        dist = np.linalg.norm(fdir[:, None, :] - d2[None, :, :], axis=2) - P[None, :, 4]          # (F, 12)
        foreign = f["dom"][:, None] != (np.arange(12) // 2)[None, :]
        out["fields"] = (np.clip(0.004 - dist, 0, None) * foreign).sum()
        if self.vision:
            out["vision"] = max(0.0, self.s.get("vision_margin", 0) + EPS["vision"] - self.vision_sep(P))
        return out

    def see(self, x):
        """OKLab of linear colours (N, 3) as this set's viewer sees them (colour_sets.see_linear, vectorised)."""
        vision = self.viewer
        x = np.clip(x, 0, 1)
        if vision in cs.CVD:
            x = np.clip(x @ np.array(cs.CVD[vision]).T, 0, 1)
        elif vision == "achromat":
            y = x @ np.array([0.2126, 0.7152, 0.0722])
            x = np.stack([y, y, y], 1)
        o = lab_of_lin(np.clip(x, 1e-12, 1))
        if vision == "low_vision":
            o[:, 1:] *= 0.5
        return o

    def margin(self, seen, fam):
        """For each seen colour (N, 3) of domain fam[i]: how much nearer its own family's anchors than any other's."""
        D = np.linalg.norm(seen[:, None, :] - self.proto[None, :, :], axis=2)
        own = np.where(self.proto_fam[None, :] == fam[:, None], D, np.inf).min(1)
        other = np.where(self.proto_fam[None, :] != fam[:, None], D, np.inf).min(1)
        return other - own

    def vision_sep(self, P):
        lab = self.swatch_lab(P)
        lin = np.clip(lin_of_lab(lab), 0, None)
        lin[0::2] = lin[0::2] / np.maximum(lin[0::2].max(1, keepdims=True), 1e-9)
        hud = linear(lin[0::2])                       # the rims as the HUD paints them: the floats read as sRGB
        vision = self.vision

        def see(x):
            if vision in cs.CVD:
                x = np.clip(np.clip(x, 0, 1) @ np.array(cs.CVD[vision]).T, 0, 1)
            elif vision == "achromat":
                y = np.clip(x, 0, 1) @ np.array([0.2126, 0.7152, 0.0722])
                x = np.stack([y, y, y], 1)
            o = lab_of_lin(np.clip(x, 0, 1))
            if vision == "low_vision":
                o[:, 1:] *= 0.5
            return o
        seen, seen_hud = see(lin), see(hud)
        rims, faces = seen[0::2], seen[1::2]
        a, b = self.rim_pairs[:, 0], self.rim_pairs[:, 1]
        return min(np.linalg.norm(rims[a] - rims[b], axis=1).min(), np.linalg.norm(faces[a] - faces[b], axis=1).min(),
                   np.linalg.norm(seen_hud[a] - seen_hud[b], axis=1).min(), np.linalg.norm(rims - faces, axis=1).min())

    def distance(self, P):
        lab = self.swatch_lab(P)
        d = np.linalg.norm(lab - self.intent_lab, axis=1)
        return d.sum() + 0.3 * np.abs(P[:, 3] - self.intent[:, 3]).sum() + 0.3 * np.abs(P[:, 4] - self.intent[:, 4]).sum()

    def name_floor(self, P):
        """The weakest name margin of the set's rims, light and HUD, as its viewer sees them (rule 4 for a vision)."""
        lab = self.swatch_lab(P)
        lin = np.clip(lin_of_lab(lab), 0, None)[0::2]
        unit = lin / np.maximum(lin.max(1, keepdims=True), 1e-9)
        fam = np.arange(6)
        return min(self.margin(self.see(unit), fam).min(), self.margin(self.see(linear(unit)), fam).min())

    def cost(self, P, objective="near"):
        v = self.violations(P)
        total = sum(v.values())
        if objective == "vision":
            return 40 * total - self.vision_sep(P), total
        if objective == "name":      # for a viewer no lawful set exists for: as near its names as it can be
            rest = total - v.get("name", 0.0)
            return 40 * rest - self.name_floor(P) - 0.2 * self.vision_sep(P), rest
        return 40 * total + self.distance(P), total


_FIELDS = None


def writer_fields():
    """Every field author_colour_set_assets.py paints, as arrays: the pigment row it wears, its brightness, its share of
    the way to white, whether it is stored as sRGB, and its domain. Read once from PhoticColorSetSO."""
    global _FIELDS
    if _FIELDS is None:
        import author_colour_set_assets as w
        orig = w.read_original()
        rows, peak, white, srgb, dom = [], [], [], [], []
        for block, d in w.DOMAIN_SETS:
            di = DOMAINS.index(d)
            for field, role in w.FIELD_ROLE.items():
                c, plain = orig[block][field], orig[block][w.PLAIN[role]]
                pk = max(c[:3])
                if pk <= 1e-6:
                    continue
                w0 = w.whiteness(plain)
                s_ = max(0.0, (w.whiteness(c) - w0) / (1 - w0)) if w0 < 1 else 0.0
                rows.append(2 * di + (role == "face")); peak.append(min(pk, 1.0)); white.append(s_)
                srgb.append(field in w.SRGB_FIELDS); dom.append(2 * di)
        env = orig["EnvironmentColors"]
        for field, (d, role) in w.ENV_PAINTED.items():
            di = DOMAINS.index(d)
            rows.append(2 * di + (role == "face")); peak.append(min(max(env[field][:3]), 1.0)); white.append(0.0)
            srgb.append(False); dom.append(2 * di)
        _FIELDS = {"row": np.array(rows), "peak": np.array(peak), "white": np.array(white), "srgb": np.array(srgb),
                   "dom": np.array(dom) // 2}
    return _FIELDS


def clip(P):
    P[:, 0] %= 360
    for k, (lo, hi) in enumerate(BOUNDS[x] for x in KEYS):
        if k:
            P[:, k] = np.clip(P[:, k], lo, hi)
    return P


def search(model, iters=30000, seed=1, objective="near", P=None, steps=None, lattice=False):
    """A (1+1) evolution strategy with one adaptive step per parameter kind; pinned pigments never move."""
    rng = random.Random(seed)
    P = model.P.copy() if P is None else P.copy()
    J, V = model.cost(P, objective)
    step = dict(steps or STEP0)
    movable = [i for i in range(12) if not model.pins[i]]
    q = {"hue": 1.0, "sat": 0.001, "Lm": 0.005, "Lh": 0.005, "rho": 0.001}
    tries = {k: [0, 0] for k in KEYS}
    for it in range(iters):
        i = rng.choice(movable)
        k = rng.randrange(5)
        key = KEYS[k]
        Q = P.copy()
        delta = rng.gauss(0, step[key])
        if lattice:
            delta = q[key] * (1 if delta > 0 else -1) * max(1, round(abs(delta) / q[key]))
        Q[i, k] += delta
        if rng.random() < 0.15:   # sometimes move the same pigment's partner with it, so a pair can slide together
            j = i ^ 1
            if not model.pins[j]:
                Q[j, k] += delta * rng.uniform(0.3, 1.0)
        clip(Q)
        if lattice:
            Q = quantise(Q)
        Jq, Vq = model.cost(Q, objective)
        tries[key][0] += 1
        if Jq < J:
            P, J, V = Q, Jq, Vq
            tries[key][1] += 1
        if tries[key][0] >= 40:
            rate = tries[key][1] / tries[key][0]
            step[key] = min(STEP0[key] * 4, max(q[key] * 0.5, step[key] * (1.3 if rate > 0.2 else 0.77)))
            tries[key] = [0, 0]
    return P, J, V


def quantise(P):
    Q = P.copy()
    Q[:, 0] = np.round(Q[:, 0]) % 360
    Q[:, 1] = np.round(Q[:, 1], 3)
    L0 = np.round(Q[:, 2] - Q[:, 3], 2)
    L1 = np.round(Q[:, 2] + Q[:, 3], 2)
    Q[:, 2], Q[:, 3] = (L0 + L1) / 2, (L1 - L0) / 2
    Q[:, 4] = np.round(Q[:, 4], 3)
    return Q


def to_set(model, P):
    """The fitted set in colour_sets.json's shape, every swatch recomputed."""
    s = json.loads(json.dumps(model.s))
    for row, (d, r, _) in enumerate(cs.pigments(model.s)):
        p = s["domains"][d][r]
        p.pop("pin", None)
        L0, L1 = round(P[row, 2] - P[row, 3], 2), round(P[row, 2] + P[row, 3], 2)
        p.update(hue=int(round(P[row, 0])) % 360, sat=round(float(P[row, 1]), 3), L=[L0, L1], rho=round(float(P[row, 4]), 3))
        p["swatch"] = cs.hexof(cs.swatch(p)[0])
    s.pop("objective", None)
    return s


def fit(s, defs, iters=30000, seed=1, objective=None, restarts=3, quiet=False):
    """(the nearest lawful set, its problems under colour_sets.set_problems)."""
    objective = objective or s.get("objective", "near")
    model = Model(s, defs)
    best = None
    for r in range(restarts):
        P, J, V = search(model, iters, seed + r * 101, objective)
        if best is None or (V, J) < (best[2], best[1]):
            best = (P, J, V)
        if not quiet:
            print("  restart %d: violation %.5f, cost %.4f" % (r, V, J), file=sys.stderr)
    P = quantise(best[0])
    P, J, V = search(model, max(4000, iters // 4), seed + 7, objective, P=P, lattice=True)
    out = to_set(model, P)
    truth = colour_domains.load()
    probe = dict(defs, sets={"fit": out}, examples={})
    errs = cs.set_problems("fit", out, truth, probe)
    return out, errs


# ----------------------------------------------------------------------------- the atlas
def atlas():
    """For each family: where a rim can live (rule 4's three readings all inside it) and where a face can name it."""
    print("THE TERRITORIES (the six-family truth, colour_domains.json). A rim must read as its family as swatch, light and")
    print("paint (rule 4); a face named by its family must have its swatch inside it (rule 6 'family'). Hue is OKLab")
    print("degrees, sat is chroma/L (a pigment's direction), C is chroma at that L. '-' means nowhere.\n")
    hues = np.arange(0, 360, 2.0)
    for fi, f in enumerate(DOMAINS):
        print(f.upper())
        for L in (0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9):
            faces, rims = [], []
            for C in np.arange(0.01, 0.40, 0.005):
                lab = np.stack([np.full_like(hues, L), C * np.cos(np.radians(hues)), C * np.sin(np.radians(hues))], 1)
                lin = lin_of_lab(lab)
                ok = (lin >= -1e-4).all(1) & (lin <= 1 + 1e-4).all(1)
                inside = (clearance(lab, fi) > 0) & ok
                for h in hues[inside]:
                    faces.append((h, C))
                unit = np.clip(lin, 0, None) / np.maximum(np.clip(lin, 0, None).max(1, keepdims=True), 1e-9)
                rim_ok = inside & (clearance(lab_of_lin(linear(gamma(unit))), fi) > 0) & (clearance(lab_of_lin(linear(unit)), fi) > 0)
                for h in hues[rim_ok]:
                    rims.append((h, C))

            def span(pts):
                pts = [(h, c) for h, c in pts if c >= 0.04]      # colours, not greys (every grey is Blue's anyway)
                if not pts:
                    return "-"
                hs = sorted(set(h for h, _ in pts))
                runs, start, prev = [], hs[0], hs[0]
                for h in hs[1:]:
                    if h != prev + 2:
                        runs.append((start, prev))
                        start = h
                    prev = h
                runs.append((start, prev))
                if len(runs) > 1 and runs[0][0] == 0 and runs[-1][1] == 358:
                    runs = [(runs[-1][0] - 360, runs[0][1])] + runs[1:-1]
                cs_ = [c for _, c in pts]
                return "hue %s, C %.3f-%.3f (sat %.2f-%.2f)" % (" ".join("%d..%d" % r for r in runs), min(cs_), max(cs_),
                                                                  min(cs_) / L, max(cs_) / L)
            print("  L %.1f  face: %-52s rim: %s" % (L, span(faces), span(rims)))
        print()
    print("Blue also holds every grey (C under about 0.04) at every L. Below L 0.40 only Fire and Blue hold any colour:")
    print("that is the night line (rule 6 'quiet'). Gold exists only at L 0.70 and above, Ruby only at L 0.50-0.70 with")
    print("C 0.20 or more, and a Gold rim must sit at hue 82-96: an orange Gold reads as Fire on the HUD.")


# ----------------------------------------------------------------------------- sampling a photograph
def sample(path, k=8, box=None, seed=1):
    """The image's k dominant colours as pigment candidates: OKLab k-means, each cluster as hue, sat (C/L) and the L
    range between its 10th and 90th percentile."""
    from PIL import Image
    img = Image.open(path).convert("RGB")
    if box:
        img = img.crop(box)
    img.thumbnail((240, 240))
    px = np.asarray(img, float).reshape(-1, 3) / 255
    lab = lab_of_lin(linear(px))
    rng = np.random.default_rng(seed)
    cent = lab[rng.choice(len(lab), k, replace=False)]
    for _ in range(40):
        lbl = np.argmin(((lab[:, None] - cent[None]) ** 2).sum(-1), 1)
        cent = np.array([lab[lbl == c].mean(0) if (lbl == c).any() else cent[c] for c in range(k)])
    order = np.argsort(-np.bincount(lbl, minlength=k))
    print("share  hex      hue   sat   L range      nearest family (swatch)")
    for c in order:
        pts = lab[lbl == c]
        if len(pts) == 0:
            continue
        L, a, b = cent[c]
        hue = math.degrees(math.atan2(b, a)) % 360
        lo, hi = np.percentile(pts[:, 0], [10, 90])
        rgb = tuple(float(v) for v in gamma(lin_of_lab(cent[c][None]))[0])
        fam, top, clear = cs.family_of(TRUTH, rgb)
        print("%4.0f%%  %s  %4.0f  %.3f  %.2f-%.2f    %s" % (100 * len(pts) / len(lab), cs.hexof(rgb), hue, math.hypot(a, b) / L,
                                                          lo, hi, "%s %+.1f JND" % (fam, clear) if fam else "band (near %s)" % top))


# ----------------------------------------------------------------------------- preview
def preview(sets, out, width=1280):
    """Each set as the game paints it (author_colour_set_assets.palette: the rims at the original's HDR brightness, the
    faces at their pigment's L), on the arena sky: four prisms per domain row (Jade, Ruby, Gold, Blue: plain and
    shielded), the danger rim and a free crystal, with the block shader's fresnel lerp(face, rim, (1 - N.V)^4), the
    gameplay bloom (threshold 0.2, clamp 0.5, intensity 2.5) and no tonemapping."""
    from PIL import Image, ImageDraw, ImageFont
    import author_colour_set_assets as writer
    orig = writer.read_original()
    cols = len(sets)
    cw, ch = width // cols, 560
    sheet = Image.new("RGB", (cw * cols, ch + 70), (6, 9, 32))
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 18)
    except OSError:
        font = ImageFont.load_default()
    for ci, (label, s) in enumerate(sets):
        pal = orig if s is None else writer.palette(s, orig)
        img = render_tile(pal, cw, ch)
        sheet.paste(img, (ci * cw, 40))
        draw.text((ci * cw + 12, 10), label, fill=(230, 236, 255), font=font)
    try:
        small = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 13)
    except OSError:
        small = font
    draw.text((12, ch + 46), "Rows: Jade, Ruby, Gold, Blue (plain, shielded, super-shielded); then the danger rim, a free "
              "crystal (the CTA pair) and Blue super-shielded. Painted as the asset writer paints them, gameplay bloom, "
              "no tonemapping.", fill=(150, 160, 200), font=small)
    sheet.save(out)
    return out


def render_tile(pal, W, H):
    """One set's tile: rows of rounded slabs, ray-cast in numpy."""
    from PIL import Image
    sky = np.array(pal["EnvironmentColors"].get("SkyColor", (0, 0, 0.102, 0))[:3], float)
    img = np.ones((H, W, 3)) * linear(sky)        # the arena sky, an sRGB colour (COLOR.md: "the sky (0, 0, 0.102)")
    rows = [("JadeColors", "jade"), ("RubyColors", "ruby"), ("GoldColors", "gold"), ("BlueColors", "blue")]
    yy, xx = np.mgrid[0:H, 0:W]
    u = (xx + 0.5) / W * 2 - 1
    v = 1 - (yy + 0.5) / H * 2
    aspect = W / H
    ray = np.stack([u * aspect * 0.55, v * 0.55, -np.ones_like(u)], -1)
    ray /= np.linalg.norm(ray, axis=-1, keepdims=True)
    depth = np.full((H, W), np.inf)
    z = 4.6
    for ri, (block, _) in enumerate(rows + [("Env", None)]):
        y = (2.0 - ri * 1.0) * (z * 0.55) / 2.6
        for k in range(3):
            x = (-0.86 + k * 0.86) * min(1.0, aspect * 0.55 * z / 1.45)
            if block == "Env":
                if k == 0:
                    rim, face = pal["EnvironmentColors"]["Danger"][:3], pal["GoldColors"]["ShieldedOutsideBlockColor"][:3]
                elif k == 1:
                    rim, face = pal["EnvironmentColors"]["BrightCTA"][:3], pal["EnvironmentColors"]["DarkCTA"][:3]
                else:
                    rim, face = pal["BlueColors"]["SuperShieldedInsideBlockColor"][:3], pal["BlueColors"]["SuperShieldedOutsideBlockColor"][:3]
            else:
                tier = ("", "Shielded", "SuperShielded")[k]
                rim = pal[block][tier + "InsideBlockColor"][:3]
                face = pal[block][tier + "OutsideBlockColor"][:3]
            centre = np.array([x, y, -z])
            half = np.array([0.32, 0.085, 0.13])
            rot = rotation(0.45 + 0.12 * k, 0.5 - 0.12 * ri, 0.08)
            hit, t, nrm = ray_box(ray, centre, half, rot, bevel=0.075)
            take = hit & (t < depth)
            ndv = np.abs((nrm * -ray).sum(-1))
            fres = (1 - np.clip(ndv, 0, 1)) ** 4
            col = np.array(face)[None, None] * (1 - fres[..., None]) + np.array(rim)[None, None] * fres[..., None]
            img = np.where(take[..., None], col, img)
            depth = np.where(take, t, depth)
    # URP bloom as the gameplay profile sets it: the source clamped at 0.5, threshold 0.2, soft knee 0.1, intensity 2.5
    src = np.minimum(img, 0.5)
    br = src.max(-1)
    soft = np.clip(br - 0.2 + 0.1, 0, 0.2) ** 2 / (4 * 0.1 + 1e-5)
    contrib = np.maximum(soft, br - 0.2) / np.maximum(br, 1e-5)
    bloom = src * contrib[..., None]
    for sigma in (3, 9, 21):
        bloom = bloom + blur(src * contrib[..., None], sigma)
    out = img + 2.5 * bloom / 4
    return Image.fromarray((gamma(out) * 255 + 0.5).astype(np.uint8))


def hero(s, out, W=640, H=400, seed=7):
    """A set's picture for the lab: four loose clusters of prisms (Jade, Ruby, Gold, Blue; plain, shielded and
    super-shielded mixed), then a row of signals (danger over each domain's shielded face, a free crystal), painted as
    the asset writer paints the set, on the arena sky, with the gameplay bloom. Saved as a JPEG."""
    from PIL import Image
    import author_colour_set_assets as writer
    orig = writer.read_original()
    pal = orig if s is None else writer.palette(s, orig)
    rng = random.Random(seed)
    sky = np.array(pal["EnvironmentColors"].get("SkyColor", (0, 0, 0.102, 0))[:3], float)
    img = np.ones((H, W, 3)) * linear(sky)
    yy, xx = np.mgrid[0:H, 0:W]
    u, v = (xx + 0.5) / W * 2 - 1, 1 - (yy + 0.5) / H * 2
    ray = np.stack([u * (W / H) * 0.5, v * 0.5, -np.ones_like(u)], -1)
    ray /= np.linalg.norm(ray, axis=-1, keepdims=True)
    depth = np.full((H, W), np.inf)
    boxes = []
    blocks = ("JadeColors", "RubyColors", "GoldColors", "BlueColors")
    for gi, block in enumerate(blocks):
        cx = (-1.5 + gi) * 1.3
        for k in range(7):
            tier = ("", "", "", "Shielded", "Shielded", "SuperShielded", "")[k]
            boxes.append((pal[block][tier + "InsideBlockColor"][:3], pal[block][tier + "OutsideBlockColor"][:3],
                          np.array([cx + rng.uniform(-0.36, 0.36), rng.uniform(-0.1, 1.0), -5.2 + rng.uniform(-0.6, 0.6)])))
    env = pal["EnvironmentColors"]
    for k, block in enumerate(blocks):
        boxes.append((env["Danger"][:3], pal[block]["ShieldedOutsideBlockColor"][:3], np.array([(-1.9 + k * 1.05), -1.25, -5.0])))
    boxes.append((env["BrightCTA"][:3], env["DarkCTA"][:3], np.array([2.35, -1.2, -5.0])))
    for rim, face, centre in boxes:
        rot = rotation(rng.uniform(-1.2, 1.2), rng.uniform(-1.2, 1.2), rng.uniform(-1.2, 1.2))
        half = np.array([0.34, 0.09, 0.14]) if rim is not env["BrightCTA"][:3] else np.array([0.13, 0.13, 0.13])
        hit, tt, nrm = ray_box(ray, centre, half, rot, bevel=0.06)
        take = hit & (tt < depth)
        f = (1 - np.clip(np.abs((nrm * -ray).sum(-1)), 0, 1)) ** 4
        col = np.array(face)[None, None] * (1 - f[..., None]) + np.array(rim)[None, None] * f[..., None]
        img = np.where(take[..., None], col, img)
        depth = np.where(take, tt, depth)
    src = np.minimum(img, 0.5)
    br = src.max(-1)
    soft = np.clip(br - 0.2 + 0.1, 0, 0.2) ** 2 / (4 * 0.1 + 1e-5)
    contrib = (np.maximum(soft, br - 0.2) / np.maximum(br, 1e-5))[..., None]
    bloom = src * contrib
    acc = bloom.copy()
    for sigma in (3, 9, 21):
        acc = acc + blur(bloom, sigma)
    out_img = img + 2.5 * acc / 4
    Image.fromarray((gamma(out_img) * 255 + 0.5).astype(np.uint8)).save(out, quality=82)
    return out


def blur(a, sigma):
    r = int(3 * sigma)
    k = np.exp(-np.arange(-r, r + 1) ** 2 / (2 * sigma ** 2))
    k /= k.sum()
    out = np.apply_along_axis(lambda m: np.convolve(m, k, mode="same"), 0, a)
    return np.apply_along_axis(lambda m: np.convolve(m, k, mode="same"), 1, out)


def rotation(ax, ay, az):
    cx, sx, cy, sy, cz, sz = math.cos(ax), math.sin(ax), math.cos(ay), math.sin(ay), math.cos(az), math.sin(az)
    rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
    ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
    return rz @ ry @ rx


def ray_box(ray, centre, half, rot, bevel):
    """Hit mask, distance and a rounded-edge normal for rays from the origin against an oriented box: the normal of a
    box whose edges are rounded by `bevel`, so the fresnel brightens along the edges the way a prism's does."""
    o = (-centre) @ rot          # ray origin in box space (rot's columns are the box axes)
    d = ray @ rot
    with np.errstate(divide="ignore", invalid="ignore"):
        t1 = (-half - o) / d
        t2 = (half - o) / d
    tn = np.minimum(t1, t2).max(-1)
    tf = np.maximum(t1, t2).min(-1)
    hit = (tn <= tf) & (tf > 0)
    p = o + d * tn[..., None]
    inner = np.clip(p, -(half - bevel), half - bevel)
    nrm = p - inner
    nrm /= np.maximum(np.linalg.norm(nrm, axis=-1, keepdims=True), 1e-9)
    return hit, tn, nrm @ rot.T


# ----------------------------------------------------------------------------- the self-test
def self_test():
    """The solver's numbers agree with colour_sets.py's, and fitting a set that breaks a rule repairs it."""
    defs = cs.load()
    bad = 0
    for key in ("nocturne", "dart_frog"):
        s = defs["sets"][key]
        m = Model(s, defs)
        v = m.violations(m.P)
        errs = cs.set_problems(key, s, TRUTH, defs)
        agree = (sum(v.values()) > 0) == bool(errs) or not errs
        print("  %-36s %s" % ("solver agrees with the gate on " + key, "ok" if agree else "DISAGREES"))
        bad += not agree
    broken = json.loads(json.dumps(defs["sets"]["nocturne"]))
    broken["domains"]["gold"]["rim"].update(hue=72, sat=0.2)
    out, errs = fit(broken, defs, iters=6000, restarts=1, quiet=True)
    print("  %-36s %s" % ("fit repairs a gold the HUD shows as fire", "ok" if not errs else "FAILED: %s" % errs[:2]))
    bad += bool(errs)
    return bad


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cmd", nargs="?", choices=("atlas", "sample", "fit", "preview", "hero"))
    ap.add_argument("args", nargs="*")
    ap.add_argument("--out")
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--iters", type=int, default=30000)
    ap.add_argument("--restarts", type=int, default=3)
    ap.add_argument("--k", type=int, default=8)
    ap.add_argument("--box")
    ap.add_argument("--draft", action="append", default=[])
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args(argv)
    if a.self_test:
        bad = self_test()
        print("colour_set_maker --self-test:", "ok" if not bad else "%d failed" % bad)
        return 1 if bad else 0
    defs = cs.load()
    if a.cmd == "atlas":
        atlas()
        return 0
    if a.cmd == "sample":
        box = tuple(int(v) for v in a.box.split(",")) if a.box else None
        sample(a.args[0], a.k, box, a.seed)
        return 0
    if a.cmd == "fit":
        src = a.args[0]
        if os.path.exists(src):
            with open(src, encoding="utf-8") as fh:
                draft = json.load(fh)
            (key, s), = draft.items()
        else:
            key, s = src, (defs["sets"].get(src) or defs["examples"][src])
        for _, _, p in cs.pigments(s):
            p.setdefault("swatch", "#000000")
        out, errs = fit(s, defs, a.iters, a.seed, restarts=a.restarts)
        text = json.dumps({key: out}, indent=2)
        if a.out:
            with open(a.out, "w", encoding="utf-8") as fh:
                fh.write(text + "\n")
        print(text)
        for e in errs:
            print("STILL BROKEN:", e, file=sys.stderr)
        return 1 if errs else 0
    if a.cmd == "hero":
        k = a.args[0]
        if os.path.exists(k):
            with open(k, encoding="utf-8") as fh:
                (_, s), = json.load(fh).items()
        else:
            s = None if k == "photic" else (defs["sets"].get(k) or defs["examples"].get(k))
        print(hero(s, a.out or "hero.jpg"))
        return 0
    if a.cmd == "preview":
        sets = []
        for k in a.args:
            if k == "photic":
                sets.append(("Photic (the asset)", None))
            else:
                s = defs["sets"].get(k) or defs["examples"].get(k)
                sets.append((s["name"], s))
        for path in a.draft:
            with open(path, encoding="utf-8") as fh:
                (key, s), = json.load(fh).items()
            sets.append((s.get("name", key) + " (draft)", s))
        print(preview(sets, a.out or "colour_sets_preview.png"))
        return 0
    ap.print_help()
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
