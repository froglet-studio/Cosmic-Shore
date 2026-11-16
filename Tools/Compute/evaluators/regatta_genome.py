"""THE REGATTA COURSE GENOME in Python: the search's side of RegattaCourseGenome.cs (Plan step: time-arena-course;
Regatta flywheel W2, 2026-11-16).

Garrett: "our genes could bring in every fundamental with all their types and parameters fully expressable. all the
segment spawners, all the flora, anything. really mutate the heck out the course and find tons of different solutions
in a vast space." A genome is one JSON document (the flywheel's shared contract):

    {"version": 1, "intensity": 1-4, "seed": int (0 = the default circuit seed),
     "circuit": {"CornerProfile": [8 turn angles], "RingRadius": u, ...},       RegattaCourse.ForIntensity overrides
     "rails": {"LaneOffset", "TwistTurnsPerLap", "PrismSpacing"},              the braid
     "features": {"Sections", "AlternateGates", "Crystals"},                   RegattaCourseFeatures (regatta_features.py)
     "line_clearance": u,                                                      the tube round the line kept open
     "placements": [{"kind": catalogue id, "leg": leg + Hermite t, "offset": [radius_u, bearing_deg, along_u],
                     "scale": bounding radius u, "yaw", "pitch", "roll", "domain": Native|Jade|Ruby|Gold|Blue,
                     "prism": Native|Plain|Danger|Shielded|SuperShielded, "seed", "density",
                     "cross_line": bool (may stand in the line's tube), "cross_rail": bool (may stand on a rail),
                     "params": {field: value}}],
     "flora": {"preset", "sitesPerLeg", "offsetMin", ...}}                     RegattaLifeformSiteSettings + the preset

What may be placed, and every parameter's range, is the GENE CATALOGUE (Assets/Resources/Regatta/
RegattaGeneCatalogue.json, Tools/Build/regatta_gene_catalogue.py: written from the C# and from a measured run of every
segment spawner through the game's own code). The empty genome is today's course ("Wide", the I1 circuit).

    random_genome(rng, intensity=None)   a valid random course
    mutate(genome, rng, strength=1.0)    heavy or light variation, repaired
    crossover(a, b, rng)                 a cut by leg: one parent's lap before it, the other's after
    repair(genome) -> (genome, notes)    RegattaCourseGenome.Repair, line for line (held to the C# by --self-test)
    descriptors(genome)                  what a quality-diversity archive bins by (counts per kind, mean scale per
                                         kind, share of legs with a placement, min clearance to the line, ...)

The genome is LAID by the game: SpawnableRegattaRails reads RegattaCourseGenome.Override (the race judge) or its own
courseGenome field (a pool entry). Where a placement's PRISMS may go is enforced there, per prism (never in the line
tube, a ring mouth, a rail, the nucleus, the membrane or an earlier placement), so a genome cannot seal the race; the
estimates here (prisms, clearance) are the catalogue's, the real numbers are the harness's
(Tools/Build/regatta_genome_harness/run.sh lay).

    python3 Tools/Compute/evaluators/regatta_genome.py --self-test     # parity with the C# repair + negative controls
    python3 Tools/Compute/evaluators/regatta_genome.py --random 12 --seed 7 --out DIR   # write random genomes
"""
import copy
import json
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
sys.path.insert(0, HERE)

import course_port as cp  # noqa: E402

VERSION = 1
CATALOGUE = os.path.join(REPO, "Assets", "Resources", "Regatta", "RegattaGeneCatalogue.json")
FIXTURE = os.path.join(REPO, "Tools", "Build", "regatta_genome_harness", "repair_fixture.jsonl")

RINGS = 8
MAX_PLACEMENTS = 24            # RegattaCourseGenome.MaxPlacements
PER_PLACEMENT_CAP = 3000       # RegattaCourseGenome.PerPlacementCap
TOTAL_PRISM_BUDGET = 6000      # RegattaCourseGenome.TotalPrismBudget
NUCLEUS_CARVE = 391.9 * 1.1    # RegattaCourseGenome.NucleusCarve
MEMBRANE_CARVE = 1150.0        # RegattaCourseGenome.MembraneCarve
LINE_CLEARANCE = (12.0, 80.0, 32.0)
MIN_RAIL_CLEARANCE = 30.0      # RegattaLifeformSites.MinRailClearance
DOMAINS = ["Native", "Jade", "Ruby", "Gold", "Blue"]
PRISMS = ["Native", "Plain", "Danger", "Shielded", "SuperShielded"]
CIRCUIT_RANGES = {"BaseRadius": (700.0, 900.0), "RingRadius": (50.0, 140.0), "CornerFloorRadius": (100.0, 260.0),
                  "RadialSwing": (0.0, 0.6), "AngularSpread": (0.5, 5.5), "LateralPerturbation": (0.0, 320.0),
                  "AxisJitterDegrees": (0.0, 50.0), "MaxPresentDegrees": (30.0, 85.0)}
FLORA_PRESETS = ["none", "groves", "thicket", "reef", "reef-fenced"]   # Tools/Build/regatta_lifeforms.py PRESETS
FLORA_DEFAULTS = {"sitesPerLeg": [], "offsetMin": 90.0, "offsetMax": 160.0, "sideDegrees": 0.0,
                  "sideSpreadDegrees": 180.0, "legWindowStart": 0.2, "legWindowEnd": 0.8, "ringClearance": 60.0,
                  "faunaCorridorRadius": 0.0, "faunaCorridorStandoff": 50.0}

_CAT = None


def catalogue():
    global _CAT
    if _CAT is None:
        cat = json.load(open(CATALOGUE, encoding="utf-8"))
        cat["by_id"] = {k["id"]: k for k in cat["kinds"]}
        cat["usable"] = [k for k in cat["kinds"] if k["usable"]]
        _CAT = cat
    return _CAT


def empty():
    """Today's course."""
    return {"version": VERSION, "intensity": 1, "seed": 0,
            "features": {"Sections": [], "AlternateGates": [], "Crystals": {"PerLap": 0, "Phase": 0.0}},
            "line_clearance": LINE_CLEARANCE[2], "placements": [], "flora": dict(FLORA_DEFAULTS, preset="none")}


def is_empty(g):
    f = g.get("features") or {}
    fl = g.get("flora") or {}
    return (int(g.get("intensity", 1)) == 1 and int(g.get("seed", 0)) == 0 and not g.get("circuit")
            and not g.get("rails") and not f.get("Sections") and not f.get("AlternateGates")
            and int((f.get("Crystals") or {}).get("PerLap", 0)) <= 0 and not g.get("placements")
            and not any(int(x) > 0 for x in fl.get("sitesPerLeg") or []) and float(fl.get("faunaCorridorRadius", 0)) <= 0)


# ── The circuit and the placement frame (RegattaCourseGenome.CircuitSettings / SpineFrame / PlacementPose) ───────

def _clamp(v, lo, hi):
    return lo if v < lo else hi if v > hi else v


def _wrap(v, period):
    v = float(v)
    return v - math.floor(v / period) * period


def circuit_settings(g):
    s = cp.regatta_settings(int(g.get("intensity", 1)))
    c = g.get("circuit") or {}
    prof = c.get("CornerProfile")
    if isinstance(prof, list) and len(prof) == RINGS:
        s["CornerProfile"] = [float(x) for x in prof]
    for k, v in c.items():
        if k in CIRCUIT_RANGES:
            s[k] = float(v)
    return s


def gates(g):
    seed = int(g.get("seed", 0)) or cp.REGATTA_DEFAULT_SEED
    return cp.generate(cp.seed_for_intensity(seed, int(g.get("intensity", 1))), circuit_settings(g))


def spine_frame(gs, leg):
    n = len(gs)
    wrapped = cp.f(leg) - cp.f(math.floor(float(cp.f(leg)) / n) * n)
    i = min(n - 1, int(wrapped))
    t = cp.f(wrapped - cp.f(i))
    a, b = gs[i], gs[(i + 1) % n]
    chord = cp.magnitude(cp.sub(b["p"], a["p"]))
    m0 = cp.mul(cp.normalized(a["a"]), chord)
    m1 = cp.mul(cp.normalized(b["a"]), chord)
    point = cp.hermite(a["p"], m0, b["p"], m1, t)
    tangent = cp.safe_normalize(cp.hermite_derivative(a["p"], m0, b["p"], m1, t), a["a"])
    o = cp.sub(point, cp.mul(tangent, cp.dot(point, tangent)))
    outward = cp.safe_normalize(o, cp.perpendicular(tangent))
    return point, tangent, outward


def placement_centre(gs, p):
    point, tangent, outward = spine_frame(gs, p["leg"])
    side = cp.cross(tangent, outward)
    off = p.get("offset") or [0.0, 0.0, 0.0]
    b = cp.f(off[1]) * cp.DEG2RAD
    d = cp.add(cp.mul(outward, cp.fcos(b)), cp.mul(side, cp.fsin(b)))
    return cp.add(cp.add(point, cp.mul(d, cp.f(off[0]))), cp.mul(tangent, cp.f(off[2])))


# ── Repair (RegattaCourseGenome.Repair, line for line) ────────────────────────────────────────────────────────────

def _param_clamp(prm, v):
    t = prm["type"]
    v = float(v)
    if t == "bool":
        return 1.0 if v >= 0.5 else 0.0
    if t == "enum":
        return float(max(0, min(max(0, len(prm.get("values") or []) - 1), _round_half_even(v))))
    if t == "domain":
        return float(max(0, min(len(DOMAINS) - 2, _round_half_even(v))))
    if t == "int":
        return float(max(prm["lo"], min(prm["hi"], _round_half_even(v))))
    return max(prm["lo"], min(prm["hi"], v))


def _round_half_even(v):
    """Math.Round / Mathf.Round: banker's rounding, as Python's round()."""
    return round(v)


def estimated_prisms(kind, p):
    d = float(p.get("density", 1.0)) if kind["world"] else 1.0
    return min(PER_PLACEMENT_CAP, max(1, int(round(kind["native_prisms"] * d))))


def _repair_features(f, notes):
    o = {"Sections": [], "AlternateGates": [], "Crystals": {"PerLap": 0, "Phase": 0.0}}
    f = f or {}
    for s in f.get("Sections") or []:
        if len(o["Sections"]) >= 24:
            notes.append("features: over 24 sections, the rest dropped")
            break
        a, b = _clamp(float(s.get("From", 0)), 0.0, 1.0), _clamp(float(s.get("To", 0)), 0.0, 1.0)
        lo, hi = min(a, b), max(a, b)
        if hi - lo < 0.01:
            continue
        o["Sections"].append({"Leg": int(_clamp(int(s.get("Leg", -1)), -1, RINGS - 1)), "From": lo, "To": hi,
                              "Kind": int(_clamp(int(s.get("Kind", 0)), 0, 3)), "Neutral": bool(s.get("Neutral", False)),
                              "Detour": _clamp(float(s.get("Detour", 0)), 0.0, 200.0),
                              "DetourBearing": _wrap(s.get("DetourBearing", 0), 360.0)})
    for a in f.get("AlternateGates") or []:
        ring = int(a.get("Ring", 0)) % RINGS
        if any(x["Ring"] == ring for x in o["AlternateGates"]):
            continue
        off = _clamp(float(a.get("Offset", 0)), 0.0, 420.0)
        if off <= 0:
            continue
        o["AlternateGates"].append({"Ring": ring, "Offset": off, "Bearing": _wrap(a.get("Bearing", 0), 360.0),
                                    "RadiusScale": _clamp(float(a.get("RadiusScale", 1)), 0.6, 1.4)})
    c = f.get("Crystals") or {}
    o["Crystals"] = {"PerLap": int(_clamp(int(c.get("PerLap", 0)), 0, 4)), "Phase": _wrap(c.get("Phase", 0), 1.0)}
    return o


def _repair_flora(f, lane_offset):
    f = dict(FLORA_DEFAULTS, **(f or {}))
    sites = [int(_clamp(int(x), 0, 12)) for x in (f.get("sitesPerLeg") or [])[:RINGS]]
    o = {"sitesPerLeg": sites if any(x > 0 for x in sites) else []}
    o["offsetMin"] = _clamp(max(float(f["offsetMin"]), lane_offset + MIN_RAIL_CLEARANCE), 0.0, 300.0)
    o["offsetMax"] = _clamp(max(float(f["offsetMax"]), o["offsetMin"]), o["offsetMin"], 320.0)
    o["sideDegrees"] = _wrap(f["sideDegrees"], 360.0)
    o["sideSpreadDegrees"] = _clamp(float(f["sideSpreadDegrees"]), 0.0, 180.0)
    ws, we = float(f["legWindowStart"]), float(f["legWindowEnd"])
    o["legWindowStart"] = _clamp(min(ws, we), 0.0, 1.0)
    o["legWindowEnd"] = _clamp(max(ws, we), 0.0, 1.0)
    o["ringClearance"] = _clamp(float(f["ringClearance"]), 0.0, 200.0)
    o["faunaCorridorRadius"] = _clamp(float(f["faunaCorridorRadius"]), 0.0, 200.0)
    o["faunaCorridorStandoff"] = _clamp(float(f["faunaCorridorStandoff"]), 0.0, 200.0)
    return o


def repair(genome):
    """RegattaCourseGenome.Repair: (a legal copy, the notes on what changed)."""
    cat = catalogue()
    notes = []
    src = genome or {}
    g = {"version": VERSION, "intensity": int(_clamp(int(src.get("intensity", 1)), 1, 4)), "seed": int(src.get("seed", 0))}

    circuit = {}
    for k, v in (src.get("circuit") or {}).items():
        if k == "CornerProfile":
            continue
        if k not in CIRCUIT_RANGES:
            notes.append(f"circuit: dropped unknown setting {k}")
            continue
        lo, hi = CIRCUIT_RANGES[k]
        circuit[k] = _clamp(float(v), lo, hi)
    prof = (src.get("circuit") or {}).get("CornerProfile")
    if prof is not None:
        cpf = [_clamp(float(prof[i]), 0.0, 160.0) if i < len(prof) else 0.0 for i in range(RINGS)]
        total = sum(cpf)
        if total > 1.0:
            circuit["CornerProfile"] = [x * 360.0 / total for x in cpf]
        else:
            notes.append("circuit: corner profile had no turning; the intensity's row is used")
    if circuit:
        g["circuit"] = circuit
    ring_radius = circuit_settings(g)["RingRadius"]

    rails_in, rails = src.get("rails") or {}, {}
    if "LaneOffset" in rails_in:
        rails["LaneOffset"] = _clamp(float(rails_in["LaneOffset"]), 14.0, max(14.0, min(40.0, ring_radius - 28.0)))
    elif cp.REGATTA_RAILS["LaneOffset"] > ring_radius - 28.0:
        rails["LaneOffset"] = max(14.0, ring_radius - 28.0)
        notes.append("rails: lane offset pulled in to fit the mouth")
    if "TwistTurnsPerLap" in rails_in:
        rails["TwistTurnsPerLap"] = _clamp(float(round(float(rails_in["TwistTurnsPerLap"]))), 0.0, 3.0)
    if "PrismSpacing" in rails_in:
        rails["PrismSpacing"] = _clamp(float(rails_in["PrismSpacing"]), 6.0, 16.0)
    if rails:
        g["rails"] = rails

    g["features"] = _repair_features(src.get("features"), notes)
    if "crystals" in src and isinstance(src["crystals"], dict):   # the contract's top-level form
        c = src["crystals"]
        g["features"]["Crystals"] = {"PerLap": int(_clamp(int(c.get("PerLap", 0)), 0, 4)),
                                     "Phase": _wrap(c.get("Phase", 0), 1.0)}
    g["line_clearance"] = _clamp(float(src.get("line_clearance", LINE_CLEARANCE[2])), LINE_CLEARANCE[0], LINE_CLEARANCE[1])
    lane = rails.get("LaneOffset", cp.REGATTA_RAILS["LaneOffset"])
    fl = src.get("flora") or {}
    g["flora"] = dict(_repair_flora(fl, lane), preset=fl.get("preset") or "none")

    gs = gates(g)
    placements, estimated = [], 0
    for idx, s in enumerate(src.get("placements") or []):
        if s is None:
            continue
        if len(placements) >= MAX_PLACEMENTS:
            notes.append(f"placement {idx}: over the {MAX_PLACEMENTS} cap, dropped")
            continue
        kind = cat["by_id"].get(s.get("kind"))
        if kind is None or not kind["usable"]:
            notes.append(f"placement {idx}: kind '{s.get('kind')}' is not placeable, dropped")
            continue
        off = list(s.get("offset") or [0.0, 0.0, 0.0]) + [0.0, 0.0, 0.0]
        p = {"kind": kind["id"], "leg": _wrap(s.get("leg", 0), RINGS),
             "offset": [_clamp(float(off[0]), 0.0, 400.0), _wrap(off[1], 360.0), _clamp(float(off[2]), -150.0, 150.0)],
             "scale": float(s.get("scale", 60.0)), "yaw": _wrap(s.get("yaw", 0), 360.0),
             "pitch": _wrap(s.get("pitch", 0), 360.0), "roll": _wrap(s.get("roll", 0), 360.0),
             "domain": s.get("domain") if s.get("domain") in DOMAINS else "Native",
             "prism": s.get("prism") if s.get("prism") in PRISMS else "Native",
             "seed": int(s.get("seed", 0)),
             "density": _clamp(float(s.get("density", 1.0)), 0.1, 2.0) if kind["world"] else 1.0,
             "cross_line": bool(s.get("cross_line", False)), "cross_rail": bool(s.get("cross_rail", False))}
        params = {}
        prms = {q["name"]: q for q in kind["params"]}
        for name in sorted((s.get("params") or {}).keys()):
            if name not in prms:
                notes.append(f"placement {idx}: dropped unknown parameter {name}")
                continue
            params[name] = _param_clamp(prms[name], s["params"][name])
        p["params"] = params
        p["scale"] = _clamp(p["scale"], kind["scale_lo"], kind["scale_hi"])
        c = placement_centre(gs, p)
        if float(cp.magnitude(c)) > MEMBRANE_CARVE:
            notes.append(f"placement {idx}: centre outside the membrane, dropped")
            continue
        est = estimated_prisms(kind, p)
        if estimated + est > TOTAL_PRISM_BUDGET:
            notes.append(f"placement {idx}: over the prism budget, dropped")
            continue
        estimated += est
        placements.append(p)
    g["placements"] = placements
    return g, notes


# ── Variation ─────────────────────────────────────────────────────────────────────────────────────────────────────

def _log_uniform(rng, lo, hi):
    return math.exp(rng.uniform(math.log(max(lo, 1e-3)), math.log(max(hi, lo + 1e-3))))


def random_params(rng, kind, share=0.5):
    out = {}
    for prm in kind["params"]:
        if rng.random() >= share:
            continue
        if prm["type"] in ("bool", "enum", "domain", "int"):
            out[prm["name"]] = float(rng.randint(int(prm["lo"]), int(prm["hi"])))
        else:
            out[prm["name"]] = rng.uniform(prm["lo"], prm["hi"])
    return out


def random_placement(rng, kind=None):
    cat = catalogue()
    kind = kind or rng.choice(cat["usable"])
    lo, hi = kind["scale_lo"], kind["scale_hi"]
    # Mostly obstacles a leg's width, sometimes a world the course runs through.
    scale = _log_uniform(rng, lo, min(hi, max(lo, 260.0))) if rng.random() < 0.8 else _log_uniform(rng, lo, hi)
    radius = rng.uniform(0.0, 160.0) if rng.random() < 0.5 else rng.uniform(100.0, 400.0)
    return {"kind": kind["id"], "leg": rng.uniform(0.0, RINGS),
            "offset": [radius, rng.uniform(0.0, 360.0), rng.uniform(-60.0, 60.0)],
            "scale": scale, "yaw": rng.uniform(0.0, 360.0), "pitch": rng.uniform(0.0, 360.0),
            "roll": rng.uniform(0.0, 360.0),
            "domain": "Native" if rng.random() < 0.5 else rng.choice(DOMAINS[1:]),
            "prism": "Native" if rng.random() < 0.5 else rng.choice(PRISMS[1:]),
            "seed": rng.randint(1, 2 ** 31 - 1) if rng.random() < 0.7 else 0,
            "density": rng.uniform(0.1, 1.2) if kind["world"] else 1.0,
            "cross_line": rng.random() < 0.35,
            "cross_rail": rng.random() < 0.25,
            "params": random_params(rng, kind)}


def _random_section(rng):
    a = rng.uniform(0.0, 0.9)
    return {"Leg": rng.randint(-1, RINGS - 1) if rng.random() < 0.15 else rng.randint(0, RINGS - 1),
            "From": a, "To": min(1.0, a + rng.uniform(0.05, 0.6)), "Kind": rng.randint(0, 3),
            "Neutral": rng.random() < 0.3, "Detour": rng.uniform(0.0, 150.0) if rng.random() < 0.3 else 0.0,
            "DetourBearing": rng.uniform(0.0, 360.0)}


def random_genome(rng, intensity=None):
    """A valid random course: today's circuit family moved, the features shuffled, a handful to a couple of dozen
    placements of any kind in the catalogue, sometimes lifeform ground."""
    g = empty()
    g["intensity"] = intensity or rng.randint(1, 4)
    g["seed"] = rng.randint(1, 2 ** 31 - 1) if rng.random() < 0.8 else 0
    if rng.random() < 0.5:
        circuit = {k: rng.uniform(lo, hi) for k, (lo, hi) in CIRCUIT_RANGES.items() if rng.random() < 0.4}
        if rng.random() < 0.5:
            circuit["CornerProfile"] = [rng.uniform(0.0, 150.0) for _ in range(RINGS)]
        g["circuit"] = circuit
    if rng.random() < 0.3:
        g["rails"] = {"LaneOffset": rng.uniform(14.0, 40.0)}
    g["features"]["Sections"] = [_random_section(rng) for _ in range(rng.randint(0, 6))]
    g["features"]["AlternateGates"] = [{"Ring": rng.randint(0, RINGS - 1), "Offset": rng.uniform(150.0, 420.0),
                                        "Bearing": rng.uniform(0.0, 360.0), "RadiusScale": rng.uniform(0.6, 1.4)}
                                       for _ in range(rng.randint(0, 3))]
    g["features"]["Crystals"] = {"PerLap": rng.randint(0, 4) if rng.random() < 0.3 else 0, "Phase": rng.random()}
    g["line_clearance"] = rng.uniform(*LINE_CLEARANCE[:2])
    g["placements"] = [random_placement(rng) for _ in range(rng.randint(2, 16))]
    if rng.random() < 0.3:
        g["flora"]["preset"] = rng.choice(FLORA_PRESETS[1:])
        g["flora"]["sitesPerLeg"] = [rng.randint(0, 6) for _ in range(RINGS)]
    return repair(g)[0]


def _jitter(rng, v, sigma, lo, hi):
    return _clamp(v + rng.gauss(0.0, sigma), lo, hi)


def _where_from_play(prior, g, rng):
    """Half the time, where people flew (regatta_player_prior.draw_where); else None. Only with a prior, so a search
    without one draws exactly the stream it always did."""
    if not prior or rng.random() >= 0.5:
        return None
    import regatta_player_prior  # noqa: E402 (beside this file)
    return regatta_player_prior.draw_where(prior, int(g.get("intensity", 1)), rng)


def mutate(genome, rng, strength=1.0, prior=None):
    """Apply 1 + Poisson(2 x strength) random operators, then repair. Strength 0.3 is a nudge, 3 a storm. With a
    player `prior` (regatta_player_prior.py), half the placements added or moved land on the legs and beside the
    lines people flew."""
    cat = catalogue()
    g = copy.deepcopy(genome)
    g.setdefault("placements", [])
    lam, k, ops = 2.0 * max(0.0, strength), 0, 1
    # Knuth's Poisson draw (small lambda).
    lim, prod = math.exp(-lam), rng.random()
    while prod > lim:
        k += 1
        prod *= rng.random()
    ops += k
    s = max(0.1, strength)
    for _ in range(ops):
        r = rng.random()
        pl = g["placements"]
        if r < 0.14 or not pl:                                     # add a placement
            p = random_placement(rng)
            where = _where_from_play(prior, g, rng)
            if where:
                p["leg"], p["offset"] = where
            pl.append(p)
        elif r < 0.22:                                             # remove one
            pl.pop(rng.randrange(len(pl)))
        elif r < 0.40:                                             # move one
            p = rng.choice(pl)
            where = _where_from_play(prior, g, rng)
            if where:
                p["leg"], p["offset"] = where
            else:
                p["leg"] = p["leg"] + rng.gauss(0.0, 0.3 * s)
                off = p["offset"]
                p["offset"] = [max(0.0, off[0] + rng.gauss(0.0, 40.0 * s)), off[1] + rng.gauss(0.0, 40.0 * s),
                               off[2] + rng.gauss(0.0, 20.0 * s)]
        elif r < 0.50:                                             # rescale / turn one
            p = rng.choice(pl)
            p["scale"] = p["scale"] * math.exp(rng.gauss(0.0, 0.35 * s))
            for ax in ("yaw", "pitch", "roll"):
                if rng.random() < 0.5:
                    p[ax] = p[ax] + rng.gauss(0.0, 45.0 * s)
        elif r < 0.62:                                             # re-parameterise one
            p = rng.choice(pl)
            kind = cat["by_id"].get(p["kind"])
            if kind and kind["params"]:
                prm = rng.choice(kind["params"])
                if prm["type"] in ("bool", "enum", "domain", "int"):
                    p["params"][prm["name"]] = float(rng.randint(int(prm["lo"]), int(prm["hi"])))
                else:
                    cur = float(p["params"].get(prm["name"], prm["default"]))
                    p["params"][prm["name"]] = cur + rng.gauss(0.0, 0.25 * s * (prm["hi"] - prm["lo"]))
            if kind and kind["world"] and rng.random() < 0.3:
                p["density"] = p.get("density", 1.0) * math.exp(rng.gauss(0.0, 0.3 * s))
            if rng.random() < 0.2:
                p["seed"] = rng.randint(1, 2 ** 31 - 1)
        elif r < 0.68:                                             # recolour / re-kind its prisms
            p = rng.choice(pl)
            q = rng.random()
            if q < 0.4:
                p["domain"] = rng.choice(DOMAINS)
            elif q < 0.8:
                p["prism"] = rng.choice(PRISMS)
            elif q < 0.9:                                          # onto the line, or off it
                p["cross_line"] = not p.get("cross_line", False)
            else:                                                  # onto the rails, or off them
                p["cross_rail"] = not p.get("cross_rail", False)
        elif r < 0.73:                                             # swap what it is, keep where it is
            p = rng.choice(pl)
            fresh = random_placement(rng)
            p.update({"kind": fresh["kind"], "params": fresh["params"], "scale": fresh["scale"],
                      "density": fresh["density"]})
        elif r < 0.82:                                             # the circuit
            c = g.setdefault("circuit", {})
            if rng.random() < 0.3:
                prof = list(c.get("CornerProfile") or cp.regatta_settings(g.get("intensity", 1))["CornerProfile"])
                i = rng.randrange(RINGS)
                prof[i] = max(0.0, prof[i] + rng.gauss(0.0, 25.0 * s))
                c["CornerProfile"] = prof
            else:
                key = rng.choice(list(CIRCUIT_RANGES))
                lo, hi = CIRCUIT_RANGES[key]
                base = c.get(key, cp.regatta_settings(g.get("intensity", 1)).get(key, (lo + hi) / 2))
                c[key] = _jitter(rng, float(base), 0.15 * s * (hi - lo), lo, hi)
            if rng.random() < 0.05 * s:
                g["seed"] = rng.randint(1, 2 ** 31 - 1)
        elif r < 0.92:                                             # the rail features
            f = g.setdefault("features", {"Sections": [], "AlternateGates": [], "Crystals": {"PerLap": 0, "Phase": 0}})
            q = rng.random()
            if q < 0.4 or not f["Sections"]:
                f["Sections"].append(_random_section(rng))
            elif q < 0.6:
                f["Sections"].pop(rng.randrange(len(f["Sections"])))
            elif q < 0.8:
                sec = rng.choice(f["Sections"])
                sec["From"], sec["To"] = sec["From"] + rng.gauss(0, 0.1 * s), sec["To"] + rng.gauss(0, 0.1 * s)
                sec["Kind"] = rng.randint(0, 3) if rng.random() < 0.3 else sec["Kind"]
            elif q < 0.93:
                f["AlternateGates"].append({"Ring": rng.randint(0, RINGS - 1), "Offset": rng.uniform(150.0, 420.0),
                                            "Bearing": rng.uniform(0.0, 360.0), "RadiusScale": rng.uniform(0.6, 1.4)})
            elif f["AlternateGates"]:
                f["AlternateGates"].pop(rng.randrange(len(f["AlternateGates"])))
        elif r < 0.96:                                             # the line tube
            g["line_clearance"] = float(g.get("line_clearance", 32.0)) + rng.gauss(0.0, 10.0 * s)
        else:                                                      # the lifeform ground
            fl = g.setdefault("flora", dict(FLORA_DEFAULTS, preset="none"))
            if rng.random() < 0.5:
                fl["preset"] = rng.choice(FLORA_PRESETS)
            fl["sitesPerLeg"] = [max(0, int(x) + rng.randint(-2, 2))
                                 for x in (fl.get("sitesPerLeg") or [0] * RINGS)][:RINGS]
    return repair(g)[0]


def crossover(a, b, rng):
    """A cut at one point of the lap: placements, rail sections and alternate rings before it from one parent, after it
    from the other. Placements live in leg coordinates, so they stay on their stretch of course whichever circuit the
    child takes (the circuit, the braid, the line tube and the lifeform ground come from one parent, by a coin)."""
    cut = rng.uniform(0.0, RINGS)
    first, second = (a, b) if rng.random() < 0.5 else (b, a)
    child = copy.deepcopy(first if rng.random() < 0.5 else second)
    child["placements"] = ([copy.deepcopy(p) for p in first.get("placements", []) if float(p["leg"]) < cut]
                           + [copy.deepcopy(p) for p in second.get("placements", []) if float(p["leg"]) >= cut])
    fa, fb = first.get("features") or {}, second.get("features") or {}
    child["features"] = {
        "Sections": ([copy.deepcopy(s) for s in fa.get("Sections", []) if 0 <= int(s["Leg"]) < cut]
                     + [copy.deepcopy(s) for s in fb.get("Sections", []) if int(s["Leg"]) >= cut or int(s["Leg"]) < 0]),
        "AlternateGates": ([copy.deepcopy(x) for x in fa.get("AlternateGates", []) if int(x["Ring"]) < cut]
                           + [copy.deepcopy(x) for x in fb.get("AlternateGates", []) if int(x["Ring"]) >= cut]),
        "Crystals": copy.deepcopy((fa if rng.random() < 0.5 else fb).get("Crystals") or {"PerLap": 0, "Phase": 0.0}),
    }
    return repair(child)[0]


# ── Descriptors (what an archive bins by; the search's surrogate inputs) ─────────────────────────────────────────

def descriptors(genome):
    """Numbers that say what a course IS, from the genome alone (estimates; the laid truth is the harness's)."""
    cat = catalogue()
    pl = genome.get("placements") or []
    counts, scales = {}, {}
    for p in pl:
        counts[p["kind"]] = counts.get(p["kind"], 0) + 1
        scales.setdefault(p["kind"], []).append(float(p["scale"]))
    legs = {int(float(p["leg"])) % RINGS for p in pl}
    clearance = [float(p["offset"][0]) - float(p["scale"]) for p in pl]
    est = sum(estimated_prisms(cat["by_id"][p["kind"]], p) for p in pl if p["kind"] in cat["by_id"])
    painted = [p for p in pl if p.get("domain", "Native") != "Native"]
    team = [p for p in painted if p["domain"] in ("Jade", "Ruby", "Gold")]
    hard = [p for p in pl if p.get("prism") in ("Shielded", "SuperShielded")]
    worlds = [p for p in pl if cat["by_id"].get(p["kind"], {}).get("world")]
    f = genome.get("features") or {}
    return {
        "placements": len(pl),
        "kinds_used": len(counts),
        "count_by_kind": counts,
        "mean_scale_by_kind": {k: round(sum(v) / len(v), 2) for k, v in scales.items()},
        "leg_share": round(len(legs) / RINGS, 3),
        "min_line_clearance": round(min(clearance), 2) if clearance else 400.0,
        "mean_line_clearance": round(sum(clearance) / len(clearance), 2) if clearance else 400.0,
        "on_line_share": round(sum(1 for c in clearance if c < 0) / len(pl), 3) if pl else 0.0,
        "cross_line_share": round(sum(1 for p in pl if p.get("cross_line")) / len(pl), 3) if pl else 0.0,
        "cross_rail_share": round(sum(1 for p in pl if p.get("cross_rail")) / len(pl), 3) if pl else 0.0,
        "est_prisms": est,
        "total_scale": round(sum(float(p["scale"]) for p in pl), 1),
        "world_share": round(len(worlds) / len(pl), 3) if pl else 0.0,
        "team_coloured_share": round(len(team) / len(pl), 3) if pl else 0.0,
        "hard_prism_share": round(len(hard) / len(pl), 3) if pl else 0.0,
        "sections": len(f.get("Sections") or []),
        "alternates": len(f.get("AlternateGates") or []),
        "intensity": int(genome.get("intensity", 1)),
        "line_clearance": float(genome.get("line_clearance", 32.0)),
        "flora_sites": sum(int(x) for x in (genome.get("flora") or {}).get("sitesPerLeg") or []),
    }


# ── Self-test: parity with the C# repair, and negative controls ──────────────────────────────────────────────────

def _close(a, b, path="$"):
    """None when a and b agree (numbers to 1e-3 relative), else where they part."""
    if isinstance(a, dict) and isinstance(b, dict):
        for k in sorted(set(a) | set(b)):
            if k not in a or k not in b:
                return f"{path}.{k} missing on one side"
            why = _close(a[k], b[k], f"{path}.{k}")
            if why:
                return why
        return None
    if isinstance(a, list) and isinstance(b, list):
        if len(a) != len(b):
            return f"{path} length {len(a)} vs {len(b)}"
        for i, (x, y) in enumerate(zip(a, b)):
            why = _close(x, y, f"{path}[{i}]")
            if why:
                return why
        return None
    if isinstance(a, bool) or isinstance(b, bool):
        return None if bool(a) == bool(b) else f"{path}: {a} vs {b}"
    if isinstance(a, (int, float)) and isinstance(b, (int, float)):
        return None if abs(float(a) - float(b)) <= 1e-3 * max(1.0, abs(float(a)), abs(float(b))) else f"{path}: {a} vs {b}"
    return None if a == b else f"{path}: {a!r} vs {b!r}"


def canonical(g):
    """The genome in the shape RegattaCourseGenome.ToJson writes (defaults filled), for comparing the two sides."""
    out = {"version": g.get("version", VERSION), "intensity": g.get("intensity", 1), "seed": g.get("seed", 0)}
    if g.get("circuit"):
        out["circuit"] = g["circuit"]
    if g.get("rails"):
        out["rails"] = g["rails"]
    out["features"] = g.get("features") or {"Sections": [], "AlternateGates": [], "Crystals": {"PerLap": 0, "Phase": 0.0}}
    out["line_clearance"] = g.get("line_clearance", LINE_CLEARANCE[2])
    out["placements"] = g.get("placements") or []
    out["flora"] = g.get("flora") or dict(FLORA_DEFAULTS, preset="none")
    return out


def parity_inputs(n=40, seed=20261116):
    """Raw (unrepaired) genomes that exercise every repair rule: random courses, storms of mutation, and hand-made
    out-of-range ones."""
    rng = random.Random(seed)
    out = []
    for i in range(n):
        g = random_genome(rng)
        g = copy.deepcopy(g)
        # Push numbers out of range so the C# has to clamp what the Python clamps.
        for p in g["placements"]:
            if rng.random() < 0.4:
                p["scale"] = p["scale"] * rng.choice([0.01, 10.0])
            if rng.random() < 0.3:
                p["offset"][0] += rng.choice([-500.0, 600.0])
            if rng.random() < 0.2:
                p["params"]["no_such_field"] = 3.0
            if rng.random() < 0.3 and p["params"]:
                k = rng.choice(sorted(p["params"]))
                p["params"][k] = p["params"][k] * rng.choice([-5.0, 50.0])
        if rng.random() < 0.3:
            g["placements"].append({"kind": "RegattaRails", "leg": 1.0, "offset": [0, 0, 0], "scale": 50})
        if rng.random() < 0.3:
            g["circuit"] = dict(g.get("circuit") or {}, RingRadius=rng.choice([10.0, 500.0]), Bogus=1.0)
        if rng.random() < 0.2:
            g["line_clearance"] = rng.choice([0.0, 400.0])
        out.append(g)
    out.append(empty())
    return out


def self_test():
    cat = catalogue()
    assert cat["usable"], "the catalogue has no placeable kind"
    # 1. The empty genome stays empty, and repair is idempotent.
    e, notes = repair(empty())
    assert is_empty(e) and not notes, (e, notes)
    rng = random.Random(5)
    for _ in range(12):
        g = random_genome(rng)
        assert _close(canonical(repair(g)[0]), canonical(g)) is None, "repair is not idempotent"
        m = mutate(g, rng, 2.0)
        c = crossover(g, m, rng)
        for x in (m, c):
            assert len(x["placements"]) <= MAX_PLACEMENTS
            assert sum(estimated_prisms(cat["by_id"][p["kind"]], p) for p in x["placements"]) <= TOTAL_PRISM_BUDGET
            assert all(cat["by_id"][p["kind"]]["usable"] for p in x["placements"])
    d = descriptors(random_genome(random.Random(9)))
    assert {"count_by_kind", "mean_scale_by_kind", "leg_share", "min_line_clearance"} <= set(d), d
    # 2. Parity with the C# repair (the harness's fixture: inputs and RegattaCourseGenome.Repair's outputs).
    if not os.path.exists(FIXTURE):
        print(f"regatta_genome self-test: NO FIXTURE at {os.path.relpath(FIXTURE, REPO)} "
              "(bash Tools/Build/regatta_genome_harness/run.sh fixture)")
        return 1
    rows = [json.loads(x) for x in open(FIXTURE, encoding="utf-8") if x.strip()]
    for i, row in enumerate(rows):
        mine = canonical(repair(row["in"])[0])
        why = _close(mine, canonical(row["out"]))
        assert why is None, f"fixture row {i}: Python repair parts from the C# at {why}"
    # 3. Negative controls: the parity check must catch a changed rule.
    global MEMBRANE_CARVE
    keep = MEMBRANE_CARVE
    try:
        MEMBRANE_CARVE = 700.0
        caught = any(_close(canonical(repair(r["in"])[0]), canonical(r["out"])) for r in rows)
    finally:
        MEMBRANE_CARVE = keep
    assert caught, "negative control: a tighter membrane went unnoticed"
    bad = copy.deepcopy(rows[0]["out"])
    bad["line_clearance"] = float(bad["line_clearance"]) + 1.0
    assert _close(canonical(bad), canonical(rows[0]["out"])), "negative control: a moved number went unnoticed"
    print(f"regatta_genome self-test OK: {len(cat['usable'])} placeable kinds, {len(rows)} genomes repaired the same "
          "as the C#, idempotent, mutation and crossover stay legal, 2 negative controls.")
    return 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--parity-inputs" in argv:
        for g in parity_inputs():
            print(json.dumps(g))
        return 0
    if "--random" in argv:
        n = int(argv[argv.index("--random") + 1])
        seed = int(argv[argv.index("--seed") + 1]) if "--seed" in argv else 1
        out = argv[argv.index("--out") + 1] if "--out" in argv else "."
        os.makedirs(out, exist_ok=True)
        rng = random.Random(seed)
        for i in range(n):
            path = os.path.join(out, f"genome_{i + 1:02d}.json")
            json.dump(random_genome(rng), open(path, "w"), indent=1)
            print(path)
        return 0
    print(__doc__)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
