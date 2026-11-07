#!/usr/bin/env python3
"""Regatta's course FEATURES, in Python: the port, the search space, and what each feature costs each hull's model.

THE PORT. A line-for-line port of Assets/_Scripts/Controller/Arcade/Regatta/RegattaCourseFeatures.cs (rail sections
with their kind, neutral colour, gaps and detours; alternate rings; crystals on the line; the AI's route rule) on top
of course_port.py's RegattaCourse.BuildRails, which reads the detours. float32 throughout, like course_port. A feature
set is the C# class as a dict:

    {"Sections": [{"Leg": -1|0-7, "From": 0-1, "To": 0-1, "Kind": 0 super-shielded | 1 shielded | 2 plain | 3 gap,
                   "Neutral": bool, "Detour": u, "DetourBearing": deg}, ...],          later entries win
     "AlternateGates": [{"Ring": 0-7, "Offset": u, "Bearing": deg, "RadiusScale": x}, ...],
     "Crystals": {"PerLap": 0-4, "Phase": 0-1}}

An empty one is today's course, prism for prism.

THE GATE (`--self-test`): the port reproduces Tools/Build/regatta_course_harness/features.json, which the C# harness
(features.sh) writes from the shipped C# for four feature sets at all four intensities: every 4th lane point and leg
param, every prism's plan code, the runs, the alternate rings, the line crystals. Negative controls: a moved detour, a
moved gap and a moved alternate must each FAIL it. Then the models: the empty set changes no hull's time, and each
feature moves the hulls the table below says it moves, in the direction it says.

THE SPACE (`space()`, `features_from_params()`): per leg one section (span, kind, neutral, detour and bearing) and a gap
of a chosen LENGTH in u at the span's centre; per ring an alternate (offset, bearing, mouth scale); crystals per lap and
phase. 82 numbers; every one at its init value is today's course.

WHAT EACH FEATURE COSTS (`race()`; every number is a MODEL, KNOBS are its assumptions, all to be measured in the game):

    feature          Urchin (rail)                Squirrel (skims its lane)       race-line / drive AIs     competent pilot
    neutral stretch  leaves its rail: free speed  none (skims any colour)        none                      none
                     + a re-attach per stretch
    plain/shielded   in a grid, a cut share of    none                           none                      none
                     it rides at destroyed speed
    gap              free speed + re-attach       skim energy runs out: held     none (the line chords it)  none
                                                  KNOBS skim_hold_s, then cruise
    detour           its lane is longer           its lane is longer             their line follows the     none (flies the
                                                                                 braid: longer              spine)
    alternate ring   none (rides the course ring) none                           shorter by > margin: taken shorter: taken
    line crystals    none (Time does not reach)   none                           a Time lift for the hulls Time reaches
                                                                                 (Manta Dolphin Serpent Sparrow Scarab Stoat)

    python3 Tools/Compute/evaluators/regatta_features.py --self-test
    python3 Tools/Compute/evaluators/regatta_features.py --reach        # the table above, measured on the four circuits
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(REPO, "Tools", "Build"))

import course_port as cp  # noqa: E402

f = cp.f
F0, F1 = cp.F0, cp.F1
MAX_LINE_CRYSTALS = 4          # RegattaCourseFeatures.MaxLineCrystalsPerLap
MIN_GAP_PRISMS = 2             # RegattaCourseFeatures.MinGapPrisms
MIN_ALTERNATE_CLEARANCE = 20.0  # RegattaCourseFeatures.MinAlternateClearance
GAP, SUPER, SHIELDED, PLAIN = 3, 0, 1, 2
NUCLEUS, MEMBRANE = 391.9, 1200.0   # RegattaCourse.NucleusWorldRadius; the membrane the shell is cut from (x 0.9 = 1080)

FEATURES_JSON = os.path.join(REPO, "Tools", "Build", "regatta_course_harness", "features.json")


def empty():
    return {"Sections": [], "AlternateGates": [], "Crystals": {"PerLap": 0, "Phase": 0.0}}


def is_empty(features):
    return not features or (not features.get("Sections") and not features.get("AlternateGates")
                            and int((features.get("Crystals") or {}).get("PerLap", 0)) <= 0)


# ── RegattaCourseFeatures.cs ──────────────────────────────────────────────────

def _covers(s, leg, share):
    lo, hi = f(s["From"]), f(s["To"])
    return (int(s["Leg"]) < 0 or int(s["Leg"]) == leg) and hi > lo and lo <= share < hi


def section_at(features, leg, share):
    """TrySectionAt: the section in force (the last that covers), or None."""
    found = None
    for s in (features or {}).get("Sections") or ():
        if _covers(s, leg, share):
            found = s
    return found


def detour_at(features, leg, share):
    """DetourAt: (bow in u, bearing in degrees), float32."""
    s = section_at(features, leg, share)
    if s is None or f(s.get("Detour", 0.0)) == F0 or int(s.get("Kind", 0)) == GAP:
        return F0, F0
    lo, hi = f(s["From"]), f(s["To"])
    u = (share - lo) / (hi - lo)
    bow = cp.fsin(cp.PI * cp.clamp01(u))
    return f(s["Detour"]) * bow * bow, f(s.get("DetourBearing", 0.0))


def plan_for_lane(params, legs, features):
    """RegattaRailPlan.ForLane: (codes, runs). Codes per prism: s/h/p (super-shielded/shielded/plain), upper case when
    neutral, '_' for a gap; runs [(start, count, closed)]."""
    n = len(params)
    kinds, neutral, gap = [SUPER] * n, [False] * n, [False] * n
    any_gap = False
    has = bool(features and features.get("Sections"))
    for k in range(n):
        if not has:
            continue
        leg = min(max(int(params[k]), 0), max(0, legs - 1))
        s = section_at(features, leg, params[k] - f(leg))
        if s is None:
            continue
        neutral[k] = bool(s.get("Neutral", False))
        gap[k] = int(s.get("Kind", 0)) == GAP
        kinds[k] = {SHIELDED: SHIELDED, PLAIN: PLAIN}.get(int(s.get("Kind", 0)), SUPER)
        any_gap |= gap[k]
    if any_gap:
        for k in range(n):
            if not gap[k] or gap[(k - 1) % n]:
                continue
            length = 0
            while length < n and gap[(k + length) % n]:
                length += 1
            if length >= MIN_GAP_PRISMS or length >= n:
                continue
            for q in range(length):
                gap[(k + q) % n] = False
    first = next((k for k in range(n) if not gap[k] and gap[(k - 1) % n]), -1)
    runs = []
    if first < 0:
        if n > 0 and not gap[0]:
            runs.append((0, n, True))
    else:
        q = 0
        while q < n:
            k = (first + q) % n
            if gap[k]:
                q += 1
                continue
            count = 0
            while q + count < n and not gap[(first + q + count) % n]:
                count += 1
            runs.append((k, count, False))
            q += count
    codes = "".join("_" if gap[k] else ("shp"[kinds[k]].upper() if neutral[k] else "shp"[kinds[k]]) for k in range(n))
    return codes, runs


def alternate_for(gates, ring, spec):
    """AlternateFor on course_port's float32 gates ({"p", "a", "r"} tuples)."""
    n = len(gates)
    g = gates[ring]
    axis = cp.safe_normalize(g["a"], cp.FORWARD)
    mid = cp.sub(cp.mul(cp.add(gates[(ring - 1) % n]["p"], gates[(ring + 1) % n]["p"]), f(0.5)), g["p"])
    inside = cp.sub(mid, cp.mul(axis, cp.dot(mid, axis)))
    inside = cp.safe_normalize(inside, cp.perpendicular(axis))
    direction = cp.rotate_about(inside, axis, f(spec.get("Bearing", 0.0)) * cp.DEG2RAD)
    scale = f(spec.get("RadiusScale", 1.0))
    scale = scale if scale > F0 else F1
    radius = f(g["r"]) * scale
    offset = max(f(spec["Offset"]), f(g["r"]) + radius + f(MIN_ALTERNATE_CLEARANCE))
    return {"p": cp.add(g["p"], cp.mul(direction, offset)), "a": axis, "r": radius}


def try_alternate(features, gates, ring):
    n = len(gates)
    if n < 3:
        return None
    for spec in (features or {}).get("AlternateGates") or ():
        if (int(spec["Ring"]) % n + n) % n == ring and f(spec["Offset"]) > F0:
            return alternate_for(gates, ring, spec)
    return None


def line_crystal_points(rails, features):
    """LineCrystalPoints: crystals on the spine, evenly by arc from the phase, capped."""
    c = (features or {}).get("Crystals") or {}
    count = min(int(c.get("PerLap", 0)), MAX_LINE_CRYSTALS)
    spine, arc = rails.get("spinePositions"), rails.get("spineArc")
    length = rails.get("length32", f(rails["spineLength"]))
    if count <= 0 or not spine or len(spine) < 2 or length < F1:
        return []
    phase = f(c.get("Phase", 0.0))
    phase = phase - f(math.floor(float(phase)))
    out, m = [], len(spine)
    for k in range(count):
        target = f(math.fmod(float(phase + f(k) / f(count)), 1.0)) * length
        j = 0
        while j < m - 1 and arc[j + 1] < target:
            j += 1
        seg = arc[j + 1] - arc[j]
        u = cp.clamp01((target - arc[j]) / seg) if seg > f(1e-6) else F0
        out.append(cp.lerp3(spine[j], spine[(j + 1) % m], u))
    return out


def takes_alternate(self_p, ring, alternate, nxt, margin):
    """RegattaAlternateChoice.TakesAlternate."""
    d = lambda a, b: math.dist(a, b)
    via_ring = d(ring, self_p) + d(nxt, ring)
    via_alt = d(alternate, self_p) + d(nxt, alternate)
    return via_alt < via_ring * (1.0 - min(max(margin, 0.0), 1.0))


# ── the course, with its features ─────────────────────────────────────────────

def _fl(v):
    return [float(x) for x in v]


def course(intensity=1, features=None, settings=None, base_seed=cp.REGATTA_DEFAULT_SEED, seed=None):
    """course_port.course plus the features: `lanes` are the LAID prisms of each lane in SpawnableRegattaRails.LanePrisms
    order (runs joined; what every AI reader of the rails sees), `laneLengths` the braid's (detoured) lane lengths, and
    `features` / `plans` / `segments` / `alternates` / `crystals` / `plainLaneLengths` for the models."""
    features = features or empty()
    settings = settings or cp.regatta_settings(intensity)
    seed = cp.seed_for_intensity(base_seed, intensity) if seed is None else seed
    gates = cp.generate(seed, settings)
    out = cp.course(settings, seed, rails=False)
    rails = cp.build_rails(gates, features=features)
    plain = rails if not any(f(s.get("Detour", 0.0)) != F0 for s in features.get("Sections") or ()) \
        else cp.build_rails(gates, features=empty())
    n = len(gates)
    lanes, plans, segments = [], [], []
    for lane, pts in enumerate(rails["lanes"]):
        codes, runs = plan_for_lane(rails["legParams"][lane], n, features)
        laid = []
        for start, count, _closed in runs:
            laid.extend(_fl(pts[(start + q) % len(pts)]) for q in range(count))
        lanes.append(laid)
        plans.append({"codes": codes, "runs": runs})
        segments.append(_segments(pts, codes))
    out.update({
        "spineLength": rails["spineLength"], "laneLengths": rails["laneLengths"], "lanes": lanes,
        "plainLaneLengths": plain["laneLengths"], "features": features, "plans": plans, "segments": segments,
        "legArc": [float(rails["spineArc"][(i + 1) * 64] - rails["spineArc"][i * 64]) for i in range(n)],
        "alternates": [], "crystals": [_fl(p) for p in line_crystal_points(rails, features)],
    })
    for g in range(n):
        alt = try_alternate(features, gates, g)
        if alt is not None:
            out["alternates"].append({"ring": g, "p": _fl(alt["p"]), "a": _fl(alt["a"]), "r": float(alt["r"])})
    return out


def _segments(pts, codes):
    """Share of the lane's length by what it is made of, and how many free stretches (neutral or gap) it has: the
    rail rider's ledger. A segment k -> k+1 is what prism k is (a gap if either end is one)."""
    m = len(pts)
    by, total = {}, 0.0
    for k in range(m):
        d = float(cp.magnitude(cp.sub(pts[(k + 1) % m], pts[k])))
        code = "_" if "_" in (codes[k], codes[(k + 1) % m]) else codes[k]
        by[code] = by.get(code, 0.0) + d
        total += d
    free = [c == "_" or c.isupper() for c in codes]
    stretches = sum(1 for k in range(m) if free[k] and not free[(k - 1) % m]) if any(free) and not all(free) else 0
    gaps = [c == "_" for c in codes]
    gap_runs = []
    if any(gaps) and not all(gaps):
        for k in range(m):
            if gaps[k] and not gaps[(k - 1) % m]:
                length = 0
                while gaps[(k + length) % m]:
                    length += 1
                gap_runs.append(length)
    return {"share": {c: v / total for c, v in by.items()}, "free_stretches": stretches,
            "gap_lengths": [g * total / m for g in gap_runs]}


# ── the search space ──────────────────────────────────────────────────────────

LEGS = RINGS = 8


def space():
    """The job space (froglet-compute/job/1 `space` entries). Every init value lays today's course."""
    out = []
    for k in range(LEGS):
        out += [{"name": f"leg{k}_from", "lo": 0.0, "hi": 0.9, "init": 0.2},
                {"name": f"leg{k}_len", "lo": 0.0, "hi": 1.0, "init": 0.0},
                {"name": f"leg{k}_kind", "lo": 0, "hi": 2, "init": 0, "type": "int"},
                {"name": f"leg{k}_neutral", "lo": 0, "hi": 1, "init": 0, "type": "int"},
                {"name": f"leg{k}_detour", "lo": 0.0, "hi": 150.0, "init": 0.0},
                {"name": f"leg{k}_bearing", "lo": 0.0, "hi": 360.0, "init": 0.0},
                {"name": f"leg{k}_gap", "lo": 0.0, "hi": 240.0, "init": 0.0}]
    for k in range(RINGS):
        out += [{"name": f"alt{k}_offset", "lo": 0.0, "hi": 420.0, "init": 0.0},
                {"name": f"alt{k}_bearing", "lo": 0.0, "hi": 360.0, "init": 0.0},
                {"name": f"alt{k}_scale", "lo": 0.6, "hi": 1.4, "init": 1.0}]
    out += [{"name": "crystals", "lo": 0, "hi": MAX_LINE_CRYSTALS, "init": 0, "type": "int"},
            {"name": "crystal_phase", "lo": 0.0, "hi": 1.0, "init": 0.0}]
    return out


def baseline_params():
    return {p["name"]: p["init"] for p in space()}


def features_from_params(params, leg_arc):
    """The search's numbers -> a feature set. `leg_arc`: each leg's spine arc (course()["legArc"]), which turns a gap's
    LENGTH in u into the share of its leg the C# reads. A stretch that changes nothing is left out, so the init values
    are the empty set (today's course)."""
    feats = empty()
    g = lambda k, d=0.0: float(params.get(k, d))
    for k in range(LEGS):
        lo = min(max(g(f"leg{k}_from", 0.2), 0.0), 0.95)
        hi = min(lo + max(g(f"leg{k}_len"), 0.0), 1.0)
        kind = int(round(g(f"leg{k}_kind")))
        neutral = int(round(g(f"leg{k}_neutral"))) >= 1
        detour = max(g(f"leg{k}_detour"), 0.0)
        if hi > lo and (kind in (SHIELDED, PLAIN) or neutral or detour >= 1.0):
            feats["Sections"].append({"Leg": k, "From": round(lo, 4), "To": round(hi, 4),
                                      "Kind": kind if kind in (SHIELDED, PLAIN) else SUPER, "Neutral": neutral,
                                      "Detour": round(detour, 1) if detour >= 1.0 else 0.0,
                                      "DetourBearing": round(g(f"leg{k}_bearing") % 360.0, 1)})
        gap = max(g(f"leg{k}_gap"), 0.0)
        if gap >= 8.0 * MIN_GAP_PRISMS and k < len(leg_arc) and leg_arc[k] > 1.0:
            mid = 0.5 * (lo + hi) if hi > lo else 0.5
            half = min(0.5 * gap / leg_arc[k], 0.45)
            mid = min(max(mid, 0.05 + half), 0.95 - half)   # never at a ring: the rails must thread every mouth
            feats["Sections"].append({"Leg": k, "From": round(mid - half, 4), "To": round(mid + half, 4), "Kind": GAP,
                                      "Neutral": False, "Detour": 0.0, "DetourBearing": 0.0})
    for k in range(RINGS):
        off = g(f"alt{k}_offset")
        if off >= 1.0:
            feats["AlternateGates"].append({"Ring": k, "Offset": round(off, 1),
                                            "Bearing": round(g(f"alt{k}_bearing") % 360.0, 1),
                                            "RadiusScale": round(min(max(g(f"alt{k}_scale", 1.0), 0.6), 1.4), 3)})
    feats["Crystals"] = {"PerLap": min(max(int(round(g("crystals"))), 0), MAX_LINE_CRYSTALS),
                         "Phase": round(g("crystal_phase") % 1.0, 4)}
    return feats


def descriptors(fc):
    """Quality-diversity descriptors of a featured course (brief 12's archive axes)."""
    seg = fc["segments"][0]["share"]
    return {"ridable_share": round(sum(v for c, v in seg.items() if c in "shp"), 3),
            "contested_share": round(sum(v for c, v in seg.items() if c in "hpHP"), 3),
            "gap_count": len(fc["segments"][0]["gap_lengths"]),
            "detour_u": round(sum(float(s.get("Detour", 0.0)) for s in fc["features"]["Sections"]), 1),
            "alternates": len(fc["alternates"]), "crystals": len(fc["crystals"])}


def guard_fails(fc):
    """Shell guard: a detoured rail or an alternate ring inside the nucleus or out at the membrane, and every rail
    through every mouth (rm.rail_coverage over the LAID lanes, so a gap at a ring fails)."""
    import race_models as rm
    bad = rm.rail_coverage(fc)
    for lane in fc["lanes"]:
        for q in lane[::4]:
            r = math.sqrt(q[0] * q[0] + q[1] * q[1] + q[2] * q[2])
            if r < NUCLEUS + 20.0 or r > MEMBRANE - 30.0:
                bad += 1
                break
    for a in fc["alternates"]:
        r = math.sqrt(sum(x * x for x in a["p"]))
        if r - a["r"] < NUCLEUS + 20.0 or r + a["r"] > MEMBRANE - 30.0:
            bad += 1
    return bad


# ── what the features cost each hull's model ──────────────────────────────────

KNOBS = {
    # The Urchin off its rail flies free at about its cruise (the overnight branch measured ~65 u/s), rides a destroyed
    # prism at its DestroyedTerrainSpeed (150, Urchin.prefab), and pays this long to latch its rail again.
    "urchin_free": 65.0, "urchin_destroyed": 150.0, "reattach_s": 0.5,
    # In a GRID, the share of a contested stretch that rivals have cut by the time the rider arrives (a guess).
    "cut_share": {"p": 0.5, "h": 0.1, "s": 0.0},
    # Off a rail the Squirrel's skim boost decays at its prefab's BoostDecayRate (VesselTransformer.DecayBoost: 0.3 / s
    # of multiplier, x its 60 throttle scaler = 18 u/s^2), and winning the lost boost back costs this many times the
    # time the decay cost (a guess until a gapped Regatta flight is measured).
    "skim_regain": 1.0,
    # The AI's alternate-ring margin (RegattaController.aiAlternateMargin); a competent pilot takes any shorter way.
    "alt_margin": 0.05,
    # A line crystal lifts the collector's Time by this many levels, and a racer collects this share of the crystals it
    # passes (they are contested and respawn in place). Garrett: "use sparingly ... likely noise".
    "crystal_levels": 1.0, "crystal_collect": 0.5,
    "grid": True,
}

RIDERS = ("Urchin",)
SKIMMERS = ("Squirrel",)


def _rb():
    import regatta_balance as RB
    return RB


def race(hull, model, fc, intensity=1, drive=None, steering="centre", limit=400.0, laps=3, knobs=None):
    """race_models.race on a featured course, priced for the features (module doc's table). Exactly race_models.race
    when the feature set is empty."""
    import race_models as rm
    k = dict(KNOBS, **(knobs or {}))
    drive = drive or {}
    feats = fc.get("features") or empty()
    if model == "rail":
        return _rail(fc, drive, k, laps)
    c = fc
    if model == "balance":
        c = dict(rm.balance_course(fc), gates=fc["gates"])
    t, raw = rm.race(hull, model, c, intensity, drive=drive, steering=steering, limit=limit, laps=laps)
    if is_empty(feats) or raw.get("time") is None:
        return t, raw
    extra = 0.0
    v_mean = fc["spineLength"] * laps / t
    if hull in SKIMMERS:
        extra += _skim(hull, fc, drive, k, laps)
    elif model != "balance":
        # A race-line AI follows the braid: a detour lengthens its line by what it lengthens the lanes. The line model
        # sees the detoured lanes itself; the drive models fly ring to ring and are told here.
        if model != "line":
            extra += _detour_extra(fc) * laps / v_mean
    if hull not in RIDERS and hull not in SKIMMERS:
        margin = 0.0 if model == "balance" else k["alt_margin"]
        extra += _alternate_saving(fc, margin) * laps / v_mean
    t2 = (t + extra) * _crystal_ratio(hull, fc, drive, k, intensity)
    return t2, dict(raw, time=t2, base_time=t, features_extra=round(t2 - t, 3))


def _rail(fc, drive, k, laps):
    lane = int(drive.get("lane", 0))
    speed = float(drive.get("speed", 300.0))
    length = fc["laneLengths"][lane]
    seg = fc["segments"][lane]
    if is_empty(fc.get("features")):
        t = length * laps / speed
        return t, {"time": t, "rings": len(fc["gates"]) * laps}
    cut = k["cut_share"] if k.get("grid") else {}
    per = 0.0
    for code, share in seg["share"].items():
        if code == "_" or code.isupper():
            v = k["urchin_free"]
        else:
            c = cut.get(code, 0.0)
            v = 1.0 / ((1.0 - c) / speed + c / k["urchin_destroyed"]) if c > 0 else speed
        per += share / v
    t = laps * (length * per + seg["free_stretches"] * k["reattach_s"])
    return t, {"time": t, "rings": len(fc["gates"]) * laps}


def _detour_extra(fc):
    lanes, plain = fc["laneLengths"], fc["plainLaneLengths"]
    return sum(a - b for a, b in zip(lanes, plain)) / len(lanes)


def _skim(hull, fc, drive, k, laps):
    """The Squirrel follows its lane: a detour is extra path at its skim top speed, and each gap decays its skim boost
    (VesselTransformer.DecayBoost) until the next rail, then it wins the boost back (KNOBS skim_regain)."""
    RB = _rb()
    v = RB.read_constants()["vessels"][hull]
    model = RB.MODELS[hull](RB.read_constants(), int(drive.get("time_level", 0)))
    top, cruise = model["top"], v["ts"] + v["ms"]
    decel = v["ts"] * _boost_decay(hull)
    lane = int(drive.get("lane", 0))
    extra = (fc["laneLengths"][lane] - fc["plainLaneLengths"][lane]) / top
    for gap in fc["segments"][lane]["gap_lengths"]:
        t_floor = (top - cruise) / decel if decel > 0 else float("inf")
        x_floor = top * t_floor - 0.5 * decel * t_floor * t_floor
        if gap <= x_floor:   # gap = top t - decel t^2 / 2
            t_gap = (top - math.sqrt(max(0.0, top * top - 2.0 * decel * gap))) / decel if decel > 0 else gap / top
        else:
            t_gap = t_floor + (gap - x_floor) / cruise
        extra += (t_gap - gap / top) * (1.0 + k["skim_regain"])
    return extra * laps


def _boost_decay(hull):
    RB = _rb()
    try:
        return RB._first_key(RB._read(f"Assets/_Prefabs/Spacevessels/{hull}.prefab"), "BoostDecayRate", hull)
    except Exception:
        return 0.1   # VesselTransformer's default


def _alternate_saving(fc, margin):
    """Per lap, the path an AI (or a pilot) saves by the alternate rings it takes: negative u."""
    gates = fc["gates"]
    n = len(gates)
    saved = 0.0
    for a in fc["alternates"]:
        i = a["ring"]
        prev, ring, nxt = gates[(i - 1) % n]["p"], gates[i]["p"], gates[(i + 1) % n]["p"]
        if takes_alternate(prev, ring, a["p"], nxt, margin):
            saved += (math.dist(prev, a["p"]) + math.dist(a["p"], nxt)) - (math.dist(prev, ring) + math.dist(ring, nxt))
    return saved


def _crystal_ratio(hull, fc, drive, k, intensity):
    RB = _rb()
    if not fc["crystals"] or hull not in RB.TUNABLE:
        return 1.0
    c = RB.read_constants()
    levels = (drive.get("time_levels") or {}).get(str(intensity), drive.get("time_level", 0))
    level = float(levels or 0)
    lift = min(10.0 - level, len(fc["crystals"]) * k["crystal_collect"] * k["crystal_levels"])
    if lift <= 0:
        return 1.0
    import race_models as rm
    bc = rm.balance_course(fc)
    before = RB.lap_time(hull, RB.MODELS[hull](c, level), bc)
    after = RB.lap_time(hull, RB.MODELS[hull](c, level + lift), bc)
    return after / before


# ── the reach table ───────────────────────────────────────────────────────────

ROSTER = {   # bleeding-edge's regatta-course job roster, plus the competent-pilot model for the hulls without an AI model
    "Urchin": ("rail", {"speed": 300}), "Squirrel": ("balance", {}), "Rhino": ("rhino_drive", None),
    "Manta": ("manta_drive", None), "Dolphin": ("dolphin_drive", None), "Serpent": ("balance", {}),
    "Sparrow": ("balance", {}), "Stoat": ("balance", {}),
}

PROBES = {
    "neutral 50% of every leg": {"Sections": [{"Leg": -1, "From": 0.25, "To": 0.75, "Kind": 0, "Neutral": True,
                                               "Detour": 0.0, "DetourBearing": 0.0}]},
    "plain rail 50% of every leg": {"Sections": [{"Leg": -1, "From": 0.25, "To": 0.75, "Kind": PLAIN, "Neutral": False,
                                                  "Detour": 0.0, "DetourBearing": 0.0}]},
    "a 120 u gap mid every leg": "gap120",
    "a 100 u detour on every leg": {"Sections": [{"Leg": -1, "From": 0.15, "To": 0.85, "Kind": 0, "Neutral": False,
                                                  "Detour": 100.0, "DetourBearing": 90.0}]},
    "an alternate inside every corner (250 u)": {"AlternateGates": [{"Ring": r, "Offset": 250.0, "Bearing": 0.0,
                                                                     "RadiusScale": 1.0} for r in range(8)]},
    "4 crystals on the line": {"Crystals": {"PerLap": 4, "Phase": 0.06}},
}


def _probe(name, intensity):
    spec = PROBES[name]
    if spec == "gap120":
        base = course(intensity)
        p = {f"leg{k}_gap": 120.0 for k in range(8)}
        return features_from_params(p, base["legArc"])
    return dict(empty(), **spec)


def _roster_drives():
    import race_models as rm
    out = {}
    for hull, (model, drive) in ROSTER.items():
        if drive is None:
            asset = {"rhino_drive": "Assets/Resources/AIBoostPolicies/RhinoAIBoostPolicy_Regatta.asset",
                     "manta_drive": "Assets/Resources/AIBoostPolicies/MantaSoarAITuning_Regatta.asset"}.get(model)
            drive = rm.to_sim(model, rm.read_asset_fields(REPO, asset)) if asset else {}
        out[hull] = (model, drive)
    return out


def reach(intensities=(1, 2, 3, 4), hulls=None, knobs=None):
    """Seconds each probe feature adds to each hull's 3-lap race, mean over the intensities (a MODEL)."""
    drives = _roster_drives()
    hulls = hulls or list(drives)
    base = {}
    for i in intensities:
        c = course(i)
        for h in hulls:
            m, d = drives[h]
            base[(i, h)] = race(h, m, c, i, d, knobs=knobs)[0]
    table = {}
    for name in PROBES:
        row = {}
        for i in intensities:
            c = course(i, _probe(name, i))
            for h in hulls:
                m, d = drives[h]
                row.setdefault(h, []).append(race(h, m, c, i, d, knobs=knobs)[0] - base[(i, h)])
        table[name] = {h: sum(v) / len(v) for h, v in row.items()}
    return {"base": {h: sum(base[(i, h)] for i in intensities) / len(intensities) for h in hulls}, "delta": table}


# ── the gate ──────────────────────────────────────────────────────────────────

def _parity(fixture, sets=None, mutate=None):
    bad = []
    r1 = lambda x: round(float(x) + 0.0, 1)
    for name, entry in fixture["sets"].items():
        if sets and name not in sets:
            continue
        feats = json.loads(json.dumps(entry["features"]))
        if mutate:
            mutate(name, feats)
        for i in ("1", "2", "3", "4"):
            ref = entry[i]
            settings = cp.regatta_settings(int(i))
            gates = cp.generate(cp.seed_for_intensity(cp.REGATTA_DEFAULT_SEED, int(i)), settings)
            rails = cp.build_rails(gates, features=feats)
            if abs(r1(rails["spineLength"]) - ref["spine"]) > 0.11:
                bad.append(f"{name} I{i}: spine {rails['spineLength']:.1f} vs {ref['spine']}")
            for lane, lr in enumerate(ref["lanes"]):
                pts, par = rails["lanes"][lane], rails["legParams"][lane]
                if len(pts) != lr["count"]:
                    bad.append(f"{name} I{i} lane {lane}: {len(pts)} prisms vs {lr['count']}")
                    continue
                flat = [r1(x) for p in pts[::4] for x in p]
                if max(abs(a - b) for a, b in zip(flat, lr["every4"])) > 0.11:
                    bad.append(f"{name} I{i} lane {lane}: a lane point is off")
                if max(abs(round(float(a), 4) - b) for a, b in zip(par[::4], lr["params4"])) > 0.00011:
                    bad.append(f"{name} I{i} lane {lane}: a leg param is off")
                codes, runs = plan_for_lane(par, len(gates), feats)
                if codes != lr["plan"]:
                    first = next(k for k, (a, b) in enumerate(zip(codes, lr["plan"])) if a != b) if len(codes) == len(lr["plan"]) else -1
                    bad.append(f"{name} I{i} lane {lane}: plan differs (first at prism {first})")
                if [list((s, c, 1 if cl else 0)) for s, c, cl in runs] != lr["runs"]:
                    bad.append(f"{name} I{i} lane {lane}: runs {runs} vs {lr['runs']}")
            alts = [(g, try_alternate(feats, gates, g)) for g in range(len(gates))]
            alts = [(g, a) for g, a in alts if a is not None]
            if len(alts) != len(ref["alternates"]):
                bad.append(f"{name} I{i}: {len(alts)} alternates vs {len(ref['alternates'])}")
            for (g, a), ra in zip(alts, ref["alternates"]):
                if g != ra["ring"] or max(abs(r1(x) - y) for x, y in zip(a["p"], ra["p"])) > 0.11 \
                        or abs(r1(a["r"]) - ra["r"]) > 0.11:
                    bad.append(f"{name} I{i}: alternate at ring {g} off")
            cr = line_crystal_points(rails, feats)
            if len(cr) != len(ref["crystals"]) or any(max(abs(r1(x) - y) for x, y in zip(p, q)) > 0.11
                                                       for p, q in zip(cr, ref["crystals"])):
                bad.append(f"{name} I{i}: line crystals off")
    return bad


def self_test():
    bad = []
    fixture = json.load(open(FEATURES_JSON, encoding="utf-8"))
    bad += _parity(fixture)

    # Negative controls: the gate must see a moved detour, a moved gap and a moved alternate.
    def move_detour(name, fe):
        for s in fe["Sections"]:
            if s["Detour"]:
                s["Detour"] += 2.0

    def move_gap(name, fe):
        for s in fe["Sections"]:
            if s["Kind"] == GAP:
                s["From"] += 0.03

    def move_alt(name, fe):
        for a in fe["AlternateGates"]:
            a["Bearing"] += 3.0

    for label, sets, mut in (("detour", ["detour"], move_detour), ("gap", ["gaps"], move_gap),
                             ("alternate", ["busy"], move_alt)):
        if not _parity(fixture, sets, mut):
            bad.append(f"negative control: a moved {label} reproduced the C# fixture (the gate cannot see it)")

    # Today's course: the empty feature set is course_port's course, and moves no hull's time.
    import race_models as rm
    c0, ship = course(2), cp.regatta_course(2)
    if c0["lanes"] != ship["lanes"] or c0["laneLengths"] != ship["laneLengths"]:
        bad.append("the empty feature set does not lay course_port's course")
    params = baseline_params()
    if not is_empty(features_from_params(params, c0["legArc"])):
        bad.append("the space's init values are not today's course")
    for hull, model, drive in (("Urchin", "rail", {"speed": 300}), ("Squirrel", "balance", {}),
                               ("Sparrow", "balance", {})):
        mine = race(hull, model, c0, 2, drive)[0]
        theirs = rm.race(hull, model, dict(rm.balance_course(ship), gates=ship["gates"]) if model == "balance" else ship,
                         2, drive=drive)[0]
        if abs(mine - theirs) > 1e-9:
            bad.append(f"{hull}: the empty feature set moved its time ({mine:.3f} vs {theirs:.3f})")

    # Each feature reaches the hulls the table says, in the direction it says (the competent-pilot and rail models;
    # the drive sims are slower and covered by --reach).
    fast = {"Urchin": ("rail", {"speed": 300}), "Squirrel": ("balance", {}), "Sparrow": ("balance", {})}
    base = {h: race(h, m, c0, 2, d)[0] for h, (m, d) in fast.items()}

    def delta(name):
        c = course(2, _probe(name, 2))
        return {h: race(h, m, c, 2, d)[0] - base[h] for h, (m, d) in fast.items()}

    expect = {
        "neutral 50% of every leg": {"Urchin": +1, "Squirrel": 0, "Sparrow": 0},
        "plain rail 50% of every leg": {"Urchin": +1, "Squirrel": 0, "Sparrow": 0},
        "a 120 u gap mid every leg": {"Urchin": +1, "Squirrel": +1, "Sparrow": 0},
        "a 100 u detour on every leg": {"Urchin": +1, "Squirrel": +1, "Sparrow": 0},
        "an alternate inside every corner (250 u)": {"Urchin": 0, "Squirrel": 0, "Sparrow": -1},
        "4 crystals on the line": {"Urchin": 0, "Squirrel": 0, "Sparrow": -1},
    }
    for name, want in expect.items():
        got = delta(name)
        for h, sign in want.items():
            d = got[h]
            ok = abs(d) < 1e-6 if sign == 0 else (d > 0.05 if sign > 0 else d < -0.05)
            if not ok:
                bad.append(f"{name}: {h} moved {d:+.2f} s, the table says {'none' if sign == 0 else ('slower' if sign > 0 else 'faster')}")
    # And the guard sees a rail removed at a mouth.
    gap_at_ring = dict(empty(), Sections=[{"Leg": leg, "From": lo, "To": hi, "Kind": GAP, "Neutral": False,
                                           "Detour": 0.0, "DetourBearing": 0.0} for leg, lo, hi in ((2, 0.8, 1.0),
                                                                                                    (3, 0.0, 0.2))])
    if guard_fails(course(2, gap_at_ring)) == 0:
        bad.append("guard: a gap at a ring passes the mouth coverage check")
    if guard_fails(c0):
        bad.append("guard: today's course fails the guard")

    print("regatta_features self-test: " + ("ok (the C# fixture at 4 intensities x 4 feature sets, 3 negative controls, "
                                            "today's course unchanged, every feature moves the hulls its table says)"
                                            if not bad else "FAILED\n  " + "\n  ".join(bad[:30])))
    return 0 if not bad else 1


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        sys.exit(self_test())
    if "--reach" in sys.argv:
        r = reach()
        hulls = list(r["base"])
        print("seconds added to a 3-lap race, mean of I1-I4 (a MODEL; KNOBS in this file are its assumptions)")
        print(f"{'feature':44s}" + "".join(f"{h:>9s}" for h in hulls))
        print(f"{'(race today)':44s}" + "".join(f"{r['base'][h]:9.1f}" for h in hulls))
        for name, row in r["delta"].items():
            print(f"{name:44s}" + "".join(f"{row[h]:+9.1f}" for h in hulls))
        if "--json" in sys.argv:
            print(json.dumps(r, indent=1))
        sys.exit(0)
    print(__doc__)
