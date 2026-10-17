#!/usr/bin/env python3
"""learn_drives_from_flights.py - teach the Regatta AIs of the OTHER racing hulls from people's editor flights.

Yash, 2026-10-17: "move forward with doing the same for the other 5 AIs that are allocated for Regatta. I will play those
as well and record data of their flight styles." The Squirrel learns its skim line with learn_from_flights.py; the other
Regatta six (REGATTA_AI_ROADMAP.md: Stoat, Manta, Rhino, Urchin, Dolphin) fly the shared gate steering plus ONE drive per
hull that decides when to spend its speed verb. This script learns each drive's numbers from how a person spends the same
verb, measured exactly as the AI measures it (AIBoostContext):

    angle   = the angle between the direction of travel (course) and the target ring          (AngleToTarget)
    stick   = the larger of the yaw and pitch sticks, max(|xSum|, |ySum|)                      (Stick)
    runway  = distance to the ring / speed                                                     (RunwaySeconds)

    Rhino   ramp     (state bit "boosting")     -> RhinoAIBoostPolicy_Regatta: when to start and stop the ramp, how much
                                                   steering while ramping, the shortest ramp and rest
    Dolphin discharge (state bit "discharging") -> DolphinAIBoostPolicy_Regatta: when to discharge, at what charge
    Manta   Soar     (both triggers)            -> MantaSoarAITuning_Regatta: the stick band it Soars flat inside, the
                                                   yaw band past which it splits the triggers (fitted to the executor's
                                                   own formula, MantaAnalogTurnBoostExecutor.AutopilotTriggers)
    Urchin  rail ride (state bit "attached")    -> report only: its Regatta riding numbers live in the scene
    Stoat                                       -> not on the Regatta card yet: nothing to fly

Everything is written as a REGATTA-ONLY variant under Assets/Resources/AIBoostPolicies/ (AIBoostPolicySO.ForMode,
MantaSoarAITuningSO.LoadForMode): the hull's own assets, and every other mode, are untouched. A variant copies the hull's
asset and overrides only the fields that asset has. Nothing is written for a hull below MIN_FINISHED finished flights;
it learns from its fastest MIN_FINISHED finished races, like the Squirrel. Seconds the AI held the hull (held = 1) are
not the person's: they are measured separately as "the AI today" for comparison.

    python3 Tools/Build/learn_drives_from_flights.py                 # every Regatta flight on every origin branch
    python3 Tools/Build/learn_drives_from_flights.py --dir <folder>  # ...or local folders (repeatable)
    python3 Tools/Build/learn_drives_from_flights.py --write         # + the Regatta variants and LEARNED_DRIVES.md
    python3 Tools/Build/learn_drives_from_flights.py --check         # the written variants are well formed
    python3 Tools/Build/learn_drives_from_flights.py --self-test     # synthetic flights with known answers
"""
import argparse
import datetime
import hashlib
import json
import math
import os
import re
import statistics
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

POLICY_DIR = os.path.join(REPO, "Assets", "_SO_Assets", "AI Boost Policies")
OUT_DIR = os.path.join(REPO, "Assets", "Resources", "AIBoostPolicies")   # AIBoostPolicySO.ModeVariantFolder
MANTA_SCRIPT = os.path.join(REPO, "Assets", "_Scripts", "Controller", "AI", "Boost", "MantaSoarAITuningSO.cs")
REPORT_DOC = os.path.join(REPO, "Docs", "AI_SYSTEM", "LEARNED_DRIVES.md")
MODE = "Regatta"
MIN_FINISHED = 3
MIN_EPISODES = 6
STATE_BOOSTING, STATE_DISCHARGING, STATE_ATTACHED = 1, 2, 16
PLATEAU = 0.3   # StraightLineGesture.EngageThreshold: the Rhino ramp's full-power steering plateau

HULLS = {
    "Rhino": {"verb": "ramp", "bit": STATE_BOOSTING, "policy": "RhinoAIBoostPolicy"},
    "Dolphin": {"verb": "discharge", "bit": STATE_DISCHARGING, "policy": "DolphinAIBoostPolicy"},
    "Manta": {"verb": "Soar", "bit": STATE_BOOSTING, "tuning": "MantaSoarAITuning"},
    "Urchin": {"verb": "rail ride", "bit": STATE_ATTACHED},
    "Stoat": {"verb": "dipole sling", "bit": STATE_BOOSTING},
}

# The C# ranges ([Range] / [Min]) every written value is held inside.
RANGES = {
    "alignDegrees": (0.0, 90.0), "releaseAlignDegrees": (0.0, 120.0), "stickBand": (0.0, 1.0),
    "releaseStickBand": (0.0, 1.0), "engageRunwaySeconds": (0.0, 6.0), "releaseRunwaySeconds": (0.0, 6.0),
    "steeringShareOfPlateau": (0.1, 1.0), "minHoldSeconds": (0.0, 5.0), "minRestSeconds": (0.0, 5.0),
    "minRearmSeconds": (0.0, 5.0), "spendAtCharge01": (0.05, 1.0),
    "BoostStickBand": (0.0, 1.0), "YastriStickBand": (0.0, 1.0),
}


def clamp(key, v):
    lo, hi = RANGES[key]
    return round(min(max(v, lo), hi), 3)


def pct(vals, q):
    vals = sorted(v for v in vals if v is not None)
    if not vals:
        return None
    k = (len(vals) - 1) * q
    lo, hi = math.floor(k), math.ceil(k)
    return vals[lo] + (vals[hi] - vals[lo]) * (k - lo)


def angle(a, b):
    la, lb = math.sqrt(sum(x * x for x in a)), math.sqrt(sum(x * x for x in b))
    if la < 1e-9 or lb < 1e-9:
        return 0.0
    return math.degrees(math.acos(max(-1.0, min(1.0, sum(x * y for x, y in zip(a, b)) / (la * lb)))))


def guid(key):
    return hashlib.md5(("cosmic-shore/" + key).encode()).hexdigest()


# ---------------------------------------------------------------- one flight -> samples the AI's way

def rings_of(f):
    """{ring index: (position, radius)} from the flight's own ringList, else the course generator's rings."""
    rl = f.get("ringList") or []
    if rl:
        return {int(r[0]): ((r[1], r[2], r[3]), r[7]) for r in rl}
    import situation_features as sf
    C = sf.course_from(sf.load_course_js(), int(f.get("intensity") or 1))
    return {i: (tuple(g["p"]), g["r"]) for i, g in enumerate(C["gates"])} if C else {}


def samples_of(f):
    """Each sample with the AI's own measures. None when the flight cannot be read that way."""
    cols = f.get("columns") or []
    ix = {c: i for i, c in enumerate(cols)}
    if not all(k in ix for k in ("t", "x", "y", "z", "speed", "xSum", "ySum", "rings")):
        return None
    rings = rings_of(f)
    if not rings:
        return None
    n = len(rings)
    has_course = all(k in ix for k in ("cx", "cy", "cz"))
    boost_meter = f.get("boostMeter", -1)
    meter = f"m{boost_meter}" if isinstance(boost_meter, int) and 0 <= boost_meter <= 3 else None
    out = []
    for r in f.get("samples") or []:
        g = lambda k, d=0.0: float(r[ix[k]]) if k in ix and ix[k] < len(r) and r[ix[k]] is not None else d
        p = (g("x"), g("y"), g("z"))
        ring = rings.get(int(g("rings")) % n)
        if ring is None:
            continue
        to = tuple(a - b for a, b in zip(ring[0], p))
        course = (g("cx"), g("cy"), g("cz")) if has_course else (g("fx"), g("fy"), g("fz"))
        dist = math.sqrt(sum(x * x for x in to))
        speed = g("speed")
        out.append({
            "t": g("t"), "angle": angle(course, to), "stick": max(abs(g("xSum")), abs(g("ySum"))),
            "steer": abs(g("xSum")) + abs(g("ySum")) + abs(g("yDiff")), "yaw": g("xSum"),
            "runway": dist / max(speed, 1.0), "speed": speed, "state": int(g("state")), "held": g("held") > 0.5,
            "lt": g("lt"), "rt": g("rt"), "meter": (min(1.0, max(0.0, g(meter, -1.0))) if meter and g(meter, -1.0) >= 0 else None),
        })
    return out


def episodes(S, bit, held):
    """Runs of samples with the verb's state bit on: (engage sample, release sample, seconds), plus the rests between."""
    eps, rests, start, last_end = [], [], None, None
    for i, s in enumerate(S):
        on = bool(s["state"] & bit) and s["held"] == held
        if on and start is None:
            start = i
            if last_end is not None:
                rests.append(S[i]["t"] - S[last_end]["t"])
        elif not on and start is not None:
            eps.append((S[start], S[i - 1], S[i - 1]["t"] - S[start]["t"], start, i - 1))
            last_end, start = i - 1, None
    return eps, rests


# ---------------------------------------------------------------- what each hull's numbers are, measured

def measure_policy(S, bit, held=False):
    """The AIBoostPolicySO numbers from one set of samples: when the verb starts and stops."""
    eps, rests = episodes(S, bit, held)
    m = {"episodes": len(eps), "seconds": round(sum(e[2] for e in eps), 1)}
    if len(eps) < MIN_EPISODES:
        return m
    eng = [e[0] for e in eps]
    rel = [e[1] for e in eps]
    m["alignDegrees"] = pct([s["angle"] for s in eng], 0.8)        # it starts once lined up this well (80% of starts)
    m["stickBand"] = pct([s["stick"] for s in eng], 0.8)
    m["engageRunwaySeconds"] = pct([s["runway"] for s in eng], 0.2)  # ...with at least this much straight left
    m["releaseAlignDegrees"] = pct([s["angle"] for s in rel], 0.5)  # it lets go here
    m["releaseStickBand"] = pct([s["stick"] for s in rel], 0.5)
    m["releaseRunwaySeconds"] = pct([s["runway"] for s in rel], 0.5)
    within = [s["steer"] for e in eps for s in S[e[3]:e[4] + 1]]
    m["steeringShareOfPlateau"] = (pct(within, 0.9) or 0) / PLATEAU
    m["minHoldSeconds"] = pct([e[2] for e in eps], 0.1)
    m["minRestSeconds"] = m["minRearmSeconds"] = pct(rests, 0.1) if rests else None
    meters = [s["meter"] for s in eng if s["meter"] is not None]
    m["spendAtCharge01"] = statistics.median(meters) if len(meters) >= MIN_EPISODES // 2 else None
    return m


def autopilot_triggers(yaw, stick, boost_band, yastri_band):
    """MantaAnalogTurnBoostExecutor.AutopilotTriggers, line for line."""
    if boost_band <= 0:
        hold = 0.0
    elif abs(stick) <= boost_band:
        hold = 1.0
    else:
        hold = min(1.0, max(0.0, 1 - (abs(stick) - boost_band) / max(1e-4, 1 - boost_band)))
    lt = rt = hold
    a = abs(yaw)
    if a > yastri_band:
        excess = min(1.0, max(0.0, (a - yastri_band) / max(1e-4, 1 - yastri_band)))
        outer = min(1.0, hold + excess)
        if yaw > 0:
            rt = outer
        else:
            lt = outer
    return lt, rt


def measure_manta(S, held=False):
    """The two Soar bands that make the executor's own trigger formula match the person's triggers best."""
    pts = [s for s in S if s["held"] == held and s["speed"] > 5]
    m = {"samples": len(pts)}
    if len(pts) < 50:
        return m
    grid = [i / 40 for i in range(1, 40)]
    best_b = min(grid, key=lambda b: sum((min(s["lt"], s["rt"]) - autopilot_triggers(0, s["stick"], b, 1.0)[0]) ** 2 for s in pts))
    best_y = min([y for y in grid if y >= best_b] or [1.0],
                 key=lambda y: sum(((s["rt"] - s["lt"]) - (lambda t: t[1] - t[0])(autopilot_triggers(s["yaw"], s["stick"], best_b, y))) ** 2 for s in pts))
    m["BoostStickBand"], m["YastriStickBand"] = best_b, best_y
    m["meanSoar"] = statistics.mean(min(s["lt"], s["rt"]) for s in pts)
    return m


def measure_urchin(f, S, held=False):
    """How a person rides: share of the race on a rail, and on their OWN rail."""
    own = f.get("ownRail", -1)
    rails = f.get("rails") or []
    cols = f.get("columns") or []
    ix = {c: i for i, c in enumerate(cols)}
    on, own_n, n = 0, 0, 0
    for s, r in zip(S, f.get("samples") or []):
        if s["held"] != held:
            continue
        n += 1
        if s["state"] & STATE_ATTACHED:
            on += 1
            if rails and own >= 0:
                p = (r[ix["x"]], r[ix["y"]], r[ix["z"]])
                near = min(range(len(rails)), key=lambda k: min(sum((a - b) ** 2 for a, b in zip(p, q[:3])) for q in rails[k]["prisms"][::4]))
                own_n += near == own
    return {"samples": n, "rideShare": on / n if n else None, "ownRailShare": own_n / on if on else None}


# ---------------------------------------------------------------- learning per hull

def learn_hull(hull, flights):
    """flights: [(flight, samples)]. The person's numbers from the fastest MIN_FINISHED finished races, the AI's
    (held) from all of them, and what would be written."""
    from learn_from_flights import finished_of
    spec = HULLS[hull]
    done = sorted([fs for fs in flights if finished_of(fs[0])], key=lambda fs: fs[0].get("raceSeconds") or 1e9)
    used = done[:MIN_FINISHED]
    every = [s for _, S in flights for s in S]
    learned, person, ai, why = {}, {}, {}, None
    if hull == "Manta":
        person = measure_manta([s for _, S in used for s in S])
        ai = measure_manta(every, held=True)
        for k in ("BoostStickBand", "YastriStickBand"):
            if k in person:
                learned[k] = clamp(k, person[k])
    elif hull == "Urchin":
        person = {"flights": len(flights)}
        rides = [measure_urchin(f, S) for f, S in flights]
        person["rideShare"] = pct([r["rideShare"] for r in rides], 0.5)
        person["ownRailShare"] = pct([r["ownRailShare"] for r in rides], 0.5)
        why = "report only: the Urchin's Regatta riding is RegattaController's (its scene fields), not a drive asset"
    elif hull == "Stoat":
        why = "the Stoat is not on the Regatta card yet"
    else:
        person = measure_policy([s for _, S in used for s in S], spec["bit"])
        ai = measure_policy(every, spec["bit"], held=True)
        keys = ["alignDegrees", "stickBand", "engageRunwaySeconds"]
        if hull == "Rhino":
            keys += ["releaseAlignDegrees", "releaseStickBand", "releaseRunwaySeconds", "steeringShareOfPlateau",
                     "minHoldSeconds", "minRestSeconds", "minRearmSeconds"]
        if hull == "Dolphin":
            keys += ["spendAtCharge01"]   # a discharge ends itself when the bank is spent: its release is not a choice
        for k in keys:
            if person.get(k) is not None:
                learned[k] = clamp(k, person[k])
        if "releaseAlignDegrees" in learned:   # the release band is the WIDER one (hysteresis)
            learned["releaseAlignDegrees"] = max(learned["releaseAlignDegrees"], learned.get("alignDegrees", 0))
            learned["releaseStickBand"] = max(learned.get("releaseStickBand", 0), learned.get("stickBand", 0))
            learned["releaseRunwaySeconds"] = min(learned["releaseRunwaySeconds"], learned.get("engageRunwaySeconds", 6))
    if why is None and len(done) < MIN_FINISHED:
        why = f"{len(done)} finished race(s); nothing is written below {MIN_FINISHED}"
    elif why is None and not learned:
        why = f"too few {spec['verb']} moments in the fastest {MIN_FINISHED} races (need {MIN_EPISODES})"
    return {"hull": hull, "flights": len(flights), "finished": len(done), "used": [f.get("name", "") for f, _ in used],
            "person": person, "ai": ai, "learned": learned if why is None else {}, "proposed": learned, "why": why}


# ---------------------------------------------------------------- writing the Regatta variants

def asset_header(script_guid, name):
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
            "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n  m_Name: {name}\n  m_EditorClassIdentifier: \n")


def native_meta(g):
    return f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def ensure_folder():
    os.makedirs(OUT_DIR, exist_ok=True)
    meta = OUT_DIR + ".meta"
    if not os.path.exists(meta):
        open(meta, "w").write(f"fileFormatVersion: 2\nguid: {guid('folder/Resources/AIBoostPolicies')}\nfolderAsset: yes\n"
                              "DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def variant_text(hull, learned):
    """(path, text) of the hull's Regatta variant: its own asset with the learned fields, or the Manta's tuning."""
    spec = HULLS[hull]
    if "policy" in spec:
        base = open(os.path.join(POLICY_DIR, spec["policy"] + ".asset"), encoding="utf-8").read()
        name = f"{spec['policy']}_{MODE}"
        text = re.sub(r"(?m)^  m_Name: .*$", f"  m_Name: {name}", base, count=1)
        for k, v in learned.items():
            text = re.sub(rf"(?m)^  {re.escape(k)}: [^\n]*$", f"  {k}: {v}", text, count=1)
        return os.path.join(OUT_DIR, name + ".asset"), text
    name = f"{spec['tuning']}_{MODE}"
    script_guid = re.search(r"guid: ([0-9a-f]{32})", open(MANTA_SCRIPT + ".meta").read()).group(1)
    body = "".join(f"  {k}: {v}\n" for k, v in learned.items())
    return os.path.join(OUT_DIR, name + ".asset"), asset_header(script_guid, name) + body


def write_variant(hull, learned):
    ensure_folder()
    path, text = variant_text(hull, learned)
    open(path, "w", encoding="utf-8").write(text)
    meta = path + ".meta"
    if not os.path.exists(meta):
        open(meta, "w").write(native_meta(guid("asset/AIBoostPolicies/" + os.path.basename(path))))
    return path


def check():
    """The written variants: each policy variant has its base's script and fields, every value inside its range."""
    problems = []
    if not os.path.isdir(OUT_DIR):
        print("check: no Regatta variants written yet (OK)")
        return 0
    for fn in sorted(os.listdir(OUT_DIR)):
        if not fn.endswith(".asset"):
            continue
        text = open(os.path.join(OUT_DIR, fn), encoding="utf-8").read()
        if not os.path.exists(os.path.join(OUT_DIR, fn + ".meta")):
            problems.append(f"{fn}: no .meta")
        base = fn[:-len(f"_{MODE}.asset")] + ".asset"
        if os.path.exists(os.path.join(POLICY_DIR, base)):
            bt = open(os.path.join(POLICY_DIR, base), encoding="utf-8").read()
            if re.search(r"m_Script: [^\n]*", bt).group(0) != re.search(r"m_Script: [^\n]*", text).group(0):
                problems.append(f"{fn}: a different script from {base} (AIBoostPolicySO.ForMode would ignore it)")
            extra = set(re.findall(r"(?m)^  (\w+):", text)) - set(re.findall(r"(?m)^  (\w+):", bt))
            if extra:
                problems.append(f"{fn}: fields its base does not have: {sorted(extra)}")
        for k, v in re.findall(r"(?m)^  (\w+): ([-0-9.eE]+)$", text):
            if k in RANGES and not RANGES[k][0] <= float(v) <= RANGES[k][1]:
                problems.append(f"{fn}: {k} {v} outside {RANGES[k]}")
    for p in problems:
        print("check:", p)
    print(f"check: {'OK' if not problems else 'FAILED'}")
    return 1 if problems else 0


# ---------------------------------------------------------------- report

def fmt(v):
    return "-" if v is None else (f"{v:.2f}" if isinstance(v, float) else str(v))


def report(results, wrote):
    lines = []
    for R in results:
        lines.append(f"### {R['hull']} ({HULLS[R['hull']]['verb']})\n")
        lines.append(f"{R['flights']} editor flight(s), {R['finished']} finished; learned from: "
                     + (", ".join(os.path.basename(u) for u in R["used"]) or "-") + "\n")
        if R["why"]:
            lines.append(f"Nothing written: {R['why']}.\n")
        keys = sorted(set(R["person"]) | set(R["ai"]))
        if keys:
            lines += ["| Measure | You (fastest finished races) | The AI today (seconds it held your hull) | Written |", "|---|---:|---:|---:|"]
            for k in keys:
                lines.append(f"| `{k}` | {fmt(R['person'].get(k))} | {fmt(R['ai'].get(k))} | {fmt(R['learned'].get(k))} |")
            lines.append("")
    lines.append("Written: " + (", ".join(os.path.relpath(p, REPO) for p in wrote) if wrote else "nothing") + ".")
    return "\n".join(lines)


def run(sources, write):
    from learn_from_flights import parse
    by_hull = {h: [] for h in HULLS}
    for name, text in sources:
        f, err = parse(name, text)
        if err or f.get("mode") != MODE or f.get("hull") not in HULLS:
            continue
        S = samples_of(f)
        if S:
            by_hull[f["hull"]].append((f, S))
    results = [learn_hull(h, fl) for h, fl in by_hull.items()]
    wrote = [write_variant(R["hull"], R["learned"]) for R in results if write and R["learned"]]
    text = report(results, wrote)
    print(text)
    if write:
        open(REPORT_DOC, "w", encoding="utf-8").write(
            "# What the other Regatta AIs learned from editor flights\n\n"
            f"Generated by `python3 Tools/Build/learn_drives_from_flights.py --write` ({datetime.date.today().isoformat()}). "
            "Each hull's numbers are written as a Regatta-only variant in `Assets/Resources/AIBoostPolicies/` "
            "(`AIBoostPolicySO.ForMode`, `MantaSoarAITuningSO.LoadForMode`); the hull's own assets and every other mode are "
            "untouched. How it works: the script's header; the plan: `SITUATION_RECORDING_PLAN.md` §9.\n\n" + text + "\n")
        print(f"wrote {os.path.relpath(REPORT_DOC, REPO)}")
    return 0


# ---------------------------------------------------------------- self-test

def self_test():
    """Synthetic Regatta flights whose pilots follow KNOWN rules: the learner must recover them, and a pilot that
    ignores the rule must not produce the same numbers (negative control)."""
    import random
    rng = random.Random(7)
    ring_list = [[i, 600.0 * (i + 1), 0.0, 0.0, 1, 0, 0, 60.0] for i in range(40)]   # ring k is the one ahead after k threaded

    def flight(hull, rule, seconds=100.0, finished=True, held=False):
        rows, x, ring, on, since = [], 0.0, 1, False, 0.0
        for k in range(int(seconds * 10)):
            t = k / 10
            speed = 200.0
            x += speed / 10
            if x >= 600.0 * ring:
                ring += 1
            dist = 600.0 * ring - x
            ang = abs(30 * math.sin(t * 0.7)) + rng.random() * 2         # the course swings off the ring and back
            yaw = 0.8 * math.sin(t * 0.9)
            stick = abs(yaw)
            runway = dist / speed
            lt = rt = 0.0
            state = 0
            if rule == "policy":   # engage when lined up within 10 deg, stick < 0.3, runway > 1.2 s; release at 20 / 0.5 s
                if on and (ang > 20 or runway < 0.5):
                    on, since = False, t
                elif not on and ang <= 10 and stick <= 0.3 and runway >= 1.2 and t - since > 0.3:
                    on = True
                state = (STATE_BOOSTING if hull == "Rhino" else STATE_DISCHARGING) if on else 0
            elif rule == "random":
                on = rng.random() < 0.3
                state = (STATE_BOOSTING if hull == "Rhino" else STATE_DISCHARGING) if on else 0
            elif rule == "manta":
                lt, rt = autopilot_triggers(yaw, stick, 0.5, 0.8)
            meter = 0.8 + 0.01 * rng.random()
            # The course swings ang degrees off the ring (which lies straight down +x): the learner measures that angle.
            cx, cy = math.cos(math.radians(ang)), math.sin(math.radians(ang))
            rows.append([t, x, 0, 0, 1, 0, 0, 0, 1, 0, speed, 1, yaw, 0, 0, 0, 1, ring - 1, lt, rt, cx, cy, 0, state, 1 if held else 0, meter])
        return {"schema": "cosmic-shore.flight/3", "mode": MODE, "hull": hull, "ended": "race over", "finished": finished,
                "raceSeconds": seconds, "raceTarget": 24, "rings": 24 if finished else 3, "ringList": ring_list, "boostMeter": 0,
                "columns": ["t", "x", "y", "z", "fx", "fy", "fz", "ux", "uy", "uz", "speed", "boost", "xSum", "ySum", "xDiff", "yDiff",
                            "throttle", "rings", "lt", "rt", "cx", "cy", "cz", "state", "held", "m0"], "samples": rows}

    def learned(hull, rule, n=3, finished=True):
        fl = [flight(hull, rule, 100 + i, finished) for i in range(n)]
        return learn_hull(hull, [(f, samples_of(f)) for f in fl])

    rh = learned("Rhino", "policy")["learned"]
    assert rh and rh["alignDegrees"] <= 10.5 and rh["stickBand"] <= 0.31 and rh["engageRunwaySeconds"] >= 1.15, rh
    assert rh["releaseAlignDegrees"] >= rh["alignDegrees"] and rh["releaseRunwaySeconds"] <= rh["engageRunwaySeconds"], rh
    ctrl = learned("Rhino", "random")["learned"]
    assert ctrl["alignDegrees"] > rh["alignDegrees"] + 5, ("negative control: a pilot ignoring the rule must not look lined up", ctrl)
    do = learned("Dolphin", "policy")["learned"]
    assert do and abs(do["spendAtCharge01"] - 0.805) < 0.02 and "releaseAlignDegrees" not in do, do
    mt = learned("Manta", "manta")["learned"]
    assert abs(mt["BoostStickBand"] - 0.5) < 0.03 and abs(mt["YastriStickBand"] - 0.8) < 0.03, mt
    assert not learned("Rhino", "policy", n=2)["learned"], "fewer than 3 finished races writes nothing"
    assert not learned("Rhino", "policy", finished=False)["learned"], "unfinished races write nothing"
    held = flight("Rhino", "policy", held=True)
    assert not learn_hull("Rhino", [(held, samples_of(held))] * 3)["person"].get("alignDegrees"), "AI-held seconds are not the person's"
    # A variant overrides only the base's own fields and keeps its script.
    path, text = variant_text("Rhino", {"alignDegrees": 9.5, "notAField": 1})
    assert "alignDegrees: 9.5" in text and "notAField" not in text and "m_Name: RhinoAIBoostPolicy_Regatta" in text, text
    path, text = variant_text("Manta", {"BoostStickBand": 0.5, "YastriStickBand": 0.8})
    assert "MantaSoarAITuning_Regatta" in text and "BoostStickBand: 0.5" in text
    print("learn_drives_from_flights self-test: OK (Rhino and Dolphin starts / stops recovered, a random pilot rejected; "
          "Dolphin charge 0.80; Manta bands 0.50 / 0.80 recovered; under 3 finished races, unfinished races and AI-held "
          "seconds write nothing; variants keep only their base's fields)")
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dir", action="append")
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        return self_test()
    if a.check:
        return check()
    from learn_from_flights import flights_in_dir, flights_on_branches
    sources = [s for d in a.dir for s in flights_in_dir(d)] if a.dir else flights_on_branches()
    return run(sources, a.write)


if __name__ == "__main__":
    sys.exit(main())
