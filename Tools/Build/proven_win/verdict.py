#!/usr/bin/env python3
"""
Proven Win verdict: reads a candidate's bench reports and applies EVERY rule of Docs/ProvenWin/PLAYBOOK.md, so the
call is mechanical, not a judgment. Prints PASS / FAIL / HOLD with each reason, writes verdict.json and verdict.md (the
WINS.md row) into the candidate folder, and exits 0 on PASS, 1 on FAIL, 2 on HOLD.

    python Tools/Build/proven_win/verdict.py Docs/ProvenWin/candidates/<id>
    python Tools/Build/proven_win/verdict.py --self-test

Layout (written by run.py):
    <candidate>/candidate.json
    <candidate>/reports/<api>/<role>.json      role: null | change | neg | probe_reach | probe_guard | state_<name>

HOLD means "the evidence is not good enough to decide" (a control misbehaved, a role is missing, a probe shows the bench
never reached the state the change guards). Never ship on HOLD; fix the bench and re-run. FAIL means the change is
measured and loses (it differs on screen, or it is not faster in nearly every pair, or it costs something in another
state). Record FAILs in WINS.md's Rejected table with the numbers this prints.
"""
import json
import os
import sys

# The rules, in one place. Change them only with a WINS.md entry explaining why.
NULL_RATIO_TOL = 0.02           # the A-vs-A' null control must read within 2 % of 1.00 ...
NULL_SHARE = (0.30, 0.70)       # ... and B must win about half its pairs (otherwise the instrument is biased)
WIN_SHARE = 0.99                # the change must be faster in at least 99 % of pairs ...
WIN_P90 = 1.0                   # ... and its 90th-percentile B/A must be below 1
ONE_LEVEL_SHARE = 1e-4          # "within one display level" allows at most this share of pixels to move one level
STATE_MAX_RATIO = 1.005         # in any extra state the change must not be slower by more than 0.5 % ...
STATE_MIN_SHARE = 0.30          # ... nor slower in most pairs
SCENE_WIN_SHARE = 0.90          # scene bench: the metric counter must be lower in 90 % of frames (frames are noisier)


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def tier(r):
    """The identity tier of a shader report: bit, display, one-level, or different."""
    if r.get("pixelsBitDifferent", 0) == 0:
        return "bit-identical"
    if r.get("pixelsDifferent8bit", 0) == 0:
        return "display-identical"
    if "maxLevelDiff8bit" not in r:
        return "different (report predates the level metric: re-run)"
    share = r["pixelsDifferent8bit"] / max(1, r.get("pixelsCompared", 1))
    if r["maxLevelDiff8bit"] <= 1 and share <= ONE_LEVEL_SHARE:
        return "within-one-level"
    return "different"


def shader_verdict(cand, reports):
    reasons, holds, fails, decisive = [], [], [], []
    apis = sorted(reports)
    if not apis:
        return "HOLD", ["no reports at all: run run.py first"], {}
    if len(apis) < 3:
        holds.append(f"only {apis} measured; a shader win is measured on dx12, vulkan and d3d11 (Vulkan is the phone path)")
    rows = {}
    for api in apis:
        roles = reports[api]
        for need in ("null", "change"):
            if need not in roles:
                holds.append(f"{api}: no '{need}' report")
        if "change" not in roles:
            continue
        n, c = roles.get("null"), roles["change"]
        if n:
            if abs(n["medianRatioBA"] - 1.0) > NULL_RATIO_TOL or not (NULL_SHARE[0] <= n["shareBFaster"] <= NULL_SHARE[1]):
                holds.append(f"{api}: null control is off (B/A {n['medianRatioBA']:.4f}, B won {100*n['shareBFaster']:.1f} %): "
                             "the desktop is busy or the instrument is biased; re-run when the machine is idle")
            if n.get("pixelsBitDifferent", 0) != 0:
                holds.append(f"{api}: the null control is not bit-identical: the comparison itself is broken")
        t = tier(c)
        faster = c["shareBFaster"] >= WIN_SHARE and c["ratioP90"] < WIN_P90
        rows[api] = dict(a=c["medianA"], b=c["medianB"], ratio=c["medianRatioBA"], p10=c["ratioP10"], p90=c["ratioP90"],
                         share=c["shareBFaster"], tier=t, px8=c.get("pixelsDifferent8bit", 0),
                         levels=c.get("maxLevelDiff8bit"), over1=c.get("pixelsOverOneLevel"))
        if t.startswith("different") and "re-run" not in t:
            decisive.append(f"{api}: output differs ({c.get('pixelsDifferent8bit', 0)} px at 8 bits, up to {c.get('maxLevelDiff8bit')} levels): "
                            "a picture change is decisive, no other measurement can rescue it")
        elif t.startswith("different"):
            fails.append(f"{api}: output differs ({c.get('pixelsDifferent8bit', 0)} px at 8 bits, up to {c.get('maxLevelDiff8bit')} levels)")
        if not faster:
            fails.append(f"{api}: not categorically faster (B won {100*c['shareBFaster']:.1f} % of pairs, p90 B/A {c['ratioP90']:.4f})")
        for role, r in roles.items():
            if role.startswith("state_"):
                st = tier(r)
                if st.startswith("different"):
                    fails.append(f"{api} {role}: output differs in this state ({r.get('pixelsDifferent8bit', 0)} px)")
                if r["medianRatioBA"] > STATE_MAX_RATIO and r["shareBFaster"] < STATE_MIN_SHARE:
                    fails.append(f"{api} {role}: the change COSTS {100*(r['medianRatioBA']-1):.1f} % in this state "
                                 f"(B slower in {100*(1-r['shareBFaster']):.1f} % of pairs)")
    neg = [(api, reports[api]["neg"]) for api in apis if "neg" in reports[api]]
    if not neg:
        holds.append("no negative control: without it 'identical' proves nothing")
    for api, r in neg:
        if r.get("pixelsDifferent8bit", 0) == 0:
            holds.append(f"{api}: the negative control was NOT caught: the comparison cannot see this kind of error")
    if cand.get("earlyOut"):
        reach = [(a, reports[a]["probe_reach"]) for a in apis if "probe_reach" in reports[a]]
        guard = [(a, reports[a]["probe_guard"]) for a in apis if "probe_guard" in reports[a]]
        if not reach or not guard:
            holds.append("an early-out needs both probes (probe_reach: return a constant always; probe_guard: only inside "
                         "the guard). Without them a 'no gain' or an 'identical' may mean the bench never reached the state")
        for a, r in reach:
            if r.get("pixelsBitDifferent", 0) == 0:
                holds.append(f"{a}: probe_reach changed nothing: the code under test is not reached in the bench")
        for a, r in guard:
            if r.get("pixelsBitDifferent", 0) == 0:
                holds.append(f"{a}: probe_guard changed nothing: the guarded state never occurs in the bench (fix the bench "
                             "state: game-state floats, globals, camera distance), so the measurement says nothing")
    # A changed picture fails the candidate outright; otherwise missing or misbehaving evidence holds it.
    verdict = "FAIL" if decisive else ("HOLD" if holds else ("FAIL" if fails else "PASS"))
    reasons = decisive + holds + fails
    if verdict == "PASS":
        tiers = {r["tier"] for r in rows.values()}
        reasons.append("identity tier: " + ", ".join(sorted(tiers)) +
                       ("; within-one-level needs the blind sign-off page before shipping" if "within-one-level" in tiers else ""))
    return verdict, reasons, rows


def scene_verdict(cand, reports):
    holds, fails, rows = [], [], {}
    metric = cand.get("metricCounter")
    if not metric:
        return "HOLD", ["candidate.json has no metricCounter (the profiler counter the change must lower)"], {}
    for api, roles in sorted(reports.items()):
        c = roles.get("change")
        if not c:
            holds.append(f"{api}: no 'change' report")
            continue
        if str(c.get("verdict", "")).startswith("ERROR"):
            holds.append(f"{api}: the bench reported an error: {c['verdict']}")
            continue
        s = next((x for x in c["series"] if x["counter"] == metric), None)
        if not s:
            holds.append(f"{api}: counter '{metric}' not in the report (not available in this player?)")
            continue
        rows[api] = dict(a=s["medianA"], b=s["medianB"], share=s["shareBLower"], screen=c["screenshotPixelsDifferent"])
        if s["medianA"] <= 0:
            holds.append(f"{api}: '{metric}' reads 0 in A: the cost is not present in this bench state")
        if c["screenshotPixelsDifferent"] != 0:
            fails.append(f"{api}: the screen differs ({c['screenshotPixelsDifferent']} px); if the scene animates, freeze it")
        if not (s["medianB"] < s["medianA"] and s["shareBLower"] >= SCENE_WIN_SHARE):
            fails.append(f"{api}: '{metric}' not categorically lower (B lower in {100*s['shareBLower']:.0f} % of frames)")
        n = roles.get("neg")
        if not n:
            holds.append(f"{api}: no negative control")
        elif n["screenshotPixelsDifferent"] == 0:
            holds.append(f"{api}: the negative control was NOT caught by the screenshots")
    verdict = "HOLD" if holds else ("FAIL" if fails else "PASS")
    return verdict, holds + fails, rows


def collect(folder):
    reports = {}
    root = os.path.join(folder, "reports")
    if not os.path.isdir(root):
        return reports
    for api in os.listdir(root):
        d = os.path.join(root, api)
        if not os.path.isdir(d):
            continue
        for f in os.listdir(d):
            if f.endswith(".json"):
                reports.setdefault(api, {})[f[:-5]] = load(os.path.join(d, f))
    return reports


def wins_row(cand, verdict, reasons, rows):
    lines = [f"### {cand.get('id')}: {cand.get('title', '')} -> **{verdict}**", ""]
    if cand.get("kind", "shader") == "shader":
        lines += ["| API | A ms | B ms | B/A (p10-p90) | Speed-up | Pairs B faster | Identity |", "|---|---|---|---|---|---|---|"]
        for api, r in rows.items():
            lines.append(f"| {api} | {r['a']:.3f} | {r['b']:.3f} | {r['ratio']:.4f} ({r['p10']:.3f}-{r['p90']:.3f}) | "
                         f"{1/r['ratio']:.2f}x | {100*r['share']:.1f} % | {r['tier']} ({r['px8']} px) |")
    else:
        lines += [f"| API | {cand.get('metricCounter')} A ms | B ms | B lower in | Screen px different |", "|---|---|---|---|---|"]
        for api, r in rows.items():
            lines.append(f"| {api} | {r['a']:.4f} | {r['b']:.4f} | {100*r['share']:.0f} % of frames | {r['screen']} |")
    lines += ["", "Reasons:"] + [f"- {x}" for x in reasons]
    return "\n".join(lines) + "\n"


def judge(folder):
    cand = load(os.path.join(folder, "candidate.json"))
    reports = collect(folder)
    fn = scene_verdict if cand.get("kind") == "scene" else shader_verdict
    verdict, reasons, rows = fn(cand, reports)
    with open(os.path.join(folder, "verdict.json"), "w", encoding="utf-8") as f:
        json.dump(dict(verdict=verdict, reasons=reasons, rows=rows), f, indent=1)
    md = wins_row(cand, verdict, reasons, rows)
    with open(os.path.join(folder, "verdict.md"), "w", encoding="utf-8") as f:
        f.write(md)
    print(md)
    return verdict


def self_test():
    """Planted cases: every rule must fire on the report built to break it, and a clean case must PASS."""
    def rep(ratio=0.5, share=1.0, p90=0.6, bit=0, px8=0, lv=0, comp=10_000_000):
        return dict(medianA=1.0, medianB=ratio, medianRatioBA=ratio, ratioP10=ratio - 0.01, ratioP90=p90, shareBFaster=share,
                    pixelsBitDifferent=bit, pixelsDifferent8bit=px8, maxLevelDiff8bit=lv, pixelsCompared=comp, pixelsOverOneLevel=0)
    good_null = rep(1.0, 0.5, 1.02)
    good_neg = rep(0.4, 1.0, 0.5, bit=99, px8=99, lv=9)
    base = {api: {"null": good_null, "change": rep(), "neg": good_neg} for api in ("dx12", "vulkan", "d3d11")}
    cases = [
        ("clean", base, {}, "PASS"),
        ("slow pairs", {**base, "vulkan": {**base["vulkan"], "change": rep(share=0.85, p90=1.01)}}, {}, "FAIL"),
        ("differs 3 levels", {**base, "vulkan": {**base["vulkan"], "change": rep(bit=9, px8=9000, lv=3)}}, {}, "FAIL"),
        ("one level, few px", {**base, "dx12": {**base["dx12"], "change": rep(bit=100, px8=100, lv=1)}}, {}, "PASS"),
        ("neg not caught", {**base, "dx12": {**base["dx12"], "neg": rep(bit=0, px8=0)}}, {}, "HOLD"),
        ("null biased", {**base, "dx12": {**base["dx12"], "null": rep(0.95, 0.9, 0.97)}}, {}, "HOLD"),
        ("one api", {"dx12": base["dx12"]}, {}, "HOLD"),
        ("one api that differs", {"vulkan": {"change": rep(bit=9, px8=9000, lv=3)}}, {}, "FAIL"),
        ("costs in a state", {**base, "d3d11": {**base["d3d11"], "state_silenced": rep(1.016, 0.045, 1.03)}}, {}, "FAIL"),
        ("early-out no probes", base, {"earlyOut": True}, "HOLD"),
        ("guard never fires", {a: {**v, "probe_reach": rep(bit=5, px8=5), "probe_guard": rep(bit=0)} for a, v in base.items()},
         {"earlyOut": True}, "HOLD"),
        ("early-out with probes", {a: {**v, "probe_reach": rep(bit=5, px8=5), "probe_guard": rep(bit=5, px8=5)} for a, v in base.items()},
         {"earlyOut": True}, "PASS"),
    ]
    bad = 0
    for name, reports, extra, want in cases:
        got, reasons, _ = shader_verdict(dict(id="t", **extra), reports)
        ok = got == want
        bad += not ok
        print(("ok   " if ok else "FAIL ") + f"{name}: {got} (want {want})" + ("" if ok else f"  {reasons}"))
    scene = {"dx12": {"change": dict(screenshotPixelsDifferent=0, series=[dict(counter="M", medianA=0.15, medianB=0.05, shareBLower=0.99)]),
                      "neg": dict(screenshotPixelsDifferent=5, series=[])}}
    got, reasons, _ = scene_verdict(dict(metricCounter="M"), scene)
    bad += got != "PASS"
    print(("ok   " if got == "PASS" else "FAIL ") + f"scene clean: {got}")
    scene["dx12"]["change"]["series"][0]["shareBLower"] = 0.5
    got, _, _ = scene_verdict(dict(metricCounter="M"), scene)
    bad += got != "FAIL"
    print(("ok   " if got == "FAIL" else "FAIL ") + f"scene not lower: {got}")
    print("self-test:", "PASS" if bad == 0 else f"{bad} FAILED")
    return 0 if bad == 0 else 1


if __name__ == "__main__":
    if len(sys.argv) == 2 and sys.argv[1] == "--self-test":
        sys.exit(self_test())
    if len(sys.argv) != 2:
        print(__doc__)
        sys.exit(64)
    v = judge(sys.argv[1])
    sys.exit({"PASS": 0, "FAIL": 1, "HOLD": 2}[v])
