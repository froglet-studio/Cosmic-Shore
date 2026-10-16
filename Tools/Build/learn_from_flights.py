#!/usr/bin/env python3
"""learn_from_flights.py - the Regatta Squirrel AI learns from how a HUMAN flies (Docs/AI_SYSTEM/AI_TRAINING_PLAN.md L3).

Yash, 2026-10-15: "I play Regatta five times with the Squirrel, then my AI reads that data and makes new paths."

Unity writes one flight_*.json per Regatta race the player flies by hand (HumanFlightRecorder, automatic: Go to the
Play Again menu, or to stopping Play), and Store AI record puts it on the player's bugledger/<user> branch under
BugLedger/shared/reports/<user>/flights/. This script reads those flights and measures, against the pilot's own rail:

  height      how high above the rail the human skims   -> SkimHeight (the line's height minus the course's skim lift)
  gap         how close to the rail's contact shell     -> TrackGuardMargin / MassGuardMargin (only ever WIDER)
  look-ahead  how far down the rail the human aims      -> LookaheadSeconds, LookaheadMin
  stick       how hard the human steers per degree      -> StickGainPerDegree
  turns       how often and how hard they turn          (reported)

and writes them as overrides on the Regatta policy (POLICIES["SkimRaceAIConfig_Regatta"] in
author_skimrace_ai_config.py, which then writes Assets/Resources/SkimRaceAIConfig_Regatta.asset). Skim Race's own
policies are never touched.

Safety: a recording can only make the AI keep FURTHER from the rails, never closer (the margins never drop below the
current policy's); every number is clamped to a sane band; the best races count (the fastest three finished ones when
there are three or more); and nothing is written without --write. The report says, per number, what it was, what the
flights say and why.

    python3 Tools/Build/learn_from_flights.py                    # report: every flight on every origin branch
    python3 Tools/Build/learn_from_flights.py --dir <folder>     # ...or a local folder of flight_*.json
    python3 Tools/Build/learn_from_flights.py --write            # write the overrides, regenerate the asset, write the report doc
    python3 Tools/Build/learn_from_flights.py --self-test
"""
import argparse
import datetime
import glob
import json
import math
import os
import re
import statistics
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
AUTHOR = os.path.join(HERE, "author_skimrace_ai_config.py")
REPORT_DOC = os.path.join(REPO, "Docs", "AI_SYSTEM", "LEARNED_FROM_FLIGHTS.md")
REPORTS_PREFIX = "BugLedger/shared/reports/"
SCHEMA = "cosmic-shore.flight/2"
SCHEMAS = ("cosmic-shore.flight/1", SCHEMA, "cosmic-shore.flight/3")   # /1 carried the rail measures; /2 is measured here; /3 adds the situation columns (SITUATION_RECORDING_PLAN.md)
SKIM_REACH = 7.5              # the Squirrel's skimmer sphere (SkimRaceAIConfigSO.PlannerSkimReach)
BEGIN = "# learn_from_flights:begin"
END = "# learn_from_flights:end"

# Clamps: a learned number outside these is a recording problem, not a pilot.
BANDS = {
    "SkimHeight": (2.0, 8.5),          # 0.5 to 7 u above Skim Race's plate shell (1.5): skims, never inside it
    "TrackGuardMargin": (0.6, 3.0),
    "MassGuardMargin": (0.6, 3.0),
    "LookaheadSeconds": (0.3, 1.5),
    "LookaheadMin": (20.0, 200.0),
    "StickGainPerDegree": (0.015, 0.2),
}


# ---------------------------------------------------------------- reading flights

def git(*args):
    r = subprocess.run(["git", "-C", REPO] + list(args), capture_output=True, text=True, encoding="utf-8", errors="replace")
    return r.stdout if r.returncode == 0 else None


def flights_on_branches():
    """Every flight file on every origin branch (deduped by blob): (name, json)."""
    out, seen = [], set()
    refs = (git("for-each-ref", "--format=%(refname:short)", "refs/remotes/origin") or "").split()
    for ref in refs:
        if ref in ("origin", "origin/HEAD"):
            continue
        listing = git("ls-tree", "-r", ref, "--", REPORTS_PREFIX) or ""
        for line in listing.splitlines():
            parts = line.split(None, 3)
            if len(parts) < 4 or "/flights/" not in parts[3] or not parts[3].endswith(".json"):
                continue
            blob, path = parts[2], parts[3]
            if blob in seen:
                continue
            seen.add(blob)
            text = git("cat-file", "-p", blob)
            if text:
                out.append((path, text))
    return out


def flights_in_dir(folder):
    out = []
    for path in sorted(glob.glob(os.path.join(folder, "flight_*.json"))):
        with open(path, encoding="utf-8") as fh:
            out.append((path, fh.read()))
    return out


def parse(name, text):
    try:
        d = json.loads(text)
    except ValueError as e:
        return None, f"{name}: not JSON ({e})"
    if d.get("schema") not in SCHEMAS:
        return None, f"{name}: schema {d.get('schema')!r}, want one of {SCHEMAS}"
    cols = d.get("columns") or []
    rows = d.get("samples") or []
    d["rows"] = [dict(zip(cols, r)) for r in rows]
    d["name"] = name
    return d, None


# ---------------------------------------------------------------- the rail

class Lane:
    """The pilot's own rail as a closed polyline with up axes, measured by arc length."""

    def __init__(self, lane):
        self.p = [(r[0], r[1], r[2]) for r in lane]
        self.u = [(r[3], r[4], r[5]) for r in lane]
        n = len(self.p)
        self.cum = [0.0]
        for i in range(n):
            self.cum.append(self.cum[-1] + dist(self.p[i], self.p[(i + 1) % n]))
        self.length = max(self.cum[-1], 1e-3)

    def project(self, pos, hint):
        """Closest point on the rail to pos: (arc length s, point, up, segment). A window round hint, else everything."""
        n = len(self.p)
        rng = range(n) if hint is None else (k % n for k in range(hint - 40, hint + 41))
        best = None
        for i in rng:
            a, b = self.p[i], self.p[(i + 1) % n]
            ab = tuple(y - x for x, y in zip(a, b))
            l2 = sum(x * x for x in ab) or 1e-9
            t = max(0.0, min(1.0, sum((q - x) * y for q, x, y in zip(pos, a, ab)) / l2))
            c = tuple(x + y * t for x, y in zip(a, ab))
            d2 = sum((q - x) ** 2 for q, x in zip(pos, c))
            if best is None or d2 < best[0]:
                best = (d2, i, t, c)
        if hint is not None and best[0] > 400.0 ** 2:
            return self.project(pos, None)
        _, i, t, c = best
        up = norm(lerp(self.u[i], self.u[(i + 1) % n], t))
        return self.cum[i] + t * (self.cum[i + 1] - self.cum[i]), c, up, i

    def at(self, s):
        s %= self.length
        lo, hi = 0, len(self.p)
        while hi - lo > 1:
            mid = (lo + hi) // 2
            if self.cum[mid] <= s:
                lo = mid
            else:
                hi = mid
        i, j = lo, (lo + 1) % len(self.p)
        seg = self.cum[i + 1] - self.cum[i]
        t = (s - self.cum[i]) / seg if seg > 1e-6 else 0.0
        return lerp(self.p[i], self.p[j], t), norm(lerp(self.u[i], self.u[j], t))


def dist(a, b):
    return math.sqrt(sum((x - y) ** 2 for x, y in zip(a, b)))


def lerp(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def norm(v):
    m = math.sqrt(sum(x * x for x in v)) or 1.0
    return tuple(x / m for x in v)


def angle_deg(a, b):
    a, b = norm(a), norm(b)
    return math.degrees(math.acos(max(-1.0, min(1.0, sum(x * y for x, y in zip(a, b))))))


def pct(values, q):
    if not values:
        return None
    v = sorted(values)
    k = (len(v) - 1) * q
    f, c = math.floor(k), math.ceil(k)
    return v[f] if f == c else v[f] + (v[c] - v[f]) * (k - f)


# ---------------------------------------------------------------- measuring one flight

def derive(f, lane):
    """Flight format 2 records the raw flight; measure each sample against the rail here: arc length s, height above
    the rail, sideways offset, and the gap to its contact shell (the distance to the shell's BOX, which holds the
    stella, so it never overstates the gap)."""
    hx, hy = (f.get("shellHalf") or [9.0, 9.0, 12.0])[:2]
    hint = None
    for r in f["rows"]:
        pos = (r["x"], r["y"], r["z"])
        s, c, up, hint = lane.project(pos, hint)
        rel = tuple(q - x for q, x in zip(pos, c))
        h = sum(a * b for a, b in zip(rel, up))
        lat = math.sqrt(max(0.0, sum(a * a for a in rel) - h * h))
        r["s"], r["height"], r["lateral"] = s, h, lat
        r["gap"] = math.hypot(max(0.0, abs(h) - hy), max(0.0, lat - hx))
    if "skimLift" not in f:
        f["skimLift"] = max(0.0, hy - f.get("referenceShellHeight", 1.5))


def measure(f):
    if not f.get("lane"):
        return None
    lane = Lane(f["lane"])
    if f["rows"] and "s" not in f["rows"][0]:
        derive(f, lane)
    # held = 1: the AI flew the hull (the Third Eye's You / AI toggle); that is not the human's flight.
    rows = [r for r in f["rows"] if r.get("speed", 0) > 5 and not r.get("held", 0) > 0.5]
    skim = [r for r in rows if 0 <= r.get("gap", -1) < SKIM_REACH]
    line_h = statistics.median([r["height"] for r in skim]) if skim else None

    # Look-ahead: the distance down the rail whose skim-line point the human's heading points at most nearly.
    looks = []
    for r in rows[::2]:
        pos, fwd = (r["x"], r["y"], r["z"]), (r["fx"], r["fy"], r["fz"])
        best, best_l = 180.0, None
        for L in range(20, 420, 10):
            q, up = lane.at(r["s"] + L)
            if line_h is not None:
                q = tuple(a + b * line_h for a, b in zip(q, up))
            a = angle_deg(fwd, tuple(x - y for x, y in zip(q, pos)))
            if a < best:
                best, best_l = a, L
        if best_l is not None and best < 12.0:
            looks.append((best_l, r["speed"]))

    # Stick gain: stick deflection per degree of heading error toward the look-ahead point.
    gains = []
    look_s = statistics.median([l / max(v, 1.0) for l, v in looks]) if looks else None
    if look_s:
        for r in rows[::2]:
            L = min(max(r["speed"] * look_s, 20.0), 500.0)
            q, up = lane.at(r["s"] + L)
            if line_h is not None:
                q = tuple(a + b * line_h for a, b in zip(q, up))
            err = angle_deg((r["fx"], r["fy"], r["fz"]), tuple(x - y for x, y in zip(q, (r["x"], r["y"], r["z"]))))
            stick = math.hypot(r["xSum"], r["ySum"])
            if 3.0 <= err <= 30.0 and stick < 0.95:
                gains.append(stick / err)

    # Turning: how often the stick crosses centre, and how far it goes.
    xs = [r["xSum"] for r in rows]
    crossings, last = 0, 0
    for x in xs:
        sgn = 1 if x > 0.2 else -1 if x < -0.2 else 0
        if sgn and last and sgn != last:
            crossings += 1
        if sgn:
            last = sgn
    seconds = max(rows[-1]["t"] - rows[0]["t"], 1e-3) if rows else 1e-3
    gaps = [r["gap"] for r in rows if r.get("gap", -1) >= 0]
    return {
        "name": os.path.basename(f["name"]), "finished": bool(f.get("finished")), "raceSeconds": f.get("raceSeconds"),
        "rings": f.get("rings"), "target": f.get("raceTarget"), "intensity": f.get("intensity"), "skimLift": f.get("skimLift", 0.0),
        "samples": len(rows), "skimShare": len(skim) / max(len(rows), 1),
        "lineHeight": line_h, "gapP10": pct([r["gap"] for r in skim], 0.1), "gapMedian": pct([r["gap"] for r in skim], 0.5),
        "contactShare": sum(1 for g in gaps if g <= 0.05) / max(len(gaps), 1),
        "lookSeconds": look_s, "lookP10": pct([l for l, _ in looks], 0.1), "gain": pct(gains, 0.5),
        "turnsPerSecond": crossings / seconds, "meanStick": statistics.mean(abs(x) for x in xs) if xs else 0.0,
        "meanSpeed": statistics.mean(r["speed"] for r in rows) if rows else 0.0,
        "meanBoost": statistics.mean(r["boost"] for r in rows) if rows else 0.0,
        "lateralMedian": pct([r["lateral"] for r in skim], 0.5),
        "reactionLag": reaction_lag(f),
    }


def reaction_lag(f):
    """How late the pilot answers the ring (SITUATION_RECORDING_PLAN.md §7 V4), measured by situation_features.py: from
    the screen when the flight recorded the camera, else from the hull's frame. Reported, never applied to the AI here."""
    try:
        import situation_features as sf
        C = sf.course_from(sf.load_course_js(), int(f.get("intensity") or 1))
        if not C or not f.get("columns"):
            return None
        return sf.analyze(f, C)["lag"]
    except Exception as e:   # the rail learner must not fail on the situation reader
        return {"error": f"{type(e).__name__}: {e}"}


# ---------------------------------------------------------------- from measurements to the policy

def current_policy():
    """The Regatta policy as author_skimrace_ai_config.py has it now (values the learned ones are judged against)."""
    import importlib.util
    spec = importlib.util.spec_from_file_location("author_cfg", AUTHOR)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return dict(mod.POLICIES["SkimRaceAIConfig_Regatta"]), getattr(mod, "REGATTA_LEARNED", {})


def choose(ms):
    """The races that teach: the fastest three finished ones when there are three or more, else every race."""
    done = sorted([m for m in ms if m["finished"] and m["raceSeconds"]], key=lambda m: m["raceSeconds"])
    return (done[:3], "the fastest 3 finished races") if len(done) >= 3 else (ms, "every race (fewer than 3 finished)")


def learn(ms, base):
    used, why_used = choose(ms)
    def med(key):
        vals = [m[key] for m in used if m.get(key) is not None]
        return statistics.median(vals) if vals else None

    def clamp(key, v):
        lo, hi = BANDS[key]
        return round(min(max(v, lo), hi), 3)

    out, notes = {}, []
    h, lift = med("lineHeight"), med("skimLift") or 0.0
    if h is not None:
        out["SkimHeight"] = clamp("SkimHeight", h - lift)
        notes.append(("SkimHeight", base.get("SkimHeight"), out["SkimHeight"],
                      f"you skim {h:.2f} u above your rail's centre; minus the course's skim lift {lift:.2f}"))
    g = med("gapP10")
    if g is not None:
        for key in ("TrackGuardMargin", "MassGuardMargin"):
            now = float(base.get(key, BANDS[key][0]))
            v = clamp(key, max(now, 0.6 * g))
            out[key] = v
            notes.append((key, now, v, f"your closest 10% of skimming was {g:.2f} u from the shell; margins only ever widen"))
    ls = med("lookSeconds")
    if ls is not None:
        out["LookaheadSeconds"] = clamp("LookaheadSeconds", ls)
        notes.append(("LookaheadSeconds", base.get("LookaheadSeconds"), out["LookaheadSeconds"],
                      f"you point at the rail {ls:.2f} s ahead"))
    lp = med("lookP10")
    if lp is not None:
        out["LookaheadMin"] = clamp("LookaheadMin", lp)
        notes.append(("LookaheadMin", base.get("LookaheadMin"), out["LookaheadMin"], f"your shortest look-aheads are {lp:.0f} u"))
    gn = med("gain")
    if gn is not None:
        out["StickGainPerDegree"] = clamp("StickGainPerDegree", gn)
        notes.append(("StickGainPerDegree", base.get("StickGainPerDegree"), out["StickGainPerDegree"],
                      f"you deflect the stick {gn:.3f} per degree off your aim"))
    return out, notes, used, why_used


def report(ms, notes, used, why_used):
    lines = ["| Race | Finished | Time (s) | Rings | Skimming | Contact | Height above rail | Closest 10% gap | Look-ahead | Turns/s | Reaction lag |",
             "|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|"]
    for m in ms:
        lines.append(f"| {m['name']} | {'yes' if m['finished'] else 'no'} | {m['raceSeconds'] or 0:.1f} | {m['rings']}/{m['target']} | "
                     f"{m['skimShare']:.0%} | {m['contactShare']:.1%} | {fmt(m['lineHeight'])} | {fmt(m['gapP10'])} | "
                     f"{fmt(m['lookSeconds'], 's')} | {m['turnsPerSecond']:.2f} | {fmt_lag(m.get('reactionLag'))} |")
    lines += ["", f"Learned from {why_used}: {', '.join(m['name'] for m in used)}.", "",
              "| Setting | Was | Learned | Why |", "|---|---:|---:|---|"]
    for key, was, new, why in notes:
        lines.append(f"| `{key}` | {fmt(was)} | **{fmt(new)}** | {why} |")
    return "\n".join(lines)


def fmt_lag(g):
    if not g or "lag" not in g:
        return "-"
    return f"{g['lag']} s ({g['source']})"


def fmt(v, unit=""):
    return "-" if v is None else (f"{v:.3g}{unit}" if isinstance(v, float) else f"{v}{unit}")


def write_overrides(learned, n_flights):
    text = open(AUTHOR, encoding="utf-8").read()
    if BEGIN not in text or END not in text:
        raise SystemExit(f"ERROR: {AUTHOR} has no {BEGIN} ... {END} block")
    stamp = datetime.date.today().strftime("%Y%m%d")
    body = dict(learned)
    body["PolicyVersion"] = f"regatta-learned-{stamp}-{n_flights}flights"
    lines = [BEGIN + " (generated by Tools/Build/learn_from_flights.py --write; do not edit by hand)", "REGATTA_LEARNED = {"]
    for k in sorted(body):
        v = body[k]
        lines.append(f"    {json.dumps(k)}: {json.dumps(v)},")
    lines.append("}")
    new_block = "\n".join(lines) + "\n"
    text = re.sub(re.escape(BEGIN) + r"[^\n]*\n.*?" + re.escape(END), lambda _: new_block + END, text, count=1, flags=re.S)
    open(AUTHOR, "w", encoding="utf-8").write(text)
    r = subprocess.run([sys.executable, AUTHOR], capture_output=True, text=True)
    print(r.stdout.strip().splitlines()[-1] if r.stdout.strip() else r.stderr)
    return body["PolicyVersion"]


# ---------------------------------------------------------------- main

def run(sources, write):
    ms, problems = [], []
    for name, text in sources:
        f, err = parse(name, text)
        if err:
            problems.append(err)
            continue
        if f.get("mode") != "Regatta" or f.get("hull") != "Squirrel":
            problems.append(f"{os.path.basename(name)}: {f.get('mode')} / {f.get('hull')} - only Regatta Squirrel flights teach this policy")
            continue
        m = measure(f)
        if m is None:
            problems.append(f"{os.path.basename(name)}: no rail in the file ({f.get('laneError') or 'empty lane'})")
            continue
        if m["samples"] < 50:
            problems.append(f"{m['name']}: only {m['samples']} moving samples")
            continue
        ms.append(m)
    for p in problems:
        print("skipped:", p)
    if not ms:
        print("No Regatta Squirrel flights to learn from. Fly Regatta as the Squirrel by hand (recording is automatic), "
              "stop Play and press Store AI record.")
        return 1
    base, _ = current_policy()
    learned, notes, used, why_used = learn(ms, base)
    text = report(ms, notes, used, why_used)
    print(text)
    if write:
        version = write_overrides(learned, len(used))
        doc = ("# What the Regatta Squirrel AI learned from human flights\n\n"
               "Generated by `python3 Tools/Build/learn_from_flights.py --write` "
               f"({datetime.date.today().isoformat()}, policy `{version}`). The numbers are overrides on "
               "`POLICIES[\"SkimRaceAIConfig_Regatta\"]` in `Tools/Build/author_skimrace_ai_config.py`, which writes "
               "`Assets/Resources/SkimRaceAIConfig_Regatta.asset`. How it works: `Docs/SKIM_RACE_AI.md` §16.\n\n" + text + "\n")
        open(REPORT_DOC, "w", encoding="utf-8").write(doc)
        print(f"wrote {os.path.relpath(REPORT_DOC, REPO)}")
    return 0


def self_test():
    import tempfile
    # A circular rail of radius 300 with up pointing outward; the human flies 11 u outside it, steering a little.
    lane, n = [], 240
    for i in range(n):
        a = 2 * math.pi * i / n
        lane.append([300 * math.cos(a), 0, 300 * math.sin(a), math.cos(a), 0, math.sin(a)])
    def flight(seconds, finished, h=11.0):
        rows, speed, R = [], 150.0, 300.0 + h
        for k in range(int(seconds * 10)):
            t = k / 10
            a = speed * t / R
            x, z = R * math.cos(a), R * math.sin(a)
            # heading points at the line 0.5 s ahead (75 u), so the learned look-ahead is ~0.5 s
            a2 = a + 75.0 / R
            fx, fz = R * math.cos(a2) - x, R * math.sin(a2) - z
            m = math.hypot(fx, fz)
            s = (a % (2 * math.pi)) * 300.0
            stick = 0.3 * math.sin(t)
            rows.append([t, x, 0, z, fx / m, 0, fz / m, speed, 2.0, stick, 0.05, 0, 0, 1, 0, s, h, 0.0, max(0.0, h - 9.0)])
        return {"schema": SCHEMA, "mode": "Regatta", "hull": "Squirrel", "intensity": 1, "finished": finished,
                "raceSeconds": seconds, "rings": 8, "raceTarget": 8, "skimLift": 7.5, "lane": lane,
                "columns": ["t", "x", "y", "z", "fx", "fy", "fz", "speed", "boost", "xSum", "ySum", "xDiff", "yDiff",
                            "throttle", "rings", "s", "height", "lateral", "gap"], "samples": rows}
    with tempfile.TemporaryDirectory() as tmp:
        for i, (secs, fin) in enumerate([(30, True), (32, True), (31, True), (40, True), (20, False)]):
            json.dump(flight(secs, fin), open(os.path.join(tmp, f"flight_Regatta_Squirrel_{i}.json"), "w"))
        json.dump({"schema": SCHEMA, "mode": "SkimRace", "hull": "Squirrel", "columns": [], "samples": [], "lane": []},
                  open(os.path.join(tmp, "flight_other.json"), "w"))
        ms = []
        for name, text in flights_in_dir(tmp):
            f, err = parse(name, text)
            if not err and f["mode"] == "Regatta":
                ms.append(measure(f))
        assert len(ms) == 5, len(ms)
        used, why = choose(ms)
        assert len(used) == 3 and all(m["finished"] for m in used) and max(m["raceSeconds"] for m in used) == 32, (used, why)
        learned, notes, _, _ = learn(ms, {"TrackGuardMargin": 1.642, "MassGuardMargin": 1.298, "SkimHeight": 4.346})
        assert abs(learned["SkimHeight"] - 3.5) < 0.05, learned          # 11 u above the rail minus the 7.5 lift
        assert learned["TrackGuardMargin"] >= 1.642 and learned["MassGuardMargin"] >= 1.298, learned   # only widen
        assert 0.35 <= learned["LookaheadSeconds"] <= 0.65, learned      # aimed 0.5 s ahead
        for k, (lo, hi) in BANDS.items():
            if k in learned:
                assert lo <= learned[k] <= hi, (k, learned[k])
        # A flight inside the shell (gap 0) cannot pull the margins in, and a height below the plate is clamped.
        low = learn([measure(parse("x", json.dumps(flight(30, True, h=7.0)))[0])], {"TrackGuardMargin": 1.642, "MassGuardMargin": 1.3})[0]
        assert low["TrackGuardMargin"] == 1.642 and low["SkimHeight"] == BANDS["SkimHeight"][0], low
    # Format 2: the raw flight only (no rail columns); the learner measures it against the rail itself.
    raw = flight(30, True)
    keep = ["t", "x", "y", "z", "fx", "fy", "fz", "speed", "boost", "xSum", "ySum", "xDiff", "yDiff", "throttle", "rings"]
    idx = [raw["columns"].index(c) for c in keep]
    raw["samples"] = [[r[i] for i in idx] for r in raw["samples"]]
    raw["columns"] = keep
    raw["shellHalf"] = [9.0, 9.0, 12.0]
    raw["referenceShellHeight"] = 1.5
    del raw["skimLift"]
    m2 = measure(parse("raw", json.dumps(raw))[0])
    assert abs(m2["lineHeight"] - 11.0) < 0.3 and abs(m2["skimLift"] - 7.5) < 1e-6, m2
    assert m2["gapP10"] is not None and abs(m2["gapP10"] - 2.0) < 0.3, m2          # 11 above, shell reaches 9
    assert abs(learn([m2], {"TrackGuardMargin": 1.642, "MassGuardMargin": 1.3})[0]["SkimHeight"] - 3.5) < 0.3
    # Seconds the AI held the hull (held = 1, the You / AI toggle) are not the human's flight: half the race flown
    # 30 u up by the AI must not count against the human's time on the skim line. Negative control: with the held
    # column dropped it does (half the race then looks off the line).
    held = flight(30, True)
    hi = flight(30, True, h=30.0)
    held["columns"] = held["columns"] + ["held"]
    held["samples"] = [(b if k % 2 else a) + [k % 2] for k, (a, b) in enumerate(zip(held["samples"], hi["samples"]))]
    m3 = measure(parse("held", json.dumps(held))[0])
    assert m3["skimShare"] > 0.95 and abs(m3["lineHeight"] - 11.0) < 0.3, m3
    held["columns"], held["samples"] = held["columns"][:-1], [r[:-1] for r in held["samples"]]
    m4 = measure(parse("held-dropped", json.dumps(held))[0])
    assert m4["skimShare"] < 0.6, ("negative control: the held rows should have counted", m4)
    # The author script carries the block this writes into.
    text = open(AUTHOR, encoding="utf-8").read()
    assert BEGIN in text and END in text, "author_skimrace_ai_config.py lost its learn_from_flights block"
    print("learn_from_flights self-test: ok")
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dir", action="append", help="a local folder of flight_*.json (repeatable); default: every origin branch")
    ap.add_argument("--write", action="store_true", help="write the overrides, regenerate the asset and the report doc")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        return self_test()
    if a.dir:
        sources = [s for d in a.dir for s in flights_in_dir(d)]
    else:
        git("fetch", "-q", "origin", "+refs/heads/bugledger/*:refs/remotes/origin/bugledger/*")
        sources = flights_on_branches()
    return run(sources, a.write)


if __name__ == "__main__":
    sys.exit(main())
