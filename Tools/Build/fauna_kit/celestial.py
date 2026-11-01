#!/usr/bin/env python3
"""The celestial laws of a fauna body, checked on its SPEC before anything is built.

    python3 Tools/Build/fauna_kit/celestial.py bee           # check Tools/Build/fauna_kit/specs/bee.py
    python3 Tools/Build/fauna_kit/celestial.py --all         # every spec
    python3 Tools/Build/fauna_kit/celestial.py --self-test   # every law fires on a planted defect

A Cosmic Shore creature is a celestial, Etherion-based lifeform, not an Earth animal (Garrett, 2026-11-01: "fauna
have rules like bodies made of spindles that are wireframe inspired structures suitable for the space element to make
thinner. there must be a long skinny danger prism as a stinger. other health prisms around its body. it isn't actually
a bee. it is a celestial etherion based life form. it doesn't really have a sense of up"). The /fauna-creation skill
holds the laws and where each comes from; this file is the part of it that can say no.

A spec is a Python module with SPEC = {...} (see specs/bee.py for the schema). Coordinates are world units at a root
scale of 1, the heart (the Etherion crystal, 3.55 across) at the origin, the creature travelling along +z. The checker
is a READER: it writes nothing.
"""
import importlib.util
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SPECS = os.path.join(HERE, "specs")

HEART_WORLD = 3.55          # ElementalCrystalSetSO.CrystalWorldScale: every heart is one size (ECOSYSTEM.md section 64)
HEART_CLEAR = 2.55          # a heart's clearance to prism boxes (ECOSYSTEM_DESIGN_PRINCIPLES, FL section 5.1)
MASS_SWING = 12.44          # ElementalArmour.HeartSwingClearance: a Mass heart's swing to every plain prism's shield
SPACE_OVER_MASS = 1.6477    # FaunaElementalForm.SpaceLengthOverMass
PRISM_MIN, PRISM_MAX = 0.5, 10.0   # PrismScaleAnimator's per-axis window; outside it a prism needs SeatAuthoredSize
STINGER_ASPECT = 6.0        # "long skinny": the long axis at least this many times the thickest cross axis
STINGER_MIN_LENGTH = 4.0
ROOT_REACH = 1.5            # a spindle's root must start within this of its parent's segment (or of the heart)
PRISM_REACH = 2.5           # a body prism's nearest point must lie within this of its own spindle's centreline
TERRESTRIAL = ("leg", "belly", "back", "dorsal", "ventral", "eye", "antenna", "foot", "feet", "head", "torso",
               "thorax", "abdomen", "wing")   # Earth anatomy; a celestial body names its parts by what they are
ELEMENTS = ("Charge", "Mass", "Space", "Time")


# ------------------------------------------------------------------ geometry

def sub(a, b): return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]
def add(a, b): return [a[0] + b[0], a[1] + b[1], a[2] + b[2]]
def mul(a, s): return [a[0] * s, a[1] * s, a[2] * s]
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]
def norm(a): return math.sqrt(dot(a, a))
def unit(a):
    n = norm(a)
    return [a[0] / n, a[1] / n, a[2] / n] if n > 1e-9 else [0.0, 0.0, 1.0]


def seg_dist(p, a, b):
    """Distance from p to the segment a-b."""
    ab = sub(b, a)
    t = max(0.0, min(1.0, dot(sub(p, a), ab) / max(dot(ab, ab), 1e-12)))
    return norm(sub(p, add(a, mul(ab, t))))


def frame(axis):
    """An orthonormal (right, up, forward) with forward = axis. The up is ARBITRARY: a celestial body has none."""
    f = unit(axis)
    helper = [0.0, 1.0, 0.0] if abs(f[1]) < 0.9 else [1.0, 0.0, 0.0]
    r = unit(cross(helper, f))
    u = cross(f, r)
    return r, u, f


def box_axes(p):
    r, u, f = frame(p["axis"])
    half = [p["size"][0] / 2, p["size"][1] / 2, p["size"][2] / 2]
    return [r, u, f], half


def obb_overlap(p, q, slack=0.0):
    """Separating-axis test for two prisms' oriented boxes (each sized along its own frame)."""
    A, ha = box_axes(p)
    B, hb = box_axes(q)
    t = sub(q["at"], p["at"])
    axes = A + B + [cross(a, b) for a in A for b in B]
    for L in axes:
        if norm(L) < 1e-9:
            continue
        L = unit(L)
        ra = sum(ha[i] * abs(dot(A[i], L)) for i in range(3))
        rb = sum(hb[i] * abs(dot(B[i], L)) for i in range(3))
        if abs(dot(t, L)) > ra + rb - slack:
            return False
    return True


def box_corners(p):
    A, h = box_axes(p)
    out = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                out.append(add(p["at"], add(mul(A[0], sx * h[0]), add(mul(A[1], sy * h[1]), mul(A[2], sz * h[2])))))
    return out


def box_point_dist(p, x):
    """Distance from point x to the prism's box (0 inside)."""
    A, h = box_axes(p)
    d = sub(x, p["at"])
    acc = 0.0
    for i in range(3):
        c = dot(d, A[i])
        e = max(0.0, abs(c) - h[i])
        acc += e * e
    return math.sqrt(acc)


def rot_z(v, ang):
    c, s = math.cos(ang), math.sin(ang)
    return [c * v[0] - s * v[1], s * v[0] + c * v[1], v[2]]


# ------------------------------------------------------------------ the laws

def check(spec):
    """Every law the spec breaks, as plain sentences (empty = celestial)."""
    errs = []
    want = lambda cond, msg: (None if cond else errs.append(msg))
    spindles = {s["name"]: s for s in spec.get("spindles", [])}
    prisms = spec.get("prisms", [])
    heart_r = HEART_WORLD / 2

    # L1 the Etherion heart leads; the body trails behind it
    for s in spindles.values():
        want(max(s["from"][2], s["to"][2]) <= 0.0 + 1e-6,
             f"L1 spindle '{s['name']}' reaches ahead of the heart (z > 0): the Etherion leads and the body trails")
    for p in prisms:
        ahead = max(c[2] for c in box_corners(p))
        want(ahead <= -heart_r, f"L1 prism '{p['name']}' reaches ahead of the heart's back face: the body trails the heart")
        want(box_point_dist(p, [0, 0, 0]) >= heart_r + HEART_CLEAR - 1e-6,
             f"L1 prism '{p['name']}' is within {heart_r + HEART_CLEAR:.2f} u of the heart's centre (radius + {HEART_CLEAR} clearance)")

    # L2 the body is a wireframe of spindles, every one with a spindle path back to the heart
    want(len(spindles) >= 3, f"L2 the body has {len(spindles)} spindles: a celestial body is a wireframe of spindles (3 at least)")
    for s in spindles.values():
        want(s.get("mesh", "braid") in ("braid",), f"L2 spindle '{s['name']}' is not the braid: every living limb is the braid spindle")
        par = s.get("parent")
        if par is None:
            want(norm(s["from"]) <= heart_r + ROOT_REACH,
                 f"L2 root spindle '{s['name']}' starts {norm(s['from']):.2f} u from the heart: a root grows from the Etherion")
            continue
        if par not in spindles:
            errs.append(f"L2 spindle '{s['name']}' names a parent '{par}' that does not exist")
            continue
        P = spindles[par]
        want(seg_dist(s["from"], P["from"], P["to"]) <= ROOT_REACH,
             f"L2 spindle '{s['name']}' does not start on its parent '{par}': every limb has a spindle path to the heart")
    seen = set()
    for s in spindles.values():   # no cycles
        chain, cur = set(), s["name"]
        while cur is not None and cur in spindles:
            if cur in chain:
                errs.append(f"L2 spindle '{s['name']}' sits in a parent cycle")
                break
            chain.add(cur)
            cur = spindles[cur].get("parent")
        seen |= chain
    want(not any(s.get("solid_skin") for s in spindles.values()) and not spec.get("model"),
         "L2 the body carries a solid model skin: a celestial body is spindles and prisms, not a sculpted mesh")

    # L3 every prism rides its own spindle (a DIRECT child, so it sways with the limb)
    for p in prisms:
        s = spindles.get(p.get("spindle"))
        if not s:
            errs.append(f"L3 prism '{p['name']}' names no spindle: a body prism is a direct child of the limb it rides")
            continue
        near = min(seg_dist(c, s["from"], s["to"]) for c in box_corners(p) + [p["at"]])
        want(near <= PRISM_REACH, f"L3 prism '{p['name']}' is {near:.2f} u from its spindle '{s['name']}': it must sit on its limb")

    # L4 health prisms around the body
    health = [p for p in prisms if p.get("kind") == "health"]
    want(len(health) >= 3, f"L4 {len(health)} health prisms: a body needs health prisms around it (3 at least) - its conserved mass and its killable surface")

    # L5 the stinger: one long skinny authored danger prism at the trailing extremity, on a terminal limb
    danger = [p for p in prisms if p.get("kind") == "danger"]
    stingers = [p for p in danger if p.get("role") == "stinger"]
    want(len(stingers) >= 1, "L5 no stinger: a celestial lifeform carries a long skinny danger prism as its stinger (role 'stinger')")
    tail_z = min(min(c[2] for c in box_corners(p)) for p in prisms) if prisms else 0.0
    for st in stingers:
        long_axis = st["size"][2]
        thick = max(st["size"][0], st["size"][1])
        want(long_axis >= STINGER_MIN_LENGTH and long_axis >= STINGER_ASPECT * thick,
             f"L5 stinger '{st['name']}' is {st['size']}: long and skinny means length >= {STINGER_MIN_LENGTH} and >= {STINGER_ASPECT}x its thickness")
        want(abs(min(c[2] for c in box_corners(st)) - tail_z) <= 1e-6 or st.get("weapon_at") == "front",
             f"L5 stinger '{st['name']}' is not the trailing extremity (the body's last point)")
        lim = spindles.get(st.get("spindle"))
        if lim:
            kids = [s for s in spindles.values() if s.get("parent") == lim["name"]]
            want(not kids, f"L5 stinger '{st['name']}' rides '{lim['name']}', which has child limbs: a stinger is on a terminal limb")
    for p in prisms:   # the scale animator's window
        out = [v for v in p["size"] if v < PRISM_MIN or v > PRISM_MAX]
        want(not out or p.get("seat_authored"),
             f"L5 prism '{p['name']}' has an axis outside [{PRISM_MIN}, {PRISM_MAX}] {p['size']}: set seat_authored (Fauna.SeatAuthoredSize) or resize")
    want(spec.get("mouth") in ("none", "stinger", "front"),
         "L5 'mouth' is not declared: LightFauna puts the mouth at the authored-danger centroid, so a stinger-only body says where it feeds (none / stinger / front)")

    # L6 no sense of up: the body is n-fold symmetric about its travel axis, n >= 3
    n = spec.get("symmetry", 0)
    want(n >= 3, f"L6 symmetry {n}: a celestial body has no up, so it repeats round its travel axis at least 3 times")
    if n >= 3:
        ang = 2 * math.pi / n
        pts = [("s", s["from"]) for s in spindles.values()] + [("s", s["to"]) for s in spindles.values()] + \
              [(p.get("kind"), p["at"]) for p in prisms]
        for kind, x in pts:
            y = rot_z(x, ang)
            match = any(k == kind and norm(sub(y, z)) <= 0.05 for k, z in pts)
            want(match, f"L6 {kind} point {[round(v, 2) for v in x]} has no partner {360 / n:.0f} degrees round the axis: the body must not have an up")
    want(spec.get("steering_up") == "carried",
         "L6 steering_up is not 'carried': the species steers with a carried (parallel-transported) up, never Vector3.up")

    # L7 prisms never interpenetrate (spindles may pass through anything)
    for i in range(len(prisms)):
        for j in range(i + 1, len(prisms)):
            want(not obb_overlap(prisms[i], prisms[j], slack=1e-4),
                 f"L7 prisms '{prisms[i]['name']}' and '{prisms[j]['name']}' interpenetrate: fit them flush (shrink minimally, keep the count)")

    # L8 the element law is written down for every element
    el = spec.get("elements", {})
    for e in ELEMENTS:
        want(e in el, f"L8 the element table has no {e} row: every species states its four forms")
    if "Space" in el:
        want(el["Space"].get("erode_spindle") is True and abs(el["Space"].get("span_over_mass", 0) - SPACE_OVER_MASS) < 1e-3,
             f"L8 Space must erode its spindles and out-span Mass by {SPACE_OVER_MASS} (FaunaElementalForm)")
        want(el["Space"].get("stinger_thins") in (True, False),
             "L8 Space: say whether the stinger thins (Fauna.ErodeSpindle skips authored danger today)")
    if "Mass" in el:
        plain = [p for p in prisms if p.get("kind") == "health"]
        need = max((MASS_SWING / max(box_point_dist(p, [0, 0, 0]) + 1e-9, 1e-9)) for p in plain) if plain else 1.0
        want(el["Mass"].get("body_scale", 0) >= need - 1e-3,
             f"L8 Mass body_scale {el['Mass'].get('body_scale')} is under {need:.3f}: a Mass heart's swing needs {MASS_SWING} u to every shielded prism")
    if "Charge" in el:
        want(el["Charge"].get("all_danger") is True, "L8 Charge: every body prism is danger (Fauna.ArmourBody)")

    # L9 names: no Earth anatomy
    for thing in list(spindles.values()) + prisms:
        low = thing["name"].lower()
        bad = [w for w in TERRESTRIAL if w in low]
        if bad:
            errs.append(f"L9 part '{thing['name']}' is named for Earth anatomy ({bad[0]}): name it for what it is in the HyperSea")

    # L10 the premise and the role are written
    want(bool(spec.get("premise")), "L10 no premise: say what this lifeform is and the feeling it is for")
    want(spec.get("role") in ("herbivore", "predator", "pollinator", "omnivore"), "L10 no role (herbivore / predator / pollinator / omnivore)")
    return errs


# ------------------------------------------------------------------ loading and the self-test

def load(name):
    path = os.path.join(SPECS, name + ".py")
    sp = importlib.util.spec_from_file_location("fauna_spec_" + name, path)
    mod = importlib.util.module_from_spec(sp)
    sp.loader.exec_module(mod)
    return mod.SPEC


def self_test():
    import copy
    base = load("bee")
    fails = []
    if check(base):
        fails.append("the bee spec itself fails: " + "; ".join(check(base)))

    def planted(label, mutate, law):
        s = copy.deepcopy(base)
        mutate(s)
        got = check(s)
        if not any(e.startswith(law) for e in got):
            fails.append(f"planted '{label}' was not caught by {law} (got: {got[:2]})")

    def add_leg(s):
        s["prisms"].append({"name": "Strut", "spindle": s["spindles"][0]["name"], "kind": "health",
                            "at": [0.0, -1.8, -9.0], "size": [0.5, 2.0, 0.5], "axis": [0, 0, 1]})
    planted("a one-sided part (an up)", add_leg, "L6")
    planted("a stubby stinger", lambda s: [p.update(size=[1.5, 1.5, 3.0]) for p in s["prisms"] if p.get("role") == "stinger"], "L5")
    planted("no stinger", lambda s: s.update(prisms=[p for p in s["prisms"] if p.get("role") != "stinger"]), "L5")
    planted("a prism off its limb", lambda s: [p.update(spindle="Vane1") for p in s["prisms"] if p["name"] == "VanePlate0"], "L3")
    planted("the body ahead of the heart", lambda s: s["spindles"][0].update(to=[0.0, 0.0, 4.0]), "L1")
    planted("a solid model skin", lambda s: s.update(model="Bee.fbx"), "L2")
    planted("steering with world up", lambda s: s.update(steering_up="world"), "L6")
    planted("Space not eroded", lambda s: s["elements"]["Space"].update(erode_spindle=False), "L8")
    planted("a leg by name", lambda s: s["spindles"][1].update(name="Leg1") or [p.update(spindle="Leg1") for p in s["prisms"] if p["spindle"] == base["spindles"][1]["name"]], "L9")

    def overlap(s):
        h = [p for p in s["prisms"] if p.get("kind") == "health"][0]
        s["prisms"].append(dict(h, name=h["name"] + "Twin", at=list(h["at"])))
    planted("two prisms in one place", overlap, "L7")
    planted("a limb floating free", lambda s: s["spindles"][-1].update(**{"from": [0.0, 0.0, -40.0], "to": [0.0, 0.0, -42.0]}), "L2")
    return fails


def main(argv):
    if "--self-test" in argv:
        fails = self_test()
        for f in fails:
            print("FAIL:", f)
        print("celestial self-test:", "ok" if not fails else f"{len(fails)} problem(s)")
        return 1 if fails else 0
    names = sorted(f[:-3] for f in os.listdir(SPECS) if f.endswith(".py") and not f.startswith("_")) if "--all" in argv \
        else [a for a in argv if not a.startswith("-")]
    if not names:
        print(__doc__)
        return 2
    bad = 0
    for name in names:
        errs = check(load(name))
        for e in errs:
            print(f"FAIL {name}: {e}")
        print(f"{name}: {'celestial' if not errs else str(len(errs)) + ' law(s) broken'}")
        bad += bool(errs)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
