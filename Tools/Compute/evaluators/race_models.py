"""Race any hull on any circuit with the models the AI team already trusts, for the compute evaluators.

The models are NOT copied here: every one is the shipped offline model in Tools/Build, imported from the job's own
checkout (so a job pinned to the ai branch races its AI):

    rhino_drive    Tools/Build/rhino_drive_sim.py    RampBoostAIPolicySO's ramp / brake drive (Policy fields)
    manta_drive    Tools/Build/manta_drive_sim.py    the Soar drive's stick bands (and Tuning when `aware`)
    dolphin_drive  Tools/Build/dolphin_drive_sim.py  ChargeBoostAIPolicySO's discharge drive (module constants)
    line           Tools/Build/regatta_line_sim.py   the race line + AIPilot's orbit break-off at the hull's AI speed
                                                     (Regatta's rails only)
    rail           a rider on its own lane: lane length x laps / rail speed (the Urchin's grind, 300 u/s measured)
    balance        Tools/Build/regatta_balance.py    the competent-pilot lap model (every hull, including the Squirrel
                                                     and the Stoat, which have no AI race model yet)

Every sim reads its course through situation_features.course_from(load_course_js(), intensity). `use_course` swaps
that one function for the duration of a call, so a sim races a course it has never seen without being edited.

What a model does NOT see is listed in MODEL_LIMITS; the evaluators copy it into every result's metrics, so a number
on the board never travels without its caveat.
"""
import contextlib
import math
import os
import sys

MODEL_LIMITS = {
    "rhino_drive": "point mass; no orbit break-off (a circling AI stalls); proportional stick; no rivals",
    "manta_drive": "point mass; no break-off, no nose lag; Regatta's through-the-centre steering in every mode",
    "dolphin_drive": "point mass; no break-off; the scalar charge economy (DOLPHIN_ENERGY_ECONOMY.md 2)",
    "line": "constant speed (the hull's median AI speed in editor Regatta races); Regatta rails only",
    "rail": "constant rail speed, no corners (the rail rides them), no lane changes",
    "balance": "a competent pilot, not an AI: top speed, corner speed solved per radius, instant braking",
}
DNF_BASE, DNF_PER_RING = 30.0, 10.0      # a race not finished costs the time limit + 30 s + 10 s per ring left


@contextlib.contextmanager
def use_course(course):
    import situation_features as sf
    old_load, old_from = sf.load_course_js, sf.course_from
    sf.load_course_js = lambda *a, **k: {}
    sf.course_from = lambda C, intensity: course
    try:
        yield
    finally:
        sf.load_course_js, sf.course_from = old_load, old_from


def _sim(name):
    return __import__(name)


def race(hull, model, course, intensity=1, drive=None, steering="centre", limit=400.0, laps=3):
    """Seconds for `laps` laps (a DNF is priced: limit + DNF_BASE + DNF_PER_RING x rings left), and the raw result."""
    drive = drive or {}
    n = len(course["gates"])
    total = n * laps
    if model == "rail":
        lane = drive.get("lane", 0)
        speed = float(drive.get("speed", 300.0))
        t = course["laneLengths"][lane] * laps / speed
        return t, {"time": t, "rings": total}
    if model == "balance":
        import regatta_balance as RB
        c = _balance_constants()
        level = int(drive.get("time_level", 0))
        lap = RB.lap_time(hull, RB.MODELS[hull](c, level), course)
        return lap * laps, {"time": lap * laps, "rings": total}
    with use_course(course):
        if model == "rhino_drive":
            sim = _sim("rhino_drive_sim")
            sim.THROUGH_CENTRE = steering == "centre"
            for k, v in drive.items():
                setattr(sim.Policy, k, v)
            t, rings, top = sim.race("new", intensity, T=limit)
        elif model == "manta_drive":
            sim = _sim("manta_drive_sim")
            for k, v in drive.items():
                if k not in ("boost_band", "yastri_band", "aware"):
                    setattr(sim.Tuning, k, v)
            bands = (drive.get("boost_band", sim.BOOST_BAND), drive.get("yastri_band", sim.YASTRI_BAND))
            t, rings, top = sim.race(bool(drive.get("aware", False)), intensity, T=limit, bands=bands)
        elif model == "dolphin_drive":
            sim = _sim("dolphin_drive_sim")
            sim.THROUGH_CENTRE = steering == "centre"
            for k, v in drive.items():
                setattr(sim, k.upper(), v)
            t, rings, top = sim.race(True, intensity, T=limit)
        elif model == "line":
            sim = _sim("regatta_line_sim")
            r = sim.race(hull, intensity, True, T=limit, gate=True, only_when_cut=True)
            t, rings, top = r["time"], r["rings"], None
        else:
            raise ValueError(f"unknown race model {model}")
    if t is None:
        return limit + DNF_BASE + DNF_PER_RING * (total - rings), {"time": None, "rings": rings, "top": top}
    return float(t), {"time": float(t), "rings": rings, "top": top}


_BAL = {}


def _balance_constants():
    if "c" not in _BAL:
        import regatta_balance as RB
        _BAL["c"] = RB.read_constants()
    return _BAL["c"]


def balance_course(course):
    """The keys regatta_balance.lap_time reads, from a course_port course."""
    return {"legLengths": course["legLengths"], "cornerRadii": course["cornerRadii"],
            "spineLength": course["spineLength"], "laneLengths": course["laneLengths"]}


def rail_coverage(course, lane_offset=22.0, spacing=8.0):
    """RegattaCourseTests' gate coverage, as the course harness's Driver checks it: every lane passes through every
    mouth at its offset with a prism's clearance. Returns the number of (gate, lane) pairs that fail."""
    fails = 0
    for g in course["gates"]:
        p, a, r = g["p"], g["a"], g["r"]
        for lane in course["lanes"]:
            best, plane, lat = 1e18, 0.0, 0.0
            for q in lane[::2]:
                d = (q[0] - p[0], q[1] - p[1], q[2] - p[2])
                dist = d[0] * d[0] + d[1] * d[1] + d[2] * d[2]
                if dist < best:
                    best = dist
                    along = d[0] * a[0] + d[1] * a[1] + d[2] * a[2]
                    plane = abs(along)
                    lat = math.sqrt(max(0.0, dist - along * along))
            if plane > spacing * 2 or abs(lat - lane_offset) > spacing or lat + 6.0 > r:
                fails += 1
    return fails


# ── the drives' shipped numbers, by the ASSET's field names ───────────────────
# A drive job searches in the asset's own field names, so its result can be written into the `<asset>_<Mode>` variant
# the learners already use (AIBoostPolicySO.ForMode) without a rename. Each name maps to the sim's attribute.

DRIVE_FIELDS = {
    "rhino_drive": {"alignDegrees": "align", "releaseAlignDegrees": "ralign", "stickBand": "stick",
                    "releaseStickBand": "rstick", "engageRunwaySeconds": "erun", "releaseRunwaySeconds": "rrun",
                    "steeringShareOfPlateau": "share", "minHoldSeconds": "hold", "minRestSeconds": "rest",
                    "minRearmSeconds": "rearm", "minSpeedGain": "gain", "cornerSpeed": "corner",
                    "minCornerSpeed": "mincorner", "brakeMarginUnits": "margin", "brakeLeadSeconds": "lead",
                    "graceShare": "gshare", "brakeSteerFloor": "steerfloor", "throttleSlewPerSecond": "slew",
                    "cornerReleaseDegrees": "crel"},
    "manta_drive": {"BoostStickBand": "boost_band", "YastriStickBand": "yastri_band"},
    "dolphin_drive": {"alignDegrees": "align", "engageRunwaySeconds": "runway", "spendAtCharge01": "spend",
                      "maxHoldOffSeconds": "max_hold", "peakMarginUnits": "peak_margin",
                      "minDischargeSeconds": "min_dis", "minPulseGainUnits": "min_gain"},
}


def read_asset_fields(root, rel):
    """Scalar `name: number` fields of a Unity YAML asset (an AI drive variant); {} when it does not exist."""
    import re
    path = os.path.join(root, rel)
    if not os.path.isfile(path):
        return {}
    out = {}
    for line in open(path, encoding="utf-8"):
        m = re.match(r"^\s{2}([A-Za-z_]\w*):\s*(-?[0-9.]+(?:[eE][-+]?\d+)?)\s*$", line)
        if m:
            out[m.group(1)] = float(m.group(2))
    return out


def to_sim(model, fields):
    """Asset field names -> the sim's attribute names (unknown names are dropped)."""
    table = DRIVE_FIELDS.get(model, {})
    return {table[k]: v for k, v in fields.items() if k in table}
