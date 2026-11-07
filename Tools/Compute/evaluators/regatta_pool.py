"""The Regatta POOL search: one course, scored as a candidate for the shuffled pool (Plan step: time-arena-course).

Regatta becomes ONE level that shuffles between several balanced courses (Garrett, 2026-11-06). A course may sit near
1.25x on its own only if the spreads CANCEL across the pool: each hull's mean time over the pool within about 1.10x of
the others, and no hull fastest on every course. This evaluator scores ONE course for that pool; the job runs it as a
MAP-Elites archive (`Docs/Compute/jobs/regatta-pool.json`) so the archive holds many DIFFERENT good courses, and
`Tools/Build/regatta_pool_pick.py` picks the pool from it.

    loss = ln(slowest / fastest)                                  the SPREAD on this course (scored hulls only)
         + CHAOS_WEIGHT x chaos                                   a coin-flip course is not a balanced one
         + 0.5 per (gate, lane) whose rail misses the mouth       RegattaCourseTests' coverage rule
         + 2 x how far the mean race left [0.8, 1.25] x today's   a race stays a race (in ln)
         + 0.5 per hull that does not finish

CHAOS: the same settings laid by the next `chaos_seeds` seeds. Each hull's standing (its ln time minus the field's mean
ln time) is taken on every seed; chaos is the mean over hulls of that standing's standard deviation. A course whose
balance comes from one lucky seed, not from its settings, scores high, and so does one whose order flips from seed to
seed. It costs `chaos_seeds` more races of the roster, so it is measured only on courses whose spread is already under
`chaos_gate`; the rest pay CHAOS_UNMEASURED instead (more than any measured course near the pass line).

A course PASSES (metrics.passes) when: spread <= `pass_spread` (1.25), chaos <= `pass_chaos`, every rail through every
mouth, the race 0.8-1.25x today's, every hull finishes. The picker takes only passing courses.

DESCRIPTORS (quality-diversity axes, metrics.descriptors; the job bins some of them):
    sharpness     the hardest corner, degrees                       the course's character
    mean_turn     the mean corner, degrees
    ridable       the share of every leg's rail in a team's colour  1 - NeutralShare (brief 10's sections refine it)
    race_length   the field's mean race / today's
    mouth         the tightest ring radius, u
    leader        which scored hull is fastest (its index in `scored`), so the archive keeps courses won by DIFFERENT
                  hulls: the raw material a cancelling pool is made of
    trailer       which scored hull is slowest
    gaps          rail gaps (0 until brief 10's course features land)
    crystals      crystals on the racing line (0 until brief 10)

Everything here is the MODEL (regatta_measured_roster.py: each hull anchored at its measured editor race on the shipped
I1 course and moved by the line and lap models; the Urchin on its rail model). No number from it is a real-game race.

The course: the shipped generator (course_port.py, gated against the C# harness) with RegattaCourse.ForIntensity(1)'s
row as it shipped until 2026-11-06 (regatta_measured_roster.MEASURED_ON, the course the roster was measured on), each
setting the job names moved, laid by generator seed MEASURED_SEED + 104729 x `seed`. A pool entry is that settings row,
that generator seed and the neutral share (`regatta_pool_pick.py` writes them out for RegattaCoursePool, brief 13).

args:
    roster        {hull: {"model": race_models model, "drive": {...}}}  (regatta_measured_roster.roster(), pinned)
    report_only   hulls raced and reported but not scored (the Sparrow until brief 4 fixes its rail rams)
    limit         seconds before a race counts as not finished (600)
    chaos_seeds   extra seeds the chaos is measured on (2)
    chaos_gate    spread under which chaos is measured (1.6)
    pass_spread   a passing course's spread (1.25)
    pass_chaos    a passing course's chaos (0.06)
    ref_neutral   the neutral share of TODAY's course, for the race-length guard (0: bleeding-edge lays none)
    features      true to search brief 10's course features too (regatta_features.py, draft PR #1787; this module only
                  imports it when asked), in a COMPACT form that a course keeps when it is laid by another seed:
                    NeutralShare   the middle share of every leg in no team's colour
                    PlainShare     a stretch at the start of every leg in plain (cuttable) rail
                    GapLength, GapLegs   a gap of that many u in the middle of that many legs, spread round the lap
                    AltCount, AltOffset, AltBearing, AltScale   an alternate ring at each of the AltCount sharpest corners
                    Crystals, CrystalPhase   crystals on the line, at most 4 a lap
                  The hulls are priced by regatta_features.race (race_models.race on an empty set), the Urchin's free
                  flight at its roster free_speed; the rail guard becomes regatta_features.guard_fails.
    anchor_all    a COUNTERFACTUAL: every "measured" hull anchored at these seconds on today's course instead of its
                  measured race (what pool the course alone would find if every AI raced today's course alike, e.g.
                  once time-arcade has trained them); never the shipped roster
"""
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(_HERE)))
for _p in (_HERE, os.path.join(_ROOT, "Tools", "Build")):
    if _p not in sys.path:
        sys.path.insert(0, _p)

import course_port as cp          # noqa: E402
import race_models as rm          # noqa: E402

VERSION = "2"   # 2 (2026-11-07): `features` (brief 10's course features, in a compact form); off, it is VERSION 1
SEED_STRIDE = 104729
CHAOS_WEIGHT = 2.0
CHAOS_UNMEASURED = 0.3
GEOMETRY = ("RingRadius", "CornerFloorRadius", "AngularSpread", "LateralPerturbation", "AxisJitterDegrees",
            "MaxPresentDegrees", "RadialSwing")
_CTX = {}


def _measured():
    import regatta_measured_roster as mr
    return mr


def inputs(args):
    return ["Tools/Build/regatta_line_sim.py", "Tools/Build/regatta_balance.py", "Tools/Build/situation_features.py",
            "Tools/Build/regatta_measured_roster.py", "Tools/Compute/evaluators/race_models.py",
            "Tools/Compute/evaluators/course_port.py", "Assets/Resources/RaceLines/"]


def scored_hulls(args):
    skip = set(args.get("report_only") or ())
    return [h for h in sorted(args["roster"]) if h not in skip]


def wrap32(v):
    v &= 0xFFFFFFFF
    return v - (1 << 32) if v >= (1 << 31) else v


def generator_seed(step):
    return wrap32(_measured().MEASURED_SEED + SEED_STRIDE * int(step))


def settings_from(params):
    """The job's parameters -> a RegattaCourse settings dict (MEASURED_ON with the named settings moved)."""
    s = dict(_measured().MEASURED_ON)
    prof = list(s["CornerProfile"])
    angles = [max(0.0, float(params.get(f"corner{k}", prof[k]))) for k in range(len(prof))]
    total = sum(angles) or 1.0
    s["CornerProfile"] = [a * 360.0 / total for a in angles]
    for key in GEOMETRY:
        if key in params:
            s[key] = float(params[key])
    return s


FEATURE_KEYS = ("NeutralShare", "PlainShare", "GapLength", "GapLegs", "AltCount", "AltOffset", "AltBearing", "AltScale",
                "Crystals", "CrystalPhase")


def compact_features(params, plain):
    """The compact feature params -> a RegattaCourseFeatures dict (regatta_features' schema) for the circuit `plain`
    (its legArc and turnDegrees). Every param at 0 is the empty set (today's course)."""
    g = lambda k, d=0.0: float(params.get(k, d))
    feats = {"Sections": [], "AlternateGates": [], "Crystals": {"PerLap": 0, "Phase": 0.0}}
    plain_share = min(max(g("PlainShare"), 0.0), 0.9)
    if plain_share > 0.01:
        feats["Sections"].append({"Leg": -1, "From": 0.02, "To": round(0.02 + plain_share, 4), "Kind": 2,
                                  "Neutral": False, "Detour": 0.0, "DetourBearing": 0.0})
    n = min(max(g("NeutralShare"), 0.0), 0.95)
    if n > 0.01:      # later sections win: where it overlaps the plain stretch, the neutral one is super-shielded
        feats["Sections"].append({"Leg": -1, "From": round(0.5 - n / 2, 4), "To": round(0.5 + n / 2, 4), "Kind": 0,
                                  "Neutral": True, "Detour": 0.0, "DetourBearing": 0.0})
    legs = len(plain["legArc"])
    gap, count = g("GapLength"), min(max(int(round(g("GapLegs"))), 0), legs)
    if gap >= 16.0 and count:
        for j in range(count):
            leg = int(j * legs / count)
            if plain["legArc"][leg] > 1.0:
                half = min(0.5 * gap / plain["legArc"][leg], 0.45)
                feats["Sections"].append({"Leg": leg, "From": round(0.5 - half, 4), "To": round(0.5 + half, 4),
                                          "Kind": 3, "Neutral": False, "Detour": 0.0, "DetourBearing": 0.0})
    alts = min(max(int(round(g("AltCount"))), 0), legs)
    if alts and g("AltOffset") >= 1.0:
        sharpest = sorted(range(len(plain["turnDegrees"])), key=lambda i: -plain["turnDegrees"][i])[:alts]
        for ring in sorted(sharpest):
            feats["AlternateGates"].append({"Ring": ring, "Offset": round(g("AltOffset"), 1),
                                            "Bearing": round(g("AltBearing") % 360.0, 1),
                                            "RadiusScale": round(min(max(g("AltScale", 1.0), 0.6), 1.4), 3)})
    feats["Crystals"] = {"PerLap": min(max(int(round(g("Crystals"))), 0), 4), "Phase": round(g("CrystalPhase") % 1.0, 4)}
    return feats


def lay(params, step_offset=0, features=False):
    s = settings_from(params)
    seed = generator_seed(int(params.get("seed", 0)) + step_offset)
    if not features:
        return s, seed, cp.course(s, seed)
    import regatta_features as rf
    plain = rf.course(settings=s, seed=seed)
    return s, seed, rf.course(settings=s, seed=seed, features=compact_features(params, plain))


def roster_of(args):
    """The job's roster, with `anchor_all` (a counterfactual) applied to every measured hull."""
    out = {}
    for hull, h in args["roster"].items():
        h = dict(h, drive=dict(h.get("drive") or {}))
        if args.get("anchor_all") and h["model"] == "measured":
            h["drive"]["anchor"] = float(args["anchor_all"])
        out[hull] = h
    return out


def race_all(course, roster, limit, neutral):
    """Each hull's race. A featured course (it carries `features`) is priced by regatta_features.race, which lays the
    neutral share itself; the rail rider then flies free at its roster free_speed."""
    times, raw = {}, {}
    featured = course.get("features") is not None
    if featured:
        import regatta_features as rf
    for hull, h in roster.items():
        d = dict(h.get("drive") or {})
        if featured:
            knobs = {"urchin_free": float(d["free_speed"])} if h["model"] == "rail" and "free_speed" in d else None
            t, r = rf.race(hull, h["model"], course, 1, drive=d, limit=limit, knobs=knobs)
        else:
            if h["model"] == "rail":
                d["neutral"] = neutral
            t, r = rm.race(hull, h["model"], course, 1, drive=d, steering="centre", limit=limit)
        times[hull], raw[hull] = t, r
    return times, raw


def standing(times, hulls):
    logs = {h: math.log(times[h]) for h in hulls}
    m = sum(logs.values()) / len(logs)
    return {h: v - m for h, v in logs.items()}


def chaos_of(standings, hulls):
    out = []
    for h in hulls:
        xs = [s[h] for s in standings]
        mu = sum(xs) / len(xs)
        out.append(math.sqrt(sum((x - mu) ** 2 for x in xs) / max(1, len(xs) - 1)))
    return sum(out) / len(out)


def setup(ctx):
    a = ctx["args"]
    _CTX.clear()
    mr = _measured()
    today = mr.measured_course()
    _CTX["roster"] = roster_of(a)
    times, _ = race_all(today, _CTX["roster"], float(a.get("limit", 600)), float(a.get("ref_neutral", 0.0)))
    hulls = scored_hulls(a)
    _CTX["ref_mean"] = sum(times[h] for h in hulls) / len(hulls)
    _CTX["ref_seconds"] = times


def baseline(args):
    """Today's course: the shipped I1 row and seed, no neutral rail (bleeding-edge lays none)."""
    s = _measured().MEASURED_ON
    p = {f"corner{k}": float(v) for k, v in enumerate(s["CornerProfile"])}
    p.update({k: float(s[k]) for k in GEOMETRY})
    p["seed"] = 0
    p["NeutralShare"] = float(args.get("ref_neutral", 0.0))
    if args.get("features"):
        p.update({k: 0.0 for k in FEATURE_KEYS if k != "NeutralShare"}, AltScale=1.0)
    return p


def evaluate(params, ctx):
    a = ctx["args"]
    if "ref_mean" not in _CTX:
        setup(ctx)
    limit = float(a.get("limit", 600))
    neutral = min(max(float(params.get("NeutralShare", 0.0)), 0.0), 0.95)
    hulls = scored_hulls(a)
    feat = bool(a.get("features"))
    try:
        s, seed, course = lay(params, features=feat)
    except Exception as e:      # the generator found no legal circuit for these settings
        return {"loss": float("inf"), "valid": False, "error": f"generator: {type(e).__name__}: {e}"}
    roster = _CTX["roster"]
    times, raw = race_all(course, roster, limit, neutral)
    sc = {h: times[h] for h in hulls}
    fastest, slowest = min(sc, key=sc.get), max(sc, key=sc.get)
    spread = sc[slowest] / sc[fastest]
    if feat:
        import regatta_features as rf
        cover = rf.guard_fails(course)
    else:
        cover = rm.rail_coverage(course)
    mean = sum(sc.values()) / len(sc)
    length = mean / _CTX["ref_mean"]
    length_pen = max(0.0, math.log(0.8) - math.log(length)) + max(0.0, math.log(length) - math.log(1.25))
    dnf = [h for h, r in raw.items() if r.get("time") is None]
    dnf_scored = [h for h in dnf if h in sc]

    chaos, seed_spreads, leaders = None, [round(spread, 3)], [fastest]
    if spread <= float(a.get("chaos_gate", 1.6)):
        stands = [standing(sc, hulls)]
        for k in range(1, int(a.get("chaos_seeds", 2)) + 1):
            try:
                _, _, c2 = lay(params, k, features=feat)
            except Exception:
                continue
            t2, _ = race_all(c2, {h: roster[h] for h in hulls}, limit, neutral)
            stands.append(standing(t2, hulls))
            seed_spreads.append(round(max(t2.values()) / min(t2.values()), 3))
            leaders.append(min(t2, key=t2.get))
        if len(stands) > 1:
            chaos = chaos_of(stands, hulls)
    loss = (math.log(spread) + CHAOS_WEIGHT * (chaos if chaos is not None else CHAOS_UNMEASURED) + 0.5 * cover
            + 2.0 * length_pen + 0.5 * len(dnf_scored))
    passes = (spread <= float(a.get("pass_spread", 1.25)) + 1e-9 and chaos is not None
              and chaos <= float(a.get("pass_chaos", 0.06)) and cover == 0 and length_pen == 0.0 and not dnf_scored)
    turns = course["turnDegrees"]
    desc = {"sharpness": round(max(turns), 2), "mean_turn": round(sum(turns) / len(turns), 2),
            "ridable": round(1.0 - neutral, 3), "race_length": round(length, 3),
            "mouth": round(min(g["r"] for g in course["gates"]), 1),
            "leader": hulls.index(fastest), "trailer": hulls.index(slowest), "gaps": 0, "crystals": 0,
            "alternates": 0, "contested": 0.0}
    if feat:
        fd = rf.descriptors(course)
        desc.update(ridable=fd["ridable_share"], gaps=fd["gap_count"], crystals=fd["crystals"],
                    alternates=fd["alternates"], contested=fd["contested_share"])
    return {"loss": loss, "valid": True, "descriptors": desc, "metrics": {
        "passes": passes, "spread_x": round(spread, 4), "chaos": None if chaos is None else round(chaos, 4),
        "fastest": fastest, "slowest": slowest, "seconds": {h: round(t, 2) for h, t in times.items()},
        "scored": hulls, "seed_spreads": seed_spreads, "seed_leaders": leaders, "rail_fails": cover,
        "race_length": round(length, 3), "dnf": dnf, "generator_seed": seed, "seed_step": int(params.get("seed", 0)),
        "neutral_share": round(neutral, 3), "settings": s, "descriptors": desc,
        **({"features": course["features"]} if feat else {}),
        "turn_degrees": [round(t, 1) for t in turns], "lane_length": round(course["laneLengths"][0], 1),
        "model": ("COUNTERFACTUAL: every measured hull anchored at %s s on today's course; " % a["anchor_all"]
                  if a.get("anchor_all") else "") + "measured roster (regatta_measured_roster.py); a MODEL, not a real-game race"}}


def self_test():
    """Today's course scores with no guard penalty and its seconds are the measured roster's; a negative control: a
    course whose race is twice as long is penalised, and a chaos made of one flipped hull is measured as chaos."""
    bad = []
    mr = _measured()
    roster = mr.roster()
    roster = {h: roster[h] for h in ("Manta", "Squirrel", "Stoat", "Urchin")}
    ctx = {"root": _ROOT, "args": {"roster": roster, "report_only": [], "chaos_seeds": 1, "chaos_gate": 99}}
    _CTX.clear()
    r = evaluate(baseline(ctx["args"]), ctx)
    m = r["metrics"]
    if m["rail_fails"] or abs(m["race_length"] - 1.0) > 1e-6 or m["chaos"] is None:
        bad.append(f"regatta_pool: today's course is penalised or unmeasured ({m['rail_fails']} rail fails, "
                   f"length {m['race_length']}, chaos {m['chaos']})")
    if abs(m["seconds"]["Manta"] - mr.MEASURED["Manta"]) > 2.0:
        bad.append(f"regatta_pool: today's Manta is {m['seconds']['Manta']} s, the roster measured {mr.MEASURED['Manta']}")
    want = math.log(m["spread_x"]) + CHAOS_WEIGHT * m["chaos"]
    if abs(r["loss"] - want) > 1e-3:
        bad.append(f"regatta_pool: today's loss {r['loss']:.4f} is not spread + chaos {want:.4f}")
    # Negative controls: a far longer race pays the length guard; a hull flipping from first to last is chaos.
    long_ctx = {"root": _ROOT, "args": dict(ctx["args"])}
    _CTX["ref_mean"] /= 2.5
    r2 = evaluate(baseline(long_ctx["args"]), long_ctx)
    if r2["loss"] < r["loss"] + 1.0:
        bad.append("regatta_pool: a race 2.5x today's is not penalised by the length guard")
    flip = chaos_of([{"a": -0.2, "b": 0.0, "c": 0.2}, {"a": 0.2, "b": 0.0, "c": -0.2}], ["a", "b", "c"])
    still = chaos_of([{"a": -0.2, "b": 0.0, "c": 0.2}, {"a": -0.2, "b": 0.0, "c": 0.2}], ["a", "b", "c"])
    if not (flip > 0.15 and still < 1e-9):
        bad.append(f"regatta_pool: chaos does not see a flip ({flip:.3f}) or sees one in a steady field ({still:.3f})")
    _CTX.clear()
    return bad


if __name__ == "__main__":
    problems = self_test()
    print("\n".join(problems) or "regatta_pool --self-test: ok")
    sys.exit(1 if problems else 0)
