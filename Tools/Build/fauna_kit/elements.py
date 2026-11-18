#!/usr/bin/env python3
"""The four ELEMENT FORMS of a grown creature, made from its one spec.

    python3 Tools/Build/fauna_kit/elements.py            # every grown creature's four forms: mesh size against the bar
    python3 Tools/Build/fauna_kit/elements.py --check [name]   # exit 1 when a form of a creature that has its own forms is over the bar or breaks a law
    python3 Tools/Build/fauna_kit/elements.py --sheet <species> <png>     # the four forms side by side, at their sizes
    python3 Tools/Build/fauna_kit/elements.py --silhouette <species> [--form Space]   # law L12 for each form, or for one
    python3 Tools/Build/fauna_kit/elements.py --self-test

Garrett, 2026-11-17: "be sure that you create all four elements and follow the rules for doing so. when
differentiating by element really take things to the next level ... emotionally provocative lifeforms of the highest
fidelity that define our own unique whimsical style while remaining scalable and performant."

THREE LAYERS, each on top of the one before:

1. THE GAME'S LAW (Docs/ECOSYSTEM.md 64-65; celestial L8), unchanged. Time is the base body, 1.4 x faster. Charge
   makes every prism danger. Mass shields every plain prism, and its body is grown until the heart's swing clears the
   shields, never under the base body. Space is 1.6477 x Mass's span, its thick sections eroded, its prisms needles.
   The config and the runtime own these (BaseBodyScale, Fauna.ArmourBody); nothing here re-does them, with one
   exception: THE SPACE EROSION IS AUTHORED HERE for a jointed body. The runtime erodes each skin about its own spine,
   which tears a wing cut into jointed pieces (erosion.py measures it: 6.7 u on the Ray). So the Space form applies the
   law's own function (erosion.girth, the body's thickest girth as the reference) to every limb, the wire takes the
   law's 1/1.6477 (so a Space body's wire is no thicker in the world than the base body's), each prism becomes the
   law's needle in its socket (erosion.needle), and the prefab says `spindleAuthoredEroded`.

2. THE ELEMENT'S STYLE (RULES below), the same law carried into the WIRE so a form is not a recolour. It applies to
   every grown creature for nothing:
     Time    the quick one: the base wire, beating 1.4 x faster, its light running quicker.
     Charge  the dangerous beauty: the braid OPENS and winds tighter; limbs with no barbs grow short sparks.
     Mass    the gentle giant: one more strand, each much thicker and a full tube, wound slowly; a slow shallow beat.
     Space   the ghost: a strand fewer (never under two), ribbons, membranes woven sparser, a slow wide sway.

3. THE CREATURE'S OWN ANATOMY for an element (the spec's `forms` block), where a flagship is made. A form may add
   limbs and membranes, change a limb, and joint what it adds:
     "forms": {"Space": {"feeling": "...", "add": [limbs], "membranes": [...], "change": {"Lead0": {...}},
                         "unity": {"wires": {"VaneA": ["Veil0a", ...]}}}}
   What a form may NOT do (form() refuses each): touch the prisms (their count, seats and authored sizes are the
   species': an element changes what a prism IS by the game's law, never how many there are); drop or re-route a limb
   of the body's plan; change the body at all on Time (Time is the base body: it may only add); or strip more than
   half the barbs a limb of the body has (they are the species' look: a feather is not a whip).

A form is a SPEC (form(spec, element)), so everything that reads a spec reads a form: the laws, the mesh budget, the
lab, unity_port.py.
"""
import copy
import sys
import os

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import celestial as C  # noqa: E402
import erosion as E  # noqa: E402
import spindle as SP  # noqa: E402

ELEMENTS = ("Time", "Charge", "Mass", "Space")
# HOW A BODY ANSWERS ITS OWN TURNING (Garrett, 2026-11-18: "its rotations just happen ... i want the wings to take on
# chiral movements that match their rotations"). Four answers to the body's angular velocity, the same in the lab
# (FaunaLab.html stepTurn) and the game (GrownLimbDriver + SpindleGrownSway.hlsl); the two sides of a body always do
# mirrored things. A spec's own "turn" block overrides any of these; an element scales lag and spring (RULES).
#   lag     seconds of turning the tips trail behind the rotation (a spring: it trails, overshoots a little, settles)
#   stroke  how much harder the OUTSIDE of a turn beats than the inside, per radian a second
#   twist   the hind edges deflect like ailerons in a roll: radians at the tip per radian a second
#   bank    the body rolls into a turn: seconds of turning (0.45 = 26 degrees at one radian a second)
TURN = {"lag": 0.14, "max_lag": 0.32, "spring_hz": 1.3, "damping": 0.45, "stroke": 0.3, "max_stroke": 0.75,
        "twist": 0.12, "max_twist": 0.35, "bank": 0.45, "max_bank": 38.0}


def turn_of(spec, element=None):
    """The turn's numbers for a body (or one of its element forms): the defaults, the spec's own, the element's weight."""
    t = dict(TURN, **(spec.get("turn") or {}))
    if element:
        r = RULES[element]
        t["lag"], t["spring_hz"] = t["lag"] * r["turn_lag"], t["spring_hz"] * r["turn_hz"]
    return {k: round(v, 4) for k, v in t.items()}

SPARKS = {"count": 9, "length": [1.3, 0.5], "angle": 80.0, "sides": 2, "flutter": 0.9}
# strands: added (+) or removed (-); the rest multiply the design's own value. section None keeps the design's.
# Space's girth is not here: it is the law's erosion (erosion.girth), limb by limb. Its wire is the law's 1/1.6477.
RULES = {
    "Time":   {"strands": 0,  "wire": 1.0, "girth": 1.0,  "twist": 1.0, "amp": 1.0,  "hz": 1.4,  "across": 1.0, "glow_waves": 1.5, "bright": 1.0,  "section": None, "sparks": False, "turn_lag": 0.8, "turn_hz": 1.3},
    "Charge": {"strands": 0,  "wire": 0.9, "girth": 1.5,  "twist": 1.3, "amp": 1.1,  "hz": 1.15, "across": 1.0, "glow_waves": 2.5, "bright": 1.15, "section": None, "sparks": True,  "turn_lag": 0.9, "turn_hz": 1.5},
    "Mass":   {"strands": 1,  "wire": 1.7, "girth": 1.15, "twist": 0.7, "amp": 0.7,  "hz": 0.72, "across": 1.0, "glow_waves": 0.6, "bright": 1.0,  "section": 3,    "sparks": False, "turn_lag": 1.3, "turn_hz": 0.75},
    "Space":  {"strands": -1, "wire": E.THICKEST_GIRTH, "girth": 1.0, "twist": 1.0, "amp": 1.35, "hz": 0.8, "across": 0.6, "glow_waves": 1.0, "bright": 0.8, "section": 2, "sparks": False, "turn_lag": 1.5, "turn_hz": 0.7},
}


def _girth_at(limb, at):
    """A limb's girth at the point of it nearest `at` (its girth runs evenly from root to tip)."""
    import organic
    P = organic.centre(limb)
    i = min(range(len(P)), key=lambda k: C.norm(C.sub(P[k], at)))
    g0, g1 = limb["girth"]
    return g0 + (g1 - g0) * i / (len(P) - 1)


def form(spec, element, refit=True):
    """The spec of one element's form: the species' own anatomy for the element, the element's style, and at Space the
    law's erosion. `refit=False` keeps the prisms as the species authored them (what the celestial laws read; a needle
    is the game's law acting on a prism, not a design)."""
    r = RULES[element]
    out = copy.deepcopy(spec)
    out["element"] = element
    own = (spec.get("forms") or {}).get(element) or {}
    names = {s["name"] for s in out["spindles"]}
    who = f"{spec['species']} {element}"
    if "limbs" in own:
        raise SystemExit(f"{who}: a form's changes to the body's limbs are under \"change\" (\"limbs\" is the unity block's word)")
    if element == "Time" and own.get("change"):
        raise SystemExit(f"{who}: Time is the base body going faster. A Time form may ADD limbs; it may not change the body's own")
    for name, change in (own.get("change") or {}).items():
        if name not in names:
            raise SystemExit(f"{who}: the form changes a limb the body has not got: {name}")
        limb = next(s for s in out["spindles"] if s["name"] == name)
        if set(change) & {"path", "from", "to", "parent", "name", "mesh"}:
            raise SystemExit(f"{who}: the form re-routes {name}. A form changes a limb's WIRE (barbs, strands, step, twist, glow, sway), never where it runs")
        had = (SP.resolve(limb).get("barbs") or {}).get("count", 0)
        if "barbs" in change and had:
            left = (change["barbs"] or {}).get("count", 0) if change["barbs"] is not False else 0
            if left * 2 < had:
                raise SystemExit(f"{who}: the form leaves {name} {left} of its {had} barbs. They are the species' look: keep at least half "
                                 "(turn them, shorten them, still them), and pay for what the form adds somewhere else")
        limb.update(copy.deepcopy(change))      # a changed key REPLACES the limb's whole value for that key (give every key of a barbs dict)
    for limb in copy.deepcopy(own.get("add") or []):
        if limb["name"] in names:
            raise SystemExit(f"{spec['species']} {element}: the form adds a limb the body already has: {limb['name']}")
        out["spindles"].append(limb)
    out["membranes"] = list(out.get("membranes", [])) + copy.deepcopy(own.get("membranes") or [])
    if own.get("feeling"):
        out["feeling"] = own["feeling"]

    limbs = []
    for limb in out["spindles"]:
        if limb.get("mesh") != "grown":
            limbs.append(limb)
            continue
        s = SP.resolve(limb)
        s["strands"] = max(2, s.get("strands", 3) + r["strands"])
        s["wire"] = [w * r["wire"] for w in s.get("wire", [0.09, 0.04])]
        s["girth"] = [g * r["girth"] for g in s["girth"]]
        s["twist"] = s.get("twist", 1.5) * r["twist"]
        if r["section"]:
            s["section"] = r["section"]
        if s.get("sway"):
            s["sway"] = dict(s["sway"], amp=s["sway"]["amp"] * r["amp"])
        g = dict(s.get("glow") or {})
        g["waves"] = g.get("waves", 1.0) * r["glow_waves"]
        g["bright"] = [min(1.3, b * r["bright"]) for b in g.get("bright", [1.0, 0.75])]
        s["glow"] = g
        if r["sparks"] and not s.get("barbs") and s.get("barbs") is not False:
            s["barbs"] = dict(SPARKS)
        # barbs False stays False: deleting the key would hand the limb back its DESIGN's barbs when the body is built
        limbs.append(s)
    out["spindles"] = limbs

    if element == "Space":      # the law's erosion, limb by limb, and each prism the law's needle in its socket
        grown = [s for s in limbs if s.get("mesh") == "grown"]
        ref = E.reference(max(max(s["girth"]) for s in grown))
        by = {s["name"]: s for s in limbs}
        if refit:
            for p in out["prisms"]:
                host = by.get(p["spindle"])
                f = E.girth(_girth_at(host, p["at"]), ref) if host and host.get("mesh") == "grown" else 1.0
                size = [float(v) for v in p["size"]]
                p["size"] = [round(v, 4) for v in E.needle(size, [size[0] * f, size[1] * f, size[2]])]      # z runs along the limb
        for s in grown:
            s["girth"] = [g * E.girth(g, ref) for g in s["girth"]]
        out["eroded"] = "authored"

    base_across = {m["name"]: m.get("across", 9) for m in out["membranes"]}
    out["membranes"] = [dict(m, across=max(4, int(round(base_across[m["name"]] * r["across"])))) for m in out["membranes"]]
    out["sway_hz"] = out.get("sway_hz", 0.5) * r["hz"]
    out["turn"] = turn_of(spec, element)
    if out.get("unity"):
        u = out["unity"]
        u["idle_hz"], u["fast_hz"] = u.get("idle_hz", 0.38) * r["hz"], u.get("fast_hz", 0.9) * r["hz"]
        mine = own.get("unity") or {}
        for lm in u["limbs"]:
            lm["wires"] = list(lm["wires"]) + list((mine.get("wires") or {}).get(lm["name"], []))
        u["limbs"] = [dict(lm, degrees=[d * r["amp"] for d in lm["degrees"]]) for lm in u["limbs"] + copy.deepcopy(mine.get("limbs") or [])]
    return _fit(out)


LIGHTEST = 2.6      # the coarsest a form's wire is ever made to fit the bar: rings this many times further apart


def _fit(out):
    """A form over the mesh bar is LIGHTENED until it fits, the same way a person would (/spindle section 4), and
    says by how much (`lightened`): rings further apart on every limb, membranes woven sparser, then fewer barbs.
    The body's plan, its limbs and its prisms are untouched. A form that would need more than LIGHTEST is left over
    the bar for a person to lighten by design."""
    if mesh_size(out) <= SP.CREATURE_VERTS:
        return out
    base = copy.deepcopy(out)
    k = 1.0
    while k < LIGHTEST:
        k = round(k + 0.1, 2)
        out = copy.deepcopy(base)
        for s in out["spindles"]:
            if s.get("mesh") != "grown":
                continue
            s["step"] = s.get("step", 1.0) * k
            if s.get("barbs") and k > 1.5:
                s["barbs"] = dict(s["barbs"], count=max(3, int(round(s["barbs"]["count"] / (k / 1.5)))))
        out["membranes"] = [dict(m, across=max(4, int(round(m["across"] / k ** 0.5)))) for m in out["membranes"]]
        out["lightened"] = k
        if mesh_size(out) <= SP.CREATURE_VERTS:
            break
    return out


REACH = 2.5      # a form's longest extent over the base body's: past this the creature is its addition, not itself


def reach(spec, base):
    """How far a form reaches against the body it was made of: the longest side of its box over the base body's."""
    import body as BODY

    def longest(sp):
        vs = [v for w in BODY.from_spec(sp)["wires"] for v in w["verts"]]
        return max(max(v[i] for v in vs) - min(v[i] for v in vs) for i in range(3))
    plain = {k: v for k, v in base.items() if k != "forms"}
    return longest(spec) / max(longest(plain), 1e-9)


def mesh_size(spec):
    import body as BODY
    return sum(len(SP.mesh(w)[0]) for w in BODY.from_spec(spec)["wires"] if w.get("source") == "grown")


def table(names=None):
    """[(creature, element, vertices, broken laws, in Unity)]. A form the tool refuses is a row with its reason as a law."""
    rows = []
    for key in SP.grown_species():
        if names and key not in names:
            continue
        base = C.load(key)
        for e in ELEMENTS:
            try:
                laws = C.check(form(base, e, refit=False))
                far = reach(form(base, e), base)
                if far > REACH:
                    laws.append(f"REACH the form is {far:.1f} x the body's own longest side (at most {REACH}): what it adds has become the creature. Pull it in")
                rows.append((key, e, mesh_size(form(base, e)), laws, bool(base.get("unity"))))
            except SystemExit as refused:
                rows.append((key, e, 0, [str(refused)], bool(base.get("unity"))))
    return rows


def sheet(key, out_path, size=520):
    """The four forms. Top row: from above on ONE ruler, each at its element's body scale (how big it is in the cell).
    Bottom row: each filling its panel, mid-beat and seen from behind and above (what it is)."""
    import math
    import body as BODY
    from PIL import Image, ImageDraw
    base = C.load(key)
    made = [(e, form(base, e), BODY.from_spec(form(base, e)), base["elements"][e]["body_scale"]) for e in ELEMENTS]
    span = max(max(abs(c) for w in b["wires"] for v in w["verts"] for c in v) * k for _e, _f, b, k in made) * 2.05
    px = (size - 20) / span
    top = int(size * 0.62)
    img = Image.new("RGB", (size * 4, top + size + 50), (9, 12, 22))
    d = ImageDraw.Draw(img)
    tint = {"Time": (120, 220, 255), "Charge": (255, 120, 150), "Mass": (150, 255, 190), "Space": (200, 170, 255)}
    sn, cs = math.sin(1.1), math.cos(1.1)
    for i, (e, f, b, k) in enumerate(made):
        ox, oy = i * size + size / 2, 44 + top / 2
        nv = sum(len(SP.mesh(w)[0]) for w in b["wires"] if w.get("source") == "grown")
        d.text((i * size + 8, 6), f"{f['species']} {e}   x{k:g}   {nv} vertices   {f['sway_hz']:.2f} Hz", fill=tint[e])
        feel = f.get("feeling") or ""
        cut = feel.rfind(" ", 0, int(size / 6.2)) if len(feel) > size / 6.2 else len(feel)
        d.text((i * size + 8, 19), feel[:cut], fill=(150, 160, 180))
        d.text((i * size + 8, 31), feel[cut:].strip()[:int(size / 6.2)], fill=(150, 160, 180))
        posed = []
        for w in b["wires"]:
            vs = w["verts"]
            col = tuple(int(c * (0.4 if w.get("membrane") else 0.9)) for c in tint[e])
            for a, c in w["edges"]:
                d.line([ox + vs[a][0] * k * px, oy - (vs[a][2] + 26) * k * px, ox + vs[c][0] * k * px, oy - (vs[c][2] + 26) * k * px], fill=col)
            sw = w["sway"]
            posed.append((w, [[v[j] + sw[n][j] * sn + sw[n][3 + j] * cs for j in range(3)] for n, v in enumerate(vs)]))
        r = max(2.0, 1.775 * px)
        d.ellipse([ox - r, oy - 26 * k * px - r, ox + r, oy - 26 * k * px + r], outline=(90, 230, 255), width=2)
        # the portrait: x right, the body's length up the panel and foreshortened, its lift (y) on top of that
        proj = lambda v: (v[0], v[2] * 0.62 + v[1] * 0.78)
        pts = [proj(v) for _w, vs in posed for v in vs]
        lo = [min(p[j] for p in pts) for j in range(2)]
        hi = [max(p[j] for p in pts) for j in range(2)]
        q = (size - 36) / max(hi[0] - lo[0], hi[1] - lo[1])
        cx, cy = i * size + size / 2, top + 44 + size / 2
        at = lambda v: (cx + (proj(v)[0] - (lo[0] + hi[0]) / 2) * q, cy - (proj(v)[1] - (lo[1] + hi[1]) / 2) * q)
        for w, vs in posed:
            col = tuple(int(c * (0.38 if w.get("membrane") else 1.0)) for c in tint[e])
            for a, c in w["edges"]:
                d.line([at(vs[a]), at(vs[c])], fill=col)
        for p in b["prisms"]:
            s6 = p.get("sway") or [0.0] * 6
            x, y = at([p["at"][j] + s6[j] * sn + s6[3 + j] * cs for j in range(3)])
            half = max(2.0, max(p["size"]) * q / 2)
            thin = max(1.5, sorted(p["size"])[1] * q / 2)
            col = (255, 80, 100) if (p["kind"] == "danger" or e == "Charge") else (240, 244, 252)
            d.ellipse([x - thin, y - half * 0.62, x + thin, y + half * 0.62], outline=col, width=2)
            if e == "Mass" and p["kind"] != "danger":
                d.ellipse([x - half * 1.5, y - half * 1.5, x + half * 1.5, y + half * 1.5], outline=(120, 255, 190))
        x, y = at([0, 0, 0])
        r = 1.775 * q
        d.ellipse([x - r, y - r, x + r, y + r], outline=(90, 230, 255), width=2)
    img.save(out_path)
    return out_path


def silhouettes(key, only=None):
    """Law L12 for the four forms (or the ones in `only`): each must be as far from every OTHER species and every vessel
    as a new species must be. Its own species is not counted: the forms are told apart, and are meant to be kin.
    Returns ([(element, nearest name, distance, the distance it must reach)], {(a, b): distance between two forms},
    the bar, the base body's own (nearest, distance)). The bar is silhouette.py's own, the upper quartile of how far
    the shipped bodies stand from their nearest look-alike. A species whose BASE outline a person accepted under the
    bar (silhouette.ACCEPTED) is held to its base instead: a form may not be more alike than the body it was made of."""
    import numpy as np
    import body as BODY
    import silhouette as S
    base = C.load(key)
    mine = {e: S.views(S.body_points(BODY.from_spec(form(base, e)))) for e in (only or ELEMENTS)}
    plain = S.views(S.body_points(BODY.from_spec(base)))
    ros = [(n, k, S.views(p)) for n, k, p in S.roster() if k != "archive"]
    shipped = [v for _n, k, v in ros if k in ("vessel", "fauna")]
    bar = float(np.percentile([min(S.distance(a, b) for b in shipped if b is not a) for a in shipped], 75))
    others = [(n, v) for n, _k, v in ros if n != base["species"]]
    base_d, base_n = min((S.distance(plain, v), n) for n, v in others)
    need = min(bar, base_d) if base["species"] in S.ACCEPTED else bar
    near = []
    for e in mine:
        d, n = min((S.distance(mine[e], v), n) for n, v in others)
        near.append((e, n, d, need))
    kin = {(a, b): S.distance(mine[a], mine[b]) for i, a in enumerate(list(mine)) for b in list(mine)[i + 1:]}
    return near, kin, bar, (base_n, base_d)


def self_test():
    fails = []
    base = C.load("ray")
    sizes = {e: mesh_size(form(base, e)) for e in ELEMENTS}
    if max(sizes.values()) > SP.CREATURE_VERTS:
        fails.append(f"a form of the ray is over the mesh bar: {sizes}")
    for e in ELEMENTS:
        f = form(base, e, refit=False)
        if f["prisms"] != base["prisms"]:
            fails.append(f"{e} moved or resized a prism: an element changes what a prism is by the game's law, never the species' plan")
        if not {s["name"] for s in base["spindles"]} <= {s["name"] for s in f["spindles"]}:
            fails.append(f"{e} dropped a limb of the body's plan")
        errs = C.check(f)
        if errs:
            fails.append(f"{e} breaks a law: {errs[0]}")
    if base["spindles"][0].get("strands") != C.load("ray")["spindles"][0].get("strands") or "element" in base:
        fails.append("form() changed the spec it was given")
    # Space is the law: the thickest girth comes back to 1/1.6477, a thin limb keeps its girth, every prism is a needle in its socket
    sp, plain = form(base, "Space"), form(base, "Time")
    thick = max(max(s["girth"]) for s in plain["spindles"])
    got = max(max(s["girth"]) for s in sp["spindles"])
    if abs(got / thick - E.THICKEST_GIRTH) > 1e-6:
        fails.append(f"the Space form's thickest girth is {got / thick:.4f} of the base body's, the law says {E.THICKEST_GIRTH:.4f}")
    thin = next(s for s in plain["spindles"] if s["name"] == "Whip")
    if next(s for s in sp["spindles"] if s["name"] == "Whip")["girth"] != thin["girth"]:
        fails.append("the Space form thinned a limb already under the law's reference")
    for p, q in zip(base["prisms"], sp["prisms"]):
        long = max(range(3), key=lambda i: p["size"][i])
        if not (abs(q["size"][long] - p["size"][long] * E.FIT_CLEARANCE) < 1e-3 and all(q["size"][i] < p["size"][i] for i in range(3))):
            fails.append(f"the Space prism {p['name']} is not the law's needle: {q['size']}")
            break
    if sp.get("eroded") != "authored" or plain.get("eroded"):
        fails.append("only the Space form is authored eroded")
    # a form may not touch a limb the body has not got, or add one it has
    bad = copy.deepcopy(base)
    bad.setdefault("forms", {}).setdefault("Mass", {})["change"] = {"NoSuchLimb": {"strands": 9}}
    try:
        form(bad, "Mass")
        fails.append("a form changing a limb the body has not got was accepted")
    except SystemExit:
        pass
    for label, element, change in (("a Time form that changes the body", "Time", {"Whip": {"strands": 4}}),
                                   ("a form that re-routes a limb", "Mass", {"Whip": {"path": [[0, 0, -30], [0, 0, -40]]}}),
                                   ("a form that strips a limb's barbs", "Charge", {"Lead0": {"barbs": {"count": 1, "length": [1, 1]}}})):
        bad = copy.deepcopy(base)
        if label.endswith("barbs"):
            bad["spindles"] = [dict(s, barbs={"count": 12, "length": [2, 1]}) if s["name"] == "Lead0" else s for s in bad["spindles"]]
        bad["forms"][element] = dict(bad["forms"][element], change=change)
        try:
            form(bad, element)
            fails.append(f"{label} was accepted")
        except SystemExit:
            pass
    off = copy.deepcopy(base)
    off["spindles"] = [dict(s, design="rachis") if s["name"] == "Whip" else s for s in off["spindles"]]
    off["forms"]["Mass"] = dict(off["forms"]["Mass"], change={})
    with_barbs = mesh_size(form(off, "Space"))
    off["spindles"] = [dict(s, barbs=False) if s["name"] == "Whip" else s for s in off["spindles"]]
    if not mesh_size(form(off, "Space")) < with_barbs:
        fails.append("barbs: False did not remove a design's barbs (the design's came back when the body was built)")
    return fails


def main(argv):
    if "--self-test" in argv:
        fails = self_test()
        for f in fails:
            print("FAIL:", f)
        print("elements self-test:", "ok" if not fails else f"{len(fails)} problem(s)")
        return 1 if fails else 0
    if "--silhouette" in argv:
        key = argv[argv.index("--silhouette") + 1]
        only = [argv[argv.index("--form") + 1]] if "--form" in argv else None
        near, kin, bar, (base_n, base_d) = silhouettes(key, only)
        bad = 0
        print(f"{key} base body: nearest other body {base_n} {base_d:.3f}; the bar is {bar:.3f}"
              + (f"; a person accepted this outline, so each form must reach {min(bar, base_d):.3f}" if near and near[0][3] < bar else ""))
        for e, n, d, need in near:
            ok = d >= need - 0.0005
            bad += not ok
            print(f"{key} {e:<7} nearest other body: {n} {d:.3f}" + ("" if ok else f"   UNDER {need:.3f}: spread what this form adds OUTWARD, "
                                                                     f"away from the shape of the {n} (/fauna-maker step 6.5)"))
        if kin:
            print("between its own forms (kin, no bar): " + ", ".join(f"{a}-{b} {d:.2f}" for (a, b), d in kin.items()))
        return 1 if bad else 0
    if "--sheet" in argv:
        i = argv.index("--sheet")
        print(sheet(argv[i + 1], argv[i + 2]))
        return 0
    bad = 0
    names = [a for a in argv if not a.startswith("-")]
    print(f"{'creature':<10}" + "".join(f"{e:>9}" for e in ELEMENTS) + f"   vertices a form (bar {SP.CREATURE_VERTS})")
    rows = table(names)
    for key in SP.grown_species():
        if names and key not in names:
            continue
        mine = {e: (v, errs, ported) for k, e, v, errs, ported in rows if k == key}
        ported = mine["Time"][2] or bool(C.load(key).get("forms"))      # a creature with its own forms is held to them
        flags = [e + (": " + mine[e][1][0] if mine[e][1] else " over the bar") for e in ELEMENTS if mine[e][0] > SP.CREATURE_VERTS or mine[e][1]]
        bad += bool(flags) and ported
        light = [f"{e} x{form(C.load(key), e)['lightened']:g}" for e in ELEMENTS if mine[e][0] and form(C.load(key), e).get("lightened")]
        print(f"{key:<10}" + "".join(f"{mine[e][0]:>9}" for e in ELEMENTS)
              + ("   " + ("FAIL " if ported else "no forms of its own yet: ") + "; ".join(flags) if flags else ("   ok" if ported else "   no forms of its own yet"))
              + ("   lightened to fit (rings further apart): " + ", ".join(light) if light else ""))
    return 1 if ("--check" in argv and bad) else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
