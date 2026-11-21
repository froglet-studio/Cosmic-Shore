"""BIOFORMS: new animated bodies a swarm can switch into, found by quality-diversity search.

A swarm (Docs/SWARM_FAUNA.md) is tadpoles swimming as one animal, and it can switch between body PLANS - the four
elemental ones, and Tandava's scripted forms (SwarmFaunaConfigSO.ScriptedPlans, SwarmPlanLibrary.LoadScripted). Every
plan so far was designed by hand. This evaluator turns a GENOME of ~20 numbers into a plan in the exact schema the game
loads (tandava_plans.bake's: n, frames, frameSteps, order, elem, tier, half, pos, face, slot, halfF, tierF, sp, ...),
so MAP-Elites can fill a grid of DIFFERENT good bodies instead of polishing one:

    the genome   a spine (length, taper, arch), a swim (lateral / vertical / helical wave, or a pulse), up to six
                 limb pairs (length, reach, angle, paddle phase), up to ten radial arms off the head ring, a dorsal
                 crest, and which element each region is made of
    the grid     elongation (6 bins) x share of the body in limbs and arms (5 bins) x majority element (4 bins)
    the loss     what a swarm needs from a body (lower is better):
                   size        a member count the cell can afford (60-400 before the config's density)
                   integrity   the share of the drawn body kept after the spacing thinning (MIN_GAP in every frame,
                               the sort core's collision radius) - a body that folds into itself loses its shape
                   one animal  no member further than 2.5 x SPACING from its nearest neighbour in any frame
                   keepable    no member moves more than MAX_STEP voxels between frames (the swimmers must keep up)
                   alive       mean motion inside [LIVELY_LO, LIVELY_HI]: neither a statue nor a blur
                   loops       the last frame returns to the first like any other step

Every elite is VALID by construction (tandava_plans' rules: the identity clamp, MIN_GAP, the census majority, both
regions). `export()` bakes chosen elites as SwarmPlan_forge_<cell>.json for a session to wire in (/bioform-forge).
"""
import math
import os

VERSION = "1"
PATHS = ["Tools/Build/"]
FRAMES = 8
MIN_GAP = 2.1
SPACING = 2.65
MAX_STEP = 5.0
LIVELY_LO, LIVELY_HI = 0.6, 3.0
N_LO, N_HI = 60, 400
LIVELY_AIM, N_AIM = 1.2, 160   # the shipped whale's motion per frame and member count, roughly
CHARGE, MASS, SPACE, TIME = 0, 1, 2, 3
LOOK = {MASS: ([1.0, 0.8, 0.8], (0.4, 0.0)), SPACE: ([2.2, 0.38, 0.38], (0.6, 0.1)),
        TIME: ([0.9, 0.45, 0.4], (0.3, 0.0)), CHARGE: ([0.45, 1.2, 1.1], (0.5, 0.0))}
SWIMS = ("lateral", "vertical", "helical", "pulse")
_ID = {}


def inputs(args):
    return ["Tools/Build/swarm_plans.py", "Tools/Build/tandava_plans.py"]


def _identity(elem, half):
    if "f" not in _ID:
        import swarm_plans
        _ID["f"] = swarm_plans.identity
    return _ID["f"](elem, half, 0)[0]


# ── vectors ──────────────────────────────────────────────────────────────────

def add(a, b): return [a[0] + b[0], a[1] + b[1], a[2] + b[2]]
def sub(a, b): return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]
def mul(a, s): return [a[0] * s, a[1] * s, a[2] * s]
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]
def norm(a): return math.sqrt(dot(a, a))


def unit(a):
    n = norm(a)
    return [a[0] / n, a[1] / n, a[2] / n] if n > 1e-9 else [1.0, 0.0, 0.0]


# ── genome -> body ───────────────────────────────────────────────────────────

def _spine(g, s, frame):
    """Point and frame (tangent, side, up) of the spine at arc share s in [0, 1] (0 = head), in this animation frame.
    The body swims along +x; the wave travels head to tail, one whole number of cycles per loop so frames close."""
    L = g["length"]
    x = (0.5 - s) * L
    arch = g["arch"] * L * (s - 0.5) ** 2 * 2
    ph = 2 * math.pi * frame / FRAMES
    amp = g["wave_amp"] * L * (0.25 + 0.75 * s)          # the tail swings most
    k = 2 * math.pi * g["wave_count"]
    swim = g["swim"]
    w = amp * math.sin(k * s - ph) if swim != "pulse" else 0.0
    w2 = amp * math.cos(k * s - ph) if swim == "helical" else 0.0
    if swim == "lateral":
        p = [x, arch, w]
    elif swim == "vertical":
        p = [x, arch + w, 0.0]
    elif swim == "helical":
        p = [x, arch + w2 * 0.7, w * 0.7]
    else:
        p = [x, arch, 0.0]
    return p


def _frame_at(g, s, f, eps=0.01):
    a, b = _spine(g, max(0.0, s - eps), f), _spine(g, min(1.0, s + eps), f)
    t = unit(sub(a, b))                                    # toward the head
    side = unit(cross([0.0, 1.0, 0.0], t))
    up = cross(t, side)
    return t, side, up


def _radius(g, s, f):
    r = g["radius"] * (1.0 - g["taper"] * s) * (0.35 + 0.65 * math.sin(math.pi * min(1.0, s * 1.15 + 0.08)))
    if g["swim"] == "pulse":
        r *= 1.0 + g["wave_amp"] * 1.5 * math.sin(2 * math.pi * f / FRAMES + 2 * s)
    return max(0.6, r)


class U:
    __slots__ = ("elem", "slot", "pos", "face", "part")

    def __init__(self, elem, slot, part):
        self.elem, self.slot, self.part = elem, slot, part
        self.pos, self.face = [None] * FRAMES, [None] * FRAMES


def build(g):
    """The genome's body as plan units, before thinning."""
    units = []
    L = g["length"]
    rings = max(3, int(L / SPACING))
    for ri in range(rings):
        s = ri / (rings - 1)
        r0 = _radius(g, s, 0)
        per = max(1, min(int(g["ring_units"]), int(2 * math.pi * r0 / SPACING) + 1))
        for q in range(per):
            a0 = 2 * math.pi * q / per + (ri % 2) * math.pi / per
            elem = g["head_elem"] if s < 0.2 else g["body_elem"]
            u = U(elem, 0 if math.sin(a0) >= 0 else 1, "body")
            for f in range(FRAMES):
                c = _spine(g, s, f)
                t, side, up = _frame_at(g, s, f)
                r = _radius(g, s, f)
                u.pos[f] = add(c, add(mul(side, r * math.cos(a0)), mul(up, r * math.sin(a0))))
                u.face[f] = t
            units.append(u)
    # dorsal crest
    if g["crest"] > 0.5:
        n_c = int(L * 0.5 / SPACING)
        for k in range(n_c):
            s = 0.2 + 0.5 * k / max(1, n_c - 1)
            for lift in range(1, int(g["crest"] / SPACING) + 1):
                u = U(g["limb_elem"], 0, "crest")
                for f in range(FRAMES):
                    c = _spine(g, s, f)
                    t, side, up = _frame_at(g, s, f)
                    u.pos[f] = add(c, mul(up, _radius(g, s, f) + lift * SPACING))
                    u.face[f] = up
                units.append(u)
    # limb pairs: chains out from the flank, paddling with a phase lag head to tail
    pairs = int(g["limb_pairs"])
    for pi_ in range(pairs):
        s = g["limb_start"] + g["limb_spread"] * (pi_ / max(1, pairs - 1) if pairs > 1 else 0.5)
        s = min(0.95, s)
        links = max(1, int(g["limb_len"] / SPACING))
        for sign in (1, -1):
            for k in range(1, links + 1):
                u = U(g["limb_elem"], 1, "limb")
                for f in range(FRAMES):
                    c = _spine(g, s, f)
                    t, side, up = _frame_at(g, s, f)
                    sweep = math.radians(g["limb_sweep"]) * math.sin(2 * math.pi * f / FRAMES - g["limb_lag"] * 2 * math.pi * pi_ / max(1, pairs))
                    ang = math.radians(g["limb_angle"])
                    d = add(mul(side, sign * math.sin(ang) * math.cos(sweep)), add(mul(t, math.sin(sweep)), mul(up, -math.cos(ang))))
                    d = unit(d)
                    base = add(c, mul(side, sign * _radius(g, s, f)))
                    u.pos[f] = add(base, mul(d, k * SPACING))
                    u.face[f] = d
                units.append(u)
    # radial arms off the head ring (tentacles / necks)
    arms = int(g["arms"])
    for ai in range(arms):
        a0 = 2 * math.pi * ai / max(1, arms)
        links = max(1, int(g["arm_len"] / SPACING))
        for k in range(1, links + 1):
            u = U(g["limb_elem"], 0 if math.sin(a0) >= 0 else 1, "arm")
            for f in range(FRAMES):
                c = _spine(g, 0.0, f)
                t, side, up = _frame_at(g, 0.02, f)
                wav = 0.35 * math.sin(2 * math.pi * f / FRAMES - 0.6 * k + a0)
                out = unit(add(mul(add(mul(side, math.cos(a0)), mul(up, math.sin(a0))), g["arm_flare"]), mul(t, 1.0 - g["arm_flare"])))
                bend = unit(add(out, mul(add(mul(side, -math.sin(a0)), mul(up, math.cos(a0))), wav)))
                u.pos[f] = add(c, mul(bend, _radius(g, 0.0, f) + k * SPACING))
                u.face[f] = bend
            units.append(u)
    return units


def _keep(units):
    kept = []
    for i, u in enumerate(units):
        ok = True
        for j in kept:
            v = units[j]
            for f in range(FRAMES):
                d = sub(u.pos[f], v.pos[f])
                if d[0] * d[0] + d[1] * d[1] + d[2] * d[2] < (MIN_GAP + 0.01) ** 2:
                    ok = False
                    break
            if not ok:
                break
        if ok:
            kept.append(i)
    return [units[i] for i in kept]


def decode(params):
    g = dict(params)
    for k in ("head_elem", "body_elem", "limb_elem"):
        g[k] = int(g[k])
    return g


# ── the loss ─────────────────────────────────────────────────────────────────

def measure(units, drawn):
    n = len(units)
    out = {"n": n, "integrity": n / max(1, drawn)}
    if n < 2:
        return out
    worst_island, steps, lives = 0.0, [], []
    for f in range(FRAMES):
        P = [u.pos[f] for u in units]
        for i in range(n):
            best = 1e18
            pi = P[i]
            for j in range(n):
                if i != j:
                    d = sub(pi, P[j])
                    dd = d[0] * d[0] + d[1] * d[1] + d[2] * d[2]
                    if dd < best:
                        best = dd
            worst_island = max(worst_island, math.sqrt(best))
    for u in units:
        st = [norm(sub(u.pos[(f + 1) % FRAMES], u.pos[f])) for f in range(FRAMES)]
        steps.append(max(st))
        lives.append(sum(st) / FRAMES)
    out["island"] = worst_island
    out["max_step"] = max(steps)
    out["lively"] = sum(lives) / n
    P0 = [u.pos[0] for u in units]
    c = [sum(p[a] for p in P0) / n for a in range(3)]
    ext = [max(p[a] for p in P0) - min(p[a] for p in P0) for a in range(3)]
    ext.sort(reverse=True)
    out["elongation"] = ext[0] / max(1.0, (ext[1] + ext[2]) / 2)
    out["appendage_share"] = sum(1 for u in units if u.part in ("limb", "arm")) / n
    census = [0, 0, 0, 0]
    for u in units:
        census[u.elem] += 1
    out["census"] = census
    out["major"] = max(range(4), key=lambda e: census[e])
    out["centroid"] = c
    return out


def evaluate(params, ctx=None):
    g = decode(params)
    drawn = build(g)
    units = _keep(drawn)
    m = measure(units, len(drawn))
    if m["n"] < 2:
        return {"loss": float("inf"), "valid": False}
    loss = 0.0
    loss += 0.004 * max(0, N_LO - m["n"]) + 0.004 * max(0, m["n"] - N_HI)
    loss += 1.0 - m["integrity"]
    loss += 0.5 * max(0.0, m["island"] - 2.5 * SPACING)
    loss += 0.5 * max(0.0, m["max_step"] - MAX_STEP)
    loss += 0.5 * max(0.0, LIVELY_LO - m["lively"]) + 0.3 * max(0.0, m["lively"] - LIVELY_HI)
    # Inside the hard bands, a gradient toward the swarm's sweet spot, so elites keep improving once every cell is legal:
    # a body that moves about as much as the shipped animals (LIVELY_AIM) at about a whale's member count (N_AIM).
    loss += 0.1 * abs(math.log(max(m["lively"], 1e-3) / LIVELY_AIM)) + 0.05 * abs(math.log(m["n"] / N_AIM))
    slots = {u.slot for u in units}
    valid = slots == {0, 1} and N_LO // 2 <= m["n"] <= N_HI * 2
    return {"loss": round(loss, 5), "valid": valid,
            "descriptors": {"elongation": m["elongation"], "appendages": m["appendage_share"], "major": m["major"]},
            "metrics": {k: (round(v, 3) if isinstance(v, float) else v) for k, v in m.items() if k != "centroid"}}


def bake(params, kind):
    """One genome as a SwarmPlanJson plan (the schema tandava_plans.bake writes)."""
    g = decode(params)
    units = _keep(build(g))
    n = len(units)
    c = [sum(u.pos[0][a] for u in units) / n for a in range(3)]
    r = lambda x: round(float(x), 3)
    pos, face, half_f, tier_f, sp_f, half0 = [], [], [], [], [], []
    for f in range(FRAMES):
        for u in units:
            pos.extend(r(u.pos[f][a] - c[a]) for a in range(3))
            face.extend(r(x) for x in unit(u.face[f]))
            half_f.extend(r(x) for x in _identity(u.elem, LOOK[u.elem][0]))
            tier_f.append(0)
            sp_f.extend((r(LOOK[u.elem][1][0]), r(LOOK[u.elem][1][1])))
    for u in units:
        half0.extend(r(x) for x in _identity(u.elem, LOOK[u.elem][0]))
    census = [0, 0, 0, 0]
    for u in units:
        census[u.elem] += 1
    return {"kind": kind, "name": kind, "form": 0, "major": max(range(4), key=lambda e: census[e]), "n": n,
            "frames": FRAMES, "frameSteps": 6, "order": list(range(FRAMES)), "elem": [u.elem for u in units],
            "tier": [0] * n, "half": half0, "pos": pos, "face": face, "swimAxis": [1, 0, 0], "upAxis": [0, 1, 0],
            "slot": [u.slot for u in units], "halfF": half_f, "tierF": tier_f, "sp": sp_f, "centroid": [r(x) for x in c]}


def validate_plan(p):
    """tandava_plans.validate's single-plan promises (no form ladder: a forge plan is one animal)."""
    errors = []
    n = p["n"]
    census = [0, 0, 0, 0]
    for e in p["elem"]:
        census[e] += 1
    if max(range(4), key=lambda e: census[e]) != p["major"]:
        errors.append("major is not the census majority")
    for f in range(FRAMES):
        P = [p["pos"][3 * (f * n + i):3 * (f * n + i) + 3] for i in range(n)]
        for i in range(n):
            for j in range(i + 1, n):
                if norm(sub(P[i], P[j])) < MIN_GAP:
                    errors.append(f"frame {f}: units {i} and {j} closer than {MIN_GAP}")
                    break
            if errors:
                break
    if set(p["slot"]) != {0, 1}:
        errors.append("needs both regions")
    if len(p["pos"]) != 3 * FRAMES * n or len(p["sp"]) != 2 * FRAMES * n:
        errors.append("frame arrays are the wrong length")
    return errors


def self_test():
    """A whale-like genome builds a valid plan; a degenerate genome (all limbs folded onto the spine) scores worse."""
    import sys
    here = os.path.dirname(os.path.abspath(__file__))
    sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(here)), "Build"))
    whale = {"length": 40, "radius": 5, "taper": 0.6, "arch": 0.05, "ring_units": 7, "swim": "vertical", "wave_amp": 0.08,
             "wave_count": 1, "limb_pairs": 1, "limb_len": 8, "limb_start": 0.3, "limb_spread": 0.0, "limb_angle": 80,
             "limb_sweep": 25, "limb_lag": 0.3, "arms": 0, "arm_len": 6, "arm_flare": 0.6, "crest": 0,
             "head_elem": MASS, "body_elem": MASS, "limb_elem": CHARGE}
    bad = []
    r = evaluate(whale)
    if not r["valid"]:
        bad.append(f"bioform_forms: the whale genome is invalid {r['metrics']}")
    p = bake(whale, "forge_selftest")
    errs = validate_plan(p)
    if errs:
        bad.append("bioform_forms: baked plan fails: " + "; ".join(errs[:3]))
    folded = dict(whale, limb_angle=180, limb_len=24, limb_pairs=6, limb_sweep=0, limb_spread=0.6)
    if evaluate(folded)["loss"] <= r["loss"]:
        bad.append("negative control: limbs folded into the body did not score worse")
    return bad


def export(cells, out_dir, prefix="forge"):
    """Bake chosen archive cells ({cell: {params}}) to out_dir/SwarmPlan_<prefix>_<cell>.json; returns the paths."""
    import json
    os.makedirs(out_dir, exist_ok=True)
    paths = []
    for cell, e in cells.items():
        kind = f"{prefix}_{cell.replace(',', '_')}"
        plan = bake(e["params"], kind)
        errs = validate_plan(plan)
        if errs:
            raise ValueError(f"{kind}: {errs[:3]}")
        path = os.path.join(out_dir, f"SwarmPlan_{kind}.json")
        with open(path, "w", encoding="utf-8") as fh:
            json.dump(plan, fh, separators=(",", ":"))
        paths.append(path)
        nca = os.path.join(out_dir, f"{kind}.prisms.json")      # the same body as an NCA training target
        with open(nca, "w", encoding="utf-8") as fh:
            json.dump(to_prisms_json(plan), fh, separators=(",", ":"))
        paths.append(nca)
    return paths


# ── a forged body as an NCA training target ──────────────────────────────────
# nca3d.py (research branch cece/gifted-curie-x2cpd0, Tools/NCA) trains a 3D neural cellular automaton on any PRISM
# target: whale_target.py's prisms.json - per frame a list of {p, h, R, dom, tier}, R row-major with the prism's axes as
# its COLUMNS, +x head, +y up. The colour comes from `dom` (0 Jade = the Space colour, 1 Ruby = Mass, 2 Gold = Charge;
# nca3d has no Time colour, so Time prisms wear Jade). A trained network is one more creature that grows from a seed,
# swims with no animation and heals (Docs/NCA_CREATURES.md).

DOM_OF = {SPACE: 0, MASS: 1, CHARGE: 2, TIME: 0}


def to_prisms_json(plan):
    n = plan["n"]
    frames = []
    for f in range(plan["frames"]):
        fr = []
        for i in range(n):
            p = plan["pos"][3 * (f * n + i):3 * (f * n + i) + 3]
            x = unit(plan["face"][3 * (f * n + i):3 * (f * n + i) + 3])
            y = cross([0.0, 0.0, 1.0], x) if abs(x[2]) < 0.9 else cross([1.0, 0.0, 0.0], x)
            y = unit(y)
            z = cross(x, y)
            h = plan["halfF"][3 * (f * n + i):3 * (f * n + i) + 3]
            R = [x[0], y[0], z[0], x[1], y[1], z[1], x[2], y[2], z[2]]
            fr.append({"p": [round(v, 4) for v in p], "h": h, "R": [round(v, 4) for v in R],
                       "dom": DOM_OF[plan["elem"][i]], "tier": 0, "part": "body"})
        frames.append(fr)
    return {"grid": None, "frames": frames, "report": {"source": "bioform_forms", "kind": plan["kind"], "n": n}}
