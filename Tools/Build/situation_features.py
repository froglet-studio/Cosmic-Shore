#!/usr/bin/env python3
"""situation_features.py - what a pilot was facing, doing and trying to do, moment by moment (Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md S3).

Yash, 2026-10-15: "record the situations: what target the player was aiming at, where the ring or the target is, where
the obstacles are, how we skim, how quickly we change direction, how we ignore some skims when we must keep speed... This
is how we can actually train the AI, not just by input."

The Python twin of Docs/Studios/RegattaAI/situation.js (the Training AI for Regatta page): the same features, labels,
legs, skim choices and learning, line for line, so a flight Unity recorded (HumanFlightRecorder, flight/2 or flight/3)
and a flight the page recorded give the same answers. The definitions are in Docs/AI_SYSTEM/situation_features.json.
--self-test runs both on the same fixture flight and fails on any difference.

    python3 Tools/Build/situation_features.py                     # every Regatta flight on every origin branch (Send to Claude)
    python3 Tools/Build/situation_features.py --dir <folder>      # ...or a local folder of flight_*.json
    python3 Tools/Build/situation_features.py --json <file>       # one flight: the full analysis as JSON on stdout
    python3 Tools/Build/situation_features.py --self-test         # Python against situation.js on the fixture (needs node)
"""
import argparse
import json
import math
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
COURSE_JS = os.path.join(REPO, "Docs", "Studios", "RegattaAI", "course.js")
SITUATION_JS = os.path.join(REPO, "Docs", "Studios", "RegattaAI", "situation.js")
FIXTURE = os.path.join(HERE, "situation_fixtures", "flight_artifact_squirrel_I1.json")
sys.path.insert(0, HERE)

VERSION = 1
DEG = 180 / math.pi
PRISM_HALF = 3
LEAD_CAP, LEAD_SHARE, THROUGH = 480, 0.4, 320
PHASES = ["cruise", "approach", "line-up", "thread", "exit", "skim run", "boost straight", "recovery"]


# ---------------------------------------------------------------- JavaScript's rounding and vectors

def jsround(x):
    return math.floor(x + 0.5)


def r2(x):
    return jsround(x * 100) / 100


def sub(a, b): return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]
def add(a, b): return [a[0] + b[0], a[1] + b[1], a[2] + b[2]]
def mul(a, s): return [a[0] * s, a[1] * s, a[2] * s]
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]
def vlen(a): return math.sqrt(dot(a, a))
def dist(a, b): return vlen(sub(a, b))
def clamp(x, lo, hi): return lo if x < lo else hi if x > hi else x


def norm(a):
    n = vlen(a)
    return mul(a, 1 / n) if n > 1e-9 else [0, 0, 0]


def angle(a, b):
    la, lb = vlen(a), vlen(b)
    if la < 1e-9 or lb < 1e-9:
        return 0
    return math.acos(clamp(dot(a, b) / (la * lb), -1, 1)) * DEG


def pct(arr, q):
    if not arr:
        return None
    s = sorted(arr)
    return s[min(len(s) - 1, max(0, jsround(q * (len(s) - 1))))]


# ---------------------------------------------------------------- the course (course.js is generated from RegattaCourse)

def load_course_js(path=COURSE_JS):
    text = open(path, encoding="utf-8").read()
    body = text[text.index("=") + 1:].strip().rstrip(";")
    body = re.sub(r"/\*.*?\*/", "", body, flags=re.S)
    body = re.sub(r"([{,]\s*)([A-Za-z_]\w*|\d+)\s*:", r'\1"\2":', body).replace("'", '"')
    body = re.sub(r",\s*([}\]])", r"\1", body)
    return json.loads(body)


def course_from(COURSE, intensity):
    src = COURSE.get(str(intensity))
    if not src:
        return None
    lanes = []
    for flat in src["lanes"]:
        lanes.append([[flat[k], flat[k + 1], flat[k + 2]] for k in range(0, len(flat) - 2, 3)])
    gates = [{"p": list(g["p"]), "a": norm(g["a"]), "r": g["r"]} for g in src["gates"]]
    return {"intensity": intensity, "gates": gates, "lanes": lanes, "laneDomains": list(COURSE.get("laneDomains", []))}


# ---------------------------------------------------------------- a flight -> columns

def normalize(f):
    cols = f.get("columns") or []
    idx = {c: i for i, c in enumerate(cols)}
    rows = f.get("samples") or []
    n = len(rows)

    def col(name, dflt):
        k = idx.get(name)
        return [dflt if k is None or k >= len(r) or r[k] is None else float(r[k]) for r in rows]

    hull = f.get("hull") or ""
    N = {
        "schema": f.get("schema"), "source": f.get("source", "unity"), "mode": f.get("mode"), "hull": hull,
        "hullKey": (f.get("hullKey") or hull).lower(), "verb": f.get("verb") or ("skim" if hull.lower() == "squirrel" else ""),
        "domain": (f.get("domain") or "").lower(), "intensity": int(f.get("intensity") or 1), "hz": float(f.get("sampleHz") or 10),
        "turn": f.get("turn"), "skimReach": float(f["skimReach"]) if f.get("skimReach") is not None else (7.5 if hull.lower() == "squirrel" else 0),
        "raceTarget": int(f.get("raceTarget") or 0), "finished": bool(f.get("finished")), "overrides": f.get("overrides") or [],
        "n": n, "t": col("t", 0), "spd": col("speed", 0), "boost": col("boost", 1), "thr": col("throttle", 1), "lt": col("lt", 0),
        "rt": col("rt", 0), "rings": [int(x) for x in col("rings", 0)], "held": col("held", 0),
        "skims": col("skims", 0) if "skims" in idx else None, "rams": col("rams", 0) if "rams" in idx else None,
        "p": [], "f": [], "u": [], "rv": f.get("rivals"), "rivalHulls": f.get("rivalHulls") or [],
    }
    for r in rows:
        N["p"].append([float(r[idx["x"]]), float(r[idx["y"]]), float(r[idx["z"]])])
        N["f"].append(norm([float(r[idx["fx"]]), float(r[idx["fy"]]), float(r[idx["fz"]])]))
        N["u"].append(norm([float(r[idx["ux"]]), float(r[idx["uy"]]), float(r[idx["uz"]])]))
    return N


def omega_fn(N, turn_rates):
    t = N.get("turn")
    if t and t.get("p") is not None:
        return lambda v: min(t["p"], t["y"]) + v * (t.get("rts") or 0)
    hi = pct([abs(x) for x in turn_rates], 0.95) or 60
    hi = max(30, hi)
    return lambda v: hi


# ---------------------------------------------------------------- features

def features(N, C):
    G, ng, n, dt = C["gates"], len(C["gates"]), N["n"], 1 / N["hz"]
    own = C["laneDomains"].index(N["domain"]) if N["domain"] in C["laneDomains"] else -1
    turn_rates = []
    for i in range(n):
        a, b = N["f"][max(0, i - 1)], N["f"][min(n - 1, i + 1)]
        span = (min(n - 1, i + 1) - max(0, i - 1)) * dt
        turn_rates.append(angle(a, b) / span if span > 0 else 0)
    omega = omega_fn(N, turn_rates)
    out = []
    for i in range(n):
        p, f, u = N["p"][i], N["f"][i], N["u"][i]
        right, v = norm(cross(f, u)), max(1, N["spd"][i])
        ring = N["rings"][i] % ng
        g, g2, g0 = G[ring], G[(ring + 1) % ng], G[(ring - 1 + ng) % ng]
        rel = sub(g["p"], p)
        d, fz = vlen(rel), dot(rel, f)
        side = 1 if dot(sub(p, g["p"]), g["a"]) >= 0 else -1
        in_dir = mul(g["a"], -side)
        fa, mouth_offset, threads = dot(f, g["a"]), -1, False
        if abs(fa) > 1e-4:
            t_hit = -dot(sub(p, g["p"]), g["a"]) / fa
            if t_hit > 0:
                off = sub(add(p, mul(f, t_hit)), g["p"])
                mouth_offset = vlen(sub(off, mul(g["a"], dot(off, g["a"]))))
                threads = mouth_offset <= g["r"]
        leg_len = dist(g0["p"], g["p"])
        lead = min(LEAD_CAP, LEAD_SHARE * leg_len)
        heading_err = angle(f, rel)
        w = omega(N["spd"][i]) / DEG
        r_min = v / max(1e-3, w)
        r_need = d / (2 * math.sin(min(math.pi / 2, heading_err / DEG))) if heading_err > 1 else 1e6
        gap, gap_own, ahead_n, ahead_gap, ahead_bearing = 1e6, False, 0, 1e6, 0
        skim_detour, skim_t, skim_pt, skim_lane = 1e6, 1e6, None, -1
        reach, horizon = N["skimReach"], max(60, v * 2)
        for l, pts in enumerate(C["lanes"]):
            for q in pts:
                dx, dy, dz = q[0] - p[0], q[1] - p[1], q[2] - p[2]
                if abs(dx) > horizon + 10 or abs(dy) > horizon + 10 or abs(dz) > horizon + 10:
                    continue
                dq = [dx, dy, dz]
                dd = vlen(dq)
                gq = dd - PRISM_HALF
                if gq < gap:
                    gap, gap_own = gq, l == own
                fwdq = dot(dq, f)
                if fwdq <= 0:
                    continue
                ang = angle(f, dq)
                if ang < 30 and dd < 160:
                    ahead_n += 1
                    if gq < ahead_gap:
                        ahead_gap, ahead_bearing = gq, math.atan2(dot(dq, right), fwdq) * DEG
                if reach > 0 and ang < 35 and 15 < dd < horizon:
                    det = dd + dist(q, g["p"]) - d
                    if det < skim_detour:
                        skim_detour, skim_t, skim_pt, skim_lane = det, dd / v, q, l
        place, rv = 1, (N["rv"][i] if N["rv"] and i < len(N["rv"]) else None)
        if rv:
            for j in range(0, len(rv) - 3, 4):
                if rv[j + 3] > N["rings"][i]:
                    place += 1
        out.append({
            "t": N["t"][i], "ring": ring, "dist": d, "bearing": math.atan2(dot(rel, right), fz) * DEG,
            "elevation": math.atan2(dot(rel, u), math.sqrt(fz * fz + dot(rel, right) ** 2)) * DEG,
            "headingErr": heading_err, "axisAngle": angle(f, in_dir), "mouthOffset": mouth_offset, "threadsIfStraight": threads,
            "mouthRadius": g["r"], "timeToMouth": d / v, "legLen": leg_len, "legTurn": angle(sub(g["p"], g0["p"]), sub(g2["p"], g["p"])),
            "nextLeg": dist(g["p"], g2["p"]), "approachLead": lead, "rMin": r_min, "rNeed": r_need, "canTurn": r_need >= r_min,
            "turnRate": turn_rates[i], "turnJerk": 0, "speed": N["spd"][i], "throttle": N["thr"][i], "boost": N["boost"][i], "lt": N["lt"][i],
            "gap": gap, "gapOwnRail": gap_own, "aheadPrisms": ahead_n, "aheadGap": ahead_gap, "aheadBearing": ahead_bearing,
            "skimInReach": reach > 0 and gap < reach, "skimDetour": skim_detour, "skimTime": skim_t, "skimPoint": skim_pt, "skimLane": skim_lane,
            "lap": N["rings"][i] // ng + 1, "ringsLeft": N["raceTarget"] - N["rings"][i] if N["raceTarget"] else None,
            "place": place if rv else None, "side": side, "held": N["held"][i] > 0.5,
        })
    for i in range(1, n):
        out[i]["turnJerk"] = (out[i]["turnRate"] - out[i - 1]["turnRate"]) * N["hz"]
    return out


# ---------------------------------------------------------------- events, intent, labels

def events(N, C, F):
    G, ng, n = C["gates"], len(C["gates"]), N["n"]
    ev = {"ring": [], "miss": [], "ram": [], "skim": [], "boostPress": []}
    skim_hull = N["skimReach"] > 0
    for i in range(1, n):
        if N["rings"][i] > N["rings"][i - 1]:
            ev["ring"].append(i)
        else:
            g = G[N["rings"][i - 1] % ng]
            s0, s1 = dot(sub(N["p"][i - 1], g["p"]), g["a"]), dot(sub(N["p"][i], g["p"]), g["a"])
            if s0 * s1 < 0:
                t = s0 / (s0 - s1)
                off = sub(add(N["p"][i - 1], mul(sub(N["p"][i], N["p"][i - 1]), t)), g["p"])
                lat = vlen(sub(off, mul(g["a"], dot(off, g["a"]))))
                if g["r"] < lat < g["r"] * 3:
                    ev["miss"].append(i)
        if (N["rams"][i] > N["rams"][i - 1]) if N["rams"] else (N["spd"][i] < N["spd"][i - 1] * 0.65):
            ev["ram"].append(i)
        if skim_hull and ((N["skims"][i] > N["skims"][i - 1]) if N["skims"] else (N["boost"][i] > N["boost"][i - 1] + 0.04)):
            ev["skim"].append(i)
        on = N["lt"][i] > 0.5 or (not skim_hull and N["boost"][i] > 1.05)
        was = N["lt"][i - 1] > 0.5 or (not skim_hull and N["boost"][i - 1] > 1.05)
        if on and not was:
            ev["boostPress"].append(i)
    return ev


def candidates(N, C, F, i):
    x, G = F[i], C["gates"]
    ng = len(G)
    g = G[x["ring"]]
    in_dir = mul(g["a"], -x["side"])
    lst = [
        {"kind": "mouth", "at": g["p"]},
        {"kind": "approach", "at": add(g["p"], mul(g["a"], x["side"] * x["approachLead"]))},
        {"kind": "through", "at": add(g["p"], mul(in_dir, THROUGH))},
        {"kind": "next ring", "at": G[(x["ring"] + 1) % ng]["p"]},
    ]
    if x["skimPoint"] is not None and x["skimDetour"] < 40:
        lst.append({"kind": "skim", "at": x["skimPoint"]})
    rv = N["rv"][i] if N["rv"] and i < len(N["rv"]) else None
    if rv:
        best, bd = None, 150
        for j in range(0, len(rv) - 3, 4):
            q = [rv[j], rv[j + 1], rv[j + 2]]
            d = dist(q, N["p"][i])
            if d < bd:
                bd, best = d, q
        if best:
            lst.append({"kind": "rival", "at": best})
    return lst


def intent(N, C, F, i):
    K = max(2, jsround(0.8 * N["hz"]))
    cands = candidates(N, C, F, i)
    scores = []
    for c in cands:
        s = m = 0
        k = 1
        while k <= K and i + k < N["n"]:
            s += angle(N["f"][i + k], sub(c["at"], N["p"][i + k]))
            m += 1
            k += 1
        scores.append(s / m if m else 180)
    order = sorted(range(len(cands)), key=lambda c: scores[c])
    best = order[0]
    second = scores[order[1]] if len(order) > 1 else 180
    return {"target": cands[best]["kind"], "at": cands[best]["at"], "err": scores[best], "conf": clamp((second - scores[best]) / max(1, second), 0, 1)}


def labels(N, C, F, ev):
    n, hz = N["n"], N["hz"]

    def near(lst, i, before, after):
        return any(-after * hz <= e - i <= before * hz for e in lst)

    out, recovering, last_ring = [], False, -1
    miss, ring_set = set(ev["miss"]), set(ev["ring"])
    for i in range(n):
        x = F[i]
        it = intent(N, C, F, i)
        if i in miss:
            recovering = True
        if i in ring_set:
            recovering, last_ring = False, i
        if recovering and x["headingErr"] < 25:
            recovering = False
        boosting = N["lt"][i] > 0.5 or (N["skimReach"] == 0 and N["boost"][i] > 1.05)
        if recovering:
            ph = "recovery"
        elif near(ev["ring"], i, 1.0, 0):
            ph = "thread"
        elif last_ring >= 0 and i - last_ring <= 0.8 * hz:
            ph = "exit"
        elif near(ev["skim"], i, 0.5, 0.5) or it["target"] == "skim":
            ph = "skim run"
        elif x["timeToMouth"] < 4 and x["turnRate"] > 25:
            ph = "line-up"
        elif boosting and x["turnRate"] < 12:
            ph = "boost straight"
        elif x["headingErr"] < 30 or it["target"] in ("approach", "mouth"):
            ph = "approach"
        else:
            ph = "cruise"
        out.append({"phase": ph, "target": it["target"], "targetAt": it["at"], "targetErr": it["err"], "conf": it["conf"], "corrected": False})
    for o in N["overrides"]:
        for i in range(n):
            if o["t0"] <= N["t"][i] <= o["t1"]:
                if o.get("phase"):
                    out[i]["phase"] = o["phase"]
                if o.get("target"):
                    out[i]["target"] = o["target"]
                out[i]["corrected"] = True
    return out


def segments(N, F, L, ev):
    out, s, n = [], 0, N["n"]
    for i in range(1, n + 1):
        if i < n and L[i]["phase"] == L[s]["phase"]:
            continue
        e = i - 1
        threaded = any(s <= r <= min(n - 1, e + 1) for r in ev["ring"])
        missed = any(s <= m <= e for m in ev["miss"])
        out.append({"phase": L[s]["phase"], "i0": s, "i1": e, "t0": r2(N["t"][s]), "t1": r2(N["t"][e]), "dur": r2(N["t"][e] - N["t"][s] + 1 / N["hz"]),
                    "speedIn": jsround(N["spd"][s]), "speedOut": jsround(N["spd"][e]), "threaded": threaded, "missed": missed})
        s = i
    return out


def legs(N, C, F, L, ev):
    out, start = [], 0
    for j in ev["ring"]:
        c = j - 1
        while c > start and L[c]["target"] != "approach":
            c -= 1
        commit = jsround(F[c + 1]["dist"]) if start < c < j - 1 else None
        missed = any(start < m <= j for m in ev["miss"])
        rams = len([m for m in ev["ram"] if start < m <= j])
        out.append({"ring": N["rings"][j - 1] % len(C["gates"]), "i0": start, "i1": j, "t0": r2(N["t"][start]), "time": r2(N["t"][j] - N["t"][start]),
                    "speedIn": jsround(N["spd"][start]), "speedOut": jsround(N["spd"][j]), "legLen": jsround(F[j - 1]["legLen"]),
                    "legTurn": jsround(F[j - 1]["legTurn"]), "commitDist": commit,
                    "clean": not missed and rams == 0 and not any(x["held"] for x in F[start:j]), "missed": missed, "rams": rams})
        start = j
    return out


def skim_choices(N, F, ev):
    if not N["skimReach"] > 0:
        return []
    out, hz, prev = [], N["hz"], -1e9
    for i in ev["skim"]:
        gap_t = N["t"][i] - prev
        prev = N["t"][i]
        if gap_t < 1.5:
            continue
        b = max(0, i - jsround(0.5 * hz))
        x = F[b]
        out.append({"i": b, "t": r2(N["t"][b]), "taken": True, "detour": r2(min(x["skimDetour"], 99)), "timeToMouth": r2(x["timeToMouth"]),
                    "headingErr": r2(x["headingErr"]), "speed": jsround(x["speed"])})
    last = -1e9
    for i in range(N["n"]):
        x = F[i]
        if x["held"] or x["skimDetour"] > 12 or x["skimTime"] > 2 or N["t"][i] - last < 1.5:
            continue
        if any(i < e <= i + 2 * hz for e in ev["skim"]):
            continue
        last = N["t"][i]
        out.append({"i": i, "t": r2(N["t"][i]), "taken": False, "detour": r2(x["skimDetour"]), "timeToMouth": r2(x["timeToMouth"]),
                    "headingErr": r2(x["headingErr"]), "speed": jsround(x["speed"])})
    return sorted(out, key=lambda c: c["t"])


def analyze(flight, course):
    N = normalize(flight) if "columns" in flight else flight
    C = course
    F = features(N, C)
    ev = events(N, C, F)
    L = labels(N, C, F, ev)
    segs, lg, sk = segments(N, F, L, ev), legs(N, C, F, L, ev), skim_choices(N, F, ev)
    boosts = [{"i": i, "t": r2(N["t"][i]), "headingErr": r2(F[i]["headingErr"]), "dist": jsround(F[i]["dist"]), "timeToMouth": r2(F[i]["timeToMouth"])} for i in ev["boostPress"]]
    mistakes = sorted([{"i": i, "t": r2(N["t"][i]), "kind": "missed mouth"} for i in ev["miss"]] + [{"i": i, "t": r2(N["t"][i]), "kind": "rail hit"} for i in ev["ram"]], key=lambda m: m["t"])
    share = {p: 0 for p in PHASES}
    for l in L:
        share[l["phase"]] = share.get(l["phase"], 0) + 1
    share = {p: r2(v / max(1, N["n"])) for p, v in share.items()}
    return {"version": VERSION, "norm": N, "feats": F, "labels": L, "events": ev, "segments": segs, "legs": lg, "skims": sk, "boosts": boosts, "mistakes": mistakes,
            "summary": {"samples": N["n"], "seconds": r2(N["n"] / N["hz"]), "rings": len(ev["ring"]), "missed": len(ev["miss"]), "rams": len(ev["ram"]),
                        "skimsTaken": len([s for s in sk if s["taken"]]), "skimsSkipped": len([s for s in sk if not s["taken"]]), "boostPresses": len(boosts),
                        "meanTurnRate": r2(sum(x["turnRate"] for x in F) / max(1, N["n"])), "phaseShare": share}}


# ---------------------------------------------------------------- learning (step 1 curves, step 2 rules)

def learn(analyses, good_leg=None):
    pts, sk, boost_err = [], [], []
    for A in analyses:
        for g in A["legs"]:
            if g["clean"] and (good_leg is None or good_leg(g, A)) and g["legLen"] > 0 and g["commitDist"] is not None and g["commitDist"] > 0:
                pts.append([g["legLen"], g["commitDist"]])
        sk.extend(A["skims"])
        boost_err.extend(b["headingErr"] for b in A["boosts"])
        boost_err.extend(x["headingErr"] for x in A["feats"] if x["lt"] > 0.5 and not x["held"])
    commit = None
    if len(pts) >= 3:
        mx = sum(q[0] for q in pts) / len(pts)
        my = sum(q[1] for q in pts) / len(pts)
        sxy = sum((q[0] - mx) * (q[1] - my) for q in pts)
        sxx = sum((q[0] - mx) ** 2 for q in pts)
        syy = sum((q[1] - my) ** 2 for q in pts)
        b = clamp(sxy / sxx, -0.5, 1) if sxx > 1e-6 else 0
        a = clamp(my - b * mx, 60, 800)
        fit = sxy * sxy / (sxx * syy) if sxx > 0 and syy > 0 else 0
        med = pct([q[1] for q in pts], 0.5)
        if fit < 0.2:
            a, b = clamp(med, 60, 800), 0
        commit = {"a": jsround(a), "b": r2(b), "n": len(pts), "r2": r2(fit), "median": jsround(med)}
    skim = None
    if len(sk) >= 4 and any(s["taken"] for s in sk) and any(not s["taken"] for s in sk):
        best = None
        for D in range(1, 16):
            T = 0.0
            while T <= 3.001:
                ok = sum(1 for s in sk if (s["detour"] <= D and s["timeToMouth"] >= T) == s["taken"])
                if best is None or ok > best["ok"]:
                    best = {"ok": ok, "D": D, "T": r2(T)}
                T += 0.25
        skim = {"maxDetour": best["D"], "minTimeToMouth": best["T"], "agreement": r2(best["ok"] / len(sk)), "n": len(sk), "taken": len([s for s in sk if s["taken"]])}
    boost_within = {"deg": jsround(pct(boost_err, 0.9)), "n": len(boost_err)} if len(boost_err) >= 5 else None
    return {"commit": commit, "skim": skim, "boostWithin": boost_within}


# ---------------------------------------------------------------- the comparable part of an analysis (the golden test)

def comparable(A):
    F, L = A["feats"], A["labels"]
    keys = ["dist", "bearing", "elevation", "headingErr", "axisAngle", "mouthOffset", "timeToMouth", "legLen", "legTurn", "rMin", "turnRate", "gap", "aheadPrisms", "skimDetour"]
    return {
        "feats": [[r2(min(x[k], 1e5)) if not isinstance(x[k], bool) else int(x[k]) for k in keys] for x in F],
        "threads": [int(x["threadsIfStraight"]) for x in F],
        "labels": [[l["phase"], l["target"]] for l in L],
        "segments": A["segments"], "legs": A["legs"], "skims": A["skims"], "boosts": A["boosts"], "mistakes": A["mistakes"], "summary": A["summary"],
    }


def js_comparable(flight_path):
    """Run situation.js in node on the same flight and return the same comparable shape."""
    script = r"""
const S = require(process.argv[1]); global.window = {}; require(process.argv[2]); const C = S.courseFrom(window.REGATTA_COURSE, %INT%);
const fl = JSON.parse(require('fs').readFileSync(process.argv[3], 'utf8')); const A = S.analyze(fl, C);
const r2 = (x) => Math.round(x * 100) / 100, keys = %KEYS%;
process.stdout.write(JSON.stringify({ feats: A.feats.map((x) => keys.map((k) => typeof x[k] === 'boolean' ? +x[k] : r2(Math.min(x[k], 1e5)))), threads: A.feats.map((x) => +x.threadsIfStraight),
  labels: A.labels.map((l) => [l.phase, l.target]), segments: A.segments, legs: A.legs, skims: A.skims, boosts: A.boosts, mistakes: A.mistakes, summary: A.summary }));
"""
    fl = json.load(open(flight_path, encoding="utf-8"))
    keys = ["dist", "bearing", "elevation", "headingErr", "axisAngle", "mouthOffset", "timeToMouth", "legLen", "legTurn", "rMin", "turnRate", "gap", "aheadPrisms", "skimDetour"]
    script = script.replace("%INT%", str(int(fl.get("intensity") or 1))).replace("%KEYS%", json.dumps(keys))
    r = subprocess.run(["node", "-e", script, SITUATION_JS, COURSE_JS, flight_path], capture_output=True, text=True)
    if r.returncode != 0:
        raise RuntimeError("node failed: " + r.stderr[-800:])
    return json.loads(r.stdout)


def diff(py, js, path="", out=None, tol=0.02):
    out = [] if out is None else out
    if len(out) > 12:
        return out
    if isinstance(py, dict) and isinstance(js, dict):
        for k in sorted(set(py) | set(js)):
            if k not in py or k not in js:
                out.append(f"{path}.{k}: only in {'python' if k in py else 'js'}")
            else:
                diff(py[k], js[k], f"{path}.{k}", out, tol)
    elif isinstance(py, list) and isinstance(js, list):
        if len(py) != len(js):
            out.append(f"{path}: length {len(py)} (python) vs {len(js)} (js)")
        for i, (a, b) in enumerate(zip(py, js)):
            diff(a, b, f"{path}[{i}]", out, tol)
    elif isinstance(py, (int, float)) and isinstance(js, (int, float)) and not isinstance(py, bool):
        if abs(py - js) > tol * max(1, abs(js)):
            out.append(f"{path}: {py} (python) vs {js} (js)")
    elif py != js:
        out.append(f"{path}: {py!r} (python) vs {js!r} (js)")
    return out


def self_test():
    if not os.path.exists(FIXTURE):
        print(f"self-test: fixture missing: {FIXTURE}")
        return 1
    fl = json.load(open(FIXTURE, encoding="utf-8"))
    C = course_from(load_course_js(), int(fl.get("intensity") or 1))
    py = comparable(analyze(fl, C))
    js = js_comparable(FIXTURE)
    problems = diff(json.loads(json.dumps(py)), js)
    # negative control: a changed definition must be caught
    neg = json.loads(json.dumps(py))
    neg["labels"][len(neg["labels"]) // 2][0] = "cruise" if neg["labels"][len(neg["labels"]) // 2][0] != "cruise" else "approach"
    caught = bool(diff(neg, js))
    ok = not problems and caught and py["summary"]["rings"] > 0
    print(f"self-test: {'OK' if ok else 'FAILED'}  ({py['summary']['samples']} samples, {py['summary']['rings']} rings, "
          f"{py['summary']['skimsTaken']} skims taken, {py['summary']['skimsSkipped']} skipped; Python and situation.js "
          f"{'agree' if not problems else 'DIFFER'}; negative control {'caught' if caught else 'NOT caught'})")
    for p in problems:
        print("   ", p)
    return 0 if ok else 1


# ---------------------------------------------------------------- report over recorded flights

def report(sources):
    from learn_from_flights import parse  # the same reader (schema check) as the rail learner
    COURSE = load_course_js()
    analyses = []
    for name, text in sources:
        d, err = parse(name, text)
        if err:
            print("  skip", err)
            continue
        if d.get("mode") != "Regatta":
            continue
        C = course_from(COURSE, int(d.get("intensity") or 1))
        if not C:
            print(f"  skip {name}: intensity {d.get('intensity')} has no course")
            continue
        A = analyze(d, C)
        analyses.append(A)
        m = A["summary"]
        print(f"{os.path.basename(name)}: {d.get('hull')} I{d.get('intensity')}  {m['seconds']} s, {m['rings']} rings, {m['missed']} missed, "
              f"{m['rams']} rail hits, skims {m['skimsTaken']} taken / {m['skimsSkipped']} skipped, {m['boostPresses']} boost presses")
        print("   time in each phase: " + ", ".join(f"{k} {round(v * 100)}%" for k, v in sorted(m["phaseShare"].items(), key=lambda kv: -kv[1]) if v > 0))
    if not analyses:
        print("No Regatta flight found. Fly a Regatta race by hand in Unity and press Send to Claude, or pass --dir.")
        return 0
    L = learn(analyses)
    print("\nLearned from the clean legs (the Training AI for Regatta page also races it against today's AI from the same spots):")
    print("  commit to the ring:", L["commit"] or "needs 3 clean legs with an approach")
    print("  when a skim is worth it:", L["skim"] or "needs skims both taken and skipped")
    print("  boost within:", L["boostWithin"] or "needs boost moments")
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dir")
    ap.add_argument("--json")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        return self_test()
    if a.json:
        fl = json.load(open(a.json, encoding="utf-8"))
        A = analyze(fl, course_from(load_course_js(), int(fl.get("intensity") or 1)))
        A.pop("norm", None)
        json.dump(A, sys.stdout)
        return 0
    from learn_from_flights import flights_in_dir, flights_on_branches
    return report(flights_in_dir(a.dir) if a.dir else flights_on_branches())


if __name__ == "__main__":
    sys.exit(main())
